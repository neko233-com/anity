namespace UnityEngine;

[Bindings.NativeHeader("Runtime/Transform/RectTransform.h")]
[NativeClass("UI::RectTransform")]
public sealed class RectTransform : Transform
{
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

  public delegate void ReapplyDrivenProperties(RectTransform driven);

  public static event ReapplyDrivenProperties? reapplyDrivenProperties;

  private Vector2 _sizeDelta = new(100f, 100f);
  private Vector2 _anchorMin = new(0.5f, 0.5f);
  private Vector2 _anchorMax = new(0.5f, 0.5f);
  private Vector2 _pivot = new(0.5f, 0.5f);
  private Object? _drivenByObject;
  private DrivenTransformProperties _drivenProperties;

  public Vector2 anchoredPosition
  {
    get
    {
      Vector2 reference = GetAnchorReference();
      Vector3 position = localPosition;
      return new Vector2(position.x - reference.x, position.y - reference.y);
    }
    set
    {
      Vector2 reference = GetAnchorReference();
      Vector3 position = localPosition;
      localPosition = new Vector3(value.x + reference.x, value.y + reference.y, position.z);
    }
  }

  public Vector3 anchoredPosition3D
  {
    get
    {
      Vector2 anchored = anchoredPosition;
      return new Vector3(anchored.x, anchored.y, localPosition.z);
    }
    set
    {
      anchoredPosition = new Vector2(value.x, value.y);
      Vector3 position = localPosition;
      localPosition = new Vector3(position.x, position.y, value.z);
    }
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
    get => anchoredPosition - Vector2.Scale(sizeDelta, pivot);
    set
    {
      Vector2 offset = value - (anchoredPosition - Vector2.Scale(sizeDelta, pivot));
      sizeDelta -= offset;
      anchoredPosition += Vector2.Scale(offset, Vector2.one - pivot);
    }
  }

  public Vector2 offsetMax
  {
    get => anchoredPosition + Vector2.Scale(sizeDelta, Vector2.one - pivot);
    set
    {
      Vector2 offset = value - (anchoredPosition + Vector2.Scale(sizeDelta, Vector2.one - pivot));
      sizeDelta += offset;
      anchoredPosition += Vector2.Scale(offset, pivot);
    }
  }

  public Rect rect
  {
    get
    {
      Vector2 parentSize = GetParentSize();
      float w = parentSize.x * (_anchorMax.x - _anchorMin.x) + _sizeDelta.x;
      float h = parentSize.y * (_anchorMax.y - _anchorMin.y) + _sizeDelta.y;
      return new Rect(-_pivot.x * w, -_pivot.y * h, w, h);
    }
  }

  public Object? drivenByObject => _drivenByObject;

  private Vector2 GetParentSize()
  {
    if (parent is RectTransform parentRect)
      return parentRect.rect.size;
    return Vector2.zero;
  }

  private Vector2 GetAnchorReference()
  {
    if (parent is not RectTransform parentRect)
      return Vector2.zero;
    Rect parentArea = parentRect.rect;
    float anchorX = _anchorMin.x + (_anchorMax.x - _anchorMin.x) * _pivot.x;
    float anchorY = _anchorMin.y + (_anchorMax.y - _anchorMin.y) * _pivot.y;
    return new Vector2(
      parentArea.x + parentArea.width * anchorX,
      parentArea.y + parentArea.height * anchorY);
  }

  public void GetLocalCorners(Vector3[] fourCornersArray)
  {
    if (fourCornersArray is null || fourCornersArray.Length < 4) return;
    Rect r = rect;
    fourCornersArray[0] = new Vector3(r.xMin, r.yMin, 0f);
    fourCornersArray[1] = new Vector3(r.xMin, r.yMax, 0f);
    fourCornersArray[2] = new Vector3(r.xMax, r.yMax, 0f);
    fourCornersArray[3] = new Vector3(r.xMax, r.yMin, 0f);
  }

  public void GetWorldCorners(Vector3[] fourCornersArray)
  {
    if (fourCornersArray is null || fourCornersArray.Length < 4) return;
    GetLocalCorners(fourCornersArray);
    Matrix4x4 mat = localToWorldMatrix;
    for (int i = 0; i < 4; i++)
      fourCornersArray[i] = mat.MultiplyPoint(fourCornersArray[i]);
  }

  public void SetInsetAndSizeFromParentEdge(Edge edge, float inset, float size)
  {
    int axis = edge == Edge.Top || edge == Edge.Bottom ? 1 : 0;
    bool end = edge == Edge.Top || edge == Edge.Right;
    float anchorValue = end ? 1f : 0f;
    Vector2 minimum = anchorMin;
    Vector2 maximum = anchorMax;
    minimum[axis] = anchorValue;
    maximum[axis] = anchorValue;
    anchorMin = minimum;
    anchorMax = maximum;

    Vector2 delta = sizeDelta;
    delta[axis] = size;
    sizeDelta = delta;

    Vector2 position = anchoredPosition;
    position[axis] = end ? -inset - size * (1f - pivot[axis]) : inset + size * pivot[axis];
    anchoredPosition = position;
  }

  public void SetSizeWithCurrentAnchors(Axis axis, float size)
  {
    int index = (int)axis;
    Vector2 delta = sizeDelta;
    Vector2 parentSize = GetParentSize();
    delta[index] = size - parentSize[index] * (anchorMax[index] - anchorMin[index]);
    sizeDelta = delta;
  }

  [Bindings.NativeMethod("UpdateIfTransformDispatchIsDirty")]
  public void ForceUpdateRectTransforms()
  {
  }

  internal DrivenTransformProperties drivenProperties => _drivenProperties;

  internal void SetDriven(Object? driver, DrivenTransformProperties properties)
  {
    _drivenByObject = driver;
    _drivenProperties = properties;
  }

  internal static void SendReapplyDrivenProperties(RectTransform driven)
  {
    reapplyDrivenProperties?.Invoke(driven);
  }
}
