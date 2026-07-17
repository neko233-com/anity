using System;
using System.Collections.Generic;

namespace UnityEngine;

[Bindings.NativeHeader("Runtime/Export/Scripting/AsyncOperation.bindings.h")]
[Bindings.NativeHeader("Runtime/Misc/AsyncOperation.h")]
[Scripting.RequiredByNativeCode]
public class AsyncOperation : YieldInstruction
{
    private static readonly object SchedulerLock = new();
    private static readonly List<AsyncOperation> PendingOperations = new();
    private static readonly List<(AsyncOperation operation, Action<AsyncOperation> callbacks)> DeferredCallbacks = new();

    private readonly object _completionLock = new();
    private bool _isDone;
    private float _progress;
    private Action<AsyncOperation>? _completed;
    private Action? _scheduledAction;

    public bool isDone
    {
        get
        {
            lock (_completionLock)
                return _isDone;
        }
        internal set
        {
            Action<AsyncOperation>? callbacks = null;
            lock (_completionLock)
            {
                if (_isDone == value)
                    return;

                _isDone = value;
                if (value)
                {
                    _progress = 1f;
                    callbacks = _completed;
                    _completed = null;
                }
            }

            callbacks?.Invoke(this);
        }
    }

    public float progress
    {
        get
        {
            lock (_completionLock)
                return _progress;
        }
        internal set
        {
            lock (_completionLock)
                _progress = Mathf.Clamp01(value);
        }
    }

    public bool allowSceneActivation { get; set; } = true;
    internal string operationName { get; set; } = "AsyncOperation";
    public int priority { get; set; }

    public event Action<AsyncOperation>? completed
    {
        add
        {
            if (value is null)
                return;

            bool invokeNow;
            lock (_completionLock)
            {
                invokeNow = _isDone;
                if (!invokeNow)
                    _completed += value;
            }

            if (invokeNow)
                value(this);
        }
        remove
        {
            lock (_completionLock)
                _completed -= value;
        }
    }

    public AsyncOperation()
        : this(true)
    {
    }

    internal AsyncOperation(bool done)
    {
        _isDone = done;
        _progress = done ? 1f : 0f;
    }

    ~AsyncOperation()
    {
    }

    internal void SetDone() => isDone = true;

    internal void SetProgress(float p) => progress = p;

    internal void Schedule(Action action)
    {
        if (action is null)
            throw new ArgumentNullException(nameof(action));

        lock (_completionLock)
        {
            _scheduledAction = action;
            _isDone = false;
            _progress = 0f;
        }

        lock (SchedulerLock)
        {
            if (!PendingOperations.Contains(this))
                PendingOperations.Add(this);
        }
    }

    internal void CompleteScheduledNow()
    {
        lock (SchedulerLock)
            PendingOperations.Remove(this);

        RunScheduledOperation(deferCallbacks: false);
    }

    internal static void ProcessPendingOperations()
    {
        AsyncOperation[] pending;
        lock (SchedulerLock)
        {
            if (PendingOperations.Count == 0)
                return;

            pending = PendingOperations.ToArray();
            PendingOperations.Clear();
        }

        foreach (var operation in pending)
            operation.RunScheduledOperation(deferCallbacks: true);
    }

    internal static void DispatchDeferredCompletionCallbacks()
    {
        (AsyncOperation operation, Action<AsyncOperation> callbacks)[] callbacks;
        lock (SchedulerLock)
        {
            if (DeferredCallbacks.Count == 0)
                return;

            callbacks = DeferredCallbacks.ToArray();
            DeferredCallbacks.Clear();
        }

        foreach (var (operation, callback) in callbacks)
        {
            foreach (Action<AsyncOperation> handler in callback.GetInvocationList())
            {
                try { handler(operation); }
                catch { }
            }
        }
    }

    private void RunScheduledOperation(bool deferCallbacks)
    {
        Action? action;
        lock (_completionLock)
        {
            action = _scheduledAction;
            _scheduledAction = null;
        }

        if (action is null)
            return;

        try { action(); }
        catch { }
        finally
        {
            if (deferCallbacks)
                CompleteWithDeferredCallbacks();
            else
                SetDone();
        }
    }

    private void CompleteWithDeferredCallbacks()
    {
        Action<AsyncOperation>? callbacks;
        lock (_completionLock)
        {
            if (_isDone)
                return;

            _isDone = true;
            _progress = 1f;
            callbacks = _completed;
            _completed = null;
        }

        if (callbacks is null)
            return;

        lock (SchedulerLock)
            DeferredCallbacks.Add((this, callbacks));
    }
}
