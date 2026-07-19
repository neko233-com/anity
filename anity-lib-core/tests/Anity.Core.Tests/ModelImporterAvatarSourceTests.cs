using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class ModelImporterAvatarSourceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-model-avatar-source-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public ModelImporterAvatarSourceTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void SourceAvatarPropertyMatchesOfficialAvatarContract()
    {
        var property = typeof(ModelImporter).GetProperty("sourceAvatar", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!;
        Assert.Equal(typeof(Avatar), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.True(property.CanWrite);
    }

    [Fact]
    public void MotionNodeNamePropertyMatchesOfficialStringContract()
    {
        var property = typeof(ModelImporter).GetProperty("motionNodeName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!;
        Assert.Equal(typeof(string), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.True(property.CanWrite);
    }

    [Fact]
    public void ModelImporterDoesNotExposeNonUnityAvatarAliases()
    {
        Assert.Null(typeof(ModelImporter).GetProperty("avatar", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(ModelImporter).GetProperty("isHuman", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void ModelImporterHasOfficialNativeBindingMetadata()
    {
        var attributes = typeof(ModelImporter).CustomAttributes.ToArray();
        Assert.Contains(attributes, attribute => attribute.AttributeType.FullName == "UnityEngine.Bindings.NativeHeaderAttribute" && attribute.ConstructorArguments[0].Value?.ToString() == "Modules/Animation/ScriptBindings/AvatarBuilder.bindings.h");
        Assert.Contains(attributes, attribute => attribute.AttributeType.FullName == "UnityEngine.Bindings.NativeHeaderAttribute" && attribute.ConstructorArguments[0].Value?.ToString() == "Modules/AssetPipelineEditor/Public/ModelImporting/ModelImporter.bindings.h");
        Assert.Contains(attributes, attribute => attribute.AttributeType.FullName == "UnityEngine.Bindings.NativeTypeAttribute" && attribute.NamedArguments.Any(argument => argument.MemberName == "Header" && argument.TypedValue.Value?.ToString() == "Modules/AssetPipelineEditor/Public/ModelImporting/ModelImporter.h"));
    }

    [Theory]
    [InlineData("Root")]
    [InlineData("Hips")]
    [InlineData("Spine")]
    [InlineData("Chest")]
    [InlineData("Neck")]
    [InlineData("Head")]
    [InlineData("LeftFoot")]
    [InlineData("RightFoot")]
    [InlineData("Root_M")]
    [InlineData("Armature")]
    public void ReadsOfficialRootMotionBoneName(string boneName)
    {
        var path = ImportModel(NewGuid(), "ReadMotion", "{instanceID: 0}", boneName);
        Assert.Equal(boneName, ModelImporter.GetAtPath(path).motionNodeName);
    }

    [Theory]
    [InlineData("Root")]
    [InlineData("Hips")]
    [InlineData("Spine")]
    [InlineData("Chest")]
    [InlineData("Neck")]
    [InlineData("Head")]
    [InlineData("LeftFoot")]
    [InlineData("RightFoot")]
    [InlineData("Root_M")]
    [InlineData("Armature")]
    public void WritesOfficialRootMotionBoneName(string boneName)
    {
        var path = ImportModel(NewGuid(), "WriteMotion", "{instanceID: 0}", "OldRoot");
        var importer = ModelImporter.GetAtPath(path);
        importer.motionNodeName = boneName;
        importer.SaveSettings();
        Assert.Contains("rootMotionBoneName: " + boneName, Meta(path));
    }

    [Fact]
    public void HumanModelImportWithoutDecodedHierarchyGeneratesInvalidAvatarSubAsset()
    {
        var path = ImportModel(NewGuid(), "HumanSource", "{instanceID: 0}", "Root");
        var avatar = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>());
        Assert.False(avatar.isValid);
        Assert.True(avatar.isHuman);
        Assert.Equal(Path.GetFileNameWithoutExtension(path) + "Avatar", avatar.name);
    }

    [Fact]
    public void ReadsSingleLineSourceAvatarReference()
    {
        const string guid = "11111111111111111111111111111111";
        var source = ImportModel(guid, "Source", "{instanceID: 0}", "Root");
        var target = ImportModel(NewGuid(), "Target", "{fileID: 9000000, guid: " + guid + ", type: 3}", "Root");
        Assert.Same(AssetDatabase.LoadAllAssetsAtPath(source).OfType<Avatar>().Single(), ModelImporter.GetAtPath(target).sourceAvatar);
    }

    [Fact]
    public void ReadsWrappedSourceAvatarReference()
    {
        const string guid = "22222222222222222222222222222222";
        var source = ImportModel(guid, "WrappedSource", "{instanceID: 0}", "Root");
        var target = ImportModel(NewGuid(), "WrappedTarget", "{fileID: 9000000, guid: " + guid + ",\n    type: 3}", "Root");
        Assert.Same(AssetDatabase.LoadAllAssetsAtPath(source).OfType<Avatar>().Single(), ModelImporter.GetAtPath(target).sourceAvatar);
    }

    [Fact]
    public void WritesSourceAvatarAsUnityGuidFileIdReference()
    {
        const string guid = "33333333333333333333333333333333";
        var source = ImportModel(guid, "WriteSource", "{instanceID: 0}", "Root");
        var target = ImportModel(NewGuid(), "WriteTarget", "{instanceID: 0}", "Root");
        var importer = ModelImporter.GetAtPath(target);
        importer.sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(source).OfType<Avatar>().Single();
        importer.SaveSettings();
        Assert.Contains("lastHumanDescriptionAvatarSource: {fileID: 9000000, guid: " + guid + ", type: 3}", Meta(target));
    }

    [Fact]
    public void ClearingSourceAvatarWritesNullInstanceReference()
    {
        const string guid = "44444444444444444444444444444444";
        ImportModel(guid, "ClearSource", "{instanceID: 0}", "Root");
        var target = ImportModel(NewGuid(), "ClearTarget", "{fileID: 9000000, guid: " + guid + ", type: 3}", "Root");
        var importer = ModelImporter.GetAtPath(target);
        importer.sourceAvatar = null!;
        importer.SaveSettings();
        Assert.Contains("lastHumanDescriptionAvatarSource: {instanceID: 0}", Meta(target));
        Assert.Null(importer.sourceAvatar);
    }

    [Fact]
    public void UnresolvedSourceGuidIsRetainedUntilDependencyImports()
    {
        const string guid = "55555555555555555555555555555555";
        var target = ImportModel(NewGuid(), "DeferredTarget", "{fileID: 9000000, guid: " + guid + ", type: 3}", "Root");
        var importer = ModelImporter.GetAtPath(target);
        Assert.Null(importer.sourceAvatar);
        importer.SaveSettings();
        Assert.Contains("guid: " + guid, Meta(target));
        var source = ImportModel(guid, "DeferredSource", "{instanceID: 0}", "Root");
        Assert.Same(AssetDatabase.LoadAllAssetsAtPath(source).OfType<Avatar>().Single(), importer.sourceAvatar);
    }

    [Fact]
    public void SourceAvatarResolvesAcrossProjectSession()
    {
        const string guid = "66666666666666666666666666666666";
        var source = ImportModel(guid, "SessionSource", "{instanceID: 0}", "Root");
        var target = ImportModel(NewGuid(), "SessionTarget", "{fileID: 9000000, guid: " + guid + ", type: 3}", "Root");
        EditorApplication.OpenProject(_dir);
        AssetDatabase.ImportAsset(source);
        AssetDatabase.ImportAsset(target);
        Assert.Equal(Path.GetFileNameWithoutExtension(source) + "Avatar", ModelImporter.GetAtPath(target).sourceAvatar.name);
    }

    [Fact]
    public void DeletedSourceAssetInvalidatesAvatarReference()
    {
        const string guid = "77777777777777777777777777777777";
        var source = ImportModel(guid, "DeleteSource", "{instanceID: 0}", "Root");
        var target = ImportModel(NewGuid(), "DeleteTarget", "{fileID: 9000000, guid: " + guid + ", type: 3}", "Root");
        var importer = ModelImporter.GetAtPath(target);
        Assert.NotNull(importer.sourceAvatar);
        Assert.True(AssetDatabase.DeleteAsset(source));
        Assert.Null(importer.sourceAvatar);
    }

    [Fact]
    public void ChangingSourceAvatarWritesReplacementGuid()
    {
        const string firstGuid = "88888888888888888888888888888888";
        const string secondGuid = "99999999999999999999999999999999";
        ImportModel(firstGuid, "FirstSource", "{instanceID: 0}", "Root");
        var second = ImportModel(secondGuid, "SecondSource", "{instanceID: 0}", "Root");
        var target = ImportModel(NewGuid(), "ChangeTarget", "{fileID: 9000000, guid: " + firstGuid + ", type: 3}", "Root");
        var importer = ModelImporter.GetAtPath(target);
        importer.sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(second).OfType<Avatar>().Single();
        importer.SaveSettings();
        Assert.Contains("guid: " + secondGuid, Meta(target));
        Assert.DoesNotContain("guid: " + firstGuid, Meta(target));
    }

    [Fact]
    public void SaveAddsMissingMotionAndAvatarSourceFields()
    {
        const string guid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var source = ImportModel(guid, "MissingSource", "{instanceID: 0}", "Root");
        var target = ImportModelWithoutMotionOrSource(NewGuid(), "MissingTarget");
        var importer = ModelImporter.GetAtPath(target);
        importer.motionNodeName = "Root";
        importer.sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(source).OfType<Avatar>().Single();
        importer.SaveSettings();
        var meta = Meta(target);
        Assert.Contains("rootMotionBoneName: Root", meta);
        Assert.Contains("lastHumanDescriptionAvatarSource: {fileID: 9000000, guid: " + guid + ", type: 3}", meta);
    }

    [Fact]
    public void SavePreservesRootMotionRotationAndUnknownHierarchyFields()
    {
        var path = ImportModel(NewGuid(), "Preserve", "{instanceID: 0}", "OldRoot");
        var importer = ModelImporter.GetAtPath(path);
        importer.motionNodeName = "NewRoot";
        importer.SaveSettings();
        var meta = Meta(path);
        Assert.Contains("rootMotionBoneRotation: {x: 0, y: 0, z: 0, w: 1}", meta);
        Assert.Contains("skeletonHasParents: 1", meta);
    }

    [Fact]
    public void MotionNodeNameUsesUnityYamlEscaping()
    {
        var path = ImportModel(NewGuid(), "Escaped", "{instanceID: 0}", "Root");
        var importer = ModelImporter.GetAtPath(path);
        importer.motionNodeName = "Rig: Root Bone";
        importer.SaveSettings();
        Assert.Contains("rootMotionBoneName: \"Rig: Root Bone\"", Meta(path));
    }

    private string ImportModel(string guid, string name, string avatarSource, string motionNodeName)
    {
        var path = "Assets/Models/" + name + "-" + Guid.NewGuid().ToString("N") + ".fbx";
        Write(path, "Kaydara FBX Binary");
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\nModelImporter:\n  serializedVersion: 23\n  humanDescription:\n    serializedVersion: 3\n    human: []\n    skeleton: []\n    rootMotionBoneName: " + motionNodeName + "\n    rootMotionBoneRotation: {x: 0, y: 0, z: 0, w: 1}\n    hasTranslationDoF: 0\n    skeletonHasParents: 1\n  lastHumanDescriptionAvatarSource: " + avatarSource + "\n  autoGenerateAvatarMappingIfUnspecified: 1\n  animationType: 3\n  avatarSetup: 1\n  assetBundleName: \n  assetBundleVariant: \n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private string ImportModelWithoutMotionOrSource(string guid, string name)
    {
        var path = "Assets/Models/" + name + "-" + Guid.NewGuid().ToString("N") + ".fbx";
        Write(path, "Kaydara FBX Binary");
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\nModelImporter:\n  serializedVersion: 23\n  humanDescription:\n    serializedVersion: 3\n    human: []\n    skeleton: []\n    rootMotionBoneRotation: {x: 0, y: 0, z: 0, w: 1}\n    hasTranslationDoF: 0\n  autoGenerateAvatarMappingIfUnspecified: 1\n  animationType: 3\n  avatarSetup: 1\n  assetBundleName: \n  assetBundleVariant: \n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private void Write(string path, string contents)
    {
        var fullPath = FullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
    }

    private string Meta(string path) => File.ReadAllText(FullPath(path) + ".meta");
    private string FullPath(string path) => Path.Combine(_dir, path);
    private static string NewGuid() => Guid.NewGuid().ToString("N");
}
