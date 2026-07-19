#pragma once
#include "../anity_core.h"
#include "anity_hdr.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Aligns with UnityEngine.Rendering.GraphicsDeviceType values */
typedef enum AnityGraphicsDeviceType {
  ANITY_GFX_NULL = 4,
  ANITY_GFX_D3D11 = 2,
  ANITY_GFX_OPENGLES2 = 8,
  ANITY_GFX_OPENGLES3 = 11,
  ANITY_GFX_METAL = 16,
  ANITY_GFX_OPENGLCORE = 17,
  ANITY_GFX_D3D12 = 18,
  ANITY_GFX_VULKAN = 21,
  ANITY_GFX_WEBGL2 = 28
} AnityGraphicsDeviceType;

typedef struct AnityGraphicsDeviceDesc {
  AnityGraphicsDeviceType preferred;
  int32_t width;
  int32_t height;
  int32_t hdrEnabled;
  int32_t msaaSamples;
  int32_t vsync;
  void* nativeWindow;
} AnityGraphicsDeviceDesc;

/* Backend-neutral camera render-pass control plane. The renderer owns the
 * actual backend encoders; this ABI records the validated target, viewport,
 * clear/load/store contract in native state so Metal/Vulkan/D3D backends can
 * consume the exact same pass description. */
typedef enum AnityGraphicsCameraPassFlags {
  ANITY_CAMERA_PASS_CLEAR_COLOR = 1 << 0,
  ANITY_CAMERA_PASS_CLEAR_DEPTH = 1 << 1,
  ANITY_CAMERA_PASS_STORE_COLOR = 1 << 2,
  ANITY_CAMERA_PASS_STORE_DEPTH = 1 << 3,
  ANITY_CAMERA_PASS_FINAL = 1 << 4,
  ANITY_CAMERA_PASS_HDR = 1 << 5,
  /* targetId is an object identity and can numerically equal CameraTarget.
   * Keep attachment ownership explicit rather than inferring it from 2. */
  ANITY_CAMERA_PASS_TARGET_IS_CAMERA_TARGET = 1 << 6
} AnityGraphicsCameraPassFlags;

typedef struct AnityGraphicsCameraPassDesc {
  uint64_t targetId;
  int32_t targetWidth;
  int32_t targetHeight;
  float viewportX;
  float viewportY;
  float viewportWidth;
  float viewportHeight;
  float clearR;
  float clearG;
  float clearB;
  float clearA;
  float clearDepth;
  int32_t msaaSamples;
  int32_t depthSlice;
  /* Number of contiguous array layers encoded by this pass.  Unity XR
   * single-pass instancing uses two layers beginning at depthSlice. */
  int32_t depthSliceCount;
  uint32_t flags;
} AnityGraphicsCameraPassDesc;

typedef struct AnityGraphicsCameraPassInfo {
  uint64_t frameId;
  uint64_t sequence;
  AnityGraphicsCameraPassDesc desc;
} AnityGraphicsCameraPassInfo;

/* Device-owned offscreen target used by Camera.targetTexture. This is
 * separate from the sampled Texture registry: render targets have depth and
 * command-buffer lifetime in addition to a color texture. */
typedef struct AnityGraphicsCameraRenderTargetDesc {
  uint64_t targetId;
  int32_t width;
  int32_t height;
  int32_t msaaSamples;
  int32_t hdrEnabled;
  /* UnityEngine.Rendering.TextureDimension: Tex2D=2, Tex2DArray=5. */
  int32_t dimension;
  int32_t volumeDepth;
  /* 0 = backend default LDR/HDR color, 1 = RGBA8 signed normalized,
   * 2 = RG16Float motion vector target. */
  int32_t colorFormat;
} AnityGraphicsCameraRenderTargetDesc;

/* Internal evidence for the URP HDR post-process curve resource. This is not
 * part of Unity's public managed API; it makes native cache behavior testable. */
typedef struct AnityGraphicsHDRPostProcessStats {
  int32_t backendKind; /* 0=none, 2=Metal */
  int32_t curveLutSamplesPerCurve;
  uint64_t curveLutByteCapacity;
  uint64_t curveLutUploadCount;
  uint64_t curveLutCacheHitCount;
} AnityGraphicsHDRPostProcessStats;

typedef struct AnityGraphicsDevice AnityGraphicsDevice;
typedef struct AnityUICanvas AnityUICanvas;

/* Internal deterministic diagnostics for exercising asynchronous VFX
 * recovery. This is an Anity extension and is not part of Unity's public API. */
typedef enum AnityGraphicsVFXFailurePoint {
  ANITY_GFX_VFX_FAILURE_INITIALIZE_COMMAND = 1,
  ANITY_GFX_VFX_FAILURE_UPDATE_COMMAND = 2,
  ANITY_GFX_VFX_FAILURE_PLANAR_CAMERA_COMMAND = 3,
  ANITY_GFX_VFX_FAILURE_DEVICE_REMOVAL = 4
} AnityGraphicsVFXFailurePoint;

typedef struct AnityGraphicsUIUploadStats {
  uint64_t frameId;
  uint64_t uploadGeneration;
  int32_t submitted;
  int32_t batchCount;
  int32_t drawCount;
  int32_t vertexCount;
  int32_t indexCount;
  int32_t vertexBytes;
  int32_t indexBytes;
  int32_t ringIndex;
  int32_t backendKind; /* 0=CPU/headless, 1=Vulkan, 2=Metal, 3=D3D11 */
} AnityGraphicsUIUploadStats;

typedef struct AnityGraphicsTextureDesc {
  uint64_t textureId;
  uint64_t revision;
  int32_t width;
  int32_t height;
  int32_t mipCount;
  int32_t filterMode; /* Unity FilterMode: Point=0, Bilinear=1, Trilinear=2 */
  int32_t wrapU;      /* Unity TextureWrapMode */
  int32_t wrapV;
  int32_t linear;
  /* Unity Texture.mipMapBias; applied by backend texture sampling paths. */
  float mipMapBias;
  /* Effective anisotropy after QualitySettings policy, in [1, 16]. */
  int32_t anisoLevel;
} AnityGraphicsTextureDesc;

typedef struct AnityGraphicsTextureInfo {
  AnityGraphicsTextureDesc desc;
  int32_t byteCount;
  uint64_t uploadGeneration;
  int32_t backendKind; /* 0=CPU registry, 1=Vulkan, 2=Metal, 3=D3D11 */
} AnityGraphicsTextureInfo;

/* One CPU-authored VFX event record upload. Records use the compiled VFX
 * StructureOfArrayProvider-compatible 32-bit word layout. */
typedef struct AnityGraphicsVFXEventUploadDesc {
  uint64_t effectId;
  uint64_t sequence;
  int32_t eventNameId;
  int32_t recordCount;
  int32_t strideBytes;
} AnityGraphicsVFXEventUploadDesc;

typedef struct AnityGraphicsVFXEventUploadInfo {
  AnityGraphicsVFXEventUploadDesc desc;
  int32_t byteCount;
  uint64_t uploadGeneration;
  int32_t backendKind; /* 0=native CPU staging, GPU dispatch backends follow */
} AnityGraphicsVFXEventUploadInfo;

/* Immutable prefix-sum snapshot of pending CPU-authored VFX events. All
 * non-empty batches in one effect use the compiled asset's shared record
 * stride. startEventIndex addresses the packed record buffer copied below. */
