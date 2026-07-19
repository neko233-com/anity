using System;
using System.Collections.Generic;
using System.Text;
using Anity.Core.Runtime.Native;

namespace UnityEngine;

[UnityEngine.Bindings.NativeHeader("Modules/Animation/Avatar.h")]
[UnityEngine.Scripting.UsedByNativeCode]
public class Avatar : Object
{
  private bool _isValid;
  private bool _isHuman;
  private HumanDescription _humanDescription;

  internal Avatar()
  {
  }

  public bool isValid => _isValid;

  public bool isHuman => _isHuman;

  public HumanDescription humanDescription => _humanDescription;

  internal AnityNative.AvatarValidationFlags ValidationFlags { get; private set; }

  internal static Avatar Create(
    bool isValid,
    bool isHuman,
    HumanDescription humanDescription,
    AnityNative.AvatarValidationFlags validationFlags = AnityNative.AvatarValidationFlags.Valid)
  {
    return new Avatar
    {
      _isValid = isValid,
      _isHuman = isHuman,
      _humanDescription = humanDescription,
      ValidationFlags = validationFlags,
    };
  }
}

[UnityEngine.Bindings.NativeHeader("Modules/Animation/ScriptBindings/AvatarBuilder.bindings.h")]
public class AvatarBuilder
{
  public AvatarBuilder()
  {
  }

  [UnityEngine.Bindings.FreeFunction("AvatarBuilderBindings::BuildGenericAvatar")]
  public static Avatar BuildGenericAvatar(
    [UnityEngine.Bindings.NotNull("ArgumentNullException")] GameObject go,
    [UnityEngine.Bindings.NotNull("ArgumentNullException")] string rootMotionTransformName)
  {
    if (go is null) throw new ArgumentNullException(nameof(go));
    if (rootMotionTransformName is null) throw new ArgumentNullException(nameof(rootMotionTransformName));

    AnityNative.AvatarSkeletonBoneDesc[] skeleton = BuildGenericSkeleton(go.transform);
    bool nativeCallSucceeded = AnityNative.TryValidateGenericAvatar(
      skeleton,
      StableHash(rootMotionTransformName),
      out AnityNative.AvatarBuildResult result);
    return Avatar.Create(
      nativeCallSucceeded && result.flags == AnityNative.AvatarValidationFlags.Valid,
      false,
      default,
      nativeCallSucceeded ? result.flags : AnityNative.AvatarValidationFlags.InvalidParent);
  }

  public static Avatar BuildHumanAvatar(GameObject go, HumanDescription humanDescription)
  {
    if (go is null) throw new ArgumentNullException(nameof(go));

    SkeletonBone[] sourceSkeleton = humanDescription.skeleton ?? Array.Empty<SkeletonBone>();
    HumanBone[] sourceHuman = humanDescription.human ?? Array.Empty<HumanBone>();
    AnityNative.AvatarSkeletonBoneDesc[] skeleton = BuildHumanSkeleton(go.transform, sourceSkeleton);
    var human = new AnityNative.AvatarHumanBoneDesc[sourceHuman.Length];
    for (int index = 0; index < sourceHuman.Length; ++index)
    {
      human[index] = new AnityNative.AvatarHumanBoneDesc
      {
        boneNameHash = StableHash(sourceHuman[index].boneName),
        humanNameHash = StableHash(sourceHuman[index].humanName),
      };
    }

    bool nativeCallSucceeded = AnityNative.TryValidateHumanAvatar(skeleton, human, out AnityNative.AvatarBuildResult result);
    return Avatar.Create(
      nativeCallSucceeded && result.flags == AnityNative.AvatarValidationFlags.Valid,
      true,
      humanDescription,
      nativeCallSucceeded ? result.flags : AnityNative.AvatarValidationFlags.InvalidParent);
  }

