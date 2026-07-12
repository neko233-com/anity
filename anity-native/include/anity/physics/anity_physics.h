#pragma once
#include "../anity_export.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct AnityVec3 { float x, y, z; } AnityVec3;
typedef struct AnityVec2 { float x, y; } AnityVec2;

/* 3D sphere-sphere continuous TOI (matches ContinuousCollision semantics) */
ANITY_API int32_t ANITY_CALL AnityPhysics3D_SphereSphereTOI(
    AnityVec3 posA, float radiusA, AnityVec3 velA,
    AnityVec3 posB, float radiusB,
    float deltaTime,
    float* outTOI, AnityVec3* outNormal, AnityVec3* outPoint);

/* 2D SAT polygon-polygon; points are interleaved x,y; returns 1 if overlapping */
ANITY_API int32_t ANITY_CALL AnityPhysics2D_PolygonSAT(
    const float* polyA, int32_t countA,
    const float* polyB, int32_t countB,
    float* outNx, float* outNy, float* outPenetration);

#ifdef __cplusplus
}
#endif
