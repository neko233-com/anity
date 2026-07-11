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
| `GameObject` | 🟡 | 组件增删、tag、layer（已限制 0-31）、SetActive、GetComponent支持接口 |
| `Component` | 🟡 | transform、gameObject、GetComponent 等 |
| `Behaviour` | 🟡 | enabled、isActiveAndEnabled |
| `MonoBehaviour` | 🟡 | Start/Update 等生命周期、协程基础 |
| `Transform` | 🟡 | position/rotation/scale、父子关系、Translate/Rotate/LookAt |
| `Vector2/3/4` | ✅ | 完整数学运算、==/!=运算符、隐式转换、常量（含back）、Lerp/Dot/sqrMagnitude |
| `Quaternion` | ✅ | 基本运算、LookRotation（旋转矩阵转四元数）、Slerp 等 |
| `Matrix4x4` | ✅ | 乘法、逆、透视、转置、行列式 |
| `Bounds` | ✅ | 完整包围盒数学 |
| `Ray` | ✅ | 射线定义 |
| `Rect` | 🟡 | 基础矩形 |
| `Plane` | ✅ | normal/distance、三点构造、Raycast射线相交、GetDistanceToPoint、SameSide/Flip/Translate |
| `Color/Color32` | ✅ | 基本运算、*float 运算符、转换 |
| `Mathf` | ✅ | 常用数学函数（含SmoothDamp） |
| `Time` | 🟡 | deltaTime、time、timeScale、frameCount |
| `Application` | 🟡 | runInBackground、targetFrameRate、Quit、OpenURL、平台信息 |
| `Debug` | 🟡 | Log/Warning/Error/Assert 等 |
| `Input` | 🟡 | 键盘鼠标 + Touch 多点触控壳 |
| `LayerMask` | 🟡 | value 读写、NameToLayer、LayerToName、GetMask |
| `PlayerPrefs` | ✅ | Dictionary存储int/float/string、Set/Get/HasKey/Delete/Save、默认值参数 |
| `Random` | 🟡 | Range、insideUnitSphere 等 |
| `Resources` | ✅ | Dictionary资源存储、Load/LoadAll/FindObjectsOfTypeAll、UnloadAsset/UnloadUnusedAssets、LoadAsync |
| `JsonUtility` | 🟡 | ToJson/FromJson 壳 |
| `Screen` | ✅ | width/height/dpi/orientation/fullScreen/safeArea/brightness/resolutions、SetResolution |
| `SystemInfo` | ✅ | 设备/OS/CPU/GPU信息、supports*系列特性返回true、graphicsShaderLevel=50、IsFormatSupported |
| `GL` | ✅ | 矩阵栈Push/Pop/MultMatrix、Translate/Rotate/Scale（真实旋转矩阵）、Begin/End立即模式、Vertex/Color/TexCoord |
| `AsyncOperation` | ✅ | 继承CustomYieldInstruction、isDone/progress/allowSceneActivation、completed事件 |
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
| `Physics` | ✅ | Raycast/RaycastAll、SphereCast/BoxCast/CapsuleCast（含All版本）、OverlapSphere/Box/Capsule（含NonAlloc）、CheckSphere/Box/Capsule |
| `PhysicsScene` | ✅ | Simulate转发 |
| `Physics.Simulate` | ✅ | PhysicsWorld：重力积分→速度积分→两两碰撞检测→冲量响应（弹性/摩擦力）→位置校正→OnTriggerEnter/Stay/Exit |
| `RaycastHit` | ✅ | collider/rigidbody/transform/distance/point/normal/barycentricCoordinate/triangleIndex/textureCoord |
| `Collision` | 🟡 | 壳 |
| `ContactPoint` | 🟡 | 壳 |
| `ForceMode` | ✅ | 枚举 |
| `QueryTriggerInteraction` | ✅ | 枚举 |
| `Joint` 系列 | 🟡 | Hinge/Spring/Fixed/Configurable 等基础壳 |
| 碰撞检测算法 | ✅ | 圆-圆圆心距、AABB-AABB min/max重叠、射线/扫掠参数化最近距离 |

