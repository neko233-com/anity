# Unity 2022.3.61f1 兼容性功能清单

> 本文件记录 Anity 对 Unity 2022.3.61f1 各模块 API 的实现状态。
> 状态说明：
> - ✅ 已实现（含完整行为或核心行为）
> - 🟡 已实现 API 壳 / Stub（签名兼容，方法体为空或占位）
> - ❌ 未实现

---

## 1. UnityEngine.CoreModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Object` | 🟡 | 销毁、实例化、比较等基础 API 已壳化 |
| `GameObject` | 🟡 | 组件增删、tag、layer（已限制 0-31）、SetActive 等 |
| `Component` | 🟡 | transform、gameObject、GetComponent 等 |
| `Behaviour` | 🟡 | enabled、isActiveAndEnabled |
| `MonoBehaviour` | 🟡 | Start/Update 等生命周期、协程基础 |
| `Transform` | 🟡 | position/rotation/scale、父子关系、Translate/Rotate/LookAt |
| `Vector2/3/4` | ✅ | 完整数学运算与常量 |
| `Quaternion` | ✅ | 基本运算、LookRotation、Slerp 等 |
| `Matrix4x4` | ✅ | 乘法、逆、透视、转置、行列式 |
| `Bounds` | 🟡 | 基础包围盒 |
| `Ray` | ✅ | 射线定义 |
| `Rect` | 🟡 | 基础矩形 |
| `Color/Color32` | ✅ | 基本运算与转换 |
| `Mathf` | ✅ | 常用数学函数 |
| `Time` | 🟡 | deltaTime、time、timeScale、frameCount |
| `Application` | 🟡 | runInBackground、targetFrameRate、Quit、OpenURL、平台信息 |
| `Debug` | 🟡 | Log/Warning/Error/Assert 等 |
| `Input` | 🟡 | 键盘鼠标 + Touch 多点触控壳 |
| `LayerMask` | 🟡 | value 读写、NameToLayer、LayerToName、GetMask |
| `PlayerPrefs` | 🟡 | 读写壳 |
| `Random` | 🟡 | Range、insideUnitSphere 等 |
| `Resources` | 🟡 | Load 壳 |
| `JsonUtility` | 🟡 | ToJson/FromJson 壳 |
| `Screen` | 🟡 | 分辨率、全屏、DPI 等基础 API 已壳化 |
| `SystemInfo` | 🟡 | 设备/操作系统/处理器信息已壳化 |
| `GL` | 🟡 | 即时渲染命令已壳化 |
| `AsyncOperation` | 🟡 | 基础壳 |
| `ScriptableObject` | 🟡 | 基础壳 |

---

## 2. UnityEngine.PhysicsModule（3D 物理）

| 类型 | 状态 | 备注 |
|------|------|------|
| `Rigidbody` | 🟡 | 基础属性、AddForce、MovePosition 等 |
| `Collider` | 🟡 | 基础属性、isTrigger、ClosestPoint |
| `BoxCollider` | 🟡 | 壳 |
| `SphereCollider` | 🟡 | 壳 |
| `CapsuleCollider` | 🟡 | 壳 |
| `MeshCollider` | 🟡 | 壳 |
| `CharacterController` | 🟡 | 壳 |
| `WheelCollider` | 🟡 | 含 WheelHit/WheelFrictionCurve 壳 |
| `Physics` | 🟡 | Raycast/Cast/Overlap 多种重载签名 |
| `PhysicsScene` | 🟡 | 壳 |
| `RaycastHit` | ✅ | 完整结构 |
| `Collision` | 🟡 | 壳 |
| `ContactPoint` | 🟡 | 壳 |
| `ForceMode` | ✅ | 枚举 |
| `QueryTriggerInteraction` | ✅ | 枚举 |
| `Joint` 系列 | 🟡 | Hinge/Spring/Fixed/Configurable 等基础壳 |
| `Physics.Simulate` 真实现 | ✅ | PhysicsWorld 管理刚体积分、碰撞检测/响应、射线/体积查询 |

---

