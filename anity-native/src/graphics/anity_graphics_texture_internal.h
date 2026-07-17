#pragma once

#include "anity_graphics_device.h"

#include <cstdint>
#include <vector>

struct AnityGraphicsTextureSnapshot {
  AnityGraphicsTextureInfo info{};
  std::vector<uint8_t> rgba8;
  void* nativeHandle = nullptr;
};

bool AnityGraphics_CopyTextureSnapshot(
    const AnityGraphicsDevice* device, uint64_t textureId,
    AnityGraphicsTextureSnapshot& outSnapshot);
void AnityGraphics_SetTextureBackendState(
    AnityGraphicsDevice* device, uint64_t textureId,
    void* nativeHandle, int32_t backendKind);