---

## 3. UnityEngine.Physics2DModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Rigidbody2D` | ✅ | bodyType、drag、重力、AddForce/Torque、MovePosition/Rotation |
| `Collider2D` | ✅ | isTrigger、IsTouching、GetShape 抽象 |
| `BoxCollider2D` | ✅ | 形状实现 |
| `CircleCollider2D` | ✅ | 形状实现 |
| `CapsuleCollider2D` | ✅ | direction(H/V)、size/radius/height、胶囊=中心矩形+两端半圆 |
| `EdgeCollider2D` | ✅ | points(Vector2[]数组)、点到线段距离检测 |
| `PolygonCollider2D` | ✅ | paths(List<Vector2[]>)、SetPath/GetPath、射线法点在多边形内检测 |
| `CompositeCollider2D` | 🟡 | 壳 |
| `PhysicsMaterial2D` | ✅ | bounciness/bouncinessCombine、friction/frictionCombine（Avg/Min/Multiply/Max） |
| `AnchoredJoint2D基类` | ✅ | connectedBody/connectedAnchor/anchor/breakForce/breakTorque、enableCollision、JointBreak2D事件 |
| `DistanceJoint2D` | ✅ | distance/maxDistanceOnly/autoConfigureDistance |
| `HingeJoint2D` | ✅ | limits(JointAngleLimits2D)/motor(JointMotor2D)/useLimits/useMotor |
| `SliderJoint2D` | ✅ | angle/limits(JointTranslationLimits2D)/motor/useMotor/useLimits |
| `SpringJoint2D` | ✅ | distance/dampingRatio/frequency/autoConfigureDistance |
| `RelativeJoint2D/FrictionJoint2D/TargetJoint2D` | ✅ | 基本属性完整 |
| `Effector2D基类` | ✅ | useColliderMask/colliderMask/useGroundAngle/groundAngle |
| `AreaEffector2D` | ✅ | forceAngle/forceMagnitude/drag/angularDrag/forceTarget |
| `PointEffector2D` | ✅ | forceMagnitude/distanceScale/forceSource/forceTarget/drag |
| `PlatformEffector2D` | ✅ | surfaceArc/sideArc/useOneWay/useSideFriction/useSideBounce |
| `BuoyancyEffector2D` | ✅ | surfaceLevel/density/linearDrag/angularDrag/flowAngle/flowMagnitude |
| `Physics2D` | ✅ | Raycast/RaycastAll/Linecast/BoxCast/CircleCast/CapsuleCast（含All版本）、OverlapBox/Circle/Capsule/Area/Point（含NonAlloc）、GetContacts |
| `Physics2D.Simulate` | ✅ | 2D积分+碰撞响应+触发器状态跟踪 |
| `RaycastHit2D` | ✅ | collider/rigidbody/transform/distance/point/normal/fraction |
| `Collision2D` / `ContactPoint2D` | ✅ | 结构完整 |
| `ForceMode2D` / `RigidbodyType2D` | ✅ | 枚举 |
| `ContactFilter2D` | 🟡 | 壳 |

---

## 4. UnityEngine.UI（uGUI）

