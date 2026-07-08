using System;
using System.Collections.Generic;

namespace UnityEngine.UI;

public class Text : MaskableGraphic, ILayoutElement, ICanvasRescale
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
  private HorizontalWrapMode _horizontalOverflow;
  private VerticalWrapMode _verticalOverflow;
  private float _preferredWidth;
  private float _preferredHeight;
  private float _flexibleWidth;
  private float _flexibleHeight;
  private int _layoutPriority;
  private bool _alignByGeometry;
  private bool _updateMeshLayout;
  private bool _cplacedVerts;
  private float _minWidth;
  private float _minHeight;

  public string text
  {
    get => _text;
    set
    {
      if (_text != value)
      {
        _text = value ?? string.Empty;
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
        SetLayoutDirty();
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

  public float preferredWidth => _preferredWidth;
  public float preferredHeight => _preferredHeight;
  public float flexibleWidth => _flexibleWidth;
  public float flexibleHeight => _flexibleHeight;
  public int layoutPriority => _layoutPriority;
  public float minWidth => _minWidth;
  public float minHeight => _minHeight;

  public virtual float GetPreferredWidth()
  {
    return _preferredWidth;
  }

  public virtual float GetPreferredHeight()
  {
    return _preferredHeight;
  }

  public virtual float GetMinWidth()
  {
    return _minWidth;
  }

  public virtual float GetMinHeight()
  {
    return _minHeight;
  }

  public virtual void OnRebuildVertices()
  {
    SetVerticesDirty();
  }

  public virtual void OnPopulateMesh(Mesh mesh)
  {
    _ = mesh;
  }

  public virtual void CalculateLayoutInputHorizontal()
  {
  }

  public virtual void CalculateLayoutInputVertical()
  {
  }

  public void SetAllDirty()
  {
    SetLayoutDirty();
    SetVerticesDirty();
    SetMaterialDirty();
  }
}

public enum HorizontalWrapMode
{
  Wrap,
  Overflow
}

public enum VerticalWrapMode
{
  Truncate,
  Overflow
}

public interface ILayoutElement
{
  float minWidth { get; }
  float preferredWidth { get; }
  float flexibleWidth { get; }
  float minHeight { get; }
  float preferredHeight { get; }
  float flexibleHeight { get; }
  int layoutPriority { get; }
  void CalculateLayoutInputHorizontal();
  void CalculateLayoutInputVertical();
}

public interface ICanvasRescale
{
}
