#define ANITY_NATIVE_BUILD
#include "anity/graphics/anity_hdr.h"
#include <algorithm>
#include <cmath>
#include <cstring>
#include <vector>

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

inline void ApplyWhiteBalance(float* r, float* g, float* b,
    float temperature, float tint) {
  const float warm = std::max(-1.f, std::min(1.f, temperature / 100.f));
  const float magenta = std::max(-1.f, std::min(1.f, tint / 100.f));
  *r *= 1.f + warm * 0.25f;
  *b *= 1.f - warm * 0.25f;
  // Positive Unity tint is magenta (less green); negative is green.
  *g *= 1.f - magenta * 0.15f;
}

inline void ApplySaturation(float* r, float* g, float* b, float saturation) {
  const float amount = std::max(0.f, 1.f + saturation / 100.f);
  const float luminance = 0.2126f * *r + 0.7152f * *g + 0.0722f * *b;
  *r = luminance + (*r - luminance) * amount;
  *g = luminance + (*g - luminance) * amount;
  *b = luminance + (*b - luminance) * amount;
}

inline void ApplyColorFilter(float* r, float* g, float* b,
    float filterR, float filterG, float filterB) {
  *r *= std::max(0.f, filterR);
  *g *= std::max(0.f, filterG);
  *b *= std::max(0.f, filterB);
}

inline void ApplyChannelMixer(float* r, float* g, float* b,
    const AnityHDRColorGrade& grade) {
  const float sourceR = *r, sourceG = *g, sourceB = *b;
  *r = sourceR * grade.mixerRedR + sourceG * grade.mixerRedG +
      sourceB * grade.mixerRedB;
  *g = sourceR * grade.mixerGreenR + sourceG * grade.mixerGreenG +
      sourceB * grade.mixerGreenB;
  *b = sourceR * grade.mixerBlueR + sourceG * grade.mixerBlueG +
      sourceB * grade.mixerBlueB;
}

inline float SampleCurve(const float* samples, float value) {
  constexpr int kCurveSampleCount = 128;
  const float position = Clamp01(value) * static_cast<float>(kCurveSampleCount - 1);
  const int index = static_cast<int>(position);
  const int next = std::min(index + 1, kCurveSampleCount - 1);
  const float fraction = position - static_cast<float>(index);
  return samples[index] + (samples[next] - samples[index]) * fraction;
}

inline void ApplyColorCurves(float* r, float* g, float* b,
    const AnityHDRColorGrade& grade) {
  if (grade.curveEnabled == 0) return;
  constexpr int kCurveSampleCount = 128;
  const float masterR = SampleCurve(grade.curveLut, *r);
  const float masterG = SampleCurve(grade.curveLut, *g);
  const float masterB = SampleCurve(grade.curveLut, *b);
  *r = SampleCurve(grade.curveLut + kCurveSampleCount, masterR);
  *g = SampleCurve(grade.curveLut + kCurveSampleCount * 2, masterG);
  *b = SampleCurve(grade.curveLut + kCurveSampleCount * 3, masterB);

  const float maxValue = std::max(*r, std::max(*g, *b));
  const float minValue = std::min(*r, std::min(*g, *b));
  const float delta = maxValue - minValue;
  float hue = 0.f;
  if (delta > 0.000001f) {
    if (maxValue == *r) hue = std::fmod((*g - *b) / delta, 6.f);
    else if (maxValue == *g) hue = (*b - *r) / delta + 2.f;
    else hue = (*r - *g) / delta + 4.f;
    hue /= 6.f;
    if (hue < 0.f) hue += 1.f;
  }
  float saturation = maxValue <= 0.000001f ? 0.f : delta / maxValue;
  const float luminance = Clamp01(0.2126f * *r + 0.7152f * *g + 0.0722f * *b);
  hue = SampleCurve(grade.curveLut + kCurveSampleCount * 4, hue);
  saturation *= std::max(0.f, SampleCurve(grade.curveLut + kCurveSampleCount * 5, hue));
  saturation *= std::max(0.f, SampleCurve(grade.curveLut + kCurveSampleCount * 6, saturation));
  saturation *= std::max(0.f, SampleCurve(grade.curveLut + kCurveSampleCount * 7, luminance));
  saturation = Clamp01(saturation);
  hue = std::fmod(hue, 1.f);
  if (hue < 0.f) hue += 1.f;
  const float section = hue * 6.f;
  const float chroma = maxValue * saturation;
  const float x = chroma * (1.f - std::fabs(std::fmod(section, 2.f) - 1.f));
  float rr = 0.f, gg = 0.f, bb = 0.f;
  if (section < 1.f) { rr = chroma; gg = x; }
  else if (section < 2.f) { rr = x; gg = chroma; }
  else if (section < 3.f) { gg = chroma; bb = x; }
  else if (section < 4.f) { gg = x; bb = chroma; }
  else if (section < 5.f) { rr = x; bb = chroma; }
  else { rr = chroma; bb = x; }
  const float match = maxValue - chroma;
  *r = rr + match;
  *g = gg + match;
  *b = bb + match;
}