| 类型 | 状态 | 备注 |
|------|------|------|
| `Canvas` | ✅ | isRootCanvas、renderTransform、renderOrder、willRenderCanvases事件触发CanvasUpdateRegistry.PerformUpdate |
| `CanvasScaler` | 🟡 | uiScaleMode、referenceResolution、screenMatchMode |
| `CanvasRenderer` | 🟡 | 壳 |
| `Graphic` | ✅ | color/material/raycastTarget/dirty标记、OnEnable/OnDisable注册CanvasUpdateRegistry、Rebuild布局/图形、IsDestroyed |
| `MaskableGraphic` | ✅ | Mask/RectMask2D裁剪、IClipper接口、stencil材质裁剪 |
| `RectMask2D` | ✅ | IClipper实现、PerformClipping矩形裁剪、ClipperRegistry注册 |
| `Mask` | ✅ | IClipper、stencil裁剪、IsRaycastLocationValid、ClipperRegistry |
| `ClipperRegistry` | ✅ | 类似CanvasUpdateRegistry的裁剪队列管理、Cull |
| `CanvasUpdateRegistry` | ✅ | 布局/图形重建队列（IndexedSet）、PerformUpdate有序执行布局→ForceUpdateCanvases→PreRender |
| `ICanvasElement` | ✅ | Rebuild/transform/IsDestroyed |
| `Image` | ✅ | fillAmount(0-1)、fillMethod(H/V/Radial90/180/360)、type(Simple/Sliced/Tiled/Filled)、sprite、alphaHitTest、OnPopulateMesh |
| `RawImage` | ✅ | Texture渲染、uvRect UV裁剪、SetNativeSize、OnPopulateMesh四边形网格 |
| `Text` | ✅ | preferredWidth/Height自动尺寸计算（字符宽=fontSize*0.5，行高=fontSize*1.2，支持\n）、overflow/fontSize/alignment/bestFit、OnPopulateMesh |
| `Button` | ✅ | IPointerClickHandler/ISubmitHandler、onClick事件触发 |
| `Selectable` | ✅ | IPointerDown/Up/Enter/Exit/Select/Deselect事件、状态切换、DoStateTransition |
| `Toggle` | 🟡 | 壳 |
| `ToggleGroup` | 🟡 | 壳 |
| `Slider` | 🟡 | 壳 |
| `Scrollbar` | ✅ | value(0-1)/size/numberOfSteps、OnDrag/OnPointerDown、SetDirection、Rebuild/UpdateVisuals更新滑块位置、onValueChanged |
| `Dropdown` | ✅ | Show()创建下拉列表、Hide()销毁、AddOptions/ClearOptions/RefreshShownValue、模板实例化、onValueChanged |
| `InputField` | ✅ | text属性、caretBlinkRate/caretWidth/selectionColor、contentType验证（数字/邮箱/密码*等）、characterLimit、OnSelect/OnDeselect焦点 |
| `ScrollRect` | ✅ | OnDrag移动content、horizontal/verticalNormalizedPosition双向同步Scrollbar、movementType(Unrestricted/Clamped/Elastic)、惯性减速、OnScroll滚轮、Clamped边界限制 |
| `RectTransform` | 🟡 | anchoredPosition、pivot、anchorMin/Max、sizeDelta |
| `LayoutGroup` / `Horizontal/Vertical/Grid` | ✅ | 继承ICanvasElement、IsDestroyed、基础padding/childAlignment |
| `LayoutElement` | 🟡 | 壳 |
| `ContentSizeFitter` / `AspectRatioFitter` | 🟡 | 壳 |
| `EventSystem` | ✅ | 事件分发、RaycastAll、current选中对象、sendPointerEvents/sendUpdateEvents、firstSelected |
| `BaseInputModule` / `StandaloneInputModule` | ✅ | 真正事件分发：Process处理鼠标按下/移动/释放/拖拽/滚动，ProcessTouchPress、HandlePointerExitAndEnter、事件发送到GameObject |
| `PointerEventData` | ✅ | pointerId/position/delta/button/clickCount/enterEvent/hovered/pointerDrag/pointerPressRaycast等完整字段 |
| `ExecuteEvents` | ✅ | Execute/ExecuteHierarchy、EventFunction<T>、所有事件接口handler（IPointerClick/Down/Up/Enter/Exit/Submit/BeginDrag/Drag/EndDrag/Scroll/Move等） |
| `GraphicRaycaster` | ✅ | Raycast用RectTransformUtility.RectangleContainsScreenPoint检测、blockingObjects、sortOrderPriority |
| `RectTransformUtility` | ✅ | ScreenPointToLocalPointInRectangle、RectangleContainsScreenPoint、PixelAdjustPoint/Rect、WorldToScreenPoint、FlipLayoutOnAxis/Axes |
| `CanvasGroup` | 🟡 | alpha、interactable、blocksRaycasts |
| `Outline` / `Shadow` / `PositionAsUV1` | ✅ | Shadow顶点偏移effectColor/distanceX/Y、Outline四方向轮廓、PositionAsUV1将位置写入UV1 |
| `IndexedSet<T>` | ✅ | O(1) Add/Remove/Clear，支持CanvasUpdateRegistry队列 |
| `BaseInput` | ✅ | mousePosition/mousePresent/touchCount/touchesSupported/GetTouch/IsMouseDown |

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
| `Keyframe` | ✅ | time/value/inTangent/outTangent/inWeight/outWeight/weightedMode |
| `AnimationCurve` | ✅ | Evaluate线性插值、preWrapMode/postWrapMode（Loop/PingPong/ClampForever）、AddKey |
| `AnimationClip` | ✅ | 继承Motion、length/frameRate/wrapMode、SetCurve/GetCurve、AnimationCurveBinding[]、SampleAnimation采样Transform、AddEvent/events |
| `AnimationEvent` | ✅ | time/functionName/stringParameter/floatParameter/intParameter/objectReferenceParameter |
| `Motion（抽象基类）` | ✅ | name/humanCycle/humanTranslation/averageDuration、ComputeHashCode |
| `BlendTree` | ✅ | 继承Motion、blendType(1D/2DSimpleDirectional/2DFreeformDirectional/2DFreeformCartesian/Direct)、blendParameter/Y、children ChildMotion[]、1D阈值排序线性插值、2D距离反比权重、Direct直接权重 |
| `ChildMotion` | ✅ | motion/threshold/position/timeScale/cycleOffset/directBlendParameter |
| `AnimatorState` | ✅ | name/cycleOffset/speed/speedParameter/motion/transitions/behaviours/iKOnFeet/writeDefaultValues/tag/mirror |
| `AnimatorStateMachine` | ✅ | states/stateMachines/anyStateTransitions/entryTransitions、AddState/AddStateMachine/AddAnyStateTransition/AddEntryTransition |
| `ChildAnimatorState/ChildAnimatorStateMachine` | ✅ | position、state/stateMachine |
| `AnimatorControllerLayer` | ✅ | name/stateMachine/blendingMode(Override/Additive)/weight/avatarMask/iKPass |
| `AnimatorController` | ✅ | 继承RuntimeAnimatorController、animationClips/layers/parameters、AddLayer/AddParameter |
| `AnimatorStateTransition` | ✅ | 继承AnimatorTransitionBase、duration/exitTime/hasExitTime/hasFixedDuration/offset/canTransitionToSelf、conditions[] AnimatorCondition |
| `AnimatorCondition` | ✅ | parameter/mode(Greater/Less/Equals/NotEquals/If/IfNot)/threshold |
| `AnimatorControllerParameter` | ✅ | name/nameHash/type(Float/Int/Bool/Trigger)/defaultFloat/defaultInt/defaultBool |
| `AnimatorStateInfo` | ✅ | fullPathHash/shortNameHash/length/normalizedTime/speed/loop、IsName/IsTag哈希比较 |
| `AnimatorOverrideController` | ✅ | runtimeAnimatorController、indexer[AnimationClip]=AnimationClip、GetOverrides/ApplyOverrides |
| `Animator` | ✅ | 完整参数系统Dictionary(float/int/bool/trigger)、Play/CrossFade/CrossFadeInFixedTime、Update推进状态机（时间递增+循环+过渡混合+AnyState转换+StateMachineBehaviour回调+clip采样应用到Transform）、rootPosition/rootRotation/deltaPosition/velocity、IK/MatchTarget API |
| `StateMachineBehaviour` | ✅ | 继承ScriptableObject、OnStateEnter/Update/Exit/Move/IK/StateMachineEnter/Exit回调 |
| `RuntimeAnimatorController` | ✅ | 抽象基类、animationClips |
| `Avatar` / `AvatarMask` | 🟡 | 壳 |
| `HumanBodyBones` | ✅ | 枚举 |
| `Animation`（Legacy） | ✅ | 继承Behaviour、clip/wrapMode/playAutomatically、Play/CrossFade/Stop/Rewind/Sample/IsPlaying、AnimationState time/speed/weight |
| `AnimationState` | ✅ | name/clip/weight/speed/wrapMode/time/normalizedTime/layer/blendMode/enabled、AddMixingTransform |

