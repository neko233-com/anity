using System;
using System.Collections.Generic;

namespace UnityEngine.UI;

public static class LayoutRebuilder
{
  public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot)
  {
    if (layoutRoot == null) return;
    var controllers = new List<ILayoutController>();
    GetLayoutControllers(layoutRoot, controllers);

    for (var i = 0; i < controllers.Count; i++)
    {
      if (controllers[i] is ILayoutElement element)
        element.CalculateLayoutInputHorizontal();
    }
    for (var i = 0; i < controllers.Count; i++)
    {
      controllers[i].SetLayoutHorizontal();
    }

    for (var i = 0; i < controllers.Count; i++)
    {
      if (controllers[i] is ILayoutElement element)
        element.CalculateLayoutInputVertical();
    }
    for (var i = 0; i < controllers.Count; i++)
    {
      controllers[i].SetLayoutVertical();
    }
  }

  private static void GetLayoutControllers(RectTransform rect, List<ILayoutController> controllers)
  {
    var components = rect.GetComponents<Component>();
    for (var i = 0; i < components.Length; i++)
    {
      if (components[i] is ILayoutController controller)
        controllers.Add(controller);
    }

    for (var i = 0; i < rect.childCount; i++)
    {
      var child = rect.GetChild(i) as RectTransform;
      if (child != null)
        GetLayoutControllers(child, controllers);
    }
  }

  public static void MarkLayoutForRebuild(RectTransform rect)
  {
    if (rect is null) return;
    CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(new LayoutRebuildProxy(rect));
  }

  public static bool IsRebuildingLayout()
  {
    return CanvasUpdateRegistry.instance.IsRebuildingLayout();
  }
}

internal class LayoutRebuildProxy : ICanvasElement
{
  private readonly RectTransform _rect;
  public LayoutRebuildProxy(RectTransform rect) => _rect = rect;
  public Transform transform => _rect;
  public void Rebuild(CanvasUpdate executing)
  {
    if (executing == CanvasUpdate.Layout)
      LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
  }
  public void LayoutComplete() { }
  public void GraphicUpdateComplete() { }
  public bool IsDestroyed() => _rect == null;
}

public class LayoutGroup : UIBehaviour, ILayoutElement, ILayoutGroup, ICanvasElement
{
  private RectOffset? _padding;
  private TextAnchor _childAlignment = TextAnchor.UpperLeft;
  private bool _childControlWidth;
  private bool _childControlHeight;
  private bool _childForceExpandWidth;
  private bool _childForceExpandHeight;
  private bool _reverseArrangement;
  private Vector2 _spacing;

  protected float _minWidth;
  protected float _preferredWidth;
  protected float _flexibleWidth;
  protected float _minHeight;
  protected float _preferredHeight;
  protected float _flexibleHeight;

  public RectOffset padding
  {
    get => _padding ??= new RectOffset();
    set => _padding = value;
  }

  public Vector2 spacing
  {
    get => _spacing;
    set { _spacing = value; SetDirty(); }
  }

  public bool childControlWidth
  {
    get => _childControlWidth;
    set { _childControlWidth = value; SetDirty(); }
  }

  public bool childControlHeight
  {
    get => _childControlHeight;
    set { _childControlHeight = value; SetDirty(); }
  }

  public bool childForceExpandWidth
  {
    get => _childForceExpandWidth;
    set { _childForceExpandWidth = value; SetDirty(); }
  }

  public bool childForceExpandHeight
  {
    get => _childForceExpandHeight;
    set { _childForceExpandHeight = value; SetDirty(); }
  }

  public bool reverseArrangement
  {
    get => _reverseArrangement;
    set { _reverseArrangement = value; SetDirty(); }
  }

  public TextAnchor childAlignment
  {
    get => _childAlignment;
    set { _childAlignment = value; SetDirty(); }
  }

  public virtual float minWidth => _minWidth;
  public virtual float preferredWidth => _preferredWidth;
  public virtual float flexibleWidth => _flexibleWidth;
  public virtual float minHeight => _minHeight;
  public virtual float preferredHeight => _preferredHeight;
  public virtual float flexibleHeight => _flexibleHeight;
  public virtual int layoutPriority => 0;

  protected List<RectTransform> rectChildren { get; private set; } = new();

  public virtual void CalculateLayoutInputHorizontal()
  {
    rectChildren.Clear();
    for (var i = 0; i < transform.childCount; i++)
    {
      var child = transform.GetChild(i);
      if (child is RectTransform rect)
      {
        var ignoreLayout = false;
        var layoutElement = rect.GetComponent<LayoutElement>();
        if (layoutElement != null && layoutElement.ignoreLayout)
          ignoreLayout = true;
        if (!ignoreLayout)
          rectChildren.Add(rect);
      }
    }

    _minWidth = padding.horizontal;
    _preferredWidth = padding.horizontal;
    _flexibleWidth = 0f;
  }

