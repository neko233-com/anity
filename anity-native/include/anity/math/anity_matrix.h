#pragma once

#include "../anity_export.h"
#include "../transform/anity_transform.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef AnityTransformVector3 AnityMatrixVector3;
typedef AnityTransformQuaternion AnityMatrixQuaternion;
typedef AnityTransformMatrix4x4 AnityMatrix4x4;

typedef struct AnityMatrixFrustumPlanes {
    float left;
    float right;
    float bottom;
    float top;
    float zNear;
    float zFar;
} AnityMatrixFrustumPlanes;

ANITY_API float ANITY_CALL AnityMatrix_Determinant(const AnityMatrix4x4* matrix);
ANITY_API int32_t ANITY_CALL AnityMatrix_Inverse(
    const AnityMatrix4x4* matrix,
    AnityMatrix4x4* result);
ANITY_API int32_t ANITY_CALL AnityMatrix_Inverse3DAffine(
    const AnityMatrix4x4* matrix,
    AnityMatrix4x4* result);
ANITY_API int32_t ANITY_CALL AnityMatrix_Transpose(
    const AnityMatrix4x4* matrix,
    AnityMatrix4x4* result);
ANITY_API int32_t ANITY_CALL AnityMatrix_TRS(
    AnityMatrixVector3 position,
    AnityMatrixQuaternion rotation,
    AnityMatrixVector3 scale,
    AnityMatrix4x4* result);
ANITY_API int32_t ANITY_CALL AnityMatrix_Ortho(
    float left, float right, float bottom, float top, float zNear, float zFar,
    AnityMatrix4x4* result);
ANITY_API int32_t ANITY_CALL AnityMatrix_Perspective(
    float fov, float aspect, float zNear, float zFar,
    AnityMatrix4x4* result);
ANITY_API int32_t ANITY_CALL AnityMatrix_Frustum(
    float left, float right, float bottom, float top, float zNear, float zFar,
    AnityMatrix4x4* result);
ANITY_API int32_t ANITY_CALL AnityMatrix_LookAt(
    AnityMatrixVector3 from,
    AnityMatrixVector3 to,
    AnityMatrixVector3 up,
    AnityMatrix4x4* result);
ANITY_API int32_t ANITY_CALL AnityMatrix_ExtractRotation(
    const AnityMatrix4x4* matrix,
    AnityMatrixQuaternion* result);
ANITY_API int32_t ANITY_CALL AnityMatrix_ValidTRS(const AnityMatrix4x4* matrix);
ANITY_API int32_t ANITY_CALL AnityMatrix_DecomposeProjection(
    const AnityMatrix4x4* matrix,
    AnityMatrixFrustumPlanes* result);

#ifdef __cplusplus
}
#endif
