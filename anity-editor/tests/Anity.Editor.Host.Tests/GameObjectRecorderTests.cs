using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Xunit;

namespace Anity.Editor.Host.Tests;

public sealed class GameObjectRecorderTests
{
    [Fact]
    public void Constructor_ExposesRootAndInitialTime()
    {
        var root = new GameObject("Root");
        var recorder = new GameObjectRecorder(root);
        Assert.Same(root, recorder.root);
        Assert.Equal(0f, recorder.currentTime);
        Assert.False(recorder.isRecording);
    }

    [Fact]
    public void Bind_RejectsIncompleteBinding()
    {
        var recorder = new GameObjectRecorder(new GameObject("Root"));
        Assert.Throws<ArgumentException>(() => recorder.Bind(default));
    }

    [Fact]
    public void BindTransform_RecordsPositionCurve()
    {
        var root = new GameObject("Root");
        var recorder = new GameObjectRecorder(root);
        recorder.Bind(EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"));
        root.transform.localPosition = new Vector3(1, 0, 0); recorder.TakeSnapshot(1f);
        root.transform.localPosition = new Vector3(3, 0, 0); recorder.TakeSnapshot(1f);
        var clip = new AnimationClip(); recorder.SaveToClip(clip);
        var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"));
        Assert.NotNull(curve);
        Assert.Equal(2, curve.length);
        Assert.Equal(3f, curve.keys[1].value);
    }

    [Fact]
    public void TakeSnapshot_RejectsNonFiniteAndNegativeDelta()
    {
        var recorder = new GameObjectRecorder(new GameObject("Root"));
        recorder.Bind(EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => recorder.TakeSnapshot(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => recorder.TakeSnapshot(float.NaN));
    }

    [Fact]
    public void ResetRecording_ClearsSamplesAndTime()
    {
        var root = new GameObject("Root");
        var recorder = new GameObjectRecorder(root);
        recorder.Bind(EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"));
        recorder.TakeSnapshot(1);
        recorder.ResetRecording();
        var clip = new AnimationClip(); recorder.SaveToClip(clip);
        Assert.Equal(0f, recorder.currentTime);
        Assert.Null(AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x")));
    }

    [Fact]
    public void BindAll_RecursivelyBindsChildTransform()
    {
        var root = new GameObject("Root");
        var child = new GameObject("Child"); child.transform.parent = root.transform;
        var recorder = new GameObjectRecorder(root);
        recorder.BindAll(root, recursive: true);
        Assert.Contains(recorder.GetBindings(), binding => binding.path == "Child" && binding.propertyName == "m_LocalPosition.x");
    }

    [Fact]
    public void BindComponent_RecordsNumericComponentField()
    {
        var root = new GameObject("Root");
        var component = root.AddComponent<RecorderComponent>(); component.value = 2f;
        var recorder = new GameObjectRecorder(root);
        recorder.BindComponent(component);
        recorder.TakeSnapshot(1f);
        component.value = 4f; recorder.TakeSnapshot(1f);
        var clip = new AnimationClip(); recorder.SaveToClip(clip);
        var binding = EditorCurveBinding.FloatCurve("", typeof(RecorderComponent), nameof(RecorderComponent.value));
        Assert.Equal(4f, AnimationUtility.GetEditorCurve(clip, binding)!.keys[1].value);
    }

    [Fact]
    public void BindComponentsOfType_RespectsRecursiveTargeting()
    {
        var root = new GameObject("Root"); root.AddComponent<RecorderComponent>();
        var child = new GameObject("Child"); child.transform.parent = root.transform; child.AddComponent<RecorderComponent>();
        var recorder = new GameObjectRecorder(root);
        recorder.BindComponentsOfType<RecorderComponent>(root, recursive: true);
        Assert.Equal(2, recorder.GetBindings().Count(binding => binding.type == typeof(RecorderComponent) && binding.propertyName == nameof(RecorderComponent.value)));
    }

    [Fact]
    public void FilterOptions_ReduceLinearMiddleKey()
    {
        var root = new GameObject("Root");
        var recorder = new GameObjectRecorder(root);
        recorder.Bind(EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"));
        root.transform.localPosition = new Vector3(0, 0, 0); recorder.TakeSnapshot(1);
        root.transform.localPosition = new Vector3(1, 0, 0); recorder.TakeSnapshot(1);
        root.transform.localPosition = new Vector3(2, 0, 0); recorder.TakeSnapshot(1);
        var clip = new AnimationClip(); recorder.SaveToClip(clip, 60, new CurveFilterOptions { keyframeReduction = true, floatError = .001f });
        Assert.Equal(2, AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"))!.length);
    }

    [Fact]
    public void SaveToClip_RejectsInvalidFps()
    {
        var recorder = new GameObjectRecorder(new GameObject("Root"));
        Assert.Throws<ArgumentOutOfRangeException>(() => recorder.SaveToClip(new AnimationClip(), 0));
    }

    [Fact]
    public void GetBindings_ReturnsStableSortedSnapshot()
    {
        var root = new GameObject("Root");
        var recorder = new GameObjectRecorder(root);
        recorder.Bind(EditorCurveBinding.FloatCurve("z", typeof(Transform), "m_LocalPosition.z"));
        recorder.Bind(EditorCurveBinding.FloatCurve("a", typeof(Transform), "m_LocalPosition.x"));
        var bindings = recorder.GetBindings();
        Assert.Equal("a", bindings[0].path);
        Assert.Equal(2, bindings.Length);
    }

    [Fact]
    public void BindComponentsOfTypeOverload_UsesRequestedType()
    {
        var root = new GameObject("Root"); root.AddComponent<RecorderComponent>();
        var recorder = new GameObjectRecorder(root);
        recorder.BindComponentsOfType(root, typeof(RecorderComponent), recursive: false);
        Assert.Contains(recorder.GetBindings(), binding => binding.type == typeof(RecorderComponent));
    }

    public sealed class RecorderComponent : Component { public float value; public int count; public bool enabledFlag; }
}