  public virtual void CalculateLayoutInputVertical()
  {
    _minHeight = padding.vertical;
    _preferredHeight = padding.vertical;
    _flexibleHeight = 0f;
  }

  public virtual void SetLayoutHorizontal() { }
  public virtual void SetLayoutVertical() { }

  public virtual void Rebuild(CanvasUpdate rebuildingLayout)
  {
    if (rebuildingLayout == CanvasUpdate.Layout)
    {
      if (!IsActive()) return;
      CalculateLayoutInputHorizontal();
      CalculateLayoutInputVertical();
      SetLayoutHorizontal();
      SetLayoutVertical();
    }
  }

  public virtual void LayoutComplete() { }
  public virtual void GraphicUpdateComplete() { }
  public virtual bool IsDestroyed() => this == null;

  protected override void OnEnable()
  {
    base.OnEnable();
    SetDirty();
  }

  protected override void OnDisable()
  {
    base.OnDisable();
    LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
  }

  protected override void OnRectTransformDimensionsChange()
  {
    base.OnRectTransformDimensionsChange();
    if (IsActive()) SetDirty();
  }

  protected override void OnTransformChildrenChanged()
  {
    base.OnTransformChildrenChanged();
    SetDirty();
  }

  protected void SetDirty()
  {
    if (!IsActive()) return;
    LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
  }

  protected override void OnDidApplyAnimationProperties()
  {
    base.OnDidApplyAnimationProperties();
    SetDirty();
  }

  protected void SetChildAlongAxis(RectTransform rect, int axis, float pos)
  {
    SetChildAlongAxis(rect, axis, pos, axis == 0 ? rect.sizeDelta.x : rect.sizeDelta.y);
  }

  protected void SetChildAlongAxis(RectTransform rect, int axis, float pos, float size)
  {
    if (rect == null) return;

    var rt = rect;
    var pivot = rt.pivot;

    if (axis == 0)
    {
      var anchoredPosition = rt.anchoredPosition;
      anchoredPosition.x = pos + size * pivot.x;
      rt.anchoredPosition = anchoredPosition;
      var sizeDelta = rt.sizeDelta;
      sizeDelta.x = size;
      rt.sizeDelta = sizeDelta;
    }
    else
    {
      var anchoredPosition = rt.anchoredPosition;
      anchoredPosition.y = pos + size * pivot.y;
      rt.anchoredPosition = anchoredPosition;
      var sizeDelta = rt.sizeDelta;
      sizeDelta.y = size;
      rt.sizeDelta = sizeDelta;
    }
  }

  protected float GetStartOffset(int axis, float requiredSpaceWithoutPadding)
  {
    var requiredSpace = requiredSpaceWithoutPadding + (axis == 0 ? padding.horizontal : padding.vertical);
    var availableSpace = axis == 0 ? (transform as RectTransform)?.rect.width ?? 0f : (transform as RectTransform)?.rect.height ?? 0f;
    var surplusSpace = availableSpace - requiredSpace;
    float alignmentOnAxis;

    if (axis == 0)
    {
      alignmentOnAxis = ((int)childAlignment % 3) * 0.5f;
    }
    else
    {
      alignmentOnAxis = ((int)childAlignment / 3) * 0.5f;
    }

    return (axis == 0 ? padding.left : padding.top) + surplusSpace * alignmentOnAxis;
  }

  protected float GetTotalFlexibleSize(int axis)
  {
    float totalFlexible = 0f;
    for (var i = 0; i < rectChildren.Count; i++)
    {
      var child = rectChildren[i];
      var layoutElement = child.GetComponent<ILayoutElement>();
      if (layoutElement != null)
      {
        totalFlexible += Mathf.Max(0f, axis == 0 ? layoutElement.flexibleWidth : layoutElement.flexibleHeight);
      }
    }
    return totalFlexible;
  }

  protected float GetTotalMinSize(int axis)
  {
    float totalMin = 0f;
    for (var i = 0; i < rectChildren.Count; i++)
    {
      var child = rectChildren[i];
      var layoutElement = child.GetComponent<ILayoutElement>();
      if (layoutElement != null)
      {
        totalMin += axis == 0 ? layoutElement.minWidth : layoutElement.minHeight;
      }
    }
    return totalMin;
  }

