#define ANITY_NATIVE_BUILD
#include "anity/transform/anity_transform.h"
#include <cmath>

namespace {

using Matrix = AnityTransformMatrix4x4;
using Vector3 = AnityTransformVector3;
using Quaternion = AnityTransformQuaternion;

Matrix Identity() {
    Matrix result{};
    result.m00 = result.m11 = result.m22 = result.m33 = 1.f;
    return result;
}

Matrix Multiply(const Matrix& lhs, const Matrix& rhs) {
    const float* a = &lhs.m00;
    const float* b = &rhs.m00;
    Matrix result{};
    float* output = &result.m00;
    for (int row = 0; row < 4; ++row) {
        for (int column = 0; column < 4; ++column) {
            float sum = 0.f;
            for (int index = 0; index < 4; ++index)
                sum += a[row * 4 + index] * b[index * 4 + column];
            output[row * 4 + column] = sum;
        }
    }
    return result;
}

Matrix Rotation(Quaternion q) {
    const float x2 = q.x + q.x;
    const float y2 = q.y + q.y;
    const float z2 = q.z + q.z;
    const float xx = q.x * x2;
    const float xy = q.x * y2;
    const float xz = q.x * z2;
    const float yy = q.y * y2;
    const float yz = q.y * z2;
    const float zz = q.z * z2;
    const float wx = q.w * x2;
    const float wy = q.w * y2;
    const float wz = q.w * z2;
    Matrix result = Identity();
    result.m00 = 1.f - (yy + zz); result.m01 = xy - wz;         result.m02 = xz + wy;
    result.m10 = xy + wz;         result.m11 = 1.f - (xx + zz); result.m12 = yz - wx;
    result.m20 = xz - wy;         result.m21 = yz + wx;         result.m22 = 1.f - (xx + yy);
    return result;
}

Quaternion Inverse(Quaternion q) {
    const float norm = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
    if (norm < 1e-6f) return {0.f, 0.f, 0.f, 1.f};
    const float reciprocal = 1.f / norm;
    return {-q.x * reciprocal, -q.y * reciprocal, -q.z * reciprocal, q.w * reciprocal};
}

Matrix Trs(Vector3 position, Quaternion rotation, Vector3 scale) {
    Matrix result = Rotation(rotation);
    result.m00 *= scale.x; result.m10 *= scale.x; result.m20 *= scale.x;
    result.m01 *= scale.y; result.m11 *= scale.y; result.m21 *= scale.y;
    result.m02 *= scale.z; result.m12 *= scale.z; result.m22 *= scale.z;
    result.m03 = position.x;
    result.m13 = position.y;
    result.m23 = position.z;
    return result;
}

Matrix InverseTrs(Vector3 position, Quaternion rotation, Vector3 scale) {
    const Vector3 reciprocal{
        scale.x == 0.f ? 0.f : 1.f / scale.x,
        scale.y == 0.f ? 0.f : 1.f / scale.y,
        scale.z == 0.f ? 0.f : 1.f / scale.z};
    Matrix inverseScale = Identity();
    inverseScale.m00 = reciprocal.x;
    inverseScale.m11 = reciprocal.y;
    inverseScale.m22 = reciprocal.z;
    Matrix inverseRotation = Rotation(Inverse(rotation));
    Matrix inverseTranslation = Identity();
    inverseTranslation.m03 = -position.x;
    inverseTranslation.m13 = -position.y;
    inverseTranslation.m23 = -position.z;
    return Multiply(Multiply(inverseScale, inverseRotation), inverseTranslation);
}

float Dot(float ax, float ay, float az, float bx, float by, float bz) {
    return ax * bx + ay * by + az * bz;
}

} // namespace

extern "C" {

int32_t ANITY_CALL AnityTransform_ComposeLocalToWorld(
    const Matrix* parentLocalToWorld,
    Vector3 localPosition,
    Quaternion localRotation,
    Vector3 localScale,
    Matrix* outLocalToWorld) {
    if (!outLocalToWorld) return 0;
    const Matrix local = Trs(localPosition, localRotation, localScale);
    *outLocalToWorld = parentLocalToWorld ? Multiply(*parentLocalToWorld, local) : local;
    return 1;
}

int32_t ANITY_CALL AnityTransform_ComposeWorldToLocal(
    const Matrix* parentWorldToLocal,
    Vector3 localPosition,
    Quaternion localRotation,
    Vector3 localScale,
    Matrix* outWorldToLocal) {
    if (!outWorldToLocal) return 0;
    const Matrix localInverse = InverseTrs(localPosition, localRotation, localScale);
    *outWorldToLocal = parentWorldToLocal ? Multiply(localInverse, *parentWorldToLocal) : localInverse;
    return 1;
}

int32_t ANITY_CALL AnityTransform_ProjectLossyScale(
    const Matrix* localToWorld,
    Quaternion worldRotation,
    Vector3* outLossyScale) {
    if (!localToWorld || !outLossyScale) return 0;
    const Matrix axes = Rotation(worldRotation);
    outLossyScale->x = Dot(localToWorld->m00, localToWorld->m10, localToWorld->m20,
                           axes.m00, axes.m10, axes.m20);
    outLossyScale->y = Dot(localToWorld->m01, localToWorld->m11, localToWorld->m21,
                           axes.m01, axes.m11, axes.m21);
    outLossyScale->z = Dot(localToWorld->m02, localToWorld->m12, localToWorld->m22,
                           axes.m02, axes.m12, axes.m22);
    return 1;
}

} // extern "C"
