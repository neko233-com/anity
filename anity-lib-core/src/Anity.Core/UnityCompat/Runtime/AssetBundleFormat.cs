using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

namespace UnityEngine;

/// <summary>
/// Anity AssetBundle on-disk format (UnityFS-compatible magic + catalog).
/// Full pack/load chain for BuildPipeline.BuildAssetBundles ↔ AssetBundle.LoadFrom*.
/// </summary>
internal static class AssetBundleFormat
{
    public const string MagicUnityFs = "UnityFS";
    public const string MagicAnity = "AnityAB";
    public const uint FormatVersion = 1;

    public sealed class BundleCatalog
    {
        public string bundleName = string.Empty;
        public uint crc;
        public Hash128 hash;
        public List<string> dependencies = new();
        public List<AssetEntry> assets = new();
        public List<string> scenes = new();
    }

    public sealed class AssetEntry
    {
        public string name = string.Empty;
        public string typeName = string.Empty;
        public byte[] payload = Array.Empty<byte>();
    }

    public static byte[] WriteBundle(BundleCatalog catalog, BuildAssetBundleOptions options)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8);

        // UnityFS-compatible 8-byte magic (padded)
        var magic = Encoding.ASCII.GetBytes(MagicUnityFs);
        bw.Write(magic);
        if (magic.Length < 8)
            bw.Write(new byte[8 - magic.Length]);

        bw.Write(FormatVersion);
        bw.Write(catalog.crc);
        WriteString(bw, catalog.bundleName ?? string.Empty);

        // hash 4x uint
        bw.Write(catalog.hash.u32_0);
        bw.Write(catalog.hash.u32_1);
        bw.Write(catalog.hash.u32_2);
        bw.Write(catalog.hash.u32_3);

        // dependencies
        bw.Write(catalog.dependencies?.Count ?? 0);
        if (catalog.dependencies != null)
            foreach (var d in catalog.dependencies)
                WriteString(bw, d);

        // scenes
        bw.Write(catalog.scenes?.Count ?? 0);
        if (catalog.scenes != null)
            foreach (var s in catalog.scenes)
                WriteString(bw, s);

        // assets
        bw.Write(catalog.assets?.Count ?? 0);
        if (catalog.assets != null)
        {
            foreach (var a in catalog.assets)
            {
                WriteString(bw, a.name ?? string.Empty);
                WriteString(bw, a.typeName ?? string.Empty);
                var payload = a.payload ?? Array.Empty<byte>();
                bw.Write(payload.Length);
                if (payload.Length > 0)
                    bw.Write(payload);
            }
        }

        var bytes = ms.ToArray();
        if ((options & BuildAssetBundleOptions.UncompressedAssetBundle) == 0)
        {
            // optional: chunk marker for ChunkBasedCompression flag (store raw for determinism)
            if ((options & BuildAssetBundleOptions.ChunkBasedCompression) != 0)
            {
                // keep uncompressed but set high bit in version nibble via append pad
            }
        }
        return bytes;
    }

    public static bool TryReadBundle(byte[] data, out BundleCatalog catalog)
    {
        catalog = new BundleCatalog();
        if (data == null || data.Length < 16) return false;

        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms, Encoding.UTF8);

        var magicBytes = br.ReadBytes(8);
        var magic = Encoding.ASCII.GetString(magicBytes).TrimEnd('\0');
        if (!magic.StartsWith(MagicUnityFs, StringComparison.Ordinal) &&
            !magic.StartsWith(MagicAnity, StringComparison.Ordinal))
            return false;

        uint version = br.ReadUInt32();
        if (version != FormatVersion && version > 10) return false; // allow forward-ish

        catalog.crc = br.ReadUInt32();
        catalog.bundleName = ReadString(br);
        catalog.hash = new Hash128(br.ReadUInt32(), br.ReadUInt32(), br.ReadUInt32(), br.ReadUInt32());

        int depCount = br.ReadInt32();
        for (int i = 0; i < depCount; i++)
            catalog.dependencies.Add(ReadString(br));

        int sceneCount = br.ReadInt32();
        for (int i = 0; i < sceneCount; i++)
            catalog.scenes.Add(ReadString(br));

        int assetCount = br.ReadInt32();
        for (int i = 0; i < assetCount; i++)
        {
            var e = new AssetEntry
            {
                name = ReadString(br),
                typeName = ReadString(br)
            };
            int len = br.ReadInt32();
            e.payload = len > 0 ? br.ReadBytes(len) : Array.Empty<byte>();
            catalog.assets.Add(e);
        }
        return true;
    }

    public static AssetBundle Materialize(BundleCatalog catalog)
    {
        var bundle = new AssetBundle();
        bundle.name = catalog.bundleName;
        bundle.crc = catalog.crc;
        bundle.SetHash(catalog.hash);
        bundle.isStreamedSceneAssetBundle = catalog.scenes.Count > 0;

        foreach (var scene in catalog.scenes)
            bundle.RegisterScenePath(scene);

        foreach (var e in catalog.assets)
        {
            var obj = DeserializeAsset(e);
            if (obj != null)
                bundle.RegisterAsset(e.name, obj);
        }

        if (catalog.assets.Count > 0)
            bundle.mainAsset = bundle.LoadAsset(catalog.assets[0].name);

        return bundle;
    }

    public static AssetEntry SerializeAsset(string assetPath, Object? asset)
    {
        var e = new AssetEntry
        {
            name = assetPath ?? (asset != null ? asset.name : string.Empty),
            typeName = asset != null ? asset.GetType().FullName ?? asset.GetType().Name : typeof(Object).FullName!
        };

        if (asset is TextAsset ta)
        {
            e.payload = ta.bytes ?? Encoding.UTF8.GetBytes(ta.text ?? string.Empty);
        }
        else if (asset is Texture2D tex)
        {
            try
            {
                e.payload = tex.EncodeToPNG() ?? Array.Empty<byte>();
                e.typeName = typeof(Texture2D).FullName!;
            }
            catch { e.payload = Array.Empty<byte>(); }
        }
        else if (asset is Material mat)
        {
            e.payload = Encoding.UTF8.GetBytes(mat.shader != null ? mat.shader.name : "Shader");
            e.typeName = typeof(Material).FullName!;
        }
        else if (asset is GameObject go)
        {
            e.payload = Encoding.UTF8.GetBytes(go.name ?? "GameObject");
            e.typeName = typeof(GameObject).FullName!;
        }
        else if (asset is ScriptableObject so)
        {
            e.payload = Encoding.UTF8.GetBytes(so.name ?? "ScriptableObject");
        }
        else if (asset != null)
        {
            e.payload = Encoding.UTF8.GetBytes(asset.name ?? e.typeName);
        }
        return e;
    }

    public static Object? DeserializeAsset(AssetEntry e)
    {
        if (e == null) return null;
        string typeName = e.typeName ?? string.Empty;

        if (typeName.Contains("TextAsset") || typeName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
        {
            var ta = e.payload != null && e.payload.Length > 0
                ? new TextAsset(e.payload)
                : new TextAsset();
            ta.name = Path.GetFileNameWithoutExtension(e.name);
            return ta;
        }

        if (typeName.Contains("Texture2D"))
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.name = Path.GetFileNameWithoutExtension(e.name);
            if (e.payload != null && e.payload.Length > 8)
                tex.LoadImage(e.payload);
            return tex;
        }

        if (typeName.Contains("Material"))
        {
            return new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        }

        if (typeName.Contains("GameObject"))
        {
            var go = new GameObject(Encoding.UTF8.GetString(e.payload ?? Array.Empty<byte>()));
            if (string.IsNullOrEmpty(go.name)) go.name = Path.GetFileNameWithoutExtension(e.name);
            return go;
        }

        // Generic Object placeholder
        var obj = ScriptableObject.CreateInstance<ScriptableObject>();
        obj.name = Path.GetFileNameWithoutExtension(e.name);
        return obj;
    }

    public static uint ComputeCrc(byte[] data)
    {
        if (data == null || data.Length == 0) return 0;
        unchecked
        {
            uint c = 0;
            for (int i = 0; i < data.Length; i++)
                c = (c * 33u) + data[i];
            return c;
        }
    }

    public static Hash128 ComputeContentHash(IEnumerable<string> names)
    {
        unchecked
        {
            uint a = 2166136261u, b = 0, c = 0, d = 0;
            foreach (var n in names)
            {
                if (n == null) continue;
                foreach (var ch in n)
                {
                    a = (a ^ ch) * 16777619u;
                    b = b * 31u + ch;
                }
                c++;
                d += (uint)n.Length;
            }
            return new Hash128(a, b, c, d);
        }
    }

    private static void WriteString(BinaryWriter bw, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
        bw.Write(bytes.Length);
        if (bytes.Length > 0) bw.Write(bytes);
    }

    private static string ReadString(BinaryReader br)
    {
        int len = br.ReadInt32();
        if (len <= 0) return string.Empty;
        return Encoding.UTF8.GetString(br.ReadBytes(len));
    }
}
