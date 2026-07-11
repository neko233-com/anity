using System;
using System.Collections.Generic;

namespace UnityEngine.EventSystems;

public abstract class PointerInputModule : BaseInputModule
{
    public const int kMouseLeftId = -1;
    public const int kMouseRightId = -2;
    public const int kMouseMiddleId = -3;
    public const int kFakeTouchesId = -4;

    protected readonly Dictionary<int, PointerEventData> m_PointerData = new();

    public bool GetPointerData(int id, out PointerEventData data, bool create)
    {
        if (!m_PointerData.TryGetValue(id, out data) && create)
        {
            data = new PointerEventData(eventSystem) { pointerId = id };
            m_PointerData[id] = data;
            return false;
        }
        return data is not null;
    }

    protected void RemovePointerData(PointerEventData data)
    {
        if (data is null) return;
        m_PointerData.Remove(data.pointerId);
    }

    protected PointerEventData GetTouchPointerEventData(Touch input, out bool pressed, out bool released)
    {
        var created = GetPointerData(input.fingerId, out var pointerData, true);
        pointerData.Reset();
        pressed = created || input.phase == TouchPhase.Began;
        released = input.phase == TouchPhase.Canceled || input.phase == TouchPhase.Ended;

        if (pressed)
        {
            pointerData.delta = Vector2.zero;
            pointerData.position = input.position;
            pointerData.pressPosition = input.position;
            pointerData.eligibleForClick = true;
            pointerData.clickTime = Time.unscaledTime;
            pointerData.clickCount = 1;
        }
        else if (released)
        {
            pointerData = m_PointerData[input.fingerId];
        }
        else
        {
            pointerData.delta = input.deltaPosition;
            pointerData.position = input.position;
        }

        return pointerData;
    }

    protected PointerEventData GetMousePointerEventData(int id = 0)
    {
        var created = GetPointerData(kMouseLeftId + id, out var pointerData, true);
        pointerData.Reset();
        pointerData.button = id switch
        {
            0 => PointerEventData.InputButton.Left,
            1 => PointerEventData.InputButton.Right,
            2 => PointerEventData.InputButton.Middle,
            _ => PointerEventData.InputButton.Left
        };

        var pos = input.mousePosition;
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            pointerData.position = new Vector2(-Screen.width * 2, -Screen.height * 2);
            pointerData.delta = Vector2.zero;
        }
        else
        {
            pointerData.delta = pos - pointerData.position;
            pointerData.position = pos;
        }

        pointerData.scrollDelta = input.mouseScrollDelta;
        pointerData.button = id switch
        {
            1 => PointerEventData.InputButton.Right,
            2 => PointerEventData.InputButton.Middle,
            _ => PointerEventData.InputButton.Left
        };

