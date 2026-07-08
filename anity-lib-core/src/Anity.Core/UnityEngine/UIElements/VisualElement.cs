using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.UIElements;

public class VisualElement
{
  private readonly List<VisualElement> _children = new();
  private readonly List<string> _classList = new();
  private readonly Dictionary<string, object> _userData = new();
  private bool _enabledSelf = true;
  private PickingMode _pickingMode = PickingMode.Position;
  private UsageHints _usageHints;
  private string _name;
  private string _tooltip = string.Empty;
  private bool _focusable;
  private int _tabIndex;
  private string _viewDataKey = string.Empty;
  private RenderHints _renderHints;
  private string _languageDirection;
  private ObjectBindingContext _bindingContext;

  public string name
  {
    get => _name;
    set => _name = value;
  }

  public IStyle style { get; } = new Style();
  public ITransform transform { get; } = new UIElementsTransform();
  public bool enabledSelf
  {
    get => _enabledSelf;
    set => _enabledSelf = value;
  }

  public bool enabledHierarchy { get; set; } = true;
  public bool visible { get; set; } = true;
  public int childCount => _children.Count;
  public VisualElement parent { get; private set; }
  public IReadOnlyList<string> classList => _classList;
  public Rect layout { get; set; }
  public Rect contentRect { get; set; }
  public Rect worldBound { get; set; }
  public Rect worldTransform { get; set; }
  public int pseudoStates { get; set; }
  public object userData { get; set; }

  public string tooltip
  {
    get => _tooltip;
    set => _tooltip = value;
  }

  public bool focusable
  {
    get => _focusable;
    set => _focusable = value;
  }

  public int tabIndex
  {
    get => _tabIndex;
    set => _tabIndex = value;
  }

  public PickingMode pickingMode
  {
    get => _pickingMode;
    set => _pickingMode = value;
  }

  public UsageHints usageHints
  {
    get => _usageHints;
    set => _usageHints = value;
  }

  public string viewDataKey
  {
    get => _viewDataKey;
    set => _viewDataKey = value;
  }

  public RenderHints renderHints
  {
    get => _renderHints;
    set => _renderHints = value;
  }

  public string languageDirection
  {
    get => _languageDirection;
    set => _languageDirection = value;
  }

  public ISchedule schedule { get; } = new UIElementsScheduler();

  public StyleSheet customStyle { get; set; }

  public IBinding binding { get; set; }
  public ObjectBindingContext bindingContext
  {
    get => _bindingContext;
    set => _bindingContext = value;
  }

  public ITransform worldTransformRef { get; }
  public EventCallbackRegistry callbackRegistry { get; } = new();
  public IResolvedStyle resolvedStyle => new ResolvedStyle(style);

  public event Action<ChangeEvent<bool>> focusChanged;
  public event Action<ChangeEvent<string>> tooltipChanged;

  public VisualElement()
  {
    _name = string.Empty;
    worldTransformRef = transform;
  }

  public VisualElement(string name)
  {
    _name = name;
    worldTransformRef = transform;
  }

  public VisualElement(string name, string className)
  {
    _name = name;
    worldTransformRef = transform;
    if (!string.IsNullOrEmpty(className))
      _classList.Add(className);
  }

  public void Add(VisualElement child)
  {
    if (child is null)
      throw new ArgumentNullException(nameof(child));

    child.RemoveFromHierarchy();
    _children.Add(child);
    child.parent = this;
    OnHierarchyChange();
  }

  public void Insert(int index, VisualElement child)
  {
    if (child is null)
      throw new ArgumentNullException(nameof(child));
    if (index < 0 || index > _children.Count)
      throw new ArgumentOutOfRangeException(nameof(index));

    child.RemoveFromHierarchy();
    _children.Insert(index, child);
    child.parent = this;
    OnHierarchyChange();
  }

  public void Remove(VisualElement child)
  {
    if (child is null)
      throw new ArgumentNullException(nameof(child));

    if (_children.Remove(child))
    {
      child.parent = null;
      OnHierarchyChange();
    }
  }

  public void RemoveAt(int index)
  {
    if (index < 0 || index >= _children.Count)
      throw new ArgumentOutOfRangeException(nameof(index));

    var child = _children[index];
    _children.RemoveAt(index);
    child.parent = null;
    OnHierarchyChange();
  }

  public void Clear()
  {
    for (int i = _children.Count - 1; i >= 0; i--)
    {
      _children[i].parent = null;
    }
    _children.Clear();
    OnHierarchyChange();
  }

  public bool Contains(VisualElement child)
  {
    if (child is null) return false;
    return child.IsDescendantOf(this);
  }

