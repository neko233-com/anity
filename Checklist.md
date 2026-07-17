# Unity 2022.3.61f1 Pro 兼容性功能清单

> 本文件记录 Anity 对 Unity 2022.3.61f1 Pro 及其官方包的实现状态；当前本机自动审计证据仍为 2022.3.51f1，必须迁移到 2022.3.61f1 后才能作为最终版本证据。
> 状态说明：
> - ✅ 已实现并通过备注所列范围的行为测试（只代表该行证据，不代表 Unity 全局对等）
> - 🟡 部分实现 / API 壳 / 尚缺 Unity 官方 A/B 行为证据
> - ❌ 未实现
>
> **全局状态：🟡 持续推进。** “Anity = 源码自主可控的 Unity 2022 Ultra” 是最终验收目标；只有官方 Unity 2022.3 反射面、行为 fixture、编辑器交互及各平台产物门禁全部通过后，才能宣称完全对等。官方 2022.3.51f1 当前基线：类型存在 928/4,117（22.541%）、类型契约完全一致 404（9.813%）；成员存在 8,645/37,164（23.262%）、成员契约完全一致 6,417（17.267%）；缺失类型 3,189、真实缺失成员 28,519。

---

## 1. UnityEngine.CoreModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Object` | 🟡 | 类型及全部官方公开成员反射精确；无错误 IDisposable；帧末 Destroy、同步 DestroyImmediate、FindFirst/Any/inactive filter、隐式 bool、同步/异步 Instantiate 已测；同步 parent 默认/保留世界/显式姿态/目标 Scene clone 已通过官方 A/B，仍需扩大序列化对象图与 native 资源 clone 矩阵 |
| `GameObject` | 🟡 | 类型及全部官方公开成员反射差异 0；组件查询/索引/消息、Require/Disallow、21 个 legacy/bulk API、GetScene、批量激活/实例化、目标 Scene 整树迁移、inactive clone 生命周期及同步 clone/reparent 矩阵已通过官方 A/B；optional Scene=null 由构建期 metadata fixup 精确复现，仍需扩大复杂组件对象图 |
| `Component` | 🟡 | 类型及全部官方公开成员反射差异 0；查询/消息重载支持 interface 与 List；13 个 legacy 属性的 obsolete metadata 与异常文本精确；仍需扩大官方生命周期 A/B 矩阵 |
| legacy networking removed surface | ✅ | `NetworkView` / `NetworkPlayer` / `NetworkViewID` / `NetworkMessageInfo` / `NetworkStateSynchronization` / `RPCMode` 精确复现 Unity 2022.3 的 `Obsolete(error:true)`、EditorBrowsable 与 `NotSupportedException`；测试 32+ |
| `Behaviour` | ✅ | 类型及全部官方成员反射精确；enabled 触发 OnEnable/OnDisable、isActiveAndEnabled |
| `MonoBehaviour` | 🟡 | 类型及全部官方成员反射精确；Unity 消息按名称派发，支持 private/protected 与 IEnumerator Start，协程/Invoke/销毁令牌已测；仍缺官方 batchmode 全生命周期 A/B |
| 组件元数据特性 | 🟡 | `RequireComponent` / `AddComponentMenu` / `ContextMenu` / `ContextMenuItemAttribute` / `DefaultExecutionOrder` / `DisallowMultipleComponent` / `ExecuteAlways` / `ExecuteInEditMode` / `HideInInspector` / `HelpURLAttribute` 已与官方反射面精确一致；Require/Disallow/默认执行顺序行为测试 15/15；ExecuteAlways/EditMode 编辑器调度仍缺官方 A/B |
| `Transform` | 🟡 | 官方公开反射差异 0；完整父子仿射矩阵链保留多级 shear，投影 lossyScale、负/零 scale quaternion 反射、奇异 worldToLocal 零倒数链及 SetParent true/false 已经官方 A/B；Transform/Scene/Affine 合计 46/46，3 个 C ABI 由实际 dylib 强制执行。对象/层级存储、dirty cache 与 Jobs 并发所有权仍待迁入 native |
| `Vector2/3/4` | ✅ | 完整数学运算、所有常量(zero/one/up/down/left/right/forward/back/positiveInfinity/negativeInfinity)、Lerp/LerpUnclamped/SmoothDamp/MoveTowards/Reflect/Project/ProjectOnPlane/Exclude/OrthoNormalize/ClampMagnitude/Angle/SignedAngle/Perpendicular/Distance/Cross/Dot/Scale/Normalize/Set/implicit conversions |
| `Quaternion` | ✅ | 可变struct，x/y/z/w/identity/eulerAngles、LookRotation/AngleAxis/FromToRotation/Slerp/SlerpUnclamped/Lerp/LerpUnclamped/RotateTowards/Euler/Angle/Dot/Inverse/Normalize、Set/SetFromToRotation/SetLookRotation/ToAngleAxis、QQ乘法/QV旋转、==/!=、IEquatable；LookRotation 基矩阵布局与 Angle epsilon 已按官方 Transform fixture 修正 |
| `Matrix4x4` / `FrustumPlanes` | 🟡 | 官方公开反射差异 0；native C++ determinant/inverse/transpose/TRS/投影/LookAt/rotation/decompose/ValidTRS 主路径与托管回退；Unity 2022.3.51f1 普通、signed/zero scale、shear、projection、singular、退化 LookAt、格式/异常 A/B；22 测试，托管/native 双模式全门禁通过。仍需扩展 IEEE 极值与跨平台数值一致性，不以此宣称全引擎完成 |
| `Bounds` | ✅ | center/extents/size/min/max、Encapsulate/Intersects/Contains/ClosestPoint/SqrDistance/IntersectRay/Expand/SetMinMax |
| `Ray` | ✅ | origin/direction/GetPoint |
| `Rect` | ✅ | xMin/xMax/yMin/yMax/center/min/max、Contains/Overlaps/Expand/Encapsulate几何 |
| `Plane` | ✅ | normal/distance、三点构造、Raycast射线相交、GetDistanceToPoint/ClosestPointOnPlane/GetSide/SameSide/Flip/Translate |
| `Color/Color32` | ✅ | r/g/b/a、基本运算、*float运算符、Color↔Color32隐式转换、Lerp/grayscale/linear/gamma/maxColorComponent、Vector4隐式转换 |
| `Mathf` | ✅ | 完整数学 API；**PerlinNoise** 为 Improved Perlin 映射到 Unity [0,1] 区间（非恒 0） |
| `AnimationCurve` | ✅ | Cubic Hermite + tangents；Linear/EaseInOut/Constant；Loop/PingPong wrap；SmoothTangents；测试≥14 |
| `Vector3.Slerp` | ✅ | Slerp/SlerpUnclamped 球面插值 |
| `Time` | ✅ | time/timeScale/unscaledTime/fixedDeltaTime/fixedUnscaledTime/smoothDeltaTime/timeSinceLevelLoad/frameCount/realtimeSinceStartup/captureDeltaTime/maximumDeltaTime/maximumParticleDeltaTime/inFixedTimeStep、双精度变体timeAsDouble/unscaledTimeAsDouble/fixedTimeAsDouble/fixedUnscaledTimeAsDouble/realtimeSinceStartupAsDouble/timeSinceLevelLoadAsDouble |
| `Application` | ✅ | 进程信息(PID/isPlaying/isFocused/isPaused/isBatchMode/isEditor)、真实平台路径(dataPath/persistentDataPath/streamingAssetsPath/temporaryCachePath)、RuntimePlatform自动检测(OS/Arch)、unityVersion="2022.3.61f1"、identifier/companyName/productName/version/buildGUID与PlayerSettings同步、systemLanguage/internetReachability/runInBackground/targetFrameRate/sleepTimeout/installMode/sandboxType/productGUID/cloudProjectId/genuine、Quit/OpenURL/SetLogCallback/RequestAdvertisingIdentifierAsync、事件:focusChanged/pausing/logMessageReceived/lowMemory/wantsToQuit/deepLinkActivated |
| `Debug` | ✅ | ILogger/ILogHandler接口、ConsoleLogHandler、Log/Warning/Error/LogException/LogAssertion/Assert、LogFormat全系列、developerConsoleVisible/isDebugBuild/unityLogger、LogType/LogOption枚举 |
| `Input` | ✅ | HashSet存储key/button状态、GetKey/Button/axis、SimulateKeyDown测试API、touchCount/touches(Touch[])/GetTouch、Gyroscope/Compass/LocationService、DeviceOrientation、IMECompositionMode、AccelerationEvent、ResetInputAxes、multiTouchEnabled/touchPressureSupported、onDeviceOrientationChange |
| `Cursor` | ✅ | visible/lockState(CursorLockMode: None/Locked/Confined)、SetCursor(texture,hotspot,CursorMode) |
| `CullingGroup` | ✅ | enabled/onStateChanged/targetCamera、SetBoundingSpheres/SetBoundingDistances/SetBoundingSphereCount、IsVisible/GetDistance/QueryIndices/Dispose、**Query(viewer)** 距离带+OcclusionCulling 联动、BoundingSphere/CullingGroupEvent |
| `LayerMask` | ✅ | NameToLayer/LayerToName字典、GetMask位运算、隐式int转换、内置层 |
| `PlayerPrefs` | ✅ | 类型化JSON、大小写敏感、线程安全、原子Save、类型转换、GetAllKeys、Quit刷盘；测试≥17 |
| `EditorPrefs` | ✅ | 独立持久化、Int/Float/String/Bool、Load/Save原子写、测试隔离路径 |
| `LocalStorage` | ✅ | persistent/temp/streaming/data 路径读写删除 |
| `Random` | 🟡 | `InitState/state/value` 已按 Unity 2022.3 Player 四字 xorshift128 精确实现：5 组官方 State、seed 1 连续 12 值、snapshot restore 与公开面共 **19/19**；`State` 仅有 4 个 private serialized int，无错误公开字段/构造器/额外 API。`Range(int,int)` 的官方无偏映射及 Range/几何/rotation/ColorHSV 的完整分布与极值仍缺 Player A/B，故不能标全完成 |
| `Resources` / `ResourceRequest` | ✅ | Dictionary资源存储、Load/LoadAll/FindObjectsOfTypeAll、UnloadAsset/UnloadUnusedAssets；ResourceRequest 继承/virtual GetResult/get-only asset/native metadata 反射精确，LoadAsync 初始 pending、asset 同步解析不强制完成、PlayerLoop/协程/帧末回调经官方 A/B |
| `JsonUtility` | ✅ | System.Text.Json序列化/反序列化、FromJsonOverwrite反射覆盖 |
| `Screen` | ✅ | width/height/dpi/orientation/fullScreen/safeArea/brightness/resolutions、SetResolution |
| `SystemInfo` | ✅ | 设备/OS/CPU/GPU信息(deviceName/model/type/uniqueId/operatingSystem/processorCount/frequency/systemMemorySize)、graphicsDeviceType支持D3D11/D3D12/Vulkan/Metal/OpenGLCore/OpenGLES2/OpenGLES3/WebGL2、graphicsDeviceVersion自动匹配、supports*系列特性、graphicsShaderLevel=50、IsFormatSupported、overrideGraphicsDeviceType支持构建切换 |
| `GL` | ✅ | 矩阵栈Push/Pop/MultMatrix、Translate/Rotate/Scale（真实旋转矩阵）、Begin/End立即模式、Vertex/Color/TexCoord |
| `AsyncOperation` | ✅ | 官方 `YieldInstruction` 继承、native 元数据及全部公开成员反射精确；isDone/progress/allowSceneActivation、自动完成帧首集成、协程先恢复、原 completion 帧末派发、阻塞完成同步回调及晚订阅即时回调已测 |
| `AsyncInstantiateOperation<T>` | ✅ | 两个 operation 类型及 `Object.InstantiateAsync<T>` 10/10 重载反射精确；分片预算、typed Result、span 循环、父节点世界变换、取消清理、完成/晚订阅、场景激活门均经官方 batchmode 对照与 20/20 测试 |
| `ScriptableObject` | ✅ | CreateInstance&lt;T&gt;/CreateInstance(Type) |
| `RectTransform` | 🟡 | 本体/nested types 与 DrivenRectTransformTracker/DrivenTransformProperties 公开反射差异 0；官方默认/anchor/pivot/anchored-local/offset/stretch/inset/corners、tracker 共享登记/null/Clear A/B；GameObject 单一 Transform 替换；Canvas native dispatch 与编辑器 Rect Tool 仍待闭环 |

---

## 2. UnityEngine.PhysicsModule（3D 物理）

