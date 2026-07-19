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
    private bool _quaternionContinuityEnsured;
    private AnimationClip? _additiveReferencePoseClip;
    private float _additiveReferencePoseTime;
    private bool _mecanimDataBuilt;

    public static AnimationClip Empty => new AnimationClip { name = "Empty" };

    public bool enabled { get; set; } = true;

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
        EvaluateTransformPose(go, time).Apply();
    }

    internal AnimationPose EvaluateTransformPose(GameObject go, float time)
    {
        var pose = new AnimationPose();
        if (go is null) return pose;
        float wrappedTime = WrapTime(time, length, wrapMode);
        foreach (AnimationCurveBinding binding in _bindings)
        {
            Transform target = string.IsNullOrEmpty(binding.path)
                ? go.transform
                : go.transform.Find(binding.path);
            if (target is null || binding.curve is null) continue;
            AnimationTransformSample sample = pose.GetOrCapture(target, binding.path ?? string.Empty);
            float value = binding.curve.Evaluate(wrappedTime);
            switch (binding.propertyName)
            {
                case "m_LocalPosition.x": case "localPosition.x": sample.Position.x = value; sample.Properties |= AnimationTransformProperties.Position; break;
                case "m_LocalPosition.y": case "localPosition.y": sample.Position.y = value; sample.Properties |= AnimationTransformProperties.Position; break;
                case "m_LocalPosition.z": case "localPosition.z": sample.Position.z = value; sample.Properties |= AnimationTransformProperties.Position; break;
                case "m_LocalRotation.x": case "localRotation.x": sample.Rotation.x = value; sample.Properties |= AnimationTransformProperties.Rotation; break;
                case "m_LocalRotation.y": case "localRotation.y": sample.Rotation.y = value; sample.Properties |= AnimationTransformProperties.Rotation; break;
                case "m_LocalRotation.z": case "localRotation.z": sample.Rotation.z = value; sample.Properties |= AnimationTransformProperties.Rotation; break;
                case "m_LocalRotation.w": case "localRotation.w": sample.Rotation.w = value; sample.Properties |= AnimationTransformProperties.Rotation; break;
                case "m_LocalScale.x": case "localScale.x": sample.Scale.x = value; sample.Properties |= AnimationTransformProperties.Scale; break;
                case "m_LocalScale.y": case "localScale.y": sample.Scale.y = value; sample.Properties |= AnimationTransformProperties.Scale; break;
                case "m_LocalScale.z": case "localScale.z": sample.Scale.z = value; sample.Properties |= AnimationTransformProperties.Scale; break;
                default: continue;
            }
            pose.Set(sample);
        }
        return pose;
    }

    internal void SetAdditiveReferencePose(AnimationClip? referenceClip, float time)
    {
        _additiveReferencePoseClip = _mecanimDataBuilt && referenceClip?._mecanimDataBuilt == true
            ? referenceClip
            : null;
        _additiveReferencePoseTime = time;
    }

    internal void MarkMecanimDataBuilt() => _mecanimDataBuilt = true;

    internal bool CanUseAdditiveReferencePose(AnimationClip? referenceClip)
        => _mecanimDataBuilt && referenceClip?._mecanimDataBuilt == true;

    internal bool TryEvaluateAdditiveReferencePose(GameObject go, out AnimationPose pose)
    {
        if (_additiveReferencePoseClip is null)
        {
            pose = new AnimationPose();
            return false;
        }
        pose = _additiveReferencePoseClip.EvaluateTransformPose(go, _additiveReferencePoseTime);
        return true;
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
        _quaternionContinuityEnsured = true;
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
