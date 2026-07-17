#define ANITY_NATIVE_BUILD
#include "anity_graphics_device.h"
#include "anity/ui/anity_ui_renderer.h"
#include <new>
#include <cstring>

extern "C" AnityResult AnityGraphics_CreateNull(const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**);
extern "C" AnityResult AnityGraphics_CreateD3D11(const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**);
extern "C" AnityResult AnityGraphics_D3D11_BeginFrame(AnityGraphicsDevice*);
extern "C" AnityResult AnityGraphics_D3D11_Present(AnityGraphicsDevice*);
extern "C" void AnityGraphics_D3D11_Destroy(AnityGraphicsDevice*);
extern "C" AnityResult AnityGraphics_CreateVulkan(const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**);
extern "C" void AnityGraphics_Vulkan_Destroy(AnityGraphicsDevice*);
extern "C" AnityResult AnityGraphics_Vulkan_CreateSwapchain(AnityGraphicsDevice*, const AnitySwapchainDesc*, AnitySwapchain**);
extern "C" void AnityGraphics_Vulkan_DestroySwapchain(AnitySwapchain*);
extern "C" AnityResult AnityGraphics_Vulkan_Acquire(AnitySwapchain*, int32_t*);
extern "C" AnityResult AnityGraphics_Vulkan_Present(AnitySwapchain*);
extern "C" int32_t AnityGraphics_Vulkan_SwapchainHasNativeSurface(const AnitySwapchain*);
extern "C" int32_t AnityGraphics_Vulkan_GetSwapchainSurfaceKind(const AnitySwapchain*);
extern "C" AnityResult AnityGraphics_CreateMetal(const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**);
extern "C" void AnityGraphics_Metal_Destroy(AnityGraphicsDevice*);
extern "C" AnityResult AnityGraphics_Metal_CreateSwapchain(AnityGraphicsDevice*, const AnitySwapchainDesc*, AnitySwapchain**);
extern "C" void AnityGraphics_Metal_DestroySwapchain(AnitySwapchain*);
extern "C" AnityResult AnityGraphics_Metal_Acquire(AnitySwapchain*, int32_t*);
extern "C" AnityResult AnityGraphics_Metal_Present(AnitySwapchain*);
extern "C" int32_t AnityGraphics_Metal_SwapchainHasNativeLayer(const AnitySwapchain*);
extern "C" AnityResult AnityGraphics_Metal_ReadbackSwapchainRGBA8(
    AnitySwapchain*, uint8_t*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_D3D11_ReadbackSwapchainRGBA8(
    AnitySwapchain*, uint8_t*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_Vulkan_ReadbackSwapchainRGBA8(
    AnitySwapchain*, uint8_t*, int32_t, int32_t*);

static AnityResult CreateHeadlessSwapchain(
    AnityGraphicsDevice* device, const AnitySwapchainDesc* desc, AnitySwapchain** out) {
  if (!device || !desc || !out) return ANITY_ERR_INVALID_ARG;
  auto* sc = new (std::nothrow) AnitySwapchain();
  if (!sc) return ANITY_ERR_OUT_OF_MEMORY;
  std::memset(sc, 0, sizeof(*sc));
  sc->device = device;
  sc->width = desc->width > 0 ? desc->width : (device->width > 0 ? device->width : 1280);
  sc->height = desc->height > 0 ? desc->height : (device->height > 0 ? device->height : 720);
  sc->imageCount = desc->imageCount > 0 ? desc->imageCount : 2;
  if (sc->imageCount > 4) sc->imageCount = 4;
  sc->vsync = desc->vsync;
  sc->hdr = desc->hdr;
  sc->headless = desc->nativeWindow == nullptr ? 1 : 0;
  sc->currentImage = 0;
  sc->presentCount = 0;
  sc->backend = nullptr;
  device->swapchain = sc;
  *out = sc;
  return ANITY_OK;
}

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
    if (r == ANITY_OK) {
      if (*outDevice) (*outDevice)->swapchain = nullptr;
      return r;
    }
  }
#endif
#if defined(ANITY_HAS_VULKAN)
  if (want == ANITY_GFX_VULKAN) {
    AnityResult r = AnityGraphics_CreateVulkan(&d, outDevice);
    if (r == ANITY_OK) {
      if (*outDevice) (*outDevice)->swapchain = nullptr;
      return r;
    }
  }
#endif
#if defined(ANITY_HAS_METAL)
  if (want == ANITY_GFX_METAL) {
    AnityResult r = AnityGraphics_CreateMetal(&d, outDevice);
    if (r == ANITY_OK) {
      if (*outDevice) (*outDevice)->swapchain = nullptr;
      return r;
    }
  }
#endif

  AnityResult r = AnityGraphics_CreateNull(&d, outDevice);
  if (r == ANITY_OK && *outDevice) {
    (*outDevice)->type = want;
    (*outDevice)->swapchain = nullptr;
  }
  return r;
}

