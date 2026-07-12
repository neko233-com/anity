using System;
using System.Collections.Generic;
using System.IO;

namespace UnityEngine;

public class AssetBundle : Object
{
    private static readonly HashSet<AssetBundle> _allLoadedBundles = new();
    private readonly Dictionary<string, Object> _assets = new();
    private Hash128 _hash;
    private Object? _mainAsset;
    private readonly Dictionary<string, string> _scenePaths = new();

    public Hash128 hash => _hash;
    public bool isStreamedSceneAssetBundle { get; set; }
    public uint crc { get; internal set; }

    internal void SetHash(Hash128 hash) => _hash = hash;
    public Object? mainAsset
    {
        get => _mainAsset;
        set => _mainAsset = value;
    }

    public AssetBundle()
    {
        _allLoadedBundles.Add(this);
    }

    internal void RegisterAsset(string assetName, Object asset)
    {
        if (string.IsNullOrEmpty(assetName) || asset == null) return;
        _assets[assetName] = asset;
        asset.name = assetName;
    }

    internal void RegisterScenePath(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return;
        _scenePaths[scenePath] = scenePath;
    }

    public bool Contains(string name)
    {
        return !string.IsNullOrEmpty(name) && _assets.ContainsKey(name);
    }

    public Object? LoadAsset(string name)
    {
        return LoadAsset(name, typeof(Object));
    }

    public T? LoadAsset<T>(string name) where T : Object
    {
        return LoadAsset(name, typeof(T)) as T;
    }

    public Object? LoadAsset(string name, Type type)
    {
        if (string.IsNullOrEmpty(name) || !_assets.TryGetValue(name, out var asset))
            return null;
        if (type != null && !type.IsInstanceOfType(asset))
            return null;
        return asset;
    }

    public AssetBundleRequest LoadAssetAsync<T>(string name) where T : Object
    {
        return LoadAssetAsync(name, typeof(T));
    }

    public AssetBundleRequest LoadAssetAsync(string name)
    {
        return LoadAssetAsync(name, typeof(Object));
    }

    public AssetBundleRequest LoadAssetAsync(string name, Type type)
    {
        var request = new AssetBundleRequest();
        var asset = LoadAsset(name, type);
        request.SetAsset(asset);
        request.SetAllAssets(asset != null ? new[] { asset } : Array.Empty<Object>());
        request.SetDone();
        return request;
    }

    public Object[] LoadAllAssets()
    {
        return LoadAllAssets(typeof(Object));
    }

    public T[] LoadAllAssets<T>() where T : Object
    {
        var assets = new List<T>();
        foreach (var kvp in _assets)
        {
            if (kvp.Value is T typed)
                assets.Add(typed);
        }
        return assets.ToArray();
    }

    public Object[] LoadAllAssets(Type type)
    {
        var assets = new List<Object>();
        foreach (var kvp in _assets)
        {
            if (type == null || type.IsInstanceOfType(kvp.Value))
                assets.Add(kvp.Value);
        }
        return assets.ToArray();
    }

    public AssetBundleRequest LoadAllAssetsAsync()
    {
        return LoadAllAssetsAsync(typeof(Object));
    }

    public AssetBundleRequest LoadAllAssetsAsync<T>() where T : Object
    {
        return LoadAllAssetsAsync(typeof(T));
    }

    public AssetBundleRequest LoadAllAssetsAsync(Type type)
    {
        var request = new AssetBundleRequest();
        var allAssets = LoadAllAssets(type);
        request.SetAllAssets(allAssets);
        if (allAssets.Length > 0)
            request.SetAsset(allAssets[0]);
        request.SetDone();
        return request;
    }

    public Object[] LoadAssetWithSubAssets(string name)
    {
        return LoadAssetWithSubAssets(name, typeof(Object));
    }

