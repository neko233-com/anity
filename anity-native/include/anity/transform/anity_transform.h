#pragma once
#include "../anity_export.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct AnityTransformVector3 { float x, y, z; } AnityTransformVector3;
typedef struct AnityTransformQuaternion { float x, y, z, w; } AnityTransformQuaternion;
typedef struct AnityTransformMatrix4x4 {
    float m00, m01, m02, m03;
    float m10, m11, m12, m13;
    float m20, m21, m22, m23;
    float m30, m31, m32, m33;
} AnityTransformMatrix4x4;

/* Unity-style parent * local TRS composition. */
ANITY_API int32_t ANITY_CALL AnityTransform_ComposeLocalToWorld(
    const AnityTransformMatrix4x4* parentLocalToWorld,
    AnityTransformVector3 localPosition,
    AnityTransformQuaternion localRotation,
    AnityTransformVector3 localScale,
    AnityTransformMatrix4x4* outLocalToWorld);

/* Unity Transform inverse chain. Zero scale axes use a zero reciprocal. */
ANITY_API int32_t ANITY_CALL AnityTransform_ComposeWorldToLocal(
    const AnityTransformMatrix4x4* parentWorldToLocal,
    AnityTransformVector3 localPosition,
    AnityTransformQuaternion localRotation,
    AnityTransformVector3 localScale,
    AnityTransformMatrix4x4* outWorldToLocal);

/* Projects affine matrix columns onto the Transform world rotation axes. */
ANITY_API int32_t ANITY_CALL AnityTransform_ProjectLossyScale(
    const AnityTransformMatrix4x4* localToWorld,
    AnityTransformQuaternion worldRotation,
    AnityTransformVector3* outLossyScale);

#ifdef __cplusplus
}
#endif
