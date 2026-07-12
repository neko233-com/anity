using System;

namespace UnityEngine;

public sealed class GUIStyleState
{
  public Color textColor { get; set; } = Color.black;
  public Texture2D? background { get; set; }
  public Texture2D[]? scaledBackgrounds { get; set; }
}

public sealed class GUIStyle
{
  public string name { get; set; } = string.Empty;
  public GUIStyleState normal { get; set; } = new();
  public GUIStyleState hover { get; set; } = new();
  public GUIStyleState active { get; set; } = new();
  public GUIStyleState focused { get; set; } = new();
  public GUIStyleState onNormal { get; set; } = new();
  public GUIStyleState onHover { get; set; } = new();
  public GUIStyleState onActive { get; set; } = new();
  public GUIStyleState onFocused { get; set; } = new();
  public RectOffset border { get; set; } = new();
  public RectOffset margin { get; set; } = new();
  public RectOffset padding { get; set; } = new();
  public RectOffset overflow { get; set; } = new();
  public Font? font { get; set; }
  public int fontSize { get; set; }
  public FontStyle fontStyle { get; set; }
  public bool richText { get; set; }
  public bool wordWrap { get; set; }
  public TextClipping clipping { get; set; }
  public ImagePosition imagePosition { get; set; }
  public Vector2 contentOffset { get; set; }
  public float fixedWidth { get; set; }
  public float fixedHeight { get; set; }
  public bool stretchWidth { get; set; } = true;
  public bool stretchHeight { get; set; }
  public TextAnchor alignment { get; set; }
  public bool stretchTextWidth { get; set; }
  public float lineHeight { get; set; } = 18f;
  public float lineHeightMultiplier { get; set; } = 1f;

  public GUIStyle() { }

  public GUIStyle(GUIStyle other)
  {
    name = other.name;
    normal = new GUIStyleState { textColor = other.normal.textColor, background = other.normal.background, scaledBackgrounds = other.normal.scaledBackgrounds };
    hover = new GUIStyleState { textColor = other.hover.textColor, background = other.hover.background, scaledBackgrounds = other.hover.scaledBackgrounds };
    active = new GUIStyleState { textColor = other.active.textColor, background = other.active.background, scaledBackgrounds = other.active.scaledBackgrounds };
    focused = new GUIStyleState { textColor = other.focused.textColor, background = other.focused.background, scaledBackgrounds = other.focused.scaledBackgrounds };
    onNormal = new GUIStyleState { textColor = other.onNormal.textColor, background = other.onNormal.background, scaledBackgrounds = other.onNormal.scaledBackgrounds };
    onHover = new GUIStyleState { textColor = other.onHover.textColor, background = other.onHover.background, scaledBackgrounds = other.onHover.scaledBackgrounds };
    onActive = new GUIStyleState { textColor = other.onActive.textColor, background = other.onActive.background, scaledBackgrounds = other.onActive.scaledBackgrounds };
    onFocused = new GUIStyleState { textColor = other.onFocused.textColor, background = other.onFocused.background, scaledBackgrounds = other.onFocused.scaledBackgrounds };
    border = new RectOffset(other.border.left, other.border.right, other.border.top, other.border.bottom);
    margin = new RectOffset(other.margin.left, other.margin.right, other.margin.top, other.margin.bottom);
    padding = new RectOffset(other.padding.left, other.padding.right, other.padding.top, other.padding.bottom);
    overflow = new RectOffset(other.overflow.left, other.overflow.right, other.overflow.top, other.overflow.bottom);
    font = other.font;
    fontSize = other.fontSize;
    fontStyle = other.fontStyle;
    richText = other.richText;
    wordWrap = other.wordWrap;
    clipping = other.clipping;
    imagePosition = other.imagePosition;
    contentOffset = other.contentOffset;
    fixedWidth = other.fixedWidth;
    fixedHeight = other.fixedHeight;
    stretchWidth = other.stretchWidth;
    stretchHeight = other.stretchHeight;
    alignment = other.alignment;
    stretchTextWidth = other.stretchTextWidth;
    lineHeight = other.lineHeight;
    lineHeightMultiplier = other.lineHeightMultiplier;
  }

  public Vector2 CalcSize(GUIContent content) => new(100f, string.IsNullOrEmpty(content?.text) ? lineHeight : lineHeight);
  public float CalcHeight(GUIContent content, float width) => lineHeight;
  public float CalcMinMaxWidth(GUIContent content, out float minWidth, out float maxWidth) { minWidth = 50f; maxWidth = 200f; return lineHeight; }
  public float CalcHeight(GUIContent content) => CalcHeight(content, 0f);
}

public sealed class RectOffset
{
  public int left { get; set; }
  public int right { get; set; }
  public int top { get; set; }
  public int bottom { get; set; }
  public int horizontal => left + right;
  public int vertical => top + bottom;

  public RectOffset() { }
  public RectOffset(int left, int right, int top, int bottom) { this.left = left; this.right = right; this.top = top; this.bottom = bottom; }

  public Rect Add(Rect rect) => new(rect.x + left, rect.y + top, rect.width - horizontal, rect.height - vertical);
  public Rect Remove(Rect rect) => new(rect.x - left, rect.y - top, rect.width + horizontal, rect.height + vertical);
}

public enum TextClipping { Overflow, Clip }
public enum ImagePosition { ImageLeft, ImageAbove, ImageOnly, TextOnly }
public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
public enum ImageType { Simple, Sliced, Tiled, Filled }
