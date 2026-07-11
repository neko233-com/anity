using System;
using UnityEngine;

namespace UnityEditor;

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
  public bool stretchWidth { get; set; }
  public bool stretchHeight { get; set; }
  public TextAnchor alignment { get; set; }
  public bool stretchTextWidth { get; set; }
  public TextClipping clipping2 { get; set; }
  public TextAnchor lineHeight { get; set; }
  public float lineHeightMultiplier { get; set; } = 1f;
  public GUIStyle() { }
  public GUIStyle(GUIStyle other)
  {
    name = other.name;
    normal = other.normal;
    hover = other.hover;
    active = other.active;
    focused = other.focused;
    onNormal = other.onNormal;
    onHover = other.onHover;
    onActive = other.onActive;
    onFocused = other.onFocused;
    border = other.border;
    margin = other.margin;
    padding = other.padding;
    overflow = other.overflow;
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
  }

  public Vector2 CalcSize(GUIContent content) => new(100f, 20f);
  public float CalcHeight(GUIContent content, float width) => 20f;
  public float CalcMinMaxWidth(GUIContent content, out float minWidth, out float maxWidth) { minWidth = 50f; maxWidth = 200f; return 20f; }
}

public sealed class RectOffset
{
  public float left { get; set; }
  public float right { get; set; }
  public float top { get; set; }
  public float bottom { get; set; }
  public float horizontal => left + right;
  public float vertical => top + bottom;
  public RectOffset() { }
  public RectOffset(float left, float right, float top, float bottom) { this.left = left; this.right = right; this.top = top; this.bottom = bottom; }
  public Rect Add(Rect rect) => new(rect.x + left, rect.y + top, rect.width - horizontal, rect.height - vertical);
  public Rect Remove(Rect rect) => new(rect.x - left, rect.y - top, rect.width + horizontal, rect.height + vertical);
}

public enum TextClipping { Overflow, Clip }
public enum ImagePosition { ImageLeft, ImageAbove, ImageOnly, TextOnly }
public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
