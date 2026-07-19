using UnityEditor;
using UnityEngine;
using Xunit;
using Object = UnityEngine.Object;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeAnimatorAvatarMaskLayerTests
{
    [Fact]
    public void OverrideLayerWeightZeroPreservesBasePose()
        => AssertPose(Evaluate(0f), 10f, 20f, 40f, 30f);

    [Fact]
    public void OverrideLayerQuarterWeightMatchesUnityBatchmodeProbe()
        => AssertPose(Evaluate(0.25f), 32.5f, 65f, 130f, 97.5f);

    [Fact]
    public void OverrideLayerFullWeightReplacesAnimatedTransforms()
        => AssertPose(Evaluate(1f), 100f, 200f, 400f, 300f);

    [Theory]
    [InlineData(-1f)]
    [InlineData(-0.25f)]
    [InlineData(2f)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void OutOfRangeFiniteOrInfiniteWeightUsesFullUpperLayerLikeUnity(float weight)
        => AssertPose(Evaluate(weight), 100f, 200f, 400f, 300f);

    [Fact]
    public void NaNLayerWeightDisablesUpperLayerLikeUnity()
        => AssertPose(Evaluate(float.NaN), 10f, 20f, 40f, 30f);

    [Fact]
    public void EmptyTransformMaskDoesNotFilterGenericAnimation()
    {
        AvatarMask mask = Mask();
        AssertPose(Evaluate(1f, mask), 100f, 200f, 400f, 300f);
    }

    [Fact]
    public void ExactMaskPathFiltersSiblingAndDescendantButEnablesRoot()
    {
        AvatarMask mask = Mask(("A", true));
        AssertPose(Evaluate(1f, mask), 100f, 200f, 40f, 30f);
    }

    [Fact]
    public void GrandchildMaskDoesNotImplicitlyEnableParent()
    {
        AvatarMask mask = Mask(("A/Grand", true));
        AssertPose(Evaluate(1f, mask), 100f, 20f, 400f, 30f);
    }

    [Fact]
    public void AllInactiveMaskEntriesDisableOverrideLayer()
    {
        AvatarMask mask = Mask((string.Empty, false), ("A", false), ("B", false));
        AssertPose(Evaluate(1f, mask), 10f, 20f, 40f, 30f);
    }

    [Fact]
    public void MissingActivePathOnlyKeepsUnityRootParticipation()
    {
        AvatarMask mask = Mask(("Missing", true));
        AssertPose(Evaluate(1f, mask), 100f, 20f, 40f, 30f);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void DuplicateMaskPathsUseOrderIndependentActiveOr(bool first, bool second)
    {
        AvatarMask mask = Mask(("A", first), ("A", second));
        AssertPose(Evaluate(1f, mask), 100f, 200f, 40f, 30f);
    }

    [Fact]
    public void PartialUpperClipLeavesUnanimatedBaseBindingsUntouched()
    {
        AvatarMask? mask = null;
        AssertPose(Evaluate(1f, mask, upperIncludesGrandAndB: false), 100f, 200f, 40f, 30f);
    }

    [Fact]
    public void Utf8MaskPathFiltersExactTransform()
    {
        GameObject root = new("Root");
        GameObject child = new("角色");
        child.transform.SetParent(root.transform, false);
        var mask = Mask(("角色", true));
        AnimationClip baseClip = PositionClip((string.Empty, 10f), ("角色", 20f));
        AnimationClip upperClip = PositionClip((string.Empty, 100f), ("角色", 200f));
        AnimatorController controller = Controller(baseClip, upperClip, 1f, AnimatorLayerBlendingMode.Override, mask);
        try
        {
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0.25f);
            Assert.Equal(100f, root.transform.localPosition.x, 4);
            Assert.Equal(200f, child.transform.localPosition.x, 4);
        }
        finally
        {
            Destroy(root, controller, baseClip, upperClip, mask);
        }
    }

    [Fact]
    public void OverrideRotationUsesUnityNormalizedLerpAndScaleUsesLinearBlend()
    {
        GameObject root = new("Root");
        AnimationClip baseClip = TransformClip(Quaternion.identity, 2f);
        AnimationClip upperClip = TransformClip(Quaternion.AngleAxis(180f, Vector3.forward), 4f);
        AnimatorController controller = Controller(baseClip, upperClip, 0.25f);
        try
        {
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0.25f);
            Quaternion rotation = root.transform.localRotation;
            Assert.Equal(-0.3162278f, rotation.z, 5);
            Assert.Equal(0.9486833f, rotation.w, 5);
            Assert.Equal(new Vector3(2.5f, 2.5f, 2.5f), root.transform.localScale);
        }
        finally
        {
            Destroy(root, controller, baseClip, upperClip);
        }
    }

    [Fact]
    public void AdditiveLayerWithoutReferencePoseIsIgnoredLikeUnity()
        => AssertPose(Evaluate(1f, mode: AnimatorLayerBlendingMode.Additive), 10f, 20f, 40f, 30f);

    [Fact]
    public void AdditiveReferencePoseAppliesPositionDeltasAtLayerWeight()
    {
        AnimationClip reference = PositionClip((string.Empty, 90f), ("A", 180f), ("A/Grand", 360f), ("B", 270f));
        AssertPose(Evaluate(0.25f, referenceClip: reference, validAdditiveReference: true, mode: AnimatorLayerBlendingMode.Additive),
            12.5f, 25f, 50f, 37.5f);
    }

    [Fact]
    public void ProgrammaticAdditiveReferenceWithoutMecanimDataIsIgnoredLikeUnity()
    {
        AnimationClip reference = PositionClip((string.Empty, 90f), ("A", 180f), ("A/Grand", 360f), ("B", 270f));
        AssertPose(Evaluate(1f, referenceClip: reference, mode: AnimatorLayerBlendingMode.Additive),
            10f, 20f, 40f, 30f);
    }

    [Fact]
    public void AdditiveReferencePoseHonorsAvatarMask()
    {
        AnimationClip reference = PositionClip((string.Empty, 90f), ("A", 180f), ("A/Grand", 360f), ("B", 270f));
        AvatarMask mask = Mask(("A", true));
        AssertPose(Evaluate(1f, mask, referenceClip: reference, validAdditiveReference: true, mode: AnimatorLayerBlendingMode.Additive),
            20f, 40f, 40f, 30f);
    }

    [Fact]
    public void CrossFadeUsesNativePoseBlendInsteadOfLastClipWins()
    {
        GameObject root = new("Root");
        AnimationClip first = PositionClip((string.Empty, 0f));
        AnimationClip second = PositionClip((string.Empty, 100f));
        var controller = new AnimatorController();
        AnimatorState firstState = controller.layers[0].stateMachine.AddState("First");
        firstState.motion = first;
        AnimatorState secondState = controller.layers[0].stateMachine.AddState("Second");
        secondState.motion = second;
        controller.layers[0].stateMachine.defaultState = firstState;
        try
        {
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.CrossFade("Second", 1f);
            animator.Update(0.25f);
            Assert.Equal(25f, root.transform.localPosition.x, 4);
        }
        finally
        {
            Destroy(root, controller, first, second);
        }
    }

    [Fact]
    public void PartialTransformCurvePreservesUnanimatedComponents()
    {
        GameObject root = new("Root");
        root.transform.localPosition = new Vector3(1f, 7f, 8f);
        AnimationClip clip = PositionClip((string.Empty, 10f));
        var controller = new AnimatorController();
        AnimatorState state = controller.layers[0].stateMachine.AddState("Base");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        try
        {
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0.25f);
            Assert.Equal(new Vector3(10f, 7f, 8f), root.transform.localPosition);
        }
        finally
        {
            Destroy(root, controller, clip);
        }
    }

    private static PoseResult Evaluate(
        float weight,
        AvatarMask? mask = null,
        bool upperIncludesGrandAndB = true,
        AnimationClip? referenceClip = null,
        bool validAdditiveReference = false,
        AnimatorLayerBlendingMode mode = AnimatorLayerBlendingMode.Override)
    {
        GameObject root = new("Root");
        GameObject a = new("A");
        GameObject grand = new("Grand");
        GameObject b = new("B");
        a.transform.SetParent(root.transform, false);
        grand.transform.SetParent(a.transform, false);
        b.transform.SetParent(root.transform, false);
        AnimationClip baseClip = PositionClip((string.Empty, 10f), ("A", 20f), ("A/Grand", 40f), ("B", 30f));
        AnimationClip upperClip = upperIncludesGrandAndB
            ? PositionClip((string.Empty, 100f), ("A", 200f), ("A/Grand", 400f), ("B", 300f))
            : PositionClip((string.Empty, 100f), ("A", 200f));
        if (referenceClip is not null)
        {
            if (validAdditiveReference)
            {
                upperClip.MarkMecanimDataBuilt();
                referenceClip.MarkMecanimDataBuilt();
            }
            AnimationUtility.SetAdditiveReferencePose(upperClip, referenceClip, 0f);
        }
        AnimatorController controller = Controller(baseClip, upperClip, weight, mode, mask);
        try
        {
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0.25f);
            return new PoseResult(
                root.transform.localPosition.x,
                a.transform.localPosition.x,
                grand.transform.localPosition.x,
                b.transform.localPosition.x);
        }
        finally
        {
            Destroy(root, controller, baseClip, upperClip, referenceClip, mask);
        }
    }

    private static AnimatorController Controller(
        AnimationClip baseClip,
        AnimationClip upperClip,
        float weight,
        AnimatorLayerBlendingMode mode = AnimatorLayerBlendingMode.Override,
        AvatarMask? mask = null)
    {
        var controller = new AnimatorController();
        AnimatorControllerLayer baseLayer = controller.layers[0];
        AnimatorState baseState = baseLayer.stateMachine.AddState("Base");
        baseState.motion = baseClip;
        baseLayer.stateMachine.defaultState = baseState;
        AnimatorControllerLayer upperLayer = controller.AddLayer("Upper");
        AnimatorState upperState = upperLayer.stateMachine.AddState("Upper");
        upperState.motion = upperClip;
        upperLayer.stateMachine.defaultState = upperState;
        upperLayer.weight = weight;
        upperLayer.blendingMode = mode;
        upperLayer.avatarMask = mask!;
        return controller;
    }

    private static AnimationClip PositionClip(params (string Path, float Value)[] values)
    {
        var clip = new AnimationClip();
        foreach ((string path, float value) in values)
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, 1f, value));
        return clip;
    }

    private static AnimationClip TransformClip(Quaternion rotation, float scale)
    {
        var clip = new AnimationClip();
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.x", AnimationCurve.Constant(0f, 1f, rotation.x));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.y", AnimationCurve.Constant(0f, 1f, rotation.y));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.z", AnimationCurve.Constant(0f, 1f, rotation.z));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalRotation.w", AnimationCurve.Constant(0f, 1f, rotation.w));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.x", AnimationCurve.Constant(0f, 1f, scale));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.y", AnimationCurve.Constant(0f, 1f, scale));
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalScale.z", AnimationCurve.Constant(0f, 1f, scale));
        return clip;
    }

    private static AvatarMask Mask(params (string Path, bool Active)[] entries)
    {
        var mask = new AvatarMask { transformCount = entries.Length };
        for (int index = 0; index < entries.Length; ++index)
        {
            mask.SetTransformPath(index, entries[index].Path);
            mask.SetTransformActive(index, entries[index].Active);
        }
        return mask;
    }

    private static void AssertPose(PoseResult result, float root, float a, float grand, float b)
    {
        Assert.Equal(root, result.Root, 4);
        Assert.Equal(a, result.A, 4);
        Assert.Equal(grand, result.Grand, 4);
        Assert.Equal(b, result.B, 4);
    }

    private static void Destroy(params Object?[] objects)
    {
        foreach (Object? value in objects)
            if (value is not null) Object.DestroyImmediate(value);
    }

    private readonly record struct PoseResult(float Root, float A, float Grand, float B);
}
