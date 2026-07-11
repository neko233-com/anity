using System;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

[AddComponentMenu("UI/Scroll Rect", 37)]
[RequireComponent(typeof(RectTransform))]
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

    [SerializeField] private RectTransform? m_Content;
    [SerializeField] private RectTransform? m_Viewport;
    [SerializeField] private bool m_Horizontal = true;
    [SerializeField] private bool m_Vertical = true;
    [SerializeField] private MovementType m_MovementType = MovementType.Elastic;
    [SerializeField] private float m_Elasticity = 0.1f;
    [SerializeField] private bool m_Inertia = true;
    [SerializeField] private float m_DecelerationRate = 0.135f;
    [SerializeField] private float m_ScrollSensitivity = 1f;
    [SerializeField] private Scrollbar? m_HorizontalScrollbar;
    [SerializeField] private Scrollbar? m_VerticalScrollbar;
    [SerializeField] private ScrollbarVisibility m_HorizontalScrollbarVisibility;
    [SerializeField] private ScrollbarVisibility m_VerticalScrollbarVisibility;
    [SerializeField] private float m_HorizontalScrollbarSpacing;
    [SerializeField] private float m_VerticalScrollbarSpacing;

    private Bounds m_ContentBounds;
    private Bounds m_ViewBounds;
    private Vector2 m_Velocity;
    private bool m_Dragging;
    private Vector2 m_PointerStartLocalCursor;
    private Vector2 m_ContentStartPosition;
    private RectTransform? m_ViewRect;
    private Vector3[] m_Corners = new Vector3[4];
    private Vector2 m_PrevPosition = Vector2.zero;
    private Bounds m_PrevContentBounds;
    private Bounds m_PrevViewBounds;
    private bool m_HasRebuiltLayout;
    private bool m_SuppressOnScroll;
    private bool m_VelocityDirty;
    private int m_RebuildingLayoutCount;
    private ScrollRect? m_ParentScrollRect;

    [SerializeField] private ScrollRectEvent m_OnValueChanged = new();

    public RectTransform? content
    {
        get => m_Content;
        set
        {
            m_Content = value;
            m_HasRebuiltLayout = false;
            m_ViewRect = null;
            SetDirty();
        }
    }

    public RectTransform? viewport
    {
        get => m_Viewport;
        set
        {
            m_Viewport = value;
            m_ViewRect = null;
            SetDirty();
        }
    }

    public bool horizontal
    {
        get => m_Horizontal;
        set
        {
            m_Horizontal = value;
            SetDirty();
        }
    }

    public bool vertical
    {
        get => m_Vertical;
        set
        {
            m_Vertical = value;
            SetDirty();
        }
    }

    public MovementType movementType
    {
        get => m_MovementType;
        set
        {
            m_MovementType = value;
            SetDirty();
        }
    }

    public float elasticity
    {
        get => m_Elasticity;
        set => m_Elasticity = value;
    }

    public bool inertia
    {
        get => m_Inertia;
        set => m_Inertia = value;
    }

    public float decelerationRate
    {
        get => m_DecelerationRate;
        set => m_DecelerationRate = value;
    }

    public float scrollSensitivity
    {
        get => m_ScrollSensitivity;
        set => m_ScrollSensitivity = value;
    }

    public Scrollbar? horizontalScrollbar
    {
        get => m_HorizontalScrollbar;
        set
        {
            if (m_HorizontalScrollbar != null)
                m_HorizontalScrollbar.onValueChanged.ValueChanged -= OnHorizontalScrollbarValueChanged;
            m_HorizontalScrollbar = value;
            if (m_HorizontalScrollbar != null && IsActive())
                m_HorizontalScrollbar.onValueChanged.ValueChanged += OnHorizontalScrollbarValueChanged;
            SetDirty();
        }
    }

    public Scrollbar? verticalScrollbar
    {
        get => m_VerticalScrollbar;
        set
        {
            if (m_VerticalScrollbar != null)
                m_VerticalScrollbar.onValueChanged.ValueChanged -= OnVerticalScrollbarValueChanged;
            m_VerticalScrollbar = value;
            if (m_VerticalScrollbar != null && IsActive())
                m_VerticalScrollbar.onValueChanged.ValueChanged += OnVerticalScrollbarValueChanged;
            SetDirty();
        }
    }

    public ScrollbarVisibility horizontalScrollbarVisibility
    {
        get => m_HorizontalScrollbarVisibility;
        set
        {
            m_HorizontalScrollbarVisibility = value;
            SetDirty();
        }
    }

    public ScrollbarVisibility verticalScrollbarVisibility
    {
        get => m_VerticalScrollbarVisibility;
        set
        {
            m_VerticalScrollbarVisibility = value;
            SetDirty();
        }
    }

    public float horizontalScrollbarSpacing
    {
        get => m_HorizontalScrollbarSpacing;
        set
        {
            m_HorizontalScrollbarSpacing = value;
            SetDirty();
        }
    }

    public float verticalScrollbarSpacing
    {
        get => m_VerticalScrollbarSpacing;
        set
        {
            m_VerticalScrollbarSpacing = value;
            SetDirty();
        }
    }

    public Vector2 velocity
    {
        get => m_Velocity;
        set
        {
            m_Velocity = value;
            m_VelocityDirty = true;
        }
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
        get => m_OnValueChanged;
        set => m_OnValueChanged = value;
    }

    public RectTransform rectTransform => transform as RectTransform;

    protected RectTransform viewRect
    {
        get
        {
            if (m_ViewRect == null)
                m_ViewRect = m_Viewport;
            if (m_ViewRect == null)
                m_ViewRect = rectTransform;
            return m_ViewRect;
        }
    }

    public Bounds bounds
    {
        get
        {
            UpdateBounds();
            return m_ContentBounds;
        }
    }

    private bool hScrollingNeeded
    {
        get
        {
            if (Application.isPlaying)
                return m_ContentBounds.size.x > m_ViewBounds.size.x + 0.01f;
            return true;
        }
    }

    private bool vScrollingNeeded
    {
        get
        {
            if (Application.isPlaying)
                return m_ContentBounds.size.y > m_ViewBounds.size.y + 0.01f;
            return true;
        }
    }

    public Vector2 normalizedPosition
    {
        get => new Vector2(horizontalNormalizedPosition, verticalNormalizedPosition);
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
            if (m_ContentBounds.size.x <= m_ViewBounds.size.x)
                return (m_ViewBounds.min.x > m_ContentBounds.min.x) ? 1f : 0f;
            return (m_ViewBounds.min.x - m_ContentBounds.min.x) / (m_ContentBounds.size.x - m_ViewBounds.size.x);
        }
        set => SetNormalizedPosition(value, 0);
    }

    public float verticalNormalizedPosition
    {
        get
        {
            UpdateBounds();
            if (m_ContentBounds.size.y <= m_ViewBounds.size.y)
                return (m_ViewBounds.min.y > m_ContentBounds.min.y) ? 1f : 0f;
            return (m_ViewBounds.min.y - m_ContentBounds.min.y) / (m_ContentBounds.size.y - m_ViewBounds.size.y);
        }
        set => SetNormalizedPosition(value, 1);
    }

    private void OnHorizontalScrollbarValueChanged(float value)
    {
        if (m_SuppressOnScroll) return;
        if (!m_Horizontal) return;
        SetNormalizedPosition(value, 0);
    }

    private void OnVerticalScrollbarValueChanged(float value)
    {
        if (m_SuppressOnScroll) return;
        if (!m_Vertical) return;
        SetNormalizedPosition(value, 1);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (m_HorizontalScrollbar != null)
        {
            m_HorizontalScrollbar.onValueChanged.ValueChanged -= OnHorizontalScrollbarValueChanged;
            m_HorizontalScrollbar.onValueChanged.ValueChanged += OnHorizontalScrollbarValueChanged;
        }

        if (m_VerticalScrollbar != null)
        {
            m_VerticalScrollbar.onValueChanged.ValueChanged -= OnVerticalScrollbarValueChanged;
            m_VerticalScrollbar.onValueChanged.ValueChanged += OnVerticalScrollbarValueChanged;
        }

        m_ParentScrollRect = FindParentScrollRect();

        CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
        SetDirty();
    }

    protected override void OnDisable()
    {
        CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);

        if (m_HorizontalScrollbar != null)
            m_HorizontalScrollbar.onValueChanged.ValueChanged -= OnHorizontalScrollbarValueChanged;
        if (m_VerticalScrollbar != null)
            m_VerticalScrollbar.onValueChanged.ValueChanged -= OnVerticalScrollbarValueChanged;

        m_Dragging = false;
        m_Velocity = Vector2.zero;
        base.OnDisable();
    }

    public virtual void Rebuild(CanvasUpdate executing)
    {
        if (executing == CanvasUpdate.PostLayout)
        {
            m_RebuildingLayoutCount++;
            UpdateBounds();
            UpdateScrollbars();
            UpdateScrollbarVisibility();
            m_HasRebuiltLayout = true;
            m_RebuildingLayoutCount--;
        }
    }

    public virtual void LayoutComplete() { }
    public virtual void GraphicUpdateComplete() { }
    public virtual bool IsDestroyed() => this == null;

    public virtual void CalculateLayoutInputHorizontal() { }
    public virtual void CalculateLayoutInputVertical() { }
    public virtual void SetLayoutHorizontal() { }
    public virtual void SetLayoutVertical() { }

    protected void UpdateScrollbarVisibility()
    {
        UpdateOneScrollbarVisibility(vScrollingNeeded, m_VerticalScrollbar, m_VerticalScrollbarVisibility);
        UpdateOneScrollbarVisibility(hScrollingNeeded, m_HorizontalScrollbar, m_HorizontalScrollbarVisibility);
    }

    private void UpdateOneScrollbarVisibility(bool xScrollingNeeded, Scrollbar? scrollbar, ScrollbarVisibility visibility)
    {
        if (scrollbar == null)
            return;

        if (visibility == ScrollbarVisibility.Permanent)
        {
            scrollbar.gameObject.SetActive(true);
        }
        else
        {
            bool shouldBeVisible = xScrollingNeeded;
            if (visibility == ScrollbarVisibility.AutoHideAndExpandViewport)
            {
                scrollbar.gameObject.SetActive(shouldBeVisible);
            }
            else if (visibility == ScrollbarVisibility.AutoHide)
            {
                scrollbar.gameObject.SetActive(shouldBeVisible || m_Dragging);
            }
        }
    }

    private void SetNormalizedPosition(float value, int axis)
    {
        if (m_Content == null)
            return;

        UpdateBounds();

        float contentSize = axis == 0 ? m_ContentBounds.size.x : m_ContentBounds.size.y;
        float viewSize = axis == 0 ? m_ViewBounds.size.x : m_ViewBounds.size.y;
        float delta = contentSize - viewSize;

        if (delta <= 0.01f)
            return;

        Vector2 newAnchoredPosition = m_Content.anchoredPosition;
        float newPos = Mathf.Clamp01(value) * delta;
        if (axis == 0)
            newAnchoredPosition.x = -newPos;
        else
            newAnchoredPosition.y = newPos;

        SetContentAnchoredPosition(newAnchoredPosition);
    }

    public virtual void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (m_ParentScrollRect != null)
            m_ParentScrollRect.OnInitializePotentialDrag(eventData);

        m_Velocity = Vector2.zero;
    }

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (!IsActive())
            return;

        if (m_HorizontalScrollbar != null && m_HorizontalScrollbar.IsActive() && m_HorizontalScrollbar.IsInteractable() && RectTransformUtility.RectangleContainsScreenPoint(m_HorizontalScrollbar.GetComponent<RectTransform>(), eventData.position, eventData.pressEventCamera))
            return;
        if (m_VerticalScrollbar != null && m_VerticalScrollbar.IsActive() && m_VerticalScrollbar.IsInteractable() && RectTransformUtility.RectangleContainsScreenPoint(m_VerticalScrollbar.GetComponent<RectTransform>(), eventData.position, eventData.pressEventCamera))
            return;

        if (m_Content != null)
        {
            UpdateBounds();

            if (!IsHorizontalScrollbarVisible())
            {
                if (m_ParentScrollRect != null && m_ParentScrollRect.IsActive())
                {
                    m_ParentScrollRect.OnBeginDrag(eventData);
                }
                return;
            }
            if (!IsVerticalScrollbarVisible())
            {
                if (m_ParentScrollRect != null && m_ParentScrollRect.IsActive())
                {
                    m_ParentScrollRect.OnBeginDrag(eventData);
                }
                return;
            }
        }

        m_Dragging = true;
        m_Velocity = Vector2.zero;

        if (m_Content == null)
            return;

        Vector2 localCursor;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.pressPosition, eventData.pressEventCamera, out localCursor))
            return;

        m_PointerStartLocalCursor = localCursor;
        m_ContentStartPosition = m_Content.anchoredPosition;
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (!IsActive())
            return;
        if (m_Content == null)
            return;

        if (!m_Dragging)
        {
            if (m_ParentScrollRect != null && m_ParentScrollRect.IsActive())
                m_ParentScrollRect.OnDrag(eventData);
            return;
        }

        Vector2 localCursor;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position, eventData.pressEventCamera, out localCursor))
            return;

        UpdateBounds();

        var pointerDelta = localCursor - m_PointerStartLocalCursor;
        Vector2 position = m_ContentStartPosition + pointerDelta;

        Vector2 offset = CalculateOffset(position - m_Content.anchoredPosition);
        position += offset;

        if (m_MovementType == MovementType.Elastic)
        {
            if (offset.x != 0)
                position.x = position.x - RubberDelta(offset.x, m_ViewBounds.size.x);
            if (offset.y != 0)
                position.y = position.y - RubberDelta(offset.y, m_ViewBounds.size.y);
        }

        bool setX = m_Horizontal;
        bool setY = m_Vertical;

        if (m_Horizontal)
        {
            if (Mathf.Abs(offset.x) > 0.01f && m_ParentScrollRect != null && m_ParentScrollRect.IsActive())
            {
                if (offset.x > 0 && m_Content.anchoredPosition.x >= -0.01f)
                {
                    setX = false;
                    position.x = m_Content.anchoredPosition.x;
                }
                else if (offset.x < 0 && m_Content.anchoredPosition.x <= -(m_ContentBounds.size.x - m_ViewBounds.size.x) + 0.01f)
                {
                    setX = false;
                    position.x = m_Content.anchoredPosition.x;
                }
            }
        }

        if (m_Vertical)
        {
            if (Mathf.Abs(offset.y) > 0.01f && m_ParentScrollRect != null && m_ParentScrollRect.IsActive())
            {
                if (offset.y < 0 && m_Content.anchoredPosition.y <= 0.01f)
                {
                    setY = false;
                    position.y = m_Content.anchoredPosition.y;
                }
                else if (offset.y > 0 && m_Content.anchoredPosition.y >= m_ContentBounds.size.y - m_ViewBounds.size.y - 0.01f)
                {
                    setY = false;
                    position.y = m_Content.anchoredPosition.y;
                }
            }
        }

        if (!setX) position.x = m_Content.anchoredPosition.x;
        if (!setY) position.y = m_Content.anchoredPosition.y;

        if (!setX || !setY)
        {
            if (m_ParentScrollRect != null && m_ParentScrollRect.IsActive())
                m_ParentScrollRect.OnDrag(eventData);
        }

        SetContentAnchoredPosition(position);
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (m_Dragging)
        {
            m_Dragging = false;
        }
        else
        {
            if (m_ParentScrollRect != null && m_ParentScrollRect.IsActive())
                m_ParentScrollRect.OnEndDrag(eventData);
        }
    }

    public virtual void OnScroll(PointerEventData data)
    {
        if (!IsActive())
            return;
        if (m_Content == null)
            return;

        EnsureLayoutHasRebuilt();
        UpdateBounds();

        Vector2 delta = data.scrollDelta;
        delta.y *= -1f;

        if (!m_Horizontal && m_Vertical)
        {
            float tmp = delta.x;
            delta.x = delta.y;
            delta.y = tmp;
        }

        if (!m_Horizontal) delta.x = 0f;
        if (!m_Vertical) delta.y = 0f;

        if (delta.sqrMagnitude < float.Epsilon)
            return;

        Vector2 position = m_Content.anchoredPosition;
        position += delta * m_ScrollSensitivity;

        Vector2 offset = Vector2.zero;
        if (m_MovementType == MovementType.Clamped)
        {
            offset = CalculateOffset(position - m_Content.anchoredPosition);
            position += offset;
        }

        bool shouldConsumeScroll = true;
        if (m_ParentScrollRect != null && m_ParentScrollRect.IsActive())
        {
            if (m_Horizontal)
            {
                if ((delta.x > 0 && m_Content.anchoredPosition.x >= -0.01f) ||
                    (delta.x < 0 && m_Content.anchoredPosition.x <= -(m_ContentBounds.size.x - m_ViewBounds.size.x) + 0.01f))
                {
                    position.x = m_Content.anchoredPosition.x;
                    shouldConsumeScroll = false;
                }
            }
            if (m_Vertical)
            {
                if ((delta.y < 0 && m_Content.anchoredPosition.y <= 0.01f) ||
                    (delta.y > 0 && m_Content.anchoredPosition.y >= m_ContentBounds.size.y - m_ViewBounds.size.y - 0.01f))
                {
                    position.y = m_Content.anchoredPosition.y;
                    shouldConsumeScroll = false;
                }
            }

            if (!shouldConsumeScroll)
                m_ParentScrollRect.OnScroll(data);
        }

        SetContentAnchoredPosition(position);
    }

    protected virtual void LateUpdate()
    {
        if (m_Content == null)
            return;

        EnsureLayoutHasRebuilt();
        UpdateBounds();

        float deltaTime = Time.unscaledDeltaTime;
        if (deltaTime <= 0f)
            return;

        Vector2 offset = CalculateOffset(Vector2.zero);

        if (!m_Dragging && (offset != Vector2.zero || m_Velocity != Vector2.zero))
        {
            Vector2 position = m_Content.anchoredPosition;
            for (int axis = 0; axis < 2; axis++)
            {
                if (m_MovementType == MovementType.Elastic)
                {
                    if (offset[axis] != 0)
                    {
                        float speed = m_Velocity[axis];
                        float targetPos = m_Content.anchoredPosition[axis] + offset[axis];
                        position[axis] = Mathf.SmoothDamp(m_Content.anchoredPosition[axis], targetPos, ref speed, m_Elasticity, Mathf.Infinity, deltaTime);
                        m_Velocity[axis] = speed;
                    }
                    else if (m_Inertia)
                    {
                        m_Velocity[axis] *= Mathf.Pow(m_DecelerationRate, deltaTime);
                        if (Mathf.Abs(m_Velocity[axis]) < 1f)
                            m_Velocity[axis] = 0;
                        position[axis] += m_Velocity[axis] * deltaTime;
                    }
                    else
                    {
                        m_Velocity[axis] = 0;
                    }
                }
                else if (m_MovementType == MovementType.Clamped)
                {
                    if (offset[axis] != 0)
                    {
                        position[axis] += offset[axis];
                        m_Velocity[axis] = 0;
                    }
                    else if (m_Inertia)
                    {
                        m_Velocity[axis] *= Mathf.Pow(m_DecelerationRate, deltaTime);
                        if (Mathf.Abs(m_Velocity[axis]) < 1f)
                            m_Velocity[axis] = 0;
                        position[axis] += m_Velocity[axis] * deltaTime;
                    }
                    else
                    {
                        m_Velocity[axis] = 0;
                    }
                }
                else
                {
                    if (m_Inertia)
                    {
                        m_Velocity[axis] *= Mathf.Pow(m_DecelerationRate, deltaTime);
                        if (Mathf.Abs(m_Velocity[axis]) < 1f)
                            m_Velocity[axis] = 0;
                        position[axis] += m_Velocity[axis] * deltaTime;
                    }
                    else
                    {
                        m_Velocity[axis] = 0;
                    }
                }
            }

            if (m_MovementType != MovementType.Unrestricted)
            {
                Vector2 offset2 = CalculateOffset(position - m_Content.anchoredPosition);
                position += offset2;
            }

            SetContentAnchoredPosition(position);
        }

        if (m_Dragging && m_Inertia)
        {
            Vector2 newVelocity = (m_Content.anchoredPosition - m_PrevPosition) / deltaTime;
            m_Velocity = Vector2.Lerp(m_Velocity, newVelocity, deltaTime * 10f);
        }

        if (BoundsChanged() || PositionChanged())
        {
            UpdateScrollbars();
            UpdateScrollbarVisibility();
            m_OnValueChanged?.Invoke(normalizedPosition);
        }

        UpdatePrevData();
    }

    private bool BoundsChanged()
    {
        return !Approximately(m_PrevViewBounds, m_ViewBounds) || !Approximately(m_PrevContentBounds, m_ContentBounds);
    }

    private bool PositionChanged()
    {
        return m_Content != null && (m_Content.anchoredPosition - m_PrevPosition).sqrMagnitude > 0.0001f;
    }

    private static bool Approximately(Bounds a, Bounds b)
    {
        return (a.center - b.center).sqrMagnitude < 0.0001f && (a.size - b.size).sqrMagnitude < 0.0001f;
    }

    public virtual void StopMovement()
    {
        m_Velocity = Vector2.zero;
    }

    private ScrollRect? FindParentScrollRect()
    {
        Transform? parent = transform.parent;
        while (parent != null)
        {
            var scrollRect = parent.GetComponent<ScrollRect>();
            if (scrollRect != null)
                return scrollRect;
            parent = parent.parent;
        }
        return null;
    }

    private bool IsHorizontalScrollbarVisible()
    {
        return m_Horizontal || !vScrollingNeeded;
    }

    private bool IsVerticalScrollbarVisible()
    {
        return m_Vertical || !hScrollingNeeded;
    }

    private void EnsureLayoutHasRebuilt()
    {
        if (!m_HasRebuiltLayout && !CanvasUpdateRegistry.instance.IsRebuildingLayout())
            Canvas.ForceUpdateCanvases();
    }

    private void UpdatePrevData()
    {
        if (m_Content == null)
            m_PrevPosition = Vector2.zero;
        else
            m_PrevPosition = m_Content.anchoredPosition;
        m_PrevViewBounds = m_ViewBounds;
        m_PrevContentBounds = m_ContentBounds;
    }

    protected virtual void SetContentAnchoredPosition(Vector2 position)
    {
        if (m_Content == null)
            return;

        if (!m_Horizontal)
            position.x = m_Content.anchoredPosition.x;
        if (!m_Vertical)
            position.y = m_Content.anchoredPosition.y;

        if ((position - m_Content.anchoredPosition).sqrMagnitude > 0.0001f)
        {
            m_Content.anchoredPosition = position;
            UpdateBounds();
        }
    }

    protected Vector2 CalculateOffset(Vector2 delta)
    {
        return InternalCalculateOffset(ref m_ViewBounds, ref m_ContentBounds, m_Horizontal, m_Vertical, m_MovementType, delta);
    }

    private static Vector2 InternalCalculateOffset(ref Bounds viewBounds, ref Bounds contentBounds, bool horizontal, bool vertical, MovementType movementType, Vector2 delta)
    {
        Vector2 offset = Vector2.zero;
        if (movementType == MovementType.Unrestricted)
            return offset;

        Vector3 min = contentBounds.min;
        Vector3 max = contentBounds.max;

        if (horizontal)
        {
            min.x += delta.x;
            max.x += delta.x;
            if (min.x > viewBounds.min.x)
                offset.x = viewBounds.min.x - min.x;
            else if (max.x < viewBounds.max.x)
                offset.x = viewBounds.max.x - max.x;
        }

        if (vertical)
        {
            min.y += delta.y;
            max.y += delta.y;
            if (max.y < viewBounds.max.y)
                offset.y = viewBounds.max.y - max.y;
            else if (min.y > viewBounds.min.y)
                offset.y = viewBounds.min.y - min.y;
        }

        return offset;
    }

    private static float RubberDelta(float overStretching, float viewSize)
    {
        return (1f - (1f / ((Mathf.Abs(overStretching) * 0.55f / viewSize) + 1f))) * viewSize * Mathf.Sign(overStretching);
    }

    protected void UpdateBounds()
    {
        if (m_Content == null || viewRect == null)
            return;

        m_ViewBounds = GetViewBounds();
        m_ContentBounds = GetContentBounds();
    }

    private Bounds GetViewBounds()
    {
        if (viewRect == null)
            return new Bounds();

        var vMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var vMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        var corners = m_Corners;
        viewRect.GetWorldCorners(corners);

        Matrix4x4 worldToLocalMatrix = viewRect.worldToLocalMatrix;
        for (int i = 0; i < 4; i++)
        {
            Vector3 v = worldToLocalMatrix.MultiplyPoint3x4(corners[i]);
            vMin = Vector3.Min(v, vMin);
            vMax = Vector3.Max(v, vMax);
        }

        var bounds = new Bounds(vMin, Vector3.zero);
        bounds.Encapsulate(vMax);
        return bounds;
    }

    private Bounds GetContentBounds()
    {
        if (m_Content == null || viewRect == null)
            return new Bounds();

        var vMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var vMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        var corners = m_Corners;
        Matrix4x4 worldToLocalMatrix = viewRect.worldToLocalMatrix;

        GetChildBounds(m_Content, ref vMin, ref vMax, corners, worldToLocalMatrix);

        var bounds = new Bounds(vMin, Vector3.zero);
        bounds.Encapsulate(vMax);
        return bounds;
    }

    private void GetChildBounds(RectTransform rect, ref Vector3 vMin, ref Vector3 vMax, Vector3[] corners, Matrix4x4 worldToLocalMatrix)
    {
        rect.GetWorldCorners(corners);
        for (int i = 0; i < 4; i++)
        {
            Vector3 v = worldToLocalMatrix.MultiplyPoint3x4(corners[i]);
            vMin = Vector3.Min(v, vMin);
            vMax = Vector3.Max(v, vMax);
        }

        for (int i = 0; i < rect.childCount; i++)
        {
            var child = rect.GetChild(i);
            if (child is RectTransform childRect)
            {
                GetChildBounds(childRect, ref vMin, ref vMax, corners, worldToLocalMatrix);
            }
        }
    }

    private void UpdateScrollbars()
    {
        if (m_Content == null || viewRect == null)
            return;

        m_SuppressOnScroll = true;

        if (m_HorizontalScrollbar != null)
        {
            m_HorizontalScrollbar.size = hScrollingNeeded ? Mathf.Clamp01(m_ViewBounds.size.x / m_ContentBounds.size.x) : 1f;
            m_HorizontalScrollbar.SetValueWithoutNotify(horizontalNormalizedPosition);
        }

        if (m_VerticalScrollbar != null)
        {
            m_VerticalScrollbar.size = vScrollingNeeded ? Mathf.Clamp01(m_ViewBounds.size.y / m_ContentBounds.size.y) : 1f;
            m_VerticalScrollbar.SetValueWithoutNotify(verticalNormalizedPosition);
        }

        m_SuppressOnScroll = false;
    }

    private void SetDirty()
    {
        if (!IsActive())
            return;
        if (m_RebuildingLayoutCount > 0)
            return;
        if (!CanvasUpdateRegistry.instance.IsRebuildingLayout())
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
        m_ViewRect = null;
        m_ParentScrollRect = FindParentScrollRect();
        SetDirty();
    }

    protected override void OnDidApplyAnimationProperties()
    {
        UpdateBounds();
    }
}

[Serializable]
public class ScrollRectEvent : UnityEvent<Vector2>
{
}
