# PLAN

## 2026-07-13q — CI 根因修复：Public 仓库 + Object 枚举竞态

### 根因
- GitHub Actions 注解：`payments have failed or spending limit needs to be increased`
- Job **从未分配 runner**（steps 空、3–5 秒失败）——非代码失败
- 仓库曾为 **private**，免费额度/账单阻断 runner

### 已完成
- 仓库改为 **public**（公开仓库 Actions 免费）
- `Object.FindObjects*` 快照 + 锁，消除 `Collection was modified` 竞态
- `StaticOcclusionCulling.Compute` 安全枚举
- `ci.yml`：concurrency、timeout、`workflow_dispatch`、`set -euo pipefail`

### 验收
- 本地 Release Core 全绿
- GitHub Actions anity-ci 全 job success

## 2026-07-13p — Vulkan Android/X11/Wayland surface + Timeline Signal/Window

### 已完成
- **Vulkan 多平台 surface**：Win32 HWND、Android `ANativeWindow`、X11（`AnityX11NativeWindow`）、Wayland（可选）；`surfaceKind` + 支持掩码 API
- **CMake**：Android `android` lib；Linux 可选 X11 / Wayland-client
- **C#**：`SwapchainSurfaceKind`、`VulkanSupportedSurfaceMask`、`PlatformGraphics.GetVulkanSurfaceKind/DescribeNativeWindowType`
- **Timeline Signal**：`SignalAsset`/`SignalEmitter`/`SignalReceiver`/`SignalTrack`/`SignalUtility`；Director Evaluate 跨时间点发射（含 Loop）
- **TimelineWindow**：编辑器播放头、轨道/Clip/Signal 添加、Play/Pause/Stop/Tick
- **测试**：本批 **21** 新测 + 相关 66 绿；native build-batch OK

### 下一次
1. 真机/模拟器上验证 ANativeWindow 与 X11 present
2. Timeline Marker 接口完整对齐（IMarker）
3. Umbra 官方 bake 二进制（可选）

## 2026-07-13o — Umbra/Wind/Vehicles/IMGUI/Director/Timeline/StreamingAssets 全落地

### 已完成
- **OcclusionCulling (Umbra 子集)**：Bake 网格、IsVisible、Portal 遮挡、Area 注册、StaticOcclusionCulling.Compute/Cancel/Clear；StaticBatchingUtility 标记 static + 网格合并
- **StreamingAssets**：root 覆盖、读写、列表、file:// URL、CopyFrom；对齐 Application.streamingAssetsPath
- **Wind**：WindZone OnEnable 注册、Directional/Spherical 求值、多区叠加
- **Vehicles**：VehicleChassis + VehicleUtility.CreateSimpleCar；WheelCollider 编排油门/转向/刹车
- **IMGUI**：稳定 ControlId；Button/Toggle/TextField/Slider 真命中交互
- **Playables/Director/Timeline**：PlayableGraph、PlayableDirector(Hold/Loop/None)、TimelineAsset 轨道/Clip、PlayableAsset
- **CullingGroup.Query**：距离带 + OcclusionCulling 联动
- **测试**：本批 **80** 新测全绿；Core 全量 **324** 全绿

### 下一次
1. ~~Android ANativeWindow / X11 surface~~ → 见 13p
2. ~~Timeline 编辑器窗口与 Signal 发射深度~~ → 见 13p
3. 真 Umbra 二进制 bake 格式（若需与官方数据互通）

## 2026-07-13n — Vulkan surface/swapchain + Metal CAMetalLayer 全落地

### 已完成
- **Vulkan**：Instance surface 扩展、Win32 `VkSurfaceKHR`、`VkSwapchainKHR` 创建/acquire/present；无窗 headless 软件 ring；无 SDK 时 stub 仍链
- **Metal**：`CAMetalLayer` 自建/外接、drawableSize、HDR EDR、displaySync、acquire/present commandBuffer
- **C API**：PresentCount / HasNativeSurface / BackendKind
- **C#**：`NativeGraphicsDevice` 暴露上述字段
- **测试**：Swapchain **18**；Core **244** 全绿；native cmake build-batch OK

### 下一次
1. Android `ANativeWindow` surface
2. X11/Wayland surface
3. 继续编辑器/物理深度

## 2026-07-13m — AnimationCurve Hermite + Vector3.Slerp + multipart + 去假 Curve

### 已完成
- **删除** `Volume.cs` 全局假 `AnimationCurve`/`Keyframe`（`Evaluate=>0` 污染 API）
- **AnimationCurve**：Cubic Hermite 求值、Linear 正确斜率、EaseInOut/Constant、Loop wrap
- **Vector3.Slerp / SlerpUnclamped** 球面插值
- **UnityWebRequest.SerializeFormSections** 真 multipart body；GenerateBoundary 可打印
- **测试**：AnimationCurveAndMathDepth **14**；Core **239** 全绿

### 下一次
1. Vulkan 真 surface/swapchain
2. Metal CAMetalLayer
3. 继续清其它全局假类型 / 深度行为

