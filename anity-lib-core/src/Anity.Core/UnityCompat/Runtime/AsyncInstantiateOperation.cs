using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace UnityEngine;

[Bindings.NativeHeader("Runtime/GameCode/AsyncInstantiate/AsyncInstantiateOperation.h")]
[Scripting.RequiredByNativeCode]
public class AsyncInstantiateOperation : AsyncOperation
{
    private static readonly object PendingLock = new();
    private static readonly List<AsyncInstantiateOperation> Pending = new();
    private static float _integrationTimeMS = 2f;

    private readonly Func<int, Object>? _instantiate;
    private readonly Type? _resultElementType;
    private readonly Object?[]? _integrated;
    private int _integratedCount;
    private volatile bool _cancelRequested;
    private volatile bool _waitingForSceneActivation;
    private Object[]? _result;

    public Object[]? Result => _result;

    public AsyncInstantiateOperation()
    {
    }

    internal AsyncInstantiateOperation(int count, Type resultElementType, Func<int, Object> instantiate)
        : base(false)
    {
        _instantiate = instantiate ?? throw new ArgumentNullException(nameof(instantiate));
        _resultElementType = resultElementType ?? throw new ArgumentNullException(nameof(resultElementType));
        _integrated = new Object[count];
        progress = 0.9f;
        operationName = "AsyncInstantiateOperation";

        lock (PendingLock)
            Pending.Add(this);
    }

    [Bindings.NativeMethod("IsWaitingForSceneActivation")]
    public bool IsWaitingForSceneActivation() => _waitingForSceneActivation;

    [Bindings.NativeMethod("WaitForCompletion")]
    public void WaitForCompletion()
    {
        while (!isDone)
        {
            if (!allowSceneActivation && !_cancelRequested)
            {
                _waitingForSceneActivation = true;
                return;
            }

            ProcessSlice(forceCompletion: true);
        }
    }

    [Bindings.NativeMethod("Cancel")]
    public void Cancel() => _cancelRequested = true;

    public static float GetIntegrationTimeMS() => _integrationTimeMS;

    public static void SetIntegrationTimeMS(float integrationTimeMS)
    {
        if (integrationTimeMS <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(integrationTimeMS),
                "integrationTimeMS was out of range. Must be greater than zero.");
        }

        _integrationTimeMS = integrationTimeMS;
    }

    internal static void ProcessPendingOperations()
    {
        AsyncInstantiateOperation[] snapshot;
        lock (PendingLock)
            snapshot = Pending.ToArray();

        foreach (AsyncInstantiateOperation operation in snapshot)
            operation.ProcessSlice(forceCompletion: false);
    }

    private void ProcessSlice(bool forceCompletion)
    {
        if (isDone)
        {
            RemovePending();
            return;
        }

        if (_cancelRequested)
        {
            DestroyIntegratedObjects();
            _result = null;
            Complete();
            return;
        }

        if (!allowSceneActivation)
        {
            _waitingForSceneActivation = true;
            return;
        }

        _waitingForSceneActivation = false;
        if (_instantiate is null || _integrated is null || _resultElementType is null)
        {
            _result = Array.Empty<Object>();
            Complete();
            return;
        }

        long started = Stopwatch.GetTimestamp();
        double budgetMilliseconds = _integrationTimeMS;

        while (_integratedCount < _integrated.Length)
        {
            try
            {
                _integrated[_integratedCount] = _instantiate(_integratedCount);
                _integratedCount++;
            }
            catch (Exception exception)
            {
                DestroyIntegratedObjects();
                _result = null;
                Complete();
                Debug.LogException(exception);
                return;
            }

            if (!forceCompletion && ElapsedMilliseconds(started) >= budgetMilliseconds)
                return;
        }

        Array typedResult = Array.CreateInstance(_resultElementType, _integrated.Length);
        for (int i = 0; i < _integrated.Length; i++)
            typedResult.SetValue(_integrated[i], i);

        _result = (Object[])typedResult;
        Complete();
    }

    private static double ElapsedMilliseconds(long started)
        => (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;

    private void DestroyIntegratedObjects()
    {
        if (_integrated is null)
            return;

        for (int i = 0; i < _integratedCount; i++)
        {
            if (_integrated[i] is Component component && component.gameObject is GameObject gameObject && gameObject)
                Object.DestroyImmediate(gameObject);
            else if (_integrated[i] is Object instance && instance)
                Object.DestroyImmediate(instance);
            _integrated[i] = null;
        }

        _integratedCount = 0;
    }

    private void Complete()
    {
        RemovePending();
        SetDone();
    }

    private void RemovePending()
    {
        lock (PendingLock)
            Pending.Remove(this);
    }
}

[Internal.ExcludeFromDocs]
public class AsyncInstantiateOperation<T> : CustomYieldInstruction where T : Object
{
    private readonly AsyncInstantiateOperation m_op;

    internal AsyncInstantiateOperation(AsyncInstantiateOperation op)
    {
        m_op = op;
    }

    public override bool keepWaiting => !m_op.isDone;

    public AsyncInstantiateOperation GetOperation() => m_op;

    public static implicit operator AsyncInstantiateOperation(AsyncInstantiateOperation<T> generic) => generic.m_op;

    public bool IsWaitingForSceneActivation() => m_op.IsWaitingForSceneActivation();

    public event Action<AsyncOperation>? completed
    {
        add => m_op.completed += value;
        remove => m_op.completed -= value;
    }

    public bool isDone => m_op.isDone;
    public float progress => m_op.progress;

    public bool allowSceneActivation
    {
        get => m_op.allowSceneActivation;
        set => m_op.allowSceneActivation = value;
    }

    public void WaitForCompletion() => m_op.WaitForCompletion();
    public void Cancel() => m_op.Cancel();
    public T[]? Result => (T[]?)(object?)m_op.Result;
}
