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

  public static AnimationCurve Linear(float timeStart, float valueStart, float timeEnd, float valueEnd)
  {
    return new AnimationCurve(
      new Keyframe(timeStart, valueStart, 0f, 0f),
      new Keyframe(timeEnd, valueEnd, 0f, 0f)
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
    var curve = new AnimationCurve();
    var startKey = new Keyframe(timeStart, valueStart) { outTangent = 0f };
    var endKey = new Keyframe(timeEnd, valueEnd) { inTangent = 0f };
    curve.AddKey(startKey);
    curve.AddKey(endKey);
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
    for (var i = 1; i < _keys.Count; i++)
    {
      if (time <= _keys[i].time)
      {
        var a = _keys[i - 1];
        var b = _keys[i];
        var t = (time - a.time) / MathF.Max(1e-6f, b.time - a.time);
        return a.value + (b.value - a.value) * t;
      }
    }

    return _keys[^1].value;
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
