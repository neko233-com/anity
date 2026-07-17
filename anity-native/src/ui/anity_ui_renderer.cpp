#define ANITY_NATIVE_BUILD
#include "anity/ui/anity_ui_renderer.h"
#include "anity_ui_renderer_internal.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <mutex>
#include <new>
#include <vector>

struct AnityUICommandStorage {
  AnityUIRenderCommandDesc desc{};
  uint64_t insertionOrder = 0;
  std::vector<AnityUIPackedVertex> vertices;
  std::vector<uint32_t> indices;
};

struct AnityUIBatchStorage {
  AnityUIBatchInfo info{};
  AnityUIRenderCommandDesc key{};
  std::vector<AnityUIPackedVertex> vertices;
  std::vector<uint32_t> indices;
};

struct AnityUICanvas {
  mutable std::mutex mutex;
  uint64_t frameId = 0;
  uint64_t generation = 0;
  uint64_t insertionSequence = 0;
  std::vector<AnityUICommandStorage> commands;
  std::vector<AnityUIBatchStorage> batches;
};

namespace {

AnityUIBounds EmptyBounds() {
  return {};
}

bool IsFinite(const AnityUIVector3& value) {
  return std::isfinite(value.x) && std::isfinite(value.y) && std::isfinite(value.z);
}

bool IsFinite(const AnityUIRenderCommandDesc& value) {
  return std::isfinite(value.clipXMin) && std::isfinite(value.clipYMin) &&
      std::isfinite(value.clipXMax) && std::isfinite(value.clipYMax) &&
      std::isfinite(value.softnessX) && std::isfinite(value.softnessY) &&
      std::isfinite(value.effectiveAlpha);
}

bool IsVisible(const AnityUICommandStorage& command) {
  return (command.desc.flags & ANITY_UI_COMMAND_VISIBLE) != 0 &&
      command.desc.effectiveAlpha > 0.001f && !command.vertices.empty() &&
      !command.indices.empty();
}

bool SameBatchKey(const AnityUIRenderCommandDesc& left,
                  const AnityUIRenderCommandDesc& right) {
  const uint32_t stateFlags = ANITY_UI_COMMAND_RECT_CLIP |
      ANITY_UI_COMMAND_MASK | ANITY_UI_COMMAND_POP;
  if (left.materialId != right.materialId || left.textureId != right.textureId ||
      left.alphaTextureId != right.alphaTextureId ||
      (left.flags & stateFlags) != (right.flags & stateFlags)) {
    return false;
  }
  if ((left.flags & (ANITY_UI_COMMAND_MASK | ANITY_UI_COMMAND_POP)) != 0)
    return false;
  if ((left.flags & ANITY_UI_COMMAND_RECT_CLIP) == 0)
    return true;
  return left.clipXMin == right.clipXMin && left.clipYMin == right.clipYMin &&
      left.clipXMax == right.clipXMax && left.clipYMax == right.clipYMax &&
      left.softnessX == right.softnessX && left.softnessY == right.softnessY;
}

AnityResult AppendCommandToBatch(const AnityUICommandStorage& command,
                                 AnityUIBatchStorage& batch) {
  if (batch.vertices.size() > std::numeric_limits<uint32_t>::max() -
          command.vertices.size())
    return ANITY_ERR_OUT_OF_MEMORY;
  const uint32_t vertexOffset = static_cast<uint32_t>(batch.vertices.size());
  batch.vertices.insert(batch.vertices.end(), command.vertices.begin(), command.vertices.end());
  batch.indices.reserve(batch.indices.size() + command.indices.size());
  for (uint32_t index : command.indices) {
    if (index > std::numeric_limits<uint32_t>::max() - vertexOffset)
      return ANITY_ERR_INVALID_ARG;
    batch.indices.push_back(index + vertexOffset);
  }
  batch.info.commandCount++;
  batch.info.lastSortDepth = command.desc.sortDepth;
  batch.info.vertexCount = static_cast<int32_t>(batch.vertices.size());
  batch.info.indexCount = static_cast<int32_t>(batch.indices.size());
  return ANITY_OK;
}

AnityResult BuildBatchesLocked(AnityUICanvas* canvas) {
  canvas->batches.clear();
  try {
    std::vector<const AnityUICommandStorage*> sorted;
    sorted.reserve(canvas->commands.size());
    for (const AnityUICommandStorage& command : canvas->commands)
      if (IsVisible(command)) sorted.push_back(&command);
    std::stable_sort(sorted.begin(), sorted.end(),
        [](const AnityUICommandStorage* left, const AnityUICommandStorage* right) {
          if (left->desc.sortDepth != right->desc.sortDepth)
            return left->desc.sortDepth < right->desc.sortDepth;
          return left->insertionOrder < right->insertionOrder;
        });
    for (const AnityUICommandStorage* command : sorted) {
      if (canvas->batches.empty() ||
          !SameBatchKey(canvas->batches.back().key, command->desc)) {
        AnityUIBatchStorage batch;
        batch.key = command->desc;
        batch.info.materialId = command->desc.materialId;
        batch.info.textureId = command->desc.textureId;
        batch.info.alphaTextureId = command->desc.alphaTextureId;
        batch.info.firstSortDepth = command->desc.sortDepth;
        batch.info.lastSortDepth = command->desc.sortDepth;
        batch.info.flags = command->desc.flags;
        canvas->batches.push_back(std::move(batch));
      }
      AnityResult result = AppendCommandToBatch(*command, canvas->batches.back());
      if (result != ANITY_OK) {
        canvas->batches.clear();
        return result;
      }
    }
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    canvas->batches.clear();
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

void FillStatsLocked(const AnityUICanvas* canvas, AnityUICanvasStats* outStats) {
  *outStats = {};
  outStats->frameId = canvas->frameId;
  outStats->generation = canvas->generation;
  outStats->commandCount = static_cast<int32_t>(canvas->commands.size());
  outStats->batchCount = static_cast<int32_t>(canvas->batches.size());
  for (const AnityUICommandStorage& command : canvas->commands)
    if (IsVisible(command)) outStats->visibleCommandCount++;
  for (const AnityUIBatchStorage& batch : canvas->batches) {
    outStats->vertexCount += batch.info.vertexCount;
    outStats->indexCount += batch.info.indexCount;
  }
}

}  // namespace

AnityResult ANITY_CALL AnityUIRenderer_PackVertices(
    const AnityUIVertex* vertices,
    int32_t vertexCount,
    AnityUIPackedVertex* packedVertices,
    int32_t packedCapacity,
    int32_t* outWritten,
    AnityUIBounds* outBounds) {
  if (outWritten == nullptr || outBounds == nullptr || vertexCount < 0 ||
      packedCapacity < 0 || packedCapacity < vertexCount ||
      (vertexCount > 0 && (vertices == nullptr || packedVertices == nullptr))) {
    return ANITY_ERR_INVALID_ARG;
  }

  *outWritten = 0;
  *outBounds = EmptyBounds();
  if (vertexCount == 0) {
    return ANITY_OK;
  }

  AnityUIVector3 minimum = vertices[0].position;
  AnityUIVector3 maximum = vertices[0].position;
  if (!IsFinite(minimum)) {
    return ANITY_ERR_INVALID_ARG;
  }

  for (int32_t index = 0; index < vertexCount; ++index) {
    const AnityUIVertex& source = vertices[index];
    if (!IsFinite(source.position)) {
      *outWritten = 0;
      *outBounds = EmptyBounds();
      return ANITY_ERR_INVALID_ARG;
    }

    AnityUIPackedVertex& destination = packedVertices[index];
    destination.position = source.position;
    destination.color = source.color;
    destination.uv0 = source.uv0;
    destination.uv1 = source.uv1;
    destination.uv2 = source.uv2;
    destination.uv3 = source.uv3;
    destination.normal = source.normal;
    destination.tangent = source.tangent;

    minimum.x = std::min(minimum.x, source.position.x);
    minimum.y = std::min(minimum.y, source.position.y);
    minimum.z = std::min(minimum.z, source.position.z);
    maximum.x = std::max(maximum.x, source.position.x);
    maximum.y = std::max(maximum.y, source.position.y);
    maximum.z = std::max(maximum.z, source.position.z);
  }

  outBounds->min = minimum;
  outBounds->max = maximum;
  *outWritten = vertexCount;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityUIRenderer_BuildQuadIndices(
    int32_t vertexCount,
    uint32_t* indices,
    int32_t indexCapacity,
    int32_t* outWritten) {
  if (outWritten == nullptr || vertexCount < 0 || indexCapacity < 0 ||
      vertexCount % 4 != 0) {
    return ANITY_ERR_INVALID_ARG;
  }

  const int32_t required = (vertexCount / 4) * 6;
  if (indexCapacity < required || (required > 0 && indices == nullptr)) {
    return ANITY_ERR_INVALID_ARG;
  }

  *outWritten = 0;
  for (int32_t start = 0; start < vertexCount; start += 4) {
    indices[*outWritten + 0] = static_cast<uint32_t>(start + 0);
    indices[*outWritten + 1] = static_cast<uint32_t>(start + 1);
    indices[*outWritten + 2] = static_cast<uint32_t>(start + 2);
    indices[*outWritten + 3] = static_cast<uint32_t>(start + 2);
    indices[*outWritten + 4] = static_cast<uint32_t>(start + 3);
    indices[*outWritten + 5] = static_cast<uint32_t>(start + 0);
    *outWritten += 6;
  }
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityUIRenderer_EvaluateVisibility(
    const AnityUIRenderState* state,
    AnityUIVisibility* outVisibility) {
  if (state == nullptr || outVisibility == nullptr ||
      !std::isfinite(state->colorAlpha) || !std::isfinite(state->inheritedAlpha) ||
      !std::isfinite(state->softnessX) || !std::isfinite(state->softnessY)) {
    return ANITY_ERR_INVALID_ARG;
  }

  *outVisibility = {};
  outVisibility->effectiveAlpha = state->colorAlpha * state->inheritedAlpha;
  const float softnessX = std::max(0.0f, state->softnessX);
  const float softnessY = std::max(0.0f, state->softnessY);
  outVisibility->innerClipXMin = state->clipXMin + softnessX;
  outVisibility->innerClipYMin = state->clipYMin + softnessY;
  outVisibility->innerClipXMax = state->clipXMax - softnessX;
  outVisibility->innerClipYMax = state->clipYMax - softnessY;

  const bool hasGeometry = state->hasGeometry != 0;
  const bool alphaCull = state->cullTransparentMesh != 0 &&
      outVisibility->effectiveAlpha <= 0.001f;
  bool clipCull = false;
  bool clipped = false;
  if (state->rectClipping != 0 && hasGeometry) {
    const AnityUIBounds& bounds = state->bounds;
    clipCull = bounds.max.x < state->clipXMin || bounds.min.x > state->clipXMax ||
        bounds.max.y < state->clipYMin || bounds.min.y > state->clipYMax;
    clipped = !clipCull && (bounds.min.x < state->clipXMin ||
        bounds.max.x > state->clipXMax || bounds.min.y < state->clipYMin ||
        bounds.max.y > state->clipYMax);
  }

  outVisibility->culledByAlpha = alphaCull ? 1 : 0;
  outVisibility->culledByClip = clipCull ? 1 : 0;
  outVisibility->clipped = clipped ? 1 : 0;
  outVisibility->visible = hasGeometry && !alphaCull && !clipCull ? 1 : 0;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityUICanvas_Create(AnityUICanvas** outCanvas) {
  if (outCanvas == nullptr) return ANITY_ERR_INVALID_ARG;
  auto* canvas = new (std::nothrow) AnityUICanvas();
  if (canvas == nullptr) return ANITY_ERR_OUT_OF_MEMORY;
  *outCanvas = canvas;
  return ANITY_OK;
}

void ANITY_CALL AnityUICanvas_Destroy(AnityUICanvas* canvas) {
  delete canvas;
}

AnityResult ANITY_CALL AnityUICanvas_BeginFrame(AnityUICanvas* canvas,
                                                 uint64_t frameId) {
  if (canvas == nullptr) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(canvas->mutex);
  canvas->frameId = frameId;
  canvas->batches.clear();
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityUICanvas_Clear(AnityUICanvas* canvas) {
  if (canvas == nullptr) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(canvas->mutex);
  canvas->commands.clear();
  canvas->batches.clear();
  canvas->generation++;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityUICanvas_UpsertCommand(
    AnityUICanvas* canvas,
    const AnityUIRenderCommandDesc* desc,
    const AnityUIPackedVertex* vertices,
    int32_t vertexCount,
    const uint32_t* indices,
    int32_t indexCount) {
  if (canvas == nullptr || desc == nullptr || desc->rendererId == 0 ||
      vertexCount < 0 || indexCount < 0 || !IsFinite(*desc) ||
      (vertexCount > 0 && vertices == nullptr) ||
      (indexCount > 0 && indices == nullptr)) {
    return ANITY_ERR_INVALID_ARG;
  }
  for (int32_t index = 0; index < indexCount; ++index)
    if (indices[index] >= static_cast<uint32_t>(vertexCount))
      return ANITY_ERR_INVALID_ARG;

  try {
    std::vector<AnityUIPackedVertex> copiedVertices;
    std::vector<uint32_t> copiedIndices;
    if (vertexCount > 0) copiedVertices.assign(vertices, vertices + vertexCount);
    if (indexCount > 0) copiedIndices.assign(indices, indices + indexCount);
    std::lock_guard<std::mutex> lock(canvas->mutex);
    auto existing = std::find_if(canvas->commands.begin(), canvas->commands.end(),
        [desc](const AnityUICommandStorage& command) {
          return command.desc.rendererId == desc->rendererId;
        });
    if (existing == canvas->commands.end()) {
      AnityUICommandStorage command;
      command.desc = *desc;
      command.insertionOrder = canvas->insertionSequence++;
      command.vertices = std::move(copiedVertices);
      command.indices = std::move(copiedIndices);
      canvas->commands.push_back(std::move(command));
    } else {
      existing->desc = *desc;
      existing->vertices = std::move(copiedVertices);
      existing->indices = std::move(copiedIndices);
    }
    canvas->batches.clear();
    canvas->generation++;
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

AnityResult ANITY_CALL AnityUICanvas_RemoveCommand(AnityUICanvas* canvas,
                                                    uint64_t rendererId) {
  if (canvas == nullptr || rendererId == 0) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(canvas->mutex);
  auto existing = std::find_if(canvas->commands.begin(), canvas->commands.end(),
      [rendererId](const AnityUICommandStorage& command) {
        return command.desc.rendererId == rendererId;
      });
  if (existing == canvas->commands.end()) return ANITY_OK;
  canvas->commands.erase(existing);
  canvas->batches.clear();
  canvas->generation++;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityUICanvas_BuildBatches(AnityUICanvas* canvas) {
  if (canvas == nullptr) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(canvas->mutex);
  return BuildBatchesLocked(canvas);
}

AnityResult ANITY_CALL AnityUICanvas_GetStats(const AnityUICanvas* canvas,
                                               AnityUICanvasStats* outStats) {
  if (canvas == nullptr || outStats == nullptr) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(canvas->mutex);
  FillStatsLocked(canvas, outStats);
  return ANITY_OK;
}

AnityResult AnityUICanvas_CopyFlattenedSnapshot(
    AnityUICanvas* canvas,
    std::vector<AnityUIPackedVertex>& vertices,
    std::vector<uint32_t>& indices,
    std::vector<AnityUIDrawPacket>& draws,
    AnityUICanvasStats& stats) {
  if (canvas == nullptr) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(canvas->mutex);
  AnityResult result = BuildBatchesLocked(canvas);
  if (result != ANITY_OK) return result;
  vertices.clear();
  indices.clear();
  draws.clear();
  FillStatsLocked(canvas, &stats);
  try {
    vertices.reserve(static_cast<size_t>(stats.vertexCount));
    indices.reserve(static_cast<size_t>(stats.indexCount));
    draws.reserve(static_cast<size_t>(stats.batchCount));
    for (const AnityUIBatchStorage& batch : canvas->batches) {
      if (vertices.size() > std::numeric_limits<uint32_t>::max() -
              batch.vertices.size()) {
        vertices.clear();
        indices.clear();
        draws.clear();
        return ANITY_ERR_OUT_OF_MEMORY;
      }
      const uint32_t vertexOffset = static_cast<uint32_t>(vertices.size());
      const uint32_t firstIndex = static_cast<uint32_t>(indices.size());
      vertices.insert(vertices.end(), batch.vertices.begin(), batch.vertices.end());
      for (uint32_t index : batch.indices) {
        if (index > std::numeric_limits<uint32_t>::max() - vertexOffset) {
          vertices.clear();
          indices.clear();
          draws.clear();
          return ANITY_ERR_OUT_OF_MEMORY;
        }
        indices.push_back(index + vertexOffset);
      }
      AnityUIDrawPacket packet;
      packet.info = batch.info;
      packet.firstIndex = firstIndex;
      packet.clipXMin = batch.key.clipXMin;
      packet.clipYMin = batch.key.clipYMin;
      packet.clipXMax = batch.key.clipXMax;
      packet.clipYMax = batch.key.clipYMax;
      packet.softnessX = batch.key.softnessX;
      packet.softnessY = batch.key.softnessY;
      draws.push_back(packet);
    }
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    vertices.clear();
    indices.clear();
    draws.clear();
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

AnityResult ANITY_CALL AnityUICanvas_GetBatchInfo(const AnityUICanvas* canvas,
                                                   int32_t batchIndex,
                                                   AnityUIBatchInfo* outInfo) {
  if (canvas == nullptr || outInfo == nullptr || batchIndex < 0)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(canvas->mutex);
  if (static_cast<size_t>(batchIndex) >= canvas->batches.size())
    return ANITY_ERR_INVALID_ARG;
  *outInfo = canvas->batches[batchIndex].info;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityUICanvas_CopyBatchVertices(
    const AnityUICanvas* canvas, int32_t batchIndex,
    AnityUIPackedVertex* vertices, int32_t vertexCapacity, int32_t* outWritten) {
  if (canvas == nullptr || batchIndex < 0 || vertexCapacity < 0 || outWritten == nullptr)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(canvas->mutex);
  if (static_cast<size_t>(batchIndex) >= canvas->batches.size())
    return ANITY_ERR_INVALID_ARG;
  const auto& source = canvas->batches[batchIndex].vertices;
  if (vertexCapacity < static_cast<int32_t>(source.size()) ||
      (!source.empty() && vertices == nullptr)) return ANITY_ERR_INVALID_ARG;
  if (!source.empty())
    std::memcpy(vertices, source.data(), source.size() * sizeof(AnityUIPackedVertex));
  *outWritten = static_cast<int32_t>(source.size());
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityUICanvas_CopyBatchIndices(
    const AnityUICanvas* canvas, int32_t batchIndex,
    uint32_t* indices, int32_t indexCapacity, int32_t* outWritten) {
  if (canvas == nullptr || batchIndex < 0 || indexCapacity < 0 || outWritten == nullptr)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(canvas->mutex);
  if (static_cast<size_t>(batchIndex) >= canvas->batches.size())
    return ANITY_ERR_INVALID_ARG;
  const auto& source = canvas->batches[batchIndex].indices;
  if (indexCapacity < static_cast<int32_t>(source.size()) ||
      (!source.empty() && indices == nullptr)) return ANITY_ERR_INVALID_ARG;
  if (!source.empty())
    std::memcpy(indices, source.data(), source.size() * sizeof(uint32_t));
  *outWritten = static_cast<int32_t>(source.size());
  return ANITY_OK;
}
