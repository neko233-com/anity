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

struct Mesh {
  std::string name;
  std::vector<AnityModelVertex> vertices;
  std::vector<uint32_t> indices;
  std::vector<SubMesh> subMeshes;
};

struct Track {
  int32_t nodeIndex = -1;
  std::vector<AnityModelVectorKey> positionKeys;
  std::vector<AnityModelQuaternionKey> rotationKeys;
  std::vector<AnityModelVectorKey> scaleKeys;
};

struct Clip {
  std::string name;
  float duration = 0.0f;
  float frameRate = 30.0f;
  std::vector<Track> tracks;
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

static bool BuildMesh(const ufbx_mesh* source, float scale, Mesh& destination, std::string& error) {
  destination.name = String(source->name);
  if (source->num_triangles > static_cast<size_t>(std::numeric_limits<int32_t>::max() / 3)) {
    error = "Model mesh index count exceeds the supported range.";
    return false;
  }

  std::vector<uint32_t> triangleIndices(std::max<size_t>(source->max_face_triangles * 3, 3));
  std::vector<AnityModelVertex> flatVertices;
  flatVertices.reserve(source->num_triangles * 3);

  const size_t partCount = source->material_parts.count;
  for (size_t partIndex = 0; partIndex < partCount; ++partIndex) {
    const ufbx_mesh_part& part = source->material_parts.data[partIndex];
    const size_t begin = flatVertices.size();
    for (size_t faceIndex = 0; faceIndex < part.num_faces; ++faceIndex) {
      const uint32_t meshFaceIndex = part.face_indices.data[faceIndex];
      if (meshFaceIndex >= source->faces.count) continue;
      const size_t triangleCount = ufbx_triangulate_face(
        triangleIndices.data(), triangleIndices.size(), source, source->faces.data[meshFaceIndex]);
      for (size_t index = 0; index < triangleCount * 3; ++index)
        flatVertices.push_back(ReadVertex(source, triangleIndices[index], scale));
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
    ufbx_vertex_stream stream{};
    stream.data = flatVertices.data();
    stream.vertex_count = flatVertices.size();
    stream.vertex_size = sizeof(AnityModelVertex);
    ufbx_error indexError{};
    const size_t vertexCount = ufbx_generate_indices(
      &stream, 1, destination.indices.data(), destination.indices.size(), nullptr, &indexError);
    if (indexError.type != UFBX_ERROR_NONE) {
      char buffer[512]{};
      ufbx_format_error(buffer, sizeof(buffer), &indexError);
      error = buffer;
      return false;
    }
    flatVertices.resize(vertexCount);
  }
  destination.vertices = std::move(flatVertices);
  if (destination.subMeshes.empty()) destination.subMeshes.push_back({0, 0, -1});
  return true;
}

static AnityModelVectorKey VectorKey(const ufbx_baked_vec3& source, float scale) {
  return { Real(source.time), Real(source.value.x * scale), Real(source.value.y * scale), Real(source.value.z * scale) };
}

static AnityModelQuaternionKey QuaternionKey(const ufbx_baked_quat& source) {
  return { Real(source.time), Real(source.value.x), Real(source.value.y), Real(source.value.z), Real(source.value.w) };
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

  ufbx_load_opts loadOptions{};
  loadOptions.target_axes = ufbx_axes_left_handed_y_up;
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
    scene->meshes.resize(source->meshes.count);
    std::string buildError;
    for (size_t index = 0; index < source->meshes.count; ++index) {
      if (!BuildMesh(source->meshes.data[index], options->globalScale, scene->meshes[index], buildError)) {
        CopyError(errorBuffer, errorBufferSize, buildError.c_str());
        result = ANITY_ERR_DECODE;
        break;
      }
    }

    std::unordered_map<uint32_t, int32_t> nodeMap;
    if (result == ANITY_OK) {
      scene->nodes.reserve(source->nodes.count > 0 ? source->nodes.count - 1 : 0);
      for (size_t index = 0; index < source->nodes.count; ++index) {
        const ufbx_node* node = source->nodes.data[index];
        if (node == source->root_node) continue;
        nodeMap[node->typed_id] = static_cast<int32_t>(scene->nodes.size());
        Node destination;
        destination.name = String(node->name);
        destination.meshIndex = node->mesh ? static_cast<int32_t>(node->mesh->typed_id) : -1;
        destination.transform = node->local_transform;
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
    }

    if (result == ANITY_OK && options->importAnimation) {
      for (size_t clipIndex = 0; clipIndex < source->anim_stacks.count; ++clipIndex) {
        const ufbx_anim_stack* stack = source->anim_stacks.data[clipIndex];
        ufbx_bake_opts bakeOptions{};
        bakeOptions.trim_start_time = true;
        bakeOptions.resample_rate = scene->frameRate;
        bakeOptions.maximum_sample_rate = std::max<double>(scene->frameRate, 1.0);
        bakeOptions.key_reduction_enabled = true;
        bakeOptions.key_reduction_rotation = true;
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
        clip.duration = Real(baked->playback_duration);
        clip.frameRate = scene->frameRate;
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
          for (size_t key = 0; key < bakedNode.rotation_keys.count; ++key)
            track.rotationKeys.push_back(QuaternionKey(bakedNode.rotation_keys.data[key]));
          track.scaleKeys.reserve(bakedNode.scale_keys.count);
          for (size_t key = 0; key < bakedNode.scale_keys.count; ++key)
            track.scaleKeys.push_back(VectorKey(bakedNode.scale_keys.data[key], 1.0f));
          clip.tracks.push_back(std::move(track));
        }
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
  *outInfo = { mesh.name.c_str(), static_cast<int32_t>(mesh.vertices.size()), static_cast<int32_t>(mesh.indices.size()), static_cast<int32_t>(mesh.subMeshes.size()) };
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

AnityResult ANITY_CALL AnityModel_GetClipInfo(const AnityModelScene* scene, int32_t clipIndex, AnityModelClipInfo* outInfo) {
  if (!scene || !outInfo || clipIndex < 0 || clipIndex >= static_cast<int32_t>(scene->clips.size())) return ANITY_ERR_INVALID_ARG;
  const Clip& clip = scene->clips[clipIndex];
  *outInfo = { clip.name.c_str(), clip.duration, clip.frameRate, static_cast<int32_t>(clip.tracks.size()) };
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityModel_GetTrackInfo(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelTrackInfo* outInfo) {
  if (!scene || !outInfo || clipIndex < 0 || clipIndex >= static_cast<int32_t>(scene->clips.size())) return ANITY_ERR_INVALID_ARG;
  const Clip& clip = scene->clips[clipIndex];
  if (trackIndex < 0 || trackIndex >= static_cast<int32_t>(clip.tracks.size())) return ANITY_ERR_INVALID_ARG;
  const Track& track = clip.tracks[trackIndex];
  *outInfo = { track.nodeIndex, static_cast<int32_t>(track.positionKeys.size()),
    static_cast<int32_t>(track.rotationKeys.size()), static_cast<int32_t>(track.scaleKeys.size()) };
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

} // extern "C"