  protected float GetTotalPreferredSize(int axis)
  {
    float totalPreferred = 0f;
    for (var i = 0; i < rectChildren.Count; i++)
    {
      var child = rectChildren[i];
      var layoutElement = child.GetComponent<ILayoutElement>();
      if (layoutElement != null)
      {
        totalPreferred += axis == 0 ? layoutElement.preferredWidth : layoutElement.preferredHeight;
      }
    }
    return totalPreferred;
  }
}

public class HorizontalLayoutGroup : LayoutGroup
{
  public override void CalculateLayoutInputHorizontal()
  {
    base.CalculateLayoutInputHorizontal();

    var totalMin = padding.horizontal;
    var totalPreferred = padding.horizontal;
    var totalFlexible = 0f;
    var spacing = this.spacing.x;

    for (var i = 0; i < rectChildren.Count; i++)
    {
      if (i > 0)
      {
        totalMin += spacing;
        totalPreferred += spacing;
      }

      var child = rectChildren[i];
      var layoutElement = child.GetComponent<ILayoutElement>();
      if (layoutElement != null)
      {
        totalMin += layoutElement.minWidth;
        totalPreferred += layoutElement.preferredWidth;
        totalFlexible += Mathf.Max(0f, layoutElement.flexibleWidth);
      }
    }

    if (childForceExpandWidth)
      totalFlexible = Mathf.Max(totalFlexible, 1f);

    SetLayoutInputForAxis(totalMin, totalPreferred, totalFlexible, 0);
  }

  public override void CalculateLayoutInputVertical()
  {
    base.CalculateLayoutInputVertical();

    var totalMin = padding.vertical;
    var totalPreferred = padding.vertical;
    var totalFlexible = 0f;

    for (var i = 0; i < rectChildren.Count; i++)
    {
      var child = rectChildren[i];
      var layoutElement = child.GetComponent<ILayoutElement>();
      if (layoutElement != null)
      {
        totalMin = Mathf.Max(totalMin, padding.vertical + layoutElement.minHeight);
        totalPreferred = Mathf.Max(totalPreferred, padding.vertical + layoutElement.preferredHeight);
        totalFlexible = Mathf.Max(totalFlexible, layoutElement.flexibleHeight);
      }
    }

    if (childForceExpandHeight)
      totalFlexible = Mathf.Max(totalFlexible, 1f);

    SetLayoutInputForAxis(totalMin, totalPreferred, totalFlexible, 1);
  }

  private void SetLayoutInputForAxis(float totalMin, float totalPreferred, float totalFlexible, int axis)
  {
    if (axis == 0)
    {
      _minWidth = totalMin;
      _preferredWidth = totalPreferred;
      _flexibleWidth = totalFlexible;
    }
    else
    {
      _minHeight = totalMin;
      _preferredHeight = totalPreferred;
      _flexibleHeight = totalFlexible;
    }
  }

  public override void SetLayoutHorizontal()
  {
    SetChildrenAlongAxis(0);
  }

  public override void SetLayoutVertical()
  {
    SetChildrenAlongAxis(1);
  }

  private void SetChildrenAlongAxis(int axis)
  {
    var size = rectTransform.rect;
    var pos = GetStartOffset(axis, axis == 0 ? GetTotalPreferredSize(0) - padding.horizontal : GetTotalPreferredSize(1) - padding.vertical);
    var surplusSpace = (axis == 0 ? size.width : size.height) - (axis == 0 ? preferredWidth : preferredHeight);
    var flexibleCount = GetTotalFlexibleSize(axis);

    if (reverseArrangement)
      pos = (axis == 0 ? size.width - padding.right : size.height - padding.top);

    for (var i = 0; i < rectChildren.Count; i++)
    {
      var child = rectChildren[i];
      var index = reverseArrangement ? rectChildren.Count - 1 - i : i;
      child = rectChildren[index];

      float min, preferred, flexible;
      var layoutElement = child.GetComponent<ILayoutElement>();
      if (layoutElement != null)
      {
        if (axis == 0)
        {
          min = layoutElement.minWidth;
          preferred = layoutElement.preferredWidth;
          flexible = Mathf.Max(0f, layoutElement.flexibleWidth);
        }
        else
        {
          min = layoutElement.minHeight;
          preferred = layoutElement.preferredHeight;
          flexible = Mathf.Max(0f, layoutElement.flexibleHeight);
        }
      }
      else
      {
        min = preferred = axis == 0 ? child.sizeDelta.x : child.sizeDelta.y;
        flexible = 0f;
      }

      var childSize = Mathf.Clamp(preferred + surplusSpace * (flexibleCount > 0f ? flexible / flexibleCount : 0f), min, flexible > 0f ? float.PositiveInfinity : preferred);

      if (axis == 1)
      {
        childSize = size.height - padding.vertical;
        var offset = GetStartOffset(1, childSize);
        SetChildAlongAxis(child, 1, offset, childSize);
        continue;
      }

      if (reverseArrangement)
      {
        pos -= childSize;
        SetChildAlongAxis(child, axis, pos, childSize);
        pos -= spacing.x;
      }
      else
      {
        SetChildAlongAxis(child, axis, pos, childSize);
        pos += childSize + spacing.x;
      }
    }
  }

