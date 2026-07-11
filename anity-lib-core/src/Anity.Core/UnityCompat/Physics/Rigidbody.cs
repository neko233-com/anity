namespace UnityEngine;

[Flags]
public enum RigidbodyConstraints
{
    None = 0,
    FreezePositionX = 1 << 0,
    FreezePositionY = 1 << 1,
    FreezePositionZ = 1 << 2,
    FreezeRotationX = 1 << 3,
    FreezeRotationY = 1 << 4,
    FreezeRotationZ = 1 << 5,
    FreezePosition = FreezePositionX | FreezePositionY | FreezePositionZ,
    FreezeRotation = FreezeRotationX | FreezeRotationY | FreezeRotationZ,
    FreezeAll = FreezePosition | FreezeRotation
}

public enum CollisionDetectionMode
{
    Discrete,
    Continuous,
    ContinuousDynamic,
    ContinuousSpeculative
}

public enum RigidbodyInterpolation
{
    None,
    Interpolate,
    Extrapolate
}

public class Rigidbody : Component
{
    private Vector3 _velocity;
    private Vector3 _angularVelocity;
    private Vector3 _centerOfMass;
    private bool _useCustomCenterOfMass;
    private Vector3 _inertiaTensor = Vector3.one;
    private bool _useCustomInertiaTensor;
    private Quaternion _inertiaTensorRotation = Quaternion.identity;
    private bool _isSleeping;
    private Vector3 _accumulatedForce;
    private Vector3 _accumulatedTorque;
    private float _maxAngularVelocity = 7f;
    private float _maxDepenetrationVelocity = 10f;
    private Vector3 _previousPosition;
    private Quaternion _previousRotation = Quaternion.identity;

    public Vector3 velocity
    {
        get => _velocity;
        set
        {
            _velocity = value;
            WakeUp();
        }
    }

    public Vector3 angularVelocity
    {
        get => _angularVelocity;
        set
        {
            _angularVelocity = value;
            WakeUp();
        }
    }

    public Vector3 position
    {
        get => transform != null ? transform.position : Vector3.zero;
        set
        {
            if (transform != null) transform.position = value;
            WakeUp();
        }
    }

    public Quaternion rotation
    {
        get => transform != null ? transform.rotation : Quaternion.identity;
        set
        {
            if (transform != null) transform.rotation = value;
            WakeUp();
        }
    }

    public bool useGravity { get; set; } = true;
    public bool isKinematic { get; set; }
    public float mass { get; set; } = 1f;
    public float drag { get; set; }
    public float angularDrag { get; set; } = 0.05f;
    public bool freezeRotation { get; set; }
    public RigidbodyConstraints constraints { get; set; }
    public CollisionDetectionMode collisionDetectionMode { get; set; }
    public RigidbodyInterpolation interpolation { get; set; }
    public float sleepThreshold { get; set; } = 0.005f;
    public float maxAngularVelocity
    {
        get => _maxAngularVelocity;
        set => _maxAngularVelocity = value;
    }
    public float maxDepenetrationVelocity
    {
        get => _maxDepenetrationVelocity;
        set => _maxDepenetrationVelocity = value;
    }

    public Vector3 centerOfMass
    {
        get => _useCustomCenterOfMass ? _centerOfMass : CalculateCenterOfMass();
        set
        {
            _centerOfMass = value;
            _useCustomCenterOfMass = true;
        }
    }

    public Vector3 worldCenterOfMass
    {
        get
        {
            if (transform == null) return centerOfMass;
            return transform.TransformPoint(centerOfMass);
        }
    }

    public Vector3 inertiaTensor
    {
        get => _useCustomInertiaTensor ? _inertiaTensor : CalculateInertiaTensor();
        set
        {
            _inertiaTensor = value;
            _useCustomInertiaTensor = true;
        }
    }

    public Quaternion inertiaTensorRotation
    {
        get => _inertiaTensorRotation;
        set => _inertiaTensorRotation = value;
    }

    public Rigidbody()
    {
        Physics.s_world.Register(this);
    }

    ~Rigidbody()
    {
        Physics.s_world.UnregisterRigidbody(this);
    }

    private Vector3 CalculateCenterOfMass()
    {
        return Vector3.zero;
    }

    private Vector3 CalculateInertiaTensor()
    {
        return new Vector3(1f, 1f, 1f) * mass;
    }

    public void AddForce(Vector3 force)
    {
        AddForce(force, ForceMode.Force);
    }

    public void AddForce(float x, float y, float z)
    {
        AddForce(new Vector3(x, y, z), ForceMode.Force);
    }

    public void AddForce(float x, float y, float z, ForceMode mode)
    {
        AddForce(new Vector3(x, y, z), mode);
    }

