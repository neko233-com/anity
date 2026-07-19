using System;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>High-frequency AssetDatabase object/load contracts — normal, type, error and collection paths.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetDatabaseLoadContractTests
{
    [Fact]
    public void CreateAsset_RegistersGenericLoadAndPath()
    {
        var asset = new TextAsset("load");
        var path = PathFor("generic.asset");

        AssetDatabase.CreateAsset(asset, path);

        Assert.Same(asset, AssetDatabase.LoadAssetAtPath<TextAsset>(path));
        Assert.Equal(path, AssetDatabase.GetAssetPath(asset));
    }

    [Fact]
    public void GenericLoad_ReturnsNullForDifferentType()
    {
        AssetDatabase.CreateAsset(new TextAsset("text"), PathFor("mismatch.asset"));

        Assert.Null(AssetDatabase.LoadAssetAtPath<GameObject>(PathFor("mismatch.asset")));
    }

    [Fact]
    public void TypedLoad_ReturnsMatchingObject()
    {
        var asset = new TextAsset("typed");
        var path = PathFor("typed.asset");
        AssetDatabase.CreateAsset(asset, path);

        Assert.Same(asset, AssetDatabase.LoadAssetAtPath(path, typeof(TextAsset)));
    }

    [Fact]
    public void TypedLoad_ReturnsNullForDifferentType()
    {
        AssetDatabase.CreateAsset(new TextAsset("typed"), PathFor("typed-mismatch.asset"));

        Assert.Null(AssetDatabase.LoadAssetAtPath(PathFor("typed-mismatch.asset"), typeof(GameObject)));
    }

    [Fact]
    public void TypedLoad_RejectsNullType()
    {
        Assert.Throws<ArgumentNullException>(() => AssetDatabase.LoadAssetAtPath(PathFor("null-type.asset"), null!));
    }

    [Fact]
    public void MainAssetLoad_ReturnsRegisteredObject()
    {
        var asset = new TextAsset("main");
        var path = PathFor("main.asset");
        AssetDatabase.CreateAsset(asset, path);

        Assert.Same(asset, AssetDatabase.LoadMainAssetAtPath(path));
    }

    [Fact]
    public void LoadAllAssets_ReturnsMainAsset()
    {
        var asset = new TextAsset("all");
        var path = PathFor("all.asset");
        AssetDatabase.CreateAsset(asset, path);

        Assert.Equal(new UnityEngine.Object[] { asset }, AssetDatabase.LoadAllAssetsAtPath(path));
    }

    [Fact]
    public void LoadRepresentations_ExcludesMainAssetWhenNoSubAssetsExist()
    {
        var asset = new TextAsset("representation");
        var path = PathFor("representations.asset");
        AssetDatabase.CreateAsset(asset, path);

        Assert.Empty(AssetDatabase.LoadAllAssetRepresentationsAtPath(path));
    }

    [Fact]
    public void MissingPaths_ReturnEmptyOrNull()
    {
        var path = PathFor("missing.asset");

        Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
        Assert.Null(AssetDatabase.LoadMainAssetAtPath(path));
        Assert.Empty(AssetDatabase.LoadAllAssetsAtPath(path));
    }

    [Fact]
    public void CreateAsset_RejectsNullAsset()
    {
        Assert.Throws<ArgumentNullException>(() => AssetDatabase.CreateAsset(null!, PathFor("null.asset")));
    }

    [Fact]
    public void AddObjectToAsset_RejectsUnknownTargetObject()
    {
        Assert.Throws<ArgumentException>(() => AssetDatabase.AddObjectToAsset(new TextAsset("child"), new TextAsset("unknown")));
    }

    private static string PathFor(string name) => "Assets/AssetDatabaseLoadTests/" + Guid.NewGuid().ToString("N") + "/" + name;
}
