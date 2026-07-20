using System;
using System.Runtime.InteropServices;
using Anity.Core.Runtime.Native;
using UnityEngine.Bindings;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine
{
  public struct HumanPose
  {
    public Vector3 bodyPosition;
    public Quaternion bodyRotation;
    public float[] muscles;
  }

  public enum BodyDof
  {
    SpineFrontBack = 0,
    SpineLeftRight = 1,
    SpineRollLeftRight = 2,
    ChestFrontBack = 3,
    ChestLeftRight = 4,
    ChestRollLeftRight = 5,
    UpperChestFrontBack = 6,
    UpperChestLeftRight = 7,
    UpperChestRollLeftRight = 8,
    LastBodyDof = 9,
  }

  public enum HeadDof
  {
    NeckFrontBack = 0,
    NeckLeftRight = 1,
    NeckRollLeftRight = 2,
    HeadFrontBack = 3,
    HeadLeftRight = 4,
    HeadRollLeftRight = 5,
    LeftEyeDownUp = 6,
    LeftEyeInOut = 7,
    RightEyeDownUp = 8,
    RightEyeInOut = 9,
    JawDownUp = 10,
    JawLeftRight = 11,
    LastHeadDof = 12,
  }

  public enum HumanPartDof
  {
    Body = 0,
    Head = 1,
    LeftLeg = 2,
    RightLeg = 3,
    LeftArm = 4,
    RightArm = 5,
    LeftThumb = 6,
    LeftIndex = 7,
    LeftMiddle = 8,
    LeftRing = 9,
    LeftLittle = 10,
    RightThumb = 11,
    RightIndex = 12,
    RightMiddle = 13,
    RightRing = 14,
    RightLittle = 15,
    LastHumanPartDof = 16,
  }

  public enum ArmDof
  {
    ShoulderDownUp = 0,
    ShoulderFrontBack = 1,
    ArmDownUp = 2,
    ArmFrontBack = 3,
    ArmRollInOut = 4,
    ForeArmCloseOpen = 5,
    ForeArmRollInOut = 6,
    HandDownUp = 7,
    HandInOut = 8,
    LastArmDof = 9,
  }

  public enum LegDof
  {
    UpperLegFrontBack = 0,
    UpperLegInOut = 1,
    UpperLegRollInOut = 2,
    LegCloseOpen = 3,
    LegRollInOut = 4,
    FootCloseOpen = 5,
    FootInOut = 6,
    ToesUpDown = 7,
    LastLegDof = 8,
  }

  public enum FingerDof
  {
    ProximalDownUp = 0,
    ProximalInOut = 1,
    IntermediateCloseOpen = 2,
    DistalCloseOpen = 3,
    LastFingerDof = 4,
  }

  [NativeHeader("Modules/Animation/HumanTrait.h")]
  public class HumanTrait
  {
    public HumanTrait()
    {
    }

    public static int MuscleCount => HumanTraitData.MuscleCount;
    public static int BoneCount => HumanTraitData.BoneCount;
    public static int RequiredBoneCount => HumanTraitData.RequiredBoneCount;
    public static string[] MuscleName => HumanTraitData.CopyMuscleNames();
    public static string[] BoneName => HumanTraitData.CopyBoneNames();

    public static int BoneFromMuscle(int i)
      => HumanTraitData.TryGetMuscle(i, out HumanTraitData.Muscle muscle) ? muscle.BoneIndex : -1;

    public static int MuscleFromBone(int i, int dofIndex)
    {
      if (!HumanTraitData.TryGetBone(i, out HumanTraitData.Bone bone)) return -1;
      return dofIndex switch
      {
        0 => bone.MuscleX,
        1 => bone.MuscleY,
        2 => bone.MuscleZ,
        _ => -1,
      };
    }

    public static bool RequiredBone(int i)
      => HumanTraitData.TryGetBone(i, out HumanTraitData.Bone bone) && bone.Required;

    public static int GetParentBone(int i)
      => HumanTraitData.TryGetBone(i, out HumanTraitData.Bone bone) ? bone.ParentBoneIndex : -1;

    public static float GetMuscleDefaultMin(int i)
      => HumanTraitData.TryGetMuscle(i, out HumanTraitData.Muscle muscle) ? muscle.DefaultMin : 0f;

    public static float GetMuscleDefaultMax(int i)
      => HumanTraitData.TryGetMuscle(i, out HumanTraitData.Muscle muscle) ? muscle.DefaultMax : 0f;

    public static float GetBoneDefaultHierarchyMass(int i)
      => HumanTraitData.TryGetBone(i, out HumanTraitData.Bone bone) ? bone.DefaultHierarchyMass : float.NaN;
  }

  internal static class HumanTraitData
  {
    internal readonly struct Muscle
    {
      internal Muscle(AnityNative.HumanTraitMuscleInfo native)
      {
        MuscleName = NativeString(native.muscleName);
        HandleName = NativeString(native.handleName);
        BoneIndex = native.boneIndex;
        DefaultMin = native.defaultMin;
        DefaultMax = native.defaultMax;
        HumanPartDof = native.humanPartDof;
        Dof = native.dof;
      }

      internal string MuscleName { get; }
      internal string HandleName { get; }
      internal int BoneIndex { get; }
      internal float DefaultMin { get; }
      internal float DefaultMax { get; }
      internal int HumanPartDof { get; }
      internal int Dof { get; }
    }

    internal readonly struct Bone
    {
      internal Bone(AnityNative.HumanTraitBoneInfo native)
      {
        BoneName = NativeString(native.boneName);
        ParentBoneIndex = native.parentBoneIndex;
        Required = native.required != 0;
        DefaultHierarchyMass = native.defaultHierarchyMass;
        MuscleX = native.muscleX;
        MuscleY = native.muscleY;
        MuscleZ = native.muscleZ;
      }

      internal string BoneName { get; }
      internal int ParentBoneIndex { get; }
      internal bool Required { get; }
      internal float DefaultHierarchyMass { get; }
      internal int MuscleX { get; }
      internal int MuscleY { get; }
      internal int MuscleZ { get; }
    }

    private sealed class Table
    {
      internal Table()
      {
        if (!AnityNative.TryGetHumanTraitCounts(out int muscleCount, out int boneCount, out int requiredBoneCount))
          throw new InvalidOperationException("anity-native HumanTrait table is unavailable.");
        Muscles = new Muscle[muscleCount];
        for (int index = 0; index < Muscles.Length; ++index)
        {
          if (!AnityNative.TryGetHumanTraitMuscleInfo(index, out AnityNative.HumanTraitMuscleInfo info))
            throw new InvalidOperationException("anity-native returned an incomplete HumanTrait muscle table.");
          Muscles[index] = new Muscle(info);
        }
        Bones = new Bone[boneCount];
        for (int index = 0; index < Bones.Length; ++index)
        {
          if (!AnityNative.TryGetHumanTraitBoneInfo(index, out AnityNative.HumanTraitBoneInfo info))
            throw new InvalidOperationException("anity-native returned an incomplete HumanTrait bone table.");
          Bones[index] = new Bone(info);
        }
        RequiredBoneCount = requiredBoneCount;
      }

      internal Muscle[] Muscles { get; }
      internal Bone[] Bones { get; }
      internal int RequiredBoneCount { get; }
    }

    private static readonly Lazy<Table> Data = new(() => new Table(), true);

    internal static int MuscleCount => Data.Value.Muscles.Length;
    internal static int BoneCount => Data.Value.Bones.Length;
    internal static int RequiredBoneCount => Data.Value.RequiredBoneCount;

    internal static bool TryGetMuscle(int index, out Muscle muscle)
    {
      if ((uint)index < (uint)Data.Value.Muscles.Length)
      {
        muscle = Data.Value.Muscles[index];
        return true;
      }
      muscle = default;
      return false;
    }

    internal static bool TryGetBone(int index, out Bone bone)
    {
      if ((uint)index < (uint)Data.Value.Bones.Length)
      {
        bone = Data.Value.Bones[index];
        return true;
      }
      bone = default;
      return false;
    }

    internal static string[] CopyMuscleNames()
    {
      var result = new string[Data.Value.Muscles.Length];
      for (int index = 0; index < result.Length; ++index) result[index] = Data.Value.Muscles[index].MuscleName;
      return result;
    }

    internal static string[] CopyBoneNames()
    {
      var result = new string[Data.Value.Bones.Length];
      for (int index = 0; index < result.Length; ++index) result[index] = Data.Value.Bones[index].BoneName;
      return result;
    }

    internal static string HandleName(HumanPartDof part, int dof)
    {
      if (!AnityNative.TryFindHumanTraitMuscleHandle((int)part, dof, out int muscleIndex) ||
          !TryGetMuscle(muscleIndex, out Muscle muscle)) return string.Empty;
      return muscle.HandleName;
    }

    private static string NativeString(IntPtr pointer)
      => pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(pointer) ?? string.Empty;
  }
}