  public VisualElement ElementAt(int index)
  {
    if (index < 0 || index >= _children.Count)
      throw new ArgumentOutOfRangeException(nameof(index));
    return _children[index];
  }

  public int IndexOf(VisualElement child)
  {
    return _children.IndexOf(child);
  }

  public bool HasChild(VisualElement child)
  {
    return _children.Contains(child);
  }

  public void BringToFront()
  {
    if (parent is null) return;
    parent.BringToFront(this);
  }

  public void SendToBack()
  {
    if (parent is null) return;
    parent.SendToBack(this);
  }

  public void PlaceBehind(VisualElement sibling)
  {
    if (sibling is null) throw new ArgumentNullException(nameof(sibling));
    if (parent is null) return;
    parent.PlaceBehind(this, sibling);
  }

  public void PlaceInFront(VisualElement sibling)
  {
    if (sibling is null) throw new ArgumentNullException(nameof(sibling));
    if (parent is null) return;
    parent.PlaceInFront(this, sibling);
  }

  public void SetEnabled(bool value)
  {
    enabledSelf = value;
  }

  public bool IsEnabled()
  {
    return enabledSelf && enabledHierarchy;
  }

  public void AddClass(string className)
  {
    if (!string.IsNullOrEmpty(className) && !_classList.Contains(className))
      _classList.Add(className);
  }

  public void AddClasses(params string[] classNames)
  {
    if (classNames is null) return;
    foreach (var className in classNames)
      AddClass(className);
  }

  public void RemoveClass(string className)
  {
    _classList.Remove(className);
  }

  public void RemoveClasses(params string[] classNames)
  {
    if (classNames is null) return;
    foreach (var className in classNames)
      RemoveClass(className);
  }

  public void ToggleClass(string className)
  {
    if (_classList.Contains(className))
      _classList.Remove(className);
    else
      _classList.Add(className);
  }

  public void ToggleClass(string className, bool enable)
  {
    if (enable) AddClass(className);
    else RemoveClass(className);
  }

  public bool ClassListContains(string cls)
  {
    return _classList.Contains(cls);
  }

  public void ClearClassList()
  {
    _classList.Clear();
  }

  public void RemoveFromClassList(string cls)
  {
    _classList.Remove(cls);
  }

  public void SetProperty(string name, object value)
  {
    _userData[name] = value;
  }

  public T GetProperty<T>(string name, T defaultValue = default)
  {
    if (_userData.TryGetValue(name, out var value) && value is T typed)
      return typed;
    return defaultValue;
  }

  public void MarkDirtyRepaint()
  {
  }

  public void SendEvent(EventBase evt)
  {
    _ = evt;
  }

  public void Focus()
  {
  }

  public void Blur()
  {
  }

  public bool HasFocus()
  {
    return false;
  }

  public void SetPickingMode(PickingMode mode)
  {
    _pickingMode = mode;
  }

  public void SetUsageHints(UsageHints hints)
  {
    _usageHints = hints;
  }

  public void BindStyleSheet(StyleSheet styleSheet)
  {
    customStyle = styleSheet;
  }

  public void BindingPath(string path, object dataSource, Func<object, object> getter, Action<object, object> setter)
  {
    _ = path;
    _ = dataSource;
    _ = getter;
    _ = setter;
  }

  // UQuery support
  public VisualElement Q(string name = null, string className = null)
  {
    return Query<VisualElement>(name, className).FirstOrDefault();
  }

  public T Q<T>(string name = null, string className = null) where T : VisualElement
  {
    return Query<T>(name, className).FirstOrDefault();
  }

  public UQueryEnumerable<T> Query<T>(string name = null, string className = null) where T : VisualElement
  {
    return new UQueryEnumerable<T>(this, name, className);
  }

  public UQueryEnumerable<VisualElement> Query(string name = null, string className = null)
  {
    return new UQueryEnumerable<VisualElement>(this, name, className);
  }

  public UQueryExpression<T> Q<T>(string name, string className, int index) where T : VisualElement
  {
    _ = index;
    return new UQueryExpression<T>(Query<T>(name, className).ToList());
  }

  // Callback system
  public void RegisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
  {
    callbackRegistry.RegisterCallback(callback, useTrickleDown);
  }

  public void UnregisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
  {
    callbackRegistry.UnregisterCallback(callback, useTrickleDown);
  }

  public bool HasCapabilities(params Capability[] capabilities)
  {
    _ = capabilities;
    return true;
  }

  public void RemoveFromHierarchy()
  {
    parent?.Remove(this);
  }

