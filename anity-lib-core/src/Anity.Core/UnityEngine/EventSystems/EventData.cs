using System;
using System.Collections.Generic;
using UnityEngine.UI;

namespace UnityEngine.EventSystems;

public class BaseEventData
{
    private readonly EventSystem? _eventSystem;
    private bool _used;
    private bool _usedBySelection;

    public BaseEventData(EventSystem? eventSystem)
    {
        _eventSystem = eventSystem;
    }

    public EventSystem? eventSystem => _eventSystem;
    public bool used => _used;

    public void Use()
    {
        _used = true;
    }

    public void Reset()
    {
        _used = false;
        _usedBySelection = false;
    }

    public bool IsUseSelected() => _usedBySelection;

    public void UseSelected()
    {
        _usedBySelection = true;
    }

    public BaseInputModule? currentInputModule { get; set; }
    public GameObject? selectedObject { get; set; }
}

public class PointerEventData : BaseEventData
{
    public enum InputButton
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

    public enum FramePressState
    {
        Pressed,
        Released,
        PressedAndReleased,
        NotChanged
    }

    public GameObject? pointerEnter { get; set; }
    public GameObject? lastPress { get; set; }
    public GameObject? rawPointerPress { get; set; }
    public GameObject? pointerDrag { get; set; }
    public GameObject? pointerPress { get; set; }
    public GameObject? pointerClick { get; set; }
    public RaycastResult pointerCurrentRaycast { get; set; }
    public RaycastResult pointerPressRaycast { get; set; }
    public List<GameObject> hovered { get; private set; } = new();

    public bool eligibleForClick { get; set; }
    public int pointerId { get; set; }
    public Vector2 position { get; set; }
    public Vector2 delta { get; set; }
    public Vector2 pressPosition { get; set; }
    public Vector2 scrollDelta { get; set; }
    public int clickCount { get; set; }
    public float clickTime { get; set; }
    public Vector2 lastPosition { get; set; }
    public bool dragging { get; set; }
    public bool useDragThreshold { get; set; } = true;
    public InputButton button { get; set; }
    public Camera? enterEventCamera { get; set; }
    public Camera? pressEventCamera { get; set; }
    public bool IsPointerMoving() => delta.sqrMagnitude > 0.0f;
    public bool IsScrolling() => scrollDelta.sqrMagnitude > 0.0f;

    public PointerEventData(EventSystem eventSystem) : base(eventSystem) { }

    public override string ToString() => $"position: {position}, delta: {delta}, eligibleForClick: {eligibleForClick}, pointerEnter: {pointerEnter}, pressPosition: {pressPosition}, pointerDrag: {pointerDrag}, clickCount: {clickCount}, clickTime: {clickTime}, button: {button}";
}

public class AxisEventData : BaseEventData
{
    public Vector2 moveVector { get; set; }
    public MoveDirection moveDir { get; set; }

    public AxisEventData(EventSystem eventSystem) : base(eventSystem) { }
}

public enum MoveDirection
{
    Left, Up, Right, Down, None
}

public struct RaycastResult
{
    private GameObject? _gameObject;

    public GameObject? gameObject
    {
        get => _gameObject;
        set => _gameObject = value;
    }

    public BaseRaycaster? module { get; set; }
    public float distance { get; set; }
    public float index { get; set; }
    public int depth { get; set; }
    public int sortingLayer { get; set; }
    public int sortingOrder { get; set; }
    public Vector3 worldPosition { get; set; }
    public Vector3 worldNormal { get; set; }
    public Vector2 screenPosition { get; set; }
    public int displayIndex { get; set; }

    public bool isValid => gameObject is not null && module is not null;

    public void Clear()
    {
        _gameObject = null;
        module = null;
        distance = 0;
        index = 0;
        depth = 0;
        sortingLayer = 0;
        sortingOrder = 0;
        worldPosition = Vector3.zero;
        worldNormal = Vector3.zero;
        screenPosition = Vector2.zero;
        displayIndex = 0;
    }

    public override string ToString() => $"Game Object: {gameObject}, module: {module}, distance: {distance}, sortingLayer: {sortingLayer}, sortingOrder: {sortingOrder}";
}

public class RaycastResultComparer : IComparer<RaycastResult>
{
    public int Compare(RaycastResult lhs, RaycastResult rhs)
    {
        if (lhs.module != rhs.module)
        {
            if (lhs.module is not null && rhs.module is not null)
            {
                var lEvent = lhs.module.eventCamera;
                var rEvent = rhs.module.eventCamera;
                if (lEvent is not null && rEvent is not null && lEvent.depth != rEvent.depth)
                    return rEvent.depth.CompareTo(lEvent.depth);
                if (lhs.module.sortOrderPriority != rhs.module.sortOrderPriority)
                    return rhs.module.sortOrderPriority.CompareTo(lhs.module.sortOrderPriority);
                if (lhs.module.renderOrderPriority != rhs.module.renderOrderPriority)
                    return rhs.module.renderOrderPriority.CompareTo(lhs.module.renderOrderPriority);
            }
        }

        if (lhs.sortingLayer != rhs.sortingLayer)
        {
            var rid = SortingLayer.GetLayerValueFromID(rhs.sortingLayer);
            var lid = SortingLayer.GetLayerValueFromID(lhs.sortingLayer);
            return rid.CompareTo(lid);
        }

        if (lhs.sortingOrder != rhs.sortingOrder)
            return rhs.sortingOrder.CompareTo(lhs.sortingOrder);

        if (lhs.depth != rhs.depth)
            return rhs.depth.CompareTo(lhs.depth);

        if (lhs.distance != rhs.distance)
            return lhs.distance.CompareTo(rhs.distance);

        return rhs.index.CompareTo(lhs.index);
    }
}

internal static class SortingLayer
{
    private static readonly Dictionary<int, int> _layerValues = new();
    private static readonly Dictionary<string, int> _nameToId = new();

    static SortingLayer()
    {
        _nameToId["Default"] = 0;
        _layerValues[0] = 0;
    }

    public static int GetLayerValueFromID(int id)
    {
        return _layerValues.TryGetValue(id, out var v) ? v : 0;
    }

    public static int NameToID(string name)
    {
        return _nameToId.TryGetValue(name, out var id) ? id : 0;
    }
}
