using System;
using System.Collections.Generic;

namespace UnityEngine.UI;

public class Text : MaskableGraphic, ILayoutElement
{
  private string _text = string.Empty;
  private Font? _font;
  private int _fontSize = 14;
  private TextAnchor _alignment = TextAnchor.UpperLeft;
  private FontStyles _fontStyle = FontStyles.Normal;
  private float _lineSpacing = 1f;
  private bool _supportRichText = true;
  private bool _resizeTextForBestFit;
  private int _resizeTextMinSize = 10;
  private int _resizeTextMaxSize = 40;
  private HorizontalWrapMode _horizontalOverflow = HorizontalWrapMode.Wrap;
  private VerticalWrapMode _verticalOverflow = VerticalWrapMode.Truncate;
  private bool _alignByGeometry;

  private float _cachedPreferredWidth = -1f;
  private float _cachedPreferredHeight = -1f;
  private string _cachedText = string.Empty;
  private int _cachedFontSize;

  private static Texture2D? s_WhiteTexture;
  private static Texture2D whiteTexture
  {
    get
    {
      if (s_WhiteTexture == null)
      {
        s_WhiteTexture = new Texture2D();
      }
      return s_WhiteTexture;
    }
  }

  public override Texture mainTexture => whiteTexture;

  public string text
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

  public Font? font
  {
    get => _font;
    set
    {
      if (_font != value)
      {
        _font = value;
        _cachedPreferredWidth = -1f;
        _cachedPreferredHeight = -1f;
        SetVerticesDirty();
        SetMaterialDirty();
        SetLayoutDirty();
      }
    }
  }

  public int fontSize
  {
    get => _fontSize;
    set
    {
      if (_fontSize != value)
      {
        _fontSize = value;
        _cachedPreferredWidth = -1f;
        _cachedPreferredHeight = -1f;
        SetVerticesDirty();
        SetLayoutDirty();
      }
    }
  }

  public TextAnchor alignment
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

  public float lineSpacing
  {
    get => _lineSpacing;
    set
    {
      if (_lineSpacing != value)
      {
        _lineSpacing = value;
        _cachedPreferredHeight = -1f;
        SetVerticesDirty();
        SetLayoutDirty();
      }
    }
  }

  public bool supportRichText
  {
    get => _supportRichText;
    set
    {
      if (_supportRichText != value)
      {
        _supportRichText = value;
        SetVerticesDirty();
      }
    }
  }

  public bool resizeTextForBestFit
  {
    get => _resizeTextForBestFit;
    set
    {
      if (_resizeTextForBestFit != value)
      {
        _resizeTextForBestFit = value;
        _cachedPreferredWidth = -1f;
        _cachedPreferredHeight = -1f;
        SetVerticesDirty();
        SetLayoutDirty();
      }
    }
  }

  public int resizeTextMinSize
  {
    get => _resizeTextMinSize;
    set => _resizeTextMinSize = value;
  }

  public int resizeTextMaxSize
  {
    get => _resizeTextMaxSize;
    set => _resizeTextMaxSize = value;
  }

  public bool alignByGeometry
  {
    get => _alignByGeometry;
    set
    {
      if (_alignByGeometry != value)
      {
        _alignByGeometry = value;
        SetVerticesDirty();
      }
    }
  }

  public HorizontalWrapMode horizontalOverflow
  {
    get => _horizontalOverflow;
    set
    {
      if (_horizontalOverflow != value)
      {
        _horizontalOverflow = value;
        SetVerticesDirty();
        SetLayoutDirty();
      }
    }
  }

  public VerticalWrapMode verticalOverflow
  {
    get => _verticalOverflow;
    set
    {
      if (_verticalOverflow != value)
      {
        _verticalOverflow = value;
        SetVerticesDirty();
        SetLayoutDirty();
      }
    }
  }

  public virtual float minWidth => 0f;
  public virtual float preferredWidth => CalculatePreferredWidth();
  public virtual float flexibleWidth => -1f;
  public virtual float minHeight => 0f;
  public virtual float preferredHeight => CalculatePreferredHeight();
  public virtual float flexibleHeight => -1f;
  public virtual int layoutPriority => 0;

  private float CalculatePreferredWidth()
  {
    if (_cachedPreferredWidth >= 0f && _cachedText == _text && _cachedFontSize == _fontSize)
      return _cachedPreferredWidth;

    var charWidth = _fontSize * 0.5f;
    var maxLineWidth = 0f;
    var lines = _text.Split('\n');
    foreach (var line in lines)
    {
      var lineWidth = line.Length * charWidth;
      if (lineWidth > maxLineWidth)
        maxLineWidth = lineWidth;
    }

    _cachedText = _text;
    _cachedFontSize = _fontSize;
    _cachedPreferredWidth = maxLineWidth;
    return _cachedPreferredWidth;
  }

  private float CalculatePreferredHeight()
  {
    if (_cachedPreferredHeight >= 0f && _cachedText == _text && _cachedFontSize == _fontSize)
      return _cachedPreferredHeight;

    var lineHeight = _fontSize * 1.2f * _lineSpacing;
    var lines = _text.Split('\n');
    var numLines = lines.Length;
    if (numLines == 0) numLines = 1;

    _cachedText = _text;
    _cachedFontSize = _fontSize;
    _cachedPreferredHeight = numLines * lineHeight;
    return _cachedPreferredHeight;
  }

