using System;
using System.Collections.Generic;

namespace UnityEngine.UI;

public class Slider : Selectable
{
    private float _value;
    private float _minValue;
    private float _maxValue = 1f;
    private bool _wholeNumbers;
    private Slider.Direction _direction = Direction.LeftToRight;
    private RectTransform _fillRect;
    private RectTransform _handleRect;

    public enum Direction
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    public float minValue
    {
        get => _minValue;
        set { _minValue = value; ClampValue(); }
    }

    public float maxValue
    {
        get => _maxValue;
        set { _maxValue = value; ClampValue(); }
    }

    public bool wholeNumbers
    {
        get => _wholeNumbers;
        set { _wholeNumbers = value; ClampValue(); }
    }

    public float normalizedValue
    {
        get => Mathf.Approximately(minValue, maxValue) ? 0f : Mathf.InverseLerp(minValue, maxValue, value);
        set => this.value = Mathf.Lerp(minValue, maxValue, value);
    }

    public float value
    {
        get => _value;
        set
        {
            _value = ClampValue(value);
            onValueChanged?.Invoke(_value);
        }
    }

    public Direction direction
    {
        get => _direction;
        set => _direction = value;
    }

    public RectTransform fillRect
    {
        get => _fillRect;
        set => _fillRect = value;
    }

    public RectTransform handleRect
    {
        get => _handleRect;
        set => _handleRect = value;
    }

    public Slider.SliderEvent onValueChanged { get; set; } = new Slider.SliderEvent();

    private float ClampValue(float input)
    {
        float newValue = input;
        if (wholeNumbers)
            newValue = Mathf.Round(newValue);
        return Mathf.Clamp(newValue, minValue, maxValue);
    }

    private void ClampValue()
    {
        _value = ClampValue(_value);
    }

    public virtual void SetValueWithoutNotify(float input)
    {
        _value = ClampValue(input);
    }

    public class SliderEvent : UnityEngine.Events.UnityEvent<float> { }
}

public class Toggle : Selectable
{
    private bool _isOn;
    private Toggle.ToggleTransition _toggleTransition = ToggleTransition.Fade;
    private Graphic _graphic;
    private ToggleGroup _group;

    public enum ToggleTransition
    {
        None,
        Fade
    }

    public bool isOn
    {
        get => _isOn;
        set
        {
            Set(value);
        }
    }

    public Toggle.ToggleTransition toggleTransition
    {
        get => _toggleTransition;
        set => _toggleTransition = value;
    }

    public Graphic graphic
    {
        get => _graphic;
        set => _graphic = value;
    }

    public ToggleGroup group
    {
        get => _group;
        set => _group = value;
    }

    public Toggle.ToggleEvent onValueChanged { get; set; } = new Toggle.ToggleEvent();

    private void Set(bool value)
    {
        if (_isOn == value) return;
        _isOn = value;
        onValueChanged?.Invoke(_isOn);
    }

    public virtual void SetIsOnWithoutNotify(bool value)
    {
        _isOn = value;
    }

    public class ToggleEvent : UnityEngine.Events.UnityEvent<bool> { }
}

public class ToggleGroup : UIBehaviour
{
    private List<Toggle> _toggles = new List<Toggle>();
    private bool _allowSwitchOff;

    public bool allowSwitchOff
    {
        get => _allowSwitchOff;
        set => _allowSwitchOff = value;
    }

    public IEnumerable<Toggle> ActiveToggles()
    {
        foreach (var t in _toggles)
        {
            if (t.isOn) yield return t;
        }
    }

    public bool AnyTogglesOn()
    {
        foreach (var t in _toggles)
        {
            if (t.isOn) return true;
        }
        return false;
    }

    public void NotifyToggleOn(Toggle toggle)
    {
        foreach (var t in _toggles)
        {
            if (t != toggle)
                t.isOn = false;
        }
    }

    public void UnregisterToggle(Toggle toggle)
    {
        _toggles.Remove(toggle);
    }

