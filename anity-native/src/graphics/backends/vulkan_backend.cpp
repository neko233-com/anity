#define ANITY_NATIVE_BUILD
#include "../anity_graphics_device.h"
#include <new>
#include <cstring>

#if defined(ANITY_HAS_VULKAN)
#include <vulkan/vulkan.h>

struct VkState {
  VkInstance instance = VK_NULL_HANDLE;
  VkPhysicalDevice phys = VK_NULL_HANDLE;
  VkDevice device = VK_NULL_HANDLE;
  bool hdr = false;
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

  float prio = 1.f;
  VkDeviceQueueCreateInfo qci{};
  qci.sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
  qci.queueFamilyIndex = 0;
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
  dev->type = ANITY_GFX_VULKAN;
  dev->width = desc->width > 0 ? desc->width : 1280;
  dev->height = desc->height > 0 ? desc->height : 720;
  dev->hdrEnabled = st->hdr ? 1 : 0;
  dev->msaaSamples = desc->msaaSamples;
  dev->vsync = desc->vsync;
  dev->supportsHdr = 1;
  dev->backend = st;
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

#else

extern "C" AnityResult AnityGraphics_CreateVulkan(
    const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" void AnityGraphics_Vulkan_Destroy(AnityGraphicsDevice*) {}

#endif