| 类型 | 状态 | 备注 |
|------|------|------|
| `ConstantForce` | ✅ | 官方签名/RequireComponent/native metadata 精确；world/relative force/torque、质量/惯量、逐步累加、disabled/inactive/kinematic/睡眠唤醒、多组件经官方 play-mode A/B 与 native C ABI 深测 |
| `Rigidbody` | ✅ | 速度/角速度积分、质量/阻尼/约束、4种AddForce模式、AddTorque/AddExplosionForce、Sleep/WakeUp |
| `Collider` | ✅ | attachedRigidbody、isTrigger、PhysicMaterial、bounds、ClosestPoint/Raycast、OnCollision/OnTrigger事件 |
| `BoxCollider` | ✅ | center/size、世界空间AABB |
| `SphereCollider` | ✅ | center/radius、世界空间AABB |
| `CapsuleCollider` | ✅ | center/radius/height/direction、世界空间AABB |
| `MeshCollider` | ✅ | 基础形状、世界空间AABB |
| `CharacterController` | ✅ | SimpleMove重力+速度、Move分步碰撞检测+CollisionFlags、isGrounded |
| `WheelCollider` | ✅ | 悬挂弹簧阻尼、frictionCurve、motor/brake/steer、rpm、GetGroundHit/GetWorldPose、ConfigureVehicleSubsteps |
| `VehicleChassis` / `VehicleUtility` | ✅ | 多轮编排、ApplyInput 油门/转向/刹车、CreateSimpleCar 四轮布局；测试≥10 |
| `Physics` | ✅ | Raycast/RaycastAll、SphereCast/BoxCast/CapsuleCast（含All版本）、OverlapSphere/Box/Capsule（含NonAlloc）、CheckSphere/Box/Capsule |
| `PhysicsScene` | ✅ | Simulate转发 |
| `Physics.Simulate` | ✅ | PhysicsWorld：重力积分→速度积分→两两碰撞检测→冲量响应（弹性/摩擦力）→位置校正→OnTriggerEnter/Stay/Exit |
| `RaycastHit` | ✅ | collider/rigidbody/transform/distance/point/normal/barycentricCoordinate/triangleIndex/textureCoord |
| `Collision` | ✅ | collider/rigidbody/contacts/relativeVelocity/impulse |
| `ContactPoint` | ✅ | 完整结构 |
| `ForceMode` | ✅ | 枚举 |
| `QueryTriggerInteraction` | ✅ | 枚举 |
| `PhysicMaterial` | ✅ | bounciness/friction、4种Combine模式、static/dynamicFriction |
| `Joints (Hinge/Spring/Fixed/Character/Configurable)` | ✅ | anchor/connectedBody/limits/motor/spring/breakForce、JointDrive/SoftJointLimit/JointMotor |
| `碰撞检测算法` | ✅ | 圆-圆圆心距、AABB-AABB min/max重叠、射线/扫掠参数化最近距离 |

---

## 3. UnityEngine.Physics2DModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `PhysicsUpdateBehaviour2D` / `ConstantForce2D` | ✅ | 官方继承/ sealed /属性/RequireComponent/native metadata 精确；world/relative force、torque、重力共存、disabled/inactive/simulated/kinematic/睡眠唤醒及重新激活已测 |
| `Rigidbody2D` | ✅ | bodyType、drag、重力、AddForce/Torque、MovePosition/Rotation |
| `Collider2D` | ✅ | isTrigger、IsTouching、GetShape 抽象 |
| `BoxCollider2D` | ✅ | 形状实现 |
| `CircleCollider2D` | ✅ | 形状实现 |
| `CapsuleCollider2D` | ✅ | direction(H/V)、size/radius/height、胶囊=中心矩形+两端半圆 |
| `EdgeCollider2D` | ✅ | points(Vector2[]数组)、点到线段距离检测 |
| `PolygonCollider2D` | ✅ | paths(List&lt;Vector2[]&gt;)、SetPath/GetPath、射线法点在多边形内检测 |
| `CompositeCollider2D` | ✅ | **完整Unity 2022功能**：geometryType(Outlines/Polygons)、generationType(Synchronous/WhenTriggered/Manual)、vertexDistance/offsetDistance/edgeRadius、GenerateGeometry凸包算法(Graham扫描)、CollectChildTransforms递归收集子节点、CollectColliderPoints支持Box/Circle/Polygon/Edge/Capsule子碰撞器、TransformPoint2D坐标变换、GetPath数组/List版本、OverlapPoint点内检测、OverlapCollider碰撞体重叠、IsTouching/IsTouchingLayers、usedByComposite与TilemapCollider2D协同、ConvexHull ComputeConvexHull凸包计算 |
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
| `ContactFilter2D` | ✅ | 基本过滤 |

---

## 4. UnityEngine.UI（uGUI）

| 类型 | 状态 | 备注 |
|------|------|------|
| `Canvas` | ✅ | 官方 `UnityEngine` 命名空间/type/native metadata/全部公开成员反射差异 0；Overlay/Camera/World、rootCanvas、pixelRect、scaleFactor 根变换、sorting、default material；WillRenderCanvases→layout/graphic/clip→自动 native queue flush |
| `CanvasScaler` | ✅ | uiScaleMode(ConstantPixel/ScaleWithScreen/ConstantPhysical)、referenceResolution、screenMatchMode、matchWidthOrHeight、scaleFactor计算 |
| `CanvasRenderer` | 🟡 | 官方 `UnityEngine` sealed/type/全部公开成员反射差异 0；material/pop/mesh/texture/color-alpha/clipping/cull/Clear/三类 stream 经官方探针闭环。场景 Renderer 已按 Canvas/sibling 排序自动转换并 attach 持久 native queue；每个非空 triangle submesh 按原 material slot/texture 拆为稳定独立 command，packing/batching/snapshot/triple-buffer/indexed draw/fence/readback 已在 C++。Metal/Vulkan/D3D11 已实现完整 RGBA8 mip resource、UV0/main+alpha sampling、Apply/Destroy/native pointer；Metal、MoltenVK、SwiftShader 已用 minification readback 证明非零 mip 采样，D3D11 真实分支交叉编译通过。相关 **13 组/169 个测试**；仍缺 Windows WARP 12 个像素用例实跑、mip streaming/compressed、soft clip、mask/stencil、完整 URP variants、Vulkan window/HDR/resize 与 Unity 2022.3.61f1 截图 A/B |
| `Graphic` | ✅ | color/material/raycastTarget/dirty标记、OnEnable/OnDisable注册CanvasUpdateRegistry、Rebuild布局/图形、IsDestroyed |
| `MaskableGraphic` | ✅ | IClipper接口、stencil裁剪、ClipperRegistry |
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
| `Toggle` | ✅ | isOn切换、toggleTransition、group、IPointerClickHandler/ISubmitHandler、onValueChanged |
| `ToggleGroup` | ✅ | m_Toggles HashSet、AllowSwitchOff、NotifyToggleOn互斥 |
| `Slider` | ✅ | fillRect/handleRect、direction、minValue/maxValue/wholeNumbers、OnDrag更新value、Rebuild/UpdateVisuals、onValueChanged |
| `Scrollbar` | ✅ | value(0-1)/size/numberOfSteps、OnDrag/OnPointerDown、SetDirection、Rebuild/UpdateVisuals更新滑块位置、**SetValueWithoutNotify**、onValueChanged |
| `Dropdown` | ✅ | Show()创建下拉列表、Hide()销毁、AddOptions/ClearOptions/RefreshShownValue、模板实例化、onValueChanged |
| `InputField` | ✅ | text属性、caretBlinkRate/caretWidth/selectionColor、contentType验证（数字/邮箱/密码*等）、characterLimit、OnSelect/OnDeselect焦点 |
| `ScrollRect` | ✅ | **完整Unity 2022功能**：MovementType(Unrestricted/Elastic/Clamped)、Elastic弹性回弹SmoothDamp、惯性decelerationRate=0.135、ScrollbarVisibility(Permanent/AutoHide/AutoHideAndExpandViewport)、horizontal/verticalScrollbar联动size/value、嵌套滚动（到达边界事件传递父级）、滚轮scrollSensitivity、LateUpdate惯性+边界校正、ContentBounds/ViewBounds递归计算子节点、normalizedPosition、RubberDelta弹性拉伸、Rebuild/LayoutComplete/GraphicUpdateComplete、ICanvasElement/ILayoutElement/ILayoutGroup |
| `RectTransform` | 🟡 | 见 CoreModule；公开面、核心布局数学、DrivenRectTransformTracker 与布局 driver ownership/disable 清理已闭环，Canvas native dispatch/编辑器交互仍在推进 |
| `DrivenRectTransformTracker` / `DrivenTransformProperties` | ✅ | 两类型官方反射差异 0；25 个 flags、Add/Clear/obsolete Clear/undo 入口、null、struct copy 共享登记及多 Rect 释放经官方探针与 14 测试；LayoutGroup/ContentSizeFitter/AspectRatioFitter 已接入 ownership |
| `LayoutGroup` / `Horizontal/Vertical/Grid` | ✅ | Horizontal/VerticalLayoutGroup: CalculateLayoutInput遍历子ILayoutElement、SetLayoutHorizontal/Vertical设置anchoredPosition；GridLayoutGroup: cellSize/spacing/startCorner/constraint；tracker 驱动 anchors/position/size 并在重建/禁用时释放 |
| `LayoutElement` | ✅ | minWidth/preferredWidth/flexibleWidth等布局属性 |
| `ContentSizeFitter` / `AspectRatioFitter` | ✅ | ContentSizeFitter 两轴按 fit mode 登记/驱动 sizeDelta；AspectRatioFitter 按 Width/Height/Fit/Envelope 登记精确属性、stretch/fitted size；两者 disable 均 Clear ownership |
| `EventSystem` | ✅ | 事件分发、RaycastAll、current选中对象、sendPointerEvents/sendUpdateEvents、firstSelected |
| `BaseInputModule` / `StandaloneInputModule` | ✅ | 真正事件分发：Process处理鼠标按下/移动/释放/拖拽/滚动，ProcessTouchPress、HandlePointerExitAndEnter、事件发送到GameObject |
| `PointerEventData` | ✅ | pointerId/position/delta/button/clickCount/enterEvent/hovered/pointerDrag/pointerPressRaycast等完整字段 |
| `ExecuteEvents` | ✅ | Execute/ExecuteHierarchy、EventFunction&lt;T&gt;、所有事件接口handler（IPointerClick/Down/Up/Enter/Exit/Submit/BeginDrag/Drag/EndDrag/Scroll/Move等） |
| `GraphicRaycaster` | ✅ | Raycast用RectTransformUtility.RectangleContainsScreenPoint检测、blockingObjects、sortOrderPriority |
| `RectTransformUtility` | ✅ | 官方 `UnityEngine` 公开面差异 0；screen ray/world/local、contains+Vector4 inset、overlay/camera、pixel-perfect point/rect、recursive flip/bounds 经 Unity 2022.3 探针与 16 测试 |
| `CanvasGroup` | ✅ | 官方 `UnityEngine` sealed/type/NativeProperty/ICanvasRaycastFilter 公开面差异 0；defaults、blocks raycast、alpha inheritance 与 ignoreParentGroups 截断经官方探针 |
| `Outline` / `Shadow` / `PositionAsUV1` | ✅ | Shadow顶点偏移effectColor/distanceX/Y、Outline四方向轮廓、PositionAsUV1将位置写入UV1 |
| `IndexedSet&lt;T&gt;` | ✅ | O(1) Add/Remove/Clear，支持CanvasUpdateRegistry队列 |
| `BaseInput` | ✅ | mousePosition/mousePresent/touchCount/touchesSupported/GetTouch/IsMouseDown |

---

## 4.5 UnityEngine.UIElements

