#define ANITY_NATIVE_BUILD
#include "../anity_graphics_device.h"
#include <new>

extern "C" AnityResult AnityGraphics_CreateNull(
    const AnityGraphicsDeviceDesc* desc, AnityGraphicsDevice** outDevice) {
  if (!desc || !outDevice) return ANITY_ERR_INVALID_ARG;
  auto* dev = new (std::nothrow) AnityGraphicsDevice();
  if (!dev) return ANITY_ERR_OUT_OF_MEMORY;

  AnityGraphicsDeviceType type = desc->preferred;
  if (type == static_cast<AnityGraphicsDeviceType>(0) || type == ANITY_GFX_NULL)
    type = AnityGraphics_GetDefaultDeviceType(Anity_GetPlatform());

  dev->type = type;
  dev->width = desc->width > 0 ? desc->width : 1280;
  dev->height = desc->height > 0 ? desc->height : 720;
  dev->hdrEnabled = desc->hdrEnabled;
  dev->msaaSamples = desc->msaaSamples;
  dev->vsync = desc->vsync;
  dev->supportsHdr = desc->hdrEnabled || type == ANITY_GFX_METAL || type == ANITY_GFX_VULKAN
                     || type == ANITY_GFX_D3D11 || type == ANITY_GFX_D3D12;
  dev->backend = nullptr;
  dev->swapchain = nullptr;
  *outDevice = dev;
  return ANITY_OK;
}
