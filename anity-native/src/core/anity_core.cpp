#define ANITY_NATIVE_BUILD
#include "anity/anity_core.h"
#include <cstring>
#include <string>

static std::string g_lastError;
static bool g_initialized = false;

static void SetError(const char* msg) {
  g_lastError = msg ? msg : "";
}

extern "C" {

int32_t ANITY_CALL Anity_GetApiVersion(void) {
  return ANITY_NATIVE_API_VERSION;
}

AnityPlatform ANITY_CALL Anity_GetPlatform(void) {
#if defined(ANITY_OS_WINDOWS)
  return ANITY_PLATFORM_WINDOWS;
#elif defined(ANITY_OS_APPLE)
  #if defined(TARGET_OS_IPHONE) && TARGET_OS_IPHONE
    return ANITY_PLATFORM_IOS;
  #else
    return ANITY_PLATFORM_MACOS;
  #endif
#elif defined(ANITY_OS_ANDROID)
  return ANITY_PLATFORM_ANDROID;
#elif defined(ANITY_OS_LINUX)
  return ANITY_PLATFORM_LINUX;
#else
  return ANITY_PLATFORM_UNKNOWN;
#endif
}

AnityResult ANITY_CALL Anity_Initialize(void) {
  g_initialized = true;
  SetError("");
  return ANITY_OK;
}

void ANITY_CALL Anity_Shutdown(void) {
  g_initialized = false;
}

const char* ANITY_CALL Anity_GetVersionString(void) {
  return "anity-native 0.1.0 (Unity 2022.3 Pro parity target)";
}

const char* ANITY_CALL Anity_GetLastError(void) {
  return g_lastError.c_str();
}

} // extern "C"

/* shared by other units */
namespace anity {
void SetLastError(const char* msg) { SetError(msg); }
bool IsInitialized() { return g_initialized; }
}
