namespace UnityEngine.UI;

public interface ILayoutController
{
  void SetLayoutHorizontal();
  void SetLayoutVertical();
}

public interface ILayoutGroup : ILayoutController
{
}

public interface ILayoutSelfController : ILayoutController
{
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

public interface ILayoutElement2D : ILayoutElement
{
}

public interface ILayoutIgnorer
{
  bool ignoreLayout { get; }
}

public interface ILayoutTransform
{
  Vector2 anchoredPosition { get; set; }
  Vector2 sizeDelta { get; set; }
  Vector2 anchorMin { get; set; }
  Vector2 anchorMax { get; set; }
  Vector2 pivot { get; set; }
}

public struct RectOffset
{
  public float left { get; set; }
  public float right { get; set; }
  public float top { get; set; }
  public float bottom { get; set; }

  public RectOffset() { }
  public RectOffset(float left, float right, float top, float bottom)
  {
    this.left = left;
    this.right = right;
    this.top = top;
    this.bottom = bottom;
  }

  public float horizontal => left + right;
  public float vertical => top + bottom;
}