    public void RegisterToggle(Toggle toggle)
    {
        if (!_toggles.Contains(toggle))
            _toggles.Add(toggle);
    }

    public void SetAllTogglesOff()
    {
        bool oldAllowSwitchOff = _allowSwitchOff;
        _allowSwitchOff = true;
        foreach (var t in _toggles)
            t.isOn = false;
        _allowSwitchOff = oldAllowSwitchOff;
    }

    internal void EnsureValidState()
    {
    }
}

public class Scrollbar : Selectable
{
    private RectTransform _handleRect;
    private float _size = 0.2f;
    private float _value;
    private Scrollbar.Direction _direction = Direction.LeftToRight;
    private int _numberOfSteps = 0;

    public enum Direction
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    public RectTransform handleRect
    {
        get => _handleRect;
        set => _handleRect = value;
    }

    public float size
    {
        get => _size;
        set => _size = Mathf.Clamp01(value);
    }

    public float value
    {
        get => _value;
        set
        {
            _value = Mathf.Clamp01(value);
            onValueChanged?.Invoke(_value);
        }
    }

    public Direction direction
    {
        get => _direction;
        set => _direction = value;
    }

    public int numberOfSteps
    {
        get => _numberOfSteps;
        set => _numberOfSteps = value;
    }

    public Scrollbar.ScrollEvent onValueChanged { get; set; } = new Scrollbar.ScrollEvent();

    public class ScrollEvent : UnityEngine.Events.UnityEvent<float> { }
}

public class ScrollRect : UIBehaviour
{
    private RectTransform _content;
    private RectTransform _viewport;
    private Scrollbar _horizontalScrollbar;
    private Scrollbar _verticalScrollbar;
    private Vector2 _normalizedPosition = Vector2.zero;
    private bool _horizontal = true;
    private bool _vertical = true;
    private ScrollRect.MovementType _movementType = MovementType.Elastic;
    private float _elasticity = 0.1f;
    private bool _inertia = true;
    private float _decelerationRate = 0.135f;
    private float _scrollSensitivity = 1f;

    public enum MovementType
    {
        Unrestricted,
        Elastic,
        Clamped
    }

    public enum ScrollbarVisibility
    {
        Permanent,
        AutoHide,
        AutoHideAndExpandViewport
    }

    public RectTransform content
    {
        get => _content;
        set => _content = value;
    }

    public RectTransform viewport
    {
        get => _viewport;
        set => _viewport = value;
    }

    public bool horizontal
    {
        get => _horizontal;
        set => _horizontal = value;
    }

    public bool vertical
    {
        get => _vertical;
        set => _vertical = value;
    }

    public ScrollRect.MovementType movementType
    {
        get => _movementType;
        set => _movementType = value;
    }

    public float elasticity
    {
        get => _elasticity;
        set => _elasticity = value;
    }

    public bool inertia
    {
        get => _inertia;
        set => _inertia = value;
    }

    public float decelerationRate
    {
        get => _decelerationRate;
        set => _decelerationRate = value;
    }

    public float scrollSensitivity
    {
        get => _scrollSensitivity;
        set => _scrollSensitivity = value;
    }

    public Vector2 normalizedPosition
    {
        get => _normalizedPosition;
        set => _normalizedPosition = value;
    }

    public float horizontalNormalizedPosition
    {
        get => _normalizedPosition.x;
        set => _normalizedPosition.x = value;
    }

    public float verticalNormalizedPosition
    {
        get => _normalizedPosition.y;
        set => _normalizedPosition.y = value;
    }

    public Scrollbar horizontalScrollbar
    {
        get => _horizontalScrollbar;
        set => _horizontalScrollbar = value;
    }

    public Scrollbar verticalScrollbar
    {
        get => _verticalScrollbar;
        set => _verticalScrollbar = value;
    }

    public ScrollRect.ScrollRectEvent onValueChanged { get; set; } = new ScrollRect.ScrollRectEvent();

