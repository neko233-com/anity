#define ANITY_NATIVE_BUILD
#include "anity/animation/anity_avatar.h"

#include <array>
#include <cmath>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace {

constexpr uint64_t Hash(const char* text) {
    uint64_t value = 14695981039346656037ull;
    while (*text) {
        value ^= static_cast<uint8_t>(*text++);
        value *= 1099511628211ull;
    }
    return value;
}

constexpr std::array<uint64_t, 15> kRequiredHumanBones{
    Hash("Hips"),
    Hash("Spine"),
    Hash("Head"),
    Hash("LeftUpperArm"), Hash("RightUpperArm"),
    Hash("LeftLowerArm"), Hash("RightLowerArm"),
    Hash("LeftHand"), Hash("RightHand"),
    Hash("LeftUpperLeg"), Hash("RightUpperLeg"),
    Hash("LeftLowerLeg"), Hash("RightLowerLeg"),
    Hash("LeftFoot"), Hash("RightFoot")
};

bool Finite(float value) {
    return std::isfinite(value);
}

bool ValidTransform(const AnityAvatarSkeletonBoneDesc& bone) {
    const bool valuesFinite =
        Finite(bone.positionX) && Finite(bone.positionY) && Finite(bone.positionZ) &&
        Finite(bone.rotationX) && Finite(bone.rotationY) && Finite(bone.rotationZ) && Finite(bone.rotationW) &&
        Finite(bone.scaleX) && Finite(bone.scaleY) && Finite(bone.scaleZ);
    if (!valuesFinite) return false;
    const float quaternionLengthSquared =
        bone.rotationX * bone.rotationX + bone.rotationY * bone.rotationY +
        bone.rotationZ * bone.rotationZ + bone.rotationW * bone.rotationW;
    constexpr float epsilon = 1e-12f;
    return quaternionLengthSquared > epsilon &&
           std::abs(bone.scaleX) > epsilon &&
           std::abs(bone.scaleY) > epsilon &&
           std::abs(bone.scaleZ) > epsilon;
}

void SetFirstError(AnityAvatarBuildResult& result, int32_t index) {
    if (result.errorIndex < 0) result.errorIndex = index;
}

void ValidateSkeleton(
    const AnityAvatarSkeletonBoneDesc* skeleton,
    int32_t skeletonCount,
    AnityAvatarBuildResult& result,
    std::unordered_map<uint64_t, int32_t>& indexByName) {
    if (skeletonCount == 0) {
        result.flags |= ANITY_AVATAR_EMPTY_SKELETON;
        return;
    }

    int32_t rootCount = 0;
    for (int32_t index = 0; index < skeletonCount; ++index) {
        const auto& bone = skeleton[index];
        if (bone.nameHash == 0) {
            result.flags |= ANITY_AVATAR_INVALID_SKELETON_NAME;
            SetFirstError(result, index);
        } else if (!indexByName.emplace(bone.nameHash, index).second) {
            result.flags |= ANITY_AVATAR_DUPLICATE_SKELETON_NAME;
            SetFirstError(result, index);
        }

        if (!ValidTransform(bone)) {
            result.flags |= ANITY_AVATAR_INVALID_TRANSFORM;
            SetFirstError(result, index);
        }

        if (bone.parentIndex == -1) {
            ++rootCount;
            result.rootIndex = index;
        } else if (bone.parentIndex < 0 || bone.parentIndex >= skeletonCount || bone.parentIndex == index) {
            result.flags |= ANITY_AVATAR_INVALID_PARENT;
            SetFirstError(result, index);
        }
    }

    if (rootCount != 1) {
        result.flags |= ANITY_AVATAR_MULTIPLE_ROOTS;
        if (rootCount == 0) result.rootIndex = -1;
    }

    std::vector<uint8_t> states(static_cast<size_t>(skeletonCount), 0);
    for (int32_t start = 0; start < skeletonCount; ++start) {
        int32_t current = start;
        std::vector<int32_t> path;
        while (current >= 0 && current < skeletonCount) {
            if (states[static_cast<size_t>(current)] == 2) break;
            if (states[static_cast<size_t>(current)] == 1) {
                result.flags |= ANITY_AVATAR_HIERARCHY_CYCLE;
                SetFirstError(result, current);
                break;
            }
            states[static_cast<size_t>(current)] = 1;
            path.push_back(current);
            current = skeleton[current].parentIndex;
        }
        for (int32_t index : path) states[static_cast<size_t>(index)] = 2;
    }
}

} // namespace