  private RectTransform rectTransform => transform as RectTransform;
}

public class VerticalLayoutGroup : LayoutGroup
{
  public override void CalculateLayoutInputHorizontal()
  {
    base.CalculateLayoutInputHorizontal();

    var totalMin = padding.horizontal;
    var totalPreferred = padding.horizontal;
    var totalFlexible = 0f;

    for (var i = 0; i < rectChildren.Count; i++)
    {
      var child = rectChildren[i];
      var layoutElement = child.GetComponent<ILayoutElement>();
      if (layoutElement != null)
      {
        totalMin = Mathf.Max(totalMin, padding.horizontal + layoutElement.minWidth);
        totalPreferred = Mathf.Max(totalPreferred, padding.horizontal + layoutElement.preferredWidth);
        totalFlexible = Mathf.Max(totalFlexible, layoutElement.flexibleWidth);
      }
    }

    if (childForceExpandWidth)
      totalFlexible = Mathf.Max(totalFlexible, 1f);

    SetLayoutInputForAxis(totalMin, totalPreferred, totalFlexible, 0);
  }

  public override void CalculateLayoutInputVertical()
  {
    base.CalculateLayoutInputVertical();

    var totalMin = padding.vertical;
    var totalPreferred = padding.vertical;
    var totalFlexible = 0f;
    var spacing = this.spacing.y;

    for (var i = 0; i < rectChildren.Count; i++)
    {
      if (i > 0)
      {
        totalMin += spacing;
        totalPreferred += spacing;
      }

      var child = rectChildren[i];
      var layoutElement = child.GetComponent<ILayoutElement>();
      if (layoutElement != null)
      {
        totalMin += layoutElement.minHeight;
        totalPreferred += layoutElement.preferredHeight;
        totalFlexible += Mathf.Max(0f, layoutElement.flexibleHeight);
      }
    }

    if (childForceExpandHeight)
      totalFlexible = Mathf.Max(totalFlexible, 1f);

    SetLayoutInputForAxis(totalMin, totalPreferred, totalFlexible, 1);
  }

  private void SetLayoutInputForAxis(float totalMin, float totalPreferred, float totalFlexible, int axis)
  {
    if (axis == 0)
    {
      _minWidth = totalMin;
      _preferredWidth = totalPreferred;
      _flexibleWidth = totalFlexible;
    }
    else
    {
      _minHeight = totalMin;
      _preferredHeight = totalPreferred;
      _flexibleHeight = totalFlexible;
    }
  }

  public override void SetLayoutHorizontal()
  {
    SetChildrenAlongAxis(0);
  }

  public override void SetLayoutVertical()
  {
    SetChildrenAlongAxis(1);
  }

  private void SetChildrenAlongAxis(int axis)
  {
    var size = rectTransform.rect;
    var pos = GetStartOffset(axis, axis == 1 ? GetTotalPreferredSize(1) - padding.vertical : GetTotalPreferredSize(0) - padding.horizontal);
    var surplusSpace = (axis == 1 ? size.height : size.width) - (axis == 1 ? preferredHeight : preferredWidth);
    var flexibleCount = GetTotalFlexibleSize(axis);

    if (reverseArrangement)
      pos = axis == 1 ? size.height - padding.top : size.width - padding.right;

    for (var i = 0; i < rectChildren.Count; i++)
    {
      var child = rectChildren[i];
      var index = reverseArrangement ? rectChildren.Count - 1 - i : i;
      child = rectChildren[index];

      float min, preferred, flexible;
      var layoutElement = child.GetComponent<ILayoutElement>();
      if (layoutElement != null)
      {
        if (axis == 1)
        {
          min = layoutElement.minHeight;
          preferred = layoutElement.preferredHeight;
          flexible = Mathf.Max(0f, layoutElement.flexibleHeight);
        }
        else
        {
          min = layoutElement.minWidth;
          preferred = layoutElement.preferredWidth;
          flexible = Mathf.Max(0f, layoutElement.flexibleWidth);
        }
      }
      else
      {
        min = preferred = axis == 1 ? child.sizeDelta.y : child.sizeDelta.x;
        flexible = 0f;
      }

      var childSize = Mathf.Clamp(preferred + surplusSpace * (flexibleCount > 0f ? flexible / flexibleCount : 0f), min, flexible > 0f ? float.PositiveInfinity : preferred);

      if (axis == 0)
      {
        childSize = size.width - padding.horizontal;
        var offset = GetStartOffset(0, childSize);
        SetChildAlongAxis(child, 0, offset, childSize);
        continue;
      }

      if (reverseArrangement)
      {
        pos -= childSize;
        SetChildAlongAxis(child, axis, pos, childSize);
        pos -= spacing.y;
      }
      else
      {
        SetChildAlongAxis(child, axis, pos, childSize);
        pos += childSize + spacing.y;
      }
    }
  }

