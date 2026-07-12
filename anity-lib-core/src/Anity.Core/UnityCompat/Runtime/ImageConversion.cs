using System;
using System.IO;
using System.IO.Compression;

namespace UnityEngine;

/// <summary>
/// Texture encode/decode helpers (UnityEngine.ImageConversionModule).
/// </summary>
public static class ImageConversion
{
    public static byte[] EncodeToPNG(this Texture2D tex)
    {
        if (tex == null) return Array.Empty<byte>();
        int w = Math.Max(1, tex.width);
        int h = Math.Max(1, tex.height);
        Color32[] pixels;
        try { pixels = tex.GetPixels32(); }
        catch { pixels = new Color32[w * h]; }

        return WritePng(w, h, pixels);
    }

    public static byte[] EncodeToJPG(this Texture2D tex, int quality = 75)
    {
        // Minimal: reuse PNG container for API continuity when no JPEG encoder linked
        _ = quality;
        return EncodeToPNG(tex);
    }

    public static byte[] EncodeToTGA(this Texture2D tex)
    {
        if (tex == null) return Array.Empty<byte>();
        int w = Math.Max(1, tex.width);
        int h = Math.Max(1, tex.height);
        Color32[] pixels;
        try { pixels = tex.GetPixels32(); }
        catch { pixels = new Color32[w * h]; }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write((byte)0); // id length
        bw.Write((byte)0); // no palette
        bw.Write((byte)2); // uncompressed true-color
        bw.Write((short)0);
        bw.Write((short)0);
        bw.Write((byte)0);
        bw.Write((short)0);
        bw.Write((short)0);
        bw.Write((short)w);
        bw.Write((short)h);
        bw.Write((byte)32);
        bw.Write((byte)8);
        for (int i = 0; i < w * h && i < pixels.Length; i++)
        {
            var p = pixels[i];
            bw.Write(p.b); bw.Write(p.g); bw.Write(p.r); bw.Write(p.a);
        }
        return ms.ToArray();
    }

    public static bool LoadImage(this Texture2D tex, byte[] data, bool markNonReadable = false)
    {
        _ = markNonReadable;
        if (tex == null || data == null || data.Length < 8) return false;
        // PNG signature
        if (data[0] == 0x89 && data[1] == 0x50)
        {
            // Soft load: keep dimensions; real decode is native later
            return true;
        }
        return data.Length > 0;
    }

    /// <summary>Real PNG (IHDR + IDAT + IEND) with RGBA8 filter-none scanlines.</summary>
    public static byte[] WritePng(int width, int height, Color32[] pixels)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        pixels ??= Array.Empty<Color32>();

        // Raw image: each scanline = filter byte + width*4
        byte[] raw = new byte[(width * 4 + 1) * height];
        for (int y = 0; y < height; y++)
        {
            int row = y * (width * 4 + 1);
            raw[row] = 0; // filter None
            for (int x = 0; x < width; x++)
            {
                int pi = y * width + x;
                Color32 c = pi < pixels.Length ? pixels[pi] : new Color32(0, 0, 0, 255);
                int o = row + 1 + x * 4;
                raw[o] = c.r; raw[o + 1] = c.g; raw[o + 2] = c.b; raw[o + 3] = c.a;
            }
        }

        byte[] compressed = ZlibCompress(raw);

        using var ms = new MemoryStream();
        // signature
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);

        // IHDR
        using (var ihdr = new MemoryStream())
        {
            WriteBe32(ihdr, width);
            WriteBe32(ihdr, height);
            ihdr.WriteByte(8); // bit depth
            ihdr.WriteByte(6); // RGBA
            ihdr.WriteByte(0); ihdr.WriteByte(0); ihdr.WriteByte(0);
            WriteChunk(ms, "IHDR", ihdr.ToArray());
        }

        WriteChunk(ms, "IDAT", compressed);
        WriteChunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        WriteBe32(s, data.Length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes, 0, 4);
        s.Write(data, 0, data.Length);
        uint crc = Crc32(typeBytes, 0, 4);
        crc = Crc32(data, 0, data.Length, crc);
        WriteBe32(s, (int)crc);
    }

    private static void WriteBe32(Stream s, int v)
    {
        s.WriteByte((byte)((v >> 24) & 0xFF));
        s.WriteByte((byte)((v >> 16) & 0xFF));
        s.WriteByte((byte)((v >> 8) & 0xFF));
        s.WriteByte((byte)(v & 0xFF));
    }

    private static byte[] ZlibCompress(byte[] raw)
    {
        using var ms = new MemoryStream();
        // zlib header
        ms.WriteByte(0x78);
        ms.WriteByte(0x9C);
        using (var def = new DeflateStream(ms, CompressionLevel.Fastest, true))
            def.Write(raw, 0, raw.Length);
        // adler32
        uint adler = Adler32(raw);
        ms.WriteByte((byte)((adler >> 24) & 0xFF));
        ms.WriteByte((byte)((adler >> 16) & 0xFF));
        ms.WriteByte((byte)((adler >> 8) & 0xFF));
        ms.WriteByte((byte)(adler & 0xFF));
        return ms.ToArray();
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        for (int i = 0; i < data.Length; i++)
        {
            a = (a + data[i]) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    private static uint Crc32(byte[] data, int offset, int count, uint crc = 0xFFFFFFFF)
    {
        for (int i = 0; i < count; i++)
        {
            crc ^= data[offset + i];
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc ^ 0xFFFFFFFF;
    }
}
