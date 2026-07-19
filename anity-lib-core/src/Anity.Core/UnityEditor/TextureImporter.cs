using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public class TextureImporter : AssetImporter
{
  private readonly Dictionary<string, TextureImporterPlatformSettings> _platformSettings = new();

  public TextureImporterType textureType { get; set; } = TextureImporterType.Default;
  public TextureImporterShape textureShape { get; set; } = TextureImporterShape.Texture2D;
  public bool sRGBTexture { get; set; } = true;
  public bool alphaIsTransparency { get; set; }
  public TextureImporterAlphaSource alphaSource { get; set; } = TextureImporterAlphaSource.InputTextureAlpha;
  public bool mipmapEnabled { get; set; } = true;
  public bool mipMapsPreserveCoverage { get; set; }
  public bool mipmapStreaming { get; set; }
  public float mipMapBias { get; set; }
  public int mipmapBias { get => (int)mipMapBias; set => mipMapBias = value; }
  public TextureImporterCompression textureCompression { get; set; } = TextureImporterCompression.Automatic;
  public TextureImporterFormat textureFormat { get; set; } = TextureImporterFormat.Automatic;
  public int compressionQuality { get; set; } = 50;
  public int maxTextureSize { get; set; } = 2048;
  public TextureResizeAlgorithm resizeAlgorithm { get; set; }
  public TextureImporterNPOTScale npotScale { get; set; } = TextureImporterNPOTScale.ToNearest;
  public bool readable { get; set; }
  public bool isReadable { get => readable; set => readable = value; }
  public bool streamingMipmaps { get; set; }
  public int streamingMipmapsPriority { get; set; }
  public bool generateMipsInLinearSpace { get; set; }
  public bool borderMipmap { get; set; }
  public bool fadeOut { get; set; }
  public int anisoLevel { get; set; } = 1;
  public FilterMode filterMode { get; set; } = FilterMode.Bilinear;
  public TextureWrapMode wrapMode { get; set; } = TextureWrapMode.Repeat;
  public TextureWrapMode wrapModeU { get; set; } = TextureWrapMode.Repeat;
  public TextureWrapMode wrapModeV { get; set; } = TextureWrapMode.Repeat;
  public TextureWrapMode wrapModeW { get; set; } = TextureWrapMode.Repeat;
  public bool convertToNormalmap { get; set; }
  public TextureImporterNormalFilter normalmapFilter { get; set; } = TextureImporterNormalFilter.Standard;
  public float normalmapFilterValue { get; set; } = 1f;
  public float heightmapScale { get; set; } = 0.25f;

  public float spritePixelsPerUnit { get; set; } = 100f;
  public Vector2 spritePivot { get; set; } = new Vector2(0.5f, 0.5f);
  public SpriteMeshType spriteMeshType { get; set; } = SpriteMeshType.Tight;
  public uint spriteExtrude { get; set; } = 1;
  public Vector4 spriteBorder { get; set; }
  public SpriteImportMode spriteImportMode { get; set; } = SpriteImportMode.Single;
  public int spritePixelsToUnits { get => (int)spritePixelsPerUnit; set => spritePixelsPerUnit = value; }

  public TextureImporterSettings GetTextureImporterSettings()
  {
    return new TextureImporterSettings
    {
      textureType = textureType,
      textureShape = textureShape,
      aniso = anisoLevel,
      filterMode = filterMode,
      wrapMode = wrapMode,
      wrapModeU = wrapModeU,
      wrapModeV = wrapModeV,
      wrapModeW = wrapModeW,
      mipmapEnabled = mipmapEnabled,
      readable = readable,
      maxTextureSize = maxTextureSize
    };
  }

  public void SetTextureSettings(TextureImporterSettings settings)
  {
    if (settings == null) return;
    textureType = settings.textureType;
    textureShape = settings.textureShape;
    anisoLevel = settings.aniso;
    filterMode = settings.filterMode;
    wrapMode = settings.wrapMode;
    wrapModeU = settings.wrapModeU;
    wrapModeV = settings.wrapModeV;
    wrapModeW = settings.wrapModeW;
    mipmapEnabled = settings.mipmapEnabled;
    readable = settings.readable;
    maxTextureSize = settings.maxTextureSize;
  }

  public void GetTextureSettings(TextureImporterSettings settings)
  {
    if (settings == null) return;
    settings.textureType = textureType;
    settings.textureShape = textureShape;
    settings.aniso = anisoLevel;
    settings.filterMode = filterMode;
    settings.wrapMode = wrapMode;
    settings.wrapModeU = wrapModeU;
    settings.wrapModeV = wrapModeV;
    settings.wrapModeW = wrapModeW;
    settings.mipmapEnabled = mipmapEnabled;
    settings.readable = readable;
    settings.maxTextureSize = maxTextureSize;
  }

  public TextureImporterPlatformSettings GetPlatformTextureSettings(string platform)
  {
    if (_platformSettings.TryGetValue(platform, out var settings))
      return settings;
    return new TextureImporterPlatformSettings { name = platform };
  }

  public void SetPlatformTextureSettings(TextureImporterPlatformSettings settings)
  {
    if (settings == null) return;
    _platformSettings[settings.name] = settings;
  }

  public void SetPlatformTextureSettings(string platform, int maxTextureSize, TextureImporterFormat textureFormat, int compressionQuality, bool? overridden = null)
  {
    var settings = new TextureImporterPlatformSettings
    {
      name = platform,
      maxTextureSize = maxTextureSize,
      format = textureFormat,
      compressionQuality = compressionQuality,
      overridden = overridden ?? true
    };
    _platformSettings[platform] = settings;
  }

  public void ClearPlatformTextureSettings(string platform)
  {
    _platformSettings.Remove(platform);
  }

  internal IEnumerable<TextureImporterPlatformSettings> GetConfiguredPlatformTextureSettings() => _platformSettings.Values;

  public bool DoesSourceTextureHaveAlpha() { return false; }
  public TextureImporterFormat GetDefaultTextureFormat() { return textureFormat; }

  public static new TextureImporter GetAtPath(string path)
  {
    return AssetDatabase.GetImporterAtPath(path) as TextureImporter ?? new TextureImporter { assetPath = path, importSettingsMissing = true };
  }
}

