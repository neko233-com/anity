using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Deep Unity math/curve/network form parity — ≥12 cases.</summary>
public class AnimationCurveAndMathDepthTests
{
    [Fact]
    public void Linear_Evaluate_Midpoint()
    {
        var c = AnimationCurve.Linear(0f, 0f, 1f, 10f);
        Assert.InRange(c.Evaluate(0.5f), 4.99f, 5.01f);
        Assert.Equal(0f, c.Evaluate(0f), 3);
        Assert.Equal(10f, c.Evaluate(1f), 3);
    }

    [Fact]
    public void Linear_IsTrulyLinear_AtQuarter()
    {
        var c = AnimationCurve.Linear(0f, 0f, 4f, 8f);
        Assert.InRange(c.Evaluate(1f), 1.99f, 2.01f);
        Assert.InRange(c.Evaluate(3f), 5.99f, 6.01f);
    }

    [Fact]
    public void EaseInOut_EndsMatch_MidIsHalfish()
    {
        var c = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        Assert.Equal(0f, c.Evaluate(0f), 3);
        Assert.Equal(1f, c.Evaluate(1f), 3);
        // zero tangents → Hermite mid ≈ 0.5
        Assert.InRange(c.Evaluate(0.5f), 0.45f, 0.55f);
    }

    [Fact]
    public void Constant_AlwaysSame()
    {
        var c = AnimationCurve.Constant(0f, 2f, 7f);
        Assert.Equal(7f, c.Evaluate(0f), 3);
        Assert.Equal(7f, c.Evaluate(1f), 3);
        Assert.Equal(7f, c.Evaluate(2f), 3);
    }

    [Fact]
    public void Hermite_WithTangents_NotLinear()
    {
        // steep out tangent at start → value rises faster early
        var c = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 4f),
            new Keyframe(1f, 1f, 0f, 0f));
        float mid = c.Evaluate(0.5f);
        Assert.True(mid > 0.55f, "steep outTangent should overshoot linear mid; got " + mid);
    }

    [Fact]
    public void Empty_Evaluate_Zero()
    {
        Assert.Equal(0f, new AnimationCurve().Evaluate(0.5f));
    }

    [Fact]
    public void Loop_Wrap_Repeats()
    {
        var c = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        c.preWrapMode = WrapMode.Loop;
        c.postWrapMode = WrapMode.Loop;
        Assert.InRange(c.Evaluate(1.5f), 0.45f, 0.55f);
        Assert.InRange(c.Evaluate(-0.5f), 0.45f, 0.55f);
    }

    [Fact]
    public void SmoothTangents_SetsBoth()
    {
        // rising overall 0→1→3 so center slope (3-0)/(2-0)=1.5
        var c = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f),
            new Keyframe(2f, 3f));
        c.SmoothTangents(1, 1f);
        Assert.InRange(c.keys[1].inTangent, 1.4f, 1.6f);
        Assert.Equal(c.keys[1].inTangent, c.keys[1].outTangent, 3);
    }

    [Fact]
    public void Slerp_UnitSphere_MaintainsMagnitude()
    {
        Vector3 a = Vector3.forward;
        Vector3 b = Vector3.right;
        var m = Vector3.Slerp(a, b, 0.5f);
        Assert.InRange(m.magnitude, 0.99f, 1.01f);
        // mid should be ~45° between forward and right
        Assert.True(Vector3.Dot(m.normalized, a) > 0.5f);
        Assert.True(Vector3.Dot(m.normalized, b) > 0.5f);
    }

    [Fact]
    public void Slerp_Clamps_T()
    {
        Vector3 a = Vector3.up;
        Vector3 b = Vector3.down;
        var r0 = Vector3.Slerp(a, b, -1f);
        var r1 = Vector3.Slerp(a, b, 2f);
        Assert.True(Vector3.Distance(r0, a) < 0.01f || Vector3.Dot(r0.normalized, a) > 0.99f);
        Assert.True(Vector3.Dot(r1.normalized, b) > 0.99f || Vector3.Distance(r1, b) < 0.05f);
    }

    [Fact]
    public void SlerpUnclamped_Extrapolates()
    {
        Vector3 a = Vector3.right * 2f;
        Vector3 b = Vector3.up * 2f;
        var mid = Vector3.SlerpUnclamped(a, b, 0.5f);
        Assert.InRange(mid.magnitude, 1.9f, 2.1f);
    }

    [Fact]
    public void SerializeFormSections_ContainsParts()
    {
        var boundary = Encoding.ASCII.GetBytes("BOUNDARY123");
        var sections = new List<IMultipartFormSection>
        {
            new MultipartFormDataSection("field", "hello"),
            new MultipartFormFileSection("file", Encoding.UTF8.GetBytes("bin"), "a.txt", "text/plain")
        };
        string body = UnityWebRequest.SerializeFormSections(sections, boundary);
        Assert.Contains("BOUNDARY123", body);
        Assert.Contains("name=\"field\"", body);
        Assert.Contains("hello", body);
        Assert.Contains("filename=\"a.txt\"", body);
        Assert.Contains("bin", body);
        Assert.Contains("--BOUNDARY123--", body);
    }

    [Fact]
    public void GenerateBoundary_IsPrintableAscii()
    {
        var b = UnityWebRequest.GenerateBoundary();
        Assert.True(b.Length >= 8);
        string s = Encoding.ASCII.GetString(b);
        Assert.Matches("^[0-9a-f]+$", s);
    }

    [Fact]
    public void SerializeFormSections_Empty_EmptyString()
    {
        Assert.Equal(string.Empty, UnityWebRequest.SerializeFormSections(null!, null!));
        Assert.Equal(string.Empty, UnityWebRequest.SerializeFormSections(new List<IMultipartFormSection>(), Array.Empty<byte>()));
    }
}
