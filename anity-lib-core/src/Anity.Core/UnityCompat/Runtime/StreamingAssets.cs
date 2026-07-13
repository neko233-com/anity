using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityEngine;

/// <summary>
/// StreamingAssets runtime helpers (Unity Application.streamingAssetsPath aligned).
/// Supports file:// on desktop; WebGL uses path string only (no www here).
/// </summary>
public static class StreamingAssets
{
    private static string? _overrideRoot;

    /// <summary>Active StreamingAssets root (test override or Application.streamingAssetsPath).</summary>
    public static string root
    {
        get
        {
            if (!string.IsNullOrEmpty(_overrideRoot)) return _overrideRoot!;
            return Application.streamingAssetsPath;
        }
    }

    /// <summary>Tests: isolate StreamingAssets path.</summary>
    public static void SetRootForTests(string path)
    {
        _overrideRoot = path;
        if (!string.IsNullOrEmpty(path))
            Directory.CreateDirectory(path);
    }

    public static void ClearRootOverride() => _overrideRoot = null;

    public static string GetPath(string relativePath)
    {
        relativePath = (relativePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public static bool Exists(string relativePath) => File.Exists(GetPath(relativePath));

    public static bool DirectoryExists(string relativePath)
    {
        string p = GetPath(relativePath);
        return Directory.Exists(p);
    }

    public static string ReadAllText(string relativePath, Encoding? encoding = null)
    {
        string path = GetPath(relativePath);
        if (!File.Exists(path)) return string.Empty;
        return File.ReadAllText(path, encoding ?? Encoding.UTF8);
    }

    public static byte[] ReadAllBytes(string relativePath)
    {
        string path = GetPath(relativePath);
        return File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
    }

    public static void WriteAllText(string relativePath, string contents)
    {
        string path = GetPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
        File.WriteAllText(path, contents ?? string.Empty, Encoding.UTF8);
    }

    public static void WriteAllBytes(string relativePath, byte[] data)
    {
        string path = GetPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
        File.WriteAllBytes(path, data ?? Array.Empty<byte>());
    }

    public static string[] GetFiles(string relativeDirectory = "", string searchPattern = "*")
    {
        string dir = string.IsNullOrEmpty(relativeDirectory) ? root : GetPath(relativeDirectory);
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        var files = Directory.GetFiles(dir, searchPattern, SearchOption.TopDirectoryOnly);
        var list = new List<string>(files.Length);
        string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        foreach (var f in files)
        {
            string full = Path.GetFullPath(f);
            string rel;
            if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                rel = full.Substring(rootFull.Length);
            else
                rel = Path.GetFileName(f);
            list.Add(rel.Replace('\\', '/'));
        }
        return list.ToArray();
    }

    public static string[] GetDirectories(string relativeDirectory = "")
    {
        string dir = string.IsNullOrEmpty(relativeDirectory) ? root : GetPath(relativeDirectory);
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        var dirs = Directory.GetDirectories(dir);
        var list = new List<string>(dirs.Length);
        string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        foreach (var d in dirs)
        {
            string full = Path.GetFullPath(d);
            string rel = full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(rootFull.Length)
                : Path.GetFileName(d);
            list.Add(rel.Replace('\\', '/'));
        }
        return list.ToArray();
    }

    /// <summary>Copy a file into StreamingAssets (player content install helper).</summary>
    public static void CopyFrom(string sourceAbsolutePath, string relativeDest)
    {
        if (string.IsNullOrEmpty(sourceAbsolutePath) || !File.Exists(sourceAbsolutePath))
            throw new FileNotFoundException("StreamingAssets source missing", sourceAbsolutePath);
        string dest = GetPath(relativeDest);
        Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? root);
        File.Copy(sourceAbsolutePath, dest, true);
    }

    /// <summary>Unity WebRequest-friendly URL for a StreamingAssets file.</summary>
    public static string GetFileUrl(string relativePath)
    {
        string full = Path.GetFullPath(GetPath(relativePath)).Replace('\\', '/');
        if (!full.StartsWith("/")) full = "/" + full;
        // Windows drive: /C:/...
        return "file://" + full;
    }

    public static bool Delete(string relativePath)
    {
        string path = GetPath(relativePath);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }
}
