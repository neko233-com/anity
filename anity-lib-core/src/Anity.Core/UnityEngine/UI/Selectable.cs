using System;
using System.Collections.Generic;

namespace UnityEngine.UI;

public class Selectable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
  private static List<Selectable> _selectables = new();
  private static Selectable? _currentSelection;

  private bool _interactable = true;
  private Navigation _navigation = new();
  private Transition _transition = Transition.ColorTint;
  private ColorBlock _colors = ColorBlock.defaultColorBlock;
  private SpriteState _spriteState;
  private AnimationTriggers? _animationTriggers;
  private Graphic? _targetGraphic;
  private bool _hasSelection;
  private bool _isPointerInside;
  private bool _isPointerDown;
  private bool _hasSubmitted;
  private bool _evaluateAndHighlightOnPointerEnter;
  private bool _evaluateAndHighlightOnPointerExit;

  public bool interactable
  {
    get => _interactable;
    set
    {
      if (_interactable != value)
      {
        _interactable = value;
        OnSetProperty();
      }
    }
  }

  public Navigation navigation
  {
    get => _navigation;
    set
    {
      if (_navigation != value)
      {
        _navigation = value;
        OnSetProperty();
      }
    }
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

  public bool isPointerInside => _isPointerInside;
  public bool isPointerDown => _isPointerDown;
  public bool hasSubmitted => _hasSubmitted;

  public virtual bool IsInteractable()
  {
    return _interactable;
  }

  public static Selectable? allSelectablesArray => _selectables.Count > 0 ? _selectables[0] : null;
  public static int allSelectableCount => _selectables.Count;

  public static List<Selectable> allSelectables => _selectables;

  public virtual void Select()
  {
    if (!_interactable || !IsActive())
    {
      return;
    }

    if (_currentSelection == this)
    {
      return;
    }

    if (_currentSelection is not null)
    {
      _currentSelection.OnDeselect(new BaseEventData(null));
    }

    _currentSelection = this;
    _hasSelection = true;
    OnSelect(new BaseEventData(null));
  }

  public virtual void SelectOnEnable()
  {
    if (!_interactable || !IsActive())
    {
      return;
    }

    if (!_selectables.Contains(this))
    {
      _selectables.Add(this);
    }
  }

  public virtual void OnSelect(BaseEventData? eventData)
  {
  }

  public virtual void OnDeselect(BaseEventData? eventData)
  {
    _hasSelection = false;
  }

  protected override void OnEnable()
  {
    base.OnEnable();
    OnPointerExit(null);
    SelectOnEnable();
  }

  protected override void OnDisable()
  {
    base.OnDisable();
    OnPointerExit(null);

    if (_currentSelection == this)
    {
      _currentSelection = null;
      _hasSelection = false;
    }

    _selectables.Remove(this);
  }

  protected virtual void OnSetProperty()
  {
    if (!IsActive() || !_interactable)
    {
      OnPointerExit(null);
    }
  }

  public virtual void OnPointerDown(PointerEventData? eventData)
  {
    if (eventData is null || eventData.button != PointerEventData.InputButton.Left)
    {
      return;
    }

    if (!_interactable || !IsActive())
    {
      return;
    }

    _isPointerDown = true;
    EvaluateAndTransitionToSelectionState();
  }

  public virtual void OnPointerUp(PointerEventData? eventData)
  {
    if (eventData is null || eventData.button != PointerEventData.InputButton.Left)
    {
      return;
    }

    if (!_interactable || !IsActive())
    {
      return;
    }

    _isPointerDown = false;
    EvaluateAndTransitionToSelectionState();
  }

  public virtual void OnPointerEnter(PointerEventData? eventData)
  {
    if (!_interactable || !IsActive())
    {
      return;
    }

    _isPointerInside = true;
    EvaluateAndTransitionToSelectionState();
  }

  public virtual void OnPointerExit(PointerEventData? eventData)
  {
    if (!_interactable || !IsActive())
    {
      return;
    }

    _isPointerInside = false;
    _isPointerDown = false;
    EvaluateAndTransitionToSelectionState();
  }

  public virtual void OnMove(AxisEventData? eventData)
  {
    if (eventData is null || !_interactable || !IsActive())
    {
      return;
    }

    var mode = _navigation.mode;
    if (mode == NavigationMode.None)
    {
      return;
    }

    Selectable? target = null;

    switch (eventData.moveDir)
    {
      case MoveDirection.Up:
        target = _navigation.selectOnUp;
        break;
      case MoveDirection.Down:
        target = _navigation.selectOnDown;
        break;
      case MoveDirection.Left:
        target = _navigation.selectOnLeft;
        break;
      case MoveDirection.Right:
        target = _navigation.selectOnRight;
        break;
    }

    if (target is not null && target.IsActive())
    {
      target.Select();
    }
  }

  public virtual void OnSubmit(BaseEventData? eventData)
  {
    if (!_interactable || !IsActive())
    {
      return;
    }

    _hasSubmitted = true;
    EvaluateAndTransitionToSelectionState();
  }

  public virtual void OnCancel(BaseEventData? eventData)
  {
  }

  public virtual void OnBeginDrag(PointerEventData eventData)
  {
  }

  public virtual void OnDrag(PointerEventData eventData)
  {
  }

  public virtual void OnEndDrag(PointerEventData eventData)
  {
  }

  public void EvaluateAndTransitionToSelectionState()
  {
    if (!IsActive() || !_interactable)
    {
      return;
    }

    DoStateTransition(GetSelectionState(), false);
  }

  protected virtual void DoStateTransition(SelectionState state, bool instant)
  {
    _ = state;
    _ = instant;
  }

  protected virtual SelectionState GetSelectionState()
  {
    if (!_interactable)
    {
      return SelectionState.Disabled;
    }

    if (_hasSelection)
    {
      return SelectionState.Selected;
    }

    if (_isPointerDown)
    {
      return SelectionState.Pressed;
    }

    if (_isPointerInside)
    {
      return SelectionState.Highlighted;
    }

    return SelectionState.Normal;
  }

  public virtual bool FindSelectable(Vector3 dir)
  {
    _ = dir;
    return false;
  }

  public virtual bool FindSelectableOnLeft()
  {
    return false;
  }

  public virtual bool FindSelectableOnRight()
  {
    return false;
  }

  public virtual bool FindSelectableOnUp()
  {
    return false;
  }

  public virtual bool FindSelectableOnDown()
  {
    return false;
  }
}

