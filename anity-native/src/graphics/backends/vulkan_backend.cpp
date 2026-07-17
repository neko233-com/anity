#define ANITY_NATIVE_BUILD
#include "../anity_graphics_device.h"
#include "../anity_graphics_texture_internal.h"
#include "../../ui/anity_ui_renderer_internal.h"
#include <new>
#include <cstring>
#include <vector>
#include <algorithm>
#include <cmath>
#include <cstddef>
#include <limits>
#include <memory>
#include <mutex>
#include <unordered_map>

#if defined(ANITY_HAS_VULKAN)
#include <vulkan/vulkan.h>
#include "../shaders/anity_ui_spirv.h"

#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <vulkan/vulkan_win32.h>
#endif

#if defined(__ANDROID__)
#include <android/native_window.h>
#include <vulkan/vulkan_android.h>
#endif

#if defined(ANITY_HAS_X11)
#include <X11/Xlib.h>
#include <vulkan/vulkan_xlib.h>
#endif

#if defined(ANITY_HAS_WAYLAND)
#include <wayland-client.h>
#include <vulkan/vulkan_wayland.h>
#endif

/* Surface kind: 0=none/headless, 1=Win32, 2=Android ANativeWindow, 3=X11, 4=Wayland */
enum AnityVkSurfaceKind : int32_t {
  ANITY_VK_SURFACE_NONE = 0,
  ANITY_VK_SURFACE_WIN32 = 1,
  ANITY_VK_SURFACE_ANDROID = 2,
  ANITY_VK_SURFACE_X11 = 3,
  ANITY_VK_SURFACE_WAYLAND = 4
};

struct VkSwapchainState {
  int32_t width = 0;
  int32_t height = 0;
  int32_t imageCount = 2;
  int32_t headless = 1;
  int32_t hasNativeSurface = 0;
  int32_t surfaceKind = ANITY_VK_SURFACE_NONE;
  VkSurfaceKHR surface = VK_NULL_HANDLE;
  VkSwapchainKHR swapchain = VK_NULL_HANDLE;
  std::vector<VkImage> images;
  std::vector<VkDeviceMemory> imageMemories;
  std::vector<VkImageView> imageViews;
  std::vector<VkFramebuffer> framebuffers;
  uint32_t imageIndex = 0;
  VkFormat format = VK_FORMAT_R8G8B8A8_UNORM;
  VkRenderPass renderPass = VK_NULL_HANDLE;
  VkPipeline pipeline = VK_NULL_HANDLE;
  VkSemaphore imageAvailable = VK_NULL_HANDLE;
  VkSemaphore renderFinished = VK_NULL_HANDLE;
  bool imageAcquired = false;
  bool ownsImages = false;
};

struct VkUIBuffer {
  VkBuffer buffer = VK_NULL_HANDLE;
  VkDeviceMemory memory = VK_NULL_HANDLE;
  VkDeviceSize capacity = 0;
};

struct VkTextureResource {
  VkImage image = VK_NULL_HANDLE;
  VkDeviceMemory memory = VK_NULL_HANDLE;
  VkImageView view = VK_NULL_HANDLE;
  VkSampler sampler = VK_NULL_HANDLE;
  VkDescriptorSet descriptorSet = VK_NULL_HANDLE;
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
  bool hasWin32SurfaceExt = false;
  bool hasAndroidSurfaceExt = false;
  bool hasXlibSurfaceExt = false;
  bool hasWaylandSurfaceExt = false;
  VkUIBuffer uiVertexBuffers[3];
  VkUIBuffer uiIndexBuffers[3];
  VkDeviceSize uiVertexLengths[3] = {0, 0, 0};
  VkDeviceSize uiIndexLengths[3] = {0, 0, 0};
  VkCommandPool commandPool = VK_NULL_HANDLE;
  VkCommandBuffer commandBuffers[3] = {VK_NULL_HANDLE, VK_NULL_HANDLE, VK_NULL_HANDLE};
  VkFence uiSlotFences[3] = {VK_NULL_HANDLE, VK_NULL_HANDLE, VK_NULL_HANDLE};
  VkPipelineLayout uiPipelineLayout = VK_NULL_HANDLE;
  VkDescriptorSetLayout uiTextureSetLayout = VK_NULL_HANDLE;
  VkDescriptorPool uiTextureDescriptorPool = VK_NULL_HANDLE;
  std::mutex textureMutex;
  std::unordered_map<uint64_t, std::unique_ptr<VkTextureResource>> textures;
  VkTextureResource whiteTexture;
};

static_assert(offsetof(AnityUIPackedVertex, color) == 12,
              "Vulkan UI vertex ABI color offset changed");
static_assert(sizeof(AnityUIPackedVertex) == 108,
              "Vulkan UI vertex ABI stride changed");

static VkDeviceSize GrowCapacity(VkDeviceSize required) {
  VkDeviceSize capacity = 4096;
  while (capacity < required && capacity <= UINT64_MAX / 2) capacity *= 2;
  return capacity < required ? required : capacity;
}

static bool FindMemoryType(VkState* st, uint32_t typeBits,
                           VkMemoryPropertyFlags required, uint32_t* outIndex) {
  if (!st || !outIndex) return false;
  VkPhysicalDeviceMemoryProperties properties{};
  vkGetPhysicalDeviceMemoryProperties(st->phys, &properties);
  for (uint32_t i = 0; i < properties.memoryTypeCount; ++i) {
    if ((typeBits & (1u << i)) != 0 &&
        (properties.memoryTypes[i].propertyFlags & required) == required) {
      *outIndex = i;
      return true;
    }
  }
  return false;
}

static void DestroyUIBuffer(VkState* st, VkUIBuffer* buffer) {
  if (!st || !buffer || !st->device) return;
  if (buffer->buffer) vkDestroyBuffer(st->device, buffer->buffer, nullptr);
  if (buffer->memory) vkFreeMemory(st->device, buffer->memory, nullptr);
  *buffer = {};
}