typedef struct AnityGraphicsVFXEventDispatchPlanInfo {
  uint64_t effectId;
  uint64_t firstSequence;
  uint64_t lastSequence;
  int32_t batchCount;
  int32_t recordCount;
  int32_t strideBytes;
  int32_t byteCount;
  uint64_t uploadGeneration;
} AnityGraphicsVFXEventDispatchPlanInfo;

typedef struct AnityGraphicsVFXEventDispatchBatch {
  uint64_t sequence;
  int32_t eventNameId;
  int32_t startEventIndex;
  int32_t recordCount;
  int32_t strideBytes;
} AnityGraphicsVFXEventDispatchBatch;

/* One compiled CPU Event batch routed to one Initialize context. The source
 * buffer is the complete prefix-summed event record plan; startEventIndex
 * selects this batch's first record. */
typedef struct AnityGraphicsVFXInitializeDispatchDesc {
  uint64_t effectId;
  uint64_t sequence;
  int64_t initializeContextId;
  int64_t sourceSpawnerContextId;
  int32_t eventNameId;
  int32_t particleSystemId;
  int32_t spawnSystemId;
  int32_t startEventIndex;
  int32_t recordCount;
  int32_t strideBytes;
} AnityGraphicsVFXInitializeDispatchDesc;

typedef struct AnityGraphicsVFXInitializeDispatchInfo {
  AnityGraphicsVFXInitializeDispatchDesc desc;
  int32_t sourceByteCount;
  int32_t outputByteCount;
  uint64_t dispatchGeneration;
  int32_t backendKind; /* 0=CPU reference, 1=Vulkan, 2=Metal, 3=D3D11 */
} AnityGraphicsVFXInitializeDispatchInfo;

/* Backend-neutral VFX Initialize kernel ABI. Arrays are transaction-global;
 * each kernel descriptor addresses contiguous attribute/operation ranges. */
typedef struct AnityGraphicsVFXInitializeKernelDesc {
  uint32_t version;
  uint32_t flags; /* bit0=consume particle dead list */
  int32_t particleCapacity;
  int32_t attributeStrideBytes;
  int32_t sourceStrideBytes;
  int32_t attributeStart;
  int32_t attributeCount;
  int32_t operationStart;
  int32_t operationCount;
  int32_t spawnCountSourceOffsetBytes; /* -1=one spawn candidate per source record */
  uint32_t systemSeed;
} AnityGraphicsVFXInitializeKernelDesc;

typedef struct AnityGraphicsVFXInitializeAttributeDesc {
  int32_t offsetBytes;
  int32_t componentCount;
  int32_t valueType; /* VFXRuntimeValueType: bool,uint,int,float,float2..4 */
  int32_t semantic;  /* 0=none, 1=alive */
  uint32_t defaultWords[4];
} AnityGraphicsVFXInitializeAttributeDesc;

typedef struct AnityGraphicsVFXInitializeOperationDesc {
  int32_t targetOffsetBytes;
  int32_t sourceOffsetBytes;
  int32_t componentCount;
  int32_t valueType;
  int32_t valueSource; /* 0=constant,1=source,2=particleId,3=seed,4=spawnIndex */
  int32_t composition; /* 0=overwrite,1=add,2=multiply,3=blend */
  int32_t randomMode;  /* 0=off,1=per-component,2=uniform */
  int32_t reserved;
  uint32_t valueA[4];
  uint32_t valueB[4];
  uint32_t blendFactorBits;
} AnityGraphicsVFXInitializeOperationDesc;

typedef struct AnityGraphicsVFXParticleSystemInfo {
  uint64_t effectId;
  int32_t particleSystemId;
  int32_t capacity;
  int32_t attributeStrideBytes;
  int32_t aliveCount;
  int32_t deadCount;
  int32_t backendKind; /* 0=CPU reference, 1=Vulkan, 2=Metal, 3=D3D11 */
  uint64_t generation;
} AnityGraphicsVFXParticleSystemInfo;

/* Backend-neutral executable Update IR. Operations run in serialized block
 * order against one particle-local working record. Source-snapshot reads use
 * the record as it existed at Update entry, matching VFX Graph source
 * attribute semantics. */
typedef struct AnityGraphicsVFXUpdateKernelDesc {
  uint32_t version; /* 1=ordered Update operation IR */
  uint32_t flags;   /* bit0=uses dead list, bit1=skip zero delta update */
  uint64_t effectId;
  int64_t contextId;
  int32_t particleSystemId;
  int32_t particleCapacity;
  int32_t attributeStrideBytes;
  int32_t operationStart;
  int32_t operationCount;
  int32_t aliveOffsetBytes; /* -1 when the layout has no alive attribute */
  int32_t seedOffsetBytes;  /* -1 when random state is unused */
  float deltaTime;
  uint32_t systemSeed;
} AnityGraphicsVFXUpdateKernelDesc;

typedef struct AnityGraphicsVFXUpdateOperationDesc {
  int32_t kind; /* 0=set,1=copy,2=integrate,3=reap,4=force,5=relative force,6=drag */
  int32_t targetOffsetBytes;
  int32_t sourceAOffsetBytes;
  int32_t sourceBOffsetBytes;
  int32_t auxiliaryOffset0Bytes;
  int32_t auxiliaryOffset1Bytes;
  int32_t componentCount;
  int32_t valueType;
  int32_t composition;
  int32_t randomMode;
  int32_t flags; /* set bit0=read value from Update-entry source snapshot */
  uint32_t valueA[4];
  uint32_t valueB[4];
  uint32_t blendFactorBits;
} AnityGraphicsVFXUpdateOperationDesc;

/* Internal backend evidence for the persistent VFX Update resource path.
 * This is intentionally separate from Unity's public managed API. */
