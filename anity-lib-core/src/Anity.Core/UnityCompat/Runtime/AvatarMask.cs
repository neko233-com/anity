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

public enum HumanBodyBones
{
  Hips,
  Spine,
  Chest,
  UpperChest,
  Neck,
  Head,
  LeftShoulder,
  LeftUpperArm,
  LeftLowerArm,
  LeftHand,
  RightShoulder,
  RightUpperArm,
  RightLowerArm,
  RightHand,
  LeftUpperLeg,
  LeftLowerLeg,
  LeftFoot,
  LeftToes,
  RightUpperLeg,
  RightLowerLeg,
  RightFoot,
  RightToes,
  LeftThumbProximal,
  LeftThumbIntermediate,
  LeftThumbDistal,
  LeftIndexProximal,
  LeftIndexIntermediate,
  LeftIndexDistal,
  LeftMiddleProximal,
  LeftMiddleIntermediate,
  LeftMiddleDistal,
  LeftRingProximal,
  LeftRingIntermediate,
  LeftRingDistal,
  LeftLittleProximal,
  LeftLittleIntermediate,
  LeftLittleDistal,
  RightThumbProximal,
  RightThumbIntermediate,
  RightThumbDistal,
  RightIndexProximal,
  RightIndexIntermediate,
  RightIndexDistal,
  RightMiddleProximal,
  RightMiddleIntermediate,
  RightMiddleDistal,
  RightRingProximal,
  RightRingIntermediate,
  RightRingDistal,
  RightLittleProximal,
  RightLittleIntermediate,
  RightLittleDistal,
  LastBone
}
