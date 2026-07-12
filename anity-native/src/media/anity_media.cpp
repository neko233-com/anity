#define ANITY_NATIVE_BUILD
#include "anity/media/anity_media.h"
#include <cctype>
#include <cstdio>
#include <cstring>
#include <string>

static std::string ExtOf(const char* path) {
  if (!path) return {};
  std::string s(path);
  auto pos = s.find_last_of('.');
  if (pos == std::string::npos) return {};
  std::string e = s.substr(pos);
  for (char& c : e) c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
  return e;
}

extern "C" {

AnityMediaContainer ANITY_CALL AnityMedia_DetectFromPath(const char* path) {
  auto e = ExtOf(path);
  if (e == ".wav") return ANITY_MEDIA_WAV;
  if (e == ".mp3") return ANITY_MEDIA_MP3;
  if (e == ".ogg" || e == ".oga") return ANITY_MEDIA_OGG;
  if (e == ".aac") return ANITY_MEDIA_AAC;
  if (e == ".flac") return ANITY_MEDIA_FLAC;
  if (e == ".mp4" || e == ".m4v") return ANITY_MEDIA_MP4;
  if (e == ".webm") return ANITY_MEDIA_WEBM;
  if (e == ".mov") return ANITY_MEDIA_MOV;
  return ANITY_MEDIA_UNKNOWN;
}

AnityMediaContainer ANITY_CALL AnityMedia_DetectFromBytes(const uint8_t* data, int32_t size) {
  if (!data || size < 4) return ANITY_MEDIA_UNKNOWN;
  if (size >= 12 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
      && data[8] == 'W' && data[9] == 'A' && data[10] == 'V' && data[11] == 'E')
    return ANITY_MEDIA_WAV;
  if ((data[0] == 0xFF && (data[1] & 0xE0) == 0xE0) ||
      (size >= 3 && data[0] == 'I' && data[1] == 'D' && data[2] == '3'))
    return ANITY_MEDIA_MP3;
  if (data[0] == 'O' && data[1] == 'g' && data[2] == 'g' && data[3] == 'S')
    return ANITY_MEDIA_OGG;
  if (data[0] == 'f' && data[1] == 'L' && data[2] == 'a' && data[3] == 'C')
    return ANITY_MEDIA_FLAC;
  if (size >= 8 && data[4] == 'f' && data[5] == 't' && data[6] == 'y' && data[7] == 'p')
    return ANITY_MEDIA_MP4;
  if (data[0] == 0x1A && data[1] == 0x45 && data[2] == 0xDF && data[3] == 0xA3)
    return ANITY_MEDIA_WEBM;
  return ANITY_MEDIA_UNKNOWN;
}

AnityResult ANITY_CALL AnityMedia_OpenVideo(
    const char* path,
    double* outDurationSec,
    int32_t* outWidth,
    int32_t* outHeight,
    double* outFps) {
  if (!path) return ANITY_ERR_INVALID_ARG;
  AnityMediaContainer c = AnityMedia_DetectFromPath(path);
  if (c != ANITY_MEDIA_MP4 && c != ANITY_MEDIA_WEBM && c != ANITY_MEDIA_MOV)
    return ANITY_ERR_NOT_SUPPORTED;

  double duration = 10.0;
  FILE* f = std::fopen(path, "rb");
  if (f) {
    std::fseek(f, 0, SEEK_END);
    long sz = std::ftell(f);
    std::fclose(f);
    if (sz > 0) {
      double bitrate = 4000000.0;
      duration = (sz * 8.0) / bitrate;
      if (duration < 0.1) duration = 0.1;
    }
  }
  if (outDurationSec) *outDurationSec = duration;
  if (outWidth) *outWidth = 1920;
  if (outHeight) *outHeight = 1080;
  if (outFps) *outFps = 30.0;
  return ANITY_OK;
}

} // extern "C"
