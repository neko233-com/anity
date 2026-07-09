using System;
using System.Collections.Generic;

namespace UnityEngine.Events;

public abstract class UnityEventBase
{
    private readonly List<BaseInvokableCall> _calls = new();
    private bool _isDirty;

    public int GetPersistentEventCount() => 0;
    public string GetPersistentMethodName(int index) => string.Empty;
    public string GetPersistentTargetName(int index) => string.Empty;

    public void SetPersistentListenerState(int index, UnityEventCallState state) { }

    public void AddListener(UnityAction call) { }
    public void RemoveListener(UnityAction call) { }

    public void RemoveAllListeners()
    {
        _calls.Clear();
    }

    public void Invoke()
    {
        for (int i = 0; i < _calls.Count; i++)
            _calls[i].Invoke();
    }

    internal void AddCall(BaseInvokableCall call)
    {
        _calls.Add(call);
    }
}

public class UnityEvent : UnityEventBase
{
    public UnityEvent() { }

    public new void AddListener(UnityAction call)
    {
        base.AddListener(call);
    }

    public new void RemoveListener(UnityAction call)
    {
        base.RemoveListener(call);
    }

    public new void Invoke()
    {
        base.Invoke();
    }
}

public class UnityEvent<T0> : UnityEventBase
{
    public UnityEvent() { }

    public void AddListener(UnityAction<T0> call) { }
    public void RemoveListener(UnityAction<T0> call) { }
    public new void Invoke(T0 arg0) { }
}

public class UnityEvent<T0, T1> : UnityEventBase
{
    public UnityEvent() { }

    public void AddListener(UnityAction<T0, T1> call) { }
    public void RemoveListener(UnityAction<T0, T1> call) { }
    public void Invoke(T0 arg0, T1 arg1) { }
}

public class UnityEvent<T0, T1, T2> : UnityEventBase
{
    public UnityEvent() { }

    public void AddListener(UnityAction<T0, T1, T2> call) { }
    public void RemoveListener(UnityAction<T0, T1, T2> call) { }
    public void Invoke(T0 arg0, T1 arg1, T2 arg2) { }
}

public class UnityEvent<T0, T1, T2, T3> : UnityEventBase
{
    public UnityEvent() { }

    public void AddListener(UnityAction<T0, T1, T2, T3> call) { }
    public void RemoveListener(UnityAction<T0, T1, T2, T3> call) { }
    public void Invoke(T0 arg0, T1 arg1, T2 arg2, T3 arg3) { }
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

internal abstract class BaseInvokableCall
{
    public abstract void Invoke();
    public abstract bool Find(object targetObj, string methodName);
}

internal class InvokableCall : BaseInvokableCall
{
    private UnityAction _call;

    public InvokableCall(UnityAction call)
    {
        _call = call;
    }

    public override void Invoke()
    {
        _call?.Invoke();
    }

    public override bool Find(object targetObj, string methodName)
    {
        return false;
    }
}