namespace UnityEngine.Animations
{
  [NativeHeader("Modules/Animation/Animator.h")]
  [NativeHeader("Modules/Animation/MuscleHandle.h")]
  [MovedFrom("UnityEngine.Experimental.Animations")]
  public struct MuscleHandle
  {
    public MuscleHandle(BodyDof bodyDof)
    {
      humanPartDof = HumanPartDof.Body;
      dof = (int)bodyDof;
    }

    public MuscleHandle(HeadDof headDof)
    {
      humanPartDof = HumanPartDof.Head;
      dof = (int)headDof;
    }

    public MuscleHandle(HumanPartDof partDof, LegDof legDof)
    {
      if (partDof != HumanPartDof.LeftLeg && partDof != HumanPartDof.RightLeg)
        throw new InvalidOperationException("Invalid HumanPartDof for a leg, please use either HumanPartDof.LeftLeg or HumanPartDof.RightLeg.");
      humanPartDof = partDof;
      dof = (int)legDof;
    }

    public MuscleHandle(HumanPartDof partDof, ArmDof armDof)
    {
      if (partDof != HumanPartDof.LeftArm && partDof != HumanPartDof.RightArm)
        throw new InvalidOperationException("Invalid HumanPartDof for an arm, please use either HumanPartDof.LeftArm or HumanPartDof.RightArm.");
      humanPartDof = partDof;
      dof = (int)armDof;
    }

