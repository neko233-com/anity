using UnityEngine;

namespace UnityEditor;

public class TextureImporter : AssetImporter
{
  public TextureImporterType textureType { get; set; } = TextureImporterType.Default;
  public TextureImporterShape textureShape { get; set; } = TextureImporterShape.Texture2D;
  public bool sRGBTexture { get; set; } = true;
  public bool alphaIsTransparency { get; set; }
  public bool mipmapEnabled { get; set; } = true;
  public bool mipMapsPreserveCoverage { get; set; }
  public bool mipmapStreaming { get; set; }
  public int mipmapBias { get; set; }
  public TextureImporterCompression textureCompression { get; set; } = TextureImporterCompression.Automatic;
  public int maxTextureSize { get; set; } = 2048;
  public TextureResizeAlgorithm resizeAlgorithm { get; set; }
  public TextureImporterNPOTScale npotScale { get; set; } = TextureImporterNPOTScale.ToNearest;
  public bool readable { get; set; }
  public bool streamingMipmaps { get; set; }
  public int streamingMipmapsPriority { get; set; }
  public bool isReadable { get; set; }
  public bool generateMipsInLinearSpace { get; set; }
  public bool borderMipmap { get; set; }
  public bool fadeOut { get; set; }
  public int anisoLevel { get; set; } = 1;
  public FilterMode filterMode { get; set; } = FilterMode.Bilinear;
  public TextureWrapMode wrapMode { get; set; } = TextureWrapMode.Repeat;
  public TextureWrapMode wrapModeU { get; set; } = TextureWrapMode.Repeat;
  public TextureWrapMode wrapModeV { get; set; } = TextureWrapMode.Repeat;
  public TextureWrapMode wrapModeW { get; set; } = TextureWrapMode.Repeat;

  public void SetTextureSettings(TextureImporterSettings settings)
  {
    _ = settings;
  }

  public void GetTextureSettings(TextureImporterSettings settings)
  {
    _ = settings;
  }

  public static TextureImporter GetAtPath(string path)
  {
    return new TextureImporter { assetPath = path };
  }
}

public enum TextureImporterType
{
  Default,
  NormalMap,
  EditorGUIandLegacyGUI,
  Sprite,
  Cursor,
  Cookie,
  Lightmap,
  SingleChannel,
  Shadowmask,
  DirectionalLightmap
}

public enum TextureImporterShape
{
  Texture2D,
  Cube,
  Texture2DArray,
  CubeArray
}

public enum TextureImporterCompression
{
  Uncompressed,
  Compressed,
  CompressedHQ,
  CompressedLQ,
  Automatic = -1
}

public enum TextureResizeAlgorithm
{
  Mitchell,
  Bilinear
}

public enum TextureImporterNPOTScale
{
  None,
  ToNearest,
  ToLarger,
  ToSmaller,
  TurnIntoSquare
}

public sealed class TextureImporterSettings
{
  public TextureImporterType textureType { get; set; }
  public TextureImporterShape textureShape { get; set; }
  public int aniso { get; set; }
  public FilterMode filterMode { get; set; }
  public TextureWrapMode wrapMode { get; set; }
  public TextureWrapMode wrapModeU { get; set; }
  public TextureWrapMode wrapModeV { get; set; }
  public TextureWrapMode wrapModeW { get; set; }
  public bool mipmapEnabled { get; set; }
  public bool readable { get; set; }
  public int maxTextureSize { get; set; } = 2048;
}
