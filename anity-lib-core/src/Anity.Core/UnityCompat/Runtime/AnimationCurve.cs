using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine;

public class AnimationCurve
{
  private readonly List<Keyframe> _keys;

  public AnimationCurve()
  {
    _keys = new List<Keyframe>();
    preWrapMode = WrapMode.Default;
    postWrapMode = WrapMode.Default;
  }

  public AnimationCurve(params Keyframe[] keys)
  {
    _keys = keys?.ToList() ?? new List<Keyframe>();
    preWrapMode = WrapMode.Default;
    postWrapMode = WrapMode.Default;
  }

  public Keyframe[] keys
  {
    get => _keys.ToArray();
    set
    {
      _keys.Clear();
      if (value is not null)
      {
        _keys.AddRange(value);
      }
    }
  }

  public WrapMode preWrapMode { get; set; }
  public WrapMode postWrapMode { get; set; }
  public int length => _keys.Count;

  public Keyframe this[int index]
  {
    get => _keys[index];
  }

  public static AnimationCurve Linear(float timeStart, float valueStart, float timeEnd, float valueEnd)
  {
    // Unity: constant slope as in/out tangents so Hermite reduces to linear
    float dt = timeEnd - timeStart;
    float slope = MathF.Abs(dt) > 1e-8f ? (valueEnd - valueStart) / dt : 0f;
    return new AnimationCurve(
      new Keyframe(timeStart, valueStart, 0f, slope),
      new Keyframe(timeEnd, valueEnd, slope, 0f)
    );
  }

  public static AnimationCurve Constant(float timeStart, float timeEnd, float value)
  {
    return new AnimationCurve(
      new Keyframe(timeStart, value, 0f, 0f),
      new Keyframe(timeEnd, value, 0f, 0f)
    );
  }

  public static AnimationCurve EaseInOut(float timeStart, float valueStart, float timeEnd, float valueEnd)
  {
    // Unity EaseInOut: zero end-tangents (smooth ease)
    var curve = new AnimationCurve(
      new Keyframe(timeStart, valueStart, 0f, 0f),
      new Keyframe(timeEnd, valueEnd, 0f, 0f)
    );
    return curve;
  }

  public float Evaluate(float time)
  {
    if (_keys.Count == 0)
    {
      return 0f;
    }

    if (_keys.Count == 1)
    {
      return _keys[0].value;
    }

    var firstTime = _keys[0].time;
    var lastTime = _keys[^1].time;
    var duration = lastTime - firstTime;

    if (duration <= 0f)
    {
      return _keys[0].value;
    }

    if (time < firstTime)
    {
      return EvaluateWrap(time, firstTime, lastTime, duration, preWrapMode);
    }

    if (time > lastTime)
    {
      return EvaluateWrap(time, firstTime, lastTime, duration, postWrapMode);
    }

    return EvaluateSegment(time);
  }

  private float EvaluateWrap(float time, float firstTime, float lastTime, float duration, WrapMode mode)
  {
    switch (mode)
    {
      case WrapMode.Loop:
        var t1 = (time - firstTime) % duration;
        if (t1 < 0f) t1 += duration;
        return EvaluateSegment(firstTime + t1);

      case WrapMode.PingPong:
        var cycle = (time - firstTime) / duration;
        var cycleFloor = MathF.Floor(cycle);
        var t2 = cycle - cycleFloor;
        if ((int)cycleFloor % 2 == 1)
        {
          t2 = 1f - t2;
        }
        return EvaluateSegment(firstTime + t2 * duration);

      case WrapMode.ClampForever:
        return time < firstTime ? _keys[0].value : _keys[^1].value;

      case WrapMode.Once:
      default:
        return time < firstTime ? _keys[0].value : _keys[^1].value;
    }
  }

  private float EvaluateSegment(float time)
  {
    // Sort-stable: keys assumed ordered by AddKey BinarySearch
    for (var i = 1; i < _keys.Count; i++)
    {
      if (time <= _keys[i].time)
      {
        var a = _keys[i - 1];
        var b = _keys[i];
        return Hermite(a, b, time);
      }
    }

    return _keys[^1].value;
  }

  /// <summary>
  /// Cubic Hermite spline matching Unity AnimationCurve (tangent = dValue/dTime).
  /// </summary>
  private static float Hermite(Keyframe a, Keyframe b, float time)
  {
    float dx = b.time - a.time;
    if (MathF.Abs(dx) < 1e-8f)
      return a.value;
    // Unity encodes constant/stepped curve segments with an infinite tangent.
    // Hold the left value until the right key time instead of allowing the
    // Hermite products to become NaN.
    if (!float.IsFinite(a.outTangent) || !float.IsFinite(b.inTangent))
      return time < b.time ? a.value : b.value;

    float t = (time - a.time) / dx;
    t = t < 0f ? 0f : (t > 1f ? 1f : t);
    float t2 = t * t;
    float t3 = t2 * t;

    // Hermite basis
    float h00 = 2f * t3 - 3f * t2 + 1f;
    float h10 = t3 - 2f * t2 + t;
    float h01 = -2f * t3 + 3f * t2;
    float h11 = t3 - t2;

    // Unity tangents are in value/time units → scale by segment length
    float m0 = a.outTangent * dx;
    float m1 = b.inTangent * dx;
    return h00 * a.value + h10 * m0 + h01 * b.value + h11 * m1;
  }

  public int AddKey(float time, float value)
  {
    var key = new Keyframe(time, value);
    return AddKey(key);
  }

  public int AddKey(Keyframe key)
  {
    var index = _keys.BinarySearch(key, Comparer<Keyframe>.Create((a, b) => a.time.CompareTo(b.time)));
    if (index < 0)
    {
      index = ~index;
    }

    _keys.Insert(index, key);
    return index;
  }

  public int MoveKey(int index, Keyframe key)
  {
    _keys.RemoveAt(index);
    return AddKey(key);
  }

  public void RemoveKey(int index)
  {
    _keys.RemoveAt(index);
  }

  public void SmoothTangents(int index, float weight)
  {
    var key = _keys[index];
    if (index > 0 && index < _keys.Count - 1)
    {
      var prev = _keys[index - 1];
      var next = _keys[index + 1];
      var tangent = (next.value - prev.value) / (next.time - prev.time);
      key.inTangent = tangent * weight;
      key.outTangent = tangent * weight;
    }
    else if (index == 0 && _keys.Count > 1)
    {
      var next = _keys[index + 1];
      var tangent = (next.value - key.value) / (next.time - key.time);
      key.inTangent = tangent * weight;
      key.outTangent = tangent * weight;
    }
    else if (index == _keys.Count - 1 && _keys.Count > 1)
    {
      var prev = _keys[index - 1];
      var tangent = (key.value - prev.value) / (key.time - prev.time);
      key.inTangent = tangent * weight;
      key.outTangent = tangent * weight;
    }

    _keys[index] = key;
  }

  public void SetKeys(Keyframe[] keys)
  {
    _keys.Clear();
    if (keys is not null)
    {
      _keys.AddRange(keys);
    }
  }
}
