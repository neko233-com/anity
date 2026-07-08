# UnityCompat (Unity-like API Shim)

目标是“兼容迁移”，不是替代官方实现。  
用于让项目结构和代码模型先在 Anity 平台上可运行，再分阶段对齐行为细节。

## 覆盖范围（当前）

- 数学：`Vector2`, `Vector3`, `Quaternion`, `Mathf`
- 对象系统：`Object`, `Component`, `Behaviour`, `MonoBehaviour`, `Transform`, `GameObject`
- 运行时：`Time`, `Application`
- 调试与输入：`Debug`, `Random`
- 特性：`SerializeField`, `HideInInspector`
- 场景管理：`Scene`, `SceneManager`, `LoadSceneMode`
- 资源系统：`Resources`
- 可脚本化对象：`ScriptableObject`
- 渲染入口：`Camera`
- 输入：`Input`, `KeyCode`
- 编辑器外壳：`UnityEditor.EditorWindow`, `UnityEditor.EditorGUILayout`, `UnityEditor.EditorGUI`, `UnityEditor.GUILayout`, `UnityEditor.Selection`, `UnityEditor.AssetDatabase`, `UnityEditor.EditorApplication`, `UnityEditor.EditorUtility`, `UnityEditor.SerializedObject`, `UnityEditor.SerializedProperty`, `UnityEditor.Handles`, `UnityEditor.MenuItem`
- 编辑器 shell 扩展：`UnityEditor.EditorPrefs`, `UnityEditor.EditorSceneManager`, `UnityEditor.GenericMenu`, `UnityEditor.AssetImporter`, `UnityEditor.Undo`, `UnityEditor.MenuCommand`, `UnityEditor.EditorBuildSettings`
- 运行时：`PlayerPrefs`

## 使用建议（跨平台）

- 不依赖平台 API，仅使用纯 .NET 可移植类型。
- 任何与图形渲染、输入系统、资源管线相关逻辑请放在各模块自己的上层抽象中，避免污染核心层。
- 后续版本按优先级补齐 `Camera`, `Scene`, `Resources`, `JsonUtility`, `Physics` 分层。
- 当前版本优先保证 API 外观兼容，不保证渲染/编辑器行为与 Unity 完全一致。

## 版本迭代

- `UnityCompat` 与 `VersionInfo` 一并走 SemVer 管理
- 只做**行为一致性增强**，不做“名义 API 完全替代”
