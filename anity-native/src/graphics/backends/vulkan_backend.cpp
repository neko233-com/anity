#define ANITY_NATIVE_BUILD
#include "../anity_graphics_device.h"
#include <new>
#include <cstring>
#include <vector>
#include <algorithm>

#if defined(ANITY_HAS_VULKAN)
#include <vulkan/vulkan.h>

#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <vulkan/vulkan_win32.h>
#endif

struct VkSwapchainState {
  int32_t width = 0;
  int32_t height = 0;
  int32_t imageCount = 2;
  int32_t headless = 1;
  int32_t hasNativeSurface = 0;
  VkSurfaceKHR surface = VK_NULL_HANDLE;
  VkSwapchainKHR swapchain = VK_NULL_HANDLE;
  std::vector<VkImage> images;
  uint32_t imageIndex = 0;
  /* headless software ring: just track present cycles */
  int32_t softwareImages = 0;
};

struct VkState {
  VkInstance instance = VK_NULL_HANDLE;
  VkPhysicalDevice phys = VK_NULL_HANDLE;
  VkDevice device = VK_NULL_HANDLE;
  VkQueue queue = VK_NULL_HANDLE;
  bool hdr = false;
  uint32_t queueFamily = 0;
  bool hasSurfaceExt = false;
  bool hasSwapchainExt = false;
};

static bool HasExtension(const char* name, const std::vector<VkExtensionProperties>& props) {
  for (const auto& p : props)
    if (std::strcmp(p.extensionName, name) == 0) return true;
  return false;
}