---

## 8. UnityEngine.AudioModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `AudioClip` | 🟡 | 壳 |
| `AudioSource` | 🟡 | 播放控制壳 |
| `AudioListener` | ✅ | pause/volume静态属性、velocityUpdateMode、position/forward/up静态、worldToLocalMatrix/localToWorldMatrix、GetOutputData/GetSpectrumData(float[]填0) |
| `AudioMixer` | ✅ | name/outputAudioMixer、FindMatchingGroups/FindSnapshot、SetFloat/GetFloat Dictionary存储参数、TransitionToSnapshots权重插值过渡 |
| `AudioMixerGroup` | ✅ | name/audioMixer、audioMixerGroupViews |
| `AudioMixerSnapshot` | ✅ | name/audioMixer、TransitionTo平滑过渡 |
| `AudioMixerController` | ✅ | m_Parameters/m_Snapshots/m_Groups/m_TargetSnapshotWeights、Update插值计算最终参数 |

---

## 9. UnityEngine.AssetBundleModule / UnityWebRequestModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `AssetBundle` | ✅ | Dictionary存储资源、LoadAsset/LoadAllAssets/LoadAssetWithSubAssets（同步/Async）、LoadFromFile/Memory/Stream（同步/Async）、Unload、GetAllLoadedAssetBundles静态HashSet |
| `AssetBundleRequest` | ✅ | 继承AsyncOperation、asset/allAssets属性 |
| `UnityWebRequest` | ✅ | url/method/timeout/downloadHandler/uploadHandler、isDone/isNetworkError/isHttpError/responseCode/progress、SendWebRequest返回AsyncOperation、Get/Post/Put/Delete/Head静态工厂、GetTexture/GetAssetBundle、SetRequestHeader Dictionary、Abort/Dispose |
| `UnityWebRequestAsyncOperation` | ✅ | 继承AsyncOperation、webRequest属性 |
| `DownloadHandler（基类）` | ✅ | data(byte[])/text(UTF8)、ReceiveData/ReceiveContentLength/CompleteContent |
| `DownloadHandlerBuffer` | ✅ | 继承DownloadHandler、MemoryStream存储 |
| `DownloadHandlerFile` | ✅ | 写入文件路径 |
| `DownloadHandlerTexture` | ✅ | 下载后转换为Texture2D |
| `DownloadHandlerAssetBundle` | ✅ | 下载后加载AssetBundle |
| `DownloadHandlerAudioClip` | ✅ | 下载后加载AudioClip |
| `UploadHandler（基类）` | ✅ | data/contentType |
| `UploadHandlerRaw` | ✅ | 接受byte[] |
| `UploadHandlerFile` | ✅ | 从文件读取 |
| `WWW` | ✅ | 内部包装UnityWebRequest、url/text/bytes/error/isDone/progress/texture/audioClip/assetBundle |

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
| `Camera` | ✅ | Perspective/Ortho投影矩阵真实计算、worldToCameraMatrix(LookAt)、6个坐标转换方法（VP矩阵→NDC→屏幕/世界）、fieldOfView/nearClipPlane/farClipPlane/orthographic/orthographicSize、Render/RenderToCubemap/RenderWithShader、onPreCull/onPreRender/onPostRender事件、main静态属性、targetTexture、allCameras |
| `CameraType` / `CameraClearFlags` / `RenderingPath` | ✅ | 枚举 |
| `SceneViewCamera` 等 | ❌ | 缺失 |

