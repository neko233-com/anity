using System;

namespace UnityEngine;

/// <summary>
/// Unity Joint base class for physics joints.
/// </summary>
public class Joint : Component
{
    private Rigidbody? _connectedBody;
    private Vector3 _anchor;
    private Vector3 _axis;
    private bool _autoConfigureConnectedAnchor;
    private Vector3 _connectedAnchor;
    private float _breakForce = float.PositiveInfinity;
    private float _breakTorque = float.PositiveInfinity;
    private bool _enableCollision;
    private bool _enablePreprocessing = true;
    private float _massScale = 1.0f;
    private float _connectedMassScale = 1.0f;

    public Rigidbody? connectedBody
    {
        get => _connectedBody;
        set => _connectedBody = value;
    }

    public Vector3 anchor
    {
        get => _anchor;
        set => _anchor = value;
    }

    public Vector3 axis
    {
        get => _axis;
        set => _axis = value;
    }

    public bool autoConfigureConnectedAnchor
    {
        get => _autoConfigureConnectedAnchor;
        set => _autoConfigureConnectedAnchor = value;
    }

    public Vector3 connectedAnchor
    {
        get => _connectedAnchor;
        set => _connectedAnchor = value;
    }

    public float breakForce
    {
        get => _breakForce;
        set => _breakForce = value;
    }

    public float breakTorque
    {
        get => _breakTorque;
        set => _breakTorque = value;
    }

    public bool enableCollision
    {
        get => _enableCollision;
        set => _enableCollision = value;
    }

    public bool enablePreprocessing
    {
        get => _enablePreprocessing;
        set => _enablePreprocessing = value;
    }

    public float massScale
    {
        get => _massScale;
        set => _massScale = value;
    }

    public float connectedMassScale
    {
        get => _connectedMassScale;
        set => _connectedMassScale = value;
    }

    public Vector3 currentForce { get; }
    public Vector3 currentTorque { get; }

    public void SetConnectedBody(Rigidbody? rb) => _connectedBody = rb;
    public void SetAxis(Vector3 axis) => _axis = axis;
}

/// <summary>
/// Unity FixedJoint for connecting two Rigidbodies.
/// </summary>
[AddComponentMenu("Physics/Fixed Joint")]
public class FixedJoint : Joint
{
    private float _breakForce = float.PositiveInfinity;
    private float _breakTorque = float.PositiveInfinity;

    public float breakForce
    {
        get => _breakForce;
        set => _breakForce = value;
    }

    public float breakTorque
    {
        get => _breakTorque;
        set => _breakTorque = value;
    }
}

/// <summary>
/// Unity HingeJoint for hinge-based connections.
/// </summary>
[AddComponentMenu("Physics/Hinge Joint")]
public class HingeJoint : Joint
{
    private bool _useLimits;
    private JointLimits _limits;
    private bool _useMotor;
    private JointMotor _motor;
    private bool _useSpring;
    private JointSpring _spring;

    public bool useLimits
    {
        get => _useLimits;
        set => _useLimits = value;
    }

    public JointLimits limits
    {
        get => _limits;
        set => _limits = value;
    }

    public bool useMotor
    {
        get => _useMotor;
        set => _useMotor = value;
    }

    public JointMotor motor
    {
        get => _motor;
        set => _motor = value;
    }

    public bool useSpring
    {
        get => _useSpring;
        set => _useSpring = value;
    }

    public JointSpring spring
    {
        get => _spring;
        set => _spring = value;
    }

    public float angle => 0f;
    public float velocity => 0f;
}

/// <summary>
/// Unity SpringJoint for spring-based connections.
/// </summary>
[AddComponentMenu("Physics/Spring Joint")]
public class SpringJoint : Joint
{
    private float _spring = 10.0f;
    private float _damper = 0.2f;
    private float _minDistance;
    private float _maxDistance = Mathf.Infinity;

    public float spring
    {
        get => _spring;
        set => _spring = value;
    }

    public float damper
    {
        get => _damper;
        set => _damper = value;
    }

    public float minDistance
    {
        get => _minDistance;
        set => _minDistance = value;
    }

    public float maxDistance
    {
        get => _maxDistance;
        set => _maxDistance = value;
    }
}

