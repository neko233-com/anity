using System.Threading;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;
using Xunit;

namespace Anity.Editor.Host.Tests;

/// <summary>UnityEditor.AnimatedValues editor-update interpolation and callback semantics.</summary>
public sealed class AnimatedValuesTests
{
    [Fact]
    public void AnimBool_Default_IsFalseAndNotAnimating()
    {
        var value = new AnimBool();
        Assert.False(value.value);
        Assert.False(value.target);
        Assert.False(value.isAnimating);
        Assert.Equal(0f, value.faded);
    }

    [Fact]
    public void AnimBool_Target_AdvancesFromEditorUpdate()
    {
        var value = new AnimBool(false) { speed = 10000f };
        value.target = true;
        Assert.True(value.isAnimating);
        AdvanceEditor();
        Assert.True(value.value);
        Assert.True(value.target);
        Assert.False(value.isAnimating);
        Assert.Equal(1f, value.faded);
    }

    [Fact]
    public void AnimBool_Fade_UsesFadedProgress()
    {
        var value = new AnimBool(false) { speed = 10000f };
        value.target = true;
        Assert.Equal(10f, value.Fade(10f, 20f));
        AdvanceEditor();
        Assert.Equal(20f, value.Fade(10f, 20f));
    }

    [Fact]
    public void AnimFloat_Target_InterpolatesToTarget()
    {
        var value = new AnimFloat(2f) { speed = 10000f };
        value.target = 8f;
        AdvanceEditor();
        Assert.Equal(8f, value.value);
        Assert.Equal(8f, value.target);
        Assert.False(value.isAnimating);
    }

    [Fact]
    public void AnimFloat_Value_SnapsAndCancelsAnimation()
    {
        var value = new AnimFloat(0f) { speed = 0.01f };
        value.target = 10f;
        Assert.True(value.isAnimating);
        value.value = 3f;
        Assert.Equal(3f, value.value);
        Assert.Equal(3f, value.target);
        Assert.False(value.isAnimating);
    }

    [Fact]
    public void AnimVector3_Target_InterpolatesAllComponents()
    {
        var value = new AnimVector3(Vector3.zero) { speed = 10000f };
        value.target = new Vector3(2f, 4f, 6f);
        AdvanceEditor();
        Assert.Equal(new Vector3(2f, 4f, 6f), value.value);
    }

    [Fact]
    public void AnimQuaternion_Target_InterpolatesToTarget()
    {
        var target = Quaternion.Euler(0f, 90f, 0f);
        var value = new AnimQuaternion(Quaternion.identity) { speed = 10000f };
        value.target = target;
        AdvanceEditor();
        Assert.Equal(target, value.value);
    }

    [Fact]
    public void Callback_FiresWhenTargetAnimationAdvances()
    {
        var calls = 0;
        var value = new AnimFloat(0f, () => calls++) { speed = 10000f };
        value.target = 1f;
        AdvanceEditor();
        Assert.True(calls >= 1);
    }

    [Fact]
    public void ReassigningSameTarget_DoesNotRestartAnimation()
    {
        var value = new AnimFloat(0f) { speed = 0.01f };
        value.target = 1f;
        Assert.True(value.isAnimating);
        value.target = 1f;
        Assert.True(value.isAnimating);
        value.value = 1f;
    }

    [Fact]
    public void BaseAnimValueNonAlloc_UsesSameTargetAndValueContract()
    {
        var value = new IntAnimValue(1) { speed = 10000f };
        value.target = 9;
        AdvanceEditor();
        Assert.Equal(9, value.value);
        Assert.Equal(9, value.target);
        Assert.False(value.isAnimating);
    }

    private static void AdvanceEditor()
    {
        Thread.Sleep(2);
        EditorApplication.Update();
    }

    private sealed class IntAnimValue : BaseAnimValueNonAlloc<int>
    {
        public IntAnimValue(int initial) : base(initial) { }
        protected override int GetValue() => lerpPosition < 1f ? start : target;
    }
}
