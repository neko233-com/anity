using System;

namespace UnityEngine;

public class AsyncOperation
{
  public bool isDone { get; private set; } = true;
  public float progress { get; private set; } = 1f;
  public bool allowSceneActivation { get; set; } = true;
  public event Action<AsyncOperation>? completed;

  public AsyncOperation()
  {
  }

  public AsyncOperation(bool done)
  {
    isDone = done;
    progress = done ? 1f : 0f;
  }

  internal void SetDone()
  {
    if (isDone)
    {
      return;
    }

    isDone = true;
    progress = 1f;
    completed?.Invoke(this);
  }
}
