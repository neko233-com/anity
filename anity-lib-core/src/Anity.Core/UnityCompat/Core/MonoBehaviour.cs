using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.EventSystems;

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

    if (coroutine.WaitRealtimeTimeLeft > 0f)
    {
      coroutine.WaitRealtimeTimeLeft -= Time.unscaledDeltaTime;
      if (coroutine.WaitRealtimeTimeLeft > 0f) return true;
      coroutine.WaitingForSecondsRealtime = false;
    }

    if (coroutine.WaitingForCustomYield != null)
    {
      if (coroutine.WaitingForCustomYield.keepWaiting)
        return true;
      coroutine.WaitingForCustomYield = null;
    }

    if (coroutine.WaitingForCoroutine != null)
    {
      if (!coroutine.WaitingForCoroutine.Finished)
        return true;
      coroutine.WaitingForCoroutine = null;
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

    if (current is WaitForSecondsRealtime waitRealtime)
    {
      coroutine.WaitingForSecondsRealtime = true;
      coroutine.WaitRealtimeTimeLeft = waitRealtime.waitTime;
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

  public Coroutine StartCoroutine(string methodName, object? value = null)
  {
    var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
    if (method is not null)
    {
      var parameters = method.GetParameters();
      if (parameters.Length == 0)
      {
        if (method.Invoke(this, null) is IEnumerator enumerator)
        {
          return StartCoroutine(enumerator);
        }
      }
      else if (value is not null)
      {
        if (method.Invoke(this, new[] { value }) is IEnumerator enumerator)
        {
          return StartCoroutine(enumerator);
        }
      }
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
  protected virtual void OnApplicationQuit() {}
  protected virtual void OnGUI() {}
  protected virtual void OnValidate() {}
  protected virtual void OnTransformParentChanged() {}
  protected virtual void OnTransformChildrenChanged() {}
  protected virtual void OnCanvasHierarchyChanged() {}
  protected virtual void OnCanvasGroupChanged() {}
  protected virtual void OnRectTransformDimensionsChange() {}
  protected virtual void OnWillRenderObject() {}
  protected virtual void OnPreCull() {}
  protected virtual void OnPreRender() {}
  protected virtual void OnPostRender() {}
  protected virtual void OnRenderObject() {}
  protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination) {}
  protected virtual void OnCollisionEnter(Collision collision) {}
  protected virtual void OnCollisionStay(Collision collision) {}
  protected virtual void OnCollisionExit(Collision collision) {}
  protected virtual void OnCollisionEnter2D(Collision2D collision) {}
  protected virtual void OnCollisionStay2D(Collision2D collision) {}
  protected virtual void OnCollisionExit2D(Collision2D collision) {}
  protected virtual void OnTriggerEnter(Collider other) {}
  protected virtual void OnTriggerStay(Collider other) {}
  protected virtual void OnTriggerExit(Collider other) {}
  protected virtual void OnTriggerEnter2D(Collider2D other) {}
  protected virtual void OnTriggerStay2D(Collider2D other) {}
  protected virtual void OnTriggerExit2D(Collider2D other) {}
  protected virtual void OnMouseEnter() {}
  protected virtual void OnMouseExit() {}
  protected virtual void OnMouseDown() {}
  protected virtual void OnMouseUp() {}
  protected virtual void OnMouseUpAsButton() {}
  protected virtual void OnMouseOver() {}
  protected virtual void OnMouseDrag() {}
  protected virtual void OnBecameVisible() {}
  protected virtual void OnBecameInvisible() {}
  protected virtual void OnPointerEnter(PointerEventData eventData) {}
  protected virtual void OnPointerExit(PointerEventData eventData) {}
  protected virtual void OnPointerDown(PointerEventData eventData) {}
  protected virtual void OnPointerUp(PointerEventData eventData) {}
  protected virtual void OnPointerClick(PointerEventData eventData) {}
  protected virtual void OnBeginDrag(PointerEventData eventData) {}
  protected virtual void OnDrag(PointerEventData eventData) {}
  protected virtual void OnEndDrag(PointerEventData eventData) {}
  protected virtual void OnDrop(PointerEventData eventData) {}
  protected virtual void OnScroll(PointerEventData eventData) {}
  protected virtual void OnSelect(BaseEventData eventData) {}
  protected virtual void OnDeselect(BaseEventData eventData) {}
  protected virtual void OnSubmit(BaseEventData eventData) {}
  protected virtual void OnCancel(BaseEventData eventData) {}
  protected virtual void OnMove(AxisEventData eventData) {}

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

public class WaitUntil : CustomYieldInstruction
{
  private readonly Func<bool> _predicate;

  public WaitUntil(Func<bool> predicate)
  {
    _predicate = predicate;
  }

  public override bool keepWaiting => !_predicate();
}

public class WaitWhile : CustomYieldInstruction
{
  private readonly Func<bool> _predicate;

  public WaitWhile(Func<bool> predicate)
  {
    _predicate = predicate;
  }

  public override bool keepWaiting => _predicate();
}
