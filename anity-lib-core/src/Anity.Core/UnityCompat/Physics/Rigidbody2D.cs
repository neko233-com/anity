using UnityEngine;

namespace UnityEngine;

public class Rigidbody2D : Component
{
  public Vector2 velocity { get; set; }
  public float angularVelocity { get; set; }
  public float gravityScale { get; set; } = 1f;
  public bool isKinematic { get; set; }
  public float mass { get; set; } = 1f;

  public void AddForce(Vector2 force)
  {
    if (isKinematic) return;
    velocity += force * 0.1f;
  }

  public void AddTorque(float torque)
  {
    if (isKinematic) return;
    angularVelocity += torque;
  }
}

