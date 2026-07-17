#define ANITY_NATIVE_BUILD
#include "anity/physics/anity_physics.h"
#include <cmath>
#include <vector>
#include <limits>

extern "C" {

static AnityVec3 RotateVector(AnityVec3 value, AnityQuat rotation) {
  const float lengthSq = rotation.x * rotation.x + rotation.y * rotation.y
      + rotation.z * rotation.z + rotation.w * rotation.w;
  if (lengthSq <= 1e-20f) return value;

  const float inverseLength = 1.f / std::sqrt(lengthSq);
  const float qx = rotation.x * inverseLength;
  const float qy = rotation.y * inverseLength;
  const float qz = rotation.z * inverseLength;
  const float qw = rotation.w * inverseLength;

  const float tx = 2.f * (qy * value.z - qz * value.y);
  const float ty = 2.f * (qz * value.x - qx * value.z);
  const float tz = 2.f * (qx * value.y - qy * value.x);
  return {
      value.x + qw * tx + (qy * tz - qz * ty),
      value.y + qw * ty + (qz * tx - qx * tz),
      value.z + qw * tz + (qx * ty - qy * tx)};
}

int32_t ANITY_CALL AnityPhysics3D_ResolveConstantForce(
    AnityVec3 force, AnityVec3 relativeForce,
    AnityVec3 torque, AnityVec3 relativeTorque,
    AnityQuat rotation,
    AnityVec3* outForce, AnityVec3* outTorque) {
  if (!outForce || !outTorque) return 0;
  const AnityVec3 rotatedForce = RotateVector(relativeForce, rotation);
  const AnityVec3 rotatedTorque = RotateVector(relativeTorque, rotation);
  *outForce = {force.x + rotatedForce.x, force.y + rotatedForce.y, force.z + rotatedForce.z};
  *outTorque = {torque.x + rotatedTorque.x, torque.y + rotatedTorque.y, torque.z + rotatedTorque.z};
  return 1;
}

int32_t ANITY_CALL AnityPhysics2D_ResolveConstantForce(
    AnityVec2 force, AnityVec2 relativeForce,
    AnityQuat rotation, float torque,
    AnityVec2* outForce, float* outTorque) {
  if (!outForce || !outTorque) return 0;
  const AnityVec3 rotated = RotateVector({relativeForce.x, relativeForce.y, 0.f}, rotation);
  outForce->x = force.x + rotated.x;
  outForce->y = force.y + rotated.y;
  *outTorque = torque;
  return 1;
}

int32_t ANITY_CALL AnityPhysics3D_SphereSphereTOI(
    AnityVec3 posA, float radiusA, AnityVec3 velA,
    AnityVec3 posB, float radiusB,
    float deltaTime,
    float* outTOI, AnityVec3* outNormal, AnityVec3* outPoint) {
  if (!outTOI || !outNormal || !outPoint) return 0;

  float rpx = posA.x - posB.x, rpy = posA.y - posB.y, rpz = posA.z - posB.z;
  float r = radiusA + radiusB;
  float r2 = r * r;
  float c = rpx * rpx + rpy * rpy + rpz * rpz - r2;
  if (c < 0.f) {
    *outTOI = 0.f;
    float dist = std::sqrt(rpx * rpx + rpy * rpy + rpz * rpz);
    if (dist > 1e-6f) {
      outNormal->x = rpx / dist; outNormal->y = rpy / dist; outNormal->z = rpz / dist;
    } else {
      outNormal->x = 0; outNormal->y = 1; outNormal->z = 0;
    }
    outPoint->x = posB.x + outNormal->x * radiusB;
    outPoint->y = posB.y + outNormal->y * radiusB;
    outPoint->z = posB.z + outNormal->z * radiusB;
    return 1;
  }

  float a = velA.x * velA.x + velA.y * velA.y + velA.z * velA.z;
  if (a < 1e-12f) return 0;
  float b = rpx * velA.x + rpy * velA.y + rpz * velA.z;
  if (b >= 0.f) return 0;
  float discr = b * b - a * c;
  if (discr < 0.f) return 0;
  float t = (-b - std::sqrt(discr)) / a;
  if (t < 0.f || t > deltaTime) return 0;

  *outTOI = t;
  float hx = posA.x + velA.x * t - posB.x;
  float hy = posA.y + velA.y * t - posB.y;
  float hz = posA.z + velA.z * t - posB.z;
  float nlen = std::sqrt(hx * hx + hy * hy + hz * hz);
  if (nlen > 1e-6f) {
    outNormal->x = hx / nlen; outNormal->y = hy / nlen; outNormal->z = hz / nlen;
  } else {
    outNormal->x = 0; outNormal->y = 1; outNormal->z = 0;
  }
  outPoint->x = posB.x + outNormal->x * radiusB;
  outPoint->y = posB.y + outNormal->y * radiusB;
  outPoint->z = posB.z + outNormal->z * radiusB;
  return 1;
}

static void Project(const float* poly, int32_t count, float ax, float ay, float* minV, float* maxV) {
  *minV = std::numeric_limits<float>::infinity();
  *maxV = -std::numeric_limits<float>::infinity();
  for (int i = 0; i < count; ++i) {
    float d = poly[i * 2] * ax + poly[i * 2 + 1] * ay;
    if (d < *minV) *minV = d;
    if (d > *maxV) *maxV = d;
  }
}

int32_t ANITY_CALL AnityPhysics2D_PolygonSAT(
    const float* polyA, int32_t countA,
    const float* polyB, int32_t countB,
    float* outNx, float* outNy, float* outPenetration) {
  if (!polyA || !polyB || countA < 3 || countB < 3) return 0;
  float bestPen = std::numeric_limits<float>::infinity();
  float bestNx = 0.f, bestNy = 1.f;

  auto testEdges = [&](const float* poly, int32_t count) -> int32_t {
    for (int i = 0; i < count; ++i) {
      int j = (i + 1) % count;
      float ex = poly[j * 2] - poly[i * 2];
      float ey = poly[j * 2 + 1] - poly[i * 2 + 1];
      float ax = -ey, ay = ex;
      float len = std::sqrt(ax * ax + ay * ay);
      if (len < 1e-8f) continue;
      ax /= len; ay /= len;
      float minA, maxA, minB, maxB;
      Project(polyA, countA, ax, ay, &minA, &maxA);
      Project(polyB, countB, ax, ay, &minB, &maxB);
      if (maxA < minB || maxB < minA) return 0;
      float pen = (maxA < maxB ? maxA - minB : maxB - minA);
      if (pen < bestPen) {
        bestPen = pen;
        bestNx = ax; bestNy = ay;
      }
    }
    return 1;
  };

  if (!testEdges(polyA, countA)) return 0;
  if (!testEdges(polyB, countB)) return 0;
  if (outNx) *outNx = bestNx;
  if (outNy) *outNy = bestNy;
  if (outPenetration) *outPenetration = bestPen;
  return 1;
}

} // extern "C"
