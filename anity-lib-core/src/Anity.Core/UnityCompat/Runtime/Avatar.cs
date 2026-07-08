namespace UnityEngine;

public class Avatar : Object
{
  public string name { get; set; } = string.Empty;
  public bool isValid { get; set; }
  public bool isHuman { get; set; }
  public bool hasTransformHierarchy { get; set; }
  public float humanScale { get; set; } = 1f;
  public int avatarSize { get; set; }

  public static Avatar Build(RawAvatar avatar)
  {
    _ = avatar;
    return null;
  }

  public static Avatar Build(RawAvatar avatar, bool copyScripts)
  {
    _ = avatar;
    _ = copyScripts;
    return null;
  }

  public HumanDescription humanDescription { get; set; }

  public String BoneNameToHumanBoneName(string boneName)
  {
    _ = boneName;
    return string.Empty;
  }
}

public struct RawAvatar
{
  public string name { get; set; }
  public Transform skeleton { get; set; }
  public HumanBone[] human { get; set; }
  public SkeletonBone[] skeleton { get; set; }
  public float humanScale { get; set; }
  public bool isHuman { get; set; }
  public bool hasTranslationDoF { get; set; }
}

public struct HumanBone
{
  public string boneName { get; set; }
  public HumanLimit limit { get; set; }
}

public struct SkeletonBone
{
  public string name { get; set; }
  public Vector3 position { get; set; }
  public Quaternion rotation { get; set; }
  public Vector3 scale { get; set; }
  public bool transformModified { get; set; }
}

public struct HumanLimit
{
  public bool useDefaultValues { get; set; }
  public float min { get; set; }
  public float max { get; set; }
  public float center { get; set; }
  public float axisLength { get; set; }
}

public struct HumanDescription
{
  public HumanBone[] human { get; set; }
  public SkeletonBone[] skeleton { get; set; }
  public float upperArmTwist { get; set; }
  public float lowerArmTwist { get; set; }
  public float upperLegTwist { get; set; }
  public float lowerLegTwist { get; set; }
  public float armStretch { get; set; }
  public float legStretch { get; set; }
  public float feetSpacing { get; set; }
  public bool hasTranslationDoF { get; set; }
  public bool extraSkeleton { get; set; }
  public float armTwist { get; set; }
  public float forearmTwist { get; set; }
  public float legTwist { get; set; }
  public float armLength { get; set; }
  public float forearmLength { get; set; }
  public float handLength { get; set; }
  public float footLength { get; set; }
  public float fingerLength { get; set; }
  public float toeLength { get; set; }
  public float handFingerIK { get; set; }
  public float footIK { get; set; }
  public float armIK { get; set; }
  public float legIK { get; set; }
  public float bodyYaw { get; set; }
  public float bodyPitch { get; set; }
  public float bodyRoll { get; set; }
}
