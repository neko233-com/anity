using Anity.Core.Runtime.Native;

namespace UnityEngine;

[Bindings.NativeHeader("Modules/Physics/ConstantForce.h")]
[RequireComponent(typeof(Rigidbody))]
public class ConstantForce : Behaviour
{
  public Vector3 force { get; set; }
  public Vector3 relativeForce { get; set; }
  public Vector3 torque { get; set; }
  public Vector3 relativeTorque { get; set; }

  internal void Resolve(out Vector3 worldForce, out Vector3 worldTorque)
  {
    Quaternion rotation = transform?.rotation ?? Quaternion.identity;
    if (AnityNative.TryResolveConstantForce3D(
        force.x, force.y, force.z,
        relativeForce.x, relativeForce.y, relativeForce.z,
        torque.x, torque.y, torque.z,
        relativeTorque.x, relativeTorque.y, relativeTorque.z,
        rotation.x, rotation.y, rotation.z, rotation.w,
        out AnityNative.Vec3 resolvedForce, out AnityNative.Vec3 resolvedTorque))
    {
      worldForce = new Vector3(resolvedForce.x, resolvedForce.y, resolvedForce.z);
      worldTorque = new Vector3(resolvedTorque.x, resolvedTorque.y, resolvedTorque.z);
      return;
    }

    worldForce = force + rotation * relativeForce;
    worldTorque = torque + rotation * relativeTorque;
  }
}

[Bindings.NativeHeader("Modules/Physics2D/PhysicsUpdateBehaviour2D.h")]
public class PhysicsUpdateBehaviour2D : Behaviour
{
}

[Bindings.NativeHeader("Modules/Physics2D/ConstantForce2D.h")]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class ConstantForce2D : PhysicsUpdateBehaviour2D
{
  public Vector2 force { get; set; }
  public Vector2 relativeForce { get; set; }
  public float torque { get; set; }

  internal void Resolve(out Vector2 worldForce, out float worldTorque)
  {
    Quaternion rotation = transform?.rotation ?? Quaternion.identity;
    if (AnityNative.TryResolveConstantForce2D(
        force.x, force.y, relativeForce.x, relativeForce.y,
        rotation.x, rotation.y, rotation.z, rotation.w, torque,
        out AnityNative.Vec2 resolvedForce, out worldTorque))
    {
      worldForce = new Vector2(resolvedForce.x, resolvedForce.y);
      return;
    }

    Vector3 rotated = rotation * new Vector3(relativeForce.x, relativeForce.y, 0f);
    worldForce = force + new Vector2(rotated.x, rotated.y);
    worldTorque = torque;
  }
}
