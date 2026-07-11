using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

public class ScrollView : VisualElement
{
  public static readonly string ussClassName = "unity-scroll-view";
  public static readonly string viewportUssClassName = ussClassName + "__content-viewport";
  public static readonly string contentContainerUssClassName = ussClassName + "__content-container";
  public static readonly string hScrollerUssClassName = ussClassName + "__horizontal-scroller";
  public static readonly string vScrollerUssClassName = ussClassName + "__vertical-scroller";
  public static readonly string horizontalVariantUssClassName = ussClassName + "--horizontal";
  public static readonly string verticalVariantUssClassName = ussClassName + "--vertical";
  public static readonly string verticalHorizontalVariantUssClassName = ussClassName + "--vertical-horizontal";

  private readonly VisualElement _contentViewport;
  private readonly VisualElement _contentContainer;
  private Vector2 _scrollOffset;
  private ScrollViewMode _mode;
  private Vector2 _velocity;
  private bool _isDragging;
  private Vector2 _dragStartPosition;
  private Vector2 _dragStartScroll;
  private float _viewportWidth;
  private float _viewportHeight;

  public ScrollDirection horizontalScrollerVisibility { get; set; }
  public ScrollDirection verticalScrollerVisibility { get; set; }
  public Scroller horizontalScroller { get; } = new() { direction = SliderDirection.Horizontal };
  public Scroller verticalScroller { get; } = new() { direction = SliderDirection.Vertical };
  public float horizontalPageSize { get; set; } = -1f;
  public float verticalPageSize { get; set; } = -1f;
  public float scrollDecelerationRate { get; set; } = 0.135f;
  public float elasticity { get; set; } = 0.1f;
  public float elasticityThreshold { get; set; } = 130f;
  public ScrollTouchBehavior touchScrollBehavior { get; set; } = ScrollTouchBehavior.Auto;
  public NestedInteractionKind nestedInteractionKind { get; set; } = NestedInteractionKind.Default;
  public float mouseWheelScrollSize { get; set; } = 20f;
  public float horizontalWheelScrollSize { get; set; } = 20f;
  public ScrollMomentum scrollMomentum { get; set; } = ScrollMomentum.Elastic;
  public bool animateWheelDelta { get; set; }
  public bool isAnimated { get; set; }
  public ScrollType scrollType { get; set; } = ScrollType.Both;
  public bool elasticityEnabled { get; set; } = true;
  public bool showHorizontal
  {
    get => horizontalScrollerVisibility != ScrollDirection.Hidden;
    set => horizontalScrollerVisibility = value ? ScrollDirection.Auto : ScrollDirection.Hidden;
  }
  public bool showVertical
  {
    get => verticalScrollerVisibility != ScrollDirection.Hidden;
    set => verticalScrollerVisibility = value ? ScrollDirection.Auto : ScrollDirection.Hidden;
  }
  public bool horizontalScrollerEnabled => _mode != ScrollViewMode.Vertical;
  public bool verticalScrollerEnabled => _mode != ScrollViewMode.Horizontal;

  public Vector2 scrollOffset
  {
    get => _scrollOffset;
    set => SetScrollOffset(value);
  }

  public float contentViewportWidth => _viewportWidth;
  public float contentViewportHeight => _viewportHeight;
  public Vector2 contentViewportSize => new Vector2(contentViewportWidth, contentViewportHeight);
  public float contentWidth => _contentContainer?.layout.width ?? 0f;
  public float contentHeight => _contentContainer?.layout.height ?? 0f;
  public Vector2 contentSize => new Vector2(contentWidth, contentHeight);
  public Rect contentBoundingBox => new Rect(0, 0, contentWidth, contentHeight);

  public VisualElement contentContainer => _contentContainer;
  public Vector2 worldBoundary { get; private set; }
  public event Action<Vector2> scrollPositionChanged;

  public ScrollView() : this(ScrollViewMode.Vertical)
  {
  }

  public ScrollView(ScrollViewMode mode)
  {
    _mode = mode;
    _contentViewport = new VisualElement { name = "unity-content-viewport" };
    _contentViewport.AddClass(viewportUssClassName);
    _contentContainer = new VisualElement { name = "unity-content-container" };
    _contentContainer.AddClass(contentContainerUssClassName);
    _contentViewport.Add(_contentContainer);
    Add(_contentViewport);

    horizontalScroller.AddClass(hScrollerUssClassName);
    verticalScroller.AddClass(vScrollerUssClassName);
    Add(horizontalScroller);
    Add(verticalScroller);

    horizontalScroller.valueChanged += OnHorizontalScroll;
    verticalScroller.valueChanged += OnVerticalScroll;

    UpdateModeClass();
  }

