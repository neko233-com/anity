using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TMPro;

public enum FontWeights { Thin = 100, ExtraLight = 200, Light = 300, Regular = 400, Medium = 500, SemiBold = 600, Bold = 700, Heavy = 800, Black = 900 }
public enum TextAlignmentOptions { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight, Justified, Flush, CenterGeoAligned }
public enum TextOverflowModes { Overflow, Ellipsis, Masking, ScrollRect, Page, Linked, Truncate }
public enum TextureMappingOptions { Character, Line, Paragraph, MatchAspect }
public enum VertexSortingOrder { Normal, Reverse }
public enum FontStyles { Normal = 0, Bold = 1, Italic = 2, Underline = 4, LowerCase = 8, UpperCase = 16, SmallCaps = 32, Strikethrough = 64, Superscript = 128, Subscript = 256, Highlight = 512 }
public enum TextContainerAnchors { TopLeft, Top, TopRight, Left, Middle, Right, BottomLeft, Bottom, BottomRight, Custom }
public enum MaskingOffsetMode { Percentage, Pixel }
public enum TMP_FontAssetTypes { Bitmap = 0, SDF = 1, SDFAA = 2 }
public enum TMP_InputFieldContentType { Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric, Name, EmailAddress, Password, Pin, Custom }
public enum TMP_InputFieldLineType { SingleLine, MultiLineSubmit, MultiLineNewline }
public enum TMP_InputType { Standard, AutoCorrect, Password }
public enum TMP_CharacterValidation { None, Integer, Decimal, Alphanumeric, Name, EmailAddress }

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

public class TMP_Text : MaskableGraphic, ILayoutElement
{
    private string _text = string.Empty;
    private TMP_FontAsset? _font;
    private float _fontSize = 36f;
    private Color _color = Color.white;
    private bool _richText = true;
    private bool _enableWordWrapping = true;
    private TextOverflowModes _overflowMode = TextOverflowModes.Overflow;
    private float _characterSpacing;
    private float _wordSpacing;
    private float _lineSpacing;
    private float _paragraphSpacing;
    private Vector4 _margin = Vector4.zero;
    private FontStyles _fontStyle = FontStyles.Normal;
    private bool _enableAutoSizing;
    private float _fontSizeMin = 18f;
    private float _fontSizeMax = 72f;
    private TextAlignmentOptions _alignment = TextAlignmentOptions.TopLeft;
    private bool _isRightToLeft;
    private float _outlineWidth;
    private Color _outlineColor = Color.black;
    private Material? _fontMaterial;
    private bool _autoSizeTextContainer;
    private TMP_TextInfo? _textInfo;
    private bool _havePropertiesChanged;

    private float _cachedPreferredWidth = -1f;
    private float _cachedPreferredHeight = -1f;
    private string _cachedText = string.Empty;
    private float _cachedFontSize;

    public override Texture mainTexture
    {
        get
        {
            if (_font != null && _font.atlasTexture != null)
                return _font.atlasTexture;
            return defaultWhiteTexture;
        }
    }

