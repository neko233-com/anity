using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;
using Xunit;

namespace Anity.Core.Tests;

public sealed class HumanTraitTests
{
    [Fact]
    public void HumanPoseStoresBodyAndCompleteMuscleVector()
    {
        var pose = new HumanPose
        {
            bodyPosition = new Vector3(1f, 2f, 3f),
            bodyRotation = Quaternion.Euler(10f, 20f, 30f),
            muscles = Enumerable.Range(0, HumanTrait.MuscleCount).Select(index => index / 100f).ToArray(),
        };
        Assert.Equal(new Vector3(1f, 2f, 3f), pose.bodyPosition);
        Assert.Equal(HumanTrait.MuscleCount, pose.muscles.Length);
        Assert.Equal(.94f, pose.muscles[94]);
    }

    [Fact]
    public void CountsMatchUnity2022MecanimTable()
    {
        Assert.Equal(95, HumanTrait.MuscleCount);
        Assert.Equal(55, HumanTrait.BoneCount);
        Assert.Equal(15, HumanTrait.RequiredBoneCount);
        Assert.Equal(95, MuscleHandle.muscleHandleCount);
    }

    [Fact]
    public void CompleteMuscleTableMatchesUnityProbeFingerprint()
    {
        var text = new StringBuilder();
        for (int index = 0; index < HumanTrait.MuscleCount; ++index)
            text.Append("M\t").Append(index).Append('\t').Append(HumanTrait.MuscleName[index]).Append('\t')
                .Append(HumanTrait.BoneFromMuscle(index)).Append('\t')
                .Append(Float(HumanTrait.GetMuscleDefaultMin(index))).Append('\t')
                .Append(Float(HumanTrait.GetMuscleDefaultMax(index))).Append('\n');
        Assert.Equal("D6B3DE9F19E34E0CE5AE390ED300B2D9C0F94336846E9EEE368DE6E24DC81D72", Hash(text));
    }

    [Fact]
    public void CompleteBoneTableMatchesUnityProbeFingerprint()
    {
        var text = new StringBuilder();
        for (int index = 0; index < HumanTrait.BoneCount; ++index)
            text.Append("B\t").Append(index).Append('\t').Append(HumanTrait.BoneName[index]).Append('\t')
                .Append(HumanTrait.GetParentBone(index)).Append('\t')
                .Append(HumanTrait.RequiredBone(index) ? 1 : 0).Append('\t')
                .Append(Float(HumanTrait.GetBoneDefaultHierarchyMass(index))).Append('\t')
                .Append(HumanTrait.MuscleFromBone(index, 0)).Append('\t')
                .Append(HumanTrait.MuscleFromBone(index, 1)).Append('\t')
                .Append(HumanTrait.MuscleFromBone(index, 2)).Append('\n');
        Assert.Equal("728226F70077D85191D83D25C7DA1F31BF79444BBFF1A70FFABBAF646C04FC1F", Hash(text));
    }

    [Fact]
    public void CompleteMuscleHandleTableMatchesUnityProbeFingerprint()
    {
        var handles = new MuscleHandle[MuscleHandle.muscleHandleCount];
        MuscleHandle.GetMuscleHandles(handles);
        var text = new StringBuilder();
        for (int index = 0; index < handles.Length; ++index)
            text.Append("H\t").Append(index).Append('\t').Append(handles[index].name).Append('\t')
                .Append((int)handles[index].humanPartDof).Append('\t').Append(handles[index].dof).Append('\n');
        Assert.Equal("869692EB76C5D994C56344B867B67EE66E89C008936F73AD0FE190C1F0D40C37", Hash(text));
    }

    [Fact]
    public void NamePropertiesReturnIndependentCopies()
    {
        string[] muscles = HumanTrait.MuscleName;
        string[] bones = HumanTrait.BoneName;
        muscles[0] = "changed";
        bones[0] = "changed";
        Assert.Equal("Spine Front-Back", HumanTrait.MuscleName[0]);
        Assert.Equal("Hips", HumanTrait.BoneName[0]);
        Assert.NotSame(muscles, HumanTrait.MuscleName);
        Assert.NotSame(bones, HumanTrait.BoneName);
    }

    [Fact]
    public void InvalidIndicesReturnUnitySentinels()
    {
        Assert.Equal(-1, HumanTrait.BoneFromMuscle(-1));
        Assert.Equal(-1, HumanTrait.BoneFromMuscle(HumanTrait.MuscleCount));
        Assert.Equal(-1, HumanTrait.MuscleFromBone(-1, 0));
        Assert.Equal(-1, HumanTrait.MuscleFromBone(0, -1));
        Assert.Equal(-1, HumanTrait.MuscleFromBone(0, 3));
        Assert.Equal(-1, HumanTrait.GetParentBone(-1));
        Assert.False(HumanTrait.RequiredBone(-1));
        Assert.True(float.IsNaN(HumanTrait.GetBoneDefaultHierarchyMass(-1)));
        Assert.Equal(0f, HumanTrait.GetMuscleDefaultMin(-1));
        Assert.Equal(0f, HumanTrait.GetMuscleDefaultMax(HumanTrait.MuscleCount));
    }

