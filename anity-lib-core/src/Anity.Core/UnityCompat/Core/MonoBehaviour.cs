using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UnityEngine;

public class MonoBehaviour : Behaviour
{
  private readonly HashSet<Coroutine> _coroutines = new();
  private readonly Dictionary<string, float> _invokes = new();
  private readonly Dictionary<string, float> _repeatingInvokes = new();

  public Coroutine StartCoroutine(IEnumerator routine)
  {
    if (routine is null)
    {
      return new Coroutine(Task.CompletedTask, null, null);
    }

    var coroutine = new Coroutine(Task.CompletedTask, routine, routine.GetType().FullName);
    _coroutines.Add(coroutine);
    return coroutine;
  }

  public Coroutine StartCoroutine(string methodName)
  {
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
    _invokes[methodName] = MathF.Max(0f, time);
  }

  public void InvokeRepeating(string methodName, float time, float repeatRate)
  {
    _invokes[methodName] = MathF.Max(0f, time);
    _repeatingInvokes[methodName] = MathF.Max(0f, repeatRate);
  }

  public void CancelInvoke()
  {
    _invokes.Clear();
    _repeatingInvokes.Clear();
  }

  public void CancelInvoke(string methodName)
  {
    _ = _invokes.Remove(methodName);
    _ = _repeatingInvokes.Remove(methodName);
  }

  public bool IsInvoking()
  {
    return _invokes.Count > 0 || _repeatingInvokes.Count > 0;
  }

  public bool IsInvoking(string methodName)
  {
    return _invokes.ContainsKey(methodName) || _repeatingInvokes.ContainsKey(methodName);
  }

  public void Invoke(Action action, float time)
  {
    if (action is null) return;
    _invokes[action.Method.Name] = MathF.Max(0f, time);
  }

  public void InvokeRepeating(Action action, float time, float repeatRate)
  {
    if (action is null) return;
    _invokes[action.Method.Name] = MathF.Max(0f, time);
    _repeatingInvokes[action.Method.Name] = MathF.Max(0f, repeatRate);
  }

  public void CancelInvoke(Action action)
  {
    if (action is null) return;
    _ = _invokes.Remove(action.Method.Name);
    _ = _repeatingInvokes.Remove(action.Method.Name);
  }

  public bool IsInvoking(Action action)
  {
    if (action is null) return false;
    return _invokes.ContainsKey(action.Method.Name) || _repeatingInvokes.ContainsKey(action.Method.Name);
  }

  public Coroutine StartCoroutine(Func<IEnumerator> routine)
  {
    if (routine is null) return new Coroutine(Task.CompletedTask, null, null);
    return StartCoroutine(routine());
  }

  public new Coroutine StartCoroutine<T>(Func<T> routine) where T : IEnumerator
  {
    if (routine is null) return new Coroutine(Task.CompletedTask, null, null);
    return StartCoroutine(routine());
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
}
