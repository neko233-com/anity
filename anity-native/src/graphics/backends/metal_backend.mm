#define ANITY_NATIVE_BUILD
#include "../anity_graphics_device.h"
#include "../anity_graphics_texture_internal.h"
#include "../../ui/anity_ui_renderer_internal.h"
#include <new>
#include <cstring>
#include <algorithm>
#include <cmath>
#include <limits>
#include <vector>
#include <deque>
#include <mutex>
#include <condition_variable>
#include <chrono>
#include <atomic>
#include <unordered_map>

#if defined(ANITY_HAS_METAL)
#import <Metal/Metal.h>
#import <Foundation/Foundation.h>
#import <QuartzCore/CAMetalLayer.h>
#import <CoreGraphics/CoreGraphics.h>
#import <dispatch/dispatch.h>

static std::mutex gMetalVFXPipelineCompilationMutex;

struct MetalTextureEntry {
  id<MTLTexture> texture = nil;
  id<MTLSamplerState> sampler = nil;
  uint64_t revision = 0;
};

struct MetalVFXParticleKey {
  uint64_t effectId = 0;
  int32_t particleSystemId = 0;

  bool operator==(const MetalVFXParticleKey& other) const {
    return effectId == other.effectId && particleSystemId == other.particleSystemId;
  }
};

struct MetalVFXParticleKeyHash {
  size_t operator()(const MetalVFXParticleKey& key) const {
    const size_t a = std::hash<uint64_t>{}(key.effectId);
    const size_t b = std::hash<int32_t>{}(key.particleSystemId);
    return a ^ (b + 0x9e3779b9u + (a << 6) + (a >> 2));
  }
};

struct MetalVFXUpdateSlot {
  id<MTLBuffer> output = nil;
  id<MTLBuffer> allocationState = nil;
  id<MTLBuffer> residentDeadList = nil;
  id<MTLBuffer> operations = nil;
  id<MTLBuffer> deathPrefix = nil;
  id<MTLBuffer> deathScratch = nil;
  id<MTLBuffer> deadIndices = nil;
  id<MTLBuffer> deadCount = nil;
  NSUInteger outputCapacity = 0;
  NSUInteger allocationStateCapacity = 0;
  NSUInteger residentDeadListCapacity = 0;
  NSUInteger operationCapacity = 0;
  NSUInteger deathPrefixCapacity = 0;
  NSUInteger deathScratchCapacity = 0;
  NSUInteger deadIndexCapacity = 0;
  NSUInteger deadCountCapacity = 0;
  dispatch_semaphore_t available = nullptr;
  dispatch_semaphore_t completed = nullptr;
};

struct MetalVFXResidentSnapshot {
  id<MTLBuffer> buffer = nil;
  NSUInteger capacity = 0;
  id<MTLBuffer> allocationState = nil;
  NSUInteger allocationStateCapacity = 0;
  id<MTLBuffer> deadList = nil;
  NSUInteger deadListCapacity = 0;
};

constexpr int32_t kMetalVFXPlanarSortCacheCapacity = 4;
constexpr size_t kMetalVFXPlanarSubmissionResultCapacity = 1024u;

struct MetalVFXPlanarSortCacheEntry {
  id<MTLBuffer> entries = nil;
  id<MTLBuffer> sortedIndices = nil;
  NSUInteger entryCapacity = 0;
  NSUInteger sortedIndexCapacity = 0;
  uint64_t generation = 0;
  uint64_t cameraId = 0;
  uint64_t lastUseSerial = 0;
  int32_t strideWords = 0;
  int32_t particleCapacity = 0;
  int32_t positionOffsetWords = -1;
  uint32_t paddedLength = 0;
  float localToWorld[16] = {};
  float worldToClip[16] = {};

  MetalVFXPlanarSortCacheEntry() = default;
  ~MetalVFXPlanarSortCacheEntry() {
    [entries release];
    [sortedIndices release];
  }
  MetalVFXPlanarSortCacheEntry(const MetalVFXPlanarSortCacheEntry&) = delete;
  MetalVFXPlanarSortCacheEntry& operator=(
      const MetalVFXPlanarSortCacheEntry&) = delete;
  MetalVFXPlanarSortCacheEntry(MetalVFXPlanarSortCacheEntry&&) = delete;
  MetalVFXPlanarSortCacheEntry& operator=(
      MetalVFXPlanarSortCacheEntry&&) = delete;
};

struct MetalVFXUpdateBuffers {
  id<MTLBuffer> resident = nil;
  NSUInteger residentCapacity = 0;
  uint64_t residentGeneration = 0;
  id<MTLBuffer> allocationState = nil;
  NSUInteger allocationStateCapacity = 0;
  id<MTLBuffer> residentDeadList = nil;
  NSUInteger residentDeadListCapacity = 0;
  uint64_t allocationStateGeneration = 0;
  id<MTLBuffer> aliveFlags = nil;
  id<MTLBuffer> aliveScratch = nil;
  id<MTLBuffer> aliveIndices = nil;
  id<MTLBuffer> aliveCount = nil;
  NSUInteger aliveFlagCapacity = 0;
  NSUInteger aliveScratchCapacity = 0;
  NSUInteger aliveIndexCapacity = 0;
  NSUInteger aliveCountCapacity = 0;
  uint64_t aliveCompactGeneration = 0;
  int32_t aliveCompactStrideWords = 0;
  int32_t aliveCompactCapacity = 0;
  int32_t aliveCompactOffsetWords = -1;
  MetalVFXPlanarSortCacheEntry
      planarSortCaches[kMetalVFXPlanarSortCacheCapacity];
  uint64_t planarSortUseSerial = 0;
  MetalVFXUpdateSlot slots[3];
  uint32_t nextSlot = 0;
  uint64_t dispatchCount = 0;
  uint64_t particleUploadCount = 0;
  uint64_t operationUploadCount = 0;
  uint64_t gpuCopyCount = 0;
  uint64_t completionCount = 0;
  uint64_t synchronousReadbackCount = 0;
  int32_t lastRingIndex = -1;
  int32_t lastBatchWidth = 0;
  int32_t peakBatchWidth = 0;
  uint64_t asyncBatchCount = 0;
  uint64_t boundsDispatchCount = 0;
  uint64_t boundsResidentHitCount = 0;
  uint64_t boundsParticleUploadCount = 0;
  uint64_t boundsCompletionCount = 0;
  uint64_t boundsResultCacheHitCount = 0;
  uint64_t boundsPendingDispatchCount = 0;
  uint64_t boundsPendingPublishCount = 0;
  uint64_t boundsPendingDiscardCount = 0;
  uint64_t deadPrefixPassCount = 0;
  uint64_t deadCompactionDispatchCount = 0;
  uint64_t residentOnlyPublishCount = 0;
  uint64_t deferredParticleReadbackCount = 0;
  uint64_t deferredParticleReadbackBytes = 0;
  uint64_t residentSnapshotCount = 0;
  uint64_t residentRestoreCount = 0;
  uint64_t residentSnapshotDiscardCount = 0;
  uint64_t asynchronousResidentPublishCount = 0;
  uint64_t asynchronousResidentCompletionCount = 0;
  uint64_t asynchronousResidentRollbackCount = 0;
  uint64_t completionWaitCount = 0;
  uint64_t cameraDependencyCount = 0;
  uint64_t preparationPollCount = 0;
  uint64_t preparationDeferredCount = 0;
  uint64_t preparationRetiredCount = 0;
  uint64_t allocationStateUploadCount = 0;
  uint64_t allocationStateGpuCopyCount = 0;
  uint64_t allocationStateResidentHitCount = 0;
  uint64_t residentInitializeCount = 0;
  uint64_t residentInitializeSpawnCount = 0;
  uint64_t residentInitializeReadbackAvoidedBytes = 0;
  uint64_t residentInitializeAllocationStateReadCount = 0;
  uint64_t allocationStateReadbackCount = 0;
  uint64_t deadListReadbackCount = 0;
  uint64_t metadataReadbackBytes = 0;
  uint64_t metadataReadbackGeneration = 0;
  uint64_t residentInitializeIndirectDispatchCount = 0;
  uint64_t residentInitializeIndirectPreparationCount = 0;
  uint64_t residentInitializeSourceStateGpuCopyCount = 0;
  uint64_t initializeCpuDispatchSizingCount = 0;
  uint64_t residentInitializeTargetCopyCount = 0;
  uint64_t residentInitializeTargetCopyBytes = 0;
  uint64_t residentInitializeAtomicPublishCount = 0;
  uint64_t asynchronousInitializeBeginCount = 0;
  uint64_t asynchronousInitializePollCount = 0;
  uint64_t asynchronousInitializeCompletionCount = 0;
  uint64_t asynchronousInitializeCancelCount = 0;
  uint64_t asynchronousInitializeResidentPublishCount = 0;
  uint64_t asynchronousInitializeResidentCompletionCount = 0;
  uint64_t asynchronousInitializeResidentRollbackCount = 0;
  std::deque<uint64_t> inFlightGenerations;
  std::deque<uint64_t> inFlightInitializeGenerations;
  std::vector<std::pair<uint64_t, MetalVFXResidentSnapshot>> residentSnapshots;
  bool boundsCacheValid = false;
  AnityGraphicsVFXBoundsReductionDesc boundsCacheDesc{};
  AnityGraphicsVFXBoundsReductionResult boundsCacheResult{};
};

static void ReleaseMetalVFXBuffer(id<MTLBuffer>* buffer) {
  if (!buffer || !*buffer) return;
  [*buffer release];
  *buffer = nil;
}

static void ReleaseMetalVFXResidentSnapshot(
    MetalVFXResidentSnapshot* snapshot) {
  if (!snapshot) return;
  ReleaseMetalVFXBuffer(&snapshot->buffer);
  ReleaseMetalVFXBuffer(&snapshot->allocationState);
  ReleaseMetalVFXBuffer(&snapshot->deadList);
}

static void ReleaseMetalVFXUpdateBuffers(MetalVFXUpdateBuffers* buffers) {
  if (!buffers) return;
  ReleaseMetalVFXBuffer(&buffers->resident);
  ReleaseMetalVFXBuffer(&buffers->allocationState);
  ReleaseMetalVFXBuffer(&buffers->residentDeadList);
  ReleaseMetalVFXBuffer(&buffers->aliveFlags);
  ReleaseMetalVFXBuffer(&buffers->aliveScratch);
  ReleaseMetalVFXBuffer(&buffers->aliveIndices);
  ReleaseMetalVFXBuffer(&buffers->aliveCount);
  for (MetalVFXUpdateSlot& slot : buffers->slots) {
    ReleaseMetalVFXBuffer(&slot.output);
    ReleaseMetalVFXBuffer(&slot.allocationState);
    ReleaseMetalVFXBuffer(&slot.residentDeadList);
    ReleaseMetalVFXBuffer(&slot.operations);
    ReleaseMetalVFXBuffer(&slot.deathPrefix);
    ReleaseMetalVFXBuffer(&slot.deathScratch);
    ReleaseMetalVFXBuffer(&slot.deadIndices);
    ReleaseMetalVFXBuffer(&slot.deadCount);
  }
  for (auto& snapshot : buffers->residentSnapshots)
    ReleaseMetalVFXResidentSnapshot(&snapshot.second);
  buffers->residentSnapshots.clear();
}

static void InvalidateMetalVFXPlanarSortCaches(
    MetalVFXUpdateBuffers& buffers) {
  for (MetalVFXPlanarSortCacheEntry& entry : buffers.planarSortCaches)
    entry.generation = 0;
}

static bool MatchesMetalVFXPlanarSortCache(
    const MetalVFXPlanarSortCacheEntry& entry, uint64_t generation,
    uint64_t cameraId, int32_t strideWords, int32_t particleCapacity,
    int32_t positionOffsetWords, uint32_t paddedLength,
    const float* localToWorld, const float* worldToClip) {
  return entry.generation == generation && entry.cameraId == cameraId &&
      entry.strideWords == strideWords &&
      entry.particleCapacity == particleCapacity &&
      entry.positionOffsetWords == positionOffsetWords &&
      entry.paddedLength == paddedLength && entry.entries &&
      entry.sortedIndices &&
      std::memcmp(entry.localToWorld, localToWorld,
                  sizeof(entry.localToWorld)) == 0 &&
      std::memcmp(entry.worldToClip, worldToClip,
                  sizeof(entry.worldToClip)) == 0;
}

static MetalVFXPlanarSortCacheEntry* FindMetalVFXPlanarSortCache(
    MetalVFXUpdateBuffers& buffers, uint64_t generation, uint64_t cameraId,
    int32_t strideWords, int32_t particleCapacity,
    int32_t positionOffsetWords, uint32_t paddedLength,
    const float* localToWorld, const float* worldToClip) {
  for (MetalVFXPlanarSortCacheEntry& entry : buffers.planarSortCaches) {
    if (MatchesMetalVFXPlanarSortCache(
            entry, generation, cameraId, strideWords, particleCapacity,
            positionOffsetWords, paddedLength, localToWorld, worldToClip))
      return &entry;
  }
  return nullptr;
}

static MetalVFXPlanarSortCacheEntry* AcquireMetalVFXPlanarSortCache(
    MetalVFXUpdateBuffers& buffers, bool* evicted) {
  if (evicted) *evicted = false;
  for (MetalVFXPlanarSortCacheEntry& entry : buffers.planarSortCaches) {
    if (entry.generation == 0) return &entry;
  }
  MetalVFXPlanarSortCacheEntry* oldest = &buffers.planarSortCaches[0];
  uint64_t oldestAge = buffers.planarSortUseSerial - oldest->lastUseSerial;
  for (int32_t index = 1; index < kMetalVFXPlanarSortCacheCapacity; ++index) {
    MetalVFXPlanarSortCacheEntry& candidate = buffers.planarSortCaches[index];
    const uint64_t age = buffers.planarSortUseSerial - candidate.lastUseSerial;
    if (age > oldestAge) {
      oldest = &candidate;
      oldestAge = age;
    }
  }
  if (evicted) *evicted = true;
  return oldest;
}

static int32_t CountMetalVFXPlanarSortCaches(
    const MetalVFXUpdateBuffers& buffers) {
  int32_t count = 0;
  for (const MetalVFXPlanarSortCacheEntry& entry : buffers.planarSortCaches)
    if (entry.generation != 0) ++count;
  return count;
}

struct MetalVFXRawBoundsResult {
  float minX, minY, minZ, maxX, maxY, maxZ;
  uint32_t validCount, invalid;
};
static_assert(sizeof(MetalVFXRawBoundsResult) == 32,
              "Metal VFX bounds reduction result layout changed");

struct MetalVFXBoundsMapParams {
  uint32_t strideWords, capacity;
  int32_t positionOffsetWords, aliveOffsetWords, sizeOffsetWords;
  int32_t scaleXOffsetWords, scaleYOffsetWords, scaleZOffsetWords;
  uint32_t expectedAliveCount, sequentialLimit;
};
static_assert(sizeof(MetalVFXBoundsMapParams) == 40,
              "Metal VFX bounds map parameters layout changed");

static bool SameVFXBoundsDesc(
    const AnityGraphicsVFXBoundsReductionDesc& a,
    const AnityGraphicsVFXBoundsReductionDesc& b) {
  return a.effectId == b.effectId &&
      a.particleSystemId == b.particleSystemId &&
      a.positionOffsetBytes == b.positionOffsetBytes &&
      a.aliveOffsetBytes == b.aliveOffsetBytes &&
      a.sizeOffsetBytes == b.sizeOffsetBytes &&
      a.scaleXOffsetBytes == b.scaleXOffsetBytes &&
      a.scaleYOffsetBytes == b.scaleYOffsetBytes &&
      a.scaleZOffsetBytes == b.scaleZOffsetBytes &&
      a.paddingX == b.paddingX && a.paddingY == b.paddingY &&
      a.paddingZ == b.paddingZ &&
      a.boundsInWorldSpace == b.boundsInWorldSpace;
}

struct MetalState {
  id<MTLDevice> device = nil;
  id<MTLCommandQueue> queue = nil;
  bool hdr = false;
  id<MTLBuffer> uiVertexBuffers[3] = { nil, nil, nil };
  id<MTLBuffer> uiIndexBuffers[3] = { nil, nil, nil };
  NSUInteger uiVertexCapacities[3] = { 0, 0, 0 };
  NSUInteger uiIndexCapacities[3] = { 0, 0, 0 };
  NSUInteger uiVertexLengths[3] = { 0, 0, 0 };
  NSUInteger uiIndexLengths[3] = { 0, 0, 0 };
  dispatch_semaphore_t uiSlotSemaphores[3] = { nullptr, nullptr, nullptr };
  bool uiSlotAcquired[3] = { false, false, false };
  std::atomic<int32_t> uiSlotSubmitted[3]{};
  id<MTLRenderPipelineState> uiPipelineBGRA8 = nil;
  id<MTLRenderPipelineState> uiPipelineRGBA16 = nil;
  id<MTLComputePipelineState> vfxInitializeCopyPipeline = nil;
  id<MTLComputePipelineState> vfxInitializeKernelPipeline = nil;
  id<MTLComputePipelineState> vfxInitializeIndirectPipeline = nil;
  id<MTLComputePipelineState> vfxUpdateKernelPipeline = nil;
  id<MTLComputePipelineState> vfxDeathPrefixPipeline = nil;
  id<MTLComputePipelineState> vfxDeathCompactPipeline = nil;
  id<MTLComputePipelineState> vfxDeathCommitPipeline = nil;
  id<MTLComputePipelineState> vfxPlanarAliveMapPipeline = nil;
  id<MTLComputePipelineState> vfxPlanarIndirectPipeline = nil;
  id<MTLComputePipelineState> vfxPlanarSortMapPipeline = nil;
  id<MTLComputePipelineState> vfxPlanarSortStagePipeline = nil;
  id<MTLComputePipelineState> vfxPlanarSortExtractPipeline = nil;
  id<MTLComputePipelineState> vfxBoundsMapPipeline = nil;
  id<MTLComputePipelineState> vfxBoundsReducePipeline = nil;
  id<MTLLibrary> vfxPlanarLibrary = nil;
  id<MTLRenderPipelineState> vfxPlanarPipelinesBGRA8[4] = { nil, nil, nil, nil };
  id<MTLRenderPipelineState> vfxPlanarPipelinesRGBA16[4] = { nil, nil, nil, nil };
  id<MTLDepthStencilState> vfxPlanarDepthStates[7][2] = {};
  std::mutex vfxComputeMutex;
  std::unordered_map<MetalVFXParticleKey, MetalVFXUpdateBuffers,
      MetalVFXParticleKeyHash> vfxUpdateBuffers;
  int32_t failNextVFXInitializeCount = 0;
  int32_t failNextVFXUpdateCount = 0;
  int32_t failNextVFXPlanarCount = 0;
  int32_t failNextVFXDeviceRemovalCount = 0;
  std::atomic<int32_t> deviceLost{0};
  std::mutex vfxPlanarSubmissionMutex;
  std::condition_variable vfxPlanarSubmissionCondition;
  AnityGraphicsVFXPlanarSubmissionStats vfxPlanarSubmissionStats{};
  std::deque<std::pair<uint64_t, bool>> vfxPlanarSubmissionResults;
  uint64_t vfxPlanarLastEvictedSubmissionId = 0;
  std::mutex textureMutex;
  std::unordered_map<uint64_t, MetalTextureEntry> textures;
  id<MTLTexture> whiteTexture = nil;
  id<MTLSamplerState> defaultSampler = nil;
};

static bool IsMetalDeviceLost(const MetalState* st) {
  return st && st->deviceLost.load(std::memory_order_acquire) != 0;
}

static bool IsTerminalMetalDeviceError(NSError* error) {
  if (!error || ![error.domain isEqualToString:MTLCommandBufferErrorDomain])
    return false;
  return error.code == MTLCommandBufferErrorDeviceRemoved ||
      error.code == MTLCommandBufferErrorAccessRevoked;
}

static void ObserveMetalDeviceLoss(
    MetalState* st, id<MTLCommandBuffer> commandBuffer,
    bool injectedDeviceRemoval = false) {
  if (!st) return;
  if (injectedDeviceRemoval ||
      (commandBuffer && IsTerminalMetalDeviceError(commandBuffer.error)))
    st->deviceLost.store(1, std::memory_order_release);
}

static AnityResult WaitForMetalVFXPlanarSubmissions(
    MetalState* st, uint64_t throughSubmissionId,
    int32_t timeoutMilliseconds, bool countWait) {
  if (!st || timeoutMilliseconds < -1) return ANITY_ERR_INVALID_ARG;
  std::unique_lock<std::mutex> lock(st->vfxPlanarSubmissionMutex);
  const uint64_t target = throughSubmissionId == 0
      ? st->vfxPlanarSubmissionStats.lastSubmittedId
      : throughSubmissionId;
  if (target > st->vfxPlanarSubmissionStats.lastSubmittedId)
    return ANITY_ERR_INVALID_ARG;
  if (countWait) ++st->vfxPlanarSubmissionStats.waitCount;
  const auto completed = [&] {
    return st->vfxPlanarSubmissionStats.lastCompletedId >= target;
  };
  bool ready = completed();
  if (!ready && timeoutMilliseconds < 0) {
    st->vfxPlanarSubmissionCondition.wait(lock, completed);
    ready = true;
  } else if (!ready && timeoutMilliseconds > 0) {
    ready = st->vfxPlanarSubmissionCondition.wait_for(
        lock, std::chrono::milliseconds(timeoutMilliseconds), completed);
  }
  if (!ready) return ANITY_ERR_TIMEOUT;
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
  /* Exact per-fence success/failure is bounded. Never turn an evicted failure
   * into success merely because the aggregate completion watermark advanced. */
  if (target != 0 && target <= st->vfxPlanarLastEvictedSubmissionId)
    return ANITY_ERR_INVALID_ARG;
  const auto result = std::find_if(
      st->vfxPlanarSubmissionResults.begin(),
      st->vfxPlanarSubmissionResults.end(),
      [&](const auto& item) { return item.first == target; });
  if (result != st->vfxPlanarSubmissionResults.end() && result->second)
    return ANITY_ERR_DEVICE_LOST;
  return ANITY_OK;
}

struct MetalVFXPlanarParams {
  int32_t strideWords;
  uint32_t capacity;
  int32_t primitiveType;
  int32_t aliveOffsetWords;
  int32_t positionOffsetWords;
  int32_t colorOffsetWords;
  int32_t alphaOffsetWords;
  int32_t axisXOffsetWords;
  int32_t axisYOffsetWords;
  int32_t axisZOffsetWords;
  int32_t angleXOffsetWords;
  int32_t angleYOffsetWords;
  int32_t angleZOffsetWords;
  int32_t pivotXOffsetWords;
  int32_t pivotYOffsetWords;
  int32_t pivotZOffsetWords;
  int32_t sizeOffsetWords;
  int32_t scaleXOffsetWords;
  int32_t scaleYOffsetWords;
  int32_t scaleZOffsetWords;
  float localToWorld[16];
  float worldToClip[16];
};
static_assert(sizeof(MetalVFXPlanarParams) == 208,
              "Metal VFX Planar parameter ABI changed");

struct MetalVFXPlanarSortMapParams {
  uint32_t strideWords;
  uint32_t capacity;
  int32_t positionOffsetWords;
  uint32_t paddedLength;
  float localToWorld[16];
  float worldToClip[16];
};
static_assert(sizeof(MetalVFXPlanarSortMapParams) == 144,
              "Metal VFX Planar sort-map parameter ABI changed");

struct MetalVFXPlanarSortStageParams {
  uint32_t compareDistance;
  uint32_t sequenceLength;
  uint32_t paddedLength;
};
static_assert(sizeof(MetalVFXPlanarSortStageParams) == 12,
              "Metal VFX Planar sort-stage parameter ABI changed");

static bool TryVFXPlanarPaddedLength(
    int32_t capacity, uint32_t* outPaddedLength) {
  if (!outPaddedLength || capacity <= 0) return false;
  uint32_t value = static_cast<uint32_t>(capacity);
  if (value > (uint32_t{1} << 26)) return false;
  --value;
  value |= value >> 1;
  value |= value >> 2;
  value |= value >> 4;
  value |= value >> 8;
  value |= value >> 16;
  *outPaddedLength = value + 1;
  return *outPaddedLength != 0;
}

static NSString* const MetalVFXPlanarSource = @R"metal(
#include <metal_stdlib>
using namespace metal;

struct PlanarParams {
  int strideWords;
  uint capacity;
  int primitiveType;
  int aliveOffsetWords;
  int positionOffsetWords;
  int colorOffsetWords;
  int alphaOffsetWords;
  int axisXOffsetWords;
  int axisYOffsetWords;
  int axisZOffsetWords;
  int angleXOffsetWords;
  int angleYOffsetWords;
  int angleZOffsetWords;
  int pivotXOffsetWords;
  int pivotYOffsetWords;
  int pivotZOffsetWords;
  int sizeOffsetWords;
  int scaleXOffsetWords;
  int scaleYOffsetWords;
  int scaleZOffsetWords;
  float localToWorld[16];
  float worldToClip[16];
};

struct PlanarVertexOut {
  float4 position [[position]];
  float4 color;
};

static float loadFloat(device const uint* records, uint base, int offset) {
  return as_type<float>(records[base + uint(offset)]);
}

static float3 loadFloat3(device const uint* records, uint base, int offset) {
  return float3(loadFloat(records, base, offset + 0),
                loadFloat(records, base, offset + 1),
                loadFloat(records, base, offset + 2));
}

static float4 mulRowMajor(constant float* matrix, float4 value) {
  return float4(dot(float4(matrix[0], matrix[1], matrix[2], matrix[3]), value),
                dot(float4(matrix[4], matrix[5], matrix[6], matrix[7]), value),
                dot(float4(matrix[8], matrix[9], matrix[10], matrix[11]), value),
                dot(float4(matrix[12], matrix[13], matrix[14], matrix[15]), value));
}

static float3 rotateEuler(float3 value, float3 degrees) {
  float3 angles = degrees * (M_PI_F / 180.0f);
  float3 s = sin(angles);
  float3 c = cos(angles);
  float3x3 matrix = float3x3(
      float3(c.y * c.z + s.x * s.y * s.z, c.x * s.z,
             -c.z * s.y + c.y * s.x * s.z),
      float3(c.z * s.x * s.y - c.y * s.z, c.x * c.z,
             c.y * c.z * s.x + s.y * s.z),
      float3(c.x * s.y, -s.x, c.x * c.y));
  return matrix * value;
}

static uint indexCount(int primitiveType) {
  return primitiveType == 0 ? 3u : (primitiveType == 1 ? 6u : 18u);
}

static uint localVertex(int primitiveType, uint index) {
  if (primitiveType == 0) return index;
  if (primitiveType == 1) {
    const uint pattern[6] = {0u, 2u, 1u, 1u, 2u, 3u};
    return pattern[index];
  }
  const uint pattern[18] = {0u, 1u, 2u, 0u, 2u, 3u, 0u, 3u, 4u,
                            0u, 4u, 5u, 0u, 5u, 6u, 0u, 6u, 7u};
  return pattern[index];
}

static float2 planarOffset(int primitiveType, uint localIndex) {
  if (primitiveType == 0) {
    const float2 offsets[3] = {
      float2(-0.5, -0.288675129413604736328125),
      float2(0.0, 0.57735025882720947265625),
      float2(0.5, -0.288675129413604736328125)};
    return offsets[localIndex];
  }
  if (primitiveType == 1)
    return float2(float(localIndex & 1u), float(localIndex & 2u) * 0.5) - 0.5;
  const float2 offsets[8] = {
    float2(-0.5, 0.0), float2(-0.5, 0.5),
    float2(0.0, 0.5), float2(0.5, 0.5),
    float2(0.5, 0.0), float2(0.5, -0.5),
    float2(0.0, -0.5), float2(-0.5, -0.5)};
  const float crop = (localIndex & 1u) != 0u ? 0.707 : 1.0;
  return offsets[localIndex] * crop;
}

vertex PlanarVertexOut anityVfxPlanarVertex(
    uint vertexId [[vertex_id]],
    device const uint* records [[buffer(0)]],
    constant PlanarParams& params [[buffer(1)]],
    device const uint* aliveIndices [[buffer(2)]]) {
  PlanarVertexOut output;
  const uint perParticle = indexCount(params.primitiveType);
  const uint particleOrdinal = vertexId / perParticle;
  const uint particle = aliveIndices[particleOrdinal];
  if (particle >= params.capacity) {
    output.position = float4(INFINITY, INFINITY, INFINITY, 1.0);
    output.color = 0.0;
    return output;
  }
  const uint base = particle * uint(params.strideWords);
  if (records[base + uint(params.aliveOffsetWords)] == 0u) {
    output.position = float4(INFINITY, INFINITY, INFINITY, 1.0);
    output.color = 0.0;
    return output;
  }
  const uint local = localVertex(params.primitiveType, vertexId % perParticle);
  const float2 offset = planarOffset(params.primitiveType, local);
  const float size = loadFloat(records, base, params.sizeOffsetWords);
  const float3 scale = size * float3(
      loadFloat(records, base, params.scaleXOffsetWords),
      loadFloat(records, base, params.scaleYOffsetWords),
      loadFloat(records, base, params.scaleZOffsetWords));
  const float3 pivot = float3(
      loadFloat(records, base, params.pivotXOffsetWords),
      loadFloat(records, base, params.pivotYOffsetWords),
      loadFloat(records, base, params.pivotZOffsetWords));
  const float3 angles = float3(
      loadFloat(records, base, params.angleXOffsetWords),
      loadFloat(records, base, params.angleYOffsetWords),
      loadFloat(records, base, params.angleZOffsetWords));
  const float3 axisX = loadFloat3(records, base, params.axisXOffsetWords);
  const float3 axisY = loadFloat3(records, base, params.axisYOffsetWords);
  const float3 axisZ = loadFloat3(records, base, params.axisZOffsetWords);
  float3 element = (float3(offset, 0.0) - pivot) * scale;
  element = rotateEuler(element, angles);
  element = axisX * element.x + axisY * element.y + axisZ * element.z;
  const float3 position = loadFloat3(records, base, params.positionOffsetWords) + element;
  const float4 world = mulRowMajor(params.localToWorld, float4(position, 1.0));
  output.position = mulRowMajor(params.worldToClip, world);
  output.color = float4(loadFloat3(records, base, params.colorOffsetWords),
                        loadFloat(records, base, params.alphaOffsetWords));
  return output;
}

fragment float4 anityVfxPlanarFragment(PlanarVertexOut input [[stage_in]]) {
  float4 color = input.color;
  color.a = saturate(color.a);
  return color;
}
)metal";

