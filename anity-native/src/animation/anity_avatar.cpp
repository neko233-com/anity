#define ANITY_NATIVE_BUILD
#include "anity/animation/anity_avatar.h"

#include <array>
#include <cmath>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace {

struct Vector3 {
    float x, y, z;
};

struct Quaternion {
    float x, y, z, w;
};

struct WorldTransform {
    Vector3 position{};
    Quaternion rotation{0.0f, 0.0f, 0.0f, 1.0f};
    Vector3 scale{1.0f, 1.0f, 1.0f};
};

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

struct HumanMass {
    uint64_t humanNameHash;
    float doubledWeight;
};

// Unity's Avatar T-pose humanScale is the Y coordinate of the fixed Mecanim
// body-mass distribution. Doubled weights keep the half-unit arm weights exact;
// the complete 18-bone distribution totals 660.
constexpr std::array<HumanMass, 18> kHumanMasses{{
    {Hash("Spine"), 42.0f}, {Hash("Chest"), 58.0f},
    {Hash("Neck"), 52.0f}, {Hash("Head"), 36.0f},
    {Hash("LeftShoulder"), 50.0f}, {Hash("RightShoulder"), 50.0f},
    {Hash("LeftUpperArm"), 13.0f}, {Hash("RightUpperArm"), 13.0f},
    {Hash("LeftLowerArm"), 11.0f}, {Hash("RightLowerArm"), 11.0f},
    {Hash("LeftHand"), 10.0f}, {Hash("RightHand"), 10.0f},
    {Hash("LeftUpperLeg"), 72.0f}, {Hash("RightUpperLeg"), 72.0f},
    {Hash("LeftLowerLeg"), 56.0f}, {Hash("RightLowerLeg"), 56.0f},
    {Hash("LeftFoot"), 24.0f}, {Hash("RightFoot"), 24.0f},
}};

bool Finite(float value) {
    return std::isfinite(value);
}

Quaternion Normalize(Quaternion value) {
    const float lengthSquared = value.x * value.x + value.y * value.y +
                                value.z * value.z + value.w * value.w;
    if (!Finite(lengthSquared) || lengthSquared <= 1.0e-12f)
        return {0.0f, 0.0f, 0.0f, 1.0f};
    const float inverseLength = 1.0f / std::sqrt(lengthSquared);
    return {value.x * inverseLength, value.y * inverseLength,
            value.z * inverseLength, value.w * inverseLength};
}

Quaternion Multiply(Quaternion left, Quaternion right) {
    return {
        left.w * right.x + left.x * right.w + left.y * right.z - left.z * right.y,
        left.w * right.y - left.x * right.z + left.y * right.w + left.z * right.x,
        left.w * right.z + left.x * right.y - left.y * right.x + left.z * right.w,
        left.w * right.w - left.x * right.x - left.y * right.y - left.z * right.z,
    };
}

Vector3 Rotate(Quaternion rotation, Vector3 value) {
    rotation = Normalize(rotation);
    const Quaternion conjugate{-rotation.x, -rotation.y, -rotation.z, rotation.w};
    const Quaternion vector{value.x, value.y, value.z, 0.0f};
    const Quaternion result = Multiply(Multiply(rotation, vector), conjugate);
    return {result.x, result.y, result.z};
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

bool ResolveWorldTransform(
    int32_t index,
    const AnityAvatarSkeletonBoneDesc* skeleton,
    int32_t skeletonCount,
    std::vector<uint8_t>& states,
    std::vector<WorldTransform>& world) {
    if (index < 0 || index >= skeletonCount) return false;
    if (states[static_cast<size_t>(index)] == 2) return true;
    if (states[static_cast<size_t>(index)] == 1) return false;
    states[static_cast<size_t>(index)] = 1;
    const auto& source = skeleton[index];
    WorldTransform value{
        {source.positionX, source.positionY, source.positionZ},
        Normalize({source.rotationX, source.rotationY, source.rotationZ, source.rotationW}),
        {source.scaleX, source.scaleY, source.scaleZ},
    };
    if (source.parentIndex >= 0) {
        if (!ResolveWorldTransform(source.parentIndex, skeleton, skeletonCount, states, world)) return false;
        const WorldTransform& parent = world[static_cast<size_t>(source.parentIndex)];
        const Vector3 localScaled{
            parent.scale.x * value.position.x,
            parent.scale.y * value.position.y,
            parent.scale.z * value.position.z,
        };
        const Vector3 offset = Rotate(parent.rotation, localScaled);
        value.position = {
            parent.position.x + offset.x,
            parent.position.y + offset.y,
            parent.position.z + offset.z,
        };
        value.rotation = Normalize(Multiply(parent.rotation, value.rotation));
        value.scale = {
            parent.scale.x * value.scale.x,
            parent.scale.y * value.scale.y,
            parent.scale.z * value.scale.z,
        };
    }
    world[static_cast<size_t>(index)] = value;
    states[static_cast<size_t>(index)] = 2;
    return true;
}

float CalculateHumanScale(
    const AnityAvatarSkeletonBoneDesc* skeleton,
    int32_t skeletonCount,
    const AnityAvatarHumanBoneDesc* human,
    int32_t humanCount,
    int32_t rootIndex,
    const std::unordered_map<uint64_t, int32_t>& indexByName) {
    if (!skeleton || skeletonCount <= 0 || !human || humanCount <= 0 ||
        rootIndex < 0 || rootIndex >= skeletonCount)
        return 1.0f;

    std::unordered_map<uint64_t, int32_t> skeletonByHumanName;
    for (int32_t index = 0; index < humanCount; ++index) {
        const auto match = indexByName.find(human[index].boneNameHash);
        if (match != indexByName.end())
            skeletonByHumanName.emplace(human[index].humanNameHash, match->second);
    }

    std::vector<uint8_t> states(static_cast<size_t>(skeletonCount), 0);
    std::vector<WorldTransform> world(static_cast<size_t>(skeletonCount));
    for (int32_t index = 0; index < skeletonCount; ++index)
        if (!ResolveWorldTransform(index, skeleton, skeletonCount, states, world)) return 1.0f;

    float weightedHeight = 0.0f;
    float totalWeight = 0.0f;
    for (const HumanMass& mass : kHumanMasses) {
        const auto match = skeletonByHumanName.find(mass.humanNameHash);
        if (match == skeletonByHumanName.end()) continue;
        weightedHeight += world[static_cast<size_t>(match->second)].position.y * mass.doubledWeight;
        totalWeight += mass.doubledWeight;
    }
    if (totalWeight <= 0.0f) return 1.0f;
    const float rootHeight = world[static_cast<size_t>(rootIndex)].position.y;
    const float scale = std::abs(weightedHeight / totalWeight - rootHeight);
    return Finite(scale) && scale > 1.0e-8f ? scale : 1.0f;
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
    result.humanScale = 1.0f;
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

    if (result.flags == ANITY_AVATAR_VALID)
        result.humanScale = CalculateHumanScale(
            skeleton, skeletonCount, human, humanCount, result.rootIndex, indexByName);

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
    result.humanScale = 1.0f;
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
