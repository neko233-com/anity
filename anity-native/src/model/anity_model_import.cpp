#define ANITY_NATIVE_BUILD
#include "anity/model/anity_model_import.h"
#include "ufbx.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <memory>
#include <new>
#include <string>
#include <unordered_map>
#include <vector>

namespace {

struct Node {
  std::string name;
  int32_t parentIndex = -1;
  int32_t meshIndex = -1;
  ufbx_transform transform{};
};

struct SubMesh {
  int32_t indexStart = 0;
  int32_t indexCount = 0;
  int32_t materialIndex = -1;
};

struct Bone {
  std::string name;
  int32_t nodeIndex = -1;
  ufbx_matrix bindpose{};
};

struct BlendFrame {
  float weight = 100.0f;
  std::vector<AnityModelBlendShapeDelta> deltas;
};

struct BlendShape {
  std::string name;
  std::vector<BlendFrame> frames;
};

struct Mesh {
  std::string name;
  std::vector<AnityModelVertex> vertices;
  std::vector<uint32_t> indices;
  std::vector<SubMesh> subMeshes;
  std::vector<Bone> bones;
  std::vector<AnityModelSkinVertexInfo> skinVertices;
  std::vector<AnityModelBoneWeight> boneWeights;
  std::vector<BlendShape> blendShapes;
};

struct Track {
  int32_t nodeIndex = -1;
  std::vector<AnityModelVectorKey> positionKeys;
  std::vector<AnityModelQuaternionKey> rotationKeys;
  std::vector<AnityModelVectorKey> scaleKeys;
  struct TransformCurve {
    AnityModelTransformCurveProperty property = ANITY_MODEL_POSITION_X;
    std::vector<AnityModelScalarKey> keys;
  };
  std::vector<TransformCurve> transformCurves;
};

struct BlendShapeTrack {
  int32_t nodeIndex = -1;
  std::string name;
  std::vector<AnityModelScalarKey> keys;
};

struct Clip {
  std::string name;
  float duration = 0.0f;
  float frameRate = 30.0f;
  float firstFrame = 0.0f;
  float lastFrame = 0.0f;
  std::vector<Track> tracks;
  std::vector<BlendShapeTrack> blendShapeTracks;
};

static float Real(float value) { return std::isfinite(value) ? value : 0.0f; }
static float Real(double value) {
  return std::isfinite(value) && value <= std::numeric_limits<float>::max() && value >= -std::numeric_limits<float>::max()
    ? static_cast<float>(value) : 0.0f;
}

static std::string String(ufbx_string value) {
  return value.data && value.length ? std::string(value.data, value.length) : std::string();
}

static void CopyError(char* destination, int32_t size, const char* message) {
  if (!destination || size <= 0) return;
  const char* source = message ? message : "Model import failed.";
  std::strncpy(destination, source, static_cast<size_t>(size - 1));
  destination[size - 1] = '\0';
}

static bool UsesUnityFbxXAxisBasis(const ufbx_node* node) {
  return node &&
    std::abs(std::abs(node->adjust_pre_rotation.x) - 1.0) < 1e-9 &&
    std::abs(node->adjust_pre_rotation.y) < 1e-9 &&
    std::abs(node->adjust_pre_rotation.z) < 1e-9 &&
    std::abs(node->adjust_pre_rotation.w) < 1e-9;
}

static AnityModelVertex ReadVertex(
    const ufbx_mesh* mesh, uint32_t index, float scale, bool flipYAxisAndZAxis) {
  AnityModelVertex vertex{};
  const ufbx_vec3 position = ufbx_get_vertex_vec3(&mesh->vertex_position, index);
  const ufbx_vec3 normal = mesh->vertex_normal.exists
    ? ufbx_get_vertex_vec3(&mesh->vertex_normal, index) : ufbx_vec3{0.0, 1.0, 0.0};
  const ufbx_vec2 uv = mesh->vertex_uv.exists
    ? ufbx_get_vertex_vec2(&mesh->vertex_uv, index) : ufbx_vec2{0.0, 0.0};
  vertex.positionX = Real(position.x * scale);
  vertex.positionY = Real((flipYAxisAndZAxis ? -position.y : position.y) * scale);
  vertex.positionZ = Real((flipYAxisAndZAxis ? -position.z : position.z) * scale);
  vertex.normalX = Real(normal.x);
  vertex.normalY = Real(flipYAxisAndZAxis ? -normal.y : normal.y);
  vertex.normalZ = Real(flipYAxisAndZAxis ? -normal.z : normal.z);
  vertex.uvX = Real(uv.x);
  vertex.uvY = Real(uv.y);
  if (mesh->vertex_tangent.exists) {
    const ufbx_vec3 tangent = ufbx_get_vertex_vec3(&mesh->vertex_tangent, index);
    vertex.tangentX = Real(tangent.x);
    vertex.tangentY = Real(flipYAxisAndZAxis ? -tangent.y : tangent.y);
    vertex.tangentZ = Real(flipYAxisAndZAxis ? -tangent.z : tangent.z);
    vertex.tangentW = 1.0f;
    if (mesh->vertex_bitangent.exists) {
      const ufbx_vec3 bitangent = ufbx_get_vertex_vec3(&mesh->vertex_bitangent, index);
      const double cx = normal.y * tangent.z - normal.z * tangent.y;
      const double cy = normal.z * tangent.x - normal.x * tangent.z;
      const double cz = normal.x * tangent.y - normal.y * tangent.x;
      vertex.tangentW = cx * bitangent.x + cy * bitangent.y + cz * bitangent.z < 0.0 ? -1.0f : 1.0f;
    }
  }
  return vertex;
}

static bool BuildMesh(const ufbx_mesh* source, float scale, int32_t maxBonesPerVertex, float minBoneWeight,
    bool importBlendShapes, const std::unordered_map<uint32_t, int32_t>& nodeMap,
    Mesh& destination, std::string& error) {
  destination.name = String(source->name);
  if (source->num_triangles > static_cast<size_t>(std::numeric_limits<int32_t>::max() / 3)) {
    error = "Model mesh index count exceeds the supported range.";
    return false;
  }

  std::vector<uint32_t> triangleIndices(std::max<size_t>(source->max_face_triangles * 3, 3));
  std::vector<AnityModelVertex> flatVertices;
  std::vector<uint32_t> flatLogicalVertices;
  flatVertices.reserve(source->num_triangles * 3);
  flatLogicalVertices.reserve(source->num_triangles * 3);

  bool flipYAxisAndZAxis = source->instances.count > 0;
  for (const ufbx_node* instance : source->instances)
    flipYAxisAndZAxis = flipYAxisAndZAxis && UsesUnityFbxXAxisBasis(instance);

  const size_t partCount = source->material_parts.count;
  for (size_t partIndex = 0; partIndex < partCount; ++partIndex) {
    const ufbx_mesh_part& part = source->material_parts.data[partIndex];
    const size_t begin = flatVertices.size();
    for (size_t faceIndex = 0; faceIndex < part.num_faces; ++faceIndex) {
      const uint32_t meshFaceIndex = part.face_indices.data[faceIndex];
      if (meshFaceIndex >= source->faces.count) continue;
      const size_t triangleCount = ufbx_triangulate_face(
        triangleIndices.data(), triangleIndices.size(), source, source->faces.data[meshFaceIndex]);
      for (size_t index = 0; index < triangleCount * 3; ++index) {
        flatVertices.push_back(ReadVertex(
          source, triangleIndices[index], scale, flipYAxisAndZAxis));
        flatLogicalVertices.push_back(triangleIndices[index] < source->vertex_indices.count
          ? source->vertex_indices.data[triangleIndices[index]] : UFBX_NO_INDEX);
      }
    }
    const size_t count = flatVertices.size() - begin;
    if (count > 0) destination.subMeshes.push_back({
      static_cast<int32_t>(begin), static_cast<int32_t>(count), static_cast<int32_t>(part.index) });
  }

  if (flatVertices.empty() && source->num_triangles > 0) {
    error = "Model mesh did not yield any triangulated faces.";
    return false;
  }

  destination.indices.resize(flatVertices.size());
  if (!flatVertices.empty()) {
    ufbx_vertex_stream streams[2]{};
    streams[0].data = flatVertices.data();
    streams[0].vertex_count = flatVertices.size();
    streams[0].vertex_size = sizeof(AnityModelVertex);
    streams[1].data = flatLogicalVertices.data();
    streams[1].vertex_count = flatLogicalVertices.size();
    streams[1].vertex_size = sizeof(uint32_t);
    ufbx_error indexError{};
    const size_t vertexCount = ufbx_generate_indices(
      streams, 2, destination.indices.data(), destination.indices.size(), nullptr, &indexError);
    if (indexError.type != UFBX_ERROR_NONE) {
      char buffer[512]{};
      ufbx_format_error(buffer, sizeof(buffer), &indexError);
      error = buffer;
      return false;
    }
    flatVertices.resize(vertexCount);
    flatLogicalVertices.resize(vertexCount);
  }
  destination.vertices = std::move(flatVertices);
  if (destination.subMeshes.empty()) destination.subMeshes.push_back({0, 0, -1});

  if (source->skin_deformers.count > 0) {
    const ufbx_skin_deformer* skin = source->skin_deformers.data[0];
    destination.bones.reserve(skin->clusters.count);
    for (size_t clusterIndex = 0; clusterIndex < skin->clusters.count; ++clusterIndex) {
      const ufbx_skin_cluster* cluster = skin->clusters.data[clusterIndex];
      Bone bone;
      if (cluster->bone_node) {
        bone.name = String(cluster->bone_node->name);
        const auto found = nodeMap.find(cluster->bone_node->typed_id);
        if (found != nodeMap.end()) bone.nodeIndex = found->second;
      }
      bone.bindpose = cluster->geometry_to_bone;
      bone.bindpose.m03 *= scale;
      bone.bindpose.m13 *= scale;
      bone.bindpose.m23 *= scale;
      destination.bones.push_back(std::move(bone));
    }

    destination.skinVertices.resize(destination.vertices.size());
    const int32_t influenceLimit = std::clamp(maxBonesPerVertex, 1, 8);
    for (size_t vertexIndex = 0; vertexIndex < flatLogicalVertices.size(); ++vertexIndex) {
      AnityModelSkinVertexInfo& outputVertex = destination.skinVertices[vertexIndex];
      outputVertex.weightStart = static_cast<int32_t>(destination.boneWeights.size());
      const uint32_t logicalVertex = flatLogicalVertices[vertexIndex];
      if (logicalVertex == UFBX_NO_INDEX || logicalVertex >= skin->vertices.count) continue;
      const ufbx_skin_vertex& skinVertex = skin->vertices.data[logicalVertex];
      const size_t count = std::min<size_t>(skinVertex.num_weights, static_cast<size_t>(influenceLimit));
      float total = 0.0f;
      for (size_t influence = 0; influence < count; ++influence) {
        const ufbx_skin_weight& sourceWeight = skin->weights.data[skinVertex.weight_begin + influence];
        const float weight = Real(sourceWeight.weight);
        if (weight < minBoneWeight || sourceWeight.cluster_index >= destination.bones.size()) continue;
        destination.boneWeights.push_back({static_cast<int32_t>(sourceWeight.cluster_index), weight});
        total += weight;
      }
      outputVertex.weightCount = static_cast<int32_t>(destination.boneWeights.size()) - outputVertex.weightStart;
      if (outputVertex.weightCount == 0 && !destination.bones.empty()) {
        destination.boneWeights.push_back({0, 1.0f});
        outputVertex.weightCount = 1;
        total = 1.0f;
      }
      if (total > 0.0f && std::abs(total - 1.0f) > 1e-6f) {
        for (int32_t influence = 0; influence < outputVertex.weightCount; ++influence)
          destination.boneWeights[outputVertex.weightStart + influence].weight /= total;
      }
    }
  }

  if (importBlendShapes) {
    for (size_t deformerIndex = 0; deformerIndex < source->blend_deformers.count; ++deformerIndex) {
      const ufbx_blend_deformer* deformer = source->blend_deformers.data[deformerIndex];
      for (size_t channelIndex = 0; channelIndex < deformer->channels.count; ++channelIndex) {
        const ufbx_blend_channel* channel = deformer->channels.data[channelIndex];
        BlendShape blend;
        blend.name = String(channel->name);
        for (size_t keyframeIndex = 0; keyframeIndex < channel->keyframes.count; ++keyframeIndex) {
          const ufbx_blend_keyframe& keyframe = channel->keyframes.data[keyframeIndex];
          if (!keyframe.shape) continue;
          BlendFrame frame;
          // ufbx normalizes FBX percent weights to 0..1 while Unity's Mesh API
          // exposes blend-shape frame weights in percent units.
          frame.weight = Real(keyframe.target_weight * 100.0);
          frame.deltas.resize(destination.vertices.size());
          std::vector<ufbx_vec3> positionOffsets(source->num_vertices);
          std::vector<ufbx_vec3> normalOffsets(source->num_vertices);
          for (size_t offsetIndex = 0; offsetIndex < keyframe.shape->offset_vertices.count; ++offsetIndex) {
            const uint32_t logicalVertex = keyframe.shape->offset_vertices.data[offsetIndex];
            if (logicalVertex >= source->num_vertices) continue;
            positionOffsets[logicalVertex] = keyframe.shape->position_offsets.data[offsetIndex];
            if (offsetIndex < keyframe.shape->normal_offsets.count)
              normalOffsets[logicalVertex] = keyframe.shape->normal_offsets.data[offsetIndex];
          }
          for (size_t vertexIndex = 0; vertexIndex < flatLogicalVertices.size(); ++vertexIndex) {
            const uint32_t logicalVertex = flatLogicalVertices[vertexIndex];
            if (logicalVertex == UFBX_NO_INDEX || logicalVertex >= source->num_vertices) continue;
            const ufbx_vec3& p = positionOffsets[logicalVertex];
            const ufbx_vec3& n = normalOffsets[logicalVertex];
            frame.deltas[vertexIndex] = {
              Real(p.x * scale),
              Real((flipYAxisAndZAxis ? -p.y : p.y) * scale),
              Real((flipYAxisAndZAxis ? -p.z : p.z) * scale),
              Real(n.x),
              Real(flipYAxisAndZAxis ? -n.y : n.y),
              Real(flipYAxisAndZAxis ? -n.z : n.z),
            };
          }
          blend.frames.push_back(std::move(frame));
        }
        if (!blend.frames.empty()) destination.blendShapes.push_back(std::move(blend));
      }
    }
  }
  return true;
}

static AnityModelVectorKey VectorKey(const ufbx_baked_vec3& source, float scale) {
  return { Real(source.time), Real(source.value.x * scale), Real(source.value.y * scale), Real(source.value.z * scale) };
}

static ufbx_quat RemoveRootAxisRotation(ufbx_quat rotation, const ufbx_node* node) {
  if (!node) return ufbx_quat_normalize(rotation);
  const ufbx_quat adjustment = node->adjust_pre_rotation;
  const ufbx_quat inverseAdjustment = {
    -adjustment.x, -adjustment.y, -adjustment.z, adjustment.w,
  };
  return ufbx_quat_normalize(ufbx_quat_mul(rotation, inverseAdjustment));
}

static AnityModelQuaternionKey QuaternionKey(const ufbx_baked_quat& source, const ufbx_node* node) {
  const ufbx_quat value = RemoveRootAxisRotation(source.value, node);
  return { Real(source.time), Real(value.x), Real(value.y), Real(value.z), Real(value.w) };
}

static bool HasBlendShape(const Mesh& mesh, ufbx_string name) {
  for (const BlendShape& shape : mesh.blendShapes) {
    if (shape.name.size() == name.length && std::memcmp(shape.name.data(), name.data, name.length) == 0) return true;
  }
  return false;
}

static bool SameString(ufbx_string left, ufbx_string right) {
  return left.length == right.length &&
    (left.length == 0 || std::memcmp(left.data, right.data, left.length) == 0);
}

static const ufbx_anim_curve* FindSingleRawCurve(
    const ufbx_anim_stack* stack, const ufbx_element* element, ufbx_string propertyName) {
  const ufbx_anim_curve* result = nullptr;
  for (size_t layerIndex = 0; layerIndex < stack->layers.count; ++layerIndex) {
    const ufbx_anim_layer* layer = stack->layers.data[layerIndex];
    for (size_t propIndex = 0; propIndex < layer->anim_props.count; ++propIndex) {
      const ufbx_anim_prop& property = layer->anim_props.data[propIndex];
      if (property.element != element || !SameString(property.prop_name, propertyName) ||
          !property.anim_value || !property.anim_value->curves[0]) continue;
      if (result && result != property.anim_value->curves[0]) return nullptr;
      result = property.anim_value->curves[0];
    }
  }
  return result;
}

static const ufbx_anim_value* FindSingleRawValue(
    const ufbx_anim_stack* stack, const ufbx_element* element, const char* propertyName) {
  const ufbx_anim_value* result = nullptr;
  for (size_t layerIndex = 0; layerIndex < stack->layers.count; ++layerIndex) {
    const ufbx_anim_prop* property = ufbx_find_anim_prop(
      stack->layers.data[layerIndex], element, propertyName);
    if (!property || !property->anim_value) continue;
    if (result && result != property->anim_value) return nullptr;
    result = property->anim_value;
  }
  return result;
}

static float EvaluateUnityFbxUnweightedCubic(
    float leftValue, float rightValue,
    float outHandle, float inHandle, double sourceT) {
  // Autodesk KFCurve::EvaluateIndex() has a specialized unweighted path. It
  // uses the tangent handles directly for the outer first-level segments and
  // constructs the middle difference in two float subtractions. A generic
  // De Casteljau loop loses this instruction ordering near float boundaries.
  volatile double scaledOut = sourceT * static_cast<double>(outHandle);
  const float a = static_cast<float>(
    scaledOut + static_cast<double>(leftValue));
  const float p1 = leftValue + outHandle;
  const float p2 = rightValue - inHandle;
  const float middleFromLeft = p2 - leftValue;
  const float middleDifference = middleFromLeft - outHandle;
  volatile double scaledMiddle =
    sourceT * static_cast<double>(middleDifference);
  const float b = static_cast<float>(
    scaledMiddle + static_cast<double>(p1));
  volatile double scaledIn = sourceT * static_cast<double>(inHandle);
  const float c = static_cast<float>(
    scaledIn + static_cast<double>(p2));

  const float secondLeftDifference = b - a;
  volatile double scaledSecondLeft =
    sourceT * static_cast<double>(secondLeftDifference);
  const float secondLeft = static_cast<float>(
    scaledSecondLeft + static_cast<double>(a));
  const float secondRightDifference = c - b;
  volatile double scaledSecondRight =
    sourceT * static_cast<double>(secondRightDifference);
  const float secondRight = static_cast<float>(
    scaledSecondRight + static_cast<double>(b));
  const float finalDifference = secondRight - secondLeft;
  volatile double scaledFinal =
    sourceT * static_cast<double>(finalDifference);
  return static_cast<float>(
    scaledFinal + static_cast<double>(secondLeft));
}

static double EvaluateUnityCompatibleCurve(
    const ufbx_anim_curve* curve, double time, double defaultValue) {
  if (!curve || curve->keyframes.count < 2 ||
      time < curve->keyframes.data[0].time ||
      time > curve->keyframes.data[curve->keyframes.count - 1].time)
    return ufbx_evaluate_curve(curve, time, defaultValue);

  const ufbx_keyframe* left = curve->keyframes.data;
  const ufbx_keyframe* right = left + 1;
  size_t leftIndex = 0;
  for (size_t index = 1; index < curve->keyframes.count; ++index) {
    right = curve->keyframes.data + index;
    left = right - 1;
    leftIndex = index - 1;
    if (time <= right->time) break;
  }
  if (time == left->time) return left->value;
  if (time == right->time) return right->value;
  if (left->interpolation != UFBX_INTERPOLATION_CUBIC)
    return ufbx_evaluate_curve(curve, time, defaultValue);

  // KFCurve derives both the segment duration and local parameter from
  // FbxTime's integer tick subtraction, not from the rounded seconds exposed
  // by ufbx. The two differ by a few double ULPs at ordinary frame ratios.
  constexpr double ticksPerSecond = 141120000.0;
  const int64_t leftTicks = static_cast<int64_t>(
    std::llround(left->time * ticksPerSecond));
  const int64_t rightTicks = static_cast<int64_t>(
    std::llround(right->time * ticksPerSecond));
  const int64_t timeTicks = static_cast<int64_t>(
    std::llround(time * ticksPerSecond));
  const int64_t durationTicks = rightTicks - leftTicks;
  const double duration = static_cast<double>(durationTicks) / ticksPerSecond;
  if (!(duration > 0.0)) return left->value;
  const double rightWeight = left->right.dx / duration;
  const double leftWeight = right->left.dx / duration;
  // Legacy FBX unweighted tangents serialize the decimal weight 0.333333.
  // Autodesk's evaluator treats these as exact one-third Hermite handles;
  // interpreting the rounded value as a weighted Bezier handle shifts Unity's
  // resampled rotations enough to change the strict rotation-error gate.
  if (std::abs(rightWeight - 0.333333) > 2e-6 ||
      std::abs(leftWeight - 0.333333) > 2e-6)
    return ufbx_evaluate_curve(curve, time, defaultValue);

  const double segmentRatio = static_cast<double>(timeTicks - leftTicks) /
    static_cast<double>(durationTicks);
  // KeyFind() returns keyIndex + ratio; EvaluateIndex() then subtracts the
  // integer index again. Preserve the intervening double rounding.
  const double curveIndex = static_cast<double>(leftIndex) + segmentRatio;
  const double sourceT = curveIndex - static_cast<double>(leftIndex);
  const float durationFloat = static_cast<float>(duration);
  const float outSlope = std::abs(left->right.dx) > 1e-12
    ? left->right.dy / left->right.dx : 0.0f;
  const float inSlope = std::abs(right->left.dx) > 1e-12
    ? right->left.dy / right->left.dx : 0.0f;
  const float outHandle = outSlope * durationFloat / 3.0f;
  const float inHandle = inSlope * durationFloat / 3.0f;
  const float p0 = Real(left->value);
  const float p3 = Real(right->value);
  return EvaluateUnityFbxUnweightedCubic(
    p0, p3, outHandle, inHandle, sourceT);
}

static void UnityFbxSinCos(double angle, double* sine, double* cosine) {
#if defined(__APPLE__)
  // Autodesk FBX SDK calls Darwin's joint sin/cos entry point. Calling the
  // functions separately changes range-reduction rounding for large angles.
  __sincos(angle, sine, cosine);
#else
  *sine = std::sin(angle);
  *cosine = std::cos(angle);
#endif
}

static ufbx_quat UnityFbxEulerToQuaternion(
    ufbx_vec3 euler, ufbx_rotation_order order,
    ufbx_vec3* xyzEquivalent = nullptr) {
  constexpr double degreesToRadians = 3.14159265358979323846264338327950288 / 180.0;
  const double rx = euler.x * degreesToRadians;
  const double ry = euler.y * degreesToRadians;
  const double rz = euler.z * degreesToRadians;
  double sx = 0.0, cx = 1.0;
  double sy = 0.0, cy = 1.0;
  double sz = 0.0, cz = 1.0;
  UnityFbxSinCos(rx, &sx, &cx);
  UnityFbxSinCos(ry, &sy, &cy);
  UnityFbxSinCos(rz, &sz, &cz);

  // FbxAMatrix::SetROnly() uses row-vector Euler matrices. Reconstructing its
  // matrix before GetQ() preserves the rounding Unity receives from FBX SDK;
  // the direct closed-form Euler quaternion differs by a few float ULPs.
  double m00, m01, m02, m10, m11, m12, m20, m21, m22;
  if (order == UFBX_ROTATION_ORDER_XYZ) {
    const double sinXSinY = sx * sy;
    const double cosXSinY = cx * sy;
    m00 = cy * cz;
    m01 = cy * sz;
    m02 = -sy;
    volatile double m10Left = cz * sinXSinY;
    volatile double m10Right = cx * sz;
    m10 = m10Left - m10Right;
    volatile double m11Left = cx * cz;
    volatile double m11Right = sz * sinXSinY;
    m11 = m11Left + m11Right;
    m12 = cy * sx;
    volatile double m20Left = sx * sz;
    volatile double m20Right = cz * cosXSinY;
    m20 = m20Left + m20Right;
    volatile double m21Left = sz * cosXSinY;
    volatile double m21Right = sx * cz;
    m21 = m21Left - m21Right;
    m22 = cy * cx;
  } else {
    const int orderIndex = static_cast<int>(order);
    if (orderIndex <= 0 || orderIndex >= 6)
      return ufbx_euler_to_quat(euler, order);
    static constexpr int axisOrders[][3] = {
      {0, 1, 2}, {0, 2, 1}, {1, 2, 0},
      {1, 0, 2}, {2, 0, 1}, {2, 1, 0},
    };
    const double angles[] = {euler.x, euler.y, euler.z};
    const int i = axisOrders[orderIndex][0];
    const int j = axisOrders[orderIndex][1];
    const int k = axisOrders[orderIndex][2];
    // FbxRotationOrder::V2M() reorders the angles through FbxEuler::AxisTable,
    // then FbxAMatrix::SetR() applies a negative degree conversion for odd
    // Euler parity (XZY/YXZ/ZYX). Keep that path and its arithmetic ordering:
    // direct multiplication of axis matrices is geometrically equivalent but
    // differs from Unity's FBX SDK result by several float ULPs.
    const double parityRadians = (orderIndex & 1) != 0
      ? -degreesToRadians : degreesToRadians;
    const double a = angles[i] * parityRadians;
    const double b = angles[j] * parityRadians;
    const double c = angles[k] * parityRadians;
    double sinA = 0.0, cosA = 1.0;
    double sinB = 0.0, cosB = 1.0;
    double sinC = 0.0, cosC = 1.0;
    UnityFbxSinCos(a, &sinA, &cosA);
    UnityFbxSinCos(b, &sinB, &cosB);
    UnityFbxSinCos(c, &sinC, &cosC);
    const double cosASinC = cosA * sinC;
    const double cosACosC = cosA * cosC;
    const double cosCSinA = cosC * sinA;
    const double sinCSinA = sinC * sinA;
    double matrix[3][3] = {
      {1.0, 0.0, 0.0}, {0.0, 1.0, 0.0}, {0.0, 0.0, 1.0},
    };
    matrix[i][i] = cosB * cosC;
    volatile double jiLeft = sinB * cosCSinA;
    matrix[j][i] = jiLeft - cosASinC;
    volatile double kiLeft = cosACosC * sinB;
    matrix[k][i] = kiLeft + sinCSinA;
    matrix[i][j] = cosB * sinC;
    volatile double jjLeft = sinB * sinCSinA;
    matrix[j][j] = jjLeft + cosACosC;
    volatile double kjLeft = sinB * cosASinC;
    matrix[k][j] = kjLeft - cosCSinA;
    matrix[i][k] = -sinB;
    matrix[j][k] = cosB * sinA;
    matrix[k][k] = cosA * cosB;
    m00 = matrix[0][0]; m01 = matrix[0][1]; m02 = matrix[0][2];
    m10 = matrix[1][0]; m11 = matrix[1][1]; m12 = matrix[1][2];
    m20 = matrix[2][0]; m21 = matrix[2][1]; m22 = matrix[2][2];
  }

  constexpr double radiansToDegrees = 180.0 /
    3.14159265358979323846264338327950288;
  // FbxRotationOrder::M2V(XYZ) and FbxAMatrix::GetQ() both pass through
  // GetROnly(). Its singular test is the length of matrix row 0's XY
  // projection against 2^-48; at gimbal lock it moves the coupled rotation
  // into X and emits Z as zero.
  constexpr double singularThreshold = 0x1p-48;
  volatile double projectionX = m00 * m00;
  volatile double projectionY = m01 * m01;
  const double projection = std::sqrt(projectionX + projectionY);
  const double canonicalX = projection > singularThreshold
    ? std::atan2(m12, m22) : std::atan2(-m21, m11);
  const double canonicalY = std::atan2(-m02, projection);
  const double canonicalZ = projection > singularThreshold
    ? std::atan2(m01, m00) : 0.0;
  if (xyzEquivalent) {
    const bool exactIdentityMatrix =
      m00 == 1.0 && m01 == 0.0 && m02 == 0.0 &&
      m10 == 0.0 && m11 == 1.0 && m12 == 0.0 &&
      m20 == 0.0 && m21 == 0.0 && m22 == 1.0;
    const auto storeFbxEuler = [exactIdentityMatrix](double value) {
      // M2V canonicalizes a bit-exact identity matrix to positive zero. Keep
      // the signed trigonometric residue of rotations that are only
      // mathematically equivalent to identity, such as the +/-180 tie.
      return exactIdentityMatrix ? 0.0f : static_cast<float>(value);
    };
    xyzEquivalent->x = storeFbxEuler(canonicalX * radiansToDegrees);
    xyzEquivalent->y = storeFbxEuler(canonicalY * radiansToDegrees);
    xyzEquivalent->z = storeFbxEuler(canonicalZ * radiansToDegrees);
  }

  // FbxAMatrix::GetQ() calls GetR(), rebuilds a pure XYZ rotation matrix from
  // that canonical Euler vector, and only then calls GetUnnormalizedQ().
  double canonicalSinX = 0.0, canonicalCosX = 1.0;
  double canonicalSinY = 0.0, canonicalCosY = 1.0;
  double canonicalSinZ = 0.0, canonicalCosZ = 1.0;
  UnityFbxSinCos(canonicalX, &canonicalSinX, &canonicalCosX);
  UnityFbxSinCos(canonicalY, &canonicalSinY, &canonicalCosY);
  UnityFbxSinCos(canonicalZ, &canonicalSinZ, &canonicalCosZ);
  const double canonicalSinXSinY = canonicalSinX * canonicalSinY;
  const double canonicalCosXSinY = canonicalCosX * canonicalSinY;
  m00 = canonicalCosY * canonicalCosZ;
  m01 = canonicalCosY * canonicalSinZ;
  m02 = -canonicalSinY;
  volatile double canonicalM10Left = canonicalCosZ * canonicalSinXSinY;
  volatile double canonicalM10Right = canonicalCosX * canonicalSinZ;
  m10 = canonicalM10Left - canonicalM10Right;
  volatile double canonicalM11Left = canonicalCosX * canonicalCosZ;
  volatile double canonicalM11Right = canonicalSinZ * canonicalSinXSinY;
  m11 = canonicalM11Left + canonicalM11Right;
  m12 = canonicalCosY * canonicalSinX;
  volatile double canonicalM20Left = canonicalSinX * canonicalSinZ;
  volatile double canonicalM20Right = canonicalCosZ * canonicalCosXSinY;
  m20 = canonicalM20Left + canonicalM20Right;
  volatile double canonicalM21Left = canonicalSinZ * canonicalCosXSinY;
  volatile double canonicalM21Right = canonicalSinX * canonicalCosZ;
  m21 = canonicalM21Left - canonicalM21Right;
  m22 = canonicalCosY * canonicalCosX;

  ufbx_quat result{};
  const double trace = m00 + m11 + m22;
  if (trace > 0.0) {
    const double root = std::sqrt(trace + 1.0);
    volatile double principal = 0.5 * root;
    result.w = principal;
    volatile double inverse = 0.5 / root;
    result.x = inverse * (m12 - m21);
    result.y = inverse * (m20 - m02);
    result.z = inverse * (m01 - m10);
  } else if (m00 > m11 && m00 > m22) {
    const double root = std::sqrt(1.0 + m00 - m11 - m22);
    volatile double principal = 0.5 * root;
    result.x = principal;
    volatile double inverse = 0.5 / root;
    result.y = inverse * (m01 + m10);
    result.z = inverse * (m02 + m20);
    result.w = inverse * (m12 - m21);
  } else if (m11 > m22) {
    const double root = std::sqrt(1.0 + m11 - m00 - m22);
    volatile double principal = 0.5 * root;
    result.y = principal;
    volatile double inverse = 0.5 / root;
    result.x = inverse * (m01 + m10);
    result.z = inverse * (m12 + m21);
    result.w = inverse * (m20 - m02);
  } else {
    const double root = std::sqrt(1.0 + m22 - m00 - m11);
    volatile double principal = 0.5 * root;
    result.z = principal;
    volatile double inverse = 0.5 / root;
    result.x = inverse * (m02 + m20);
    result.y = inverse * (m12 + m21);
    result.w = inverse * (m01 - m10);
  }
  return result;
}

struct UnityFbxRotationMatrix3 {
  double m[3][3];
};

static UnityFbxRotationMatrix3 UnityFbxColumnRotationMatrix(
    ufbx_vec3 euler, ufbx_rotation_order order) {
  if (order != UFBX_ROTATION_ORDER_XYZ) {
    const ufbx_quat q = UnityFbxEulerToQuaternion(euler, order);
    const double xx = q.x * q.x;
    const double yy = q.y * q.y;
    const double zz = q.z * q.z;
    const double xy = q.x * q.y;
    const double xz = q.x * q.z;
    const double yz = q.y * q.z;
    const double wx = q.w * q.x;
    const double wy = q.w * q.y;
    const double wz = q.w * q.z;
    return {{{1.0 - 2.0 * (yy + zz), 2.0 * (xy - wz),
              2.0 * (xz + wy)},
             {2.0 * (xy + wz), 1.0 - 2.0 * (xx + zz),
              2.0 * (yz - wx)},
             {2.0 * (xz - wy), 2.0 * (yz + wx),
              1.0 - 2.0 * (xx + yy)}}};
  }

  constexpr double degreesToRadians =
    3.14159265358979323846264338327950288 / 180.0;
  double sx = 0.0, cx = 1.0;
  double sy = 0.0, cy = 1.0;
  double sz = 0.0, cz = 1.0;
  UnityFbxSinCos(euler.x * degreesToRadians, &sx, &cx);
  UnityFbxSinCos(euler.y * degreesToRadians, &sy, &cy);
  UnityFbxSinCos(euler.z * degreesToRadians, &sz, &cz);
  const double sinXSinY = sx * sy;
  const double cosXSinY = cx * sy;
  const double r00 = cy * cz;
  const double r01 = cy * sz;
  const double r02 = -sy;
  volatile double r10Left = cz * sinXSinY;
  volatile double r10Right = cx * sz;
  const double r10 = r10Left - r10Right;
  volatile double r11Left = cx * cz;
  volatile double r11Right = sz * sinXSinY;
  const double r11 = r11Left + r11Right;
  const double r12 = cy * sx;
  volatile double r20Left = sx * sz;
  volatile double r20Right = cz * cosXSinY;
  const double r20 = r20Left + r20Right;
  volatile double r21Left = sz * cosXSinY;
  volatile double r21Right = sx * cz;
  const double r21 = r21Left - r21Right;
  const double r22 = cy * cx;
  // FbxAMatrix stores the row-vector form above. MatrixConverter composes the
  // equivalent column matrices, so transpose once at this boundary.
  return {{{r00, r10, r20}, {r01, r11, r21}, {r02, r12, r22}}};
}

static UnityFbxRotationMatrix3 UnityFbxMultiplyRotationMatrix(
    const UnityFbxRotationMatrix3& left,
    const UnityFbxRotationMatrix3& right) {
  UnityFbxRotationMatrix3 result{};
  for (int row = 0; row < 3; ++row) {
    for (int column = 0; column < 3; ++column) {
      volatile double p0 = left.m[row][0] * right.m[0][column];
      volatile double p1 = left.m[row][1] * right.m[1][column];
      volatile double sum01 = p0 + p1;
      volatile double p2 = left.m[row][2] * right.m[2][column];
      result.m[row][column] = sum01 + p2;
    }
  }
  return result;
}

static UnityFbxRotationMatrix3 UnityFbxTransposeRotationMatrix(
    const UnityFbxRotationMatrix3& source) {
  return {{{source.m[0][0], source.m[1][0], source.m[2][0]},
           {source.m[0][1], source.m[1][1], source.m[2][1]},
           {source.m[0][2], source.m[1][2], source.m[2][2]}}};
}

static ufbx_vec3 UnityFbxRotationMatrixToXyzEuler(
    const UnityFbxRotationMatrix3& columnMatrix) {
  // M2V(XYZ) reads FbxAMatrix's row-vector representation.
  const double r00 = columnMatrix.m[0][0];
  const double r01 = columnMatrix.m[1][0];
  const double r02 = columnMatrix.m[2][0];
  const double r11 = columnMatrix.m[1][1];
  const double r12 = columnMatrix.m[2][1];
  const double r21 = columnMatrix.m[1][2];
  const double r22 = columnMatrix.m[2][2];
  constexpr double singularThreshold = 0x1p-48;
  volatile double projectionX = r00 * r00;
  volatile double projectionY = r01 * r01;
  const double projection = std::sqrt(projectionX + projectionY);
  const double x = projection > singularThreshold
    ? std::atan2(r12, r22) : std::atan2(-r21, r11);
  const double y = std::atan2(-r02, projection);
  const double z = projection > singularThreshold
    ? std::atan2(r01, r00) : 0.0;
  constexpr double radiansToDegrees = 180.0 /
    3.14159265358979323846264338327950288;
  const bool exactIdentityMatrix =
    r00 == 1.0 && r01 == 0.0 && r02 == 0.0 &&
    columnMatrix.m[0][1] == 0.0 && r11 == 1.0 && r12 == 0.0 &&
    columnMatrix.m[0][2] == 0.0 && r21 == 0.0 && r22 == 1.0;
  const auto store = [exactIdentityMatrix, radiansToDegrees](double value) {
    return exactIdentityMatrix ? 0.0f
      : static_cast<float>(value * radiansToDegrees);
  };
  return {store(x), store(y), store(z)};
}

static ufbx_vec3 UnityFbxComposePrePostXyzEuler(
    const ufbx_node* node, ufbx_vec3 localEuler) {
  const ufbx_vec3 preRotation = ufbx_find_vec3(
    &node->props, UFBX_PreRotation, ufbx_vec3{});
  const ufbx_vec3 postRotation = ufbx_find_vec3(
    &node->props, UFBX_PostRotation, ufbx_vec3{});
  const UnityFbxRotationMatrix3 pre = UnityFbxColumnRotationMatrix(
    preRotation, UFBX_ROTATION_ORDER_XYZ);
  const UnityFbxRotationMatrix3 local = UnityFbxColumnRotationMatrix(
    localEuler, node->rotation_order);
  const UnityFbxRotationMatrix3 inversePost = UnityFbxTransposeRotationMatrix(
    UnityFbxColumnRotationMatrix(postRotation, UFBX_ROTATION_ORDER_XYZ));
  return UnityFbxRotationMatrixToXyzEuler(UnityFbxMultiplyRotationMatrix(
    UnityFbxMultiplyRotationMatrix(pre, local), inversePost));
}

static float EvaluateUnityConvertedFrameSegment(
    float previousValue, float leftValue, float rightValue, float nextValue,
    double leftTangentValue, double rightTangentValue,
    bool leftUsesAutoTangent, bool rightUsesAutoTangent,
    double sourceT, double frameStep) {
  const float duration = static_cast<float>(frameStep);
  const auto autoHandle = [duration](float previous, float current, float next) {
    const float leftDelta = current - previous;
    const float rightDelta = next - current;
    if (leftDelta == 0.0f || rightDelta == 0.0f ||
        (leftDelta < 0.0f) != (rightDelta < 0.0f))
      return 0.0f;
    const float span = duration + duration;
    const float slope = (next - previous) / span;
    const float centeredHandle = slope * duration / 3.0f;
    const float clampedMagnitude = std::min({
      std::abs(centeredHandle), std::abs(leftDelta), std::abs(rightDelta),
    });
    return std::copysign(clampedMagnitude, centeredHandle);
  };
  // MatrixConverter initially installs a shared user tangent for each segment.
  // When Unroll selects the alternate XYZ Euler representation, KFCurve
  // recalculates that key's side as a clamped automatic tangent. A transition
  // therefore intentionally mixes a user handle on one side and auto on the
  // other side.
  // SetDestFCurveTangeant divides the double V2VRef delta by its exact FbxTime
  // period before storing a float derivative. EvaluateIndex multiplies that
  // derivative by the float-expanded key duration.
  const float userSlope = static_cast<float>(
    (rightTangentValue - leftTangentValue) / frameStep);
  const float userHandle = userSlope * duration / 3.0f;
  const float outHandle = leftUsesAutoTangent
    ? autoHandle(previousValue, leftValue, rightValue) : userHandle;
  const float inHandle = rightUsesAutoTangent
    ? autoHandle(leftValue, rightValue, nextValue) : userHandle;
  return EvaluateUnityFbxUnweightedCubic(
    leftValue, rightValue, outHandle, inHandle, sourceT);
}

static double UnityFbxWrapEulerNear(double value, double reference) {
  // FbxGetContinuousRotation uses modf(), not round(), so preserve its exact
  // half-turn comparisons and arithmetic order.
  double integral = 0.0;
  const double fraction = std::modf((reference - value) / 360.0, &integral);
  if (fraction > 0.5) integral += 1.0;
  if (fraction < -0.5) integral -= 1.0;
  return value + integral * 360.0;
}

static ufbx_vec3 UnityFbxContinuousXyzEuler(
    const ufbx_vec3& value, const ufbx_vec3& reference,
    bool* usedAlternate = nullptr) {
  const ufbx_vec3 direct = {
    UnityFbxWrapEulerNear(value.x, reference.x),
    UnityFbxWrapEulerNear(value.y, reference.y),
    UnityFbxWrapEulerNear(value.z, reference.z),
  };
  const ufbx_vec3 alternate = {
    UnityFbxWrapEulerNear(value.x + 180.0, reference.x),
    UnityFbxWrapEulerNear(180.0 - value.y, reference.y),
    UnityFbxWrapEulerNear(value.z + 180.0, reference.z),
  };
  const double directX = reference.x - direct.x;
  const double directY = reference.y - direct.y;
  const double directZ = reference.z - direct.z;
  const double alternateX = reference.x - alternate.x;
  const double alternateY = reference.y - alternate.y;
  const double alternateZ = reference.z - alternate.z;
  // FBX emits three multiplies and two ordered additions for each candidate.
  volatile double directSquareX = directX * directX;
  volatile double directSquareY = directY * directY;
  volatile double directSquareZ = directZ * directZ;
  volatile double alternateSquareX = alternateX * alternateX;
  volatile double alternateSquareY = alternateY * alternateY;
  volatile double alternateSquareZ = alternateZ * alternateZ;
  volatile double directSquareXy = directSquareX + directSquareY;
  volatile double alternateSquareXy = alternateSquareX + alternateSquareY;
  const double directDistance = directSquareXy + directSquareZ;
  const double alternateDistance = alternateSquareXy + alternateSquareZ;
  const bool selectAlternate = !(directDistance < alternateDistance);
  if (usedAlternate) *usedAlternate = selectAlternate;
  ufbx_vec3 result = selectAlternate ? alternate : direct;

  // At XYZ gimbal lock X and Z are coupled. FbxGetContinuousRotation splits
  // their residual equally so the continuous destination key stays closest
  // to the preceding frame without changing the represented rotation.
  const double middle = std::fmod(std::fmod(result.y, 360.0) + 360.0, 360.0);
  const bool positiveGimbal = std::abs(middle - 90.0) <= 1e-6;
  const bool negativeGimbal = std::abs(middle - 270.0) <= 1e-6;
  if (positiveGimbal || negativeGimbal) {
    const double xDelta = reference.x - result.x;
    const double zDelta = reference.z - result.z;
    const double adjustment = positiveGimbal
      ? (xDelta + zDelta) * 0.5
      : (xDelta - zDelta) * 0.5;
    result.x += adjustment;
    result.z += positiveGimbal ? adjustment : -adjustment;
  }
  return result;
}

static double UnityFbxSampleTime(
    double playbackTimeBegin, float frameRate, size_t sampleIndex) {
  // Unity creates resampling times through FBX's legacy KTime path. The frame
  // number and seconds-per-frame multiplication happen in float, after which
  // FbxTime truncates to its 141120000-tick second. Keeping that tick grid is
  // observable at source keys and at strict quaternion reduction thresholds.
  constexpr double ticksPerSecond = 141120000.0;
  const float firstFrame = static_cast<float>(playbackTimeBegin * frameRate);
  const float secondsPerFrame = 1.0f / frameRate;
  const float sampleSeconds =
    (firstFrame + static_cast<float>(sampleIndex)) * secondsPerFrame;
  const int64_t ticks = static_cast<int64_t>(
    static_cast<double>(sampleSeconds) * ticksPerSecond);
  return static_cast<double>(ticks) / ticksPerSecond;
}

static double UnityFbxFrameTime(
    double playbackTimeBegin, float frameRate, size_t sampleIndex) {
  constexpr double ticksPerSecond = 141120000.0;
  const int64_t startTicks = static_cast<int64_t>(
    std::llround(playbackTimeBegin * ticksPerSecond));
  const int64_t frameTicks = static_cast<int64_t>(
    std::llround(ticksPerSecond / static_cast<double>(frameRate)));
  return static_cast<double>(
    startTicks + static_cast<int64_t>(sampleIndex) * frameTicks) /
    ticksPerSecond;
}

struct UnityFbxConvertedEulerKey {
  ufbx_vec3 value;
  ufbx_vec3 tangentValue;
  bool usesAutoTangent;
};

static bool HasNonZeroVectorProperty(const ufbx_node* node, const char* name);

static bool UnityFbxPreservesUnrolledQuaternionZeroSigns(
    const ufbx_anim_value* rawRotation) {
  if (!rawRotation) return false;
  for (const ufbx_anim_curve* curve : rawRotation->curves) {
    if (!curve) continue;
    for (size_t index = 0; index < curve->keyframes.count; ++index) {
      const ufbx_keyframe& key = curve->keyframes.data[index];
      // FbxAnimCurveFilterUnroll switches to its half-turn path at the exact
      // +/-180 tie. Quaternion extraction on that path preserves the signed
      // zero produced by the FBX basis conversion; ordinary tracks
      // canonicalize identity components to positive zero.
      if (std::abs(key.value) >= 180.0) return true;
    }
  }
  return false;
}

static std::vector<UnityFbxConvertedEulerKey> BuildUnityFbxConvertedEulerKeys(
    const ufbx_anim_value* rawRotation, const ufbx_node* node,
    ufbx_rotation_order rotationOrder,
    double playbackTimeBegin, float frameRate, size_t keyCount) {
  std::vector<UnityFbxConvertedEulerKey> result;
  const bool hasPreOrPostRotation =
    HasNonZeroVectorProperty(node, UFBX_PreRotation) ||
    HasNonZeroVectorProperty(node, UFBX_PostRotation);
  if (!rawRotation || (!hasPreOrPostRotation &&
      rotationOrder == UFBX_ROTATION_ORDER_XYZ) ||
      rotationOrder == UFBX_ROTATION_ORDER_SPHERIC || !(frameRate > 0.0f))
    return result;

  constexpr double ticksPerSecond = 141120000.0;
  const int64_t startTicks = static_cast<int64_t>(
    std::llround(playbackTimeBegin * ticksPerSecond));
  const int64_t frameTicks = static_cast<int64_t>(
    std::llround(ticksPerSecond / static_cast<double>(frameRate)));
  result.reserve(keyCount);
  ufbx_vec3 previous{};
  for (size_t key = 0; key < keyCount; ++key) {
    const double time = static_cast<double>(
      startTicks + static_cast<int64_t>(key) * frameTicks) / ticksPerSecond;
    ufbx_vec3 stored{};
    if (hasPreOrPostRotation) {
      ufbx_vec3 source = rawRotation->default_value;
      source.x = static_cast<float>(EvaluateUnityCompatibleCurve(
        rawRotation->curves[0], time, source.x));
      source.y = static_cast<float>(EvaluateUnityCompatibleCurve(
        rawRotation->curves[1], time, source.y));
      source.z = static_cast<float>(EvaluateUnityCompatibleCurve(
        rawRotation->curves[2], time, source.z));
      stored = UnityFbxComposePrePostXyzEuler(node, source);
    } else {
      ufbx_vec3 source = rawRotation->default_value;
      source.x = static_cast<float>(EvaluateUnityCompatibleCurve(
        rawRotation->curves[0], time, source.x));
      source.y = static_cast<float>(EvaluateUnityCompatibleCurve(
        rawRotation->curves[1], time, source.y));
      source.z = static_cast<float>(EvaluateUnityCompatibleCurve(
        rawRotation->curves[2], time, source.z));
      UnityFbxEulerToQuaternion(source, rotationOrder, &stored);
    }
    bool usesAutoTangent = false;
    const ufbx_vec3 continuous = key == 0
      ? stored : UnityFbxContinuousXyzEuler(
          stored, previous, &usesAutoTangent);
    // MatrixConverter writes its continuous destination Euler result into
    // float curve keys. Extraction later reads those float-expanded values,
    // while tangent construction retains the full V2VRef result.
    const ufbx_vec3 storedContinuous = {
      static_cast<float>(continuous.x),
      static_cast<float>(continuous.y),
      static_cast<float>(continuous.z),
    };
    result.push_back({storedContinuous, continuous, usesAutoTangent});
    previous = continuous;
  }
  return result;
}

static AnityModelQuaternionKey UnityQuaternionKey(const ufbx_baked_quat& source,
    const ufbx_node* node, const ufbx_anim_value* rawRotation,
    double absoluteTime, double orderedConversionTime, float frameRate,
    size_t sampleIndex,
    const std::vector<UnityFbxConvertedEulerKey>* convertedEulerKeys,
    bool preserveUnrolledZeroSigns,
    bool allowMissingBakedSample, bool* matchedBakedRotation) {
  const AnityModelQuaternionKey fallback = QuaternionKey(source, node);
  if (matchedBakedRotation) *matchedBakedRotation = false;
  if (!node || !rawRotation || node->rotation_order == UFBX_ROTATION_ORDER_SPHERIC) return fallback;

  ufbx_vec3 euler = rawRotation->default_value;
  ufbx_vec3 referenceEuler = rawRotation->default_value;
  // Unity first resamples non-XYZ rotations onto the exact take frame grid,
  // converts each pose through FbxRotationOrder::V2M/M2V(XYZ), then sends the
  // resulting XYZ float curves through its legacy KTime extraction path. XYZ
  // sources skip that conversion and therefore retain the truncated KTime.
  const double evaluationTime = node->rotation_order == UFBX_ROTATION_ORDER_XYZ
    ? absoluteTime : orderedConversionTime;
  // Unity's FBX path evaluates each scalar curve to float before passing the
  // values back to the double-precision FBX matrix/quaternion conversion.
  euler.x = static_cast<float>(EvaluateUnityCompatibleCurve(rawRotation->curves[0], evaluationTime, euler.x));
  euler.y = static_cast<float>(EvaluateUnityCompatibleCurve(rawRotation->curves[1], evaluationTime, euler.y));
  euler.z = static_cast<float>(EvaluateUnityCompatibleCurve(rawRotation->curves[2], evaluationTime, euler.z));
  referenceEuler.x = ufbx_evaluate_curve(rawRotation->curves[0], evaluationTime, referenceEuler.x);
  referenceEuler.y = ufbx_evaluate_curve(rawRotation->curves[1], evaluationTime, referenceEuler.y);
  referenceEuler.z = ufbx_evaluate_curve(rawRotation->curves[2], evaluationTime, referenceEuler.z);
  ufbx_vec3 unityXyzEuler = euler;
  UnityFbxEulerToQuaternion(euler, node->rotation_order, &unityXyzEuler);
  const bool hasConvertedEulerKeys = convertedEulerKeys &&
    !convertedEulerKeys->empty();
  if (hasConvertedEulerKeys && frameRate > 0.0f) {
    const double timeOffset = absoluteTime - orderedConversionTime;
    // Unity evaluates the converted destination curve on its float-derived
    // KTime even when that sample is only a few ticks away from a source key.
    if (convertedEulerKeys && sampleIndex < convertedEulerKeys->size()) {
      const UnityFbxConvertedEulerKey& currentKey =
        (*convertedEulerKeys)[sampleIndex];
      unityXyzEuler = currentKey.value;
      if (timeOffset != 0.0) {
        constexpr double ticksPerSecond = 141120000.0;
        const int64_t sampleTicks = static_cast<int64_t>(
          std::llround(absoluteTime * ticksPerSecond));
        const int64_t orderedTicks = static_cast<int64_t>(
          std::llround(orderedConversionTime * ticksPerSecond));
        const int64_t frameTicks = static_cast<int64_t>(
          std::llround(ticksPerSecond / static_cast<double>(frameRate)));
        const double frameStep = static_cast<double>(frameTicks) / ticksPerSecond;
        // MatrixConverter runs each direct M2V float through V2VRef before it
        // writes the destination curve. That continuous float-expanded sequence
        // defines both the key values and user tangents. Use the track-level
        // sequence precomputed from the take start.
        const int64_t neighborIndex = timeOffset > 0.0
          ? static_cast<int64_t>(sampleIndex) + 1
          : std::max<int64_t>(static_cast<int64_t>(sampleIndex) - 1, 0);
        const size_t neighborKey = static_cast<size_t>(std::clamp<int64_t>(
          neighborIndex, 0,
          static_cast<int64_t>(convertedEulerKeys->size() - 1)));
        const double interpolation = static_cast<double>(
          std::abs(sampleTicks - orderedTicks)) / static_cast<double>(frameTicks);
        const bool usesFollowingFrame = timeOffset > 0.0;
        const double sourceT = usesFollowingFrame ? interpolation : 1.0 - interpolation;
        const size_t segmentLeft = usesFollowingFrame ? sampleIndex : neighborKey;
        const size_t segmentRight = usesFollowingFrame ? neighborKey : sampleIndex;
        const size_t beforeLeft = segmentLeft > 0 ? segmentLeft - 1 : segmentLeft;
        const size_t afterRight = std::min(
          segmentRight + 1, convertedEulerKeys->size() - 1);
        const UnityFbxConvertedEulerKey& previous = (*convertedEulerKeys)[beforeLeft];
        const UnityFbxConvertedEulerKey& left = (*convertedEulerKeys)[segmentLeft];
        const UnityFbxConvertedEulerKey& right = (*convertedEulerKeys)[segmentRight];
        const UnityFbxConvertedEulerKey& next = (*convertedEulerKeys)[afterRight];
        unityXyzEuler.x = EvaluateUnityConvertedFrameSegment(
          static_cast<float>(previous.value.x), static_cast<float>(left.value.x),
          static_cast<float>(right.value.x), static_cast<float>(next.value.x),
          left.tangentValue.x, right.tangentValue.x,
          left.usesAutoTangent, right.usesAutoTangent,
          sourceT, frameStep);
        unityXyzEuler.y = EvaluateUnityConvertedFrameSegment(
          static_cast<float>(previous.value.y), static_cast<float>(left.value.y),
          static_cast<float>(right.value.y), static_cast<float>(next.value.y),
          left.tangentValue.y, right.tangentValue.y,
          left.usesAutoTangent, right.usesAutoTangent,
          sourceT, frameStep);
        unityXyzEuler.z = EvaluateUnityConvertedFrameSegment(
          static_cast<float>(previous.value.z), static_cast<float>(left.value.z),
          static_cast<float>(right.value.z), static_cast<float>(next.value.z),
          left.tangentValue.z, right.tangentValue.z,
          left.usesAutoTangent, right.usesAutoTangent,
          sourceT, frameStep);
      }
    }
  }
  const ufbx_quat referenceRaw = ufbx_euler_to_quat(referenceEuler, node->rotation_order);
  const ufbx_quat inverseAdjustment = {
    -node->adjust_pre_rotation.x, -node->adjust_pre_rotation.y,
    -node->adjust_pre_rotation.z, node->adjust_pre_rotation.w,
  };
  // Conjugate into Unity's imported coordinate basis without a double-precision
  // normalization. Unity converts the FBX quaternion to float first and only
  // then normalizes it below.
  const bool xAxisBasis = UsesUnityFbxXAxisBasis(node);
  // ExtractQuaternionFromFBXEulerOld receives FbxVector4 values expanded from
  // the float destination curves, including values evaluated between keys.
  // Preserve that final curve-output rounding before SetROnly/GetQ.
  const ufbx_vec3 extractedEuler = {
    static_cast<float>(unityXyzEuler.x),
    static_cast<float>(unityXyzEuler.y),
    static_cast<float>(unityXyzEuler.z),
  };
  const ufbx_quat extractedRaw = UnityFbxEulerToQuaternion(
    extractedEuler, UFBX_ROTATION_ORDER_XYZ);
  const ufbx_quat compatible = xAxisBasis
    ? UnityFbxEulerToQuaternion(
        {extractedEuler.x, -extractedEuler.y, -extractedEuler.z},
        UFBX_ROTATION_ORDER_XYZ)
    : ufbx_quat_mul(
        ufbx_quat_mul(node->adjust_pre_rotation, extractedRaw), inverseAdjustment);
  const ufbx_quat reference = hasConvertedEulerKeys
    ? compatible
    : ufbx_quat_mul(
        ufbx_quat_mul(node->adjust_pre_rotation, referenceRaw), inverseAdjustment);

  // Only replace a baked value when it matches the reconstructed rotation.
  // Pre/post tracks compare against MatrixConverter's composed XYZ result;
  // unsupported layers and helper transforms retain the baked path.
  const double dx = static_cast<double>(fallback.x) - reference.x;
  const double dy = static_cast<double>(fallback.y) - reference.y;
  const double dz = static_cast<double>(fallback.z) - reference.z;
  const double dw = static_cast<double>(fallback.w) - reference.w;
  const double sx = static_cast<double>(fallback.x) + reference.x;
  const double sy = static_cast<double>(fallback.y) + reference.y;
  const double sz = static_cast<double>(fallback.z) + reference.z;
  const double sw = static_cast<double>(fallback.w) + reference.w;
  const bool matchesBakedRotation =
    std::min(dx * dx + dy * dy + dz * dz + dw * dw,
      sx * sx + sy * sy + sz * sz + sw * sw) <= 1e-10;
  if (matchedBakedRotation) *matchedBakedRotation = matchesBakedRotation;
  if (!matchesBakedRotation && !allowMissingBakedSample) return fallback;
  float x = Real(compatible.x);
  float y = Real(compatible.y);
  float z = Real(compatible.z);
  float w = Real(compatible.w);
  // Unity emits four separate multiplies and three ordered adds here. Prevent
  // FP contraction/reassociation: a fused sum changes the normalized result
  // by one ULP on otherwise identical FBX samples.
  volatile float squareX = x * x;
  volatile float squareY = y * y;
  volatile float squareZ = z * z;
  volatile float squareW = w * w;
  volatile float sumXY = squareX + squareY;
  volatile float sumXYZ = sumXY + squareZ;
  volatile float sumXYZW = sumXYZ + squareW;
  const float magnitude = std::sqrt(sumXYZW);
  if (magnitude > 1e-6f) {
    // Keep component-wise division: this is the instruction sequence used by
    // Unity's ExtractQuaternionFromFBXEulerOld path after FbxAMatrix::GetQ().
    x /= magnitude;
    y /= magnitude;
    z /= magnitude;
    w /= magnitude;
  }
  if ((hasConvertedEulerKeys ||
      node->rotation_order != UFBX_ROTATION_ORDER_XYZ) &&
      !preserveUnrolledZeroSigns) {
    if (x == 0.0f) x = 0.0f;
    if (y == 0.0f) y = 0.0f;
    if (z == 0.0f) z = 0.0f;
    if (w == 0.0f) w = 0.0f;
  }
  return { Real(source.time), x, y, z, w };
}

static double WrapAngleNear(double value, double reference) {
  while (value - reference > 180.0) value -= 360.0;
  while (value - reference < -180.0) value += 360.0;
  return value;
}

static float RawInTangent(const ufbx_anim_curve* curve, size_t index);
static float RawOutTangent(const ufbx_anim_curve* curve, size_t index);
static void BuildCentralScalarTangents(std::vector<AnityModelScalarKey>& keys);

static double RawTransformFactor(AnityModelTransformCurveProperty property, float globalScale) {
  switch (property) {
    case ANITY_MODEL_POSITION_X: return -globalScale;
    case ANITY_MODEL_POSITION_Y:
    case ANITY_MODEL_POSITION_Z: return globalScale;
    case ANITY_MODEL_EULER_X: return 1.0;
    case ANITY_MODEL_EULER_Y:
    case ANITY_MODEL_EULER_Z: return -1.0;
    default: return 1.0;
  }
}

static double EvaluateBakedVectorComponent(const ufbx_baked_node* node,
    AnityModelTransformCurveProperty property, double time, float globalScale) {
  switch (property) {
    case ANITY_MODEL_POSITION_X:
    case ANITY_MODEL_POSITION_Y:
    case ANITY_MODEL_POSITION_Z: {
      const ufbx_vec3 value = ufbx_evaluate_baked_vec3(node->translation_keys, time);
      const double component = property == ANITY_MODEL_POSITION_X ? value.x :
        property == ANITY_MODEL_POSITION_Y ? value.y : value.z;
      return component * globalScale;
    }
    case ANITY_MODEL_SCALE_X:
    case ANITY_MODEL_SCALE_Y:
    case ANITY_MODEL_SCALE_Z: {
      const ufbx_vec3 value = ufbx_evaluate_baked_vec3(node->scale_keys, time);
      return property == ANITY_MODEL_SCALE_X ? value.x :
        property == ANITY_MODEL_SCALE_Y ? value.y : value.z;
    }
    default: return 0.0;
  }
}

static void BuildRawTransformCurve(const ufbx_baked_node* node,
    const ufbx_anim_curve* source, AnityModelTransformCurveProperty property,
    double trimStart, float globalScale, Track& track) {
  if (!source || source->keyframes.count == 0) return;
  Track::TransformCurve curve;
  curve.property = property;
  curve.keys.reserve(source->keyframes.count);
  double reference = 0.0;
  for (size_t index = 0; index < source->keyframes.count; ++index) {
    const double time = source->keyframes.data[index].time;
    const double bakedTime = time - trimStart;
    const bool isEuler = property >= ANITY_MODEL_EULER_X && property <= ANITY_MODEL_EULER_Z;
    const double factor = RawTransformFactor(property, globalScale);
    const double value = isEuler
      ? WrapAngleNear(source->keyframes.data[index].value * factor, reference)
      : EvaluateBakedVectorComponent(node, property, bakedTime, globalScale);
    reference = value;
    double inTangent = RawInTangent(source, index) * factor;
    double outTangent = RawOutTangent(source, index) * factor;
    if (index == 0) inTangent = outTangent;
    if (index + 1 == source->keyframes.count) outTangent = inTangent;
    float relativeTime = Real(time - trimStart);
    if (std::abs(relativeTime) < 1e-7f) relativeTime = 0.0f;
    curve.keys.push_back({relativeTime, Real(value), Real(inTangent), Real(outTangent)});
  }
  track.transformCurves.push_back(std::move(curve));
}

static bool HasNonZeroVectorProperty(const ufbx_node* node, const char* name) {
  if (!node) return false;
  const ufbx_vec3 value = ufbx_find_vec3(&node->props, name, ufbx_vec3{});
  return std::abs(value.x) > 1e-9 || std::abs(value.y) > 1e-9 ||
    std::abs(value.z) > 1e-9;
}

static void BuildRawTransformGroup(const ufbx_anim_stack* stack,
    const ufbx_node* sourceNode, const ufbx_baked_node* bakedNode,
    const char* name, AnityModelTransformCurveProperty first,
    double trimStart, float globalScale, Track& track) {
  const ufbx_anim_value* value =
    FindSingleRawValue(stack, &sourceNode->element, name);
  if (!value) return;
  for (int axis = 0; axis < 3; ++axis)
    BuildRawTransformCurve(bakedNode, value->curves[axis],
      static_cast<AnityModelTransformCurveProperty>(
        static_cast<int>(first) + axis),
      trimStart, globalScale, track);
}

static void BuildSampledEulerCurves(const ufbx_anim_stack* stack,
    const ufbx_node* sourceNode, double trimStart, float frameRate,
    size_t sampleCount, float globalScale, Track& track) {
  const ufbx_anim_value* value =
    FindSingleRawValue(stack, &sourceNode->element, UFBX_Lcl_Rotation);
  if (!value || sampleCount == 0) return;
  for (int axis = 0; axis < 3; ++axis) {
    Track::TransformCurve curve;
    curve.property = static_cast<AnityModelTransformCurveProperty>(
      static_cast<int>(ANITY_MODEL_EULER_X) + axis);
    curve.keys.reserve(sampleCount);
    const double factor = RawTransformFactor(curve.property, globalScale);
    double reference = 0.0;
    for (size_t index = 0; index < sampleCount; ++index) {
      // MatrixConverter installs destination keys on the exact FbxTime frame
      // grid. This differs from ExtractQuaternionFromFBXEulerOld's legacy
      // float frame-to-seconds resampling by several ticks at common rates.
      const double absoluteTime = UnityFbxFrameTime(trimStart, frameRate, index);
      const double sourceValue = EvaluateUnityCompatibleCurve(
        value->curves[axis], absoluteTime, value->default_value.v[axis]);
      const double sampled = WrapAngleNear(sourceValue * factor, reference);
      reference = sampled;
      float relativeTime = Real(absoluteTime) - Real(trimStart);
      if (std::abs(relativeTime) < 1e-7f) relativeTime = 0.0f;
      curve.keys.push_back({relativeTime, Real(sampled), 0.0f, 0.0f});
    }
    BuildCentralScalarTangents(curve.keys);
    track.transformCurves.push_back(std::move(curve));
  }
}

static ufbx_vec3 EvaluateUnityRawVector(const ufbx_anim_stack* stack,
    const ufbx_node* node, const char* propertyName,
    ufbx_vec3 fallback, double time) {
  const ufbx_anim_value* value = FindSingleRawValue(
    stack, &node->element, propertyName);
  if (!value) return fallback;
  ufbx_vec3 result = value->default_value;
  for (int axis = 0; axis < 3; ++axis) {
    result.v[axis] = static_cast<float>(EvaluateUnityCompatibleCurve(
      value->curves[axis], time, result.v[axis]));
  }
  return result;
}

struct UnityFbxAffineMatrix {
  double m[3][3];
  double t[3];
};

static UnityFbxAffineMatrix UnityFbxIdentityMatrix() {
  return {{{1.0, 0.0, 0.0}, {0.0, 1.0, 0.0}, {0.0, 0.0, 1.0}},
    {0.0, 0.0, 0.0}};
}

static UnityFbxAffineMatrix UnityFbxTranslationMatrix(ufbx_vec3 value) {
  UnityFbxAffineMatrix result = UnityFbxIdentityMatrix();
  result.t[0] = value.x;
  result.t[1] = value.y;
  result.t[2] = value.z;
  return result;
}

static UnityFbxAffineMatrix UnityFbxScaleMatrix(ufbx_vec3 value) {
  UnityFbxAffineMatrix result = UnityFbxIdentityMatrix();
  result.m[0][0] = value.x;
  result.m[1][1] = value.y;
  result.m[2][2] = value.z;
  return result;
}

static UnityFbxAffineMatrix UnityFbxMultiplyMatrix(
    const UnityFbxAffineMatrix& left, const UnityFbxAffineMatrix& right) {
  UnityFbxAffineMatrix result{};
  for (int row = 0; row < 3; ++row) {
    for (int column = 0; column < 3; ++column) {
      volatile double p0 = left.m[row][0] * right.m[0][column];
      volatile double p1 = left.m[row][1] * right.m[1][column];
      volatile double sum01 = p0 + p1;
      volatile double p2 = left.m[row][2] * right.m[2][column];
      result.m[row][column] = sum01 + p2;
    }
    volatile double p0 = left.m[row][0] * right.t[0];
    volatile double p1 = left.m[row][1] * right.t[1];
    volatile double sum01 = p0 + p1;
    volatile double p2 = left.m[row][2] * right.t[2];
    volatile double sum012 = sum01 + p2;
    result.t[row] = sum012 + left.t[row];
  }
  return result;
}

static UnityFbxAffineMatrix UnityFbxRotationMatrixXyz(ufbx_vec3 euler) {
  constexpr double degreesToRadians =
    3.14159265358979323846264338327950288 / 180.0;
  double sx = 0.0, cx = 1.0;
  double sy = 0.0, cy = 1.0;
  double sz = 0.0, cz = 1.0;
  UnityFbxSinCos(euler.x * degreesToRadians, &sx, &cx);
  UnityFbxSinCos(euler.y * degreesToRadians, &sy, &cy);
  UnityFbxSinCos(euler.z * degreesToRadians, &sz, &cz);

  const double sinXSinY = sx * sy;
  const double cosXSinY = cx * sy;
  const double m00 = cy * cz;
  const double m01 = cy * sz;
  const double m02 = -sy;
  volatile double m10Left = cz * sinXSinY;
  volatile double m10Right = cx * sz;
  const double m10 = m10Left - m10Right;
  volatile double m11Left = cx * cz;
  volatile double m11Right = sz * sinXSinY;
  const double m11 = m11Left + m11Right;
  const double m12 = cy * sx;
  volatile double m20Left = sx * sz;
  volatile double m20Right = cz * cosXSinY;
  const double m20 = m20Left + m20Right;
  volatile double m21Left = sz * cosXSinY;
  volatile double m21Right = sx * cz;
  const double m21 = m21Left - m21Right;
  const double m22 = cy * cx;

  return {{{m00, m10, m20}, {m01, m11, m21}, {m02, m12, m22}},
    {0.0, 0.0, 0.0}};
}

static ufbx_vec3 EvaluateUnityRetainedPivotPosition(
    const ufbx_anim_stack* stack, const ufbx_node* node, double time) {
  const ufbx_vec3 rotationPivot = ufbx_find_vec3(
    &node->props, UFBX_RotationPivot, ufbx_vec3{});
  const ufbx_vec3 scalingPivot = ufbx_find_vec3(
    &node->props, UFBX_ScalingPivot, ufbx_vec3{});
  const ufbx_vec3 rotationOffset = ufbx_find_vec3(
    &node->props, UFBX_RotationOffset, ufbx_vec3{});
  const ufbx_vec3 scalingOffset = ufbx_find_vec3(
    &node->props, UFBX_ScalingOffset, ufbx_vec3{});
  const ufbx_vec3 translation = EvaluateUnityRawVector(
    stack, node, UFBX_Lcl_Translation,
    ufbx_find_vec3(&node->props, UFBX_Lcl_Translation, ufbx_vec3{}), time);
  const ufbx_vec3 rotation = EvaluateUnityRawVector(
    stack, node, UFBX_Lcl_Rotation,
    ufbx_find_vec3(&node->props, UFBX_Lcl_Rotation, ufbx_vec3{}), time);
  const ufbx_vec3 scaling = EvaluateUnityRawVector(
    stack, node, UFBX_Lcl_Scaling,
    ufbx_find_vec3(&node->props, UFBX_Lcl_Scaling, ufbx_vec3{1.0, 1.0, 1.0}), time);

  UnityFbxAffineMatrix matrix = UnityFbxTranslationMatrix(translation);
  matrix = UnityFbxMultiplyMatrix(
    matrix, UnityFbxTranslationMatrix(rotationOffset));
  matrix = UnityFbxMultiplyMatrix(
    matrix, UnityFbxTranslationMatrix(rotationPivot));
  if (node->rotation_order == UFBX_ROTATION_ORDER_XYZ) {
    matrix = UnityFbxMultiplyMatrix(
      matrix, UnityFbxRotationMatrixXyz(rotation));
  } else {
    const ufbx_transform transform{ufbx_vec3{},
      UnityFbxEulerToQuaternion(rotation, node->rotation_order),
      ufbx_vec3{1.0, 1.0, 1.0}};
    const ufbx_matrix source = ufbx_transform_to_matrix(&transform);
    UnityFbxAffineMatrix converted{};
    for (int row = 0; row < 3; ++row)
      for (int column = 0; column < 3; ++column)
        converted.m[row][column] = source.cols[column].v[row];
    matrix = UnityFbxMultiplyMatrix(matrix, converted);
  }
  matrix = UnityFbxMultiplyMatrix(matrix, UnityFbxTranslationMatrix(
    ufbx_vec3{-rotationPivot.x, -rotationPivot.y, -rotationPivot.z}));
  matrix = UnityFbxMultiplyMatrix(
    matrix, UnityFbxTranslationMatrix(scalingOffset));
  matrix = UnityFbxMultiplyMatrix(
    matrix, UnityFbxTranslationMatrix(scalingPivot));
  matrix = UnityFbxMultiplyMatrix(matrix, UnityFbxScaleMatrix(scaling));
  matrix = UnityFbxMultiplyMatrix(matrix, UnityFbxTranslationMatrix(
    ufbx_vec3{-scalingPivot.x, -scalingPivot.y, -scalingPivot.z}));
  ufbx_vec3 value{matrix.t[0], matrix.t[1], matrix.t[2]};
  for (int axis = 0; axis < 3; ++axis)
    value.v[axis] += node->adjust_pre_translation.v[axis];

  const ufbx_quat adjust = node->adjust_pre_rotation;
  if (adjust.x == 1.0 && adjust.y == 0.0 &&
      adjust.z == 0.0 && adjust.w == 0.0) {
    value.y = -value.y;
    value.z = -value.z;
  } else {
    value = ufbx_quat_rotate_vec3(adjust, value);
  }
  const double scale = static_cast<float>(node->adjust_pre_scale *
    node->adjust_translation_scale);
  value.x = static_cast<float>(value.x) * static_cast<float>(scale);
  value.y = static_cast<float>(value.y) * static_cast<float>(scale);
  value.z = static_cast<float>(value.z) * static_cast<float>(scale);
  if (node->adjust_mirror_axis != UFBX_MIRROR_AXIS_NONE)
    value.v[static_cast<int>(node->adjust_mirror_axis) - 1] =
      -value.v[static_cast<int>(node->adjust_mirror_axis) - 1];
  return value;
}

static void BuildRetainedPivotPositionCurves(
    const ufbx_anim_stack* retainedStack, const ufbx_node* retainedNode,
    double trimStart, float frameRate, size_t sampleCount,
    float globalScale, Track& track) {
  if (!retainedStack || !retainedNode || sampleCount == 0) return;
  for (int axis = 0; axis < 3; ++axis) {
    Track::TransformCurve curve;
    curve.property = static_cast<AnityModelTransformCurveProperty>(
      static_cast<int>(ANITY_MODEL_POSITION_X) + axis);
    curve.keys.reserve(sampleCount);
    for (size_t index = 0; index < sampleCount; ++index) {
      const double absoluteTime = UnityFbxFrameTime(trimStart, frameRate, index);
      const ufbx_vec3 value = EvaluateUnityRetainedPivotPosition(
        retainedStack, retainedNode, absoluteTime);
      const double component = axis == 0 ? value.x : axis == 1 ? value.y : value.z;
      float relativeTime = Real(absoluteTime) - Real(trimStart);
      if (std::abs(relativeTime) < 1e-7f) relativeTime = 0.0f;
      curve.keys.push_back({relativeTime, Real(component * globalScale), 0.0f, 0.0f});
    }
    BuildCentralScalarTangents(curve.keys);
    track.transformCurves.push_back(std::move(curve));
  }
}

static void BuildUnityRawTransformCurves(const ufbx_anim_stack* stack,
    const ufbx_node* sourceNode, const ufbx_baked_node* bakedNode,
    const ufbx_anim_stack* retainedPivotStack,
    const ufbx_node* retainedPivotNode,
    double trimStart, float frameRate, size_t sampleCount,
    float globalScale, Track& track) {
  const bool hasPreOrPostRotation =
    HasNonZeroVectorProperty(sourceNode, UFBX_PreRotation) ||
    HasNonZeroVectorProperty(sourceNode, UFBX_PostRotation);
  const bool hasAdjustedPivot =
    HasNonZeroVectorProperty(sourceNode, UFBX_ScalingPivot);
  const bool hasRotationOffset =
    HasNonZeroVectorProperty(sourceNode, UFBX_RotationOffset);

  if (hasPreOrPostRotation) {
    // MatrixConverter can preserve independent translation source keys, but
    // pre/post rotation couples the rotation and scale decomposition. Unity
    // emits baked quaternion/scale curves even when Resample Curves is off.
    BuildRawTransformGroup(stack, sourceNode, bakedNode,
      UFBX_Lcl_Translation, ANITY_MODEL_POSITION_X,
      trimStart, globalScale, track);
    return;
  }

  if (hasAdjustedPivot) {
    // Pivot compensation couples translation and scale, so those channels use
    // the shared baked grid. With no rotation offset Unity can still expose
    // the evaluated Euler source as localEulerAnglesRaw; otherwise it falls
    // back to the baked quaternion path.
    BuildRetainedPivotPositionCurves(retainedPivotStack, retainedPivotNode,
      trimStart, frameRate, sampleCount, globalScale, track);
    if (!hasRotationOffset)
      BuildSampledEulerCurves(stack, sourceNode, trimStart, frameRate,
        sampleCount, globalScale, track);
    return;
  }

  BuildRawTransformGroup(stack, sourceNode, bakedNode,
    UFBX_Lcl_Translation, ANITY_MODEL_POSITION_X,
    trimStart, globalScale, track);
  BuildRawTransformGroup(stack, sourceNode, bakedNode,
    UFBX_Lcl_Rotation, ANITY_MODEL_EULER_X,
    trimStart, globalScale, track);
  BuildRawTransformGroup(stack, sourceNode, bakedNode,
    UFBX_Lcl_Scaling, ANITY_MODEL_SCALE_X,
    trimStart, globalScale, track);
}

static float Secant(const ufbx_keyframe& left, const ufbx_keyframe& right) {
  const double delta = right.time - left.time;
  return std::abs(delta) > 1e-12 ? Real((right.value - left.value) / delta) : 0.0f;
}

static float HandleSlope(ufbx_tangent tangent, float fallback) {
  return std::abs(tangent.dx) > 1e-12f ? Real(tangent.dy / tangent.dx) : fallback;
}

static float RawInTangent(const ufbx_anim_curve* curve, size_t index) {
  if (!curve || curve->keyframes.count < 2) return 0.0f;
  if (index == 0) index = 1;
  const ufbx_keyframe& previous = curve->keyframes.data[index - 1];
  const ufbx_keyframe& key = curve->keyframes.data[index];
  const float linear = Secant(previous, key);
  return previous.interpolation == UFBX_INTERPOLATION_CUBIC ? HandleSlope(key.left, linear) : linear;
}

static float RawOutTangent(const ufbx_anim_curve* curve, size_t index) {
  if (!curve || curve->keyframes.count < 2) return 0.0f;
  if (index + 1 >= curve->keyframes.count) return RawInTangent(curve, index);
  const ufbx_keyframe& key = curve->keyframes.data[index];
  const ufbx_keyframe& next = curve->keyframes.data[index + 1];
  const float linear = Secant(key, next);
  return key.interpolation == UFBX_INTERPOLATION_CUBIC ? HandleSlope(key.right, linear) : linear;
}

static bool BuildRawScalarKeys(const ufbx_anim_stack* stack, const ufbx_element* element,
    ufbx_string propertyName, double trimStart, std::vector<AnityModelScalarKey>& destination) {
  const ufbx_anim_curve* curve = FindSingleRawCurve(stack, element, propertyName);
  if (!curve || curve->keyframes.count == 0) return false;
  destination.reserve(curve->keyframes.count);
  for (size_t index = 0; index < curve->keyframes.count; ++index) {
    const ufbx_keyframe& key = curve->keyframes.data[index];
    float inTangent = RawInTangent(curve, index);
    float outTangent = RawOutTangent(curve, index);
    if (index == 0) inTangent = outTangent;
    if (index + 1 == curve->keyframes.count) outTangent = inTangent;
    float time = Real(key.time - trimStart);
    if (std::abs(time) < 1e-7f) time = 0.0f;
    destination.push_back({time, Real(key.value), inTangent, outTangent});
  }
  return true;
}

static void BuildCentralScalarTangents(std::vector<AnityModelScalarKey>& keys) {
  if (keys.empty()) return;
  for (size_t index = 0; index < keys.size(); ++index) {
    size_t left = index == 0 ? 0 : index - 1;
    size_t right = index + 1 < keys.size() ? index + 1 : index;
    const float delta = keys[right].time - keys[left].time;
    const float tangent = std::abs(delta) > 1e-8f ? (keys[right].value - keys[left].value) / delta : 0.0f;
    keys[index].inTangent = tangent;
    keys[index].outTangent = tangent;
  }
}

template <typename T>
static AnityResult CopyVector(const std::vector<T>& source, T* destination, int32_t capacity) {
  if (capacity < 0 || capacity < static_cast<int32_t>(source.size()) || (!destination && !source.empty()))
    return ANITY_ERR_INVALID_ARG;
  if (!source.empty()) std::memcpy(destination, source.data(), source.size() * sizeof(T));
  return ANITY_OK;
}

} // namespace

