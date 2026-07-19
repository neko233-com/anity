#define ANITY_NATIVE_BUILD
#include "anity_graphics_texture_internal.h"

#include <cmath>
#include <limits>
#include <memory>
#include <mutex>
#include <new>
#include <unordered_map>

extern "C" AnityResult AnityGraphics_Metal_SyncTexture(
    AnityGraphicsDevice* device, uint64_t textureId);
extern "C" void AnityGraphics_Metal_DestroyTexture(
    AnityGraphicsDevice* device, uint64_t textureId);
extern "C" AnityResult AnityGraphics_D3D11_SyncTexture(
    AnityGraphicsDevice* device, uint64_t textureId);
extern "C" void AnityGraphics_D3D11_DestroyTexture(
    AnityGraphicsDevice* device, uint64_t textureId);
extern "C" AnityResult AnityGraphics_Vulkan_SyncTexture(
    AnityGraphicsDevice* device, uint64_t textureId);
extern "C" void AnityGraphics_Vulkan_DestroyTexture(
    AnityGraphicsDevice* device, uint64_t textureId);

struct AnityGraphicsTextureStorage {
  AnityGraphicsTextureInfo info{};
  std::vector<uint8_t> rgba8;
  void* nativeHandle = nullptr;
};

struct AnityGraphicsTextureRegistry {
  std::mutex operationMutex;
  mutable std::mutex mutex;
  uint64_t generation = 0;
  std::unordered_map<uint64_t, std::unique_ptr<AnityGraphicsTextureStorage>> entries;
};

namespace {

bool IsValidTextureDesc(const AnityGraphicsTextureDesc& desc, int32_t byteCount) {
  if (desc.textureId == 0 || desc.width <= 0 || desc.height <= 0 ||
      desc.mipCount <= 0 || byteCount < 0 || desc.filterMode < 0 ||
      desc.filterMode > 2 || desc.wrapU < 0 || desc.wrapU > 3 ||
      desc.wrapV < 0 || desc.wrapV > 3 || (desc.linear != 0 && desc.linear != 1) ||
      !std::isfinite(desc.mipMapBias) || desc.anisoLevel < 0 || desc.anisoLevel > 16)
    return false;
  int32_t fullMipCount = 1;
  int32_t fullWidth = desc.width;
  int32_t fullHeight = desc.height;
  while (fullWidth > 1 || fullHeight > 1) {
    fullWidth = fullWidth > 1 ? fullWidth >> 1 : 1;
    fullHeight = fullHeight > 1 ? fullHeight >> 1 : 1;
    ++fullMipCount;
  }
  if (desc.mipCount > fullMipCount) return false;
  int64_t required = 0;
  int32_t mipWidth = desc.width;
  int32_t mipHeight = desc.height;
  for (int32_t mip = 0; mip < desc.mipCount; ++mip) {
    required += static_cast<int64_t>(mipWidth) * mipHeight * 4;
    mipWidth = mipWidth > 1 ? mipWidth >> 1 : 1;
    mipHeight = mipHeight > 1 ? mipHeight >> 1 : 1;
  }
  return required <= std::numeric_limits<int32_t>::max() && byteCount == required;
}

AnityGraphicsTextureRegistry* EnsureRegistry(AnityGraphicsDevice* device) {
  if (!device) return nullptr;
  if (!device->textures) {
    static std::mutex creationMutex;
    std::lock_guard<std::mutex> lock(creationMutex);
    if (!device->textures)
      device->textures = new (std::nothrow) AnityGraphicsTextureRegistry();
  }
  return device->textures;
}

}  // namespace

void AnityGraphics_DestroyTextureRegistry(AnityGraphicsDevice* device) {
  if (!device) return;
  delete device->textures;
  device->textures = nullptr;
}

bool AnityGraphics_CopyTextureSnapshot(
    const AnityGraphicsDevice* device, uint64_t textureId,
    AnityGraphicsTextureSnapshot& outSnapshot) {
  outSnapshot = {};
  if (!device || !device->textures || textureId == 0) return false;
  const AnityGraphicsTextureRegistry* registry = device->textures;
  std::lock_guard<std::mutex> lock(registry->mutex);
  auto found = registry->entries.find(textureId);
  if (found == registry->entries.end()) return false;
  try {
    outSnapshot.info = found->second->info;
    outSnapshot.rgba8 = found->second->rgba8;
    outSnapshot.nativeHandle = found->second->nativeHandle;
    return true;
  } catch (const std::bad_alloc&) {
    outSnapshot = {};
    return false;
  }
}