    public void StopMovement()
    {
    }

    public void SetNormalizedPosition(float value, int axis)
    {
    }

    public class ScrollRectEvent : UnityEngine.Events.UnityEvent<Vector2> { }
}

public class Dropdown : Selectable
{
    private int _value;
    private List<OptionData> _options = new List<OptionData>();
    private Text _captionText;
    private Image _captionImage;
    private GameObject _template;
    private RectTransform _dropdownList;
    private int _itemTextSize = 14;
    private Color _itemTextColor = Color.white;

    public int value
    {
        get => _value;
        set
        {
            _value = value;
            onValueChanged?.Invoke(value);
        }
    }

    public List<OptionData> options
    {
        get => _options;
        set => _options = value ?? new List<OptionData>();
    }

    public Text captionText
    {
        get => _captionText;
        set => _captionText = value;
    }

    public Image captionImage
    {
        get => _captionImage;
        set => _captionImage = value;
    }

    public GameObject template
    {
        get => _template;
        set => _template = value;
    }

    public RectTransform dropdownList
    {
        get => _dropdownList;
        set => _dropdownList = value;
    }

    public int itemTextSize
    {
        get => _itemTextSize;
        set => _itemTextSize = value;
    }

    public Color itemTextColor
    {
        get => _itemTextColor;
        set => _itemTextColor = value;
    }

    public Dropdown.DropdownEvent onValueChanged { get; set; } = new Dropdown.DropdownEvent();

    public void Show() { }
    public void Hide() { }
    public void RefreshShownValue() { }

    public void AddOptions(List<string> options)
    {
        if (options == null) return;
        foreach (var opt in options)
            _options.Add(new OptionData(opt));
    }

    public void AddOptions(List<OptionData> options)
    {
        if (options == null) return;
        _options.AddRange(options);
    }

    public void AddOptions(List<Sprite> options)
    {
        if (options == null) return;
        foreach (var opt in options)
            _options.Add(new OptionData(opt));
    }

    public void ClearOptions()
    {
        _options.Clear();
    }

    public class OptionData
    {
        public string text { get; set; }
        public Sprite image { get; set; }

        public OptionData() { }
        public OptionData(string text) { this.text = text; }
        public OptionData(Sprite image) { this.image = image; }
        public OptionData(string text, Sprite image) { this.text = text; this.image = image; }
    }

    public class DropdownEvent : UnityEngine.Events.UnityEvent<int> { }
}

public class InputField : Selectable
{
    private string _text = string.Empty;
    private Text _textComponent;
    private int _characterLimit = 0;
    private ContentType _contentType = ContentType.Standard;
    private LineType _lineType = LineType.SingleLine;
    private InputType _inputType = InputType.Standard;
    private bool _readOnly;
    private bool _multiLine;

    public enum ContentType
    {
        Standard,
        Autocorrected,
        IntegerNumber,
        DecimalNumber,
        Alphanumeric,
        Name,
        EmailAddress,
        Password,
        Pin,
        Custom
    }

    public enum InputType
    {
        Standard,
        AutoCorrect,
        Password,
    }

    public enum LineType
    {
        SingleLine,
        MultiLineSubmit,
        MultiLineNewline
    }

    public enum CharacterValidation
    {
        None,
        Digit,
        Integer,
        Decimal,
        Alphanumeric,
        Name,
        EmailAddress,
        Custom
    }

