using System;
using System.Collections.Generic;

namespace UnityEngine.Events;

public abstract class UnityEventBase
{
    private readonly List<object> _calls = new();
    private readonly List<UnityEventCallState> _persistentStates = new();

    public int GetPersistentEventCount() => _persistentStates.Count;
    public object GetPersistentTarget(int index) => null;
    public string GetPersistentMethodName(int index) => string.Empty;
    public string GetPersistentTargetName(int index) => string.Empty;

    public void SetPersistentListenerState(int index, UnityEventCallState state)
    {
        if (index < 0) return;
        while (_persistentStates.Count <= index)
        {
            _persistentStates.Add(UnityEventCallState.RuntimeOnly);
        }
        _persistentStates[index] = state;
    }

    public UnityEventCallState GetPersistentListenerState(int index)
    {
        if (index < 0 || index >= _persistentStates.Count) return UnityEventCallState.RuntimeOnly;
        return _persistentStates[index];
    }

    public void RemoveAllListeners()
    {
        _calls.Clear();
    }

    protected void AddCall(object call)
    {
        _calls.Add(call);
    }

    protected List<object> GetCalls() => _calls;
}

public class UnityEvent : UnityEventBase
{
    private readonly List<UnityAction> _actions = new();

    public void AddListener(UnityAction call)
    {
        if (call != null && !_actions.Contains(call))
            _actions.Add(call);
    }

    public void RemoveListener(UnityAction call)
    {
        if (call != null)
            _actions.Remove(call);
    }

    public void Invoke()
    {
        for (int i = 0; i < _actions.Count; i++)
            _actions[i]?.Invoke();
    }
}

public class UnityEvent<T0> : UnityEventBase
{
    private readonly List<UnityAction<T0>> _actions = new();

    public void AddListener(UnityAction<T0> call)
    {
        if (call != null && !_actions.Contains(call))
            _actions.Add(call);
    }

    public void RemoveListener(UnityAction<T0> call)
    {
        if (call != null)
            _actions.Remove(call);
    }

    public void Invoke(T0 arg0)
    {
        for (int i = 0; i < _actions.Count; i++)
            _actions[i]?.Invoke(arg0);
    }
}

public class UnityEvent<T0, T1> : UnityEventBase
{
    private readonly List<UnityAction<T0, T1>> _actions = new();

    public void AddListener(UnityAction<T0, T1> call)
    {
        if (call != null && !_actions.Contains(call))
            _actions.Add(call);
    }

    public void RemoveListener(UnityAction<T0, T1> call)
    {
        if (call != null)
            _actions.Remove(call);
    }

    public void Invoke(T0 arg0, T1 arg1)
    {
        for (int i = 0; i < _actions.Count; i++)
            _actions[i]?.Invoke(arg0, arg1);
    }
}

public class UnityEvent<T0, T1, T2> : UnityEventBase
{
    private readonly List<UnityAction<T0, T1, T2>> _actions = new();

    public void AddListener(UnityAction<T0, T1, T2> call)
    {
        if (call != null && !_actions.Contains(call))
            _actions.Add(call);
    }

    public void RemoveListener(UnityAction<T0, T1, T2> call)
    {
        if (call != null)
            _actions.Remove(call);
    }

    public void Invoke(T0 arg0, T1 arg1, T2 arg2)
    {
        for (int i = 0; i < _actions.Count; i++)
            _actions[i]?.Invoke(arg0, arg1, arg2);
    }
}

public class UnityEvent<T0, T1, T2, T3> : UnityEventBase
{
    private readonly List<UnityAction<T0, T1, T2, T3>> _actions = new();

    public void AddListener(UnityAction<T0, T1, T2, T3> call)
    {
        if (call != null && !_actions.Contains(call))
            _actions.Add(call);
    }

    public void RemoveListener(UnityAction<T0, T1, T2, T3> call)
    {
        if (call != null)
            _actions.Remove(call);
    }

    public void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3)
    {
        for (int i = 0; i < _actions.Count; i++)
            _actions[i]?.Invoke(arg0, arg1, arg2, arg3);
    }
}

public enum UnityEventCallState
{
    Off = 0,
    EditorAndRuntime = 1,
    RuntimeOnly = 2,
}

public delegate void UnityAction();
public delegate void UnityAction<T0>(T0 arg0);
public delegate void UnityAction<T0, T1>(T0 arg0, T1 arg1);
public delegate void UnityAction<T0, T1, T2>(T0 arg0, T1 arg1, T2 arg2);
public delegate void UnityAction<T0, T1, T2, T3>(T0 arg0, T1 arg1, T2 arg2, T3 arg3);
