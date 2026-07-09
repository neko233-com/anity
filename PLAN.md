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

## 2026-07-08（本次）

### 已完成
- **目录结构平铺**：将 `modules/anity-hub`、`modules/anity-editor`、`modules/anity-lib-core` 移至根目录，删除空的 `modules/` 目录
- **新增 `anity-webgl` 模块**：WebGL 平台支持
  - `WebGLRuntime`：WebGL 环境检测、初始化
  - `WebGLAudio`：音频支持（需用户交互）
  - `WebGLInput`：输入支持（触摸/鼠标/键盘/手柄）
  - `WebGLStorage`：IndexedDB 持久化存储
  - `WebGLUI`：Canvas 缩放、UI 事件处理
  - `WebGLTextMeshPro`：TMP WebGL 支持
  - `WebGLAssetBundle`：AssetBundle URL 加载
  - `WebGLNetworking`：UnityWebRequest/WebSocket/HTTP 支持
  - `WebGLVideo`：视频播放支持
- **更新 `.gitmodules`**：4 个子模块平铺在根目录
- **更新所有 `.csproj` 引用**：路径从 `../anity-lib-core` 改为 `../../anity-lib-core`
- **更新 `README.md`**：反映新的 4 模块平铺结构
- **增强 PrefabUtility**：
  - `InstantiatePrefab` 多重载支持（parent、position、rotation）
  - `ApplyPrefabInstance`/`RevertPrefabInstance` 实现
  - `FindPrefabInstanceRoot`/`IsPropertyOverriddenByPrefabInstance` 方法
  - `RecordPrefabInstancePropertyModifications` 方法
- **增强 AssetPostprocessor**：
  - `OnPostprocessTexture`/`OnPostprocessModel`/`OnPostprocessAnimation` 方法
  - `OnPostprocessAudio`/`OnPostprocessHumanoid`/`OnPostprocessSpeedTree` 方法
  - `OnPostprocessSprite`/`OnPostprocessMaterial`/`OnPostprocessGameObjectWithUserProperties` 方法
  - `OnPostprocessRenderTexture`/`OnPostprocessCubemap`/`OnPostprocessFont` 方法
  - `OnPostprocessLightmap`/`OnPostprocessMesh`/`OnPostprocessAvatar` 方法
  - `OnPostprocessShader` 方法
- **增强 BuildPipeline**：
  - `GetUsedAssets`/`GetDirectDependencies`/`GetAllDependencies` 方法
  - `IsBuildTargetSupported`/`IsSceneInBuildSettings`/`GetBuildTargetGroup` 方法
  - 扩展 `BuildOptions`/`BuildAssetBundleOptions` 枚举
  - 新增 `BuildPlayerOptions` 更多字段
- **新增 Unity 运行时类型**：
  - `AnimationClip`/`AnimationEvent`/`AnimatorStateInfo`/`AnimatorClipInfo`
  - `AudioClip`/`ShaderCompilerPlatform`
  - `AvatarMask`/`HumanBodyBones`
  - `Cubemap`/`CubemapFace`
  - `LightmapData`/`LightmapSettings`/`LightmapParameters`/`AmbientMode`
  - `Mesh`/`BoneWeight`
  - `Avatar`/`RawAvatar`/`HumanBone`/`SkeletonBone`/`HumanLimit`/`HumanDescription`
- **增强 PackageManager**：
  - `Client`：`List`/`Search`/`Add`/`Remove`/`Embed`/`Update` 完整实现
  - `PackageInfo`：完整属性（author、documentationUrl、changelogUrl、licensesUrl、path、depth、resolvedPath、sourceGitRevision、distribution、registry）
  - `Requests`：新增 `ResolveRequest`/`PackRequest`/`DisposeRequest`/`EmbedAndAddRequest`/`DependencyResolveRequest`
  - `PackageCollection`：新增 `Find`/`Contains` 方法
