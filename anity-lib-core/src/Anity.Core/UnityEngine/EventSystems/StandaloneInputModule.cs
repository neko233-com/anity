using System;
using System.Collections.Generic;

namespace UnityEngine.EventSystems;

public class StandaloneInputModule : PointerInputModule
{
    private float _nextAction;
    private float _prevActionTime;
    private Vector2 _lastMoveVector;

    public string horizontalAxis { get; set; } = "Horizontal";
    public string verticalAxis { get; set; } = "Vertical";
    public string submitButton { get; set; } = "Submit";
    public string cancelButton { get; set; } = "Cancel";
    public string inputActionsAsset { get; set; }
    public float inputActionsPerSecond { get; set; } = 10f;
    public float repeatDelay { get; set; } = 0.5f;
    public float forceModuleActive { get; set; }
    public bool allowActivationOnMobileDevice { get; set; } = true;

    public override bool IsModuleSupported() => true;

    public override bool ShouldActivateModule()
    {
        if (!base.ShouldActivateModule()) return false;
        if (forceModuleActive > 0f) return true;
        var needsActivation = false;
        if (forceModuleActive > 0) needsActivation = true;
        if (input.GetButtonDown(submitButton)) needsActivation = true;
        if (input.GetButtonDown(cancelButton)) needsActivation = true;
        if (Mathf.Abs(input.GetAxisRaw(horizontalAxis)) > 0.1f) needsActivation = true;
        if (Mathf.Abs(input.GetAxisRaw(verticalAxis)) > 0.1f) needsActivation = true;
        if (input.touchSupported && input.touchCount > 0) needsActivation = true;
        if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0)) needsActivation = true;
        if (Input.mouseScrollDelta != Vector2.zero) needsActivation = true;
        return needsActivation;
    }

    public override void ActivateModule()
    {
        base.ActivateModule();
        var toSelect = eventSystem.currentSelectedGameObject;
        if (toSelect is null)
            toSelect = eventSystem.currentSelectedGameObject;
        eventSystem.SetSelectedGameObject(toSelect, GetBaseEventData());
    }

    public override void DeactivateModule()
    {
        base.DeactivateModule();
        ClearSelection();
    }

    public override void Process()
    {
        if (!eventSystem.isFocused && ShouldIgnoreEventsOnNoFocus())
            return;

        var usedEvent = SendUpdateEventToSelectedObject();

        if (!usedEvent)
            usedEvent |= SendSubmitEventToSelectedObject();

        if (!SendMoveEventToSelectedObject())
        {
            ProcessMouseEvent();
            if (input.touchSupported)
                ProcessTouchEvents();
        }
    }

    private bool ShouldIgnoreEventsOnNoFocus() => true;

    protected void ProcessMouseEvent()
    {
        ProcessMouseEvent(0);
    }

    protected void ProcessMouseEvent(int id)
    {
        var mouseData = GetMousePointerEventData(id);
        var leftButtonData = mouseData;

        ProcessMousePress(leftButtonData);
        ProcessMove(leftButtonData.buttonData);
        ProcessDrag(leftButtonData.buttonData);

        if (Mathf.Abs(leftButtonData.buttonData.scrollDelta.sqrMagnitude) > 0f)
        {
            var scrollHandler = ExecuteEvents.GetEventHandler<IScrollHandler>(leftButtonData.buttonData.pointerCurrentRaycast.gameObject);
            ExecuteEvents.ExecuteHierarchy(scrollHandler, leftButtonData.buttonData, ExecuteEvents.scrollHandler);
        }
    }

    private MouseButtonEventData GetMousePointerEventData(int id)
    {
        var leftData = base.GetMousePointerEventData(kMouseLeftId);
        var buttonData = base.GetMouseButtonEventData(id);
        buttonData.buttonData = leftData;
        buttonData.buttonState = StateForMouseButton(id);
        return buttonData;
    }

    private new MouseButtonEventData GetMouseButtonEventData(int id) => base.GetMouseButtonEventData(id);

    private void ProcessTouchEvents()
    {
        for (var i = 0; i < input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.type == TouchType.Indirect) continue;

            var pressed = touch.phase == TouchPhase.Began;
            var released = touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended;
            var pointerData = GetTouchPointerEventData(touch, out _, out _);

            ProcessTouchPress(pointerData, pressed, released);

            if (!released)
            {
                ProcessMove(pointerData);
                ProcessDrag(pointerData);
            }
            else
            {
                RemovePointerData(pointerData);
            }
        }
    }

    protected bool SendMoveEventToSelectedObject()
    {
        var time = Time.unscaledTime;
        var x = input.GetAxisRaw(horizontalAxis);
        var y = input.GetAxisRaw(verticalAxis);
        var movement = new Vector2(x, y);
        var move = GetAxisEventData(movement.x, movement.y, 0.6f);

        if (move.moveDir != MoveDirection.None)
        {
            if (_nextAction <= time || _lastMoveVector != movement)
            {
                ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, move, ExecuteEvents.moveHandler);
                _prevActionTime = time;
                _nextAction = time + repeatDelay;
            }
            else if (time > _nextAction)
            {
                ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, move, ExecuteEvents.moveHandler);
                _nextAction = time + (1f / inputActionsPerSecond);
            }
            _lastMoveVector = movement;
        }
        else
        {
            _nextAction = time;
            _lastMoveVector = Vector2.zero;
        }

        return move.used;
    }

    protected new bool SendSubmitEventToSelectedObject()
    {
        var data = GetBaseEventData();
        if (input.GetButtonDown(submitButton))
            ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.submitHandler);
        if (input.GetButtonDown(cancelButton))
            ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.cancelHandler);
        return data.used;
    }

    private void ClearSelection()
    {
        var baseEventData = GetBaseEventData();
        foreach (var pointer in m_PointerData.Values)
        {
            HandlePointerExitAndEnter(pointer, null);
        }
        m_PointerData.Clear();
        eventSystem.SetSelectedGameObject(null, baseEventData);
    }

    public override bool IsPointerOverGameObject(int pointerId)
    {
        if (m_PointerData.TryGetValue(pointerId, out var data))
            return data.pointerEnter is not null;
        return false;
    }
}