public enum TextureImporterNormalFilter
{
  Standard = 0,
  Sobel = 1
}

public enum TextureImporterFormat
{
  Automatic = -1,
  AutomaticCompressed = -1,
  AutomaticTruecolor = -2,
  AutomaticCrunched = -3,
  DXT1 = 10,
  DXT5 = 12,
  RGB24 = 20,
  RGBA32 = 32,
  ARGB32 = 33,
  R16 = 40,
  RHalf = 41,
  RGHalf = 42,
  RGBAHalf = 43,
  RFloat = 44,
  RGFloat = 45,
  RGBAFloat = 46,
  BC7 = 50,
  BC6H = 51,
  BC4 = 52,
  BC5 = 53,
  ETC_RGB4 = 60,
  ETC2_RGB = 61,
  ETC2_RGBA8 = 62,
  ASTC_4x4 = 70,
  ASTC_6x6 = 72,
  ASTC_8x8 = 74,
  ASTC_12x12 = 78,
  PVRTC_RGB2 = 80,
  PVRTC_RGBA2 = 81,
  PVRTC_RGB4 = 82,
  PVRTC_RGBA4 = 83,
  Alpha8 = 9
}

public enum TextureImporterAlphaSource
{
  None = 0,
  InputTextureAlpha = 1,
  FromGrayScale = 2
}

public enum SpriteMeshType
{
  FullRect = 0,
  Tight = 1
}

public enum SpriteImportMode
{
  None = 0,
  Single = 1,
  Multiple = 2,
  Polygon = 3
}

public class TextureImporterPlatformSettings
{
  public string name { get; set; } = string.Empty;
  public int maxTextureSize { get; set; } = 2048;
  public TextureImporterFormat format { get; set; } = TextureImporterFormat.Automatic;
  public int compressionQuality { get; set; } = 50;
  public bool overridden { get; set; }
  public bool allowsAlphaSplitting { get; set; }
  public AndroidETC2FallbackOverride androidETC2FallbackOverride { get; set; }
  public TextureImporterCompression textureCompression { get; set; } = TextureImporterCompression.Compressed;
  public bool crunchedCompression { get; set; }
}

public enum AndroidETC2FallbackOverride
{
  UseBuildSettings = 0,
  Quality32Bit = 1,
  Quality16Bit = 2,
  Quality32BitDownscaled = 3
}

public enum TextureImporterType
{
  Default = 0,
  NormalMap = 1,
  Editor = 2,
  LegacyGUI = 3,
  Sprite = 8,
  Cursor = 7,
  Cookie = 4,
  Lightmap = 6,
  DirectionalLightmap = 10,
  SingleChannel = 9,
  Shadowmask = 11,
  Advanced = 5,
  EditorGUIandLegacyGUI = 3
}

public enum TextureImporterShape
{
  Texture2D = 1,
  Cube = 2,
  Texture2DArray = 3,
  Texture3D = 4,
  CubeArray = 5
}

public enum TextureImporterCompression
{
  Uncompressed = 0,
  Compressed = 1,
  CompressedHQ = 2,
  CompressedLQ = 3,
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
  public float spritePixelsPerUnit { get; set; } = 100f;
  public Vector2 spritePivot { get; set; } = new Vector2(0.5f, 0.5f);
  public SpriteMeshType spriteMeshType { get; set; } = SpriteMeshType.Tight;
  public uint spriteExtrude { get; set; } = 1;
  public Vector4 spriteBorder { get; set; }
  public SpriteImportMode spriteImportMode { get; set; } = SpriteImportMode.Single;
  public TextureImporterAlphaSource alphaSource { get; set; } = TextureImporterAlphaSource.InputTextureAlpha;
  public bool alphaIsTransparency { get; set; }
  public bool sRGBTexture { get; set; } = true;
}
