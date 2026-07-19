namespace UnityEngine;

public class Avatar : Object
{
  public string name { get; set; } = string.Empty;
  public bool isValid { get; set; }
  public bool isHuman { get; set; }
  public bool hasTransformHierarchy { get; set; }
  public float humanScale { get; set; } = 1f;
  public int avatarSize { get; set; }
  public int muscleCount { get; set; } = 95;
  public Transform? rootBone { get; set; }
  public Vector3 bodyPosition { get; set; }
  public Quaternion bodyRotation { get; set; } = Quaternion.identity;

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

  public string BoneNameToHumanBoneName(string boneName)
  {
    if (humanDescription.human == null) return string.Empty;
    foreach (var hb in humanDescription.human)
    {
      if (hb.boneName == boneName) return hb.humanName;
    }
    return string.Empty;
  }
}

public static class AvatarBuilder
{
  public static Avatar BuildHumanAvatar(GameObject go, HumanDescription humanDescription)
  {
    _ = go;
    var avatar = new Avatar
    {
      isValid = true,
      isHuman = true,
      hasTransformHierarchy = true,
      humanDescription = humanDescription,
      rootBone = go?.transform
    };
    return avatar;
  }
}

public struct RawAvatar
{
  public string name { get; set; }
  public Transform rootBone { get; set; }
  public HumanBone[] human { get; set; }
  public SkeletonBone[] skeleton { get; set; }
  public float humanScale { get; set; }
  public bool isHuman { get; set; }
  public bool hasTranslationDoF { get; set; }
}

[UnityEngine.Bindings.NativeHeader("Modules/Animation/HumanDescription.h")]
[UnityEngine.Bindings.NativeType(1, "MonoHumanBone")]
[UnityEngine.Scripting.RequiredByNativeCode]
public struct HumanBone
{
  public string boneName { get; set; }
  public string humanName { get; set; }
  [UnityEngine.Bindings.NativeName("m_Limit")]
  public HumanLimit limit;
}

[UnityEngine.Bindings.NativeHeader("Modules/Animation/HumanDescription.h")]
[UnityEngine.Bindings.NativeType(1, "MonoSkeletonBone")]
[UnityEngine.Scripting.RequiredByNativeCode]
public struct SkeletonBone
{
  [UnityEngine.Bindings.NativeName("m_Name")]
  public string name;
  [UnityEngine.Bindings.NativeName("m_Position")]
  public Vector3 position;
  [UnityEngine.Bindings.NativeName("m_Rotation")]
  public Quaternion rotation;
  [UnityEngine.Bindings.NativeName("m_Scale")]
  public Vector3 scale;

  [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
  [System.Obsolete("transformModified is no longer used and has been deprecated.", true)]
  public int transformModified { get; set; }
}

[UnityEngine.Bindings.NativeHeader("Modules/Animation/HumanDescription.h")]
[UnityEngine.Bindings.NativeHeader("Modules/Animation/ScriptBindings/AvatarBuilder.bindings.h")]
[UnityEngine.Bindings.NativeType(1, "MonoHumanLimit")]
public struct HumanLimit
{
  public bool useDefaultValues { get; set; }
  public Vector3 min { get; set; }
  public Vector3 max { get; set; }
  public Vector3 center { get; set; }
  public float axisLength { get; set; }
}

[UnityEngine.Bindings.NativeHeader("Modules/Animation/HumanDescription.h")]
[UnityEngine.Bindings.NativeHeader("Modules/Animation/ScriptBindings/AvatarBuilder.bindings.h")]
public struct HumanDescription
{
  [UnityEngine.Bindings.NativeName("m_Human")]
  public HumanBone[] human;
  [UnityEngine.Bindings.NativeName("m_Skeleton")]
  public SkeletonBone[] skeleton;
  public float upperArmTwist { get; set; }
  public float lowerArmTwist { get; set; }
  public float upperLegTwist { get; set; }
  public float lowerLegTwist { get; set; }
  public float armStretch { get; set; }
  public float legStretch { get; set; }
  public float feetSpacing { get; set; }
  public bool hasTranslationDoF { get; set; }
}
