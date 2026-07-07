# PLAN

目标（规范）：
- 每次代码落地后，必须先更新本文件，记录本次完成项并给出下一次要做的 1-3 项。
- 统一优先保证 API 签名兼容性（Unity Pro 风格），行为逐步补齐。
- 优先补齐高频迁移阻塞点：`Build`、`PackageManager`、`Editor`、`Runtime` 核心运行时 API。

## 2026-07-07（本次）

### 已完成
- 修复 `UnityEditor.Compilation.AssemblyBuilder` 重复属性定义问题，并补充额外编译相关可选字段。
- 新增并落地兼容 API 壳：
  - `UnityEditor.PlayerSettings`
  - `UnityEditor.SettingsProvider`
  - `UnityEditorInternal.InternalEditorUtility`
  - `UnityEditor.PackageManager.Client` 与 `PackageInfo/Requests`
- 第三波完成：Unity Pro 2022 Full Compat
  - Build Callbacks: `IPreprocessBuildWithReport`, `IPostprocessBuildWithReport`, `IProcessSceneWithReport`, `IOrderedCallback`
  - BuildReporting: `BuildFile`, `BuildStepMessage`, expanded `BuildSummary` with TimeSpan and buildGuid
  - SceneView: 7+ `LookAt` overloads, `orthographic` property, `duringSceneGui` event
  - Handles: `DrawArc`, `DrawCone`, `DrawDottedLine`, `Disc`, `RadiusHandle`, and 15+ new methods
  - PlayerSettings: `Windows` nested class, `StandaloneBuildSubtarget`, per-platform icons
  - PackageManager: `Request.completed` event, `GetEnumerator()`, `EmbedRequest`, `UpdateRequest`
  - BuildPipeline: Expanded enums with PS5, Xbox, Nintendo Switch, additional BuildOptions
- 第四波完成：Runtime & Physics Deep Dive
  - Physics: BoxCast, CapsuleCast, OverlapSphereNonAlloc, OverlapBoxNonAlloc, ComputePenetration, ClosestPoint, SyncTransforms
  - Physics2D: BoxCast, CapsuleCast, OverlapCircleNonAlloc, OverlapPointNonAlloc, GetRayIntersection
  - LayerMask: NameToLayer, LayerToName, GetMask
  - Matrix4x4: inverse, operator *, Perspective, Transpose, Determinant, zero
  - AnimationCurve: AddKey, MoveKey, RemoveKey, SmoothTangents, SetKeys
  - Texture2D: TextureFormat enum, LoadImage, EncodeToPNG, EncodeToJPG, GetPixels32, SetPixels32
  - Material: SetVector/GetVector, SetInt/GetInt, SetMatrix/GetMatrix, EnableKeyword/DisableKeyword, IsKeywordEnabled
  - Collider Subtypes: BoxCollider, SphereCollider, CapsuleCollider, MeshCollider
  - Color32: implicit cast FROM Color, Lerp, LerpUnclamped
  - TextAsset: bytes property
  - RenderTexture: RenderTextureFormat enum, active property
- 完善 `.gitignore`，清理子模块构建产物
- 为 `anity-hub` 添加 `.gitattributes` 和 `.editorconfig` 配置 git VCS
- 所有变更已推送到远程仓库

### 下一次要做（优先）
1. `UI` 侧：补齐 `UnityEngine.UI` 命名空间（`Button`, `Text`, `Image`, `Canvas`, `RectTransform`）以及 `UIToolkit` 核心 API。
2. `Editor` 侧：补齐 `AssetDatabase` 深度 API（`LoadAssetByGUID`, `FindAssets` with `SearchFilter`）、`EditorBuildSettings` 场景管理、`SerializedProperty` 高级迭代。
3. `Compilation` 侧：补齐 `CompilationPipeline` 完整 API、`AssemblyBuilder` 编译回调。
