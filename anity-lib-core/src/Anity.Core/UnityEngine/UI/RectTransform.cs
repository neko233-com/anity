namespace UnityEngine;

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

public class RectTransform : Transform
{
  private Vector2 _anchoredPosition;
  private Vector2 _sizeDelta;
  private Vector2 _anchorMin = new(0.5f, 0.5f);
  private Vector2 _anchorMax = new(0.5f, 0.5f);
  private Vector2 _pivot = new(0.5f, 0.5f);
  private Vector3 _anchoredPosition3D;

  public Vector2 anchoredPosition
  {
    get => _anchoredPosition;
    set => _anchoredPosition = value;
  }

  public Vector3 anchoredPosition3D
  {
    get => _anchoredPosition3D;
    set => _anchoredPosition3D = value;
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
    get
    {
      Rect pr = GetParentRect();
      return new Vector2(rect.xMin - pr.xMin, rect.yMin - pr.yMin);
    }
    set
    {
      Rect pr = GetParentRect();
      _anchoredPosition.x = value.x + _sizeDelta.x * _pivot.x + pr.width * _anchorMin.x;
      _anchoredPosition.y = value.y + _sizeDelta.y * _pivot.y + pr.height * _anchorMin.y;
    }
  }

  public Vector2 offsetMax
  {
    get
    {
      Rect pr = GetParentRect();
      return new Vector2(rect.xMax - pr.xMax, rect.yMax - pr.yMax);
    }
    set
    {
      Rect pr = GetParentRect();
      _anchoredPosition.x = value.x - _sizeDelta.x * (1f - _pivot.x) + pr.width * _anchorMax.x;
      _anchoredPosition.y = value.y - _sizeDelta.y * (1f - _pivot.y) + pr.height * _anchorMax.y;
    }
  }

  public Rect rect
  {
    get
    {
      float w, h;
      bool anchorsSameX = MathF.Abs(_anchorMax.x - _anchorMin.x) < 1e-5f;
      bool anchorsSameY = MathF.Abs(_anchorMax.y - _anchorMin.y) < 1e-5f;
      Rect pr = GetParentRect();
      if (anchorsSameX) w = _sizeDelta.x;
      else w = pr.width * (_anchorMax.x - _anchorMin.x) + _sizeDelta.x;
      if (anchorsSameY) h = _sizeDelta.y;
      else h = pr.height * (_anchorMax.y - _anchorMin.y) + _sizeDelta.y;
      return new Rect(-_pivot.x * w, -_pivot.y * h, w, h);
    }
  }

  private Rect GetParentRect()
  {
    if (parent is RectTransform prt) return prt.rect;
    return new Rect(-10000f, -10000f, 20000f, 20000f);
  }

  public void GetLocalCorners(Vector3[] corners)
  {
    if (corners is null || corners.Length < 4) return;
    Rect r = rect;
    corners[0] = new Vector3(r.xMin, r.yMin, 0f);
    corners[1] = new Vector3(r.xMax, r.yMin, 0f);
    corners[2] = new Vector3(r.xMax, r.yMax, 0f);
    corners[3] = new Vector3(r.xMin, r.yMax, 0f);
  }

  public void GetWorldCorners(Vector3[] corners)
  {
    if (corners is null || corners.Length < 4) return;
    GetLocalCorners(corners);
    Matrix4x4 mat = localToWorldMatrix;
    for (int i = 0; i < 4; i++)
    {
      corners[i] = mat.MultiplyPoint(corners[i]);
    }
  }

  public void SetInsetAndSizeFromParentEdge(Edge edge, float inset, float size)
  {
    float posX = _anchoredPosition.x;
    float posY = _anchoredPosition.y;
    switch (edge)
    {
      case Edge.Left:
        _anchorMax = new Vector2(_anchorMin.x, _anchorMax.y);
        _sizeDelta = new Vector2(size, _sizeDelta.y);
        posX = inset + size * _pivot.x;
        break;
      case Edge.Right:
        _anchorMin = new Vector2(_anchorMax.x, _anchorMin.y);
        _sizeDelta = new Vector2(size, _sizeDelta.y);
        float xOffset = 1f - _pivot.x;
        posX = -inset - size * xOffset;
        break;
      case Edge.Top:
        _anchorMin = new Vector2(_anchorMin.x, _anchorMax.y);
        _sizeDelta = new Vector2(_sizeDelta.x, size);
        float yOffset = 1f - _pivot.y;
        posY = -inset - size * yOffset;
        break;
      case Edge.Bottom:
        _anchorMax = new Vector2(_anchorMax.x, _anchorMin.y);
        _sizeDelta = new Vector2(_sizeDelta.x, size);
        posY = inset + size * _pivot.y;
        break;
    }
    _anchoredPosition = new Vector2(posX, posY);
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

  public void ForceUpdateRects() {}

  public static void ForceUpdateRects(RectTransform[] rectTransforms)
  {
    _ = rectTransforms;
  }
}
