#define ANITY_NATIVE_BUILD
#include "anity/animation/anity_animation_pose.h"

#include <algorithm>
#include <cmath>

namespace {

struct Quaternion {
    float x, y, z, w;
};

struct Vector3 {
    float x, y, z;
};

struct RootMotionPose {
    Vector3 position;
    Quaternion rotation;
};

float Clamp01(float value) {
    return std::max(0.0f, std::min(1.0f, value));
}

Quaternion Normalize(Quaternion value) {
    const float lengthSquared = value.x * value.x + value.y * value.y +
                                value.z * value.z + value.w * value.w;
    if (!std::isfinite(lengthSquared) || lengthSquared <= 1.0e-12f)
        return {0.0f, 0.0f, 0.0f, 1.0f};
    const float inverseLength = 1.0f / std::sqrt(lengthSquared);
    return {value.x * inverseLength, value.y * inverseLength,
            value.z * inverseLength, value.w * inverseLength};
}

bool IsFinite(Vector3 value) {
    return std::isfinite(value.x) && std::isfinite(value.y) && std::isfinite(value.z);
}

bool IsFinite(Quaternion value) {
    return std::isfinite(value.x) && std::isfinite(value.y) &&
           std::isfinite(value.z) && std::isfinite(value.w);
}

float Dot(Quaternion a, Quaternion b) {
    return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
}

Quaternion Negate(Quaternion value) {
    return {-value.x, -value.y, -value.z, -value.w};
}

Quaternion Conjugate(Quaternion value) {
    return {-value.x, -value.y, -value.z, value.w};
}

Quaternion Multiply(Quaternion a, Quaternion b) {
    return {
        a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
        a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
        a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
        a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z
    };
}

Vector3 Add(Vector3 a, Vector3 b) {
    return {a.x + b.x, a.y + b.y, a.z + b.z};
}

Vector3 Subtract(Vector3 a, Vector3 b) {
    return {a.x - b.x, a.y - b.y, a.z - b.z};
}

Vector3 Multiply(Vector3 value, float scalar) {
    return {value.x * scalar, value.y * scalar, value.z * scalar};
}

Vector3 Rotate(Quaternion rotation, Vector3 point) {
    rotation = Normalize(rotation);
    const Quaternion vector{point.x, point.y, point.z, 0.0f};
    const Quaternion result = Multiply(Multiply(rotation, vector), Conjugate(rotation));
    return {result.x, result.y, result.z};
}

RootMotionPose Normalize(RootMotionPose value) {
    value.rotation = Normalize(value.rotation);
    return value;
}

RootMotionPose Compose(RootMotionPose first, RootMotionPose second) {
    first = Normalize(first);
    second = Normalize(second);
    return {
        Add(first.position, Rotate(first.rotation, second.position)),
        Normalize(Multiply(first.rotation, second.rotation))
    };
}

RootMotionPose Inverse(RootMotionPose value) {
    value = Normalize(value);
    const Quaternion inverseRotation = Conjugate(value.rotation);
    return {
        Rotate(inverseRotation, Multiply(value.position, -1.0f)),
        inverseRotation
    };
}

RootMotionPose IdentityRootMotion() {
    return {{0.0f, 0.0f, 0.0f}, {0.0f, 0.0f, 0.0f, 1.0f}};
}

RootMotionPose Power(RootMotionPose value, int64_t exponent) {
    RootMotionPose factor = exponent < 0 ? Inverse(value) : Normalize(value);
    uint64_t remaining = exponent < 0
        ? static_cast<uint64_t>(-(exponent + 1)) + 1u
        : static_cast<uint64_t>(exponent);
    RootMotionPose result = IdentityRootMotion();
    while (remaining != 0u) {
        if ((remaining & 1u) != 0u) result = Compose(result, factor);
        remaining >>= 1u;
        if (remaining != 0u) factor = Compose(factor, factor);
    }
    return result;
}

RootMotionPose ReadRootMotion(const AnityAnimationRootMotionPose& pose) {
    return {
        {pose.positionX, pose.positionY, pose.positionZ},
        {pose.rotationX, pose.rotationY, pose.rotationZ, pose.rotationW}
    };
}

void WriteRootMotion(AnityAnimationRootMotionPose& pose, RootMotionPose value) {
    value = Normalize(value);
    pose.positionX = value.position.x;
    pose.positionY = value.position.y;
    pose.positionZ = value.position.z;
    pose.rotationX = value.rotation.x;
    pose.rotationY = value.rotation.y;
    pose.rotationZ = value.rotation.z;
    pose.rotationW = value.rotation.w;
}

bool IsFinite(RootMotionPose value) {
    return IsFinite(value.position) && IsFinite(value.rotation);
}

Quaternion NormalizedLerp(Quaternion from, Quaternion to, float weight) {
    from = Normalize(from);
    to = Normalize(to);
    if (Dot(from, to) < 0.0f) to = Negate(to);
    return Normalize(Quaternion{
        from.x + (to.x - from.x) * weight,
        from.y + (to.y - from.y) * weight,
        from.z + (to.z - from.z) * weight,
        from.w + (to.w - from.w) * weight
    });
}

Quaternion ReadRotation(const AnityAnimationTransformPose& pose) {
    return {pose.rotationX, pose.rotationY, pose.rotationZ, pose.rotationW};
}

void WriteRotation(AnityAnimationTransformPose& pose, Quaternion value) {
    pose.rotationX = value.x;
    pose.rotationY = value.y;
    pose.rotationZ = value.z;
    pose.rotationW = value.w;
}

float SafeScaleRatio(float value, float reference) {
    return std::abs(reference) <= 1.0e-8f ? 1.0f : value / reference;
}

} // namespace