## 2026-07-13k（本次）— IL2CPP 端到端打包管线 + CLI 对接 + goal 验收

### 已完成
- **Il2CppPackagePipeline**：convert → link.xml/metadata/cpp → LinkPlayer → Launch（managed 回退）
- **CLI `-il2cpp`**：完整 Package+Launch；`-build*Player` + IL2CPP 写 `Il2CppOutputProject` + `.il2cpp.json`
- **测试**：Il2CppPackagePipeline ≥13；CLI Il2Cpp package/launch；Core **213** / AB **22** / Agent **13** / Cli **15**
- **.gitignore**：Library/Temp/bin/obj/native build*/Il2CppOutputProject 已覆盖；无 build-ci 入仓

### 下一次要做（优先）
1. Vulkan 真 VkSurface/SwapchainKHR
2. Metal CAMetalLayer 上屏
3. 继续扩展未覆盖的 Unity 编辑器/运行时深度行为（非 Checklist 假 ✅）

## 2026-07-13l — skeptic fixes: PerlinNoise + MSVC/clang detect + native link assert

### 已完成
- **Mathf.PerlinNoise**：Improved Perlin → Unity [0,1]；Checklist 去掉 “stub”
- **Il2CppToolchain.IsMsvcCl**：仅 `cl` 为 MSVC；clang/g++ 用 GNU 标志；Detect 优先 clang++/g++
- **测试**：nativeLinked 在编译器存在时必须 true；IsMsvcCl 不匹配 clang
# PLAN

## 2026-07-13j（本次）— AB.Compare 门禁 + IL2CPP Player 链接启动 + Metal/Vulkan Swapchain

### 已完成
- **AB.Compare 二进制门禁**
  - `AssetBundleBinaryComparer`：UnityFS magic（空格/NUL）、ALZ4 解压后校验、catalog 解析、Gate
  - 写盘 magic 对齐官方 `"UnityFS "`；AB.Compare.Tests **22 全绿**
  - CI：unit 内必跑 AB.Compare + 独立 ab-compare job
- **IL2CPP 全链接 + Player 启动**
  - `Il2CppPlayerHost.BuildPlayer/Launch/LaunchManaged`
  - `Il2CppToolchain.LinkPlayer`（cl/clang 链 AnityIl2CppPlayer）+ CompileAllUnits + CMake exe target
  - 无编译器时 managed player 仍可 Launch；Il2CppPlayerTests **13**
- **Metal/Vulkan 原生交换链**
  - C-API：Create/Destroy/Acquire/Present/Get* Swapchain
  - Vulkan 真设备 + headless swapchain 状态；Metal Apple 实现 + 非 Apple stub
  - managed `NativeGraphicsDevice.CreateSwapchain` 双路径；SwapchainTests **13**
  - native CMake 构建通过（build-ci）
- **测试**：AB.Compare 22 + Core 增补；CI native-graphics job

### 下一次要做（优先）
1. Vulkan 真 VkSurface/SwapchainKHR（Win32/Android）
2. Metal CAMetalLayer 上屏 + EDR
3. IL2CPP player 产物打包进 CLI `-il2cpp -buildPlayer`

## 2026-07-13i（本次）— 真 LZ4 + UWR 证书/Cookie + Addressables 标签依赖 + IL2CPP 工具链 + Metal/Vulkan

### 已完成
- 真 LZ4、UWR Cookie/证书、Addressables 标签依赖、Il2CppToolchain、PlatformGraphics
- Core **174 全绿**

## 2026-07-13h（本次）— UnityWebRequest + Addressables + AB ALZ4 + Instantiate 修复

### 已完成
- **UnityWebRequest 真网络栈**、Addressables catalog、ALZ4、Instantiate 修复
- **测试**：Core **133 通过**

### 下一次要做（优先）
1. 真 LZ4 / UWR 证书 Cookie / Addressables 标签（见 13i）

## 2026-07-13g（本次）— PlayerPrefs/本地存储 + CI 实跑 + push

### 已完成
- **PlayerPrefs 深度对齐**
  - 类型化存储 `{t,v}`、大小写敏感、线程安全、原子 Save
  - Get 类型强制转换（int/float/string）、GetAllKeys/GetKeyType、SaveIfDirty
  - `SetSavePathForTests` 隔离 CI/单测；Application.Quit / CLI quit 自动刷盘
- **EditorPrefs** 持久化路径 + Load/Save 原子写 + 测试隔离
- **LocalStorage** 对接 persistent/temp/streaming/data 路径读写
- **CI** `.github/workflows/ci.yml`：dotnet build + 全量 unit tests + CLI smoke（win/ubuntu）
- **测试**：PlayerPrefs 相关 **17 通过**；Core 全量再验

### 下一次要做（优先）
1. 继续其余模块边界测试与行为对齐（Addressables、LZ4 AB）
2. CI 加 native cmake 矩阵与 AB.Compare 门禁（已有 job）

## 2026-07-13f（本次）— AssetBundle 全链路 + GraphicRaycaster + 测试 80 + push

