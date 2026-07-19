using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Editor.Host.Tests;

public sealed class MonoScriptTests
{
    [Fact]
    public void FromMonoBehaviour_ReturnsScriptForConcreteComponentType()
    {
        var script = MonoScript.FromMonoBehaviour(new GameObject().AddComponent<FirstBehaviour>());
        Assert.NotNull(script);
        Assert.Equal(typeof(FirstBehaviour), script!.GetClass());
    }

    [Fact]
    public void FromMonoBehaviour_ReturnsStableScriptPerType()
    {
        var first = MonoScript.FromMonoBehaviour(new GameObject().AddComponent<FirstBehaviour>());
        var second = MonoScript.FromMonoBehaviour(new GameObject().AddComponent<FirstBehaviour>());
        Assert.Same(first, second);
    }

    [Fact]
    public void FromMonoBehaviour_DistinguishesConcreteComponentTypes()
    {
        var first = MonoScript.FromMonoBehaviour(new GameObject().AddComponent<FirstBehaviour>());
        var second = MonoScript.FromMonoBehaviour(new GameObject().AddComponent<SecondBehaviour>());
        Assert.NotSame(first, second);
    }

    [Fact]
    public void FromMonoBehaviour_SetsScriptAssetName()
    {
        var script = MonoScript.FromMonoBehaviour(new GameObject().AddComponent<FirstBehaviour>());
        Assert.Equal(nameof(FirstBehaviour), script!.name);
    }

    [Fact]
    public void FromScriptableObject_ReturnsScriptForConcreteAssetType()
    {
        var script = MonoScript.FromScriptableObject(ScriptableObject.CreateInstance<FirstAsset>());
        Assert.NotNull(script);
        Assert.Equal(typeof(FirstAsset), script!.GetClass());
    }

    [Fact]
    public void FromScriptableObject_ReturnsStableScriptPerType()
    {
        var first = MonoScript.FromScriptableObject(ScriptableObject.CreateInstance<FirstAsset>());
        var second = MonoScript.FromScriptableObject(ScriptableObject.CreateInstance<FirstAsset>());
        Assert.Same(first, second);
    }

    [Fact]
    public void FromScriptableObject_DistinguishesConcreteAssetTypes()
    {
        var first = MonoScript.FromScriptableObject(ScriptableObject.CreateInstance<FirstAsset>());
        var second = MonoScript.FromScriptableObject(ScriptableObject.CreateInstance<SecondAsset>());
        Assert.NotSame(first, second);
    }

    [Fact]
    public void FromScriptableObject_SetsScriptAssetName()
    {
        var script = MonoScript.FromScriptableObject(ScriptableObject.CreateInstance<SecondAsset>());
        Assert.Equal(nameof(SecondAsset), script!.name);
    }

    [Fact]
    public void FromMonoBehaviour_NullReturnsNoScript()
    {
        Assert.Null(MonoScript.FromMonoBehaviour(null!));
    }

    [Fact]
    public void FromScriptableObject_NullReturnsNoScript()
    {
        Assert.Null(MonoScript.FromScriptableObject(null!));
    }

    [Fact]
    public void MonoScript_InheritsTextAsset()
    {
        var script = MonoScript.FromMonoBehaviour(new GameObject().AddComponent<FirstBehaviour>());
        Assert.IsAssignableFrom<TextAsset>(script);
    }

    [Fact]
    public void MonoScript_HasPublicParameterlessConstructorLikeUnity()
    {
        Assert.NotNull(typeof(MonoScript).GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null));
        Assert.Null(new MonoScript().GetClass());
    }

    private sealed class FirstBehaviour : MonoBehaviour { }
    private sealed class SecondBehaviour : MonoBehaviour { }
    private sealed class FirstAsset : ScriptableObject { }
    private sealed class SecondAsset : ScriptableObject { }
}
