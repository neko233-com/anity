#pragma once

#include "../anity_core.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct AnityUIVector3 {
  float x, y, z;
} AnityUIVector3;

typedef struct AnityUIVector4 {
  float x, y, z, w;
} AnityUIVector4;

typedef struct AnityUIColor32 {
  uint8_t r, g, b, a;
} AnityUIColor32;

/* Mirrors UnityEngine.UIVertex without relying on a managed struct layout. */
typedef struct AnityUIVertex {
  AnityUIVector3 position;
  AnityUIVector3 normal;
  AnityUIVector4 tangent;
  AnityUIColor32 color;
  AnityUIVector4 uv0;
  AnityUIVector4 uv1;
  AnityUIVector4 uv2;
  AnityUIVector4 uv3;
} AnityUIVertex;

/* GPU upload order used by the native Canvas renderer staging path. */
typedef struct AnityUIPackedVertex {
  AnityUIVector3 position;
  AnityUIColor32 color;
  AnityUIVector4 uv0;
  AnityUIVector4 uv1;
  AnityUIVector4 uv2;
  AnityUIVector4 uv3;
  AnityUIVector3 normal;
  AnityUIVector4 tangent;
} AnityUIPackedVertex;

typedef struct AnityUIBounds {
  AnityUIVector3 min;
  AnityUIVector3 max;
} AnityUIBounds;

typedef struct AnityUIRenderState {
  AnityUIBounds bounds;
  float clipXMin;
  float clipYMin;
  float clipXMax;
  float clipYMax;
  float softnessX;
  float softnessY;
  float colorAlpha;
  float inheritedAlpha;
  int32_t hasGeometry;
  int32_t rectClipping;
  int32_t cullTransparentMesh;
} AnityUIRenderState;

typedef struct AnityUIVisibility {
  float effectiveAlpha;
  float innerClipXMin;
  float innerClipYMin;
  float innerClipXMax;
  float innerClipYMax;
  int32_t visible;
  int32_t clipped;
  int32_t culledByAlpha;
  int32_t culledByClip;
} AnityUIVisibility;

typedef enum AnityUIRenderCommandFlags {
  ANITY_UI_COMMAND_VISIBLE = 1u << 0,
  ANITY_UI_COMMAND_RECT_CLIP = 1u << 1,
  ANITY_UI_COMMAND_MASK = 1u << 2,
  ANITY_UI_COMMAND_POP = 1u << 3
} AnityUIRenderCommandFlags;

typedef struct AnityUIRenderCommandDesc {
  uint64_t rendererId;
  uint64_t materialId;
  uint64_t textureId;
  uint64_t alphaTextureId;
  int32_t sortDepth;
  uint32_t flags;
  float clipXMin;
  float clipYMin;
  float clipXMax;
  float clipYMax;
  float softnessX;
  float softnessY;
  float effectiveAlpha;
} AnityUIRenderCommandDesc;

typedef struct AnityUIBatchInfo {
  uint64_t materialId;
  uint64_t textureId;
  uint64_t alphaTextureId;
  int32_t firstSortDepth;
  int32_t lastSortDepth;
  uint32_t flags;
  int32_t commandCount;
  int32_t vertexCount;
  int32_t indexCount;
} AnityUIBatchInfo;

typedef struct AnityUICanvasStats {
  uint64_t frameId;
  uint64_t generation;
  int32_t commandCount;
  int32_t visibleCommandCount;
  int32_t batchCount;
  int32_t vertexCount;
  int32_t indexCount;
} AnityUICanvasStats;

typedef struct AnityUICanvas AnityUICanvas;

ANITY_API AnityResult ANITY_CALL AnityUIRenderer_PackVertices(
    const AnityUIVertex* vertices,
    int32_t vertexCount,
    AnityUIPackedVertex* packedVertices,
    int32_t packedCapacity,
    int32_t* outWritten,
    AnityUIBounds* outBounds);

ANITY_API AnityResult ANITY_CALL AnityUIRenderer_BuildQuadIndices(
    int32_t vertexCount,
    uint32_t* indices,
    int32_t indexCapacity,
    int32_t* outWritten);

ANITY_API AnityResult ANITY_CALL AnityUIRenderer_EvaluateVisibility(
    const AnityUIRenderState* state,
    AnityUIVisibility* outVisibility);

ANITY_API AnityResult ANITY_CALL AnityUICanvas_Create(AnityUICanvas** outCanvas);
ANITY_API void ANITY_CALL AnityUICanvas_Destroy(AnityUICanvas* canvas);
ANITY_API AnityResult ANITY_CALL AnityUICanvas_BeginFrame(
    AnityUICanvas* canvas, uint64_t frameId);
ANITY_API AnityResult ANITY_CALL AnityUICanvas_Clear(AnityUICanvas* canvas);
ANITY_API AnityResult ANITY_CALL AnityUICanvas_UpsertCommand(
    AnityUICanvas* canvas,
    const AnityUIRenderCommandDesc* desc,
    const AnityUIPackedVertex* vertices,
    int32_t vertexCount,
    const uint32_t* indices,
    int32_t indexCount);
ANITY_API AnityResult ANITY_CALL AnityUICanvas_RemoveCommand(
    AnityUICanvas* canvas, uint64_t rendererId);
ANITY_API AnityResult ANITY_CALL AnityUICanvas_BuildBatches(AnityUICanvas* canvas);
ANITY_API AnityResult ANITY_CALL AnityUICanvas_GetStats(
    const AnityUICanvas* canvas, AnityUICanvasStats* outStats);
ANITY_API AnityResult ANITY_CALL AnityUICanvas_GetBatchInfo(
    const AnityUICanvas* canvas, int32_t batchIndex, AnityUIBatchInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityUICanvas_CopyBatchVertices(
    const AnityUICanvas* canvas,
    int32_t batchIndex,
    AnityUIPackedVertex* vertices,
    int32_t vertexCapacity,
    int32_t* outWritten);
ANITY_API AnityResult ANITY_CALL AnityUICanvas_CopyBatchIndices(
    const AnityUICanvas* canvas,
    int32_t batchIndex,
    uint32_t* indices,
    int32_t indexCapacity,
    int32_t* outWritten);

#ifdef __cplusplus
}
#endif
