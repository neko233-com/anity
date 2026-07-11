using System.Collections.Generic;

namespace UnityEditor;

public static class EditorSettings
{
    private static readonly Dictionary<string, object> _settings = new();

    public static bool SerializeInlineVariablesOnOneLine
    {
        get => GetValue<bool>(nameof(SerializeInlineVariablesOnOneLine));
        set => SetValue(nameof(SerializeInlineVariablesOnOneLine), value);
    }

    public static bool bakeCollisionMeshes
    {
        get => GetValue<bool>(nameof(bakeCollisionMeshes));
        set => SetValue(nameof(bakeCollisionMeshes), value);
    }

    public static bool useLegacyProbeVolumes
    {
        get => GetValue<bool>(nameof(useLegacyProbeVolumes));
        set => SetValue(nameof(useLegacyProbeVolumes), value);
    }

    public static string unityRemoteDevice { get; set; } = string.Empty;
    public static string unityRemoteCompression { get; set; } = string.Empty;
    public static string unityRemoteResolution { get; set; } = string.Empty;
    public static string unityRemoteJoystickSource { get; set; } = string.Empty;

    public static bool enterPlayModeOptionsEnabled { get; set; }
    public static EnterPlayModeOptions enterPlayModeOptions { get; set; }

    public static string projectGenerationIncludedExtensions
    {
        get => GetValue<string>(nameof(projectGenerationIncludedExtensions));
        set => SetValue(nameof(projectGenerationIncludedExtensions), value);
    }

    public static string projectGenerationRootNamespace
    {
        get => GetValue<string>(nameof(projectGenerationRootNamespace));
        set => SetValue(nameof(projectGenerationRootNamespace), value);
    }

    public static string cacheServerEndpoint
    {
        get => GetValue<string>(nameof(cacheServerEndpoint));
        set => SetValue(nameof(cacheServerEndpoint), value);
    }

    public static bool cacheServerEnableDownload
    {
        get => GetValue<bool>(nameof(cacheServerEnableDownload), true);
        set => SetValue(nameof(cacheServerEnableDownload), value);
    }

    public static bool cacheServerEnableUpload
    {
        get => GetValue<bool>(nameof(cacheServerEnableUpload), true);
        set => SetValue(nameof(cacheServerEnableUpload), value);
    }

    public static CacheServerMode cacheServerMode
    {
        get => GetValue<CacheServerMode>(nameof(cacheServerMode));
        set => SetValue(nameof(cacheServerMode), value);
    }

    public static SerializationMode serializationMode
    {
        get => GetValue<SerializationMode>(nameof(serializationMode), SerializationMode.ForceText);
        set => SetValue(nameof(serializationMode), value);
    }

    public static EditorBehaviorMode defaultBehaviorMode
    {
        get => GetValue<EditorBehaviorMode>(nameof(defaultBehaviorMode), EditorBehaviorMode.Mode3D);
        set => SetValue(nameof(defaultBehaviorMode), value);
    }

    public static BuildCompression defaultBundleCompression
    {
        get => GetValue<BuildCompression>(nameof(defaultBundleCompression));
        set => SetValue(nameof(defaultBundleCompression), value);
    }

    public static int defaultMaxTextureSize
    {
        get => GetValue<int>(nameof(defaultMaxTextureSize), 2048);
        set => SetValue(nameof(defaultMaxTextureSize), value);
    }

    public static TextureImporterCompression defaultTextureCompression
    {
        get => GetValue<TextureImporterCompression>(nameof(defaultTextureCompression));
        set => SetValue(nameof(defaultTextureCompression), value);
    }

    public static bool inspectorUseIMGUIDefaultInspector
    {
        get => GetValue<bool>(nameof(inspectorUseIMGUIDefaultInspector));
        set => SetValue(nameof(inspectorUseIMGUIDefaultInspector), value);
    }

    public static SpritePackerMode spritePackerMode
    {
        get => GetValue<SpritePackerMode>(nameof(spritePackerMode));
        set => SetValue(nameof(spritePackerMode), value);
    }

    public static TextureCompressionQuality etcTextureCompressor
    {
        get => GetValue<TextureCompressionQuality>(nameof(etcTextureCompressor), TextureCompressionQuality.Normal);
        set => SetValue(nameof(etcTextureCompressor), value);
    }

    public static TextureCompressionQuality pvrtcTextureCompressor
    {
        get => GetValue<TextureCompressionQuality>(nameof(pvrtcTextureCompressor), TextureCompressionQuality.Normal);
        set => SetValue(nameof(pvrtcTextureCompressor), value);
    }

    public static TextureCompressionQuality bcTextureCompressor
    {
        get => GetValue<TextureCompressionQuality>(nameof(bcTextureCompressor), TextureCompressionQuality.Normal);
        set => SetValue(nameof(bcTextureCompressor), value);
    }

    public static TextureCompressionQuality atcTextureCompressor
    {
        get => GetValue<TextureCompressionQuality>(nameof(atcTextureCompressor), TextureCompressionQuality.Normal);
        set => SetValue(nameof(atcTextureCompressor), value);
    }

