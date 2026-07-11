using System;
using System.Collections.Generic;
using UnityEngine.UI;

namespace UnityEngine.EventSystems;

public abstract class BaseRaycaster : UIBehaviour
{
    public abstract int sortOrderPriority { get; }
    public abstract int renderOrderPriority { get; }
    public abstract Camera eventCamera { get; }

    public virtual int priority => 0;

    public abstract void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList);

    protected override void OnEnable()
    {
        base.OnEnable();
        EventSystem?.UpdateRaycasters();
    }

    protected override void OnDisable()
    {
        EventSystem?.UpdateRaycasters();
        base.OnDisable();
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        EventSystem?.UpdateRaycasters();
    }

    protected override void OnTransformParentChanged()
    {
        base.OnTransformParentChanged();
        EventSystem?.UpdateRaycasters();
    }

    public EventSystem? EventSystem => EventSystem.current;

    public override string ToString() => $"Raycaster: {name}, module: {GetType().Name}";
}

public class EventSystem : UIBehaviour
{
    private static EventSystem? _current;
    private readonly List<BaseInputModule> _systemInputModules = new();
    private readonly List<BaseRaycaster> _raycasters = new();
    private BaseInputModule? _currentModule;
    private GameObject? _currentSelected;
    private bool _alreadyChanged;
    private bool _hasFocus = true;

    private static int _eventSystemCount;
    private readonly int _eventSystemId = _eventSystemCount++;

    public bool sendPointerEventsToFocusedObject { get; set; } = true;
    public int pixelDragThreshold { get; set; } = 5;
    public bool isFocused => _hasFocus;

    public static EventSystem? current
    {
        get => _current;
        set => _current = value;
    }

    public GameObject? currentSelectedGameObject
    {
        get => _currentSelected;
        set
        {
            if (_currentSelected == value) return;
            var old = _currentSelected;
            _currentSelected = value;
            if (old is not null)
                ExecuteEvents.ExecuteHierarchy(old, new BaseEventData(this), ExecuteEvents.deselectHandler);
            if (value is not null)
                ExecuteEvents.ExecuteHierarchy(value, new BaseEventData(this), ExecuteEvents.selectHandler);
        }
    }

    public bool alreadySelecting => _alreadyChanged;

    public void SetSelectedGameObject(GameObject? selected)
    {
        SetSelectedGameObject(selected, new BaseEventData(this));
    }

    public void SetSelectedGameObject(GameObject? selected, BaseEventData pointer)
    {
        if (_alreadyChanged)
        {
            Debug.LogError($"Attempting to select {selected} while already selecting {_currentSelected}");
            return;
        }

        _alreadyChanged = true;
        if (currentSelectedGameObject is not null)
            ExecuteEvents.Execute(currentSelectedGameObject, pointer, ExecuteEvents.deselectHandler);
        _currentSelected = selected;
        if (selected is not null)
            ExecuteEvents.Execute(selected, pointer, ExecuteEvents.selectHandler);
        _alreadyChanged = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_current is not null && _current != this)
        {
            Debug.LogWarning($"Multiple EventSystems in scene. Using new one: {name}");
        }
        _current = this;
    }

    protected override void OnDisable()
    {
        if (_current == this)
            _current = null;
        base.OnDisable();
    }

    protected virtual void Update()
    {
        if (_current != this) return;
        TickModules();
    }

    private void TickModules()
    {
        for (var i = 0; i < _systemInputModules.Count; i++)
        {
            var module = _systemInputModules[i];
            if (module is null) continue;
            module.UpdateModule();
            if (module.IsModuleSupported() && module.ShouldActivateModule())
            {
                if (_currentModule != module)
                {
                    if (_currentModule != null)
                        _currentModule.DeactivateModule();
                    _currentModule = module;
                    _currentModule.ActivateModule();
                }
                _currentModule.Process();
            }
        }
    }

    public void UpdateModules()
    {
        _systemInputModules.Clear();
        var modules = GetComponents<BaseInputModule>();
        foreach (var module in modules)
        {
            if (module != null)
            {
                _systemInputModules.Add(module);
            }
        }
        if (_systemInputModules.Count > 0)
        {
            _currentModule = _systemInputModules[0];
            _currentModule.ActivateModule();
        }
        else
        {
            _currentModule = FindObjectOfType<BaseInputModule>();
            if (_currentModule != null)
            {
                _systemInputModules.Add(_currentModule);
                _currentModule.ActivateModule();
            }
        }
    }

    public void UpdateRaycasters()
    {
        _raycasters.Clear();
        var casters = FindObjectsOfType<BaseRaycaster>();
        foreach (var caster in casters)
        {
            _raycasters.Add(caster);
        }
    }

    protected override void OnTransformParentChanged()
    {
        base.OnTransformParentChanged();
        UpdateRaycasters();
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        UpdateRaycasters();
    }

    public bool IsPointerOverGameObject() => IsPointerOverGameObject(PointerInputModule.kMouseLeftId);

    public bool IsPointerOverGameObject(int pointerId)
    {
        if (_currentModule is null) return false;
        return _currentModule.IsPointerOverGameObject(pointerId);
    }

    public void RaycastAll(PointerEventData eventData, List<RaycastResult> raycastResults)
    {
        raycastResults.Clear();
        if (_raycasters.Count == 0)
            UpdateRaycasters();

        foreach (var caster in _raycasters)
        {
            if (caster is null || !caster.IsActive()) continue;
            caster.Raycast(eventData, raycastResults);
        }

        raycastResults.Sort(new RaycastResultComparer());
    }

    public override string ToString() => $"EventSystem:{_eventSystemId}";
}