  private RectTransform rectTransform => transform as RectTransform;
}

public class GridLayoutGroup : LayoutGroup
{
  public enum Corner
  {
    UpperLeft = 0,
    UpperRight = 1,
    LowerLeft = 2,
    LowerRight = 3
  }

  public enum GridAxis
  {
    Horizontal = 0,
    Vertical = 1
  }

  public enum Constraint
  {
    Flexible = 0,
    FixedColumnCount = 1,
    FixedRowCount = 2
  }

  private Vector2 _cellSize = new(100f, 100f);
  private Vector2 _gridSpacing = Vector2.zero;
  private Corner _startCorner = Corner.UpperLeft;
  private GridAxis _startAxis = GridAxis.Horizontal;
  private Constraint _constraint = Constraint.Flexible;
  private int _constraintCount;

  private int _cellCountX;
  private int _cellCountY;
  private float _spacingX;
  private float _spacingY;
  private float _cornerX;
  private float _cornerY;

  public Vector2 cellSize
  {
    get => _cellSize;
    set { _cellSize = value; SetDirty(); }
  }

  public new Vector2 spacing
  {
    get => _gridSpacing;
    set { _gridSpacing = value; SetDirty(); }
  }

  public Corner startCorner
  {
    get => _startCorner;
    set { _startCorner = value; SetDirty(); }
  }

  public GridAxis startAxis
  {
    get => _startAxis;
    set { _startAxis = value; SetDirty(); }
  }

  public Constraint constraint
  {
    get => _constraint;
    set { _constraint = value; SetDirty(); }
  }

  public int constraintCount
  {
    get => _constraintCount;
    set { _constraintCount = Mathf.Max(1, value); SetDirty(); }
  }

  public override void CalculateLayoutInputHorizontal()
  {
    base.CalculateLayoutInputHorizontal();

    int count = rectChildren.Count;
    if (count == 0)
    {
      _cellCountX = _cellCountY = 0;
    }
    else
    {
      float width = (transform as RectTransform).rect.width - padding.horizontal;
      float height = (transform as RectTransform).rect.height - padding.vertical;
      if (_constraint == Constraint.FixedColumnCount)
      {
        _cellCountX = _constraintCount;
        _cellCountY = Mathf.CeilToInt(count / (float)_cellCountX);
      }
      else if (_constraint == Constraint.FixedRowCount)
      {
        _cellCountY = _constraintCount;
        _cellCountX = Mathf.CeilToInt(count / (float)_cellCountY);
      }
      else
      {
        if (_startAxis == GridAxis.Horizontal)
        {
          float fitX = _cellSize.x + _gridSpacing.x;
          _cellCountX = Mathf.Max(1, Mathf.FloorToInt((width + _gridSpacing.x) / fitX));
          _cellCountX = Mathf.Clamp(_cellCountX, 1, count);
          _cellCountY = Mathf.CeilToInt(count / (float)_cellCountX);
        }
        else
        {
          float fitY = _cellSize.y + _gridSpacing.y;
          _cellCountY = Mathf.Max(1, Mathf.FloorToInt((height + _gridSpacing.y) / fitY));
          _cellCountY = Mathf.Clamp(_cellCountY, 1, count);
          _cellCountX = Mathf.CeilToInt(count / (float)_cellCountY);
        }
      }
    }

    float minPreferredX = padding.horizontal + _cellSize.x * _cellCountX + _gridSpacing.x * Mathf.Max(0, _cellCountX - 1);
    float minPreferredY = padding.vertical + _cellSize.y * _cellCountY + _gridSpacing.y * Mathf.Max(0, _cellCountY - 1);

    SetLayoutInputForAxis(minPreferredX, minPreferredX, -1f, 0);
    SetLayoutInputForAxis(minPreferredY, minPreferredY, -1f, 1);
  }

