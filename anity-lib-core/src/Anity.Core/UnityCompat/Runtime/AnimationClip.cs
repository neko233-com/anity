using System;
using System.Collections.Generic;
using Anity.Core.Runtime.Native;

namespace UnityEngine;

public struct AnimationCurveBinding
{
    public string path;
    public Type type;
    public string propertyName;
    public AnimationCurve curve;
}

internal enum AnimationFloatPropertyKind
{
    BlendShape,
    RendererEnabled,
}

internal readonly struct AnimationFloatPropertyKey : IEquatable<AnimationFloatPropertyKey>
{
    internal AnimationFloatPropertyKey(SkinnedMeshRenderer renderer, int blendShapeIndex, string path)
    {
        Renderer = renderer;
        BlendShapeIndex = blendShapeIndex;
        Path = path;
        Kind = AnimationFloatPropertyKind.BlendShape;
    }

    internal AnimationFloatPropertyKey(Renderer renderer, string path)
    {
        Renderer = renderer;
        BlendShapeIndex = -1;
        Path = path;
        Kind = AnimationFloatPropertyKind.RendererEnabled;
    }

    internal Renderer Renderer { get; }
    internal int BlendShapeIndex { get; }
    internal string Path { get; }
    internal AnimationFloatPropertyKind Kind { get; }
    internal float CurrentValue => Kind switch
    {
        AnimationFloatPropertyKind.BlendShape when Renderer is SkinnedMeshRenderer skinned
            => skinned.GetBlendShapeWeight(BlendShapeIndex),
        AnimationFloatPropertyKind.RendererEnabled => Renderer.enabled ? 1f : 0f,
        _ => 0f,
    };
    internal void Apply(float value)
    {
        if (Kind == AnimationFloatPropertyKind.BlendShape && Renderer is SkinnedMeshRenderer skinned)
            skinned.SetBlendShapeWeight(BlendShapeIndex, value);
        else if (Kind == AnimationFloatPropertyKind.RendererEnabled)
            Renderer.enabled = value != 0f;
    }
    public bool Equals(AnimationFloatPropertyKey other)
        => ReferenceEquals(Renderer, other.Renderer) && BlendShapeIndex == other.BlendShapeIndex && Kind == other.Kind;
    public override bool Equals(object? obj) => obj is AnimationFloatPropertyKey other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Renderer?.GetInstanceID() ?? 0, BlendShapeIndex, Kind);
}

internal sealed class AnimationFloatPose
{
    private readonly Dictionary<AnimationFloatPropertyKey, float> _values = new();
    internal int Count => _values.Count;
    internal IEnumerable<KeyValuePair<AnimationFloatPropertyKey, float>> Values => _values;
    internal void Set(AnimationFloatPropertyKey key, float value) => _values[key] = value;
    internal bool TryGetValue(AnimationFloatPropertyKey key, out float value) => _values.TryGetValue(key, out value);
    internal AnimationFloatPose Clone()
    {
        var clone = new AnimationFloatPose();
        foreach (var pair in _values) clone._values[pair.Key] = pair.Value;
        return clone;
    }
    internal void Apply()
    {
        foreach (var pair in _values)
            if (pair.Key.Renderer is not null) pair.Key.Apply(pair.Value);
    }
    internal static AnimationFloatPose Blend(AnimationFloatPose lower, AnimationFloatPose upper, float weight,
        bool additive = false, AnimationFloatPose? reference = null, Func<string, bool>? pathActive = null)
    {
        var result = lower?.Clone() ?? new AnimationFloatPose();
        if (upper is null) return result;
        foreach (var pair in upper._values)
        {
            if (pathActive is not null && !pathActive(pair.Key.Path)) continue;
            var baseValue = result._values.TryGetValue(pair.Key, out var existing)
                ? existing : pair.Key.CurrentValue;
            if (additive)
            {
                var referenceValue = reference is not null && reference._values.TryGetValue(pair.Key, out var found)
                    ? found : baseValue;
                result._values[pair.Key] = baseValue + (pair.Value - referenceValue) * weight;
            }
            else result._values[pair.Key] = baseValue + (pair.Value - baseValue) * weight;
        }
        return result;
    }
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
    private bool _legacy;
    private bool _humanMotion;
    private bool _isLooping;
    private bool _loopBlend;
    private float _cycleOffset;
    private bool _hasGenericRootTransform;
    private bool _hasMotionCurves;
    private bool _hasMotionFloatCurves;
    private bool _hasRootCurves;
    private bool _lockRootRotation;
    private bool _lockRootHeightY;
    private bool _lockRootPositionXZ;

    public static AnimationClip Empty => new AnimationClip { name = "Empty" };

