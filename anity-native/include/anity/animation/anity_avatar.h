#pragma once
#include "../anity_core.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct AnityAvatarSkeletonBoneDesc {
    uint64_t nameHash;
    int32_t parentIndex;
    float positionX, positionY, positionZ;
    float rotationX, rotationY, rotationZ, rotationW;
    float scaleX, scaleY, scaleZ;
} AnityAvatarSkeletonBoneDesc;

typedef struct AnityAvatarHumanBoneDesc {
    uint64_t boneNameHash;
    uint64_t humanNameHash;
} AnityAvatarHumanBoneDesc;

typedef enum AnityAvatarValidationFlags {
    ANITY_AVATAR_VALID = 0,
    ANITY_AVATAR_EMPTY_SKELETON = 1u << 0,
    ANITY_AVATAR_INVALID_SKELETON_NAME = 1u << 1,
    ANITY_AVATAR_DUPLICATE_SKELETON_NAME = 1u << 2,
    ANITY_AVATAR_INVALID_PARENT = 1u << 3,
    ANITY_AVATAR_MULTIPLE_ROOTS = 1u << 4,
    ANITY_AVATAR_HIERARCHY_CYCLE = 1u << 5,
    ANITY_AVATAR_INVALID_TRANSFORM = 1u << 6,
    ANITY_AVATAR_EMPTY_HUMAN_MAPPING = 1u << 7,
    ANITY_AVATAR_INVALID_HUMAN_MAPPING = 1u << 8,
    ANITY_AVATAR_DUPLICATE_HUMAN_BONE = 1u << 9,
    ANITY_AVATAR_DUPLICATE_HUMAN_NAME = 1u << 10,
    ANITY_AVATAR_MISSING_MAPPED_BONE = 1u << 11,
    ANITY_AVATAR_MISSING_REQUIRED_HUMAN_BONE = 1u << 12,
    ANITY_AVATAR_MISSING_ROOT_MOTION_TRANSFORM = 1u << 13
} AnityAvatarValidationFlags;

typedef struct AnityAvatarBuildResult {
    uint32_t flags;
    int32_t rootIndex;
    int32_t errorIndex;
    int32_t mappedBoneCount;
    float humanScale;
} AnityAvatarBuildResult;

/* Validates the transform hierarchy, rest pose and Unity humanoid mapping. */
ANITY_API AnityResult ANITY_CALL AnityAvatar_ValidateHuman(
    const AnityAvatarSkeletonBoneDesc* skeleton,
    int32_t skeletonCount,
    const AnityAvatarHumanBoneDesc* human,
    int32_t humanCount,
    AnityAvatarBuildResult* outResult);

/* Validates a generic hierarchy and resolves the named root-motion transform. */
ANITY_API AnityResult ANITY_CALL AnityAvatar_ValidateGeneric(
    const AnityAvatarSkeletonBoneDesc* skeleton,
    int32_t skeletonCount,
    uint64_t rootMotionTransformNameHash,
    AnityAvatarBuildResult* outResult);

#ifdef __cplusplus
}
#endif