- 升级所有项目到 .NET 10.0（net10.0）
- 修复所有 .NET 10 编译错误：
  - `Object` 歧义：添加 `GlobalUsings.cs` 全局别名 `global using Object = UnityEngine.Object`
  - `Vector3.Cross`/`Dot` 缺失：补充静态方法
  - `BuildSummary.totalTime` 类型：`long` → `TimeSpan`
  - `PrefabUtility.InstantiatePrefab` 重复签名：移除冗余重载
  - `Undo` 类名冲突：重命名方法为 `PerformUndoAction`
  - `LoadSceneParameters` readonly struct：字段添加 `readonly`
  - `EditorWindow._windowFactories` 类型修正：`Dictionary<string, EditorWindow>` → `Dictionary<string, Func<EditorWindow>>`
  - `RectTransform.anchoredPosition3D` 类型转换：`Vector2` ↔ `Vector3` 显式转换
  - `Physics` 重载签名：补充缺失的 `Raycast(origin, direction, out hitInfo, ...)` 重载
  - `AssetDatabase.FindAssets` 歧义：显式转换为 `string[]?`
  - `VisualElement.Q<T>` 方法：移除 `(T)` 强制转换，改用 `Query<T>().FirstOrDefault()`
  - `VisualElement` UIElements 类型：补充 `IStyle`、`Style`、`ITransform`、`UIElementsTransform` 等
- 新增 IL2CPP/AOT 支持：
  - `[Preserve]`/`[Il2CppSetOption]`/`[AlwaysLinkAssembly]` 属性定义
  - `[assembly: Preserve]` 全程序集标记
- 新增 Roslyn Analyzer 项目 `Anity.Core.Analyzers`：
  - `AotCompatibilityAnalyzer`（ANITY_AOT001）：检测反射 Emit、Activator.CreateInstance 等 AOT 不安全 API
  - `UnityApiCompatibilityAnalyzer`（ANITY_API001/API002）：检测 Unity 命名空间类型兼容性、IL2CPP 安全性
  - 构建成功并生成 NuGet 包
- 新增 `HotUpdateContext`（AssemblyLoadContext 热更运行时）：
  - `LoadAssembly` / `LoadAssemblyFromFile`：从字节或文件加载热更程序集
  - `Unload` / `ReloadAssembly`：卸载与重载
  - `CreateInstance`：从热更程序集创建类型实例
  - 基于 `AssemblyLoadContext(isCollectible: true)` 实现
- 新增 `Il2CppRuntime` 平台检测：
  - `IsIl2Cpp` / `IsIos` / `IsAndroid` / `IsWebGL` / `Platform` 属性
  - `Initialize()` 初始化方法
- 新增 `PlatformConfig` 平台构建配置：
  - 支持 iOS / Android / WebGL / Desktop 各平台默认配置
  - IL2CPP 代码生成、GC、Strip、Metadata 等细粒度选项

### 下一次要做（优先）
1. `PrefabUtility`/`AssetPostprocessor` 深度：补齐 Prefab 实例化、Apply/Revert、Prefab 格式兼容、Asset 后处理器回调。
2. `BuildPipeline` 增强：`BuildAssetBundles`、`BuildPlayer` 完整签名、`BuildReport` 深度字段。
3. `UnityEditor` 扩展：`SettingsProvider`、`PackageManager.Client` 深度、`InternalEditorUtility` 补齐。

## 2026-07-09（本次）

### 已完成
- **切换到 .NET Standard 2.1**：完全对齐 Unity 2022 的 .NET 版本
  - 修改所有项目的目标框架为 netstandard2.1
  - 添加必要的 NuGet 包：System.Text.Json、System.Runtime.Loader、Microsoft.Bcl.HashCode
  - 创建 IsExternalInit polyfill 支持 C# 9+ init 访问器
  - 创建 RequiredMemberAttribute 和 CompilerFeatureRequiredAttribute polyfill 支持 C# 11+ required 成员
- **修复 API 兼容性问题**：
  - 替换 ArgumentNullException.ThrowIfNull、ObjectDisposedException.ThrowIf、ArgumentException.ThrowIfNullOrWhiteSpace 为传统检查方式
  - 修复 StringSplitOptions.TrimEntries 问题
  - 修复 Environment.ProcessPath 问题
  - 修复 AssemblyLoadContext 相关问题
  - 修复 record struct 问题
  - 修复 CanvasScaler.ScaleMode 和 ScreenMatchMode 引用问题
  - 修复项目引用路径问题
- **简化 HotUpdateContext 实现**：
  - 移除 AssemblyLoadContext 依赖，使用更简单的程序集加载方式
  - 保留核心热更新功能：LoadAssembly、Unload、ReloadAssembly、GetAssembly
- **修复所有项目编译错误**：
  - anity-lib-core：核心库编译成功
  - anity-webgl：WebGL 模块编译成功
  - anity-hub：Hub 模块编译成功
  - anity-editor：部分修复，仍需处理 EditorSession 类型缺失问题

### 下一次要做（优先）
1. 完成 Editor.Host 项目的编译修复（添加缺失的 EditorSession 类型）
2. 继续增强 Unity 2022 API 兼容性
3. 实现 WebGL 浏览器互操作功能

