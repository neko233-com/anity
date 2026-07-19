using System;
using UnityEditor;
using UnityEngine;
using Xunit;
using EditorAssetImporter = UnityEditor.AssetImporter;

namespace Anity.Core.Tests;

/// <summary>Asset importer registry — type selection, stable identity and settings retention.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetImporterRegistryTests
{
    [Fact] public void TextureAsset_GetsTextureImporter() { var path = PathFor("texture.png"); AssetDatabase.CreateAsset(new Texture2D(1, 1), path); Assert.IsType<TextureImporter>(EditorAssetImporter.GetAtPath(path)); }
    [Fact] public void TextureImporter_IsStablePerPath() { var path = PathFor("stable.png"); AssetDatabase.CreateAsset(new Texture2D(1, 1), path); Assert.Same(TextureImporter.GetAtPath(path), TextureImporter.GetAtPath(path)); }
    [Fact] public void GetImporters_ReturnsSingleRegisteredImporter() { var path = PathFor("all.png"); AssetDatabase.CreateAsset(new Texture2D(1, 1), path); Assert.Single(AssetDatabase.GetImporters(path)); }
    [Fact] public void GenericGetImporters_ReturnsMatchingType() { var path = PathFor("typed.png"); AssetDatabase.CreateAsset(new Texture2D(1, 1), path); Assert.Single(AssetDatabase.GetImporters<TextureImporter>(path)); }
    [Fact] public void GenericGetImporters_RejectsWrongType() { var path = PathFor("wrong.png"); AssetDatabase.CreateAsset(new Texture2D(1, 1), path); Assert.Empty(AssetDatabase.GetImporters<AudioImporter>(path)); }
    [Fact] public void AudioAsset_GetsAudioImporter() { var path = PathFor("sound.wav"); AssetDatabase.CreateAsset(AudioClip.Create("a", 1, 1, 44100, false), path); Assert.IsType<AudioImporter>(EditorAssetImporter.GetAtPath(path)); }
    [Fact] public void TextAsset_GetsBaseImporter() { var path = PathFor("text.txt"); AssetDatabase.CreateAsset(new TextAsset("t"), path); Assert.IsType<EditorAssetImporter>(EditorAssetImporter.GetAtPath(path)); }
    [Fact] public void MissingAsset_ReportsMissingSettings() { var importer = EditorAssetImporter.GetAtPath(PathFor("missing.asset")); Assert.True(importer.importSettingsMissing); }
    [Fact] public void TextureSettings_PersistThroughRegistryLookup() { var path = PathFor("settings.png"); AssetDatabase.CreateAsset(new Texture2D(1, 1), path); var importer = TextureImporter.GetAtPath(path); importer.maxTextureSize = 512; Assert.Equal(512, TextureImporter.GetAtPath(path).maxTextureSize); }
    [Fact] public void ImporterAssetPath_IsCanonical() { var path = PathFor("canonical.txt"); AssetDatabase.CreateAsset(new TextAsset("x"), path); Assert.Equal(path, EditorAssetImporter.GetAtPath(path).assetPath); }

    private static string PathFor(string name) => "Assets/ImporterRegistry/" + Guid.NewGuid().ToString("N") + "/" + name;
}
