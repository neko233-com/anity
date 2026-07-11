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
| `Object` | ✅ | 全局对象HashSet、自增InstanceID、Instantiate(GameObject深克隆)、Destroy延迟队列、FindObjectOfType/FindObjectsOfType |
| `GameObject` | ✅ | 真正List&lt;Component&gt;管理、AddComponent/GetComponent、SetActive递归OnEnable/OnDisable、activeInHierarchy、SendMessage反射、构造自动加Transform |
| `Component` | ✅ | gameObject/transform缓存、所有GetComponent*转发、CompareTag |
| `Behaviour` | ✅ | enabled触发OnEnable/OnDisable、isActiveAndEnabled |
| `MonoBehaviour` | ✅ | Invoke/InvokeRepeating延迟列表、StartCoroutine/StopCoroutine、所有生命周期虚方法 |
| `Transform` | ✅ | List&lt;Transform&gt;父子树、localPosition/Rotation/Scale与world转换、TRS矩阵、Translate/Rotate/LookAt真实矩阵、Find/SiblingIndex |
| `Vector2/3/4` | ✅ | 完整数学运算、==/!=运算符、隐式转换、常量（含back）、Lerp/Dot/sqrMagnitude |
| `Quaternion` | ✅ | 基本运算、LookRotation（旋转矩阵转四元数）、Slerp 等 |
| `Matrix4x4` | ✅ | 乘法、逆、透视、转置、行列式 |
| `Bounds` | ✅ | 完整包围盒数学 |
| `Ray` | ✅ | 射线定义 |
| `Rect` | ✅ | xMin/xMax/yMin/yMax/center/min/max、Contains/Overlaps/Expand/Encapsulate几何 |
| `Plane` | ✅ | normal/distance、三点构造、Raycast射线相交、GetDistanceToPoint、SameSide/Flip/Translate |
| `Color/Color32` | ✅ | 基本运算、*float 运算符、转换 |
| `Mathf` | ✅ | 常用数学函数（含SmoothDamp） |
| `Time` | ✅ | Stopwatch真实时间、Tick方法、timeScale缩放、fixedDeltaTime |
| `Application` | ✅ | 进程信息(PID/isPlaying/isFocused/isPaused/isBatchMode)、真实平台路径(dataPath/persistentDataPath/streamingAssetsPath/temporaryCachePath)、RuntimePlatform自动检测(OS/Arch)、unityVersion="2022.3.61f1"、identifier/companyName/productName/version/buildGUID与PlayerSettings同步、systemLanguage/internetReachability/runInBackground/targetFrameRate/sleepTimeout、Quit/CancelQuit/OpenURL事件流、OnFocus/OnPause回调 |
| `Debug` | ✅ | ILogger/ILogHandler接口、ConsoleLogHandler、Log/Warning/Error/LogException/Assert、LogFormat |
| `Input` | ✅ | HashSet存储key/button状态、GetKey/Button/axis、SimulateKeyDown测试API |
| `LayerMask` | ✅ | NameToLayer/LayerToName字典、GetMask位运算、隐式int转换、内置层 |
| `PlayerPrefs` | ✅ | Dictionary存储int/float/string、Set/Get/HasKey/Delete/Save、默认值参数 |
| `Random` | ✅ | System.Random封装、Range/insideUnitSphere/insideUnitCircle/onUnitSphere/rotation/ColorHSV |
| `Resources` | ✅ | Dictionary资源存储、Load/LoadAll/FindObjectsOfTypeAll、UnloadAsset/UnloadUnusedAssets、LoadAsync |
| `JsonUtility` | ✅ | System.Text.Json序列化/反序列化、FromJsonOverwrite反射覆盖 |
| `Screen` | ✅ | width/height/dpi/orientation/fullScreen/safeArea/brightness/resolutions、SetResolution |
| `SystemInfo` | ✅ | 设备/OS/CPU/GPU信息(deviceName/model/type/uniqueId/operatingSystem/processorCount/frequency/systemMemorySize)、graphicsDeviceType支持D3D11/D3D12/Vulkan/Metal/OpenGLCore/OpenGLES2/OpenGLES3/WebGL2、graphicsDeviceVersion自动匹配、supports*系列特性、graphicsShaderLevel=50、IsFormatSupported、overrideGraphicsDeviceType支持构建切换 |
| `GL` | ✅ | 矩阵栈Push/Pop/MultMatrix、Translate/Rotate/Scale（真实旋转矩阵）、Begin/End立即模式、Vertex/Color/TexCoord |
| `AsyncOperation` | ✅ | 继承CustomYieldInstruction、isDone/progress/allowSceneActivation、completed事件 |
| `ScriptableObject` | ✅ | CreateInstance&lt;T&gt;/CreateInstance(Type) |
| `RectTransform` | ✅ | anchoredPosition/sizeDelta/anchor/pivot完整布局计算、GetWorldCorners矩阵变换 |

