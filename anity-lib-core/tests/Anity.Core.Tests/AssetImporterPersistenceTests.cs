using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Xunit;
using EditorAssetImporter = UnityEditor.AssetImporter;
using EditorAudioClipLoadType = UnityEditor.AudioClipLoadType;

namespace Anity.Core.Tests;

/// <summary>Importer settings survive a real project session reset through the asset .meta file.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetImporterPersistenceTests : IDisposable
{
    private const string SettingsPrefix = "# ANITY_IMPORTER_SETTINGS: ";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-importer-settings-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetImporterPersistenceTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact] public void WriteImportSettings_PersistsMetadata() { var path = TrackText("write.txt"); Assert.True(AssetDatabase.WriteImportSettingsIfDirty(path)); Assert.Contains(SettingsPrefix, File.ReadAllText(FullPath(path) + ".meta")); }
    [Fact] public void WriteImportSettings_PreservesExistingUnityYaml() { const string guid = "0123456789abcdef0123456789abcdef"; var path = TrackText("yaml.txt"); File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n"); AssetDatabase.WriteImportSettingsIfDirty(path); var meta = File.ReadAllText(FullPath(path) + ".meta"); Assert.Contains("fileFormatVersion: 2", meta); Assert.Contains("guid: " + guid, meta); }
    [Fact] public void WriteImportSettings_ReplacesPriorPayloadInsteadOfAppending() { var path = TrackText("once.txt"); AssetDatabase.WriteImportSettingsIfDirty(path); AssetDatabase.WriteImportSettingsIfDirty(path); Assert.Single(File.ReadAllLines(FullPath(path) + ".meta").Where(line => line.StartsWith(SettingsPrefix, StringComparison.Ordinal))); }
    [Fact] public void WriteImportSettings_ReturnsFalseForMissingFile() { Assert.False(AssetDatabase.WriteImportSettingsIfDirty("Assets/Missing/nope.txt")); }
    [Fact] public void SaveSettings_PersistsBaseImporterDataAcrossSession() { var path = TrackText("base.txt"); var importer = EditorAssetImporter.GetAtPath(path); importer.editorUserSettingsData = "editor-data"; importer.SaveSettings(); RestartProject(); AssetDatabase.ImportAsset(path); Assert.Equal("editor-data", EditorAssetImporter.GetAtPath(path).editorUserSettingsData); }
    [Fact] public void SaveAndReimport_PersistsAndRefreshesTextAsset() { var path = TrackText("reimport.txt", "old"); var importer = EditorAssetImporter.GetAtPath(path); importer.editorUserSettingsData = "saved"; File.WriteAllText(FullPath(path), "new"); importer.SaveAndReimport(); Assert.Equal("new", AssetDatabase.LoadAssetAtPath<TextAsset>(path)!.text); RestartProject(); AssetDatabase.ImportAsset(path); Assert.Equal("saved", EditorAssetImporter.GetAtPath(path).editorUserSettingsData); }
    [Fact] public void TextureImporter_PersistsCoreSettingsAcrossSession() { var path = TrackTexture("core.png"); var importer = TextureImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.mipmapEnabled = false; importer.readable = true; importer.maxTextureSize = 512; importer.SaveSettings(); RestartProject(); AssetDatabase.ImportAsset(path); var restored = TextureImporter.GetAtPath(path); Assert.Equal(TextureImporterType.Sprite, restored.textureType); Assert.False(restored.mipmapEnabled); Assert.True(restored.readable); Assert.Equal(512, restored.maxTextureSize); }
    [Fact] public void TextureImporter_PersistsSamplingSettingsAcrossSession() { var path = TrackTexture("sampling.png"); var importer = TextureImporter.GetAtPath(path); importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp; importer.wrapModeU = TextureWrapMode.Mirror; importer.wrapModeV = TextureWrapMode.MirrorOnce; importer.anisoLevel = 7; importer.SaveSettings(); RestartProject(); AssetDatabase.ImportAsset(path); var restored = TextureImporter.GetAtPath(path); Assert.Equal(FilterMode.Point, restored.filterMode); Assert.Equal(TextureWrapMode.Clamp, restored.wrapMode); Assert.Equal(TextureWrapMode.Mirror, restored.wrapModeU); Assert.Equal(TextureWrapMode.MirrorOnce, restored.wrapModeV); Assert.Equal(7, restored.anisoLevel); }
    [Fact] public void AudioImporter_PersistsSampleSettingsAcrossSession() { var path = TrackAudio("sample.wav"); var importer = AudioImporter.GetAtPath(path); importer.loadInBackground = true; importer.forceToMono = true; importer.defaultSampleSettings = new AudioImporterSampleSettings { compressionFormat = AudioCompressionFormat.ADPCM, quality = .25f, loadType = EditorAudioClipLoadType.Streaming, sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate, sampleRateOverride = 22050 }; importer.SaveSettings(); RestartProject(); AssetDatabase.ImportAsset(path); var restored = AudioImporter.GetAtPath(path); Assert.True(restored.loadInBackground); Assert.True(restored.forceToMono); Assert.Equal(AudioCompressionFormat.ADPCM, restored.defaultSampleSettings.compressionFormat); Assert.Equal(.25f, restored.defaultSampleSettings.quality); Assert.Equal(EditorAudioClipLoadType.Streaming, restored.defaultSampleSettings.loadType); Assert.Equal((uint)22050, restored.defaultSampleSettings.sampleRateOverride); }
    [Fact] public void InvalidPersistencePayload_IsIgnoredWithoutBlockingImport() { var path = TrackText("bad.txt"); File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\n" + SettingsPrefix + "not-base64\n"); RestartProject(); AssetDatabase.ImportAsset(path); Assert.NotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(path)); Assert.Equal(string.Empty, EditorAssetImporter.GetAtPath(path).editorUserSettingsData); }
    [Fact] public void UnityYamlOnly_RecoversAssetBundleAssignment() { var path = TrackText("yaml-bundle.txt"); File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nassetBundleName: characters\nassetBundleVariant: hd\n"); RestartProject(); AssetDatabase.ImportAsset(path); Assert.Equal("characters", AssetDatabase.GetAssetBundleName(path)); Assert.Equal("hd", AssetDatabase.GetAssetBundleVariant(path)); }
    [Fact] public void UnityYamlOnly_NameWithoutVariant_UsesEmptyVariant() { var path = TrackText("yaml-name.txt"); WriteYamlMeta(path, "ui"); RestartProject(); AssetDatabase.ImportAsset(path); Assert.Equal("ui", AssetDatabase.GetAssetBundleName(path)); Assert.Equal(string.Empty, AssetDatabase.GetAssetBundleVariant(path)); }
    [Fact] public void UnityYamlOnly_CrlfMetadata_IsRead() { var path = TrackText("yaml-crlf.txt"); File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\r\nguid: 0123456789abcdef0123456789abcdef\r\nassetBundleName: crlf\r\nassetBundleVariant: mobile\r\n"); RestartProject(); AssetDatabase.ImportAsset(path); Assert.Equal("crlf", AssetDatabase.GetAssetBundleName(path)); Assert.Equal("mobile", AssetDatabase.GetAssetBundleVariant(path)); }
    [Fact] public void UnityYamlOnly_TrimmedScalars_AreRead() { var path = TrackText("yaml-trim.txt"); File.WriteAllText(FullPath(path) + ".meta", "assetBundleName:  trimmed  \nassetBundleVariant:  hd  \n"); RestartProject(); AssetDatabase.ImportAsset(path); Assert.Equal("trimmed", AssetDatabase.GetAssetBundleName(path)); Assert.Equal("hd", AssetDatabase.GetAssetBundleVariant(path)); }
    [Fact] public void UnityYamlOnly_SingleQuotedScalar_IsRead() { var path = TrackText("yaml-single-quote.txt"); File.WriteAllText(FullPath(path) + ".meta", "assetBundleName: 'ui''s'\nassetBundleVariant: 'hd'\n"); RestartProject(); AssetDatabase.ImportAsset(path); Assert.Equal("ui's", AssetDatabase.GetAssetBundleName(path)); Assert.Equal("hd", AssetDatabase.GetAssetBundleVariant(path)); }
    [Fact] public void UnityYamlOnly_DoubleQuotedScalar_IsRead() { var path = TrackText("yaml-double-quote.txt"); File.WriteAllText(FullPath(path) + ".meta", "assetBundleName: \"ui bundle\"\nassetBundleVariant: \"hd\"\n"); RestartProject(); AssetDatabase.ImportAsset(path); Assert.Equal("ui bundle", AssetDatabase.GetAssetBundleName(path)); Assert.Equal("hd", AssetDatabase.GetAssetBundleVariant(path)); }
    [Fact] public void UnityYaml_MetadataOverridesLegacyPayloadAssignment() { var path = TrackText("yaml-authoritative.txt"); var importer = EditorAssetImporter.GetAtPath(path); importer.assetBundleName = "legacy"; importer.assetBundleVariant = "old"; importer.SaveSettings(); var meta = File.ReadAllText(FullPath(path) + ".meta").Replace("assetBundleName: legacy", "assetBundleName: yaml").Replace("assetBundleVariant: old", "assetBundleVariant: current"); File.WriteAllText(FullPath(path) + ".meta", meta); RestartProject(); AssetDatabase.ImportAsset(path); Assert.Equal("yaml", AssetDatabase.GetAssetBundleName(path)); Assert.Equal("current", AssetDatabase.GetAssetBundleVariant(path)); }
    [Fact] public void UnityYaml_InvalidLegacyPayloadStillRestoresAssignment() { var path = TrackText("yaml-invalid-payload.txt"); File.WriteAllText(FullPath(path) + ".meta", "assetBundleName: yaml\nassetBundleVariant: safe\n" + SettingsPrefix + "not-base64\n"); RestartProject(); AssetDatabase.ImportAsset(path); Assert.Equal("yaml", AssetDatabase.GetAssetBundleName(path)); Assert.Equal("safe", AssetDatabase.GetAssetBundleVariant(path)); }
    [Fact] public void UnityYaml_MissingFieldsClearsExistingAssignment() { var path = TrackText("yaml-clear.txt"); AssetDatabase.SetAssetBundleNameAndVariant(path, "old", "hd"); File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n"); AssetDatabase.ImportAsset(path); Assert.Equal(string.Empty, AssetDatabase.GetAssetBundleName(path)); Assert.Equal(string.Empty, AssetDatabase.GetAssetBundleVariant(path)); }
    [Fact] public void WriteImportSettings_EmitsUnityYamlBundleFields() { var path = TrackText("write-yaml.txt"); AssetDatabase.SetAssetBundleNameAndVariant(path, "characters", "hd"); Assert.True(AssetDatabase.WriteImportSettingsIfDirty(path)); var lines = File.ReadAllLines(FullPath(path) + ".meta"); Assert.Contains("DefaultImporter:", lines); Assert.Contains("  assetBundleName: characters", lines); Assert.Contains("  assetBundleVariant: hd", lines); }
    [Fact] public void WriteImportSettings_ReplacesStaleUnityYamlBundleFields() { var path = TrackText("replace-yaml.txt"); File.WriteAllText(FullPath(path) + ".meta", "assetBundleName: stale\nassetBundleVariant: old\nassetBundleName: duplicate\nassetBundleVariant: duplicate\n"); AssetDatabase.SetAssetBundleNameAndVariant(path, "fresh", "new"); Assert.True(AssetDatabase.WriteImportSettingsIfDirty(path)); var lines = File.ReadAllLines(FullPath(path) + ".meta"); Assert.Equal(new[] { "  assetBundleName: fresh" }, lines.Where(line => line.TrimStart().StartsWith("assetBundleName:", StringComparison.Ordinal))); Assert.Equal(new[] { "  assetBundleVariant: new" }, lines.Where(line => line.TrimStart().StartsWith("assetBundleVariant:", StringComparison.Ordinal))); }
    [Fact] public void UnityYamlImportThenSave_RetainsSingleAuthoritativeAssignment() { var path = TrackText("yaml-save.txt"); WriteYamlMeta(path, "from-yaml", "hd"); RestartProject(); AssetDatabase.ImportAsset(path); EditorAssetImporter.GetAtPath(path).SaveSettings(); var lines = File.ReadAllLines(FullPath(path) + ".meta"); Assert.Equal(new[] { "  assetBundleName: from-yaml" }, lines.Where(line => line.TrimStart().StartsWith("assetBundleName:", StringComparison.Ordinal))); Assert.Equal(new[] { "  assetBundleVariant: hd" }, lines.Where(line => line.TrimStart().StartsWith("assetBundleVariant:", StringComparison.Ordinal))); }

    private string TrackText(string name, string contents = "text")
    {
        var path = "Assets/Settings/" + Guid.NewGuid().ToString("N") + "/" + name;
        Write(path, System.Text.Encoding.UTF8.GetBytes(contents));
        AssetDatabase.CreateAsset(new TextAsset(contents), path);
        return path;
    }

    private string TrackTexture(string name)
    {
        var path = "Assets/Settings/" + Guid.NewGuid().ToString("N") + "/" + name;
        Write(path, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL8+QAAAABJRU5ErkJggg=="));
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private string TrackAudio(string name)
    {
        var path = "Assets/Settings/" + Guid.NewGuid().ToString("N") + "/" + name;
        Write(path, new byte[] { 82, 73, 70, 70, 36, 0, 0, 0, 87, 65, 86, 69, 102, 109, 116, 32, 16, 0, 0, 0, 1, 0, 1, 0, 68, 172, 0, 0, 136, 88, 1, 0, 2, 0, 16, 0, 100, 97, 116, 97, 0, 0, 0, 0 });
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private void RestartProject()
    {
        var transitionRoot = Path.Combine(_dir, "Library", "SessionTransition");
        Directory.CreateDirectory(transitionRoot);
        EditorApplication.OpenProject(transitionRoot);
        EditorApplication.OpenProject(_dir);
    }

    private void WriteYamlMeta(string path, string name, string variant = "")
    {
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nassetBundleName: " + name + "\nassetBundleVariant: " + variant + "\n");
    }

    private void Write(string path, byte[] bytes)
    {
        var fullPath = FullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, bytes);
    }

    private string FullPath(string path) => Path.Combine(_dir, path);
}
