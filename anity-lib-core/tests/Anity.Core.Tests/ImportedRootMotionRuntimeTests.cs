using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class ImportedRootMotionRuntimeTests : IDisposable
{
    private readonly string _project = Path.Combine(
        Path.GetTempPath(), "anity-imported-root-motion-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public ImportedRootMotionRuntimeTests()
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
    public void MotionNodeImportCreatesUnityAnimatorBindingsAndMetadata()
    {
        Imported imported = Import();
        AnimationCurveBinding[] motion = imported.Clip.bindings
            .Where(binding => binding.type == typeof(Animator)).ToArray();
        Assert.Equal(new[]
        {
            "MotionQ.w", "MotionQ.x", "MotionQ.y", "MotionQ.z",
            "MotionT.x", "MotionT.y", "MotionT.z",
        }, motion.Select(binding => binding.propertyName).OrderBy(value => value).ToArray());
        Assert.All(motion, binding => Assert.Equal(string.Empty, binding.path));
        Assert.True(imported.Clip.hasMotionCurves);
        Assert.True(imported.Clip.hasMotionFloatCurves);
        Assert.False(imported.Clip.hasGenericRootTransform);
        Assert.False(imported.Clip.hasRootCurves);
        Assert.False(imported.Clip.isHumanMotion);
        Assert.Equal(23f / 24f, imported.Clip.length, 5);
        Assert.Equal(-0.4791667f,
            imported.Clip.GetCurve(string.Empty, typeof(Animator), "MotionT.x")
                .Evaluate(imported.Clip.length / 4f), 5);
        Assert.Equal(0.23958334f,
            imported.Clip.GetCurve(string.Empty, typeof(Animator), "MotionT.y")
                .Evaluate(imported.Clip.length / 4f), 5);
    }

    [Fact]
    public void MissingMotionNodeDoesNotInventAnimatorMotionCurves()
    {
        Imported imported = Import(motionNodeName: "Missing");
        Assert.DoesNotContain(imported.Clip.bindings, binding => binding.type == typeof(Animator));
        Assert.False(imported.Clip.hasMotionCurves);
        Assert.False(imported.Clip.hasMotionFloatCurves);
    }

    [Fact]
    public void AnimatorHasRootMotionIsReadOnlyAndGenericProbeReportsFalse()
    {
        PropertyInfo property = typeof(Animator).GetProperty(nameof(Animator.hasRootMotion),
            BindingFlags.Instance | BindingFlags.Public)!;
        Assert.NotNull(property.GetMethod);
        Assert.Null(property.SetMethod);
        Runtime runtime = CreateRuntime(Import(), applyRootMotion: true);
        Assert.False(runtime.Animator.hasRootMotion);
    }

    [Fact]
    public void ApplyRootMotionFalseKeepsWorldRootAndSuppressesDelta()
    {
        Runtime runtime = CreateRuntime(Import(), applyRootMotion: false);
        runtime.Animator.Update(runtime.Clip.length / 4f);
        AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
        AssertVector(runtime.Animator.rootPosition, 10f, 20f, 30f);
        AssertVector(runtime.Animator.deltaPosition, 0f, 0f, 0f);
        AssertQuaternion(runtime.Animator.deltaRotation, 0f, 0f, 0f, 1f);
        AssertVector(runtime.Animator.velocity, 0f, 0f, 0f);
        AssertVector(runtime.Animator.angularVelocity, 0f, 0f, 0f);
        AssertVector(runtime.Child.localPosition, -0.4791667f, 0.23958334f, 0.11979167f);
    }

    [Fact]
    public void QuarterCycleRootPositionMatchesUnityWorldAnchor()
    {
        Runtime runtime = CreateRuntime(Import(), applyRootMotion: true);
        runtime.Animator.Update(runtime.Clip.length / 4f);
        AssertVector(runtime.Root.transform.position, 9.745884f, 20.239584f, 30.423527f, 2e-4f);
        AssertVector(runtime.Animator.deltaPosition, -0.2541165f, 0.23958334f, 0.4235275f, 2e-4f);
        AssertVector(runtime.Child.localPosition, -0.4791667f, 0.23958334f, 0.11979167f, 2e-4f);
    }

    [Fact]
    public void QuarterCycleRotationAndVelocitiesMatchUnityProbe()
    {
        Runtime runtime = CreateRuntime(Import(), applyRootMotion: true);
        runtime.Animator.Update(runtime.Clip.length / 4f);
        AssertQuaternion(runtime.Root.transform.rotation, 0f, 0.20310721f, 0f, 0.97915655f, 3e-4f);
        AssertQuaternion(runtime.Animator.deltaRotation, 0f, -0.18706042f, 0f, 0.98234844f, 3e-4f);
        AssertVector(runtime.Animator.velocity, -1.06066f, 1f, 1.7677671f, 3e-4f);
        AssertVector(runtime.Animator.angularVelocity, 0f, -1.5708022f, 0f, 4e-4f);
    }

    [Fact]
    public void RootAccessorsExposeAbsoluteWorldPoseInsteadOfFrameDelta()
    {
        Runtime runtime = CreateRuntime(Import(), applyRootMotion: true);
        runtime.Animator.Update(runtime.Clip.length / 2f);
        AssertVector(runtime.Animator.rootPosition, 9.491768f, 20.479168f, 30.847054f, 3e-4f);
        AssertQuaternion(runtime.Animator.rootRotation, 0f, 0.01636173f, 0f, 0.9998662f, 3e-4f);
        AssertVector(runtime.Animator.deltaPosition, -0.508232f, 0.4791667f, 0.847054f, 4e-4f);
    }

    [Fact]
    public void NonLoopingMotionClampsAndClearsDeltaPastClipEnd()
    {
        Runtime runtime = CreateRuntime(Import(), applyRootMotion: true);
        for (int index = 0; index < 4; index++) runtime.Animator.Update(runtime.Clip.length / 4f);
        AssertVector(runtime.Root.transform.position, 8.983536f, 20.958336f, 31.694107f, 4e-4f);
        runtime.Animator.Update(runtime.Clip.length / 4f);
        AssertVector(runtime.Root.transform.position, 8.983536f, 20.958336f, 31.694107f, 4e-4f);
        AssertVector(runtime.Animator.deltaPosition, 0f, 0f, 0f, 1e-5f);
        AssertQuaternion(runtime.Animator.deltaRotation, 0f, 0f, 0f, 1f, 1e-5f);
        AssertVector(runtime.Animator.velocity, 0f, 0f, 0f, 1e-5f);
    }

    [Fact]
    public void LoopEndpointWrapsChildPoseButPreservesContinuousRootMotion()
    {
        Runtime runtime = CreateRuntime(Import(loop: true), applyRootMotion: true);
        for (int index = 0; index < 4; index++) runtime.Animator.Update(runtime.Clip.length / 4f);
        AssertVector(runtime.Child.localPosition, 0f, 0f, 0f, 2e-4f);
        AssertQuaternion(runtime.Child.localRotation, 0f, 0f, 0f, 1f, 3e-4f);
        AssertVector(runtime.Root.transform.position, 8.983536f, 20.958336f, 31.694107f, 4e-4f);
        AssertVector(runtime.Animator.deltaPosition, -0.2541165f, 0.23958337f, 0.4235275f, 4e-4f);
    }

    [Fact]
    public void LoopSecondCycleComposesTranslationInAccumulatedRotation()
    {
        Runtime runtime = CreateRuntime(Import(loop: true), applyRootMotion: true);
        for (int index = 0; index < 5; index++) runtime.Animator.Update(runtime.Clip.length / 4f);
        AssertVector(runtime.Root.transform.position, 8.544294f, 21.19792f, 31.468235f, 6e-4f);
        AssertQuaternion(runtime.Root.transform.rotation, 0f, -0.5211033f, 0f, 0.85349363f, 5e-4f);
        AssertVector(runtime.Animator.deltaPosition, -0.43924096f, 0.23958346f, -0.22587252f, 6e-4f);
        AssertVector(runtime.Animator.velocity, -1.8333534f, 1.0000005f, -0.94277215f, 8e-4f);
    }

    [Fact]
    public void UpdateZeroCapturesAnchorWithoutProducingMotion()
    {
        Runtime runtime = CreateRuntime(Import(), applyRootMotion: true, prime: false);
        runtime.Animator.Update(0f);
        AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
        AssertVector(runtime.Animator.rootPosition, 10f, 20f, 30f);
        AssertVector(runtime.Animator.deltaPosition, 0f, 0f, 0f);
        AssertQuaternion(runtime.Animator.deltaRotation, 0f, 0f, 0f, 1f);
    }

    [Fact]
    public void FirstPositiveUpdateAdvancesFromStateStartWithoutPrimingCall()
    {
        Runtime runtime = CreateRuntime(Import(), applyRootMotion: true, prime: false);
        runtime.Animator.Update(runtime.Clip.length / 4f);
        AssertVector(runtime.Root.transform.position, 9.745884f, 20.239584f, 30.423527f, 3e-4f);
        AssertVector(runtime.Animator.deltaPosition, -0.2541165f, 0.23958334f, 0.4235275f, 3e-4f);
    }

    [Fact]
    public void ApplyRootMotionToggleReanchorsWithoutRetroactiveJump()
    {
        Runtime runtime = CreateRuntime(Import(), applyRootMotion: false);
        runtime.Animator.Update(runtime.Clip.length / 4f);
        runtime.Animator.applyRootMotion = true;
        runtime.Animator.Update(0f);
        AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
        runtime.Animator.Update(runtime.Clip.length / 4f);
        Assert.True((runtime.Root.transform.position - new Vector3(10f, 20f, 30f)).sqrMagnitude > 0.1f);
        AssertVector(runtime.Animator.rootPosition,
            runtime.Root.transform.position.x,
            runtime.Root.transform.position.y,
            runtime.Root.transform.position.z);
    }

    [Fact]
    public void RebindUsesCurrentWorldPoseAsNewMotionAnchor()
    {
        Runtime runtime = CreateRuntime(Import(), applyRootMotion: true);
        runtime.Animator.Update(runtime.Clip.length / 4f);
        runtime.Root.transform.position = new Vector3(100f, 200f, 300f);
        runtime.Root.transform.rotation = Quaternion.identity;
        runtime.Animator.Rebind();
        runtime.Animator.Update(0f);
        runtime.Animator.Update(runtime.Clip.length / 4f);
        AssertVector(runtime.Root.transform.position, 99.520836f, 200.23958f, 300.11978f, 5e-4f);
    }

    [Fact]
    public void RootLockAndKeepOriginalFlagsRoundTripWithoutInventedGenericFiltering()
    {
        Imported imported = Import(configure: setting =>
        {
            setting.lockRootRotation = true;
            setting.lockRootHeightY = true;
            setting.lockRootPositionXZ = true;
            setting.keepOriginalOrientation = true;
            setting.keepOriginalPositionY = true;
            setting.keepOriginalPositionXZ = true;
        });
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(imported.Clip);
        Assert.True(settings.loopBlendOrientation);
        Assert.True(settings.loopBlendPositionY);
        Assert.True(settings.loopBlendPositionXZ);
        Assert.True(settings.keepOriginalOrientation);
        Assert.True(settings.keepOriginalPositionY);
        Assert.True(settings.keepOriginalPositionXZ);
        Runtime runtime = CreateRuntime(imported, applyRootMotion: true);
        runtime.Animator.Update(runtime.Clip.length / 4f);
        AssertVector(runtime.Root.transform.position, 9.745884f, 20.239584f, 30.423527f, 3e-4f);
    }

    [Fact]
    public void ManualBuiltinRootMotionDoesNothingWhenAutomaticMotionIsDisabled()
    {
        Runtime runtime = CreateRuntime(Import(), applyRootMotion: false);
        runtime.Animator.Update(runtime.Clip.length / 4f);
        runtime.Animator.ApplyBuiltinRootMotion();
        AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
        AssertVector(runtime.Animator.deltaPosition, 0f, 0f, 0f);
    }

    [Fact]
    public void PlayModeAnimatorMoveRunsOnZeroDeltaUpdate()
    {
        InPlayMode(() =>
        {
            AnimatorMoveRecorder receiver = null!;
            Runtime runtime = CreateRuntime(Import(), applyRootMotion: true, prime: false,
                configureRoot: root => receiver = root.AddComponent<AnimatorMoveRecorder>());
            runtime.Animator.Update(0f);
            Assert.Equal(1, receiver.Calls);
            AssertVector(receiver.DeltaSeen, 0f, 0f, 0f);
            AssertVector(receiver.RootSeen, 10f, 20f, 30f);
        });
    }

    [Fact]
    public void SameObjectAnimatorMoveSuppressesAutomaticMotionAndExposesDelta()
    {
        InPlayMode(() =>
        {
            AnimatorMoveRecorder receiver = null!;
            Runtime runtime = CreateRuntime(Import(), applyRootMotion: true,
                configureRoot: root => receiver = root.AddComponent<AnimatorMoveRecorder>());
            receiver.ResetObservations();
            runtime.Animator.Update(runtime.Clip.length / 4f);
            Assert.Equal(1, receiver.Calls);
            AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
            AssertVector(runtime.Animator.deltaPosition, -0.2541165f, 0.23958334f, 0.4235275f, 3e-4f);
            AssertVector(receiver.DeltaSeen, -0.2541165f, 0.23958334f, 0.4235275f, 3e-4f);
        });
    }

    [Fact]
    public void ApplyRootMotionFalseStillDispatchesAnimatorMoveAndExposesDelta()
    {
        InPlayMode(() =>
        {
            AnimatorMoveRecorder receiver = null!;
            Runtime runtime = CreateRuntime(Import(), applyRootMotion: false,
                configureRoot: root => receiver = root.AddComponent<AnimatorMoveRecorder>());
            receiver.ResetObservations();
            runtime.Animator.Update(runtime.Clip.length / 4f);
            Assert.Equal(1, receiver.Calls);
            AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
            AssertVector(runtime.Animator.deltaPosition, -0.2541165f, 0.23958334f, 0.4235275f, 3e-4f);
            AssertVector(runtime.Animator.velocity, -1.06066f, 1f, 1.7677671f, 4e-4f);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ApplyBuiltinRootMotionInsideCallbackAppliesPendingPose(bool applyRootMotion)
    {
        InPlayMode(() =>
        {
            AnimatorMoveRecorder receiver = null!;
            Runtime runtime = CreateRuntime(Import(), applyRootMotion,
                configureRoot: root =>
                {
                    receiver = root.AddComponent<AnimatorMoveRecorder>();
                    receiver.ApplyBuiltin = true;
                });
            receiver.ResetObservations();
            runtime.Animator.Update(runtime.Clip.length / 4f);
            Assert.Equal(1, receiver.Calls);
            AssertVector(runtime.Root.transform.position, 9.745884f, 20.239584f, 30.423527f, 3e-4f);
            AssertVector(runtime.Animator.rootPosition, 9.745884f, 20.239584f, 30.423527f, 3e-4f);
        });
    }

    [Fact]
    public void ApplyBuiltinRootMotionTwiceInsideCallbackIsIdempotent()
    {
        InPlayMode(() =>
        {
            AnimatorMoveRecorder receiver = null!;
            Runtime runtime = CreateRuntime(Import(), applyRootMotion: false,
                configureRoot: root =>
                {
                    receiver = root.AddComponent<AnimatorMoveRecorder>();
                    receiver.ApplyBuiltin = true;
                    receiver.ApplyBuiltinTwice = true;
                });
            receiver.ResetObservations();
            runtime.Animator.Update(runtime.Clip.length / 4f);
            AssertVector(receiver.AfterFirstBuiltin, 9.745884f, 20.239584f, 30.423527f, 3e-4f);
            AssertVector(receiver.AfterSecondBuiltin, 9.745884f, 20.239584f, 30.423527f, 3e-4f);
        });
    }

    [Fact]
    public void ManualDeltaApplicationMatchesBuiltinRootMotion()
    {
        InPlayMode(() =>
        {
            AnimatorMoveRecorder builtinReceiver = null!;
            Runtime builtin = CreateRuntime(Import(), applyRootMotion: false,
                configureRoot: root =>
                {
                    builtinReceiver = root.AddComponent<AnimatorMoveRecorder>();
                    builtinReceiver.ApplyBuiltin = true;
                });
            AnimatorMoveRecorder manualReceiver = null!;
            Runtime manual = CreateRuntime(Import(), applyRootMotion: false,
                configureRoot: root =>
                {
                    manualReceiver = root.AddComponent<AnimatorMoveRecorder>();
                    manualReceiver.ApplyManual = true;
                });
            builtin.Animator.Update(builtin.Clip.length / 4f);
            manual.Animator.Update(manual.Clip.length / 4f);
            AssertVector(manual.Root.transform.position,
                builtin.Root.transform.position.x,
                builtin.Root.transform.position.y,
                builtin.Root.transform.position.z, 3e-4f);
            AssertQuaternion(manual.Root.transform.rotation,
                builtin.Root.transform.rotation.x,
                builtin.Root.transform.rotation.y,
                builtin.Root.transform.rotation.z,
                builtin.Root.transform.rotation.w, 3e-4f);
        });
    }

    [Fact]
    public void DisabledSameObjectReceiverSuppressesAutomaticMotionWithoutCallback()
    {
        InPlayMode(() =>
        {
            AnimatorMoveRecorder receiver = null!;
            Runtime runtime = CreateRuntime(Import(), applyRootMotion: true,
                configureRoot: root =>
                {
                    receiver = root.AddComponent<AnimatorMoveRecorder>();
                    receiver.enabled = false;
                });
            runtime.Animator.Update(runtime.Clip.length / 4f);
            Assert.Equal(0, receiver.Calls);
            AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
            AssertVector(runtime.Animator.deltaPosition, -0.2541165f, 0.23958334f, 0.4235275f, 3e-4f);
        });
    }

    [Fact]
    public void ChildReceiverNeitherSuppressesRootMotionNorReceivesCallback()
    {
        InPlayMode(() =>
        {
            AnimatorMoveRecorder receiver = null!;
            Runtime runtime = CreateRuntime(Import(), applyRootMotion: true,
                configureRoot: root =>
                    receiver = root.transform.Find("InstanceA")!.gameObject.AddComponent<AnimatorMoveRecorder>());
            runtime.Animator.Update(runtime.Clip.length / 4f);
            Assert.Equal(0, receiver.Calls);
            AssertVector(runtime.Root.transform.position, 9.745884f, 20.239584f, 30.423527f, 3e-4f);
        });
    }

    [Fact]
    public void EditModeReceiverSuppressesAutomaticMotionWithoutCallback()
    {
        OutsidePlayMode(() =>
        {
            AnimatorMoveRecorder receiver = null!;
            Runtime runtime = CreateRuntime(Import(), applyRootMotion: true,
                configureRoot: root => receiver = root.AddComponent<AnimatorMoveRecorder>());
            runtime.Animator.Update(runtime.Clip.length / 4f);
            Assert.Equal(0, receiver.Calls);
            AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
            AssertVector(runtime.Animator.deltaPosition, -0.2541165f, 0.23958334f, 0.4235275f, 3e-4f);
        });
    }

    [Fact]
    public void ApplyBuiltinRootMotionOutsideCallbackDoesNothing()
    {
        InPlayMode(() =>
        {
            Runtime runtime = CreateRuntime(Import(), applyRootMotion: true,
                configureRoot: root => root.AddComponent<AnimatorMoveRecorder>());
            runtime.Animator.Update(runtime.Clip.length / 4f);
            runtime.Animator.ApplyBuiltinRootMotion();
            AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
            AssertVector(runtime.Animator.deltaPosition, -0.2541165f, 0.23958334f, 0.4235275f, 3e-4f);
        });
    }

    [Fact]
    public void CallbackSeesPendingRootPoseButAccessorRevertsToActualPoseAfterward()
    {
        InPlayMode(() =>
        {
            AnimatorMoveRecorder receiver = null!;
            Runtime runtime = CreateRuntime(Import(), applyRootMotion: true,
                configureRoot: root => receiver = root.AddComponent<AnimatorMoveRecorder>());
            receiver.ResetObservations();
            runtime.Animator.Update(runtime.Clip.length / 4f);
            AssertVector(receiver.RootSeen, 9.745884f, 20.239584f, 30.423527f, 3e-4f);
            AssertQuaternion(receiver.RootRotationSeen, 0f, 0.20310721f, 0f, 0.97915655f, 3e-4f);
            AssertVector(runtime.Animator.rootPosition, 10f, 20f, 30f);
            AssertQuaternion(runtime.Animator.rootRotation, 0f, 0.38268343f, 0f, 0.9238795f, 3e-4f);
        });
    }

    [Fact]
    public void UnappliedMotionRebasesNextDeltaFromActualTransform()
    {
        InPlayMode(() =>
        {
            AnimatorMoveRecorder receiver = null!;
            Runtime runtime = CreateRuntime(Import(), applyRootMotion: true,
                configureRoot: root => receiver = root.AddComponent<AnimatorMoveRecorder>());
            runtime.Animator.Update(runtime.Clip.length / 4f);
            receiver.ResetObservations();
            runtime.Animator.Update(runtime.Clip.length / 4f);
            AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
            AssertVector(runtime.Animator.deltaPosition, -0.080679f, 0.23958334f, 0.48727983f, 4e-4f);
            AssertVector(receiver.RootSeen, 9.919321f, 20.239584f, 30.48728f, 4e-4f);
        });
    }

    [Fact]
    public void InheritedPrivateAnimatorMoveIsDetectedAndInvoked()
    {
        InPlayMode(() =>
        {
            DerivedAnimatorMoveRecorder receiver = null!;
            Runtime runtime = CreateRuntime(Import(), applyRootMotion: true,
                configureRoot: root => receiver = root.AddComponent<DerivedAnimatorMoveRecorder>());
            receiver.Calls = 0;
            runtime.Animator.Update(runtime.Clip.length / 4f);
            Assert.Equal(1, receiver.Calls);
            AssertVector(runtime.Root.transform.position, 10f, 20f, 30f);
        });
    }

    private Imported Import(
        bool loop = false,
        string motionNodeName = "InstanceA",
        Action<ModelImporterClipAnimation>? configure = null)
    {
        string source = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "Models", "InstancedVisibility.fbx");
        string path = "Assets/Models/RootMotion-" + Guid.NewGuid().ToString("N") + ".fbx";
        string fullPath = Path.Combine(_project, path);
        File.WriteAllText(fullPath, AddRootMotionAnimation(File.ReadAllText(source)));
        AssetDatabase.ImportAsset(path);
        ModelImporter importer = ModelImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.motionNodeName = motionNodeName;
        importer.resampleCurves = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        ModelImporterClipAnimation take = Assert.Single(importer.defaultClipAnimations);
        var setting = new ModelImporterClipAnimation
        {
            name = "RootMotion",
            takeName = take.takeName,
            firstFrame = take.firstFrame,
            lastFrame = take.lastFrame,
            loopTime = loop,
        };
        configure?.Invoke(setting);
        importer.clipAnimations = new[] { setting };
        importer.SaveAndReimport();
        return new Imported(
            AssetDatabase.LoadAssetAtPath<GameObject>(path)!,
            Assert.Single(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()));
    }

    private static Runtime CreateRuntime(
        Imported imported,
        bool applyRootMotion,
        bool prime = true,
        Action<GameObject>? configureRoot = null)
    {
        GameObject root = imported.Root;
        root.transform.position = new Vector3(10f, 20f, 30f);
        root.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        configureRoot?.Invoke(root);
        var controller = new AnimatorController();
        AnimatorState state = controller.layers[0].stateMachine.AddState(imported.Clip.name);
        state.motion = imported.Clip;
        controller.layers[0].stateMachine.defaultState = state;
        Animator animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = applyRootMotion;
        animator.Rebind();
        animator.Play(state.nameHash, 0, 0f);
        if (prime) animator.Update(0f);
        Transform child = root.transform.Find("InstanceA")!;
        return new Runtime(root, child, imported.Clip, animator);
    }

    private static void InPlayMode(Action action) => WithPlayMode(true, action);

    private static void OutsidePlayMode(Action action) => WithPlayMode(false, action);

    private static void WithPlayMode(bool isPlaying, Action action)
    {
        bool previous = EditorApplication.isPlaying;
        try
        {
            EditorApplication.isPlaying = isPlaying;
            action();
        }
        finally
        {
            EditorApplication.isPlaying = previous;
        }
    }

    private static string AddRootMotionAnimation(string source)
    {
        source = ReplaceRequired(source, "\tCount: 14\n", "\tCount: 22\n");
        source = ReplaceRequired(source,
            "\tObjectType: \"AnimationCurveNode\" {\n\t\tCount: 3\n",
            "\tObjectType: \"AnimationCurveNode\" {\n\t\tCount: 5\n");
        source = ReplaceRequired(source,
            "\tObjectType: \"AnimationCurve\" {\n\t\tCount: 3\n",
            "\tObjectType: \"AnimationCurve\" {\n\t\tCount: 9\n");
        source = ReplaceRequired(source,
            "\tAnimationLayer: 49406012672, \"AnimLayer::Animation Base Layer\", \"\" {",
            RootMotionObjects + "\n\tAnimationLayer: 49406012672, \"AnimLayer::Animation Base Layer\", \"\" {");
        source = ReplaceRequired(source,
            "\t;AnimLayer::Animation Base Layer, AnimStack::InstancedRootVisibility",
            RootMotionModelConnections + "\n\t;AnimLayer::Animation Base Layer, AnimStack::InstancedRootVisibility");
        source = ReplaceRequired(source,
            "\t;AnimCurve::, AnimCurveNode::Visibility\n\tC: \"OP\",49407816992,49414345792, \"d|Visibility\"",
            RootMotionLayerConnections + "\n\t;AnimCurve::, AnimCurveNode::Visibility\n\tC: \"OP\",49407816992,49414345792, \"d|Visibility\"");
        source = ReplaceRequired(source,
            "\tC: \"OP\",49407815392,49414346176, \"d|Visibility\"\n}",
            "\tC: \"OP\",49407815392,49414346176, \"d|Visibility\"\n" +
            RootMotionCurveConnections + "\n}");
        return source;
    }

    private static string ReplaceRequired(string source, string original, string replacement)
    {
        string rewritten = source.Replace(original, replacement, StringComparison.Ordinal);
        if (ReferenceEquals(rewritten, source) || string.Equals(rewritten, source, StringComparison.Ordinal))
            throw new InvalidDataException("Root-motion FBX fixture marker was not found.");
        return rewritten;
    }

    private const string RootMotionObjects = """
	AnimationCurve: 49500000101, "AnimCurve::RootTX", "" {
		Default: 0
		KeyVer: 4009
		KeyTime: *3 {
			a: 0,23093079000,46186158000
		}
		KeyValueFloat: *3 {
			a: 0,100,200
		}
		KeyAttrFlags: *3 {
			a: 4,4,4
		}
		KeyAttrDataFloat: *12 {
			a: 0,0,0,0,0,0,0,0,0,0,0,0
		}
		KeyAttrRefCount: *3 {
			a: 1,1,1
		}
	}
	AnimationCurve: 49500000102, "AnimCurve::RootTY", "" {
		Default: 0
		KeyVer: 4009
		KeyTime: *3 {
			a: 0,23093079000,46186158000
		}
		KeyValueFloat: *3 {
			a: 0,50,100
		}
		KeyAttrFlags: *3 {
			a: 4,4,4
		}
		KeyAttrDataFloat: *12 {
			a: 0,0,0,0,0,0,0,0,0,0,0,0
		}
		KeyAttrRefCount: *3 {
			a: 1,1,1
		}
	}
	AnimationCurve: 49500000103, "AnimCurve::RootTZ", "" {
		Default: 0
		KeyVer: 4009
		KeyTime: *3 {
			a: 0,23093079000,46186158000
		}
		KeyValueFloat: *3 {
			a: 0,25,50
		}
		KeyAttrFlags: *3 {
			a: 4,4,4
		}
		KeyAttrDataFloat: *12 {
			a: 0,0,0,0,0,0,0,0,0,0,0,0
		}
		KeyAttrRefCount: *3 {
			a: 1,1,1
		}
	}
	AnimationCurve: 49500000104, "AnimCurve::RootRX", "" {
		Default: 0
		KeyVer: 4009
		KeyTime: *3 {
			a: 0,23093079000,46186158000
		}
		KeyValueFloat: *3 {
			a: 0,0,0
		}
		KeyAttrFlags: *3 {
			a: 4,4,4
		}
		KeyAttrDataFloat: *12 {
			a: 0,0,0,0,0,0,0,0,0,0,0,0
		}
		KeyAttrRefCount: *3 {
			a: 1,1,1
		}
	}
	AnimationCurve: 49500000105, "AnimCurve::RootRY", "" {
		Default: 0
		KeyVer: 4009
		KeyTime: *3 {
			a: 0,23093079000,46186158000
		}
		KeyValueFloat: *3 {
			a: 0,45,90
		}
		KeyAttrFlags: *3 {
			a: 4,4,4
		}
		KeyAttrDataFloat: *12 {
			a: 0,0,0,0,0,0,0,0,0,0,0,0
		}
		KeyAttrRefCount: *3 {
			a: 1,1,1
		}
	}
	AnimationCurve: 49500000106, "AnimCurve::RootRZ", "" {
		Default: 0
		KeyVer: 4009
		KeyTime: *3 {
			a: 0,23093079000,46186158000
		}
		KeyValueFloat: *3 {
			a: 0,0,0
		}
		KeyAttrFlags: *3 {
			a: 4,4,4
		}
		KeyAttrDataFloat: *12 {
			a: 0,0,0,0,0,0,0,0,0,0,0,0
		}
		KeyAttrRefCount: *3 {
			a: 1,1,1
		}
	}
	AnimationCurveNode: 49500000201, "AnimCurveNode::RootTranslation", "" {
		Properties70:  {
			P: "d|X", "Number", "", "A",0
			P: "d|Y", "Number", "", "A",0
			P: "d|Z", "Number", "", "A",0
		}
	}
	AnimationCurveNode: 49500000202, "AnimCurveNode::RootRotation", "" {
		Properties70:  {
			P: "d|X", "Number", "", "A",0
			P: "d|Y", "Number", "", "A",0
			P: "d|Z", "Number", "", "A",0
		}
	}
""";

    private const string RootMotionModelConnections = """
	;AnimCurveNode::RootTranslation, Model::InstanceA
	C: "OP",49500000201,49362921472, "Lcl Translation"

	;AnimCurveNode::RootRotation, Model::InstanceA
	C: "OP",49500000202,49362921472, "Lcl Rotation"

""";

    private const string RootMotionLayerConnections = """
	;AnimCurveNode::RootTranslation, AnimLayer::Animation Base Layer
	C: "OO",49500000201,49406012672

	;AnimCurveNode::RootRotation, AnimLayer::Animation Base Layer
	C: "OO",49500000202,49406012672

""";

    private const string RootMotionCurveConnections = """
	C: "OP",49500000101,49500000201,"d|X"
	C: "OP",49500000102,49500000201,"d|Y"
	C: "OP",49500000103,49500000201,"d|Z"
	C: "OP",49500000104,49500000202,"d|X"
	C: "OP",49500000105,49500000202,"d|Y"
	C: "OP",49500000106,49500000202,"d|Z"
""";

    private static void AssertVector(
        Vector3 actual, float x, float y, float z, float tolerance = 1e-5f)
    {
        Assert.True(MathF.Abs(actual.x - x) <= tolerance,
            $"x actual={actual.x:R}, expected={x:R}, vector={actual}");
        Assert.True(MathF.Abs(actual.y - y) <= tolerance,
            $"y actual={actual.y:R}, expected={y:R}, vector={actual}");
        Assert.True(MathF.Abs(actual.z - z) <= tolerance,
            $"z actual={actual.z:R}, expected={z:R}, vector={actual}");
    }

    private static void AssertQuaternion(
        Quaternion actual, float x, float y, float z, float w, float tolerance = 1e-5f)
    {
        Quaternion expected = new(x, y, z, w);
        if (Quaternion.Dot(actual, expected) < 0f)
            actual = new Quaternion(-actual.x, -actual.y, -actual.z, -actual.w);
        Assert.True(MathF.Abs(actual.x - expected.x) <= tolerance,
            $"x actual={actual}, expected={expected}");
        Assert.True(MathF.Abs(actual.y - expected.y) <= tolerance,
            $"y actual={actual}, expected={expected}");
        Assert.True(MathF.Abs(actual.z - expected.z) <= tolerance,
            $"z actual={actual}, expected={expected}");
        Assert.True(MathF.Abs(actual.w - expected.w) <= tolerance,
            $"w actual={actual}, expected={expected}");
    }

    private readonly record struct Imported(GameObject Root, AnimationClip Clip);
    private readonly record struct Runtime(
        GameObject Root, Transform Child, AnimationClip Clip, Animator Animator);
}

public sealed class AnimatorMoveRecorder : MonoBehaviour
{
    public int Calls;
    public bool ApplyBuiltin;
    public bool ApplyBuiltinTwice;
    public bool ApplyManual;
    public Vector3 DeltaSeen;
    public Vector3 RootSeen;
    public Quaternion RootRotationSeen = Quaternion.identity;
    public Vector3 AfterFirstBuiltin;
    public Vector3 AfterSecondBuiltin;

    public void ResetObservations()
    {
        Calls = 0;
        DeltaSeen = Vector3.zero;
        RootSeen = Vector3.zero;
        RootRotationSeen = Quaternion.identity;
        AfterFirstBuiltin = Vector3.zero;
        AfterSecondBuiltin = Vector3.zero;
    }

    private void OnAnimatorMove()
    {
        Calls++;
        Animator animator = GetComponent<Animator>();
        DeltaSeen = animator.deltaPosition;
        RootSeen = animator.rootPosition;
        RootRotationSeen = animator.rootRotation;
        if (ApplyManual)
        {
            transform.position += animator.deltaPosition;
            transform.rotation = animator.deltaRotation * transform.rotation;
            return;
        }
        if (!ApplyBuiltin) return;
        animator.ApplyBuiltinRootMotion();
        AfterFirstBuiltin = transform.position;
        if (!ApplyBuiltinTwice) return;
        animator.ApplyBuiltinRootMotion();
        AfterSecondBuiltin = transform.position;
    }
}

public class AnimatorMoveBaseRecorder : MonoBehaviour
{
    public int Calls;

    private void OnAnimatorMove()
    {
        Calls++;
    }
}

public sealed class DerivedAnimatorMoveRecorder : AnimatorMoveBaseRecorder
{
}
