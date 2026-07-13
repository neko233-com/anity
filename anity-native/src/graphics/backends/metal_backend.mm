#define ANITY_NATIVE_BUILD
#include "../anity_graphics_device.h"
#include <new>
#include <cstring>
#include <algorithm>

#if defined(ANITY_HAS_METAL)
#import <Metal/Metal.h>
#import <Foundation/Foundation.h>
#import <QuartzCore/CAMetalLayer.h>
#import <CoreGraphics/CoreGraphics.h>

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
  int32_t hasNativeLayer = 0;
  int32_t ownsLayer = 0;
  CAMetalLayer* layer = nil;
  id<CAMetalDrawable> currentDrawable = nil;
  int32_t imageIndex = 0;
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
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  if (!st || !st->device) return ANITY_ERR_DEVICE_LOST;

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
  sc->presentCount = 0;

  auto* mst = new (std::nothrow) MetalSwapchainState();
  if (!mst) {
    delete sc;
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  mst->width = sc->width;
  mst->height = sc->height;
  mst->imageCount = sc->imageCount;
  mst->headless = sc->headless;

  CAMetalLayer* layer = nil;
  if (desc->nativeWindow) {
    /* Caller may pass CAMetalLayer* or NSView* — prefer layer cast */
    id obj = (__bridge id)desc->nativeWindow;
    if ([obj isKindOfClass:[CAMetalLayer class]]) {
      layer = (CAMetalLayer*)obj;
      mst->ownsLayer = 0;
    }
  }

  if (!layer) {
    /* Create offscreen CAMetalLayer (drawable simulation / headless present path) */
    layer = [CAMetalLayer layer];
    mst->ownsLayer = 1;
    mst->headless = 1;
    sc->headless = 1;
  } else {
    mst->headless = 0;
    sc->headless = 0;
  }

  layer.device = st->device;
  layer.pixelFormat = (desc->hdr || device->hdrEnabled)
      ? MTLPixelFormatRGBA16Float
      : MTLPixelFormatBGRA8Unorm;
  layer.framebufferOnly = YES;
  layer.drawableSize = CGSizeMake((CGFloat)sc->width, (CGFloat)sc->height);
  if (@available(macOS 10.13, iOS 11.0, *)) {
    layer.displaySyncEnabled = desc->vsync != 0;
  }
  if (desc->hdr || device->hdrEnabled) {
    if (@available(macOS 10.15, iOS 13.0, *)) {
      layer.wantsExtendedDynamicRangeContent = YES;
    }
  }
  /* Triple-buffering intent */
  layer.maximumDrawableCount = (NSUInteger)std::min(std::max(sc->imageCount, 2), 3);

  mst->layer = layer;
  mst->hasNativeLayer = 1;
  sc->backend = mst;
  device->swapchain = sc;
  *out = sc;
  return ANITY_OK;
}

extern "C" void AnityGraphics_Metal_DestroySwapchain(AnitySwapchain* swapchain) {
  if (!swapchain || !swapchain->backend) return;
  auto* mst = reinterpret_cast<MetalSwapchainState*>(swapchain->backend);
  mst->currentDrawable = nil;
  if (mst->ownsLayer) {
    mst->layer = nil; /* ARC release */
  } else {
    mst->layer = nil;
  }
  delete mst;
  swapchain->backend = nullptr;
}

extern "C" AnityResult AnityGraphics_Metal_Acquire(AnitySwapchain* swapchain, int32_t* outIndex) {
  if (!swapchain || !swapchain->backend) return ANITY_ERR_INVALID_ARG;
  auto* mst = reinterpret_cast<MetalSwapchainState*>(swapchain->backend);
  if (!mst->layer) return ANITY_ERR_DEVICE_LOST;

  mst->currentDrawable = [mst->layer nextDrawable];
  mst->imageIndex = (mst->imageIndex + 1) % (mst->imageCount > 0 ? mst->imageCount : 1);
  swapchain->currentImage = mst->imageIndex;
  if (outIndex) *outIndex = mst->imageIndex;
  /* nextDrawable may be nil under extreme load — still advance index for API continuity */
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_Present(AnitySwapchain* swapchain) {
  if (!swapchain || !swapchain->backend) return ANITY_ERR_INVALID_ARG;
  auto* mst = reinterpret_cast<MetalSwapchainState*>(swapchain->backend);
  auto* st = swapchain->device ? reinterpret_cast<MetalState*>(swapchain->device->backend) : nullptr;

  if (mst->currentDrawable && st && st->queue) {
    id<MTLCommandBuffer> cb = [st->queue commandBuffer];
    [cb presentDrawable:mst->currentDrawable];
    [cb commit];
    mst->currentDrawable = nil;
  }
  swapchain->presentCount++;
  return ANITY_OK;
}

extern "C" int32_t AnityGraphics_Metal_SwapchainHasNativeLayer(const AnitySwapchain* swapchain) {
  if (!swapchain || !swapchain->backend) return 0;
  return reinterpret_cast<const MetalSwapchainState*>(swapchain->backend)->hasNativeLayer;
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
