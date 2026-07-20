using System;
using System.Collections.Generic;
using Anity.Core.Runtime.Native;

namespace UnityEngine;

[Flags]
internal enum AnimationTransformProperties : uint
{
    None = 0,
    Position = 1u << 0,
    Rotation = 1u << 1,
    Scale = 1u << 2,
    All = Position | Rotation | Scale
}

internal struct AnimationTransformSample
{
    public Transform Transform;
    public string Path;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 EulerRotation;
    public Vector3 Scale;
    public AnimationTransformProperties Properties;

    public static AnimationTransformSample Capture(Transform transform, string path)
        => new()
        {
            Transform = transform,
            Path = path ?? string.Empty,
            Position = transform.localPosition,
            Rotation = transform.localRotation,
            EulerRotation = transform.localEulerAngles,
            Scale = transform.localScale,
            Properties = AnimationTransformProperties.None
        };

    public readonly AnityNative.AnimationTransformPose ToNative()
        => new()
        {
            positionX = Position.x,
            positionY = Position.y,
            positionZ = Position.z,
            rotationX = Rotation.x,
            rotationY = Rotation.y,
            rotationZ = Rotation.z,
            rotationW = Rotation.w,
            scaleX = Scale.x,
            scaleY = Scale.y,
            scaleZ = Scale.z,
            flags = (AnityNative.AnimationPoseFlags)Properties
        };

    public static AnimationTransformSample FromNative(
        AnimationTransformSample identity,
        in AnityNative.AnimationTransformPose native)
    {
        identity.Position = new Vector3(native.positionX, native.positionY, native.positionZ);
        identity.Rotation = new Quaternion(native.rotationX, native.rotationY, native.rotationZ, native.rotationW);
        identity.Scale = new Vector3(native.scaleX, native.scaleY, native.scaleZ);
        identity.Properties = (AnimationTransformProperties)native.flags;
        return identity;
    }
}

internal readonly struct AnimationRootMotionPose
{
    internal AnimationRootMotionPose(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation.normalized;
    }

    internal Vector3 Position { get; }
    internal Quaternion Rotation { get; }
    internal static AnimationRootMotionPose Identity => new(Vector3.zero, Quaternion.identity);

    internal AnityNative.AnimationRootMotionPose ToNative() => new()
    {
        positionX = Position.x,
        positionY = Position.y,
        positionZ = Position.z,
        rotationX = Rotation.x,
        rotationY = Rotation.y,
        rotationZ = Rotation.z,
        rotationW = Rotation.w,
    };

    internal static AnimationRootMotionPose FromNative(in AnityNative.AnimationRootMotionPose pose)
        => new(
            new Vector3(pose.positionX, pose.positionY, pose.positionZ),
            new Quaternion(pose.rotationX, pose.rotationY, pose.rotationZ, pose.rotationW));

    internal static AnimationRootMotionPose ResolveLooped(
        AnimationRootMotionPose start,
        AnimationRootMotionPose end,
        AnimationRootMotionPose sample,
        long completedLoops)
    {
        AnityNative.AnimationRootMotionPose nativeStart = start.ToNative();
        AnityNative.AnimationRootMotionPose nativeEnd = end.ToNative();
        AnityNative.AnimationRootMotionPose nativeSample = sample.ToNative();
        if (!AnityNative.TryResolveAnimationRootMotion(
                in nativeStart, in nativeEnd, in nativeSample, completedLoops,
                out AnityNative.AnimationRootMotionPose result))
            throw new InvalidOperationException("anity-native root-motion loop resolution failed.");
        return FromNative(in result);
    }

    internal static AnimationRootMotionPose PrepareHumanoid(
        AnimationRootMotionPose reference,
        AnimationRootMotionPose sample,
        float humanScale,
        AnityNative.AnimationHumanoidRootMotionFlags flags)
    {
        AnityNative.AnimationRootMotionPose nativeReference = reference.ToNative();
        AnityNative.AnimationRootMotionPose nativeSample = sample.ToNative();
        if (!AnityNative.TryPrepareHumanoidAnimationRootMotion(
                in nativeReference, in nativeSample, humanScale, flags,
                out AnityNative.AnimationRootMotionPose result))
            throw new InvalidOperationException("anity-native humanoid root-motion preparation failed.");
        return FromNative(in result);
    }

    internal static AnimationRootMotionPose Blend(
        AnimationRootMotionPose lower,
        AnimationRootMotionPose upper,
        float weight)
    {
        AnityNative.AnimationRootMotionPose nativeLower = lower.ToNative();
        AnityNative.AnimationRootMotionPose nativeUpper = upper.ToNative();
        if (!AnityNative.TryBlendAnimationRootMotion(
                in nativeLower, in nativeUpper, weight,
                out AnityNative.AnimationRootMotionPose result))
            throw new InvalidOperationException("anity-native root-motion blend failed.");
        return FromNative(in result);
    }

    internal static AnimationRootMotionPose Anchor(
        AnimationRootMotionPose anchor,
        AnimationRootMotionPose motion)
    {
        AnityNative.AnimationRootMotionPose nativeAnchor = anchor.ToNative();
        AnityNative.AnimationRootMotionPose nativeMotion = motion.ToNative();
        if (!AnityNative.TryAnchorAnimationRootMotion(
                in nativeAnchor, in nativeMotion,
                out AnityNative.AnimationRootMotionPose result))
            throw new InvalidOperationException("anity-native root-motion anchoring failed.");
        return FromNative(in result);
    }

    internal static AnimationRootMotionPose CalculateAnchor(
        AnimationRootMotionPose world,
        AnimationRootMotionPose motion)
    {
        AnityNative.AnimationRootMotionPose nativeWorld = world.ToNative();
        AnityNative.AnimationRootMotionPose nativeMotion = motion.ToNative();
        if (!AnityNative.TryCalculateAnimationRootMotionAnchor(
                in nativeWorld, in nativeMotion,
                out AnityNative.AnimationRootMotionPose result))
            throw new InvalidOperationException("anity-native root-motion anchor calculation failed.");
        return FromNative(in result);
    }

    internal static AnimationRootMotionDelta CalculateDelta(
        AnimationRootMotionPose previous,
        AnimationRootMotionPose current,
        float deltaTime)
    {
        AnityNative.AnimationRootMotionPose nativePrevious = previous.ToNative();
        AnityNative.AnimationRootMotionPose nativeCurrent = current.ToNative();
        if (!AnityNative.TryCalculateAnimationRootMotionDelta(
                in nativePrevious, in nativeCurrent, deltaTime,
                out AnityNative.AnimationRootMotionDelta result))
            throw new InvalidOperationException("anity-native root-motion delta calculation failed.");
        return new AnimationRootMotionDelta(
            new Vector3(result.positionX, result.positionY, result.positionZ),
            new Quaternion(result.rotationX, result.rotationY, result.rotationZ, result.rotationW),
            new Vector3(result.velocityX, result.velocityY, result.velocityZ),
            new Vector3(result.angularVelocityX, result.angularVelocityY, result.angularVelocityZ));
    }
}

