namespace UnityEditor;

public static class ProjectSettings
{
    public static T GetSingleton<T>() where T : class, new() => new T();
    public static void SetSingleton<T>(T singleton) where T : class { _ = singleton; }

    public static string projectPath => string.Empty;
    public static string[] availableProjectPaths => System.Array.Empty<string>();

    public static bool InitializeOnStartup(string projectPath) { _ = projectPath; return true; }
    public static bool TryGetSetting<T>(string key, out T value) { _ = key; value = default!; return false; }
    public static void SetSetting<T>(string key, T value) { _ = key; _ = value; }
    public static void DeleteSetting(string key) { _ = key; }
}