    public void AddForce(Vector3 force, ForceMode mode)
    {
        if (isKinematic) return;
        WakeUp();
        switch (mode)
        {
            case ForceMode.Force:
                _accumulatedForce += force;
                break;
            case ForceMode.Acceleration:
                _accumulatedForce += force * mass;
                break;
            case ForceMode.Impulse:
                _velocity += force / mass;
                break;
            case ForceMode.VelocityChange:
                _velocity += force;
                break;
        }
    }

    public void AddRelativeForce(Vector3 force)
    {
        AddForce(force);
    }

    public void AddRelativeForce(Vector3 force, ForceMode mode)
    {
        AddForce(force, mode);
    }

    public void AddTorque(Vector3 torque)
    {
        AddTorque(torque, ForceMode.Force);
    }

    public void AddTorque(float x, float y, float z)
    {
        AddTorque(new Vector3(x, y, z), ForceMode.Force);
    }

    public void AddTorque(float x, float y, float z, ForceMode mode)
    {
        AddTorque(new Vector3(x, y, z), mode);
    }

    public void AddTorque(Vector3 torque, ForceMode mode)
    {
        if (isKinematic || freezeRotation) return;
        WakeUp();
        switch (mode)
        {
            case ForceMode.Force:
                _accumulatedTorque += torque;
                break;
            case ForceMode.Acceleration:
                _accumulatedTorque += torque * mass;
                break;
            case ForceMode.Impulse:
                _angularVelocity += new Vector3(torque.x / MathF.Max(inertiaTensor.x, 0.001f),
                    torque.y / MathF.Max(inertiaTensor.y, 0.001f),
                    torque.z / MathF.Max(inertiaTensor.z, 0.001f));
                break;
            case ForceMode.VelocityChange:
                _angularVelocity += new Vector3(torque.x / MathF.Max(inertiaTensor.x, 0.001f),
                    torque.y / MathF.Max(inertiaTensor.y, 0.001f),
                    torque.z / MathF.Max(inertiaTensor.z, 0.001f)) * mass;
                break;
        }
    }

    public void AddRelativeTorque(Vector3 torque)
    {
        AddTorque(torque);
    }

    public void AddRelativeTorque(Vector3 torque, ForceMode mode)
    {
        AddTorque(torque, mode);
    }

    public void AddForceAtPosition(Vector3 force, Vector3 position)
    {
        AddForceAtPosition(force, position, ForceMode.Force);
    }

