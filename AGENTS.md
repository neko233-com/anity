# AGENTS.md - Anity 项目开发规范与用户偏好

> 所有 Agent 必须阅读并遵守本文件中的规范。**不要就以下内容再询问用户**，直接按规范执行。

---

## 〇、终极目标（强制）

**完全对标 Unity 2022.3.x Pro（LTS），API 与功能效果必须一模一样。**

1. **API 签名**：命名空间、类型、成员、枚举值、重载、事件、特性与 Unity 2022.3 Pro **逐字一致**（可多实现内部类型，但公开表面必须兼容）。
2. **行为与效果**：运行时语义、物理/渲染/音频/动画结果、编辑器交互与 Unity 2022 Pro **功能等价**；允许实现路径不同，**不允许**“只有签名、没有效果”的空壳长期存在。
3. **编辑器**：菜单、窗口布局、工具栏、状态栏、Inspector、Scene/Game/Project/Hierarchy/Console、Prefab Mode、Ctrl+K 搜索等与 Unity 2022 Pro **视觉与交互一致**。
4. **查漏补缺是默认工作**：发现与 Unity 2022 Pro 的差距时 **直接补齐**，不要问“是否需要支持”。
5. **新增/变更 API 必须记入 `PLAN.md`**。
6. **禁止**用“stub / 假实现 / 仅签名”作为最终交付；临时 stub 必须标注 `// ANITY_STUB` 并列入 PLAN 下一次优先项。

### 与 Unity 实现语言对齐（强制）

| Unity 侧 | Anity 侧 |
|----------|----------|
| 引擎核心、渲染、物理、音频混音、资源导入、Jobs 调度等 **C/C++** | 必须落在 **`anity-native/` C++**（或平台原生 Objective-C++/Metal），经 P/Invoke / C-ABI 暴露 |
| 脚本 API、编辑器 UI、托管层 **C#** | 落在 **`anity-lib-core`** 等 C# 程序集，调用 native |
| 平台后端（D3D/Vulkan/Metal/GLES） | native 图形后端；iOS **Metal only**；Android **Vulkan 主路径**（GLES 回退） |

- **Unity 用 C++ 写的部分，Anity 也必须用 C++ 写**（或同等原生语言），不得只用 C# 永久替代性能/语义关键路径。
- C# 层负责 API 兼容与托管对象生命周期；**重计算、渲染命令、物理步进、解码、压缩** 优先进 native。

---

## 一、渲染管线

- **唯一产品级渲染管线：URP（Universal Render Pipeline）14.x**（对齐 Unity 2022 LTS）
- **不支持**：Built-in Render Pipeline 作为主路径、**HDRP 产品管线**、自定义 SRP 模板作为交付目标
- **必须支持 HDR（High Dynamic Range）**：  
  - 这是 **HDR 渲染/显示**，不是 HDRP 管线  
  - 含：Linear 工作流、HDR RenderTexture、`Camera.allowHDR`、`HDROutputSettings`、色调映射、Bloom 等 URP 后处理、平台 HDR 显示输出
- 所有 Shader、材质、渲染特性以 **URP 14.x + Unity 2022.3 Pro 行为** 为基准
- 纹理压缩：**ASTC / ETC·ETC2 / DXT·BC / PVRTC（legacy）** 与平台矩阵一致

---

## 二、核心功能要求

### 1. Job System
- 完整支持 Unity C# Job System：`IJob`、`IJobParallelFor`、`IJobParallelForTransform`、`JobHandle`
- Burst 兼容 `[BurstCompile]`；NativeContainer：`NativeArray`、`NativeList`、`NativeHashMap`、`NativeQueue` 等
- 调度与并行核心应对齐 native jobs（`anity-native` jobs 模块）

### 2. 代码裁切 / Code Stripping
- Managed Stripping：High / Medium / Low / Disabled
- Engine Module Stripping、`[Preserve]`、`link.xml`、IL2CPP 协同

### 3. IL2CPP（绝对强制）
- **IL2CPP 必须完整支持**，不得降级为“仅检测壳”
- 完整构建管线：AOT、泛型共享、代码生成、`link.xml`、`[Preserve]`、`Il2CppSetOption`、剥离级别
- 运行时：`Il2CppRuntime.IsIl2Cpp`、泛型注册、AOT 元数据、与 `PlayerSettings`/`BuildPipeline` 后端切换一致
- 平台：iOS/Android/Standalone IL2CPP 构建路径均要可用（行为与 Unity 2022 Pro 对齐）

### 4. CLI（anity.exe，对标 Unity 命令行）
- 产出 **`anity.exe`**（或跨平台 `anity`），命令行能力与 **Unity Editor/Player CLI** 对齐
- 至少支持（与 Unity 同名/同语义）：`-batchmode`、`-quit`、`-projectPath`、`-executeMethod`、`-buildTarget`、`-logFile`、`-nographics`、`-runTests`、`-testResults`、`-buildWindowsPlayer` / 各平台 Build 开关、`-silent-crashes` 等
- 额外 Anity 扩展参数允许，但不得破坏 Unity 兼容参数解析

### 5. 官方扩展库：Anity.Agent（独立，类似 UGUI）
- **Agent 原生能力**是**外部官方扩展**，必须独立程序集/包：`anity-agent/`（对标 Unity 官方 `com.unity.ugui` 式独立库）
- **禁止**把 Agent 核心逻辑塞进 `Anity.Core` 引擎层；Core 只提供可被扩展的运行时钩子
- Agent 库自有 API、自有测试、可单独版本化与发布

### 6. 截图
- 完整支持 Unity `ScreenCapture`：`CaptureScreenshot` / `CaptureScreenshotAsTexture` / `CaptureScreenshotIntoRenderTexture` 等
- 行为与 Unity 2022 Pro 一致（含 superSize、异步路径若有）

