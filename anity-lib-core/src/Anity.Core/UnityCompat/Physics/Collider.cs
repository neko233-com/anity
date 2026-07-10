using UnityEngine;

namespace UnityEngine;

public class Collider : Component
{
  public bool enabled { get; set; } = true;
  public bool isTrigger { get; set; }
  public Rigidbody? attachedRigidbody { get; set; }
  public Bounds bounds { get; set; } = new Bounds(Vector3.zero, Vector3.one);
  public object? sharedMaterial { get; set; }
  public PhysicMaterial? sharedMaterialInstance;

  public Collider()
  {
    PhysicsWorld.Register(this);
  }

  public virtual Vector3 ClosestPoint(Vector3 point)
  {
    return point;
  }

  public virtual bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance = 1000f)
  {
    hitInfo = new RaycastHit { point = ray.origin };
    return Physics.Raycast(ray, out hitInfo, maxDistance);
  }

  public Vector3 ClosestPointOnBounds(Vector3 position)
  {
    return ClosestPoint(position);
  }
}

public class PhysicMaterial
{
  public string name { get; set; } = string.Empty;
  public float dynamicFriction { get; set; } = 0.6f;
  public float staticFriction { get; set; } = 0.6f;
  public float bounciness { get; set; }
  public PhysicMaterialCombine frictionCombine { get; set; } = PhysicMaterialCombine.Average;
  public PhysicMaterialCombine bounceCombine { get; set; } = PhysicMaterialCombine.Average;
}

public enum PhysicMaterialCombine
{
  Average,
  Minimum,
  Maximum,
  Multiply
}
