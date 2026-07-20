#pragma once
#include "../anity_core.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct AnityHumanTraitMuscleInfo {
    const char* muscleName;
    const char* handleName;
    int32_t boneIndex;
    float defaultMin;
    float defaultMax;
    int32_t humanPartDof;
    int32_t dof;
} AnityHumanTraitMuscleInfo;

typedef struct AnityHumanTraitBoneInfo {
    const char* boneName;
    int32_t parentBoneIndex;
    int32_t required;
    float defaultHierarchyMass;
    int32_t muscleX;
    int32_t muscleY;
    int32_t muscleZ;
} AnityHumanTraitBoneInfo;

ANITY_API int32_t ANITY_CALL AnityHumanTrait_GetMuscleCount(void);
ANITY_API int32_t ANITY_CALL AnityHumanTrait_GetBoneCount(void);
ANITY_API int32_t ANITY_CALL AnityHumanTrait_GetRequiredBoneCount(void);
ANITY_API AnityResult ANITY_CALL AnityHumanTrait_GetMuscleInfo(
    int32_t muscleIndex, AnityHumanTraitMuscleInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityHumanTrait_GetBoneInfo(
    int32_t boneIndex, AnityHumanTraitBoneInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityHumanTrait_FindMuscleHandle(
    int32_t humanPartDof, int32_t dof, int32_t* outMuscleIndex);

#ifdef __cplusplus
}
#endif