typedef struct AnityGraphicsVFXUpdateBackendStats {
  uint64_t effectId;
  int32_t particleSystemId;
  int32_t backendKind; /* 0=CPU reference, 1=Vulkan, 2=Metal, 3=D3D11 */
  uint64_t residentGeneration;
  uint64_t dispatchCount;
  uint64_t particleUploadCount;
  uint64_t operationUploadCount;
  uint64_t gpuCopyCount;
  uint64_t completionCount;
  int32_t ringIndex;
  int32_t ringSize;
  uint64_t particleBufferCapacityBytes;
  uint64_t operationBufferCapacityBytes;
  uint64_t synchronousReadbackCount;
  int32_t lastBatchWidth;
  int32_t peakBatchWidth;
  uint64_t asyncBatchCount;
  uint64_t boundsDispatchCount;
  uint64_t boundsResidentHitCount;
  uint64_t boundsParticleUploadCount;
  uint64_t boundsCompletionCount;
  uint64_t boundsResultCacheHitCount;
  uint64_t boundsPendingDispatchCount;
  uint64_t boundsPendingPublishCount;
  uint64_t boundsPendingDiscardCount;
  uint64_t deadPrefixPassCount;
  uint64_t deadCompactionDispatchCount;
  uint64_t residentOnlyPublishCount;
  uint64_t deferredParticleReadbackCount;
  uint64_t deferredParticleReadbackBytes;
  uint64_t residentSnapshotCount;
  uint64_t residentRestoreCount;
  uint64_t residentSnapshotDiscardCount;
  /* Two-phase frame commit: the resident GPU buffer can be queue-visible
   * before compacted dead-list/alive/bounds metadata is observed by the CPU. */
  uint64_t asynchronousResidentPublishCount;
  uint64_t asynchronousResidentCompletionCount;
  uint64_t asynchronousResidentRollbackCount;
  uint64_t completionWaitCount;
  uint64_t cameraDependencyCount;
  /* Number of resident generations published while CPU completion metadata
   * and ring-slot retirement remain pending (bounded by the update ring). */
  uint64_t pendingUpdateCount;
  /* Next-frame preparation is an overlap window, not a metadata dependency.
   * These counters prove whether preparation polled, deferred, or retired an
   * already-completed asynchronous update without entering a CPU wait. */
  uint64_t preparationPollCount;
  uint64_t preparationDeferredCount;
  uint64_t preparationRetiredCount;
  /* Particle allocation metadata is generation-resident beside attributes:
   * {aliveCount, deadCount, nextSequentialIndex, usesDeadList}. */
  uint64_t allocationStateGeneration;
  uint64_t allocationStateUploadCount;
  uint64_t allocationStateGpuCopyCount;
  uint64_t allocationStateResidentHitCount;
  /* Initialize can mutate the resident particle/dead/allocation buffers
   * directly after Update without materializing the full particle array. */
  uint64_t residentInitializeCount;
  uint64_t residentInitializeSpawnCount;
  uint64_t residentInitializeReadbackAvoidedBytes;
  uint64_t residentInitializeAllocationStateReadCount;
  uint64_t allocationStateReadbackCount;
  uint64_t deadListReadbackCount;
  uint64_t metadataReadbackBytes;
  uint64_t metadataReadbackGeneration;
  uint64_t residentInitializeIndirectDispatchCount;
  uint64_t residentInitializeIndirectPreparationCount;
  uint64_t residentInitializeSourceStateGpuCopyCount;
  uint64_t initializeCpuDispatchSizingCount;
  /* Resident Initialize writes into a ring target, then queue-publishes the
   * three particle/dead/allocation buffers as one generation. The source
   * snapshot remains available until CPU metadata retirement or rollback. */
  uint64_t residentInitializeTargetCopyCount;
  uint64_t residentInitializeTargetCopyBytes;
  uint64_t residentInitializeAtomicPublishCount;
  uint64_t asynchronousInitializeBeginCount;
  uint64_t asynchronousInitializePollCount;
  uint64_t asynchronousInitializeCompletionCount;
  uint64_t asynchronousInitializeCancelCount;
  uint64_t asynchronousInitializeResidentPublishCount;
  uint64_t asynchronousInitializeResidentCompletionCount;
  uint64_t asynchronousInitializeResidentRollbackCount;
  uint64_t pendingInitializeCount;
} AnityGraphicsVFXUpdateBackendStats;

typedef struct AnityGraphicsVFXUpdateTicketInfo {
  uint64_t ticketId;
  uint64_t effectId;
  uint32_t frameIndex;
  int32_t state; /* 0=pending, 1=ready, 2=failed */
  int32_t kernelCount;
  int32_t backendKind;
  uint64_t preparedFrameGeneration;
  uint64_t submitGeneration;
} AnityGraphicsVFXUpdateTicketInfo;

typedef struct AnityGraphicsVFXInitializeTicketInfo {
  uint64_t ticketId;
  uint64_t effectId; /* 0 when one atomic transaction spans multiple effects. */
  int32_t state; /* 0=pending, 1=ready, 2=failed */
  int32_t dispatchCount;
  int32_t backendKind;
  int32_t effectCount;
  uint64_t sourceRegistryGeneration;
  uint64_t targetRegistryGeneration;
} AnityGraphicsVFXInitializeTicketInfo;

/* Runtime layout used by Automatic bounds reduction. Offsets are byte offsets
 * into one particle record; -1 means the optional attribute is absent. */
typedef struct AnityGraphicsVFXBoundsReductionDesc {
  uint64_t effectId;
  int32_t particleSystemId;
  int32_t positionOffsetBytes;
  int32_t aliveOffsetBytes;
  int32_t sizeOffsetBytes;
  int32_t scaleXOffsetBytes;
  int32_t scaleYOffsetBytes;
  int32_t scaleZOffsetBytes;
  float paddingX;
  float paddingY;
  float paddingZ;
  int32_t boundsInWorldSpace;
  int32_t reserved;
} AnityGraphicsVFXBoundsReductionDesc;

typedef struct AnityGraphicsVFXBoundsReductionResult {
  uint64_t effectId;
  int32_t particleSystemId;
  int32_t valid;
  float centerX;
  float centerY;
  float centerZ;
  float extentsX;
  float extentsY;
  float extentsZ;
  int32_t backendKind; /* 0=CPU native fallback, 2=Metal compute */
  int32_t boundsInWorldSpace;
  uint64_t generation;
} AnityGraphicsVFXBoundsReductionResult;

/* Device-owned VFX simulation clock. frameIndex is shared by every effect on
 * one graphics device; accumulator and totalTime are isolated per effect.
 * Prepare exposes the pre-commit total required by Dynamic Built-In callback
 * expressions. Commit advances totalTime after all callbacks have completed. */
typedef struct AnityGraphicsVFXFrameState {
  uint64_t effectId;
  uint32_t frameIndex;
  uint32_t stepCount;
  float gameDeltaTime;
  float unscaledDeltaTime;
  float deltaTime;
  float totalTime;
  float accumulator;
  uint32_t prepared;
  uint64_t generation;
} AnityGraphicsVFXFrameState;

/* Per-PlayerLoop VFX culling transaction. Bounds are world-space AABBs.
 * Begin snapshots the previous completed result used by simulation, camera
 * submissions OR visibility across the whole camera stack, and Complete
 * publishes the result for the next PlayerLoop update (Unity one-frame
 * visibility latency). Effects without static bounds and frames without a
 * camera always simulate. */
typedef struct AnityGraphicsVFXCullingBounds {
  uint64_t effectId;
  float centerX;
  float centerY;
  float centerZ;
  float extentsX;
  float extentsY;
  float extentsZ;
  int32_t layer;
  int32_t valid;
} AnityGraphicsVFXCullingBounds;

typedef struct AnityGraphicsVFXCullingCamera {
  uint64_t cameraId;
  float m00, m01, m02, m03;
  float m10, m11, m12, m13;
  float m20, m21, m22, m23;
  float m30, m31, m32, m33;
  int32_t cullingMask;
  int32_t cameraType;
} AnityGraphicsVFXCullingCamera;

typedef struct AnityGraphicsVFXCullingState {
  uint64_t effectId;
  uint64_t playerLoopToken;
  int32_t culled;
  int32_t hasBounds;
  int32_t cameraCount;
  int32_t visibleCameraCount;
  uint64_t generation;
} AnityGraphicsVFXCullingState;

/* Immutable runtime Planar Output ABI compiled from VFX Graph runtime asset v15.
 * Attribute offsets are byte offsets in the shared particle record. flags:
 * bit0=compiler program executable, bit1=alpha clipping, bit2=sorting required,
 * bit3=indirect draw requested. Backend support is reported per camera draw and
 * never inferred from the compiler-executable bit. */