    public string text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            onValueChanged?.Invoke(_text);
        }
    }

    public Text textComponent
    {
        get => _textComponent;
        set => _textComponent = value;
    }

    public int characterLimit
    {
        get => _characterLimit;
        set => _characterLimit = value;
    }

    public ContentType contentType
    {
        get => _contentType;
        set => _contentType = value;
    }

    public LineType lineType
    {
        get => _lineType;
        set
        {
            _lineType = value;
            _multiLine = value != LineType.SingleLine;
        }
    }

    public InputType inputType
    {
        get => _inputType;
        set => _inputType = value;
    }

    public bool readOnly
    {
        get => _readOnly;
        set => _readOnly = value;
    }

    public bool multiLine
    {
        get => _multiLine;
        set => _multiLine = value;
    }

    public InputField.SubmitEvent onSubmit { get; set; } = new InputField.SubmitEvent();
    public InputField.OnChangeEvent onValueChanged { get; set; } = new InputField.OnChangeEvent();
    public InputField.EndEditEvent onEndEdit { get; set; } = new InputField.EndEditEvent();

    public void ActivateInputField() { }
    public void DeactivateInputField() { }
    public void MoveTextEnd(bool shift) { }
    public void MoveTextStart(bool shift) { }
    public void ForceLabelUpdate() { }
    public void Rebuild(CanvasUpdate update) { }

    public class SubmitEvent : UnityEngine.Events.UnityEvent<string> { }
    public class OnChangeEvent : UnityEngine.Events.UnityEvent<string> { }
    public class EndEditEvent : UnityEngine.Events.UnityEvent<string> { }
}

public class GraphicRaycaster : UIBehaviour
{
    public enum BlockingObjects
    {
        None = 0,
        TwoD = 1,
        ThreeD = 2,
        All = 3,
    }

    public Canvas canvas { get; }
    public bool ignoreReversedGraphics { get; set; } = true;
    public BlockingObjects blockingObjects { get; set; } = BlockingObjects.None;
    public int blockingMask { get; set; } = ~0;
    public float maxRayDistance { get; set; }

    public GraphicRaycaster()
    {
    }
}

public class EventSystem : MonoBehaviour
{
    private static EventSystem _current;
    private GameObject _selectedGameObject;
    private BaseInputModule _currentInputModule;
    private bool _sendNavigationEvents = true;
    private int _pixelDragThreshold = 5;

    public static EventSystem current
    {
        get => _current;
        set => _current = value;
    }

    public GameObject currentSelectedGameObject
    {
        get => _selectedGameObject;
        set => _selectedGameObject = value;
    }

    public GameObject firstSelectedGameObject
    {
        get => _selectedGameObject;
        set => _selectedGameObject = value;
    }

    public BaseInputModule currentInputModule
    {
        get => _currentInputModule;
        set => _currentInputModule = value;
    }

    public bool sendNavigationEvents
    {
        get => _sendNavigationEvents;
        set => _sendNavigationEvents = value;
    }

    public int pixelDragThreshold
    {
        get => _pixelDragThreshold;
        set => _pixelDragThreshold = value;
    }

    public bool alreadySendUpdate { get; set; }

    public void SetSelectedGameObject(GameObject selected)
    {
        _selectedGameObject = selected;
    }

    public void SetSelectedGameObject(GameObject selected, BaseEventData pointer)
    {
        _ = pointer;
        _selectedGameObject = selected;
    }

    public bool IsPointerOverGameObject() => false;
    public bool IsPointerOverGameObject(int pointerId) => false;

    public void UpdateModules() { }
    public void SetSelectedGameObject(GameObject selected, PointerEventData pointer) { _ = pointer; _selectedGameObject = selected; }
}

public class StandaloneInputModule : PointerInputModule
{
    public string horizontalAxis { get; set; } = "Horizontal";
    public string verticalAxis { get; set; } = "Vertical";
    public string submitButton { get; set; } = "Submit";
    public string cancelButton { get; set; } = "Cancel";
    public string inputActionsAsset { get; set; }
    public float inputActionsPerSecond { get; set; } = 10f;
    public float repeatDelay { get; set; } = 0.5f;
    public float moveRepeatRate { get; set; } = 0.1f;
}

public abstract class PointerInputModule : BaseInputModule
{
    public enum MouseButton
    {
        Left = 0,
        Right = 1,
        Middle = 2,
    }
}

