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

    _cachedText = _text;
    _cachedFontSize = _fontSize;
    _cachedPreferredHeight = _fontSize;
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

    var rect = rectTransform != null ? rectTransform.rect : new Rect(0f, 0f, 100f, 100f);
    var color32 = (Color32)color;
    var actualFontSize = _fontSize;

    if (_resizeTextForBestFit)
    {
      actualFontSize = CalculateBestFitSize(rect.width, rect.height);
    }

    var charWidth = actualFontSize * 0.5f;
    var textWidth = _text.Length * charWidth;
    var textHeight = actualFontSize;

    float xMin, yMin, xMax, yMax;

    switch (_alignment)
    {
      case TextAnchor.UpperLeft:
      case TextAnchor.MiddleLeft:
      case TextAnchor.LowerLeft:
        xMin = rect.xMin;
        xMax = rect.xMin + textWidth;
        break;
      case TextAnchor.UpperRight:
      case TextAnchor.MiddleRight:
      case TextAnchor.LowerRight:
        xMin = rect.xMax - textWidth;
        xMax = rect.xMax;
        break;
      default:
        xMin = rect.xMin + (rect.width - textWidth) * 0.5f;
        xMax = xMin + textWidth;
        break;
    }

    switch (_alignment)
    {
      case TextAnchor.UpperLeft:
      case TextAnchor.UpperCenter:
      case TextAnchor.UpperRight:
        yMax = rect.yMax;
        yMin = rect.yMax - textHeight;
        break;
      case TextAnchor.LowerLeft:
      case TextAnchor.LowerCenter:
      case TextAnchor.LowerRight:
        yMin = rect.yMin;
        yMax = rect.yMin + textHeight;
        break;
      default:
        yMin = rect.yMin + (rect.height - textHeight) * 0.5f;
        yMax = yMin + textHeight;
        break;
    }

    if (_horizontalOverflow == HorizontalWrapMode.Overflow)
    {
      xMin = rect.xMin;
      xMax = rect.xMax;
    }
    if (_verticalOverflow == VerticalWrapMode.Overflow)
    {
      yMin = rect.yMin;
      yMax = rect.yMax;
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
    if (string.IsNullOrEmpty(_text)) return _resizeTextMinSize;

    var charWidthRatio = 0.5f;
    var maxLineLen = _text.Length;

    var sizeByWidth = availableWidth / (maxLineLen * charWidthRatio);
    var sizeByHeight = availableHeight;
    var bestSize = Mathf.FloorToInt(Mathf.Min(sizeByWidth, sizeByHeight));

    return Mathf.Clamp(bestSize, _resizeTextMinSize, Mathf.Min(_resizeTextMaxSize, _fontSize));
  }

  public void SetNativeSize()
  {
    if (rectTransform is null) return;
    rectTransform.anchorMin = rectTransform.anchorMax;
    rectTransform.sizeDelta = new Vector2(preferredWidth, preferredHeight);
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