| 类型 | 状态 | 备注 |
|------|------|------|
| `VisualElement` | ✅ | name/classList/children/parent/layout/worldBound/contentRect、Add/Remove/Insert/Q/Query、AddClass/RemoveClass/EnableInClassList/ToggleInClassList、style/resolvedStyle、RegisterCallback/UnregisterCallback事件系统、GeometryChangedEvent/AttachToPanelEvent/DetachFromPanelEvent、SetEnabled/visible/pickingMode/opcacity/transform/tooltip |
| `ScrollView` | ✅ | **完整Unity 2022功能**：scrollOffset/horizontal/vertical/touchScrollBehavior/scrollDecelerationRate/elasticity/nestedInteractionKind/mouseWheelScrollSize/horizontalPageSize/verticalPageSize、ApplyElasticRubberBand弹性回弹SmoothDamp、惯性滚动ApplyInertia减速、ApplyScrollOffset更新content transform、ScrollTo滚动到子元素、OnScrollWheel滚轮响应、ComputeContentBounds/ComputeViewportBounds计算边界、ScrollTo(VisualElement)/ScrollTo(ChildOf)定位、horizontalScroller/verticalScroller Scroller联动、showHorizontal/showVertical滚动条显示 |
| `Scroller` | ✅ | value/size/lowValue/highValue/direction/slider、valueChanged事件 |
| `Button` | ✅ | 继承VisualElement、clicked事件、text属性 |
| `Label` | ✅ | 继承VisualElement、text属性 |
| `TextField/PasswordField/IntegerField/FloatField` | ✅ | value属性、ChangeEvent事件 |
| `Toggle/Slider/Scroller/DropdownField` | ✅ | value属性、ChangeEvent |
| `ListView/TreeView/TreeViewController` | ✅ | **完整Unity 2022功能**：ListView：itemsSource/makeItem/bindItem/unbindItem/destroyItem、selectedIndex/selectedItem/selectedIndices/selectedItems、SelectionType(None/Single/Multiple)、reorderable拖拽重排、showBorder/showAlternatingRowBackgrounds/showFoldoutHeader/showAddRemoveFooter、fixedItemHeight/resolvedItemHeight、horizontalScrollingEnabled、virtualizationMethod(FixedHeight/DynamicHeight)、AlternatingRowBackground(None/ContentOnly/All)、Rebuild虚拟滚动、OnScrollChanged滚动联动、键盘导航(上下箭头/Home/End/PageUp/PageDown)、Ctrl/Shift多选、onItemsChosen/onSelectionChange/onSelectedIndicesChanged/itemIndexChanged/itemsAdded/itemsRemoved/itemsSourceSizeChanged事件；TreeView：AddToTree/RemoveFromTree、BuildTree树形结构、SetExpanded/IsExpanded展开折叠、SetSelection/GetSelection/ScrollToItem、MakeTreeView/MakeNode数据驱动、Foldout箭头控件、depth缩进、键盘导航多选、EnsureVisible滚动定位 |
| `IMGUIContainer/IMGUIContainer` | ✅ | onGUIHandler回调、DrawIMGUI绘制 |
| `VisualTreeAsset/UxmlFactory/UxmlTraits` | ✅ | CloneTree、Load/Save、UxmlAttributeDescription |
| `StyleSheet/USS` | ✅ | StylePropertyDictionary样式存储、ParseUSSDocument、TryGetProperty/SetProperty/RemoveProperty |
| `PanelSettings` / `UIDocument` | ✅ | UIDocument(PanelSettings/rootVisualElement/visualTreeAsset) |
| `EventBase/PropagationPhase` | ✅ | 事件基类、BubbleUp/TrickleDown传播阶段、target/previousParent/eventTypeId |
| `PointerEventBase/MouseEventBase` | ✅ | position/deltaPosition/button/clickCount/modifiers、PointerDownEvent/MoveEvent/UpEvent/ClickEvent/ScrollWheelEvent |
| `FocusEvent/ChangeEvent/GeometryChangedEvent` | ✅ | 完整事件类型 |
| `StyleLength/StyleFloat/StyleColor/StyleInt/StyleEnum` | ✅ | 完整样式结构 |
| `FlexDirection/JustifyContent/Align/Overflow/Position/Display/Visibility` | ✅ | 完整枚举 |
| `IBindable/INotifyValueChanged<T>` | ✅ | 接口定义 |

---

## 5. UnityEngine.UIModule（Canvas/IMGUI 底层）

| 类型 | 状态 | 备注 |
|------|------|------|
| `Font` | ✅ | 基础字体属性 |
| `TextAnchor/TextAlignment/FontStyle` | ✅ | 枚举 |
| `CanvasRenderer` (底层) | 🟡 | C++ queue/sort/batching/原子快照、多 submesh/material slot、三槽 upload 与 Texture2D registry 完成；Metal/Vulkan/D3D11 完整 RGBA8 mip resource、UV/main+alpha sampling、indexed draw、blend、scissor、fence、offscreen readback已接通，Metal/Vulkan 非零 mip 已真实像素验证，D3D11 真实 Windows 分支交叉编译通过；相关 **13 组/169 个测试**。仍缺 Windows WARP 12 个像素用例实跑、mip streaming/compressed、soft clip、mask/stencil、完整 URP material/shader variants 与 Unity 2022.3.61f1 官方场景截图 A/B，故保持 🟡 |
| `GUI` / `GUIUtility` / `GUISkin` | ✅ | 即时模式控件命中与状态；见 Editor 节 IMGUI 交互 |

---

## 6. UnityEngine.Rendering + SRP

| 类型 | 状态 | 备注 |
|------|------|------|
| `Graphics` | ✅ | DrawMesh/DrawMeshInstanced、Blit、ClearRenderTarget、SetRenderTarget、static properties |
| `GraphicsSettings` | ✅ | renderPipelineAsset/currentRenderPipeline、renderPipelineChanged事件 |
| `QualitySettings` | ✅ | pixelLightCount/shadowDistance/cascades/vSync/antiAliasing/lodBias/anisotropicFiltering/streamingMipmaps、SetQualityLevel |
| `RenderPipelineAsset` | ✅ | CreatePipeline抽象 |
| `RenderPipeline` | ✅ | Render(ScriptableRenderContext,Camera[])虚方法、事件 |
| `RenderPipelineManager` | ✅ | currentPipeline、DoRenderLoop、begin/endFrameRendering/begin/endCameraRendering事件 |
| `ScriptableRenderContext` | ✅ | Cull/ExecuteCommandBuffer/ExecuteCommandBufferAsync/DrawRenderers(含stateBlock)/DrawSkybox/DrawShadows(DrawShadowsSettings)/DrawGizmos/DrawWireOverlay/SetRenderTarget/SetupCameraProperties/Submit、GizmoSubset枚举 |
| `CommandBuffer` | ✅ | DrawRenderer/DrawMesh/DrawMeshInstanced/DrawRenderers/DrawProcedural/DrawProceduralIndexed/DrawMeshInstancedProcedural、SetRenderTarget/ClearRenderTarget/Blit/SetGlobalFloat/Int/Vector/Color/Matrix/Texture/Buffer/SetViewMatrix/SetProjectionMatrix/SetViewProjectionMatrices/EnableScissorRect/DisableScissorRect/SetInvertCulling/SetShadowSamplingMode/GenerateMips/CopyTexture、SetViewport/Scissor、GetTemporaryRT/ReleaseTemporary |
| `CommandBufferPool` | ✅ | 命令缓冲池 |
| `CullingResults` | ✅ | visibleLights/visibleReflectionProbes、ComputeShadowMatrices |
| `DrawingSettings/FilteringSettings` | ✅ | DrawingSettings: ShaderTagId、sortingSettings、perObjectData、enableDynamicBatching/Instancing; FilteringSettings: renderQueueRange、layerMask、sortingLayerRange、defaultValue |
| `RenderTargetIdentifier` | ✅ | 渲染目标标识 |
| `ShaderPassName` | ✅ | ShaderTagId |
| `SortingCriteria/RenderQueueRange/SortingLayerRange` | ✅ | 枚举/结构 |
| `PerObjectData` | ✅ | 枚举 |
| `CameraEvent/LightEvent/ShadowMapPass` | ✅ | 枚举 |
| `Volume框架` | ✅ | VolumeManager单例、VolumeProfile、VolumeComponent、VolumeParameter&lt;T&gt;/Bool/Float/Color/Vector3等参数类型 |
| `Light` | ✅ | type/color/intensity/range/spotAngle/shadows/lightmappingBakeType/renderingLayerMask、AddCommandBuffer |
| `ReflectionProbe` | ✅ | mode(ReflectionProbeMode)/importance/intensity/resolution/clearFlags/backgroundColor/boxProjection/boxSize/center/blendDistance/refreshMode/timeSlicingMode/hdr/renderDynamicObjects/cullingMask/nearClip/farClip/shadowDistance/shadows/cubemap/realtimeTexture、RenderProbe/IsFinishedRendering/UpdateCachedRenderData、static defaultTexture/defaultTextureHDRDecodeValues、RefreshMode/TimeSlicingMode/Type/ClearFlags枚举、reflectionProbeChanged事件 |
| `RenderSettings` | ✅ | fog/fogColor/fogMode/fogDensity/fogStartDistance/fogEndDistance、ambientMode(AmbientMode)/ambientSkyColor/ambientEquatorColor/ambientGroundColor/ambientLight/ambientIntensity/ambientProbe、skybox/sunSource、defaultReflectionMode/defaultReflectionResolution/defaultReflectionCubemap、reflectionBounces/reflectionIntensity、haloStrength/flareStrength/flareFadeSpeed/subtractiveShadowColor |
| `ComputeShader` | ✅ | 完整API：FindKernel/HasKernel/Dispatch/DispatchIndirect、SetFloat/Int/Bool/Vector/Matrix/Texture/Buffer(带kernelIndex)、SetFloats/Ints/Vectors/Matrices、EnableKeyword/DisableKeyword/IsKeywordEnabled/SetKeyword、SetConstantBuffer |
| `ComputeBuffer` | ✅ | count/stride/SetData/GetData/SetCounterValue/Dispose |
| `AsyncGPUReadback/GraphicsFence/GraphicsBuffer` | ✅ | AsyncGPUReadback(Request)/AsyncGPUReadbackRequest(hasError/done/WaitForCompletion/GetData) |

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
| `Avatar` / `AvatarMask` | ✅ | **完整Unity 2022功能**：Avatar：isValid/isHuman/hasTransformHierarchy/humanScale/avatarSize/muscleCount/rootBone/bodyPosition/bodyRotation、BoneNameToHumanBoneName映射、AvatarBuilder.BuildHumanAvatar从GameObject构建Humanoid Avatar、RawAvatar/RawAvatar构建输入、HumanBone/SkeletonBone/HumanLimit/HumanDescription完整结构（upperArmTwist/lowerArmTwist/armStretch/legStretch/feetSpacing等所有IK/肌肉参数）；AvatarMask：humanMachineCount/skeletonCount/transformCount、Get/SetHumanoidBodyPartActive按HumanBodyBones开关、Get/SetTransformMask/TransformActive/TransformPath、Add/RemoveTransformPath、TransformMaskElement路径掩码、GetTransformMaskElements导出、GetTransformPathFromTransform递归路径构建 |
| `HumanBodyBones` | ✅ | 枚举 |
| `Animation`（Legacy） | ✅ | 继承Behaviour、clip/wrapMode/playAutomatically、Play/CrossFade/Stop/Rewind/Sample/IsPlaying、AnimationState time/speed/weight |
| `AnimationState` | ✅ | name/clip/weight/speed/wrapMode/time/normalizedTime/layer/blendMode/enabled、AddMixingTransform |

---

## 8. UnityEngine.AudioModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `AudioClip` | ✅ | length/samples/channels/frequency/loadType/loadInBackground/preloadAudioData/ambisonic、GetData/SetData、Create(name,length,channels,freq,stream,3D,loadType)、LoadAudioData/UnloadAudioData、AudioClipLoadType枚举 |
| `AudioSource` | ✅ | clip/volume/pitch/panStereo/spatialBlend/outputAudioMixerGroup/mute/bypass/panLevel/reverbZoneMix/dopplerLevel/spread/rolloffMode/minDistance/maxDistance/playOnAwake/loop/priority/time/timeSamples/isPlaying/isVirtual、Play/PlayDelayed/PlayOneShot/PlayClipAtPoint/Stop/Pause/UnPause |
| `AudioListener` | ✅ | pause/volume静态属性、velocityUpdateMode、position/forward/up静态、worldToLocalMatrix/localToWorldMatrix、GetOutputData/GetSpectrumData、FFTWindow枚举 |
| `AudioMixer` | ✅ | name/outputAudioMixer、FindMatchingGroups/FindSnapshot、SetFloat/GetFloat Dictionary存储参数、TransitionToSnapshots权重插值过渡 |
| `AudioMixerGroup` | ✅ | name/audioMixer、audioMixerGroupViews |
| `AudioMixerSnapshot` | ✅ | name/audioMixer、TransitionTo平滑过渡 |
| `AudioMixerController` | ✅ | m_Parameters/m_Snapshots/m_Groups/m_TargetSnapshotWeights、Update插值计算最终参数 |
| `AudioLowPassFilter/AudioHighPassFilter/AudioReverbFilter/AudioEchoFilter/AudioDistortionFilter/AudioChorusFilter` | ✅ | 6种音频过滤器组件 |

---