    public bool enabled { get; set; } = true;

    public AnimationClip()
    {
        frameRate = 60f;
        wrapMode = WrapMode.Default;
    }

    [Bindings.NativeProperty("Length", false, Bindings.TargetType.Function)]
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
    }

    public float frameRate { get; set; }
    public WrapMode wrapMode { get; set; }
    public new bool legacy { get => _legacy; set => _legacy = value; }
    public bool humanMotion => _humanMotion;
    internal override bool HumanMotion => _humanMotion;
    internal override bool LoopingMotion => _isLooping;
    internal override bool LegacyMotion => _legacy;
    public bool empty => _bindings.Count == 0;
    public bool hasGenericRootTransform => _hasGenericRootTransform;
    public bool hasMotionCurves => _hasMotionCurves;
    public bool hasMotionFloatCurves => _hasMotionFloatCurves;
    public bool hasRootCurves => _hasRootCurves;
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

    internal void SetImportedLength(float value)
    {
        float maximum = MathF.Max(0f, value);
        foreach (var binding in _bindings)
            if (binding.curve is { length: > 0 }) maximum = MathF.Max(maximum, binding.curve.keys[^1].time);
        _length = maximum;
    }

    internal void SetImportedMotionMetadata(bool human, bool hasGenericRoot, bool hasMotion,
        bool hasMotionFloat, bool hasRoot)
    {
        _humanMotion = human;
        _hasGenericRootTransform = hasGenericRoot;
        _hasMotionCurves = hasMotion;
        _hasMotionFloatCurves = hasMotionFloat;
        _hasRootCurves = hasRoot;
    }

    internal void ApplyMecanimSettings(
        bool loopTime,
        bool loopBlend,
        float cycleOffset,
        bool lockRootRotation,
        bool lockRootHeightY,
        bool lockRootPositionXZ)
    {
        _isLooping = loopTime;
        _loopBlend = loopBlend;
        _cycleOffset = float.IsFinite(cycleOffset) ? cycleOffset : 0f;
        _lockRootRotation = lockRootRotation;
        _lockRootHeightY = lockRootHeightY;
        _lockRootPositionXZ = lockRootPositionXZ;
    }

    public void SampleAnimation(GameObject go, float time)
    {
        if (go == null) return;
        EvaluateTransformPose(go, time).Apply();
        EvaluateFloatProperties(go, time).Apply();
    }

    internal AnimationFloatPose EvaluateFloatProperties(GameObject go, float time)
        => EvaluateFloatProperties(go, time, false);

    internal AnimationFloatPose EvaluateFloatProperties(GameObject go, float time, bool animatorPlayback)
    {
        var pose = new AnimationFloatPose();
        if (go is null) return pose;
        float wrappedTime = ResolveSampleTime(time, animatorPlayback, out float loopPhase);
        foreach (AnimationCurveBinding binding in _bindings)
        {
            if (binding.curve is null) continue;
            Transform target = string.IsNullOrEmpty(binding.path) ? go.transform : go.transform.Find(binding.path);
            if (target?.gameObject is null) continue;
            if (binding.type is not null && typeof(Renderer).IsAssignableFrom(binding.type) &&
                string.Equals(binding.propertyName, "m_Enabled", StringComparison.Ordinal))
            {
                if (target.gameObject.GetComponent(binding.type) is Renderer renderer)
                    pose.Set(new AnimationFloatPropertyKey(renderer, binding.path ?? string.Empty),
                        EvaluateCurve(binding.curve, wrappedTime, animatorPlayback, loopPhase));
                continue;
            }
            if (binding.type != typeof(SkinnedMeshRenderer) ||
                !binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)) continue;
            var skinned = target.gameObject.GetComponent<SkinnedMeshRenderer>();
            var mesh = skinned?.sharedMesh;
            if (skinned is null || mesh is null) continue;
            var shapeIndex = mesh.GetBlendShapeIndex(binding.propertyName[11..]);
            if (shapeIndex < 0) continue;
            pose.Set(new AnimationFloatPropertyKey(skinned, shapeIndex, binding.path ?? string.Empty),
                EvaluateCurve(binding.curve, wrappedTime, animatorPlayback, loopPhase));
        }
        return pose;
    }

    internal AnimationPose EvaluateTransformPose(GameObject go, float time)
        => EvaluateTransformPose(go, time, false);

    internal AnimationPose EvaluateTransformPose(GameObject go, float time, bool animatorPlayback)
    {
        var pose = new AnimationPose();
        if (go is null) return pose;
        float wrappedTime = ResolveSampleTime(time, animatorPlayback, out float loopPhase);
        foreach (AnimationCurveBinding binding in _bindings)
        {
            Transform target = string.IsNullOrEmpty(binding.path)
                ? go.transform
                : go.transform.Find(binding.path);
            if (target is null || binding.curve is null) continue;
            AnimationTransformSample sample = pose.GetOrCapture(target, binding.path ?? string.Empty);
            float value = EvaluateCurve(binding.curve, wrappedTime, animatorPlayback, loopPhase);
            switch (binding.propertyName)
            {
                case "m_LocalPosition.x": case "localPosition.x": sample.Position.x = value; sample.Properties |= AnimationTransformProperties.Position; break;
                case "m_LocalPosition.y": case "localPosition.y": sample.Position.y = value; sample.Properties |= AnimationTransformProperties.Position; break;
                case "m_LocalPosition.z": case "localPosition.z": sample.Position.z = value; sample.Properties |= AnimationTransformProperties.Position; break;
                case "m_LocalRotation.x": case "localRotation.x": sample.Rotation.x = value; sample.Properties |= AnimationTransformProperties.Rotation; break;
                case "m_LocalRotation.y": case "localRotation.y": sample.Rotation.y = value; sample.Properties |= AnimationTransformProperties.Rotation; break;
                case "m_LocalRotation.z": case "localRotation.z": sample.Rotation.z = value; sample.Properties |= AnimationTransformProperties.Rotation; break;
                case "m_LocalRotation.w": case "localRotation.w": sample.Rotation.w = value; sample.Properties |= AnimationTransformProperties.Rotation; break;
                case "localEulerAnglesRaw.x": sample.EulerRotation.x = value; sample.Rotation = Quaternion.Euler(sample.EulerRotation); sample.Properties |= AnimationTransformProperties.Rotation; break;
                case "localEulerAnglesRaw.y": sample.EulerRotation.y = value; sample.Rotation = Quaternion.Euler(sample.EulerRotation); sample.Properties |= AnimationTransformProperties.Rotation; break;
                case "localEulerAnglesRaw.z": sample.EulerRotation.z = value; sample.Rotation = Quaternion.Euler(sample.EulerRotation); sample.Properties |= AnimationTransformProperties.Rotation; break;
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

    internal bool TryEvaluateAdditiveReferenceFloatProperties(GameObject go, out AnimationFloatPose pose)
    {
        if (_additiveReferencePoseClip is null)
        {
            pose = new AnimationFloatPose();
            return false;
        }
        pose = _additiveReferencePoseClip.EvaluateFloatProperties(go, _additiveReferencePoseTime);
        return true;
    }

    internal bool TryEvaluateRootMotion(float time, float humanScale, out AnimationRootMotionPose pose)
    {
        if (!TryGetRootMotionCurves(
                out AnimationCurve? tx, out AnimationCurve? ty, out AnimationCurve? tz,
                out AnimationCurve? qx, out AnimationCurve? qy,
                out AnimationCurve? qz, out AnimationCurve? qw,
                out bool humanoid))
        {
            pose = AnimationRootMotionPose.Identity;
            return false;
        }

        float duration = length;
        float animatorTime = float.IsFinite(time) ? time : 0f;
        animatorTime += _cycleOffset * duration;
        long completedLoops = 0;
        float sampleTime;
        if (_isLooping && duration > 0f)
        {
            double cycles = Math.Floor((double)animatorTime / duration);
            completedLoops = cycles <= long.MinValue ? long.MinValue
                : cycles >= long.MaxValue ? long.MaxValue : (long)cycles;
            sampleTime = animatorTime - (float)(cycles * duration);
            if (sampleTime < 0f) sampleTime = 0f;
            else if (sampleTime >= duration) sampleTime = 0f;
        }
        else sampleTime = duration > 0f ? Math.Clamp(animatorTime, 0f, duration) : 0f;

        AnimationRootMotionPose start = EvaluateRawRootMotion(
            0f, tx, ty, tz, qx, qy, qz, qw);
        AnimationRootMotionPose end = EvaluateRawRootMotion(
            duration, tx, ty, tz, qx, qy, qz, qw);
        AnimationRootMotionPose sample = EvaluateRawRootMotion(
            sampleTime, tx, ty, tz, qx, qy, qz, qw);
        if (humanoid)
        {
            AnityNative.AnimationHumanoidRootMotionFlags flags =
                (_lockRootRotation ? AnityNative.AnimationHumanoidRootMotionFlags.LockRotation : 0) |
                (_lockRootHeightY ? AnityNative.AnimationHumanoidRootMotionFlags.LockHeightY : 0) |
                (_lockRootPositionXZ ? AnityNative.AnimationHumanoidRootMotionFlags.LockPositionXZ : 0);
            float scale = float.IsFinite(humanScale) && humanScale > 0f ? humanScale : 1f;
            AnimationRootMotionPose reference = start;
            start = AnimationRootMotionPose.PrepareHumanoid(reference, start, scale, flags);
            end = AnimationRootMotionPose.PrepareHumanoid(reference, end, scale, flags);
            sample = AnimationRootMotionPose.PrepareHumanoid(reference, sample, scale, flags);
        }
        pose = AnimationRootMotionPose.ResolveLooped(
            start, end, sample, _isLooping ? completedLoops : 0);
        return true;
    }

    private bool TryGetRootMotionCurves(
        out AnimationCurve? tx, out AnimationCurve? ty, out AnimationCurve? tz,
        out AnimationCurve? qx, out AnimationCurve? qy,
        out AnimationCurve? qz, out AnimationCurve? qw,
        out bool humanoid)
    {
        tx = GetCurve(string.Empty, typeof(Animator), "RootT.x");
        ty = GetCurve(string.Empty, typeof(Animator), "RootT.y");
        tz = GetCurve(string.Empty, typeof(Animator), "RootT.z");
        qx = GetCurve(string.Empty, typeof(Animator), "RootQ.x");
        qy = GetCurve(string.Empty, typeof(Animator), "RootQ.y");
        qz = GetCurve(string.Empty, typeof(Animator), "RootQ.z");
        qw = GetCurve(string.Empty, typeof(Animator), "RootQ.w");
        bool hasPosition = tx is not null && ty is not null && tz is not null;
        bool hasRotation = qx is not null && qy is not null && qz is not null && qw is not null;
        if (hasPosition || hasRotation)
        {
            humanoid = true;
            return true;
        }

        tx = GetCurve(string.Empty, typeof(Animator), "MotionT.x");
        ty = GetCurve(string.Empty, typeof(Animator), "MotionT.y");
        tz = GetCurve(string.Empty, typeof(Animator), "MotionT.z");
        qx = GetCurve(string.Empty, typeof(Animator), "MotionQ.x");
        qy = GetCurve(string.Empty, typeof(Animator), "MotionQ.y");
        qz = GetCurve(string.Empty, typeof(Animator), "MotionQ.z");
        qw = GetCurve(string.Empty, typeof(Animator), "MotionQ.w");
        hasPosition = tx is not null && ty is not null && tz is not null;
        hasRotation = qx is not null && qy is not null && qz is not null && qw is not null;
        humanoid = false;
        return hasPosition || hasRotation;
    }

    private static AnimationRootMotionPose EvaluateRawRootMotion(
        float time,
        AnimationCurve? tx, AnimationCurve? ty, AnimationCurve? tz,
        AnimationCurve? qx, AnimationCurve? qy, AnimationCurve? qz, AnimationCurve? qw)
    {
        Vector3 position = tx is not null && ty is not null && tz is not null
            ? new Vector3(tx.Evaluate(time), ty.Evaluate(time), tz.Evaluate(time))
            : Vector3.zero;
        Quaternion rotation = qx is not null && qy is not null && qz is not null && qw is not null
            ? new Quaternion(qx.Evaluate(time), qy.Evaluate(time), qz.Evaluate(time), qw.Evaluate(time))
            : Quaternion.identity;
        return new AnimationRootMotionPose(position, rotation);
    }

    private float ResolveSampleTime(float time, bool animatorPlayback, out float loopPhase)
    {
        float duration = length;
        if (!animatorPlayback)
        {
            float directTime = WrapTime(time, duration, wrapMode);
            loopPhase = duration > 0f ? Math.Clamp(directTime / duration, 0f, 1f) : 0f;
            return directTime;
        }

        if (duration <= 0f)
        {
            loopPhase = 0f;
            return time;
        }

        float animatorTime = time + _cycleOffset * duration;
        if (_isLooping)
        {
            animatorTime %= duration;
            if (animatorTime < 0f) animatorTime += duration;
        }
        else animatorTime = Math.Clamp(animatorTime, 0f, duration);
        loopPhase = Math.Clamp(animatorTime / duration, 0f, 1f);
        return animatorTime;
    }

    private float EvaluateCurve(AnimationCurve curve, float time, bool animatorPlayback, float loopPhase)
    {
        float value = curve.Evaluate(time);
        if (!animatorPlayback || !_isLooping || !_loopBlend || curve.length == 0 || loopPhase <= 0f)
            return value;
        float first = curve.Evaluate(0f);
        float last = curve.Evaluate(length);
        return value - (last - first) * loopPhase;
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
