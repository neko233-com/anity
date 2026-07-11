using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityEngine;

public class MonoBehaviour : Behaviour
{
  internal bool _started;
  private readonly List<Coroutine> _coroutines = new();
  private readonly Dictionary<string, InvokeCall> _invokeCalls = new();

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
    if (!_started) InternalStart();
    try { Update(); } catch { }
    TickInvokes();
    TickCoroutines();
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

    foreach (var kvp in _invokeCalls)
    {
      var info = kvp.Value;
      if (currentTime >= info.nextTime)
      {
        toInvoke.Add(kvp.Key);
        if (info.repeatRate > 0f)
        {
          info.nextTime = currentTime + info.repeatRate;
          _invokeCalls[kvp.Key] = info;
        }
      }
    }

    foreach (var methodName in toInvoke)
    {
      if (_invokeCalls.TryGetValue(methodName, out var info) && info.repeatRate <= 0f)
      {
        _invokeCalls.Remove(methodName);
      }

      var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
      if (method is not null)
      {
        try { method.Invoke(this, null); } catch { }
      }
    }
  }

  internal void TickCoroutines()
  {
    for (int i = _coroutines.Count - 1; i >= 0; i--)
    {
      var coroutine = _coroutines[i];
      if (!TickCoroutine(coroutine))
      {
        _coroutines.RemoveAt(i);
      }
    }
  }

  private bool TickCoroutine(Coroutine coroutine)
  {
    if (coroutine.Routine == null || coroutine.Finished) return false;

    if (coroutine.WaitTimeLeft > 0f)
    {
      coroutine.WaitTimeLeft -= Time.deltaTime;
      if (coroutine.WaitTimeLeft > 0f) return true;
      coroutine.WaitingForSeconds = false;
    }

    if (coroutine.WaitingForFixedUpdate)
    {
      return true;
    }

    if (coroutine.WaitingForEndOfFrame)
    {
      return true;
    }

    bool moveNext;
    try
    {
      moveNext = coroutine.Routine.MoveNext();
    }
    catch
    {
      return false;
    }

    if (!moveNext)
    {
      coroutine.Finished = true;
      return false;
    }

    var current = coroutine.Routine.Current;
    if (current == null)
    {
      return true;
    }

    if (current is WaitForSeconds wait)
    {
      coroutine.WaitingForSeconds = true;
      coroutine.WaitTimeLeft = wait.seconds;
      return true;
    }

    if (current is WaitForFixedUpdate)
    {
      coroutine.WaitingForFixedUpdate = true;
      return true;
    }

    if (current is WaitForEndOfFrame)
    {
      coroutine.WaitingForEndOfFrame = true;
      return true;
    }

    if (current is CustomYieldInstruction customYield)
    {
      coroutine.WaitingForCustomYield = customYield;
      return true;
    }

    if (current is Coroutine nestedCoroutine)
    {
      coroutine.WaitingForCoroutine = nestedCoroutine;
      return true;
    }

    if (current is float seconds)
    {
      coroutine.WaitingForSeconds = true;
      coroutine.WaitTimeLeft = seconds;
      return true;
    }

    return true;
  }

  internal void TickFixedUpdateCoroutines()
  {
    foreach (var coroutine in _coroutines)
    {
      if (coroutine.WaitingForFixedUpdate)
      {
        coroutine.WaitingForFixedUpdate = false;
      }
    }
  }

  internal void TickEndOfFrameCoroutines()
  {
    for (int i = _coroutines.Count - 1; i >= 0; i--)
    {
      var coroutine = _coroutines[i];
      if (coroutine.WaitingForEndOfFrame)
      {
        coroutine.WaitingForEndOfFrame = false;
        if (!TickCoroutine(coroutine))
        {
          _coroutines.RemoveAt(i);
        }
      }
    }
  }

  public Coroutine StartCoroutine(IEnumerator routine)
  {
    if (routine is null)
    {
      return new Coroutine(null);
    }

    var coroutine = new Coroutine(routine);
    _coroutines.Add(coroutine);
    return coroutine;
  }

  public Coroutine StartCoroutine(string methodName)
  {
    var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
    if (method is not null && method.Invoke(this, null) is IEnumerator enumerator)
    {
      return StartCoroutine(enumerator);
    }
    var coroutine = new Coroutine(null) { MethodName = methodName };
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
    if (coroutine is null) return;
    coroutine.Finished = true;
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
      coroutine.Finished = true;
    }
    _coroutines.Clear();
  }

  public void Invoke(string methodName, float time)
  {
    _invokeCalls[methodName] = new InvokeCall(methodName, Time.time + MathF.Max(0f, time), -1f);
  }

  public void InvokeRepeating(string methodName, float time, float repeatRate)
  {
    _invokeCalls[methodName] = new InvokeCall(methodName, Time.time + MathF.Max(0f, time), MathF.Max(0f, repeatRate));
  }

  public void CancelInvoke()
  {
    _invokeCalls.Clear();
  }

  public void CancelInvoke(string methodName)
  {
    _ = _invokeCalls.Remove(methodName);
  }

  public bool IsInvoking()
  {
    return _invokeCalls.Count > 0;
  }

  public bool IsInvoking(string methodName)
  {
    return _invokeCalls.ContainsKey(methodName);
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
    if (routine is null) return new Coroutine(null);
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

  internal void InternalAwake() { try { Awake(); } catch { } }
  internal void InternalOnEnable() { try { OnEnable(); } catch { } }
  internal void InternalOnDisable() { try { OnDisable(); } catch { } }
  internal void InternalOnDestroy() { try { OnDestroy(); } catch { } }
}

internal struct InvokeCall
{
  public string methodName;
  public float nextTime;
  public float repeatRate;

  public InvokeCall(string methodName, float nextTime, float repeatRate)
  {
    this.methodName = methodName;
    this.nextTime = nextTime;
    this.repeatRate = repeatRate;
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
