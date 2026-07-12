#define ANITY_NATIVE_BUILD
#include "../anity_graphics_device.h"
#include <new>
#include <cstring>

#if defined(ANITY_HAS_VULKAN)
#include <vulkan/vulkan.h>

struct VkSwapchainState {
  int32_t width = 0;
  int32_t height = 0;
  int32_t imageCount = 2;
  int32_t headless = 1;
  /* Real VkSwapchainKHR would live here when surface is available */
  VkSwapchainKHR swapchain = VK_NULL_HANDLE;
};

struct VkState {
  VkInstance instance = VK_NULL_HANDLE;
  VkPhysicalDevice phys = VK_NULL_HANDLE;
  VkDevice device = VK_NULL_HANDLE;
  bool hdr = false;
  uint32_t queueFamily = 0;
};

extern "C" AnityResult AnityGraphics_CreateVulkan(
    const AnityGraphicsDeviceDesc* desc, AnityGraphicsDevice** outDevice) {
  if (!desc || !outDevice) return ANITY_ERR_INVALID_ARG;

  auto* st = new (std::nothrow) VkState();
  if (!st) return ANITY_ERR_OUT_OF_MEMORY;
  st->hdr = desc->hdrEnabled != 0;

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
  VkPhysicalDevice physList[8];
  if (count > 8) count = 8;
  vkEnumeratePhysicalDevices(st->instance, &count, physList);
  st->phys = physList[0];

  /* Find a graphics queue family */
  uint32_t qCount = 0;
  vkGetPhysicalDeviceQueueFamilyProperties(st->phys, &qCount, nullptr);
  st->queueFamily = 0;
  if (qCount > 0) {
    VkQueueFamilyProperties* props = new (std::nothrow) VkQueueFamilyProperties[qCount];
    if (props) {
      vkGetPhysicalDeviceQueueFamilyProperties(st->phys, &qCount, props);
      for (uint32_t i = 0; i < qCount; i++) {
        if (props[i].queueFlags & VK_QUEUE_GRAPHICS_BIT) {
          st->queueFamily = i;
          break;
        }
      }
      delete[] props;
    }
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

  vr = vkCreateDevice(st->phys, &dci, nullptr, &st->device);
  if (vr != VK_SUCCESS) {
    vkDestroyInstance(st->instance, nullptr);
    delete st;
    return ANITY_ERR_DEVICE_LOST;
  }

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

extern "C" AnityResult AnityGraphics_Vulkan_CreateSwapchain(
    AnityGraphicsDevice* device, const AnitySwapchainDesc* desc, AnitySwapchain** out) {
  if (!device || !desc || !out) return ANITY_ERR_INVALID_ARG;

  auto* sc = new (std::nothrow) AnitySwapchain();
  if (!sc) return ANITY_ERR_OUT_OF_MEMORY;
  std::memset(sc, 0, sizeof(*sc));
  sc->device = device;
  sc->width = desc->width > 0 ? desc->width : device->width;
  sc->height = desc->height > 0 ? desc->height : device->height;
  sc->imageCount = desc->imageCount > 0 ? desc->imageCount : 3; /* triple-buffer common on Vulkan */
  if (sc->imageCount > 4) sc->imageCount = 4;
  sc->vsync = desc->vsync;
  sc->hdr = desc->hdr;
  sc->headless = desc->nativeWindow == nullptr ? 1 : 0;
  sc->currentImage = 0;

  auto* vst = new (std::nothrow) VkSwapchainState();
  if (!vst) {
    delete sc;
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  vst->width = sc->width;
  vst->height = sc->height;
  vst->imageCount = sc->imageCount;
  vst->headless = sc->headless;
  /* Without a platform surface we keep a headless swapchain record.
     Real VkSwapchainKHR creation requires VkSurfaceKHR (Win32/Android/X11). */
  sc->backend = vst;
  device->swapchain = sc;
  *out = sc;
  return ANITY_OK;
}

extern "C" void AnityGraphics_Vulkan_DestroySwapchain(AnitySwapchain* swapchain) {
  if (!swapchain) return;
  if (swapchain->backend) {
    auto* vst = reinterpret_cast<VkSwapchainState*>(swapchain->backend);
    /* if (vst->swapchain) vkDestroySwapchainKHR(...); */
    delete vst;
    swapchain->backend = nullptr;
  }
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

#endif