## 9. UnityEngine.AssetBundleModule / UnityWebRequestModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `AssetBundle` | ✅ | **全链路**：UnityFS catalog 写盘/读回、BuildAssetBundles(DryRun/AppendHash/Strict/变体/ChunkBasedCompression)、LoadFromFile/Memory/Stream+CRC、LoadAsset/All/SubAssets、Unload/Async、Manifest 依赖；**真 LZ4 block** 压缩往返；magic=`UnityFS ` 官方对齐 |
| `AssetBundleBinaryComparer` | ✅ | UnityFS 门禁、ALZ4 解压校验、catalog Gate；AB.Compare.Tests **22**；CI 必跑 |
| `Lz4Codec` | ✅ | 纯 C# LZ4 block Encode/Decode（非 Deflate 伪装） |
| `AssetBundleCompression` | ✅ | ALZ4 + codec(LZ4/Deflate)；legacy Deflate 兼容；MaybeCompress/DecompressIfNeeded |
| `AssetBundleRequest` / `AssetBundleCreateRequest` / `AssetBundleUnloadOperation` | ✅ | 三类型公开反射差异 0；AssetBundleRequest 正确继承 ResourceRequest，asset/allAssets 与 create assetBundle 阻塞完成、allAssets 独立数组快照，Unload WaitForCompletion、自动完成/协程/回调相位、missing asset、File/Memory/Stream 延迟执行经真实 Unity A/B 与 15/15 深测 |
| `UnityWebRequest` | ✅ | **HttpClient**；Cookie 容器；CertificateHandler TLS 回调；timeout/redirectLimit；file://；WaitForCompletion；Abort；测试≥23 |
| `UnityWebRequestAsyncOperation` | ✅ | 继承AsyncOperation、webRequest属性、SetDone |
| `DownloadHandler（基类）` | ✅ | data(byte[])/text(UTF8 去 BOM)、ReceiveData/ReceiveContentLength/CompleteContent |
| `DownloadHandlerBuffer` | ✅ | 继承DownloadHandler、MemoryStream存储 |
| `DownloadHandlerFile` | ✅ | 写入文件路径 |
| `DownloadHandlerTexture` | ✅ | 下载后转换为Texture2D |
| `DownloadHandlerAssetBundle` | ✅ | 下载后 LoadFromMemory 加载 AssetBundle；data 缓存 |
| `DownloadHandlerAudioClip` | ✅ | 下载后加载AudioClip |
| `UploadHandler（基类）` | ✅ | data/contentType |
| `UploadHandlerRaw` | ✅ | 接受byte[] |
| `UploadHandlerFile` | ✅ | 从文件读取 |
| `WWW` | ✅ | 内部包装UnityWebRequest、url/text/bytes/error/isDone/progress/texture/audioClip/assetBundle |

---

## 10. UnityEngine.ImageConversionModule / Texture

| 类型 | 状态 | 备注 |
|------|------|------|
| `Texture` | ✅ | width/height/format/filterMode/wrapMode |
| `Texture2D` | ✅ | Color[]/Color32[]像素、GetPixel/SetPixel/GetPixelBilinear(双线性过滤)/GetPixels/SetPixels/SetPixels32/GetPixels32/Apply/ReadPixels/LoadImage/EncodeToPNG/EncodeToJPG/Resize/PackTextures/GetRawTextureData<T>/LoadRawTextureData、static whiteTexture/blackTexture/redTexture/normalTexture、TextureFormat枚举完整(50+格式含BC/DXT/ETC/ASTC/PVRTC)、mipmapCount、linear |
| `RenderTexture` | ✅ | descriptor/width/height/format/depth/volumeDepth/antiAliasing/useMipMap/colorBuffer/depthBuffer/doubleBuffered/dimension/enableRandomWrite/Create/Release/IsCreated/static active/GetTemporary/ReleaseTemporary、RenderTextureDescriptor完整结构、CustomRenderTexture(updateMode/doubleBuffered/Initialize/Update)、RenderBuffer/RenderTargetSetup |
| `Cubemap` | ✅ | GetPixel/SetPixel |
| `TextureFormat` / `RenderTextureFormat` | ✅ | 枚举 |
| `ImageConversion` | ✅ | EncodeToPNG/JPG/TGA、LoadImage 扩展 |

---

## 11. UnityEngine.MeshModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Mesh` | ✅ | vertices/normals/tangents/uv/uv2-uv8(8 UV通道)/colors/colors32/triangles/bindposes/boneWeights/IndexFormat/subMeshCount/indexCount/vertexCount/bounds、SetVertices/SetTriangles/SetIndices/SetNormals/SetTangents/SetUVs/SetColors/SetBindposes/SetBoneWeights(含List版本)、GetTriangles/GetIndices/GetVertices/GetNormals/GetTangents/GetUVs/GetColors/GetBindposes/GetBoneWeights、MarkDynamic/UploadMeshData/Optimize/OptimizeIndexBuffers/OptimizeReorderVertexBuffer、Clear(keepVertexLayout)/RecalculateBounds/RecalculateNormals/RecalculateTangents、BoneWeight(weight0-3,boneIndex0-3)、MeshTopology |
| `BoneWeight` | ✅ | 骨骼权重结构 |
| `MeshTopology` | ✅ | 枚举(Triangles/Quads/Lines/LineStrip/Points) |
| `MeshData/MeshDataArray` | ✅ | **完整Unity 2022功能**：MeshData：IndexFormat/vertexCount/indexCount/subMeshCount/vertexBufferCount、SetVertexBufferParams/SetIndexBufferParams、GetVertexAttributes/SetVertexAttributes、VertexAttributeDescriptor(attribute/format/dimension/stream)、VertexAttribute枚举(Position/Normal/Tangent/Color/TexCoord0-7/BlendWeight/BlendIndices)、VertexAttributeFormat完整枚举(Float32/Float16/UNorm8/SNorm8等)、GetVertexData/GetIndexData泛型、GetVertices/GetNormals/GetTangents/GetColors/GetUVs/GetIndices返回NativeArray、SetSubMesh/GetSubMesh/AddSubMesh/SetSubMeshes、SubmeshDescriptor(indexStart/indexCount/firstVertex/vertexCount/bounds/topology)、MeshUpdateFlags(DontValidateIndices/DontResetBoneBounds等)；MeshDataArray：length/indexer/Allocate/Dispose；Mesh扩展：SetVertexBufferParams/SetIndexBufferParams、GetVertexData/GetIndexData、GetVertexAttributes/GetVertexAttributeDimension/GetVertexAttributeFormat/HasVertexAttribute、AllocateWritableMeshData/ApplyAndDisposeWritableMeshData（多Mesh版本+单Mesh版本）、AcquireReadOnlyMeshData、SetVertexBufferData/SetIndexBufferData泛型、RecalculateUVDistribution、MeshUtility.Optimize/CreateMeshFromVertices |

---

## 12. UnityEngine.Renderer / Material

