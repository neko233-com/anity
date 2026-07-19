#pragma once
#include "../anity_core.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* UnityEngine.HDROutputSettings / ColorGamut aligned */
typedef enum AnityColorGamut {
  ANITY_GAMUT_SRGB = 0,
  ANITY_GAMUT_REC709 = 1,
  ANITY_GAMUT_REC2020 = 2,
  ANITY_GAMUT_DISPLAY_P3 = 3,
  ANITY_GAMUT_HDR10 = 4,
  ANITY_GAMUT_DOLBY_HDR = 5,
  ANITY_GAMUT_HDR10_PLUS = 6
} AnityColorGamut;

typedef enum AnityHDRDisplayBitDepth {
  ANITY_HDR_BITDEPTH_8 = 0,
  ANITY_HDR_BITDEPTH_10 = 1,
  ANITY_HDR_BITDEPTH_16 = 2
} AnityHDRDisplayBitDepth;

typedef enum AnityTonemappingMode {
  ANITY_TONEMAP_NONE = 0,
  ANITY_TONEMAP_NEUTRAL = 1,
  ANITY_TONEMAP_ACES = 2
} AnityTonemappingMode;

typedef struct AnityHDRDisplayInfo {
  int32_t available;
  int32_t active;
  AnityColorGamut displayColorGamut;
  AnityHDRDisplayBitDepth bitsPerColorComponent;
  float maxFullFrameToneMapLuminance; /* nits */
  float maxToneMapLuminance;
  float minToneMapLuminance;
  float paperWhiteNits;
  int32_t automaticHDRTonemapping;
} AnityHDRDisplayInfo;

typedef struct AnityHDRColorGrade {
  float postExposure;   /* EV */
  float contrast;       /* -100..100 */
  float saturation;     /* -100..100 */
  float temperature;    /* -100..100 */
  float tint;           /* -100..100 */
  float hueShift;       /* -180..180 degrees */
  float colorFilterR;   /* linear RGB multiplier */
  float colorFilterG;
  float colorFilterB;
  /* Output rows of the linear RGB channel mixer. */
  float mixerRedR;
  float mixerRedG;
  float mixerRedB;
  float mixerGreenR;
  float mixerGreenG;
  float mixerGreenB;
  float mixerBlueR;
  float mixerBlueG;
  float mixerBlueB;
  /* Eight baked 128-sample curves: master/R/G/B plus HSV/Luma variants. */
  int32_t curveEnabled;
  float curveLut[1024];
  float bloomThreshold;
  float bloomIntensity;
  float bloomScatter;
  int32_t bloomMaxIterations;
  int32_t bloomDownscale; /* 0=half, 1=quarter */
  int32_t bloomHighQualityFiltering;
  float bloomTintR;
  float bloomTintG;
  float bloomTintB;
  uint64_t bloomDirtTextureId;
  float bloomDirtIntensity;
  AnityTonemappingMode tonemapMode;
} AnityHDRColorGrade;

ANITY_API AnityResult ANITY_CALL AnityHDR_QueryDisplay(AnityHDRDisplayInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityHDR_SetActive(int32_t active);
ANITY_API AnityResult ANITY_CALL AnityHDR_SetPaperWhiteNits(float nits);
ANITY_API AnityResult ANITY_CALL AnityHDR_SetAutomaticTonemapping(int32_t enabled);

/* Soft HDR path: tonemap + grade into sRGB or HDR10 buffer (CPU reference path) */
ANITY_API AnityResult ANITY_CALL AnityHDR_ProcessFrame(
    const float* rgbaHdr, int32_t width, int32_t height,
    const AnityHDRColorGrade* grade,
    float* rgbaOut,
    int32_t outHdr10);

/* CPU reference variant for URP Bloom Lens Dirt. Pixels are a tightly packed
 * base RGBA8 level; filter and wrap values use Unity's FilterMode and
 * TextureWrapMode integer values, and linear is 1 for linear texture data. */
ANITY_API AnityResult ANITY_CALL AnityHDR_ProcessFrameWithLensDirtRGBA8(
    const float* rgbaHdr, int32_t width, int32_t height,
    const AnityHDRColorGrade* grade,
    const uint8_t* dirtRgba8, int32_t dirtWidth, int32_t dirtHeight,
    int32_t dirtFilterMode, int32_t dirtWrapU, int32_t dirtWrapV,
    int32_t dirtLinear, int32_t dirtByteCount,
    float* rgbaOut,
    int32_t outHdr10);

/* Mip-aware CPU Lens Dirt path. dirtRgba8 packs mip 0 through dirtMipCount-1
 * consecutively; each level uses max(1, baseDimension >> level). Point and
 * Bilinear choose the derivative-selected level, while Trilinear blends the
 * adjacent levels. */
ANITY_API AnityResult ANITY_CALL AnityHDR_ProcessFrameWithLensDirtRGBA8Mips(
    const float* rgbaHdr, int32_t width, int32_t height,
    const AnityHDRColorGrade* grade,
    const uint8_t* dirtRgba8, int32_t dirtWidth, int32_t dirtHeight,
    int32_t dirtMipCount, int32_t dirtFilterMode, int32_t dirtWrapU,
    int32_t dirtWrapV, int32_t dirtLinear, int32_t dirtByteCount,
    float* rgbaOut, int32_t outHdr10);

/* Same packed-mip path with Unity Texture.mipMapBias applied to the
 * derivative-selected level before Point/Bilinear/Trilinear sampling. */
ANITY_API AnityResult ANITY_CALL AnityHDR_ProcessFrameWithLensDirtRGBA8MipsBias(
    const float* rgbaHdr, int32_t width, int32_t height,
    const AnityHDRColorGrade* grade,
    const uint8_t* dirtRgba8, int32_t dirtWidth, int32_t dirtHeight,
    int32_t dirtMipCount, int32_t dirtFilterMode, int32_t dirtWrapU,
    int32_t dirtWrapV, int32_t dirtLinear, float dirtMipBias,
    int32_t dirtByteCount, float* rgbaOut, int32_t outHdr10);

/* Linear <-> sRGB (Unity Color/Mathf aligned) */
ANITY_API float ANITY_CALL AnityHDR_LinearToGammaSpace(float value);
ANITY_API float ANITY_CALL AnityHDR_GammaToLinearSpace(float value);

#ifdef __cplusplus
}
#endif
