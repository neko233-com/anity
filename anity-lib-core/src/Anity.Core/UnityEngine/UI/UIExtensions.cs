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

public interface IEventSystemHandler { }
public interface IPointerMoveHandler : IEventSystemHandler { void OnPointerMove(PointerEventData eventData); }
public interface IPointerOverHandler : IEventSystemHandler { }

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

public abstract class PointerInputModule : BaseInputModule
{
    public enum MouseButton
    {
        Left = 0,
        Right = 1,
        Middle = 2,
    }
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
