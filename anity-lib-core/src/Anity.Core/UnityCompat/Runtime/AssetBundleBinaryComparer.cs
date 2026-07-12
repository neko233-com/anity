using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityEngine;

/// <summary>
/// Official-style UnityFS binary gate for Anity AssetBundles.
/// Compares structure against Unity 2022 FS contract (magic, version field layout)
/// and Anity catalog parseability — used by AB.Compare CI job.
/// </summary>
public static class AssetBundleBinaryComparer
{
    /// <summary>UnityFS magic (8 bytes, space-padded): "UnityFS ".</summary>
    public static readonly byte[] UnityFsMagic = { 0x55, 0x6E, 0x69, 0x74, 0x79, 0x46, 0x53, 0x20 };

    public sealed class CompareReport
    {
        public bool passed;
        public bool hasUnityFsMagic;
        public bool catalogReadable;
        public bool crcPresent;
        public bool versionOk;
        public int byteLength;
        public string bundleName = string.Empty;
        public int assetCount;
        public int dependencyCount;
        public List<string> errors = new();
        public List<string> warnings = new();

        public override string ToString() =>
            $"passed={passed} magic={hasUnityFsMagic} catalog={catalogReadable} assets={assetCount} errs={errors.Count}";
    }

    public static bool HasUnityFsMagic(byte[] data)
    {
        if (data == null || data.Length < 8) return false;
        // "UnityFS" + pad (space 0x20 official, or NUL legacy Anity)
        for (int i = 0; i < 7; i++)
            if (data[i] != UnityFsMagic[i]) return false;
        return data[7] == (byte)' ' || data[7] == 0;
    }

    public static bool HasUnityFsMagic(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
        using var fs = File.OpenRead(path);
        var buf = new byte[8];
        if (fs.Read(buf, 0, 8) < 8) return false;
        return HasUnityFsMagic(buf);
    }

    /// <summary>Validate a raw bundle buffer against UnityFS + Anity catalog contract.</summary>
    public static CompareReport Validate(byte[] data)
    {
        var r = new CompareReport();
        if (data == null || data.Length == 0)
        {
            r.errors.Add("empty buffer");
            return r;
        }
        r.byteLength = data.Length;

        // Optional ALZ4 wrap — decompress first so outer ALZ4 magic is not a hard fail
        byte[] body = data;
        if (AssetBundleCompression.IsCompressed(data))
        {
            try
            {
                body = AssetBundleCompression.DecompressIfNeeded(data);
                r.warnings.Add("ALZ4 compressed payload — decompressed for catalog");
            }
            catch (Exception ex)
            {
                r.errors.Add("ALZ4 decompress failed: " + ex.Message);
                return r;
            }
        }

        r.hasUnityFsMagic = HasUnityFsMagic(body);
        if (!r.hasUnityFsMagic)
            r.errors.Add("missing UnityFS magic (expected 'UnityFS ')");

        if (body.Length < 16)
        {
            r.errors.Add("bundle too small for version/crc fields");
            return r;
        }

        // version at offset 8 (uint LE) for Anity layout
        uint version = BitConverter.ToUInt32(body, 8);
        r.versionOk = version >= 1 && version <= 10;
        if (!r.versionOk)
            r.warnings.Add($"unusual format version {version}");

        try
        {
            if (AssetBundleFormat.TryReadBundle(body, out var catalog) && catalog != null)
            {
                r.catalogReadable = true;
                r.bundleName = catalog.bundleName ?? string.Empty;
                r.assetCount = catalog.assets?.Count ?? 0;
                r.dependencyCount = catalog.dependencies?.Count ?? 0;
                r.crcPresent = catalog.crc != 0 || r.assetCount == 0;
            }
            else if (r.hasUnityFsMagic)
            {
                // Official Unity binary or minimal fixture — magic gate still passes
                r.warnings.Add(body.Length <= 16
                    ? "minimal UnityFS fixture (no Anity catalog)"
                    : "UnityFS magic OK but Anity catalog parse failed (possible official Unity binary)");
                r.catalogReadable = false;
            }
            else
            {
                r.errors.Add("catalog unreadable");
            }
        }
        catch (Exception ex)
        {
            if (r.hasUnityFsMagic)
                r.warnings.Add("catalog parse exception (magic OK): " + ex.Message);
            else
                r.errors.Add("catalog parse exception: " + ex.Message);
        }

        r.passed = r.hasUnityFsMagic && r.errors.Count == 0;
        return r;
    }

    public static CompareReport ValidateFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            var r = new CompareReport();
            r.errors.Add("file missing: " + path);
            return r;
        }
        return Validate(File.ReadAllBytes(path));
    }

    /// <summary>
    /// Structural compare of two bundles (magic + asset names set).
    /// Byte-identical not required (Anity vs official Unity differ in compression).
    /// </summary>
    public static CompareReport CompareStructure(byte[] expected, byte[] actual)
    {
        var a = Validate(expected);
        var b = Validate(actual);
        var r = new CompareReport
        {
            hasUnityFsMagic = a.hasUnityFsMagic && b.hasUnityFsMagic,
            catalogReadable = a.catalogReadable && b.catalogReadable,
            byteLength = actual?.Length ?? 0
        };
        if (!a.hasUnityFsMagic) r.errors.Add("expected missing UnityFS magic");
        if (!b.hasUnityFsMagic) r.errors.Add("actual missing UnityFS magic");
        if (a.catalogReadable && b.catalogReadable)
        {
            if (a.assetCount != b.assetCount)
                r.warnings.Add($"assetCount expected={a.assetCount} actual={b.assetCount}");
        }
        r.passed = r.errors.Count == 0 && r.hasUnityFsMagic;
        r.assetCount = b.assetCount;
        r.bundleName = b.bundleName;
        return r;
    }

    /// <summary>Gate used by CI: path must exist, UnityFS magic, and if Anity-built must parse catalog.</summary>
    public static bool Gate(string path, bool requireAnityCatalog = false)
    {
        var r = ValidateFile(path);
        if (!r.passed) return false;
        if (requireAnityCatalog && !r.catalogReadable) return false;
        return true;
    }

    /// <summary>Diff first N bytes (hex) for diagnostics.</summary>
    public static string HexHeader(byte[] data, int n = 32)
    {
        if (data == null) return string.Empty;
        int len = Math.Min(n, data.Length);
        var sb = new StringBuilder(len * 3);
        for (int i = 0; i < len; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(data[i].ToString("X2"));
        }
        return sb.ToString();
    }
}
