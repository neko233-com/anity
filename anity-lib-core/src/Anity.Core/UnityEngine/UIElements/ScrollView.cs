using System;

namespace UnityEngine.UIElements;

public class ScrollView : VisualElement
{
  public ScrollDirection horizontalScrollerVisibility { get; set; }
  public ScrollDirection verticalScrollerVisibility { get; set; }
  public Scroller horizontalScroller { get; } = new();
  public Scroller verticalScroller { get; } = new();
  public float horizontalPageSize { get; set; } = -1f;
  public float verticalPageSize { get; set; } = -1f;
  public float scrollDecelerationRate { get; set; } = 0.135f;
  public bool elasticity { get; set; } = true;
  public float elasticityThreshold { get; set; } = 130f;
  public ScrollTouchBehavior touchWheelBehavior { get; set; }
  public bool nestedInteractionKind { get; set; }
  public bool mouseWheelScrollSize { get; set; }
  public bool horizontalWheelScrollSize { get; set; }
  public ScrollMomentum scrollMomentum { get; set; }
  public bool animateWheelDelta { get; set; }
  public bool isAnimated { get; set; }
  public ScrollType scrollType { get; set; }
  public bool elasticityEnabled { get; set; } = true;

  public Vector2 scrollOffset
  {
    get => new(horizontalScroller.value, verticalScroller.value);
    set
    {
      horizontalScroller.value = value.x;
      verticalScroller.value = value.y;
    }
  }

  public Vector2 contentViewportSize
  {
    get => new(layout.width, layout.height);
  }

  public float contentWidth
  {
    get => layout.width;
  }

  public float contentHeight
  {
    get => layout.height;
  }

  public event Action<Vector2> scrollPositionChanged;

  public ScrollView()
  {
  }

  public ScrollView(ScrollViewMode mode)
  {
    // Stub
  }

  public void ScrollTo(VisualElement child)
  {
    // Stub
  }

  public void ScrollTo(float x, float y)
  {
    horizontalScroller.value = x;
    verticalScroller.value = y;
  }

  public void UpdateContentViewTransform()
  {
    // Stub
  }

  public void ResetScroll()
  {
    horizontalScroller.value = 0;
    verticalScroller.value = 0;
  }
}

public enum ScrollViewMode
{
  Vertical = 0,
  Horizontal = 1,
  VerticalAndHorizontal = 2
}

public enum ScrollDirection
{
  Auto = 0,
  Horizontal = 1,
  Vertical = 2,
  Hidden = 3
}

public enum ScrollTouchBehavior
{
  Auto = 0,
  Enabled = 1,
  Disabled = 2
}

public enum ScrollMomentum
{
  None = 0,
  Elastic = 1,
  Stopped = 2
}

public enum ScrollType
{
  None = 0,
  Horizontal = 1,
  Vertical = 2,
  Both = 3
}

public class Scroller
{
  public float value { get; set; }
  public float lowValue { get; set; }
  public float highValue { get; set; }
  public float size { get; set; }
  public SliderDirection direction { get; set; }

  public event Action<float> valueChanged;

  public void SetValueWithoutNotify(float newValue)
  {
    value = newValue;
  }
}

public enum SliderDirection
{
  Horizontal = 0,
  Vertical = 1
}