  public virtual void CalculateLayoutInputHorizontal()
  {
  }

  public virtual void CalculateLayoutInputVertical()
  {
  }

  public virtual void OnPopulateMesh(VertexHelper vh)
  {
    vh.Clear();

    if (string.IsNullOrEmpty(_text))
      return;

    var rect = rectTransform != null ? rectTransform.rect : new Rect(0f, 0f, 100f, 100f);
    var charWidth = _fontSize * 0.5f;
    var lineHeight = _fontSize * 1.2f * _lineSpacing;
    var actualFontSize = _fontSize;

    if (_resizeTextForBestFit)
    {
      actualFontSize = CalculateBestFitSize(rect.width, rect.height);
      charWidth = actualFontSize * 0.5f;
      lineHeight = actualFontSize * 1.2f * _lineSpacing;
    }

    var color32 = (Color32)color;
    var lines = _text.Split('\n');
    var totalHeight = lines.Length * lineHeight;
    var verticalStart = GetVerticalStartPosition(rect.height, totalHeight);

    for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
    {
      var line = lines[lineIdx];
      var lineWidth = line.Length * charWidth;
      var horizontalStart = GetHorizontalStartPosition(rect.width, lineWidth);
      var y = verticalStart - lineIdx * lineHeight;

      if (_verticalOverflow == VerticalWrapMode.Truncate && y < rect.yMin - lineHeight)
        break;

      for (var charIdx = 0; charIdx < line.Length; charIdx++)
      {
        var x = horizontalStart + charIdx * charWidth;
        if (_horizontalOverflow == HorizontalWrapMode.Wrap && x + charWidth > rect.xMax)
          break;

        AddCharacterQuad(vh, x, y, charWidth, lineHeight, color32, charIdx, line.Length);
      }
    }
  }

  private int CalculateBestFitSize(float availableWidth, float availableHeight)
  {
    if (string.IsNullOrEmpty(_text)) return _resizeTextMinSize;

    var charWidthRatio = 0.5f;
    var lineHeightRatio = 1.2f * _lineSpacing;
    var lines = _text.Split('\n');
    var maxLineLen = 0;
    foreach (var line in lines)
      if (line.Length > maxLineLen) maxLineLen = line.Length;

    var sizeByWidth = availableWidth / (maxLineLen * charWidthRatio);
    var sizeByHeight = availableHeight / (lines.Length * lineHeightRatio);
    var bestSize = Mathf.FloorToInt(Mathf.Min(sizeByWidth, sizeByHeight));

    return Mathf.Clamp(bestSize, _resizeTextMinSize, Mathf.Min(_resizeTextMaxSize, _fontSize));
  }

  private float GetHorizontalStartPosition(float rectWidth, float lineWidth)
  {
    return _alignment switch
    {
      TextAnchor.UpperLeft or TextAnchor.MiddleLeft or TextAnchor.LowerLeft => 0f,
      TextAnchor.UpperCenter or TextAnchor.MiddleCenter or TextAnchor.LowerCenter => (rectWidth - lineWidth) * 0.5f,
      TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight => rectWidth - lineWidth,
      _ => 0f
    };
  }

  private float GetVerticalStartPosition(float rectHeight, float totalHeight)
  {
    return _alignment switch
    {
      TextAnchor.UpperLeft or TextAnchor.UpperCenter or TextAnchor.UpperRight => rectHeight,
      TextAnchor.MiddleLeft or TextAnchor.MiddleCenter or TextAnchor.MiddleRight => rectHeight - (rectHeight - totalHeight) * 0.5f,
      TextAnchor.LowerLeft or TextAnchor.LowerCenter or TextAnchor.LowerRight => totalHeight,
      _ => rectHeight
    };
  }

  private void AddCharacterQuad(VertexHelper vh, float x, float y, float w, float h, Color32 color, int charIdx, int lineLen)
  {
    var uvOffsetX = (float)charIdx / Mathf.Max(1, lineLen);
    var uvW = 1f / Mathf.Max(1, lineLen);

    var startIndex = vh.currentVertCount;

    vh.AddVert(new Vector3(x, y - h, 0f), color, new Vector4(uvOffsetX, 0f, 0f, 1f));
    vh.AddVert(new Vector3(x, y, 0f), color, new Vector4(uvOffsetX, 1f, 0f, 1f));
    vh.AddVert(new Vector3(x + w, y, 0f), color, new Vector4(uvOffsetX + uvW, 1f, 0f, 1f));
    vh.AddVert(new Vector3(x + w, y - h, 0f), color, new Vector4(uvOffsetX + uvW, 0f, 0f, 1f));

    vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
    vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
  }

  public void SetNativeSize()
  {
    if (rectTransform is null) return;
    rectTransform.anchorMin = rectTransform.anchorMax;
    rectTransform.sizeDelta = new Vector2(preferredWidth, preferredHeight);
  }

  protected override void UpdateGeometry()
  {
    if (canvasRenderer is null) return;
    var vh = new VertexHelper();
    OnPopulateMesh(vh);
    var verts = new List<UIVertex>();
    vh.GetUIVertexStream(verts);
    canvasRenderer.SetVertices(verts);
    vh.Dispose();
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
