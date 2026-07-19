#define ANITY_NATIVE_BUILD
#include "anity/animation/anity_avatar_mask.h"

#include <algorithm>
#include <array>
#include <cstring>
#include <new>
#include <string>
#include <vector>

namespace {

constexpr int32_t kHumanoidBodyPartCount = 13;

struct TransformEntry {
    std::string path;
    bool active = false;
};

struct AvatarMaskState {
    std::array<bool, kHumanoidBodyPartCount> humanoidParts{};
    std::vector<TransformEntry> transforms;

    AvatarMaskState() {
        humanoidParts.fill(true);
    }
};

AvatarMaskState* State(AnityAvatarMaskHandle handle) {
    return static_cast<AvatarMaskState*>(handle);
}

bool ValidBodyPart(int32_t index) {
    return index >= 0 && index < kHumanoidBodyPartCount;
}

bool ValidTransformIndex(const AvatarMaskState& state, int32_t index) {
    return index >= 0 && index < static_cast<int32_t>(state.transforms.size());
}

bool MatchesPath(const std::string& candidate, const std::string& path, bool recursive) {
    if (candidate == path) return true;
    if (!recursive) return false;
    if (path.empty()) return true;
    return candidate.size() > path.size() &&
           candidate.compare(0, path.size(), path) == 0 &&
           candidate[path.size()] == '/';
}

} // namespace

extern "C" {

AnityResult ANITY_CALL AnityAvatarMask_Create(AnityAvatarMaskHandle* outMask) {
    if (!outMask) return ANITY_ERR_INVALID_ARG;
    auto* state = new (std::nothrow) AvatarMaskState();
    if (!state) return ANITY_ERR_OUT_OF_MEMORY;
    *outMask = state;
    return ANITY_OK;
}

void ANITY_CALL AnityAvatarMask_Destroy(AnityAvatarMaskHandle mask) {
    delete State(mask);
}

AnityResult ANITY_CALL AnityAvatarMask_GetHumanoidBodyPartActive(
    AnityAvatarMaskHandle mask, int32_t index, int32_t* outActive) {
    if (!mask || !outActive) return ANITY_ERR_INVALID_ARG;
    *outActive = ValidBodyPart(index) && State(mask)->humanoidParts[static_cast<size_t>(index)] ? 1 : 0;
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAvatarMask_SetHumanoidBodyPartActive(
    AnityAvatarMaskHandle mask, int32_t index, int32_t active) {
    if (!mask) return ANITY_ERR_INVALID_ARG;
    if (ValidBodyPart(index)) State(mask)->humanoidParts[static_cast<size_t>(index)] = active != 0;
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAvatarMask_GetTransformCount(
    AnityAvatarMaskHandle mask, int32_t* outCount) {
    if (!mask || !outCount) return ANITY_ERR_INVALID_ARG;
    *outCount = static_cast<int32_t>(State(mask)->transforms.size());
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAvatarMask_SetTransformCount(
    AnityAvatarMaskHandle mask, int32_t count) {
    if (!mask) return ANITY_ERR_INVALID_ARG;
    State(mask)->transforms.resize(static_cast<size_t>(std::max(0, count)));
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAvatarMask_GetTransformActive(
    AnityAvatarMaskHandle mask, int32_t index, int32_t* outActive) {
    if (!mask || !outActive) return ANITY_ERR_INVALID_ARG;
    const auto& state = *State(mask);
    *outActive = ValidTransformIndex(state, index) && state.transforms[static_cast<size_t>(index)].active ? 1 : 0;
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAvatarMask_SetTransformActive(
    AnityAvatarMaskHandle mask, int32_t index, int32_t active) {
    if (!mask) return ANITY_ERR_INVALID_ARG;
    auto& state = *State(mask);
    if (ValidTransformIndex(state, index)) state.transforms[static_cast<size_t>(index)].active = active != 0;
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAvatarMask_CopyTransformPath(
    AnityAvatarMaskHandle mask,
    int32_t index,
    char* buffer,
    int32_t bufferCapacity,
    int32_t* outRequiredBytes) {
    if (!mask || !outRequiredBytes || bufferCapacity < 0) return ANITY_ERR_INVALID_ARG;
    const auto& state = *State(mask);
    const std::string empty;
    const std::string& path = ValidTransformIndex(state, index)
        ? state.transforms[static_cast<size_t>(index)].path
        : empty;
    const int32_t required = static_cast<int32_t>(path.size()) + 1;
    *outRequiredBytes = required;
    if (!buffer || bufferCapacity == 0) return ANITY_OK;
    if (bufferCapacity < required) return ANITY_ERR_INVALID_ARG;
    std::memcpy(buffer, path.c_str(), static_cast<size_t>(required));
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAvatarMask_SetTransformPath(
    AnityAvatarMaskHandle mask, int32_t index, const char* path) {
    if (!mask) return ANITY_ERR_INVALID_ARG;
    auto& state = *State(mask);
    if (ValidTransformIndex(state, index))
        state.transforms[static_cast<size_t>(index)].path = path ? path : "";
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAvatarMask_AddTransformPath(
    AnityAvatarMaskHandle mask, const char* path) {
    if (!mask) return ANITY_ERR_INVALID_ARG;
    State(mask)->transforms.push_back({path ? path : "", true});
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityAvatarMask_RemoveTransformPath(
    AnityAvatarMaskHandle mask, const char* path, int32_t recursive) {
    if (!mask) return ANITY_ERR_INVALID_ARG;
    auto& transforms = State(mask)->transforms;
    const std::string target = path ? path : "";
    transforms.erase(
        std::remove_if(transforms.begin(), transforms.end(), [&](const TransformEntry& entry) {
            return MatchesPath(entry.path, target, recursive != 0);
        }),
        transforms.end());
    return ANITY_OK;
}

} // extern "C"