inline void ApplyHueShift(float* r, float* g, float* b, float degrees) {
  const float maxValue = std::max(*r, std::max(*g, *b));
  const float minValue = std::min(*r, std::min(*g, *b));
  const float delta = maxValue - minValue;
  if (delta <= 0.000001f) return;

  float hue = 0.f;
  if (maxValue == *r) hue = std::fmod((*g - *b) / delta, 6.f);
  else if (maxValue == *g) hue = (*b - *r) / delta + 2.f;
  else hue = (*r - *g) / delta + 4.f;
  hue = std::fmod(hue / 6.f + degrees / 360.f, 1.f);
  if (hue < 0.f) hue += 1.f;

  const float section = hue * 6.f;
  const float x = delta * (1.f - std::fabs(std::fmod(section, 2.f) - 1.f));
  float rr = 0.f, gg = 0.f, bb = 0.f;
  if (section < 1.f) { rr = delta; gg = x; }
  else if (section < 2.f) { rr = x; gg = delta; }
  else if (section < 3.f) { gg = delta; bb = x; }
  else if (section < 4.f) { gg = x; bb = delta; }
  else if (section < 5.f) { rr = x; bb = delta; }
  else { rr = delta; bb = x; }
  *r = rr + minValue;
  *g = gg + minValue;
  *b = bb + minValue;
}

struct BloomLevel {
  int width = 0;
  int height = 0;
  std::vector<float> rgb;
};

inline void AverageBloomBox(const std::vector<float>& source, int sourceWidth,
    int sourceHeight, int baseX, int baseY, int box,
    float* r, float* g, float* b) {
  *r = *g = *b = 0.f;
  for (int y = 0; y < box; ++y) {
    const int sampleY = std::min(baseY + y, sourceHeight - 1);
    for (int x = 0; x < box; ++x) {
      const int sampleX = std::min(baseX + x, sourceWidth - 1);
      const size_t offset = (static_cast<size_t>(sampleY) * sourceWidth + sampleX) * 3u;
      *r += source[offset + 0];
      *g += source[offset + 1];
      *b += source[offset + 2];
    }
  }
  const float divisor = static_cast<float>(box * box);
  *r /= divisor; *g /= divisor; *b /= divisor;
}

