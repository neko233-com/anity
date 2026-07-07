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

- 第五波完成：UI & Editor Deep Dive
  - UnityEngine.UI: Canvas, RectTransform, Graphic, MaskableGraphic, Text, Image, Button, Selectable, CanvasGroup
  - UIToolkit: VisualElement, Label, Button, TextField, ListView, ScrollView, Toggle, Slider, VisualTreeAsset, StyleSheet
  - AssetDatabase: LoadAssetByGUID, FindAssets(Type), GetMainObjectAtGUID, GetSubObjectsAtGUID, IsOpenForEdit, GetAvailableExtensions
  - EditorBuildSettings: EditorBuildSettingsScene, scenes, AddScene, RemoveScene, MoveScene, GetSceneByPath/GUID
  - SerializedProperty: enumValueIndex, managedReferenceValue, Copy, Next, NextVisible, ClearArray, depth, displayName
  - CompilationPipeline: CompilationResult, AssemblyDefinition, GetAssemblyNames, GetAllAssemblyDefinitions, RequestScriptCompilation
  - AssemblyBuilder: compilationFinished event, References, DefineConstraints, OutputPath, PlatformArchitecture

### 下一次要做（优先）
1. `PrefabUtility`/`AssetPostprocessor` 深度：补齐 Prefab 实例化、Apply/Revert、Prefab 格式兼容、Asset 后处理器回调。
2. `BuildPipeline` 增强：`BuildAssetBundles`、`BuildPlayer` 完整签名、`BuildReport` 深度字段。
3. `UnityEditor` 扩展：`SettingsProvider`、`PackageManager.Client` 深度、`InternalEditorUtility` 补齐。
