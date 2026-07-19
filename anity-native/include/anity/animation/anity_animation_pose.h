#pragma once
#include "../anity_core.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum AnityAnimationPoseFlags {
    ANITY_ANIMATION_POSE_POSITION = 1u << 0,
    ANITY_ANIMATION_POSE_ROTATION = 1u << 1,
    ANITY_ANIMATION_POSE_SCALE = 1u << 2
} AnityAnimationPoseFlags;

typedef struct AnityAnimationTransformPose {
    float positionX, positionY, positionZ;
    float rotationX, rotationY, rotationZ, rotationW;
    float scaleX, scaleY, scaleZ;
    uint32_t flags;
} AnityAnimationTransformPose;

/* Unity Mecanim transform-layer composition. Additive mode requires referencePose. */
ANITY_API AnityResult ANITY_CALL AnityAnimation_BlendTransformPose(
    const AnityAnimationTransformPose* basePose,
    const AnityAnimationTransformPose* layerPose,
    const AnityAnimationTransformPose* referencePose,
    float weight,
    int32_t additive,
    AnityAnimationTransformPose* outPose);

#ifdef __cplusplus
}
#endif
