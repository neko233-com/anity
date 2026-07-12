#define ANITY_NATIVE_BUILD
#include "../anity_graphics_device.h"
#include <new>
#include <cstring>

#if defined(ANITY_HAS_METAL)
#import <Metal/Metal.h>
#import <Foundation/Foundation.h>

struct MetalState {
  id<MTLDevice> device = nil;
  id<MTLCommandQueue> queue = nil;
  bool hdr = false;
};

struct MetalSwapchainState {
  int32_t width = 0;
  int32_t height = 0;
  int32_t imageCount = 3;
  int32_t headless = 1;
  /* CAMetalLayer* would attach to nativeWindow when provided */
  void* layer = nullptr;
};

extern "C" AnityResult AnityGraphics_CreateMetal(
    const AnityGraphicsDeviceDesc* desc, AnityGraphicsDevice** outDevice) {
  if (!desc || !outDevice) return ANITY_ERR_INVALID_ARG;

  id<MTLDevice> mtl = MTLCreateSystemDefaultDevice();
  if (!mtl) return ANITY_ERR_DEVICE_LOST;

  auto* st = new (std::nothrow) MetalState();
  if (!st) return ANITY_ERR_OUT_OF_MEMORY;
  st->device = mtl;
  st->queue = [mtl newCommandQueue];
  st->hdr = desc->hdrEnabled != 0;

  auto* dev = new (std::nothrow) AnityGraphicsDevice();
  if (!dev) {
    delete st;
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  std::memset(dev, 0, sizeof(*dev));
  dev->type = ANITY_GFX_METAL;
  dev->width = desc->width > 0 ? desc->width : 1280;
  dev->height = desc->height > 0 ? desc->height : 720;
  dev->hdrEnabled = st->hdr ? 1 : 0;
  dev->msaaSamples = desc->msaaSamples;
  dev->vsync = desc->vsync;
  dev->supportsHdr = 1; /* EDR path on Apple */
  dev->backend = st;
  dev->swapchain = nullptr;
  *outDevice = dev;
  return ANITY_OK;
}

extern "C" void AnityGraphics_Metal_Destroy(AnityGraphicsDevice* device) {
  if (!device || !device->backend) return;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  st->queue = nil;
  st->device = nil;
  delete st;
  device->backend = nullptr;
}

extern "C" AnityResult AnityGraphics_Metal_CreateSwapchain(
    AnityGraphicsDevice* device, const AnitySwapchainDesc* desc, AnitySwapchain** out) {
  if (!device || !desc || !out) return ANITY_ERR_INVALID_ARG;

  auto* sc = new (std::nothrow) AnitySwapchain();
  if (!sc) return ANITY_ERR_OUT_OF_MEMORY;
  std::memset(sc, 0, sizeof(*sc));
  sc->device = device;
  sc->width = desc->width > 0 ? desc->width : device->width;
  sc->height = desc->height > 0 ? desc->height : device->height;
  sc->imageCount = desc->imageCount > 0 ? desc->imageCount : 3;
  sc->vsync = desc->vsync;
  sc->hdr = desc->hdr;
  sc->headless = desc->nativeWindow == nullptr ? 1 : 0;

  auto* mst = new (std::nothrow) MetalSwapchainState();
  if (!mst) {
    delete sc;
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  mst->width = sc->width;
  mst->height = sc->height;
  mst->imageCount = sc->imageCount;
  mst->headless = sc->headless;
  mst->layer = desc->nativeWindow; /* CAMetalLayer* when provided */
  sc->backend = mst;
  device->swapchain = sc;
  *out = sc;
  return ANITY_OK;
}

extern "C" void AnityGraphics_Metal_DestroySwapchain(AnitySwapchain* swapchain) {
  if (!swapchain) return;
  if (swapchain->backend) {
    delete reinterpret_cast<MetalSwapchainState*>(swapchain->backend);
    swapchain->backend = nullptr;
  }
}

#else

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

#endif
