using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace UnityEngine;

[Bindings.NativeHeader("Runtime/Mono/MonoBehaviour.h")]
[Bindings.NativeHeader("Runtime/Scripting/DelayedCallUtility.h")]
[ExtensionOfNativeClass]
[Scripting.RequiredByNativeCode]
public class MonoBehaviour : Behaviour
{
  private bool _awakened;
  internal bool _started;
  private readonly List<Coroutine> _coroutines = new();
  private readonly Dictionary<string, InvokeCall> _invokeCalls = new();
  private readonly CancellationTokenSource _destroyCancellation = new();

  public bool useGUILayout { get; set; } = true;
  public bool runInEditMode { get; set; }
  public CancellationToken destroyCancellationToken => _destroyCancellation.Token;

  internal bool IsStarted => _started;
  internal bool IsAwakened => _awakened;

  internal void InternalStart()
  {
    if (_started) return;
    _started = true;
    try
    {
      if (InvokeUnityMessage("Start") is IEnumerator routine)
        StartCoroutine(routine);
    }
    catch { }
  }

  internal void InternalUpdate()
  {
    if (!isActiveAndEnabled) return;
    if (!_started) InternalStart();
    try { InvokeUnityMessage("Update"); } catch { }
    TickInvokes();
    TickCoroutines();
  }

  internal void InternalFixedUpdate()
  {
    if (!isActiveAndEnabled) return;
    try { InvokeUnityMessage("FixedUpdate"); } catch { }
  }

  internal void InternalLateUpdate()
  {
    if (!isActiveAndEnabled) return;
    try { InvokeUnityMessage("LateUpdate"); } catch { }
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

    if (coroutine.WaitingForAsyncOperation != null)
    {
      if (!coroutine.WaitingForAsyncOperation.isDone)
        return true;
      coroutine.WaitingForAsyncOperation = null;
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

    if (current is AsyncOperation asyncOperation)
    {
      coroutine.WaitingForAsyncOperation = asyncOperation;
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

  [Internal.ExcludeFromDocs]
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

  public Coroutine StartCoroutine(string methodName, [Internal.DefaultValue("null")] object? value)
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

  [Obsolete("StartCoroutine_Auto has been deprecated. Use StartCoroutine instead (UnityUpgradable) -> StartCoroutine([mscorlib] System.Collections.IEnumerator)", false)]
  public Coroutine StartCoroutine_Auto(IEnumerator routine) => StartCoroutine(routine);

  public void StopCoroutine(Coroutine? routine)
  {
    if (routine is null) return;
    routine.Finished = true;
    _coroutines.Remove(routine);
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

  public static void print(object? message)
  {
    Debug.Log(message);
  }

  private object? InvokeUnityMessage(string methodName)
  {
    MethodInfo? method = FindUnityMessage(methodName);
    return method?.Invoke(this, null);
  }

  private MethodInfo? FindUnityMessage(string methodName)
  {
    for (Type? current = GetType(); current is not null && current != typeof(MonoBehaviour); current = current.BaseType)
    {
      MethodInfo? method = current.GetMethod(
        methodName,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
        null,
        Type.EmptyTypes,
        null);
      if (method is not null)
        return method;
    }
    return null;
  }

  internal bool HasUnityMessage(string methodName) => FindUnityMessage(methodName) is not null;

  internal void InternalAnimatorMove()
  {
    if (!isActiveAndEnabled) return;
    try { InvokeUnityMessage("OnAnimatorMove"); } catch { }
  }

  internal void InternalAwake()
  {
    if (_awakened) return;
    _awakened = true;
    try { InvokeUnityMessage("Awake"); } catch { }
  }
  internal void InternalOnEnable() { try { InvokeUnityMessage("OnEnable"); } catch { } }
  internal void InternalOnDisable() { try { InvokeUnityMessage("OnDisable"); } catch { } }
  internal void InternalOnDestroy()
  {
    if (!_destroyCancellation.IsCancellationRequested)
      _destroyCancellation.Cancel();
    try { InvokeUnityMessage("OnDestroy"); } catch { }
  }
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
