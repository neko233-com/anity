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
  }

  public AnimationCurve(params Keyframe[] keys)
  {
    _keys = keys?.ToList() ?? new List<Keyframe>();
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

  public int length => _keys.Count;

  public float Evaluate(float time)
  {
    if (_keys.Count == 0)
    {
      return 0f;
    }

    if (time <= _keys[0].time)
    {
      return _keys[0].value;
    }

    for (var i = 1; i < _keys.Count; i++)
    {
      if (time <= _keys[i].time)
      {
        var a = _keys[i - 1];
        var b = _keys[i];
        var t = (time - a.time) / Math.Max(1e-6f, b.time - a.time);
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
    _keys[index] = key;
    return index;
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

