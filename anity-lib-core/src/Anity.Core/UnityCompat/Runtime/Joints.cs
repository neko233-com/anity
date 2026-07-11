using System;

namespace UnityEngine;

public class Joint : Component
{
    private Rigidbody _connectedBody;
    private Vector3 _anchor;
    private Vector3 _axis = Vector3.right;
    private Vector3 _secondaryAxis = Vector3.up;
    private bool _autoConfigureConnectedAnchor = true;
    private Vector3 _connectedAnchor;
    private float _breakForce = float.PositiveInfinity;
    private float _breakTorque = float.PositiveInfinity;
    private bool _enableCollision;
    private bool _enablePreprocessing = true;
    private float _massScale = 1f;
    private float _connectedMassScale = 1f;
    private Vector3 _currentForce;
    private Vector3 _currentTorque;

    public Rigidbody connectedBody
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
        set => _axis = value.normalized;
    }

    public Vector3 secondaryAxis
    {
        get => _secondaryAxis;
        set => _secondaryAxis = value.normalized;
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

    public Vector3 currentForce => _currentForce;
    public Vector3 currentTorque => _currentTorque;

    public Joint()
    {
        PhysicsWorld.RegisterJoint(this);
    }

    ~Joint()
    {
        PhysicsWorld.UnregisterJoint(this);
    }

    internal void SetCurrentForce(Vector3 f) => _currentForce = f;
    internal void SetCurrentTorque(Vector3 t) => _currentTorque = t;
}

[AddComponentMenu("Physics/Fixed Joint")]
public class FixedJoint : Joint
{
}

[AddComponentMenu("Physics/Hinge Joint")]
public class HingeJoint : Joint
{
    private bool _useLimits;
    private JointLimits _limits;
    private bool _useMotor;
    private JointMotor _motor;
    private bool _useSpring;
    private JointSpring _spring;
    private float _angle;
    private float _velocity;

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

    public float angle => _angle;
    public float velocity => _velocity;

    internal void UpdateState(float angle, float vel)
    {
        _angle = angle;
        _velocity = vel;
    }
}

[AddComponentMenu("Physics/Spring Joint")]
public class SpringJoint : Joint
{
    private float _spring = 10f;
    private float _damper = 0.2f;
    private float _minDistance;
    private float _maxDistance;
    private float _tolerance = 0.025f;

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

    public float tolerance
    {
        get => _tolerance;
        set => _tolerance = value;
    }
}

[AddComponentMenu("Physics/Character Joint")]
public class CharacterJoint : Joint
{
    private Vector3 _swingAxis = Vector3.forward;
    private SoftJointLimit _lowTwistLimit = new SoftJointLimit { limit = -45f };
    private SoftJointLimit _highTwistLimit = new SoftJointLimit { limit = 45f };
    private SoftJointLimit _swing1Limit = new SoftJointLimit { limit = 45f };
    private SoftJointLimit _swing2Limit = new SoftJointLimit { limit = 45f };
    private bool _enableProjection;
    private float _projectionDistance = 0.1f;
    private float _projectionAngle = 180f;

    public Vector3 swingAxis
    {
        get => _swingAxis;
        set => _swingAxis = value.normalized;
    }

    public SoftJointLimit lowTwistLimit
    {
        get => _lowTwistLimit;
        set => _lowTwistLimit = value;
    }

    public SoftJointLimit highTwistLimit
    {
        get => _highTwistLimit;
        set => _highTwistLimit = value;
    }

    public SoftJointLimit swing1Limit
    {
        get => _swing1Limit;
        set => _swing1Limit = value;
    }

    public SoftJointLimit swing2Limit
    {
        get => _swing2Limit;
        set => _swing2Limit = value;
    }

    public bool enableProjection
    {
        get => _enableProjection;
        set => _enableProjection = value;
    }

    public float projectionDistance
    {
        get => _projectionDistance;
        set => _projectionDistance = value;
    }

    public float projectionAngle
    {
        get => _projectionAngle;
        set => _projectionAngle = value;
    }
}

