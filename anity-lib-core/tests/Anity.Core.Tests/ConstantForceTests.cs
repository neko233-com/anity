using System.ComponentModel;
using System.Reflection;
using Anity.Core.Runtime.Native;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class ConstantForceTests
{
    [Fact]
    public void ConstantForcePublicSurfaceMatchesUnity2022Shape()
    {
        Type type = typeof(ConstantForce);

        Assert.Equal(typeof(Behaviour), type.BaseType);
        Assert.False(type.IsSealed);
        Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        Assert.Equal(
            new[] { "force", "relativeForce", "relativeTorque", "torque" },
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => property.Name).OrderBy(name => name));
        Assert.All(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), property =>
        {
            Assert.Equal(typeof(Vector3), property.PropertyType);
            Assert.True(property.CanRead);
            Assert.True(property.CanWrite);
        });
    }

    [Fact]
    public void ConstantForce2DPublicSurfaceMatchesUnity2022Shape()
    {
        Type updateType = typeof(PhysicsUpdateBehaviour2D);
        Type type = typeof(ConstantForce2D);

        Assert.Equal(typeof(Behaviour), updateType.BaseType);
        Assert.False(updateType.IsSealed);
        Assert.Equal(updateType, type.BaseType);
        Assert.True(type.IsSealed);
        Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        Assert.Equal(
            new[] { "force", "relativeForce", "torque" },
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => property.Name).OrderBy(name => name));
    }

    [Fact]
    public void ConstantForceRequiresRigidbodyBeforeItself()
    {
        var gameObject = new GameObject("constant-force-requirement");
        try
        {
            ConstantForce constantForce = gameObject.AddComponent<ConstantForce>();

            Assert.NotNull(gameObject.GetComponent<Rigidbody>());
            Assert.Equal(1, gameObject.GetComponentIndex(gameObject.GetComponent<Rigidbody>()));
            Assert.Equal(2, gameObject.GetComponentIndex(constantForce));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void ConstantForce2DRequiresRigidbody2DBeforeItself()
    {
        var gameObject = new GameObject("constant-force-2d-requirement");
        try
        {
            ConstantForce2D constantForce = gameObject.AddComponent<ConstantForce2D>();

            Assert.NotNull(gameObject.GetComponent<Rigidbody2D>());
            Assert.Equal(1, gameObject.GetComponentIndex(gameObject.GetComponent<Rigidbody2D>()));
            Assert.Equal(2, gameObject.GetComponentIndex(constantForce));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void PropertiesRoundTripIncludingNonFiniteValues()
    {
        var gameObject = new GameObject("constant-force-properties");
        try
        {
            ConstantForce force = gameObject.AddComponent<ConstantForce>();
            force.force = new Vector3(1f, -2f, float.PositiveInfinity);
            force.relativeForce = new Vector3(float.NaN, 4f, 5f);
            force.torque = new Vector3(6f, 7f, 8f);
            force.relativeTorque = new Vector3(9f, 10f, 11f);

            Assert.Equal(1f, force.force.x);
            Assert.True(float.IsPositiveInfinity(force.force.z));
            Assert.True(float.IsNaN(force.relativeForce.x));
            Assert.Equal(8f, force.torque.z);
            Assert.Equal(10f, force.relativeTorque.y);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void WorldForceUsesMassAndDeltaTimeAndRepeatsEveryStep()
    {
        var gameObject = Create3DBody(out Rigidbody rigidbody, out ConstantForce force);
        try
        {
            rigidbody.mass = 2f;
            force.force = new Vector3(4f, 0f, 0f);

            Physics.Simulate(0.5f);
            Assert.Equal(1f, rigidbody.velocity.x, 5);
            Physics.Simulate(0.5f);
            Assert.Equal(2f, rigidbody.velocity.x, 5);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void RelativeForceUsesBodyRotation()
    {
        var gameObject = Create3DBody(out Rigidbody rigidbody, out ConstantForce force);
        try
        {
            rigidbody.mass = 2f;
            rigidbody.rotation = Quaternion.Euler(0f, 0f, 90f);
            force.relativeForce = new Vector3(4f, 0f, 0f);

            Physics.Simulate(0.5f);

            Assert.Equal(0f, rigidbody.velocity.x, 4);
            Assert.Equal(1f, rigidbody.velocity.y, 4);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void WorldTorqueUsesInertiaAndDeltaTime()
    {
        var gameObject = Create3DBody(out Rigidbody rigidbody, out ConstantForce force);
        try
        {
            rigidbody.inertiaTensor = new Vector3(2f, 2f, 2f);
            force.torque = new Vector3(0f, 0f, 4f);

            Physics.Simulate(0.5f);

            Assert.Equal(1f, rigidbody.angularVelocity.z, 5);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void RelativeTorqueUsesBodyRotation()
    {
        var gameObject = Create3DBody(out Rigidbody rigidbody, out ConstantForce force);
        try
        {
            rigidbody.inertiaTensor = new Vector3(2f, 2f, 2f);
            rigidbody.rotation = Quaternion.Euler(0f, 0f, 90f);
            force.relativeTorque = new Vector3(4f, 0f, 0f);

            Physics.Simulate(0.5f);

            Assert.Equal(0f, rigidbody.angularVelocity.x, 4);
            Assert.Equal(1f, rigidbody.angularVelocity.y, 4);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void DisabledConstantForceDoesNotApply()
    {
        var gameObject = Create3DBody(out Rigidbody rigidbody, out ConstantForce force);
        try
        {
            force.force = Vector3.right * 10f;
            force.enabled = false;

            Physics.Simulate(1f);

            Assert.Equal(Vector3.zero, rigidbody.velocity);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void InactiveConstantForceDoesNotApply()
    {
        var gameObject = Create3DBody(out Rigidbody rigidbody, out ConstantForce force);
        try
        {
            force.force = Vector3.right * 10f;
            gameObject.SetActive(false);

            Physics.Simulate(1f);

            Assert.Equal(Vector3.zero, rigidbody.velocity);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void Reactivated3DBodyResumesPhysicsWithoutReregistration()
    {
        var gameObject = Create3DBody(out Rigidbody rigidbody, out ConstantForce force);
        try
        {
            force.force = Vector3.right * 2f;
            gameObject.SetActive(false);
            Physics.Simulate(0.5f);
            gameObject.SetActive(true);

            Physics.Simulate(0.5f);

            Assert.Equal(1f, rigidbody.velocity.x, 5);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void ConstantForceWakesSleepingBody()
    {
        var gameObject = Create3DBody(out Rigidbody rigidbody, out ConstantForce force);
        try
        {
            force.force = Vector3.right;
            rigidbody.Sleep();
            Assert.True(rigidbody.IsSleeping());

            Physics.Simulate(0.5f);

            Assert.False(rigidbody.IsSleeping());
            Assert.Equal(0.5f, rigidbody.velocity.x, 5);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void KinematicBodyIgnoresConstantForce()
    {
        var gameObject = Create3DBody(out Rigidbody rigidbody, out ConstantForce force);
        try
        {
            force.force = Vector3.right * 10f;
            force.torque = Vector3.forward * 10f;
            rigidbody.isKinematic = true;

            Physics.Simulate(1f);

            Assert.Equal(Vector3.zero, rigidbody.velocity);
            Assert.Equal(Vector3.zero, rigidbody.angularVelocity);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void MultipleConstantForceComponentsAccumulate()
    {
        var gameObject = Create3DBody(out Rigidbody rigidbody, out ConstantForce first);
        try
        {
            first.force = Vector3.right * 2f;
            gameObject.AddComponent<ConstantForce>().force = Vector3.right * 4f;

            Physics.Simulate(0.5f);

            Assert.Equal(3f, rigidbody.velocity.x, 5);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void RigidbodyAddRelativeForceUsesTransformRotation()
    {
        var gameObject = Create3DBody(out Rigidbody rigidbody, out _);
        try
        {
            rigidbody.rotation = Quaternion.Euler(0f, 0f, 90f);
            rigidbody.AddRelativeForce(Vector3.right * 2f, ForceMode.Impulse);

            Assert.Equal(0f, rigidbody.velocity.x, 4);
            Assert.Equal(2f, rigidbody.velocity.y, 4);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void RigidbodyAddRelativeTorqueUsesTransformRotation()
    {
        var gameObject = Create3DBody(out Rigidbody rigidbody, out _);
        try
        {
            rigidbody.inertiaTensor = Vector3.one;
            rigidbody.rotation = Quaternion.Euler(0f, 0f, 90f);
            rigidbody.AddRelativeTorque(Vector3.right * 2f, ForceMode.Impulse);

            Assert.Equal(0f, rigidbody.angularVelocity.x, 4);
            Assert.Equal(2f, rigidbody.angularVelocity.y, 4);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void ConstantForce2DWorldForceUsesMassAndRepeats()
    {
        var gameObject = Create2DBody(out Rigidbody2D rigidbody, out ConstantForce2D force);
        try
        {
            rigidbody.mass = 2f;
            force.force = Vector2.right * 4f;

            Physics2D.Simulate(0.5f);
            Assert.Equal(1f, rigidbody.velocity.x, 5);
            Physics2D.Simulate(0.5f);
            Assert.Equal(2f, rigidbody.velocity.x, 5);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void ConstantForce2DRelativeForceUsesRotation()
    {
        var gameObject = Create2DBody(out Rigidbody2D rigidbody, out ConstantForce2D force);
        try
        {
            rigidbody.mass = 2f;
            rigidbody.rotation = 90f;
            force.relativeForce = Vector2.right * 4f;

            Physics2D.Simulate(0.5f);

            Assert.Equal(0f, rigidbody.velocity.x, 4);
            Assert.Equal(1f, rigidbody.velocity.y, 4);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void ConstantForce2DTorqueUsesInertiaAndDeltaTime()
    {
        var gameObject = Create2DBody(out Rigidbody2D rigidbody, out ConstantForce2D force);
        try
        {
            rigidbody.inertia = 2f;
            force.torque = 4f;

            Physics2D.Simulate(0.5f);

            Assert.Equal(1f, rigidbody.angularVelocity, 5);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void ConstantForce2DDisabledAndUnsimulatedBodiesDoNotMove()
    {
        var disabledObject = Create2DBody(out Rigidbody2D disabledBody, out ConstantForce2D disabledForce);
        var unsimulatedObject = Create2DBody(out Rigidbody2D unsimulatedBody, out ConstantForce2D unsimulatedForce);
        try
        {
            disabledForce.force = Vector2.right * 10f;
            disabledForce.enabled = false;
            unsimulatedForce.force = Vector2.right * 10f;
            unsimulatedBody.simulated = false;

            Physics2D.Simulate(1f);

            Assert.Equal(Vector2.zero, disabledBody.velocity);
            Assert.Equal(Vector2.zero, unsimulatedBody.velocity);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(disabledObject);
            UnityEngine.Object.DestroyImmediate(unsimulatedObject);
        }
    }

    [Fact]
    public void Reactivated2DBodyResumesPhysicsWithoutReregistration()
    {
        var gameObject = Create2DBody(out Rigidbody2D rigidbody, out ConstantForce2D force);
        try
        {
            force.force = Vector2.right * 2f;
            gameObject.SetActive(false);
            Physics2D.Simulate(0.5f);
            gameObject.SetActive(true);

            Physics2D.Simulate(0.5f);

            Assert.Equal(1f, rigidbody.velocity.x, 5);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void ConstantForce2DWakesSleepingBodyAndKinematicBodyIgnoresIt()
    {
        var sleepingObject = Create2DBody(out Rigidbody2D sleepingBody, out ConstantForce2D sleepingForce);
        var kinematicObject = Create2DBody(out Rigidbody2D kinematicBody, out ConstantForce2D kinematicForce);
        try
        {
            sleepingForce.force = Vector2.right;
            sleepingBody.Sleep();
            kinematicForce.force = Vector2.right * 10f;
            kinematicBody.bodyType = RigidbodyType2D.Kinematic;

            Physics2D.Simulate(0.5f);

            Assert.False(sleepingBody.IsSleeping());
            Assert.Equal(0.5f, sleepingBody.velocity.x, 5);
            Assert.Equal(Vector2.zero, kinematicBody.velocity);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sleepingObject);
            UnityEngine.Object.DestroyImmediate(kinematicObject);
        }
    }

    [Fact]
    public void Rigidbody2DAddRelativeForceUsesTransformRotation()
    {
        var gameObject = Create2DBody(out Rigidbody2D rigidbody, out _);
        try
        {
            rigidbody.rotation = 90f;
            rigidbody.AddRelativeForce(Vector2.right * 2f, ForceMode2D.Impulse);

            Assert.Equal(0f, rigidbody.velocity.x, 4);
            Assert.Equal(2f, rigidbody.velocity.y, 4);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void ConstantForceTypesCarryRequiredNativeMetadata()
    {
        Assert.Equal(
            "Modules/Physics/ConstantForce.h",
            GetAttributeConstructorArgument(typeof(ConstantForce), "UnityEngine.Bindings.NativeHeaderAttribute"));
        Assert.Equal(
            "Modules/Physics2D/ConstantForce2D.h",
            GetAttributeConstructorArgument(typeof(ConstantForce2D), "UnityEngine.Bindings.NativeHeaderAttribute"));

        CustomAttributeData forceRequirement = Assert.Single(typeof(ConstantForce).CustomAttributes,
            attribute => attribute.AttributeType == typeof(RequireComponent));
        CustomAttributeData force2DRequirement = Assert.Single(typeof(ConstantForce2D).CustomAttributes,
            attribute => attribute.AttributeType == typeof(RequireComponent));
        Assert.Equal(typeof(Rigidbody), forceRequirement.ConstructorArguments[0].Value);
        Assert.Equal(typeof(Rigidbody2D), force2DRequirement.ConstructorArguments[0].Value);
    }

    [Fact]
    public void Native3DResolverCombinesWorldAndRotatedLocalInputs()
    {
        Quaternion rotation = Quaternion.Euler(0f, 0f, 90f);
        bool resolved = AnityNative.TryResolveConstantForce3D(
            1f, 2f, 3f, 4f, 0f, 0f,
            5f, 6f, 7f, 2f, 0f, 0f,
            rotation.x, rotation.y, rotation.z, rotation.w,
            out AnityNative.Vec3 force, out AnityNative.Vec3 torque);

        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1") Assert.True(resolved);
        if (!resolved) return;
        Assert.Equal(1f, force.x, 4);
        Assert.Equal(6f, force.y, 4);
        Assert.Equal(3f, force.z, 4);
        Assert.Equal(5f, torque.x, 4);
        Assert.Equal(8f, torque.y, 4);
        Assert.Equal(7f, torque.z, 4);
    }

    [Fact]
    public void Native2DResolverRotatesRelativeForceAndPreservesTorque()
    {
        Quaternion rotation = Quaternion.Euler(0f, 0f, 90f);
        bool resolved = AnityNative.TryResolveConstantForce2D(
            1f, 2f, 4f, 0f,
            rotation.x, rotation.y, rotation.z, rotation.w, 3f,
            out AnityNative.Vec2 force, out float torque);

        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1") Assert.True(resolved);
        if (!resolved) return;
        Assert.Equal(1f, force.x, 4);
        Assert.Equal(6f, force.y, 4);
        Assert.Equal(3f, torque, 4);
    }

    private static object? GetAttributeConstructorArgument(Type type, string attributeTypeName)
        => Assert.Single(type.CustomAttributes, attribute => attribute.AttributeType.FullName == attributeTypeName)
            .ConstructorArguments[0].Value;

    private static GameObject Create3DBody(out Rigidbody rigidbody, out ConstantForce force)
    {
        var gameObject = new GameObject("constant-force-3d");
        force = gameObject.AddComponent<ConstantForce>();
        rigidbody = gameObject.GetComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.drag = 0f;
        rigidbody.angularDrag = 0f;
        return gameObject;
    }

    private static GameObject Create2DBody(out Rigidbody2D rigidbody, out ConstantForce2D force)
    {
        var gameObject = new GameObject("constant-force-2d");
        force = gameObject.AddComponent<ConstantForce2D>();
        rigidbody = gameObject.GetComponent<Rigidbody2D>();
        rigidbody.gravityScale = 0f;
        rigidbody.drag = 0f;
        rigidbody.angularDrag = 0f;
        return gameObject;
    }
}