typedef struct AnityGraphicsVFXPlanarOutputDesc {
  uint32_t version; /* 1 */
  uint32_t flags;
  uint64_t effectId;
  int64_t contextId;
  int32_t particleSystemId;
  int32_t primitiveType; /* 0=triangle,1=quad,2=octagon */
  int32_t particleCapacity;
  int32_t attributeStrideBytes;
  int32_t aliveOffsetBytes;
  int32_t positionOffsetBytes;
  int32_t colorOffsetBytes;
  int32_t alphaOffsetBytes;
  int32_t axisXOffsetBytes;
  int32_t axisYOffsetBytes;
  int32_t axisZOffsetBytes;
  int32_t angleXOffsetBytes;
  int32_t angleYOffsetBytes;
  int32_t angleZOffsetBytes;
  int32_t pivotXOffsetBytes;
  int32_t pivotYOffsetBytes;
  int32_t pivotZOffsetBytes;
  int32_t sizeOffsetBytes;
  int32_t scaleXOffsetBytes;
  int32_t scaleYOffsetBytes;
  int32_t scaleZOffsetBytes;
  int32_t uvMode;
  int32_t blendMode;
  int32_t cullMode;
  int32_t zWrite;
  int32_t zTest;
  int32_t renderQueue;
  int32_t reserved0;
  int32_t reserved1;
} AnityGraphicsVFXPlanarOutputDesc;

/* One camera/effect submission. Matrices use Unity Matrix4x4 row-major field
 * order. flags bit0 clears the target to transparent before drawing; product
 * camera integration normally leaves it unset and composes over the URP target. */
typedef struct AnityGraphicsVFXPlanarCameraDesc {
  uint64_t cameraId;
  float localToWorld[16];
  float worldToClip[16];
  int32_t cullingMask;
  int32_t cameraType;
  int32_t flags;
  int32_t reserved;
} AnityGraphicsVFXPlanarCameraDesc;

typedef struct AnityGraphicsVFXPlanarDrawInfo {
  uint64_t effectId;
  uint64_t cameraId;
  uint64_t residentGeneration;
  int32_t outputCount;
  int32_t drawCount;
  int32_t skippedOutputCount;
  int32_t particleCount;
  int32_t vertexCount;
  int32_t backendKind; /* 0=no backend draw, 2=Metal */
} AnityGraphicsVFXPlanarDrawInfo;

/* One effect in a camera-wide Planar Output submission. Effects are flattened
 * with their registered outputs, filtered by layer and then stably ordered by
 * renderQueue, sortOrder, effectId and output contextId before backend draw. */
typedef struct AnityGraphicsVFXPlanarEffectDesc {
  uint64_t effectId;
  float localToWorld[16];
  int32_t layer;
  int32_t sortOrder;
} AnityGraphicsVFXPlanarEffectDesc;

/* Camera-wide Planar Output submission. Matrices use Unity Matrix4x4 row-major
 * field order. flags bit0 clears color and depth once before the entire batch. */
typedef struct AnityGraphicsVFXPlanarCameraBatchDesc {
  uint64_t cameraId;
  float worldToClip[16];
  int32_t cullingMask;
  int32_t cameraType;
  int32_t flags;
  int32_t reserved;
} AnityGraphicsVFXPlanarCameraBatchDesc;

typedef struct AnityGraphicsVFXPlanarCameraDrawInfo {
  uint64_t cameraId;
  uint64_t residentGeneration;
  uint64_t submissionGeneration;
  int32_t effectCount;
  int32_t outputCount;
  int32_t drawCount;
  int32_t skippedOutputCount;
  int32_t particleCount;
  int32_t vertexCount;
  int32_t backendKind; /* 0=no backend draw, 2=Metal */
  int32_t commandBufferCount;
  int32_t renderPassCount;
  int32_t reserved;
  int32_t aliveCompactionCount;
  int32_t aliveCompactionCacheHitCount;
  int32_t alivePrefixPassCount;
  int32_t indirectArgumentCount;
  uint64_t capacityVertexCount;
  int32_t depthTestOutputCount;
  int32_t depthWriteOutputCount;
  int32_t depthStateChangeCount;
  int32_t depthClearCount;
  int32_t sortedOutputCount;
  int32_t sortCacheHitCount;
  int32_t sortMapDispatchCount;
  int32_t sortStageDispatchCount;
  int32_t sortExtractDispatchCount;
  int32_t sortPaddedParticleCount;
  int32_t sortCacheInsertCount;
  int32_t sortCacheEvictionCount;
  int32_t sortCacheEntryCount;
  int32_t sortCacheCapacityPerSystem;
  uint64_t submissionId;
  int32_t asyncSubmissionCount;
  int32_t synchronousWaitCount;
} AnityGraphicsVFXPlanarCameraDrawInfo;

typedef struct AnityGraphicsVFXPlanarSubmissionStats {
  uint64_t submissionCount;
  uint64_t completionCount;
  uint64_t failureCount;
  uint64_t lastSubmittedId;
  uint64_t lastCompletedId;
  uint64_t lastFailedId;
  uint64_t waitCount;
  int32_t inFlightCount;
  int32_t maxInFlightCount;
  int32_t backendKind; /* 0=no backend submissions, 2=Metal */
  int32_t deviceLost; /* backend observed terminal device removal */
  uint64_t resultEvictionCount; /* exact fence results dropped from history */
} AnityGraphicsVFXPlanarSubmissionStats;

/* Device-owned VFX Spawner scheduler ABI. Programs are immutable compiled IR;
 * clocks, random streams and block accumulators are native per-effect state. */
typedef struct AnityGraphicsVFXSpawnerProgramDesc {
  uint32_t version; /* 4=base task IR, 5=ordered managed callback task IR */
  uint32_t eventStrideWords; /* zero when no Set SpawnEvent Attribute task */
  uint64_t effectId;
  int64_t contextId;
  int32_t systemId;
  int32_t blockStart;
  int32_t blockCount;
  int32_t loopDurationMode;   /* 0=disabled,1=constant,2=random,3=infinite */
  int32_t loopCountMode;
  int32_t delayBeforeLoopMode;
  int32_t delayAfterLoopMode;
  float loopDurationMin;
  float loopDurationMax;
  double loopCountMin; /* constant Int32 or exact promoted Random Float2 endpoint */
  double loopCountMax;
  float delayBeforeLoopMin;
  float delayBeforeLoopMax;
  float delayAfterLoopMin;
  float delayAfterLoopMax;
} AnityGraphicsVFXSpawnerProgramDesc;

typedef struct AnityGraphicsVFXSpawnerBlockDesc {
  int64_t blockId;
  int32_t kind;     /* 1=constant rate,2=variable rate,3=burst,4=set attribute,5=custom callback */
  int32_t periodic; /* 0/1, burst only */
  float valueMin;
  float valueMax;
  float periodMin;
  float periodMax;
  int32_t targetOffsetWords;
  int32_t valueType;  /* VFXRuntimeValueType */
  int32_t randomMode; /* 0=off,1=per-component,2=uniform */
  int32_t valueWordCount;
  uint32_t valueA[4];
  uint32_t valueB[4];
} AnityGraphicsVFXSpawnerBlockDesc;