  public IEnumerable<VisualElement> Children()
  {
    return _children;
  }

  public virtual string GenerateHint()
  {
    return string.Empty;
  }

  protected virtual void OnHierarchyChange()
  {
  }

  internal bool IsDescendantOf(VisualElement ancestor)
  {
    var current = parent;
    while (current != null)
    {
      if (current == ancestor) return true;
      current = current.parent;
    }
    return false;
  }

  private bool Matches(VisualElement element, string name, string className)
  {
    if (!string.IsNullOrEmpty(name) && element.name != name) return false;
    if (!string.IsNullOrEmpty(className) && !element.ClassListContains(className)) return false;
    return true;
  }

  private void BringToFront(VisualElement child)
  {
    var index = _children.IndexOf(child);
    if (index >= 0 && index < _children.Count - 1)
    {
      _children.RemoveAt(index);
      _children.Add(child);
    }
  }

  private void SendToBack(VisualElement child)
  {
    var index = _children.IndexOf(child);
    if (index > 0)
    {
      _children.RemoveAt(index);
      _children.Insert(0, child);
    }
  }

  private void PlaceBehind(VisualElement target, VisualElement sibling)
  {
    var targetIndex = _children.IndexOf(target);
    if (targetIndex >= 0)
    {
      _children.RemoveAt(targetIndex);
      var newIndex = _children.IndexOf(sibling);
      _children.Insert(newIndex, target);
    }
  }

  private void PlaceInFront(VisualElement target, VisualElement sibling)
  {
    var targetIndex = _children.IndexOf(target);
    if (targetIndex >= 0)
    {
      _children.RemoveAt(targetIndex);
      var newIndex = _children.IndexOf(sibling) + 1;
      _children.Insert(newIndex, target);
    }
  }
}

// Enums
public enum PickingMode
{
  Position = 0,
  Ignore = 1
}

[Flags]
public enum UsageHints
{
  None = 0,
  DynamicTransform = 1 << 0,
  GroupTransform = 1 << 1,
  MaskContainer = 1 << 2,
  DynamicColor = 1 << 3
}

public enum Capability
{
  Clipboard = 0,
  EventSynthesizer = 1,
  Tooltip = 2,
  DragAndDrop = 3,
  Keyboard = 4
}

[Flags]
public enum RenderHints
{
  None = 0,
  Billboard = 1 << 0,
  GroupTransform = 1 << 1,
  ClipWithScissors = 1 << 2,
  ShadowInjection = 1 << 3
}

public enum LanguageDirection
{
  LTR,
  RTL
}

// Style types
public interface IStyle
{
  StyleLength width { get; set; }
  StyleLength height { get; set; }
  StyleLength minWidth { get; set; }
  StyleLength minHeight { get; set; }
  StyleLength maxWidth { get; set; }
  StyleLength maxHeight { get; set; }
  StyleColor backgroundColor { get; set; }
  StyleColor color { get; set; }
  StyleInt fontSize { get; set; }
  StyleEnum<DisplayStyle> display { get; set; }
  StyleEnum<Visibility> visibility { get; set; }
  StyleEnum<Position> position { get; set; }
  StyleFloat flexGrow { get; set; }
  StyleFloat flexShrink { get; set; }
  StyleLength flexBasis { get; set; }
  StyleEnum<FlexDirection> flexDirection { get; set; }
  StyleEnum<Wrap> flexWrap { get; set; }
  StyleLength top { get; set; }
  StyleLength left { get; set; }
  StyleLength right { get; set; }
  StyleLength bottom { get; set; }
  StyleLength marginLeft { get; set; }
  StyleLength marginTop { get; set; }
  StyleLength marginRight { get; set; }
  StyleLength marginBottom { get; set; }
  StyleLength paddingLeft { get; set; }
  StyleLength paddingTop { get; set; }
  StyleLength paddingRight { get; set; }
  StyleLength paddingBottom { get; set; }
  StyleInt unitySliceTop { get; set; }
  StyleInt unitySliceRight { get; set; }
  StyleInt unitySliceBottom { get; set; }
  StyleInt unitySliceLeft { get; set; }
  StyleFloat opacity { get; set; }
  StyleFloat unityBackgroundImageTintColor { get; set; }
  StyleEnum<ScaleMode> backgroundSize { get; set; }
  StyleLength backgroundPositionX { get; set; }
  StyleLength backgroundPositionY { get; set; }
  StyleEnum<BackgroundPositionKeyword> backgroundRepeat { get; set; }
  StyleFloat letterSpacing { get; set; }
  StyleFloat wordSpacing { get; set; }
  StyleFloat whiteSpace { get; set; }
}