/// <summary>
/// Unity ConfigurableJoint for highly configurable connections.
/// </summary>
[AddComponentMenu("Physics/Configurable Joint")]
public class ConfigurableJoint : Joint
{
    private ConfigurableJointMotion _xMotion;
    private ConfigurableJointMotion _yMotion;
    private ConfigurableJointMotion _zMotion;
    private ConfigurableJointMotion _angularXMotion;
    private ConfigurableJointMotion _angularYMotion;
    private ConfigurableJointMotion _angularZMotion;
    private Vector3 _linearLimit;
    private SoftJointLimit _lowAngularXLimit;
    private SoftJointLimit _highAngularXLimit;
    private SoftJointLimit _angularYLimit;
    private SoftJointLimit _angularZLimit;
    private Vector3 _targetPosition;
    private Vector3 _targetVelocity;
    private Quaternion _targetRotation;
    private Vector3 _targetAngularVelocity;
    private JointDrive _xDrive;
    private JointDrive _yDrive;
    private JointDrive _zDrive;
    private JointDrive _angularXDrive;
    private JointDrive _angularYZDrive;
    private JointDrive _slerpDrive;
    private bool _projectionMode;
    private float _projectionDistance;
    private float _projectionAngle;
    private ConfigurableJointMotion _angularXDriveMode;

    public ConfigurableJointMotion xMotion { get => _xMotion; set => _xMotion = value; }
    public ConfigurableJointMotion yMotion { get => _yMotion; set => _yMotion = value; }
    public ConfigurableJointMotion zMotion { get => _zMotion; set => _zMotion = value; }
    public ConfigurableJointMotion angularXMotion { get => _angularXMotion; set => _angularXMotion = value; }
    public ConfigurableJointMotion angularYMotion { get => _angularYMotion; set => _angularYMotion = value; }
    public ConfigurableJointMotion angularZMotion { get => _angularZMotion; set => _angularZMotion = value; }
    public Vector3 targetPosition { get => _targetPosition; set => _targetPosition = value; }
    public Vector3 targetVelocity { get => _targetVelocity; set => _targetVelocity = value; }
    public Quaternion targetRotation { get => _targetRotation; set => _targetRotation = value; }
    public Vector3 targetAngularVelocity { get => _targetAngularVelocity; set => _targetAngularVelocity = value; }
    public JointDrive xDrive { get => _xDrive; set => _xDrive = value; }
    public JointDrive yDrive { get => _yDrive; set => _yDrive = value; }
    public JointDrive zDrive { get => _zDrive; set => _zDrive = value; }
    public JointDrive angularXDrive { get => _angularXDrive; set => _angularXDrive = value; }
    public JointDrive angularYZDrive { get => _angularYZDrive; set => _angularYZDrive = value; }
    public JointDrive slerpDrive { get => _slerpDrive; set => _slerpDrive = value; }
    public bool projectionMode { get => _projectionMode; set => _projectionMode = value; }
    public float projectionDistance { get => _projectionDistance; set => _projectionDistance = value; }
    public float projectionAngle { get => _projectionAngle; set => _projectionAngle = value; }
}

/// <summary>
/// Joint limits.
/// </summary>
public struct JointLimits
{
    public float min;
    public float max;
    public float bounciness;
    public float contactDistance;
}

/// <summary>
/// Soft joint limit.
/// </summary>
public struct SoftJointLimit
{
    public float limit;
    public float bounciness;
    public float contactDistance;
}

/// <summary>
/// Joint motor.
/// </summary>
public struct JointMotor
{
    public float targetVelocity;
    public float force;
    public bool useMotor;
}

/// <summary>
/// Joint spring.
/// </summary>
public struct JointSpring
{
    public float spring;
    public float damper;
    public float targetPosition;
}

/// <summary>
/// Joint drive.
/// </summary>
public struct JointDrive
{
    public float positionSpring;
    public float positionDamper;
    public float maximumForce;
    public float useSpring;
    public float useDamper;
    public float useAcceleration;
}

/// <summary>
/// Configurable joint motion type.
/// </summary>
public enum ConfigurableJointMotion
{
    Free,
    Limited,
    Locked
}
