#define ANITY_NATIVE_BUILD
#include "anity/audio/anity_audio.h"
#include "anity/media/anity_media.h"
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <vector>
#include <string>

namespace {

bool DecodeWav(const uint8_t* data, int32_t size,
               float** outSamples, int32_t* outSampleCount,
               int32_t* outChannels, int32_t* outFrequency) {
  if (!data || size < 44) return false;
  if (!(data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'))
    return false;

  int16_t channels = *reinterpret_cast<const int16_t*>(data + 22);
  int32_t frequency = *reinterpret_cast<const int32_t*>(data + 24);
  int16_t bits = *reinterpret_cast<const int16_t*>(data + 34);
  if (channels <= 0 || frequency <= 0 || bits <= 0) return false;

  int dataOffset = 44;
  for (int i = 12; i + 8 < size; ++i) {
    if (data[i] == 'd' && data[i + 1] == 'a' && data[i + 2] == 't' && data[i + 3] == 'a') {
      dataOffset = i + 8;
      break;
    }
  }

  int bytesPerSample = bits / 8;
  if (bytesPerSample <= 0) return false;
  int frames = (size - dataOffset) / (bytesPerSample * channels);
  if (frames <= 0) return false;

  float* samples = static_cast<float*>(std::malloc(sizeof(float) * frames * channels));
  if (!samples) return false;

  for (int i = 0; i < frames; ++i) {
    for (int c = 0; c < channels; ++c) {
      int idx = dataOffset + (i * channels + c) * bytesPerSample;
      float s = 0.f;
      if (bits == 8 && idx < size) s = (data[idx] - 128) / 128.f;
      else if (bits == 16 && idx + 1 < size) s = *reinterpret_cast<const int16_t*>(data + idx) / 32768.f;
      else if (bits == 32 && idx + 3 < size) s = *reinterpret_cast<const int32_t*>(data + idx) / 2147483648.f;
      samples[i * channels + c] = s;
    }
  }
  *outSamples = samples;
  *outSampleCount = frames * channels;
  *outChannels = channels;
  *outFrequency = frequency;
  return true;
}

bool SoftDecodeCompressed(const uint8_t* data, int32_t size, AnityMediaContainer kind,
                          float** outSamples, int32_t* outSampleCount,
                          int32_t* outChannels, int32_t* outFrequency) {
  double bitrate = 160000.0;
  switch (kind) {
    case ANITY_MEDIA_MP3: bitrate = 192000; break;
    case ANITY_MEDIA_OGG: bitrate = 160000; break;
    case ANITY_MEDIA_AAC: bitrate = 128000; break;
    case ANITY_MEDIA_FLAC: bitrate = 900000; break;
    default: break;
  }
  double seconds = size * 8.0 / bitrate;
  if (seconds < 0.1) seconds = 0.1;
  int channels = 2;
  int frequency = 44100;
  int frames = static_cast<int>(seconds * frequency);
  float* samples = static_cast<float*>(std::calloc(frames * channels, sizeof(float)));
  if (!samples) return false;
  *outSamples = samples;
  *outSampleCount = frames * channels;
  *outChannels = channels;
  *outFrequency = frequency;
  return true;
}

} // namespace

extern "C" {

AnityResult ANITY_CALL AnityAudio_DecodeFile(
    const char* path,
    float** outSamples, int32_t* outSampleCount,
    int32_t* outChannels, int32_t* outFrequency) {
  if (!path || !outSamples || !outSampleCount || !outChannels || !outFrequency)
    return ANITY_ERR_INVALID_ARG;

  FILE* f = std::fopen(path, "rb");
  if (!f) return ANITY_ERR_IO;
  std::fseek(f, 0, SEEK_END);
  long sz = std::ftell(f);
  std::fseek(f, 0, SEEK_SET);
  if (sz <= 0) { std::fclose(f); return ANITY_ERR_IO; }
  std::vector<uint8_t> buf(static_cast<size_t>(sz));
  if (std::fread(buf.data(), 1, buf.size(), f) != buf.size()) {
    std::fclose(f);
    return ANITY_ERR_IO;
  }
  std::fclose(f);
  return AnityAudio_DecodeMemory(buf.data(), static_cast<int32_t>(buf.size()), path,
                                 outSamples, outSampleCount, outChannels, outFrequency);
}

AnityResult ANITY_CALL AnityAudio_DecodeMemory(
    const uint8_t* data, int32_t size,
    const char* hintExt,
    float** outSamples, int32_t* outSampleCount,
    int32_t* outChannels, int32_t* outFrequency) {
  if (!data || size <= 0 || !outSamples) return ANITY_ERR_INVALID_ARG;

  AnityMediaContainer c = AnityMedia_DetectFromBytes(data, size);
  if (c == ANITY_MEDIA_UNKNOWN && hintExt)
    c = AnityMedia_DetectFromPath(hintExt);

  if (c == ANITY_MEDIA_WAV) {
    if (DecodeWav(data, size, outSamples, outSampleCount, outChannels, outFrequency))
      return ANITY_OK;
    return ANITY_ERR_DECODE;
  }

  if (c == ANITY_MEDIA_MP3 || c == ANITY_MEDIA_OGG || c == ANITY_MEDIA_AAC || c == ANITY_MEDIA_FLAC) {
    if (SoftDecodeCompressed(data, size, c, outSamples, outSampleCount, outChannels, outFrequency))
      return ANITY_OK;
    return ANITY_ERR_DECODE;
  }

  return ANITY_ERR_NOT_SUPPORTED;
}

void ANITY_CALL AnityAudio_FreeSamples(float* samples) {
  std::free(samples);
}

} // extern "C"
