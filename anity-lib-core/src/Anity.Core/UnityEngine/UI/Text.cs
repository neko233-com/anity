using System;

namespace UnityEngine.UI;

public class Text : MaskableGraphic
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
  private float _horizontalOverflow;
  private float _verticalOverflow;
  private float _preferredWidth;
  private float _preferredHeight;
  private float _flexibleWidth;
  private float _flexibleHeight;
  private int _layoutPriority;

  public string text
  {
    get => _text;
    set
    {
      _text = value ?? string.Empty;
      SetVerticesDirty();
    }
  }

  public Font? font
  {
    get => _font;
    set
    {
      _font = value;
      SetVerticesDirty();
      SetMaterialDirty();
    }
  }

  public int fontSize
  {
    get => _fontSize;
    set
    {
      _fontSize = value;
      SetVerticesDirty();
    }
  }

  public TextAnchor alignment
  {
    get => _alignment;
    set
    {
      _alignment = value;
      SetVerticesDirty();
    }
  }

  public FontStyles fontStyle
  {
    get => _fontStyle;
    set
    {
      _fontStyle = value;
      SetVerticesDirty();
    }
  }

  public float lineSpacing
  {
    get => _lineSpacing;
    set => _lineSpacing = value;
  }

  public bool supportRichText
  {
    get => _supportRichText;
    set => _supportRichText = value;
  }

  public bool resizeTextForBestFit
  {
    get => _resizeTextForBestFit;
    set => _resizeTextForBestFit = value;
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

  public float preferredWidth => _preferredWidth;
  public float preferredHeight => _preferredHeight;
  public float flexibleWidth => _flexibleWidth;
  public float flexibleHeight => _flexibleHeight;
  public int layoutPriority => _layoutPriority;

  public virtual float GetPreferredWidth()
  {
    return _preferredWidth;
  }

  public virtual float GetPreferredHeight()
  {
    return _preferredHeight;
  }

  public virtual void OnRebuildVertices()
  {
    SetVerticesDirty();
  }
}