void ANITY_CALL AnityGraphics_DestroyDevice(AnityGraphicsDevice* device) {
  if (!device) return;
  if (device->swapchain) {
    AnityGraphics_DestroySwapchain(device->swapchain);
    device->swapchain = nullptr;
  }
  AnityGraphics_DestroyUIUpload(device);
  AnityGraphics_DestroyTextureRegistry(device);
  AnityGraphics_DestroyVFXEventRegistry(device);
  if (device->type == ANITY_GFX_D3D11 || device->type == ANITY_GFX_D3D12)
    AnityGraphics_D3D11_Destroy(device);
  else if (device->type == ANITY_GFX_VULKAN)
    AnityGraphics_Vulkan_Destroy(device);
  else if (device->type == ANITY_GFX_METAL)
    AnityGraphics_Metal_Destroy(device);
  delete device;
}

AnityGraphicsDeviceType ANITY_CALL AnityGraphics_GetDeviceType(const AnityGraphicsDevice* device) {
  return device ? device->type : ANITY_GFX_NULL;
}

AnityResult ANITY_CALL AnityGraphics_BeginFrame(AnityGraphicsDevice* device) {
  if (!device) return ANITY_ERR_INVALID_ARG;
  device->frameId++;
  if (device->uiCanvas) {
    AnityResult uiResult = AnityUICanvas_BeginFrame(device->uiCanvas, device->frameId);
    if (uiResult != ANITY_OK) return uiResult;
  }
  if (device->type == ANITY_GFX_D3D11 || device->type == ANITY_GFX_D3D12)
    return AnityGraphics_D3D11_BeginFrame(device);
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_EndFrame(AnityGraphicsDevice* device) {
  if (!device) return ANITY_ERR_INVALID_ARG;
  if (device->uiCanvas) {
    AnityResult uiResult = AnityGraphics_SubmitUICanvas(device, device->uiCanvas);
    if (uiResult != ANITY_OK) return uiResult;
  }
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_Present(AnityGraphicsDevice* device) {
  if (!device) return ANITY_ERR_INVALID_ARG;
  if (device->swapchain)
    return AnityGraphics_PresentSwapchain(device->swapchain);
  if (device->type == ANITY_GFX_D3D11 || device->type == ANITY_GFX_D3D12)
    return AnityGraphics_D3D11_Present(device);
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_Resize(AnityGraphicsDevice* device, int32_t width, int32_t height) {
  if (!device || width <= 0 || height <= 0) return ANITY_ERR_INVALID_ARG;
  device->width = width;
  device->height = height;
  if (device->swapchain) {
    device->swapchain->width = width;
    device->swapchain->height = height;
  }
  return ANITY_OK;
}

int32_t ANITY_CALL AnityGraphics_SupportsHDR(const AnityGraphicsDevice* device) {
  return device ? device->supportsHdr : 0;
}

AnityResult ANITY_CALL AnityGraphics_CreateSwapchain(
    AnityGraphicsDevice* device,
    const AnitySwapchainDesc* desc,
    AnitySwapchain** outSwapchain) {
  if (!device || !desc || !outSwapchain) return ANITY_ERR_INVALID_ARG;
  if (device->swapchain) {
    AnityGraphics_DestroySwapchain(device->swapchain);
    device->swapchain = nullptr;
  }

#if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN) {
    AnityResult r = AnityGraphics_Vulkan_CreateSwapchain(device, desc, outSwapchain);
    if (r == ANITY_OK) return r;
  }
#endif
#if defined(ANITY_HAS_METAL)
  if (device->type == ANITY_GFX_METAL) {
    AnityResult r = AnityGraphics_Metal_CreateSwapchain(device, desc, outSwapchain);
    if (r == ANITY_OK) return r;
  }
#endif

  // Headless / null / D3D fallback path — always works for CI
  return CreateHeadlessSwapchain(device, desc, outSwapchain);
}

void ANITY_CALL AnityGraphics_DestroySwapchain(AnitySwapchain* swapchain) {
  if (!swapchain) return;
  AnityGraphicsDevice* dev = swapchain->device;
#if defined(ANITY_HAS_VULKAN)
  if (dev && dev->type == ANITY_GFX_VULKAN)
    AnityGraphics_Vulkan_DestroySwapchain(swapchain);
#endif
#if defined(ANITY_HAS_METAL)
  if (dev && dev->type == ANITY_GFX_METAL)
    AnityGraphics_Metal_DestroySwapchain(swapchain);
#endif
  if (dev && dev->swapchain == swapchain)
    dev->swapchain = nullptr;
  delete swapchain;
}

AnityResult ANITY_CALL AnityGraphics_AcquireNextImage(AnitySwapchain* swapchain, int32_t* outImageIndex) {
  if (!swapchain) return ANITY_ERR_INVALID_ARG;
  AnityGraphicsDevice* dev = swapchain->device;
  if (dev) {
    if (dev->type == ANITY_GFX_VULKAN)
      return AnityGraphics_Vulkan_Acquire(swapchain, outImageIndex);
    if (dev->type == ANITY_GFX_METAL)
      return AnityGraphics_Metal_Acquire(swapchain, outImageIndex);
  }
  swapchain->currentImage = (swapchain->currentImage + 1) % (swapchain->imageCount > 0 ? swapchain->imageCount : 1);
  if (outImageIndex) *outImageIndex = swapchain->currentImage;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_PresentSwapchain(AnitySwapchain* swapchain) {
  if (!swapchain) return ANITY_ERR_INVALID_ARG;
  AnityGraphicsDevice* dev = swapchain->device;
  if (dev) {
    if (dev->type == ANITY_GFX_VULKAN)
      return AnityGraphics_Vulkan_Present(swapchain);
    if (dev->type == ANITY_GFX_METAL)
      return AnityGraphics_Metal_Present(swapchain);
  }
  swapchain->presentCount++;
  return ANITY_OK;
}

int32_t ANITY_CALL AnityGraphics_GetSwapchainImageCount(const AnitySwapchain* swapchain) {
  return swapchain ? swapchain->imageCount : 0;
}
int32_t ANITY_CALL AnityGraphics_GetSwapchainWidth(const AnitySwapchain* swapchain) {
  return swapchain ? swapchain->width : 0;
}
int32_t ANITY_CALL AnityGraphics_GetSwapchainHeight(const AnitySwapchain* swapchain) {
  return swapchain ? swapchain->height : 0;
}
int32_t ANITY_CALL AnityGraphics_IsSwapchainHeadless(const AnitySwapchain* swapchain) {
  return swapchain ? swapchain->headless : 1;
}
int32_t ANITY_CALL AnityGraphics_GetSwapchainPresentCount(const AnitySwapchain* swapchain) {
  return swapchain ? swapchain->presentCount : 0;
}
AnityResult ANITY_CALL AnityGraphics_ReadbackSwapchainRGBA8(
    AnitySwapchain* swapchain, uint8_t* pixels, int32_t pixelCapacity,
    int32_t* outWritten) {
  if (!swapchain || pixelCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  if (swapchain->device && swapchain->device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_ReadbackSwapchainRGBA8(
        swapchain, pixels, pixelCapacity, outWritten);
  if (swapchain->device && swapchain->device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_ReadbackSwapchainRGBA8(
        swapchain, pixels, pixelCapacity, outWritten);
  if (swapchain->device && (swapchain->device->type == ANITY_GFX_D3D11 ||
      swapchain->device->type == ANITY_GFX_D3D12))
    return AnityGraphics_D3D11_ReadbackSwapchainRGBA8(
        swapchain, pixels, pixelCapacity, outWritten);
  *outWritten = 0;
  return ANITY_ERR_NOT_SUPPORTED;
}
int32_t ANITY_CALL AnityGraphics_SwapchainHasNativeSurface(const AnitySwapchain* swapchain) {
  if (!swapchain || !swapchain->device) return 0;
  if (swapchain->device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_SwapchainHasNativeSurface(swapchain);
  if (swapchain->device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_SwapchainHasNativeLayer(swapchain);
  return 0;
}
int32_t ANITY_CALL AnityGraphics_GetSwapchainBackendKind(const AnitySwapchain* swapchain) {
  if (!swapchain || !swapchain->device || !swapchain->device->backend) return 0;
  switch (swapchain->device->type) {
    case ANITY_GFX_VULKAN: return 1;
    case ANITY_GFX_METAL: return 2;
    case ANITY_GFX_D3D11:
    case ANITY_GFX_D3D12: return 3;
    default: return 0;
  }
}

int32_t ANITY_CALL AnityGraphics_GetSwapchainSurfaceKind(const AnitySwapchain* swapchain) {
  if (!swapchain || !swapchain->device) return 0;
  if (swapchain->device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_GetSwapchainSurfaceKind(swapchain);
  /* Metal CAMetalLayer counts as native surface kind 0 (use HasNativeSurface) */
  return 0;
}

} // extern "C"