static AnityResult EnsureUIBuffer(VkState* st, VkUIBuffer* buffer,
                                  VkDeviceSize required, VkBufferUsageFlags usage) {
  if (!st || !buffer || !st->device) return ANITY_ERR_INVALID_ARG;
  if (required == 0 || (buffer->buffer && buffer->capacity >= required)) return ANITY_OK;

  DestroyUIBuffer(st, buffer);
  const VkDeviceSize capacity = GrowCapacity(required);
  VkBufferCreateInfo createInfo{};
  createInfo.sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
  createInfo.size = capacity;
  createInfo.usage = usage;
  createInfo.sharingMode = VK_SHARING_MODE_EXCLUSIVE;
  if (vkCreateBuffer(st->device, &createInfo, nullptr, &buffer->buffer) != VK_SUCCESS)
    return ANITY_ERR_OUT_OF_MEMORY;

  VkMemoryRequirements memoryRequirements{};
  vkGetBufferMemoryRequirements(st->device, buffer->buffer, &memoryRequirements);
  uint32_t memoryType = 0;
  constexpr VkMemoryPropertyFlags properties =
      VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT;
  if (!FindMemoryType(st, memoryRequirements.memoryTypeBits, properties, &memoryType)) {
    DestroyUIBuffer(st, buffer);
    return ANITY_ERR_NOT_SUPPORTED;
  }

  VkMemoryAllocateInfo allocateInfo{};
  allocateInfo.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
  allocateInfo.allocationSize = memoryRequirements.size;
  allocateInfo.memoryTypeIndex = memoryType;
  if (vkAllocateMemory(st->device, &allocateInfo, nullptr, &buffer->memory) != VK_SUCCESS) {
    DestroyUIBuffer(st, buffer);
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  if (vkBindBufferMemory(st->device, buffer->buffer, buffer->memory, 0) != VK_SUCCESS) {
    DestroyUIBuffer(st, buffer);
    return ANITY_ERR_DEVICE_LOST;
  }
  buffer->capacity = capacity;
  return ANITY_OK;
}

static AnityResult UploadUIBuffer(VkState* st, VkUIBuffer* buffer,
                                  const void* data, int32_t bytes,
                                  VkBufferUsageFlags usage) {
  if (bytes < 0 || (bytes > 0 && !data)) return ANITY_ERR_INVALID_ARG;
  if (bytes == 0) return ANITY_OK;
  AnityResult result = EnsureUIBuffer(st, buffer, static_cast<VkDeviceSize>(bytes), usage);
  if (result != ANITY_OK) return result;
  void* mapped = nullptr;
  if (vkMapMemory(st->device, buffer->memory, 0, static_cast<VkDeviceSize>(bytes), 0,
                  &mapped) != VK_SUCCESS)
    return ANITY_ERR_DEVICE_LOST;
  std::memcpy(mapped, data, static_cast<size_t>(bytes));
  vkUnmapMemory(st->device, buffer->memory);
  return ANITY_OK;
}

static AnityResult CreateImage(VkState* st, int32_t width, int32_t height,
                               uint32_t mipLevels, VkFormat format, VkImageUsageFlags usage,
                               VkImage* outImage, VkDeviceMemory* outMemory) {
  if (!st || width <= 0 || height <= 0 || mipLevels == 0 || !outImage || !outMemory)
    return ANITY_ERR_INVALID_ARG;
  *outImage = VK_NULL_HANDLE;
  *outMemory = VK_NULL_HANDLE;
  VkImageCreateInfo imageInfo{};
  imageInfo.sType = VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO;
  imageInfo.imageType = VK_IMAGE_TYPE_2D;
  imageInfo.format = format;
  imageInfo.extent = {static_cast<uint32_t>(width), static_cast<uint32_t>(height), 1};
  imageInfo.mipLevels = mipLevels;
  imageInfo.arrayLayers = 1;
  imageInfo.samples = VK_SAMPLE_COUNT_1_BIT;
  imageInfo.tiling = VK_IMAGE_TILING_OPTIMAL;
  imageInfo.usage = usage;
  imageInfo.sharingMode = VK_SHARING_MODE_EXCLUSIVE;
  imageInfo.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
  if (vkCreateImage(st->device, &imageInfo, nullptr, outImage) != VK_SUCCESS)
    return ANITY_ERR_OUT_OF_MEMORY;

  VkMemoryRequirements requirements{};
  vkGetImageMemoryRequirements(st->device, *outImage, &requirements);
  uint32_t memoryType = 0;
  if (!FindMemoryType(st, requirements.memoryTypeBits,
                      VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT, &memoryType)) {
    vkDestroyImage(st->device, *outImage, nullptr);
    *outImage = VK_NULL_HANDLE;
    return ANITY_ERR_NOT_SUPPORTED;
  }
  VkMemoryAllocateInfo allocation{};
  allocation.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
  allocation.allocationSize = requirements.size;
  allocation.memoryTypeIndex = memoryType;
  if (vkAllocateMemory(st->device, &allocation, nullptr, outMemory) != VK_SUCCESS) {
    vkDestroyImage(st->device, *outImage, nullptr);
    *outImage = VK_NULL_HANDLE;
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  if (vkBindImageMemory(st->device, *outImage, *outMemory, 0) != VK_SUCCESS) {
    vkFreeMemory(st->device, *outMemory, nullptr);
    vkDestroyImage(st->device, *outImage, nullptr);
    *outMemory = VK_NULL_HANDLE;
    *outImage = VK_NULL_HANDLE;
    return ANITY_ERR_DEVICE_LOST;
  }
  return ANITY_OK;
}

static void DestroyTextureResource(VkState* st, VkTextureResource* texture) {
  if (!st || !texture || !st->device) return;
  if (texture->descriptorSet && st->uiTextureDescriptorPool)
    vkFreeDescriptorSets(st->device, st->uiTextureDescriptorPool,
                         1, &texture->descriptorSet);
  if (texture->sampler) vkDestroySampler(st->device, texture->sampler, nullptr);
  if (texture->view) vkDestroyImageView(st->device, texture->view, nullptr);
  if (texture->image) vkDestroyImage(st->device, texture->image, nullptr);
  if (texture->memory) vkFreeMemory(st->device, texture->memory, nullptr);
  *texture = {};
}

static VkSamplerAddressMode ToVkAddressMode(int32_t mode) {
  switch (mode) {
    case 0: return VK_SAMPLER_ADDRESS_MODE_REPEAT;
    case 2: return VK_SAMPLER_ADDRESS_MODE_MIRRORED_REPEAT;
    default: return VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE;
  }
}

static AnityResult CreateTextureResource(
    VkState* st, const AnityGraphicsTextureDesc& desc,
    const uint8_t* pixels, int32_t byteCount, VkTextureResource* outTexture) {
  if (!st || !pixels || byteCount <= 0 || !outTexture ||
      !st->uiTextureSetLayout || !st->uiTextureDescriptorPool)
    return ANITY_ERR_INVALID_ARG;
  *outTexture = {};
  VkTextureResource texture{};
  VkFormat format = desc.linear != 0
      ? VK_FORMAT_R8G8B8A8_UNORM : VK_FORMAT_R8G8B8A8_SRGB;
  AnityResult result = CreateImage(st, desc.width, desc.height,
      static_cast<uint32_t>(desc.mipCount), format,
      VK_IMAGE_USAGE_TRANSFER_DST_BIT | VK_IMAGE_USAGE_SAMPLED_BIT,
      &texture.image, &texture.memory);
  if (result != ANITY_OK) return result;

  VkImageViewCreateInfo viewInfo{};
  viewInfo.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
  viewInfo.image = texture.image;
  viewInfo.viewType = VK_IMAGE_VIEW_TYPE_2D;
  viewInfo.format = format;
  viewInfo.subresourceRange.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
  viewInfo.subresourceRange.levelCount = static_cast<uint32_t>(desc.mipCount);
  viewInfo.subresourceRange.layerCount = 1;
  if (vkCreateImageView(st->device, &viewInfo, nullptr, &texture.view) != VK_SUCCESS) {
    DestroyTextureResource(st, &texture);
    return ANITY_ERR_DEVICE_LOST;
  }

  VkSamplerCreateInfo samplerInfo{};
  samplerInfo.sType = VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO;
  samplerInfo.magFilter = desc.filterMode == 0 ? VK_FILTER_NEAREST : VK_FILTER_LINEAR;
  samplerInfo.minFilter = desc.filterMode == 0 ? VK_FILTER_NEAREST : VK_FILTER_LINEAR;
  samplerInfo.mipmapMode = desc.filterMode == 2
      ? VK_SAMPLER_MIPMAP_MODE_LINEAR : VK_SAMPLER_MIPMAP_MODE_NEAREST;
  samplerInfo.addressModeU = ToVkAddressMode(desc.wrapU);
  samplerInfo.addressModeV = ToVkAddressMode(desc.wrapV);
  samplerInfo.addressModeW = VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE;
  samplerInfo.minLod = 0.f;
  samplerInfo.maxLod = static_cast<float>(std::max(0, desc.mipCount - 1));
  if (vkCreateSampler(st->device, &samplerInfo, nullptr, &texture.sampler) != VK_SUCCESS) {
    DestroyTextureResource(st, &texture);
    return ANITY_ERR_DEVICE_LOST;
  }

  VkDescriptorSetAllocateInfo descriptorAllocation{};
  descriptorAllocation.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO;
  descriptorAllocation.descriptorPool = st->uiTextureDescriptorPool;
  descriptorAllocation.descriptorSetCount = 1;
  descriptorAllocation.pSetLayouts = &st->uiTextureSetLayout;
  if (vkAllocateDescriptorSets(st->device, &descriptorAllocation,
                               &texture.descriptorSet) != VK_SUCCESS) {
    DestroyTextureResource(st, &texture);
    return ANITY_ERR_OUT_OF_MEMORY;
  }

  VkUIBuffer staging{};
  result = UploadUIBuffer(st, &staging, pixels, byteCount,
                          VK_BUFFER_USAGE_TRANSFER_SRC_BIT);
  if (result != ANITY_OK) {
    DestroyTextureResource(st, &texture);
    return result;
  }
  VkCommandBufferAllocateInfo commandAllocation{};
  commandAllocation.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
  commandAllocation.commandPool = st->commandPool;
  commandAllocation.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
  commandAllocation.commandBufferCount = 1;
  VkCommandBuffer command = VK_NULL_HANDLE;
  if (vkAllocateCommandBuffers(st->device, &commandAllocation, &command) != VK_SUCCESS) {
    DestroyUIBuffer(st, &staging);
    DestroyTextureResource(st, &texture);
    return ANITY_ERR_DEVICE_LOST;
  }
  VkCommandBufferBeginInfo beginInfo{};
  beginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
  beginInfo.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
  VkImageMemoryBarrier toTransfer{};
  toTransfer.sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER;
  toTransfer.srcAccessMask = 0;
  toTransfer.dstAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT;
  toTransfer.oldLayout = VK_IMAGE_LAYOUT_UNDEFINED;
  toTransfer.newLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
  toTransfer.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
  toTransfer.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
  toTransfer.image = texture.image;
  toTransfer.subresourceRange.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
  toTransfer.subresourceRange.levelCount = static_cast<uint32_t>(desc.mipCount);
  toTransfer.subresourceRange.layerCount = 1;
  std::vector<VkBufferImageCopy> copies(static_cast<size_t>(desc.mipCount));
  VkDeviceSize byteOffset = 0;
  int32_t mipWidth = desc.width;
  int32_t mipHeight = desc.height;
  for (int32_t mip = 0; mip < desc.mipCount; ++mip) {
    VkBufferImageCopy& copy = copies[static_cast<size_t>(mip)];
    copy.bufferOffset = byteOffset;
    copy.imageSubresource.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
    copy.imageSubresource.mipLevel = static_cast<uint32_t>(mip);
    copy.imageSubresource.layerCount = 1;
    copy.imageExtent = {static_cast<uint32_t>(mipWidth),
                        static_cast<uint32_t>(mipHeight), 1};
    byteOffset += static_cast<VkDeviceSize>(mipWidth) *
        static_cast<VkDeviceSize>(mipHeight) * 4u;
    mipWidth = std::max(1, mipWidth >> 1);
    mipHeight = std::max(1, mipHeight >> 1);
  }
  VkImageMemoryBarrier toShaderRead = toTransfer;
  toShaderRead.srcAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT;
  toShaderRead.dstAccessMask = VK_ACCESS_SHADER_READ_BIT;
  toShaderRead.oldLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
  toShaderRead.newLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
  result = ANITY_OK;
  if (vkBeginCommandBuffer(command, &beginInfo) != VK_SUCCESS) {
    result = ANITY_ERR_DEVICE_LOST;
  } else {
    vkCmdPipelineBarrier(command, VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT,
        VK_PIPELINE_STAGE_TRANSFER_BIT, 0, 0, nullptr, 0, nullptr, 1, &toTransfer);
    vkCmdCopyBufferToImage(command, staging.buffer, texture.image,
        VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
        static_cast<uint32_t>(copies.size()), copies.data());
    vkCmdPipelineBarrier(command, VK_PIPELINE_STAGE_TRANSFER_BIT,
        VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0, 0, nullptr, 0, nullptr,
        1, &toShaderRead);
    if (vkEndCommandBuffer(command) != VK_SUCCESS) {
      result = ANITY_ERR_DEVICE_LOST;
    } else {
      VkFenceCreateInfo fenceInfo{};
      fenceInfo.sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
      VkFence fence = VK_NULL_HANDLE;
      VkSubmitInfo submit{};
      submit.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
      submit.commandBufferCount = 1;
      submit.pCommandBuffers = &command;
      if (vkCreateFence(st->device, &fenceInfo, nullptr, &fence) != VK_SUCCESS ||
          vkQueueSubmit(st->queue, 1, &submit, fence) != VK_SUCCESS ||
          vkWaitForFences(st->device, 1, &fence, VK_TRUE, UINT64_MAX) != VK_SUCCESS) {
        result = ANITY_ERR_DEVICE_LOST;
      }
      if (fence) vkDestroyFence(st->device, fence, nullptr);
    }
  }
  vkFreeCommandBuffers(st->device, st->commandPool, 1, &command);
  DestroyUIBuffer(st, &staging);
  if (result != ANITY_OK) {
    DestroyTextureResource(st, &texture);
    return result;
  }

  VkDescriptorImageInfo imageInfo{};
  imageInfo.sampler = texture.sampler;
  imageInfo.imageView = texture.view;
  imageInfo.imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
  VkWriteDescriptorSet write{};
  write.sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
  write.dstSet = texture.descriptorSet;
  write.dstBinding = 0;
  write.descriptorCount = 1;
  write.descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
  write.pImageInfo = &imageInfo;
  vkUpdateDescriptorSets(st->device, 1, &write, 0, nullptr);
  *outTexture = texture;
  return ANITY_OK;
}

static void DestroyTextureInfrastructure(VkState* st) {
  if (!st || !st->device) return;
  {
    std::lock_guard<std::mutex> lock(st->textureMutex);
    for (auto& entry : st->textures)
      DestroyTextureResource(st, entry.second.get());
    st->textures.clear();
  }
  DestroyTextureResource(st, &st->whiteTexture);
  if (st->uiTextureDescriptorPool)
    vkDestroyDescriptorPool(st->device, st->uiTextureDescriptorPool, nullptr);
  if (st->uiTextureSetLayout)
    vkDestroyDescriptorSetLayout(st->device, st->uiTextureSetLayout, nullptr);
  st->uiTextureDescriptorPool = VK_NULL_HANDLE;
  st->uiTextureSetLayout = VK_NULL_HANDLE;
}

static VkShaderModule CreateShaderModule(VkState* st, const uint32_t* words,
                                         size_t byteCount) {
  if (!st || !words || byteCount == 0 || (byteCount % sizeof(uint32_t)) != 0)
    return VK_NULL_HANDLE;
  VkShaderModuleCreateInfo info{};
  info.sType = VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO;
  info.codeSize = byteCount;
  info.pCode = words;
  VkShaderModule module = VK_NULL_HANDLE;
  return vkCreateShaderModule(st->device, &info, nullptr, &module) == VK_SUCCESS
      ? module : VK_NULL_HANDLE;
}

static AnityResult CreateUIRenderResources(VkState* st, VkSwapchainState* vst) {
  if (!st || !vst || vst->images.empty()) return ANITY_ERR_INVALID_ARG;

  VkAttachmentDescription attachment{};
  attachment.format = vst->format;
  attachment.samples = VK_SAMPLE_COUNT_1_BIT;
  attachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
  attachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
  attachment.stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE;
  attachment.stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
  attachment.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
  attachment.finalLayout = vst->swapchain
      ? VK_IMAGE_LAYOUT_PRESENT_SRC_KHR : VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL;

  VkAttachmentReference colorReference{};
  colorReference.attachment = 0;
  colorReference.layout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
  VkSubpassDescription subpass{};
  subpass.pipelineBindPoint = VK_PIPELINE_BIND_POINT_GRAPHICS;
  subpass.colorAttachmentCount = 1;
  subpass.pColorAttachments = &colorReference;
  VkSubpassDependency dependency{};
  dependency.srcSubpass = VK_SUBPASS_EXTERNAL;
  dependency.dstSubpass = 0;
  dependency.srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
  dependency.dstStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
  dependency.dstAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
  VkRenderPassCreateInfo renderPassInfo{};
  renderPassInfo.sType = VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO;
  renderPassInfo.attachmentCount = 1;
  renderPassInfo.pAttachments = &attachment;
  renderPassInfo.subpassCount = 1;
  renderPassInfo.pSubpasses = &subpass;
  renderPassInfo.dependencyCount = 1;
  renderPassInfo.pDependencies = &dependency;
  if (vkCreateRenderPass(st->device, &renderPassInfo, nullptr, &vst->renderPass) != VK_SUCCESS)
    return ANITY_ERR_DEVICE_LOST;

  vst->imageViews.resize(vst->images.size(), VK_NULL_HANDLE);
  vst->framebuffers.resize(vst->images.size(), VK_NULL_HANDLE);
  for (size_t i = 0; i < vst->images.size(); ++i) {
    VkImageViewCreateInfo viewInfo{};
    viewInfo.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
    viewInfo.image = vst->images[i];
    viewInfo.viewType = VK_IMAGE_VIEW_TYPE_2D;
    viewInfo.format = vst->format;
    viewInfo.subresourceRange.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
    viewInfo.subresourceRange.levelCount = 1;
    viewInfo.subresourceRange.layerCount = 1;
    if (vkCreateImageView(st->device, &viewInfo, nullptr, &vst->imageViews[i]) != VK_SUCCESS)
      return ANITY_ERR_DEVICE_LOST;
    VkFramebufferCreateInfo framebufferInfo{};
    framebufferInfo.sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO;
    framebufferInfo.renderPass = vst->renderPass;
    framebufferInfo.attachmentCount = 1;
    framebufferInfo.pAttachments = &vst->imageViews[i];
    framebufferInfo.width = static_cast<uint32_t>(vst->width);
    framebufferInfo.height = static_cast<uint32_t>(vst->height);
    framebufferInfo.layers = 1;
    if (vkCreateFramebuffer(st->device, &framebufferInfo, nullptr,
                            &vst->framebuffers[i]) != VK_SUCCESS)
      return ANITY_ERR_DEVICE_LOST;
  }

  VkShaderModule vertex = CreateShaderModule(
      st, kAnityUIVertexSpirv, sizeof(kAnityUIVertexSpirv));
  VkShaderModule fragment = CreateShaderModule(
      st, kAnityUIFragmentSpirv, sizeof(kAnityUIFragmentSpirv));
  if (!vertex || !fragment) {
    if (vertex) vkDestroyShaderModule(st->device, vertex, nullptr);
    if (fragment) vkDestroyShaderModule(st->device, fragment, nullptr);
    return ANITY_ERR_NOT_SUPPORTED;
  }

  VkPipelineShaderStageCreateInfo stages[2]{};
  stages[0].sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
  stages[0].stage = VK_SHADER_STAGE_VERTEX_BIT;
  stages[0].module = vertex;
  stages[0].pName = "main";
  stages[1].sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
  stages[1].stage = VK_SHADER_STAGE_FRAGMENT_BIT;
  stages[1].module = fragment;
  stages[1].pName = "main";

  VkVertexInputBindingDescription binding{};
  binding.binding = 0;
  binding.stride = sizeof(AnityUIPackedVertex);
  binding.inputRate = VK_VERTEX_INPUT_RATE_VERTEX;
  VkVertexInputAttributeDescription attributes[3]{};
  attributes[0] = {0, 0, VK_FORMAT_R32G32B32_SFLOAT, 0};
  attributes[1] = {1, 0, VK_FORMAT_R8G8B8A8_UNORM,
                   static_cast<uint32_t>(offsetof(AnityUIPackedVertex, color))};
  attributes[2] = {2, 0, VK_FORMAT_R32G32B32A32_SFLOAT,
                   static_cast<uint32_t>(offsetof(AnityUIPackedVertex, uv0))};
  VkPipelineVertexInputStateCreateInfo vertexInput{};
  vertexInput.sType = VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO;
  vertexInput.vertexBindingDescriptionCount = 1;
  vertexInput.pVertexBindingDescriptions = &binding;
  vertexInput.vertexAttributeDescriptionCount = 3;
  vertexInput.pVertexAttributeDescriptions = attributes;
  VkPipelineInputAssemblyStateCreateInfo inputAssembly{};
  inputAssembly.sType = VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO;
  inputAssembly.topology = VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
  VkPipelineViewportStateCreateInfo viewportState{};
  viewportState.sType = VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO;
  viewportState.viewportCount = 1;
  viewportState.scissorCount = 1;
  VkPipelineRasterizationStateCreateInfo rasterizer{};
  rasterizer.sType = VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO;
  rasterizer.polygonMode = VK_POLYGON_MODE_FILL;
  rasterizer.cullMode = VK_CULL_MODE_NONE;
  rasterizer.frontFace = VK_FRONT_FACE_COUNTER_CLOCKWISE;
  rasterizer.lineWidth = 1.0f;
  VkPipelineMultisampleStateCreateInfo multisample{};
  multisample.sType = VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO;
  multisample.rasterizationSamples = VK_SAMPLE_COUNT_1_BIT;
  VkPipelineColorBlendAttachmentState blendAttachment{};
  blendAttachment.blendEnable = VK_TRUE;
  blendAttachment.srcColorBlendFactor = VK_BLEND_FACTOR_SRC_ALPHA;
  blendAttachment.dstColorBlendFactor = VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA;
  blendAttachment.colorBlendOp = VK_BLEND_OP_ADD;
  blendAttachment.srcAlphaBlendFactor = VK_BLEND_FACTOR_ONE;
  blendAttachment.dstAlphaBlendFactor = VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA;
  blendAttachment.alphaBlendOp = VK_BLEND_OP_ADD;
  blendAttachment.colorWriteMask = VK_COLOR_COMPONENT_R_BIT | VK_COLOR_COMPONENT_G_BIT |
      VK_COLOR_COMPONENT_B_BIT | VK_COLOR_COMPONENT_A_BIT;
  VkPipelineColorBlendStateCreateInfo blend{};
  blend.sType = VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO;
  blend.attachmentCount = 1;
  blend.pAttachments = &blendAttachment;
  VkDynamicState dynamicStates[] = {VK_DYNAMIC_STATE_VIEWPORT, VK_DYNAMIC_STATE_SCISSOR};
  VkPipelineDynamicStateCreateInfo dynamic{};
  dynamic.sType = VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO;
  dynamic.dynamicStateCount = 2;
  dynamic.pDynamicStates = dynamicStates;
  VkGraphicsPipelineCreateInfo pipelineInfo{};
  pipelineInfo.sType = VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO;
  pipelineInfo.stageCount = 2;
  pipelineInfo.pStages = stages;
  pipelineInfo.pVertexInputState = &vertexInput;
  pipelineInfo.pInputAssemblyState = &inputAssembly;
  pipelineInfo.pViewportState = &viewportState;
  pipelineInfo.pRasterizationState = &rasterizer;
  pipelineInfo.pMultisampleState = &multisample;
  pipelineInfo.pColorBlendState = &blend;
  pipelineInfo.pDynamicState = &dynamic;
  pipelineInfo.layout = st->uiPipelineLayout;
  pipelineInfo.renderPass = vst->renderPass;
  pipelineInfo.subpass = 0;
  VkResult pipelineResult = vkCreateGraphicsPipelines(
      st->device, VK_NULL_HANDLE, 1, &pipelineInfo, nullptr, &vst->pipeline);
  vkDestroyShaderModule(st->device, vertex, nullptr);
  vkDestroyShaderModule(st->device, fragment, nullptr);
  return pipelineResult == VK_SUCCESS ? ANITY_OK : ANITY_ERR_NOT_SUPPORTED;
}

static void DestroyUIRenderResources(VkState* st, VkSwapchainState* vst) {
  if (!st || !vst || !st->device) return;
  if (vst->pipeline) vkDestroyPipeline(st->device, vst->pipeline, nullptr);
  for (VkFramebuffer framebuffer : vst->framebuffers)
    if (framebuffer) vkDestroyFramebuffer(st->device, framebuffer, nullptr);
  for (VkImageView view : vst->imageViews)
    if (view) vkDestroyImageView(st->device, view, nullptr);
  if (vst->renderPass) vkDestroyRenderPass(st->device, vst->renderPass, nullptr);
  if (vst->ownsImages) {
    for (VkImage image : vst->images)
      if (image) vkDestroyImage(st->device, image, nullptr);
    for (VkDeviceMemory memory : vst->imageMemories)
      if (memory) vkFreeMemory(st->device, memory, nullptr);
  }
  if (vst->imageAvailable) vkDestroySemaphore(st->device, vst->imageAvailable, nullptr);
  if (vst->renderFinished) vkDestroySemaphore(st->device, vst->renderFinished, nullptr);
}

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
  if (HasExtension(VK_KHR_WIN32_SURFACE_EXTENSION_NAME, instExts)) {
    enabledInst.push_back(VK_KHR_WIN32_SURFACE_EXTENSION_NAME);
    st->hasWin32SurfaceExt = true;
  }
#endif
#if defined(__ANDROID__)
  if (HasExtension(VK_KHR_ANDROID_SURFACE_EXTENSION_NAME, instExts)) {
    enabledInst.push_back(VK_KHR_ANDROID_SURFACE_EXTENSION_NAME);
    st->hasAndroidSurfaceExt = true;
  }
#endif
#if defined(ANITY_HAS_X11)
  if (HasExtension(VK_KHR_XLIB_SURFACE_EXTENSION_NAME, instExts)) {
    enabledInst.push_back(VK_KHR_XLIB_SURFACE_EXTENSION_NAME);
    st->hasXlibSurfaceExt = true;
  }
#endif
#if defined(ANITY_HAS_WAYLAND)
  if (HasExtension(VK_KHR_WAYLAND_SURFACE_EXTENSION_NAME, instExts)) {
    enabledInst.push_back(VK_KHR_WAYLAND_SURFACE_EXTENSION_NAME);
    st->hasWaylandSurfaceExt = true;
  }
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

  VkDescriptorSetLayoutBinding textureBinding{};
  textureBinding.binding = 0;
  textureBinding.descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
  textureBinding.descriptorCount = 1;
  textureBinding.stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT;
  VkDescriptorSetLayoutCreateInfo setLayoutInfo{};
  setLayoutInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
  setLayoutInfo.bindingCount = 1;
  setLayoutInfo.pBindings = &textureBinding;
  if (vkCreateDescriptorSetLayout(st->device, &setLayoutInfo, nullptr,
                                  &st->uiTextureSetLayout) != VK_SUCCESS) {
    vkDestroyDevice(st->device, nullptr);
    vkDestroyInstance(st->instance, nullptr);
    delete st;
    return ANITY_ERR_DEVICE_LOST;
  }
  VkDescriptorPoolSize poolSize{};
  poolSize.type = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
  poolSize.descriptorCount = 4096;
  VkDescriptorPoolCreateInfo poolCreateInfo{};
  poolCreateInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
  poolCreateInfo.flags = VK_DESCRIPTOR_POOL_CREATE_FREE_DESCRIPTOR_SET_BIT;
  poolCreateInfo.maxSets = 4096;
  poolCreateInfo.poolSizeCount = 1;
  poolCreateInfo.pPoolSizes = &poolSize;
  if (vkCreateDescriptorPool(st->device, &poolCreateInfo, nullptr,
                             &st->uiTextureDescriptorPool) != VK_SUCCESS) {
    DestroyTextureInfrastructure(st);
    vkDestroyDevice(st->device, nullptr);
    vkDestroyInstance(st->instance, nullptr);
    delete st;
    return ANITY_ERR_DEVICE_LOST;
  }

  VkPushConstantRange pushConstant{};
  pushConstant.stageFlags = VK_SHADER_STAGE_VERTEX_BIT;
  pushConstant.offset = 0;
  pushConstant.size = sizeof(float) * 2;
  VkPipelineLayoutCreateInfo layoutInfo{};
  layoutInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
  layoutInfo.pushConstantRangeCount = 1;
  layoutInfo.pPushConstantRanges = &pushConstant;
  VkDescriptorSetLayout textureLayouts[2] = {
      st->uiTextureSetLayout, st->uiTextureSetLayout};
  layoutInfo.setLayoutCount = 2;
  layoutInfo.pSetLayouts = textureLayouts;
  if (vkCreatePipelineLayout(st->device, &layoutInfo, nullptr,
                             &st->uiPipelineLayout) != VK_SUCCESS) {
    DestroyTextureInfrastructure(st);
    vkDestroyDevice(st->device, nullptr);
    vkDestroyInstance(st->instance, nullptr);
    delete st;
    return ANITY_ERR_DEVICE_LOST;
  }
  VkCommandPoolCreateInfo poolInfo{};
  poolInfo.sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO;
  poolInfo.queueFamilyIndex = st->queueFamily;
  poolInfo.flags = VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT;
  if (vkCreateCommandPool(st->device, &poolInfo, nullptr, &st->commandPool) != VK_SUCCESS) {
    vkDestroyPipelineLayout(st->device, st->uiPipelineLayout, nullptr);
    DestroyTextureInfrastructure(st);
    vkDestroyDevice(st->device, nullptr);
    vkDestroyInstance(st->instance, nullptr);
    delete st;
    return ANITY_ERR_DEVICE_LOST;
  }
  VkCommandBufferAllocateInfo commandAllocation{};
  commandAllocation.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
  commandAllocation.commandPool = st->commandPool;
  commandAllocation.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
  commandAllocation.commandBufferCount = 3;
  if (vkAllocateCommandBuffers(st->device, &commandAllocation,
                               st->commandBuffers) != VK_SUCCESS) {
    vkDestroyCommandPool(st->device, st->commandPool, nullptr);
    vkDestroyPipelineLayout(st->device, st->uiPipelineLayout, nullptr);
    DestroyTextureInfrastructure(st);
    vkDestroyDevice(st->device, nullptr);
    vkDestroyInstance(st->instance, nullptr);
    delete st;
    return ANITY_ERR_DEVICE_LOST;
  }
  VkFenceCreateInfo fenceInfo{};
  fenceInfo.sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
  fenceInfo.flags = VK_FENCE_CREATE_SIGNALED_BIT;
  for (int i = 0; i < 3; ++i) {
    if (vkCreateFence(st->device, &fenceInfo, nullptr, &st->uiSlotFences[i]) != VK_SUCCESS) {
      for (int j = 0; j < i; ++j)
        vkDestroyFence(st->device, st->uiSlotFences[j], nullptr);
      vkDestroyCommandPool(st->device, st->commandPool, nullptr);
      vkDestroyPipelineLayout(st->device, st->uiPipelineLayout, nullptr);
      DestroyTextureInfrastructure(st);
      vkDestroyDevice(st->device, nullptr);
      vkDestroyInstance(st->instance, nullptr);
      delete st;
      return ANITY_ERR_DEVICE_LOST;
    }
  }

  AnityGraphicsTextureDesc whiteDesc{};
  whiteDesc.textureId = UINT64_MAX;
  whiteDesc.revision = 1;
  whiteDesc.width = 1;
  whiteDesc.height = 1;
  whiteDesc.mipCount = 1;
  whiteDesc.filterMode = 1;
  whiteDesc.wrapU = 1;
  whiteDesc.wrapV = 1;
  whiteDesc.linear = 1;
  const uint8_t whitePixel[4] = {255, 255, 255, 255};
  if (CreateTextureResource(st, whiteDesc, whitePixel, 4,
                            &st->whiteTexture) != ANITY_OK) {
    for (VkFence fence : st->uiSlotFences)
      if (fence) vkDestroyFence(st->device, fence, nullptr);
    vkDestroyCommandPool(st->device, st->commandPool, nullptr);
    vkDestroyPipelineLayout(st->device, st->uiPipelineLayout, nullptr);
    DestroyTextureInfrastructure(st);
    vkDestroyDevice(st->device, nullptr);
    vkDestroyInstance(st->instance, nullptr);
    delete st;
    return ANITY_ERR_DEVICE_LOST;
  }

  auto* dev = new (std::nothrow) AnityGraphicsDevice();
  if (!dev) {
    for (VkFence fence : st->uiSlotFences)
      if (fence) vkDestroyFence(st->device, fence, nullptr);
    vkDestroyCommandPool(st->device, st->commandPool, nullptr);
    vkDestroyPipelineLayout(st->device, st->uiPipelineLayout, nullptr);
    DestroyTextureInfrastructure(st);
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
  if (st->device) {
    vkDeviceWaitIdle(st->device);
    for (int i = 0; i < 3; ++i) {
      DestroyUIBuffer(st, &st->uiVertexBuffers[i]);
      DestroyUIBuffer(st, &st->uiIndexBuffers[i]);
      if (st->uiSlotFences[i])
        vkDestroyFence(st->device, st->uiSlotFences[i], nullptr);
    }
    DestroyTextureInfrastructure(st);
    if (st->commandPool) vkDestroyCommandPool(st->device, st->commandPool, nullptr);
    if (st->uiPipelineLayout)
      vkDestroyPipelineLayout(st->device, st->uiPipelineLayout, nullptr);
    vkDestroyDevice(st->device, nullptr);
  }
  if (st->instance) vkDestroyInstance(st->instance, nullptr);
  delete st;
  device->backend = nullptr;
}

extern "C" AnityResult AnityGraphics_Vulkan_UploadUI(
    AnityGraphicsDevice* device, int32_t ringIndex,
    const void* vertices, int32_t vertexBytes,
    const void* indices, int32_t indexBytes) {
  if (!device || !device->backend || ringIndex < 0 || ringIndex >= 3 ||
      vertexBytes < 0 || indexBytes < 0)
    return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<VkState*>(device->backend);
  if (vkWaitForFences(st->device, 1, &st->uiSlotFences[ringIndex], VK_TRUE,
                      UINT64_MAX) != VK_SUCCESS)
    return ANITY_ERR_DEVICE_LOST;
  AnityResult result = UploadUIBuffer(st, &st->uiVertexBuffers[ringIndex],
      vertices, vertexBytes, VK_BUFFER_USAGE_VERTEX_BUFFER_BIT);
  if (result != ANITY_OK) return result;
  result = UploadUIBuffer(st, &st->uiIndexBuffers[ringIndex],
      indices, indexBytes, VK_BUFFER_USAGE_INDEX_BUFFER_BIT);
  if (result == ANITY_OK) {
    st->uiVertexLengths[ringIndex] = static_cast<VkDeviceSize>(vertexBytes);
    st->uiIndexLengths[ringIndex] = static_cast<VkDeviceSize>(indexBytes);
  }
  return result;
}

extern "C" AnityResult AnityGraphics_Vulkan_DrawUI(
    AnityGraphicsDevice* device, int32_t ringIndex,
    const AnityUIDrawPacket* packets, int32_t packetCount, int32_t* outDrawCount) {
  if (!device || !device->backend || ringIndex < 0 || ringIndex >= 3 ||
      packetCount < 0 || (packetCount > 0 && !packets) || !outDrawCount)
    return ANITY_ERR_INVALID_ARG;
  *outDrawCount = 0;
  auto* st = reinterpret_cast<VkState*>(device->backend);
  auto* swapchain = device->swapchain;
  auto* vst = swapchain ? reinterpret_cast<VkSwapchainState*>(swapchain->backend) : nullptr;
  if (!vst || !vst->pipeline || vst->framebuffers.empty())
    return ANITY_ERR_NOT_SUPPORTED;
  if (packetCount > 0 && (!st->uiVertexBuffers[ringIndex].buffer ||
                          !st->uiIndexBuffers[ringIndex].buffer))
    return ANITY_ERR_INVALID_ARG;
  uint32_t imageIndex = vst->swapchain
      ? vst->imageIndex : static_cast<uint32_t>(swapchain->currentImage);
  if (imageIndex >= vst->framebuffers.size()) return ANITY_ERR_INVALID_ARG;
  if (vst->swapchain && !vst->imageAcquired) return ANITY_ERR_INVALID_ARG;

  VkCommandBuffer command = st->commandBuffers[ringIndex];
  if (vkResetCommandBuffer(command, 0) != VK_SUCCESS)
    return ANITY_ERR_DEVICE_LOST;
  VkCommandBufferBeginInfo beginInfo{};
  beginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
  beginInfo.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
  if (vkBeginCommandBuffer(command, &beginInfo) != VK_SUCCESS)
    return ANITY_ERR_DEVICE_LOST;

  VkClearValue clear{};
  clear.color = {{0.f, 0.f, 0.f, 0.f}};
  VkRenderPassBeginInfo passInfo{};
  passInfo.sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO;
  passInfo.renderPass = vst->renderPass;
  passInfo.framebuffer = vst->framebuffers[imageIndex];
  passInfo.renderArea.extent = {static_cast<uint32_t>(vst->width),
                                static_cast<uint32_t>(vst->height)};
  passInfo.clearValueCount = 1;
  passInfo.pClearValues = &clear;
  vkCmdBeginRenderPass(command, &passInfo, VK_SUBPASS_CONTENTS_INLINE);
  vkCmdBindPipeline(command, VK_PIPELINE_BIND_POINT_GRAPHICS, vst->pipeline);
  VkViewport viewport{};
  viewport.width = static_cast<float>(vst->width);
  viewport.height = static_cast<float>(vst->height);
  viewport.minDepth = 0.f;
  viewport.maxDepth = 1.f;
  vkCmdSetViewport(command, 0, 1, &viewport);
  if (packetCount > 0) {
    VkDeviceSize offset = 0;
    vkCmdBindVertexBuffers(command, 0, 1, &st->uiVertexBuffers[ringIndex].buffer, &offset);
    vkCmdBindIndexBuffer(command, st->uiIndexBuffers[ringIndex].buffer, 0,
                         VK_INDEX_TYPE_UINT32);
    const float dimensions[2] = {static_cast<float>(vst->width),
                                 static_cast<float>(vst->height)};
    vkCmdPushConstants(command, st->uiPipelineLayout, VK_SHADER_STAGE_VERTEX_BIT,
                       0, sizeof(dimensions), dimensions);
  }

  const uint64_t indexCapacity = st->uiIndexLengths[ringIndex] / sizeof(uint32_t);
  for (int32_t index = 0; index < packetCount; ++index) {
    const AnityUIDrawPacket& packet = packets[index];
    if ((packet.info.flags & (ANITY_UI_COMMAND_MASK | ANITY_UI_COMMAND_POP)) != 0)
      continue;
    const uint64_t endIndex = static_cast<uint64_t>(packet.firstIndex) +
        static_cast<uint64_t>(packet.info.indexCount);
    if (packet.info.indexCount <= 0 || endIndex > indexCapacity) {
      vkCmdEndRenderPass(command);
      vkEndCommandBuffer(command);
      return ANITY_ERR_INVALID_ARG;
    }
    int32_t minX = 0;
    int32_t minY = 0;
    int32_t maxX = vst->width;
    int32_t maxY = vst->height;
    if ((packet.info.flags & ANITY_UI_COMMAND_RECT_CLIP) != 0) {
      minX = std::max(0, static_cast<int32_t>(std::floor(packet.clipXMin)));
      minY = std::max(0, static_cast<int32_t>(std::floor(packet.clipYMin)));
      maxX = std::min(vst->width, static_cast<int32_t>(std::ceil(packet.clipXMax)));
      maxY = std::min(vst->height, static_cast<int32_t>(std::ceil(packet.clipYMax)));
      if (maxX <= minX || maxY <= minY) continue;
    }
    VkRect2D scissor{};
    scissor.offset = {minX, minY};
    scissor.extent = {static_cast<uint32_t>(maxX - minX),
                      static_cast<uint32_t>(maxY - minY)};
    vkCmdSetScissor(command, 0, 1, &scissor);
    VkDescriptorSet textureSets[2] = {
        st->whiteTexture.descriptorSet, st->whiteTexture.descriptorSet};
    {
      std::lock_guard<std::mutex> lock(st->textureMutex);
      auto main = st->textures.find(packet.info.textureId);
      if (main != st->textures.end())
        textureSets[0] = main->second->descriptorSet;
      auto alpha = st->textures.find(packet.info.alphaTextureId);
      if (alpha != st->textures.end())
        textureSets[1] = alpha->second->descriptorSet;
    }
    vkCmdBindDescriptorSets(command, VK_PIPELINE_BIND_POINT_GRAPHICS,
        st->uiPipelineLayout, 0, 2, textureSets, 0, nullptr);
    vkCmdDrawIndexed(command, static_cast<uint32_t>(packet.info.indexCount), 1,
                     packet.firstIndex, 0, 0);
    (*outDrawCount)++;
  }
  vkCmdEndRenderPass(command);
  if (vkEndCommandBuffer(command) != VK_SUCCESS)
    return ANITY_ERR_DEVICE_LOST;

  VkPipelineStageFlags waitStage = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
  VkSubmitInfo submit{};
  submit.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
  submit.commandBufferCount = 1;
  submit.pCommandBuffers = &command;
  if (vst->swapchain) {
    submit.waitSemaphoreCount = 1;
    submit.pWaitSemaphores = &vst->imageAvailable;
    submit.pWaitDstStageMask = &waitStage;
    submit.signalSemaphoreCount = 1;
    submit.pSignalSemaphores = &vst->renderFinished;
  }
  if (vkResetFences(st->device, 1, &st->uiSlotFences[ringIndex]) != VK_SUCCESS)
    return ANITY_ERR_DEVICE_LOST;
  if (vkQueueSubmit(st->queue, 1, &submit, st->uiSlotFences[ringIndex]) != VK_SUCCESS)
    return ANITY_ERR_DEVICE_LOST;
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Vulkan_SyncTexture(
    AnityGraphicsDevice* device, uint64_t textureId) {
  if (!device || !device->backend || textureId == 0) return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<VkState*>(device->backend);
  AnityGraphicsTextureSnapshot snapshot;
  if (!AnityGraphics_CopyTextureSnapshot(device, textureId, snapshot))
    return ANITY_ERR_INVALID_ARG;
  if (vkDeviceWaitIdle(st->device) != VK_SUCCESS) return ANITY_ERR_DEVICE_LOST;
  auto replacement = std::make_unique<VkTextureResource>();
  AnityResult result = CreateTextureResource(st, snapshot.info.desc,
      snapshot.rgba8.data(), static_cast<int32_t>(snapshot.rgba8.size()),
      replacement.get());
  if (result != ANITY_OK) return result;
  uintptr_t imageBits = 0;
  static_assert(sizeof(replacement->image) <= sizeof(imageBits),
                "VkImage cannot be represented by Unity native texture pointer");
  std::memcpy(&imageBits, &replacement->image, sizeof(replacement->image));
  void* nativeHandle = reinterpret_cast<void*>(imageBits);
  {
    std::lock_guard<std::mutex> lock(st->textureMutex);
    auto existing = st->textures.find(textureId);
    if (existing != st->textures.end())
      DestroyTextureResource(st, existing->second.get());
    st->textures[textureId] = std::move(replacement);
  }
  AnityGraphics_SetTextureBackendState(device, textureId, nativeHandle, 1);
  return ANITY_OK;
}

extern "C" void AnityGraphics_Vulkan_DestroyTexture(
    AnityGraphicsDevice* device, uint64_t textureId) {
  if (!device || !device->backend || textureId == 0) return;
  auto* st = reinterpret_cast<VkState*>(device->backend);
  vkDeviceWaitIdle(st->device);
  {
    std::lock_guard<std::mutex> lock(st->textureMutex);
    auto existing = st->textures.find(textureId);
    if (existing != st->textures.end()) {
      DestroyTextureResource(st, existing->second.get());
      st->textures.erase(existing);
    }
  }
  AnityGraphics_SetTextureBackendState(device, textureId, nullptr, 0);
}

static AnityResult CreateWin32Surface(VkState* st, void* nativeWindow, VkSurfaceKHR* outSurface) {
  *outSurface = VK_NULL_HANDLE;
#if defined(_WIN32)
  if (!nativeWindow || !st->hasSurfaceExt || !st->hasWin32SurfaceExt) return ANITY_ERR_NOT_SUPPORTED;
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

/* Android: nativeWindow is ANativeWindow* from ANativeActivity / SurfaceView. */
static AnityResult CreateAndroidSurface(VkState* st, void* nativeWindow, VkSurfaceKHR* outSurface) {
  *outSurface = VK_NULL_HANDLE;
#if defined(__ANDROID__)
  if (!nativeWindow || !st->hasSurfaceExt || !st->hasAndroidSurfaceExt) return ANITY_ERR_NOT_SUPPORTED;
  ANativeWindow* window = reinterpret_cast<ANativeWindow*>(nativeWindow);
  if (ANativeWindow_getWidth(window) <= 0) return ANITY_ERR_INVALID_ARG;

  VkAndroidSurfaceCreateInfoKHR sci{};
  sci.sType = VK_STRUCTURE_TYPE_ANDROID_SURFACE_CREATE_INFO_KHR;
  sci.window = window;
  VkResult vr = vkCreateAndroidSurfaceKHR(st->instance, &sci, nullptr, outSurface);
  return vr == VK_SUCCESS ? ANITY_OK : ANITY_ERR_DEVICE_LOST;
#else
  (void)st;
  (void)nativeWindow;
  return ANITY_ERR_NOT_SUPPORTED;
#endif
}

/* X11: nativeWindow is AnityX11NativeWindow* (see anity_graphics.h). */
static AnityResult CreateX11Surface(VkState* st, void* nativeWindow, VkSurfaceKHR* outSurface) {
  *outSurface = VK_NULL_HANDLE;
#if defined(ANITY_HAS_X11)
  if (!nativeWindow || !st->hasSurfaceExt || !st->hasXlibSurfaceExt) return ANITY_ERR_NOT_SUPPORTED;
  auto* xw = reinterpret_cast<AnityX11NativeWindow*>(nativeWindow);
  if (!xw->display || xw->window == 0) return ANITY_ERR_INVALID_ARG;

  VkXlibSurfaceCreateInfoKHR sci{};
  sci.sType = VK_STRUCTURE_TYPE_XLIB_SURFACE_CREATE_INFO_KHR;
  sci.dpy = reinterpret_cast<Display*>(xw->display);
  sci.window = (Window)xw->window;
  VkResult vr = vkCreateXlibSurfaceKHR(st->instance, &sci, nullptr, outSurface);
  return vr == VK_SUCCESS ? ANITY_OK : ANITY_ERR_DEVICE_LOST;
#else
  (void)st;
  (void)nativeWindow;
  return ANITY_ERR_NOT_SUPPORTED;
#endif
}

/* Wayland: nativeWindow is AnityWaylandNativeWindow* (see anity_graphics.h). */
static AnityResult CreateWaylandSurface(VkState* st, void* nativeWindow, VkSurfaceKHR* outSurface) {
  *outSurface = VK_NULL_HANDLE;
#if defined(ANITY_HAS_WAYLAND)
  if (!nativeWindow || !st->hasSurfaceExt || !st->hasWaylandSurfaceExt) return ANITY_ERR_NOT_SUPPORTED;
  auto* ww = reinterpret_cast<AnityWaylandNativeWindow*>(nativeWindow);
  if (!ww->display || !ww->surface) return ANITY_ERR_INVALID_ARG;

  VkWaylandSurfaceCreateInfoKHR sci{};
  sci.sType = VK_STRUCTURE_TYPE_WAYLAND_SURFACE_CREATE_INFO_KHR;
  sci.display = reinterpret_cast<wl_display*>(ww->display);
  sci.surface = reinterpret_cast<wl_surface*>(ww->surface);
  VkResult vr = vkCreateWaylandSurfaceKHR(st->instance, &sci, nullptr, outSurface);
  return vr == VK_SUCCESS ? ANITY_OK : ANITY_ERR_DEVICE_LOST;
#else
  (void)st;
  (void)nativeWindow;
  return ANITY_ERR_NOT_SUPPORTED;
#endif
}

/* Try platform surfaces in priority order for this build. */
static AnityResult CreatePlatformSurface(VkState* st, void* nativeWindow, VkSurfaceKHR* outSurface, int32_t* outKind) {
  *outSurface = VK_NULL_HANDLE;
  if (outKind) *outKind = ANITY_VK_SURFACE_NONE;
  if (!nativeWindow) return ANITY_ERR_INVALID_ARG;

#if defined(__ANDROID__)
  if (CreateAndroidSurface(st, nativeWindow, outSurface) == ANITY_OK && *outSurface) {
    if (outKind) *outKind = ANITY_VK_SURFACE_ANDROID;
    return ANITY_OK;
  }
#endif

#if defined(_WIN32)
  if (CreateWin32Surface(st, nativeWindow, outSurface) == ANITY_OK && *outSurface) {
    if (outKind) *outKind = ANITY_VK_SURFACE_WIN32;
    return ANITY_OK;
  }
#endif

#if defined(ANITY_HAS_X11)
  if (CreateX11Surface(st, nativeWindow, outSurface) == ANITY_OK && *outSurface) {
    if (outKind) *outKind = ANITY_VK_SURFACE_X11;
    return ANITY_OK;
  }
#endif

#if defined(ANITY_HAS_WAYLAND)
  if (CreateWaylandSurface(st, nativeWindow, outSurface) == ANITY_OK && *outSurface) {
    if (outKind) *outKind = ANITY_VK_SURFACE_WAYLAND;
    return ANITY_OK;
  }
#endif

  return ANITY_ERR_NOT_SUPPORTED;
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

  /* Try native surface (Win32 / Android ANativeWindow / X11 / Wayland) + real VkSwapchainKHR */
  if (desc->nativeWindow && st->hasSurfaceExt && st->hasSwapchainExt) {
    int32_t kind = ANITY_VK_SURFACE_NONE;
    if (CreatePlatformSurface(st, desc->nativeWindow, &vst->surface, &kind) == ANITY_OK && vst->surface) {
      vst->surfaceKind = kind;
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
        sci.imageUsage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT;
        if ((caps.supportedUsageFlags & VK_IMAGE_USAGE_TRANSFER_SRC_BIT) != 0)
          sci.imageUsage |= VK_IMAGE_USAGE_TRANSFER_SRC_BIT;
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
          vst->format = fmt.format;
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

  /* Headless or failed native: real offscreen Vulkan images for deterministic draw/readback. */
  if (!vst->swapchain) {
    vst->format = VK_FORMAT_R8G8B8A8_UNORM;
    vst->images.resize(static_cast<size_t>(sc->imageCount), VK_NULL_HANDLE);
    vst->imageMemories.resize(static_cast<size_t>(sc->imageCount), VK_NULL_HANDLE);
    vst->ownsImages = true;
    for (int32_t index = 0; index < sc->imageCount; ++index) {
      AnityResult imageResult = CreateImage(st, sc->width, sc->height, 1, vst->format,
          VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_TRANSFER_SRC_BIT,
          &vst->images[static_cast<size_t>(index)],
          &vst->imageMemories[static_cast<size_t>(index)]);
      if (imageResult != ANITY_OK) {
        DestroyUIRenderResources(st, vst);
        if (vst->surface) vkDestroySurfaceKHR(st->instance, vst->surface, nullptr);
        delete vst;
        delete sc;
        return imageResult;
      }
    }
    vst->hasNativeSurface = 0;
    vst->headless = 1;
    sc->headless = 1;
  }

  VkSemaphoreCreateInfo semaphoreInfo{};
  semaphoreInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO;
  if (vst->swapchain &&
      (vkCreateSemaphore(st->device, &semaphoreInfo, nullptr,
                         &vst->imageAvailable) != VK_SUCCESS ||
       vkCreateSemaphore(st->device, &semaphoreInfo, nullptr,
                         &vst->renderFinished) != VK_SUCCESS)) {
    DestroyUIRenderResources(st, vst);
    if (vst->swapchain) vkDestroySwapchainKHR(st->device, vst->swapchain, nullptr);
    if (vst->surface) vkDestroySurfaceKHR(st->instance, vst->surface, nullptr);
    delete vst;
    delete sc;
    return ANITY_ERR_DEVICE_LOST;
  }
  AnityResult resourcesResult = CreateUIRenderResources(st, vst);
  if (resourcesResult != ANITY_OK) {
    DestroyUIRenderResources(st, vst);
    if (vst->swapchain) vkDestroySwapchainKHR(st->device, vst->swapchain, nullptr);
    if (vst->surface) vkDestroySurfaceKHR(st->instance, vst->surface, nullptr);
    delete vst;
    delete sc;
    return resourcesResult;
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
  if (st && st->device) {
    vkDeviceWaitIdle(st->device);
    DestroyUIRenderResources(st, vst);
    if (vst->swapchain) {
      vkDestroySwapchainKHR(st->device, vst->swapchain, nullptr);
      vst->swapchain = VK_NULL_HANDLE;
    }
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
    uint32_t idx = 0;
    VkResult r = vkAcquireNextImageKHR(st->device, vst->swapchain, UINT64_MAX,
        vst->imageAvailable, VK_NULL_HANDLE, &idx);
    if (r == VK_SUCCESS || r == VK_SUBOPTIMAL_KHR) {
      vst->imageIndex = idx;
      vst->imageAcquired = true;
      swapchain->currentImage = (int32_t)idx;
      if (outIndex) *outIndex = (int32_t)idx;
      return ANITY_OK;
    }
    /* fall through to software */
  }

  int n = vst->imageCount > 0 ? vst->imageCount : 1;
  swapchain->currentImage = (swapchain->currentImage + 1) % n;
  vst->imageIndex = static_cast<uint32_t>(swapchain->currentImage);
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
    pi.waitSemaphoreCount = vst->imageAcquired ? 1u : 0u;
    pi.pWaitSemaphores = vst->imageAcquired ? &vst->renderFinished : nullptr;
    pi.swapchainCount = 1;
    pi.pSwapchains = &vst->swapchain;
    pi.pImageIndices = &idx;
    VkResult result = vkQueuePresentKHR(st->queue, &pi);
    vst->imageAcquired = false;
    if (result != VK_SUCCESS && result != VK_SUBOPTIMAL_KHR)
      return ANITY_ERR_DEVICE_LOST;
  }
  swapchain->presentCount++;
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Vulkan_ReadbackSwapchainRGBA8(
    AnitySwapchain* swapchain, uint8_t* pixels, int32_t pixelCapacity,
    int32_t* outWritten) {
  if (!swapchain || !swapchain->backend || pixelCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  *outWritten = 0;
  auto* vst = reinterpret_cast<VkSwapchainState*>(swapchain->backend);
  auto* st = swapchain->device
      ? reinterpret_cast<VkState*>(swapchain->device->backend) : nullptr;
  if (!st || vst->swapchain || vst->format != VK_FORMAT_R8G8B8A8_UNORM ||
      vst->images.empty())
    return ANITY_ERR_NOT_SUPPORTED;
  const uint64_t required64 = static_cast<uint64_t>(vst->width) *
      static_cast<uint64_t>(vst->height) * 4u;
  if (required64 > static_cast<uint64_t>(std::numeric_limits<int32_t>::max()))
    return ANITY_ERR_OUT_OF_MEMORY;
  const int32_t required = static_cast<int32_t>(required64);
  *outWritten = required;
  if (pixelCapacity < required || (required > 0 && !pixels))
    return ANITY_ERR_INVALID_ARG;

  for (VkFence fence : st->uiSlotFences) {
    if (vkWaitForFences(st->device, 1, &fence, VK_TRUE, UINT64_MAX) != VK_SUCCESS)
      return ANITY_ERR_DEVICE_LOST;
  }

  VkBufferCreateInfo bufferInfo{};
  bufferInfo.sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
  bufferInfo.size = static_cast<VkDeviceSize>(required);
  bufferInfo.usage = VK_BUFFER_USAGE_TRANSFER_DST_BIT;
  bufferInfo.sharingMode = VK_SHARING_MODE_EXCLUSIVE;
  VkBuffer staging = VK_NULL_HANDLE;
  if (vkCreateBuffer(st->device, &bufferInfo, nullptr, &staging) != VK_SUCCESS)
    return ANITY_ERR_OUT_OF_MEMORY;
  VkMemoryRequirements requirements{};
  vkGetBufferMemoryRequirements(st->device, staging, &requirements);
  uint32_t memoryType = 0;
  constexpr VkMemoryPropertyFlags hostMemory =
      VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT;
  if (!FindMemoryType(st, requirements.memoryTypeBits, hostMemory, &memoryType)) {
    vkDestroyBuffer(st->device, staging, nullptr);
    return ANITY_ERR_NOT_SUPPORTED;
  }
  VkMemoryAllocateInfo allocation{};
  allocation.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
  allocation.allocationSize = requirements.size;
  allocation.memoryTypeIndex = memoryType;
  VkDeviceMemory stagingMemory = VK_NULL_HANDLE;
  if (vkAllocateMemory(st->device, &allocation, nullptr, &stagingMemory) != VK_SUCCESS ||
      vkBindBufferMemory(st->device, staging, stagingMemory, 0) != VK_SUCCESS) {
    if (stagingMemory) vkFreeMemory(st->device, stagingMemory, nullptr);
    vkDestroyBuffer(st->device, staging, nullptr);
    return ANITY_ERR_OUT_OF_MEMORY;
  }

  VkCommandBufferAllocateInfo commandAllocation{};
  commandAllocation.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
  commandAllocation.commandPool = st->commandPool;
  commandAllocation.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
  commandAllocation.commandBufferCount = 1;
  VkCommandBuffer command = VK_NULL_HANDLE;
  AnityResult result = ANITY_OK;
  if (vkAllocateCommandBuffers(st->device, &commandAllocation, &command) != VK_SUCCESS) {
    result = ANITY_ERR_DEVICE_LOST;
  } else {
    VkCommandBufferBeginInfo beginInfo{};
    beginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
    beginInfo.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
    if (vkBeginCommandBuffer(command, &beginInfo) != VK_SUCCESS) {
      result = ANITY_ERR_DEVICE_LOST;
    } else {
      VkBufferImageCopy copy{};
      copy.imageSubresource.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
      copy.imageSubresource.layerCount = 1;
      copy.imageExtent = {static_cast<uint32_t>(vst->width),
                          static_cast<uint32_t>(vst->height), 1};
      uint32_t imageIndex = static_cast<uint32_t>(swapchain->currentImage);
      if (imageIndex >= vst->images.size()) imageIndex = 0;
      vkCmdCopyImageToBuffer(command, vst->images[imageIndex],
          VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, staging, 1, &copy);
      if (vkEndCommandBuffer(command) != VK_SUCCESS) {
        result = ANITY_ERR_DEVICE_LOST;
      } else {
        VkSubmitInfo submit{};
        submit.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
        submit.commandBufferCount = 1;
        submit.pCommandBuffers = &command;
        if (vkQueueSubmit(st->queue, 1, &submit, VK_NULL_HANDLE) != VK_SUCCESS ||
            vkQueueWaitIdle(st->queue) != VK_SUCCESS) {
          result = ANITY_ERR_DEVICE_LOST;
        } else {
          void* mapped = nullptr;
          if (vkMapMemory(st->device, stagingMemory, 0,
                          static_cast<VkDeviceSize>(required), 0, &mapped) != VK_SUCCESS) {
            result = ANITY_ERR_DEVICE_LOST;
          } else {
            std::memcpy(pixels, mapped, static_cast<size_t>(required));
            vkUnmapMemory(st->device, stagingMemory);
          }
        }
      }
    }
    vkFreeCommandBuffers(st->device, st->commandPool, 1, &command);
  }
  vkDestroyBuffer(st->device, staging, nullptr);
  vkFreeMemory(st->device, stagingMemory, nullptr);
  return result;
}

extern "C" int32_t AnityGraphics_Vulkan_SwapchainHasNativeSurface(const AnitySwapchain* swapchain) {
  if (!swapchain || !swapchain->backend) return 0;
  return reinterpret_cast<const VkSwapchainState*>(swapchain->backend)->hasNativeSurface;
}

extern "C" int32_t AnityGraphics_Vulkan_GetSwapchainSurfaceKind(const AnitySwapchain* swapchain) {
  if (!swapchain || !swapchain->backend) return ANITY_VK_SURFACE_NONE;
  return reinterpret_cast<const VkSwapchainState*>(swapchain->backend)->surfaceKind;
}

/* Compile-time supported surface platforms bitmask: bit0=Win32, bit1=Android, bit2=X11, bit3=Wayland */
extern "C" int32_t AnityGraphics_Vulkan_GetSupportedSurfaceMask() {
  int32_t mask = 0;
#if defined(_WIN32)
  mask |= 1; /* Win32 */
#endif
#if defined(__ANDROID__)
  mask |= 2; /* Android */
#endif
#if defined(ANITY_HAS_X11)
  mask |= 4; /* X11 */
#endif
#if defined(ANITY_HAS_WAYLAND)
  mask |= 8; /* Wayland */
#endif
  return mask;
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
extern "C" int32_t AnityGraphics_Vulkan_GetSwapchainSurfaceKind(const AnitySwapchain*) {
  return 0;
}
extern "C" int32_t AnityGraphics_Vulkan_GetSupportedSurfaceMask() {
  return 0;
}
extern "C" AnityResult AnityGraphics_Vulkan_UploadUI(
    AnityGraphicsDevice*, int32_t, const void*, int32_t, const void*, int32_t) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Vulkan_DrawUI(
    AnityGraphicsDevice*, int32_t, const AnityUIDrawPacket*, int32_t, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Vulkan_ReadbackSwapchainRGBA8(
    AnitySwapchain*, uint8_t*, int32_t, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Vulkan_SyncTexture(
    AnityGraphicsDevice*, uint64_t) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" void AnityGraphics_Vulkan_DestroyTexture(
    AnityGraphicsDevice*, uint64_t) {}

#endif
