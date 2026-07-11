using System;
using System.Collections.Generic;

namespace UnityEngine.EventSystems;

public static class ExecuteEvents
{
    public static readonly ExecuteEvents.EventFunction<IPointerEnterHandler> pointerEnterHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IPointerExitHandler> pointerExitHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IPointerDownHandler> pointerDownHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IPointerUpHandler> pointerUpHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IPointerClickHandler> pointerClickHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IInitializePotentialDragHandler> initializePotentialDrag = Execute;
    public static readonly ExecuteEvents.EventFunction<IBeginDragHandler> beginDragHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IDragHandler> dragHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IEndDragHandler> endDragHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IDropHandler> dropHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IScrollHandler> scrollHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IUpdateSelectedHandler> updateSelectedHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<ISelectHandler> selectHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IDeselectHandler> deselectHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IMoveHandler> moveHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<ISubmitHandler> submitHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<ICancelHandler> cancelHandler = Execute;
    public static readonly ExecuteEvents.EventFunction<IPointerMoveHandler> pointerMoveHandler = Execute;

    public delegate void EventFunction<in T1>(T1 handler, BaseEventData eventData) where T1 : class, IEventSystemHandler;

    private static void Execute(IPointerEnterHandler handler, BaseEventData eventData) => handler.OnPointerEnter(ValidateEventData<PointerEventData>(eventData));
    private static void Execute(IPointerExitHandler handler, BaseEventData eventData) => handler.OnPointerExit(ValidateEventData<PointerEventData>(eventData));
    private static void Execute(IPointerDownHandler handler, BaseEventData eventData) => handler.OnPointerDown(ValidateEventData<PointerEventData>(eventData));
    private static void Execute(IPointerUpHandler handler, BaseEventData eventData) => handler.OnPointerUp(ValidateEventData<PointerEventData>(eventData));
    private static void Execute(IPointerClickHandler handler, BaseEventData eventData) => handler.OnPointerClick(ValidateEventData<PointerEventData>(eventData));
    private static void Execute(IInitializePotentialDragHandler handler, BaseEventData eventData) => handler.OnInitializePotentialDrag(ValidateEventData<PointerEventData>(eventData));
    private static void Execute(IBeginDragHandler handler, BaseEventData eventData) => handler.OnBeginDrag(ValidateEventData<PointerEventData>(eventData));
    private static void Execute(IDragHandler handler, BaseEventData eventData) => handler.OnDrag(ValidateEventData<PointerEventData>(eventData));
    private static void Execute(IEndDragHandler handler, BaseEventData eventData) => handler.OnEndDrag(ValidateEventData<PointerEventData>(eventData));
    private static void Execute(IDropHandler handler, BaseEventData eventData) => handler.OnDrop(ValidateEventData<PointerEventData>(eventData));
    private static void Execute(IScrollHandler handler, BaseEventData eventData) => handler.OnScroll(ValidateEventData<PointerEventData>(eventData));
    private static void Execute(IUpdateSelectedHandler handler, BaseEventData eventData) => handler.OnUpdateSelected(eventData);
    private static void Execute(ISelectHandler handler, BaseEventData eventData) => handler.OnSelect(eventData);
    private static void Execute(IDeselectHandler handler, BaseEventData eventData) => handler.OnDeselect(eventData);
    private static void Execute(IMoveHandler handler, BaseEventData eventData) => handler.OnMove(ValidateEventData<AxisEventData>(eventData));
    private static void Execute(ISubmitHandler handler, BaseEventData eventData) => handler.OnSubmit(eventData);
    private static void Execute(ICancelHandler handler, BaseEventData eventData) => handler.OnCancel(eventData);
    private static void Execute(IPointerMoveHandler handler, BaseEventData eventData) => handler.OnPointerMove(ValidateEventData<PointerEventData>(eventData));

    public static T ValidateEventData<T>(BaseEventData data) where T : BaseEventData
    {
        return data as T ?? throw new ArgumentException($"Invalid type: {data?.GetType()} passed as {typeof(T)}");
    }

    public static GameObject? ExecuteHierarchy<T>(GameObject root, BaseEventData eventData, EventFunction<T> callbackFunction) where T : class, IEventSystemHandler
    {
        if (root is null) return null;
        var t = root.transform;
        while (t is not null)
        {
            if (Execute(t.gameObject, eventData, callbackFunction) is not null)
                return t.gameObject;
            t = t.parent;
        }
        return null;
    }

    public static GameObject? Execute<T>(GameObject target, BaseEventData eventData, EventFunction<T> functor) where T : class, IEventSystemHandler
    {
        if (target is null) return null;
        var components = target.GetComponents<MonoBehaviour>();
        GameObject? result = null;
        foreach (var comp in components)
        {
            if (comp is T handler)
            {
                functor(handler, eventData);
                result = target;
            }
        }
        return result;
    }

    public static bool CanHandleEvent<T>(GameObject go) where T : class, IEventSystemHandler
    {
        if (go is null) return false;
        var components = go.GetComponents<MonoBehaviour>();
        foreach (var comp in components)
        {
            if (comp is T) return true;
        }
        return false;
    }

    public static GameObject? GetEventHandler<T>(GameObject root) where T : class, IEventSystemHandler
    {
        if (root is null) return null;
        var t = root.transform;
        while (t is not null)
        {
            if (CanHandleEvent<T>(t.gameObject))
                return t.gameObject;
            t = t.parent;
        }
        return null;
    }
}

public interface IEventSystemHandler { }
public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData eventData); }
public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData eventData); }
public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData eventData); }
public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData eventData); }
public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData eventData); }
public interface IPointerMoveHandler : IEventSystemHandler { void OnPointerMove(PointerEventData eventData); }
public interface IInitializePotentialDragHandler : IEventSystemHandler { void OnInitializePotentialDrag(PointerEventData eventData); }
public interface IBeginDragHandler : IEventSystemHandler { void OnBeginDrag(PointerEventData eventData); }
public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData eventData); }
public interface IEndDragHandler : IEventSystemHandler { void OnEndDrag(PointerEventData eventData); }
public interface IDropHandler : IEventSystemHandler { void OnDrop(PointerEventData eventData); }
public interface IScrollHandler : IEventSystemHandler { void OnScroll(PointerEventData eventData); }
public interface IUpdateSelectedHandler : IEventSystemHandler { void OnUpdateSelected(BaseEventData eventData); }
public interface ISelectHandler : IEventSystemHandler { void OnSelect(BaseEventData eventData); }
public interface IDeselectHandler : IEventSystemHandler { void OnDeselect(BaseEventData eventData); }
public interface IMoveHandler : IEventSystemHandler { void OnMove(AxisEventData eventData); }
public interface ISubmitHandler : IEventSystemHandler { void OnSubmit(BaseEventData eventData); }
public interface ICancelHandler : IEventSystemHandler { void OnCancel(BaseEventData eventData); }
