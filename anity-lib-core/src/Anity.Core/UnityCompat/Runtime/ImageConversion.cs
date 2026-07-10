using System;

namespace UnityEngine;

public static class ImageConversion
{
    public static byte[] EncodeToPNG(this Texture2D tex)
    {
        _ = tex;
        return Array.Empty<byte>();
    }

    public static byte[] EncodeToJPG(this Texture2D tex, int quality = 75)
    {
        _ = tex;
        _ = quality;
        return Array.Empty<byte>();
    }

    public static byte[] EncodeToTGA(this Texture2D tex)
    {
        _ = tex;
        return Array.Empty<byte>();
    }

    public static bool LoadImage(this Texture2D tex, byte[] data, bool markNonReadable = false)
    {
        _ = tex;
        _ = data;
        _ = markNonReadable;
        return false;
    }
}
