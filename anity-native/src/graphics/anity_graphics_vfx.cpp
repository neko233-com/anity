#define ANITY_NATIVE_BUILD
#include "anity_graphics_device.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <deque>
#include <functional>
#include <iterator>
#include <limits>
#include <memory>
#include <mutex>
#include <new>
#include <unordered_map>
#include <vector>

static_assert(sizeof(AnityGraphicsVFXInitializeDispatchDesc) == 56,
              "VFX Initialize dispatch descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXInitializeDispatchInfo) == 80,
              "VFX Initialize dispatch info C ABI changed");
static_assert(sizeof(AnityGraphicsVFXInitializeKernelDesc) == 44,
              "VFX Initialize kernel descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXInitializeAttributeDesc) == 32,
              "VFX Initialize attribute descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXInitializeOperationDesc) == 68,
              "VFX Initialize operation descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXParticleSystemInfo) == 40,
              "VFX particle system info C ABI changed");
static_assert(sizeof(AnityGraphicsVFXUpdateKernelDesc) == 64,
              "VFX Update kernel descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXUpdateOperationDesc) == 80,
              "VFX Update operation descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXUpdateBackendStats) == 528,
              "VFX Update backend stats C ABI changed");
static_assert(sizeof(AnityGraphicsVFXUpdateTicketInfo) == 48,
              "VFX Update ticket info C ABI changed");
static_assert(sizeof(AnityGraphicsVFXInitializeTicketInfo) == 48,
              "VFX Initialize ticket info C ABI changed");
static_assert(sizeof(AnityGraphicsVFXBoundsReductionDesc) == 56,
              "VFX bounds reduction descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXBoundsReductionResult) == 56,
              "VFX bounds reduction result C ABI changed");
static_assert(sizeof(AnityGraphicsVFXFrameState) == 48,
              "VFX frame state C ABI changed");
static_assert(sizeof(AnityGraphicsVFXCullingBounds) == 40,
              "VFX culling bounds C ABI changed");
static_assert(sizeof(AnityGraphicsVFXCullingCamera) == 80,
              "VFX culling camera C ABI changed");
static_assert(sizeof(AnityGraphicsVFXCullingState) == 40,
              "VFX culling state C ABI changed");
static_assert(sizeof(AnityGraphicsVFXPlanarOutputDesc) == 144,
              "VFX Planar Output descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXPlanarCameraDesc) == 152,
              "VFX Planar camera descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXPlanarDrawInfo) == 48,
              "VFX Planar draw info C ABI changed");
static_assert(sizeof(AnityGraphicsVFXPlanarEffectDesc) == 80,
              "VFX Planar effect descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXPlanarCameraBatchDesc) == 88,
              "VFX Planar camera batch descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXPlanarCameraDrawInfo) == 160,
              "VFX Planar camera draw info C ABI changed");
static_assert(sizeof(AnityGraphicsVFXPlanarSubmissionStats) == 80,
              "VFX Planar submission stats C ABI changed");
static_assert(sizeof(AnityGraphicsVFXPlanarDrawPacket) == 232,
              "VFX Planar internal draw packet ABI changed");
static_assert(sizeof(AnityGraphicsVFXSpawnerProgramDesc) == 96,
              "VFX Spawner program descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXSpawnerBlockDesc) == 80,
              "VFX Spawner block descriptor C ABI changed");
static_assert(sizeof(AnityGraphicsVFXSpawnerState) == 80,
              "VFX Spawner state C ABI changed");

struct AnityGraphicsVFXEventStorage {
  AnityGraphicsVFXEventUploadInfo info{};
  std::vector<uint8_t> records;
};

struct AnityGraphicsVFXInitializeStorage {
  AnityGraphicsVFXInitializeDispatchInfo info{};
  std::vector<uint8_t> records;
  uint64_t kernelFingerprint = 0;
};

struct AnityGraphicsVFXParticleSystemStorage {
  AnityGraphicsVFXParticleSystemInfo info{};
  std::vector<uint8_t> attributes;
  std::vector<uint32_t> deadList;
  uint32_t nextSequentialIndex = 0;
  bool usesDeadList = false;
  bool attributesResidentOnly = false;
  uint64_t metadataMaterializedGeneration = 0;
};

struct AnityGraphicsVFXSpawnerBlockStorage {
  AnityGraphicsVFXSpawnerBlockDesc desc{};
  float currentValue = 0.0f;
  float intervalRemaining = 0.0f;
  bool intervalInitialized = false;
  bool burstFinished = false;
};

struct AnityGraphicsVFXSpawnerStorage {
  AnityGraphicsVFXSpawnerProgramDesc desc{};
  AnityGraphicsVFXSpawnerState state{};
  std::vector<AnityGraphicsVFXSpawnerBlockStorage> blocks;
  std::vector<uint32_t> eventRecord;
  std::vector<uint32_t> eventRecordDefaults;
  uint32_t randomState[4]{};
  bool randomInitialized = false;
  float phaseTime = 0.0f;
  float eventAccumulator = 0.0f;
  bool newLoopOnNextTick = false;
  bool prepareLoopOnNextTick = false;
  bool pendingControlEvent = false;
};

struct AnityGraphicsVFXFrameStorage {
  AnityGraphicsVFXFrameState state{};
  AnityGraphicsVFXFrameState rollbackState{};
  std::vector<std::pair<int64_t, AnityGraphicsVFXSpawnerStorage>>
      spawnerRollback;
  std::vector<std::pair<int64_t,
      std::shared_ptr<AnityGraphicsVFXInitializeStorage>>> initializeRollback;
  std::vector<std::pair<int32_t,
      std::shared_ptr<AnityGraphicsVFXParticleSystemStorage>>> particleRollback;
  std::deque<std::shared_ptr<AnityGraphicsVFXEventStorage>> consumedInputRollback;
  std::deque<std::shared_ptr<AnityGraphicsVFXEventStorage>> outputRollback;
  uint64_t outputSequenceRollback = 0;
  bool hadOutputEntries = false;
  bool hadOutputSequence = false;
  bool hadRollbackState = false;
  bool allowNonFiniteCommit = false;
};

struct AnityGraphicsVFXCullingStorage {
  AnityGraphicsVFXCullingBounds bounds{};
  AnityGraphicsVFXCullingState state{};
  bool visible = false;
};

struct AnityGraphicsVFXInitializeKey {
  uint64_t effectId;
  int64_t initializeContextId;
  bool operator==(const AnityGraphicsVFXInitializeKey& other) const {
    return effectId == other.effectId && initializeContextId == other.initializeContextId;
  }
};

struct AnityGraphicsVFXInitializeKeyHash {
  size_t operator()(const AnityGraphicsVFXInitializeKey& key) const {
    const size_t a = std::hash<uint64_t>{}(key.effectId);
    const size_t b = std::hash<int64_t>{}(key.initializeContextId);
    return a ^ (b + static_cast<size_t>(0x9e3779b9u) + (a << 6) + (a >> 2));
  }
};

struct AnityGraphicsVFXParticleSystemKey {
  uint64_t effectId;
  int32_t particleSystemId;
  bool operator==(const AnityGraphicsVFXParticleSystemKey& other) const {
    return effectId == other.effectId && particleSystemId == other.particleSystemId;
  }
};

struct AnityGraphicsVFXParticleSystemKeyHash {
  size_t operator()(const AnityGraphicsVFXParticleSystemKey& key) const {
    const size_t a = std::hash<uint64_t>{}(key.effectId);
    const size_t b = std::hash<int32_t>{}(key.particleSystemId);
    return a ^ (b + static_cast<size_t>(0x9e3779b9u) + (a << 6) + (a >> 2));
  }
};

struct AnityGraphicsVFXPendingUpdate {
  AnityGraphicsVFXUpdateTicketInfo info{};
  std::vector<AnityGraphicsVFXParticleSystemKey> keys;
  std::vector<std::shared_ptr<AnityGraphicsVFXParticleSystemStorage>>
      replacements;
  std::vector<std::shared_ptr<AnityGraphicsVFXParticleSystemStorage>>
      sources;
  std::vector<uint64_t> sourceGenerations;
  std::vector<uint64_t> targetGenerations;
  std::vector<std::vector<uint32_t>> deadIndices;
  std::vector<int32_t> deadCounts;
  uint64_t initializeDependencyTicketId = 0;
  void* backendHandle = nullptr;
  bool cpuReady = false;
  bool residentPublished = false;
  bool centralPublished = false;
  bool frameCommitted = false;
};

struct AnityGraphicsVFXPendingInitialize {
  AnityGraphicsVFXInitializeTicketInfo info{};
  std::unordered_map<AnityGraphicsVFXInitializeKey,
      std::shared_ptr<AnityGraphicsVFXInitializeStorage>,
      AnityGraphicsVFXInitializeKeyHash> stagedDispatches;
  std::unordered_map<AnityGraphicsVFXParticleSystemKey,
      std::shared_ptr<AnityGraphicsVFXParticleSystemStorage>,
      AnityGraphicsVFXParticleSystemKeyHash> stagedSystems;
  std::vector<AnityGraphicsVFXParticleSystemKey> keys;
  std::vector<std::shared_ptr<AnityGraphicsVFXParticleSystemStorage>> systems;
  std::vector<void*> backendHandles;
  std::vector<int32_t> aliveCounts;
  std::vector<int32_t> deadCounts;
  std::vector<uint32_t> nextSequentialIndices;
  std::vector<int32_t> spawnedCounts;
  std::vector<uint64_t> sourceGenerations;
  std::vector<uint64_t> targetGenerations;
  std::vector<int32_t> usesDeadLists;
  std::vector<int32_t> backendCompleted;
  std::vector<int32_t> retainSnapshots;
  std::vector<uint64_t> effectIds;
};

struct AnityGraphicsVFXSpawnerKey {
  uint64_t effectId;
  int64_t contextId;
  bool operator==(const AnityGraphicsVFXSpawnerKey& other) const {
    return effectId == other.effectId && contextId == other.contextId;
  }
};

struct AnityGraphicsVFXSpawnerKeyHash {
  size_t operator()(const AnityGraphicsVFXSpawnerKey& key) const {
    const size_t a = std::hash<uint64_t>{}(key.effectId);
    const size_t b = std::hash<int64_t>{}(key.contextId);
    return a ^ (b + static_cast<size_t>(0x9e3779b9u) + (a << 6) + (a >> 2));
  }
};

struct AnityGraphicsVFXEventRegistry {
  mutable std::mutex mutex;
  std::mutex synchronousUpdateMutex;
  uint64_t generation = 0;
  uint64_t nextUpdateTicketId = 0;
  uint64_t nextInitializeTicketId = 0;
  uint32_t frameIndex = 0;
  uint64_t playerLoopToken = 0;
  uint32_t playerLoopFrameIndex = 0;
  uint64_t cullingFrameToken = 0;
  bool cullingFrameOpen = false;
  std::vector<uint64_t> cullingEffectIds;
  std::unordered_map<uint64_t, bool> cullingCameraIds;
  std::unordered_map<uint64_t, std::shared_ptr<AnityGraphicsVFXEventStorage>> entries;
  std::unordered_map<uint64_t, std::deque<std::shared_ptr<AnityGraphicsVFXEventStorage>>>
      inputEntries;
  std::unordered_map<uint64_t, std::deque<std::shared_ptr<AnityGraphicsVFXEventStorage>>>
      outputEntries;
  std::unordered_map<uint64_t, uint64_t> outputSequences;
  std::unordered_map<AnityGraphicsVFXInitializeKey,
      std::shared_ptr<AnityGraphicsVFXInitializeStorage>,
      AnityGraphicsVFXInitializeKeyHash> initializeDispatches;
  std::unordered_map<AnityGraphicsVFXParticleSystemKey,
      std::shared_ptr<AnityGraphicsVFXParticleSystemStorage>,
      AnityGraphicsVFXParticleSystemKeyHash> particleSystems;
  std::unordered_map<uint64_t,
      std::shared_ptr<AnityGraphicsVFXPendingUpdate>> pendingUpdates;
  std::unordered_map<uint64_t, std::deque<uint64_t>> pendingUpdateByEffect;
  std::unordered_map<uint64_t,
      std::shared_ptr<AnityGraphicsVFXPendingInitialize>> pendingInitializes;
  std::unordered_map<uint64_t, uint64_t> pendingInitializeByEffect;
  std::unordered_map<AnityGraphicsVFXSpawnerKey,
      std::shared_ptr<AnityGraphicsVFXSpawnerStorage>,
      AnityGraphicsVFXSpawnerKeyHash> spawners;
  std::unordered_map<uint64_t, AnityGraphicsVFXFrameStorage> frameStates;
  std::unordered_map<uint64_t, AnityGraphicsVFXCullingStorage> cullingStates;
  std::unordered_map<uint64_t, std::vector<AnityGraphicsVFXPlanarOutputDesc>>
      planarOutputs;
};

void CaptureSpawnerRollback(
    const AnityGraphicsVFXEventRegistry& registry,
    uint64_t effectId,
    AnityGraphicsVFXFrameStorage* frame) {
  frame->spawnerRollback.clear();
  for (const auto& item : registry.spawners) {
    if (item.first.effectId != effectId) continue;
    frame->spawnerRollback.emplace_back(
        item.first.contextId, *item.second);
  }
}

void RestoreSpawnerRollback(
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t effectId,
    const AnityGraphicsVFXFrameStorage& frame) {
  for (auto it = registry->spawners.begin(); it != registry->spawners.end();) {
    if (it->first.effectId == effectId)
      it = registry->spawners.erase(it);
    else
      ++it;
  }
  for (const auto& snapshot : frame.spawnerRollback) {
    const AnityGraphicsVFXSpawnerKey key{effectId, snapshot.first};
    registry->spawners.emplace(
        key, std::make_shared<AnityGraphicsVFXSpawnerStorage>(snapshot.second));
  }
}

void CaptureEffectDataRollback(
    const AnityGraphicsVFXEventRegistry& registry,
    uint64_t effectId,
    AnityGraphicsVFXFrameStorage* frame) {
  frame->initializeRollback.clear();
  frame->particleRollback.clear();
  frame->consumedInputRollback.clear();
  frame->outputRollback.clear();
  for (const auto& item : registry.initializeDispatches) {
    if (item.first.effectId == effectId)
      frame->initializeRollback.emplace_back(
          item.first.initializeContextId, item.second);
  }
  for (const auto& item : registry.particleSystems) {
    if (item.first.effectId == effectId)
      frame->particleRollback.emplace_back(
          item.first.particleSystemId, item.second);
  }
  auto output = registry.outputEntries.find(effectId);
  frame->hadOutputEntries = output != registry.outputEntries.end();
  if (frame->hadOutputEntries) frame->outputRollback = output->second;
  auto sequence = registry.outputSequences.find(effectId);
  frame->hadOutputSequence = sequence != registry.outputSequences.end();
  frame->outputSequenceRollback = frame->hadOutputSequence
      ? sequence->second : 0;
}

void RestoreEffectDataRollback(
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t effectId,
    const AnityGraphicsVFXFrameStorage& frame) {
  if (!frame.consumedInputRollback.empty()) {
    auto current = registry->inputEntries.find(effectId);
    std::deque<std::shared_ptr<AnityGraphicsVFXEventStorage>> restored =
        frame.consumedInputRollback;
    if (current != registry->inputEntries.end()) {
      restored.insert(restored.end(), current->second.begin(), current->second.end());
      registry->inputEntries.erase(current);
    }
    registry->inputEntries.emplace(effectId, std::move(restored));
  }

  for (auto it = registry->initializeDispatches.begin();
       it != registry->initializeDispatches.end();) {
    if (it->first.effectId == effectId)
      it = registry->initializeDispatches.erase(it);
    else
      ++it;
  }
  for (const auto& snapshot : frame.initializeRollback)
    registry->initializeDispatches.emplace(
        AnityGraphicsVFXInitializeKey{effectId, snapshot.first}, snapshot.second);

  for (auto it = registry->particleSystems.begin();
       it != registry->particleSystems.end();) {
    if (it->first.effectId == effectId)
      it = registry->particleSystems.erase(it);
    else
      ++it;
  }
  for (const auto& snapshot : frame.particleRollback)
    registry->particleSystems.emplace(
        AnityGraphicsVFXParticleSystemKey{effectId, snapshot.first}, snapshot.second);

  if (frame.hadOutputEntries)
    registry->outputEntries[effectId] = frame.outputRollback;
  else
    registry->outputEntries.erase(effectId);
  if (frame.hadOutputSequence)
    registry->outputSequences[effectId] = frame.outputSequenceRollback;
  else
    registry->outputSequences.erase(effectId);
}

void ClearEffectDataRollback(AnityGraphicsVFXFrameStorage* frame) {
  frame->initializeRollback.clear();
  frame->particleRollback.clear();
  frame->consumedInputRollback.clear();
  frame->outputRollback.clear();
  frame->outputSequenceRollback = 0;
  frame->hadOutputEntries = false;
  frame->hadOutputSequence = false;
  frame->spawnerRollback.clear();
  frame->hadRollbackState = false;
  frame->allowNonFiniteCommit = false;
}

void InstallRollbackJournal(
    AnityGraphicsVFXFrameStorage* target,
    AnityGraphicsVFXFrameStorage* source) {
  target->spawnerRollback = std::move(source->spawnerRollback);
  target->initializeRollback = std::move(source->initializeRollback);
  target->particleRollback = std::move(source->particleRollback);
  target->consumedInputRollback = std::move(source->consumedInputRollback);
  target->outputRollback = std::move(source->outputRollback);
  target->outputSequenceRollback = source->outputSequenceRollback;
  target->hadOutputEntries = source->hadOutputEntries;
  target->hadOutputSequence = source->hadOutputSequence;
}

AnityGraphicsVFXFrameStorage* GetPreparedFrameStorage(
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t effectId) {
  auto found = registry->frameStates.find(effectId);
  if (found == registry->frameStates.end() || found->second.state.prepared == 0)
    return nullptr;
  return &found->second;
}

extern "C" AnityResult AnityGraphics_Metal_DispatchVFXInitializeCopy(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* desc,
    const uint8_t* sourceRecords, int32_t sourceByteCount,
    uint8_t* outputRecords, int32_t outputByteCount);
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
    int32_t retainSourceGeneration);
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
    int32_t retainSourceGeneration, void** outHandle);
extern "C" AnityResult AnityGraphics_Metal_PollVFXInitializeKernel(
    void* handle, int32_t* outState);
extern "C" AnityResult AnityGraphics_Metal_PublishVFXInitializeKernel(
    void* handle);
extern "C" AnityResult AnityGraphics_Metal_CompleteVFXInitializeKernel(
    void* handle);
extern "C" AnityResult AnityGraphics_Metal_CancelVFXInitializeKernel(
    void* handle);
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
    int32_t* outDeadCounts);
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
    int32_t boundsCount, void** outHandle);
extern "C" AnityResult AnityGraphics_Metal_PollVFXUpdateBatch(
    void* handle, int32_t* outState);
extern "C" AnityResult AnityGraphics_Metal_PollVFXUpdateBatchForPreparation(
    void* handle, int32_t* outState);
extern "C" AnityResult AnityGraphics_Metal_CompleteVFXUpdateBatch(void* handle);
extern "C" AnityResult AnityGraphics_Metal_CancelVFXUpdateBatch(void* handle);
extern "C" AnityResult AnityGraphics_Metal_GetVFXUpdateBackendStats(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId,
    AnityGraphicsVFXUpdateBackendStats* outStats);
extern "C" AnityResult AnityGraphics_Metal_SetVFXFailureInjection(
    AnityGraphicsDevice* device, int32_t failurePoint,
    int32_t failureCount);
extern "C" AnityResult AnityGraphics_Metal_ReadbackVFXResidentParticles(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId, uint64_t generation,
    uint8_t* records, int32_t recordByteCount);
extern "C" AnityResult AnityGraphics_Metal_ReadbackVFXResidentMetadata(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId, uint64_t generation,
    uint32_t* allocationState, int32_t allocationStateCapacity,
    uint32_t* deadList, int32_t deadListCapacity, int32_t* outDeadCount);
extern "C" AnityResult AnityGraphics_Metal_RestoreVFXResidentGeneration(
    AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId, uint64_t generation);
extern "C" void AnityGraphics_Metal_DiscardVFXResidentSnapshots(
    AnityGraphicsDevice* device, uint64_t effectId);
extern "C" void AnityGraphics_Metal_DiscardVFXResidentSnapshot(
    AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId, uint64_t generation);
extern "C" void AnityGraphics_Metal_ClearVFXEffectResources(
    AnityGraphicsDevice* device, uint64_t effectId);
extern "C" AnityResult AnityGraphics_Metal_DrawVFXPlanarCamera(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXPlanarCameraBatchDesc* camera,
    const AnityGraphicsVFXPlanarDrawPacket* packets, int32_t packetCount,
    AnityGraphicsVFXPlanarCameraDrawInfo* outInfo);
extern "C" AnityResult AnityGraphics_Metal_GetVFXPlanarSubmissionStats(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXPlanarSubmissionStats* outStats);
extern "C" AnityResult AnityGraphics_Metal_WaitForVFXPlanarSubmissions(
    AnityGraphicsDevice* device, uint64_t throughSubmissionId,
    int32_t timeoutMilliseconds);
extern "C" AnityResult AnityGraphics_Metal_PublishVFXUpdateBatch(
    void* handle);

extern "C" {
static AnityResult CancelVFXPendingUpdateLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t ticketId);
static AnityResult CancelVFXPendingUpdatesForEffectLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t effectId);
static AnityResult CompleteVFXPendingUpdateLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t ticketId);
static AnityResult FinalizeCommittedVFXPendingUpdateLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t effectId, bool wait, bool* outFinalized);
static AnityResult PollCommittedVFXPendingUpdateForPreparationLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t effectId, bool* outFinalized);
}
extern "C" AnityResult AnityGraphics_Metal_ReduceVFXParticleBounds(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXBoundsReductionDesc* desc,
    const uint8_t* particleRecords, int32_t particleByteCount,
    int32_t capacity, int32_t attributeStrideBytes, int32_t aliveCount,
    uint32_t nextSequentialIndex, uint64_t generation,
    int32_t particleRecordsAuthoritative,
    AnityGraphicsVFXBoundsReductionResult* outResult);

static AnityResult MaterializeVFXParticleAttributesLocked(
    const AnityGraphicsDevice* device,
    AnityGraphicsVFXParticleSystemStorage* storage) {
  if (!device || !storage) return ANITY_ERR_INVALID_ARG;
  if (!storage->attributesResidentOnly) return ANITY_OK;
  if (device->type != ANITY_GFX_METAL || storage->info.generation == 0 ||
      storage->attributes.empty() ||
      storage->attributes.size() >
          static_cast<size_t>(std::numeric_limits<int32_t>::max()))
    return ANITY_ERR_INTERNAL;
  AnityResult result = AnityGraphics_Metal_ReadbackVFXResidentParticles(
      device, storage->info.effectId, storage->info.particleSystemId,
      storage->info.generation, storage->attributes.data(),
      static_cast<int32_t>(storage->attributes.size()));
  if (result == ANITY_OK) storage->attributesResidentOnly = false;
  return result;
}

