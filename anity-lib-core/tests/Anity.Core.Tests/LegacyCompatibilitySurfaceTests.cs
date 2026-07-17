using System.ComponentModel;
using System.Reflection;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class LegacyCompatibilitySurfaceTests
{
    private const string RemovedMessage =
        "The legacy networking system has been removed in Unity 2018.2. Use Unity Multiplayer and NetworkIdentity instead.";

    private static readonly Assembly CoreAssembly = typeof(UnityEngine.Object).Assembly;

    public static TheoryData<string, string> ComponentLegacyProperties => new()
    {
        { "rigidbody", "Rigidbody" },
        { "rigidbody2D", "Rigidbody2D" },
        { "camera", "Camera" },
        { "light", "Light" },
        { "animation", "Animation" },
        { "constantForce", "ConstantForce" },
        { "renderer", "Renderer" },
        { "audio", "AudioSource" },
        { "networkView", "NetworkView" },
        { "collider", "Collider" },
        { "collider2D", "Collider2D" },
        { "hingeJoint", "HingeJoint" },
        { "particleSystem", "ParticleSystem" }
    };

    [Theory]
    [MemberData(nameof(ComponentLegacyProperties))]
    public void ComponentLegacyPropertyMatchesOfficialMetadataAndException(string propertyName, string replacementType)
    {
        PropertyInfo? candidate = typeof(UnityEngine.Component).GetProperty(
            propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.NotNull(candidate);
        PropertyInfo property = candidate!;
        Assert.Equal(typeof(UnityEngine.Component), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
        Assert.Equal(EditorBrowsableState.Never,
            Assert.Single(property.GetCustomAttributes<EditorBrowsableAttribute>()).State);

        ObsoleteAttribute obsolete = Assert.Single(property.GetCustomAttributes<ObsoleteAttribute>());
        Assert.True(obsolete.IsError);
        Assert.Equal(
            $"Property {propertyName} has been deprecated. Use GetComponent<{replacementType}>() instead. (UnityUpgradable)",
            obsolete.Message);

        var gameObject = new GameObject("legacy-component-property");
        try
        {
            TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(
                () => property.GetValue(gameObject.transform));
            NotSupportedException inner = Assert.IsType<NotSupportedException>(invocation.InnerException);
            Assert.Equal($"{propertyName} property has been deprecated", inner.Message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Theory]
    [InlineData("UnityEngine.NetworkView")]
    [InlineData("UnityEngine.NetworkViewID")]
    [InlineData("UnityEngine.NetworkPlayer")]
    [InlineData("UnityEngine.NetworkMessageInfo")]
    [InlineData("UnityEngine.NetworkStateSynchronization")]
    [InlineData("UnityEngine.RPCMode")]
    public void LegacyNetworkTypesCarryOfficialRemovedMetadata(string typeName)
    {
        Type type = RequiredType(typeName);

        ObsoleteAttribute obsolete = Assert.Single(type.GetCustomAttributes<ObsoleteAttribute>());
        Assert.True(obsolete.IsError);
        Assert.Equal(RemovedMessage, obsolete.Message);
        Assert.Equal(EditorBrowsableState.Never,
            Assert.Single(type.GetCustomAttributes<EditorBrowsableAttribute>()).State);
    }

    [Fact]
    public void RemovedNetworkEnumsExposeNoNamedValues()
    {
        Assert.Empty(Enum.GetNames(RequiredType("UnityEngine.RPCMode")));
        Assert.Empty(Enum.GetNames(RequiredType("UnityEngine.NetworkStateSynchronization")));
    }

    [Fact]
    public void NetworkPlayerConstructorThrowsOfficialNotSupportedException()
    {
        Type type = RequiredType("UnityEngine.NetworkPlayer");
        ConstructorInfo? candidate = type.GetConstructor(new[] { typeof(string), typeof(int) });
        Assert.NotNull(candidate);
        ConstructorInfo constructor = candidate!;

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(
            () => constructor.Invoke(new object[] { "127.0.0.1", 25000 }));

        AssertRemoved(invocation);
    }

    [Theory]
    [InlineData("ipAddress")]
    [InlineData("port")]
    [InlineData("guid")]
    [InlineData("externalIP")]
    [InlineData("externalPort")]
    public void NetworkPlayerPropertiesThrowOfficialNotSupportedException(string propertyName)
    {
        Type type = RequiredType("UnityEngine.NetworkPlayer");
        object instance = Activator.CreateInstance(type)!;

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(
            () => type.GetProperty(propertyName)!.GetValue(instance));

        AssertRemoved(invocation);
    }

    [Theory]
    [InlineData("unassigned", true)]
    [InlineData("isMine", false)]
    [InlineData("owner", false)]
    public void NetworkViewIdPropertiesThrowOfficialNotSupportedException(string propertyName, bool isStatic)
    {
        Type type = RequiredType("UnityEngine.NetworkViewID");
        object? instance = isStatic ? null : Activator.CreateInstance(type);

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(
            () => type.GetProperty(propertyName)!.GetValue(instance));

        AssertRemoved(invocation);
    }

    [Theory]
    [InlineData("timestamp")]
    [InlineData("sender")]
    [InlineData("networkView")]
    public void NetworkMessageInfoPropertiesThrowOfficialNotSupportedException(string propertyName)
    {
        Type type = RequiredType("UnityEngine.NetworkMessageInfo");
        object instance = Activator.CreateInstance(type)!;

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(
            () => type.GetProperty(propertyName)!.GetValue(instance));

        AssertRemoved(invocation);
    }

    [Fact]
    public void NetworkViewConstructorStillCreatesBehaviourLikeOfficialUnity()
    {
        Type type = RequiredType("UnityEngine.NetworkView");

        object instance = Activator.CreateInstance(type)!;

        Assert.IsAssignableFrom<Behaviour>(instance);
    }

    [Theory]
    [InlineData("observed")]
    [InlineData("stateSynchronization")]
    [InlineData("viewID")]
    [InlineData("group")]
    [InlineData("isMine")]
    [InlineData("owner")]
    public void NetworkViewPropertyGettersThrowOfficialNotSupportedException(string propertyName)
    {
        Type type = RequiredType("UnityEngine.NetworkView");
        object instance = Activator.CreateInstance(type)!;

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(
            () => type.GetProperty(propertyName)!.GetValue(instance));

        AssertRemoved(invocation);
    }

    [Theory]
    [InlineData("observed")]
    [InlineData("stateSynchronization")]
    [InlineData("viewID")]
    [InlineData("group")]
    public void NetworkViewPropertySettersThrowOfficialNotSupportedException(string propertyName)
    {
        Type type = RequiredType("UnityEngine.NetworkView");
        object instance = Activator.CreateInstance(type)!;
        PropertyInfo property = type.GetProperty(propertyName)!;
        object? value = property.PropertyType.IsValueType
            ? Activator.CreateInstance(property.PropertyType)
            : null;

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(
            () => property.SetValue(instance, value));

        AssertRemoved(invocation);
    }

    [Fact]
    public void NetworkViewRpcOverloadsHaveParamArraysAndThrowOfficialException()
    {
        Type type = RequiredType("UnityEngine.NetworkView");
        object instance = Activator.CreateInstance(type)!;
        MethodInfo[] overloads = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == "RPC").ToArray();

        Assert.Equal(2, overloads.Length);
        foreach (MethodInfo overload in overloads)
        {
            ParameterInfo[] parameters = overload.GetParameters();
            Assert.Equal(typeof(string), parameters[0].ParameterType);
            Assert.True(parameters[2].IsDefined(typeof(ParamArrayAttribute), false));
            object target = Activator.CreateInstance(parameters[1].ParameterType)!;
            TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(
                () => overload.Invoke(instance, new object[] { "RemovedRpc", target, Array.Empty<object>() }));
            AssertRemoved(invocation);
        }
    }

    [Fact]
    public void NetworkViewPublicSurfaceMatchesUnity2022RemovedShell()
    {
        Type type = RequiredType("UnityEngine.NetworkView");

        Assert.Equal(typeof(Behaviour), type.BaseType);
        Assert.False(type.IsSealed);
        Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        Assert.Equal(
            new[] { "group", "isMine", "observed", "owner", "stateSynchronization", "viewID" },
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => property.Name).OrderBy(name => name));
        Assert.Equal(2, type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(method => method.Name == "RPC"));
    }

    private static Type RequiredType(string fullName)
    {
        Type? type = CoreAssembly.GetType(fullName, throwOnError: false);
        Assert.NotNull(type);
        return type!;
    }

    private static void AssertRemoved(TargetInvocationException invocation)
    {
        NotSupportedException inner = Assert.IsType<NotSupportedException>(invocation.InnerException);
        Assert.Equal(RemovedMessage, inner.Message);
    }
}
