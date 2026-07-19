#define ANITY_NATIVE_BUILD
#include "anity/model/anity_model_import.h"
#include "ufbx.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
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

static AnityModelVertex ReadVertex(const ufbx_mesh* mesh, uint32_t index, float scale) {
  AnityModelVertex vertex{};
  const ufbx_vec3 position = ufbx_get_vertex_vec3(&mesh->vertex_position, index);
  const ufbx_vec3 normal = mesh->vertex_normal.exists
    ? ufbx_get_vertex_vec3(&mesh->vertex_normal, index) : ufbx_vec3{0.0, 1.0, 0.0};
  const ufbx_vec2 uv = mesh->vertex_uv.exists
    ? ufbx_get_vertex_vec2(&mesh->vertex_uv, index) : ufbx_vec2{0.0, 0.0};
  vertex.positionX = Real(position.x * scale);
  vertex.positionY = Real(position.y * scale);
  vertex.positionZ = Real(position.z * scale);
  vertex.normalX = Real(normal.x);
  vertex.normalY = Real(normal.y);
  vertex.normalZ = Real(normal.z);
  vertex.uvX = Real(uv.x);
  vertex.uvY = Real(uv.y);
  if (mesh->vertex_tangent.exists) {
    const ufbx_vec3 tangent = ufbx_get_vertex_vec3(&mesh->vertex_tangent, index);
    vertex.tangentX = Real(tangent.x);
    vertex.tangentY = Real(tangent.y);
    vertex.tangentZ = Real(tangent.z);
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
        flatVertices.push_back(ReadVertex(source, triangleIndices[index], scale));
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
            frame.deltas[vertexIndex] = {Real(p.x * scale), Real(p.y * scale), Real(p.z * scale),
              Real(n.x), Real(n.y), Real(n.z)};
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

static double EvaluateUnityCompatibleCurve(
    const ufbx_anim_curve* curve, double time, double defaultValue) {
  if (!curve || curve->keyframes.count < 2 ||
      time < curve->keyframes.data[0].time ||
      time > curve->keyframes.data[curve->keyframes.count - 1].time)
    return ufbx_evaluate_curve(curve, time, defaultValue);

  const ufbx_keyframe* left = curve->keyframes.data;
  const ufbx_keyframe* right = left + 1;
  for (size_t index = 1; index < curve->keyframes.count; ++index) {
    right = curve->keyframes.data + index;
    left = right - 1;
    if (time <= right->time) break;
  }
  if (time == left->time) return left->value;
  if (time == right->time) return right->value;
  if (left->interpolation != UFBX_INTERPOLATION_CUBIC)
    return ufbx_evaluate_curve(curve, time, defaultValue);

  const double duration = right->time - left->time;
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

  const double sourceT = (time - left->time) / duration;
  const float durationFloat = static_cast<float>(duration);
  const float outSlope = std::abs(left->right.dx) > 1e-12
    ? left->right.dy / left->right.dx : 0.0f;
  const float inSlope = std::abs(right->left.dx) > 1e-12
    ? right->left.dy / right->left.dx : 0.0f;
  const float outHandle = outSlope * durationFloat / 3.0f;
  const float inHandle = inSlope * durationFloat / 3.0f;
  const float p0 = Real(left->value);
  const float p1 = p0 + outHandle;
  const float p3 = Real(right->value);
  const float p2 = p3 - inHandle;
  const auto evaluateStage = [sourceT](float from, float to) {
    // FBX SDK's unweighted KFCurve::EvaluateIndex() subtracts in float,
    // performs the multiply/add with its double-precision time parameter, and
    // rounds every De Casteljau stage back to float.
    const float difference = to - from;
    return static_cast<float>(
      sourceT * static_cast<double>(difference) + static_cast<double>(from));
  };
  const float a = evaluateStage(p0, p1);
  const float b = evaluateStage(p1, p2);
  const float c = evaluateStage(p2, p3);
  return evaluateStage(evaluateStage(a, b), evaluateStage(b, c));
}

static ufbx_quat UnityFbxXyzEulerToQuaternion(ufbx_vec3 euler) {
  constexpr double degreesToRadians = 3.14159265358979323846264338327950288 / 180.0;
  const double rx = euler.x * degreesToRadians;
  const double ry = euler.y * degreesToRadians;
  const double rz = euler.z * degreesToRadians;
  const double sx = std::sin(rx), cx = std::cos(rx);
  const double sy = std::sin(ry), cy = std::cos(ry);
  const double sz = std::sin(rz), cz = std::cos(rz);

  // FbxAMatrix::SetROnly() uses row-vector XYZ matrices. Reconstructing its
  // matrix before GetQ() preserves the rounding Unity receives from FBX SDK;
  // the direct closed-form Euler quaternion differs by a few float ULPs.
  const double m00 = cy * cz;
  const double m01 = cy * sz;
  const double m02 = -sy;
  const double m10 = cz * sy * sx - sz * cx;
  const double m11 = sz * sy * sx + cz * cx;
  const double m12 = cy * sx;
  const double m20 = cz * sy * cx + sz * sx;
  const double m21 = sz * sy * cx - cz * sx;
  const double m22 = cy * cx;

  ufbx_quat result{};
  const double trace = m00 + m11 + m22;
  if (trace > 0.0) {
    const double scale = std::sqrt(trace + 1.0) * 2.0;
    result.x = (m12 - m21) / scale;
    result.y = (m20 - m02) / scale;
    result.z = (m01 - m10) / scale;
    result.w = 0.25 * scale;
  } else if (m00 > m11 && m00 > m22) {
    const double scale = std::sqrt(1.0 + m00 - m11 - m22) * 2.0;
    result.x = 0.25 * scale;
    result.y = (m01 + m10) / scale;
    result.z = (m02 + m20) / scale;
    result.w = (m12 - m21) / scale;
  } else if (m11 > m22) {
    const double scale = std::sqrt(1.0 + m11 - m00 - m22) * 2.0;
    result.x = (m01 + m10) / scale;
    result.y = 0.25 * scale;
    result.z = (m12 + m21) / scale;
    result.w = (m20 - m02) / scale;
  } else {
    const double scale = std::sqrt(1.0 + m22 - m00 - m11) * 2.0;
    result.x = (m02 + m20) / scale;
    result.y = (m12 + m21) / scale;
    result.z = 0.25 * scale;
    result.w = (m01 - m10) / scale;
  }
  return result;
}

static double UnityFbxSampleTime(
    double playbackTimeBegin, float frameRate, size_t sampleIndex) {
  // Unity creates resampling times through FBX's legacy KTime path. The frame
  // number and seconds-per-frame multiplication happen in float, after which
  // KTime truncates to its 141120000-tick second. Keeping that tick grid is
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

static AnityModelQuaternionKey UnityQuaternionKey(const ufbx_baked_quat& source,
    const ufbx_node* node, const ufbx_anim_value* rawRotation,
    double absoluteTime) {
  const AnityModelQuaternionKey fallback = QuaternionKey(source, node);
  if (!node || !rawRotation || node->rotation_order != UFBX_ROTATION_ORDER_XYZ) return fallback;

  ufbx_vec3 euler = rawRotation->default_value;
  ufbx_vec3 referenceEuler = rawRotation->default_value;
  // Unity's FBX path evaluates each scalar curve to float before passing the
  // values back to the double-precision FBX matrix/quaternion conversion.
  euler.x = static_cast<float>(EvaluateUnityCompatibleCurve(rawRotation->curves[0], absoluteTime, euler.x));
  euler.y = static_cast<float>(EvaluateUnityCompatibleCurve(rawRotation->curves[1], absoluteTime, euler.y));
  euler.z = static_cast<float>(EvaluateUnityCompatibleCurve(rawRotation->curves[2], absoluteTime, euler.z));
  referenceEuler.x = ufbx_evaluate_curve(rawRotation->curves[0], absoluteTime, referenceEuler.x);
  referenceEuler.y = ufbx_evaluate_curve(rawRotation->curves[1], absoluteTime, referenceEuler.y);
  referenceEuler.z = ufbx_evaluate_curve(rawRotation->curves[2], absoluteTime, referenceEuler.z);
  const ufbx_quat compatibleRaw = UnityFbxXyzEulerToQuaternion(euler);
  const ufbx_quat referenceRaw = ufbx_euler_to_quat(referenceEuler, node->rotation_order);
  const ufbx_quat inverseAdjustment = {
    -node->adjust_pre_rotation.x, -node->adjust_pre_rotation.y,
    -node->adjust_pre_rotation.z, node->adjust_pre_rotation.w,
  };
  // Conjugate into Unity's imported coordinate basis without a double-precision
  // normalization. Unity converts the FBX quaternion to float first and only
  // then normalizes it below.
  const bool xAxisBasis = std::abs(std::abs(node->adjust_pre_rotation.x) - 1.0) < 1e-9 &&
    std::abs(node->adjust_pre_rotation.y) < 1e-9 &&
    std::abs(node->adjust_pre_rotation.z) < 1e-9 &&
    std::abs(node->adjust_pre_rotation.w) < 1e-9;
  const ufbx_quat compatible = xAxisBasis
    ? UnityFbxXyzEulerToQuaternion({euler.x, -euler.y, -euler.z})
    : ufbx_quat_mul(
        ufbx_quat_mul(node->adjust_pre_rotation, compatibleRaw), inverseAdjustment);
  const ufbx_quat reference = ufbx_quat_mul(
    ufbx_quat_mul(node->adjust_pre_rotation, referenceRaw), inverseAdjustment);

  // Only replace a baked value when it matches the same raw Euler rotation
  // after coordinate conversion. Complex FBX pre/post rotations, pivots,
  // layers, and helper transforms retain the baked path.
  const double dx = static_cast<double>(fallback.x) - reference.x;
  const double dy = static_cast<double>(fallback.y) - reference.y;
  const double dz = static_cast<double>(fallback.z) - reference.z;
  const double dw = static_cast<double>(fallback.w) - reference.w;
  if (dx * dx + dy * dy + dz * dz + dw * dw > 1e-10) return fallback;
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
  return { Real(source.time), x, y, z, w };
}

static double WrapAngleNear(double value, double reference) {
  while (value - reference > 180.0) value -= 360.0;
  while (value - reference < -180.0) value += 360.0;
  return value;
}

static float RawInTangent(const ufbx_anim_curve* curve, size_t index);
static float RawOutTangent(const ufbx_anim_curve* curve, size_t index);

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

static void BuildRawTransformCurves(const ufbx_anim_stack* stack, const ufbx_node* sourceNode,
    const ufbx_baked_node* bakedNode,
    double trimStart, float globalScale, Track& track) {
  struct PropertyGroup {
    const char* name;
    AnityModelTransformCurveProperty first;
  };
  const PropertyGroup groups[] = {
    {UFBX_Lcl_Translation, ANITY_MODEL_POSITION_X},
    {UFBX_Lcl_Rotation, ANITY_MODEL_EULER_X},
    {UFBX_Lcl_Scaling, ANITY_MODEL_SCALE_X},
  };
  for (const PropertyGroup& group : groups) {
    const ufbx_anim_value* value = FindSingleRawValue(stack, &sourceNode->element, group.name);
    if (!value) continue;
    for (int axis = 0; axis < 3; ++axis)
      BuildRawTransformCurve(bakedNode, value->curves[axis],
        static_cast<AnityModelTransformCurveProperty>(static_cast<int>(group.first) + axis),
        trimStart, globalScale, track);
  }
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

  AnityModelScene* scene = new (std::nothrow) AnityModelScene();
  if (!scene) {
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
          track.rotationKeys.reserve(bakedNode.rotation_keys.count);
          const ufbx_node* sourceNode = bakedNode.typed_id < source->nodes.count
            ? source->nodes.data[bakedNode.typed_id] : nullptr;
          const ufbx_anim_value* rawRotation = sourceNode
            ? FindSingleRawValue(stack, &sourceNode->element, UFBX_Lcl_Rotation) : nullptr;
          for (size_t key = 0; key < bakedNode.rotation_keys.count; ++key) {
            const ufbx_baked_quat& sourceKey = bakedNode.rotation_keys.data[key];
            const double sampleTime = UnityFbxSampleTime(
              baked->playback_time_begin, scene->frameRate, key);
            AnityModelQuaternionKey rotationKey = UnityQuaternionKey(
              sourceKey, sourceNode, rawRotation, sampleTime);
            rotationKey.time = Real(sampleTime) - Real(baked->playback_time_begin);
            track.rotationKeys.push_back(rotationKey);
          }
          track.scaleKeys.reserve(bakedNode.scale_keys.count);
          for (size_t key = 0; key < bakedNode.scale_keys.count; ++key)
            track.scaleKeys.push_back(VectorKey(bakedNode.scale_keys.data[key], 1.0f));
          if (options->resampleCurves == 0 && sourceNode)
            BuildRawTransformCurves(stack, sourceNode, &bakedNode,
              baked->playback_time_begin, options->globalScale, track);
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
