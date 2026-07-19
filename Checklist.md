# Unity 2022.3.61f1 Pro 兼容性功能清单

> 本文件记录 Anity 对 Unity 2022.3.61f1 Pro 及其官方包的实现状态；当前本机自动审计证据仍为 2022.3.51f1，必须迁移到 2022.3.61f1 后才能作为最终版本证据。
> 状态说明：
> - ✅ 已实现并通过备注所列范围的行为测试（只代表该行证据，不代表 Unity 全局对等）
> - 🟡 部分实现 / API 壳 / 尚缺 Unity 官方 A/B 行为证据
> - ❌ 未实现
>
> **全局状态：🟡 持续推进。** “Anity = 源码自主可控的 Unity 2022 Ultra” 是最终验收目标；只有官方 Unity 2022.3 反射面、行为 fixture、编辑器交互及各平台产物门禁全部通过后，才能宣称完全对等。官方 2022.3.51f1 当前预备基线：类型存在 989/4,117（24.022%）、类型契约完全一致 460（11.173%）；成员存在 9,242/37,164（24.868%）、成员契约完全一致 6,973（18.763%）；缺失类型 3,128、真实缺失成员 27,922。本轮 native-required 统一 Release 门禁 **4,212/4,212**（Core **3,190/3,190**）通过、0 失败、0 跳过；目标 2022.3.61f1 尚未安装，以上仍不可作为最终 Pro 证据。

---

## 0. 仓库与构建卫生

| 项目 | 状态 | 备注 |
|------|------|------|
| Monorepo source layout | ✅ | 四个历史 submodule 声明与实际 Git index 不一致，已证明当前模块全部为普通 tracked source；`.gitmodules`、recursive checkout、`modules/` ignore 与多仓库文档已移除 |
| `_scripts/` 唯一入口 | ✅ | 12 个未被现行构建/测试/workflow 引用的旧 `scripts/` helper 及其 README 已删除；8 个历史入口名在 tracked source 中反向引用为 0，AGENTS 明确禁止恢复旧目录 |
| Cache-free rebuild | ✅ | Visibility hierarchy/layer 最终门禁完成 build-all 0 错误、八工程 **4,230/4,230**、self-contained CLI 与 App 安装运行门禁；逐项确认 Git ignored/零 tracked 后移出 39 个 repo-local `bin/obj/build`、399,540 KiB 至可恢复废纸篓，最终全类生成目录复扫为 0 |
| Unity obsolete/legacy compatibility | 🟡 | 精确引用审计删除唯一零调用/零导出的内部 VFX synchronous legacy 实现 220 行；Unity 2022.3 公开 `[Obsolete]` API、removed networking、FBX legacy 数值、Shader/VFX deprecated serialized migration及仍被调用的兼容 ABI 均有反射/行为/资产测试引用，按兼容目标保留；完整 obsolete surface 仍随全局 parity 推进 |

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
| `AnimationCurve` | ✅ | Cubic Hermite + tangents；Linear/EaseInOut/Constant；Loop/PingPong wrap；SmoothTangents；支持 Unity constant/stepped curve 的 infinite-tangent 求值且不产生 NaN；测试≥14 |
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
| `SystemInfo` | ✅ | 设备/OS/CPU/GPU信息(deviceName/model/type/uniqueId/operatingSystem/processorCount/frequency/systemMemorySize)、graphicsDeviceType支持D3D11/D3D12/Vulkan/Metal/OpenGLCore/OpenGLES2/OpenGLES3/WebGL2、graphicsDeviceVersion自动匹配、supports*系列特性（含 `supportsAsyncGPUReadback`）、graphicsShaderLevel=50、IsFormatSupported、overrideGraphicsDeviceType支持构建切换 |
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
| `AsyncGPUReadback/GraphicsFence/GraphicsBuffer` | 🟡 | `UnityEngine.Rendering` 的 sequential `AsyncGPUReadbackRequest` 与全量 Request/NativeArray/NativeSlice 公开签名已反射对齐；Unity 2022.3.51f1 预检亦确认 optional metadata、泛型约束与 StaticAccessor 特性准确（最终 2022.3.61f1 A/B 待完成）。PlayerLoop deferred completion、Texture2D mip/region、Texture2DArray/Texture3D/Cubemap/CubemapArray z/layer、ComputeBuffer/GraphicsBuffer byte range、native RenderTexture RGBA8、callback/error/typed `GetData<T>(layer)` 已有 19 用例。GraphicsFence/GPUFence、两个 fence type、stage enums 与 CommandBuffer create/wait overload 亦已反射对齐：其在 Anity command-buffer submit 后下一 PlayerLoop frame retirement，并支持 dependency，11 项测试通过。尚缺格式转换、mipmapped volume/cube、native Metal/Vulkan/D3D fence 与 Unity 2022.3.61f1 A/B，因此不能标为完成。 |

---