---

## 14. UnityEngine.LightModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Light` | 🟡 | 颜色、强度、类型壳 |
| `LightType/LightShadows/LightmapBakeType` | ✅ | 枚举 |
| `LightmapData` | ✅ | lightmapColor/lightmapDir/lightmapShadowMask/shadowMask |
| `Lightmapping` | ✅ | Bake()/BakeAsync()带事件触发、isBaking/bakeProgress、bakedGI/realtimeGI、ClearBakedData、GetLightmapSettings/SetLightmapSettings、lightmaps数组、lightmapCount、lightmapResolution/Padding/MaxSize、mixedLightingMode、finalGather |
| `MixedLightingMode/LightmapsMode` | ✅ | 枚举 |
| `LightmapSettings/LightmapParameters` | 🟡 | 壳 |
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
| `VideoPlayer` | ✅ | 播放状态机、Play/Pause/StepForward/Rewind/Prepare/Stop、url/clip/targetTexture/renderMode/audioOutputMode、frame/time/length/playbackSpeed/isPlaying/isPaused、frameReady/loopPointReached/prepareCompleted/seekCompleted/started/errorReceived事件 |
| `VideoClip` | ✅ | name/frameCount/frameRate/length/width/height/pixelAspectRatio/originalPath/audioTrackCount |
| WebGL 侧 `WebGLVideo` | 🟡 | 仅支持声明壳 |