static std::vector<float> BuildBloomPyramid(const float* rgbaHdr,
    int width, int height, const AnityHDRColorGrade& grade) {
  const size_t pixelCount = static_cast<size_t>(width) * static_cast<size_t>(height);
  std::vector<float> combined(pixelCount * 3u, 0.f);
  if (grade.bloomIntensity <= 0.f) return combined;

  const int requestedLevels = grade.bloomMaxIterations > 0
      ? std::max(1, std::min(8, grade.bloomMaxIterations)) : 2;
  const int initialScale = grade.bloomDownscale == 0 ? 2 : 4;
  const int prefilterBox = grade.bloomHighQualityFiltering != 0
      ? initialScale * 2 : initialScale;

  std::vector<float> source(pixelCount * 3u);
  for (size_t index = 0; index < pixelCount; ++index) {
    source[index * 3u + 0] = rgbaHdr[index * 4u + 0];
    source[index * 3u + 1] = rgbaHdr[index * 4u + 1];
    source[index * 3u + 2] = rgbaHdr[index * 4u + 2];
  }

  std::vector<BloomLevel> levels;
  levels.reserve(static_cast<size_t>(requestedLevels));
  BloomLevel first;
  first.width = std::max(1, (width + initialScale - 1) / initialScale);
  first.height = std::max(1, (height + initialScale - 1) / initialScale);
  first.rgb.resize(static_cast<size_t>(first.width) * static_cast<size_t>(first.height) * 3u);
  for (int y = 0; y < first.height; ++y) {
    for (int x = 0; x < first.width; ++x) {
      float r, g, b;
      AverageBloomBox(source, width, height, x * initialScale, y * initialScale,
          prefilterBox, &r, &g, &b);
      const float luminance = 0.2126f * r + 0.7152f * g + 0.0722f * b;
      const float multiplier = luminance > grade.bloomThreshold
          ? (luminance - grade.bloomThreshold) / std::max(luminance, 0.0001f) : 0.f;
      const size_t offset = (static_cast<size_t>(y) * first.width + x) * 3u;
      first.rgb[offset + 0] = r * multiplier;
      first.rgb[offset + 1] = g * multiplier;
      first.rgb[offset + 2] = b * multiplier;
    }
  }
  levels.push_back(std::move(first));

  while (static_cast<int>(levels.size()) < requestedLevels) {
    const BloomLevel& previous = levels.back();
    if (previous.width == 1 && previous.height == 1) break;
    BloomLevel next;
    next.width = std::max(1, (previous.width + 1) / 2);
    next.height = std::max(1, (previous.height + 1) / 2);
    next.rgb.resize(static_cast<size_t>(next.width) * static_cast<size_t>(next.height) * 3u);
    const int downsampleBox = grade.bloomHighQualityFiltering != 0 ? 4 : 2;
    for (int y = 0; y < next.height; ++y) {
      for (int x = 0; x < next.width; ++x) {
        float r, g, b;
        AverageBloomBox(previous.rgb, previous.width, previous.height, x * 2, y * 2,
            downsampleBox, &r, &g, &b);
        const size_t offset = (static_cast<size_t>(y) * next.width + x) * 3u;
        next.rgb[offset + 0] = r;
        next.rgb[offset + 1] = g;
        next.rgb[offset + 2] = b;
      }
    }
    levels.push_back(std::move(next));
  }

  const float scatter = std::max(0.f, std::min(1.f, grade.bloomScatter));
  for (int y = 0; y < height; ++y) {
    for (int x = 0; x < width; ++x) {
      float weight = 1.f;
      const size_t outputOffset = (static_cast<size_t>(y) * width + x) * 3u;
      for (size_t levelIndex = 0; levelIndex < levels.size(); ++levelIndex) {
        const BloomLevel& level = levels[levelIndex];
        const int sampleX = std::min((x * level.width) / std::max(width, 1), level.width - 1);
        const int sampleY = std::min((y * level.height) / std::max(height, 1), level.height - 1);
        const size_t sampleOffset = (static_cast<size_t>(sampleY) * level.width + sampleX) * 3u;
        combined[outputOffset + 0] += level.rgb[sampleOffset + 0] * weight;
        combined[outputOffset + 1] += level.rgb[sampleOffset + 1] * weight;
        combined[outputOffset + 2] += level.rgb[sampleOffset + 2] * weight;
        weight *= scatter;
      }
    }
  }
  return combined;
}

inline float WrapDirtCoordinate(float value, int32_t mode) {
  switch (mode) {
    case 0: // Repeat
      return value - std::floor(value);
    case 2: { // Mirror
      const float period = std::fmod(std::fabs(value), 2.f);
      return period <= 1.f ? period : 2.f - period;
    }
    case 3: // MirrorOnce
      return std::max(0.f, std::min(1.f, std::fabs(value)));
    default: // Clamp and unknown values
      return std::max(0.f, std::min(1.f, value));
  }
}

struct DirtMipLevel {
  const uint8_t* pixels = nullptr;
  int width = 0;
  int height = 0;
};

inline float DirtComponent(const uint8_t* pixels, int width, int height,
    int x, int y, int channel, int32_t wrapU, int32_t wrapV, bool linear) {
  const float u = WrapDirtCoordinate((static_cast<float>(x) + 0.5f) /
      static_cast<float>(width), wrapU);
  const float v = WrapDirtCoordinate((static_cast<float>(y) + 0.5f) /
      static_cast<float>(height), wrapV);
  const int sampleX = std::min(width - 1, std::max(0,
      static_cast<int>(std::floor(u * static_cast<float>(width)))));
  const int sampleY = std::min(height - 1, std::max(0,
      static_cast<int>(std::floor(v * static_cast<float>(height)))));
  float component = pixels[(static_cast<size_t>(sampleY) * width + sampleX) * 4u + channel] / 255.f;
  return linear ? component : AnityHDR_GammaToLinearSpace(component);
}