    public new string text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value ?? string.Empty;
                _cachedPreferredWidth = -1f;
                _cachedPreferredHeight = -1f;
                SetVerticesDirty();
                SetLayoutDirty();
            }
        }
    }

    public TMP_FontAsset? font
    {
        get => _font;
        set
        {
            if (_font != value)
            {
                _font = value;
                SetVerticesDirty();
                SetMaterialDirty();
                SetLayoutDirty();
            }
        }
    }

    public float fontSize
    {
        get => _fontSize;
        set
        {
            if (Math.Abs(_fontSize - value) > 0.001f)
            {
                _fontSize = value;
                _cachedPreferredWidth = -1f;
                _cachedPreferredHeight = -1f;
                SetVerticesDirty();
                SetLayoutDirty();
            }
        }
    }

    public new Color color
    {
        get => _color;
        set
        {
            if (_color != value)
            {
                _color = value;
                SetVerticesDirty();
            }
        }
    }

    public bool richText
    {
        get => _richText;
        set
        {
            if (_richText != value)
            {
                _richText = value;
                SetVerticesDirty();
            }
        }
    }

    public bool enableWordWrapping
    {
        get => _enableWordWrapping;
        set
        {
            if (_enableWordWrapping != value)
            {
                _enableWordWrapping = value;
                SetVerticesDirty();
                SetLayoutDirty();
            }
        }
    }

    public TextOverflowModes overflowMode
    {
        get => _overflowMode;
        set
        {
            if (_overflowMode != value)
            {
                _overflowMode = value;
                SetVerticesDirty();
            }
        }
    }

    public float characterSpacing
    {
        get => _characterSpacing;
        set
        {
            if (Math.Abs(_characterSpacing - value) > 0.001f)
            {
                _characterSpacing = value;
                SetVerticesDirty();
            }
        }
    }

    public float wordSpacing
    {
        get => _wordSpacing;
        set
        {
            if (Math.Abs(_wordSpacing - value) > 0.001f)
            {
                _wordSpacing = value;
                SetVerticesDirty();
            }
        }
    }

    public float lineSpacing
    {
        get => _lineSpacing;
        set
        {
            if (Math.Abs(_lineSpacing - value) > 0.001f)
            {
                _lineSpacing = value;
                _cachedPreferredHeight = -1f;
                SetVerticesDirty();
                SetLayoutDirty();
            }
        }
    }

    public float paragraphSpacing
    {
        get => _paragraphSpacing;
        set
        {
            if (Math.Abs(_paragraphSpacing - value) > 0.001f)
            {
                _paragraphSpacing = value;
                SetVerticesDirty();
                SetLayoutDirty();
            }
        }
    }

    public Vector4 margin
    {
        get => _margin;
        set
        {
            if (_margin != value)
            {
                _margin = value;
                SetVerticesDirty();
            }
        }
    }

    public FontStyles fontStyle
    {
        get => _fontStyle;
        set
        {
            if (_fontStyle != value)
            {
                _fontStyle = value;
                SetVerticesDirty();
            }
        }
    }

    public bool enableAutoSizing
    {
        get => _enableAutoSizing;
        set
        {
            if (_enableAutoSizing != value)
            {
                _enableAutoSizing = value;
                SetVerticesDirty();
                SetLayoutDirty();
            }
        }
    }

    public float fontSizeMin
    {
        get => _fontSizeMin;
        set => _fontSizeMin = value;
    }

    public float fontSizeMax
    {
        get => _fontSizeMax;
        set => _fontSizeMax = value;
    }

    public TextAlignmentOptions alignment
    {
        get => _alignment;
        set
        {
            if (_alignment != value)
            {
                _alignment = value;
                SetVerticesDirty();
            }
        }
    }

    public bool isRightToLeftText
    {
        get => _isRightToLeft;
        set => _isRightToLeft = value;
    }

    public float outlineWidth
    {
        get => _outlineWidth;
        set => _outlineWidth = value;
    }

    public Color outlineColor
    {
        get => _outlineColor;
        set => _outlineColor = value;
    }

    public Material? fontMaterial
    {
        get => _fontMaterial;
        set => _fontMaterial = value;
    }

    public Material? fontSharedMaterial { get; set; }
    public Material[]? fontSharedMaterials { get; set; }

    public bool autoSizeTextContainer
    {
        get => _autoSizeTextContainer;
        set => _autoSizeTextContainer = value;
    }

    public bool havePropertiesChanged
    {
        get => _havePropertiesChanged;
        set => _havePropertiesChanged = value;
    }

    public bool isTextOverflowing { get; set; }

    public TMP_TextInfo textInfo
    {
        get
        {
            if (_textInfo == null)
                _textInfo = new TMP_TextInfo();
            return _textInfo;
        }
        set => _textInfo = value;
    }

    public virtual float minWidth => 0f;

    public virtual float preferredWidth
    {
        get
        {
            if (_cachedPreferredWidth >= 0f && _cachedText == _text && Math.Abs(_cachedFontSize - _fontSize) < 0.001f)
                return _cachedPreferredWidth;

            var actualFontSize = _enableAutoSizing ? _fontSizeMax : _fontSize;
            var charWidth = actualFontSize * 0.5f + _characterSpacing;
            var maxLineWidth = 0f;
            var lines = _text.Split('\n');
            foreach (var line in lines)
            {
                var lineWidth = line.Length * charWidth;
                if (lineWidth > maxLineWidth) maxLineWidth = lineWidth;
            }

            _cachedText = _text;
            _cachedFontSize = _fontSize;
            _cachedPreferredWidth = maxLineWidth + _margin.x + _margin.z;
            return _cachedPreferredWidth;
        }
    }

    public virtual float flexibleWidth => -1f;
    public virtual float minHeight => 0f;

    public virtual float preferredHeight
    {
        get
        {
            if (_cachedPreferredHeight >= 0f && _cachedText == _text && Math.Abs(_cachedFontSize - _fontSize) < 0.001f)
                return _cachedPreferredHeight;

            _cachedText = _text;
            _cachedFontSize = _fontSize;
            var actualFontSize = _enableAutoSizing ? _fontSizeMax : _fontSize;
            var lineHeight = actualFontSize * 1.2f + _lineSpacing;
            var lines = _text.Split('\n');
            var numLines = lines.Length == 0 ? 1 : lines.Length;
            _cachedPreferredHeight = numLines * lineHeight + _paragraphSpacing * Mathf.Max(0, numLines - 1) + _margin.y + _margin.w;
            return _cachedPreferredHeight;
        }
    }

    public virtual float flexibleHeight => -1f;
    public virtual int layoutPriority => 0;

    public void ForceMeshUpdate(bool ignoreActiveState = false, bool forceTextReparsing = false)
    {
        _ = ignoreActiveState;
        _ = forceTextReparsing;
        SetVerticesDirty();
    }

    public void UpdateGeometry(Mesh mesh, int index)
    {
        _ = mesh;
        _ = index;
    }

    public void UpdateVertexData(TMP_VertexDataUpdateFlags flags = TMP_VertexDataUpdateFlags.All)
    {
        _ = flags;
        SetVerticesDirty();
    }

    public void SetText(string text) => this.text = text;
    public void SetText(string text, float arg0) { _ = arg0; this.text = text; }
    public void SetText(string text, float arg0, float arg1) { _ = arg0; _ = arg1; this.text = text; }
    public void SetText(string text, float arg0, float arg1, float arg2) { _ = arg0; _ = arg1; _ = arg2; this.text = text; }
    public void SetText(string text, string arg0) { _ = arg0; this.text = text; }
    public void SetCharArray(char[] chars) => text = new string(chars);
    public void SetCharArray(char[] chars, int start, int length) => text = new string(chars, start, length);

    public Vector2 GetPreferredValues(string text)
    {
        return GetPreferredValues(text, 0f, 0f);
    }

    public Vector2 GetPreferredValues(string text, float width, float height)
    {
        _ = text;
        _ = width;
        _ = height;
        return new Vector2(preferredWidth, preferredHeight);
    }

    public virtual void CalculateLayoutInputHorizontal() { }
    public virtual void CalculateLayoutInputVertical() { }

    public void SetNativeSize()
    {
        if (rectTransform is null) return;
        rectTransform.anchorMin = rectTransform.anchorMax;
        rectTransform.sizeDelta = new Vector2(preferredWidth, preferredHeight);
    }

    public override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        var rect = rectTransform != null ? rectTransform.rect : new Rect(0f, 0f, 100f, 100f);
        var color32 = (Color32)_color;
        var actualFontSize = _enableAutoSizing ? CalculateBestFitSize(rect.width, rect.height) : _fontSize;

        var charWidth = actualFontSize * 0.5f + _characterSpacing;
        var textWidth = _text.Length * charWidth;
        var textHeight = actualFontSize + _lineSpacing;

        float xMin, yMin, xMax, yMax;
        var contentRect = new Rect(rect.x + _margin.x, rect.y + _margin.y, rect.width - _margin.x - _margin.z, rect.height - _margin.y - _margin.w);

        switch (_alignment)
        {
            case TextAlignmentOptions.TopLeft:
            case TextAlignmentOptions.Left:
            case TextAlignmentOptions.BottomLeft:
                xMin = contentRect.xMin;
                xMax = contentRect.xMin + textWidth;
                break;
            case TextAlignmentOptions.TopRight:
            case TextAlignmentOptions.Right:
            case TextAlignmentOptions.BottomRight:
                xMin = contentRect.xMax - textWidth;
                xMax = contentRect.xMax;
                break;
            default:
                xMin = contentRect.xMin + (contentRect.width - textWidth) * 0.5f;
                xMax = xMin + textWidth;
                break;
        }

        switch (_alignment)
        {
            case TextAlignmentOptions.TopLeft:
            case TextAlignmentOptions.Top:
            case TextAlignmentOptions.TopRight:
                yMax = contentRect.yMax;
                yMin = contentRect.yMax - textHeight;
                break;
            case TextAlignmentOptions.BottomLeft:
            case TextAlignmentOptions.Bottom:
            case TextAlignmentOptions.BottomRight:
                yMin = contentRect.yMin;
                yMax = contentRect.yMin + textHeight;
                break;
            default:
                yMin = contentRect.yMin + (contentRect.height - textHeight) * 0.5f;
                yMax = yMin + textHeight;
                break;
        }

        if (_overflowMode == TextOverflowModes.Overflow)
        {
            xMin = contentRect.xMin;
            xMax = contentRect.xMax;
            yMin = contentRect.yMin;
            yMax = contentRect.yMax;
        }

        vh.AddVert(new Vector3(xMin, yMin, 0f), color32, new Vector2(0f, 0f));
        vh.AddVert(new Vector3(xMax, yMin, 0f), color32, new Vector2(1f, 0f));
        vh.AddVert(new Vector3(xMax, yMax, 0f), color32, new Vector2(1f, 1f));
        vh.AddVert(new Vector3(xMin, yMax, 0f), color32, new Vector2(0f, 1f));
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }

    private int CalculateBestFitSize(float availableWidth, float availableHeight)
    {
        if (string.IsNullOrEmpty(_text)) return Mathf.RoundToInt(_fontSizeMin);

        var charWidthRatio = 0.5f;
        var maxLineLen = _text.Length;
        var sizeByWidth = availableWidth / (maxLineLen * charWidthRatio + _characterSpacing);
        var sizeByHeight = availableHeight / (1.2f + _lineSpacing / _fontSize);
        var bestSize = Mathf.FloorToInt(Mathf.Min(sizeByWidth, sizeByHeight));

        return Mathf.Clamp(bestSize, Mathf.RoundToInt(_fontSizeMin), Mathf.Min(Mathf.RoundToInt(_fontSizeMax), Mathf.RoundToInt(_fontSize)));
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SetAllDirty();
    }

    protected override void OnDisable()
    {
        CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
        base.OnDestroy();
    }
}