  public override void CalculateLayoutInputVertical()
  {
  }

  private void SetLayoutInputForAxis(float totalMin, float totalPreferred, float totalFlexible, int axis)
  {
    if (axis == 0)
    {
      _minWidth = totalMin;
      _preferredWidth = totalPreferred;
      _flexibleWidth = totalFlexible;
    }
    else
    {
      _minHeight = totalMin;
      _preferredHeight = totalPreferred;
      _flexibleHeight = totalFlexible;
    }
  }

  public override void SetLayoutHorizontal()
  {
    SetCells();
    SetChildrenAlongAxis(0);
  }

  public override void SetLayoutVertical()
  {
    SetCells();
    SetChildrenAlongAxis(1);
  }

  private void SetCells()
  {
    var rect = rectTransform.rect;
    _spacingX = _gridSpacing.x;
    _spacingY = _gridSpacing.y;

    var requiredWidth = _cellSize.x * _cellCountX + _spacingX * Mathf.Max(0, _cellCountX - 1);
    var requiredHeight = _cellSize.y * _cellCountY + _spacingY * Mathf.Max(0, _cellCountY - 1);

    var startOffsetX = GetStartOffset(0, requiredWidth);
    var startOffsetY = GetStartOffset(1, requiredHeight);

    _cornerX = startOffsetX;
    _cornerY = startOffsetY;

    if (((int)_startCorner & 1) != 0)
      _cornerX = rect.width - padding.right - _cellSize.x;
    if (((int)_startCorner & 2) != 0)
      _cornerY = rect.height - padding.top - _cellSize.y;
  }

  private RectTransform rectTransform => transform as RectTransform;

  private void SetChildrenAlongAxis(int axis)
  {
    for (var i = 0; i < rectChildren.Count; i++)
    {
      var child = rectChildren[i];

      int xIndex;
      int yIndex;

      if (_startAxis == GridAxis.Horizontal)
      {
        xIndex = i % _cellCountX;
        yIndex = i / _cellCountX;
      }
      else
      {
        xIndex = i / _cellCountY;
        yIndex = i % _cellCountY;
      }

      if (((int)_startCorner & 1) != 0)
        xIndex = _cellCountX - 1 - xIndex;
      if (((int)_startCorner & 2) != 0)
        yIndex = _cellCountY - 1 - yIndex;

      var xPos = _cornerX + (_cellSize.x + _spacingX) * xIndex;
      var yPos = _cornerY + (_cellSize.y + _spacingY) * yIndex;

      if (axis == 0)
        SetChildAlongAxis(child, 0, xPos, _cellSize.x);
      else
        SetChildAlongAxis(child, 1, yPos, _cellSize.y);
    }
  }
}

public class ContentSizeFitter : UIBehaviour, ILayoutSelfController
{
  public enum FitMode
  {
    Unconstrained = 0,
    MinSize = 1,
    PreferredSize = 2
  }

  private FitMode _horizontalFit = FitMode.Unconstrained;
  private FitMode _verticalFit = FitMode.Unconstrained;

  public FitMode horizontalFit
  {
    get => _horizontalFit;
    set { _horizontalFit = value; SetDirty(); }
  }

  public FitMode verticalFit
  {
    get => _verticalFit;
    set { _verticalFit = value; SetDirty(); }
  }

  private RectTransform rectTransform => transform as RectTransform;

  public virtual void SetLayoutHorizontal()
  {
    if (rectTransform == null) return;
    HandleSelfFittingAlongAxis(0);
  }

  public virtual void SetLayoutVertical()
  {
    if (rectTransform == null) return;
    HandleSelfFittingAlongAxis(1);
  }

  private void HandleSelfFittingAlongAxis(int axis)
  {
    var fitting = axis == 0 ? horizontalFit : verticalFit;
    if (fitting == FitMode.Unconstrained) return;

    var layoutElement = GetComponent<ILayoutElement>();
    if (layoutElement == null) return;

    float size;
    if (fitting == FitMode.MinSize)
      size = axis == 0 ? LayoutUtility.GetMinWidth(layoutElement) : LayoutUtility.GetMinHeight(layoutElement);
    else
      size = axis == 0 ? LayoutUtility.GetPreferredWidth(layoutElement) : LayoutUtility.GetPreferredHeight(layoutElement);

    rectTransform.SetSizeWithCurrentAnchors((Axis)axis, size);
  }

  protected override void OnEnable()
  {
    base.OnEnable();
    SetDirty();
  }

  protected override void OnRectTransformDimensionsChange()
  {
    base.OnRectTransformDimensionsChange();
    SetDirty();
  }

