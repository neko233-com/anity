using System;
using System.Collections.Generic;

namespace UnityEngine;

/// <summary>
/// Vehicle helper module (Unity Vehicles / WheelCollider orchestration surface).
/// Aggregates multiple WheelColliders under a Rigidbody chassis.
/// </summary>
[AddComponentMenu("Physics/Vehicle Chassis")]
[RequireComponent(typeof(Rigidbody))]
public class VehicleChassis : MonoBehaviour
{
    private readonly List<WheelCollider> _wheels = new();
    private Rigidbody _body;
    private float _motorTorque;
    private float _brakeTorque;
    private float _steerAngle;
    private float _maxSteerAngle = 30f;
    private float _maxMotorTorque = 1500f;
    private float _maxBrakeTorque = 3000f;
    private int _steerWheelCount = 2;
    private int _driveWheelCount = 2;

    public float motorInput { get; set; }
    public float steerInput { get; set; }
    public float brakeInput { get; set; }

    public float maxSteerAngle
    {
        get => _maxSteerAngle;
        set => _maxSteerAngle = Mathf.Clamp(value, 0f, 90f);
    }

    public float maxMotorTorque
    {
        get => _maxMotorTorque;
        set => _maxMotorTorque = Mathf.Max(0f, value);
    }

    public float maxBrakeTorque
    {
        get => _maxBrakeTorque;
        set => _maxBrakeTorque = Mathf.Max(0f, value);
    }

    public int wheelCount => _wheels.Count;
    public float speedKmh
    {
        get
        {
            if (_body == null) return 0f;
            return _body.velocity.magnitude * 3.6f;
        }
    }

    public float motorTorque => _motorTorque;
    public float brakeTorque => _brakeTorque;
    public float steerAngle => _steerAngle;

    protected override void Awake()
    {
        base.Awake();
        _body = GetComponent<Rigidbody>();
        RefreshWheels();
    }

    public void RefreshWheels()
    {
        _wheels.Clear();
        if (gameObject == null) return;
        var found = GetComponentsInChildren<WheelCollider>(true);
        if (found != null)
        {
            foreach (var w in found)
            {
                if (w != null) _wheels.Add(w);
            }
        }
    }

    public void AddWheel(WheelCollider wheel)
    {
        if (wheel == null) return;
        if (!_wheels.Contains(wheel)) _wheels.Add(wheel);
    }

    public IReadOnlyList<WheelCollider> GetWheels() => _wheels;

    public void SetDriveLayout(int steerWheels, int driveWheels)
    {
        _steerWheelCount = Math.Max(0, steerWheels);
        _driveWheelCount = Math.Max(0, driveWheels);
    }

    /// <summary>Apply throttle/steer/brake inputs and push torques to wheels.</summary>
    public void ApplyInput(float motor, float steer, float brake)
    {
        motorInput = Mathf.Clamp(motor, -1f, 1f);
        steerInput = Mathf.Clamp(steer, -1f, 1f);
        brakeInput = Mathf.Clamp01(brake);
        UpdateVehicle(Time.fixedDeltaTime > 0 ? Time.fixedDeltaTime : 0.02f);
    }

    public void UpdateVehicle(float dt)
    {
        _ = dt;
        if (_wheels.Count == 0) RefreshWheels();

        _motorTorque = motorInput * _maxMotorTorque;
        _brakeTorque = brakeInput * _maxBrakeTorque;
        _steerAngle = steerInput * _maxSteerAngle;

        for (int i = 0; i < _wheels.Count; i++)
        {
            var w = _wheels[i];
            if (w == null) continue;

            bool isSteer = i < _steerWheelCount;
            bool isDrive = i < _driveWheelCount || (_driveWheelCount <= 0 && i >= _wheels.Count - 2);

            w.steerAngle = isSteer ? _steerAngle : 0f;
            w.motorTorque = isDrive ? _motorTorque : 0f;
            w.brakeTorque = _brakeTorque;
        }
    }

    public int CountGroundedWheels()
    {
        int n = 0;
        foreach (var w in _wheels)
        {
            if (w != null && w.isGrounded) n++;
        }
        return n;
    }

    public bool AllWheelsGrounded() => _wheels.Count > 0 && CountGroundedWheels() == _wheels.Count;

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        UpdateVehicle(Time.fixedDeltaTime);
    }
}

/// <summary>Static helpers for vehicle wheel setup (editor/runtime).</summary>
public static class VehicleUtility
{
    public static WheelCollider CreateWheel(GameObject parent, string name, Vector3 localCenter, float radius = 0.35f)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localCenter;
        var wheel = go.AddComponent<WheelCollider>();
        wheel.center = Vector3.zero;
        wheel.radius = radius;
        wheel.suspensionDistance = 0.2f;
        return wheel;
    }

    public static VehicleChassis CreateSimpleCar(Vector3 position, float wheelBase = 2.5f, float track = 1.6f)
    {
        var root = new GameObject("Vehicle");
        root.transform.position = position;
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1200f;
        var chassis = root.AddComponent<VehicleChassis>();

        float halfBase = wheelBase * 0.5f;
        float halfTrack = track * 0.5f;
        chassis.AddWheel(CreateWheel(root, "FL", new Vector3(-halfTrack, 0.35f, halfBase)));
        chassis.AddWheel(CreateWheel(root, "FR", new Vector3(halfTrack, 0.35f, halfBase)));
        chassis.AddWheel(CreateWheel(root, "RL", new Vector3(-halfTrack, 0.35f, -halfBase)));
        chassis.AddWheel(CreateWheel(root, "RR", new Vector3(halfTrack, 0.35f, -halfBase)));
        chassis.SetDriveLayout(2, 2);
        return chassis;
    }
}
