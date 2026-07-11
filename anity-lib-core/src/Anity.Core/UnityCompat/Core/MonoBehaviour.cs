using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace UnityEngine;

public class MonoBehaviour : Behaviour
{
  private readonly HashSet<Coroutine> _coroutines = new();
  private readonly Dictionary<string, InvokeInfo> _invokes = new();
  private bool _started;

  internal bool IsStarted => _started;

  internal void InternalStart()
  {
    if (_started) return;
    _started = true;
    try { Start(); } catch { }
  }

  internal void InternalUpdate()
  {
    if (!isActiveAndEnabled) return;
    try { Update(); } catch { }
    TickInvokes();
  }

  internal void InternalFixedUpdate()
  {
    if (!isActiveAndEnabled) return;
    try { FixedUpdate(); } catch { }
  }

  internal void InternalLateUpdate()
  {
    if (!isActiveAndEnabled) return;
    try { LateUpdate(); } catch { }
  }

  private void TickInvokes()
  {
    float currentTime = Time.time;
    var toInvoke = new List<string>();

    foreach (var kvp in _invokes)
    {
      var info = kvp.Value;
      if (info.TargetTime > 0f && currentTime >= info.TargetTime)
      {
        toInvoke.Add(kvp.Key);
        if (info.RepeatRate > 0f)
        {
          info.TargetTime = currentTime + info.RepeatRate;
          _invokes[kvp.Key] = info;
        }
      }
    }

    foreach (var methodName in toInvoke)
    {
      if (_invokes.TryGetValue(methodName, out var info) && info.RepeatRate <= 0f)
      {
        _invokes.Remove(methodName);
      }

      var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
      if (method is not null)
      {
        try { method.Invoke(this, null); } catch { }
      }
    }
  }

  public Coroutine StartCoroutine(IEnumerator routine)
  {
    if (routine is null)
    {
      return new Coroutine(Task.CompletedTask, null, null);
    }

    var coroutine = new Coroutine(Task.CompletedTask, routine, routine.GetType().FullName);
    _coroutines.Add(coroutine);
    StartCoroutineInternal(routine);
    return coroutine;
  }

  private async void StartCoroutineInternal(IEnumerator routine)
  {
    try
    {
      while (routine.MoveNext())
      {
        var current = routine.Current;
        if (current is YieldInstruction yield)
        {
          await WaitForYield(yield);
        }
        else if (current is float seconds)
        {
          await Task.Delay((int)(seconds * 1000f));
        }
        else
        {
          await Task.Yield();
        }
      }
    }
    catch { }
  }

  private async Task WaitForYield(YieldInstruction yield)
  {
    if (yield is WaitForSeconds wait)
    {
      await Task.Delay((int)(wait.seconds * 1000f));
    }
    else if (yield is WaitForEndOfFrame)
    {
      await Task.Yield();
    }
    else if (yield is WaitForFixedUpdate)
    {
      await Task.Delay((int)(Time.fixedDeltaTime * 1000f));
    }
    else
    {
      await Task.Yield();
    }
  }

  public Coroutine StartCoroutine(string methodName)
  {
    var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
    if (method is not null && method.Invoke(this, null) is IEnumerator enumerator)
    {
      return StartCoroutine(enumerator);
    }
    var coroutine = new Coroutine(Task.CompletedTask, null, methodName);
    _coroutines.Add(coroutine);
    return coroutine;
  }

  public Coroutine StartCoroutine(string methodName, params object[] args)
  {
    _ = args;
    return StartCoroutine(methodName);
  }

  public Coroutine StartCoroutine_Auto(string methodName)
  {
    return StartCoroutine(methodName);
  }

  public void StopCoroutine(Coroutine? coroutine)
  {
    if (coroutine is null)
    {
      return;
    }

    coroutine.Cancel();
    _coroutines.Remove(coroutine);
  }

  public void StopCoroutine(IEnumerator routine)
  {
    var target = _coroutines.FirstOrDefault(c => ReferenceEquals(c.Routine, routine));
    if (target is not null)
    {
      StopCoroutine(target);
    }
  }

  public void StopCoroutine(string methodName)
  {
    var target = _coroutines.FirstOrDefault(c => string.Equals(c.MethodName, methodName, StringComparison.Ordinal));
    if (target is not null)
    {
      StopCoroutine(target);
    }
  }

