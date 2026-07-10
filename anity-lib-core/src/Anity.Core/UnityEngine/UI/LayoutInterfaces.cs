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
  public int left { get; set; }
  public int right { get; set; }
  public int top { get; set; }
  public int bottom { get; set; }

  public RectOffset() { }
  public RectOffset(int left, int right, int top, int bottom)
  {
    this.left = left;
    this.right = right;
    this.top = top;
    this.bottom = bottom;
  }

  public int horizontal => left + right;
  public int vertical => top + bottom;
}
