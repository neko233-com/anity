# AGENTS.md - Anity 项目开发规范与用户偏好

> 所有 Agent 必须阅读并遵守本文件中的规范。**不要就以下内容再询问用户**，直接按规范执行。

---

## 一、渲染管线

- **唯一支持的渲染管线：URP（Universal Render Pipeline）**
- **不需要**：Built-in Render Pipeline、HDRP、SRP 模板/自定义管线
- 所有 Shader、材质、渲染特性以 URP 14.x（Unity 2022 LTS 对应版本）为基准

---

## 二、核心功能要求

### 1. Job System（任务系统）
- 完整支持 Unity C# Job System
- 包含：`IJob`、`IJobParallelFor`、`IJobParallelForTransform`、`JobHandle`、`JobScheduler`
- 包含：Burst 兼容的 `[BurstCompile]` 作业
- 包含：NativeContainer 系列（`NativeArray`、`NativeList`、`NativeHashMap`、`NativeQueue` 等）

### 2. 代码裁切 / Code Stripping
- **Managed Stripping**：支持 High/Medium/Low/Disabled 四个级别
- **Engine Module Stripping**：按模块裁切 Unity 引擎代码
- **[Preserve] 标记**：保留类型/方法不被裁切
- **Link.xml 支持**：程序集级链接配置
- **代码生成**：IL2CPP 时代码生成与裁切协同

### 3. IL2CPP
- 完整的 IL2CPP 构建管线支持
- AOT 编译、泛型共享、代码生成选项
- 运行时检测（`Il2CppRuntime.IsIl2Cpp`）

---

## 三、开发原则

1. **完全对标 Unity 2022 LTS**（2022.3.x）：API 签名、命名空间、行为一致
2. **编辑器界面也要完全一样**：菜单、窗口布局、工具栏、状态栏、Inspector 样式
3. **优先补高频迁移阻塞点**：Build、PackageManager、Editor、Runtime 核心 API
4. **API 签名兼容优先**：行为逐步补齐，但签名必须先齐
5. **新增 API 必须在 PLAN.md 中记录**

---

## 四、平台支持优先级

1. **WebGL** - 最高优先（浏览器运行）
2. **Windows** - 第二优先（桌面编辑器/玩家）
3. 其它平台按需补

---

## 五、禁止事项

- 不要询问"是否要支持 HDRP/Built-in"——答案是**不支持**，只做 URP
- 不要询问"是否需要 Job System"——答案是**需要**
- 不要询问"是否需要代码裁切"——答案是**需要**
- 不要在用户没要求的情况下创建额外的 .md 文档
- 不要在代码里写入 HTML/XML 转义字符（如 `&amp;`、`&lt;`）