static MTLSamplerAddressMode ToMetalAddressMode(int32_t mode) {
  switch (mode) {
    case 0: return MTLSamplerAddressModeRepeat;
    case 2: return MTLSamplerAddressModeMirrorRepeat;
    default: return MTLSamplerAddressModeClampToEdge;
  }
}

static id<MTLSamplerState> CreateSampler(
    MetalState* st, const AnityGraphicsTextureDesc& desc) {
  MTLSamplerDescriptor* sampler = [[MTLSamplerDescriptor alloc] init];
  const bool point = desc.filterMode == 0;
  sampler.minFilter = point ? MTLSamplerMinMagFilterNearest : MTLSamplerMinMagFilterLinear;
  sampler.magFilter = point ? MTLSamplerMinMagFilterNearest : MTLSamplerMinMagFilterLinear;
  sampler.mipFilter = desc.mipCount <= 1 ? MTLSamplerMipFilterNotMipmapped
      : (desc.filterMode == 2 ? MTLSamplerMipFilterLinear : MTLSamplerMipFilterNearest);
  sampler.lodMinClamp = 0.0f;
  sampler.lodMaxClamp = static_cast<float>(std::max(0, desc.mipCount - 1));
  sampler.sAddressMode = ToMetalAddressMode(desc.wrapU);
  sampler.tAddressMode = ToMetalAddressMode(desc.wrapV);
  return [st->device newSamplerStateWithDescriptor:sampler];
}

static bool EnsureWhiteTexture(MetalState* st) {
  if (st->whiteTexture && st->defaultSampler) return true;
  MTLTextureDescriptor* descriptor = [MTLTextureDescriptor
      texture2DDescriptorWithPixelFormat:MTLPixelFormatRGBA8Unorm
      width:1 height:1 mipmapped:NO];
  descriptor.usage = MTLTextureUsageShaderRead;
  descriptor.storageMode = MTLStorageModeShared;
  st->whiteTexture = [st->device newTextureWithDescriptor:descriptor];
  AnityGraphicsTextureDesc sampling{};
  sampling.filterMode = 1;
  sampling.wrapU = 1;
  sampling.wrapV = 1;
  st->defaultSampler = CreateSampler(st, sampling);
  if (!st->whiteTexture || !st->defaultSampler) return false;
  const uint8_t white[4] = {255, 255, 255, 255};
  [st->whiteTexture replaceRegion:MTLRegionMake2D(0, 0, 1, 1)
      mipmapLevel:0 withBytes:white bytesPerRow:4];
  return true;
}

static id<MTLRenderPipelineState> GetVFXPlanarPipeline(
    MetalState* st, MTLPixelFormat pixelFormat, int32_t blendMode) {
  if (!st || blendMode < 0 || blendMode > 3) return nil;
  id<MTLRenderPipelineState>* pipelines = nullptr;
  if (pixelFormat == MTLPixelFormatBGRA8Unorm)
    pipelines = st->vfxPlanarPipelinesBGRA8;
  else if (pixelFormat == MTLPixelFormatRGBA16Float)
    pipelines = st->vfxPlanarPipelinesRGBA16;
  else
    return nil;
  if (pipelines[blendMode]) return pipelines[blendMode];
  if (!st->vfxPlanarLibrary) {
    NSError* libraryError = nil;
    st->vfxPlanarLibrary = [st->device
        newLibraryWithSource:MetalVFXPlanarSource
        options:nil error:&libraryError];
    if (!st->vfxPlanarLibrary || libraryError) {
      NSLog(@"Anity VFX Planar Metal library compilation failed: %@", libraryError);
      std::fprintf(stderr, "Anity VFX Planar Metal library compilation failed: %s\n",
                   libraryError.localizedDescription.UTF8String);
      return nil;
    }
  }
  id<MTLFunction> vertex = [st->vfxPlanarLibrary
      newFunctionWithName:@"anityVfxPlanarVertex"];
  id<MTLFunction> fragment = [st->vfxPlanarLibrary
      newFunctionWithName:@"anityVfxPlanarFragment"];
  if (!vertex || !fragment) return nil;
  MTLRenderPipelineDescriptor* descriptor =
      [[MTLRenderPipelineDescriptor alloc] init];
  descriptor.label = @"Anity VFX Planar Output";
  descriptor.vertexFunction = vertex;
  descriptor.fragmentFunction = fragment;
  descriptor.depthAttachmentPixelFormat = MTLPixelFormatDepth32Float;
  MTLRenderPipelineColorAttachmentDescriptor* color =
      descriptor.colorAttachments[0];
  color.pixelFormat = pixelFormat;
  color.writeMask = MTLColorWriteMaskAll;
  color.blendingEnabled = blendMode != 3;
  if (color.blendingEnabled) {
    color.sourceRGBBlendFactor = blendMode == 2
        ? MTLBlendFactorOne : MTLBlendFactorSourceAlpha;
    color.destinationRGBBlendFactor = blendMode == 0
        ? MTLBlendFactorOne : MTLBlendFactorOneMinusSourceAlpha;
    color.rgbBlendOperation = MTLBlendOperationAdd;
    color.sourceAlphaBlendFactor = blendMode == 2
        ? MTLBlendFactorOne : MTLBlendFactorSourceAlpha;
    color.destinationAlphaBlendFactor = blendMode == 0
        ? MTLBlendFactorOne : MTLBlendFactorOneMinusSourceAlpha;
    color.alphaBlendOperation = MTLBlendOperationAdd;
  }
  NSError* pipelineError = nil;
  pipelines[blendMode] = [st->device
      newRenderPipelineStateWithDescriptor:descriptor error:&pipelineError];
  if (!pipelines[blendMode] || pipelineError) {
    NSLog(@"Anity VFX Planar Metal pipeline creation failed: %@", pipelineError);
    std::fprintf(stderr, "Anity VFX Planar Metal pipeline creation failed: %s\n",
                 pipelineError.localizedDescription.UTF8String);
    return nil;
  }
  return pipelines[blendMode];
}

static MTLCompareFunction ToVFXPlanarCompareFunction(int32_t zTest) {
  switch (zTest) {
    case 0: return MTLCompareFunctionLess;
    case 1: return MTLCompareFunctionGreater;
    case 2: return MTLCompareFunctionLessEqual;
    case 3: return MTLCompareFunctionGreaterEqual;
    case 4: return MTLCompareFunctionEqual;
    case 5: return MTLCompareFunctionNotEqual;
    case 6: return MTLCompareFunctionAlways;
    default: return MTLCompareFunctionNever;
  }
}

static id<MTLDepthStencilState> GetVFXPlanarDepthState(
    MetalState* st, int32_t zTest, int32_t zWrite) {
  if (!st || !st->device || zTest < 0 || zTest > 6 ||
      zWrite < 0 || zWrite > 1)
    return nil;
  id<MTLDepthStencilState>& cached = st->vfxPlanarDepthStates[zTest][zWrite];
  if (cached) return cached;
  MTLDepthStencilDescriptor* descriptor =
      [[MTLDepthStencilDescriptor alloc] init];
  descriptor.label = @"Anity VFX Planar Depth";
  descriptor.depthCompareFunction = ToVFXPlanarCompareFunction(zTest);
  descriptor.depthWriteEnabled = zWrite != 0;
  cached = [st->device newDepthStencilStateWithDescriptor:descriptor];
  return cached;
}

static void ReleaseUISlot(MetalState* st, int32_t ringIndex) {
  if (!st || ringIndex < 0 || ringIndex >= 3 || !st->uiSlotAcquired[ringIndex]) return;
  st->uiSlotSubmitted[ringIndex].store(0, std::memory_order_release);
  dispatch_semaphore_signal(st->uiSlotSemaphores[ringIndex]);
  st->uiSlotAcquired[ringIndex] = false;
}

static id<MTLRenderPipelineState> CreateUIPipeline(
    MetalState* st, MTLPixelFormat pixelFormat) {
  if (!st || !st->device) return nil;
  NSString* source = @"#include <metal_stdlib>\n"
      "using namespace metal;\n"
      "struct PackedVertex {\n"
      "  float px; float py; float pz;\n"
      "  uchar r; uchar g; uchar b; uchar a;\n"
      "  float u0x; float u0y; float u0z; float u0w;\n"
      "  float u1x; float u1y; float u1z; float u1w;\n"
      "  float u2x; float u2y; float u2z; float u2w;\n"
      "  float u3x; float u3y; float u3z; float u3w;\n"
      "  float nx; float ny; float nz;\n"
      "  float tx; float ty; float tz; float tw;\n"
      "};\n"
      "struct ViewportUniform { float width; float height; };\n"
      "struct VertexOut { float4 position [[position]]; float4 color; float2 uv; };\n"
      "vertex VertexOut anity_ui_vertex(const device PackedVertex* vertices [[buffer(0)]],\n"
      "    constant ViewportUniform& viewport [[buffer(1)]], uint vertexId [[vertex_id]]) {\n"
      "  PackedVertex v = vertices[vertexId];\n"
      "  float2 ndc = float2((v.px / viewport.width) * 2.0 - 1.0,\n"
      "      1.0 - (v.py / viewport.height) * 2.0);\n"
      "  VertexOut output; output.position = float4(ndc, 0.0, 1.0);\n"
      "  output.color = float4(float(v.r), float(v.g), float(v.b), float(v.a)) / 255.0;\n"
      "  output.uv = float2(v.u0x, v.u0y);\n"
      "  return output;\n"
      "}\n"
      "fragment float4 anity_ui_fragment(VertexOut input [[stage_in]],\n"
      "    texture2d<float> mainTexture [[texture(0)]],\n"
      "    texture2d<float> alphaTexture [[texture(1)]],\n"
      "    sampler mainSampler [[sampler(0)]], sampler alphaSampler [[sampler(1)]],\n"
      "    constant uint& textureFlags [[buffer(0)]]) {\n"
      "  float4 output = input.color * mainTexture.sample(mainSampler, input.uv);\n"
      "  if ((textureFlags & 2u) != 0u) output.a *= alphaTexture.sample(alphaSampler, input.uv).r;\n"
      "  return output;\n"
      "}\n";
  NSError* error = nil;
  id<MTLLibrary> library = [st->device newLibraryWithSource:source options:nil error:&error];
  if (!library) return nil;
  id<MTLFunction> vertex = [library newFunctionWithName:@"anity_ui_vertex"];
  id<MTLFunction> fragment = [library newFunctionWithName:@"anity_ui_fragment"];
  if (!vertex || !fragment) return nil;
  MTLRenderPipelineDescriptor* descriptor = [[MTLRenderPipelineDescriptor alloc] init];
  descriptor.label = @"Anity UI Pipeline";
  descriptor.vertexFunction = vertex;
  descriptor.fragmentFunction = fragment;
  descriptor.colorAttachments[0].pixelFormat = pixelFormat;
  descriptor.colorAttachments[0].blendingEnabled = YES;
  descriptor.colorAttachments[0].rgbBlendOperation = MTLBlendOperationAdd;
  descriptor.colorAttachments[0].alphaBlendOperation = MTLBlendOperationAdd;
  descriptor.colorAttachments[0].sourceRGBBlendFactor = MTLBlendFactorSourceAlpha;
  descriptor.colorAttachments[0].destinationRGBBlendFactor = MTLBlendFactorOneMinusSourceAlpha;
  descriptor.colorAttachments[0].sourceAlphaBlendFactor = MTLBlendFactorOne;
  descriptor.colorAttachments[0].destinationAlphaBlendFactor = MTLBlendFactorOneMinusSourceAlpha;
  return [st->device newRenderPipelineStateWithDescriptor:descriptor error:&error];
}

static id<MTLRenderPipelineState> GetUIPipeline(
    MetalState* st, MTLPixelFormat pixelFormat) {
  id<MTLRenderPipelineState>* pipeline = pixelFormat == MTLPixelFormatRGBA16Float
      ? &st->uiPipelineRGBA16 : &st->uiPipelineBGRA8;
  if (!*pipeline) *pipeline = CreateUIPipeline(st, pixelFormat);
  return *pipeline;
}

static id<MTLComputePipelineState> GetVFXInitializeCopyPipeline(MetalState* st) {
  if (!st || !st->device) return nil;
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  if (st->vfxInitializeCopyPipeline) return st->vfxInitializeCopyPipeline;
  NSString* source = @"#include <metal_stdlib>\n"
      "using namespace metal;\n"
      "struct Params { uint startEventIndex; uint recordCount; uint strideWords; };\n"
      "kernel void anity_vfx_initialize_copy(\n"
      "    const device uint* source [[buffer(0)]],\n"
      "    device uint* output [[buffer(1)]],\n"
      "    constant Params& params [[buffer(2)]],\n"
      "    uint tid [[thread_position_in_grid]]) {\n"
      "  uint totalWords = params.recordCount * params.strideWords;\n"
      "  if (tid >= totalWords) return;\n"
      "  uint record = tid / params.strideWords;\n"
      "  uint word = tid - record * params.strideWords;\n"
      "  output[tid] = source[(params.startEventIndex + record) * params.strideWords + word];\n"
      "}\n";
  NSError* error = nil;
  id<MTLLibrary> library = [st->device newLibraryWithSource:source options:nil error:&error];
  if (!library) return nil;
  id<MTLFunction> function = [library newFunctionWithName:@"anity_vfx_initialize_copy"];
  if (!function) return nil;
  st->vfxInitializeCopyPipeline =
      [st->device newComputePipelineStateWithFunction:function error:&error];
  return st->vfxInitializeCopyPipeline;
}

static id<MTLComputePipelineState> GetVFXInitializeKernelPipeline(MetalState* st) {
  if (!st || !st->device) return nil;
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  if (st->vfxInitializeKernelPipeline && st->vfxInitializeIndirectPipeline)
    return st->vfxInitializeKernelPipeline;
  std::lock_guard<std::mutex> compilationLock(
      gMetalVFXPipelineCompilationMutex);
  NSString* source = @"#include <metal_stdlib>\n"
      "using namespace metal;\n"
      "struct AttributeDesc { int offsetBytes; int componentCount; int valueType; int semantic; uint defaults[4]; };\n"
      "struct OperationDesc { int targetOffsetBytes; int sourceOffsetBytes; int componentCount; int valueType; int valueSource; int composition; int randomMode; int reserved; uint valueA[4]; uint valueB[4]; uint blendFactorBits; };\n"
      "struct Params { uint startEventIndex; uint recordCount; uint sourceStrideWords; uint attributeStrideWords; uint capacity; uint flags; uint deadCountSnapshot; uint nextSequentialIndex; uint attributeCount; uint operationCount; uint systemSeed; uint sourceEventCount; uint spawnCandidateCount; uint residentAllocationState; };\n"
      "kernel void anity_vfx_initialize_indirect(const device uint* sourceState [[buffer(0)]], device uint* arguments [[buffer(1)]], constant uint4& config [[buffer(2)]], uint tid [[thread_position_in_grid]]) { if (tid != 0u) return; uint available = (config.y & 1u) != 0u ? sourceState[1] : config.x - min(config.x, sourceState[2]); uint threads = min(config.z, available); arguments[0] = max(1u, (threads + config.w - 1u) / config.w); arguments[1] = 1u; arguments[2] = 1u; }\n"
      "uint vfx_hash(uint value) { value = (value ^ 61u) ^ (value >> 16); value += value << 3; value ^= value >> 4; value *= 0x27d4eb2du; value ^= value >> 15; return value; }\n"
      "float vfx_random(thread uint& state) { state = vfx_hash(state); return float(state & 0x00ffffffu) * (1.0 / 16777216.0); }\n"
      "uint vfx_compose(int type, int composition, uint current, uint value, float blend) {\n"
      "  if (composition == 0) return value;\n"
      "  if (type >= 4) { float a = as_type<float>(current); float b = as_type<float>(value); if (composition == 1) return as_type<uint>(a + b); if (composition == 2) return as_type<uint>(a * b); return as_type<uint>(a + (b - a) * blend); }\n"
      "  if (type == 2) return composition == 1 ? current + value : current * value;\n"
      "  int a = as_type<int>(current); int b = as_type<int>(value); return as_type<uint>(composition == 1 ? a + b : a * b);\n"
      "}\n"
      "uint vfx_find_source(uint spawnIndex, const device uint* prefix, uint count) { uint low = 0u; uint high = count; while (low < high) { uint middle = low + ((high - low) >> 1u); if (spawnIndex < prefix[middle]) high = middle; else low = middle + 1u; } return min(low, count - 1u); }\n"
      "kernel void anity_vfx_initialize_kernel(\n"
      "    const device uint* source [[buffer(0)]], device atomic_uint* particles [[buffer(1)]],\n"
      "    device uint* deadList [[buffer(2)]], const device AttributeDesc* attributes [[buffer(3)]],\n"
      "    const device OperationDesc* operations [[buffer(4)]], device atomic_uint* counters [[buffer(5)]],\n"
      "    constant Params& params [[buffer(6)]], const device uint* spawnPrefix [[buffer(7)]], const device uint* sourceState [[buffer(8)]], uint tid [[thread_position_in_grid]]) {\n"
      "  uint deadCountSnapshot = params.residentAllocationState != 0u ? sourceState[1] : params.deadCountSnapshot; uint nextSequentialIndex = params.residentAllocationState != 0u ? sourceState[2] : params.nextSequentialIndex;\n"
      "  uint available = (params.flags & 1u) != 0u ? deadCountSnapshot : params.capacity - min(params.capacity, nextSequentialIndex);\n"
      "  uint threadCount = min(params.spawnCandidateCount, available); if (tid >= threadCount) return;\n"
      "  if (tid == 0u) atomic_store_explicit(&counters[2], nextSequentialIndex + threadCount, memory_order_relaxed);\n"
      "  uint sourceIndex = params.sourceEventCount == 0u ? tid : vfx_find_source(tid, spawnPrefix, params.sourceEventCount);\n"
      "  uint logicalIndex = nextSequentialIndex + tid; uint randomState = vfx_hash(logicalIndex ^ params.systemSeed);\n"
      "  bool alive = true; bool hasAlive = false; int aliveOffset = -1;\n"
      "  for (uint i = 0; i < params.attributeCount; ++i) if (attributes[i].semantic == 1) { hasAlive = true; aliveOffset = attributes[i].offsetBytes; alive = attributes[i].defaults[0] != 0u; }\n"
      "  for (uint i = 0; i < params.operationCount; ++i) { const device OperationDesc& op = operations[i]; if (op.targetOffsetBytes != aliveOffset) continue; uint value = 0u; if (op.valueSource == 0) value = op.valueA[0]; else if (op.valueSource == 1) value = source[(params.startEventIndex + sourceIndex) * params.sourceStrideWords + uint(op.sourceOffsetBytes / 4)]; else if (op.valueSource == 2) value = logicalIndex; else if (op.valueSource == 3) value = vfx_hash(logicalIndex ^ params.systemSeed); else value = tid; alive = vfx_compose(op.valueType, op.composition, alive ? 1u : 0u, value, as_type<float>(op.blendFactorBits)) != 0u; }\n"
      "  if (hasAlive && !alive) return;\n"
      "  uint outputIndex = logicalIndex; if ((params.flags & 1u) != 0u) { uint previous = atomic_fetch_sub_explicit(&counters[1], 1u, memory_order_relaxed); outputIndex = deadList[previous - 1u]; }\n"
      "  uint baseWord = outputIndex * params.attributeStrideWords;\n"
      "  for (uint i = 0; i < params.attributeCount; ++i) { const device AttributeDesc& attr = attributes[i]; for (int c = 0; c < attr.componentCount; ++c) atomic_store_explicit(&particles[baseWord + uint(attr.offsetBytes / 4 + c)], attr.defaults[c], memory_order_relaxed); }\n"
      "  randomState = vfx_hash(logicalIndex ^ params.systemSeed);\n"
      "  for (uint i = 0; i < params.operationCount; ++i) { const device OperationDesc& op = operations[i]; float uniformRandom = op.randomMode == 2 ? vfx_random(randomState) : 0.0; for (int c = 0; c < op.componentCount; ++c) { uint value = 0u; if (op.valueSource == 0) { value = op.valueA[c]; if (op.randomMode != 0) { float t = op.randomMode == 2 ? uniformRandom : vfx_random(randomState); float a = as_type<float>(op.valueA[c]); float b = as_type<float>(op.valueB[c]); value = as_type<uint>(a + (b - a) * t); } } else if (op.valueSource == 1) value = source[(params.startEventIndex + sourceIndex) * params.sourceStrideWords + uint(op.sourceOffsetBytes / 4 + c)]; else if (op.valueSource == 2) value = logicalIndex; else if (op.valueSource == 3) value = vfx_hash(logicalIndex ^ params.systemSeed); else value = tid; device atomic_uint* target = &particles[baseWord + uint(op.targetOffsetBytes / 4 + c)]; uint current = atomic_load_explicit(target, memory_order_relaxed); atomic_store_explicit(target, vfx_compose(op.valueType, op.composition, current, value, as_type<float>(op.blendFactorBits)), memory_order_relaxed); } }\n"
      "  atomic_fetch_add_explicit(&counters[0], 1u, memory_order_relaxed);\n"
      "}\n";
  NSError* error = nil;
  id<MTLLibrary> library = [st->device newLibraryWithSource:source options:nil error:&error];
  if (!library) return nil;
  id<MTLFunction> function = [library newFunctionWithName:@"anity_vfx_initialize_kernel"];
  id<MTLFunction> indirectFunction =
      [library newFunctionWithName:@"anity_vfx_initialize_indirect"];
  if (!function || !indirectFunction) {
    [function release];
    [indirectFunction release];
    [library release];
    return nil;
  }
  id<MTLComputePipelineState> kernelPipeline =
      [st->device newComputePipelineStateWithFunction:function error:&error];
  id<MTLComputePipelineState> indirectPipeline =
      [st->device newComputePipelineStateWithFunction:indirectFunction
          error:&error];
  [function release];
  [indirectFunction release];
  [library release];
  if (!kernelPipeline || !indirectPipeline) {
    [kernelPipeline release];
    [indirectPipeline release];
    return nil;
  }
  [st->vfxInitializeKernelPipeline release];
  [st->vfxInitializeIndirectPipeline release];
  st->vfxInitializeKernelPipeline = kernelPipeline;
  st->vfxInitializeIndirectPipeline = indirectPipeline;
  return st->vfxInitializeKernelPipeline;
}

static id<MTLComputePipelineState> GetVFXUpdateKernelPipeline(MetalState* st) {
  if (!st || !st->device) return nil;
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  if (st->vfxUpdateKernelPipeline) return st->vfxUpdateKernelPipeline;
  std::lock_guard<std::mutex> compilationLock(
      gMetalVFXPipelineCompilationMutex);
  const char* sourceText = R"METAL(
#include <metal_stdlib>
using namespace metal;

struct OperationDesc {
  int kind;
  int targetOffsetBytes;
  int sourceAOffsetBytes;
  int sourceBOffsetBytes;
  int auxiliaryOffset0Bytes;
  int auxiliaryOffset1Bytes;
  int componentCount;
  int valueType;
  int composition;
  int randomMode;
  int flags;
  uint valueA[4];
  uint valueB[4];
  uint blendFactorBits;
};

struct Params {
  uint strideWords;
  uint capacity;
  uint flags;
  int aliveOffsetWords;
  int seedOffsetWords;
  float deltaTime;
  uint systemSeed;
  uint sequentialLimit;
  uint operationCount;
};

uint vfx_update_hash(uint value) {
  value = (value ^ 61u) ^ (value >> 16);
  value += value << 3;
  value ^= value >> 4;
  value *= 0x27d4eb2du;
  value ^= value >> 15;
  return value;
}

float vfx_update_random(thread uint& state) {
  state = vfx_update_hash(state);
  return float(state & 0x00ffffffu) * (1.0 / 16777216.0);
}

uint vfx_update_compose(
    int type, int composition, uint current, uint value, float blend) {
  if (composition == 0) return value;
  if (type >= 4) {
    float a = as_type<float>(current);
    float b = as_type<float>(value);
    if (composition == 1) return as_type<uint>(a + b);
    if (composition == 2) return as_type<uint>(a * b);
    return as_type<uint>(a + (b - a) * blend);
  }
  if (type == 2)
    return composition == 1 ? current + value : current * value;
  int a = as_type<int>(current);
  int b = as_type<int>(value);
  return as_type<uint>(composition == 1 ? a + b : a * b);
}

kernel void anity_vfx_update_kernel(
    const device uint* source [[buffer(0)]],
    device uint* output [[buffer(1)]],
    const device OperationDesc* operations [[buffer(2)]],
    constant Params& params [[buffer(3)]],
    device uint* deathFlags [[buffer(4)]],
    const device uint* allocationState [[buffer(5)]],
    uint particleIndex [[thread_position_in_grid]]) {
  if (particleIndex >= params.capacity) return;
  deathFlags[particleIndex] = 0u;
  uint base = particleIndex * params.strideWords;
  bool initiallyAlive = params.aliveOffsetWords >= 0
      ? source[base + uint(params.aliveOffsetWords)] != 0u
      : particleIndex < min(params.capacity, allocationState[2]);
  if (!initiallyAlive) return;

  uint randomState = params.seedOffsetWords >= 0
      ? output[base + uint(params.seedOffsetWords)] ^ particleIndex
      : vfx_update_hash(particleIndex ^ params.systemSeed);
  bool usedRandom = false;
  if ((params.flags & 2u) == 0u || params.deltaTime != 0.0f) {
    for (uint operationIndex = 0u;
         operationIndex < params.operationCount; ++operationIndex) {
      const device OperationDesc& op = operations[operationIndex];
      uint targetWord = base + uint(op.targetOffsetBytes / 4);
      int sourceAWord = op.sourceAOffsetBytes < 0
          ? -1 : int(base) + op.sourceAOffsetBytes / 4;
      switch (op.kind) {
        case 0: {
          float uniformRandom = 0.0f;
          if (op.randomMode == 2) {
            uniformRandom = vfx_update_random(randomState);
            usedRandom = true;
          }
          for (int component = 0; component < op.componentCount; ++component) {
            uint value = 0u;
            if ((op.flags & 1) != 0) {
              value = source[base + uint(op.sourceAOffsetBytes / 4 + component)];
            } else {
              value = op.valueA[component];
              if (op.randomMode != 0) {
                float t = op.randomMode == 2
                    ? uniformRandom : vfx_update_random(randomState);
                usedRandom = true;
                float a = as_type<float>(op.valueA[component]);
                float b = as_type<float>(op.valueB[component]);
                value = as_type<uint>(a + (b - a) * t);
              }
            }
            uint current = output[targetWord + uint(component)];
            output[targetWord + uint(component)] = vfx_update_compose(
                op.valueType, op.composition, current, value,
                as_type<float>(op.blendFactorBits));
          }
          break;
        }
        case 1: {
          uint copied[4] = {0u, 0u, 0u, 0u};
          for (int component = 0; component < op.componentCount; ++component)
            copied[component] = output[uint(sourceAWord + component)];
          for (int component = 0; component < op.componentCount; ++component)
            output[targetWord + uint(component)] = copied[component];
          break;
        }
        case 2:
          for (int component = 0; component < op.componentCount; ++component) {
            float value = sourceAWord >= 0
                ? as_type<float>(output[uint(sourceAWord + component)])
                : as_type<float>(op.valueA[component]);
            float current = as_type<float>(output[targetWord + uint(component)]);
            output[targetWord + uint(component)] =
                as_type<uint>(current + value * params.deltaTime);
          }
          break;
        case 3: {
          float age = as_type<float>(output[uint(sourceAWord)]);
          float lifetime = as_type<float>(
              output[base + uint(op.sourceBOffsetBytes / 4)]);
          if (age > lifetime) output[targetWord] = 0u;
          break;
        }
        case 4: {
          float mass = as_type<float>(output[uint(sourceAWord)]);
          for (int component = 0; component < 3; ++component) {
            float current = as_type<float>(output[targetWord + uint(component)]);
            float force = as_type<float>(op.valueA[component]);
            output[targetWord + uint(component)] =
                as_type<uint>(current + (force / mass) * params.deltaTime);
          }
          break;
        }
        case 5: {
          float mass = as_type<float>(output[uint(sourceAWord)]);
          float drag = as_type<float>(op.valueB[0]);
          float factor = min(1.0f, drag * params.deltaTime / mass);
          for (int component = 0; component < 3; ++component) {
            float current = as_type<float>(output[targetWord + uint(component)]);
            float desired = as_type<float>(op.valueA[component]);
            output[targetWord + uint(component)] =
                as_type<uint>(current + (desired - current) * factor);
          }
          break;
        }
        case 6: {
          float mass = as_type<float>(output[uint(sourceAWord)]);
          float drag = as_type<float>(op.valueA[0]);
          if (op.sourceBOffsetBytes >= 0) {
            float size = as_type<float>(
                output[base + uint(op.sourceBOffsetBytes / 4)]);
            float scaleX = as_type<float>(
                output[base + uint(op.auxiliaryOffset0Bytes / 4)]);
            float scaleY = as_type<float>(
                output[base + uint(op.auxiliaryOffset1Bytes / 4)]);
            drag *= (size * scaleX) * (size * scaleY);
          }
          float factor = max(0.0f, 1.0f -
              (drag * params.deltaTime) / mass);
          for (int component = 0; component < 3; ++component) {
            float current = as_type<float>(output[targetWord + uint(component)]);
            output[targetWord + uint(component)] =
                as_type<uint>(current * factor);
          }
          break;
        }
      }
    }
  }

  if (usedRandom && params.seedOffsetWords >= 0)
    output[base + uint(params.seedOffsetWords)] = randomState;
  bool alive = params.aliveOffsetWords < 0 ||
      output[base + uint(params.aliveOffsetWords)] != 0u;
  if (!alive) {
    for (uint word = 0u; word < params.strideWords; ++word)
      output[base + word] = source[base + word];
    output[base + uint(params.aliveOffsetWords)] = 0u;
    deathFlags[particleIndex] = 1u;
  }
}
)METAL";
  NSString* source = [NSString stringWithUTF8String:sourceText];
  NSError* error = nil;
  id<MTLLibrary> library =
      [st->device newLibraryWithSource:source options:nil error:&error];
  if (!library) return nil;
  id<MTLFunction> function =
      [library newFunctionWithName:@"anity_vfx_update_kernel"];
  if (!function) return nil;
  st->vfxUpdateKernelPipeline =
      [st->device newComputePipelineStateWithFunction:function error:&error];
  [function release];
  [library release];
  return st->vfxUpdateKernelPipeline;
}