## 3. UnityEngine.Physics2DModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Rigidbody2D` | ✅ | bodyType、drag、重力、AddForce/Torque、MovePosition/Rotation |
| `Collider2D` | ✅ | isTrigger、IsTouching、GetShape 抽象 |
| `BoxCollider2D` | ✅ | 形状实现 |
| `CircleCollider2D` | ✅ | 形状实现 |
| `CapsuleCollider2D` | ✅ | 形状实现（以 Box 近似参与碰撞） |
| `EdgeCollider2D` | 🟡 | 边缘顶点壳 |
| `PolygonCollider2D` | 🟡 | 多边形路径壳 |
| `CompositeCollider2D` | 🟡 | 壳 |
| `PhysicsMaterial2D` | 🟡 | friction/bounciness 壳 |
| `Joint2D` 系列 | 🟡 | Fixed/Spring/Distance/Hinge/Slider/Wheel/Relative/Friction/Target 壳 |
| `Effector2D` 系列 | 🟡 | Area/Point/Platform/Surface/Buoyancy 壳 |
| `Physics2D` | ✅ | Raycast/Overlap/Cast 查询签名 |
| `Physics2DWorld` | ✅ | 内部世界管理、积分、碰撞检测/响应、触发器 |
| `RaycastHit2D` | ✅ | 完整结构 |
| `Collision2D` / `ContactPoint2D` | ✅ | 结构完整 |
| `ForceMode2D` / `RigidbodyType2D` | ✅ | 枚举 |
| `ContactFilter2D` | 🟡 | 壳 |

---

## 4. UnityEngine.UI（uGUI）

| 类型 | 状态 | 备注 |
|------|------|------|
| `Canvas` | 🟡 | isRootCanvas、renderTransform、ForceUpdateCanvases 等 |
| `CanvasScaler` | 🟡 | uiScaleMode、referenceResolution、screenMatchMode |
| `CanvasRenderer` | 🟡 | 壳 |
| `Graphic` | 🟡 | color、material、raycastTarget、dirty 标记 |
| `MaskableGraphic` | 🟡 | RectMask2D / Mask 裁剪接口 |
| `RectMask2D` | 🟡 | IClipper 实现 |
| `Image` | 🟡 | 壳 |
| `RawImage` | 🟡 | texture/uvRect 壳 |
| `Text` | 🟡 | 壳 |
| `Button` | 🟡 | 壳 |
| `Selectable` | 🟡 | 壳 |
| `Toggle` | 🟡 | 壳 |
| `ToggleGroup` | 🟡 | 壳 |
| `Slider` | 🟡 | 壳 |
| `Scrollbar` | 🟡 | 壳 |
| `Dropdown` | 🟡 | 选项管理、value、事件已落地，下拉弹窗未完整 |
| `InputField` | 🟡 | 文本/验证/激活/提交事件已落地，键盘输入未完整 |
| `ScrollRect` | 🟡 | 拖拽/滚动/惯性/归一化位置已落地，完整布局未接 |
| `Scrollbar` | 🟡 | value/size/方向/事件已落地 |
| `RectTransform` | 🟡 | anchoredPosition、pivot、anchorMin/Max、sizeDelta |
| `LayoutGroup` / `Horizontal/Vertical/Grid` | 🟡 | 壳 |
| `LayoutElement` | 🟡 | 壳 |
| `ContentSizeFitter` / `AspectRatioFitter` | 🟡 | 壳 |
| `EventSystem` | 🟡 | 壳 |
| `BaseInputModule` / `StandaloneInputModule` | 🟡 | 壳 |
| `PointerEventData` | 🟡 | 壳 |
| `GraphicRaycaster` | 🟡 | 壳 |
| `CanvasGroup` | 🟡 | alpha、interactable、blocksRaycasts |
| `Outline` / `Shadow` / `PositionAsUV1` | 🟡 | BaseMeshEffect + ModifyMesh 实现 |

---

## 5. UnityEngine.UIModule（Canvas/IMGUI 底层）

