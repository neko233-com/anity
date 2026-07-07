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
- 更新进度文档：`docs/unity-compat-roadmap.md`。

### 下一次要做（优先）
1. `Editor` 侧：补齐 `UnityEditor.EditorUserSettings` + `SceneView` + `Handles` 深水位 API，并增加更多场景/构建常用方法。
2. `Build` 侧：补齐 `BuildPlayer`、`BuildTargetGroup`、`BuildOptions` 与 Pro 常见 build 回调/报告细节。
3. `PackageManager` 侧：补齐 `PackageManager.Request` 轮询/等待逻辑、`SearchRequest` 过滤参数和 `Add/Remove` 状态反馈。

