using System;

namespace UnityEngine;

[Bindings.NativeHeader("Runtime/Mono/MonoBehaviour.h")]
[Scripting.UsedByNativeCode]
public class Behaviour : Component
{
  private bool _enabled = true;

  [Bindings.NativeProperty]
  [Scripting.RequiredByNativeCode]
  public bool enabled
  {
    get => _enabled;
    set
    {
      if (_enabled == value) return;
      _enabled = value;

      if (gameObject is null) return;

      bool isActive = gameObject.activeInHierarchy;
      if (isActive)
      {
        if (this is MonoBehaviour mb)
        {
          try
          {
            if (_enabled) mb.InternalOnEnable();
            else mb.InternalOnDisable();
          }
          catch { }
        }
      }
    }
  }

  [Bindings.NativeProperty]
  public bool isActiveAndEnabled => _enabled && gameObject is not null && gameObject.activeInHierarchy;
}
