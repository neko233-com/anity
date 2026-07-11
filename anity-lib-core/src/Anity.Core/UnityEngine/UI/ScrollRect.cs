using System;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

[AddComponentMenu("UI/Scroll Rect", 37)]
public class ScrollRect : UIBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, ICanvasElement, ILayoutElement, ILayoutGroup
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
    private bool _viewRectIsValid;
    private Bounds _contentBounds;
    private Bounds _viewBounds;
    private Vector2 _velocity;
    private Vector2 _normalizedPosition;
    private bool _dragging;
    private Vector2 _pointerStartLocalCursor;
    private Vector2 _contentStartPosition;
    private Vector2 _prevPosition;
    private bool _horizontalScrollbarVisibilityNeedsToBeUpdated;
    private bool _verticalScrollbarVisibilityNeedsToBeUpdated;
    private bool _hasRebuiltLayout;
    private bool _suppressOnScroll;

    private ScrollRectEvent _onValueChanged = new();

    public RectTransform? content
    {
        get => _content;
        set
        {
            _content = value;
            _hasRebuiltLayout = false;
        }
    }

    public RectTransform? viewport
    {
        get => _viewport;
        set
        {
            _viewport = value;
            _viewRectIsValid = false;
            m_ViewRect = null;
        }
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
        set
        {
            if (_horizontalScrollbar != null)
                _horizontalScrollbar.onValueChanged.ValueChanged -= OnHorizontalScrollbarValueChanged;
            _horizontalScrollbar = value;
            if (_horizontalScrollbar != null && IsActive())
            {
                _horizontalScrollbar.onValueChanged.ValueChanged += OnHorizontalScrollbarValueChanged;
            }
        }
    }

    public Scrollbar? verticalScrollbar
    {
        get => _verticalScrollbar;
        set
        {
            if (_verticalScrollbar != null)
                _verticalScrollbar.onValueChanged.ValueChanged -= OnVerticalScrollbarValueChanged;
            _verticalScrollbar = value;
            if (_verticalScrollbar != null && IsActive())
            {
                _verticalScrollbar.onValueChanged.ValueChanged += OnVerticalScrollbarValueChanged;
            }
        }
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

    public RectTransform rectTransform => transform as RectTransform;
    private RectTransform? m_ViewRect;
    private RectTransform viewRect
    {
        get
        {
            if (!_viewRectIsValid)
            {
                m_ViewRect = _viewport == null ? GetComponent<RectTransform>() : _viewport;
                _viewRectIsValid = true;
            }
            return m_ViewRect;
        }
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
            SetNormalizedPosition(value.x, 0);
            SetNormalizedPosition(value.y, 1);
        }
    }

    public float horizontalNormalizedPosition
    {
        get
        {
            UpdateBounds();
            return _normalizedPosition.x;
        }
        set => SetNormalizedPosition(value, 0);
    }

    public float verticalNormalizedPosition
    {
        get
        {
            UpdateBounds();
            return _normalizedPosition.y;
        }
        set => SetNormalizedPosition(value, 1);
    }

    public virtual float minWidth => -1f;
    public virtual float preferredWidth => -1f;
    public virtual float flexibleWidth => -1f;
    public virtual float minHeight => -1f;
    public virtual float preferredHeight => -1f;
    public virtual float flexibleHeight => -1f;
    public virtual int layoutPriority => -1;

    public ScrollRectEvent onValueChanged
    {
        get => _onValueChanged;
        set => _onValueChanged = value;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_horizontalScrollbar != null)
        {
            _horizontalScrollbar.onValueChanged.ValueChanged += OnHorizontalScrollbarValueChanged;
        }
        if (_verticalScrollbar != null)
        {
            _verticalScrollbar.onValueChanged.ValueChanged += OnVerticalScrollbarValueChanged;
        }
        CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
        SetDirty();
    }

    protected override void OnDisable()
    {
        CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
        if (_horizontalScrollbar != null)
        {
            _horizontalScrollbar.onValueChanged.ValueChanged -= OnHorizontalScrollbarValueChanged;
        }
        if (_verticalScrollbar != null)
        {
            _verticalScrollbar.onValueChanged.ValueChanged -= OnVerticalScrollbarValueChanged;
        }
        _velocity = Vector2.zero;
        _dragging = false;
        base.OnDisable();
    }

    public virtual void Rebuild(CanvasUpdate executing)
    {
        if (executing == CanvasUpdate.PostLayout)
        {
            UpdateBounds();
            UpdateScrollbars();
            _hasRebuiltLayout = true;
        }
        if (executing == CanvasUpdate.PreRender)
        {
            UpdateScrollbarVisibility();
        }
    }

    public virtual void LayoutComplete() { }
    public virtual void GraphicUpdateComplete() { }

    public virtual void CalculateLayoutInputHorizontal() { }
    public virtual void CalculateLayoutInputVertical() { }
    public virtual void SetLayoutHorizontal() { }
    public virtual void SetLayoutVertical() { }

    private void OnHorizontalScrollbarValueChanged(float value)
    {
        if (_suppressOnScroll) return;
        if (!_horizontal) return;
        SetNormalizedPosition(value, 0);
    }

    private void OnVerticalScrollbarValueChanged(float value)
    {
        if (_suppressOnScroll) return;
        if (!_vertical) return;
        SetNormalizedPosition(value, 1);
    }

    protected void SetNormalizedPosition(float value, int axis)
    {
        if (_content == null) return;
        UpdateBounds();

        var delta = axis == 0 ? _contentBounds.size.x - _viewBounds.size.x : _contentBounds.size.y - _viewBounds.size.y;
        var newAnchoredPosition = _content.anchoredPosition;
        if (delta > 0.01f)
        {
            if (axis == 0)
                newAnchoredPosition.x = -value * delta;
            else
                newAnchoredPosition.y = value * delta;
        }
        _content.anchoredPosition = newAnchoredPosition;
        UpdateBounds();
        UpdatePrevData();
        _onValueChanged?.Invoke(_normalizedPosition);
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        _ = eventData;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        _velocity = Vector2.zero;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!IsActive()) return;

        _dragging = true;
        if (_content == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.pressPosition, eventData.pressEventCamera, out _pointerStartLocalCursor))
        {
            _contentStartPosition = _content.anchoredPosition;
            UpdateBounds();
        }
        _velocity = Vector2.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging || _content == null) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!IsActive()) return;

        Vector2 localCursor;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position, eventData.pressEventCamera, out localCursor))
            return;

        var pointerDelta = localCursor - _pointerStartLocalCursor;
        var position = _contentStartPosition + pointerDelta;

        if (!_horizontal)
            position.x = _content.anchoredPosition.x;
        if (!_vertical)
            position.y = _content.anchoredPosition.y;

        var offset = CalculateOffset(position - _content.anchoredPosition);
        position += offset;

        if (_movementType == MovementType.Elastic)
        {
            if (offset.x != 0f) position.x = position.x - RubberDelta(offset.x, _viewBounds.size.x);
            if (offset.y != 0f) position.y = position.y - RubberDelta(offset.y, _viewBounds.size.y);
        }

        SetContentAnchoredPosition(position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _ = eventData;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        _dragging = false;
    }

    public void OnScroll(PointerEventData data)
    {
        if (!IsActive()) return;
        if (_content == null) return;

        var delta = data.scrollDelta;
        delta.y *= -1f;
        if (!_horizontal) delta.x = 0f;
        if (!_vertical) delta.y = 0f;

        var position = _content.anchoredPosition;
        if (_horizontal) position.x += delta.x * _scrollSensitivity;
        if (_vertical) position.y += delta.y * _scrollSensitivity;
        position += CalculateOffset(position - _content.anchoredPosition);
        SetContentAnchoredPosition(position);
        UpdateBounds();
    }

    protected virtual void LateUpdate()
    {
        if (_content == null) return;
        EnsureLayoutHasRebuilt();
        UpdateBounds();
        var deltaTime = Time.unscaledDeltaTime;
        var offset = CalculateOffset(Vector2.zero);

        if (!_dragging && (offset != Vector2.zero || _velocity != Vector2.zero))
        {
            var position = _content.anchoredPosition;
            for (int axis = 0; axis < 2; axis++)
            {
                if (offset[axis] != 0f && _movementType == MovementType.Elastic)
                {
                    var speed = _velocity[axis];
                    position[axis] = Mathf.SmoothDamp(_content.anchoredPosition[axis], _content.anchoredPosition[axis] + offset[axis], ref speed, _elasticity, Mathf.Infinity, deltaTime);
                    _velocity[axis] = speed;
                }
                else if (_inertia)
                {
                    _velocity[axis] *= Mathf.Pow(_decelerationRate, deltaTime);
                    if (Mathf.Abs(_velocity[axis]) < 1f) _velocity[axis] = 0f;
                    position[axis] += _velocity[axis] * deltaTime;
                }
                else
                {
                    _velocity[axis] = 0f;
                }
            }

            if (_movementType != MovementType.Unrestricted)
            {
                var offset2 = CalculateOffset(position - _content.anchoredPosition);
                position += offset2;
                for (int axis = 0; axis < 2; axis++)
                {
                    if (offset2[axis] != 0f && _movementType == MovementType.Clamped)
                    {
                        offset = CalculateOffset(position - _content.anchoredPosition);
                        position += offset;
                        _velocity[axis] = 0f;
                    }
                }
            }

            SetContentAnchoredPosition(position);
        }

        if (_dragging && _inertia)
        {
            var newVelocity = (_content.anchoredPosition - _prevPosition) / deltaTime;
            _velocity = Vector2.Lerp(_velocity, newVelocity, deltaTime * 10f);
        }

        UpdatePrevData();
        UpdateScrollbars();
    }

    private void EnsureLayoutHasRebuilt()
    {
        if (!_hasRebuiltLayout && !CanvasUpdateRegistry.instance.IsRebuildingLayout())
        {
            Canvas.ForceUpdateCanvases();
        }
    }

    private void UpdatePrevData()
    {
        if (_content == null)
            _prevPosition = Vector2.zero;
        else
            _prevPosition = _content.anchoredPosition;
    }

    private void SetContentAnchoredPosition(Vector2 position)
    {
        if (_content == null) return;
        if (position != _content.anchoredPosition)
        {
            _content.anchoredPosition = position;
            UpdateBounds();
            UpdateNormalizedPosition();
        }
    }

    private Vector2 CalculateOffset(Vector2 delta)
    {
        var offset = Vector2.zero;
        if (_movementType == MovementType.Unrestricted) return offset;
        if (_content == null) return offset;

        var vMin = new Vector2(_contentBounds.min.x, _contentBounds.min.y);
        var vMax = new Vector2(_contentBounds.max.x, _contentBounds.max.y);
        var bounds = new Bounds(_viewBounds.center, _viewBounds.size);
        var max = new Vector2(bounds.max.x, bounds.max.y);
        var min = new Vector2(bounds.min.x, bounds.min.y);

        if (_horizontal)
        {
            if (delta.x > 0f) vMin.x += delta.x;
            if (delta.x < 0f) vMax.x += delta.x;
            offset.x = vMin.x > min.x ? min.x - vMin.x : (vMax.x < max.x ? max.x - vMax.x : 0f);
        }
        else
        {
            offset.x = _content.anchoredPosition.x + offset.x;
        }

        if (_vertical)
        {
            if (delta.y > 0f) vMin.y += delta.y;
            if (delta.y < 0f) vMax.y += delta.y;
            offset.y = vMin.y > min.y ? min.y - vMin.y : (vMax.y < max.y ? max.y - vMax.y : 0f);
        }
        else
        {
            offset.y = _content.anchoredPosition.y + offset.y;
        }

        return offset;
    }

    private static float RubberDelta(float overStretching, float viewSize)
    {
        return (1f - (1f / ((Mathf.Abs(overStretching) * 0.55f / viewSize) + 1f))) * viewSize * Mathf.Sign(overStretching);
    }

    private void UpdateBounds()
    {
        if (_content == null || viewRect == null) return;
        var vMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var vMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var cMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var cMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < viewRect.childCount; i++)
        {
            _ = viewRect.GetChild(i);
        }
        var viewSize = viewRect.rect.size;
        var contentSize = _content.rect.size;
        var contentPos = _content.anchoredPosition;
        _viewBounds = new Bounds(new Vector3(0, 0, 0), new Vector3(viewSize.x, viewSize.y, 0f));
        _contentBounds = new Bounds(new Vector3(contentPos.x + contentSize.x * _content.pivot.x, contentPos.y + contentSize.y * _content.pivot.y, 0f), new Vector3(contentSize.x, contentSize.y, 0f));
        UpdateNormalizedPosition();
    }

    private void UpdateNormalizedPosition()
    {
        float hDelta = _contentBounds.size.x - _viewBounds.size.x;
        float vDelta = _contentBounds.size.y - _viewBounds.size.y;
        if (hDelta > 0.01f)
        {
            _normalizedPosition.x = Mathf.Clamp01(-_content.anchoredPosition.x / hDelta);
        }
        else
        {
            _normalizedPosition.x = (_contentBounds.min.x > _viewBounds.min.x) ? 1f : 0f;
        }
        if (vDelta > 0.01f)
        {
            _normalizedPosition.y = Mathf.Clamp01(_content.anchoredPosition.y / vDelta);
        }
        else
        {
            _normalizedPosition.y = (_contentBounds.min.y > _viewBounds.min.y) ? 1f : 0f;
        }
        _onValueChanged?.Invoke(_normalizedPosition);
        _suppressOnScroll = true;
        _horizontalScrollbar?.SetValueWithoutNotify(_normalizedPosition.x);
        _verticalScrollbar?.SetValueWithoutNotify(_normalizedPosition.y);
        _suppressOnScroll = false;
    }

    private void UpdateScrollbars()
    {
        if (_content == null || viewRect == null) return;
        float hDelta = _contentBounds.size.x - _viewBounds.size.x;
        float vDelta = _contentBounds.size.y - _viewBounds.size.y;
        var hSize = hDelta > 0f ? Mathf.Clamp01(_viewBounds.size.x / _contentBounds.size.x) : 1f;
        var vSize = vDelta > 0f ? Mathf.Clamp01(_viewBounds.size.y / _contentBounds.size.y) : 1f;

        _suppressOnScroll = true;
        if (_horizontalScrollbar != null)
        {
            _horizontalScrollbar.size = hSize;
            _horizontalScrollbar.SetValueWithoutNotify(_normalizedPosition.x);
        }
        if (_verticalScrollbar != null)
        {
            _verticalScrollbar.size = vSize;
            _verticalScrollbar.SetValueWithoutNotify(_normalizedPosition.y);
        }
        _suppressOnScroll = false;
    }

    private void UpdateScrollbarVisibility()
    {
        _horizontalScrollbarVisibilityNeedsToBeUpdated = true;
        _verticalScrollbarVisibilityNeedsToBeUpdated = true;
        if (_horizontalScrollbar != null && _horizontalScrollbarVisibility != ScrollbarVisibility.Permanent)
        {
            _horizontalScrollbar.gameObject.SetActive(true);
        }
        if (_verticalScrollbar != null && _verticalScrollbarVisibility != ScrollbarVisibility.Permanent)
        {
            _verticalScrollbar.gameObject.SetActive(true);
        }
    }

    public void StopMovement()
    {
        _velocity = Vector2.zero;
    }

    private void SetDirty()
    {
        if (!IsActive()) return;
        LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetDirty();
    }

    protected override void OnTransformParentChanged()
    {
        base.OnTransformParentChanged();
        if (viewRect != null) _viewRectIsValid = false;
    }

    public bool IsActive()
    {
        return gameObject != null && gameObject.activeInHierarchy && enabled;
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
