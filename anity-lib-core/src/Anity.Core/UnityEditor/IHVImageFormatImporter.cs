using UnityEngine;

namespace UnityEditor;

public class IHVImageFormatImporter : AssetImporter
{
    public bool isReadable { get; set; }
    public FilterMode filterMode { get; set; } = FilterMode.Bilinear;
    public TextureWrapMode wrapMode { get; set; } = TextureWrapMode.Repeat;
    public TextureWrapMode wrapModeU { get; set; } = TextureWrapMode.Repeat;
    public TextureWrapMode wrapModeV { get; set; } = TextureWrapMode.Repeat;
    public TextureWrapMode wrapModeW { get; set; } = TextureWrapMode.Repeat;
    public bool sRGBTexture { get; set; } = true;
    public int anisoLevel { get; set; } = 1;
    public bool ignoreMipmapLimit { get; set; }
    public StreamingMipmapPriority streamingMipmapsPriority { get; set; }

    public static new IHVImageFormatImporter GetAtPath(string path)
    {
        return new IHVImageFormatImporter { assetPath = path };
    }
}

public enum StreamingMipmapPriority
{
    NotStreaming,
    Low,
    Normal,
    High
}
