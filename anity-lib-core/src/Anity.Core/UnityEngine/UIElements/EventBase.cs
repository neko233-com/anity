using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

// Event system
public abstract class EventBase
{
  private static long _nextId;
  public long eventTypeId { get; set; }
  public long id { get; } = _nextId++;
  public double time { get; set; }
  public EventBase eventBase { get; set; }
  public bool propagation { get; set; }
  public PropagationPhase propagationPhase { get; set; }
  public EventBase originalEvent { get; set; }
  public bool pooled { get; set; }
  public bool tricklesDown { get; set; }
  public VisualElement target { get; set; }
  public VisualElement currentTarget { get; set; }
  public bool isDefaultPrevented => false;
  public bool stopPropagation => false;
  public bool stopImmediatePropagation => false;
  public bool preventDefault => false;

  public void StopPropagation()
  {
  }

  public void StopImmediatePropagation()
  {
  }

  public void PreventDefault()
  {
  }

  public void PreDispatch(IPanel panel)
  {
    _ = panel;
  }

  public void PostDispatch(IPanel panel)
  {
    _ = panel;
  }

  public static T GetPooled<T>() where T : EventBase, new()
  {
    return new T();
  }

  public static EventBase GetPooled(Type eventType)
  {
    _ = eventType;
    return null;
  }
}

public enum PropagationPhase
{
  None,
  TrickleDown,
  AtTarget,
  BubbleUp,
  DefaultActionAtTarget,
  DefaultAction
}

public interface IPanel
{
  VisualElement visualTree { get; }
  PanelContextType contextType { get; }
  bool isFocused { get; }
  void Update();
  void Repaint();
}

public enum PanelContextType
{
  Standard,
  PlayerDebugConsole
}

// Specific event types
public class EventBase<T> : EventBase where T : EventBase<T>, new()
{
  public static long TypeId()
  {
    return typeof(T).GetHashCode();
  }
}

public class ChangeEvent<T> : EventBase<ChangeEvent<T>>
{
  public T previousValue { get; set; }
  public T newValue { get; set; }
  public bool bubbles { get; set; }

  public new static long TypeId()
  {
    return EventBase<ChangeEvent<T>>.TypeId();
  }

  public static ChangeEvent<T> GetPooled(T previousValue, T newValue)
  {
    var e = EventBase.GetPooled<ChangeEvent<T>>();
    e.previousValue = previousValue;
    e.newValue = newValue;
    return e;
  }
}

public class FocusEvent : EventBase<FocusEvent>
{
  public IEventHandler relatedTarget { get; set; }
  public FocusDirection direction { get; set; }
}

public class BlurEvent : EventBase<BlurEvent>
{
  public IEventHandler relatedTarget { get; set; }
  public FocusDirection direction { get; set; }
}

public enum FocusDirection
{
  None,
  Forward,
  Backward
}

public class FocusInEvent : EventBase<FocusInEvent>
{
  public IEventHandler relatedTarget { get; set; }
}

public class FocusOutEvent : EventBase<FocusOutEvent>
{
  public IEventHandler relatedTarget { get; set; }
  public FocusDirection direction { get; set; }
}

public class MouseDownEvent : EventBase<MouseDownEvent>
{
  public int button { get; set; }
  public int clickCount { get; set; }
  public Vector2 mousePosition { get; set; }
}

public class MouseUpEvent : EventBase<MouseUpEvent>
{
  public int button { get; set; }
  public int clickCount { get; set; }
  public Vector2 mousePosition { get; set; }
}

public class MouseMoveEvent : EventBase<MouseMoveEvent>
{
  public Vector2 mousePosition { get; set; }
  public Vector2 delta { get; set; }
  public int button { get; set; }
}

public class MouseEnterEvent : EventBase<MouseEnterEvent>
{
}

public class MouseLeaveEvent : EventBase<MouseLeaveEvent>
{
}

public class MouseOverEvent : EventBase<MouseOverEvent>
{
}

public class MouseOutEvent : EventBase<MouseOutEvent>
{
}

public class ContextualMenuPopulateEvent : EventBase<ContextualMenuPopulateEvent>
{
  public ContextualMenuManager menuManager { get; set; }
  public IEventHandler target { get; set; }
  public MenuBuilder menu { get; set; }
}

public interface ContextualMenuManager
{
  void DisplayMenuManipulatorIGenericMenuToEvent(EventBase evt, IEventHandler target);
}

public interface IEventHandler { }

public class KeyDownEvent : EventBase<KeyDownEvent>
{
  public KeyCode keyCode { get; set; }
  public char character { get; set; }
  public bool shiftKey { get; set; }
  public bool ctrlKey { get; set; }
  public bool altKey { get; set; }
  public bool commandKey { get; set; }
}

