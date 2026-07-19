#define ANITY_NATIVE_BUILD
#include "anity_graphics_device.h"
#include "anity/ui/anity_ui_renderer.h"
#include <new>
#include <cstring>
#include <cmath>

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
extern "C" AnityResult AnityGraphics_Vulkan_ExecuteCameraPass(
    AnityGraphicsDevice*, const AnityGraphicsCameraPassDesc*);
extern "C" AnityResult AnityGraphics_Vulkan_EnsureCameraRenderTarget(
    AnityGraphicsDevice*, const AnityGraphicsCameraRenderTargetDesc*);
extern "C" void AnityGraphics_Vulkan_DestroyCameraRenderTarget(
    AnityGraphicsDevice*, uint64_t);
extern "C" AnityResult AnityGraphics_Vulkan_ReadbackCameraRenderTargetRGBA8(
    AnityGraphicsDevice*, uint64_t, uint8_t*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_Vulkan_ReadbackCameraRenderTargetSliceRGBA8(
    AnityGraphicsDevice*, uint64_t, int32_t, uint8_t*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_Vulkan_CopyCameraRenderTargetColor(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Vulkan_CopyCameraRenderTargetColorSlice(
    AnityGraphicsDevice*, uint64_t, int32_t, int32_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Vulkan_CopyCameraRenderTargetDepthToColor(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Vulkan_CopyCameraRenderTargetDepthToColorSlice(
    AnityGraphicsDevice*, uint64_t, int32_t, int32_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Vulkan_CopyCameraRenderTargetNormalsToColor(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Vulkan_CopyCameraRenderTargetNormalsToColorSlice(
    AnityGraphicsDevice*, uint64_t, int32_t, int32_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Vulkan_DrawCameraMesh(
    AnityGraphicsDevice*, const AnityGraphicsCameraMeshDrawDesc*);
extern "C" AnityResult AnityGraphics_CreateMetal(const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**);
extern "C" void AnityGraphics_Metal_Destroy(AnityGraphicsDevice*);
extern "C" AnityResult AnityGraphics_Metal_CreateSwapchain(AnityGraphicsDevice*, const AnitySwapchainDesc*, AnitySwapchain**);
extern "C" void AnityGraphics_Metal_DestroySwapchain(AnitySwapchain*);
extern "C" AnityResult AnityGraphics_Metal_Acquire(AnitySwapchain*, int32_t*);
extern "C" AnityResult AnityGraphics_Metal_Present(AnitySwapchain*);
extern "C" int32_t AnityGraphics_Metal_SwapchainHasNativeLayer(const AnitySwapchain*);
extern "C" AnityResult AnityGraphics_Metal_ReadbackSwapchainRGBA8(
    AnitySwapchain*, uint8_t*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_Metal_ReadbackSwapchainToneMappedRGBA8(
    AnitySwapchain*, uint8_t*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_Metal_ExecuteCameraPass(
    AnityGraphicsDevice*, const AnityGraphicsCameraPassDesc*);
extern "C" AnityResult AnityGraphics_Metal_EnsureCameraRenderTarget(
    AnityGraphicsDevice*, const AnityGraphicsCameraRenderTargetDesc*);
extern "C" void AnityGraphics_Metal_DestroyCameraRenderTarget(
    AnityGraphicsDevice*, uint64_t);
extern "C" AnityResult AnityGraphics_Metal_ReadbackCameraRenderTargetRGBA8(
    AnityGraphicsDevice*, uint64_t, uint8_t*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_Metal_ReadbackCameraRenderTargetSliceRGBA8(
    AnityGraphicsDevice*, uint64_t, int32_t, uint8_t*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_Metal_ReadbackCameraRenderTargetToneMappedRGBA8(
    AnityGraphicsDevice*, uint64_t, uint8_t*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_Metal_ReadbackCameraRenderTargetToneMappedSliceRGBA8(
    AnityGraphicsDevice*, uint64_t, int32_t, uint8_t*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_Metal_CopyCameraRenderTargetColor(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Metal_CopyCameraRenderTargetColorSlice(
    AnityGraphicsDevice*, uint64_t, int32_t, int32_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Metal_CopyCameraRenderTargetDepthToColor(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Metal_CopyCameraRenderTargetDepthToColorSlice(
    AnityGraphicsDevice*, uint64_t, int32_t, int32_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Metal_CopyCameraRenderTargetNormalsToColor(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Metal_CopyCameraRenderTargetNormalsToColorSlice(
    AnityGraphicsDevice*, uint64_t, int32_t, int32_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Metal_CopyCameraRenderTargetMotionToColor(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Metal_CopyCameraRenderTargetMotionToColorSlice(
    AnityGraphicsDevice*, uint64_t, int32_t, int32_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_Metal_DrawCameraMesh(
    AnityGraphicsDevice*, const AnityGraphicsCameraMeshDrawDesc*);
extern "C" AnityResult AnityGraphics_Metal_ProcessCameraRenderTargetHDR(
    AnityGraphicsDevice*, uint64_t, const AnityHDRColorGrade*);
extern "C" AnityResult AnityGraphics_Metal_GetHDRPostProcessStats(
    const AnityGraphicsDevice*, AnityGraphicsHDRPostProcessStats*);
extern "C" AnityResult AnityGraphics_Metal_ProcessSwapchainHDR(
    AnitySwapchain*, const AnityHDRColorGrade*);
extern "C" AnityResult AnityGraphics_D3D11_ReadbackSwapchainRGBA8(
    AnitySwapchain*, uint8_t*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_D3D11_ExecuteCameraPass(
    AnityGraphicsDevice*, const AnityGraphicsCameraPassDesc*);
extern "C" AnityResult AnityGraphics_D3D11_EnsureCameraRenderTarget(
    AnityGraphicsDevice*, const AnityGraphicsCameraRenderTargetDesc*);
extern "C" void AnityGraphics_D3D11_DestroyCameraRenderTarget(
    AnityGraphicsDevice*, uint64_t);
extern "C" AnityResult AnityGraphics_D3D11_ReadbackCameraRenderTargetRGBA8(
    AnityGraphicsDevice*, uint64_t, uint8_t*, int32_t, int32_t*);
extern "C" AnityResult AnityGraphics_D3D11_CopyCameraRenderTargetColor(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t);
extern "C" AnityResult AnityGraphics_D3D11_CopyCameraRenderTargetDepthToColor(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t);
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

AnityResult ANITY_CALL AnityGraphics_RecordCameraPass(
    AnityGraphicsDevice* device, const AnityGraphicsCameraPassDesc* desc,
    AnityGraphicsCameraPassInfo* outInfo) {
  if (!device || !desc) return ANITY_ERR_INVALID_ARG;
  AnityGraphicsCameraPassDesc normalized = *desc;
  // Existing managed callers that zero-initialize the appended ABI field are
  // the original one-slice contract. Preserve that binary behavior.
  if (normalized.depthSliceCount == 0) normalized.depthSliceCount = 1;
  if (normalized.targetWidth <= 0 || normalized.targetHeight <= 0 ||
      normalized.viewportWidth < 0.0f || normalized.viewportHeight < 0.0f ||
      (normalized.msaaSamples != 1 && normalized.msaaSamples != 2 &&
       normalized.msaaSamples != 4 && normalized.msaaSamples != 8) ||
      !std::isfinite(normalized.viewportX) || !std::isfinite(normalized.viewportY) ||
      !std::isfinite(normalized.viewportWidth) || !std::isfinite(normalized.viewportHeight) ||
      !std::isfinite(normalized.clearR) || !std::isfinite(normalized.clearG) ||
      !std::isfinite(normalized.clearB) || !std::isfinite(normalized.clearA) ||
      !std::isfinite(normalized.clearDepth) || normalized.depthSlice < 0 ||
      normalized.depthSliceCount < 1 || normalized.depthSliceCount > 2) return ANITY_ERR_INVALID_ARG;

  AnityGraphicsCameraPassInfo info{};
  info.frameId = device->frameId;
  info.sequence = ++device->cameraPassSequence;
  info.desc = normalized;
  device->lastCameraPass = info;
  device->hasCameraPass = 1;
  if (device->type == ANITY_GFX_METAL) {
    const AnityResult backendResult =
        AnityGraphics_Metal_ExecuteCameraPass(device, &normalized);
    if (backendResult != ANITY_OK) return backendResult;
  }
  if (device->type == ANITY_GFX_D3D11) {
    const AnityResult backendResult = AnityGraphics_D3D11_ExecuteCameraPass(device, &normalized);
    if (backendResult != ANITY_OK) return backendResult;
  }
#if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN) {
    const AnityResult backendResult = AnityGraphics_Vulkan_ExecuteCameraPass(device, &normalized);
    if (backendResult != ANITY_OK) return backendResult;
  }
#endif
  if (outInfo) *outInfo = info;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_EnsureCameraRenderTarget(
    AnityGraphicsDevice* device, const AnityGraphicsCameraRenderTargetDesc* desc) {
  if (!device || !desc) return ANITY_ERR_INVALID_ARG;
  AnityGraphicsCameraRenderTargetDesc normalized = *desc;
  if (normalized.dimension == 0) normalized.dimension = 2;
  if (normalized.volumeDepth == 0) normalized.volumeDepth = 1;
  if (normalized.targetId == 0 || normalized.width <= 0 ||
      normalized.height <= 0 || (normalized.msaaSamples != 1 && normalized.msaaSamples != 2 &&
      normalized.msaaSamples != 4 && normalized.msaaSamples != 8) ||
      (normalized.hdrEnabled != 0 && normalized.hdrEnabled != 1) ||
      (normalized.colorFormat < 0 || normalized.colorFormat > 2) ||
      (normalized.colorFormat != 0 && normalized.hdrEnabled != 0) ||
      (normalized.dimension != 2 && normalized.dimension != 5) ||
      normalized.volumeDepth <= 0 ||
      (normalized.dimension == 2 && normalized.volumeDepth != 1))
    return ANITY_ERR_INVALID_ARG;
  // Signed-normalized render attachments are currently implemented by the
  // Metal URP backend only. Do not silently allocate an UNorm substitute.
  if (normalized.colorFormat != 0 && device->type != ANITY_GFX_METAL)
    return ANITY_ERR_NOT_SUPPORTED;
  // Tex2DArray / XR eye-slice targets remain enabled only after their backend
  // passes the per-layer command-submission gate. Other backends must reject
  // them rather than rendering every eye into layer zero.
  if (normalized.dimension == 5 && device->type != ANITY_GFX_METAL &&
      device->type != ANITY_GFX_VULKAN)
    return ANITY_ERR_NOT_SUPPORTED;
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_EnsureCameraRenderTarget(device, &normalized);
  if (device->type == ANITY_GFX_D3D11)
    return AnityGraphics_D3D11_EnsureCameraRenderTarget(device, &normalized);
#if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_EnsureCameraRenderTarget(device, &normalized);
#endif
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_DestroyCameraRenderTarget(
    AnityGraphicsDevice* device, uint64_t targetId) {
  if (!device || targetId == 0) return ANITY_ERR_INVALID_ARG;
  if (device->type == ANITY_GFX_METAL) {
    AnityGraphics_Metal_DestroyCameraRenderTarget(device, targetId);
    return ANITY_OK;
  }
  if (device->type == ANITY_GFX_D3D11) {
    AnityGraphics_D3D11_DestroyCameraRenderTarget(device, targetId);
    return ANITY_OK;
  }
#if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN) {
    AnityGraphics_Vulkan_DestroyCameraRenderTarget(device, targetId);
    return ANITY_OK;
  }
#endif
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_ReadbackCameraRenderTargetRGBA8(
    AnityGraphicsDevice* device, uint64_t targetId, uint8_t* pixels,
    int32_t pixelCapacity, int32_t* outWritten) {
  if (!device || targetId == 0 || pixelCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_ReadbackCameraRenderTargetRGBA8(
        device, targetId, pixels, pixelCapacity, outWritten);
  if (device->type == ANITY_GFX_D3D11)
    return AnityGraphics_D3D11_ReadbackCameraRenderTargetRGBA8(
        device, targetId, pixels, pixelCapacity, outWritten);
#if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_ReadbackCameraRenderTargetRGBA8(
        device, targetId, pixels, pixelCapacity, outWritten);
#endif
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_ReadbackCameraRenderTargetSliceRGBA8(
    AnityGraphicsDevice* device, uint64_t targetId, int32_t depthSlice,
    uint8_t* pixels, int32_t pixelCapacity, int32_t* outWritten) {
  if (!device || targetId == 0 || depthSlice < 0 || pixelCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_ReadbackCameraRenderTargetSliceRGBA8(
        device, targetId, depthSlice, pixels, pixelCapacity, outWritten);
 #if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_ReadbackCameraRenderTargetSliceRGBA8(
        device, targetId, depthSlice, pixels, pixelCapacity, outWritten);
 #endif
  // D3D11 currently retains the original layer-zero path.
  if (depthSlice == 0)
    return AnityGraphics_ReadbackCameraRenderTargetRGBA8(
        device, targetId, pixels, pixelCapacity, outWritten);
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_ReadbackCameraRenderTargetToneMappedRGBA8(
    AnityGraphicsDevice* device, uint64_t targetId, uint8_t* pixels,
    int32_t pixelCapacity, int32_t* outWritten) {
  if (!device || targetId == 0 || pixelCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_ReadbackCameraRenderTargetToneMappedRGBA8(
        device, targetId, pixels, pixelCapacity, outWritten);
  // D3D11 currently exposes only the LDR target path; for an RGBA8 target
  // its stored pixels are already the presentation/tone-mapped result.
  if (device->type == ANITY_GFX_D3D11)
    return AnityGraphics_D3D11_ReadbackCameraRenderTargetRGBA8(
        device, targetId, pixels, pixelCapacity, outWritten);
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_ReadbackCameraRenderTargetToneMappedSliceRGBA8(
    AnityGraphicsDevice* device, uint64_t targetId, int32_t depthSlice,
    uint8_t* pixels, int32_t pixelCapacity, int32_t* outWritten) {
  if (!device || targetId == 0 || depthSlice < 0 || pixelCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_ReadbackCameraRenderTargetToneMappedSliceRGBA8(
        device, targetId, depthSlice, pixels, pixelCapacity, outWritten);
  if (depthSlice == 0)
    return AnityGraphics_ReadbackCameraRenderTargetToneMappedRGBA8(
        device, targetId, pixels, pixelCapacity, outWritten);
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetColor(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, uint64_t destinationTargetId) {
  if (!device || destinationTargetId == 0 ||
      (sourceIsCameraTarget != 0 && sourceIsCameraTarget != 1) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == 0) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == destinationTargetId))
    return ANITY_ERR_INVALID_ARG;
#if defined(ANITY_HAS_METAL)
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_CopyCameraRenderTargetColor(
        device, sourceTargetId, sourceIsCameraTarget, destinationTargetId);
#endif
  if (device->type == ANITY_GFX_D3D11)
    return AnityGraphics_D3D11_CopyCameraRenderTargetColor(
        device, sourceTargetId, sourceIsCameraTarget, destinationTargetId);
#if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_CopyCameraRenderTargetColor(
        device, sourceTargetId, sourceIsCameraTarget, destinationTargetId);
#endif
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetColorSlice(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, int32_t sourceSlice, int32_t destinationSlice,
    uint64_t destinationTargetId) {
  if (!device || destinationTargetId == 0 || sourceSlice < 0 ||
      destinationSlice < 0 ||
      (sourceIsCameraTarget != 0 && sourceIsCameraTarget != 1) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == 0) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == destinationTargetId))
    return ANITY_ERR_INVALID_ARG;
#if defined(ANITY_HAS_METAL)
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_CopyCameraRenderTargetColorSlice(
        device, sourceTargetId, sourceIsCameraTarget, sourceSlice, destinationSlice,
        destinationTargetId);
#endif
#if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_CopyCameraRenderTargetColorSlice(
        device, sourceTargetId, sourceIsCameraTarget, sourceSlice, destinationSlice,
        destinationTargetId);
#endif
  if (sourceSlice == 0 && destinationSlice == 0)
    return AnityGraphics_CopyCameraRenderTargetColor(
        device, sourceTargetId, sourceIsCameraTarget, destinationTargetId);
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetDepthToColor(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, uint64_t destinationTargetId) {
  if (!device || destinationTargetId == 0 ||
      (sourceIsCameraTarget != 0 && sourceIsCameraTarget != 1) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == 0) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == destinationTargetId))
    return ANITY_ERR_INVALID_ARG;
#if defined(ANITY_HAS_METAL)
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_CopyCameraRenderTargetDepthToColor(
        device, sourceTargetId, sourceIsCameraTarget, destinationTargetId);
#endif
#if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_CopyCameraRenderTargetDepthToColor(
        device, sourceTargetId, sourceIsCameraTarget, destinationTargetId);
#endif
  if (device->type == ANITY_GFX_D3D11)
    return AnityGraphics_D3D11_CopyCameraRenderTargetDepthToColor(
        device, sourceTargetId, sourceIsCameraTarget, destinationTargetId);
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetDepthToColorSlice(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, int32_t sourceSlice, int32_t destinationSlice,
    uint64_t destinationTargetId) {
  if (!device || destinationTargetId == 0 || sourceSlice < 0 || destinationSlice < 0 ||
      (sourceIsCameraTarget != 0 && sourceIsCameraTarget != 1) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == 0) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == destinationTargetId))
    return ANITY_ERR_INVALID_ARG;
#if defined(ANITY_HAS_METAL)
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_CopyCameraRenderTargetDepthToColorSlice(
        device, sourceTargetId, sourceIsCameraTarget, sourceSlice, destinationSlice,
        destinationTargetId);
#endif
#if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_CopyCameraRenderTargetDepthToColorSlice(
        device, sourceTargetId, sourceIsCameraTarget, sourceSlice, destinationSlice,
        destinationTargetId);
#endif
  if (sourceSlice == 0 && destinationSlice == 0)
    return AnityGraphics_CopyCameraRenderTargetDepthToColor(
        device, sourceTargetId, sourceIsCameraTarget, destinationTargetId);
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetNormalsToColor(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, uint64_t destinationTargetId) {
  if (!device || destinationTargetId == 0 ||
      (sourceIsCameraTarget != 0 && sourceIsCameraTarget != 1) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == 0) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == destinationTargetId))
    return ANITY_ERR_INVALID_ARG;
#if defined(ANITY_HAS_METAL)
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_CopyCameraRenderTargetNormalsToColor(
        device, sourceTargetId, sourceIsCameraTarget, destinationTargetId);
#endif
#if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_CopyCameraRenderTargetNormalsToColor(
        device, sourceTargetId, sourceIsCameraTarget, destinationTargetId);
#endif
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetNormalsToColorSlice(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, int32_t sourceSlice, int32_t destinationSlice,
    uint64_t destinationTargetId) {
  if (!device || destinationTargetId == 0 || sourceSlice < 0 || destinationSlice < 0 ||
      (sourceIsCameraTarget != 0 && sourceIsCameraTarget != 1) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == 0) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == destinationTargetId))
    return ANITY_ERR_INVALID_ARG;
#if defined(ANITY_HAS_METAL)
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_CopyCameraRenderTargetNormalsToColorSlice(
        device, sourceTargetId, sourceIsCameraTarget, sourceSlice, destinationSlice,
        destinationTargetId);
#endif
#if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_CopyCameraRenderTargetNormalsToColorSlice(
        device, sourceTargetId, sourceIsCameraTarget, sourceSlice, destinationSlice,
        destinationTargetId);
#endif
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetMotionToColor(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, uint64_t destinationTargetId) {
  if (!device || destinationTargetId == 0 ||
      (sourceIsCameraTarget != 0 && sourceIsCameraTarget != 1) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == 0) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == destinationTargetId))
    return ANITY_ERR_INVALID_ARG;
#if defined(ANITY_HAS_METAL)
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_CopyCameraRenderTargetMotionToColor(
        device, sourceTargetId, sourceIsCameraTarget, destinationTargetId);
#endif
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetMotionToColorSlice(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, int32_t sourceSlice, int32_t destinationSlice,
    uint64_t destinationTargetId) {
  if (!device || destinationTargetId == 0 || sourceSlice < 0 || destinationSlice < 0 ||
      (sourceIsCameraTarget != 0 && sourceIsCameraTarget != 1) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == 0) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == destinationTargetId))
    return ANITY_ERR_INVALID_ARG;
#if defined(ANITY_HAS_METAL)
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_CopyCameraRenderTargetMotionToColorSlice(
        device, sourceTargetId, sourceIsCameraTarget, sourceSlice, destinationSlice,
        destinationTargetId);
#endif
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_DrawCameraMesh(
    AnityGraphicsDevice* device, const AnityGraphicsCameraMeshDrawDesc* desc) {
  if (!device || !desc) return ANITY_ERR_INVALID_ARG;
  AnityGraphicsCameraMeshDrawDesc normalized = *desc;
  if (normalized.stereoInstanceCount == 0) normalized.stereoInstanceCount = 1;
  if ((normalized.targetId == 0 && normalized.targetIsCameraTarget == 0) ||
      (normalized.targetIsCameraTarget != 0 && normalized.targetIsCameraTarget != 1) || !normalized.vertices ||
      normalized.blendMode < 0 || normalized.blendMode > 4 ||
      (normalized.depthWriteEnabled != 0 && normalized.depthWriteEnabled != 1) ||
      (normalized.alphaClipEnabled != 0 && normalized.alphaClipEnabled != 1) ||
      normalized.depthSlice < 0 ||
      normalized.vertexCount <= 0 || !normalized.indices || normalized.indexCount <= 0 ||
      (normalized.indexCount % 3) != 0 ||
      (normalized.hasPreviousObjectToClip != 0 && normalized.hasPreviousObjectToClip != 1))
    return ANITY_ERR_INVALID_ARG;
#if defined(ANITY_HAS_METAL)
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_DrawCameraMesh(device, &normalized);
#endif
#if defined(ANITY_HAS_VULKAN)
  if (device->type == ANITY_GFX_VULKAN)
    return AnityGraphics_Vulkan_DrawCameraMesh(device, &normalized);
#endif
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_SkinMeshVertices(
    const AnityGraphicsSkinMeshDesc* desc) {
  if (!desc || !desc->sourceVertices || !desc->boneMatrices ||
      !desc->outVertices || desc->vertexCount <= 0 || desc->boneCount <= 0 ||
      desc->outVertexCount < desc->vertexCount)
    return ANITY_ERR_INVALID_ARG;
  const bool variableInfluences = desc->bonesPerVertex != nullptr || desc->allBoneWeights != nullptr;
  if (variableInfluences) {
    if (!desc->bonesPerVertex || !desc->allBoneWeights || desc->allBoneWeightCount < 0 ||
        desc->maxInfluences < 1 || desc->maxInfluences > 8) return ANITY_ERR_INVALID_ARG;
  } else if (!desc->boneWeights) {
    return ANITY_ERR_INVALID_ARG;
  }
  const auto finite3 = [](const float* value) {
    return std::isfinite(value[0]) && std::isfinite(value[1]) && std::isfinite(value[2]);
  };
  const auto transformPoint = [](const float* m, const float* value, float* out) {
    out[0] = m[0] * value[0] + m[1] * value[1] + m[2] * value[2] + m[3];
    out[1] = m[4] * value[0] + m[5] * value[1] + m[6] * value[2] + m[7];
    out[2] = m[8] * value[0] + m[9] * value[1] + m[10] * value[2] + m[11];
  };
  const auto transformVector = [](const float* m, const float* value, float* out) {
    out[0] = m[0] * value[0] + m[1] * value[1] + m[2] * value[2];
    out[1] = m[4] * value[0] + m[5] * value[1] + m[6] * value[2];
    out[2] = m[8] * value[0] + m[9] * value[1] + m[10] * value[2];
  };
  const auto normalize = [](float* value, const float* fallback) {
    const float lengthSq = value[0] * value[0] + value[1] * value[1] + value[2] * value[2];
    if (!std::isfinite(lengthSq) || lengthSq <= 1e-20f) {
      value[0] = fallback[0]; value[1] = fallback[1]; value[2] = fallback[2];
      return;
    }
    const float invLength = 1.0f / std::sqrt(lengthSq);
    value[0] *= invLength; value[1] *= invLength; value[2] *= invLength;
  };
  int32_t variableOffset = 0;
  for (int32_t vertexIndex = 0; vertexIndex < desc->vertexCount; ++vertexIndex) {
    const AnityGraphicsSkinVertex& source = desc->sourceVertices[vertexIndex];
    if (!finite3(source.position) || !finite3(source.normal) || !finite3(source.tangent))
      return ANITY_ERR_INVALID_ARG;
    AnityGraphicsSkinVertex result{};
    result.tangent[3] = source.tangent[3];
    float totalWeight = 0.0f;
    const int32_t influenceCount = variableInfluences ? static_cast<int32_t>(desc->bonesPerVertex[vertexIndex]) : 4;
    if (variableInfluences && (influenceCount > 8 || variableOffset > desc->allBoneWeightCount - influenceCount))
      return ANITY_ERR_INVALID_ARG;
    for (int influence = 0; influence < influenceCount; ++influence) {
      const float influenceWeight = variableInfluences
          ? desc->allBoneWeights[variableOffset + influence].weight
          : desc->boneWeights[vertexIndex].weight[influence];
      const int32_t boneIndex = variableInfluences
          ? desc->allBoneWeights[variableOffset + influence].boneIndex
          : desc->boneWeights[vertexIndex].boneIndex[influence];
      if (!std::isfinite(influenceWeight) || influenceWeight < 0.0f) return ANITY_ERR_INVALID_ARG;
      if (influenceWeight == 0.0f) continue;
      if (variableInfluences && influence >= desc->maxInfluences) continue;
      if (boneIndex < 0 || boneIndex >= desc->boneCount) return ANITY_ERR_INVALID_ARG;
      const float* matrix = desc->boneMatrices + static_cast<size_t>(boneIndex) * 16u;
      float position[3], normal[3], tangent[3];
      transformPoint(matrix, source.position, position);
      transformVector(matrix, source.normal, normal);
      transformVector(matrix, source.tangent, tangent);
      for (int component = 0; component < 3; ++component) {
        result.position[component] += position[component] * influenceWeight;
        result.normal[component] += normal[component] * influenceWeight;
        result.tangent[component] += tangent[component] * influenceWeight;
      }
      totalWeight += influenceWeight;
    }
    if (variableInfluences) variableOffset += influenceCount;
    if (totalWeight <= 1e-20f) {
      result = source;
    } else {
      const float reciprocal = 1.0f / totalWeight;
      for (int component = 0; component < 3; ++component) result.position[component] *= reciprocal;
      normalize(result.normal, source.normal);
      normalize(result.tangent, source.tangent);
    }
    desc->outVertices[vertexIndex] = result;
  }
  if (variableInfluences && variableOffset != desc->allBoneWeightCount)
    return ANITY_ERR_INVALID_ARG;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_ApplyBlendShapeDeltas(
    const AnityGraphicsBlendShapeDesc* desc) {
  if (!desc || !desc->sourceVertices || !desc->shapeDeltas || !desc->outVertices ||
      desc->vertexCount <= 0 || desc->shapeCount <= 0 || desc->outVertexCount < desc->vertexCount)
    return ANITY_ERR_INVALID_ARG;
  const auto finite3 = [](const float* value) {
    return std::isfinite(value[0]) && std::isfinite(value[1]) && std::isfinite(value[2]);
  };
  for (int32_t vertexIndex = 0; vertexIndex < desc->vertexCount; ++vertexIndex) {
    const AnityGraphicsSkinVertex& source = desc->sourceVertices[vertexIndex];
    if (!finite3(source.position) || !finite3(source.normal) || !finite3(source.tangent))
      return ANITY_ERR_INVALID_ARG;
    AnityGraphicsSkinVertex result = source;
    for (int32_t shapeIndex = 0; shapeIndex < desc->shapeCount; ++shapeIndex) {
      const AnityGraphicsSkinVertex& delta = desc->shapeDeltas[
          static_cast<size_t>(shapeIndex) * static_cast<size_t>(desc->vertexCount) +
          static_cast<size_t>(vertexIndex)];
      if (!finite3(delta.position) || !finite3(delta.normal) || !finite3(delta.tangent))
        return ANITY_ERR_INVALID_ARG;
      for (int component = 0; component < 3; ++component) {
        result.position[component] += delta.position[component];
        result.normal[component] += delta.normal[component];
        result.tangent[component] += delta.tangent[component];
      }
    }
    result.tangent[3] = source.tangent[3];
    desc->outVertices[vertexIndex] = result;
  }
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_ProcessCameraRenderTargetHDR(
    AnityGraphicsDevice* device, uint64_t targetId,
    const AnityHDRColorGrade* grade) {
  if (!device || targetId == 0 || !grade) return ANITY_ERR_INVALID_ARG;
#if defined(ANITY_HAS_METAL)
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_ProcessCameraRenderTargetHDR(device, targetId, grade);
#endif
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_GetHDRPostProcessStats(
    const AnityGraphicsDevice* device,
    AnityGraphicsHDRPostProcessStats* outStats) {
  if (!device || !outStats) return ANITY_ERR_INVALID_ARG;
#if defined(ANITY_HAS_METAL)
  if (device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_GetHDRPostProcessStats(device, outStats);
#endif
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_GetLastCameraPass(
    const AnityGraphicsDevice* device, AnityGraphicsCameraPassInfo* outInfo) {
  if (!device || !outInfo) return ANITY_ERR_INVALID_ARG;
  if (!device->hasCameraPass) return ANITY_ERR_NOT_SUPPORTED;
  *outInfo = device->lastCameraPass;
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
    /* A live Metal device must surface attachment/surface validation errors
     * instead of falling through to the generic CPU headless swapchain. */
    if (device->backend) return r;
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

AnityResult ANITY_CALL AnityGraphics_ReadbackSwapchainToneMappedRGBA8(
    AnitySwapchain* swapchain, uint8_t* pixels, int32_t pixelCapacity,
    int32_t* outWritten) {
  if (!swapchain || pixelCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  if (swapchain->device && swapchain->device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_ReadbackSwapchainToneMappedRGBA8(
        swapchain, pixels, pixelCapacity, outWritten);
  *outWritten = 0;
  return ANITY_ERR_NOT_SUPPORTED;
}

AnityResult ANITY_CALL AnityGraphics_ProcessSwapchainHDR(
    AnitySwapchain* swapchain, const AnityHDRColorGrade* grade) {
  if (!swapchain || !grade) return ANITY_ERR_INVALID_ARG;
#if defined(ANITY_HAS_METAL)
  if (swapchain->device && swapchain->device->type == ANITY_GFX_METAL)
    return AnityGraphics_Metal_ProcessSwapchainHDR(swapchain, grade);
#endif
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