public abstract class BaseInputModule : UIBehaviour
{
    public virtual bool IsModuleSupported() => true;
    public virtual bool ShouldActivateModule() => false;
    public virtual void ActivateModule() { }
    public virtual void DeactivateModule() { }
    public virtual void UpdateModule() { }
    public virtual bool IsPointerOverGameObject(int pointerId) => false;
    public virtual GameObject FindCommonRoot(GameObject g1, GameObject g2) => null;
    public virtual void HandlePointerExitAndEnter(PointerEventData currentPointerData, GameObject newEnterTarget) { }
}

public class BaseEventData : IEventSystemHandler
{
    public BaseEventData(EventSystem eventSystem)
    {
        currentInputModule = null;
    }

    public BaseInputModule currentInputModule { get; set; }
    public bool used { get; set; }
    public void Use() { used = true; }
    public void Reset() { used = false; }
}

public class PointerEventData : BaseEventData
{
    public enum InputButton
    {
        Left = 0,
        Right = 1,
        Middle = 2,
    }

    public enum FramePressState
    {
        Pressed,
        Released,
        PressedAndReleased,
        NotChanged,
    }

    public PointerEventData(EventSystem eventSystem) : base(eventSystem)
    {
    }

    public GameObject pointerEnter { get; set; }
    public GameObject pointerPress { get; set; }
    public GameObject pointerDrag { get; set; }
    public GameObject lastPress { get; set; }
    public GameObject rawPointerPress { get; set; }
    public GameObject hovered { get; set; }
    public Vector2 position { get; set; }
    public Vector2 delta { get; set; }
    public Vector2 pressPosition { get; set; }
    public Vector3 worldPosition { get; set; }
    public Vector3 worldNormal { get; set; }
    public int pointerId { get; set; }
    public int clickCount { get; set; }
    public float clickTime { get; set; }
    public InputButton button { get; set; }
    public float scrollDelta { get; set; }
    public bool eligibleForClick { get; set; }
    public bool dragging { get; set; }
    public bool useDragThreshold { get; set; }
    public bool press { get; set; }
    public bool rawPointerPress { get; set; }
}

public interface IEventSystemHandler { }
public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData eventData); }
public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData eventData); }
public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData eventData); }
public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData eventData); }
public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData eventData); }
public interface IPointerMoveHandler : IEventSystemHandler { void OnPointerMove(PointerEventData eventData); }
public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData eventData); }
public interface IBeginDragHandler : IEventSystemHandler { void OnBeginDrag(PointerEventData eventData); }
public interface IEndDragHandler : IEventSystemHandler { void OnEndDrag(PointerEventData eventData); }
public interface IDropHandler : IEventSystemHandler { void OnDrop(PointerEventData eventData); }
public interface ISubmitHandler : IEventSystemHandler { void OnSubmit(BaseEventData eventData); }
public interface ICancelHandler : IEventSystemHandler { void OnCancel(BaseEventData eventData); }
public interface ISelectHandler : IEventSystemHandler { void OnSelect(BaseEventData eventData); }
public interface IDeselectHandler : IEventSystemHandler { void OnDeselect(BaseEventData eventData); }
public interface IMoveHandler : IEventSystemHandler { void OnMove(AxisEventData eventData); }
public interface IScrollHandler : IEventSystemHandler { void OnScroll(PointerEventData eventData); }
public interface IUpdateSelectedHandler : IEventSystemHandler { void OnUpdateSelected(BaseEventData eventData); }
public interface IPointerOverHandler : IEventSystemHandler { }

public class AxisEventData : BaseEventData
{
    public AxisEventData(EventSystem eventSystem) : base(eventSystem) { }
    public Vector2 moveVector { get; set; }
    public MoveDirection moveDir { get; set; }
}

public enum MoveDirection
{
    None,
    Left,
    Up,
    Right,
    Down
}

public enum CanvasUpdate
{
    Prelayout = 0,
    Layout = 1,
    PostLayout = 2,
    PreRender = 3,
    LatePreRender = 4,
    MaxUpdateValue = 5,
}