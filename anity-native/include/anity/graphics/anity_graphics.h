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

#ifdef __cplusplus
}
#endif