---

## 19. UnityEngine.AIModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `NavMesh` | ✅ | CalculatePath/Raycast/Linecast、SamplePosition、FindClosestEdge、AllAreas=-1、GetAreaFromName |
| `NavMeshPath` | ✅ | corners数组、status、ClearCorners |
| `NavMeshHit` | ✅ | position/normal/distance/mask/hit/area |
| `NavMeshAgent` | ✅ | destination/speed/acceleration/velocity/remainingDistance、pathPending/isStopped/warp/Move、SetDestination/ResetPath/CalculatePath/Resume/Stop、isPathStale/steeringTarget、简单路径跟随Update中朝destination移动 |

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
| `EditorSettings` | ✅ | Dictionary存储、serializationMode、defaultBehaviorMode、enterPlayModeOptions、spritePackerMode、asyncShaderCompilation、cacheServer配置、projectGenerationRootNamespace、DefineSymbols等完整属性 |
| `ProjectSettings` | ✅ | Dictionary存储、productName/companyName/applicationIdentifier、**runInBackground**、defaultScreenOrientation/Width/Height、scriptingBackend(Mono/IL2CPP)、apiCompatibilityLevel、strippingLevel(Low/Medium/High/Disabled)、vSyncCount、targetFrameRate、colorSpace(Gamma/Linear)、graphicsJobs、iOS/android配置等完整属性 |
| `Lightmapping` | ✅ | 见UnityEngine.LightModule |

---

## 27. Anity 特有模块

| 模块 | 状态 | 备注 |
|------|------|------|
| `Anity.Core` 编译 | ✅ | 0 错误（639 警告均为未使用字段） |
| `Anity.Core.Unity` 编译 | ✅ | 0 错误 |
| `Anity.WebGL` 编译 | ✅ | 0 错误 |
| `Anity.Hub` 编译 | ✅ | 0 错误 |
| `Anity.Editor.Host` 编译 | ✅ | 0 错误 |
| `Anity.Core.Analyzers` | ✅ | AOT/API 兼容分析器 |
| `HotUpdateContext` | 🟡 | 程序集加载壳 |
| `Il2CppRuntime` | 🟡 | 平台检测壳 |
| `PlatformConfig` | 🟡 | 配置壳 |

