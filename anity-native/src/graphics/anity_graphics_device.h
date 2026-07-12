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
};