static AnityResult MaterializeVFXParticleMetadataLocked(
    const AnityGraphicsDevice* device,
    AnityGraphicsVFXParticleSystemStorage* storage) {
  if (!device || !storage) return ANITY_ERR_INVALID_ARG;
  if (device->type != ANITY_GFX_METAL || storage->info.generation == 0)
    return ANITY_OK;
  if (storage->metadataMaterializedGeneration == storage->info.generation)
    return ANITY_OK;
  try {
    std::vector<uint32_t> deadList(
        static_cast<size_t>(storage->info.capacity));
    uint32_t state[4]{};
    int32_t deadCount = 0;
    const AnityResult result =
        AnityGraphics_Metal_ReadbackVFXResidentMetadata(
            device, storage->info.effectId, storage->info.particleSystemId,
            storage->info.generation, state, 4, deadList.data(),
            storage->info.capacity, &deadCount);
    if (result == ANITY_ERR_NOT_SUPPORTED) {
      storage->metadataMaterializedGeneration = storage->info.generation;
      return ANITY_OK;
    }
    if (result != ANITY_OK) return result;
    const uint32_t capacity = static_cast<uint32_t>(storage->info.capacity);
    const uint32_t usesDeadList = storage->usesDeadList ? 1u : 0u;
    if (state[0] > capacity || state[1] > capacity ||
        state[0] + state[1] > capacity || state[3] != usesDeadList ||
        deadCount < 0 || static_cast<uint32_t>(deadCount) != state[1] ||
        (!storage->usesDeadList && deadCount != 0))
      return ANITY_ERR_INTERNAL;
    std::vector<uint8_t> seen(static_cast<size_t>(capacity), 0u);
    for (int32_t index = 0; index < deadCount; ++index) {
      const uint32_t particleIndex = deadList[static_cast<size_t>(index)];
      if (particleIndex >= capacity || seen[particleIndex] != 0u)
        return ANITY_ERR_INTERNAL;
      seen[particleIndex] = 1u;
    }
    storage->info.aliveCount = static_cast<int32_t>(state[0]);
    storage->info.deadCount = deadCount;
    storage->nextSequentialIndex = state[2];
    deadList.resize(static_cast<size_t>(deadCount));
    storage->deadList = std::move(deadList);
    storage->metadataMaterializedGeneration = storage->info.generation;
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

static AnityResult RestoreVFXResidentRollbackLocked(
    AnityGraphicsDevice* device, uint64_t effectId,
    const AnityGraphicsVFXFrameStorage& frame) {
  if (!device || effectId == 0) return ANITY_ERR_INVALID_ARG;
  if (device->type != ANITY_GFX_METAL) return ANITY_OK;
  for (const auto& snapshot : frame.particleRollback) {
    if (!snapshot.second || snapshot.second->info.generation == 0)
      return ANITY_ERR_INTERNAL;
    AnityResult restored = AnityGraphics_Metal_RestoreVFXResidentGeneration(
        device, effectId, snapshot.first,
        snapshot.second->info.generation);
    if (restored != ANITY_OK) return restored;
  }
  AnityGraphics_Metal_DiscardVFXResidentSnapshots(device, effectId);
  return ANITY_OK;
}

namespace {

uint32_t ReadVFXParticleWord(const uint8_t* record, int32_t offset);

constexpr size_t kMaxOutputBatchesPerEffect = 4096;
constexpr size_t kMaxInputBatchesPerEffect = 4096;
constexpr int32_t kMaxInitializeDispatchesPerTransaction = 4096;
constexpr int32_t kMaxSpawnerProgramsPerEffect = 4096;
constexpr int32_t kMaxSpawnerBlocksPerEffect = 65536;
constexpr int32_t kMaxSpawnerEventStrideWords = 1024 * 1024;
constexpr int32_t kMaxCullingEffects = 1024 * 1024;
constexpr int32_t kMaxCullingCameras = 4096;
constexpr int32_t kSpawnerLoopFinished = 0;
constexpr int32_t kSpawnerLoopDelayingBefore = 1;
constexpr int32_t kSpawnerLooping = 2;
constexpr int32_t kSpawnerLoopDelayingAfter = 3;
constexpr int32_t kSpawnerCallbackOnPlay = 1;
constexpr int32_t kSpawnerCallbackOnUpdate = 2;
constexpr int32_t kSpawnerCallbackOnStop = 3;

bool IsFiniteNonNegative(float value) {
  return std::isfinite(value) && value >= 0.0f;
}

bool IsFiniteMatrix(const AnityGraphicsVFXCullingCamera& camera) {
  const float values[] = {
      camera.m00, camera.m01, camera.m02, camera.m03,
      camera.m10, camera.m11, camera.m12, camera.m13,
      camera.m20, camera.m21, camera.m22, camera.m23,
      camera.m30, camera.m31, camera.m32, camera.m33};
  for (float value : values)
    if (!std::isfinite(value)) return false;
  return true;
}

bool IsVisibleInClipSpace(
    const AnityGraphicsVFXCullingBounds& bounds,
    const AnityGraphicsVFXCullingCamera& camera) {
  bool outsideLeft = true;
  bool outsideRight = true;
  bool outsideBottom = true;
  bool outsideTop = true;
  bool outsideNear = true;
  bool outsideFar = true;
  for (int32_t corner = 0; corner < 8; ++corner) {
    const float x = bounds.centerX + ((corner & 1) == 0 ? -bounds.extentsX : bounds.extentsX);
    const float y = bounds.centerY + ((corner & 2) == 0 ? -bounds.extentsY : bounds.extentsY);
    const float z = bounds.centerZ + ((corner & 4) == 0 ? -bounds.extentsZ : bounds.extentsZ);
    const float clipX = camera.m00 * x + camera.m01 * y + camera.m02 * z + camera.m03;
    const float clipY = camera.m10 * x + camera.m11 * y + camera.m12 * z + camera.m13;
    const float clipZ = camera.m20 * x + camera.m21 * y + camera.m22 * z + camera.m23;
    const float clipW = camera.m30 * x + camera.m31 * y + camera.m32 * z + camera.m33;
    outsideLeft &= clipX < -clipW;
    outsideRight &= clipX > clipW;
    outsideBottom &= clipY < -clipW;
    outsideTop &= clipY > clipW;
    outsideNear &= clipZ < -clipW;
    outsideFar &= clipZ > clipW;
  }
  return !(outsideLeft || outsideRight || outsideBottom || outsideTop ||
           outsideNear || outsideFar);
}

int64_t RoundToNearestEvenPositive(double value) {
  const double integral = std::floor(value);
  const double fraction = value - integral;
  if (fraction < 0.5) return static_cast<int64_t>(integral);
  if (fraction > 0.5) return static_cast<int64_t>(integral + 1.0);
  const int64_t candidate = static_cast<int64_t>(integral);
  return (candidate & 1) == 0 ? candidate : candidate + 1;
}

bool IsValidSpawnerRange(float minimum, float maximum) {
  return IsFiniteNonNegative(minimum) && IsFiniteNonNegative(maximum) &&
      minimum <= maximum;
}

bool IsValidPlanarOffset(int32_t offset, int32_t componentCount, int32_t stride) {
  return offset >= 0 && (offset & 3) == 0 && componentCount > 0 &&
      static_cast<int64_t>(offset) + componentCount * 4ll <= stride;
}

bool IsValidPlanarOutput(
    const AnityGraphicsVFXPlanarOutputDesc& output, uint64_t effectId) {
  if (output.version != 1 || (output.flags & ~uint32_t{15}) != 0 ||
      output.effectId != effectId || output.contextId == 0 ||
      output.particleSystemId == 0 || output.primitiveType < 0 ||
      output.primitiveType > 2 || output.particleCapacity <= 0 ||
      output.attributeStrideBytes <= 0 ||
      (output.attributeStrideBytes & 3) != 0 || output.uvMode < 0 ||
      output.uvMode > 4 || output.blendMode < 0 || output.blendMode > 3 ||
      output.cullMode < 0 || output.cullMode > 2 ||
      output.zWrite < 0 || output.zWrite > 1 ||
      output.zTest < 0 || output.zTest > 6 ||
      output.renderQueue < 0 || output.renderQueue > 5000 ||
      output.reserved0 != 0 || output.reserved1 != 0)
    return false;
  const int32_t stride = output.attributeStrideBytes;
  return IsValidPlanarOffset(output.aliveOffsetBytes, 1, stride) &&
      IsValidPlanarOffset(output.positionOffsetBytes, 3, stride) &&
      IsValidPlanarOffset(output.colorOffsetBytes, 3, stride) &&
      IsValidPlanarOffset(output.alphaOffsetBytes, 1, stride) &&
      IsValidPlanarOffset(output.axisXOffsetBytes, 3, stride) &&
      IsValidPlanarOffset(output.axisYOffsetBytes, 3, stride) &&
      IsValidPlanarOffset(output.axisZOffsetBytes, 3, stride) &&
      IsValidPlanarOffset(output.angleXOffsetBytes, 1, stride) &&
      IsValidPlanarOffset(output.angleYOffsetBytes, 1, stride) &&
      IsValidPlanarOffset(output.angleZOffsetBytes, 1, stride) &&
      IsValidPlanarOffset(output.pivotXOffsetBytes, 1, stride) &&
      IsValidPlanarOffset(output.pivotYOffsetBytes, 1, stride) &&
      IsValidPlanarOffset(output.pivotZOffsetBytes, 1, stride) &&
      IsValidPlanarOffset(output.sizeOffsetBytes, 1, stride) &&
      IsValidPlanarOffset(output.scaleXOffsetBytes, 1, stride) &&
      IsValidPlanarOffset(output.scaleYOffsetBytes, 1, stride) &&
      IsValidPlanarOffset(output.scaleZOffsetBytes, 1, stride);
}

bool IsFinitePlanarCamera(const AnityGraphicsVFXPlanarCameraDesc& camera) {
  if (camera.cameraId == 0 || camera.cameraType < 0 ||
      (camera.flags & ~1) != 0 || camera.reserved != 0)
    return false;
  for (int32_t index = 0; index < 16; ++index)
    if (!std::isfinite(camera.localToWorld[index]) ||
        !std::isfinite(camera.worldToClip[index])) return false;
  return true;
}

bool IsFinitePlanarCameraBatch(
    const AnityGraphicsVFXPlanarCameraBatchDesc& camera) {
  if (camera.cameraId == 0 || camera.cameraType < 0 ||
      (camera.flags & ~1) != 0 || camera.reserved != 0)
    return false;
  for (int32_t index = 0; index < 16; ++index)
    if (!std::isfinite(camera.worldToClip[index])) return false;
  return true;
}

bool IsFinitePlanarEffect(const AnityGraphicsVFXPlanarEffectDesc& effect) {
  constexpr int32_t kMaxPlanarSortOrder = 1 << 20;
  if (effect.effectId == 0 || effect.layer < 0 || effect.layer > 31 ||
      effect.sortOrder < -kMaxPlanarSortOrder ||
      effect.sortOrder > kMaxPlanarSortOrder)
    return false;
  for (int32_t index = 0; index < 16; ++index)
    if (!std::isfinite(effect.localToWorld[index])) return false;
  return true;
}

bool IsValidSpawnerProgram(const AnityGraphicsVFXSpawnerProgramDesc& program,
                           uint64_t effectId, int32_t blockCount) {
  if ((program.version != 4 && program.version != 5) || program.eventStrideWords >
          static_cast<uint32_t>(kMaxSpawnerEventStrideWords) || program.effectId != effectId ||
      program.contextId == 0 || program.systemId == 0 || program.blockStart < 0 ||
      program.blockCount <= 0 ||
      static_cast<int64_t>(program.blockStart) + program.blockCount > blockCount)
    return false;
  if (program.loopDurationMode < 1 || program.loopDurationMode > 3 ||
      program.loopCountMode < 1 || program.loopCountMode > 3 ||
      program.delayBeforeLoopMode < 0 || program.delayBeforeLoopMode > 2 ||
      program.delayAfterLoopMode < 0 || program.delayAfterLoopMode > 2)
    return false;
  if (!IsValidSpawnerRange(program.loopDurationMin, program.loopDurationMax) ||
      !IsValidSpawnerRange(program.delayBeforeLoopMin, program.delayBeforeLoopMax) ||
      !IsValidSpawnerRange(program.delayAfterLoopMin, program.delayAfterLoopMax) ||
      !std::isfinite(program.loopCountMin) || !std::isfinite(program.loopCountMax) ||
      program.loopCountMin < 0.0 || program.loopCountMax < program.loopCountMin ||
      program.loopCountMax > static_cast<double>(std::numeric_limits<int32_t>::max()))
    return false;
  if ((program.loopCountMode == 3 &&
       (program.loopCountMin != 0.0 || program.loopCountMax != 0.0)) ||
      (program.loopCountMode == 1 &&
       (program.loopCountMin != program.loopCountMax ||
        program.loopCountMin != std::trunc(program.loopCountMin))) ||
      (program.loopCountMode == 2 && program.loopCountMax > 2147483520.0))
    return false;
  return true;
}

bool IsValidSpawnerBlock(const AnityGraphicsVFXSpawnerBlockDesc& block) {
  if (block.blockId == 0 || block.kind < 1 || block.kind > 5 ||
      block.periodic < 0 || block.periodic > 1 ||
      block.valueWordCount < 0 || block.valueWordCount > 4)
    return false;
  if (block.kind == 5) {
    return block.periodic == 0 && block.valueMin == 0.0f &&
        block.valueMax == 0.0f && block.periodMin == 0.0f &&
        block.periodMax == 0.0f && block.targetOffsetWords == -1 &&
        block.valueType == 4 && block.randomMode == 0 &&
        block.valueWordCount == 0;
  }
  if (block.kind == 4) {
    if (block.periodic != 0 || block.valueMin != 0.0f || block.valueMax != 0.0f ||
        block.periodMin != 0.0f || block.periodMax != 0.0f ||
        block.targetOffsetWords < 0 || block.valueType < 1 || block.valueType > 7 ||
        block.randomMode < 0 || block.randomMode > 2 || block.valueWordCount < 1)
      return false;
    const int32_t expectedWords = block.valueType <= 4 ? 1 : block.valueType - 3;
    if (block.valueWordCount != expectedWords ||
        (block.randomMode != 0 && block.valueType < 4))
      return false;
    if (block.valueType >= 4) {
      for (int32_t index = 0; index < block.valueWordCount; ++index) {
        float first = 0.0f;
        float second = 0.0f;
        std::memcpy(&first, &block.valueA[index], sizeof(float));
        std::memcpy(&second, &block.valueB[index], sizeof(float));
        if (!std::isfinite(first) || !std::isfinite(second)) return false;
      }
    }
    return true;
  }
  if (!IsValidSpawnerRange(block.valueMin, block.valueMax) ||
      !IsValidSpawnerRange(block.periodMin, block.periodMax) ||
      block.targetOffsetWords != -1 || block.valueType != 4 ||
      block.randomMode != 0 || block.valueWordCount != 0)
    return false;
  if (block.kind != 3 && block.periodic != 0) return false;
  return true;
}

uint32_t NextSpawnerRandom(AnityGraphicsVFXSpawnerStorage& storage) {
  uint32_t value = storage.randomState[0] ^ (storage.randomState[0] << 11);
  storage.randomState[0] = storage.randomState[1];
  storage.randomState[1] = storage.randomState[2];
  storage.randomState[2] = storage.randomState[3];
  storage.randomState[3] = storage.randomState[3] ^
      (storage.randomState[3] >> 19) ^ value ^ (value >> 8);
  return storage.randomState[3];
}

float SpawnerRandomRange(AnityGraphicsVFXSpawnerStorage& storage,
                         float minimum, float maximum) {
  if (minimum == maximum) return minimum;
  const float unit = static_cast<float>(NextSpawnerRandom(storage) & 0x007fffffu) /
      8388607.0f;
  const volatile float scaled = (maximum - minimum) * unit;
  return minimum + scaled;
}

uint32_t SampleSpawnerAttributeWord(
    AnityGraphicsVFXSpawnerStorage& storage,
    const AnityGraphicsVFXSpawnerBlockDesc& block,
    int32_t index,
    float uniform) {
  uint32_t word = block.valueA[index];
  if (block.randomMode == 0) return word;
  const float unit = block.randomMode == 2
      ? uniform
      : static_cast<float>(NextSpawnerRandom(storage) & 0x007fffffu) / 8388607.0f;
  float minimum = 0.0f;
  float maximum = 0.0f;
  std::memcpy(&minimum, &block.valueA[index], sizeof(float));
  std::memcpy(&maximum, &block.valueB[index], sizeof(float));
  const volatile float scaled = (maximum - minimum) * unit;
  const float sampled = minimum + scaled;
  std::memcpy(&word, &sampled, sizeof(uint32_t));
  return word;
}

void ExecuteSpawnerSetAttribute(
    AnityGraphicsVFXSpawnerStorage& storage,
    const AnityGraphicsVFXSpawnerBlockDesc& block,
    float& spawnCount) {
  float uniform = 0.0f;
  if (block.randomMode == 2)
    uniform = static_cast<float>(NextSpawnerRandom(storage) & 0x007fffffu) / 8388607.0f;
  for (int32_t index = 0; index < block.valueWordCount; ++index) {
    const uint32_t word = SampleSpawnerAttributeWord(storage, block, index, uniform);
    const int32_t target = block.targetOffsetWords + index;
    if (target == 0) {
      std::memcpy(&spawnCount, &word, sizeof(float));
    } else {
      storage.eventRecord[static_cast<size_t>(target)] = word;
    }
  }
}

void FinalizeSpawnerEvent(AnityGraphicsVFXSpawnerStorage& storage) {
  storage.state.eventSpawnCount = 0.0f;
  if (!(storage.state.spawnCount > 0.0f)) return;
  if (!std::isfinite(storage.state.spawnCount)) {
    storage.state.eventSpawnCount = storage.state.spawnCount;
    storage.eventAccumulator = 0.0f;
  } else {
    storage.eventAccumulator += storage.state.spawnCount;
    if (storage.eventAccumulator < 1.0f) return;
    storage.state.eventSpawnCount = storage.eventAccumulator;
    storage.eventAccumulator -= std::floor(storage.eventAccumulator);
  }
  if (!storage.eventRecord.empty())
    std::memcpy(storage.eventRecord.data(), &storage.state.eventSpawnCount, sizeof(float));
}

void ResetSpawnerBlocks(AnityGraphicsVFXSpawnerStorage& storage) {
  for (auto& block : storage.blocks) {
    block.currentValue = 0.0f;
    block.intervalRemaining = 0.0f;
    block.intervalInitialized = false;
    block.burstFinished = false;
  }
}

void ResetSpawnerRandom(AnityGraphicsVFXSpawnerStorage& storage, uint32_t seed) {
  storage.randomState[0] = seed;
  for (int32_t index = 1; index < 4; ++index)
    storage.randomState[index] =
        1812433253u * storage.randomState[index - 1] + 1u;
  storage.randomInitialized = true;
}

float SampleSpawnerValue(AnityGraphicsVFXSpawnerStorage& storage, int32_t mode,
                         float minimum, float maximum) {
  if (mode == 0) return 0.0f;
  if (mode == 3) return -1.0f;
  return mode == 2 ? SpawnerRandomRange(storage, minimum, maximum) : minimum;
}

void EnterSpawnerLoop(AnityGraphicsVFXSpawnerStorage& storage) {
  storage.phaseTime = 0.0f;
  storage.state.loopState = kSpawnerLooping;
  storage.state.playing = 1;
  ResetSpawnerBlocks(storage);
}

void PrepareSpawnerLoop(AnityGraphicsVFXSpawnerStorage& storage) {
  storage.state.loopDuration = SampleSpawnerValue(
      storage, storage.desc.loopDurationMode,
      storage.desc.loopDurationMin, storage.desc.loopDurationMax);
  storage.state.delayBeforeLoop = SampleSpawnerValue(
      storage, storage.desc.delayBeforeLoopMode,
      storage.desc.delayBeforeLoopMin, storage.desc.delayBeforeLoopMax);
  storage.state.delayAfterLoop = SampleSpawnerValue(
      storage, storage.desc.delayAfterLoopMode,
      storage.desc.delayAfterLoopMin, storage.desc.delayAfterLoopMax);
  storage.phaseTime = 0.0f;
  if (storage.state.delayBeforeLoop > 0.0f) {
    storage.state.loopState = kSpawnerLoopDelayingBefore;
    storage.state.playing = 0;
  } else {
    EnterSpawnerLoop(storage);
  }
}

void PrepareFirstSpawnerLoop(AnityGraphicsVFXSpawnerStorage& storage) {
  // Unity 2022 evaluates the CPU expression graph in Count, Duration,
  // Before, After order. Each random expression advances the component's
  // UnityEngine.Random-compatible xorshift128 stream exactly once.
  storage.state.loopCount = storage.desc.loopCountMode == 3
      ? -1
      : (storage.desc.loopCountMode == 2
          ? static_cast<int32_t>(SpawnerRandomRange(
                storage, static_cast<float>(storage.desc.loopCountMin),
                static_cast<float>(storage.desc.loopCountMax)))
          : static_cast<int32_t>(storage.desc.loopCountMin));
  storage.state.loopDuration = SampleSpawnerValue(
      storage, storage.desc.loopDurationMode,
      storage.desc.loopDurationMin, storage.desc.loopDurationMax);
  storage.state.delayBeforeLoop = SampleSpawnerValue(
      storage, storage.desc.delayBeforeLoopMode,
      storage.desc.delayBeforeLoopMin, storage.desc.delayBeforeLoopMax);
  storage.state.delayAfterLoop = SampleSpawnerValue(
      storage, storage.desc.delayAfterLoopMode,
      storage.desc.delayAfterLoopMin, storage.desc.delayAfterLoopMax);
  storage.phaseTime = 0.0f;
  if (storage.state.delayBeforeLoop > 0.0f) {
    storage.state.loopState = kSpawnerLoopDelayingBefore;
    storage.state.playing = 0;
  } else {
    EnterSpawnerLoop(storage);
  }
}

bool HasSpawnerCallback(const AnityGraphicsVFXSpawnerStorage& storage) {
  return std::any_of(storage.blocks.begin(), storage.blocks.end(),
      [](const AnityGraphicsVFXSpawnerBlockStorage& block) {
        return block.desc.kind == 5;
      });
}

bool IsValidCallbackState(
    const AnityGraphicsVFXSpawnerStorage& storage,
    const AnityGraphicsVFXSpawnerState& state,
    uint64_t generation) {
  return state.effectId == storage.desc.effectId &&
      state.contextId == storage.desc.contextId &&
      state.systemId == storage.desc.systemId && state.generation == generation &&
      state.loopState >= kSpawnerLoopFinished &&
      state.loopState <= kSpawnerLoopDelayingAfter &&
      state.playing >= 0 && state.playing <= 1 &&
      state.newLoop >= 0 && state.newLoop <= 1 &&
      (state.playing != 0) == (state.loopState == kSpawnerLooping) &&
      std::isfinite(state.spawnCount) &&
      IsFiniteNonNegative(state.delayBeforeLoop) &&
      std::isfinite(state.loopDuration) &&
      (state.loopDuration >= 0.0f || state.loopDuration == -1.0f) &&
      IsFiniteNonNegative(state.delayAfterLoop) && state.loopIndex >= 0 &&
      state.loopCount >= -1;
}

AnityResult InvokeSpawnerCallback(
    AnityGraphicsVFXSpawnerStorage& storage,
    const AnityGraphicsVFXSpawnerBlockDesc& block,
    int32_t phase,
    AnityGraphicsVFXSpawnerCallback callback,
    void* userData,
    bool exposeEventRecord) {
  if (!callback) return ANITY_ERR_INVALID_ARG;
  const uint64_t generation = storage.state.generation;
  const float previousTotalTime = storage.state.totalTime;
  uint8_t* record = exposeEventRecord && !storage.eventRecord.empty()
      ? reinterpret_cast<uint8_t*>(storage.eventRecord.data())
      : nullptr;
  const int32_t recordBytes = exposeEventRecord
      ? static_cast<int32_t>(storage.eventRecord.size() * sizeof(uint32_t))
      : 0;
  const AnityResult result = callback(
      userData, block.blockId, phase, &storage.state, record, recordBytes);
  if (result != ANITY_OK) return result;
  if (!IsValidCallbackState(storage, storage.state, generation))
    return ANITY_ERR_INVALID_ARG;
  if (storage.state.totalTime != previousTotalTime)
    storage.phaseTime = storage.state.totalTime;
  return ANITY_OK;
}

AnityResult InvokeSpawnerControlCallbacks(
    AnityGraphicsVFXSpawnerStorage& storage,
    int32_t phase,
    AnityGraphicsVFXSpawnerCallback callback,
    void* userData) {
  if (!HasSpawnerCallback(storage)) return ANITY_OK;
  storage.state.spawnCount = 1.0f;
  for (const auto& block : storage.blocks) {
    if (block.desc.kind != 5) continue;
    const AnityResult result = InvokeSpawnerCallback(
        storage, block.desc, phase, callback, userData, false);
    if (result != ANITY_OK) return result;
  }
  // Unity exposes one as the control callback sentinel, but it is not an
  // Initialize dispatch count and does not survive the control operation.
  storage.state.spawnCount = 0.0f;
  storage.state.eventSpawnCount = 0.0f;
  return ANITY_OK;
}

AnityResult StartSpawner(AnityGraphicsVFXSpawnerStorage& storage, uint32_t seed,
                         bool resetSeed,
                         AnityGraphicsVFXSpawnerCallback callback,
                         void* userData) {
  if (resetSeed || !storage.randomInitialized) ResetSpawnerRandom(storage, seed);
  ResetSpawnerBlocks(storage);
  storage.eventRecord = storage.eventRecordDefaults;
  storage.eventAccumulator = 0.0f;
  storage.pendingControlEvent = false;
  storage.state.spawnCount = 0.0f;
  storage.state.eventSpawnCount = 0.0f;
  storage.state.totalTime = 0.0f;
  storage.state.newLoop = 1;
  storage.state.loopIndex = 0;
  PrepareFirstSpawnerLoop(storage);
  storage.newLoopOnNextTick = true;
  storage.prepareLoopOnNextTick = false;
  return InvokeSpawnerControlCallbacks(
      storage, kSpawnerCallbackOnPlay, callback, userData);
}

AnityResult StopSpawner(AnityGraphicsVFXSpawnerStorage& storage,
                        AnityGraphicsVFXSpawnerCallback callback,
                        void* userData) {
  ResetSpawnerBlocks(storage);
  storage.eventRecord = storage.eventRecordDefaults;
  storage.eventAccumulator = 0.0f;
  storage.state.loopState = kSpawnerLoopFinished;
  storage.state.playing = 0;
  storage.state.newLoop = 0;
  storage.state.spawnCount = 0.0f;
  storage.state.eventSpawnCount = 0.0f;
  storage.state.totalTime = 0.0f;
  storage.phaseTime = 0.0f;
  storage.newLoopOnNextTick = false;
  storage.prepareLoopOnNextTick = false;
  const AnityResult callbackResult = InvokeSpawnerControlCallbacks(
      storage, kSpawnerCallbackOnStop, callback, userData);
  if (callbackResult != ANITY_OK) return callbackResult;
  for (const auto& block : storage.blocks)
    if (block.desc.kind == 4)
      ExecuteSpawnerSetAttribute(storage, block.desc, storage.state.spawnCount);
  FinalizeSpawnerEvent(storage);
  storage.pendingControlEvent = storage.state.eventSpawnCount >= 1.0f;
  return ANITY_OK;
}

AnityResult TickSpawnerBlock(AnityGraphicsVFXSpawnerStorage& storage,
                             AnityGraphicsVFXSpawnerBlockStorage& block,
                             float& spawnCount,
                             AnityGraphicsVFXSpawnerCallback callback,
                             void* userData) {
  if (block.desc.kind == 5) {
    storage.state.spawnCount = spawnCount;
    const AnityResult result = InvokeSpawnerCallback(
        storage, block.desc, kSpawnerCallbackOnUpdate, callback, userData, true);
    spawnCount = storage.state.spawnCount;
    return result;
  }
  if (block.desc.kind == 4) {
    ExecuteSpawnerSetAttribute(storage, block.desc, spawnCount);
    return ANITY_OK;
  }
  if (storage.state.loopState != kSpawnerLooping) return ANITY_OK;
  const float deltaTime = storage.state.deltaTime;
  if (block.desc.kind == 1) {
    spawnCount += block.desc.valueMin * deltaTime;
    return ANITY_OK;
  }
  if (block.desc.kind == 2) {
    float remaining = deltaTime;
    int32_t guard = 0;
    while (remaining > 0.0f) {
      if (!block.intervalInitialized || block.intervalRemaining <= 0.0f) {
        block.currentValue = SpawnerRandomRange(
            storage, block.desc.valueMin, block.desc.valueMax);
        block.intervalRemaining = SpawnerRandomRange(
            storage, block.desc.periodMin, block.desc.periodMax);
        block.intervalInitialized = true;
        if (block.intervalRemaining <= 0.0f) block.intervalRemaining = remaining;
      }
      const float step = std::min(remaining, block.intervalRemaining);
      spawnCount += block.currentValue * step;
      remaining -= step;
      block.intervalRemaining -= step;
      if (++guard > 1000000) break;
    }
    return ANITY_OK;
  }
  if (block.burstFinished) return ANITY_OK;
  if (!block.intervalInitialized) {
    block.intervalRemaining = SpawnerRandomRange(
        storage, block.desc.periodMin, block.desc.periodMax);
    block.intervalInitialized = true;
  }
  block.intervalRemaining -= deltaTime;
  int32_t guard = 0;
  while (block.intervalRemaining <= 0.0f && !block.burstFinished) {
    spawnCount += SpawnerRandomRange(storage, block.desc.valueMin, block.desc.valueMax);
    if (block.desc.periodic == 0) {
      block.burstFinished = true;
      break;
    }
    const float period = SpawnerRandomRange(
        storage, block.desc.periodMin, block.desc.periodMax);
    if (period <= 0.0f) {
      block.intervalRemaining = 0.0f;
      break;
    }
    block.intervalRemaining += period;
    if (++guard > 1000000) break;
  }
  return ANITY_OK;
}

AnityResult TickSpawnerBlocks(AnityGraphicsVFXSpawnerStorage& storage,
                              bool inactiveOnly,
                              AnityGraphicsVFXSpawnerCallback callback,
                              void* userData,
                              float* outSpawnCount) {
  if (!outSpawnCount) return ANITY_ERR_INVALID_ARG;
  float spawnCount = 0.0f;
  for (auto& block : storage.blocks) {
    if (inactiveOnly && block.desc.kind != 4 && block.desc.kind != 5) continue;
    const AnityResult result = TickSpawnerBlock(
        storage, block, spawnCount, callback, userData);
    if (result != ANITY_OK) return result;
  }
  *outSpawnCount = spawnCount;
  return ANITY_OK;
}

void CompleteSpawnerLoop(AnityGraphicsVFXSpawnerStorage& storage) {
  ++storage.state.loopIndex;
  if (storage.state.loopCount >= 0 &&
      storage.state.loopIndex >= storage.state.loopCount) {
    storage.state.loopState = kSpawnerLoopFinished;
    storage.state.playing = 0;
    storage.state.newLoop = 0;
    storage.phaseTime = 0.0f;
    return;
  }
  storage.phaseTime = 0.0f;
  storage.state.loopState = storage.state.delayBeforeLoop > 0.0f
      ? kSpawnerLoopDelayingBefore
      : kSpawnerLooping;
  storage.state.playing = storage.state.loopState == kSpawnerLooping ? 1 : 0;
  storage.prepareLoopOnNextTick = true;
  storage.newLoopOnNextTick = true;
}

AnityResult TickSpawner(AnityGraphicsVFXSpawnerStorage& storage, float deltaTime,
                        AnityGraphicsVFXSpawnerCallback callback,
                        void* userData) {
  storage.state.deltaTime = deltaTime;
  if (storage.pendingControlEvent) {
    storage.pendingControlEvent = false;
    storage.state.totalTime = storage.phaseTime;
    return ANITY_OK;
  }
  storage.state.spawnCount = 0.0f;
  storage.state.eventSpawnCount = 0.0f;
  if (storage.prepareLoopOnNextTick) {
    PrepareSpawnerLoop(storage);
    storage.prepareLoopOnNextTick = false;
  }
  storage.state.newLoop = storage.newLoopOnNextTick ? 1 : 0;
  storage.newLoopOnNextTick = false;
  if (deltaTime == 0.0f) {
    float spawnCount = 0.0f;
    const AnityResult result = TickSpawnerBlocks(
        storage, true, callback, userData, &spawnCount);
    if (result != ANITY_OK) return result;
    storage.state.spawnCount = spawnCount;
    FinalizeSpawnerEvent(storage);
    storage.state.totalTime = storage.phaseTime;
    return ANITY_OK;
  }
  if (storage.state.loopState == kSpawnerLoopFinished && !HasSpawnerCallback(storage)) {
    storage.phaseTime += deltaTime;
    storage.state.totalTime = storage.phaseTime;
    return ANITY_OK;
  }
  storage.phaseTime += deltaTime;
  bool enteredLoopThisTick = false;
  if (storage.state.loopState == kSpawnerLoopDelayingBefore) {
    if (storage.phaseTime >= storage.state.delayBeforeLoop) {
      EnterSpawnerLoop(storage);
      enteredLoopThisTick = true;
    }
  }
  if ((storage.state.loopState == kSpawnerLooping && !enteredLoopThisTick) ||
      HasSpawnerCallback(storage)) {
    float spawnCount = 0.0f;
    const AnityResult result = TickSpawnerBlocks(
        storage, storage.state.loopState != kSpawnerLooping || enteredLoopThisTick,
        callback, userData, &spawnCount);
    if (result != ANITY_OK) return result;
    storage.state.spawnCount = spawnCount;
    FinalizeSpawnerEvent(storage);
    if (storage.state.loopState == kSpawnerLooping &&
        storage.desc.loopDurationMode != 3 &&
        storage.phaseTime >= storage.state.loopDuration) {
      storage.phaseTime = 0.0f;
      if (storage.state.delayAfterLoop > 0.0f) {
        storage.state.loopState = kSpawnerLoopDelayingAfter;
        storage.state.playing = 0;
      } else {
        CompleteSpawnerLoop(storage);
      }
    }
  }
  if (storage.state.loopState == kSpawnerLoopDelayingAfter &&
      storage.phaseTime >= storage.state.delayAfterLoop) {
    CompleteSpawnerLoop(storage);
  }
  storage.state.totalTime = storage.phaseTime;
  return ANITY_OK;
}

bool IsValidUpload(const AnityGraphicsVFXEventUploadDesc& desc,
                   const uint8_t* records, int32_t byteCount) {
  if (desc.effectId == 0 || desc.sequence == 0 || desc.recordCount < 0 ||
      desc.strideBytes < 0 || byteCount < 0 || (desc.strideBytes & 3) != 0)
    return false;
  if (desc.recordCount == 0)
    return desc.strideBytes == 0 && byteCount == 0;
  if (!records || desc.strideBytes == 0) return false;
  int64_t required = static_cast<int64_t>(desc.recordCount) * desc.strideBytes;
  return required <= std::numeric_limits<int32_t>::max() && byteCount == required;
}

bool IsValidInitializeDispatch(
    const AnityGraphicsVFXInitializeDispatchDesc& desc,
    const uint8_t* sourceRecords, int32_t sourceByteCount,
    int32_t* outOutputByteCount) {
  if (!outOutputByteCount || desc.effectId == 0 || desc.sequence == 0 ||
      desc.initializeContextId == 0 || desc.startEventIndex < 0 ||
      desc.recordCount <= 0 || desc.strideBytes <= 0 ||
      (desc.strideBytes & 3) != 0 || sourceByteCount < 0 || !sourceRecords)
    return false;
  const int64_t outputBytes =
      static_cast<int64_t>(desc.recordCount) * desc.strideBytes;
  const int64_t requiredSourceBytes =
      (static_cast<int64_t>(desc.startEventIndex) + desc.recordCount) *
      desc.strideBytes;
  if (outputBytes > std::numeric_limits<int32_t>::max() ||
      requiredSourceBytes > sourceByteCount)
    return false;
  *outOutputByteCount = static_cast<int32_t>(outputBytes);
  return true;
}

bool MatchesInitializeDispatch(
    const AnityGraphicsVFXInitializeStorage& stored,
    const AnityGraphicsVFXInitializeDispatchDesc& desc,
    const uint8_t* sourceRecords, int32_t,
    int32_t outputByteCount) {
  const auto& current = stored.info.desc;
  if (current.effectId != desc.effectId || current.sequence != desc.sequence ||
      current.initializeContextId != desc.initializeContextId ||
      current.sourceSpawnerContextId != desc.sourceSpawnerContextId ||
      current.eventNameId != desc.eventNameId ||
      current.particleSystemId != desc.particleSystemId ||
      current.spawnSystemId != desc.spawnSystemId ||
      current.startEventIndex != desc.startEventIndex ||
      current.recordCount != desc.recordCount ||
      current.strideBytes != desc.strideBytes ||
      stored.info.outputByteCount != outputByteCount ||
      stored.records.size() != static_cast<size_t>(outputByteCount))
    return false;
  const size_t sourceOffset =
      static_cast<size_t>(desc.startEventIndex) * desc.strideBytes;
  return std::memcmp(stored.records.data(), sourceRecords + sourceOffset,
                     static_cast<size_t>(outputByteCount)) == 0;
}

int32_t VFXValueComponentCount(int32_t valueType) {
  switch (valueType) {
    case 1: case 2: case 3: case 4: return 1;
    case 5: return 2;
    case 6: return 3;
    case 7: return 4;
    default: return 0;
  }
}

bool IsValidInitializeKernel(
    const AnityGraphicsVFXInitializeDispatchDesc& dispatch,
    const AnityGraphicsVFXInitializeKernelDesc& kernel,
    const AnityGraphicsVFXInitializeAttributeDesc* attributes,
    int32_t attributeCount,
    const AnityGraphicsVFXInitializeOperationDesc* operations,
    int32_t operationCount) {
  if (kernel.version == 0)
    return kernel.flags == 0 && kernel.particleCapacity == 0 &&
        kernel.attributeStrideBytes == 0 && kernel.sourceStrideBytes == 0 &&
        kernel.attributeStart == 0 && kernel.attributeCount == 0 &&
        kernel.operationStart == 0 && kernel.operationCount == 0 &&
        kernel.spawnCountSourceOffsetBytes == 0 && kernel.systemSeed == 0;
  if ((kernel.version != 1 && kernel.version != 2) || (kernel.flags & ~1u) != 0 ||
      kernel.particleCapacity <= 0 || kernel.attributeStrideBytes <= 0 ||
      (kernel.attributeStrideBytes & 3) != 0 || kernel.sourceStrideBytes <= 0 ||
      kernel.sourceStrideBytes != dispatch.strideBytes ||
      kernel.attributeStart < 0 || kernel.attributeCount <= 0 ||
      kernel.operationStart < 0 || kernel.operationCount < 0 ||
      static_cast<int64_t>(kernel.attributeStart) + kernel.attributeCount > attributeCount ||
      static_cast<int64_t>(kernel.operationStart) + kernel.operationCount > operationCount)
    return false;
  if ((kernel.version == 1 && kernel.spawnCountSourceOffsetBytes != -1) ||
      (kernel.version == 2 &&
       (kernel.spawnCountSourceOffsetBytes < 0 ||
        (kernel.spawnCountSourceOffsetBytes & 3) != 0 ||
        static_cast<int64_t>(kernel.spawnCountSourceOffsetBytes) + 4 >
            kernel.sourceStrideBytes)))
    return false;
  const auto* kernelAttributes = attributes + kernel.attributeStart;
  const auto* kernelOperations = kernel.operationCount == 0
      ? nullptr : operations + kernel.operationStart;
  int32_t expectedOffset = 0;
  bool hasAlive = false;
  for (int32_t i = 0; i < kernel.attributeCount; ++i) {
    const auto& attribute = kernelAttributes[i];
    const int32_t components = VFXValueComponentCount(attribute.valueType);
    if (components == 0 || attribute.componentCount != components ||
        attribute.offsetBytes != expectedOffset || attribute.semantic < 0 ||
        attribute.semantic > 1)
      return false;
    if (attribute.semantic == 1) {
      if (hasAlive || attribute.valueType != 1) return false;
      hasAlive = true;
    }
    expectedOffset += components * static_cast<int32_t>(sizeof(uint32_t));
  }
  if (expectedOffset != kernel.attributeStrideBytes ||
      ((kernel.flags & 1u) != 0 && !hasAlive))
    return false;
  for (int32_t i = 0; i < kernel.operationCount; ++i) {
    const auto& operation = kernelOperations[i];
    const int32_t components = VFXValueComponentCount(operation.valueType);
    if (components == 0 || operation.componentCount != components ||
        operation.targetOffsetBytes < 0 ||
        static_cast<int64_t>(operation.targetOffsetBytes) + components * 4 >
            kernel.attributeStrideBytes ||
        operation.valueSource < 0 || operation.valueSource > 4 ||
        operation.composition < 0 || operation.composition > 3 ||
        operation.randomMode < 0 || operation.randomMode > 2 ||
        operation.reserved != 0)
      return false;
    bool exactTarget = false;
    for (int32_t j = 0; j < kernel.attributeCount; ++j) {
      const auto& attribute = kernelAttributes[j];
      if (attribute.offsetBytes == operation.targetOffsetBytes &&
          attribute.valueType == operation.valueType &&
          attribute.componentCount == operation.componentCount) {
        exactTarget = true;
        break;
      }
    }
    if (!exactTarget) return false;
    if (operation.valueSource == 1) {
      if (operation.sourceOffsetBytes < 0 || operation.randomMode != 0 ||
          static_cast<int64_t>(operation.sourceOffsetBytes) + components * 4 >
              kernel.sourceStrideBytes)
        return false;
    } else if (operation.valueSource == 0) {
      if (operation.sourceOffsetBytes != -1) return false;
      if (operation.randomMode != 0 && operation.valueType < 4) return false;
    } else if (operation.sourceOffsetBytes != -1 || operation.valueType != 2 ||
               operation.componentCount != 1 || operation.randomMode != 0 ||
               operation.composition != 0) {
      return false;
    }
    if (operation.valueType == 1 && operation.composition != 0) return false;
    if (operation.composition == 3 && operation.valueType < 4) return false;
  }
  const int64_t particleBytes =
      static_cast<int64_t>(kernel.particleCapacity) * kernel.attributeStrideBytes;
  return particleBytes <= std::numeric_limits<int32_t>::max();
}

uint32_t VFXHash(uint32_t value) {
  value = (value ^ 61u) ^ (value >> 16);
  value += value << 3;
  value ^= value >> 4;
  value *= 0x27d4eb2du;
  value ^= value >> 15;
  return value;
}

float VFXWordToFloat(uint32_t word) {
  float value;
  std::memcpy(&value, &word, sizeof(value));
  return value;
}

uint32_t VFXFloatToWord(float value) {
  uint32_t word;
  std::memcpy(&word, &value, sizeof(word));
  return word;
}

float VFXRandom(uint32_t* state) {
  *state = VFXHash(*state);
  return static_cast<float>(*state & 0x00ffffffu) *
         (1.0f / 16777216.0f);
}

uint64_t VFXHashBytes(uint64_t state, const void* bytes, size_t count) {
  const auto* current = static_cast<const uint8_t*>(bytes);
  for (size_t index = 0; index < count; ++index) {
    state ^= current[index];
    state *= 1099511628211ull;
  }
  return state;
}

uint64_t VFXKernelFingerprint(
    const AnityGraphicsVFXInitializeKernelDesc& kernel,
    const AnityGraphicsVFXInitializeAttributeDesc* attributes,
    const AnityGraphicsVFXInitializeOperationDesc* operations) {
  uint64_t state = 1469598103934665603ull;
  // attributeStart/operationStart are transaction-local packing offsets. They
  // are not part of a kernel's semantics and can legitimately change when an
  // otherwise identical retry orders its dispatches differently.
  auto normalizedKernel = kernel;
  normalizedKernel.attributeStart = 0;
  normalizedKernel.operationStart = 0;
  state = VFXHashBytes(state, &normalizedKernel, sizeof(normalizedKernel));
  state = VFXHashBytes(state, attributes,
      static_cast<size_t>(kernel.attributeCount) * sizeof(*attributes));
  return VFXHashBytes(state, operations,
      static_cast<size_t>(kernel.operationCount) * sizeof(*operations));
}

struct VFXSpawnPlan {
  std::vector<uint32_t> inclusivePrefix;
  int32_t candidateCount = 0;
};

bool BuildVFXSpawnPlan(
    const AnityGraphicsVFXInitializeDispatchDesc& dispatch,
    const AnityGraphicsVFXInitializeKernelDesc& kernel,
    const uint8_t* sourceRecords,
    VFXSpawnPlan* outPlan) {
  if (!sourceRecords || !outPlan) return false;
  outPlan->inclusivePrefix.clear();
  if (kernel.spawnCountSourceOffsetBytes < 0) {
    outPlan->candidateCount = std::min(dispatch.recordCount, kernel.particleCapacity);
    return true;
  }
  outPlan->inclusivePrefix.resize(static_cast<size_t>(dispatch.recordCount));
  uint32_t cumulative = 0;
  const uint32_t capacity = static_cast<uint32_t>(kernel.particleCapacity);
  for (int32_t sourceIndex = 0; sourceIndex < dispatch.recordCount; ++sourceIndex) {
    uint32_t bits = 0;
    const size_t byteOffset =
        static_cast<size_t>(dispatch.startEventIndex + sourceIndex) *
            kernel.sourceStrideBytes +
        static_cast<size_t>(kernel.spawnCountSourceOffsetBytes);
    std::memcpy(&bits, sourceRecords + byteOffset, sizeof(bits));
    float value;
    std::memcpy(&value, &bits, sizeof(value));
    uint32_t count = 0;
    if (value > 0.0f && !std::isnan(value)) {
      const uint32_t remaining = capacity - cumulative;
      if (!std::isfinite(value) || value >= static_cast<float>(remaining))
        count = remaining;
      else
        count = std::min<uint32_t>(
            static_cast<uint32_t>(std::floor(value)), remaining);
    }
    cumulative += count;
    outPlan->inclusivePrefix[static_cast<size_t>(sourceIndex)] = cumulative;
  }
  outPlan->candidateCount = static_cast<int32_t>(cumulative);
  return true;
}

int32_t VFXFindSpawnSourceIndex(
    const uint32_t* inclusivePrefix, int32_t prefixCount,
    int32_t spawnThreadIndex) {
  if (!inclusivePrefix || prefixCount <= 0) return spawnThreadIndex;
  return static_cast<int32_t>(std::upper_bound(
      inclusivePrefix, inclusivePrefix + prefixCount,
      static_cast<uint32_t>(spawnThreadIndex)) - inclusivePrefix);
}

uint32_t VFXComposeWord(
    int32_t valueType, int32_t composition, uint32_t current,
    uint32_t value, float blend) {
  if (composition == 0) return value;
  if (valueType >= 4) {
    const float a = VFXWordToFloat(current);
    const float b = VFXWordToFloat(value);
    if (composition == 1) return VFXFloatToWord(a + b);
    if (composition == 2) return VFXFloatToWord(a * b);
    return VFXFloatToWord(a + (b - a) * blend);
  }
  if (valueType == 2) {
    if (composition == 1) return current + value;
    return current * value;
  }
  const int32_t a = static_cast<int32_t>(current);
  const int32_t b = static_cast<int32_t>(value);
  return static_cast<uint32_t>(composition == 1 ? a + b : a * b);
}

AnityResult ExecuteInitializeKernelCPU(
    const AnityGraphicsVFXInitializeDispatchDesc& dispatch,
    const AnityGraphicsVFXInitializeKernelDesc& kernel,
    const AnityGraphicsVFXInitializeAttributeDesc* attributes,
    const AnityGraphicsVFXInitializeOperationDesc* operations,
    const uint32_t* spawnCountPrefix,
    int32_t spawnCountPrefixCount,
    int32_t spawnCandidateCount,
    const uint8_t* sourceRecords,
    AnityGraphicsVFXParticleSystemStorage* storage,
    int32_t* outSpawnedCount) {
  if (!storage || !outSpawnedCount) return ANITY_ERR_INVALID_ARG;
  *outSpawnedCount = 0;
  const bool usesDeadList = (kernel.flags & 1u) != 0;
  int32_t aliveAttributeOffset = -1;
  for (int32_t i = 0; i < kernel.attributeCount; ++i)
    if (attributes[i].semantic == 1) aliveAttributeOffset = attributes[i].offsetBytes;
  const int32_t available = usesDeadList
      ? static_cast<int32_t>(storage->deadList.size())
      : std::max(0, kernel.particleCapacity -
          static_cast<int32_t>(storage->nextSequentialIndex));
  const int32_t threadCount = std::min(spawnCandidateCount, available);
  std::vector<uint32_t> local(static_cast<size_t>(kernel.attributeStrideBytes / 4));
  for (int32_t spawnThreadIndex = 0; spawnThreadIndex < threadCount; ++spawnThreadIndex) {
    const int32_t sourceIndex = VFXFindSpawnSourceIndex(
        spawnCountPrefix, spawnCountPrefixCount, spawnThreadIndex);
    if (sourceIndex < 0 || sourceIndex >= dispatch.recordCount)
      return ANITY_ERR_INVALID_ARG;
    std::fill(local.begin(), local.end(), 0u);
    for (int32_t attributeIndex = 0; attributeIndex < kernel.attributeCount; ++attributeIndex) {
      const auto& attribute = attributes[attributeIndex];
      std::memcpy(local.data() + attribute.offsetBytes / 4,
                  attribute.defaultWords,
                  static_cast<size_t>(attribute.componentCount) * sizeof(uint32_t));
    }
    const uint32_t logicalParticleIndex = storage->nextSequentialIndex +
        static_cast<uint32_t>(spawnThreadIndex);
    uint32_t randomState = VFXHash(logicalParticleIndex ^ kernel.systemSeed);
    for (int32_t operationIndex = 0; operationIndex < kernel.operationCount; ++operationIndex) {
      const auto& operation = operations[operationIndex];
      uint32_t* target = local.data() + operation.targetOffsetBytes / 4;
      const uint8_t* sourceBase = sourceRecords +
          static_cast<size_t>(dispatch.startEventIndex + sourceIndex) *
              kernel.sourceStrideBytes;
      float uniformRandom = 0.0f;
      if (operation.randomMode == 2) uniformRandom = VFXRandom(&randomState);
      for (int32_t component = 0; component < operation.componentCount; ++component) {
        uint32_t value = 0;
        if (operation.valueSource == 0) {
          value = operation.valueA[component];
          if (operation.randomMode != 0) {
            const float t = operation.randomMode == 2
                ? uniformRandom : VFXRandom(&randomState);
            const float a = VFXWordToFloat(operation.valueA[component]);
            const float b = VFXWordToFloat(operation.valueB[component]);
            value = VFXFloatToWord(a + (b - a) * t);
          }
        } else if (operation.valueSource == 1) {
          std::memcpy(&value,
                      sourceBase + operation.sourceOffsetBytes + component * 4,
                      sizeof(value));
        } else if (operation.valueSource == 2) {
          value = logicalParticleIndex;
        } else if (operation.valueSource == 3) {
          value = VFXHash(logicalParticleIndex ^ kernel.systemSeed);
        } else {
          value = static_cast<uint32_t>(spawnThreadIndex);
        }
        target[component] = VFXComposeWord(
            operation.valueType, operation.composition, target[component], value,
            VFXWordToFloat(operation.blendFactorBits));
      }
    }
    if (aliveAttributeOffset >= 0 && local[aliveAttributeOffset / 4] == 0u)
      continue;
    uint32_t outputParticleIndex;
    if (usesDeadList) {
      outputParticleIndex = storage->deadList.back();
      storage->deadList.pop_back();
    } else {
      outputParticleIndex = storage->nextSequentialIndex +
          static_cast<uint32_t>(spawnThreadIndex);
    }
    std::memcpy(storage->attributes.data() +
                    static_cast<size_t>(outputParticleIndex) * kernel.attributeStrideBytes,
                local.data(), static_cast<size_t>(kernel.attributeStrideBytes));
    ++*outSpawnedCount;
  }
  storage->nextSequentialIndex += static_cast<uint32_t>(threadCount);
  storage->info.aliveCount += *outSpawnedCount;
  storage->info.deadCount = usesDeadList
      ? static_cast<int32_t>(storage->deadList.size())
      : 0;
  return ANITY_OK;
}

AnityGraphicsVFXEventRegistry* EnsureRegistry(AnityGraphicsDevice* device) {
  if (!device) return nullptr;
  if (!device->vfxEvents) {
    static std::mutex creationMutex;
    std::lock_guard<std::mutex> creationLock(creationMutex);
    if (!device->vfxEvents)
      device->vfxEvents = new (std::nothrow) AnityGraphicsVFXEventRegistry();
  }
  return device->vfxEvents;
}

bool BuildDispatchPlan(
    const std::deque<std::shared_ptr<AnityGraphicsVFXEventStorage>>& queue,
    uint64_t throughSequence, AnityGraphicsVFXEventDispatchPlanInfo* outInfo,
    std::vector<AnityGraphicsVFXEventDispatchBatch>* outBatches,
    std::vector<uint8_t>* outRecords) {
  if (queue.empty() || throughSequence == 0 || !outInfo) return false;
  *outInfo = {};
  outInfo->effectId = queue.front()->info.desc.effectId;
  outInfo->firstSequence = queue.front()->info.desc.sequence;
  int64_t totalRecords = 0;
  int64_t totalBytes = 0;
  int32_t sharedStride = 0;
  bool foundThroughSequence = false;
  for (const auto& entry : queue) {
    if (entry->info.desc.sequence > throughSequence) break;
    if (entry->info.desc.recordCount > 0) {
      if (sharedStride == 0) sharedStride = entry->info.desc.strideBytes;
      if (sharedStride != entry->info.desc.strideBytes) return false;
    }
    AnityGraphicsVFXEventDispatchBatch batch{};
    batch.sequence = entry->info.desc.sequence;
    batch.eventNameId = entry->info.desc.eventNameId;
    batch.startEventIndex = static_cast<int32_t>(totalRecords);
    batch.recordCount = entry->info.desc.recordCount;
    batch.strideBytes = entry->info.desc.strideBytes;
    if (outBatches) outBatches->push_back(batch);
    if (outRecords && !entry->records.empty())
      outRecords->insert(outRecords->end(), entry->records.begin(), entry->records.end());
    totalRecords += entry->info.desc.recordCount;
    totalBytes += entry->info.byteCount;
    if (totalRecords > std::numeric_limits<int32_t>::max() ||
        totalBytes > std::numeric_limits<int32_t>::max())
      return false;
    outInfo->lastSequence = entry->info.desc.sequence;
    outInfo->uploadGeneration = entry->info.uploadGeneration;
    outInfo->batchCount++;
    if (entry->info.desc.sequence == throughSequence) {
      foundThroughSequence = true;
      break;
    }
  }
  if (!foundThroughSequence) return false;
  outInfo->recordCount = static_cast<int32_t>(totalRecords);
  outInfo->strideBytes = sharedStride;
  outInfo->byteCount = static_cast<int32_t>(totalBytes);
  return totalBytes == totalRecords * sharedStride;
}

AnityResult CancelPendingInitializeForEffectLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t effectId);

}  // namespace

void AnityGraphics_DestroyVFXEventRegistry(AnityGraphicsDevice* device) {
  if (!device) return;
  if (device->vfxEvents) {
    std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
    while (!device->vfxEvents->pendingUpdateByEffect.empty()) {
      const uint64_t effectId =
          device->vfxEvents->pendingUpdateByEffect.begin()->first;
      CancelVFXPendingUpdatesForEffectLocked(
          device, device->vfxEvents, effectId);
    }
    while (!device->vfxEvents->pendingInitializeByEffect.empty()) {
      const uint64_t effectId =
          device->vfxEvents->pendingInitializeByEffect.begin()->first;
      CancelPendingInitializeForEffectLocked(
          device, device->vfxEvents, effectId);
    }
  }
  delete device->vfxEvents;
  device->vfxEvents = nullptr;
}

extern "C" {

AnityResult ANITY_CALL AnityGraphics_UploadVFXEventRecords(
    AnityGraphicsDevice* device, const AnityGraphicsVFXEventUploadDesc* desc,
    const uint8_t* records, int32_t byteCount) {
  if (!device || !desc || !IsValidUpload(*desc, records, byteCount))
    return ANITY_ERR_INVALID_ARG;
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  try {
    auto entry = std::make_shared<AnityGraphicsVFXEventStorage>();
    if (byteCount > 0) entry->records.assign(records, records + byteCount);
    entry->info.desc = *desc;
    entry->info.byteCount = byteCount;
    entry->info.backendKind = 0;
    std::lock_guard<std::mutex> lock(registry->mutex);
    auto found = registry->entries.find(desc->effectId);
    if (found != registry->entries.end() &&
        found->second->info.desc.sequence >= desc->sequence)
      return ANITY_ERR_INVALID_ARG;
    auto& queue = registry->inputEntries[desc->effectId];
    if (queue.size() >= kMaxInputBatchesPerEffect) return ANITY_ERR_OUT_OF_MEMORY;
    if (desc->recordCount > 0) {
      auto nonEmpty = std::find_if(queue.begin(), queue.end(), [](const auto& queued) {
        return queued->info.desc.recordCount > 0;
      });
      if (nonEmpty != queue.end() &&
          (*nonEmpty)->info.desc.strideBytes != desc->strideBytes)
        return ANITY_ERR_INVALID_ARG;
    }
    entry->info.uploadGeneration = ++registry->generation;
    queue.push_back(entry);
    registry->entries[desc->effectId] = std::move(entry);
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

AnityResult ANITY_CALL AnityGraphics_GetVFXEventUploadInfo(
    const AnityGraphicsDevice* device, uint64_t effectId,
    AnityGraphicsVFXEventUploadInfo* outInfo) {
  if (!device || effectId == 0 || !outInfo) return ANITY_ERR_INVALID_ARG;
  *outInfo = {};
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->entries.find(effectId);
  if (found == device->vfxEvents->entries.end()) return ANITY_ERR_INVALID_ARG;
  *outInfo = found->second->info;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_ReadbackVFXEventRecords(
    const AnityGraphicsDevice* device, uint64_t effectId,
    uint8_t* records, int32_t recordCapacity, int32_t* outWritten) {
  if (!device || effectId == 0 || recordCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  *outWritten = 0;
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->entries.find(effectId);
  if (found == device->vfxEvents->entries.end()) return ANITY_ERR_INVALID_ARG;
  int32_t required = found->second->info.byteCount;
  if (recordCapacity < required || (required > 0 && !records))
    return ANITY_ERR_INVALID_ARG;
  if (required > 0) std::memcpy(records, found->second->records.data(), required);
  *outWritten = required;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_GetVFXEventDispatchPlanInfo(
    const AnityGraphicsDevice* device, uint64_t effectId,
    AnityGraphicsVFXEventDispatchPlanInfo* outInfo) {
  if (!device || effectId == 0 || !outInfo) return ANITY_ERR_INVALID_ARG;
  *outInfo = {};
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->inputEntries.find(effectId);
  if (found == device->vfxEvents->inputEntries.end() || found->second.empty())
    return ANITY_ERR_INVALID_ARG;
  return BuildDispatchPlan(found->second, found->second.back()->info.desc.sequence,
                           outInfo, nullptr, nullptr)
             ? ANITY_OK
             : ANITY_ERR_INVALID_ARG;
}

AnityResult ANITY_CALL AnityGraphics_CopyVFXEventDispatchBatches(
    const AnityGraphicsDevice* device, uint64_t effectId,
    uint64_t throughSequence, AnityGraphicsVFXEventDispatchBatch* batches,
    int32_t batchCapacity, int32_t* outWritten) {
  if (!device || effectId == 0 || throughSequence == 0 || batchCapacity < 0 ||
      !outWritten)
    return ANITY_ERR_INVALID_ARG;
  *outWritten = 0;
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->inputEntries.find(effectId);
  if (found == device->vfxEvents->inputEntries.end()) return ANITY_ERR_INVALID_ARG;
  AnityGraphicsVFXEventDispatchPlanInfo info{};
  std::vector<AnityGraphicsVFXEventDispatchBatch> snapshot;
  if (!BuildDispatchPlan(found->second, throughSequence, &info, &snapshot, nullptr) ||
      batchCapacity < info.batchCount || (info.batchCount > 0 && !batches))
    return ANITY_ERR_INVALID_ARG;
  if (!snapshot.empty())
    std::memcpy(batches, snapshot.data(),
                snapshot.size() * sizeof(AnityGraphicsVFXEventDispatchBatch));
  *outWritten = info.batchCount;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_CopyVFXEventDispatchRecords(
    const AnityGraphicsDevice* device, uint64_t effectId,
    uint64_t throughSequence, uint8_t* records, int32_t recordCapacity,
    int32_t* outWritten) {
  if (!device || effectId == 0 || throughSequence == 0 || recordCapacity < 0 ||
      !outWritten)
    return ANITY_ERR_INVALID_ARG;
  *outWritten = 0;
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->inputEntries.find(effectId);
  if (found == device->vfxEvents->inputEntries.end()) return ANITY_ERR_INVALID_ARG;
  AnityGraphicsVFXEventDispatchPlanInfo info{};
  std::vector<uint8_t> snapshot;
  if (!BuildDispatchPlan(found->second, throughSequence, &info, nullptr, &snapshot) ||
      recordCapacity < info.byteCount || (info.byteCount > 0 && !records))
    return ANITY_ERR_INVALID_ARG;
  if (!snapshot.empty()) std::memcpy(records, snapshot.data(), snapshot.size());
  *outWritten = info.byteCount;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_ConsumeVFXEventDispatchPlan(
    AnityGraphicsDevice* device, uint64_t effectId, uint64_t throughSequence) {
  if (!device || effectId == 0 || throughSequence == 0 || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->inputEntries.find(effectId);
  if (found == device->vfxEvents->inputEntries.end() || found->second.empty())
    return ANITY_ERR_INVALID_ARG;
  auto through = std::find_if(found->second.begin(), found->second.end(),
      [throughSequence](const auto& entry) {
        return entry->info.desc.sequence == throughSequence;
      });
  if (through == found->second.end()) return ANITY_ERR_INVALID_ARG;
  try {
    if (AnityGraphicsVFXFrameStorage* frame =
            GetPreparedFrameStorage(device->vfxEvents, effectId)) {
      frame->consumedInputRollback.insert(
          frame->consumedInputRollback.end(), found->second.begin(),
          std::next(through));
    }
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  found->second.erase(found->second.begin(), std::next(through));
  if (found->second.empty()) device->vfxEvents->inputEntries.erase(found);
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_SubmitVFXInitializeDispatch(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* desc,
    const uint8_t* sourceRecords, int32_t sourceByteCount) {
  return AnityGraphics_SubmitVFXInitializeDispatches(
      device, desc, 1, sourceRecords, sourceByteCount);
}

AnityResult ANITY_CALL AnityGraphics_SubmitVFXInitializeDispatches(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* descs, int32_t descCount,
    const uint8_t* sourceRecords, int32_t sourceByteCount) {
  if (!device || !descs || descCount <= 0 ||
      descCount > kMaxInitializeDispatchesPerTransaction ||
      !sourceRecords || sourceByteCount <= 0)
    return ANITY_ERR_INVALID_ARG;
  for (int32_t index = 0; index < descCount; ++index) {
    int32_t outputByteCount = 0;
    if (!IsValidInitializeDispatch(
            descs[index], sourceRecords, sourceByteCount, &outputByteCount))
      return ANITY_ERR_INVALID_ARG;
  }
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  try {
    std::lock_guard<std::mutex> lock(registry->mutex);
    auto staged = registry->initializeDispatches;
    uint64_t stagedGeneration = registry->generation;
    for (int32_t index = 0; index < descCount; ++index) {
      const AnityGraphicsVFXInitializeDispatchDesc& desc = descs[index];
      const int32_t outputByteCount = static_cast<int32_t>(
          static_cast<int64_t>(desc.recordCount) * desc.strideBytes);
      const AnityGraphicsVFXInitializeKey key{
          desc.effectId, desc.initializeContextId};
      auto found = staged.find(key);
      if (found != staged.end()) {
        if (found->second->info.desc.sequence > desc.sequence)
          return ANITY_ERR_INVALID_ARG;
        if (found->second->info.desc.sequence == desc.sequence) {
          if (!MatchesInitializeDispatch(
                  *found->second, desc, sourceRecords, sourceByteCount,
                  outputByteCount))
            return ANITY_ERR_INVALID_ARG;
          continue;
        }
      }

      auto replacement = std::make_shared<AnityGraphicsVFXInitializeStorage>();
      replacement->records.resize(static_cast<size_t>(outputByteCount));
      int32_t backendKind = 0;
      if (device->type == ANITY_GFX_METAL) {
        AnityResult result = AnityGraphics_Metal_DispatchVFXInitializeCopy(
            device, &desc, sourceRecords, sourceByteCount,
            replacement->records.data(), outputByteCount);
        if (result != ANITY_OK) return result;
        backendKind = 2;
      } else {
        const size_t sourceOffset =
            static_cast<size_t>(desc.startEventIndex) * desc.strideBytes;
        std::memcpy(replacement->records.data(), sourceRecords + sourceOffset,
                    static_cast<size_t>(outputByteCount));
      }
      replacement->info.desc = desc;
      replacement->info.sourceByteCount = sourceByteCount;
      replacement->info.outputByteCount = outputByteCount;
      replacement->info.backendKind = backendKind;
      replacement->info.dispatchGeneration = ++stagedGeneration;
      staged[key] = std::move(replacement);
    }
    registry->initializeDispatches.swap(staged);
    registry->generation = stagedGeneration;
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

static AnityResult SubmitVFXInitializeKernelsSynchronousLegacy(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* dispatches,
    const AnityGraphicsVFXInitializeKernelDesc* kernels, int32_t dispatchCount,
    const AnityGraphicsVFXInitializeAttributeDesc* attributes, int32_t attributeCount,
    const AnityGraphicsVFXInitializeOperationDesc* operations, int32_t operationCount,
    const uint8_t* sourceRecords, int32_t sourceByteCount) {
  if (!device || !dispatches || !kernels || dispatchCount <= 0 ||
      dispatchCount > kMaxInitializeDispatchesPerTransaction ||
      attributeCount < 0 || (attributeCount > 0 && !attributes) || operationCount < 0 ||
      (operationCount > 0 && !operations) || !sourceRecords || sourceByteCount <= 0)
    return ANITY_ERR_INVALID_ARG;
  for (int32_t index = 0; index < dispatchCount; ++index) {
    int32_t outputByteCount = 0;
    if (!IsValidInitializeDispatch(
            dispatches[index], sourceRecords, sourceByteCount, &outputByteCount) ||
        !IsValidInitializeKernel(
            dispatches[index], kernels[index], attributes, attributeCount,
            operations, operationCount))
      return ANITY_ERR_INVALID_ARG;
  }
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  try {
    std::lock_guard<std::mutex> lock(registry->mutex);
    std::vector<uint64_t> finalizedEffects;
    finalizedEffects.reserve(static_cast<size_t>(dispatchCount));
    for (int32_t index = 0; index < dispatchCount; ++index) {
      if (kernels[index].version == 0) continue;
      const uint64_t effectId = dispatches[index].effectId;
      if (std::find(finalizedEffects.begin(), finalizedEffects.end(), effectId) !=
          finalizedEffects.end())
        continue;
      auto pendingByEffect = registry->pendingUpdateByEffect.find(effectId);
      if (pendingByEffect != registry->pendingUpdateByEffect.end()) {
        bool hasUncommitted = false;
        for (uint64_t ticketId : pendingByEffect->second) {
          auto pending = registry->pendingUpdates.find(ticketId);
          if (pending == registry->pendingUpdates.end())
            return ANITY_ERR_INTERNAL;
          hasUncommitted = hasUncommitted || !pending->second->frameCommitted;
        }
        if (!hasUncommitted) {
          AnityResult finalized = FinalizeCommittedVFXPendingUpdateLocked(
              device, registry, effectId, true, nullptr);
          if (finalized != ANITY_OK) return finalized;
        }
      }
      finalizedEffects.push_back(effectId);
    }
    for (int32_t index = 0; index < dispatchCount; ++index) {
      if (kernels[index].version == 0) continue;
      const AnityGraphicsVFXParticleSystemKey key{
          dispatches[index].effectId, dispatches[index].particleSystemId};
      auto current = registry->particleSystems.find(key);
      if (current != registry->particleSystems.end()) {
        const bool canUseResidentInitialize =
            device->type == ANITY_GFX_METAL &&
            current->second->attributesResidentOnly;
        if (canUseResidentInitialize) continue;
        AnityResult materialized = MaterializeVFXParticleAttributesLocked(
            device, current->second.get());
        if (materialized != ANITY_OK) return materialized;
      }
    }
    auto stagedDispatches = registry->initializeDispatches;
    auto stagedSystems = registry->particleSystems;
    uint64_t stagedGeneration = registry->generation;
    for (int32_t index = 0; index < dispatchCount; ++index) {
      const auto& dispatch = dispatches[index];
      const auto& kernel = kernels[index];
      const auto* kernelAttributes = kernel.attributeCount == 0
          ? nullptr : attributes + kernel.attributeStart;
      const auto* kernelOperations = kernel.operationCount == 0
          ? nullptr : operations + kernel.operationStart;
      const uint64_t fingerprint = VFXKernelFingerprint(
          kernel, kernelAttributes, kernelOperations);
      const int32_t outputByteCount = dispatch.recordCount * dispatch.strideBytes;
      const AnityGraphicsVFXInitializeKey dispatchKey{
          dispatch.effectId, dispatch.initializeContextId};
      auto existingDispatch = stagedDispatches.find(dispatchKey);
      if (existingDispatch != stagedDispatches.end()) {
        if (existingDispatch->second->info.desc.sequence > dispatch.sequence)
          return ANITY_ERR_INVALID_ARG;
        if (existingDispatch->second->info.desc.sequence == dispatch.sequence) {
          if (existingDispatch->second->kernelFingerprint != fingerprint ||
              !MatchesInitializeDispatch(
                  *existingDispatch->second, dispatch, sourceRecords,
                  sourceByteCount, outputByteCount))
            return ANITY_ERR_INVALID_ARG;
          continue;
        }
      }

      if (kernel.version == 0) {
        auto dispatchStorage = std::make_shared<AnityGraphicsVFXInitializeStorage>();
        const size_t sourceOffset =
            static_cast<size_t>(dispatch.startEventIndex) * dispatch.strideBytes;
        dispatchStorage->records.assign(
            sourceRecords + sourceOffset,
            sourceRecords + sourceOffset + outputByteCount);
        dispatchStorage->info.desc = dispatch;
        dispatchStorage->info.sourceByteCount = sourceByteCount;
        dispatchStorage->info.outputByteCount = outputByteCount;
        dispatchStorage->info.backendKind = 0;
        dispatchStorage->info.dispatchGeneration = ++stagedGeneration;
        dispatchStorage->kernelFingerprint = fingerprint;
        stagedDispatches[dispatchKey] = std::move(dispatchStorage);
        continue;
      }

      const AnityGraphicsVFXParticleSystemKey systemKey{
          dispatch.effectId, dispatch.particleSystemId};
      std::shared_ptr<AnityGraphicsVFXParticleSystemStorage> system;
      auto existingSystem = stagedSystems.find(systemKey);
      if (existingSystem == stagedSystems.end()) {
        system = std::make_shared<AnityGraphicsVFXParticleSystemStorage>();
        system->info.effectId = dispatch.effectId;
        system->info.particleSystemId = dispatch.particleSystemId;
        system->info.capacity = kernel.particleCapacity;
        system->info.attributeStrideBytes = kernel.attributeStrideBytes;
        system->attributes.resize(
            static_cast<size_t>(kernel.particleCapacity) * kernel.attributeStrideBytes);
        system->usesDeadList = (kernel.flags & 1u) != 0;
        if ((kernel.flags & 1u) != 0) {
          system->deadList.resize(static_cast<size_t>(kernel.particleCapacity));
          for (int32_t particleIndex = 0;
               particleIndex < kernel.particleCapacity; ++particleIndex)
            system->deadList[static_cast<size_t>(particleIndex)] =
                static_cast<uint32_t>(particleIndex);
          system->info.deadCount = kernel.particleCapacity;
        }
      } else {
        if (existingSystem->second->info.capacity != kernel.particleCapacity ||
            existingSystem->second->info.attributeStrideBytes !=
                kernel.attributeStrideBytes ||
            existingSystem->second->usesDeadList != ((kernel.flags & 1u) != 0))
          return ANITY_ERR_INVALID_ARG;
        system = std::make_shared<AnityGraphicsVFXParticleSystemStorage>(
            *existingSystem->second);
      }

      VFXSpawnPlan spawnPlan;
      if (!BuildVFXSpawnPlan(dispatch, kernel, sourceRecords, &spawnPlan))
        return ANITY_ERR_INVALID_ARG;

      int32_t spawnedCount = 0;
      AnityResult executionResult;
      if (device->type == ANITY_GFX_METAL) {
        if (stagedGeneration == std::numeric_limits<uint64_t>::max())
          return ANITY_ERR_INTERNAL;
        const uint64_t sourceGeneration = system->info.generation;
        const uint64_t targetGeneration = stagedGeneration + 1u;
        const int32_t particleRecordsAuthoritative =
            system->attributesResidentOnly ? 0 : 1;
        const int32_t retainSourceGeneration =
            GetPreparedFrameStorage(registry, dispatch.effectId) ? 1 : 0;
        int32_t aliveCount = system->info.aliveCount;
        int32_t deadCount = static_cast<int32_t>(system->deadList.size());
        uint32_t nextSequentialIndex = system->nextSequentialIndex;
        executionResult = AnityGraphics_Metal_DispatchVFXInitializeKernel(
            device, &dispatch, &kernel, kernelAttributes, kernelOperations,
            spawnPlan.inclusivePrefix.empty()
                ? nullptr : spawnPlan.inclusivePrefix.data(),
            static_cast<int32_t>(spawnPlan.inclusivePrefix.size()),
            spawnPlan.candidateCount,
            sourceRecords, sourceByteCount,
            system->attributes.data(), static_cast<int32_t>(system->attributes.size()),
            system->deadList.empty() ? nullptr : system->deadList.data(),
            static_cast<int32_t>(system->deadList.size()), &aliveCount,
            &deadCount, &nextSequentialIndex, &spawnedCount,
            sourceGeneration, targetGeneration,
            particleRecordsAuthoritative, retainSourceGeneration);
        if (executionResult == ANITY_OK) {
          system->nextSequentialIndex = nextSequentialIndex;
          if ((kernel.flags & 1u) != 0)
            system->deadList.resize(static_cast<size_t>(deadCount));
          system->info.aliveCount = aliveCount;
          system->info.deadCount = (kernel.flags & 1u) != 0
              ? deadCount : 0;
          stagedGeneration = targetGeneration;
        }
      } else {
        executionResult = ExecuteInitializeKernelCPU(
            dispatch, kernel, kernelAttributes, kernelOperations,
            spawnPlan.inclusivePrefix.empty()
                ? nullptr : spawnPlan.inclusivePrefix.data(),
            static_cast<int32_t>(spawnPlan.inclusivePrefix.size()),
            spawnPlan.candidateCount,
            sourceRecords, system.get(), &spawnedCount);
      }
      if (executionResult != ANITY_OK) return executionResult;
      system->info.backendKind = device->type == ANITY_GFX_METAL ? 2 : 0;
      if (device->type != ANITY_GFX_METAL) ++stagedGeneration;
      system->info.generation = stagedGeneration;
      stagedSystems[systemKey] = system;

      auto dispatchStorage = std::make_shared<AnityGraphicsVFXInitializeStorage>();
      const size_t sourceOffset =
          static_cast<size_t>(dispatch.startEventIndex) * dispatch.strideBytes;
      dispatchStorage->records.assign(
          sourceRecords + sourceOffset,
          sourceRecords + sourceOffset + outputByteCount);
      dispatchStorage->info.desc = dispatch;
      dispatchStorage->info.sourceByteCount = sourceByteCount;
      dispatchStorage->info.outputByteCount = outputByteCount;
      dispatchStorage->info.backendKind = system->info.backendKind;
      dispatchStorage->info.dispatchGeneration = ++stagedGeneration;
      dispatchStorage->kernelFingerprint = fingerprint;
      stagedDispatches[dispatchKey] = std::move(dispatchStorage);
    }
    registry->initializeDispatches.swap(stagedDispatches);
    registry->particleSystems.swap(stagedSystems);
    registry->generation = stagedGeneration;
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

namespace {

void ApplyPendingInitializeResult(
    AnityGraphicsVFXPendingInitialize* pending, int32_t index) {
  auto& system = *pending->systems[static_cast<size_t>(index)];
  system.nextSequentialIndex =
      pending->nextSequentialIndices[static_cast<size_t>(index)];
  if (pending->usesDeadLists[static_cast<size_t>(index)] != 0)
    system.deadList.resize(static_cast<size_t>(
        pending->deadCounts[static_cast<size_t>(index)]));
  system.info.aliveCount = pending->aliveCounts[static_cast<size_t>(index)];
  system.info.deadCount =
      pending->usesDeadLists[static_cast<size_t>(index)] != 0
      ? pending->deadCounts[static_cast<size_t>(index)] : 0;
}

void ReleasePendingInitializeOwnershipLocked(
    AnityGraphicsVFXEventRegistry* registry,
    const AnityGraphicsVFXPendingInitialize& pending) {
  for (uint64_t effectId : pending.effectIds) {
    auto owner = registry->pendingInitializeByEffect.find(effectId);
    if (owner != registry->pendingInitializeByEffect.end() &&
        owner->second == pending.info.ticketId)
      registry->pendingInitializeByEffect.erase(owner);
  }
}

AnityResult RollbackPendingInitializeLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXPendingInitialize* pending) {
  AnityResult result = ANITY_OK;
  for (size_t reverse = pending->backendHandles.size(); reverse > 0; --reverse) {
    const size_t index = reverse - 1u;
    if (pending->backendHandles[index]) {
      AnityResult cancelled = AnityGraphics_Metal_CancelVFXInitializeKernel(
          pending->backendHandles[index]);
      pending->backendHandles[index] = nullptr;
      if (cancelled != ANITY_OK) result = cancelled;
    }
    if (pending->backendCompleted[index] != 0 &&
        pending->sourceGenerations[index] != 0) {
      const auto& key = pending->keys[index];
      AnityResult restored = AnityGraphics_Metal_RestoreVFXResidentGeneration(
          device, key.effectId, key.particleSystemId,
          pending->sourceGenerations[index]);
      if (restored != ANITY_OK) result = restored;
      pending->backendCompleted[index] = 0;
    }
  }
  return result;
}

AnityResult CancelPendingInitializeForEffectLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t effectId) {
  auto owner = registry->pendingInitializeByEffect.find(effectId);
  if (owner == registry->pendingInitializeByEffect.end()) return ANITY_OK;
  auto found = registry->pendingInitializes.find(owner->second);
  if (found == registry->pendingInitializes.end()) {
    registry->pendingInitializeByEffect.erase(owner);
    return ANITY_ERR_INTERNAL;
  }
  auto pending = found->second;
  AnityResult result = RollbackPendingInitializeLocked(device, pending.get());
  ReleasePendingInitializeOwnershipLocked(registry, *pending);
  registry->pendingInitializes.erase(found);
  return result;
}

}  // namespace

AnityResult ANITY_CALL AnityGraphics_BeginVFXInitializeKernels(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* dispatches,
    const AnityGraphicsVFXInitializeKernelDesc* kernels, int32_t dispatchCount,
    const AnityGraphicsVFXInitializeAttributeDesc* attributes,
    int32_t attributeCount,
    const AnityGraphicsVFXInitializeOperationDesc* operations,
    int32_t operationCount,
    const uint8_t* sourceRecords, int32_t sourceByteCount,
    uint64_t* outTicketId) {
  if (!device || !dispatches || !kernels || dispatchCount <= 0 ||
      dispatchCount > kMaxInitializeDispatchesPerTransaction ||
      attributeCount < 0 || (attributeCount > 0 && !attributes) ||
      operationCount < 0 || (operationCount > 0 && !operations) ||
      !sourceRecords || sourceByteCount <= 0 || !outTicketId)
    return ANITY_ERR_INVALID_ARG;
  *outTicketId = 0;
  for (int32_t index = 0; index < dispatchCount; ++index) {
    int32_t outputByteCount = 0;
    if (!IsValidInitializeDispatch(
            dispatches[index], sourceRecords, sourceByteCount,
            &outputByteCount) ||
        !IsValidInitializeKernel(
            dispatches[index], kernels[index], attributes, attributeCount,
            operations, operationCount))
      return ANITY_ERR_INVALID_ARG;
  }
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  std::shared_ptr<AnityGraphicsVFXPendingInitialize> pending;
  try {
    pending = std::make_shared<AnityGraphicsVFXPendingInitialize>();
    const size_t count = static_cast<size_t>(dispatchCount);
    pending->keys.resize(count);
    pending->systems.resize(count);
    pending->backendHandles.assign(count, nullptr);
    pending->aliveCounts.resize(count);
    pending->deadCounts.resize(count);
    pending->nextSequentialIndices.resize(count);
    pending->spawnedCounts.resize(count);
    pending->sourceGenerations.resize(count);
    pending->targetGenerations.resize(count);
    pending->usesDeadLists.resize(count);
    pending->backendCompleted.assign(count, 0);
    pending->retainSnapshots.resize(count);

    std::lock_guard<std::mutex> lock(registry->mutex);
    for (int32_t index = 0; index < dispatchCount; ++index) {
      const uint64_t effectId = dispatches[index].effectId;
      if (std::find(pending->effectIds.begin(), pending->effectIds.end(),
                    effectId) == pending->effectIds.end())
        pending->effectIds.push_back(effectId);
    }
    for (uint64_t effectId : pending->effectIds)
      if (registry->pendingInitializeByEffect.find(effectId) !=
          registry->pendingInitializeByEffect.end())
        return ANITY_ERR_INVALID_ARG;

    for (uint64_t effectId : pending->effectIds) {
      auto pendingUpdates = registry->pendingUpdateByEffect.find(effectId);
      if (pendingUpdates == registry->pendingUpdateByEffect.end()) continue;
      bool hasUncommitted = false;
      for (uint64_t updateTicket : pendingUpdates->second) {
        auto update = registry->pendingUpdates.find(updateTicket);
        if (update == registry->pendingUpdates.end())
          return ANITY_ERR_INTERNAL;
        hasUncommitted = hasUncommitted || !update->second->frameCommitted;
      }
      if (!hasUncommitted) {
        AnityResult finalized = FinalizeCommittedVFXPendingUpdateLocked(
            device, registry, effectId, true, nullptr);
        if (finalized != ANITY_OK) return finalized;
      }
    }

    for (int32_t index = 0; index < dispatchCount; ++index) {
      if (kernels[index].version == 0) continue;
      const AnityGraphicsVFXParticleSystemKey key{
          dispatches[index].effectId, dispatches[index].particleSystemId};
      auto current = registry->particleSystems.find(key);
      if (current == registry->particleSystems.end()) continue;
      const bool resident = device->type == ANITY_GFX_METAL &&
          current->second->attributesResidentOnly;
      if (!resident) {
        AnityResult materialized = MaterializeVFXParticleAttributesLocked(
            device, current->second.get());
        if (materialized != ANITY_OK) return materialized;
      }
    }

    pending->stagedDispatches = registry->initializeDispatches;
    pending->stagedSystems = registry->particleSystems;
    uint64_t stagedGeneration = registry->generation;
    const uint64_t sourceRegistryGeneration = stagedGeneration;
    bool hasBackendHandle = false;

    for (int32_t index = 0; index < dispatchCount; ++index) {
      const size_t slot = static_cast<size_t>(index);
      const auto& dispatch = dispatches[index];
      const auto& kernel = kernels[index];
      const auto* kernelAttributes = kernel.attributeCount == 0
          ? nullptr : attributes + kernel.attributeStart;
      const auto* kernelOperations = kernel.operationCount == 0
          ? nullptr : operations + kernel.operationStart;
      const uint64_t fingerprint = VFXKernelFingerprint(
          kernel, kernelAttributes, kernelOperations);
      const int32_t outputByteCount =
          dispatch.recordCount * dispatch.strideBytes;
      const AnityGraphicsVFXInitializeKey dispatchKey{
          dispatch.effectId, dispatch.initializeContextId};
      auto existingDispatch = pending->stagedDispatches.find(dispatchKey);
      if (existingDispatch != pending->stagedDispatches.end()) {
        if (existingDispatch->second->info.desc.sequence > dispatch.sequence) {
          RollbackPendingInitializeLocked(device, pending.get());
          return ANITY_ERR_INVALID_ARG;
        }
        if (existingDispatch->second->info.desc.sequence == dispatch.sequence) {
          if (existingDispatch->second->kernelFingerprint != fingerprint ||
              !MatchesInitializeDispatch(
                  *existingDispatch->second, dispatch, sourceRecords,
                  sourceByteCount, outputByteCount)) {
            RollbackPendingInitializeLocked(device, pending.get());
            return ANITY_ERR_INVALID_ARG;
          }
          continue;
        }
      }

      if (kernel.version == 0) {
        auto storage =
            std::make_shared<AnityGraphicsVFXInitializeStorage>();
        const size_t sourceOffset =
            static_cast<size_t>(dispatch.startEventIndex) * dispatch.strideBytes;
        storage->records.assign(sourceRecords + sourceOffset,
            sourceRecords + sourceOffset + outputByteCount);
        storage->info.desc = dispatch;
        storage->info.sourceByteCount = sourceByteCount;
        storage->info.outputByteCount = outputByteCount;
        storage->info.dispatchGeneration = ++stagedGeneration;
        storage->kernelFingerprint = fingerprint;
        pending->stagedDispatches[dispatchKey] = std::move(storage);
        continue;
      }

      const AnityGraphicsVFXParticleSystemKey systemKey{
          dispatch.effectId, dispatch.particleSystemId};
      for (int32_t previous = 0; previous < index; ++previous) {
        const size_t previousSlot = static_cast<size_t>(previous);
        if (!(pending->keys[previousSlot] == systemKey) ||
            !pending->backendHandles[previousSlot])
          continue;
        AnityResult completed =
            AnityGraphics_Metal_CompleteVFXInitializeKernel(
                pending->backendHandles[previousSlot]);
        pending->backendHandles[previousSlot] = nullptr;
        if (completed != ANITY_OK) {
          RollbackPendingInitializeLocked(device, pending.get());
          return completed;
        }
        pending->backendCompleted[previousSlot] = 1;
        ApplyPendingInitializeResult(pending.get(), previous);
      }

      std::shared_ptr<AnityGraphicsVFXParticleSystemStorage> system;
      auto existingSystem = pending->stagedSystems.find(systemKey);
      if (existingSystem == pending->stagedSystems.end()) {
        system = std::make_shared<AnityGraphicsVFXParticleSystemStorage>();
        system->info.effectId = dispatch.effectId;
        system->info.particleSystemId = dispatch.particleSystemId;
        system->info.capacity = kernel.particleCapacity;
        system->info.attributeStrideBytes = kernel.attributeStrideBytes;
        system->attributes.resize(static_cast<size_t>(kernel.particleCapacity) *
                                  kernel.attributeStrideBytes);
        system->usesDeadList = (kernel.flags & 1u) != 0u;
        if (system->usesDeadList) {
          system->deadList.resize(static_cast<size_t>(kernel.particleCapacity));
          for (int32_t particle = 0; particle < kernel.particleCapacity;
               ++particle)
            system->deadList[static_cast<size_t>(particle)] =
                static_cast<uint32_t>(particle);
          system->info.deadCount = kernel.particleCapacity;
        }
      } else {
        if (existingSystem->second->info.capacity != kernel.particleCapacity ||
            existingSystem->second->info.attributeStrideBytes !=
                kernel.attributeStrideBytes ||
            existingSystem->second->usesDeadList !=
                ((kernel.flags & 1u) != 0u)) {
          RollbackPendingInitializeLocked(device, pending.get());
          return ANITY_ERR_INVALID_ARG;
        }
        system = std::make_shared<AnityGraphicsVFXParticleSystemStorage>(
            *existingSystem->second);
      }
      pending->keys[slot] = systemKey;
      pending->systems[slot] = system;
      pending->aliveCounts[slot] = system->info.aliveCount;
      pending->deadCounts[slot] = static_cast<int32_t>(system->deadList.size());
      pending->nextSequentialIndices[slot] = system->nextSequentialIndex;
      pending->usesDeadLists[slot] = (kernel.flags & 1u) != 0u ? 1 : 0;
      pending->retainSnapshots[slot] =
          GetPreparedFrameStorage(registry, dispatch.effectId) ? 1 : 0;

      VFXSpawnPlan spawnPlan;
      if (!BuildVFXSpawnPlan(dispatch, kernel, sourceRecords, &spawnPlan)) {
        RollbackPendingInitializeLocked(device, pending.get());
        return ANITY_ERR_INVALID_ARG;
      }
      AnityResult execution = ANITY_OK;
      bool metalResidentSource = false;
      if (device->type == ANITY_GFX_METAL) {
        if (stagedGeneration == std::numeric_limits<uint64_t>::max()) {
          RollbackPendingInitializeLocked(device, pending.get());
          return ANITY_ERR_INTERNAL;
        }
        pending->sourceGenerations[slot] = system->info.generation;
        pending->targetGenerations[slot] = stagedGeneration + 1u;
        if (pending->sourceGenerations[slot] != 0) {
          AnityGraphicsVFXUpdateBackendStats residentStats{};
          metalResidentSource =
              AnityGraphics_Metal_GetVFXUpdateBackendStats(
                  device, systemKey.effectId, systemKey.particleSystemId,
                  &residentStats) == ANITY_OK &&
              residentStats.residentGeneration ==
                  pending->sourceGenerations[slot];
        }
        const int32_t authoritative = metalResidentSource ? 0 : 1;
        execution = AnityGraphics_Metal_BeginVFXInitializeKernel(
            device, &dispatch, &kernel, kernelAttributes, kernelOperations,
            spawnPlan.inclusivePrefix.empty()
                ? nullptr : spawnPlan.inclusivePrefix.data(),
            static_cast<int32_t>(spawnPlan.inclusivePrefix.size()),
            spawnPlan.candidateCount, sourceRecords, sourceByteCount,
            system->attributes.data(),
            static_cast<int32_t>(system->attributes.size()),
            system->deadList.empty() ? nullptr : system->deadList.data(),
            static_cast<int32_t>(system->deadList.size()),
            &pending->aliveCounts[slot], &pending->deadCounts[slot],
            &pending->nextSequentialIndices[slot],
            &pending->spawnedCounts[slot],
            pending->sourceGenerations[slot],
            pending->targetGenerations[slot], authoritative,
            1, &pending->backendHandles[slot]);
        if (execution == ANITY_OK && pending->backendHandles[slot])
          execution = AnityGraphics_Metal_PublishVFXInitializeKernel(
              pending->backendHandles[slot]);
        if (execution == ANITY_OK) {
          stagedGeneration = pending->targetGenerations[slot];
          hasBackendHandle = hasBackendHandle ||
              pending->backendHandles[slot] != nullptr;
        }
      } else {
        execution = ExecuteInitializeKernelCPU(
            dispatch, kernel, kernelAttributes, kernelOperations,
            spawnPlan.inclusivePrefix.empty()
                ? nullptr : spawnPlan.inclusivePrefix.data(),
            static_cast<int32_t>(spawnPlan.inclusivePrefix.size()),
            spawnPlan.candidateCount, sourceRecords, system.get(),
            &pending->spawnedCounts[slot]);
        if (execution == ANITY_OK) ++stagedGeneration;
      }
      if (execution != ANITY_OK) {
        RollbackPendingInitializeLocked(device, pending.get());
        return execution;
      }
      system->info.backendKind = device->type == ANITY_GFX_METAL ? 2 : 0;
      system->attributesResidentOnly = metalResidentSource;
      system->info.generation = stagedGeneration;
      pending->stagedSystems[systemKey] = system;

      auto dispatchStorage =
          std::make_shared<AnityGraphicsVFXInitializeStorage>();
      const size_t sourceOffset =
          static_cast<size_t>(dispatch.startEventIndex) * dispatch.strideBytes;
      dispatchStorage->records.assign(sourceRecords + sourceOffset,
          sourceRecords + sourceOffset + outputByteCount);
      dispatchStorage->info.desc = dispatch;
      dispatchStorage->info.sourceByteCount = sourceByteCount;
      dispatchStorage->info.outputByteCount = outputByteCount;
      dispatchStorage->info.backendKind = system->info.backendKind;
      dispatchStorage->info.dispatchGeneration = ++stagedGeneration;
      dispatchStorage->kernelFingerprint = fingerprint;
      pending->stagedDispatches[dispatchKey] = std::move(dispatchStorage);
    }

    uint64_t ticketId = ++registry->nextInitializeTicketId;
    if (ticketId == 0) ticketId = ++registry->nextInitializeTicketId;
    pending->info.ticketId = ticketId;
    pending->info.effectId = pending->effectIds.size() == 1
        ? pending->effectIds.front() : 0;
    pending->info.state = hasBackendHandle ? 0 : 1;
    pending->info.dispatchCount = dispatchCount;
    pending->info.backendKind = device->type == ANITY_GFX_METAL ? 2 : 0;
    pending->info.effectCount = static_cast<int32_t>(pending->effectIds.size());
    pending->info.sourceRegistryGeneration = sourceRegistryGeneration;
    pending->info.targetRegistryGeneration = stagedGeneration;
    auto inserted = registry->pendingInitializes.emplace(ticketId, pending);
    if (!inserted.second) {
      RollbackPendingInitializeLocked(device, pending.get());
      return ANITY_ERR_INTERNAL;
    }
    for (uint64_t effectId : pending->effectIds)
      registry->pendingInitializeByEffect[effectId] = ticketId;
    /* Reserve the complete transaction's generation range immediately. This
     * keeps independent effect tickets unique even when their GPU commands
     * overlap; cancellation may leave a harmless generation gap. */
    registry->generation = std::max(registry->generation, stagedGeneration);
    *outTicketId = ticketId;
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    if (pending) {
      std::lock_guard<std::mutex> lock(registry->mutex);
      RollbackPendingInitializeLocked(device, pending.get());
      ReleasePendingInitializeOwnershipLocked(registry, *pending);
      if (pending->info.ticketId != 0)
        registry->pendingInitializes.erase(pending->info.ticketId);
    }
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

AnityResult ANITY_CALL AnityGraphics_GetVFXInitializeTicketInfo(
    AnityGraphicsDevice* device, uint64_t ticketId,
    AnityGraphicsVFXInitializeTicketInfo* outInfo) {
  if (!device || ticketId == 0 || !outInfo || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->pendingInitializes.find(ticketId);
  if (found == device->vfxEvents->pendingInitializes.end())
    return ANITY_ERR_INVALID_ARG;
  auto& pending = *found->second;
  bool ready = true;
  bool failed = false;
  for (void* handle : pending.backendHandles) {
    if (!handle) continue;
    int32_t state = 0;
    AnityResult result =
        AnityGraphics_Metal_PollVFXInitializeKernel(handle, &state);
    if (result != ANITY_OK) return result;
    failed = failed || state == 2;
    ready = ready && state == 1;
  }
  pending.info.state = failed ? 2 : (ready ? 1 : 0);
  *outInfo = pending.info;
  return ANITY_OK;
}

static AnityResult CompleteVFXInitializeKernelsLocked(
    AnityGraphicsDevice* device, AnityGraphicsVFXEventRegistry* registry,
    uint64_t ticketId) {
  auto found = registry->pendingInitializes.find(ticketId);
  if (found == registry->pendingInitializes.end())
    return ANITY_ERR_INVALID_ARG;
  auto pending = found->second;
  for (size_t index = 0; index < pending->backendHandles.size(); ++index) {
    if (!pending->backendHandles[index]) continue;
    AnityResult completed = AnityGraphics_Metal_CompleteVFXInitializeKernel(
        pending->backendHandles[index]);
    pending->backendHandles[index] = nullptr;
    if (completed != ANITY_OK) {
      RollbackPendingInitializeLocked(device, pending.get());
      ReleasePendingInitializeOwnershipLocked(registry, *pending);
      registry->pendingInitializes.erase(found);
      return completed;
    }
    pending->backendCompleted[index] = 1;
    ApplyPendingInitializeResult(pending.get(), static_cast<int32_t>(index));
  }
  for (auto it = registry->initializeDispatches.begin();
       it != registry->initializeDispatches.end();) {
    if (std::find(pending->effectIds.begin(), pending->effectIds.end(),
                  it->first.effectId) != pending->effectIds.end())
      it = registry->initializeDispatches.erase(it);
    else
      ++it;
  }
  for (const auto& item : pending->stagedDispatches)
    if (std::find(pending->effectIds.begin(), pending->effectIds.end(),
                  item.first.effectId) != pending->effectIds.end())
      registry->initializeDispatches.emplace(item.first, item.second);
  for (auto it = registry->particleSystems.begin();
       it != registry->particleSystems.end();) {
    if (std::find(pending->effectIds.begin(), pending->effectIds.end(),
                  it->first.effectId) != pending->effectIds.end())
      it = registry->particleSystems.erase(it);
    else
      ++it;
  }
  for (const auto& item : pending->stagedSystems)
    if (std::find(pending->effectIds.begin(), pending->effectIds.end(),
                  item.first.effectId) != pending->effectIds.end())
      registry->particleSystems.emplace(item.first, item.second);
  registry->generation = std::max(
      registry->generation, pending->info.targetRegistryGeneration);
  if (device->type == ANITY_GFX_METAL) {
    for (size_t index = 0; index < pending->sourceGenerations.size(); ++index) {
      if (pending->sourceGenerations[index] == 0 ||
          pending->retainSnapshots[index] != 0)
        continue;
      const auto& key = pending->keys[index];
      AnityGraphics_Metal_DiscardVFXResidentSnapshot(
          device, key.effectId, key.particleSystemId,
          pending->sourceGenerations[index]);
    }
  }
  ReleasePendingInitializeOwnershipLocked(registry, *pending);
  registry->pendingInitializes.erase(found);
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_CompleteVFXInitializeKernels(
    AnityGraphicsDevice* device, uint64_t ticketId) {
  if (!device || ticketId == 0 || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  return CompleteVFXInitializeKernelsLocked(
      device, device->vfxEvents, ticketId);
}

AnityResult ANITY_CALL AnityGraphics_CancelVFXInitializeKernels(
    AnityGraphicsDevice* device, uint64_t ticketId) {
  if (!device || ticketId == 0 || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->pendingInitializes.find(ticketId);
  if (found == device->vfxEvents->pendingInitializes.end())
    return ANITY_ERR_INVALID_ARG;
  auto pending = found->second;
  for (uint64_t effectId : pending->effectIds) {
    auto updates = device->vfxEvents->pendingUpdateByEffect.find(effectId);
    if (updates == device->vfxEvents->pendingUpdateByEffect.end()) continue;
    for (uint64_t updateTicketId : updates->second) {
      auto update = device->vfxEvents->pendingUpdates.find(updateTicketId);
      if (update == device->vfxEvents->pendingUpdates.end())
        return ANITY_ERR_INTERNAL;
      if (update->second->initializeDependencyTicketId != ticketId)
        return ANITY_ERR_INVALID_ARG;
    }
    AnityResult descendants = CancelVFXPendingUpdatesForEffectLocked(
        device, device->vfxEvents, effectId);
    if (descendants != ANITY_OK) return descendants;
  }
  AnityResult result = RollbackPendingInitializeLocked(device, pending.get());
  ReleasePendingInitializeOwnershipLocked(device->vfxEvents, *pending);
  device->vfxEvents->pendingInitializes.erase(found);
  return result;
}

AnityResult ANITY_CALL AnityGraphics_SubmitVFXInitializeKernels(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* dispatches,
    const AnityGraphicsVFXInitializeKernelDesc* kernels, int32_t dispatchCount,
    const AnityGraphicsVFXInitializeAttributeDesc* attributes,
    int32_t attributeCount,
    const AnityGraphicsVFXInitializeOperationDesc* operations,
    int32_t operationCount,
    const uint8_t* sourceRecords, int32_t sourceByteCount) {
  if (!device) return ANITY_ERR_INVALID_ARG;
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  std::lock_guard<std::mutex> synchronousLock(
      registry->synchronousUpdateMutex);
  uint64_t ticketId = 0;
  AnityResult result = AnityGraphics_BeginVFXInitializeKernels(
      device, dispatches, kernels, dispatchCount,
      attributes, attributeCount, operations, operationCount,
      sourceRecords, sourceByteCount, &ticketId);
  if (result != ANITY_OK) return result;
  return AnityGraphics_CompleteVFXInitializeKernels(device, ticketId);
}

AnityResult ANITY_CALL AnityGraphics_GetVFXInitializeDispatchInfo(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int64_t initializeContextId,
    AnityGraphicsVFXInitializeDispatchInfo* outInfo) {
  if (!device || effectId == 0 || initializeContextId == 0 || !outInfo)
    return ANITY_ERR_INVALID_ARG;
  *outInfo = {};
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->initializeDispatches.find(
      AnityGraphicsVFXInitializeKey{effectId, initializeContextId});
  if (found == device->vfxEvents->initializeDispatches.end())
    return ANITY_ERR_INVALID_ARG;
  *outInfo = found->second->info;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_ReadbackVFXInitializeDispatch(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int64_t initializeContextId, uint8_t* records, int32_t recordCapacity,
    int32_t* outWritten) {
  if (!device || effectId == 0 || initializeContextId == 0 ||
      recordCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  *outWritten = 0;
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->initializeDispatches.find(
      AnityGraphicsVFXInitializeKey{effectId, initializeContextId});
  if (found == device->vfxEvents->initializeDispatches.end())
    return ANITY_ERR_INVALID_ARG;
  const int32_t required = found->second->info.outputByteCount;
  if (recordCapacity < required || (required > 0 && !records))
    return ANITY_ERR_INVALID_ARG;
  if (required > 0)
    std::memcpy(records, found->second->records.data(), static_cast<size_t>(required));
  *outWritten = required;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_GetVFXParticleSystemInfo(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId, AnityGraphicsVFXParticleSystemInfo* outInfo) {
  if (!device || effectId == 0 || particleSystemId == 0 || !outInfo)
    return ANITY_ERR_INVALID_ARG;
  *outInfo = {};
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  AnityResult finalize = FinalizeCommittedVFXPendingUpdateLocked(
      const_cast<AnityGraphicsDevice*>(device), device->vfxEvents,
      effectId, false, nullptr);
  if (finalize != ANITY_OK) return finalize;
  auto found = device->vfxEvents->particleSystems.find(
      AnityGraphicsVFXParticleSystemKey{effectId, particleSystemId});
  if (found == device->vfxEvents->particleSystems.end())
    return ANITY_ERR_INVALID_ARG;
  *outInfo = found->second->info;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_ReadbackVFXParticleSystem(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId, uint8_t* records, int32_t recordCapacity,
    int32_t* outWritten) {
  if (!device || effectId == 0 || particleSystemId == 0 ||
      recordCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  *outWritten = 0;
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  AnityResult finalize = FinalizeCommittedVFXPendingUpdateLocked(
      const_cast<AnityGraphicsDevice*>(device), device->vfxEvents,
      effectId, true, nullptr);
  if (finalize != ANITY_OK) return finalize;
  auto found = device->vfxEvents->particleSystems.find(
      AnityGraphicsVFXParticleSystemKey{effectId, particleSystemId});
  if (found == device->vfxEvents->particleSystems.end())
    return ANITY_ERR_INVALID_ARG;
  AnityResult metadata = MaterializeVFXParticleMetadataLocked(
      device, found->second.get());
  if (metadata != ANITY_OK) return metadata;
  AnityResult materialized = MaterializeVFXParticleAttributesLocked(
      device, found->second.get());
  if (materialized != ANITY_OK) return materialized;
  const int32_t required = static_cast<int32_t>(found->second->attributes.size());
  if (recordCapacity < required || (required > 0 && !records))
    return ANITY_ERR_INVALID_ARG;
  if (required > 0)
    std::memcpy(records, found->second->attributes.data(),
                static_cast<size_t>(required));
  *outWritten = required;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_ReadbackVFXParticleDeadList(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId, uint32_t* indices, int32_t indexCapacity,
    int32_t* outWritten) {
  if (!device || effectId == 0 || particleSystemId == 0 ||
      indexCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  *outWritten = 0;
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  AnityResult finalize = FinalizeCommittedVFXPendingUpdateLocked(
      const_cast<AnityGraphicsDevice*>(device), device->vfxEvents,
      effectId, true, nullptr);
  if (finalize != ANITY_OK) return finalize;
  auto found = device->vfxEvents->particleSystems.find(
      AnityGraphicsVFXParticleSystemKey{effectId, particleSystemId});
  if (found == device->vfxEvents->particleSystems.end())
    return ANITY_ERR_INVALID_ARG;
  AnityResult metadata = MaterializeVFXParticleMetadataLocked(
      device, found->second.get());
  if (metadata != ANITY_OK) return metadata;
  const int32_t required = static_cast<int32_t>(found->second->deadList.size());
  if (indexCapacity < required || (required > 0 && !indices))
    return ANITY_ERR_INVALID_ARG;
  if (required > 0)
    std::memcpy(indices, found->second->deadList.data(),
                static_cast<size_t>(required) * sizeof(uint32_t));
  *outWritten = required;
  return ANITY_OK;
}

namespace {

int32_t VFXUpdateValueComponentCount(int32_t valueType) {
  if (valueType >= 1 && valueType <= 4) return 1;
  if (valueType >= 5 && valueType <= 7) return valueType - 3;
  return 0;
}

bool IsValidVFXUpdateOffset(int32_t offset, int32_t componentCount,
                            int32_t strideBytes, bool required) {
  if (offset == -1) return !required;
  if (offset < 0 || (offset & 3) != 0 || componentCount <= 0) return false;
  return static_cast<int64_t>(offset) + componentCount * 4 <= strideBytes;
}

bool ValidateVFXUpdateOperation(
    const AnityGraphicsVFXUpdateOperationDesc& operation,
    int32_t strideBytes) {
  const int32_t expectedComponents =
      VFXUpdateValueComponentCount(operation.valueType);
  if (operation.kind < 0 || operation.kind > 6 ||
      operation.componentCount <= 0 || operation.componentCount > 4 ||
      expectedComponents != operation.componentCount ||
      operation.composition < 0 || operation.composition > 3 ||
      operation.randomMode < 0 || operation.randomMode > 2 ||
      (operation.flags & ~1) != 0 ||
      !IsValidVFXUpdateOffset(operation.targetOffsetBytes,
          operation.componentCount, strideBytes, true))
    return false;
  switch (operation.kind) {
    case 0:  // SetAttribute
      if ((operation.flags & 1) != 0 &&
          (!IsValidVFXUpdateOffset(operation.sourceAOffsetBytes,
               operation.componentCount, strideBytes, true) ||
           operation.randomMode != 0))
        return false;
      if ((operation.flags & 1) == 0 && operation.sourceAOffsetBytes != -1)
        return false;
      return operation.randomMode == 0 || operation.valueType >= 4;
    case 1:  // Copy
      return operation.composition == 0 && operation.randomMode == 0 &&
          operation.flags == 0 &&
          IsValidVFXUpdateOffset(operation.sourceAOffsetBytes,
              operation.componentCount, strideBytes, true);
    case 2:  // Integrate current source or constant valueA by deltaTime
      return operation.valueType >= 4 && operation.composition == 0 &&
          operation.randomMode == 0 && operation.flags == 0 &&
          IsValidVFXUpdateOffset(operation.sourceAOffsetBytes,
              operation.componentCount, strideBytes, false);
    case 3:  // Reap age > lifetime
      return operation.componentCount == 1 && operation.valueType == 1 &&
          operation.composition == 0 && operation.randomMode == 0 &&
          operation.flags == 0 &&
          IsValidVFXUpdateOffset(operation.sourceAOffsetBytes, 1, strideBytes, true) &&
          IsValidVFXUpdateOffset(operation.sourceBOffsetBytes, 1, strideBytes, true);
    case 4:  // Absolute force
    case 5:  // Relative force
      return operation.componentCount == 3 && operation.valueType == 6 &&
          operation.composition == 0 && operation.randomMode == 0 &&
          operation.flags == 0 &&
          IsValidVFXUpdateOffset(operation.sourceAOffsetBytes, 1, strideBytes, true);
    case 6: {  // Linear drag
      const bool usesParticleSize = operation.sourceBOffsetBytes != -1;
      return operation.componentCount == 3 && operation.valueType == 6 &&
          operation.composition == 0 && operation.randomMode == 0 &&
          operation.flags == 0 &&
          IsValidVFXUpdateOffset(operation.sourceAOffsetBytes, 1, strideBytes, true) &&
          IsValidVFXUpdateOffset(operation.sourceBOffsetBytes, 1, strideBytes, false) &&
          IsValidVFXUpdateOffset(operation.auxiliaryOffset0Bytes, 1, strideBytes, usesParticleSize) &&
          IsValidVFXUpdateOffset(operation.auxiliaryOffset1Bytes, 1, strideBytes, usesParticleSize) &&
          (usesParticleSize ||
              (operation.auxiliaryOffset0Bytes == -1 && operation.auxiliaryOffset1Bytes == -1));
    }
    default:
      return false;
  }
}

AnityResult ExecuteVFXUpdateKernelCPU(
    const AnityGraphicsVFXUpdateKernelDesc& kernel,
    const AnityGraphicsVFXUpdateOperationDesc* operations,
    AnityGraphicsVFXParticleSystemStorage* storage) {
  if (!operations || !storage) return ANITY_ERR_INVALID_ARG;
  const int32_t stride = kernel.attributeStrideBytes;
  const int32_t wordStride = stride / 4;
  try {
    const std::vector<uint8_t> sourceSnapshot = storage->attributes;
    std::vector<uint32_t> local(static_cast<size_t>(wordStride));
    const uint32_t sequentialLimit = std::min<uint32_t>(
        storage->nextSequentialIndex,
        static_cast<uint32_t>(storage->info.capacity));
    for (int32_t particleIndex = 0; particleIndex < storage->info.capacity;
         ++particleIndex) {
      const uint8_t* sourceRecord = sourceSnapshot.data() +
          static_cast<size_t>(particleIndex) * stride;
      bool initiallyAlive = kernel.aliveOffsetBytes >= 0
          ? ReadVFXParticleWord(sourceRecord, kernel.aliveOffsetBytes) != 0u
          : static_cast<uint32_t>(particleIndex) < sequentialLimit;
      if (!initiallyAlive) continue;
      std::memcpy(local.data(), sourceRecord, static_cast<size_t>(stride));
      uint32_t randomState = kernel.seedOffsetBytes >= 0
          ? local[static_cast<size_t>(kernel.seedOffsetBytes / 4)] ^
              static_cast<uint32_t>(particleIndex)
          : VFXHash(static_cast<uint32_t>(particleIndex) ^ kernel.systemSeed);
      bool usedRandom = false;
      if ((kernel.flags & 2u) == 0u || kernel.deltaTime != 0.0f) {
        for (int32_t operationIndex = 0;
             operationIndex < kernel.operationCount; ++operationIndex) {
          const auto& operation = operations[operationIndex];
          uint32_t* target = local.data() + operation.targetOffsetBytes / 4;
          const uint32_t* sourceA = operation.sourceAOffsetBytes < 0
              ? nullptr
              : local.data() + operation.sourceAOffsetBytes / 4;
          switch (operation.kind) {
            case 0: {
              float uniformRandom = 0.0f;
              if (operation.randomMode == 2) {
                uniformRandom = VFXRandom(&randomState);
                usedRandom = true;
              }
              for (int32_t component = 0;
                   component < operation.componentCount; ++component) {
                uint32_t value;
                if ((operation.flags & 1) != 0) {
                  std::memcpy(&value,
                      sourceRecord + operation.sourceAOffsetBytes + component * 4,
                      sizeof(value));
                } else {
                  value = operation.valueA[component];
                  if (operation.randomMode != 0) {
                    const float t = operation.randomMode == 2
                        ? uniformRandom : VFXRandom(&randomState);
                    usedRandom = true;
                    const float a = VFXWordToFloat(operation.valueA[component]);
                    const float b = VFXWordToFloat(operation.valueB[component]);
                    value = VFXFloatToWord(a + (b - a) * t);
                  }
                }
                target[component] = VFXComposeWord(
                    operation.valueType, operation.composition,
                    target[component], value,
                    VFXWordToFloat(operation.blendFactorBits));
              }
              break;
            }
            case 1: {
              uint32_t copied[4]{};
              std::memcpy(copied, sourceA,
                  static_cast<size_t>(operation.componentCount) * sizeof(uint32_t));
              std::memcpy(target, copied,
                  static_cast<size_t>(operation.componentCount) * sizeof(uint32_t));
              break;
            }
            case 2:
              for (int32_t component = 0;
                   component < operation.componentCount; ++component) {
                const float value = sourceA
                    ? VFXWordToFloat(sourceA[component])
                    : VFXWordToFloat(operation.valueA[component]);
                target[component] = VFXFloatToWord(
                    VFXWordToFloat(target[component]) + value * kernel.deltaTime);
              }
              break;
            case 3: {
              const float age = VFXWordToFloat(*sourceA);
              const float lifetime = VFXWordToFloat(
                  local[static_cast<size_t>(operation.sourceBOffsetBytes / 4)]);
              if (age > lifetime) target[0] = 0u;
              break;
            }
            case 4: {
              const float mass = VFXWordToFloat(*sourceA);
              for (int32_t component = 0; component < 3; ++component)
                target[component] = VFXFloatToWord(
                    VFXWordToFloat(target[component]) +
                    (VFXWordToFloat(operation.valueA[component]) / mass) *
                        kernel.deltaTime);
              break;
            }
            case 5: {
              const float mass = VFXWordToFloat(*sourceA);
              const float drag = VFXWordToFloat(operation.valueB[0]);
              const float factor = std::min(1.0f, drag * kernel.deltaTime / mass);
              for (int32_t component = 0; component < 3; ++component) {
                const float current = VFXWordToFloat(target[component]);
                const float desired = VFXWordToFloat(operation.valueA[component]);
                target[component] = VFXFloatToWord(
                    current + (desired - current) * factor);
              }
              break;
            }
            case 6: {
              const float mass = VFXWordToFloat(*sourceA);
              float drag = VFXWordToFloat(operation.valueA[0]);
              if (operation.sourceBOffsetBytes >= 0) {
                const float size = VFXWordToFloat(
                    local[static_cast<size_t>(operation.sourceBOffsetBytes / 4)]);
                const float scaleX = VFXWordToFloat(local[static_cast<size_t>(
                    operation.auxiliaryOffset0Bytes / 4)]);
                const float scaleY = VFXWordToFloat(local[static_cast<size_t>(
                    operation.auxiliaryOffset1Bytes / 4)]);
                drag *= (size * scaleX) * (size * scaleY);
              }
              const float factor = std::max(
                  0.0f, 1.0f - (drag * kernel.deltaTime) / mass);
              for (int32_t component = 0; component < 3; ++component)
                target[component] = VFXFloatToWord(
                    VFXWordToFloat(target[component]) * factor);
              break;
            }
            default:
              return ANITY_ERR_INVALID_ARG;
          }
        }
      }
      if (usedRandom && kernel.seedOffsetBytes >= 0)
        local[static_cast<size_t>(kernel.seedOffsetBytes / 4)] = randomState;
      const bool alive = kernel.aliveOffsetBytes < 0 ||
          local[static_cast<size_t>(kernel.aliveOffsetBytes / 4)] != 0u;
      uint8_t* destinationRecord = storage->attributes.data() +
          static_cast<size_t>(particleIndex) * stride;
      if (alive) {
        std::memcpy(destinationRecord, local.data(), static_cast<size_t>(stride));
      } else {
        std::memset(destinationRecord + kernel.aliveOffsetBytes, 0,
                    sizeof(uint32_t));
        if (storage->info.aliveCount <= 0) return ANITY_ERR_INTERNAL;
        --storage->info.aliveCount;
        if (storage->usesDeadList) {
          if (storage->deadList.size() >=
              static_cast<size_t>(storage->info.capacity))
            return ANITY_ERR_INTERNAL;
          storage->deadList.push_back(static_cast<uint32_t>(particleIndex));
        }
      }
    }
    storage->info.deadCount = storage->usesDeadList
        ? static_cast<int32_t>(storage->deadList.size()) : 0;
    storage->info.backendKind = 0;
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

bool IsValidVFXBoundsOffset(int32_t offset, int32_t componentCount,
                            int32_t strideBytes, bool required) {
  if (offset == -1) return !required;
  if (offset < 0 || (offset & 3) != 0 || componentCount <= 0) return false;
  const int64_t end = static_cast<int64_t>(offset) + componentCount * 4;
  return end <= strideBytes;
}

float ReadVFXParticleFloat(const uint8_t* record, int32_t offset) {
  float value = 0.0f;
  std::memcpy(&value, record + offset, sizeof(value));
  return value;
}

uint32_t ReadVFXParticleWord(const uint8_t* record, int32_t offset) {
  uint32_t value = 0;
  std::memcpy(&value, record + offset, sizeof(value));
  return value;
}

AnityResult ReduceVFXParticleBoundsCPU(
    const AnityGraphicsVFXParticleSystemStorage& storage,
    const AnityGraphicsVFXBoundsReductionDesc& desc,
    AnityGraphicsVFXBoundsReductionResult* outResult) {
  const int32_t capacity = storage.info.capacity;
  const int32_t stride = storage.info.attributeStrideBytes;
  if (storage.info.aliveCount == 0) return ANITY_OK;
  try {
    std::vector<uint8_t> deadMask;
    if (desc.aliveOffsetBytes < 0 && !storage.deadList.empty()) {
      deadMask.assign(static_cast<size_t>(capacity), 0u);
      for (uint32_t index : storage.deadList) {
        if (index >= static_cast<uint32_t>(capacity)) return ANITY_ERR_INTERNAL;
        deadMask[index] = 1u;
      }
    }

    float minimum[3] = {
        std::numeric_limits<float>::infinity(),
        std::numeric_limits<float>::infinity(),
        std::numeric_limits<float>::infinity()};
    float maximum[3] = {
        -std::numeric_limits<float>::infinity(),
        -std::numeric_limits<float>::infinity(),
        -std::numeric_limits<float>::infinity()};
    int32_t reducedCount = 0;
    const uint32_t sequentialLimit = std::min<uint32_t>(
        storage.nextSequentialIndex, static_cast<uint32_t>(capacity));
    for (int32_t particleIndex = 0; particleIndex < capacity; ++particleIndex) {
      const uint8_t* record = storage.attributes.data() +
          static_cast<size_t>(particleIndex) * stride;
      bool alive = desc.aliveOffsetBytes >= 0
          ? ReadVFXParticleWord(record, desc.aliveOffsetBytes) != 0u
          : static_cast<uint32_t>(particleIndex) < sequentialLimit &&
              (deadMask.empty() || deadMask[static_cast<size_t>(particleIndex)] == 0u);
      if (!alive) continue;

      float position[3] = {
          ReadVFXParticleFloat(record, desc.positionOffsetBytes),
          ReadVFXParticleFloat(record, desc.positionOffsetBytes + 4),
          ReadVFXParticleFloat(record, desc.positionOffsetBytes + 8)};
      float size = desc.sizeOffsetBytes >= 0
          ? std::abs(ReadVFXParticleFloat(record, desc.sizeOffsetBytes)) : 0.0f;
      float scales[3] = {
          desc.scaleXOffsetBytes >= 0
              ? std::abs(ReadVFXParticleFloat(record, desc.scaleXOffsetBytes)) : 1.0f,
          desc.scaleYOffsetBytes >= 0
              ? std::abs(ReadVFXParticleFloat(record, desc.scaleYOffsetBytes)) : 1.0f,
          desc.scaleZOffsetBytes >= 0
              ? std::abs(ReadVFXParticleFloat(record, desc.scaleZOffsetBytes)) : 1.0f};
      if (!std::isfinite(position[0]) || !std::isfinite(position[1]) ||
          !std::isfinite(position[2]) || !std::isfinite(size) ||
          !std::isfinite(scales[0]) || !std::isfinite(scales[1]) ||
          !std::isfinite(scales[2]))
        return ANITY_OK;
      for (int axis = 0; axis < 3; ++axis) {
        const float extent = size * scales[axis] * 0.5f;
        const float low = position[axis] - extent;
        const float high = position[axis] + extent;
        if (!std::isfinite(extent) || !std::isfinite(low) || !std::isfinite(high))
          return ANITY_OK;
        minimum[axis] = std::min(minimum[axis], low);
        maximum[axis] = std::max(maximum[axis], high);
      }
      ++reducedCount;
    }
    if (reducedCount != storage.info.aliveCount || reducedCount == 0)
      return ANITY_OK;

    const float padding[3] = {desc.paddingX, desc.paddingY, desc.paddingZ};
    float center[3]{};
    float extents[3]{};
    for (int axis = 0; axis < 3; ++axis) {
      center[axis] = minimum[axis] + (maximum[axis] - minimum[axis]) * 0.5f;
      extents[axis] = (maximum[axis] - minimum[axis]) * 0.5f + padding[axis];
      if (!std::isfinite(center[axis]) || !IsFiniteNonNegative(extents[axis]))
        return ANITY_OK;
    }
    outResult->valid = 1;
    outResult->centerX = center[0];
    outResult->centerY = center[1];
    outResult->centerZ = center[2];
    outResult->extentsX = extents[0];
    outResult->extentsY = extents[1];
    outResult->extentsZ = extents[2];
    outResult->backendKind = 0;
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

}  // namespace

static bool IsVFXPendingUpdateGenerationCurrent(
    const AnityGraphicsVFXEventRegistry& registry,
    const AnityGraphicsVFXPendingUpdate& pending) {
  if (!pending.frameCommitted &&
      pending.info.preparedFrameGeneration != 0) {
    auto frame = registry.frameStates.find(pending.info.effectId);
    if (frame == registry.frameStates.end() ||
        frame->second.state.prepared == 0 ||
        frame->second.state.frameIndex != pending.info.frameIndex ||
        frame->second.state.generation != pending.info.preparedFrameGeneration)
      return false;
  }
  if (pending.initializeDependencyTicketId != 0 &&
      !pending.centralPublished) {
    auto initialize = registry.pendingInitializes.find(
        pending.initializeDependencyTicketId);
    if (initialize != registry.pendingInitializes.end()) {
      const auto& dependency = *initialize->second;
      for (size_t index = 0; index < pending.keys.size(); ++index) {
        auto staged = dependency.stagedSystems.find(pending.keys[index]);
        if (staged == dependency.stagedSystems.end() ||
            staged->second != pending.sources[index] ||
            staged->second->info.generation !=
                pending.sourceGenerations[index])
          return false;
        auto committed = registry.particleSystems.find(pending.keys[index]);
        if (committed == registry.particleSystems.end()) return false;
        bool sourceMatches = false;
        for (size_t dependencyIndex = 0;
             dependencyIndex < dependency.keys.size(); ++dependencyIndex) {
          if (dependency.keys[dependencyIndex] == pending.keys[index] &&
              committed->second->info.generation ==
                  dependency.sourceGenerations[dependencyIndex]) {
            sourceMatches = true;
            break;
          }
        }
        if (!sourceMatches) return false;
      }
      return true;
    }
    /* The dependency may have been retired explicitly after Update Begin.
     * Its staged system is then the committed source generation. */
    for (size_t index = 0; index < pending.keys.size(); ++index) {
      auto committed = registry.particleSystems.find(pending.keys[index]);
      if (committed == registry.particleSystems.end() ||
          committed->second->info.generation !=
              pending.sourceGenerations[index])
        return false;
    }
    return true;
  }
  for (size_t index = 0; index < pending.keys.size(); ++index) {
    auto found = registry.particleSystems.find(pending.keys[index]);
    if (found == registry.particleSystems.end())
      return false;
    const uint64_t expected = pending.centralPublished
        ? pending.targetGenerations[index]
        : pending.sourceGenerations[index];
    if ((!pending.frameCommitted &&
         found->second->info.generation != expected) ||
        (pending.frameCommitted &&
         found->second->info.generation < expected))
      return false;
  }
  return true;
}

static void RestoreVFXPendingUpdateSourcesLocked(
    AnityGraphicsVFXEventRegistry* registry,
    const AnityGraphicsVFXPendingUpdate& pending) {
  if (!registry || !pending.centralPublished ||
      pending.sources.size() != pending.keys.size())
    return;
  for (size_t index = 0; index < pending.keys.size(); ++index)
    registry->particleSystems[pending.keys[index]] = pending.sources[index];
}

static bool RemoveVFXPendingTicketFromEffectLocked(
    AnityGraphicsVFXEventRegistry* registry, uint64_t effectId,
    uint64_t ticketId) {
  if (!registry) return false;
  auto byEffect = registry->pendingUpdateByEffect.find(effectId);
  if (byEffect == registry->pendingUpdateByEffect.end()) return false;
  auto& queue = byEffect->second;
  auto found = std::find(queue.begin(), queue.end(), ticketId);
  if (found == queue.end()) return false;
  queue.erase(found);
  if (queue.empty()) registry->pendingUpdateByEffect.erase(byEffect);
  return true;
}

static AnityResult CancelVFXPendingUpdateLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t ticketId) {
  auto found = registry->pendingUpdates.find(ticketId);
  if (found == registry->pendingUpdates.end()) return ANITY_ERR_INVALID_ARG;
  std::shared_ptr<AnityGraphicsVFXPendingUpdate> pending = found->second;
  auto byEffect = registry->pendingUpdateByEffect.find(pending->info.effectId);
  if (byEffect == registry->pendingUpdateByEffect.end() ||
      byEffect->second.empty() || byEffect->second.back() != ticketId)
    return ANITY_ERR_INVALID_ARG;
  AnityResult result = ANITY_OK;
  if (pending->backendHandle) {
    result = AnityGraphics_Metal_CancelVFXUpdateBatch(
        pending->backendHandle);
    pending->backendHandle = nullptr;
  }
  RestoreVFXPendingUpdateSourcesLocked(registry, *pending);
  RemoveVFXPendingTicketFromEffectLocked(
      registry, pending->info.effectId, ticketId);
  registry->pendingUpdates.erase(found);
  return result;
}

static AnityResult CancelVFXPendingUpdatesForEffectLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t effectId) {
  AnityResult firstFailure = ANITY_OK;
  while (true) {
    auto byEffect = registry->pendingUpdateByEffect.find(effectId);
    if (byEffect == registry->pendingUpdateByEffect.end() ||
        byEffect->second.empty())
      return firstFailure;
    /* Published generations form a stack of source snapshots.  Roll them
     * back newest-to-oldest so each ticket sees its target as resident. */
    const uint64_t ticketId = byEffect->second.back();
    AnityResult canceled = CancelVFXPendingUpdateLocked(
        device, registry, ticketId);
    /* Backend device loss must not strand central tickets whose source
     * generation can no longer become valid.  Continue releasing the chain
     * while preserving the first backend failure for the caller. */
    if (canceled != ANITY_OK && firstFailure == ANITY_OK)
      firstFailure = canceled;
  }
}

static AnityResult CompleteVFXPendingUpdateLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t ticketId) {
  auto requested = registry->pendingUpdates.find(ticketId);
  if (requested == registry->pendingUpdates.end()) return ANITY_ERR_INVALID_ARG;
  const uint64_t effectId = requested->second->info.effectId;
  auto byEffect = registry->pendingUpdateByEffect.find(effectId);
  if (byEffect == registry->pendingUpdateByEffect.end() ||
      std::find(byEffect->second.begin(), byEffect->second.end(), ticketId) ==
          byEffect->second.end())
    return ANITY_ERR_INTERNAL;
  while (true) {
    byEffect = registry->pendingUpdateByEffect.find(effectId);
    if (byEffect == registry->pendingUpdateByEffect.end() ||
        byEffect->second.empty())
      return ANITY_ERR_INTERNAL;
    const uint64_t oldestTicketId = byEffect->second.front();
    if (oldestTicketId == ticketId) break;
    auto oldest = registry->pendingUpdates.find(oldestTicketId);
    if (oldest == registry->pendingUpdates.end() ||
        !oldest->second->frameCommitted)
      return ANITY_ERR_INVALID_ARG;
    AnityResult predecessor = CompleteVFXPendingUpdateLocked(
        device, registry, oldestTicketId);
    if (predecessor != ANITY_OK) return predecessor;
  }
  auto found = registry->pendingUpdates.find(ticketId);
  if (found == registry->pendingUpdates.end()) return ANITY_ERR_INVALID_ARG;
  std::shared_ptr<AnityGraphicsVFXPendingUpdate> pending = found->second;
  if (!IsVFXPendingUpdateGenerationCurrent(*registry, *pending)) {
    AnityResult cancel = CancelVFXPendingUpdatesForEffectLocked(
        device, registry, effectId);
    return cancel == ANITY_OK ? ANITY_ERR_INVALID_ARG : cancel;
  }
  std::shared_ptr<AnityGraphicsVFXPendingInitialize> completedInitialize;
  std::unordered_map<AnityGraphicsVFXInitializeKey,
      std::shared_ptr<AnityGraphicsVFXInitializeStorage>,
      AnityGraphicsVFXInitializeKeyHash> initializeDispatchSources;
  std::unordered_map<AnityGraphicsVFXParticleSystemKey,
      std::shared_ptr<AnityGraphicsVFXParticleSystemStorage>,
      AnityGraphicsVFXParticleSystemKeyHash> initializeSystemSources;
  const auto capturesInitializeEffect = [&](uint64_t candidateEffectId) {
    return completedInitialize &&
        std::find(completedInitialize->effectIds.begin(),
                  completedInitialize->effectIds.end(), candidateEffectId) !=
            completedInitialize->effectIds.end();
  };
  const auto restoreCompletedInitialize = [&]() {
    if (!completedInitialize) return ANITY_OK;
    AnityResult result =
        RollbackPendingInitializeLocked(device, completedInitialize.get());
    if (device->type == ANITY_GFX_METAL)
      for (const auto& item : initializeSystemSources) {
        const AnityResult restored =
            AnityGraphics_Metal_RestoreVFXResidentGeneration(
                device, item.first.effectId, item.first.particleSystemId,
                item.second->info.generation);
        if (restored != ANITY_OK) result = restored;
      }
    for (auto it = registry->initializeDispatches.begin();
         it != registry->initializeDispatches.end();) {
      if (capturesInitializeEffect(it->first.effectId))
        it = registry->initializeDispatches.erase(it);
      else
        ++it;
    }
    for (const auto& item : initializeDispatchSources)
      registry->initializeDispatches.emplace(item.first, item.second);
    for (auto it = registry->particleSystems.begin();
         it != registry->particleSystems.end();) {
      if (capturesInitializeEffect(it->first.effectId))
        it = registry->particleSystems.erase(it);
      else
        ++it;
    }
    for (const auto& item : initializeSystemSources)
      registry->particleSystems.emplace(item.first, item.second);
    return result;
  };
  if (pending->initializeDependencyTicketId != 0) {
    auto dependency = registry->pendingInitializes.find(
        pending->initializeDependencyTicketId);
    if (dependency != registry->pendingInitializes.end()) {
      if (pending->backendHandle && device->type == ANITY_GFX_METAL) {
        int32_t updateState = 0;
        const AnityResult polled = AnityGraphics_Metal_PollVFXUpdateBatch(
            pending->backendHandle, &updateState);
        if (polled != ANITY_OK) return polled;
        pending->info.state = updateState;
        if (updateState == 2) {
          /* Keep the dependency unretired on the failure path.  Cancel Update
           * first (target -> Initialize target), then cancel Initialize
           * (Initialize target -> stable pre-frame source).  Both backend
           * handles still own the exact buffers needed for an atomic unwind. */
          const auto initializeToCancel = dependency->second;
          const AnityResult updateCancel =
              CancelVFXPendingUpdatesForEffectLocked(
                  device, registry, effectId);
          AnityResult initializeCancel =
              CancelPendingInitializeForEffectLocked(
                  device, registry, effectId);
          AnityResult forcedRestore = ANITY_OK;
          for (size_t index = 0;
               index < initializeToCancel->sourceGenerations.size(); ++index) {
            if (initializeToCancel->sourceGenerations[index] == 0) continue;
            const auto& key = initializeToCancel->keys[index];
            const AnityResult restored =
                AnityGraphics_Metal_RestoreVFXResidentGeneration(
                    device, key.effectId, key.particleSystemId,
                    initializeToCancel->sourceGenerations[index]);
            if (restored != ANITY_OK) forcedRestore = restored;
          }
          if (forcedRestore == ANITY_OK) initializeCancel = ANITY_OK;
          if (updateCancel != ANITY_OK) return updateCancel;
          if (forcedRestore != ANITY_OK) return forcedRestore;
          if (initializeCancel != ANITY_OK) return initializeCancel;
          return ANITY_ERR_DEVICE_LOST;
        }
      }
      completedInitialize = dependency->second;
      for (const auto& item : registry->initializeDispatches)
        if (capturesInitializeEffect(item.first.effectId))
          initializeDispatchSources.emplace(item.first, item.second);
      for (const auto& item : registry->particleSystems)
        if (capturesInitializeEffect(item.first.effectId))
          initializeSystemSources.emplace(item.first, item.second);
      /* The dependent Update is the transaction boundary. Keep the
       * pre-Initialize resident generation until that Update has either
       * committed completely or rolled the whole chain back. */
      std::fill(completedInitialize->retainSnapshots.begin(),
                completedInitialize->retainSnapshots.end(), 1);
      AnityResult initialized = CompleteVFXInitializeKernelsLocked(
          device, registry, pending->initializeDependencyTicketId);
      if (initialized != ANITY_OK) {
        CancelVFXPendingUpdatesForEffectLocked(device, registry, effectId);
        return initialized;
      }
    }
    pending->initializeDependencyTicketId = 0;
  }
  if (pending->backendHandle) {
    AnityResult result = AnityGraphics_Metal_CompleteVFXUpdateBatch(
        pending->backendHandle);
    pending->backendHandle = nullptr;
    if (result != ANITY_OK) {
      /* A backend failure invalidates every descendant generation.  Roll the
       * queue back newest-to-oldest so no later ticket retains a dead source. */
      const AnityResult canceled =
          CancelVFXPendingUpdatesForEffectLocked(device, registry, effectId);
      const AnityResult restored = restoreCompletedInitialize();
      if (restored != ANITY_OK) return restored;
      if (canceled != ANITY_OK) return canceled;
      return result;
    }
    /* A later GPU generation may have been published before this ticket's
     * CPU metadata retires.  Rebase the cloned replacement on its predecessor,
     * which has already been completed because effect queues retire in order. */
    for (size_t index = 0; index < pending->replacements.size(); ++index) {
      auto& replacement = *pending->replacements[index];
      const auto& source = *pending->sources[index];
      replacement.info.aliveCount = source.info.aliveCount;
      replacement.info.deadCount = source.info.deadCount;
      replacement.deadList = source.deadList;
      replacement.nextSequentialIndex = source.nextSequentialIndex;
      replacement.usesDeadList = source.usesDeadList;
    }
    bool metadataValid = true;
    for (size_t index = 0; index < pending->replacements.size(); ++index) {
      const auto& replacement = *pending->replacements[index];
      const int32_t deadCount = pending->deadCounts[index];
      if (deadCount < 0 || deadCount > replacement.info.aliveCount ||
          (replacement.usesDeadList &&
           replacement.deadList.size() + static_cast<size_t>(deadCount) >
               static_cast<size_t>(replacement.info.capacity))) {
        metadataValid = false;
        break;
      }
    }
    if (!metadataValid) {
      AnityResult restoreResult = ANITY_OK;
      const AnityResult cancelResult =
          CancelVFXPendingUpdatesForEffectLocked(device, registry, effectId);
      if (pending->residentPublished) {
        for (size_t index = 0; index < pending->keys.size(); ++index) {
          const AnityResult restored =
              AnityGraphics_Metal_RestoreVFXResidentGeneration(
                  device, pending->keys[index].effectId,
                  pending->keys[index].particleSystemId,
                  pending->sourceGenerations[index]);
          if (restored != ANITY_OK) restoreResult = restored;
        }
      }
      const AnityResult initializeRestore = restoreCompletedInitialize();
      if (initializeRestore != ANITY_OK) restoreResult = initializeRestore;
      if (restoreResult != ANITY_OK) return restoreResult;
      if (cancelResult != ANITY_OK) return cancelResult;
      return ANITY_ERR_INTERNAL;
    }
    for (size_t index = 0; index < pending->replacements.size(); ++index) {
      auto& replacement = *pending->replacements[index];
      const int32_t deadCount = pending->deadCounts[index];
      replacement.info.aliveCount -= deadCount;
      if (replacement.usesDeadList)
        replacement.deadList.insert(
            replacement.deadList.end(), pending->deadIndices[index].begin(),
            pending->deadIndices[index].begin() + deadCount);
      replacement.info.deadCount = replacement.usesDeadList
          ? static_cast<int32_t>(replacement.deadList.size()) : 0;
      replacement.info.backendKind = 2;
      replacement.attributesResidentOnly = true;
    }
  }
  for (size_t index = 0; index < pending->keys.size(); ++index) {
    auto current = registry->particleSystems.find(pending->keys[index]);
    if (!pending->centralPublished ||
        current == registry->particleSystems.end() ||
        current->second->info.generation == pending->targetGenerations[index])
      registry->particleSystems[pending->keys[index]] =
          pending->replacements[index];
  }
  if (device->type == ANITY_GFX_METAL &&
      (pending->frameCommitted || pending->info.preparedFrameGeneration == 0)) {
    for (size_t index = 0; index < pending->keys.size(); ++index)
      AnityGraphics_Metal_DiscardVFXResidentSnapshot(
          device, pending->keys[index].effectId,
          pending->keys[index].particleSystemId,
          pending->sourceGenerations[index]);
    if (completedInitialize) {
      for (size_t index = 0;
           index < completedInitialize->sourceGenerations.size(); ++index) {
        if (completedInitialize->sourceGenerations[index] == 0) continue;
        const auto& key = completedInitialize->keys[index];
        AnityGraphics_Metal_DiscardVFXResidentSnapshot(
            device, key.effectId, key.particleSystemId,
            completedInitialize->sourceGenerations[index]);
      }
    }
  }
  RemoveVFXPendingTicketFromEffectLocked(
      registry, pending->info.effectId, ticketId);
  registry->pendingUpdates.erase(found);
  return ANITY_OK;
}

static AnityResult PublishVFXPendingUpdateLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t ticketId) {
  auto found = registry->pendingUpdates.find(ticketId);
  if (found == registry->pendingUpdates.end()) return ANITY_ERR_INVALID_ARG;
  std::shared_ptr<AnityGraphicsVFXPendingUpdate> pending = found->second;
  if (pending->residentPublished) return ANITY_OK;
  if (!pending->backendHandle || device->type != ANITY_GFX_METAL ||
      !IsVFXPendingUpdateGenerationCurrent(*registry, *pending))
    return ANITY_ERR_INVALID_ARG;
  AnityResult result = AnityGraphics_Metal_PublishVFXUpdateBatch(
      pending->backendHandle);
  if (result != ANITY_OK) return result;
  pending->residentPublished = true;
  if (pending->initializeDependencyTicketId == 0) {
    pending->centralPublished = true;
    for (size_t index = 0; index < pending->keys.size(); ++index) {
      auto& replacement = *pending->replacements[index];
      replacement.info.backendKind = 2;
      replacement.attributesResidentOnly = true;
      registry->particleSystems[pending->keys[index]] =
          pending->replacements[index];
    }
  }
  return ANITY_OK;
}

static AnityResult FinalizeCommittedVFXPendingUpdateLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t effectId, bool wait, bool* outFinalized) {
  if (outFinalized) *outFinalized = true;
  while (true) {
    auto byEffect = registry->pendingUpdateByEffect.find(effectId);
    if (byEffect == registry->pendingUpdateByEffect.end() ||
        byEffect->second.empty())
      return ANITY_OK;
    const uint64_t ticketId = byEffect->second.front();
    auto found = registry->pendingUpdates.find(ticketId);
    if (found == registry->pendingUpdates.end()) return ANITY_ERR_INTERNAL;
    auto& pending = *found->second;
    if (!pending.frameCommitted) {
      if (outFinalized) *outFinalized = false;
      /* An uncommitted frame owns only staged update data.  Readers continue
       * to observe the stable resident/source generation until Commit; a
       * blocking read is not authority to publish or cancel that frame. */
      return ANITY_OK;
    }
    if (!wait && pending.backendHandle) {
      int32_t state = 0;
      AnityResult poll = AnityGraphics_Metal_PollVFXUpdateBatch(
          pending.backendHandle, &state);
      if (poll != ANITY_OK) return poll;
      pending.info.state = state;
      if (state == 0) {
        if (outFinalized) *outFinalized = false;
        return ANITY_OK;
      }
    }
    AnityResult completed = CompleteVFXPendingUpdateLocked(
        device, registry, ticketId);
    if (completed != ANITY_OK) return completed;
  }
}

static AnityResult PollCommittedVFXPendingUpdateForPreparationLocked(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXEventRegistry* registry,
    uint64_t effectId, bool* outFinalized) {
  if (outFinalized) *outFinalized = true;
  while (true) {
    auto byEffect = registry->pendingUpdateByEffect.find(effectId);
    if (byEffect == registry->pendingUpdateByEffect.end() ||
        byEffect->second.empty())
      return ANITY_OK;
    const uint64_t ticketId = byEffect->second.front();
    auto found = registry->pendingUpdates.find(ticketId);
    if (found == registry->pendingUpdates.end()) return ANITY_ERR_INTERNAL;
    auto& pending = *found->second;
    if (!pending.frameCommitted || !pending.backendHandle ||
        device->type != ANITY_GFX_METAL) {
      if (outFinalized) *outFinalized = false;
      return ANITY_OK;
    }
    int32_t state = 0;
    AnityResult poll = AnityGraphics_Metal_PollVFXUpdateBatchForPreparation(
        pending.backendHandle, &state);
    if (poll != ANITY_OK) return poll;
    pending.info.state = state;
    if (state == 0) {
      if (outFinalized) *outFinalized = false;
      return ANITY_OK;
    }
    AnityResult completed = CompleteVFXPendingUpdateLocked(
        device, registry, ticketId);
    if (completed != ANITY_OK) return completed;
  }
}

static AnityResult BeginVFXUpdateKernelsInternal(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXUpdateKernelDesc* kernels, int32_t kernelCount,
    const AnityGraphicsVFXUpdateOperationDesc* operations,
    int32_t operationCount,
    const AnityGraphicsVFXBoundsReductionDesc* boundsDescs,
    int32_t boundsCount, uint64_t* outTicketId) {
  if (!device || !kernels || kernelCount <= 0 || kernelCount > 4096 ||
      !operations || operationCount <= 0 || operationCount > 65536 ||
      !outTicketId ||
      (boundsCount != 0 && (!boundsDescs || boundsCount != kernelCount)))
    return ANITY_ERR_INVALID_ARG;
  *outTicketId = 0;
  const uint64_t effectId = kernels[0].effectId;
  for (int32_t index = 0; index < kernelCount; ++index) {
    const auto& kernel = kernels[index];
    if (kernel.version != 1u || (kernel.flags & ~3u) != 0u ||
        kernel.effectId == 0 || kernel.contextId == 0 ||
        kernel.particleSystemId == 0 || kernel.particleCapacity <= 0 ||
        kernel.attributeStrideBytes <= 0 ||
        (kernel.attributeStrideBytes & 3) != 0 ||
        kernel.effectId != effectId ||
        kernel.operationStart < 0 || kernel.operationCount <= 0 ||
        static_cast<int64_t>(kernel.operationStart) + kernel.operationCount >
            operationCount)
      return ANITY_ERR_INVALID_ARG;
  }
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  try {
    std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
    AnityGraphicsVFXEventRegistry& registry = *device->vfxEvents;
    std::shared_ptr<AnityGraphicsVFXPendingInitialize> initializeDependency;
    uint64_t initializeDependencyTicketId = 0;
    auto initializeOwner = registry.pendingInitializeByEffect.find(effectId);
    if (initializeOwner != registry.pendingInitializeByEffect.end()) {
      if (device->type != ANITY_GFX_METAL) return ANITY_ERR_INVALID_ARG;
      auto initialize = registry.pendingInitializes.find(
          initializeOwner->second);
      if (initialize == registry.pendingInitializes.end())
        return ANITY_ERR_INTERNAL;
      initializeDependency = initialize->second;
      initializeDependencyTicketId = initializeOwner->second;
    }
    auto queued = registry.pendingUpdateByEffect.find(effectId);
    if (queued != registry.pendingUpdateByEffect.end()) {
      if (initializeDependency) return ANITY_ERR_INVALID_ARG;
      for (uint64_t queuedTicketId : queued->second) {
        auto queuedTicket = registry.pendingUpdates.find(queuedTicketId);
        if (queuedTicket == registry.pendingUpdates.end())
          return ANITY_ERR_INTERNAL;
        if (!queuedTicket->second->frameCommitted)
          return ANITY_ERR_INVALID_ARG;
      }
      /* Metal owns three update slots per particle system.  Retire only the
       * oldest generation when the central transaction queue reaches that
       * capacity; one- and two-frame GPU latency stays fully asynchronous. */
      while (true) {
        queued = registry.pendingUpdateByEffect.find(effectId);
        if (queued == registry.pendingUpdateByEffect.end() ||
            queued->second.size() < 3u)
          break;
        const uint64_t oldestTicketId = queued->second.front();
        AnityResult retired = CompleteVFXPendingUpdateLocked(
            device, &registry, oldestTicketId);
        if (retired != ANITY_OK) return retired;
      }
    }
    auto pending = std::make_shared<AnityGraphicsVFXPendingUpdate>();
    pending->initializeDependencyTicketId =
        initializeDependencyTicketId;
    uint64_t stagedGeneration = device->vfxEvents->generation;
    pending->keys.reserve(static_cast<size_t>(kernelCount));
    pending->replacements.reserve(static_cast<size_t>(kernelCount));
    pending->sources.reserve(static_cast<size_t>(kernelCount));
    pending->sourceGenerations.reserve(static_cast<size_t>(kernelCount));
    pending->targetGenerations.reserve(static_cast<size_t>(kernelCount));
    pending->deadIndices.reserve(static_cast<size_t>(kernelCount));
    pending->deadCounts.assign(static_cast<size_t>(kernelCount), 0);
    for (int32_t index = 0; index < kernelCount; ++index) {
      const auto& kernel = kernels[index];
      const AnityGraphicsVFXParticleSystemKey key{
          kernel.effectId, kernel.particleSystemId};
      if (std::find(pending->keys.begin(), pending->keys.end(), key) !=
          pending->keys.end())
        return ANITY_ERR_INVALID_ARG;
      std::shared_ptr<AnityGraphicsVFXParticleSystemStorage> existingStorage;
      if (initializeDependency) {
        auto staged = initializeDependency->stagedSystems.find(key);
        if (staged == initializeDependency->stagedSystems.end() ||
            !staged->second->attributesResidentOnly)
          return ANITY_ERR_INVALID_ARG;
        bool residentTarget = false;
        for (size_t reverse = initializeDependency->keys.size();
             reverse > 0; --reverse) {
          const size_t dependencyIndex = reverse - 1u;
          if (initializeDependency->keys[dependencyIndex] == key &&
              initializeDependency->sourceGenerations[dependencyIndex] != 0 &&
              initializeDependency->targetGenerations[dependencyIndex] ==
                  staged->second->info.generation &&
              (initializeDependency->backendHandles[dependencyIndex] ||
               initializeDependency->backendCompleted[dependencyIndex] != 0)) {
            residentTarget = true;
            break;
          }
        }
        if (!residentTarget) return ANITY_ERR_INVALID_ARG;
        existingStorage = staged->second;
      } else {
        auto found = registry.particleSystems.find(key);
        if (found == registry.particleSystems.end())
          return ANITY_ERR_INVALID_ARG;
        existingStorage = found->second;
      }
      const auto& existing = *existingStorage;
      if (existing.info.capacity != kernel.particleCapacity ||
          existing.info.attributeStrideBytes != kernel.attributeStrideBytes ||
          existing.usesDeadList != ((kernel.flags & 1u) != 0u) ||
          !IsValidVFXUpdateOffset(kernel.aliveOffsetBytes, 1,
              kernel.attributeStrideBytes, false) ||
          !IsValidVFXUpdateOffset(kernel.seedOffsetBytes, 1,
              kernel.attributeStrideBytes, false))
        return ANITY_ERR_INVALID_ARG;
      const auto* kernelOperations = operations + kernel.operationStart;
      bool usesRandom = false;
      for (int32_t operationIndex = 0;
           operationIndex < kernel.operationCount; ++operationIndex) {
        const auto& operation = kernelOperations[operationIndex];
        if (!ValidateVFXUpdateOperation(
                operation, kernel.attributeStrideBytes) ||
            (operation.kind == 3 &&
             operation.targetOffsetBytes != kernel.aliveOffsetBytes))
          return ANITY_ERR_INVALID_ARG;
        usesRandom = usesRandom || operation.randomMode != 0;
      }
      if (usesRandom && kernel.seedOffsetBytes < 0)
        return ANITY_ERR_INVALID_ARG;
      if (boundsCount != 0 && boundsDescs[index].effectId != 0) {
        const auto& bounds = boundsDescs[index];
        if (bounds.effectId != kernel.effectId ||
            bounds.particleSystemId != kernel.particleSystemId ||
            bounds.aliveOffsetBytes < 0 ||
            !IsFiniteNonNegative(bounds.paddingX) ||
            !IsFiniteNonNegative(bounds.paddingY) ||
            !IsFiniteNonNegative(bounds.paddingZ) ||
            (bounds.boundsInWorldSpace != 0 &&
             bounds.boundsInWorldSpace != 1) ||
            bounds.reserved != 0 ||
            !IsValidVFXBoundsOffset(
                bounds.positionOffsetBytes, 3,
                kernel.attributeStrideBytes, true) ||
            !IsValidVFXBoundsOffset(
                bounds.aliveOffsetBytes, 1,
                kernel.attributeStrideBytes, false) ||
            !IsValidVFXBoundsOffset(
                bounds.sizeOffsetBytes, 1,
                kernel.attributeStrideBytes, false) ||
            !IsValidVFXBoundsOffset(
                bounds.scaleXOffsetBytes, 1,
                kernel.attributeStrideBytes, false) ||
            !IsValidVFXBoundsOffset(
                bounds.scaleYOffsetBytes, 1,
                kernel.attributeStrideBytes, false) ||
            !IsValidVFXBoundsOffset(
                bounds.scaleZOffsetBytes, 1,
                kernel.attributeStrideBytes, false))
          return ANITY_ERR_INVALID_ARG;
      }
      auto replacement =
          std::make_shared<AnityGraphicsVFXParticleSystemStorage>(existing);
      replacement->deadList.reserve(
          static_cast<size_t>(replacement->info.capacity));
      if (stagedGeneration == std::numeric_limits<uint64_t>::max())
        return ANITY_ERR_INTERNAL;
      const uint64_t targetGeneration = stagedGeneration + 1u;
      stagedGeneration = targetGeneration;
      replacement->info.generation = targetGeneration;
      pending->keys.push_back(key);
      pending->sources.push_back(existingStorage);
      pending->replacements.push_back(std::move(replacement));
      pending->sourceGenerations.push_back(existing.info.generation);
      pending->targetGenerations.push_back(targetGeneration);
      pending->deadIndices.emplace_back(
          static_cast<size_t>(kernel.particleCapacity));
    }

    uint64_t ticketId = ++registry.nextUpdateTicketId;
    if (ticketId == 0) ticketId = ++registry.nextUpdateTicketId;
    pending->info.ticketId = ticketId;
    pending->info.effectId = effectId;
    pending->info.kernelCount = kernelCount;
    pending->info.backendKind = device->type == ANITY_GFX_METAL ? 2 : 0;
    pending->info.submitGeneration = stagedGeneration;
    auto prepared = registry.frameStates.find(effectId);
    if (prepared != registry.frameStates.end() &&
        prepared->second.state.prepared != 0) {
      pending->info.frameIndex = prepared->second.state.frameIndex;
      pending->info.preparedFrameGeneration =
          prepared->second.state.generation;
    }
    registry.generation = stagedGeneration;
    bool insertedTicket = false;
    bool queuedTicket = false;
    try {
      insertedTicket = registry.pendingUpdates.emplace(
          ticketId, pending).second;
      if (!insertedTicket) return ANITY_ERR_INTERNAL;
      auto [effectQueue, insertedQueue] =
          registry.pendingUpdateByEffect.try_emplace(effectId);
      effectQueue->second.push_back(ticketId);
      queuedTicket = true;
      if (effectQueue->second.size() > 3u) {
        RemoveVFXPendingTicketFromEffectLocked(
            &registry, effectId, ticketId);
        queuedTicket = false;
        if (insertedTicket) registry.pendingUpdates.erase(ticketId);
        return ANITY_ERR_INTERNAL;
      }
    } catch (const std::bad_alloc&) {
      if (queuedTicket)
        RemoveVFXPendingTicketFromEffectLocked(
            &registry, effectId, ticketId);
      if (insertedTicket) registry.pendingUpdates.erase(ticketId);
      throw;
    }
    const auto discardPending = [&]() {
      if (pending->backendHandle) {
        AnityGraphics_Metal_CancelVFXUpdateBatch(pending->backendHandle);
        pending->backendHandle = nullptr;
      }
      RemoveVFXPendingTicketFromEffectLocked(
          &registry, effectId, ticketId);
      registry.pendingUpdates.erase(ticketId);
    };

    try {
      if (device->type == ANITY_GFX_METAL) {
      std::vector<uint8_t*> particleRecords;
      std::vector<int32_t> particleByteCounts;
      std::vector<uint32_t> nextSequentialIndices;
      std::vector<int32_t> sourceAliveCounts;
      std::vector<const uint32_t*> sourceDeadLists;
      std::vector<int32_t> sourceDeadCounts;
      std::vector<int32_t> usesDeadLists;
      std::vector<int32_t> retainSourceGenerations;
      std::vector<uint32_t*> deadIndexPointers;
      std::vector<int32_t> deadIndexCapacities;
      particleRecords.reserve(static_cast<size_t>(kernelCount));
      particleByteCounts.reserve(static_cast<size_t>(kernelCount));
      nextSequentialIndices.reserve(static_cast<size_t>(kernelCount));
      sourceAliveCounts.reserve(static_cast<size_t>(kernelCount));
      sourceDeadLists.reserve(static_cast<size_t>(kernelCount));
      sourceDeadCounts.reserve(static_cast<size_t>(kernelCount));
      usesDeadLists.reserve(static_cast<size_t>(kernelCount));
      retainSourceGenerations.reserve(static_cast<size_t>(kernelCount));
      deadIndexPointers.reserve(static_cast<size_t>(kernelCount));
      deadIndexCapacities.reserve(static_cast<size_t>(kernelCount));
      for (int32_t index = 0; index < kernelCount; ++index) {
        auto& replacement = *pending->replacements[static_cast<size_t>(index)];
        particleRecords.push_back(replacement.attributes.data());
        particleByteCounts.push_back(
            static_cast<int32_t>(replacement.attributes.size()));
        nextSequentialIndices.push_back(replacement.nextSequentialIndex);
        sourceAliveCounts.push_back(replacement.info.aliveCount);
        sourceDeadLists.push_back(replacement.deadList.empty()
            ? nullptr : replacement.deadList.data());
        sourceDeadCounts.push_back(
            static_cast<int32_t>(replacement.deadList.size()));
        usesDeadLists.push_back(replacement.usesDeadList ? 1 : 0);
        retainSourceGenerations.push_back(
            pending->info.preparedFrameGeneration != 0 ? 1 : 0);
        deadIndexPointers.push_back(
            pending->deadIndices[static_cast<size_t>(index)].data());
        deadIndexCapacities.push_back(kernels[index].particleCapacity);
      }
      AnityResult result = AnityGraphics_Metal_BeginVFXUpdateBatch(
          device, kernels, kernelCount, operations,
          particleRecords.data(), particleByteCounts.data(),
          nextSequentialIndices.data(), sourceAliveCounts.data(),
          sourceDeadLists.data(), sourceDeadCounts.data(),
          usesDeadLists.data(),
          retainSourceGenerations.data(),
          pending->sourceGenerations.data(),
          pending->targetGenerations.data(), deadIndexPointers.data(),
          deadIndexCapacities.data(), pending->deadCounts.data(),
          boundsDescs, boundsCount,
          &pending->backendHandle);
      if (result != ANITY_OK) {
        discardPending();
        return result;
      }
      pending->info.state = 0;
      } else {
        for (int32_t index = 0; index < kernelCount; ++index) {
          AnityResult result = ExecuteVFXUpdateKernelCPU(
              kernels[index], operations + kernels[index].operationStart,
              pending->replacements[static_cast<size_t>(index)].get());
          if (result != ANITY_OK) {
            discardPending();
            return result;
          }
        }
        pending->cpuReady = true;
        pending->info.state = 1;
      }
    } catch (const std::bad_alloc&) {
      discardPending();
      throw;
    }
    *outTicketId = ticketId;
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

AnityResult ANITY_CALL AnityGraphics_BeginVFXUpdateKernels(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXUpdateKernelDesc* kernels, int32_t kernelCount,
    const AnityGraphicsVFXUpdateOperationDesc* operations,
    int32_t operationCount, uint64_t* outTicketId) {
  return BeginVFXUpdateKernelsInternal(
      device, kernels, kernelCount, operations, operationCount,
      nullptr, 0, outTicketId);
}

AnityResult ANITY_CALL AnityGraphics_BeginVFXUpdateKernelsWithBounds(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXUpdateKernelDesc* kernels, int32_t kernelCount,
    const AnityGraphicsVFXUpdateOperationDesc* operations,
    int32_t operationCount,
    const AnityGraphicsVFXBoundsReductionDesc* boundsDescs,
    int32_t boundsCount, uint64_t* outTicketId) {
  return BeginVFXUpdateKernelsInternal(
      device, kernels, kernelCount, operations, operationCount,
      boundsDescs, boundsCount, outTicketId);
}

AnityResult ANITY_CALL AnityGraphics_GetVFXUpdateTicketInfo(
    AnityGraphicsDevice* device, uint64_t ticketId,
    AnityGraphicsVFXUpdateTicketInfo* outInfo) {
  if (!device || ticketId == 0 || !outInfo || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->pendingUpdates.find(ticketId);
  if (found == device->vfxEvents->pendingUpdates.end())
    return ANITY_ERR_INVALID_ARG;
  auto& pending = *found->second;
  if (pending.backendHandle) {
    int32_t state = 0;
    AnityResult result = AnityGraphics_Metal_PollVFXUpdateBatch(
        pending.backendHandle, &state);
    if (result != ANITY_OK) return result;
    pending.info.state = state;
  }
  *outInfo = pending.info;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_CompleteVFXUpdateKernels(
    AnityGraphicsDevice* device, uint64_t ticketId) {
  if (!device || ticketId == 0 || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  return CompleteVFXPendingUpdateLocked(
      device, device->vfxEvents, ticketId);
}

AnityResult ANITY_CALL AnityGraphics_CancelVFXUpdateKernels(
    AnityGraphicsDevice* device, uint64_t ticketId) {
  if (!device || ticketId == 0 || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->pendingUpdates.find(ticketId);
  if (found != device->vfxEvents->pendingUpdates.end() &&
      found->second->frameCommitted)
    return ANITY_ERR_INVALID_ARG;
  return CancelVFXPendingUpdateLocked(
      device, device->vfxEvents, ticketId);
}

AnityResult ANITY_CALL AnityGraphics_DispatchVFXUpdateKernels(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXUpdateKernelDesc* kernels, int32_t kernelCount,
    const AnityGraphicsVFXUpdateOperationDesc* operations,
    int32_t operationCount) {
  if (!device || !device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> synchronousLock(
      device->vfxEvents->synchronousUpdateMutex);
  uint64_t ticketId = 0;
  AnityResult result = AnityGraphics_BeginVFXUpdateKernels(
      device, kernels, kernelCount, operations, operationCount, &ticketId);
  if (result != ANITY_OK) return result;
  return AnityGraphics_CompleteVFXUpdateKernels(device, ticketId);
}

AnityResult ANITY_CALL AnityGraphics_GetVFXUpdateBackendStats(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId,
    AnityGraphicsVFXUpdateBackendStats* outStats) {
  if (!device || effectId == 0 || particleSystemId == 0 || !outStats)
    return ANITY_ERR_INVALID_ARG;
  std::memset(outStats, 0, sizeof(*outStats));
  if (device->type != ANITY_GFX_METAL) return ANITY_ERR_NOT_SUPPORTED;
  return AnityGraphics_Metal_GetVFXUpdateBackendStats(
      device, effectId, particleSystemId, outStats);
}

AnityResult ANITY_CALL AnityGraphics_SetVFXFailureInjection(
    AnityGraphicsDevice* device, AnityGraphicsVFXFailurePoint failurePoint,
    int32_t failureCount) {
  if (!device || failureCount < 0 || failureCount > 1024 ||
      (failurePoint != ANITY_GFX_VFX_FAILURE_INITIALIZE_COMMAND &&
       failurePoint != ANITY_GFX_VFX_FAILURE_UPDATE_COMMAND &&
       failurePoint != ANITY_GFX_VFX_FAILURE_PLANAR_CAMERA_COMMAND &&
       failurePoint != ANITY_GFX_VFX_FAILURE_DEVICE_REMOVAL))
    return ANITY_ERR_INVALID_ARG;
  if (device->type != ANITY_GFX_METAL) return ANITY_ERR_NOT_SUPPORTED;
  return AnityGraphics_Metal_SetVFXFailureInjection(
      device, static_cast<int32_t>(failurePoint), failureCount);
}

AnityResult ANITY_CALL AnityGraphics_ReduceVFXParticleBounds(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXBoundsReductionDesc* desc,
    AnityGraphicsVFXBoundsReductionResult* outResult) {
  if (!device || !desc || !outResult || desc->effectId == 0 ||
      desc->particleSystemId == 0 ||
      !IsFiniteNonNegative(desc->paddingX) ||
      !IsFiniteNonNegative(desc->paddingY) ||
      !IsFiniteNonNegative(desc->paddingZ) ||
      (desc->boundsInWorldSpace != 0 && desc->boundsInWorldSpace != 1) ||
      desc->reserved != 0)
    return ANITY_ERR_INVALID_ARG;
  *outResult = {};
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  AnityResult finalized = FinalizeCommittedVFXPendingUpdateLocked(
      device, device->vfxEvents, desc->effectId, true, nullptr);
  if (finalized != ANITY_OK) return finalized;
  auto initializeOwner = device->vfxEvents->pendingInitializeByEffect.find(
      desc->effectId);
  if (initializeOwner != device->vfxEvents->pendingInitializeByEffect.end()) {
    /* Bounds is an explicit CPU-visible dependency.  If no committed Update
     * consumed this Initialize, retire it here under the same registry lock;
     * a committed Initialize -> Update chain was already retired above. */
    AnityResult initialized = CompleteVFXInitializeKernelsLocked(
        device, device->vfxEvents, initializeOwner->second);
    if (initialized != ANITY_OK) return initialized;
  }
  auto prepared = device->vfxEvents->frameStates.find(desc->effectId);
  if (prepared != device->vfxEvents->frameStates.end() &&
      prepared->second.state.prepared != 0)
    return ANITY_ERR_INVALID_ARG;
  auto found = device->vfxEvents->particleSystems.find(
      AnityGraphicsVFXParticleSystemKey{
          desc->effectId, desc->particleSystemId});
  if (found == device->vfxEvents->particleSystems.end())
    return ANITY_ERR_INVALID_ARG;
  AnityGraphicsVFXParticleSystemStorage& storage = *found->second;
  const int32_t stride = storage.info.attributeStrideBytes;
  if (!IsValidVFXBoundsOffset(desc->positionOffsetBytes, 3, stride, true) ||
      !IsValidVFXBoundsOffset(desc->aliveOffsetBytes, 1, stride, false) ||
      !IsValidVFXBoundsOffset(desc->sizeOffsetBytes, 1, stride, false) ||
      !IsValidVFXBoundsOffset(desc->scaleXOffsetBytes, 1, stride, false) ||
      !IsValidVFXBoundsOffset(desc->scaleYOffsetBytes, 1, stride, false) ||
      !IsValidVFXBoundsOffset(desc->scaleZOffsetBytes, 1, stride, false))
    return ANITY_ERR_INVALID_ARG;

  outResult->effectId = desc->effectId;
  outResult->particleSystemId = desc->particleSystemId;
  outResult->boundsInWorldSpace = desc->boundsInWorldSpace;
  outResult->generation = storage.info.generation;
  if (device->type == ANITY_GFX_METAL && desc->aliveOffsetBytes >= 0) {
    AnityResult metalResult = AnityGraphics_Metal_ReduceVFXParticleBounds(
        device, desc, storage.attributes.data(),
        static_cast<int32_t>(storage.attributes.size()), storage.info.capacity,
        stride, storage.info.aliveCount, storage.nextSequentialIndex,
        storage.info.generation,
        storage.attributesResidentOnly ? 0 : 1, outResult);
    if (metalResult == ANITY_OK) return ANITY_OK;
    if (metalResult != ANITY_ERR_NOT_SUPPORTED) return metalResult;
  }
  AnityResult materialized = MaterializeVFXParticleAttributesLocked(
      device, &storage);
  if (materialized != ANITY_OK) return materialized;
  return ReduceVFXParticleBoundsCPU(storage, *desc, outResult);
}

AnityResult ANITY_CALL AnityGraphics_BeginVFXFrame(
    AnityGraphicsDevice* device, uint32_t* outFrameIndex) {
  if (!device || !outFrameIndex) return ANITY_ERR_INVALID_ARG;
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  std::lock_guard<std::mutex> lock(registry->mutex);
  registry->frameIndex = static_cast<uint32_t>(registry->frameIndex + 1u);
  *outFrameIndex = registry->frameIndex;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_BeginVFXPlayerLoopFrame(
    AnityGraphicsDevice* device, uint64_t playerLoopToken,
    uint32_t* outFrameIndex, int32_t* outBeganFrame) {
  if (!device || playerLoopToken == 0 || !outFrameIndex || !outBeganFrame)
    return ANITY_ERR_INVALID_ARG;
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  std::lock_guard<std::mutex> lock(registry->mutex);
  if (registry->playerLoopToken != 0 &&
      playerLoopToken < registry->playerLoopToken)
    return ANITY_ERR_INVALID_ARG;
  if (playerLoopToken == registry->playerLoopToken) {
    if (registry->frameIndex != registry->playerLoopFrameIndex)
      return ANITY_ERR_INVALID_ARG;
    *outFrameIndex = registry->playerLoopFrameIndex;
    *outBeganFrame = 0;
    return ANITY_OK;
  }
  registry->frameIndex = static_cast<uint32_t>(registry->frameIndex + 1u);
  registry->playerLoopToken = playerLoopToken;
  registry->playerLoopFrameIndex = registry->frameIndex;
  *outFrameIndex = registry->frameIndex;
  *outBeganFrame = 1;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_BeginVFXCullingFrame(
    AnityGraphicsDevice* device, uint64_t playerLoopToken,
    const AnityGraphicsVFXCullingBounds* bounds, int32_t boundsCount) {
  if (!device || playerLoopToken == 0 || boundsCount < 0 ||
      boundsCount > kMaxCullingEffects || (boundsCount > 0 && !bounds))
    return ANITY_ERR_INVALID_ARG;
  try {
    std::vector<uint64_t> effectIds;
    effectIds.reserve(static_cast<size_t>(boundsCount));
    std::unordered_map<uint64_t, AnityGraphicsVFXCullingBounds> stagedBounds;
    stagedBounds.reserve(static_cast<size_t>(boundsCount));
    for (int32_t index = 0; index < boundsCount; ++index) {
      const AnityGraphicsVFXCullingBounds& item = bounds[index];
      if (item.effectId == 0 || item.layer < 0 || item.layer > 31 ||
          (item.valid != 0 && item.valid != 1) ||
          (item.valid != 0 &&
           (!std::isfinite(item.centerX) || !std::isfinite(item.centerY) ||
            !std::isfinite(item.centerZ) ||
            !IsFiniteNonNegative(item.extentsX) ||
            !IsFiniteNonNegative(item.extentsY) ||
            !IsFiniteNonNegative(item.extentsZ))) ||
          !stagedBounds.emplace(item.effectId, item).second)
        return ANITY_ERR_INVALID_ARG;
      effectIds.push_back(item.effectId);
    }

    AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
    if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
    std::lock_guard<std::mutex> lock(registry->mutex);
    if (playerLoopToken != registry->playerLoopToken ||
        registry->frameIndex != registry->playerLoopFrameIndex ||
        registry->cullingFrameOpen ||
        playerLoopToken <= registry->cullingFrameToken)
      return ANITY_ERR_INVALID_ARG;

    auto stagedStates = registry->cullingStates;
    uint64_t stagedGeneration = registry->generation;
    for (uint64_t effectId : effectIds) {
      AnityGraphicsVFXCullingStorage& storage = stagedStates[effectId];
      storage.bounds = stagedBounds.at(effectId);
      storage.visible = false;
      storage.state.effectId = effectId;
      storage.state.playerLoopToken = playerLoopToken;
      storage.state.hasBounds = storage.bounds.valid;
      storage.state.cameraCount = 0;
      storage.state.visibleCameraCount = 0;
      if (storage.bounds.valid == 0) storage.state.culled = 0;
      storage.state.generation = ++stagedGeneration;
    }
    registry->cullingStates.swap(stagedStates);
    registry->generation = stagedGeneration;
    registry->cullingEffectIds.swap(effectIds);
    registry->cullingCameraIds.clear();
    registry->cullingFrameToken = playerLoopToken;
    registry->cullingFrameOpen = true;
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

AnityResult ANITY_CALL AnityGraphics_SubmitVFXCullingCamera(
    AnityGraphicsDevice* device, uint64_t playerLoopToken,
    const AnityGraphicsVFXCullingCamera* camera) {
  if (!device || playerLoopToken == 0 || !camera || camera->cameraId == 0 ||
      camera->cameraType < 0 || !IsFiniteMatrix(*camera))
    return ANITY_ERR_INVALID_ARG;
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  try {
    std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
    AnityGraphicsVFXEventRegistry& registry = *device->vfxEvents;
    if (!registry.cullingFrameOpen ||
        registry.cullingFrameToken != playerLoopToken ||
        static_cast<int32_t>(registry.cullingCameraIds.size()) >= kMaxCullingCameras ||
        !registry.cullingCameraIds.emplace(camera->cameraId, true).second)
      return ANITY_ERR_INVALID_ARG;
    for (uint64_t effectId : registry.cullingEffectIds) {
      auto found = registry.cullingStates.find(effectId);
      if (found == registry.cullingStates.end()) return ANITY_ERR_INVALID_ARG;
      AnityGraphicsVFXCullingStorage& storage = found->second;
      ++storage.state.cameraCount;
      if (storage.bounds.valid == 0) continue;
      const uint32_t mask = static_cast<uint32_t>(camera->cullingMask);
      const uint32_t layerBit = uint32_t{1} << storage.bounds.layer;
      if ((mask & layerBit) == 0) continue;
      if (IsVisibleInClipSpace(storage.bounds, *camera)) {
        ++storage.state.visibleCameraCount;
        storage.visible = true;
      }
    }
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

AnityResult ANITY_CALL AnityGraphics_CompleteVFXCullingFrame(
    AnityGraphicsDevice* device, uint64_t playerLoopToken) {
  if (!device || playerLoopToken == 0 || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  AnityGraphicsVFXEventRegistry& registry = *device->vfxEvents;
  if (!registry.cullingFrameOpen || registry.cullingFrameToken != playerLoopToken)
    return ANITY_ERR_INVALID_ARG;
  for (uint64_t effectId : registry.cullingEffectIds) {
    auto found = registry.cullingStates.find(effectId);
    if (found == registry.cullingStates.end()) return ANITY_ERR_INVALID_ARG;
    AnityGraphicsVFXCullingStorage& storage = found->second;
    storage.state.culled = storage.bounds.valid != 0 &&
        storage.state.cameraCount > 0 && !storage.visible ? 1 : 0;
    storage.state.generation = ++registry.generation;
  }
  registry.cullingFrameOpen = false;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_GetVFXCullingState(
    const AnityGraphicsDevice* device, uint64_t effectId,
    AnityGraphicsVFXCullingState* outState) {
  if (!device || effectId == 0 || !outState) return ANITY_ERR_INVALID_ARG;
  *outState = {};
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->cullingStates.find(effectId);
  if (found == device->vfxEvents->cullingStates.end())
    return ANITY_ERR_INVALID_ARG;
  *outState = found->second.state;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_SetVFXPlanarOutputs(
    AnityGraphicsDevice* device, uint64_t effectId,
    const AnityGraphicsVFXPlanarOutputDesc* outputs, int32_t outputCount) {
  constexpr int32_t kMaxPlanarOutputsPerEffect = 4096;
  if (!device || effectId == 0 || outputCount < 0 ||
      outputCount > kMaxPlanarOutputsPerEffect ||
      (outputCount > 0 && !outputs))
    return ANITY_ERR_INVALID_ARG;
  std::vector<AnityGraphicsVFXPlanarOutputDesc> replacement;
  try {
    replacement.reserve(static_cast<size_t>(outputCount));
    std::unordered_map<int64_t, bool> contextIds;
    struct PlanarParticleLayout {
      int32_t capacity;
      int32_t stride;
      int32_t aliveOffset;
      int32_t positionOffset;
    };
    std::unordered_map<int32_t, PlanarParticleLayout> particleLayouts;
    contextIds.reserve(static_cast<size_t>(outputCount));
    particleLayouts.reserve(static_cast<size_t>(outputCount));
    for (int32_t index = 0; index < outputCount; ++index) {
      if (!IsValidPlanarOutput(outputs[index], effectId) ||
          !contextIds.emplace(outputs[index].contextId, true).second)
        return ANITY_ERR_INVALID_ARG;
      const PlanarParticleLayout layout{
          outputs[index].particleCapacity,
          outputs[index].attributeStrideBytes,
          outputs[index].aliveOffsetBytes,
          outputs[index].positionOffsetBytes};
      auto inserted = particleLayouts.emplace(
          outputs[index].particleSystemId, layout);
      if (!inserted.second &&
          (inserted.first->second.capacity != layout.capacity ||
           inserted.first->second.stride != layout.stride ||
           inserted.first->second.aliveOffset != layout.aliveOffset ||
           inserted.first->second.positionOffset != layout.positionOffset))
        return ANITY_ERR_INVALID_ARG;
      replacement.push_back(outputs[index]);
    }
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  try {
    std::lock_guard<std::mutex> lock(registry->mutex);
    registry->planarOutputs[effectId] = std::move(replacement);
    ++registry->generation;
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

AnityResult ANITY_CALL AnityGraphics_GetVFXPlanarOutputCount(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t* outCount) {
  if (!device || effectId == 0 || !outCount || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  *outCount = 0;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->planarOutputs.find(effectId);
  if (found == device->vfxEvents->planarOutputs.end())
    return ANITY_ERR_INVALID_ARG;
  if (found->second.size() >
      static_cast<size_t>(std::numeric_limits<int32_t>::max()))
    return ANITY_ERR_INTERNAL;
  *outCount = static_cast<int32_t>(found->second.size());
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_DrawVFXPlanarOutputs(
    AnityGraphicsDevice* device, uint64_t effectId,
    const AnityGraphicsVFXPlanarCameraDesc* camera,
    AnityGraphicsVFXPlanarDrawInfo* outInfo) {
  if (!device || effectId == 0 || !camera || !outInfo ||
      !IsFinitePlanarCamera(*camera) || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  *outInfo = {};
  outInfo->effectId = effectId;
  outInfo->cameraId = camera->cameraId;
  AnityGraphicsVFXPlanarCameraBatchDesc batch{};
  batch.cameraId = camera->cameraId;
  std::memcpy(batch.worldToClip, camera->worldToClip,
              sizeof(batch.worldToClip));
  batch.cullingMask = -1; /* Preserve the legacy entry point's no-layer contract. */
  batch.cameraType = camera->cameraType;
  batch.flags = camera->flags;
  AnityGraphicsVFXPlanarEffectDesc effect{};
  effect.effectId = effectId;
  std::memcpy(effect.localToWorld, camera->localToWorld,
              sizeof(effect.localToWorld));
  AnityGraphicsVFXPlanarCameraDrawInfo batchInfo{};
  const AnityResult result = AnityGraphics_DrawVFXPlanarCamera(
      device, &batch, &effect, 1, &batchInfo);
  if (result != ANITY_OK) return result;
  outInfo->residentGeneration = batchInfo.residentGeneration;
  outInfo->outputCount = batchInfo.outputCount;
  outInfo->drawCount = batchInfo.drawCount;
  outInfo->skippedOutputCount = batchInfo.skippedOutputCount;
  outInfo->particleCount = batchInfo.particleCount;
  outInfo->vertexCount = batchInfo.vertexCount;
  outInfo->backendKind = batchInfo.backendKind;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_DrawVFXPlanarCamera(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXPlanarCameraBatchDesc* camera,
    const AnityGraphicsVFXPlanarEffectDesc* effects, int32_t effectCount,
    AnityGraphicsVFXPlanarCameraDrawInfo* outInfo) {
  constexpr int32_t kMaxPlanarEffectsPerCamera = 4096;
  if (!device || !camera || !outInfo || effectCount < 0 ||
      effectCount > kMaxPlanarEffectsPerCamera ||
      (effectCount > 0 && !effects) || !IsFinitePlanarCameraBatch(*camera))
    return ANITY_ERR_INVALID_ARG;
  *outInfo = {};
  outInfo->cameraId = camera->cameraId;
  std::unordered_map<uint64_t, bool> effectIds;
  try {
    effectIds.reserve(static_cast<size_t>(effectCount));
    for (int32_t index = 0; index < effectCount; ++index) {
      if (!IsFinitePlanarEffect(effects[index]) ||
          !effectIds.emplace(effects[index].effectId, true).second)
        return ANITY_ERR_INVALID_ARG;
    }
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }

  std::vector<AnityGraphicsVFXPlanarDrawPacket> packets;
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  try {
    std::lock_guard<std::mutex> lock(registry->mutex);
    /* A first Initialize has no resident allocation that Camera can bind before
     * CPU retirement. Retire only those bootstrap tickets here. Once a system
     * is resident, Metal publishes its target ring allocation at Begin and the
     * shared command queue orders Camera behind Initialize without a CPU wait. */
    if (device->type == ANITY_GFX_METAL) {
      std::vector<uint64_t> bootstrapTickets;
      bootstrapTickets.reserve(static_cast<size_t>(effectCount));
      for (int32_t effectIndex = 0; effectIndex < effectCount; ++effectIndex) {
        const uint64_t effectId = effects[effectIndex].effectId;
        auto owner = registry->pendingInitializeByEffect.find(effectId);
        if (owner == registry->pendingInitializeByEffect.end()) continue;
        auto initialize = registry->pendingInitializes.find(owner->second);
        if (initialize == registry->pendingInitializes.end())
          return ANITY_ERR_INTERNAL;
        const auto& pending = *initialize->second;
        bool requiresBootstrap = false;
        for (size_t index = 0; index < pending.keys.size(); ++index) {
          if (pending.keys[index].effectId == effectId &&
              pending.sourceGenerations[index] == 0 &&
              pending.targetGenerations[index] != 0) {
            requiresBootstrap = true;
            break;
          }
        }
        if (requiresBootstrap &&
            std::find(bootstrapTickets.begin(), bootstrapTickets.end(),
                      owner->second) == bootstrapTickets.end())
          bootstrapTickets.push_back(owner->second);
      }
      for (uint64_t ticketId : bootstrapTickets) {
        const AnityResult completed = CompleteVFXInitializeKernelsLocked(
            device, registry, ticketId);
        if (completed != ANITY_OK) return completed;
      }
    }
    outInfo->submissionGeneration = registry->generation;
    for (int32_t effectIndex = 0; effectIndex < effectCount; ++effectIndex) {
      const AnityGraphicsVFXPlanarEffectDesc& effect = effects[effectIndex];
      auto found = registry->planarOutputs.find(effect.effectId);
      if (found == registry->planarOutputs.end())
        return ANITY_ERR_INVALID_ARG;
      if ((static_cast<uint32_t>(camera->cullingMask) &
           (uint32_t{1} << effect.layer)) == 0) continue;
      ++outInfo->effectCount;
      for (const AnityGraphicsVFXPlanarOutputDesc& output : found->second) {
        AnityGraphicsVFXPlanarDrawPacket packet{};
        packet.output = output;
        std::memcpy(packet.localToWorld, effect.localToWorld,
                    sizeof(packet.localToWorld));
        packet.effectSortOrder = effect.sortOrder;
        const auto particle = registry->particleSystems.find(
            AnityGraphicsVFXParticleSystemKey{
                effect.effectId, output.particleSystemId});
        if (particle != registry->particleSystems.end()) {
          const auto& storage = *particle->second;
          if (storage.info.capacity != output.particleCapacity ||
              storage.info.attributeStrideBytes != output.attributeStrideBytes)
            return ANITY_ERR_INVALID_ARG;
          packet.generation = storage.info.generation;
          packet.aliveCount = storage.info.aliveCount;
        }
        auto updateQueue = registry->pendingUpdateByEffect.find(
            effect.effectId);
        if (updateQueue != registry->pendingUpdateByEffect.end()) {
          const AnityGraphicsVFXParticleSystemKey key{
              effect.effectId, output.particleSystemId};
          for (auto ticket = updateQueue->second.rbegin();
               ticket != updateQueue->second.rend(); ++ticket) {
            auto update = registry->pendingUpdates.find(*ticket);
            if (update == registry->pendingUpdates.end())
              return ANITY_ERR_INTERNAL;
            const auto& pendingUpdate = *update->second;
            if (!pendingUpdate.residentPublished) continue;
            auto keyIndex = std::find(
                pendingUpdate.keys.begin(), pendingUpdate.keys.end(), key);
            if (keyIndex == pendingUpdate.keys.end()) continue;
            const size_t updateIndex = static_cast<size_t>(
                std::distance(pendingUpdate.keys.begin(), keyIndex));
            packet.generation =
                pendingUpdate.targetGenerations[updateIndex];
            if (pendingUpdate.initializeDependencyTicketId != 0)
              packet.pendingInitialize = 1;
            break;
          }
        }
        auto initializeOwner = registry->pendingInitializeByEffect.find(
            effect.effectId);
        if (initializeOwner != registry->pendingInitializeByEffect.end()) {
          auto initialize = registry->pendingInitializes.find(
              initializeOwner->second);
          if (initialize == registry->pendingInitializes.end())
            return ANITY_ERR_INTERNAL;
          const auto& pending = *initialize->second;
          const AnityGraphicsVFXParticleSystemKey key{
              effect.effectId, output.particleSystemId};
          for (size_t reverse = pending.keys.size(); reverse > 0; --reverse) {
            const size_t pendingIndex = reverse - 1u;
            if (!(pending.keys[pendingIndex] == key) ||
                pending.sourceGenerations[pendingIndex] == 0 ||
                pending.targetGenerations[pendingIndex] == 0 ||
                (!pending.backendHandles[pendingIndex] &&
                 pending.backendCompleted[pendingIndex] == 0))
              continue;
            if (packet.generation < pending.targetGenerations[pendingIndex])
              packet.generation = pending.targetGenerations[pendingIndex];
            packet.pendingInitialize = 1;
            break;
          }
        }
        packets.push_back(packet);
      }
    }
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  if (packets.size() > static_cast<size_t>(std::numeric_limits<int32_t>::max()))
    return ANITY_ERR_OUT_OF_MEMORY;
  std::stable_sort(
      packets.begin(), packets.end(),
      [](const AnityGraphicsVFXPlanarDrawPacket& left,
         const AnityGraphicsVFXPlanarDrawPacket& right) {
        if (left.output.renderQueue != right.output.renderQueue)
          return left.output.renderQueue < right.output.renderQueue;
        if (left.effectSortOrder != right.effectSortOrder)
          return left.effectSortOrder < right.effectSortOrder;
        if (left.output.effectId != right.output.effectId)
          return left.output.effectId < right.output.effectId;
        return left.output.contextId < right.output.contextId;
      });
  outInfo->outputCount = static_cast<int32_t>(packets.size());
  if (device->type != ANITY_GFX_METAL) {
    outInfo->skippedOutputCount = outInfo->outputCount;
    return ANITY_OK;
  }
  return AnityGraphics_Metal_DrawVFXPlanarCamera(
      device, camera, packets.empty() ? nullptr : packets.data(),
      static_cast<int32_t>(packets.size()), outInfo);
}

AnityResult ANITY_CALL AnityGraphics_GetVFXPlanarSubmissionStats(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXPlanarSubmissionStats* outStats) {
  if (!device || !outStats) return ANITY_ERR_INVALID_ARG;
  *outStats = {};
  if (device->type != ANITY_GFX_METAL || !device->backend) return ANITY_OK;
  return AnityGraphics_Metal_GetVFXPlanarSubmissionStats(device, outStats);
}

AnityResult ANITY_CALL AnityGraphics_WaitForVFXPlanarSubmissions(
    AnityGraphicsDevice* device, uint64_t throughSubmissionId,
    int32_t timeoutMilliseconds) {
  if (!device || timeoutMilliseconds < -1) return ANITY_ERR_INVALID_ARG;
  if (device->type != ANITY_GFX_METAL || !device->backend)
    return throughSubmissionId == 0 ? ANITY_OK : ANITY_ERR_INVALID_ARG;
  return AnityGraphics_Metal_WaitForVFXPlanarSubmissions(
      device, throughSubmissionId, timeoutMilliseconds);
}

AnityResult ANITY_CALL AnityGraphics_PrepareVFXEffectFrame(
    AnityGraphicsDevice* device, uint64_t effectId, uint32_t frameIndex,
    float gameDeltaTime, float playRate, float fixedTimeStep,
    float maxDeltaTime, int32_t paused,
    AnityGraphicsVFXFrameState* outState) {
  if (!device || effectId == 0 || !outState ||
      !IsFiniteNonNegative(gameDeltaTime) ||
      !IsFiniteNonNegative(playRate) ||
      !std::isfinite(fixedTimeStep) || fixedTimeStep <= 0.0f ||
      !std::isfinite(maxDeltaTime) || maxDeltaTime <= 0.0f ||
      (paused != 0 && paused != 1))
    return ANITY_ERR_INVALID_ARG;
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  std::lock_guard<std::mutex> lock(registry->mutex);
  if (frameIndex != registry->frameIndex) return ANITY_ERR_INVALID_ARG;
  AnityResult finalized = device->type == ANITY_GFX_METAL
      ? PollCommittedVFXPendingUpdateForPreparationLocked(
          device, registry, effectId, nullptr)
      : FinalizeCommittedVFXPendingUpdateLocked(
          device, registry, effectId, true, nullptr);
  if (finalized != ANITY_OK) return finalized;

  try {
    auto existing = registry->frameStates.find(effectId);
    if (existing != registry->frameStates.end() &&
        existing->second.state.prepared != 0)
      return ANITY_ERR_INVALID_ARG;
    float accumulator = existing == registry->frameStates.end()
        ? 0.0f : existing->second.state.accumulator;
    uint32_t stepCount = 0;
    float unscaledDeltaTime = 0.0f;
    float deltaTime = 0.0f;
    if (paused == 0) {
      const double accumulated =
          static_cast<double>(accumulator) + static_cast<double>(gameDeltaTime);
      if (!std::isfinite(accumulated) ||
          accumulated > static_cast<double>(std::numeric_limits<float>::max()))
        return ANITY_ERR_INVALID_ARG;
      accumulator = static_cast<float>(accumulated);
      const double maximumRatio =
          static_cast<double>(maxDeltaTime / fixedTimeStep);
      int64_t maximumSteps = maximumRatio >=
              static_cast<double>(std::numeric_limits<uint32_t>::max())
          ? static_cast<int64_t>(std::numeric_limits<uint32_t>::max())
          : RoundToNearestEvenPositive(maximumRatio);
      maximumSteps = std::max<int64_t>(1, maximumSteps);
      const double availableRatio =
          static_cast<double>(accumulator / fixedTimeStep);
      const int64_t availableSteps = availableRatio >=
              static_cast<double>(maximumSteps)
          ? maximumSteps
          : static_cast<int64_t>(std::floor(availableRatio));
      stepCount = static_cast<uint32_t>(std::max<int64_t>(0, availableSteps));
      const double consumed =
          static_cast<double>(stepCount) * static_cast<double>(fixedTimeStep);
      unscaledDeltaTime = static_cast<float>(consumed);
      accumulator = static_cast<float>(std::max(0.0,
          static_cast<double>(accumulator) - consumed));
      const double scaled = consumed * static_cast<double>(playRate);
      if (!std::isfinite(scaled) ||
          scaled > static_cast<double>(std::numeric_limits<float>::max()))
        return ANITY_ERR_INVALID_ARG;
      deltaTime = static_cast<float>(scaled);
    }

    AnityGraphicsVFXFrameStorage rollbackJournal;
    CaptureSpawnerRollback(*registry, effectId, &rollbackJournal);
    CaptureEffectDataRollback(*registry, effectId, &rollbackJournal);
    auto [found, inserted] = registry->frameStates.try_emplace(effectId);
    AnityGraphicsVFXFrameStorage& storage = found->second;
    storage.hadRollbackState = !inserted;
    if (!inserted) storage.rollbackState = storage.state;
    InstallRollbackJournal(&storage, &rollbackJournal);
    storage.allowNonFiniteCommit = !inserted &&
        !std::isfinite(storage.state.totalTime);
    AnityGraphicsVFXFrameState& state = storage.state;
    if (inserted) state.effectId = effectId;
    state.frameIndex = frameIndex;
    state.stepCount = stepCount;
    state.gameDeltaTime = gameDeltaTime;
    state.unscaledDeltaTime = unscaledDeltaTime;
    state.deltaTime = deltaTime;
    state.accumulator = accumulator;
    state.prepared = 1;
    state.generation = ++registry->generation;
    *outState = state;
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  } catch (...) {
    return ANITY_ERR_INTERNAL;
  }
}

AnityResult ANITY_CALL AnityGraphics_PrepareVFXEffectManualFrame(
    AnityGraphicsDevice* device, uint64_t effectId, uint32_t frameIndex,
    float stepDeltaTime, AnityGraphicsVFXFrameState* outState) {
  if (!device || effectId == 0 || !outState) return ANITY_ERR_INVALID_ARG;
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  std::lock_guard<std::mutex> lock(registry->mutex);
  if (frameIndex != registry->frameIndex) return ANITY_ERR_INVALID_ARG;
  AnityResult finalized = device->type == ANITY_GFX_METAL
      ? PollCommittedVFXPendingUpdateForPreparationLocked(
          device, registry, effectId, nullptr)
      : FinalizeCommittedVFXPendingUpdateLocked(
          device, registry, effectId, true, nullptr);
  if (finalized != ANITY_OK) return finalized;
  try {
    auto existing = registry->frameStates.find(effectId);
    if (existing != registry->frameStates.end() &&
        existing->second.state.prepared != 0)
      return ANITY_ERR_INVALID_ARG;
    AnityGraphicsVFXFrameStorage rollbackJournal;
    CaptureSpawnerRollback(*registry, effectId, &rollbackJournal);
    CaptureEffectDataRollback(*registry, effectId, &rollbackJournal);
    auto [found, inserted] = registry->frameStates.try_emplace(effectId);
    AnityGraphicsVFXFrameStorage& storage = found->second;
    storage.hadRollbackState = !inserted;
    if (!inserted) storage.rollbackState = storage.state;
    InstallRollbackJournal(&storage, &rollbackJournal);
    storage.allowNonFiniteCommit = true;
    AnityGraphicsVFXFrameState& state = storage.state;
    if (inserted) state.effectId = effectId;
    state.frameIndex = frameIndex;
    state.stepCount = 1;
    state.gameDeltaTime = stepDeltaTime;
    state.unscaledDeltaTime = stepDeltaTime;
    state.deltaTime = stepDeltaTime;
    state.prepared = 1;
    state.generation = ++registry->generation;
    *outState = state;
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  } catch (...) {
    return ANITY_ERR_INTERNAL;
  }
}

AnityResult ANITY_CALL AnityGraphics_CommitVFXEffectFrame(
    AnityGraphicsDevice* device, uint64_t effectId, uint32_t frameIndex,
    AnityGraphicsVFXFrameState* outState) {
  if (!device || effectId == 0 || !outState || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->frameStates.find(effectId);
  if (found == device->vfxEvents->frameStates.end() ||
      found->second.state.prepared == 0 ||
      found->second.state.frameIndex != frameIndex)
    return ANITY_ERR_INVALID_ARG;
  auto pending = device->vfxEvents->pendingUpdateByEffect.find(effectId);
  bool asynchronousUpdatePublished = false;
  uint64_t frameTicketId = 0;
  if (pending != device->vfxEvents->pendingUpdateByEffect.end()) {
    for (auto ticket = pending->second.rbegin();
         ticket != pending->second.rend(); ++ticket) {
      auto candidate = device->vfxEvents->pendingUpdates.find(*ticket);
      if (candidate == device->vfxEvents->pendingUpdates.end())
        return ANITY_ERR_INTERNAL;
      if (candidate->second->frameCommitted) continue;
      if (frameTicketId != 0 ||
          candidate->second->info.preparedFrameGeneration !=
              found->second.state.generation ||
          candidate->second->info.frameIndex != frameIndex)
        return ANITY_ERR_INVALID_ARG;
      frameTicketId = *ticket;
    }
  }
  if (frameTicketId != 0) {
    AnityResult updateResult = device->type == ANITY_GFX_METAL
        ? PublishVFXPendingUpdateLocked(
            device, device->vfxEvents, frameTicketId)
        : CompleteVFXPendingUpdateLocked(
            device, device->vfxEvents, frameTicketId);
    if (updateResult != ANITY_OK) return updateResult;
    if (device->type == ANITY_GFX_METAL) {
      auto published = device->vfxEvents->pendingUpdates.find(
          frameTicketId);
      if (published == device->vfxEvents->pendingUpdates.end())
        return ANITY_ERR_INTERNAL;
      published->second->frameCommitted = true;
      asynchronousUpdatePublished = true;
    }
  }
  AnityGraphicsVFXFrameState& state = found->second.state;
  const double total = static_cast<double>(state.totalTime) +
      static_cast<double>(state.deltaTime);
  if (!found->second.allowNonFiniteCommit &&
      (!std::isfinite(total) ||
       total > static_cast<double>(std::numeric_limits<float>::max())))
    return ANITY_ERR_INVALID_ARG;
  state.totalTime = static_cast<float>(total);
  state.prepared = 0;
  state.generation = ++device->vfxEvents->generation;
  if (device->type == ANITY_GFX_METAL && !asynchronousUpdatePublished &&
      device->vfxEvents->pendingUpdateByEffect.find(effectId) ==
          device->vfxEvents->pendingUpdateByEffect.end())
    AnityGraphics_Metal_DiscardVFXResidentSnapshots(device, effectId);
  ClearEffectDataRollback(&found->second);
  *outState = state;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_AbortVFXEffectFrame(
    AnityGraphicsDevice* device, uint64_t effectId, uint32_t frameIndex) {
  if (!device || effectId == 0 || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->frameStates.find(effectId);
  if (found == device->vfxEvents->frameStates.end() ||
      found->second.state.prepared == 0 ||
      found->second.state.frameIndex != frameIndex)
    return ANITY_ERR_INVALID_ARG;
  try {
    bool hasCommittedPendingBaseline = false;
    auto pending = device->vfxEvents->pendingUpdateByEffect.find(effectId);
    if (pending != device->vfxEvents->pendingUpdateByEffect.end()) {
      uint64_t uncommittedTicketId = 0;
      for (uint64_t ticketId : pending->second) {
        auto update = device->vfxEvents->pendingUpdates.find(ticketId);
        if (update == device->vfxEvents->pendingUpdates.end())
          return ANITY_ERR_INTERNAL;
        if (update->second->frameCommitted) {
          hasCommittedPendingBaseline = true;
        } else {
          if (uncommittedTicketId != 0) return ANITY_ERR_INTERNAL;
          uncommittedTicketId = ticketId;
        }
      }
      if (uncommittedTicketId != 0) {
        AnityResult cancelResult = CancelVFXPendingUpdateLocked(
            device, device->vfxEvents, uncommittedTicketId);
        if (cancelResult != ANITY_OK) return cancelResult;
      }
    }
    if (!hasCommittedPendingBaseline) {
      AnityResult residentRollback = RestoreVFXResidentRollbackLocked(
          device, effectId, found->second);
      if (residentRollback != ANITY_OK) return residentRollback;
    }
    if (!found->second.hadRollbackState) {
      RestoreSpawnerRollback(device->vfxEvents, effectId, found->second);
      RestoreEffectDataRollback(device->vfxEvents, effectId, found->second);
      device->vfxEvents->frameStates.erase(found);
      ++device->vfxEvents->generation;
      return ANITY_OK;
    }
    RestoreSpawnerRollback(device->vfxEvents, effectId, found->second);
    RestoreEffectDataRollback(device->vfxEvents, effectId, found->second);
    found->second.state = found->second.rollbackState;
    found->second.state.prepared = 0;
    found->second.state.generation = ++device->vfxEvents->generation;
    ClearEffectDataRollback(&found->second);
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  } catch (...) {
    return ANITY_ERR_INTERNAL;
  }
}

AnityResult ANITY_CALL AnityGraphics_GetVFXEffectFrameState(
    const AnityGraphicsDevice* device, uint64_t effectId,
    AnityGraphicsVFXFrameState* outState) {
  if (!device || effectId == 0 || !outState || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->frameStates.find(effectId);
  if (found == device->vfxEvents->frameStates.end()) return ANITY_ERR_INVALID_ARG;
  *outState = found->second.state;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_ResetVFXEffectFrameState(
    AnityGraphicsDevice* device, uint64_t effectId) {
  if (!device || effectId == 0) return ANITY_ERR_INVALID_ARG;
  if (!device->vfxEvents) return ANITY_OK;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto pending = device->vfxEvents->pendingUpdateByEffect.find(effectId);
  if (pending != device->vfxEvents->pendingUpdateByEffect.end()) {
    AnityResult cancelResult = CancelVFXPendingUpdatesForEffectLocked(
        device, device->vfxEvents, effectId);
    if (cancelResult != ANITY_OK) return cancelResult;
  }
  AnityResult initializeCancel = CancelPendingInitializeForEffectLocked(
      device, device->vfxEvents, effectId);
  if (initializeCancel != ANITY_OK) return initializeCancel;
  device->vfxEvents->frameStates.erase(effectId);
  if (device->type == ANITY_GFX_METAL)
    AnityGraphics_Metal_DiscardVFXResidentSnapshots(device, effectId);
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_SetVFXSpawnerPrograms(
    AnityGraphicsDevice* device, uint64_t effectId,
    const AnityGraphicsVFXSpawnerProgramDesc* programs, int32_t programCount,
    const AnityGraphicsVFXSpawnerBlockDesc* blocks, int32_t blockCount) {
  if (!device || effectId == 0 || programCount < 0 ||
      programCount > kMaxSpawnerProgramsPerEffect || blockCount < 0 ||
      blockCount > kMaxSpawnerBlocksPerEffect ||
      (programCount > 0 && !programs) || (blockCount > 0 && !blocks))
    return ANITY_ERR_INVALID_ARG;
  std::unordered_map<AnityGraphicsVFXSpawnerKey,
      std::shared_ptr<AnityGraphicsVFXSpawnerStorage>,
      AnityGraphicsVFXSpawnerKeyHash> replacements;
  try {
    replacements.reserve(static_cast<size_t>(programCount));
    for (int32_t index = 0; index < blockCount; ++index)
      if (!IsValidSpawnerBlock(blocks[index])) return ANITY_ERR_INVALID_ARG;
    for (int32_t index = 0; index < programCount; ++index) {
      const auto& program = programs[index];
      if (!IsValidSpawnerProgram(program, effectId, blockCount))
        return ANITY_ERR_INVALID_ARG;
      auto storage = std::make_shared<AnityGraphicsVFXSpawnerStorage>();
      storage->desc = program;
      storage->state.effectId = effectId;
      storage->state.contextId = program.contextId;
      storage->state.systemId = program.systemId;
      storage->state.loopState = kSpawnerLoopFinished;
      storage->state.loopCount = 0;
      storage->state.loopDuration = 0.0f;
      if (program.eventStrideWords > 0)
      {
        storage->eventRecord.resize(program.eventStrideWords, 0u);
        storage->eventRecordDefaults.resize(program.eventStrideWords, 0u);
      }
      storage->blocks.reserve(static_cast<size_t>(program.blockCount));
      for (int32_t blockIndex = 0; blockIndex < program.blockCount; ++blockIndex) {
        AnityGraphicsVFXSpawnerBlockStorage block{};
        block.desc = blocks[program.blockStart + blockIndex];
        if (block.desc.kind == 5 && program.version < 5)
          return ANITY_ERR_INVALID_ARG;
        if (block.desc.kind == 4 &&
            (program.eventStrideWords == 0 ||
             block.desc.targetOffsetWords > static_cast<int32_t>(program.eventStrideWords) -
                 block.desc.valueWordCount))
          return ANITY_ERR_INVALID_ARG;
        storage->blocks.push_back(block);
      }
      AnityGraphicsVFXSpawnerKey key{effectId, program.contextId};
      if (!replacements.emplace(key, std::move(storage)).second)
        return ANITY_ERR_INVALID_ARG;
    }
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  } catch (...) {
    return ANITY_ERR_INTERNAL;
  }
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  std::lock_guard<std::mutex> lock(registry->mutex);
  for (auto it = registry->spawners.begin(); it != registry->spawners.end();) {
    if (it->first.effectId == effectId)
      it = registry->spawners.erase(it);
    else
      ++it;
  }
  const uint64_t generation = ++registry->generation;
  for (auto& item : replacements) {
    item.second->state.generation = generation;
    registry->spawners.emplace(item.first, std::move(item.second));
  }
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_SetVFXSpawnerEventRecordDefaults(
    AnityGraphicsDevice* device, uint64_t effectId, int64_t contextId,
    const uint8_t* record, int32_t recordByteCount) {
  if (!device || effectId == 0 || contextId == 0 || recordByteCount < 0 ||
      (recordByteCount > 0 && !record) || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->spawners.find(
      AnityGraphicsVFXSpawnerKey{effectId, contextId});
  if (found == device->vfxEvents->spawners.end()) return ANITY_ERR_INVALID_ARG;
  const int32_t expected = static_cast<int32_t>(
      found->second->eventRecord.size() * sizeof(uint32_t));
  if (recordByteCount != expected) return ANITY_ERR_INVALID_ARG;
  if (expected > 0) {
    std::memcpy(found->second->eventRecord.data(), record,
                static_cast<size_t>(expected));
    found->second->eventRecordDefaults = found->second->eventRecord;
  }
  found->second->state.generation = ++device->vfxEvents->generation;
  return ANITY_OK;
}

AnityResult ControlVFXSpawnerInternal(
    AnityGraphicsDevice* device, uint64_t effectId, int64_t contextId,
    int32_t play, uint32_t seed, int32_t resetSeed,
    AnityGraphicsVFXSpawnerCallback callback, void* userData) {
  if (!device || effectId == 0 || contextId == 0 ||
      (play != 0 && play != 1) || (resetSeed != 0 && resetSeed != 1) ||
      !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->spawners.find(
      AnityGraphicsVFXSpawnerKey{effectId, contextId});
  if (found == device->vfxEvents->spawners.end()) return ANITY_ERR_INVALID_ARG;
  if (HasSpawnerCallback(*found->second) && !callback)
    return ANITY_ERR_INVALID_ARG;
  const AnityResult result = play != 0
      ? StartSpawner(*found->second, seed, resetSeed != 0, callback, userData)
      : StopSpawner(*found->second, callback, userData);
  if (result != ANITY_OK) return result;
  found->second->state.generation = ++device->vfxEvents->generation;
  return ANITY_OK;
}

AnityResult TickVFXSpawnersInternal(
    AnityGraphicsDevice* device, uint64_t effectId, float deltaTime,
    AnityGraphicsVFXSpawnerState* states, int32_t stateCapacity,
    int32_t* outWritten, AnityGraphicsVFXSpawnerCallback callback,
    void* userData) {
  if (!device || effectId == 0 || stateCapacity < 0 || !outWritten ||
      !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  *outWritten = 0;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  int32_t required = 0;
  for (const auto& item : device->vfxEvents->spawners)
    if (item.first.effectId == effectId) ++required;
  if (stateCapacity < required || (required > 0 && !states))
    return ANITY_ERR_INVALID_ARG;
  const uint64_t generation = ++device->vfxEvents->generation;
  int32_t written = 0;
  for (auto& item : device->vfxEvents->spawners) {
    if (item.first.effectId != effectId) continue;
    if (HasSpawnerCallback(*item.second) && !callback)
      return ANITY_ERR_INVALID_ARG;
    const AnityResult result = TickSpawner(
        *item.second, deltaTime, callback, userData);
    if (result != ANITY_OK) return result;
    item.second->state.generation = generation;
    states[written++] = item.second->state;
  }
  *outWritten = written;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_ControlVFXSpawner(
    AnityGraphicsDevice* device, uint64_t effectId, int64_t contextId,
    int32_t play, uint32_t seed, int32_t resetSeed) {
  return ControlVFXSpawnerInternal(
      device, effectId, contextId, play, seed, resetSeed, nullptr, nullptr);
}

AnityResult ANITY_CALL AnityGraphics_ControlVFXSpawnerWithCallbacks(
    AnityGraphicsDevice* device, uint64_t effectId, int64_t contextId,
    int32_t play, uint32_t seed, int32_t resetSeed,
    AnityGraphicsVFXSpawnerCallback callback, void* userData) {
  if (!callback) return ANITY_ERR_INVALID_ARG;
  return ControlVFXSpawnerInternal(
      device, effectId, contextId, play, seed, resetSeed, callback, userData);
}

AnityResult ANITY_CALL AnityGraphics_TickVFXSpawners(
    AnityGraphicsDevice* device, uint64_t effectId, float deltaTime,
    AnityGraphicsVFXSpawnerState* states, int32_t stateCapacity,
    int32_t* outWritten) {
  return TickVFXSpawnersInternal(
      device, effectId, deltaTime, states, stateCapacity, outWritten,
      nullptr, nullptr);
}

AnityResult ANITY_CALL AnityGraphics_TickVFXSpawnersWithCallbacks(
    AnityGraphicsDevice* device, uint64_t effectId, float deltaTime,
    AnityGraphicsVFXSpawnerState* states, int32_t stateCapacity,
    int32_t* outWritten, AnityGraphicsVFXSpawnerCallback callback,
    void* userData) {
  if (!callback) return ANITY_ERR_INVALID_ARG;
  return TickVFXSpawnersInternal(
      device, effectId, deltaTime, states, stateCapacity, outWritten,
      callback, userData);
}

AnityResult ANITY_CALL AnityGraphics_GetVFXSpawnerState(
    const AnityGraphicsDevice* device, uint64_t effectId, int64_t contextId,
    AnityGraphicsVFXSpawnerState* outState) {
  if (!device || effectId == 0 || contextId == 0 || !outState ||
      !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->spawners.find(
      AnityGraphicsVFXSpawnerKey{effectId, contextId});
  if (found == device->vfxEvents->spawners.end()) return ANITY_ERR_INVALID_ARG;
  *outState = found->second->state;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_ReadVFXSpawnerEventRecord(
    const AnityGraphicsDevice* device, uint64_t effectId, int64_t contextId,
    uint8_t* record, int32_t recordCapacity, int32_t* outWritten) {
  if (!device || effectId == 0 || contextId == 0 || recordCapacity < 0 ||
      !outWritten || !device->vfxEvents)
    return ANITY_ERR_INVALID_ARG;
  *outWritten = 0;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->spawners.find(
      AnityGraphicsVFXSpawnerKey{effectId, contextId});
  if (found == device->vfxEvents->spawners.end()) return ANITY_ERR_INVALID_ARG;
  const size_t byteCount = found->second->eventRecord.size() * sizeof(uint32_t);
  if (byteCount > static_cast<size_t>(std::numeric_limits<int32_t>::max()) ||
      recordCapacity < static_cast<int32_t>(byteCount) || (byteCount > 0 && !record))
    return ANITY_ERR_INVALID_ARG;
  if (byteCount > 0)
    std::memcpy(record, found->second->eventRecord.data(), byteCount);
  *outWritten = static_cast<int32_t>(byteCount);
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_ClearVFXEffectState(
    AnityGraphicsDevice* device, uint64_t effectId) {
  if (!device || effectId == 0) return ANITY_ERR_INVALID_ARG;
  if (!device->vfxEvents) return ANITY_OK;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto pending = device->vfxEvents->pendingUpdateByEffect.find(effectId);
  if (pending != device->vfxEvents->pendingUpdateByEffect.end()) {
    AnityResult cancelResult = CancelVFXPendingUpdatesForEffectLocked(
        device, device->vfxEvents, effectId);
    if (cancelResult != ANITY_OK) return cancelResult;
  }
  AnityResult initializeCancel = CancelPendingInitializeForEffectLocked(
      device, device->vfxEvents, effectId);
  if (initializeCancel != ANITY_OK) return initializeCancel;
  device->vfxEvents->entries.erase(effectId);
  device->vfxEvents->inputEntries.erase(effectId);
  device->vfxEvents->outputEntries.erase(effectId);
  device->vfxEvents->outputSequences.erase(effectId);
  device->vfxEvents->frameStates.erase(effectId);
  device->vfxEvents->cullingStates.erase(effectId);
  device->vfxEvents->planarOutputs.erase(effectId);
  device->vfxEvents->cullingEffectIds.erase(
      std::remove(device->vfxEvents->cullingEffectIds.begin(),
                  device->vfxEvents->cullingEffectIds.end(), effectId),
      device->vfxEvents->cullingEffectIds.end());
  for (auto it = device->vfxEvents->initializeDispatches.begin();
       it != device->vfxEvents->initializeDispatches.end();) {
    if (it->first.effectId == effectId)
      it = device->vfxEvents->initializeDispatches.erase(it);
    else
      ++it;
  }
  for (auto it = device->vfxEvents->particleSystems.begin();
       it != device->vfxEvents->particleSystems.end();) {
    if (it->first.effectId == effectId)
      it = device->vfxEvents->particleSystems.erase(it);
    else
      ++it;
  }
  for (auto it = device->vfxEvents->spawners.begin();
       it != device->vfxEvents->spawners.end();) {
    if (it->first.effectId == effectId)
      it = device->vfxEvents->spawners.erase(it);
    else
      ++it;
  }
  if (device->type == ANITY_GFX_METAL)
    AnityGraphics_Metal_ClearVFXEffectResources(device, effectId);
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_EnqueueVFXOutputEventRecords(
    AnityGraphicsDevice* device, const AnityGraphicsVFXEventUploadDesc* desc,
    const uint8_t* records, int32_t byteCount) {
  if (!device || !desc || !IsValidUpload(*desc, records, byteCount))
    return ANITY_ERR_INVALID_ARG;
  AnityGraphicsVFXEventRegistry* registry = EnsureRegistry(device);
  if (!registry) return ANITY_ERR_OUT_OF_MEMORY;
  try {
    auto entry = std::make_shared<AnityGraphicsVFXEventStorage>();
    if (byteCount > 0) entry->records.assign(records, records + byteCount);
    entry->info.desc = *desc;
    entry->info.byteCount = byteCount;
    entry->info.backendKind = 0;
    std::lock_guard<std::mutex> lock(registry->mutex);
    uint64_t& latestSequence = registry->outputSequences[desc->effectId];
    if (latestSequence >= desc->sequence) return ANITY_ERR_INVALID_ARG;
    auto& queue = registry->outputEntries[desc->effectId];
    if (queue.size() >= kMaxOutputBatchesPerEffect)
      return ANITY_ERR_OUT_OF_MEMORY;
    entry->info.uploadGeneration = ++registry->generation;
    latestSequence = desc->sequence;
    queue.push_back(std::move(entry));
    return ANITY_OK;
  } catch (const std::bad_alloc&) {
    return ANITY_ERR_OUT_OF_MEMORY;
  }
}

AnityResult ANITY_CALL AnityGraphics_GetVFXOutputEventCount(
    const AnityGraphicsDevice* device, uint64_t effectId, int32_t* outCount) {
  if (!device || effectId == 0 || !outCount) return ANITY_ERR_INVALID_ARG;
  *outCount = 0;
  if (!device->vfxEvents) return ANITY_OK;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->outputEntries.find(effectId);
  if (found == device->vfxEvents->outputEntries.end()) return ANITY_OK;
  if (found->second.size() > static_cast<size_t>(std::numeric_limits<int32_t>::max()))
    return ANITY_ERR_INVALID_ARG;
  *outCount = static_cast<int32_t>(found->second.size());
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_PeekVFXOutputEventInfo(
    const AnityGraphicsDevice* device, uint64_t effectId,
    AnityGraphicsVFXEventUploadInfo* outInfo) {
  if (!device || effectId == 0 || !outInfo) return ANITY_ERR_INVALID_ARG;
  *outInfo = {};
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->outputEntries.find(effectId);
  if (found == device->vfxEvents->outputEntries.end() || found->second.empty())
    return ANITY_ERR_INVALID_ARG;
  *outInfo = found->second.front()->info;
  return ANITY_OK;
}

AnityResult ANITY_CALL AnityGraphics_DequeueVFXOutputEventRecords(
    AnityGraphicsDevice* device, uint64_t effectId, uint64_t expectedSequence,
    uint8_t* records, int32_t recordCapacity, int32_t* outWritten) {
  if (!device || effectId == 0 || expectedSequence == 0 || recordCapacity < 0 ||
      !outWritten)
    return ANITY_ERR_INVALID_ARG;
  *outWritten = 0;
  if (!device->vfxEvents) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(device->vfxEvents->mutex);
  auto found = device->vfxEvents->outputEntries.find(effectId);
  if (found == device->vfxEvents->outputEntries.end() || found->second.empty())
    return ANITY_ERR_INVALID_ARG;
  const auto& entry = found->second.front();
  if (entry->info.desc.sequence != expectedSequence) return ANITY_ERR_INVALID_ARG;
  int32_t required = entry->info.byteCount;
  if (recordCapacity < required || (required > 0 && !records))
    return ANITY_ERR_INVALID_ARG;
  if (required > 0) std::memcpy(records, entry->records.data(), required);
  *outWritten = required;
  found->second.pop_front();
  if (found->second.empty()) device->vfxEvents->outputEntries.erase(found);
  return ANITY_OK;
}

}  // extern "C"