static void SampleLensDirtLevel(const uint8_t* pixels, int width, int height,
    int32_t filterMode, int32_t wrapU, int32_t wrapV, bool linear,
    float u, float v, float* r, float* g, float* b) {
  if (filterMode == 0) {
    const int x = static_cast<int>(std::floor(u * static_cast<float>(width)));
    const int y = static_cast<int>(std::floor(v * static_cast<float>(height)));
    *r = DirtComponent(pixels, width, height, x, y, 0, wrapU, wrapV, linear);
    *g = DirtComponent(pixels, width, height, x, y, 1, wrapU, wrapV, linear);
    *b = DirtComponent(pixels, width, height, x, y, 2, wrapU, wrapV, linear);
    return;
  }
  const float sampleX = u * static_cast<float>(width) - 0.5f;
  const float sampleY = v * static_cast<float>(height) - 0.5f;
  const int x0 = static_cast<int>(std::floor(sampleX));
  const int y0 = static_cast<int>(std::floor(sampleY));
  const float fx = sampleX - static_cast<float>(x0);
  const float fy = sampleY - static_cast<float>(y0);
  float sampled[3]{};
  for (int channel = 0; channel < 3; ++channel) {
    const float c00 = DirtComponent(pixels, width, height, x0, y0, channel, wrapU, wrapV, linear);
    const float c10 = DirtComponent(pixels, width, height, x0 + 1, y0, channel, wrapU, wrapV, linear);
    const float c01 = DirtComponent(pixels, width, height, x0, y0 + 1, channel, wrapU, wrapV, linear);
    const float c11 = DirtComponent(pixels, width, height, x0 + 1, y0 + 1, channel, wrapU, wrapV, linear);
    const float horizontal0 = c00 + (c10 - c00) * fx;
    const float horizontal1 = c01 + (c11 - c01) * fx;
    sampled[channel] = horizontal0 + (horizontal1 - horizontal0) * fy;
  }
  *r = sampled[0]; *g = sampled[1]; *b = sampled[2];
}

