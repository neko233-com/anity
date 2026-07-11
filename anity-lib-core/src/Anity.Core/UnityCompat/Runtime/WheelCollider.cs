using System;

namespace UnityEngine;

public struct WheelHit
{
    public Collider collider;
    public Vector3 point;
    public Vector3 normal;
    public Vector3 forwardDir;
    public Vector3 sideDir;
    public float force;
    public float forwardSlip;
    public float sidewaysSlip;
}

[Serializable]
public struct WheelFrictionCurve
{
    public float extremumSlip;
    public float extremumValue;
    public float asymptoteSlip;
    public float asymptoteValue;
    public float stiffness;

    public float Evaluate(float slip)
    {
        slip = MathF.Abs(slip);
        if (extremumSlip <= 0f || asymptoteSlip <= extremumSlip) return 0f;
        float normExt = extremumSlip;
        float normAsym = asymptoteSlip;
        if (slip <= normExt)
        {
            float t = slip / normExt;
            return MathF.Sin(t * MathF.PI * 0.5f) * extremumValue * stiffness;
        }
        if (slip <= normAsym)
        {
            float t = (slip - normExt) / (normAsym - normExt);
            return extremumValue * stiffness + (asymptoteValue - extremumValue) * stiffness * t;
        }
        return asymptoteValue * stiffness;
    }
}

[AddComponentMenu("Physics/Wheel Collider")]
[RequireComponent(typeof(Transform))]
public class WheelCollider : Collider
{
    private Vector3 _center;
    private float _radius = 0.5f;
    private float _suspensionDistance = 0.3f;
    private JointSpring _suspensionSpring = new JointSpring { spring = 20000f, damper = 1000f, targetPosition = 0.5f };
    private WheelFrictionCurve _forwardFriction = new WheelFrictionCurve { extremumSlip = 0.4f, extremumValue = 1f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1f };
    private WheelFrictionCurve _sidewaysFriction = new WheelFrictionCurve { extremumSlip = 0.2f, extremumValue = 1f, asymptoteSlip = 0.5f, asymptoteValue = 0.75f, stiffness = 1f };
    private float _motorTorque;
    private float _brakeTorque;
    private float _steerAngle;
    private float _mass = 20f;
    private float _sprungMass;
    private float _rpm;
    private bool _isGrounded;
    private WheelHit _lastHit;
    private float _suspensionCompression;
    private float _wheelRotationAngle;
    private Vector3 _suspensionForce;
    private int _vehicleSubsteps = 15;

    public WheelCollider()
    {
        PhysicsWorld.RegisterWheel(this);
    }

    ~WheelCollider()
    {
        PhysicsWorld.UnregisterWheel(this);
    }

    public Vector3 center
    {
        get => _center;
        set => _center = value;
    }

    public float radius
    {
        get => _radius;
        set => _radius = MathF.Max(0.01f, value);
    }

    public float suspensionDistance
    {
        get => _suspensionDistance;
        set => _suspensionDistance = MathF.Max(0f, value);
    }

    public JointSpring suspensionSpring
    {
        get => _suspensionSpring;
        set => _suspensionSpring = value;
    }

    public WheelFrictionCurve forwardFriction
    {
        get => _forwardFriction;
        set => _forwardFriction = value;
    }

    public WheelFrictionCurve sidewaysFriction
    {
        get => _sidewaysFriction;
        set => _sidewaysFriction = value;
    }

    public float motorTorque
    {
        get => _motorTorque;
        set => _motorTorque = value;
    }

    public float brakeTorque
    {
        get => _brakeTorque;
        set => _brakeTorque = MathF.Max(0f, value);
    }

    public float steerAngle
    {
        get => _steerAngle;
        set => _steerAngle = value;
    }

    public float mass
    {
        get => _mass;
        set => _mass = MathF.Max(0.01f, value);
    }

    public float sprungMass => _sprungMass;
    public float rpm => _rpm;
    public bool isGrounded => _isGrounded;
    public Vector3 suspensionForce => _suspensionForce;

    public override Bounds bounds
    {
        get
        {
            if (transform == null) return new Bounds(_center, new Vector3(_radius * 2f, _radius * 2f + _suspensionDistance, _radius * 2f));
            Vector3 scale = transform.lossyScale;
            float worldRadius = _radius * MathF.Max(MathF.Abs(scale.x), MathF.Abs(scale.z));
            float worldHeight = (_radius * 2f + _suspensionDistance) * MathF.Abs(scale.y);
            Vector3 worldCenter = transform.TransformPoint(_center);
            return new Bounds(worldCenter, new Vector3(worldRadius * 2f, worldHeight, worldRadius * 2f));
        }
    }

    public override ColliderShape GetShape()
    {
        if (transform == null)
            return new ColliderShape(ColliderShapeType.Sphere, _center, Vector3.one, _radius, 0f, 0);
        Vector3 scale = transform.lossyScale;
        float worldRadius = _radius * MathF.Max(MathF.Abs(scale.x), MathF.Abs(scale.z));
        Vector3 worldCenter = transform.TransformPoint(_center) - transform.up * _suspensionCompression;
        return new ColliderShape(ColliderShapeType.Sphere, worldCenter, Vector3.one, worldRadius, 0f, 0);
    }

    public override Vector3 ClosestPoint(Vector3 position)
    {
        Vector3 wheelCenter = GetWorldWheelCenter();
        Vector3 d = position - wheelCenter;
        float dist = d.magnitude;
        if (dist <= _radius) return position;
        return wheelCenter + d.normalized * _radius;
    }