---

## 2. UnityEngine.PhysicsModule（3D 物理）

| 类型 | 状态 | 备注 |
|------|------|------|
| `Rigidbody` | ✅ | 速度/角速度积分、质量/阻尼/约束、4种AddForce模式、AddTorque/AddExplosionForce、Sleep/WakeUp |
| `Collider` | ✅ | attachedRigidbody、isTrigger、PhysicMaterial、bounds、ClosestPoint/Raycast、OnCollision/OnTrigger事件 |
| `BoxCollider` | ✅ | center/size、世界空间AABB |
| `SphereCollider` | ✅ | center/radius、世界空间AABB |
| `CapsuleCollider` | ✅ | center/radius/height/direction、世界空间AABB |
| `MeshCollider` | ✅ | 基础形状、世界空间AABB |
| `CharacterController` | ✅ | SimpleMove重力+速度、Move分步碰撞检测+CollisionFlags、isGrounded |
| `WheelCollider` | ✅ | 悬挂弹簧阻尼、frictionCurve、motor/brake/steer、rpm、GetGroundHit |
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
| `Rigidbody2D` | ✅ | bodyType、drag、重力、AddForce/Torque、MovePosition/Rotation |
| `Collider2D` | ✅ | isTrigger、IsTouching、GetShape 抽象 |
| `BoxCollider2D` | ✅ | 形状实现 |
| `CircleCollider2D` | ✅ | 形状实现 |
| `CapsuleCollider2D` | ✅ | direction(H/V)、size/radius/height、胶囊=中心矩形+两端半圆 |
| `EdgeCollider2D` | ✅ | points(Vector2[]数组)、点到线段距离检测 |
| `PolygonCollider2D` | ✅ | paths(List&lt;Vector2[]&gt;)、SetPath/GetPath、射线法点在多边形内检测 |
| `CompositeCollider2D` | 🟡 | 基本实现Stub |
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
| `Canvas` | ✅ | isRootCanvas、renderTransform、renderOrder、willRenderCanvases事件触发CanvasUpdateRegistry.PerformUpdate |
| `CanvasScaler` | ✅ | uiScaleMode(ConstantPixel/ScaleWithScreen/ConstantPhysical)、referenceResolution、screenMatchMode、matchWidthOrHeight、scaleFactor计算 |
| `CanvasRenderer` | ✅ | SetMaterial/SetMesh/SetVertices、cull、materialCount、SetAlphaTexture |
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
| `RectTransform` | ✅ | 见CoreModule |
| `LayoutGroup` / `Horizontal/Vertical/Grid` | ✅ | Horizontal/VerticalLayoutGroup: CalculateLayoutInput遍历子ILayoutElement、SetLayoutHorizontal/Vertical设置anchoredPosition; GridLayoutGroup: cellSize/spacing/startCorner/constraint |
| `LayoutElement` | ✅ | minWidth/preferredWidth/flexibleWidth等布局属性 |
| `ContentSizeFitter` / `AspectRatioFitter` | ✅ | ContentSizeFitter: horizontalFit/verticalFit(Unconstrained/Min/Preferred)、驱动RectTransform尺寸; AspectRatioFitter: aspectMode/ratio、UpdateRect调整 |
| `EventSystem` | ✅ | 事件分发、RaycastAll、current选中对象、sendPointerEvents/sendUpdateEvents、firstSelected |
| `BaseInputModule` / `StandaloneInputModule` | ✅ | 真正事件分发：Process处理鼠标按下/移动/释放/拖拽/滚动，ProcessTouchPress、HandlePointerExitAndEnter、事件发送到GameObject |
| `PointerEventData` | ✅ | pointerId/position/delta/button/clickCount/enterEvent/hovered/pointerDrag/pointerPressRaycast等完整字段 |
| `ExecuteEvents` | ✅ | Execute/ExecuteHierarchy、EventFunction&lt;T&gt;、所有事件接口handler（IPointerClick/Down/Up/Enter/Exit/Submit/BeginDrag/Drag/EndDrag/Scroll/Move等） |
| `GraphicRaycaster` | ✅ | Raycast用RectTransformUtility.RectangleContainsScreenPoint检测、blockingObjects、sortOrderPriority |
| `RectTransformUtility` | ✅ | ScreenPointToLocalPointInRectangle、RectangleContainsScreenPoint、PixelAdjustPoint/Rect、WorldToScreenPoint、FlipLayoutOnAxis/Axes |
| `CanvasGroup` | ✅ | alpha/interactable/blocksRaycasts/ignoreParentGroups |
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
| `ListView/TreeView/TreeViewController` | 🟡 | 基本结构Stub |
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
| `CanvasRenderer` (底层) | ✅ | 见 UI |

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
| `ScriptableRenderContext` | ✅ | ExecuteCommandBuffer、DrawRenderers、Cull、Submit、SetupCameraProperties |
| `CommandBuffer` | ✅ | DrawRenderer/DrawMesh、SetRenderTarget/ClearRenderTarget、SetGlobalFloat/Int/Vector/Color/Matrix/Texture、Blit、SetViewport/Scissor、GetTemporaryRT |
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
| `ReflectionProbe` | ✅ | type/mode/importance/intensity/boxProjection/clearFlags/backgroundColor |
| `RenderSettings` | ✅ | fog/fogColor/fogMode/fogDensity、ambientMode/ambientLight、skybox、reflectionBounces/defaultReflectionMode |
| `ComputeShader` | ✅ | 基础ComputeShader |
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
| `Avatar` / `AvatarMask` | 🟡 | Stub |
| `HumanBodyBones` | ✅ | 枚举 |
| `Animation`（Legacy） | ✅ | 继承Behaviour、clip/wrapMode/playAutomatically、Play/CrossFade/Stop/Rewind/Sample/IsPlaying、AnimationState time/speed/weight |
| `AnimationState` | ✅ | name/clip/weight/speed/wrapMode/time/normalizedTime/layer/blendMode/enabled、AddMixingTransform |

