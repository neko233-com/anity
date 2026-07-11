using System;

namespace UnityEngine;

public class AsyncOperation : CustomYieldInstruction
{
    private bool _isDone;
    private float _progress;

    public bool isDone
    {
        get => _isDone;
        internal set
        {
            if (_isDone != value)
            {
                _isDone = value;
                if (value)
                {
                    _progress = 1f;
                    completed?.Invoke(this);
                }
            }
        }
    }

    public float progress
    {
        get => _progress;
        internal set => _progress = Mathf.Clamp01(value);
    }

    public bool allowSceneActivation { get; set; } = true;
    public string operationName { get; set; } = "AsyncOperation";
    public int priority { get; set; }

    public event Action<AsyncOperation>? completed;

    public override bool keepWaiting => !_isDone;

    public AsyncOperation()
    {
        _isDone = true;
        _progress = 1f;
    }

    public AsyncOperation(bool done)
    {
        _isDone = done;
        _progress = done ? 1f : 0f;
    }

    internal void SetDone()
    {
        isDone = true;
    }

    internal void SetProgress(float p)
    {
        _progress = Mathf.Clamp01(p);
    }
}
