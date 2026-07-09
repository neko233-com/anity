using System;

namespace UnityEngine;

/// <summary>
/// Unity WheelCollider component for vehicle physics.
/// </summary>
public class WheelCollider : Collider
{
    private Vector3 _center;
    private float _radius = 0.5f;
    private float _suspensionDistance = 0.3f;
    private JointSpring _suspensionSpring;
    private WheelFrictionCurve _forwardFriction;
    private WheelFrictionCurve _sidewaysFriction;
    private float _motorTorque;
    private float _brakeTorque;
    private float _steerAngle;
    private bool _isGrounded;

    public Vector3 center
    {
        get => _center;
        set => _center = value;
    }

    public float radius
    {
        get => _radius;
        set => _radius = value;
    }

    public float suspensionDistance
    {
        get => _suspensionDistance;
        set => _suspensionDistance = value;
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
        set => _brakeTorque = value;
    }

    public float steerAngle
    {
        get => _steerAngle;
        set => _steerAngle = value;
    }

    public bool isGrounded => _isGrounded;
    public float rpm { get; }
    public float sprungMass { get; }

    public void ConfigureVehicleSubdivisions(int subdivisions) { }

    public bool GetGroundHit(out WheelHit hitInfo)
    {
        hitInfo = default;
        return _isGrounded;
    }

    public void GetWorldPose(out Quaternion quat, out Vector3 pos)
    {
        quat = Quaternion.identity;
        pos = Vector3.zero;
    }
}

/// <summary>
/// Wheel hit information.
/// </summary>
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

/// <summary>
/// Wheel friction curve.
/// </summary>
[Serializable]
public struct WheelFrictionCurve
{
    public float extremumSlip;
    public float extremumValue;
    public float asymptoteSlip;
    public float asymptoteValue;
    public float stiffness;
}

/// <summary>
/// Unity TrailRenderer for rendering trails.
/// </summary>
[AddComponentMenu("Effects/Trail Renderer")]
[RequireComponent(typeof(Transform))]
public class TrailRenderer : Renderer
{
    private float _time = 5.0f;
    private float _startWidth = 1.0f;
    private float _endWidth = 0.0f;
    private int _positionCount = 0;
    private float _minVertexDistance = 0.1f;
    private bool _autodestruct;
    private bool _emitting;
    private bool _generateLightingData;
    private int _numCornerVertices;
    private int _numCapVertices;
    private LineAlignment _alignment;
    private Gradient _colorGradient;
    private AnimationCurve _widthCurve;
    private AnimationCurve _timeCurve;

    public float time
    {
        get => _time;
        set => _time = value;
    }

    public float startWidth
    {
        get => _startWidth;
        set => _startWidth = value;
    }

    public float endWidth
    {
        get => _endWidth;
        set => _endWidth = value;
    }

    public int positionCount
    {
        get => _positionCount;
        set => _positionCount = value;
    }

    public float minVertexDistance
    {
        get => _minVertexDistance;
        set => _minVertexDistance = value;
    }

    public bool autodestruct
    {
        get => _autodestruct;
        set => _autodestruct = value;
    }

    public bool emitting
    {
        get => _emitting;
        set => _emitting = value;
    }

    public bool generateLightingData
    {
        get => _generateLightingData;
        set => _generateLightingData = value;
    }

    public int numCornerVertices
    {
        get => _numCornerVertices;
        set => _numCornerVertices = value;
    }

    public int numCapVertices
    {
        get => _numCapVertices;
        set => _numCapVertices = value;
    }

    public LineAlignment alignment
    {
        get => _alignment;
        set => _alignment = value;
    }

    public Gradient colorGradient => _colorGradient;
    public AnimationCurve widthCurve => _widthCurve;
    public AnimationCurve timeCurve => _timeCurve;

    public float timeRemaining { get; }
    public float startLifetime { get; set; }

    public void Clear() { }
    public int GetPositions(Vector3[] positions) => 0;
    public void SetPositions(Vector3[] positions) { }
    public void AddPosition(Vector3 position) { }
    public void AddPositions(Vector3[] positions) { }
}

/// <summary>
/// Line alignment options.
/// </summary>
public enum LineAlignment
{
    View,
    TransformZ,
    Local
}

/// <summary>
/// Unity LineRenderer for rendering lines.
/// </summary>
public class LineRenderer : Renderer
{
    private int _positionCount = 2;
    private float _startWidth = 0.1f;
    private float _endWidth = 0.1f;
    private bool _loop;
    private bool _useWorldSpace = true;
    private bool _numCornerVertices;
    private int _numCapVertices;
    private LineAlignment _alignment;
    private AnimationCurve _widthCurve;
    private Gradient _colorGradient;

    public int positionCount
    {
        get => _positionCount;
        set => _positionCount = value;
    }

    public float startWidth
    {
        get => _startWidth;
        set => _startWidth = value;
    }

    public float endWidth
    {
        get => _endWidth;
        set => _endWidth = value;
    }

    public bool loop
    {
        get => _loop;
        set => _loop = value;
    }

    public bool useWorldSpace
    {
        get => _useWorldSpace;
        set => _useWorldSpace = value;
    }

    public int numCornerVertices
    {
        get => _numCapVertices ? 5 : 0;
        set => _numCapVertices = value > 0;
    }

    public int numCapVertices
    {
        get => _numCapVertices ? 5 : 0;
        set => _numCapVertices = value > 0;
    }

    public LineAlignment alignment
    {
        get => _alignment;
        set => _alignment = value;
    }

    public AnimationCurve widthCurve => _widthCurve;
    public Gradient colorGradient => _colorGradient;
    public bool useWorldSpace2 => _useWorldSpace;
    public bool positionCount2 => _positionCount > 0;

    public void SetPosition(int index, Vector3 position) { }
    public Vector3 GetPosition(int index) => Vector3.zero;
    public int GetPositions(Vector3[] positions) => 0;
    public void SetPositions(Vector3[] positions) { }
    public void Simplify(float tolerance) { }
    public void Simplify() { }
    public void BakeMesh(Mesh mesh, bool useTransform) { }
    public void BakeMesh(Mesh mesh) { }
}
