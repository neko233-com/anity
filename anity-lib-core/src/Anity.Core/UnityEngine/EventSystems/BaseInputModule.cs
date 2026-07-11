using System;
using System.Collections.Generic;
using UnityEngine.UI;

namespace UnityEngine.EventSystems;

public abstract class BaseInputModule : UIBehaviour
{
    protected BaseInput? m_InputOverride;
    public BaseInput input { get; set; } = new BaseInput();

    public BaseInput inputOverride
    {
        get => m_InputOverride ?? input;
        set => m_InputOverride = value;
    }

    protected EventSystem? eventSystem => EventSystem.current;

    protected override void OnEnable()
    {
        base.OnEnable();
        EventSystem.current?.UpdateModules();
    }

    protected override void OnDisable()
    {
        EventSystem.current?.UpdateModules();
        base.OnDisable();
    }

    public virtual bool IsModuleSupported() => true;
    public virtual bool ShouldActivateModule() => isActiveAndEnabled && gameObject.activeInHierarchy;

    public virtual void ActivateModule()
    {
    }

    public virtual void DeactivateModule()
    {
    }

    public virtual void UpdateModule()
    {
    }

    public virtual bool IsPointerOverGameObject(int pointerId) => false;

    public virtual void Process()
    {
    }

    protected static void ExecuteEventsExecute<T>(GameObject target, BaseEventData eventData, ExecuteEvents.EventFunction<T> functor) where T : class, IEventSystemHandler
    {
        ExecuteEvents.Execute(target, eventData, functor);
    }

    protected static GameObject? ExecuteEventsExecuteHierarchy<T>(GameObject root, BaseEventData eventData, ExecuteEvents.EventFunction<T> callbackFunction) where T : class, IEventSystemHandler
    {
        return ExecuteEvents.ExecuteHierarchy(root, eventData, callbackFunction);
    }

    protected GameObject? FindCommonRoot(GameObject g1, GameObject g2)
    {
        if (g1 is null || g2 is null) return null;
        var t1 = g1.transform;
        while (t1 is not null)
        {
            var t2 = g2.transform;
            while (t2 is not null)
            {
                if (t1 == t2) return t1.gameObject;
                t2 = t2.parent;
            }
            t1 = t1.parent;
        }
        return null;
    }

    protected void HandlePointerExitAndEnter(PointerEventData currentPointerData, GameObject? newEnterTarget)
    {
        if (newEnterTarget is null || currentPointerData.pointerEnter != newEnterTarget)
        {
            var commonRoot = FindCommonRoot(currentPointerData.pointerEnter, newEnterTarget);

            if (currentPointerData.pointerEnter is not null)
            {
                var oldPointerEnter = currentPointerData.pointerEnter;
                var t = oldPointerEnter.transform;
                while (t is not null)
                {
                    if (t.gameObject == commonRoot) break;
                    var handler = ExecuteEvents.GetEventHandler<IPointerExitHandler>(t.gameObject);
                    if (handler is not null)
                        ExecuteEvents.Execute(handler, currentPointerData, ExecuteEvents.pointerExitHandler);
                    currentPointerData.hovered.Remove(t.gameObject);
                    t = t.parent;
                }
            }

            currentPointerData.pointerEnter = newEnterTarget;
            if (newEnterTarget is not null)
            {
                var t = newEnterTarget.transform;
                while (t is not null)
                {
                    var handler = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(t.gameObject);
                    if (handler is not null)
                    {
                        ExecuteEvents.Execute(handler, currentPointerData, ExecuteEvents.pointerEnterHandler);
                        currentPointerData.hovered.Add(handler);
                    }
                    if (t.gameObject == commonRoot) break;
                    t = t.parent;
                }
            }
        }
        else
        {
            if (newEnterTarget is not null && !currentPointerData.hovered.Contains(newEnterTarget))
            {
                var handler = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(newEnterTarget);
                if (handler is not null)
                {
                    ExecuteEvents.Execute(handler, currentPointerData, ExecuteEvents.pointerEnterHandler);
                    currentPointerData.hovered.Add(handler);
                }
            }

            for (var i = currentPointerData.hovered.Count - 1; i >= 0; i--)
            {
                var h = currentPointerData.hovered[i];
                if (h is null)
                {
                    currentPointerData.hovered.RemoveAt(i);
                    continue;
                }
                if (!newEnterTarget.transform.IsChildOf(h.transform) && h != newEnterTarget)
                {
                    var exitHandler = h.GetComponent<IPointerExitHandler>();
                    if (exitHandler is not null)
                        exitHandler.OnPointerExit(currentPointerData);
                    currentPointerData.hovered.RemoveAt(i);
                }
            }
        }
    }

