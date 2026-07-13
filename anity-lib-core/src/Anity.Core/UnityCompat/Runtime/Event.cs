using System;
using System.Collections.Generic;

namespace UnityEngine;

public class Event
{
  private static readonly Stack<Event> _eventStack = new();
  private static Event? _current;
  private static readonly Queue<Event> _eventQueue = new();

  public EventType type { get; set; }
  public EventType rawType { get; set; }
  public int button { get; set; }
  public EventModifiers modifiers { get; set; }
  public Vector2 mousePosition { get; set; }
  public Vector2 delta { get; set; }
  public KeyCode keyCode { get; set; }
  public char character { get; set; }
  public string commandName { get; set; } = string.Empty;
  public int clickCount { get; set; }
  public int displayIndex { get; set; }
  public Ray mouseRay { get; set; }
  public PointerType pointerType { get; set; } = PointerType.Mouse;
  private bool _used;

  public bool shift
  {
    get => (modifiers & EventModifiers.Shift) != 0;
    set
    {
      if (value) modifiers |= EventModifiers.Shift;
      else modifiers &= ~EventModifiers.Shift;
    }
  }

  public bool control
  {
    get => (modifiers & EventModifiers.Control) != 0;
    set
    {
      if (value) modifiers |= EventModifiers.Control;
      else modifiers &= ~EventModifiers.Control;
    }
  }

  public bool alt
  {
    get => (modifiers & EventModifiers.Alt) != 0;
    set
    {
      if (value) modifiers |= EventModifiers.Alt;
      else modifiers &= ~EventModifiers.Alt;
    }
  }

  public bool command
  {
    get => (modifiers & EventModifiers.Command) != 0;
    set
    {
      if (value) modifiers |= EventModifiers.Command;
      else modifiers &= ~EventModifiers.Command;
    }
  }

  public bool capsLock
  {
    get => (modifiers & EventModifiers.CapsLock) != 0;
    set
    {
      if (value) modifiers |= EventModifiers.CapsLock;
      else modifiers &= ~EventModifiers.CapsLock;
    }
  }

  public bool numeric
  {
    get => (modifiers & EventModifiers.Numeric) != 0;
    set
    {
      if (value) modifiers |= EventModifiers.Numeric;
      else modifiers &= ~EventModifiers.Numeric;
    }
  }

  public bool functionKey
  {
    get => (modifiers & EventModifiers.FunctionKey) != 0;
    set
    {
      if (value) modifiers |= EventModifiers.FunctionKey;
      else modifiers &= ~EventModifiers.FunctionKey;
    }
  }

  public bool isMouse => type == EventType.MouseDown || type == EventType.MouseUp || type == EventType.MouseMove || type == EventType.MouseDrag || type == EventType.ContextClick;
  public bool isKey => type == EventType.KeyDown || type == EventType.KeyUp;
  public bool isScrollWheel => type == EventType.ScrollWheel;

  public Event() {}

  public Event(int displayIndex)
  {
    this.displayIndex = displayIndex;
  }

  public Event(Event other)
  {
    if (other == null) return;
    type = other.type;
    rawType = other.rawType;
    mousePosition = other.mousePosition;
    delta = other.delta;
    button = other.button;
    keyCode = other.keyCode;
    character = other.character;
    commandName = other.commandName;
    clickCount = other.clickCount;
    shift = other.shift;
    control = other.control;
    alt = other.alt;
    command = other.command;
    capsLock = other.capsLock;
    numeric = other.numeric;
    functionKey = other.functionKey;
    displayIndex = other.displayIndex;
    mouseRay = other.mouseRay;
    modifiers = other.modifiers;
    pointerType = other.pointerType;
  }

  public static Event current
  {
    get => _current ?? (_current = new Event());
    set => _current = value;
  }

  public EventType GetTypeForControl(int controlID)
  {
    _ = controlID;
    return type;
  }

  public void Use()
  {
    _used = true;
    type = EventType.Used;
  }

  public static int GetEventCount()
  {
    return _eventQueue.Count;
  }

  public static void PopEvent(Event outEvent)
  {
    if (outEvent == null) return;
    if (_eventQueue.Count > 0)
    {
      var e = _eventQueue.Dequeue();
      outEvent.CopyFrom(e);
    }
  }

  public static void PushEvent(Event e)
  {
    if (e != null) _eventQueue.Enqueue(new Event(e));
  }

  internal static int GetEventQueueCount() => _eventQueue.Count;

  public Vector2 Delta()
  {
    return delta;
  }

  public bool IsRaycastableObject()
  {
    return false;
  }

  public bool IsScrollWheel()
  {
    return isScrollWheel;
  }

  public Vector2 HandleDragAndDrop(int id)
  {
    _ = id;
    return delta;
  }

  private void CopyFrom(Event other)
  {
    type = other.type;
    rawType = other.rawType;
    mousePosition = other.mousePosition;
    delta = other.delta;
    button = other.button;
    keyCode = other.keyCode;
    character = other.character;
    commandName = other.commandName;
    clickCount = other.clickCount;
    modifiers = other.modifiers;
    displayIndex = other.displayIndex;
    mouseRay = other.mouseRay;
    pointerType = other.pointerType;
  }
}

public enum EventType
{
  MouseDown = 0,
  MouseUp = 1,
  MouseMove = 2,
  MouseDrag = 3,
  KeyDown = 4,
  KeyUp = 5,
  ScrollWheel = 6,
  Repaint = 7,
  Layout = 8,
  DragUpdated = 9,
  DragPerform = 10,
  DragExited = 11,
  Ignore = 12,
  Used = 13,
  ValidateCommand = 14,
  ExecuteCommand = 15,
  ContextClick = 16,
  MouseEnterWindow = 20,
  MouseLeaveWindow = 21,
  TouchDown = 30,
  TouchUp = 31,
  TouchMove = 32,
  TouchEnter = 33,
  TouchLeave = 34,
  TouchStationary = 35
}

[Flags]
public enum EventModifiers
{
  None = 0,
  Shift = 1,
  Control = 2,
  Alt = 4,
  Command = 8,
  Numeric = 16,
  CapsLock = 32,
  FunctionKey = 64
}

public enum PointerType
{
  Mouse,
  Touch,
  Pen
}

public enum FocusType
{
  Keyboard = 0,
  Passive = 1,
  Native = 2,
  /// <summary>Alias kept for older EditorGUI call sites.</summary>
  Mouse = Passive
}