typedef struct AnityGraphicsVFXSpawnerState {
  uint64_t effectId;
  int64_t contextId;
  int32_t systemId;
  int32_t loopState; /* VFXSpawnerLoopState */
  int32_t playing;
  int32_t newLoop;
  float spawnCount;
  float deltaTime;
  float totalTime;
  float delayBeforeLoop;
  float loopDuration;
  float delayAfterLoop;
  int32_t loopIndex;
  int32_t loopCount;
  float eventSpawnCount; /* accumulated Event record count; zero when no dispatch */
  uint64_t generation;
} AnityGraphicsVFXSpawnerState;

/* Synchronous managed callback boundary used by CustomCallbackSpawner tasks.
 * phase: 1=OnPlay, 2=OnUpdate, 3=OnStop. The callback may mutate state and,
 * during OnUpdate, the packed SpawnEvent record before native execution resumes. */
typedef AnityResult(ANITY_CALL* AnityGraphicsVFXSpawnerCallback)(
    void* userData, int64_t blockId, int32_t phase,
    AnityGraphicsVFXSpawnerState* state, uint8_t* eventRecord,
    int32_t eventRecordByteCount);

ANITY_API AnityResult ANITY_CALL AnityGraphics_CreateDevice(
    const AnityGraphicsDeviceDesc* desc,
    AnityGraphicsDevice** outDevice);

ANITY_API void ANITY_CALL AnityGraphics_DestroyDevice(AnityGraphicsDevice* device);

ANITY_API AnityGraphicsDeviceType ANITY_CALL AnityGraphics_GetDeviceType(
    const AnityGraphicsDevice* device);

