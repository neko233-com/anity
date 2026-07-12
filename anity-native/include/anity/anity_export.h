#pragma once

#if defined(_WIN32) || defined(_WIN64)
  #if defined(anity_native_EXPORTS) || defined(ANITY_NATIVE_BUILD)
    #define ANITY_API __declspec(dllexport)
  #else
    #define ANITY_API __declspec(dllimport)
  #endif
  #define ANITY_CALL __cdecl
#else
  #define ANITY_API __attribute__((visibility("default")))
  #define ANITY_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* C ABI version — bump when breaking P/Invoke surface */
#define ANITY_NATIVE_API_VERSION 1

#ifdef __cplusplus
}
#endif