## 2026-07-09（本次）

### 已完成
- **切换到 .NET Standard 2.1**：完全对齐 Unity 2022 的 .NET 版本
  - 修改所有项目的目标框架为 netstandard2.1
  - 添加必要的 NuGet 包：System.Text.Json、System.Runtime.Loader、Microsoft.Bcl.HashCode
  - 创建 IsExternalInit polyfill 支持 C# 9+ init 访问器
  - 创建 RequiredMemberAttribute 和 CompilerFeatureRequiredAttribute polyfill 支持 C# 11+ required 成员
- **修复 API 兼容性问题**：
  - 替换 ArgumentNullException.ThrowIfNull、ObjectDisposedException.ThrowIf、ArgumentException.ThrowIfNullOrWhiteSpace 为传统检查方式
  - 修复 StringSplitOptions.TrimEntries 问题
  - 修复 Environment.ProcessPath 问题
  - 修复 AssemblyLoadContext 相关问题
  - 修复 record struct 问题
  - 修复 CanvasScaler.ScaleMode 和 ScreenMatchMode 引用问题
  - 修复项目引用路径问题
- **简化 HotUpdateContext 实现**：
  - 移除 AssemblyLoadContext 依赖，使用更简单的程序集加载方式
  - 保留核心热更新功能：LoadAssembly、Unload、ReloadAssembly、GetAssembly
- **修复所有项目编译错误**：
  - anity-lib-core：核心库编译成功
  - anity-webgl：WebGL 模块编译成功
  - anity-hub：Hub 模块编译成功
  - anity-editor：修复 Editor.Host 项目编译问题
- **修复 Editor.Host 项目**：
  - 将 EditorSession 和 EditorStatus 从 record 转换为 class（.NET Standard 2.1 兼容）
  - 添加 LangVersion 设置到 Editor.Host 项目
  - 修复 EditorWindow.RegisterWindowFactory 方法可见性（internal -> public）
  - 修复 EditorSessionState 类以包含 ToSession 方法
  - 修复构造函数调用以使用位置参数而不是命名参数

### 下一次要做（优先）
1. 继续增强 Unity 2022 API 兼容性
2. 实现 WebGL 浏览器互操作功能
3. 添加更多 Unity 2022 运行时类型

## 2026-07-09（本次 - 双模式切换架构）

### 已完成
- **实现 Unity 官方 DLL 和 Anity 自研库的二选一切换**
  - 创建 `Anity.Core.Unity` 项目：Unity 官方 DLL 引用包装器
  - 修改 `Anity.Core.csproj`：支持条件编译，Unity 模式下排除自研实现
  - 创建切换脚本 `scripts/switch-mode.ps1`：自动检测 Unity 安装并切换模式
- **创建 AB 对照测试框架**
  - 创建 `Anity.AB.Compare.Tests` 测试项目
  - 实现二进制对比测试（AssetBundle 文件头验证）
  - 实现行为空为对比测试（文件读取、并发访问等）
- **Unity DLL 自动检测**
  - 自动检测 Unity Hub 安装路径
  - 优先选择 Unity 2022 LTS 版本
  - 验证 DLL 完整性（必需和推荐 DLL）
- **测试运行脚本**
  - 创建 `scripts/run-compare-tests.ps1`：在两种模式下运行对照测试
  - 支持生成对照报告
  - 自动恢复到 Anity 模式

### 下一次要做（优先）
1. 准备 AssetBundle 测试资源并运行完整对照测试
2. 继续增强 Unity 2022 API 兼容性
3. 实现 WebGL 浏览器互操作功能

## 2026-07-09（本次 - Unity 2022 核心运行时类型）

### 已完成
- **添加 Unity 2022 核心运行时类型**
  - Animator：动画控制器组件，支持 GetFloat/SetFloat、GetBool/SetBool、CrossFade、Play 等
  - AudioSource：音频源组件，支持 Play/Stop/Pause、音量/音调控制、3D 音频
  - Renderer：渲染器基类及派生类型（MeshRenderer、SkinnedMeshRenderer、CanvasRenderer）
  - MeshFilter：网格过滤器组件
  - YieldInstruction：协程支持类型（WaitForSeconds、WaitForEndOfFrame 等）
  - ParticleSystem：粒子系统基础实现（MainModule、EmissionModule、ShapeModule）
  - CharacterController：角色控制器组件
  - Joint 系列：FixedJoint、HingeJoint、SpringJoint、ConfigurableJoint
  - WheelCollider：车辆物理组件
  - Gizmos：调试可视化绘制
  - Gradient：颜色渐变类型
  - PhysicsScene/PhysicsScene2D：物理场景查询
  - TrailRenderer/LineRenderer：轨迹和线渲染器

