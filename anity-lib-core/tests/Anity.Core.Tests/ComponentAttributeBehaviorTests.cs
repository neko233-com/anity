using System.Reflection;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ComponentAttributeBehaviorCollection
{
    public const string Name = "Component attribute behavior";
}

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class ComponentAttributeBehaviorTests
{
    [Fact]
    public void OfficialAttributeTypeNamesExistWithoutLegacySuffixTypes()
    {
        Assembly assembly = typeof(GameObject).Assembly;

        Assert.Equal("RequireComponent", typeof(RequireComponent).Name);
        Assert.Equal("AddComponentMenu", typeof(AddComponentMenu).Name);
        Assert.Equal("ContextMenu", typeof(ContextMenu).Name);
        Assert.Equal("DisallowMultipleComponent", typeof(DisallowMultipleComponent).Name);
        Assert.Equal("ExecuteInEditMode", typeof(ExecuteInEditMode).Name);
        Assert.Equal("ExecuteAlways", typeof(ExecuteAlways).Name);
        Assert.Equal("HideInInspector", typeof(HideInInspector).Name);
        Assert.Null(assembly.GetType("UnityEngine.RequireComponentAttribute"));
        Assert.Null(assembly.GetType("UnityEngine.AddComponentMenuAttribute"));
        Assert.Null(assembly.GetType("UnityEngine.ContextMenuAttribute"));
    }

    [Fact]
    public void RequireComponentConstructorsPopulateAllPublicFields()
    {
        var one = new RequireComponent(typeof(DependencyA));
        var two = new RequireComponent(typeof(DependencyA), typeof(DependencyB));
        var three = new RequireComponent(typeof(DependencyA), typeof(DependencyB), typeof(DependencyC));

        Assert.Equal(typeof(DependencyA), one.m_Type0);
        Assert.Null(one.m_Type1);
        Assert.Null(one.m_Type2);
        Assert.Equal(typeof(DependencyB), two.m_Type1);
        Assert.Null(two.m_Type2);
        Assert.Equal(typeof(DependencyC), three.m_Type2);
    }

    [Fact]
    public void AddComponentMenuConstructorsExposeMenuAndOrder()
    {
        var defaultOrder = new AddComponentMenu("Anity/Agent");
        var explicitOrder = new AddComponentMenu("Anity/Agent", 42);

        Assert.Equal("Anity/Agent", defaultOrder.componentMenu);
        Assert.Equal(0, defaultOrder.componentOrder);
        Assert.Equal("Anity/Agent", explicitOrder.componentMenu);
        Assert.Equal(42, explicitOrder.componentOrder);
    }

    [Fact]
    public void ContextMenuConstructorsMatchUnityDefaults()
    {
        var basic = new ContextMenu("Run");
        var validator = new ContextMenu("Run", true);
        var prioritized = new ContextMenu("Run", true, -50);

        Assert.Equal("Run", basic.menuItem);
        Assert.False(basic.validate);
        Assert.Equal(1_000_000, basic.priority);
        Assert.True(validator.validate);
        Assert.Equal(1_000_000, validator.priority);
        Assert.Equal(-50, prioritized.priority);
    }

    [Fact]
    public void ContextMenuItemExposesImmutableNameAndFunction()
    {
        var attribute = new ContextMenuItemAttribute("Reset Value", "ResetValue");

        Assert.Equal("Reset Value", attribute.name);
        Assert.Equal("ResetValue", attribute.function);
    }

    [Fact]
    public void MetadataAttributesExposeConfiguredValues()
    {
        Assert.Equal(-250, new DefaultExecutionOrder(-250).order);
        Assert.Equal("https://docs.anity.dev/component", new HelpURLAttribute("https://docs.anity.dev/component").URL);
    }

    [Fact]
    public void AddComponentAutomaticallyAddsSingleRequiredComponent()
    {
        var gameObject = new GameObject("single-requirement");
        try
        {
            NeedsA component = gameObject.AddComponent<NeedsA>();

            Assert.Same(component, gameObject.GetComponent<NeedsA>());
            Assert.NotNull(gameObject.GetComponent<DependencyA>());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void AddComponentAddsAllThreeRequiredComponents()
    {
        var gameObject = new GameObject("three-requirements");
        try
        {
            gameObject.AddComponent<NeedsThree>();

            Assert.NotNull(gameObject.GetComponent<DependencyA>());
            Assert.NotNull(gameObject.GetComponent<DependencyB>());
            Assert.NotNull(gameObject.GetComponent<DependencyC>());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void AddComponentHonorsInheritedRequireComponent()
    {
        var gameObject = new GameObject("inherited-requirement");
        try
        {
            gameObject.AddComponent<DerivedNeedsA>();

            Assert.NotNull(gameObject.GetComponent<DependencyA>());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void CircularRequireComponentGraphAddsEachComponentOnce()
    {
        var gameObject = new GameObject("circular-requirement");
        try
        {
            gameObject.AddComponent<CircularA>();

            Assert.NotNull(gameObject.GetComponent<CircularA>());
            Assert.NotNull(gameObject.GetComponent<CircularB>());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void RequiredComponentAwakesBeforeDependentComponent()
    {
        LifecycleRecorder.Events.Clear();
        var gameObject = new GameObject("requirement-lifecycle");
        try
        {
            gameObject.AddComponent<LifecycleDependent>();

            Assert.Equal(new[] { "required", "dependent" }, LifecycleRecorder.Events);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
            LifecycleRecorder.Events.Clear();
        }
    }

    [Fact]
    public void DisallowMultipleComponentRejectsDuplicateExactType()
    {
        var gameObject = new GameObject("unique-component");
        try
        {
            UniqueBase first = gameObject.AddComponent<UniqueBase>();

            Assert.Throws<InvalidOperationException>(() => gameObject.AddComponent<UniqueBase>());
            Assert.Same(first, gameObject.GetComponent<UniqueBase>());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void DisallowMultipleComponentOnBaseRejectsDerivedSibling()
    {
        var gameObject = new GameObject("unique-derived-component");
        try
        {
            gameObject.AddComponent<UniqueDerivedA>();

            Assert.Throws<InvalidOperationException>(() => gameObject.AddComponent<UniqueDerivedB>());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void InvalidRequiredComponentTypeFailsBeforeDependentIsRegistered()
    {
        var gameObject = new GameObject("invalid-requirement");
        try
        {
            Assert.Throws<InvalidOperationException>(() => gameObject.AddComponent<NeedsInvalidType>());
            Assert.Null(gameObject.GetComponent<NeedsInvalidType>());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void DefaultExecutionOrderControlsUpdateOrdering()
    {
        ExecutionRecorder.Events.Clear();
        var gameObject = new GameObject("execution-order");
        try
        {
            gameObject.AddComponent<LateBehaviour>();
            gameObject.AddComponent<DefaultBehaviour>();
            gameObject.AddComponent<EarlyBehaviour>();

            UnityRuntime.Tick(0.001f);

            Assert.Equal(new[] { "early", "default", "late" }, ExecutionRecorder.Events);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
            ExecutionRecorder.Events.Clear();
        }
    }

    public sealed class DependencyA : MonoBehaviour { }
    public sealed class DependencyB : MonoBehaviour { }
    public sealed class DependencyC : MonoBehaviour { }

    [RequireComponent(typeof(DependencyA))]
    public sealed class NeedsA : MonoBehaviour { }

    [RequireComponent(typeof(DependencyA), typeof(DependencyB), typeof(DependencyC))]
    public sealed class NeedsThree : MonoBehaviour { }

    [RequireComponent(typeof(DependencyA))]
    public class BaseNeedsA : MonoBehaviour { }

    public sealed class DerivedNeedsA : BaseNeedsA { }

    [RequireComponent(typeof(CircularB))]
    public sealed class CircularA : MonoBehaviour { }

    [RequireComponent(typeof(CircularA))]
    public sealed class CircularB : MonoBehaviour { }

    public static class LifecycleRecorder
    {
        public static readonly List<string> Events = new();
    }

    public sealed class LifecycleRequired : MonoBehaviour
    {
        private void Awake() => LifecycleRecorder.Events.Add("required");
    }

    [RequireComponent(typeof(LifecycleRequired))]
    public sealed class LifecycleDependent : MonoBehaviour
    {
        private void Awake() => LifecycleRecorder.Events.Add("dependent");
    }

    [DisallowMultipleComponent]
    public class UniqueBase : MonoBehaviour { }

    public sealed class UniqueDerivedA : UniqueBase { }
    public sealed class UniqueDerivedB : UniqueBase { }

    [RequireComponent(typeof(string))]
    public sealed class NeedsInvalidType : MonoBehaviour { }

    public static class ExecutionRecorder
    {
        public static readonly List<string> Events = new();
    }

    [DefaultExecutionOrder(-100)]
    public sealed class EarlyBehaviour : MonoBehaviour
    {
        private void Update() => ExecutionRecorder.Events.Add("early");
    }

    public sealed class DefaultBehaviour : MonoBehaviour
    {
        private void Update() => ExecutionRecorder.Events.Add("default");
    }

    [DefaultExecutionOrder(100)]
    public sealed class LateBehaviour : MonoBehaviour
    {
        private void Update() => ExecutionRecorder.Events.Add("late");
    }
}
