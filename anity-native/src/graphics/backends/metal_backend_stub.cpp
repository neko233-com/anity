#define ANITY_NATIVE_BUILD
#include "../anity_graphics_device.h"
#include "../../ui/anity_ui_renderer_internal.h"

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
extern "C" AnityResult AnityGraphics_Metal_UploadUI(
    AnityGraphicsDevice*, int32_t, const void*, int32_t, const void*, int32_t) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_DrawUI(
    AnityGraphicsDevice*, int32_t, const AnityUIDrawPacket*, int32_t, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_ReadbackSwapchainRGBA8(
    AnitySwapchain*, uint8_t*, int32_t, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_SyncTexture(
    AnityGraphicsDevice*, uint64_t) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" void AnityGraphics_Metal_DestroyTexture(
    AnityGraphicsDevice*, uint64_t) {}
extern "C" AnityResult AnityGraphics_Metal_DispatchVFXInitializeCopy(
    AnityGraphicsDevice*, const AnityGraphicsVFXInitializeDispatchDesc*,
    const uint8_t*, int32_t, uint8_t*, int32_t) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_DispatchVFXInitializeKernel(
    AnityGraphicsDevice*, const AnityGraphicsVFXInitializeDispatchDesc*,
    const AnityGraphicsVFXInitializeKernelDesc*,
    const AnityGraphicsVFXInitializeAttributeDesc*,
    const AnityGraphicsVFXInitializeOperationDesc*,
    const uint32_t*, int32_t, int32_t,
    const uint8_t*, int32_t, uint8_t*, int32_t,
    uint32_t*, int32_t, int32_t*, int32_t*, uint32_t*, int32_t*,
    uint64_t, uint64_t, int32_t, int32_t) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_BeginVFXInitializeKernel(
    AnityGraphicsDevice*, const AnityGraphicsVFXInitializeDispatchDesc*,
    const AnityGraphicsVFXInitializeKernelDesc*,
    const AnityGraphicsVFXInitializeAttributeDesc*,
    const AnityGraphicsVFXInitializeOperationDesc*,
    const uint32_t*, int32_t, int32_t,
    const uint8_t*, int32_t, uint8_t*, int32_t,
    uint32_t*, int32_t, int32_t*, int32_t*, uint32_t*, int32_t*,
    uint64_t, uint64_t, int32_t, int32_t, void**) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_PollVFXInitializeKernel(
    void*, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_PublishVFXInitializeKernel(
    void*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_CompleteVFXInitializeKernel(
    void*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_CancelVFXInitializeKernel(
    void*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_DispatchVFXUpdateBatch(
    AnityGraphicsDevice*, const AnityGraphicsVFXUpdateKernelDesc*, int32_t,
    const AnityGraphicsVFXUpdateOperationDesc*,
    uint8_t* const*, const int32_t*, const uint32_t*, const int32_t*,
    const uint32_t* const*, const int32_t*, const int32_t*,
    const int32_t*,
    const uint64_t*, const uint64_t*, uint32_t* const*,
    const int32_t*, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_BeginVFXUpdateBatch(
    AnityGraphicsDevice*, const AnityGraphicsVFXUpdateKernelDesc*, int32_t,
    const AnityGraphicsVFXUpdateOperationDesc*,
    uint8_t* const*, const int32_t*, const uint32_t*, const int32_t*,
    const uint32_t* const*, const int32_t*, const int32_t*,
    const int32_t*,
    const uint64_t*, const uint64_t*, uint32_t* const*,
    const int32_t*, int32_t*,
    const AnityGraphicsVFXBoundsReductionDesc*, int32_t, void**) {
  return ANITY_ERR_NOT_SUPPORTED;
}

extern "C" AnityResult AnityGraphics_Metal_PublishVFXUpdateBatch(void*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_PollVFXUpdateBatch(
    void*, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_PollVFXUpdateBatchForPreparation(
    void*, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_CompleteVFXUpdateBatch(void*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_CancelVFXUpdateBatch(void*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_GetVFXUpdateBackendStats(
    const AnityGraphicsDevice*, uint64_t, int32_t,
    AnityGraphicsVFXUpdateBackendStats*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_ReadbackVFXResidentParticles(
    const AnityGraphicsDevice*, uint64_t, int32_t, uint64_t,
    uint8_t*, int32_t) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_ReadbackVFXResidentMetadata(
    const AnityGraphicsDevice*, uint64_t, int32_t, uint64_t,
    uint32_t*, int32_t, uint32_t*, int32_t, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_DrawVFXPlanarCamera(
    AnityGraphicsDevice*, const AnityGraphicsVFXPlanarCameraBatchDesc*,
    const AnityGraphicsVFXPlanarDrawPacket*, int32_t,
    AnityGraphicsVFXPlanarCameraDrawInfo*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_GetVFXPlanarSubmissionStats(
    AnityGraphicsDevice*, AnityGraphicsVFXPlanarSubmissionStats*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_WaitForVFXPlanarSubmissions(
    AnityGraphicsDevice*, uint64_t, int32_t) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_RestoreVFXResidentGeneration(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" void AnityGraphics_Metal_DiscardVFXResidentSnapshots(
    AnityGraphicsDevice*, uint64_t) {}
extern "C" void AnityGraphics_Metal_DiscardVFXResidentSnapshot(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t) {}
extern "C" void AnityGraphics_Metal_ClearVFXEffectResources(
    AnityGraphicsDevice*, uint64_t) {}
extern "C" AnityResult AnityGraphics_Metal_SetVFXFailureInjection(
    AnityGraphicsDevice*, int32_t, int32_t) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_ReduceVFXParticleBounds(
    AnityGraphicsDevice*, const AnityGraphicsVFXBoundsReductionDesc*,
    const uint8_t*, int32_t, int32_t, int32_t, int32_t, uint32_t, uint64_t,
    int32_t,
    AnityGraphicsVFXBoundsReductionResult*) {
  return ANITY_ERR_NOT_SUPPORTED;
}

#endif
