namespace UnityEngine;

public class Rigidbody2D : Component
{
  private Vector2 _velocity;
  private float _angularVelocity;
  private bool _isSleeping;
  private Vector2 _accumulatedForce;
  private float _accumulatedTorque;

  public Vector2 velocity
  {
    get => _velocity;
    set
    {
      _velocity = value;
      WakeUp();
    }
  }

  public float angularVelocity
  {
    get => _angularVelocity;
    set
    {
      _angularVelocity = value;
      WakeUp();
    }
  }

  public float gravityScale { get; set; } = 1f;
  public bool isKinematic { get; set; }
  public RigidbodyType2D bodyType { get; set; }
  public float mass { get; set; } = 1f;
  public float drag { get; set; }
  public float angularDrag { get; set; } = 0.05f;
  public bool freezeRotation { get; set; }
  public bool simulated { get; set; } = true;
  public float sleepThreshold { get; set; } = 0.005f;
  public CollisionDetectionMode2D collisionDetectionMode { get; set; }
  public RigidbodyInterpolation2D interpolation { get; set; }
  public Vector2 centerOfMass { get; set; }
  public Vector2 worldCenterOfMass => transform != null ? (Vector2)transform.TransformPoint(new Vector3(centerOfMass.x, centerOfMass.y, 0f)) : centerOfMass;
  public float inertia { get; set; } = 1f;
  public Vector2 position
  {
    get => transform != null ? (Vector2)transform.position : Vector2.zero;
    set
    {
      if (transform != null) transform.position = new Vector3(value.x, value.y, transform.position.z);
      WakeUp();
    }
  }
  public float rotation
  {
    get => transform != null ? transform.eulerAngles.z : 0f;
    set
    {
      if (transform != null)
      {
        var angles = transform.eulerAngles;
        angles.z = value;
        transform.eulerAngles = angles;
      }
      WakeUp();
    }
  }

  public Rigidbody2D()
  {
    Physics2D.s_world2D.Register(this);
  }

  ~Rigidbody2D()
  {
    Physics2D.s_world2D.Unregister(this);
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

    WakeUp();
    switch (mode)
    {
      case ForceMode2D.Force:
        _accumulatedForce += force;
        break;
      case ForceMode2D.Impulse:
        _velocity += force / mass;
        break;
    }
  }

  public void AddRelativeForce(Vector2 force)
  {
    AddForce(force);
  }

  public void AddRelativeForce(Vector2 force, ForceMode2D mode)
  {
    AddForce(force, mode);
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

    WakeUp();
    switch (mode)
    {
      case ForceMode2D.Force:
        _accumulatedTorque += torque;
        break;
      case ForceMode2D.Impulse:
        _angularVelocity += torque / MathF.Max(inertia, 0.001f);
        break;
    }
  }

  public void AddForceAtPosition(Vector2 force, Vector2 position)
  {
    AddForceAtPosition(force, position, ForceMode2D.Force);
  }

  public void AddForceAtPosition(Vector2 force, Vector2 position, ForceMode2D mode)
  {
    if (isKinematic || bodyType == RigidbodyType2D.Static || bodyType == RigidbodyType2D.Kinematic) return;
    WakeUp();
    Vector2 r = position - worldCenterOfMass;
    float torque = r.x * force.y - r.y * force.x;
    AddForce(force, mode);
    AddTorque(torque, mode);
  }

  public void MovePosition(Vector2 position)
  {
    if (gameObject is null)
    {
      return;
    }

    if (isKinematic || bodyType == RigidbodyType2D.Kinematic)
    {
      gameObject.transform.position = new Vector3(position.x, position.y, gameObject.transform.position.z);
    }
    else
    {
      _velocity = (position - (Vector2)gameObject.transform.position) / Time.fixedDeltaTime;
    }
  }

  public void MoveRotation(float angle)
  {
    if (gameObject is null || freezeRotation)
    {
      return;
    }

    if (isKinematic || bodyType == RigidbodyType2D.Kinematic)
    {
      var angles = gameObject.transform.localEulerAngles;
      angles.z = angle;
      gameObject.transform.localEulerAngles = angles;
    }
    else
    {
      float currentAngle = gameObject.transform.localEulerAngles.z;
      float delta = Mathf.DeltaAngle(currentAngle, angle);
      _angularVelocity = delta * Mathf.Deg2Rad / Time.fixedDeltaTime;
    }
  }

  public void Sleep()
  {
    _isSleeping = true;
    _velocity = Vector2.zero;
    _angularVelocity = 0f;
    _accumulatedForce = Vector2.zero;
    _accumulatedTorque = 0f;
  }

  public void WakeUp()
  {
    _isSleeping = false;
  }

  public bool IsSleeping() => _isSleeping;

  internal bool isSleeping
  {
    get => _isSleeping;
    set => _isSleeping = value;
  }

  public Vector2 GetPoint(Vector2 relativePoint)
  {
    if (transform == null) return relativePoint;
    return (Vector2)transform.TransformPoint(new Vector3(relativePoint.x, relativePoint.y, 0f));
  }

  public Vector2 GetRelativePoint(Vector2 relativePoint)
  {
    if (transform == null) return relativePoint;
    return (Vector2)transform.InverseTransformPoint(new Vector3(relativePoint.x, relativePoint.y, 0f));
  }

  public Vector2 GetPointVelocity(Vector2 worldPoint)
  {
    Vector2 r = worldPoint - worldCenterOfMass;
    return _velocity + new Vector2(-_angularVelocity * r.y, _angularVelocity * r.x) * Mathf.Deg2Rad;
  }

  public Vector2 GetRelativePointVelocity(Vector2 relativePoint)
  {
    Vector2 worldPoint = GetPoint(relativePoint);
    return GetPointVelocity(worldPoint);
  }

  internal void ApplyForces(float dt)
  {
    if (isKinematic || bodyType != RigidbodyType2D.Dynamic || _isSleeping || !simulated) return;

    _velocity += _accumulatedForce / mass * dt;
    if (gravityScale != 0f) _velocity += Physics2D.gravity * gravityScale * dt;
    if (drag > 0f) _velocity *= Math.Max(0f, 1f - drag * dt);

    _angularVelocity += _accumulatedTorque / MathF.Max(inertia, 0.001f) * dt;
    if (angularDrag > 0f) _angularVelocity *= Math.Max(0f, 1f - angularDrag * dt);

    _accumulatedForce = Vector2.zero;
    _accumulatedTorque = 0f;
  }

  internal void Integrate(float dt)
  {
    if (isKinematic || bodyType != RigidbodyType2D.Dynamic || _isSleeping || !simulated) return;

    if (transform != null)
    {
      var pos = transform.position;
      pos.x += _velocity.x * dt;
      pos.y += _velocity.y * dt;
      transform.position = pos;

      if (!freezeRotation && MathF.Abs(_angularVelocity) > 1e-6f)
      {
        var angles = transform.eulerAngles;
        angles.z += _angularVelocity * dt * Mathf.Rad2Deg;
        transform.eulerAngles = angles;
      }
    }

    float kineticEnergy = _velocity.sqrMagnitude + _angularVelocity * _angularVelocity;
    if (kineticEnergy < sleepThreshold * sleepThreshold)
    {
      Sleep();
    }
  }
}

public enum ForceMode2D
{
  Force,
  Impulse
}

public enum RigidbodyType2D
{
  Dynamic,
  Kinematic,
  Static
}

public enum CollisionDetectionMode2D
{
  Discrete,
  Continuous
}

public enum RigidbodyInterpolation2D
{
  None,
  Interpolate,
  Extrapolate
}