public class TextMeshPro : TMP_Text { }

public class TextMeshProUGUI : TMP_Text, IMaskable
{
    public void RecalculateMasking()
    {
        SetMaterialDirty();
    }
}

public class TMP_FontAsset : ScriptableObject
{
    public TMP_FontAssetTypes fontAssetType { get; set; } = TMP_FontAssetTypes.SDF;
    public Texture? atlasTexture { get; set; }
    public Material? material { get; set; }
    public Material[]? materials { get; set; }
    public Texture2D[]? atlasTextures { get; set; }
    public bool isMultiAtlasTexturesEnabled { get; set; }
    public int atlasPopulationMode { get; set; }
    public float atlasWidth { get; set; } = 2048f;
    public float atlasHeight { get; set; } = 2048f;
    public float atlasPadding { get; set; } = 5f;
    public float pointSize { get; set; } = 90f;
    public float scale { get; set; } = 1f;

    private readonly Dictionary<char, TMP_Character> _characterDictionary = new();
    private readonly List<TMP_Character> _characterLookupTable = new();
    private readonly Dictionary<uint, TMP_SpriteCharacter> _spriteCharacterDictionary = new();
    private readonly List<TMP_SpriteCharacter> _spriteCharacterLookupTable = new();

    public Dictionary<char, TMP_Character> characterDictionary
    {
        get => _characterDictionary;
        set
        {
            _characterDictionary.Clear();
            if (value != null)
            {
                foreach (var kvp in value)
                    _characterDictionary[kvp.Key] = kvp.Value;
            }
        }
    }