        eventSystem.RaycastAll(pointerData, m_RaycastResultCache);
        var raycast = FindFirstRaycast(m_RaycastResultCache);
        pointerData.pointerCurrentRaycast = raycast;
        m_RaycastResultCache.Clear();
        return pointerData;
    }

    private static readonly List<RaycastResult> m_RaycastResultCache = new();

    protected void ProcessTouchPress(PointerEventData pointerEvent, bool pressed, bool released)
    {
        var currentOverGo = pointerEvent.pointerCurrentRaycast.gameObject;

        if (pressed)
        {
            pointerEvent.eligibleForClick = true;
            pointerEvent.delta = Vector2.zero;
            pointerEvent.dragging = false;
            pointerEvent.useDragThreshold = true;
            pointerEvent.pressPosition = pointerEvent.position;
            pointerEvent.pointerPressRaycast = pointerEvent.pointerCurrentRaycast;
            DeselectIfSelectionChanged(currentOverGo, pointerEvent);

            if (pointerEvent.pointerEnter != currentOverGo)
            {
                HandlePointerExitAndEnter(pointerEvent, currentOverGo);
            }

            var newPressed = ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.pointerDownHandler);
            if (newPressed is null)
                newPressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);

            var clickTime = Time.unscaledTime;
            if (newPressed == pointerEvent.lastPress)
            {
                var diff = clickTime - pointerEvent.clickTime;
                if (diff < 0.3f)
                    pointerEvent.clickCount++;
                else
                    pointerEvent.clickCount = 1;
                pointerEvent.clickTime = clickTime;
            }
            else
            {
                pointerEvent.clickCount = 1;
            }

            pointerEvent.pointerPress = newPressed;
            pointerEvent.rawPointerPress = currentOverGo;
            pointerEvent.clickTime = clickTime;
            pointerEvent.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(currentOverGo);
            if (pointerEvent.pointerDrag is not null)
                ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.initializePotentialDrag);
        }

        if (released)
        {
            ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);
            var pointerUpHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
            if (pointerEvent.pointerPress == pointerUpHandler && pointerEvent.eligibleForClick)
            {
                ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerClickHandler);
            }
            else if (pointerEvent.pointerDrag is not null && pointerEvent.dragging)
            {
                ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.dropHandler);
            }

            pointerEvent.eligibleForClick = false;
            pointerEvent.pointerPress = null;
            pointerEvent.rawPointerPress = null;

            if (pointerEvent.pointerDrag is not null && pointerEvent.dragging)
                ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.endDragHandler);

            pointerEvent.dragging = false;
            pointerEvent.pointerDrag = null;

            if (currentOverGo != pointerEvent.pointerEnter)
            {
                HandlePointerExitAndEnter(pointerEvent, null);
                HandlePointerExitAndEnter(pointerEvent, currentOverGo);
            }
        }
    }

    protected void ProcessMousePress(MouseButtonEventData data)
    {
        var pointerEvent = data.buttonData;
        var currentOverGo = pointerEvent.pointerCurrentRaycast.gameObject;

        if (data.PressedThisFrame())
        {
            pointerEvent.eligibleForClick = true;
            pointerEvent.delta = Vector2.zero;
            pointerEvent.dragging = false;
            pointerEvent.useDragThreshold = true;
            pointerEvent.pressPosition = pointerEvent.position;
            pointerEvent.pointerPressRaycast = pointerEvent.pointerCurrentRaycast;
            DeselectIfSelectionChanged(currentOverGo, pointerEvent);

            var newPressed = ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.pointerDownHandler);
            if (newPressed is null)
                newPressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);

            var clickTime = Time.unscaledTime;
            if (newPressed == pointerEvent.lastPress)
            {
                var diff = clickTime - pointerEvent.clickTime;
                if (diff < 0.3f)
                    pointerEvent.clickCount++;
                else
                    pointerEvent.clickCount = 1;
                pointerEvent.clickTime = clickTime;
            }
            else
            {
                pointerEvent.clickCount = 1;
            }

            pointerEvent.pointerPress = newPressed;
            pointerEvent.rawPointerPress = currentOverGo;
            pointerEvent.clickTime = clickTime;
            pointerEvent.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(currentOverGo);
            if (pointerEvent.pointerDrag is not null)
                ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.initializePotentialDrag);
        }

        if (data.ReleasedThisFrame())
        {
            ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);
            var pointerUpHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
            if (pointerEvent.pointerPress == pointerUpHandler && pointerEvent.eligibleForClick)
            {
                ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerClickHandler);
            }
            else if (pointerEvent.pointerDrag is not null && pointerEvent.dragging)
            {
                ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.dropHandler);
            }

            pointerEvent.eligibleForClick = false;
            pointerEvent.pointerPress = null;
            pointerEvent.rawPointerPress = null;

            if (pointerEvent.pointerDrag is not null && pointerEvent.dragging)
                ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.endDragHandler);

            pointerEvent.dragging = false;
            pointerEvent.pointerDrag = null;

            if (currentOverGo != pointerEvent.pointerEnter)
            {
                HandlePointerExitAndEnter(pointerEvent, null);
                HandlePointerExitAndEnter(pointerEvent, currentOverGo);
            }
        }
    }

    protected void ProcessMove(PointerEventData pointerEvent)
    {
        var targetGO = pointerEvent.pointerCurrentRaycast.gameObject;
        HandlePointerExitAndEnter(pointerEvent, targetGO);
    }

    protected void ProcessDrag(PointerEventData pointerEvent)
    {
        if (!pointerEvent.IsPointerMoving() || pointerEvent.pointerDrag is null || Cursor.lockState == CursorLockMode.Locked)
            return;

        if (!pointerEvent.dragging && ShouldStartDrag(pointerEvent.pressPosition, pointerEvent.position, eventSystem.pixelDragThreshold, pointerEvent.useDragThreshold))
        {
            ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.beginDragHandler);
            pointerEvent.dragging = true;
        }

        if (pointerEvent.dragging)
        {
            if (pointerEvent.pointerPress != pointerEvent.pointerDrag)
            {
                ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);
                pointerEvent.eligibleForClick = false;
                pointerEvent.pointerPress = null;
                pointerEvent.rawPointerPress = null;
            }
            ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.dragHandler);
        }
    }

    private bool ShouldStartDrag(Vector2 pressPos, Vector2 currentPos, float threshold, bool useDragThreshold)
    {
        if (!useDragThreshold) return true;
        return (pressPos - currentPos).sqrMagnitude >= threshold * threshold;
    }

    protected void DeselectIfSelectionChanged(GameObject? currentOverGo, BaseEventData pointerEvent)
    {
        var selectHandlerGO = ExecuteEvents.GetEventHandler<ISelectHandler>(currentOverGo);
        if (selectHandlerGO != eventSystem.currentSelectedGameObject)
        {
            eventSystem.SetSelectedGameObject(selectHandlerGO, pointerEvent);
        }
    }

    public static RaycastResult FindFirstRaycast(List<RaycastResult> candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].gameObject is null) continue;
            return candidates[i];
        }
        return default;
    }

    protected class MouseButtonEventData
    {
        public PointerEventData.FramePressState buttonState;
        public PointerEventData buttonData = null!;

        public bool PressedThisFrame()
        {
            return buttonState == PointerEventData.FramePressState.Pressed || buttonState == PointerEventData.FramePressState.PressedAndReleased;
        }

        public bool ReleasedThisFrame()
        {
            return buttonState == PointerEventData.FramePressState.Released || buttonState == PointerEventData.FramePressState.PressedAndReleased;
        }
    }

    private readonly MouseButtonEventData[] m_MouseButtonData = new MouseButtonEventData[3];

    protected MouseButtonEventData GetMouseButtonEventData(int id)
    {
        if (m_MouseButtonData[id] is null)
            m_MouseButtonData[id] = new MouseButtonEventData();
        return m_MouseButtonData[id];
    }

    protected void CopyFromTo(PointerEventData from, PointerEventData to)
    {
        to.position = from.position;
        to.delta = from.delta;
        to.scrollDelta = from.scrollDelta;
        to.pointerCurrentRaycast = from.pointerCurrentRaycast;
    }
}

public enum CursorLockMode
{
    None,
    Locked,
    Confined
}

public static class Cursor
{
    public static CursorLockMode lockState { get; set; } = CursorLockMode.None;
    public static bool visible { get; set; } = true;
}
