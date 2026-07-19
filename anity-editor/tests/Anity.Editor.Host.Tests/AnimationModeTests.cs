using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using Xunit;

namespace Anity.Editor.Host.Tests;

[Collection("AnimationMode")]
public sealed class AnimationModeTests
{
    [Fact]
    public void EditorCurveBinding_FactoriesExposeExpectedKinds()
    {
        var floatBinding = EditorCurveBinding.FloatCurve("Child", typeof(Transform), "m_LocalPosition.x");
        var pptrBinding = EditorCurveBinding.PPtrCurve("", typeof(Renderer), "m_Materials.Array.data[0]");
        var discreteBinding = EditorCurveBinding.DiscreteCurve("", typeof(GameObject), "m_IsActive");

        Assert.False(floatBinding.isPPtrCurve);
        Assert.True(pptrBinding.isPPtrCurve);
        Assert.True(discreteBinding.isDiscreteCurve);
        Assert.Equal(typeof(Transform), floatBinding.type);
    }

    [Fact]
    public void EditorCurveBinding_SerializeReferenceRetainsAllFlags()
    {
        var binding = EditorCurveBinding.SerializeReferenceCurve("Path", typeof(Transform), 42, "field", true, true);
        Assert.True(binding.isSerializeReferenceCurve);
        Assert.True(binding.isPPtrCurve);
        Assert.True(binding.isDiscreteCurve);
    }

    [Fact]
    public void EditorCurveBinding_EqualityIncludesCurveIdentity()
    {
        var first = EditorCurveBinding.FloatCurve("A", typeof(Transform), "m_LocalPosition.x");
        var same = EditorCurveBinding.FloatCurve("A", typeof(Transform), "m_LocalPosition.x");
        var different = EditorCurveBinding.FloatCurve("A", typeof(Transform), "m_LocalPosition.y");
        Assert.True(first == same);
        Assert.False(first != same);
        Assert.False(first.Equals(different));
    }

    [Fact]
    public void DefaultAnimationMode_StartAndStopChangesGlobalState()
    {
        EnsureStopped();
        AnimationMode.StartAnimationMode();
        Assert.True(AnimationMode.InAnimationMode());
        AnimationMode.StopAnimationMode();
        Assert.False(AnimationMode.InAnimationMode());
    }

    [Fact]
    public void DriverAnimationMode_IsTrackedIndependently()
    {
        EnsureStopped();
        var driver = new AnimationModeDriver();
        AnimationMode.StartAnimationMode(driver);
        Assert.True(AnimationMode.InAnimationMode(driver));
        AnimationMode.StopAnimationMode(driver);
        Assert.False(AnimationMode.InAnimationMode(driver));
    }

    [Fact]
    public void SamplingAnimationClip_RestoresTransformOnStop()
    {
        EnsureStopped();
        var go = new GameObject("Animated");
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", new AnimationCurve(new Keyframe(0f, 4f)));

        AnimationMode.StartAnimationMode();
        AnimationMode.SampleAnimationClip(go, clip, 0f);
        Assert.Equal(4f, go.transform.localPosition.x);
        AnimationMode.StopAnimationMode();
        Assert.Equal(0f, go.transform.localPosition.x);
    }

    [Fact]
    public void SamplingScopes_RequireBalancedBeginAndEnd()
    {
        EnsureStopped();
        Assert.Throws<InvalidOperationException>(() => AnimationMode.BeginSampling());
        AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        AnimationMode.EndSampling();
        Assert.Throws<InvalidOperationException>(() => AnimationMode.EndSampling());
        AnimationMode.StopAnimationMode();
    }

    [Fact]
    public void AddEditorCurveBinding_MarksGameObjectPropertyAnimated()
    {
        EnsureStopped();
        var go = new GameObject("Bound");
        var binding = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x");
        AnimationMode.StartAnimationMode();
        AnimationMode.AddEditorCurveBinding(go, binding);
        Assert.True(AnimationMode.IsPropertyAnimated(go, "m_LocalPosition.x"));
        AnimationMode.StopAnimationMode();
    }

    [Fact]
    public void AddPropertyModification_AppliesAndRestoresTransformComponent()
    {
        EnsureStopped();
        var go = new GameObject("Modified");
        var binding = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y");
        var modification = new PropertyModification
        {
            target = go.transform,
            propertyPath = "m_LocalPosition.y",
            value = "7.5"
        };

        AnimationMode.StartAnimationMode();
        AnimationMode.AddPropertyModification(binding, modification, keepPrefabOverride: true);
        Assert.Equal(7.5f, go.transform.localPosition.y);
        Assert.True(AnimationMode.IsPropertyAnimated(go.transform, "m_LocalPosition.y"));
        AnimationMode.StopAnimationMode();
        Assert.Equal(0f, go.transform.localPosition.y);
    }

    [Fact]
    public void SamplePlayableGraph_SetsGraphTime()
    {
        EnsureStopped();
        var graph = PlayableGraph.Create("Preview");
        AnimationMode.StartAnimationMode();
        AnimationMode.SamplePlayableGraph(graph, 0, 2.25f);
        Assert.Equal(2.25d, graph.GetTime());
        AnimationMode.StopAnimationMode();
        graph.Destroy();
    }

    [Fact]
    public void Colors_AreOpaqueAndDistinct()
    {
        Assert.Equal(1f, AnimationMode.animatedPropertyColor.a);
        Assert.Equal(1f, AnimationMode.candidatePropertyColor.a);
        Assert.NotEqual(AnimationMode.animatedPropertyColor, AnimationMode.recordedPropertyColor);
    }

    private static void EnsureStopped()
    {
        if (AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();
    }
}

[CollectionDefinition("AnimationMode", DisableParallelization = true)]
public sealed class AnimationModeCollection
{
}