public class KeyUpEvent : EventBase<KeyUpEvent>
{
  public KeyCode keyCode { get; set; }
  public char character { get; set; }
  public bool shiftKey { get; set; }
  public bool ctrlKey { get; set; }
  public bool altKey { get; set; }
  public bool commandKey { get; set; }
}

public class NavigationMoveEvent : EventBase<NavigationMoveEvent>
{
  public NavigationMoveEvent()
  {
  }

  public NavigationMoveEvent(Vector2 move)
  {
    this.move = move;
  }

  public Vector2 move { get; set; }
  public Direction direction { get; set; }
}

public enum Direction
{
  None,
  Left,
  Up,
  Right,
  Down
}

public class NavigationTabEvent : EventBase<NavigationTabEvent>
{
  public Direction direction { get; set; }
}

public class NavigationSubmitEvent : EventBase<NavigationSubmitEvent>
{
}

public class NavigationCancelEvent : EventBase<NavigationCancelEvent>
{
}

public class AttachToPanelEvent : EventBase<AttachToPanelEvent>
{
  public IPanel destinationPanel { get; set; }
  public IPanel originPanel { get; set; }
}

public class DetachFromPanelEvent : EventBase<DetachFromPanelEvent>
{
  public IPanel destinationPanel { get; set; }
  public IPanel originPanel { get; set; }
}

public class DragUpdatedEvent : EventBase<DragUpdatedEvent>
{
  public DragAndDropPeer dragAndDropPeer { get; set; }
}

public class DragPerformEvent : EventBase<DragPerformEvent>
{
  public DragAndDropPeer dragAndDropPeer { get; set; }
}

public class DragExitedEvent : EventBase<DragExitedEvent>
{
}

public class DragEnterEvent : EventBase<DragEnterEvent>
{
}

public class DragLeaveEvent : EventBase<DragLeaveEvent>
{
}

public class DragAndDropPeer { }

public class TooltipsEvent : EventBase<TooltipsEvent>
{
  public TooltipEvent tooltip { get; set; }
}

public class TooltipEvent
{
  public string tooltip { get; set; }
  public Vector2 mousePosition { get; set; }
}

// IMGUIContainer event handling
public class IMGUIContainer : VisualElement
{
  public Action onGUIHandler { get; set; }
  public bool disableClipping { get; set; }

  public void OnGUI()
  {
    onGUIHandler?.Invoke();
  }
}

// Menu support
public class MenuBuilder
{
  public void AppendAction(string actionName, Action<object> action, object userData = null)
  {
    _ = actionName;
    _ = action;
    _ = userData;
  }

  public void AppendSeparator(string subMenuPath = null)
  {
    _ = subMenuPath;
  }

  public void DropDown(Vector2 position, VisualElement targetElement)
  {
    _ = position;
    _ = targetElement;
  }
}

// DropdownMenu support
public class DropdownMenu
{
  public void AppendAction(string actionName, Action<object> action, DropdownMenuEventInfo eventInfo, object userData = null)
  {
    _ = actionName;
    _ = action;
    _ = eventInfo;
    _ = userData;
  }

  public void AppendSeparator(string subMenuPath = null)
  {
    _ = subMenuPath;
  }

  public void DropDown(Vector2 position, VisualElement targetElement)
  {
    _ = position;
    _ = targetElement;
  }
}

public struct DropdownMenuEventInfo
{
}

// EventCallback system
public delegate void EventCallback<in TEventType>(TEventType evt);

public enum TrickleDown
{
  NoTrickleDown = 0,
  TrickleDown = 1
}

public class EventCallbackRegistry
{
  private readonly Dictionary<Type, List<Delegate>> _callbacks = new();

  public void RegisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown)
  {
    var type = typeof(TEventType);
    if (!_callbacks.TryGetValue(type, out var list))
    {
      list = new List<Delegate>();
      _callbacks[type] = list;
    }

    if (!list.Contains(callback))
      list.Add(callback);
  }

  public void UnregisterCallback<TEventType>(EventCallback<TEventType> callback, TrickleDown useTrickleDown)
  {
    var type = typeof(TEventType);
    if (_callbacks.TryGetValue(type, out var list))
    {
      list.Remove(callback);
    }
  }

  public bool HasRegistrations<TEventType>()
  {
    return _callbacks.ContainsKey(typeof(TEventType));
  }
}

// EventCallback utilities
public static class EventCallbackUtilities
{
  public static void RegisterCallback<TEventType>(this VisualElement element, EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
  {
    element.RegisterCallback(callback, useTrickleDown);
  }

  public static void UnregisterCallback<TEventType>(this VisualElement element, EventCallback<TEventType> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
  {
    element.UnregisterCallback(callback, useTrickleDown);
  }
}
