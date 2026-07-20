using System.Reflection;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class HumanoidRootMotionRuntimeTests
{
    private const float HumanScale = 1.12227261f;

    [Fact]
    public void AnimatorHumanoidPropertiesAreGetterOnlyAndAvatarBacked()
    {
        PropertyInfo isHuman = typeof(Animator).GetProperty(nameof(Animator.isHuman))!;
        PropertyInfo humanScale = typeof(Animator).GetProperty(nameof(Animator.humanScale))!;
        Assert.Null(isHuman.SetMethod);
        Assert.Null(humanScale.SetMethod);

        var root = new GameObject("Humanoid");
        try
        {
            Animator animator = root.AddComponent<Animator>();
            Assert.False(animator.isHuman);
            animator.avatar = Avatar.Create(true, true, default);
            animator.SetHumanScale(HumanScale);
            Assert.True(animator.isHuman);
            Assert.Equal(HumanScale, animator.humanScale);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void RootBindingsExposeHumanoidMetadataWithoutInventedMotionCurves()
    {
        AnimationClip clip = CreateHumanoidClip();

        Assert.True(clip.isHumanMotion);
        Assert.True(clip.hasRootCurves);
        Assert.False(clip.hasMotionCurves);
        Assert.False(clip.hasMotionFloatCurves);
        Assert.Equal(7, clip.bindings.Count(binding => binding.type == typeof(Animator)));
        Assert.Contains(clip.bindings, binding => binding.propertyName == "RootT.x");
        Assert.Contains(clip.bindings, binding => binding.propertyName == "RootQ.w");
    }

    [Fact]
    public void QuarterCycleMatchesUnityHumanoidScaleAndWorldRootProbe()
    {
        using Runtime runtime = CreateRuntime(CreateHumanoidClip());

        runtime.Animator.Update(0.25f);

        AssertVector(runtime.Root.transform.position, 10.5656643f, 20.385025f, 29.7958374f, 4e-4f);
        AssertVector(runtime.Animator.deltaPosition, 0.5656642f, 0.38502425f, -0.204161942f, 4e-4f);
        AssertQuaternion(runtime.Root.transform.rotation, 0f, 0.556362152f, 0f, 0.830939949f, 4e-4f);
        AssertQuaternion(runtime.Animator.deltaRotation, 0f, 0.196024626f, 0f, 0.980599f, 4e-4f);
        Assert.False(runtime.Animator.hasRootMotion);
    }

    [Fact]
    public void TiltedRootQuaternionIsProjectedToWorldYaw()
    {
        using Runtime runtime = CreateRuntime(CreateHumanoidClip());

        runtime.Animator.Update(0.25f);

        Assert.Equal(0f, runtime.Root.transform.rotation.x, 5);
        Assert.Equal(0f, runtime.Root.transform.rotation.z, 5);
        Assert.Equal(0f, runtime.Animator.deltaRotation.x, 5);
        Assert.Equal(0f, runtime.Animator.deltaRotation.z, 5);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void RootLocksComposeIndependentlyLikeUnity(
        bool lockRotation,
        bool lockHeightY,
        bool lockPositionXZ)
    {
        using Runtime runtime = CreateRuntime(CreateHumanoidClip(
            lockRotation: lockRotation,
            lockHeightY: lockHeightY,
            lockPositionXZ: lockPositionXZ));

        runtime.Animator.Update(0.25f);

        AssertVector(
            runtime.Root.transform.position,
            lockPositionXZ ? 10f : 10.5656643f,
            lockHeightY ? 20f : 20.385025f,
            lockPositionXZ ? 30f : 29.7958374f,
            4e-4f);
        if (lockRotation)
            AssertQuaternion(runtime.Root.transform.rotation, 0f, 0.382683456f, 0f, 0.9238795f, 4e-4f);
        else
            AssertQuaternion(runtime.Root.transform.rotation, 0f, 0.556362152f, 0f, 0.830939949f, 4e-4f);
    }

    [Fact]
    public void KeepOriginalFlagsDoNotChangeStandardAlignedProbeMotion()
    {
        using Runtime runtime = CreateRuntime(CreateHumanoidClip(keepOriginal: true));

        runtime.Animator.Update(0.25f);

        AssertVector(runtime.Root.transform.position, 10.5656643f, 20.385025f, 29.7958374f, 4e-4f);
        AssertQuaternion(runtime.Root.transform.rotation, 0f, 0.556362152f, 0f, 0.830939949f, 4e-4f);
    }

    [Fact]
    public void HumanScaleMultipliesRootTranslationWithoutChangingYaw()
    {
        using Runtime scaled = CreateRuntime(CreateHumanoidClip(), HumanScale);
        using Runtime unit = CreateRuntime(CreateHumanoidClip(), 1f);

        scaled.Animator.Update(0.25f);
        unit.Animator.Update(0.25f);

        float ratio = MathF.Sqrt(scaled.Animator.deltaPosition.sqrMagnitude) /
                      MathF.Sqrt(unit.Animator.deltaPosition.sqrMagnitude);
        Assert.Equal(HumanScale, ratio, 5);
        AssertQuaternion(
            scaled.Animator.deltaRotation,
            unit.Animator.deltaRotation.x,
            unit.Animator.deltaRotation.y,
            unit.Animator.deltaRotation.z,
            unit.Animator.deltaRotation.w,
            1e-5f);
    }

    [Fact]
    public void AllLocksRemainStationaryAcrossLoopBoundary()
    {
        using Runtime runtime = CreateRuntime(CreateHumanoidClip(
            loop: true,
            lockRotation: true,
            lockHeightY: true,
            lockPositionXZ: true));

        for (int index = 0; index < 5; index++) runtime.Animator.Update(0.25f);

        AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
        AssertQuaternion(runtime.Root.transform.rotation, 0f, 0.382683456f, 0f, 0.9238795f);
        AssertVector(runtime.Animator.deltaPosition, 0f, 0f, 0f);
        AssertQuaternion(runtime.Animator.deltaRotation, 0f, 0f, 0f, 1f);
    }

    [Fact]
    public void DisabledApplicationSuppressesHumanoidWorldMotionAndDelta()
    {
        using Runtime runtime = CreateRuntime(CreateHumanoidClip(), applyRootMotion: false);

        runtime.Animator.Update(0.25f);

        AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
        AssertVector(runtime.Animator.deltaPosition, 0f, 0f, 0f);
        AssertQuaternion(runtime.Animator.deltaRotation, 0f, 0f, 0f, 1f);
    }

    [Fact]
    public void GenericMotionIgnoresHumanoidLocksAndScale()
    {
        AnimationClip baseline = CreateGenericClip(false);
        AnimationClip locked = CreateGenericClip(true);

        Assert.True(baseline.TryEvaluateRootMotion(0.25f, 1f, out AnimationRootMotionPose expected));
        Assert.True(locked.TryEvaluateRootMotion(0.25f, HumanScale * 3f, out AnimationRootMotionPose actual));

        AssertVector(actual.Position, expected.Position.x, expected.Position.y, expected.Position.z);
        AssertQuaternion(actual.Rotation,
            expected.Rotation.x, expected.Rotation.y, expected.Rotation.z, expected.Rotation.w);
    }

    private static AnimationClip CreateHumanoidClip(
        bool loop = false,
        bool lockRotation = false,
        bool lockHeightY = false,
        bool lockPositionXZ = false,
        bool keepOriginal = false)
    {
        AnimationClip clip = CreateProbeClip("Root");
        clip.name = "HumanoidRootMotion";
        clip.SetImportedMotionMetadata(
            human: true,
            hasGenericRoot: false,
            hasMotion: false,
            hasMotionFloat: false,
            hasRoot: true);
        AnimationUtility.SetAnimationClipSettings(clip, new AnimationClipSettings
        {
            loopTime = loop,
            loopBlendOrientation = lockRotation,
            loopBlendPositionY = lockHeightY,
            loopBlendPositionXZ = lockPositionXZ,
            keepOriginalOrientation = keepOriginal,
            keepOriginalPositionY = keepOriginal,
            keepOriginalPositionXZ = keepOriginal,
        });
        return clip;
    }

    private static AnimationClip CreateGenericClip(bool locked)
    {
        AnimationClip clip = CreateProbeClip("Motion");
        clip.SetImportedMotionMetadata(
            human: false,
            hasGenericRoot: false,
            hasMotion: true,
            hasMotionFloat: true,
            hasRoot: false);
        AnimationUtility.SetAnimationClipSettings(clip, new AnimationClipSettings
        {
            loopBlendOrientation = locked,
            loopBlendPositionY = locked,
            loopBlendPositionXZ = locked,
        });
        return clip;
    }

    private static AnimationClip CreateProbeClip(string prefix)
    {
        var clip = new AnimationClip();
        float[] times = { 0f, 0.25f, 0.5f, 0.75f, 1f };
        AddCurve(clip, prefix + "T.x", times, 0.002430134f, 0.487472057f, 0.955638349f, 1.4183985f, 1.86833882f);
        AddCurve(clip, prefix + "T.y", times, 1f, 1.34307551f, 1.56086588f, 1.7765795f, 1.99630582f);
        AddCurve(clip, prefix + "T.z", times, 0.00777642941f, 0.235547051f, 0.448032856f, 0.6438926f, 0.828186035f);
        AddCurve(clip, prefix + "Q.x", times, 0f, 0.0385192521f, 0.06381585f, 0.07227103f, 0.0616284162f);
        AddCurve(clip, prefix + "Q.y", times, 0f, 0.195791543f, 0.384376675f, 0.556736052f, 0.704416037f);
        AddCurve(clip, prefix + "Q.z", times, 0f, -0.0298830532f, -0.0734670162f, -0.1262767f, -0.183012709f);
        AddCurve(clip, prefix + "Q.w", times, 1f, 0.979433f, 0.9180331f, 0.8178485f, 0.6830127f);
        return clip;
    }

    private static void AddCurve(AnimationClip clip, string propertyName, float[] times, params float[] values)
    {
        var keys = new Keyframe[times.Length];
        for (int index = 0; index < keys.Length; index++) keys[index] = new Keyframe(times[index], values[index]);
        clip.SetCurve(string.Empty, typeof(Animator), propertyName, new AnimationCurve(keys));
    }

    private static Runtime CreateRuntime(
        AnimationClip clip,
        float humanScale = HumanScale,
        bool applyRootMotion = true)
    {
        var root = new GameObject("HumanoidRoot");
        root.transform.position = new Vector3(10f, 20f, 30f);
        root.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        var controller = new AnimatorController();
        AnimatorState state = controller.layers[0].stateMachine.AddState(clip.name);
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        Animator animator = root.AddComponent<Animator>();
        animator.avatar = Avatar.Create(true, true, default);
        animator.SetHumanScale(humanScale);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = applyRootMotion;
        animator.Rebind();
        animator.Play(state.nameHash, 0, 0f);
        animator.Update(0f);
        return new Runtime(root, animator);
    }

    private static void AssertVector(
        Vector3 actual,
        float x,
        float y,
        float z,
        float tolerance = 1e-5f)
    {
        Assert.True(MathF.Abs(actual.x - x) <= tolerance,
            $"x actual={actual.x:R}, expected={x:R}, vector={actual}");
        Assert.True(MathF.Abs(actual.y - y) <= tolerance,
            $"y actual={actual.y:R}, expected={y:R}, vector={actual}");
        Assert.True(MathF.Abs(actual.z - z) <= tolerance,
            $"z actual={actual.z:R}, expected={z:R}, vector={actual}");
    }

    private static void AssertQuaternion(
        Quaternion actual,
        float x,
        float y,
        float z,
        float w,
        float tolerance = 1e-5f)
    {
        Quaternion expected = new(x, y, z, w);
        if (Quaternion.Dot(actual, expected) < 0f)
            actual = new Quaternion(-actual.x, -actual.y, -actual.z, -actual.w);
        Assert.True(MathF.Abs(actual.x - expected.x) <= tolerance,
            $"x actual={actual.x:R}, expected={expected.x:R}, quaternion={actual}");
        Assert.True(MathF.Abs(actual.y - expected.y) <= tolerance,
            $"y actual={actual.y:R}, expected={expected.y:R}, quaternion={actual}");
        Assert.True(MathF.Abs(actual.z - expected.z) <= tolerance,
            $"z actual={actual.z:R}, expected={expected.z:R}, quaternion={actual}");
        Assert.True(MathF.Abs(actual.w - expected.w) <= tolerance,
            $"w actual={actual.w:R}, expected={expected.w:R}, quaternion={actual}");
    }

    private sealed class Runtime : IDisposable
    {
        internal Runtime(GameObject root, Animator animator)
        {
            Root = root;
            Animator = animator;
        }

        internal GameObject Root { get; }
        internal Animator Animator { get; }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
    }
}