  private void UpdateModeClass()
  {
    RemoveClass(horizontalVariantUssClassName);
    RemoveClass(verticalVariantUssClassName);
    RemoveClass(verticalHorizontalVariantUssClassName);
    switch (_mode)
    {
      case ScrollViewMode.Horizontal:
        AddClass(horizontalVariantUssClassName);
        break;
      case ScrollViewMode.Vertical:
        AddClass(verticalVariantUssClassName);
        break;
      case ScrollViewMode.VerticalAndHorizontal:
        AddClass(verticalHorizontalVariantUssClassName);
        break;
    }
  }

  private void OnHorizontalScroll(float value)
  {
    _scrollOffset.x = value;
    ApplyScrollOffset();
    scrollPositionChanged?.Invoke(_scrollOffset);
  }

  private void OnVerticalScroll(float value)
  {
    _scrollOffset.y = value;
    ApplyScrollOffset();
    scrollPositionChanged?.Invoke(_scrollOffset);
  }

  public void SetViewportSize(float width, float height)
  {
    _viewportWidth = width;
    _viewportHeight = height;
    UpdateScrollerValues();
  }

  private void ApplyScrollOffset()
  {
    float maxX = Mathf.Max(0, contentWidth - _viewportWidth);
    float maxY = Mathf.Max(0, contentHeight - _viewportHeight);
    _scrollOffset.x = Mathf.Clamp(_scrollOffset.x, 0, maxX);
    _scrollOffset.y = Mathf.Clamp(_scrollOffset.y, 0, maxY);

    if (_contentContainer != null)
    {
      _contentContainer.layout = new Rect(-_scrollOffset.x, -_scrollOffset.y, contentWidth, contentHeight);
    }

    UpdateScrollerValues();
  }

  private void UpdateScrollerValues()
  {
    float maxX = Mathf.Max(0, contentWidth - _viewportWidth);
    float maxY = Mathf.Max(0, contentHeight - _viewportHeight);

    horizontalScroller.lowValue = 0;
    horizontalScroller.highValue = maxX;
    horizontalScroller.SetValueWithoutNotify(_scrollOffset.x);

    verticalScroller.lowValue = 0;
    verticalScroller.highValue = maxY;
    verticalScroller.SetValueWithoutNotify(_scrollOffset.y);

    bool showH = maxX > 0.01f && horizontalScrollerVisibility != ScrollDirection.Hidden;
    bool showV = maxY > 0.01f && verticalScrollerVisibility != ScrollDirection.Hidden;

    horizontalScroller.visible = showH;
    verticalScroller.visible = showV;

    if (contentWidth > 0.01f)
      horizontalScroller.size = Mathf.Clamp01(_viewportWidth / contentWidth);
    if (contentHeight > 0.01f)
      verticalScroller.size = Mathf.Clamp01(_viewportHeight / contentHeight);
  }

  private void SetScrollOffset(Vector2 value)
  {
    _scrollOffset = value;
    ApplyScrollOffset();
  }

  public void ScrollTo(VisualElement child)
  {
    if (child == null) return;

    float childY = child.layout.yMin;
    float childH = child.layout.height;
    float childX = child.layout.xMin;
    float childW = child.layout.width;

    float targetX = _scrollOffset.x;
    float targetY = _scrollOffset.y;

    if (childX < _scrollOffset.x)
      targetX = childX;
    else if (childX + childW > _scrollOffset.x + _viewportWidth)
      targetX = childX + childW - _viewportWidth;

    if (childY < _scrollOffset.y)
      targetY = childY;
    else if (childY + childH > _scrollOffset.y + _viewportHeight)
      targetY = childY + childH - _viewportHeight;

    scrollOffset = new Vector2(targetX, targetY);
  }

  public void ScrollTo(float x, float y)
  {
    scrollOffset = new Vector2(x, y);
  }

  public void UpdateContentViewTransform()
  {
    ApplyScrollOffset();
  }

  public void ResetScroll()
  {
    scrollOffset = Vector2.zero;
  }

  public void OnScrollWheelChanged(Vector2 delta)
  {
    float x = delta.x * horizontalWheelScrollSize;
    float y = delta.y * mouseWheelScrollSize;
    scrollOffset += new Vector2(x, y);
  }

  public void ApplyElasticRubberBand(float deltaTime)
  {
    if (!elasticityEnabled || scrollMomentum != ScrollMomentum.Elastic) return;

    float maxX = Mathf.Max(0, contentWidth - _viewportWidth);
    float maxY = Mathf.Max(0, contentHeight - _viewportHeight);

    Vector2 targetOffset = _scrollOffset;
    Vector2 speed = _velocity;

    if (_scrollOffset.x < 0) targetOffset.x = 0;
    else if (_scrollOffset.x > maxX) targetOffset.x = maxX;
    if (_scrollOffset.y < 0) targetOffset.y = 0;
    else if (_scrollOffset.y > maxY) targetOffset.y = maxY;

    if (targetOffset != _scrollOffset)
    {
      _scrollOffset.x = Mathf.SmoothDamp(_scrollOffset.x, targetOffset.x, ref speed.x, elasticity, Mathf.Infinity, deltaTime);
      _scrollOffset.y = Mathf.SmoothDamp(_scrollOffset.y, targetOffset.y, ref speed.y, elasticity, Mathf.Infinity, deltaTime);
      _velocity = speed;
      ApplyScrollOffset();
    }
  }