  private static AnityNative.AvatarSkeletonBoneDesc[] BuildHumanSkeleton(
    Transform root,
    SkeletonBone[] sourceSkeleton)
  {
    var transformsByName = new Dictionary<string, List<Transform>>(StringComparer.Ordinal);
    CollectTransforms(root, transformsByName);
    var skeletonIndices = new Dictionary<string, List<int>>(StringComparer.Ordinal);
    for (int index = 0; index < sourceSkeleton.Length; ++index)
    {
      string name = sourceSkeleton[index].name ?? string.Empty;
      if (!skeletonIndices.TryGetValue(name, out List<int> indices))
      {
        indices = new List<int>();
        skeletonIndices.Add(name, indices);
      }
      indices.Add(index);
    }

    var result = new AnityNative.AvatarSkeletonBoneDesc[sourceSkeleton.Length];
    for (int index = 0; index < sourceSkeleton.Length; ++index)
    {
      SkeletonBone bone = sourceSkeleton[index];
      string name = bone.name ?? string.Empty;
      int parentIndex = -2;
      if (transformsByName.TryGetValue(name, out List<Transform> matches) && matches.Count == 1)
      {
        Transform transform = matches[0];
        if (ReferenceEquals(transform, root))
        {
          parentIndex = -1;
        }
        else
        {
          string parentName = transform.parent?.gameObject?.name ?? string.Empty;
          if (skeletonIndices.TryGetValue(parentName, out List<int> parentMatches) && parentMatches.Count == 1)
            parentIndex = parentMatches[0];
        }
      }
      result[index] = ToNativeSkeletonBone(name, parentIndex, bone.position, bone.rotation, bone.scale);
    }
    return result;
  }

  private static AnityNative.AvatarSkeletonBoneDesc[] BuildGenericSkeleton(Transform root)
  {
    var transforms = new List<Transform>();
    CollectTransforms(root, transforms);
    var indices = new Dictionary<Transform, int>();
    for (int index = 0; index < transforms.Count; ++index) indices.Add(transforms[index], index);

    var result = new AnityNative.AvatarSkeletonBoneDesc[transforms.Count];
    for (int index = 0; index < transforms.Count; ++index)
    {
      Transform transform = transforms[index];
      int parentIndex = transform.parent is not null && indices.TryGetValue(transform.parent, out int match) ? match : -1;
      result[index] = ToNativeSkeletonBone(
        transform.gameObject?.name ?? string.Empty,
        parentIndex,
        transform.localPosition,
        transform.localRotation,
        transform.localScale);
    }
    return result;
  }

  private static AnityNative.AvatarSkeletonBoneDesc ToNativeSkeletonBone(
    string name,
    int parentIndex,
    Vector3 position,
    Quaternion rotation,
    Vector3 scale)
  {
    return new AnityNative.AvatarSkeletonBoneDesc
    {
      nameHash = StableHash(name),
      parentIndex = parentIndex,
      positionX = position.x,
      positionY = position.y,
      positionZ = position.z,
      rotationX = rotation.x,
      rotationY = rotation.y,
      rotationZ = rotation.z,
      rotationW = rotation.w,
      scaleX = scale.x,
      scaleY = scale.y,
      scaleZ = scale.z,
    };
  }

  private static void CollectTransforms(Transform transform, Dictionary<string, List<Transform>> transformsByName)
  {
    string name = transform.gameObject?.name ?? string.Empty;
    if (!transformsByName.TryGetValue(name, out List<Transform> matches))
    {
      matches = new List<Transform>();
      transformsByName.Add(name, matches);
    }
    matches.Add(transform);
    for (int index = 0; index < transform.childCount; ++index)
      CollectTransforms(transform.GetChild(index), transformsByName);
  }

  private static void CollectTransforms(Transform transform, List<Transform> transforms)
  {
    transforms.Add(transform);
    for (int index = 0; index < transform.childCount; ++index)
      CollectTransforms(transform.GetChild(index), transforms);
  }

  private static ulong StableHash(string value)
  {
    if (string.IsNullOrEmpty(value)) return 0;
    ulong hash = 14695981039346656037UL;
    foreach (byte item in Encoding.UTF8.GetBytes(value))
    {
      hash ^= item;
      hash *= 1099511628211UL;
    }
    return hash;
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