  public void StopAllCoroutines()
  {
    foreach (var coroutine in _coroutines)
    {
      coroutine.Cancel();
    }

    _coroutines.Clear();
  }

  public void Invoke(string methodName, float time)
  {
    _invokes[methodName] = new InvokeInfo(Time.time + MathF.Max(0f, time), -1f);
  }

  public void InvokeRepeating(string methodName, float time, float repeatRate)
  {
    _invokes[methodName] = new InvokeInfo(Time.time + MathF.Max(0f, time), MathF.Max(0f, repeatRate));
  }

  public void CancelInvoke()
  {
    _invokes.Clear();
  }

  public void CancelInvoke(string methodName)
  {
    _ = _invokes.Remove(methodName);
  }

  public bool IsInvoking()
  {
    return _invokes.Count > 0;
  }

  public bool IsInvoking(string methodName)
  {
    return _invokes.ContainsKey(methodName);
  }

  public void Invoke(Action action, float time)
  {
    if (action is null) return;
    Invoke(action.Method.Name, time);
  }

  public void InvokeRepeating(Action action, float time, float repeatRate)
  {
    if (action is null) return;
    InvokeRepeating(action.Method.Name, time, repeatRate);
  }

  public void CancelInvoke(Action action)
  {
    if (action is null) return;
    CancelInvoke(action.Method.Name);
  }

  public bool IsInvoking(Action action)
  {
    if (action is null) return false;
    return IsInvoking(action.Method.Name);
  }

  public Coroutine StartCoroutine(Func<IEnumerator> routine)
  {
    if (routine is null) return new Coroutine(Task.CompletedTask, null, null);
    return StartCoroutine(routine());
  }

  public static void print(object? message)
  {
    Debug.Log(message);
  }

  protected virtual void Awake() {}
  protected virtual void Reset() {}
  protected virtual void Start() {}
  protected virtual void Update() {}
  protected virtual void LateUpdate() {}
  protected virtual void FixedUpdate() {}
  protected virtual void OnEnable() {}
  protected virtual void OnDisable() {}
  protected virtual void OnDestroy() {}
  protected virtual void OnApplicationPause(bool pauseStatus) {}
  protected virtual void OnApplicationFocus(bool focusStatus) {}
  protected virtual void OnGUI() {}
  protected virtual void OnTransformParentChanged() {}
  protected virtual void OnTransformChildrenChanged() {}
  protected virtual void OnCanvasHierarchyChanged() {}
  protected virtual void OnCollisionEnter(Collision collision) {}
  protected virtual void OnCollisionStay(Collision collision) {}
  protected virtual void OnCollisionExit(Collision collision) {}
  protected virtual void OnTriggerEnter(Collider other) {}
  protected virtual void OnTriggerStay(Collider other) {}
  protected virtual void OnTriggerExit(Collider other) {}
  protected virtual void OnMouseEnter() {}
  protected virtual void OnMouseExit() {}
  protected virtual void OnMouseDown() {}
  protected virtual void OnMouseUp() {}
  protected virtual void OnMouseOver() {}
  protected virtual void OnMouseDrag() {}
  protected virtual void OnBecameVisible() {}
  protected virtual void OnBecameInvisible() {}

  internal void InternalAwake() { Awake(); }
  internal void InternalOnEnable() { OnEnable(); }
  internal void InternalOnDisable() { OnDisable(); }
  internal void InternalOnDestroy() { OnDestroy(); }
}

internal struct InvokeInfo
{
  public float TargetTime;
  public float RepeatRate;

  public InvokeInfo(float targetTime, float repeatRate)
  {
    TargetTime = targetTime;
    RepeatRate = repeatRate;
  }
}

public class WaitForSeconds : YieldInstruction
{
  public float seconds { get; }

  public WaitForSeconds(float seconds)
  {
    this.seconds = seconds;
  }
}

public class WaitForEndOfFrame : YieldInstruction
{
}

public class WaitForFixedUpdate : YieldInstruction
{
}

public class WaitUntil : YieldInstruction
{
  private readonly Func<bool> _predicate;

  public WaitUntil(Func<bool> predicate)
  {
    _predicate = predicate;
  }

  public bool KeepWaiting => !_predicate();
}

public class WaitWhile : YieldInstruction
{
  private readonly Func<bool> _predicate;

  public WaitWhile(Func<bool> predicate)
  {
    _predicate = predicate;
  }

  public bool KeepWaiting => _predicate();
}