### 下一次要做（优先）
1. 准备 AssetBundle 测试资源并运行完整对照测试
2. 继续增强 Unity 2022 API 兼容性
3. 实现 WebGL 浏览器互操作功能

## 2026-07-09（本次 - 编译错误修复）

### 已完成
- **修复编译错误**
  - 创建 `ComponentAttributes.cs`：添加 RequireComponent、AddComponentMenu、DisallowMultipleComponent、SerializeField、HideInInspector 等属性
  - 删除重复类型定义：Coroutine、AnimatorStateInfo、AnimatorClipInfo、MeshColliderCookingOptions、CapsuleDirection2D
  - 修复 Mathf.Infinity 使用：替换为 float.PositiveInfinity
  - 修复类型转换错误：AudioSource.priority、WheelCollider.numCornerVertices/numCapVertices
- **编译状态**：所有项目编译成功，0 个错误

### 下一次要做（优先）
1. 准备 AssetBundle 测试资源并运行完整对照测试
2. 继续增强 Unity 2022 API 兼容性
3. 实现 WebGL 浏览器互操作功能

## 2026-07-09（本次 - MonoBehaviour 增强）

### 已完成
- **增强 MonoBehaviour**
  - 添加 `Invoke(Action, float)` 方法
  - 添加 `InvokeRepeating(Action, float, float)` 方法
  - 添加 `CancelInvoke(Action)` 方法
  - 添加 `IsInvoking(Action)` 方法
  - 添加 `StartCoroutine(Func<IEnumerator>)` 方法
  - 添加 `StartCoroutine<T>(Func<T>)` 方法

## 2026-07-09（本次 - Animator/AudioSource 增强）

### 已完成
- **增强 Animator**
  - 添加 `SetLookAtPosition(Vector3)` 方法
  - 添加 `SetLookAtWeight(float)` 系列方法（5个重载）
  - 添加 `GetBoneTransform(HumanBodyBones)` 方法
  - 添加 `SetBoneLocalRotation(HumanBodyBones, Quaternion)` 方法
  - 添加 `HasState(int, int)` 方法
  - 添加 `avatar` 属性
- **增强 AudioSource**
  - 添加 `PlayClipAtPoint(AudioClip, Vector3)` 静态方法
  - 添加 `PlayClipAtPoint(AudioClip, Vector3, float)` 静态方法

## 2026-07-09（本次 - BuildPipeline 和 .asmdef）

### 已完成
- **实现 BuildPipeline 功能**
  - 添加 `CompilationPipeline` 类：支持程序集定义文件处理
  - 添加 `AssemblyDefinition` 类：.asmdef 文件处理
  - 添加 `.asmdef` 文件示例：`Anity.Core.asmdef`
  - 添加 `BuildPipeline` 方法：`GetPlayingPlayerDataPath`、`RebuildAssetBundleDependencies`、`BuildStreams`
  - 添加 `CompilerMessage` 和 `CompilationResult` 类型
  - 添加 `AssembliesType` 和 `RequestScriptCompilationOptions` 枚举
- **.asmdef 支持**
  - 创建 `Anity.Core.asmdef` 文件示例
  - 支持程序集名称、命名空间、引用、平台等配置

## 2026-07-09（本次 - Editor API 检查）

### 已完成
- **检查 Editor API 实现状态**
  - EditorApplication：已实现所有核心方法和属性
  - EditorWindow：已实现所有核心方法和属性
  - EditorUtility：已实现所有核心方法和属性
  - BuildPipeline：已实现所有核心方法和枚举
  - CompilationPipeline：已添加程序集定义支持

### 下一次要做（优先）
1. 继续增强 Unity 2022 API 兼容性
2. 实现 WebGL 浏览器互操作功能

## 2026-07-09（本次 - AssetBundle 测试）

### 已完成
- **准备 AssetBundle 测试资源**
  - 创建 `test.bundle` 测试资源文件
  - 修复测试项目目标框架（net8.0 -> net10.0）
- **运行对照测试**
  - 所有 10 个测试通过
  - 测试覆盖二进制对比和行为对比

### 下一次要做（优先）
1. 继续增强 Unity 2022 API 兼容性
2. 实现 WebGL 浏览器互操作功能