| 类型 | 状态 | 备注 |
|------|------|------|
| `Font` | 🟡 | 壳 |
| `TextAnchor/TextAlignment/FontStyle` | ✅ | 枚举 |
| `CanvasRenderer` (底层) | 🟡 | 见 UI |

---

## 6. UnityEngine.Rendering + SRP

| 类型 | 状态 | 备注 |
|------|------|------|
| `Graphics` | 🟡 | DrawMesh/DrawTexture 等壳 |
| `GraphicsSettings` | 🟡 | default/current render pipeline |
| `QualitySettings` | 🟡 | pixelLightCount、shadow、vSync 等 |
| `RenderPipelineAsset` | 🟡 | 抽象壳 |
| `RenderPipeline` | 🟡 | Render 抽象、事件触发 |
| `RenderPipelineManager` | 🟡 | 事件与 currentPipeline |
| `ScriptableRenderContext` | 🟡 | Submit/ExecuteCommandBuffer/DrawRenderers |
| `CommandBuffer` | 🟡 | 常用 Draw/SetGlobal/ClearRenderTarget 壳 |
| `CommandBufferPool` | 🟡 | 壳 |
| `CullingResults` | 🟡 | 壳 |
| `DrawingSettings/FilteringSettings` | 🟡 | 壳 |
| `RenderTargetIdentifier` | 🟡 | 壳 |
| `ShaderPassName` | 🟡 | 壳 |
| `SortingCriteria/RenderQueueRange/SortingLayerRange` | 🟡 | 枚举/结构 |
| `PerObjectData` | ✅ | 枚举 |
| `CameraEvent/LightEvent/ShadowMapPass` | ✅ | 枚举 |
| `Volume` 框架 | 🟡 | Volume/VolumeComponent/Profile/Stack/Parameter 壳 |
| `URP Asset` / `UniversalRenderPipelineAsset` | 🟡 | 壳 |
| `ScriptableRenderer` / `UniversalRendererData` | 🟡 | 壳 |
| `ComputeShader` | 🟡 | 壳 |
| `ComputeBuffer` | 🟡 | 壳 |

---

## 7. UnityEngine.AnimationModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Animator` | 🟡 | 常用参数、状态信息壳 |
| `RuntimeAnimatorController` | 🟡 | 空壳 |
| `AnimatorOverrideController` | 🟡 | 壳 |
| `AnimatorController` | 🟡 | 壳 |
| `AnimatorStateInfo/AnimatorClipInfo` | 🟡 | 壳 |
| `AnimatorControllerParameter` | 🟡 | 壳 |
| `AnimationClip` | 🟡 | 壳 |
| `AnimationEvent` | 🟡 | 壳 |
| `Avatar` / `AvatarMask` | 🟡 | 壳 |
| `HumanBodyBones` | ✅ | 枚举 |
| `StateMachineBehaviour` | 🟡 | 状态机回调壳 |
| `BlendTree` | 🟡 | 1D/2D/Simple/Direct 壳 |
| `Animation`（Legacy） | 🟡 | Play/Stop/CrossFade/AnimationState 壳 |

---

## 8. UnityEngine.AudioModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `AudioClip` | 🟡 | 壳 |
| `AudioSource` | 🟡 | 播放控制壳 |
| `AudioListener` | 🟡 | volume/pause 静态壳 |
| `AudioMixer/AudioMixerGroup` | 🟡 | Get/SetFloat、Group/Snapshot 壳 |

---

## 9. UnityEngine.AssetBundleModule / UnityWebRequestModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `AssetBundle` | 🟡 | LoadFromFile/LoadAsset/Unload 壳 |
| `UnityWebRequest` | 🟡 | Get/Post、DownloadHandlerBuffer 壳 |
| `WWW` | 🟡 | 旧版网络请求壳 |
| `DownloadHandler` / `UploadHandler` | 🟡 | Buffer/Texture/AudioClip 壳 |

---

## 10. UnityEngine.ImageConversionModule / Texture