public enum SelectionState
{
  Normal,
  Highlighted,
  Pressed,
  Selected,
  Disabled
}

public struct Navigation
{
  public NavigationMode mode;
  public Selectable? selectOnUp;
  public Selectable? selectOnDown;
  public Selectable? selectOnLeft;
  public Selectable? selectOnRight;

  public static Navigation defaultNavigation => new() { mode = NavigationMode.Automatic };

  public static bool operator !=(Navigation a, Navigation b)
  {
    return !a.Equals(b);
  }

  public static bool operator ==(Navigation a, Navigation b)
  {
    return a.Equals(b);
  }
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

  public bool Equals(SpriteState other)
  {
    return highlightedSprite == other.highlightedSprite &&
           pressedSprite == other.pressedSprite &&
           selectedSprite == other.selectedSprite &&
           disabledSprite == other.disabledSprite;
  }
}

public class AnimationTriggers
{
  public string normalTrigger { get; set; } = string.Empty;
  public string highlightedTrigger { get; set; } = string.Empty;
  public string pressedTrigger { get; set; } = string.Empty;
  public string selectedTrigger { get; set; } = string.Empty;
  public string disabledTrigger { get; set; } = string.Empty;
}

public class PointerEventData : BaseEventData
{
  public enum InputButton
  {
    Left,
    Right,
    Middle
  }

  public Vector2 position { get; set; }
  public Vector2 delta { get; set; }
  public Vector2 pressPosition { get; set; }
  public Vector2 scrollDelta { get; set; }
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
  public InputButton button { get; set; }
  public bool useDragThreshold { get; set; } = true;
  public bool eligibilityForClick => eligibleForClick;

  public PointerEventData(EventSystem eventSystem) : base(eventSystem)
  {
  }
}

public class BaseEventData
{
  public Selectable? currentSelectedGameObject { get; set; }
  public EventSystem? eventSystem { get; }

  public BaseEventData(EventSystem? eventSystem)
  {
    this.eventSystem = eventSystem;
  }
}

public class AxisEventData : BaseEventData
{
  public Vector2 moveVector { get; set; }
  public MoveDirection moveDir { get; set; }

  public AxisEventData(EventSystem? eventSystem) : base(eventSystem)
  {
  }
}

public enum MoveDirection
{
  Left = 0,
  Up = 1,
  Right = 2,
  Down = 3,
  None = 4
}

public class EventSystem : MonoBehaviour
{
  private static EventSystem? _current;

  public static EventSystem? current
  {
    get => _current;
    set => _current = value;
  }

  public bool isApplicationActive { get; set; } = true;
  public bool isInputModuleDisabled { get; set; }

  public void SetSelectedGameObject(GameObject? selected)
  {
    _ = selected;
  }

  public void SetSelectedGameObject(GameObject? selected, BaseEventData? pointer)
  {
    _ = selected;
    _ = pointer;
  }

  public GameObject? currentSelectedGameObject { get; set; }

  public void Update()
  {
  }
}

public interface IBeginDragHandler
{
  void OnBeginDrag(PointerEventData eventData);
}

public interface IDragHandler
{
  void OnDrag(PointerEventData eventData);
}

public interface IEndDragHandler
{
  void OnEndDrag(PointerEventData eventData);
}

public interface IPointerClickHandler
{
  void OnPointerClick(PointerEventData eventData);
}

public interface IPointerDownHandler
{
  void OnPointerDown(PointerEventData eventData);
}

public interface IPointerUpHandler
{
  void OnPointerUp(PointerEventData eventData);
}

public interface IPointerEnterHandler
{
  void OnPointerEnter(PointerEventData eventData);
}

public interface IPointerExitHandler
{
  void OnPointerExit(PointerEventData eventData);
}

public interface ISelectHandler
{
  void OnSelect(BaseEventData eventData);
}

public interface IDeselectHandler
{
  void OnDeselect(BaseEventData eventData);
}

public interface ISubmitHandler
{
  void OnSubmit(BaseEventData eventData);
}

public interface ICancelHandler
{
  void OnCancel(BaseEventData eventData);
}

public interface IMoveHandler
{
  void OnMove(AxisEventData eventData);
}

public interface IScrollHandler
{
  void OnScroll(PointerEventData eventData);
}

public interface IUpdateSelectedHandler
{
  void OnUpdateSelected(BaseEventData eventData);
}

public interface IPointerHoverHandler
{
  void OnPointerHover(PointerEventData eventData);
}

public interface IDropHandler
{
  void OnDrop(PointerEventData eventData);
}