    public List<TMP_Character> characterLookupTable
    {
        get => _characterLookupTable;
        set
        {
            _characterLookupTable.Clear();
            if (value != null)
                _characterLookupTable.AddRange(value);
        }
    }

    public bool HasCharacter(char character)
    {
        return _characterDictionary.ContainsKey(character);
    }

    public bool HasCharacter(int character)
    {
        return HasCharacter((char)character);
    }

    public bool HasCharacters(string text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        foreach (var c in text)
        {
            if (!_characterDictionary.ContainsKey(c))
                return false;
        }
        return true;
    }

    public bool HasCharacters(char[] text)
    {
        if (text == null || text.Length == 0) return true;
        foreach (var c in text)
        {
            if (!_characterDictionary.ContainsKey(c))
                return false;
        }
        return true;
    }

    public TMP_Character? GetCharacter(char character)
    {
        _characterDictionary.TryGetValue(character, out var c);
        return c;
    }

    public bool TryAddCharacter(TMP_Character character)
    {
        if (character == null || _characterDictionary.ContainsKey(character.unicode))
            return false;
        _characterDictionary[character.unicode] = character;
        _characterLookupTable.Add(character);
        return true;
    }

    protected virtual void OnEnable()
    {
        if (material == null)
            material = new Material();
    }
}

public class TMP_Character
{
    public char unicode { get; set; }
    public int glyphIndex { get; set; }
    public float scale { get; set; } = 1f;
    public TMP_Glyph? glyph { get; set; }
}

public class TMP_Glyph
{
    public int index { get; set; }
    public float metricsWidth { get; set; }
    public float metricsHeight { get; set; }
    public float metricsHorizontalBearingX { get; set; }
    public float metricsHorizontalBearingY { get; set; }
    public float metricsHorizontalAdvance { get; set; }
    public Rect glyphRect { get; set; }
    public float scale { get; set; } = 1f;
    public int atlasIndex { get; set; }
}

public class TMP_TextInfo
{
    public TMP_CharacterInfo[] characterInfo { get; set; } = Array.Empty<TMP_CharacterInfo>();
    public TMP_WordInfo[] wordInfo { get; set; } = Array.Empty<TMP_WordInfo>();
    public TMP_LineInfo[] lineInfo { get; set; } = Array.Empty<TMP_LineInfo>();
    public TMP_LinkInfo[] linkInfo { get; set; } = Array.Empty<TMP_LinkInfo>();
    public TMP_PageInfo[] pageInfo { get; set; } = Array.Empty<TMP_PageInfo>();
    public int characterCount { get; set; }
    public int spriteCount { get; set; }
    public int wordCount { get; set; }
    public int lineCount { get; set; }
    public int pageCount { get; set; }
    public int linkCount { get; set; }
    public int materialCount { get; set; }
    public TMP_MeshInfo[] meshInfo { get; set; } = Array.Empty<TMP_MeshInfo>();
}

public struct TMP_CharacterInfo
{
    public char character;
    public int index;
    public int stringLength;
    public int lineNumber;
    public int pageNumber;
    public int wordNumber;
    public bool isVisible;
    public Vector3 bottomLeft;
    public Vector3 topLeft;
    public Vector3 topRight;
    public Vector3 bottomRight;
    public float origin;
    public float ascender;
    public float descender;
}

public struct TMP_WordInfo
{
    public int firstCharacterIndex;
    public int length;
    public char firstCharacter;
}

public struct TMP_LineInfo
{
    public int firstCharacterIndex;
    public int lastCharacterIndex;
    public int characterCount;
    public int visibleCharacterCount;
    public int wordCount;
    public float length;
    public float lineHeight;
    public float ascender;
    public float descender;
    public float maxAdvance;
}