static bool GetVFXDeathPipelines(
    MetalState* st,
    id<MTLComputePipelineState>* outPrefix,
    id<MTLComputePipelineState>* outCompact,
    id<MTLComputePipelineState>* outCommit) {
  if (!st || !st->device || !outPrefix || !outCompact || !outCommit)
    return false;
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  if (!st->vfxDeathPrefixPipeline || !st->vfxDeathCompactPipeline ||
      !st->vfxDeathCommitPipeline) {
    /* Editor reloads and test workers may create several devices together.
     * Serialize source compilation; command encoding/execution remains fully
     * concurrent after the immutable pipelines exist. */
    std::lock_guard<std::mutex> compilationLock(
        gMetalVFXPipelineCompilationMutex);
    NSString* source = @"#include <metal_stdlib>\n"
        "using namespace metal;\n"
        "kernel void anity_vfx_death_prefix(const device uint* input [[buffer(0)]], device uint* output [[buffer(1)]], constant uint& offset [[buffer(2)]], constant uint& count [[buffer(3)]], uint tid [[thread_position_in_grid]]) { if (tid >= count) return; uint value = input[tid]; if (tid >= offset) value += input[tid - offset]; output[tid] = value; }\n"
        "kernel void anity_vfx_death_compact(const device uint* prefix [[buffer(0)]], device uint* indices [[buffer(1)]], device uint* countOut [[buffer(2)]], constant uint& count [[buffer(3)]], uint tid [[thread_position_in_grid]]) { if (tid >= count) return; uint current = prefix[tid]; uint previous = tid == 0u ? 0u : prefix[tid - 1u]; if (current > previous) indices[current - 1u] = tid; if (tid + 1u == count) countOut[0] = current; }\n"
        "kernel void anity_vfx_death_commit(const device uint* newIndices [[buffer(0)]], const device uint* newCountBuffer [[buffer(1)]], device uint* deadList [[buffer(2)]], const device uint* sourceState [[buffer(3)]], device uint* targetState [[buffer(4)]], constant uint& capacity [[buffer(5)]], uint tid [[thread_position_in_grid]]) { uint newCount = min(newCountBuffer[0], capacity); uint previousDeadCount = min(sourceState[1], capacity); bool usesDeadList = sourceState[3] != 0u; if (usesDeadList && tid < newCount && previousDeadCount + tid < capacity) deadList[previousDeadCount + tid] = newIndices[tid]; if (tid == 0u) { targetState[0] = newCount > sourceState[0] ? 0u : sourceState[0] - newCount; targetState[1] = usesDeadList ? min(capacity, previousDeadCount + newCount) : 0u; targetState[2] = sourceState[2]; targetState[3] = sourceState[3]; } }\n";
    NSError* error = nil;
    id<MTLLibrary> library =
        [st->device newLibraryWithSource:source options:nil error:&error];
    if (!library) return false;
    id<MTLFunction> prefixFunction =
        [library newFunctionWithName:@"anity_vfx_death_prefix"];
    id<MTLFunction> compactFunction =
        [library newFunctionWithName:@"anity_vfx_death_compact"];
    id<MTLFunction> commitFunction =
        [library newFunctionWithName:@"anity_vfx_death_commit"];
    if (!prefixFunction || !compactFunction || !commitFunction) return false;
    st->vfxDeathPrefixPipeline =
        [st->device newComputePipelineStateWithFunction:prefixFunction
                                                  error:&error];
    st->vfxDeathCompactPipeline =
        [st->device newComputePipelineStateWithFunction:compactFunction
                                                  error:&error];
    st->vfxDeathCommitPipeline =
        [st->device newComputePipelineStateWithFunction:commitFunction
                                                  error:&error];
    [prefixFunction release];
    [compactFunction release];
    [commitFunction release];
    [library release];
  }
  *outPrefix = st->vfxDeathPrefixPipeline;
  *outCompact = st->vfxDeathCompactPipeline;
  *outCommit = st->vfxDeathCommitPipeline;
  return *outPrefix && *outCompact && *outCommit;
}

static bool GetVFXPlanarComputePipelines(
    MetalState* st,
    id<MTLComputePipelineState>* outAliveMap,
    id<MTLComputePipelineState>* outIndirect) {
  if (!st || !st->device || !outAliveMap || !outIndirect) return false;
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  if (!st->vfxPlanarAliveMapPipeline || !st->vfxPlanarIndirectPipeline) {
    NSString* source = @"#include <metal_stdlib>\n"
        "using namespace metal;\n"
        "struct AliveParams { uint strideWords; uint capacity; int aliveOffsetWords; };\n"
        "kernel void anity_vfx_planar_alive_map(const device uint* records [[buffer(0)]], device uint* flags [[buffer(1)]], constant AliveParams& p [[buffer(2)]], uint tid [[thread_position_in_grid]]) { if (tid >= p.capacity) return; flags[tid] = records[tid * p.strideWords + uint(p.aliveOffsetWords)] != 0u ? 1u : 0u; }\n"
        "kernel void anity_vfx_planar_indirect(const device uint* aliveCount [[buffer(0)]], device uint4* args [[buffer(1)]], constant uint& verticesPerParticle [[buffer(2)]]) { args[0] = uint4(aliveCount[0] * verticesPerParticle, 1u, 0u, 0u); }\n";
    NSError* error = nil;
    id<MTLLibrary> library =
        [st->device newLibraryWithSource:source options:nil error:&error];
    if (!library) {
      NSLog(@"Anity VFX Planar compute library compilation failed: %@", error);
      return false;
    }
    id<MTLFunction> aliveMap =
        [library newFunctionWithName:@"anity_vfx_planar_alive_map"];
    id<MTLFunction> indirect =
        [library newFunctionWithName:@"anity_vfx_planar_indirect"];
    if (!aliveMap || !indirect) return false;
    st->vfxPlanarAliveMapPipeline =
        [st->device newComputePipelineStateWithFunction:aliveMap error:&error];
    st->vfxPlanarIndirectPipeline =
        [st->device newComputePipelineStateWithFunction:indirect error:&error];
  }
  *outAliveMap = st->vfxPlanarAliveMapPipeline;
  *outIndirect = st->vfxPlanarIndirectPipeline;
  return *outAliveMap && *outIndirect;
}

static bool GetVFXPlanarSortPipelines(
    MetalState* st,
    id<MTLComputePipelineState>* outMap,
    id<MTLComputePipelineState>* outStage,
    id<MTLComputePipelineState>* outExtract) {
  if (!st || !st->device || !outMap || !outStage || !outExtract) return false;
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  if (!st->vfxPlanarSortMapPipeline || !st->vfxPlanarSortStagePipeline ||
      !st->vfxPlanarSortExtractPipeline) {
    NSString* source = @R"metal(
#include <metal_stdlib>
using namespace metal;

struct SortEntry {
  float depth;
  uint ordinal;
  uint particle;
  uint valid;
};

struct SortMapParams {
  uint strideWords;
  uint capacity;
  int positionOffsetWords;
  uint paddedLength;
  float localToWorld[16];
  float worldToClip[16];
};

struct SortStageParams {
  uint compareDistance;
  uint sequenceLength;
  uint paddedLength;
};

static float loadFloat(device const uint* records, uint word) {
  return as_type<float>(records[word]);
}

static float4 mulRowMajor(constant float* matrix, float4 value) {
  return float4(dot(float4(matrix[0], matrix[1], matrix[2], matrix[3]), value),
                dot(float4(matrix[4], matrix[5], matrix[6], matrix[7]), value),
                dot(float4(matrix[8], matrix[9], matrix[10], matrix[11]), value),
                dot(float4(matrix[12], matrix[13], matrix[14], matrix[15]), value));
}

kernel void anity_vfx_planar_sort_map(
    device const uint* records [[buffer(0)]],
    device const uint* aliveIndices [[buffer(1)]],
    device const uint* aliveCount [[buffer(2)]],
    device SortEntry* entries [[buffer(3)]],
    constant SortMapParams& p [[buffer(4)]],
    uint tid [[thread_position_in_grid]]) {
  if (tid >= p.paddedLength) return;
  SortEntry entry;
  entry.depth = -INFINITY;
  entry.ordinal = tid;
  entry.particle = 0u;
  entry.valid = 0u;
  const uint count = min(aliveCount[0], p.capacity);
  if (tid < count) {
    const uint particle = aliveIndices[tid];
    if (particle < p.capacity) {
      const uint base = particle * p.strideWords + uint(p.positionOffsetWords);
      const float3 position = float3(
          loadFloat(records, base + 0u),
          loadFloat(records, base + 1u),
          loadFloat(records, base + 2u));
      const float4 world = mulRowMajor(p.localToWorld, float4(position, 1.0));
      const float4 clip = mulRowMajor(p.worldToClip, world);
      const float projected = clip.w != 0.0 ? clip.z / clip.w : -INFINITY;
      entry.depth = isfinite(projected) ? projected : -INFINITY;
      entry.particle = particle;
      entry.valid = 1u;
    }
  }
  entries[tid] = entry;
}

static bool before(SortEntry left, SortEntry right) {
  if (left.valid != right.valid) return left.valid > right.valid;
  if (left.valid == 0u) return left.ordinal < right.ordinal;
  if (left.depth > right.depth) return true;
  if (left.depth < right.depth) return false;
  return left.ordinal < right.ordinal;
}

kernel void anity_vfx_planar_sort_stage(
    device SortEntry* entries [[buffer(0)]],
    constant SortStageParams& p [[buffer(1)]],
    uint tid [[thread_position_in_grid]]) {
  if (tid >= p.paddedLength) return;
  const uint partner = tid ^ p.compareDistance;
  if (partner <= tid || partner >= p.paddedLength) return;
  const SortEntry left = entries[tid];
  const SortEntry right = entries[partner];
  const bool ascending = (tid & p.sequenceLength) == 0u;
  const bool shouldSwap = ascending ? before(right, left) : before(left, right);
  if (shouldSwap) {
    entries[tid] = right;
    entries[partner] = left;
  }
}

kernel void anity_vfx_planar_sort_extract(
    device const SortEntry* entries [[buffer(0)]],
    device const uint* aliveCount [[buffer(1)]],
    device uint* sortedIndices [[buffer(2)]],
    constant uint& capacity [[buffer(3)]],
    uint tid [[thread_position_in_grid]]) {
  const uint count = min(aliveCount[0], capacity);
  if (tid >= count) return;
  sortedIndices[tid] = entries[tid].particle;
}
)metal";
    NSError* error = nil;
    id<MTLLibrary> library =
        [st->device newLibraryWithSource:source options:nil error:&error];
    if (!library) {
      NSLog(@"Anity VFX Planar sort library compilation failed: %@", error);
      std::fprintf(stderr,
                   "Anity VFX Planar sort library compilation failed: %s\n",
                   error.localizedDescription.UTF8String);
      return false;
    }
    id<MTLFunction> map =
        [library newFunctionWithName:@"anity_vfx_planar_sort_map"];
    id<MTLFunction> stage =
        [library newFunctionWithName:@"anity_vfx_planar_sort_stage"];
    id<MTLFunction> extract =
        [library newFunctionWithName:@"anity_vfx_planar_sort_extract"];
    if (!map || !stage || !extract) return false;
    st->vfxPlanarSortMapPipeline =
        [st->device newComputePipelineStateWithFunction:map error:&error];
    st->vfxPlanarSortStagePipeline =
        [st->device newComputePipelineStateWithFunction:stage error:&error];
    st->vfxPlanarSortExtractPipeline =
        [st->device newComputePipelineStateWithFunction:extract error:&error];
  }
  *outMap = st->vfxPlanarSortMapPipeline;
  *outStage = st->vfxPlanarSortStagePipeline;
  *outExtract = st->vfxPlanarSortExtractPipeline;
  return *outMap && *outStage && *outExtract;
}

static AnityResult EncodeMetalVFXStableCompaction(
    MetalState* st, id<MTLCommandBuffer> commandBuffer,
    id<MTLBuffer> flags, id<MTLBuffer> scratch,
    id<MTLBuffer> indices, id<MTLBuffer> countBuffer,
    uint32_t capacity,
    id<MTLComputePipelineState> prefixPipeline,
    id<MTLComputePipelineState> compactPipeline,
    id<MTLBuffer>* outFinalPrefix, uint64_t* outPrefixPasses) {
  if (!st || !commandBuffer || !flags || !scratch || !indices ||
      !countBuffer || capacity == 0 ||
      !prefixPipeline || !compactPipeline || !outFinalPrefix ||
      !outPrefixPasses)
    return ANITY_ERR_INVALID_ARG;
  *outFinalPrefix = flags;
  *outPrefixPasses = 0;
  id<MTLBuffer> input = flags;
  id<MTLBuffer> output = scratch;
  const NSUInteger prefixWidth = std::min<NSUInteger>(
      prefixPipeline.maxTotalThreadsPerThreadgroup,
      static_cast<NSUInteger>(64));
  const NSUInteger compactWidth = std::min<NSUInteger>(
      compactPipeline.maxTotalThreadsPerThreadgroup,
      static_cast<NSUInteger>(64));
  if (prefixWidth == 0 || compactWidth == 0)
    return ANITY_ERR_NOT_SUPPORTED;
  for (uint32_t offset = 1u; offset < capacity;) {
    id<MTLComputeCommandEncoder> encoder =
        [commandBuffer computeCommandEncoder];
    if (!encoder) return ANITY_ERR_DEVICE_LOST;
    [encoder setComputePipelineState:prefixPipeline];
    [encoder setBuffer:input offset:0 atIndex:0];
    [encoder setBuffer:output offset:0 atIndex:1];
    [encoder setBytes:&offset length:sizeof(offset) atIndex:2];
    [encoder setBytes:&capacity length:sizeof(capacity) atIndex:3];
    [encoder dispatchThreads:MTLSizeMake(capacity, 1, 1)
        threadsPerThreadgroup:MTLSizeMake(prefixWidth, 1, 1)];
    [encoder endEncoding];
    std::swap(input, output);
    ++*outPrefixPasses;
    if (offset > capacity / 2u) break;
    offset *= 2u;
  }
  id<MTLComputeCommandEncoder> compactEncoder =
      [commandBuffer computeCommandEncoder];
  if (!compactEncoder) return ANITY_ERR_DEVICE_LOST;
  [compactEncoder setComputePipelineState:compactPipeline];
  [compactEncoder setBuffer:input offset:0 atIndex:0];
  [compactEncoder setBuffer:indices offset:0 atIndex:1];
  [compactEncoder setBuffer:countBuffer offset:0 atIndex:2];
  [compactEncoder setBytes:&capacity length:sizeof(capacity) atIndex:3];
  [compactEncoder dispatchThreads:MTLSizeMake(capacity, 1, 1)
      threadsPerThreadgroup:MTLSizeMake(compactWidth, 1, 1)];
  [compactEncoder endEncoding];
  *outFinalPrefix = input;
  return ANITY_OK;
}

static AnityResult EncodeMetalVFXDeathCompaction(
    MetalState* st, id<MTLCommandBuffer> commandBuffer,
    MetalVFXUpdateSlot* slot, uint32_t capacity,
    id<MTLComputePipelineState> prefixPipeline,
    id<MTLComputePipelineState> compactPipeline,
    id<MTLBuffer>* outFinalPrefix, uint64_t* outPrefixPasses) {
  if (!slot) return ANITY_ERR_INVALID_ARG;
  return EncodeMetalVFXStableCompaction(
      st, commandBuffer, slot->deathPrefix, slot->deathScratch,
      slot->deadIndices, slot->deadCount, capacity,
      prefixPipeline, compactPipeline, outFinalPrefix, outPrefixPasses);
}

static bool GetVFXBoundsPipelines(
    MetalState* st,
    id<MTLComputePipelineState>* outMap,
    id<MTLComputePipelineState>* outReduce) {
  if (!st || !st->device || !outMap || !outReduce) return false;
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  if (!st->vfxBoundsMapPipeline || !st->vfxBoundsReducePipeline) {
    NSString* source = @"#include <metal_stdlib>\n"
        "using namespace metal;\n"
        "struct BoundsResult { float minX; float minY; float minZ; float maxX; float maxY; float maxZ; uint validCount; uint invalid; };\n"
        "struct MapParams { uint strideWords; uint capacity; int positionOffsetWords; int aliveOffsetWords; int sizeOffsetWords; int scaleXOffsetWords; int scaleYOffsetWords; int scaleZOffsetWords; uint expectedAliveCount; uint sequentialLimit; };\n"
        "BoundsResult empty_bounds() { BoundsResult r; r.minX = INFINITY; r.minY = INFINITY; r.minZ = INFINITY; r.maxX = -INFINITY; r.maxY = -INFINITY; r.maxZ = -INFINITY; r.validCount = 0u; r.invalid = 0u; return r; }\n"
        "kernel void anity_vfx_bounds_map(const device uint* particles [[buffer(0)]], device BoundsResult* output [[buffer(1)]], constant MapParams& p [[buffer(2)]], uint tid [[thread_position_in_grid]]) {\n"
        "  if (tid >= p.capacity) return; BoundsResult r = empty_bounds(); uint base = tid * p.strideWords; bool alive = p.aliveOffsetWords >= 0 ? particles[base + uint(p.aliveOffsetWords)] != 0u : tid < p.sequentialLimit; if (!alive) { output[tid] = r; return; }\n"
        "  float x = as_type<float>(particles[base + uint(p.positionOffsetWords)]); float y = as_type<float>(particles[base + uint(p.positionOffsetWords + 1)]); float z = as_type<float>(particles[base + uint(p.positionOffsetWords + 2)]);\n"
        "  float size = p.sizeOffsetWords >= 0 ? abs(as_type<float>(particles[base + uint(p.sizeOffsetWords)])) : 0.0f; float sx = p.scaleXOffsetWords >= 0 ? abs(as_type<float>(particles[base + uint(p.scaleXOffsetWords)])) : 1.0f; float sy = p.scaleYOffsetWords >= 0 ? abs(as_type<float>(particles[base + uint(p.scaleYOffsetWords)])) : 1.0f; float sz = p.scaleZOffsetWords >= 0 ? abs(as_type<float>(particles[base + uint(p.scaleZOffsetWords)])) : 1.0f;\n"
        "  if (!isfinite(x) || !isfinite(y) || !isfinite(z) || !isfinite(size) || !isfinite(sx) || !isfinite(sy) || !isfinite(sz)) { r.invalid = 1u; output[tid] = r; return; }\n"
        "  float ex = size * sx * 0.5f; float ey = size * sy * 0.5f; float ez = size * sz * 0.5f; if (!isfinite(ex) || !isfinite(ey) || !isfinite(ez)) { r.invalid = 1u; output[tid] = r; return; }\n"
        "  r.minX = x - ex; r.minY = y - ey; r.minZ = z - ez; r.maxX = x + ex; r.maxY = y + ey; r.maxZ = z + ez; if (!isfinite(r.minX) || !isfinite(r.minY) || !isfinite(r.minZ) || !isfinite(r.maxX) || !isfinite(r.maxY) || !isfinite(r.maxZ)) { r = empty_bounds(); r.invalid = 1u; } else { r.validCount = 1u; } output[tid] = r;\n"
        "}\n"
        "kernel void anity_vfx_bounds_reduce(const device BoundsResult* input [[buffer(0)]], device BoundsResult* output [[buffer(1)]], constant uint& inputCount [[buffer(2)]], uint tid [[thread_position_in_grid]]) {\n"
        "  uint first = tid * 2u; if (first >= inputCount) return; BoundsResult a = input[first]; if (first + 1u >= inputCount) { output[tid] = a; return; } BoundsResult b = input[first + 1u]; BoundsResult r; r.minX = min(a.minX, b.minX); r.minY = min(a.minY, b.minY); r.minZ = min(a.minZ, b.minZ); r.maxX = max(a.maxX, b.maxX); r.maxY = max(a.maxY, b.maxY); r.maxZ = max(a.maxZ, b.maxZ); r.validCount = a.validCount + b.validCount; r.invalid = a.invalid | b.invalid; output[tid] = r;\n"
        "}\n";
    NSError* error = nil;
    id<MTLLibrary> library =
        [st->device newLibraryWithSource:source options:nil error:&error];
    if (!library) return false;
    id<MTLFunction> mapFunction =
        [library newFunctionWithName:@"anity_vfx_bounds_map"];
    id<MTLFunction> reduceFunction =
        [library newFunctionWithName:@"anity_vfx_bounds_reduce"];
    if (!mapFunction || !reduceFunction) return false;
    st->vfxBoundsMapPipeline =
        [st->device newComputePipelineStateWithFunction:mapFunction error:&error];
    st->vfxBoundsReducePipeline =
        [st->device newComputePipelineStateWithFunction:reduceFunction error:&error];
  }
  *outMap = st->vfxBoundsMapPipeline;
  *outReduce = st->vfxBoundsReducePipeline;
  return *outMap && *outReduce;
}

static AnityResult EncodeMetalVFXBoundsReduction(
    MetalState* st, id<MTLCommandBuffer> commandBuffer,
    id<MTLBuffer> particles,
    const AnityGraphicsVFXBoundsReductionDesc& desc,
    int32_t capacity, int32_t attributeStrideBytes,
    uint32_t nextSequentialIndex,
    id<MTLComputePipelineState> mapPipeline,
    id<MTLComputePipelineState> reducePipeline,
    id<MTLBuffer>* outResultBuffer) {
  if (!st || !commandBuffer || !particles || !mapPipeline || !reducePipeline ||
      !outResultBuffer || capacity <= 0 || attributeStrideBytes <= 0)
    return ANITY_ERR_INVALID_ARG;
  *outResultBuffer = nil;
  MetalVFXBoundsMapParams params{
      static_cast<uint32_t>(attributeStrideBytes / 4),
      static_cast<uint32_t>(capacity),
      desc.positionOffsetBytes / 4,
      desc.aliveOffsetBytes < 0 ? -1 : desc.aliveOffsetBytes / 4,
      desc.sizeOffsetBytes < 0 ? -1 : desc.sizeOffsetBytes / 4,
      desc.scaleXOffsetBytes < 0 ? -1 : desc.scaleXOffsetBytes / 4,
      desc.scaleYOffsetBytes < 0 ? -1 : desc.scaleYOffsetBytes / 4,
      desc.scaleZOffsetBytes < 0 ? -1 : desc.scaleZOffsetBytes / 4,
      0u,
      std::min<uint32_t>(nextSequentialIndex,
                         static_cast<uint32_t>(capacity))};
  id<MTLBuffer> current = [st->device
      newBufferWithLength:static_cast<NSUInteger>(capacity) *
          sizeof(MetalVFXRawBoundsResult)
      options:MTLResourceStorageModeShared];
  if (!current) return ANITY_ERR_OUT_OF_MEMORY;
  id<MTLComputeCommandEncoder> mapEncoder =
      [commandBuffer computeCommandEncoder];
  if (!mapEncoder) return ANITY_ERR_DEVICE_LOST;
  [mapEncoder setComputePipelineState:mapPipeline];
  [mapEncoder setBuffer:particles offset:0 atIndex:0];
  [mapEncoder setBuffer:current offset:0 atIndex:1];
  [mapEncoder setBytes:&params length:sizeof(params) atIndex:2];
  const NSUInteger mapWidth = std::min<NSUInteger>(
      mapPipeline.maxTotalThreadsPerThreadgroup, static_cast<NSUInteger>(64));
  if (mapWidth == 0) {
    [mapEncoder endEncoding];
    return ANITY_ERR_NOT_SUPPORTED;
  }
  [mapEncoder dispatchThreads:MTLSizeMake(
      static_cast<NSUInteger>(capacity), 1, 1)
      threadsPerThreadgroup:MTLSizeMake(mapWidth, 1, 1)];
  [mapEncoder endEncoding];

  uint32_t currentCount = static_cast<uint32_t>(capacity);
  while (currentCount > 1u) {
    const uint32_t outputCount = (currentCount + 1u) / 2u;
    id<MTLBuffer> next = [st->device
        newBufferWithLength:static_cast<NSUInteger>(outputCount) *
            sizeof(MetalVFXRawBoundsResult)
        options:MTLResourceStorageModeShared];
    if (!next) return ANITY_ERR_OUT_OF_MEMORY;
    id<MTLComputeCommandEncoder> reduceEncoder =
        [commandBuffer computeCommandEncoder];
    if (!reduceEncoder) return ANITY_ERR_DEVICE_LOST;
    [reduceEncoder setComputePipelineState:reducePipeline];
    [reduceEncoder setBuffer:current offset:0 atIndex:0];
    [reduceEncoder setBuffer:next offset:0 atIndex:1];
    [reduceEncoder setBytes:&currentCount length:sizeof(currentCount) atIndex:2];
    const NSUInteger reduceWidth = std::min<NSUInteger>(
        reducePipeline.maxTotalThreadsPerThreadgroup,
        static_cast<NSUInteger>(64));
    if (reduceWidth == 0) {
      [reduceEncoder endEncoding];
      return ANITY_ERR_NOT_SUPPORTED;
    }
    [reduceEncoder dispatchThreads:MTLSizeMake(outputCount, 1, 1)
        threadsPerThreadgroup:MTLSizeMake(reduceWidth, 1, 1)];
    [reduceEncoder endEncoding];
    current = next;
    currentCount = outputCount;
  }
  *outResultBuffer = current;
  return ANITY_OK;
}

static AnityGraphicsVFXBoundsReductionResult BuildMetalVFXBoundsResult(
    const AnityGraphicsVFXBoundsReductionDesc& desc,
    const MetalVFXRawBoundsResult& raw,
    int32_t expectedAliveCount, uint64_t generation) {
  AnityGraphicsVFXBoundsReductionResult result{};
  result.effectId = desc.effectId;
  result.particleSystemId = desc.particleSystemId;
  result.backendKind = 2;
  result.boundsInWorldSpace = desc.boundsInWorldSpace;
  result.generation = generation;
  if (expectedAliveCount <= 0 || raw.invalid != 0u ||
      raw.validCount != static_cast<uint32_t>(expectedAliveCount))
    return result;
  const float minimum[3] = {raw.minX, raw.minY, raw.minZ};
  const float maximum[3] = {raw.maxX, raw.maxY, raw.maxZ};
  const float padding[3] = {desc.paddingX, desc.paddingY, desc.paddingZ};
  float center[3]{};
  float extents[3]{};
  for (int axis = 0; axis < 3; ++axis) {
    center[axis] = minimum[axis] + (maximum[axis] - minimum[axis]) * 0.5f;
    extents[axis] = (maximum[axis] - minimum[axis]) * 0.5f + padding[axis];
    if (!std::isfinite(center[axis]) || !std::isfinite(extents[axis]) ||
        extents[axis] < 0.0f)
      return result;
  }
  result.centerX = center[0];
  result.centerY = center[1];
  result.centerZ = center[2];
  result.extentsX = extents[0];
  result.extentsY = extents[1];
  result.extentsZ = extents[2];
  result.valid = 1;
  return result;
}

static NSUInteger GrowCapacity(NSUInteger required) {
  NSUInteger capacity = 4096;
  while (capacity < required && capacity <= NSUIntegerMax / 2) capacity *= 2;
  return capacity < required ? required : capacity;
}

static AnityResult UploadMetalBuffer(
    MetalState* st, id<MTLBuffer>* buffer, NSUInteger* capacity,
    const void* data, int32_t bytes) {
  if (bytes == 0) return ANITY_OK;
  if (!st || !st->device || !data || bytes < 0) return ANITY_ERR_INVALID_ARG;
  const NSUInteger required = static_cast<NSUInteger>(bytes);
  if (!*buffer || *capacity < required) {
    *capacity = GrowCapacity(required);
    *buffer = [st->device newBufferWithLength:*capacity options:MTLResourceStorageModeShared];
    if (!*buffer) return ANITY_ERR_OUT_OF_MEMORY;
  }
  std::memcpy([*buffer contents], data, required);
  return ANITY_OK;
}

static AnityResult EnsureMetalBuffer(
    MetalState* st, id<MTLBuffer>* buffer, NSUInteger* capacity,
    int32_t bytes) {
  if (!st || !st->device || !buffer || !capacity || bytes <= 0)
    return ANITY_ERR_INVALID_ARG;
  const NSUInteger required = static_cast<NSUInteger>(bytes);
  if (!*buffer || *capacity < required) {
    const NSUInteger grown = GrowCapacity(required);
    id<MTLBuffer> replacement = [st->device
        newBufferWithLength:grown options:MTLResourceStorageModeShared];
    if (!replacement) return ANITY_ERR_OUT_OF_MEMORY;
    *buffer = replacement;
    *capacity = grown;
  }
  return ANITY_OK;
}

static AnityResult EnsureMetalOwnedBuffer(
    MetalState* st, id<MTLBuffer>* buffer, NSUInteger* capacity,
    int32_t bytes) {
  id<MTLBuffer> previous = *buffer;
  const AnityResult result = EnsureMetalBuffer(st, buffer, capacity, bytes);
  if (result == ANITY_OK && previous && previous != *buffer)
    [previous release];
  return result;
}

