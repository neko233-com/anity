#pragma once
#include "../anity_export.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct AnityVec3 { float x, y, z; } AnityVec3;
typedef struct AnityVec2 { float x, y; } AnityVec2;
typedef struct AnityQuat { float x, y, z, w; } AnityQuat;

/* Resolves Unity ConstantForce world/local vectors in the native physics layer. */
ANITY_API int32_t ANITY_CALL AnityPhysics3D_ResolveConstantForce(
    AnityVec3 force, AnityVec3 relativeForce,
    AnityVec3 torque, AnityVec3 relativeTorque,
    AnityQuat rotation,
    AnityVec3* outForce, AnityVec3* outTorque);

/* Resolves Unity ConstantForce2D world/local force and torque. */
ANITY_API int32_t ANITY_CALL AnityPhysics2D_ResolveConstantForce(
    AnityVec2 force, AnityVec2 relativeForce,
    AnityQuat rotation, float torque,
    AnityVec2* outForce, float* outTorque);

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
