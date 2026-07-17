using System.Collections;
using System.Reflection;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class CoreObjectComponentParityTests
{
    [Fact]
    public void CoreBaseTypesMatchUnityInheritanceShape()
    {
        Assert.DoesNotContain(typeof(IDisposable), typeof(UnityEngine.Object).GetInterfaces());
        Assert.True(typeof(GameObject).IsSealed);
        Assert.Equal(typeof(UnityEngine.Object), typeof(Component).BaseType);
        Assert.Equal(typeof(Component), typeof(Behaviour).BaseType);
        Assert.Equal(typeof(Behaviour), typeof(MonoBehaviour).BaseType);
    }

    [Fact]
    public void ObjectDoesNotExposeAnityOnlyLifecycleHelpers()
    {
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

        Assert.Null(typeof(UnityEngine.Object).GetMethod("Dispose", Public));
        Assert.Null(typeof(UnityEngine.Object).GetProperty("IsDestroyed", Public));
        Assert.Null(typeof(UnityEngine.Object).GetProperty("IsDontDestroyOnLoad", Public));
        Assert.Null(typeof(UnityEngine.Object).GetMethod("GetAllObjects", Public));
    }

    [Fact]
    public void MonoBehaviourDoesNotDeclareUnityMagicMethodsAsPublicApi()
    {
        const BindingFlags Surface = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        Assert.Null(typeof(MonoBehaviour).GetMethod("Awake", Surface));
        Assert.Null(typeof(MonoBehaviour).GetMethod("Start", Surface));
        Assert.Null(typeof(MonoBehaviour).GetMethod("Update", Surface));
        Assert.Null(typeof(MonoBehaviour).GetMethod("OnDestroy", Surface));
        Assert.DoesNotContain(typeof(MonoBehaviour).GetMethods(Surface), method =>
            method.Name == "Invoke" && method.GetParameters().FirstOrDefault()?.ParameterType == typeof(Action));
    }

    [Fact]
    public void ParameterlessGameObjectUsesUnityDefaultName()
    {
        var gameObject = new GameObject();
        try
        {
            Assert.Equal("New Game Object", gameObject.name);
            Assert.Same(gameObject, gameObject.gameObject);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void GenericGetComponentSupportsImplementedInterfaces()
    {
        var gameObject = new GameObject("interface-query");
        try
        {
            var component = gameObject.AddComponent<InterfaceComponent>();

            Assert.Same(component, gameObject.GetComponent<IQueryMarker>());
            Assert.Same(component, component.GetComponent<IQueryMarker>());
            Assert.True(gameObject.TryGetComponent<IQueryMarker>(out var found));
            Assert.Same(component, found);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void GetComponentsInChildrenDefaultsToExcludingInactiveChildren()
    {
        var root = CreateHierarchy(out var child, out _);
        try
        {
            child.SetActive(false);

            QueryComponent[] found = root.GetComponentsInChildren<QueryComponent>();

            Assert.Single(found);
            Assert.Same(root.GetComponent<QueryComponent>(), found[0]);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void GetComponentsInChildrenCanIncludeInactiveSubtrees()
    {
        var root = CreateHierarchy(out var child, out var grandchild);
        try
        {
            child.SetActive(false);

            QueryComponent[] found = root.GetComponentsInChildren<QueryComponent>(true);

            Assert.Equal(3, found.Length);
            Assert.Contains(child.GetComponent<QueryComponent>(), found);
            Assert.Contains(grandchild.GetComponent<QueryComponent>(), found);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void GetComponentsInChildrenStillSearchesInactiveRoot()
    {
        var root = CreateHierarchy(out _, out _);
        try
        {
            root.SetActive(false);

            QueryComponent[] found = root.GetComponentsInChildren<QueryComponent>();

            Assert.Single(found);
            Assert.Same(root.GetComponent<QueryComponent>(), found[0]);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void GetComponentsInParentDefaultsToExcludingInactiveParents()
    {
        var root = CreateHierarchy(out var child, out _);
        try
        {
            root.SetActive(false);

            QueryComponent[] defaultResult = child.GetComponentsInParent<QueryComponent>();
            QueryComponent[] includeInactive = child.GetComponentsInParent<QueryComponent>(true);

            Assert.Single(defaultResult);
            Assert.Same(child.GetComponent<QueryComponent>(), defaultResult[0]);
            Assert.Equal(2, includeInactive.Length);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void ListOverloadsClearAndPopulateCallerList()
    {
        var root = CreateHierarchy(out var child, out _);
        try
        {
            var results = new List<QueryComponent> { child.GetComponent<QueryComponent>() };

            root.GetComponentsInChildren(false, results);

            Assert.Equal(3, results.Count);
            Assert.Same(root.GetComponent<QueryComponent>(), results[0]);
            Assert.Contains(child.GetComponent<QueryComponent>(), results);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void ComponentIndexApisIncludeTransformAtZero()
    {
        var gameObject = new GameObject("component-index");
        try
        {
            QueryComponent component = gameObject.AddComponent<QueryComponent>();

            Assert.Equal(2, gameObject.GetComponentCount());
            Assert.Same(gameObject.transform, gameObject.GetComponentAtIndex(0));
            Assert.Same(component, gameObject.GetComponentAtIndex<QueryComponent>(1));
            Assert.Equal(1, gameObject.GetComponentIndex(component));
            Assert.Equal(1, component.GetComponentIndex());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void TypeBasedTryGetComponentReturnsComponentAndNullOnMiss()
    {
        var gameObject = new GameObject("type-query");
        try
        {
            QueryComponent expected = gameObject.AddComponent<QueryComponent>();

            Assert.True(gameObject.TryGetComponent(typeof(QueryComponent), out Component? found));
            Assert.Same(expected, found);
            Assert.False(gameObject.TryGetComponent(typeof(MessageReceiver), out Component? missing));
            Assert.Null(missing);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void ObjectFindApisRespectFindObjectsInactive()
    {
        var active = new GameObject("find-active");
        var inactive = new GameObject("find-inactive");
        try
        {
            QueryComponent activeComponent = active.AddComponent<QueryComponent>();
            QueryComponent inactiveComponent = inactive.AddComponent<QueryComponent>();
            inactive.SetActive(false);

            QueryComponent[] excluded = UnityEngine.Object.FindObjectsByType<QueryComponent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            QueryComponent[] included = UnityEngine.Object.FindObjectsByType<QueryComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.Contains(activeComponent, excluded);
            Assert.DoesNotContain(inactiveComponent, excluded);
            Assert.Contains(activeComponent, included);
            Assert.Contains(inactiveComponent, included);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(active);
            UnityEngine.Object.DestroyImmediate(inactive);
        }
    }

    [Fact]
    public void FindFirstObjectByTypeUsesInstanceIdOrdering()
    {
        var firstObject = new GameObject("first-instance");
        var secondObject = new GameObject("second-instance");
        try
        {
            QueryComponent first = firstObject.AddComponent<QueryComponent>();
            secondObject.AddComponent<QueryComponent>();

            QueryComponent? found = UnityEngine.Object.FindFirstObjectByType<QueryComponent>(FindObjectsInactive.Include);

            Assert.Same(first, found);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(secondObject);
        }
    }

    [Fact]
    public void DestroyIsDeferredUntilEndOfFrameAndCancelsToken()
    {
        var gameObject = new GameObject("deferred-destroy");
        DestroyAwareBehaviour behaviour = gameObject.AddComponent<DestroyAwareBehaviour>();
        CancellationToken token = behaviour.destroyCancellationToken;

        UnityEngine.Object.Destroy(gameObject);

        Assert.True(gameObject);
        Assert.False(token.IsCancellationRequested);

        UnityRuntime.Tick(0.001f);

        Assert.False(gameObject);
        Assert.False(behaviour);
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(new[] { "disable", "destroy" }, behaviour.Events);
    }

    [Fact]
    public void DestroyImmediateCancelsTokenSynchronously()
    {
        var gameObject = new GameObject("immediate-destroy");
        DestroyAwareBehaviour behaviour = gameObject.AddComponent<DestroyAwareBehaviour>();
        CancellationToken token = behaviour.destroyCancellationToken;

        UnityEngine.Object.DestroyImmediate(gameObject);

        Assert.True(token.IsCancellationRequested);
        Assert.False(gameObject);
        Assert.False(behaviour);
    }

    [Fact]
    public void SendMessageOverloadsInvokeZeroAndOneArgumentReceivers()
    {
        var gameObject = new GameObject("send-message");
        try
        {
            MessageReceiver receiver = gameObject.AddComponent<MessageReceiver>();

            gameObject.SendMessage("Ping");
            gameObject.SendMessage("SetValue", 42);

            Assert.Equal(1, receiver.Pings);
            Assert.Equal(42, receiver.Value);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void PhysicsMessagesInvokePrivateUnityReceivers()
    {
        var gameObject = new GameObject("physics-message");
        try
        {
            PhysicsMessageReceiver receiver = gameObject.AddComponent<PhysicsMessageReceiver>();
            var collision = new Collision();
            var collider = gameObject.AddComponent<BoxCollider>();

            gameObject.SendMessage("OnCollisionEnter", collision);
            gameObject.SendMessage("OnTriggerEnter", collider);

            Assert.Same(collision, receiver.Collision);
            Assert.Same(collider, receiver.Trigger);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void BroadcastAndUpwardsMessagesTraverseHierarchy()
    {
        var root = new GameObject("message-root");
        var child = new GameObject("message-child");
        child.transform.SetParent(root.transform, false);
        try
        {
            MessageReceiver rootReceiver = root.AddComponent<MessageReceiver>();
            MessageReceiver childReceiver = child.AddComponent<MessageReceiver>();

            root.BroadcastMessage("Ping");
            child.SendMessageUpwards("SetValue", 7);

            Assert.Equal(1, rootReceiver.Pings);
            Assert.Equal(1, childReceiver.Pings);
            Assert.Equal(7, rootReceiver.Value);
            Assert.Equal(7, childReceiver.Value);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void PrivateStartAndUpdateMessagesAreDispatchedByName()
    {
        var gameObject = new GameObject("magic-message");
        try
        {
            MagicMessageBehaviour behaviour = gameObject.AddComponent<MagicMessageBehaviour>();

            UnityRuntime.Tick(0.001f);

            Assert.Equal(1, behaviour.Starts);
            Assert.Equal(1, behaviour.Updates);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void EnumeratorStartIsAutomaticallyRunAsCoroutine()
    {
        var gameObject = new GameObject("enumerator-start");
        try
        {
            EnumeratorStartBehaviour behaviour = gameObject.AddComponent<EnumeratorStartBehaviour>();

            UnityRuntime.Tick(0.001f);
            Assert.Equal(1, behaviour.Steps);

            UnityRuntime.Tick(0.001f);
            Assert.Equal(2, behaviour.Steps);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static GameObject CreateHierarchy(out GameObject child, out GameObject grandchild)
    {
        var root = new GameObject("query-root");
        child = new GameObject("query-child");
        grandchild = new GameObject("query-grandchild");
        child.transform.SetParent(root.transform, false);
        grandchild.transform.SetParent(child.transform, false);
        root.AddComponent<QueryComponent>();
        child.AddComponent<QueryComponent>();
        grandchild.AddComponent<QueryComponent>();
        return root;
    }

    public interface IQueryMarker { }

    public sealed class InterfaceComponent : MonoBehaviour, IQueryMarker { }
    public sealed class QueryComponent : MonoBehaviour { }

    public sealed class DestroyAwareBehaviour : MonoBehaviour
    {
        public List<string> Events { get; } = new();
        private void OnDisable() => Events.Add("disable");
        private void OnDestroy() => Events.Add("destroy");
    }

    public sealed class MessageReceiver : MonoBehaviour
    {
        public int Pings { get; private set; }
        public int Value { get; private set; }

        private void Ping() => Pings++;
        private void SetValue(int value) => Value = value;
    }

    public sealed class PhysicsMessageReceiver : MonoBehaviour
    {
        public Collision? Collision { get; private set; }
        public Collider? Trigger { get; private set; }
        private void OnCollisionEnter(Collision collision) => Collision = collision;
        private void OnTriggerEnter(Collider collider) => Trigger = collider;
    }

    public sealed class MagicMessageBehaviour : MonoBehaviour
    {
        public int Starts { get; private set; }
        public int Updates { get; private set; }
        private void Start() => Starts++;
        private void Update() => Updates++;
    }

    public sealed class EnumeratorStartBehaviour : MonoBehaviour
    {
        public int Steps { get; private set; }

        private IEnumerator Start()
        {
            Steps++;
            yield return null;
            Steps++;
        }
    }
}