  private void SetDirty()
  {
    if (!IsActive()) return;
    LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
  }
}

public static class LayoutUtility
{
  public static float GetMinWidth(ILayoutElement layoutElement)
  {
    return layoutElement != null ? layoutElement.minWidth : 0f;
  }

  public static float GetMinHeight(ILayoutElement layoutElement)
  {
    return layoutElement != null ? layoutElement.minHeight : 0f;
  }

  public static float GetPreferredWidth(ILayoutElement layoutElement)
  {
    return layoutElement != null ? Mathf.Max(layoutElement.minWidth, layoutElement.preferredWidth) : 0f;
  }

  public static float GetPreferredHeight(ILayoutElement layoutElement)
  {
    return layoutElement != null ? Mathf.Max(layoutElement.minHeight, layoutElement.preferredHeight) : 0f;
  }

  public static float GetFlexibleWidth(ILayoutElement layoutElement)
  {
    return layoutElement != null ? layoutElement.flexibleWidth : -1f;
  }

  public static float GetFlexibleHeight(ILayoutElement layoutElement)
  {
    return layoutElement != null ? layoutElement.flexibleHeight : -1f;
  }
}

public class AspectRatioFitter : UIBehaviour, ILayoutSelfController
{
  public enum Mode
  {
    None = 0,
    WidthControlsHeight = 1,
    HeightControlsWidth = 2,
    FitInParent = 3,
    EnvelopeParent = 4
  }

  private Mode _aspectMode = Mode.None;
  private float _aspectRatio = 1f;

  public Mode aspectMode
  {
    get => _aspectMode;
    set { _aspectMode = value; SetDirty(); }
  }

  public float aspectRatio
  {
    get => _aspectRatio;
    set
    {
      if (_aspectRatio != value)
      {
        _aspectRatio = value;
        SetDirty();
      }
    }
  }

  private RectTransform rectTransform => transform as RectTransform;
  private RectTransform? parent => transform.parent as RectTransform;

  public void SetLayoutHorizontal()
  {
    UpdateRect();
  }

  public void SetLayoutVertical()
  {
    UpdateRect();
  }

  private void UpdateRect()
  {
    if (rectTransform == null || _aspectMode == Mode.None) return;

    var parentRect = parent != null ? parent.rect : new Rect(0, 0, Screen.width, Screen.height);
    var sizeDelta = rectTransform.sizeDelta;
    var anchorMin = rectTransform.anchorMin;
    var anchorMax = rectTransform.anchorMax;

    var anchorWidth = parentRect.width * (anchorMax.x - anchorMin.x);
    var anchorHeight = parentRect.height * (anchorMax.y - anchorMin.y);

    var width = anchorWidth + sizeDelta.x;
    var height = anchorHeight + sizeDelta.y;

    switch (_aspectMode)
    {
      case Mode.WidthControlsHeight:
        height = width / _aspectRatio;
        break;
      case Mode.HeightControlsWidth:
        width = height * _aspectRatio;
        break;
      case Mode.FitInParent:
        if (width == 0f) width = 1f;
        var ratio = width / height;
        if (ratio > _aspectRatio)
        {
          width = height * _aspectRatio;
        }
        else
        {
          height = width / _aspectRatio;
        }
        break;
      case Mode.EnvelopeParent:
        if (width == 0f) width = 1f;
        var ratio2 = width / height;
        if (ratio2 < _aspectRatio)
        {
          width = height * _aspectRatio;
        }
        else
        {
          height = width / _aspectRatio;
        }
        break;
    }

    sizeDelta.x = width - anchorWidth;
    sizeDelta.y = height - anchorHeight;
    rectTransform.sizeDelta = sizeDelta;
  }

  protected override void OnEnable()
  {
    base.OnEnable();
    SetDirty();
  }

  protected override void OnRectTransformDimensionsChange()
  {
    base.OnRectTransformDimensionsChange();
    SetDirty();
  }

  protected override void OnTransformParentChanged()
  {
    base.OnTransformParentChanged();
    SetDirty();
  }

  private void SetDirty()
  {
    if (!IsActive()) return;
    LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
  }
}

public class LayoutElement : UIBehaviour, ILayoutElement, ILayoutIgnorer
{
  private float _minWidth = -1f;
  private float _minHeight = -1f;
  private float _preferredWidth = -1f;
  private float _preferredHeight = -1f;
  private float _flexibleWidth = -1f;
  private float _flexibleHeight = -1f;
  private int _layoutPriority = 0;
  private bool _ignoreLayout;

  public virtual float minWidth
  {
    get => _minWidth;
    set
    {
      if (_minWidth != value)
      {
        _minWidth = value;
        SetDirty();
      }
    }
  }

