#pragma once
#include "../anity_core.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum AnityMediaContainer {
  ANITY_MEDIA_UNKNOWN = 0,
  ANITY_MEDIA_WAV,
  ANITY_MEDIA_MP3,
  ANITY_MEDIA_OGG,
  ANITY_MEDIA_AAC,
  ANITY_MEDIA_FLAC,
  ANITY_MEDIA_MP4,
  ANITY_MEDIA_WEBM,
  ANITY_MEDIA_MOV
} AnityMediaContainer;

ANITY_API AnityMediaContainer ANITY_CALL AnityMedia_DetectFromPath(const char* path);
ANITY_API AnityMediaContainer ANITY_CALL AnityMedia_DetectFromBytes(const uint8_t* data, int32_t size);

/* Video soft open: fills duration estimate; full decode is platform-backend work */
ANITY_API AnityResult ANITY_CALL AnityMedia_OpenVideo(
    const char* path,
    double* outDurationSec,
    int32_t* outWidth,
    int32_t* outHeight,
    double* outFps);

#ifdef __cplusplus
}
#endif