public struct TMP_LinkInfo
{
    public int hashCode;
    public int linkIdFirstCharacterIndex;
    public int linkIdLength;
    public int linkTextfirstCharacterIndex;
    public int linkTextLength;
}

public struct TMP_PageInfo
{
    public int firstCharacterIndex;
    public int lastCharacterIndex;
    public float ascender;
    public float descender;
}

public class TMP_MeshInfo
{
    public int vertexCount;
    public Material? material;
}

public class TMP_SpriteCharacter
{
    public uint unicode { get; set; }
    public string name { get; set; } = string.Empty;
    public int glyphIndex { get; set; }
    public float scale { get; set; } = 1f;
}

public class TMP_SpriteGlyph
{
    public int index { get; set; }
    public Rect spriteRect { get; set; }
    public float metricsWidth { get; set; }
    public float metricsHeight { get; set; }
    public float scale { get; set; } = 1f;
    public int atlasIndex { get; set; }
}

public class TMP_SpriteAsset : ScriptableObject
{
    public TMP_SpriteAsset? spriteSheet;
    public Material? material { get; set; }
    public Texture? spriteSheetTexture { get; set; }
    public List<TMP_SpriteCharacter> spriteCharacterTable { get; set; } = new();
    public List<TMP_SpriteGlyph> spriteGlyphTable { get; set; } = new();
    public List<TMP_SpriteAsset> fallbackSpriteAssets { get; set; } = new();
    private readonly Dictionary<uint, TMP_SpriteCharacter> _spriteCharacterLookupDictionary = new();
    private readonly Dictionary<string, TMP_SpriteCharacter> _spriteNameLookupDictionary = new();

    public bool SearchForSpriteByUnicode(uint unicode, out TMP_SpriteCharacter? spriteCharacter)
    {
        if (_spriteCharacterLookupDictionary.TryGetValue(unicode, out spriteCharacter))
            return true;
        spriteCharacter = null;
        return false;
    }

    protected virtual void OnEnable()
    {
        _spriteCharacterLookupDictionary.Clear();
        foreach (var c in spriteCharacterTable)
            _spriteCharacterLookupDictionary[c.unicode] = c;
    }
}

public class TMP_Style
{
    public string name { get; set; } = string.Empty;
    public int hashCode { get; set; }
    public string styleOpeningDefinition { get; set; } = string.Empty;
    public string styleClosingDefinition { get; set; } = string.Empty;
    public TMP_TextProcessingStack styleStack { get; set; } = new();
}

public struct TMP_TextProcessingStack
{
    public int bold;
    public int italic;
    public int underline;
    public int strikethrough;
    public Color color;
}

public class TMP_StyleSheet : ScriptableObject
{
    public List<TMP_Style> styles { get; set; } = new();
    private readonly Dictionary<int, TMP_Style> _styleLookupDictionary = new();

    public TMP_Style? GetStyle(int hashCode)
    {
        _styleLookupDictionary.TryGetValue(hashCode, out var style);
        return style;
    }

    public TMP_Style? GetStyle(string name)
    {
        return GetStyle(name.GetHashCode());
    }

    protected virtual void OnEnable()
    {
        _styleLookupDictionary.Clear();
        foreach (var s in styles)
            _styleLookupDictionary[s.hashCode] = s;
    }

    public void UpdateStyleDictionaryLookup()
    {
        _styleLookupDictionary.Clear();
        foreach (var s in styles)
            _styleLookupDictionary[s.hashCode] = s;
    }
}

[AddComponentMenu("UI/TMP Input Field", 33)]
public class TMP_InputField : Selectable, IPointerClickHandler, ISubmitHandler, IUpdateSelectedHandler
{
    public enum ContentType
    {
        Standard,
        Autocorrected,
        IntegerNumber,
        DecimalNumber,
        Alphanumeric,
        Name,
        EmailAddress,
        Password,
        Pin,
        Custom
    }

    public enum LineType
    {
        SingleLine,
        MultiLineSubmit,
        MultiLineNewline
    }

    public enum InputType
    {
        Standard,
        AutoCorrect,
        Password
    }

    public enum CharacterValidation
    {
        None,
        Integer,
        Decimal,
        Alphanumeric,
        Name,
        EmailAddress
    }

    private string _text = string.Empty;
    private TMP_Text? _textComponent;
    private Graphic? _placeholder;
    private ContentType _contentType = ContentType.Standard;
    private LineType _lineType = LineType.SingleLine;
    private InputType _inputType = InputType.Standard;
    private CharacterValidation _characterValidation = CharacterValidation.None;
    private int _characterLimit;
    private char _asteriskChar = '*';
    private bool _readOnly;
    private int _caretPosition;
    private int _selectionAnchorPosition;
    private int _selectionFocusPosition;
    private bool _isFocused;
    private float _caretBlinkRate = 0.85f;
    private int _caretWidth = 1;
    private Color _selectionColor = new Color(0.65882355f, 0.8156863f, 1f, 0.7529412f);
    private bool _shouldHideMobileInput;
    private bool _caretVisible;