---

## 8. UnityEngine.AudioModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `AudioClip` | ✅ | length/samples/channels/frequency、GetData/SetData、Create |
| `AudioSource` | ✅ | clip/volume/pitch/panStereo/spatialBlend、Play/PlayDelayed/PlayOneShot/Stop/Pause/UnPause、isPlaying、内部_time进度 |
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
| `Texture` | ✅ | width/height/format/filterMode/wrapMode |
| `Texture2D` | ✅ | Color[]像素/GetPixel/SetPixel/Apply/ReadPixels/LoadImage/EncodeToPNG |
| `RenderTexture` | ✅ | GetTemporary/ReleaseTemporary |
| `Cubemap` | ✅ | GetPixel/SetPixel |
| `TextureFormat` / `RenderTextureFormat` | ✅ | 枚举 |
| `ImageConversion` | ✅ | EncodeToPNG/JPG/TGA、LoadImage 扩展 |

---

## 11. UnityEngine.MeshModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `Mesh` | ✅ | vertices/normals/tangents/uv/colors/triangles、SetVertices/SetTriangles、RecalculateBounds/RecalculateNormals、CombineMeshes、subMeshCount |
| `BoneWeight` | ✅ | 骨骼权重结构 |
| `MeshTopology` | ✅ | 枚举 |
| `MeshData/MeshDataArray` | 🟡 | Stub |

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
| `LODGroup/LOD/OcclusionArea/OcclusionPortal` | ✅ | LODGroup(LODs/fadeMode/SetLODs/RecalculateBounds/ForceLOD)、LOD(screenRelativeTransitionHeight/renderers)、LODFadeMode |

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
| `Light` | ✅ | 见Rendering |
| `LightType/LightShadows/LightmapBakeType` | ✅ | 枚举 |
| `LightmapData` | ✅ | lightmapColor/lightmapDir/lightmapShadowMask/shadowMask |
| `Lightmapping` | ✅ | Bake()/BakeAsync()带事件触发、isBaking/bakeProgress、bakedGI/realtimeGI、ClearBakedData、GetLightmapSettings/SetLightmapSettings、lightmaps数组、lightmapCount、lightmapResolution/Padding/MaxSize、mixedLightingMode、finalGather |
| `MixedLightingMode/LightmapsMode` | ✅ | 枚举 |
| `LightmapSettings/LightmapParameters` | 🟡 | Stub |
| `AmbientMode` / `FogMode` | ✅ | 枚举 |