public interface IResolvedStyle
{
  float width { get; }
  float height { get; }
  float minWidth { get; }
  float minHeight { get; }
  float maxWidth { get; }
  float maxHeight { get; }
  Color backgroundColor { get; }
  Color color { get; }
  float fontSize { get; }
  DisplayStyle display { get; }
  Visibility visibility { get; }
  Position position { get; }
  float flexGrow { get; }
  float flexShrink { get; }
  float flexBasis { get; }
  FlexDirection flexDirection { get; }
  Wrap flexWrap { get; }
  float top { get; }
  float left { get; }
  float right { get; }
  float bottom { get; }
  float marginLeft { get; }
  float marginTop { get; }
  float marginRight { get; }
  float marginBottom { get; }
  float paddingLeft { get; }
  float paddingTop { get; }
  float paddingRight { get; }
  float paddingBottom { get; }
  float opacity { get; }
}

public struct StyleLength
{
  public StyleKeyword keyword;
  public float value;
  public static implicit operator StyleLength(float value) => new() { value = value };
  public static implicit operator StyleLength(int value) => new() { value = value };
  public static implicit operator StyleLength(StyleKeyword keyword) => new() { keyword = keyword };
  public static implicit operator StyleLength(Length length) => new() { value = length.value };
}

public struct StyleFloat
{
  public StyleKeyword keyword;
  public float value;
  public static implicit operator StyleFloat(float value) => new() { value = value };
  public static implicit operator StyleFloat(int value) => new() { value = value };
  public static implicit operator StyleFloat(StyleKeyword keyword) => new() { keyword = keyword };
}

public struct StyleInt
{
  public StyleKeyword keyword;
  public int value;
  public static implicit operator StyleInt(int value) => new() { value = value };
  public static implicit operator StyleInt(StyleKeyword keyword) => new() { keyword = keyword };
}

public struct StyleColor
{
  public StyleKeyword keyword;
  public Color value;
  public static implicit operator StyleColor(Color value) => new() { value = value };
  public static implicit operator StyleColor(StyleKeyword keyword) => new() { keyword = keyword };
}

public struct StyleEnum<T> where T : struct
{
  public StyleKeyword keyword;
  public T value;
}

public struct StyleBackground
{
  public StyleKeyword keyword;
  public Background value;
}

public struct Length
{
  public float value;
  public LengthUnit unit;
  public static Length Percent(float percent) => new() { value = percent, unit = LengthUnit.Percent };
  public static Length Auto() => new() { unit = LengthUnit.Auto };
}

public enum LengthUnit
{
  Pixel,
  Percent,
  Auto
}

public enum StyleKeyword
{
  Undefined,
  Auto,
  Initial,
  Inherit,
  None,
  Null,
  Unset
}

public enum DisplayStyle
{
  Flex = 0,
  None = 1
}

public enum Visibility
{
  Visible = 0,
  Hidden = 1
}

public enum Position
{
  Relative = 0,
  Absolute = 1
}

public enum FlexDirection
{
  Column = 0,
  ColumnReverse = 1,
  Row = 2,
  RowReverse = 3
}

public enum Wrap
{
  NoWrap = 0,
  Wrap = 1,
  WrapReverse = 2
}

public enum ScaleMode
{
  StretchToFill,
  ScaleAndCrop,
  ScaleToFit
}

public enum BackgroundPositionKeyword
{
  Center,
  Left,
  Right,
  Top,
  Bottom,
  TopLeft,
  TopRight,
  BottomLeft,
  BottomRight
}

public enum Align
{
  Auto = 0,
  FlexStart = 1,
  Center = 2,
  FlexEnd = 3,
  Stretch = 4
}

public enum Justify
{
  FlexStart = 0,
  Center = 1,
  FlexEnd = 2,
  SpaceBetween = 3,
  SpaceAround = 4,
  SpaceEvenly = 5
}

// Transform
public interface ITransform
{
  Vector3 position { get; set; }
  Quaternion rotation { get; set; }
  Vector3 scale { get; set; }
  Matrix4x4 matrix { get; }
}

public class UIElementsTransform : ITransform
{
  public Vector3 position { get; set; }
  public Quaternion rotation { get; set; } = Quaternion.identity;
  public Vector3 scale { get; set; } = Vector3.one;
  public Matrix4x4 matrix => Matrix4x4.TRS(position, rotation, scale);
}

