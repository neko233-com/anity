using System;

namespace UnityEngine.UI;

public class Selectable : MonoBehaviour
{
  private bool _interactable = true;
  private Navigation _navigation = new();
  private Transition _transition = Transition.ColorTint;
  private ColorBlock _colors = ColorBlock.defaultColorBlock;
  private SpriteState _spriteState;
  private AnimationTriggers? _animationTriggers;
  private Graphic? _targetGraphic;

  public bool interactable
  {
    get => _interactable;
    set
    {
      _interactable = value;
      if (!_interactable)
      {
        OnPointerExit(null);
      }
    }
  }

  public Navigation navigation
  {
    get => _navigation;
    set => _navigation = value;
  }

  public Transition transition
  {
    get => _transition;
    set => _transition = value;
  }

  public ColorBlock colors
  {
    get => _colors;
    set => _colors = value;
  }

  public SpriteState spriteState
  {
    get => _spriteState;
    set => _spriteState = value;
  }

  public AnimationTriggers? animationTriggers
  {
    get => _animationTriggers;
    set => _animationTriggers = value;
  }

  public Graphic? targetGraphic
  {
    get => _targetGraphic;
    set => _targetGraphic = value;
  }

  public virtual bool IsInteractable()
  {
    return _interactable;
  }

  public virtual void Select()
  {
    if (!_interactable)
    {
      return;
    }

    OnSelect(null);
  }

  protected new virtual void OnEnable()
  {
    OnPointerExit(null);
  }

  protected new virtual void OnDisable()
  {
    OnPointerExit(null);
  }

  protected virtual void OnPointerEnter(PointerEventData? eventData) {}
  protected virtual void OnPointerExit(PointerEventData? eventData) {}
  protected virtual void OnPointerDown(PointerEventData? eventData) {}
  protected virtual void OnPointerUp(PointerEventData? eventData) {}
  protected virtual void OnSelect(BaseEventData? eventData) {}
  protected virtual void OnDeselect(BaseEventData? eventData) {}
  protected virtual void OnMove(AxisEventData? eventData) {}
  protected virtual void OnSubmit(BaseEventData? eventData) {}
  protected virtual void OnCancel(BaseEventData? eventData) {}
}

public struct Navigation
{
  public NavigationMode mode;
  public Selectable? selectOnUp;
  public Selectable? selectOnDown;
  public Selectable? selectOnLeft;
  public Selectable? selectOnRight;

  public static Navigation defaultNavigation => new() { mode = NavigationMode.Automatic };
}

public struct ColorBlock
{
  public Color normalColor;
  public Color highlightedColor;
  public Color pressedColor;
  public Color selectedColor;
  public Color disabledColor;
  public float colorMultiplier;
  public float fadeDuration;

  public static ColorBlock defaultColorBlock => new()
  {
    normalColor = new Color(1f, 1f, 1f, 1f),
    highlightedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f),
    pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f),
    selectedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f),
    disabledColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5f),
    colorMultiplier = 1f,
    fadeDuration = 0.1f
  };
}

public struct SpriteState
{
  public Sprite? highlightedSprite;
  public Sprite? pressedSprite;
  public Sprite? selectedSprite;
  public Sprite? disabledSprite;
}

public class AnimationTriggers
{
  public string normalTrigger { get; set; } = string.Empty;
  public string highlightedTrigger { get; set; } = string.Empty;
  public string pressedTrigger { get; set; } = string.Empty;
  public string selectedTrigger { get; set; } = string.Empty;
  public string disabledTrigger { get; set; } = string.Empty;
}

public class PointerEventData
{
  public Vector2 position { get; set; }
  public Vector2 delta { get; set; }
  public bool eligibleForClick { get; set; }
  public int clickCount { get; set; }
  public float clickTime { get; set; }
  public Camera? pressEventCamera { get; set; }
  public Camera? enterEventCamera { get; set; }
  public GameObject? pointerEnter { get; set; }
  public GameObject? pointerPress { get; set; }
  public GameObject? pointerDrag { get; set; }
  public GameObject? pointerClick { get; set; }
  public bool dragging { get; set; }
}

public class BaseEventData
{
  public Selectable? currentSelectedGameObject { get; set; }
}

public class AxisEventData : BaseEventData
{
  public Vector2 moveVector { get; set; }
  public MoveDirection moveDir { get; set; }
}

public enum MoveDirection
{
  Left = 0,
  Up = 1,
  Right = 2,
  Down = 3,
  None = 4
}