struct MetalSwapchainState {
  int32_t width = 0;
  int32_t height = 0;
  int32_t imageCount = 3;
  int32_t headless = 1;
  int32_t hasNativeLayer = 0;
  int32_t ownsLayer = 0;
  CAMetalLayer* layer = nil;
  id<CAMetalDrawable> currentDrawable = nil;
  id<MTLTexture> offscreenTexture = nil;
  id<MTLTexture> depthTexture = nil;
  bool depthInitialized = false;
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
  st->vfxPlanarSubmissionStats.backendKind = 2;
  for (int i = 0; i < 3; ++i)
    st->uiSlotSemaphores[i] = dispatch_semaphore_create(1);

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

extern "C" AnityResult AnityGraphics_Metal_GetVFXPlanarSubmissionStats(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXPlanarSubmissionStats* outStats) {
  if (!device || !device->backend || !outStats)
    return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  std::lock_guard<std::mutex> lock(st->vfxPlanarSubmissionMutex);
  *outStats = st->vfxPlanarSubmissionStats;
  outStats->deviceLost = IsMetalDeviceLost(st) ? 1 : 0;
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_WaitForVFXPlanarSubmissions(
    AnityGraphicsDevice* device, uint64_t throughSubmissionId,
    int32_t timeoutMilliseconds) {
  if (!device || !device->backend) return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  return WaitForMetalVFXPlanarSubmissions(
      st, throughSubmissionId, timeoutMilliseconds, true);
}

extern "C" void AnityGraphics_Metal_Destroy(AnityGraphicsDevice* device) {
  if (!device || !device->backend) return;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  (void)WaitForMetalVFXPlanarSubmissions(st, 0, -1, true);
  for (int i = 0; i < 3; ++i) {
    if (st->uiSlotAcquired[i]) {
      if (st->uiSlotSubmitted[i].load(std::memory_order_acquire) != 0)
        dispatch_semaphore_wait(st->uiSlotSemaphores[i], DISPATCH_TIME_FOREVER);
      st->uiSlotAcquired[i] = false;
    }
    st->uiVertexBuffers[i] = nil;
    st->uiIndexBuffers[i] = nil;
  }
  st->uiPipelineBGRA8 = nil;
  st->uiPipelineRGBA16 = nil;
  [st->vfxInitializeCopyPipeline release];
  st->vfxInitializeCopyPipeline = nil;
  [st->vfxInitializeKernelPipeline release];
  st->vfxInitializeKernelPipeline = nil;
  [st->vfxInitializeIndirectPipeline release];
  st->vfxInitializeIndirectPipeline = nil;
  {
    std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
    for (auto& item : st->vfxUpdateBuffers)
      ReleaseMetalVFXUpdateBuffers(&item.second);
    [st->vfxUpdateKernelPipeline release];
    st->vfxUpdateKernelPipeline = nil;
    [st->vfxDeathPrefixPipeline release];
    st->vfxDeathPrefixPipeline = nil;
    [st->vfxDeathCompactPipeline release];
    st->vfxDeathCompactPipeline = nil;
    [st->vfxDeathCommitPipeline release];
    st->vfxDeathCommitPipeline = nil;
    [st->vfxPlanarAliveMapPipeline release];
    st->vfxPlanarAliveMapPipeline = nil;
    [st->vfxPlanarIndirectPipeline release];
    st->vfxPlanarIndirectPipeline = nil;
    [st->vfxPlanarSortMapPipeline release];
    st->vfxPlanarSortMapPipeline = nil;
    [st->vfxPlanarSortStagePipeline release];
    st->vfxPlanarSortStagePipeline = nil;
    [st->vfxPlanarSortExtractPipeline release];
    st->vfxPlanarSortExtractPipeline = nil;
    [st->vfxBoundsMapPipeline release];
    st->vfxBoundsMapPipeline = nil;
    [st->vfxBoundsReducePipeline release];
    st->vfxBoundsReducePipeline = nil;
    st->vfxUpdateBuffers.clear();
  }
  {
    std::lock_guard<std::mutex> lock(st->textureMutex);
    st->textures.clear();
  }
  st->whiteTexture = nil;
  st->defaultSampler = nil;
  st->queue = nil;
  st->device = nil;
  delete st;
  device->backend = nullptr;
}

extern "C" AnityResult AnityGraphics_Metal_DispatchVFXInitializeCopy(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* desc,
    const uint8_t* sourceRecords, int32_t sourceByteCount,
    uint8_t* outputRecords, int32_t outputByteCount) {
  if (!device || !device->backend || !desc || !sourceRecords ||
      sourceByteCount <= 0 || !outputRecords || outputByteCount <= 0 ||
      desc->startEventIndex < 0 || desc->recordCount <= 0 ||
      desc->strideBytes <= 0 || (desc->strideBytes & 3) != 0)
    return ANITY_ERR_INVALID_ARG;
  const int64_t requiredSourceBytes =
      (static_cast<int64_t>(desc->startEventIndex) + desc->recordCount) *
      desc->strideBytes;
  const int64_t requiredOutputBytes =
      static_cast<int64_t>(desc->recordCount) * desc->strideBytes;
  if (requiredSourceBytes > sourceByteCount || requiredOutputBytes != outputByteCount)
    return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
  id<MTLComputePipelineState> pipeline = GetVFXInitializeCopyPipeline(st);
  if (!st->device || !st->queue || !pipeline) return ANITY_ERR_NOT_SUPPORTED;
  id<MTLBuffer> source = [st->device
      newBufferWithBytes:sourceRecords length:static_cast<NSUInteger>(sourceByteCount)
      options:MTLResourceStorageModeShared];
  id<MTLBuffer> output = [st->device
      newBufferWithLength:static_cast<NSUInteger>(outputByteCount)
      options:MTLResourceStorageModeShared];
  if (!source || !output) return ANITY_ERR_OUT_OF_MEMORY;
  id<MTLCommandBuffer> commandBuffer = [st->queue commandBuffer];
  id<MTLComputeCommandEncoder> encoder = [commandBuffer computeCommandEncoder];
  if (!commandBuffer || !encoder) return ANITY_ERR_DEVICE_LOST;
  struct Params { uint32_t startEventIndex; uint32_t recordCount; uint32_t strideWords; } params = {
      static_cast<uint32_t>(desc->startEventIndex),
      static_cast<uint32_t>(desc->recordCount),
      static_cast<uint32_t>(desc->strideBytes / 4)};
  [encoder setComputePipelineState:pipeline];
  [encoder setBuffer:source offset:0 atIndex:0];
  [encoder setBuffer:output offset:0 atIndex:1];
  [encoder setBytes:&params length:sizeof(params) atIndex:2];
  const NSUInteger totalWords = static_cast<NSUInteger>(outputByteCount / 4);
  const NSUInteger width = std::min<NSUInteger>(
      pipeline.maxTotalThreadsPerThreadgroup, static_cast<NSUInteger>(64));
  if (width == 0) return ANITY_ERR_NOT_SUPPORTED;
  [encoder dispatchThreads:MTLSizeMake(totalWords, 1, 1)
      threadsPerThreadgroup:MTLSizeMake(width, 1, 1)];
  [encoder endEncoding];
  [commandBuffer commit];
  [commandBuffer waitUntilCompleted];
  ObserveMetalDeviceLoss(st, commandBuffer);
  if (commandBuffer.status != MTLCommandBufferStatusCompleted)
    return ANITY_ERR_DEVICE_LOST;
  std::memcpy(outputRecords, [output contents], static_cast<size_t>(outputByteCount));
  return ANITY_OK;
}

struct MetalVFXInitializeHandle {
  MetalState* state = nullptr;
  MetalVFXParticleKey key{};
  uint32_t ringIndex = 0;
  bool resident = false;
  bool residentPublished = false;
  bool asynchronousPublished = false;
  bool slotAcquired = false;
  bool retainSourceGeneration = false;
  bool injectedFailure = false;
  bool injectedDeviceRemoval = false;
  id<MTLCommandBuffer> commandBuffer = nil;
  id<MTLBuffer> source = nil;
  id<MTLBuffer> particles = nil;
  id<MTLBuffer> dead = nil;
  id<MTLBuffer> attributes = nil;
  id<MTLBuffer> operations = nil;
  id<MTLBuffer> spawnPrefix = nil;
  id<MTLBuffer> counters = nil;
  id<MTLBuffer> sourceAllocationState = nil;
  id<MTLBuffer> indirectArguments = nil;
  AnityGraphicsVFXInitializeKernelDesc kernel{};
  uint8_t* particleRecords = nullptr;
  int32_t particleByteCount = 0;
  uint32_t* deadList = nullptr;
  int32_t* aliveCount = nullptr;
  int32_t* deadCount = nullptr;
  uint32_t* nextSequentialIndex = nullptr;
  int32_t* spawnedCount = nullptr;
  int32_t sourceAliveCount = 0;
  uint64_t sourceGeneration = 0;
  uint64_t targetGeneration = 0;
};

extern "C" AnityResult AnityGraphics_Metal_BeginVFXInitializeKernel(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* dispatch,
    const AnityGraphicsVFXInitializeKernelDesc* kernel,
    const AnityGraphicsVFXInitializeAttributeDesc* attributes,
    const AnityGraphicsVFXInitializeOperationDesc* operations,
    const uint32_t* spawnCountPrefix, int32_t spawnCountPrefixCount,
    int32_t spawnCandidateCount,
    const uint8_t* sourceRecords, int32_t sourceByteCount,
    uint8_t* particleRecords, int32_t particleByteCount,
    uint32_t* deadList, int32_t deadListCapacity, int32_t* inOutAliveCount,
    int32_t* inOutDeadCount, uint32_t* inOutNextSequentialIndex,
    int32_t* outSpawnedCount, uint64_t sourceGeneration,
    uint64_t targetGeneration, int32_t particleRecordsAuthoritative,
    int32_t retainSourceGeneration, void** outHandle) {
  if (!device || !device->backend || !dispatch || !kernel || !attributes ||
      kernel->attributeCount <= 0 || kernel->operationCount < 0 ||
      (kernel->operationCount > 0 && !operations) || !sourceRecords ||
      spawnCountPrefixCount < 0 || spawnCandidateCount < 0 ||
      (spawnCountPrefixCount > 0 && !spawnCountPrefix) ||
      (kernel->spawnCountSourceOffsetBytes >= 0 &&
       spawnCountPrefixCount != dispatch->recordCount) ||
      (kernel->spawnCountSourceOffsetBytes < 0 && spawnCountPrefixCount != 0) ||
      sourceByteCount <= 0 || !particleRecords || particleByteCount <= 0 ||
      deadListCapacity < 0 || !inOutAliveCount || !inOutDeadCount ||
      !inOutNextSequentialIndex || !outSpawnedCount ||
      *inOutAliveCount < 0 || *inOutAliveCount > kernel->particleCapacity ||
      *inOutDeadCount < 0 ||
      *inOutDeadCount > deadListCapacity ||
      (particleRecordsAuthoritative != 0 &&
       particleRecordsAuthoritative != 1) ||
      (retainSourceGeneration != 0 && retainSourceGeneration != 1) ||
      targetGeneration == 0 ||
      (particleRecordsAuthoritative == 0 && sourceGeneration == 0) ||
      ((kernel->flags & 1u) != 0u && *inOutDeadCount > 0 && !deadList) ||
      !outHandle)
    return ANITY_ERR_INVALID_ARG;
  *outHandle = nullptr;
  const int64_t requiredParticleBytes =
      static_cast<int64_t>(kernel->particleCapacity) * kernel->attributeStrideBytes;
  const int64_t requiredSourceBytes =
      (static_cast<int64_t>(dispatch->startEventIndex) + dispatch->recordCount) *
      kernel->sourceStrideBytes;
  if (requiredParticleBytes != particleByteCount ||
      requiredSourceBytes > sourceByteCount)
    return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
  id<MTLComputePipelineState> pipeline = GetVFXInitializeKernelPipeline(st);
  if (!st->device || !st->queue || !pipeline) return ANITY_ERR_NOT_SUPPORTED;
  std::lock_guard<std::mutex> computeLock(st->vfxComputeMutex);
  MetalVFXUpdateBuffers* residentBuffers = nullptr;
  MetalVFXUpdateSlot* initializeSlot = nullptr;
  uint32_t initializeRingIndex = 0;
  bool initializeSlotAcquired = false;
  if (particleRecordsAuthoritative == 0) {
    const MetalVFXParticleKey key{
        dispatch->effectId, dispatch->particleSystemId};
    auto resident = st->vfxUpdateBuffers.find(key);
    if (resident == st->vfxUpdateBuffers.end() ||
        resident->second.residentGeneration != sourceGeneration ||
        resident->second.allocationStateGeneration != sourceGeneration ||
        !resident->second.resident || !resident->second.allocationState ||
        !resident->second.residentDeadList)
      return ANITY_ERR_INVALID_ARG;
    residentBuffers = &resident->second;
    initializeRingIndex = residentBuffers->nextSlot % 3u;
    initializeSlot = &residentBuffers->slots[initializeRingIndex];
    if (!initializeSlot->available)
      initializeSlot->available = dispatch_semaphore_create(1);
    dispatch_semaphore_wait(
        initializeSlot->available, DISPATCH_TIME_FOREVER);
    initializeSlotAcquired = true;
    const auto failResidentSetup = [&](AnityResult result) {
      dispatch_semaphore_signal(initializeSlot->available);
      initializeSlotAcquired = false;
      return result;
    };
    if (retainSourceGeneration != 0) {
      try {
        residentBuffers->residentSnapshots.reserve(
            residentBuffers->residentSnapshots.size() + 1u);
      } catch (const std::bad_alloc&) {
        return failResidentSetup(ANITY_ERR_OUT_OF_MEMORY);
      }
    }
    AnityResult ensured = EnsureMetalOwnedBuffer(
        st, &initializeSlot->output, &initializeSlot->outputCapacity,
        particleByteCount);
    if (ensured != ANITY_OK) return failResidentSetup(ensured);
    ensured = EnsureMetalOwnedBuffer(
        st, &initializeSlot->allocationState,
        &initializeSlot->allocationStateCapacity,
        4 * static_cast<int32_t>(sizeof(uint32_t)));
    if (ensured != ANITY_OK) return failResidentSetup(ensured);
    ensured = EnsureMetalOwnedBuffer(
        st, &initializeSlot->residentDeadList,
        &initializeSlot->residentDeadListCapacity,
        kernel->particleCapacity * static_cast<int32_t>(sizeof(uint32_t)));
    if (ensured != ANITY_OK) return failResidentSetup(ensured);
    ++residentBuffers->residentInitializeCount;
    residentBuffers->residentInitializeReadbackAvoidedBytes +=
        static_cast<uint64_t>(particleByteCount);
  }
  int32_t sourceAliveCount = *inOutAliveCount;
  uint32_t threadCount = static_cast<uint32_t>(spawnCandidateCount);
  if (!residentBuffers) {
    const uint32_t available = (kernel->flags & 1u) != 0u
        ? static_cast<uint32_t>(*inOutDeadCount)
        : static_cast<uint32_t>(std::max(0,
            kernel->particleCapacity - static_cast<int32_t>(
                std::min<uint32_t>(*inOutNextSequentialIndex,
                                    static_cast<uint32_t>(kernel->particleCapacity)))));
    threadCount = std::min<uint32_t>(threadCount, available);
    auto existingBuffers = st->vfxUpdateBuffers.find(MetalVFXParticleKey{
        dispatch->effectId, dispatch->particleSystemId});
    if (existingBuffers != st->vfxUpdateBuffers.end())
      ++existingBuffers->second.initializeCpuDispatchSizingCount;
  }
  *outSpawnedCount = 0;
  if (!residentBuffers && threadCount == 0) return ANITY_OK;
  id<MTLBuffer> source = [st->device
      newBufferWithBytes:sourceRecords length:static_cast<NSUInteger>(sourceByteCount)
      options:MTLResourceStorageModeShared];
  id<MTLBuffer> particles = residentBuffers
      ? initializeSlot->output
      : [st->device
          newBufferWithBytes:particleRecords
          length:static_cast<NSUInteger>(particleByteCount)
          options:MTLResourceStorageModeShared];
  uint32_t deadListDummy = 0;
  id<MTLBuffer> dead = residentBuffers
      ? initializeSlot->residentDeadList
      : [st->device
          newBufferWithBytes:deadListCapacity > 0 ? deadList : &deadListDummy
          length:static_cast<NSUInteger>(std::max<int32_t>(1, deadListCapacity)) * sizeof(uint32_t)
          options:MTLResourceStorageModeShared];
  id<MTLBuffer> attributeBuffer = [st->device
      newBufferWithBytes:attributes
      length:static_cast<NSUInteger>(kernel->attributeCount) * sizeof(*attributes)
      options:MTLResourceStorageModeShared];
  AnityGraphicsVFXInitializeOperationDesc operationDummy{};
  id<MTLBuffer> operationBuffer = [st->device
      newBufferWithBytes:kernel->operationCount > 0 ? operations : &operationDummy
      length:static_cast<NSUInteger>(std::max(1, kernel->operationCount)) * sizeof(*operations)
      options:MTLResourceStorageModeShared];
  uint32_t spawnPrefixDummy = 0;
  id<MTLBuffer> spawnPrefixBuffer = [st->device
      newBufferWithBytes:spawnCountPrefixCount > 0 ? spawnCountPrefix : &spawnPrefixDummy
      length:static_cast<NSUInteger>(std::max(1, spawnCountPrefixCount)) * sizeof(uint32_t)
      options:MTLResourceStorageModeShared];
  uint32_t counterValues[4] = {
      static_cast<uint32_t>(*inOutAliveCount),
      static_cast<uint32_t>(*inOutDeadCount), *inOutNextSequentialIndex,
      (kernel->flags & 1u) != 0u ? 1u : 0u };
  id<MTLBuffer> counters = residentBuffers
      ? initializeSlot->allocationState
      : [st->device
          newBufferWithBytes:counterValues length:sizeof(counterValues)
          options:MTLResourceStorageModeShared];
  id<MTLBuffer> sourceAllocationState = residentBuffers
      ? [st->device
          newBufferWithLength:sizeof(counterValues)
          options:MTLResourceStorageModeShared]
      : counters;
  id<MTLBuffer> indirectArguments = residentBuffers
      ? [st->device
          newBufferWithLength:3u * sizeof(uint32_t)
          options:MTLResourceStorageModeShared]
      : nil;
  const auto releaseTransientBuffers = [&]() {
    [source release];
    [attributeBuffer release];
    [operationBuffer release];
    [spawnPrefixBuffer release];
    if (residentBuffers) {
      [sourceAllocationState release];
      [indirectArguments release];
    }
    if (!residentBuffers) {
      [particles release];
      [dead release];
      [counters release];
    }
    if (initializeSlotAcquired) {
      dispatch_semaphore_signal(initializeSlot->available);
      initializeSlotAcquired = false;
    }
  };
  if (!source || !particles || !dead || !attributeBuffer || !operationBuffer ||
      !spawnPrefixBuffer || !counters || !sourceAllocationState ||
      (residentBuffers && !indirectArguments)) {
    releaseTransientBuffers();
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  struct Params {
    uint32_t startEventIndex, recordCount, sourceStrideWords, attributeStrideWords;
    uint32_t capacity, flags, deadCountSnapshot, nextSequentialIndex;
    uint32_t attributeCount, operationCount, systemSeed;
    uint32_t sourceEventCount, spawnCandidateCount, residentAllocationState;
  } params = {
      static_cast<uint32_t>(dispatch->startEventIndex),
      static_cast<uint32_t>(dispatch->recordCount),
      static_cast<uint32_t>(kernel->sourceStrideBytes / 4),
      static_cast<uint32_t>(kernel->attributeStrideBytes / 4),
      static_cast<uint32_t>(kernel->particleCapacity), kernel->flags,
      static_cast<uint32_t>(*inOutDeadCount), *inOutNextSequentialIndex,
      static_cast<uint32_t>(kernel->attributeCount),
      static_cast<uint32_t>(kernel->operationCount), kernel->systemSeed,
      static_cast<uint32_t>(spawnCountPrefixCount),
      static_cast<uint32_t>(spawnCandidateCount), residentBuffers ? 1u : 0u };
  const NSUInteger width = std::min<NSUInteger>(
      pipeline.maxTotalThreadsPerThreadgroup, static_cast<NSUInteger>(64));
  if (width == 0) {
    releaseTransientBuffers();
    return ANITY_ERR_NOT_SUPPORTED;
  }
  id<MTLCommandBuffer> commandBuffer = [st->queue commandBuffer];
  if (!commandBuffer) {
    releaseTransientBuffers();
    return ANITY_ERR_DEVICE_LOST;
  }
  if (residentBuffers) {
    id<MTLBlitCommandEncoder> blit = [commandBuffer blitCommandEncoder];
    if (!blit) {
      releaseTransientBuffers();
      return ANITY_ERR_DEVICE_LOST;
    }
    [blit copyFromBuffer:residentBuffers->resident sourceOffset:0
        toBuffer:particles destinationOffset:0
        size:static_cast<NSUInteger>(particleByteCount)];
    [blit copyFromBuffer:residentBuffers->residentDeadList sourceOffset:0
        toBuffer:dead destinationOffset:0
        size:static_cast<NSUInteger>(kernel->particleCapacity) *
            sizeof(uint32_t)];
    [blit copyFromBuffer:residentBuffers->allocationState sourceOffset:0
        toBuffer:counters destinationOffset:0
        size:sizeof(counterValues)];
    [blit copyFromBuffer:residentBuffers->allocationState sourceOffset:0
        toBuffer:sourceAllocationState destinationOffset:0
        size:sizeof(counterValues)];
    [blit endEncoding];
    id<MTLComputeCommandEncoder> indirectEncoder =
        [commandBuffer computeCommandEncoder];
    if (!indirectEncoder || !st->vfxInitializeIndirectPipeline) {
      releaseTransientBuffers();
      return ANITY_ERR_NOT_SUPPORTED;
    }
    const uint32_t indirectConfig[4] = {
        static_cast<uint32_t>(kernel->particleCapacity), kernel->flags,
        static_cast<uint32_t>(spawnCandidateCount),
        static_cast<uint32_t>(width)};
    [indirectEncoder setComputePipelineState:st->vfxInitializeIndirectPipeline];
    [indirectEncoder setBuffer:sourceAllocationState offset:0 atIndex:0];
    [indirectEncoder setBuffer:indirectArguments offset:0 atIndex:1];
    [indirectEncoder setBytes:indirectConfig
        length:sizeof(indirectConfig) atIndex:2];
    [indirectEncoder dispatchThreads:MTLSizeMake(1, 1, 1)
        threadsPerThreadgroup:MTLSizeMake(1, 1, 1)];
    [indirectEncoder endEncoding];
  }
  id<MTLComputeCommandEncoder> encoder =
      [commandBuffer computeCommandEncoder];
  if (!encoder) {
    releaseTransientBuffers();
    return ANITY_ERR_DEVICE_LOST;
  }
  [encoder setComputePipelineState:pipeline];
  [encoder setBuffer:source offset:0 atIndex:0];
  [encoder setBuffer:particles offset:0 atIndex:1];
  [encoder setBuffer:dead offset:0 atIndex:2];
  [encoder setBuffer:attributeBuffer offset:0 atIndex:3];
  [encoder setBuffer:operationBuffer offset:0 atIndex:4];
  [encoder setBuffer:counters offset:0 atIndex:5];
  [encoder setBytes:&params length:sizeof(params) atIndex:6];
  [encoder setBuffer:spawnPrefixBuffer offset:0 atIndex:7];
  [encoder setBuffer:sourceAllocationState offset:0 atIndex:8];
  if (residentBuffers)
    [encoder dispatchThreadgroupsWithIndirectBuffer:indirectArguments
        indirectBufferOffset:0
        threadsPerThreadgroup:MTLSizeMake(width, 1, 1)];
  else
    [encoder dispatchThreads:MTLSizeMake(threadCount, 1, 1)
        threadsPerThreadgroup:MTLSizeMake(width, 1, 1)];
  [encoder endEncoding];
  [commandBuffer addCompletedHandler:^(id<MTLCommandBuffer> completedBuffer) {
    ObserveMetalDeviceLoss(st, completedBuffer);
  }];
  [commandBuffer commit];
  auto* handle = new (std::nothrow) MetalVFXInitializeHandle();
  if (!handle) {
    [commandBuffer waitUntilCompleted];
    releaseTransientBuffers();
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  handle->state = st;
  handle->key = MetalVFXParticleKey{
      dispatch->effectId, dispatch->particleSystemId};
  handle->ringIndex = initializeRingIndex;
  handle->resident = residentBuffers != nullptr;
  handle->slotAcquired = initializeSlotAcquired;
  handle->retainSourceGeneration = retainSourceGeneration != 0;
  if (st->failNextVFXDeviceRemovalCount > 0) {
    --st->failNextVFXDeviceRemovalCount;
    handle->injectedDeviceRemoval = true;
  } else if (st->failNextVFXInitializeCount > 0) {
    --st->failNextVFXInitializeCount;
    handle->injectedFailure = true;
  }
  handle->commandBuffer = [commandBuffer retain];
  handle->source = source;
  handle->particles = particles;
  handle->dead = dead;
  handle->attributes = attributeBuffer;
  handle->operations = operationBuffer;
  handle->spawnPrefix = spawnPrefixBuffer;
  handle->counters = counters;
  handle->sourceAllocationState = sourceAllocationState;
  handle->indirectArguments = indirectArguments;
  handle->kernel = *kernel;
  handle->particleRecords = particleRecords;
  handle->particleByteCount = particleByteCount;
  handle->deadList = deadList;
  handle->aliveCount = inOutAliveCount;
  handle->deadCount = inOutDeadCount;
  handle->nextSequentialIndex = inOutNextSequentialIndex;
  handle->spawnedCount = outSpawnedCount;
  handle->sourceAliveCount = sourceAliveCount;
  handle->sourceGeneration = sourceGeneration;
  handle->targetGeneration = targetGeneration;
  if (residentBuffers) ++residentBuffers->asynchronousInitializeBeginCount;
  initializeSlotAcquired = false;
  *outHandle = handle;
  return ANITY_OK;
}

static void ReleaseMetalVFXInitializeHandleLocked(
    MetalVFXInitializeHandle* handle) {
  if (!handle) return;
  [handle->source release];
  [handle->attributes release];
  [handle->operations release];
  [handle->spawnPrefix release];
  if (handle->resident) {
    [handle->sourceAllocationState release];
    [handle->indirectArguments release];
  } else {
    [handle->particles release];
    [handle->dead release];
    [handle->counters release];
  }
  [handle->commandBuffer release];
  if (handle->slotAcquired) {
    auto found = handle->state->vfxUpdateBuffers.find(handle->key);
    if (found != handle->state->vfxUpdateBuffers.end()) {
      MetalVFXUpdateSlot& slot = found->second.slots[handle->ringIndex];
      if (slot.available) dispatch_semaphore_signal(slot.available);
    }
    handle->slotAcquired = false;
  }
}

extern "C" AnityResult AnityGraphics_Metal_PollVFXInitializeKernel(
    void* opaqueHandle, int32_t* outState) {
  if (!opaqueHandle || !outState) return ANITY_ERR_INVALID_ARG;
  auto* handle = static_cast<MetalVFXInitializeHandle*>(opaqueHandle);
  std::lock_guard<std::mutex> lock(handle->state->vfxComputeMutex);
  if (handle->resident) {
    auto found = handle->state->vfxUpdateBuffers.find(handle->key);
    if (found != handle->state->vfxUpdateBuffers.end())
      ++found->second.asynchronousInitializePollCount;
  }
  if (handle->commandBuffer.status == MTLCommandBufferStatusError)
    ObserveMetalDeviceLoss(handle->state, handle->commandBuffer);
  if (handle->injectedDeviceRemoval)
    ObserveMetalDeviceLoss(handle->state, handle->commandBuffer, true);
  if (handle->injectedFailure || handle->injectedDeviceRemoval ||
      IsMetalDeviceLost(handle->state) ||
      handle->commandBuffer.status == MTLCommandBufferStatusError)
    *outState = 2;
  else if (handle->commandBuffer.status == MTLCommandBufferStatusCompleted)
    *outState = 1;
  else
    *outState = 0;
  return ANITY_OK;
}

static AnityResult PublishMetalVFXInitializeKernelLocked(
    MetalVFXInitializeHandle* handle, bool asynchronous) {
  if (!handle) return ANITY_ERR_INVALID_ARG;
  if (IsMetalDeviceLost(handle->state)) return ANITY_ERR_DEVICE_LOST;
  if (!handle->resident || handle->residentPublished) return ANITY_OK;
  auto found = handle->state->vfxUpdateBuffers.find(handle->key);
  if (found == handle->state->vfxUpdateBuffers.end())
    return ANITY_ERR_INVALID_ARG;
  MetalVFXUpdateBuffers& buffers = found->second;
  MetalVFXUpdateSlot& slot = buffers.slots[handle->ringIndex];
  if (!buffers.resident || !buffers.allocationState ||
      !buffers.residentDeadList || slot.output != handle->particles ||
      slot.allocationState != handle->counters ||
      slot.residentDeadList != handle->dead ||
      buffers.residentGeneration != handle->sourceGeneration ||
      buffers.allocationStateGeneration != handle->sourceGeneration)
    return ANITY_ERR_INVALID_ARG;

  std::swap(buffers.resident, slot.output);
  std::swap(buffers.residentCapacity, slot.outputCapacity);
  std::swap(buffers.allocationState, slot.allocationState);
  std::swap(buffers.allocationStateCapacity, slot.allocationStateCapacity);
  std::swap(buffers.residentDeadList, slot.residentDeadList);
  std::swap(buffers.residentDeadListCapacity,
            slot.residentDeadListCapacity);
  buffers.residentGeneration = handle->targetGeneration;
  buffers.allocationStateGeneration = handle->targetGeneration;
  if (handle->retainSourceGeneration) {
    auto existing = std::find_if(
        buffers.residentSnapshots.begin(), buffers.residentSnapshots.end(),
        [&](const auto& item) {
          return item.first == handle->sourceGeneration;
        });
    if (existing != buffers.residentSnapshots.end()) {
      ReleaseMetalVFXResidentSnapshot(&existing->second);
      buffers.residentSnapshots.erase(existing);
    }
    buffers.residentSnapshots.emplace_back(
        handle->sourceGeneration,
        MetalVFXResidentSnapshot{
            slot.output, slot.outputCapacity,
            slot.allocationState, slot.allocationStateCapacity,
            slot.residentDeadList, slot.residentDeadListCapacity});
    slot.output = nil;
    slot.outputCapacity = 0;
    slot.allocationState = nil;
    slot.allocationStateCapacity = 0;
    slot.residentDeadList = nil;
    slot.residentDeadListCapacity = 0;
    ++buffers.residentSnapshotCount;
  }
  buffers.boundsCacheValid = false;
  buffers.aliveCompactGeneration = 0;
  InvalidateMetalVFXPlanarSortCaches(buffers);
  buffers.nextSlot = (handle->ringIndex + 1u) % 3u;
  if (asynchronous) {
    ++buffers.asynchronousInitializeResidentPublishCount;
    buffers.inFlightInitializeGenerations.push_back(
        handle->targetGeneration);
    handle->asynchronousPublished = true;
  }
  handle->residentPublished = true;
  return ANITY_OK;
}

static bool RollbackPublishedMetalVFXInitializeLocked(
    MetalVFXInitializeHandle* handle) {
  if (!handle || !handle->residentPublished) return true;
  auto found = handle->state->vfxUpdateBuffers.find(handle->key);
  if (found == handle->state->vfxUpdateBuffers.end()) return false;
  MetalVFXUpdateBuffers& buffers = found->second;
  MetalVFXUpdateSlot& slot = buffers.slots[handle->ringIndex];
  if (buffers.residentGeneration != handle->targetGeneration ||
      buffers.allocationStateGeneration != handle->targetGeneration ||
      buffers.resident != handle->particles ||
      buffers.allocationState != handle->counters ||
      buffers.residentDeadList != handle->dead)
    return false;
  if (handle->retainSourceGeneration) {
    auto snapshot = std::find_if(
        buffers.residentSnapshots.begin(), buffers.residentSnapshots.end(),
        [&](const auto& item) {
          return item.first == handle->sourceGeneration;
        });
    if (snapshot == buffers.residentSnapshots.end()) return false;
    slot.output = buffers.resident;
    slot.outputCapacity = buffers.residentCapacity;
    slot.allocationState = buffers.allocationState;
    slot.allocationStateCapacity = buffers.allocationStateCapacity;
    slot.residentDeadList = buffers.residentDeadList;
    slot.residentDeadListCapacity = buffers.residentDeadListCapacity;
    buffers.resident = snapshot->second.buffer;
    buffers.residentCapacity = snapshot->second.capacity;
    buffers.allocationState = snapshot->second.allocationState;
    buffers.allocationStateCapacity =
        snapshot->second.allocationStateCapacity;
    buffers.residentDeadList = snapshot->second.deadList;
    buffers.residentDeadListCapacity = snapshot->second.deadListCapacity;
    buffers.residentSnapshots.erase(snapshot);
    ++buffers.residentRestoreCount;
  } else {
    if (!slot.output || !slot.allocationState || !slot.residentDeadList)
      return false;
    std::swap(buffers.resident, slot.output);
    std::swap(buffers.residentCapacity, slot.outputCapacity);
    std::swap(buffers.allocationState, slot.allocationState);
    std::swap(buffers.allocationStateCapacity, slot.allocationStateCapacity);
    std::swap(buffers.residentDeadList, slot.residentDeadList);
    std::swap(buffers.residentDeadListCapacity,
              slot.residentDeadListCapacity);
  }
  buffers.residentGeneration = handle->sourceGeneration;
  buffers.allocationStateGeneration = handle->sourceGeneration;
  auto inFlight = std::find(
      buffers.inFlightInitializeGenerations.begin(),
      buffers.inFlightInitializeGenerations.end(),
      handle->targetGeneration);
  if (inFlight != buffers.inFlightInitializeGenerations.end())
    buffers.inFlightInitializeGenerations.erase(inFlight);
  buffers.boundsCacheValid = false;
  buffers.aliveCompactGeneration = 0;
  InvalidateMetalVFXPlanarSortCaches(buffers);
  if (handle->asynchronousPublished)
    ++buffers.asynchronousInitializeResidentRollbackCount;
  handle->residentPublished = false;
  handle->asynchronousPublished = false;
  return true;
}

extern "C" AnityResult AnityGraphics_Metal_PublishVFXInitializeKernel(
    void* opaqueHandle) {
  if (!opaqueHandle) return ANITY_ERR_INVALID_ARG;
  auto* handle = static_cast<MetalVFXInitializeHandle*>(opaqueHandle);
  std::lock_guard<std::mutex> lock(handle->state->vfxComputeMutex);
  return PublishMetalVFXInitializeKernelLocked(handle, true);
}

extern "C" AnityResult AnityGraphics_Metal_CompleteVFXInitializeKernel(
    void* opaqueHandle) {
  if (!opaqueHandle) return ANITY_ERR_INVALID_ARG;
  auto* handle = static_cast<MetalVFXInitializeHandle*>(opaqueHandle);
  std::lock_guard<std::mutex> lock(handle->state->vfxComputeMutex);
  const auto fail = [&](AnityResult result) {
    if (handle->residentPublished &&
        !RollbackPublishedMetalVFXInitializeLocked(handle))
      result = ANITY_ERR_INTERNAL;
    ReleaseMetalVFXInitializeHandleLocked(handle);
    delete handle;
    return result;
  };
  [handle->commandBuffer waitUntilCompleted];
  ObserveMetalDeviceLoss(
      handle->state, handle->commandBuffer,
      handle->injectedDeviceRemoval);
  if (handle->injectedFailure || handle->injectedDeviceRemoval ||
      IsMetalDeviceLost(handle->state))
    return fail(ANITY_ERR_DEVICE_LOST);
  if (handle->commandBuffer.status != MTLCommandBufferStatusCompleted) {
    if (handle->commandBuffer.error)
      NSLog(@"Anity VFX Initialize command failed: %@",
            handle->commandBuffer.error);
    return fail(ANITY_ERR_DEVICE_LOST);
  }

  MetalVFXUpdateBuffers* residentBuffers = nullptr;
  MetalVFXUpdateSlot* initializeSlot = nullptr;
  if (handle->resident) {
    auto found = handle->state->vfxUpdateBuffers.find(handle->key);
    if (found == handle->state->vfxUpdateBuffers.end())
      return fail(ANITY_ERR_INTERNAL);
    residentBuffers = &found->second;
    initializeSlot = &residentBuffers->slots[handle->ringIndex];
    const uint64_t expectedGeneration = handle->residentPublished
        ? handle->targetGeneration : handle->sourceGeneration;
    if (handle->residentPublished) {
      const bool targetIsCurrent =
          residentBuffers->residentGeneration == expectedGeneration &&
          residentBuffers->allocationStateGeneration == expectedGeneration &&
          residentBuffers->resident == handle->particles &&
          residentBuffers->allocationState == handle->counters &&
          residentBuffers->residentDeadList == handle->dead;
      const auto targetSnapshot = std::find_if(
          residentBuffers->residentSnapshots.begin(),
          residentBuffers->residentSnapshots.end(),
          [&](const auto& item) {
            return item.first == expectedGeneration &&
                item.second.buffer == handle->particles &&
                item.second.allocationState == handle->counters &&
                item.second.deadList == handle->dead;
          });
      if (!targetIsCurrent &&
          targetSnapshot == residentBuffers->residentSnapshots.end())
        return fail(ANITY_ERR_INTERNAL);
    } else {
      if (residentBuffers->residentGeneration != expectedGeneration ||
          residentBuffers->allocationStateGeneration != expectedGeneration ||
          initializeSlot->output != handle->particles ||
          initializeSlot->allocationState != handle->counters ||
          initializeSlot->residentDeadList != handle->dead)
        return fail(ANITY_ERR_INTERNAL);
    }
  }

  if (!handle->resident)
    std::memcpy(handle->particleRecords, [handle->particles contents],
                static_cast<size_t>(handle->particleByteCount));
  const auto* sourceState = static_cast<const uint32_t*>(
      [handle->sourceAllocationState contents]);
  const auto* completedCounters = static_cast<const uint32_t*>(
      [handle->counters contents]);
  int32_t sourceAliveCount = handle->sourceAliveCount;
  if (handle->resident) {
    const uint32_t capacity =
        static_cast<uint32_t>(handle->kernel.particleCapacity);
    const uint32_t usesDeadList =
        (handle->kernel.flags & 1u) != 0u ? 1u : 0u;
    if (sourceState[0] > capacity || sourceState[1] > capacity ||
        sourceState[0] + sourceState[1] > capacity ||
        sourceState[3] != usesDeadList ||
        completedCounters[0] > capacity || completedCounters[1] > capacity ||
        completedCounters[0] + completedCounters[1] > capacity ||
        completedCounters[3] != usesDeadList ||
        completedCounters[0] < sourceState[0]) {
      return fail(ANITY_ERR_INTERNAL);
    }
    sourceAliveCount = static_cast<int32_t>(sourceState[0]);
    ++residentBuffers->residentInitializeAllocationStateReadCount;
    ++residentBuffers->residentInitializeIndirectDispatchCount;
    ++residentBuffers->residentInitializeIndirectPreparationCount;
    ++residentBuffers->residentInitializeSourceStateGpuCopyCount;
    ++residentBuffers->residentInitializeTargetCopyCount;
    residentBuffers->residentInitializeTargetCopyBytes +=
        static_cast<uint64_t>(handle->particleByteCount) +
        static_cast<uint64_t>(handle->kernel.particleCapacity) *
            sizeof(uint32_t) +
        4u * sizeof(uint32_t);
  }
  *handle->aliveCount = static_cast<int32_t>(completedCounters[0]);
  *handle->deadCount = static_cast<int32_t>(completedCounters[1]);
  *handle->nextSequentialIndex = completedCounters[2];
  *handle->spawnedCount = *handle->aliveCount - sourceAliveCount;
  if (!handle->resident && (handle->kernel.flags & 1u) != 0u &&
      *handle->deadCount > 0)
    std::memcpy(handle->deadList, [handle->dead contents],
                static_cast<size_t>(*handle->deadCount) * sizeof(uint32_t));

  if (handle->resident) {
    if (!handle->residentPublished) {
      AnityResult published =
          PublishMetalVFXInitializeKernelLocked(handle, false);
      if (published != ANITY_OK) return fail(published);
    }
    residentBuffers->residentInitializeSpawnCount +=
        static_cast<uint64_t>(*handle->spawnedCount);
    ++residentBuffers->asynchronousInitializeCompletionCount;
    ++residentBuffers->residentInitializeAtomicPublishCount;
    if (handle->asynchronousPublished) {
      auto inFlight = std::find(
          residentBuffers->inFlightInitializeGenerations.begin(),
          residentBuffers->inFlightInitializeGenerations.end(),
          handle->targetGeneration);
      if (inFlight == residentBuffers->inFlightInitializeGenerations.end())
        return fail(ANITY_ERR_INTERNAL);
      residentBuffers->inFlightInitializeGenerations.erase(inFlight);
      ++residentBuffers->asynchronousInitializeResidentCompletionCount;
    }
  }
  ReleaseMetalVFXInitializeHandleLocked(handle);
  delete handle;
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_CancelVFXInitializeKernel(
    void* opaqueHandle) {
  if (!opaqueHandle) return ANITY_ERR_INVALID_ARG;
  auto* handle = static_cast<MetalVFXInitializeHandle*>(opaqueHandle);
  std::lock_guard<std::mutex> lock(handle->state->vfxComputeMutex);
  [handle->commandBuffer waitUntilCompleted];
  bool failed =
      handle->commandBuffer.status != MTLCommandBufferStatusCompleted;
  if (handle->resident) {
    auto found = handle->state->vfxUpdateBuffers.find(handle->key);
    if (found != handle->state->vfxUpdateBuffers.end())
      ++found->second.asynchronousInitializeCancelCount;
  }
  if (handle->residentPublished &&
      !RollbackPublishedMetalVFXInitializeLocked(handle))
    failed = true;
  ReleaseMetalVFXInitializeHandleLocked(handle);
  delete handle;
  return failed ? ANITY_ERR_DEVICE_LOST : ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_DispatchVFXInitializeKernel(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* dispatch,
    const AnityGraphicsVFXInitializeKernelDesc* kernel,
    const AnityGraphicsVFXInitializeAttributeDesc* attributes,
    const AnityGraphicsVFXInitializeOperationDesc* operations,
    const uint32_t* spawnCountPrefix, int32_t spawnCountPrefixCount,
    int32_t spawnCandidateCount,
    const uint8_t* sourceRecords, int32_t sourceByteCount,
    uint8_t* particleRecords, int32_t particleByteCount,
    uint32_t* deadList, int32_t deadListCapacity, int32_t* inOutAliveCount,
    int32_t* inOutDeadCount, uint32_t* inOutNextSequentialIndex,
    int32_t* outSpawnedCount, uint64_t sourceGeneration,
    uint64_t targetGeneration, int32_t particleRecordsAuthoritative,
    int32_t retainSourceGeneration) {
  void* handle = nullptr;
  AnityResult result = AnityGraphics_Metal_BeginVFXInitializeKernel(
      device, dispatch, kernel, attributes, operations,
      spawnCountPrefix, spawnCountPrefixCount, spawnCandidateCount,
      sourceRecords, sourceByteCount, particleRecords, particleByteCount,
      deadList, deadListCapacity, inOutAliveCount, inOutDeadCount,
      inOutNextSequentialIndex, outSpawnedCount, sourceGeneration,
      targetGeneration, particleRecordsAuthoritative,
      retainSourceGeneration, &handle);
  if (result != ANITY_OK || !handle) return result;
  return AnityGraphics_Metal_CompleteVFXInitializeKernel(handle);
}

struct MetalVFXUpdateBatchTicket {
  MetalVFXParticleKey key{};
  uint32_t requestIndex = 0;
  uint32_t ringIndex = 0;
  id<MTLCommandBuffer> commandBuffer = nil;
  bool completionObserved = false;
  AnityGraphicsVFXUpdateKernelDesc kernel{};
  uint8_t* particleRecords = nullptr;
  int32_t particleByteCount = 0;
  uint64_t targetGeneration = 0;
  int32_t sourceAliveCount = 0;
  bool retainSourceGeneration = false;
  uint32_t* deadIndices = nullptr;
  int32_t* deadCount = nullptr;
  int32_t outputAliveCount = 0;
  bool hasBounds = false;
  AnityGraphicsVFXBoundsReductionDesc boundsDesc{};
  id<MTLBuffer> boundsResultBuffer = nil;
  uint64_t sourceGeneration = 0;
  bool residentPublished = false;
};

struct MetalVFXUpdateBatchHandle {
  MetalState* state = nullptr;
  int32_t batchWidth = 0;
  bool injectedFailure = false;
  bool injectedDeviceRemoval = false;
  std::vector<MetalVFXUpdateBatchTicket> tickets;
};

extern "C" AnityResult AnityGraphics_Metal_BeginVFXUpdateBatch(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXUpdateKernelDesc* kernels, int32_t kernelCount,
    const AnityGraphicsVFXUpdateOperationDesc* operations,
    uint8_t* const* particleRecords, const int32_t* particleByteCounts,
    const uint32_t* nextSequentialIndices, const int32_t* sourceAliveCounts,
    const uint32_t* const* sourceDeadLists, const int32_t* sourceDeadCounts,
    const int32_t* usesDeadLists,
    const int32_t* retainSourceGenerations,
    const uint64_t* sourceGenerations, const uint64_t* targetGenerations,
    uint32_t* const* outDeadIndices, const int32_t* deadIndexCapacities,
    int32_t* outDeadCounts,
    const AnityGraphicsVFXBoundsReductionDesc* boundsDescs,
    int32_t boundsCount, void** outHandle) {
  if (!device || !device->backend || !kernels || kernelCount <= 0 ||
      !operations || !particleRecords || !particleByteCounts ||
      !nextSequentialIndices || !sourceAliveCounts || !sourceDeadLists ||
      !sourceDeadCounts || !usesDeadLists || !retainSourceGenerations ||
      !sourceGenerations || !targetGenerations ||
      !outDeadIndices || !deadIndexCapacities || !outDeadCounts || !outHandle ||
      (boundsCount != 0 && (!boundsDescs || boundsCount != kernelCount)))
    return ANITY_ERR_INVALID_ARG;
  *outHandle = nullptr;
  for (int32_t index = 0; index < kernelCount; ++index) {
    const auto& kernel = kernels[index];
    const int64_t requiredParticleBytes =
        static_cast<int64_t>(kernel.particleCapacity) *
        kernel.attributeStrideBytes;
    const int64_t operationBytes =
        static_cast<int64_t>(kernel.operationCount) * sizeof(*operations);
    if (kernel.operationCount <= 0 || !particleRecords[index] ||
        requiredParticleBytes != particleByteCounts[index] ||
        sourceAliveCounts[index] < 0 ||
        sourceAliveCounts[index] > kernel.particleCapacity ||
        sourceDeadCounts[index] < 0 ||
        sourceDeadCounts[index] > kernel.particleCapacity ||
        (sourceDeadCounts[index] > 0 && !sourceDeadLists[index]) ||
        (usesDeadLists[index] != 0 && usesDeadLists[index] != 1) ||
        (usesDeadLists[index] == 0 && sourceDeadCounts[index] != 0) ||
        sourceAliveCounts[index] + sourceDeadCounts[index] >
            kernel.particleCapacity ||
        (retainSourceGenerations[index] != 0 &&
         retainSourceGenerations[index] != 1) ||
        sourceGenerations[index] == 0 || targetGenerations[index] == 0 ||
        !outDeadIndices[index] ||
        deadIndexCapacities[index] < kernel.particleCapacity ||
        operationBytes <= 0 ||
        operationBytes > std::numeric_limits<int32_t>::max())
      return ANITY_ERR_INVALID_ARG;
    const MetalVFXParticleKey key{kernel.effectId, kernel.particleSystemId};
    if (boundsCount != 0 && boundsDescs[index].effectId != 0 &&
        (boundsDescs[index].effectId != kernel.effectId ||
         boundsDescs[index].particleSystemId != kernel.particleSystemId ||
         boundsDescs[index].aliveOffsetBytes < 0))
      return ANITY_ERR_INVALID_ARG;
    for (int32_t previous = 0; previous < index; ++previous) {
      if (key == MetalVFXParticleKey{
              kernels[previous].effectId, kernels[previous].particleSystemId})
        return ANITY_ERR_INVALID_ARG;
    }
    outDeadCounts[index] = 0;
  }

  auto* st = reinterpret_cast<MetalState*>(device->backend);
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
  id<MTLComputePipelineState> pipeline = GetVFXUpdateKernelPipeline(st);
  if (!st->device || !st->queue || !pipeline) return ANITY_ERR_NOT_SUPPORTED;
  id<MTLComputePipelineState> deathPrefixPipeline = nil;
  id<MTLComputePipelineState> deathCompactPipeline = nil;
  id<MTLComputePipelineState> deathCommitPipeline = nil;
  if (!GetVFXDeathPipelines(
          st, &deathPrefixPipeline, &deathCompactPipeline,
          &deathCommitPipeline))
    return ANITY_ERR_NOT_SUPPORTED;
  bool hasPendingBounds = false;
  for (int32_t index = 0; index < boundsCount; ++index)
    hasPendingBounds = hasPendingBounds || boundsDescs[index].effectId != 0;
  id<MTLComputePipelineState> boundsMapPipeline = nil;
  id<MTLComputePipelineState> boundsReducePipeline = nil;
  if (hasPendingBounds && !GetVFXBoundsPipelines(
          st, &boundsMapPipeline, &boundsReducePipeline))
    return ANITY_ERR_NOT_SUPPORTED;
  const NSUInteger width = std::min<NSUInteger>(
      pipeline.maxTotalThreadsPerThreadgroup, static_cast<NSUInteger>(64));
  if (width == 0) return ANITY_ERR_NOT_SUPPORTED;

  struct Params {
    uint32_t strideWords;
    uint32_t capacity;
    uint32_t flags;
    int32_t aliveOffsetWords;
    int32_t seedOffsetWords;
    float deltaTime;
    uint32_t systemSeed;
    uint32_t sequentialLimit;
    uint32_t operationCount;
  };
  static_assert(sizeof(Params) == 36,
                "Metal VFX Update parameters layout changed");

  auto* handle = new (std::nothrow) MetalVFXUpdateBatchHandle();
  if (!handle) return ANITY_ERR_OUT_OF_MEMORY;
  handle->state = st;
  handle->batchWidth = kernelCount;
  try {
    handle->tickets.reserve(static_cast<size_t>(kernelCount));
  } catch (const std::bad_alloc&) {
    delete handle;
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  auto& tickets = handle->tickets;
  auto discardTickets = [&](bool invalidate) {
    for (auto& ticket : tickets) {
      auto found = st->vfxUpdateBuffers.find(ticket.key);
      if (found == st->vfxUpdateBuffers.end()) continue;
      MetalVFXUpdateSlot& slot = found->second.slots[ticket.ringIndex];
      if (!ticket.completionObserved) {
        dispatch_semaphore_wait(slot.completed, DISPATCH_TIME_FOREVER);
        ticket.completionObserved = true;
        ++found->second.completionCount;
      }
      dispatch_semaphore_signal(slot.available);
    }
    if (invalidate) {
      for (const auto& ticket : tickets) {
        auto found = st->vfxUpdateBuffers.find(ticket.key);
        if (found == st->vfxUpdateBuffers.end()) continue;
        ReleaseMetalVFXUpdateBuffers(&found->second);
        st->vfxUpdateBuffers.erase(found);
      }
    }
  };

  for (int32_t index = 0; index < kernelCount; ++index) {
    const auto& kernel = kernels[index];
    const MetalVFXParticleKey key{kernel.effectId, kernel.particleSystemId};
    MetalVFXUpdateBuffers& buffers = st->vfxUpdateBuffers[key];
    const uint32_t ringIndex = buffers.nextSlot % 3u;
    MetalVFXUpdateSlot& slot = buffers.slots[ringIndex];
    if (!slot.available) slot.available = dispatch_semaphore_create(1);
    if (!slot.completed) slot.completed = dispatch_semaphore_create(0);
    dispatch_semaphore_wait(slot.available, DISPATCH_TIME_FOREVER);
    auto failCurrent = [&](AnityResult result) {
      dispatch_semaphore_signal(slot.available);
      discardTickets(true);
      auto invalid = st->vfxUpdateBuffers.find(key);
      if (invalid != st->vfxUpdateBuffers.end()) {
        ReleaseMetalVFXUpdateBuffers(&invalid->second);
        st->vfxUpdateBuffers.erase(invalid);
      }
      delete handle;
      return result;
    };

    if (retainSourceGenerations[index] != 0) {
      try {
        buffers.residentSnapshots.reserve(
            buffers.residentSnapshots.size() + 1u);
      } catch (const std::bad_alloc&) {
        return failCurrent(ANITY_ERR_OUT_OF_MEMORY);
      }
    }
    const int32_t particleByteCount = particleByteCounts[index];
    if (buffers.residentGeneration != sourceGenerations[index]) {
      auto snapshot = std::find_if(
          buffers.residentSnapshots.begin(), buffers.residentSnapshots.end(),
          [&](const auto& item) {
            return item.first == sourceGenerations[index];
          });
      if (snapshot != buffers.residentSnapshots.end()) {
        buffers.resident = snapshot->second.buffer;
        buffers.residentCapacity = snapshot->second.capacity;
        buffers.allocationState = snapshot->second.allocationState;
        buffers.allocationStateCapacity =
            snapshot->second.allocationStateCapacity;
        buffers.residentDeadList = snapshot->second.deadList;
        buffers.residentDeadListCapacity =
            snapshot->second.deadListCapacity;
        buffers.residentGeneration = sourceGenerations[index];
        buffers.allocationStateGeneration = sourceGenerations[index];
        buffers.residentSnapshots.erase(snapshot);
        ++buffers.residentRestoreCount;
        buffers.boundsCacheValid = false;
      }
    }
    const bool requiresParticleUpload =
        !buffers.resident ||
        buffers.residentCapacity < static_cast<NSUInteger>(particleByteCount) ||
        buffers.residentGeneration != sourceGenerations[index];
    AnityResult upload = EnsureMetalBuffer(
        st, &buffers.resident, &buffers.residentCapacity, particleByteCount);
    if (upload != ANITY_OK) return failCurrent(upload);
    if (requiresParticleUpload) {
      std::memcpy([buffers.resident contents], particleRecords[index],
                  static_cast<size_t>(particleByteCount));
      ++buffers.particleUploadCount;
      buffers.residentGeneration = sourceGenerations[index];
    }
    const int32_t allocationStateBytes =
        static_cast<int32_t>(4u * sizeof(uint32_t));
    const int32_t persistentDeadListBytes = static_cast<int32_t>(
        static_cast<int64_t>(kernel.particleCapacity) * sizeof(uint32_t));
    const bool requiresAllocationUpload =
        !buffers.allocationState || !buffers.residentDeadList ||
        buffers.allocationStateCapacity <
            static_cast<NSUInteger>(allocationStateBytes) ||
        buffers.residentDeadListCapacity <
            static_cast<NSUInteger>(persistentDeadListBytes) ||
        buffers.allocationStateGeneration != sourceGenerations[index];
    upload = EnsureMetalBuffer(st, &buffers.allocationState,
        &buffers.allocationStateCapacity, allocationStateBytes);
    if (upload != ANITY_OK) return failCurrent(upload);
    upload = EnsureMetalBuffer(st, &buffers.residentDeadList,
        &buffers.residentDeadListCapacity, persistentDeadListBytes);
    if (upload != ANITY_OK) return failCurrent(upload);
    if (requiresAllocationUpload) {
      const uint32_t allocationState[4] = {
          static_cast<uint32_t>(sourceAliveCounts[index]),
          static_cast<uint32_t>(sourceDeadCounts[index]),
          std::min<uint32_t>(nextSequentialIndices[index],
              static_cast<uint32_t>(kernel.particleCapacity)),
          static_cast<uint32_t>(usesDeadLists[index])};
      std::memcpy([buffers.allocationState contents], allocationState,
                  sizeof(allocationState));
      if (sourceDeadCounts[index] > 0)
        std::memcpy([buffers.residentDeadList contents],
                    sourceDeadLists[index],
                    static_cast<size_t>(sourceDeadCounts[index]) *
                        sizeof(uint32_t));
      buffers.allocationStateGeneration = sourceGenerations[index];
      ++buffers.allocationStateUploadCount;
    } else {
      ++buffers.allocationStateResidentHitCount;
    }
    upload = EnsureMetalBuffer(
        st, &slot.output, &slot.outputCapacity, particleByteCount);
    if (upload != ANITY_OK) return failCurrent(upload);
    upload = EnsureMetalBuffer(st, &slot.allocationState,
        &slot.allocationStateCapacity, allocationStateBytes);
    if (upload != ANITY_OK) return failCurrent(upload);
    upload = EnsureMetalBuffer(st, &slot.residentDeadList,
        &slot.residentDeadListCapacity, persistentDeadListBytes);
    if (upload != ANITY_OK) return failCurrent(upload);
    const int32_t particleIndexBytes = static_cast<int32_t>(
        static_cast<int64_t>(kernel.particleCapacity) * sizeof(uint32_t));
    upload = EnsureMetalBuffer(st, &slot.deathPrefix,
        &slot.deathPrefixCapacity, particleIndexBytes);
    if (upload != ANITY_OK) return failCurrent(upload);
    upload = EnsureMetalBuffer(st, &slot.deathScratch,
        &slot.deathScratchCapacity, particleIndexBytes);
    if (upload != ANITY_OK) return failCurrent(upload);
    upload = EnsureMetalBuffer(st, &slot.deadIndices,
        &slot.deadIndexCapacity, particleIndexBytes);
    if (upload != ANITY_OK) return failCurrent(upload);
    upload = EnsureMetalBuffer(st, &slot.deadCount,
        &slot.deadCountCapacity, static_cast<int32_t>(sizeof(uint32_t)));
    if (upload != ANITY_OK) return failCurrent(upload);
    const int32_t operationBytes = static_cast<int32_t>(
        static_cast<int64_t>(kernel.operationCount) * sizeof(*operations));
    upload = UploadMetalBuffer(
        st, &slot.operations, &slot.operationCapacity,
        operations + kernel.operationStart, operationBytes);
    if (upload != ANITY_OK) return failCurrent(upload);
    ++buffers.operationUploadCount;

    Params params{
        static_cast<uint32_t>(kernel.attributeStrideBytes / 4),
        static_cast<uint32_t>(kernel.particleCapacity),
        kernel.flags,
        kernel.aliveOffsetBytes < 0 ? -1 : kernel.aliveOffsetBytes / 4,
        kernel.seedOffsetBytes < 0 ? -1 : kernel.seedOffsetBytes / 4,
        kernel.deltaTime,
        kernel.systemSeed,
        std::min<uint32_t>(nextSequentialIndices[index],
                           static_cast<uint32_t>(kernel.particleCapacity)),
        static_cast<uint32_t>(kernel.operationCount)};
    id<MTLCommandBuffer> commandBuffer = [st->queue commandBuffer];
    id<MTLBlitCommandEncoder> blit = [commandBuffer blitCommandEncoder];
    if (!commandBuffer || !blit) return failCurrent(ANITY_ERR_DEVICE_LOST);
    [blit copyFromBuffer:buffers.resident sourceOffset:0
        toBuffer:slot.output destinationOffset:0
        size:static_cast<NSUInteger>(particleByteCount)];
    [blit copyFromBuffer:buffers.allocationState sourceOffset:0
        toBuffer:slot.allocationState destinationOffset:0
        size:static_cast<NSUInteger>(allocationStateBytes)];
    [blit copyFromBuffer:buffers.residentDeadList sourceOffset:0
        toBuffer:slot.residentDeadList destinationOffset:0
        size:static_cast<NSUInteger>(persistentDeadListBytes)];
    [blit endEncoding];
    id<MTLComputeCommandEncoder> encoder =
        [commandBuffer computeCommandEncoder];
    if (!encoder) return failCurrent(ANITY_ERR_DEVICE_LOST);
    [encoder setComputePipelineState:pipeline];
    [encoder setBuffer:buffers.resident offset:0 atIndex:0];
    [encoder setBuffer:slot.output offset:0 atIndex:1];
    [encoder setBuffer:slot.operations offset:0 atIndex:2];
    [encoder setBytes:&params length:sizeof(params) atIndex:3];
    [encoder setBuffer:slot.deathPrefix offset:0 atIndex:4];
    [encoder setBuffer:buffers.allocationState offset:0 atIndex:5];
    [encoder dispatchThreads:MTLSizeMake(
        static_cast<NSUInteger>(kernel.particleCapacity), 1, 1)
        threadsPerThreadgroup:MTLSizeMake(width, 1, 1)];
    [encoder endEncoding];
    id<MTLBuffer> finalDeathPrefix = nil;
    uint64_t prefixPasses = 0;
    AnityResult deathResult = EncodeMetalVFXDeathCompaction(
        st, commandBuffer, &slot,
        static_cast<uint32_t>(kernel.particleCapacity),
        deathPrefixPipeline, deathCompactPipeline,
        &finalDeathPrefix, &prefixPasses);
    if (deathResult != ANITY_OK) return failCurrent(deathResult);
    if (!finalDeathPrefix) return failCurrent(ANITY_ERR_INTERNAL);
    buffers.deadPrefixPassCount += prefixPasses;
    ++buffers.deadCompactionDispatchCount;
    id<MTLComputeCommandEncoder> deathCommitEncoder =
        [commandBuffer computeCommandEncoder];
    if (!deathCommitEncoder) return failCurrent(ANITY_ERR_DEVICE_LOST);
    [deathCommitEncoder setComputePipelineState:deathCommitPipeline];
    [deathCommitEncoder setBuffer:slot.deadIndices offset:0 atIndex:0];
    [deathCommitEncoder setBuffer:slot.deadCount offset:0 atIndex:1];
    [deathCommitEncoder setBuffer:slot.residentDeadList offset:0 atIndex:2];
    [deathCommitEncoder setBuffer:buffers.allocationState offset:0 atIndex:3];
    [deathCommitEncoder setBuffer:slot.allocationState offset:0 atIndex:4];
    const uint32_t allocationCapacity =
        static_cast<uint32_t>(kernel.particleCapacity);
    [deathCommitEncoder setBytes:&allocationCapacity
        length:sizeof(allocationCapacity) atIndex:5];
    const NSUInteger deathCommitWidth = std::min<NSUInteger>(
        deathCommitPipeline.maxTotalThreadsPerThreadgroup,
        static_cast<NSUInteger>(64));
    if (deathCommitWidth == 0) {
      [deathCommitEncoder endEncoding];
      return failCurrent(ANITY_ERR_NOT_SUPPORTED);
    }
    [deathCommitEncoder dispatchThreads:MTLSizeMake(
        static_cast<NSUInteger>(kernel.particleCapacity), 1, 1)
        threadsPerThreadgroup:MTLSizeMake(deathCommitWidth, 1, 1)];
    [deathCommitEncoder endEncoding];
    ++buffers.allocationStateGpuCopyCount;
    const bool encodeBounds = boundsCount != 0 &&
        boundsDescs[index].effectId != 0;
    id<MTLBuffer> boundsResultBuffer = nil;
    if (encodeBounds) {
      AnityResult boundsResult = EncodeMetalVFXBoundsReduction(
          st, commandBuffer, slot.output, boundsDescs[index],
          kernel.particleCapacity, kernel.attributeStrideBytes,
          nextSequentialIndices[index], boundsMapPipeline,
          boundsReducePipeline, &boundsResultBuffer);
      if (boundsResult != ANITY_OK) return failCurrent(boundsResult);
      ++buffers.boundsDispatchCount;
      ++buffers.boundsPendingDispatchCount;
    }
    dispatch_semaphore_t completed = slot.completed;
    [commandBuffer addCompletedHandler:^(id<MTLCommandBuffer> completedBuffer) {
      ObserveMetalDeviceLoss(st, completedBuffer);
      dispatch_semaphore_signal(completed);
    }];
    ++buffers.dispatchCount;
    ++buffers.gpuCopyCount;
    buffers.nextSlot = (ringIndex + 1u) % 3u;
    MetalVFXUpdateBatchTicket ticket{};
    ticket.key = key;
    ticket.requestIndex = static_cast<uint32_t>(index);
    ticket.ringIndex = ringIndex;
    ticket.commandBuffer = commandBuffer;
    ticket.kernel = kernel;
    ticket.particleRecords = particleRecords[index];
    ticket.particleByteCount = particleByteCount;
    ticket.sourceGeneration = sourceGenerations[index];
    ticket.targetGeneration = targetGenerations[index];
    ticket.sourceAliveCount = sourceAliveCounts[index];
    ticket.retainSourceGeneration = retainSourceGenerations[index] != 0;
    ticket.deadIndices = outDeadIndices[index];
    ticket.deadCount = &outDeadCounts[index];
    ticket.hasBounds = encodeBounds;
    if (encodeBounds) {
      ticket.boundsDesc = boundsDescs[index];
      ticket.boundsResultBuffer = boundsResultBuffer;
    }
    tickets.push_back(ticket);
    [commandBuffer commit];
  }
  if (st->failNextVFXDeviceRemovalCount > 0) {
    --st->failNextVFXDeviceRemovalCount;
    handle->injectedDeviceRemoval = true;
  } else if (st->failNextVFXUpdateCount > 0) {
    --st->failNextVFXUpdateCount;
    handle->injectedFailure = true;
  }
  *outHandle = handle;
  return ANITY_OK;
}

static AnityResult PublishMetalVFXUpdateBatchLocked(
    MetalVFXUpdateBatchHandle* handle, bool asynchronous) {
  if (!handle) return ANITY_ERR_INVALID_ARG;
  if (IsMetalDeviceLost(handle->state)) return ANITY_ERR_DEVICE_LOST;
  bool anyPublished = false;
  bool allPublished = true;
  for (const auto& ticket : handle->tickets) {
    anyPublished = anyPublished || ticket.residentPublished;
    allPublished = allPublished && ticket.residentPublished;
  }
  if (allPublished) return ANITY_OK;
  if (anyPublished) return ANITY_ERR_INTERNAL;
  for (const auto& ticket : handle->tickets) {
    auto found = handle->state->vfxUpdateBuffers.find(ticket.key);
    if (found == handle->state->vfxUpdateBuffers.end())
      return ANITY_ERR_INVALID_ARG;
    const MetalVFXUpdateBuffers& buffers = found->second;
    const MetalVFXUpdateSlot& slot = buffers.slots[ticket.ringIndex];
    if (!buffers.resident || !slot.output || !buffers.allocationState ||
        !slot.allocationState || !buffers.residentDeadList ||
        !slot.residentDeadList ||
        buffers.residentGeneration != ticket.sourceGeneration ||
        buffers.allocationStateGeneration != ticket.sourceGeneration)
      return ANITY_ERR_INVALID_ARG;
  }
  for (auto& ticket : handle->tickets) {
    MetalVFXUpdateBuffers& buffers =
        handle->state->vfxUpdateBuffers.at(ticket.key);
    MetalVFXUpdateSlot& slot = buffers.slots[ticket.ringIndex];
    std::swap(buffers.resident, slot.output);
    std::swap(buffers.residentCapacity, slot.outputCapacity);
    std::swap(buffers.allocationState, slot.allocationState);
    std::swap(buffers.allocationStateCapacity,
              slot.allocationStateCapacity);
    std::swap(buffers.residentDeadList, slot.residentDeadList);
    std::swap(buffers.residentDeadListCapacity,
              slot.residentDeadListCapacity);
    buffers.residentGeneration = ticket.targetGeneration;
    buffers.allocationStateGeneration = ticket.targetGeneration;
    if (ticket.retainSourceGeneration) {
      auto existing = std::find_if(
          buffers.residentSnapshots.begin(), buffers.residentSnapshots.end(),
          [&](const auto& item) {
            return item.first == ticket.sourceGeneration;
          });
      if (existing != buffers.residentSnapshots.end()) {
        ReleaseMetalVFXResidentSnapshot(&existing->second);
        buffers.residentSnapshots.erase(existing);
      }
      buffers.residentSnapshots.emplace_back(
          ticket.sourceGeneration,
          MetalVFXResidentSnapshot{
              slot.output, slot.outputCapacity,
              slot.allocationState, slot.allocationStateCapacity,
              slot.residentDeadList, slot.residentDeadListCapacity});
      slot.output = nil;
      slot.outputCapacity = 0;
      slot.allocationState = nil;
      slot.allocationStateCapacity = 0;
      slot.residentDeadList = nil;
      slot.residentDeadListCapacity = 0;
      ++buffers.residentSnapshotCount;
    }
    ++buffers.residentOnlyPublishCount;
    if (asynchronous) {
      ++buffers.asynchronousResidentPublishCount;
      buffers.inFlightGenerations.push_back(ticket.targetGeneration);
    }
    buffers.boundsCacheValid = false;
    buffers.aliveCompactGeneration = 0;
    InvalidateMetalVFXPlanarSortCaches(buffers);
    ticket.residentPublished = true;
  }
  return ANITY_OK;
}

static bool RollbackPublishedMetalVFXUpdateLocked(
    MetalVFXUpdateBatchHandle* handle,
    MetalVFXUpdateBatchTicket* ticket) {
  if (!handle || !ticket || !ticket->residentPublished) return true;
  auto found = handle->state->vfxUpdateBuffers.find(ticket->key);
  if (found == handle->state->vfxUpdateBuffers.end()) return false;
  MetalVFXUpdateBuffers& buffers = found->second;
  MetalVFXUpdateSlot& slot = buffers.slots[ticket->ringIndex];
  if (ticket->retainSourceGeneration) {
    auto snapshot = std::find_if(
        buffers.residentSnapshots.begin(), buffers.residentSnapshots.end(),
        [&](const auto& item) {
          return item.first == ticket->sourceGeneration;
        });
    if (snapshot == buffers.residentSnapshots.end()) return false;
    slot.output = buffers.resident;
    slot.outputCapacity = buffers.residentCapacity;
    slot.allocationState = buffers.allocationState;
    slot.allocationStateCapacity = buffers.allocationStateCapacity;
    slot.residentDeadList = buffers.residentDeadList;
    slot.residentDeadListCapacity = buffers.residentDeadListCapacity;
    buffers.resident = snapshot->second.buffer;
    buffers.residentCapacity = snapshot->second.capacity;
    buffers.allocationState = snapshot->second.allocationState;
    buffers.allocationStateCapacity =
        snapshot->second.allocationStateCapacity;
    buffers.residentDeadList = snapshot->second.deadList;
    buffers.residentDeadListCapacity = snapshot->second.deadListCapacity;
    buffers.residentSnapshots.erase(snapshot);
    ++buffers.residentRestoreCount;
  } else {
    if (!slot.output) return false;
    std::swap(buffers.resident, slot.output);
    std::swap(buffers.residentCapacity, slot.outputCapacity);
    std::swap(buffers.allocationState, slot.allocationState);
    std::swap(buffers.allocationStateCapacity,
              slot.allocationStateCapacity);
    std::swap(buffers.residentDeadList, slot.residentDeadList);
    std::swap(buffers.residentDeadListCapacity,
              slot.residentDeadListCapacity);
  }
  buffers.residentGeneration = ticket->sourceGeneration;
  buffers.allocationStateGeneration = ticket->sourceGeneration;
  auto inFlight = std::find(
      buffers.inFlightGenerations.begin(), buffers.inFlightGenerations.end(),
      ticket->targetGeneration);
  if (inFlight != buffers.inFlightGenerations.end())
    buffers.inFlightGenerations.erase(inFlight);
  buffers.boundsCacheValid = false;
  buffers.aliveCompactGeneration = 0;
  InvalidateMetalVFXPlanarSortCaches(buffers);
  ++buffers.asynchronousResidentRollbackCount;
  ticket->residentPublished = false;
  return true;
}

extern "C" AnityResult AnityGraphics_Metal_PublishVFXUpdateBatch(
    void* opaqueHandle) {
  if (!opaqueHandle) return ANITY_ERR_INVALID_ARG;
  auto* handle = static_cast<MetalVFXUpdateBatchHandle*>(opaqueHandle);
  std::lock_guard<std::mutex> lock(handle->state->vfxComputeMutex);
  return PublishMetalVFXUpdateBatchLocked(handle, true);
}

static bool ObserveMetalVFXUpdateCompletionLocked(
    MetalVFXUpdateBatchHandle* handle,
    MetalVFXUpdateBatchTicket* ticket,
    bool wait) {
  if (!handle || !ticket) return false;
  auto found = handle->state->vfxUpdateBuffers.find(ticket->key);
  if (found == handle->state->vfxUpdateBuffers.end()) return false;
  MetalVFXUpdateSlot& slot = found->second.slots[ticket->ringIndex];
  if (!ticket->completionObserved) {
    if (!wait && ticket->commandBuffer.status != MTLCommandBufferStatusCompleted &&
        ticket->commandBuffer.status != MTLCommandBufferStatusError)
      return false;
    dispatch_semaphore_wait(slot.completed, DISPATCH_TIME_FOREVER);
    ticket->completionObserved = true;
    ++found->second.completionCount;
    if (ticket->hasBounds) ++found->second.boundsCompletionCount;
  }
  return true;
}

static void ReleaseMetalVFXUpdateBatchLocked(
    MetalVFXUpdateBatchHandle* handle, bool invalidate) {
  if (!handle) return;
  for (auto& ticket : handle->tickets) {
    auto found = handle->state->vfxUpdateBuffers.find(ticket.key);
    if (found == handle->state->vfxUpdateBuffers.end()) continue;
    ObserveMetalVFXUpdateCompletionLocked(handle, &ticket, true);
    if (ticket.hasBounds) ++found->second.boundsPendingDiscardCount;
    dispatch_semaphore_signal(
        found->second.slots[ticket.ringIndex].available);
  }
  if (invalidate) {
    for (const auto& ticket : handle->tickets) {
      auto found = handle->state->vfxUpdateBuffers.find(ticket.key);
      if (found == handle->state->vfxUpdateBuffers.end()) continue;
      ReleaseMetalVFXUpdateBuffers(&found->second);
      handle->state->vfxUpdateBuffers.erase(found);
    }
  }
}

extern "C" AnityResult AnityGraphics_Metal_PollVFXUpdateBatch(
    void* opaqueHandle, int32_t* outState) {
  if (!opaqueHandle || !outState) return ANITY_ERR_INVALID_ARG;
  auto* handle = static_cast<MetalVFXUpdateBatchHandle*>(opaqueHandle);
  std::lock_guard<std::mutex> lock(handle->state->vfxComputeMutex);
  if (handle->injectedDeviceRemoval)
    ObserveMetalDeviceLoss(handle->state, nil, true);
  if (handle->injectedFailure || handle->injectedDeviceRemoval ||
      IsMetalDeviceLost(handle->state)) {
    *outState = 2;
    return ANITY_OK;
  }
  bool ready = true;
  for (const auto& ticket : handle->tickets) {
    if (ticket.commandBuffer.status == MTLCommandBufferStatusError) {
      ObserveMetalDeviceLoss(handle->state, ticket.commandBuffer);
      *outState = 2;
      return ANITY_OK;
    }
    if (ticket.commandBuffer.status != MTLCommandBufferStatusCompleted)
      ready = false;
  }
  *outState = ready ? 1 : 0;
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_PollVFXUpdateBatchForPreparation(
    void* opaqueHandle, int32_t* outState) {
  if (!opaqueHandle || !outState) return ANITY_ERR_INVALID_ARG;
  auto* handle = static_cast<MetalVFXUpdateBatchHandle*>(opaqueHandle);
  std::lock_guard<std::mutex> lock(handle->state->vfxComputeMutex);
  bool ready = true;
  if (handle->injectedDeviceRemoval)
    ObserveMetalDeviceLoss(handle->state, nil, true);
  bool failed = handle->injectedFailure || handle->injectedDeviceRemoval ||
      IsMetalDeviceLost(handle->state);
  for (const auto& ticket : handle->tickets) {
    if (ticket.commandBuffer.status == MTLCommandBufferStatusError) {
      ObserveMetalDeviceLoss(handle->state, ticket.commandBuffer);
      failed = true;
    } else if (ticket.commandBuffer.status != MTLCommandBufferStatusCompleted)
      ready = false;
  }
  *outState = failed ? 2 : (ready ? 1 : 0);
  for (const auto& ticket : handle->tickets) {
    auto found = handle->state->vfxUpdateBuffers.find(ticket.key);
    if (found == handle->state->vfxUpdateBuffers.end())
      return ANITY_ERR_INTERNAL;
    ++found->second.preparationPollCount;
    if (*outState == 0)
      ++found->second.preparationDeferredCount;
    else
      ++found->second.preparationRetiredCount;
  }
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_CompleteVFXUpdateBatch(
    void* opaqueHandle) {
  if (!opaqueHandle) return ANITY_ERR_INVALID_ARG;
  auto* handle = static_cast<MetalVFXUpdateBatchHandle*>(opaqueHandle);
  std::lock_guard<std::mutex> lock(handle->state->vfxComputeMutex);
  bool failed = false;
  for (auto& ticket : handle->tickets) {
    auto found = handle->state->vfxUpdateBuffers.find(ticket.key);
    if (found != handle->state->vfxUpdateBuffers.end() &&
        ticket.commandBuffer.status != MTLCommandBufferStatusCompleted &&
        ticket.commandBuffer.status != MTLCommandBufferStatusError)
      ++found->second.completionWaitCount;
    if (!ObserveMetalVFXUpdateCompletionLocked(handle, &ticket, true)) {
      failed = true;
      continue;
    }
    if (ticket.commandBuffer.status != MTLCommandBufferStatusCompleted) {
      ObserveMetalDeviceLoss(handle->state, ticket.commandBuffer);
      if (ticket.commandBuffer.error)
        NSLog(@"Anity VFX Update command failed: %@",
              ticket.commandBuffer.error);
      failed = true;
    }
  }
  if (handle->injectedDeviceRemoval)
    ObserveMetalDeviceLoss(handle->state, nil, true);
  failed = failed || handle->injectedFailure ||
      handle->injectedDeviceRemoval || IsMetalDeviceLost(handle->state);
  if (failed) {
    bool rollbackFailed = false;
    for (auto& ticket : handle->tickets)
      if (!RollbackPublishedMetalVFXUpdateLocked(handle, &ticket))
        rollbackFailed = true;
    ReleaseMetalVFXUpdateBatchLocked(handle, rollbackFailed);
    delete handle;
    return rollbackFailed ? ANITY_ERR_INTERNAL : ANITY_ERR_DEVICE_LOST;
  }

  for (auto& ticket : handle->tickets) {
    MetalVFXUpdateBuffers& buffers =
        handle->state->vfxUpdateBuffers.at(ticket.key);
    MetalVFXUpdateSlot& slot = buffers.slots[ticket.ringIndex];
    const uint32_t compactedDeadCount =
        *static_cast<const uint32_t*>([slot.deadCount contents]);
    id<MTLBuffer> completedAllocationState = nil;
    if (!ticket.residentPublished) {
      completedAllocationState = slot.allocationState;
    } else if (buffers.allocationStateGeneration == ticket.targetGeneration) {
      completedAllocationState = buffers.allocationState;
    } else {
      auto completedSnapshot = std::find_if(
          buffers.residentSnapshots.begin(), buffers.residentSnapshots.end(),
          [&](const auto& item) {
            return item.first == ticket.targetGeneration;
          });
      if (completedSnapshot != buffers.residentSnapshots.end())
        completedAllocationState = completedSnapshot->second.allocationState;
    }
    if (!completedAllocationState) {
      ReleaseMetalVFXUpdateBatchLocked(handle, true);
      delete handle;
      return ANITY_ERR_INTERNAL;
    }
    const auto* allocationState =
        static_cast<const uint32_t*>([completedAllocationState contents]);
    const uint32_t outputAliveCount = allocationState[0];
    const uint32_t outputDeadCount = allocationState[1];
    if (compactedDeadCount >
            static_cast<uint32_t>(ticket.kernel.particleCapacity) ||
        outputAliveCount >
            static_cast<uint32_t>(ticket.kernel.particleCapacity) ||
        outputDeadCount >
            static_cast<uint32_t>(ticket.kernel.particleCapacity) ||
        outputAliveCount + outputDeadCount >
            static_cast<uint32_t>(ticket.kernel.particleCapacity)) {
      ReleaseMetalVFXUpdateBatchLocked(handle, true);
      delete handle;
      return ANITY_ERR_INTERNAL;
    }
    *ticket.deadCount = static_cast<int32_t>(compactedDeadCount);
    ticket.outputAliveCount = static_cast<int32_t>(outputAliveCount);
    if (compactedDeadCount > 0u)
      std::memcpy(ticket.deadIndices, [slot.deadIndices contents],
                  static_cast<size_t>(compactedDeadCount) * sizeof(uint32_t));
  }

  AnityResult publish = PublishMetalVFXUpdateBatchLocked(handle, false);
  if (publish != ANITY_OK) {
    for (auto& ticket : handle->tickets)
      RollbackPublishedMetalVFXUpdateLocked(handle, &ticket);
    ReleaseMetalVFXUpdateBatchLocked(handle, true);
    delete handle;
    return publish;
  }

  for (auto& ticket : handle->tickets) {
    MetalVFXUpdateBuffers& buffers =
        handle->state->vfxUpdateBuffers.at(ticket.key);
    MetalVFXUpdateSlot& slot = buffers.slots[ticket.ringIndex];
    buffers.boundsCacheValid = false;
    if (ticket.hasBounds && ticket.boundsResultBuffer) {
      const auto raw = *static_cast<const MetalVFXRawBoundsResult*>(
          [ticket.boundsResultBuffer contents]);
      buffers.boundsCacheDesc = ticket.boundsDesc;
      buffers.boundsCacheResult = BuildMetalVFXBoundsResult(
          ticket.boundsDesc, raw, ticket.outputAliveCount,
          ticket.targetGeneration);
      buffers.boundsCacheValid = true;
      ++buffers.boundsPendingPublishCount;
    }
    buffers.lastRingIndex = static_cast<int32_t>(ticket.ringIndex);
    buffers.lastBatchWidth = handle->batchWidth;
    buffers.peakBatchWidth =
        std::max(buffers.peakBatchWidth, handle->batchWidth);
    if (handle->batchWidth > 1) ++buffers.asyncBatchCount;
    auto inFlight = std::find(
        buffers.inFlightGenerations.begin(), buffers.inFlightGenerations.end(),
        ticket.targetGeneration);
    if (inFlight != buffers.inFlightGenerations.end()) {
      buffers.inFlightGenerations.erase(inFlight);
      ++buffers.asynchronousResidentCompletionCount;
    }
    dispatch_semaphore_signal(slot.available);
  }
  delete handle;
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_CancelVFXUpdateBatch(
    void* opaqueHandle) {
  if (!opaqueHandle) return ANITY_ERR_INVALID_ARG;
  auto* handle = static_cast<MetalVFXUpdateBatchHandle*>(opaqueHandle);
  std::lock_guard<std::mutex> lock(handle->state->vfxComputeMutex);
  bool failed = false;
  for (auto& ticket : handle->tickets) {
    if (!ObserveMetalVFXUpdateCompletionLocked(handle, &ticket, true) ||
        ticket.commandBuffer.status != MTLCommandBufferStatusCompleted)
      failed = true;
  }
  for (auto& ticket : handle->tickets)
    if (!RollbackPublishedMetalVFXUpdateLocked(handle, &ticket))
      failed = true;
  ReleaseMetalVFXUpdateBatchLocked(handle, failed);
  delete handle;
  return failed ? ANITY_ERR_DEVICE_LOST : ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_DispatchVFXUpdateBatch(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXUpdateKernelDesc* kernels, int32_t kernelCount,
    const AnityGraphicsVFXUpdateOperationDesc* operations,
    uint8_t* const* particleRecords, const int32_t* particleByteCounts,
    const uint32_t* nextSequentialIndices, const int32_t* sourceAliveCounts,
    const uint32_t* const* sourceDeadLists, const int32_t* sourceDeadCounts,
    const int32_t* usesDeadLists,
    const int32_t* retainSourceGenerations,
    const uint64_t* sourceGenerations, const uint64_t* targetGenerations,
    uint32_t* const* outDeadIndices, const int32_t* deadIndexCapacities,
    int32_t* outDeadCounts) {
  void* handle = nullptr;
  AnityResult result = AnityGraphics_Metal_BeginVFXUpdateBatch(
      device, kernels, kernelCount, operations, particleRecords,
      particleByteCounts, nextSequentialIndices, sourceAliveCounts,
      sourceDeadLists, sourceDeadCounts, usesDeadLists,
      retainSourceGenerations,
      sourceGenerations,
      targetGenerations, outDeadIndices, deadIndexCapacities,
      outDeadCounts, nullptr, 0, &handle);
  if (result != ANITY_OK) return result;
  return AnityGraphics_Metal_CompleteVFXUpdateBatch(handle);
}

extern "C" AnityResult AnityGraphics_Metal_GetVFXUpdateBackendStats(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId,
    AnityGraphicsVFXUpdateBackendStats* outStats) {
  if (!device || !device->backend || effectId == 0 ||
      particleSystemId == 0 || !outStats)
    return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  const MetalVFXParticleKey key{effectId, particleSystemId};
  auto found = st->vfxUpdateBuffers.find(key);
  if (found == st->vfxUpdateBuffers.end()) return ANITY_ERR_INVALID_ARG;
  const MetalVFXUpdateBuffers& buffers = found->second;
  NSUInteger operationCapacity = 0;
  for (const auto& slot : buffers.slots)
    operationCapacity = std::max(operationCapacity, slot.operationCapacity);
  *outStats = AnityGraphicsVFXUpdateBackendStats{
      effectId,
      particleSystemId,
      2,
      buffers.residentGeneration,
      buffers.dispatchCount,
      buffers.particleUploadCount,
      buffers.operationUploadCount,
      buffers.gpuCopyCount,
      buffers.completionCount,
      buffers.lastRingIndex,
      3,
      static_cast<uint64_t>(buffers.residentCapacity),
      static_cast<uint64_t>(operationCapacity),
      buffers.synchronousReadbackCount,
      buffers.lastBatchWidth,
      buffers.peakBatchWidth,
      buffers.asyncBatchCount,
      buffers.boundsDispatchCount,
      buffers.boundsResidentHitCount,
      buffers.boundsParticleUploadCount,
      buffers.boundsCompletionCount,
      buffers.boundsResultCacheHitCount,
      buffers.boundsPendingDispatchCount,
      buffers.boundsPendingPublishCount,
      buffers.boundsPendingDiscardCount,
      buffers.deadPrefixPassCount,
      buffers.deadCompactionDispatchCount,
      buffers.residentOnlyPublishCount,
      buffers.deferredParticleReadbackCount,
      buffers.deferredParticleReadbackBytes,
      buffers.residentSnapshotCount,
      buffers.residentRestoreCount,
      buffers.residentSnapshotDiscardCount,
      buffers.asynchronousResidentPublishCount,
      buffers.asynchronousResidentCompletionCount,
      buffers.asynchronousResidentRollbackCount,
      buffers.completionWaitCount,
      buffers.cameraDependencyCount,
      static_cast<uint64_t>(buffers.inFlightGenerations.size()),
      buffers.preparationPollCount,
      buffers.preparationDeferredCount,
      buffers.preparationRetiredCount,
      buffers.allocationStateGeneration,
      buffers.allocationStateUploadCount,
      buffers.allocationStateGpuCopyCount,
      buffers.allocationStateResidentHitCount,
      buffers.residentInitializeCount,
      buffers.residentInitializeSpawnCount,
      buffers.residentInitializeReadbackAvoidedBytes,
      buffers.residentInitializeAllocationStateReadCount,
      buffers.allocationStateReadbackCount,
      buffers.deadListReadbackCount,
      buffers.metadataReadbackBytes,
      buffers.metadataReadbackGeneration,
      buffers.residentInitializeIndirectDispatchCount,
      buffers.residentInitializeIndirectPreparationCount,
      buffers.residentInitializeSourceStateGpuCopyCount,
      buffers.initializeCpuDispatchSizingCount,
      buffers.residentInitializeTargetCopyCount,
      buffers.residentInitializeTargetCopyBytes,
      buffers.residentInitializeAtomicPublishCount,
      buffers.asynchronousInitializeBeginCount,
      buffers.asynchronousInitializePollCount,
      buffers.asynchronousInitializeCompletionCount,
      buffers.asynchronousInitializeCancelCount,
      buffers.asynchronousInitializeResidentPublishCount,
      buffers.asynchronousInitializeResidentCompletionCount,
      buffers.asynchronousInitializeResidentRollbackCount,
      static_cast<uint64_t>(buffers.inFlightInitializeGenerations.size())};
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_SetVFXFailureInjection(
    AnityGraphicsDevice* device, int32_t failurePoint,
    int32_t failureCount) {
  if (!device || !device->backend || failureCount < 0 || failureCount > 1024 ||
      (failurePoint != ANITY_GFX_VFX_FAILURE_INITIALIZE_COMMAND &&
       failurePoint != ANITY_GFX_VFX_FAILURE_UPDATE_COMMAND &&
       failurePoint != ANITY_GFX_VFX_FAILURE_PLANAR_CAMERA_COMMAND &&
       failurePoint != ANITY_GFX_VFX_FAILURE_DEVICE_REMOVAL))
    return ANITY_ERR_INVALID_ARG;
  auto* st = static_cast<MetalState*>(device->backend);
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  if (failurePoint == ANITY_GFX_VFX_FAILURE_INITIALIZE_COMMAND)
    st->failNextVFXInitializeCount = failureCount;
  else if (failurePoint == ANITY_GFX_VFX_FAILURE_UPDATE_COMMAND)
    st->failNextVFXUpdateCount = failureCount;
  else if (failurePoint == ANITY_GFX_VFX_FAILURE_PLANAR_CAMERA_COMMAND)
    st->failNextVFXPlanarCount = failureCount;
  else
    st->failNextVFXDeviceRemovalCount = failureCount;
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_ReadbackVFXResidentParticles(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId, uint64_t generation,
    uint8_t* records, int32_t recordByteCount) {
  if (!device || !device->backend || effectId == 0 ||
      particleSystemId == 0 || generation == 0 || !records ||
      recordByteCount <= 0)
    return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  const MetalVFXParticleKey key{effectId, particleSystemId};
  auto found = st->vfxUpdateBuffers.find(key);
  if (found == st->vfxUpdateBuffers.end()) return ANITY_ERR_INVALID_ARG;
  MetalVFXUpdateBuffers& buffers = found->second;
  if (!buffers.resident || buffers.residentGeneration != generation ||
      buffers.residentCapacity < static_cast<NSUInteger>(recordByteCount))
    return ANITY_ERR_INVALID_ARG;
  std::memcpy(records, [buffers.resident contents],
              static_cast<size_t>(recordByteCount));
  ++buffers.deferredParticleReadbackCount;
  buffers.deferredParticleReadbackBytes +=
      static_cast<uint64_t>(recordByteCount);
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_ReadbackVFXResidentMetadata(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId, uint64_t generation,
    uint32_t* allocationState, int32_t allocationStateCapacity,
    uint32_t* deadList, int32_t deadListCapacity, int32_t* outDeadCount) {
  if (!device || !device->backend || effectId == 0 ||
      particleSystemId == 0 || generation == 0 || !allocationState ||
      allocationStateCapacity < 4 || deadListCapacity < 0 || !outDeadCount)
    return ANITY_ERR_INVALID_ARG;
  *outDeadCount = 0;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  const MetalVFXParticleKey key{effectId, particleSystemId};
  auto found = st->vfxUpdateBuffers.find(key);
  if (found == st->vfxUpdateBuffers.end()) return ANITY_ERR_NOT_SUPPORTED;
  MetalVFXUpdateBuffers& buffers = found->second;
  if (!buffers.allocationState || !buffers.residentDeadList ||
      buffers.residentGeneration != generation ||
      buffers.allocationStateGeneration != generation ||
      buffers.allocationStateCapacity < 4u * sizeof(uint32_t))
    return ANITY_ERR_INVALID_ARG;
  const auto* state = static_cast<const uint32_t*>(
      [buffers.allocationState contents]);
  const uint32_t deadCount = state[1];
  if (state[0] > std::numeric_limits<int32_t>::max() ||
      deadCount > static_cast<uint32_t>(deadListCapacity) ||
      (deadCount > 0u && !deadList))
    return ANITY_ERR_INVALID_ARG;
  std::memcpy(allocationState, state, 4u * sizeof(uint32_t));
  if (deadCount > 0u)
    std::memcpy(deadList, [buffers.residentDeadList contents],
                static_cast<size_t>(deadCount) * sizeof(uint32_t));
  *outDeadCount = static_cast<int32_t>(deadCount);
  ++buffers.allocationStateReadbackCount;
  ++buffers.deadListReadbackCount;
  buffers.metadataReadbackBytes +=
      4u * sizeof(uint32_t) +
      static_cast<uint64_t>(deadCount) * sizeof(uint32_t);
  buffers.metadataReadbackGeneration = generation;
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_RestoreVFXResidentGeneration(
    AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId, uint64_t generation) {
  if (!device || !device->backend || effectId == 0 ||
      particleSystemId == 0 || generation == 0)
    return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  const MetalVFXParticleKey key{effectId, particleSystemId};
  auto found = st->vfxUpdateBuffers.find(key);
  if (found == st->vfxUpdateBuffers.end()) return ANITY_OK;
  MetalVFXUpdateBuffers& buffers = found->second;
  if (buffers.residentGeneration == generation) {
    buffers.residentSnapshotDiscardCount +=
        static_cast<uint64_t>(buffers.residentSnapshots.size());
    for (auto& item : buffers.residentSnapshots)
      ReleaseMetalVFXResidentSnapshot(&item.second);
    buffers.residentSnapshots.clear();
    return ANITY_OK;
  }
  auto snapshot = std::find_if(
      buffers.residentSnapshots.begin(), buffers.residentSnapshots.end(),
      [&](const auto& item) { return item.first == generation; });
  if (snapshot == buffers.residentSnapshots.end()) return ANITY_ERR_INTERNAL;
  const MetalVFXResidentSnapshot restored = snapshot->second;
  const size_t discarded = buffers.residentSnapshots.size() - 1u;
  ReleaseMetalVFXBuffer(&buffers.resident);
  ReleaseMetalVFXBuffer(&buffers.allocationState);
  ReleaseMetalVFXBuffer(&buffers.residentDeadList);
  for (auto& item : buffers.residentSnapshots) {
    if (&item == &*snapshot) continue;
    ReleaseMetalVFXResidentSnapshot(&item.second);
  }
  buffers.resident = restored.buffer;
  buffers.residentCapacity = restored.capacity;
  buffers.allocationState = restored.allocationState;
  buffers.allocationStateCapacity = restored.allocationStateCapacity;
  buffers.residentDeadList = restored.deadList;
  buffers.residentDeadListCapacity = restored.deadListCapacity;
  buffers.residentGeneration = generation;
  buffers.allocationStateGeneration = generation;
  buffers.residentSnapshots.clear();
  ++buffers.residentRestoreCount;
  buffers.residentSnapshotDiscardCount += static_cast<uint64_t>(discarded);
  buffers.boundsCacheValid = false;
  return ANITY_OK;
}

extern "C" void AnityGraphics_Metal_DiscardVFXResidentSnapshots(
    AnityGraphicsDevice* device, uint64_t effectId) {
  if (!device || !device->backend || effectId == 0) return;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  for (auto& item : st->vfxUpdateBuffers) {
    if (item.first.effectId != effectId) continue;
    item.second.residentSnapshotDiscardCount +=
        static_cast<uint64_t>(item.second.residentSnapshots.size());
    for (auto& snapshot : item.second.residentSnapshots)
      ReleaseMetalVFXResidentSnapshot(&snapshot.second);
    item.second.residentSnapshots.clear();
  }
}

extern "C" void AnityGraphics_Metal_DiscardVFXResidentSnapshot(
    AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId, uint64_t generation) {
  if (!device || !device->backend || effectId == 0 ||
      particleSystemId == 0 || generation == 0)
    return;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  auto found = st->vfxUpdateBuffers.find(
      MetalVFXParticleKey{effectId, particleSystemId});
  if (found == st->vfxUpdateBuffers.end()) return;
  auto& snapshots = found->second.residentSnapshots;
  auto snapshot = std::find_if(
      snapshots.begin(), snapshots.end(),
      [&](const auto& item) { return item.first == generation; });
  if (snapshot == snapshots.end()) return;
  ReleaseMetalVFXResidentSnapshot(&snapshot->second);
  snapshots.erase(snapshot);
  ++found->second.residentSnapshotDiscardCount;
}

extern "C" void AnityGraphics_Metal_ClearVFXEffectResources(
    AnityGraphicsDevice* device, uint64_t effectId) {
  if (!device || !device->backend || effectId == 0) return;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  for (auto it = st->vfxUpdateBuffers.begin();
       it != st->vfxUpdateBuffers.end();) {
    if (it->first.effectId == effectId) {
      ReleaseMetalVFXUpdateBuffers(&it->second);
      it = st->vfxUpdateBuffers.erase(it);
    } else
      ++it;
  }
}

extern "C" AnityResult AnityGraphics_Metal_ReduceVFXParticleBounds(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXBoundsReductionDesc* desc,
    const uint8_t* particleRecords, int32_t particleByteCount,
    int32_t capacity, int32_t attributeStrideBytes, int32_t aliveCount,
    uint32_t nextSequentialIndex, uint64_t generation,
    int32_t particleRecordsAuthoritative,
    AnityGraphicsVFXBoundsReductionResult* outResult) {
  if (!device || !device->backend || !desc || !particleRecords ||
      particleByteCount <= 0 || capacity <= 0 || attributeStrideBytes <= 0 ||
      aliveCount < 0 || aliveCount > capacity || generation == 0 ||
      (particleRecordsAuthoritative != 0 && particleRecordsAuthoritative != 1) ||
      !outResult)
    return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
  if (aliveCount == 0) {
    outResult->valid = 0;
    outResult->backendKind = 2;
    return ANITY_OK;
  }
  const int64_t required =
      static_cast<int64_t>(capacity) * attributeStrideBytes;
  if (required != particleByteCount) return ANITY_ERR_INVALID_ARG;
  id<MTLComputePipelineState> mapPipeline = nil;
  id<MTLComputePipelineState> reducePipeline = nil;
  if (!st->device || !st->queue ||
      !GetVFXBoundsPipelines(st, &mapPipeline, &reducePipeline))
    return ANITY_ERR_NOT_SUPPORTED;
  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  const MetalVFXParticleKey key{desc->effectId, desc->particleSystemId};
  auto cache = st->vfxUpdateBuffers.find(key);
  MetalVFXUpdateBuffers* buffers = cache == st->vfxUpdateBuffers.end()
      ? nullptr : &cache->second;
  if (buffers && buffers->boundsCacheValid &&
      buffers->boundsCacheResult.generation == generation &&
      SameVFXBoundsDesc(buffers->boundsCacheDesc, *desc)) {
    *outResult = buffers->boundsCacheResult;
    ++buffers->boundsResultCacheHitCount;
    return ANITY_OK;
  }

  struct RawBoundsResult {
    float minX, minY, minZ, maxX, maxY, maxZ;
    uint32_t validCount, invalid;
  };
  static_assert(sizeof(RawBoundsResult) == 32,
                "Metal VFX bounds reduction result layout changed");
  struct MapParams {
    uint32_t strideWords, capacity;
    int32_t positionOffsetWords, aliveOffsetWords, sizeOffsetWords;
    int32_t scaleXOffsetWords, scaleYOffsetWords, scaleZOffsetWords;
    uint32_t expectedAliveCount, sequentialLimit;
  } params = {
      static_cast<uint32_t>(attributeStrideBytes / 4),
      static_cast<uint32_t>(capacity),
      desc->positionOffsetBytes / 4,
      desc->aliveOffsetBytes < 0 ? -1 : desc->aliveOffsetBytes / 4,
      desc->sizeOffsetBytes < 0 ? -1 : desc->sizeOffsetBytes / 4,
      desc->scaleXOffsetBytes < 0 ? -1 : desc->scaleXOffsetBytes / 4,
      desc->scaleYOffsetBytes < 0 ? -1 : desc->scaleYOffsetBytes / 4,
      desc->scaleZOffsetBytes < 0 ? -1 : desc->scaleZOffsetBytes / 4,
      static_cast<uint32_t>(aliveCount),
      std::min<uint32_t>(nextSequentialIndex, static_cast<uint32_t>(capacity))};
  static_assert(sizeof(MapParams) == 40,
                "Metal VFX bounds map parameters layout changed");

  id<MTLBuffer> particles = nil;
  if (buffers && buffers->resident &&
      buffers->residentGeneration == generation &&
      buffers->residentCapacity >= static_cast<NSUInteger>(particleByteCount)) {
    particles = buffers->resident;
    ++buffers->boundsResidentHitCount;
  } else {
    if (particleRecordsAuthoritative == 0) return ANITY_ERR_NOT_SUPPORTED;
    particles = [st->device
        newBufferWithBytes:particleRecords
        length:static_cast<NSUInteger>(particleByteCount)
        options:MTLResourceStorageModeShared];
    if (buffers) ++buffers->boundsParticleUploadCount;
  }
  id<MTLBuffer> current = [st->device
      newBufferWithLength:static_cast<NSUInteger>(capacity) * sizeof(RawBoundsResult)
      options:MTLResourceStorageModeShared];
  if (!particles || !current) return ANITY_ERR_OUT_OF_MEMORY;
  id<MTLCommandBuffer> commandBuffer = [st->queue commandBuffer];
  if (!commandBuffer) return ANITY_ERR_DEVICE_LOST;

  id<MTLComputeCommandEncoder> mapEncoder =
      [commandBuffer computeCommandEncoder];
  if (!mapEncoder) return ANITY_ERR_DEVICE_LOST;
  [mapEncoder setComputePipelineState:mapPipeline];
  [mapEncoder setBuffer:particles offset:0 atIndex:0];
  [mapEncoder setBuffer:current offset:0 atIndex:1];
  [mapEncoder setBytes:&params length:sizeof(params) atIndex:2];
  NSUInteger mapWidth = std::min<NSUInteger>(
      mapPipeline.maxTotalThreadsPerThreadgroup, static_cast<NSUInteger>(64));
  if (mapWidth == 0) return ANITY_ERR_NOT_SUPPORTED;
  [mapEncoder dispatchThreads:MTLSizeMake(static_cast<NSUInteger>(capacity), 1, 1)
      threadsPerThreadgroup:MTLSizeMake(mapWidth, 1, 1)];
  [mapEncoder endEncoding];

  uint32_t currentCount = static_cast<uint32_t>(capacity);
  while (currentCount > 1u) {
    uint32_t outputCount = (currentCount + 1u) / 2u;
    id<MTLBuffer> next = [st->device
        newBufferWithLength:static_cast<NSUInteger>(outputCount) * sizeof(RawBoundsResult)
        options:MTLResourceStorageModeShared];
    if (!next) return ANITY_ERR_OUT_OF_MEMORY;
    id<MTLComputeCommandEncoder> reduceEncoder =
        [commandBuffer computeCommandEncoder];
    if (!reduceEncoder) return ANITY_ERR_DEVICE_LOST;
    [reduceEncoder setComputePipelineState:reducePipeline];
    [reduceEncoder setBuffer:current offset:0 atIndex:0];
    [reduceEncoder setBuffer:next offset:0 atIndex:1];
    [reduceEncoder setBytes:&currentCount length:sizeof(currentCount) atIndex:2];
    NSUInteger reduceWidth = std::min<NSUInteger>(
        reducePipeline.maxTotalThreadsPerThreadgroup,
        static_cast<NSUInteger>(64));
    if (reduceWidth == 0) return ANITY_ERR_NOT_SUPPORTED;
    [reduceEncoder dispatchThreads:MTLSizeMake(outputCount, 1, 1)
        threadsPerThreadgroup:MTLSizeMake(reduceWidth, 1, 1)];
    [reduceEncoder endEncoding];
    current = next;
    currentCount = outputCount;
  }

  [commandBuffer commit];
  if (buffers) ++buffers->boundsDispatchCount;
  [commandBuffer waitUntilCompleted];
  ObserveMetalDeviceLoss(st, commandBuffer);
  if (commandBuffer.status != MTLCommandBufferStatusCompleted)
    return ANITY_ERR_DEVICE_LOST;
  if (buffers) ++buffers->boundsCompletionCount;
  const RawBoundsResult result =
      *static_cast<const RawBoundsResult*>([current contents]);
  outResult->backendKind = 2;
  outResult->valid = 0;
  if (result.invalid != 0u || result.validCount != static_cast<uint32_t>(aliveCount) ||
      result.validCount == 0u) {
    if (buffers) {
      buffers->boundsCacheDesc = *desc;
      buffers->boundsCacheResult = *outResult;
      buffers->boundsCacheValid = true;
    }
    return ANITY_OK;
  }
  const float minimum[3] = {result.minX, result.minY, result.minZ};
  const float maximum[3] = {result.maxX, result.maxY, result.maxZ};
  const float padding[3] = {desc->paddingX, desc->paddingY, desc->paddingZ};
  float center[3]{};
  float extents[3]{};
  for (int axis = 0; axis < 3; ++axis) {
    center[axis] = minimum[axis] + (maximum[axis] - minimum[axis]) * 0.5f;
    extents[axis] = (maximum[axis] - minimum[axis]) * 0.5f + padding[axis];
    if (!std::isfinite(center[axis]) || !std::isfinite(extents[axis]) ||
        extents[axis] < 0.0f)
      return ANITY_OK;
  }
  outResult->centerX = center[0];
  outResult->centerY = center[1];
  outResult->centerZ = center[2];
  outResult->extentsX = extents[0];
  outResult->extentsY = extents[1];
  outResult->extentsZ = extents[2];
  outResult->valid = 1;
  if (buffers) {
    buffers->boundsCacheDesc = *desc;
    buffers->boundsCacheResult = *outResult;
    buffers->boundsCacheValid = true;
  }
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_UploadUI(
    AnityGraphicsDevice* device, int32_t ringIndex,
    const void* vertices, int32_t vertexBytes,
    const void* indices, int32_t indexBytes) {
  if (!device || !device->backend || ringIndex < 0 || ringIndex >= 3 ||
      vertexBytes < 0 || indexBytes < 0) return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
  dispatch_semaphore_wait(st->uiSlotSemaphores[ringIndex], DISPATCH_TIME_FOREVER);
  st->uiSlotAcquired[ringIndex] = true;
  st->uiSlotSubmitted[ringIndex].store(0, std::memory_order_release);
  AnityResult result = UploadMetalBuffer(st, &st->uiVertexBuffers[ringIndex],
      &st->uiVertexCapacities[ringIndex], vertices, vertexBytes);
  if (result != ANITY_OK) {
    ReleaseUISlot(st, ringIndex);
    return result;
  }
  result = UploadMetalBuffer(st, &st->uiIndexBuffers[ringIndex],
      &st->uiIndexCapacities[ringIndex], indices, indexBytes);
  if (result != ANITY_OK) {
    ReleaseUISlot(st, ringIndex);
    return result;
  }
  st->uiVertexLengths[ringIndex] = static_cast<NSUInteger>(vertexBytes);
  st->uiIndexLengths[ringIndex] = static_cast<NSUInteger>(indexBytes);
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_DrawVFXPlanarCamera(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXPlanarCameraBatchDesc* camera,
    const AnityGraphicsVFXPlanarDrawPacket* packets, int32_t packetCount,
    AnityGraphicsVFXPlanarCameraDrawInfo* outInfo) {
  if (!device || !device->backend || !camera || !outInfo || packetCount < 0 ||
      (packetCount > 0 && !packets))
    return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
  auto* swapchain = device->swapchain;
  auto* mst = swapchain
      ? reinterpret_cast<MetalSwapchainState*>(swapchain->backend) : nullptr;
  id<MTLTexture> target = mst && mst->currentDrawable
      ? mst->currentDrawable.texture : (mst ? mst->offscreenTexture : nil);
  if (!target || !mst || !mst->depthTexture || !st->queue)
    return ANITY_ERR_NOT_SUPPORTED;

  bool hasCompactionCandidate = false;
  bool hasSortingCandidate = false;
  for (int32_t index = 0; index < packetCount; ++index) {
    const auto& output = packets[index].output;
    const bool candidate =
        (output.flags & 1u) != 0 && (output.flags & 2u) == 0 &&
         output.uvMode == 0 && output.zTest >= 0 && output.zTest <= 6 &&
         output.zWrite >= 0 && output.zWrite <= 1 &&
         packets[index].generation != 0 &&
         (packets[index].aliveCount > 0 ||
          packets[index].pendingInitialize != 0);
    hasCompactionCandidate = hasCompactionCandidate || candidate;
    hasSortingCandidate = hasSortingCandidate ||
        (candidate && (output.flags & 4u) != 0);
  }
  id<MTLComputePipelineState> aliveMapPipeline = nil;
  id<MTLComputePipelineState> indirectPipeline = nil;
  id<MTLComputePipelineState> prefixPipeline = nil;
  id<MTLComputePipelineState> compactPipeline = nil;
  id<MTLComputePipelineState> unusedCommitPipeline = nil;
  id<MTLComputePipelineState> sortMapPipeline = nil;
  id<MTLComputePipelineState> sortStagePipeline = nil;
  id<MTLComputePipelineState> sortExtractPipeline = nil;
  if (hasCompactionCandidate &&
      (!GetVFXPlanarComputePipelines(
           st, &aliveMapPipeline, &indirectPipeline) ||
       !GetVFXDeathPipelines(st, &prefixPipeline, &compactPipeline,
                            &unusedCommitPipeline)))
    return ANITY_ERR_NOT_SUPPORTED;
  if (hasSortingCandidate && !GetVFXPlanarSortPipelines(
          st, &sortMapPipeline, &sortStagePipeline, &sortExtractPipeline))
    return ANITY_ERR_NOT_SUPPORTED;

  std::lock_guard<std::mutex> lock(st->vfxComputeMutex);
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
  id<MTLCommandBuffer> commandBuffer = [st->queue commandBuffer];
  if (!commandBuffer) return ANITY_ERR_DEVICE_LOST;
  commandBuffer.label = @"Anity VFX Planar Camera";

  struct PreparedDraw {
    const AnityGraphicsVFXPlanarDrawPacket* packet = nullptr;
    MetalVFXUpdateBuffers* buffers = nullptr;
    id<MTLRenderPipelineState> pipeline = nil;
    id<MTLDepthStencilState> depthState = nil;
    id<MTLBuffer> particleIndices = nil;
    NSUInteger indirectOffset = 0;
    uint32_t verticesPerParticle = 0;
    int32_t visibleVertexCount = 0;
  };
  std::vector<PreparedDraw> prepared;
  std::vector<MetalVFXUpdateBuffers*> compactedBuffers;
  std::vector<MetalVFXUpdateBuffers*> mutatedSortBuffers;
  std::vector<MetalVFXUpdateBuffers*> touchedSortBuffers;
  std::vector<MetalVFXParticleKey> mutatedParticleKeys;
  id<MTLBuffer> indirectArguments = nil;
  NSUInteger indirectArgumentCapacity = 0;
  try {
    prepared.reserve(static_cast<size_t>(packetCount));
    compactedBuffers.reserve(static_cast<size_t>(packetCount));
    mutatedSortBuffers.reserve(static_cast<size_t>(packetCount));
    touchedSortBuffers.reserve(static_cast<size_t>(packetCount));
    mutatedParticleKeys.reserve(static_cast<size_t>(packetCount));
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  auto markMutated = [&](const MetalVFXParticleKey& key) {
    if (std::find(mutatedParticleKeys.begin(), mutatedParticleKeys.end(), key) ==
        mutatedParticleKeys.end())
      mutatedParticleKeys.push_back(key);
  };
  auto fail = [&](AnityResult result) {
    for (MetalVFXUpdateBuffers* buffers : compactedBuffers)
      if (buffers) buffers->aliveCompactGeneration = 0;
    for (MetalVFXUpdateBuffers* buffers : mutatedSortBuffers)
      if (buffers) InvalidateMetalVFXPlanarSortCaches(*buffers);
    [indirectArguments release];
    indirectArguments = nil;
    return result;
  };
  if (hasCompactionCandidate) {
    const int64_t argumentBytes =
        static_cast<int64_t>(packetCount) * 4ll * sizeof(uint32_t);
    if (argumentBytes <= 0 || argumentBytes > std::numeric_limits<int32_t>::max())
      return ANITY_ERR_OUT_OF_MEMORY;
    AnityResult allocation = EnsureMetalBuffer(
        st, &indirectArguments, &indirectArgumentCapacity,
        static_cast<int32_t>(argumentBytes));
    if (allocation != ANITY_OK) return allocation;
  }

  struct AliveMapParams {
    uint32_t strideWords;
    uint32_t capacity;
    int32_t aliveOffsetWords;
  };
  static_assert(sizeof(AliveMapParams) == 12,
                "Metal VFX alive map parameter ABI changed");
  const NSUInteger aliveMapWidth = aliveMapPipeline
      ? std::min<NSUInteger>(aliveMapPipeline.maxTotalThreadsPerThreadgroup, 64u)
      : 0;
  const NSUInteger indirectWidth = indirectPipeline
      ? std::min<NSUInteger>(indirectPipeline.maxTotalThreadsPerThreadgroup, 1u)
      : 0;
  const NSUInteger sortMapWidth = sortMapPipeline
      ? std::min<NSUInteger>(sortMapPipeline.maxTotalThreadsPerThreadgroup, 64u)
      : 0;
  const NSUInteger sortStageWidth = sortStagePipeline
      ? std::min<NSUInteger>(sortStagePipeline.maxTotalThreadsPerThreadgroup, 64u)
      : 0;
  const NSUInteger sortExtractWidth = sortExtractPipeline
      ? std::min<NSUInteger>(sortExtractPipeline.maxTotalThreadsPerThreadgroup, 64u)
      : 0;

  for (int32_t index = 0; index < packetCount; ++index) {
    const AnityGraphicsVFXPlanarDrawPacket& packet = packets[index];
    const AnityGraphicsVFXPlanarOutputDesc& output = packet.output;
    const bool backendSupported = (output.flags & 1u) != 0 &&
        (output.flags & 2u) == 0 && output.uvMode == 0 &&
        output.zTest >= 0 && output.zTest <= 6 &&
        output.zWrite >= 0 && output.zWrite <= 1 &&
        packet.generation != 0 &&
        (packet.aliveCount > 0 || packet.pendingInitialize != 0);
    if (!backendSupported) {
      ++outInfo->skippedOutputCount;
      continue;
    }
    const MetalVFXParticleKey key{output.effectId, output.particleSystemId};
    auto resident = st->vfxUpdateBuffers.find(key);
    const uint64_t requiredBytes =
        static_cast<uint64_t>(output.particleCapacity) *
        static_cast<uint64_t>(output.attributeStrideBytes);
    if (resident == st->vfxUpdateBuffers.end() || !resident->second.resident ||
        resident->second.residentGeneration != packet.generation ||
        requiredBytes > resident->second.residentCapacity) {
      ++outInfo->skippedOutputCount;
      continue;
    }
    id<MTLRenderPipelineState> pipeline = GetVFXPlanarPipeline(
        st, target.pixelFormat, output.blendMode);
    if (!pipeline) return fail(ANITY_ERR_NOT_SUPPORTED);
    id<MTLDepthStencilState> depthState = GetVFXPlanarDepthState(
        st, output.zTest, output.zWrite);
    if (!depthState) return fail(ANITY_ERR_NOT_SUPPORTED);
    const uint32_t verticesPerParticle = output.primitiveType == 0
        ? 3u : (output.primitiveType == 1 ? 6u : 18u);
    const uint64_t capacityVertexCount =
        static_cast<uint64_t>(output.particleCapacity) * verticesPerParticle;
    const uint64_t visibleVertexCount =
        static_cast<uint64_t>(packet.aliveCount) * verticesPerParticle;
    if (visibleVertexCount > static_cast<uint64_t>(std::numeric_limits<int32_t>::max()) ||
        static_cast<int64_t>(outInfo->particleCount) + packet.aliveCount >
            std::numeric_limits<int32_t>::max() ||
        static_cast<int64_t>(outInfo->vertexCount) +
            static_cast<int64_t>(visibleVertexCount) >
            std::numeric_limits<int32_t>::max() ||
        outInfo->capacityVertexCount >
            std::numeric_limits<uint64_t>::max() - capacityVertexCount)
      return fail(ANITY_ERR_OUT_OF_MEMORY);

    MetalVFXUpdateBuffers& buffers = resident->second;
    if (std::find(buffers.inFlightGenerations.begin(),
                  buffers.inFlightGenerations.end(), packet.generation) !=
            buffers.inFlightGenerations.end() ||
        std::find(buffers.inFlightInitializeGenerations.begin(),
                  buffers.inFlightInitializeGenerations.end(),
                  packet.generation) !=
            buffers.inFlightInitializeGenerations.end())
      ++buffers.cameraDependencyCount;
    const int32_t strideWords = output.attributeStrideBytes / 4;
    const int32_t aliveOffsetWords = output.aliveOffsetBytes / 4;
    const bool cacheHit = buffers.aliveCompactGeneration == packet.generation &&
        buffers.aliveCompactStrideWords == strideWords &&
        buffers.aliveCompactCapacity == output.particleCapacity &&
        buffers.aliveCompactOffsetWords == aliveOffsetWords &&
        buffers.aliveIndices && buffers.aliveCount;
    if (cacheHit) {
      ++outInfo->aliveCompactionCacheHitCount;
    } else {
      InvalidateMetalVFXPlanarSortCaches(buffers);
      if (aliveMapWidth == 0 || indirectWidth == 0)
        return fail(ANITY_ERR_NOT_SUPPORTED);
      const int64_t indexBytes =
          static_cast<int64_t>(output.particleCapacity) * sizeof(uint32_t);
      if (indexBytes <= 0 || indexBytes > std::numeric_limits<int32_t>::max())
        return fail(ANITY_ERR_OUT_OF_MEMORY);
      AnityResult allocation = EnsureMetalBuffer(
          st, &buffers.aliveFlags, &buffers.aliveFlagCapacity,
          static_cast<int32_t>(indexBytes));
      if (allocation == ANITY_OK)
        allocation = EnsureMetalBuffer(
            st, &buffers.aliveScratch, &buffers.aliveScratchCapacity,
            static_cast<int32_t>(indexBytes));
      if (allocation == ANITY_OK)
        allocation = EnsureMetalBuffer(
            st, &buffers.aliveIndices, &buffers.aliveIndexCapacity,
            static_cast<int32_t>(indexBytes));
      if (allocation == ANITY_OK)
        allocation = EnsureMetalBuffer(
            st, &buffers.aliveCount, &buffers.aliveCountCapacity,
            static_cast<int32_t>(sizeof(uint32_t)));
      if (allocation != ANITY_OK) return fail(allocation);
      AliveMapParams aliveParams{
          static_cast<uint32_t>(strideWords),
          static_cast<uint32_t>(output.particleCapacity), aliveOffsetWords};
      id<MTLComputeCommandEncoder> mapEncoder =
          [commandBuffer computeCommandEncoder];
      if (!mapEncoder) return fail(ANITY_ERR_DEVICE_LOST);
      [mapEncoder setComputePipelineState:aliveMapPipeline];
      [mapEncoder setBuffer:buffers.resident offset:0 atIndex:0];
      [mapEncoder setBuffer:buffers.aliveFlags offset:0 atIndex:1];
      [mapEncoder setBytes:&aliveParams length:sizeof(aliveParams) atIndex:2];
      [mapEncoder dispatchThreads:MTLSizeMake(
          static_cast<NSUInteger>(output.particleCapacity), 1, 1)
          threadsPerThreadgroup:MTLSizeMake(aliveMapWidth, 1, 1)];
      [mapEncoder endEncoding];
      id<MTLBuffer> finalPrefix = nil;
      uint64_t prefixPasses = 0;
      AnityResult compaction = EncodeMetalVFXStableCompaction(
          st, commandBuffer, buffers.aliveFlags, buffers.aliveScratch,
          buffers.aliveIndices, buffers.aliveCount,
          static_cast<uint32_t>(output.particleCapacity),
          prefixPipeline, compactPipeline, &finalPrefix, &prefixPasses);
      if (compaction != ANITY_OK || !finalPrefix)
        return fail(compaction == ANITY_OK ? ANITY_ERR_INTERNAL : compaction);
      if (prefixPasses > static_cast<uint64_t>(
              std::numeric_limits<int32_t>::max() - outInfo->alivePrefixPassCount))
        return fail(ANITY_ERR_OUT_OF_MEMORY);
      buffers.aliveCompactGeneration = packet.generation;
      buffers.aliveCompactStrideWords = strideWords;
      buffers.aliveCompactCapacity = output.particleCapacity;
      buffers.aliveCompactOffsetWords = aliveOffsetWords;
      compactedBuffers.push_back(&buffers);
      markMutated(key);
      ++outInfo->aliveCompactionCount;
      outInfo->alivePrefixPassCount += static_cast<int32_t>(prefixPasses);
    }

    id<MTLBuffer> particleIndices = buffers.aliveIndices;
    if ((output.flags & 4u) != 0) {
      uint32_t paddedLength = 0;
      if (!TryVFXPlanarPaddedLength(
              output.particleCapacity, &paddedLength) ||
          sortMapWidth == 0 || sortStageWidth == 0 || sortExtractWidth == 0)
        return fail(ANITY_ERR_NOT_SUPPORTED);
      const int32_t positionOffsetWords = output.positionOffsetBytes / 4;
      if (std::find(touchedSortBuffers.begin(), touchedSortBuffers.end(),
                    &buffers) == touchedSortBuffers.end())
        touchedSortBuffers.push_back(&buffers);
      MetalVFXPlanarSortCacheEntry* sortCache =
          FindMetalVFXPlanarSortCache(
              buffers, packet.generation, camera->cameraId, strideWords,
              output.particleCapacity, positionOffsetWords, paddedLength,
              packet.localToWorld, camera->worldToClip);
      if (sortCache) {
        sortCache->lastUseSerial = ++buffers.planarSortUseSerial;
        ++outInfo->sortCacheHitCount;
      } else {
        bool evicted = false;
        sortCache = AcquireMetalVFXPlanarSortCache(buffers, &evicted);
        if (!sortCache) return fail(ANITY_ERR_INTERNAL);
        if (std::find(mutatedSortBuffers.begin(), mutatedSortBuffers.end(),
                      &buffers) == mutatedSortBuffers.end())
          mutatedSortBuffers.push_back(&buffers);
        markMutated(key);
        ++outInfo->sortCacheInsertCount;
        if (evicted) ++outInfo->sortCacheEvictionCount;
        const int64_t entryBytes =
            static_cast<int64_t>(paddedLength) * 4ll * sizeof(uint32_t);
        const int64_t sortedIndexBytes =
            static_cast<int64_t>(output.particleCapacity) * sizeof(uint32_t);
        if (entryBytes <= 0 ||
            entryBytes > std::numeric_limits<int32_t>::max() ||
            sortedIndexBytes <= 0 ||
            sortedIndexBytes > std::numeric_limits<int32_t>::max() ||
            paddedLength > static_cast<uint32_t>(
                std::numeric_limits<int32_t>::max() -
                outInfo->sortPaddedParticleCount))
          return fail(ANITY_ERR_OUT_OF_MEMORY);
        AnityResult allocation = EnsureMetalOwnedBuffer(
            st, &sortCache->entries, &sortCache->entryCapacity,
            static_cast<int32_t>(entryBytes));
        if (allocation == ANITY_OK)
          allocation = EnsureMetalOwnedBuffer(
              st, &sortCache->sortedIndices,
              &sortCache->sortedIndexCapacity,
              static_cast<int32_t>(sortedIndexBytes));
        if (allocation != ANITY_OK) return fail(allocation);

        MetalVFXPlanarSortMapParams sortParams{};
        sortParams.strideWords = static_cast<uint32_t>(strideWords);
        sortParams.capacity = static_cast<uint32_t>(output.particleCapacity);
        sortParams.positionOffsetWords = positionOffsetWords;
        sortParams.paddedLength = paddedLength;
        std::memcpy(sortParams.localToWorld, packet.localToWorld,
                    sizeof(sortParams.localToWorld));
        std::memcpy(sortParams.worldToClip, camera->worldToClip,
                    sizeof(sortParams.worldToClip));
        id<MTLComputeCommandEncoder> sortMapEncoder =
            [commandBuffer computeCommandEncoder];
        if (!sortMapEncoder) return fail(ANITY_ERR_DEVICE_LOST);
        [sortMapEncoder setComputePipelineState:sortMapPipeline];
        [sortMapEncoder setBuffer:buffers.resident offset:0 atIndex:0];
        [sortMapEncoder setBuffer:buffers.aliveIndices offset:0 atIndex:1];
        [sortMapEncoder setBuffer:buffers.aliveCount offset:0 atIndex:2];
        [sortMapEncoder setBuffer:sortCache->entries offset:0 atIndex:3];
        [sortMapEncoder setBytes:&sortParams
                          length:sizeof(sortParams) atIndex:4];
        [sortMapEncoder dispatchThreads:MTLSizeMake(paddedLength, 1, 1)
            threadsPerThreadgroup:MTLSizeMake(sortMapWidth, 1, 1)];
        [sortMapEncoder endEncoding];
        ++outInfo->sortMapDispatchCount;

        for (uint32_t sequenceLength = 2u;
             sequenceLength <= paddedLength; sequenceLength <<= 1u) {
          for (uint32_t compareDistance = sequenceLength >> 1u;
               compareDistance > 0u; compareDistance >>= 1u) {
            MetalVFXPlanarSortStageParams stageParams{
                compareDistance, sequenceLength, paddedLength};
            id<MTLComputeCommandEncoder> sortStageEncoder =
                [commandBuffer computeCommandEncoder];
            if (!sortStageEncoder) return fail(ANITY_ERR_DEVICE_LOST);
            [sortStageEncoder setComputePipelineState:sortStagePipeline];
            [sortStageEncoder setBuffer:sortCache->entries
                                  offset:0 atIndex:0];
            [sortStageEncoder setBytes:&stageParams
                                 length:sizeof(stageParams) atIndex:1];
            [sortStageEncoder dispatchThreads:MTLSizeMake(paddedLength, 1, 1)
                threadsPerThreadgroup:MTLSizeMake(sortStageWidth, 1, 1)];
            [sortStageEncoder endEncoding];
            ++outInfo->sortStageDispatchCount;
          }
          if (sequenceLength == paddedLength) break;
        }

        const uint32_t sortCapacity =
            static_cast<uint32_t>(output.particleCapacity);
        id<MTLComputeCommandEncoder> sortExtractEncoder =
            [commandBuffer computeCommandEncoder];
        if (!sortExtractEncoder) return fail(ANITY_ERR_DEVICE_LOST);
        [sortExtractEncoder setComputePipelineState:sortExtractPipeline];
        [sortExtractEncoder setBuffer:sortCache->entries
                                offset:0 atIndex:0];
        [sortExtractEncoder setBuffer:buffers.aliveCount offset:0 atIndex:1];
        [sortExtractEncoder setBuffer:sortCache->sortedIndices
                                offset:0 atIndex:2];
        [sortExtractEncoder setBytes:&sortCapacity
                               length:sizeof(sortCapacity) atIndex:3];
        [sortExtractEncoder dispatchThreads:MTLSizeMake(sortCapacity, 1, 1)
            threadsPerThreadgroup:MTLSizeMake(sortExtractWidth, 1, 1)];
        [sortExtractEncoder endEncoding];
        ++outInfo->sortExtractDispatchCount;
        outInfo->sortPaddedParticleCount += static_cast<int32_t>(paddedLength);

        sortCache->generation = packet.generation;
        sortCache->cameraId = camera->cameraId;
        sortCache->lastUseSerial = ++buffers.planarSortUseSerial;
        sortCache->strideWords = strideWords;
        sortCache->particleCapacity = output.particleCapacity;
        sortCache->positionOffsetWords = positionOffsetWords;
        sortCache->paddedLength = paddedLength;
        std::memcpy(sortCache->localToWorld, packet.localToWorld,
                    sizeof(sortCache->localToWorld));
        std::memcpy(sortCache->worldToClip, camera->worldToClip,
                    sizeof(sortCache->worldToClip));
      }
      particleIndices = sortCache->sortedIndices;
      ++outInfo->sortedOutputCount;
    }

    const NSUInteger indirectOffset =
        static_cast<NSUInteger>(prepared.size()) * 4u * sizeof(uint32_t);
    id<MTLComputeCommandEncoder> indirectEncoder =
        [commandBuffer computeCommandEncoder];
    if (!indirectEncoder) return fail(ANITY_ERR_DEVICE_LOST);
    [indirectEncoder setComputePipelineState:indirectPipeline];
    [indirectEncoder setBuffer:buffers.aliveCount offset:0 atIndex:0];
    [indirectEncoder setBuffer:indirectArguments
                         offset:indirectOffset atIndex:1];
    [indirectEncoder setBytes:&verticesPerParticle
                       length:sizeof(verticesPerParticle) atIndex:2];
    [indirectEncoder dispatchThreads:MTLSizeMake(1, 1, 1)
        threadsPerThreadgroup:MTLSizeMake(indirectWidth, 1, 1)];
    [indirectEncoder endEncoding];
    prepared.push_back(PreparedDraw{
        &packet, &buffers, pipeline, depthState, particleIndices,
        indirectOffset, verticesPerParticle,
        static_cast<int32_t>(visibleVertexCount)});
    ++outInfo->indirectArgumentCount;
    outInfo->capacityVertexCount += capacityVertexCount;
    if (output.zTest != 6) ++outInfo->depthTestOutputCount;
    if (output.zWrite != 0) ++outInfo->depthWriteOutputCount;
  }

  for (const MetalVFXUpdateBuffers* buffers : touchedSortBuffers) {
    if (!buffers) continue;
    const int32_t count = CountMetalVFXPlanarSortCaches(*buffers);
    if (count > std::numeric_limits<int32_t>::max() -
                    outInfo->sortCacheEntryCount)
      return fail(ANITY_ERR_OUT_OF_MEMORY);
    outInfo->sortCacheEntryCount += count;
  }
  if (!touchedSortBuffers.empty())
    outInfo->sortCacheCapacityPerSystem =
        kMetalVFXPlanarSortCacheCapacity;

  MTLRenderPassDescriptor* pass = [MTLRenderPassDescriptor renderPassDescriptor];
  pass.colorAttachments[0].texture = target;
  pass.colorAttachments[0].loadAction = (camera->flags & 1) != 0
      ? MTLLoadActionClear : MTLLoadActionLoad;
  pass.colorAttachments[0].storeAction = MTLStoreActionStore;
  pass.colorAttachments[0].clearColor = MTLClearColorMake(0.0, 0.0, 0.0, 0.0);
  const bool clearDepth = (camera->flags & 1) != 0 || !mst->depthInitialized;
  pass.depthAttachment.texture = mst->depthTexture;
  pass.depthAttachment.loadAction = clearDepth
      ? MTLLoadActionClear : MTLLoadActionLoad;
  pass.depthAttachment.storeAction = MTLStoreActionStore;
  pass.depthAttachment.clearDepth = 1.0;
  outInfo->depthClearCount = clearDepth ? 1 : 0;
  id<MTLRenderCommandEncoder> encoder =
      [commandBuffer renderCommandEncoderWithDescriptor:pass];
  if (!encoder) return fail(ANITY_ERR_DEVICE_LOST);
  [encoder setFrontFacingWinding:MTLWindingClockwise];

  id<MTLDepthStencilState> activeDepthState = nil;
  for (const PreparedDraw& draw : prepared) {
    const AnityGraphicsVFXPlanarDrawPacket& packet = *draw.packet;
    const AnityGraphicsVFXPlanarOutputDesc& output = packet.output;
    MetalVFXPlanarParams params{};
    params.strideWords = output.attributeStrideBytes / 4;
    params.capacity = static_cast<uint32_t>(output.particleCapacity);
    params.primitiveType = output.primitiveType;
    params.aliveOffsetWords = output.aliveOffsetBytes / 4;
    params.positionOffsetWords = output.positionOffsetBytes / 4;
    params.colorOffsetWords = output.colorOffsetBytes / 4;
    params.alphaOffsetWords = output.alphaOffsetBytes / 4;
    params.axisXOffsetWords = output.axisXOffsetBytes / 4;
    params.axisYOffsetWords = output.axisYOffsetBytes / 4;
    params.axisZOffsetWords = output.axisZOffsetBytes / 4;
    params.angleXOffsetWords = output.angleXOffsetBytes / 4;
    params.angleYOffsetWords = output.angleYOffsetBytes / 4;
    params.angleZOffsetWords = output.angleZOffsetBytes / 4;
    params.pivotXOffsetWords = output.pivotXOffsetBytes / 4;
    params.pivotYOffsetWords = output.pivotYOffsetBytes / 4;
    params.pivotZOffsetWords = output.pivotZOffsetBytes / 4;
    params.sizeOffsetWords = output.sizeOffsetBytes / 4;
    params.scaleXOffsetWords = output.scaleXOffsetBytes / 4;
    params.scaleYOffsetWords = output.scaleYOffsetBytes / 4;
    params.scaleZOffsetWords = output.scaleZOffsetBytes / 4;
    std::memcpy(params.localToWorld, packet.localToWorld,
                sizeof(params.localToWorld));
    std::memcpy(params.worldToClip, camera->worldToClip,
                sizeof(params.worldToClip));
    [encoder setRenderPipelineState:draw.pipeline];
    if (draw.depthState != activeDepthState) {
      [encoder setDepthStencilState:draw.depthState];
      activeDepthState = draw.depthState;
      ++outInfo->depthStateChangeCount;
    }
    [encoder setCullMode:output.cullMode == 1
        ? MTLCullModeFront : (output.cullMode == 2
            ? MTLCullModeBack : MTLCullModeNone)];
    [encoder setVertexBuffer:draw.buffers->resident offset:0 atIndex:0];
    [encoder setVertexBytes:&params length:sizeof(params) atIndex:1];
    [encoder setVertexBuffer:draw.particleIndices offset:0 atIndex:2];
    [encoder drawPrimitives:MTLPrimitiveTypeTriangle
        indirectBuffer:indirectArguments indirectBufferOffset:draw.indirectOffset];
    ++outInfo->drawCount;
    outInfo->particleCount += packet.aliveCount;
    outInfo->vertexCount += draw.visibleVertexCount;
    outInfo->residentGeneration = std::max(
        outInfo->residentGeneration, packet.generation);
  }
  [encoder endEncoding];

  uint64_t submissionId = 0;
  {
    std::lock_guard<std::mutex> submissionLock(
        st->vfxPlanarSubmissionMutex);
    if (st->vfxPlanarSubmissionStats.lastSubmittedId ==
            std::numeric_limits<uint64_t>::max() ||
        st->vfxPlanarSubmissionStats.inFlightCount ==
            std::numeric_limits<int32_t>::max())
      return fail(ANITY_ERR_OUT_OF_MEMORY);
    submissionId = ++st->vfxPlanarSubmissionStats.lastSubmittedId;
    ++st->vfxPlanarSubmissionStats.submissionCount;
    ++st->vfxPlanarSubmissionStats.inFlightCount;
    st->vfxPlanarSubmissionStats.maxInFlightCount = std::max(
        st->vfxPlanarSubmissionStats.maxInFlightCount,
        st->vfxPlanarSubmissionStats.inFlightCount);
  }
  const std::vector<MetalVFXParticleKey> completionMutationKeys =
      mutatedParticleKeys;
  bool injectedDeviceRemoval = false;
  bool injectedFailure = false;
  if (st->failNextVFXDeviceRemovalCount > 0) {
    --st->failNextVFXDeviceRemovalCount;
    injectedDeviceRemoval = true;
  } else if (st->failNextVFXPlanarCount > 0) {
    --st->failNextVFXPlanarCount;
    injectedFailure = true;
  }
  [commandBuffer addCompletedHandler:^(id<MTLCommandBuffer> completedBuffer) {
    ObserveMetalDeviceLoss(st, completedBuffer, injectedDeviceRemoval);
    const bool failed =
        completedBuffer.status != MTLCommandBufferStatusCompleted ||
        injectedFailure || injectedDeviceRemoval;
    if (failed && !completionMutationKeys.empty()) {
      std::lock_guard<std::mutex> computeLock(st->vfxComputeMutex);
      for (const MetalVFXParticleKey& key : completionMutationKeys) {
        auto found = st->vfxUpdateBuffers.find(key);
        if (found == st->vfxUpdateBuffers.end()) continue;
        found->second.aliveCompactGeneration = 0;
        InvalidateMetalVFXPlanarSortCaches(found->second);
      }
    }
    {
      std::lock_guard<std::mutex> submissionLock(
          st->vfxPlanarSubmissionMutex);
      ++st->vfxPlanarSubmissionStats.completionCount;
      st->vfxPlanarSubmissionStats.lastCompletedId = std::max(
          st->vfxPlanarSubmissionStats.lastCompletedId, submissionId);
      if (st->vfxPlanarSubmissionStats.inFlightCount > 0)
        --st->vfxPlanarSubmissionStats.inFlightCount;
      if (failed) {
        ++st->vfxPlanarSubmissionStats.failureCount;
        st->vfxPlanarSubmissionStats.lastFailedId = std::max(
            st->vfxPlanarSubmissionStats.lastFailedId, submissionId);
      }
      st->vfxPlanarSubmissionResults.emplace_back(submissionId, failed);
      while (st->vfxPlanarSubmissionResults.size() >
             kMetalVFXPlanarSubmissionResultCapacity) {
        st->vfxPlanarLastEvictedSubmissionId = std::max(
            st->vfxPlanarLastEvictedSubmissionId,
            st->vfxPlanarSubmissionResults.front().first);
        st->vfxPlanarSubmissionResults.pop_front();
        ++st->vfxPlanarSubmissionStats.resultEvictionCount;
      }
    }
    st->vfxPlanarSubmissionCondition.notify_all();
  }];
  [commandBuffer commit];
  [indirectArguments release];
  indirectArguments = nil;
  mst->depthInitialized = true;
  outInfo->backendKind = 2;
  outInfo->commandBufferCount = 1;
  outInfo->renderPassCount = 1;
  outInfo->submissionId = submissionId;
  outInfo->asyncSubmissionCount = 1;
  outInfo->synchronousWaitCount = 0;
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_DrawUI(
    AnityGraphicsDevice* device, int32_t ringIndex,
    const AnityUIDrawPacket* packets, int32_t packetCount, int32_t* outDrawCount) {
  if (!device || !device->backend || ringIndex < 0 || ringIndex >= 3 ||
      packetCount < 0 || (packetCount > 0 && !packets) || !outDrawCount)
    return ANITY_ERR_INVALID_ARG;
  *outDrawCount = 0;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  if (IsMetalDeviceLost(st)) {
    if (st->uiSlotAcquired[ringIndex]) ReleaseUISlot(st, ringIndex);
    return ANITY_ERR_DEVICE_LOST;
  }
  if (!st->uiSlotAcquired[ringIndex]) return ANITY_ERR_INVALID_ARG;
  auto* swapchain = device->swapchain;
  auto* mst = swapchain ? reinterpret_cast<MetalSwapchainState*>(swapchain->backend) : nullptr;
  id<MTLTexture> target = mst && mst->currentDrawable
      ? mst->currentDrawable.texture : (mst ? mst->offscreenTexture : nil);
  if (!target || !st->queue) {
    ReleaseUISlot(st, ringIndex);
    return ANITY_ERR_NOT_SUPPORTED;
  }
  if (!EnsureWhiteTexture(st)) {
    ReleaseUISlot(st, ringIndex);
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  if (packetCount > 0 &&
      (!st->uiVertexBuffers[ringIndex] || !st->uiIndexBuffers[ringIndex])) {
    ReleaseUISlot(st, ringIndex);
    return ANITY_ERR_INVALID_ARG;
  }
  id<MTLRenderPipelineState> pipeline = packetCount > 0
      ? GetUIPipeline(st, target.pixelFormat) : nil;
  if (packetCount > 0 && !pipeline) {
    ReleaseUISlot(st, ringIndex);
    return ANITY_ERR_NOT_SUPPORTED;
  }

  id<MTLCommandBuffer> commandBuffer = [st->queue commandBuffer];
  if (!commandBuffer) {
    ReleaseUISlot(st, ringIndex);
    return ANITY_ERR_DEVICE_LOST;
  }
  commandBuffer.label = @"Anity UI Frame";
  MTLRenderPassDescriptor* pass = [MTLRenderPassDescriptor renderPassDescriptor];
  pass.colorAttachments[0].texture = target;
  pass.colorAttachments[0].loadAction = MTLLoadActionClear;
  pass.colorAttachments[0].storeAction = MTLStoreActionStore;
  pass.colorAttachments[0].clearColor = MTLClearColorMake(0.0, 0.0, 0.0, 0.0);
  id<MTLRenderCommandEncoder> encoder = [commandBuffer renderCommandEncoderWithDescriptor:pass];
  if (!encoder) {
    ReleaseUISlot(st, ringIndex);
    return ANITY_ERR_DEVICE_LOST;
  }
  if (packetCount > 0) {
    [encoder setRenderPipelineState:pipeline];
    [encoder setVertexBuffer:st->uiVertexBuffers[ringIndex] offset:0 atIndex:0];
    struct { float width; float height; } viewport = {
      static_cast<float>(target.width), static_cast<float>(target.height)
    };
    [encoder setVertexBytes:&viewport length:sizeof(viewport) atIndex:1];
  }
  const uint64_t indexCapacity = st->uiIndexLengths[ringIndex] / sizeof(uint32_t);
  for (int32_t index = 0; index < packetCount; ++index) {
    const AnityUIDrawPacket& packet = packets[index];
    if ((packet.info.flags & (ANITY_UI_COMMAND_MASK | ANITY_UI_COMMAND_POP)) != 0)
      continue;
    const uint64_t endIndex = static_cast<uint64_t>(packet.firstIndex) +
        static_cast<uint64_t>(packet.info.indexCount);
    if (packet.info.indexCount <= 0 || endIndex > indexCapacity) {
      [encoder endEncoding];
      ReleaseUISlot(st, ringIndex);
      return ANITY_ERR_INVALID_ARG;
    }
    MTLScissorRect scissor{0, 0, target.width, target.height};
    if ((packet.info.flags & ANITY_UI_COMMAND_RECT_CLIP) != 0) {
      const double minX = std::max(0.0, std::floor(static_cast<double>(packet.clipXMin)));
      const double minY = std::max(0.0, std::floor(static_cast<double>(packet.clipYMin)));
      const double maxX = std::min(static_cast<double>(target.width),
          std::ceil(static_cast<double>(packet.clipXMax)));
      const double maxY = std::min(static_cast<double>(target.height),
          std::ceil(static_cast<double>(packet.clipYMax)));
      if (maxX <= minX || maxY <= minY) continue;
      scissor.x = static_cast<NSUInteger>(minX);
      scissor.y = static_cast<NSUInteger>(minY);
      scissor.width = static_cast<NSUInteger>(maxX - minX);
      scissor.height = static_cast<NSUInteger>(maxY - minY);
    }
    [encoder setScissorRect:scissor];
    id<MTLTexture> mainTexture = st->whiteTexture;
    id<MTLSamplerState> mainSampler = st->defaultSampler;
    id<MTLTexture> alphaTexture = st->whiteTexture;
    id<MTLSamplerState> alphaSampler = st->defaultSampler;
    uint32_t textureFlags = 0;
    {
      std::lock_guard<std::mutex> lock(st->textureMutex);
      auto main = st->textures.find(packet.info.textureId);
      if (main != st->textures.end()) {
        mainTexture = main->second.texture;
        mainSampler = main->second.sampler;
        textureFlags |= 1u;
      }
      auto alpha = st->textures.find(packet.info.alphaTextureId);
      if (alpha != st->textures.end()) {
        alphaTexture = alpha->second.texture;
        alphaSampler = alpha->second.sampler;
        textureFlags |= 2u;
      }
    }
    [encoder setFragmentTexture:mainTexture atIndex:0];
    [encoder setFragmentTexture:alphaTexture atIndex:1];
    [encoder setFragmentSamplerState:mainSampler atIndex:0];
    [encoder setFragmentSamplerState:alphaSampler atIndex:1];
    [encoder setFragmentBytes:&textureFlags length:sizeof(textureFlags) atIndex:0];
    [encoder drawIndexedPrimitives:MTLPrimitiveTypeTriangle
        indexCount:static_cast<NSUInteger>(packet.info.indexCount)
        indexType:MTLIndexTypeUInt32
        indexBuffer:st->uiIndexBuffers[ringIndex]
        indexBufferOffset:static_cast<NSUInteger>(packet.firstIndex) * sizeof(uint32_t)];
    (*outDrawCount)++;
  }
  [encoder endEncoding];
  dispatch_semaphore_t semaphore = st->uiSlotSemaphores[ringIndex];
  st->uiSlotSubmitted[ringIndex].store(1, std::memory_order_release);
  [commandBuffer addCompletedHandler:^(id<MTLCommandBuffer> completedBuffer) {
    ObserveMetalDeviceLoss(st, completedBuffer);
    st->uiSlotSubmitted[ringIndex].store(0, std::memory_order_release);
    dispatch_semaphore_signal(semaphore);
  }];
  [commandBuffer commit];
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_Metal_SyncTexture(
    AnityGraphicsDevice* device, uint64_t textureId) {
  if (!device || !device->backend || textureId == 0) return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
  AnityGraphicsTextureSnapshot snapshot;
  if (!AnityGraphics_CopyTextureSnapshot(device, textureId, snapshot))
    return ANITY_ERR_INVALID_ARG;
  MTLPixelFormat format = snapshot.info.desc.linear != 0
      ? MTLPixelFormatRGBA8Unorm : MTLPixelFormatRGBA8Unorm_sRGB;
  MTLTextureDescriptor* descriptor = [[MTLTextureDescriptor alloc] init];
  descriptor.textureType = MTLTextureType2D;
  descriptor.pixelFormat = format;
  descriptor.width = static_cast<NSUInteger>(snapshot.info.desc.width);
  descriptor.height = static_cast<NSUInteger>(snapshot.info.desc.height);
  descriptor.depth = 1;
  descriptor.mipmapLevelCount = static_cast<NSUInteger>(snapshot.info.desc.mipCount);
  descriptor.arrayLength = 1;
  descriptor.sampleCount = 1;
  descriptor.usage = MTLTextureUsageShaderRead;
  descriptor.storageMode = MTLStorageModeShared;
  id<MTLTexture> texture = [st->device newTextureWithDescriptor:descriptor];
  id<MTLSamplerState> sampler = CreateSampler(st, snapshot.info.desc);
  if (!texture || !sampler) return ANITY_ERR_OUT_OF_MEMORY;
  size_t byteOffset = 0;
  int32_t mipWidth = snapshot.info.desc.width;
  int32_t mipHeight = snapshot.info.desc.height;
  for (int32_t mip = 0; mip < snapshot.info.desc.mipCount; ++mip) {
    [texture replaceRegion:MTLRegionMake2D(0, 0, mipWidth, mipHeight)
        mipmapLevel:static_cast<NSUInteger>(mip)
        withBytes:snapshot.rgba8.data() + byteOffset
        bytesPerRow:static_cast<NSUInteger>(mipWidth) * 4u];
    byteOffset += static_cast<size_t>(mipWidth) * static_cast<size_t>(mipHeight) * 4u;
    mipWidth = std::max(1, mipWidth >> 1);
    mipHeight = std::max(1, mipHeight >> 1);
  }
  {
    std::lock_guard<std::mutex> lock(st->textureMutex);
    st->textures[textureId] = MetalTextureEntry{
        texture, sampler, snapshot.info.desc.revision};
  }
  AnityGraphics_SetTextureBackendState(
      device, textureId, (__bridge void*)texture, 2);
  return ANITY_OK;
}

extern "C" void AnityGraphics_Metal_DestroyTexture(
    AnityGraphicsDevice* device, uint64_t textureId) {
  if (!device || !device->backend || textureId == 0) return;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  {
    std::lock_guard<std::mutex> lock(st->textureMutex);
    st->textures.erase(textureId);
  }
  AnityGraphics_SetTextureBackendState(device, textureId, nullptr, 0);
}

extern "C" AnityResult AnityGraphics_Metal_CreateSwapchain(
    AnityGraphicsDevice* device, const AnitySwapchainDesc* desc, AnitySwapchain** out) {
  if (!device || !desc || !out) return ANITY_ERR_INVALID_ARG;
  auto* st = reinterpret_cast<MetalState*>(device->backend);
  if (!st || !st->device) return ANITY_ERR_DEVICE_LOST;
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;

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

  if (mst->headless) {
    MTLTextureDescriptor* textureDescriptor = [MTLTextureDescriptor
        texture2DDescriptorWithPixelFormat:layer.pixelFormat
        width:static_cast<NSUInteger>(sc->width)
        height:static_cast<NSUInteger>(sc->height)
        mipmapped:NO];
    textureDescriptor.usage = MTLTextureUsageRenderTarget | MTLTextureUsageShaderRead;
    textureDescriptor.storageMode = MTLStorageModeShared;
    mst->offscreenTexture = [st->device newTextureWithDescriptor:textureDescriptor];
    if (!mst->offscreenTexture) {
      delete mst;
      delete sc;
      return ANITY_ERR_OUT_OF_MEMORY;
    }
  }

  MTLTextureDescriptor* depthDescriptor = [MTLTextureDescriptor
      texture2DDescriptorWithPixelFormat:MTLPixelFormatDepth32Float
      width:static_cast<NSUInteger>(sc->width)
      height:static_cast<NSUInteger>(sc->height)
      mipmapped:NO];
  depthDescriptor.usage = MTLTextureUsageRenderTarget;
  depthDescriptor.storageMode = MTLStorageModePrivate;
  mst->depthTexture = [st->device newTextureWithDescriptor:depthDescriptor];
  if (!mst->depthTexture) {
    mst->offscreenTexture = nil;
    mst->layer = nil;
    delete mst;
    delete sc;
    return ANITY_ERR_OUT_OF_MEMORY;
  }

  mst->layer = layer;
  mst->hasNativeLayer = 1;
  sc->backend = mst;
  device->swapchain = sc;
  *out = sc;
  return ANITY_OK;
}

extern "C" void AnityGraphics_Metal_DestroySwapchain(AnitySwapchain* swapchain) {
  if (!swapchain || !swapchain->backend) return;
  auto* st = swapchain->device
      ? reinterpret_cast<MetalState*>(swapchain->device->backend) : nullptr;
  if (st) (void)WaitForMetalVFXPlanarSubmissions(st, 0, -1, true);
  auto* mst = reinterpret_cast<MetalSwapchainState*>(swapchain->backend);
  mst->currentDrawable = nil;
  mst->offscreenTexture = nil;
  mst->depthTexture = nil;
  mst->depthInitialized = false;
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
  auto* st = swapchain->device
      ? reinterpret_cast<MetalState*>(swapchain->device->backend) : nullptr;
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;
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
  if (IsMetalDeviceLost(st)) return ANITY_ERR_DEVICE_LOST;

  if (mst->currentDrawable && st && st->queue) {
    id<MTLCommandBuffer> cb = [st->queue commandBuffer];
    if (!cb) return ANITY_ERR_DEVICE_LOST;
    [cb presentDrawable:mst->currentDrawable];
    [cb addCompletedHandler:^(id<MTLCommandBuffer> completedBuffer) {
      ObserveMetalDeviceLoss(st, completedBuffer);
    }];
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

extern "C" AnityResult AnityGraphics_Metal_ReadbackSwapchainRGBA8(
    AnitySwapchain* swapchain, uint8_t* pixels, int32_t pixelCapacity,
    int32_t* outWritten) {
  if (!swapchain || !swapchain->backend || pixelCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  *outWritten = 0;
  auto* mst = reinterpret_cast<MetalSwapchainState*>(swapchain->backend);
  auto* st = swapchain->device
      ? reinterpret_cast<MetalState*>(swapchain->device->backend) : nullptr;
  if (!st || !mst->offscreenTexture ||
      mst->offscreenTexture.pixelFormat != MTLPixelFormatBGRA8Unorm)
    return ANITY_ERR_NOT_SUPPORTED;
  const uint64_t required64 = static_cast<uint64_t>(mst->width) *
      static_cast<uint64_t>(mst->height) * 4u;
  if (required64 > static_cast<uint64_t>(std::numeric_limits<int32_t>::max()))
    return ANITY_ERR_OUT_OF_MEMORY;
  const int32_t required = static_cast<int32_t>(required64);
  *outWritten = required;
  if (pixelCapacity < required || (required > 0 && !pixels))
    return ANITY_ERR_INVALID_ARG;
  const AnityResult vfxWait =
      WaitForMetalVFXPlanarSubmissions(st, 0, -1, true);
  if (vfxWait != ANITY_OK) return vfxWait;
  for (int i = 0; i < 3; ++i) {
    if (!st->uiSlotAcquired[i] ||
        st->uiSlotSubmitted[i].load(std::memory_order_acquire) == 0)
      continue;
    dispatch_semaphore_wait(st->uiSlotSemaphores[i], DISPATCH_TIME_FOREVER);
    dispatch_semaphore_signal(st->uiSlotSemaphores[i]);
  }
  std::vector<uint8_t> bgra(static_cast<size_t>(required));
  [mst->offscreenTexture getBytes:bgra.data()
      bytesPerRow:static_cast<NSUInteger>(mst->width) * 4u
      fromRegion:MTLRegionMake2D(0, 0, mst->width, mst->height)
      mipmapLevel:0];
  for (int32_t offset = 0; offset < required; offset += 4) {
    pixels[offset + 0] = bgra[offset + 2];
    pixels[offset + 1] = bgra[offset + 1];
    pixels[offset + 2] = bgra[offset + 0];
    pixels[offset + 3] = bgra[offset + 3];
  }
  return ANITY_OK;
}

#else

extern "C" AnityResult AnityGraphics_CreateMetal(
    const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_DrawUI(
    AnityGraphicsDevice*, int32_t, const AnityUIDrawPacket*, int32_t, int32_t*) {
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
extern "C" AnityResult AnityGraphics_Metal_ReadbackSwapchainRGBA8(
    AnitySwapchain*, uint8_t*, int32_t, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_Metal_SyncTexture(
    AnityGraphicsDevice*, uint64_t) { return ANITY_ERR_NOT_SUPPORTED; }
extern "C" void AnityGraphics_Metal_DestroyTexture(
    AnityGraphicsDevice*, uint64_t) {}

#endif
