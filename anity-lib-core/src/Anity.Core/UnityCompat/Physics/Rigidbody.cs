namespace UnityEngine;

public class Rigidbody : Component
{
  public Vector3 velocity { get; set; }
  public Vector3 angularVelocity { get; set; }
  public float mass { get; set; } = 1f;
  public float drag { get; set; }
  public float angularDrag { get; set; }
  public bool useGravity { get; set; } = true;
  public bool isKinematic { get; set; }
  public bool freezeRotation { get; set; }

  public void AddForce(Vector3 force)
  {
    AddForce(force, ForceMode.Force);
  }

  public void AddForce(Vector3 force, ForceMode mode)
  {
    if (isKinematic) return;
    velocity += force * (mode == ForceMode.Impulse ? 1f : 0.1f);
  }

  public void AddRelativeForce(Vector3 force)
  {
    AddForce(force);
  }

  public void AddTorque(Vector3 torque)
  {
    if (isKinematic) return;
    angularVelocity += torque;
  }

  public void MovePosition(Vector3 position)
  {
    if (gameObject is null) return;
    gameObject.transform.localPosition = position;
  }

  public void MoveRotation(Quaternion rotation)
  {
    if (gameObject is null) return;
    gameObject.transform.localRotation = rotation;
  }

  public void Sleep()
  {
    velocity = Vector3.zero;
  }
}