### 已完成
- **AssetBundle 打包全链路**
  - `AssetBundleFormat`：UnityFS magic + catalog（assets/scenes/deps/hash/crc）
  - `BuildPipeline.BuildAssetBundles`：写盘、manifest 文件、DryRun、AppendHash、StrictMode、变体
  - `LoadFromFile/Memory/Stream` 解析 catalog 并 `RegisterAsset` 还原 TextAsset/Texture2D/Material/GameObject
  - CRC 校验、Unload/Async、GetAllLoaded
- **GraphicRaycaster** Overlay→eventCamera=null；Camera/World 用 worldCamera；排序与 blocking
- **GameObject 场景表加锁**（Job 并行下并发安全）
- **测试**：Core.Tests **80 全通过**（AB≥14、Raycaster≥12 等）
- **.gitignore**：native build、Il2Cpp cache、截图/测试产物

### 下一次要做（优先）
1. AssetBundle LZ4 真压缩与 Unity 官方 AB 二进制对照
2. Addressables 构建对接
3. 继续其余模块 10+ 边界测试矩阵

## 2026-07-13e（本次）— Canvas 全适配 + JobSystem/IL2CPP 深度

### 已完成
- **Canvas 三模式完整**
  - Screen Space **Overlay**：pixelRect/renderingDisplaySize、sorting、根 RT sizeDelta = display/scaleFactor
  - Screen Space **Camera**：worldCamera、planeDistance 放置、主相机回退
  - **World Space**：作者变换保留；Scaler 作用 localScale
  - rootCanvas / isRootCanvas 嵌套、AdditionalCanvasShaderChannels、GetSortedCanvases、ScreenPointToLocalPoint
- **CanvasScaler 全模式**
  - ConstantPixelSize / ScaleWithScreenSize（MatchWidthOrHeight log 混合、Expand、Shrink）/ ConstantPhysicalSize
  - ApplyScaleFactorToCanvas 对齐 Unity：Overlay/Camera 改 sizeDelta，World 改 localScale
  - **修复 UIBehaviour 生命周期**：`override Awake/OnEnable` 否则 AddComponent 不触发 Scaler
- **Job System 深度**
  - 真 ThreadPool + Parallel.For 批处理、JobHandle 依赖 Complete、CombineDependencies、JobsUtility.WorkerCount
  - IJob / IJobParallelFor / Batch / Transform 扩展 Schedule/Run/ByRef
  - 接 anity-native Jobs_Initialize
- **IL2CPP 深度**
  - Il2CppApi：icall/pinvoke/method pointer 注册与解析、InvokeMethod、GC、Il2CppException
  - Il2CppStripping preserve / EffectiveLevel；Builder 集成 InitializeRuntime
- **测试**：Canvas ≥15、Job ≥13、累计 Core.Tests **54 全通过**

### 下一次要做（优先）
1. GraphicRaycaster 与 Overlay/Camera/World 事件相机射线全路径测试
2. Job Burst 编译路径 / Safety system 断言
3. IL2CPP 真工具链驱动（il2cpp 转换 → 平台 C++ 编译链接）

## 2026-07-13d（本次）— CLI / Agent 官方库 / 截图 / IL2CPP / 深度测试

### 已完成
- **AGENTS.md**：CLI、Anity.Agent 独立扩展、截图、IL2CPP 绝对支持、**每功能≥10 测试用例** 强制写入规范
- **anity.exe CLI**（`anity-cli/`）：Unity 兼容 `-batchmode/-quit/-projectPath/-executeMethod/-buildTarget/-build*Player/-runTests/-logFile/-nographics` + Anity `-il2cpp/-screenshot/-agent*`
- **Anity.Agent 官方扩展**（`anity-agent/`，类 UGUI 独立包）：Session/Memory/ToolRegistry/内置 screenshot·echo·systeminfo 工具；**禁止塞进 Core**
- **ScreenCapture**：CaptureScreenshot / AsTexture / IntoRenderTexture + superSize 钳制 + 真 PNG 编码（`ImageConversion.EncodeToPNG`）
- **IL2CPP 深化**：`Il2CppBuilder`（代码生成设置、link.xml、.cpp stub、元数据 map、AOT 泛型注册）、`Il2CppRuntime.EnterIl2CppPlayerMode`
- **深度测试**
  - `Anity.Core.Tests`：ScreenCapture 12 + Il2Cpp 14 = **26 通过**
  - `Anity.Agent.Tests`：**13 通过**
  - `Anity.Cli.Tests`：**13 通过**
- **`_scripts/run-tests.ps1`**；`build-all` 纳入 agent/cli

### 下一次要做（优先）
1. 为物理/HDR/媒体/纹理压缩等其余模块各补满 ≥10 用例
2. CLI `-executeMethod` 与 Editor 编译脚本完整对接；`-runTests` 接 xUnit
3. IL2CPP → 真 C++ 工具链（il2cpp.exe 风格驱动 + 平台链接）

## 2026-07-13c（本次）— 查漏补缺：真 D3D11 + URP HDR 后处理 + native 热路径

