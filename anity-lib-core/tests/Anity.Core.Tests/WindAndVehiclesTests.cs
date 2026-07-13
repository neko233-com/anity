using System;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Wind + Vehicles (WheelCollider chassis) — ≥12 + ≥10 cases.</summary>
public class WindAndVehiclesTests : IDisposable
{
    public WindAndVehiclesTests()
    {
        Wind.Clear();
    }

    public void Dispose()
    {
        Wind.Clear();
    }

    [Fact]
    public void WindZone_RegistersOnEnable()
    {
        var go = new GameObject("wind");
        var wz = go.AddComponent<WindZone>();
        Assert.True(Wind.zoneCount >= 1);
        UnityEngine.Object.DestroyImmediate(go);
        Assert.Equal(0, Wind.zoneCount);
    }

    [Fact]
    public void Wind_Directional_HasMagnitude()
    {
        var go = new GameObject("wind");
        go.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        var wz = go.AddComponent<WindZone>();
        wz.mode = WindZoneMode.Directional;
        wz.windMain = 2f;
        wz.windPulseMagnitude = 0f;
        wz.windTurbulence = 0f;
        var v = Wind.GetWindAt(Vector3.zero);
        Assert.True(v.magnitude > 1f);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void Wind_Spherical_AttenuatesOutsideRadius()
    {
        var go = new GameObject("windS");
        go.transform.position = Vector3.zero;
        var wz = go.AddComponent<WindZone>();
        wz.mode = WindZoneMode.Spherical;
        wz.radius = 5f;
        wz.windMain = 3f;
        wz.windPulseMagnitude = 0f;
        wz.windTurbulence = 0f;

        var inside = Wind.GetWindAt(new Vector3(1, 0, 0));
        var outside = Wind.GetWindAt(new Vector3(20, 0, 0));
        Assert.True(inside.magnitude > 0.1f);
        Assert.Equal(0f, outside.magnitude, 3);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void Wind_GetWindMainAt_MatchesMagnitude()
    {
        var go = new GameObject("wind2");
        var wz = go.AddComponent<WindZone>();
        wz.windMain = 1.5f;
        wz.windPulseMagnitude = 0f;
        wz.windTurbulence = 0f;
        float m = Wind.GetWindMainAt(Vector3.zero);
        Assert.InRange(m, 1f, 3f);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void Wind_MultipleZones_Accumulate()
    {
        var a = new GameObject("a");
        var b = new GameObject("b");
        a.AddComponent<WindZone>().windMain = 1f;
        a.GetComponent<WindZone>().windPulseMagnitude = 0;
        a.GetComponent<WindZone>().windTurbulence = 0;
        b.AddComponent<WindZone>().windMain = 1f;
        b.GetComponent<WindZone>().windPulseMagnitude = 0;
        b.GetComponent<WindZone>().windTurbulence = 0;
        Assert.Equal(2, Wind.zoneCount);
        float m = Wind.GetWindMainAt(Vector3.zero);
        Assert.True(m >= 1.5f);
        UnityEngine.Object.DestroyImmediate(a);
        UnityEngine.Object.DestroyImmediate(b);
    }

    [Fact]
    public void WindZone_Disable_Unregisters()
    {
        var go = new GameObject("w");
        var wz = go.AddComponent<WindZone>();
        Assert.True(Wind.zoneCount >= 1);
        wz.enabled = false;
        Assert.Equal(0, Wind.zoneCount);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void WindZone_RadiusClamp()
    {
        var go = new GameObject("r");
        var wz = go.AddComponent<WindZone>();
        wz.radius = -5f;
        Assert.Equal(0f, wz.radius);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void WheelFrictionCurve_Evaluate_Extremum()
    {
        var curve = new WheelFrictionCurve
        {
            extremumSlip = 0.4f,
            extremumValue = 1f,
            asymptoteSlip = 0.8f,
            asymptoteValue = 0.5f,
            stiffness = 1f
        };
        Assert.True(curve.Evaluate(0.2f) > 0);
        Assert.True(curve.Evaluate(0.4f) >= curve.Evaluate(0.2f) - 0.01f);
        Assert.True(curve.Evaluate(1.5f) > 0);
    }

    [Fact]
    public void WheelCollider_Defaults()
    {
        var go = new GameObject("wheel");
        var w = go.AddComponent<WheelCollider>();
        Assert.True(w.radius > 0);
        Assert.False(w.isGrounded);
        Assert.Equal(0f, w.rpm);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void WheelCollider_MotorAndSteer()
    {
        var go = new GameObject("wheel2");
        var w = go.AddComponent<WheelCollider>();
        w.motorTorque = 100f;
        w.steerAngle = 15f;
        w.brakeTorque = 50f;
        Assert.Equal(100f, w.motorTorque);
        Assert.Equal(15f, w.steerAngle);
        Assert.Equal(50f, w.brakeTorque);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void WheelCollider_GetWorldPose()
    {
        var go = new GameObject("wheel3");
        go.transform.position = new Vector3(1, 2, 3);
        var w = go.AddComponent<WheelCollider>();
        w.GetWorldPose(out var pos, out var rot);
        Assert.Equal(1f, pos.x, 2);
        Assert.NotEqual(default, rot);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void VehicleUtility_CreateSimpleCar_HasFourWheels()
    {
        var car = VehicleUtility.CreateSimpleCar(Vector3.zero);
        Assert.Equal(4, car.wheelCount);
        Assert.NotNull(car.GetComponent<Rigidbody>());
        UnityEngine.Object.DestroyImmediate(car.gameObject);
    }

    [Fact]
    public void VehicleChassis_ApplyInput_SetsTorques()
    {
        var car = VehicleUtility.CreateSimpleCar(Vector3.zero);
        car.ApplyInput(1f, 0.5f, 0f);
        Assert.True(car.motorTorque > 0);
        Assert.True(Mathf.Abs(car.steerAngle) > 0);
        var wheels = car.GetWheels();
        Assert.True(wheels[0].steerAngle != 0); // front steer
        UnityEngine.Object.DestroyImmediate(car.gameObject);
    }

    [Fact]
    public void VehicleChassis_BrakeInput()
    {
        var car = VehicleUtility.CreateSimpleCar(Vector3.zero);
        car.ApplyInput(0f, 0f, 1f);
        Assert.True(car.brakeTorque > 0);
        UnityEngine.Object.DestroyImmediate(car.gameObject);
    }

    [Fact]
    public void VehicleChassis_MaxClamp()
    {
        var go = new GameObject("ch");
        go.AddComponent<Rigidbody>();
        var ch = go.AddComponent<VehicleChassis>();
        ch.maxSteerAngle = 200f;
        Assert.Equal(90f, ch.maxSteerAngle);
        ch.ApplyInput(2f, 2f, 2f);
        Assert.InRange(ch.motorInput, -1f, 1f);
        Assert.InRange(ch.steerInput, -1f, 1f);
        Assert.InRange(ch.brakeInput, 0f, 1f);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void VehicleUtility_CreateWheel_Attaches()
    {
        var parent = new GameObject("p");
        var w = VehicleUtility.CreateWheel(parent, "FL", new Vector3(1, 0, 1), 0.4f);
        Assert.Equal(0.4f, w.radius);
        Assert.Equal(parent.transform, w.transform.parent);
        UnityEngine.Object.DestroyImmediate(parent);
    }

    [Fact]
    public void WheelCollider_ConfigureVehicleSubsteps()
    {
        var go = new GameObject("ws");
        var w = go.AddComponent<WheelCollider>();
        w.ConfigureVehicleSubsteps(5f, 12, 15);
        UnityEngine.Object.DestroyImmediate(go);
    }
}
