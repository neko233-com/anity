using System;

namespace UnityEngine;

public class Behaviour : Component
{
  private bool _enabled = true;
  private bool _wasEnabled;

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

  public bool useGUILayout { get; set; } = true;
  public bool runInEditMode { get; set; }

  public bool isActiveAndEnabled => _enabled && gameObject is not null && gameObject.activeInHierarchy;
}
