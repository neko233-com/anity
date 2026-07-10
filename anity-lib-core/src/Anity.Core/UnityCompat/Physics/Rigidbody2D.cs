namespace UnityEngine;

public class Rigidbody2D : Component
{
  public Vector2 velocity { get; set; }
  public float angularVelocity { get; set; }
  public float gravityScale { get; set; } = 1f;
  public bool isKinematic { get; set; }
  public RigidbodyType2D bodyType { get; set; }
  public float mass { get; set; } = 1f;
  public float drag { get; set; }
  public float angularDrag { get; set; }
  public bool freezeRotation { get; set; }
  public bool simulated { get; set; } = true;

  public Rigidbody2D()
  {
    Physics2DWorld.Register(this);
  }

  public void AddForce(Vector2 force)
  {
    AddForce(force, ForceMode2D.Force);
  }

  public void AddForce(Vector2 force, ForceMode2D mode)
  {
    if (isKinematic || bodyType == RigidbodyType2D.Static || bodyType == RigidbodyType2D.Kinematic)
    {
      return;
    }

    var multiplier = mode == ForceMode2D.Impulse ? 1f : 0.1f;
    velocity += force * multiplier;
  }

  public void AddRelativeForce(Vector2 force)
  {
    AddForce(force);
  }

  public void AddTorque(float torque)
  {
    AddTorque(torque, ForceMode2D.Force);
  }

  public void AddTorque(float torque, ForceMode2D mode)
  {
    if (isKinematic || bodyType == RigidbodyType2D.Static || bodyType == RigidbodyType2D.Kinematic || freezeRotation)
    {
      return;
    }

    var multiplier = mode == ForceMode2D.Impulse ? 1f : 0.1f;
    angularVelocity += torque * multiplier;
  }

  public void MovePosition(Vector2 position)
  {
    if (gameObject is null)
    {
      return;
    }

    var pos = gameObject.transform.localPosition;
    pos.x = position.x;
    pos.y = position.y;
    gameObject.transform.localPosition = pos;
  }

  public void MoveRotation(float angle)
  {
    if (gameObject is null || freezeRotation)
    {
      return;
    }

    var angles = gameObject.transform.localEulerAngles;
    angles.z = angle;
    gameObject.transform.localEulerAngles = angles;
  }

  public void Sleep()
  {
    velocity = Vector2.zero;
    angularVelocity = 0f;
  }
}

public enum ForceMode2D
{
  Force,
  Impulse
}
