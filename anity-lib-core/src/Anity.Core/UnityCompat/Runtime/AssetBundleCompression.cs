using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;

namespace UnityEngine;

/// <summary>
/// AssetBundle payload compression (Unity ChunkBasedCompression path).
/// Format: magic "ALZ4" + rawLen + codec + payload
///   codec 0 = Deflate (legacy)
///   codec 1 = real LZ4 block (default)
/// </summary>
public static class AssetBundleCompression
{
    public const uint Magic = 0x344C5A41; // 'ALZ4' little-endian
    public const byte CodecDeflate = 0;
    public const byte CodecLz4 = 1;

    /// <summary>Active codec for new Compress() calls.</summary>
    public static byte DefaultCodec { get; set; } = CodecLz4;

    public static byte[] MaybeCompress(byte[] raw, BuildAssetBundleOptions options)
    {
        if (raw == null) return Array.Empty<byte>();
        if ((options & BuildAssetBundleOptions.UncompressedAssetBundle) != 0)
            return raw;
        if ((options & BuildAssetBundleOptions.ChunkBasedCompression) != 0)
            return Compress(raw);
        return raw;
    }

    public static byte[] Compress(byte[] raw) => Compress(raw, DefaultCodec);

    public static byte[] Compress(byte[] raw, byte codec)
    {
        if (raw == null || raw.Length == 0) return Array.Empty<byte>();
        byte[] payload;
        if (codec == CodecLz4)
            payload = Lz4Codec.Encode(raw);
        else
            payload = DeflateEncode(raw);

        using var ms = new MemoryStream(12 + payload.Length);
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(Magic);
            bw.Write(raw.Length);
            bw.Write(codec);
            bw.Write(payload.Length);
            bw.Write(payload);
        }
        return ms.ToArray();
    }

    public static byte[] DecompressIfNeeded(byte[] data)
    {
        if (data == null || data.Length < 8) return data ?? Array.Empty<byte>();
        if (BitConverter.ToUInt32(data, 0) != Magic)
            return data;

        int rawLen = BitConverter.ToInt32(data, 4);

        // New format: magic(4) + rawLen(4) + codec(1) + payloadLen(4) + payload
        if (data.Length >= 13)
        {
            byte codec = data[8];
            if (codec == CodecLz4 || codec == CodecDeflate)
            {
                int payloadLen = BitConverter.ToInt32(data, 9);
                if (payloadLen >= 0 && 13 + payloadLen <= data.Length)
                {
                    var payload = new byte[payloadLen];
                    Buffer.BlockCopy(data, 13, payload, 0, payloadLen);
                    if (codec == CodecLz4)
                        return Lz4Codec.Decode(payload, rawLen);
                    return DeflateDecode(payload, rawLen);
                }
            }
        }

        // Legacy: magic(4) + rawLen(4) + deflate body (no codec byte)
        return DeflateDecode(data, 8, data.Length - 8, rawLen);
    }

    public static bool IsCompressed(byte[] data)
    {
        if (data == null || data.Length < 4) return false;
        return BitConverter.ToUInt32(data, 0) == Magic;
    }

    public static byte GetCodec(byte[] data)
    {
        if (data == null || data.Length < 9 || BitConverter.ToUInt32(data, 0) != Magic)
            return 0xFF;
        byte c = data[8];
        if (c == CodecLz4 || c == CodecDeflate) return c;
        return CodecDeflate; // legacy treated as deflate
    }

    private static byte[] DeflateEncode(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var def = new DeflateStream(ms, CompressionLevel.Optimal, true))
            def.Write(raw, 0, raw.Length);
        return ms.ToArray();
    }

    private static byte[] DeflateDecode(byte[] payload, int rawLen)
        => DeflateDecode(payload, 0, payload.Length, rawLen);

    private static byte[] DeflateDecode(byte[] data, int offset, int count, int rawLen)
    {
        using var input = new MemoryStream(data, offset, count);
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
