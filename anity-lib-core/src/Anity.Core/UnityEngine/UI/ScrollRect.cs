using System;

namespace UnityEngine.UI;

public class ScrollRect : UIBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
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

    private RectTransform? _content;
    private RectTransform? _viewport;
    private bool _horizontal = true;
    private bool _vertical = true;
    private MovementType _movementType = MovementType.Elastic;
    private float _elasticity = 0.1f;
    private bool _inertia = true;
    private float _decelerationRate = 0.135f;
    private float _scrollSensitivity = 1f;
    private Scrollbar? _horizontalScrollbar;
    private Scrollbar? _verticalScrollbar;
    private ScrollbarVisibility _horizontalScrollbarVisibility;
    private ScrollbarVisibility _verticalScrollbarVisibility;
    private Vector2 _horizontalScrollbarSpacing;
    private Vector2 _verticalScrollbarSpacing;

    private Vector2 _velocity;
    private Vector2 _normalizedPosition;
    private Vector2 _prevPosition;
    private bool _dragging;
    private Vector2 _pointerStartLocalCursor;
    private Vector2 _contentStartPosition;

    private ScrollRectEvent _onValueChanged = new();

    public RectTransform? content
    {
        get => _content;
        set => _content = value;
    }

    public RectTransform? viewport
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

    public MovementType movementType
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

    public Scrollbar? horizontalScrollbar
    {
        get => _horizontalScrollbar;
        set => _horizontalScrollbar = value;
    }

    public Scrollbar? verticalScrollbar
    {
        get => _verticalScrollbar;
        set => _verticalScrollbar = value;
    }

    public ScrollbarVisibility horizontalScrollbarVisibility
    {
        get => _horizontalScrollbarVisibility;
        set => _horizontalScrollbarVisibility = value;
    }

    public ScrollbarVisibility verticalScrollbarVisibility
    {
        get => _verticalScrollbarVisibility;
        set => _verticalScrollbarVisibility = value;
    }

    public Vector2 horizontalScrollbarSpacing
    {
        get => _horizontalScrollbarSpacing;
        set => _horizontalScrollbarSpacing = value;
    }

    public Vector2 verticalScrollbarSpacing
    {
        get => _verticalScrollbarSpacing;
        set => _verticalScrollbarSpacing = value;
    }

    public Vector2 velocity
    {
        get => _velocity;
        set => _velocity = value;
    }

    public Vector2 normalizedPosition
    {
        get
        {
            UpdateBounds();
            return _normalizedPosition;
        }
        set
        {
            SetNormalizedPosition(value, Axis.Horizontal);
            SetNormalizedPosition(value, Axis.Vertical);
        }
    }

    public float horizontalNormalizedPosition
    {
        get => _normalizedPosition.x;
        set => SetNormalizedPosition(new Vector2(value, _normalizedPosition.y), Axis.Horizontal);
    }

    public float verticalNormalizedPosition
    {
        get => _normalizedPosition.y;
        set => SetNormalizedPosition(new Vector2(_normalizedPosition.x, value), Axis.Vertical);
    }

    public float minWidth => 0f;
    public float preferredWidth => 0f;
    public float flexibleWidth => -1f;
    public float minHeight => 0f;
    public float preferredHeight => 0f;
    public float flexibleHeight => -1f;
    public int layoutPriority => 0;

    public ScrollRectEvent onValueChanged
    {
        get => _onValueChanged;
        set => _onValueChanged = value;
    }

    public virtual bool IsActive()
    {
        return gameObject is not null && gameObject.activeInHierarchy;
    }

    public void Rebuild(CanvasUpdate executing)
    {
        _ = executing;
    }

    public void LayoutComplete()
    {
    }

    public void GraphicUpdateComplete()
    {
    }

    protected virtual void UpdateBounds()
    {
    }

    public virtual void SetLayoutHorizontal()
    {
    }

    public virtual void SetLayoutVertical()
    {
    }

    public virtual void CalculateLayoutInputHorizontal()
    {
    }

    public virtual void CalculateLayoutInputVertical()
    {
    }

    protected void SetNormalizedPosition(Vector2 value, Axis axis)
    {
        if (_content is null) return;

        var contentSize = _content.sizeDelta;
        var viewportSize = _viewport?.sizeDelta ?? Vector2.zero;

        if (axis == Axis.Horizontal)
        {
            _normalizedPosition.x = Mathf.Clamp01(value.x);
            var delta = contentSize.x - viewportSize.x;
            if (delta > 0f)
            {
                var pos = _content.anchoredPosition;
                pos.x = -_normalizedPosition.x * delta;
                _content.anchoredPosition = pos;
            }
        }
        else
        {
            _normalizedPosition.y = Mathf.Clamp01(value.y);
            var delta = contentSize.y - viewportSize.y;
            if (delta > 0f)
            {
                var pos = _content.anchoredPosition;
                pos.y = _normalizedPosition.y * delta;
                _content.anchoredPosition = pos;
            }
        }

        _onValueChanged?.Invoke(_normalizedPosition);
        UpdateScrollbars();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsActive()) return;
        _dragging = true;
        _pointerStartLocalCursor = eventData.position;
        _contentStartPosition = _content?.anchoredPosition ?? Vector2.zero;
        _velocity = Vector2.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsActive() || !_dragging || _content is null) return;

        var pointerDelta = eventData.position - _pointerStartLocalCursor;
        var position = _contentStartPosition + new Vector2(
            _horizontal ? pointerDelta.x : 0f,
            _vertical ? pointerDelta.y : 0f) * _scrollSensitivity;

        position = ApplyMovementType(position);
        _content.anchoredPosition = position;
        _velocity = (position - _prevPosition) / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        _prevPosition = position;
        UpdateNormalizedPosition();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _ = eventData;
        _dragging = false;
    }

    public void OnScroll(PointerEventData data)
    {
        if (!IsActive()) return;

        var delta = data.scrollDelta * _scrollSensitivity;
        var pos = normalizedPosition;
        pos.x += _horizontal ? delta.x : 0f;
        pos.y += _vertical ? delta.y : 0f;
        normalizedPosition = pos;
    }

    protected virtual void LateUpdate()
    {
        if (_dragging || _content is null) return;

        if (_inertia && _velocity.magnitude > 1f)
        {
            var position = _content.anchoredPosition;
            position += _velocity * Time.unscaledDeltaTime;
            position = ApplyMovementType(position);
            _content.anchoredPosition = position;
            _velocity *= Mathf.Pow(_decelerationRate, Time.unscaledDeltaTime);
            UpdateNormalizedPosition();
        }
        else if (_movementType == MovementType.Elastic)
        {
            var target = _content.anchoredPosition;
            target = ApplyClampedPosition(target);
            _content.anchoredPosition = Vector2.Lerp(_content.anchoredPosition, target, Time.unscaledDeltaTime * 10f);
            UpdateNormalizedPosition();
        }
    }

    private Vector2 ApplyMovementType(Vector2 position)
    {
        if (_movementType == MovementType.Unrestricted) return position;
        return ApplyClampedPosition(position);
    }

    private Vector2 ApplyClampedPosition(Vector2 position)
    {
        if (_content is null) return position;
        var contentSize = _content.sizeDelta;
        var viewportSize = _viewport?.sizeDelta ?? Vector2.zero;

        if (_horizontal && contentSize.x > viewportSize.x)
        {
            var min = -(contentSize.x - viewportSize.x);
            position.x = Mathf.Clamp(position.x, min, 0f);
        }
        else
        {
            position.x = 0f;
        }

        if (_vertical && contentSize.y > viewportSize.y)
        {
            var max = contentSize.y - viewportSize.y;
            position.y = Mathf.Clamp(position.y, 0f, max);
        }
        else
        {
            position.y = 0f;
        }

        return position;
    }

    private void UpdateNormalizedPosition()
    {
        if (_content is null) return;
        var contentSize = _content.sizeDelta;
        var viewportSize = _viewport?.sizeDelta ?? Vector2.zero;

        if (contentSize.x > viewportSize.x)
        {
            _normalizedPosition.x = Mathf.Clamp01(-_content.anchoredPosition.x / (contentSize.x - viewportSize.x));
        }
        else
        {
            _normalizedPosition.x = 0f;
        }

        if (contentSize.y > viewportSize.y)
        {
            _normalizedPosition.y = Mathf.Clamp01(_content.anchoredPosition.y / (contentSize.y - viewportSize.y));
        }
        else
        {
            _normalizedPosition.y = 0f;
        }

        _onValueChanged?.Invoke(_normalizedPosition);
        UpdateScrollbars();
    }

    private void UpdateScrollbars()
    {
        _horizontalScrollbar?.SetValueWithoutNotify(_normalizedPosition.x);
        _verticalScrollbar?.SetValueWithoutNotify(_normalizedPosition.y);
    }

    public void StopMovement()
    {
        _velocity = Vector2.zero;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        _ = eventData;
        _velocity = Vector2.zero;
    }
}

[Serializable]
public class ScrollRectEvent
{
    public event Action<Vector2>? ValueChanged;

    public void Invoke(Vector2 value)
    {
        ValueChanged?.Invoke(value);
    }
}