struct AnityModelScene {
  float fileScale = 1.0f;
  float frameRate = 30.0f;
  std::vector<Node> nodes;
  std::vector<Mesh> meshes;
  std::vector<Clip> clips;
};

extern "C" {

AnityResult ANITY_CALL AnityModel_LoadFile(
    const char* path, const AnityModelImportOptions* options,
    AnityModelScene** outScene, char* errorBuffer, int32_t errorBufferSize) {
  if (!path || !options || !outScene) return ANITY_ERR_INVALID_ARG;
  *outScene = nullptr;
  if (!std::isfinite(options->globalScale) || options->globalScale <= 0.0f) {
    CopyError(errorBuffer, errorBufferSize, "Model global scale must be finite and greater than zero.");
    return ANITY_ERR_INVALID_ARG;
  }
  if (options->maxBonesPerVertex < 1 || options->maxBonesPerVertex > 8 ||
      !std::isfinite(options->minBoneWeight) || options->minBoneWeight < 0.0f || options->minBoneWeight > 1.0f) {
    CopyError(errorBuffer, errorBufferSize, "Model skin weight options are outside the supported range.");
    return ANITY_ERR_INVALID_ARG;
  }

  ufbx_load_opts loadOptions{};
  // Match Unity's legacy FBX axis conversion. ufbx's built-in left-handed
  // Y-up preset produces the opposite transform signs for Maya-style FBX data.
  loadOptions.target_axes = {
    UFBX_COORDINATE_AXIS_NEGATIVE_X,
    UFBX_COORDINATE_AXIS_NEGATIVE_Y,
    UFBX_COORDINATE_AXIS_NEGATIVE_Z,
  };
  loadOptions.target_unit_meters = options->useFileUnits ? 1.0 : 0.0;
  loadOptions.space_conversion = UFBX_SPACE_CONVERSION_MODIFY_GEOMETRY;
  loadOptions.geometry_transform_handling = UFBX_GEOMETRY_TRANSFORM_HANDLING_MODIFY_GEOMETRY;
  loadOptions.inherit_mode_handling = UFBX_INHERIT_MODE_HANDLING_COMPENSATE;
  // Unity imports the FBX node at its rotation pivot, moving the inverse
  // offset into geometry/children. Retaining the raw pivot keeps the same
  // world-space mesh but exposes different local transforms and animation.
  loadOptions.pivot_handling = UFBX_PIVOT_HANDLING_ADJUST_TO_ROTATION_PIVOT;
  loadOptions.generate_missing_normals = options->generateMissingNormals != 0;
  loadOptions.ignore_animation = options->importAnimation == 0;
  loadOptions.ignore_missing_external_files = true;
  loadOptions.load_external_files = true;
  loadOptions.clean_skin_weights = true;
  loadOptions.node_depth_limit = 4096;
  loadOptions.temp_allocator.memory_limit = static_cast<size_t>(512) * 1024 * 1024;
  loadOptions.result_allocator.memory_limit = static_cast<size_t>(1024) * 1024 * 1024;

  ufbx_error loadError{};
  ufbx_scene* source = ufbx_load_file(path, &loadOptions, &loadError);
  if (!source) {
    char buffer[1024]{};
    ufbx_format_error(buffer, sizeof(buffer), &loadError);
    CopyError(errorBuffer, errorBufferSize, buffer);
    return loadError.type == UFBX_ERROR_FILE_NOT_FOUND ? ANITY_ERR_IO : ANITY_ERR_DECODE;
  }

  ufbx_scene* rawTransformSource = nullptr;
  if (options->resampleCurves == 0 && options->importAnimation != 0) {
    ufbx_load_opts rawTransformOptions = loadOptions;
    rawTransformOptions.pivot_handling = UFBX_PIVOT_HANDLING_RETAIN;
    ufbx_error rawTransformError{};
    rawTransformSource = ufbx_load_file(path, &rawTransformOptions, &rawTransformError);
    if (!rawTransformSource) {
      char buffer[1024]{};
      ufbx_format_error(buffer, sizeof(buffer), &rawTransformError);
      CopyError(errorBuffer, errorBufferSize, buffer);
      ufbx_free_scene(source);
      return rawTransformError.type == UFBX_ERROR_FILE_NOT_FOUND
        ? ANITY_ERR_IO : ANITY_ERR_DECODE;
    }
  }

  AnityModelScene* scene = new (std::nothrow) AnityModelScene();
  if (!scene) {
    if (rawTransformSource) ufbx_free_scene(rawTransformSource);
    ufbx_free_scene(source);
    return ANITY_ERR_OUT_OF_MEMORY;
  }

  AnityResult result = ANITY_OK;
  try {
    scene->fileScale = Real(source->settings.original_unit_meters > 0.0 ? source->settings.original_unit_meters : 1.0);
    scene->frameRate = Real(source->settings.frames_per_second > 0.0 ? source->settings.frames_per_second : 30.0);
    std::string buildError;
    std::unordered_map<uint32_t, int32_t> nodeMap;
    scene->nodes.reserve(source->nodes.count > 0 ? source->nodes.count - 1 : 0);
    for (size_t index = 0; index < source->nodes.count; ++index) {
      const ufbx_node* node = source->nodes.data[index];
      if (node == source->root_node) continue;
      nodeMap[node->typed_id] = static_cast<int32_t>(scene->nodes.size());
      Node destination;
      destination.name = String(node->name);
      destination.meshIndex = node->mesh ? static_cast<int32_t>(node->mesh->typed_id) : -1;
      destination.transform = node->local_transform;
      destination.transform.rotation = RemoveRootAxisRotation(destination.transform.rotation, node);
      destination.transform.translation.x *= options->globalScale;
      destination.transform.translation.y *= options->globalScale;
      destination.transform.translation.z *= options->globalScale;
      scene->nodes.push_back(std::move(destination));
    }
    for (size_t index = 0; index < source->nodes.count; ++index) {
      const ufbx_node* node = source->nodes.data[index];
      if (node == source->root_node) continue;
      const int32_t mapped = nodeMap[node->typed_id];
      if (node->parent && node->parent != source->root_node) {
        const auto found = nodeMap.find(node->parent->typed_id);
        if (found != nodeMap.end()) scene->nodes[mapped].parentIndex = found->second;
      }
    }

    scene->meshes.resize(source->meshes.count);
    for (size_t index = 0; index < source->meshes.count; ++index) {
      if (!BuildMesh(source->meshes.data[index], options->globalScale,
          options->maxBonesPerVertex, options->minBoneWeight, options->importBlendShapes != 0,
          nodeMap, scene->meshes[index], buildError)) {
        CopyError(errorBuffer, errorBufferSize, buildError.c_str());
        result = ANITY_ERR_DECODE;
        break;
      }
    }

    if (result == ANITY_OK && options->importAnimation) {
      for (size_t clipIndex = 0; clipIndex < source->anim_stacks.count; ++clipIndex) {
        const ufbx_anim_stack* stack = source->anim_stacks.data[clipIndex];
        ufbx_bake_opts bakeOptions{};
        bakeOptions.trim_start_time = true;
        bakeOptions.resample_rate = scene->frameRate;
        bakeOptions.minimum_sample_rate = scene->frameRate + 0.5;
        bakeOptions.maximum_sample_rate = std::max<double>(scene->frameRate, 1.0);
        // Unity imports FBX deformation percentages at the source take's sample
        // rate. Keep those samples intact: reducing the shared baked animation
        // here can remove a blend-shape frame and change its interpolated value.
        bakeOptions.key_reduction_enabled = false;
        ufbx_error bakeError{};
        ufbx_baked_anim* baked = ufbx_bake_anim(source, stack->anim, &bakeOptions, &bakeError);
        if (!baked) {
          char buffer[1024]{};
          ufbx_format_error(buffer, sizeof(buffer), &bakeError);
          CopyError(errorBuffer, errorBufferSize, buffer);
          result = ANITY_ERR_DECODE;
          break;
        }

        const ufbx_anim_stack* retainedPivotStack = nullptr;
        if (rawTransformSource &&
            clipIndex < rawTransformSource->anim_stacks.count) {
          retainedPivotStack =
            rawTransformSource->anim_stacks.data[clipIndex];
        }

        Clip clip;
        clip.name = String(stack->name);
        if (clip.name.empty()) clip.name = "Default Take";
        clip.duration = 0.0f;
        clip.frameRate = scene->frameRate;
        clip.firstFrame = Real(baked->playback_time_begin * scene->frameRate);
        clip.lastFrame = Real(baked->playback_time_end * scene->frameRate);
        clip.tracks.reserve(baked->nodes.count);
        for (size_t trackIndex = 0; trackIndex < baked->nodes.count; ++trackIndex) {
          const ufbx_baked_node& bakedNode = baked->nodes.data[trackIndex];
          const auto found = nodeMap.find(bakedNode.typed_id);
          if (found == nodeMap.end()) continue;
          Track track;
          track.nodeIndex = found->second;
          track.positionKeys.reserve(bakedNode.translation_keys.count);
          for (size_t key = 0; key < bakedNode.translation_keys.count; ++key)
            track.positionKeys.push_back(VectorKey(bakedNode.translation_keys.data[key], options->globalScale));
          // Unity resamples a node's transform channels on one shared frame
          // grid. ufbx may omit the last quaternion when an Euler source key is
          // between frames, while translation and scale still retain the full
          // node interval.
          const size_t rotationSampleCount = std::max({
            bakedNode.rotation_keys.count,
            bakedNode.translation_keys.count,
            bakedNode.scale_keys.count,
          });
          track.rotationKeys.reserve(rotationSampleCount);
          const ufbx_node* sourceNode = bakedNode.typed_id < source->nodes.count
            ? source->nodes.data[bakedNode.typed_id] : nullptr;
          const ufbx_node* retainedPivotNode = rawTransformSource &&
              bakedNode.typed_id < rawTransformSource->nodes.count
            ? rawTransformSource->nodes.data[bakedNode.typed_id] : nullptr;
          const ufbx_anim_value* rawRotation = sourceNode
            ? FindSingleRawValue(stack, &sourceNode->element, UFBX_Lcl_Rotation) : nullptr;
          const std::vector<UnityFbxConvertedEulerKey> convertedEulerKeys =
            BuildUnityFbxConvertedEulerKeys(
              rawRotation, sourceNode,
              sourceNode ? sourceNode->rotation_order : UFBX_ROTATION_ORDER_XYZ,
              baked->playback_time_begin, scene->frameRate,
              rotationSampleCount);
          const bool preserveUnrolledZeroSigns =
            UnityFbxPreservesUnrolledQuaternionZeroSigns(rawRotation);
          bool validatedRawRotation = false;
          bool rawRotationTrackCompatible = true;
          size_t bakedRotationCursor = 0;
          for (size_t key = 0; key < rotationSampleCount; ++key) {
            const double sampleTime = UnityFbxSampleTime(
              baked->playback_time_begin, scene->frameRate, key);
            constexpr double ticksPerSecond = 141120000.0;
            const int64_t orderedStartTicks = static_cast<int64_t>(
              std::llround(baked->playback_time_begin * ticksPerSecond));
            const int64_t orderedFrameTicks = static_cast<int64_t>(
              std::llround(ticksPerSecond / static_cast<double>(scene->frameRate)));
            const double orderedConversionTime = static_cast<double>(
              orderedStartTicks + static_cast<int64_t>(key) * orderedFrameTicks) /
              ticksPerSecond;
            ufbx_baked_quat sourceKey{};
            sourceKey.time = sampleTime;
            const double bakedEvaluationTime = sourceNode &&
                sourceNode->rotation_order != UFBX_ROTATION_ORDER_XYZ
              ? orderedConversionTime - baked->playback_time_begin
              : sampleTime - baked->playback_time_begin;
            while (bakedRotationCursor < bakedNode.rotation_keys.count &&
                bakedNode.rotation_keys.data[bakedRotationCursor].time <
                  bakedEvaluationTime - 1e-9)
              ++bakedRotationCursor;
            const bool hasExactBakedSample =
              bakedRotationCursor < bakedNode.rotation_keys.count &&
              std::abs(bakedNode.rotation_keys.data[bakedRotationCursor].time -
                bakedEvaluationTime) <= 1e-9;
            sourceKey.value = ufbx_evaluate_baked_quat(
              bakedNode.rotation_keys, bakedEvaluationTime);
            bool matchedBakedRotation = false;
            AnityModelQuaternionKey rotationKey = UnityQuaternionKey(
              sourceKey, sourceNode, rawRotation, sampleTime,
              orderedConversionTime, scene->frameRate, key,
              &convertedEulerKeys,
              preserveUnrolledZeroSigns,
              !hasExactBakedSample && validatedRawRotation &&
                rawRotationTrackCompatible,
              &matchedBakedRotation);
            if (hasExactBakedSample) {
              validatedRawRotation = true;
              rawRotationTrackCompatible = rawRotationTrackCompatible &&
                matchedBakedRotation;
            }
            rotationKey.time = Real(sampleTime) - Real(baked->playback_time_begin);
            if (!track.rotationKeys.empty()) {
              const AnityModelQuaternionKey& previous = track.rotationKeys.back();
              const float dot = previous.x * rotationKey.x +
                previous.y * rotationKey.y + previous.z * rotationKey.z +
                previous.w * rotationKey.w;
              if (dot < 0.0f) {
                rotationKey.x = -rotationKey.x;
                rotationKey.y = -rotationKey.y;
                rotationKey.z = -rotationKey.z;
                rotationKey.w = -rotationKey.w;
              }
            }
            track.rotationKeys.push_back(rotationKey);
          }
          track.scaleKeys.reserve(bakedNode.scale_keys.count);
          for (size_t key = 0; key < bakedNode.scale_keys.count; ++key)
            track.scaleKeys.push_back(VectorKey(bakedNode.scale_keys.data[key], 1.0f));
          if (options->resampleCurves == 0 && sourceNode)
            BuildUnityRawTransformCurves(stack, sourceNode, &bakedNode,
              retainedPivotStack, retainedPivotNode,
              baked->playback_time_begin, scene->frameRate,
              rotationSampleCount, options->globalScale, track);
          if (!track.positionKeys.empty()) clip.duration = std::max(clip.duration, track.positionKeys.back().time);
          if (!track.rotationKeys.empty()) clip.duration = std::max(clip.duration, track.rotationKeys.back().time);
          if (!track.scaleKeys.empty()) clip.duration = std::max(clip.duration, track.scaleKeys.back().time);
          clip.tracks.push_back(std::move(track));
        }
        if (options->importBlendShapes != 0) {
          for (size_t elementIndex = 0; elementIndex < baked->elements.count; ++elementIndex) {
            const ufbx_baked_element& bakedElement = baked->elements.data[elementIndex];
            if (bakedElement.element_id >= source->elements.count) continue;
            ufbx_element* element = source->elements.data[bakedElement.element_id];
            ufbx_mesh* animatedMesh = ufbx_as_mesh(element);
            if (!animatedMesh || animatedMesh->typed_id >= scene->meshes.size()) continue;
            const Mesh& importedMesh = scene->meshes[animatedMesh->typed_id];
            for (size_t propIndex = 0; propIndex < bakedElement.props.count; ++propIndex) {
              const ufbx_baked_prop& prop = bakedElement.props.data[propIndex];
              if (prop.keys.count == 0 || !HasBlendShape(importedMesh, prop.name)) continue;
              for (size_t instanceIndex = 0; instanceIndex < animatedMesh->instances.count; ++instanceIndex) {
                const ufbx_node* instance = animatedMesh->instances.data[instanceIndex];
                const auto found = nodeMap.find(instance->typed_id);
                if (found == nodeMap.end()) continue;
                BlendShapeTrack track;
                track.nodeIndex = found->second;
                track.name = String(prop.name);
                if (options->resampleCurves == 0 &&
                    BuildRawScalarKeys(stack, element, prop.name, baked->playback_time_begin, track.keys)) {
                  // Preserve source Bezier tangents when Unity's Resample Curves option is disabled.
                } else {
                  const double duration = prop.keys.data[prop.keys.count - 1].time;
                  const int64_t lastFrame = static_cast<int64_t>(std::floor(duration * scene->frameRate + 0.5));
                  track.keys.reserve(static_cast<size_t>(lastFrame + 2));
                  for (int64_t frame = 0; frame <= lastFrame; ++frame) {
                    const double time = static_cast<double>(frame) / scene->frameRate;
                    const ufbx_prop value = ufbx_evaluate_prop_len(stack->anim, element,
                      prop.name.data, prop.name.length, baked->playback_time_begin + time);
                    track.keys.push_back({Real(time), Real(value.value_real), 0.0f, 0.0f});
                  }
                  const double sampledEnd = static_cast<double>(lastFrame) / scene->frameRate;
                  if (duration - sampledEnd > 1e-8) {
                    const ufbx_prop value = ufbx_evaluate_prop_len(stack->anim, element,
                      prop.name.data, prop.name.length, baked->playback_time_begin + duration);
                    track.keys.push_back({Real(duration), Real(value.value_real), 0.0f, 0.0f});
                  }
                  BuildCentralScalarTangents(track.keys);
                }
                if (!track.keys.empty()) clip.duration = std::max(clip.duration, track.keys.back().time);
                clip.blendShapeTracks.push_back(std::move(track));
              }
            }
          }
        }
        if (clip.duration <= 0.0f) clip.duration = Real(baked->playback_duration);
        ufbx_free_baked_anim(baked);
        scene->clips.push_back(std::move(clip));
      }
    }
  } catch (const std::bad_alloc&) {
    result = ANITY_ERR_OUT_OF_MEMORY;
  } catch (...) {
    CopyError(errorBuffer, errorBufferSize, "Unexpected native model importer failure.");
    result = ANITY_ERR_INTERNAL;
  }

  if (rawTransformSource) ufbx_free_scene(rawTransformSource);
  ufbx_free_scene(source);
  if (result != ANITY_OK) {
    delete scene;
    return result;
  }
  *outScene = scene;
  return ANITY_OK;
}

void ANITY_CALL AnityModel_FreeScene(AnityModelScene* scene) { delete scene; }

AnityResult ANITY_CALL AnityModel_GetSceneInfo(const AnityModelScene* scene, AnityModelSceneInfo* outInfo) {
  if (!scene || !outInfo) return ANITY_ERR_INVALID_ARG;
  *outInfo = { static_cast<int32_t>(scene->nodes.size()), static_cast<int32_t>(scene->meshes.size()),
    static_cast<int32_t>(scene->clips.size()), scene->fileScale, scene->frameRate };
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityModel_GetNodeInfo(const AnityModelScene* scene, int32_t nodeIndex, AnityModelNodeInfo* outInfo) {
  if (!scene || !outInfo || nodeIndex < 0 || nodeIndex >= static_cast<int32_t>(scene->nodes.size())) return ANITY_ERR_INVALID_ARG;
  const Node& node = scene->nodes[nodeIndex];
  *outInfo = { node.name.c_str(), node.parentIndex, node.meshIndex,
    Real(node.transform.translation.x), Real(node.transform.translation.y), Real(node.transform.translation.z),
    Real(node.transform.rotation.x), Real(node.transform.rotation.y), Real(node.transform.rotation.z), Real(node.transform.rotation.w),
    Real(node.transform.scale.x), Real(node.transform.scale.y), Real(node.transform.scale.z) };
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityModel_GetMeshInfo(const AnityModelScene* scene, int32_t meshIndex, AnityModelMeshInfo* outInfo) {
  if (!scene || !outInfo || meshIndex < 0 || meshIndex >= static_cast<int32_t>(scene->meshes.size())) return ANITY_ERR_INVALID_ARG;
  const Mesh& mesh = scene->meshes[meshIndex];
  *outInfo = { mesh.name.c_str(), static_cast<int32_t>(mesh.vertices.size()), static_cast<int32_t>(mesh.indices.size()),
    static_cast<int32_t>(mesh.subMeshes.size()), static_cast<int32_t>(mesh.bones.size()),
    static_cast<int32_t>(mesh.boneWeights.size()), static_cast<int32_t>(mesh.blendShapes.size()) };
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityModel_CopyMeshVertices(const AnityModelScene* scene, int32_t meshIndex, AnityModelVertex* vertices, int32_t capacity) {
  if (!scene || meshIndex < 0 || meshIndex >= static_cast<int32_t>(scene->meshes.size())) return ANITY_ERR_INVALID_ARG;
  return CopyVector(scene->meshes[meshIndex].vertices, vertices, capacity);
}

AnityResult ANITY_CALL AnityModel_CopyMeshIndices(const AnityModelScene* scene, int32_t meshIndex, uint32_t* indices, int32_t capacity) {
  if (!scene || meshIndex < 0 || meshIndex >= static_cast<int32_t>(scene->meshes.size())) return ANITY_ERR_INVALID_ARG;
  return CopyVector(scene->meshes[meshIndex].indices, indices, capacity);
}

AnityResult ANITY_CALL AnityModel_GetSubMeshInfo(const AnityModelScene* scene, int32_t meshIndex, int32_t subMeshIndex, AnityModelSubMeshInfo* outInfo) {
  if (!scene || !outInfo || meshIndex < 0 || meshIndex >= static_cast<int32_t>(scene->meshes.size())) return ANITY_ERR_INVALID_ARG;
  const Mesh& mesh = scene->meshes[meshIndex];
  if (subMeshIndex < 0 || subMeshIndex >= static_cast<int32_t>(mesh.subMeshes.size())) return ANITY_ERR_INVALID_ARG;
  const SubMesh& subMesh = mesh.subMeshes[subMeshIndex];
  *outInfo = { subMesh.indexStart, subMesh.indexCount, subMesh.materialIndex };
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityModel_GetBoneInfo(const AnityModelScene* scene, int32_t meshIndex, int32_t boneIndex, AnityModelBoneInfo* outInfo) {
  if (!scene || !outInfo || meshIndex < 0 || meshIndex >= static_cast<int32_t>(scene->meshes.size())) return ANITY_ERR_INVALID_ARG;
  const Mesh& mesh = scene->meshes[meshIndex];
  if (boneIndex < 0 || boneIndex >= static_cast<int32_t>(mesh.bones.size())) return ANITY_ERR_INVALID_ARG;
  const Bone& bone = mesh.bones[boneIndex];
  const ufbx_matrix& m = bone.bindpose;
  *outInfo = {bone.name.c_str(), bone.nodeIndex,
    Real(m.m00), Real(m.m01), Real(m.m02), Real(m.m03),
    Real(m.m10), Real(m.m11), Real(m.m12), Real(m.m13),
    Real(m.m20), Real(m.m21), Real(m.m22), Real(m.m23),
    0.0f, 0.0f, 0.0f, 1.0f};
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityModel_CopySkinVertices(const AnityModelScene* scene, int32_t meshIndex, AnityModelSkinVertexInfo* vertices, int32_t capacity) {
  if (!scene || meshIndex < 0 || meshIndex >= static_cast<int32_t>(scene->meshes.size())) return ANITY_ERR_INVALID_ARG;
  return CopyVector(scene->meshes[meshIndex].skinVertices, vertices, capacity);
}

AnityResult ANITY_CALL AnityModel_CopyBoneWeights(const AnityModelScene* scene, int32_t meshIndex, AnityModelBoneWeight* weights, int32_t capacity) {
  if (!scene || meshIndex < 0 || meshIndex >= static_cast<int32_t>(scene->meshes.size())) return ANITY_ERR_INVALID_ARG;
  return CopyVector(scene->meshes[meshIndex].boneWeights, weights, capacity);
}

AnityResult ANITY_CALL AnityModel_GetBlendShapeInfo(const AnityModelScene* scene, int32_t meshIndex, int32_t shapeIndex, AnityModelBlendShapeInfo* outInfo) {
  if (!scene || !outInfo || meshIndex < 0 || meshIndex >= static_cast<int32_t>(scene->meshes.size())) return ANITY_ERR_INVALID_ARG;
  const Mesh& mesh = scene->meshes[meshIndex];
  if (shapeIndex < 0 || shapeIndex >= static_cast<int32_t>(mesh.blendShapes.size())) return ANITY_ERR_INVALID_ARG;
  const BlendShape& shape = mesh.blendShapes[shapeIndex];
  *outInfo = {shape.name.c_str(), static_cast<int32_t>(shape.frames.size())};
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityModel_GetBlendShapeFrameInfo(const AnityModelScene* scene, int32_t meshIndex, int32_t shapeIndex, int32_t frameIndex, AnityModelBlendShapeFrameInfo* outInfo) {
  if (!scene || !outInfo || meshIndex < 0 || meshIndex >= static_cast<int32_t>(scene->meshes.size())) return ANITY_ERR_INVALID_ARG;
  const Mesh& mesh = scene->meshes[meshIndex];
  if (shapeIndex < 0 || shapeIndex >= static_cast<int32_t>(mesh.blendShapes.size())) return ANITY_ERR_INVALID_ARG;
  const BlendShape& shape = mesh.blendShapes[shapeIndex];
  if (frameIndex < 0 || frameIndex >= static_cast<int32_t>(shape.frames.size())) return ANITY_ERR_INVALID_ARG;
  *outInfo = {shape.frames[frameIndex].weight};
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityModel_CopyBlendShapeFrameDeltas(const AnityModelScene* scene, int32_t meshIndex, int32_t shapeIndex, int32_t frameIndex, AnityModelBlendShapeDelta* deltas, int32_t capacity) {
  if (!scene || meshIndex < 0 || meshIndex >= static_cast<int32_t>(scene->meshes.size())) return ANITY_ERR_INVALID_ARG;
  const Mesh& mesh = scene->meshes[meshIndex];
  if (shapeIndex < 0 || shapeIndex >= static_cast<int32_t>(mesh.blendShapes.size())) return ANITY_ERR_INVALID_ARG;
  const BlendShape& shape = mesh.blendShapes[shapeIndex];
  if (frameIndex < 0 || frameIndex >= static_cast<int32_t>(shape.frames.size())) return ANITY_ERR_INVALID_ARG;
  return CopyVector(shape.frames[frameIndex].deltas, deltas, capacity);
}

AnityResult ANITY_CALL AnityModel_GetClipInfo(const AnityModelScene* scene, int32_t clipIndex, AnityModelClipInfo* outInfo) {
  if (!scene || !outInfo || clipIndex < 0 || clipIndex >= static_cast<int32_t>(scene->clips.size())) return ANITY_ERR_INVALID_ARG;
  const Clip& clip = scene->clips[clipIndex];
  *outInfo = { clip.name.c_str(), clip.duration, clip.frameRate, static_cast<int32_t>(clip.tracks.size()),
    static_cast<int32_t>(clip.blendShapeTracks.size()), clip.firstFrame, clip.lastFrame };
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityModel_GetTrackInfo(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelTrackInfo* outInfo) {
  if (!scene || !outInfo || clipIndex < 0 || clipIndex >= static_cast<int32_t>(scene->clips.size())) return ANITY_ERR_INVALID_ARG;
  const Clip& clip = scene->clips[clipIndex];
  if (trackIndex < 0 || trackIndex >= static_cast<int32_t>(clip.tracks.size())) return ANITY_ERR_INVALID_ARG;
  const Track& track = clip.tracks[trackIndex];
  *outInfo = { track.nodeIndex, static_cast<int32_t>(track.positionKeys.size()),
    static_cast<int32_t>(track.rotationKeys.size()), static_cast<int32_t>(track.scaleKeys.size()),
    static_cast<int32_t>(track.transformCurves.size()) };
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityModel_CopyPositionKeys(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelVectorKey* keys, int32_t capacity) {
  if (!scene || clipIndex < 0 || clipIndex >= static_cast<int32_t>(scene->clips.size())) return ANITY_ERR_INVALID_ARG;
  const Clip& clip = scene->clips[clipIndex];
  if (trackIndex < 0 || trackIndex >= static_cast<int32_t>(clip.tracks.size())) return ANITY_ERR_INVALID_ARG;
  return CopyVector(clip.tracks[trackIndex].positionKeys, keys, capacity);
}

AnityResult ANITY_CALL AnityModel_CopyRotationKeys(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelQuaternionKey* keys, int32_t capacity) {
  if (!scene || clipIndex < 0 || clipIndex >= static_cast<int32_t>(scene->clips.size())) return ANITY_ERR_INVALID_ARG;
  const Clip& clip = scene->clips[clipIndex];
  if (trackIndex < 0 || trackIndex >= static_cast<int32_t>(clip.tracks.size())) return ANITY_ERR_INVALID_ARG;
  return CopyVector(clip.tracks[trackIndex].rotationKeys, keys, capacity);
}

AnityResult ANITY_CALL AnityModel_CopyScaleKeys(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelVectorKey* keys, int32_t capacity) {
  if (!scene || clipIndex < 0 || clipIndex >= static_cast<int32_t>(scene->clips.size())) return ANITY_ERR_INVALID_ARG;
  const Clip& clip = scene->clips[clipIndex];
  if (trackIndex < 0 || trackIndex >= static_cast<int32_t>(clip.tracks.size())) return ANITY_ERR_INVALID_ARG;
  return CopyVector(clip.tracks[trackIndex].scaleKeys, keys, capacity);
}

AnityResult ANITY_CALL AnityModel_GetTransformCurveInfo(const AnityModelScene* scene,
    int32_t clipIndex, int32_t trackIndex, int32_t curveIndex, AnityModelTransformCurveInfo* outInfo) {
  if (!scene || !outInfo || clipIndex < 0 || clipIndex >= static_cast<int32_t>(scene->clips.size())) return ANITY_ERR_INVALID_ARG;
  const Clip& clip = scene->clips[clipIndex];
  if (trackIndex < 0 || trackIndex >= static_cast<int32_t>(clip.tracks.size())) return ANITY_ERR_INVALID_ARG;
  const Track& track = clip.tracks[trackIndex];
  if (curveIndex < 0 || curveIndex >= static_cast<int32_t>(track.transformCurves.size())) return ANITY_ERR_INVALID_ARG;
  const Track::TransformCurve& curve = track.transformCurves[curveIndex];
  *outInfo = {static_cast<int32_t>(curve.property), static_cast<int32_t>(curve.keys.size())};
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityModel_CopyTransformCurveKeys(const AnityModelScene* scene,
    int32_t clipIndex, int32_t trackIndex, int32_t curveIndex, AnityModelScalarKey* keys, int32_t capacity) {
  if (!scene || clipIndex < 0 || clipIndex >= static_cast<int32_t>(scene->clips.size())) return ANITY_ERR_INVALID_ARG;
  const Clip& clip = scene->clips[clipIndex];
  if (trackIndex < 0 || trackIndex >= static_cast<int32_t>(clip.tracks.size())) return ANITY_ERR_INVALID_ARG;
  const Track& track = clip.tracks[trackIndex];
  if (curveIndex < 0 || curveIndex >= static_cast<int32_t>(track.transformCurves.size())) return ANITY_ERR_INVALID_ARG;
  return CopyVector(track.transformCurves[curveIndex].keys, keys, capacity);
}

AnityResult ANITY_CALL AnityModel_GetBlendShapeTrackInfo(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelBlendShapeTrackInfo* outInfo) {
  if (!scene || !outInfo || clipIndex < 0 || clipIndex >= static_cast<int32_t>(scene->clips.size())) return ANITY_ERR_INVALID_ARG;
  const Clip& clip = scene->clips[clipIndex];
  if (trackIndex < 0 || trackIndex >= static_cast<int32_t>(clip.blendShapeTracks.size())) return ANITY_ERR_INVALID_ARG;
  const BlendShapeTrack& track = clip.blendShapeTracks[trackIndex];
  *outInfo = {track.nodeIndex, track.name.c_str(), static_cast<int32_t>(track.keys.size())};
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityModel_CopyBlendShapeKeys(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelScalarKey* keys, int32_t capacity) {
  if (!scene || clipIndex < 0 || clipIndex >= static_cast<int32_t>(scene->clips.size())) return ANITY_ERR_INVALID_ARG;
  const Clip& clip = scene->clips[clipIndex];
  if (trackIndex < 0 || trackIndex >= static_cast<int32_t>(clip.blendShapeTracks.size())) return ANITY_ERR_INVALID_ARG;
  return CopyVector(clip.blendShapeTracks[trackIndex].keys, keys, capacity);
}

} // extern "C"
