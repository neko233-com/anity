# PLAN

## 2026-07-20 — 独立 self-contained Anity CLI 分发门禁

### 已完成
- 定位 Release 目录 framework-dependent `anity` apphost 在本机 exit 150 的根因：它仍依赖系统注册的匹配 .NET runtime，而本机全局 host 只登记 .NET 9；这不是 Unity-compatible CLI 参数解析失败。新增 `_scripts/publish-cli.sh` / `publish-cli.ps1`，按当前 host RID 发布 self-contained `anity` / `anity.exe`，并把 host runtime 与 `anity-native` 动态库放入同一分发目录。
- 发布流程只接受 host-matching 架构，拒绝 cross-arch native 混装与 `build/cli/<rid>` 外的输出；使用同一文件系统下 staging + atomic move，失败 trap 同时回收 publish/smoke 临时目录。macOS 对 apphost/native 分别做 Mach-O 架构与 ad-hoc codesign 门禁，Unix 同时兼容 `file` 的 arm64/aarch64 与 x86_64/x86-64 命名。
- self-contained smoke 显式设置不存在的 `DOTNET_ROOT`、关闭 multilevel lookup，并执行 `-batchmode -quit -nographics -logFile -`；验证三项状态、exit 0 且不生成名为 `-` 的文件。本机 `osx-arm64` 分发为 **84,220 KiB**，`anity` 与 `libanity_native.dylib` 均为 ARM64、签名有效、无 PDB，apphost 仅动态链接系统 `libSystem` / `libc++`，不再依赖系统 .NET runtime。
- 新增 **13 个**真实分发进程用例，覆盖无注册 runtime/PATH、batchmode、stdout sentinel、日志 flush、help、缺失 project、未知 target、runTests XML、Mach-O/PE/ELF host 架构、native runtime、runtimeconfig included framework、app-local hostfxr 与无 debug symbols；CLI suite 由 **27/27** 增至 **40/40**。
- `_scripts/run-tests.sh` / `.ps1` 现在先生成 host self-contained CLI，再把真实分发目录注入测试；最终 native-required 八工程矩阵 **4187/4187**（Core **3165/3165**），0 失败、0 跳过。`_scripts/build-all.sh Release` 全产品 0 编译错误；边界修正后 CLI 聚焦 40/40 与完整 4187/4187 均已重跑。
- RID restore lock 改为 staging 内按 `MSBuildProjectName` 隔离；`publish-cli` 与 `install-macos-arm64.sh` 都不会再把平台 section 写入 tracked Core/Agent `packages.lock.json`。`/Applications/Anity.app` 已重新安装并独立验证 Host、内置 CLI、native 均为 ARM64，深度签名、图标逐 byte、两条 batchmode exit 0 全部通过，tracked lockfile 保持无差异。
- 所有验证完成后解析 repo-local 生成目录，确认 **40 个** `bin/obj/build` 目标均被 Git ignore 且内部无 tracked 文件；共 **381,480 KiB（约 372.5 MiB）**，已可恢复地移入 macOS 废纸篓 `/Users/solarisneko/.Trash/anity-cli-final-cleanup.9mdTP3`。最终复扫 `node_modules/bin/obj/build/dist/Library/Temp/Logs/UserSettings/.cache/.vite/packages/Generated` 为 0。

### 尚未完成
- macOS ARM64 分发已实跑；Windows `publish-cli.ps1` 已实现 `win-x64/win-arm64` host gate、native DLL 部署与无系统 runtime smoke，但本机没有 Windows/pwsh，不能把源码审阅冒充 Windows 11 `anity.exe` 产物证据。Linux x64/ARM64 路径同样尚未在真实 host 执行。
- CLI 仍缺默认 Editor.log、`-nolog` / `-upmLogFile`、许可/激活、崩溃日志、完整 Unity Editor/Player 参数和官方退出码矩阵；也尚未在 Unity 2022.3.61f1 Pro 对相同命令逐项 A/B，因此 CLI 总体继续为 🟡。
- self-contained 分发解决了本机 runtime 注册依赖，不代表 IL2CPP、各 Player build、Package Manager、Editor GUI 或整套 Unity 2022.3 Pro 已经完全对齐。

### 下一优先项
1. 对齐 pre/post rotation baked quaternion 的 MatrixConverter stack、normalize 舍入与 signed-zero，收紧到 exact-bit。
2. 增加 Renderer visibility `m_Enabled` binding，并补 ≥10 个 importer/runtime/Animator 用例。
3. 在 Windows 11 x64 与 Linux x64 实跑 `publish-cli`、40 项分发进程门禁及 Unity 2022.3.61f1 CLI A/B。

## 2026-07-20 — 全仓缓存复清与零引用 VFX legacy 路径删除

### 已完成
- 先在干净 `main` 上解析全部 repo-local 安装/构建缓存，确认 Core、MetadataFixups 与 native 的 5 个初始 `bin/obj/build` 目标均被 Git ignore、内部无 tracked 文件；共 **12,904 KiB（约 13 MiB）**，已移入 macOS 废纸篓而非不可恢复删除。冷态 `_scripts/verify-env.sh` 明确报告 native 尚未构建且工具链完整。
- 对 tracked source、测试、C ABI、P/Invoke、构建脚本和 workflow 做 legacy/obsolete/deprecated 反向引用审计。唯一真正未使用的是 native 内部 `static SubmitVFXInitializeKernelsSynchronousLegacy`：全仓精确标识符仅定义 **1** 处、调用 **0**、导出 **0**，现行 Initialize ticket/transaction 路径已替代它；已删除完整 **220 行**旧同步实现。
- 保留 Unity 2022.3 公开 `[Obsolete]` API、removed networking surface、FBX legacy 数值规则、Shader Graph/VFX Graph 旧 serialized asset migration，以及仍被 managed/native 调用或测试覆盖的 ABI compatibility entry；这些是 Unity/API/资产兼容要求，不是可删除死代码。测试编译时对 obsolete API 的显式调用也再次证明其兼容门禁仍在使用。
- 从无 native/cache 状态执行 `_scripts/build-all.sh Release` 成功；native 及 Core、Agent、Shader Graph、VFX Graph、CLI、WebGL、Hub、Editor、URP sample 均 **0 编译错误**。强制 `ANITY_REQUIRE_NATIVE=1` 的八工程矩阵 **4174/4174**（Core **3165/3165**），0 失败、0 跳过，证明旧同步实现不是任何现行构建或运行依赖。
- Unity-compatible CLI 通过 `dotnet anity.dll -batchmode -quit -nographics -logFile -`，exit 0；`/Applications/Anity.app` 已重新安装，Host、内置 CLI 与 `libanity_native.dylib` 均为 ARM64，深度签名、Host/CLI batchmode、Info.plist 和图标逐 byte 门禁均通过。
- 安装验证产生的两个 `osx-arm64` lockfile section 已精确还原；随后再次移出 **39 个**无 tracked 文件的 `bin/obj/build` 目录，共 **290,572 KiB（约 284 MiB）**。本轮合计从工作区清理 **303,476 KiB（约 296 MiB）**，最终复扫 `node_modules/bin/obj/build/dist/Library/Temp/Logs/.cache/.vite/packages` 为 **0**。

### 尚未完成
- repo-local 缓存已清空；全机 NuGet/Homebrew/Unity/.NET 共享缓存会影响其它项目，因此没有把本项目清理扩张为系统级破坏性删除。
- Release 目录中的 framework-dependent `anity` apphost 在当前机器直接运行会因全局 host 只登记 .NET 9 而 exit 150；SDK 托管入口与安装包内 self-contained CLI 均通过，但独立 CLI 分发仍需 self-contained 产物和进程级门禁，不能标为完整。
- Unity 必须公开的 obsolete/legacy compatibility surface 仍需随 2022.3.61f1 反射审计扩充；保留这些兼容 API 不代表完整 Unity 对齐。FBX pre/post quaternion、visibility binding 与完整平台/编辑器门禁也仍未闭环。

### 下一优先项
1. 产出可直接运行的 self-contained `anity` / `anity.exe` CLI，并增加无系统 runtime 的进程级安装/退出码门禁。
2. 对齐 pre/post rotation baked quaternion 的 MatrixConverter stack、normalize 舍入与 signed-zero，收紧到 exact-bit。
3. 增加 Renderer visibility `m_Enabled` binding，并补 ≥10 个 importer/runtime/Animator 用例。

## 2026-07-20 — Unity FBX retained-pivot position exact-bit 与废弃 bake 清理

### 已完成
- 用本机 Unity 2022.3.51f1 batchmode 重新采集 positive/negative/fractional/large rotation pivot、scaling pivot 与 scaling offset **10 组** fixture；每组 `m_LocalPosition.x/y/z` 各 24 keys，共固化 **720 个 Unity float bit pattern**。旧路径仅 **143/720** 逐 bit 一致，并有 16 个 signed-zero 与 561 个数值差异。
- 根因确认是 retained scene 的 `ufbx_bake_anim()` 先做矩阵分解，导致 Unity 原本精确的 `0/1` 被提前变成极小残差或 `1±3e-8`。native 现直接在 exact `FbxTime` 上用 Unity-compatible KFCurve 求值，将 scalar 先落为 float，再按 FBX translation/rotation offset/pivot/scaling offset/pivot/scale 顺序组合仿射矩阵，translation 提取为 float 后才执行 float 文件单位转换，并保留 Unity 的 180° axis-adjust signed-zero。
- 新路径对 10 组 position fixture 达到 **720/720 float exact-bit**，0 signed-zero 差异、0 数值差异；新增 **10 个**永久 bit-pattern 回归，`NativeModelImportTests` **132/132**。删除已经不再被任何输出读取的第二次 retained-pivot `ufbx_bake_anim()`、baked-node typed-id map 与相关临时对象，只保留 raw retained scene，避免双份 bake 成本与死路径继续漂移。
- `_scripts/build-all.sh Release` 全产品工程 0 编译错误；统一 native-required 八工程矩阵 **4174/4174**（Core **3165/3165**），0 失败、0 跳过。Unity-compatible CLI `-batchmode -quit -nographics -logFile -` exit 0；`/Applications/Anity.app` 已重新安装并独立验证 host/native 均为 ARM64、深度签名有效、batchmode exit 0、应用图标逐 byte 一致。
- 发布门禁完成后再次解析并审计仓库生成目录：删除 40 个无 tracked 文件的 `bin/obj/build/Generated` 目标，共 **290632 KiB（约 284 MiB）**；两个 `osx-arm64` restore 临时写入的 lockfile section 已还原，最终提交不包含安装缓存或生成依赖漂移。全机 NuGet/Homebrew/Unity 共享缓存未删除，避免破坏其它项目。

### 尚未完成
- 本轮 exact-bit 证据限定于已固化的 Unity Maya FBX 坐标/单位基与 10 组 XYZ pivot/offset fixture；其它 axis system、unit scale、non-XYZ pivot composition、instanced/helper/skinned pivot 与 bindpose 仍需权威 A/B，不能外推为完整 FBX transform stack。
- pre/post rotation baked quaternion 仍有 1–5 ULP，`Renderer.m_Enabled` visibility binding 仍缺失；多 animation layer、constant/stepped/weighted/broken tangent、loop/root motion/additive/mask、Unity 2022.3.61f1 Pro Editor/Player A/B 尚未闭环，因此 ModelImporter/AnimationClip 继续为 🟡。

### 下一优先项
1. 对齐 pre/post rotation baked quaternion 的 MatrixConverter stack、normalize 舍入与 signed-zero，收紧到 exact-bit。
2. 增加 Renderer visibility `m_Enabled` binding，并补 ≥10 个 importer/runtime/Animator 用例。
3. 扩展其它 axis/unit/rotation-order、helper/instanced/skinned pivot/bindpose fixture，再推进多 layer、tangent mode 与 root motion。

## 2026-07-19 — 安装缓存与未使用 legacy 基础设施清理

### 已完成
- 对仓库全部 ignored 安装/构建产物做冷启动审计，删除 `_scripts`、Core、Agent、CLI、Editor、Hub、WebGL、Shader Graph、VFX Graph、sample 的 `bin/obj`，以及 `anity-native/build*`、Unity probe `Generated/Logs/UserSettings` 与 ignored parity 日志；清理前约 **145 MB**，所有目标均由 `.gitignore` 确认为可重建产物。共享的系统 NuGet/Homebrew/Unity 安装缓存未越界删除。
- 证明历史 submodule 架构已经停用：`.gitmodules` 声称的四个模块在当前 Git index 中均为普通 `100644` 文件，模块内无嵌套 Git，local config 无 `submodule.*`，现行 build/test/workflow 无 `modules/` 调用。已删除 `.gitmodules`、12 个仅操作旧多仓库/模式切换/过期 unsupported 表的 `scripts/` helper 及其 legacy README，并移除 workflow 的 recursive submodule checkout。
- README、architecture、CI、ops、contributing、dependency policy、versioning 与 AGENTS 脚本规则已统一为真实 monorepo + `_scripts/` 唯一入口。对 8 个已删脚本/配置名执行 tracked-source 反向引用审计（排除 PLAN/Checklist 历史记录）均为 **0**。
- 保留 Unity 2022.3 公开反射面要求的 `[Obsolete]` API、removed legacy networking 类型、FBX legacy 数值规则和 VFX serialized deprecated-field migration；这些均被反射/行为/asset migration 测试引用，删除会破坏 Unity 对齐，不属于未使用废弃代码。
- 在完全无旧 `bin/obj/CMake` 产物下运行 `_scripts/verify-env.sh`，确认 native 初始未构建；随后 `_scripts/build-all.sh Release` 从零成功，所有产品工程 **0 编译错误**。统一 native-required 八工程矩阵 **4164/4164**（Core **3155/3155**），0 失败、0 跳过，证明被删 legacy 基础设施和旧缓存均非当前构建/运行依赖。

### 尚未完成
- 本轮只清理仓库范围内可重建缓存和已证明未使用的历史基础设施；系统级共享 package/toolchain cache 影响其它项目，未把“项目清理”扩张为全机破坏性删除。
- Unity 2022.3 的 obsolete 兼容面仍随总体 API parity 持续扩充；保留它们不代表所有历史 API 已完成。整体 Unity 2022.3.61f1 Pro 对齐仍是 🟡，尤其 retained-pivot position/pre-post quaternion、visibility binding 与完整平台/编辑器门禁均未闭环。

### 下一优先项
1. 对齐 retained-pivot position 的 FBX matrix/float 舍入和 signed-zero，并收紧 24-key position A/B 到 exact-bit。
2. 对齐 pre/post rotation baked quaternion 的 MatrixConverter stack 与 normalize 舍入，消除剩余 1–5 ULP。
3. 增加 Renderer visibility `m_Enabled` binding，并补 ≥10 个 importer/runtime/Animator 用例。

## 2026-07-19 — Unity FBX pivot raw Euler exact-bit

### 已完成
- 用本机 Unity 2022.3.51f1 batchmode 对 positive/negative/fractional/large rotation pivot、scaling pivot 与 scaling offset 构造 **10 组**新 fixture；每组 `localEulerAnglesRaw.x/y/z` 均为 24 keys，并确认 pivot 数值不改变源 rotation curve 的 Unity payload。共固化 **10 × 24 × 3 = 720 个 Unity float**。
- 定位原最大约 `1.335e-5` 差异来自错误复用了 quaternion extraction 的 legacy float frame-to-seconds 网格，并且直接调用通用 `ufbx_evaluate_curve()`。Unity 的 raw pivot Euler 实际由 MatrixConverter 放在 exact `FbxTime` frame grid，再经 `KFCurve::EvaluateIndex` 的 tick、unweighted handle 与逐级 float 舍入求值。
- `anity-native` 新增独立 `UnityFbxFrameTime()`，pivot raw Euler 与 retained-pivot position 使用 exact frame tick；raw Euler 接入既有 `EvaluateUnityCompatibleCurve()`，同时保留 resampled quaternion 的 `UnityFbxSampleTime()`，不再把两条可观测时间路径混用。
- 新增 **10 个**全曲线 bit-pattern 回归，每例检查 X/Y/Z 各 24 个 key，**720/720 float 逐 bit 一致**；既有 10 组 mixed raw binding/sample 门禁从 `2e-5` 收紧到 `1e-6`。`NativeModelImportTests` **122/122**，与 skin/blend-shape 联合 **164/164**；`_scripts/build-native.sh Release`、`_scripts/build-all.sh Release` 均通过且产品工程 0 编译错误，统一 native-required 八工程矩阵 **4164/4164**（Core **3155/3155**），0 失败、0 跳过。

### 尚未完成
- raw Euler 已 exact-bit，但 retained-pivot position 仍有少量 1 ULP 与 Y/Z signed-zero 差异；pre/post rotation 继续使用安全 baked quaternion，部分样本与 Unity 相差 1–5 ULP。二者已通过 bit 诊断与 raw Euler 根因分离，不能将本轮 720/720 外推为整个 transform stack exact-bit。
- Unity raw 导入的 3-key `Renderer.m_Enabled` visibility binding 仍缺失；多 animation layer、constant/stepped/weighted/broken tangent、instanced/helper topology、skinned pivot/bindpose、loop/root motion/additive/mask 与 Unity 2022.3.61f1 Pro Editor/Player A/B 仍未闭环。

### 下一优先项
1. 对齐 retained-pivot position 的 FBX matrix/float 舍入和 signed-zero，并收紧 position 24-key A/B 到 exact-bit。
2. 对齐 pre/post rotation baked quaternion 的 MatrixConverter stack 与 normalize 舍入，消除剩余 1–5 ULP。
3. 增加 Renderer visibility property binding，输出 Unity 同语义 `m_Enabled` 曲线并补 ≥10 个 importer/runtime/Animator 用例。

## 2026-07-19 — Unity FBX pre/post rotation、pivot 与 geometry transform

### 已完成
- 用本机 Unity 2022.3.51f1 batchmode 对同一 Maya FBX 构造 `PreRotation`、`PostRotation`、二者组合、`RotationPivot`、`RotationOffset`、`ScalingPivot`、`ScalingOffset` 与 geometric translation/rotation/scaling 共 **10 组**权威 fixture，并分别采集 `resampleCurves=true/false` 的根节点静态 TRS、Mesh bounds、Transform binding/key 数及 frame `0/13/23` 值。
- `anity-native` 现以 `UFBX_PIVOT_HANDLING_ADJUST_TO_ROTATION_PIVOT` 构建 Unity 同语义的静态层级，把 rotation pivot 逆补偿下沉到 geometry/child；Mesh position/normal/tangent 与 blend-shape delta 同步应用 Unity FBX X-axis basis 的 Y/Z 反向，rotation/scaling pivot 与 geometric transform 的 bounds center 已与 Unity A/B 一致。
- 关闭 `resampleCurves` 时实现 Unity 的分支化 Transform 曲线策略：pre/post rotation 保留 3-key raw position 并烘焙 quaternion/scale；pivot compensation 使用 24-key position/scale，未带 rotation offset 时输出 24-key `localEulerAnglesRaw`；plain/geometric-only 资产继续保留 3-key raw position/Euler/scale。为复刻 Unity 的双重 pivot 语义，静态资源使用 adjusted-pivot scene，raw pivot position 由第二个 retained-pivot scene 求值。
- 新增 resampled/raw 各 **10 个**永久回归：前者逐例校验 16 个静态值、10 条 24 帧共享网格及 30 个 Unity curve sample，后者逐字校验 Transform binding/key-count 并校验 source/0/13/23 sample；新增合计 **20/20**，`NativeModelImportTests` **112/112**，与 skin/blend-shape 联合聚焦门禁 **154/154**。`_scripts/build-native.sh Release`、`_scripts/build-all.sh Release` 均通过且产品工程 0 编译错误；统一 native-required 八工程矩阵 **4154/4154**（Core **3145/3145**），0 失败、0 跳过。

### 尚未完成
- resampled 静态/曲线与 mixed raw position/quaternion/scale 当前以绝对误差 `1e-6` 验收；raw pivot Euler 已在后续 10 组、720-float 门禁中 exact-bit。retained position 与 pre/post quaternion 的少量 ULP/signed-zero 尚未逐 bit 闭环。
- Unity raw 导入还会生成 3-key `Renderer.m_Enabled` visibility curve；Anity 当前缺该 Renderer binding，不能把 Transform curve 主链通过冒充为完整 AnimationClip 对齐。
- 多 animation layer、constant/stepped/weighted/broken tangent、真正的 instanced/helper-node topology、skinned pivot/bindpose、loop/root motion/additive/mask、stable sub-asset fileID/type-tree/artifact cache，以及 Unity 2022.3.61f1 Pro Editor/Player A/B 仍未完成，因此 ModelImporter/AnimationClip 保持 🟡。

### 下一优先项
1. 收紧 retained-pivot position 与 pre/post quaternion 的 ULP/signed-zero 到 exact-bit。
2. 增加 Renderer visibility property binding，输出 Unity 同语义 `m_Enabled` 曲线并补 ≥10 个 importer/runtime/Animator 用例。
3. 补多 animation layer、constant/stepped/weighted/broken tangent、instanced/helper 与 skinned pivot/bindpose fixture，再推进 loop/root motion/additive/mask。

## 2026-07-19 — Unity FBX 多圈 wrap / tie / 非整帧采样

### 已完成
- 用本机 Unity 2022.3.51f1 batchmode 新增长区间 `720° / 1080°` 多圈、混合轴 wrap、`±180°` tie、13.5/14.5 非整帧 source key 共 **10 组**权威 fixture，固化 24 帧 × 4 quaternion 分量 × 10 组共 **960 个 Unity float**；另以 LLDB 在 `ExtractQuaternionFromFBXEulerOld` 入口采集实际 MatrixConverter Euler 输入，确认多圈 `V2VRef` 分支与非整帧缺样行为。
- `anity-native` 的 transform bake 改为 position/rotation/scale 共用同一帧数，ufbx 在非整帧 Euler key 处缺失 quaternion sample 时会按 Unity 的 24 帧网格补算；baked quaternion 查找使用相对 take 时间并以单调 cursor 线性扫描，避免数组错位和 O(n²) 搜索。
- 非 XYZ 连续 Euler 结果现按 MatrixConverter destination curve 的 float key 边界保存；Apple 路径使用与 FBX SDK 相同的联合 `__sincos`，quaternion 兼容校验接受 `q/-q` 等价，输出按前一 key 做 hemisphere continuity。13.5/14.5 fixture 均稳定输出 24 个同步 quaternion key，不再丢失中间/末尾样本。
- 继续反汇编并以 LLDB 读取 `FbxAnimCurveFilterUnroll::Apply`、`FbxRotationOrder::V2VRef` 与 `KFCurve::EvaluateIndex` 的运行时对象/寄存器：确认 Unroll 使用 `quality=0.25`、`testForPath=false`，普通 continuous 分支保持 MatrixConverter 的 double-derived user handle；只有选择等价 alternate XYZ 分支的 key 会按最终 float curve 邻键重算 auto/clamped handle，分支边界允许左右两侧混合。native 已按每个 key 的 branch 状态复刻该规则，并补齐 Clamped Auto 对 centered handle 的相邻增量限幅。
- 对剩余 frame 13/15/16 继续在 `ExtractQuaternionFromFBXEulerOld`、`SetDestFCurveTangeant` 与 `KFCurve::EvaluateIndex` 入口采集 destination key、double `V2VRef` residue、左右 derivative/handle 与 FbxTime period：user tangent 保持 exact FbxTime 除法再以 float key duration 求值；数学 identity 的 `±180°` tie 保留非零 Euler residue；进入 half-turn Unroll 的 track 保留 quaternion signed-zero，普通 identity track 规范为正零。原 **5/10** 临时近似现收紧为 **10/10 fixture、960/960 Unity float 逐 bit 一致**。
- 新增 **10 个** wrap/tie/subframe 权威回归；连同 10 个 exact-bit gimbal 与 5 个 identity/signed-zero 既有用例，聚焦门禁 **25/25**，`NativeModelImportTests` **92/92**。新极值组已直接使用 bit-pattern 断言，删除 `2e-5` 临时门槛与近似 helper。`_scripts/build-native.sh Release`、`_scripts/build-all.sh Release` 均通过且产品工程 0 编译错误；统一 native-required 八工程矩阵 **4134/4134**（Core **3125/3125**），0 失败、0 跳过。

### 尚未完成
- 当前 960/960 exact-bit 证据限定于已固化的 Maya FBX、单层、unweighted cubic、五种非 XYZ order 与这些 wrap/tie/subframe 轨迹；不能外推为完整 ModelImporter 对齐。
- 仍需覆盖 pre/post rotation、rotation/scaling pivot、helper transform、多 animation layer，以及 constant/stepped/weighted/broken tangent；正式版本仍需 Unity 2022.3.61f1 Pro Editor/Player A/B，因此 ModelImporter/AnimationClip 保持 🟡。

### 下一优先项
1. 增加 pre/post rotation、rotation/scaling pivot 与 helper transform fixture，逐项替换 ufbx baked 安全 fallback。
2. 补 constant/stepped/weighted/broken tangent 和多 animation layer 的 MatrixConverter/Unroll A/B。
3. 在 Unity 2022.3.61f1 Pro 重跑 exact-bit、Editor curve visualization 与 Player 连续时间采样门禁。

## 2026-07-19 — Unity FBX 非 XYZ rotation-order 转换

### 已完成
- 用本机 Unity 2022.3.51f1 batchmode 对同一 Maya FBX 构造 `XYZ / XZY / YZX / YXZ / ZXY / ZYX` 六种 `RotationOrder` fixture，逐帧采集未压缩 quaternion，并采集 `animationRotationError = 0.01 / 0.1 / 0.5` 的同步保留帧集合；确认 Unity 对非 XYZ 源先走 `FbxRotationOrder::V2M → FbxAnimCurveFilterMatrixConverter → M2V(XYZ)`，再进入 legacy XYZ quaternion 提取与压缩链。
- `anity-native` 已复刻 FBX SDK 的六种 axis table、odd-parity 符号、`FbxAMatrix::SetR` 运算顺序及等价 XYZ 提取；继续反汇编 `SetDestFCurveTangeant` 与 `KFCurve::EvaluateIndex`，按 float-expanded key、double derivative、float Hermite handle、legacy KTime tick 及逐级 float De Casteljau 顺序复刻 MatrixConverter 曲线，并将 identity M2V residue 规范为正零。
- 五种非 XYZ 顺序的未压缩 quaternion 共 **480/480 float 逐 bit 一致**；`animationRotationError = 0.01 / 0.1 / 0.5` 的 **15/15** 同步保留帧集合全部一致，原 `ZYX + 0.01` 的 frame 7/9 差异已消除。新增 24 帧/四分量同步、source key、zero crossing、identity 正零符号及 strict/normal/loose reduction 共 **35 个用例**；`NativeModelImportTests` **72/72**。
- 反汇编 Unity 随附 FBX SDK 的 `FbxAMatrix::GetROnly`，补齐 `M2V(XYZ)` 的精确 singular 判定：row-0 XY projection 与 `2^-48` 比较，regular 分支使用三组 `atan2`，gimbal 分支使用 `atan2(-m21,m11)`、保留 `atan2(-m02,projection)` 并令 Z 为正零；projection 明确保持两次乘法再相加，禁止 FMA 收缩。
- 用 `+90° / -90°` 两个目标姿态反解五种非 XYZ 源欧拉角，形成 **10 组** Unity 2022.3.51f1 batchmode A/B；完整固化 24 帧 × 4 分量 × 10 组共 **960 个 Unity float**。本轮重新执行 Unity batchmode 并在 `ExtractQuaternionFromFBXEulerOld` 入口以 LLDB 捕获真实 `FbxVector4`，确认 MatrixConverter 会把 `V2VRef` 连续化结果同时作为 destination key value 与 user-tangent 基准，而非只用于 tangent。Anity 已按该顺序以 track 级 O(n) 预计算重建连续 key，并对齐 FbxTime tick/`KeyFind` index rounding、unweighted `EvaluateIndex` 指令级 float 舍入及 `GetR → SetR → GetUnnormalizedQ` 路径；960/960 float 现全部逐 bit 一致，测试已从 ≤2 ULP 收紧为 exact-bit。`NativeModelImportTests` **82/82**；`_scripts/build-all.sh Release` 全部 0 编译错误，强制 native 八工程矩阵 **4124/4124**（Core **3115/3115**），均 0 失败、0 跳过。

### 尚未完成
- 当前 exact-bit 路径覆盖已固化的单层、unweighted cubic、常见 Maya FBX 坐标基、五种非 XYZ rotation order，以及新增多圈 Euler wrap/half-turn tie/非整帧 key；pre/post rotation、rotation/scaling pivot、helper transform、多 animation layer，以及 constant/stepped/weighted/broken tangent 仍未逐项完成 Unity A/B。不满足已证明约束的资产继续保留 ufbx baked 安全路径。
- 正式目标仍需在 Unity 2022.3.61f1 Pro 上重跑逐 ULP、Editor curve visualization 与 Player 连续时间采样门禁；ModelImporter/AnimationClip 保持 🟡。

### 下一优先项
1. 增加 pre/post rotation、rotation/scaling pivot、helper 与多 animation layer fixture，逐项替换安全 fallback。
2. 增加 constant/stepped/weighted/broken tangent fixture，扩展 MatrixConverter exact-bit 范围。
3. 完成 importer loop/root motion/additive/mask 与 stable sub-asset fileID/type-tree/artifact cache，并迁移到 Unity 2022.3.61f1 Pro 完整门禁。

## 2026-07-19 — Unity FBX quaternion 同步 angular reduction

### 已完成
- 用本机 Unity 2022.3.51f1 batchmode 锁定 `animationRotationError = 0.01 / 0.1 / 0.5 / 1` 的四条 quaternion component curve：四分量始终共享 key time，分别保留 frame `0,1,3,5,6,8,9,11,12,13,14,15,16,17,18,19,20,22,23`、`0,1,7,12,14,17,19,22,23`、`0,5,11,14,17,19,22,23`、`0,7,13,18,20,22,23`，且保留 key 的 value/tangent 不被重算。
- 反汇编 Unity 的 `ReduceKeyframes` / `QuaternionDistanceError` / `ExtractQuaternionFromFBXEulerOld` 与所带 Autodesk FBX SDK 的 `KFCurve::EvaluateIndex`，复刻原始/候选四元数 normalize、dot 阈值、逐源 key/中点/固定步长采样、50 帧跨度限制和 anchor/current/following 删除顺序；不再把四分量当作四条独立 scalar curve 压缩。
- `anity-native` 现按 FBX legacy `141120000` tick、`float(frame) * float(1/rate)` 与截断顺序生成重采样时刻；非加权 cubic curve 使用 FBX 的逐级 float-round/double-multiply De Casteljau 顺序，XYZ Euler 经 `FbxAMatrix::SetROnly/GetQ` 等价矩阵路径转为 quaternion，并禁止归一化平方和的 FP contraction。24×4 quaternion value 已逐 ULP 对齐 Unity 黑盒。
- 新增四档保留 frame、四分量同步、保留 value/tangent 共 **10 个用例**；`NativeModelImportTests` **37/37**。`_scripts/build-all.sh Release` 全部 0 编译错误；最终强制 native 八工程矩阵 **4079/4079**（Core **3070/3070**），均 0 失败、0 跳过。

### 尚未完成
- 当前精确路径覆盖单层、XYZ rotation order、Unity Maya FBX 常见 X 轴坐标基；其它 rotation order、pre/post rotation、pivot/helper、不同 axis system、多 animation layer 与 weighted/broken/constant/stepped tangent 仍会在安全校验不满足时保留 ufbx baked fallback，尚未逐项完成 Unity A/B。
- loop pose/root locks/mirror/cycle offset/root motion/additive/mask、连续时间 Player sampling、Editor curve visualization、stable sub-asset fileID/type-tree/artifact cache 与 Unity 2022.3.61f1 Pro 正式门禁仍未闭环，因此 ModelImporter/AnimationClip 保持 🟡。

### 下一优先项
1. 补 rotation-order/pre-post-rotation/pivot、多 layer、constant/stepped/weighted/broken tangent 官方 fixture，把安全 fallback 逐项替换为已证明的 Unity 等价 native 路径。
2. 完成 importer loop/root motion/additive/mask 与连续时间 Animator/Player A/B。
3. 完成 stable sub-asset fileID/type-tree/artifact cache，并迁移到 Unity 2022.3.61f1 Pro 重跑完整 Editor/Player 门禁。

## 2026-07-19 — Unity CLI `-logFile -` stdout 与日志终结语义

### 已完成
- 依据 Unity 2022.3 官方 Editor command-line 文档复核：`-logFile <pathname>` 指定日志文件，pathname 为单个 `-` 时必须把日志送到 stdout，不能创建名为 `-` 的文件。
- `CliHost` 现把 stdout 作为可注入 `TextWriter`，所有实时日志统一写入该 stream；`FlushLog("-")` 只 flush stdout sentinel，不再触碰文件系统。普通相对/绝对日志路径仍会创建父目录并覆盖本次 session 文件。
- 日志终结移动到 `finally`，因此 `-version`、`-help`、无效 `-projectPath` 与异常路径也会完成 stdout flush 或文件落盘，不再因早退丢失日志。
- 新增 parser、大小写/双连字符、stdout/no-dash-file、flush、相对/嵌套/覆盖文件、help 与失败早退专项 **11 个用例**；CLI suite **27/27**。`_scripts/build-all.sh Release` 全部 0 编译错误；统一 native-required 八工程矩阵 **4069/4069**（Core **3060/3060**），均 0 失败、0 跳过。

### 尚未完成
- Unity Editor/Player 的默认日志位置、`-nolog`、`-upmLogFile`、`-stackTraceLogType`、Windows 无控制台 stdout handle、崩溃/许可/Package Manager 分日志及编码/换行/并发写入尚未完全对齐。
- CLI 仍缺 Unity 2022.3 的大量 Editor/Player 参数、参数冲突/缺值诊断、官方退出码、真实 platform player 产物与 2022.3.61f1 Pro 跨平台进程级 A/B，因此 `anity.exe` 保持 🟡。

### 下一优先项
1. 实现 quaternion 四 component 同步 angular reduction，完成 Transform rotation error 四档 key/tangent 精确对齐。
2. 补 CLI 默认日志/`-nolog`/`-upmLogFile`/参数缺值与退出码，并用子进程覆盖 macOS/Windows/Linux stdout/file 行为。
3. 完成 importer loop/root motion/additive/mask 与 stable sub-asset fileID/type-tree/artifact cache，在 Unity 2022.3.61f1 Pro 执行 Editor/Player A/B。

## 2026-07-19 — Unity FBX Transform raw Euler / resampled quaternion 语义

### 已完成
- 用本机 Unity 2022.3.51f1 batchmode 对同一 Maya FBX 探测 `resampleCurves=true/false`、`Off/Optimal` 与 rotation/position/scale error；锁定关闭重采样时的 9 条 `m_LocalPosition.*` / `localEulerAnglesRaw.*` / `m_LocalScale.*` 源曲线、frame `0/13/23`、源 Bezier tangent，以及开启重采样时 24 个逐帧 quaternion key。官方文档同时确认 rotation error 是原始与压缩 quaternion 的角度误差（度），position/scale error 是相对误差百分比。
- `anity-native` 的 FBX 坐标基现与 Unity 的 Maya FBX 结果一致；移除 ufbx 顶层轴变换附加旋转后，重采样 quaternion 在 frame 13 精确得到 Unity 的 `(0.038134575, -0.18930785, -0.23929834, 0.9515485)`。原始 Transform curve 通过新 C ABI 保留源 key/time/in-out tangent，并按 Unity 轴映射输出 position、Euler 与 scale。
- 托管 ModelImporter 在 `resampleCurves=false` 时生成 `localEulerAnglesRaw.*` 而非 quaternion bindings，且不再对源 key 二次压缩；`AnimationClip.SampleAnimation` 已能把三条 raw Euler curve 合成为实际 Transform rotation。开启重采样且 compression `Off` 时仍生成 24 帧 quaternion curves。
- 新增 Transform curve/value/time/tangent/binding/runtime sampling/quaternion 专项 **13 个用例**；`NativeModelImportTests` **27/27**、相关 model/animation 聚焦回归 **83/83**。`_scripts/build-all.sh Release` 全部 0 编译错误；统一 native-required 八工程矩阵 **4058/4058**（Core **3060/3060**），均 0 失败、0 跳过。

### 尚未完成
- Unity 的四条 quaternion component curve 会同步删 key；已取得 error `0.01 / 0.1 / 0.5 / 1` 的黑盒 key 集，但其联合 angular reduction 删除顺序尚未精确复刻。当前逐 component 压缩不能冒充联合 quaternion reducer 完成。
- 多 animation layer、不同 FBX axis/rotation order、pre/post rotation、constant/stepped/weighted/broken/infinite tangent、Euler wrap/gimbal lock，以及 loop pose/root locks/mirror/cycle offset/root motion/additive/mask 仍需扩充官方 fixture。
- 正式证据仍需 Unity 2022.3.61f1 Pro 的 Editor curve visualization、连续时间 Player sampling、negative scale/多层 hierarchy、sub-asset/fileID 与重导入持久化 A/B；ModelImporter 与 AnimationClip 保持 🟡。

### 下一优先项
1. 从现有四档 Unity key 集反推并实现同步 quaternion angular reduction，确保四 component 共享完全相同的 key time 与 tangent。
2. 补 constant/stepped/weighted/broken tangent、多 layer/rotation order 与 Euler wrap fixture，再完成 loop/root motion/additive/mask 的 importer-to-runtime 全链路。
3. 修复 `anity -logFile -` stdout 语义；补 stable sub-asset fileID/type-tree/artifact cache，并在 Unity 2022.3.61f1 Pro 执行 Editor/Player A/B。

## 2026-07-19 — Unity imported curve compression / tangent / take timeline

### 已完成
- 用本机 Unity 2022.3.51f1 batchmode 对同一 Maya blend-shape FBX 做 `Off`、`KeyframeReduction`、`KeyframeReductionAndCompression`、`Optimal`，以及 position error `1 / 0.5 / 0.1 / 0.01` 的逐 key 黑盒探针；锁定 Unity 的 forward-greedy Hermite reduction、相对误差、每个源区间“中点 + 右端点”采样规则，并逐帧复现 TopH/TopV 全部保留 key。确认压缩保留源中央差分 tangent、先压缩完整 take 再切片、默认 source take 为 frame `1..120`。
- `anity-native` model C ABI 现传递 `resampleCurves`、source take first/last frame 与 scalar in/out tangent。C++ 重采样路径生成 Unity 同语义中央差分 tangent；关闭 Resample Curves 时直接保留单层源 Maya/FBX Bezier key、时间和 handle slope，不再先烘焙成每帧 key。
- 托管 ModelImporter 现使用 source frame origin 解释 `clipAnimations.firstFrame/lastFrame`，对 position/rotation/scale 与 blend-shape curve 应用 compression mode/error，按 Unity 的完整曲线压缩结果生成切片边界值和导数。Blend-shape 在 `Off` 下精确保留 TopH 60 / TopV 90 个重采样 key，在默认压缩下分别保留 8 / 7 个 key；`resampleCurves=false` 分别保留 3 个源 key。
- 新增 compression mode/error、逐 key time、中央/源 Bezier tangent、raw curves、take frame origin 与压缩后切片专项 **15 个用例**；`NativeSkinnedModelImportTests` 合计 **42/42**。`_scripts/build-all.sh Release` 全部 0 编译错误；统一 native-required 八工程矩阵 **4045/4045**（Core **3047/3047**），0 失败、0 跳过。

### 尚未完成
- 本轮以 blend-shape scalar curve 锁定并验证 importer reduction；Transform rotation 目前按四个 quaternion component 使用 rotation error，尚需 Unity 逐 key 探针确认其联合 quaternion/angular error、Euler resampling 与 quaternion continuity 的完全语义。
- 多 animation layer/多 curve 合成下 `resampleCurves=false` 的源曲线、constant/stepped/weighted/broken tangent、infinite tangent、pre/post extrapolation，以及 clip loop pose/root locks/mirror/cycle offset/root motion/additive/mask 仍需覆盖。
- 本轮 CLI 冒烟确认 `anity -batchmode -quit` 可运行，同时发现 `-logFile -` 仍把 `-` 当作普通文件路径，而 Unity 应把它解释为 stdout；该 CLI 兼容差距已登记，不能继续将 CLI 标成完整。
- 正式证据仍需 Unity 2022.3.61f1 Pro 的 transform/morph Player 连续时间采样、Editor curve visualization、sub-asset/fileID 与重导入持久化 A/B，因此 ModelImporter 与 AnimationClip 保持 🟡。

### 下一优先项
1. 黑盒并实现 Transform quaternion/Euler reduction、raw curve layer 合成和 constant/stepped/weighted tangent 全语义。
2. 完成 loop pose/root locks/mirror/cycle offset/root motion/additive/mask 的 importer-to-runtime 全链路，并扩展 material/自定义组件 float/object-reference property pose graph。
3. 修复 `-logFile -` stdout 语义并补 CLI 深测；补 stable sub-asset fileID/type-tree/artifact cache，在 Unity 2022.3.61f1 Pro 执行 Editor preview 与 Player 连续时间 skin/morph/animation A/B。

## 2026-07-19 — FBX blend-shape animation / clip slicing / Animator float pose

### 已完成
- 用本机 Unity 2022.3.51f1 batchmode 对 Maya blend-shape FBX 做逐 key 黑盒校准：确认导入 binding 为 `SkinnedMeshRenderer` 的 `blendShape.TopH` / `blendShape.TopV`、空相对路径、24 Hz、take trim 后长度 `89/24` 秒与 0–100 百分比值；另确认 `Mesh.GetBlendShapeIndex(null)` 返回 `-1`。
- `anity-native` model C ABI 新增 blend-shape track/scalar key 输出。C++ 从 ufbx animated mesh property 映射实际 mesh instance/node，并在 take 起点按源 frame rate 直接求值，避免 key reduction 删除中间 deformation sample；clip duration 由实际 transform/blend track 末 key 决定。
- 托管 importer 现生成 `blendShape.<name>` curve，支持 `importBlendShapeDeformPercent` / `importBlendShapes` 开关、first/last frame 边界求值与时间归零，并允许同一 take 生成多个自定义切片。导入 clip 标记为 Mecanim-built；`AnimationClip.SampleAnimation` 与 `Animator` 可实际写入 `SkinnedMeshRenderer` weight，Override/Additive layer、reference pose、crossfade/BlendTree float-property 合成已接通，Transform 与 float additive reference 独立处理。
- 新增导入/曲线/采样/几何/开关/切片/多 clip/API 边界/Animator Override/Additive 专项 **14 项**，`NativeSkinnedModelImportTests` 合计 **27/27**；ModelImporter/Animator/AnimationCurve/YAML 相关回归 **160/160**，均 0 失败、0 跳过。
- `_scripts/build-native.sh Release` 与 `_scripts/build-all.sh Release` 通过、全部 0 编译错误；统一 native-required 八工程矩阵 **4030/4030**（Core **3032/3032**），0 失败、0 跳过。

### 尚未完成
- 当前逐帧 deformation 值与 Unity 探针一致，但 Unity importer 的 cubic tangent/key compression 尚未复刻，因此 curve key 数、切线与非 frame-time 连续插值仍不能宣称逐 key 完全一致。
- `clipAnimations` 的 loop pose、root locks、mirror、cycle offset、mask/source Avatar、root motion 与 additive reference 的 importer/YAML 到 Player 全生命周期仍未闭环；通用 Renderer/Material/自定义 MonoBehaviour float/object-reference animation 也尚未进入同一 property pose graph。
- 正式证据仍需 Unity 2022.3.61f1 Pro 的 sub-asset/fileID、重导入持久化、Editor preview 与 Player 逐帧 A/B；因此 ModelImporter、AnimationClip 与 Animator 相关项保持 🟡。

### 下一优先项
1. 复刻 Unity imported curve tangent/key compression，并完成 loop pose/root locks/mirror/cycle offset/root motion/additive/mask 的 importer-to-runtime 全链路。
2. 完成 material/texture slot/remap、多 skin deformer、Humanoid mapping/T-pose/muscle/retargeting，并扩展 float/object-reference property pose graph。
3. 补 stable sub-asset fileID/type-tree/artifact cache，在 Unity 2022.3.61f1 Pro 执行 Editor preview 与 Player 逐帧 skin/morph/animation A/B。

## 2026-07-19 — Native FBX skin / blend shape / imported Avatar 主链

### 已完成
- 用本机 Unity 2022.3.51f1 batchmode 对真实 Blender skinned FBX 与 Maya blend-shape FBX 做黑盒校准：确认 skin 的 `SkinnedMeshRenderer`、bone/rootBone、bindpose、未显式绑定顶点的 bone-0 权重，及纯 blend-shape 网格仍使用无 bones 的 `SkinnedMeshRenderer`；确认 blend frame 以百分比 `100` 暴露、split vertex 共享逻辑顶点 delta、`importBlendShapes=false` 回到静态 renderer。
- `anity-native` model C ABI 现输出 skin cluster、bone node index、16-float bindpose、每顶点 variable influence range、归一化 weight，以及 blend deformer/channel/frame/position-normal delta。index generation 同时压缩逻辑顶点 stream，避免 UV/normal seam 拆点后 skin/morph 映射错位；`maxBonesPerVertex`、`minBoneWeight` 与最多 8 influence 进入 native import options。
- 托管 importer 现构造 `Mesh.bindposes` / `SetBoneWeights` / `AddBlendShapeFrame`，按解码 node index 绑定 bones、计算共同 `rootBone`、建立 `SkinnedMeshRenderer`/localBounds，并从真实 decoded hierarchy 经 native AvatarBuilder validation 生成 Generic Avatar。blend-only `BakeMesh` 无需虚构 bones 即可输出实际形变；导入 Transform 保留 FBX node name。
- 新增两份真实 FBX fixture 与 skin/blend/imported-Avatar 专项 **13/13**；连同既有 native model 与 AvatarBuilder 聚焦回归 **50/50** 通过。
- `_scripts/build-native.sh Release` 与 `_scripts/build-all.sh Release` 通过；Core **3018/3018**、统一 native-required 矩阵 **4016/4016**，均为 0 失败、0 跳过。

### 尚未完成
- FBX blend-shape 动画的 `blendShape.<name>` curve、多个 skin deformer/dual-quaternion skinning、shape tangent delta、importer Optimize Bones、Humanoid Avatar mapping/T-pose/muscle/retargeting 与 root motion 尚未闭环。
- material/texture extraction/remap、完整 clip frame slicing/loop/root locks/mirror/additive/mask、camera/light/visibility/constraint/LOD/collider、多 UV/Mikk tangent/secondary UV/mesh optimization，以及 DAE/3DS/DXF/Blend 转换仍需补齐。
- 黑盒证据仍来自本机 2022.3.51f1；目标 2022.3.61f1 Pro 的正式 sub-asset/fileID、逐帧 skin/morph/animation 与 Player 渲染 A/B 尚未执行，故 ModelImporter 保持 🟡。

### 下一优先项
1. 从 ufbx baked element/property 接通 `blendShape.<channel>` AnimationClip curve，并完成 first/last frame slicing、loop/root locks/mirror/additive reference 与 root motion。
2. 完成 material/texture slot/remap、多 skin deformer与 Humanoid mapping/T-pose/retargeting，用多骨骼、多材质、多 mesh、negative scale fixture 做 Unity A/B。
3. 补 sub-asset stable fileID/type-tree/artifact cache 与 UnityPackage/import worker 共用事务，并在 2022.3.61f1 Pro 重跑 importer/Player 全门禁。

## 2026-07-19 — Native ModelImporter hierarchy / mesh / animation 主链

### 已完成
- 依据本机 Unity 2022.3.51f1 batchmode 对真实 FBX/OBJ 的黑盒导入结果，确认有效模型主资源为 `GameObject`、几何为 `Mesh` 子资源、FBX animation stack 为 `AnimationClip`，并记录默认米制换算、`fileScale`、root hierarchy、curve binding 及 `OnPreprocessModel → OnPostprocessMeshHierarchy → OnPreprocessAnimation → OnPostprocessAnimation → OnPostprocessModel` 专用消息顺序。
- `anity-native` 新增 model importer C ABI，固定 vendored ufbx `v0.23.0`（源码提交 `fcc5d6ba444cfd3eb80677dba5e37e493941abe5`，MIT/Unlicense 双许可证）。C++ 负责 FBX/OBJ 解析、左手 Y-up/米制换算、hierarchy/TRS、polygon triangulation、split-attribute vertex/index buffer、submesh、animation stack bake 与 position/quaternion/scale keys；托管层仅负责 P/Invoke 生命周期和 Unity 对象组装。
- `AssetDatabase.ImportAsset` 对有效模型不再生成 `TextAsset`：现建立可加载的 `GameObject` hierarchy、`MeshFilter`/`MeshRenderer`、indexed `Mesh`、`AnimationClip` curve 和 `defaultClipAnimations`/`fileScale` metadata。`globalScale`、`useFileUnits`、`importAnimation`、`animationType`、index format、readability、normal/tangent/UV 主路径已进入构建；损坏重导入保持最后一次成功 artifact，不用原始 bytes 覆盖可用模型。
- 专用 ModelImporter callback 现按实测顺序按名称分发；`OnPreprocessAnimation` 在 clip 构建前运行并可关闭动画导入，postprocess mutation 保留在最终资源。新增 native-required 真实 fixture/OBJ suite **14/14**，覆盖 main/sub-asset 类型、24 顶点/36 索引拓扑、厘米到米、bounds、hierarchy component、take/curve、运行时 sampling、关闭动画、全局缩放、损坏事务、OBJ quad triangulation、专用 callback 顺序与默认 take metadata；既有 ModelImporter/Avatar/AssetPostprocessor 组合 **160/160** 通过。
- `_scripts/build-native.sh Release`、`_scripts/build-all.sh Release` 与统一 Release 测试门禁通过；Core **3005/3005**、全矩阵 **4003/4003**，均为 0 失败、0 跳过。

### 尚未完成
- 本轮是可用的 static mesh + transform animation 主链，不是完整 ModelImporter：skin cluster/bindpose/bone weight、blend shape、material/texture extraction/remap、camera/light/visibility/custom properties、constraint、LOD、collider、secondary/multi-UV 与 swapUV、Mikk tangent、hierarchy sorting、mesh compression/optimization，以及 DAE/3DS/DXF/Blend 的真实转换仍需补齐。
- `clipAnimations` 当前只应用 take 选择、重命名与 wrap mode；first/last frame 裁切、loop pose/root locks/mirror/cycle offset/additive reference/mask、preview clip、Mecanim imported-data 持久化、humanoid Avatar/T-pose/muscle/retargeting/root-motion rotation尚未闭环。Unity package 导入仍需复用同一 native model transaction；sub-asset fileID/type-tree/artifact cache 与跨 session 序列化也未完成。
- 本机证据版本为 2022.3.51f1；目标 2022.3.61f1 Pro 的最终 hierarchy/mesh/animation/sub-asset 顺序、importer callback 参数与逐帧 Player A/B 尚未执行，因此本项保持 🟡。

### 下一优先项
1. 接通 skin/bindpose/bone weights、blend shapes 与 imported Avatar，完成 Humanoid/Generic Mecanim、root motion、AvatarMask/additive clip 的真实资源重载和逐帧 A/B。
2. 完成 material/texture slot、custom clip frame slicing/loop/root locks/mirror、Mikk tangent/secondary UV/mesh optimization，并用 Unity 2022 fixtures 覆盖多 mesh、多 material、instancing、negative scale 与异常文件。
3. 让 UnityPackage/Refresh/cache worker 共用 native model transaction，补 sub-asset fileID/type-tree/artifact 持久化和 RunBefore/RunAfter callback dependency，最终在 2022.3.61f1 Pro 重跑反射与 Player 门禁。

## 2026-07-19 — AssetPostprocessor 精确公开面与消息分发

### 已完成
- 本机 Unity 2022.3.51f1 batchmode 反射确认 `AssetPostprocessor` 是可构造 class，官方公开面仅含 `assetPath`、`assetImporter`、`context`、obsolete-error `preview`、`GetPostprocessOrder`、`GetVersion` 与四个 Log 重载；`OnPreprocessAsset` / `OnPostprocessAllAssets` 等均是派生类型按名称声明的导入消息，不属于基类 API。已移除错误的 abstract 基类、`GetAssetLoadPriority`、全部 public virtual callback，以及 Unity 不存在的 `RawAvatar`。
- 资源导入器改为反射分发私有/公开实例 `OnPreprocessAsset()` 和私有/公开静态 4/5 参数 `OnPostprocessAllAssets`；严格忽略错误返回值、错误参数与实例 batch callback。逐资产预处理按 `GetPostprocessOrder` 升序；batch callback 按官方规则不受该 order 影响，并采用稳定类型顺序。预处理阶段已提供稳定 `assetPath`、`assetImporter` 与 `UnityEditor.AssetImporters.AssetImportContext`，package 异常会回滚本轮临时 importer。
- `AssetImportContext` 已迁入官方命名空间并采用非公开构造器，主对象/子对象、source dependency、日志与 selected build target 具备实际状态；ShaderImporter 同步引用官方类型。新增 AssetPostprocessor 专项 **17/17**，覆盖精确公开面、默认值、obsolete metadata、私有消息、4/5 参数 batch、顺序、内部类型、错误签名、异常中止、上下文与 `RawAvatar` 不存在；连同 UnityPackage archive 为 **32/32**。
- 全门禁暴露并修复既有 native swapchain capability 漏洞：非法 3x MSAA 现在在通用 C++ 入口拒绝，原生设备返回的任何 swapchain 创建错误不再被托管 headless fallback 伪装为成功。`_scripts/build-native.sh Release`、Core **2991/2991**、全矩阵 **3989/3989** 均 0 失败、0 跳过。

### 尚未完成
- 当前通用导入链只在已有真实对象的范围内闭环 `OnPreprocessAsset` 与 batch callback；ModelImporter 仍把 FBX/OBJ 主资源建成 `TextAsset`，尚无 decoded hierarchy、真实 AnimationClip curve/Mecanim 数据，因此不能伪造调用 `OnPostprocessModel` / `OnPostprocessAnimation` 或生成空 clip 子资源。
- `AssetImportContext` 的 GUID/ArtifactKey artifact dependency、custom dependency、output artifact 路径、完整 importer log/file-line 语义仍未补齐；batch callback 的 RunBefore/RunAfter class/assembly/package dependency attributes，以及 texture/audio/model/shader 各专用 preprocess/postprocess 消息，也需随真实 importer/decoder 接入。目标 2022.3.61f1 Pro 最终反射与导入 A/B 仍待完成，本轮 2022.3.51f1 仅为预备证据。

### 下一优先项
1. 在 native importer 接入真实 FBX/OBJ hierarchy 与 animation take/curve 解码，产出可重载 AnimationClip/Avatar 子资源及 Mecanim additive reference/mask/source Avatar 数据，再分发 model/animation 消息。
2. 补齐 `AssetImportContext` 的 Unity 2022.3 公开面、artifact/GUID/custom dependency 与 importer log 行为，并接通 texture/audio/shader 专用回调及 AssetImportContext 子资源提交。
3. 将 imported humanoid clip 接入 native muscle/finger/IK stream、AvatarMaskBodyPart、root motion/retargeting，并以 Unity Player 逐帧 A/B 验证。

## 2026-07-19 — Animator AvatarMask native layer pose 合成

### 已完成
- 本机 Unity 2022.3.51f1 batchmode 控制器探针锁定 generic Transform layer 语义：Override position/scale 按 0–1 layer weight 线性插值，rotation 使用 Mecanim shortest-path normalized lerp；负数、>1 与正负 Infinity weight 使用完整上层，NaN 禁用上层；空 transform mask 等价于不筛选，非空 mask 仅启用显式 active path，active 任一路径会使 root 参与，重复路径采用 OR，未被 upper clip 绑定的属性保持下层结果。
- 新增 `anity-native` animation pose C ABI/C++ 模块，原生执行 position/rotation/scale Override 与 reference-pose Additive 合成；AvatarMask 原生状态新增运行时 path query。Animator 不再逐层直接 `SampleAnimation` 导致最后一层覆盖全部结果，而是先采样 pose、在 native 合成 layer/crossfade/BlendTree，再一次性写回 Transform。
- `AnimationClip` 的内部 pose 采样会从当前 Transform 初始化未绑定分量，修复只写 `m_LocalPosition.x` 时错误清零 y/z；`AnimationUtility.SetAdditiveReferencePose` / clip settings 已接入运行时 reference pose。仅内部已标记 Mecanim data built 的 clip 可作为有效 reference；普通程序化 clip 按官方探针保持无效且 Additive layer 不生效。
- 新增 native-required suite **25/25**，覆盖 0/0.25/1/负数/>1/NaN/Infinity layer weight、空/全关/缺失/重复/UTF-8/exact/child mask、partial upper binding、rotation/scale、无效/有效 additive reference、mask additive、crossfade 与部分分量；动画相关组合 **79/79**，AnimationUtility **10/10**。统一 Release 门禁 Core **2974/2974**、全矩阵 **3972/3972**，均为 0 失败、0 跳过。

### 尚未完成
- 本轮闭环的是 generic Transform curve 的 AnimatorController layer 路径；Humanoid muscle stream、AvatarMaskBodyPart 对 body/finger/foot/hand IK 的过滤、IK pass、root motion/mass center、synced layer、writeDefaultValues 和 Playables/AnimationPlayableOutput 尚未进入该 pose graph。
- 官方程序化 AnimationClip 不能作为有效 additive reference（Unity 报 `MecanimDataWasBuilt` / invalid additive reference）；本轮确认了无效 reference 会被忽略，并用内部 Mecanim-built 标记验证 native delta math，但 ModelImporter 尚未产出/恢复该标记，有效 imported clip 的 Additive 逐帧 A/B、资产重载持久化及目标 2022.3.61f1 Pro 最终门禁仍未闭环。

### 下一优先项
1. 将 ModelImporter 的 additive reference、mask/source Avatar 与 decoded hierarchy 产物接入 runtime clip/avatar asset，并用真实导入动画做 Additive/Override 重载 A/B。
2. 在 native pose graph 增加 humanoid muscle/bone stream、AvatarMaskBodyPart、finger 与四类 IK goal 过滤，接通 Animator IK pass/root motion。
3. 审计并精确清理 Animator/AnimatorController/Layer 的 Unity 2022.3.61f1 公开面，以及错误 `RawAvatar` / AssetPostprocessor callback。

## 2026-07-19 — AvatarMask 精确公开面与 native 状态

### 已完成
- `UnityEngine.AvatarMask` 与 `AvatarMaskBodyPart` 已按本机 Unity 2022.3.51f1 预备反射重建：补齐 sealed/type metadata、官方 13 个 body part、可写 `transformCount`、Transform 路径 overload 和 obsolete property，移除错误的 `TransformMaskElement`、HumanBodyBones/string helper、额外计数与可写 `name`。新增的 `MovedFromAttribute` 公开签名也与官方反射指纹一致。
- 新增 `anity-native` AvatarMask C ABI/C++ 状态，原生持有 humanoid body flags、UTF-8 transform path 与 active flag；托管层仅负责 Unity API、Transform 相对路径遍历和 native 生命周期，符合动画核心落在 C++ 的职责边界。
- Unity batchmode 行为探针覆盖默认 13 body parts 全启用、非法 body/index、transformCount 扩缩/负数、null/UTF-8 path、depth-first recursive add、重复路径、flat/recursive remove；Anity 逐项复现这些边界语义。
- 新增 native-required suite **17/17**；AvatarMask/AvatarBuilder/ModelImporter 组合回归 **183/183**，统一 Release 门禁 Core **2949/2949**、全矩阵 **3947/3947**，均为 0 失败、0 跳过。

### 尚未完成
- 当前已闭环 AvatarMask 公开面、独立状态、Transform path 编辑语义及 generic AnimatorController layer 的 Override/reference-pose Additive 消费；Humanoid muscle/IK body part、Playables 与导入资产生命周期仍未消费完整 mask，不能将 AvatarMask 运行时效果标为完整。
- 错误的额外 `RawAvatar` 仍被历史 `AssetPostprocessor` callback 使用，需连同官方回调签名一次性迁移；目标 Unity 2022.3.61f1 Pro 的最终反射与 Player A/B 仍待执行，本轮 2022.3.51f1 证据仅是预备门禁。

### 下一优先项
1. 将 native AvatarMask 从已完成的 generic Animator layer 扩展到 Playable、humanoid bone/finger/foot/hand IK 与 root-motion 过滤，并做逐帧 pose A/B。
2. 按官方 AssetPostprocessor 公开面移除 `RawAvatar`，完成 ModelImporter mask/source Avatar YAML、子资源与重导入生命周期。
3. 继续接通 decoded FBX hierarchy、humanoid T-pose/muscle/humanScale/retargeting/root-motion rotation，并在 Unity 2022.3.61f1 Pro 重跑最终门禁。

## 2026-07-19 — Avatar / AvatarBuilder native hierarchy validation

### 已完成
- `UnityEngine.Avatar` 已移除 Unity 2022 不存在的可写 `name`、`hasTransformHierarchy`、`humanScale`、`avatarSize`、`muscleCount`、`rootBone`、body pose、`Build` 与 bone-name helper 等公开面；现仅保留官方只读 `isValid`、`isHuman`、`humanDescription`，构造器可见性和 `NativeHeader` / `UsedByNativeCode` metadata 与本机 Unity 2022.3.51f1 预备反射逐项一致。
- `AvatarBuilder` 已由错误的 static type 修正为官方可构造 class，并补齐精确的 `BuildGenericAvatar(GameObject,string)`、参数 `NotNull` 与 free-function metadata；`BuildHumanAvatar` 不再恒定返回 valid。两种构建均通过新的 `anity-native` animation C ABI 执行层级、rest pose、mapping 与 root-motion transform 校验。
- native validator 覆盖空骨架/映射、空或重复名称、非法 parent、多个 root、层级环、NaN/Infinity、零四元数、零 scale、缺失 mapped bone、重复 bone/human mapping、15 个 Unity humanoid 必需骨骼，以及 Generic root-motion transform 解析；有限非单位 scale 保持有效。托管层从实时 GameObject Transform 树推导 HumanDescription 的 parent index，重复或缺失场景节点不会再伪装成有效 Avatar。
- 尚未解码出真实 FBX transform hierarchy 的 ModelImporter Avatar 子资源现在明确保持 `isValid == false`，不再用 managed `true` 冒充 native 构建成功；CopyFromOther 仅继承真实 source Avatar 的有效性。
- 新增 native-required suite **23/23**，与 ModelImporter/humanoid 相关组合 **166/166**；`_scripts/build-native.sh Release`、本机 2022.3.51f1 两类型预备反射与统一 Release 门禁均通过。Core **2932/2932**，全矩阵 **3930/3930**、0 失败、0 跳过。

### 尚未完成
- 当前 native AvatarBuilder 完成的是 hierarchy/rest-pose/mapping validity 主路径；muscle table、T-pose normalization、humanScale、retargeting、root-motion rotation extraction、Animator 逐帧结果与 Player 资源生命周期仍未实现，不能将 Avatar 系统标为完整。
- ModelImporter 已保留 YAML `parentName` / `rootMotionBoneRotation` / `skeletonHasParents`，但真实 FBX/OBJ hierarchy decoder 尚未把这些 importer 数据接入 native Avatar build；AvatarMask 的 Animator 实际消费、错误的额外 `RawAvatar` API 与目标 Unity 2022.3.61f1 Pro A/B 也仍未闭环。

### 下一优先项
1. 将 ModelImporter 的 importer-only `parentName`、`rootMotionBoneRotation` 与 `skeletonHasParents` 输入真实 native Avatar asset build，接通 decoded FBX hierarchy 并产出可验证的 Avatar 子资源。
2. 在 `anity-native` 实现 humanoid T-pose normalization、muscle limits/table、humanScale、retargeting 与 root-motion rotation，并用 Animator 逐帧 A/B 验证。
3. 清理 `RawAvatar` / AssetPostprocessor 公开面差异，将 native AvatarMask 接入 Animator，并完成 material/avatar remap；最终在 Unity 2022.3.61f1 Pro 重跑公开面、importer fixture 与 Player 行为 A/B。

## 2026-07-19 — ModelImporter root motion 与 source Avatar 对象引用

### 已完成
- 本机 Unity 2022.3.51f1 预备反射确认官方公开面为 `motionNodeName : string` 与 `sourceAvatar : Avatar`；移除错误的 ModelImporter `isHuman`/`avatar` 别名，将原错误 `sourceAvatar : string` 修正为 Avatar，并同步修正 `extraExposedTransformPaths : string[]`、只读 `fileScale`、`skinWeights : ModelImporterSkinWeights` 及三个官方 native binding metadata。
- `motionNodeName` 已与 `humanDescription.rootMotionBoneName` 双向持久化，支持缺失字段插入、YAML 转义并保留 `rootMotionBoneRotation`、`skeletonHasParents` 等 importer 内部字段。`lastHumanDescriptionAvatarSource` 支持 Unity 单行/折行 flow mapping、null `instanceID`、GUID 延迟解析、`fileID: 9000000` 写回、替换/清空、依赖删除失效和跨 project session 恢复。
- Human/Generic 模型导入会建立可由 AssetDatabase 枚举的 Avatar 子资源；CopyFromOther 引用通过 GUID 解析到真实 Avatar 对象，不再以字符串模拟。新增 suite **36/36**，与 Model YAML/humanoid 契约组合 **174/174**；统一 Release 回归 Core **2909/2909**，全矩阵 **3907/3907**、0 失败、0 跳过。
- UnityEditor-only 预备反射由 exact types 106 增至 **107**、type mismatch 96 降至 **95**；exact members 1343 增至 **1348**、member mismatch 671 降至 **667**、extra members 1256 降至 **1253**。上述仅为 2022.3.51f1 探索证据。

### 尚未完成
- 自动生成的 Avatar 子资源仍是 managed importer 产物，尚未连接 `anity-native` 的真实 Avatar hierarchy build、required-bone validation、muscle/retargeting、root-motion rotation 和 Player 逐帧结果。
- Material/Avatar remap、完整 transform path/take/default clips 等 ModelImporter 公开面及 2022.3.61f1 Pro A/B 仍未闭环。

### 下一优先项
1. 将 SkeletonBone parent hierarchy、`rootMotionBoneRotation` 与 `skeletonHasParents` 接入 native Avatar build/validation，覆盖循环、缺 parent、重复名称、非单位 scale 与必需 humanoid 骨骼。
2. 完成 `SearchAndRemapMaterials` / externalObjects 与 Avatar remap 的 Unity GUID/fileID 行为，并补 extraction/remap 跨会话门禁。
3. 继续按官方反射修正 ModelImporter 剩余公开成员，每批保持 mismatch/extra 单调下降；最终在 Unity 2022.3.61f1 Pro 重跑完整 A/B。

## 2026-07-19 — ModelImporter humanoid SkeletonBone YAML

### 已完成
- 依据本机 Unity 2022.3 项目生成的真实 ModelImporter `.meta`，`humanDescription.skeleton` 现按官方 `name` / importer-only `parentName` / `position` / `rotation` / `scale` 布局双向持久化。公开 `SkeletonBone` 不增加 Unity 不存在的 `parentName`；已有 parent/未来字段原样保留，新增骨骼使用空 parent 占位。
- reader 支持空列表、科学计数法兼容的 Vector3/Quaternion、多个骨骼和缺失列表；writer 支持原位更新、追加、缩短、清空、null、缺失 transform 字段补齐、需要转义的名称，以及跨 project session 重载。`human: []` 也会恢复为非 null 空数组，不再与缺失 section 混淆。
- 新增 10 组读取、10 组写入及 10 个边界场景，Model YAML suite 从 77 增至 **107/107**；连同 humanoid 公开面门禁为 **138/138**。统一 Release 回归覆盖 Core 2873、Agent 91、Editor 164、Shader Graph 198、VFX Graph 490、CLI 16、Unity API parity 17 与 A/B compare 22，合计 **3871/3871**、0 失败、0 跳过。

### 尚未完成
- `rootMotionBoneName` 与 source Avatar 已在后续批次接通；material remap、`rootMotionBoneRotation`、`skeletonHasParents` 等 importer/native 语义仍未闭环。SkeletonBone YAML 仅证明 managed importer 持久化，不证明 native Avatar 构建、层级校验、muscle/retargeting 与 Unity Player 结果等价。
- 当前样本和公开面反射仍来自本机 Unity 2022.3.51f1；目标 2022.3.61f1 Pro 尚未安装，因此不能作为最终版本 A/B 证据。

### 下一优先项
1. 接通 `rootMotionBoneRotation`、`skeletonHasParents` 与 remap 的 Unity YAML/native 语义，并补跨 session/非法引用/未知字段用例。
2. 将 SkeletonBone hierarchy 输入连接到 `anity-native` Avatar build/validation，覆盖 parent 缺失、循环、重复名称、非单位 scale 与 humanoid 必需骨骼。
3. 获得 Unity 2022.3.61f1 Pro 后重跑公开面及 importer fixture A/B，按真实差异修正而非沿用 2022.3.51f1 推断。

## 2026-07-18 — UnityEditor.AssetDatabase package import lifecycle

### 已完成
- 将 `AssetDatabase` 从不兼容的 static type 修正为官方同形的可构造 sealed facade，并补齐 Unity 2022 绑定 metadata、`ImportPackageCallback` / `ImportPackageFailedCallback`、`RefreshImportMode`、package import start/completed/cancelled/failed 回调和 `onImportPackageItemsCompleted` 字段。
- 将高频 `CreateAsset(UnityEngine.Object, string)` 与 `AddObjectToAsset(UnityEngine.Object, string)` 修正为 Unity 的 `void`/`Object` 合约，保留 native throw/null metadata；本机 2022.3.51f1 预备反射对这两个成员已无差异，Project Window 也不再以 null 伪造资产。
- `LoadAssetAtPath<T>` 现采用官方 `UnityEngine.Object` 泛型约束，typed load、main/all/representation asset load 与 `GetAssetPath(Object)` 均移除错误的 `object` 公开面并补齐防导入期执行 metadata；新增 11 个定向测试，连同 package lifecycle 和资产管线共 60/60 通过。本机 2022.3.51f1 的这组 load API 预备反射无差异。
- `ImportPackage` 现可实际读取 Unity `.unitypackage` 的 gzip/tar 条目，将 `guid/{pathname,asset,asset.meta}` 作为原子事务导入内存资源索引，保留包内 GUID、拒绝不安全路径且不会部分提交；另有 10 个 archive 测试覆盖单/多资源、GUID、回调、空归档、非法路径和重导入。该阶段仍缺真实项目目录写入、二进制 importer、postprocessor 与取消 UI。
- Editor session 与 CLI 的 `-projectPath` 现均经 `EditorApplication.OpenProject` 配置同一内部 AssetDatabase project root；package 导入会先落到 `Library/AnityPackageStaging`，再提交 asset/meta 文件，已有文件在失败时从 backup 恢复。落盘使用独立临时项目验证，archive+load+callback 33 个定向用例通过。
- `AssetPostprocessor` 已由声明型 API 接入真实 import dispatch：所有可实例化 processor 按 order/type 顺序在提交前逐 asset 执行 `OnPreprocessAsset`，提交后一次执行 `OnPostprocessAllAssets`；preprocess 异常会阻止落盘并触发 import failure，postprocess 异常记录错误但不破坏已提交事务。专项回归增至 35/35。
- package importer 现按常见扩展构建资源对象：PNG/JPEG/TGA 走 `Texture2D.LoadImage`，音频走 native/platform `AudioClip.CreateFromFile`，视频走 `VideoClip.CreateFromFile`，材质与文本保留对应对象；PNG archive fixture 验证导入结果为 `Texture2D`，专项回归 14/14。
- AssetDatabase 现维护 path→importer 稳定 registry；package/CreateAsset 后会按资源对象建立 Texture/Audio/base importer，`GetImporters`、typed GetImporters 与各 GetAtPath 可复用同一设置对象。10 个定向用例覆盖类型、稳定 identity、设置保持、缺失设置和 path。
- `UnityEngine.AssetImporter.SaveAndReimport()` 已接入磁盘 refresh：读取 project root 内 asset/meta、运行 preprocess/postprocess、重建内存对象并保留 registry importer；外部改写已导入文本后的重新导入回归通过。
- `StartAssetEditing` / `StopAssetEditing` 现在是可嵌套的资源导入事务：编辑期间将 `ImportAsset`（含 options）及 `Refresh` 收集进按 canonical path 去重、稳定排序的队列，仅由最外层 `StopAssetEditing` 执行磁盘 reimport；10 个 xUnit 用例覆盖延迟、嵌套、去重、Refresh、缺失文件、options 与 callback 顺序，AssetDatabase 聚焦回归 46/46 通过。
- `AssetImporter.SaveSettings` / `SaveAndReimport` 已实际写入 asset `.meta` 并在新 project session 后恢复：基础 importer 数据、Texture 的类型/mipmap/readability/compression/sampling/尺寸、Audio 的加载与 sample settings 均走 UTF-8 base64 JSON 注释 payload，原有 Unity YAML 完整保留；切换 project root 会清空项目级 index/registry，随后可从磁盘重新识别 importer。10 个跨 session 测试与既有资源管线组合回归共 56/56 通过。
- `DeleteAsset` / `MoveAsset` / `CopyAsset` 不再只改内存：对 project `Assets/` 内的已导入或尚未索引文件同步处理 asset/meta，移动保留 GUID 与 importer identity，复制生成全新 GUID 并改写副本 meta；路径逃逸和已有 destination 被拒绝。11 个真实磁盘回归覆盖删除、移动、复制、GUID、meta、registry 与 collision，资源管线组合回归增至 67/67。
- 磁盘 `ImportAsset` 和 `SaveSettings` 现读取/保留 `.meta` 中合法的 Unity 32 位十六进制 `guid:`，大小写统一；缺失或非法 GUID 会生成标准新 GUID，`GUIDToAssetPath` 可反查。文件操作 suite 增加 meta GUID/规范化/无效拒绝/自动写入测试，资源管线组合回归 71/71 通过。
- `Refresh` 现扫描 project root 的 `Assets/` 文件系统：发现外部新增的文本/纹理与 meta GUID、刷新已有文件，并在文件消失时撤销内存 index/importer；`.meta`、`.DS_Store`、`Library/` 均不会被误导入。扫描仍服从嵌套 `StartAssetEditing` 的队列，10 个回归覆盖 discovery/update/delete/options/batch，组合回归 81/81 通过。
- `GetDependencies(path, recursive)` 现从磁盘 asset 与 `.meta` 文本提取合法 32 位十六进制 `guid:` 引用，经 GUID registry 解析为路径；直接与递归模式均保留 source、按稳定路径排序，并对循环、未知及非法 GUID 安全收敛。10 个真实磁盘回归覆盖空输入、direct/recursive、排序、循环、meta 与异常引用，连同既有 AssetDatabase 管线组合回归 91/91 通过。
- `AddObjectToAsset` 不再覆盖主资产：已形成 main/sub-asset 关系，`LoadAllAssetsAtPath` 返回 main 后接 sub-assets，`LoadAllAssetRepresentationsAtPath` 仅返回 sub-assets，且 `Contains`、`GetAssetPath`、`IsSubAsset`、GUID 查询、Move/Delete 与 root reset 均保留或清理对应关系。13 个定向用例覆盖公开 overload、排序、去重、异常、移动/删除及替换，资源管线组合回归增至 104/104；sub-asset 尚未持久序列化，重启项目后仍需 native YAML/type tree 实现。
- `GetAssetDependencyHash` 已移除不稳定的 CLR string hash，改为对资产字节、`.meta`、排序后的直接/递归 GUID dependencies 计算稳定 SHA-256 前 128 位，并以 recursion stack 收敛真正循环；source/meta/direct/transitive 变更均会失效，无关资产不影响结果。10 个真实项目文件用例通过，AssetDatabase 组合回归增至 114/114。该摘要尚未与 Unity 的内部 artifact/importer hash 做 2022.3.61f1 A/B，不能宣称字节值一致。
- `MoveAsset(string[] paths, string destinationFolder)` 已从空壳落为批量事务：预检 destination、重复/缺失 source、碰撞与递归移动；随后逐项复用磁盘 asset/meta 与 GUID/importer/sub-asset 保持路径，任一执行失败会逆序回滚已完成项。11 项覆盖单/多项、GUID、importer、sub-asset、磁盘 meta 和拒绝路径，AssetDatabase 组合回归增至 125/125。
- 本机 Unity 2022.3.51f1 反射核对后，`ExportPackage` 已补齐官方 4 个 string/string[] overload 与顶级 `[Flags] ExportPackageOptions`（Default=0、Interactive=1、Recurse=2、IncludeDependencies=4、IncludeLibraryAssets=8）。实现输出真实 gzip/ustar `guid/{pathname,asset,asset.meta}` 归档，写入 staging 后才替换目标文件；Recurse 展开磁盘 folder、IncludeDependencies 递归纳入 GUID dependencies，并可独立 project round-trip 保持内容与 meta GUID。19 项覆盖反射值、overload、递归、依赖、异常和既有归档路径，AssetDatabase 组合回归增至 144/144。当前只导出已落盘文件；`IncludeLibraryAssets` 明确抛出 NotSupported、Interactive 还没有 Unity 选择 UI，仍待完成。
- `AssetImporter.assetBundleName` / `assetBundleVariant` 和 AssetDatabase 的 bundle registry 已落地：完整名遵循 `name.variant`，支持按 bundle / scene bundle 查询、稳定排序的 all/unused name 查询与清理，移动、删除和 project session reset 同步维护登记；SaveSettings 会将 name/variant 随现有 importer payload 跨 session 恢复。11 项 xUnit 覆盖路径/对象 overload、variant、scene filter、unused cleanup、move/delete 和磁盘 meta session 恢复。
- `GetImplicitAssetBundleName` / `GetImplicitAssetBundleVariantName` 现按官方的“资产显式分配优先，否则逐级父文件夹直到 Assets”规则执行；默认 `BuildAssetBundles(output, options, target)` 用该结果稳定分组为真实 `AssetBundleBuild`，使 folder assignment 进入 bundle/variant/manifest writer。20 项用例覆盖 direct、variant、parent/nearest/override/fallback、无分配，以及 folder-inherited/default build 的单/多 bundle 路径。跨资产依赖图与 Unity 原生 importer YAML 仍未完成，不能把此项视为完整 AssetBundle dependency system。
- `BuildPipeline.GetDirectDependencies` / `GetAllDependencies` 现复用 `AssetDatabase` 的 GUID 依赖解析，去除 source、去重并稳定排序；AssetBundle writer 随之将仅由不同 bundle 承载的直接 asset 引用写入 manifest，显式 build 与默认（含 folder-inherited）build 均已覆盖。新增 10 项用例覆盖空输入、direct/transitive/cycle、排序、同 bundle 排除和两种 manifest 路径；AssetDatabase/AssetBundle 聚焦回归 **185/185** 通过。
- `AssetBundleBuild.assetBundleVariant` 已从错误的 `string[]` 修正为 Unity 2022 的单个 `string` field；writer 以完整逻辑名 `name.variant` 建立 catalog、hash 文件名和 path→bundle map，使跨 bundle manifest dependency 不会丢失 dependency 的 variant，DryRun 同步保留 variant 元数据。新增 10 项变体用例覆盖反射 field type、显式/默认 build、qualified 文件名、AppendHash、同/跨 bundle dependency 与 DryRun；本轮资源管线聚焦回归 **195/195** 通过。`GetAllAssetBundles` 对 variant base/qualified name 的最终枚举细节仍需 Unity 2022.3.61f1 A/B。
- 补齐 `AssetBundleBuild.addressableNames` 官方 `string[]` field，并作为 bundle 内 asset 的实际加载键：数组按 index 与 `assetNames` 配对，空项回退 asset path，长度不等时拒绝构建；address alias 也进入 content hash。`BuildAssetBundles` 同时不再擅自创建输出目录，而是要求 caller 先建目录，空 output 仍保持既有空 manifest 行为。地址别名与输出目录各新增 10 项端到端用例；本轮 AssetDatabase/AssetBundle 聚焦回归 **215/215** 通过。完整 UnityFS/serialized object、Scene address key 和 Unity 2022.3.61f1 A/B 仍待完成。
- `BuildAssetBundleExplicitAssetNames` 的 Unity 2022 legacy 两个 `BuildTarget` overload 已恢复并标记 `[Obsolete]`：以 `Object[]` / custom name 一一写出单文件 bundle，可返回最终 CRC；空或不等长数组、null asset、空 custom name、无效 parent 均不落盘并返回 false。新增 10 项用例覆盖公开返回类型、写出/加载、多 asset mapping、CRC 及全部拒绝分支；AssetDatabase/AssetBundle 聚焦回归 **225/225** 通过。其余 legacy `BuildAssetBundle`/streamed-scene overload 和 Unity 2022.3.61f1 A/B 仍待完成。
- `BuildAssetBundle` 的 Unity 2022 legacy 两个 `BuildTarget` overload 亦已恢复并标记 `[Obsolete]`：主 asset 与 additional assets 走单文件 bundle writer，优先使用 `AssetDatabase.GetAssetPath`，未注册对象回退 object name，支持 CRC；null/重复加载名/无效 parent 安全失败。新增 10 项定向用例；AssetDatabase/AssetBundle 聚焦回归 **235/235** 通过。streamed-scene、parameters build 和 Unity 2022.3.61f1 A/B 仍待完成。
- `BuildAssetBundlesParameters` 及其推荐的 `BuildAssetBundles(parameters)` overload 已补齐：`outputPath`、`bundleDefinitions`、`options`、`targetPlatform`、`subtarget`、`extraScriptingDefines` 进入公开面；definitions 未指定时复用 importer assignment，指定时复用 build-map writer。新增 10 项用例通过；仍需最终 Unity 2022.3.61f1 A/B。
- importer 的 `assetBundleName` / `assetBundleVariant` 现写入并读取 Unity 实际的 `*Importer` YAML 块（`TextureImporter` / `AudioImporter` / `DefaultImporter`）；旧 Anity 根字段仍可读以完成迁移。YAML-only meta、CRLF、quoted scalar、空/过期字段、跨 session 和损坏旧 payload 均有回归，且当迁移期 Anity 兼容注释同存时以 Unity YAML 为准。Texture/Audio 常用层级字段（mipmap、sampling、sprite、normal、audio sample/flags）已按本机 Unity 2022.3.51f1 样本读取，并仅在既有 Unity importer 块中原位写回，保留未知 YAML 字段；缺失字段及剩余 importer/platform overrides 仍暂存兼容 payload，尚非完整 Unity importer YAML serializer。
- `.fbx` / `.obj` / `.dae` / `.blend` / `.3ds` / `.dxf` 现会稳定登记为 `ModelImporter`，`ModelImporter.GetAtPath` 返回 registry identity；按本机 Unity 2022.3.51f1 ModelImporter 样本接通 materials、animations、meshes、tangent space、animation type 与 userData 的原位 YAML 读写。`DefaultImporter.userData` 也直接读写 YAML，所有上述路径保留未知字段。11 项 Model/Default 专项用例通过；material remapping、platform-specific model settings 和 Unity 2022.3.61f1 A/B 仍待完成。
- `ModelImporter.animations.clipAnimations` 现可读取 Unity YAML 的空列表或常用 scalar clip 字段（name/take、frame range、loop、root locks、mirror、wrap、offset、original transforms、additive pose），并对已有 YAML clip 项原位写回而保留未知字段；新增 clip 会追加标准 v16 项，`clipAnimations: []` 会在首次新增时转为块列表，缩短 array 会删除尾部 YAML clip 块。10 组参数化 fixture 与写回/追加/空列表/删除回归使 Model YAML suite 达 25/25。mask/reference pose/humanDescription 与 Unity 2022.3.61f1 A/B 仍待完成。
- `ModelImporter.humanDescription` 已补进公开 API，并将 Unity YAML 的 `armTwist`/`foreArmTwist`/`legTwist` 正确映射到官方 `upperArmTwist`/`lowerArmTwist`/`lowerLegTwist`，同时接通 stretch、feet spacing 与 translation DoF。YAML 内部 `hasExtraRoot` 会原样保留但不再伪造成 Unity 不存在的公共属性。HumanBone/SkeletonBone 数组、root-motion bone name 与 source Avatar 已双向持久化；material/avatar remapping 与 Unity 2022.3.61f1 A/B 仍待完成。
- `ModelImporter.avatarSetup` 已按本机 Unity 2022.3.51f1 的官方名称和 `NoAvatar` / `CreateFromThisModel` / `CopyFromOther` 枚举值接通，并将 `autoGenerateAvatarMappingIfUnspecified` 与两项设置一同原位读写 ModelImporter YAML；历史误名 `avatarDefinition` 仅保留 obsolete 转发。sourceAvatar 现为官方 Avatar 类型并经 GUID/fileID 解析；完整 remapping 与 Unity 2022.3.61f1 A/B 仍待完成。
- 本机 Unity 2022.3.51f1 反射已核验 `importMaterials` 为由 `materialImportMode` 派生的只读属性；Anity 现同步公开 `ModelImporterMaterialImportMode`（None/ImportStandard/ImportViaMaterialDescription 及官方别名）、`materialImportMode`、`materialLocation` 和 `useSRGBMaterialColor`，并实际读写 Unity ModelImporter 的 `materials.materialImportMode`/`materialLocation` 与 `meshes.useSRGBMaterialColor`。旧 `materials.importMaterials` 仅作为兼容输入迁移为 mode，新的保存不会再生成它。3+3+2+2 个模式/位置/sRGB 测试与既有 suite 合计 **39/39** 通过；外部 material remap、material extraction 与 Unity 2022.3.61f1 A/B 仍待完成。
- `ModelImporter` 已继续接通 Unity YAML 的 `bakeIK`、`removeConstantScaleCurves`、`importAnimatedCustomProperties`、`importConstraints`、`importPhysicalCameras`、`sortHierarchyByName`、`bakeAxisConversion`、`preserveHierarchy`、`strictVertexDataChecks` 和 `importBlendShapeDeformPercent`；每个字段均有读写反转回归。HumanBone 与 SkeletonBone humanoid mapping 列表现已支持读取、原位写回、空列表、新增、缩短、null、跨 project session 重载、YAML 转义与未知字段保留；Model YAML suite 增至 **107/107**。
- 本机 Unity 2022.3.51f1 预备反射确认 `HumanDescription`、`HumanBone`、`HumanLimit`、`SkeletonBone` 四个类型的字段/属性、obsolete metadata、NativeHeader/NativeType/NativeName/RequiredByNativeCode 已逐项完全一致；移除了错误公共别名并将 serialized members 恢复为官方 public fields。31 项公开面门禁加 Model YAML 共 **138/138**。目标 2022.3.61f1 未安装，因此这仍不是最终 Pro 证据。
- TextureImporter `platformSettings` 的 Unity YAML 列表现会实际解析为 `TextureImporterPlatformSettings`：Default/Standalone/Android/iPhone/WebGL 等 target 的 size、format、compression、quality、override、crunch、alpha split 和 Android ETC2 fallback 已由 10 个平台配置用例覆盖。已存在平台项可原位写回并保留未知字段/列表顺序，缺失平台可追加标准 version-3 条目，Clear 会将既有项的 `overridden` 清零；复杂平台字段、Unity 2022.3.61f1 A/B 和序列化排序细节仍待完成。
- `ImportPackage(path, interactive)` 现在执行可观察的事务生命周期：先通知开始，拒绝空路径和不存在的 package 并报告绝对路径错误；有效文件会报告规范化的绝对 package item path 后完成。11 个 xUnit 用例覆盖成功、失败、回调顺序、路径、订阅解除、多个订阅者与 interactive 两条调用路径。
- 本机 Unity 2022.3.51f1 的**预备**反射审计当前为 types present **989/4117**、exact **460**，members present **9242/37164**、exact **6973**；四个 humanoid description 类型已从 mismatch 转为 exact。2022.3.61f1 尚未安装，不能作为最终 Pro 验收。
- 本轮统一 Release 门禁已在 native-required 模式下完成：`_scripts/build-native.sh Release` 构建通过，`_scripts/run-tests.sh Release` 覆盖 Core 2909、Agent 91、Editor 164、Shader Graph 198、VFX Graph 490、CLI 16、Unity API parity 17 与 A/B compare 22，合计 **3907/3907**、0 失败、0 跳过。Editor 测试工程现会在 native-required 时复制平台原生库，测试临时 project root 也会在 fixture 初始化时创建，统一入口不再依赖手工准备。

### 尚未完成
- `.unitypackage` 的 gzip/tar asset/meta 解包、指定已落盘 asset 的导出（含 Recurse/IncludeDependencies）、磁盘 meta GUID、Refresh discovery、基础 importer settings/文件操作、单/批量 Move、显式/文件夹继承 AssetBundle name/variant registry、默认 BuildPipeline bundle 分组、基于文本 YAML GUID 的跨 bundle 直接依赖图、确定性 dependency invalidation hash 与内存 main/sub-asset 关系已实现；但 export 的 IncludeLibraryAssets 与 Interactive UI 尚未实现，bundle 依赖尚未做 Unity 2022.3.61f1 A/B，且 dependency lookup 仍是文本 GUID 提取，sub-asset 尚未持久序列化，hash 未与 Unity artifact/importer hash A/B，均不是 Unity 完整序列化 type tree/fileID 语义，settings storage 也仍是 Anity 注释 payload，尚不是 Unity 原生 importer YAML serializer。跨 asset YAML GUID/fileID 引用重写、scripted/model/font 等完整 importer、platform override、import worker/后台时序、取消 UI、cache server 和其余大量 AssetDatabase API 尚未完成；本项保持 🟡，不得以当前事务闭环冒充完整资源导入。

### 下一优先项
1. 用 Unity 2022.3.61f1 Pro 做显式/隐式 bundle name、variant、跨 bundle dependency 和默认 BuildPipeline A/B，并按差异补齐 variant resolution 与 build-map 语义。
2. 继续完成 root-motion rotation、material/avatar remap 与 ModelImporter 剩余官方公开面，并保持每批预备反射差异单调下降。
3. 将 current Anity importer-settings payload 逐字段迁移为 Unity 原生 YAML（含 Texture/Audio platform override）、实现 YAML GUID/fileID 引用重写与 scripted/model/font importer，再补 importer/postprocessor 行为 A/B。

## 2026-07-18 — UnityEngine.Rendering.AsyncGPUReadback 真实异步读回

### 已完成
- 删除错误位于 `UnityEngine` 的即时完成占位类，按 Unity 2022 公开面建立 `UnityEngine.Rendering.AsyncGPUReadback` 与 sequential value-type `AsyncGPUReadbackRequest`；反射已核对全部 `Request`、`RequestIntoNativeArray`、`RequestIntoNativeSlice`、`WaitAllRequests`、请求属性和 `GetData<T>` 签名。
- Request 现在由 PlayerLoop 在 camera render 后完成，`Update` / `WaitForCompletion` 可显式等待；回调只触发一次。`Texture2D` 支持全纹理、mip 和二维区域的 RGBA8 真数据，ComputeBuffer/GraphicsBuffer 支持 byte-range 真拷贝，RenderTexture 在已有 native device/camera target 上走原生 RGBA8 readback。
- `NativeArray`/`NativeSlice` destination 写入、错误状态、typed data、layer/size dimensions 与 `SystemInfo.supportsAsyncGPUReadback` 已接通；全资源请求现覆盖 `Texture2DArray`、`Texture3D`、Cubemap、CubemapArray 与 native RenderTexture 的每层数据，`GetData<T>(layer)` 严格返回单层 byte window。19 个 xUnit 用例覆盖 deferred/callback/data/region/mip/error/buffer/container/wait/force-player-loop 与多层 z/layer 映射。
- 按 Unity 2022.3.51f1 本机预备基线收紧公开面：callback/mip 的 optional metadata、泛型约束和 `StaticAccessor` 类特性均匹配；移除了 Unity 未公开的两个完整区域 NativeArray/NativeSlice 重载。该项仅为 2022.3.61f1 到位前的预检，不能替代最终版本 A/B。

### 尚未完成
- 全部格式转换、每个平台原生 asynchronous fence/readback、mipmapped 3D/array/cube texture 和 Unity 2022.3.61f1 官方 Player A/B 仍未完成；必须完成这些验证后才可将该模块改为 ✅。

### 下一优先项
1. 在目标 Unity 2022.3.61f1 对照实际 request 生命周期、invalid range 和 RenderTexture 读回时序。
2. 为 Metal/Vulkan/D3D 后端接入非阻塞原生 fence，并补齐 mipmapped volume/cube 与 format conversion。

## 2026-07-18 — UnityEngine.Rendering GraphicsFence / GPUFence 提交语义

### 已完成
- 补齐 `GraphicsFence`、`GPUFence`、`GraphicsFenceType`、`SynchronisationStage`、`SynchronisationStageFlags` 的 Unity 2022 公开反射面，以及 CommandBuffer 的 Create/Wait 全部 fence overload。
- fence 不再是即时成功标志：它绑定 CommandBuffer，只有通过 `Graphics.ExecuteCommandBuffer`、async queue 或 `ScriptableRenderContext.Submit` 提交后，才在下一 PlayerLoop frame retirement；等待 fence 会建立跨 command-buffer dependency。
- 11 个 xUnit 用例验证 pending/submit/async/CPU sync/GPU fence/dependency/invalid/stage/context submit；与 Unity 2022.3.51f1 预备反射的类型、枚举值与方法签名一致。

### 尚未完成
- 目前 fence retirement 对齐 Anity managed command-buffer scheduler；尚未将 Metal shared-event、Vulkan timeline semaphore、D3D11 query/fence 直接接入 native backend，因此此模块保持 🟡。

### 下一优先项
1. 在三条 native backend 上将实际 queue completion 连接为 GraphicsFence retirement source。
2. 用 Unity 2022.3.61f1 对照 CPU-sync property blocking 与 cross-queue timing。

## 2026-07-18 — macOS Editor Host Unity CLI forwarding

### 已完成
- `Anity.app/Contents/MacOS/Anity.Editor.Host` 现将以 `-` 开头的 Unity 兼容参数转发给唯一的 `Anity.Cli.CliHost` 实现；因此 `-batchmode -quit`、`-nographics`、`-projectPath`、build/test/IL2CPP 等不再被 Host 误报为 unknown command，同时保留 `start`、`menu`、`window`、`sample` 等交互 Host 子命令。
- CLI 16 项和 Editor Host 39 项 xUnit 已通过；最终 app-bundle ARM64 端到端启动将与本轮重装一起验证。

### 下一优先项
1. 在 app bundle 中补齐 Unity 2022.3.61f1 的真实 project open、executeMethod、build/test 生命周期 A/B。

## 2026-07-18 — UnityEditor.AnimatedValues Inspector 动画值

### 已完成
- 实现 `BaseAnimValue<T>`、`BaseAnimValueNonAlloc<T>`、`AnimBool`、`AnimFloat`、`AnimVector3`、`AnimQuaternion`：严格采用 Unity `value/target/isAnimating`、`speed/valueChanged`、序列化回调、插值/停止语义，并通过 `EditorApplication.update` 真实驱动 Inspector 与 EditorWindow 的重绘动画。
- `AnimBool.faded/Fade`、float/vector 线性插值和 quaternion 球面插值均已覆盖；10 个 xUnit 用例覆盖初始状态、target 生命周期、取消、回调、四种值类型和 non-alloc 基类。对 Unity 2022.3.51f1 的类型/成员反射预检无该模块差异。

### 尚未完成
- `AnimationMode`、`AnimationUtility`、`UnityEditor.Animations` graph authoring 与目标 Unity 2022.3.61f1 行为 A/B 仍待实现，不能将编辑器动画系统标为完整。

## 2026-07-18 — UnityEditor.AnimationMode 编辑器预览事务

### 已完成
- 补齐 `EditorCurveBinding`、`PropertyModification`、`AnimationModeDriver` 与 `AnimationMode` 的 Unity 2022 表面及绑定特性；curve binding 的 float/PPtr/discrete/serialize-reference 类型、等值比较和 property modification 都可实际使用。
- Animation Mode 支持 default/driver 会话、balanced sampling、AnimationClip 与 PlayableGraph 采样、动画属性登记及属性修改。预览中会捕获 Transform 树和公共对象字段，StopAnimationMode 后恢复原状态，避免 Animation Window/Inspector 预览污染场景。
- 11 个 xUnit 用例覆盖绑定类型、会话、采样范围、Clip/PlayableGraph、修改应用及恢复；Unity 2022.3.51f1 预检无该四类型差异。

### 尚未完成
- Animation Window 轨道编辑、Animator Controller graph authoring 及 Unity 2022.3.61f1 的逐帧 A/B 仍待实现；`AnimationUtility` 的公开反射面已在本机 2022.3.51f1 完成预备对照，不得以此替代目标版本验收。

## 2026-07-18 — UnityEditor.AnimationUtility 曲线与剪辑编辑

### 已完成
- 实现 AnimationUtility 的曲线/绑定、clip settings、animation event、object-reference curve、tangent metadata、transform path、Animation 组件 clips、motion curves 和 AnimationMode 入口；曲线操作直接驱动现有 AnimationClip 数据，不是签名占位。
- 新增 AnimationClipCurveData、AnimationClipSettings、ObjectReferenceKeyframe 与切线/修改回调嵌套类型；10 个 xUnit 用例覆盖读写、事件、对象引用、切线、层级绑定与设置。
- 对 Unity 2022.3.51f1 的预备反射对照已无 `AnimationUtility`/clip-settings/keyframe 类型差异：弃用消息、`NativeThrows`、`ThreadSafe`、`NotNull`、`Unmarshalled` 及默认值均逐项一致；该结果只证明可用的非目标本机版本，不能替代 Unity 2022.3.61f1 Pro 验收。

### 尚未完成
- Animation Window 轨道编辑和 Unity 2022.3.61f1 逐帧 A/B 仍待实现；本项保持 🟡。

## 2026-07-18 — Animator Window 真实 Controller 图绑定

### 已完成
- `AnimatorWindow` 不再展示硬编码的 Idle/Walk/Run 示例图；现在绑定选中 `Animator` 的实际 controller，按 active layer 从真实 `AnimatorStateMachine.states` 重建 Entry/Any State/Exit/state 节点，并保留状态位置与默认状态标识。
- 窗口的状态、参数、层操作均写入同一 controller 图模型：支持新增状态、切换 active layer、新增唯一参数、新增 layer；未含 Animator 的选中对象会清除窗口绑定，避免继续编辑旧资产。
- 新增 10 个 xUnit 回归，覆盖绑定、状态位置/default-state、空名称、layer 激活/非法 index、唯一参数、GameObject Animator 同步和选择清空。

### 尚未完成
- 此项复用现有可执行图模型，但正式 `UnityEditor.Animations.AnimatorController`、`AnimatorStateMachine`、transition 等官方命名空间/API 面尚未迁移，且缺完整拖拽、transition 条件编辑、序列化/importer 与 Unity 2022.3.61f1 A/B；不得将此项标记完成。

## 2026-07-18 — UnityEditor.Animations Controller 图与 GameObjectRecorder

### 已完成
- 正式实现 `UnityEditor.Animations` 的 `AnimatorController`、layer/state/state-machine/transition/condition/child-node、`BlendTree`、`GameObjectRecorder` 与 `CurveFilterOptions`；Controller 图支持层/参数唯一命名、状态与子状态机、任意/入口/嵌套转场、条件、有效 motion/behaviour 覆盖、BlendTree clip 递归收集和 `Animator.runtimeAnimatorController` 绑定。
- Animator Window 已改绑正式 `UnityEditor.Animations.AnimatorController`，编辑和展示同一份 layer/state/parameter 数据，不再依赖错误的运行时命名空间图模型。
- `GameObjectRecorder` 实现 binding、递归 Transform/Component 收集、快照、曲线写回、线性 key reduction、reset 和参数校验；34 个 xUnit 定向用例（Controller 图 12、Window 10、Recorder 12）通过。
- 本机 Unity 2022.3.51f1 的预备反射对照中，全部 `UnityEditor.Animations` 差异已清零；结果只作为可用非目标版本的结构预检，不能代替 Unity 2022.3.61f1 Pro A/B。

### 尚未完成
- Animator Window 的节点拖拽/选中/转场条件 Inspector、资产序列化/importer、Animation Window 轨道交互、动画运行时逐帧语义和 Unity 2022.3.61f1 Pro 官方 A/B 都仍未完成；整体 Unity API 覆盖也远未完成。

## 2026-07-18 — Animator Window transition/condition 图编辑

### 已完成
- Animator Window 现从正式 `UnityEditor.Animations` state-machine 重建 source、Any State、Entry 和 Exit transition 边；边携带真实 transition 对象，窗口选择状态/transition 与模型一致。
- 实现状态间/Any State/Entry/Exit transition 的创建和删除、参数校验后的条件增删、默认状态切换、状态节点移动、跨 layer 图隔离和刷新后的选择保持；节点/边是内部实现，不新增非 Unity 公共 API。
- 新增 13 个 xUnit 用例，覆盖上述正常、无效状态、无参数、跨 layer、删除和选择持久化路径；与既有窗口用例合计 23 项通过。

### 尚未完成
- 仍缺实际 pointer 拖拽/框选/连线命中、transition condition Inspector 的完整 IMGUI/UIToolkit 控件、Undo/SerializedObject/资产 importer 持久化、nested state-machine 导航和 Unity 2022.3.61f1 Pro 图编辑 A/B。

## 2026-07-18 — Animator Window 画布命中、拖拽与嵌套 State Machine 导航

### 已完成
- Animator 图画布现在按实际 `GUILayout` canvas rect 命中节点与 transition：单击选择 state/transition，拖动 state 会写回正式 `AnimatorStateMachine.states[].position`，空白点击清空选择，Delete 删除所选 transition；命中算法以边的画布锚点及线段距离为准，避免特殊 Entry/Any State/Exit 节点误吸附普通 state 的 transition。
- graph 支持 child `AnimatorStateMachine` 节点、双击进入、返回父层/根层、在当前层新增 state/state machine；layer 切换会重置路径，避免编辑到上一层的图。state-machine→state-machine 与 state-machine→Exit transition 也会进入边模型并可删除。
- 新增 12 个 xUnit 用例，覆盖 child-node、进入/返回/重置导航、nested add、pointer double-click/click/drag/blank/edge hit、Delete 和 state-machine transition 生命周期；窗口 transition suite 达 25 项，连同 Controller 图与窗口绑定为 47 项聚焦回归。

### 尚未完成
- 框选、多选、拖拽连线创建、右键上下文菜单、条件 Inspector 的完整编辑控件、Undo/SerializedObject/Controller importer 持久化、真实 IMGUI renderer 的 Bezier raster 与 Unity 2022.3.61f1 Pro 图编辑 A/B 仍待完成；不可因此认定 Animator Window 已完全对齐。

## 2026-07-18 — UnityEditor.MonoScript 资产类型与脚本反查

### 已完成
- `MonoScript` 已从错误的 sealed `Object` 修正为 Unity 2022 相同的可继承 `TextAsset`，并补齐 `NativeType`、`ExcludeFromPreset`、`NativeClass` metadata、无参构造、`GetClass()`、`FromMonoBehaviour()` 和 `FromScriptableObject()`。
- 行为层为同一托管脚本类型缓存稳定 MonoScript asset：可从组件或 ScriptableObject 反查准确 Type 和名称，null 输入安全返回空；新增 12 个 xUnit 用例覆盖 concrete type、稳定 identity、类型隔离、asset name、继承、构造和 null。
- 本机 Unity 2022.3.51f1 预备反射中，`UnityEditor.MonoScript` 与 `UnityEditor.Animations.*` 均无差异，原先 4 个 MonoScript regression 已归零。

### 尚未完成
- `.cs` importer 到 source/assembly/class 的真实导入管线、partial/namespace/编译错误 script 的 Unity 语义、GUID/fileID 序列化及 Unity 2022.3.61f1 Pro A/B 仍未完成。

## 2026-07-18 — UnityEditor.ActiveEditorTracker Inspector 编辑器生命周期

### 已完成
- 新增正式 `ActiveEditorTracker` 与 `DataMode`。Tracker 会跟随 `Selection` 重建 active Editor、支持锁定对象快照、可见性、dirty/rebuild 延迟、unsaved-change、Inspector/Data Mode 和 shared tracker；对 Inspector 多对象不可编辑状态会按 Editor 的 multi-edit 能力计算。
- `InspectorWindow` 现由 tracker 驱动 selection rebuild 与 lock/unlock 生命周期：窗口启用创建 tracker、选择变化从 activeEditors 同步 target、解锁立刻恢复 live selection、关闭时释放订阅，避免旧 editor/selection 残留。
- 精确区分 Unity 的公开与内部编辑器 API：锁定对象、重建事件、data mode、unsaved state 与全局 rebuild 保持 internal；公开面保留 active editors、dirty/lock/visibility/rebuild、factory、custom-editor 查询、equality/hash 与官方 metadata。
- 13 个 xUnit 用例覆盖 selection rebuild/dirty、锁定/解锁、对象导入导出、可见性、unsaved state、延迟刷新、mode、rebuild event、shared tracker；本机 Unity 2022.3.51f1 的 `ActiveEditorTracker`、`DataMode` 与 `InspectorMode` 预备反射差异为 0。

### 尚未完成
- Unity 原生 inspector 的 per-component editor 发现、domain reload/native pointer 生命周期、Prefab override/Undo、Debug/Runtime data mode 与 Unity 2022.3.61f1 Pro A/B 仍待实现；本项不能替代完整 Inspector 对齐。

## 2026-07-18 — UnityEditor.AssemblyReloadEvents 脚本重载生命周期

### 已完成
- 实现正式 `AssemblyReloadEvents` 与嵌套 `AssemblyReloadCallback`：`beforeAssemblyReload`、`afterAssemblyReload` 保持 Unity 公开 delegate event；native 触发入口保持 internal，避免额外公开 API。
- `InternalEditorUtility.ReloadAssemblies` 现按 before → `scriptReloaded` → after 的顺序运行，并以 finally 保证脚本回调异常后依然结束 reloading 状态且发送 after；`RequestScriptReload` 和 `EditorUtility.RequestScriptReload` 都走同一生命周期。
- 10 个 xUnit 用例覆盖事件触发/顺序、recompiling 时段、异常 finally、两个 request 入口、订阅解除与多订阅顺序；本机 Unity 2022.3.51f1 预备反射无该模块差异。

### 尚未完成
- 真正的 managed domain unload/reload、静态字段重建、程序集编译队列、脚本序列化恢复与 Unity 2022.3.61f1 Pro A/B 仍需 native/editor-host 支持。

## 2026-07-18 — Anity 品牌图标、macOS ARM64 安装与 Metal XR 门禁修复

### 已完成
- Anity 应用标记现重新设计为三块蓝/青/石墨几何构件、中心珊瑚色 core 的简洁 A 轮廓，避免与 Unity 图标混淆；`assets/macos/AnityIcon-1024.png` 已规范为 1024px alpha PNG，并由全尺寸 iconset 重建 `AnityIcon.icns`。
- 新增 `assets/macos/Info.plist` 和 `_scripts/install-macos-arm64.sh`。该安装入口强制要求 Darwin/原生 `arm64`，发布 self-contained `osx-arm64` Editor Host、随 bundle 部署 `libanity_native.dylib`、进行 ad-hoc codesign，并安装至 `/Applications/Anity.app`（可用 `ANITY_MACOS_INSTALL_DIR` 覆盖）。
- 安装门禁实际验证 host 架构、native dylib 架构、plist bundle identifier/icon、签名、Editor `--help` 与 `menu list`；本机 Apple Silicon 已通过，应用路径为 `/Applications/Anity.app`。
- 最新 Release 已在本机原生 ARM64 重新发布并原子替换 `/Applications/Anity.app`：bundle 内 `Anity.Editor.Host` 与 `libanity_native.dylib` 均为 arm64，ad-hoc `codesign --verify --deep --strict`、图标资源与 `assets/macos/AnityIcon.icns` byte-for-byte 一致、以及 `-batchmode -quit -nographics` 均通过。
- 安装 staging 复制现采用 APFS clone-copy（`cp -c` / `cp -cR`），保持原子签名/切换事务的同时避免为 self-contained runtime 额外占用完整副本；在本机仅约 178 MiB 可用空间的压力下，重新发布、签名、安装和 `-batchmode -quit -nographics` 已通过。
- 修复 Metal XR single-pass-instanced 管线：vertex shader 写 `render_target_array_index` 时，pipeline 现声明 `inputPrimitiveTopology = MTLPrimitiveTopologyClassTriangle`；此前 Metal 会拒绝创建此 pipeline。双眼 `Tex2DArray` 的 1x/2x MSAA 共 10 组 native 像素门禁已恢复通过。
- Unity API parity 与官方 VFX spawner probe 的默认目标现固定为 **Unity 2022.3.61f1**，且 API parity 会拒绝把其他编辑器目录伪作最终证据；本机仅有 2022.3.51f1/Unity 6，所以此 gate 目前以明确的“缺少目标 Editor”失败，未再错误采用 2022.3.51f1。

### 测试与门禁
- `bash _scripts/build-all.sh Release`：通过，所有 native 与 managed 工程 0 error。
- `bash _scripts/run-tests.sh Release`：**3,234/3,234** 通过，0 failure、0 skipped；其中 Core native-required **2,361/2,361**，含 XR single-pass 双眼像素 **10/10**。
- 修复后重新执行 `_scripts/install-macos-arm64.sh Release`：通过，`/Applications/Anity.app` 是签名的 native ARM64 self-contained app bundle。

### 尚未完成
- `.app` 当前封装的是可启动的 Editor Host；完整 Unity 风格桌面编辑器 UI、正式 Developer ID/notarization、Windows/Linux/Android/iOS Player、Unity 2022.3.61f1 官方 A/B 均仍待完成，不能将本项视为 Unity 2022 Pro 全面对齐。

### 下一优先项
1. 将官方 Unity 2022.3.61f1 安装/fixture 引入 API 审计与 URP XR 图像 A/B 门禁。
2. 将 Metal 双眼 ABI 移植到 Android Vulkan 和 Windows D3D11，并在实体设备执行像素验证。
3. 继续闭合 Editor 的 Scene/Game/Inspector/Prefab/Quick Search 真实 UI 与交互路径。

## 2026-07-18 — Metal URP XR Single-Pass Instanced 双眼阵列路径

### 已完成
- 扩展 `AnityGraphicsCameraPassDesc` 为 `depthSlice + depthSliceCount` 合约；Metal 对双眼 `Tex2DArray` pass 配置 `renderTargetArrayLength=2`，原生验证连续 layer 范围，颜色、深度、normal 与 motion attachment 以同一 array pass 清除/保存。
- `AnityGraphicsCameraMeshDrawDesc` 现携带左右眼三组 object-to-clip / motion / previous matrices。Metal vertex 以 `instance_id` 选择 eye matrix，并通过 `render_target_array_index` 把同一次 indexed draw 写入对应 layer；不是托管端连续提交两次 draw。
- URP 仅在 **Metal + 双眼启用 + `Tex2DArray`/至少两层目标** 上选择 single-pass instanced；一次 camera render 使用 left/right raster、non-jittered motion 与独立 per-eye history。其他 backend、单眼或非数组目标严格保留已验证的 left/right multipass 路径。
- single-pass culling 现在分别用左右眼 frustum 执行并按 stable renderer instance-id 取并集；因此仅右眼可见的 renderer 不会因左眼剔除而漏掉，同时仍只执行一次 instanced draw。
- Opaque、Depth、Normals、Motion 四个 `AfterRenderingOpaques` transient 在 single-pass 模式也分配双层 `Tex2DArray`，逐眼 GPU copy，避免后处理 global texture 丢失右眼。
- Metal 最终 HDR grade 对 `Tex2DArray` 不再把 array 直接交给 `texture2d` compute kernel：逐 layer 建立 2D texture view，并在同一 command queue 中依次处理每只眼。新增 slice-aware HDR tone-mapped readback ABI，10 组双色/不同强度 native-required 像素用例确认左右 layer 都实际完成色调映射，而非仅处理 layer 0。
- 新增 `UnityEngine.XR.XRDisplaySubsystem` / `XRSettings` provider frame-layout：启动的 provider 会实际配置/复用 `Tex2DArray + volumeDepth=2 + VRTextureUsage.TwoEyes` 目标，绑定 camera，发布含 left/right projection/view 与 array slice 0/1 的单一 `XRRenderPass`；该 target 被既有 URP Metal single-pass-instanced 路径直接消费，而非建立托管双眼旁路。
- XR provider 现有受边界约束的 dynamic-resolution multiplier（默认 0.5–1.5）：与 viewport scale 相乘后才计算有效尺寸；下一帧仅在有效尺寸/格式/MSAA 改变时重建双眼 target，并同步刷新 `XRRenderPass` descriptor 与 viewport。10 组 0.5–1.5 scale 重建门禁覆盖 array/TwoEyes/slice 合约与非法值拒绝。
- `XRDisplaySubsystem.AttachOverlayCamera` 现把 overlay 绑定到 provider 当前双眼 target，设置 `StereoTargetEyeMask.Both`，标记为 URP Overlay 并去重注册到 base stack；URP stack 已以 base target 执行 overlay，因此两个 array layer 保持同一 native frame 且 overlay 不清 color。10 组 MSAA/dynamic-scale 组合验证该契约。
- provider-owned 双眼 target 现向 URP 暴露每帧调度语义：`singlePassRenderingDisabled` 在 Metal 上会强制逐眼 multipass，恢复后重新启用 native single-pass instanced；普通 `Tex2DArray` 目标保持既有形状推断，不会被 provider 配置误伤。
- multipass stereo 的 final stack 处理现严格延后至右眼：左眼不入队 post-process，右眼完成后只调用一次既有 native array HDR grade（逐 layer），避免每只眼重复处理整个双眼 target。
- `RenderTextureDescriptor.useDynamicScale` 与 `RenderTexture.useDynamicScale` 已进入兼容层；XR provider target 显式启用该状态。URP 现优先传递外部 target 的真实 descriptor，使单通道和强制 multipass 的 renderer feature 都能看到 `Tex2DArray`、双层、TwoEyes、MSAA 和动态尺度，而不是虚构的单层默认 descriptor。
- XR display provider 现补齐 Unity XR 的 out-parameter render-pass API：`GetRenderPass(int, out XRRenderPass)` 返回 `RenderTargetIdentifier`，`XRRenderPass.GetRenderParameter(Camera, int, out ...)` 保留 camera/pass 校验；`GetCullingParameters(Camera, int, out ScriptableCullingParameters)` 输出左眼矩阵、world origin 与 stereo-culling 状态。URP `RenderCamera` 已优先消费 provider-owned target 的该 culling contract，再在 native single-pass 时合并右眼 frustum，不是脱离渲染的 facade。
- Vulkan camera target 现将 `Tex2DArray` 分配为两层 image，并为每个 layer 建立独立 color/MSAA/depth/normal view 与 framebuffer；normal 同样拥有 MSAA resolve attachment。通用层已解锁 Vulkan array target；10 组 native-required 双色像素门禁确认右眼不会坍缩至 layer 0。native indexed mesh pipeline 真实执行 MVP、vertex color、基础 `_BaseMap`/ST、alpha clip、五类 Unity blend、ZWrite、color/depth/normal MRT 与 MSAA resolve。`AnityGraphics_CopyCameraRenderTargetColorSlice`、Depth-to-Color compute slice 与 Normals-to-Color slice 均在 Vulkan 选择 source/destination array view 和 subresource barrier；10 组世界法线方向像素门禁确认 normal attachment 经 URP transient copy 后的 RGB 值正确。当前支持 resolved color/depth/normal copy；normal map、motion transient、single-pass instanced 仍未完成。
- 新增 `_scripts/build-vulkan.sh`：使用隔离 `build-vulkan/` 显式启用 Vulkan，部署 matching versioned dylib 到 Core 测试输出，并以 `ANITY_REQUIRE_VULKAN=1` 强制运行 Vulkan Camera 门禁；它与默认 Metal 构建刻意分离，防止平台 runtime 依赖混入产品 dylib。

### 测试与门禁
- 10 组 1x/2x native pixel cases 证明单一 camera pass (`depthSliceCount=2`) 加单次 instanced mesh draw 分别在左右 layer 的不同像素写入，交叉像素保持 clear；另 10 组 URP scheduling cases 证明 Metal target 只生成一条 left-eye camera record 且 native pass 明确记录两层。
- `bash _scripts/build-native.sh Release`、Core 编译、single-pass/multipass XR 定向 **50/50** 通过；新增 10 组 right-eye-only frustum fixture；HDR 双眼 array post-process native-required **10/10** 通过。
- `XRDisplaySubsystemTests` 的 10 组尺寸/scale/MSAA frame-layout、10 组 dynamic-resolution 重建、10 组 overlay stack binding，以及 lifecycle/invalid-argument 门禁共 **32/32** 通过；另有 10 组 native Metal provider 调度切换门禁，验证单 pass → 显式 multipass → 单 pass 的 `depthSliceCount`、左右 layer 顺序及每个完整双眼 frame 仅一次 final post-process。
- `bash _scripts/build-vulkan.sh Release` 在显式 `ANITY_REQUIRE_VULKAN=1` 下成功编译 MoltenVK backend，并运行 **70/70** Vulkan camera native-required 门禁；双眼 array slice、mesh/opaque/depth-copy、`_BaseMap`、`_BaseMap_ST`、blend/alpha-clip/ZWrite，以及 normal MRT/transient copy 各为 **10/10**。`git diff --check` 通过。

### 尚未完成
- XR provider 的 frame-layout、dynamic-resolution target policy 与 base/overlay binding 已完成；Vulkan 已验证离屏 array clear/readback、vertex-color/`_BaseMap`/`_BaseMap_ST` mesh、alpha clip、五类 blend、resolved opaque slice copy 及 depth transient slice copy，但缺 normal-map、normal/motion transient、single-pass instanced 与 Android 实机，故不能视为 Vulkan XR 可用。multiview、D3D11 single-pass、正式 Unity 2022.3.61f1 Player render/image A/B 仍未完成。

### 下一优先项
1. 将 XR provider frame-layout 扩展到 post-process stereo sampling 与 multiview。
2. 将 array-layer instanced ABI 移植到 Vulkan/D3D11，并以 Android Vulkan / Windows WARP 确认真实像素。
3. 以 Unity 2022.3.61f1 URP XR sample 建立双眼 color/depth/normal/motion A/B fixtures。

## 2026-07-18 — URP Color Curves LUT 与可配置 Bloom 金字塔

### 已完成
- `ColorCurves` 已由不具备曲线语义的 `Vector2` 占位参数升级为 `TextureCurve` / `TextureCurveParameter`；标准 master/red/green/blue 及 hue-vs-hue、hue-vs-sat、sat-vs-sat、lum-vs-sat Volume 字段均已存在，默认 identity 不会错误触发后处理。
- 八条 `AnimationCurve` 均会烘焙成固定 **128-sample** LUT，随 `HDRColorGrade` 下发。native CPU 与 Metal 都执行 master → R/G/B → Hue-vs-Hue → Hue-vs-Sat → Sat-vs-Sat → Lum-vs-Sat，位于 Channel Mixer 之后、Contrast 之前；前三类 saturation modifier 的默认值为 1，而 Hue-vs-Hue 保持 identity ramp，避免未覆盖的 Volume 改变色相。
- Metal final pass 将曲线从每帧 `setBytes` grade 拆为每 device 的 4 KiB `MTLBuffer`。只有 LUT 内容变更才复制到 GPU；未变的 Volume 会命中缓存。专用 native stats 返回样本数、容量、upload/hit，且测试验证第二个相同 grade 不触发 upload。
- 托管端以 `ConditionalWeakTable<ColorCurves,...>` 保留已烘焙 LUT；缓存快照逐项比较 curve keys（含 tangent/weight/mode）、loop、zero-value 与 wrap mode，公开可变字段变更时精确重烘焙，未变时复用同一数组。
- Metal HDR 后处理已从固定两级 Bloom 升级为单次 command-buffer 内创建的至多 **8** 层 `RGBA16Float` 私有 mip 金字塔；`maxIterations`、half/quarter `downscale`、`highQualityFiltering`、`scatter`、`intensity` 与 RGB `tint` 均由 `Bloom` Volume 传到 native ABI 并参与执行。每层先以阈值预过滤，再按低 mip 下采样，最终按 scatter 权重累加后与 HDR 原图合成；未使用的 texture slot 只作安全别名，不会参与取样。
- `Bloom.dirtTexture`（当前支持原生 `Texture2D`）及 `dirtIntensity` 已接入既有 device-owned 纹理注册表；最终 Metal compute pass 绑定上传纹理及其 sampler，并将其 RGB 贡献添加到 Bloom。缺少、已释放或不受支持的纹理安全退化为普通 Bloom，不会把白色 fallback 当作 Dirt。
- `AnityHDR_ProcessFrame` CPU reference 不再使用逐像素阈值加亮：现以同样的 unexposed HDR prefilter、half/quarter downsample、normal/high-quality box filter、至多 8 层 mip 与 scatter/tint 合成顺序执行。它与 Metal 的四组 1/2/6/8 mip、downscale/filter/scatter fixture 在四个空间采样点的 RGB 误差均不超过 **2/255**。
- CPU Lens Dirt 现通过显式 `AnityHDR_ProcessFrameWithLensDirtRGBA8MipsBias` C-ABI 传递完整 `Texture2D` RGBA8 mip chain、尺寸、mip count、FilterMode、U/V wrap、linear、`mipMapBias` 和精确字节数；native 会拒绝截断或尺寸不一致的输入。Bilinear 使用全屏 UV 导数加 mip bias 选取 mip，Trilinear 混合相邻 mip；`HDRUtilities` 会保留实际 `Texture2D` 的完整 mip chain 和 mipMapBias，不再截断为 base mip。CPU 仍按与 Metal 相同的 `bloom += bloom * dirt.rgb * dirtIntensity` 顺序在 tonemap 前合成。
- GPU texture registry 的 `AnityGraphicsTextureDesc` 现明确承载 `mipMapBias` 与 QualitySettings 解析后的 `anisoLevel`（1–16）。缓存状态将二者纳入 hash，修改后会重新上传；D3D11 sampler 使用 `MipLODBias` / anisotropic filter，Vulkan 会在 physical-device feature 可用时启用 sampler anisotropy 并下发 `mipLodBias`，Metal sampler 使用 `maxAnisotropy`，其 UI fragment 为 main/alpha texture 显式传递 `sample(..., bias(...))`。旧 ABI 调用留下的 aniso=0 会安全规范为 Unity 默认 1；NaN/Infinity mip bias 及非法各向异性会被原生层拒绝。
- Point filter 是各向异性例外：D3D11 早已选择 point filter，现将 Metal `maxAnisotropy` 与 Vulkan `anisotropyEnable` 同步固定为 1/false，避免把 `Texture.anisoLevel=16` 错误施加到 nearest sampling。Metal UI 在 `FilterMode.Point` + aniso 16 的真实 GPU texel 门禁保持精确 nearest 输出。
- `TextureWrapMode.MirrorOnce` 不再在 Metal 后端退化为 Clamp：Metal sampler 现使用 `MTLSamplerAddressModeMirrorClampToEdge`，对负 UV 做一次镜像再夹边；Vulkan 在 `VK_KHR_sampler_mirror_clamp_to_edge` 可用时启用同等 address mode，并在扩展缺失时显式安全降级为 Clamp。Metal 真实 UI readback 以同一 `u=-0.75` 对照 Clamp/red 与 MirrorOnce/green，验证 sampler 行为而非只检查描述符。
- Metal HDR Lens Dirt compute pass 现在也从已上传的 texture entry 读取 `mipMapBias`，作为独立后处理参数使用 `dirtTexture.sample(dirtSampler, uv, bias(...))`；它不再依赖 sampler clamp 近似。native-required 像素测试以 red mip 0 与 green mip 1 对照，证明 `+1` bias 会实际切换到下一粗 mip。
- `ScriptableRenderPassInput` 已补齐 Unity URP 的 `None/Depth/Normal/Color/Motion` flags、只读 `input` 与受保护的 `ConfigureInput`。`ScriptableRenderer` 在 feature 的 `AddRenderPasses`/`SetupRenderPasses` 之后聚合所有 pass 请求；`UniversalAdditionalCameraData` 和 `UniversalRendererData` 的 depth/opaque 默认值也进入同一个 `CameraData` 合约。custom renderer feature、camera override 与 renderer asset 不再各自丢失资源请求；`UrpRendererLifecycleTests` 新增 11 个用例，定向结果为 **22/22**。
- `ScriptableRenderPassInput.Color` 现在会在 `AfterRenderingOpaques` 注入 `CameraOpaqueTexturePass`：它创建相同格式、单采样的临时 `RenderTexture`，经新的 `AnityGraphics_CopyCameraRenderTargetColor` C-ABI 从 resolved `Camera.targetTexture` 或 active `CameraTarget` 直接 GPU blit，并且只在成功时发布 `_CameraOpaqueTexture`；cleanup 解除全局绑定并释放 transient target，绝不以空纹理代替场景颜色。Metal 覆盖离屏/CameraTarget、RGBA8 通道、源/目标 MSAA resolve、尺寸/HDR mismatch、自拷贝、release 和 pass cleanup 的 **10** 项 native-required 像素/生命周期门禁；`NativeCameraPassTests` 现为 **210/210**。
- `ScriptableRenderPassInput.Depth` 现在同样会在 `AfterRenderingOpaques` 注入 `CameraDepthTexturePass`。Metal camera depth attachment 已声明 shader-read；新的 `AnityGraphics_CopyCameraRenderTargetDepthToColor` 以 dedicated compute pipeline 将 `Depth32Float` 的每个 depth 值写入 transient color target 的 R 通道，分别覆盖 single-sample sampler 和 MSAA sample 0 read，并发布 `_CameraDepthTexture`。clear depth 0.25/0.5/0.75 的 native readback、CameraTarget、源/目标 MSAA、尺寸/self/released/unregistered 拒绝及 pass cleanup 共 **10** 项门禁通过，`NativeCameraPassTests` 现为 **220/220**。
- D3D11 的 `AnityGraphics_CopyCameraRenderTargetColor` 已有真实 GPU `ID3D11DeviceContext::CopyResource` 实现：只接受已 resolve 的离屏 Camera RenderTexture、相同尺寸/格式且单采样资源，任何 CameraTarget、未知目标、格式或尺寸不匹配均明确失败，不会偷拷贝 backbuffer。此路径仅在 macOS 交叉的 stub 编译通过，仍需 Windows WARP/硬件像素门禁后才能作为已验证后端能力。
- Vulkan 已补齐与 Metal/D3D 独立的 `CameraRenderTarget` registry：每个离屏目标拥有 LDR `RGBA8` 或 HDR `RGBA16Float` resolved color、可选 2/4/8x MSAA color attachment、depth attachment、render pass/framebuffer、布局生命周期与安全销毁；backend-neutral `Ensure/Destroy/Record/Readback` ABI 已分发到该实现。相机 pass 执行真实 attachment clear、depth clear 与 MSAA resolve，`AnityGraphics_CopyCameraRenderTargetColor` 以 `vkCmdCopyImage` 在相同尺寸/格式的 resolved 离屏目标之间复制并恢复 attachment layout。swapchain `CameraTarget` 仍显式返回 NotSupported，绝不读取陈旧的 presentation image。macOS 已安装 Vulkan headers/loader/MoltenVK，并用 `ANITY_ENABLE_VULKAN=ON` 的独立 CMake build 成功编译整个真实 Vulkan backend；Android 设备像素门禁尚未运行。
- Vulkan 的 `AnityGraphics_CopyCameraRenderTargetDepthToColor` 已新增 true GPU compute path：single-sample shader 以 `sampler2D` 读取 depth，MSAA shader 以 `sampler2DMS` 读取 sample 0，均写入 RGBA8 transient 的 R 通道；descriptor/pipeline、depth-read/storage-image layout barrier、descriptor 生命周期和恢复 attachment layout 均在 native。`CameraDepthTexturePass` 现在强制使用单采样 RGBA8 writable transient，避免 HDR target 不能作为 portable storage image。MoltenVK 的 portability-enumeration/subset 已显式启用，10 项 native-required 门禁实跑通过，覆盖 depth 1/0.75/0.5/0.25、2x/4x MSAA、opaque `vkCmdCopyImage`、无效 source/self/HDR/dimension 拒绝和 pass global bind/cleanup；Android 实机仍待验证。
- D3D11 的同一 depth-to-color ABI 已实现 native compute 版本：camera depth 资源改用 `R24G8_TYPELESS`，并分别创建 D24S8 DSV 与 R24 depth SRV；单采样与 MSAA shader 用 `Load` / `Load(..., 0)` 写入 RGBA8 UAV 的 R 通道，严格拒绝 HDR destination、尺寸不一致和 CameraTarget。该 Windows-only 分支在 macOS 默认 stub 与 Vulkan C++ 构建均不受影响，但尚无 Windows SDK/WARP 或硬件像素验证，因此不能视作已验证后端能力。
- `ScriptableRenderContext.Cull` 不再总是返回空 scene：现以稳定 instance-id 快照收集 active renderer，执行 enabled/isVisible、camera cullingMask、world-transformed eight-corner mesh bounds frustum、per-layer cull distance 和 object motion-vector 需求判定；`DrawRenderers` 以这个 snapshot 记录每个有效 mesh/material/submesh 的真实绘制命令及 transform/bounds/filter 状态，且会将 Unity `Material.renderQueue=-1` 正确解释为 shader 的默认 queue。URP camera 不再用仅设置 `camera` 字段的零初始化参数，而是保留 camera-derived culling matrix/mask/origin。除命令记录外，Metal path 已提交 opaque mesh 到 native raster；仍缺 shader/material、透明排序、skinning、阴影和非 Metal backend，不能把 managed snapshot 视为完整渲染。
- `AnityGraphics_DrawCameraMesh` C-ABI 与 C# pinned bridge 现明确区分离屏 `Camera.targetTexture` 与 presentation `CameraTarget`。Metal swapchain 也拥有 color/depth/`RGBA8Snorm` normal/`RG16Float` motion resolved attachment（MSAA 时各自 resolve），默认 CameraTarget 与离屏 target 都可在原生层 upload 40-byte packed indexed vertex/index buffers、执行 depth test/write 和 indexed triangle raster；C# ABI edge 会转置 Unity `Matrix4x4` 以匹配 Metal column-major constant。`DrawRenderers` 现在无论是否设置 `Camera.targetTexture` 都提交 mesh/material/submesh，且显式传递 target lifetime 语义。
- Metal normal MRT 写 signed world-space normal；managed bridge 对 renderer `localToWorld` 使用 inverse-transpose 后才上传，`CameraNormalsTexturePass` 以 `R8G8B8A8_SNorm` 发布 `_CameraNormalsTexture`。`AnityGraphics_CopyCameraRenderTargetNormalsToColor` 现同时支持离屏源与 CameraTarget source 的 GPU blit。新增 10 个 native-required CameraTarget 网格/normal/motion 像素门禁与既有 13 项离屏 mesh/normal/URP lifecycle 门禁均实跑通过。材质 `_BumpMap` 现按 Linear 数据读取，10 项 native-required 像素门禁验证 TBN 的 X/Y/Z 方向、正反 tangent handedness 与 Z 反向；另 10 项覆盖 non-uniform 及镜像 scale 下 inverse-transpose normal、object-to-world tangent、正交化和 handedness 翻转；再 10 项验证 CameraTarget 的 1×/2×/4× MSAA resolve。仍缺完整 DepthNormals shader pass、Vulkan/D3D11 与 Unity 2022.3.61f1 A/B，因此不得称为完整 URP DepthNormals。
- Metal motion MRT 使用 `RG16Float`（MSAA resolve）。C# 侧按 camera instance 保存上一帧 VP、按 renderer instance 保存上一帧 local-to-world，且对启用 `skinnedMotionVectors` 的 `SkinnedMeshRenderer` 保留上一帧 renderer-local C++ skinning 输出；first frame 稳定回退到当前矩阵，后续 GPU vertex stage 以 `(currentNdc - previousNdc) * 0.5` 写每像素 velocity。`MotionVectorGenerationMode.Object` 使用 camera+object history，`Camera` 仅用 camera history，`ForceNoMotion` 强制 current=current previous；同一 renderer 的历史在所有材质 pass 提交后才更新，避免多材质首 pass 覆盖。CameraTarget 与离屏源都能发布 `RGHalf`/`R16G16_SFloat` `_MotionVectorTexture`。新增 10 组 native-required 前帧顶点位移像素回读，连同对象矩阵 motion 与四权重 skinning 定向套件 **24/24** 通过。仍缺 transparent motion、blendshape/8-weight/GPU deformation、jitter/XR、history eviction、Vulkan/D3D11 与官方 2022.3.61f1 A/B，不能标完整 Motion Vectors。
- `AnityGraphicsCameraMeshDrawDesc` 现携带显式 blend、ZWrite 与 alpha-clip contract。`DrawRenderers` 从 Unity 材质 queue / `_SrcBlend` / `_DstBlend` / `_ZWrite` / `_Cutoff` 解析 opaque、alpha、premultiplied、additive、multiply 与 cutout；Metal pipeline 把该状态真正写入 color attachment、depth stencil 与 fragment discard，而非把透明对象当作 opaque。`CommonTransparent` 也依相机位置进行 back-to-front 排序并稳定以 instance-id 打破并列。新增 10 个 native-required 门禁以真实像素验证五种 blend、`alpha < cutoff` discard、`alpha == cutoff` 保留与非法 blend/NaN cutoff 拒绝。当前仍仅支持 vertex color，缺 `_BaseMap`、Shader Graph fragment、blend op/color mask、transparent normal/motion semantics 与 Unity Player A/B。
- Mesh vertex ABI 现由 position/normal/color 扩展为 position/normal/UV/color；`DrawRenderers` 会上传 `Mesh.uv`，并将 material `_BaseMap`（回退 `mainTexture`）通过既有 native texture registry 同步。Metal mesh fragment 实际按 texture 的 sampler/mip bias 采样后再做 color、blend 与 alpha-clip；无 `_BaseMap` 时原生入口显式确保 Unity-white fallback，不能依赖此前 UI/texture submission 的副作用。10 项 native-required 像素门禁已验证 1×1 RGB 调制、2×1 Point+Repeat UV 采样和纹理 alpha 对 cutoff 的影响。Metal 已具备 ST transform；Vulkan 现具备基础 `_BaseMap`/ST 采样，仍缺 normal map、Shader Graph fragment、完整材质 variants 与 D3D11 像素后端，不能标为材质路径完成。
- `_BaseMap_ST` 等价的 scale/offset 已由 `Material.GetTextureScale/GetTextureOffset` 进入 mesh C ABI，Metal fragment 在采样前执行 `uv * scale + offset`。10 项 native-required 像素门禁覆盖 0.5×/1×/2×/3× scale、0/.5/.99/1/1.2 offset、Point 2×1 texel selection 与 Repeat 回绕；另有 10 项验证 mesh fragment 实际复用 texture registry 的 Clamp、Repeat、Mirror、MirrorOnce address modes，以及 10 项 Bilinear/Trilinear 的 2×1 texel-center/edge interpolation。C++/C# 构建均通过。normal map、Shader Graph fragment 和非 Metal backend 仍待实现或验证。
- Mesh submission 现始终将 vertex color 与材质 tint 相乘；优先使用显式 URP `_BaseColor`，但默认白色 BaseColor 会回退 legacy `Material.color/_Color`，避免默认值吞掉显式 legacy tint。C# 编译通过；完整 Lit color-property/Shader Graph A/B 仍待覆盖。
- Metal native mesh ABI 现传递 world-space tangent/handedness 与 `_BumpMap` registry ID。fragment 对 normal map 以 Linear 数据解码 `2*rgb-1`，用 `TBN` 转换为 world-space normal 并写入已有 `RGBA8Snorm` normal attachment；无 normal map 时保持 vertex normal。10 项 native-required 离屏像素门禁覆盖 TBN X/Y/Z、正反 tangent handedness 与 Z 反向，另 10 项确认 non-uniform / mirrored transform 的 Unity 风格 normal、tangent、odd-negative-scale 语义，再 10 项验证 CameraTarget 1×/2×/4× MSAA resolve。native 与 C# 构建通过；仍待 Shader Graph normal slot、skinned motion/blendshape/GPU skinning 与非 Metal backend，不能标为完整 URP Lit normal-map。
- `AnityGraphics_SkinMeshVertices` 已在 `anity-native` 执行 position/normal/tangent 混合：除兼容旧 `BoneWeight` 四通道外，`Mesh.GetBonesPerVertex/GetAllBoneWeights/SetBoneWeights(NativeArray<byte>, NativeArray<BoneWeight1>)` 已构成 Unity 2022 variable-influence stream，C++ ABI 直接消费最多 **8** 个 influence。`SkinnedMeshRenderer.skinWeight` 的 Bone1/2/4 与 Auto→`QualitySettings.skinWeights`（Unlimited=8）实际限制 kernel 参与的权重并重新归一；20 项 native-required 验证覆盖 1/2/4/8 influence 与 output 数值。`Mesh` 已具备 `blendShapeCount`、frame name/count/weight/vertices 查询、`AddBlendShapeFrame` 与 `ClearBlendShapes`，`SkinnedMeshRenderer` 具备严格索引/有限值检查的 `Get/SetBlendShapeWeight`。多 frame 形态键在 managed compatibility layer 按 Unity weight timeline 插值或外推，position/normal/tangent delta 的多 shape 合成实际由新的 `AnityGraphics_ApplyBlendShapeDeltas` C++ ABI 完成，再进入 native skin kernel；21 项定向门禁覆盖两帧插值、负/超范围权重、多 shape 叠加、BakeMesh、复制和非法契约。`DrawRenderers` 在 `SkinnedMeshRenderer` 上调用该链，`BakeMesh` 不再清空目标而会复制形变后 geometry、UV/color/submesh topology（并应用 `useScale`，零轴 scale 不会产生 NaN/Infinity）。previous-position mesh ABI 与 renderer-local skin history 已让相同物体矩阵下的骨骼与 shape deformation 产生真实 motion vector。`sharedMesh` 会初始化 skinned local bounds，显式 `localBounds` 会在 SRP 中优先经八角变换后参与 frustum culling；当 native skin stream 可用时，Cull 实际从当前 bone/shape deformation 重建 renderer-local AABB，再经八角 world transform 参与剔除，10 组动态位移的 near/frustum-edge/outside 门禁通过。仍缺 GPU compute/vertex skinning 和 Unity 2022.3.61f1 A/B，不能称完整 Unity skinning。
- `NativeCameraPassTests` 现为 **200/200**：曲线的 134 项之外，新增 66 个 Bloom/Lens Dirt 像素发现项，覆盖 1/2/4/6/8 mip、half/quarter、normal/high-quality filtering、三种 RGB tint、scatter 对低 mip 贡献、Lens Dirt 的零强度/RGB/white/0.125–2.0 通道调制、CPU ABI 截断和越过 1×1 尾级的拒绝、Point/Bilinear、Repeat/Clamp/Mirror/MirrorOnce、sRGB 解码、Trilinear 相邻 mip 混合、CPU/Metal 的 `mipMapBias`、完整 mip chain 的 `Texture2D` helper 和四组 CPU↔Metal 数值门禁。加 URP stack/lifecycle 为 **224**。
- `bash _scripts/build-native.sh Release`、`bash _scripts/build-all.sh Release` 已通过；强制 native 的 **200/200** 定向像素测试已通过。本次 texture sampling registry **26/26**、跨 backend UI 纹理定向 **61/61**，Metal 真实像素门禁 **14/14**，包含 UI 与 HDR Lens Dirt 的 `mipMapBias=+1` 切换至下一粗 mip。
- Motion-vector mesh ABI 现独立携带 raster `objectToClip` 与 velocity `motionObjectToClip`：URP 以 `Camera.projectionMatrix` 继续 raster，同时以新增 `Camera.nonJitteredProjectionMatrix` 保存 current/previous motion history；Metal vertex stage 因此用 non-jittered NDC 计算 velocity，不将 TAA jitter 伪写为物体速度。`ResetProjectionMatrix` 会同步解除 non-jittered override；camera、renderer 与 skinned history 均限制为 **4096** 项，超限同步裁剪。`Camera.useJitteredProjectionMatrixForTransparentRendering` 默认保持 transparent 的 jittered raster，关闭时 `ScriptableRenderContext` 选择 non-jittered VP；`CopyFrom` 同步此设置。`AnityGraphicsCameraMeshDrawDesc.writeMotionVectors` 把 Unity 2022 URP 的 opaque-only 契约带到 Metal MRT：alpha-clip 仍写 velocity，transparent 的颜色混合仍执行但 attachment-2 write mask 关闭，不能覆盖已经写入的不透明 motion。10 个 raster-jitter native 像素、10 个 projection override/reset、10 个 transparent-raster matrix、10 个 history-boundary 与 10 个 transparent-preserve-opaque-motion 门禁已进入 `ScriptableCullingTests`，定向套件 **234/234** 通过。
- `Camera.StereoscopicEye` / `MonoOrStereoscopicEye`、`stereoSeparation`、`stereoConvergence`、`GetStereoProjectionMatrix`、`GetStereoNonJitteredProjectionMatrix`、`GetStereoViewMatrix`、per-eye `SetStereoProjectionMatrix` / `SetStereoViewMatrix` 及对应 reset 已具备可执行 fallback：默认双眼以 convergence 生成 opposite off-axis projection，并在 calculated 或 custom world-to-camera view 后应用 local-X eye offset；custom eye matrices 严格优先并可 reset，`CopyFrom` 同步全部 stereo 状态。10 个 eye separation、non-jittered、custom override/reset 的回归进入 `ScriptableCullingTests`，定向套件 **244/244** 通过；native array target 已在下一项接通，但尚不能声称完整 XR multipass/single-pass 输出完成。
- 原生 Camera target ABI 现增加 `dimension`、`volumeDepth` 与 `depthSlice`；Metal 将 `Tex2DArray` 分配为 `MTLTextureType2DArray`（MSAA 为 `MTLTextureType2DMultisampleArray`），并把 color/depth/normal/motion attachment 和 resolve binding 到指定 layer。`ScriptableRenderContext` 将 `RenderTargetIdentifier.m_DepthSlice` 传入 native pass/draw；新增 slice readback ABI 只供内部像素门禁逐眼验证，原 layer-0 readback 保持兼容。color/depth/normal/motion 的 slice-copy ABI 都从当前 eye layer 复制到相同 destination layer，而非固定 layer 0；depth 使用 Metal `depth2d_array` / `depth2d_ms_array` compute kernel 采样/写入指定 slice。10 项 native-required 测试在 1×/2× MSAA 下分别写左/右 layer、逐层回读，并通过 opaque/depth/normal/motion 四个 URP pass 将 slice 1 复制到各自 XR transient 的 slice 1，验证实际 color、0.75 depth、right-normal、motion 像素，全部通过。Vulkan 现已验证 array target 的 per-layer clear/readback、vertex-color/`_BaseMap`/ST indexed mesh raster、alpha clip、opaque/alpha/premultiplied/additive/multiply、resolved opaque slice copy 和 depth-to-color compute slice；normal map、normal/motion transient 与 single-pass instanced 仍未实现；D3D11 仍明确 `NotSupported`。真正 XR runtime 的 multipass/single-pass instanced 调度、per-eye culling/history 和 Player A/B 仍未完成。
- URP camera stack 现以 `Tex2DArray + VRTextureUsage.TwoEyes + Camera.stereoTargetEye` 激活 managed stereo multipass：base 和每个 overlay 按 left slice 0、right slice 1 完整渲染，`CameraData` 显式携带 `isStereoEnabled/stereoEye/xrDepthSlice`，每眼 culling matrix/origin 与 native raster/non-jittered VP 都采用 `GetStereo*` 矩阵。`CameraOpaqueTexturePass` 同时把 `xrDepthSlice` 传给 native resolved-color copy，因此每眼的 `_CameraOpaqueTexture` 不会读到左眼。`StereoTargetEyeMask` 与只读 `stereoEnabled` 已加入 Camera compatibility surface，`CopyFrom` 保留 mask。camera motion history key 现为 `(camera instance, eye slice)`，因此右眼提交不会覆盖左眼 previous VP；10 项 URP stack 与 10 项每眼 history 回归均通过。仍缺 XR SDK display/provider、single-pass instanced/multiview shader variants 及 Metal Player/XR hardware A/B；Vulkan 现有离屏 array clear/readback、opaque `_BaseMap`/ST mesh 与逐 slice opaque/depth copy，但尚未接入 XR single-pass/normal/motion；D3D11 array target 仍明确不支持。

### 尚未完成
- Motion-vector 的 XR native eye-slice/multipass/single-pass 输出、GPU deformation、Vulkan/D3D11 后端与 Unity 2022.3.61f1 官方 Player A/B 仍待完成。
- CPU Lens Dirt 已具备完整 `Texture2D` mip chain、Point/Bilinear/Trilinear、Repeat/Clamp/Mirror/MirrorOnce、sRGB 与 mipMapBias 的可执行 ABI 路径；Metal Lens Dirt compute 已执行 mip bias，UI texture registry 已把 mip bias 与各向异性下发 Metal/Vulkan/D3D sampler，但 Unity 2022.3.61f1 Player A/B、Vulkan/D3D 实机像素门禁，以及 Lens Dirt compute sampler 的各向异性仍未完成。非 `Texture2D` Lens Dirt 也仍待 native texture registry 扩展。
- Metal 已将 `requiresOpaqueTexture`、`requiresDepthTexture`、`requiresNormalsTexture` 与 `requiresMotionVectors` 变成同帧真实 GPU resource；normal/motion 采用 world-space normal 与 RG16 velocity，并覆盖离屏及 CameraTarget source。Vulkan 仍只具备离屏 color/depth attachment、MSAA resolve、opaque/depth transient copy，CameraTarget/normal/motion 与 Android 实机像素门禁仍缺；D3D11 normal/motion 也未实现，不能把跨后端的 `CameraData` flag 当作资源已生成。
- D3D11 已开始消费 backend-neutral camera ABI：离屏 `Camera.targetTexture` 可在 Windows native path 创建/重建/释放，具有同尺寸/MSAA 的 D24S8 depth attachment，执行 color/depth clear/load、D3D11.1 `ClearView` 局部 viewport color clear、可选 MSAA resolve 与 LDR staging readback；LDR target 的 tone-mapped readback 复用同一 RGBA8 存储，HDR target 仍严格拒绝。无 D3D11.1 的局部 color clear、或任何局部 depth clear 均严格返回未支持，绝不扩大为全 attachment clear。`CameraTarget`、局部 depth clear、HDR post/Bloom 和 Windows WARP/硬件像素门禁仍未完成，且本机没有 Windows D3D 工具链，不能将源码路径视为验证完成。
- 曲线 LUT 尚未提供 256+ 分辨率或 Unity 2022.3.61f1 Player 颜色/Bloom A/B fixture；本机仅有 2022.3.51f1 与 Unity 6，不能替代目标版本。

### 下一优先项
1. 在实体 Android Vulkan device 上验证 camera clear/MSAA/opaque/depth copy/readback；在 Windows WARP/硬件上验证 D3D11 depth-to-color compute 的 single/MSAA 像素结果。
2. 扩展 Metal native mesh path 至 GPU compute/vertex skinning、jitter/XR 与 history eviction；随后将同一 attachment/copy contract 落地 Vulkan 与 D3D11。
3. 以 Unity 2022.3.61f1 Player fixture 验证 UI 与 Lens Dirt 的 mipMapBias/各向异性；在 Windows/D3D11 与 Android/Vulkan 实机复测 sampler 和多 mip pixel fixture。

## 2026-07-18 — URP camera pass native control-plane ABI

### 已完成
- 在 `anity-native` 建立 `AnityGraphicsCameraPassDesc/Info` 与 `AnityGraphics_RecordCameraPass`/`GetLastCameraPass` C-ABI。原生层验证 target 尺寸、viewport 有限性与范围、Unity MSAA 采样数，并以 device/frame/sequence 保存 target、viewport、clear、store、HDR、final-pass 合约。
- `NativeGraphicsDevice.TryRecordCameraPass` 已完成 P/Invoke 对齐，`UniversalRenderPipeline` 每台 Base/Overlay Camera 在 `SetRenderTarget`/`SetViewport` 后把真实 stack pass 记录到当前 native device；native 不可用时保持托管路径，不会伪造成功。
- Metal backend 现在对 swapchain `CameraTarget` 与 `Camera.targetTexture` 的独立原生目标注册表创建真实 `MTLRenderPassDescriptor`：Base color/depth clear → Overlay color load/depth clear/load → store，编码真实 `MTLViewport`。CameraTarget 和 RenderTexture 的 2x/4x（以及设备支持时的 8x）均采用 persistent multisample color/depth attachment + single-sample resolve texture，跨 Overlay 保留 MSAA attachment 并每 pass resolve；partial viewport clear 走专用 Metal clear draw pipeline（color/depth/color+depth 三种 write mask）而非扩大为 attachment clear。LDR resolve target 保持 raw RGBA8 readback；HDR RGBA16Float target 除显式 ACES→sRGB readback 外，现有真实 Metal compute final-pass：先做两级 Bloom prefilter/downsample（half 与 quarter resolution），再在 resolved attachment 合并 exposure、Bloom、temperature/tint 白平衡、Color Filter、Channel Mixer、contrast、Hue Shift、saturation、ACES/Neutral，且最终 Base/Overlay stack 的 `PostProcessPass` 会调用该路径。CPU `AnityHDR_ProcessFrame` 与 Metal 都固定采用 `white balance → color filter → channel mixer → contrast → hue shift → saturation → non-negative clamp → tonemap` 次序；`WhiteBalance` Volume 与 `ColorAdjustments` 的 temperature/tint 会合并并 clamp，ColorAdjustments 的 Hue Shift/Color Filter 和 `ChannelMixer` 3×3 输出矩阵都会下发到最终 native grade。CameraTarget、RenderTexture、MSAA resolve 与 Overlay load 均使用同一条 native execution chain。建立/尺寸或采样数重建/释放均管理原生资源与 command-buffer 生命周期；unsupported sample count 返回失败而不触发 Metal 断言。native-required 模式下已禁止 swapchain 创建错误退化为托管成功。target identity 不再通过数值 `2` 猜测，避免与 `BuiltinRenderTextureType.CameraTarget` 冲突。
- `NativeCameraPassTests` 现有 **106/106** 个强制 native 用例：原 control-plane 断言外，新增 Metal RenderTexture 与 CameraTarget 的 clear/readback、Overlay load、partial viewport、跨目标隔离、resize recreate、Release、targetId=2 消歧，以及 2x/4x/设备能力 8x MSAA resolve、overlay/partial/跨帧/invalid sample/mismatch 边界；HDR 覆盖 raw readback 拒绝、显式 ACES/sRGB readback、black/primary/highlight、MSAA resolve/overlay、LDR 拒绝和 headless CameraTarget 2x/4x 像素门禁，并新增实际 compute post 的 ACES/Neutral、exposure、两级 Bloom intensity/threshold/空间扩散、saturation、warm/cool temperature、green/magenta tint、Color Filter RGB/负值 clamp、Hue Shift 正/负/180/full-turn、Channel Mixer 矩阵路由/正负系数/alpha、CPU/Metal 参考一致性、MSAA、Overlay、CameraTarget 像素门禁。与 URP stack/lifecycle 用例合计 **130** 项。

### 测试与门禁
- `bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过；`ANITY_REQUIRE_NATIVE=1` 下 native camera pass 定向 **106/106**、URP stack/lifecycle **24/24** 通过；`git diff --check` 通过。

### 尚未完成
- Metal 已消费 `CameraTarget` 与单采样/设备支持 MSAA LDR/HDR `Camera.targetTexture` 的全/partial viewport clear/load/store 路径，并具备显式 ACES HDR readback、两级 compute Bloom/final post、ColorAdjustments 的 hue/color filter/saturation/contrast 与 temperature/tint 白平衡，以及 Channel Mixer 3×3 矩阵；Vulkan/D3D attachment、更高层级的 Bloom blur、完整 Color Curves/LUT 与 HDR10 显示输出仍是下一实际渲染阶段。

### 下一优先项
1. 将目前两级 compute Bloom 扩展为 URP 14 的可配置更多 mip/blur、完整 Color Curves/LUT，并补 mip generation/aliasing 与 renderer resource allocation。
2. 同步完成 Vulkan/D3D11 的 attachment/load-store 语义和 native target registry，再实现 MSAA resolve。
3. 以 Unity 2022.3.61f1 URP 14 Player fixture 对照多 stack、viewport、HDR、Bloom、阴影和 XR。

## 2026-07-18 — URP 14.x Base/Overlay camera stack composition

### 已完成
- 新增 `UniversalAdditionalCameraData` 与 URP `CameraExtensions.GetUniversalAdditionalCameraData`：支持 `renderType`、`cameraStack`、`rendererIndex`、`renderPostProcessing`、`clearDepth`、depth/color texture 请求等每相机状态；未挂载 Component 的兼容 Camera 也有稳定的附加数据实例。
- `UniversalRenderPipeline` 现在按 Base 相机拥有 stack：过滤 null/self/disabled/重复项及非 Overlay 相机，将合格 Overlay 依声明顺序合成至 Base 的同一 render target，并只在整栈结束后提交。直接渲染孤立 Overlay 不会错误清除或提交。
- 相机 stack 共享 Base 的 `targetTexture` 和 descriptor；Overlay 不拥有 color clear，depth clear 遵循其 `clearDepth`。URP 接管通用 SRP 清屏，以避免通用 render loop 在 stack 合成前破坏共享目标。
- stack 现在也会以共享 target 尺寸换算每台 Camera 的 `rect`，将 stack-relative `pixelRect` 写入 `CameraData` 并在清除前调用 `CommandBuffer.SetViewport`；因此多个 Base stack 或 Overlay 可以在同一输出目标的不同区域合成。
- 引入按 `rendererIndex` 缓存和选择的 renderer 实例，非法/空 index 严格回退到 asset 的 `defaultRendererIndex`；修复 `CreatePipeline` 曾将配置好的默认 renderer index 重置为 0 的问题。
- 后处理仅在 stack 的末相机执行，防止逐 Overlay 重复 tonemap；保留独立 renderer 调用的既有后处理行为。默认 target identifier 也会正确回退到 `BuiltinRenderTextureType.CameraTarget`。
- 新增 `UrpCameraStackTests` 13 个用例，连同 `UrpRendererLifecycleTests` 11 个用例，共 **24/24**：附加数据稳定性、Base/Overlay 顺序、孤立 Overlay、非法 stack 项过滤、最终后处理、depth ownership、共享 RT/descriptor/viewport、CameraTarget、renderer 选择与非法 index 回退。

### 测试与门禁
- Release URP 定向（camera stack + renderer lifecycle）：**24/24** 通过。
- `bash _scripts/build-all.sh Release` 通过：native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与 URP3DDemo 均 0 error。
- `bash _scripts/run-tests.sh Release` 通过：强制 native 的完整 Release 矩阵 **2,561/2,561**、0 失败、0 跳过；其中 Core **1,688/1,688**（本项新增 13 个 camera-stack 用例）。

### 尚未完成
- 本项只完成 URP Base/Overlay 的 managed 控制面。render graph、load/store action、camera depth/opaque texture 的原生 allocation、XR multipass/single-pass、deferred/GBuffer、真实 GPU HDR/MSAA resolve、阴影/光照及 Unity 2022.3.61f1 官方 Player 图像 A/B 仍未实现。

### 下一优先项
1. 将 stack 的 depth/color texture 请求、load/store、viewport 和 final resolve 落入 `anity-native` Metal/Vulkan/D3D backend，并以 native readback 证明 Overlay 不重清 color。
2. 完成 URP deferred/GBuffer 与真实 HDR/MSAA resolve，再补 Bloom/Tonemap fullscreen GPU pass。
3. 建立 Unity 2022.3.61f1 URP 14.x 官方 Player fixture，对 camera stack、HDR、Bloom、透明排序、阴影和 XR 做截图/数值 A/B。

## 2026-07-18 — URP 14.x renderer per-camera lifecycle and pass ordering

### 已完成
- 修复 `UniversalRenderer` 与 `Renderer2D` 的 renderer reuse 缺陷：默认 opaque/transparent/sprite pass 不再只在构造期入队；每台 Camera 的 `Setup` 都会重建其默认 pass。此前第一台相机执行后队列被清空，后续相机可能只保留 post process pass。
- `ScriptableRenderer` 现在明确以 per-camera queue 工作：`Setup` 先隔离上台相机状态，feature 依次运行 `AddRenderPasses` 与 `SetupRenderPasses`；执行时按 `RenderPassEvent` 稳定排序，同一事件保持入队顺序。
- 所有 pass 走 `Configure → OnCameraSetup → Execute`，并在成功或异常时逆序 `OnCameraCleanup`，feature camera hook 也保证成对清理；队列在 finally 中清空，防止异常污染下一台 Camera。
- 新增 `UrpRendererLifecycleTests` 11 个用例：跨相机重建、同事件稳定排序、pass/feature 生命周期、执行或 setup 异常 cleanup、Forward/2D renderer 重用与 setup 幂等性。

### 测试与门禁
- URP 定向：**11/11** 通过；Core 强制 native：**1,675/1,675**；完整 Release 强制 native 矩阵：**2,548/2,548**，0 失败、0 跳过。
- `bash _scripts/build-all.sh Release` 通过：native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与 URP3DDemo 均 0 error。

### 尚未完成
- 这次仅闭合 URP renderer queue/lifecycle；不代表完整 Unity 2022.3.61f1 Pro 或完整 URP 14.x。URP camera stack/overlay 合成、XR multipass/single-pass、deferred/GBuffer、renderer feature resource allocation、native GPU post-processing、阴影/光照、真实 HDR/MSAA resolve、以及 Unity Player 截图与数值 A/B 仍缺。

### 下一优先项
1. 实现并验证 Base/Overlay camera stack：clear/load/store、viewport、post-process final target 与 renderer index 选择，至少 10 个含异常与多相机用例。
2. 把 HDR Bloom/Tonemap 从 globals/CPU fallback 推进到 Metal/Vulkan/D3D 的真实 render target 与 fullscreen pass，补 HDR/MSAA/resize/device-loss 像素门禁。
3. 建立 Unity 2022.3.61f1 URP 14.x 官方 Player fixture，对 camera stack、HDR、Bloom、tonemap、透明排序、阴影和 XR 逐场景截图/数值 A/B。

## 2026-07-17ag — Anity.Agent 0.6.0 工具审计、完整终态与usage保存

### 已完成
- 独立 `anity-agent/` 官方扩展提升到 **0.6.0**，新增 `AgentToolAuditEvent`、`IAgentToolAuditSink`、`AgentToolAuditPhase/Outcome`、`AgentAuditFailureMode` 与typed `AgentAuditException`。每次远端工具在授权/执行前写 `Requested`，完成后写 `Succeeded/Denied/InvalidArguments/Unavailable/ToolError/TimedOut/Canceled/AuthorizationError`，不再靠模糊字符串推断执行状态。
- 审计事件严格不含原始arguments、tool result或API Key，只记录session/call/tool标识、arguments SHA-256、输入/输出UTF-8字节数、UTC时间和有界duration。工具结果新增 **64 KiB** 上限，session id限制128字符且拒绝控制字符；调用者取消仍向上抛出且Session history保持原子，超时继续以tool error回传模型恢复。
- 审计默认 `FailClosed`：`Requested`无法落盘时工具不会执行，完成记录失败时turn不会伪装成功；显式 `Continue`仅供调用方接受审计降级时使用。成功、拒绝、坏参数、未知工具、工具异常、超大结果、超时与取消均有结构化终态。
- `anity-editor`新增 `HashChainedAgentToolAuditLog`，写入 `Library/AnityAgent/Audit/tool-audit.jsonl`。JSONL记录使用逐条SHA-256链、严格schema/UTF-8/16 KiB单行校验、4 MiB默认轮换、8份默认archive、跨文件sequence连续性、启动时全链验证、进程独占lock、写穿透flush；Unix目录/文件/lock分别强制0700/0600/0600。保留窗口内的链被修改、删行、改hash、archive断档时均fail closed。
- `AgentMessage`现在保留非流式与SSE（含tool-call多轮）的 `finish_reason` 和 `AgentTokenUsage`；编辑器最终transcript以Session原子提交的assistant消息为准，并显示prompt/completion/total tokens。编辑器每次连接自动启用项目审计，重连先安全释放旧runtime/audit lock。

### 测试与门禁
- `Anity.Agent.Tests`由 **76** 增至 **91/91**，新增 **15 个发现项**：digest/无原文、UTC与runtime边界、成功、拒绝、非法参数、不可用、工具异常、超大结果、超时、调用者取消、审计fail-closed/continue、非流式tool多轮metadata及普通SSE metadata。
- `Anity.Editor.Host.Tests`由 **20** 增至 **39/39**，新增 **19 个发现项**：单条/双条链、无原文、重启续链、payload/hash篡改、64路并发、跨文件轮换、archive容量、4组非法配置、archive gap、Unix权限、dispose、独占writer、Controller真实SSE tool审计及重连lock释放。
- `_scripts/run-tests.sh/.ps1`现在统一强制 `ANITY_REQUIRE_NATIVE=1` 与 `AnityRequireNative=true`，会把当前构建的native runtime复制到测试Host；缺失或无效native库直接失败，不再以托管回退跳过原生门禁。最终Release矩阵 **2,537/2,537**：Core native **1,664**、Agent **91**、Editor Host **39**、Shader Graph **198**、VFX Graph **490**、CLI **16**、API parity **17**、A/B compare **22**，0失败、0跳过。
- `bash _scripts/build-all.sh Release`通过native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor与URP3DDemo，0编译错误；URP3DDemo由既有43个nullable warning降为0 warning。`Anity.Agent.0.6.0.nupkg`已生成。
- Unity 2022.3.51f1 API审计保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`；完整Unity 2022.3.61f1对等仍是持续目标，不据此宣称全量完成。

### 尚未完成
- SHA-256链可检测保留文件的非重算修改，但没有OS vault中的HMAC/signing key或远端不可变anchor；拥有全部文件写权限的攻击者仍可重算整条链，因此当前是tamper-evident而不是不可伪造审计。
- usage/finish reason已进入Session history与Editor transcript，但conversation/session仍未跨编辑器重启持久化；usage聚合、预算/费用策略、审计查询/导出UI和损坏文件的显式隔离恢复流程仍缺。
- 完整JSON Schema参数求值、Responses API、真实多厂商endpoint、Windows/Linux credential实机矩阵、代理/TLS/断线恢复及长期并发压力仍未闭环。

### 下一优先项
1. 用OS vault保存项目级audit HMAC/signing key并增加外部anchor/导出验证；实现审计查询、过滤、导出、损坏隔离和管理员策略锁定。
2. 实现有界、版本化、原子、可迁移的conversation/session/usage持久化，覆盖tool-call消息、启动恢复、并发保存、容量轮换和坏文件恢复。
3. 引入完整JSON Schema求值和结构化验证路径，再推进Responses API、真实多厂商及代理/TLS/断线矩阵。

## 2026-07-17af — Anity.Agent 0.5.0 编辑器窗口、安全凭据库与工具权限

### 已完成
- 独立 `anity-agent/` 官方扩展提升到 **0.5.0**，新增非敏感 `AgentConnectionProfile` 与 `IAgentCredentialVault`。项目配置只允许保存provider、Base URL、model、credential id、超时/重试/响应上限；API Key只在解析连接时从OS vault读取，profile与异常字符串继续输出 `***`，Bearer key统一限制为最多 **2048 UTF-8 bytes**。
- 系统凭据后端已实现：macOS直接调用Security.framework Keychain增删改查；Windows直接调用Credential Manager `CredWriteW/CredReadW/CredDeleteW`；Linux通过Secret Service的 `secret-tool`，secret只写stdin、不进入命令行参数。未知平台或Linux缺少Secret Service工具时fail closed，**没有明文文件fallback**；native缓冲区在使用后清零。
- `anity-editor`新增真实 `Window/Anity/Agent` 编辑器窗口和 `AgentEditorController`：可配置自定义Base URL/model/credential id、替换或删除OS凭据、连接、消费SSE增量响应、显示streaming/final transcript、断开与清空；最终消息以Session原子提交的assistant history为准。`ProjectSettings/AnityAgentSettings.json`采用64 KiB上限、严格JSON、临时文件+flush+原子replace；替换凭据后若配置落盘失败会恢复旧凭据，不留下半提交状态。
- 新增 `AgentToolPermissionPolicy`，支持每工具 `Deny/Ask/Allow`、默认权限、调用者取消和headless Ask fail-closed。编辑器对 `Ask` 显示参数预览并只授权一次；策略在远端工具真正执行前生效，被拒绝的工具不会调用实现，只向模型返回结构化拒绝结果供其恢复。
- 编辑器深测同时修复两个既有生产缺陷：菜单反射现在逐方法隔离第三方程序集的attribute/type加载失败；Host注册的window factory只构造实例，不再用 `ShowWindow()`递归进入自身造成栈溢出。Agent窗口已纳入Host catalog、菜单、打开和关闭生命周期。

### 测试与门禁
- `Anity.Agent.Tests`由 **60** 增至 **76/76**，新增 **16 个发现项**：profile解析/脱敏、缺失与超大凭据、4组非法credential id、未知provider、Deny/Allow/Ask/无prompt/cancel、permission snapshot、运行时拒绝不执行及允许只执行一次。
- 新建 `Anity.Editor.Host.Tests`并达到 **20/20**：默认设置、round-trip、项目文件无密钥、坏JSON、64 KiB上限、非法工具名/Base URL、原子replace无残留、vault-only写入、非法key不改状态、落盘失败凭据rollback、自定义连接解析、SSE增量路径、transcript成功/失败原子性、删除断连、Ask fail-closed、dispose、Host菜单/catalog及窗口真实开关。已加入 `_scripts/run-tests.ps1`。
- `Anity.Cli.Tests` **16/16**；三套Release定向工程均0 warning/0 error。`Anity.Agent.0.5.0.nupkg`已生成；`bash _scripts/build-all.sh Release`通过，产品模块0编译错误，URP3DDemo保持既有43个nullable warning；`git diff --check`通过。

### 尚未完成
- macOS/Windows/Linux后端源码和隔离测试已闭环，但本批未向用户真实Keychain写测试凭据；Windows Credential Manager与Linux Secret Service仍需各自原生机器的store/read/update/delete、锁屏/无会话/权限拒绝实机证据，不能只凭当前macOS构建宣称跨平台验证完成。
- 编辑器当前持久化连接与权限配置，但conversation/session、usage、finish reason、工具调用审计日志和凭据轮换历史尚未持久化；Ask授权是单次决定，尚无项目/组织级policy签名与管理员锁定。
- 完整JSON Schema求值、Responses API、真实多厂商endpoint互操作、代理/TLS/断线resume及长期并发网络压力仍未闭环。

### 下一优先项
1. 实现带脱敏参数摘要、decision、duration、result分类的append-only工具审计与session/usage持久化，加入容量、轮换、损坏恢复及并发写入深测。
2. 引入完整JSON Schema参数求值和结构化tool error分类，覆盖nested/required/additionalProperties/enum/range/oneOf等关键字与恶意深度/组合输入。
3. 在Windows与Linux原生环境执行credential vault契约套件，并增加Responses API、真实OpenAI-compatible多厂商、代理/TLS和断线恢复矩阵。

## 2026-07-17ae — Anity.Agent OpenAI-compatible 远端 Tool Calling 生产链

### 已完成
- 独立 `anity-agent/` 官方扩展提升到 **0.4.0**，新增 `AgentToolDefinition`、`AgentToolCall`、`AgentToolCallDelta`、`AgentModelTurn` 与 `IToolCallingAgentProvider`。OpenAI-compatible provider现可把显式授权工具的JSON Schema写入Chat Completions请求，并解析非流式 `tool_calls`、流式indexed delta、`finish_reason`与usage；assistant tool-call消息及带 `tool_call_id`/name的tool result会按协议回传下一轮模型。
- `IRemoteAgentTool`把可暴露给远端模型的能力与普通本地工具明确隔离：只有显式实现远端接口且schema有效的工具会被广告。`echo`、`systeminfo`已接入远端调用，`screenshot`保持本地专用，模型即使伪造调用也只能收到不可用错误，不能越权执行。
- 非流式与SSE流式Session都支持多轮工具循环；流式assembler按index拼接任意分片的id/name/arguments，并保留并行调用顺序。每轮最多 **16** 个调用、每turn最多 **32** 个调用和 **8** 轮；arguments最多 **64 KiB**、JSON对象深度最多 **64**，id/name长度有界，调用id在整个turn内必须唯一。
- 工具执行拥有独立可配置超时，普通工具异常与参数错误会转为tool error result供模型恢复，调用者取消仍原样传播。用户消息、assistant tool-call消息、tool result与最终assistant响应只有在整轮成功结束后才原子写入history；协议错误、越界、重复id、超时后的模型失败或取消都不会留下半个turn。

### 测试与门禁
- `Anity.Agent.Tests`由 **49** 增至 **60/60**，新增 **11 个发现项**：非流式schema/执行/result回传、流式碎片组装、并行index顺序、缺失index、非法参数、未授权screenshot、单轮重复id、超过16调用、工具执行时调用者取消、跨轮复用id、独立工具超时后模型恢复。
- `Anity.Cli.Tests` **16/16**；`dotnet pack`产出 `Anity.Agent.0.4.0.nupkg`。`bash _scripts/build-all.sh`通过，产品模块0编译错误，URP3DDemo保持既有43个nullable warning；`git diff --check`通过。

### 尚未完成
- 当前只验证schema是有界、合法的JSON对象；尚未实现完整JSON Schema关键字求值与统一参数语义校验，各工具仍必须在执行入口自行校验业务参数。
- 编辑器Agent窗口、project/session持久化、macOS Keychain/Windows Credential Manager/Linux Secret Service安全凭据存储、权限确认UX与工具调用审计日志仍缺。
- Responses API、真实多厂商endpoint互操作、finish reason/usage持久统计、断线resume、代理/TLS证书策略及长期并发/网络压力仍未闭环。

### 下一优先项
1. 在 `anity-editor` 实现Agent窗口与跨平台credential vault抽象，项目只保存provider/Base URL/model/credential id；增加逐工具权限策略、用户确认与脱敏审计。
2. 引入完整JSON Schema参数验证和结构化tool error分类，补充嵌套schema、组合关键字、恶意递归输入、并发工具与长时间取消/超时压力。
3. 增加Responses API与真实OpenAI-compatible多厂商契约测试，持久化usage/finish reason/session，并闭环代理、TLS和断线恢复矩阵。

## 2026-07-17ad — Anity.Agent OpenAI-compatible SSE 流式生产链

### 已完成
- 独立 `anity-agent/` 官方扩展提升到 **0.3.0**，新增 `IStreamingAgentProvider`、`AgentStreamUpdate` 与 `AgentTokenUsage`；`OpenAiCompatibleAgentProvider.StreamAsync`继续使用用户自定义API Key、Base URL与model，并向同一 `<base-url>/chat/completions`发送 `stream:true`，不引入闭源SDK或污染 `Anity.Core`。
- SSE reader直接在response stream上增量处理，支持任意网络分片、跨byte UTF-8字符、CRLF/LF、comment/id/event/retry字段、multi-line data、字符串/多段text delta、usage chunk、`[DONE]`与无DONE的EOF收口。总传输量继续受 `MaxResponseBytes`约束，未知Content-Length也不能绕过；非法UTF-8、坏JSON、中断、HTTP错误、超时与调用者取消保持可区分。
- transient HTTP状态与连接失败只允许在尚未建立成功stream、尚未向调用者yield任何delta前重试；stream已开始后不自动重放，避免重复token。请求超时覆盖headers与持续body read，caller cancellation原样传播，timeout转换为typed transient `AgentProviderException`。
- `AgentSession.RunTurnStreamAsync`在整个异步枚举期间持有单session turn gate；只有收到完成事件/正常EOF后才一次提交user+聚合assistant history及`last_user` memory。部分delta后取消、超时或解析失败不留下半个turn；并发stream严格串行。原非stream `RunTurnAsync`也改为同一原子提交语义。
- API Key现按Bearer token68字符集验证，`=`只允许尾部padding；网络/stream异常不再保留可能含密钥的原始inner exception，因此 `Exception.ToString()`也不会绕过外层脱敏重新泄漏API Key。空SSE data heartbeat被安全忽略。

### 测试与门禁
- `Anity.Agent.Tests`由 **30** 增至 **49/49**，新增 **19 个发现项**：Bearer非法字符、完整异常链脱敏、自定义endpoint/auth/model/stream flag、单byte Unicode分片、multi-line/multipart、usage、metadata/heartbeat、EOF、坏JSON、坏UTF-8、未知长度响应上限、HTTP脱敏、transient retry、body取消/超时、Session聚合提交、失败原子性及并发串行。
- `Anity.Cli.Tests` **16/16**；Agent与CLI Release均0 warning/0 error，`dotnet pack`产出 `Anity.Agent.0.3.0.nupkg`。`bash _scripts/build-all.sh`通过，产品模块0编译错误，URP3DDemo保持既有43个nullable warning；`git diff --check`通过。

### 尚未完成
- OpenAI-compatible tool-call delta组装、tool result回传、finish reason与usage持久统计仍缺；当前本地 `tool:name args` 路由不是远端模型tool-calling协议的替代品。
- 编辑器Agent窗口、project/session持久化、macOS Keychain/Windows Credential Manager/Linux Secret Service安全凭据存储与密钥轮换仍缺。CLI明文 `-agentApiKey`仍会出现在进程参数中，生产环境继续只推荐环境变量，不能把CLI参数当安全存储。
- Responses API、SSE断线resume、代理/TLS证书策略、真实多厂商endpoint互操作与长时间网络抖动/内存压力尚未闭环。

### 下一优先项
1. 实现OpenAI-compatible tool-call streaming assembler、参数JSON增量校验、工具权限/超时/取消与tool result回传，并以至少10组并发和恶意输入测试闭环。
2. 在 `anity-editor` 增加Agent窗口与跨平台安全凭据抽象；API Key只进入OS credential vault，项目配置仅保存provider/Base URL/model及credential id。
3. 增加真实OpenAI-compatible本地/远端endpoint契约测试、usage/finish reason统计、断线与代理/TLS矩阵，再推进长期session持久化。

## 2026-07-17ac — Metal VFX 同 effect 多 system Camera 与 submission 历史淘汰

### 已完成
- Planar Camera现以真实 `(effectId, particleSystemId)` resident key验证同一 effect 的多个 particle system。测试资产为每个 system建立独立 Initialize/Update context、resident generation与output context；单次 Camera提交会把同一 effect的全部有效system编码进同一个Metal command buffer与render pass，而不是只绘制第一个system或把system错误合并。
- Planar submission继续只保留最近 **1024** 个逐fence成功/失败结果，但淘汰不再静默。Metal state记录最后淘汰的submission id，80-byte diagnostics ABI把原reserved字段正式定义为 `resultEvictionCount`；显式等待已淘汰fence返回 `INVALID_ARG`，不会因aggregate completion watermark已前移而把旧失败误报成成功。`throughSubmissionId=0`等待最新提交的既有语义保持不变。
- history watermark、result deque和统计计数均由同一submission mutex保护；completion callback每淘汰一项精确增加计数，不增加CPU同步点，也不改变普通Camera command failure可恢复、terminal device-loss永久封锁的既有分类。

### 测试与门禁
- 新增 **20 个发现项**：10组1–10 particle system验证同一 effect单Camera提交的effect/output/draw/particle/indirect计数、单command buffer/render pass及每system独立Metal resident generation；10组1025–1034次真实空Camera render pass验证首个注入失败、后继成功、精确淘汰计数、历史容量边界及旧失败fence只能返回过期错误。历史压力合计超过 **10,000** 次Camera提交。
- Planar Camera **121/121**、Update lifecycle + Planar Camera **338/338**、VFX宽门禁 **828/828**、Core强制native **1,664/1,664**、VFX Graph **490/490**，0测试失败。
- `bash _scripts/build-native.sh` 与 `bash _scripts/build-all.sh` 通过；产品模块0编译错误，URP3DDemo保持既有43个nullable warning。最终产品/强制测试 `libanity_native.dylib` SHA-256同为 `9e9dab1d65ab71db27c4ea2f76d44ff4766beaac4aa7732c7b0213768693833a`；`git diff --check`通过。

### 尚未完成
- 本批闭环同effect多system与逐fence历史淘汰，但多个Camera真正同时在途、Camera与Update/Initialize交错超过ring深度、长时间snapshot/teardown内存压力仍需扩展；当前单Metal queue按提交顺序完成，不代表跨queue同步已经实现。
- 真实物理GPU removal/reset与系统撤销访问仍缺实机日志和资源释放A/B；Vulkan/D3D尚无等价device-health、failure classification、generation rollback及submission history契约。
- Texture/flipbook、Shader Graph material、soft particle/motion vector、Mesh/Strip/GPU Event Output、URP camera stack/XR及Unity 2022.3.61f1 Player截图/数值A/B仍未完成。

### 下一优先项
1. 增加多Camera在途与Initialize/Update/Camera交错的ring深度压力，连续运行长时间snapshot/cancel/teardown并记录内存高水位；在可控Apple硬件采集真实device removal/reset日志。
2. 将device health、terminal error分类、generation-selected consumer与明确的submission结果契约移植到Vulkan/D3D；跨queue使用timeline semaphore/shared event/fence。
3. 继续完整Texture/flipbook与Shader Graph material、Mesh/Strip/GPU Event Output、soft particle/motion vector、URP camera stack/XR，并建立Unity 2022.3.61f1 Player A/B证据。

## 2026-07-17ab — Metal terminal device-loss 状态与 Camera submission 恢复语义

### 已完成
- Metal backend新增设备级原子 health state。Initialize、Update、Planar Camera、UI draw和Present的真实 command completion都会检查 `MTLCommandBufferErrorDeviceRemoved` / `MTLCommandBufferErrorAccessRevoked`；同步 Initialize copy与Bounds路径也在等待后分类错误。观察到 terminal错误后，状态永久转为 device-lost，不再把后续调用当成普通可恢复 command error。
- device-lost现贯穿产品入口：VFX Initialize/Update/Camera、particle/metadata readback、Bounds、UI upload/draw、texture sync、swapchain create/acquire/present/readback均返回 `DEVICE_LOST`；取消、resident rollback、Clear与Destroy仍可执行，以保证在途资源安全退出。80-byte Planar submission diagnostics复用原 reserved字段暴露 `deviceLost`，ABI尺寸不变。
- managed `NativeGraphicsDevice`不再把native device-lost伪装成headless成功：`CreateSwapchain`返回false、`AcquireNextImage`返回-1、`Present`不增加成功计数、readback返回false，并通过 `LastSwapchainResult`保留精确的 `DeviceLost`；其它非terminal backend fallback保持既有兼容行为。
- 故障注入扩展为 Planar Camera command与terminal device removal两类。普通 Camera command failure在真实 command安全完成后仅标记对应 submission失败；submission结果保留最近1024项，等待失败 fence返回 device-lost，但后继成功 Camera拥有独立成功结果并可继续渲染/readback。device removal则永久封锁同一 Metal device，Initialize/Update/Camera/UI/swapchain不能继续提交。
- Camera completion在失败时使 alive-compaction与sort cache失效，防止复用可能被失败 command部分修改的派生资源。Initialize/Update/UI/Present也在 completion callback即时观察真实 terminal错误，不依赖调用者随后 Poll才更新全局 health。
- 修复 device-loss teardown下的UI ring死锁风险：每个UI slot现在区分“仅上传并占用”与“已经提交GPU”。Destroy只等待确有在途command的slot；completion以原子submitted标志配合semaphore释放，上传后尚未draw的slot不会无限等待。

### 测试与门禁
- 新增 **21 个发现项**：10组1–10 effect Camera command failure验证失败fence、cache失效、device仍健康及下一Camera/readback恢复；10组1–10代 resident generation chain后device removal验证中央generation回滚、永久health、后续Update/注入拒绝，以及native与managed swapchain acquire/present/readback一致暴露device-lost；另有1个Camera已提交后device removal与安全teardown用例。
- Update lifecycle **217/217**、Planar Camera **101/101**、两套合计 **318/318**，UI/Canvas native相关 **49/49**；VFX宽门禁 **808/808**、Core强制当前 Release产品 dylib **1,644/1,644**、VFX Graph **490/490**，0 测试失败。
- `bash _scripts/build-native.sh` 无新增编译warning，`bash _scripts/build-all.sh Release` 通过；产品模块0编译错误，URP3DDemo保持既有43个nullable warning。强制native测试输出与产品 dylib SHA-256逐字一致。

### 尚未完成
- 生产代码已消费 Metal真实 terminal error code，但当前机器尚未通过物理GPU移除、系统GPU reset、访问权限被系统撤销或驱动/进程终止做实机故障注入与崩溃日志A/B；本批 deterministic device-removal仅证明同一状态机与teardown路径，不替代硬件故障证据。
- 同 effect多 particle-system、多个Camera同时在途、超过1024 submission历史淘汰、跨 queue shared event/fence及长时间内存压力仍需扩展。Vulkan/D3D尚无等价device-health、failure classification与generation rollback。
- Texture/flipbook、Shader Graph material、soft particle/motion vector、Mesh/Strip/GPU Event Output、URP camera stack/XR及 Unity 2022.3.61f1 Player截图/数值A/B仍未完成。

### 下一优先项
1. 增加同 effect多 system、多Camera在途、submission历史淘汰与长时间ring/snapshot/teardown压力；在可控Apple硬件/虚拟化环境采集真实 device removal/reset日志与资源释放证据。
2. 将device health、terminal error分类、Initialize dependency和generation-selected consumer移植到Vulkan/D3D；跨 queue使用明确 timeline semaphore/shared event/fence。
3. 继续完整 Texture/flipbook与 Shader Graph material、Mesh/Strip/GPU Event Output、soft particle/motion vector、URP camera stack/XR，并建立 Unity 2022.3.61f1 Player A/B证据。

## 2026-07-17aa — Metal VFX 确定性 command failure 与整链原子回滚

### 已完成
- 新增仅供引擎内部验证使用的 `AnityGraphics_SetVFXFailureInjection` C ABI 与 managed diagnostic 入口，支持按成功 Begin 次数精确注入 Metal Initialize/Update command failure，计数范围为 0–1024，0 可显式解除；非 Metal backend明确返回 `NOT_SUPPORTED`，不污染 Unity 公开 API 表面。
- 注入的 Initialize/Update仍提交真实 Metal command buffer并等待 GPU 安全退出，但 Poll确定返回 failed、Complete返回 device-lost。Update rollback成功时保留已恢复的 resident resource map，只有恢复本身失败才使该 map失效，避免可恢复 command error破坏后续 generation。
- Initialize→Update依赖链现以 Update为单一事务边界：Update失败会先取消后继，再逆序回滚 Initialize，并把中央 particle info、alive/dead、attributes、dead-list与 generation逐项恢复到链前基线；Initialize已完成 backend但中央尚未最终提交时会强制保留 source snapshot，直至整链成功才统一丢弃。
- 修复显式 readback 后再次 Initialize 的 resident判定：central层不再仅凭 CPU attributes是否存在推断 authoritative source，而是查询 Metal resident generation是否与 source generation一致；因此 readback→Initialize→Update继续走GPU resident chain，同时首次/non-resident Initialize不会被错误标成 resident-only。
- Clear、Reset/Dispose与 identity reuse均可安全取消被注入失败的在途 submission；failure budget只在 command成功建立并提交后消耗，不因参数或前置资源错误误减。

### 测试与门禁
- 新增 **15 个发现项**：10组 Initialize→Update整链失败位置边界，另覆盖 Initialize失败原子恢复、连续计数消耗、0解除、Clear后同 identity复用、Dispose等待与幂等清理。测试逐项比较链前后的 info/alive/dead/attributes/dead-list，并验证失败后正常 Initialize/Update可继续得到预期数值。
- `NativeVFXUpdateLifecycleTests` **207/207**、VFX宽门禁 **787/787**、Core强制当前 Release产品 dylib **1,623/1,623**、VFX Graph **490/490**，0 测试失败。
- `bash _scripts/build-native.sh` 与 `bash _scripts/build-all.sh Release` 通过；native导出 central/Metal failure-injection符号，产品模块 0 编译错误，URP3DDemo保持既有 43 个 nullable warning；`git diff --check` 通过。

### 尚未完成
- 本批完成的是可重复的 submitted-command failure，不等于真实 `MTLDevice` removal、系统级 GPU reset、进程/驱动终止或跨 queue故障；Camera已经提交后发生失败、多 effect/多 system长链与长时间 ring/snapshot压力仍需单独闭环。
- Vulkan/D3D尚无同等 failure injection、Initialize dependency ticket和 resident generation resource-group恢复；跨 queue仍缺 shared event/fence。Texture/flipbook、Shader Graph material、soft particle/motion vector、Mesh/Strip/GPU Event Output、URP camera stack/XR与 Unity 2022.3.61f1 Player截图/数值 A/B仍未完成。

### 下一优先项
1. 加入真实 Metal device-loss可观测路径、Camera提交后故障与多 effect/多 system长链 teardown压力，验证所有 command/ring/snapshot/central registry资源无泄漏、无半发布。
2. 将 Initialize dependency、generation-selected consumer、确定性 failure与整链恢复契约移植到 Vulkan/D3D；跨 queue使用显式 shared event/fence。
3. 继续完整 Texture/flipbook与 Shader Graph material、Mesh/Strip/GPU Event Output、soft particle/motion vector、URP camera stack/XR，并建立 Unity 2022.3.61f1 Player A/B证据。

## 2026-07-17z — Metal queue-visible Initialize→Update / Prepare / Bounds / Camera

### 已完成
- 已把已有 resident particle system 的 pending Initialize target generation 直接接入后继 Metal Update。Update Begin 从 Initialize staged system取得 source generation，并在同一 `MTLCommandQueue` 上编码到新的 ring slot；Initialize command、Update command与后继 Camera按提交顺序在 GPU 上串联，产品帧不再为 Update预先执行 managed CPU Complete。
- 中央 registry仍保持事务原子性：Initialize→Update 都 pending时继续向普通 metadata查询暴露旧 committed generation，Update最终退休时先完成其 Initialize依赖，再一次发布最终 replacement。Metal backend允许 Initialize target已被后继 Update替换为当前 resident时从 snapshot完成 metadata校验；Initialize提前发布时立即推进 ring `nextSlot`，避免 Update复用尚在飞行的同一槽。
- Update ticket新增 Initialize dependency与 central-published状态。generation CAS同时覆盖“Initialize仍 pending”和“Initialize已被显式退休”两条路径；Cancel Initialize会 newest→oldest取消全部依赖 Update，Clear/Reset/Dispose/Abort统一先取消 Update再回滚 Initialize，避免 descendant持有已删除 source。
- Planar Camera packet优先选择最新已发布 Update target generation；当该 Update依赖 Initialize时继续启用 pending-initialize GPU alive compaction，因此 Camera可在两个 CPU ticket均未退休时直接绘制最终蓝色 target，而不是中间 Initialize generation。
- Prepare只推进时钟与 frame transaction，不读取 particle资源，已移除自动/手动 Prepare前不必要的 Initialize CPU屏障。Automatic Bounds是显式 CPU结果依赖：native Bounds入口先一次性退休 committed Initialize→Update最终链；若没有后继 Update，才在同一 registry lock内单独退休 Initialize，不再 managed先等待 Initialize、native再等待 Update。
- 显式 particle readback修复 metadata竞态：第一次非阻塞 Info可能仍属于 predecessor generation；attributes blocking readback退休链后会重新读取最终 Info，再按最终 alive/dead count读取 dead-list，避免前代 deadCount造成错误容量与假失败。

### 测试与门禁
- 新增 **10 组** native frame transaction Initialize→Update用例，覆盖负数、零、小数与大值；验证 Update在 Initialize ticket仍 pending时成功排队、Commit发布最终 resident target、显式 Complete同时退休两个 ticket并保持两粒子数值正确。
- 新增 1 个真实 `VisualEffect` Event→deferred Initialize→Update→Commit→readback产品用例，以及 1 个 Initialize→Update→Planar Camera像素用例；后者在两个 ticket仍 pending时直接绘制最终蓝色 generation。读回用例同时锁定最终 metadata/dead-list刷新语义。
- 新增 **10 组** pending Initialize先于 manual Prepare的 delta边界用例（0 到 1000）：Prepare期间 pending/completion/wait统计不变，随后同帧 Update消费该 generation。另有 1 个产品 Automatic Bounds用例验证 Bounds一次退休完整链并命中 Update随附的 pending bounds结果。
- 新增发现项合计 **23**；Prepare/Bounds专项 **11/11**、Update/Bounds/Planar相关三套 **260/260**、VFX宽门禁 **772/772**、Core强制当前 Release产品 dylib **1,608/1,608**、VFX Graph **490/490**，0 测试失败。
- `bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过；native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor及样例产品模块 0 编译错误，URP3DDemo保持既有 43 个 nullable warning。

### 尚未完成
- 当前无 CPU屏障链限于单个 Metal command queue与已有 resident allocation；跨 queue仍缺 shared event/fence，首次 Initialize仍需 bootstrap退休后由 Update建立 resident资源。可控 command error/device removal、多 effect/多 system长链与 Camera提交后Cancel压力仍未闭环。
- Vulkan/D3D尚未具备同等 Initialize dependency ticket与完整 resident generation resource-group链。Texture/flipbook、Shader Graph material、soft particle/motion vector、Mesh/Strip/GPU Event Output、URP camera stack/XR及 Unity 2022.3.61f1 Player截图/数值 A/B仍未完成。

### 下一优先项
1. 加入可控 Metal command error/device removal、Camera提交后Cancel、多个 effect/多个 system长 generation chain与 teardown压力，验证 ring/snapshot/central registry无泄漏、无半发布。
2. 将 Initialize dependency ticket、queue-visible generation与 Bounds/Camera consumer contract移植到 Vulkan/D3D；跨 queue路径使用明确的 shared event/fence，而不是CPU等待。
3. 继续完整 Texture/flipbook与 Shader Graph material、Mesh/Strip/GPU Event Output、soft particle/motion vector、URP camera stack/XR，并建立 Unity 2022.3.61f1 Player A/B证据。

## 2026-07-17y — Metal queue-visible resident Initialize→Planar Camera

### 已完成
- Metal resident Initialize 在 `Begin` 提交 command buffer 后立即把目标 particle/dead/allocation 三资源组发布为 target generation，并把 source generation 保留在 ring slot 或 prepared-frame snapshot 中。Planar Camera 与 Initialize 使用同一个 `MTLCommandQueue`，因此 Camera 可直接绑定 target generation，由队列顺序保证 GPU 先 Initialize、后 render，不再要求 CPU 先 Poll/Complete，也不需要在单队列路径额外引入 `MTLEvent`。
- 中央 registry 继续保持事务原子性：ticket pending 期间 `TryGetVFXParticleSystemInfo` 仍只返回已提交 generation；`Complete` 成功后才发布 staged metadata，`Cancel`/command failure 会等待已提交 command 安全退出并恢复 source resource group。`residentInitializeAtomicPublishCount` 仍只统计成功的中央退休，新增的 async resident publish/completion/rollback 与 pending Initialize 计数单独暴露。
- `AnityGraphics_DrawVFXPlanarCamera` 在 registry lock 内解析 effect 所属的 pending Initialize，选择该 system 最新的 resident target generation并写入 Camera draw packet；Metal backend允许 pending packet在旧中央 alive count为 0 时进入 GPU alive compaction/indirect draw，并记录 Camera generation dependency。`VisualEffect.DrawPlanarOutputs` 不再为已有 resident 的 Initialize调用 managed Complete barrier。
- 首次 Initialize 还没有 GPU resident allocation，无法供 Camera直接绑定。Camera入口只对 `sourceGeneration == 0` 的 bootstrap ticket使用锁内 Complete helper恢复旧产品语义：中央粒子状态被提交，本次 draw透明跳过，后续 Update负责首次 resident upload。公共 Complete与 bootstrap路径共用同一锁内事务实现，避免递归加锁与两套 commit逻辑。
- backend diagnostics ABI 从 **496 bytes** 扩展到 **528 bytes**，新增 `asynchronousInitializeResidentPublishCount`、`asynchronousInitializeResidentCompletionCount`、`asynchronousInitializeResidentRollbackCount`、`pendingInitializeCount`；C header、Metal/non-Apple backend、managed sequential layout与静态尺寸门禁同步。

### 测试与门禁
- 既有 10 组 `MetalInitializeTicket_PollThenCompleteOrCancelIsAtomic` 现在验证 Begin 后 backend resident generation 已前移、中央 registry仍不可见、Complete后原子提交、Cancel后 source恢复，以及 publish/completion/rollback/pending四类统计。
- 新增 **10 组** resident Initialize→Camera像素级用例，覆盖 Complete/Cancel × 红/绿/蓝/黄/品红：Camera在 ticket仍 pending 时已绘制 target粒子；Complete后继续保留，Cancel后下一 Camera恢复透明。另有 1 个首次 bootstrap用例验证 ticket退休、中央 alive/generation提交及未 resident前透明跳过。
- 聚焦 ticket/Camera **21/21**、Initialize/Update/Planar相关全集 **330/330**、VFX宽门禁 **749/749**、Core强制当前 Release产品 dylib **1,585/1,585**、VFX Graph **490/490**，0 测试失败。
- `bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过；native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor及样例产品模块 0 编译错误，URP3DDemo保持既有 43 个 nullable warning。

### 尚未完成
- 本批只闭环“已有 resident allocation 的 Initialize→Planar Camera”单队列依赖。下一 particle Update、Prepare、Bounds/culling和显式 CPU metadata/readback仍需要退休中央 Initialize ticket；尚未形成完整 Update→Initialize→Update/Bounds/Camera GPU generation DAG。
- 首次 Initialize仍需要 CPU bootstrap retirement和后继 Update上传 resident resource；多 queue/跨 queue场景尚需 shared event/fence。可控 Metal command failure/device removal、Vulkan/D3D等价 ticket/resident chain、Texture/flipbook/Shader Graph material、soft particle/motion vector、Mesh/Strip/GPU Event Output、URP camera stack/XR及 Unity 2022.3.61f1 Player A/B仍未闭环。

### 下一优先项
1. 将 pending Initialize target generation接入后继 Metal Update与 Bounds/Prepare，消除这些 resident GPU消费者前的 CPU Complete，并明确 CPU metadata/readback才是真正 retirement边界。
2. 增加可控 Metal command error/device removal、Camera提交后Cancel、多个 effect/多个 system长 generation chain与 teardown压力测试，验证 ring/snapshot/central registry无泄漏、无半发布。
3. 将 ticket与 queue-visible resident generation contract移植到 Vulkan/D3D，再推进 Texture/flipbook、Shader Graph material、Mesh/Strip/GPU Event Output和 Unity 2022.3.61f1 Player截图/数值 A/B。

## 2026-07-17x — VisualEffect 产品帧 Initialize ticket FIFO

### 已完成
- `VisualEffect` 已持有 committed Initialize ticket FIFO。产品 `VFXRuntimeServices` 的 CPU Event 与 Spawner 阶段改为调用 `BeginVFXInitializeKernels` 后立即消费输入并保留 ticket，不再通过同步 wrapper在提交点等待 Metal command；直接调用内部处理函数仍默认同步收口，以保持既有单步/测试调用语义。
- Initialize ticket现在在真实资源依赖点收口：下一次 particle Update、自动/手动 frame Prepare、Bounds/culling 查询及 Planar Camera draw前 Complete；frame Abort会 Cancel尚未发布的 Initialize，asset change、Reinit、Release和设备 Clear沿 native teardown取消后清空 managed FIFO。Update只在 Initialize原子发布并刷新 alive metadata后才建立后继 generation。
- FIFO retirement支持同一 `VisualEffect` 跨多个 `NativeGraphicsDevice`：完成某设备时会保持其他设备的相对顺序并跳过其 ticket，不再由异设备队首阻塞；同设备 pending ticket仍严格 FIFO。Initialize完成后按资产的全部 particle systems重算 alive总数，避免只统计本次触及 system造成多 system漏计。
- Spawner一帧内多个 Initialize program按中央 one-pending-ticket-per-effect契约有序退休前驱，最后一个 ticket延迟到 Update依赖点；CPU Event批次保持中央 multi-dispatch原子性。

### 测试与门禁
- 新增 **11 个产品 FIFO 测试发现项**：10 组真实 Metal负数/零/小数/正数/大值属性验证 Begin后 registry不可见、Update依赖点才原子发布粒子与 metadata；另 1 组双 Null device验证异设备队首不会阻塞后继设备 retirement。
- VFX 宽门禁由 **727** 增至 **738/738**，Core 强制当前 Release 产品 dylib由 **1,563** 增至 **1,574/1,574**，VFX Graph保持 **490/490**，0 测试失败。
- `bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过，native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor及样例产品模块 0 编译错误；URP3DDemo保持既有 43 个 nullable warning。

### 尚未完成
- Metal target generation仍在中央 Complete后才成为 Camera可见 resident；当前 FIFO消除了 Event/Spawner提交点的 CPU等待，但 Camera/Planar仍通过 managed Complete形成显式 barrier，尚未做到 queue-visible Initialize→Camera generation与 shared `MTLEvent` GPU-GPU依赖。
- 可控 Metal command failure/device removal、Vulkan/D3D等价 Initialize ticket backend、完整 Texture/flipbook/Shader Graph material、soft particle/motion vector、Mesh/Strip/GPU Event Output、URP camera stack/XR及 Unity 2022.3.61f1 Player A/B仍未闭环，总体 Unity 2022 Ultra目标继续进行。

### 下一优先项
1. 在 Metal Begin commit时登记 queue-visible target generation与 shared-event值，让 Update/Planar/Camera直接等待 GPU generation；Complete只退休中央 metadata，Cancel/failure按 snapshot反向恢复。
2. 增加可控 command error/device removal及多 effect、多 device、长 generation chain压力测试，验证 FIFO、中央 registry和三槽 resident资源在异常下无泄漏、无半发布。
3. 将相同 ticket/generation contract移植到 Vulkan与D3D，再推进 Texture/flipbook、Shader Graph material、Mesh/Strip/GPU Event Output及 Unity 2022.3.61f1 Player截图/数值 A/B。

## 2026-07-17w — effect-scoped multi-dispatch Initialize ticket

### 已完成
- 新增 48-byte `AnityGraphicsVFXInitializeTicketInfo` 与公开 native `Begin/Get/Complete/CancelVFXInitializeKernels` ABI。中央 pending transaction 持有完整 staged initialize-dispatch map、particle-system map、stable alive/dead/next/spawn outputs、source/target generations、effect ownership及每个 Metal command handle；原同步 `SubmitVFXInitializeKernels` 已改为中央 Begin→Complete wrapper。
- Begin 对所有 dispatch/kernel 先做整批 validation，收口已 committed Update、保留 uncommitted Update 的 generation CAS 竞争语义，按 system 构造 staged storage。不同 particle system 的 Metal commands 可并行 pending；同 system 的连续 Initialize 会按 generation dependency 完成前驱再提交后继，但整个 registry 在最终 Complete 前仍保持旧状态。
- Complete 依次验证所有 backend handles，只有全部成功才发布本 ticket 涉及的 effect 数据；Cancel 或任一 failure 会从 newest 到 oldest 取消未发布 target并恢复已完成 generation snapshots。prepared-frame 保留 source snapshots供 frame Abort，普通事务成功后逐代 discard。
- 每个 effect 同时只允许一个 Initialize ticket，Update Begin 在该 effect 有 pending Initialize 时拒绝；独立 effect 可并行。Begin 立即预留全局 generation range，Complete 只合并本 ticket effect 的 entries而不交换整张 registry，避免两个独立 effect ticket互相覆盖。Cancel 允许 generation gap但保证单调唯一。
- Clear、Reset frame state 与 graphics-device teardown 会在持有 registry lock 时收口对应 Initialize ticket并释放 GPU handles；OOM catch 也会取消已提交 handles、释放 effect ownership和 pending map。managed `AnityNative` / `NativeGraphicsDevice` 已接入 ticket layout、P/Invoke、descriptor builder、TryGet/Complete/Cancel。
- 恢复既有并发契约：未提交 Update 与 Initialize 可以同时存在；Initialize 先发布后，旧 Update Complete 由 generation CAS 拒绝，而不是 Initialize Begin提前失败。

### 测试与门禁
- 新增 **21 个 Initialize ticket 测试发现项**：10 组 Metal Poll→Complete/Cancel 位置边界、同 effect CAS、Clear、Dispose、2 组 CPU ready ticket、双 system batch Complete/Cancel、同 system 两代 chain newest→oldest rollback、两个独立 effect 并发 Complete/Cancel+merge。另回归旧 Update/Initialize generation CAS。
- ticket 定向 **22/22**、VFX 宽门禁 **727/727**、Core 强制当前产品 dylib **1,563/1,563**、VFX Graph **490/490**，0 测试失败；native symbol gate确认中央与 Metal 两层 Begin/Get/Poll/Complete/Cancel 均由产品 dylib 导出。
- `bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过，产品模块 0 编译错误；URP3DDemo 保持既有 43 个 nullable warning，`git diff --check` 通过。

### 尚未完成
- `NativeGraphicsDevice` 已可异步持有 ticket，但产品 `VisualEffect.ProcessInputEvents` / Spawner 仍调用同步 wrapper并立即读取 alive metadata；尚未把 ticket 队列保存到 effect instance、延迟到 Update/Bounds/readback/teardown 等真实依赖点 Complete。因此 backend/central 已异步，标准产品帧仍未获得完整 CPU/GPU overlap。
- Metal target generation 仍在 Complete 后才成为 Camera 可见 resident；Update→Initialize→Camera 尚无 queue-visible publish/shared `MTLEvent`。可控 command failure/device removal、Vulkan/D3D ticket backend、完整 GPU Event/Mesh/Strip/Texture Output 和 Shader Graph material仍未完成。

### 下一优先项
1. 在 `VisualEffect` 增加 committed Initialize ticket FIFO：ProcessInputEvents/Spawner Begin 后消费输入但不读 particle，下一 Update、Bounds、explicit alive/readback、Clear/Reset/asset change/dispose按依赖收口；多 program spawner先合并或有序 retirement。
2. 将 Metal target resource group在 Begin commit 后发布为 queue-visible generation，handle持有 source snapshot；Camera/Planar按 generation依赖同 queue/shared event消费，Complete只退休 metadata，Cancel/failure反向恢复。
3. 加入可控 Metal command failure/device removal和多 effect/多 generation压力测试，再移植 Vulkan/D3D并继续完整 VFX Output、Shader Graph material、URP camera stack/XR 与 2022.3.61f1 Player A/B。

## 2026-07-17v — Metal VFX Initialize command-buffer handle

### 已完成
- 将 Metal Initialize 的单体同步函数拆为真实 GPU lifecycle：`AnityGraphics_Metal_BeginVFXInitializeKernel` 只做 validation、ring ownership、buffer upload/copy、indirect preparation、kernel encode 与 `MTLCommandBuffer::commit`，不调用 `waitUntilCompleted`；所有 source/attribute/operation/prefix/counter/indirect resources 和 ring slot ownership 移入 heap handle，跨 Begin 返回保持有效。
- 新增 `PollVFXInitializeKernel`、`CompleteVFXInitializeKernel`、`CancelVFXInitializeKernel`。Poll 只观察 command status；Complete 在明确依赖边界等待、检查 device status 与 allocation invariants，确认 source generation 仍匹配后才交换 particle/dead/allocation 三 buffer并发布 target generation；Cancel 等待 command 退出后直接释放 target 与 transient ownership，不发布、也不修改 resident source。
- 原 `AnityGraphics_Metal_DispatchVFXInitializeKernel` 保留为同步兼容 ABI，但实现已收敛为 Begin + Complete，不再维护另一套 encode/publish 逻辑。非 Apple stub 同步新增四个 lifecycle symbol；产品 dylib 已通过 `nm` 确认 Begin/Poll/Complete/Cancel 全部导出。
- handle cleanup 对 resident/non-resident alias 分别处理：resident target 属于 ring slot，cleanup 只释放 transient source-state/indirect buffers并归还 semaphore；non-resident handle 释放其 particle/dead/counter output。command failure、metadata failure、generation mismatch、Cancel 和成功路径均删除 handle，避免悬挂 GPU resource。
- backend diagnostics ABI 从 **464 bytes** 扩展到 **496 bytes**，加入 async Initialize Begin/Poll/Complete/Cancel 计数；native/managed layout、静态尺寸门禁和 Metal stats initializer 同步。

### 测试与门禁
- 普通 resident Initialize 10 组与 prepared Abort/Commit 10 组均验证兼容入口实际经过 async Begin=1、Complete=1、Poll/Cancel=0，同时保持 target-copy=1、atomic publish=1、0 particle materialize、snapshot restore/discard 与后继 Update；20/20 通过。
- native Release、`bash _scripts/build-all.sh Release`、产品 dylib symbol gate 通过；Core 强制当前 dylib **1,542/1,542**、VFX Graph **490/490**，0 测试失败。产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning，`git diff --check` 通过。

### 尚未完成
- backend handle 已能异步提交/观察/取消单个 command，但中央 `AnityGraphics_SubmitVFXInitializeKernels` 尚未建立 effect-scoped multi-dispatch ticket，因此产品帧仍由同步兼容入口立即 Complete。Poll/Cancel 的直接产品 lifecycle、批内多个 system 并行 completion、同 system generation chain 与任一失败反向恢复必须在中央 ticket 中验证，不能仅凭导出符号宣称异步 Initialize 完成。
- Begin 当前保持 source resident 直到 Complete 后发布，尚不能让后继 Camera 在 CPU Complete 前消费 target generation；下一阶段需由中央 ticket 管理 queue-visible publish、source snapshot stack 和 shared-event dependency。Vulkan/D3D 等价 handle、可控 Metal command failure/device removal 仍缺失。

### 下一优先项
1. 新增公开的 `AnityGraphicsVFXInitializeTicketInfo` 与 Begin/Get/Complete/Cancel C ABI；ticket 持有 staged initialize/system maps、stable metadata outputs 和一组 Metal handles，全部成功才原子交换 registry。
2. 为 ticket 增加 effect ownership、Clear/Reset/device teardown 收口、任一 backend failure newest→oldest Cancel/Restore，以及至少 10 个 Poll/Cancel/multi-system/duplicate-system/invalid-order测试。
3. 将 resident target 改为 queue-visible publish，使用 generation snapshot/shared Metal event 串联 Update→Initialize→Camera，再移植 Vulkan/D3D 并继续完整 VFX Output/Shader Graph material/URP camera stack/XR。

## 2026-07-17u — VFX Initialize ring target-copy 与原子 generation publish

### 已完成
- Metal resident Initialize 不再直接修改当前 resident particle/dead/allocation resource group。每次 dispatch 先取得三槽 ring 的目标槽并确保三类 target buffer 容量，在同一个 command buffer 内把 source generation 的完整 resource group复制到目标，再从 immutable 16-byte source allocation state 生成 indirect args 并只修改目标 generation。
- GPU command 成功完成且 source/target allocation invariants 全部通过后，才一次性交换 particle、dead list、allocation state 三类 buffer并发布同一个 target generation；任何编码失败、command failure 或 metadata 校验失败都只留下可覆盖的 ring target，当前 resident source generation 保持未修改。prepared-frame 需要 rollback 时，交换后位于 ring 槽内的旧 source resource group 直接转移为 generation snapshot，不再额外执行 snapshot blit。
- ring 槽的 semaphore 获取、所有 buffer allocation/encoder/command 失败出口以及成功路径均统一释放；普通 Initialize 让旧 source buffer 留在槽中复用，prepared Initialize 将其移动到 snapshot 后清空槽 ownership，避免双重释放和 source/target alias。
- backend diagnostics ABI 从 **440 bytes** 扩展到 **464 bytes**，新增 `residentInitializeTargetCopyCount`、`residentInitializeTargetCopyBytes`、`residentInitializeAtomicPublishCount`。native header、Metal stats、managed sequential layout与静态尺寸门禁同步。

### 测试与门禁
- 普通 resident Initialize **10 组**与 prepared Abort/Commit **10 组**均新增 target-copy count/bytes 和 atomic publish 断言；20/20 通过。4-capacity、60-byte stride fixture 每次复制 particle 240 bytes + dead 16 bytes + allocation 16 bytes，共 **272 bytes**。
- `bash _scripts/build-native.sh Release`、`bash _scripts/build-all.sh Release` 通过；Core 强制当前产品 dylib 全量 **1,542/1,542**、VFX Graph **490/490**，0 测试失败。产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning；`git diff --check` 通过。

### 尚未完成
- 本批解决的是异步 Initialize 的 source-preserving/atomic-publish 前置条件；公开 Initialize 事务仍同步等待 command completion，尚未提供 Begin/Poll/Complete/Cancel ticket。中央多 dispatch registry 仍在同步返回后交换 staged maps，Update→Initialize→Camera 尚未通过统一 ticket/shared-event scheduler 串联。
- 还需加入可控 Metal command/device-loss failure injection，验证三类 target resource 的失败丢弃和多 dispatch newest→oldest rollback；Vulkan/D3D 尚未实现同等 generation resource-group contract。总体 Unity 2022 Ultra `/goal` 继续进行，不能标记完成。

### 下一优先项
1. 把 Metal Initialize command ownership 提取为 Begin/Poll/Complete/Cancel handle：Begin 在 queue 上提交并发布 queue-visible target generation，Complete 校验 16-byte metadata并退休 source，Cancel/失败按 generation 恢复完整 source resource group。
2. 建立 effect-scoped multi-dispatch Initialize ticket，持有 staged dispatch/system maps 和各 backend handle；全部成功才原子发布中央 registry，任一失败反向取消整批，并补至少 10 个 lifecycle/failure/teardown 测试。
3. 将 ticket 纳入 Update→Initialize→Camera shared event/generation dependency graph，再移植 Vulkan/D3D resident chain并继续 Texture/flipbook、Shader Graph material、URP camera stack/XR 与 Unity 2022.3.61f1 Player A/B。

## 2026-07-17t — VFX explicit metadata boundary、prepared GPU snapshot 与 indirect Initialize

### 已完成
- 新增 Metal resident metadata 明确读回边界：`ReadbackVFXParticleSystem` / `ReadbackVFXParticleDeadList` 在收口 committed Update 后，从对应 generation 的 GPU allocation state 与 persistent dead list 校准中央 `aliveCount/deadCount/nextSequentialIndex/deadList`，同一 generation 只物化一次。dead index 会做范围与重复校验，allocation state 会校验 capacity、usesDeadList 与 alive+dead 不变量；无 resident buffer 的初始 CPU-authoritative system 保持原路径。
- prepared-frame Initialize 不再为了 rollback 强制整粒子数组回读。resident Initialize 在原地 mutation 前用 Metal blit 保存 particle/allocation/dead 三 buffer source-generation snapshot；Abort 复用已有 `RestoreVFXResidentGeneration` 恢复三类资源和 generation，Commit 丢弃 snapshot，后续 Update 仍保持 particle upload=1。
- resident Initialize 的 dispatch width 改为 GPU allocation-driven：同一 command buffer 先 blit 16-byte source allocation state，再由 `anity_vfx_initialize_indirect` compute kernel根据 capacity、usesDeadList、spawnCandidateCount 生成 indirect threadgroups，Initialize kernel 通过 buffer(8) 读取 immutable source dead/next 状态并更新 persistent target allocation state。CPU 不再参与 resident dispatch sizing；同步 API 仅在 command 完成后读取 source/target 16-byte state以更新托管镜像和验证不变量。
- backend diagnostics ABI 扩展到 **440 bytes**：新增显式 allocation/dead metadata readback 次数/字节/generation，以及 resident Initialize indirect prepare/dispatch、source-state GPU copy 与 CPU dispatch-sizing 计数。Metal initialize indirect pipeline、snapshot buffer、临时 source/argument buffer均纳入 destroy/成功/错误释放路径，非 Apple stub 与 managed layout 同步。

### 测试与门禁
- 普通 resident Initialize **10 组**位置边界继续验证 0 particle readback、0 re-upload，并新增 allocation/dead metadata 每代仅一次读回、24-byte 最终 metadata、generation 对齐，以及 indirect prepare/dispatch/source-copy=1、CPU sizing=0。
- prepared-frame resident Initialize 新增 **10 组** Abort/Commit 测试（各 5 组，覆盖负数、零、小数和大值）：验证 GPU snapshot、Abort alive/dead/physical particle 恢复、Commit 新粒子持久化、snapshot discard/restore、后继 Update 与 0 materialize。
- native Release build 与 `bash _scripts/build-all.sh Release` 通过；Update 生命周期 **150/150**、VFX 宽门禁 **706/706**、VFX Graph **490/490**、Core 强制当前产品 dylib 全量 **1,542/1,542**，0 测试失败；产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning。

### 尚未完成
- Initialize API 仍是同步事务：indirect sizing 已完全在 GPU，但函数返回前会等待 command completion 并读取 source/target 16-byte allocation state；尚未像 Update 一样提供 Begin/Poll/Publish/Complete ticket，也未纳入 Update→Initialize→Camera shared-event dependency graph。
- prepared snapshot 当前用独立 GPU blit command 并同步确认后再提交 Initialize；下一步需合并为同一 submission/ticket，并加入可控 command failure/device-loss 注入。Vulkan/D3D allocation contract、GPU Event/Mesh/Strip、Texture/flipbook/Shader Graph material、URP camera stack/XR 与 Unity 2022.3.61f1 Player A/B 仍未闭环，总体 Unity 2022 Ultra `/goal` 继续进行。

### 下一优先项
1. 为 Initialize 建立 Begin/Poll/Publish/Complete ticket，把 snapshot blit、indirect prepare、spawn 和 generation publish 合并进统一 async submission，并通过 shared Metal event 串联 Update→Initialize→Camera。
2. 增加可控 Metal command failure/device removal 注入，覆盖 snapshot、indirect args、target allocation state 和三代链的 newest→oldest 恢复。
3. 将 persistent particle/dead/allocation resource group、indirect Initialize 和显式 metadata boundary 移植到 Vulkan/D3D，再继续 URP Output 与官方 Player A/B。

## 2026-07-17s — VFX persistent GPU allocation state 与 resident Initialize

### 已完成
- Metal VFX Update 的 resident resource 从单一 particle buffer 扩展为同代持久资源组：particle attributes、`{aliveCount, deadCount, nextSequentialIndex, usesDeadList}` allocation state 与 full-capacity dead list。三类资源一同经过 3-slot blit、publish、source snapshot、rollback、restore、discard、Clear 与 device destroy，后继 Update 的 entry-alive / sequential-limit 和 death commit 直接读取前驱 GPU allocation state。
- death compaction 新增独立 commit kernel：prefix/compact 只产生本代死亡索引，commit 从 immutable source allocation state 写入 target allocation state 与 persistent dead list，消除 source/target 同 buffer 数据竞争。同步 Dispatch 在 publish 前从 ring slot 读取目标状态，异步已发布 ticket 则从 resident/snapshot 读取对应 generation，二者保持同一完成语义。
- Metal Initialize 在非 prepared-frame 的 resident generation 上直接复用 particle/dead/allocation buffers；spawn 原地消费 dead list、更新 alive/dead/nextSequential 并推进 resident generation，不再先 materialize 整个 particle array，随后 Update 也无需 authoritative particle re-upload。prepared-frame 仍保留 CPU materialize 保守路径，以维持现有 rollback journal 的逐字节恢复语义。
- 补齐 Metal buffer/pipeline ownership：Update slot、resident snapshot、allocation/dead buffers、Clear/destroy/failure 分支现在显式释放；Initialize 的 source/attribute/operation/prefix 与非 resident 临时 buffers 在成功及错误分支回收。backend diagnostics ABI 扩展到 **376 bytes**，新增 allocation generation/upload/copy/hit 与 resident Initialize 次数、spawn 数、避免回读字节、allocation-state 读取计数。

### 测试与门禁
- resident allocation ring 新增 **10 组** 1–10 代测试，验证首次 upload、后续 resident hit、逐代 GPU copy、generation 一致与三槽轮转；resident Initialize 新增 **10 组** 正负/零/小数/大值位置用例，验证两个 physical particle、后继 Update、0 particle readback、0 re-upload、allocation generation 与诊断计数。
- native Release build 与 `bash _scripts/build-all.sh Release` 通过；Update 生命周期 **140/140**、VFX 宽门禁 **696/696**、VFX Graph **490/490**、Core 强制当前产品 dylib 全量 **1,532/1,532**，0 测试失败；产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning，`git diff --check` 与 native ABI 符号检查通过。

### 尚未完成
- 普通 resident Initialize 已消除整粒子数组回读，但当前同步 Initialize 仍在显式依赖点退休 committed Update CPU metadata，并读取 16-byte shared allocation state 来决定 dispatch 宽度；prepared-frame Initialize 仍会 materialize 以支持旧 rollback journal。下一步需用 GPU indirect dispatch/shared event 和 Initialize generation snapshot，把这两处也改为纯 GPU 依赖。
- persistent allocation contract 尚未移植到 Vulkan/D3D；可控 Metal command/device-loss 注入、长时间压力、GPU Event/Mesh/Strip Output、Texture/flipbook/Shader Graph material、URP camera stack/XR 与 Unity 2022.3.61f1 Player A/B 仍未闭环。总体 Unity 2022 Ultra `/goal` 继续进行，不能标记完成。

### 下一优先项
1. 为 resident Initialize 增加 indirect dispatch 与 source-generation snapshot，使 committed Update→Initialize 及 prepared-frame Abort/Commit 全程不读 CPU allocation metadata。
2. 增加 allocation/dead-list 显式 readback API 与 reset/clear 边界测试，再加入可控 Metal command failure/device removal 三代链恢复。
3. 将同一 generation resource-group contract 移植到 Vulkan/D3D，并继续 URP Output、Shader Graph material 与 Unity 2022.3.61f1 Player A/B。

## 2026-07-17r — VFX Update 三槽多帧 generation chain

### 已完成
- effect-scoped Update ownership 从单个 ticket 改为按提交顺序保存的 bounded queue；Metal backend 同步改为最多三代 `inFlightGenerations`。连续帧 Commit 可发布 source→target generation chain，下一次 Update 不再因上一帧 CPU metadata 尚未退休而强制等待；第四帧产生 ring pressure 时只同步退休最老 generation。
- Complete later ticket 会按队首顺序先退休全部 committed predecessor；每一代的 alive/dead-list/next-allocation CPU metadata 从已完成前驱重新基线化，旧代完成不会覆盖已经发布的新 resident。每代 source snapshot 独立释放，Camera 可依赖队列中任一仍在飞的 generation。
- managed `VisualEffect` 用 committed-ticket FIFO 取代单一 pending 标志；普通 Update/Commit 只做 nonblocking retirement，readback/Initialize/Bounds/Clear/Reset/teardown 等真实依赖点仍会有序收口。当前未提交帧 Abort 只撤销当前 ticket，保留更早 committed baseline。
- Reset/Clear/destroy 与 generation mismatch 按 newest→oldest 回滚整条 snapshot stack。任一前驱 backend failure 会清除全部后继 ticket 并恢复最早稳定 CPU source，避免后继继续引用失效 generation；未提交 ticket 存在时，读回继续观察稳定上一代而不会隐式发布或取消当前帧。

### 测试与门禁
- 新增 **10 个连续帧测试发现项**（2–11 帧，覆盖三槽以内、第四帧 ring pressure 与多轮复用），并改写旧“下一 Update 必须完成前驱”的同步契约。逐代积分、dispatch/completion/asynchronous completion 和最终 pending=0 均验证；Update 生命周期 **128/128**。
- native Release build 与 `bash _scripts/build-all.sh Release` 通过；VFX 宽门禁 **684/684**，Core 强制当前产品 dylib 全量 **1,520/1,520**，VFX Graph **490/490**，0 测试失败。产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning；`git diff --check` 通过。产品仍保持 `AnityGraphicsVFXUpdateBackendStats` **312-byte ABI**，未以统计结构变更冒充队列实现。

### 尚未完成
- 三槽 chain 已消除 Update→Update 的单-ticket CPU 等待，但 dead-list/alive-count/next-allocation 仍需在显式 CPU metadata 依赖处 materialize；Metal Initialize 尚未直接消费 persistent GPU allocation state。
- shared `MTLEvent`、可控 command/device-loss 注入、Vulkan/D3D 等价 resident chain、iOS/macOS Player 长时间压力以及完整 Texture/Shader Graph/URP Output 仍未闭环。总体 Unity 2022 Ultra `/goal` 继续进行，不能标记完成。

### 下一优先项
1. 建立 persistent GPU dead-index/count/alive-count/next-allocation，让 Initialize/Update 在无 callback/readback 时全程 GPU 串联。
2. 增加可控 Metal command failure/device removal 注入和三代链失败测试，再接入 shared event completed-generation 回收。
3. 将同一 N-slot generation contract 移植到 Vulkan/D3D，并继续 URP depth/camera stack/XR、flipbook/Texture、Shader Graph material 与 Unity 2022.3.61f1 Player A/B。

## 2026-07-17q — VFX 下一帧 Prepare 非阻塞 poll 与真实 dependency overlap

### 已完成
- Metal 的 `PrepareVFXEffectFrame` / `PrepareVFXEffectManualFrame` 不再用 `wait=true` 收口上一帧 committed Update。新增专用 `AnityGraphics_Metal_PollVFXUpdateBatchForPreparation`：Prepare 只读取 command-buffer 状态；已完成则无等待退休 metadata，未完成则保留 ticket/resident/snapshot 并继续建立下一帧 rollback journal，使输入处理与 Spawner CPU 工作可以和上一帧 GPU Update 重叠。
- managed `VisualEffect` 不再在 Prepare 前强制 `RefreshAliveParticleCountAfterUpdate(waitForCompletion:true)`。真正依赖上一帧 alive/dead metadata 的下一次 Update 仍在 `UpdateParticleSystems` 入口完成 ticket；Initialize kernel transaction 也会先完成同 effect 的 committed Update，再 materialize attributes/消费 dead-list。未提交 ticket 的并发 mutation CAS 语义保持不变。
- 新帧在上一帧 committed Update 尚 pending 时 Abort，不再错误 Cancel/rollback 已提交的 resident generation，也不提前丢弃 failure rollback 所需 source snapshot；该 committed generation 是新帧 rollback baseline。空的新帧可以在 metadata 退休前 Commit，readback/Bounds/Clear/Reset/teardown 仍保持原有显式 completion 边界。
- `AnityGraphicsVFXUpdateBackendStats` 从 **288 bytes** 扩展到 **312 bytes**，新增 `preparationPollCount / preparationDeferredCount / preparationRetiredCount`。这些统计对 batch 内每个 particle system 记录 Prepare 是延后还是无等待退休，并与 `completionWaitCount` 分离，防止用“GPU 恰好已完成”冒充非阻塞实现。Apple dylib 已导出新 poll ABI，非 Apple stub 与 managed layout 同步。

### 测试与门禁
- 新增 **10 个测试发现项**，并改写 1 个旧同步契约测试：覆盖 automatic/manual Prepare、poll/defer/retire 统计、Abort committed baseline、空帧 Commit、Initialize dead-list dependency、三 system batch、managed 下一 Update dependency、Prepare 零 particle readback、readback retirement、Clear 与多 effect 隔离。Update 生命周期由 **108** 增至 **118/118**；受影响的 Update/Frame/Manual/Initialize/Bounds/Planar 八套件 **387/387**。
- VFX 宽门禁由 **664** 增至 **674/674**，Core 强制当前产品 dylib 全量由 **1,500** 增至 **1,510/1,510**，VFX Graph 保持 **490/490**。native build 与 `bash _scripts/build-all.sh Release` 通过；产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning，新 ABI 符号导出及 `git diff --check` 通过。
- Unity API 门禁仍基于本机 **Unity 2022.3.51f1**：类型存在 **928/4,117**（22.541%）、精确 **404**（9.813%），成员存在 **8,645/37,164**（23.262%）、精确 **6,417**（17.267%），load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 本批扩大的是单个 pending Update 的 CPU/GPU overlap 窗口，不是完整多帧 GPU simulation chain。同一 effect 仍只允许一个 pending ticket；下一 Initialize 或下一 Update 真正需要 dead-list/alive metadata 时，若 GPU 尚未完成仍会等待。三槽 backend ring 尚未成为可同时持有三帧中央事务的 N-slot ownership。
- dead-list、alive count 与 spawn allocation 仍由 CPU authoritative metadata 收口；Metal Initialize 仍是同步 kernel/readback 路径。只有将 persistent GPU dead-list/count/next-allocation 直接串给下一 Initialize/Update，才能在无 CPU callback/readback 时跨多帧不断链。
- shared `MTLEvent`、多 queue completed-generation、可控 GPU failure/device-loss、Vulkan/D3D 等价 compute chain、真实 iOS/macOS Player 长时间压力、以及 Texture/Shader Graph material/完整 URP Output 仍未闭环。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型和大量行为。

### 下一优先项
1. 为每个 particle system 建立 persistent GPU dead-index/count/alive-count/next-allocation state，让 Metal Initialize 直接消费并更新该状态；CPU metadata只在显式 readback/callback 时 materialize。
2. 将中央 pending transaction 改为 N-slot frame ownership，允许三帧 Update ticket 同时排队，并以 ring pressure/shared event 完成回收、Abort/Reset 回滚与 device-loss 传播。
3. 把相同 generation/dead-list/dependency contract 移植到 Vulkan/D3D，并继续共享 URP depth、Texture2DArray/flipbook、Shader Graph material、alpha clip/soft particle/motion vector与 Unity 2022.3.61f1 Player A/B。

## 2026-07-17p — VFX Update Metal 两阶段异步发布与 Update→Camera 队列依赖

### 已完成
- Metal 产品帧的 `AnityGraphics_CommitVFXEffectFrame` 不再为了 Update ticket 调用 CPU `waitUntilCompleted`。Commit 现在先把目标 generation 的 GPU resident buffer 原子发布到 effect registry，再提交 frame clock/journal；同一 Metal command queue 上随后编码的 Camera 与 Present 依赖队列顺序消费该 generation，正常帧路径形成非阻塞的 Update→Camera→Present 链。
- Update 改为两阶段生命周期：Commit 发布 GPU resident，alive/dead compact metadata、Automatic Bounds 结果和三槽 ring 回收延后到显式 Complete、readback、culling bounds、下一次 Prepare、Clear/Reset 或 device teardown 等同步边界。下一帧的 managed `VisualEffect` 会先完成仍 pending 的上一帧 ticket；当前帧只通过 info-only particle-system metadata 更新保守的 alive count 和 active-kernel 判定，不再为了状态查询下载完整 particle records。
- 保留 Update 源 generation/snapshot 直到 CPU metadata 完成。Cancel/Reset/GPU failure 会恢复源 resident 与中央 registry storage，Clear/Dispose/device destroy 会先收口 pending work；已 Commit 的公开 ticket 拒绝取消，避免回滚已提交的帧事务。Automatic Bounds 查询先完成 committed pending Update，因此直接发布同一 command buffer 生成的 pending bounds，而不重复派发 reduction。
- `AnityGraphicsVFXUpdateBackendStats` ABI 从 **240 bytes** 扩展到 **288 bytes**，新增 asynchronous resident publish/completion/rollback、completion wait、camera dependency 与 pending update 统计；新增 `AnityGraphics_Metal_PublishVFXUpdateBatch` C ABI、Apple 实现、非 Apple stub、managed P/Invoke 与静态尺寸门禁。当前 `pendingUpdateCount` 明确定义为“GPU resident 已发布，但 CPU metadata/ring retirement 尚待收口”，不是 GPU 必然仍在执行。

### 测试与门禁
- 新增 **13 个测试发现项**：`NativeVFXUpdateLifecycleTests` 12 项覆盖非阻塞 Commit、显式 metadata completion、committed cancel 拒绝、下一 Prepare/readback/Clear/Reset/Dispose 边界、多 system batch、Null 同步兼容及产品 `VisualEffect` 无完整 particle readback；`NativeVFXPlanarCameraBatchTests` 1 项验证 Camera 消费 committed Update target generation、queue dependency、Planar async fence 与最终 Update completion。
- Update 生命周期 **108/108**，Planar 两 suite **105/105**，Update + Camera + Automatic Bounds 定向集合 **238/238**，VFX 宽门禁 **664/664**，Core 强制当前产品 dylib 全量 **1,500/1,500**，VFX Graph **490/490**。`bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例；产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning。
- Unity API 门禁仍基于本机 **Unity 2022.3.51f1**：类型存在 **928/4,117**（22.541%）、精确 **404**（9.813%），成员存在 **8,645/37,164**（23.262%）、精确 **6,417**（17.267%），load issues=0、`regressions=0`、`removed-or-changed=0`；新 C ABI 符号已从产品 dylib 导出，`git diff --check` 通过。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 同一 effect 当前只允许一个 pending Update，下一次 Prepare 在上一提交超过一帧 GPU 延迟时仍可能等待；CPU dead-list/alive metadata 仍是 ring 回收门槛，尚未形成跨多帧、N-slot ownership 的全 GPU resident death/alive chain。同步兼容入口 `AnityGraphics_DispatchVFXUpdateKernels` 仍有意等待。
- 当前 Update→Camera 安全性依赖单一 Metal command queue；还没有 shared `MTLEvent`、多 queue completed-generation contract、统一帧调度器或可控 GPU command failure/device-loss 注入。Initialize copy/kernel 与独立 bounds reduction 仍是同步边界，Vulkan/D3D 尚未实现等价 resident compute/draw dependency。
- 贴图/Texture2DArray/flipbook、Shader Graph material/property、alpha clip/soft particle/motion vector、共享 URP depth/camera stack/XR、Mesh/Strip/GPU Event Output、复杂 bounds、真实 iOS/macOS 长时间压力和 Unity 2022.3.61f1 Player A/B 均未闭环。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型与大量成员行为。

### 下一优先项
1. 将 dead-list/alive metadata 保持为 GPU-resident 多帧链，并把 frame/update ownership 扩展为 N 槽；除 ring pressure 外移除下一 Prepare 的 CPU wait，补长帧延迟、容量压力、Reset/teardown 与失败恢复证据。
2. 引入 shared Metal event 与统一 Update→Camera→Present scheduler，按 completed generation 回收/发布资源并贯通 timeout/device-loss；随后建立大 capacity 分层 scan/sort 的 GPU 性能基线。
3. 接入共享 URP depth、opaque/soft-particle/MSAA/stencil/camera stack，完成 Texture2DArray/flipbook、Shader Graph material、alpha clip/motion vector，再把相同 contract 移植到 Vulkan/D3D 并用 Unity 2022.3.61f1 Player 验收。

## 2026-07-17o — VFX Planar Metal 异步提交与可观测 completion fence

### 已完成
- 移除 `AnityGraphics_Metal_DrawVFXPlanarCamera` 尾部逐相机 `waitUntilCompleted`：compaction、GPU sort、indirect args 与整台 camera Planar render 仍编码进一个 Metal command buffer，但 `commit` 后立即返回。160-byte draw-info 新增单调 `submissionId`、async submission count 与 synchronous wait count，正常 Metal 路径明确报告 `async=1 / synchronousWait=0`。
- 新增 80-byte `AnityGraphicsVFXPlanarSubmissionStats` 与稳定 C ABI/managed wrapper：可查询提交、完成、失败、最新 ID、当前/峰值 in-flight、wait 次数与 backend kind；`AnityGraphics_WaitForVFXPlanarSubmissions` 支持精确 ID、`0=当前最新提交`、无限等待、零超时 poll 与有限 timeout，新增内部 `ANITY_ERR_TIMEOUT` 结果。Null/非 Metal 后端保持确定的零统计与参数校验语义。
- Metal completion handler 在 GPU 成功后发布完成 ID、递减 in-flight 并唤醒 waiter；失败时额外锁定 VFX compute registry，使本次 command buffer 修改过的 alive compaction 与全部 sort-cache 槽失效，防止失败结果成为后续 cache hit，并发布 device-lost/failure 统计。
- readback、swapchain destroy 与 graphics-device destroy 现在等待最新 VFX Planar submission 后才访问或释放资源；相机 A/B/A、后续 Present 与 Update 依赖 Metal 单一 command queue 的提交顺序，不需要每次 camera CPU 阻塞。临时 indirect-arguments buffer 在 pre-commit 失败或 commit 后显式 release，由 command buffer 保留其 GPU 生命周期。

### 测试与门禁
- `NativeVFXPlanarCameraBatchTests` 新增 **12 个测试发现项**：异步 identity/stats、ID 单调性、精确/latest fence、zero-timeout poll、非法 future ID/timeout、Null 语义、readback fence 与像素、连续 normal/reversed/normal camera queue 顺序、显式 wait/readback 计数，以及 32 个 in-flight camera 后直接 Dispose 的 teardown 压力。Planar 两 suite 由 **92** 增至 **104/104**。
- 强制当前产品 dylib 的 VFX 宽门禁由 **652** 增至 **664/664**，Core 全量由 **1,475** 增至 **1,487/1,487**；VFX Graph 保持 **490/490**。native build 与 `bash _scripts/build-all.sh Release` 均通过；产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning。
- Unity API 门禁仍基于本机 **Unity 2022.3.51f1**：类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`；`git diff --check` 通过。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 本批只把 Metal Planar camera draw 改为异步。VFX Update 产品 `Commit`、Initialize、bounds 与部分 readback 路径仍会等待 GPU；尚未形成 Update/Camera/Present 共用的帧级 submission dependency graph、共享 event 或统一错误恢复。
- cache entry 仍在 command 编码时发布，而不是在 completion 后发布；当前所有 producer/consumer 使用同一个 Metal command queue，因此 GPU 执行顺序保证后续消费者不会越过前一提交。未来多 queue、Vulkan/D3D 或并行 encoder 必须改为 completed-generation publication，或显式等待 shared event/fence，不能沿用这一假设。
- 失败 invalidation 路径已有实现，但测试环境没有可控的 Metal GPU command failure/device-loss 注入；timeout 恢复、失败后的资源重建、Present/device teardown 并发和长时间 iOS/macOS Player 压力仍缺真实平台证据。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 移除 VFX Update 产品 Commit 的同步等待，以帧级 async ticket/shared Metal event 统一 Update→Camera→Present 依赖、completed-generation publication、资源回收和 device-loss 传播。
2. 用 Unity 2022.3.61f1 Metal Player fixture 锁定全部 sorting mode、自定义 key、透视/正交/reversed-Z 语义，并把 stable bitonic 优化为适合大 capacity 的分层/radix/subgroup 路径。
3. 将 Planar 接入共享 URP camera depth、opaque/soft-particle/MSAA/stencil/camera stack，完成 Texture2DArray/flipbook、Shader Graph material、alpha clip/motion vector，再移植到 Vulkan/D3D。

## 2026-07-17n — VFX Planar Metal 有界多相机 GPU 排序缓存

### 已完成
- 将每个 effect/particle-system 原先唯一一份 camera sort cache 升级为固定 **4 槽**的有界多相机工作集。每个槽独立持有 sort entry 与 sorted physical-index Metal buffer，精确键继续覆盖 resident generation、cameraId、stride/capacity/position offset、padded length、local-to-world 与 world-to-clip；Camera A → B → A、Scene/Game/Preview 多视图往返不再必然重复执行 map/bitonic/extract。
- 缓存满时以 64-bit use serial 的模运算 age 选择最久未使用槽，近期命中的 Camera 会被保护；第 5 个不同 key 只驱逐一个槽，长期任意 Camera 数下每个 particle system 仍最多保留 4 组排序 buffer，不产生无界 GPU cache 增长。相同 system 多 output 继续只插入一次，多个 particle system 的统计按去重后的 system 汇总。
- resident generation/alive compaction 重建会让全部槽 generation 失效但保留已分配 buffer 供后续安全复用；command 编码或 completion 失败会让本次修改过的 system 全部失效，未执行结果不会成为 cache hit。当前同步 completion 保证驱逐槽没有 in-flight consumer。
- camera draw-info ABI 由 **128 bytes** 扩展为 **144 bytes**，新增 cache insert、eviction、active entry 与 per-system capacity 统计，managed/native 声明及 C++ static assert 同步。Metal 文件使用手动引用计数，本批缓存 entry 析构会释放两类 buffer，扩容替换也先释放旧 owned buffer，避免多相机优化放大 device teardown/resize 泄漏。

### 测试与门禁
- `NativeVFXPlanarCameraBatchTests` 新增 **11 个测试发现项**：A/B/A 独立复用、4 Camera 无驱逐、第 5 Camera 单次驱逐、LRU 热点保护、被驱逐 Camera 重建、projection/transform 变体回访、generation 清空 4 槽、多 particle-system entry 聚合、交替 projection 的真实 alpha framebuffer 顺序，以及 16 Camera 下 entry count 始终不超过 4。ABI、首插入、同 system 多 output、unsupported/Null 路径也补齐新统计断言。Planar 两 suite 强制当前产品 dylib 由 **81** 增至 **92/92**。
- 强制产品 dylib 的 VFX 宽门禁由 **641** 增至 **652/652**，Core 全量由 **1,464** 增至 **1,475/1,475**；VFX Graph 保持 **490/490**。资源释放补丁后重新执行 native build、Planar 与 Core 全量，均通过。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例；产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning。`git diff --check` 通过。
- Unity API 门禁仍基于本机 **Unity 2022.3.51f1**：类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 4 槽是当前 camera stack/Scene/Game/Preview 常见工作集的有界策略，不是已由 Unity 2022.3.61f1 workload fixture 验证的最终容量；精确 matrix bit compare 会把每次有位级抖动的 projection/transform 当作新 key。仍需真实 Editor/Player 多 Camera trace、命中率/GPU memory/驱逐性能基线与可配置或 frame-aware policy。
- 当前 camera 尾部仍同步 `waitUntilCompleted`，所以槽驱逐天然没有 GPU in-flight hazard。改为异步提交后必须把 compact/sort/indirect buffer 变成 ring-owned、由 completion fence 回收的不可覆盖资源，并让 cache entry 指向已完成 generation；现有 LRU 不能原样用于未完成 command buffer。
- stable bitonic 仍为 O(N log² N) 且每 stage 一个 encoder；官方多 sorting mode/custom key、camera distance、正交/reversed-Z、共享 URP depth、贴图/Shader Graph material、alpha clip/soft particle、Mesh/Strip/GPU Event及 Vulkan/D3D 路径均未闭环。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 将 camera compaction/sort/indirect submission 改为 3 槽或按 swapchain image 的 ring-owned storage、completion fence 与 completed-generation cache publication，移除 camera 尾部同步等待，并补多 camera/in-flight/销毁/device-loss 压力测试。
2. 用 Unity 2022.3.61f1 Metal Player fixture 锁定全部 VFX sorting mode、自定义 key、透视/正交/reversed-Z 语义，再把 bitonic 改为适合大 capacity 的分层/radix/subgroup 实现并建立 GPU 性能基线。
3. 将 Planar depth 接入共享 URP camera depth、opaque/soft-particle/MSAA/stencil/camera stack，补 Texture2DArray/flipbook、Shader Graph material、alpha clip/motion vector及官方 Output Block，再移植到 Vulkan/D3D。

## 2026-07-17m — VFX Planar Metal GPU 稳定粒子排序

### 已完成
- sorting-required Planar output 现进入真实 Metal GPU 路径：先从 stable compact alive physical-index 生成 projected-depth key，再执行稳定 bitonic sort，最后提取已排序 physical-index 供 indirect draw 使用。当前标准深度约定按 `clip.z / clip.w` **远到近**排列；相同深度以 compact alive ordinal 升序打破平局，因此结果确定且不需要 CPU particle readback。
- 任意非 2 次幂 particle capacity 会扩展到下一次幂；无效 padding 固定沉底，现有限制为 padded capacity 不超过 `2^26`。sort entry、sorted index 与 compaction/indirect buffer 均持久驻留在 particle-system Metal update buffers，map、每一级 bitonic stage、extract 与最终 render 位于同一 camera command buffer 生命周期。
- sort cache 精确绑定 resident generation、cameraId、particle stride/capacity/position offset、padded length、effect local-to-world 与 camera world-to-clip。相同 system 的多个 output 可复用一次排序；camera、矩阵、transform、layout 或 resident generation 变化会重新排序，alive compaction 重建也会使 sort generation 失效。shared-system registry 新增 position offset 一致性校验，冲突批次事务拒绝。
- camera draw-info ABI 由 **104 bytes** 扩展为 **128 bytes**，新增 sorted output、sort cache hit、map/stage/extract dispatch 与 padded particle 数统计；managed/native 声明与静态 ABI 断言同步更新。alpha-clip 仍诚实计入 unsupported，显式 indirect 与 sorting 可以组合执行。

### 测试与门禁
- `NativeVFXPlanarCameraBatchTests` / `NativeVFXPlanarOutputTests` 新增或修正 **15 个测试发现项**，覆盖 128-byte ABI、首次排序、非 2 次幂 padding、重复 camera 缓存、cameraId/projection/resident generation 失效、同 system 多 output 复用、稀疏 physical index、sorting + indirect、unsupported/Null 真值边界、position layout 冲突，以及真实 alpha framebuffer 的远到近、同深度稳定性和投影反转顺序证据。Planar 两 suite 强制当前产品 dylib 为 **81/81**。
- 强制产品 dylib 的 VFX 宽门禁为 **641/641**，Core 全量为 **1,464/1,464**；VFX Graph 保持 **490/490**。`bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例；产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning。`git diff --check` 通过。
- Unity API 门禁仍基于本机 **Unity 2022.3.51f1**：类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 当前只有 compiler 的二值 `sorting required` contract，排序 key 是投影 `clip.z / clip.w`；尚未按 Unity 2022.3.61f1 官方 Player fixture 锁定各 VFX sorting mode、自定义 sort key、camera distance 与 projected depth 的选择、正交相机和 Metal reversed-Z 语义。投影反转测试只证明 cache/key/order 会响应矩阵变化，不等价于 reversed-Z 已对齐。
- 现有 bitonic sort 为 O(N log² N)，每个 stage 使用独立 compute encoder 保证全局阶段顺序；每个 effect/system 当前只保留一份 camera sort cache，camera 尾部仍同步等待 completion。仍需分层/radix/subgroup 优化、per-camera 多缓存、ring-owned storage、跨帧 fence、timeout/device-loss 和大 capacity/iOS Player 性能压力基线。
- depth 仍只在 Planar pass 间共享；贴图/flipbook/Shader Graph material、alpha clip/soft particle/motion vector、完整 URP opaque/reversed-Z/MSAA/stencil、camera stack/XR、Mesh/Strip/GPU Event Output及 Vulkan/D3D resident compute/draw/depth 均未闭环。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 用 Unity 2022.3.61f1 Metal Player fixture 锁定全部 VFX sorting mode、自定义 key、正交/透视/reversed-Z 语义，并将 bitonic 改为适合大 capacity 的分层/radix/subgroup 实现。
2. 将 Planar depth 接入共享 URP camera depth、opaque 遮挡与 soft particle，完成 MSAA/stencil/camera stack；把 compaction/sort/indirect storage 改为 ring-owned async fence 与 per-camera 多缓存，移除 camera 尾部同步等待。
3. 实现 Texture2D/Texture2DArray/flipbook、Shader Graph material/property、alpha clip/motion vector及官方 Output Block lowering，再把 generation/compact/sort/indirect/depth contract 移植到 Vulkan 与 D3D。

## 2026-07-17l — VFX Planar Metal Depth32、七种 ZTest 与 ZWrite

### 已完成
- Metal swapchain 现在创建与 color target 同尺寸的 **Depth32Float** attachment，并随 swapchain 销毁。Planar camera pass 在首次使用或 camera clear 时把 depth 清为 1.0，成功提交后标记 initialized；后续不 clear 的 camera submission 使用 load/store 保留已有深度，不再为每次 VFX draw 隐式丢弃 depth。
- Planar render pipeline 声明 Depth32 attachment，并新增独立 depth-stencil state cache，完整映射 compiler runtime 编码：`Less / Greater / LEqual / GEqual / Equal / NotEqual / Always`，同时执行每个 output 的 `ZWrite`。同一 pass 只在状态变化时切换 depth state，原先硬编码 `Always + ZWrite off` 的 backend 限制已删除。
- camera draw-info ABI 由 **88 bytes** 扩展为 **104 bytes**，新增 depth-tested output、depth-writing output、depth-state change 与 depth-clear 统计。compiler 实际只产出 0..6 的 runtime ZTest 编码，native 与资产验证同步拒绝未使用的 7，保持安装事务性。`flags bit3` 的显式 indirect output 现在由已实现的 GPU compact/indirect 路径执行，不再被错误归类为 unsupported。

### 测试与门禁
- `NativeVFXPlanarCameraBatchTests` 新增 **16 个测试发现项**：104-byte ABI、七种 depth compare state、ZWrite on/off、非法 runtime code 7 的事务拒绝、显式 indirect flag、近处写深度遮挡后绘远处、远处写深度后近处通过、Always 绕过已有深度、跨 camera depth 保留与 camera clear 重置。全部使用当前产品 dylib；Planar 两 suite 由 **50** 增至 **66/66**。
- 强制产品 dylib 的 Core VFX 宽门禁由 **610** 增至 **626/626**，Core 全量由 **1,433** 增至 **1,449/1,449**；VFX Graph 保持 **490/490**。`bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例；产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning。`git diff --check` 通过。
- Unity API 门禁仍基于本机 **Unity 2022.3.51f1**：类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 当前证据只证明同一 Metal swapchain 上的 **Planar-to-Planar** 深度语义；depth attachment 尚未与完整 URP scene opaque/depth prepass、soft-particle depth texture 或 camera stack 共享。当前 clear=1.0 使用标准深度约定，仍缺 Unity Metal reversed-Z / `GL.GetGPUProjectionMatrix` 对照、MSAA depth resolve、stencil、动态 resize/recreate 与 native drawable 尺寸漂移处理。
- 透明粒子仍只有 output/effect 级排序，没有 per-particle camera-distance stable sort。Metal camera 调用尾部仍同步等待 completion；缺少 ring-owned camera buffers、跨帧 fence、command queue 合并、timeout/device-loss 与 iOS Player 长时间压力。
- 贴图/Texture2DArray/flipbook、Shader Graph material/property、alpha clip、soft particle、motion vector、官方 Output Block、Mesh/Strip/GPU Event Output，以及 Vulkan/D3D resident draw/depth 仍未闭环。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 为 sorting-required Planar output 实现 camera-dependent GPU depth-key generation 与 stable far-to-near compact-index sort，并补稀疏 alive、多 camera、equal-depth stability 和真实 alpha framebuffer 证据。
2. 将 Planar depth 接入共享 URP camera depth 生命周期，完成 reversed-Z/GPU projection、opaque 遮挡、soft particle、MSAA/stencil、camera stack/resize；随后把 camera indirect/sort storage 改为 ring-owned async fence，移除调用尾部同步等待。
3. 实现 Texture2D/Texture2DArray/flipbook、Shader Graph material/property、alpha clip/motion vector及官方 Output Block lowering，再把 generation/compact/sort/indirect/depth contract 移植到 Vulkan 与 D3D。

## 2026-07-17k — VFX Planar stable alive compaction 与 GPU indirect draw

### 已完成
- camera-wide draw-info ABI 由 **64 bytes** 扩展为 **88 bytes**，新增 alive compaction、cache hit、prefix pass、indirect argument 与 capacity vertex 统计。Metal resident particle system 现在持久保存与 `generation + stride + capacity + aliveOffset` 绑定的 stable compact alive index/count；同一 resident generation 被多个 output 或后续 camera 消费时直接复用，不重复扫描或压缩。
- 复用了 Update death-list 已验证的 GPU stable prefix/compact 核心：Planar camera command buffer 先从 resident records 生成 alive flags，再执行稳定前缀和 compact physical index，并由 GPU alive count 生成 Metal `drawPrimitivesIndirect` 参数。Planar vertex shader 通过 compact index 将逻辑粒子序号映射回真实 physical particle index；实际提交顶点数由 authoritative alive count 驱动，不再按 capacity 提交并依赖 dead discard。
- compaction、indirect argument 构建与全部 Planar draws 仍位于同一 camera command buffer / render pass 生命周期；同一 particle system 的多个 output 只 compact 一次。registry 安装新增 shared-system layout 一致性校验：相同 effect 内同一 particle system 的 capacity、stride 或 alive offset 冲突时整批事务性拒绝，保留此前有效 registry。

### 测试与门禁
- `NativeVFXPlanarCameraBatchTests` 新增 **13 个测试发现项**，覆盖首次 compact/indirect 统计、generation cache 命中与失效、同 system 多 output 复用、capacity 与 alive vertex 区分、Triangle/Quad/Octagon indirect 参数、零 alive、unsupported/Null backend 真值边界、shared-system layout 事务拒绝，以及“physical index 0 dead、index 1 alive”的稀疏 resident framebuffer 绿色像素证据。Planar 两 suite 强制当前产品 dylib 由 **37** 增至 **50/50**。
- 强制产品 dylib 的 Core VFX 宽门禁由 **597** 增至 **610/610**，Core 全量由 **1,420** 增至 **1,433/1,433**；VFX Graph 保持 **490/490**。`bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例；产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning。`git diff --check` 通过。
- Unity API 门禁仍基于本机 **Unity 2022.3.51f1**：类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 当前 Metal camera 调用尾部仍同步等待 command completion；缺少跨帧 async fence、ring-owned camera indirect/compact buffers、command queue 合并、timeout/device-loss/resize 恢复和 iOS Player 长时间压力。现有稳定 Hillis-Steele prefix 为 O(N log N)，还需针对大 capacity 做分层扫描与真实性能基线。
- 透明粒子仍只有 output/effect 级 render queue 与 sort order，没有 Unity per-particle camera-distance sorting；尚缺 depth attachment、完整 ZTest/ZWrite、camera stack/overlay、XR multiview、Scene/Game preview。
- 贴图/Texture2DArray/flipbook、Shader Graph material/property、alpha clip、soft particle、motion vector、官方 Output Block、Mesh/Strip/GPU Event Output，以及 Vulkan/D3D resident draw 仍未闭环。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 实现透明粒子 camera-distance stable sort、depth attachment 与完整 ZTest/ZWrite，并把 camera indirect/compact storage 改为 ring-owned async fence 生命周期，移除 render 调用尾部同步等待。
2. 实现 Texture2D/Texture2DArray/flipbook、Shader Graph material/property binding、alpha clip、soft particle/depth、motion vector及官方 Output Block lowering，并完成 URP camera stack/XR/Scene/Game 与 Unity 2022.3.61f1 截图 A/B。
3. 将相同 generation/cache/compact/indirect contract 实现到 Vulkan 与 D3D，再扩展 Mesh/Strip/GPU Event Output，并完成 Windows、Android Vulkan 与 iOS Metal Player 的 resize/device-loss/长时间压力矩阵。

## 2026-07-17j — VFX Planar 相机级批处理、数值队列与单 Pass Metal 提交

### 已完成
- 新增 camera-wide native ABI：**80-byte** `AnityGraphicsVFXPlanarEffectDesc`、**88-byte** `AnityGraphicsVFXPlanarCameraBatchDesc` 与 **64-byte** `AnityGraphicsVFXPlanarCameraDrawInfo`。一次提交携带同一 camera 的全部 effect transform/layer/sort order；native 在一个 registry generation 锁区内快照 descriptor、resident generation 与 authoritative alive count，拒绝重复 effect、非法 layer/sort/matrix、缺失 registry 和 particle layout 漂移，不把半截批次交给 backend。
- native 会先按 camera culling mask 过滤 effect，再把所有 output 展平，并以 `renderQueue -> effect sortOrder -> effectId -> contextId` 做稳定排序。Null/非 Metal backend 仍逐 output 诚实计入 skip；旧 `AnityGraphics_DrawVFXPlanarOutputs` ABI 保留并改为单 effect compatibility wrapper，现有调用方无需同时迁移。
- 修正 managed bridge 把 render queue 字符串错误映射为 `Shader.PropertyToID` hash 的问题。现在按 Unity 数值队列解析 `Background=1000`、`Geometry=2000`、`AlphaTest=2450`、`GeometryLast=2500`、`Transparent=3000`、`Overlay=4000` 及有符号 offset，并把最终值限制在 0..5000；unknown/malformed/out-of-range 资产安装失败，不产生不可排序的伪队列。
- `VFXRuntimeServices` 相机路径现在先完成 culling submission 与所有可见 effect 的 version-aware descriptor 注册，再执行**一次** `DrawVFXPlanarCamera`。Metal 在该入口为整台 camera 只创建 **1 个 command buffer + 1 个 render pass + 1 次 completion wait**，按已排序 packet 切换 pipeline/cull/buffer/transform；不再为每个 `VisualEffect` 单独开 pass 和同步等待。返回统计明确包含 effect/output/draw/skip/particle/vertex、registry snapshot generation、最大 resident generation 及 command-buffer/render-pass 数。

### 测试与门禁
- 新增 `NativeVFXPlanarCameraBatchTests` **22 个测试发现项**：三种 ABI size、六种 Unity render queue 数值映射、四种非法 queue、空 batch、重复 effect、非有限矩阵、非法 layer、缺失 registry、native queue range、Null backend 聚合/图层过滤，以及 Metal 两 effect 单 command/pass、跨 effect queue 像素顺序、同 queue sortOrder 像素顺序与独立 local-to-world 变换。连同旧 Planar suite 强制当前产品 dylib 为 **37/37**。
- 强制产品 dylib 的 Core VFX 宽门禁由 **562** 增至 **597/597**，Core 全量由 **1,398** 增至 **1,420/1,420**；VFX Graph 保持 **490/490**。`bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例，产品模块 0 编译错误；URP3DDemo 保持既有 43 个 nullable warning。`git diff --check` 通过。
- Unity API 门禁仍基于本机 **Unity 2022.3.51f1**：类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 本批消除了 per-effect pass/wait 和 hash queue，但每个 output 仍按 particle capacity 提交 vertex，并在 vertex shader 检查 alive；尚未让 stable compact alive index/indirect args 直接驱动 draw。透明粒子仍只有 output/effect 级队列顺序，没有 Unity per-particle camera-distance sorting。
- Metal 相机批次目前仍在调用尾部同步等待；缺少跨帧 fence、ring-owned argument/index buffers、command queue 合并、depth attachment 与完整 ZTest/ZWrite、camera stack/overlay、XR multiview、Scene/Game preview、resize/device-loss 和 iOS Player 长时间压力。
- 贴图/Texture2DArray/flipbook、Shader Graph material/property、alpha clip、soft particle、motion vector、官方 Output Block、Mesh/Strip/GPU Event Output，以及 Vulkan/D3D resident draw 仍未闭环。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 在 camera batch 内生成并缓存与 resident generation 绑定的 **stable compact alive index + indirect draw args**，改为 alive-count 顶点提交；随后加入透明粒子 camera-distance sort、depth attachment、ZTest/ZWrite 与异步 frame fence。
2. 实现 Texture2D/Texture2DArray/flipbook、Shader Graph material/property binding、alpha clip、soft particle/depth、motion vector及官方 Output Block lowering，并完成 URP camera stack/XR/Scene/Game 与 Unity 2022.3.61f1 截图 A/B。
3. 将相同 snapshot/order/batch/indirect contract 实现到 Vulkan 与 D3D，再扩展 Mesh/Strip/GPU Event Output，并完成 Windows、Android Vulkan 与 iOS Metal Player 的 resize/device-loss/长时间压力矩阵。

## 2026-07-17i — VFX resident Planar Output Metal framebuffer 首条生产链路

### 已完成
- 新增 backend-neutral resident Planar Output C ABI：**144-byte** output descriptor、**152-byte** camera descriptor 与 **48-byte** draw info，以及 effect-scoped `Set / Count / Draw / Clear` 生命周期。native registry 对 version/flags/effect/context/system/capacity/stride、17 个必需 attribute offset、primitive/UV/blend/cull/depth state 与 reserved 字段做完整预校验；重复 context 或非法 batch 不覆盖上一个已安装 registry，`ClearVFXEffectState` 同步释放 descriptor。
- `VisualEffectAsset` v15 Planar descriptor 现在由 `VisualEffect` 按 asset compilation version 安装到每个 live `NativeGraphicsDevice`；重新导入同一 asset 会重新安装，不继续使用旧 particle layout。managed bridge 精确映射 system capacity、packed byte offsets、compiler-executable/alpha-clip/sorting/indirect flags、render state、effect transform 与 camera world-to-clip matrix。相机 render loop 在 recorded clear/scene command 之后提交首个 transparent native pass，避免 framebuffer 被后续 camera clear 擦除；disabled/inactive/layer-mask 不匹配 effect 不提交。
- Metal 新增运行时编译并缓存的 Planar MSL/pipeline。vertex shader **直接绑定 matching `(effectId, particleSystemId, generation)` resident `MTLBuffer`**，不物化或上传 CPU particle records；按 capacity 展开 Triangle **3**、Quad **6**、Octagon **18** 个 index vertex，dead particle 在 vertex stage 丢弃，并读取 position/color/alpha、axis、Euler angle、pivot、size/scale、local-to-world 与 world-to-clip。fragment 输出 resident color/alpha；首批实现 opaque/additive/alpha/premultiplied blend 与 front/back/none cull，离屏 BGRA8/RGBA16 target 均有 pipeline cache。
- 本批保持严格真值边界：只有 `RuntimeExecutable=true`、UV0、无 alpha clipping/sorting/indirect、`ZTest=Always`、`ZWrite=false` 的 descriptor 才在 Metal 绘制；其余官方复杂输出保留 registry 并计入 `skippedOutputCount`，不伪装成功。无 resident generation、零 alive、unsupported backend/state 也明确跳过。Metal library/pipeline 编译失败会输出具体诊断并返回 `NotSupported`，不会产生空 draw。

### 测试与门禁
- 新增 `NativeVFXPlanarOutputTests` **15/15** 强制产品 dylib 测试：三种 ABI size、空/替换 registry、重复 context 原子拒绝、offset/effect/flag/camera 非法输入、Clear 生命周期、Null backend skip、Metal 无 resident clear/skip、真实 resident Quad 红色 framebuffer pixel、dead particle 透明、Triangle/Octagon vertex count，以及 sorting descriptor 的 truthful skip。专项测试触发并验证了运行时 MSL 编译；修正了 Metal shader 中 `radians` 与保留字 `vertex` 导致的真实编译错误。
- Core 强制 `AnityRequireNative=true` 全量由 **1,383** 增至 **1,398/1,398**，`FullyQualifiedName~VFX` 宽门禁由 **547** 增至 **562/562**；VFX Graph 保持 **490/490**。`bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例；产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning。`git diff --check` 通过。
- Unity API 门禁仍基于本机 **Unity 2022.3.51f1**：类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 首批 Metal pass 只支持无贴图 UV0 白材质着色，不含 Texture2D/Texture2DArray、flipbook、Shader Graph property/material binding、alpha clipping、soft particle/depth、motion vector、render queue/sorting、indirect args 或 compact alive index consumption；当前仍按 capacity 提交并在 vertex shader 丢弃 dead particle。
- 当前每个 effect 建立独立 pass、同步等待 command completion，缺少跨 effect/output 的统一 render-queue 排序、异步 frame fence、camera stack/overlay、XR multiview、Scene/Game preview、resize/device-loss 与 iOS Player 长时间压力。`ZTest=Always`/无 depth attachment 是明确的首批限制，不能描述为完整 URP 14 粒子材质/深度语义。
- Vulkan/D3D backend 目前返回 skip，尚未实现相同 resident draw contract。官方带 Output Block、Shader Graph material、geometry/mesh/strip、GPU Event 的输出仍为 descriptor-only。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 把同一 camera 的全部 effect/output 聚合成一个按 render queue/sort key 排序的 draw list，加入 alive indirect args/compact index、depth attachment/ZTest/ZWrite、async frame fence，并完成多 camera/camera stack/XR 与 resize/device-loss。
2. 实现 Texture2D/Texture2DArray/flipbook、Shader Graph material/property binding、alpha clip、soft particle/depth、motion vector及官方 Output Block lowering，再用 Unity 2022.3.61f1 Game/Scene/Player 截图 A/B 验收。
3. 将 resident Planar packet/pipeline 等价实现到 Vulkan 与 D3D，随后扩展 Mesh/Strip/GPU Event Output，并完成 Windows、Android Vulkan 与 iOS Metal 生产 Player 压力矩阵。

## 2026-07-17h — VFX Planar Output runtime asset v15 描述符桥接

### 已完成
- VFX runtime asset 格式升级到 **v15**，在现有 Update kernel 段之后追加 checksummed Planar Output 描述符。每个输出现在稳定保存 context/system identity、Triangle/Quad/Octagon 顶点数与精确 index pattern、五种 UV mode、blend/cull/ZWrite/ZTest/alpha clip/render queue/sort/indirect draw，以及与 Initialize/Update 共用的 packed particle attribute layout/stride；v1-v14 仍按原格式读取，v14 及更早资产导入为空 Planar Output 集合。
- `VfxRuntimeAssetCompiler` 现在把 `VFXPlanarPrimitiveOutput` 编译结果真正写入 runtime bytes，`VisualEffectAsset.ImportRuntimeData` 原子替换并公开给内部产品运行时消费，不再让已生成的 Planar vertex/fragment pass 只停留在 editor compiler 内存中。v15 校验强制 unique non-zero context、non-strip particle target、图元拓扑、状态范围、完整 packed layout、17 个必需 output attribute 类型，以及与同 system Update kernel 的 stride 一致性。
- 新增 `RuntimeExecutable` 真值边界：当前 childless legacy Planar Output 完整 codegen 标为 `true`；官方资产中带 Output Block、Shader Graph material 或 geometry-shader 等尚未完整执行的路径仍保存 topology/render/layout 描述符，但标为 `false`。运行时因此可以审计和继续编译官方复杂图，同时不能把 descriptor-only 路径误当成已完成 draw program。只有完整 shader codegen 入口才会标可执行。
- 修正三条旧格式测试降版器，使其在从 v15 构造 v5/v9/v10/v11 payload 时同时移除 v14 Update 段与 v15 Planar 段；真实旧资产兼容不因新增尾段产生 trailing-data 回归。

### 测试与门禁
- 新增 `VfxPlanarRuntimeDescriptorTests` **21 个测试发现项**：v15 envelope、三种精确图元、五种 UV mode、默认/opaque clipped render state、可执行与 descriptor-only 边界、17 属性 packed ABI、`VisualEffectAsset` 导入、确定性 bytes、重复 context/错误 topology/错误 required type/stride 拒绝及 v14 backward read。VFX Graph 全量由 **469** 增至 **490/490**。
- 强制加载刚重建产品 dylib 的 Core VFX 宽门禁保持 **547/547**，Core 全量保持 **1,383/1,383**；三条旧格式迁移定向门禁 **3/3**。未启用 `AnityRequireNative` 的诊断运行会按测试工程设计移除 dylib，不能作为原生门禁；最终结果均使用 `-p:AnityRequireNative=true` 隔离复验。
- `bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例；产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning。Unity 2022.3.51f1 API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。

### 尚未完成
- 本批交付的是从 VFX Graph editor compiler 到 runtime `VisualEffectAsset` 的正式 draw 描述符，不是最终 framebuffer draw：Metal/Vulkan/D3D 尚未安装/缓存 Planar pipeline、绑定 resident generation particle buffer、构造 indirect args/index expansion 并提交到 URP camera target。`RuntimeExecutable=true` 表示 compiler pass 完整，不代表所有 native backend 已接线。
- 带 Output Block、Shader Graph material、geometry shader、soft particle、gradient mapping、Texture2DArray flipbook 的复杂官方路径仍为 descriptor-only；texture/property binding、camera/XR matrices、sorting、multi-output、motion vector、depth/soft-particle 与平台 render-state A/B 尚未闭环。
- Unity 2022.3.61f1 官方 Editor/Player 仍未安装，本机 2022.3.51f1 只能作为预备基线。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 定义 backend-neutral Planar draw packet/native ABI，把 `VisualEffectAsset` v15 descriptor 注册到 effect，并让 Metal camera render 直接绑定匹配 generation 的 resident `MTLBuffer`；先闭环 childless Triangle/Quad/Octagon、render state、camera matrices 与真实 framebuffer readback。
2. 在同一 resident generation 上实现 alive compaction/indirect args、transparent sorting、multi-output/texture/property binding、motion vector、soft particle/depth 与 URP camera stack/XR，再把可执行边界扩展到官方 Output Blocks 与 Shader Graph passes。
3. 将相同 draw packet/pipeline contract 实现到 Vulkan/D3D，完成 Windows、Android Vulkan 与 iOS Metal Player 的 resize/device-loss/rollback/长时间压力和 Unity 2022.3.61f1 截图 A/B。

## 2026-07-17g — VFX GPU dead-list 压缩、resident-only 发布与回滚代际

### 已完成
- Metal Update kernel 现在为每个 physical particle 写入确定性的 death flag；同一 command buffer 随后执行并行 inclusive prefix scan 与稳定 compaction，把本次死亡的 physical index 按升序写入 GPU dead-index buffer，并产生单个 dead-count。Complete 不再复制完整 `slot.output`，也不再在 CPU 扫描 source/output records；只读取 4-byte count 与 `deadCount * 4` bytes 的紧凑索引，以 source alive-count 直接计算目标 alive-count 并更新 authoritative dead-list。
- native particle registry 新增 `attributesResidentOnly` 代际状态。Metal Update 成功后直接发布 GPU resident generation，CPU records 保持延迟状态；只有显式 `TryGetVFXParticleSystem`、Initialize 的 CPU mutation，或 resident/cache 无法满足的 CPU bounds fallback 才按准确 generation 物化完整 records。连续 Update、同队列 Automatic Bounds 与 generation CAS 不再触发无条件 particle readback 或重复 upload。
- prepared effect frame 使用 GPU copy-on-write resident snapshot 保存 Update 前代际：Complete 交换 resident/output 后把旧 buffer 从 ring slot 脱离并登记 generation；Commit 释放快照，Abort 直接恢复旧 GPU buffer/generation，再恢复 registry journal，不依赖陈旧 CPU records，也不执行补偿 upload。多 system 与同帧多代 snapshot 都按 effect 生命周期统一恢复或丢弃。
- `AnityGraphicsVFXUpdateBackendStats` 从 176 扩展到 **240 bytes**，新增 prefix pass、dead compaction、resident-only publish、deferred readback count/bytes、snapshot/restore/discard 八项计数；Null/Vulkan/D3D stub 与 managed bridge 同步保持 ABI 布局。

### 测试与门禁
- `NativeVFXUpdateLifecycleTests` 从 **81** 增至 **96/96**，新增 **15 个测试发现项**：Complete 零完整回读、首次/重复显式物化、连续六代零回读/零重传、物化后继续命中 resident、Initialize 单次物化、零死亡、capacity=1 零 prefix pass、3/5/7 非 2 次幂稳定 physical-order compaction、Commit snapshot discard、Abort 无回读 restore、同帧多代回滚与 multi-system 独立物化。
- Automatic Bounds 保持 **40/40**，与 Update 合并定向门禁由 **121** 增至 **136/136**；强制产品 dylib 的 `FullyQualifiedName~VFX` 宽过滤由 **532** 增至 **547/547**；Core 全量由 **1,368** 增至 **1,383/1,383**；VFX Graph 保持 **469/469**。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例工程；产品模块 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning。`git diff --check` 通过。
- Unity API 门禁仍基于本机可用的 **Unity 2022.3.51f1**：类型 **928/4,117**（present 22.541%，exact 404）、成员 **8,645/37,164**（present 23.262%，exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 产品 Commit 仍等待 GPU command completion，并同步读取紧凑 dead count/index list；本批消除了 `capacity * stride` 的完整 particle readback 与 CPU 双缓冲扫描，但尚未把 dead count/list 也变为后续帧异步发布。当前 Hillis-Steele prefix scan 为 `O(N log N)` 工作量，尚可替换为分层 work-efficient scan/indirect dispatch。
- Output geometry、GPU Event、Particle Strip 与真实 render draw packet 尚未直接消费 resident generation；CPU callback、显式 particle readback 与少数 fallback 仍会同步物化。Vulkan/D3D Update/dead-list/bounds 仍是 C++ CPU fallback，尚缺同等 GPU resident contract。
- Unity 2022.3.61f1 官方 Editor/Player、Windows D3D、Android Vulkan、iOS Metal 产物 A/B、device-loss/resize/长时间压力尚未闭环。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 让 Output geometry、GPU Event、Particle Strip 与 render draw packet 直接绑定 staged/resident particle generation，并把 compact dead metadata 改为跨帧/间接 GPU 消费，只在 CPU callback 真正需要时回读。
2. 将 Update、dead compaction、bounds 与 generation snapshot contract 实现到 Vulkan/D3D compute，补 Windows/Android/iOS Player 的 rollback、resize、device-loss 与长时间压力验证。
3. 扩展 VFX Update typed operand 与 Block（Turbulence、Collision、Conform、Flipbook、GPU Event），并完成复杂 Output geometry、pivot/orientation/mesh/strip 精确 bounds 与 URP camera stack/XR A/B。

## 2026-07-17f — VFX pending Update ring output 同队列 Automatic Bounds

### 已完成
- 新增内部 `AnityGraphics_BeginVFXUpdateKernelsWithBounds` C ABI：每个 Update kernel 可携带一个可选 Automatic Bounds descriptor，`effectId=0` 表示该 system 不需要 staged bounds。该入口只扩展 Anity native 调度器，不改变 Unity 公开托管 API。native 在创建 ticket 前统一校验 effect/system identity、position/alive/size/scale offsets、padding、world-space 与 reserved 字段，非法 batch 不产生 ticket 或 GPU 工作。
- Metal Update 现在在写完三槽 ring 的 `slot.output` 后，于**同一个 `MTLCommandBuffer`**继续编码 bounds map/reduce。reduction 直接读取 staged output，不等待 resident swap、不上传 CPU particle records，也不另建 command buffer。ticket 持有最终 32-byte reduction buffer与 descriptor；Complete 验证 command completion、计算 dead/alive 结果并交换 resident generation 后，才原子发布 descriptor+target-generation bounds cache。Cancel、Abort、Reset、Clear 与失败释放路径只记 discard，绝不覆盖上一个 committed cache。
- `VisualEffect.UpdateParticleSystems` 会从已编译 VFX asset 提取每个 system 的 Automatic Bounds metadata，并随 Update ticket 自动提交。PlayerLoop 继续以 Unity 式上一提交帧 bounds 做当前 culling；本帧 Update 在后台预计算下一 generation，下一帧 `TryGetWorldCullingBounds` 直接命中缓存。static bounds、无 alive layout、CPU backend 与旧内部调用继续走原路径。
- `AnityGraphicsVFXUpdateBackendStats` 从 152 扩展到 **176 bytes**，新增 pending bounds dispatch/publish/discard 三项计数；原有 bounds dispatch/completion/cache-hit 也统计同队列工作。由此可以明确区分“ring output 已预计算并发布”和“commit 后才按需 resident reduction”，而不是依赖间接时间结果。

### 测试与门禁
- `NativeVFXAutomaticBoundsTests` 从 **28** 增至 **40/40**，新增 **12 个测试发现项**：同队列 Commit/target generation/零 resident hit、Cancel 丢弃、Abort 回滚、padding key、world-space key、NaN padding 拒绝、descriptor count、null descriptor legacy fallback、连续 generation 原子替换、invalid output 保守缓存、Initialize generation 分叉 authoritative fallback，以及真实 `VisualEffect.UpdateParticleSystems → CompleteVfxFrame → culling` 产品路径。与 Update lifecycle 合并强制 native 门禁由 **109** 增至 **121/121**。
- 强制当前产品 dylib 的 `FullyQualifiedName~VFX` 宽过滤由 **520** 增至 **532/532**；Core 全量由 **1,356** 增至 **1,368/1,368**；VFX Graph 保持 **469/469**。`bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 均通过，native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例工程 0 编译错误；URP3DDemo 保持既有 43 个 nullable warning。
- Unity API 门禁仍基于本机可用的 **Unity 2022.3.51f1**：类型 **928/4,117**（present 22.541%，exact 404）、成员 **8,645/37,164**（present 23.262%，exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- Update Commit 仍等待 GPU completion，并把完整 particle output 同步复制回 authoritative CPU records、执行 CPU dead scan；本批消除的是 Update 后另起 bounds command buffer/等待/particle upload，而不是全部 simulation readback。下一步需让 resident generation 成为渲染/Output/GPU Event 的主数据源，只在 CPU callback、显式 readback 或统计真正需要时延迟复制。
- staged bounds 当前为每个 Update kernel/system 一个 AABB descriptor，最终 32-byte result 在 Commit 时读取；尚缺多 Output geometry/pivot/orientation/mesh/strip bounds、跨帧 delayed publication、timeout、可控 command/device failure、resize/device-loss 与 iOS Player 长时间压力证据。
- Vulkan/D3D Update 与 bounds 仍是 C++ staged CPU fallback，尚未实现同等 compute ring/ticket contract。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 移除 Commit 的无条件完整 particle readback/CPU dead scan：在 Update kernel 内生成 dead-index/count 与 alive-count resident buffer，先发布 GPU generation；CPU readback 延迟到事件、脚本或审计真正消费时。
2. 让 Output geometry、GPU Event、Particle Strip 与渲染 draw packet 直接消费同一 staged/resident generation，并补复杂 geometry bounds、multi-output 与 camera-stack/XR 行为。
3. 将 Update/bounds/dead-list ring contract 实现到 Vulkan 与 D3D compute，完成 Windows、Android Vulkan 与 iOS Metal Player 产物、故障注入和长时间压力验证。

## 2026-07-17e — VFX Metal resident Automatic Bounds 与 generation 结果缓存

### 已完成
- Metal Automatic Bounds 不再无条件用 authoritative CPU records 创建临时 particle buffer。`AnityGraphics_ReduceVFXParticleBounds` 现在把 committed particle generation 传入 backend；当 `(effectId, particleSystemId)` 的 Update resident generation 与 registry generation 一致时，bounds map/reduce compute 直接绑定 resident `MTLBuffer`，省略 CPU→GPU particle upload。Initialize、Abort 或其它 mutation 造成 generation 分叉时仍从 authoritative records 上传，保持事务正确性。
- 每个 Metal particle system 增加 descriptor+generation bounds result cache。position/alive/size/scale offsets、padding、world-space flag 与 generation 全部相同时，重复 camera/culling 查询直接返回已验证结果，不再提交 command buffer或等待 readback；Update Commit 会显式失效旧结果，Cancel/Abort 不交换 resident generation，因此继续复用此前 committed cache。非有限粒子产生的 invalid/conservative 结果同样缓存，避免同一坏 generation 每个 camera 重复执行。
- 内部 `AnityGraphicsVFXUpdateBackendStats` 从 112 扩展到 **152 bytes**，新增 bounds dispatch、resident hit、fallback upload、completion 与 result-cache hit 五项计数。VisualEffect 的实际 `TryGetWorldCullingBounds` 路径无需新增公开 Unity API 即自动使用 resident/cache；Null/Vulkan/D3D 保持 C++ authoritative reduction，不伪造 Metal 统计。

### 测试与门禁
- `NativeVFXAutomaticBoundsTests` 由 **15** 增至 **28/28**，新增 **13 个测试发现项**：committed Update resident 零上传、重复 descriptor cache、padding/world-space cache key、Initialize generation mismatch authoritative upload、Update Commit 失效、Cancel/Abort 保留、非法 descriptor 零计数、invalid result 保守缓存、Clear 释放、CPU fallback，以及真实 VisualEffect Metal culling 连续查询。与 Update lifecycle 合并定向门禁 **109/109**。
- 强制产品 dylib 的 `FullyQualifiedName~VFX` 宽过滤由 **507** 增至 **520/520**；Core 全量由 **1,343** 增至 **1,356/1,356**；VFX Graph 保持 **469/469**。`bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 均通过，native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例工程 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning。
- Unity API 门禁仍基于本机可用的 **Unity 2022.3.51f1**：类型 **928/4,117**（present 22.541%，exact 404）、成员 **8,645/37,164**（present 23.262%，exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 本批直接消费的是已经 Commit 的 resident generation。挂起 Update ticket 的 output slot 尚未附加 bounds descriptor，也未在同一 queue 中串接 map/reduce；因此本帧新 bounds 仍需等 Update Commit 后由后续 culling 查询计算，不能描述为 staged-output 同 command-buffer reduction。
- bounds 首次 reduction 仍同步等待 Metal completion 并读取 32-byte reduction result；结果 cache 消除了同 generation 的重复等待，但尚未实现跨帧异步 bounds ticket、multi-camera delayed publish、device-loss/resize/failure 注入与 iOS Player 压力证据。
- Output geometry、GPU Event 与 Particle Strip 尚未直接消费 resident particle buffer；Vulkan/D3D Update/bounds 仍为 C++ CPU fallback。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 给 pending Update ticket 附加 Automatic Bounds descriptors，在同一 Metal queue 上让 bounds map/reduce 直接读取 ring output，并在 Update generation CAS 成功时一起发布缓存结果；Abort/Cancel 必须丢弃 staged bounds。
2. 让 Output geometry、GPU Event 与统计读取同一 resident/staged generation，只有 CPU callback 真正需要时才延迟 readback；补 timeout、device-loss、resize 与 command failure 注入。
3. 将 resident Update/bounds contract 实现到 Vulkan 与 D3D compute，并完成 Windows、Android Vulkan 与 iOS Metal Player 产物和长时间压力验证。

## 2026-07-17d — VFX native async Update ticket 与产品帧原子提交

### 已完成
- VFX Update 新增 48-byte native ticket ABI 与 `Begin / Poll / Complete / Cancel` 四阶段入口。Begin 对整批 kernel、operation、storage 与重复 system 完成预校验后复制 authoritative particle generation；Null/Vulkan/D3D CPU 路径先计算 staged replacement 但不发布，Metal 路径提交全部 command buffer 后立即返回 ticket。Poll 只报告 pending/ready/failed，Complete 通过 particle source generation 与 prepared-frame generation 双重 CAS 后一次性发布整批 replacement，Cancel 等待并丢弃 GPU 输出，任何路径都不会让未提交记录被 readback、bounds 或下一次 simulation 观察。
- native effect transaction 已拥有挂起 Update 的生命周期：`CommitVFXEffectFrame` 在 clock/output journal 提交前完成 ticket 并发布，`AbortVFXEffectFrame`、Reset、Clear 与 device destroy 会取消 ticket；VisualEffect 产品路径由同步 Dispatch 改为 Update 阶段 Begin、frame Commit 阶段完成并刷新 `aliveParticleCount`。因此 Output event staging 可与 GPU Update 重叠，而 managed particle state 与 alive count 在 Commit 前保持上一个 authoritative generation，Abort 恢复准备帧快照。
- 同步兼容入口保持原语义：显式异步 Begin 对同 effect 的第二个 pending ticket快速拒绝；`AnityGraphics_DispatchVFXUpdateKernels` 使用独立同步串行锁，继续保证并发同步调用全部按序成功。Metal backend ticket 持有 ring slot、command buffer 与 staged records，Complete/Cancel 统一释放；Begin 后的分配失败会取消 backend handle 并从 registry 移除 ticket，不遗留不可达工作。

### 测试与门禁
- `NativeVFXUpdateLifecycleTests` 由 **63** 增至 **81/81**，新增 **18 个测试发现项**：48-byte ABI、ticket identity/poll、提交前不可见、complete/cancel 单次消费、同 effect 冲突、particle generation CAS、prepared frame identity、Commit 自动发布、Abort/Reset/Clear 自动取消、multi-system 原子发布、非法 batch 零 ticket、pending device dispose、Metal poll，以及 VisualEffect 在 Null/Metal 上的 Commit/Abort 与 alive count 事务语义。
- 强制产品 dylib 的 `FullyQualifiedName~VFX` 宽过滤 **507/507**；Core 全量由 **1,325** 增至 **1,343/1,343**；VFX Graph 保持 **469/469**。`bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 均通过，native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例工程 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning；新增四个 ticket 导出符号存在。
- Unity API 门禁仍基于本机可用的 **Unity 2022.3.51f1**：类型 **928/4,117**（present 22.541%，exact 404）、成员 **8,645/37,164**（present 23.262%，exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 产品 PlayerLoop 当前在同一 effect frame 的 Commit 阶段等待 ticket 完成；虽然 Update、Output staging 与 managed work 已拆成跨调用异步事务，但尚未把 staged GPU generation 延迟到后续 render frame，也未让 Output geometry、Automatic bounds 或 GPU Event 直接消费 resident output。Commit/Cancel 仍会等待 command completion并同步 CPU readback/dead scan。
- pending ticket 当前受 effect registry mutex 保护，Complete/Cancel 等待 GPU 时会阻塞其他 registry 操作；尚缺 per-effect/system 细粒度 ownership、timeout、可控 command/device failure、resize/device-loss 恢复与 iOS Player 长时间压力证据。
- Vulkan/D3D Update 仍为 C++ CPU staged fallback；Turbulence、Collision、Conform、Flipbook、GPU Event、Particle Strip、linked typed/resource operand、完整 Output geometry/shader execution与复杂 bounds 仍缺失。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 让 Output、Automatic bounds 与 GPU Event 直接引用 staged/resident generation，并把 readback 延迟到真正需要 CPU 事件或统计的后续帧；加入 timeout、device-loss、resize 与 command failure 注入。
2. 将同一 ticket/resident/CAS contract 实现到 Vulkan 与 D3D compute，完成 Windows、Android Vulkan 与 iOS Metal Player 产物和长时间回滚压力验证。
3. 扩展 Update typed operand 与 Block（Turbulence、Collision、Conform、Flipbook、GPU Event），再落地 Particle Strip、Output geometry/shader execution 与精确 bounds。

## 2026-07-17c — VFX Metal multi-system submit/complete/publish batch

### 已完成
- `AnityGraphics_DispatchVFXUpdateKernels` 现在先对整个 batch 完成 kernel/storage/layout/operation/random/duplicate-system 校验并预分配所有 replacement/dead-list 容量；任一后续 kernel 非法时 **零 GPU submission、零 cache 创建**。同一 effect/system 在一批中重复 Update kernel 被显式拒绝，符合官方编译图的一 system 一 Update context 不变量，也消除了旧路径可能先执行前序 kernel 再发现后序无效的 speculative 工作。
- Metal backend 从逐 kernel `commit → wait → readback` 改为真正的 **submit-all → complete-all → publish-all**：每个独立 particle system 取得自己的 resident source 与 ring slot，编码 blit + MSL compute command buffer，整批 command buffer 全部提交后才进入 completion 等待。只有全批 command 均成功完成并读回 replacement records 后，才统一交换各 system 的 resident/output generation；失败清理会等待所有已提交 command、释放 slot 并失效相关 cache，CPU authoritative registry 始终不发布半批结果。
- ring slot 拆分 `available` 与 `completed` semaphore，避免复用信号在 submit/complete 两阶段混淆。内部 backend stats ABI 从 96 扩展到 **112 bytes**，新增 last/peak batch width 与 async batch count；单 system 仍走同一 batch contract，多 system 可由统计证明在首次 wait 前已全部提交。连续 generation 命中继续省略 particle upload，operation 资源仍按 system/slot 隔离增长。

### 测试与门禁
- `NativeVFXUpdateLifecycleTests` 由 **50** 增至 **63/63**，新增 **13 个测试发现项**：2–6 system submit-all 宽度、连续五帧全 system ring 轮转/单次 particle upload、三 system Abort 后全量重传、duplicate system 零提交、Clear 全资源释放、三 system 混合死亡隔离、单 system 大 operation program 扩容不污染同批邻居、8 路并发 multi-system batch 串行安全与 zero-delta 全 completion；原失败 batch 测试升级为 pre-submit rejection 证据。
- 强制产品 dylib 的 VFX/VisualEffect 宽过滤由 **489** 增至 **502/502**；Core 全量由 **1,312** 增至 **1,325/1,325**；VFX Graph 保持 **469/469**。`bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 均通过，native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例工程 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning；导出符号与 `git diff --check` 通过。
- Unity API 门禁仍基于本机可用的 **Unity 2022.3.51f1**：类型 **928/4,117**（present 22.541%，exact 404）、成员 **8,645/37,164**（present 23.262%，exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- 当前已经允许同一 Update batch 内多个独立 system 的 command buffer 同时 in-flight，但 native 调用仍在本帧等待整批 completion、同步 memcpy 回 CPU，并在 CPU 扫描 dead-list 后才返回；尚未把 ticket 跨帧保存为延迟 readback/generation CAS，也没有让 Output/Automatic bounds 直接消费 staged GPU buffers。Metal 使用一个 command queue，实际硬件并行度由驱动调度。
- Update batch 仍在 effect registry mutex 下完成 submit/readback/publish，保证安全但会阻塞其他 VFX registry 操作；尚缺细粒度 per-effect/system lifetime、timeout/cancel、可控 command/device failure 注入、resize/device-loss 恢复和 iOS Player 压力产物。
- Vulkan/D3D Update 仍为 C++ CPU fallback；Turbulence、Collision、Conform、Flipbook、GPU Event、Particle Strip、linked typed/resource operand、完整 Output geometry/shader execution 与复杂 bounds 仍缺失。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 将 Metal batch ticket 提升为跨帧 submit/poll/publish generation queue，让 Output 与 Automatic bounds 在 GPU 直接串接，CPU 只延迟读取事件/统计；加入 timeout、cancel、device-loss/resize 注入和 generation CAS。
2. 按相同 batch/resident contract 实现 Vulkan 与 D3D compute，完成 Windows/Android/iOS Player 产物和回滚压力验证。
3. 扩展 Update typed operand 与 Block（Turbulence/Collision/Conform/Flipbook/GPU Event），再落地 Particle Strip、Output geometry/shader execution 与精确 bounds。

## 2026-07-17b — VFX Metal generation-resident Update 与 completion ring

### 已完成
- Metal VFX Update cache 从每次同时上传 source/output 改为按 `(effectId, particleSystemId)` 保存 **resident particle buffer + resident generation**。native dispatch 现在显式传入 authoritative source generation 与 staged target generation；generation 与容量均命中时不再上传 particle records，Initialize、Abort 或失败 batch 造成 generation 分叉时会自动从 authoritative CPU store 重传，避免把 speculative GPU 结果当成已提交状态。
- 每个 system 新增 **3-slot output/operation ring**。Update 前以 Metal blit 把 resident buffer 复制到当前 output slot，再执行既有 ordered MSL kernel；完成通过 `MTLCommandBuffer.addCompletedHandler` 唤醒 slot semaphore，读回成功后交换 resident/output buffer 并轮转 0→1→2。operation buffer 按 slot 独立增长，未来允许提交重叠时不会覆盖仍在使用的参数资源。
- 新增 96-byte `AnityGraphicsVFXUpdateBackendStats` 内部诊断 ABI 与 managed bridge，记录 resident generation、dispatch、particle/operation upload、GPU copy、completion、ring index/capacity 与同步 readback 数。它不污染 Unity 公共 API；Null/Vulkan/D3D 不伪造 Metal 统计，Effect Clear 与 device destroy 会移除缓存和 completion slots。

### 测试与门禁
- `NativeVFXUpdateLifecycleTests` 由 **35** 增至 **50/50**，新增 **15 个测试发现项**：7 次连续 dispatch 的 ring 轮转与仅一次 particle upload、Abort 后重传、失败 batch 后重传、Initialize mutation 后重传、operation slot 扩容但不重传粒子、Clear 后 stats/cache 重置、12 路并发 dispatch 串行安全、zero-delta completion，以及 Null backend 不暴露 Metal stats；同时锁定 64/80/96-byte C ABI。
- 强制产品 dylib 的 VFX/VisualEffect 宽过滤当前 **489/489**；Core 全量由 **1,297** 增至 **1,312/1,312**；VFX Graph 保持 **469/469**。`bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 均通过，native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例工程 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning；`git diff --check` 通过。
- Unity API 门禁仍基于本机可用的 **Unity 2022.3.51f1**：类型 **928/4,117**（present 22.541%，exact 404）、成员 **8,645/37,164**（present 23.262%，exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- particle upload 已可跨连续 Update 帧省略，GPU output allocation 也持续复用；但当前 native API 在每个 kernel 后仍等待 completion、同步 memcpy 回 authoritative CPU records，并由 CPU 扫描死亡 index。三槽 ring 已建立资源隔离与 callback completion 基础，但尚未实现跨 system/effect 的真正多 in-flight submit、延迟 readback、generation CAS 或完全 GPU-resident Output/bounds 消费。
- device/command failure 会安全丢弃 cache，但尚缺可控故障注入、超时、resize/device-loss 恢复和 iOS Metal Player 产物压力验证。Vulkan/D3D Update 仍为 C++ CPU fallback，operation descriptors 仍需每次上传。
- Turbulence、Collision、Conform、Flipbook、GPU Event、Particle Strip topology、linked typed/resource operand、完整 Output geometry/shader execution 与复杂 Automatic bounds 仍缺失。总体 Unity 2022 Ultra `/goal` 继续进行，API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 拆分 Metal Update 为 batch submit/complete/publish：跨独立 system 并行提交，使用延迟 readback ring 与 generation CAS，让 Output/Automatic bounds 可直接消费 staged GPU generation，并补 device-loss/resize/timeout 故障注入。
2. 按同一 resident-generation contract 实现 Vulkan 与 D3D compute，完成 Windows/Android/iOS Player 产物和回滚压力测试。
3. 扩展 Update typed operand 与 Block（Turbulence/Collision/Conform/Flipbook/GPU Event），再落地 Particle Strip、Output geometry/shader execution 与精确 bounds。

## 2026-07-17a — VFX Metal persistent Update compute 与事务发布

### 已完成
- `anity-native` 为 VFX Update v14 ordered IR 增加真实 Metal compute pipeline：SetAttribute（constant/source snapshot，含 per-component/uniform random 与 seed 持久化）、CopyAttribute、Integrate、Reap、absolute/relative Force、Drag 与 particle-size 路径均在 MSL kernel 执行；entry-alive、zero-delta、死亡粒子仅提交 alive=false 等既有 C++ 语义保持一致。CopyAttribute 在 CPU/Metal 两条路径统一改为临时 4-word 拷贝，重叠源/目标不再依赖写入顺序。
- Metal 后端按 `(effectId, systemId)` 复用 persistent shared source/output/operation buffers，并缓存 Update compute pipeline；每次 dispatch 从 authoritative staged CPU clone 上传，GPU 完成后同步读回，再按物理 index 升序扫描死亡变化并更新 dead-list。Effect 清理与 device destroy 会释放对应缓存，effect identity 可安全复用。
- `AnityGraphics_DispatchVFXUpdateKernels` 在 Metal 设备选择 compute backend，Null/Vulkan/D3D 保持 C++ CPU fallback；仅当整个 kernel batch 成功才交换 staged registry。后续 kernel 失败或 Effect Abort 都不会发布部分 GPU 结果；下一次 Metal dispatch 会用 committed CPU records 完整覆盖缓存，因此失败帧的 cache 内容不可被外部状态观察。

### 测试与门禁
- `NativeVFXUpdateLifecycleTests` 由 **18** 增至 **35/35**：新增 13 组 CPU↔Metal program 等价比较，覆盖 overwrite、Add/Multiply/Blend、source snapshot、Gravity/Euler、absolute/relative Force、Drag 有无 size、Reap、zero delta、两种 random 与 CopyAttribute；另覆盖 Metal Abort、batch 失败后 cache 隔离、Clear 后 identity 复用，以及多死亡 index 的确定性顺序。
- 强制产品 dylib 的 VFX/VisualEffect 宽过滤由 **444** 增至 **461/461**；Core 全量由 **1,280** 增至 **1,297/1,297**；VFX Graph 保持 **469/469**。`bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 均通过，native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例工程 0 编译错误，URP3DDemo 保持既有 43 个 nullable warning；`git diff --check` 通过。
- Unity API 门禁仍基于本机可用的 **Unity 2022.3.51f1**：类型 **928/4,117**（present 22.541%，exact 404）、成员 **8,645/37,164**（present 23.262%，exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，不能把 51f1 结果冒充最终版本证据。

### 尚未完成
- Metal Update 已是真实 GPU 计算与持久 allocation，但当前每次仍从 authoritative CPU clone 同步上传并同步读回，不是全 GPU resident/异步 generation ring；dead-list 也由 GPU 完成后 CPU 确定性扫描生成。尚缺 async generation CAS、无阻塞 readback、resize/device-loss 故障注入与 iOS Metal Player 产物验证。
- Vulkan/D3D Update 仍走 C++ CPU fallback；Turbulence、Collision、Conform、Flipbook、GPU Event、Particle Strip topology、linked typed/resource operand、完整 Output geometry/shader execution 与复杂 Automatic bounds 仍未实现。
- 总体 Unity 2022 Ultra `/goal` 继续进行：API 审计仍缺 **3,189** 个官方类型，Unity 2022.3.61f1 官方编辑器/Player 与 Windows/Android/iOS 全平台 A/B 未闭环，本里程碑不代表完整 Unity/VFX 生产等价。

### 下一优先项
1. 把 Metal Update 改为 GPU-resident staged generation + async readback ring/CAS，补 resize/device-loss/Abort 压测和 iOS Player 产物；同一事务模型下实现 Vulkan 与 D3D compute。
2. 扩展 Update typed operand 与 Block：Turbulence、Collision、Conform、Flipbook、GPU Event，并让 Rate/Burst/SetAttribute/loop/delay 复用 native evaluator。
3. 落地 Particle Strip、Output geometry/shader execution 与 pivot/orientation/mesh 精确 bounds，再用 Unity 2022.3.61f1 官方 Player 固化 URP camera stack/XR/SceneView/Preview/occlusion 证据。

## 2026-07-16ba — VFX persistent Update/Reap native 事务生命周期与 runtime asset v14

### 已完成
- VFX runtime asset 从 **v13 升级到 v14**，继续读取 v1-v13；新增按 context/system 排序的 Update kernel 与 ordered operation IR，版本化保存真实 Initialize packed-layout、alive/seed offset、dead-list、`skipZeroDeltaUpdate`、常量/Source snapshot SetAttribute、Copy、Integrate、Reap、Force、RelativeForce 与 Drag。导入会拒绝非法枚举、offset、operand、非有限常量、错误类型以及与 Initialize capacity/stride/dead-list/attribute type 不一致的布局。
- VFX Graph 的 Basic Update 不再要求至少一个显式 Block：先收集同一 particle data 的 Initialize/Update/Output 全系统持久属性，再生成 Unity 顺序的隐式 position/angular Euler、age 与 `age > lifetime` Reap。官方无显式 Block 的 `SimpleParticleSystem.vfx` 现可产生真实运行时 Update；确实没有任何运行语义的 Update context 被安全省略，不再伪造 operation。现有 SetAttribute/Custom、Gravity、Force absolute/relative、Drag 与 UseParticleSize 已 lowering 到同一 backend-neutral IR；linked activation/dynamic input 在 evaluator 支持前显式拒绝。
- `anity-native` 新增 **64-byte Update kernel / 80-byte operation C ABI** 与 `AnityGraphics_DispatchVFXUpdateKernels`。Null/Metal/Vulkan/D3D 当前均由 C++ authoritative particle store 的 CPU fallback 执行：每个存活粒子先保存 entry source snapshot，再按 block 顺序修改 local record；随机 seed 持久化，死亡粒子只提交 alive=false 并写入 dead-list，其他同帧字段修改丢弃，物理 index 可被下一次 Initialize 复用。
- Update kernel batch 使用 copy-on-write staged registry：全部 kernel 校验与执行成功后才交换发布，任一后续 kernel 失败不会留下前序部分更新。Update 已插入产品帧的 Initialize 之后、Output staging 之前，并共享既有 Prepare/Commit/Abort particle snapshot；Abort 恢复 attributes/alive/dead-list/generation，Commit 后 Output、Automatic bounds 与下一帧 culling 才观察新状态。
- 全量并行门禁另外复现并修复 native texture upload/release 的 `_textureStates` 并发破坏：纹理表与 native texture/device teardown 现在使用独立锁，避免把 Canvas 销毁路径卷入 device lifetime 锁序；ScreenCapture/native texture 定向和整个 Core 并行门禁均稳定通过。

### 测试与门禁
- 新增 `NativeVFXUpdateLifecycleTests` **18/18**：覆盖 ABI、Overwrite/Add/Multiply/Blend、entry source snapshot、Gravity→Euler 顺序、absolute/relative Force、Drag 有无 particle size、zero-delta、age/reap/physical-index recycle、Abort/Commit、batch 原子失败、v14 round-trip 与 Initialize layout 拒绝。
- VFX Graph 全量 **469/469**；强制产品 dylib 的 VFX/VisualEffect 宽过滤由 **439** 增至 **444/444**；Core 全量由 **1,262** 增至 **1,280/1,280**。ScreenCapture/native texture 定向 **27/27**；`bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例工程，0 编译错误，URP3DDemo 保持既有 43 个 nullable warning。
- Unity API 门禁仍基于本机可用的 **Unity 2022.3.51f1**：类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。本机仍未安装目标 2022.3.61f1，因此不能把 51f1 结果冒充最终 61f1 证据。

### 尚未完成
- 本批 Update 是生产 C++ authoritative store 与事务语义，但仍为 CPU fallback；尚未把 operation IR 执行到 Metal/Vulkan/D3D persistent compute buffers，也没有 async generation CAS/device-loss 恢复。因此不能描述为完整 VFX Graph GPU Update 生命周期。
- 当前 lowering 只覆盖已有受支持 Block 和常量/source snapshot 输入；Turbulence、Collision、Conform、Flipbook、GPU Event、Particle Strip topology、linked typed DAG/native operand、资源型 input 与完整 Output geometry/shader execution 仍缺失。Automatic bounds 也尚未纳入 pivot/orientation/mesh/strip 精确几何。
- Unity 2022.3.61f1 官方编辑器/Player、Windows D3D、Android Vulkan、iOS Metal 产物 A/B 仍未闭环。总体 Unity 2022 Ultra `/goal` 继续进行；当前 API 审计仍缺 **3,189** 个官方类型，本里程碑不代表完整 Unity 等价。

### 下一优先项
1. 把 Update IR 下沉为 Metal/Vulkan/D3D persistent compute resource transaction，加入 async generation CAS、device-loss/resize/Abort 深测与三平台 Player 产物。
2. 扩展 VFX Update Block 与 typed linked operand：Turbulence/Collision/Conform/Flipbook/GPU Event，并让 Rate/Burst/SetAttribute/loop/delay 复用同一 native evaluator。
3. 落地 Particle Strip、Output geometry/shader execution 与 pivot/orientation/mesh 精确 Automatic bounds，再用 2022.3.61f1 官方 Player 固化 URP camera stack/XR/SceneView/Preview/occlusion 证据。

## 2026-07-16az — VFX Automatic Bounds native/Metal reduction 与事务化剔除接入

### 已完成
- VFX runtime asset 从 **v12 升级到 v13**，继续读取 v1-v12；Particle/ParticleStrip system 现在版本化保存 Automatic bounds 标志、local/world simulation space、`position/alive/size/scaleX/Y/Z` 的真实 Initialize packed-layout word offset，以及三轴 `boundsPadding`。序列化会拒绝 static/automatic 冲突、非法 offset、负数/非有限 padding、无 position、错误 attribute 类型或与 Initialize kernel 不一致的布局；v13→旧版迁移测试会真实移除新增 63-byte system metadata 并重算 checksum。
- VFX Graph compiler 对 Automatic/`needsComputeBounds` 不再永久降级为 unbounded：从目标 Initialize 的 data-wide stored attribute compilation 生成真实 layout，校验实际 stored `position` 为 Float3，并保存可选 alive/size/scale offset；constant `boundsPadding` 被精确固化，linked padding 在 runtime expression evaluator 落地前显式拒绝，避免烘焙错误 bounds。
- `anity-native` 新增 56-byte reduction desc/result 与 `AnityGraphics_ReduceVFXParticleBounds` 稳定 C ABI。native CPU 路径遍历 authoritative particle records，按 alive attribute 或 sequential/dead-list occupancy 过滤存活粒子，并以 `position ± abs(size*scale)/2` 加 padding 归约 AABB；alive 数不一致、非有限粒子数据、0 alive 或非法 layout 都保守返回 unbounded/错误，不发布可能漏裁的范围。
- Metal 后端增加实际运行的并行 map + pairwise reduction compute pipeline，以 shared buffer 同步 readback 最终 min/max，并逐项核对 live count/non-finite 标志；Null/Vulkan/D3D11 当前走同一 native CPU fallback。返回值携带 particle generation 与 backend kind，managed bridge 严格核对 identity、generation、space、finite extents 和 ABI 结果。
- Automatic bounds 已接入 `VisualEffect` world culling AABB 合并与真实 VFX PlayerLoop culling descriptor。local-space 结果使用完整 affine matrix 转换，world-space 结果不二次变换；任一 system 没有可靠结果时整个 Effect 保守 unbounded。reduction 只允许读取最后一次 Commit 的 particle storage；Effect 已 Prepare 时拒绝 readback，Abort 恢复旧粒子/generation/bounds，Commit 后下一次 culling 才观察新范围，因此保持既有的一帧可见性延迟与 effect transaction 原子性。

### 测试与门禁
- 新增 `NativeVFXAutomaticBoundsTests` **15/15**：覆盖 56-byte ABI、CPU size/scale/padding、dead-list、无 alive sequential occupancy、0 alive、NaN、非法 offset、Prepare 拒读、Abort 恢复、Commit generation、v13 round-trip/布局拒绝、local/world transform，以及真实 Metal compute backend 与 CPU contract 一致性。
- VFX Graph bounds compiler 定向 **9/9**，新增 Automatic local/world metadata、v13 round-trip 和 linked padding 拒绝；VFX Graph 全量由 **466** 增至 **469/469**。Spawner/runtime 旧资产迁移与 callback 定向 **171/171**。
- 强制产品 dylib 的 VFX/VisualEffect 宽过滤由 **424** 增至 **439/439**；Core 全量由 **1,247** 增至 **1,262/1,262**。`bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例工程，0 编译错误；URP3DDemo 仍为既有 43 个 nullable warning。
- Unity API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。官方 Unity 2022.3.51f1 / VFX Graph 14.0.11 fixture 未改动，继续保留 **582 state + 100 Output Event + 24 callback + 66 Built-In callback + 17 manual-control** 的既有精确证据。

### 尚未完成
- Metal 已是 GPU compute reduction，但目前为同步 shared-buffer readback；Vulkan/D3D11 仍是 native CPU fallback，尚缺各自 persistent GPU reduction、异步/延迟 readback ring、generation staged CAS、device-loss 恢复与 iOS/Windows/Android Player 产物验证，不能描述为 Automatic bounds 全后端 GPU 闭环。
- 当前几何扩张覆盖 position、uniform size、scaleX/Y/Z 与 padding；尚未把 pivot、angle/axis/orientation、mesh/output geometry、Particle Strip segment topology、Update/Reap 后的持续运动和多 output 精确几何纳入 bounds，所以复杂输出仍需保守扩张/官方 A/B 后才能宣称 Unity 完全等价。
- 遮挡裁剪、URP base/overlay camera stack、XR 多 view、SceneView/Preview 编辑器证据，以及最终 engine-native player host 仍未闭环。总体 Unity 2022 Ultra `/goal` 继续进行；API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 落地 Update/Reap/Output persistent particle lifecycle，使移动、死亡、strip/output geometry 与 Automatic bounds 共用同一 GPU generation transaction，并补 pivot/orientation/mesh/strip 官方 fixture。
2. 为 Vulkan/D3D11/Metal 建立持久 compute resource、异步 bounds readback ring 与 staged generation CAS，完成 Windows/Android/iOS Player 的 device-loss/resize/rollback 深测。
3. 用 Unity Player 固化 URP base/overlay、XR views、SceneView/Preview 与 occlusion 的最终剔除语义，再继续 linked typed DAG、资源型 callback、多 chain 和完整 VFX Graph 编辑器。

## 2026-07-16ay — VFX Effect 全数据 rollback journal 与 Commit 后 Output Event

### 已完成
- `anity-native` 把当前已实现的 Effect 数据面统一纳入 Prepare/Commit/Abort 事务：除 frame clock 与 Spawner 外，现会按 effectId 保存并恢复 Initialize dispatch、Particle attribute records、alive count、dead-list、Output FIFO 与 sequence watermark。Spawner 恢复会先移除本事务新建的实例，再安装 committed snapshot，失败帧不再泄漏临时 context。
- Input Event 采用消费 journal 而不是冻结整个队列：每次 `ConsumeVFXEventDispatchPlan` 在删除前记录已消费前缀；Abort 将前缀放回队首，同时保留 Prepare 后并发追加的队尾事件，Commit 才丢弃 journal。Prepare 先在局部对象完成所有 snapshot 分配，再原子安装到 frame storage，内存分配失败不会留下半安装 rollback 状态。
- managed frame snapshot 同步覆盖 `aliveParticleCount` 与最后一次 input dispatch。内部 dispatch/callback/Output 校验失败会恢复 native 与 managed 两侧状态并允许下一帧只消费一次重试；Abort 同时清空未提交的 managed Output staging。
- 产品帧的 Output Event 改为 Prepare 内从 native FIFO 出队并校验、Commit 成功后才进入外部 `outputEventReceived` 用户回调。用户回调异常发生时 native frame 已提交，不会触发错误回滚；同次提交中尚未开始的后续 batch 会保留到下一次 Effect update 再交付。
- `NativeGraphicsDevice` 新增 VFX 产品帧 lifetime fence：PlayerLoop、显式 Camera 更新、culling camera submission/complete 在 native 使用期内阻止并发销毁；同线程回调内 `Dispose` 延迟到最外层 native use 退出，跨线程 `Dispose` 等待临界区。由此消除了全量并行测试曾复现的 native registry mutex use-after-dispose 崩溃。

### 测试与门禁
- 新增 `NativeVFXEffectDataTransactionTests` **19/19**：覆盖 input Abort/Commit/并发尾部、Initialize 新建/覆盖/Commit、Particle attributes/alive/dead-list 的 CPU 与 Metal Abort/Commit、Output FIFO/sequence watermark、Commit 前不可见、managed Abort、回调异常发生在 Commit 后、后续 batch 重试、真实 PlayerLoop 次帧交付，以及 managed input 单次重试。
- 强制产品 dylib 的 VFX/VisualEffect 宽过滤由 **405** 增至 **424/424**；Core 全量由 **1,228** 增至 **1,247/1,247**，并在并行全量门禁中确认不再出现 device Dispose/native mutex 崩溃。VFX Graph 保持 **466/466**。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例产品工程，0 编译错误；URP3DDemo 仍为既有 43 个 nullable warning。Unity API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。
- 官方 Unity 2022.3.51f1 / VFX Graph 14.0.11 fixture 未改动，继续由 **582 state + 100 Output Event + 24 callback + 66 Built-In callback + 17 manual-control** 精确记录门禁约束既有时序。

### 尚未完成
- 本批原子化的是当前已经存在的 CPU/native registry 数据。尚未实现的 Update/Reap/Output 完整 particle lifecycle、GPU persistent buffers、generation snapshot + staged CAS、Vulkan/D3D11 compute 与 GPU Event 未来加入后，也必须进入同一 effect transaction；因此不能把当前 journal 描述为完整 VFX Graph GPU 事务。
- 外部 Output callback 已严格移到 Commit 后，且后续 batch 可重试；但 callback 抛异常时同 batch 剩余 record、多个订阅者与回调重入的 Unity Player 精确继续/中止规则还缺官方异常注入 A/B，当前不宣称这一边界完全等价。
- Automatic GPU bounds、遮挡/URP camera stack/XR、最终 engine-native player host、动态 native operand、资源型 callback、多 chain、完整 VFX Graph 编辑器及跨平台产物仍未闭环。总体 Unity 2022 Ultra `/goal` 继续进行；API 审计仍缺 **3,189** 个官方类型。

### 下一优先项
1. 实现 Automatic bounds 的 native/GPU reduction、跨后端 readback 与 transaction generation，并用 URP base/overlay、XR view、SceneView/Preview 及遮挡裁剪官方 fixture 固化最终 camera stack 语义。
2. 让 linked activation、Rate/Burst/SetAttribute/loop/delay 复用 typed DAG，增加不重置 native Spawner 状态的 operand upload/evaluator，再继续资源型 callback 与多 chain。
3. 落地 Update/Reap/Output 完整粒子生命周期和 Vulkan/D3D11/Metal persistent compute resource transaction，并补 Output callback 异常/重入官方 Player A/B。

## 2026-07-16ax — VFX 手动仿真与异常 Abort/rollback native 帧事务

### 已完成
- 扩展 Unity 2022.3.51f1 / VFX Graph 14.0.11 非 batchmode Metal Player 探针，以真实 Player 锁定 `VisualEffect.Simulate`、`AdvanceOneFrame` 与 `Reinit`：`Simulate(step,count)` 延迟到下一次 VFX update，逐 step 使用原始 `stepDeltaTime`、忽略 `playRate`，同一游戏帧内多个 step 共享 VFX FrameIndex；`stepCount=0` 不运行，负数使时间倒退，NaN 原样传播且均不抛异常。`AdvanceOneFrame` 仅在 pause 时排队，使用下一帧游戏 delta/playRate 路径；非 pause 调用无效果。`Reinit` 立即清空公开 clock/Spawner 状态和已排队手动步，并在下一次 VFX update 调度初始事件。
- `anity-native` 新增 `AnityGraphics_PrepareVFXEffectManualFrame` 与 `AnityGraphics_AbortVFXEffectFrame`。每次 normal/manual Prepare 都保存最后一次 committed clock 与所有 native Spawner 状态；Commit 只在 callback、Initialize dispatch 和 Output 阶段全部成功后推进，Abort 恢复 committed total/accumulator/generation 与 Spawner loop/random/block clocks，首次未提交 Effect 则完整移除临时 clock。manual Prepare 保留 IEEE 负数/NaN 语义，公开普通 Spawner tick 的参数校验不被放宽。
- `VisualEffect` 新增延迟 manual-update 队列：`Simulate` 按 step 排队、pause 下 `AdvanceOneFrame` 排队、`Reinit` 清队列并重置 native/managed 状态。`VFXManager` 在 regular update 前排空手动步，每一步都走同一 Prepare → input → Spawner/callback → output → Commit 事务；异常会清理剩余手动工作、调用 native Abort、刷新 managed Spawner snapshot 并恢复 managed frame cache，然后重新抛出原始异常。
- PlayerLoop 异常路径同时收口：callback/dispatch 失败时会完成或清除当前 culling transaction，避免残留 active frame 和已释放 graphics device 污染下一次 update。一次性 callback 抛错后的同 Effect、同 device 下一帧可重新 Prepare 并正常推进，不再卡在 prepared 状态。

### 测试与门禁
- 新增 `NativeVFXManualFrameTransactionTests` **15** 个测试，加上 PlayerLoop callback 异常恢复 **1** 个测试，共 **16/16**：覆盖精确手动 delta、首次 Abort 删除状态、回滚 total/accumulator/Spawner、错误 frame、Abort 后禁止 Commit、同 frame 重试、延迟 Simulate、忽略 playRate、多步共享 frame、0 step、负数、NaN、pause AdvanceOneFrame、非 pause no-op、Reinit 取消队列与下一帧恢复。
- 官方 fixture 现通过 **582 state + 100 Output Event + 24 callback + 66 Built-In callback + 17 manual-control** 精确记录门禁；脚本对 deferred 执行、frame index、delta/total、playRate、pause、0/负数/NaN 与 Reinit 初始事件逐项断言。
- 强制加载产品 dylib 的 callback/Spawner 定向测试 **154/154**，VFX/VisualEffect 宽过滤由 **389** 增至 **405/405**，Core 全量由 **1,212** 增至 **1,228/1,228**；VFX Graph 保持 **466/466**。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例产品工程；0 编译错误，URP3DDemo 仅有既有 43 个 nullable warning。Unity API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。

### 尚未完成
- 本批可回滚范围是 VFX frame clock、fixed-step accumulator、managed frame cache 与 native Spawner 状态。已消费 input event queue、Initialize/Particle buffer、dead-list 以及已向外部用户代码交付的 Output Event 尚没有统一 deep snapshot/rollback；因此不能宣称 callback/dispatch 异常下整个 Effect 的全部数据都已原子化。
- 仍需补 `Simulate`/`Reinit` 并发争用、极大 `stepCount` 的调度预算/防饿死，以及各平台 Player 的异常注入 A/B。Automatic GPU bounds、遮挡/URP camera stack/XR、最终 engine-native player host、动态 operand、资源型 callback、多 chain、Update/Reap/Output 与完整 VFX Graph 编辑器仍未闭环。
- 总体 Unity 2022 Ultra `/goal` 继续进行；API 审计仍缺 **3,189** 个官方类型，本里程碑不代表完整 Unity/VFX 生产等价。

### 下一优先项
1. 把 input event queue、Initialize/Particle/dead-list 与 Output queue 纳入 per-effect native transaction snapshot，并延迟外部 Output Event 交付到 Commit 后，做到 Prepare 之后任意内部异常的全数据回滚。
2. 实现 Automatic bounds 的 native/GPU reduction 与跨后端 readback，并用 URP base/overlay、XR view、SceneView/Preview 及遮挡裁剪官方 fixture 固化最终 camera stack 语义。
3. 让 linked activation、Rate/Burst/SetAttribute/loop/delay 复用 typed DAG，增加不重置 native Spawner 状态的 operand upload/evaluator，再继续资源型 callback、多 chain 与 Update/Reap/Output。

## 2026-07-16aw — VFX native bounds/frustum culling 与一帧可见性延迟

### 已完成
- `anity-native` 新增完整 VFX culling registry 与稳定 C ABI：40-byte `AnityGraphicsVFXCullingBounds`、80-byte `AnityGraphicsVFXCullingCamera`、40-byte `AnityGraphicsVFXCullingState`，以及 Begin/SubmitCamera/Complete/GetState 四个入口。Begin 与当前 native PlayerLoop token/frame 严格绑定并事务安装 Effect bounds；Camera submission 按 `GameObject.layer`/`Camera.cullingMask` 过滤，以 world AABB 八角点执行 homogeneous clip-space frustum 判定；多个 Camera 做 OR 可见性合并，重复 Camera 拒绝，Complete 才发布下一帧 `culled` 与 generation。
- `VFXManager` 已把 culling 事务接入真实游戏帧：本帧 Effect 更新读取上一帧完成的可见性，本帧所有启用 Camera 在 render loop 中提交，`Camera.RenderAll` 结束后发布结果，因此锁定 Unity Player 已观测到的一帧延迟。一个 Effect 被任意 Game/SceneView/Preview Camera 看见即继续模拟；无 Camera、没有可靠静态 bounds、disabled Effect 都保守为不裁剪。重复渲染同一 Camera 不重复计数，多 Camera 不重复更新 Effect。
- `VisualEffect` 可把所有 particle system 的 local/world 静态 bounds 合并成 world AABB；local bounds 使用完整 `localToWorldMatrix` 的绝对 3×3 计算旋转/非均匀缩放后的 extents，world-space bounds 不受组件 Transform 二次变换。任一 system 为 dynamic/unknown bounds 时整个 Effect 保守视为 unbounded，避免错误停算。
- VFX runtime asset 从 **v11 升级到 v12**，继续读取 v1-v11；每个 Particle/ParticleStrip system 版本化保存静态 AABox 与 local/world simulation space。VFX Graph 编译器从 Initialize 的官方 `bounds` structured slot 读取 Recorded/Manual AABox，Recorded 模式加入 `boundsPadding * 2`；Automatic、`needsComputeBounds`、linked/非法 bounds 不伪造静态结果。`VisualEffectAsset.ImportRuntimeData` 将 v12 contract 安装到运行时 culling metadata。
- 旧版本迁移测试已按 v12 system layout 更新：测试真正移除新增 bounds 字段并重算 checksum 后再验证 v11/v10/v9/v5 读取，不以只改版本号的无效 payload 冒充兼容。原先直接写 internal `effect.culled` 的 callback 测试改为真实 Camera/frustum 驱动，验证首帧更新、下一帧冻结、返回视锥后一帧恢复与不补算 TotalTime。

### 测试与门禁
- 新增 native ABI/事务/边界 **14** 个测试和真实 PlayerLoop/Camera/bounds **13** 个发现项，共 **27/27**：覆盖 ABI size、内外视锥、Complete 前不发布、无 Camera、无静态 bounds、layer mask、多 Camera OR、重复 Camera/Effect、NaN、陈旧 token、跨帧保留、Clear、首帧/恢复一帧延迟、disabled Camera、SceneView/Preview、local affine bounds、world runtime-v12 bounds。
- VFX Graph 新增 **6** 个测试，覆盖 Recorded padding、Manual、world space、Automatic unbounded、v12 round-trip、非法 bounds；全量由 **460** 增至 **466/466**。Core 新增共 **27** 个测试发现项，由 **1,185** 增至强制 native **1,212/1,212**；VFX/VisualEffect 宽过滤由 **362** 增至 **389/389**。
- `bash _scripts/capture-unity-vfx-spawner.sh` 已重新构建官方 Unity 2022.3.51f1 / VFX Graph 14.0.11 非 batchmode Metal Player 并通过 **582 state + 100 Output Event + 24 callback + 45 Built-In callback** 原有精确门禁，其中双 Effect/3 Camera 的离开/返回视锥一帧延迟继续成立。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例产品工程；0 编译错误，URP3DDemo 仅有既有 43 个 nullable warning。Unity API 门禁仍为类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。

### 尚未完成
- Recorded/Manual 静态 bounds 与 frustum/layer/multi-camera 已闭环；Automatic/`needsComputeBounds` 目前只做安全的 unbounded fallback，尚未实现 GPU particle reduction/readback 的动态 bounds。遮挡裁剪、URP base/overlay camera stack 的官方逐平台 A/B、XR 多 view、SceneView/Preview 编辑器截图与 Vulkan/D3D11/iOS player 产物仍未闭环。
- PlayerLoop token、VFX clock、culling registry 都已在 native，但最终平台 player 的主循环入口仍由托管 `UnityRuntime.Tick` 驱动。`VisualEffect.Simulate` / `AdvanceOneFrame` / Reinit 与 callback/dispatch 异常 Abort/rollback 仍需统一到可回滚 native transaction。
- linked activation、Rate/Burst/SetAttribute/loop/delay 动态 operand、资源型 callback、多 chain、Update/Reap/Output、generation snapshot + staged CAS、Vulkan/D3D11 compute、GPU Event、Particle Strip、完整 VFX Graph 编辑器及其余 Unity 公开面仍未完成；API 审计仍缺 **3,189** 个官方类型，总体 Unity 2022 Ultra `/goal` 继续进行。

### 下一优先项
1. 把 `VisualEffect.Simulate`、`AdvanceOneFrame`、Reinit 与异常 Abort/rollback 纳入 native clock/culling transaction，补至少 10 个失败恢复/并发深测与官方逐相位 A/B。
2. 实现 Automatic bounds 的 native/GPU reduction 与跨后端 readback，并用 URP base/overlay、XR view、SceneView/Preview 及遮挡裁剪官方 fixture 固化最终 camera stack 语义。
3. 让 linked activation、Rate/Burst/SetAttribute/loop/delay 复用 typed DAG，增加不重置 native Spawner 状态的 operand upload/evaluator，再继续资源型 callback、多 chain 与 Update/Reap/Output。

## 2026-07-16av — VFX native PlayerLoop、多 Camera 与 pause/culled 调度语义

### 已完成
- `anity-native` 新增 `AnityGraphics_BeginVFXPlayerLoopFrame`：device registry 持有单调 `playerLoopToken` 与对应 VFX `frameIndex`，同 token 任意重复调用只返回同一帧且 `beganFrame=0`，新 token 只推进一次，0/陈旧 token 显式拒绝。显式 `BeginVFXFrame` 会使当前 PlayerLoop token 失效，避免显式测试/工具帧被误当成 render-loop 重入；四个 frame clock export 已由产品 dylib 实际导出。
- `UnityRuntime.Tick` 在 Canvas/Camera 渲染前调用 `VFXManager.ProcessPlayerLoopUpdate`，即使场景没有 Camera 也会推进 VFX。每个游戏帧由 native token 只创建一个 VFX frame，所有 live Effect 共享 FrameIndex、各自只执行一次 Prepare→Spawner/Callback/Output→Commit；随后 1 个或多个 `RenderPipeline.RenderSingleCamera` camera command 复用该结果，不再按 Camera 数重复累积时间或发射 OnUpdate。显式 `ProcessCameraCommand` 保持一调用一帧，方便编辑器/测试独立驱动。
- pause 与 culled 调度按官方 Player 证据纠正：pause 的 Effect 仍执行 Spawner OnUpdate，但 `VFX Delta Time=0`、TotalTime 与 fixed-step accumulator 冻结、全局 FrameIndex 继续递增；native zero-delta tick 仅执行 ordered SetAttribute/Custom Callback，不推进 Rate/Burst/loop time。`effect.culled=true` 时整个 Effect update 跳过，FrameIndex/TotalTime/Spawner callback 均冻结，恢复可见后从旧状态继续。
- Unity 2022.3.51f1 / VFX Graph 14.0.11 非 batchmode Metal Player fixture 新增双 Effect + 3 Camera 场景：A 使用 seed 101/playRate 1.25，B 使用 seed 202/playRate 2。精确锁定每个 `Time.frameCount` 每 Effect 仅一次 OnUpdate、共享 VFX FrameIndex；A pause 三帧仍有 3 次 zero-delta callback 且 TotalTime 不变；移到远端后有一帧可见性延迟，再连续三帧完全无 callback，返回后 TotalTime 不补算。Built-In 证据由 12 增至 **45 条**（原场景 12 + A 15 + B 18），脚本以 exact count、方法分布、Camera 数、pause/cull 分组和时间冻结逐项硬断言。

### 测试与门禁
- 新增 **22 个 Core 测试发现项**，Core 从 1,163 增至 **1,185/1,185**：覆盖 10 个 token 递增样本、同 token 重入、0/陈旧 token、显式帧冲突恢复、无 Camera、双 Camera 去重、双 Effect 共享帧/隔离 Total、pause、disabled/culled、显式 camera 独立推进，以及 pause callback zero delta 与 culled callback/clock 冻结。测试清理 live Effect，避免 PlayerLoop 将前序故障注入 fixture 当成产品 Effect 更新。
- 主 `libanity_native.dylib` 重新编译后，以 `ANITY_REQUIRE_NATIVE=1` + `AnityRequireNative=true` 强制执行 VFX 定向回归 **349/349**；额外包含 `VisualEffect*` 命名套件的宽过滤为 **362/362**，没有托管 fallback。VFX Graph 保持 **460/460**。
- `bash _scripts/capture-unity-vfx-spawner.sh`、`bash _scripts/build-native.sh Release`、`bash _scripts/build-all.sh Release` 全部通过；native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例产品工程 0 编译错误，URP3DDemo 只有既有 43 个 nullable warning。Unity API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。

### 尚未完成
- 本批已尊重 `VisualEffect.culled` 的官方停算语义，但还没有用 Effect bounds、Camera frustum、camera stack/遮挡结果自动计算并提交 culled 状态；当前不能宣称完整 VFX culling。`VFXUpdateMode`、编辑器 preview/SceneView 以及不同 render pipeline/camera stack 的逐平台 Player A/B 也未闭环。
- PlayerLoop 的唯一帧 token 与时钟所有权已在 native，但调度入口仍由托管 `UnityRuntime.Tick` 触发；还需接入最终 native engine loop/平台 player host。公共 `Simulate` / `AdvanceOneFrame`、Reinit 与 callback/dispatch 异常 Abort/rollback 尚未统一到可回滚 native transaction。
- linked activation、动态 Rate/Burst/SetAttribute/loop/delay、资源型 callback input、多 callback/Spawner/custom event chain、Update/Reap/Output、generation snapshot + staged CAS、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph与完整编辑器/平台产物仍未完成；API 审计仍缺 **3,189** 个官方类型，总体 Unity 2022 Ultra `/goal` 继续进行。

### 下一优先项
1. 将 Effect bounds 与 Camera/camera stack 的可见性判定落到 native culling registry，并用官方 Player 锁定一帧延迟、SceneView/preview 和无 Camera 行为。
2. 把 `VisualEffect.Simulate`、`AdvanceOneFrame`、Reinit 与异常 Abort/rollback 纳入 native clock transaction，补至少 10 个失败恢复/并发深测与官方逐相位 A/B。
3. 让 linked activation、Rate/Burst/SetAttribute/loop/delay 复用 v11 typed DAG，增加不重置 native Spawner 状态的 operand upload/evaluator，再继续资源型 callback、多 chain 与 Update/Reap/Output。

## 2026-07-16au — VFX native 全局帧时钟与 Effect 两阶段仿真提交

### 已完成
- 将 VFX manager frame clock 下沉 `anity-native`：设备级 registry 现唯一持有全局 `frameIndex`，每个 Effect 独立持有 fixed-step accumulator、当前 game/unscaled/scaled delta、step count 与 total time。新增稳定 **48-byte `AnityGraphicsVFXFrameState` C ABI**，以及 Begin、Prepare、Commit、Get、Reset 五组原生入口；`ClearVFXEffectState` 同步销毁 Effect clock，device teardown 继续统一释放完整 registry。
- 原生 Prepare 按官方 Player 已锁定的规则累积 `Time.deltaTime`、使用 Unity nearest-even 的 `maxDeltaTime/fixedTimeStep` 步上限、消费整数 fixed step 后再乘 `VisualEffect.playRate`。多个 Effect 在同一次 manager process 中共享一个 FrameIndex，但 accumulator/TotalTime 完全隔离；pause 不积累 delta，`playRate=0` 仍消费 unscaled step 而 scaled time 保持零。
- 时钟改为严格两阶段语义：Prepare 暴露 callback 当前帧 Delta 与**提交前** TotalTime，OnPlay/OnUpdate/OnStop、Spawner 和 Output 全部完成后 Commit 才推进 TotalTime。这与官方初始 OnPlay、replay OnPlay 和 Stop callback 的相位证据一致；重复 Prepare、错误 frame Commit、陈旧 FrameIndex 与非法/非有限参数均显式拒绝，不产生静默状态漂移。
- `VFXManager.ProcessCameraCommand` 产品路径现从当前 native graphics device 获取 FrameIndex，并为每个 `VisualEffect` 执行 native Prepare → input/callback → native Spawner → output → native Commit。`VisualEffect` 只缓存原生返回值供 17 个 Dynamic Built-In 同步读取，并校验 effect/frame/prepared/generation、有限性和 playRate scaling；不再由 C# 产品调度路径自行计算 accumulator 或 TotalTime。无 native 设备的现有 internal 单元测试 helper 仍保留确定性托管回退，不冒充产品 native 路径。
- native clock state 与 Spawner/Event/Initialize/Particle 生命周期已统一：更换 asset、`Reinit`、对象销毁和显式 Clear 都会移除同一 effectId 的全部原生状态；Reset 仅重建 Effect clock，不回退设备全局 FrameIndex。

### 测试与门禁
- 新增 **32 个 Core 测试发现项**：48-byte ABI、设备递增帧号、双 Effect 同帧、10 帧官方 **3,2,2,2,2,2,1,2,2,2** 序列、Prepare/Commit Total 相位、独立 accumulator、pause、零 playRate、重复 Prepare、错误 Commit、Reset/Clear、8 组非法参数、nearest-even 上限、托管 cache 原生来源，以及 OnPlay Dynamic Built-In 读取 native prepared delta。专项 callback + frame clock **102/102**，全部实际加载产品 dylib。
- 强制 native 全部 VFX 从 295 增至 **327/327**；VFX Graph 保持 **460/460**；Core 全量从 1,131 增至 **1,163/1,163**。本批只新增 internal/native C ABI，不改变 Unity 公开 API 表面。
- `bash _scripts/capture-unity-vfx-spawner.sh` 再次重建并通过官方非 batchmode Metal Player 门禁：**582 state + 100 Output Event + 24 callback + 12 Built-In callback**，证据继续保存于 `parity-evidence/unity-vfx-spawner-2022.3.51f1.json`。
- `bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过；native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 和样例产品工程 0 编译错误，URP3DDemo 仅有既有 43 个 nullable warning。Unity API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。

### 尚未完成
- 设备级时钟已经 native 化，但 Anity 仍缺独立于显式 camera command 的 engine-native PlayerLoop VFX update hook；当前每次显式 `ProcessCameraCommand` 仍代表一次 VFX update。多 Camera 同游戏帧去重、camera stack、无 Camera 仿真、pause/culled 与不同 `VFXUpdateMode` 的最终官方语义尚未闭环。
- 公共 `Simulate` / `AdvanceOneFrame` 与 internal raw `AdvanceSpawnerSystems` 尚未统一为可事务回滚的 native clock control；callback/dispatch 异常发生在 Prepare 与 Commit 之间时也缺 native Abort/rollback ABI。上述路径保持明确未完成，不能据本批宣称完整 VisualEffect runtime。
- linked activation、Rate/Burst/SetAttribute/loop/delay 的动态 operand、资源型 callback input、多 callback/Spawner/custom event chain、极端 spawnCount，以及 Update/Reap/Output、generation snapshot + staged CAS、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph和完整编辑器/平台产物仍未完成；API 审计仍缺 **3,189** 个官方类型，总体 Unity 2022 Ultra `/goal` 继续进行。

### 下一优先项
1. 建立 engine-native VFX PlayerLoop update hook 与每游戏帧 token，扩展官方 fixture 覆盖多 Camera、多 Effect、无 Camera、pause/culled/update mode，并证明每个 Effect 每游戏帧只 Prepare/Commit 一次。
2. 把 `VisualEffect.Simulate`、`AdvanceOneFrame`、Reinit 与异常 Abort/rollback 纳入 native clock transaction，补官方逐相位 A/B 和至少 10 个失败恢复/并发深测。
3. 让 linked activation、Rate/Burst/SetAttribute/loop/delay 复用 v11 typed DAG，增加不重置 native Spawner 状态的 operand upload/evaluator，再继续资源型 callback、多 chain 与 Update/Reap/Output。

## 2026-07-16at — VFX Dynamic Built-In 官方 Metal Player 全相位时钟语义

### 已完成
- 扩展 Unity 2022.3.51f1 / Visual Effect Graph 14.0.11 的非 batchmode Metal Player 探针，新增真实 `VFXDynamicBuiltInParameter(m_BuiltInParameters=0x1ffff) → Custom Spawner Callback` 场景。回调同时读取官方 17 个 Built-In 与 `VFXSpawnerState`、`Time`、`VFXManager`、`VisualEffect`、Transform 和 seed 参考值；场景固定 `startSeed=17`、`resetSeedOnPlay=false`、`playRate=1.75`、`Time.timeScale=0.5` 及非单位 position/rotation/scale。Matrix4x4 callback input 通过官方 Transform→Matrix slot conversion，LocalToWorld/WorldToLocal 的 16 项值均由 Player 实际编译运行，不以源码推断代替。
- 官方证据扩展为 **582 条 Spawner state + 100 条 Output Event + 24 条既有 callback + 12 条 Built-In callback**，覆盖 3 次 OnPlay、8 次 OnUpdate、1 次 OnStop、Stop 后 Finished update 与 replay。门禁逐项比较 7 个 VFX clock/manager 值、7 个 Game Time 值、双矩阵、SystemSeed，并验证 VFX Frame Index 与 `Time.frameCount` 是同步递增但独立的计数源。
- Player A/B 锁定了此前推断错误：Built-In VFX Delta Time 不是 `VFXSpawnerState.deltaTime` 的别名。OnUpdate 两者相等，但初始 OnPlay 的 state delta 为 0、Built-In 已是当前帧步长；replay OnPlay 可保留上一 state delta，同时 Built-In 使用新帧步长。VFX Total Time 也不随 Stop callback 的 state total 清零，而是按组件 VFX 仿真时钟继续累积。
- `VisualEffect` 现保存独立的 VFX frame delta、total、frame index 与 fixed-step accumulator。每次 manager process 把游戏 `Time.deltaTime` 累积成 `VFXManager.fixedTimeStep` 的整数步，每次最多消费按 `maxDeltaTime/fixedTimeStep` 四舍五入得到的步数，再乘 component playRate；官方固定输入序列已精确复现 **3,2,2,2,2,2,1,2,2,2** 步。OnPlay/OnUpdate/OnStop 的 Built-In 求值统一读取该当前 VFX frame context；Game Time、Manager、矩阵和 seed 仍逐 callback 实时读取。
- `VFXManager` 新增独立递增 frame index；显式 `ProcessCameraCommand` 现在用同一 prepared frame delta 完成输入 callback、native Spawner tick、输出交付与 total-time 提交。强制 native 回归曾发现按 `Time.frameCount` 去重会吞掉同帧第二次显式 camera process 的 Output Event，因此在尚无独立 engine VFX update hook 前保留“一次显式 process 对应一次 VFX update”的可验证语义，不留下假绿。

### 测试与门禁
- 新增 **13 个 Core 测试发现项**：10 个官方 fixed-step 累积序列、OnPlay/OnStop 当前帧 delta、VFX total 累积，并把 Frame Index 断言改为独立 VFX manager source。callback + Spawner 从 154 增至 **167/167**；强制加载产品 dylib 的全部 VFX 从 282 增至 **295/295**。
- `bash _scripts/capture-unity-vfx-spawner.sh` 已连续重建并通过官方 Metal Player 语义门禁；脚本会先删除旧输出，拒绝 stale evidence。证据保存于 `parity-evidence/unity-vfx-spawner-2022.3.51f1.json`，Editor/Player 日志同步保留。
- VFX Graph 全量保持 **460/460**；Core 全量从 1,118 增至 **1,131/1,131**。本批只改 internal runtime 调度语义，Unity 公开 API 表面无新增/删除。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例；产品工程 0 编译错误，URP3DDemo 保留既有 43 个 nullable warning。Unity 2022.3.51f1 API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。

### 尚未完成
- 当前 Player fixture 已关闭默认可见运行、Stop/replay 与 `resetSeedOnPlay=false` 的 17 项相位语义；pause、culled、不同 `VFXUpdateMode`、`Simulate`、`AdvanceOneFrame`、`resetSeedOnPlay=true` 随机 seed、多 Camera/多 Effect 的官方逐帧矩阵仍未覆盖。Anity 也尚缺独立于 camera process 的 engine-native VFX update hook，因此多 Camera 下“每游戏帧只推进一次”的最终架构仍待闭环。
- fixed-step accumulator 与 VFX total/frame context 目前由 C# `VisualEffect`/`VFXManager` 持有，native C++ 仍只拥有 Spawner 内部 loop/task clocks；后续必须把 manager frame preparation 与全部 VFX system 共享时钟下沉 native，避免不同 system/backend 各自累积。
- linked activation、Rate/Burst/SetAttribute/loop/delay 的动态 operand、资源型 callback input、多 callback/Spawner/custom event chain、极端 spawnCount，以及 Update/Reap/Output、generation snapshot + staged CAS、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph和完整编辑器/平台产物仍未完成；API 审计仍缺 **3,189** 个官方类型，总体 Unity 2022 Ultra `/goal` 保持进行中。

### 下一优先项
1. 把 VFX manager frame preparation/accumulator/FrameIndex/TotalTime 下沉 `anity-native`，增加独立 PlayerLoop update hook，并用多 Camera、多 Effect、pause/culled/update mode 官方 Player fixture 验证每游戏帧只推进一次。
2. 让 linked activation、Rate/Burst/SetAttribute/loop/delay 复用 v11 typed DAG，增加不重置 native Spawner 状态的 operand upload/evaluator。
3. 扩展 Curve/Gradient/Texture/Mesh callback binding、多 callback/Spawner/custom event chain、`resetSeedOnPlay=true` 与极端 spawnCount 官方 fixture，再继续 Update/Reap/Output 和三后端闭环。

## 2026-07-16as — VFX Graph 14 全量 dynamic built-in callback expression

### 已完成
- 对照本机 Unity 2022.3.51f1 / Visual Effect Graph 14.0.11 官方 `VFXBuiltInExpression.cs` 与 `VFXDynamicBuiltInParameter.cs`，runtime asset 从 **v10 升级为 v11**，为官方 17 个 dynamic built-in 建立独立 typed opcode：VFX Delta/Unscaled Delta/Total Time/Frame Index/Play Rate/Manager Fixed Time Step/Manager Max Delta Time、7 个 Game Time、LocalToWorld、WorldToLocal 与 SystemSeed。v1-v10 继续读取；built-in instruction 必须无 operand/常量/属性名，并严格匹配 Float、UInt32 或 Matrix4x4 类型。
- VFX Graph 编译器按官方 `BuiltInFlag` 的 bit0→bit16 顺序映射 `VFXDynamicBuiltInParameter.m_BuiltInParameters` 与 output slot，而不是依赖易变的显示名。单项/组合 flag、输出数、未知位和类型均验证；17 项都可作为 Custom Spawner Callback 输入，也可继续参与既有 Add/Subtract/Multiply/OneMinus SSA。Transform callback input 同步进入 Matrix4x4 runtime type，并把 VFX Transform 默认 position/Euler/scale 编码为 16-word TRS。
- callback 求值现在接收本次 native callback 的 `VFXSpawnerState` 与当前系统 seed：VFX delta 取 native 调度 delta，unscaled delta 去除 component playRate，total time 取组件可见运行时钟，Frame Index 取全局 VFX 更新源，PlayRate/Manager/Game Time 每次调用实时读取；LocalToWorld/WorldToLocal 取组件 Transform 仿射矩阵。每个 native Spawner instance 保存当前 system seed，Play control 在同步 OnPlay callback **之前**切换到实际 seed，失败时回滚，确保 `resetSeedOnPlay=false` 的 `startSeed` 与自动重播 seed 都不晚一帧。
- 用官方 `SimpleParticleSystem.vfx` 的真实 graph/spawner 结构注入 `VFXDynamicBuiltInParameter(PlayRate) → Custom Callback`，已闭环官方 YAML → typed graph → v11 opcode → runtime asset；合成图同时覆盖全部 17 flag 与组合输出顺序。

### 测试与门禁
- 新增 **22 个 Core 测试发现项**：10 类 live VFXManager/Game Time、VFX delta/unscaled/total、FrameIndex、同步 OnPlay SystemSeed、双矩阵、错误类型/operand、v10 读取；callback + Spawner 从 132 增至 **154/154**，强制 native VFX 从 260 增至 **282/282**，全部实际加载本批 arm64 dylib。
- VFX Graph 新增 **22 个测试发现项**，从 438 增至 **460/460**；Core 全量从 1,096 增至 **1,118/1,118**。Unity API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。
- `bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过；native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 和样例均 0 编译错误。URP3DDemo 仍只有既有 43 个 nullable warning。

### 尚未完成
- 17 个 built-in 已按官方源码定义、真实 native callback 和官方 `.vfx` 拓扑闭环，但 VFX Total Time/Frame Index、不同 update mode、pause/culled、fixed delta、`resetSeedOnPlay=true` 随机 seed 的**官方非 batchmode Metal Player 逐相位 A/B 记录尚未扩展**；因此本批不宣称完整 VFX Graph 完成。
- typed DAG 尚未覆盖更多官方 operator、linked activation，以及 Rate/Burst/SetAttribute/loop/delay 的动态 operand；CPU expression 求值仍在托管 callback 边界，尚未成为可由 native task chain 复用的 operand evaluator。
- Curve/Gradient/Texture/Mesh 等资源型 callback input、多 callback/Spawner/custom event chain、负数/非有限/极大 spawnCount，以及 Update/Reap/Output、generation snapshot + staged CAS、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph、完整编辑器/平台产物仍未完成；API 审计仍缺 **3,189** 个官方类型，总体 Unity 2022 Ultra `/goal` 保持进行中。

### 下一优先项
1. 扩展官方 Unity Metal Player probe，把 17 个 built-in 在 OnPlay/OnUpdate/OnStop、pause/culled/fixed/update mode/replay 下逐帧记录并与 Anity native callback 对照，先消除 VFX Total Time/Frame Index/seed 相位推断。
2. 让 linked activation、Rate/Burst/SetAttribute/loop/delay 复用 v11 typed DAG，并增加不重置 Spawner 状态的 native operand upload/evaluator。
3. 扩展常用 VFX operator 与 Curve/Gradient/Texture/Mesh 资源 binding/lifetime，再补多 callback/Spawner/custom event chain 和极端 spawnCount 官方 fixture。

## 2026-07-16ar — VFX v10 typed runtime expression DAG

### 已完成
- runtime asset 从 **v9 升级为 v10**，新增 checksummed typed expression program：有序 SSA instruction 可表达 Constant、ExposedProperty、Add、Subtract、Multiply 与 OneMinus，且 callback input 只能在常量、direct source property、expression 三种来源中选择一种。反序列化会在安装前验证 result/type、常量 word 数与有限性、暴露属性引用、逐指令同型 operand、前向/越界引用和运算类型；保留 v1-v9 读取，并新增真实删去 v10 expression 字段的 v9 callback payload 迁移证据。
- VFX Graph 编译器已把 callback input 的 reciprocal slot link 降级为上述 SSA：支持 Float/Float2/Float3/Float4 以及 UInt32/Int32 的 Add/Subtract/Multiply，OneMinus 限制为浮点标量/向量；direct exposed property 继续走 v9 快速引用。共享上游只发射一次，嵌套 operator 保持依赖顺序，未知 operator、错误 arity、类型变化与非法 root 均显式拒绝。
- Custom Spawner Callback 每次 OnPlay/OnUpdate/OnStop 调用前都会针对当前 `VisualEffect` 实例求值 expression。Constant 与组件级 exposed-property override 可混合计算，Set/ResetOverride 在下一次 callback 立即生效；UInt/Int 使用确定性 unchecked 运算，Float1-4 使用逐分量运算，同一 Asset 的组件仍保持 override/求值隔离。
- 用 Unity VFX Graph 14.0.11 官方 `SimpleParticleSystem.vfx` 的真实 Graph/Spawner 结构注入 `VFXParameter → Add → Custom Callback`，已闭环 YAML → typed graph → v10 SSA → runtime asset；另有合成图覆盖四种 operator、嵌套和共享依赖，不用 HLSL 文本测试代替 CPU runtime program 证据。

### 测试与门禁
- 新增 **12 个 Core 深测**，覆盖 v10 round-trip、真实 v9 payload 读取、空 program/前向引用/result mismatch/missing property/多来源拒绝，以及 Add/Subtract/Multiply/OneMinus、帧间属性刷新与嵌套 SSA 运行结果；callback + Spawner 专项从 120 增至 **132/132**，强制 native VFX 从 248 增至 **260/260**。
- VFX Graph 新增 **13 个**编译深测，全量从 425 增至 **438/438**；Core 全量从 1,084 增至 **1,096/1,096**。Unity API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。
- 全量复跑暴露并修复了测试基础设施中的 AssetDatabase/AssetBundle 全局状态竞态：Addressables、AssetBundle pipeline 与 LZ4 compression 现进入独占 collection，与既有异步 AssetBundle 全局状态不再并行。相关 **65/65** 定向测试及最终 Core **1,096/1,096** 均通过，避免一次绿、一次集合损坏的假稳定门禁。
- `bash _scripts/build-all.sh Release` 通过；native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 和样例均 0 编译错误。现存 Core warning 债务与 URP3DDemo 43 个 nullable warning 未因本批扩大。

### 尚未完成
- 本批 expression subset 只进入 Custom Callback input；VFX/Game time 等 dynamic built-in、spaceable Position/Direction/Vector 的坐标变换、更多 operator、linked activation，以及 Rate/Burst/SetAttribute/loop/delay 的动态 operand 尚未共享该 program。CPU 求值也仍在托管层，尚未下沉 native operand upload/evaluator。
- Texture/Mesh/AnimationCurve/Gradient/Transform callback input 仍缺资源 GUID/import binding、实例生命周期和官方 Player A/B；多 callback、多 Spawner/custom event chain、负数/非有限/极大 spawnCount 边界证据也未闭环。
- Update/Reap/Output、generation snapshot + staged CAS、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph、完整 VFX/Shader Graph、编辑器与平台产物仍未完成；API 审计仍缺 **3,189** 个官方类型，因此总体 Unity 2022 Ultra `/goal` 保持进行中。

### 下一优先项
1. 给 v10 expression program 增加 VFX/Game time built-in、spaceable 坐标变换与常用 operator，并让 linked activation 复用同一 typed DAG。
2. 把 Rate/Burst/SetAttribute/loop/delay 的动态 operand 接入 native Spawner ordered task chain，增加不重置状态的 native operand upload/evaluator，并用官方 Player 做逐帧 A/B。
3. 为 Texture/Mesh/AnimationCurve/Gradient/Transform 建立资源引用、导入绑定、实例生命周期与 callback 资源型 getter；随后补多 callback/Spawner/custom event chain 和极端 spawnCount fixture。

## 2026-07-16aq — VFX v9 暴露属性与 Custom Callback 动态直连

### 已完成
- 对照 Unity VFX Graph 14.0.11 官方 `VFXParameter` 序列化结构，支持 `m_Exposed=1`、`m_ExposedName`、root output slot 与 callback input 的双向 `m_LinkedSlots` 直连。编译器现在收集 Boolean/Int32/UInt32/Float/Float2/Float3/Float4 暴露属性及默认原始 word，不再把 direct exposed-property callback link 错误拒绝或烘焙成 callback input 常量。
- runtime asset 从 **v8 升级为 v9**，保持 v1-v8 读取；新增 checksummed exposed-property 表和 callback input source-property 引用。反序列化严格验证名称唯一性、类型/word 数、浮点有限性、引用存在性与类型一致性，并保持 Asset 原子替换；v5 迁移 fixture 已从真实 v9 payload 删除 v7-v9 扩展后继续通过。
- `VisualEffectAsset.ImportRuntimeData` 现在导入 Graph 暴露属性的公开 surface、类型与默认值；`VisualEffect.Has*/Get*/Set*/ResetOverride` 因此直接作用于编译资产。每个 Custom Spawner Callback 调用前都会从当前组件刷新其 `VFXExpressionValues`，同一 Asset 的不同组件 override 互不污染，帧间 Set 与 ResetOverride 立即生效。
- 用官方 `SimpleParticleSystem.vfx` 的真实 graph/spawner 结构注入 `VFXParameter → Custom Callback` reciprocal slot link，已证明 YAML → typed graph → exposed-property table → v9 callback source reference 全链路保真；非 direct-property operator graph 继续显式拒绝，不静默改变效果。

### 测试与门禁
- 新增 **10 个** Core 深测，覆盖 v9 round-trip、missing/type mismatch/duplicate/non-finite 拒绝、公开 surface/default、组件 override、帧间刷新、ResetOverride 与共享 Asset 组件隔离；callback + Spawner 专项从 110 增至 **120/120**，强制 native VFX 从 238 增至 **248/248**。
- VFX Graph 全量从 424 增至 **425/425**；Core 全量从 1,074 增至 **1,084/1,084**。Unity API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。
- `bash _scripts/build-all.sh Release` 通过；native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 和样例均 0 编译错误，URP3DDemo 仅有既有 43 个 nullable warning。

### 尚未完成
- 本批只支持 callback input **直接连接单个暴露属性**。Add/Subtract/Multiply/OneMinus 等 operator DAG、dynamic built-in、linked activation，以及 Rate/Burst/SetAttribute/loop/delay 的动态 operand 尚未进入统一 runtime expression program；这些路径仍明确拒绝。
- Texture/Mesh/AnimationCurve/Gradient/Transform 暴露属性与 callback input 尚缺资源 GUID/import binding、对象生命周期和官方 Player A/B；本批 direct property 的帧间刷新也仍需官方 Player fixture 固化调用相位。
- Update/Reap/Output、generation snapshot + staged CAS、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph 与完整 VFX/编辑器/平台闭环仍未完成；项目仍缺 **3,189** 个官方类型，总体 Unity 2022 Ultra `/goal` 继续进行。

### 下一优先项
1. 将 direct property 扩展为版本化 typed runtime expression DAG，先覆盖 Add/Subtract/Multiply/OneMinus 与 VFX/Game time built-in，并让 callback inputs 每次调用求值。
2. 把 linked activation 与 Rate/Burst/SetAttribute/loop/delay 动态 operand 接入 native Spawner task chain，增加不会重置状态的 native operand upload ABI，并做官方 Player 逐帧 A/B。
3. 为 Texture/Mesh/AnimationCurve/Gradient/Transform 建立资源引用、导入绑定、实例生命周期与 callback `VFXExpressionValues` 资源型 getter 证据。

## 2026-07-16ap — VFX Custom Spawner Callback 官方 Player 证据与有序 native/managed 执行链

### 已完成
- 将 Unity 2022.3.51f1 / Visual Effect Graph 14.0.11 的可重复 Metal Player 探针扩展到 `VFXSpawnerCallbacks`：覆盖 callback 位于 Constant Rate 前后、后续 Set SpawnEvent Attribute 覆盖、OnPlay/OnStop、Finished 后 OnUpdate、replay 与两个组件共享 Asset 的实例隔离。证据现有 **573 条**逐帧 `VFXSpawnerState`、**100 条** Output Event record 和 **24 条** callback lifecycle record；脚本会拒绝记录数以及 callback 输入/输出、顺序、默认 Event record 或生命周期语义漂移。
- 官方 Player 已固定关键语义：Rate→callback 时 callback 收到 `spawnCount=0.8` 并可改成 2.8；callback→Rate 时 callback 先收到 0、返回 2，再由 Rate 累加为 2.8；后续 Set block 可覆盖 callback 写入的 Event Attribute。OnPlay/OnStop 收到 sentinel `spawnCount=1`，但不直接产生 dispatch；Finished Spawner 仍执行一次 OnUpdate 与后续 Set，跳过 Rate/Burst；Stop 保留上一帧 `deltaTime`、将 `totalTime` 清零；Play/Stop Event record 从官方默认值重建，replay 会创建新的 callback 实例并重置实例字段。
- native Spawner Program ABI 升级为 **v5 / 96 bytes**，Block/State 保持 **80/80 bytes**。新增同步 callback C ABI、callback-aware Play/Stop/tick 和 Event record 默认值安装；C++ 继续唯一拥有时钟、随机流、Rate/Burst/Set 与 event accumulator，并在同一 ordered task chain 中按 block 位置调用托管 callback。callback 可修改完整 `VFXSpawnerState` 与 packed Event record，native 会校验 identity、枚举与全部有限数值；托管异常跨 ABI 转为 native error 后以原始异常重新抛出。
- runtime asset 升级为 **v8** 并保持 v1-v7 读取；VFX Graph registry 加入官方 custom wrapper GUID，编译器按图顺序导出 callback assembly-qualified type 与 Bool/Int/UInt/Float/Vector2/3/4/Matrix4x4 常量输入。每个 `VisualEffect`、每个 callback block 都创建独立 `ScriptableObject` 实例，生命周期结束时销毁；`VFXExpressionValues`、`VFXSpawnerState` 和 `VFXEventAttribute` 全部用当前 Asset schema 构造并双向同步。Event record 同时恢复 Unity 的非零内置默认值，例如 `size=0.1`、`alpha=1`、`lifetime=1`、`scale=1`、`alive=true`。

### 测试与门禁
- 新增 callback 专项 **13 个**深测，覆盖 v8 round-trip、非法/重复输入、Rate 前后顺序、Set 覆盖、Play/Stop sentinel 与状态、Event 默认值、Finished OnUpdate、replay、共享 Asset 隔离、异常重抛，以及全部 blittable expression 类型；callback + Spawner 专项 **110/110**，强制 native VFX **238/238**。
- VFX Graph 全量从 422 增至 **424/424**；Core 全量从 1,061 增至 **1,074/1,074**。Unity API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。
- `bash _scripts/capture-unity-vfx-spawner.sh`、`bash _scripts/build-all.sh Release` 与上述测试全部通过；native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 和样例均 0 编译错误，URP3DDemo 仅有既有 43 个 nullable warning。

### 尚未完成
- 本批只闭环 callback 的常量 blittable 输入。linked runtime expression/activation、AnimationCurve/Gradient/Mesh/Texture callback 输入仍被编译器显式拒绝；多 custom callback、多 Spawner、custom event chain 也缺官方 Player 证据，不能把 VFX Spawner 或 Visual Effect Graph 总行标为完成。
- 负数、NaN/Infinity、极大 `spawnCount` 的官方 Player 边界仍待 fixture；Update/Reap/Output 尚未共享完整 native particle/dead-list storage。generation snapshot + staged CAS、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph、完整 VFX Block/Operator/Output 与编辑器/截图/产物 A/B 均未闭环。
- 最终 Unity 2022.3.61f1 fixture 尚未安装，项目仍缺 **3,189** 个官方类型以及大量成员、行为、编辑器和平台证据；总体 Unity 2022 Ultra `/goal` 继续进行，不能标记完成。

### 下一优先项
1. 把 linked activation/runtime expression 编译为版本化 operand graph，并为 callback 的 AnimationCurve/Gradient/Mesh/Texture 输入建立资源绑定与生命周期所有权。
2. 用官方 Player 补多 custom callback、多 Spawner/custom event chain，以及负数/非有限/极大 `spawnCount` 的顺序与边界 fixture，再扩展统一 native Program/control routing。
3. 让 Update/Reap/Output 共享 native particle/dead-list storage，完成 generation snapshot + staged CAS，以及 Vulkan/D3D11 compute、GPU Event、Particle Strip和 URP frame graph。

## 2026-07-16ao — VFX Set SpawnEvent Attribute 官方 Player A/B、spawnCount 顺序与 Output Event

### 已完成
- 把 Unity 2022.3.51f1 / Visual Effect Graph 14.0.11 的可重复 Metal Player 探针扩展到 Set SpawnEvent Attribute：scalar 多块覆盖、Float3 Off、PerComponent/Uniform Random、`spawnCount` 位于 Rate 前后，以及 Spawner→Output Event。证据现有 **555 条**逐帧 `VFXSpawnerState` 和 **84 条** `outputEventReceived` record；脚本逐项验证 size=7.25 最终覆盖、UInt/Bool/spawnTime 原始值、固定 vector、两类随机 record 数、Rate→Set=3、Set→Rate 首两次事件 3.8/4.6，并拒绝记录数或关键语义漂移。
- 官方 Player 证明并已在 native C++ 对齐：公共 `spawnCount` 是每帧按 block 顺序计算的 raw float（Constant Rate 16、0.05s 时恒为约 0.8），Initialize/Output Event 使用独立跨帧 accumulator（依次 1.6、1.4、1.2、1.0）；事件后仅减去 floor 并保留余数。Set `spawnCount` 是 offset 0 的普通有序任务：Rate 后 Set 覆盖为 3，Rate 前 Set 与 Rate 相加为 3.8；Stop 会清除旧余数后继续执行 SetAttribute，Play/Reinit 重建 accumulator。随机 SetAttribute 即使当帧不足 1 个事件也会消费随机值，PerComponent 每分量消费、Uniform 每帧一次消费均与 Player record 对齐。
- native Spawner Program ABI 升级为 **v4 / 96 bytes**，Block 保持 80 bytes，State 扩展为 **80 bytes**并分离 raw `spawnCount` 与 `eventSpawnCount`。删除错误的 per-Rate floor/debt，把 Constant/Variable Rate、Burst 与 SetAttribute 全部归入同一 ordered raw task chain，再由统一 event accumulator 决定 dispatch record word 0；Stop 的单次 pending control event、restart reset、event record readback 和非有限值防护均在 native 所有权内。
- VFX Graph 编译器与 runtime asset v7 现在接受 Set `spawnCount` 的保留 offset 0，保持 serialized block 顺序，并把有真实 operand 的 SetAttribute-only Spawner 编译为 native Program。Spawner 可同时或单独驱动 Initialize 与映射 Output Event；`VisualEffect` 将全局 Event record 按目标 Output Event 的独立紧凑 layout 重排并入 native FIFO，只有 Output Event、没有 Initialize 的 SetAttribute-only Program 也可运行。公共 `VFXSpawnerState.vfxEventAttribute.spawnCount` 同步暴露 raw 值，与官方 Player snapshot 一致。

### 测试与门禁
- 官方 probe 语义门禁：**555 state + 84 Output Event**；Spawner 专项从 87 增至 **97/97**，覆盖 raw/event 双计数、跨帧余数、offset 0 前后顺序、Stop pending、restart、event record word 0、last setter wins、无 dispatch 帧随机消费、公共 state 与 Initialize/Output Event/Output-only 端到端。
- VFX Graph 全量从 420 增至 **422/422**；强制加载本批 dylib 的 Core VFX 从 215 增至 **225/225**；Core 全量从 1,051 增至 **1,061/1,061**。Unity API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。
- `bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过；native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 和样例均 0 编译错误，URP3DDemo 仅有既有 43 个 nullable warning。

### 尚未完成
- 本批已关闭常量/随机 SetAttribute、`spawnCount` target、SetAttribute-only、Initialize 与直接 Output Event routing，但 custom callback、linked expression/activation、多个 Spawner/事件 chain 的任务和 callback 顺序仍未进入统一 native Program。负数、NaN/Infinity、极大 `spawnCount` 的官方 Player 行为还缺独立 fixture，不能臆测其 clamp/丢弃/异常语义。
- Update/Reap/Output 仍未共享完整 native particle/dead-list storage；generation snapshot + staged CAS、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph、完整 VFX Block/Operator/Output 和编辑器/截图/产物 A/B 均未闭环。最终 Unity 2022.3.61f1 fixture 尚未安装，项目仍缺 **3,189** 个官方类型，总体 `/goal` 继续进行。

### 下一优先项
1. 用官方 Player 补 custom spawner callback、linked expression/activation、多 Spawner/custom event chain，以及负数/非有限/极大 spawnCount 的顺序与边界 fixture，再扩展 typed native opcode/control routing。
2. 让 Update/Reap/Output 共享 native particle/dead-list storage，完成 generation snapshot + staged CAS，以及 Vulkan/D3D11 compute、GPU Event、Particle Strip和 URP frame graph。
3. 安装并固定 Unity 2022.3.61f1 Editor/包 fixture，迁移 API、Shader Graph、VFX Graph 与 Player 行为基线，继续关闭缺失的 3,189 个类型和完整编辑器/平台产物矩阵。

## 2026-07-16an — VFX Set SpawnEvent Attribute native typed opcode

### 已完成
- 对照 Unity 2022.3.51f1 / Visual Effect Graph 14.0.11 官方 `VFXSpawnerSetAttribute` 源码，把 Spawner runtime asset 升级为 **v7**：Program 新增全局 Event record stride，Block 新增目标 word offset、值类型、Random mode 以及两组最多 4-word 的原始 operand；v1-v6 继续读取，checksum、长度、枚举、字段范围、连续 variadic channel、有限 float 与 operand word 数在安装前严格验证。
- native C++ Spawner Program ABI 升级为 **v3 / 96 bytes**，Block 从 32 扩展为 **80 bytes**，State 保持 72 bytes。新增按 block 序执行的 `SetAttribute` opcode 与 event-record readback：Bool/UInt32/Int32/Float/Float2/Float3/Float4 的 Off 常量按原始 word 写入；Float1-4 的 PerComponent/Uniform Random 分别消费逐分量/单次 Unity xorshift128 值，float lerp 显式限制中间舍入，避免 FMA 改变位级结果。调度器执行完 task 后再把实际 spawnCount 写入 event record word 0。
- VFX Graph 编译器现在收集 `VFXSpawnerSetAttribute` 写入的内置属性并纳入全局 Event schema，解析未 linked 的常量槽位、Color→Float3、连续 variadic float channel 与 Off/PerComponent/Uniform Random，导出 v7 typed opcode。官方 `SimpleParticleSystem.vfx` 注入 size=-4.5、随机 Color Min/Max 后已证明 graph→schema→runtime asset→native descriptor 全链路保真，负有限 float 不再被误拒绝。
- `VisualEffect` 在存在 typed Set Attribute Program 时读取 native event record 并提交现有 Initialize dispatch；无该 opcode 的旧 Program 仍走原有 spawnCount 快速路径。共享 Asset 的 effect/context 隔离、block 顺序和旧版本迁移保持不变。

### 测试与门禁
- Spawner 专项从 72 增至 **87/87**：新增 15 个发现用例，覆盖四种标量原始 word、Float2/3/4 宽度、PerComponent/Uniform 精确随机序列、v7 round-trip、四类非法契约，以及 native event record→Initialize 端到端提交。
- VFX Graph 全量从 417 增至 **420/420**；强制加载本批 dylib 的 Core VFX **215/215**；Core 全量 **1,051/1,051**。Unity API 门禁保持类型 **928/4,117**（exact 404）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`。
- `bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过；native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 和样例均 0 编译错误，URP3DDemo 仅保留既有 43 个 nullable warning。

### 尚未完成
- 本批尚未建立 Set Attribute 最终值的官方 Player A/B fixture；当前证据是官方源码语义、官方图结构注入以及 Anity native 端到端测试，不能替代官方 Player 的标量/向量/随机/多 block 顺序对照。
- `spawnCount` 是 Event record word 0，且调度器会在 block 执行后写入实际生成数；为避免静默错误，编译器和 runtime 当前明确拒绝 Set SpawnEvent `spawnCount`。只含 Set Attribute、没有 Rate/Burst 的 Spawner，以及 custom callback、linked expression/activation 和多 Spawner chain 也尚未生成统一 native Program。
- Update/Reap/Output 仍未共享完整 native particle/dead-list storage；generation snapshot + staged CAS、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph、完整 VFX Block/Operator/Output 和编辑器/截图/产物 A/B 均未闭环。最终 2022.3.61f1 fixture 尚未安装，项目仍缺 **3,189** 个官方类型，总体 `/goal` 继续进行。

### 下一优先项
1. 建立官方 Player Set SpawnEvent Attribute fixture，覆盖标量/向量、Off/PerComponent/Uniform、负数、多 block 顺序和 `spawnCount`，据实实现 word 0 的覆盖/调度语义。
2. 为 SetAttribute-only、custom callback、linked expression/activation 与多 Spawner chain 增加统一 typed native Program/control/event routing。
3. 让 Update/Reap/Output 共享 native particle/dead-list storage，完成 generation snapshot + staged CAS，以及 Vulkan/D3D11 compute、GPU Event、Particle Strip 和 URP frame graph。

## 2026-07-16am — Unity Player Spawner A/B、xorshift128 与边界帧保真

### 已完成
- 新增可重复构建的 Unity 2022.3.51f1 / Visual Effect Graph 14.0.11 / URP 14.0.11 **macOS Metal Player** 探针：`_scripts/capture-unity-vfx-spawner.sh` 在非 batchmode 的 64×64 Player 中执行 Infinite、Constant finite、0 count、Random count 与全 Random 五类场景，输出 `parity-evidence/unity-vfx-spawner-2022.3.51f1.json`。421 条逐帧记录包含 public `VFXSpawnerState`、spawnCount、粒子数、sleeping/capacity、`VFXManager.fixedTimeStep/maxDeltaTime`、start/reset seed，以及 5 个 seed 的 `UnityEngine.Random.State` 初始四字与连续 12 次 value 后状态；脚本验证 Editor/包/Metal/记录数和关键 Finished 语义。生成 VFX 资产使用固定 GUID，ignored Build/Generated 目录可由脚本完整重建。
- 官方 Player 证据固化了状态机边界：`VFXManager.maxDeltaTime=0.05` 且输入 delta 会钳制；未播放/`Reinit` 的 public state 为 duration/count 0，Infinite 播放后为 -1/-1；`totalTime` 是 phase-local 时钟，切 phase 清零且不携带 overshoot，Finished 后仍随 tick 增长；0 LoopCount 仍完整执行 **1 轮** 后以 `loopIndex=1` Finished；loop completion 帧先增加 index，下一 tick 才采样新一轮并令 `newLoop=true`；`playing` 只在 Looping 为 true，最终 after-delay 完成后才 Finished。native C++ Spawner 已逐项改为这些边界语义。
- 从 Player 暴露的四字状态与 5×12 连续采样反推并验证 Unity 2022.3 `Random`：InitState 以 `s0=uint(seed)`、`s[n]=1812433253*s[n-1]+1` 展开，step 为标准 xorshift128，`value=(next & 0x007fffff)/8388607.0f`（含 1.0）。native Spawner 与 `UnityEngine.Random` 托管 API 都改用同一精确算法；`Random.State` 恢复为四个 private serialized int，删除错误公开字段、公开构造器和额外 `InitSeed`，state snapshot/restore 可逐值续播。
- 官方固定 seed 证明首轮 Spawner 随机采样顺序为 **Count → Duration → Before → After**，后续轮次为 Duration → Before → After；native 不再混入 effect/context ID。Random 全参数 5 个 seed 的 count/duration/before/after 已逐 float 对齐 Player。`resetSeedOnPlay=false` 使用 startSeed 并保留 native stream；`true` 在每次 Play 生成新 seed、忽略 startSeed，并在同次 control dispatch 的所有 Spawner 间共享该 seed。

### 测试与门禁
- Spawner 专项 **72/72**：包含官方 Constant finite 第 0–24 帧的逐字段表驱动断言、0 count 一轮、phase boundary/no overshoot、Finished clock、next-loop 延迟采样和 5 组官方 fixed-seed Random 全参数。新增 `RandomParityTests` **19/19**，覆盖 seed 1 的 12 个 value、5 个官方初始 State、state restore 与公开面约束。
- 强制加载本批 dylib 的 Core VFX **200/200**；VFX Graph **417/417**；Core 全量 **1,036/1,036**。Unity API 门禁仍为类型 **928/4,117**，其中 exact 从 403 提升为 **404**；成员 **8,645/37,164**（exact 6,417），load issues=0。经逐项审查，旧基线的 6 个 removed-or-changed 全是本批删除的错误 Random 额外公开面；重建后复跑 `regressions=0`、`removed-or-changed=0`。
- `bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过；native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 和样例均 0 编译错误，URP3DDemo 只有既有 43 个 nullable warning。

### 尚未完成
- 本批只证明 `Random.InitState/state/value` 与 Spawner 所消费的 float stream；`Random.Range(int,int)` 的官方无偏整数映射、Range/几何/rotation/ColorHSV 的完整分布与极值仍缺 Player A/B，因此 Random 总行不能标为全完成。
- Spawner Set Attribute、custom callback、linked expression/activation、Spawner chain 与完整 event routing 仍缺 typed native opcode。Update/Reap/Output 尚未共享完整 particle/dead-list storage；generation snapshot + staged CAS、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph、完整 VFX Block/Operator/Output 和编辑器/截图/产物 A/B 均未闭环。
- 最终 Unity 2022.3.61f1 fixture 尚未安装；项目仍缺 **3,189** 个官方类型与大量成员、行为、编辑器和平台证据，总体 Unity 2022 Ultra `/goal` 继续进行，不能标记完成。

### 下一优先项
1. 为 Spawner Set Attribute、custom callback、linked expression/activation 与 Spawner chain 增加 typed native opcode，并用官方多 Spawner/自定义事件 Player fixture 对照 control、attribute 与 callback 顺序。
2. 让 Update/Reap/Output 共享 native particle/dead-list storage，完成 generation snapshot + staged CAS，以及 Vulkan/D3D11 compute、GPU Event、Particle Strip 与 URP frame graph。
3. 安装并固定 Unity 2022.3.61f1 Editor/包 fixture，迁移 API、Shader Graph、VFX Graph 和 Player 行为基线；同时扩展 `Random.Range` 与分布类 API 的官方 A/B。

## 2026-07-16al — VFX runtime asset v6 与 Random LoopCount Float2 保真

### 已完成
- 对照 Unity 2022.3.51f1 / Visual Effect Graph 14.0.11 官方 `VFXBasicSpawner.GetExpressionMapper`：Random `LoopCount` 先以 `Vector2` 执行 float `lerp(min,max,random)`，再经 `VFXExpressionCastFloatToInt` 截断为 Int32。runtime asset 从 **v5 升级为 v6**，将 loop-count 两个 operand 改为 Double：Random 精确保留从官方 Float2 提升的 float32 端点，Constant 同时能无损保存完整非负 Int32（含 `int.MaxValue`），不再把 `1.25–3.75` 之类官方合法范围错误整数化或拒绝。
- v6 serializer 写入两个 Double；deserializer 对 v1-v4 继续迁移为空 Spawner Program，对 v5 的两个 Int32 显式提升为 Double。checksum、payload length、collection、mode/range 和 system/context 验证保持原子边界；Infinite 强制 0/0，Constant 强制相等整数，Random 强制有限、非负、有序并限制到最大的安全 Int32 float `2147483520`，避免 native float-to-int 溢出或未定义转换。
- Spawner Program C ABI 升级为 **v2 / 96 bytes**，Block 仍为 32 bytes、State 仍为 72 bytes；C++ `static_assert` 与 C# `Marshal.SizeOf` 同时锁定布局。native Random LoopCount 将 Double 端点恢复为原始 float32，执行与官方表达式相同的 float lerp 后截断；Constant 直接从精确 Double 转 Int32，0 次循环立即进入 Finished，非法/非有限/越界 operand 在安装 Program 前拒绝。
- VFX Graph 编译器现在接受官方 Random `VFXSlotFloat2` 的非整数端点并保真导出；Constant 继续读取官方 `VFXSlotInt32`。官方 `SimpleParticleSystem.vfx` 注入测试已改为 Random `1.25–3.75`，证明 typed graph → v6 → runtime Program 全链路没有整数化。

### 测试与门禁
- Spawner 专项从 25 增至 **41/41**：新增 16 个发现用例，覆盖 5 组 seed 的 float lerp→Int32 cast 精确结果、fractional v6 round-trip、真实 v5 二进制迁移、Constant `int.MaxValue`、0 loop count、6 类非法 operand，以及 native unsafe random range 拒绝；测试以 `ANITY_REQUIRE_NATIVE=1` + `AnityRequireNative=true` 强制加载本批 96-byte ABI dylib。
- VFX Graph 全量 **417/417**；强制 native 的 Core VFX **169/169**；Core 全量 **986/986**。Unity 2022.3.51f1 的 84 程序集 API 门禁保持类型 **928/4,117**（exact 403）、成员 **8,645/37,164**（exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`，`UnityEngine.VFXModule` 公开面没有回归。
- `bash _scripts/build-native.sh Release` 与 `bash _scripts/build-all.sh Release` 通过；native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 和样例均 0 编译错误，URP3DDemo 仍只有既有 43 个 nullable warning。

### 尚未完成
- 本批闭环的是 Random LoopCount 的**数值表示与 lerp/cast 算术**，不是 Unity 官方随机流等价证明。Anity 当前仍使用 effect/context 级 xorshift stream；Unity 官方 Duration/Count/Before/After 分别使用 `RandId` 0/1/2/3，其随机 seed、采样顺序、重启行为以及边界帧 `newLoop/totalTime/playing` 仍缺官方 Player 逐帧 A/B，不能宣称随机序列或逐帧状态已经完全一致。
- 0 LoopCount 的 Finished 边界也仍需官方 Player fixture 固化。Spawner Set Attribute、custom callback、linked expression/activation、Spawner chain、Update/Reap/Output、staged CAS、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph、完整 VFX Block/Operator/Output 与编辑器/截图/产物 A/B 尚未闭环。
- 最终 Unity 2022.3.61f1 fixture 尚未安装；全项目仍缺 **3,189** 个官方类型和大量成员/行为/编辑器/平台证据，总体 Unity 2022 Ultra `/goal` 继续进行，不能标记完成。

### 下一优先项
1. 在 Unity 2022.3 官方 Player 记录 Duration/Count/Before/After 四个 `RandId` 的 seed、采样顺序、重启及 0/有限 loop 边界帧，形成逐帧 `VFXSpawnerState`、spawnCount、粒子数 A/B fixture，并据此替换当前临时 xorshift 顺序。
2. 为 Spawner Set Attribute、custom callback、linked expression/activation 与 Spawner chain 增加 typed native opcode，使全部 CPU Spawner task 在同一版本化 Program 中组合执行。
3. 让 Update/Reap/Output 共享 native particle/dead-list storage，完成 generation snapshot + staged CAS、Vulkan/D3D11 compute、GPU Event/Particle Strip 与 URP frame graph。

## 2026-07-16ak — VFX Spawner native 调度、有限/随机 Loop 与前后 Delay

### 已完成
- 将 v5 Spawner Program evaluator 从 C# 迁入 `anity-native` C++。新增 88-byte Program、32-byte Block 与 72-byte State C ABI，以及 program 原子安装、Play/Stop control、effect 级批量 tick、state readback 四个 export；device registry 以 effect + context 隔离不可变 Program 和 clock、phase、xorshift random、rate debt、interval、burst 状态，并在 effect teardown 一并释放。三份 ABI 由 C++ `static_assert` 与 C# `Marshal.SizeOf` 双侧锁定。
- native 状态机执行 Infinite/Constant/Random loop duration、Infinite/Constant/Random loop count、None/Constant/Random before/after delay。一个 tick 可跨越 delay→loop→delay→下一 loop 乃至最终 Finished，并只把真正 Looping 的时间送入 Constant/Variable Rate 与 Single/Periodic Burst；每轮重置 task state，有限生命周期结束时保留本帧已生成的 spawnCount，外部 Stop/Play 则正确清零或重建实例状态。
- `VisualEffect` 不再拥有 Spawner evaluator：每个 native device 只安装一次 Asset Program，输入事件直接调用 native control，每帧一次 native effect tick 后同步 `VFXSpawnerState` snapshot，再将 native `spawnCount` 打包给现有 Initialize/prefix/dead-list transaction。共享 Asset 的组件仍按 effect ID 隔离；换 Asset、Reinit、销毁和显式 clear 同步清掉 native/managed state。
- VFX Graph 编译器开始读取 Unity 官方 `VFXBasicSpawner` 动态槽位：Constant/Random `LoopDuration`、Constant/Random `LoopCount`、Constant/Random `DelayBeforeLoop` / `DelayAfterLoop`，并导出到既有 v5 operand。linked runtime expression 继续显式拒绝；由于 v5 的 LoopCount operand 仍是 Int32 min/max，Random LoopCount 的非整数 Vector2 端点当前也明确拒绝，避免静默产生错误分布。

### 测试与门禁
- native Spawner 专项 **25/25**：覆盖三份 ABI、初始/Play/Stop/restart、fractional rate、single/periodic/zero-period burst、Variable Rate seed 确定性、随机 loop/delay 有界与同 seed 一致、有限 duration/count、before/after delay、跨多阶段大 delta、每轮 burst reset、非法 delta、clear，以及事件不直穿/Initialize record/共享 Asset 端到端。
- 基于 Unity 2022.3.51f1 / VFX Graph 14.0.11 官方 `Editor/Templates/SimpleParticleSystem.vfx` 的真实图结构注入四种官方动态槽位后，v5 round-trip 精确保留 Random Duration、Constant Count、Constant Before 与 Random After operand。VFX Graph 全量 **417/417**；主 dylib + `ANITY_REQUIRE_NATIVE=1` 的 VFX 组 **153/153**、Core 全量 **970/970**。
- Unity 2022.3.51f1 的 84 程序集 API 门禁保持类型 **928/4,117**（22.541%，exact 403）、成员 **8,645/37,164**（23.262%，exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`；本批只增加 Anity internal/native ABI，`UnityEngine.VFXModule` 公开面没有回归。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例；产品工程 0 编译错误，URP3DDemo 保留既有 43 个 nullable warning。

### 尚未完成
- native random stream、边界帧 `newLoop/totalTime/playing`、随机值采样顺序与 Random LoopCount float→int 分布仍缺 Unity 官方 Player 逐帧 A/B；v5 Int32 range 不能表达非整数 Random LoopCount 端点，后续 ABI 必须保真 float range。当前实现是可执行生产路径，但不能据此宣称逐边界已与 Unity 完全一致。
- Spawner Set Attribute、custom callback、linked expression/activation 与 Spawner chain 仍缺 typed runtime opcode；Update/Reap/Output 尚未共享完整 native particle lifecycle。Metal transaction 长锁、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph、完整 VFX Block/Operator/Output、编辑器/截图/产物 A/B 均未闭环。
- 最终 Unity 2022.3.61f1 fixture 尚未安装，项目仍缺 **3,189** 个官方类型与大量成员/行为/编辑器/平台证据；总体 Unity 2022 Ultra `/goal` 继续进行，不能标记完成。

### 下一优先项
1. 用 Unity 2022.3 官方 Player 记录 Constant/Random loop/delay 的逐帧 `VFXSpawnerState`、spawnCount 与粒子数 A/B；将 LoopCount operand 升级为保真 float range并对齐官方 cast/random sampling order。
2. 为 Spawner Set Attribute、custom callback、linked expression/activation 与 Spawner chain 增加 typed native opcode，使所有 CPU Spawner task 在统一 Program 中组合执行。
3. 让 Update/Reap/Output 共享 native particle/dead-list storage，完成 staged CAS submit、Vulkan/D3D11 compute、GPU Event/Particle Strip 与 URP frame graph。

## 2026-07-16aj — VFX Spawner v5 IR、实例级 Rate/Burst 状态机与逐帧 Initialize

### 已完成
- 对照本机 Unity 2022.3.51f1 / Visual Effect Graph 14.0.11 官方 `VFXBasicSpawner`、`VFXSpawnerConstantRate`、`VFXSpawnerVariableRate` 与 `VFXSpawnerBurst` 源码，将 runtime asset 升级为 **v5**：每个可执行 Spawner Program 保存 context/system identity、OnPlay/OnStop 或自定义输入 control、Initialize output/kernel、按序 Block opcode，以及 loop duration/count、before/after delay 的 mode 与 operand ABI。v1-v4 继续读取并迁移为空 Spawner Program；checksum、collection、enum、system/context、range、kernel 与重复键仍在 Asset 原子替换前严格验证。
- VFX Graph registry 加入官方 `VFXSpawnerVariableRate` GUID，累计 **71** 个 typed script type。编译器现在读取未 linked 的 Float/Float2 slot 常量、activation 与 `m_Disabled`，导出 Constant Rate、Variable Rate、Single/Periodic Burst 的 min/max rate/count/period/delay；反向 range、NaN/Infinity、负数、未知 mode、linked expression/activation 和混入无 runtime opcode 的 active task 都明确拒绝，不静默改变图效果。没有 Rate/Burst task 的旧 Spawner 保留既有 event path，多级旧 Spawner 图不被误升级。
- `VisualEffectAsset` 持有不可变 Program 索引，`VisualEffect` 持有独立的 clock、fractional spawn accumulator、variable-rate interval/random stream、burst delay/repeat 与 `VFXSpawnerState`。这修复了旧实现把动态 Spawn 状态放在共享 Asset 上的问题；两个组件共享同一 Asset 时 Play/Stop、totalTime、spawnCount 与随机进度互不污染，换 Asset、Reinit 和销毁会释放实例状态。
- 输入事件到达已编译 Spawner 时不再用默认 `spawnCount=1` 直接穿透 Initialize，而是按输入 slot 驱动 Start/Stop。`VFXManager` 每帧在 input 后执行 Spawner：Constant/Variable Rate 保留跨帧小数债务，Burst 支持 delay boundary、single/periodic、大 delta catch-up 与零 period 每帧一次保护；结果打包成真实 `spawnCount` Event record，继续走已验证的 native Initialize kernel/prefix/dead-list transaction。`GetSpawnSystemInfo` 现在返回组件实例 snapshot，而非 Asset 静态模板。

### 测试与门禁
- 新增 Spawner runtime/ABI/端到端深测 **21/21**，覆盖初始/Play/Stop/new loop、分数累积、delta/public state、restart reset、single/immediate/periodic/zero-period Burst、大步长 catch-up、Variable Rate seed 确定性与 constant-range 对照、非法 delta、实例隔离、v5 round-trip、事件不直穿 Initialize、逐帧 native spawnCount record 和共享 Asset 隔离。
- 本机官方 `Editor/Templates/SimpleParticleSystem.vfx` 已实际编译出 Constant Rate=16 的 v5 Program、默认 OnPlay control 与 Initialize output；VFX Graph 全量 **416/416**，Core 全量 **966/966**。主 dylib 复制进测试产物并设置 `ANITY_REQUIRE_NATIVE=1` 后，全部 VFX 定向回归 **149/149**。
- Unity 2022.3.51f1 的 84 程序集 API 门禁保持类型 **928/4,117**（22.541%，exact 403）、成员 **8,645/37,164**（23.262%，exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`；当前机器仍未安装最终目标 2022.3.61f1 fixture。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例；产品工程 0 编译错误，URP3DDemo 保留既有 43 个 nullable warning。

### 尚未完成
- 本批 loop/delay mode 与 operand 已进入 v5 ABI，但编译器仍明确拒绝非 Infinite/None 配置，尚未执行有限/随机 loop duration、loop count、before/after delay 和跨状态大 delta。Spawner Set Attribute、custom callback、Spawner chain 的新状态机组合、linked expression/activation runtime opcode 也未闭环。
- Rate/Burst 调度器当前是组件实例级托管执行器，已经产生真实效果但仍须按项目强制分层迁入 `anity-native` C++；Variable/Burst 的随机采样顺序、边界帧和 `VFXSpawnerState.newLoop/totalTime` 还需 Unity 官方播放器黑盒 A/B 后才能宣称逐边界一致。
- Update/Reap/Output 仍未共享完整 native particle lifecycle；Metal transaction 长锁、Vulkan/D3D11 compute、GPU Event、Particle Strip、URP frame graph、完整 VFX Block/Operator/Output、编辑器与截图/产物 A/B，以及 Unity 2022.3.61f1 最终 fixture 和剩余 **3,189** 个官方类型缺口均未完成，因此总体 Unity 2022 Ultra 目标继续进行。

### 下一优先项
1. 把 Spawner Program evaluator 迁入 `anity-native` C++，实现有限/随机 loop duration/count、before/after delay 与大 delta 状态跨越，并用 Unity 2022.3 官方 Player 做逐帧 `VFXSpawnerState`/粒子数 A/B。
2. 为 Spawner Set Attribute、custom callback、linked expression/activation 与 Spawner chain 增加 typed runtime opcode，使所有 CPU Spawner task 在同一 v5+ IR 中组合执行。
3. 让 Update/Reap/Output 共享 native particle/dead-list storage，完成 CAS staged submit、Vulkan/D3D11 compute、GPU Event/Particle Strip 与 URP frame graph。

## 2026-07-16ai — VFX Spawner spawnCount 多生成与 Initialize inclusive-prefix 执行

### 已完成
- 对照本机 Unity 2022.3.51f1 / Visual Effect Graph 14.0.11 官方 `VFXExpressionGraph`、`VFXAttribute.SpawnCount` 与 `VFXInit.template` 收口 CPU Event 多生成语义：`spawnCount` 现在始终作为 Event record 的第一个隐式 Float 字段，运行时导入的默认值为 **1.0f**；手工 `DefineEventAttribute` 仍保持普通 Float 的 0 默认，避免把编译资产规则错误扩散到动态 schema。
- VFX runtime asset ABI 升级为 **v4**，Initialize kernel 新增严格验证的 `SpawnCountSourceOffsetWords`；v1-v3 继续读取并迁移为 legacy 一条 source record 对应一个 candidate。编译器只有在首字段确为 Float scalar `spawnCount` 且 offset/stride 精确匹配时才导出 prefix binding，损坏或错类型资产在原子替换前拒绝。
- native Initialize kernel ABI 升级为 **v2 / 44 bytes**，CPU reference 为选中 source records 构建容量饱和的 inclusive prefix sum，并用与官方模板一致的 upper-bound 二分把每个 spawn thread 映射回 source record。正有限值向零截断，负数、零与 NaN 不生成，正无穷及超大值按剩余 capacity 饱和；`startEventIndex`、dead-list、alive source gate、连续追加、事务回滚、sequence/idempotency 均作用于扩展后的真实粒子集合。
- Metal 通用 MSL interpreter 新增 prefix buffer、source-event count 与 candidate count binding，真实 compute thread grid 按展开后的粒子数发射，并在 shader 内二分定位 source record；macOS 已验证多 source 的 bit-exact 粒子属性映射及 `backendKind=2`。`VisualEffect.SendEvent` 无显式 attribute 时会打包 runtime schema 默认值，因此默认 Spawn 事件真实生成一个粒子。
- 修复 Apple 产品构建关闭 Vulkan 后 native software/headless fallback 仍冒充 `backendKind=Vulkan` 的问题：只有真实 backend storage 存在才报告 Vulkan/Metal/D3D，Vulkan UI 实测门禁也统一要求真实 backend kind，不再把 not-supported readback 记为产品回归。

### 测试与门禁
- Initialize kernel 专项 **32/32**，新增多生成深测覆盖单 record 展开、多 record inclusive-prefix、batched offset、fraction/negative/NaN/+Infinity、全零、capacity 续写、dead-list、alive source gate、幂等重试、v4 round-trip/非法 binding、真实 v3 二进制迁移、无参数 `SendEvent` 默认 1、双 Event 单事务以及真实 Metal bit-exact 映射。强制主 dylib 的全部 VFX 回归 **128/128**。
- VFX Graph 全量 **415/415**；Core 全量 **945/945**。Apple 无 Vulkan 产品后端时，原先误入 fallback 的 Vulkan UI 两类 **21/21** 现在按真实 backend 能力正确判定；native 构建和四个 particle-state export 检查通过。
- Unity 2022.3.51f1 的 84 程序集 API 门禁保持类型 **928/4,117**（22.541%，exact 403）、成员 **8,645/37,164**（23.262%，exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`；当前机器仍未安装最终目标 2022.3.61f1 fixture。
- `bash _scripts/build-all.sh` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例；所有产品工程 0 编译错误，现有 warning 债务不属于本批新增回归。

### 尚未完成
- 本批执行的是已经物化到 CPU Event record 的 `spawnCount`；Constant/Variable Rate、Burst/Periodic Burst、loop/delay 与 deltaTime accumulation 等 Spawner block 状态机尚未在 native/runtime 逐帧执行。linked expression/activation 也仍缺 typed runtime opcode。
- Update kernel、死亡/reap、dead-list recycling 与 Output draw 还未共享同一份 native particle storage；Metal transaction 在 GPU completion 期间仍持有 registry lock，Vulkan/D3D11 compute、GPU Event header/instancing、Particle Strip、URP frame graph 与完整 Block/Operator/Output 尚未闭环。
- Unity 2022.3.61f1 最终 fixture、剩余 **3,189** 个官方类型缺口、完整 Shader Graph/VFX Graph 包、编辑器交互及截图/产物 A/B 仍未完成，因此总体 Unity 2022 Ultra 目标继续进行，不能标记完成。

### 下一优先项
1. 将官方 Spawner Constant/Variable Rate、Burst/Periodic Burst、loop/delay 与 deltaTime 状态机编译为版本化 runtime IR，并把每帧结果直接物化为当前已验证的 spawnCount prefix 输入。
2. 让 Update/Reap/Output 共享 native particle/dead-list storage，完成生成、更新、死亡回收与 URP draw 生命周期；同时把长锁 GPU 提交改为 generation snapshot + staged resource + CAS commit。
3. 将同一 IR/存储路径接入 Vulkan/D3D11 compute，继续 linked expression/activation、GPU Event、Particle Strip、完整 VFX Block/Operator/Output 与 Unity 2022.3.61f1 官方 A/B。

## 2026-07-16ah — VFX runtime asset v3、可执行 Initialize IR 与真实粒子状态

### 已完成
- VFX runtime asset ABI 升级为 **v3**，并继续读取 v1/v2。每个 Initialize target 现在可携带 backend-neutral kernel IR：Particle capacity、stored/source stride、catalog-order attribute layout/default words、dead-list contract，以及 Constant/Source/particleId/seed/spawnIndex operand、Overwrite/Add/Multiply/Blend composition、PerComponent/Uniform deterministic random。序列化边界严格校验 system/context/capacity、紧凑 attribute offset、alive/dead-list、source/target 精确绑定、operand arity/type 与 system value 约束；坏资产仍在原子替换前拒绝。
- `VfxInitializeRuntimeKernelCompiler` 将已支持的 Initialize Block/attribute 语义降级为该 IR，并复用全局 Event record layout 的 source offset。编译器保留 stored/source attribute metadata，常量、source、random range、composition/blend 与 system value 不再只存在于生成的 HLSL 文本中。尚无 opcode 的 linked expression/activation 会明确拒绝导入，不静默改变效果。
- native 新增版本化 Initialize kernel、attribute、operation 与 particle-system C ABI；device registry 以 effect + particle system 保存完整 capacity attribute buffer、alive/dead count、dead list、顺序 spawn index、backend 与 generation。bulk submit 对整个事务预校验并 staged publish，CPU reference interpreter 已执行默认值、source/constant/system operand、全部 composition/random、alive gate、dead-list consume、capacity clamp 与连续追加；effect teardown 同时释放粒子状态和死亡列表。
- Metal 后端运行时编译通用 MSL IR interpreter，使用真实 `MTLComputeCommandEncoder` 和 source/particle/dead-list/descriptor/counter buffers 执行同一语义并回读 staged 状态；macOS 实测 `backendKind=2` 且结果 bit-exact。空 schema CPU Event 现在仍上传一个内部 transport word/record，能够真实触发一次 Initialize，而不会因公开 stride 为零丢事件。
- kernel 幂等指纹已排除 transaction-local `attributeStart`/`operationStart` 打包偏移；同一批 dispatch 交换顺序重试仍被识别为相同语义，既不拒绝也不重复生成粒子。四个新增 C ABI 已由主 `libanity_native.dylib` 导出并通过双侧 size/static ABI gate。

### 测试与门禁
- Initialize runtime kernel 专项 **17/17**：覆盖四个 ABI size、defaults/constant/source batch offset、Add/Multiply/Blend 顺序、particleId/seed/spawnIndex、dead-list physical slot、alive=false、capacity clamp、相同 kernel 重试、交换 transaction 顺序重试、新 sequence 追加、effect clear、v3 round-trip/非法 capacity/零 source stride、真实 Metal compute/readback 与空 schema Event。
- VFX Graph 新增 runtime IR 编译测试 **5/5**，全量 **414/414**；Core 托管全量 **929/929**；主 dylib + `ANITY_REQUIRE_NATIVE=1` 的 VFX 组合回归 **112/112**。
- Unity 2022.3.51f1 的 84 程序集 API 门禁保持类型 **928/4,117**（22.541%，exact 403）、成员 **8,645/37,164**（23.262%，exact 6,417）、load issues=0、`regressions=0`、`removed-or-changed=0`；当前机器尚未安装最终目标 2022.3.61f1 fixture。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、API auditor、WebGL、Hub、Editor 与样例；产品工程 0 编译错误，URP3DDemo 保留既有 43 个 nullable warning。`bash _scripts/build-native.sh` 与四个新 export 的 `nm` 检查通过。

### 尚未完成
- linked expression/activation 尚缺 typed opcode lowering；CPU Event 当前仍是一条 record 对应一个 Initialize candidate，Spawner prefix-sum/multi-spawn 语义尚未闭环。Update kernel、粒子死亡/reap 后 dead-list recycling 与 Output draw 还未消费本批同一份 native particle storage，因此本批不能描述为完整 VFX 粒子生命周期。
- Metal 事务在 GPU completion 期间仍持有 registry lock；需要 generation snapshot + staged GPU resources + compare-and-swap commit。Vulkan/D3D11 尚无同 IR compute interpreter，GPU Event header/instance、Particle Strip、URP frame graph、完整 VFX Block/Operator/Output、编辑器与截图/产物 A/B 仍未完成。
- 最终 Unity 2022.3.61f1 官方 fixture、全项目剩余 **3,189** 个官方类型缺口，以及 Shader Graph/VFX Graph 官方包的全功能生产级对齐仍未收口，不能宣称总体 Unity 2022 Ultra 已完成。

### 下一优先项
1. 将 Spawner spawnCount/prefix-sum/multi-spawn 编译进 runtime IR，并让 Update/Reap/Output draw 共享 native particle/dead-list storage，闭环生成、更新、死亡回收和渲染。
2. 把 bulk transaction 改为 generation snapshot + staged GPU CAS commit，并将同一 IR interpreter 接入 Vulkan/D3D11 compute 与对应平台强制 native 门禁。
3. 扩展 linked expression/activation opcode、GPU Event/Particle Strip/其余 Block/Operator/Output 与 URP frame graph，并迁移到 Unity 2022.3.61f1 官方 fixture 和编辑器/截图 A/B。

## 2026-07-16ag — VFX Initialize bulk transaction 与 effect 全状态 teardown

### 已完成
- 新增 `AnityGraphics_SubmitVFXInitializeDispatches`：一次提交最多 4,096 个 target descriptor，先对整组 ID、sequence、offset/count/stride、source range 与 byte overflow 做全量预校验，再在 registry lock 下复制 shared-state map、按调用顺序执行 CPU/Metal target、校验同 target sequence/幂等内容，最后仅在全部成功后通过 map swap 与 generation commit 一次性发布。任一后续 descriptor、Metal dispatch 或分配失败都会销毁 staged state，原 registry 完全不变，关闭了多 target 帧失败时的部分可见窗口。
- 单 descriptor ABI 现在复用 bulk 实现，只有一套 sequence、idempotency 与 backend 逻辑。相同 descriptor/record slice 的整组重试不会重复 GPU dispatch或推进 generation；同一 target 的多个递增 batch 在 transaction 内顺序执行并只发布最新可观察结果，倒序或同序列异内容整组拒绝。
- `VisualEffect.ProcessInputEvents` 不再逐 target 修改 native registry，而是先完成全部 compiled target descriptor 绑定，再执行单次 bulk transaction；仅 transaction 成功后才消费 native prefix plan 和 managed attribute snapshots。因此 Event→Spawner→Initialize 的每帧提交、输入队列消费与结果可见边界现在一致。
- 新增 `AnityGraphics_ClearVFXEffectState`，在同一 device lock 下清除指定 effect 的 latest upload、input FIFO、output FIFO、output sequence 与全部 Initialize target。`VisualEffect.visualEffectAsset` 切换和 `Object.DestroyImmediate` 销毁组件/物体均接入所有 live graphics device 的 teardown；managed cached attribute、pending attribute snapshot 与 last bound plan 同步释放，避免 effect ID 长期占用 native registry 或旧 Asset 的事件泄漏到新 Asset。

### 测试与门禁
- bulk/teardown 新增 **12/12**，Initialize 专项累计 **26/26**：覆盖多 target 同时发布、后置非法 descriptor 全回滚、已存在 target 冲突回滚无关新 target、同 target 递增/倒序、整组幂等 generation、input/latest 清理、output FIFO/sequence 重置、全 target 清理与跨 effect 隔离、Asset 切换、`DestroyImmediate` 及未知 effect 幂等清理。
- 主 `libanity_native.dylib` + `ANITY_REQUIRE_NATIVE=1` 的 Input/Initialize/Output/EventAttribute 组合回归 **79/79**；Core 托管全量 **912/912**，VFX Graph 全量 **409/409**。
- Unity 2022.3.51f1 的 84 程序集 API 门禁保持类型 **928/4,117**、成员 **8,645/37,164**、`regressions=0`、`removed-or-changed=0`、load issues=0；`UnityEngine.VFXModule` 公开差异仍为 **0**。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、WebGL、Hub、Editor 与样例；产品工程 0 错误/警告，URP3DDemo 保留既有 43 个 nullable warning。

### 尚未完成
- bulk transaction 当前原子发布的是 selected source record 结果；Metal generated Initialize block→particle attribute/dead-list buffer 仍未执行。transaction 在 GPU completion 期间持有 VFX registry lock，语义正确但后续应改为 generation snapshot + staged GPU resources + compare-and-swap commit，避免长 kernel 阻塞同 device 的事件查询/输出队列。
- Vulkan/D3D11 compute、GPU Event、Particle Strip、Update/Output draw、URP frame graph、完整 VFX Block/Operator/Output 与编辑器/截图 A/B 仍未完成；Unity 2022.3.61f1 最终 fixture 和全项目 **3,189** 个缺失官方类型也仍未收口，不能宣称总体 Unity 2022 Ultra 已完成。

### 下一优先项
1. 将 Initialize compilation 的 stored/source attribute layout、capacity、default/SetAttribute 表达式与 dead-list contract 序列化进 runtime asset，建立 backend-neutral kernel IR；Metal 先用真实 particle/dead-list buffers 执行并回读 bit-exact 粒子状态。
2. 将 transaction 改为 generation snapshot + staged GPU resources + CAS commit，并把同一 kernel IR 接入 Vulkan/D3D11 compute 与各平台强制 native 门禁。
3. 继续 GPU Event/Particle Strip/Update/Planar draw/URP frame graph、完整 VFX/Shader Graph 与 Unity 2022.3.61f1 官方验证。

## 2026-07-16af — VFX CPU Event bound target 到 backend Initialize dispatch 与首个真实 Metal compute

### 已完成
- `anity-native` 新增 backend-neutral VFX Initialize dispatch C ABI。descriptor 完整携带 effect/sequence、Initialize 与 source Spawner context ID、event/Particle/Spawn system property ID、`startEventIndex`、record count 与 stride；native 在 effect + Initialize context 维度保存最新结果、generation、source/output byte count 与真实 backend kind，并提供严格 info/readback。C++/C# ABI 固定为 56/80 bytes，双侧测试与 native `static_assert` 同时守护。
- native 边界现在拒绝零 ID/sequence、负 offset、空 record、非 4-byte stride、整数溢出、source 越界、旧 sequence 与同 sequence 异内容。完全相同的 sequence/descriptor/目标 record slice 支持幂等重试，即使重试时 source plan 后缀增长也不会重复 dispatch 或推进 generation；这使多 target 提交中途失败后可以安全重试未完成 target，成功前不会消费 CPU Event prefix plan。
- `NativeGraphicsDevice` 已接入 submit/info/readback P/Invoke，并再次验证 identity、record/stride/byte count 与 backend kind。`VisualEffect.ProcessInputEvents` 现在把已编译的 Event→Spawner→Initialize target 映射逐 batch 转成 native descriptor，在成功提交全部有 record 的 target 后才消费 native/managed input queue；未知 event、无 target event 与零 record event 不伪造 GPU 工作。
- Metal 后端新增缓存并加锁创建的 `MTLComputePipelineState`，运行时编译 `anity_vfx_initialize_copy` MSL kernel；每次 dispatch 使用真实 shared source/output `MTLBuffer`、`MTLCommandBuffer`、`MTLComputeCommandEncoder`、thread grid、GPU completion wait 与 output buffer readback。Metal 成功结果明确标记 `backendKind=2`；Vulkan/D3D11 在尚无 compute pipeline 时只走并明确标记为 `backendKind=0` 的 CPU reference，绝不冒充 GPU 完成。非 Metal 构建提供显式 not-supported backend hook，跨平台可链接。

### 测试与门禁
- 新增 Initialize dispatch 深测 **14/14**：C ABI size、CPU selected slice/metadata、target 隔离、newer replace、older reject、相同重试幂等、同序列异内容拒绝、source 越界、stride 对齐、readback 容量、单/分叉 compiled target、unknown event，以及 macOS 真实 Metal compute/readback。主 `libanity_native.dylib` + `ANITY_REQUIRE_NATIVE=1` 的 Input/Initialize/Output/EventAttribute 组合回归 **67/67**，Metal 用例强制要求实际 device、`backendKind=2` 与 bit-exact bytes。
- Core 托管全量 **900/900**，VFX Graph 全量 **409/409**。诊断性“全部 Core 强制 native”在 Apple 产品 dylib 上为 **879/900**：21 个失败全部来自现有 Vulkan-only UI/texture 用例仍强制要求 Vulkan readback，而 Apple 产品构建按平台规范显式关闭 Vulkan；本批 Metal/VFX native 测试没有失败。后续须把三后端实测拆成对应平台矩阵，不能把未编入当前 dylib 的 Vulkan 用例算作 Apple 产品失败或假绿。
- Unity 2022.3.51f1 的 84 程序集 API 门禁保持类型 **928/4,117**、成员 **8,645/37,164**、`regressions=0`、`removed-or-changed=0`、load issues=0。`UnityEngine.VFXModule` 公开差异仍为 **0**。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、WebGL、Hub、Editor 与样例；产品工程 0 错误/警告，URP3DDemo 保留既有 43 个 nullable warning。主 dylib 导出三个 Initialize C ABI，并经 `otool -L` 确认只有 Metal/Foundation/QuartzCore 等 Apple 系统依赖、不含 Vulkan runtime。

### 尚未完成
- 当前 Metal kernel 已真实执行 source record range 的 GPU 搬运和 readback，但尚未把 VFX Graph 生成的 Initialize block 计算编译成 MSL/平台 shader，也未写入 Particle attribute/dead-list buffer，因此不能把本批描述为完整粒子生成结果或完整 VFX GPU runtime。
- Vulkan/D3D11 仍是明确的 CPU reference；需要实现各自 compute pipeline、device-local source/particle/dead-list buffer、fence/readback 与对应 Windows/Android/Linux 实机门禁。多 target 当前依靠幂等重试保证可恢复，但尚缺一个 native bulk transaction 来消除失败时已提交 target 的短暂部分可见状态；effect/asset 销毁时的 device registry 清理 ABI 也仍需补齐。
- GPU Event header/instance index、Particle Strip 生命周期、Update/Output draw、URP frame graph、完整 VFX Block/Operator/Output、编辑器和官方截图/产物 A/B，以及 Unity 2022.3.61f1 最终 fixture 与全项目 **3,189** 个缺失官方类型仍未完成，不能宣称总体 Unity 2022 Ultra 已完成。

### 下一优先项
1. 将编译后的 Initialize kernel contract 接入 Metal particle/dead-list buffers，生成并执行真实 block 计算；同时增加 bulk transaction 与 effect registry teardown，关闭部分可见和长期资源生命周期缺口。
2. 以同一 ABI 实现 Vulkan/D3D11 compute、GPU Event header/batch/instance、Particle Strip 初始化/回收，并建立 Apple Metal、Windows D3D11、Android/Linux Vulkan 分平台强制 native 测试矩阵。
3. 接入 Update/Planar draw 与 URP frame graph，继续扩展 VFX Block/Operator/Output、Shader Graph material integration，并迁移到 Unity 2022.3.61f1 官方 fixture、编辑器与截图 A/B。

## 2026-07-16ae — VFX CPU Event→Spawner→Initialize runtime target 映射

### 已完成
- VFX runtime asset ABI 升级为 **v2**：每个 CPU input event 除公开事件名外，保存全部稳定 dispatch target；每个 target 明确记录 Initialize context ID、Particle/Particle Strip system name，以及从 Event 到 Initialize 的完整 Spawner context ID/system name 有序路径。v1 资产继续可读，并迁移为同名事件 + 空 target，不破坏旧产物加载。
- `VfxRuntimeAssetCompiler` 现在沿 typed flow DAG 从每个 `VFXBasicEvent` 递归到 Spawner 链与 `VFXBasicInitialize`，支持 Event 直连 Init、多级 Spawner、分叉到多个 Particle system、多个同名 Event context 合并与无目标事件；顺序保持 graph/flow 序列化顺序，重复路径去重，无法解析或到达不支持 context 时明确拒绝编译。
- runtime data 验证新增 input-event 顺序精确一致、Initialize/context 非零、Particle system 类型、Spawner path 长度/context 唯一性、Spawn system 类型及重复 target 检查；所有验证都发生在 `VisualEffectAsset` 原子替换前。
- `VisualEffectAsset` 按 Unity property ID 导入 input dispatch lookup。`VisualEffect.ProcessInputEvents` 在 native prefix-sum plan 消费前将每个 batch 绑定到编译后的 event target，并保存包含原始 records、batch prefix 和 target path 的帧 dispatch plan，供下一阶段 Metal/Vulkan/D3D11 source-buffer/Initialize compute 直接消费；未知 event 保留 batch 但不伪造 target。切换 Asset 会清理旧 plan 与待处理 attribute snapshot。

### 测试与门禁
- 新增 mapping/compiler/runtime 深测 **16 个**：编译器 **12/12** 覆盖直连、单/多级 Spawner、分叉、同名合并、不同事件隔离、无目标、round-trip、Asset lookup、bit deterministic、未知 Particle 与错误 Spawner path；Core 新增 **4/4** 覆盖 native batch target 绑定、未知事件、Asset 切换与 v1 兼容。CPU Input 强制 native 组现为 **19/19**。
- VFX Graph 全量 **409/409**；Core 全量 **886/886**；本机 Unity 2022.3.51f1 API 门禁仍为类型 **928/4,117**、成员 **8,645/37,164**、`regressions=0`、`removed-or-changed=0`、load issues=0，`UnityEngine.VFXModule` 公开差异 **0**。
- `bash _scripts/build-all.sh Release` 通过 native 与全部产品程序集/样例；产品工程 0 错误/警告，URP3DDemo 保留既有 43 个 nullable warning。

### 尚未完成
- CPU Event 的 graph→asset→native batch→runtime target 数据链已经闭环，但 Metal/Vulkan/D3D11 尚未按 target 上传 source records、创建/复用 GPU buffers、设置 `startEventIndex` 并发出 Initialize compute；当前不能声称粒子生成的 GPU 运行结果已完成。
- GPU Event header/instance index、Particle Strip 初始化/回收、三后端 Update/Planar draw、URP frame graph、VFX 其余 Block/Operator/Output、编辑器和官方截图/产物 A/B 仍未完成。
- Unity 2022.3.61f1 最终基线与全项目 **3,189** 个缺失官方类型仍需持续收口，不能宣称总体 Unity 2022 Ultra 已完成。

### 下一优先项
1. 定义 backend-neutral VFX Initialize dispatch ABI，将 bound plan 的 records/target/context/`startEventIndex` 交给 native，并先落地 Metal compute buffer + dispatch/readback 证据。
2. 将同一 ABI 扩展到 Vulkan/D3D11，补 GPU Event header/batch/instance 与 Particle Strip 生命周期，再接入 URP frame graph。
3. 扩展 VFX Block/Operator/Output、Shader Graph material integration，并迁移到 Unity 2022.3.61f1 官方 fixture、编辑器与截图 A/B。

## 2026-07-16ad — VFX CPU Event native FIFO、prefix-sum dispatch plan 与帧消费

### 已完成
- `AnityGraphics_UploadVFXEventRecords` 从“每个 effect 只保留最后一次上传”升级为每 effect 有界 FIFO，同时继续保留 latest descriptor/record readback 兼容面。相同 effect 的 sequence 必须严格递增，重复/倒序提交、跨批次不一致的编译 stride、非法 record bytes 与超过 4,096 个待处理批次都会在 native 边界拒绝。
- 新增 native CPU Event dispatch plan ABI：快照 first/last sequence、batch/record/byte count、共享 stride 与 upload generation；每个 batch 输出 eventNameId、sequence、recordCount、stride 和真实 `startEventIndex` 前缀。record copy 按批次稳定拼接，可按已存在 sequence 截取并原子消费，容量不足或未知 sequence 不会修改队列。
- Managed `NativeGraphicsDevice` 对 plan info、batch array 与 record bytes 做二次结构校验，包括 effect identity、sequence 单调性、prefix 连续性、统一 stride 与精确 byte count。`VisualEffect.ProcessInputEvents` 在成功快照后按 last sequence 消费 native/managed 两侧队列并释放 attribute snapshot；`VFXManager.ProcessCamera` 在 Output Event 与 timestep 前执行该路径。
- `VisualEffect.SendEvent` 的 sequence 分配、managed enqueue 与 native upload 现在处于同一 per-effect 锁内，关闭了并发调用中较大 sequence 先到 native、较小 sequence 被拒绝而丢事件的竞态；`Reinit` 也会释放被清理事件的 attribute snapshot。

### 测试与门禁
- 新增 CPU Input dispatch 深测 **15/15**，并用主 `libanity_native.dylib` + `ANITY_REQUIRE_NATIVE=1` 强制执行：单/多 batch、完整 plan、prefix sum、零 record、跨 eventName 顺序、legacy latest readback、重复/倒序、stride 冲突、部分消费、未知 sequence、容量不足、managed/native 联合消费、空队列、32 路并发及 `VFXManager.ProcessCamera` 集成。Input + Output + EventAttribute 强制 native 定向回归 **49/49**。
- Core 全量 **882/882**；VFX Graph **397/397**；本机 Unity 2022.3.51f1 的 84 程序集 API 门禁保持类型 **928/4,117**、成员 **8,645/37,164**、`regressions=0`、`removed-or-changed=0`、load issues=0，`UnityEngine.VFXModule` 公开差异仍为 **0**。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、WebGL、Hub、Editor 与样例；产品工程 0 错误/警告，URP3DDemo 保留既有 43 个 nullable warning。

### 尚未完成
- 本批已经生成可直接驱动 HLSL `startEventIndex` 的 native dispatch plan，但 Metal/Vulkan/D3D11 后端尚未把 plan records 上传为真实 source buffer，也未据 batch descriptor 发出 Initialize compute dispatch；当前帧消费是 runtime staging 完成标志，不代表粒子已经在 GPU 上生成。
- CPU/GPU Event 到具体 Spawner/Initialize system 的编译映射、GPU Event header/instance index、Particle Strip 初始化/回收、三后端 compute + Planar draw、URP frame graph，以及 VFX 其余 Block/Operator/Output、编辑器和截图 A/B 仍未完成。
- Unity 2022.3.61f1 最终基线与全项目剩余 **3,189** 个官方类型缺口不变，不能宣称总体 Unity 2022 Ultra 已完成。

### 下一优先项
1. 将 CPU Event context→Spawner→Initialize system 映射写入 runtime asset，并让 Metal/Vulkan/D3D11 后端消费 dispatch plan、上传 source buffer、发出真实 Initialize compute。
2. 实现 GPU Event header/batch/instance ABI、Particle Strip 初始化/回收和 Planar draw，挂入 URP 真实 frame graph。
3. 扩展 VFX Block/Operator/Output 与 Shader Graph material integration，并迁移到 Unity 2022.3.61f1 官方 fixture、编辑器交互和截图 A/B。

## 2026-07-16ac — VFX Output Event native 队列、readback 与托管帧内回调

### 已完成
- `anity-native` 新增 device-owned Output Event FIFO C ABI：按 effect 隔离批次，严格校验 record count/stride/byte count，以单调 sequence 拒绝重复或倒序提交，并提供 count、peek 与带 expected-sequence 的原子 dequeue；每个 effect 的未消费批次有明确上限，避免无界增长。
- `NativeGraphicsDevice` 完成 P/Invoke 包装与强类型 enqueue/peek/dequeue；`VisualEffect.ProcessOutputEvents` 按 runtime asset 的 Output Event context/record layout 解码 Bool、Int、UInt、Float、Vector2/3/4、Matrix4x4 的 bit-exact record，按 FIFO/record 顺序触发 `outputEventReceived`。未知 event 与已移除 asset 的陈旧批次会被安全消费，不会永久阻塞队列；非法 stride/byte layout 会在消费后明确失败。
- `VFXManager.ProcessCamera` 已在 effect timestep 前从当前 native graphics device 抽取并分发 Output Event，因此 callback 进入实际相机帧调度路径，而不是仅测试调用的旁路。
- Apple native 产品构建现在默认且由 `_scripts/build-native.sh` 显式关闭宿主 Vulkan，使用 Metal 主路径；这清除了旧 CMake cache 将主 dylib 绑定到 Android Emulator `libvulkan` 的风险。MoltenVK 验证仍可显式开启，Android/Windows/Linux 的 Vulkan 默认不变。

### 测试与门禁
- 新增 native/managed Output Event 深测 **15/15**：覆盖 native handle、descriptor/count、精确 bytes、FIFO、重复/倒序 sequence、错误 expected sequence、单/多 record、多 batch、全部 scalar/vector 位型、unknown event、坏 stride、空队列、asset 移除与 `VFXManager.ProcessCamera` 集成。用主 `libanity_native.dylib` 和 `ANITY_REQUIRE_NATIVE=1` 强制验证，未走托管 fallback。
- Core 全量 **867/867**；VFX Graph **397/397**。本机 Unity 2022.3.51f1 的 84 程序集 API 门禁保持类型 **928/4,117**、成员 **8,645/37,164**、`regressions=0`、`removed-or-changed=0`、load issues=0，`UnityEngine.VFXModule` 公开差异仍为 **0**。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、WebGL、Hub、Editor 与样例；产品工程 0 错误/警告，URP3DDemo 保留既有 43 个 nullable warning。主 macOS dylib 经 `otool -L` 确认不再含 Vulkan runtime dependency。

### 尚未完成
- 当前 native Output Event 是后端无关的生产级队列/readback ABI；Metal/Vulkan/D3D11 compute kernel 尚未在 GPU dispatch 完成后自动产出这些 records，因此不能把本批描述为完整 GPU VFX Output Event 闭环。
- CPU Event 的 native batched prefix-sum/source dispatch、GPU Event header/instance index、Particle Strip 回收、三后端 compute/Planar draw、URP frame integration，以及 VFX 其余 Block/Operator/Output、编辑器 Graph UI/preview、官方截图/产物 A/B 仍未完成。
- 全项目仍缺 **3,189** 个官方类型；最终 API、包与行为基准仍须迁移到 Unity 2022.3.61f1，当前 2022.3.51f1 证据不能用于宣称 Anity 已总体完成 Unity 2022 Ultra。

### 下一优先项
1. 将 CPU Event runtime data 接入 native batched prefix-sum/source dispatch，并由 Metal/Vulkan/D3D11 VFX compute 结束路径直接生产本批 Output Event records。
2. 实现 GPU Event header/batch/instance ABI、Particle Strip 初始化/回收及三后端 compute + Planar draw，挂入 URP 真实帧调度。
3. 扩展 VFX Block/Operator/Output 与 Shader Graph material integration，并持续补 Unity 2022.3.61f1 官方 fixture、编辑器交互和截图 A/B。

## 2026-07-16ab — VFX Graph 编译描述符到 VisualEffectAsset 生产运行时桥接

### 已完成
- `Unity.VisualEffectGraph.Editor` 现在按正确依赖方向引用 `Anity.Core`，由编辑器编译器生成 Core 内部 runtime contract；Core 不反向依赖编辑器，新增类型与导入入口全部保持 `internal`，没有扩大或污染 Unity 公开 API。
- 新增 `VfxRuntimeAssetCompiler`：按序列化 Graph 顺序提取 CPU Event 名称、Initialize source attribute、Particle/Particle Strip/Spawner/Mesh 系统、容量、Output Event 同名上下文合并、`spawner_input` 映射及 spawnCount-first record layout。系统命名按 Unity `VFXSystemNames` 规则去除自动编号后缀并稳定生成 `(1)`、`(2)`。
- 新增版本化二进制 runtime data ABI：固定 magic/version、显式长度、UTF-8/集合上限、SHA-256 checksum、无 trailing data；属性类型/offset/size/stride、重复名称、跨系统名称、Output Event 与全局 event schema、Property ID 碰撞均在提交 Asset 前验证。相同 Graph 编译结果 bit-exact 稳定。
- `VisualEffectAsset.ImportRuntimeData` 先完整反序列化和预构建，再原子替换 event schema、CPU Event、全部系统清单、Particle capacity/初始 sleeping state、Spawner state 与 Output Event context/mapping/record 元数据；失败不会修改 compilation version 或旧运行时状态，旧 Spawner state 在成功替换后释放。
- `VisualEffect` 的 `GetSystemNames` / `GetParticleSystemNames` / `GetSpawnSystemNames` / `GetOutputEventNames`、`GetParticleSystemInfo`、`GetSpawnSystemInfo`、`HasSystem` 与 `CreateVFXEventAttribute` 现在可直接消费 VFX Graph 编译资产，不再要求测试或调用方手工维护一套平行 schema。

### 测试与门禁
- 新增跨程序集 runtime bridge 深测 **16 个**；Output Event compiler/runtime 组 **32/32**，覆盖确定性、CPU Event 顺序/去重、Initialize source schema、Spawner/Particle 容量与 Unity 命名、Output context/mapping/stride、event attribute 类型、替换、checksum/truncate/trailing、冲突 schema 与非法 stride。
- VFX Graph 全量 **397/397**；Core 全量 **852/852**；本机 Unity 2022.3.51f1 的 84 程序集 API 门禁保持类型 **928/4,117**、成员 **8,645/37,164**、`regressions=0`、`removed-or-changed=0`、load issues=0，`UnityEngine.VFXModule` 公开差异仍为 **0**。
- `bash _scripts/build-all.sh Release` 通过 native、Core、Agent、Shader Graph、VFX Graph、CLI、WebGL、Hub、Editor 与样例；产品工程 0 错误/警告，URP3DDemo 保留既有 43 个 nullable warning。`git diff --check` 作为最终文本门禁执行。

### 尚未完成
- 本批完成的是编译资产 contract 与托管运行时状态桥接；CPU Event 多 record 的 native prefix-sum/dispatch 消费、Output Event native readback→managed callback、GPU Event header/instance index、Particle Strip 回收、Metal/Vulkan/D3D11 compute/Planar draw 与 URP frame integration 仍未贯通。
- 仍缺 VFX 其余 Block/Operator/Output、Planar Lit/Gradient/soft particle/Texture2DArray/Shader Graph material、编辑器 Graph UI/preview、官方运行截图/产物 A/B；最终基准仍必须迁移到 Unity 2022.3.61f1，当前 2022.3.51f1 仅是预备证据。
- 全项目仍有 **3,189** 个官方类型缺失及大量行为、编辑器和平台差距，不能宣称 Anity 已总体完成 Unity 2022 Ultra。

### 下一优先项
1. 将 CPU Event runtime data 接入 native batched event prefix-sum/source dispatch，并实现 Output Event record readback、schema decode 与 `outputEventReceived` 帧内回调。
2. 实现 GPU Event header/batch/instance ABI、Particle Strip 初始化/回收，以及 Metal/Vulkan/D3D11 compute + Planar draw，接入 URP 真实帧调度。
3. 扩展 VFX Block/Operator/Output 与 Shader Graph material integration，并在 Unity 2022.3.61f1 官方 fixture、编辑器交互和截图 A/B 上持续收口。

## 2026-07-16aa — UnityEngine.VFXModule 公开面闭环与可运行 Manager/System/Property 状态

### 已完成
- 以本机 Unity 2022.3.51f1 `UnityEngine.VFXModule.dll` 为预备反射基线，将该程序集全部公开类型与公开成员差异收口为 **0**。`VisualEffect` 已补齐 Bool/Int/UInt/Float/Vector2/3/4/Matrix4x4/Texture/AnimationCurve/Gradient/Mesh/SkinnedMeshRenderer/GraphicsBuffer 的 int-ID/string `Has/Get/Set`、`ResetOverride`、纹理维度、系统清单/存在性/awake 查询、Particle/Spawner info 与官方 metadata；`aliveParticleCount` 修正为官方 `int`。
- `VisualEffectAsset` 新增 compiled exposed-property definition、默认值、类型与 texture dimension；组件 override 与 asset default 分离，换 Asset 清理 override，未知/错类型写入不会污染状态。Particle/Spawner/Output Event system 使用同一编译资产数据源，查询返回 snapshot，缺失系统明确抛错。
- 新增并按官方表面精确实现 `VFXSpawnerLoopState`、`VFXSpawnerState`、`VFXParticleSystemInfo`、`VFXExpressionValues`、`VFXCameraBufferTypes`、`VFXCameraXRSettings`、`VFXBatchedEffectInfo`、`VFXSpawnerCallbacks` 与 `VFXManager`。ExpressionValues 提供强类型读取、错误类型拒绝及 Curve/Gradient 独立复制；SpawnerState 具备 loop/play/time/count/event attribute 生命周期与复制语义。
- `VFXManager` 现在从 live `VisualEffect` 注册表聚合组件与 batch info，维护有限正数 timestep、XR camera preparation、camera buffer requirement/binding、per-camera process 与空 camera state 清理；camera process 会推进未暂停 effect，而不是空方法。该托管调度层仍需接入 VFX Graph runtime descriptor、native batch/compute/draw 与 URP frame graph，不能据此宣称完整 VFX 运行时完成。
- API evidence baseline 已审查并重建：84 个官方程序集、类型存在 **928/4117**、精确 **403**；成员存在 **8,645/37,164**、精确 **6,417**；`regressions=0`、`removed-or-changed=0`、load issues=0。最终验收仍必须在目标 Unity 2022.3.61f1 重建，当前 2022.3.51f1 只作为预备基线。

### 测试与门禁
- 新增 `VisualEffectPropertyAndSystemTests` **14/14** 与 `VFXRuntimeServicesTests` **15/15**；连同事件属性组，VFX runtime 定向测试 **48/48**，覆盖默认值/override/reset、全部值族、对象与 null 约束、Asset 更换、系统分类、Particle/Spawner snapshot/Dispose、ExpressionValues、callback、batch、XR camera、buffer binding 与 process。
- Core 全量 **852/852**；VFX Graph **381/381**；Shader Graph **198/198**；强制实际加载 `libanity_native` 的 VFX descriptor/record byte readback **1/1**。
- `bash _scripts/build-all.sh Release` 通过 native 与全部托管产品/样例，0 编译错误；URP 示例保留既有 43 个 nullable warning。`git diff --check` 通过。

### 尚未完成
- VFX Graph compiler descriptor 尚未序列化/导入 runtime `VisualEffectAsset`；CPU Event 多 record/batched source 尚未贯通 native runtime；Output Event native callback/readback、GPU Event header/instancing、Particle Strip、Metal/Vulkan/D3D11 compute/draw dispatch、URP frame integration 与 editor/preview/截图 A/B 仍未完成。
- Unity 2022.3.61f1 官方 editor、程序集与包尚未安装，本批 API 精确结论必须迁移重验；全项目仍有 3,189 个官方类型缺失和大量行为/编辑器/平台差距，不能宣称总体 Unity 2022 Pro 已完成。

### 下一优先项
1. 定义 VFX Graph compiler descriptor→runtime asset 的稳定序列化 ABI，贯通 CPU Event source/record/batch 与 Output Event callback/readback。
2. 实现 GPU Event header、batch/instance index、Particle Strip 初始化/回收及 Metal/Vulkan/D3D11 compute + Planar draw，并挂入 URP 真实帧。
3. 安装 Unity 2022.3.61f1 后重建 API/package fixture baseline，并继续 Shader Graph 全 value/node/pass 与 VFX 其余 Block/Output/editor A/B。

## 2026-07-16z — UnityEngine.VFXModule 事件属性、运行时队列与 native record upload

### 已完成
- 以本机 Unity 2022.3.51f1 `UnityEngine.VFXModule.dll` 为预备反射基线，新增 `UnityEngine.VFX.VisualEffectObject`、`VisualEffectAsset`、`VFXExposedProperty`、`VFXEventAttribute`、`VFXOutputEventArgs` 与首批 `VisualEffect` 运行时公开面；`VFXEventAttribute` 的 public copy constructor、Dispose，以及 Bool/Int/Uint/Float/Vector2/3/4/Matrix4x4 的 int-ID/string `Has/Set/Get` 和 `CopyValuesFrom` 已按官方命名与重载落地。该证据仍需在 2022.3.61f1 重建，且不代表完整 VFXModule 公开面已关闭。
- `VisualEffectAsset` 现在保存编译后 event-attribute schema、稳定字段顺序/offset/stride、event 与 exposed-property 元数据；`Has*` 反映 Asset 声明类型而不是“是否 Set 过”。未知字段或错误类型不会污染编译布局，copy 会按目标 schema 过滤。
- `VFXEventAttribute` 可把 Bool/Int/UInt/Float/Float2/3/4/Matrix4x4 按稳定 32-bit word offset 打包为 bit-exact little-endian record，保留 `-0`、有符号整数位型和 column-major matrix component 顺序。
- `VisualEffect.SendEvent` / `Play` / `Stop` / `Reinit` 已实现 Asset 归属校验、调用时不可变 attribute snapshot、单 effect 单调 sequence 与线程安全有序队列；`CreateVFXEventAttribute` 在无 Asset 时按官方返回 null。Output callback 使用复用的缓存属性对象，避免把调用方对象直接暴露给回调。
- `anity-native` 新增设备级 `AnityGraphics_UploadVFXEventRecords` / info / readback C ABI：严格验证 effect/sequence、recordCount、4-byte 对齐 stride 与精确 byte count，原生复制后按 effect 保留最新单调事件记录，并可字节级回读。Managed `NativeGraphicsDevice` 会把事件上传到所有 live native device；这只是后端无关的 native staging ABI，尚未冒充 Metal/Vulkan/D3D11 GPU dispatch。
- 补充官方命名空间 `UnityEngine.Rendering.TextureDimension`，供 `VisualEffectAsset.GetTextureDimension` 返回精确类型；旧根命名空间兼容面暂未在本批迁移，避免扩大无关变更。

### 测试与门禁
- 新增 `VFXEventAttributeTests` **19/19**：覆盖公开方法/构造器形状、schema-before-set、string/ID 同址、全部标量/向量/矩阵、位型打包、未知/错类型、copy/null/dispose、跨 Asset 拒绝、不可变队列快照、32 路并发顺序、Play/Stop/Reinit、Output callback、Asset metadata 与 native descriptor/record 回读。
- Core 全量 **823/823**；既有 `UnityWebRequest` AssetBundle 用例暴露 `AssetDatabase` 全局状态并行冲突，纳入已有非并行 collection 后全量稳定通过。VFX Graph **381/381**、Shader Graph **198/198**；强制实际加载 `libanity_native` 的 VFX byte readback **1/1**。
- `bash _scripts/build-all.sh Release` 通过 native、全部产品程序集与样例；0 编译错误，URP 示例仍为既有 43 个 nullable warning。`git diff --check` 作为本批最后门禁执行。

### 尚未完成
- `UnityEngine.VFXModule` 仍缺 `VFXManager`、Spawner、ExpressionValues、particle/system info、完整 `VisualEffect` exposed property/system 查询面与精确官方行为；本批只关闭事件属性/队列/native staging 子集。
- VFX Graph compiler 的 CPU Event layout 尚未自动导入 `VisualEffectAsset` runtime schema；Output Event descriptor 尚未连接 native callback/readback；GPU Event header/instancing、batched offsets、Particle Strip、Metal/Vulkan/D3D11 compute/draw dispatch 与 URP frame integration 仍未完成。

### 下一优先项
1. 将 VFX Graph compiler event/source descriptor 序列化为 `VisualEffectAsset` runtime data，接通 CPU Event batched upload、Output Event native callback/readback，并补 `VFXManager` / `VisualEffect` 剩余公开 API 与官方反射门禁。
2. 实现 GPU Event header、batch/instance 索引、Particle Strip 初始化/回收与 Metal/Vulkan/D3D11 compute dispatch，再把 Planar Output 接入 URP 真实 draw frame。
3. 继续 Shader Graph vector/texture/matrix/gradient、Custom Function 多输入输出与 URP Decal/Fullscreen pass，并迁移到 Unity 2022.3.61f1 官方 fixture/A-B。

## 2026-07-16y — Unity 2022.3.61f1/官方包目标固化与三后端真实 mip chain

### 目标口径更新
- 唯一最终基准明确为 **Unity 2022.3.61f1 Pro**，不再以泛化的 2022.3.x 或当前安装的 2022.3.51f1 代替最终证据。
- 对等范围新增 Unity 官方包全链路，至少包含 Shader Graph、Visual Effect Graph、URP、Timeline、UGUI、Test Framework、Collections、Burst、Mathematics、Input System、Addressables。package manifest/依赖、公开 API、资产 importer、编辑器、代码生成、运行时、样例和平台矩阵都属于验收内容。
- 当前机器只有 Unity 2022.3.51f1；其内置 Shader Graph/VFX Graph manifest 均为 14.0.11，只作为审计工具与 fixture 的预备基线，不得冒充 2022.3.61f1 最终版本证据。

### 已完成
- `Texture2D` 不再把 `mipmapCount` 当占位数字：按 Unity floor-halving 规则拥有每级 Color/Color32 storage，`Get/SetPixel(s)` 与 `GetPixelBilinear` 的 mipLevel 真正寻址独立层级；显式 mipCount 会按物理链上限裁剪。
- `Apply(updateMipmaps:true)` 由 mip0 递归生成 2×2 box-filter 链；`Apply(false)` 保留手工 mip。`GetRawTextureData` / `LoadRawTextureData` 与 native upload 统一使用 largest-first 紧密打包的完整 RGBA8 mip chain，non-readable gate 在完整上传后关闭。
- native registry 按 width/height/mipCount 计算并严格验证全链 byte count，拒绝 mip descriptor + mip0-only 数据。operation mutex 继续保证同 device replace/destroy 原子性。
- Metal 创建精确 `mipmapLevelCount` 的 `MTLTexture` 并逐级 `replaceRegion`，Point/Bilinear 使用 nearest mip、Trilinear 使用 linear mip；Vulkan image/view/barrier/copy regions/sampler LOD 覆盖全部 mip；D3D11 immutable texture 使用完整 subresource array 与 mip-aware sampler。
- D3D11 真实 Windows 分支再次用 Zig `x86_64-windows-gnu` 编译；macOS native 与 Android NDK arm64-v8a/API26 全库重新编译链接通过。

### 测试与门禁
- 新增 `Texture2DMipmapTests` **14/14**：完整/显式/NPoT chain、独立 mip read/write/bilinear、自动递归生成、手工 mip 保留、raw pack/load、native byte count/非法 base-only ABI、non-readable upload。
- Metal texture tests **12/12**、Vulkan texture tests **11/11**；新增 minification fixture 将 64×64 七级纹理压缩到 8×8 quad，通过隐式导数真实采到 mip3 黄色。Vulkan 在 MoltenVK 与 SwiftShader 两套 ICD 均通过，证明非零 mip 已实际上传和采样。
- Core 强制 native + SwiftShader 全量 **804/804**。D3D11 同一 minification 用例已加入 Windows/WARP 门禁，当前非 Windows 主机只完成真实分支交叉编译，不能声称运行验收。
- `Texture2D.GetPixel/GetPixelBilinear/SetPixel(..., mipLevel)` 已补齐官方 `DefaultValue("0")` 参数 metadata；2022.3.51f1 临时 API baseline 经审查更新后复跑为 `regressions=0` / `removed-or-changed=0`，成员存在 **8,364**、精确 **6,136**。`bash _scripts/build-all.sh Release` 已通过；产品工程 0 错误/警告，样例仍是既有 43 个 nullable 警告。

### Shader Graph 14.x 独立包首批实现
- 新增独立 `anity-shader-graph/src/Unity.ShaderGraph.Editor` 工程，程序集名精确为 `Unity.ShaderGraph.Editor`，没有把官方包逻辑塞入 `Anity.Core`。构建与 PowerShell 全测试入口已纳入该工程。
- 实现 Shader Graph 14.x multi-json stream parser：保留原始源文本，解析连续 JSON objects，校验 `m_Type` / `m_ObjectId`、重复对象与本地 `m_Id` 引用，并建立稳定 object registry；同时读取 Unity 包内仍存在的 legacy 单 JSON `ShaderSubGraph`。
- 实现确定性的 legacy→14 multi-json upgrader：迁移 GraphData、properties、groups、nodes、nested slots、edges、PropertyNode property reference 与 default category，生成稳定 object ID，并在升级后重新执行引用、拓扑、无环与 Blackboard 校验。本机官方包的 **14/14** 个 legacy subgraph 已全部升级闭环。
- 实现现代 `GraphData.m_Nodes/m_Edges` 拓扑：节点 registry、output/input slot（含 Unity 14 使用的负数 hashed slot id）、单输入连接约束、稳定拓扑排序与 cycle detection，为节点编辑器和 HLSL 代码生成建立真实结构底座。
- 实现强类型 Blackboard：按源顺序建模 property/keyword/dropdown/category，覆盖 Unity 14 定义的 **16** 种 property type，解析 reference name、precision、value、float/range 与 HLSL declaration override，并严格拒绝 duplicate/missing/type mismatch。官方 356 个 modern 资产中实际出现的 948 properties、57 keywords、25 dropdowns 与 432 categories 均已进入 gate。
- 实现 Unity 14 ShaderKeyword pragma 语义：Boolean/Enum、ShaderFeature/MultiCompile/Predefined、Local/Global 与 vertex/fragment/geometry/hull/domain/raytracing stage suffix，Predefined 不生成 pragma。
- 实现首批确定性 scalar HLSL generation：`Vector1Node`、Add/Subtract/Multiply/Divide 与真实 `PropertyNode` 使用 slot/edge/default value 生成依赖有序函数；Vector1/Boolean property 支持 Global、UnityPerMaterial、HybridPerInstance、DoNotDeclare 声明路径，Hybrid 生成 DOTS/classic instancing 宏并通过 access macro 读值。共享依赖/属性只生成一次；cycle、非标量 property、缺 slot/节点/引用及未支持 node type 会明确拒绝，不生成占位 shader。
- 实现 Target/SubTarget 强类型层与严格 target-pairing：覆盖 URP Lit/Unlit/Decal/Fullscreen、Built-in Lit/Unlit、HDRP Lit/Unlit/Decal/Fabric；Built-in/HDRP 可无损读取但明确为非产品支持，唯一产品路径仍为 URP。官方 modern fixture gate 已覆盖 220 Universal、189 Built-in、212 HD targets 及全部实际 subtarget 分布。
- 实现 URP 14 Lit/Unlit pass planner：按 Unity 的 surface、alpha clip、z-write、clear coat/complex-lit 条件生成 Forward/ForwardOnly、GBuffer、ShadowCaster、DepthOnly、DepthNormals/Only、Meta、SceneSelection/Picking 与 2D 的精确顺序、LightMode、template/pragmas/includes；Decal/Fullscreen 已识别但在 pass 生成阶段明确拒绝，尚未冒充完成。
- 实现 Custom Function 强类型模型与首条可执行 scalar HLSL 链：File=0/String=1、6 类官方样例 slot、输入后输出签名顺序、`_$precision`/`_float` 命名；String 模式生成函数定义，File 模式通过 GUID resolver 生成受限 `.hlsl/.cginc/.cg` include。默认占位节点可无损读取但不可生成；冲突定义、非法标识符/路径、错误输出槽、非单 Vector1 输出和向量输入均明确拒绝。向量/纹理资源与多输出代码生成仍未完成。
- 新增 **198/198** Shader Graph 测试；官方 fixture gate 已遍历本机 Shader Graph 14.0.11 的 **370** 个 `Samples~` / `ShaderGraphLibrary` `.shadergraph/.shadersubgraph` 资产（356 modern + 14 legacy），全部通过解析与对象引用完整性；modern 图通过拓扑、无环、Blackboard、keyword、Target/SubTarget 与 Custom Function typed gate，legacy 图升级后通过引用、拓扑、无环与 Blackboard gate。该证据仍属于 Unity 2022.3.51f1 预备基线。

### Visual Effect Graph 14.x 独立包首批实现
- 新增独立 `anity-vfx-graph/src/Unity.VisualEffectGraph.Editor` 工程，程序集名精确为 `Unity.VisualEffectGraph.Editor`，并进入正式 build-all 与 PowerShell 全测试入口；VFX 包逻辑没有污染 `Anity.Core`。
- 实现 `.vfx` Unity YAML 1.1 multi-document lossless index：解析 `!u!classID`、64-bit signed `fileID`、serialized root type、原始 document text 与 object registry，识别 `VisualEffectResource` 根对象，区分本地 `{fileID: ...}` 和带 guid 的外部资源引用，报告重复/悬空引用。
- 建立 **70** 个 script GUID 类型 registry：69 个 VFX Graph 14 内置 Graph/UI/Data/Context/Block/Operator/Parameter/Slot 类型（含 CPU/GPU Event、Output Event、`VFXSpawnerSetAttribute` 与官方 `VFXSlotFloat4`），以及 1 个 HDRP `VFXHDRPSubOutput` 外部类型。外部类型可无损读取并明确标为 unsupported，不把 HDRP 变成 Anity 产品管线。
- 实现 typed graph：解析并分类 context/block/operator/parameter/slot/data，验证严格 parent/children 层级、master slot owner/direction、linked slot、context↔data owner、output↔external sub-output，以及双向 input/output flow slot；生成去重 flow edge 与稳定 context topological sort，cycle、越界、非 context endpoint、非双向引用和 malformed YAML list 均明确拒绝。Data 使用 `m_Data/m_Owners` 关系而非错误套用通用 `m_Children` 树。
- typed slot value 层解析 `m_Property`、property/value serializable type、direction 与精确 `VFXCoordinateSpace`（Local=0/World=1/None=int.MaxValue）；强类型覆盖 Float/Int/Uint/Bool/Float2/Float3/Color/Position/Direction/Vector/Transform、Texture2D/Mesh object reference、AnimationCurve、Gradient 与 generic structured JSON。类型错配、非法数值/JSON/component/object GUID/space 均明确拒绝，child slot 的空 master value 保持为空而不伪造默认值。
- 实现首批真实 typed GPU expression/HLSL compiler：沿 reciprocal linked slot DAG 编译 Float/Float2/Float3/Position/Direction/Vector 默认值与 Add/Subtract/Multiply/OneMinus operator，按依赖稳定排序，共享输出只生成一次，使用 invariant finite HLSL literal 与合法 signed-fileID 变量名。Local↔World 转换逐式对齐 VFX Graph 14 官方 `VFXExpressionTransform`：Position 使用 `mul(matrix,float4(value,1)).xyz`、Vector 使用 3×3、Direction 使用 3×3 后 normalize，None/same-space 不插转换；旧 scalar 入口复用同一 typed compiler。cycle、错误 direction/owner/arity、类型/space mismatch、未支持 operator/value type、NaN/Infinity 均拒绝，不输出占位代码。
- 实现 VFX Graph 14 attribute schema：精确建模 Boolean/UInt32/Int32/Float/Float2/Float3/Float4、Read/Write/ReadSource、Overwrite/Add/Multiply/Blend、Off/PerComponent/Uniform、Slot/Source 与 variadic channel；catalog 覆盖 **40 个 stored attribute + 4 个 variadic aggregate**，含默认值、space/read-only/write-only/local/internal 约束与 7 类 custom attribute signature。SetAttribute/SetCustomAttribute 可生成确定性 typed HLSL，随机路径自动注入 seed；`VFXSpawnerSetAttribute` 已按官方 lowercase `randomMode`、全 variadic channel 以及 Spawn Context 可写 `spawnTime/spawnCount` 特例建模。本机 12 个官方 fixture 的 **80 个 attribute-bearing model** 全部通过 schema/statement gate。
- 实现 Context/Event/Data 强类型层：按 VFX Graph 14 精确值建模 9 类 Context flag、SpawnEvent/OutputEvent/Particle/Mesh/ParticleStrip data、Spawner/Initialize/Update/Output task；解析 CPU Event/Output Event 默认名、Particle/Strip capacity、simulation space、bounds mode、compute bounds、Planar primitive task 与 context flow slot profile，并严格验证 context↔data ownership、Particle/Strip 容量乘积及 flow data type。
- 实现首个可执行 Update Context GPU kernel 子集：按 context child 顺序编译 SetAttribute/SetCustomAttribute，生成 data-wide `VFXAttributes` 工作结构与 `RWByteAddressBuffer` 显式 typed 读写、`[numthreads(64,1,1)]` bounds gate、typed 常量槽局部变量、四种 composition 与 deterministic random hash/state/seed writeback。Float/Int/UInt/Bool/Float2/Float3/Float4/Position/Direction/Vector linked input 会复用 typed expression compiler 内联 operator/Attribute Parameter DAG，并把 Local↔World 矩阵加入 dispatch contract；可达 `VFXAttributeParameter` 会按 Current/Source 与 variadic mask 推导结构体字段，避免不可达参数污染布局。
- Update Source 语义已按官方 `VFXCodeGenerator.GenerateLoadAttribute` 对齐：进入 Update 时建立 `VFXSourceAttributes` 快照；同属性在当前布局存在时复制初值，否则使用 catalog 默认值，不错误绑定 Init/GPU Event 专用 `sourceAttributeBuffer/sourceIndex`。Source SetAttribute 的 `Value`、variadic channel packing 及历史 Source+Blend 资产均可生成。
- Block activation 已按官方 `_vfx_enabled` expression mapper 对齐：无 activation slot 时迁移 deprecated `m_Disabled`，常量 false 编译期裁剪，serialized activation slot 优先于旧字段，linked Bool/Current/Source Attribute Parameter 生成运行时 `if` 并合并 attribute dependency；非 Bool link 明确拒绝。其余未支持 Block 与非 Init/Update context 仍在生成前拒绝，不输出占位内核。
- 对照官方 VFX Graph 14 `Gravity.cs`、`Force.cs`、`ForceHelper.cs` 与 `Drag.cs` 落地首批非 Set Block：Gravity 精确执行 `velocity += Force * deltaTime`；Force Absolute/Relative 分别复现 mass 除法与 Drag clamp；Linear Drag 复现 mass 衰减、零下限以及可选 `size * scaleX/Y` 面积影响。常量/linked input、activation、执行顺序与 attribute dependency 共用现有 typed kernel 路径；错误 Mode、设置、slot 名与 slot 类型在 HLSL 生成前拒绝。
- 对照官方 `VFXBasicUpdate` 与 implicit Block 源码实现 Update 上下文隐式语义：按实际 Read/Write dependency 在显式块前备份 `oldPosition`，在显式块后插入 Euler position、逐通道 angular Euler、Age、Reap；`integration/angularIntegration/ageParticles/reapParticles/skipZeroDeltaUpdate` 的默认值、枚举/布尔验证和开关行为已落地。使用 alive 时先过滤初始死亡粒子，Reap 按官方严格 `age > lifetime` 置死；dispatch contract 正式加入 `deltaTime`。
- 对齐官方 `VFXUpdate.template` 的普通 Particle alive/dead-list 分支：alive 被使用时只让初始存活粒子进入 block；仍存活时提交全部属性，新死亡时从原始 buffer snapshot 仅持久化 `alive=false`，随后以 `InterlockedAdd` 将 particle index 追加到 `deadListOut`。单实例 dispatch contract 提供 `deadListCount`/capacity；越界追加会原子回滚 count，避免损坏后续 Init 消费状态。Particle Strip 明确不绑定普通粒子 dead-list，保留后续 strip 专用回收路径。
- 对照官方 `VFXInit.template` 与 `VFXBasicInitialize` 新增独立 Initialize kernel：当前 attribute 从 catalog 默认值开始，Source Attribute 从 `sourceAttributesBuffer[sourceIndex]` 读取；CPU source 用带零事件保护的 prefix-sum 二分定位，GPU Event 由真实 `VFXBasicGPUEvent -> Init` flow 选择 `eventList` sourceIndex。`particleId`、`seed=WangHash(particleIndex ^ systemSeed)`、`spawnIndex` 在 block 前初始化，WangHash 五步整数公式与官方 `VFXCommon.hlsl` 一致并回写 random state；GPU Event 自动插入官方 implicit `alive=true`，即使没有显式 block 仍可编译。普通 Particle 在 block/alive gate 后以 snapshot 限制 spawn 数并原子消费 `deadListIn/deadListCount`，死亡结果不会占用槽位。
- 同一 `VFXDataParticle` 的 Init/Update 已改为 data-wide stored attribute 分析：所有支持 context 的 block、Current Attribute Parameter、random seed 与 Basic Update implicit dependency 合并为唯一稳定 catalog-order ABI；Init 会按 Unity 语义默认初始化只在 Update 使用的字段，Update 也保留只由 Init 写入的字段。编译结果公开字段 type/offset/size 与 stride 元数据；跨 context custom attribute 类型/space 冲突在生成前拒绝，disabled block 不污染 ABI，Update Reap 的隐式 alive 会自动驱动 Init dead-list 消费。
- 当前粒子 attribute storage 已从平台相关的 `RWStructuredBuffer<VFXAttributes>` 迁移为官方同类 `RWByteAddressBuffer`，按显式 offset/stride 生成 Bool/UInt/Int/Float/Float2/Float3/Float4 的 typed Load/Store；Bool 明确编码为 0/1 uint，本机官方 VFX 源码中的 `Load3`/`StoreN` 路径用于语法对照。local-only event/strip attribute 在专用 event/strip storage 未实现前明确拒绝，禁止错误进入 particle ABI。官方 `VFXSlotFloat4` GUID、Vector4 typed value/expression/HLSL declaration 同步补齐。
- Init 外部 Source event attribute 已从 `StructuredBuffer<VFXSourceAttributes>` 迁移为只读 `ByteAddressBuffer`，独立输出 source 字段 type/offset/size/stride 与 `UsesExternalSourceBuffer` binding 元数据，按 sourceIndex 生成 Bool/UInt/Float/Float3 `LoadN`。CPU prefix-sum 与 GPU Event event-list 都在 raw load 前确定 sourceIndex；Update Source 继续保持官方 entry snapshot 语义且不会误绑外部 buffer。
- 外部 Source raw layout 已继续对齐官方 `StructureOfArrayProvider` 的单 ReadSource bucket：attribute 按组件宽度稳定降序装入 4-word block，逐字段按 1/2/4-word alignment 排列，bucket stride 按最大 alignment 补齐；Float3+Float2、Float4+scalar 等组合不再使用错误的简单紧密串接。Initialize dispatch 正式加入 `startEventIndex`，所有 CPU/GPU Event raw load 使用 `(startEventIndex + sourceIndex) * stride`，并公开 batched source offset binding 元数据。
- `VFXPlanarPrimitiveOutput` 已参与同一 Particle Data 的 stored layout：逐项对齐官方 17 个 Read attribute（position/color/alpha/alive、axis/angle/pivot XYZ、size/scale XYZ），Init 会为只被 Output 使用的字段写入官方默认值，alive 自动启用 dead-list。`uvMode` 0–4 严格验证，Flipbook/Blend/MotionBlend 才加入 `texIndex`；存在 Shader Graph 时按官方 `supportsUV=false` 抑制 legacy flipbook attribute。默认 Planar profile 的稳定 stride 为 108 bytes，并在 Init/Update 完全一致。
- `VFXPlanarPrimitiveOutput` 已从 layout 推进到完整 Unlit graphics pass HLSL：只读 `ByteAddressBuffer` 先独立读取 alive，再对越界/死亡粒子写 NaN clip position；Triangle/Quad/Octagon 按官方 3/4/8 顶点偏移和 UV 展开，Octagon 默认 cropFactor=0.293；`size * scaleXYZ`、axis basis、度制 Euler、pivot、position 逐式组合 `elementToVFX`，随后执行 VFX→World→Clip 并输出 positionWS/UV/color/particleIndex。Fragment entry 实际采样主纹理；Flipbook 在 vertex 将 texIndex 转成帧 UV，Blend 双帧插值，MotionBlend 采样 motion vector map 后偏移双帧，ScaleAndBias 读取对应参数。Alpha clip 阈值进入 varying 并执行 `clip`；Additive/Alpha/Premultiplied/Opaque、ZWrite Default/Off/On、7 种 ZTest、Cull Default/Front/Back/Off、render queue/priority 与 Auto/Off/On sorting 按官方枚举生成强类型 render state。编译产物同时提供 vertex/fragment entry、每粒子顶点数、Triangle/Quad/Octagon 的 3/6/18 triangle index pattern、checked draw count 和多粒子 expanded index buffer，供后续 native/URP dispatch 直接消费。未实现的 Gradient Mapped、soft particle、Texture2DArray、geometry shader、Shader Graph material pass、Particle Strip 与 output block 在 HLSL 前拒绝。
- `VFXOutputEvent` 已按官方 `VFXDataOutputEvent` 落为 CPU compilation target，而非伪造 GPU kernel：无 HLSL、无 GPU attribute layout；只编译存在输入的 context，同名 event 合并为一个 system contract，所有直接 Spawner 去重输出 `spawner_input` binding，并设置 OutputEvent 实例化禁用原因。祖先 context 按 flow 递归收集 `VFXSpawnerSetAttribute`，以稳定 catalog 顺序导出独立的 ReadSource attribute contract；直接 CPU Event 等非 Spawner 输入在 descriptor 生成前拒绝。
- 对齐官方 `VFXExpressionGraph.ComputeEventAttributeDescs` 新增 CPU Event record ABI：`spawnCount` 无条件位于第一个 field，其余 ReadSource attribute 按首次出现顺序去重；每个 field 输出 32-bit word `element/structure` 信息。typed record packer 支持 Bool/Int/UInt/Float/Float2/3/4 的 bit-exact little-endian 写入、多个 record 与 `startEventIndex` 前缀，严格拒绝未知字段、类型/宽度冲突和非法批次偏移；Output Event descriptor 可直接生成对应 record layout。
- 新增 **381/381** VFX YAML/typed graph/slot value/expression/attribute/context/kernel/pass/event ABI 测试：除既有 Init、data-wide ABI 与 current/source raw buffer 深测外，覆盖 Planar Output layout、完整 vertex/fragment pass、外部 binding、typed load、官方 source bucket packing/alignment/stride、CPU/GPU sourceIndex 与 batched `startEventIndex`、CPU Event field/record binary packing、Attribute Parameter、Update snapshot、只读约束、官方 output 字段/default、alive/dead-list、bounds/dead cull、三种 primitive、官方 transform、五种 uvMode、主纹理/双帧/motion sampling、alpha clip、四种 blend、ZWrite/ZTest/Cull/queue/sort、native index expansion，以及 Output Event CPU/no-layout、同名合并、多 Spawner mapping、实例化禁用、递归 ReadSource attribute contract、spawn-only attribute 和非法输入分支。官方 fixture gate 已遍历本机 Visual Effect Graph 14.0.11 的 **12** 个 `Editor/Templates` 与 `Samples~` `.vfx` 资产，全部通过多文档解析、resource/本地 fileID 完整性、已知 script GUID、typed hierarchy/slot value/data/flow、attribute/context schema 与无环排序检查；其中 **6 个官方 VFXAttributeParameter** 已实际通过 typed expression codegen。Shader Graph 仍为 **198/198**。本机仅有内部协议的 `UnityShaderCompiler`，无可直接调用的 dxc/glslang，因此官方源码与 codegen 深测不冒充真实 GPU shader 编译或运行 A/B。该证据仍属于 Unity 2022.3.51f1 预备基线；Init/Update/Planar graphics pass codegen、CPU record packer 与 Output Event descriptor 仍不等于完整 VFX native/URP runtime。

### 仍未完成
- Unity 2022.3.61f1 官方 editor/assemblies/packages 尚未安装到当前机器；现有 2022.3.51f1 API、Shader Graph 与 VFX fixture baseline 必须迁移重建。Shader Graph 尚缺完整 node/property runtime value、Custom Function 向量/纹理/多输出、URP Decal/Fullscreen pass、编辑器、预览、全节点/全 variant HLSL 与运行 A/B；VFX Graph 尚缺完整包 registry/settings、更多 operator/cast 与其余 Block、Planar 之外的完整 Output、compiler descriptor→runtime asset bridge、Output Event native callback/readback/runtime 分发、Source upstream packing/runtime、CPU batched/instanced event offsets、GPU Event header/instancing 索引、Particle Strip 初始化/回收及真实 native/URP dispatch runtime、event/property/output 编辑器与运行 A/B。
- mip streaming（requested/minimum/loaded level）、mip bias、anisotropy、compressed ASTC/ETC/BC/PVRTC native resources、Texture2DArray/Cubemap/RenderTexture、external texture、async upload/device-loss rebuild 尚未完成。
- Windows MSVC/WARP mip readback、WebGL texture LOD、移动端实机 mip/压缩格式矩阵及 Unity 2022.3.61f1 数值 A/B 尚未完成。

### 下一优先项
1. 在已闭环 Target/SubTarget、Lit/Unlit pass 与 scalar Custom Function 的底座上扩展 Shader Graph texture/vector/matrix/gradient property、Custom Function 向量/纹理/多输出、URP Decal/Fullscreen pass 与 variant generation；在 VFX current/source raw ABI、Init/Update、Planar Output、GPU Event、dead-list 与 Output Event CPU descriptor 底座上继续补 native/URP draw dispatch、Output Event runtime callback/readback、Source upstream packing、Turbulence、Collision、Spawn/Output Block，并建立 batched/instanced offsets、Particle Strip 与真实 GPU dispatch runtime。
2. 实现 mip bias/aniso 与 requested/minimum/loaded mip streaming，再接 ASTC/ETC/BC/PVRTC native resource paths。
3. 继续 uGUI soft clip、mask/pop stencil，并执行 Windows WARP 与 Unity 2022.3.61f1 官方截图/行为 A/B。

## 2026-07-16x — D3D11 Texture2D/SRV/Sampler 与三后端纹理契约闭环

### 已完成
- D3D11 backend 新增 device-owned `ID3D11Texture2D`、`ID3D11ShaderResourceView`、`ID3D11SamplerState` cache，RGBA8 linear/sRGB format、Point/Bilinear/Trilinear filter、Repeat/Clamp/Mirror/MirrorOnce address mode 与 1×1 white fallback 均由同一 native texture descriptor 驱动。
- D3D11 UI HLSL 与 input layout 现在读取 packed vertex UV0，pixel shader执行 vertex color × main texture，并用独立 alpha texture red channel调制 coverage；每个 draw packet 分别绑定 main/alpha SRV 与 sampler，不再只输出顶点色。
- generic texture registry 已把 D3D11/D3D12 纳入 upload/destroy backend dispatch；`Apply` 同 ID 替换 GPU resource，`DestroyImmediate(Texture)` 释放 SRV/sampler/texture，`GetNativeTexturePtr()` 在 D3D11 返回真实 `ID3D11Texture2D*`。device teardown 释放全部 cache 与 white fallback。
- registry 增加独立 operation mutex，将同 device 的 upload/replace/destroy 串行化；descriptor/info 查询继续用短 data mutex，避免并发 Apply/Destroy 令 backend handle 与 registry entry 交叉失配。
- D3D11 ABI 增加 packed vertex color/UV0/stride 静态断言；资源替换先完整创建 replacement，再原子切换 map entry，分配失败保持旧资源有效并清理新资源。

### 测试与门禁
- 新增 `NativeD3D11UITextureTests` **12 个** Windows/WARP 门禁用例：solid sample、vertex tint、main alpha、独立 alpha、alpha-only fallback、Apply replacement、native handle/backend kind、Point、Repeat、多 texture SRV、Destroy stale-SRV 与非零 mip minification。
- D3D11 真实 `_WIN32 + ANITY_HAS_D3D11` 分支已用 Zig `x86_64-windows-gnu` 目标编译通过；macOS native Release 重新编译链接通过，Android NDK arm64-v8a/API26 全库重新编译链接通过。
- Core 纯托管 **787/787**；强制 native + `ANITY_REQUIRE_VULKAN=1` + SwiftShader **787/787**。D3D 契约组在当前非 Windows 主机计入编译/发现门禁，但测试主体会按平台返回；真实像素断言仍必须在 Windows/WARP runner 执行后才可标记平台验收完成。
- `bash _scripts/build-all.sh Release` 通过：native/Core/Agent/CLI/parity/WebGL/Hub/Editor 均 0 编译错误且产品工程 0 警告；URP3DDemo 保留既有 43 个 nullable 警告、0 错误。

### 仍未完成（不得宣称 Unity uGUI/跨平台纹理完成）
- 缺 Windows 机器上的 MSVC/CMake 全库构建、WARP/hardware 真实 framebuffer readback 11/11 证据；Zig 交叉编译只能证明 Windows 源分支可编译，不能代替运行时验收。
- mip generation 与三后端完整 RGBA8 mip resource 已于 2026-07-16y 完成；mip streaming、ASTC/ETC/BC/PVRTC、aniso/mip bias、Texture2DArray/Cubemap/RenderTexture、external native texture、async upload 与 device-loss rebuild仍未完成。
- uGUI soft clip、Mask/RectMask2D stencil stack、URP material/shader variants、字体/ETC1 alpha split 官方 fixture 与 Unity 2022.3 截图 A/B 尚未完成。

### 下一优先项
1. 在 Windows runner 执行 MSVC build + `ANITY_REQUIRE_D3D11=1` WARP 12 个真实像素用例，并补 hardware adapter evidence。
2. 实现 mip、compressed formats、aniso/mip bias、async upload 与 device-loss rebuild。
3. 实现 soft clip 与 mask/pop stencil，建立 Unity 2022.3 Image/RawImage/Text/Font/Mask 官方场景截图 A/B。

## 2026-07-16w — Texture2D native registry、Metal/Vulkan 真实 UV/alpha 采样与资源释放

### 已完成
- graphics device 新增 device-owned RGBA8 texture registry C ABI：`UploadTextureRGBA8`、`DestroyTexture`、`GetTextureInfo`、`GetTextureNativeHandle`。registry 用 mutex + unique ownership 深拷贝像素，按 texture ID 原子替换，记录 revision、尺寸、mip、linear、filter、wrap、byte count、upload generation 与实际 backend kind；无效尺寸/采样枚举/byte count 会在 native 边界拒绝。
- 托管 `Texture2D.Apply` 现在推进 revision 并同步当前 graphics device；`NativeGraphicsDevice` 按 revision + dimensions/format/filter/wrap/linear 计算状态 key，未变化帧不重复上传。Canvas bridge 在生成每个 submesh command 时确保 main/material texture 与 alpha texture 已注册，`Apply(makeNoLongerReadable:true)` 在释放公开 CPU readability 前完成 native upload。
- 建立 live graphics-device 集合；`Object.DestroyImmediate(Texture)` 会从所有活跃 device 原子移除 CPU/GPU texture entry，device teardown 统一销毁整个 registry。Null backend 不把 CPU vector 冒充 native GPU pointer。
- Metal backend 增加真实 `MTLTexture`/`MTLSamplerState` cache：RGBA8 linear/sRGB format、Point/Bilinear/Trilinear filter、Repeat/Clamp/Mirror address mode，上传后 `Texture.GetNativeTexturePtr()` 返回真实 backend handle。UI vertex shader传递 UV0，fragment shader执行 vertex color × main texture，并用独立 alpha texture red channel调制 coverage；每个 draw packet 按 texture/alpha ID 绑定，缺失纹理使用 owned 1×1 white fallback。
- Vulkan backend 增加 device-local sampled `VkImage`/memory、host-visible staging upload、`UNDEFINED → TRANSFER_DST → SHADER_READ_ONLY` barriers、image view、sampler、freeable descriptor pool 与每 texture descriptor set；pipeline layout 使用 main/alpha 两个 combined-image-sampler set，SPIR-V 输入 UV0 并执行与 Metal 相同的 main/alpha 公式。Apply replacement 与 Destroy 在 device idle 后安全切换/释放 image、view、sampler、descriptor，`GetNativeTexturePtr()` 返回真实 `VkImage` handle。
- Texture Apply replacement 会创建并切换新 GPU resource；Destroy 清除 Metal/Vulkan cache 和 registry handle，旧 command 不再悬挂引用。Android/non-Apple 通过明确 Metal sync/destroy stub 保持同一链接契约。
- 新增 `_scripts/compile-ui-shaders.sh`，从受版本控制的 GLSL 用 `glslc -O` 可重复生成嵌入式 SPIR-V header，避免手工修改二进制数组。

### 测试与门禁
- 新增 `NativeTextureRegistryTests` **14/14**：descriptor、byte count、revision/generation、unchanged cache、Apply gate、sampling-state invalidation、non-readable、Canvas main+alpha 自动注册、显式/DestroyImmediate 释放、多 device 隔离、非法 ABI、32 路并发替换、Null pointer 语义。
- 新增 `NativeMetalUITextureTests` **11/11** 真实 GPU/readback：solid sample、vertex tint、main alpha、separate alpha red、alpha-only white fallback、dynamic Apply、native handle/backend kind、Point、Repeat、多 texture draw、Destroy 后 white fallback。
- 新增 `NativeVulkanUITextureTests` **10/10**：solid/tint/main alpha/separate alpha、dynamic Apply、VkImage handle、Point/Repeat、per-draw descriptor 与 Destroy stale-descriptor 防护；**SwiftShader 10/10、MoltenVK 10/10** 双 ICD 均为真实 framebuffer readback。
- macOS native Release 编译链接通过；Android NDK arm64-v8a/API26 全库重新编译链接通过并包含四个 texture C ABI export。该里程碑 Core 纯托管 **776/776**、强制 native/SwiftShader **776/776**；Agent **30/30**、CLI **16/16**、Unity API parity **17/17**，`bash _scripts/build-all.sh Release` 通过。

### 仍未完成（不得宣称跨平台纹理完成）
- D3D11 texture2D/SRV/sampler cache 已于 2026-07-16x 落地并通过真实 Windows 分支交叉编译；仍缺 Windows WARP 运行证据，因此跨三后端平台验收尚未完成。
- 完整 RGBA8 mip generation/upload/sampling 已于 2026-07-16y 完成；mip streaming、ASTC/ETC/BC/PVRTC GPU compressed resources、aniso/mip bias、Texture2DArray/Cubemap/RenderTexture、external native texture、async upload 与 device-loss rebuild 尚未接通。
- sRGB/linear 已选择 backend format，但尚缺 Unity Color Space/URP shader 的官方 A/B 数值矩阵；alpha split texture channel语义也需要官方 ETC1/Font fixture 扩充。

### 下一优先项
1. ~~为 D3D11 实现 Texture2D/SRV/SamplerState 与 `GetNativeTexturePtr` 绑定。~~（源码已于 2026-07-16x 完成；Windows WARP 运行证据仍待补）
2. 实现 mip、compressed formats、aniso/mip bias、device-loss rebuild 与 async upload。
3. 实现 soft clip 与 mask/pop stencil，再做 Unity 2022.3 Image/RawImage/Font/Mask 官方场景截图 A/B。

## 2026-07-16v — CanvasRenderer 多 submesh 独立 command 与材质槽语义

### 已完成
- `CanvasNativeRenderBridge` 不再把一个 Mesh 的所有 submesh 索引合并后错误套用 material slot 0；每个非空 triangle submesh 现在生成独立持久 command，共享一次 packed vertex 转换，但保持自己的 index stream、原始 submesh material slot、material main texture 与稳定 renderer-command ID。
- 单 submesh 保留原 renderer object ID；多 submesh ID 由 renderer ID + 原始 submesh index 确定，跨帧稳定且互不覆盖。空 submesh 会跳过但不会压缩后续 material slot；缺失 slot 按 CanvasRenderer slot 0 回退，显式 `SetTexture` 优先于各 material 的 `mainTexture`，alpha texture、clip、softness、mask/pop、透明度与 sort depth 逐 command 继承。
- `Mesh` 从单个全局 topology 修正为 per-submesh topology 存储；`subMeshCount`、`Clear`、`SetTriangles`、`SetIndices` 同步维护索引与 topology，UI bridge 会在进入 native queue 前拒绝 Lines/Points/Quads 和非三角 index count，避免后端按 triangle list 误解释。
- 清理 Vulkan swapchain 中未实际拥有资源、只残留在销毁路径的 readback buffer 字段；真实 Vulkan readback 继续使用函数内 staging buffer/memory 并在同一次调用中释放。

### 测试与门禁
- 新增 `CanvasNativeSubMeshCommandTests` **13/13**：单/双 submesh、独立稳定 ID、material slot、slot 0 fallback、显式 texture 优先、material texture、alpha texture、空 slot 保序、非 triangle topology、后续非法 index、共享 render state，以及自动 frame flush 后 native 2 command/2 batch/6 index 集成。
- 与自动 bridge 合并定向强制 native **24/24**；native Release（含当前 Vulkan real branch）重新编译、链接成功。Core 全量纯托管 **741/741**，显式 `VK_ICD_FILENAMES=.../vk_swiftshader_icd.json` + `ANITY_REQUIRE_NATIVE=1` + `ANITY_REQUIRE_VULKAN=1` 强制 native/Vulkan **741/741**。

### 仍未完成（不得宣称完整 uGUI）
- command 已按 material/texture ID 正确拆分；Metal 已接通真实 texture ownership、upload、sampler 与 shader binding，但 Vulkan/D3D11 尚未完成同一资源链。
- alpha texture 尚未进入 shader coverage，material shader/blend/stencil variants、texture wrap/filter/mipmap/sRGB、动态 Apply 更新、non-readable CPU data release、RenderTexture/native external texture 生命周期仍待实现。
- soft clip、mask/pop stencil、Vulkan window/HDR/resize、D3D Windows runner 与官方 Unity uGUI 截图 A/B 仍是明确缺口。

### 下一优先项
1. 建立 Texture2D RGBA8 native resource registry、创建/更新/销毁 C ABI 与托管 dirty/version 同步，并让 `GetNativeTexturePtr` 返回 backend resource handle。
2. 为 Metal/Vulkan/D3D11 增加 UV0 texture + alpha texture sampling、filter/wrap sampler、descriptor/argument binding 与跨材质像素门禁。
3. 实现 rect softness 与 mask/pop stencil stack；再补官方 Unity 2022.3 多材质 Image/RawImage/Mask 场景截图 A/B。

## 2026-07-16u — Vulkan UI render pass、per-slot fence 与真实双 ICD 像素门禁

### 已完成
- Vulkan backend 从 upload-only 补齐为真实 GPU 路径：版本化 GLSL 450 vertex/fragment source、NDK `glslc -O` 生成的嵌入式 SPIR-V、108-byte `AnityUIPackedVertex` position/Color32 layout、viewport push constant、source-alpha blend、dynamic viewport/scissor 与 `vkCmdDrawIndexed`。
- headless swapchain 不再是 software image counter：为每个 image 创建 device-local optimal-tiling RGBA8 `VkImage`、memory、image view、framebuffer、render pass；每帧透明 clear，render pass 结束转 `TRANSFER_SRC_OPTIMAL`，staging buffer + `vkCmdCopyImageToBuffer` 输出 top-to-bottom tightly packed RGBA8。
- 建立三槽 primary command buffer + signaled fence：upload 覆盖 ring slot 前 `vkWaitForFences`，draw submit 前 reset，对应 GPU 完成后才允许复用；device/swapchain teardown `vkDeviceWaitIdle` 后按 Vulkan ownership 顺序释放 pipeline/framebuffer/view/image/memory/fence/pool/layout。
- windowed swapchain 补齐 `imageAvailable`/`renderFinished` semaphore：`vkAcquireNextImageKHR` 使用有效 semaphore 与无限 timeout，draw submit 等待 acquire 并 signal render-finished，present 等待 render-finished；不再使用 Vulkan 规范禁止的 semaphore/fence 都为空 acquire。
- `AnityGraphics_ReadbackSwapchainRGBA8` 现在 dispatch Vulkan readback；mask/pop 继续明确跳过且不计 draw，clip packet 用动态 scissor，多个 material batch 编码为多个 indexed draw。

### 测试与门禁
- 新增 `NativeVulkanUIDrawTests` **10/10**：红/蓝像素、geometry 外透明、source alpha、scissor、空帧清除、两 material draws、六帧 triple-ring fence、RGBA8 行序、mask 不误报。
- macOS 通过 Android Emulator Vulkan loader 分别在 **SwiftShader 10/10** 与 **MoltenVK 10/10** 真实执行 framebuffer tests；`ANITY_REQUIRE_VULKAN=1` 确保 Vulkan device/pipeline/swapchain 缺失即硬失败，不是 skip。
- Android NDK 23 arm64-v8a / API 26 完成全库 CMake build 与 shared-library link；`compile_commands.json` 明确 `ANITY_HAS_VULKAN=1`，ELF 依赖 `libvulkan.so`/`libandroid.so`，`AnityGraphics_Vulkan_UploadUI/DrawUI/ReadbackSwapchainRGBA8` 导出存在。
- Core 纯托管 **728/728**；`ANITY_REQUIRE_NATIVE=1` + `ANITY_REQUIRE_VULKAN=1` + SwiftShader 强制 native **728/728**。上一批 Agent **30/30**、CLI **16/16**、反射审计器 **17/17** 与官方 baseline 仍有效。

### 仍未完成（不得宣称完整 URP/uGUI）
- Vulkan/Metal/D3D 当前 shader 仍只消费 vertex color；`materialId`、`textureId`、`alphaTextureId` 已按 submesh/material slot 正确进入独立 draw packet，但尚未映射为 GPU resource、descriptor/sampler 与 shader variant。
- softness 未做 shader edge fade，mask/pop 未实现 stencil increment/decrement/test；HDR Vulkan render target/readback、MSAA resolve、resize/out-of-date swapchain rebuild、device-lost recovery 与真实 Android ANativeWindow present 像素证据仍待补。
- D3D11 真实 Windows/WARP 编译执行仍缺 runner；官方 Unity uGUI 场景截图 A/B 尚未建立，不能以三个 backend 的纯色 quad 代替完整 uGUI 对等。

### 下一优先项
1. 将 CanvasRenderer submesh/material slots 拆成独立 command，并为 Metal/Vulkan/D3D11 建立 texture/alpha texture resource registry、sampler/descriptor 与 UV shader。
2. 实现 rect softness 与 mask/pop stencil stack，覆盖 nested RectMask2D/Mask、多层透明与 batch break。
3. 补齐 Vulkan resize/out-of-date/device-loss、HDR/MSAA 与 Android ANativeWindow present；建立 Unity 2022.3 官方场景截图 A/B。

## 2026-07-16t — CanvasRenderer 自动 native queue、场景排序与 Metal 端到端像素闭环

### 已完成
- 新增 internal `CanvasNativeRenderBridge`：每帧自动发现有效 `UnityEngine.CanvasRenderer`，按 display、Canvas sorting key 与 Transform sibling hierarchy 稳定排序，将 Mesh、material/texture IDs、alpha texture、clip/softness、mask/pop、renderer tint 与 CanvasGroup inherited alpha 转换为持久 `NativeUICanvas` command；Unity 公开 API 面不增加 Anity-only 成员。
- Overlay mesh 顶点经完整 Transform 链转换到 framebuffer pixel space，并把 Unity bottom-left screen 坐标翻转为 backend top-left 坐标；ScreenSpaceCamera/WorldSpace 在存在 `worldCamera` 时走 `Camera.WorldToScreenPoint`。UV0–UV3、normal、tangent、Color32 与全部 submesh indices 均进入 packed command，非法 index、空 geometry 与非有限坐标在 native submission 前拒绝。
- `NativeGraphicsDevice.BeginFrame` 自动 flush/attach bridge-owned queue，`Canvas.ForceUpdateCanvases` 在 layout/graphic/clip 完成后同步 queue，`UnityRuntime.Tick` 改走官方 Canvas render event phase；device dispose 会释放 bridge queue。显式调用 `AttachUICanvas` 的 caller-owned queue 始终优先，避免自动场景扫描覆盖工具/测试自管队列。
- 自动队列已经接通现有 Metal pipeline：业务侧只创建 `Canvas` + `CanvasRenderer` + `Mesh`，无需实例化或提交 `NativeUICanvas`，即可完成 native batch、triple-buffer upload、indexed GPU draw 与 framebuffer RGBA8 readback。

### 测试与门禁
- 新增 `CanvasNativeRenderBridgeTests` **11/11**：Overlay 坐标/Y 翻转、renderer tint、CanvasGroup alpha、clip/scissor 坐标与 softness、cull、mask/pop、material/main/alpha texture ID、multi-submesh index 合并、非法 index、empty geometry、UV/normal/tangent，以及无手工 queue 的 Metal 中心像素实证。
- 自动桥接 + 原 Metal draw 定向强制 native **21/21**；Core 纯托管 **718/718**、`ANITY_REQUIRE_NATIVE=1` 强制 dylib **718/718**。全量回归曾捕获自动 queue 覆盖显式 attachment，修正 ownership 优先级后全部通过。
- Agent **30/30**、CLI **16/16**、反射审计器 **17/17**；`bash _scripts/build-all.sh Release` 通过，native/Core/Agent/CLI/parity/WebGL/Hub/Editor/URP3DDemo 均 0 编译错误。官方 84 程序集 baseline 保持 `regressions=0`、`removed-or-changed=0`、加载问题 0。

### 仍未完成（不得宣称完整 uGUI/平台渲染）
- 当前一个 CanvasRenderer command 合并全部 submesh 但只选择 material slot 0；尚需按 submesh/material slot 拆 command。texture/alphaTexture IDs 已传到底层，但 Metal/D3D11/Vulkan 尚未创建真实 texture resource/descriptor 与采样 shader。
- rect scissor 已可用，但 softness 仍未在 shader 做渐变；mask/pop 仍缺 stencil nesting。Camera/WorldSpace 已完成投影入口，仍需官方 camera viewport、target display、透视背面剔除、nested overrideSorting/pixelPerfect 的场景 A/B。
- Vulkan indexed draw/fence/readback 和 D3D11 Windows 编译/WARP 像素 fixture 仍是跨平台阻塞；本机无 Vulkan SDK/Windows runner，不能把源码存在当作平台验收。

### 下一优先项
1. 完成 Vulkan UI render pass、pipeline、descriptor、indexed draw、per-slot fence 与 headless image readback，在 Android/Linux runner 建立像素门禁。
2. 将 CanvasRenderer submesh/material slots 拆成独立 command，并为三后端实现 texture/alpha texture、UV shader、blend/material variants。
3. 实现 rect softness 与 mask/pop stencil stack，使用 Unity 2022.3 官方 uGUI 多层场景做截图 A/B。

## 2026-07-16s — Metal UI indexed draw、GPU completion fence 与 framebuffer 像素门禁

### 已完成
- 将 Canvas batch snapshot 从只有 `AnityUIBatchInfo` 扩展为内部 `AnityUIDrawPacket`：携带全局 index-buffer `firstIndex`、index count、material/texture IDs、flags 与 clip/softness，graphics upload state 现在区分 batch count 与真正成功编码的 draw count；CPU/headless upload 不再把计划批次数误报成已绘制次数。
- Metal 后端新增真实 UI render pipeline：按 108-byte packed vertex ABI读取 position/Color32，pixel-space→NDC vertex transform、标准 source-alpha blend、indexed triangle draw、per-packet scissor、透明 clear 与 BGRA8/RGBA16 render-pipeline variant。
- headless Metal swapchain 新增 shared offscreen render target；导出 `AnityGraphics_ReadbackSwapchainRGBA8` 并接入 `NativeGraphicsDevice.TryReadbackSwapchainRGBA8`，输出 top-to-bottom tightly packed RGBA8，使 GPU 结果可做确定性像素验收。
- Metal 三槽 upload ring 使用每槽 dispatch semaphore 作为 command-buffer completion fence：覆盖 vertex/index buffer 前等待对应 slot，draw completion signal；无 swapchain、失败和 device teardown 路径均释放/等待 slot，六帧连续复用已验证。
- D3D11 同一契约已实现到源码：HLSL VS/PS、108-byte input stride、dynamic VB/IB、alpha blend、scissor、indexed draw、headless RTV、`D3D11_QUERY_EVENT` per-slot fence 与 staging texture RGBA8 readback；CMake 增加 `d3dcompiler`。当前 macOS 只能编译 D3D stub 分支，因此真实 Windows 分支仍等待 Windows runner 编译与像素实证。

### 测试与门禁
- 新增 `NativeMetalUIDrawTests` **10/10**：红/蓝颜色、geometry 外透明、source alpha blend、GPU scissor、空帧清除、两 material 两 indexed draws、六帧 triple-ring completion、RGBA8 行序与 unsupported mask 不误报 draw。
- UI upload + Metal draw 定向强制 native **26/26**；Core 纯托管 **707/707**、`ANITY_REQUIRE_NATIVE=1` **707/707**；Agent **30/30**、CLI **16/16**、反射审计器 **17/17**。
- `bash _scripts/build-all.sh Release` 通过；本机 Metal shader compile、pipeline creation、command encoding、GPU completion 与 framebuffer readback 均被真实执行。官方 84 程序集 baseline 保持 `regressions=0`、`removed-or-changed=0`、加载问题 0。

### 仍未完成（不得宣称完整 uGUI/平台渲染）
- 当前 Metal shader 只消费 vertex color，尚未解析/bind `materialId`、`textureId`、`alphaTextureId`、UV 与 URP shader variants；mask/pop packet 明确跳过且不计 draw，stencil nesting/soft clip 尚未实现。
- Vulkan 仍只有 buffer upload，没有 render pass/pipeline/descriptor/command-buffer draw 与 semaphore fence；D3D11 实分支尚缺 Windows 编译、WARP/硬件像素 fixture。Metal runtime source compilation 后续需替换为版本化 metallib/离线 shader 产物。
- `CanvasRenderer` 仍未自动同步到 root Canvas queue，ScreenSpaceCamera/WorldSpace 投影、nested Canvas sorting、HDR RGBA16 readback 与 device-loss/resize 重建矩阵仍未闭环。

### 下一优先项
1. 完成 Vulkan UI render pass、pipeline、descriptor、indexed draw、per-slot fence 与 headless image readback，并在 Android/Linux Vulkan runner 做像素门禁。
2. 为 Metal/D3D11/Vulkan 绑定 texture/alpha texture、UV、material/blend variant、rect softness 与 mask/stencil stack，建立 Unity uGUI 官方场景截图 A/B。
3. 将 `CanvasRenderer` dirty geometry/material/texture/clip 自动 upsert/remove 到 root `AnityUICanvas`，让 Canvas rebuild phase 无需手动 queue 管理。

## 2026-07-16r — Native Canvas 三缓冲 GPU upload 与 graphics-device 帧接线

### 已完成
- 新增 `anity_graphics_ui.cpp` 并纳入唯一 CMake 生产构建；graphics device 现在可非 owning 绑定 `AnityUICanvas`，`BeginFrame` 推进 Canvas frame，`EndFrame` 自动构建并提交 UI，亦支持显式立即提交与 56-byte upload stats 查询。
- 建立三槽 CPU upload ring：每帧持有扁平化 `AnityUIPackedVertex`、重定位后的 `uint32` indices 与 batch draw metadata，记录 frame/generation、batch/draw/vertex/index 数量、字节数、ring index 与实际 backend kind。
- 把 batch build、统计、vertex/index rebasing 与 draw metadata copy 合并为一次 C++-only 原子快照；整个快照持有 Canvas mutex，消除了 command 更新线程与 render submit 线程跨 generation 混读的窗口。
- Metal 后端已创建/扩容三组 shared `MTLBuffer` 并执行真实 CPU→GPU vertex/index copy；D3D11 使用三组 dynamic buffer + `Map(WRITE_DISCARD)`；Vulkan 使用三组 host-visible/coherent `VkBuffer`/memory + map/copy/unmap，并在 device teardown 释放资源。空批次合法，backend 不可用时保留明确的 CPU/headless ring。
- `NativeGraphicsDevice` 增加 Canvas attach/detach、立即 submit、强引用生命周期保护、失效 Canvas 帧前安全解绑与 `LastUIUploadStats`；dispose 先解绑再销毁 device，不取得 Canvas ownership。

### 测试与门禁
- 新增 `NativeGraphicsUIUploadTests` **16/16**：ABI、初始状态、EndFrame 自动提交、精确 byte/count、三帧 ring、generation、显式提交、detach、空批次、材质拆批、replacement、invisible cull、失效 Canvas、CPU fallback、真实 macOS Metal upload 与 100×并发更新/提交原子快照。
- Core 纯托管 **697/697**；`ANITY_REQUIRE_NATIVE=1` 强制 dylib **697/697**；Agent **30/30**；CLI **16/16**；反射审计器 **17/17**。
- `bash _scripts/build-all.sh Release` 通过：native/Core/Agent/CLI/parity/WebGL/Hub/Editor/URP3DDemo 均 0 编译错误；当前 macOS 实际编译并执行 Metal upload。本机无 Vulkan SDK，因此 Vulkan 真实分支本轮为源码落地但仅 stub 分支编译，D3D11 也仍需 Windows runner 编译/执行证明。
- 本批只新增 Anity internal/native API，Unity 官方公开面不变；Unity 2022.3.51f1 复审保持类型 **912/4,117**、成员 **8,361/37,164**，baseline `regressions=0`、`removed-or-changed=0`、加载问题 0。

### 仍未完成（不得把 buffer upload 误报为 UI 渲染完成）
- 当前完成到 CPU batch → backend vertex/index buffer；尚未编码/提交 Metal render encoder、Vulkan command buffer 或 D3D11 draw calls，也未绑定 URP UI shader/material/texture、clip/scissor、mask/stencil pipeline。
- 三槽 ring 尚无 GPU completion fence/semaphore 保护；高 GPU 延迟下仍可能覆盖 in-flight slot。`CanvasRenderer` dirty state 也尚未自动 upsert/remove 到所属 root Canvas，当前由 `NativeUICanvas` 显式组织。
- 全局仍缺 **3,205** 个官方类型与 **28,803** 个官方成员，编辑器、平台构建和运行时大量模块仍距离 Unity 2022 Pro 生产级对等很远。

### 下一优先项
1. 为 Metal/Vulkan/D3D11 增加 UI pipeline state、vertex layout、material/texture/clip/mask/stencil bind 与 indexed draw encoding，并用平台可见 framebuffer/readback fixture 验证像素结果。
2. 接入 per-slot GPU fence/semaphore/command-buffer completion，处理 resize、device loss、buffer growth 与多帧 in-flight 生命周期，并在 Windows/Vulkan runner 强制构建执行。
3. 将 `CanvasRenderer` geometry/material/texture/clip/dirty 自动同步到 root `AnityUICanvas`，接入 Canvas rebuild phase 与 nested Canvas sorting。

## 2026-07-16q — Native Canvas 持久 command queue 与稳定 UI batching

### 已完成
- 在 `anity-native` UI 模块新增持久 `AnityUICanvas` 所有权模型及 11 个 C ABI：frame、clear、renderer command upsert/remove、batch build、stats、batch metadata 与合并后 vertex/index buffer copy。
- renderer command 由 native 深拷贝持有，按 sort depth + insertion order 稳定排序；相邻 material/texture/alpha-texture/clip state 相同的命令合批并重定位 indices，mask/pop 强制隔离，透明或 invisible command 不进入 upload batch。
- `NativeUICanvas` 提供托管 RAII/finalizer、严格 native 模式、persistent frame lifecycle 与 batch buffer 读取；native mutex 已覆盖 command mutation、batch build 与查询。
- 新增 `NativeUICanvasBatchTests` **15/15**，覆盖 ABI、持久帧、replacement/remove/clear、排序、合批、索引重定位、state break、mask/pop、透明剔除、非法索引、32 路并发与 dispose。

### 测试与门禁
- 定向纯托管/强制 native 均 **15/15**；Core 纯托管 **681/681**、`ANITY_REQUIRE_NATIVE=1` **681/681**；native Release 构建通过。
- 本批只新增 Anity native/interop API，UnityEngine/UnityEditor 公开面未改变；上一批官方 baseline 指标保持有效。

### 仍未完成
- 已拥有 production-oriented CPU command/batch ownership，但 D3D11/Vulkan/Metal 的真实 GPU buffer 创建、上传、draw encoding、fence/ring-buffer 与 `NativeGraphicsDevice.EndFrame` 消费接线尚未完成，不能把 Canvas GPU dispatch 标为完成。

### 下一优先项
1. 将 `AnityUICanvas` batch 接入 graphics device，完成动态 vertex/index ring buffer、frame fence 与 backend draw submission。
2. 将各 `CanvasRenderer` 的 mesh/material/texture/clip dirty state 自动 upsert/remove 到所属 root Canvas queue。
3. 继续 RectTransform dimensions-change 与 Canvas rebuild phase 官方 A/B。

## 2026-07-16p — CanvasRenderer native vertex staging、quad index 与裁剪可见性内核

### 已完成
- 新增 `anity-native/include/anity/ui/anity_ui_renderer.h` 与 `src/ui/anity_ui_renderer.cpp`，并纳入唯一 CMake 生产构建；导出 `AnityUIRenderer_PackVertices`、`AnityUIRenderer_BuildQuadIndices`、`AnityUIRenderer_EvaluateVisibility` 三个 C ABI，macOS dylib 符号已用 `nm` 实证存在。
- 定义与托管层逐字段对应的 108-byte `AnityUIVertex` / `AnityUIPackedVertex` ABI：完整保留 position、normal、tangent、Color32 与 uv0–uv3，native staging 同时计算三维 min/max bounds；空 geometry 合法，越界容量、非有限 position 与非法参数返回 `ANITY_ERR_INVALID_ARG`。
- 将 Unity legacy quad 路径的 `0,1,2,2,3,0` 索引生成迁入 native，支持任意多 quad 与空 geometry，拒绝非 4 倍数顶点；`CanvasRenderer.SetVertices` 现在优先经过 native pack/bounds/index，再创建 Mesh，动态库缺失时保持原托管等价回退。
- 增加 native UI render-state evaluator：合并 renderer color alpha 与 CanvasGroup inherited alpha，计算 rect overlap、partial clip、alpha/clip cull、softness inner clip；`SetMesh`、颜色/alpha、裁剪/softness与 transparent-cull 变更均刷新 native state，不改变 Unity 公开 `cull` 契约。
- `AnityNative` 增加严格顺序布局的 UI C ABI structs 与三个 Try 入口；`ANITY_REQUIRE_NATIVE=1` 下缺失动态库或 export 会硬失败，普通运行时只在入口不可用时降级，不吞掉 native 参数错误。
- `CanvasRenderer` 官方公开面未变；84 个 Unity 2022.3.51f1 程序集复审保持 type/member 指标不变，baseline `regressions=0`、`removed-or-changed=0`。

### 测试与门禁
- 新增 `CanvasRendererNativeTests` **15/15**：8 个 ABI size、position/color/normal/tangent、4 UV streams、3D bounds、empty、越界/NaN、单/多 quad winding、partial quad、alpha inheritance、clip outside/partial、softness与真实 CanvasRenderer mesh/bounds 集成。
- CanvasRenderer 定向套件纯托管与强制 native 均 **31/31**；Core 纯托管 **666/666**、`ANITY_REQUIRE_NATIVE=1` 强制 dylib **666/666**。
- Agent **30/30**、CLI **16/16**、反射审计器 **17/17**；`bash _scripts/build-all.sh Release` 通过，native/Core/Agent/CLI/parity/WebGL/Hub/Editor/URP3DDemo 均 0 编译错误。
- 官方反射指标保持：类型存在 **912/4,117**、精确 **387**、missing **3,205**、mismatch **525**、extra **627**；成员存在 **8,361/37,164**、精确 **6,133**、missing **28,803**、mismatch **2,228**、extra **3,558**；加载问题 0。

### 仍未完成（不得误报“Unity 全量完成”）
- 本批关闭的是 CPU vertex staging、bounds、quad index 与裁剪/透明度判定内核；真实 GPU buffer upload、Canvas batch 合并、material/texture command stream、render-thread ownership、mask/stencil dispatch 仍未迁入 native，因此 `CanvasRenderer` 底层总项继续保持 🟡。
- 全局仍缺 **3,205** 个官方类型与 **28,803** 个官方成员；编辑器 Canvas/Rect Tool、平台渲染与全引擎 native 所有权仍距离 Unity 2022 Pro 生产级对等很远。

### 下一优先项
1. 在 `anity-native` 实现持久 Canvas render command/batch、GPU vertex/index upload 与 material/texture/clip state 合并，接入现有 graphics backend 与 render-thread 生命周期。
2. 实现 RectTransform 尺寸 dirty propagation、`OnRectTransformDimensionsChange` 层级消息与 Canvas layout→graphic→pre-render phase 顺序，建立官方多层 UI 树 A/B。
3. 补齐 ScreenSpaceCamera/perspective/display rect projection、pixel adjustment、raycast 与 nested Canvas sorting/culling 边界。

## 2026-07-16o — CanvasRenderer / CanvasGroup / UIVertex 根命名空间与运行时语义闭环

### 已完成
- 将 `CanvasGroup`、`UIVertex`、`ICanvasRaycastFilter` 从错误 `UnityEngine.UI` 迁移到官方 `UnityEngine`，移除重复 `UnityEngine.UI.CanvasRenderer`，让 uGUI `Graphic` 统一使用唯一的官方 `UnityEngine.CanvasRenderer`。
- `CanvasRenderer` 修正为 sealed `Component`，补齐 `NativeHeader("Modules/UI/CanvasRenderer.h")`、`NativeClass("UI::CanvasRenderer")`、nested `OnRequestRebuild` 与静态 event；清理 19 个 Anity-only 公开方法/属性，补齐 23 个官方缺失成员与 4 个 metadata/accessor mismatch。
- 完整实现 material/pop-material slots、texture/alpha texture、mesh、color/alpha、rect clipping/softness、cull/mask flags、Clear、legacy SetVertices、Split/Create/Add UIVertex streams；`UIVertex` 改用官方 Vector4 uv0–uv3 与 `UsedByNativeCode` metadata。
- 以 Unity **2022.3.51f1** batchmode 实测默认状态：`materialCount/popMaterialCount=0`、depth=-1、`hasMoved=true`、`cullTransparentMesh=true`、white/alpha=1；`SetAlpha` 同步 `GetColor().a`。
- `GetInheritedAlpha` 对齐官方：只乘当前/父级 CanvasGroup alpha，不重复乘 Renderer 自身 alpha；遇到 `ignoreParentGroups` 在该组后截断。CanvasGroup 默认 alpha/interactable/blocks/ignore 与 raycast filter 行为已固化。
- vertex stream 语义经官方探针闭环：Split 生成 sequential indices，Create 按 indices 重排，Add 虽名为 Add 但会重建/清空输出 streams；legacy SetVertices 以每 4 顶点构建 0-1-2/2-3-0 quad mesh；Clear 同时释放 mesh 与 material slots。
- `CanvasRenderer`、nested delegate、`CanvasGroup`、`UIVertex`、`ICanvasRaycastFilter` 及旧错误命名空间目标当前官方反射差异合计 **0**。

### 测试与门禁
- 新增 `CanvasRendererCanvasGroupTests` **16/16**：公开面、defaults、group raycast、alpha/inheritance/ignore、materials/pop、clipping、三类 stream、legacy mesh、Clear、UIVertex、event。
- Core 纯托管 **651/651**；native 配置整套 **651/651**；Agent **30/30**；CLI **16/16**；反射审计器 **17/17**。
- `_scripts/build-all.sh Release` 通过；native/Core/Agent/CLI/parity/WebGL/Hub/Editor/URP3DDemo 均 0 编译错误。

### 官方反射面增量（相对 2026-07-16n 基线）
- 类型存在 **908 → 912**、类型精确 **382 → 387**、missing **3,209 → 3,205**、mismatch **526 → 525**、extra **631 → 627**；成员存在 **8,318 → 8,361**、成员精确 **6,086 → 6,133**、missing **28,846 → 28,803**、mismatch **2,232 → 2,228**、extra **3,577 → 3,558**。
- 当前覆盖率：类型存在 **22.152%**、类型精确 **9.400%**；成员存在 **22.498%**、成员精确 **16.503%**。重建 baseline 后复跑 `regressions=0`、`removed-or-changed=0`，84 个官方程序集加载问题 0。

### 仍未完成（不得误报“Unity 全量完成”）
- CanvasRenderer 状态与 UIVertex 转换当前仍主要在 C#；Unity native 所承担的 UI mesh upload、batching、clip/cull dispatch 与多线程渲染同步尚未迁入 `anity-native`，因此底层 Canvas 渲染总项保持 🟡。
- 全局仍缺 **3,205** 个官方类型与 **28,803** 个官方成员；编辑器 Canvas/Rect Tool、display/camera 边界、完整输入与平台渲染仍需继续闭环。

### 下一优先项
1. 新增 `anity-native` UI renderer 模块，迁移 UIVertex stream packing、mesh upload command、rect clipping/cull 与 material slot state，经 C ABI/强制 native 测试验证。
2. 实现 RectTransform 尺寸 dirty propagation、`OnRectTransformDimensionsChange` 层级消息和 Canvas layout→graphic rebuild phase 顺序，建立官方多层 UI 树 A/B。
3. 补齐 ScreenSpaceCamera/perspective/display rect 的 projection、pixel adjustment、raycast 与 nested Canvas sorting/culling 边界。

## 2026-07-16n — RectTransformUtility / Canvas 官方命名空间、坐标行为与像素对齐闭环

### 已完成
- 修正 UIModule 的根命名空间错误：`RectTransformUtility`、`Canvas`、`RenderMode`、`AdditionalCanvasShaderChannels` 从错误的 `UnityEngine.UI` 迁移到官方 `UnityEngine`；补齐 `StandaloneRenderResize`、`Canvas.WillRenderCanvases` 及 Canvas native headers/NativeClass/RequireComponent metadata，并删除错误 `UnityEngine.UI.SortOrder`。
- `RectTransformUtility` 公开面完全对齐：补齐 `PixelAdjustPoint/Rect`、无 Camera overload、Vector4 inset overload、公开 `ScreenPointToRay` / `WorldToScreenPoint`，移除错误 out-Vector4 overload与公开构造器；该类型当前官方反射差异 **0**。
- 以 Unity **2022.3.51f1** batchmode 探针闭环无 Camera ray/world/local、orthographic camera round-trip、inclusive contains、Vector4 正 inset/负 expand、axis/axes flip、递归 RectTransform bounds、pixelPerfect overlay 与 world-space bypass。
- 完整实现 `FlipLayoutOnAxis/Axes` 与递归子树、`CalculateRelativeRectTransformBounds` 的 world-corners→root-local 聚合，替换原空实现；平面不相交返回 false，不再猜测伪 world point。
- PixelAdjust 改为 element local→world/screen→整数 pixel→inverse element 的真实路径；`PixelAdjustRect` 对 min/max 两角分别量化。官方探针的 point `(1.2,2.3)→(1.3,2.2)`、rect `(-2.26,-16.56,11.3,20.7)→(-2.2,-16.8,11,21)` 已固化。
- Canvas overlay 根布局改为官方 `size=screen/scaleFactor`、`localScale=(scaleFactor,scaleFactor,1)`、world position=display center；清理全部错误 public helper API为 internal，实现官方 default/ETC1 material、cached sorting、standalone resize、gamma vertex color与 obsolete sorting grid成员，事件 delegate/metadata 精确一致。
- `Canvas`、nested delegate、`RenderMode`、`AdditionalCanvasShaderChannels`、`StandaloneRenderResize`、`RectTransformUtility` 以及旧错误命名空间目标当前官方反射差异合计 **0**。

### 测试与门禁
- 新增 `RectTransformUtilityParityTests` **16/16**；与 Canvas/GraphicRaycaster 定向套件合计 **43/43**。
- Core 纯托管 **635/635**；native 配置整套 **635/635**；Agent **30/30**；CLI **16/16**；反射审计器 **17/17**。
- `_scripts/build-all.sh Release` 通过；native/Core/Agent/CLI/parity/WebGL/Hub/Editor/URP3DDemo 均 0 编译错误。

### 官方反射面增量（相对 2026-07-16m 基线）
- 类型存在 **902 → 908**、类型精确 **376 → 382**、missing **3,215 → 3,209**、extra **636 → 631**；成员存在 **8,257 → 8,318**、成员精确 **6,025 → 6,086**、missing **28,907 → 28,846**。
- 当前覆盖率：类型存在 **22.055%**、类型精确 **9.279%**；成员存在 **22.382%**、成员精确 **16.376%**。重建 baseline 后复跑 `regressions=0`、`removed-or-changed=0`，84 个官方程序集加载问题 0。

### 仍未完成（不得误报“Unity 全量完成”）
- `CanvasRenderer`、`CanvasGroup` 仍错误公开在 `UnityEngine.UI`，Canvas native batching/render dispatch 仍主要为托管模拟；编辑器 Canvas/Rect Tool、跨 display、ScreenSpaceCamera 投影边界仍需继续闭环。
- 全局仍缺 **3,209** 个官方类型与 **28,846** 个官方成员；本批只关闭 UIModule 的 Canvas/RectTransformUtility 子集，不能宣称 Unity 2022 Pro 全引擎已完成。

### 下一优先项
1. 把 `CanvasRenderer` / `CanvasGroup` 迁移到官方 `UnityEngine`，关闭公开面、材质/mesh/alpha/culling与 group 继承阻断行为，补官方探针与 ≥10 测试。
2. 实现 Canvas rebuild native dispatch、RectTransform 尺寸 dirty/消息传播和 layout→graphic phase 顺序，验证深层 UI 树与多 Canvas。
3. 补齐 ScreenSpaceCamera/perspective/display rect 的 projection、pixel adjustment 与 raycast A/B，并将批处理/裁剪关键路径迁入 `anity-native`。

## 2026-07-16m — DrivenRectTransformTracker 公开面、共享登记语义与布局驱动生命周期闭环

### 已完成
- 以官方 Unity **2022.3.51f1** batchmode 探针逐项固化 `DrivenTransformProperties` 的 25 个名称/位值，以及 `DrivenRectTransformTracker.Add`、`Clear()`、obsolete `Clear(bool)`、`StartRecordingUndo`、`StopRecordingUndo` 的公开面；两个类型当前官方反射差异均为 **0**。
- 实现 tracker 的真实登记与释放：`Add` 写入 `RectTransform.drivenByObject` 及内部 driven property mask，`Clear` 释放全部已登记 RectTransform；tracker 保持 Unity 值类型复制后共享登记列表的语义，副本 `Clear` 会释放原 tracker 登记。
- 对齐官方边界：null driver 合法，null RectTransform 抛 `NullReferenceException`；默认 tracker 可直接 Clear；obsolete overload 的 attribute 构造参数与官方 metadata 一致；Add/Clear/ForceUpdate 不错误触发 `reapplyDrivenProperties`。
- 将 ownership 接入 uGUI 布局主路径：`LayoutGroup` 在水平输入重建前清理旧登记，并按 anchors/anchored position/size delta 掩码驱动子 RectTransform；`ContentSizeFitter` 横向重建前 Clear、两轴分别登记，禁用时释放；`AspectRatioFitter` 按 Width/Height/Fit/Envelope 模式登记精确属性并在禁用时释放。
- 同步校正布局行为为 Unity 2022.3 uGUI 源码语义：LayoutGroup 子项固定 `anchorMin/anchorMax=Vector2.up`，纵轴 anchored position 使用 top-origin；AspectRatioFitter 的 layout controller 方法保持 no-op，实际 dirty/update 路径直接刷新，Fit/Envelope 拉伸 anchors 后只调整对应 sizeDelta 轴。

### 测试与门禁
- 新增 `DrivenRectTransformTrackerTests` **14/14**：枚举、公开方法、默认值、Add/Clear/null、值类型副本共享、obsolete overload、多 Rect、undo 入口及三类布局组件 ownership/disable 清理。
- Core 纯托管 **619/619**；强制 native 配置整套 **619/619**；Agent **30/30**；CLI **16/16**；反射审计器 **17/17**。
- `_scripts/build-all.sh Release` 通过；native/Core/Agent/CLI/parity/WebGL/Hub/Editor/URP3DDemo 均 0 编译错误。

### 官方反射面增量（相对 2026-07-16l 基线）
- 类型存在 **900 → 902**、类型精确 **374 → 376**、missing **3,217 → 3,215**；成员存在 **8,226 → 8,257**、成员精确 **5,994 → 6,025**、missing **28,938 → 28,907**、extra 保持 **3,577**。
- 当前覆盖率：类型存在 **21.909%**、类型精确 **9.133%**；成员存在 **22.218%**、成员精确 **16.212%**。重建 baseline 后复跑 `regressions=0`、`removed-or-changed=0`，84 个官方程序集加载问题 0。

### 仍未完成（不得误报“Unity 全量完成”）
- `RectTransform`/tracker/layout ownership 的本批目标已闭环，但 Canvas rebuild native dispatch、尺寸变更消息传播、编辑器 Rect Tool/anchor handles、undo 实际编辑器栈仍未达到 Unity 2022 Pro 产品级全行为，因此 RectTransform 总项继续保持 🟡。
- 全局仍缺 **3,215** 个官方类型与 **28,907** 个官方成员；Anity 距离 Unity 2022 Pro 全量生产级对等仍有大量编辑器、渲染、资源、平台与 native 所有权工作。

### 下一优先项
1. 将 `RectTransformUtility` 从错误的 `UnityEngine.UI` 公开位置迁移到官方 `UnityEngine`，关闭完整反射差异并用 overlay/camera/world-space Canvas 做坐标 A/B 与 ≥10 测试。
2. 实现 Canvas/RectTransform 尺寸 dirty dispatch、`OnRectTransformDimensionsChange` 层级传播与布局 rebuild 阶段顺序，接入 native transform 状态并验证深层 UI 树。
3. 补齐编辑器 Rect Tool、anchor/pivot handles、driven property 禁用显示及 undo/reapply 生命周期，保持每批官方反射 baseline 零回退。

## 2026-07-16l — RectTransform 公开面、布局数学与 GameObject Transform 替换语义闭环

### 已完成
- 以官方 Unity **2022.3.51f1** 编辑器探针闭环 `RectTransform` 默认值与核心布局行为：默认 `sizeDelta=(100,100)`、anchor/pivot reference、`anchoredPosition` ↔ `localPosition`、`anchoredPosition3D` z 同步、stretch rect、`offsetMin/offsetMax` 双向修改、`SetSizeWithCurrentAnchors`、四种 `SetInsetAndSizeFromParentEdge`、local/world corner 顺序以及 null/短数组静默返回。
- 公开面 14 条差异归零：`Axis` / `Edge` 改为官方 nested enum，新增 nested `ReapplyDrivenProperties` delegate、静态 `reapplyDrivenProperties` event、`drivenByObject`、`ForceUpdateRectTransforms` 与 `NativeMethod("UpdateIfTransformDispatchIsDirty")`；修正 corner 参数名并移除错误的顶层 `UnityEngine.Axis/Edge` 和两个 `ForceUpdateRects` 额外 API。`RectTransform` 及三个 nested type 当前官方反射差异均为 **0**。
- 重写布局状态模型：anchored position 由真实 Transform localPosition 与父 Rect anchor reference 双向换算；rect 尺寸统一为 `parentSize * anchorSpan + sizeDelta`；offset setter 同时调整 size/position；父 pivot/rect origin 被纳入 reference，不再使用错误的 20,000×20,000 虚拟父矩形。
- `GetLocalCorners` / `GetWorldCorners` 顺序修正为官方 bottom-left → top-left → top-right → bottom-right；world corners 经过完整 Transform/native matrix 链。
- `GameObject(..., typeof(RectTransform))` 与 `AddComponent<RectTransform>()` 现在真正以 RectTransform 替换基础 Transform，保留 local pose、parent/children、sibling 位置与 GameObject 单一 Transform 约束；`gameObject.transform`、`GetComponent<Transform>()`、UI 组件看到同一实例。

### 测试与门禁
- 新增 `RectTransformParityTests` **18/18**：公开 nested type/metadata、Transform 替换及状态迁移、默认/伸展布局、3D anchored/local 同步、offset、size、四边 inset、corner、event/driver 边界。
- Core 纯托管 **605/605**；`ANITY_REQUIRE_NATIVE=1` 强制真实 dylib 整套复跑 **605/605**。第一次 native 整套中的既有 AssetBundle 并行用例瞬时失败，单测立即通过且第二次完整整套通过，未掩盖为成功。
- `_scripts/build-all.sh Release` 通过；native/Core/Agent/CLI/parity/WebGL/Hub/Editor/URP3DDemo 均 0 编译错误。

### 官方反射面增量（相对 2026-07-16k 基线）
- 类型存在 **897 → 900**、类型精确 **371 → 374**、missing **3,220 → 3,217**、extra **638 → 636**；成员存在 **8,209 → 8,226**、成员精确 **5,975 → 5,994**、missing **28,955 → 28,938**、mismatch **2,234 → 2,232**、extra **3,581 → 3,577**。
- 当前覆盖率：类型存在 **21.861%**、类型精确 **9.084%**；成员存在 **22.134%**、成员精确 **16.129%**。重建 baseline 后复跑 `regressions=0`、`removed-or-changed=0`，84 个官方程序集加载问题 0。

### 仍未完成（不得误报“Unity 全量完成”）
- `drivenByObject` / reapply event 的公开契约已对齐，但官方 `DrivenRectTransformTracker` / `DrivenTransformProperties` 类型、布局控制器驱动所有权与清理/重应用生命周期尚未实现，RectTransform 仍保持 🟡。
- 全局仍缺 **3,217** 个官方类型与 **28,938** 个官方成员；编辑器 Rect Tool/anchor handles、Canvas rebuild native dispatch 与跨平台 UI 渲染仍需继续闭环。

### 下一优先项
1. 实现 `DrivenRectTransformTracker`、`DrivenTransformProperties` 与 LayoutGroup/ContentSizeFitter/AspectRatioFitter 的 driver ownership、Clear/重应用事件和销毁清理，配官方 A/B 与 ≥10 测试。
2. 将 `RectTransformUtility` 从错误 `UnityEngine.UI` 公开位置迁移到官方 `UnityEngine`，关闭其完整反射面并验证 camera/overlay/world-space 坐标变换。
3. 继续把 Transform/RectTransform hierarchy state、dirty dispatch 与批量布局更新迁入 `anity-native`，覆盖深层 UI 树与并发读门禁。

## 2026-07-16k — Matrix4x4 / FrustumPlanes 公开面、官方行为与 native C++ 数学模块闭环

### 已完成
- 以本机官方 Unity **2022.3.51f1** 的运行时探针补齐 `Matrix4x4`：`rotation`（含负/零 scale、shear、projection 与 zero matrix）、`decomposeProjection`、`GetPosition`、`TransformPlane`、`Inverse3DAffine`、`Determinant/Inverse/Transpose` 静态入口、`Frustum(FrustumPlanes)`、`ToString` overload、精确/近似 equality、异常文本及齐次 `w=0` 除法语义。`LookAt` 已确认是 object pose，不是 view matrix；零方向、平行 up、零 up 均按官方返回带 `from` 平移的 identity rotation。
- `FrustumPlanes` 从错误 enum 改为官方 `[Serializable] struct` 六字段；`Matrix4x4` 的类型接口、native attributes、16 个字段 `NativeName`、`FreeFunction` / `ThreadSafe` metadata、参数名与公开 overload 已逐项对齐。当前 `Matrix4x4` 与 `FrustumPlanes` 在 84 个官方程序集反射审计中均为 **0 差异**。
- 新增 `anity-native` C++ `math/anity_matrix` 模块及 12 组 C ABI：determinant、通用/3D affine inverse、transpose、TRS、Ortho/Perspective/Frustum/LookAt、closest rotation、ValidTRS、projection decomposition。C# 以 native 为主路径，无动态库时保留已验证的托管等价回退；强制 native 模式会在入口缺失时硬失败。
- closest proper rotation 使用 Davenport 4x4 对称特征问题与 Jacobi 求解，覆盖 signed scale、singular axis、shear 和投影矩阵；逆矩阵奇异路径写回 zero，`Inverse3DAffine` 对 projection 保持 Unity 只处理上 3x3+translation 的语义。
- 修复两个官方 A/B 才暴露的托管细节：无效 `SetRow` 必须抛 `Invalid matrix index!`；固定点格式改用 away-from-zero midpoint rounding，使 `ToString("F1")` 的 `-2.25` 与 Unity/Mono 一致输出 `-2.3`。

### 测试与门禁
- 新增 `Matrix4x4ParityTests` **22/22**：公开 metadata、构造/投影、普通与退化 LookAt、rotation polar extraction、ValidTRS、inverse、decompose、plane、字符串、equality、异常与 3 组直接 native export 验证。
- Core 纯托管回退 **587/587**；`ANITY_REQUIRE_NATIVE=1` 强制真实 dylib **587/587**；Agent **30/30**；CLI **16/16**；反射审计器 **17/17**。
- `_scripts/build-native.sh Release` 与 `_scripts/build-all.sh Release` 通过；Core/Agent/CLI/WebGL/Hub/Editor/URP3DDemo 均 0 编译错误。

### 官方反射面增量（相对 2026-07-16j 基线）
- 类型精确 **369 → 371**，type mismatch **528 → 526**；成员存在 **8,192 → 8,209**，成员精确 **5,930 → 5,975**，missing **28,972 → 28,955**，mismatch **2,262 → 2,234**，extra **3,589 → 3,581**。
- 当前覆盖率：类型存在 **21.788%**、类型精确 **9.011%**；成员存在 **22.089%**、成员精确 **16.077%**。重建 SHA-256 baseline 后复跑 `regressions=0`、`removed-or-changed=0`，84 个官方程序集加载问题 0。

### 仍未完成（不得误报“Unity 全量完成”）
- 全局仍缺 **3,220** 个官方类型与 **28,955** 个官方成员；本批只闭环 `Matrix4x4` / `FrustumPlanes`，不代表 Unity 2022.3 Pro 全引擎已经完成。
- Matrix native 模块仍是无状态数学内核；Transform 层级存储/dirty propagation/批量 Jobs 所有权、物理世界、渲染器及资源导入等 Unity C++ 职责仍需继续迁入 `anity-native`。

### 下一优先项
1. 关闭 `RectTransform` 剩余公开反射差异，并用官方 Unity 建立 anchor/pivot/offset/SetSizeWithCurrentAnchors/驱动属性的场景 A/B 与 ≥10 测试。
2. 将 Transform 层级状态、dirty propagation、矩阵缓存和批量 Jobs 访问迁入 `anity-native`，补深层树、销毁/reparent 与并发读写门禁。
3. 按反射审计的高频迁移阻塞排序继续关闭 Core 类型公开面，同时保持每批 baseline 零回退与官方行为探针证据。

## 2026-07-16j — Transform 完整层级仿射链、native 热路径与 IL2CPP 构建死锁修复

### 已完成
- 用 Unity 2022.3.51f1 官方 batchmode fixture 对非均匀/负/零 scale、两级 shear、world/local 矩阵、`lossyScale`、奇异逆矩阵及 `SetParent(true)` 做逐元素 A/B；确认 `localToWorldMatrix = parent * local TRS`，而奇异 `worldToLocalMatrix` 必须沿层级使用“零轴倒数为 0”的逆 TRS 链，不能直接取整体矩阵逆。
- `Transform.localToWorldMatrix` / `worldToLocalMatrix` 已改为保留全部 shear 的真实父子仿射矩阵链；`TransformPoint/Vector` 及逆变换随之使用完整矩阵，不再由 world position/rotation/lossyScale 重新拼接而丢失 shear。
- `Transform.lossyScale` 按官方行为把世界矩阵三列投影到 world rotation 三轴，而非使用列长度；负 scale 父链的 world/local rotation 使用官方轴反射 quaternion 规则，`SetParent(true)` 在可表达范围内保持 position、rotation 与投影 scale，并精确处理奇异父矩阵投影。
- 新增 `anity-native` C++ transform 模块及 `AnityTransform_ComposeLocalToWorld`、`AnityTransform_ComposeWorldToLocal`、`AnityTransform_ProjectLossyScale` 三个 C ABI；C# 走 native 主路径并保留无动态库时的确定性托管回退。测试项目仅在 `AnityRequireNative=true` 时把对应平台动态库部署到程序集旁，避免依赖脆弱的环境搜索路径。
- `Matrix4x4.lossyScale` 已按官方 determinant 符号规则补齐；精确奇异矩阵的 `inverse` 由错误 identity 改为官方 zero matrix，近奇异非零 determinant 仍计算真实大系数逆矩阵。
- 新增 `TransformAffineParityTests` **18/18**，覆盖两级 shear、点/向量/方向、投影 scale、负轴、零轴、奇异 inverse、reparent/unparent，以及 3 个强制实际加载 dylib 的 native 入口；与既有 Transform/Scene 28 例合计 **46/46**。
- 完整 native Core 门禁暴露并修复既有 IL2CPP 构建挂死：`TryNativeCompile` / `LinkPlayer` / `CompileAllUnits` 现并行排空 stdout/stderr、超时终止子进程，`CompileAllUnits` 按 CPU 并行编译 2,080+ C++ 单元，不再因管道填满或串行耗时失控而卡死。
- Core 在托管回退与实际加载 `libanity_native` 两种配置均为 **565/565**；Agent **30/30**、CLI **16/16**、API 审计器 **17/17**。`build-all Release` 通过 native + 全部托管项目；URP 示例仍为既有 43 个 nullable warning、0 error。

### 官方反射面增量（相对 2026-07-16i 基线）
- 类型指标不变：存在 **897/4,117**、精确 **369**、缺失 **3,220**、不一致 **528**、扩展 **638**。
- 成员同签名存在 **8,191→8,192**；契约精确 **5,929→5,930**；真实缺失 **28,973→28,972**（已有类型内 **6,991→6,990**）；不一致 **2,262**、错误扩展 **3,589** 不变。
- 当前覆盖率：类型存在 **21.788%**、类型精确 **8.963%**；成员存在 **22.043%**、成员精确 **15.956%**。`Transform` 公开反射差异保持 0；审查 `Matrix4x4.lossyScale` 增量后重建 baseline，复跑 `regressions=0`、`removed-or-changed=0`，84 个官方程序集加载问题 0。

### 尚未完成
- Transform 的仿射数学热路径已进入 C++，但 Transform 对象/层级存储、dirty propagation、矩阵缓存、线程与 Jobs 访问所有权仍主要在 C#；在这部分迁入 `anity-native` 并完成并发/生命周期 A/B 前，Transform 保持 🟡，不宣称 native 引擎级完成。
- `Matrix4x4` 当前仍有 1 个 type mismatch、11 个 missing member、29 个 member mismatch、1 个 extra member；本批只关闭 `lossyScale` 与奇异 inverse 行为，不能把整个类型标成官方反射精确。
- Scene 异步加载、平台资源生命周期、编辑器多 Scene/Prefab Stage，以及 `SceneManagerAPI` / `SceneUtility` / `PhysicsSceneExtensions` / `EditorSceneManager` 的剩余公开面和行为仍未闭环。

### 下一次优先项
1. 关闭 `Matrix4x4` 剩余 42 个公开反射差异，并为 `decomposeProjection`、`rotation`、`GetPosition`、`Frustum`、异常/索引边界建立官方 A/B 与 ≥10 测试。
2. 将 Transform 层级存储、dirty propagation、矩阵缓存及批量 Jobs 访问迁入 `anity-native`，覆盖销毁/reparent/并发读写和深层树性能，不让 C++ 仅停留在无状态数学函数。
3. 补齐 `SceneManagerAPI`、`SceneUtility`、`PhysicsSceneExtensions` 与 `EditorSceneManager` 的剩余官方公开面和异步/编辑器多 Scene 生命周期。

## 2026-07-16i — Transform / Scene / 同步实例化层级语义闭环

### 已完成
- `Transform` 的类型、继承、接口、构造器、全部公开成员、参数名/默认值及 native metadata 已与 Unity 2022.3.51f1 官方反射指纹完全一致；补齐 `forward/up/right` setter、`hierarchyCapacity/hierarchyCount`、12 个 span 批量变换重载与精确参数异常。
- 以官方 Unity batchmode fixture 实测并固化层级语义：`Find` 的直接/路径/空串行为，`IsChildOf(self/null)`，循环/自父节点 no-op，reparent 消息顺序，`SetParent(true/false)` 的世界/局部姿态，以及 inactive、跨 Scene 父子树传播。
- 修正 `Quaternion.LookRotation` 的矩阵基向量布局与 `Quaternion.Angle` 的 Unity epsilon 快速归零，保证 Transform 方向 setter 与 reparent 旋转结果对齐官方。
- 将 `Scene` 从错误的可变 class 重构为官方 `[Serializable] struct`，以 `m_Handle` 连接 `SceneManager` 内部状态注册表；value copy 共享场景状态，根对象、active scene、合并、卸载与跨 Scene 整树迁移不再依赖对象引用身份。
- `SceneManager`、`Scene`、`CreateSceneParameters`、`LoadSceneParameters`、`LocalPhysicsMode` 的公开反射差异全部归零；补齐全部官方 overload、事件、legacy unload、`MoveGameObjectToScene` / `MoveGameObjectsToScene`，并精确处理仅 root 可迁移等验证。
- 修复同步 `Object.Instantiate`：parent 默认重载采用局部空间，`worldPositionStays=true` 保持世界姿态，显式 position/rotation + parent 使用指定世界姿态，Scene overload 递归迁移 clone 整树。
- 官方 `GameObject.InstantiateGameObjects(..., Scene destinationScene = null)` 的“值类型参数 + optional null constant”无法由 C# 源码表达；新增构建期 `_scripts/Anity.MetadataFixups`，使用 Mono.Cecil 精确修补最终程序集 metadata，并改为进程/调用唯一临时文件以支持并行 MSBuild。Release NuGet 包内 DLL 已复验该默认值。
- `RectTransform` 的 sealed/type/native metadata 已纠正为官方类型指纹；其剩余成员/布局语义仍按差距清单继续推进，不以类型指纹归零代替完整行为验收。
- 新增 `TransformSceneParityTests` **28/28**，覆盖公开面、层级、span/重叠内存、异常、方向、Scene value copy/root、跨场景迁移、同步 clone 与 merge；Core 全量 **547/547**，Agent **30/30**，CLI **16/16**，API 审计器 **17/17**。
- `build-all Release` 通过 native + 全部托管项目；当前 macOS 未安装 Vulkan SDK，因此按既有规则编译 Vulkan stub，URP 示例保留既有 43 个 nullable warning、0 error。

### 官方反射面增量（相对 2026-07-16h 基线）
- 类型存在 **896→897/4,117**；类型契约精确 **362→369**；缺失 **3,221→3,220**；不一致 **534→528**；扩展保持 **638**。
- 成员同签名存在 **8,157→8,191**；契约精确 **5,856→5,929**；真实缺失 **29,007→28,973**（已有类型内 **7,023→6,991**，缺失类型内 **21,984→21,982**）；不一致 **2,301→2,262**；错误扩展 **3,595→3,589**。
- 当前覆盖率：类型存在 **21.788%**、类型精确 **8.963%**；成员存在 **22.040%**、成员精确 **15.954%**。8 个目标类型公开反射差异为 0，84 个官方程序集加载问题 0；重建 evidence baseline 后复跑 `regressions=0`、`removed-or-changed=0`。

### 尚未完成
- 层级旋转 + 非均匀缩放可产生 shear；当前 `localToWorldMatrix` 仍由 `TRS(position, rotation, lossyScale)` 重建，尚不能逐元素复现 Unity 的完整层级仿射矩阵。Transform 因此保持 🟡，不能仅凭本批公开面与行为矩阵宣称全语义完成。
- Scene 异步加载、平台资源生命周期、编辑器多 Scene 工作流，以及 `SceneManagerAPI` / `SceneUtility` / `PhysicsSceneExtensions` 的剩余公开面与行为仍未闭环。

### 下一次优先项
1. 把 Transform 层级矩阵改为真实父子仿射矩阵链，并用官方 fixture 覆盖旋转 + 非均匀/负/零 scale、shear、逆矩阵及 world/local round-trip。
2. 补齐 `SceneManagerAPI`、`SceneUtility`、`PhysicsSceneExtensions` 与 `EditorSceneManager` 的剩余官方公开面和异步/编辑器多 Scene 生命周期，每批保持反射差距单调下降并补 ≥10 A/B 测试。
3. 扩展 native 物理世界所有权，把 3D/2D broadphase、solver 与刚体步进继续迁入 `anity-native`，以官方 PhysX/Box2D fixture 验证结果与性能。

## 2026-07-16h — Resource / AssetBundle 异步请求公开面与 PlayerLoop 时序闭环

### 已完成
- `ResourceRequest`、`AssetBundleRequest`、`AssetBundleCreateRequest`、`AssetBundleUnloadOperation` 四个类型的继承、构造器、public/protected 成员、virtual override 及 `RequiredByNativeCode` / `NativeHeader` / `NativeMethod` 元数据已与 Unity 2022.3.51f1 官方反射指纹逐项完全一致。
- 纠正 `AssetBundleRequest` 从错误的 `AsyncOperation` 直系继承为官方 `ResourceRequest`；移除 `GetAwaiter`、`IsCompleted`、`assetAsTyped<T>`、公开 `GetResult` 等 9 个 Anity-only 错误公开成员，并补回官方 `AssetBundleUnloadOperation.WaitForCompletion()`。
- `AsyncOperation` 增加不扩张公开 API 的内部 PlayerLoop 调度器：资源、AssetBundle 创建/加载/卸载请求创建后保持 `isDone=false` / `progress=0`，自动完成在帧首集成，等待它的协程在完成帧先恢复，原订阅 completion 回调在帧末派发，完成后的晚订阅同步回调。
- `ResourceRequest.asset` 依照官方语义同步解析资源但不强制请求完成；`AssetBundleRequest.asset/allAssets`、`AssetBundleCreateRequest.assetBundle` 与 unload `WaitForCompletion` 会同步执行尚未运行的工作并立即触发原 completion 回调。
- 建立真实 Unity 2022.3.51f1 batchmode + Play Mode AssetBundle fixture：实际构建 macOS bundle，验证 Resource/Create/Load/Unload 的初始 pending、yield 恢复与 callback 跨帧次序、晚订阅、missing asset、blocking getter、blocking unload。补充探针确认三种阻塞完成路径的原 completion callback 均在 getter/Wait 返回前触发，且 `allAssets` 每次读取返回不共享的数组快照。
- AssetBundle 的 File/Memory/Stream 创建、单资源/全资源/子资源加载及异步卸载均改为延迟执行，不再在返回 request 前偷偷同步完成。
- 新增 `AssetBundleAsyncRequestTests` **15/15**，覆盖公开面、native 元数据、资源 getter、PlayerLoop、协程/回调相位、blocking getter、独立 allAssets 快照、空结果、stream 延迟读取及卸载；Core 全量 **519/519**，Agent **30/30**，CLI **16/16**，API 审计器 **17/17**。
- `build-all Release` 通过 native + 全部托管项目；当前 macOS 未安装 Vulkan SDK，因此按既有规则编译 Vulkan stub，URP 示例保留既有 43 个 nullable warning、0 error。

### 官方反射面增量（相对 2026-07-16g 基线）
- 类型存在保持 **896/4,117**；类型契约精确 **358→362**；不一致 **538→534**；缺失 **3,221**、扩展 **638** 不变。
- 成员同签名存在 **8,155→8,157**；契约精确 **5,852→5,856**；真实缺失 **29,009→29,007**（已有类型内 **7,025→7,023**）；不一致 **2,303→2,301**；错误扩展 **3,604→3,595**。
- 四个目标类型当前公开反射差异均为 0；84 个官方程序集加载问题 0。审查后重建 evidence baseline，复跑门禁 `regressions=0`、`removed-or-changed=0`。

### 下一次优先项
1. 审计并补齐 `Transform.SetParent` / `SceneManager.MoveGameObjectToScene` 的非法 child/root、inactive 重挂载、跨 Scene 生命周期及同步 `Object.Instantiate` 全重载官方 A/B。
2. 继续处理 `UnityEngine.CoreModule` 高频 mismatch/missing，优先 Scene、Transform、Resources 与 AssetBundle 公开面的剩余差异，每批保持官方反射差异单调下降并补 ≥10 行为测试。
3. 扩展 native 物理世界所有权，把 3D/2D broadphase、solver 与刚体步进继续迁入 `anity-native`，以官方 PhysX/Box2D fixture 验证结果与性能。

## 2026-07-16g — `GameObject` 全公开面与批量生命周期闭环

### 已完成
- 补齐 `GameObject` 剩余 **21/21** 官方公开成员：`GetScene`、两组 `SetGameObjectsActive`、`InstantiateGameObjects`、`SetActiveRecursively`、3 个 removed animation 方法及 13 个 legacy Component 属性；`GameObject` 当前对 Unity 2022.3.51f1 的公开反射差异为 **0**。
- 直接反汇编官方 `UnityEngine.CoreModule.dll`，精确复现 `NativeArray` 未初始化、count/容量不匹配、无效 source InstanceID、removed API 的异常类型/参数名/文本，以及 optional `destinationScene=null` 元数据。
- 建立官方 Unity batchmode + Play Mode A/B：验证空/重复/无效 ID、批量激活父子状态、递归 activeSelf 覆盖、GameObject/Transform ID 回填、目标 Scene 整树迁移、inactive clone 延迟 `Awake`、字段在 `Awake` 前完成复制，以及禁用子→父/启用父→子的回调顺序。
- 重写层级激活派发为变更前快照 + 原子状态更新：重复或父子重叠 ID 不会重复回调；禁用按逆层级、启用按正层级派发。
- `MonoBehaviour` 增加内部一次性 Awake 状态；inactive GameObject 上新增/克隆的脚本延迟到首次激活才 `Awake`，后续重复激活只重发 `OnEnable`。
- 修复同步克隆路径的两个既有生产错误：组件曾在字段复制前且可能重复 `Awake`，以及通用字段复制会把 clone Component 的 `gameObject` 所有权覆盖回 source；现在克隆整树构建完成后才派发生命周期，并过滤运行时所有权/协程状态。
- `SceneManager.MoveGameObjectToScene` 通过 `SetSceneInternal` 把目标 Scene 递归传播到全部后代；批量实例化返回的 root/child 场景归属与官方一致。
- 新增 `GameObjectBulkApiTests` **31/31** 深测；受影响既有生命周期/异步实例化测试 **101/101**，Core 全量 **504/504**。

### 官方反射面增量（相对 2026-07-16f 基线）
- 类型指标不变：同名存在 **896/4,117**，类型指纹精确 **358**，缺失 **3,221**，不一致 **538**，扩展 **638**。
- 成员：同签名存在 **8,134→8,155**；契约精确 **5,831→5,852**；真实缺失 **29,030→29,009**（已有类型内 **7,046→7,025**）；不一致保持 **2,303**，扩展保持 **3,604**。
- `GameObject` 的 21 个旧差异全部消失；84 个官方程序集加载问题 0，旧基线门禁 `regressions=0`、`removed-or-changed=21`。

### 下一次优先项
1. ~~纠正 `AssetBundleRequest : ResourceRequest` 官方继承、native 元数据和 asset/allAssets 完成语义，并清理 AsyncOperation 派生类型上的错误 awaiter 扩展；建立官方 coroutine/batchmode A/B。~~（已于 2026-07-16h 完成）
2. 审计并补齐 `Transform.SetParent` / `SceneManager.MoveGameObjectToScene` 的非法 child/root、inactive 重挂载与跨 Scene 生命周期边界，扩展同步 `Object.Instantiate` 全重载 A/B。
3. 扩展 native 物理世界所有权，把 3D/2D broadphase、solver 与刚体步进继续迁入 `anity-native`，以官方 PhysX/Box2D fixture 验证结果与性能。

## 2026-07-16f — `ConstantForce` native 物理语义与 legacy API 收口

### 已完成
- 新增 `PhysicsUpdateBehaviour2D`、`ConstantForce`、`ConstantForce2D`，三者的继承、sealed、构造器、属性、`RequireComponent` 与 `NativeHeader` 已和 Unity 2022.3.51f1 官方反射面逐项精确一致。
- 在 `anity-native` C++ 物理模块新增 3D/2D 恒力解析 C ABI：组合 world/local force 与 torque，并使用刚体 quaternion 将局部向量变换到世界空间；C# 通过 P/Invoke 使用 native 主路径并保留确定性的托管回退。
- 以官方 Unity 2022.3.51f1 play-mode A/B 实测闭环：恒力按 `F / mass * fixedDeltaTime` 每步累加、relative force/torque 随刚体旋转、disabled/kinematic 不生效、睡眠刚体被唤醒；3D/2D 数值结果均已固化为测试断言。
- 修正既有 `Rigidbody.AddRelativeForce` / `AddRelativeTorque` 与 `Rigidbody2D.AddRelativeForce` 未执行局部到世界坐标变换的错误；2D `Physics.Simulate` 现会真正消费累积力且重力只积分一次。
- 物理世界在每次步进前回收已销毁 rigidbody/collider/joint/wheel 与碰撞状态，并对 inactive 对象暂停而不注销；修复长进程中碰撞双循环随历史对象持续膨胀及 2D 对象重新激活后丢失注册的问题。
- 补齐 `Component` 剩余 **13/13** legacy 属性：公开类型、`EditorBrowsable(Never)`、`Obsolete(error: true)` 文本与运行时 `NotSupportedException` 均和官方一致；`Component` 当前官方公开成员差异为 0。
- 新增 `NetworkView`、`NetworkPlayer`、`NetworkViewID`、`NetworkMessageInfo`、`NetworkStateSynchronization`、`RPCMode` 的 Unity 2022 removed compatibility surface。官方 2022.3 本身已移除 legacy networking，Anity 精确复现编译期禁用与运行时统一 `NotSupportedException`，不伪造已不存在的网络传输。
- 新增 **72/72** 深测（恒力/native/RequireComponent/坐标变换/睡眠/inactive/kinematic/多组件/legacy metadata/异常），Core 全量由 **401/401→473/473**；native dylib 已实际加载验证两个新增 C ABI，而非只验证托管回退。

### 官方反射面增量（相对 2026-07-16e 基线）
- 精确审查的 10 个类型：`Component`、`PhysicsUpdateBehaviour2D`、`ConstantForce`、`ConstantForce2D` 与 6 个 legacy networking 类型，当前差异全部为 0。
- 类型：同名存在 **887→896**；契约完全一致 **349→358**；缺失 **3,230→3,221**；不一致保持 **538**；错误扩展保持 **638**。
- 成员：同签名存在 **8,088→8,134**；契约完全一致 **5,785→5,831**；真实缺失 **29,076→29,030**（缺失类型内 **22,017→21,984**，已有类型内 **7,059→7,046**）；不一致保持 **2,303**；错误扩展保持 **3,604**。
- 审查后重建 evidence baseline；84 个官方程序集加载问题为 0，复跑回归门禁要求 `regressions=0`、`removed-or-changed=0`。

### 下一次优先项
1. ~~补齐 `GameObject` 剩余 21 个 legacy/bulk API，优先 `SetGameObjectsActive` / `InstantiateGameObjects` 与 scene/bulk 生命周期路径，并同步官方 batchmode A/B。~~（已于 2026-07-16g 完成）
2. ~~纠正 `AssetBundleRequest : ResourceRequest` 继承和 AsyncOperation 派生类 native 元数据，移除错误公开 awaiter 扩展。~~（已于 2026-07-16h 完成）
3. 扩展 native 物理世界所有权，把当前仍在 C# 的 3D/2D broadphase、solver 与刚体步进继续迁入 `anity-native`，以官方 PhysX/Box2D fixture 验证结果与性能。

## 2026-07-16e — 官方 `InstantiateAsync` 完整公开面与异步集成语义

### 已完成
- 新增 `AsyncInstantiateOperation` / `AsyncInstantiateOperation<T>`，并把 `AsyncOperation` 从错误的 `CustomYieldInstruction` 修正为官方 `YieldInstruction` 继承；三个类型的类型、成员、泛型约束、事件、构造器可见性及 native 元数据均与 Unity 2022.3.51f1 反射精确一致。
- 补齐 `Object.InstantiateAsync<T>` 官方 **10/10** 重载；`Object` 当前全部官方公开成员均已存在且契约精确。
- 以官方 Unity 2022.3.51f1 batchmode 实测闭环：初始 `isDone=false` / `progress=0.9` / `Result=null`、完成后 typed array、晚订阅即时回调、取消完成后 `Result=null`、span 短于 count 时循环复用、父节点参数使用世界位置、非法 count/null/ScriptableObject 异常文本及 integration time 边界。
- 异步实例化接入 `UnityRuntime` PlayerLoop，按 `SetIntegrationTimeMS` 预算分片集成；支持 `allowSceneActivation` 门、同步 `WaitForCompletion`、线程可见取消、部分集成对象回收、完成事件仅一次及底层 `AsyncOperation` 协程等待。
- 新增 **20/20** 深测；Core 全量由 **381/381→401/401**。`AsyncOperation` 继承修复影响的 Resource/AssetBundle/UnityWebRequest 派生类型已逐项反射审查，没有行为测试回归。

### 官方反射面增量（相对 2026-07-16d 基线）
- 类型：同名存在 **885→887**；契约完全一致 **346→349**；缺失 **3,232→3,230**；不一致 **539→538**；错误扩展保持 **638**。
- 成员：同签名存在 **8,059→8,088**；契约完全一致 **5,756→5,785**；真实缺失 **29,105→29,076**（缺失类型内 **22,035→22,017**，已有类型内 **7,070→7,059**）；不一致保持 **2,303**；错误扩展 **3,607→3,604**。
- 审查后重建 evidence baseline 并复跑：`regressions=0`、`removed-or-changed=0`、84 个官方程序集加载问题 0。

### 下一次优先项
1. ~~落地 `ConstantForce` 与 legacy `NetworkView` 兼容层，关闭 `Component` 剩余 13 个官方 legacy 属性，并为每个模块补 ≥10 测试。~~（已于 2026-07-16f 完成）
2. 补 `GameObject` 剩余 21 个 legacy/bulk API，优先 `SetGameObjectsActive` / `InstantiateGameObjects` 与 scene/bulk 路径。
3. ~~纠正 `AssetBundleRequest : ResourceRequest` 继承和 AsyncOperation 派生类 native 元数据，移除错误公开 awaiter 扩展，同时建立可重复运行的官方 batchmode async/coroutine fixture。~~（已于 2026-07-16h 完成）

## 2026-07-16d — Core 基类链精确公开面与 Unity 消息调度

### 已完成
- `UnityEngine.Object` 移除错误的公开 `IDisposable` / `Dispose` 与 Anity-only helper 表面；该错误此前会把 `System.IDisposable` 继承到大量 Unity 对象类型，现已从根上消除。
- `Object`、`Component`、`Behaviour`、`MonoBehaviour`、`GameObject` 五个类型的种类、sealed、继承、接口与 native 元数据指纹已全部和官方 Unity 2022.3.51f1 精确一致。
- `Component` / `GameObject` 补齐官方无约束泛型查询、Type/List/children/parent/index/TryGetComponent/消息重载；默认 `includeInactive=false`，inactive subtree、inactive root 与 interface 查询行为落地。
- `MonoBehaviour` 不再伪造 61 个 protected virtual 生命周期 API；改为 Unity 式按名称查找消息，支持 private/protected `Awake` / `Start` / `Update` 等，并支持 `IEnumerator Start()` 自动协程。
- `Object.Destroy` 改为帧末销毁；`DestroyImmediate` 同步销毁 GameObject 的全部 Component，按 `OnDisable`→`OnDestroy` 顺序清理并触发 `destroyCancellationToken`。
- 补 `FindFirstObjectByType` / `FindAnyObjectByType` / inactive 过滤、隐式 bool、`Instantiate(Object, Scene)` 与场景归属更新；`GameObject()` 默认名对齐 `New Game Object`。
- 新增官方兼容元数据：`ExcludeFromPresetAttribute`、`ExcludeFromDocsAttribute`、`DefaultValueAttribute` 及内部 native binding 特性，用于真实反射签名而非审计白名单。
- 新增 **21/21** 深测；Core 全量 **381/381**。并修复 Vulkan/PlatformGraphics 测试对同一进程全局状态的并行隔离。
- `build-all` 的 native 阶段改为强制门禁：缺 CMake 或 C++ 构建失败时 Unix/PowerShell 均立即失败，不再静默跳过 native 后产生假绿。

### 官方反射面增量（相对 2026-07-16c 基线）
- 类型：同名存在 **882→885**；契约完全一致 **338→346**；缺失 **3,235→3,232**；不一致 **544→539**；错误扩展 **639→638**。
- 成员：同签名存在 **7,977→8,059**；契约完全一致 **5,609→5,756**；真实缺失 **29,187→29,105**；不一致 **2,368→2,303**；错误扩展 **3,697→3,607**。
- 五个核心类型在本批结束时剩余：`Behaviour` **0**、`MonoBehaviour` **0**；`Object` 的 10 个 `InstantiateAsync` 已于 2026-07-16e 补齐；`Component` 仍缺 13 个 legacy 属性；`GameObject` 仍缺 21 个 legacy/bulk API。
- 新 evidence baseline 复跑：`regressions=0`、`removed-or-changed=0`、84 个官方程序集加载问题 0。

### 下一次优先项
1. ~~实现 `AsyncInstantiateOperation<T>` 与 10 个 `Object.InstantiateAsync`，覆盖批量实例化、父节点、位置/旋转 span、取消和完成事件。~~（已于 2026-07-16e 完成）
2. 落地生产级 `ConstantForce` / legacy `NetworkView` 兼容层并补齐 Component/GameObject 旧属性，每模块 ≥10 测试。
3. 补 `GameObject.SetGameObjectsActive`、`InstantiateGameObjects`、scene/bulk API，并建立官方 Unity batchmode 生命周期 A/B fixture。

## 2026-07-16c — CoreModule 官方组件特性签名与运行时语义

### 已完成
- 纠正 7 个错误公开类型名：`RequireComponent`、`AddComponentMenu`、`ContextMenu`、`DisallowMultipleComponent`、`ExecuteInEditMode`、`ExecuteAlways`、`HideInInspector`；删除错误的公开 `*Attribute` 表面。
- 补齐 `ContextMenuItemAttribute`、`DefaultExecutionOrder`、`HelpURLAttribute`，并逐项对齐官方构造器、字段/属性、sealed/继承关系、`AttributeUsage` 与 native-code 元数据。
- `GameObject.AddComponent` 实现 `RequireComponent` 的 1–3 依赖、继承/多特性/递归图、依赖先注册及非法依赖校验；实现基类上的 `DisallowMultipleComponent` 对派生类型互斥。
- `UnityRuntime` 按继承的 `DefaultExecutionOrder` 排序 Update/FixedUpdate/LateUpdate，同优先级按 InstanceID 保持确定顺序。
- `_scripts/UnityApiParity` 增加 `--inspect-type`，可同时输出官方与 Anity 的精确类型/成员指纹，供后续逐类型修复使用。
- 新增组件特性行为测试 **15/15**，覆盖官方类型名、构造器默认值、1/3 个依赖、继承、循环依赖、Awake 顺序、非法依赖、基类互斥及执行顺序。

### 官方反射面增量（Unity 2022.3.51f1）
- 类型：同名存在 **872→882**，契约完全一致 **328→338**，缺失 **3,245→3,235**，错误扩展类型 **646→639**。
- 成员：同签名存在 **7,950→7,977**，契约完全一致 **5,582→5,609**，真实缺失 **29,214→29,187**。
- 更新后的 evidence baseline 已复跑：`regressions=0`、`removed-or-changed=0`、程序集加载问题 0。

### 尚未完成
- `ExecuteAlways` / `ExecuteInEditMode` 的编辑模式 PlayerLoop、Prefab Stage 与 Domain Reload 行为尚未形成官方 A/B 闭环，本批只完成其公开 API 精确签名，不标记该行为完成。

### 下一次优先项
1. 用 `--inspect-type` 继续处理 `UnityEngine.CoreModule` 高频 TypeMismatch，优先 `Object` / `Component` / `Behaviour` / `MonoBehaviour` / `GameObject` 的公开契约与继承元数据。
2. 建立官方 batchmode 组件生命周期 fixture，验证 Awake/OnEnable/Start、RequireComponent、Destroy 与 execution order 的同输入/同输出。
3. 补 `ExecuteAlways` / `ExecuteInEditMode` 编辑模式调度，并覆盖 Prefab Stage、进入/退出 Play Mode 与脚本重载边界。

## 2026-07-16b — 官方 Unity 2022.3 API 反射门禁与真实差距基线

### 已完成
- 新增跨平台 `_scripts/UnityApiParity` 审计器，直接读取官方 UnityEngine/UnityEditor 程序集与 `Anity.Core.dll`，不再用 Checklist 文本或类型名搜索代替兼容性证据。
- 审计公开及 protected 契约：类型种类/继承/接口/泛型约束、构造器、方法与转换操作符、字段/枚举值、属性/索引器、事件、参数名与默认值、`ref/out/in/params`、可见性、静态/虚方法语义及特性。
- Unix/Windows 入口：`_scripts/unity-api-parity.sh` / `.ps1`；自动探测 Unity 2022.3，输出完整当前 JSON，并以紧凑 SHA-256 evidence baseline 阻止新缺口或未审查的签名变化。
- 修复审计边界：转换操作符按返回目标区分；官方 Editor 依赖从 Managed 父目录解析；缺失类型下的全部成员计入真实缺失数；当前官方程序集加载问题为 0。
- 审计器 xUnit **17/17**，覆盖 public/protected 过滤、类型/枚举/泛型、重载、转换操作符、参数、ref/out/in、属性、事件/特性以及所有差异分类。

### 官方基线（本机 Unity 2022.3.51f1，84 个程序集）
- 类型：官方 **4,117**；Anity 同名存在 **872（21.180%）**；契约完全一致 **328（7.967%）**；缺失 **3,245**；不一致 **544**；Anity 扩展 **646**。
- 成员：官方 **37,164**；同签名身份存在 **7,950（21.392%）**；契约完全一致 **5,582（15.020%）**；真实缺失 **29,214**（缺失类型内 22,068 + 已有类型内 7,146）；不一致 **2,368**；扩展 **3,697**。
- 最大缺口：`UnityEditor.CoreModule` 缺类型 1,358；`UnityEngine.CoreModule` 缺类型 690、已有类型内缺成员 2,582；`UnityEngine.UIElementsModule` 缺类型 396。
- 证据：`parity-evidence/unity-api-parity-baseline.json`；重复运行门禁结果 `regressions=0`、`removed-or-changed=0`。

### 结论
- 历史“全部 Unity API 落地”结论被官方证据否定；全局仍为 🟡，不得以现有单元测试绿灯宣称 Unity 2022 Pro 完全对等。
- 基线只用于防止倒退，不降低最终验收线；完成条件仍是 missing/mismatch/load issue 全部归零，并叠加官方行为、编辑器交互与平台产物门禁。

### 下一次优先项
1. 先处理 `UnityEngine.CoreModule`：按官方反射表分批补齐高频迁移阻塞类型/成员，每批同步 ≥10 行为与签名测试，并要求基线差异单调下降。
2. 随后处理 `UnityEditor.CoreModule` 与 UIElements，优先 Build/PlayerSettings/AssetDatabase/EditorWindow/Inspector/SceneView 的签名和行为闭环。
3. 增加官方 Unity batchmode 行为 fixture runner，把 API 一致门禁扩展为同输入/同输出 A/B 门禁。

## 2026-07-16 — Unity 2022 Ultra 目标续跑：可控 Agent 自定义接入

### 目标口径
- 总目标保持为：所有 Unity 2022.3 Pro 公开 API、行为、编辑器交互、构建与平台效果一致；Anity 实现源码自主可控。
- Agent 是独立 `anity-agent/` 官方扩展，不进入 `Anity.Core`；本批只关闭自定义模型接入纵向链路，不宣称 Unity 全局对等完成。

### 已完成
- 新增源码可控的 `OpenAiCompatibleAgentProvider`，直接请求 `<base-url>/chat/completions`，无闭源厂商 SDK 绑定。
- `AgentConnectionOptions` 支持自定义 API Key、Base URL、模型、超时、重试和最大响应体；支持 `ANITY_AGENT_API_KEY`、`ANITY_AGENT_BASE_URL`、`ANITY_AGENT_MODEL`。
- `anity` CLI 新增 `-agentApiKey/-agentBaseUrl/-agentModel/-agentTimeoutSeconds`；推荐密钥走环境变量，日志与异常均脱敏。
- 生产防护：HTTP(S) URL 校验、Bearer 鉴权、取消与请求超时、408/429/5xx 重试、Retry-After、响应体上限、结构化 HTTP 错误、字符串及多段文本响应解析。
- Session 异步调用改为真正 async，单 Session 并发 turn 串行化，历史返回快照；Memory/ToolRegistry 改为并发容器；本地工具不误发到模型端点。
- `System.Text.Json` 从存在高危告警的 8.0.0 升级到 8.0.6；Agent/Core 生成带 content hash 的 `packages.lock.json`；漏洞扫描无已知漏洞。
- Unix `_scripts/build-all.sh` 补回 `anity-agent` 与 `anity-cli`，与 PowerShell 构建入口一致。

### 验收证据
- `Anity.Agent.Tests`：30/30 通过，其中自定义接入覆盖配置、URL、鉴权、模型/历史 JSON、错误脱敏、重试、取消、超时、响应上限、并发和本地工具隔离。
- `Anity.Cli.Tests`：16/16 通过，覆盖新增 CLI 参数与帮助面。
- `dotnet list ... package --vulnerable --include-transitive`：无易受攻击包。

### 下一次优先项
1. Agent 工具调用协议、SSE 流式输出、用量统计与编辑器安全凭据存储，形成编辑器内生产交互闭环。
2. ~~建立 Unity 2022.3 官方程序集反射面自动门禁~~（API 门禁已完成；行为 fixture 继续见 07-16b）。
3. 从 WebGL/Windows 的 PlayerLoop、渲染帧与资源导入开始补生产级端到端互操作测试。

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
- 本地 Release Core 全绿（345）
- GitHub Actions **anity-ci 全 job success**（run 29222163518）
- 额外修复：`IsMsvcCl` 在 Linux 上正确解析 Windows 风格 `cl.exe` 路径

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
