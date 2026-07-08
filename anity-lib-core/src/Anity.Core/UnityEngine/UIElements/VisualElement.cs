using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

public class VisualElement
{
  private readonly List<VisualElement> _children = new();
  private readonly List<string> _classList = new();
  private readonly Dictionary<string, object> _userData = new();

  public string name { get; set; } = string.Empty;
  public IStyle style { get; } = new Style();
  public ITransform transform { get; } = new UIElementsTransform();
  public bool enabledSelf { get; set; } = true;
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
  public string tooltip { get; set; } = string.Empty;
  public bool focusable { get; set; }
  public string viewDataKey { get; set; } = string.Empty;
  public bool scheduleUpdate { get; set; }

  public event Action<ChangeEvent<bool>> focusChanged;
  public event Action<ChangeEvent<string>> tooltipChanged;

  public VisualElement()
  {
  }

  public VisualElement(string name)
  {
    this.name = name;
  }

  public void Add(VisualElement child)
  {
    if (child is null)
      throw new ArgumentNullException(nameof(child));

    child.RemoveFromHierarchy();
    _children.Add(child);
    child.parent = this;
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
  }

  public void Remove(VisualElement child)
  {
    if (child is null)
      throw new ArgumentNullException(nameof(child));

    if (_children.Remove(child))
    {
      child.parent = null;
    }
  }

  public void RemoveAt(int index)
  {
    if (index < 0 || index >= _children.Count)
      throw new ArgumentOutOfRangeException(nameof(index));

    var child = _children[index];
    _children.RemoveAt(index);
    child.parent = null;
  }

  public void Clear()
  {
    for (int i = _children.Count - 1; i >= 0; i--)
    {
      _children[i].parent = null;
    }
    _children.Clear();
  }

  public bool Contains(VisualElement child)
  {
    if (child is null)
      return false;

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
    if (parent is null)
      return;
    parent.BringToFront(this);
  }

  public void SendToBack()
  {
    if (parent is null)
      return;
    parent.SendToBack(this);
  }

  public void PlaceBehind(VisualElement sibling)
  {
    if (sibling is null)
      throw new ArgumentNullException(nameof(sibling));
    if (parent is null)
      return;
    parent.PlaceBehind(this, sibling);
  }

  public void PlaceInFront(VisualElement sibling)
  {
    if (sibling is null)
      throw new ArgumentNullException(nameof(sibling));
    if (parent is null)
      return;
    parent.PlaceInFront(this, sibling);
  }

  public void SetEnabled(bool value)
  {
    enabledSelf = value;
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
    if (enable)
      AddClass(className);
    else
      RemoveClass(className);
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

  public object GetProperty(string name)
  {
    return _userData.TryGetValue(name, out var value) ? value : null;
  }

  public T Q<T>(string name = null, string className = null) where T : VisualElement
  {
    return Query<T>(name, className).FirstOrDefault();
  }

  public VisualElement Q(string name = null, string className = null)
  {
    return Q<VisualElement>(name, className);
  }

  public IEnumerable<T> Query<T>(string name = null, string className = null) where T : VisualElement
  {
    foreach (var child in Children())
    {
      if (child is T typed && Matches(typed, name, className))
        yield return typed;

      foreach (var descendant in child.Query<T>(name, className))
        yield return descendant;
    }
  }

  public StyleSheet customStyle { get; set; }

  public void BindStyleSheet(StyleSheet styleSheet)
  {
    customStyle = styleSheet;
  }

  public virtual string GenerateHint()
  {
    return string.Empty;
  }

  public EventCallbackRegistry callbackRegistry { get; } = new();

  public void RegisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
  {
    callbackRegistry.RegisterCallback(callback, useTrickleDown);
  }

  public void UnregisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
  {
    callbackRegistry.UnregisterCallback(callback, useTrickleDown);
  }

  public void RemoveFromHierarchy()
  {
    parent?.Remove(this);
  }

  public IEnumerable<VisualElement> Children()
  {
    return _children;
  }

  public ITransform worldTransformRef { get; }

  internal bool IsDescendantOf(VisualElement ancestor)
  {
    var current = parent;
    while (current != null)
    {
      if (current == ancestor)
        return true;
      current = current.parent;
    }
    return false;
  }

  private bool Matches(VisualElement element, string name, string className)
  {
    if (!string.IsNullOrEmpty(name) && element.name != name)
      return false;
    if (!string.IsNullOrEmpty(className) && !element.ClassListContains(className))
      return false;
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
    var siblingIndex = _children.IndexOf(sibling);
    if (targetIndex >= 0 && siblingIndex >= 0)
    {
      _children.RemoveAt(targetIndex);
      var newIndex = _children.IndexOf(sibling);
      _children.Insert(newIndex, target);
    }
  }

  private void PlaceInFront(VisualElement target, VisualElement sibling)
  {
    var targetIndex = _children.IndexOf(target);
    var siblingIndex = _children.IndexOf(sibling);
    if (targetIndex >= 0 && siblingIndex >= 0)
    {
      _children.RemoveAt(targetIndex);
      var newIndex = _children.IndexOf(sibling) + 1;
      _children.Insert(newIndex, target);
    }
  }
}

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
}

public struct StyleLength
{
  public StyleKeyword keyword;
  public float value;

  public static implicit operator StyleLength(float value) => new() { value = value };
  public static implicit operator StyleLength(int value) => new() { value = value };
  public static implicit operator StyleLength(StyleKeyword keyword) => new() { keyword = keyword };
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
}

public class ChangeEvent<T>
{
  public T previousValue { get; set; }
  public T newValue { get; set; }
  public bool bubbles { get; set; }
  public bool isPropagationStopped { get; set; }
}

public delegate void EventCallback<in TEventType>(TEventType evt);
public enum TrickleDown { NoTrickleDown = 0, TrickleDown = 1 }

public class EventCallbackRegistry
{
  public void RegisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown)
  {
    // Stub
  }

  public void UnregisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown)
  {
    // Stub
  }
}
