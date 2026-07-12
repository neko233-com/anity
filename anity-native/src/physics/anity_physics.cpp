#define ANITY_NATIVE_BUILD
#include "anity/physics/anity_physics.h"
#include <cmath>
#include <vector>
#include <limits>

extern "C" {

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