  public void ApplyInertia(float deltaTime)
  {
    if (_isDragging || _velocity.sqrMagnitude < 0.01f) return;

    _velocity *= Mathf.Pow(scrollDecelerationRate, deltaTime);
    if (_velocity.sqrMagnitude < 1f)
      _velocity = Vector2.zero;

    _scrollOffset += _velocity * deltaTime;
    ApplyScrollOffset();
  }

  public void SetContentViewContainer(VisualElement element)
  {
    if (_contentViewport != null && _contentContainer != null)
      _contentViewport.Remove(_contentContainer);
    if (_contentViewport != null && element != null)
      _contentViewport.Add(element);
  }

  public void ScrollTo(VisualElement child, bool animate)
  {
    _ = animate;
    ScrollTo(child);
  }

  public void ScrollToId(string childId)
  {
    var child = _contentContainer?.Q(childId);
    if (child != null) ScrollTo(child);
  }

  public void ScrollToRectangle(Rect rect)
  {
    float targetX = _scrollOffset.x;
    float targetY = _scrollOffset.y;

    if (rect.xMin < _scrollOffset.x)
      targetX = rect.xMin;
    else if (rect.xMax > _scrollOffset.x + _viewportWidth)
      targetX = rect.xMax - _viewportWidth;

    if (rect.yMin < _scrollOffset.y)
      targetY = rect.yMin;
    else if (rect.yMax > _scrollOffset.y + _viewportHeight)
      targetY = rect.yMax - _viewportHeight;

    scrollOffset = new Vector2(targetX, targetY);
  }

  public void OnPointerDown(Vector2 position)
  {
    _isDragging = true;
    _dragStartPosition = position;
    _dragStartScroll = _scrollOffset;
    _velocity = Vector2.zero;
  }

  public void OnPointerMove(Vector2 position, Vector2 delta)
  {
    if (!_isDragging) return;
    Vector2 pointerDelta = position - _dragStartPosition;
    float newX = _dragStartScroll.x - pointerDelta.x;
    float newY = _dragStartScroll.y - pointerDelta.y;
    _velocity = -delta / Time.unscaledDeltaTime;
    scrollOffset = new Vector2(newX, newY);
  }

  public void OnPointerUp()
  {
    _isDragging = false;
  }
}

public enum NestedInteractionKind
{
  Default = 0,
  ForwardScrolling = 1,
  StopScrolling = 2
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

public class Scroller : VisualElement
{
  public static readonly string ussClassName = "unity-scroller";
  public static readonly string horizontalVariantUssClassName = ussClassName + "--horizontal";
  public static readonly string verticalVariantUssClassName = ussClassName + "--vertical";

  private float _value;
  private SliderDirection _direction;

  public float value
  {
    get => _value;
    set
    {
      float clamped = Mathf.Clamp(value, lowValue, highValue);
      if (Math.Abs(_value - clamped) > 0.0001f)
      {
        _value = clamped;
        valueChanged?.Invoke(_value);
      }
    }
  }

  public float lowValue { get; set; }
  public float highValue { get; set; } = 100f;
  public float size { get; set; } = 1f;

  public SliderDirection direction
  {
    get => _direction;
    set
    {
      _direction = value;
      RemoveClass(horizontalVariantUssClassName);
      RemoveClass(verticalVariantUssClassName);
      AddClass(_direction == SliderDirection.Horizontal ? horizontalVariantUssClassName : verticalVariantUssClassName);
    }
  }

  public event Action<float> valueChanged;

  public Scroller()
  {
    AddClass(ussClassName);
  }

  public Scroller(float lowValue, float highValue, Action<float> valueChanged, SliderDirection direction)
  {
    this.lowValue = lowValue;
    this.highValue = highValue;
    this.direction = direction;
    this.valueChanged += valueChanged;
    AddClass(ussClassName);
  }

  public void SetValueWithoutNotify(float newValue)
  {
    _value = Mathf.Clamp(newValue, lowValue, highValue);
  }

  public void Adjust(float factor)
  {
    value += factor * (highValue - lowValue) * 0.1f;
  }

  public void ScrollToPage(int page)
  {
    float pageSize = (highValue - lowValue) * 0.9f;
    value += page * pageSize;
  }
}

public enum SliderDirection
{
  Horizontal = 0,
  Vertical = 1
}