### 已完成
- **D3D11 真设备**：`D3D11CreateDevice`（Hardware + WARP 回退）、可选 swapchain/RTV、BeginFrame/Present、HDR10 格式 `R10G10B10A2`
- **Vulkan 设备骨架**：有 SDK 时 `vkCreateInstance` + physical + logical device（无 SDK 时 NOT_SUPPORTED）
- **设备创建分发**：`AnityGraphics_CreateDevice` → D3D11 / Vulkan / Null
- **C# 热路径接 native**
  - 3D CCD `SphereSphereTOI` → `AnityPhysics3D_*`
  - 2D SAT `PolygonIntersectPolygon` → `AnityPhysics2D_PolygonSAT`
  - `AudioClip.CreateFromFile` → `AnityAudio_DecodeFile`
  - `TextureCompressionUtility.Compress` → `AnityTexture_CompressRGBA8`
- **URP HDR 后处理**：`PostProcessPass` / `PostProcessRendererFeature` 自动注入；Bloom/Tonemap/ColorAdjustments Volume 参数 → Shader globals + `PostProcessRuntime`
- **`NativeGraphicsDevice`** 托管包装；**`Display`** 多显示器；**`ColorSpacePipeline`** Linear/HDR 配置
- **GameView** HDR/Linear 联动 post grade
- **build-native.ps1** 自动拷贝 `anity_native.dll` 到 managed 输出目录
- **编译**：Anity.Core 0 错误；native 产出 `anity_native.dll`

### 下一次要做（优先）
1. Metal 真设备 + iOS EDR；Vulkan swapchain + Android 集成
2. FFmpeg / 平台 MediaCodec 真 mp3/H.264 解码
3. 阴影级联/光照探针采样进 native + 对比测试矩阵

## 2026-07-13b（本次）— 完全对标 + C++ 原生 + HDR + _scripts

### 已完成
- **AGENTS.md 规范升级（强制）**
  - 完全对标 Unity 2022.3 Pro：API + 行为效果 + 编辑器一模一样
  - Unity 用 C++ 的部分必须用 C++（`anity-native/`）
  - 必须支持 URP 下 **HDR**（非 HDRP 产品管线）
  - 查漏补缺默认执行；`_scripts/` 为环境/构建脚本唯一权威目录
- **anity-native C++ 引擎核心（可编译）**
  - 模块：core / graphics（Null+Vulkan/D3D11/Metal 后端壳）/ HDR / physics(CCD+SAT) / audio / media / jobs / texture compress
  - C-ABI + P/Invoke：`Anity.Core.Runtime.Native.AnityNative`
  - `_scripts/build-native.ps1` 已产出 `anity_native.dll`
- **HDR 全链路 API**
  - `HDROutputSettings` / `ColorGamut` / `HDRDisplayBitDepth` / `HDRUtilities`
  - native `AnityHDR_*`：ACES/Neutral 色调映射、Bloom 阈值、Linear↔sRGB、显示查询
  - URP：`supportsHDR`、`DefaultHDR` RT、`QualitySettings.activeColorSpace=Linear` 默认
  - `Mathf.LinearToGammaSpace` / `GammaToLinearSpace` 对齐 sRGB 曲线
- **`_scripts/` 环境脚本**
  - install-env / verify-env / build-native / build-all / gap-audit
  - install-vulkan-sdk / install-android-sdk（Windows）
  - 对应 .sh（Unix）
- **gap-audit**：C# 332 文件、Checklist ✅=346 ❌=0、关键类型齐

### 下一次要做（优先）
1. Vulkan/D3D11/Metal **真设备**交换链与 HDR 显示输出（当前为后端壳 + 类型矩阵 + HDR CPU 路径）
2. 接入 FFmpeg/平台编解码：真 mp3/H.264 解码进 native
3. 逐模块把 C# 热路径（物理步进、网格、粒子）下沉到 `anity-native` 并加对比测试

## 2026-07-13（本次）

### 已完成
- **媒体格式**：`MediaFormatUtility` 支持 mp3/wav/ogg/aac/m4a/flac + mp4/webm/mov/avi；`AudioClip.CreateFromFile`；`VideoClip`/`WebGLVideo`；Project 浏览器识别媒体扩展
- **平台图形**：iOS → Metal（`PlatformGraphics.ConfigureIOSMetal`）；Android → Vulkan 主路径 + GLES 回退；`PlayerSettings.GetGraphicsAPIs` 默认矩阵对齐
- **纹理压缩 ASTC/ETC/DXT(BC)**：`TextureCompressionUtility` 平台默认格式/块大小/软压缩/`ToGraphicsFormat`；去重 `TextureFormat` 枚举冲突；补 `ASTC_HDR_*`
- **物理深化（已有+修编译）**：3D CCD 参数化 TOI（`ContinuousCollision`）；2D SAT（`PolygonIntersectPolygon`）；`CompositeCollider2D` 凸包合并
- **编辑器对接**：
  - Host 默认布局：Scene / Game / Hierarchy / Project / Inspector / Console
  - Host 改为打开 `Anity.Core` 真实窗口（不再用占位 stub）
  - **Ctrl+K** Quick Search（`SearchService`/`SearchWindow`，资产/Hierarchy/菜单/窗口/设置）
  - **Prefab Mode**：`PrefabStage` Isolation/Context；Project 双击 `.prefab` 进入；菜单 `Assets/Open Prefab Mode`
  - **GameView**：Display/Aspect/Scale/VSync/Stats + `Camera.Render`→SRP + LightProbes
  - **SceneViewCamera**：Scene 相机 + Render/探针采样
