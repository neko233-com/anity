namespace UnityEditor;

public static class AssemblyReloadEvents
{
    public delegate void AssemblyReloadCallback();

    public static event AssemblyReloadCallback? beforeAssemblyReload;
    public static event AssemblyReloadCallback? afterAssemblyReload;

    internal static void OnBeforeAssemblyReload() => beforeAssemblyReload?.Invoke();

    internal static void OnAfterAssemblyReload() => afterAssemblyReload?.Invoke();
}