extern "C" {

AnityResult ANITY_CALL AnityAvatar_ValidateHuman(
    const AnityAvatarSkeletonBoneDesc* skeleton,
    int32_t skeletonCount,
    const AnityAvatarHumanBoneDesc* human,
    int32_t humanCount,
    AnityAvatarBuildResult* outResult) {
    if (!outResult || skeletonCount < 0 || humanCount < 0 ||
        (skeletonCount > 0 && !skeleton) || (humanCount > 0 && !human))
        return ANITY_ERR_INVALID_ARG;

    AnityAvatarBuildResult result{};
    result.rootIndex = -1;
    result.errorIndex = -1;
    std::unordered_map<uint64_t, int32_t> indexByName;
    ValidateSkeleton(skeleton, skeletonCount, result, indexByName);

    if (humanCount == 0) result.flags |= ANITY_AVATAR_EMPTY_HUMAN_MAPPING;
    std::unordered_set<uint64_t> boneNames;
    std::unordered_set<uint64_t> humanNames;
    for (int32_t index = 0; index < humanCount; ++index) {
        const auto& mapping = human[index];
        if (mapping.boneNameHash == 0 || mapping.humanNameHash == 0) {
            result.flags |= ANITY_AVATAR_INVALID_HUMAN_MAPPING;
            SetFirstError(result, index);
            continue;
        }
        if (!boneNames.insert(mapping.boneNameHash).second) {
            result.flags |= ANITY_AVATAR_DUPLICATE_HUMAN_BONE;
            SetFirstError(result, index);
        }
        if (!humanNames.insert(mapping.humanNameHash).second) {
            result.flags |= ANITY_AVATAR_DUPLICATE_HUMAN_NAME;
            SetFirstError(result, index);
        }
        if (indexByName.find(mapping.boneNameHash) == indexByName.end()) {
            result.flags |= ANITY_AVATAR_MISSING_MAPPED_BONE;
            SetFirstError(result, index);
        } else {
            ++result.mappedBoneCount;
        }
    }

    for (uint64_t required : kRequiredHumanBones) {
        if (humanNames.find(required) == humanNames.end()) {
            result.flags |= ANITY_AVATAR_MISSING_REQUIRED_HUMAN_BONE;
            break;
        }
    }

    *outResult = result;
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAvatar_ValidateGeneric(
    const AnityAvatarSkeletonBoneDesc* skeleton,
    int32_t skeletonCount,
    uint64_t rootMotionTransformNameHash,
    AnityAvatarBuildResult* outResult) {
    if (!outResult || skeletonCount < 0 || (skeletonCount > 0 && !skeleton))
        return ANITY_ERR_INVALID_ARG;

    AnityAvatarBuildResult result{};
    result.rootIndex = -1;
    result.errorIndex = -1;
    std::unordered_map<uint64_t, int32_t> indexByName;
    ValidateSkeleton(skeleton, skeletonCount, result, indexByName);

    if (rootMotionTransformNameHash != 0) {
        auto match = indexByName.find(rootMotionTransformNameHash);
        if (match == indexByName.end()) {
            result.flags |= ANITY_AVATAR_MISSING_ROOT_MOTION_TRANSFORM;
        } else {
            result.rootIndex = match->second;
        }
    }

    *outResult = result;
    return ANITY_OK;
}

} // extern "C"
