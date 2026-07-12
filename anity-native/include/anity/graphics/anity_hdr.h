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
  float bloomThreshold;
  float bloomIntensity;
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

/* Linear <-> sRGB (Unity Color/Mathf aligned) */
ANITY_API float ANITY_CALL AnityHDR_LinearToGammaSpace(float value);
ANITY_API float ANITY_CALL AnityHDR_GammaToLinearSpace(float value);

#ifdef __cplusplus
}
#endif