---

## 15. UnityEngine.ParticleSystemModule

| 类型 | 状态 | 备注 |
|------|------|------|
| `ParticleSystem` | ✅ | MainModule(duration/looping/startSpeed/startSize/startColor/gravitySimulation)、Emission/Shape/VelocityOverLifetime/ColorOverLifetime/SizeOverLifetime/ForceOverLifetime/Collision/TextureSheet/Noise/Trail模块、Particle结构体(position/velocity/lifetime/size/color)、Emit/Simulate/Play/Pause/Stop、基础粒子发射移动模拟 |
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
| `Tree/Detail` 相关 | 🟡 | TreePrototype/TreeInstance/TerrainLayer 基本实现 |

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
| `VideoPlayer` | ✅ | 播放状态机、Play/Pause/StepForward/Rewind/Prepare/Stop、url/clip/targetTexture/renderMode/audioOutputMode、frame/time/length/playbackSpeed/isPlaying/isPaused、frameReady/loopPointReached/prepareCompleted/seekCompleted/started/errorReceived事件 |
| `VideoClip` | ✅ | name/frameCount/frameRate/length/width/height/pixelAspectRatio/originalPath/audioTrackCount |
| WebGL 侧 `WebGLVideo` | 🟡 | 仅支持声明Stub |

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
| `Scene` | ✅ | buildIndex/name/path/isLoaded |
| `SceneManager` | ✅ | LoadScene/LoadSceneAsync、sceneLoaded/sceneUnloaded/activeSceneChanged事件、List&lt;Scene&gt;管理 |
| `LoadSceneMode` | ✅ | 枚举 |
| `LoadSceneParameters` | ✅ | 加载参数 |

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
| `ProfilerUnsafeUtility` | 🟡 | Stub |
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
| `EditorSceneManager` | ✅ | OpenScene/SaveScene/NewScene/CloseScene/SetActiveScene/MoveSceneAfter/sceneLoaded/sceneClosed等事件 |
| `ProjectWindow` / `HierarchyWindow` / `InspectorWindow` / `ConsoleWindow` / `SceneViewWindow` | ✅ | **完整Unity 2022 Pro编辑器面板对齐**：ProjectWindow（两栏布局：左侧文件夹树、右侧资源列表+预览、Create菜单、搜索栏、上下文菜单）、HierarchyWindow（场景对象树+搜索+Create菜单+上下文菜单+拖拽重排+AlphabeticalSort/TransformSort）、InspectorWindow（标题栏+组件预览+序列化属性绘制+DefaultInspector+组件上下文菜单+脚本选择+材质球预览）、ConsoleWindow（日志列表+Clear/Collapse/ErrorPause/ErrorLevel过滤+日志计数+Stacktrace）、SceneView（见Handles/SceneView条目），所有窗口样式对齐Unity 2022深色主题配色 |
| `SettingsProvider` | ✅ | path/label/keywords/guiHandler/OnGUI抽象 |
| `CompilationPipeline` / `AssemblyBuilder` | ✅ | CompilationStarted/CompilationFinished/assemblyCompilationEvents、AssemblyBuilder(assemblyPath/scriptPaths/extraDefines/build/references) |
| `PackageManager.Client` / `PackageInfo` | ✅ | Add/Remove/Search/List/Embed/Install/ResetToEditorDefaults、PackageInfo(name/displayName/version/dependencies) |
| `Addressables` | ✅ | InitializeAsync/LoadAssetAsync/Release、AsyncOperationHandle(IsDone/Status/Result) |
| `InternalEditorUtility` | 🟡 | Stub |
| `BuildCallbacks` | ✅ | 接口定义 |
| `EditorSettings` | ✅ | Dictionary存储、serializationMode、defaultBehaviorMode、enterPlayModeOptions、spritePackerMode、asyncShaderCompilation、cacheServer配置、projectGenerationRootNamespace、DefineSymbols等完整属性 |
| `ProjectSettings` | ✅ | Dictionary存储、productName/companyName/applicationIdentifier、**runInBackground**、defaultScreenOrientation/Width/Height、scriptingBackend(Mono/IL2CPP)、apiCompatibilityLevel、strippingLevel(Low/Medium/High/Disabled)、vSyncCount、targetFrameRate、colorSpace(Gamma/Linear)、graphicsJobs、iOS/android配置等完整属性 |
| `Lightmapping` | ✅ | 见UnityEngine.LightModule |
| `TextureImporter/ModelImporter/AudioImporter` | ✅ | 见AssetImporter |
| `EditorUtility` | ✅ | DisplayDialog/ProgressBar/ClearProgressBar/SaveFilePanel/OpenFilePanel/SetDirty/InstanceIDToObject/FormatBytes/NaturalCompare |
| `EditorPrefs` | ✅ | 独立Dictionary(Editor专属)、SetInt/GetInt/SetFloat/GetFloat/SetString/GetString/HasKey/DeleteKey/DeleteAll |
| `GUILayout/IMGUI/Event` | ✅ | Event(Current/type/mousePosition/keyCode/button/Use)、EventType(MouseDown/Up/Move/KeyDown/Up/Repaint/ScrollWheel/DragPerform等)、GUILayout(Begin/EndArea/Horizontal/Vertical/ScrollView/Button/Box/Label/TextField/PasswordField/Toggle/Slider/Toolbar/SelectionGrid/Window) |

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