    private readonly TMP_InputFieldSubmitEvent _onEndEdit = new();
    private readonly TMP_InputFieldChangeEvent _onValueChanged = new();
    private readonly TMP_InputFieldSubmitEvent _onSubmit = new();
    private readonly TMP_InputFieldSelectionEvent _onSelect = new();
    private readonly TMP_InputFieldSelectionEvent _onDeselect = new();

    public string text
    {
        get => _text;
        set
        {
            var newText = value ?? string.Empty;
            newText = ClampAndValidate(newText);
            if (_text != newText)
            {
                _text = newText;
                _caretPosition = _text.Length;
                _selectionAnchorPosition = _caretPosition;
                _selectionFocusPosition = _caretPosition;
                UpdateLabel();
                _onValueChanged?.Invoke(_text);
            }
        }
    }

    public bool isFocused => _isFocused;
    public int caretPosition { get => _caretPosition; set => _caretPosition = value; }
    public int selectionAnchorPosition => _selectionAnchorPosition;
    public int selectionFocusPosition => _selectionFocusPosition;

    public TMP_Text? textComponent
    {
        get => _textComponent;
        set => _textComponent = value;
    }

    public Graphic? placeholder
    {
        get => _placeholder;
        set => _placeholder = value;
    }

    public ContentType contentType
    {
        get => _contentType;
        set
        {
            if (_contentType != value)
            {
                _contentType = value;
                EnforceContentType();
            }
        }
    }

    public LineType lineType
    {
        get => _lineType;
        set => _lineType = value;
    }

    public InputType inputType
    {
        get => _inputType;
        set => _inputType = value;
    }

    public CharacterValidation characterValidation
    {
        get => _characterValidation;
        set => _characterValidation = value;
    }

    public int characterLimit
    {
        get => _characterLimit;
        set => _characterLimit = value;
    }

    public char asteriskChar
    {
        get => _asteriskChar;
        set => _asteriskChar = value;
    }

    public bool readOnly
    {
        get => _readOnly;
        set => _readOnly = value;
    }

    public float caretBlinkRate
    {
        get => _caretBlinkRate;
        set => _caretBlinkRate = value;
    }

    public int caretWidth
    {
        get => _caretWidth;
        set => _caretWidth = value;
    }

    public Color selectionColor
    {
        get => _selectionColor;
        set => _selectionColor = value;
    }

    public bool shouldHideMobileInput
    {
        get => _shouldHideMobileInput;
        set => _shouldHideMobileInput = value;
    }

    public bool multiLine => _lineType != LineType.SingleLine;
    public bool caretVisible => _caretVisible;

    public TMP_InputFieldSubmitEvent onEndEdit
    {
        get => _onEndEdit;
    }

    public TMP_InputFieldChangeEvent onValueChanged
    {
        get => _onValueChanged;
    }

    public TMP_InputFieldSubmitEvent onSubmit
    {
        get => _onSubmit;
    }

    public TMP_InputFieldSelectionEvent onSelect
    {
        get => _onSelect;
    }

    public TMP_InputFieldSelectionEvent onDeselect
    {
        get => _onDeselect;
    }

    private void EnforceContentType()
    {
        switch (_contentType)
        {
            case ContentType.Standard:
                _inputType = InputType.Standard;
                _characterValidation = CharacterValidation.None;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.Autocorrected:
                _inputType = InputType.AutoCorrect;
                _characterValidation = CharacterValidation.None;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.IntegerNumber:
                _inputType = InputType.Standard;
                _characterValidation = CharacterValidation.Integer;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.DecimalNumber:
                _inputType = InputType.Standard;
                _characterValidation = CharacterValidation.Decimal;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.Alphanumeric:
                _inputType = InputType.Standard;
                _characterValidation = CharacterValidation.Alphanumeric;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.Name:
                _inputType = InputType.Standard;
                _characterValidation = CharacterValidation.Name;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.EmailAddress:
                _inputType = InputType.Standard;
                _characterValidation = CharacterValidation.EmailAddress;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.Password:
                _inputType = InputType.Password;
                _characterValidation = CharacterValidation.None;
                _lineType = LineType.SingleLine;
                break;
            case ContentType.Pin:
                _inputType = InputType.Password;
                _characterValidation = CharacterValidation.Integer;
                _lineType = LineType.SingleLine;
                break;
        }
    }

    private string ClampAndValidate(string input)
    {
        if (_characterLimit > 0 && input.Length > _characterLimit)
            input = input.Substring(0, _characterLimit);

        return _characterValidation switch
        {
            CharacterValidation.Integer => ValidateInteger(input),
            CharacterValidation.Decimal => ValidateDecimal(input),
            CharacterValidation.Alphanumeric => ValidateAlphanumeric(input),
            CharacterValidation.Name => ValidateName(input),
            CharacterValidation.EmailAddress => ValidateEmail(input),
            _ => input
        };
    }

