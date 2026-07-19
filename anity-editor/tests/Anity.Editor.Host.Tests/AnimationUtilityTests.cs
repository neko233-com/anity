using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Editor.Host.Tests;

public sealed class AnimationUtilityTests
{
    [Fact]
    public void SetEditorCurve_RoundTripsAndPublishesModification()
    {
        var clip = new AnimationClip();
        var binding = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x");
        var calls = 0;
        AnimationUtility.OnCurveWasModified callback = (_, b, kind) => { if (b == binding && kind == AnimationUtility.CurveModifiedType.CurveModified) calls++; };
        AnimationUtility.onCurveWasModified += callback;
        try { AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(new Keyframe(0, 3))); }
        finally { AnimationUtility.onCurveWasModified -= callback; }
        Assert.NotNull(AnimationUtility.GetEditorCurve(clip, binding));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void GetAllCurves_ExposesBindingAndOptionalData()
    {
        var clip = NewPositionClip();
        Assert.Single(AnimationUtility.GetAllCurves(clip));
        Assert.Null(AnimationUtility.GetAllCurves(clip, false)[0].curve);
    }

    [Fact]
    public void SetEditorCurves_RejectsUnequalArrays()
    {
        Assert.Throws<ArgumentException>(() => AnimationUtility.SetEditorCurves(new AnimationClip(), Array.Empty<EditorCurveBinding>(), new[] { new AnimationCurve() }));
    }

    [Fact]
    public void ClipSettings_AreCopiedOnSetAndGet()
    {
        var clip = new AnimationClip();
        var settings = new AnimationClipSettings { loopTime = true, cycleOffset = .25f };
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        settings.loopTime = false;
        var read = AnimationUtility.GetAnimationClipSettings(clip);
        Assert.True(read.loopTime);
        Assert.Equal(.25f, read.cycleOffset);
    }

    [Fact]
    public void AnimationEvents_RoundTrip()
    {
        var clip = new AnimationClip();
        AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = .5f, functionName = "Hit" } });
        Assert.Single(AnimationUtility.GetAnimationEvents(clip));
        Assert.Equal("Hit", AnimationUtility.GetAnimationEvents(clip)[0].functionName);
    }

    [Fact]
    public void ObjectReferenceCurves_RoundTrip()
    {
        var clip = new AnimationClip();
        var binding = EditorCurveBinding.PPtrCurve("", typeof(Renderer), "m_Material");
        var value = new Material();
        AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] { new ObjectReferenceKeyframe { time = 1f, value = value } });
        Assert.Equal(value, AnimationUtility.GetObjectReferenceCurve(clip, binding)[0].value);
        Assert.Single(AnimationUtility.GetObjectReferenceCurveBindings(clip));
    }

    [Fact]
    public void TangentMetadata_RoundTrips()
    {
        var curve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
        AnimationUtility.SetKeyBroken(curve, 0, true);
        AnimationUtility.SetKeyLeftTangentMode(curve, 0, AnimationUtility.TangentMode.Linear);
        AnimationUtility.SetKeyRightTangentMode(curve, 0, AnimationUtility.TangentMode.Constant);
        Assert.True(AnimationUtility.GetKeyBroken(curve, 0));
        Assert.Equal(AnimationUtility.TangentMode.Linear, AnimationUtility.GetKeyLeftTangentMode(curve, 0));
        Assert.Equal(AnimationUtility.TangentMode.Constant, AnimationUtility.GetKeyRightTangentMode(curve, 0));
    }

    [Fact]
    public void TransformPathAndFloatValue_UseHierarchyAndBinding()
    {
        var root = new GameObject("Root");
        var child = new GameObject("Child"); child.transform.parent = root.transform; child.transform.localPosition = new Vector3(2, 0, 0);
        var binding = EditorCurveBinding.FloatCurve("Child", typeof(Transform), "m_LocalPosition.x");
        Assert.Equal("Child", AnimationUtility.CalculateTransformPath(child.transform, root.transform));
        Assert.True(AnimationUtility.GetFloatValue(root, binding, out var value));
        Assert.Equal(2f, value);
    }

    [Fact]
    public void AnimationClipsAndMotionCurves_AreManaged()
    {
        var go = new GameObject("Animated");
        var animation = go.AddComponent<Animation>();
        var clip = new AnimationClip { name = "Walk" };
        AnimationUtility.SetAnimationClips(animation, new[] { clip });
        AnimationUtility.SetGenerateMotionCurves(clip, true);
        Assert.Single(AnimationUtility.GetAnimationClips(go));
        Assert.True(AnimationUtility.GetGenerateMotionCurves(clip));
    }

    [Fact]
    public void PropertyModification_ConvertsToBinding()
    {
        var go = new GameObject("Root");
        var modification = new PropertyModification { target = go.transform, propertyPath = "m_LocalPosition.x" };
        var type = AnimationUtility.PropertyModificationToEditorCurveBinding(modification, go, out var binding);
        Assert.Equal(typeof(float), type);
        Assert.Equal("m_LocalPosition.x", binding.propertyName);
    }

    private static AnimationClip NewPositionClip()
    {
        var clip = new AnimationClip();
        AnimationUtility.SetEditorCurve(clip, "", typeof(Transform), "m_LocalPosition.x", new AnimationCurve(new Keyframe(0, 1)));
        return clip;
    }
}
