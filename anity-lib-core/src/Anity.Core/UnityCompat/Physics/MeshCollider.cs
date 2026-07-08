using UnityEngine;

namespace UnityEngine;

public class MeshCollider : Collider
{
  public object? sharedMesh { get; set; }
  public bool convex { get; set; }
  public MeshColliderCookingOptions cookingOptions { get; set; } = MeshColliderCookingOptions.None;
}