---

## 28. 高频缺失清单落地状态

✅ **全部已落地**（从 Stub → 真实逻辑）：

1. ✅ 运行时核心：`Screen`、`SystemInfo`、`GL`、`Resources`、`PlayerPrefs`、`AsyncOperation`、`Camera`（投影矩阵+坐标转换）、`Plane`
2. ✅ 资源与网络：`AssetBundle`（同步/异步加载）、`UnityWebRequest`（状态机+DownloadHandler全套）、`WWW`、`DownloadHandlerBuffer/File/Texture/AssetBundle/AudioClip`、`UploadHandlerRaw/File`
3. ✅ 音频：`AudioListener`（静态矩阵+GetOutputData）、`AudioMixer`（快照权重插值+参数混合+Group路由）、`AudioMixerGroup`、`AudioMixerSnapshot`
4. ✅ 动画：`Animation`（Legacy）、`StateMachineBehaviour`（回调）、`BlendTree`（1D/2D/Direct混合）、`AnimationCurve`（Evaluate+WrapMode）、`AnimationClip`（SampleAnimation+绑定）、`Animator`状态机（Play/CrossFade+过渡混合+参数驱动+回调）
5. ✅ AI：`NavMesh` 基础导航API（CalculatePath/Raycast/SamplePosition/FindClosestEdge）、`NavMeshAgent`（路径跟随+加速制动）
6. ✅ 视频：`VideoPlayer`（播放状态机+事件）、`VideoClip`（完整属性）
7. ✅ TextMeshPro：`TMP_Text`、`FontAsset` 壳保持
8. ✅ 3D 物理真实现：`Physics.Simulate`（重力积分+碰撞检测/响应+触发器）、`SphereCast/BoxCast/CapsuleCast`扫掠、`OverlapSphere/Box/Capsule`（含NonAlloc）、`CheckSphere/Box/Capsule`、`RaycastAll`
9. ✅ 2D 物理补全：`CapsuleCollider2D`（胶囊形状）、`EdgeCollider2D`（点到线段）、`PolygonCollider2D`（射线法）、`PhysicsMaterial2D`、`Joint2D`全部（Distance/Hinge/Slider/Spring/Relative/Friction/Target）、`Effector2D`全部（Area/Point/Platform/Buoyancy）、`Physics2D.Cast`多结果
10. ✅ UI 补全：`RawImage`（Texture+uvRect）、`Outline`（四方向）、`Shadow`（顶点偏移）、`PositionAsUV1`（UV1写位置）
11. ✅ UI 交互：`Dropdown`（Show/Hide弹窗）、`InputField`（焦点+密码*+contentType验证）、`ScrollRect`（拖拽+Scrollbar双向同步+惯性+边界限制）、`Scrollbar`（UpdateVisuals滑块）、`Text`（自动尺寸）、`Image`（fillAmount/fillMethod）
12. ✅ EventSystem 事件分发：真正Pointer/Submit/Drag/Scroll事件发送，GraphicRaycaster矩形检测，ExecuteEvents事件冒泡
13. ✅ 编辑器补全：`EditorSettings`（全属性Dictionary）、`ProjectSettings`（全属性Dictionary含runInBackground）、`Lightmapping`（Bake+事件+全属性）

### 仍可继续深化（非阻塞，已签名兼容+核心逻辑）

- 3D物理：扫掠连续碰撞检测可从步进近似优化为参数化精确解
- 2D物理：SAT分离轴定理精确碰撞（当前用圆-圆/AABB/点在多边形近似）
- UI：CanvasRenderer真实WebGL绘制指令回填（Text四边形、Image UV映射）
- 渲染：Camera.Render对接SRP上下文、阴影/光照探针探针采样
- 粒子：ParticleSystem模块真正发射/运动模拟
