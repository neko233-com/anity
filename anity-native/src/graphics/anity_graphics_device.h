#pragma once
#include "anity/graphics/anity_graphics.h"

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
};

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
