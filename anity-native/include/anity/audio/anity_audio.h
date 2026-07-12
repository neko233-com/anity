#pragma once
#include "../anity_core.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct AnityAudioClip AnityAudioClip;

ANITY_API AnityResult ANITY_CALL AnityAudio_DecodeFile(
    const char* path,
    float** outSamples, int32_t* outSampleCount,
    int32_t* outChannels, int32_t* outFrequency);

ANITY_API AnityResult ANITY_CALL AnityAudio_DecodeMemory(
    const uint8_t* data, int32_t size,
    const char* hintExt,
    float** outSamples, int32_t* outSampleCount,
    int32_t* outChannels, int32_t* outFrequency);

ANITY_API void ANITY_CALL AnityAudio_FreeSamples(float* samples);

#ifdef __cplusplus
}
#endif
