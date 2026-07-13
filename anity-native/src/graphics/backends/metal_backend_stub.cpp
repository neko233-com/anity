#define ANITY_NATIVE_BUILD
#include "../anity_graphics_device.h"

/* Non-Apple builds: Metal symbols as stubs so the library links. */
#if !defined(ANITY_HAS_METAL)

extern "C" AnityResult AnityGraphics_CreateMetal(
    const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" void AnityGraphics_Metal_Destroy(AnityGraphicsDevice*) {}
extern "C" AnityResult AnityGraphics_Metal_CreateSwapchain(
    AnityGraphicsDevice*, const AnitySwapchainDesc*, AnitySwapchain**) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" void AnityGraphics_Metal_DestroySwapchain(AnitySwapchain*) {}
extern "C" AnityResult AnityGraphics_Metal_Acquire(AnitySwapchain*, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_Present(AnitySwapchain*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" int32_t AnityGraphics_Metal_SwapchainHasNativeLayer(const AnitySwapchain*) {
  return 0;
}

#endif
