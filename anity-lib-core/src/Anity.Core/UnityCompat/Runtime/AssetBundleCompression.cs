using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;

namespace UnityEngine;

/// <summary>
/// AssetBundle payload compression (Unity ChunkBasedCompression / LZ4-equivalent path).
/// Magic "ALZ4" + deflate body — portable; used when ChunkBasedCompression is set.
/// </summary>
public static class AssetBundleCompression
{
    public const uint Magic = 0x344C5A41; // 'ALZ4' little-endian

    public static byte[] MaybeCompress(byte[] raw, BuildAssetBundleOptions options)
    {
        if (raw == null) return Array.Empty<byte>();
        if ((options & BuildAssetBundleOptions.UncompressedAssetBundle) != 0)
            return raw;
        if ((options & BuildAssetBundleOptions.ChunkBasedCompression) != 0)
            return Compress(raw);
        return raw;
    }

    public static byte[] Compress(byte[] raw)
    {
        if (raw == null || raw.Length == 0) return Array.Empty<byte>();
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(Magic);
            bw.Write(raw.Length);
            using (var def = new DeflateStream(ms, CompressionLevel.Optimal, true))
                def.Write(raw, 0, raw.Length);
        }
        return ms.ToArray();
    }

    public static byte[] DecompressIfNeeded(byte[] data)
    {
        if (data == null || data.Length < 8) return data ?? Array.Empty<byte>();
        if (BitConverter.ToUInt32(data, 0) != Magic)
            return data;
        int rawLen = BitConverter.ToInt32(data, 4);
        using var input = new MemoryStream(data, 8, data.Length - 8);
        using var def = new DeflateStream(input, CompressionMode.Decompress);
        var output = new byte[rawLen];
        int read = 0;
        while (read < rawLen)
        {
            int n = def.Read(output, read, rawLen - read);
            if (n <= 0) break;
            read += n;
        }
        return output;
    }
}
