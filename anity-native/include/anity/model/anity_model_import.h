#pragma once
#include "../anity_core.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct AnityModelScene AnityModelScene;

typedef struct AnityModelImportOptions {
  float globalScale;
  uint8_t useFileUnits;
  uint8_t importAnimation;
  uint8_t generateMissingNormals;
  uint8_t importBlendShapes;
  int32_t maxBonesPerVertex;
  float minBoneWeight;
  uint8_t resampleCurves;
} AnityModelImportOptions;

typedef struct AnityModelSceneInfo {
  int32_t nodeCount;
  int32_t meshCount;
  int32_t clipCount;
  float fileScale;
  float frameRate;
} AnityModelSceneInfo;

typedef struct AnityModelNodeInfo {
  const char* name;
  int32_t parentIndex;
  int32_t meshIndex;
  float positionX, positionY, positionZ;
  float rotationX, rotationY, rotationZ, rotationW;
  float scaleX, scaleY, scaleZ;
} AnityModelNodeInfo;

typedef struct AnityModelVertex {
  float positionX, positionY, positionZ;
  float normalX, normalY, normalZ;
  float tangentX, tangentY, tangentZ, tangentW;
  float uvX, uvY;
} AnityModelVertex;

typedef struct AnityModelMeshInfo {
  const char* name;
  int32_t vertexCount;
  int32_t indexCount;
  int32_t subMeshCount;
  int32_t boneCount;
  int32_t skinWeightCount;
  int32_t blendShapeCount;
} AnityModelMeshInfo;

typedef struct AnityModelBoneInfo {
  const char* name;
  int32_t nodeIndex;
  float m00, m01, m02, m03;
  float m10, m11, m12, m13;
  float m20, m21, m22, m23;
  float m30, m31, m32, m33;
} AnityModelBoneInfo;

typedef struct AnityModelSkinVertexInfo {
  int32_t weightStart;
  int32_t weightCount;
} AnityModelSkinVertexInfo;

typedef struct AnityModelBoneWeight {
  int32_t boneIndex;
  float weight;
} AnityModelBoneWeight;

typedef struct AnityModelBlendShapeInfo {
  const char* name;
  int32_t frameCount;
} AnityModelBlendShapeInfo;

typedef struct AnityModelBlendShapeFrameInfo {
  float weight;
} AnityModelBlendShapeFrameInfo;

typedef struct AnityModelBlendShapeDelta {
  float positionX, positionY, positionZ;
  float normalX, normalY, normalZ;
} AnityModelBlendShapeDelta;

typedef struct AnityModelSubMeshInfo {
  int32_t indexStart;
  int32_t indexCount;
  int32_t materialIndex;
} AnityModelSubMeshInfo;

typedef struct AnityModelClipInfo {
  const char* name;
  float duration;
  float frameRate;
  int32_t trackCount;
  int32_t blendShapeTrackCount;
  float firstFrame;
  float lastFrame;
} AnityModelClipInfo;

typedef struct AnityModelTrackInfo {
  int32_t nodeIndex;
  int32_t positionKeyCount;
  int32_t rotationKeyCount;
  int32_t scaleKeyCount;
} AnityModelTrackInfo;

typedef struct AnityModelVectorKey {
  float time;
  float x, y, z;
} AnityModelVectorKey;

typedef struct AnityModelQuaternionKey {
  float time;
  float x, y, z, w;
} AnityModelQuaternionKey;

typedef struct AnityModelBlendShapeTrackInfo {
  int32_t nodeIndex;
  const char* name;
  int32_t keyCount;
} AnityModelBlendShapeTrackInfo;

typedef struct AnityModelScalarKey {
  float time;
  float value;
  float inTangent;
  float outTangent;
} AnityModelScalarKey;

ANITY_API AnityResult ANITY_CALL AnityModel_LoadFile(
    const char* path, const AnityModelImportOptions* options,
    AnityModelScene** outScene, char* errorBuffer, int32_t errorBufferSize);
ANITY_API void ANITY_CALL AnityModel_FreeScene(AnityModelScene* scene);
ANITY_API AnityResult ANITY_CALL AnityModel_GetSceneInfo(const AnityModelScene* scene, AnityModelSceneInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityModel_GetNodeInfo(const AnityModelScene* scene, int32_t nodeIndex, AnityModelNodeInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityModel_GetMeshInfo(const AnityModelScene* scene, int32_t meshIndex, AnityModelMeshInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityModel_CopyMeshVertices(const AnityModelScene* scene, int32_t meshIndex, AnityModelVertex* vertices, int32_t capacity);
ANITY_API AnityResult ANITY_CALL AnityModel_CopyMeshIndices(const AnityModelScene* scene, int32_t meshIndex, uint32_t* indices, int32_t capacity);
ANITY_API AnityResult ANITY_CALL AnityModel_GetSubMeshInfo(const AnityModelScene* scene, int32_t meshIndex, int32_t subMeshIndex, AnityModelSubMeshInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityModel_GetBoneInfo(const AnityModelScene* scene, int32_t meshIndex, int32_t boneIndex, AnityModelBoneInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityModel_CopySkinVertices(const AnityModelScene* scene, int32_t meshIndex, AnityModelSkinVertexInfo* vertices, int32_t capacity);
ANITY_API AnityResult ANITY_CALL AnityModel_CopyBoneWeights(const AnityModelScene* scene, int32_t meshIndex, AnityModelBoneWeight* weights, int32_t capacity);
ANITY_API AnityResult ANITY_CALL AnityModel_GetBlendShapeInfo(const AnityModelScene* scene, int32_t meshIndex, int32_t shapeIndex, AnityModelBlendShapeInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityModel_GetBlendShapeFrameInfo(const AnityModelScene* scene, int32_t meshIndex, int32_t shapeIndex, int32_t frameIndex, AnityModelBlendShapeFrameInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityModel_CopyBlendShapeFrameDeltas(const AnityModelScene* scene, int32_t meshIndex, int32_t shapeIndex, int32_t frameIndex, AnityModelBlendShapeDelta* deltas, int32_t capacity);
ANITY_API AnityResult ANITY_CALL AnityModel_GetClipInfo(const AnityModelScene* scene, int32_t clipIndex, AnityModelClipInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityModel_GetTrackInfo(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelTrackInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityModel_CopyPositionKeys(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelVectorKey* keys, int32_t capacity);
ANITY_API AnityResult ANITY_CALL AnityModel_CopyRotationKeys(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelQuaternionKey* keys, int32_t capacity);
ANITY_API AnityResult ANITY_CALL AnityModel_CopyScaleKeys(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelVectorKey* keys, int32_t capacity);
ANITY_API AnityResult ANITY_CALL AnityModel_GetBlendShapeTrackInfo(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelBlendShapeTrackInfo* outInfo);
ANITY_API AnityResult ANITY_CALL AnityModel_CopyBlendShapeKeys(const AnityModelScene* scene, int32_t clipIndex, int32_t trackIndex, AnityModelScalarKey* keys, int32_t capacity);

#ifdef __cplusplus
}
#endif