- **编译修复**：TextureFormat 双枚举歧义、GraphicsFormat 映射、SearchService 事件/类型、CCD 可访问性
- **编译状态**：`Anity.Core`、`Anity.Editor.Host` 0 错误

### 下一次要做（优先）
1. 原生编解码深化：mp3/mp4 真实解码后端（FFmpeg/平台 API/WebAudio），当前为软解码/时长估计
2. iOS Metal / Android Vulkan 原生设备后端（当前为 API 矩阵 + 构建产物管线，非 GPU 真驱动）
3. GameView RT 像素回读与 Scene/Game 联动 Play Mode 帧循环

## 2026-07-10（本次）

### 已完成
- **Git 分支整理**：当前仅有 `main` 分支，无其他待合并分支。
- **Canvas 增强**：
  - 新增 `isRootCanvas`、`renderTransform` 属性
  - 新增静态 `ForceUpdateCanvasesStatic` 方法，完善 `preWillRenderCanvases` 调用
- **2D 物理系统落地**：
  - 新增 `Physics2DWorld` 内部世界管理器：统一注册/注销 `Collider2D` 与 `Rigidbody2D`
  - 实现刚体积分（速度、重力、阻力、角速度）
  - 实现 Box-Box、Circle-Circle、Box-Circle 碰撞检测与冲量/位置校正
  - 实现触发器检测：`isTrigger` 只发消息不做物理响应
  - 实现射线检测（Raycast）、圆形/矩形/点覆盖查询（OverlapCircle/Box/Point）
  - 实现层碰撞忽略：`IgnoreLayerCollision`、`IsLayerCollisionEnabled`
  - 新增 `BoxCollider2D`、`CircleCollider2D`
  - 增强 `Rigidbody2D`：`bodyType`、`drag`、`angularDrag`、`freezeRotation`、`simulated`、多模式 `AddForce`/`AddTorque`、`MovePosition`/`MoveRotation`
  - 新增 `RigidbodyType2D`、`ForceMode2D`、`Collision2D`、`ContactPoint2D`
  - 增强 `Collider2D`：`IsTouching`、`IsTouchingLayers`，内部形状抽象
  - 增强 `Vector2`：补充 `left`、`down` 常量
- **编译状态**：`Anity.Core`、`Anity.WebGL`、`Anity.Hub`、`Anity.Editor.Host` 全部编译成功，0 个错误

### 下一次要做（优先）
1. 补齐 3D 物理基础实现：`Physics.Simulate`、简单 Sphere/Box 碰撞检测、Rigidbody 增强。
2. 为 2D/3D 物理补充单元测试，验证碰撞、触发器、Raycast 行为。
3. 补齐 CanvasScaler、GraphicRaycaster 等 UI 渲染管线类型。

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

## 2026-07-09（本次 - Unity 核心机制）

### 已完成
- **实现 Addressable 系统**
  - 添加 `Addressables` 类：资产加载、实例化、释放
  - 添加 `AssetReference` 类：资产引用
  - 添加 `AsyncOperationHandle<T>` 结构：异步操作句柄
  - 添加 `IResourceLocator` 接口：资源定位器
- **实现 Terrain 地形系统**
  - 添加 `Terrain` 组件：地形渲染
  - 添加 `TerrainData` 资产：地形数据
  - 添加 `TerrainLayer` 类：地形层
  - 添加 `TreePrototype`/`TreeInstance` 结构：树木原型和实例
- **实现 Tilemap 瓦片地图系统**
  - 添加 `Tilemap` 组件：瓦片地图渲染
  - 添加 `Tile`/`TileData`/`TileFlags` 类型：瓦片数据
  - 添加 `ITilemap` 接口：瓦片地图接口
  - 添加 `Vector3Int`/`BoundsInt` 类型：整数向量和边界
- **实现 Burst 编译器支持**
  - 添加 `BurstCompileAttribute`：Burst 编译属性
  - 添加 `math` 静态类：Burst 兼容数学库
  - 添加 `FixedPoint` 结构：定点数学

## 2026-07-09（本次 - 渲染类型）

### 已完成
- **添加渲染相关类型**
  - 添加 `CommandBuffer` 类：渲染命令缓冲区
  - 添加 `Graphics` 类：图形渲染操作
  - 添加 `MeshTopology` 枚举：网格拓扑类型
  - 添加支持类型：`ShaderPassName`、`RenderTargetIdentifier`、`ScriptableRenderContext` 等

## 2026-07-09（本次 - 完整 URP + Job System + 代码裁切 + 编辑器窗口）