    public T[] LoadAssetWithSubAssets<T>(string name) where T : Object
    {
        var all = LoadAssetWithSubAssets(name, typeof(T));
        var result = new T[all.Length];
        for (int i = 0; i < all.Length; i++)
            result[i] = (T)all[i];
        return result;
    }

    public Object[] LoadAssetWithSubAssets(string name, Type type)
    {
        var results = new List<Object>();
        foreach (var kvp in _assets)
        {
            if (kvp.Key.StartsWith(name, StringComparison.Ordinal) &&
                (type == null || type.IsInstanceOfType(kvp.Value)))
            {
                results.Add(kvp.Value);
            }
        }
        return results.ToArray();
    }

    public AssetBundleRequest LoadAssetWithSubAssetsAsync(string name)
    {
        return LoadAssetWithSubAssetsAsync(name, typeof(Object));
    }

    public AssetBundleRequest LoadAssetWithSubAssetsAsync<T>(string name) where T : Object
    {
        return LoadAssetWithSubAssetsAsync(name, typeof(T));
    }

    public AssetBundleRequest LoadAssetWithSubAssetsAsync(string name, Type type)
    {
        var request = new AssetBundleRequest();
        var assets = LoadAssetWithSubAssets(name, type);
        request.SetAllAssets(assets);
        if (assets.Length > 0)
            request.SetAsset(assets[0]);
        request.SetDone();
        return request;
    }

    public string[] GetAllAssetNames()
    {
        var names = new string[_assets.Count];
        _assets.Keys.CopyTo(names, 0);
        return names;
    }

    public string[] GetAllScenePaths()
    {
        var paths = new string[_scenePaths.Count];
        _scenePaths.Keys.CopyTo(paths, 0);
        return paths;
    }

    public void Unload(bool unloadAllLoadedObjects)
    {
        if (unloadAllLoadedObjects)
        {
            foreach (var asset in _assets.Values)
            {
                Destroy(asset);
            }
            _assets.Clear();
        }
        _allLoadedBundles.Remove(this);
    }

    public AssetBundleUnloadOperation UnloadAsync(bool unloadAllLoadedObjects)
    {
        var op = new AssetBundleUnloadOperation();
        Unload(unloadAllLoadedObjects);
        op.SetDone();
        return op;
    }

    public static AssetBundle? LoadFromFile(string path)
    {
        return LoadFromFile(path, 0, 0);
    }

    public static AssetBundle? LoadFromFile(string path, uint crc)
    {
        return LoadFromFile(path, crc, 0);
    }

