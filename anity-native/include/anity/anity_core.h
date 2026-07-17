#pragma once
#include "anity_export.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum AnityResult {
  ANITY_OK = 0,
  ANITY_ERR_INVALID_ARG = 1,
  ANITY_ERR_NOT_SUPPORTED = 2,
  ANITY_ERR_OUT_OF_MEMORY = 3,
  ANITY_ERR_DEVICE_LOST = 4,
  ANITY_ERR_IO = 5,
  ANITY_ERR_DECODE = 6,
  ANITY_ERR_TIMEOUT = 7,
  ANITY_ERR_INTERNAL = 100
} AnityResult;

typedef enum AnityPlatform {
  ANITY_PLATFORM_UNKNOWN = 0,
  ANITY_PLATFORM_WINDOWS = 1,
  ANITY_PLATFORM_LINUX = 2,
  ANITY_PLATFORM_MACOS = 3,
  ANITY_PLATFORM_IOS = 4,
  ANITY_PLATFORM_ANDROID = 5,
  ANITY_PLATFORM_WEBGL = 6
} AnityPlatform;

ANITY_API int32_t ANITY_CALL Anity_GetApiVersion(void);
ANITY_API AnityPlatform ANITY_CALL Anity_GetPlatform(void);
ANITY_API AnityResult ANITY_CALL Anity_Initialize(void);
ANITY_API void ANITY_CALL Anity_Shutdown(void);
ANITY_API const char* ANITY_CALL Anity_GetVersionString(void);
ANITY_API const char* ANITY_CALL Anity_GetLastError(void);

#ifdef __cplusplus
}
#endif