### 7. 媒体
- **音频**：mp3、wav、ogg、aac、m4a、flac 等（解码进 native 或平台编解码器）
- **视频**：mp4、webm、mov 等；`VideoPlayer` / WebGL 视频行为对齐 Unity

### 8. 物理
- 3D：CCD 扫掠 / 参数化 TOI；2D：SAT 等精确碰撞；与 Unity 2022 PhysX/Box2D 语义对齐（实现可自研，效果要对齐）

### 9. 编辑器
- Prefab Mode、Project 浏览器、Scene/Game、Ctrl+K Quick Search、Hierarchy、Inspector、Console 等全量对齐

### 10. 测试（强制深度）
- **先完全落地 Unity Pro 功能，再谈“完成”**；禁止用浅测冒充一致
- **每个功能模块至少 10 个测试用例**，覆盖：正常路径、边界值、非法参数、空引用、多线程/并发（如适用）、平台分支、与 Unity 语义对照
- 测试工程：`anity-lib-core/tests/`、`anity-agent/tests/`、`anity-cli/tests/` 等；用 xUnit
- 新增功能 **必须同步提交 ≥10 用例**，否则不得在 Checklist 标 ✅

---

## 三、开发原则

1. **完全对标 Unity 2022.3 Pro**：API + 行为 + 编辑器 UI + 构建产物语义；**深度完善到边界条件一致**
2. **签名先齐，行为必须跟上**：不允许无限期“只签名”
3. **优先补高频迁移阻塞点**：Build、PackageManager、Editor、Runtime、URP、物理、资源导入、**CLI、IL2CPP、截图**
4. **平台图形默认**：
   - iOS / tvOS / visionOS → **Metal**
   - Android → **Vulkan**（GLES3/2 回退）
   - Windows → D3D11/12（Vulkan 可选）
   - WebGL → WebGL2
5. **脚本与工具位置**：环境安装、依赖、构建、审计脚本统一放在 **`_scripts/`**（见第六节）
6. **新增 API / 原生模块变更必须写入 `PLAN.md`**
7. **查漏补缺**：每次任务结束后更新 `Checklist.md` 状态；发现 ❌ 或 ANITY_STUB 必须排期补齐
8. **官方扩展（Agent）独立版本化**，不污染引擎 Core

---

## 四、平台支持优先级

1. **WebGL** — 最高优先  
2. **Windows** — 第二优先（编辑器 + 玩家）  
3. **Android（Vulkan）**  
4. **iOS（Metal）**  
5. 其它平台按需补  

---

## 五、禁止事项

- 不要询问“是否要支持 HDRP 产品管线”——**不支持 HDRP**；**必须支持 URP 下的 HDR**
- 不要询问“是否需要 Job System / 代码裁切 / IL2CPP / Metal / Vulkan / Prefab Mode / 全局搜索 / CLI / 截图 / Agent 扩展”——**全部需要**
- 不要询问“是否要和 Unity 完全一样”——**必须完全一样**
- 不要把 Agent 扩展做进 Core 引擎程序集
- 不要用 C# 永久代替 Unity 侧的 C++ 模块职责
- 不要在用户没要求时创建额外说明性 `.md`（`AGENTS.md` / `PLAN.md` / `Checklist.md` 除外）
- 不要在代码里写入 HTML/XML 转义字符（如 `&amp;`、`&lt;`）
- 不要用少于 10 个用例就宣称某功能“完成”

---

## 六、脚本与环境（`_scripts/`）

**所有安装环境、依赖、构建、验证、审计脚本必须放在仓库根目录 `_scripts/`。**

| 脚本 | 用途 |
|------|------|
| `install-env.ps1` / `install-env.sh` | 一键安装开发环境（入口） |
| `verify-env.ps1` / `verify-env.sh` | 校验 SDK / 编译器 / 工具链 |
| `build-native.ps1` / `build-native.sh` | 构建 `anity-native` C++ |
| `build-all.ps1` / `build-all.sh` | 构建 native + 全部 C# 模块 |
| `gap-audit.ps1` | Unity 2022 Pro API / 模块差距审计 |
| `install-android-sdk.ps1` | Android SDK/NDK |
| `install-vulkan-sdk.ps1` | Vulkan SDK（Windows/Linux） |
| 其它 | 按模块追加，统一放在 `_scripts/` |

- 旧目录 `scripts/` 仅作兼容转发；**新脚本只写 `_scripts/`**。
- Windows 优先 PowerShell；Unix 提供对应 `.sh`。

---

## 七、仓库布局（Agent 须知）

```
anity/
  AGENTS.md                 # 本规范
  PLAN.md / Checklist.md
  _scripts/                 # 环境与构建脚本（唯一权威）
  anity-native/             # C++ 引擎核心（对标 Unity native）
  anity-lib-core/           # C# Unity API 兼容层
  anity-cli/                # anity.exe 命令行（对标 Unity CLI）
  anity-agent/              # 官方扩展：Agent（独立，类 UGUI）
  anity-editor/             # 编辑器 Host
  anity-webgl/              # WebGL 运行时
  anity-hub/                # Hub
  samples/                  # 示例
```

---

## 八、验收标准（每次相关改动）

1. 相关工程 **0 编译错误**  
2. 公开 API 与 Unity 2022.3 文档/反射面一致（或 PLAN 登记差异与补齐 ETA）  
3. **每个功能 ≥10 测试用例**，含边界条件；`dotnet test` 通过  
4. native 变更：`_scripts/build-native.*` 可构建  
5. CLI：`anity -batchmode -quit` 等路径可运行  
6. IL2CPP 相关路径不可缺失  
7. `PLAN.md` 已记录本次完成项与下 1–3 个优先项  
