using UnityEngine;

namespace UnityEngine;

public class CapsuleCollider : Collider
{
  public Vector3 center { get; set; } = Vector3.zero;
  public float radius { get; set; } = 0.5f;
  public float height { get; set; } = 2.0f;
  public int direction { get; set; }
}