  public virtual float minHeight
  {
    get => _minHeight;
    set
    {
      if (_minHeight != value)
      {
        _minHeight = value;
        SetDirty();
      }
    }
  }

  public virtual float preferredWidth
  {
    get => _preferredWidth;
    set
    {
      if (_preferredWidth != value)
      {
        _preferredWidth = value;
        SetDirty();
      }
    }
  }

  public virtual float preferredHeight
  {
    get => _preferredHeight;
    set
    {
      if (_preferredHeight != value)
      {
        _preferredHeight = value;
        SetDirty();
      }
    }
  }

  public virtual float flexibleWidth
  {
    get => _flexibleWidth;
    set
    {
      if (_flexibleWidth != value)
      {
        _flexibleWidth = value;
        SetDirty();
      }
    }
  }

  public virtual float flexibleHeight
  {
    get => _flexibleHeight;
    set
    {
      if (_flexibleHeight != value)
      {
        _flexibleHeight = value;
        SetDirty();
      }
    }
  }

  public virtual int layoutPriority
  {
    get => _layoutPriority;
    set
    {
      if (_layoutPriority != value)
      {
        _layoutPriority = value;
        SetDirty();
      }
    }
  }

  public bool ignoreLayout
  {
    get => _ignoreLayout;
    set
    {
      if (_ignoreLayout != value)
      {
        _ignoreLayout = value;
        LayoutRebuilder.MarkLayoutForRebuild(GetComponent<RectTransform>());
      }
    }
  }

  public virtual void CalculateLayoutInputHorizontal()
  {
  }

  public virtual void CalculateLayoutInputVertical()
  {
  }

  protected void SetDirty()
  {
    if (!IsActive()) return;
    LayoutRebuilder.MarkLayoutForRebuild(GetComponent<RectTransform>());
  }
}

public class CanvasUpdateRegistry
{
  private static CanvasUpdateRegistry? _instance;
  public static CanvasUpdateRegistry instance => _instance ??= new CanvasUpdateRegistry();

  private readonly IndexedSet<ICanvasElement> _layoutRebuildQueue = new();
  private readonly IndexedSet<ICanvasElement> _graphicRebuildQueue = new();
  private bool _performingLayout;
  private bool _performingUpdate;

  public bool IsRebuildingLayout() => _performingLayout;
  public bool IsUpdating() => _performingUpdate;

  public static void RegisterCanvasElementForLayoutRebuild(ICanvasElement element)
  {
    instance._layoutRebuildQueue.Add(element);
  }

  public static void RegisterCanvasElementForGraphicRebuild(ICanvasElement element)
  {
    instance._graphicRebuildQueue.Add(element);
  }

  public static void UnRegisterCanvasElementForRebuild(ICanvasElement element)
  {
    instance._layoutRebuildQueue.Remove(element);
    instance._graphicRebuildQueue.Remove(element);
  }

  internal void PerformUpdate()
  {
    _performingLayout = true;
    for (var i = 0; i < _layoutRebuildQueue.Count; i++)
    {
      var elem = _layoutRebuildQueue[i];
      if (elem is null || elem.IsDestroyed()) continue;
      try { elem.Rebuild(CanvasUpdate.Layout); } catch { }
    }
    _layoutRebuildQueue.Clear();
    _performingLayout = false;

    _performingUpdate = true;
    for (var i = 0; i < _graphicRebuildQueue.Count; i++)
    {
      var elem = _graphicRebuildQueue[i];
      if (elem is null || elem.IsDestroyed()) continue;
      try { elem.Rebuild(CanvasUpdate.PreRender); } catch { }
    }
    _graphicRebuildQueue.Clear();
    _performingUpdate = false;
  }
}

internal class IndexedSet<T> where T : class
{
    private readonly List<T> _list = new();
    private readonly Dictionary<T, int> _index = new();

    public int Count => _list.Count;
    public T this[int i] => _list[i];

    public bool Add(T item)
    {
        if (item is null || _index.ContainsKey(item)) return false;
        _index[item] = _list.Count;
        _list.Add(item);
        return true;
    }

    public bool Remove(T item)
    {
        if (item is null || !_index.TryGetValue(item, out var idx)) return false;
        _index.Remove(item);
        var lastIdx = _list.Count - 1;
        if (idx != lastIdx)
        {
            var last = _list[lastIdx];
            _list[idx] = last;
            _index[last] = idx;
        }
        _list.RemoveAt(lastIdx);
        return true;
    }

    public void Clear()
    {
        _list.Clear();
        _index.Clear();
    }
}
