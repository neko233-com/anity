using System;
using System.Collections;

namespace UnityEngine;

public class Coroutine
{
  internal bool Finished;
  internal bool WaitingForSeconds;
  internal float WaitTimeLeft;
  internal bool WaitingForSecondsRealtime;
  internal float WaitRealtimeTimeLeft;
  internal bool WaitingForFixedUpdate;
  internal bool WaitingForEndOfFrame;
  internal CustomYieldInstruction WaitingForCustomYield;
  internal AsyncOperation WaitingForAsyncOperation;
  internal Coroutine WaitingForCoroutine;

  public Coroutine(IEnumerator? routine)
  {
    Routine = routine;
  }

  public IEnumerator? Routine { get; }
  public string? MethodName { get; internal set; }

  public bool IsRunning => Routine != null && !Finished;

  internal void Cancel()
  {
    Finished = true;
  }
}