internal readonly struct AnimationRootMotionDelta
{
    internal AnimationRootMotionDelta(
        Vector3 position,
        Quaternion rotation,
        Vector3 velocity,
        Vector3 angularVelocity)
    {
        Position = position;
        Rotation = rotation;
        Velocity = velocity;
        AngularVelocity = angularVelocity;
    }

    internal Vector3 Position { get; }
    internal Quaternion Rotation { get; }
    internal Vector3 Velocity { get; }
    internal Vector3 AngularVelocity { get; }
}

internal sealed class AnimationPose
{
    private readonly Dictionary<Transform, AnimationTransformSample> _samples = new();

    public int Count => _samples.Count;

    public IEnumerable<AnimationTransformSample> Samples => _samples.Values;

    public bool TryGet(Transform transform, out AnimationTransformSample sample)
        => _samples.TryGetValue(transform, out sample);

    public AnimationTransformSample GetOrCapture(Transform transform, string path)
        => _samples.TryGetValue(transform, out AnimationTransformSample sample)
            ? sample
            : AnimationTransformSample.Capture(transform, path);

    public void Set(AnimationTransformSample sample)
    {
        if (sample.Transform is not null) _samples[sample.Transform] = sample;
    }

    public AnimationPose Clone()
    {
        var clone = new AnimationPose();
        foreach (AnimationTransformSample sample in _samples.Values) clone.Set(sample);
        return clone;
    }

    public void Apply()
    {
        foreach (AnimationTransformSample sample in _samples.Values)
        {
            if ((sample.Properties & AnimationTransformProperties.Position) != 0)
                sample.Transform.localPosition = sample.Position;
            if ((sample.Properties & AnimationTransformProperties.Rotation) != 0)
                sample.Transform.localRotation = sample.Rotation;
            if ((sample.Properties & AnimationTransformProperties.Scale) != 0)
                sample.Transform.localScale = sample.Scale;
        }
    }

    public static AnimationPose Blend(
        AnimationPose basePose,
        AnimationPose layerPose,
        float weight,
        bool additive = false,
        AnimationPose? referencePose = null,
        Func<string, bool>? pathActive = null)
    {
        if (basePose is null) throw new ArgumentNullException(nameof(basePose));
        if (layerPose is null) throw new ArgumentNullException(nameof(layerPose));
        var result = basePose.Clone();
        float clampedWeight = Mathf.Clamp01(weight);
        if (clampedWeight <= 0f) return result;

        foreach (AnimationTransformSample layerSample in layerPose.Samples)
        {
            if (pathActive is not null && !pathActive(layerSample.Path)) continue;
            AnimationTransformSample baseSample = result.GetOrCapture(layerSample.Transform, layerSample.Path);
            AnityNative.AnimationTransformPose nativeBase = baseSample.ToNative();
            AnityNative.AnimationTransformPose nativeLayer = layerSample.ToNative();
            AnityNative.AnimationTransformPose? nativeReference = null;
            if (additive)
            {
                if (referencePose is null || !referencePose.TryGet(layerSample.Transform, out AnimationTransformSample referenceSample))
                    continue;
                nativeReference = referenceSample.ToNative();
            }

            if (!AnityNative.TryBlendAnimationTransformPose(
                    in nativeBase,
                    in nativeLayer,
                    nativeReference,
                    clampedWeight,
                    additive,
                    out AnityNative.AnimationTransformPose nativeResult))
                throw new InvalidOperationException("anity-native animation pose blend failed.");
            result.Set(AnimationTransformSample.FromNative(baseSample, in nativeResult));
        }
        return result;
    }
}
