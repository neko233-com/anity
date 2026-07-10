namespace UnityEngine;

/// <summary>
/// AssetBundle loading and management.
/// </summary>
public class AssetBundle : Object
{
    public bool isStreamedSceneAssetBundle { get; protected set; }
    public uint crc { get; protected set; }

    public static AssetBundle? LoadFromFile(string path)
    {
        _ = path;
        return null;
    }

    public static AssetBundle? LoadFromFile(string path, uint crc)
    {
        _ = path;
        _ = crc;
        return null;
    }

    public static AssetBundle? LoadFromFile(string path, uint crc, ulong offset)
    {
        _ = path;
        _ = crc;
        _ = offset;
        return null;
    }

    public static AssetBundle? LoadFromMemory(byte[] binary)
    {
        _ = binary;
        return null;
    }

    public static AssetBundle? LoadFromMemory(byte[] binary, uint crc)
    {
        _ = binary;
        _ = crc;
        return null;
    }

    public static AssetBundleCreateRequest LoadFromMemoryAsync(byte[] binary)
    {
        _ = binary;
        return new AssetBundleCreateRequest();
    }

    public static AssetBundleCreateRequest LoadFromMemoryAsync(byte[] binary, uint crc)
    {
        _ = binary;
        _ = crc;
        return new AssetBundleCreateRequest();
    }

    public static AssetBundleCreateRequest LoadFromFileAsync(string path)
    {
        _ = path;
        return new AssetBundleCreateRequest();
    }

    public static AssetBundleCreateRequest LoadFromFileAsync(string path, uint crc)
    {
        _ = path;
        _ = crc;
        return new AssetBundleCreateRequest();
    }

    public static AssetBundleCreateRequest LoadFromFileAsync(string path, uint crc, ulong offset)
    {
        _ = path;
        _ = crc;
        _ = offset;
        return new AssetBundleCreateRequest();
    }

    public static AssetBundle[] GetAllLoadedAssetBundles() => Array.Empty<AssetBundle>();

    public static void UnloadAllAssetBundles(bool unloadAllObjects) { }

    public Object? LoadAsset(string name)
    {
        _ = name;
        return null;
    }

    public T? LoadAsset<T>(string name) where T : Object
    {
        _ = name;
        return null;
    }

    public Object? LoadAsset(string name, Type type)
    {
        _ = name;
        _ = type;
        return null;
    }

    public AssetBundleRequest LoadAssetAsync(string name)
    {
        _ = name;
        return new AssetBundleRequest();
    }

    public AssetBundleRequest LoadAssetAsync<T>(string name) where T : Object
    {
        _ = name;
        return new AssetBundleRequest();
    }

    public AssetBundleRequest LoadAssetAsync(string name, Type type)
    {
        _ = name;
        _ = type;
        return new AssetBundleRequest();
    }

    public Object[] LoadAllAssets() => Array.Empty<Object>();
    public T[] LoadAllAssets<T>() where T : Object => Array.Empty<T>();
    public Object[] LoadAllAssets(Type type)
    {
        _ = type;
        return Array.Empty<Object>();
    }

    public AssetBundleRequest LoadAllAssetsAsync() => new AssetBundleRequest();
    public AssetBundleRequest LoadAllAssetsAsync<T>() where T : Object => new AssetBundleRequest();
    public AssetBundleRequest LoadAllAssetsAsync(Type type)
    {
        _ = type;
        return new AssetBundleRequest();
    }

    public string[] GetAllAssetNames() => Array.Empty<string>();
    public string[] GetAllScenePaths() => Array.Empty<string>();

    public bool Contains(string name)
    {
        _ = name;
        return false;
    }

    public void Unload(bool unloadAllLoadedObjects) { }
    public void UnloadAsync(bool unloadAllLoadedObjects) { }
}

public class AssetBundleCreateRequest : AsyncOperation
{
    public AssetBundle? assetBundle => null;
}

public class AssetBundleRequest : AsyncOperation
{
    public Object? asset => null;
    public T? assetAsTyped<T>() where T : Object => null;
    public Object[]? allAssets => Array.Empty<Object>();
}
