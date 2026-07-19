#define ANITY_NATIVE_BUILD
#include "anity/animation/anity_animation_pose.h"

#include <algorithm>
#include <cmath>

namespace {

struct Quaternion {
    float x, y, z, w;
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

Quaternion NormalizedLerp(Quaternion from, Quaternion to, float weight) {
    from = Normalize(from);
    to = Normalize(to);
    if (Dot(from, to) < 0.0f) to = Negate(to);
    return Normalize({
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

} // extern "C"
