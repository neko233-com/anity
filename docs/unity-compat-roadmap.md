# UnityCompat Roadmap

目标：让项目可运行于 Unity 风格 API 上下文，先兼容“编辑器可迁移 API 外观”，后补行为精度。

## 已实现（第一波）

- Core Object Model: `Object`, `Component`, `Behaviour`, `MonoBehaviour`, `Transform`, `GameObject`
- 数学: `Vector2`, `Vector3`, `Quaternion`, `Mathf`
- 运行时: `Time`, `Application`, `Debug`, `Random`
- 输入: `Input`, `KeyCode`
- 场景: `Scene`, `SceneManager`, `LoadSceneMode`
- 资源: `Resources`
- 渲染: `Camera`
- 可序列化对象: `ScriptableObject`

## 进行中 / 计划（第二波）

- `Prefab`/`Asset` 生命周期 API
- `Physics`, `Physics2D`, `Collider`, `Rigidbody`
- `Material`, `Texture2D`, `Shader`, `RenderTexture`
- `UI` 与 `GUILayout` 基础层
- `JsonUtility`、`PlayerPrefs`、`PlayerPrefsException`

## 约定

- 所有 shim 文件仅作为兼容 API 外壳，优先保证签名可迁移。
- 任何 `Editor*` API 默认不在核心兼容层兜底；放到 Editor 扩展层实现。
