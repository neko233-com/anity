namespace UnityEngine;

public class AvatarMask : Object
{
  public string name { get; set; } = string.Empty;
  public int humanMachineCount { get; set; }
  public int skeletonCount { get; set; }

  public bool GetHumanoidBodyPartMask(HumanBodyBones humanBodyPart)
  {
    _ = humanBodyPart;
    return false;
  }

  public void SetHumanoidBodyPartMask(HumanBodyBones humanBodyPart, bool value)
  {
    _ = humanBodyPart;
    _ = value;
  }

  public bool GetTransformMask(Transform transform)
  {
    _ = transform;
    return false;
  }

  public void SetTransformMask(Transform transform, bool value)
  {
    _ = transform;
    _ = value;
  }
}
