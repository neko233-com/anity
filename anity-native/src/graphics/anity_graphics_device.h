#pragma once
#include "anity/graphics/anity_graphics.h"

struct AnityGraphicsUIUploadState;
struct AnityGraphicsTextureRegistry;
struct AnityGraphicsVFXEventRegistry;

struct AnityGraphicsVFXPlanarDrawPacket {
  AnityGraphicsVFXPlanarOutputDesc output;
  float localToWorld[16];
  uint64_t generation;
  int32_t aliveCount;
  int32_t effectSortOrder;
  int32_t pendingInitialize;
  int32_t reserved;
};

struct AnityGraphicsDevice {
  AnityGraphicsDeviceType type;
  int32_t width;
  int32_t height;
  int32_t hdrEnabled;
  int32_t msaaSamples;
  int32_t vsync;
  int32_t supportsHdr;
  void* backend; /* backend-private state (D3D11State*, VkState*, ...) */
  AnitySwapchain* swapchain; /* optional owned swapchain */
  uint64_t frameId = 0;
  AnityUICanvas* uiCanvas = nullptr; /* non-owning */
  AnityGraphicsUIUploadState* uiUpload = nullptr;
  AnityGraphicsTextureRegistry* textures = nullptr;
  AnityGraphicsVFXEventRegistry* vfxEvents = nullptr;
};

void AnityGraphics_DestroyUIUpload(AnityGraphicsDevice* device);
void AnityGraphics_DestroyTextureRegistry(AnityGraphicsDevice* device);
void AnityGraphics_DestroyVFXEventRegistry(AnityGraphicsDevice* device);

struct AnitySwapchain {
  AnityGraphicsDevice* device;
  int32_t width;
  int32_t height;
  int32_t imageCount;
  int32_t currentImage;
  int32_t vsync;
  int32_t hdr;
  int32_t headless;
  int32_t presentCount;
  void* backend; /* VkSwapchainKHR wrapper / CAMetalLayer / headless buffers */
};
