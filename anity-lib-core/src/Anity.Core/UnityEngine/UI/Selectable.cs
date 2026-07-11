using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

public class Selectable : UIBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler, IMoveHandler, ISubmitHandler, ICancelHandler,
    IUpdateSelectedHandler
{
  private static readonly List<Selectable> _selectables = new();
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

  public virtual bool IsInteractable() => _interactable && isActiveAndEnabled;

  public static Selectable? allSelectablesArray => _selectables.Count > 0 ? _selectables[0] : null;
  public static int allSelectableCount => _selectables.Count;
  public static IReadOnlyList<Selectable> allSelectables => _selectables;

  public static Selectable? FindSelectable(Vector3 dir)
  {
    _ = dir;
    return null;
  }

  public virtual void Select()
  {
    if (!_interactable || !IsActive()) return;
    if (EventSystem.current is null) return;
    if (EventSystem.current.currentSelectedGameObject == gameObject) return;

    EventSystem.current.SetSelectedGameObject(gameObject);
  }

  public virtual void OnSelect(BaseEventData eventData)
  {
    _hasSelection = true;
    EvaluateAndTransitionToSelectionState();
  }

  public virtual void OnDeselect(BaseEventData eventData)
  {
    _hasSelection = false;
    EvaluateAndTransitionToSelectionState();
  }

  protected override void OnEnable()
  {
    base.OnEnable();
    _isPointerDown = false;
    _isPointerInside = false;
    _hasSubmitted = false;
    _hasSelection = false;

    if (!_selectables.Contains(this))
      _selectables.Add(this);

    EvaluateAndTransitionToSelectionState();
  }

  protected override void OnDisable()
  {
    base.OnDisable();
    _isPointerDown = false;
    _isPointerInside = false;
    _hasSubmitted = false;
    _hasSelection = false;
    _selectables.Remove(this);
    if (_currentSelection == this)
      _currentSelection = null;
    if (EventSystem.current?.currentSelectedGameObject == gameObject)
      EventSystem.current.SetSelectedGameObject(null);
  }

  protected virtual void OnSetProperty()
  {
    EvaluateAndTransitionToSelectionState();
  }

  public virtual void OnPointerDown(PointerEventData eventData)
  {
    if (eventData.button != PointerEventData.InputButton.Left) return;
    if (!IsInteractable()) return;
    _isPointerDown = true;
    EvaluateAndTransitionToSelectionState();
  }

  public virtual void OnPointerUp(PointerEventData eventData)
  {
    if (eventData.button != PointerEventData.InputButton.Left) return;
    if (!IsInteractable()) return;
    _isPointerDown = false;
    EvaluateAndTransitionToSelectionState();
  }

  public virtual void OnPointerEnter(PointerEventData eventData)
  {
    if (!IsInteractable()) return;
    _isPointerInside = true;
    EvaluateAndTransitionToSelectionState();
  }

  public virtual void OnPointerExit(PointerEventData eventData)
  {
    if (!IsInteractable()) return;
    _isPointerInside = false;
    _isPointerDown = false;
    EvaluateAndTransitionToSelectionState();
  }

  public virtual void OnMove(AxisEventData eventData)
  {
    if (!IsInteractable()) return;
    var mode = _navigation.mode;
    if (mode == NavigationMode.None) return;

    Selectable? target = eventData.moveDir switch
    {
      MoveDirection.Up => _navigation.selectOnUp,
      MoveDirection.Down => _navigation.selectOnDown,
      MoveDirection.Left => _navigation.selectOnLeft,
      MoveDirection.Right => _navigation.selectOnRight,
      _ => null
    };

    if (target is not null && target.IsActive())
      target.Select();
  }

  public virtual void OnSubmit(BaseEventData eventData)
  {
    if (!IsInteractable()) return;
    _hasSubmitted = true;
    EvaluateAndTransitionToSelectionState();
  }

  public virtual void OnCancel(BaseEventData eventData)
  {
  }

  public virtual void OnUpdateSelected(BaseEventData eventData)
  {
  }

  public void EvaluateAndTransitionToSelectionState()
  {
    if (!IsActive() || !_interactable) return;
    DoStateTransition(GetSelectionState(), false);
  }

  protected virtual void DoStateTransition(SelectionState state, bool instant)
  {
    _ = instant;
    if (_targetGraphic is null) return;

    var color = state switch
    {
      SelectionState.Disabled => _colors.disabledColor,
      SelectionState.Pressed => _colors.pressedColor,
      SelectionState.Selected => _colors.selectedColor,
      SelectionState.Highlighted => _colors.highlightedColor,
      _ => _colors.normalColor
    };

    color *= _colors.colorMultiplier;
    _targetGraphic.CrossFadeColor(color, _colors.fadeDuration, true, true);
  }

  protected virtual SelectionState GetSelectionState()
  {
    if (!_interactable) return SelectionState.Disabled;
    if (_hasSelection) return SelectionState.Selected;
    if (_isPointerDown) return SelectionState.Pressed;
    if (_isPointerInside) return SelectionState.Highlighted;
    return SelectionState.Normal;
  }

  public virtual Selectable? FindSelectableOnLeft() => _navigation.selectOnLeft;
  public virtual Selectable? FindSelectableOnRight() => _navigation.selectOnRight;
  public virtual Selectable? FindSelectableOnUp() => _navigation.selectOnUp;
  public virtual Selectable? FindSelectableOnDown() => _navigation.selectOnDown;

  protected virtual void InstantClearState()
  {
    _isPointerInside = false;
    _isPointerDown = false;
    _hasSelection = false;
    _hasSubmitted = false;
    DoStateTransition(SelectionState.Normal, true);
  }

  protected bool IsActive()
  {
    return gameObject is not null && gameObject.activeInHierarchy && enabled;
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

public struct Navigation : IEquatable<Navigation>
{
  public NavigationMode mode;
  public Selectable? selectOnUp;
  public Selectable? selectOnDown;
  public Selectable? selectOnLeft;
  public Selectable? selectOnRight;

  public static Navigation defaultNavigation => new() { mode = NavigationMode.Automatic };

  public bool Equals(Navigation other)
  {
    return mode == other.mode && selectOnUp == other.selectOnUp && selectOnDown == other.selectOnDown && selectOnLeft == other.selectOnLeft && selectOnRight == other.selectOnRight;
  }

  public override bool Equals(object? obj) => obj is Navigation other && Equals(other);
  public override int GetHashCode() => HashCode.Combine(mode, selectOnUp, selectOnDown, selectOnLeft, selectOnRight);
  public static bool operator ==(Navigation a, Navigation b) => a.Equals(b);
  public static bool operator !=(Navigation a, Navigation b) => !a.Equals(b);
}

public struct ColorBlock : IEquatable<ColorBlock>
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

  public bool Equals(ColorBlock other) => normalColor.Equals(other.normalColor) && highlightedColor.Equals(other.highlightedColor) && pressedColor.Equals(other.pressedColor) && selectedColor.Equals(other.selectedColor) && disabledColor.Equals(other.disabledColor) && colorMultiplier.Equals(other.colorMultiplier) && fadeDuration.Equals(other.fadeDuration);
  public override bool Equals(object? obj) => obj is ColorBlock other && Equals(other);
  public override int GetHashCode() => HashCode.Combine(normalColor, highlightedColor, pressedColor, selectedColor, disabledColor, colorMultiplier, fadeDuration);
}

public struct SpriteState : IEquatable<SpriteState>
{
  public Sprite? highlightedSprite;
  public Sprite? pressedSprite;
  public Sprite? selectedSprite;
  public Sprite? disabledSprite;

  public bool Equals(SpriteState other) => highlightedSprite == other.highlightedSprite && pressedSprite == other.pressedSprite && selectedSprite == other.selectedSprite && disabledSprite == other.disabledSprite;
  public override bool Equals(object? obj) => obj is SpriteState other && Equals(other);
  public override int GetHashCode() => HashCode.Combine(highlightedSprite, pressedSprite, selectedSprite, disabledSprite);
}

public class AnimationTriggers
{
  public string normalTrigger { get; set; } = "Normal";
  public string highlightedTrigger { get; set; } = "Highlighted";
  public string pressedTrigger { get; set; } = "Pressed";
  public string selectedTrigger { get; set; } = "Selected";
  public string disabledTrigger { get; set; } = "Disabled";
}