    private Vector3 GetWorldWheelCenter()
    {
        if (transform == null) return _center;
        return transform.TransformPoint(_center) - transform.up * _suspensionCompression;
    }

    public void ConfigureVehicleSubsteps(float speedThreshold, int stepsBelowThreshold, int stepsAboveThreshold)
    {
        _vehicleSubsteps = stepsBelowThreshold > 0 ? stepsBelowThreshold : 1;
    }

    public bool GetGroundHit(out WheelHit hitInfo)
    {
        hitInfo = _lastHit;
        return _isGrounded;
    }

    public void GetWorldPose(out Vector3 pos, out Quaternion quat)
    {
        pos = GetWorldWheelCenter();
        Quaternion steerRot = Quaternion.AngleAxis(_steerAngle, Vector3.up);
        Quaternion wheelRot = Quaternion.AngleAxis(_wheelRotationAngle, Vector3.right);
        if (transform != null)
            quat = transform.rotation * steerRot * wheelRot;
        else
            quat = steerRot * wheelRot;
    }

    internal void UpdateWheel(float dt)
    {
        _isGrounded = false;
        _suspensionForce = Vector3.zero;
        _sprungMass = _mass;

        if (transform == null) return;

        Rigidbody rb = attachedRigidbody;
        Vector3 up = transform.up;
        Vector3 origin = transform.TransformPoint(_center);
        float rayLength = _suspensionDistance + _radius;

        if (PhysicsWorld.Raycast(origin, -up, out RaycastHit hit, rayLength, -1, QueryTriggerInteraction.Ignore))
        {
            _isGrounded = true;
            float hitDistance = hit.distance;
            float prevCompression = _suspensionCompression;
            _suspensionCompression = _suspensionDistance + _radius - hitDistance;
            _suspensionCompression = Math.Clamp(_suspensionCompression, 0f, _suspensionDistance + _radius);

            float springForce = (_suspensionSpring.targetPosition * _suspensionDistance - _suspensionCompression) * _suspensionSpring.spring;
            float damperForce = -(prevCompression - _suspensionCompression) / MathF.Max(dt, 0.0001f) * _suspensionSpring.damper;
            float totalSuspensionForce = springForce + damperForce;
            if (rb != null && !rb.isKinematic)
            {
                _sprungMass = rb.mass * 0.25f;
                totalSuspensionForce += _sprungMass * -Physics.gravity.y;
            }
            _suspensionForce = up * totalSuspensionForce;

            if (rb != null && !rb.isKinematic)
            {
                rb.AddForceAtPosition(_suspensionForce, origin);
            }

            Vector3 right = transform.right;
            Vector3 forward = transform.forward;
            Quaternion steerRot = Quaternion.AngleAxis(_steerAngle, up);
            Vector3 wheelForward = steerRot * forward;
            Vector3 wheelRight = steerRot * right;

            Vector3 wheelVelocity = rb != null ? rb.GetPointVelocity(origin) : Vector3.zero;
            float forwardVelocity = Vector3.Dot(wheelVelocity, wheelForward);
            float sidewaysVelocity = Vector3.Dot(wheelVelocity, wheelRight);

            float wheelAngularVelocity = _rpm * 2f * MathF.PI / 60f;
            float wheelLinearVelocity = wheelAngularVelocity * _radius;
            float forwardSlip = wheelLinearVelocity - forwardVelocity;
            float sidewaysSlip = sidewaysVelocity;

            float forwardForceMag = _forwardFriction.Evaluate(forwardSlip);
            float sidewaysForceMag = _sidewaysFriction.Evaluate(sidewaysSlip);

            float totalGripForce = totalSuspensionForce;
            Vector3 frictionForce = -wheelForward * forwardForceMag * totalGripForce - wheelRight * sidewaysForceMag * totalGripForce;

            if (rb != null && !rb.isKinematic)
            {
                rb.AddForceAtPosition(frictionForce, origin);
            }

            float driveTorque = _motorTorque;
            if (_brakeTorque > 0f)
            {
                driveTorque -= Math.Sign(wheelAngularVelocity) * _brakeTorque;
            }
            wheelAngularVelocity += driveTorque / MathF.Max(_mass * _radius * _radius, 0.01f) * dt;
            _rpm = wheelAngularVelocity * 60f / (2f * MathF.PI);
            _wheelRotationAngle += wheelAngularVelocity * Mathf.Rad2Deg * dt;

            _lastHit = new WheelHit
            {
                collider = hit.collider,
                point = hit.point,
                normal = hit.normal,
                forwardDir = wheelForward,
                sideDir = wheelRight,
                force = totalSuspensionForce,
                forwardSlip = forwardSlip,
                sidewaysSlip = sidewaysSlip
            };
        }
        else
        {
            _suspensionCompression = 0f;
            if (rb != null && !rb.isKinematic)
            {
                float wheelAngularVelocity = _rpm * 2f * MathF.PI / 60f;
                float driveTorque = _motorTorque;
                if (_brakeTorque > 0f)
                {
                    driveTorque -= Math.Sign(wheelAngularVelocity) * _brakeTorque;
                }
                wheelAngularVelocity += driveTorque / MathF.Max(_mass * _radius * _radius, 0.01f) * dt;
                _rpm = wheelAngularVelocity * 60f / (2f * MathF.PI);
                _wheelRotationAngle += wheelAngularVelocity * Mathf.Rad2Deg * dt;
            }
        }
    }
}