void AnityGraphics_SetTextureBackendState(
    AnityGraphicsDevice* device, uint64_t textureId,
    void* nativeHandle, int32_t backendKind) {
  if (!device || !device->textures || textureId == 0) return;
  std::lock_guard<std::mutex> lock(device->textures->mutex);
  auto found = device->textures->entries.find(textureId);
  if (found == device->textures->entries.end()) return;
  found->second->nativeHandle = nativeHandle;
  found->second->info.backendKind = nativeHandle ? backendKind : 0;
}

extern "C" {

AnityResult ANITY_CALL AnityGraphics_UploadTextureRGBA8(
    AnityGraphicsDevice* device, const AnityGraphicsTextureDesc* desc,
    const uint8_t* pixels, int32_t byteCount) {
  if (!device || !desc || !pixels || !IsValidTextureDesc(*desc, byteCount))
    return ANITY_ERR_INVALID_ARG;
  AnityGraphicsTextureRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  try {
    std::lock_guard<std::mutex> operationLock(registry->operationMutex);
    auto replacement = std::make_unique<AnityGraphicsTextureStorage>();
    replacement->rgba8.assign(pixels, pixels + byteCount);
    replacement->info.desc = *desc;
    /* Zero was the field's value for callers compiled against the previous
     * descriptor. Normalize it to Unity's default aniso level. */
    if (replacement->info.desc.anisoLevel == 0)
      replacement->info.desc.anisoLevel = 1;
    replacement->info.byteCount = byteCount;
    replacement->info.backendKind = 0;
    {
      std::lock_guard<std::mutex> lock(registry->mutex);
      replacement->info.uploadGeneration = ++registry->generation;
      registry->entries[desc->textureId] = std::move(replacement);
    }
    if (device->type == ANITY_GFX_D3D11 || device->type == ANITY_GFX_D3D12) {
      AnityResult backendResult = AnityGraphics_D3D11_SyncTexture(device, desc->textureId);
      if (backendResult != ANITY_OK && backendResult != ANITY_ERR_NOT_SUPPORTED)
        return backendResult;
    } else if (device->type == ANITY_GFX_METAL) {
      AnityResult backendResult = AnityGraphics_Metal_SyncTexture(device, desc->textureId);
      if (backendResult != ANITY_OK && backendResult != ANITY_ERR_NOT_SUPPORTED)
        return backendResult;
    } else if (device->type == ANITY_GFX_VULKAN) {
      AnityResult backendResult = AnityGraphics_Vulkan_SyncTexture(device, desc->textureId);
      if (backendResult != ANITY_OK && backendResult != ANITY_ERR_NOT_SUPPORTED)
        return backendResult;
    }
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

AnityResult ANITY_CALL AnityGraphics_DestroyTexture(
    AnityGraphicsDevice* device, uint64_t textureId) {
  if (!device || textureId == 0) return ANITY_ERR_INVALID_ARG;
  if (!device->textures) return ANITY_OK;
  std::lock_guard<std::mutex> operationLock(device->textures->operationMutex);
  if (device->type == ANITY_GFX_D3D11 || device->type == ANITY_GFX_D3D12)
    AnityGraphics_D3D11_DestroyTexture(device, textureId);
  else if (device->type == ANITY_GFX_METAL)
    AnityGraphics_Metal_DestroyTexture(device, textureId);
  else if (device->type == ANITY_GFX_VULKAN)
    AnityGraphics_Vulkan_DestroyTexture(device, textureId);
  std::lock_guard<std::mutex> lock(device->textures->mutex);
  device->textures->entries.erase(textureId);
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_GetTextureInfo(
    const AnityGraphicsDevice* device, uint64_t textureId,
    AnityGraphicsTextureInfo* outInfo) {
  if (!device || textureId == 0 || !outInfo) return ANITY_ERR_INVALID_ARG;
  *outInfo = {};
  if (!device->textures) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->textures->mutex);
  auto found = device->textures->entries.find(textureId);
  if (found == device->textures->entries.end()) return ANITY_ERR_INVALID_ARG;
  *outInfo = found->second->info;
  return ANITY_OK;
}

void* ANITY_CALL AnityGraphics_GetTextureNativeHandle(
    const AnityGraphicsDevice* device, uint64_t textureId) {
  if (!device || !device->textures || textureId == 0) return nullptr;
  std::lock_guard<std::mutex> lock(device->textures->mutex);
  auto found = device->textures->entries.find(textureId);
  return found == device->textures->entries.end()
      ? nullptr : found->second->nativeHandle;
}

}  // extern "C"