## 7. UnityEngine.AnimationModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Keyframe` | ✅ | time/value/inTangent/outTangent/inWeight/outWeight/weightedMode |
| `AnimationCurve` | ✅ | Evaluate Hermite 插值、preWrapMode/postWrapMode（Loop/PingPong/ClampForever）、AddKey；Unity constant/stepped curve 的 infinite-tangent segment 保持左值到右 key，避免 NaN |
| `AnimationClip` | 🟡 | 继承Motion、length/frameRate/wrapMode、SetCurve/GetCurve、AnimationCurveBinding[]、event 主路径已具备；SampleAnimation/Animator property pose 已覆盖 Transform、`SkinnedMeshRenderer.blendShape.*` 与 `Renderer.m_Enabled`。FBX blend-shape 已按 Unity 2022.3 探针闭环 24 Hz deformation、Bezier tangent、compression/error、source frame、切片与多 clip（42/42）；Transform raw/resampled、同步 quaternion reduction、非 XYZ gimbal 与 wrap/tie/subframe exact-bit 主链已覆盖。pre/post/pivot/geometric 双模式新增 20/20；10 组 pivot raw Euler 与 retained position 均各自达到 **720/720 float exact-bit**，Pre/Post quaternion 10 组 24×4 key 达到 **960/960 float exact-bit**。Visibility 已覆盖 raw float、非零 bool、resample/raw step、compression/import off、祖先与 helper topology 的数值乘积，以及 additive/override/override-passthrough/animated-weight 多层 bake；`NativeModelImportTests` **175/175**。weighted layer curve 仍有最大约 4.3e-5 value/8.3e-4 tangent 差；通用组件 curve、layer mute/solo、root-motion/Player A/B 未闭环 |
| `AnimationEvent` | ✅ | time/functionName/stringParameter/floatParameter/intParameter/objectReferenceParameter |
| `Motion（抽象基类）` | ✅ | name/humanCycle/humanTranslation/averageDuration、ComputeHashCode |
| `BlendTree` | ✅ | 继承Motion、blendType(1D/2DSimpleDirectional/2DFreeformDirectional/2DFreeformCartesian/Direct)、blendParameter/Y、children ChildMotion[]、1D阈值排序线性插值、2D距离反比权重、Direct直接权重 |
| `ChildMotion` | ✅ | motion/threshold/position/timeScale/cycleOffset/directBlendParameter |
| `AnimatorState` | ✅ | name/cycleOffset/speed/speedParameter/motion/transitions/behaviours/iKOnFeet/writeDefaultValues/tag/mirror |
| `AnimatorStateMachine` | ✅ | states/stateMachines/anyStateTransitions/entryTransitions、AddState/AddStateMachine/AddAnyStateTransition/AddEntryTransition |
| `ChildAnimatorState/ChildAnimatorStateMachine` | ✅ | position、state/stateMachine |
| `AnimatorControllerLayer` | 🟡 | generic Transform、blend-shape 与 Renderer enabled float property pose 已接通 Override/reference-pose Additive、weight 与 AvatarMask exact-path 过滤；Transform/float additive reference 在 BlendTree 内独立合成。Humanoid muscle/IK、其它通用 property、synced layer、write defaults、root motion 与正式 2022.3.61f1 A/B 未闭环 |
| `AnimatorController` | ✅ | 继承RuntimeAnimatorController、animationClips/layers/parameters、AddLayer/AddParameter |
| `AnimatorStateTransition` | ✅ | 继承AnimatorTransitionBase、duration/exitTime/hasExitTime/hasFixedDuration/offset/canTransitionToSelf、conditions[] AnimatorCondition |
| `AnimatorCondition` | ✅ | parameter/mode(Greater/Less/Equals/NotEquals/If/IfNot)/threshold |
| `AnimatorControllerParameter` | ✅ | name/nameHash/type(Float/Int/Bool/Trigger)/defaultFloat/defaultInt/defaultBool |
| `AnimatorStateInfo` | ✅ | fullPathHash/shortNameHash/length/normalizedTime/speed/loop、IsName/IsTag哈希比较 |
| `AnimatorOverrideController` | ✅ | runtimeAnimatorController、indexer[AnimationClip]=AnimationClip、GetOverrides/ApplyOverrides |
| `Animator` | 🟡 | 参数、Play/CrossFade/状态推进已有主路径；generic layer 以 native Transform pose graph + 托管 float property pose 合成 Override/Additive、mask、crossfade 与 BlendTree，不再 last-clip-wins；真实 FBX blendShape clip 的直接播放、Override/additive reference，以及 imported `Renderer.m_Enabled` 直接播放已测。Humanoid muscle/IK pass、其它通用 property、bool curve 的 crossfade/layer 官方逐帧 A/B、root motion、write defaults、synced layers、Playables 及完整公开面仍未闭环 |
| `StateMachineBehaviour` | ✅ | 继承ScriptableObject、OnStateEnter/Update/Exit/Move/IK/StateMachineEnter/Exit回调 |
| `RuntimeAnimatorController` | ✅ | 抽象基类、animationClips |
| `Avatar` / `AvatarMask` | 🟡 | `HumanDescription` / `HumanBone` / `HumanLimit` / `SkeletonBone`、`Avatar` / `AvatarBuilder` 及 `AvatarMask` / `AvatarMaskBodyPart` 已按本机 Unity 2022.3.51f1 预备反射做到对应公开面与 native metadata 一致；AvatarBuilder 已接 native hierarchy/rest-pose/mapping validation（23 项），ModelImporter 现把真实 decoded FBX hierarchy 交给同一 native validation 并产出有效 Generic imported Avatar；AvatarMask 已接 native body/path 状态（17 项）并被 generic Animator layer 的 native pose graph 消费（25 项）。HumanBone/SkeletonBone importer YAML、`motionNodeName` 与 GUID/fileID source Avatar 已双向持久化。Humanoid imported mapping/T-pose、muscle/finger/IK mask、root-motion rotation、retargeting 与 Unity 2022.3.61f1 A/B 尚未闭环，故不得标完整。 |
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
| `SkinnedMeshRenderer` | ✅ | sharedMesh/bones/rootBone/quality；native ModelImporter 会从真实 FBX skin cluster 或 blend deformer 建立 renderer、bindpose、最多 8 个 variable influences、共同 rootBone、localBounds 与 blend frames；FBX `blendShape.<name>` curve 可经 SampleAnimation/Animator 驱动 weight 并由 BakeMesh 输出实际 morph geometry |
| `SpriteRenderer` | ✅ | 继承Renderer |
| `TrailRenderer` / `LineRenderer` | ✅ | TrailRenderer时间老化点记录、LineRenderer SetPosition |
| `ParticleSystemRenderer` | ✅ | 继承Renderer |
| `Material` | ✅ | **完整Unity 2022 API**：shader/color/renderQueue/shaderKeywords、Dictionary属性SetFloat/GetFloat/SetColor/GetColor/SetInt/GetInt/SetVector/GetVector/SetMatrix/GetMatrix/SetTexture/GetTexture/SetBuffer/GetBuffer/SetFloatArray/SetColorArray/SetVectorArray/SetMatrixArray、EnableKeyword/DisableKeyword/IsKeywordEnabled/SetKeyword/CopyPropertiesFromMaterial/HasProperty/HasFloat/HasColor/HasInt/HasVector/HasMatrix/HasTexture/HasBuffer、GetPropertyName/GetPropertyCount/FindPass、SetRenderingMode(Opaque/AlphaBlend/AlphaTest/Additive)、Lerp插值、parent/DisableKeyword、GetPassName/passCount、shaderKeywords完整List管理、GetTag |
| `Shader` | ✅ | **完整ShaderLab/HLSL系统（Unity 2022对齐）**：name/renderQueue/passes/subShaders/properties/tags/keywords/constantBuffers/fallback/customEditor完整解析、ParseShaderSource解析ShaderLab语法、ParseShaderProperties(_Color/_MainTex/_Glossiness/Range等类型+默认值+flags)、ParseShaderTags(RenderPipeline/Queue/RenderType/DisableBatching/ForceNoMirroredLighting等)、ParseSubShaders/ParsePasses(Blend/BlendOp/ZWrite/ZTest/Cull/ColorMask/Offset/Stencil完整渲染状态解析)、ParseShaderKeywords(multi_compile/shader_feature multi_compile_instancing multi_compile_fog multi_compile_light)、ParseConstantBuffers(CBUFFER_START/UnityPerMaterial/UnityPerDraw SRP Batcher兼容检测)、GetPropertyName/FindPropertyIndex/PropertyToID/PropertyToName、SetGlobalFloat/Int/Vector/Color/Matrix/Texture/Buffer/全局属性管理、globalMaximumLOD/globalRenderPipeline/WarmupAllShaders、isInstancingSupported检测multi_compile_instancing/UNITY_INSTANCING_BUFFER/UNITY_VERTEX_INPUT_INSTANCE_ID、GPU Instancing完整支持(UNITY_ACCESS_INSTANCED_PROP/instanceID/SV_InstanceID)、ShaderPropertyFlags(Normal/Texture/HDR/PerRendererData/MainTexture/MainColor/NoScaleOffset)、ShaderPropertyType(Float/Int/Vector/Color/Texture/Matrix/Range)、GetPropertyCount/GetPropertyType/SetPropertyFlags/IsKeywordEnabled/EnableKeyword/DisableKeyword、Keywords系统(GlobalKeyword/LocalKeyword/ShaderKeywordSet/KeywordState)、ShaderVariantCollection(Add/Remove/Contains/WarmUp/ShaderVariant变体管理+WarmUp预编译)、Pass/SubShader结构(BlendState/DepthState/RasterState/StencilState完整渲染状态)、BlendMode/BlendOp/CullMode/CompareFunction/StencilOp/ColorWriteMask/BlendEquation完整枚举、HLSL编译框架(ShaderCompilerPlatform:D3D/Metal/Vulkan/OpenGLCore/GLES2/GLES3/WebGL等全平台、CompileShader/CompileShaderFromSource/SetIncludeHandler/ClearCachedData/Preprocess/ParseHLSL)、ShaderUtil类(GetPropertyCount/GetPropertyType/GetRangeLimits/GetShaderKeywords等)、Shader.dependency/HasPass/FindPassTag/GetDependency、SRP Batcher兼容性检测isSRPBatcherCompatible、变体收集WarmUp/CollectVariants |
| `ShaderVariantCollection` | ✅ | Add/Remove/RemoveVariant/Contains/WarmUp/WarmUpProgress/ShaderVariant(shader/passName/keywords[])结构、变体warmup模拟+isWarmedUp状态、ShaderVariantCollectionHelper枚举Shader/passes/keywords组合 |
| `BlendState/DepthState/RasterState/StencilState` | ✅ | 完整渲染状态结构体，BlendMode/BlendOp/CullMode/CompareFunction/StencilOp/ColorWriteMask枚举，Opaque/AlphaBlend/Additive/Modulate预定义状态 |
| `ComputeShader/ComputeBuffer/GraphicsBuffer` | 🟡 | ComputeBuffer(count/stride/SetData/GetData/SetCounterValue/Dispose/SetData(Array)/GetData(Array)/count/stride)、GraphicsBuffer(target/stride/count)；AsyncGPUReadback 的 buffer 真 byte-range readback 已接入，但该组 remaining fence/readback platform parity 见上行。 |
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
| `AssetDatabase` | 🟡 | 内存资产索引、LoadAssetAtPath&lt;T&gt;/AssetPathToGUID/GUIDToAssetPath/Contains/Refresh/CreateAsset/DeleteAsset/MoveAsset/CopyAsset/GetAllAssetPaths/FindAssets；`CreateAsset`/`AddObjectToAsset` 与高频 object load APIs 的 Object 签名、metadata 已经本机预备反射核对；`AddObjectToAsset` 已维护 main/sub-asset，all-assets 包含 main、representations 仅含 sub-assets，Contains/path/sub-asset/GUID/Move/Delete 均同步关系（13 用例）；批量 `MoveAsset(paths, folder)` 会预检重复/缺失/碰撞/递归、同步磁盘 asset/meta，并在执行失败后回滚，保持 GUID/importer/sub-asset（11 用例）；`ExportPackage` 官方 string/string[] overload 与顶级 flags 已按 2022.3.51f1 反射核对，能输出 gzip/ustar `guid/{pathname,asset,asset.meta}`、round-trip、Recurse folder 与 IncludeDependencies（19 用例；IncludeLibraryAssets/Interactive UI 尚缺）；显式及父文件夹继承的 AssetBundle name/variant、all/unused name、scene filter、move/delete 与 meta session persistence 已接通，默认 `BuildAssetBundles` 会将隐式结果写为 bundle/variant/manifest；`AssetBundleBuild.assetBundleVariant` 已修正为官方单 `string` field，`addressableNames` 现按官方 index/空值规则成为 bundle 加载键，legacy `BuildAssetBundleExplicitAssetNames` 双 overload 可实写/回读 bundle+CRC，`GetDirectDependencies`/`GetAllDependencies` 和 writer 的跨 bundle variant-qualified manifest dependency 已接通，output directory 必须预建（20+10+10+10+10+10 用例；仍缺 Unity 2022.3.61f1 A/B 与完整 importer/variant/scene-address/legacy API 语义）；磁盘 ImportAsset/SaveSettings 读取或生成 Unity 格式 32 位 `guid:`，GUID↔path 可反查（4 用例）；Refresh 扫描 `Assets/` 并发现/刷新/撤销外部文件，忽略 meta/Library，且遵守 editing queue（10 用例）；`GetDependencies` 可从 disk asset/.meta 提取 32 位 YAML `guid:` 并稳定解析 direct/recursive 路径、处理循环和异常引用（10 用例）；`GetAssetDependencyHash` 基于 asset/meta/recursive dependencies 的稳定 SHA-256 128-bit 摘要，覆盖 source/meta/direct/transitive/unrelated/cycle（10 用例）；`ImportPackage` 的 started/completed/failed/item 回调、gzip/tar `guid/{pathname,asset,asset.meta}`、CLI/Editor project root staging/backup 落盘、preprocess/postprocess dispatcher、Texture2D/AudioClip/VideoClip/Material importer 主路径，以及 stable importer registry 已接通（registry 10 用例）；Delete/Move/Copy 已同步处理 `Assets/` 内 asset/meta，move 保留 GUID/importer identity、copy 产生新 GUID（11 用例）；可嵌套的 Start/StopAssetEditing 会对 ImportAsset/Refresh 做 path 去重、稳定排序并在最外层 stop 后批量刷新（10 用例）；SaveSettings/SaveAndReimport 会保留既有 YAML，将 AssetBundle name/variant 同步写为 Unity 实际 `*Importer` 块（同存的兼容 payload 以 YAML 为准）并跨 project session 恢复；Texture/Audio 常用 YAML 字段已按本机 Unity 2022.3.51f1 样本读取和原位写回、未知字段保留（本轮 YAML 23 项、既有 persistence 22 项）；缺失字段与基础/Texture/Audio 的其余设置仍在兼容注释 payload；sub-asset 持久序列化、Unity artifact/importer hash A/B、完整 type tree/fileID 语义、跨 asset YAML GUID/fileID rewrite、Unity 原生 importer YAML/platform override、import worker/cache server 与其余公开面仍待完成 |
| `AssetImporter` / `AssetPostprocessor` | 🟡 | `AssetPostprocessor` 已按本机 Unity 2022.3.51f1 反射修正为可构造 class，公开面仅保留四属性、order/version 与四个日志重载；callback 以派生类名称反射分发，支持 private/public instance preprocess、static 4/5 参数 batch、官方 order、上下文/importer 注入与异常事务语义，专项 **17/17**。`AssetImportContext` 已迁入 `UnityEditor.AssetImporters`。ModelImporter 通过 `anity-native` + pinned ufbx `v0.23.0` 实际解码 FBX/OBJ hierarchy、indexed Mesh/submesh、transform/blend/visibility curves、skin/bindpose/weights 与 blend frames；blend-shape/source take/clip/compression suite **42/42**。Transform quaternion/rotation-order/wrap exact-bit 主链已闭环；adjusted/retained 双 scene 对齐 pre/post/pivot/geometric 的静态层级与 raw binding。pivot raw Euler、retained position 各有 10 组 **720/720 float exact-bit**；Pre/Post quaternion 10 组按 MatrixConverter/FbxTime/normalize 达到 **960/960 float exact-bit**。Visibility 已按 Unity 探针对齐静态 enabled、raw/resampled step、compression、祖先/helper propagation 与 additive/override/animated-weight 多层 bake，现代 FBX 使用声明 stack range、旧 6.1 保留实际 curve range；`NativeModelImportTests` **175/175**。常用 Unity YAML/HumanBone/SkeletonBone mapping 已双向持久化。material extraction、weighted FBX SDK exact-bit、layer mute/solo、instanced/其它 axis/unit/rotation-order pre/post、loop/root motion/Mecanim/humanoid、package/cache/type-tree/fileID、完整 settings 与 Unity 2022.3.61f1 A/B 未完成，故不可标为完成。 |
| `PrefabUtility` | ✅ | InstantiatePrefab/IsPrefabAsset/IsPartOfPrefabInstance/IsAnyPrefabInstanceRoot/GetCorrespondingObjectFromSource/ApplyPrefabInstance/RevertPrefabInstance |
| `PrefabStage` / Prefab Mode | ✅ | Isolation/Context、OpenPrefab/Close/Save/MarkDirty、stage 栈、PrefabStageUtility.EnterPrefabMode；Project 双击 .prefab 进入 |
| `SearchService` / Ctrl+K | ✅ | Quick Search：资产/Hierarchy/菜单/设置/窗口 Provider、FuzzyScore、SearchWindow、MenuItem Edit/Search All... _%k |
| `GameView` | ✅ | Display/Aspect/Scale/VSync/Maximize/Mute/Stats、Camera.Render→SRP、LightProbes、RenderTexture 目标 |
| `TextureCompressionUtility` | ✅ | DXT/BC/ETC/ETC2/ASTC/PVRTC 族、平台默认格式(Metal=ASTC,Vulkan=ASTC/ETC2,Desktop=DXT)、块大小/软压缩/IsFormatSupportedOnAPI |
| `PlatformGraphics` | ✅ | iOS Metal / Android Vulkan 主路径、GetPreferredApis、ConfigureIOSMetal/ConfigureAndroidVulkan |
| `HDROutputSettings` / HDR | ✅ | available/active/paperWhiteNits/automaticHDRTonemapping/displayColorGamut/bitDepth、native AnityHDR 路径、HDRUtilities 色调映射 |
| `ColorGamut` / `HDRDisplayBitDepth` | ✅ | sRGB/Rec709/Rec2020/DisplayP3/HDR10/DolbyHDR/HDR10Plus；8/10/16 bit |
| `anity-native` C++ | 🟡 | core/graphics/HDR/physics/audio/media/jobs/texture/transform/math/ui/model importer；model C ABI 已以 pinned ufbx 实际解析 FBX/OBJ hierarchy、mesh/submesh、坐标、transform/blend/visibility animation、skin/bindpose/weights、blend frame/delta 与 scalar tangent/source take。FBX quaternion 已复刻 legacy tick、KFCurve/MatrixConverter float/double 阶段、FbxAMatrix/GetQ、normalize 与 continuity；非 XYZ gimbal、wrap/tie/subframe 已有 exact-bit fixture。adjusted-to-rotation-pivot 静态层级、Mesh/normal/tangent/blend-delta basis 与 retained scene 已接通；pivot raw Euler/retained position 各 **720/720 exact-bit**，Pre/Post 以 native 3×3 `Pre * Local * Post^-1`、M2V、V2VRef 与 float normalize 达到 **960/960 exact-bit**。Visibility ABI 已提供 raw static real、祖先/helper effective product、clip track/key copy、24 帧 resample 与 T-1e-5 raw step；native 层已组合 base/additive/override/override-passthrough、animated layer weight 与 Mute，并复刻 FBX SDK weighted tangent 求值，20 帧与 12 组 Mute/Solo A/B 达到 exact-bit。Canvas native ownership 681/681。material、更多 weighted/broken/auto/TCB/extrapolation 极值、instanced、非 XYZ pre/post、Humanoid、Transform ownership 与更多 solver/importer 仍待 native 化 |
| `_scripts/` 环境与 macOS ARM64 安装 | ✅ | install-env/verify-env/build-native/build-all/gap-audit/install-vulkan/android；新增 `publish-cli.sh/.ps1` 生成 host-matching self-contained CLI、部署 native runtime、隔离 RID lockfile，并在不存在的 `DOTNET_ROOT` 下做进程 smoke；`install-macos-arm64.sh` 同样使用隔离 restore lock，原生 ARM64 Host/CLI/native、签名、batchmode、plist 与图标通过。Windows/Linux 分发实机证据仍按 CLI 行记录为待办 |
| Unity API 官方反射门禁 | ✅ | 84 个官方 2022.3.51f1 UnityEngine/UnityEditor 程序集；类型/成员/参数/枚举/特性对照；当前类型存在 **928/4,117**、精确 **404**，成员存在 **8,645/37,164**、精确 **6,417**；SHA-256 baseline `regressions=0` / `removed-or-changed=0` / load issues=0；`UnityEngine.VFXModule` 公开差异 **0**。仍必须迁移到 2022.3.61f1 后重建最终基线 |
| D3D11 native device | ✅ | D3D11CreateDevice+WARP、swapchain/RTV、Present、HDR R10G10B10A2 |
| Vulkan native device | ✅ | Instance/Physical/Logical device（需 Vulkan SDK） |
| Vulkan URP camera target / opaque / depth copy | 🟡 | 离屏 LDR/HDR resolved color、depth、normal、2/4/8x MSAA resolve、render pass/framebuffer、clear/readback 与逐 slice copy 已接入通用 camera ABI。`Tex2DArray` 真实分配 per-layer color/MSAA/depth/normal view+framebuffer；mesh pipeline 输出 color+world-normal MRT，支持 `_BaseMap`/ST、alpha clip、五种 blend、ZWrite。Vulkan color/depth/normal slice copy 均保持双眼 layer；MoltenVK 的 70/70 门禁覆盖 array、mesh/opaque/depth、BaseMap、ST、blend/cutoff/ZWrite 和十种 world-normal→URP transient 像素。仍缺 normal-map、motion MRT、slice-aware motion copy、CameraTarget source、Android 实机与 D3D11 Windows 像素验证。 |
| D3D11 URP depth-to-color compute | 🟡 | `R24G8_TYPELESS` camera depth 同时建立 D24S8 DSV 与 R24 SRV；single/MSAA sample-0 compute 从 depth SRV 写 RGBA8 UAV 的 R 通道，拒绝 HDR/dimension/self/CameraTarget 不安全路径。源码尚待 Windows SDK 编译及 WARP/硬件像素门禁，不能标为已验证。 |
| SRP culling / draw-command bridge | 🟡 | `ScriptableRenderContext.Cull` 真实快照 active Renderer，应用 enabled/isVisible、camera cullingMask、世界八角 mesh bounds frustum、每层距离与 Object motion-vector 请求；URP 保留 camera-derived culling matrix/mask/origin；`DrawRenderers` 从快照按 layer/renderingLayer/renderQueue 过滤，正确解析 `Material.renderQueue=-1` 的 shader 默认 queue，并记录实际 mesh/material/submesh/transform/bounds 命令。Metal 现将 opaque mesh 和 C++ 四权重/shape `SkinnedMeshRenderer` 形变提交到 native raster；skinned `sharedMesh` 初始化 local bounds，显式 skinned local bounds 会优先经过八角 world transform 参与 frustum culling；native skin stream 可用时 Cull 从当前 deformation 重建动态 AABB，10 组静态与 10 组动态边界门禁通过。透明、8-weight/GPU skinning、阴影、材质语义和 Vulkan/D3D11 仍未完成。 |
| Native camera mesh raster ABI | 🟡 | `AnityGraphics_DrawCameraMesh` / C# pinned bridge 明确区分离屏 target 与 CameraTarget：Metal 真实上传 packed indexed current/previous position、normal/tangent/UV/color vertex/index buffers，在两类 color/depth attachment 执行 indexed triangle raster、depth test/write 和 MSAA resolve；C# ABI 转置 Unity matrix fields。离屏 `Tex2DArray` 现真实创建 Metal array/MSAA-array color/depth/normal/motion attachments，pass/draw 的 `depthSlice` 绑定各 attachment/resolve layer；color/depth/normal/motion slice-copy ABI 都会从指定 source layer 写到指定 destination layer，其中 depth 的 single/MSAA array compute 读取与写入指定 slice。`depthSliceCount=2` 和 `stereoInstanceCount=2` 现在真实驱动 Metal `renderTargetArrayLength`、`instance_id` eye matrix 与 `render_target_array_index`；一条 camera pass、一次 indexed draw 可分写两只眼，10 组 1x/2x 像素门禁确认交叉 layer 没有串写。交换链亦拥有 `RGBA8Snorm` normal 与 `RG16Float` motion resolve attachment；`DrawRenderers` 无论是否设 `Camera.targetTexture` 都提交。ABI 现传递 `_BaseMap`、`_BumpMap`、opaque/alpha/premultiplied/additive/multiply、ZWrite、alpha cutoff；无 `_BaseMap` 时 native entry 显式初始化 Unity-white fallback。Metal 实际采样 texture registry 后配置 blend/depth/fragment discard；normal map 以 Linear TBN 转 world-space normal，并对 non-uniform / mirrored transform 使用 inverse-transpose normal、object-direction tangent、正交化与 odd-negative-scale handedness，透明队列按相机距离 back-to-front。`Mesh` 的 Unity 2022 variable-influence API（`BoneWeight1`、per-vertex count、all weights）已直接进入 C++ skin kernel，支持最多 8 influence 并按 `SkinQuality`/`QualitySettings` 选择 1/2/4/8 后归一；20 项 native 门禁通过。blendshape frame API 和 `SkinnedMeshRenderer` weight API 已接入；frame interpolation/extrapolation 后的 multi-shape position/normal/tangent delta 由 C++ `AnityGraphics_ApplyBlendShapeDeltas` 合成再进入 skinning，`BakeMesh` 真实输出形变 geometry；previous skinned positions 在 renderer 的所有材质 pass 后提交，能写出骨骼与 shape deformation velocity。Metal HDR 现逐层把双眼 `Tex2DArray` 建为 2D view 后执行 final grade，新增 tone-mapped slice readback；10 组 native-required 双色/不同强度像素门禁确认两层均完成 grade。ST、Shader Graph 材质、透明 normal/motion、GPU skinning、XR provider/display、Vulkan/D3D11 single-pass 仍未完成。 |
| Native camera motion vectors | 🟡 | Metal `RG16Float` motion MRT（MSAA resolve）在离屏及 CameraTarget 均可用；mesh ABI 同时带 raster `objectToClip`、velocity 专用 `motionObjectToClip`、previous object-to-clip 与 per-vertex previous position，vertex stage 真实写 `(currentNonJitteredNdc - previousNonJitteredNdc) * 0.5`。URP 保持 `Camera.projectionMatrix` raster，并以新增 `Camera.nonJitteredProjectionMatrix` 保存 current/previous motion history，避免 TAA jitter 伪写速度；`ResetProjectionMatrix` 也会解除 non-jittered override。`Camera.useJitteredProjectionMatrixForTransparentRendering` 默认使透明 raster 用 jittered VP，关闭时实际切到 non-jittered VP，且 `CopyFrom` 保留设置。`Camera` 已有 Unity 2022 per-eye projection/non-jittered/view get/set/reset、`StereoTargetEyeMask` 与 off-axis eye-separation fallback；URP 在 Metal 双眼 `Tex2DArray` 上现一次 camera pass 同时下发 left/right raster 与 non-jittered motion matrices，native instance 0/1 分别写 array layer 0/1，history 仍按 `(instance, eye slice)` 独立存储；culling 分别测试左右 frustum 后按 stable renderer instance-id 取并集，防止右眼独有对象缺失。其他 backend/target 保持 left/right multipass。single-pass transient motion/opaque/depth/normal 均按两个 slice 分配与复制。托管端保存 per-renderer local-to-world 及启用 `skinnedMotionVectors` 时的 renderer-local skinning history；first frame 归零，同 renderer 的 history 在其所有材质 pass 后才更新，camera/renderer/skinned history 均有 **4096** 项上限。`writeMotionVectors` C ABI 使 opaque/alpha-clip 写 motion，而透明 forward draw 保持 color blend 却禁写 motion MRT，符合 Unity 2022 URP 的 built-in opaque-only 契约。`CameraMotionVectorsTexturePass` 发布 `RGHalf` / `R16G16_SFloat` `_MotionVectorTexture` 并在 cleanup 释放。10 项 raster jitter native 像素、10 项 projection override/reset、10 项 transparent-raster matrix、10 项 stereo matrix override/reset、10 项 history boundary、10 项 transparent-preserve-opaque-motion、10 项 array eye-slice/opaque-slice copy 读回、10 项 URP multipass scheduling、10 项 single-pass scheduling、10 项 single-pass union-frustum、10 项 per-eye history、skinned vertex deformation、对象矩阵 motion 和 four-weight skinning 门禁均通过。GPU deformation、XR provider/display、Vulkan/D3D11 single-pass 和官方 A/B 仍缺。 |
| URP renderer queue / camera stack / PostProcessPass | 🟡 | Bloom/Tonemap/ColorAdjustments/WhiteBalance/ChannelMixer/ColorCurves Volume 已绑定最终 native HDR pass；Metal 对 CameraTarget 和 `Camera.targetTexture` 执行真实 clear/load/store、MSAA resolve，以及至多 **8** 层 `RGBA16Float` 私有 Bloom 金字塔。Bloom 的 `maxIterations`、half/quarter `downscale`、`highQualityFiltering`、`scatter`、intensity、RGB tint 和 `Texture2D` Lens Dirt / `dirtIntensity` 均进入 Metal 执行；Dirt 通过 device-owned texture registry 与 sampler 绑定，缺失或失效时安全退化为普通 Bloom。GPU texture registry 现把 `Texture.mipMapBias` 与 QualitySettings 解析后的 aniso 1–16 贯通：D3D11 `MipLODBias`/anisotropic filter，Vulkan feature-gated `mipLodBias`/anisotropy，Metal `maxAnisotropy` 与 UI fragment 显式 `sample(..., bias(...))`；Point filter 在所有三条后端路径禁用各向异性，Metal `MirrorOnce` 用 MirrorClampToEdge，Vulkan 在 mirror-clamp 扩展可用时同等启用，缓存会在任一采样状态改变时重传。Metal UI GPU readback 已以负 UV 确认 Clamp/red 与 MirrorOnce/green 的可观察差异。Metal HDR Lens Dirt compute 同样从 registry entry 带入 mip bias 并以 `sample(..., bias(...))` 执行；红色 base mip / 绿色 mip 1 的 native GPU 像素门禁验证 `+1` 实际选择下一粗 mip。CPU `AnityHDR_ProcessFrame` 也实现相同的 unexposed HDR prefilter、mip/box filter、scatter/tint，并通过 `AnityHDR_ProcessFrameWithLensDirtRGBA8MipsBias` 接收完整 `Texture2D` mip chain、Point/Bilinear/Trilinear、Repeat/Clamp/Mirror/MirrorOnce、linear、mipMapBias 元数据；其拒绝截断和越过 1×1 尾级的输入，Bilinear 使用导数+mip bias 选 mip、Trilinear 混合相邻 mip，按同一 Dirt 合成顺序执行，四组 CPU↔Metal fixture 误差 ≤2/255。`ScriptableRenderPassInput` 的 `None/Depth/Normal/Color/Motion`、`ConfigureInput` 和 per-camera 聚合已接通；其中 `Color` 会在 `AfterRenderingOpaques` 创建 transient single-sample target，并经 Metal GPU resolved-color blit 发布真实 `_CameraOpaqueTexture`，失败不会发布空资源，cleanup 会解除绑定和释放；`Depth` 同时把 shader-read `Depth32Float` 通过 single/MSAA compute 写入 R 通道并发布 `_CameraDepthTexture`，同样有 failure-safe cleanup。Vulkan 已有离屏 camera target registry（LDR/HDR resolved color、depth、可选 2/4/8x MSAA resolve、render pass/framebuffer）、真实 pass clear/readback 和 resolved offscreen `vkCmdCopyImage` opaque copy；swapchain CameraTarget 与 depth-to-color 尚明确未支持，且这台机器没有 Vulkan SDK/NDK，待 Android 真实 Vulkan 编译与像素门禁。两条 Metal transient 各有 10 项 native-required 门禁覆盖 CameraTarget/RenderTexture、MSAA、format/dimension/self/release 拒绝和 pass cleanup。final post 依次执行白平衡、Color Filter、Channel Mixer、八条 128-sample baked curves、contrast、Hue Shift、saturation、non-negative clamp、ACES/Neutral。native-required 像素门禁为 **220/220**，加 URP stack/lifecycle 为 **255**；本次 sampler registry **26/26**、跨 backend UI texture **61/61**、Metal UI sampling **14/14**。**未完成**：normals/motion 仍未生成同帧可采样 native attachment 或 global binding；D3D11 depth copy、Vulkan depth-to-color/CameraTarget copy、Unity Player anisotropic A/B、Vulkan/D3D11 实机多 mip/Dirt 像素门禁、Lens Dirt 各向异性、非 Texture2D Dirt、HDR10、resource graph、XR、deferred/GBuffer、阴影/光照及 Unity 2022.3.61f1 Player A/B；本机仅有 2022.3.51f1 与 Unity 6，不能标完整 URP 14.x。 |
| Native 热路径 | 🟡 | CCD TOI / 2D SAT / ConstantForce 3D/2D / Audio decode / Texture compress / Transform 仿射矩阵链 / Matrix4x4 / Canvas vertex staging、bounds、quad indices、clip-alpha visibility / FBX-OBJ parse、triangulation、index generation、animation bake、skin/bindpose/weight、blend-shape/visibility decode、raw/resampled scalar key 与 tangent 走 anity-native；material、Transform raw layer 全语义、Humanoid、Transform state、物理世界与剩余资源导入所有权仍待 native 化 |
| `Display` | ✅ | multi-display、Activate、RelativeMouseAt、HDR 探测扩展 |
| `XRDisplaySubsystem` / `XRSettings` | 🟡 | provider 已实际配置/复用 `Tex2DArray` 双眼 target 并绑定 Camera；单一 `XRRenderPass` 发布左右 projection/view、slice 0/1 与 culling pass，直接驱动已有 Metal URP single-pass instanced。`XRRenderPass.renderTarget` 现为 Unity-compatible `RenderTargetIdentifier`，provider 实现 `GetRenderPass(int, out ...)`、`GetRenderParameter(Camera, int, out ...)` 与 `GetCullingParameters(Camera, int, out ...)`；后者输出 stereo culling matrix/origin，并由 URP `RenderCamera` 实际作为 provider target 的初始 culling 参数，single-pass 仍合并右眼 frustum。`XRSettings` 提供设备装载/启停与 scale facade；provider dynamic-resolution multiplier 受 0.5–1.5 bounds 约束、参与最终尺寸且只在实际变化时重建双眼 target，并设置 Unity-compatible `RenderTextureDescriptor.useDynamicScale` / target 状态。`AttachOverlayCamera` 让 overlay 与 base 共用同一 array target、设置 Both 并注册 URP stack，因此 overlay 继承同一双眼 layer 而不清 color。URP feature camera data 现保留真实外部 target descriptor（array layer、TwoEyes、MSAA、dynamic scale），即使 provider 强制逐眼 multipass。provider-owned target 会使 `singlePassRenderingDisabled` 真正切换 Metal URP 为逐眼 multipass，恢复时才允许 native instanced path；普通 array target 不受该 provider 状态影响。multipass 左眼不执行 final post-process，右眼完成两层合成后只调用一次 native array HDR grade。32 项 frame-layout/scale/overlay/lifecycle/非法参数与 10 项 native 调度/最终处理/descriptor/render-pass-culling 门禁覆盖。尚缺原生 HMD runtime、multiview、输入 subsystem、Android/iOS/Windows Player 与 Unity 2022.3.61f1 A/B。 |
| `ColorSpacePipeline` | ✅ | Linear/Gamma 转换、ConfigureURPLinearHDR |
| `ScreenCapture` | ✅ | CaptureScreenshot/AsTexture/IntoRenderTexture、superSize、StereoMode、真 PNG；测试≥12 |
| `Il2CppBuilder` / IL2CPP 管线 | ✅ | CodeGeneration/CompilerConfig/stripping、.cpp stub、link.xml、AOT 注册；测试≥14 |
| `anity.exe` CLI | 🟡 | batchmode/quit/projectPath/executeMethod/build*/runTests + il2cpp/screenshot/agent 主路径已具备；`-logFile -` 已按 Unity 2022.3 官方语义写入并 flush stdout，不创建 dash 文件，普通路径及 version/help/error 早退均会最终落盘。独立 host-matching self-contained 分发已接入 native runtime、atomic publish、RID/输出边界与 staging lock 隔离；macOS ARM64 产物在不存在的 `DOTNET_ROOT` / 无工具 PATH 下直接运行，Mach-O/PE/ELF 架构、hostfxr、runtimeconfig、native 库、日志/错误/测试 XML 等新增 13 项真实进程门禁。CLI **40/40**、统一矩阵 **4197/4197**；Windows `anity.exe` 与 Linux 产物尚未实机执行，默认日志、`-nolog`/`-upmLogFile`、完整 Unity 2022 Editor/Player 参数、退出码、崩溃/许可/UPM 日志及 2022.3.61f1 平台 A/B 仍缺，故保持 🟡 |
| `Anity.Agent` 官方扩展 | 🟡 | 独立包 Session/Memory/Tools；自定义API Key/Base URL/model、SSE、tool calling、Editor窗口与OS vault已实现。0.6.0新增工具Requested/8类终态审计、默认fail-closed、无原文digest、64 KiB结果上限、跨轮finish reason/usage保存，以及Editor项目级有界SHA-256链/轮换/启动验证/独占lock/Unix私有权限。Agent **91/91**、Editor Host **39/39**、CLI **16/16**；统一Release矩阵强制native并达到 **2,548/2,548**、0失败、0跳过。审计HMAC/外部anchor、持久session、完整JSON Schema、Windows/Linux vault实机、Responses API、真实多厂商及网络矩阵仍缺，故不能标全完成 |
| `Canvas` Overlay/Camera/World | ✅ | 官方根命名空间与公开面差异 0；pixelRect、planeDistance、worldCamera、rootCanvas、排序、根布局 size/scale/position；Canvas/utility/raycaster 定向 43 测试 |
| `CanvasScaler` 三模式 | ✅ | ConstantPixel/ScaleWithScreen(Match/Expand/Shrink)/Physical；UIBehaviour override 修复 |
| `Job System` 深度 | ✅ | ThreadPool 并行、依赖 Complete、Combine、JobsUtility；测试≥13 |
| `Il2CppApi` 深度 | ✅ | icall/pinvoke/method pointer、Invoke、Strip preserve、Builder 集成 |
| `Undo` | ✅ | RecordObject/RecordObjects/DestroyObjectImmediate、Stack&lt;UndoCommand&gt;记录、PerformUndo/PerformRedo、undoRedoPerformed事件 |
| `MenuItem` / `MenuCommand` / `ContextMenu` / `ContextMenuItem` / `AddComponentMenu` | ✅ | Attribute类、menuName/validate/priority/context |
| `GenericMenu` | ✅ | AddItem/AddDisabledItem/AddSeparator/ShowAsContext/DropDown、内部MenuItemData列表 |
| `SerializedObject` / `SerializedProperty` | ✅ | targetObject/targets/ApplyModifiedProperties/FindProperty/Update、反射访问字段、intValue/floatValue/boolValue/stringValue/colorValue/vector2Value/vector3Value/enumValue/objectReferenceValue/arraySize等 |
| `Editor` | ✅ | target/targets/serializedObject/Repaint、OnInspectorGUI/OnSceneGUI/CreateEditor/DrawDefaultInspector |
| `BuildPipeline` | 🟡 | BuildPlayer(BuildPlayerOptions)、BuildTarget 枚举/映射、平台扩展名、BuildReport/BuildSummary 主路径已实现；AssetBundle build-map 已覆盖 variant、addressable name、依赖、预建输出目录，legacy `BuildAssetBundleExplicitAssetNames`/`BuildAssetBundle` 均可实写回读并支持 CRC（当前聚焦回归 235/235）。仍缺 BuildAssetBundlesParameters、streamed scene、若干 legacy utility 与 Unity 2022.3.61f1 Pro A/B，故不得标为完整。 |
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
| `Native Swapchain` | ✅ | Create/Acquire/Present；Vulkan Win32/Android/X11/Wayland surface + headless ring；Metal CAMetalLayer/EDR；PresentCount/HasNativeSurface/BackendKind/SurfaceKind；1/2/4/8x MSAA capability validation，非法 3x 在 C++ 通用入口拒绝，原生创建错误不会降级成托管假成功；测试≥18+surface |
| `Vulkan surface platforms` | ✅ | HWND / ANativeWindow / AnityX11NativeWindow / AnityWaylandNativeWindow；GetSupportedSurfaceMask；PlatformGraphics 映射 |
| `InternalEditorUtility` | ✅ | **完整UnityEditorInternal API**：inBatchMode/isHumanControllable/isApplicationActive/hasProLicense/unityVersion/isProSkin/unityPreferencesFolder/projectPath、tags/layers/sortingLayerNames/sortingLayerUniqueIDs/asmrefGUIDs/assemblyNames、ReloadAssemblies/RequestScriptReload/IsRecompiling、OpenFileAtLineExternal、LoadRequiredAdditionalDataToWindow、LoadWindowLayout、GetAllGlobalTags/GetAllLayers/TagToLayer/LayerToTag、IsNativeModule/GetScriptAssemblies/GetEditorScriptAssemblies/GetRuntimeScriptAssemblies/GetAssemblyPath/GetAssemblies、IsInEditor/IsInPlayer/GetPlatformDefines/GetDefinesForAssembly/GetPredefinedDefines、RepaintAll/SetDirty/IsObjectAManagedReference、GetSerializedObjectProperties/GetActiveSceneName/GetOpenScenes/IsSceneSaved/GetSceneAssetPath、FindAssets/GetAssetPath/GUIDToAssetPath/AssetPathToGUID、CalculateBounds真实Renderer包围盒计算、SetIconForObject/GetIconForObject图标管理、scriptReloaded事件 |
| `BuildCallbacks` | ✅ | 接口定义 |
| `EditorSettings` | ✅ | Dictionary存储、serializationMode、defaultBehaviorMode、enterPlayModeOptions、spritePackerMode、asyncShaderCompilation、cacheServer配置、projectGenerationRootNamespace、DefineSymbols等完整属性 |
| `ProjectSettings` | ✅ | Dictionary存储、productName/companyName/applicationIdentifier、**runInBackground**、defaultScreenOrientation/Width/Height、scriptingBackend(Mono/IL2CPP)、apiCompatibilityLevel、strippingLevel(Low/Medium/High/Disabled)、vSyncCount、targetFrameRate、colorSpace(Gamma/Linear)、graphicsJobs、iOS/android配置等完整属性 |
| `Lightmapping` | ✅ | 见UnityEngine.LightModule |
| `TextureImporter/ModelImporter/AudioImporter` | 🟡 | 见 AssetImporter；ModelImporter 已进入 native FBX/OBJ hierarchy/indexed mesh/transform + blend-shape/visibility animation/skin/bindpose/weights 主链，支持 source take/frame-range、多 clip、raw/resampled tangent 与 Unity compression/切片顺序。Transform source curve、坐标、同步 quaternion reduction、六种 rotation order、非 XYZ gimbal 与 wrap/tie/subframe exact-bit fixture 已闭环。pre/post、rotation/scaling pivot/offset、geometric transform 的 adjusted/retained 双路径已覆盖；10 组 pivot raw Euler 与 retained position 各达到 **720/720 float exact-bit**，Pre/Post quaternion 达到 **960/960 float exact-bit**。`Renderer.m_Enabled` 已覆盖 static/import-off/raw step/resample/compression/runtime、祖先/helper topology 数值乘积与 additive/override/animated-weight 多层 bake；weighted layer 已复刻 FBX SDK `9999.0f` 权重解码、float secant/De Casteljau、source derivative 与 double layer accumulator，20 帧 exact-bit，并覆盖 12 组 Mute/Solo 导入组合；`NativeModelImportTests` **206/206**。其它 axis/unit/rotation-order/pivot topology、instanced/非 Mesh Renderer、material、更多 weighted/broken/auto/TCB/extrapolation 极值、loop/root motion/additive/mask/humanoid、完整 YAML/import worker/package/cache 行为仍未闭环，故 ModelImporter 继续 🟡。 |
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
9. 🟡 Editor 主路径：EditorApplication/EditorWindow/EditorGUI/EditorGUILayout/GUIStyle/Handles/SceneView/Selection/AssetDatabase/PrefabUtility/Undo/MenuItem/GenericMenu/SerializedObject/Editor/BuildPipeline/PlayerSettings/EditorBuildSettings/EditorSceneManager/EditorUtility/EditorPrefs/CompilationPipeline/PackageManager/SettingsProvider/GUILayout/Event；`UnityEditor.AnimatedValues` 的 Base/NonAlloc/Bool/Float/Vector3/Quaternion 已实现真实 Editor update 插值并经 10 用例与本机反射验证；`AnimationMode`/curve binding/property modification/driver 的预览采样会话与停止恢复也已实现并经 11 用例和本机反射验证；`AnimationUtility` 的曲线/剪辑/事件/对象引用/切线/绑定主数据路径已实现并经 10 用例验证，且对本机 Unity 2022.3.51f1 的预备反射对照无该模块差异；正式 `UnityEditor.Animations` Controller 图、BlendTree 和 GameObjectRecorder 已实现，34 个定向用例通过且对本机 2022.3.51f1 预备反射无该命名空间差异；`MonoScript` 现按官方继承 `TextAsset` 并可从 MonoBehaviour/ScriptableObject 反查托管 Type，12 个用例与 2022.3.51f1 预备反射均通过；`ActiveEditorTracker`/`DataMode` 已进入 Inspector 的 selection/locked/dirty/rebuild/visibility/unsaved 状态主路径，13 个用例及本机反射通过；`AssemblyReloadEvents` 现驱动 InternalEditorUtility/EditorUtility 的 before→script→after 生命周期，10 个用例与本机反射通过；Animator Window 现有真实 transition/condition/默认状态/layer 图编辑，并支持画布节点/边命中、state 拖拽、删除所选 transition、child state-machine 双击导航与层级返回；新增 12 个用例、窗口图 suite 达 25 项，连同 Controller/绑定为 47 项聚焦回归。框选、多选、连线创建、完整 transition Inspector、Undo/资产序列化、运行时逐帧语义与 Unity 2022.3.61f1 Pro A/B 仍待补齐；macOS app-bundle Host 已将 Unity `-batchmode` 等 CLI switches 转发至唯一 CLI 实现，仍需官方反射、交互与平台产物全量门禁
10. ✅ Anity特有：HotUpdateContext/Il2CppRuntime/PlatformConfig/Gizmos/ComputeBuffer/AsyncGPUReadback/LODGroup/ShaderVariantCollection/Addressables/Touch输入
11. ✅ UIElements全量：VisualElement/ScrollView/Scroller/Button/Label/TextField/Toggle/Slider/DropdownField/ListView/TreeView/Foldout/ProgressBar/HelpBox/EnumField/MinMaxSlider/BoundsFields/TagLayerFields/Toolbar/TabView/RadioButton完整控件系统
12. 🟡 动画 Avatar：HumanDescription/HumanBone/HumanLimit/SkeletonBone、Avatar/AvatarBuilder 及 AvatarMask/AvatarMaskBodyPart 的 Unity 2022.3.51f1 预备反射公开面已精确；Human/Generic AvatarBuilder 已接 `anity-native` hierarchy/rest-pose/mapping/required-bone/root-motion-name validation（23 项）；AvatarMask native body/path 状态（17 项）已进入 generic Animator layer 的 native Override/reference-pose Additive、weight、crossfade 与 exact-path mask 合成（25 项）；HumanBone/SkeletonBone、motionNodeName/sourceAvatar 主路径已双向读写，错误 `RawAvatar` 已移除。ModelImporter decoded hierarchy/additive asset、Humanoid muscle/finger/IK mask、T-pose/humanScale/retargeting/root-motion rotation与 2022.3.61f1 A/B 仍未全量闭环
13. ✅ MeshData全量：MeshData/MeshDataArray/VertexAttribute/VertexAttributeFormat/VertexAttributeDescriptor/SubmeshDescriptor/MeshUpdateFlags/MeshUtility完整高性能网格API
14. ✅ 编辑器内部API：InternalEditorUtility完整标签/层/程序集/资源/图标/包围盒管理
15. ✅ WebGL视频：WebGLVideo/WebGLApplication完整视频播放管理
16. ✅ Lightmap光照设置：LightmapSettings/LightmapParameters/AmbientMode完整光照配置
17. ✅ Profiler完整层级：Profiler/ProfilerUnsafeUtility/ProfilerRecorder/ProfilerMarker/ProfilerArea/ProfilerCategory全层级性能分析API
