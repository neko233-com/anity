namespace UnityEngine.UI;

public enum TextAnchor
{
  UpperLeft = 0,
  UpperCenter = 1,
  UpperRight = 2,
  MiddleLeft = 3,
  MiddleCenter = 4,
  MiddleRight = 5,
  LowerLeft = 6,
  LowerCenter = 7,
  LowerRight = 8
}

public enum TextOverflow
{
  Overflow = 0,
  Ellipsis = 1,
  Mask = 2,
  Truncate = 3
}

public enum FontStyles
{
  Normal = 0,
  Bold = 1,
  Italic = 2,
  BoldAndItalic = 3
}

public enum ImageFillMethod
{
  Horizontal = 0,
  Vertical = 1,
  Radial90 = 2,
  Radial180 = 3,
  Radial360 = 4
}

public enum ImageType
{
  Simple = 0,
  Sliced = 1,
  Tiled = 2,
  Filled = 3
}

public enum BlendMode
{
  Zero = 0,
  One = 1,
  DstColor = 2,
  SrcColor = 3,
  OneMinusDstColor = 4,
  SrcAlpha = 5,
  OneMinusSrcColor = 6,
  DstAlpha = 7,
  OneMinusDstAlpha = 8,
  SrcAlphaSaturate = 9,
  OneMinusSrcAlpha = 10
}

public enum ColorBlockColorMode
{
  Multiplied = 0,
  Tinted = 1
}

public enum NavigationMode
{
  None = 0,
  Horizontal = 1,
  Vertical = 2,
  Automatic = 3,
  Explicit = 4
}

public enum Transition
{
  None = 0,
  ColorTint = 1,
  SpriteSwap = 2,
  Animation = 3
}

public class RectTransform : Transform
{
  private Vector2 _anchoredPosition;
  private Vector2 _sizeDelta;
  private Vector2 _anchorMin = new(0.5f, 0.5f);
  private Vector2 _anchorMax = new(0.5f, 0.5f);
  private Vector2 _pivot = new(0.5f, 0.5f);
  private Vector2 _offsetMin;
  private Vector2 _offsetMax;
  private Vector2 _anchoredPosition3D;

  public Vector2 anchoredPosition
  {
    get => _anchoredPosition;
    set => _anchoredPosition = value;
  }

  public Vector3 anchoredPosition3D
  {
    get => new Vector3(_anchoredPosition3D.x, _anchoredPosition3D.y, 0f);
    set => _anchoredPosition3D = new Vector2(value.x, value.y);
  }

  public Vector2 sizeDelta
  {
    get => _sizeDelta;
    set => _sizeDelta = value;
  }

  public Vector2 anchorMin
  {
    get => _anchorMin;
    set => _anchorMin = value;
  }

  public Vector2 anchorMax
  {
    get => _anchorMax;
    set => _anchorMax = value;
  }

  public Vector2 pivot
  {
    get => _pivot;
    set => _pivot = value;
  }

  public Vector2 offsetMin
  {
    get => _offsetMin;
    set => _offsetMin = value;
  }

  public Vector2 offsetMax
  {
    get => _offsetMax;
    set => _offsetMax = value;
  }

  public Rect rect => new(
    _anchoredPosition.x - _sizeDelta.x * _pivot.x,
    _anchoredPosition.y - _sizeDelta.y * _pivot.y,
    _sizeDelta.x,
    _sizeDelta.y);

  public Vector2 GetAnchoredPosition()
  {
    return _anchoredPosition;
  }

  public void SetSizeWithCurrentAnchors(Axis axis, float size)
  {
    if (axis == Axis.Horizontal)
    {
      _sizeDelta = new Vector2(size, _sizeDelta.y);
    }
    else
    {
      _sizeDelta = new Vector2(_sizeDelta.x, size);
    }
  }

  public void SetInsetAndSizeFromParentEdge(Edge edge, float inset, float size)
  {
    _ = edge;
    _ = inset;
    _ = size;
  }

  public void SetAnchorMin3D(Vector3 anchorMin)
  {
    _anchorMin = new Vector2(anchorMin.x, anchorMin.y);
  }

  public void SetAnchorMax3D(Vector3 anchorMax)
  {
    _anchorMax = new Vector2(anchorMax.x, anchorMax.y);
  }

  public void SetPivot(Vector2 pivot)
  {
    this.pivot = pivot;
  }
}

public enum Axis
{
  Horizontal = 0,
  Vertical = 1
}

public enum Edge
{
  Left = 0,
  Right = 1,
  Top = 2,
  Bottom = 3
}
