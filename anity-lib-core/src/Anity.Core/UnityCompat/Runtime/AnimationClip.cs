using System;
using System.Collections.Generic;

namespace UnityEngine;

public struct AnimationCurveBinding
{
    public string path;
    public Type type;
    public string propertyName;
    public AnimationCurve curve;
}

public class AnimationClip : Motion
{
    private float _length;
    private readonly List<AnimationCurveBinding> _bindings = new();
    private readonly List<AnimationEvent> _events = new();

    public AnimationClip()
    {
        frameRate = 60f;
        wrapMode = WrapMode.Default;
    }

    public float length
    {
        get
        {
            if (_length > 0f) return _length;
            float maxTime = 0f;
            foreach (var binding in _bindings)
            {
                if (binding.curve != null && binding.curve.length > 0)
                {
                    var keys = binding.curve.keys;
                    if (keys.Length > 0)
                    {
                        maxTime = MathF.Max(maxTime, keys[^1].time);
                    }
                }
            }
            return maxTime;
        }
        set => _length = value;
    }

    public float frameRate { get; set; }
    public WrapMode wrapMode { get; set; }
    public bool legacy { get; set; }
    public bool humanMotion { get; set; }
    public bool empty => _bindings.Count == 0;
    public bool hasGenericRootTransform { get; set; }
    public bool hasMotionFloatCurves { get; set; }
    public bool hasRootCurves { get; set; }
    public Bounds localBounds { get; set; }

    public AnimationEvent[] events
    {
        get => _events.ToArray();
        set
        {
            _events.Clear();
            if (value != null) _events.AddRange(value);
        }
    }

    public AnimationCurveBinding[] bindings => _bindings.ToArray();

    public void SampleAnimation(GameObject go, float time)
    {
        if (go == null) return;

        float wrappedTime = WrapTime(time, length, wrapMode);

        var sampledValues = new Dictionary<(Transform, string), Vector4>();

        foreach (var binding in _bindings)
        {
            Transform target;
            if (string.IsNullOrEmpty(binding.path))
            {
                target = go.transform;
            }
            else
            {
                target = go.transform.Find(binding.path);
            }

            if (target == null || binding.curve == null) continue;

            float value = binding.curve.Evaluate(wrappedTime);
            var key = (target, GetPropertyBase(binding.propertyName));

            if (!sampledValues.TryGetValue(key, out var vec))
            {
                vec = new Vector4(0, 0, 0, 1);
            }

            switch (binding.propertyName)
            {
                case "m_LocalPosition.x": vec.x = value; break;
                case "m_LocalPosition.y": vec.y = value; break;
                case "m_LocalPosition.z": vec.z = value; break;
                case "m_LocalRotation.x": vec.x = value; break;
                case "m_LocalRotation.y": vec.y = value; break;
                case "m_LocalRotation.z": vec.z = value; break;
                case "m_LocalRotation.w": vec.w = value; break;
                case "m_LocalScale.x": vec.x = value; break;
                case "m_LocalScale.y": vec.y = value; break;
                case "m_LocalScale.z": vec.z = value; break;
                case "localPosition.x": vec.x = value; break;
                case "localPosition.y": vec.y = value; break;
                case "localPosition.z": vec.z = value; break;
                case "localRotation.x": vec.x = value; break;
                case "localRotation.y": vec.y = value; break;
                case "localRotation.z": vec.z = value; break;
                case "localRotation.w": vec.w = value; break;
                case "localScale.x": vec.x = value; break;
                case "localScale.y": vec.y = value; break;
                case "localScale.z": vec.z = value; break;
            }

            sampledValues[key] = vec;
        }

        foreach (var kv in sampledValues)
        {
            var (transform, propBase) = kv.Key;
            var v = kv.Value;

            if (propBase.Contains("Position"))
            {
                transform.localPosition = new Vector3(v.x, v.y, v.z);
            }
            else if (propBase.Contains("Rotation"))
            {
                transform.localRotation = new Quaternion(v.x, v.y, v.z, v.w);
            }
            else if (propBase.Contains("Scale"))
            {
                if (v.x == 0 && v.y == 0 && v.z == 0)
                {
                    transform.localScale = Vector3.one;
                }
                else
                {
                    transform.localScale = new Vector3(v.x, v.y, v.z);
                }
            }
        }
    }

    private string GetPropertyBase(string propertyName)
    {
        if (propertyName.EndsWith(".x") || propertyName.EndsWith(".y") || propertyName.EndsWith(".z") || propertyName.EndsWith(".w"))
        {
            return propertyName.Substring(0, propertyName.Length - 2);
        }
        return propertyName;
    }

    private float WrapTime(float time, float duration, WrapMode mode)
    {
        if (duration <= 0f) return time;

        switch (mode)
        {
            case WrapMode.Loop:
                var t1 = time % duration;
                if (t1 < 0f) t1 += duration;
                return t1;

            case WrapMode.PingPong:
                var cycle = time / duration;
                var cycleFloor = MathF.Floor(cycle);
                var t2 = cycle - cycleFloor;
                if ((int)cycleFloor % 2 == 1)
                {
                    t2 = 1f - t2;
                }
                return t2 * duration;

            case WrapMode.ClampForever:
                return Math.Clamp(time, 0f, duration);

            case WrapMode.Once:
            default:
                return Math.Clamp(time, 0f, duration);
        }
    }

    public void SetCurve(string relativePath, Type type, string propertyName, AnimationCurve curve)
    {
        for (int i = _bindings.Count - 1; i >= 0; i--)
        {
            var b = _bindings[i];
            if (b.path == relativePath && b.type == type && b.propertyName == propertyName)
            {
                if (curve == null)
                {
                    _bindings.RemoveAt(i);
                }
                else
                {
                    b.curve = curve;
                    _bindings[i] = b;
                }
                return;
            }
        }

        if (curve != null)
        {
            _bindings.Add(new AnimationCurveBinding
            {
                path = relativePath,
                type = type,
                propertyName = propertyName,
                curve = curve
            });
        }
    }

    public AnimationCurve GetCurve(string relativePath, Type type, string propertyName)
    {
        foreach (var b in _bindings)
        {
            if (b.path == relativePath && b.type == type && b.propertyName == propertyName)
            {
                return b.curve;
            }
        }
        return null;
    }

    public void EnsureQuaternionContinuity()
    {
    }

    public void ClearCurves()
    {
        _bindings.Clear();
    }

    public void AddEvent(AnimationEvent evt)
    {
        _events.Add(evt);
    }
}

public struct AnimationEvent
{
    public float time { get; set; }
    public string functionName { get; set; }
    public float floatParameter { get; set; }
    public int intParameter { get; set; }
    public string stringParameter { get; set; }
    public Object objectReferenceParameter { get; set; }
    public SendMessageOptions messageOptions { get; set; }
    public AnimationEventSendMessageOptions sendMessageOptions { get; set; }
    public bool isFiredByLegacy { get; set; }
    public bool isFiredByAnimator { get; set; }
    public AnimatorStateInfo animatorStateInfo { get; set; }
    public AnimatorClipInfo animatorClipInfo { get; set; }
}

public enum AnimationEventSendMessageOptions
{
    NoOptions = 0,
    DontSendToDisabledObjects = 1
}