[AddComponentMenu("Physics/Configurable Joint")]
public class ConfigurableJoint : Joint
{
    private ConfigurableJointMotion _xMotion;
    private ConfigurableJointMotion _yMotion;
    private ConfigurableJointMotion _zMotion;
    private ConfigurableJointMotion _angularXMotion;
    private ConfigurableJointMotion _angularYMotion;
    private ConfigurableJointMotion _angularZMotion;
    private SoftJointLimit _linearLimit = new SoftJointLimit();
    private SoftJointLimit _lowAngularXLimit = new SoftJointLimit();
    private SoftJointLimit _highAngularXLimit = new SoftJointLimit();
    private SoftJointLimit _angularYLimit = new SoftJointLimit();
    private SoftJointLimit _angularZLimit = new SoftJointLimit();
    private Vector3 _targetPosition;
    private Vector3 _targetVelocity;
    private Quaternion _targetRotation = Quaternion.identity;
    private Vector3 _targetAngularVelocity;
    private JointDrive _xDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = float.MaxValue };
    private JointDrive _yDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = float.MaxValue };
    private JointDrive _zDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = float.MaxValue };
    private JointDrive _angularXDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = float.MaxValue };
    private JointDrive _angularYZDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = float.MaxValue };
    private JointDrive _slerpDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = float.MaxValue };
    private bool _configuredInWorldSpace;
    private bool _swapBodies;
    private JointProjectionMode _projectionMode = JointProjectionMode.PositionAndRotation;
    private float _projectionDistance = 0.1f;
    private float _projectionAngle = 180f;
    private RotationDriveMode _rotationDriveMode;

    public ConfigurableJointMotion xMotion { get => _xMotion; set => _xMotion = value; }
    public ConfigurableJointMotion yMotion { get => _yMotion; set => _yMotion = value; }
    public ConfigurableJointMotion zMotion { get => _zMotion; set => _zMotion = value; }
    public ConfigurableJointMotion angularXMotion { get => _angularXMotion; set => _angularXMotion = value; }
    public ConfigurableJointMotion angularYMotion { get => _angularYMotion; set => _angularYMotion = value; }
    public ConfigurableJointMotion angularZMotion { get => _angularZMotion; set => _angularZMotion = value; }
    public SoftJointLimit linearLimit { get => _linearLimit; set => _linearLimit = value; }
    public SoftJointLimit lowAngularXLimit { get => _lowAngularXLimit; set => _lowAngularXLimit = value; }
    public SoftJointLimit highAngularXLimit { get => _highAngularXLimit; set => _highAngularXLimit = value; }
    public SoftJointLimit angularYLimit { get => _angularYLimit; set => _angularYLimit = value; }
    public SoftJointLimit angularZLimit { get => _angularZLimit; set => _angularZLimit = value; }
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
    public bool configuredInWorldSpace { get => _configuredInWorldSpace; set => _configuredInWorldSpace = value; }
    public bool swapBodies { get => _swapBodies; set => _swapBodies = value; }
    public JointProjectionMode projectionMode { get => _projectionMode; set => _projectionMode = value; }
    public float projectionDistance { get => _projectionDistance; set => _projectionDistance = value; }
    public float projectionAngle { get => _projectionAngle; set => _projectionAngle = value; }
    public RotationDriveMode rotationDriveMode { get => _rotationDriveMode; set => _rotationDriveMode = value; }
}

public enum JointProjectionMode
{
    None,
    PositionAndRotation
}

public enum RotationDriveMode
{
    XYAndZ,
    Slerp
}

public struct JointLimits
{
    public float min;
    public float max;
    public float bounciness;
    public float bouncyMinVelocity;
    public float contactDistance;
    public float minBounce;
    public float maxBounce;
}

public struct SoftJointLimit
{
    public float limit;
    public float bounciness;
    public float contactDistance;
    public float spring;
    public float damper;
}

public struct JointMotor
{
    public float targetVelocity;
    public float force;
    public float freeSpin;

    public JointMotor(float targetVelocity, float force, bool freeSpin)
    {
        this.targetVelocity = targetVelocity;
        this.force = force;
        this.freeSpin = freeSpin ? 1f : 0f;
    }
}

public struct JointSpring
{
    public float spring;
    public float damper;
    public float targetPosition;
}

public struct JointDrive
{
    public float positionSpring;
    public float positionDamper;
    public float maximumForce;
    public float positionDamper2;
}

public enum ConfigurableJointMotion
{
    Free,
    Locked,
    Limited
}
