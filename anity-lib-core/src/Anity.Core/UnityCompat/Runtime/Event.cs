namespace UnityEngine;

public class Event
{
  public UnityEngine.EventType type { get; set; }
  public Vector2 mousePosition { get; set; }
  public Vector2 delta { get; set; }
  public int button { get; set; }
  public KeyCode keyCode { get; set; }
  public char character { get; set; }
  public int commandName { get; set; }
  public int clickCount { get; set; }
  public bool shift { get; set; }
  public bool control { get; set; }
  public bool alt { get; set; }
  public bool command { get; set; }
  public bool capsLock { get; set; }
  public bool numeric { get; set; }
  public bool functionKey { get; set; }
  public bool isMouse { get; set; }
  public bool isKey { get; set; }
  public bool isScrollWheel { get; set; }
  public int displayIndex { get; set; }
  public Ray mouseRay { get; set; }
  public int modifiers { get; set; }
  public bool use { get; set; }
  public int pointerType { get; set; }

  public Event() {}
  public Event(int displayIndex) { this.displayIndex = displayIndex; }
  public Event(Event other)
  {
    if (other == null) return;
    type = other.type;
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
    isMouse = other.isMouse;
    isKey = other.isKey;
    isScrollWheel = other.isScrollWheel;
    displayIndex = other.displayIndex;
    mouseRay = other.mouseRay;
    modifiers = other.modifiers;
    use = other.use;
    pointerType = other.pointerType;
  }
  public static Event current { get; set; }

  public UnityEngine.EventType GetTypeForControl(int controlID)
  {
    _ = controlID;
    return type;
  }

  public void Use()
  {
    use = true;
  }

  public Vector2 Delta()
  {
    return delta;
  }

  public Vector2 DeltaMagnitude()
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
}

public enum EventType
{
  MouseDown = 0,
  MouseUp = 1,
  MouseDrag = 3,
  MouseMove = 2,
  KeyDown = 4,
  KeyUp = 5,
  ScrollWheel = 6,
  Repaint = 7,
  Layout = 8,
  Used = 12
}
