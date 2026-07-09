using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets;

/// <summary>
/// Unity Addressables system for managing asset references.
/// </summary>
public static class Addressables
{
    public static AsyncOperationHandle<T> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
    {
        return new AsyncOperationHandle<T>();
    }

    public static AsyncOperationHandle<T> LoadAssetAsync<T>(AssetReference assetReference) where T : UnityEngine.Object
    {
        return new AsyncOperationHandle<T>();
    }

    public static AsyncOperationHandle<IList<T>> LoadAssetsAsync<T>(string address, Action<T> callback) where T : UnityEngine.Object
    {
        return new AsyncOperationHandle<IList<T>>();
    }

    public static AsyncOperationHandle<GameObject> InstantiateAsync(string address)
    {
        return new AsyncOperationHandle<GameObject>();
    }

    public static AsyncOperationHandle<GameObject> InstantiateAsync(string address, Transform? parent)
    {
        return new AsyncOperationHandle<GameObject>();
    }

    public static AsyncOperationHandle<GameObject> InstantiateAsync(string address, Vector3 position, Quaternion rotation)
    {
        return new AsyncOperationHandle<GameObject>();
    }

    public static AsyncOperationHandle<GameObject> InstantiateAsync(string address, Vector3 position, Quaternion rotation, Transform? parent)
    {
        return new AsyncOperationHandle<GameObject>();
    }

    public static void Release<T>(AsyncOperationHandle<T> handle)
    {
    }

    public static void Release(UnityEngine.Object obj)
    {
    }

    public static bool ReleaseInstance(GameObject instance)
    {
        return true;
    }

    public static AsyncOperationHandle<IResourceLocator> UpdateCatalogs(IEnumerable<string> catalogs = null, bool autoReleaseHandle = true)
    {
        return new AsyncOperationHandle<IResourceLocator>();
    }

    public static AsyncOperationHandle<IResourceLocator> LoadContentCatalogAsync(string catalogPath)
    {
        return new AsyncOperationHandle<IResourceLocator>();
    }

    public static IList<IResourceLocator> InternalIdTransformFunc
    {
        get => new List<IResourceLocator>();
    }
}

/// <summary>
/// Resource locator interface.
/// </summary>
public interface IResourceLocator
{
    bool Locate(object key, Type type, out IList<object> locations);
    IEnumerable<object> Keys { get; }
}

/// <summary>
/// Asset reference for Addressables.
/// </summary>
[Serializable]
public class AssetReference
{
    public string AssetGUID;
    public string SubObjectName;

    public AssetReference()
    {
    }

    public AssetReference(string guid)
    {
        AssetGUID = guid;
    }

    public AsyncOperationHandle<T> LoadAssetAsync<T>() where T : UnityEngine.Object
    {
        return Addressables.LoadAssetAsync<T>(this);
    }

    public AsyncOperationHandle<GameObject> InstantiateAsync()
    {
        return Addressables.InstantiateAsync(AssetGUID);
    }

    public bool RuntimeKeyIsValid()
    {
        return !string.IsNullOrEmpty(AssetGUID);
    }

    public override string ToString()
    {
        return AssetGUID ?? string.Empty;
    }
}

/// <summary>
/// Asset reference runtime key.
/// </summary>
[Serializable]
public struct AssetReferenceT<T> where T : UnityEngine.Object
{
    public AssetReference Reference;

    public AsyncOperationHandle<T> LoadAssetAsync()
    {
        return Reference.LoadAssetAsync<T>();
    }
}

/// <summary>
/// Async operation handle for Addressables.
/// </summary>
public struct AsyncOperationHandle<T>
{
    private T? _result;
    private bool _isDone;
    private float _percentComplete;
    private Exception? _operationException;

    public T Result => _result ?? default!;
    public bool IsDone => _isDone;
    public float PercentComplete => _percentComplete;
    public Exception? OperationException => _operationException;
    public bool IsValid() => true;

    public event Action<AsyncOperationHandle<T>> Completed;

    public AsyncOperationHandle<T> WaitForCompletion()
    {
        _isDone = true;
        _percentComplete = 1.0f;
        return this;
    }
}
