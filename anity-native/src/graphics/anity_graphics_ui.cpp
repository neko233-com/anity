#define ANITY_NATIVE_BUILD
#include "anity_graphics_device.h"
#include "anity/ui/anity_ui_renderer.h"
#include "../ui/anity_ui_renderer_internal.h"

#include <cstring>
#include <limits>
#include <new>
#include <vector>

extern "C" AnityResult AnityGraphics_D3D11_UploadUI(
    AnityGraphicsDevice*, int32_t, const void*, int32_t, const void*, int32_t);
extern "C" AnityResult AnityGraphics_Metal_UploadUI(
    AnityGraphicsDevice*, int32_t, const void*, int32_t, const void*, int32_t);
extern "C" AnityResult AnityGraphics_Vulkan_UploadUI(
    AnityGraphicsDevice*, int32_t, const void*, int32_t, const void*, int32_t);
extern "C" AnityResult AnityGraphics_D3D11_DrawUI(
    AnityGraphicsDevice*, int32_t, const AnityUIDrawPacket*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_Metal_DrawUI(
    AnityGraphicsDevice*, int32_t, const AnityUIDrawPacket*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_Vulkan_DrawUI(
    AnityGraphicsDevice*, int32_t, const AnityUIDrawPacket*, int32_t, int32_t*);

struct AnityUIUploadRing {
  std::vector<AnityUIPackedVertex> vertices;
  std::vector<uint32_t> indices;
  std::vector<AnityUIDrawPacket> draws;
};

struct AnityGraphicsUIUploadState {
  AnityUIUploadRing rings[3];
  AnityGraphicsUIUploadStats stats{};
};

namespace {

AnityGraphicsUIUploadState* EnsureState(AnityGraphicsDevice* device) {
  if (device->uiUpload) return device->uiUpload;
  device->uiUpload = new (std::nothrow) AnityGraphicsUIUploadState();
  return device->uiUpload;
}

AnityResult UploadBackend(AnityGraphicsDevice* device, int32_t ringIndex,
                          const void* vertices, int32_t vertexBytes,
                          const void* indices, int32_t indexBytes,
                          int32_t* backendKind) {
  AnityResult result = ANITY_ERR_NOT_SUPPORTED;
  switch (device->type) {
    case ANITY_GFX_D3D11:
    case ANITY_GFX_D3D12:
      result = AnityGraphics_D3D11_UploadUI(
          device, ringIndex, vertices, vertexBytes, indices, indexBytes);
      if (result == ANITY_OK) *backendKind = 3;
      break;
    case ANITY_GFX_METAL:
      result = AnityGraphics_Metal_UploadUI(
          device, ringIndex, vertices, vertexBytes, indices, indexBytes);
      if (result == ANITY_OK) *backendKind = 2;
      break;
    case ANITY_GFX_VULKAN:
      result = AnityGraphics_Vulkan_UploadUI(
          device, ringIndex, vertices, vertexBytes, indices, indexBytes);
      if (result == ANITY_OK) *backendKind = 1;
      break;
    default:
      break;
  }
  /* CPU ring remains the authoritative headless/null fallback. */
  if (result == ANITY_ERR_NOT_SUPPORTED || result == ANITY_ERR_INVALID_ARG ||
      result == ANITY_ERR_DEVICE_LOST) {
    *backendKind = 0;
    return ANITY_OK;
  }
  return result;
}

AnityResult DrawBackend(AnityGraphicsDevice* device, int32_t ringIndex,
                        const std::vector<AnityUIDrawPacket>& draws,
                        int32_t* drawCount) {
  *drawCount = 0;
  const AnityUIDrawPacket* packets = draws.empty() ? nullptr : draws.data();
  const int32_t packetCount = static_cast<int32_t>(draws.size());
  AnityResult result = ANITY_ERR_NOT_SUPPORTED;
  switch (device->type) {
    case ANITY_GFX_D3D11:
    case ANITY_GFX_D3D12:
      result = AnityGraphics_D3D11_DrawUI(
          device, ringIndex, packets, packetCount, drawCount);
      break;
    case ANITY_GFX_METAL:
      result = AnityGraphics_Metal_DrawUI(
          device, ringIndex, packets, packetCount, drawCount);
      break;
    case ANITY_GFX_VULKAN:
      result = AnityGraphics_Vulkan_DrawUI(
          device, ringIndex, packets, packetCount, drawCount);
      break;
    default:
      break;
  }
  if (result == ANITY_ERR_NOT_SUPPORTED || result == ANITY_ERR_INVALID_ARG ||
      result == ANITY_ERR_DEVICE_LOST) {
    *drawCount = 0;
    return ANITY_OK;
  }
  return result;
}

}  // namespace

void AnityGraphics_DestroyUIUpload(AnityGraphicsDevice* device) {
  if (!device) return;
  delete device->uiUpload;
  device->uiUpload = nullptr;
  device->uiCanvas = nullptr;
}

extern "C" {

AnityResult ANITY_CALL AnityGraphics_SetUICanvas(
    AnityGraphicsDevice* device, AnityUICanvas* canvas) {
  if (!device) return ANITY_ERR_INVALID_ARG;
  device->uiCanvas = canvas;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_SubmitUICanvas(
    AnityGraphicsDevice* device, AnityUICanvas* canvas) {
  if (!device || !canvas) return ANITY_ERR_INVALID_ARG;
  auto* state = EnsureState(device);
  if (!state) return ANITY_ERR_OUT_OF_MEMORY;
  const int32_t ringIndex = static_cast<int32_t>(device->frameId % 3u);
  AnityUIUploadRing& ring = state->rings[ringIndex];
  AnityUICanvasStats canvasStats{};
  AnityResult result = AnityUICanvas_CopyFlattenedSnapshot(
      canvas, ring.vertices, ring.indices, ring.draws, canvasStats);
  if (result != ANITY_OK) return result;
  if (ring.vertices.size() > static_cast<size_t>(std::numeric_limits<int32_t>::max()) /
          sizeof(AnityUIPackedVertex) ||
      ring.indices.size() > static_cast<size_t>(std::numeric_limits<int32_t>::max()) /
          sizeof(uint32_t)) return ANITY_ERR_OUT_OF_MEMORY;
  const int32_t vertexBytes = static_cast<int32_t>(
      ring.vertices.size() * sizeof(AnityUIPackedVertex));
  const int32_t indexBytes = static_cast<int32_t>(ring.indices.size() * sizeof(uint32_t));
  int32_t backendKind = 0;
  result = UploadBackend(device, ringIndex,
      ring.vertices.empty() ? nullptr : ring.vertices.data(), vertexBytes,
      ring.indices.empty() ? nullptr : ring.indices.data(), indexBytes, &backendKind);
  if (result != ANITY_OK) return result;
  int32_t drawCount = 0;
  result = DrawBackend(device, ringIndex, ring.draws, &drawCount);
  if (result != ANITY_OK) return result;
  state->stats.frameId = device->frameId;
  state->stats.uploadGeneration++;
  state->stats.submitted = 1;
  state->stats.batchCount = canvasStats.batchCount;
  state->stats.drawCount = drawCount;
  state->stats.vertexCount = static_cast<int32_t>(ring.vertices.size());
  state->stats.indexCount = static_cast<int32_t>(ring.indices.size());
  state->stats.vertexBytes = vertexBytes;
  state->stats.indexBytes = indexBytes;
  state->stats.ringIndex = ringIndex;
  state->stats.backendKind = backendKind;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_GetUIUploadStats(
    const AnityGraphicsDevice* device, AnityGraphicsUIUploadStats* outStats) {
  if (!device || !outStats) return ANITY_ERR_INVALID_ARG;
  if (!device->uiUpload) {
    *outStats = {};
    return ANITY_OK;
  }
  *outStats = device->uiUpload->stats;
  return ANITY_OK;
}

}  // extern "C"
