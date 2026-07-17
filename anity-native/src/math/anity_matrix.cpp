#define ANITY_NATIVE_BUILD
#include "anity/math/anity_matrix.h"

#include <algorithm>
#include <cmath>

namespace {

using Matrix = AnityMatrix4x4;
using Vector3 = AnityMatrixVector3;
using Quaternion = AnityMatrixQuaternion;
using FrustumPlanes = AnityMatrixFrustumPlanes;

Matrix Zero() {
    return {};
}

Matrix Identity() {
    Matrix result{};
    result.m00 = result.m11 = result.m22 = result.m33 = 1.f;
    return result;
}

float Determinant(const Matrix& m) {
    const float a0 = m.m00 * m.m11 - m.m01 * m.m10;
    const float a1 = m.m00 * m.m12 - m.m02 * m.m10;
    const float a2 = m.m00 * m.m13 - m.m03 * m.m10;
    const float a3 = m.m01 * m.m12 - m.m02 * m.m11;
    const float a4 = m.m01 * m.m13 - m.m03 * m.m11;
    const float a5 = m.m02 * m.m13 - m.m03 * m.m12;
    const float b0 = m.m20 * m.m31 - m.m21 * m.m30;
    const float b1 = m.m20 * m.m32 - m.m22 * m.m30;
    const float b2 = m.m20 * m.m33 - m.m23 * m.m30;
    const float b3 = m.m21 * m.m32 - m.m22 * m.m31;
    const float b4 = m.m21 * m.m33 - m.m23 * m.m31;
    const float b5 = m.m22 * m.m33 - m.m23 * m.m32;
    return a0 * b5 - a1 * b4 + a2 * b3 + a3 * b2 - a4 * b1 + a5 * b0;
}

Matrix Adjugate(const Matrix& m) {
    Matrix a{};
    a.m00 = m.m11 * (m.m22 * m.m33 - m.m23 * m.m32) - m.m12 * (m.m21 * m.m33 - m.m23 * m.m31) + m.m13 * (m.m21 * m.m32 - m.m22 * m.m31);
    a.m01 = -(m.m01 * (m.m22 * m.m33 - m.m23 * m.m32) - m.m02 * (m.m21 * m.m33 - m.m23 * m.m31) + m.m03 * (m.m21 * m.m32 - m.m22 * m.m31));
    a.m02 = m.m01 * (m.m12 * m.m33 - m.m13 * m.m32) - m.m02 * (m.m11 * m.m33 - m.m13 * m.m31) + m.m03 * (m.m11 * m.m32 - m.m12 * m.m31);
    a.m03 = -(m.m01 * (m.m12 * m.m23 - m.m13 * m.m22) - m.m02 * (m.m11 * m.m23 - m.m13 * m.m21) + m.m03 * (m.m11 * m.m22 - m.m12 * m.m21));
    a.m10 = -(m.m10 * (m.m22 * m.m33 - m.m23 * m.m32) - m.m12 * (m.m20 * m.m33 - m.m23 * m.m30) + m.m13 * (m.m20 * m.m32 - m.m22 * m.m30));
    a.m11 = m.m00 * (m.m22 * m.m33 - m.m23 * m.m32) - m.m02 * (m.m20 * m.m33 - m.m23 * m.m30) + m.m03 * (m.m20 * m.m32 - m.m22 * m.m30);
    a.m12 = -(m.m00 * (m.m12 * m.m33 - m.m13 * m.m32) - m.m02 * (m.m10 * m.m33 - m.m13 * m.m30) + m.m03 * (m.m10 * m.m32 - m.m12 * m.m30));
    a.m13 = m.m00 * (m.m12 * m.m23 - m.m13 * m.m22) - m.m02 * (m.m10 * m.m23 - m.m13 * m.m20) + m.m03 * (m.m10 * m.m22 - m.m12 * m.m20);
    a.m20 = m.m10 * (m.m21 * m.m33 - m.m23 * m.m31) - m.m11 * (m.m20 * m.m33 - m.m23 * m.m30) + m.m13 * (m.m20 * m.m31 - m.m21 * m.m30);
    a.m21 = -(m.m00 * (m.m21 * m.m33 - m.m23 * m.m31) - m.m01 * (m.m20 * m.m33 - m.m23 * m.m30) + m.m03 * (m.m20 * m.m31 - m.m21 * m.m30));
    a.m22 = m.m00 * (m.m11 * m.m33 - m.m13 * m.m31) - m.m01 * (m.m10 * m.m33 - m.m13 * m.m30) + m.m03 * (m.m10 * m.m31 - m.m11 * m.m30);
    a.m23 = -(m.m00 * (m.m11 * m.m23 - m.m13 * m.m21) - m.m01 * (m.m10 * m.m23 - m.m13 * m.m20) + m.m03 * (m.m10 * m.m21 - m.m11 * m.m20));
    a.m30 = -(m.m10 * (m.m21 * m.m32 - m.m22 * m.m31) - m.m11 * (m.m20 * m.m32 - m.m22 * m.m30) + m.m12 * (m.m20 * m.m31 - m.m21 * m.m30));
    a.m31 = m.m00 * (m.m21 * m.m32 - m.m22 * m.m31) - m.m01 * (m.m20 * m.m32 - m.m22 * m.m30) + m.m02 * (m.m20 * m.m31 - m.m21 * m.m30);
    a.m32 = -(m.m00 * (m.m11 * m.m32 - m.m12 * m.m31) - m.m01 * (m.m10 * m.m32 - m.m12 * m.m30) + m.m02 * (m.m10 * m.m31 - m.m11 * m.m30));
    a.m33 = m.m00 * (m.m11 * m.m22 - m.m12 * m.m21) - m.m01 * (m.m10 * m.m22 - m.m12 * m.m20) + m.m02 * (m.m10 * m.m21 - m.m11 * m.m20);
    return a;
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

Vector3 Subtract(Vector3 left, Vector3 right) {
    return {left.x - right.x, left.y - right.y, left.z - right.z};
}

float Dot(Vector3 left, Vector3 right) {
    return left.x * right.x + left.y * right.y + left.z * right.z;
}

Vector3 Cross(Vector3 left, Vector3 right) {
    return {
        left.y * right.z - left.z * right.y,
        left.z * right.x - left.x * right.z,
        left.x * right.y - left.y * right.x};
}

Vector3 Normalize(Vector3 value) {
    const float magnitude = std::sqrt(Dot(value, value));
    if (magnitude <= 1e-5f) return {};
    const float reciprocal = 1.f / magnitude;
    return {value.x * reciprocal, value.y * reciprocal, value.z * reciprocal};
}

Quaternion Normalize(Quaternion value) {
    const float magnitude = std::sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
    if (magnitude <= 1e-5f) return {0.f, 0.f, 0.f, 1.f};
    const float reciprocal = 1.f / magnitude;
    return {value.x * reciprocal, value.y * reciprocal, value.z * reciprocal, value.w * reciprocal};
}

Quaternion ClosestRotation(const Matrix& m) {
    if (m.m00 == 0.f && m.m01 == 0.f && m.m02 == 0.f &&
        m.m10 == 0.f && m.m11 == 0.f && m.m12 == 0.f &&
        m.m20 == 0.f && m.m21 == 0.f && m.m22 == 0.f)
        return Normalize({-0.1592f, -0.3844f, -0.3844f, 0.8241f});

    float k[4][4]{};
    const float trace = m.m00 + m.m11 + m.m22;
    k[0][0] = 2.f * m.m00 - trace;
    k[1][1] = 2.f * m.m11 - trace;
    k[2][2] = 2.f * m.m22 - trace;
    k[3][3] = trace;
    k[0][1] = k[1][0] = m.m01 + m.m10;
    k[0][2] = k[2][0] = m.m02 + m.m20;
    k[1][2] = k[2][1] = m.m12 + m.m21;
    k[0][3] = k[3][0] = m.m21 - m.m12;
    k[1][3] = k[3][1] = m.m02 - m.m20;
    k[2][3] = k[3][2] = m.m10 - m.m01;

    float vectors[4][4]{};
    for (int index = 0; index < 4; ++index) vectors[index][index] = 1.f;
    for (int sweep = 0; sweep < 32; ++sweep) {
        int p = 0;
        int q = 1;
        float largest = std::fabs(k[p][q]);
        for (int row = 0; row < 4; ++row) {
            for (int column = row + 1; column < 4; ++column) {
                const float candidate = std::fabs(k[row][column]);
                if (candidate > largest) {
                    largest = candidate;
                    p = row;
                    q = column;
                }
            }
        }
        if (largest <= 1e-7f) break;

        const float angle = 0.5f * std::atan2(2.f * k[p][q], k[q][q] - k[p][p]);
        const float sine = std::sin(angle);
        const float cosine = std::cos(angle);
        for (int index = 0; index < 4; ++index) {
            if (index == p || index == q) continue;
            const float kip = k[index][p];
            const float kiq = k[index][q];
            k[index][p] = k[p][index] = cosine * kip - sine * kiq;
            k[index][q] = k[q][index] = sine * kip + cosine * kiq;
        }
        const float app = k[p][p];
        const float aqq = k[q][q];
        const float apq = k[p][q];
        k[p][p] = cosine * cosine * app - 2.f * sine * cosine * apq + sine * sine * aqq;
        k[q][q] = sine * sine * app + 2.f * sine * cosine * apq + cosine * cosine * aqq;
        k[p][q] = k[q][p] = 0.f;

        for (int row = 0; row < 4; ++row) {
            const float vip = vectors[row][p];
            const float viq = vectors[row][q];
            vectors[row][p] = cosine * vip - sine * viq;
            vectors[row][q] = sine * vip + cosine * viq;
        }
    }

    int largestEigenvalue = 0;
    for (int index = 1; index < 4; ++index)
        if (k[index][index] > k[largestEigenvalue][largestEigenvalue]) largestEigenvalue = index;
    Quaternion result = Normalize({
        vectors[0][largestEigenvalue], vectors[1][largestEigenvalue],
        vectors[2][largestEigenvalue], vectors[3][largestEigenvalue]});
    const float components[4]{result.x, result.y, result.z, result.w};
    int canonical = 0;
    for (int index = 1; index < 4; ++index)
        if (std::fabs(components[index]) > std::fabs(components[canonical])) canonical = index;
    if (components[canonical] < 0.f)
        result = {-result.x, -result.y, -result.z, -result.w};
    return result;
}

} // namespace

extern "C" {

float ANITY_CALL AnityMatrix_Determinant(const Matrix* matrix) {
    return matrix ? Determinant(*matrix) : 0.f;
}

int32_t ANITY_CALL AnityMatrix_Inverse(const Matrix* matrix, Matrix* result) {
    if (!matrix || !result) return 0;
    const float determinant = Determinant(*matrix);
    if (determinant == 0.f || std::isnan(determinant)) {
        *result = Zero();
        return 0;
    }
    *result = Adjugate(*matrix);
    const float reciprocal = 1.f / determinant;
    float* values = &result->m00;
    for (int index = 0; index < 16; ++index) values[index] *= reciprocal;
    return 1;
}

int32_t ANITY_CALL AnityMatrix_Inverse3DAffine(const Matrix* matrix, Matrix* result) {
    if (!matrix || !result) return 0;
    const float determinant = matrix->m00 * (matrix->m11 * matrix->m22 - matrix->m12 * matrix->m21)
                            - matrix->m01 * (matrix->m10 * matrix->m22 - matrix->m12 * matrix->m20)
                            + matrix->m02 * (matrix->m10 * matrix->m21 - matrix->m11 * matrix->m20);
    if (determinant == 0.f || std::isnan(determinant)) {
        *result = Zero();
        return 0;
    }
    const float reciprocal = 1.f / determinant;
    Matrix inverse = Identity();
    inverse.m00 = (matrix->m11 * matrix->m22 - matrix->m12 * matrix->m21) * reciprocal;
    inverse.m01 = (matrix->m02 * matrix->m21 - matrix->m01 * matrix->m22) * reciprocal;
    inverse.m02 = (matrix->m01 * matrix->m12 - matrix->m02 * matrix->m11) * reciprocal;
    inverse.m10 = (matrix->m12 * matrix->m20 - matrix->m10 * matrix->m22) * reciprocal;
    inverse.m11 = (matrix->m00 * matrix->m22 - matrix->m02 * matrix->m20) * reciprocal;
    inverse.m12 = (matrix->m02 * matrix->m10 - matrix->m00 * matrix->m12) * reciprocal;
    inverse.m20 = (matrix->m10 * matrix->m21 - matrix->m11 * matrix->m20) * reciprocal;
    inverse.m21 = (matrix->m01 * matrix->m20 - matrix->m00 * matrix->m21) * reciprocal;
    inverse.m22 = (matrix->m00 * matrix->m11 - matrix->m01 * matrix->m10) * reciprocal;
    inverse.m03 = -(inverse.m00 * matrix->m03 + inverse.m01 * matrix->m13 + inverse.m02 * matrix->m23);
    inverse.m13 = -(inverse.m10 * matrix->m03 + inverse.m11 * matrix->m13 + inverse.m12 * matrix->m23);
    inverse.m23 = -(inverse.m20 * matrix->m03 + inverse.m21 * matrix->m13 + inverse.m22 * matrix->m23);
    *result = inverse;
    return 1;
}

int32_t ANITY_CALL AnityMatrix_Transpose(const Matrix* matrix, Matrix* result) {
    if (!matrix || !result) return 0;
    result->m00 = matrix->m00; result->m01 = matrix->m10; result->m02 = matrix->m20; result->m03 = matrix->m30;
    result->m10 = matrix->m01; result->m11 = matrix->m11; result->m12 = matrix->m21; result->m13 = matrix->m31;
    result->m20 = matrix->m02; result->m21 = matrix->m12; result->m22 = matrix->m22; result->m23 = matrix->m32;
    result->m30 = matrix->m03; result->m31 = matrix->m13; result->m32 = matrix->m23; result->m33 = matrix->m33;
    return 1;
}

int32_t ANITY_CALL AnityMatrix_TRS(Vector3 position, Quaternion rotation, Vector3 scale, Matrix* result) {
    if (!result) return 0;
    *result = Rotation(rotation);
    result->m00 *= scale.x; result->m10 *= scale.x; result->m20 *= scale.x;
    result->m01 *= scale.y; result->m11 *= scale.y; result->m21 *= scale.y;
    result->m02 *= scale.z; result->m12 *= scale.z; result->m22 *= scale.z;
    result->m03 = position.x; result->m13 = position.y; result->m23 = position.z;
    return 1;
}

int32_t ANITY_CALL AnityMatrix_Ortho(float left, float right, float bottom, float top, float zNear, float zFar, Matrix* result) {
    if (!result) return 0;
    *result = Identity();
    result->m00 = 2.f / (right - left);
    result->m11 = 2.f / (top - bottom);
    result->m22 = -2.f / (zFar - zNear);
    result->m03 = -(right + left) / (right - left);
    result->m13 = -(top + bottom) / (top - bottom);
    result->m23 = -(zFar + zNear) / (zFar - zNear);
    return 1;
}

int32_t ANITY_CALL AnityMatrix_Perspective(float fov, float aspect, float zNear, float zFar, Matrix* result) {
    if (!result) return 0;
    const float tangent = std::tan(fov * 3.14159265358979323846f / 360.f);
    *result = Zero();
    result->m00 = 1.f / (aspect * tangent);
    result->m11 = 1.f / tangent;
    result->m22 = -(zFar + zNear) / (zFar - zNear);
    result->m23 = -(2.f * zFar * zNear) / (zFar - zNear);
    result->m32 = -1.f;
    return 1;
}

int32_t ANITY_CALL AnityMatrix_Frustum(float left, float right, float bottom, float top, float zNear, float zFar, Matrix* result) {
    if (!result) return 0;
    *result = Zero();
    result->m00 = 2.f * zNear / (right - left);
    result->m11 = 2.f * zNear / (top - bottom);
    result->m02 = (right + left) / (right - left);
    result->m12 = (top + bottom) / (top - bottom);
    result->m22 = -(zFar + zNear) / (zFar - zNear);
    result->m23 = -(2.f * zFar * zNear) / (zFar - zNear);
    result->m32 = -1.f;
    return 1;
}

int32_t ANITY_CALL AnityMatrix_LookAt(Vector3 from, Vector3 to, Vector3 up, Matrix* result) {
    if (!result) return 0;
    const Vector3 forward = Normalize(Subtract(to, from));
    if (Dot(forward, forward) <= 1e-10f) {
        *result = Identity();
        result->m03 = from.x; result->m13 = from.y; result->m23 = from.z;
        return 1;
    }
    Vector3 right = Normalize(Cross(up, forward));
    if (Dot(right, right) <= 1e-10f) {
        *result = Identity();
        result->m03 = from.x; result->m13 = from.y; result->m23 = from.z;
        return 1;
    }
    const Vector3 correctedUp = Normalize(Cross(forward, right));
    *result = Identity();
    result->m00 = right.x; result->m10 = right.y; result->m20 = right.z;
    result->m01 = correctedUp.x; result->m11 = correctedUp.y; result->m21 = correctedUp.z;
    result->m02 = forward.x; result->m12 = forward.y; result->m22 = forward.z;
    result->m03 = from.x; result->m13 = from.y; result->m23 = from.z;
    return 1;
}

int32_t ANITY_CALL AnityMatrix_ExtractRotation(const Matrix* matrix, Quaternion* result) {
    if (!matrix || !result) return 0;
    *result = ClosestRotation(*matrix);
    return 1;
}

int32_t ANITY_CALL AnityMatrix_ValidTRS(const Matrix* matrix) {
    return matrix && matrix->m30 == 0.f && matrix->m31 == 0.f && matrix->m32 == 0.f && matrix->m33 == 1.f;
}

int32_t ANITY_CALL AnityMatrix_DecomposeProjection(const Matrix* matrix, FrustumPlanes* result) {
    if (!matrix || !result) return 0;
    if (matrix->m33 == 0.f) {
        const float zNear = matrix->m23 / (matrix->m22 - 1.f);
        const float zFar = matrix->m23 / (matrix->m22 + 1.f);
        result->left = zNear * (matrix->m02 - 1.f) / matrix->m00;
        result->right = zNear * (matrix->m02 + 1.f) / matrix->m00;
        result->bottom = zNear * (matrix->m12 - 1.f) / matrix->m11;
        result->top = zNear * (matrix->m12 + 1.f) / matrix->m11;
        result->zNear = zNear;
        result->zFar = zFar;
    } else {
        result->left = (-1.f - matrix->m03) / matrix->m00;
        result->right = (1.f - matrix->m03) / matrix->m00;
        result->bottom = (-1.f - matrix->m13) / matrix->m11;
        result->top = (1.f - matrix->m13) / matrix->m11;
        result->zNear = (1.f + matrix->m23) / matrix->m22;
        result->zFar = (matrix->m23 - 1.f) / matrix->m22;
    }
    return 1;
}

} // extern "C"
