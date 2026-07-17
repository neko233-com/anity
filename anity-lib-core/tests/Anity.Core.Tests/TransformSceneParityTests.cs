using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Jobs;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class TransformSceneParityTests
{
    private const float Tolerance = 0.001f;

    [Fact]
    public void SceneAndSceneManagerHaveUnityValueTypePublicShape()
    {
        Type sceneType = typeof(Scene);
        Type managerType = typeof(SceneManager);

        Assert.True(sceneType.IsValueType);
        Assert.True(sceneType.IsSerializable);
        Assert.Empty(sceneType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(sceneType.GetProperty("isValid", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(sceneType.GetField("Invalid", BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(sceneType.GetMethod(nameof(Scene.GetRootGameObjects), new[] { typeof(List<GameObject>) }));
        Assert.False(managerType.IsAbstract && managerType.IsSealed);
        Assert.NotNull(managerType.GetConstructor(Type.EmptyTypes));
        Assert.Null(managerType.GetProperty("activeScene", BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void SceneParameterTypesMatchUnityDefaultsAndFlags()
    {
        var create = new CreateSceneParameters(LocalPhysicsMode.Physics3D);
        var load = new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics2D | LocalPhysicsMode.Physics3D);

        Assert.True(typeof(CreateSceneParameters).IsSerializable);
        Assert.True(typeof(LoadSceneParameters).IsSerializable);
        Assert.NotNull(typeof(LocalPhysicsMode).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(LocalPhysicsMode.Physics3D, create.localPhysicsMode);
        Assert.Equal(LoadSceneMode.Additive, load.loadSceneMode);
        Assert.Equal((LocalPhysicsMode)3, load.localPhysicsMode);
    }

    [Fact]
    public void FindUsesDirectChildOrExplicitPathAndEmptyReturnsSelf()
    {
        var root = new GameObject("find-root");
        var direct = new GameObject("direct");
        var deep = new GameObject("deep");
        direct.transform.SetParent(root.transform, false);
        deep.transform.SetParent(direct.transform, false);
        try
        {
            Assert.Same(direct.transform, root.transform.Find("direct"));
            Assert.Null(root.transform.Find("deep"));
            Assert.Same(deep.transform, root.transform.Find("direct/deep"));
            Assert.Same(root.transform, root.transform.Find(string.Empty));
            Assert.Null(root.transform.Find("direct/missing"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void IsChildOfIncludesSelfAndRejectsNullLikeUnity()
    {
        var root = new GameObject("childof-root");
        var child = new GameObject("childof-child");
        child.transform.SetParent(root.transform, false);
        try
        {
            Assert.True(root.transform.IsChildOf(root.transform));
            Assert.True(child.transform.IsChildOf(root.transform));
            Assert.False(root.transform.IsChildOf(child.transform));
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => child.transform.IsChildOf(null));
            Assert.Equal("parent", exception.ParamName);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void HierarchyCountAndCapacityAreSharedByEveryNode()
    {
        var root = new GameObject("capacity-root");
        var child = new GameObject("capacity-child");
        var grandchild = new GameObject("capacity-grandchild");
        child.transform.SetParent(root.transform, false);
        grandchild.transform.SetParent(child.transform, false);
        try
        {
            Assert.Equal(3, root.transform.hierarchyCount);
            Assert.Equal(3, child.transform.hierarchyCount);
            child.transform.hierarchyCapacity = 1;
            Assert.Equal(3, root.transform.hierarchyCapacity);
            root.transform.hierarchyCapacity = 16;
            Assert.Equal(16, grandchild.transform.hierarchyCapacity);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void ReparentAllowsMovingLeafToAncestorButRejectsCyclesAndSelf()
    {
        var root = new GameObject("cycle-root");
        var middle = new GameObject("cycle-middle");
        var leaf = new GameObject("cycle-leaf");
        middle.transform.SetParent(root.transform, false);
        leaf.transform.SetParent(middle.transform, false);
        try
        {
            leaf.transform.SetParent(root.transform, false);
            Assert.Same(root.transform, leaf.transform.parent);
            Assert.Equal(2, root.transform.childCount);

            root.transform.SetParent(leaf.transform, false);
            Assert.Null(root.transform.parent);
            root.transform.SetParent(root.transform, false);
            Assert.Null(root.transform.parent);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void ReparentMessagesFollowUnityMovedHierarchyThenOldAndNewParentsOrder()
    {
        var oldParent = new GameObject("old-parent");
        var newParent = new GameObject("new-parent");
        var child = new GameObject("message-child");
        var grandchild = new GameObject("message-grandchild");
        oldParent.AddComponent<TransformMessageRecorder>().Label = "old";
        newParent.AddComponent<TransformMessageRecorder>().Label = "new";
        child.AddComponent<TransformMessageRecorder>().Label = "child";
        grandchild.AddComponent<TransformMessageRecorder>().Label = "grandchild";
        child.transform.SetParent(oldParent.transform, false);
        grandchild.transform.SetParent(child.transform, false);
        try
        {
            TransformMessageRecorder.Events.Clear();
            child.transform.SetParent(newParent.transform, false);

            Assert.Equal(
                new[] { "child.parent", "grandchild.parent", "old.children", "new.children" },
                TransformMessageRecorder.Events);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(oldParent);
            UnityEngine.Object.DestroyImmediate(newParent);
            TransformMessageRecorder.Events.Clear();
        }
    }

    [Fact]
    public void SetParentTruePreservesWorldPoseAndReportedLossyScale()
    {
        var parent = new GameObject("world-parent");
        var child = new GameObject("world-child");
        parent.transform.SetPositionAndRotation(new Vector3(10, 1, -3), Quaternion.Euler(0, 90, 0));
        parent.transform.localScale = new Vector3(2, 2, 2);
        child.transform.SetPositionAndRotation(new Vector3(3, 4, 5), Quaternion.Euler(10, 20, 30));
        child.transform.localScale = new Vector3(3, 4, 5);
        Vector3 worldPosition = child.transform.position;
        Quaternion worldRotation = child.transform.rotation;
        Vector3 worldScale = child.transform.lossyScale;
        try
        {
            child.transform.SetParent(parent.transform, true);

            AssertVector(worldPosition, child.transform.position);
            Assert.True(Quaternion.Angle(worldRotation, child.transform.rotation) < Tolerance);
            AssertVector(worldScale, child.transform.lossyScale);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
        }
    }

    [Fact]
    public void SetParentFalsePreservesLocalPoseIncludingWhenUnparenting()
    {
        var parent = new GameObject("local-parent");
        var child = new GameObject("local-child");
        parent.transform.SetPositionAndRotation(new Vector3(5, 6, 7), Quaternion.Euler(0, 45, 0));
        child.transform.localPosition = new Vector3(1, 2, 3);
        child.transform.localRotation = Quaternion.Euler(4, 5, 6);
        child.transform.localScale = new Vector3(2, 3, 4);
        try
        {
            Vector3 localPosition = child.transform.localPosition;
            Quaternion localRotation = child.transform.localRotation;
            Vector3 localScale = child.transform.localScale;
            child.transform.SetParent(parent.transform, false);
            AssertVector(localPosition, child.transform.localPosition);
            Assert.True(Quaternion.Angle(localRotation, child.transform.localRotation) < Tolerance);
            AssertVector(localScale, child.transform.localScale);

            child.transform.SetParent(null, false);
            AssertVector(localPosition, child.transform.localPosition);
            Assert.True(Quaternion.Angle(localRotation, child.transform.localRotation) < Tolerance);
            AssertVector(localScale, child.transform.localScale);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
            UnityEngine.Object.DestroyImmediate(child);
        }
    }

    [Fact]
    public void SpanTransformsMatchScalarOperationsAndRoundTripInPlace()
    {
        var gameObject = new GameObject("span-transform");
        gameObject.transform.SetPositionAndRotation(new Vector3(10, 1, -3), Quaternion.Euler(0, 90, 0));
        gameObject.transform.localScale = new Vector3(2, 3, 4);
        Vector3[] input = { new(1, 2, 3), new(-4, 5, 6) };
        try
        {
            var directions = new Vector3[2];
            var vectors = new Vector3[2];
            var points = new Vector3[2];
            gameObject.transform.TransformDirections(input, directions);
            gameObject.transform.TransformVectors(input, vectors);
            gameObject.transform.TransformPoints(input, points);

            for (int i = 0; i < input.Length; i++)
            {
                AssertVector(gameObject.transform.TransformDirection(input[i]), directions[i]);
                AssertVector(gameObject.transform.TransformVector(input[i]), vectors[i]);
                AssertVector(gameObject.transform.TransformPoint(input[i]), points[i]);
                Assert.InRange(MathF.Abs(directions[i].magnitude - input[i].magnitude), 0, Tolerance);
                AssertVector(gameObject.transform.position, points[i] - vectors[i]);
            }

            gameObject.transform.InverseTransformDirections(directions);
            gameObject.transform.InverseTransformVectors(vectors);
            gameObject.transform.InverseTransformPoints(points);
            for (int i = 0; i < input.Length; i++)
            {
                AssertVector(input[i], directions[i]);
                AssertVector(input[i], vectors[i]);
                AssertVector(input[i], points[i]);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void SpanTransformsSupportOverlappingSourceAndDestination()
    {
        var gameObject = new GameObject("span-overlap");
        gameObject.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(0, 90, 0));
        Vector3 first = new(1, 0, 0);
        Vector3 second = new(0, 1, 0);
        Vector3 expectedFirst = gameObject.transform.TransformPoint(first);
        Vector3 expectedSecond = gameObject.transform.TransformPoint(second);
        Vector3[] values = { first, second, new(9, 9, 9) };
        try
        {
            gameObject.transform.TransformPoints(values.AsSpan(0, 2), values.AsSpan(1, 2));
            AssertVector(expectedFirst, values[1]);
            AssertVector(expectedSecond, values[2]);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Theory]
    [InlineData("TransformDirections")]
    [InlineData("InverseTransformDirections")]
    [InlineData("TransformVectors")]
    [InlineData("InverseTransformVectors")]
    [InlineData("TransformPoints")]
    [InlineData("InverseTransformPoints")]
    public void SpanLengthMismatchUsesMethodSpecificUnityMessage(string methodName)
    {
        var gameObject = new GameObject("span-mismatch");
        try
        {
            MethodInfo method = typeof(Transform).GetMethod(methodName, new[] { typeof(ReadOnlySpan<Vector3>), typeof(Span<Vector3>) });
            Assert.NotNull(method);
            InvalidOperationException exception = methodName switch
            {
                "TransformDirections" => Assert.Throws<InvalidOperationException>(() => gameObject.transform.TransformDirections(new Vector3[1], new Vector3[2])),
                "InverseTransformDirections" => Assert.Throws<InvalidOperationException>(() => gameObject.transform.InverseTransformDirections(new Vector3[1], new Vector3[2])),
                "TransformVectors" => Assert.Throws<InvalidOperationException>(() => gameObject.transform.TransformVectors(new Vector3[1], new Vector3[2])),
                "InverseTransformVectors" => Assert.Throws<InvalidOperationException>(() => gameObject.transform.InverseTransformVectors(new Vector3[1], new Vector3[2])),
                "TransformPoints" => Assert.Throws<InvalidOperationException>(() => gameObject.transform.TransformPoints(new Vector3[1], new Vector3[2])),
                _ => Assert.Throws<InvalidOperationException>(() => gameObject.transform.InverseTransformPoints(new Vector3[1], new Vector3[2]))
            };
            Assert.Equal($"Both spans passed to Transform.{methodName}() must be the same length", exception.Message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void DirectionSettersAlignRequestedAxis()
    {
        var gameObject = new GameObject("direction-setters");
        try
        {
            gameObject.transform.forward = Vector3.right;
            Assert.True(Vector3.Angle(Vector3.right, gameObject.transform.forward) < Tolerance);
            gameObject.transform.up = Vector3.forward;
            Assert.True(Vector3.Angle(Vector3.forward, gameObject.transform.up) < Tolerance);
            gameObject.transform.right = Vector3.up;
            Assert.True(Vector3.Angle(Vector3.up, gameObject.transform.right) < Tolerance);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void SceneCopiesShareHandleBackedMutableStateAndRootRegistry()
    {
        string originalName = "copy-scene-" + Guid.NewGuid().ToString("N");
        Scene scene = SceneManager.CreateScene(originalName);
        Scene copy = scene;
        var root = new GameObject("copy-root");
        try
        {
            copy.name = originalName + "-renamed";
            copy.isSubScene = true;

            Assert.Equal(scene.handle, copy.handle);
            Assert.Equal(copy.name, scene.name);
            Assert.True(scene.isSubScene);
            Assert.True(scene.IsValid());
            Assert.Contains(root, scene.GetRootGameObjects());
            Assert.Equal(scene.GetRootGameObjects().Length, scene.rootCount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void RootListOverloadClearsAndRefillsCallerList()
    {
        var stale = new GameObject("root-list-stale");
        Scene scene = SceneManager.CreateScene("root-list-" + Guid.NewGuid().ToString("N"));
        var first = new GameObject("root-list-first");
        var second = new GameObject("root-list-second");
        var roots = new List<GameObject> { stale };
        try
        {
            scene.GetRootGameObjects(roots);

            Assert.DoesNotContain(stale, roots);
            Assert.Contains(first, roots);
            Assert.Contains(second, roots);
            Assert.True(roots.Capacity >= scene.rootCount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(stale);
        }
    }

    [Fact]
    public void CrossSceneParentingMovesWholeHierarchyAndUnparentKeepsDestinationScene()
    {
        Scene sourceScene = SceneManager.CreateScene("source-scene-" + Guid.NewGuid().ToString("N"));
        var sourceRoot = new GameObject("cross-source-root");
        var sourceChild = new GameObject("cross-source-child");
        sourceChild.transform.SetParent(sourceRoot.transform, false);
        Scene destinationScene = SceneManager.CreateScene("destination-scene-" + Guid.NewGuid().ToString("N"));
        var destinationRoot = new GameObject("cross-destination-root");
        try
        {
            Assert.Equal(sourceScene, sourceRoot.scene);
            sourceRoot.transform.SetParent(destinationRoot.transform, true);
            Assert.Equal(destinationScene, sourceRoot.scene);
            Assert.Equal(destinationScene, sourceChild.scene);

            sourceRoot.transform.SetParent(null, true);
            Assert.Equal(destinationScene, sourceRoot.scene);
            Assert.Contains(sourceRoot, destinationScene.GetRootGameObjects());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceRoot);
            UnityEngine.Object.DestroyImmediate(destinationRoot);
        }
    }

    [Fact]
    public void MoveGameObjectToSceneRequiresRootAndMovesDescendants()
    {
        Scene sourceScene = SceneManager.CreateScene("move-source-" + Guid.NewGuid().ToString("N"));
        var root = new GameObject("move-root");
        var child = new GameObject("move-child");
        child.transform.SetParent(root.transform, false);
        Scene destination = SceneManager.CreateScene("move-destination-" + Guid.NewGuid().ToString("N"));
        try
        {
            ArgumentException childException = Assert.Throws<ArgumentException>(() => SceneManager.MoveGameObjectToScene(child, destination));
            Assert.StartsWith("Gameobject is not a root in a scene", childException.Message, StringComparison.Ordinal);
            Assert.Throws<ArgumentException>(() => SceneManager.MoveGameObjectToScene(root, default));

            SceneManager.MoveGameObjectToScene(root, destination);
            Assert.Equal(destination, root.scene);
            Assert.Equal(destination, child.scene);
            Assert.DoesNotContain(root, sourceScene.GetRootGameObjects());
            Assert.Contains(root, destination.GetRootGameObjects());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void MoveGameObjectsToSceneValidatesInitializationButEmptyArrayIsNoOp()
    {
        NativeArray<int> uninitialized = default;
        ArgumentException exception = Assert.Throws<ArgumentException>(() => SceneManager.MoveGameObjectsToScene(uninitialized, default));
        Assert.Equal("instanceIDs", exception.ParamName);
        Assert.StartsWith("NativeArray is uninitialized", exception.Message, StringComparison.Ordinal);

        using var empty = new NativeArray<int>(0, Allocator.Temp);
        SceneManager.MoveGameObjectsToScene(empty, default);
    }

    [Fact]
    public void InstantiateWithParentDefaultsToLocalSpaceLikeUnity()
    {
        var original = new GameObject("clone-local-original");
        original.transform.localPosition = new Vector3(3, 4, 5);
        original.transform.localRotation = Quaternion.Euler(10, 20, 30);
        original.transform.localScale = new Vector3(2, 3, 4);
        var parent = CreateCloneParent("clone-local-parent");
        GameObject clone = null;
        try
        {
            clone = UnityEngine.Object.Instantiate(original, parent.transform);
            Assert.Same(parent.transform, clone.transform.parent);
            AssertVector(original.transform.localPosition, clone.transform.localPosition);
            Assert.True(Quaternion.Angle(original.transform.localRotation, clone.transform.localRotation) < Tolerance);
            AssertVector(original.transform.localScale, clone.transform.localScale);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void InstantiateWithWorldSpaceTruePreservesWorldPositionAndRotation()
    {
        var original = new GameObject("clone-world-original");
        original.transform.SetPositionAndRotation(new Vector3(3, 4, 5), Quaternion.Euler(10, 20, 30));
        var parent = CreateCloneParent("clone-world-parent");
        GameObject clone = null;
        try
        {
            clone = UnityEngine.Object.Instantiate(original, parent.transform, true);
            Assert.Same(parent.transform, clone.transform.parent);
            AssertVector(original.transform.position, clone.transform.position);
            Assert.True(Quaternion.Angle(original.transform.rotation, clone.transform.rotation) < Tolerance);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void InstantiateWithExplicitPoseAndParentUsesRequestedWorldPose()
    {
        var original = new GameObject("clone-pose-original");
        var parent = CreateCloneParent("clone-pose-parent");
        Vector3 requestedPosition = new(-2, 8, 11);
        Quaternion requestedRotation = Quaternion.Euler(15, 25, 35);
        GameObject clone = null;
        try
        {
            clone = UnityEngine.Object.Instantiate(original, requestedPosition, requestedRotation, parent.transform);
            Assert.Same(parent.transform, clone.transform.parent);
            AssertVector(requestedPosition, clone.transform.position);
            Assert.True(Quaternion.Angle(requestedRotation, clone.transform.rotation) < Tolerance);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void InstantiateIntoSceneMovesClonedHierarchyToRequestedScene()
    {
        var original = new GameObject("clone-scene-original");
        var originalChild = new GameObject("clone-scene-child");
        originalChild.transform.SetParent(original.transform, false);
        Scene destination = SceneManager.CreateScene("clone-scene-destination-" + Guid.NewGuid().ToString("N"));
        GameObject clone = null;
        try
        {
            clone = (GameObject)UnityEngine.Object.Instantiate(original, destination);
            Assert.Equal(destination, clone.scene);
            Assert.Equal(destination, clone.transform.GetChild(0).gameObject.scene);
            Assert.Contains(clone, destination.GetRootGameObjects());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(clone);
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void MergeScenesMovesRootsAndInvalidatesSourceHandle()
    {
        Scene source = SceneManager.CreateScene("merge-source-" + Guid.NewGuid().ToString("N"));
        var root = new GameObject("merge-root");
        var child = new GameObject("merge-child");
        child.transform.SetParent(root.transform, false);
        Scene destination = SceneManager.CreateScene("merge-destination-" + Guid.NewGuid().ToString("N"));
        try
        {
            SceneManager.MergeScenes(source, destination);
            Assert.False(source.IsValid());
            Assert.Equal(destination, root.scene);
            Assert.Equal(destination, child.scene);
            Assert.Contains(root, destination.GetRootGameObjects());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateCloneParent(string name)
    {
        var parent = new GameObject(name);
        parent.transform.SetPositionAndRotation(new Vector3(10, 1, -3), Quaternion.Euler(0, 90, 0));
        parent.transform.localScale = new Vector3(2, 2, 2);
        return parent;
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(MathF.Abs(expected.x - actual.x), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.y - actual.y), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.z - actual.z), 0, Tolerance);
    }
}

public sealed class TransformMessageRecorder : MonoBehaviour
{
    public static readonly List<string> Events = new();
    public string Label = string.Empty;

    private void OnTransformParentChanged() => Events.Add(Label + ".parent");
    private void OnTransformChildrenChanged() => Events.Add(Label + ".children");
}
