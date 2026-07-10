namespace UnityEditor;

public static class EditorSettings
{
    public static string unityRemoteDevice { get; set; } = string.Empty;
    public static string unityRemoteCompression { get; set; } = string.Empty;
    public static string unityRemoteResolution { get; set; } = string.Empty;
    public static string unityRemoteJoystickSource { get; set; } = string.Empty;

    public static bool enterPlayModeOptionsEnabled { get; set; }
    public static EnterPlayModeOptions enterPlayModeOptions { get; set; }

    public static bool prefabModeAllowAutoSave { get; set; } = true;
    public static bool asyncShaderCompilation { get; set; } = true;
    public static bool cachingShaderPreprocessor { get; set; }

    public static bool useLegacyProbeSampleCount { get; set; }
    public static bool enableCookiesInLightmapper { get; set; } = true;

    public static bool useEnterPlayModeOptions { get; set; }
    public static bool restartEditorAfterCompile { get; set; }

    public static void SetConfigValue(string name, string value) { _ = name; _ = value; }
    public static string GetConfigValue(string name) { _ = name; return string.Empty; }
}

[System.Flags]
public enum EnterPlayModeOptions
{
    None = 0,
    DisableDomainReload = 1,
    DisableSceneReload = 2
}