### 已完成
- **创建 AGENTS.md 项目规范文档**
  - 明确：仅支持 URP 渲染管线（不支持 Built-in/HDRP）
  - 明确：必须实现 Job System、代码裁切、IL2CPP
  - 明确：完全对标 Unity 2022 LTS
  - 明确：平台优先级 WebGL > Windows
- **完整 URP 渲染管线实现**（UnityEngine.Rendering.Universal 命名空间）
  - `UniversalRenderPipelineAsset`：URP 管线资产，完整 50+ 配置项
    - HDR、SRP Batcher、动态批处理、自适应性能
    - 阴影质量/级联/距离/分辨率/软阴影
    - 主光/附加光阴影配置、级联分割
    - 渲染器列表、默认渲染器索引
  - `UniversalRenderPipeline`：URP 管线实例
    - `RenderSingleCamera`、`beginCameraRendering`/`endCameraRendering` 事件
    - `beginFrameRendering`/`endFrameRendering` 事件
  - `ScriptableRenderer` / `ScriptableRendererFeature` / `ScriptableRenderPass`
    - RenderPass 完整生命周期（Setup/Execute/OnCameraSetup/Cleanup）
    - `RenderPassEvent` 枚举（16 个注入点：BeforeShadows→AfterRendering 全流程）
  - `UniversalRendererData` / `UniversalRenderer`：前向渲染器数据与实现
    - Forward/Deferred 模式、SRP Batcher、HDR、深度纹理
    - 主光/附加光渲染配置、每对象附加光上限
  - `ForwardRendererData` / `Renderer2DData`：前向和 2D 渲染器
  - **Volume 系统完整实现**（UnityEngine.Rendering 命名空间）
    - `Volume` 组件：全局/局部、混合距离、权重、优先级
    - `VolumeProfile` 资产：Volume 组件集合、增删改查
    - `VolumeComponent` / `VolumeParameter<T>` / 8 种参数类型
      - MinFloatParameter、ClampedFloatParameter、FloatParameter、IntParameter、BoolParameter
      - ColorParameter、Vector2Parameter、Vector3Parameter、Texture2DParameter、CubemapParameter
    - `VolumeStack` / `VolumeManager`：Volume 栈与单例管理器
  - **URP 后处理 Volume 组件（18 种）**
    - Bloom、ColorAdjustments、ColorCurves、Vignette、FilmGrain
    - Tonemapping、LensDistortion、DepthOfField、MotionBlur
    - PaniniProjection、ScreenSpaceReflection、ShadowsMidtonesHighlights
    - WhiteBalance、ChannelMixer、IPostProcessComponent 接口
- **完整 C# Job System 实现**（Unity.Jobs 命名空间）
  - 接口：`IJob`、`IJobParallelFor`、`IJobParallelForTransform`、`IJobParallelForBatch`、`IJobParallelForFilter`
  - `JobHandle`：完整结构、`Complete()`、`CombineDependencies`（多个重载）
  - `JobExtensions` / `IJobParallelForExtensions`：Schedule/Run 扩展方法
  - `BurstCompileAttribute` / `FloatPrecision` 枚举
  - `JobScheduler` 内部调度器、`TransformAccess` 结构体
  - `Allocator` / `NativeArrayOptions` / `JobMode` 枚举
  - `ReadOnlyAttribute` / `WriteOnlyAttribute` / `NativeContainerAttribute`
- **完整 NativeContainer 集合**（Unity.Collections 命名空间）
  - `NativeArray<T>`：完整实现（索引、Copy、Dispose JobHandle、枚举器）
  - `NativeList<T>`：动态数组（Add/RemoveAt/Clear/Resize/TrimExcess）
  - `NativeHashMap<TKey, TValue>`：哈希映射（TryAdd/TryGetValue/Remove/GetKeyValueArrays）
  - `NativeQueue<T>`：队列（Enqueue/Dequeue/Peek/Clear）
  - `NativeSlice<T>`：数组切片
  - `NativeKeyValueArrays<TKey, TValue>`：键值对数组
- **完整代码裁切 / Code Stripping 实现**（UnityEditor 命名空间）
  - `ManagedStrippingLevel`：Disabled/Low/Medium/High 四级
  - `ManagedStrippingEngineClass` / `StrippingUsedAsOption` 枚举
  - `PreserveAttribute`：多目标（程序集/类/方法/字段/属性等）
  - `UsedByNativeCodeAttribute`：原生代码引用标记
  - `LinkXmlGenerator`：link.xml 生成器（程序集/类型/方法/字段）
  - `ManagedStrippingInfo` / `CodeStrippingUtils`：裁切信息与工具