    protected virtual void ProcessMove(PointerEventData pointerEvent)
    {
        var targetGO = pointerEvent.pointerCurrentRaycast.gameObject;
        HandlePointerExitAndEnter(pointerEvent, targetGO);
    }

    protected virtual void ProcessDrag(PointerEventData pointerEvent)
    {
        if (!pointerEvent.IsPointerMoving() || pointerEvent.pointerDrag is null)
            return;

        if (!pointerEvent.dragging && ShouldStartDrag(pointerEvent.pressPosition, pointerEvent.position, (float)eventSystem.pixelDragThreshold, pointerEvent.useDragThreshold))
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

    private static bool ShouldStartDrag(Vector2 pressPos, Vector2 currentPos, float threshold, bool useDragThreshold)
    {
        if (!useDragThreshold) return true;
        return (pressPos - currentPos).sqrMagnitude >= threshold * threshold;
    }

    protected virtual bool EnableMouseInput(int pointerId) => pointerId == PointerInputModule.kMouseLeftId || pointerId == PointerInputModule.kMouseRightId || pointerId == PointerInputModule.kMouseMiddleId;

    protected internal static PointerEventData.FramePressState StateForMouseButton(int buttonId)
    {
        var pressed = Input.GetMouseButtonDown(buttonId);
        var released = Input.GetMouseButtonUp(buttonId);
        if (pressed && released) return PointerEventData.FramePressState.PressedAndReleased;
        if (pressed) return PointerEventData.FramePressState.Pressed;
        if (released) return PointerEventData.FramePressState.Released;
        return PointerEventData.FramePressState.NotChanged;
    }

    protected bool SendUpdateEventToSelectedObject()
    {
        if (eventSystem?.currentSelectedGameObject is null) return false;
        var data = GetBaseEventData();
        ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.updateSelectedHandler);
        return data.used;
    }

    protected bool SendMoveEventToSelectedObject()
    {
        var horizontal = Input.GetAxisRaw("Horizontal");
        var vertical = Input.GetAxisRaw("Vertical");
        var wasHeld = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;
        if (!wasHeld) return false;

        var axisData = GetAxisEventData(horizontal, vertical, 0.6f);
        var selected = eventSystem?.currentSelectedGameObject;
        if (selected is not null)
        {
            ExecuteEvents.Execute(selected, axisData, ExecuteEvents.moveHandler);
        }
        return axisData.used;
    }

    protected bool SendSubmitEventToSelectedObject()
    {
        var data = GetBaseEventData();
        if (Input.GetButtonDown("Submit"))
        {
            ExecuteEvents.Execute(eventSystem?.currentSelectedGameObject, data, ExecuteEvents.submitHandler);
        }
        if (Input.GetButtonDown("Cancel"))
        {
            ExecuteEvents.Execute(eventSystem?.currentSelectedGameObject, data, ExecuteEvents.cancelHandler);
        }
        return data.used;
    }

    private BaseEventData? _baseEventData;
    protected BaseEventData GetBaseEventData()
    {
        _baseEventData ??= new BaseEventData(eventSystem);
        _baseEventData.Reset();
        return _baseEventData;
    }

    private AxisEventData? _axisEventData;
    protected AxisEventData GetAxisEventData(float x, float y, float moveDeadZone)
    {
        _axisEventData ??= new AxisEventData(eventSystem);
        _axisEventData.Reset();
        _axisEventData.moveVector = new Vector2(x, y);
        _axisEventData.moveDir = DetermineMoveDirection(x, y, moveDeadZone);
        return _axisEventData;
    }

    private static MoveDirection DetermineMoveDirection(float x, float y, float deadZone)
    {
        var dir = new Vector2(x, y);
        if (dir.sqrMagnitude < deadZone * deadZone) return MoveDirection.None;
        if (Mathf.Abs(x) > Mathf.Abs(y))
            return x > 0 ? MoveDirection.Right : MoveDirection.Left;
        return y > 0 ? MoveDirection.Up : MoveDirection.Down;
    }

    protected static bool ShouldIgnoreEvents(GameObject? go)
    {
        if (go is null) return false;
        return false;
    }

    public override string ToString()
    {
        return GetType().Name;
    }
}

public class BaseInput : MonoBehaviour
{
    public virtual bool mousePresent => Input.mousePresent;
    public virtual Vector2 mousePosition => new Vector2(Input.mousePosition.x, Input.mousePosition.y);
    public virtual Vector2 mouseScrollDelta => Input.mouseScrollDelta;
    public virtual bool touchSupported => Input.GetTouchSupported();
    public virtual int touchCount => Input.touchCount;
    public virtual float GetAxisRaw(string axisName) => Input.GetAxisRaw(axisName);
    public virtual bool GetButtonDown(string buttonName) => Input.GetButtonDown(buttonName);
}
