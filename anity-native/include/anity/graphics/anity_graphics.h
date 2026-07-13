#pragma once
#include "../anity_core.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Aligns with UnityEngine.Rendering.GraphicsDeviceType values */
typedef enum AnityGraphicsDeviceType {
  ANITY_GFX_NULL = 4,
  ANITY_GFX_D3D11 = 2,
  ANITY_GFX_OPENGLES2 = 8,
  ANITY_GFX_OPENGLES3 = 11,
  ANITY_GFX_METAL = 16,
  ANITY_GFX_OPENGLCORE = 17,
  ANITY_GFX_D3D12 = 18,
  ANITY_GFX_VULKAN = 21,
  ANITY_GFX_WEBGL2 = 28
} AnityGraphicsDeviceType;

typedef struct AnityGraphicsDeviceDesc {
  AnityGraphicsDeviceType preferred;
  int32_t width;
  int32_t height;
  int32_t hdrEnabled;
  int32_t msaaSamples;
  int32_t vsync;
  void* nativeWindow;
} AnityGraphicsDeviceDesc;

typedef struct AnityGraphicsDevice AnityGraphicsDevice;

ANITY_API AnityResult ANITY_CALL AnityGraphics_CreateDevice(
    const AnityGraphicsDeviceDesc* desc,
    AnityGraphicsDevice** outDevice);

ANITY_API void ANITY_CALL AnityGraphics_DestroyDevice(AnityGraphicsDevice* device);

ANITY_API AnityGraphicsDeviceType ANITY_CALL AnityGraphics_GetDeviceType(
    const AnityGraphicsDevice* device);

ANITY_API AnityResult ANITY_CALL AnityGraphics_BeginFrame(AnityGraphicsDevice* device);
ANITY_API AnityResult ANITY_CALL AnityGraphics_EndFrame(AnityGraphicsDevice* device);
ANITY_API AnityResult ANITY_CALL AnityGraphics_Present(AnityGraphicsDevice* device);

ANITY_API AnityResult ANITY_CALL AnityGraphics_Resize(
    AnityGraphicsDevice* device, int32_t width, int32_t height);

ANITY_API int32_t ANITY_CALL AnityGraphics_SupportsHDR(const AnityGraphicsDevice* device);

ANITY_API AnityGraphicsDeviceType ANITY_CALL AnityGraphics_GetDefaultDeviceType(
    AnityPlatform platform);

/* --- Swapchain (Metal / Vulkan / D3D / headless) --- */
typedef struct AnitySwapchainDesc {
  int32_t width;
  int32_t height;
  int32_t imageCount; /* preferred buffer count, 0 = default 2 */
  int32_t vsync;
  int32_t hdr;
  void* nativeWindow; /* HWND / ANativeWindow / CAMetalLayer* / nullptr = headless */
} AnitySwapchainDesc;

typedef struct AnitySwapchain AnitySwapchain;

ANITY_API AnityResult ANITY_CALL AnityGraphics_CreateSwapchain(
    AnityGraphicsDevice* device,
    const AnitySwapchainDesc* desc,
    AnitySwapchain** outSwapchain);

ANITY_API void ANITY_CALL AnityGraphics_DestroySwapchain(AnitySwapchain* swapchain);

ANITY_API AnityResult ANITY_CALL AnityGraphics_AcquireNextImage(
    AnitySwapchain* swapchain, int32_t* outImageIndex);

ANITY_API AnityResult ANITY_CALL AnityGraphics_PresentSwapchain(AnitySwapchain* swapchain);

ANITY_API int32_t ANITY_CALL AnityGraphics_GetSwapchainImageCount(const AnitySwapchain* swapchain);
ANITY_API int32_t ANITY_CALL AnityGraphics_GetSwapchainWidth(const AnitySwapchain* swapchain);
ANITY_API int32_t ANITY_CALL AnityGraphics_GetSwapchainHeight(const AnitySwapchain* swapchain);
ANITY_API int32_t ANITY_CALL AnityGraphics_IsSwapchainHeadless(const AnitySwapchain* swapchain);
ANITY_API int32_t ANITY_CALL AnityGraphics_GetSwapchainPresentCount(const AnitySwapchain* swapchain);
/* 1 if backend created real VkSurface/CAMetalLayer (may still be offscreen) */
ANITY_API int32_t ANITY_CALL AnityGraphics_SwapchainHasNativeSurface(const AnitySwapchain* swapchain);
/* Backend tag: 0=unknown/headless software, 1=Vulkan, 2=Metal, 3=D3D11 */
ANITY_API int32_t ANITY_CALL AnityGraphics_GetSwapchainBackendKind(const AnitySwapchain* swapchain);

/*
 * Vulkan surface kind for active swapchain:
 * 0=none/headless, 1=Win32 HWND, 2=Android ANativeWindow, 3=X11, 4=Wayland
 */
ANITY_API int32_t ANITY_CALL AnityGraphics_GetSwapchainSurfaceKind(const AnitySwapchain* swapchain);

/*
 * Compile-time Vulkan surface platform mask:
 * bit0=Win32, bit1=Android, bit2=X11, bit3=Wayland
 */
ANITY_API int32_t ANITY_CALL AnityGraphics_Vulkan_GetSupportedSurfaceMask(void);

/* Packing for X11: pass as nativeWindow to CreateSwapchain on Linux. */
typedef struct AnityX11NativeWindow {
  void* display;       /* Display* */
  unsigned long window; /* Window XID */
} AnityX11NativeWindow;

/* Packing for Wayland: pass as nativeWindow to CreateSwapchain. */
typedef struct AnityWaylandNativeWindow {
  void* display; /* wl_display* */
  void* surface; /* wl_surface* */
} AnityWaylandNativeWindow;

#ifdef __cplusplus
}
#endif
