using System;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

[AddComponentMenu("UI/Scrollbar", 34)]
[RequireComponent(typeof(RectTransform))]
public class Scrollbar : Selectable, IBeginDragHandler, IDragHandler, IEndDragHandler, ICanvasElement
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
    private RectTransform? _containerRect;
    private Vector2 _offset;
    private bool _delayUpdateVisuals;

    private ScrollEvent _onValueChanged = new();

    public RectTransform? handleRect
    {
        get => _handleRect;
        set
        {
            _handleRect = value;
            UpdateCachedReferences();
            UpdateVisuals();
        }
    }

    public Direction direction
    {
        get => _direction;
        set
        {
            if (_direction != value)
            {
                _direction = value;
                UpdateVisuals();
            }
        }
    }

    public float value
    {
        get => _value;
        set => Set(Mathf.Clamp01(value));
    }

    public float size
    {
        get => _size;
        set
        {
            _size = Mathf.Clamp01(value);
            UpdateVisuals();
        }
    }

    public int numberOfSteps
    {
        get => _numberOfSteps;
        set
        {
            _numberOfSteps = Math.Max(0, value);
            Set(_value);
        }
    }

    public ScrollEvent onValueChanged
    {
        get => _onValueChanged;
        set => _onValueChanged = value;
    }

    public RectTransform rectTransform => transform as RectTransform;

    private void UpdateCachedReferences()
    {
        if (_handleRect != null && _handleRect.parent != null)
        {
            _containerRect = _handleRect.parent as RectTransform;
        }
        else
        {
            _containerRect = null;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        UpdateCachedReferences();
        Set(_value, false);
        _delayUpdateVisuals = true;
        CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);
    }

    protected override void OnDisable()
    {
        CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
        base.OnDisable();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (IsActive()) UpdateVisuals();
    }

    protected override void OnDidApplyAnimationProperties()
    {
        Set(_value, false);
        UpdateVisuals();
    }

    public void SetValueWithoutNotify(float input)
    {
        Set(Mathf.Clamp01(input), false);
    }

    private void Set(float input, bool sendCallback = true)
    {
        var newValue = Mathf.Clamp01(input);
        if (_numberOfSteps > 1)
        {
            newValue = Mathf.Round(newValue * (_numberOfSteps - 1)) / (_numberOfSteps - 1);
        }

        if (Mathf.Abs(_value - newValue) > 1e-6f)
        {
            _value = newValue;
            UpdateVisuals();
            if (sendCallback)
            {
                _onValueChanged?.Invoke(_value);
            }
        }
    }

    public virtual void Rebuild(CanvasUpdate executing)
    {
        if (executing == CanvasUpdate.PreRender)
        {
            if (_delayUpdateVisuals)
            {
                _delayUpdateVisuals = false;
                UpdateVisuals();
            }
        }
    }

    public virtual void LayoutComplete() { }
    public virtual void GraphicUpdateComplete() { }

    private void UpdateVisuals()
    {
        if (!Application.isPlaying) UpdateCachedReferences();
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
        _handleRect.sizeDelta = Vector2.zero;
    }

    public void SetDirection(Direction direction, bool includeRectLayouts)
    {
        var oldDir = _direction;
        _direction = direction;
        if (includeRectLayouts)
        {
            if (axis == Axis.Horizontal)
                RectTransformUtility.FlipLayoutAxes(rectTransform, true, true);
            if ((oldDir == Direction.LeftToRight && direction == Direction.RightToLeft) ||
                (oldDir == Direction.RightToLeft && direction == Direction.LeftToRight) ||
                (oldDir == Direction.TopToBottom && direction == Direction.BottomToTop) ||
                (oldDir == Direction.BottomToTop && direction == Direction.TopToBottom))
            {
                RectTransformUtility.FlipLayoutOnAxis(rectTransform, (int)axis, true, true);
            }
        }
        UpdateVisuals();
    }

    private Axis axis => _direction == Direction.LeftToRight || _direction == Direction.RightToLeft ? Axis.Horizontal : Axis.Vertical;
    private bool reverseValue => _direction == Direction.RightToLeft || _direction == Direction.TopToBottom;

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (!IsActive() || !IsInteractable()) return;
        base.OnPointerDown(eventData);

        UpdateCachedReferences();
        if (_containerRect is null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_containerRect, eventData.position, eventData.pressEventCamera, out var localPoint)) return;

        var localPos = localPoint - _containerRect.rect.position;
        var handleCenter = _handleRect != null ? (Vector2)_handleRect.localPosition + _containerRect.rect.center : Vector2.zero;
        float val;
        if (axis == Axis.Horizontal)
        {
            var total = _containerRect.rect.width * (1f - _size);
            val = total <= 0f ? 0f : Mathf.Clamp01((localPos.x - _containerRect.rect.width * _size * 0.5f) / total);
        }
        else
        {
            var total = _containerRect.rect.height * (1f - _size);
            val = total <= 0f ? 0f : Mathf.Clamp01((localPos.y - _containerRect.rect.height * _size * 0.5f) / total);
        }
        if (reverseValue) val = 1f - val;
        value = val;
    }

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        _ = eventData;
        if (!IsActive() || !IsInteractable()) return;
        UpdateCachedReferences();
        if (_handleRect != null)
        {
            _offset = _handleRect.localPosition;
        }
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (!IsActive() || !IsInteractable()) return;
        if (_containerRect is null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_containerRect, eventData.position, eventData.pressEventCamera, out var localPos)) return;

        float val;
        if (axis == Axis.Horizontal)
        {
            var total = _containerRect.rect.width * (1f - _size);
            val = total <= 0f ? 0f : Mathf.Clamp01((localPos.x - _offset.x) / total);
        }
        else
        {
            var total = _containerRect.rect.height * (1f - _size);
            val = total <= 0f ? 0f : Mathf.Clamp01((localPos.y - _offset.y) / total);
        }
        if (reverseValue) val = 1f - val;
        value = val;
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        _ = eventData;
    }

    public override void OnMove(AxisEventData eventData)
    {
        if (!IsActive() || !IsInteractable())
        {
            base.OnMove(eventData);
            return;
        }

        var step = 0.1f;
        switch (eventData.moveDir)
        {
            case MoveDirection.Left:
                if (axis == Axis.Horizontal)
                {
                    value = Mathf.Clamp01(_value + (reverseValue ? step : -step));
                    eventData.Use();
                }
                else
                {
                    base.OnMove(eventData);
                }
                break;
            case MoveDirection.Right:
                if (axis == Axis.Horizontal)
                {
                    value = Mathf.Clamp01(_value + (reverseValue ? -step : step));
                    eventData.Use();
                }
                else
                {
                    base.OnMove(eventData);
                }
                break;
            case MoveDirection.Up:
                if (axis == Axis.Vertical)
                {
                    value = Mathf.Clamp01(_value + (reverseValue ? -step : step));
                    eventData.Use();
                }
                else
                {
                    base.OnMove(eventData);
                }
                break;
            case MoveDirection.Down:
                if (axis == Axis.Vertical)
                {
                    value = Mathf.Clamp01(_value + (reverseValue ? step : -step));
                    eventData.Use();
                }
                else
                {
                    base.OnMove(eventData);
                }
                break;
        }
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