    private static string ValidateInteger(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        if (int.TryParse(input, out _)) return input;
        var result = string.Empty;
        foreach (var c in input)
        {
            if (char.IsDigit(c) || (result.Length == 0 && c == '-'))
                result += c;
        }
        return result;
    }

    private static string ValidateDecimal(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        if (float.TryParse(input, out _)) return input;
        var result = string.Empty;
        var hasDot = false;
        foreach (var c in input)
        {
            if (char.IsDigit(c) || (result.Length == 0 && c == '-'))
                result += c;
            else if (c == '.' && !hasDot)
            {
                result += c;
                hasDot = true;
            }
        }
        return result;
    }

    private static string ValidateAlphanumeric(string input)
    {
        var result = string.Empty;
        foreach (var c in input)
            if (char.IsLetterOrDigit(c))
                result += c;
        return result;
    }

    private static string ValidateName(string input)
    {
        var result = string.Empty;
        var lastWasSpace = false;
        foreach (var c in input)
        {
            if (char.IsLetter(c) || c == ' ' || c == '-')
            {
                if (c == ' ' && lastWasSpace) continue;
                result += c;
                lastWasSpace = c == ' ';
            }
        }
        return result;
    }

    private static string ValidateEmail(string input)
    {
        var result = string.Empty;
        foreach (var c in input)
            if (char.IsLetterOrDigit(c) || c == '@' || c == '.' || c == '_' || c == '-')
                result += c;
        return result;
    }

    public void ActivateInputField()
    {
        if (!interactable || !IsActive()) return;
        _isFocused = true;
        _caretVisible = true;
        Select();
    }

    public void DeactivateInputField()
    {
        _isFocused = false;
        _caretVisible = false;
        _onEndEdit?.Invoke(_text);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        ActivateInputField();
    }

    public new void OnSubmit(BaseEventData eventData)
    {
        _ = eventData;
        if (!IsInteractable()) return;
        _onSubmit?.Invoke(_text);
        _onEndEdit?.Invoke(_text);
        if (_lineType == LineType.MultiLineNewline) return;
        DeactivateInputField();
    }

    public new void OnUpdateSelected(BaseEventData eventData)
    {
        _ = eventData;
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        ActivateInputField();
        _onSelect?.Invoke(_text);
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        base.OnDeselect(eventData);
        DeactivateInputField();
        _onDeselect?.Invoke(_text);
    }

    public void SetTextWithoutNotify(string input)
    {
        var newText = ClampAndValidate(input ?? string.Empty);
        if (_text != newText)
        {
            _text = newText;
            _caretPosition = _text.Length;
            _selectionAnchorPosition = _caretPosition;
            _selectionFocusPosition = _caretPosition;
            UpdateLabel();
        }
    }

    public void ForceLabelUpdate()
    {
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (_textComponent != null)
        {
            _textComponent.text = _inputType == InputType.Password ? new string(_asteriskChar, _text.Length) : _text;
        }
        if (_placeholder is not null)
        {
            _placeholder.enabled = string.IsNullOrEmpty(_text);
        }
    }

    protected override void OnDisable()
    {
        DeactivateInputField();
        base.OnDisable();
    }
}

[Serializable]
public class TMP_InputFieldSubmitEvent
{
    public event Action<string>? Submit;
    public void Invoke(string text) => Submit?.Invoke(text);
}

[Serializable]
public class TMP_InputFieldChangeEvent
{
    public event Action<string>? ValueChanged;
    public void Invoke(string text) => ValueChanged?.Invoke(text);
}

[Serializable]
public class TMP_InputFieldSelectionEvent
{
    public event Action<string>? Selection;
    public void Invoke(string text) => Selection?.Invoke(text);
}

[AddComponentMenu("UI/TMP Dropdown", 35)]
public class TMP_Dropdown : Selectable, IPointerClickHandler, ISubmitHandler, ICancelHandler
{
    public class OptionData
    {
        public string text { get; set; } = string.Empty;
        public Sprite? image { get; set; }

        public OptionData() { }
        public OptionData(string text) { this.text = text; }
        public OptionData(Sprite? image) { this.image = image; }
        public OptionData(string text, Sprite? image) { this.text = text; this.image = image; }
    }

    public class OptionDataList
    {
        public List<OptionData> options { get; set; } = new();
        public OptionDataList() { }
    }

    private RectTransform? _template;
    private TMP_Text? _captionText;
    private Image? _captionImage;
    private TMP_Text? _itemText;
    private Image? _itemImage;
    private float _alphaFadeSpeed = 0.15f;
    private List<OptionData> _options = new();
    private readonly TMP_DropdownEvent _onValueChanged = new();
    private int _value;
    private GameObject? _dropdown;
    private GameObject? _blocker;
    private readonly List<TMP_DropdownItem> _items = new();
    private bool _validTemplate;

    public RectTransform? template
    {
        get => _template;
        set
        {
            _template = value;
            RefreshShownValue();
        }
    }

    public TMP_Text? captionText
    {
        get => _captionText;
        set => _captionText = value;
    }