    public static AssetBundle? LoadFromFile(string path, uint crc, ulong offset)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            byte[] data;
            using (var fs = File.OpenRead(path))
            {
                if (offset > 0 && (long)offset < fs.Length)
                    fs.Seek((long)offset, SeekOrigin.Begin);
                using var ms = new MemoryStream();
                fs.CopyTo(ms);
                data = ms.ToArray();
            }
            return LoadFromMemory(data, crc);
        }
        catch
        {
            return null;
        }
    }

    public static AssetBundleCreateRequest LoadFromFileAsync(string path)
    {
        return LoadFromFileAsync(path, 0, 0);
    }

    public static AssetBundleCreateRequest LoadFromFileAsync(string path, uint crc)
    {
        return LoadFromFileAsync(path, crc, 0);
    }

    public static AssetBundleCreateRequest LoadFromFileAsync(string path, uint crc, ulong offset)
    {
        var request = new AssetBundleCreateRequest();
        var bundle = LoadFromFile(path, crc, offset);
        request.SetAssetBundle(bundle);
        request.SetDone();
        return request;
    }

    public static AssetBundle? LoadFromMemory(byte[] binary)
    {
        return LoadFromMemory(binary, 0);
    }

    public static AssetBundle? LoadFromMemory(byte[] binary, uint crc)
    {
        if (binary == null || binary.Length == 0) return null;

        if (AssetBundleFormat.TryReadBundle(binary, out var catalog))
        {
            if (crc != 0 && catalog.crc != 0 && catalog.crc != crc)
                return null; // CRC mismatch (Unity rejects)
            var bundle = AssetBundleFormat.Materialize(catalog);
            if (crc != 0) bundle.crc = crc;
            return bundle;
        }

        // Fallback empty catalog bundle (legacy / non-Anity payload)
        var empty = new AssetBundle();
        empty.name = "MemoryBundle_" + DateTime.Now.Ticks;
        empty.crc = crc;
        empty._hash = ComputeHash(binary);
        return empty;
    }

    public static AssetBundleCreateRequest LoadFromMemoryAsync(byte[] binary)
    {
        return LoadFromMemoryAsync(binary, 0);
    }

    public static AssetBundleCreateRequest LoadFromMemoryAsync(byte[] binary, uint crc)
    {
        var request = new AssetBundleCreateRequest();
        var bundle = LoadFromMemory(binary, crc);
        request.SetAssetBundle(bundle);
        request.SetDone();
        return request;
    }

    public static AssetBundle? LoadFromStream(Stream stream)
    {
        return LoadFromStream(stream, 0, 0);
    }

    public static AssetBundle? LoadFromStream(Stream stream, uint crc)
    {
        return LoadFromStream(stream, crc, 0);
    }

    public static AssetBundle? LoadFromStream(Stream stream, uint crc, uint managedReadBufferSize)
    {
        _ = managedReadBufferSize;
        if (stream == null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return LoadFromMemory(ms.ToArray(), crc);
    }

    public static AssetBundleCreateRequest LoadFromStreamAsync(Stream stream)
    {
        return LoadFromStreamAsync(stream, 0, 0);
    }

    public static AssetBundleCreateRequest LoadFromStreamAsync(Stream stream, uint crc)
    {
        return LoadFromStreamAsync(stream, crc, 0);
    }

    public static AssetBundleCreateRequest LoadFromStreamAsync(Stream stream, uint crc, uint managedReadBufferSize)
    {
        var request = new AssetBundleCreateRequest();
        var bundle = LoadFromStream(stream, crc, managedReadBufferSize);
        request.SetAssetBundle(bundle);
        request.SetDone();
        return request;
    }

    public static AssetBundle[] GetAllLoadedAssetBundles()
    {
        var bundles = new AssetBundle[_allLoadedBundles.Count];
        _allLoadedBundles.CopyTo(bundles);
        return bundles;
    }

    public static void UnloadAllAssetBundles(bool unloadAllObjects)
    {
        var bundles = GetAllLoadedAssetBundles();
        foreach (var bundle in bundles)
        {
            bundle.Unload(unloadAllObjects);
        }
    }

    private static Hash128 ComputeHash(string data)
    {
        if (string.IsNullOrEmpty(data)) return default;
        int h = data.GetHashCode();
        return new Hash128((uint)h, (uint)data.Length, 0, 0);
    }

    private static Hash128 ComputeHash(byte[] data)
    {
        if (data == null || data.Length == 0) return default;
        unchecked
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < Math.Min(data.Length, 256); i++)
            {
                h ^= data[i];
                h *= 1099511628211UL;
            }
            return new Hash128((uint)h, (uint)(h >> 32), (uint)data.Length, 0);
        }
    }
}

public class AssetBundleCreateRequest : AsyncOperation
{
    private AssetBundle? _assetBundle;

    public AssetBundle? assetBundle => _assetBundle;

    public AssetBundleCreateRequest() : base(false)
    {
    }

    internal void SetAssetBundle(AssetBundle? bundle)
    {
        _assetBundle = bundle;
    }

    public AssetBundleCreateRequest GetAwaiter()
    {
        return this;
    }

    public bool IsCompleted => isDone;

    public AssetBundle? GetResult()
    {
        return _assetBundle;
    }
}

public class AssetBundleRequest : AsyncOperation
{
    private Object? _asset;
    private Object[] _allAssets = Array.Empty<Object>();

