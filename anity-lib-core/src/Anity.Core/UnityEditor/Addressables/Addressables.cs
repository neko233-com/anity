using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace UnityEditor.AddressableAssets;

/// <summary>
/// Addressables runtime catalog + load API (Unity Addressables subset, 2022.3-aligned surface).
/// Independent of engine packaging; maps address → AssetDatabase path or AssetBundle asset.
/// </summary>
public static class Addressables
{
    private static readonly object _lock = new();
    private static readonly Dictionary<string, string> _addressToPath = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _addressToBundle = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, AssetBundle> _loadedBundles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, HashSet<string>> _addressLabels = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, HashSet<string>> _labelToAddresses = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, HashSet<string>> _dependencies = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<IResourceLocator> _locators = new();
    private static readonly HashSet<object> _handles = new();

    public static string RuntimePath { get; set; } = "ServerData";
    public static string PlayerBuildDataPath => Path.Combine(Application.streamingAssetsPath, "aa");
    public static string BuildPath => Path.Combine("Library", "com.unity.addressables", "aa");

    public static event Action<AsyncOperationHandle>? ResourceManagerException;

    /// <summary>Register address → project asset path (editor / player bootstrap).</summary>
    public static void Register(string address, string assetPath)
    {
        if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(assetPath)) return;
        lock (_lock) _addressToPath[address] = assetPath;
    }

    public static void RegisterBundle(string address, string bundlePath)
    {
        if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(bundlePath)) return;
        lock (_lock) _addressToBundle[address] = bundlePath;
    }

    /// <summary>Attach label(s) to an address (Unity Addressables label).</summary>
    public static void AddLabel(string address, string label)
    {
        if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(label)) return;
        lock (_lock)
        {
            if (!_addressLabels.TryGetValue(address, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _addressLabels[address] = set;
            }
            set.Add(label);
            if (!_labelToAddresses.TryGetValue(label, out var addrs))
            {
                addrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _labelToAddresses[label] = addrs;
            }
            addrs.Add(address);
        }
    }

    public static void AddLabels(string address, IEnumerable<string> labels)
    {
        if (labels == null) return;
        foreach (var l in labels) AddLabel(address, l);
    }

    public static IReadOnlyCollection<string> GetLabels(string address)
    {
        lock (_lock)
        {
            if (_addressLabels.TryGetValue(address, out var set))
                return set.ToList();
            return Array.Empty<string>();
        }
    }

    public static IReadOnlyCollection<string> GetAddressesWithLabel(string label)
    {
        lock (_lock)
        {
            if (_labelToAddresses.TryGetValue(label, out var set))
                return set.ToList();
            return Array.Empty<string>();
        }
    }

    /// <summary>address depends on depAddress (must load deps first).</summary>
    public static void AddDependency(string address, string depAddress)
    {
        if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(depAddress)) return;
        lock (_lock)
        {
            if (!_dependencies.TryGetValue(address, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _dependencies[address] = set;
            }
            set.Add(depAddress);
        }
    }

    public static IReadOnlyList<string> GetDependencies(string address, bool recursive = false)
    {
        lock (_lock)
        {
            if (!recursive)
            {
                if (_dependencies.TryGetValue(address, out var direct))
                    return direct.ToList();
                return Array.Empty<string>();
            }
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Walk(string a)
            {
                if (!_dependencies.TryGetValue(a, out var deps)) return;
                foreach (var d in deps)
                {
                    if (!seen.Add(d)) continue;
                    result.Add(d);
                    Walk(d);
                }
            }
            Walk(address);
            return result;
        }
    }

    public static void ClearCatalog()
    {
        lock (_lock)
        {
            _addressToPath.Clear();
            _addressToBundle.Clear();
            _addressLabels.Clear();
            _labelToAddresses.Clear();
            _dependencies.Clear();
            _locators.Clear();
            foreach (var ab in _loadedBundles.Values)
            {
                try { ab?.Unload(false); } catch { }
            }
            _loadedBundles.Clear();
        }
    }

    public static bool Exists(string address)
    {
        lock (_lock)
            return _addressToPath.ContainsKey(address) || _addressToBundle.ContainsKey(address);
    }

    public static AsyncOperationHandle InitializeAsync()
    {
        var h = new AsyncOperationHandle();
        h.Complete(true, null);
        return h;
    }

    public static AsyncOperationHandle<T> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
    {
        var handle = new AsyncOperationHandle<T>();
        try
        {
            var obj = LoadSync(address);
            handle.Complete(obj as T, obj != null ? null : new Exception("Address not found: " + address));
        }
        catch (Exception ex)
        {
            handle.Complete(default, ex);
            ResourceManagerException?.Invoke(handle);
        }
        return handle;
    }

    public static AsyncOperationHandle<T> LoadAssetAsync<T>(AssetReference assetReference) where T : UnityEngine.Object
    {
        string key = assetReference?.AssetGUID ?? string.Empty;
        if (string.IsNullOrEmpty(key) && assetReference != null)
            key = assetReference.ToString();
        return LoadAssetAsync<T>(key);
    }

    public static AsyncOperationHandle<IList<T>> LoadAssetsAsync<T>(string address, Action<T>? callback) where T : UnityEngine.Object
    {
        var handle = new AsyncOperationHandle<IList<T>>();
        var list = new List<T>();
        var obj = LoadSync(address) as T;
        if (obj != null)
        {
            list.Add(obj);
            callback?.Invoke(obj);
        }
        handle.Complete(list, list.Count > 0 ? null : new Exception("Address not found: " + address));
        return handle;
    }

    public static AsyncOperationHandle<IList<T>> LoadAssetsAsync<T>(IList<object> keys, Action<T>? callback, Addressables.MergeMode mode)
        where T : UnityEngine.Object
    {
        var handle = new AsyncOperationHandle<IList<T>>();
        var list = new List<T>();
        if (keys != null && keys.Count > 0)
        {
            // Expand labels → addresses, then merge by mode
            var addressSets = new List<HashSet<string>>();
            foreach (var k in keys)
            {
                string key = k?.ToString() ?? string.Empty;
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                // if key is a label, expand
                var byLabel = GetAddressesWithLabel(key);
                if (byLabel.Count > 0)
                    foreach (var a in byLabel) set.Add(a);
                else if (Exists(key))
                    set.Add(key);
                addressSets.Add(set);
            }

            HashSet<string> merged = MergeAddressSets(addressSets, mode);
            foreach (var addr in merged)
            {
                // load deps first
                foreach (var dep in GetDependencies(addr, recursive: true))
                    LoadSync(dep);
                var o = LoadSync(addr) as T;
                if (o != null)
                {
                    list.Add(o);
                    callback?.Invoke(o);
                }
            }
        }
        handle.Complete(list, null);
        return handle;
    }

    /// <summary>Load all assets tagged with label.</summary>
    public static AsyncOperationHandle<IList<T>> LoadAssetsByLabelAsync<T>(string label, Action<T>? callback = null)
        where T : UnityEngine.Object
    {
        return LoadAssetsAsync(new List<object> { label }, callback, MergeMode.Union);
    }

    private static HashSet<string> MergeAddressSets(List<HashSet<string>> sets, MergeMode mode)
    {
        if (sets.Count == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (mode == MergeMode.UseFirst || mode == MergeMode.None)
            return new HashSet<string>(sets[0], StringComparer.OrdinalIgnoreCase);
        if (mode == MergeMode.Intersection)
        {
            var acc = new HashSet<string>(sets[0], StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < sets.Count; i++)
                acc.IntersectWith(sets[i]);
            return acc;
        }
        // Union
        var u = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sets)
            foreach (var a in s) u.Add(a);
        return u;
    }

    public enum MergeMode
    {
        None = 0,
        UseFirst = 1,
        Union = 2,
        Intersection = 3
    }

    public static AsyncOperationHandle<GameObject> InstantiateAsync(string address)
        => InstantiateAsync(address, null);

    public static AsyncOperationHandle<GameObject> InstantiateAsync(string address, Transform? parent)
    {
        var handle = new AsyncOperationHandle<GameObject>();
        var src = LoadSync(address) as GameObject;
        GameObject? inst = null;
        if (src != null)
        {
            inst = UnityEngine.Object.Instantiate(src);
            if (parent != null && inst != null)
                inst.transform.SetParent(parent, false);
        }
        handle.Complete(inst, inst != null ? null : new Exception("Instantiate failed: " + address));
        return handle;
    }

    public static AsyncOperationHandle<GameObject> InstantiateAsync(string address, Vector3 position, Quaternion rotation)
        => InstantiateAsync(address, position, rotation, null);

    public static AsyncOperationHandle<GameObject> InstantiateAsync(string address, Vector3 position, Quaternion rotation, Transform? parent)
    {
        var h = InstantiateAsync(address, parent);
        if (h.Result != null)
        {
            h.Result.transform.position = position;
            h.Result.transform.rotation = rotation;
        }
        return h;
    }

    public static void Release<T>(AsyncOperationHandle<T> handle)
    {
        _ = handle;
        // Soft release — full refcount later
    }

    public static void Release(UnityEngine.Object obj)
    {
        if (obj is GameObject go)
            UnityEngine.Object.Destroy(go);
    }

    public static bool ReleaseInstance(GameObject instance)
    {
        if (instance == null) return false;
        UnityEngine.Object.Destroy(instance);
        return true;
    }

    public static AsyncOperationHandle<IResourceLocator> LoadContentCatalogAsync(string catalogPath)
    {
        var handle = new AsyncOperationHandle<IResourceLocator>();
        try
        {
            var locator = LoadCatalogFile(catalogPath);
            if (locator != null)
            {
                lock (_lock) _locators.Add(locator);
                handle.Complete(locator, null);
            }
            else
                handle.Complete(null, new Exception("Catalog load failed: " + catalogPath));
        }
        catch (Exception ex)
        {
            handle.Complete(null, ex);
        }
        return handle;
    }

    public static AsyncOperationHandle<IList<IResourceLocator>> UpdateCatalogs(IEnumerable<string>? catalogs = null, bool autoReleaseHandle = true)
    {
        var handle = new AsyncOperationHandle<IList<IResourceLocator>>();
        var list = new List<IResourceLocator>(_locators);
        if (catalogs != null)
        {
            foreach (var c in catalogs)
            {
                var loc = LoadCatalogFile(c);
                if (loc != null)
                {
                    lock (_lock) _locators.Add(loc);
                    list.Add(loc);
                }
            }
        }
        handle.Complete(list, null);
        return handle;
    }

    public static IList<IResourceLocator> ResourceLocators
    {
        get { lock (_lock) return _locators.ToList(); }
    }

    /// <summary>Write a simple catalog JSON: { "address": "Assets/path.ext", ... }</summary>
    public static void WriteCatalog(string path, IDictionary<string, string> addressToAssetPath)
    {
        if (string.IsNullOrEmpty(path) || addressToAssetPath == null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        using var sw = new StreamWriter(path);
        sw.WriteLine("{");
        bool first = true;
        foreach (var kvp in addressToAssetPath)
        {
            if (!first) sw.WriteLine(",");
            sw.Write($"  \"{Escape(kvp.Key)}\": \"{Escape(kvp.Value)}\"");
            first = false;
        }
        sw.WriteLine();
        sw.WriteLine("}");
    }

    public static void BuildPlayerContent(string outputDirectory, IDictionary<string, string> addressToAssetPath)
    {
        if (string.IsNullOrEmpty(outputDirectory)) return;
        Directory.CreateDirectory(outputDirectory);
        WriteCatalog(Path.Combine(outputDirectory, "catalog.json"), addressToAssetPath);
        // Also register into runtime for current process
        foreach (var kvp in addressToAssetPath)
            Register(kvp.Key, kvp.Value);
    }

    /// <summary>Unity-style: download remote deps (local bundles = size 0 / success). Resolves dependency graph.</summary>
    public static AsyncOperationHandle DownloadDependenciesAsync(string address, bool autoReleaseHandle = false)
    {
        _ = autoReleaseHandle;
        var h = new AsyncOperationHandle();
        if (string.IsNullOrEmpty(address))
        {
            h.Complete(false, new Exception("Empty address"));
            return h;
        }
        // Ensure deps loadable
        var deps = GetDependencies(address, recursive: true);
        foreach (var d in deps)
            LoadSync(d);
        bool ok = Exists(address) || deps.Count > 0;
        if (ok) LoadSync(address);
        h.Complete(ok, ok ? null : new Exception("Unknown address: " + address));
        return h;
    }

    public static AsyncOperationHandle DownloadDependenciesAsync(IList<object> keys, bool autoReleaseHandle = false)
    {
        _ = autoReleaseHandle;
        var h = new AsyncOperationHandle();
        bool any = keys != null && keys.Count > 0;
        h.Complete(any, null);
        return h;
    }

    public static AsyncOperationHandle<long> GetDownloadSizeAsync(string address)
    {
        var h = new AsyncOperationHandle<long>();
        long size = 0;
        lock (_lock)
        {
            if (_addressToBundle.TryGetValue(address, out var bp) && File.Exists(bp))
                size = new FileInfo(bp).Length;
        }
        h.Complete(size, null);
        return h;
    }

    public static AsyncOperationHandle<long> GetDownloadSizeAsync(IList<object> keys)
    {
        var h = new AsyncOperationHandle<long>();
        long total = 0;
        if (keys != null)
        {
            foreach (var k in keys)
            {
                var sub = GetDownloadSizeAsync(k?.ToString() ?? string.Empty);
                total += sub.Result;
            }
        }
        h.Complete(total, null);
        return h;
    }

    /// <summary>Load scene by address (sync catalog path; returns handle with scene name).</summary>
    public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(string address, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true)
    {
        var handle = new AsyncOperationHandle<SceneInstance>();
        try
        {
            var obj = LoadSync(address);
            string sceneName = address;
            if (obj != null && !string.IsNullOrEmpty(obj.name)) sceneName = obj.name;
            Scene scene;
            try
            {
                if (activateOnLoad)
                    SceneManager.LoadScene(sceneName, mode);
                scene = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(scene.name))
                    scene = new Scene(sceneName, -1, activateOnLoad);
            }
            catch
            {
                scene = new Scene(sceneName, -1, activateOnLoad);
            }
            var inst = new SceneInstance
            {
                Scene = scene,
                ActivateOnLoad = activateOnLoad,
                Mode = mode
            };
            handle.Complete(inst, null);
        }
        catch (Exception ex)
        {
            handle.Complete(default, ex);
        }
        return handle;
    }

    public static bool HasCatalogLoaded
    {
        get { lock (_lock) return _locators.Count > 0; }
    }

    private static ResourceLocator? LoadCatalogFile(string catalogPath)
    {
        if (string.IsNullOrEmpty(catalogPath) || !File.Exists(catalogPath)) return null;
        var text = File.ReadAllText(catalogPath);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // minimal JSON object parse "key":"value"
        int i = 0;
        while (i < text.Length)
        {
            int k0 = text.IndexOf('"', i);
            if (k0 < 0) break;
            int k1 = text.IndexOf('"', k0 + 1);
            if (k1 < 0) break;
            string key = text.Substring(k0 + 1, k1 - k0 - 1);
            int c = text.IndexOf(':', k1);
            if (c < 0) break;
            int v0 = text.IndexOf('"', c);
            if (v0 < 0) break;
            int v1 = text.IndexOf('"', v0 + 1);
            if (v1 < 0) break;
            string val = text.Substring(v0 + 1, v1 - v0 - 1);
            map[key] = val;
            Register(key, val);
            i = v1 + 1;
        }
        return new ResourceLocator(map);
    }

    private static UnityEngine.Object? LoadSync(string address)
    {
        if (string.IsNullOrEmpty(address)) return null;
        string path;
        string bundlePath;
        lock (_lock)
        {
            _addressToPath.TryGetValue(address, out path);
            _addressToBundle.TryGetValue(address, out bundlePath);
        }

        if (!string.IsNullOrEmpty(path))
        {
            var fromDb = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (fromDb != null) return fromDb;
            // file system fallback for tests
            if (File.Exists(path) && path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                return new TextAsset(File.ReadAllText(path)) { name = Path.GetFileNameWithoutExtension(path) };
        }

        if (!string.IsNullOrEmpty(bundlePath))
        {
            AssetBundle ab;
            lock (_lock)
            {
                if (!_loadedBundles.TryGetValue(bundlePath, out ab))
                {
                    ab = AssetBundle.LoadFromFile(bundlePath);
                    if (ab != null) _loadedBundles[bundlePath] = ab;
                }
            }
            if (ab != null)
            {
                var names = ab.GetAllAssetNames();
                if (names.Length > 0)
                    return ab.LoadAsset(names[0]);
            }
        }

        // treat address as direct asset path
        return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(address);
    }

    private static string Escape(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
}

public interface IResourceLocator
{
    bool Locate(object key, Type type, out IList<object> locations);
    IEnumerable<object> Keys { get; }
}

internal sealed class ResourceLocator : IResourceLocator
{
    private readonly Dictionary<string, string> _map;
    public ResourceLocator(Dictionary<string, string> map) => _map = map ?? new();
    public IEnumerable<object> Keys => _map.Keys.Cast<object>();
    public bool Locate(object key, Type type, out IList<object> locations)
    {
        locations = new List<object>();
        string k = key?.ToString() ?? string.Empty;
        if (_map.TryGetValue(k, out var path))
        {
            locations.Add(path);
            return true;
        }
        return false;
    }
}

[Serializable]
public class AssetReference
{
    public string AssetGUID;
    public string SubObjectName;

    public AssetReference() { }
    public AssetReference(string guid) { AssetGUID = guid; }

    public AsyncOperationHandle<T> LoadAssetAsync<T>() where T : UnityEngine.Object
        => Addressables.LoadAssetAsync<T>(this);

    public AsyncOperationHandle<GameObject> InstantiateAsync()
        => Addressables.InstantiateAsync(AssetGUID);

    public bool RuntimeKeyIsValid() => !string.IsNullOrEmpty(AssetGUID);
    public override string ToString() => AssetGUID ?? string.Empty;
}

[Serializable]
public struct AssetReferenceT<T> where T : UnityEngine.Object
{
    public AssetReference Reference;
    public AsyncOperationHandle<T> LoadAssetAsync() => Reference.LoadAssetAsync<T>();
}

public struct AsyncOperationHandle
{
    private bool _isDone;
    private Exception? _exception;
    public bool IsDone => _isDone;
    public Exception? OperationException => _exception;
    public float PercentComplete => _isDone ? 1f : 0f;
    public bool IsValid() => true;
    public event Action<AsyncOperationHandle>? Completed;

    internal void Complete(bool ok, Exception? ex)
    {
        _isDone = true;
        _exception = ex;
        Completed?.Invoke(this);
    }

    public AsyncOperationHandle WaitForCompletion()
    {
        _isDone = true;
        return this;
    }
}

public struct AsyncOperationHandle<T>
{
    private T? _result;
    private bool _isDone;
    private float _percentComplete;
    private Exception? _operationException;

    public T Result => _result!;
    public bool IsDone => _isDone;
    public float PercentComplete => _percentComplete;
    public Exception? OperationException => _operationException;
    public AsyncOperationStatus Status =>
        !_isDone ? AsyncOperationStatus.None
        : _operationException != null ? AsyncOperationStatus.Failed
        : AsyncOperationStatus.Succeeded;

    public bool IsValid() => true;
    public event Action<AsyncOperationHandle<T>>? Completed;

    internal void Complete(T? result, Exception? ex)
    {
        _result = result;
        _operationException = ex;
        _isDone = true;
        _percentComplete = 1f;
        Completed?.Invoke(this);
    }

    public AsyncOperationHandle<T> WaitForCompletion()
    {
        _isDone = true;
        _percentComplete = 1f;
        return this;
    }

    public static implicit operator AsyncOperationHandle(AsyncOperationHandle<T> h)
    {
        var n = new AsyncOperationHandle();
        n.Complete(h.IsDone && h.OperationException == null, h.OperationException);
        return n;
    }
}

public enum AsyncOperationStatus
{
    None = 0,
    Succeeded = 1,
    Failed = 2
}

/// <summary>Addressables scene load result (Unity.Addressables.SceneInstance-aligned).</summary>
public struct SceneInstance
{
    public Scene Scene;
    public bool ActivateOnLoad;
    public LoadSceneMode Mode;

    public override string ToString() => Scene.name ?? string.Empty;
}
