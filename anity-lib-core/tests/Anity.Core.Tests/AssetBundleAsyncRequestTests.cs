using System.Collections;
using System.Reflection;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class AssetBundleAsyncRequestTests : IDisposable
{
    public AssetBundleAsyncRequestTests()
    {
        Resources.Clear();
        AssetBundle.UnloadAllAssetBundles(true);
    }

    public void Dispose()
    {
        Resources.Clear();
        AssetBundle.UnloadAllAssetBundles(true);
    }

    [Fact]
    public void PublicSurfaceMatchesUnity2022RequestContracts()
    {
        Assert.Equal(typeof(AsyncOperation), typeof(ResourceRequest).BaseType);
        Assert.Equal(typeof(ResourceRequest), typeof(AssetBundleRequest).BaseType);
        Assert.Equal(typeof(AsyncOperation), typeof(AssetBundleCreateRequest).BaseType);
        Assert.Equal(typeof(AsyncOperation), typeof(AssetBundleUnloadOperation).BaseType);

        AssertPublicGetOnlyProperty(typeof(ResourceRequest), nameof(ResourceRequest.asset), typeof(UnityEngine.Object));
        AssertPublicGetOnlyProperty(typeof(AssetBundleRequest), nameof(AssetBundleRequest.asset), typeof(UnityEngine.Object));
        AssertPublicGetOnlyProperty(typeof(AssetBundleRequest), nameof(AssetBundleRequest.allAssets), typeof(UnityEngine.Object[]));
        AssertPublicGetOnlyProperty(typeof(AssetBundleCreateRequest), nameof(AssetBundleCreateRequest.assetBundle), typeof(AssetBundle));

        Assert.Null(typeof(AssetBundleRequest).GetMethod("GetAwaiter", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(AssetBundleRequest).GetProperty("IsCompleted", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(AssetBundleRequest).GetMethod("assetAsTyped", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(AssetBundleCreateRequest).GetMethod("GetAwaiter", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(AssetBundleUnloadOperation).GetMethod("GetResult", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void ProtectedResultMethodsAndNativeMetadataMatchUnity2022()
    {
        MethodInfo resourceResult = GetDeclaredResultMethod(typeof(ResourceRequest));
        MethodInfo bundleResult = GetDeclaredResultMethod(typeof(AssetBundleRequest));

        Assert.True(resourceResult.IsFamily);
        Assert.True(resourceResult.IsVirtual);
        Assert.Equal(typeof(UnityEngine.Object), resourceResult.ReturnType);
        Assert.True(bundleResult.IsFamily);
        Assert.True(bundleResult.IsVirtual);
        Assert.Equal(typeof(ResourceRequest), bundleResult.GetBaseDefinition().DeclaringType);

        AssertRequiredByNativeCode(typeof(ResourceRequest));
        AssertNativeHeader(typeof(AssetBundleRequest), "Modules/AssetBundle/Public/AssetBundleLoadAssetOperation.h");
        AssertNativeHeader(typeof(AssetBundleCreateRequest), "Modules/AssetBundle/Public/AssetBundleLoadFromAsyncOperation.h");
        AssertNativeHeader(typeof(AssetBundleUnloadOperation), "Modules/AssetBundle/Public/AssetBundleUnloadOperation.h");
        AssertRequiredByNativeCode(typeof(AssetBundleRequest));
        AssertRequiredByNativeCode(typeof(AssetBundleCreateRequest));
        AssertRequiredByNativeCode(typeof(AssetBundleUnloadOperation));
        AssertAttributeString(bundleResult, "UnityEngine.Bindings.NativeMethodAttribute", "GetLoadedAsset");

        MethodInfo wait = typeof(AssetBundleUnloadOperation).GetMethod(nameof(AssetBundleUnloadOperation.WaitForCompletion))!;
        AssertAttributeString(wait, "UnityEngine.Bindings.NativeMethodAttribute", "WaitForCompletion");
    }

    [Fact]
    public void ResourceRequestStartsPendingWhileAssetGetterLoadsSynchronously()
    {
        var asset = new TextAsset("resource-payload");
        Resources.RegisterAsset("request/resource", asset);

        ResourceRequest request = Resources.LoadAsync<TextAsset>("request/resource");

        Assert.False(request.isDone);
        Assert.Equal(0f, request.progress);
        Assert.Same(asset, request.asset);
        Assert.False(request.isDone);
        Assert.Equal(0f, request.progress);
    }

    [Fact]
    public void ResourceRequestCompletesInPlayerLoopAndLateCallbackRunsImmediately()
    {
        Resources.RegisterAsset("request/loop", new TextAsset("loop"));
        ResourceRequest request = Resources.LoadAsync<TextAsset>("request/loop");
        int callbacks = 0;
        request.completed += _ => callbacks++;

        UnityRuntime.Tick(0.001f);

        Assert.True(request.isDone);
        Assert.Equal(1f, request.progress);
        Assert.Equal(1, callbacks);
        request.completed += _ => callbacks++;
        Assert.Equal(2, callbacks);
    }

    [Fact]
    public void CoroutineResumesBeforeOriginalCompletionCallbackAtFrameEnd()
    {
        Resources.RegisterAsset("request/coroutine", new TextAsset("coroutine"));
        ResourceRequest request = Resources.LoadAsync<TextAsset>("request/coroutine");
        var host = new GameObject("resource-request-yield-host");
        try
        {
            var receiver = host.AddComponent<RequestYieldReceiver>();
            receiver.Begin(request);
            PrimeCoroutines(receiver);

            UnityRuntime.Tick(0.001f);

            Assert.True(receiver.Resumed);
            Assert.Equal(0, receiver.CallbackCountWhenResumed);
            Assert.Equal(1, receiver.CallbackCount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Fact]
    public void AssetGetterBlocksRequestAndInvokesOriginalCallbackSynchronously()
    {
        var bundle = CreateBundleWithAssets(("asset.txt", new TextAsset("asset")));
        AssetBundleRequest request = bundle.LoadAssetAsync<TextAsset>("asset.txt");
        int callbacks = 0;
        request.completed += _ => callbacks++;

        Assert.False(request.isDone);
        UnityEngine.Object? asset = request.asset;

        Assert.IsType<TextAsset>(asset);
        Assert.True(request.isDone);
        Assert.Equal(1f, request.progress);
        Assert.Equal(1, callbacks);
        Assert.Single(request.allAssets);
    }

    [Fact]
    public void AllAssetsGetterBlocksAndReturnsEveryMatchingAsset()
    {
        var first = new TextAsset("first");
        var second = new TextAsset("second");
        var bundle = CreateBundleWithAssets(("first.txt", first), ("second.txt", second));
        AssetBundleRequest request = bundle.LoadAllAssetsAsync<TextAsset>();

        UnityEngine.Object[] assets = request.allAssets;

        Assert.True(request.isDone);
        Assert.Equal(2, assets.Length);
        Assert.Contains(first, assets);
        Assert.Contains(second, assets);
        Assert.Same(assets[0], request.asset);

        UnityEngine.Object[] secondRead = request.allAssets;
        Assert.NotSame(assets, secondRead);
        assets[0] = null!;
        Assert.NotNull(secondRead[0]);
        Assert.NotNull(request.allAssets[0]);
    }

    [Fact]
    public void MissingAssetCompletesWithNullAndEmptyArray()
    {
        var bundle = new AssetBundle();
        AssetBundleRequest request = bundle.LoadAssetAsync<TextAsset>("missing.txt");

        Assert.Null(request.asset);

        Assert.True(request.isDone);
        Assert.Empty(request.allAssets);
    }

    [Fact]
    public void AssetBundleRequestPlayerLoopCompletionUsesDeferredFrameCallback()
    {
        var bundle = CreateBundleWithAssets(("loop.txt", new TextAsset("loop")));
        AssetBundleRequest request = bundle.LoadAssetAsync<TextAsset>("loop.txt");
        var host = new GameObject("asset-bundle-request-yield-host");
        try
        {
            var receiver = host.AddComponent<RequestYieldReceiver>();
            receiver.Begin(request);
            PrimeCoroutines(receiver);

            UnityRuntime.Tick(0.001f);

            Assert.True(request.isDone);
            Assert.True(receiver.Resumed);
            Assert.Equal(0, receiver.CallbackCountWhenResumed);
            Assert.Equal(1, receiver.CallbackCount);
            Assert.IsType<TextAsset>(request.asset);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Fact]
    public void CreateRequestAssetBundleGetterBlocksAndInvokesCallback()
    {
        AssetBundleCreateRequest request = AssetBundle.LoadFromMemoryAsync(new byte[] { 1, 2, 3, 4 });
        int callbacks = 0;
        request.completed += _ => callbacks++;

        Assert.False(request.isDone);
        AssetBundle? bundle = request.assetBundle;

        Assert.NotNull(bundle);
        Assert.True(request.isDone);
        Assert.Equal(1f, request.progress);
        Assert.Equal(1, callbacks);
    }

    [Fact]
    public void CreateRequestCompletesThroughPlayerLoop()
    {
        AssetBundleCreateRequest request = AssetBundle.LoadFromMemoryAsync(new byte[] { 5, 6, 7, 8 });
        int callbacks = 0;
        request.completed += _ => callbacks++;

        UnityRuntime.Tick(0.001f);

        Assert.True(request.isDone);
        Assert.Equal(1, callbacks);
        Assert.NotNull(request.assetBundle);
    }

    [Fact]
    public void InvalidMemoryCreateRequestCompletesWithNullBundle()
    {
        AssetBundleCreateRequest request = AssetBundle.LoadFromMemoryAsync(Array.Empty<byte>());

        Assert.False(request.isDone);
        Assert.Null(request.assetBundle);
        Assert.True(request.isDone);
        Assert.Equal(1f, request.progress);
    }

    [Fact]
    public void StreamCreateRequestDefersReadingUntilCompletion()
    {
        using var stream = new MemoryStream(new byte[] { 9, 10, 11, 12 });
        AssetBundleCreateRequest request = AssetBundle.LoadFromStreamAsync(stream);

        Assert.False(request.isDone);
        Assert.Equal(0, stream.Position);

        Assert.NotNull(request.assetBundle);
        Assert.True(request.isDone);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void UnloadWaitForCompletionPerformsUnloadAndInvokesCallback()
    {
        var bundle = new AssetBundle();
        AssetBundleUnloadOperation operation = bundle.UnloadAsync(false);
        int callbacks = 0;
        operation.completed += _ => callbacks++;

        Assert.False(operation.isDone);
        Assert.Contains(bundle, AssetBundle.GetAllLoadedAssetBundles());

        operation.WaitForCompletion();

        Assert.True(operation.isDone);
        Assert.Equal(1f, operation.progress);
        Assert.Equal(1, callbacks);
        Assert.DoesNotContain(bundle, AssetBundle.GetAllLoadedAssetBundles());
    }

    [Fact]
    public void UnloadOperationCompletesThroughPlayerLoop()
    {
        var bundle = new AssetBundle();
        AssetBundleUnloadOperation operation = bundle.UnloadAsync(false);
        int callbacks = 0;
        operation.completed += _ => callbacks++;

        UnityRuntime.Tick(0.001f);

        Assert.True(operation.isDone);
        Assert.Equal(1, callbacks);
        Assert.DoesNotContain(bundle, AssetBundle.GetAllLoadedAssetBundles());
    }

    private static AssetBundle CreateBundleWithAssets(params (string name, UnityEngine.Object asset)[] assets)
    {
        var bundle = new AssetBundle();
        MethodInfo register = typeof(AssetBundle).GetMethod("RegisterAsset", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var (name, asset) in assets)
            register.Invoke(bundle, new object[] { name, asset });
        return bundle;
    }

    private static void AssertPublicGetOnlyProperty(Type type, string name, Type propertyType)
    {
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        Assert.Equal(propertyType, property.PropertyType);
        Assert.NotNull(property.GetMethod);
        Assert.Null(property.SetMethod);
    }

    private static MethodInfo GetDeclaredResultMethod(Type type)
    {
        return type.GetMethod("GetResult", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!;
    }

    private static void AssertRequiredByNativeCode(MemberInfo member)
    {
        Assert.Contains(member.CustomAttributes, attribute =>
            attribute.AttributeType.FullName == "UnityEngine.Scripting.RequiredByNativeCodeAttribute");
    }

    private static void AssertNativeHeader(Type type, string expected)
    {
        AssertAttributeString(type, "UnityEngine.Bindings.NativeHeaderAttribute", expected);
    }

    private static void AssertAttributeString(MemberInfo member, string attributeType, string expected)
    {
        CustomAttributeData attribute = Assert.Single(member.CustomAttributes.Where(candidate =>
            candidate.AttributeType.FullName == attributeType));
        Assert.Equal(expected, attribute.ConstructorArguments.Single().Value);
    }

    private static void PrimeCoroutines(MonoBehaviour behaviour)
    {
        typeof(MonoBehaviour)
            .GetMethod("TickCoroutines", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(behaviour, null);
    }

    public sealed class RequestYieldReceiver : MonoBehaviour
    {
        public bool Resumed { get; private set; }
        public int CallbackCount { get; private set; }
        public int CallbackCountWhenResumed { get; private set; } = -1;

        public void Begin(AsyncOperation operation)
        {
            operation.completed += _ => CallbackCount++;
            StartCoroutine(Wait(operation));
        }

        private IEnumerator Wait(AsyncOperation operation)
        {
            yield return operation;
            CallbackCountWhenResumed = CallbackCount;
            Resumed = true;
        }
    }
}