| 类型 | 状态 | 备注 |
|------|------|------|
| `Texture` | 🟡 | 基础属性壳 |
| `Texture2D` | 🟡 | LoadImage、EncodeToPNG/JPG、Get/SetPixels32 签名 |
| `RenderTexture` | 🟡 | active、format 壳 |
| `Cubemap` | 🟡 | 壳 |
| `TextureFormat` / `RenderTextureFormat` | ✅ | 枚举 |
| `ImageConversion` | 🟡 | EncodeToPNG/JPG/TGA、LoadImage 扩展壳 |

---

## 11. UnityEngine.MeshModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Mesh` | 🟡 | vertices/uv/triangles 等基础壳 |
| `BoneWeight` | 🟡 | 壳 |
| `MeshTopology` | ✅ | 枚举 |
| `MeshData/MeshDataArray` | 🟡 | 壳 |

---

## 12. UnityEngine.Renderer / Material

| 类型 | 状态 | 备注 |
|------|------|------|
| `Renderer` | 🟡 | material(s)、sharedMaterial、bounds 等 |
| `MeshRenderer` | 🟡 | 壳 |
| `SkinnedMeshRenderer` | 🟡 | 壳 |
| `SpriteRenderer` | 🟡 | 壳 |
| `TrailRenderer` / `LineRenderer` | 🟡 | 壳 |
| `Material` | 🟡 | Set/Get Float/Int/Vector/Matrix/Color、Keyword |
| `Shader` | 🟡 | Find、PropertyToID、Keyword 壳 |
| `ShaderVariantCollection` | 🟡 | 壳 |
| `ComputeShader/ComputeBuffer` | 🟡 | 壳（见 Rendering） |
| `ReflectionProbe` | 🟡 | 壳 |
| `LightProbeGroup` | 🟡 | 壳 |
| `LODGroup` | 🟡 | 壳 |
| `OcclusionArea/Portal` | 🟡 | 壳 |

---

## 13. UnityEngine.CameraModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Camera` | ✅ | 渲染管线接入、Render、RenderWithShader、viewport |
| `CameraType` / `CameraClearFlags` / `RenderingPath` | ✅ | 枚举 |
| `SceneViewCamera` 等 | ❌ | 缺失 |

---

## 14. UnityEngine.LightModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Light` | 🟡 | 颜色、强度、类型壳 |
| `LightType/LightShadows/LightmapBakeType` | ✅ | 枚举 |
| `LightmapData` / `LightmapSettings` | 🟡 | 壳 |
| `LightmapParameters` | 🟡 | 壳 |
| `AmbientMode` / `FogMode` | ✅ | 枚举 |

---

## 15. UnityEngine.ParticleSystemModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `ParticleSystem` | 🟡 | 主类 + 模块访问器壳 |
| `ParticleSystemRenderer` | 🟡 | 壳 |
| 各模块（Emission/Shape/Velocity...） | 🟡 | 已拆分 4 个文件 |
| 相关枚举/结构 | 🟡 | 大部分枚举已定义 |

---

## 16. UnityEngine.TerrainModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Terrain` | 🟡 | 基础属性壳 |
| `TerrainData` | 🟡 | 高度图/alphamap/图层壳 |
| `TerrainCollider` | 🟡 | 壳 |
| `Tree/Detail` 相关 | 🟡 | TreePrototype/TreeInstance/TerrainLayer 壳 |

---

## 17. UnityEngine.TilemapModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Tilemap` | 🟡 | 基础壳 |
| `Tile` / `TileBase` / `TilemapCollider2D` | 🟡 | TileBase/Tile/TileData/TileFlags 壳 |

---

## 18. UnityEngine.VideoModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `VideoPlayer` | 🟡 | source/clip/url/Play/Pause/Stop 壳 |
| `VideoClip` | 🟡 | 属性壳 |
| WebGL 侧 `WebGLVideo` | 🟡 | 仅支持声明壳 |

---

## 19. UnityEngine.AIModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `NavMesh` / `NavMeshAgent` / `NavMeshPath` | 🟡 | CalculatePath/SetDestination 壳 |