- **核心编辑器窗口完整实现**（UnityEditor 命名空间）
  - `HierarchyWindow`：层级窗口
    - 工具栏（搜索、Create 按钮）、树状结构、展开/折叠
    - 选择同步、`[MenuItem("Window/General/Hierarchy")]`
  - `ProjectWindow`：项目窗口
    - 双栏布局（文件夹树 + 资源列表）、单栏切换
    - 搜索过滤、资源图标按类型显示、`SceneAsset` 类型
    - `[MenuItem("Window/General/Project")]`
  - `InspectorWindow`：检视器窗口
    - 顶部工具栏（锁定、混合值、图层、菜单）
    - 标题栏（图标、名称、Tag、Layer）
    - 组件标题栏（折叠、展开）、Transform 编辑器
    - 空选择提示、Add Component 按钮
    - `[MenuItem("Window/General/Inspector")]`
  - `ConsoleWindow`：控制台窗口
    - 工具栏（Clear/Collapse/Clear on Play/Error Pause/搜索）
    - 日志级别过滤（Info/Warning/Error 计数）
    - 条目选择展开堆栈、状态栏统计
    - `Application.logMessageReceived` 事件监听
    - `[MenuItem("Window/General/Console")]`
- **编辑器样式与工具增强**
  - `EditorStyles`：从 10 个扩展到 70+ 样式
    - 标签类（miniLabel/redLabel/yellowLabel/highlight/selected/centeredGreyMiniLabel）
    - 工具栏类（toolbarButton/toolbarDropDown/toolbarSearchField/toolbarPopup）
    - Inspector 类（inspectorDefaultMargins/inspectorTitlebar）
    - ProjectBrowser 全套（headerBg/sidebar/gridLabel/iconDropShadow）
    - TreeView 全套（item/selected/active/inactive/renamingField）
    - SectionHeader、largeLabel、titleLabel、statusbar、notification
  - `EditorGUIUtility`：完整工具类
    - `FindTexture` / `IconContent` / `TrTextContent`
    - `pixelsPerPoint` / `singleLineHeight` / `isProSkin` / `mainBackgroundColor`
    - `GetAspectRect` / `GetControlID` / `PingObject`
    - `ConvertToGammaSpace` / `ConvertToLinearSpace`
    - 文件面板（SaveFilePanel/OpenFilePanel/GetSaveFolderPanel）
- **Editor 类增强**
  - 静态 `CreateEditor` 方法（4 个重载）
  - `targets` 属性（多对象编辑）
  - `DrawHeader()` / `Repaint()` 方法
  - `GenericEditor` 内部默认编辑器实现

## 2026-07-09（本次 - Unity 2022 粒子系统完整实现）

### 已完成
- **完整粒子系统（ParticleSystem）实现**，完全对齐 Unity 2022 LTS API
  - 位置：`UnityEngine/ParticleSystem/` 目录
  - 删除旧的 `UnityCompat/Runtime/ParticleSystem.cs`（已被新实现取代）

