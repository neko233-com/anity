#define ANITY_NATIVE_BUILD
#include "anity_graphics_device.h"
#include <new>

extern "C" AnityResult AnityGraphics_CreateNull(const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**);
extern "C" AnityResult AnityGraphics_CreateD3D11(const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**);
extern "C" AnityResult AnityGraphics_D3D11_BeginFrame(AnityGraphicsDevice*);
extern "C" AnityResult AnityGraphics_D3D11_Present(AnityGraphicsDevice*);
extern "C" void AnityGraphics_D3D11_Destroy(AnityGraphicsDevice*);
extern "C" AnityResult AnityGraphics_CreateVulkan(const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**);
extern "C" void AnityGraphics_Vulkan_Destroy(AnityGraphicsDevice*);

extern "C" {

AnityGraphicsDeviceType ANITY_CALL AnityGraphics_GetDefaultDeviceType(AnityPlatform platform) {
  switch (platform) {
    case ANITY_PLATFORM_IOS: return ANITY_GFX_METAL;
    case ANITY_PLATFORM_ANDROID: return ANITY_GFX_VULKAN;
    case ANITY_PLATFORM_MACOS: return ANITY_GFX_METAL;
    case ANITY_PLATFORM_WINDOWS: return ANITY_GFX_D3D11;
    case ANITY_PLATFORM_LINUX: return ANITY_GFX_VULKAN;
    case ANITY_PLATFORM_WEBGL: return ANITY_GFX_WEBGL2;
    default: return ANITY_GFX_NULL;
  }
}

AnityResult ANITY_CALL AnityGraphics_CreateDevice(
    const AnityGraphicsDeviceDesc* desc,
    AnityGraphicsDevice** outDevice) {
  if (!desc || !outDevice) return ANITY_ERR_INVALID_ARG;

  AnityGraphicsDeviceType want = desc->preferred;
  if (want == static_cast<AnityGraphicsDeviceType>(0))
    want = AnityGraphics_GetDefaultDeviceType(Anity_GetPlatform());

  AnityGraphicsDeviceDesc d = *desc;
  d.preferred = want;

#if defined(ANITY_HAS_D3D11) && defined(_WIN32)
  if (want == ANITY_GFX_D3D11 || want == ANITY_GFX_D3D12) {
    AnityResult r = AnityGraphics_CreateD3D11(&d, outDevice);
    if (r == ANITY_OK) return r;
  }
#endif
#if defined(ANITY_HAS_VULKAN)
  if (want == ANITY_GFX_VULKAN) {
    AnityResult r = AnityGraphics_CreateVulkan(&d, outDevice);
    if (r == ANITY_OK) return r;
  }
#endif

  AnityResult r = AnityGraphics_CreateNull(&d, outDevice);
  if (r == ANITY_OK && *outDevice)
    (*outDevice)->type = want;
  return r;
}

void ANITY_CALL AnityGraphics_DestroyDevice(AnityGraphicsDevice* device) {
  if (!device) return;
  if (device->type == ANITY_GFX_D3D11 || device->type == ANITY_GFX_D3D12)
    AnityGraphics_D3D11_Destroy(device);
  else if (device->type == ANITY_GFX_VULKAN)
    AnityGraphics_Vulkan_Destroy(device);
  delete device;
}

AnityGraphicsDeviceType ANITY_CALL AnityGraphics_GetDeviceType(const AnityGraphicsDevice* device) {
  return device ? device->type : ANITY_GFX_NULL;
}

AnityResult ANITY_CALL AnityGraphics_BeginFrame(AnityGraphicsDevice* device) {
  if (!device) return ANITY_ERR_INVALID_ARG;
  if (device->type == ANITY_GFX_D3D11 || device->type == ANITY_GFX_D3D12)
    return AnityGraphics_D3D11_BeginFrame(device);
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_EndFrame(AnityGraphicsDevice* device) {
  if (!device) return ANITY_ERR_INVALID_ARG;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_Present(AnityGraphicsDevice* device) {
  if (!device) return ANITY_ERR_INVALID_ARG;
  if (device->type == ANITY_GFX_D3D11 || device->type == ANITY_GFX_D3D12)
    return AnityGraphics_D3D11_Present(device);
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_Resize(AnityGraphicsDevice* device, int32_t width, int32_t height) {
  if (!device || width <= 0 || height <= 0) return ANITY_ERR_INVALID_ARG;
  device->width = width;
  device->height = height;
  return ANITY_OK;
}

int32_t ANITY_CALL AnityGraphics_SupportsHDR(const AnityGraphicsDevice* device) {
  return device ? device->supportsHdr : 0;
}

} // extern "C"
