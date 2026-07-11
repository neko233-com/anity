using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

namespace UnityEngine;

public static class UnityRuntime
{
  private static float _fixedTimeAccumulator;
  private static MonoBehaviour[] _cachedMonoBehaviours = Array.Empty<MonoBehaviour>();
  private static Animator[] _cachedAnimators = Array.Empty<Animator>();
  private static ParticleSystem[] _cachedParticleSystems = Array.Empty<ParticleSystem>();

  public static void Tick(float deltaTime)
  {
    Time.Tick(deltaTime);

    Input.UpdatePerFrame();

    UpdateCachedObjects();

    foreach (var mb in _cachedMonoBehaviours)
    {
      if (mb != null && !mb.IsDestroyed && mb.isActiveAndEnabled)
      {
        try { mb.InternalUpdate(); } catch { }
      }
    }

    _fixedTimeAccumulator += Time.deltaTime;
    while (_fixedTimeAccumulator >= Time.fixedDeltaTime)
    {
      _fixedTimeAccumulator -= Time.fixedDeltaTime;
      Time.FixedTick();

      Physics.Simulate(Time.fixedDeltaTime);
      Physics2D.Simulate(Time.fixedDeltaTime);

      foreach (var mb in _cachedMonoBehaviours)
      {
        if (mb != null && !mb.IsDestroyed && mb.isActiveAndEnabled)
        {
          try { mb.InternalFixedUpdate(); } catch { }
          try { mb.TickFixedUpdateCoroutines(); } catch { }
        }
      }
    }

    foreach (var animator in _cachedAnimators)
    {
      if (animator != null && !animator.IsDestroyed && animator.enabled)
      {
        try { animator.Update(deltaTime); } catch { }
      }
    }

    foreach (var ps in _cachedParticleSystems)
    {
      if (ps != null && !ps.IsDestroyed)
      {
        try { ps.Simulate(deltaTime, true, false); } catch { }
      }
    }

    try { CanvasUpdateRegistry.instance.PerformUpdate(); } catch { }

    Camera.RenderAll();

    foreach (var mb in _cachedMonoBehaviours)
    {
      if (mb != null && !mb.IsDestroyed && mb.isActiveAndEnabled)
      {
        try { mb.InternalLateUpdate(); } catch { }
        try { mb.TickEndOfFrameCoroutines(); } catch { }
      }
    }

    Object.TickDestroyQueue();

    Debug.TickLines(deltaTime);
  }

  private static void UpdateCachedObjects()
  {
    _cachedMonoBehaviours = Object.FindObjectsOfType<MonoBehaviour>();
    _cachedAnimators = Object.FindObjectsOfType<Animator>();
    _cachedParticleSystems = Object.FindObjectsOfType<ParticleSystem>();
  }
}