static void SampleLensDirt(const std::vector<DirtMipLevel>& mips,
    int32_t filterMode, int32_t wrapU, int32_t wrapV, bool linear,
    float lod, float u, float v, float* r, float* g, float* b) {
  const int maxMip = static_cast<int>(mips.size()) - 1;
  if (maxMip <= 0) {
    const DirtMipLevel& base = mips.front();
    SampleLensDirtLevel(base.pixels, base.width, base.height, filterMode,
        wrapU, wrapV, linear, u, v, r, g, b);
    return;
  }
  const float clampedLod = std::max(0.f, std::min(lod, static_cast<float>(maxMip)));
  const int lowerMip = static_cast<int>(std::floor(clampedLod));
  if (filterMode != 2 || lowerMip == maxMip) {
    const DirtMipLevel& level = mips[lowerMip];
    SampleLensDirtLevel(level.pixels, level.width, level.height, filterMode,
        wrapU, wrapV, linear, u, v, r, g, b);
    return;
  }
  const int upperMip = lowerMip + 1;
  const float fraction = clampedLod - static_cast<float>(lowerMip);
  float lower[3]{};
  float upper[3]{};
  const DirtMipLevel& lowerLevel = mips[lowerMip];
  const DirtMipLevel& upperLevel = mips[upperMip];
  SampleLensDirtLevel(lowerLevel.pixels, lowerLevel.width, lowerLevel.height,
      1, wrapU, wrapV, linear, u, v, &lower[0], &lower[1], &lower[2]);
  SampleLensDirtLevel(upperLevel.pixels, upperLevel.width, upperLevel.height,
      1, wrapU, wrapV, linear, u, v, &upper[0], &upper[1], &upper[2]);
  *r = lower[0] + (upper[0] - lower[0]) * fraction;
  *g = lower[1] + (upper[1] - lower[1]) * fraction;
  *b = lower[2] + (upper[2] - lower[2]) * fraction;
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

static AnityResult ProcessFrameInternal(
    const float* rgbaHdr, int32_t width, int32_t height,
    const AnityHDRColorGrade* grade,
    const uint8_t* dirtRgba8, int32_t dirtWidth, int32_t dirtHeight,
    int32_t dirtMipCount, int32_t dirtFilterMode, int32_t dirtWrapU, int32_t dirtWrapV,
    int32_t dirtLinear, float dirtMipBias, int32_t dirtByteCount,
    float* rgbaOut,
    int32_t outHdr10) {
  if (!rgbaHdr || !rgbaOut || width <= 0 || height <= 0)
    return ANITY_ERR_INVALID_ARG;
  if (!std::isfinite(dirtMipBias)) return ANITY_ERR_INVALID_ARG;
  if (dirtRgba8 && (dirtWidth <= 0 || dirtHeight <= 0 || dirtMipCount <= 0 ||
      dirtByteCount < 0))
    return ANITY_ERR_INVALID_ARG;

  std::vector<DirtMipLevel> dirtMips;
  if (dirtRgba8) {
    int maxMipCount = 1;
    for (int maxWidth = dirtWidth, maxHeight = dirtHeight;
         maxWidth > 1 || maxHeight > 1; ++maxMipCount) {
      maxWidth = std::max(1, maxWidth >> 1);
      maxHeight = std::max(1, maxHeight >> 1);
    }
    if (dirtMipCount > maxMipCount) return ANITY_ERR_INVALID_ARG;
    int64_t requiredBytes = 0;
    int mipWidth = dirtWidth;
    int mipHeight = dirtHeight;
    for (int mip = 0; mip < dirtMipCount; ++mip) {
      const int64_t levelBytes = static_cast<int64_t>(mipWidth) * mipHeight * 4;
      if (requiredBytes > INT32_MAX - levelBytes) return ANITY_ERR_INVALID_ARG;
      dirtMips.push_back({dirtRgba8 + requiredBytes, mipWidth, mipHeight});
      requiredBytes += levelBytes;
      mipWidth = std::max(1, mipWidth >> 1);
      mipHeight = std::max(1, mipHeight >> 1);
    }
    if (requiredBytes != dirtByteCount) return ANITY_ERR_INVALID_ARG;
  }

  AnityHDRColorGrade g{};
  if (grade) g = *grade;
  else {
    g.tonemapMode = ANITY_TONEMAP_ACES;
    g.bloomThreshold = 0.9f;
    g.colorFilterR = g.colorFilterG = g.colorFilterB = 1.f;
    g.mixerRedR = g.mixerGreenG = g.mixerBlueB = 1.f;
  }

  const float exposure = std::pow(2.f, g.postExposure);
  const int n = width * height;
  const float dirtLod = dirtMips.empty() ? 0.f : std::max(0.f,
      std::log2(std::max(static_cast<float>(dirtWidth) / std::max(width, 1),
          static_cast<float>(dirtHeight) / std::max(height, 1))) + dirtMipBias);
  const std::vector<float> bloom = BuildBloomPyramid(rgbaHdr, width, height, g);
  for (int i = 0; i < n; ++i) {
    float r = rgbaHdr[i * 4 + 0] * exposure;
    float gr = rgbaHdr[i * 4 + 1] * exposure;
    float b = rgbaHdr[i * 4 + 2] * exposure;
    float a = rgbaHdr[i * 4 + 3];

    // Matches the Metal HDR post path: bloom is filtered from unexposed HDR,
    // then tint/intensity are applied after the multi-level scatter combine.
    float bloomR = bloom[static_cast<size_t>(i) * 3u + 0];
    float bloomG = bloom[static_cast<size_t>(i) * 3u + 1];
    float bloomB = bloom[static_cast<size_t>(i) * 3u + 2];
    if (dirtRgba8 && g.bloomDirtIntensity > 0.f) {
      float dirtR, dirtG, dirtB;
      const int pixelX = i % width;
      const int pixelY = i / width;
      SampleLensDirt(dirtMips, dirtFilterMode,
          dirtWrapU, dirtWrapV, dirtLinear != 0,
          dirtLod,
          (static_cast<float>(pixelX) + 0.5f) / static_cast<float>(width),
          (static_cast<float>(pixelY) + 0.5f) / static_cast<float>(height),
          &dirtR, &dirtG, &dirtB);
      const float intensity = std::max(0.f, g.bloomDirtIntensity);
      bloomR += bloomR * dirtR * intensity;
      bloomG += bloomG * dirtG * intensity;
      bloomB += bloomB * dirtB * intensity;
    }
    r += bloomR * g.bloomIntensity * std::max(0.f, g.bloomTintR);
    gr += bloomG * g.bloomIntensity * std::max(0.f, g.bloomTintG);
    b += bloomB * g.bloomIntensity * std::max(0.f, g.bloomTintB);

    // URP color-adjustment order: white balance, color filter, channel mixer,
    // contrast, hue shift, saturation, then tonemapping/output transfer.
    ApplyWhiteBalance(&r, &gr, &b, g.temperature, g.tint);
    ApplyColorFilter(&r, &gr, &b, g.colorFilterR, g.colorFilterG, g.colorFilterB);
    ApplyChannelMixer(&r, &gr, &b, g);
    ApplyColorCurves(&r, &gr, &b, g);

    // contrast around mid-gray
    float c = 1.f + g.contrast / 100.f;
    r = (r - 0.18f) * c + 0.18f;
    gr = (gr - 0.18f) * c + 0.18f;
    b = (b - 0.18f) * c + 0.18f;
    ApplyHueShift(&r, &gr, &b, g.hueShift);
    ApplySaturation(&r, &gr, &b, g.saturation);
    // ACES rational approximation is not monotonic for negative input.
    // Color grading operates on non-negative scene-referred RGB.
    r = std::max(0.f, r);
    gr = std::max(0.f, gr);
    b = std::max(0.f, b);

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

AnityResult ANITY_CALL AnityHDR_ProcessFrame(
    const float* rgbaHdr, int32_t width, int32_t height,
    const AnityHDRColorGrade* grade,
    float* rgbaOut,
    int32_t outHdr10) {
  return ProcessFrameInternal(rgbaHdr, width, height, grade,
      nullptr, 0, 0, 0, 0, 1, 1, 1, 0.f, 0, rgbaOut, outHdr10);
}

AnityResult ANITY_CALL AnityHDR_ProcessFrameWithLensDirtRGBA8(
    const float* rgbaHdr, int32_t width, int32_t height,
    const AnityHDRColorGrade* grade,
    const uint8_t* dirtRgba8, int32_t dirtWidth, int32_t dirtHeight,
    int32_t dirtFilterMode, int32_t dirtWrapU, int32_t dirtWrapV,
    int32_t dirtLinear, int32_t dirtByteCount,
    float* rgbaOut,
    int32_t outHdr10) {
  return ProcessFrameInternal(rgbaHdr, width, height, grade,
      dirtRgba8, dirtWidth, dirtHeight, 1, dirtFilterMode, dirtWrapU, dirtWrapV,
      dirtLinear, 0.f, dirtByteCount, rgbaOut, outHdr10);
}

AnityResult ANITY_CALL AnityHDR_ProcessFrameWithLensDirtRGBA8Mips(
    const float* rgbaHdr, int32_t width, int32_t height,
    const AnityHDRColorGrade* grade,
    const uint8_t* dirtRgba8, int32_t dirtWidth, int32_t dirtHeight,
    int32_t dirtMipCount, int32_t dirtFilterMode, int32_t dirtWrapU,
    int32_t dirtWrapV, int32_t dirtLinear, int32_t dirtByteCount,
    float* rgbaOut, int32_t outHdr10) {
  return ProcessFrameInternal(rgbaHdr, width, height, grade,
      dirtRgba8, dirtWidth, dirtHeight, dirtMipCount, dirtFilterMode,
      dirtWrapU, dirtWrapV, dirtLinear, 0.f, dirtByteCount, rgbaOut, outHdr10);
}

AnityResult ANITY_CALL AnityHDR_ProcessFrameWithLensDirtRGBA8MipsBias(
    const float* rgbaHdr, int32_t width, int32_t height,
    const AnityHDRColorGrade* grade,
    const uint8_t* dirtRgba8, int32_t dirtWidth, int32_t dirtHeight,
    int32_t dirtMipCount, int32_t dirtFilterMode, int32_t dirtWrapU,
    int32_t dirtWrapV, int32_t dirtLinear, float dirtMipBias,
    int32_t dirtByteCount, float* rgbaOut, int32_t outHdr10) {
  return ProcessFrameInternal(rgbaHdr, width, height, grade,
      dirtRgba8, dirtWidth, dirtHeight, dirtMipCount, dirtFilterMode,
      dirtWrapU, dirtWrapV, dirtLinear, dirtMipBias, dirtByteCount, rgbaOut, outHdr10);
}

} // extern "C"
