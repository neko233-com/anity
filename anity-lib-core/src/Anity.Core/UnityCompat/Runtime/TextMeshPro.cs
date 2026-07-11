using System;
using UnityEngine;

namespace TMPro;

public enum FontWeights { Thin = 100, ExtraLight = 200, Light = 300, Regular = 400, Medium = 500, SemiBold = 600, Bold = 700, Heavy = 800, Black = 900 }
public enum TextAlignmentOptions { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight, Justified, Flush, CenterGeoAligned }
public enum TextOverflowModes { Overflow, Ellipsis, Masking, ScrollRect, Page, Linked, Truncate }
public enum TextureMappingOptions { Character, Line, Paragraph, MatchAspect }
public enum VertexSortingOrder { Normal, Reverse }

public class TMP_Text : UnityEngine.UI.Text
{
    private string _text = string.Empty;
    private float _fontSize = 36f;
    private bool _autoSizeTextContainer;
    private TextAlignmentOptions _alignment = TextAlignmentOptions.TopLeft;
    private TextOverflowModes _overflowMode = TextOverflowModes.Overflow;
    private bool _enableWordWrapping = true;
    private bool _isRightToLeft;
    private float _outlineWidth;
    private Color _outlineColor = Color.black;
    private Material? _fontMaterial;
    private TMP_FontAsset? _fontAsset;

    public new string text
    {
        get => _text;
        set { _text = value ?? string.Empty; SetVerticesDirty(); }
    }

    public new float fontSize { get => _fontSize; set => _fontSize = value; }
    public bool autoSizeTextContainer { get => _autoSizeTextContainer; set => _autoSizeTextContainer = value; }
    public TextAlignmentOptions alignment { get => _alignment; set => _alignment = value; }
    public TextOverflowModes overflowMode { get => _overflowMode; set => _overflowMode = value; }
    public bool enableWordWrapping { get => _enableWordWrapping; set => _enableWordWrapping = value; }
    public bool isRightToLeftText { get => _isRightToLeft; set => _isRightToLeft = value; }
    public float outlineWidth { get => _outlineWidth; set => _outlineWidth = value; }
    public Color outlineColor { get => _outlineColor; set => _outlineColor = value; }
    public Material? fontMaterial { get => _fontMaterial; set => _fontMaterial = value; }
    public TMP_FontAsset? fontAsset { get => _fontAsset; set => _fontAsset = value; }
    public Material? fontSharedMaterial { get; set; }
    public Material[]? fontSharedMaterials { get; set; }
    public bool havePropertiesChanged { get; set; }
    public TMP_TextInfo? textInfo { get; set; }

    public override float preferredWidth
    {
        get
        {
            var charWidth = _fontSize * 0.5f;
            var maxLineWidth = 0f;
            var lines = _text.Split('\n');
            foreach (var line in lines)
            {
                var lineWidth = line.Length * charWidth;
                if (lineWidth > maxLineWidth) maxLineWidth = lineWidth;
            }
            return maxLineWidth;
        }
    }

    public override float preferredHeight
    {
        get
        {
            var lineHeight = _fontSize * 1.2f;
            var lines = _text.Split('\n');
            var numLines = lines.Length == 0 ? 1 : lines.Length;
            return numLines * lineHeight;
        }
    }

    public void ForceMeshUpdate(bool ignoreActiveState = false, bool forceTextReparsing = false) { _ = ignoreActiveState; _ = forceTextReparsing; }
    public void UpdateGeometry(Mesh mesh, int index) { _ = mesh; _ = index; }
    public void UpdateVertexData(TMP_VertexDataUpdateFlags flags = TMP_VertexDataUpdateFlags.All) { _ = flags; }
    public void SetText(string text) => this.text = text;
    public void SetText(string text, float arg0) { _ = arg0; this.text = text; }
    public void SetText(string text, float arg0, float arg1) { _ = arg0; _ = arg1; this.text = text; }
    public void SetText(string text, float arg0, float arg1, float arg2) { _ = arg0; _ = arg1; _ = arg2; this.text = text; }
}

public class TextMeshPro : TMP_Text { }
public class TextMeshProUGUI : TMP_Text { }

public class TMP_FontAsset : Object
{
    public Material? material { get; set; }
    public Material[]? materials { get; set; }
    public bool isMultiAtlasTexturesEnabled { get; set; }
    public int atlasPopulationMode { get; set; }
}
public class TMP_TextInfo
{
    public TMP_CharacterInfo[] characterInfo { get; set; } = Array.Empty<TMP_CharacterInfo>();
    public int characterCount { get; set; }
    public int spriteCount { get; set; }
    public int wordCount { get; set; }
    public int lineCount { get; set; }
    public int pageCount { get; set; }
    public int linkCount { get; set; }
}

public struct TMP_CharacterInfo
{
    public char character;
    public int index;
    public int lineNumber;
    public int pageNumber;
}

[Flags]
public enum TMP_VertexDataUpdateFlags
{
    None = 0,
    Vertices = 1,
    Uv0 = 2,
    Uv2 = 4,
    Uv4 = 8,
    Colors32 = 16,
    All = Vertices | Uv0 | Uv2 | Uv4 | Colors32
}
