using System;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

[AddComponentMenu("UI/Slider")]
public class Slider : Selectable, IDragHandler, IInitializePotentialDragHandler, IPointerDownHandler, IPointerUpHandler, ICanvasElement
{
    public enum Direction
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    [Serializable]
    public class SliderEvent : UnityEngine.Events.UnityEvent<float>
    {
    }

    private float _value;
    private float _minValue;
    private float _maxValue = 1f;
    private bool _wholeNumbers;
    private Direction _direction = Direction.LeftToRight;
    private SliderEvent _onValueChanged = new();

    public RectTransform fillRect;
    public RectTransform handleRect;

    public float value
    {
        get => _value;
        set
        {
            value = Mathf.Clamp(value, _minValue, _maxValue);
            if (_wholeNumbers)
                value = Mathf.Round(value);
            if (Mathf.Approximately(_value, value)) return;
            _value = value;
            _onValueChanged?.Invoke(_value);
            UpdateVisuals();
        }
    }

    public float minValue
    {
        get => _minValue;
        set
        {
            _minValue = value;
            this.value = _value;
        }
    }

    public float maxValue
    {
        get => _maxValue;
        set
        {
            _maxValue = value;
            this.value = _value;
        }
    }

    public bool wholeNumbers
    {
        get => _wholeNumbers;
        set
        {
            _wholeNumbers = value;
            this.value = _value;
        }
    }

    public Direction direction
    {
        get => _direction;
        set => _direction = value;
    }

    public float normalizedValue
    {
        get => Mathf.Approximately(_minValue, _maxValue) ? 0f : Mathf.InverseLerp(_minValue, _maxValue, _value);
        set => this.value = Mathf.Lerp(_minValue, _maxValue, value);
    }

    public SliderEvent onValueChanged
    {
        get => _onValueChanged;
        set => _onValueChanged = value;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        UpdateVisuals();
    }

    public virtual void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        UpdateDrag(eventData);
    }

    public virtual void OnPointerUp(PointerEventData eventData)
    {
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        UpdateDrag(eventData);
    }

    public virtual void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public virtual void Rebuild(CanvasUpdate executing)
    {
        if (executing == CanvasUpdate.Prelayout || executing == CanvasUpdate.PostLayout)
        {
            UpdateVisuals();
        }
    }

    public virtual void LayoutComplete() { }
    public virtual void GraphicUpdateComplete() { }

    private void UpdateDrag(PointerEventData eventData)
    {
        if (fillRect == null && handleRect == null) return;
        var rectTransform = this.rectTransform;
        if (rectTransform == null) return;

        var localPoint = Vector2.zero;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            var axis = (_direction == Direction.LeftToRight || _direction == Direction.RightToLeft) ? 0 : 1;
            var size = axis == 0 ? rectTransform.rect.width : rectTransform.rect.height;
            var normalizedPos = axis == 0
                ? localPoint.x / size + 0.5f
                : localPoint.y / size + 0.5f;

            if (_direction == Direction.RightToLeft || _direction == Direction.TopToBottom)
                normalizedPos = 1f - normalizedPos;

            normalizedValue = normalizedPos;
        }
    }

    private void UpdateVisuals()
    {
        var offset = normalizedValue;
        if (fillRect != null)
        {
            if (_direction == Direction.LeftToRight)
                fillRect.anchorMax = new Vector2(offset, fillRect.anchorMax.y);
            else if (_direction == Direction.RightToLeft)
                fillRect.anchorMin = new Vector2(1f - offset, fillRect.anchorMin.y);
            else if (_direction == Direction.BottomToTop)
                fillRect.anchorMax = new Vector2(fillRect.anchorMax.x, offset);
            else if (_direction == Direction.TopToBottom)
                fillRect.anchorMin = new Vector2(fillRect.anchorMin.x, 1f - offset);
        }

        if (handleRect != null)
        {
            var anchorPos = offset;
            if (_direction == Direction.RightToLeft || _direction == Direction.TopToBottom)
                anchorPos = 1f - offset;
            handleRect.anchorMin = new Vector2(anchorPos, handleRect.anchorMin.y);
            handleRect.anchorMax = new Vector2(anchorPos, handleRect.anchorMax.y);
        }
    }

    public void SetDirection(Direction direction, bool includeRectLayouts)
    {
        _direction = direction;
        if (includeRectLayouts)
            UpdateVisuals();
    }
}
