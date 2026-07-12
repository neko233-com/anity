#pragma once
#include "../anity_core.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Matches UnityEngine.TextureFormat subset for compressed formats */
typedef enum AnityTextureFormat {
  ANITY_TEX_RGBA32 = 4,
  ANITY_TEX_DXT1 = 10,
  ANITY_TEX_DXT5 = 12,
  ANITY_TEX_ETC_RGB4 = 34,
  ANITY_TEX_ETC2_RGB = 45,
  ANITY_TEX_ETC2_RGBA8 = 47,
  ANITY_TEX_ASTC_4x4 = 52,
  ANITY_TEX_ASTC_6x6 = 54,
  ANITY_TEX_ASTC_8x8 = 55
} AnityTextureFormat;

ANITY_API int32_t ANITY_CALL AnityTexture_CalculateImageSize(
    int32_t width, int32_t height, AnityTextureFormat format);

/* Soft block compress (correct size; not bit-exact GPU encoder) */
ANITY_API AnityResult ANITY_CALL AnityTexture_CompressRGBA8(
    const uint8_t* rgba, int32_t width, int32_t height,
    AnityTextureFormat format,
    uint8_t* outBuffer, int32_t outBufferSize);

#ifdef __cplusplus
}
#endif
