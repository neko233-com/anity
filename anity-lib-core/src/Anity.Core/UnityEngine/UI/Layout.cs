using System;
using System.Collections.Generic;

namespace UnityEngine.UI;

public static class LayoutRebuilder
{
  public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot)
  {
    _ = layoutRoot;
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
  public void Rebuild(CanvasUpdate executing) { }
  public bool IsDestroyed() => _rect == null;
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

public interface ICanvasElement
{
  void Rebuild(CanvasUpdate executing);
  Transform transform { get; }
  bool IsDestroyed();
}

public class LayoutGroup : UIBehaviour, ILayoutElement, ILayoutGroup, ICanvasElement
{
  private RectOffset? _padding;
  private Vector2 _spacing = Vector2.zero;
  private bool _childControlWidth;
  private bool _childControlHeight;
  private bool _childForceExpandWidth;
  private bool _childForceExpandHeight;
  private bool _childAlignment;
  private bool _reverseArrangement;

  public RectOffset padding
  {
    get => _padding ??= new RectOffset();
    set => _padding = value;
  }

  public Vector2 spacing
  {
    get => _spacing;
    set => _spacing = value;
  }

  public bool childControlWidth
  {
    get => _childControlWidth;
    set => _childControlWidth = value;
  }

  public bool childControlHeight
  {
    get => _childControlHeight;
    set => _childControlHeight = value;
  }

  public bool childForceExpandWidth
  {
    get => _childForceExpandWidth;
    set => _childForceExpandWidth = value;
  }

  public bool childForceExpandHeight
  {
    get => _childForceExpandHeight;
    set => _childForceExpandHeight = value;
  }

  public bool reverseArrangement
  {
    get => _reverseArrangement;
    set => _reverseArrangement = value;
  }

  public TextAnchor childAlignment
  {
    get => _childAlignment ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
    set => _childAlignment = value != TextAnchor.UpperLeft;
  }

  public virtual float minWidth => 0f;
  public virtual float preferredWidth => 0f;
  public virtual float flexibleWidth => -1f;
  public virtual float minHeight => 0f;
  public virtual float preferredHeight => 0f;
  public virtual float flexibleHeight => -1f;
  public virtual int layoutPriority => 0;

  public virtual void CalculateLayoutInputHorizontal()
  {
  }

  public virtual void CalculateLayoutInputVertical()
  {
  }

  public virtual void SetLayoutHorizontal()
  {
  }

  public virtual void SetLayoutVertical()
  {
  }

  public virtual void Rebuild(CanvasUpdate rebuildingLayout)
  {
    if (rebuildingLayout == CanvasUpdate.Layout)
    {
      SetLayoutHorizontal();
      SetLayoutVertical();
    }
  }

  protected override void OnEnable()
  {
    base.OnEnable();
    SetDirty();
  }

  protected override void OnDisable()
  {
    base.OnDisable();
  }

  protected override void OnRectTransformDimensionsChange()
  {
    base.OnRectTransformDimensionsChange();
    SetDirty();
  }

  protected override void OnTransformChildrenChanged()
  {
    base.OnTransformChildrenChanged();
    SetDirty();
  }

  protected void SetDirty()
  {
    LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
  }

  protected override void OnDidApplyAnimationProperties()
  {
    base.OnDidApplyAnimationProperties();
    SetDirty();
  }
}

public class HorizontalLayoutGroup : LayoutGroup
{
  public override void CalculateLayoutInputHorizontal()
  {
    base.CalculateLayoutInputHorizontal();
  }

  public override void SetLayoutHorizontal()
  {
  }

  public override void SetLayoutVertical()
  {
  }
}

public class VerticalLayoutGroup : LayoutGroup
{
  public override void CalculateLayoutInputHorizontal()
  {
    base.CalculateLayoutInputHorizontal();
  }

  public override void SetLayoutHorizontal()
  {
  }

  public override void SetLayoutVertical()
  {
  }
}

public class GridLayoutGroup : LayoutGroup
{
  private Vector2 _cellSize = new(100f, 100f);
  private Vector2 _spacing = Vector2.zero;
  private Corner _startCorner = Corner.UpperLeft;
  private Axis _startAxis = Axis.Horizontal;
  private Constraint _constraint = Constraint.Flexible;

  public Vector2 cellSize
  {
    get => _cellSize;
    set => _cellSize = value;
  }

  public new Vector2 spacing
  {
    get => _spacing;
    set => _spacing = value;
  }

  public Corner startCorner
  {
    get => _startCorner;
    set => _startCorner = value;
  }

  public Axis startAxis
  {
    get => _startAxis;
    set => _startAxis = value;
  }

  public Constraint constraint
  {
    get => _constraint;
    set => _constraint = value;
  }

  public override void CalculateLayoutInputHorizontal()
  {
    base.CalculateLayoutInputHorizontal();
  }

  public override void SetLayoutHorizontal()
  {
  }

  public override void SetLayoutVertical()
  {
  }
}

public enum Corner
{
  UpperLeft = 0,
  UpperRight = 1,
  LowerLeft = 2,
  LowerRight = 3
}

public enum Constraint
{
  Flexible = 0,
  FixedColumnCount = 1,
  FixedRowCount = 2
}

public class LayoutElement : UIBehaviour, ILayoutElement
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
    LayoutRebuilder.MarkLayoutForRebuild(GetComponent<RectTransform>());
  }
}

public class LayoutElement2D : LayoutElement
{
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
    set
    {
      _aspectMode = value;
      SetDirty();
    }
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

  public void SetLayoutHorizontal()
  {
  }

  public void SetLayoutVertical()
  {
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
    LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
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
    set
    {
      _horizontalFit = value;
      SetDirty();
    }
  }

  public FitMode verticalFit
  {
    get => _verticalFit;
    set
    {
      _verticalFit = value;
      SetDirty();
    }
  }

  public void SetLayoutHorizontal()
  {
  }

  public void SetLayoutVertical()
  {
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
    LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
  }
}

public class LayoutPosition : LayoutElement
{
}

public class VerticalLayoutGroup2 : LayoutGroup
{
}

public class HorizontalLayoutGroup2 : LayoutGroup
{
}

public class GridLayoutGroup2 : LayoutGroup
{
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