    public Object? asset => _asset;

    public T? assetAsTyped<T>() where T : Object
    {
        return _asset as T;
    }

    public Object[] allAssets => _allAssets;

    public AssetBundleRequest() : base(false)
    {
    }

    internal void SetAsset(Object? asset)
    {
        _asset = asset;
    }

    internal void SetAllAssets(Object[] assets)
    {
        _allAssets = assets ?? Array.Empty<Object>();
    }

    public AssetBundleRequest GetAwaiter()
    {
        return this;
    }

    public bool IsCompleted => isDone;

    public Object? GetResult()
    {
        return _asset;
    }
}

public class AssetBundleUnloadOperation : AsyncOperation
{
    public AssetBundleUnloadOperation() : base(false)
    {
    }

    internal void SetDone()
    {
        isDone = true;
    }

    public AssetBundleUnloadOperation GetAwaiter()
    {
        return this;
    }

    public bool IsCompleted => isDone;

    public void GetResult()
    {
    }
}

public class AssetBundleManifest : Object
{
    private readonly Dictionary<string, Hash128> _bundleHashes = new();
    private readonly Dictionary<string, string[]> _bundleDependencies = new();
    private readonly Dictionary<string, string[]> _bundleVariants = new();
    private readonly List<string> _allBundles = new();

    internal void AddBundle(string bundleName, Hash128 hash, string[]? dependencies)
    {
        if (string.IsNullOrEmpty(bundleName)) return;
        if (!_allBundles.Contains(bundleName))
            _allBundles.Add(bundleName);
        _bundleHashes[bundleName] = hash;
        _bundleDependencies[bundleName] = dependencies ?? Array.Empty<string>();
    }

    internal void AddBundleWithVariant(string bundleName, string variant, Hash128 hash)
    {
        if (string.IsNullOrEmpty(bundleName)) return;
        if (!_allBundles.Contains(bundleName))
            _allBundles.Add(bundleName);
        _bundleHashes[bundleName] = hash;
        if (_bundleVariants.TryGetValue(bundleName, out var existing))
        {
            var list = new List<string>(existing) { variant };
            _bundleVariants[bundleName] = list.ToArray();
        }
        else
        {
            _bundleVariants[bundleName] = new[] { variant };
        }
    }

    public string[] GetAllAssetBundles()
    {
        return _allBundles.ToArray();
    }

    public string[] GetAllAssetBundlesWithVariant()
    {
        var result = new List<string>();
        foreach (var kvp in _bundleVariants)
        {
            foreach (var variant in kvp.Value)
            {
                result.Add($"{kvp.Key}.{variant}");
            }
        }
        return result.ToArray();
    }

    public string[] GetAllDependencies(string assetBundleName)
    {
        var result = new HashSet<string>();
        GetDependenciesRecursive(assetBundleName, result);
        result.Remove(assetBundleName);
        return result.ToArray();
    }

    public string[] GetDirectDependencies(string assetBundleName)
    {
        return _bundleDependencies.TryGetValue(assetBundleName, out var deps) ? (string[])deps.Clone() : Array.Empty<string>();
    }

    public Hash128 GetAssetBundleHash(string assetBundleName)
    {
        return _bundleHashes.TryGetValue(assetBundleName, out var hash) ? hash : default;
    }

    [Obsolete("Use GetAssetBundleHash instead")]
    public Hash128 GetHash(string assetBundleName)
    {
        return GetAssetBundleHash(assetBundleName);
    }

    private void GetDependenciesRecursive(string bundleName, HashSet<string> result)
    {
        if (string.IsNullOrEmpty(bundleName) || result.Contains(bundleName)) return;
        result.Add(bundleName);
        if (_bundleDependencies.TryGetValue(bundleName, out var deps))
        {
            foreach (var dep in deps)
            {
                GetDependenciesRecursive(dep, result);
            }
        }
    }
}