    public static bool etcTextureFastCompressor
    {
        get => GetValue<bool>(nameof(etcTextureFastCompressor), true);
        set => SetValue(nameof(etcTextureFastCompressor), value);
    }

    public static bool pvrtcTextureFastCompressor
    {
        get => GetValue<bool>(nameof(pvrtcTextureFastCompressor), true);
        set => SetValue(nameof(pvrtcTextureFastCompressor), value);
    }

    public static bool bcTextureFastCompressor
    {
        get => GetValue<bool>(nameof(bcTextureFastCompressor), true);
        set => SetValue(nameof(bcTextureFastCompressor), value);
    }

    public static bool atcTextureFastCompressor
    {
        get => GetValue<bool>(nameof(atcTextureFastCompressor), true);
        set => SetValue(nameof(atcTextureFastCompressor), value);
    }

    public static bool prefabModeAllowAutoSave { get; set; } = true;
    public static bool asyncShaderCompilation { get; set; } = true;
    public static bool cachingShaderPreprocessor { get; set; }

    public static int gameObjectNamingDigits
    {
        get => GetValue<int>(nameof(gameObjectNamingDigits));
        set => SetValue(nameof(gameObjectNamingDigits), value);
    }

    public static GameObjectNamingScheme gameObjectNamingScheme
    {
        get => GetValue<GameObjectNamingScheme>(nameof(gameObjectNamingScheme));
        set => SetValue(nameof(gameObjectNamingScheme), value);
    }

    public static bool SerializedFieldCanBeEmpty
    {
        get => GetValue<bool>(nameof(SerializedFieldCanBeEmpty));
        set => SetValue(nameof(SerializedFieldCanBeEmpty), value);
    }

    public static Object prefabRegularEnvironment
    {
        get => GetValue<Object>(nameof(prefabRegularEnvironment));
        set => SetValue(nameof(prefabRegularEnvironment), value);
    }

    public static Object prefabUIEnvironment
    {
        get => GetValue<Object>(nameof(prefabUIEnvironment));
        set => SetValue(nameof(prefabUIEnvironment), value);
    }

    public static string DefineSymbols
    {
        get => GetValue<string>(nameof(DefineSymbols));
        set => SetValue(nameof(DefineSymbols), value);
    }

    public static bool enableTextureStreamingInEditMode
    {
        get => GetValue<bool>(nameof(enableTextureStreamingInEditMode));
        set => SetValue(nameof(enableTextureStreamingInEditMode), value);
    }

    public static bool enableTextureStreamingInPlayMode
    {
        get => GetValue<bool>(nameof(enableTextureStreamingInPlayMode));
        set => SetValue(nameof(enableTextureStreamingInPlayMode), value);
    }

    public static bool allowNestingInPrefabMode
    {
        get => GetValue<bool>(nameof(allowNestingInPrefabMode), true);
        set => SetValue(nameof(allowNestingInPrefabMode), value);
    }

    public static bool useRoslynForAssetSerialisation
    {
        get => GetValue<bool>(nameof(useRoslynForAssetSerialisation));
        set => SetValue(nameof(useRoslynForAssetSerialisation), value);
    }

    public static bool ShowLightmapResolutionOverlay
    {
        get => GetValue<bool>(nameof(ShowLightmapResolutionOverlay));
        set => SetValue(nameof(ShowLightmapResolutionOverlay), value);
    }

    public static bool disableCookiesInLightmapper { get; set; } = true;

    public static bool openUIPropertyDrawerAttribute
    {
        get => GetValue<bool>(nameof(openUIPropertyDrawerAttribute));
        set => SetValue(nameof(openUIPropertyDrawerAttribute), value);
    }

    public static bool useLegacyProbeSampleCount { get; set; }
    public static bool useEnterPlayModeOptions { get; set; }
    public static bool restartEditorAfterCompile { get; set; }

    private static T GetValue<T>(string key, T defaultValue = default!)
    {
        if (_settings.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return defaultValue;
    }

    private static void SetValue<T>(string key, T value)
    {
        _settings[key] = value!;
    }

    public static void SetConfigValue(string name, string value)
    {
        _settings[name] = value;
    }

    public static string GetConfigValue(string name)
    {
        if (_settings.TryGetValue(name, out var value) && value is string strValue)
            return strValue;
        return string.Empty;
    }
}

[System.Flags]
public enum EnterPlayModeOptions
{
    None = 0,
    DisableDomainReload = 1,
    DisableSceneReload = 2
}

public enum CacheServerMode
{
    Disabled,
    Enabled,
    OverridePreferences
}

public enum SerializationMode
{
    Mixed = 0,
    ForceBinary = 1,
    ForceText = 2
}

public enum EditorBehaviorMode
{
    Mode3D,
    Mode2D
}

public enum SpritePackerMode
{
    Disabled,
    BuildTimeOnly,
    AlwaysOn
}

public enum TextureCompressionQuality
{
    Fast,
    Normal,
    Best
}

public enum GameObjectNamingScheme
{
    Space,
    Period,
    Dash,
    Underscore
}

public enum BuildCompression
{
    LZ4,
    LZ4HC,
    Uncompressed
}
