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

typedef struct AnityAnimationRootMotionPose {
    float positionX, positionY, positionZ;
    float rotationX, rotationY, rotationZ, rotationW;
} AnityAnimationRootMotionPose;

typedef struct AnityAnimationRootMotionDelta {
    float positionX, positionY, positionZ;
    float rotationX, rotationY, rotationZ, rotationW;
    float velocityX, velocityY, velocityZ;
    float angularVelocityX, angularVelocityY, angularVelocityZ;
} AnityAnimationRootMotionDelta;

/* Unity Mecanim transform-layer composition. Additive mode requires referencePose. */
ANITY_API AnityResult ANITY_CALL AnityAnimation_BlendTransformPose(
    const AnityAnimationTransformPose* basePose,
    const AnityAnimationTransformPose* layerPose,
    const AnityAnimationTransformPose* referencePose,
    float weight,
    int32_t additive,
    AnityAnimationTransformPose* outPose);

/* Resolves MotionT/MotionQ relative to the first sample and accumulates complete loops. */
ANITY_API AnityResult ANITY_CALL AnityAnimation_ResolveRootMotion(
    const AnityAnimationRootMotionPose* startPose,
    const AnityAnimationRootMotionPose* endPose,
    const AnityAnimationRootMotionPose* samplePose,
    int64_t completedLoops,
    AnityAnimationRootMotionPose* outPose);

/* Mecanim transition/blend-tree root-motion interpolation. */
ANITY_API AnityResult ANITY_CALL AnityAnimation_BlendRootMotion(
    const AnityAnimationRootMotionPose* basePose,
    const AnityAnimationRootMotionPose* layerPose,
    float weight,
    AnityAnimationRootMotionPose* outPose);

/* Applies the Animator GameObject's captured world-space anchor to relative motion. */
ANITY_API AnityResult ANITY_CALL AnityAnimation_AnchorRootMotion(
    const AnityAnimationRootMotionPose* anchorPose,
    const AnityAnimationRootMotionPose* motionPose,
    AnityAnimationRootMotionPose* outPose);

/* Computes the anchor that maps a sampled motion pose onto the current world pose. */
ANITY_API AnityResult ANITY_CALL AnityAnimation_CalculateRootMotionAnchor(
    const AnityAnimationRootMotionPose* worldPose,
    const AnityAnimationRootMotionPose* motionPose,
    AnityAnimationRootMotionPose* outAnchorPose);

/* Computes Unity-style world delta, linear velocity and angular velocity. */
ANITY_API AnityResult ANITY_CALL AnityAnimation_CalculateRootMotionDelta(
    const AnityAnimationRootMotionPose* previousPose,
    const AnityAnimationRootMotionPose* currentPose,
    float deltaTime,
    AnityAnimationRootMotionDelta* outDelta);

#ifdef __cplusplus
}
#endif