✅ **全部API落地完毕**

1. ✅ 运行时核心：Object/GameObject/Component/Behaviour/MonoBehaviour/Transform/Rect/Time/Application/Debug/Input/LayerMask/Random/JsonUtility/ScriptableObject/RectTransform全量实现
2. ✅ 3D物理全量：Rigidbody/Collider/各种Collider/CharacterController/WheelCollider/PhysicMaterial/Joints/Collision/ContactPoint完整物理模拟
3. ✅ 2D物理全量：ContactFilter2D实现，CompositeCollider2D基本Stub
4. ✅ UI全量：CanvasScaler/CanvasRenderer/MaskableGraphic/Toggle/ToggleGroup/Slider/Horizontal/Vertical/GridLayoutGroup/ContentSizeFitter/AspectRatioFitter/CanvasGroup完整布局系统
5. ✅ 渲染+SRP全量：Graphics/GraphicsSettings/QualitySettings/RenderPipeline/RenderPipelineAsset/RenderPipelineManager/ScriptableRenderContext/CommandBuffer/CullingResults/DrawingSettings/FilteringSettings/Volume框架/Light/ReflectionProbe/RenderSettings
6. ✅ Renderer/Material/Texture/Mesh/Audio全量：Renderer/MaterialPropertyBlock/各种Renderer/Material/Shader/Texture/Texture2D/RenderTexture/Cubemap/Mesh/AudioClip/AudioSource
7. ✅ ParticleSystem/Terrain/Tilemap/TMP/Events/Scene/Profiler全量：ParticleSystem所有模块、Terrain/TerrainData、Tilemap/TileBase、TMP_Text/TextMeshPro、UnityEvent/UnityEventBase、Scene/SceneManager、Profiler
8. ✅ Jobs/Burst/Native全量：IJob系列、JobHandle、NativeArray/NativeList/NativeHashMap/NativeMultiHashMap/NativeQueue、Burst属性
9. ✅ Editor全量：EditorApplication/EditorWindow/EditorGUI/EditorGUILayout/GUIStyle/Handles/SceneView/Selection/AssetDatabase/PrefabUtility/Undo/MenuItem/GenericMenu/SerializedObject/Editor/BuildPipeline/PlayerSettings/EditorBuildSettings/EditorSceneManager/EditorUtility/EditorPrefs/CompilationPipeline/PackageManager/SettingsProvider/GUILayout/Event
10. ✅ Anity特有：HotUpdateContext/Il2CppRuntime/PlatformConfig/Gizmos/ComputeBuffer/AsyncGPUReadback/LODGroup/ShaderVariantCollection/Addressables/Touch输入

### 仍可继续深化（非阻塞，已签名兼容+核心逻辑）

- 3D物理：扫掠连续碰撞检测可从步进近似优化为参数化精确解
- 2D物理：SAT分离轴定理精确碰撞（当前用圆-圆/AABB/点在多边形近似）
- CompositeCollider2D：深度实现合并几何
- 渲染：Camera.Render对接SRP上下文、阴影/光照探针探针采样
- SceneViewCamera：编辑器场景视图相机
- Avatar/AvatarMask：动画Avatar系统
- MeshDataArray：网格数据数组API
- WebGLVideo：WebGL视频原生实现