// Default IStyle implementation
public class Style : IStyle
{
  public StyleLength width { get; set; }
  public StyleLength height { get; set; }
  public StyleLength minWidth { get; set; }
  public StyleLength minHeight { get; set; }
  public StyleLength maxWidth { get; set; }
  public StyleLength maxHeight { get; set; }
  public StyleColor backgroundColor { get; set; }
  public StyleColor color { get; set; }
  public StyleInt fontSize { get; set; }
  public StyleEnum<DisplayStyle> display { get; set; }
  public StyleEnum<Visibility> visibility { get; set; }
  public StyleEnum<Position> position { get; set; }
  public StyleFloat flexGrow { get; set; }
  public StyleFloat flexShrink { get; set; }
  public StyleLength flexBasis { get; set; }
  public StyleEnum<FlexDirection> flexDirection { get; set; }
  public StyleEnum<Wrap> flexWrap { get; set; }
  public StyleLength top { get; set; }
  public StyleLength left { get; set; }
  public StyleLength right { get; set; }
  public StyleLength bottom { get; set; }
  public StyleLength marginLeft { get; set; }
  public StyleLength marginTop { get; set; }
  public StyleLength marginRight { get; set; }
  public StyleLength marginBottom { get; set; }
  public StyleLength paddingLeft { get; set; }
  public StyleLength paddingTop { get; set; }
  public StyleLength paddingRight { get; set; }
  public StyleLength paddingBottom { get; set; }
  public StyleInt unitySliceTop { get; set; }
  public StyleInt unitySliceRight { get; set; }
  public StyleInt unitySliceBottom { get; set; }
  public StyleInt unitySliceLeft { get; set; }
  public StyleFloat opacity { get; set; }
  public StyleFloat unityBackgroundImageTintColor { get; set; }
  public StyleEnum<ScaleMode> backgroundSize { get; set; }
  public StyleLength backgroundPositionX { get; set; }
  public StyleLength backgroundPositionY { get; set; }
  public StyleEnum<BackgroundPositionKeyword> backgroundRepeat { get; set; }
  public StyleFloat letterSpacing { get; set; }
  public StyleFloat wordSpacing { get; set; }
  public StyleFloat whiteSpace { get; set; }
}

// Binding
public interface IBinding
{
  string path { get; set; }
  void PreUpdate();
  void Update();
  void Release();
  bool IsDirty();
}

public class BindableElement : VisualElement, IBinding
{
  public virtual string bindingPath { get; set; }
  public IBinding binding { get; set; }
  public string path { get; set; }

  public virtual void PreUpdate() { }
  public virtual void Update() { }
  public virtual void Release() { }
  public virtual bool IsDirty() => false;
}

public class ObjectBindingContext { }

// Background image
public struct Background
{
  public Texture2D texture;
  public Sprite sprite;
  public RenderTexture renderTexture;
  public VectorImage vectorImage;
  public Material material;
  public int sliceTop;
  public int sliceRight;
  public int sliceBottom;
  public int sliceLeft;
}

public class VectorImage : ScriptableObject
{
  public string name { get; set; } = string.Empty;
  public int width { get; set; }
  public int height { get; set; }
}

public class ResolvedStyle : IResolvedStyle
{
  private readonly IStyle _style;

  public ResolvedStyle(IStyle style)
  {
    _style = style;
  }

  public float width => _style.width.value;
  public float height => _style.height.value;
  public float minWidth => _style.minWidth.value;
  public float minHeight => _style.minHeight.value;
  public float maxWidth => _style.maxWidth.value;
  public float maxHeight => _style.maxHeight.value;
  public Color backgroundColor => _style.backgroundColor.value;
  public Color color => _style.color.value;
  public float fontSize => _style.fontSize.value;
  public DisplayStyle display => _style.display.value;
  public Visibility visibility => _style.visibility.value;
  public Position position => _style.position.value;
  public float flexGrow => _style.flexGrow.value;
  public float flexShrink => _style.flexShrink.value;
  public float flexBasis => _style.flexBasis.value;
  public FlexDirection flexDirection => _style.flexDirection.value;
  public Wrap flexWrap => _style.flexWrap.value;
  public float top => _style.top.value;
  public float left => _style.left.value;
  public float right => _style.right.value;
  public float bottom => _style.bottom.value;
  public float marginLeft => _style.marginLeft.value;
  public float marginTop => _style.marginTop.value;
  public float marginRight => _style.marginRight.value;
  public float marginBottom => _style.marginBottom.value;
  public float paddingLeft => _style.paddingLeft.value;
  public float paddingTop => _style.paddingTop.value;
  public float paddingRight => _style.paddingRight.value;
  public float paddingBottom => _style.paddingBottom.value;
  public float opacity => _style.opacity.value;
}