ANITY_API AnityResult ANITY_CALL AnityGraphics_BeginFrame(AnityGraphicsDevice* device);
ANITY_API AnityResult ANITY_CALL AnityGraphics_EndFrame(AnityGraphicsDevice* device);
ANITY_API AnityResult ANITY_CALL AnityGraphics_RecordCameraPass(
    AnityGraphicsDevice* device, const AnityGraphicsCameraPassDesc* desc,
    AnityGraphicsCameraPassInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetLastCameraPass(
    const AnityGraphicsDevice* device, AnityGraphicsCameraPassInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityGraphics_EnsureCameraRenderTarget(
    AnityGraphicsDevice* device, const AnityGraphicsCameraRenderTargetDesc* desc);
ANITY_API AnityResult ANITY_CALL AnityGraphics_DestroyCameraRenderTarget(
    AnityGraphicsDevice* device, uint64_t targetId);
/* Writes tightly packed top-to-bottom RGBA8 rows. HDR target readback is not
 * implicitly tone-mapped and therefore reports NotSupported. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_ReadbackCameraRenderTargetRGBA8(
    AnityGraphicsDevice* device, uint64_t targetId, uint8_t* pixels,
    int32_t pixelCapacity, int32_t* outWritten);
/* Reads one Tex2DArray layer. The original readback entry point remains an
 * ABI-stable shorthand for depthSlice=0. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_ReadbackCameraRenderTargetSliceRGBA8(
    AnityGraphicsDevice* device, uint64_t targetId, int32_t depthSlice,
    uint8_t* pixels, int32_t pixelCapacity, int32_t* outWritten);
/* Explicit ACES-to-sRGB readback for RGBA16Float HDR targets. Raw RGBA8
 * readback above intentionally never applies an implicit tone map. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_ReadbackCameraRenderTargetToneMappedRGBA8(
    AnityGraphicsDevice* device, uint64_t targetId, uint8_t* pixels,
    int32_t pixelCapacity, int32_t* outWritten);
/* Slice-aware HDR readback for XR Texture2DArray targets. The legacy entry
 * point remains an ABI-stable shorthand for depthSlice=0. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_ReadbackCameraRenderTargetToneMappedSliceRGBA8(
    AnityGraphicsDevice* device, uint64_t targetId, int32_t depthSlice,
    uint8_t* pixels, int32_t pixelCapacity, int32_t* outWritten);
/* GPU-only resolved color copy used by URP _CameraOpaqueTexture. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetColor(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, uint64_t destinationTargetId);
/* Slice-aware variant used when URP captures an opaque texture from a stereo
 * Tex2DArray camera target. The legacy entry point remains sourceSlice=0. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetColorSlice(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, int32_t sourceSlice, int32_t destinationSlice,
    uint64_t destinationTargetId);
/* Converts the camera depth attachment to R in a color target on the GPU. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetDepthToColor(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, uint64_t destinationTargetId);
/* Converts one eye slice of an array depth attachment into the matching color slice. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetDepthToColorSlice(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, int32_t sourceSlice, int32_t destinationSlice,
    uint64_t destinationTargetId);
/* Copies the native mesh-normal attachment to a color target.  The encoded
 * normals are 0.5 * normalizedNormal + 0.5; unwritten pixels remain zero. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetNormalsToColor(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, uint64_t destinationTargetId);
ANITY_API AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetNormalsToColorSlice(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, int32_t sourceSlice, int32_t destinationSlice,
    uint64_t destinationTargetId);
/* Copies the native RG16Float motion attachment to a matching target. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetMotionToColor(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, uint64_t destinationTargetId);
ANITY_API AnityResult ANITY_CALL AnityGraphics_CopyCameraRenderTargetMotionToColorSlice(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, int32_t sourceSlice, int32_t destinationSlice,
    uint64_t destinationTargetId);
/* Native indexed mesh submission for the URP geometry bridge. Positions are
 * object-space; normals have been inverse-transpose transformed to world space
 * by the managed renderer. objectToClip is Unity Matrix4x4 field order. */
typedef struct AnityGraphicsMeshVertex {
  float position[3];
  /* Renderer-local vertex position from the prior submitted frame.  Keeping
   * this per vertex is required for skinned motion vectors: a bone can move
   * while the renderer's object transform remains unchanged. */
  float previousPosition[3];
  float normal[3];
  float tangent[4];
  float texcoord[2];
  float color[4];
} AnityGraphicsMeshVertex;

typedef struct AnityGraphicsCameraMeshDrawDesc {
  uint64_t targetId;
  int32_t targetIsCameraTarget;
  /* 0=opaque, 1=alpha, 2=premultiplied alpha, 3=additive, 4=multiply. */
  int32_t blendMode;
  int32_t depthWriteEnabled;
  /* URP built-in motion-vector support is opaque-only (including alpha clip).
   * Transparent forward draws must retain the existing opaque velocity. */
  int32_t writeMotionVectors;
  int32_t depthSlice;
  int32_t alphaClipEnabled;
  float alphaClipThreshold;
  uint64_t baseTextureId;
  float baseMapST[4]; /* xy=scale, zw=offset */
  uint64_t normalMapTextureId;
  const AnityGraphicsMeshVertex* vertices;
  int32_t vertexCount;
  const uint32_t* indices;
  int32_t indexCount;
  float objectToClip[16];
  float motionObjectToClip[16];
  float previousObjectToClip[16];
  int32_t hasPreviousObjectToClip;
  /* 1=ordinary draw; 2=Metal single-pass instanced stereo. In the latter
   * case, the two matrix arrays are left/right and the target must be a
   * two-layer Tex2DArray beginning at depthSlice. */
  int32_t stereoInstanceCount;
  float stereoObjectToClip[32];
  float stereoMotionObjectToClip[32];
  float stereoPreviousObjectToClip[32];
} AnityGraphicsCameraMeshDrawDesc;

/* Backend-neutral CPU skinning bridge. The native implementation is the
 * authoritative deformation path for managed SkinnedMeshRenderer submission;
 * matrices use Unity Matrix4x4 field (row-major) order. */
typedef struct AnityGraphicsSkinVertex {
  float position[3];
  float normal[3];
  float tangent[4];
} AnityGraphicsSkinVertex;

/* Blend-shape deltas are pre-evaluated from Unity's multi-frame shape weights
 * by the managed Mesh compatibility layer, then composed here in native code
 * before the skinning kernel consumes the deformed stream. */
typedef struct AnityGraphicsBlendShapeDesc {
  const AnityGraphicsSkinVertex* sourceVertices;
  const AnityGraphicsSkinVertex* shapeDeltas; /* shapeCount * vertexCount */
  int32_t vertexCount;
  int32_t shapeCount;
  AnityGraphicsSkinVertex* outVertices;
  int32_t outVertexCount;
} AnityGraphicsBlendShapeDesc;

ANITY_API AnityResult ANITY_CALL AnityGraphics_ApplyBlendShapeDeltas(
    const AnityGraphicsBlendShapeDesc* desc);

typedef struct AnityGraphicsBoneWeight {
  float weight[4];
  int32_t boneIndex[4];
} AnityGraphicsBoneWeight;

typedef struct AnityGraphicsBoneWeight1 {
  float weight;
  int32_t boneIndex;
} AnityGraphicsBoneWeight1;

typedef struct AnityGraphicsSkinMeshDesc {
  const AnityGraphicsSkinVertex* sourceVertices;
  const AnityGraphicsBoneWeight* boneWeights;
  int32_t vertexCount;
  const float* boneMatrices; /* boneCount contiguous Unity Matrix4x4 values */
  int32_t boneCount;
  AnityGraphicsSkinVertex* outVertices;
  int32_t outVertexCount;
  /* Optional Unity 2022 variable-influence stream. When present it supersedes
   * boneWeights; maxInfluences is clamped by the managed quality policy. */
  const uint8_t* bonesPerVertex;
  const AnityGraphicsBoneWeight1* allBoneWeights;
  int32_t allBoneWeightCount;
  int32_t maxInfluences;
} AnityGraphicsSkinMeshDesc;

ANITY_API AnityResult ANITY_CALL AnityGraphics_SkinMeshVertices(
    const AnityGraphicsSkinMeshDesc* desc);

/* Draws an indexed triangle mesh into an existing camera target. Set
 * targetIsCameraTarget for the presentation swapchain; otherwise targetId
 * selects an offscreen RenderTexture-backed target. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_DrawCameraMesh(
    AnityGraphicsDevice* device, const AnityGraphicsCameraMeshDrawDesc* desc);
/* Applies the supplied URP HDR grade to an RGBA16Float camera target through
 * the native backend. The result is display-referred sRGB stored in the HDR
 * target so the final stack pass has an actual backend execution path. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_ProcessCameraRenderTargetHDR(
    AnityGraphicsDevice* device, uint64_t targetId,
    const AnityHDRColorGrade* grade);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetHDRPostProcessStats(
    const AnityGraphicsDevice* device,
    AnityGraphicsHDRPostProcessStats* outStats);
ANITY_API AnityResult ANITY_CALL AnityGraphics_Present(AnityGraphicsDevice* device);

/* Non-owning Canvas binding. EndFrame builds and uploads attached Canvas batches. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_SetUICanvas(
    AnityGraphicsDevice* device, AnityUICanvas* canvas);
ANITY_API AnityResult ANITY_CALL AnityGraphics_SubmitUICanvas(
    AnityGraphicsDevice* device, AnityUICanvas* canvas);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetUIUploadStats(
    const AnityGraphicsDevice* device, AnityGraphicsUIUploadStats* outStats);

/* Device-owned RGBA8 texture registry. Pixels contain tightly packed mip levels, largest first. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_UploadTextureRGBA8(
    AnityGraphicsDevice* device, const AnityGraphicsTextureDesc* desc,
    const uint8_t* pixels, int32_t byteCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_DestroyTexture(
    AnityGraphicsDevice* device, uint64_t textureId);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetTextureInfo(
    const AnityGraphicsDevice* device, uint64_t textureId,
    AnityGraphicsTextureInfo* outInfo);
ANITY_API void* ANITY_CALL AnityGraphics_GetTextureNativeHandle(
    const AnityGraphicsDevice* device, uint64_t textureId);

ANITY_API AnityResult ANITY_CALL AnityGraphics_UploadVFXEventRecords(
    AnityGraphicsDevice* device, const AnityGraphicsVFXEventUploadDesc* desc,
    const uint8_t* records, int32_t byteCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXEventUploadInfo(
    const AnityGraphicsDevice* device, uint64_t effectId,
    AnityGraphicsVFXEventUploadInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityGraphics_ReadbackVFXEventRecords(
    const AnityGraphicsDevice* device, uint64_t effectId,
    uint8_t* records, int32_t recordCapacity, int32_t* outWritten);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXEventDispatchPlanInfo(
    const AnityGraphicsDevice* device, uint64_t effectId,
    AnityGraphicsVFXEventDispatchPlanInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityGraphics_CopyVFXEventDispatchBatches(
    const AnityGraphicsDevice* device, uint64_t effectId,
    uint64_t throughSequence, AnityGraphicsVFXEventDispatchBatch* batches,
    int32_t batchCapacity, int32_t* outWritten);
ANITY_API AnityResult ANITY_CALL AnityGraphics_CopyVFXEventDispatchRecords(
    const AnityGraphicsDevice* device, uint64_t effectId,
    uint64_t throughSequence, uint8_t* records, int32_t recordCapacity,
    int32_t* outWritten);
ANITY_API AnityResult ANITY_CALL AnityGraphics_ConsumeVFXEventDispatchPlan(
    AnityGraphicsDevice* device, uint64_t effectId, uint64_t throughSequence);
ANITY_API AnityResult ANITY_CALL AnityGraphics_SubmitVFXInitializeDispatch(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* desc,
    const uint8_t* sourceRecords, int32_t sourceByteCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_SubmitVFXInitializeDispatches(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* descs, int32_t descCount,
    const uint8_t* sourceRecords, int32_t sourceByteCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_SubmitVFXInitializeKernels(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* dispatches,
    const AnityGraphicsVFXInitializeKernelDesc* kernels, int32_t dispatchCount,
    const AnityGraphicsVFXInitializeAttributeDesc* attributes, int32_t attributeCount,
    const AnityGraphicsVFXInitializeOperationDesc* operations, int32_t operationCount,
    const uint8_t* sourceRecords, int32_t sourceByteCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_BeginVFXInitializeKernels(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXInitializeDispatchDesc* dispatches,
    const AnityGraphicsVFXInitializeKernelDesc* kernels, int32_t dispatchCount,
    const AnityGraphicsVFXInitializeAttributeDesc* attributes, int32_t attributeCount,
    const AnityGraphicsVFXInitializeOperationDesc* operations, int32_t operationCount,
    const uint8_t* sourceRecords, int32_t sourceByteCount,
    uint64_t* outTicketId);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXInitializeTicketInfo(
    AnityGraphicsDevice* device, uint64_t ticketId,
    AnityGraphicsVFXInitializeTicketInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityGraphics_CompleteVFXInitializeKernels(
    AnityGraphicsDevice* device, uint64_t ticketId);
ANITY_API AnityResult ANITY_CALL AnityGraphics_CancelVFXInitializeKernels(
    AnityGraphicsDevice* device, uint64_t ticketId);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXInitializeDispatchInfo(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int64_t initializeContextId,
    AnityGraphicsVFXInitializeDispatchInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityGraphics_ReadbackVFXInitializeDispatch(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int64_t initializeContextId, uint8_t* records, int32_t recordCapacity,
    int32_t* outWritten);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXParticleSystemInfo(
    const AnityGraphicsDevice* device, uint64_t effectId, int32_t particleSystemId,
    AnityGraphicsVFXParticleSystemInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityGraphics_ReadbackVFXParticleSystem(
    const AnityGraphicsDevice* device, uint64_t effectId, int32_t particleSystemId,
    uint8_t* records, int32_t recordCapacity, int32_t* outWritten);
ANITY_API AnityResult ANITY_CALL AnityGraphics_ReadbackVFXParticleDeadList(
    const AnityGraphicsDevice* device, uint64_t effectId, int32_t particleSystemId,
    uint32_t* indices, int32_t indexCapacity, int32_t* outWritten);
ANITY_API AnityResult ANITY_CALL AnityGraphics_DispatchVFXUpdateKernels(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXUpdateKernelDesc* kernels, int32_t kernelCount,
    const AnityGraphicsVFXUpdateOperationDesc* operations,
    int32_t operationCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_BeginVFXUpdateKernels(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXUpdateKernelDesc* kernels, int32_t kernelCount,
    const AnityGraphicsVFXUpdateOperationDesc* operations,
    int32_t operationCount, uint64_t* outTicketId);
/* Internal product path: boundsDescs is aligned one-to-one with kernels;
 * effectId == 0 disables pending bounds for that kernel. The public Unity API
 * remains unchanged while the native scheduler can reduce the Update ring
 * output in the same command buffer. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_BeginVFXUpdateKernelsWithBounds(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXUpdateKernelDesc* kernels, int32_t kernelCount,
    const AnityGraphicsVFXUpdateOperationDesc* operations,
    int32_t operationCount,
    const AnityGraphicsVFXBoundsReductionDesc* boundsDescs,
    int32_t boundsCount, uint64_t* outTicketId);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXUpdateTicketInfo(
    AnityGraphicsDevice* device, uint64_t ticketId,
    AnityGraphicsVFXUpdateTicketInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityGraphics_CompleteVFXUpdateKernels(
    AnityGraphicsDevice* device, uint64_t ticketId);
ANITY_API AnityResult ANITY_CALL AnityGraphics_CancelVFXUpdateKernels(
    AnityGraphicsDevice* device, uint64_t ticketId);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXUpdateBackendStats(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t particleSystemId,
    AnityGraphicsVFXUpdateBackendStats* outStats);
ANITY_API AnityResult ANITY_CALL AnityGraphics_SetVFXFailureInjection(
    AnityGraphicsDevice* device, AnityGraphicsVFXFailurePoint failurePoint,
    int32_t failureCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_ReduceVFXParticleBounds(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXBoundsReductionDesc* desc,
    AnityGraphicsVFXBoundsReductionResult* outResult);
ANITY_API AnityResult ANITY_CALL AnityGraphics_BeginVFXFrame(
    AnityGraphicsDevice* device, uint32_t* outFrameIndex);
ANITY_API AnityResult ANITY_CALL AnityGraphics_BeginVFXPlayerLoopFrame(
    AnityGraphicsDevice* device, uint64_t playerLoopToken,
    uint32_t* outFrameIndex, int32_t* outBeganFrame);
ANITY_API AnityResult ANITY_CALL AnityGraphics_BeginVFXCullingFrame(
    AnityGraphicsDevice* device, uint64_t playerLoopToken,
    const AnityGraphicsVFXCullingBounds* bounds, int32_t boundsCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_SubmitVFXCullingCamera(
    AnityGraphicsDevice* device, uint64_t playerLoopToken,
    const AnityGraphicsVFXCullingCamera* camera);
ANITY_API AnityResult ANITY_CALL AnityGraphics_CompleteVFXCullingFrame(
    AnityGraphicsDevice* device, uint64_t playerLoopToken);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXCullingState(
    const AnityGraphicsDevice* device, uint64_t effectId,
    AnityGraphicsVFXCullingState* outState);
ANITY_API AnityResult ANITY_CALL AnityGraphics_SetVFXPlanarOutputs(
    AnityGraphicsDevice* device, uint64_t effectId,
    const AnityGraphicsVFXPlanarOutputDesc* outputs, int32_t outputCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXPlanarOutputCount(
    const AnityGraphicsDevice* device, uint64_t effectId,
    int32_t* outCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_DrawVFXPlanarOutputs(
    AnityGraphicsDevice* device, uint64_t effectId,
    const AnityGraphicsVFXPlanarCameraDesc* camera,
    AnityGraphicsVFXPlanarDrawInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityGraphics_DrawVFXPlanarCamera(
    AnityGraphicsDevice* device,
    const AnityGraphicsVFXPlanarCameraBatchDesc* camera,
    const AnityGraphicsVFXPlanarEffectDesc* effects, int32_t effectCount,
    AnityGraphicsVFXPlanarCameraDrawInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXPlanarSubmissionStats(
    AnityGraphicsDevice* device,
    AnityGraphicsVFXPlanarSubmissionStats* outStats);
ANITY_API AnityResult ANITY_CALL AnityGraphics_WaitForVFXPlanarSubmissions(
    AnityGraphicsDevice* device, uint64_t throughSubmissionId,
    int32_t timeoutMilliseconds);
ANITY_API AnityResult ANITY_CALL AnityGraphics_PrepareVFXEffectFrame(
    AnityGraphicsDevice* device, uint64_t effectId, uint32_t frameIndex,
    float gameDeltaTime, float playRate, float fixedTimeStep,
    float maxDeltaTime, int32_t paused,
    AnityGraphicsVFXFrameState* outState);
ANITY_API AnityResult ANITY_CALL AnityGraphics_PrepareVFXEffectManualFrame(
    AnityGraphicsDevice* device, uint64_t effectId, uint32_t frameIndex,
    float stepDeltaTime, AnityGraphicsVFXFrameState* outState);
ANITY_API AnityResult ANITY_CALL AnityGraphics_CommitVFXEffectFrame(
    AnityGraphicsDevice* device, uint64_t effectId, uint32_t frameIndex,
    AnityGraphicsVFXFrameState* outState);
ANITY_API AnityResult ANITY_CALL AnityGraphics_AbortVFXEffectFrame(
    AnityGraphicsDevice* device, uint64_t effectId, uint32_t frameIndex);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXEffectFrameState(
    const AnityGraphicsDevice* device, uint64_t effectId,
    AnityGraphicsVFXFrameState* outState);
ANITY_API AnityResult ANITY_CALL AnityGraphics_ResetVFXEffectFrameState(
    AnityGraphicsDevice* device, uint64_t effectId);
ANITY_API AnityResult ANITY_CALL AnityGraphics_SetVFXSpawnerPrograms(
    AnityGraphicsDevice* device, uint64_t effectId,
    const AnityGraphicsVFXSpawnerProgramDesc* programs, int32_t programCount,
    const AnityGraphicsVFXSpawnerBlockDesc* blocks, int32_t blockCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_SetVFXSpawnerEventRecordDefaults(
    AnityGraphicsDevice* device, uint64_t effectId, int64_t contextId,
    const uint8_t* record, int32_t recordByteCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_ControlVFXSpawner(
    AnityGraphicsDevice* device, uint64_t effectId, int64_t contextId,
    int32_t play, uint32_t seed, int32_t resetSeed);
ANITY_API AnityResult ANITY_CALL AnityGraphics_ControlVFXSpawnerWithCallbacks(
    AnityGraphicsDevice* device, uint64_t effectId, int64_t contextId,
    int32_t play, uint32_t seed, int32_t resetSeed,
    AnityGraphicsVFXSpawnerCallback callback, void* userData);
ANITY_API AnityResult ANITY_CALL AnityGraphics_TickVFXSpawners(
    AnityGraphicsDevice* device, uint64_t effectId, float deltaTime,
    AnityGraphicsVFXSpawnerState* states, int32_t stateCapacity,
    int32_t* outWritten);
ANITY_API AnityResult ANITY_CALL AnityGraphics_TickVFXSpawnersWithCallbacks(
    AnityGraphicsDevice* device, uint64_t effectId, float deltaTime,
    AnityGraphicsVFXSpawnerState* states, int32_t stateCapacity,
    int32_t* outWritten, AnityGraphicsVFXSpawnerCallback callback,
    void* userData);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXSpawnerState(
    const AnityGraphicsDevice* device, uint64_t effectId, int64_t contextId,
    AnityGraphicsVFXSpawnerState* outState);
ANITY_API AnityResult ANITY_CALL AnityGraphics_ReadVFXSpawnerEventRecord(
    const AnityGraphicsDevice* device, uint64_t effectId, int64_t contextId,
    uint8_t* record, int32_t recordCapacity, int32_t* outWritten);
ANITY_API AnityResult ANITY_CALL AnityGraphics_ClearVFXEffectState(
    AnityGraphicsDevice* device, uint64_t effectId);
ANITY_API AnityResult ANITY_CALL AnityGraphics_EnqueueVFXOutputEventRecords(
    AnityGraphicsDevice* device, const AnityGraphicsVFXEventUploadDesc* desc,
    const uint8_t* records, int32_t byteCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_GetVFXOutputEventCount(
    const AnityGraphicsDevice* device, uint64_t effectId, int32_t* outCount);
ANITY_API AnityResult ANITY_CALL AnityGraphics_PeekVFXOutputEventInfo(
    const AnityGraphicsDevice* device, uint64_t effectId,
    AnityGraphicsVFXEventUploadInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityGraphics_DequeueVFXOutputEventRecords(
    AnityGraphicsDevice* device, uint64_t effectId, uint64_t expectedSequence,
    uint8_t* records, int32_t recordCapacity, int32_t* outWritten);

ANITY_API AnityResult ANITY_CALL AnityGraphics_Resize(
    AnityGraphicsDevice* device, int32_t width, int32_t height);

ANITY_API int32_t ANITY_CALL AnityGraphics_SupportsHDR(const AnityGraphicsDevice* device);

ANITY_API AnityGraphicsDeviceType ANITY_CALL AnityGraphics_GetDefaultDeviceType(
    AnityPlatform platform);

/* --- Swapchain (Metal / Vulkan / D3D / headless) --- */
typedef struct AnitySwapchainDesc {
  int32_t width;
  int32_t height;
  int32_t imageCount; /* preferred buffer count, 0 = default 2 */
  int32_t vsync;
  int32_t hdr;
  void* nativeWindow; /* HWND / ANativeWindow / CAMetalLayer* / nullptr = headless */
} AnitySwapchainDesc;

typedef struct AnitySwapchain AnitySwapchain;

ANITY_API AnityResult ANITY_CALL AnityGraphics_CreateSwapchain(
    AnityGraphicsDevice* device,
    const AnitySwapchainDesc* desc,
    AnitySwapchain** outSwapchain);

ANITY_API void ANITY_CALL AnityGraphics_DestroySwapchain(AnitySwapchain* swapchain);

ANITY_API AnityResult ANITY_CALL AnityGraphics_AcquireNextImage(
    AnitySwapchain* swapchain, int32_t* outImageIndex);

ANITY_API AnityResult ANITY_CALL AnityGraphics_PresentSwapchain(AnitySwapchain* swapchain);

ANITY_API int32_t ANITY_CALL AnityGraphics_GetSwapchainImageCount(const AnitySwapchain* swapchain);
ANITY_API int32_t ANITY_CALL AnityGraphics_GetSwapchainWidth(const AnitySwapchain* swapchain);
ANITY_API int32_t ANITY_CALL AnityGraphics_GetSwapchainHeight(const AnitySwapchain* swapchain);
ANITY_API int32_t ANITY_CALL AnityGraphics_IsSwapchainHeadless(const AnitySwapchain* swapchain);
ANITY_API int32_t ANITY_CALL AnityGraphics_GetSwapchainPresentCount(const AnitySwapchain* swapchain);
/* Headless/test readback. Writes tightly packed RGBA8 rows, top-to-bottom. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_ReadbackSwapchainRGBA8(
    AnitySwapchain* swapchain, uint8_t* pixels, int32_t pixelCapacity,
    int32_t* outWritten);
ANITY_API AnityResult ANITY_CALL AnityGraphics_ReadbackSwapchainToneMappedRGBA8(
    AnitySwapchain* swapchain, uint8_t* pixels, int32_t pixelCapacity,
    int32_t* outWritten);
/* Applies the supplied URP HDR grade to a headless/native HDR swapchain. */
ANITY_API AnityResult ANITY_CALL AnityGraphics_ProcessSwapchainHDR(
    AnitySwapchain* swapchain, const AnityHDRColorGrade* grade);
/* 1 if backend created real VkSurface/CAMetalLayer (may still be offscreen) */
ANITY_API int32_t ANITY_CALL AnityGraphics_SwapchainHasNativeSurface(const AnitySwapchain* swapchain);
/* Backend tag: 0=unknown/headless software, 1=Vulkan, 2=Metal, 3=D3D11 */
ANITY_API int32_t ANITY_CALL AnityGraphics_GetSwapchainBackendKind(const AnitySwapchain* swapchain);

/*
 * Vulkan surface kind for active swapchain:
 * 0=none/headless, 1=Win32 HWND, 2=Android ANativeWindow, 3=X11, 4=Wayland
 */
ANITY_API int32_t ANITY_CALL AnityGraphics_GetSwapchainSurfaceKind(const AnitySwapchain* swapchain);

/*
 * Compile-time Vulkan surface platform mask:
 * bit0=Win32, bit1=Android, bit2=X11, bit3=Wayland
 */
ANITY_API int32_t ANITY_CALL AnityGraphics_Vulkan_GetSupportedSurfaceMask(void);

/* Packing for X11: pass as nativeWindow to CreateSwapchain on Linux. */
typedef struct AnityX11NativeWindow {
  void* display;       /* Display* */
  unsigned long window; /* Window XID */
} AnityX11NativeWindow;

/* Packing for Wayland: pass as nativeWindow to CreateSwapchain. */
typedef struct AnityWaylandNativeWindow {
  void* display; /* wl_display* */
  void* surface; /* wl_surface* */
} AnityWaylandNativeWindow;

#ifdef __cplusplus
}
#endif