---

## 20. UnityEngine.TextRendering / TextMeshPro

| 类型 | 状态 | 备注 |
|------|------|------|
| `Font` | 🟡 | 见 UIModule |
| `TextMeshPro` / `TMP_Text` / `FontAsset` | 🟡 | TMP_Text 继承 Text，FontAsset 壳 |

---

## 21. UnityEngine.Events / UnityEvent

| 类型 | 状态 | 备注 |
|------|------|------|
| `UnityEvent` / `UnityEvent<T>` | 🟡 | 基础壳 |

---

## 22. UnityEngine.SceneManagementModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Scene` | 🟡 | 壳 |
| `SceneManager` | 🟡 | LoadScene 重载 |
| `LoadSceneMode` | ✅ | 枚举 |
| `LoadSceneParameters` | 🟡 | 壳 |

---

## 23. UnityEngine.Jobs / Unity.Collections / Unity.Burst

| 类型 | 状态 | 备注 |
|------|------|------|
| `IJob` / `JobHandle` / `JobSystem` | 🟡 | 壳 |
| `NativeArray<T>` / `NativeSlice<T>` | 🟡 | 壳 |
| `Burst` / `BurstCompile` | 🟡 | 壳 |

---

## 24. UnityEngine.Profiling

| 类型 | 状态 | 备注 |
|------|------|------|
| `Profiler` | 🟡 | BeginSample/EndSample 壳 |
| `ProfilerUnsafeUtility` | 🟡 | 壳 |

---

## 25. UnityEngine.IL2CPP / Preserve

| 类型 | 状态 | 备注 |
|------|------|------|
| `PreserveAttribute` | ✅ | 已定义 |
| `Il2CppSetOptionAttribute` | ✅ | 已定义 |
| `AlwaysLinkAssemblyAttribute` | ✅ | 已定义 |

---

## 26. UnityEditor.CoreModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `EditorApplication` | 🟡 | playModeStateChanged、DelayCall 等事件壳 |
| `EditorWindow` | 🟡 | 窗口壳 |
| `EditorGUI` / `EditorGUILayout` | 🟡 | 大量控件签名壳 |
| `EditorUtility` | 🟡 | 壳 |
| `EditorPrefs` / `EditorUserSettings` | 🟡 | 壳 |
| `EditorGUIUtility` / `EditorStyles` | 🟡 | 壳 |
| `GUIContent` / `GUIStyle` / `GUILayoutOption` | 🟡 | 壳 |
| `Handles` | 🟡 | 大量绘制签名壳 |
| `SceneView` | 🟡 | LookAt、orthographic、事件壳 |
| `Selection` | 🟡 | 壳 |
| `AssetDatabase` | 🟡 | GUID/路径/加载壳 |
| `AssetImporter` / `AssetPostprocessor` | 🟡 | 回调壳 |
| `PrefabUtility` | 🟡 | InstantiatePrefab、Apply/Revert 壳 |
| `Undo` | 🟡 | 壳 |
| `MenuItem` / `MenuCommand` | 🟡 | 属性/壳 |
| `GenericMenu` | 🟡 | 壳 |
| `SerializedObject` / `SerializedProperty` | 🟡 | 壳 |
| `Editor` | 🟡 | 壳 |
| `BuildPipeline` | 🟡 | BuildPlayer/BuildAssetBundles 签名壳 |
| `BuildReport` / `BuildSummary` / `BuildFile` | 🟡 | 壳 |
| `BuildPlayerWindow` / `BuildPlayerOptions` | 🟡 | 壳 |
| `PlayerSettings` | 🟡 | 多平台字段壳 |
| `EditorBuildSettings` | 🟡 | scenes 管理壳 |
| `EditorSceneManager` | 🟡 | Open/Save/NewScene 壳 |
| `ProjectWindow` / `HierarchyWindow` / `InspectorWindow` / `ConsoleWindow` | 🟡 | 壳 |
| `SettingsProvider` | 🟡 | 壳 |
| `CompilationPipeline` / `AssemblyBuilder` | 🟡 | 编译回调壳 |
| `PackageManager.Client` / `PackageInfo` | 🟡 | 请求壳 |
| `Addressables` | 🟡 | 异步句柄壳 |
| `InternalEditorUtility` | 🟡 | 壳 |
| `BuildCallbacks` | 🟡 | 接口定义 |
| `EditorSettings` | 🟡 | 编辑器设置壳 |
| `ProjectSettings` | 🟡 | 项目设置壳 |
| `Lightmapping` | 🟡 | Bake/Clear/LightingSettings 壳 |