    [Fact]
    public void RequiredBonesMatchUnityRequiredSet()
    {
        int[] required = Enumerable.Range(0, HumanTrait.BoneCount).Where(HumanTrait.RequiredBone).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 10, 13, 14, 15, 16, 17, 18 }, required);
    }

    [Fact]
    public void EveryMuscleMapsBackThroughItsBoneAxes()
    {
        for (int muscle = 0; muscle < HumanTrait.MuscleCount; ++muscle)
        {
            int bone = HumanTrait.BoneFromMuscle(muscle);
            Assert.InRange(bone, 0, HumanTrait.BoneCount - 1);
            Assert.Contains(muscle, new[]
            {
                HumanTrait.MuscleFromBone(bone, 0),
                HumanTrait.MuscleFromBone(bone, 1),
                HumanTrait.MuscleFromBone(bone, 2),
            });
        }
    }

    [Fact]
    public void AsymmetricDefaultRangesMatchUnity()
    {
        Assert.Equal(-10f, HumanTrait.GetMuscleDefaultMin(15));
        Assert.Equal(15f, HumanTrait.GetMuscleDefaultMax(15));
        Assert.Equal(-90f, HumanTrait.GetMuscleDefaultMin(21));
        Assert.Equal(50f, HumanTrait.GetMuscleDefaultMax(21));
        Assert.Equal(-7.5f, HumanTrait.GetMuscleDefaultMin(64));
        Assert.Equal(7.5f, HumanTrait.GetMuscleDefaultMax(64));
    }

    [Fact]
    public void BodyAndHeadConstructorsExposeExactHandleIdentity()
    {
        var body = new MuscleHandle(BodyDof.UpperChestRollLeftRight);
        Assert.Equal(HumanPartDof.Body, body.humanPartDof);
        Assert.Equal(8, body.dof);
        Assert.Equal("UpperChest Twist Left-Right", body.name);
        var head = new MuscleHandle(HeadDof.JawLeftRight);
        Assert.Equal(HumanPartDof.Head, head.humanPartDof);
        Assert.Equal(11, head.dof);
        Assert.Equal("Jaw Left-Right", head.name);
    }

    [Fact]
    public void ArmConstructorsDistinguishLeftAndRight()
    {
        Assert.Equal("Left Arm Down-Up", new MuscleHandle(HumanPartDof.LeftArm, ArmDof.ArmDownUp).name);
        Assert.Equal("Right Arm Down-Up", new MuscleHandle(HumanPartDof.RightArm, ArmDof.ArmDownUp).name);
    }

    [Fact]
    public void LegConstructorsDistinguishLeftAndRight()
    {
        Assert.Equal("Left Toes Up-Down", new MuscleHandle(HumanPartDof.LeftLeg, LegDof.ToesUpDown).name);
        Assert.Equal("Right Toes Up-Down", new MuscleHandle(HumanPartDof.RightLeg, LegDof.ToesUpDown).name);
    }

    [Fact]
    public void FingerConstructorsUseUnityHandleNaming()
    {
        var handle = new MuscleHandle(HumanPartDof.RightRing, FingerDof.IntermediateCloseOpen);
        Assert.Equal(HumanPartDof.RightRing, handle.humanPartDof);
        Assert.Equal(2, handle.dof);
        Assert.Equal("RightHand.Ring.2 Stretched", handle.name);
    }

    [Fact]
    public void InvalidArmPartThrowsUnityMessage()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new MuscleHandle(HumanPartDof.LeftLeg, ArmDof.ArmDownUp));
        Assert.Equal("Invalid HumanPartDof for an arm, please use either HumanPartDof.LeftArm or HumanPartDof.RightArm.", exception.Message);
    }

    [Fact]
    public void InvalidLegPartThrowsUnityMessage()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new MuscleHandle(HumanPartDof.LeftArm, LegDof.FootCloseOpen));
        Assert.Equal("Invalid HumanPartDof for a leg, please use either HumanPartDof.LeftLeg or HumanPartDof.RightLeg.", exception.Message);
    }

    [Fact]
    public void InvalidFingerPartThrowsUnityMessage()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new MuscleHandle(HumanPartDof.Head, FingerDof.DistalCloseOpen));
        Assert.Equal("Invalid HumanPartDof for a finger.", exception.Message);
    }

    [Fact]
    public void GetMuscleHandlesAcceptsShortAndLongArrays()
    {
        var shortArray = new MuscleHandle[1];
        MuscleHandle.GetMuscleHandles(shortArray);
        Assert.Equal("Spine Front-Back", shortArray[0].name);
        var longArray = new MuscleHandle[MuscleHandle.muscleHandleCount + 1];
        MuscleHandle.GetMuscleHandles(longArray);
        Assert.Equal("RightHand.Little.3 Stretched", longArray[94].name);
        Assert.Equal("Spine Front-Back", longArray[95].name);
    }

    [Fact]
    public void GetMuscleHandlesRejectsNullWithUnityParameterName()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => MuscleHandle.GetMuscleHandles(null!));
        Assert.Equal("muscleHandles", exception.ParamName);
    }

    private static string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Hash(StringBuilder value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())));
}
