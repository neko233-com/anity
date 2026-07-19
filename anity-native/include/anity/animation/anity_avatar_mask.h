#pragma once
#include "../anity_core.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef void* AnityAvatarMaskHandle;

ANITY_API AnityResult ANITY_CALL AnityAvatarMask_Create(AnityAvatarMaskHandle* outMask);
ANITY_API void ANITY_CALL AnityAvatarMask_Destroy(AnityAvatarMaskHandle mask);

ANITY_API AnityResult ANITY_CALL AnityAvatarMask_GetHumanoidBodyPartActive(
    AnityAvatarMaskHandle mask, int32_t index, int32_t* outActive);
ANITY_API AnityResult ANITY_CALL AnityAvatarMask_SetHumanoidBodyPartActive(
    AnityAvatarMaskHandle mask, int32_t index, int32_t active);

ANITY_API AnityResult ANITY_CALL AnityAvatarMask_GetTransformCount(
    AnityAvatarMaskHandle mask, int32_t* outCount);
ANITY_API AnityResult ANITY_CALL AnityAvatarMask_SetTransformCount(
    AnityAvatarMaskHandle mask, int32_t count);
ANITY_API AnityResult ANITY_CALL AnityAvatarMask_GetTransformActive(
    AnityAvatarMaskHandle mask, int32_t index, int32_t* outActive);
ANITY_API AnityResult ANITY_CALL AnityAvatarMask_SetTransformActive(
    AnityAvatarMaskHandle mask, int32_t index, int32_t active);

/* outRequiredBytes includes the terminating NUL. Invalid indexes read as an empty path. */
ANITY_API AnityResult ANITY_CALL AnityAvatarMask_CopyTransformPath(
    AnityAvatarMaskHandle mask,
    int32_t index,
    char* buffer,
    int32_t bufferCapacity,
    int32_t* outRequiredBytes);
ANITY_API AnityResult ANITY_CALL AnityAvatarMask_SetTransformPath(
    AnityAvatarMaskHandle mask, int32_t index, const char* path);
ANITY_API AnityResult ANITY_CALL AnityAvatarMask_AddTransformPath(
    AnityAvatarMaskHandle mask, const char* path);
ANITY_API AnityResult ANITY_CALL AnityAvatarMask_RemoveTransformPath(
    AnityAvatarMaskHandle mask, const char* path, int32_t recursive);
/* Runtime generic-mask query: zero entries mean unmasked; duplicate active paths use OR semantics. */
ANITY_API AnityResult ANITY_CALL AnityAvatarMask_GetTransformPathActive(
    AnityAvatarMaskHandle mask, const char* path, int32_t* outActive);

#ifdef __cplusplus
}
#endif