---

## 27. Anity 特有模块

| 模块 | 状态 | 备注 |
|------|------|------|
| `Anity.Core` 编译 | ✅ | 0 错误 |
| `Anity.Core.Unity` 编译 | ✅ | 0 错误 |
| `Anity.WebGL` 编译 | ✅ | 0 错误 |
| `Anity.Hub` 编译 | ✅ | 0 错误 |
| `Anity.Editor.Host` 编译 | ✅ | 0 错误 |
| `Anity.Core.Analyzers` | ✅ | AOT/API 兼容分析器 |
| `HotUpdateContext` | 🟡 | 程序集加载壳 |
| `Il2CppRuntime` | 🟡 | 平台检测壳 |
| `PlatformConfig` | 🟡 | 配置壳 |

---

## 28. 高频缺失清单（本批次已补齐，下一步继续深实现）

1. ~~运行时核心：`Screen`、`SystemInfo`、`GL`~~（已壳化）
2. ~~资源与网络：`AssetBundle`、`UnityWebRequest`、`WWW`、`DownloadHandler`~~（已壳化）
3. ~~音频：`AudioListener`、`AudioMixer`、`AudioMixerGroup`~~（已壳化）
4. ~~动画：`Animation`（Legacy）、`StateMachineBehaviour`、`BlendTree`~~（已壳化）
5. ~~AI：`NavMesh` 基础导航 API~~（已壳化）
6. ~~视频：`VideoPlayer`、`VideoClip`~~（已壳化）
7. ~~TextMeshPro：`TMP_Text`、`FontAsset`~~（已壳化）
8. ~~3D 物理真实现：`Physics.Simulate`、球/盒扫掠与碰撞响应~~（核心实现完成，`RaycastAll`/`CheckSphere`/`CheckBox`/`CheckCapsule` 已接入，`Cast` 为步进扫掠近似）
9. ~~2D 物理补全：`CapsuleCollider2D`、`EdgeCollider2D`、`PolygonCollider2D`、`PhysicsMaterial2D`、各种 Joint2D/Effector2D~~（核心实现完成，`Physics2D.Cast` 系列已接入）
10. ~~UI 补全：`RawImage`、`Outline`、`Shadow`、`PositionAsUV1`~~（已实现）
11. ~~Renderer 补全：真实 `Camera.Render`、阴影/光照探针~~（Camera.Render 已接入 SRP，探针已壳化）
12. ~~编辑器补全：`EditorSettings`、`ProjectSettings`、`Lightmapping` 接口~~（已壳化）
13. ~~UI 交互组件：`Dropdown`/`InputField`/`ScrollRect`/`Scrollbar`~~（核心交互已落地）

### 仍待深实现（下一批）

- 3D 物理：`SphereCast`/`BoxCast`/`CapsuleCast` 真实连续扫掠（当前为步进近似）、`OverlapCapsule` 填充结果
- 2D 物理：多边形/边缘/胶囊碰撞器旋转与精确 SAT、`Physics2D.Cast` 多结果返回
- 动画：`Animator` 基于 `RuntimeAnimatorController`/`AnimationClip` 的真正状态机与 BlendTree 驱动
- 音频：`AudioMixer` 快照权重真正影响参数、Group 路由
- UI：真实渲染回填（Text 网格、Image 填充、`Dropdown` 弹窗、`ScrollRect` 自动布局）
- 输入：EventSystem 与 StandaloneInputModule 真正分发 Pointer/Submit 事件