extern "C" {

AnityResult ANITY_CALL AnityAnimation_BlendTransformPose(
    const AnityAnimationTransformPose* basePose,
    const AnityAnimationTransformPose* layerPose,
    const AnityAnimationTransformPose* referencePose,
    float weight,
    int32_t additive,
    AnityAnimationTransformPose* outPose) {
    if (!basePose || !layerPose || !outPose || !std::isfinite(weight))
        return ANITY_ERR_INVALID_ARG;
    if (additive != 0 && !referencePose) return ANITY_ERR_INVALID_ARG;

    const float blendWeight = Clamp01(weight);
    *outPose = *basePose;
    outPose->flags = basePose->flags | layerPose->flags;

    if (additive == 0) {
        if ((layerPose->flags & ANITY_ANIMATION_POSE_POSITION) != 0) {
            outPose->positionX += (layerPose->positionX - outPose->positionX) * blendWeight;
            outPose->positionY += (layerPose->positionY - outPose->positionY) * blendWeight;
            outPose->positionZ += (layerPose->positionZ - outPose->positionZ) * blendWeight;
        }
        if ((layerPose->flags & ANITY_ANIMATION_POSE_ROTATION) != 0)
            WriteRotation(*outPose, NormalizedLerp(ReadRotation(*basePose), ReadRotation(*layerPose), blendWeight));
        if ((layerPose->flags & ANITY_ANIMATION_POSE_SCALE) != 0) {
            outPose->scaleX += (layerPose->scaleX - outPose->scaleX) * blendWeight;
            outPose->scaleY += (layerPose->scaleY - outPose->scaleY) * blendWeight;
            outPose->scaleZ += (layerPose->scaleZ - outPose->scaleZ) * blendWeight;
        }
        return ANITY_OK;
    }

    if ((layerPose->flags & ANITY_ANIMATION_POSE_POSITION) != 0 &&
        (referencePose->flags & ANITY_ANIMATION_POSE_POSITION) != 0) {
        outPose->positionX += (layerPose->positionX - referencePose->positionX) * blendWeight;
        outPose->positionY += (layerPose->positionY - referencePose->positionY) * blendWeight;
        outPose->positionZ += (layerPose->positionZ - referencePose->positionZ) * blendWeight;
    }
    if ((layerPose->flags & ANITY_ANIMATION_POSE_ROTATION) != 0 &&
        (referencePose->flags & ANITY_ANIMATION_POSE_ROTATION) != 0) {
        const Quaternion reference = Normalize(ReadRotation(*referencePose));
        const Quaternion layer = Normalize(ReadRotation(*layerPose));
        const Quaternion delta = Normalize(Multiply(Conjugate(reference), layer));
        const Quaternion weightedDelta = NormalizedLerp({0.0f, 0.0f, 0.0f, 1.0f}, delta, blendWeight);
        WriteRotation(*outPose, Normalize(Multiply(ReadRotation(*basePose), weightedDelta)));
    }
    if ((layerPose->flags & ANITY_ANIMATION_POSE_SCALE) != 0 &&
        (referencePose->flags & ANITY_ANIMATION_POSE_SCALE) != 0) {
        const float ratioX = SafeScaleRatio(layerPose->scaleX, referencePose->scaleX);
        const float ratioY = SafeScaleRatio(layerPose->scaleY, referencePose->scaleY);
        const float ratioZ = SafeScaleRatio(layerPose->scaleZ, referencePose->scaleZ);
        outPose->scaleX *= 1.0f + (ratioX - 1.0f) * blendWeight;
        outPose->scaleY *= 1.0f + (ratioY - 1.0f) * blendWeight;
        outPose->scaleZ *= 1.0f + (ratioZ - 1.0f) * blendWeight;
    }
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAnimation_ResolveRootMotion(
    const AnityAnimationRootMotionPose* startPose,
    const AnityAnimationRootMotionPose* endPose,
    const AnityAnimationRootMotionPose* samplePose,
    int64_t completedLoops,
    AnityAnimationRootMotionPose* outPose) {
    if (!startPose || !endPose || !samplePose || !outPose)
        return ANITY_ERR_INVALID_ARG;
    const RootMotionPose start = ReadRootMotion(*startPose);
    const RootMotionPose end = ReadRootMotion(*endPose);
    const RootMotionPose sample = ReadRootMotion(*samplePose);
    if (!IsFinite(start) || !IsFinite(end) || !IsFinite(sample))
        return ANITY_ERR_INVALID_ARG;

    const RootMotionPose inverseStart = Inverse(start);
    const RootMotionPose cycle = Compose(inverseStart, end);
    const RootMotionPose partial = Compose(inverseStart, sample);
    WriteRootMotion(*outPose, Compose(Power(cycle, completedLoops), partial));
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAnimation_BlendRootMotion(
    const AnityAnimationRootMotionPose* basePose,
    const AnityAnimationRootMotionPose* layerPose,
    float weight,
    AnityAnimationRootMotionPose* outPose) {
    if (!basePose || !layerPose || !outPose || !std::isfinite(weight))
        return ANITY_ERR_INVALID_ARG;
    const RootMotionPose base = ReadRootMotion(*basePose);
    const RootMotionPose layer = ReadRootMotion(*layerPose);
    if (!IsFinite(base) || !IsFinite(layer)) return ANITY_ERR_INVALID_ARG;
    const float blendWeight = Clamp01(weight);
    WriteRootMotion(*outPose, {
        Add(base.position, Multiply(Subtract(layer.position, base.position), blendWeight)),
        NormalizedLerp(base.rotation, layer.rotation, blendWeight)
    });
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAnimation_AnchorRootMotion(
    const AnityAnimationRootMotionPose* anchorPose,
    const AnityAnimationRootMotionPose* motionPose,
    AnityAnimationRootMotionPose* outPose) {
    if (!anchorPose || !motionPose || !outPose) return ANITY_ERR_INVALID_ARG;
    const RootMotionPose anchor = ReadRootMotion(*anchorPose);
    const RootMotionPose motion = ReadRootMotion(*motionPose);
    if (!IsFinite(anchor) || !IsFinite(motion)) return ANITY_ERR_INVALID_ARG;
    WriteRootMotion(*outPose, Compose(anchor, motion));
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAnimation_CalculateRootMotionAnchor(
    const AnityAnimationRootMotionPose* worldPose,
    const AnityAnimationRootMotionPose* motionPose,
    AnityAnimationRootMotionPose* outAnchorPose) {
    if (!worldPose || !motionPose || !outAnchorPose) return ANITY_ERR_INVALID_ARG;
    const RootMotionPose world = ReadRootMotion(*worldPose);
    const RootMotionPose motion = ReadRootMotion(*motionPose);
    if (!IsFinite(world) || !IsFinite(motion)) return ANITY_ERR_INVALID_ARG;
    WriteRootMotion(*outAnchorPose, Compose(world, Inverse(motion)));
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAnimation_CalculateRootMotionDelta(
    const AnityAnimationRootMotionPose* previousPose,
    const AnityAnimationRootMotionPose* currentPose,
    float deltaTime,
    AnityAnimationRootMotionDelta* outDelta) {
    if (!previousPose || !currentPose || !outDelta || !std::isfinite(deltaTime) || deltaTime < 0.0f)
        return ANITY_ERR_INVALID_ARG;
    RootMotionPose previous = Normalize(ReadRootMotion(*previousPose));
    RootMotionPose current = Normalize(ReadRootMotion(*currentPose));
    if (!IsFinite(previous) || !IsFinite(current)) return ANITY_ERR_INVALID_ARG;

    const Vector3 deltaPosition = Subtract(current.position, previous.position);
    Quaternion deltaRotation = Normalize(Multiply(current.rotation, Conjugate(previous.rotation)));
    if (deltaRotation.w < 0.0f) deltaRotation = Negate(deltaRotation);
    Vector3 velocity{0.0f, 0.0f, 0.0f};
    Vector3 angularVelocity{0.0f, 0.0f, 0.0f};
    if (deltaTime > 1.0e-8f) {
        velocity = Multiply(deltaPosition, 1.0f / deltaTime);
        const float clampedW = std::max(-1.0f, std::min(1.0f, deltaRotation.w));
        const float halfSine = std::sqrt(std::max(0.0f, 1.0f - clampedW * clampedW));
        if (halfSine > 1.0e-7f) {
            const float angle = 2.0f * std::acos(clampedW);
            angularVelocity = Multiply(
                {deltaRotation.x / halfSine, deltaRotation.y / halfSine,
                 deltaRotation.z / halfSine},
                angle / deltaTime);
        }
    }

    outDelta->positionX = deltaPosition.x;
    outDelta->positionY = deltaPosition.y;
    outDelta->positionZ = deltaPosition.z;
    outDelta->rotationX = deltaRotation.x;
    outDelta->rotationY = deltaRotation.y;
    outDelta->rotationZ = deltaRotation.z;
    outDelta->rotationW = deltaRotation.w;
    outDelta->velocityX = velocity.x;
    outDelta->velocityY = velocity.y;
    outDelta->velocityZ = velocity.z;
    outDelta->angularVelocityX = angularVelocity.x;
    outDelta->angularVelocityY = angularVelocity.y;
    outDelta->angularVelocityZ = angularVelocity.z;
    return ANITY_OK;
}

} // extern "C"
