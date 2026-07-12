#define ANITY_NATIVE_BUILD
#include "anity/texture/anity_texture_compress.h"
#include <cstring>

static void GetBlock(AnityTextureFormat format, int* bw, int* bh, int* blockBytes) {
  *bw = 4; *bh = 4; *blockBytes = 16;
  switch (format) {
    case ANITY_TEX_DXT1:
    case ANITY_TEX_ETC_RGB4:
    case ANITY_TEX_ETC2_RGB:
      *blockBytes = 8; break;
    case ANITY_TEX_DXT5:
    case ANITY_TEX_ETC2_RGBA8:
      *blockBytes = 16; break;
    case ANITY_TEX_ASTC_4x4:
      *bw = 4; *bh = 4; *blockBytes = 16; break;
    case ANITY_TEX_ASTC_6x6:
      *bw = 6; *bh = 6; *blockBytes = 16; break;
    case ANITY_TEX_ASTC_8x8:
      *bw = 8; *bh = 8; *blockBytes = 16; break;
    case ANITY_TEX_RGBA32:
      *bw = 1; *bh = 1; *blockBytes = 4; break;
    default: break;
  }
}

extern "C" {

int32_t ANITY_CALL AnityTexture_CalculateImageSize(
    int32_t width, int32_t height, AnityTextureFormat format) {
  if (width < 1) width = 1;
  if (height < 1) height = 1;
  int bw, bh, bb;
  GetBlock(format, &bw, &bh, &bb);
  if (format == ANITY_TEX_RGBA32) return width * height * 4;
  int bx = (width + bw - 1) / bw;
  int by = (height + bh - 1) / bh;
  return bx * by * bb;
}

AnityResult ANITY_CALL AnityTexture_CompressRGBA8(
    const uint8_t* rgba, int32_t width, int32_t height,
    AnityTextureFormat format,
    uint8_t* outBuffer, int32_t outBufferSize) {
  if (!rgba || !outBuffer || width <= 0 || height <= 0) return ANITY_ERR_INVALID_ARG;
  int need = AnityTexture_CalculateImageSize(width, height, format);
  if (outBufferSize < need) return ANITY_ERR_INVALID_ARG;
  std::memset(outBuffer, 0, static_cast<size_t>(need));

  if (format == ANITY_TEX_RGBA32) {
    std::memcpy(outBuffer, rgba, static_cast<size_t>(need));
    return ANITY_OK;
  }

  /* Soft compress: store average color as endpoint proxy per block */
  int bw, bh, bb;
  GetBlock(format, &bw, &bh, &bb);
  int bx = (width + bw - 1) / bw;
  int by = (height + bh - 1) / bh;
  int bi = 0;
  for (int y = 0; y < by; ++y) {
    for (int x = 0; x < bx; ++x) {
      int r = 0, g = 0, b = 0, a = 0, n = 0;
      for (int py = 0; py < bh; ++py) {
        int iy = y * bh + py;
        if (iy >= height) break;
        for (int px = 0; px < bw; ++px) {
          int ix = x * bw + px;
          if (ix >= width) break;
          const uint8_t* p = rgba + (iy * width + ix) * 4;
          r += p[0]; g += p[1]; b += p[2]; a += p[3];
          ++n;
        }
      }
      if (n > 0) { r /= n; g /= n; b /= n; a /= n; }
      if (bi + 4 <= need) {
        outBuffer[bi + 0] = static_cast<uint8_t>(r);
        outBuffer[bi + 1] = static_cast<uint8_t>(g);
        outBuffer[bi + 2] = static_cast<uint8_t>(b);
        outBuffer[bi + 3] = static_cast<uint8_t>(a);
      }
      bi += bb;
    }
  }
  return ANITY_OK;
}

} // extern "C"
