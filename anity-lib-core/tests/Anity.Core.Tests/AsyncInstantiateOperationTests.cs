using System.Reflection;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class AsyncInstantiateOperationTests
{
    [Fact]
    public void PublicSurfaceContainsAllTenOfficialOverloads()
    {
        MethodInfo[] overloads = typeof(UnityEngine.Object)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(UnityEngine.Object.InstantiateAsync))
            .ToArray();

        Assert.Equal(10, overloads.Length);
        Assert.All(overloads, method =>
        {
            Assert.True(method.IsGenericMethodDefinition);
            Assert.Equal(typeof(UnityEngine.Object), method.GetGenericArguments()[0].GetGenericParameterConstraints().Single());
        });
    }

    [Fact]
    public void OperationTypesMatchOfficialInheritanceAndConstructorShape()
    {
        Assert.Equal(typeof(AsyncOperation), typeof(AsyncInstantiateOperation).BaseType);
        Assert.Equal(typeof(CustomYieldInstruction), typeof(AsyncInstantiateOperation<>).BaseType);
        Assert.NotNull(typeof(AsyncInstantiateOperation).GetConstructor(Type.EmptyTypes));
        Assert.Empty(typeof(AsyncInstantiateOperation<>).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void NewOperationStartsPendingAtOfficialProgressBoundary()
    {
        var original = new GameObject("async-original");
        try
        {
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(original, 3);

            Assert.False(operation.isDone);
            Assert.Equal(0.9f, operation.progress);
            Assert.Null(operation.Result);
            Assert.True(operation.keepWaiting);
            Assert.Same(operation.GetOperation(), (AsyncInstantiateOperation)operation);

            operation.Cancel();
            operation.WaitForCompletion();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void WaitForCompletionCreatesRequestedTypedResult()
    {
        var original = new GameObject("batch-original");
        try
        {
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(original, 3);
            operation.WaitForCompletion();

            Assert.True(operation.isDone);
            Assert.Equal(1f, operation.progress);
            Assert.False(operation.keepWaiting);
            Assert.IsType<GameObject[]>(operation.Result);
            Assert.Equal(3, operation.Result!.Length);
            Assert.All(operation.Result, clone => Assert.Equal("batch-original(Clone)", clone.name));

            DestroyResults(operation.Result);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void PlayerLoopIntegratesPendingOperation()
    {
        var original = new GameObject("tick-original");
        try
        {
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(original, 2);

            UnityRuntime.Tick(0.001f);

            Assert.True(operation.isDone);
            Assert.Equal(2, operation.Result!.Length);
            DestroyResults(operation.Result);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void CoroutineWaitsForNonGenericAsyncOperation()
    {
        var original = new GameObject("yield-original");
        var host = new GameObject("yield-host");
        try
        {
            var receiver = host.AddComponent<AsyncYieldReceiver>();
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(original, 2);
            receiver.Begin(operation.GetOperation());

            Assert.False(receiver.Resumed);
            UnityRuntime.Tick(0.001f);

            Assert.True(operation.isDone);
            UnityRuntime.Tick(0.001f);
            Assert.True(receiver.Resumed);
            DestroyResults(operation.Result);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void EmptySpansPreserveOriginalWorldPose()
    {
        var original = new GameObject("pose-original");
        original.transform.SetPositionAndRotation(new Vector3(3, 4, 5), Quaternion.Euler(10, 20, 30));
        try
        {
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(original, 2);
            operation.WaitForCompletion();

            Assert.All(operation.Result!, clone =>
            {
                Assert.Equal(original.transform.position, clone.transform.position);
                Assert.True(Quaternion.Angle(original.transform.rotation, clone.transform.rotation) < 0.001f);
            });
            DestroyResults(operation.Result);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void ParentOverloadPreservesWorldPoseWhenSpansAreEmpty()
    {
        var original = new GameObject("parent-pose-original");
        var parent = new GameObject("parent");
        original.transform.position = new Vector3(3, 4, 5);
        parent.transform.position = new Vector3(10, 0, 0);
        try
        {
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(original, 2, parent.transform);
            operation.WaitForCompletion();

            Assert.All(operation.Result!, clone =>
            {
                Assert.Same(parent.transform, clone.transform.parent);
                Assert.Equal(new Vector3(3, 4, 5), clone.transform.position);
                Assert.Equal(new Vector3(-7, 4, 5), clone.transform.localPosition);
            });
            DestroyResults(operation.Result);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void ExplicitParentPoseIsInterpretedInWorldSpace()
    {
        var original = new GameObject("explicit-parent-original");
        var parent = new GameObject("parent");
        parent.transform.position = new Vector3(10, 0, 0);
        try
        {
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(
                original, 2, parent.transform, new Vector3(1, 2, 3), Quaternion.identity);
            operation.WaitForCompletion();

            Assert.All(operation.Result!, clone =>
            {
                Assert.Equal(new Vector3(1, 2, 3), clone.transform.position);
                Assert.Equal(new Vector3(-9, 2, 3), clone.transform.localPosition);
            });
            DestroyResults(operation.Result);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void SpanValuesCycleWhenShorterThanCount()
    {
        var original = new GameObject("span-original");
        Vector3[] positions = { Vector3.one, Vector3.one * 2 };
        Quaternion[] rotations = { Quaternion.identity, Quaternion.Euler(0, 90, 0) };
        try
        {
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(original, 3, positions, rotations);
            operation.WaitForCompletion();

            Assert.Equal(Vector3.one, operation.Result![0].transform.position);
            Assert.Equal(Vector3.one * 2, operation.Result[1].transform.position);
            Assert.Equal(Vector3.one, operation.Result[2].transform.position);
            Assert.True(rotations[1] == operation.Result[1].transform.rotation);
            Assert.True(rotations[0] == operation.Result[2].transform.rotation);
            DestroyResults(operation.Result);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void ComponentInstantiationReturnsMatchingTypedComponents()
    {
        var original = new GameObject("component-original");
        var component = original.AddComponent<AsyncCloneComponent>();
        component.Value = 42;
        try
        {
            AsyncInstantiateOperation<AsyncCloneComponent> operation = UnityEngine.Object.InstantiateAsync(component, 2);
            operation.WaitForCompletion();

            Assert.IsType<AsyncCloneComponent[]>(operation.Result);
            Assert.Equal(2, operation.Result!.Length);
            Assert.All(operation.Result, clone => Assert.Equal(42, clone.Value));
            DestroyResults(operation.Result);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void NullOriginalUsesOfficialArgumentException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => UnityEngine.Object.InstantiateAsync<GameObject>(null!));

        Assert.Equal("The Object you want to instantiate is null.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveCountUsesOfficialArgumentException(int count)
    {
        var original = new GameObject("invalid-count");
        try
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => UnityEngine.Object.InstantiateAsync(original, count));
            Assert.Equal("Cannot call instantiate multiple with count less or equal to zero", exception.Message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void ScriptableObjectUsesOfficialUnsupportedException()
    {
        var original = ScriptableObject.CreateInstance<AsyncCloneAsset>();
        try
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => UnityEngine.Object.InstantiateAsync(original));
            Assert.Equal("Cannot call instantiate multiple for a ScriptableObject", exception.Message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void CompletionFiresOnceAndLateSubscriberRunsImmediately()
    {
        var original = new GameObject("completion-original");
        try
        {
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(original);
            int callbacks = 0;
            operation.completed += _ => callbacks++;

            operation.WaitForCompletion();
            operation.WaitForCompletion();
            Assert.Equal(1, callbacks);

            int lateCallbacks = 0;
            operation.completed += _ => lateCallbacks++;
            Assert.Equal(1, lateCallbacks);
            DestroyResults(operation.Result);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void SceneActivationGateBlocksAndThenReleasesIntegration()
    {
        var original = new GameObject("activation-original");
        try
        {
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(original, 2);
            operation.allowSceneActivation = false;

            UnityRuntime.Tick(0.001f);
            Assert.False(operation.isDone);
            Assert.True(operation.IsWaitingForSceneActivation());
            Assert.Null(operation.Result);

            operation.allowSceneActivation = true;
            UnityRuntime.Tick(0.001f);
            Assert.True(operation.isDone);
            Assert.False(operation.IsWaitingForSceneActivation());
            DestroyResults(operation.Result);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void CancelCompletesWithNullResultAndOneCallback()
    {
        var original = new GameObject("cancel-original");
        try
        {
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(original, 100);
            int callbacks = 0;
            operation.completed += _ => callbacks++;

            operation.Cancel();
            Assert.False(operation.isDone);
            operation.WaitForCompletion();

            Assert.True(operation.isDone);
            Assert.Equal(1f, operation.progress);
            Assert.Null(operation.Result);
            Assert.Equal(1, callbacks);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void CancelDestroysObjectsIntegratedInEarlierSlices()
    {
        float previous = AsyncInstantiateOperation.GetIntegrationTimeMS();
        var original = new GameObject("partial-original");
        try
        {
            AsyncInstantiateOperation.SetIntegrationTimeMS(0.000001f);
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(original, 20);

            UnityRuntime.Tick(0.001f);
            Assert.False(operation.isDone);
            Assert.Contains(
                UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                gameObject => gameObject.name == "partial-original(Clone)");

            operation.Cancel();
            UnityRuntime.Tick(0.001f);

            Assert.True(operation.isDone);
            Assert.Null(operation.Result);
            Assert.DoesNotContain(
                UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                gameObject => gameObject.name == "partial-original(Clone)");
        }
        finally
        {
            AsyncInstantiateOperation.SetIntegrationTimeMS(previous);
            UnityEngine.Object.DestroyImmediate(original);
        }
    }

    [Fact]
    public void IntegrationTimeMatchesOfficialValidationRules()
    {
        float previous = AsyncInstantiateOperation.GetIntegrationTimeMS();
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AsyncInstantiateOperation.SetIntegrationTimeMS(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AsyncInstantiateOperation.SetIntegrationTimeMS(-1f));

            AsyncInstantiateOperation.SetIntegrationTimeMS(float.NaN);
            Assert.True(float.IsNaN(AsyncInstantiateOperation.GetIntegrationTimeMS()));
            AsyncInstantiateOperation.SetIntegrationTimeMS(float.PositiveInfinity);
            Assert.True(float.IsPositiveInfinity(AsyncInstantiateOperation.GetIntegrationTimeMS()));
            AsyncInstantiateOperation.SetIntegrationTimeMS(0.5f);
            Assert.Equal(0.5f, AsyncInstantiateOperation.GetIntegrationTimeMS());
        }
        finally
        {
            AsyncInstantiateOperation.SetIntegrationTimeMS(previous);
        }
    }

    private static void DestroyResults<T>(T[]? results) where T : UnityEngine.Object
    {
        if (results is null)
            return;
        foreach (T result in results)
        {
            GameObject? gameObject = result as GameObject ?? (result as Component)?.gameObject;
            if (gameObject is not null && gameObject)
                UnityEngine.Object.DestroyImmediate(gameObject);
            else if (result)
                UnityEngine.Object.DestroyImmediate(result);
        }
    }

    public sealed class AsyncCloneComponent : MonoBehaviour
    {
        public int Value;
    }

    public sealed class AsyncCloneAsset : ScriptableObject
    {
    }

    public sealed class AsyncYieldReceiver : MonoBehaviour
    {
        public bool Resumed { get; private set; }

        public void Begin(AsyncOperation operation) => StartCoroutine(Wait(operation));

        private System.Collections.IEnumerator Wait(AsyncOperation operation)
        {
            yield return operation;
            Resumed = true;
        }
    }
}
