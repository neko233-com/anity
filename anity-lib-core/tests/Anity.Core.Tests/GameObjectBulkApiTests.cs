using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Jobs;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class GameObjectBulkApiTests
{
    private static readonly string[] LegacyProperties =
    {
        "rigidbody", "rigidbody2D", "camera", "light", "animation", "constantForce", "renderer",
        "audio", "networkView", "collider", "collider2D", "hingeJoint", "particleSystem"
    };

    [Fact]
    public void PublicSurfaceContainsAllOfficialBulkAndLegacyMembers()
    {
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        Assert.NotNull(typeof(GameObject).GetMethod(nameof(GameObject.GetScene), Public, null, new[] { typeof(int) }, null));
        Assert.NotNull(typeof(GameObject).GetMethod(nameof(GameObject.SetGameObjectsActive), Public, null, new[] { typeof(ReadOnlySpan<int>), typeof(bool) }, null));
        Assert.NotNull(typeof(GameObject).GetMethod(nameof(GameObject.SetGameObjectsActive), Public, null, new[] { typeof(NativeArray<int>), typeof(bool) }, null));

        MethodInfo instantiate = Assert.Single(typeof(GameObject).GetMethods(Public), method => method.Name == nameof(GameObject.InstantiateGameObjects));
        ParameterInfo destination = instantiate.GetParameters()[4];
        Assert.True(destination.IsOptional);
        Assert.Null(destination.DefaultValue);
        Assert.NotNull(destination.GetCustomAttribute<OptionalAttribute>());

        Assert.All(LegacyProperties, name => Assert.NotNull(typeof(GameObject).GetProperty(name, Public)));
        Assert.NotNull(typeof(GameObject).GetMethod("SetActiveRecursively", Public));
        Assert.NotNull(typeof(GameObject).GetMethod("SampleAnimation", Public));
        Assert.NotNull(typeof(GameObject).GetMethod("PlayAnimation", Public));
        Assert.NotNull(typeof(GameObject).GetMethod("StopAnimation", Public));
    }

    [Fact]
    public void SpanBulkActivationIgnoresInvalidAndDuplicateIds()
    {
        var first = new GameObject("bulk-first");
        var second = new GameObject("bulk-second");
        try
        {
            int[] ids = { first.GetInstanceID(), 0, int.MaxValue, first.GetInstanceID() };
            GameObject.SetGameObjectsActive(new ReadOnlySpan<int>(ids), false);

            Assert.False(first.activeSelf);
            Assert.True(second.activeSelf);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    [Fact]
    public void EmptySpanIsNoOp()
    {
        GameObject.SetGameObjectsActive(ReadOnlySpan<int>.Empty, false);
    }

    [Fact]
    public void NativeArrayBulkActivationMatchesSpanPath()
    {
        var first = new GameObject("native-first");
        var second = new GameObject("native-second");
        using var ids = new NativeArray<int>(new[] { first.GetInstanceID(), second.GetInstanceID() }, Allocator.Temp);
        try
        {
            GameObject.SetGameObjectsActive(ids, false);
            Assert.False(first.activeSelf);
            Assert.False(second.activeSelf);

            GameObject.SetGameObjectsActive(ids, true);
            Assert.True(first.activeSelf);
            Assert.True(second.activeSelf);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    [Fact]
    public void UninitializedNativeArrayUsesOfficialException()
    {
        NativeArray<int> ids = default;
        ArgumentException exception = Assert.Throws<ArgumentException>(() => GameObject.SetGameObjectsActive(ids, true));
        Assert.Equal("instanceIDs", exception.ParamName);
        Assert.StartsWith("NativeArray is uninitialized", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BulkLifecycleDisablesChildFirstAndEnablesParentFirstExactlyOnce()
    {
        var root = new GameObject("root");
        var rootReceiver = root.AddComponent<BulkLifecycleReceiver>();
        var child = new GameObject("child");
        child.transform.SetParent(root.transform);
        var childReceiver = child.AddComponent<BulkLifecycleReceiver>();
        try
        {
            BulkLifecycleReceiver.Events.Clear();
            int[] ids = { root.GetInstanceID(), child.GetInstanceID(), root.GetInstanceID() };

            GameObject.SetGameObjectsActive(new ReadOnlySpan<int>(ids), false);
            Assert.Equal(new[] { "child:OnDisable", "root:OnDisable" }, BulkLifecycleReceiver.Events);
            Assert.Equal(1, rootReceiver.DisableCount);
            Assert.Equal(1, childReceiver.DisableCount);

            BulkLifecycleReceiver.Events.Clear();
            GameObject.SetGameObjectsActive(new ReadOnlySpan<int>(ids), true);
            Assert.Equal(new[] { "root:OnEnable", "child:OnEnable" }, BulkLifecycleReceiver.Events);
            Assert.Equal(2, rootReceiver.EnableCount);
            Assert.Equal(2, childReceiver.EnableCount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            BulkLifecycleReceiver.Events.Clear();
        }
    }

    [Fact]
    public void SetActiveRecursivelyOverwritesEveryActiveSelfFlag()
    {
        var root = new GameObject("recursive-root");
        var child = new GameObject("recursive-child");
        var grandchild = new GameObject("recursive-grandchild");
        child.transform.SetParent(root.transform);
        grandchild.transform.SetParent(child.transform);
        child.SetActive(false);
        try
        {
            InvokeLegacy(root, "SetActiveRecursively", false);
            Assert.False(root.activeSelf);
            Assert.False(child.activeSelf);
            Assert.False(grandchild.activeSelf);

            InvokeLegacy(root, "SetActiveRecursively", true);
            Assert.True(root.activeSelf);
            Assert.True(child.activeSelf);
            Assert.True(grandchild.activeSelf);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void InstantiateGameObjectsClonesPoseHierarchyAndFillsIds()
    {
        var source = new GameObject("bulk-source");
        source.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(10, 20, 30));
        var child = new GameObject("bulk-child");
        child.transform.SetParent(source.transform, false);
        child.transform.localPosition = Vector3.right;
        using var objectIds = new NativeArray<int>(2, Allocator.Temp);
        using var transformIds = new NativeArray<int>(2, Allocator.Temp);
        var clones = new List<GameObject>();
        try
        {
            GameObject.InstantiateGameObjects(source.GetInstanceID(), 2, objectIds, transformIds);
            for (int i = 0; i < 2; i++)
            {
                GameObject clone = FindGameObject(objectIds[i]);
                clones.Add(clone);
                Assert.NotEqual(0, objectIds[i]);
                Assert.Equal(clone.transform.GetInstanceID(), transformIds[i]);
                Assert.Equal("bulk-source(Clone)", clone.name);
                Assert.Equal(source.transform.position, clone.transform.position);
                Assert.True(Quaternion.Angle(source.transform.rotation, clone.transform.rotation) < 0.001f);
                Assert.Equal(1, clone.transform.childCount);
                Assert.Equal(Vector3.right, clone.transform.GetChild(0).localPosition);
            }
            Assert.NotEqual(objectIds[0], objectIds[1]);
        }
        finally
        {
            foreach (GameObject clone in clones) UnityEngine.Object.DestroyImmediate(clone);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Fact]
    public void ActiveCloneReceivesCopiedFieldsBeforeSingleAwakeAndEnable()
    {
        var source = new GameObject("active-source");
        var sourceReceiver = source.AddComponent<BulkCloneReceiver>();
        sourceReceiver.Value = 7;
        BulkCloneReceiver.Events.Clear();
        using var objectIds = new NativeArray<int>(2, Allocator.Temp);
        using var transformIds = new NativeArray<int>(2, Allocator.Temp);
        var clones = new List<GameObject>();
        try
        {
            GameObject.InstantiateGameObjects(source.GetInstanceID(), 2, objectIds, transformIds);
            clones.Add(FindGameObject(objectIds[0]));
            clones.Add(FindGameObject(objectIds[1]));

            Assert.Equal(new[] { "Awake:7", "OnEnable:7", "Awake:7", "OnEnable:7" }, BulkCloneReceiver.Events);
            Assert.All(clones, clone =>
            {
                BulkCloneReceiver receiver = clone.GetComponent<BulkCloneReceiver>();
                Assert.Same(clone, receiver.gameObject);
                Assert.Equal(7, receiver.Value);
            });
        }
        finally
        {
            foreach (GameObject clone in clones) UnityEngine.Object.DestroyImmediate(clone);
            UnityEngine.Object.DestroyImmediate(source);
            BulkCloneReceiver.Events.Clear();
        }
    }

    [Fact]
    public void InactiveCloneDefersAwakeUntilFirstActivation()
    {
        var source = new GameObject("inactive-source");
        source.SetActive(false);
        var sourceReceiver = source.AddComponent<BulkCloneReceiver>();
        sourceReceiver.Value = 42;
        BulkCloneReceiver.Events.Clear();
        using var objectIds = new NativeArray<int>(1, Allocator.Temp);
        using var transformIds = new NativeArray<int>(1, Allocator.Temp);
        GameObject? clone = null;
        try
        {
            GameObject.InstantiateGameObjects(source.GetInstanceID(), 1, objectIds, transformIds);
            clone = FindGameObject(objectIds[0]);
            Assert.Empty(BulkCloneReceiver.Events);

            clone.SetActive(true);
            Assert.Equal(new[] { "Awake:42", "OnEnable:42" }, BulkCloneReceiver.Events);

            clone.SetActive(false);
            clone.SetActive(true);
            Assert.Equal(1, BulkCloneReceiver.Events.Count(entry => entry == "Awake:42"));
            Assert.Equal(2, BulkCloneReceiver.Events.Count(entry => entry == "OnEnable:42"));
        }
        finally
        {
            if (clone is not null) UnityEngine.Object.DestroyImmediate(clone);
            UnityEngine.Object.DestroyImmediate(source);
            BulkCloneReceiver.Events.Clear();
        }
    }

    [Fact]
    public void DestinationSceneAppliesToEntireClonedHierarchy()
    {
        var source = new GameObject("scene-source");
        var child = new GameObject("scene-child");
        child.transform.SetParent(source.transform);
        var destination = SceneManager.CreateScene("bulk-destination-" + Guid.NewGuid().ToString("N"));
        using var objectIds = new NativeArray<int>(1, Allocator.Temp);
        using var transformIds = new NativeArray<int>(1, Allocator.Temp);
        GameObject? clone = null;
        try
        {
            GameObject.InstantiateGameObjects(source.GetInstanceID(), 1, objectIds, transformIds, destination);
            clone = FindGameObject(objectIds[0]);
            Assert.Equal(destination, clone.scene);
            Assert.Equal(destination, clone.transform.GetChild(0).gameObject.scene);
            Assert.Contains(clone, destination.GetRootGameObjects());
        }
        finally
        {
            if (clone is not null) UnityEngine.Object.DestroyImmediate(clone);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Fact]
    public void ZeroCountReturnsAfterArrayInitializationValidation()
    {
        var source = new GameObject("zero-source");
        using var emptyObjects = new NativeArray<int>(0, Allocator.Temp);
        using var emptyTransforms = new NativeArray<int>(0, Allocator.Temp);
        try
        {
            GameObject.InstantiateGameObjects(source.GetInstanceID(), 0, emptyObjects, emptyTransforms);
            NativeArray<int> missing = default;
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                GameObject.InstantiateGameObjects(source.GetInstanceID(), 0, missing, emptyTransforms));
            Assert.Equal("newInstanceIDs", exception.ParamName);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Fact]
    public void InstantiateValidatesSecondArrayAndExactSizes()
    {
        var source = new GameObject("validation-source");
        using var one = new NativeArray<int>(1, Allocator.Temp);
        using var empty = new NativeArray<int>(0, Allocator.Temp);
        try
        {
            NativeArray<int> missing = default;
            ArgumentException uninitialized = Assert.Throws<ArgumentException>(() =>
                GameObject.InstantiateGameObjects(source.GetInstanceID(), 1, one, missing));
            Assert.Equal("newTransformInstanceIDs", uninitialized.ParamName);

            ArgumentException mismatch = Assert.Throws<ArgumentException>(() =>
                GameObject.InstantiateGameObjects(source.GetInstanceID(), 1, empty, empty));
            Assert.Equal("Size mismatch! Both arrays must already be the size of count.", mismatch.Message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Fact]
    public void InvalidOrComponentSourceProducesZeroIds()
    {
        var source = new GameObject("invalid-source");
        var objectIds = new NativeArray<int>(1, Allocator.Temp);
        var transformIds = new NativeArray<int>(1, Allocator.Temp);
        try
        {
            GameObject.InstantiateGameObjects(int.MaxValue, 1, objectIds, transformIds);
            Assert.Equal(0, objectIds[0]);
            Assert.Equal(0, transformIds[0]);

            objectIds[0] = -10;
            transformIds[0] = -10;
            GameObject.InstantiateGameObjects(source.transform.GetInstanceID(), 1, objectIds, transformIds);
            Assert.Equal(0, objectIds[0]);
            Assert.Equal(0, transformIds[0]);
        }
        finally
        {
            objectIds.Dispose();
            transformIds.Dispose();
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Fact]
    public void GetSceneOnlyResolvesGameObjectInstanceIds()
    {
        var source = new GameObject("scene-lookup");
        try
        {
            Assert.Equal(source.scene, GameObject.GetScene(source.GetInstanceID()));
            Assert.False(GameObject.GetScene(source.transform.GetInstanceID()).IsValid());
            Assert.False(GameObject.GetScene(int.MaxValue).IsValid());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Theory]
    [MemberData(nameof(LegacyPropertyCases))]
    public void LegacyPropertiesMatchOfficialMetadataAndException(string propertyName, string obsoleteMessage)
    {
        PropertyInfo property = typeof(GameObject).GetProperty(propertyName)!;
        Assert.Equal(typeof(UnityEngine.Component), property.PropertyType);
        Assert.Equal(EditorBrowsableState.Never, property.GetCustomAttribute<EditorBrowsableAttribute>()!.State);
        ObsoleteAttribute obsolete = property.GetCustomAttribute<ObsoleteAttribute>()!;
        Assert.True(obsolete.IsError);
        Assert.Equal(obsoleteMessage, obsolete.Message);

        var gameObject = new GameObject("legacy-property");
        try
        {
            TargetInvocationException wrapper = Assert.Throws<TargetInvocationException>(() => property.GetValue(gameObject));
            NotSupportedException exception = Assert.IsType<NotSupportedException>(wrapper.InnerException);
            Assert.Equal(propertyName + " property has been deprecated", exception.Message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Theory]
    [InlineData("SampleAnimation", "GameObject.SampleAnimation is deprecated")]
    [InlineData("PlayAnimation", "gameObject.PlayAnimation is not supported anymore. Use animation.Play();")]
    [InlineData("StopAnimation", "gameObject.StopAnimation(); is not supported anymore. Use animation.Stop();")]
    public void RemovedAnimationMethodsThrowOfficialRuntimeMessage(string methodName, string expectedMessage)
    {
        MethodInfo method = typeof(GameObject).GetMethods().Single(candidate => candidate.Name == methodName);
        var gameObject = new GameObject("legacy-animation");
        try
        {
            object?[] arguments = methodName switch
            {
                "SampleAnimation" => new object?[] { null, 1f },
                "PlayAnimation" => new object?[] { null },
                _ => Array.Empty<object?>()
            };
            TargetInvocationException wrapper = Assert.Throws<TargetInvocationException>(() => method.Invoke(gameObject, arguments));
            NotSupportedException exception = Assert.IsType<NotSupportedException>(wrapper.InnerException);
            Assert.Equal(expectedMessage, exception.Message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    public static IEnumerable<object[]> LegacyPropertyCases()
    {
        yield return Case("rigidbody", "Rigidbody");
        yield return Case("rigidbody2D", "Rigidbody2D");
        yield return Case("camera", "Camera");
        yield return Case("light", "Light");
        yield return Case("animation", "Animation");
        yield return Case("constantForce", "ConstantForce");
        yield return Case("renderer", "Renderer");
        yield return Case("audio", "AudioSource");
        yield return Case("networkView", "NetworkView");
        yield return Case("collider", "Collider");
        yield return Case("collider2D", "Collider2D");
        yield return Case("hingeJoint", "HingeJoint");
        yield return Case("particleSystem", "ParticleSystem");
    }

    private static object[] Case(string propertyName, string componentName)
        => new object[] { propertyName, $"Property {propertyName} has been deprecated. Use GetComponent<{componentName}>() instead. (UnityUpgradable)" };

    private static GameObject FindGameObject(int instanceId)
        => UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(gameObject => gameObject.GetInstanceID() == instanceId);

    private static void InvokeLegacy(GameObject target, string methodName, bool value)
        => typeof(GameObject).GetMethod(methodName)!.Invoke(target, new object[] { value });
}

public sealed class BulkLifecycleReceiver : MonoBehaviour
{
    public static readonly List<string> Events = new();
    public int EnableCount;
    public int DisableCount;

    private void OnEnable()
    {
        EnableCount++;
        Events.Add(gameObject!.name + ":OnEnable");
    }

    private void OnDisable()
    {
        DisableCount++;
        Events.Add(gameObject!.name + ":OnDisable");
    }
}

public sealed class BulkCloneReceiver : MonoBehaviour
{
    public static readonly List<string> Events = new();
    public int Value;

    private void Awake() => Events.Add("Awake:" + Value);
    private void OnEnable() => Events.Add("OnEnable:" + Value);
    private void OnDisable() => Events.Add("OnDisable:" + Value);
}
