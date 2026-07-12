using UnityEngine;

namespace UnityEditor;

public class TrueTypeFontImporter : AssetImporter
{
    public int fontSize { get; set; } = 16;
    public int fontRenderingMode { get; set; }
    public bool includeFontData { get; set; } = true;
    public CharacterInfo[] characterInfo { get; set; }
    public string customCharacters { get; set; } = string.Empty;
    public string[] fontNames { get; set; }
    public float fontSpacing { get; set; } = 1f;
    public float characterSpacing { get; set; }
    public float lineSpacing { get; set; } = 1f;
    public int ascentCalculationMode { get; set; }
    public bool shouldRoundAdvanceValue { get; set; } = true;
    public FontTextureCase fontTextureCase { get; set; } = FontTextureCase.Dynamic;
    public Texture2D[] fontTexture { get; set; }
    public int smallestSize { get; set; }
    public int largestSize { get; set; } = 500;
    public bool use220GlyphSet { get; set; } = true;
    public string style { get; set; } = string.Empty;
    public Font font { get; set; }

    public Font[] fontReferences { get; set; }

    public Font GenerateEditableFont(string path)
    {
        _ = path;
        return font;
    }

    public static new TrueTypeFontImporter GetAtPath(string path)
    {
        return new TrueTypeFontImporter { assetPath = path };
    }
}

public enum FontTextureCase
{
    Dynamic = -1,
    Unicode = 0,
    ASCII = 1,
    ASCIIUpperCase = 2,
    ASCIILowerCase = 3,
    CustomSet = 4
}

public struct CharacterInfo
{
    public int index;
    public float advance;
    public Rect uv;
    public Rect vert;
}
