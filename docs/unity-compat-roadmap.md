# UnityCompat Roadmap

目标：让项目可运行于 Unity Pro 风格 API 上下文，先兼容“编辑器可迁移 API 外观”，后补行为精度。

## 已实现（第一波）

- Core Object Model: `Object`, `Component`, `Behaviour`, `MonoBehaviour`, `Transform`, `GameObject`
- 数学: `Vector2`, `Vector3`, `Quaternion`, `Mathf`
- 运行时: `Time`, `Application`, `Debug`, `Random`
- 工具化运行时: `PlayerPrefs`, `PlayerPrefsException`
- 输入: `Input`, `KeyCode`
- 场景: `Scene`, `SceneManager`, `LoadSceneMode`
- 资源: `Resources`
- 渲染: `Camera`
- 可序列化对象: `ScriptableObject`
- 编辑器层: `UnityEditor.EditorWindow`, `UnityEditor.EditorApplication`, `UnityEditor.EditorGUILayout`, `UnityEditor.EditorGUI`, `UnityEditor.GUILayout`, `UnityEditor.Selection`, `UnityEditor.AssetDatabase`, `UnityEditor.EditorUtility`, `UnityEditor.SerializedObject`, `UnityEditor.SerializedProperty`, `UnityEditor.Handles`, `UnityEditor.MenuItem`
- 编辑器 shell 扩展: `UnityEditor.EditorPrefs`, `UnityEditor.EditorSceneManager`, `UnityEditor.GenericMenu`, `UnityEditor.AssetImporter`, `UnityEditor.Undo`, `UnityEditor.MenuCommand`
- 编辑器构建: `UnityEditor.EditorBuildSettings`

## 进行中 / 计划（第二波）

- `Prefab`/`Asset` 生命周期 API（含 BuildPipeline/BuildPlayerWindow/Unity.Profiling）
- 脚本编译链路：`UnityEditor.Compilation` `AssemblyBuilder`/`CompilationPipeline`
- `Physics`, `Physics2D`, `Collider`, `Rigidbody`
- `Material`, `Texture2D`, `Shader`, `RenderTexture`
- `UI` 与 `IMGUI/UIToolkit` 深度层
- `JsonUtility`
- `PackageManager`/`PlayerSettings`/`SettingsProvider`/`InternalEditorUtility`

本轮新增：
- `AssetDatabase` 增强到更多查找/重命名/移动/复制与依赖接口骨架
- `Undo`/`GenericMenu`/`EditorWindow`/`EditorUtility` 的编辑器壳方法补齐
- `UnityEngine` 增加 `Physics`、`Physics2D`、`Collider`、`Rigidbody`、`Material`、`Texture2D`、`RenderTexture` 等运行时骨架
- 继续补齐 `PrefabUtility`、`AssetPostprocessor`、`JsonUtility`、`AnimationCurve/LayerMask/Matrix4x4/Color32/TextAsset` 常用骨架
- 新增 `PlayerSettings`、`SettingsProvider`、`PackageManager`、`UnityEditorInternal.InternalEditorUtility` 兼容层

## 已实现（第三波 - Unity Pro 2022 Full Compat）

- Build Callbacks: `IPreprocessBuildWithReport`, `IPostprocessBuildWithReport`, `IProcessSceneWithReport`, `IOrderedCallback`
- BuildReporting: `BuildFile`, `BuildStepMessage`, expanded `BuildSummary` with TimeSpan and buildGuid
- SceneView: 7+ `LookAt` overloads, `orthographic` property, `duringSceneGui` event
- Handles: `DrawArc`, `DrawCone`, `DrawDottedLine`, `Disc`, `RadiusHandle`, and 15+ new methods
- PlayerSettings: `Windows` nested class, `StandaloneBuildSubtarget`, per-platform icons
- PackageManager: `Request.completed` event, `GetEnumerator()`, `EmbedRequest`, `UpdateRequest`
- BuildPipeline: Expanded enums with PS5, Xbox, Nintendo Switch, additional BuildOptions

## 约定

- 所有 shim 文件仅作为兼容 API 外壳，优先保证签名可迁移。
- 任何 `Editor*` API 默认不在核心兼容层兜底；放到 Editor 扩展层实现。
