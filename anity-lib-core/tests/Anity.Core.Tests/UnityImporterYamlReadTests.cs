using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;
using EditorAudioClipLoadType = UnityEditor.AudioClipLoadType;
using EditorSpriteMeshType = UnityEditor.SpriteMeshType;

namespace Anity.Core.Tests;

/// <summary>Reads the scalar paths emitted by Unity 2022 TextureImporter and AudioImporter metadata.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class UnityImporterYamlReadTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-unity-importer-yaml-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public UnityImporterYamlReadTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact] public void TextureYaml_ReadsTypeAndShape() { var path = ImportTexture("textureType: 8\n  textureShape: 1"); var importer = TextureImporter.GetAtPath(path); Assert.Equal(TextureImporterType.Sprite, importer.textureType); Assert.Equal(TextureImporterShape.Texture2D, importer.textureShape); }
    [Fact] public void TextureYaml_ReadsMipmapAndSrgbSettings() { var path = ImportTexture("mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 0"); var importer = TextureImporter.GetAtPath(path); Assert.False(importer.mipmapEnabled); Assert.False(importer.sRGBTexture); }
    [Fact] public void TextureYaml_ReadsReadabilityAndStreamingSettings() { var path = ImportTexture("isReadable: 1\n  streamingMipmaps: 1\n  streamingMipmapsPriority: 7"); var importer = TextureImporter.GetAtPath(path); Assert.True(importer.readable); Assert.True(importer.streamingMipmaps); Assert.Equal(7, importer.streamingMipmapsPriority); }
    [Fact] public void TextureYaml_ReadsSamplingSettings() { var path = ImportTexture("textureSettings:\n    filterMode: 0\n    aniso: 9\n    mipBias: 1.25\n    wrapU: 1\n    wrapV: 2\n    wrapW: 0"); var importer = TextureImporter.GetAtPath(path); Assert.Equal(FilterMode.Point, importer.filterMode); Assert.Equal(9, importer.anisoLevel); Assert.Equal(1.25f, importer.mipMapBias); Assert.Equal(TextureWrapMode.Clamp, importer.wrapModeU); Assert.Equal(TextureWrapMode.Mirror, importer.wrapModeV); Assert.Equal(TextureWrapMode.Repeat, importer.wrapModeW); }
    [Fact] public void TextureYaml_ReadsSizeCompressionAndNpotSettings() { var path = ImportTexture("maxTextureSize: 512\n  compressionQuality: 83\n  nPOTScale: 2"); var importer = TextureImporter.GetAtPath(path); Assert.Equal(512, importer.maxTextureSize); Assert.Equal(83, importer.compressionQuality); Assert.Equal(TextureImporterNPOTScale.ToLarger, importer.npotScale); }
    [Fact] public void TextureYaml_ReadsSpriteSettings() { var path = ImportTexture("spriteMode: 2\n  spriteMeshType: 0\n  spriteExtrude: 3\n  spritePixelsToUnits: 64"); var importer = TextureImporter.GetAtPath(path); Assert.Equal(SpriteImportMode.Multiple, importer.spriteImportMode); Assert.Equal(EditorSpriteMeshType.FullRect, importer.spriteMeshType); Assert.Equal((uint)3, importer.spriteExtrude); Assert.Equal(64f, importer.spritePixelsPerUnit); }
    [Fact] public void TextureYaml_ReadsNormalMapAndUserData() { var path = ImportTexture("userData: imported-texture\n  bumpmap:\n    convertToNormalMap: 1\n    normalMapFilter: 1"); var importer = TextureImporter.GetAtPath(path); Assert.True(importer.convertToNormalmap); Assert.Equal(TextureImporterNormalFilter.Sobel, importer.normalmapFilter); Assert.Equal("imported-texture", importer.editorUserSettingsData); }
    [Fact] public void TextureYaml_IgnoresScalarPathsWithoutTextureImporterBlock() { var path = ImportTexture("NotTextureImporter:\n    serializedVersion: 13\n    maxTextureSize: 512"); Assert.Equal(2048, TextureImporter.GetAtPath(path).maxTextureSize); }
    [Fact] public void TextureYaml_ReadsBundleAssignmentInsideImporterBlock() { var path = ImportTexture("assetBundleName: characters\n  assetBundleVariant: hd"); Assert.Equal("characters", AssetDatabase.GetAssetBundleName(path)); Assert.Equal("hd", AssetDatabase.GetAssetBundleVariant(path)); }
    [Fact] public void TextureYaml_SaveSettingsKeepsBundleFieldsInsideTextureImporterBlock() { var path = ImportTexture("maxTextureSize: 512"); AssetDatabase.SetAssetBundleNameAndVariant(path, "textures", "web"); TextureImporter.GetAtPath(path).SaveSettings(); var meta = File.ReadAllText(FullPath(path) + ".meta"); Assert.Contains("TextureImporter:", meta); Assert.Contains("  assetBundleName: textures", meta); Assert.Contains("  assetBundleVariant: web", meta); }
    [Fact] public void TextureYaml_SaveSettingsWritesTypeAndShape() { var path = ImportTexture("textureType: 0\n  textureShape: 1"); var importer = TextureImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.textureShape = TextureImporterShape.Cube; importer.SaveSettings(); var meta = Meta(path); Assert.Contains("textureType: 8", meta); Assert.Contains("textureShape: 2", meta); }
    [Fact] public void TextureYaml_SaveSettingsWritesMipmapAndSrgb() { var path = ImportTexture("mipmaps:\n    enableMipMap: 1\n    sRGBTexture: 1"); var importer = TextureImporter.GetAtPath(path); importer.mipmapEnabled = false; importer.sRGBTexture = false; importer.SaveSettings(); var meta = Meta(path); Assert.Contains("enableMipMap: 0", meta); Assert.Contains("sRGBTexture: 0", meta); }
    [Fact] public void TextureYaml_SaveSettingsWritesReadabilityAndStreaming() { var path = ImportTexture("isReadable: 0\n  streamingMipmaps: 0\n  streamingMipmapsPriority: 0"); var importer = TextureImporter.GetAtPath(path); importer.readable = true; importer.streamingMipmaps = true; importer.streamingMipmapsPriority = 8; importer.SaveSettings(); var meta = Meta(path); Assert.Contains("isReadable: 1", meta); Assert.Contains("streamingMipmaps: 1", meta); Assert.Contains("streamingMipmapsPriority: 8", meta); }
    [Fact] public void TextureYaml_SaveSettingsWritesSamplingSettings() { var path = ImportTexture("textureSettings:\n    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 0\n    wrapV: 0\n    wrapW: 0"); var importer = TextureImporter.GetAtPath(path); importer.filterMode = FilterMode.Point; importer.anisoLevel = 6; importer.mipMapBias = .75f; importer.wrapModeU = TextureWrapMode.Clamp; importer.wrapModeV = TextureWrapMode.Mirror; importer.wrapModeW = TextureWrapMode.MirrorOnce; importer.SaveSettings(); var meta = Meta(path); Assert.Contains("filterMode: 0", meta); Assert.Contains("aniso: 6", meta); Assert.Contains("mipBias: 0.75", meta); Assert.Contains("wrapU: 1", meta); Assert.Contains("wrapV: 2", meta); Assert.Contains("wrapW: 3", meta); }
    [Fact] public void TextureYaml_SaveSettingsWritesSizeCompressionAndNpot() { var path = ImportTexture("maxTextureSize: 2048\n  compressionQuality: 50\n  nPOTScale: 0"); var importer = TextureImporter.GetAtPath(path); importer.maxTextureSize = 512; importer.compressionQuality = 87; importer.npotScale = TextureImporterNPOTScale.ToSmaller; importer.SaveSettings(); var meta = Meta(path); Assert.Contains("maxTextureSize: 512", meta); Assert.Contains("compressionQuality: 87", meta); Assert.Contains("nPOTScale: 3", meta); }
    [Fact] public void TextureYaml_SaveSettingsWritesSpriteSettings() { var path = ImportTexture("spriteMode: 1\n  spriteMeshType: 1\n  spriteExtrude: 1\n  spritePixelsToUnits: 100"); var importer = TextureImporter.GetAtPath(path); importer.spriteImportMode = SpriteImportMode.Multiple; importer.spriteMeshType = EditorSpriteMeshType.FullRect; importer.spriteExtrude = 4; importer.spritePixelsPerUnit = 48; importer.SaveSettings(); var meta = Meta(path); Assert.Contains("spriteMode: 2", meta); Assert.Contains("spriteMeshType: 0", meta); Assert.Contains("spriteExtrude: 4", meta); Assert.Contains("spritePixelsToUnits: 48", meta); }
    [Fact] public void TextureYaml_SaveSettingsWritesNormalMapAndQuotedUserData() { var path = ImportTexture("userData: old\n  bumpmap:\n    convertToNormalMap: 0\n    normalMapFilter: 0"); var importer = TextureImporter.GetAtPath(path); importer.convertToNormalmap = true; importer.normalmapFilter = TextureImporterNormalFilter.Sobel; importer.editorUserSettingsData = "artist note: yes"; importer.SaveSettings(); var meta = Meta(path); Assert.Contains("convertToNormalMap: 1", meta); Assert.Contains("normalMapFilter: 1", meta); Assert.Contains("userData: \"artist note: yes\"", meta); }
    [Fact] public void TextureYaml_SaveSettingsPreservesUnknownScalarAndDoesNotInventMissingSettings() { var path = ImportTexture("maxTextureSize: 2048\n  unknownFutureField: 17"); var importer = TextureImporter.GetAtPath(path); importer.maxTextureSize = 256; importer.SaveSettings(); var meta = Meta(path); Assert.Contains("unknownFutureField: 17", meta); Assert.DoesNotContain("isReadable:", meta); }
    [Theory]
    [InlineData("DefaultTexturePlatform", 2048, -1, 1, 50, 0, 0, 0, 0)]
    [InlineData("Standalone", 1024, 50, 2, 75, 1, 1, 0, 0)]
    [InlineData("Android", 512, 70, 3, 90, 1, 0, 1, 2)]
    [InlineData("iPhone", 256, 81, 1, 35, 1, 1, 1, 3)]
    [InlineData("WebGL", 1024, 62, 0, 10, 0, 0, 0, 0)]
    [InlineData("tvOS", 4096, 78, 2, 100, 1, 1, 1, 1)]
    [InlineData("PS4", 128, 10, 1, 40, 0, 1, 0, 0)]
    [InlineData("XboxOne", 64, 12, 3, 60, 1, 0, 1, 2)]
    [InlineData("Switch", 2048, 60, 2, 25, 1, 0, 0, 3)]
    [InlineData("VisionOS", 512, 83, 1, 55, 0, 1, 1, 1)]
    public void TextureYaml_ReadsPlatformSettings(string target, int maxSize, int format, int compression, int quality, int overridden, int crunched, int alphaSplit, int fallback) { var path = ImportTexturePlatform(target, maxSize, format, compression, quality, overridden, crunched, alphaSplit, fallback); var settings = TextureImporter.GetAtPath(path).GetPlatformTextureSettings(target); Assert.Equal(maxSize, settings.maxTextureSize); Assert.Equal((TextureImporterFormat)format, settings.format); Assert.Equal((TextureImporterCompression)compression, settings.textureCompression); Assert.Equal(quality, settings.compressionQuality); Assert.Equal(overridden == 1, settings.overridden); Assert.Equal(crunched == 1, settings.crunchedCompression); Assert.Equal(alphaSplit == 1, settings.allowsAlphaSplitting); Assert.Equal((AndroidETC2FallbackOverride)fallback, settings.androidETC2FallbackOverride); }
    [Fact] public void TextureYaml_SaveSettingsWritesExistingPlatformSettingAndPreservesUnknownFields() { var path = ImportTexturePlatform("Android", 2048, 50, 1, 50, 1, 0, 0, 0); var importer = TextureImporter.GetAtPath(path); importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", maxTextureSize = 512, format = TextureImporterFormat.ASTC_4x4, textureCompression = TextureImporterCompression.CompressedHQ, compressionQuality = 88, overridden = true, crunchedCompression = true, allowsAlphaSplitting = true, androidETC2FallbackOverride = AndroidETC2FallbackOverride.Quality16Bit }); importer.SaveSettings(); var meta = Meta(path); Assert.Contains("resizeAlgorithm: 0", meta); Assert.Contains("maxTextureSize: 512", meta); Assert.Contains("textureFormat: 70", meta); Assert.Contains("textureCompression: 2", meta); Assert.Contains("compressionQuality: 88", meta); Assert.Contains("crunchedCompression: 1", meta); Assert.Contains("allowsAlphaSplitting: 1", meta); Assert.Contains("androidETC2FallbackOverride: 2", meta); }
    [Fact] public void TextureYaml_SaveSettingsAppendsMissingPlatformSetting() { var path = ImportTexturePlatform("Android", 2048, 50, 1, 50, 1, 0, 0, 0); var importer = TextureImporter.GetAtPath(path); importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "WebGL", maxTextureSize = 1024, format = TextureImporterFormat.ETC2_RGBA8, textureCompression = TextureImporterCompression.Compressed, compressionQuality = 70, overridden = true }); importer.SaveSettings(); var meta = Meta(path); Assert.Contains("buildTarget: WebGL", meta); Assert.Contains("maxTextureSize: 1024", meta); Assert.Contains("textureFormat: 62", meta); }
    [Fact] public void TextureYaml_ClearPlatformSettingDisablesExistingOverride() { var path = ImportTexturePlatform("Android", 2048, 50, 1, 50, 1, 0, 0, 0); var importer = TextureImporter.GetAtPath(path); importer.ClearPlatformTextureSettings("Android"); importer.SaveSettings(); Assert.Contains("overridden: 0", Meta(path)); }
    [Fact] public void AudioYaml_ReadsDefaultSampleSettings() { var path = ImportAudio("defaultSettings:\n    loadType: 2\n    sampleRateSetting: 2\n    sampleRateOverride: 22050\n    compressionFormat: 2\n    quality: 0.25"); var settings = AudioImporter.GetAtPath(path).defaultSampleSettings; Assert.Equal(EditorAudioClipLoadType.Streaming, settings.loadType); Assert.Equal(AudioSampleRateSetting.OverrideSampleRate, settings.sampleRateSetting); Assert.Equal((uint)22050, settings.sampleRateOverride); Assert.Equal(AudioCompressionFormat.ADPCM, settings.compressionFormat); Assert.Equal(.25f, settings.quality); }
    [Fact] public void AudioYaml_ReadsImporterFlagsAndUserData() { var path = ImportAudio("forceToMono: 1\n  normalize: 1\n  loadInBackground: 1\n  ambisonic: 1\n  userData: imported-audio\n  defaultSettings:\n    preloadAudioData: 0"); var importer = AudioImporter.GetAtPath(path); Assert.False(importer.preloadAudioData); Assert.True(importer.forceToMono); Assert.True(importer.normalize); Assert.True(importer.loadInBackground); Assert.True(importer.ambisonic); Assert.Equal("imported-audio", importer.editorUserSettingsData); }
    [Fact] public void AudioYaml_PartialDefaultSettingsPreserveOtherDefaults() { var path = ImportAudio("defaultSettings:\n    quality: 0.6"); var settings = AudioImporter.GetAtPath(path).defaultSampleSettings; Assert.Equal(.6f, settings.quality); Assert.Equal(AudioCompressionFormat.Vorbis, settings.compressionFormat); Assert.Equal((uint)44100, settings.sampleRateOverride); }
    [Fact] public void AudioYaml_SaveSettingsWritesDefaultSampleSettings() { var path = ImportAudio("defaultSettings:\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n    compressionFormat: 1\n    quality: 1\n    preloadAudioData: 1"); var importer = AudioImporter.GetAtPath(path); importer.defaultSampleSettings = new AudioImporterSampleSettings { loadType = EditorAudioClipLoadType.Streaming, sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate, sampleRateOverride = 22050, compressionFormat = AudioCompressionFormat.ADPCM, quality = .3f }; importer.preloadAudioData = false; importer.SaveSettings(); var meta = Meta(path); Assert.Contains("loadType: 2", meta); Assert.Contains("sampleRateSetting: 2", meta); Assert.Contains("sampleRateOverride: 22050", meta); Assert.Contains("compressionFormat: 2", meta); Assert.Contains("quality: 0.3", meta); Assert.Contains("preloadAudioData: 0", meta); }
    [Fact] public void AudioYaml_SaveSettingsWritesFlagsAndPreservesUnknownScalar() { var path = ImportAudio("forceToMono: 0\n  normalize: 0\n  loadInBackground: 0\n  ambisonic: 0\n  userData: old\n  futureAudioField: 9"); var importer = AudioImporter.GetAtPath(path); importer.forceToMono = true; importer.normalize = true; importer.loadInBackground = true; importer.ambisonic = true; importer.editorUserSettingsData = "mix: final"; importer.SaveSettings(); var meta = Meta(path); Assert.Contains("forceToMono: 1", meta); Assert.Contains("normalize: 1", meta); Assert.Contains("loadInBackground: 1", meta); Assert.Contains("ambisonic: 1", meta); Assert.Contains("userData: \"mix: final\"", meta); Assert.Contains("futureAudioField: 9", meta); }

    private string ImportTexture(string body)
    {
        var path = "Assets/Yaml/" + Guid.NewGuid().ToString("N") + ".png";
        Write(path, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL8+QAAAABJRU5ErkJggg=="));
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nTextureImporter:\n  serializedVersion: 13\n  " + body.Replace("\n", "\n  ") + "\n  assetBundleName: \n  assetBundleVariant: \n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private string ImportAudio(string body)
    {
        var path = "Assets/Yaml/" + Guid.NewGuid().ToString("N") + ".wav";
        Write(path, new byte[] { 82, 73, 70, 70, 36, 0, 0, 0, 87, 65, 86, 69, 102, 109, 116, 32, 16, 0, 0, 0, 1, 0, 1, 0, 68, 172, 0, 0, 136, 88, 1, 0, 2, 0, 16, 0, 100, 97, 116, 97, 0, 0, 0, 0 });
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nAudioImporter:\n  serializedVersion: 7\n  " + body.Replace("\n", "\n  ") + "\n  assetBundleName: \n  assetBundleVariant: \n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private string ImportTexturePlatform(string target, int maxSize, int format, int compression, int quality, int overridden, int crunched, int alphaSplit, int fallback)
    {
        var path = "Assets/Yaml/" + Guid.NewGuid().ToString("N") + ".png";
        Write(path, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL8+QAAAABJRU5ErkJggg=="));
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nTextureImporter:\n  serializedVersion: 13\n  platformSettings:\n  - serializedVersion: 3\n    buildTarget: " + target + "\n    maxTextureSize: " + maxSize + "\n    resizeAlgorithm: 0\n    textureFormat: " + format + "\n    textureCompression: " + compression + "\n    compressionQuality: " + quality + "\n    crunchedCompression: " + crunched + "\n    allowsAlphaSplitting: " + alphaSplit + "\n    overridden: " + overridden + "\n    androidETC2FallbackOverride: " + fallback + "\n  assetBundleName: \n  assetBundleVariant: \n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private void Write(string path, byte[] bytes)
    {
        var fullPath = FullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, bytes);
    }

    private string FullPath(string path) => Path.Combine(_dir, path);
    private string Meta(string path) => File.ReadAllText(FullPath(path) + ".meta");
}