    public Image? captionImage
    {
        get => _captionImage;
        set => _captionImage = value;
    }

    public TMP_Text? itemText
    {
        get => _itemText;
        set => _itemText = value;
    }

    public Image? itemImage
    {
        get => _itemImage;
        set => _itemImage = value;
    }

    public List<OptionData> options
    {
        get => _options;
        set
        {
            _options = value ?? new List<OptionData>();
            RefreshShownValue();
        }
    }

    public float alphaFadeSpeed
    {
        get => _alphaFadeSpeed;
        set => _alphaFadeSpeed = value;
    }

    public int value
    {
        get => _value;
        set => Set(value);
    }

    public TMP_DropdownEvent onValueChanged
    {
        get => _onValueChanged;
    }

    public bool IsExpanded => _dropdown != null;

    private void Set(int value, bool sendCallback = true)
    {
        if (_value == value) return;
        _value = Mathf.Clamp(value, 0, Mathf.Max(0, _options.Count - 1));
        RefreshShownValue();
        if (sendCallback)
            _onValueChanged?.Invoke(_value);
    }

    public void RefreshShownValue()
    {
        var validIdx = Mathf.Clamp(_value, 0, Mathf.Max(0, _options.Count - 1));
        var data = _options.Count == 0 ? null : _options[validIdx];

        if (_captionText != null)
            _captionText.text = data != null ? data.text : string.Empty;
        if (_captionImage != null)
        {
            _captionImage.sprite = data?.image;
            _captionImage.enabled = data != null && data.image != null;
        }
    }

    public void AddOptions(List<OptionData> options)
    {
        if (options == null) return;
        _options.AddRange(options);
        RefreshShownValue();
    }

    public void AddOptions(List<string> options)
    {
        if (options == null) return;
        foreach (var t in options)
            _options.Add(new OptionData(t));
        RefreshShownValue();
    }

    public void AddOptions(List<Sprite> options)
    {
        if (options == null) return;
        foreach (var s in options)
            _options.Add(new OptionData(s));
        RefreshShownValue();
    }

    public void ClearOptions()
    {
        _options.Clear();
        _value = 0;
        RefreshShownValue();
    }

    public void Show()
    {
        if (_template == null || IsExpanded) return;
        var canvas = GetComponentInParent<Canvas>();
        var root = canvas != null ? canvas.transform : transform;
        _dropdown = CreateDropdownList(root);
        if (_dropdown == null) return;
        _dropdown.SetActive(true);
        _blocker = CreateBlocker(root);
    }

    public void Hide()
    {
        if (_dropdown != null)
        {
            Object.Destroy(_dropdown);
            _dropdown = null;
        }
        if (_blocker != null)
        {
            Object.Destroy(_blocker);
            _blocker = null;
        }
        _items.Clear();
    }

    private GameObject? CreateDropdownList(Transform root)
    {
        var go = new GameObject("Dropdown List");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        var canvas = go.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30000;
        go.AddComponent<CanvasGroup>();
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 1f);
        go.AddComponent<ScrollRect>();
        go.SetActive(false);
        return go;
    }

    private GameObject CreateBlocker(Transform root)
    {
        var go = new GameObject("Blocker");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        var canvas = go.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 29999;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        var blocker = go.AddComponent<TMP_DropdownBlocker>();
        blocker.dropdown = this;
        return go;
    }

    public void OnSelectItem(int index)
    {
        _value = index;
        RefreshShownValue();
        _onValueChanged?.Invoke(index);
        Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!IsActive() || !IsInteractable()) return;
        Show();
    }

    public new void OnSubmit(BaseEventData eventData)
    {
        if (!IsActive() || !IsInteractable()) return;
        Show();
    }

    public virtual void OnCancel(BaseEventData eventData)
    {
        Hide();
    }

    protected override void OnDisable()
    {
        Hide();
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        Hide();
        base.OnDestroy();
    }

    public void SetValueWithoutNotify(int input)
    {
        _value = Mathf.Clamp(input, 0, Mathf.Max(0, _options.Count - 1));
        RefreshShownValue();
    }
}

public class TMP_DropdownItem : MonoBehaviour, IPointerClickHandler
{
    public TMP_Text? text;
    public Image? image;
    public int index;

    public void OnPointerClick(PointerEventData eventData)
    {
        _ = eventData;
        var dropdown = GetComponentInParent<TMP_Dropdown>();
        dropdown?.OnSelectItem(index);
    }

    private T? GetComponentInParent<T>() where T : Component
    {
        for (var t = transform; t != null; t = t.parent)
        {
            var c = t.gameObject.GetComponent<T>();
            if (c != null) return c;
        }
        return null;
    }
}

[Serializable]
public class TMP_DropdownEvent
{
    public event Action<int>? ValueChanged;
    public void Invoke(int value) => ValueChanged?.Invoke(value);
}

public class TMP_DropdownBlocker : MonoBehaviour, IPointerClickHandler
{
    public TMP_Dropdown? dropdown;

    public void OnPointerClick(PointerEventData eventData)
    {
        _ = eventData;
        dropdown?.Hide();
    }
}
