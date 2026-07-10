using System;

namespace UnityEngine.UI;

public class Scrollbar : Selectable, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum Direction
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    private RectTransform? _handleRect;
    private Direction _direction = Direction.LeftToRight;
    private float _value;
    private float _size = 0.2f;
    private int _numberOfSteps;

    private ScrollEvent _onValueChanged = new();

    public RectTransform? handleRect
    {
        get => _handleRect;
        set => _handleRect = value;
    }

    public Direction direction
    {
        get => _direction;
        set => _direction = value;
    }

    public float value
    {
        get => _value;
        set => Set(Mathf.Clamp01(value));
    }

    public float size
    {
        get => _size;
        set => _size = Mathf.Clamp01(value);
    }

    public int numberOfSteps
    {
        get => _numberOfSteps;
        set => _numberOfSteps = Math.Max(0, value);
    }

    public ScrollEvent onValueChanged
    {
        get => _onValueChanged;
        set => _onValueChanged = value;
    }

    public void SetValueWithoutNotify(float input)
    {
        _value = Mathf.Clamp01(input);
        UpdateVisuals();
    }

    private void Set(float input)
    {
        var newValue = input;
        if (_numberOfSteps > 1)
        {
            newValue = Mathf.Round(newValue * (_numberOfSteps - 1)) / (_numberOfSteps - 1);
        }

        if (Mathf.Abs(_value - newValue) > float.Epsilon)
        {
            _value = newValue;
            UpdateVisuals();
            _onValueChanged?.Invoke(_value);
        }
    }

    private void UpdateVisuals()
    {
        if (_handleRect is null) return;
        var anchorMin = Vector2.zero;
        var anchorMax = Vector2.one;

        switch (_direction)
        {
            case Direction.LeftToRight:
                anchorMin.x = _value * (1f - _size);
                anchorMax.x = anchorMin.x + _size;
                break;
            case Direction.RightToLeft:
                anchorMax.x = 1f - _value * (1f - _size);
                anchorMin.x = anchorMax.x - _size;
                break;
            case Direction.BottomToTop:
                anchorMin.y = _value * (1f - _size);
                anchorMax.y = anchorMin.y + _size;
                break;
            case Direction.TopToBottom:
                anchorMax.y = 1f - _value * (1f - _size);
                anchorMin.y = anchorMax.y - _size;
                break;
        }

        _handleRect.anchorMin = anchorMin;
        _handleRect.anchorMax = anchorMax;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _ = eventData;
    }

    public void OnDrag(PointerEventData eventData)
    {
        _ = eventData;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _ = eventData;
    }
}

[Serializable]
public class ScrollEvent
{
    public event Action<float>? ValueChanged;

    public void Invoke(float value)
    {
        ValueChanged?.Invoke(value);
    }
}
