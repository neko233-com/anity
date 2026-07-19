using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class ImportedAnimationLoopRuntimeTests : IDisposable
{
    private readonly string _project = Path.Combine(
        Path.GetTempPath(), "anity-imported-loop-runtime-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public ImportedAnimationLoopRuntimeTests()
    {
        Directory.CreateDirectory(Path.Combine(_project, "Assets", "Models"));
        EditorApplication.OpenProject(_project);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_project, true); } catch { }
    }

    [Fact]
    public void ImportedDefaultClipIsNotMecanimLooping()
    {
        var imported = Import();
        var settings = AnimationUtility.GetAnimationClipSettings(imported.Clip);
        Assert.False(imported.Clip.isLooping);
        Assert.Equal(WrapMode.Default, imported.Clip.wrapMode);
        Assert.False(settings.loopTime);
        Assert.Equal(0f, settings.startTime);
        Assert.Equal(imported.Clip.length, settings.stopTime, 6);
    }

    [Fact]
    public void LoopTimeSetsMecanimLoopWithoutChangingWrapMode()
    {
        var imported = Import(setting => setting.loopTime = true);
        Assert.True(imported.Clip.isLooping);
        Assert.Equal(WrapMode.Default, imported.Clip.wrapMode);
        Assert.True(AnimationUtility.GetAnimationClipSettings(imported.Clip).loopTime);
    }

    [Fact]
    public void ExplicitWrapLoopDoesNotMakeMecanimStateLoop()
    {
        var imported = Import(setting => setting.wrapMode = WrapMode.Loop);
        Assert.False(imported.Clip.isLooping);
        Assert.Equal(WrapMode.Loop, imported.Clip.wrapMode);
        Assert.False(AnimationUtility.GetAnimationClipSettings(imported.Clip).loopTime);
    }

    [Fact]
    public void DirectSamplingUsesWrapModeInsteadOfLoopTime()
    {
        var loopTime = Import(setting => setting.loopTime = true);
        loopTime.Clip.SampleAnimation(loopTime.Root, loopTime.Clip.length * 1.25f);
        AssertRendererState(loopTime.Root, instanceA: false, instanceB: true);

        var wrapLoop = Import(setting => setting.wrapMode = WrapMode.Loop);
        wrapLoop.Clip.SampleAnimation(wrapLoop.Root, wrapLoop.Clip.length * 1.25f);
        AssertRendererState(wrapLoop.Root, instanceA: true, instanceB: true);
    }

    [Fact]
    public void AnimatorSamplingUsesLoopTimeInsteadOfWrapMode()
    {
        var loopTime = Import(setting => setting.loopTime = true);
        SampleAnimator(loopTime, 1.25f);
        AssertRendererState(loopTime.Root, instanceA: true, instanceB: true);

        var wrapLoop = Import(setting => setting.wrapMode = WrapMode.Loop);
        SampleAnimator(wrapLoop, 1.25f);
        AssertRendererState(wrapLoop.Root, instanceA: false, instanceB: true);
    }

    [Fact]
    public void ImportedCycleOffsetShiftsAnimatorSampling()
    {
        var imported = Import(setting =>
        {
            setting.loopTime = true;
            setting.cycleOffset = 0.25f;
        });
        var pose = imported.Clip.EvaluateFloatProperties(imported.Root, imported.Clip.length * 0.75f, true);
        var values = pose.Values.ToDictionary(value => value.Key.Path, value => value.Value);
        Assert.NotEqual(0f, values["InstanceA"]);
        Assert.NotEqual(0f, values["InstanceB"]);
        SampleAnimator(imported, 0.75f);
        AssertRendererState(imported.Root, instanceA: true, instanceB: true);
        Assert.Equal(0.25f, AnimationUtility.GetAnimationClipSettings(imported.Clip).cycleOffset, 6);
    }

    [Fact]
    public void ImportedLoopPoseDistributesEndpointDifferenceDuringAnimatorSampling()
    {
        var imported = Import(setting =>
        {
            setting.loopTime = true;
            setting.loopPose = true;
        });
        var pose = imported.Clip.EvaluateFloatProperties(imported.Root, imported.Clip.length * 0.75f, true);
        var values = pose.Values.ToDictionary(value => value.Key.Path, value => value.Value);
        Assert.NotEqual(0f, values["InstanceA"]);
        Assert.NotEqual(0f, values["InstanceB"]);
        SampleAnimator(imported, 0.75f);
        AssertRendererState(imported.Root, instanceA: true, instanceB: true);
        Assert.True(AnimationUtility.GetAnimationClipSettings(imported.Clip).loopBlend);
    }

    [Fact]
    public void CustomMiddleSlicePreservesLoopSettingsAndShiftedDuration()
    {
        var imported = Import(setting =>
        {
            setting.firstFrame = 5.75f;
            setting.lastFrame = 17.25f;
            setting.loopTime = true;
        });
        Assert.Equal(11.5f / 24f, imported.Clip.length, 6);
        Assert.True(imported.Clip.isLooping);
        foreach (var binding in AnimationUtility.GetCurveBindings(imported.Clip))
        {
            var curve = Assert.IsType<AnimationCurve>(AnimationUtility.GetEditorCurve(imported.Clip, binding));
            Assert.Equal(0f, curve.keys[0].time, 6);
            Assert.Equal(imported.Clip.length, curve.keys[^1].time, 6);
        }
    }

    [Fact]
    public void LoopingStateKeepsUnboundedNormalizedTimeWhileSamplingWrappedPose()
    {
        var imported = Import(setting => setting.loopTime = true);
        var animator = AnimatorFor(imported.Root, imported.Clip, out var state);
        animator.Play(state.nameHash, 0, 2.25f);
        animator.Update(0f);
        Assert.Equal(2.25f, animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 5);
        Assert.True(animator.GetCurrentAnimatorStateInfo(0).loop);
        AssertRendererState(imported.Root, instanceA: true, instanceB: true);
    }

    [Fact]
    public void MotionAndAnimationClipReadOnlySurfaceMatchesUnity2022()
    {
        AssertReadOnly(typeof(Motion), "averageDuration", "averageAngularSpeed", "averageSpeed",
            "apparentSpeed", "isLooping", "legacy", "isHumanMotion", "isAnimatorMotion");
        AssertReadOnly(typeof(AnimationClip), "length", "humanMotion", "hasMotionCurves",
            "hasMotionFloatCurves", "hasRootCurves", "hasGenericRootTransform");
        AssertWritable(typeof(AnimationClip), "frameRate", "wrapMode", "legacy");
        Assert.Null(typeof(Motion).GetProperty("duration", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(typeof(Motion).GetProperty("humanCycle", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(typeof(Motion).GetProperty("humanTranslation", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(typeof(Motion).GetMethod("ComputeHashCode", BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void ProceduralClipMotionFlagsMatchUnityDefaults()
    {
        var clip = new AnimationClip();
        Assert.False(clip.isLooping);
        Assert.False(clip.isHumanMotion);
        Assert.False(clip.humanMotion);
        Assert.False(clip.hasMotionCurves);
        Assert.False(clip.hasMotionFloatCurves);
        Assert.False(clip.hasRootCurves);
        Assert.False(clip.hasGenericRootTransform);
    }

    [Fact]
    public void SetAnimationClipSettingsUpdatesMecanimLoopState()
    {
        var clip = LinearPositionClip("Clip", 1f);
        AnimationUtility.SetAnimationClipSettings(clip, new AnimationClipSettings
        {
            loopTime = true,
            loopBlend = true,
            cycleOffset = 0.25f,
            stopTime = clip.length,
        });
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        Assert.True(clip.isLooping);
        Assert.True(settings.loopTime);
        Assert.True(settings.loopBlend);
        Assert.Equal(0.25f, settings.cycleOffset, 6);
    }

    [Fact]
    public void AnimatorUpdateZeroEvaluatesCurrentPose()
    {
        var root = new GameObject("Root");
        var clip = ConstantPositionClip("Pose", 42f, 2f);
        var animator = AnimatorFor(root, clip, out _);
        animator.Update(0f);
        Assert.Equal(42f, root.transform.localPosition.x, 5);
    }

    [Fact]
    public void PlayUsesNormalizedTimeScaledByClipLength()
    {
        var root = new GameObject("Root");
        var clip = LinearPositionClip("Move", 2f);
        var animator = AnimatorFor(root, clip, out var state);
        animator.Play(state.nameHash, 0, 0.5f);
        animator.Update(0f);
        Assert.Equal(50f, root.transform.localPosition.x, 5);
        Assert.Equal(0.5f, animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 5);
    }

    [Fact]
    public void PlayInFixedTimeUsesSecondsInsteadOfNormalizedTime()
    {
        var root = new GameObject("Root");
        var clip = LinearPositionClip("Move", 2f);
        var animator = AnimatorFor(root, clip, out var state);
        animator.PlayInFixedTime(state.nameHash, 0, 0.5f);
        animator.Update(0f);
        Assert.Equal(25f, root.transform.localPosition.x, 5);
        Assert.Equal(0.25f, animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 5);
    }

    [Fact]
    public void AnimatorStateCycleOffsetShiftsClipTime()
    {
        var root = new GameObject("Root");
        var clip = LinearPositionClip("Move", 2f);
        var animator = AnimatorFor(root, clip, out var state);
        state.cycleOffset = 0.25f;
        animator.Rebind();
        animator.Update(0f);
        Assert.Equal(25f, root.transform.localPosition.x, 5);
    }

    [Theory]
    [InlineData(0.5f, 0.25f, 25f, 0.25f)]
    [InlineData(1f, 0.25f, 12.5f, 0.125f)]
    public void NormalizedCrossFadeUsesCurrentStateLength(float duration, float delta,
        float expectedPosition, float expectedProgress)
    {
        var sample = CrossFade(fixedTime: false, duration, delta);
        Assert.Equal(expectedPosition, sample.Position, 5);
        Assert.Equal(expectedProgress, sample.Progress, 5);
        Assert.Equal(duration, sample.ReportedDuration, 5);
    }

    [Theory]
    [InlineData(1f, 0.25f, 25f, 0.25f)]
    [InlineData(0.5f, 0.25f, 50f, 0.5f)]
    public void FixedCrossFadeUsesSeconds(float duration, float delta,
        float expectedPosition, float expectedProgress)
    {
        var sample = CrossFade(fixedTime: true, duration, delta);
        Assert.Equal(expectedPosition, sample.Position, 5);
        Assert.Equal(expectedProgress, sample.Progress, 5);
        Assert.Equal(duration, sample.ReportedDuration, 5);
    }

    [Theory]
    [InlineData(false, 0.5f)]
    [InlineData(true, 1f)]
    public void CrossFadeHonorsNormalizedTransitionStartProgress(bool fixedTime, float duration)
    {
        var root = new GameObject("Root");
        var animator = CrossFadeAnimator(root, out var second);
        if (fixedTime) animator.CrossFadeInFixedTime(second.nameHash, duration, 0, 0f, 0.5f);
        else animator.CrossFade(second.nameHash, duration, 0, 0f, 0.5f);
        animator.Update(0f);
        Assert.Equal(50f, root.transform.localPosition.x, 5);
        Assert.Equal(0.5f, animator.GetAnimatorTransitionInfo(0).normalizedTime, 5);
    }

    [Fact]
    public void CrossFadeReportsCurrentAndNextClipWeights()
    {
        var root = new GameObject("Root");
        var animator = CrossFadeAnimator(root, out var second);
        animator.CrossFade(second.nameHash, 0.5f, 0);
        animator.Update(0.25f);
        Assert.Equal(0.75f, Assert.Single(animator.GetCurrentAnimatorClipInfo(0)).weight, 5);
        Assert.Equal(0.25f, Assert.Single(animator.GetNextAnimatorClipInfo(0)).weight, 5);
    }

    [Fact]
    public void CrossFadeDestinationOffsetIsNormalizedForNormalizedApi()
    {
        var root = new GameObject("Root");
        var animator = CrossFadeAnimator(root, out var second, linearSecond: true);
        animator.CrossFade(second.nameHash, 0.5f, 0, 0.5f, 0f);
        Assert.Equal(0.5f, animator.GetNextAnimatorStateInfo(0).normalizedTime, 5);
    }

    [Fact]
    public void FixedCrossFadeDestinationOffsetIsMeasuredInSeconds()
    {
        var root = new GameObject("Root");
        var animator = CrossFadeAnimator(root, out var second, linearSecond: true);
        animator.CrossFadeInFixedTime(second.nameHash, 1f, 0, 0.5f, 0f);
        Assert.Equal(0.125f, animator.GetNextAnimatorStateInfo(0).normalizedTime, 5);
    }

    private Imported Import(Action<ModelImporterClipAnimation>? configure = null)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Models", "InstancedVisibility.fbx");
        string path = "Assets/Models/InstancedVisibility-" + Guid.NewGuid().ToString("N") + ".fbx";
        File.Copy(source, Path.Combine(_project, path));
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        var take = Assert.Single(importer.defaultClipAnimations);
        if (configure is not null)
        {
            var setting = new ModelImporterClipAnimation
            {
                name = "Configured",
                takeName = take.takeName,
                firstFrame = take.firstFrame,
                lastFrame = take.lastFrame,
            };
            configure(setting);
            importer.clipAnimations = new[] { setting };
            importer.SaveAndReimport();
        }
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path)!;
        var clip = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>());
        return new Imported(root, clip);
    }

    private static Animator SampleAnimator(Imported imported, float normalizedTime)
    {
        var animator = AnimatorFor(imported.Root, imported.Clip, out var state);
        animator.Play(state.nameHash, 0, normalizedTime);
        animator.Update(0f);
        return animator;
    }

    private static Animator AnimatorFor(GameObject root, AnimationClip clip, out AnimatorState state)
    {
        var controller = new AnimatorController();
        state = controller.layers[0].stateMachine.AddState(clip.name);
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.Rebind();
        return animator;
    }

    private static Animator CrossFadeAnimator(GameObject root, out AnimatorState second, bool linearSecond = false)
    {
        var firstClip = ConstantPositionClip("First", 0f, 2f);
        var secondClip = linearSecond ? LinearPositionClip("Second", 4f) : ConstantPositionClip("Second", 100f, 4f);
        var controller = new AnimatorController();
        var first = controller.layers[0].stateMachine.AddState("First");
        first.motion = firstClip;
        second = controller.layers[0].stateMachine.AddState("Second");
        second.motion = secondClip;
        controller.layers[0].stateMachine.defaultState = first;
        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.Rebind();
        animator.Update(0f);
        return animator;
    }

    private static CrossFadeSample CrossFade(bool fixedTime, float duration, float delta)
    {
        var root = new GameObject("Root");
        var animator = CrossFadeAnimator(root, out var second);
        if (fixedTime) animator.CrossFadeInFixedTime(second.nameHash, duration, 0, 0f, 0f);
        else animator.CrossFade(second.nameHash, duration, 0, 0f, 0f);
        animator.Update(delta);
        var transition = animator.GetAnimatorTransitionInfo(0);
        return new CrossFadeSample(root.transform.localPosition.x, transition.normalizedTime, transition.duration);
    }

    private static AnimationClip ConstantPositionClip(string name, float value, float length)
    {
        var clip = new AnimationClip { name = name };
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x",
            AnimationCurve.Constant(0f, length, value));
        return clip;
    }

    private static AnimationClip LinearPositionClip(string name, float length)
    {
        var clip = new AnimationClip { name = name };
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x",
            AnimationCurve.Linear(0f, 0f, length, 100f));
        return clip;
    }

    private static void AssertRendererState(GameObject root, bool instanceA, bool instanceB)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        Assert.Equal(instanceA, Assert.Single(renderers.Where(value => value.gameObject.name == "InstanceA")).enabled);
        Assert.Equal(instanceB, Assert.Single(renderers.Where(value => value.gameObject.name == "InstanceB")).enabled);
    }

    private static void AssertReadOnly(Type type, params string[] names)
    {
        foreach (string name in names)
        {
            PropertyInfo? property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property);
            Assert.NotNull(property.GetMethod);
            Assert.Null(property.SetMethod);
        }
    }

    private static void AssertWritable(Type type, params string[] names)
    {
        foreach (string name in names)
        {
            PropertyInfo? property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property);
            Assert.NotNull(property.GetMethod);
            Assert.NotNull(property.SetMethod);
        }
    }

    private readonly record struct Imported(GameObject Root, AnimationClip Clip);
    private readonly record struct CrossFadeSample(float Position, float Progress, float ReportedDuration);
}