    public MuscleHandle(HumanPartDof partDof, FingerDof fingerDof)
    {
      if (partDof < HumanPartDof.LeftThumb || partDof > HumanPartDof.RightLittle)
        throw new InvalidOperationException("Invalid HumanPartDof for a finger.");
      humanPartDof = partDof;
      dof = (int)fingerDof;
    }

    public HumanPartDof humanPartDof { get; }
    public int dof { get; }
    public string name => HumanTraitData.HandleName(humanPartDof, dof);
    public static int muscleHandleCount => HumanTrait.MuscleCount;

    public static void GetMuscleHandles([Out, NotNull("ArgumentNullException")] MuscleHandle[] muscleHandles)
    {
      if (muscleHandles is null) throw new ArgumentNullException(nameof(muscleHandles));
      int count = Math.Min(muscleHandles.Length, HumanTraitData.MuscleCount);
      for (int index = 0; index < count; ++index)
      {
        HumanTraitData.TryGetMuscle(index, out HumanTraitData.Muscle muscle);
        muscleHandles[index] = new MuscleHandle((HumanPartDof)muscle.HumanPartDof, muscle.Dof, true);
      }
    }

    private MuscleHandle(HumanPartDof partDof, int dofIndex, bool _)
    {
      humanPartDof = partDof;
      dof = dofIndex;
    }
  }
}
