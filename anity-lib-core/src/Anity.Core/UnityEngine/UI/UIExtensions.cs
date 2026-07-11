using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

public class Slider : Selectable
{
    private float _value;
    private float _minValue;
    private float _maxValue = 1f;
    private bool _wholeNumbers;
    private Slider.Direction _direction = Direction.LeftToRight;
    private RectTransform? _fillRect;
    private RectTransform? _handleRect;

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

    public RectTransform? fillRect
    {
        get => _fillRect;
        set => _fillRect = value;
    }

    public RectTransform? handleRect
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

public class Toggle : Selectable, IPointerClickHandler
{
    private bool _isOn;
    private Toggle.ToggleTransition _toggleTransition = ToggleTransition.Fade;
    private Graphic? _graphic;
    private ToggleGroup? _group;

    public enum ToggleTransition
    {
        None,
        Fade
    }

    public bool isOn
    {
        get => _isOn;
        set => Set(value);
    }

    public Toggle.ToggleTransition toggleTransition
    {
        get => _toggleTransition;
        set => _toggleTransition = value;
    }

    public Graphic? graphic
    {
        get => _graphic;
        set => _graphic = value;
    }

    public ToggleGroup? group
    {
        get => _group;
        set => _group = value;
    }

    public Toggle.ToggleEvent onValueChanged { get; set; } = new Toggle.ToggleEvent();

    private void Set(bool value, bool sendCallback = true)
    {
        if (_isOn == value) return;
        _isOn = value;
        if (_group is not null && value)
            _group.NotifyToggleOn(this);
        if (sendCallback)
            onValueChanged?.Invoke(_isOn);
        PlayEffect();
    }

    private void PlayEffect()
    {
        if (_graphic is null) return;
        _graphic.CrossFadeAlpha(_isOn ? 1f : 0f, 0.1f, true);
    }

    public virtual void SetIsOnWithoutNotify(bool value)
    {
        _isOn = value;
    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (!IsInteractable() || !IsActive()) return;
        Set(!_isOn);
    }

    public class ToggleEvent : UnityEngine.Events.UnityEvent<bool> { }
}

public class ToggleGroup : UIBehaviour
{
    private readonly List<Toggle> _toggles = new();
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
                t.SetIsOnWithoutNotify(false);
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
        var old = _allowSwitchOff;
        _allowSwitchOff = true;
        foreach (var t in _toggles)
            t.SetIsOnWithoutNotify(false);
        _allowSwitchOff = old;
    }
}