extern "C" AnityResult AnityGraphics_CreateVulkan(
    const AnityGraphicsDeviceDesc* desc, AnityGraphicsDevice** outDevice) {
  if (!desc || !outDevice) return ANITY_ERR_INVALID_ARG;

  auto* st = new (std::nothrow) VkState();
  if (!st) return ANITY_ERR_OUT_OF_MEMORY;
  st->hdr = desc->hdrEnabled != 0;

  uint32_t instExtCount = 0;
  vkEnumerateInstanceExtensionProperties(nullptr, &instExtCount, nullptr);
  std::vector<VkExtensionProperties> instExts(instExtCount);
  if (instExtCount)
    vkEnumerateInstanceExtensionProperties(nullptr, &instExtCount, instExts.data());

  std::vector<const char*> enabledInst;
  if (HasExtension(VK_KHR_SURFACE_EXTENSION_NAME, instExts)) {
    enabledInst.push_back(VK_KHR_SURFACE_EXTENSION_NAME);
    st->hasSurfaceExt = true;
  }
#if defined(_WIN32)
  if (HasExtension(VK_KHR_WIN32_SURFACE_EXTENSION_NAME, instExts))
    enabledInst.push_back(VK_KHR_WIN32_SURFACE_EXTENSION_NAME);
#endif
#if defined(__ANDROID__)
  if (HasExtension("VK_KHR_android_surface", instExts))
    enabledInst.push_back("VK_KHR_android_surface");
#endif

  VkApplicationInfo app{};
  app.sType = VK_STRUCTURE_TYPE_APPLICATION_INFO;
  app.pApplicationName = "Anity";
  app.applicationVersion = VK_MAKE_VERSION(0, 1, 0);
  app.pEngineName = "anity-native";
  app.engineVersion = VK_MAKE_VERSION(0, 1, 0);
  app.apiVersion = VK_API_VERSION_1_1;

  VkInstanceCreateInfo ici{};
  ici.sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO;
  ici.pApplicationInfo = &app;
  ici.enabledExtensionCount = (uint32_t)enabledInst.size();
  ici.ppEnabledExtensionNames = enabledInst.empty() ? nullptr : enabledInst.data();

  VkResult vr = vkCreateInstance(&ici, nullptr, &st->instance);
  if (vr != VK_SUCCESS) {
    delete st;
    return ANITY_ERR_DEVICE_LOST;
  }

  uint32_t count = 0;
  vkEnumeratePhysicalDevices(st->instance, &count, nullptr);
  if (count == 0) {
    vkDestroyInstance(st->instance, nullptr);
    delete st;
    return ANITY_ERR_DEVICE_LOST;
  }
  std::vector<VkPhysicalDevice> physList(count);
  vkEnumeratePhysicalDevices(st->instance, &count, physList.data());
  st->phys = physList[0];

  uint32_t qCount = 0;
  vkGetPhysicalDeviceQueueFamilyProperties(st->phys, &qCount, nullptr);
  st->queueFamily = 0;
  if (qCount > 0) {
    std::vector<VkQueueFamilyProperties> props(qCount);
    vkGetPhysicalDeviceQueueFamilyProperties(st->phys, &qCount, props.data());
    for (uint32_t i = 0; i < qCount; i++) {
      if (props[i].queueFlags & VK_QUEUE_GRAPHICS_BIT) {
        st->queueFamily = i;
        break;
      }
    }
  }

  uint32_t devExtCount = 0;
  vkEnumerateDeviceExtensionProperties(st->phys, nullptr, &devExtCount, nullptr);
  std::vector<VkExtensionProperties> devExts(devExtCount);
  if (devExtCount)
    vkEnumerateDeviceExtensionProperties(st->phys, nullptr, &devExtCount, devExts.data());

  std::vector<const char*> enabledDev;
  if (HasExtension(VK_KHR_SWAPCHAIN_EXTENSION_NAME, devExts)) {
    enabledDev.push_back(VK_KHR_SWAPCHAIN_EXTENSION_NAME);
    st->hasSwapchainExt = true;
  }

  float prio = 1.f;
  VkDeviceQueueCreateInfo qci{};
  qci.sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
  qci.queueFamilyIndex = st->queueFamily;
  qci.queueCount = 1;
  qci.pQueuePriorities = &prio;

  VkDeviceCreateInfo dci{};
  dci.sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO;
  dci.queueCreateInfoCount = 1;
  dci.pQueueCreateInfos = &qci;
  dci.enabledExtensionCount = (uint32_t)enabledDev.size();
  dci.ppEnabledExtensionNames = enabledDev.empty() ? nullptr : enabledDev.data();

  vr = vkCreateDevice(st->phys, &dci, nullptr, &st->device);
  if (vr != VK_SUCCESS) {
    vkDestroyInstance(st->instance, nullptr);
    delete st;
    return ANITY_ERR_DEVICE_LOST;
  }
  vkGetDeviceQueue(st->device, st->queueFamily, 0, &st->queue);

  auto* dev = new (std::nothrow) AnityGraphicsDevice();
  if (!dev) {
    vkDestroyDevice(st->device, nullptr);
    vkDestroyInstance(st->instance, nullptr);
    delete st;
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  std::memset(dev, 0, sizeof(*dev));
  dev->type = ANITY_GFX_VULKAN;
  dev->width = desc->width > 0 ? desc->width : 1280;
  dev->height = desc->height > 0 ? desc->height : 720;
  dev->hdrEnabled = st->hdr ? 1 : 0;
  dev->msaaSamples = desc->msaaSamples;
  dev->vsync = desc->vsync;
  dev->supportsHdr = 1;
  dev->backend = st;
  dev->swapchain = nullptr;
  *outDevice = dev;
  return ANITY_OK;
}

extern "C" void AnityGraphics_Vulkan_Destroy(AnityGraphicsDevice* device) {
  if (!device || !device->backend) return;
  auto* st = reinterpret_cast<VkState*>(device->backend);
  if (st->device) vkDestroyDevice(st->device, nullptr);
  if (st->instance) vkDestroyInstance(st->instance, nullptr);
  delete st;
  device->backend = nullptr;
}

static AnityResult CreateWin32Surface(VkState* st, void* nativeWindow, VkSurfaceKHR* outSurface) {
  *outSurface = VK_NULL_HANDLE;
#if defined(_WIN32)
  if (!nativeWindow || !st->hasSurfaceExt) return ANITY_ERR_NOT_SUPPORTED;
  HWND hwnd = reinterpret_cast<HWND>(nativeWindow);
  if (!IsWindow(hwnd)) return ANITY_ERR_INVALID_ARG;

  VkWin32SurfaceCreateInfoKHR sci{};
  sci.sType = VK_STRUCTURE_TYPE_WIN32_SURFACE_CREATE_INFO_KHR;
  sci.hwnd = hwnd;
  sci.hinstance = GetModuleHandle(nullptr);
  VkResult vr = vkCreateWin32SurfaceKHR(st->instance, &sci, nullptr, outSurface);
  return vr == VK_SUCCESS ? ANITY_OK : ANITY_ERR_DEVICE_LOST;
#else
  (void)st;
  (void)nativeWindow;
  return ANITY_ERR_NOT_SUPPORTED;
#endif
}

extern "C" AnityResult AnityGraphics_Vulkan_CreateSwapchain(
    AnityGraphicsDevice* device, const AnitySwapchainDesc* desc, AnitySwapchain** out) {
  if (!device || !desc || !out) return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<VkState*>(device->backend);
  if (!st) return ANITY_ERR_INVALID_ARG;

  auto* sc = new (std::nothrow) AnitySwapchain();
  if (!sc) return ANITY_ERR_OUT_OF_MEMORY;
  std::memset(sc, 0, sizeof(*sc));
  sc->device = device;
  sc->width = desc->width > 0 ? desc->width : device->width;
  sc->height = desc->height > 0 ? desc->height : device->height;
  sc->imageCount = desc->imageCount > 0 ? desc->imageCount : 3;
  if (sc->imageCount > 4) sc->imageCount = 4;
  sc->vsync = desc->vsync;
  sc->hdr = desc->hdr;
  sc->headless = desc->nativeWindow == nullptr ? 1 : 0;
  sc->currentImage = 0;
  sc->presentCount = 0;

  auto* vst = new (std::nothrow) VkSwapchainState();
  if (!vst) {
    delete sc;
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  vst->width = sc->width;
  vst->height = sc->height;
  vst->imageCount = sc->imageCount;
  vst->headless = sc->headless;

  /* Try native surface + real VkSwapchainKHR when window provided */
  if (desc->nativeWindow && st->hasSurfaceExt && st->hasSwapchainExt) {
    if (CreateWin32Surface(st, desc->nativeWindow, &vst->surface) == ANITY_OK && vst->surface) {
      VkBool32 supported = VK_FALSE;
      vkGetPhysicalDeviceSurfaceSupportKHR(st->phys, st->queueFamily, vst->surface, &supported);
      if (supported) {
        VkSurfaceCapabilitiesKHR caps{};
        vkGetPhysicalDeviceSurfaceCapabilitiesKHR(st->phys, vst->surface, &caps);

        uint32_t fmtCount = 0;
        vkGetPhysicalDeviceSurfaceFormatsKHR(st->phys, vst->surface, &fmtCount, nullptr);
        std::vector<VkSurfaceFormatKHR> formats(fmtCount);
        if (fmtCount)
          vkGetPhysicalDeviceSurfaceFormatsKHR(st->phys, vst->surface, &fmtCount, formats.data());
        VkSurfaceFormatKHR fmt = formats.empty()
          ? VkSurfaceFormatKHR{VK_FORMAT_B8G8R8A8_UNORM, VK_COLOR_SPACE_SRGB_NONLINEAR_KHR}
          : formats[0];
        for (const auto& f : formats) {
          if (f.format == VK_FORMAT_B8G8R8A8_UNORM || f.format == VK_FORMAT_R8G8B8A8_UNORM) {
            fmt = f;
            break;
          }
        }

        uint32_t minImages = caps.minImageCount;
        uint32_t want = (uint32_t)std::max(minImages, (uint32_t)sc->imageCount);
        if (caps.maxImageCount > 0 && want > caps.maxImageCount)
          want = caps.maxImageCount;

        VkExtent2D extent{};
        if (caps.currentExtent.width != 0xFFFFFFFFu) {
          extent = caps.currentExtent;
        } else {
          extent.width = (uint32_t)sc->width;
          extent.height = (uint32_t)sc->height;
          extent.width = std::max(caps.minImageExtent.width, std::min(extent.width, caps.maxImageExtent.width));
          extent.height = std::max(caps.minImageExtent.height, std::min(extent.height, caps.maxImageExtent.height));
        }

        VkPresentModeKHR presentMode = VK_PRESENT_MODE_FIFO_KHR; /* vsync */
        if (!desc->vsync) {
          uint32_t pmCount = 0;
          vkGetPhysicalDeviceSurfacePresentModesKHR(st->phys, vst->surface, &pmCount, nullptr);
          std::vector<VkPresentModeKHR> modes(pmCount);
          if (pmCount)
            vkGetPhysicalDeviceSurfacePresentModesKHR(st->phys, vst->surface, &pmCount, modes.data());
          for (auto m : modes)
            if (m == VK_PRESENT_MODE_MAILBOX_KHR || m == VK_PRESENT_MODE_IMMEDIATE_KHR) {
              presentMode = m;
              break;
            }
        }

        VkSwapchainCreateInfoKHR sci{};
        sci.sType = VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR;
        sci.surface = vst->surface;
        sci.minImageCount = want;
        sci.imageFormat = fmt.format;
        sci.imageColorSpace = fmt.colorSpace;
        sci.imageExtent = extent;
        sci.imageArrayLayers = 1;
        sci.imageUsage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_TRANSFER_DST_BIT;
        sci.imageSharingMode = VK_SHARING_MODE_EXCLUSIVE;
        sci.preTransform = caps.currentTransform;
        sci.compositeAlpha = VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR;
        sci.presentMode = presentMode;
        sci.clipped = VK_TRUE;

        if (vkCreateSwapchainKHR(st->device, &sci, nullptr, &vst->swapchain) == VK_SUCCESS) {
          uint32_t imgCount = 0;
          vkGetSwapchainImagesKHR(st->device, vst->swapchain, &imgCount, nullptr);
          vst->images.resize(imgCount);
          if (imgCount)
            vkGetSwapchainImagesKHR(st->device, vst->swapchain, &imgCount, vst->images.data());
          vst->imageCount = (int32_t)imgCount;
          sc->imageCount = vst->imageCount;
          sc->width = (int32_t)extent.width;
          sc->height = (int32_t)extent.height;
          vst->width = sc->width;
          vst->height = sc->height;
          vst->hasNativeSurface = 1;
          sc->headless = 0;
          vst->headless = 0;
        }
      }
    }
  }

  /* Headless or failed native: software image ring (still valid Unity-like swapchain API) */
  if (!vst->swapchain) {
    vst->softwareImages = sc->imageCount;
    vst->hasNativeSurface = 0;
    vst->headless = 1;
    sc->headless = 1;
  }

  sc->backend = vst;
  device->swapchain = sc;
  *out = sc;
  return ANITY_OK;
}

extern "C" void AnityGraphics_Vulkan_DestroySwapchain(AnitySwapchain* swapchain) {
  if (!swapchain || !swapchain->backend) return;
  auto* vst = reinterpret_cast<VkSwapchainState*>(swapchain->backend);
  auto* st = swapchain->device ? reinterpret_cast<VkState*>(swapchain->device->backend) : nullptr;
  if (st && st->device && vst->swapchain) {
    vkDestroySwapchainKHR(st->device, vst->swapchain, nullptr);
    vst->swapchain = VK_NULL_HANDLE;
  }
  if (st && st->instance && vst->surface) {
    vkDestroySurfaceKHR(st->instance, vst->surface, nullptr);
    vst->surface = VK_NULL_HANDLE;
  }
  delete vst;
  swapchain->backend = nullptr;
}

extern "C" AnityResult AnityGraphics_Vulkan_Acquire(AnitySwapchain* swapchain, int32_t* outIndex) {
  if (!swapchain || !swapchain->backend) return ANITY_ERR_INVALID_ARG;
  auto* vst = reinterpret_cast<VkSwapchainState*>(swapchain->backend);
  auto* st = swapchain->device ? reinterpret_cast<VkState*>(swapchain->device->backend) : nullptr;

  if (vst->swapchain && st && st->device) {
    /* No semaphore for simplicity — headless-safe path uses timeout */
    uint32_t idx = 0;
    VkResult r = vkAcquireNextImageKHR(st->device, vst->swapchain, 0, VK_NULL_HANDLE, VK_NULL_HANDLE, &idx);
    if (r == VK_SUCCESS || r == VK_SUBOPTIMAL_KHR) {
      vst->imageIndex = idx;
      swapchain->currentImage = (int32_t)idx;
      if (outIndex) *outIndex = (int32_t)idx;
      return ANITY_OK;
    }
    /* fall through to software */
  }

  int n = vst->imageCount > 0 ? vst->imageCount : 1;
  swapchain->currentImage = (swapchain->currentImage + 1) % n;
  if (outIndex) *outIndex = swapchain->currentImage;
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Vulkan_Present(AnitySwapchain* swapchain) {
  if (!swapchain || !swapchain->backend) return ANITY_ERR_INVALID_ARG;
  auto* vst = reinterpret_cast<VkSwapchainState*>(swapchain->backend);
  auto* st = swapchain->device ? reinterpret_cast<VkState*>(swapchain->device->backend) : nullptr;

  if (vst->swapchain && st && st->queue) {
    uint32_t idx = (uint32_t)swapchain->currentImage;
    VkPresentInfoKHR pi{};
    pi.sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR;
    pi.swapchainCount = 1;
    pi.pSwapchains = &vst->swapchain;
    pi.pImageIndices = &idx;
    vkQueuePresentKHR(st->queue, &pi);
  }
  swapchain->presentCount++;
  return ANITY_OK;
}

extern "C" int32_t AnityGraphics_Vulkan_SwapchainHasNativeSurface(const AnitySwapchain* swapchain) {
  if (!swapchain || !swapchain->backend) return 0;
  return reinterpret_cast<const VkSwapchainState*>(swapchain->backend)->hasNativeSurface;
}

#else

extern "C" AnityResult AnityGraphics_CreateVulkan(
    const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" void AnityGraphics_Vulkan_Destroy(AnityGraphicsDevice*) {}
extern "C" AnityResult AnityGraphics_Vulkan_CreateSwapchain(
    AnityGraphicsDevice*, const AnitySwapchainDesc*, AnitySwapchain**) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" void AnityGraphics_Vulkan_DestroySwapchain(AnitySwapchain*) {}
extern "C" AnityResult AnityGraphics_Vulkan_Acquire(AnitySwapchain*, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Vulkan_Present(AnitySwapchain*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" int32_t AnityGraphics_Vulkan_SwapchainHasNativeSurface(const AnitySwapchain*) {
  return 0;
}

#endif
