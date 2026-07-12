#define ANITY_NATIVE_BUILD
#include "anity/graphics/anity_hdr.h"
#include <algorithm>
#include <cmath>
#include <cstring>

namespace {

static AnityHDRDisplayInfo g_hdr = {
  /*available*/ 1,
  /*active*/ 0,
  ANITY_GAMUT_SRGB,
  ANITY_HDR_BITDEPTH_10,
  /*maxFullFrame*/ 300.f,
  /*maxToneMap*/ 1000.f,
  /*minToneMap*/ 0.02f,
  /*paperWhite*/ 160.f,
  /*auto*/ 1
};

inline float Clamp01(float v) {
  return v < 0.f ? 0.f : (v > 1.f ? 1.f : v);
}

/* ACES filmic approximation (Narkowicz) */
inline float AcesTonemap(float x) {
  const float a = 2.51f, b = 0.03f, c = 2.43f, d = 0.59f, e = 0.14f;
  return Clamp01((x * (a * x + b)) / (x * (c * x + d) + e));
}

inline float NeutralTonemap(float x) {
  // Unity-ish neutral: soft shoulder
  float y = x / (x + 0.187f) * 1.035f;
  return Clamp01(y);
}

} // namespace

extern "C" {

AnityResult ANITY_CALL AnityHDR_QueryDisplay(AnityHDRDisplayInfo* outInfo) {
  if (!outInfo) return ANITY_ERR_INVALID_ARG;
  *outInfo = g_hdr;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityHDR_SetActive(int32_t active) {
  g_hdr.active = active ? 1 : 0;
  if (g_hdr.active) {
    g_hdr.displayColorGamut = ANITY_GAMUT_HDR10;
    g_hdr.bitsPerColorComponent = ANITY_HDR_BITDEPTH_10;
  } else {
    g_hdr.displayColorGamut = ANITY_GAMUT_SRGB;
  }
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityHDR_SetPaperWhiteNits(float nits) {
  if (nits < 80.f) nits = 80.f;
  if (nits > 400.f) nits = 400.f;
  g_hdr.paperWhiteNits = nits;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityHDR_SetAutomaticTonemapping(int32_t enabled) {
  g_hdr.automaticHDRTonemapping = enabled ? 1 : 0;
  return ANITY_OK;
}

float ANITY_CALL AnityHDR_LinearToGammaSpace(float value) {
  if (value <= 0.0031308f)
    return 12.92f * value;
  return 1.055f * std::pow(value, 1.f / 2.4f) - 0.055f;
}

float ANITY_CALL AnityHDR_GammaToLinearSpace(float value) {
  if (value <= 0.04045f)
    return value / 12.92f;
  return std::pow((value + 0.055f) / 1.055f, 2.4f);
}

AnityResult ANITY_CALL AnityHDR_ProcessFrame(
    const float* rgbaHdr, int32_t width, int32_t height,
    const AnityHDRColorGrade* grade,
    float* rgbaOut,
    int32_t outHdr10) {
  if (!rgbaHdr || !rgbaOut || width <= 0 || height <= 0)
    return ANITY_ERR_INVALID_ARG;

  AnityHDRColorGrade g{};
  if (grade) g = *grade;
  else {
    g.tonemapMode = ANITY_TONEMAP_ACES;
    g.bloomThreshold = 0.9f;
  }

  const float exposure = std::pow(2.f, g.postExposure);
  const int n = width * height;
  for (int i = 0; i < n; ++i) {
    float r = rgbaHdr[i * 4 + 0] * exposure;
    float gr = rgbaHdr[i * 4 + 1] * exposure;
    float b = rgbaHdr[i * 4 + 2] * exposure;
    float a = rgbaHdr[i * 4 + 3];

    // simple bloom contribution (threshold)
    float lum = 0.2126f * r + 0.7152f * gr + 0.0722f * b;
    if (lum > g.bloomThreshold && g.bloomIntensity > 0.f) {
      float bloom = (lum - g.bloomThreshold) * g.bloomIntensity;
      r += bloom; gr += bloom; b += bloom;
    }

    // contrast around mid-gray
    float c = 1.f + g.contrast / 100.f;
    r = (r - 0.18f) * c + 0.18f;
    gr = (gr - 0.18f) * c + 0.18f;
    b = (b - 0.18f) * c + 0.18f;

    switch (g.tonemapMode) {
      case ANITY_TONEMAP_ACES:
        r = AcesTonemap(r); gr = AcesTonemap(gr); b = AcesTonemap(b);
        break;
      case ANITY_TONEMAP_NEUTRAL:
        r = NeutralTonemap(r); gr = NeutralTonemap(gr); b = NeutralTonemap(b);
        break;
      default:
        r = Clamp01(r); gr = Clamp01(gr); b = Clamp01(b);
        break;
    }

    if (!outHdr10) {
      r = AnityHDR_LinearToGammaSpace(r);
      gr = AnityHDR_LinearToGammaSpace(gr);
      b = AnityHDR_LinearToGammaSpace(b);
    } else {
      // scale to approximate PQ-like nits range for 10-bit path (simplified)
      float scale = g_hdr.paperWhiteNits / 80.f;
      r = std::min(r * scale, g_hdr.maxToneMapLuminance / 80.f);
      gr = std::min(gr * scale, g_hdr.maxToneMapLuminance / 80.f);
      b = std::min(b * scale, g_hdr.maxToneMapLuminance / 80.f);
    }

    rgbaOut[i * 4 + 0] = r;
    rgbaOut[i * 4 + 1] = gr;
    rgbaOut[i * 4 + 2] = b;
    rgbaOut[i * 4 + 3] = a;
  }
  return ANITY_OK;
}

} // extern "C"