    public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode)
    {
        if (isKinematic) return;
        WakeUp();
        Vector3 r = position - worldCenterOfMass;
        Vector3 torque = Vector3.Cross(r, force);
        AddForce(force, mode);
        AddTorque(torque, mode);
    }

    public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius)
    {
        AddExplosionForce(explosionForce, explosionPosition, explosionRadius, 0f);
    }

    public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius, float upwardsModifier)
    {
        AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upwardsModifier, ForceMode.Force);
    }

    public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius, float upwardsModifier, ForceMode mode)
    {
        if (isKinematic) return;
        Vector3 pos = worldCenterOfMass;
        Vector3 dir = pos - explosionPosition;
        float dist = dir.magnitude;
        if (dist > explosionRadius) return;
        dir = dist > 1e-6f ? dir / dist : Vector3.up;
        if (upwardsModifier != 0f)
        {
            dir = (dir + Vector3.up * upwardsModifier).normalized;
        }
        float falloff = 1f - (dist / explosionRadius);
        Vector3 force = dir * explosionForce * falloff;
        AddForce(force, mode);
    }

    public void MovePosition(Vector3 position)
    {
        if (transform == null) return;
        if (isKinematic)
        {
            transform.position = position;
            _previousPosition = position;
        }
        else
        {
            _velocity = (position - transform.position) / Time.fixedDeltaTime;
        }
    }

    public void MoveRotation(Quaternion rotation)
    {
        if (transform == null) return;
        if (isKinematic)
        {
            transform.rotation = rotation;
            _previousRotation = rotation;
        }
        else
        {
            Quaternion delta = rotation * Quaternion.Inverse(transform.rotation);
            _angularVelocity = new Vector3(delta.x, delta.y, delta.z) * 2f / Time.fixedDeltaTime;
        }
    }

    public void Sleep()
    {
        _isSleeping = true;
        _velocity = Vector3.zero;
        _angularVelocity = Vector3.zero;
        _accumulatedForce = Vector3.zero;
        _accumulatedTorque = Vector3.zero;
    }

    public void WakeUp()
    {
        _isSleeping = false;
    }

    public bool IsSleeping() => _isSleeping;

    internal void CheckSleep(float threshold)
    {
        if (isKinematic) return;
        float kineticEnergy = _velocity.sqrMagnitude * mass + _angularVelocity.sqrMagnitude * Mathf.Max(inertiaTensor.x, inertiaTensor.y, inertiaTensor.z);
        _isSleeping = kineticEnergy < threshold * threshold;
    }

    public bool SweepTest(Vector3 direction, out RaycastHit hitInfo)
    {
        return SweepTest(direction, out hitInfo, float.PositiveInfinity);
    }

    public bool SweepTest(Vector3 direction, out RaycastHit hitInfo, float maxDistance)
    {
        hitInfo = default;
        if (transform == null) return false;
        return Physics.s_world.SphereCast(worldCenterOfMass, 0.5f, direction.normalized, out hitInfo, maxDistance, -1, QueryTriggerInteraction.Ignore);
    }

    public RaycastHit[] SweepTestAll(Vector3 direction)
    {
        return SweepTestAll(direction, float.PositiveInfinity);
    }

    public RaycastHit[] SweepTestAll(Vector3 direction, float maxDistance)
    {
        direction = direction.normalized;
        var hits = Physics.s_world.SphereCastAll(worldCenterOfMass, 0.5f, direction, maxDistance, -1, QueryTriggerInteraction.Ignore);
        return hits;
    }

    public Vector3 GetPointVelocity(Vector3 worldPoint)
    {
        Vector3 r = worldPoint - worldCenterOfMass;
        return _velocity + Vector3.Cross(_angularVelocity, r);
    }

    public Vector3 GetRelativePointVelocity(Vector3 relativePoint)
    {
        Vector3 worldPoint = transform != null ? transform.TransformPoint(relativePoint) : relativePoint;
        return GetPointVelocity(worldPoint);
    }

    public void ResetCenterOfMass()
    {
        _useCustomCenterOfMass = false;
        _centerOfMass = Vector3.zero;
    }

    public void ResetInertiaTensor()
    {
        _useCustomInertiaTensor = false;
        _inertiaTensor = Vector3.one;
        _inertiaTensorRotation = Quaternion.identity;
    }

    internal void ApplyForces(float dt)
    {
        if (isKinematic || _isSleeping) return;

        _velocity += _accumulatedForce / mass * dt;
        if (useGravity) _velocity += Physics.gravity * dt;
        _velocity *= (1f - MathF.Min(drag * dt, 1f));

        Vector3 angAccel = new Vector3(
            _accumulatedTorque.x / MathF.Max(inertiaTensor.x, 0.001f),
            _accumulatedTorque.y / MathF.Max(inertiaTensor.y, 0.001f),
            _accumulatedTorque.z / MathF.Max(inertiaTensor.z, 0.001f));
        _angularVelocity += angAccel * dt;
        _angularVelocity *= (1f - MathF.Min(angularDrag * dt, 1f));

        float angSpeed = _angularVelocity.magnitude;
        if (angSpeed > _maxAngularVelocity)
        {
            _angularVelocity = _angularVelocity.normalized * _maxAngularVelocity;
        }

        _accumulatedForce = Vector3.zero;
        _accumulatedTorque = Vector3.zero;

        ApplyConstraints();
    }

    internal void Integrate(float dt)
    {
        if (isKinematic || _isSleeping) return;

        if (transform != null)
        {
            _previousPosition = transform.position;
            _previousRotation = transform.rotation;
            transform.position += _velocity * dt;

            float angle = _angularVelocity.magnitude * dt;
            if (angle > 1e-6f)
            {
                Vector3 axis = _angularVelocity.normalized;
                Quaternion q = new Quaternion(axis.x * MathF.Sin(angle * 0.5f),
                    axis.y * MathF.Sin(angle * 0.5f),
                    axis.z * MathF.Sin(angle * 0.5f),
                    MathF.Cos(angle * 0.5f));
                transform.rotation = (q * transform.rotation).normalized;
            }
        }

        float kineticEnergy = _velocity.sqrMagnitude + _angularVelocity.sqrMagnitude;
        if (kineticEnergy < sleepThreshold * sleepThreshold)
        {
            Sleep();
        }
    }

    private void ApplyConstraints()
    {
        if ((constraints & RigidbodyConstraints.FreezePositionX) != 0) _velocity.x = 0f;
        if ((constraints & RigidbodyConstraints.FreezePositionY) != 0) _velocity.y = 0f;
        if ((constraints & RigidbodyConstraints.FreezePositionZ) != 0) _velocity.z = 0f;
        if (freezeRotation || (constraints & RigidbodyConstraints.FreezeRotation) != 0)
        {
            _angularVelocity = Vector3.zero;
        }
        else
        {
            if ((constraints & RigidbodyConstraints.FreezeRotationX) != 0) _angularVelocity.x = 0f;
            if ((constraints & RigidbodyConstraints.FreezeRotationY) != 0) _angularVelocity.y = 0f;
            if ((constraints & RigidbodyConstraints.FreezeRotationZ) != 0) _angularVelocity.z = 0f;
        }
    }

    public Vector3 ClosestPointOnBounds(Vector3 position)
    {
        Bounds b = new Bounds(worldCenterOfMass, Vector3.one);
        Vector3 min = b.min;
        Vector3 max = b.max;
        return new Vector3(
            Math.Clamp(position.x, min.x, max.x),
            Math.Clamp(position.y, min.y, max.y),
            Math.Clamp(position.z, min.z, max.z));
    }
}