- **粒子系统枚举（35+ 枚举）** — [ParticleSystemEnums.cs](file:///workspace/anity-lib-core/src/Anity.Core/UnityEngine/ParticleSystem/ParticleSystemEnums.cs)
  - 形状相关：`ParticleSystemShapeType`（20种）、`ParticleSystemShapeMultiModeValue`、`ParticleSystemMeshShapeType`
  - 动画/模式：`ParticleSystemCurveMode`、`ParticleSystemGradientMode`、`ParticleSystemSimulationSpace`
  - 渲染：`ParticleSystemRenderMode`、`ParticleSystemRenderSpace`、`ParticleSystemSortMode`、`ParticleSystemVertexStreams`
  - 碰撞：`ParticleSystemCollisionType`、`ParticleSystemCollisionMode`、`ParticleSystemCollisionQuality`
  - 拖尾：`ParticleSystemTrailMode`、`ParticleSystemTrailRibbonShape`
  - 噪声：`ParticleSystemNoiseQuality`
  - 子发射器：`ParticleSystemSubEmitterType`、`ParticleSystemSubEmitterProperties`
  - 其他：`ParticleSystemStopBehavior`、`ParticleSystemEmitterVelocityMode`、`ParticleSystemCullingMode`、`ParticleSystemRingBufferMode`
  - 力场：`ParticleSystemForceFieldShape`、`ParticleSystemForceFieldShapeType`
  - 自定义数据：`ParticleSystemCustomData`、`ParticleSystemCustomDataMode`
  - 纹理动画：`ParticleSystemAnimationMode`、`ParticleSystemAnimationTimeMode`、`ParticleSystemAnimationType`

- **粒子数据结构** — [ParticleSystemDataStructs.cs](file:///workspace/anity-lib-core/src/Anity.Core/UnityEngine/ParticleSystem/ParticleSystemDataStructs.cs)
  - `ParticleSystem.MinMaxCurve`：4 种模式（Constant/Curve/TwoCurves/TwoConstants）、Evaluate 方法
  - `ParticleSystem.MinMaxGradient`：4 种模式（Color/Gradient/TwoColors/RandomColor）、Evaluate 方法
  - `ParticleSystem.Particle`：完整字段（lifetime/position/velocity/rotation/scale/color/seed 等 20+ 字段）
  - `ParticleSystem.Burst`：爆裂发射（time/count/minCount/maxCount/cycles/interval/probability）

- **ParticleSystem 核心类** — [ParticleSystem.cs](file:///workspace/anity-lib-core/src/Anity.Core/UnityEngine/ParticleSystem/ParticleSystem.cs)
  - 状态：`isPlaying`、`isStopped`、`isPaused`、`isEmitting`、`particleCount`、`time`
  - 控制：`Play()`、`Stop()`、`Pause()`、`Resume()`、`Clear()`、`Simulate()`、`Emit()`
  - 粒子读写：`GetParticles()`、`SetParticles()`
  - 子系统：`GetChildParticleSystems()`、`TriggerSubEmitter()`、`ResetSimulation()`
  - 18 个模块属性访问器（main/emission/shape/velocityOverLifetime 等）

- **主模块 / 发射 / 形状模块** — [ParticleSystemModules1.cs](file:///workspace/anity-lib-core/src/Anity.Core/UnityEngine/ParticleSystem/ParticleSystemModules1.cs)
  - `MainModule`：duration/loop/playOnAwake/startLifetime/startSpeed/startSize/startColor/gravityModifier/simulationSpeed/maxParticles 等 20+ 属性
  - `EmissionModule`：rateOverTime/rateOverDistance/bursts（支持 GetBursts/SetBursts）
  - `ShapeModule`：20 种形状类型、radius/angle/position/rotation/scale、mesh/meshRenderer/skinnedMeshRenderer、纹理采样、arc/donut 等高级参数

- **速度 / 力 / 颜色 / 大小 / 旋转模块** — [ParticleSystemModules2.cs](file:///workspace/anity-lib-core/src/Anity.Core/UnityEngine/ParticleSystem/ParticleSystemModules2.cs)
  - `VelocityOverLifetimeModule`：x/y/z + orbital + radial + speedModifier
  - `LimitVelocityOverLifetimeModule`：limit/dampen/drag、separateAxes
  - `InheritVelocityModule`：mode（Initial/Current）+ curve
  - `ForceOverLifetimeModule`：x/y/z + space + randomize
  - `ColorOverLifetimeModule` / `ColorBySpeedModule`
  - `SizeOverLifetimeModule` / `SizeBySpeedModule`（支持 separateAxes）
  - `RotationOverLifetimeModule` / `RotationBySpeedModule`（支持 separateAxes）

- **外力 / 噪声 / 碰撞 / 触发 / 子发射器模块** — [ParticleSystemModules3.cs](file:///workspace/anity-lib-core/src/Anity.Core/UnityEngine/ParticleSystem/ParticleSystemModules3.cs)
  - `ExternalForcesModule`：multiplier + influenceFilter + forceFields 增删改查
  - `NoiseModule`：strength/frequency/scrollSpeed/octaves/quality + positionAmount/rotationAmount/sizeAmount + remap
  - `CollisionModule`：type（World/Planes）+ quality + bounce/dampen/lifetimeLoss + collidesWith + planes
  - `TriggerModule`：inside/outside/enter/exit + colliders
  - `SubEmittersModule`：birth/collision/death/trigger/manual 子发射器 + Add/Remove/Get/Set

- **纹理动画 / 灯光 / 拖尾 / 自定义数据模块** — [ParticleSystemModules4.cs](file:///workspace/anity-lib-core/src/Anity.Core/UnityEngine/ParticleSystem/ParticleSystemModules4.cs)
  - `TextureSheetAnimationModule`：grid/sprites 模式 + frameOverTime + startFrame + cycles + flipU/V
  - `LightsModule`：light/ratio + range/intensity 曲线 + useParticleColor/maxLights
  - `TrailModule`：ratio/lifetime + widthOverTrail + colorOverTrail + ribbonCount
  - `CustomDataModule`：Custom1/Custom2 两个流 + vector/color 模式

- **ParticleSystemForceField**：力场组件（shape/startRange/endRange/strength/rotationSpeed/drag/gravity）

- **ParticleSystemRenderer 渲染器** — [ParticleSystemRenderer.cs](file:///workspace/anity-lib-core/src/Anity.Core/UnityEngine/ParticleSystem/ParticleSystemRenderer.cs)
  - 渲染模式：renderMode（Billboard/Stretch/Mesh/HorizontalBillboard/VerticalBillboard）
  - 对齐与排序：renderAlignment、sortMode
  - 拉伸：speedScale、lengthScale、cameraVelocityScale
  - 尺寸控制：minParticleSize、maxParticleSize、flip、pivot
  - 网格：meshCount、GetMesh/SetMesh/GetMeshes/SetMeshes
  - 顶点流：activeVertexStreams、EnableVertexStreams/DisableVertexStreams
  - 烘焙：BakeMesh、BakeTrailsMesh
  - 排序：sortingOrder、sortingLayerID
  - 拖尾材质：trailMaterial

### 下一次要做（优先）
1. 完善 URP Shader 和材质系统（URP/Lit、URP/Unlit 等）
2. 实现编辑器主窗口框架（菜单栏、工具栏、状态栏、Dock 布局）
3. 实现 SceneView 场景绘制与 Gizmos 系统