| 类型 | 状态 | 备注 |
|------|------|------|
| `Renderer` | ✅ | material/Materials懒创建、bounds、sortingLayerID/Order、shadowCastingMode/receiveShadows、lightProbeUsage、SetPropertyBlock/GetPropertyBlock |
| `MaterialPropertyBlock` | ✅ | Dictionary存储SetFloat/Int/Vector/Color/Matrix/Texture/Buffer/ColorArray/VectorArray/FloatArray/MatrixArray、Get*全部系列、SetTexture/SetBuffer、Clear |
| `MeshRenderer` | ✅ | 继承Renderer |
| `SkinnedMeshRenderer` | ✅ | sharedMesh/bones/rootBone/quality |
| `SpriteRenderer` | ✅ | 继承Renderer |
| `TrailRenderer` / `LineRenderer` | ✅ | TrailRenderer时间老化点记录、LineRenderer SetPosition |
| `ParticleSystemRenderer` | ✅ | 继承Renderer |
| `Material` | ✅ | **完整Unity 2022 API**：shader/color/renderQueue/shaderKeywords、Dictionary属性SetFloat/GetFloat/SetColor/GetColor/SetInt/GetInt/SetVector/GetVector/SetMatrix/GetMatrix/SetTexture/GetTexture/SetBuffer/GetBuffer/SetFloatArray/SetColorArray/SetVectorArray/SetMatrixArray、EnableKeyword/DisableKeyword/IsKeywordEnabled/SetKeyword/CopyPropertiesFromMaterial/HasProperty/HasFloat/HasColor/HasInt/HasVector/HasMatrix/HasTexture/HasBuffer、GetPropertyName/GetPropertyCount/FindPass、SetRenderingMode(Opaque/AlphaBlend/AlphaTest/Additive)、Lerp插值、parent/DisableKeyword、GetPassName/passCount、shaderKeywords完整List管理、GetTag |
| `Shader` | ✅ | **完整ShaderLab/HLSL系统（Unity 2022对齐）**：name/renderQueue/passes/subShaders/properties/tags/keywords/constantBuffers/fallback/customEditor完整解析、ParseShaderSource解析ShaderLab语法、ParseShaderProperties(_Color/_MainTex/_Glossiness/Range等类型+默认值+flags)、ParseShaderTags(RenderPipeline/Queue/RenderType/DisableBatching/ForceNoMirroredLighting等)、ParseSubShaders/ParsePasses(Blend/BlendOp/ZWrite/ZTest/Cull/ColorMask/Offset/Stencil完整渲染状态解析)、ParseShaderKeywords(multi_compile/shader_feature multi_compile_instancing multi_compile_fog multi_compile_light)、ParseConstantBuffers(CBUFFER_START/UnityPerMaterial/UnityPerDraw SRP Batcher兼容检测)、GetPropertyName/FindPropertyIndex/PropertyToID/PropertyToName、SetGlobalFloat/Int/Vector/Color/Matrix/Texture/Buffer/全局属性管理、globalMaximumLOD/globalRenderPipeline/WarmupAllShaders、isInstancingSupported检测multi_compile_instancing/UNITY_INSTANCING_BUFFER/UNITY_VERTEX_INPUT_INSTANCE_ID、GPU Instancing完整支持(UNITY_ACCESS_INSTANCED_PROP/instanceID/SV_InstanceID)、ShaderPropertyFlags(Normal/Texture/HDR/PerRendererData/MainTexture/MainColor/NoScaleOffset)、ShaderPropertyType(Float/Int/Vector/Color/Texture/Matrix/Range)、GetPropertyCount/GetPropertyType/SetPropertyFlags/IsKeywordEnabled/EnableKeyword/DisableKeyword、Keywords系统(GlobalKeyword/LocalKeyword/ShaderKeywordSet/KeywordState)、ShaderVariantCollection(Add/Remove/Contains/WarmUp/ShaderVariant变体管理+WarmUp预编译)、Pass/SubShader结构(BlendState/DepthState/RasterState/StencilState完整渲染状态)、BlendMode/BlendOp/CullMode/CompareFunction/StencilOp/ColorWriteMask/BlendEquation完整枚举、HLSL编译框架(ShaderCompilerPlatform:D3D/Metal/Vulkan/OpenGLCore/GLES2/GLES3/WebGL等全平台、CompileShader/CompileShaderFromSource/SetIncludeHandler/ClearCachedData/Preprocess/ParseHLSL)、ShaderUtil类(GetPropertyCount/GetPropertyType/GetRangeLimits/GetShaderKeywords等)、Shader.dependency/HasPass/FindPassTag/GetDependency、SRP Batcher兼容性检测isSRPBatcherCompatible、变体收集WarmUp/CollectVariants |
| `ShaderVariantCollection` | ✅ | Add/Remove/RemoveVariant/Contains/WarmUp/WarmUpProgress/ShaderVariant(shader/passName/keywords[])结构、变体warmup模拟+isWarmedUp状态、ShaderVariantCollectionHelper枚举Shader/passes/keywords组合 |
| `BlendState/DepthState/RasterState/StencilState` | ✅ | 完整渲染状态结构体，BlendMode/BlendOp/CullMode/CompareFunction/StencilOp/ColorWriteMask枚举，Opaque/AlphaBlend/Additive/Modulate预定义状态 |
| `ComputeShader/ComputeBuffer/GraphicsBuffer` | ✅ | ComputeBuffer(count/stride/SetData/GetData/SetCounterValue/Dispose/SetData(Array)/GetData(Array)/count/stride)、GraphicsBuffer(target/stride/count)、AsyncGPUReadback(Request)、GraphicsFence |
| `ReflectionProbe` | ✅ | type/mode/importance/intensity/boxProjection/clearFlags/backgroundColor |
| `LightProbeGroup/LightProbes/SphericalHarmonicsL2` | ✅ | LightProbeGroup(positions)/LightProbes(InterpolateProbe)、SphericalHarmonicsL2完整SH系数 |
| `LODGroup/LOD/OcclusionArea/OcclusionPortal` | ✅ | LODGroup(LODs/fadeMode/SetLODs/RecalculateBounds/ForceLOD)、LOD(screenRelativeTransitionHeight/renderers)、LODFadeMode、OcclusionArea/Portal |
| `OcclusionCulling` / Umbra | ✅ | Bake 网格 PVS、IsVisible 查询、Portal 关闭遮挡、RegisterArea/Portal、queryCount；**StaticOcclusionCulling.Compute/Cancel/Clear**；StaticBatchingUtility 标记 static+网格合并；测试≥16 |
| `StreamingAssets` | ✅ | root/GetPath/Exists/ReadWrite 文本与字节、GetFiles/GetDirectories、GetFileUrl(file://)、CopyFrom、测试隔离 SetRootForTests；对齐 Application.streamingAssetsPath；测试≥14 |
| `Wind` / `WindZone` | ✅ | Directional/Spherical、pulse/turbulence、OnEnable 注册/GetWindAt 合成；测试≥7 |

---

## 13. UnityEngine.CameraModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Camera` | ✅ | Perspective/Ortho投影矩阵真实计算、worldToCameraMatrix(LookAt)、6个坐标转换方法（VP矩阵→NDC→屏幕/世界）、fieldOfView/nearClipPlane/farClipPlane/orthographic/orthographicSize、Render/RenderToCubemap/RenderWithShader、onPreCull/onPreRender/onPostRender事件、main静态属性、targetTexture、allCameras |
| `CameraType` / `CameraClearFlags` / `RenderingPath` | ✅ | 枚举 |
| `SceneViewCamera` 等 | ✅ | SceneView 专用相机：SyncFromSceneView/Render/RenderToTexture、Camera.Render→SRP、LightProbes 采样、Gizmos/Grid pass、坐标转换 |

---

## 14. UnityEngine.LightModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Light` | ✅ | 见Rendering |
| `LightType/LightShadows/LightmapBakeType` | ✅ | 枚举 |
| `LightmapData` | ✅ | lightmapColor/lightmapDir/lightmapShadowMask/shadowMask |
| `Lightmapping` | ✅ | Bake()/BakeAsync()带事件触发、isBaking/bakeProgress、bakedGI/realtimeGI、ClearBakedData、GetLightmapSettings/SetLightmapSettings、lightmaps数组、lightmapCount、lightmapResolution/Padding/MaxSize、mixedLightingMode、finalGather |
| `MixedLightingMode/LightmapsMode` | ✅ | 枚举 |
| `LightmapSettings/LightmapParameters` | ✅ | **完整光照贴图设置**：LightmapSettings：lightmaps/lightmapsMode/lightProbes/lightmapParameters/bakedColorSpace/quality/skybox；LightmapParameters：resolution/irradianceQuality/backFaceTolerance/padding/quality/blurRadius/directLightQuality/antiAliasingSamples/AOQuality/AOAntiAliasingSamples/bounceBoost/bounceIntensity/ambientLight/ambientSkyColor/ambientEquatorColor/ambientGroundColor/ambientIntensity/ambientMode/skyboxIntensity/skyboxMaterial/skyboxCubemap/reflectionIntensity/reflectionBounces/haloStrength/flareStrength/flareFadeSpeed |
| `AmbientMode` / `FogMode` | ✅ | 枚举 |

---

## 15. UnityEngine.ParticleSystemModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `ParticleSystem` | ✅ | 所有嵌套模块struct(MainModule/EmissionModule/ShapeModule/VelocityOverLifetime/ColorOverLifetime/SizeOverLifetime/RotationOverLifetime/ForceOverLifetime/ColorBySpeed/SizeBySpeed/RotationBySpeed/InheritVelocity/LimitVelocityOverLifetime/Trigger/Trails/Lights/Collision/Noise/TextureSheetAnimation/SubEmitters)、ParticleSystem.MinMaxCurve(mode+constant/curve/curveMin/curveMax)、ParticleSystem.MinMaxGradient(mode+color/gradient等)、Particle结构体(position/velocity/lifetime/size/color/randomSeed/GetCurrentSize/GetCurrentColor)、Burst结构体、ShapeType枚举、Emit(count/Particle[]/position+vel+size+lifetime+color)、Play/Pause/Stop/Clear/Simulate/IsAlive/GetParticles/SetParticles |
| `ParticleSystemRenderer` | ✅ | 见Renderer |
| `各模块（Emission/Shape/Velocity...）` | ✅ | 完整模块实现 |
| `相关枚举/结构` | ✅ | 完整定义 |

---

## 16. UnityEngine.TerrainModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Terrain` | ✅ | heightmapWidth/Height相关 |
| `TerrainData` | ✅ | SetHeights/GetHeight/GetInterpolatedHeight、size/heightmapScale/alphamaps/terrainLayers |
| `TerrainCollider` | ✅ | 基础碰撞器 |
| `Tree/Detail` 相关 | ✅ | **完整Terrain植被系统**：TreePrototype(prefab/bendFactor/navMeshColor)、TreeInstance(position/widthScale/heightScale/rotation/color/lightmapColor/prototypeIndex)、DetailPrototype(prototype/prototypeTexture/minWidth/maxWidth/minHeight/maxHeight/dryColor/healthyColor/renderMode/usePrototypeMesh/noiseSpread/bendFactor/DetailRenderMode)、TerrainLayer(diffuseTexture/normalMapTexture/maskMapTexture/tileSize/tileOffset/specular/metallic/smoothness/normalScale/diffuseRemap/maskMapRemap)、TerrainRenderFlags(Heightmap/Trees/Details/All)、TerrainData.treePrototypes/treeInstances/detailPrototypes/terrainLayers、SetTreeInstances/AddTreeInstance/GetTreeInstances/RefreshPrototypes |

---

## 17. UnityEngine.TilemapModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Tilemap` | ✅ | Dictionary&lt;Vector3Int,TileBase&gt; tiles、SetTile/GetTile/SetTiles/HasTile/RefreshTile/FloodFill/BoxFill/InsertCells/DeleteCells、cellBounds |
| `Tile` / `TileBase` / `TilemapCollider2D` | ✅ | TileBase/Tile/TileData/TileFlags 完整实现 |

---

## 18. UnityEngine.VideoModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `VideoPlayer` | ✅ | canPlay/canStep/canSetTime/aspectRatio/timeReference、Step/GetTargetAudioSource/SetTargetAudioSource/EnableAudioTrack/DisableAudioTrack/controlledAudioTrackCount、播放状态机、Play/Pause/Rewind/Prepare/Stop、url/clip/targetTexture/renderMode/audioOutputMode、frame/time/length/playbackSpeed/isPlaying/isPaused、prepareCompleted/loopPointReached/frameReady/errorReceived/started/seekCompleted/started事件 |
| `VideoClip` | ✅ | name/frameCount/frameRate/length/width/height/pixelAspectRatio/originalPath/audioTrackCount、GetAudioChannelCount/GetAudioSampleRate |
| `MediaFormatUtility` | ✅ | mp3/wav/ogg/aac/m4a/flac 音频 + mp4/webm/mov/avi 视频；DetectFromPath/Bytes、TryDecodeWav/PCM soft decode |
| WebGL 侧 `WebGLVideo` | ✅ | **完整WebGL视频API**：url/playing/paused/isPlaying/isPaused/isPrepared、time/duration/length/volume/loop/playbackSpeed、prepareCompleted/started/loopPointReached/errorReceived事件、Prepare()/Play()/Pause()/UnPause()/Stop()播放控制、UpdateTime时间推进与循环处理、WebGLApplication单例视频管理器（PlayVideo/PauseVideo/StopVideo/PauseCurrentVideo/StopCurrentVideo/GetVideo/GetCurrentVideo/UpdateVideos）、SupportedFormats mp4/webm/ogg |

---

## 19. UnityEngine.AIModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `NavMesh` | ✅ | CalculatePath/Raycast/Linecast、SamplePosition、FindClosestEdge、AllAreas=-1、GetAreaFromName/SetAreaCost/GetAreaCost(string)/AddNavMeshData/RemoveNavMeshData/RemoveAllNavMeshData、NavMeshTriangulation(vertices/indices/areas) |
| `NavMeshPath` | ✅ | corners数组、status(PathComplete/PathPartial/PathInvalid)、ClearCorners |
| `NavMeshHit` | ✅ | position/normal/distance/mask/hit/area |
| `NavMeshAgent` | ✅ | destination/speed/acceleration/velocity/remainingDistance/nextPosition/nextOrientation/stoppingDistance/angularSpeed/radius/height/autoBrakes/autoRepath/isStopped/isOnNavMesh/isPathStale/pathPending/steeringTarget/desiredVelocity/updatePosition/updateRotation/updateUpAxis/areaMask/path、pathPending/isStopped/warp/Move、SetDestination/ResetPath/CalculatePath/Resume/Stop/ActivateCurrentOffMeshLink、完整路径跟随 |
| `NavMeshObstacle` | ✅ | shape(NavMeshObstacleShape: Capsule/Box/None)、center/radius/height/size/velocity/carving/carveOnlyStationary |
| `OffMeshLink` | ✅ | startTransform/endTransform/activated/costOverride/biDirectional/occupied/area/navMeshLayer |

---

## 20. UnityEngine.TextRendering / TextMeshPro

| 类型 | 状态 | 备注 |
|------|------|------|
| `Font` | ✅ | 见 UIModule |
| `TextMeshPro` / `TMP_Text` / `FontAsset` | ✅ | TMP_Text继承Graphic、text/fontSize/alignment/wordWrapping、preferredWidth/Height计算 |

---

## 21. UnityEngine.Events / UnityEvent

| 类型 | 状态 | 备注 |
|------|------|------|
| `UnityEvent` / `UnityEvent&lt;T&gt;` | ✅ | UnityEvent AddListener/RemoveListener/Invoke、List&lt;UnityAction&gt;、UnityEvent&lt;T0-T3&gt;泛型 |

---

## 22. UnityEngine.SceneManagementModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Scene` | ✅ | 官方 `[Serializable] struct` 公开反射差异 0；m_Handle 状态注册、value-copy 共享状态、buildIndex/name/path/isLoaded/isDirty/rootCount/valid、根对象快照与跨 Scene 迁移已测 |
| `SceneManager` | 🟡 | 官方公开反射差异 0；全部 overload/事件/legacy unload、active Scene、root registry、跨 Scene 父子传播、MoveGameObject(s)、MergeScenes 已通过官方 A/B；真实异步资源加载、平台卸载及编辑器多 Scene 生命周期仍未闭环 |
| `LoadSceneMode` | ✅ | 枚举 |
| `CreateSceneParameters` / `LoadSceneParameters` / `LocalPhysicsMode` | ✅ | 类型、构造器、字段/属性、Serializable/Flags metadata 与官方反射差异 0；local physics mode 状态随 Scene handle 保存 |

---

## 23. UnityEngine.Jobs / Unity.Collections / Unity.Burst

| 类型 | 状态 | 备注 |
|------|------|------|
| `IJob/IJobParallelFor/IJobParallelForTransform` | ✅ | Execute()、Execute(int index)、Execute(int index,TransformAccess)接口 |
| `JobHandle` | ✅ | Complete()立即执行_execute、IsCompleted、CombineDependencies、ScheduleBatchedJobs |
| `IJobExtensions/IJobParallelForExtensions` | ✅ | Schedule&lt;T&gt;/Run&lt;T&gt;扩展方法，立即同步执行 |
| `TransformAccess` | ✅ | position/rotation/localPosition/localRotation/localScale/localToWorldMatrix/worldToLocalMatrix |
| `TransformAccessArray` | ✅ | length/Add/SetTransforms/Dispose |
| `NativeArray&lt;T&gt;` / `NativeSlice&lt;T&gt;` | ✅ | T[]内部数组、Length/this[]/ToArray/CopyFrom/CopyTo/Dispose/AsSpan/Slice、Allocator(Invalid/Temp/TempJob/Persistent) |
| `NativeList&lt;T&gt;` | ✅ | Capacity/Count/Add/AddRange/Insert/RemoveAt/RemoveAtSwapBack/Clear/Resize/Dispose、内部T[]扩容 |
| `NativeHashMap&lt;TKey,TValue&gt;` | ✅ | Capacity/Count/Add/Remove/TryGetValue/ContainsKey/Clear/Dispose、内部Dictionary |
| `NativeMultiHashMap&lt;TKey,TValue&gt;` | ✅ | Add(允许多值)、Remove/ContainsKey/TryGetFirstValue/TryGetNextValue |
| `NativeQueue&lt;T&gt;` | ✅ | Enqueue/Dequeue/Peek/Clear/Dispose/ToArray |
| `Burst` / `BurstCompile` | ✅ | BurstCompile/ReadOnly/WriteOnly/DeallocateOnJobCompletion/NativeDisableContainerSafetyRestriction/NativeSetThreadIndex等Attribute类 |

---

## 24. UnityEngine.Profiling

| 类型 | 状态 | 备注 |
|------|------|------|
| `Profiler` | ✅ | BeginSample/EndSample、logFile/usedHeapSizeLong、内部栈记录 |
| `ProfilerUnsafeUtility` | ✅ | 完整低层级Profiler API、与Unity.Profiling.Profiler集成、BeginSample/EndSample/CreateMarker/BeginThreadProfiling/EndThreadProfiling、ProfilerRecorder/ProfilerMarker/ProfilerArea/ProfilerCategory完整枚举 |
| `RuntimeInitializeOnLoadMethodAttribute` | ✅ | Attribute类、RuntimeInitializeLoadType枚举 |

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
| `EditorApplication` | ✅ | isPlaying/isPaused/isCompiling/playModeStateChanged/update/delayCall/EnterPlaymode/ExitPlaymode/CallDelayed/ExecuteMenuItem |
| `PlayModeStateChange` | ✅ | EnteredEditMode/ExitingEditMode/EnteredPlayMode/ExitingPlayMode枚举 |
| `EditorWindow` | ✅ | titleContent/position/Show/Popup/ShowUtility/ShowModalUtility/Focus/Close/Repaint/GetWindow/HasOpenInstances、OnGUI等虚方法、static List&lt;EditorWindow&gt; |
| `EditorGUI` | ✅ | **完整Unity 2022控件**：BeginChangeCheck/EndChangeCheck、Toggle/ToggleLeft/IntField/FloatField/DoubleField/LongField/TextField/PasswordField/TextArea/Delayed*/ColorField/CurveField/GradientField/ObjectField/BoundsField/BoundsIntField/RectField/RectIntField/Vector2Field/Vector2IntField/Vector3Field/Vector3IntField/Vector4Field/EnumPopup/EnumFlagsField/LayerField/LayerMaskField/TagField/MaskField/IntSlider/FloatSlider/Slider、LabelField/Space/HelpBox/ProgressBar/DropdownButton/Foldout/BeginFoldoutHeaderGroup、Begin/EndHorizontal/Vertical/ScrollView/DisabledGroup、Indent/Unindent、DrawTexture/DrawRect/DrawPreviewTexture、PrefixLabel、MinMaxSlider、BoundsField、fieldWidth/labelWidth/indentLevel属性，所有控件带Rect(手动布局)和自动布局两套API签名 |
| `EditorGUILayout` | ✅ | **完整自动布局版本**：所有EditorGUI方法的自动布局版本（无需Rect参数），Space/FlexibleSpace/Separator、BeginVertical/BeginHorizontal/ScrollView Toggle/TextField/ColorField/EnumPopup/ObjectField/Popup/IntPopup/MaskField/Vector2/3/4Field/IntSlider/FloatSlider/Slider/Delayed*、HelpBox/ProgressBar/Foldout、PropertyField |
| `EditorStyles` | ✅ | **完整Unity 2022样式**：toolbar/label/boldLabel/boldTextField/miniLabel/miniBoldLabel/largeBoldLabel/centeredGreyMiniLabel/highlightLabel/selectedLabel/redLabel/yellowLabel/whiteLabel/whiteBoldLabel/whiteMiniLabel/radiobutton/textField/button/toolbarButton/toolbarDropDown/toolbarSearchField/toolbarSearchFieldCancelButton/foldout/header/inspectorTitlebar/inspectorDefaultMargins、所有常用GUIStyle静态属性 |
| `EditorGUIUtility` | ✅ | FindTexture/Load/Load&lt;T&gt;/whiteTexture/scriptIcon/standardScriptIcon/CurrentViewWidth/currentViewWidth/editingTextField/SystemCopyBuffer/systemCopyBuffer/ObjectContent/IconSize/PointsToPixels/PixelsToPoints/pixelsPerPoint |
| `GUIStyle/GUIStyleState/GUISkin/GUIContent/GUILayoutOption/GUILayoutUtility` | ✅ | GUIContent(text/image/tooltip)、GUIStyleState(textColor/background)、GUIStyle(normal/hover/active/padding/margin/border/font/alignment等)、GUISkin(box/button/label等styles)、GUILayoutOption(Width/Height/MinWidth/MinHeight/MaxWidth/MaxHeight/ExpandWidth/ExpandHeight) |
| `Handles` | ✅ | PositionHandle/RotationHandle/ScaleHandle、DrawLine/DrawWireCube/DrawWireDisc/DrawDisc/DrawArc/DrawPolyLine/DrawBezier、CapFunction(Cube/Sphere/Cylinder/Cone/Arrow/Dot/RectangleHandleCap)、CurrentCamera、color、Matrix4x4矩阵、DrawSolidRectangleWithOutline |
| `SceneView` | ✅ | **完整Unity 2022 Pro编辑器对齐**：lastActiveSceneView/camera(带CameraType.SceneView)/orthographic/orthographicSize/pivot/rotation/in2DMode、FrameSelected/Frame(Bounds/Transform[])/LookAt/RepaintAll、**完整OnGUI工具栏**（DrawCameraMode下拉Shaded/Wireframe/ShadedWireframe等、2D/3D切换、Lit/Audio/Gizmos开关、视角快速切换Top/Bottom/Front/Back/Left/Right/Persp/Iso按钮、缩放控制-/+）、SceneViewState(showFog/showSkybox/showImageEffects/showParticleSystems/showFlares)、renderMode/drawGizmos/showSelectionOutline/audioPlay、DistanceToCamera/ViewToScreenPoint/ScreenToViewPoint/WorldToScreenPoint/ScreenToWorldPoint、SetupViewRotation/AlignViewToObject/AlignWithView/MoveToView/ResetCameraOrientation、onSceneGUI/beforeSceneGui/duringSceneGui/lastActiveSceneViewChanged事件回调、DrawCreate/GetAllSceneCamerasProjection/SetSceneViewShaderReplace |
| `Selection` | ✅ | activeGameObject/activeTransform/activeObject/objects/gameObjects/transforms/SelectionChanged、GetFiltered&lt;T&gt;按SelectionMode过滤、SelectionMode枚举(Unfiltered/TopLevel/Deep/Assets/Editable) |
| `AssetDatabase` | ✅ | Dictionary&lt;string,Object&gt;、LoadAssetAtPath&lt;T&gt;/AssetPathToGUID/GUIDToAssetPath/Contains/Refresh/CreateAsset/DeleteAsset/MoveAsset/CopyAsset/GetAllAssetPaths/FindAssets |
| `AssetImporter` / `AssetPostprocessor` | ✅ | AssetImporter(assetPath/importSettingsMissing/SaveAndReimport)、TextureImporter(textureType/filterMode/compressionQuality)、ModelImporter(animationType/importMaterials)、AssetPostprocessor虚方法(OnPreprocess/Postprocess Texture/Model/Audio) |
| `PrefabUtility` | ✅ | InstantiatePrefab/IsPrefabAsset/IsPartOfPrefabInstance/IsAnyPrefabInstanceRoot/GetCorrespondingObjectFromSource/ApplyPrefabInstance/RevertPrefabInstance |
| `PrefabStage` / Prefab Mode | ✅ | Isolation/Context、OpenPrefab/Close/Save/MarkDirty、stage 栈、PrefabStageUtility.EnterPrefabMode；Project 双击 .prefab 进入 |
| `SearchService` / Ctrl+K | ✅ | Quick Search：资产/Hierarchy/菜单/设置/窗口 Provider、FuzzyScore、SearchWindow、MenuItem Edit/Search All... _%k |
| `GameView` | ✅ | Display/Aspect/Scale/VSync/Maximize/Mute/Stats、Camera.Render→SRP、LightProbes、RenderTexture 目标 |
| `TextureCompressionUtility` | ✅ | DXT/BC/ETC/ETC2/ASTC/PVRTC 族、平台默认格式(Metal=ASTC,Vulkan=ASTC/ETC2,Desktop=DXT)、块大小/软压缩/IsFormatSupportedOnAPI |
| `PlatformGraphics` | ✅ | iOS Metal / Android Vulkan 主路径、GetPreferredApis、ConfigureIOSMetal/ConfigureAndroidVulkan |
| `HDROutputSettings` / HDR | ✅ | available/active/paperWhiteNits/automaticHDRTonemapping/displayColorGamut/bitDepth、native AnityHDR 路径、HDRUtilities 色调映射 |
| `ColorGamut` / `HDRDisplayBitDepth` | ✅ | sRGB/Rec709/Rec2020/DisplayP3/HDR10/DolbyHDR/HDR10Plus；8/10/16 bit |
| `anity-native` C++ | 🟡 | core/graphics/HDR/physics/audio/media/jobs/texture/transform/math/ui；Canvas persistent command/batch ownership 已以 native 配置 681/681 验证。GPU upload/draw、Transform 层级所有权、更多物理 solver/资源导入仍待 native 化 |
| `_scripts/` 环境 | ✅ | install-env/verify-env/build-native/build-all/gap-audit/install-vulkan/android |
| Unity API 官方反射门禁 | ✅ | 84 个官方 2022.3.51f1 UnityEngine/UnityEditor 程序集；类型/成员/参数/枚举/特性对照；当前类型存在 **928/4,117**、精确 **404**，成员存在 **8,645/37,164**、精确 **6,417**；SHA-256 baseline `regressions=0` / `removed-or-changed=0` / load issues=0；`UnityEngine.VFXModule` 公开差异 **0**。仍必须迁移到 2022.3.61f1 后重建最终基线 |
| D3D11 native device | ✅ | D3D11CreateDevice+WARP、swapchain/RTV、Present、HDR R10G10B10A2 |
| Vulkan native device | ✅ | Instance/Physical/Logical device（需 Vulkan SDK） |
| URP PostProcessPass | ✅ | Bloom/Tonemap/ColorAdjustments Volume→globals、自动 Feature 注入 |
| Native 热路径 | 🟡 | CCD TOI / 2D SAT / ConstantForce 3D/2D / Audio decode / Texture compress / Transform 仿射矩阵链 / Matrix4x4 / Canvas vertex staging、bounds、quad indices、clip-alpha visibility 走 anity-native；完整 Canvas GPU dispatch、Transform state、物理世界与资源导入所有权仍待 native 化 |
| `Display` | ✅ | multi-display、Activate、RelativeMouseAt、HDR 探测扩展 |
| `ColorSpacePipeline` | ✅ | Linear/Gamma 转换、ConfigureURPLinearHDR |
| `ScreenCapture` | ✅ | CaptureScreenshot/AsTexture/IntoRenderTexture、superSize、StereoMode、真 PNG；测试≥12 |
| `Il2CppBuilder` / IL2CPP 管线 | ✅ | CodeGeneration/CompilerConfig/stripping、.cpp stub、link.xml、AOT 注册；测试≥14 |
| `anity.exe` CLI | ✅ | Unity 兼容 batchmode/quit/projectPath/executeMethod/build*/runTests + il2cpp/screenshot/agent；测试≥13 |
| `Anity.Agent` 官方扩展 | 🟡 | 独立包 Session/Memory/Tools；自定义API Key/Base URL/model、SSE、tool calling、Editor窗口与OS vault已实现。0.6.0新增工具Requested/8类终态审计、默认fail-closed、无原文digest、64 KiB结果上限、跨轮finish reason/usage保存，以及Editor项目级有界SHA-256链/轮换/启动验证/独占lock/Unix私有权限。Agent **91/91**、Editor Host **39/39**、CLI **16/16**；统一Release矩阵强制native并达到 **2,537/2,537**、0失败、0跳过。审计HMAC/外部anchor、持久session、完整JSON Schema、Windows/Linux vault实机、Responses API、真实多厂商及网络矩阵仍缺，故不能标全完成 |
| `Canvas` Overlay/Camera/World | ✅ | 官方根命名空间与公开面差异 0；pixelRect、planeDistance、worldCamera、rootCanvas、排序、根布局 size/scale/position；Canvas/utility/raycaster 定向 43 测试 |
| `CanvasScaler` 三模式 | ✅ | ConstantPixel/ScaleWithScreen(Match/Expand/Shrink)/Physical；UIBehaviour override 修复 |
| `Job System` 深度 | ✅ | ThreadPool 并行、依赖 Complete、Combine、JobsUtility；测试≥13 |
| `Il2CppApi` 深度 | ✅ | icall/pinvoke/method pointer、Invoke、Strip preserve、Builder 集成 |
| `Undo` | ✅ | RecordObject/RecordObjects/DestroyObjectImmediate、Stack&lt;UndoCommand&gt;记录、PerformUndo/PerformRedo、undoRedoPerformed事件 |
| `MenuItem` / `MenuCommand` / `ContextMenu` / `ContextMenuItem` / `AddComponentMenu` | ✅ | Attribute类、menuName/validate/priority/context |
| `GenericMenu` | ✅ | AddItem/AddDisabledItem/AddSeparator/ShowAsContext/DropDown、内部MenuItemData列表 |
| `SerializedObject` / `SerializedProperty` | ✅ | targetObject/targets/ApplyModifiedProperties/FindProperty/Update、反射访问字段、intValue/floatValue/boolValue/stringValue/colorValue/vector2Value/vector3Value/enumValue/objectReferenceValue/arraySize等 |
| `Editor` | ✅ | target/targets/serializedObject/Repaint、OnInspectorGUI/OnSceneGUI/CreateEditor/DrawDefaultInspector |
| `BuildPipeline` | ✅ | BuildPlayer(BuildPlayerOptions)完整实现、**完整BuildTarget枚举**(StandaloneWindows/64/OSX/Linux64/Android/iOS/tvOS/VisionOS/WebGL/WSAPlayer/PS4/PS5/XboxOne/XboxOneD3D12/Switch/Lumin/Stadia/EmbeddedLinux)、正确BuildTargetGroup映射、平台扩展名自动匹配(.exe/.apk/.ipa/.app/.x86_64/.appx)、NormalizeOutputPath目录创建、BuildReport/BuildSummary完整结构 |
| `BuildReport` / `BuildSummary` / `BuildFile` | ✅ | BuildReport(BuildSummary(result/outputPath/totalSize/totalTime/totalErrors/totalWarnings/platform/platformGroup/platformDefaultExtension/buildGuid)) |
| `BuildPlayerWindow` / `BuildPlayerOptions` | ✅ | BuildPlayerOptions完整结构(scenes/locationPathName/target/targetGroup/options/subtarget/extraScriptingDefines等) |
| `BuildTarget` / `BuildTargetGroup` / `BuildOptions` | ✅ | BuildTarget完整Unity 2022值、BuildTargetGroup完整映射(iOS=4/Android=7/WebGL=13/Standalone=1/tvOS=25/VisionOS=39等)、BuildOptions完整标志位(Development/AutoRunPlayer/CompressWithLz4/ConnectWithProfiler/AllowDebugging/StrictMode/StripEngineCode/ForceSingleInstance等) |
| `GraphicsDeviceType` 枚举 | ✅ | 完整支持：Direct3D11(2)/Direct3D12(18)/Vulkan(21)/Metal(16)/OpenGLCore(17)/OpenGLES2(8)/OpenGLES3(11)/WebGL2(28)/WebGPU(29)/Null(4)/PlayStation4(13)/XboxOne(14)/Switch(22)/PlayStation5(26)/XboxOneD3D12(23) |
| `PlayerSettings` | ✅ | productName/companyName/applicationIdentifier/bundleVersion/buildGUID、runInBackground/defaultScreenWidth/Height、Set/GetGraphicsAPIs按平台返回正确默认图形API数组(Win:D3D11/D3D12/Vulkan/GL; iOS:Metal; Android:Vulkan/GLES3/GLES2; WebGL:WebGL2)、ScriptingBackend(Mono2x/IL2CPP/Wasm/WebAssembly)、iOS/Settings(sdkVersion/targetOSVersion/requireARKit/usageDescriptions)、AndroidSettings(minSdkVersion=24/targetSdkVersion=34/targetArchitectures/bundleVersionCode/keystore)、WebGL设置(memorySize/compressionFormat/linkerTarget/threadsSupport/exceptionSupport)、SetScriptingDefineSymbols、fullScreenMode/resizableWindow/vSyncCount/antiAliasing/colorSpace/stripEngineCode/graphicsJobs、平台特定设置类(iOS/Android嵌套) |
| `EditorUserBuildSettings` | ✅ | activeBuildTarget/selectedBuildTargetGroup、SwitchActiveBuildTarget切换平台同时override SystemInfo.graphicsDeviceType/deviceType/Application.platform、BuildTargetToBuildTargetGroup完整映射、BuildTargetToRuntimePlatform(iOS→IPhonePlayer/Android→Android/WebGL→WebGLPlayer)、developmentBuild/connectProfiler/allowDebugging、androidBuildSubtarget |
| `EditorBuildSettings` | ✅ | scenes/activeBuildTarget、EditorBuildSettingsScene(path/enabled) |
| `AndroidSdkVersions` / `AndroidArchitecture` / `WebGL*Enums` | ✅ | AndroidApiLevel16-34、AndroidArchitecture(ARMv7/ARM64/X86/X86_64/All)、WebGLCompressionFormat(Brotli/Gzip/Disabled)、WebGLLinkerTarget(Asm/Wasm/Both)、WebGLExceptionSupport(None/Explicit/Full)、AndroidGamepadSupportLevel、UIInterfaceOrientationMask |
| `EditorSceneManager` | 🟡 | OpenScene/SaveScene/NewScene/CloseScene/SetActiveScene/MoveSceneAfter/sceneLoaded/sceneClosed 等已实现；剩余官方公开面、Prefab Stage/多 Scene 编辑、异步导入与保存生命周期尚待反射及官方 A/B 闭环 |
| `ProjectWindow` / `HierarchyWindow` / `InspectorWindow` / `ConsoleWindow` / `SceneViewWindow` | ✅ | **完整Unity 2022 Pro编辑器面板对齐**：InspectorWindow(GetComponents<Component>()遍历全部组件、组件头启用开关+上下文菜单+折叠箭头、反射绘制bool/int/float/string/Vector2/3/4/Color/Object/enum序列化字段、Material/Texture/AudioClip预览区、Add Component可搜索下拉菜单、多对象编辑mixedValue)、HierarchyWindow(SceneManager.GetActiveScene().GetRootGameObjects()真实构建树、active状态Toggle、右键上下文菜单Create 3D/2D/Light/Audio/UI/Empty、搜索过滤、AlphabeticalSort/TransformSort、选择同步InspectorWindow)、ProjectWindow(AssetDatabase.GetAllAssetPaths()真实加载资产、网格/列表视图切换+图标大小控制、面包屑路径导航、Favorites收藏夹、双击打开资产、文件大小/修改时间、单列/双列模式)、ConsoleWindow(Collapse折叠合并相同消息+计数徽章、时间戳、双击Open in Editor、编译错误/警告分离计数、Recompile按钮、富文本着色、自动滚动到底部、复制消息+堆栈到剪贴板)、SceneView（完整工具栏DrawMode/2D/3D/Gizmos/Audio/视角快速切换+FrameSelected），所有窗口对齐Unity 2022深色主题配色 |
| `SettingsProvider` | ✅ | path/label/keywords/guiHandler/OnGUI抽象 |
| `CompilationPipeline` / `AssemblyBuilder` | ✅ | CompilationStarted/CompilationFinished/assemblyCompilationEvents、AssemblyBuilder(assemblyPath/scriptPaths/extraDefines/build/references) |
| `PackageManager.Client` / `PackageInfo` | ✅ | Add/Remove/Search/List/Embed/Install/ResetToEditorDefaults、PackageInfo(name/displayName/version/dependencies) |
| `Shader Graph` 官方包 | 🟡 | 独立 `Unity.ShaderGraph.Editor` 程序集已建立；multi-json/legacy 读取与升级、拓扑 DAG、16 类 property Blackboard、keyword pragma、Target/SubTarget（URP/Built-in/HDRP 保真读取且仅 URP 产品支持）、URP14 Lit/Unlit pass planner，以及 Vector1/算术/PropertyNode/Custom Function 的真实 scalar HLSL generation 已实现。Custom Function 支持 String 定义与 File GUID include，严格拒绝占位/冲突/非法路径和非标量形态。**198/198** 测试通过；本机 2022.3.51f1 / Shader Graph 14.0.11 的 **370** 个官方图资产（356 modern + 14 legacy）通过 typed gate。仍缺 2022.3.61f1 重验、完整 node/property runtime value、Custom Function 向量/纹理/多输出、URP Decal/Fullscreen/全 variant、窗口/预览、运行时与截图/产物 A/B，故保持 🟡 |
| `Visual Effect Graph` 官方包 | 🟡 | 独立 `Unity.VisualEffectGraph.Editor` 程序集已建立；`.vfx` registry 有 **72 个** typed GUID，runtime asset **v15** 保持 v1-v14 读取，并导出 context/system/control/output/kernel、Spawner/Initialize/Update、Recorded/Manual/Automatic bounds 与 Planar Output ABI。Metal 已形成 **3-slot generation chain + persistent particle/dead/allocation resource group**；Update 与 indirect Initialize 均在 GPU。Initialize具备 Metal command handle与中央48-byte multi-dispatch ticket，支持多 system/effect、原子publish、generation CAS、Cancel/failure reverse restore及Clear/Reset/device teardown。已有resident的pending Initialize可在同一Metal queue串联后继Update与Planar Camera；同一effect的1–10个独立particle system已验证可在单Camera command buffer/render pass全部真实绘制。Prepare不再制造CPU屏障，Automatic Bounds/显式readback只在真正CPU依赖点退休最终链。现已具备Initialize/Update/Camera确定性command failure、整链原子回滚，以及对Metal真实 `DeviceRemoved/AccessRevoked` 的永久device-health分类；device-lost一致封锁VFX/UI/texture/swapchain提交，普通Camera失败仍可按submission恢复。逐fence结果保留最近1024项并显式统计淘汰，过期fence不会把旧失败误报成功。首次非resident Initialize、真实硬件removal/reset实机A/B、多个Camera在途、跨queue shared event、Vulkan/D3D同等resident chain、Texture/flipbook/Shader Graph material、soft particle/motion vector、URP camera stack/XR、Mesh/Strip/GPU Event及Unity 2022.3.61f1 Player A/B仍缺。backend stats **528 bytes**，VFX **828/828**、Core native **1,664/1,664**、VFX Graph **490/490**，故保持 🟡 |
| `VFX CPU Event runtime mapping / Initialize dispatch` | 🟡 | **本行是上方 VFX 总行的增量证据**：native C++ Spawner/Initialize/Update 已形成持久particle lifecycle；Update与Initialize ticket均使用48-byte info ABI。Initialize ticket持有staged registry、stable metadata outputs、effect ownership和多个backend handles；Poll不等待，全部Complete后只合并相关effect，Cancel/任一failure整批恢复。已有15个Initialize/Update failure发现项、21个Camera/device-loss发现项及新增20个Camera压力发现项；后者覆盖同effect 1–10 system单提交真实绘制，以及1025–1034次真实Camera pass下精确history eviction与旧失败fence过期语义。`VisualEffect.ProcessInputEvents`/Spawner保存pending FIFO，Planar draw对resident target走同队列GPU依赖；多个Camera在途、真实硬件reset日志、跨queue shared event、Vulkan/D3D与完整Output仍缺，故保持 🟡 |
| `Addressables` | ✅ | Catalog/标签/依赖图、Register/RegisterBundle/BuildPlayerContent、LoadAsset/LoadAssets/ByLabel/Instantiate/LoadScene、MergeMode、DownloadDependencies、ResourceLocator、AssetReference；测试≥22 |
| `Il2CppToolchain` | ✅ | CMake/config.h/MethodMap/ABI、LinkPlayer、CompileAllUnits、DetectCompiler；编译/链接 stdout+stderr 并行排空、超时杀进程，2,080+ 单元按 CPU 并行编译，已消除完整门禁挂死 |
| `Il2CppPlayerHost` | ✅ | BuildPlayer/Launch/LaunchManaged；native exe 或 managed 回退；测试≥13 |
| `Il2CppPackagePipeline` | ✅ | 端到端 convert→artifacts→link→launch；CLI `-il2cpp`/`-build*Player`；测试≥13 |
| `PlatformGraphics` Metal/Vulkan | ✅ | iOS→Metal、Android→Vulkan、Force/PreferredApis；测试≥11 |
| `Native Swapchain` | ✅ | Create/Acquire/Present；Vulkan Win32/Android/X11/Wayland surface + headless ring；Metal CAMetalLayer/EDR；PresentCount/HasNativeSurface/BackendKind/SurfaceKind；测试≥18+surface |
| `Vulkan surface platforms` | ✅ | HWND / ANativeWindow / AnityX11NativeWindow / AnityWaylandNativeWindow；GetSupportedSurfaceMask；PlatformGraphics 映射 |
| `InternalEditorUtility` | ✅ | **完整UnityEditorInternal API**：inBatchMode/isHumanControllable/isApplicationActive/hasProLicense/unityVersion/isProSkin/unityPreferencesFolder/projectPath、tags/layers/sortingLayerNames/sortingLayerUniqueIDs/asmrefGUIDs/assemblyNames、ReloadAssemblies/RequestScriptReload/IsRecompiling、OpenFileAtLineExternal、LoadRequiredAdditionalDataToWindow、LoadWindowLayout、GetAllGlobalTags/GetAllLayers/TagToLayer/LayerToTag、IsNativeModule/GetScriptAssemblies/GetEditorScriptAssemblies/GetRuntimeScriptAssemblies/GetAssemblyPath/GetAssemblies、IsInEditor/IsInPlayer/GetPlatformDefines/GetDefinesForAssembly/GetPredefinedDefines、RepaintAll/SetDirty/IsObjectAManagedReference、GetSerializedObjectProperties/GetActiveSceneName/GetOpenScenes/IsSceneSaved/GetSceneAssetPath、FindAssets/GetAssetPath/GUIDToAssetPath/AssetPathToGUID、CalculateBounds真实Renderer包围盒计算、SetIconForObject/GetIconForObject图标管理、scriptReloaded事件 |
| `BuildCallbacks` | ✅ | 接口定义 |
| `EditorSettings` | ✅ | Dictionary存储、serializationMode、defaultBehaviorMode、enterPlayModeOptions、spritePackerMode、asyncShaderCompilation、cacheServer配置、projectGenerationRootNamespace、DefineSymbols等完整属性 |
| `ProjectSettings` | ✅ | Dictionary存储、productName/companyName/applicationIdentifier、**runInBackground**、defaultScreenOrientation/Width/Height、scriptingBackend(Mono/IL2CPP)、apiCompatibilityLevel、strippingLevel(Low/Medium/High/Disabled)、vSyncCount、targetFrameRate、colorSpace(Gamma/Linear)、graphicsJobs、iOS/android配置等完整属性 |
| `Lightmapping` | ✅ | 见UnityEngine.LightModule |
| `TextureImporter/ModelImporter/AudioImporter` | ✅ | 见AssetImporter |
| `EditorUtility` | ✅ | DisplayDialog/ProgressBar/ClearProgressBar/SaveFilePanel/OpenFilePanel/SetDirty/InstanceIDToObject/FormatBytes/NaturalCompare |
| `EditorPrefs` | ✅ | 独立Dictionary(Editor专属)、SetInt/GetInt/SetFloat/GetFloat/SetString/GetString/HasKey/DeleteKey/DeleteAll |
| `GUILayout/IMGUI/Event` | ✅ | Event(Current/type/mousePosition/keyCode/button/Use)、EventType(MouseDown/Up/Move/KeyDown/Up/Repaint/ScrollWheel/DragPerform等)、GUILayout(Begin/EndArea/Horizontal/Vertical/ScrollView/Button/Box/Label/TextField/PasswordField/Toggle/Slider/Toolbar/SelectionGrid/Window/Space/FlexibleSpace)、BeginScrollView/BeginArea push/pop scope栈、Handles.BeginGUI/EndGUI矩阵相机栈、EditorPrefs.Save序列化为JSON |
| `GUI` IMGUI 交互 | ✅ | 稳定 ControlId、Button MouseDown/Up 命中、Toggle 翻转、TextField 输入/Backspace/maxLength、H/V Slider 拖拽、BeginGroup 状态栈、Window 回调、GUIUtility hotControl/matrix；测试≥17 |
| `Playables` / `PlayableDirector` | ✅ | PlayableGraph Play/Stop/Evaluate/SetTime、ScriptPlayable&lt;T&gt;、ScriptPlayableOutput、PlayableAsset、Director Hold/Loop/None、played/paused/stopped、**Signal 跨时间发射**；测试≥16 |
| `Timeline` | ✅ | TimelineAsset/TrackAsset/TimelineClip、Animation/Audio/Activation/Control/Signal/PlayableTrack、GetClipsAt/muted、CreatePlayable 接 Director；测试≥16 |
| `Timeline Signal` | ✅ | SignalAsset/Emitter/Receiver/Track、emitOnce、Loop 重置、Director.signalReceiver；测试≥10 |
| `TimelineWindow` | ✅ | Window/Sequencing/Timeline、playhead、AddTrack/AddSignal、Play/Pause/Stop/Tick；测试≥5 |
| `MaterialEditor` | ✅ | 继承Editor、customShaderGUI/isVisible/firstInspectedEditor、OnInspectorGUI/OnHeaderGUI/DrawHeader/DrawPropertiesExcluding、DefaultShaderProperty/TexturePropertySingleLine/TwoLine/ColorProperty/FloatProperty/RangeProperty/VectorProperty/ShaderProperty、RenderQueueField/EnableInstancingField/DoubleSidedGIField/LightmapEmissionFlagsProperty/EmissionEnabledProperty、DrawPreview/GetPreviewTitle/OnPreviewSettings、静态RegisterPropertyChangeUndo/FixupEmissiveFlag/GetDefaultShaderProperty |
| `ShaderImporter` | ✅ | 继承AssetImporter、defaultTextureCompression/preprocessorOverride/shaderCompilerPlatforms(HashSet)/disableOptimizations/nonModifiableTextures、OnImportAsset/GetShaderCompilerDefines/SetShaderCompilerDefines |
| `ShaderUtil` | ✅ | 完整API：FindShader/GetPropertyCount/GetPropertyType/GetPropertyName/GetPropertyDescription/GetRangeLimits/GetTexDim/GetTextureBindingIndex/GetBufferBindingIndex/GetMaterialProperties/IsShaderPropertyHidden/IsPassEnabled/SetPassEnabled/GetBlendFactors/GetZTest/GetZWrite/GetCullMode/GetStencilOp/GetStencilComp/GetStencilRefForPass/GetShaderKeywords/GetRenderQueue/SetRenderQueue/HasInstancing/IsShaderCompiled/GetCustomEditor/WarmupShaderFromCollection/CalculateFogStencil/GetGlobalTextureDimension/CreateShaderMaterial/GetDependency/GetDependencyNames/CompileShader/SetShaderVariantCollection/ClearShaderCache/ApplyMaterialPropertyBlock |
| `MaterialProperty` | ✅ | 结构体：name/nameID/type/flags/floatValue/colorValue/vectorValue/textureValue/rangeLimits/textureDimension |

---

## 27. Anity 特有模块

| 模块 | 状态 | 备注 |
|------|------|------|
| `Anity.Core` 编译 | ✅ | 0错误编译 |
| `Anity.Core.Unity` 编译 | ✅ | 0错误编译 |
| `Anity.WebGL` 编译 | ✅ | 0错误编译 |
| `Anity.Hub` 编译 | ✅ | 0错误编译 |
| `Anity.Editor.Host` 编译 | ✅ | 0错误编译 |
| `Anity.Core.Analyzers` | ✅ | AOT/API 兼容分析器 |
| `HotUpdateContext` | ✅ | DefaultAssemblyLoadContext自定义(解决.NET Standard抽象)、LoadAssembly(byte[])/Assemblies/ExecuteEntryPoint/InvokeMethod/Unload |
| `Il2CppRuntime` | ✅ | Platform枚举(Interpreter/Mono/IL2CPP/WebGL)、IsIl2Cpp/CurrentPlatform、AOTSuffix、GetGenericMethod/ImplementGeneric |
| `PlatformConfig` | ✅ | TargetPlatform枚举(WebGL/Windows/Mac/Linux/Android/iOS)、IsWebGL/IsWindows/IsEditor/IsMobile、SetTargetPlatform/SetRenderPipeline |
| `Gizmos` | ✅ | color/matrix、DrawLine/DrawRay/DrawWireSphere/DrawSphere/DrawWireCube/DrawCube/DrawMesh、List&lt;GizmoCommand&gt; |
| `Touch输入完整` | ✅ | Touch结构体(fingerId/position/deltaPosition/deltaTime/tapCount/phase(TouchPhase)/pressure/radius/type)、TouchPhase(Began/Moved/Stationary/Ended/Canceled)、TouchType(Direct/Indirect/Stylus) |
| `ILogger/ILogHandler` | ✅ | 接口、Debug.unityLogger默认Console实现 |

---

## 28. 高频缺失清单落地状态

🟡 **以下为历史 API 覆盖盘点，不等于 Unity 2022.3.61f1 全行为已对等；需继续经过官方反射面、行为 A/B、编辑器与平台产物门禁复核。**

1. 🟡 运行时核心：Object/GameObject/Component/Behaviour/MonoBehaviour/Transform/Rect/Time/Application/Debug/Input/LayerMask/Random/JsonUtility/ScriptableObject/RectTransform 已有主路径；Transform shear/奇异仿射、Matrix4x4/FrustumPlanes、RectTransform 公开面/核心布局及 DrivenRectTransformTracker/布局 ownership 已闭环，但 native 层级/Canvas dispatch 与完整编辑器行为仍在推进
2. ✅ 3D物理全量：Rigidbody/Collider/各种Collider/CharacterController/WheelCollider/PhysicMaterial/Joints/Collision/ContactPoint完整物理模拟
3. ✅ 2D物理全量：ContactFilter2D实现，CompositeCollider2D完整凸包算法与路径管理
4. ✅ UI全量：CanvasScaler/CanvasRenderer/MaskableGraphic/Toggle/ToggleGroup/Slider/Horizontal/Vertical/GridLayoutGroup/ContentSizeFitter/AspectRatioFitter/CanvasGroup完整布局系统
5. ✅ 渲染+SRP全量：Graphics/GraphicsSettings/QualitySettings/RenderPipeline/RenderPipelineAsset/RenderPipelineManager/ScriptableRenderContext/CommandBuffer/CullingResults/DrawingSettings/FilteringSettings/Volume框架/Light/ReflectionProbe/RenderSettings
6. ✅ Renderer/Material/Texture/Mesh/Audio全量：Renderer/MaterialPropertyBlock/各种Renderer/Material/Shader/Texture/Texture2D/RenderTexture/Cubemap/Mesh/AudioClip/AudioSource
7. 🟡 ParticleSystem/Terrain/Tilemap/TMP/Events/Scene/Profiler：主路径已实现；Scene/SceneManager 公开面已精确，但异步资源/平台卸载/编辑器多 Scene 生命周期及其余模块仍需逐项官方 A/B 复核
8. ✅ Jobs/Burst/Native全量：IJob系列、JobHandle、NativeArray/NativeList/NativeHashMap/NativeMultiHashMap/NativeQueue、Burst属性
9. 🟡 Editor 主路径：EditorApplication/EditorWindow/EditorGUI/EditorGUILayout/GUIStyle/Handles/SceneView/Selection/AssetDatabase/PrefabUtility/Undo/MenuItem/GenericMenu/SerializedObject/Editor/BuildPipeline/PlayerSettings/EditorBuildSettings/EditorSceneManager/EditorUtility/EditorPrefs/CompilationPipeline/PackageManager/SettingsProvider/GUILayout/Event；仍需官方反射、交互与平台产物全量门禁
10. ✅ Anity特有：HotUpdateContext/Il2CppRuntime/PlatformConfig/Gizmos/ComputeBuffer/AsyncGPUReadback/LODGroup/ShaderVariantCollection/Addressables/Touch输入
11. ✅ UIElements全量：VisualElement/ScrollView/Scroller/Button/Label/TextField/Toggle/Slider/DropdownField/ListView/TreeView/Foldout/ProgressBar/HelpBox/EnumField/MinMaxSlider/BoundsFields/TagLayerFields/Toolbar/TabView/RadioButton完整控件系统
12. ✅ 动画Avatar全量：Avatar/AvatarMask/AvatarBuilder/HumanBone/SkeletonBone/HumanLimit/HumanDescription完整Avatar系统
13. ✅ MeshData全量：MeshData/MeshDataArray/VertexAttribute/VertexAttributeFormat/VertexAttributeDescriptor/SubmeshDescriptor/MeshUpdateFlags/MeshUtility完整高性能网格API
14. ✅ 编辑器内部API：InternalEditorUtility完整标签/层/程序集/资源/图标/包围盒管理
15. ✅ WebGL视频：WebGLVideo/WebGLApplication完整视频播放管理
16. ✅ Lightmap光照设置：LightmapSettings/LightmapParameters/AmbientMode完整光照配置
17. ✅ Profiler完整层级：Profiler/ProfilerUnsafeUtility/ProfilerRecorder/ProfilerMarker/ProfilerArea/ProfilerCategory全层级性能分析API
