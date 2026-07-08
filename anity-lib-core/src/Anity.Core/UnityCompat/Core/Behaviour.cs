namespace UnityEngine;

public class Behaviour : Component
{
  public bool enabled = true;
  public bool useGUILayout = true;
  public bool runInEditMode;

  public bool isActiveAndEnabled => enabled;
}
