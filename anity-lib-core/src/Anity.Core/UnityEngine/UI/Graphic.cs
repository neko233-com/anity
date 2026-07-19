using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

public enum CanvasUpdate
{
  Prelayout = 0,
  Layout = 1,
  PostLayout = 2,
  PreRender = 3,
  LatePreRender = 4,
  MaxUpdateValue = 5
}

public enum ScaleMode
{
  ConstantPixelSize,
  ScaleWithScreenSize,
  ConstantPhysicalSize
}

public enum ScreenMatchMode
{
  MatchWidthOrHeight = 0,
  Expand = 1,
  Shrink = 2
}

public enum Unit
{
  Centimeters,
  Millimeters,
  Inches,
  Points,
  Picas
}

public enum BlockingObjects
{
  None = 0,
  TwoD = 1,
  ThreeD = 2,
  All = 3
}

public abstract class Graphic : UIBehaviour, ICanvasElement
{
  private Color _color = Color.white;
  private Material? _material;
  private bool _raycastTarget = true;
  private bool _useGUILayout = true;
  private bool _maskable = true;
  private bool _onCullStateChangedShouldCallGraphicRaycast;
  private bool _verticesDirty;
  private bool _materialDirty;
  private Rect _pixelAdjustedRect;
  private Canvas? _canvas;
  private CanvasRenderer? _canvasRenderer;
  private int _depth;
  private static readonly List<Graphic> _graphics = new();

  public virtual Color color
  {
    get => _color;
    set
    {
      if (_color != value)
      {
        _color = value;
        SetVerticesDirty();
      }
    }
  }

  public virtual Material? material
  {
    get => _material;
    set
    {
      if (_material != value)
      {
        _material = value;
        SetMaterialDirty();
      }
    }
  }

  public bool raycastTarget
  {
    get => _raycastTarget;
    set => _raycastTarget = value;
  }

  public bool maskable
  {
    get => _maskable;
    set => _maskable = value;
  }

  public bool onCullStateChangedShouldCallGraphicRaycast
  {
    get => _onCullStateChangedShouldCallGraphicRaycast;
    set => _onCullStateChangedShouldCallGraphicRaycast = value;
  }

  public bool verticesDirty => _verticesDirty;
  public bool materialDirty => _materialDirty;
  public Rect pixelAdjustedRect => _pixelAdjustedRect;
  public int depth => _depth;

  public RectTransform? rectTransform => transform as RectTransform;

  public Canvas? canvas
  {
    get
    {
      if (_canvas is null)
      {
        CacheCanvas();
      }
      return _canvas;
    }
  }

  public CanvasRenderer? canvasRenderer
  {
    get
    {
      if (_canvasRenderer is null)
      {
        _canvasRenderer = GetComponent<CanvasRenderer>();
        if (_canvasRenderer is null && gameObject is not null)
        {
          _canvasRenderer = gameObject.AddComponent<CanvasRenderer>();
        }
      }
      return _canvasRenderer;
    }
  }

  public virtual Texture mainTexture => defaultWhiteTexture;
  public virtual float minWidth => 0f;
  public virtual float preferredWidth => 0f;
  public virtual float flexibleWidth => -1f;
  public virtual float minHeight => 0f;
  public virtual float preferredHeight => 0f;
  public virtual float flexibleHeight => -1f;
  public virtual int layoutPriority => 0;

  private static Texture2D? s_WhiteTexture;
  protected static Texture2D defaultWhiteTexture
  {
    get
    {
      if (s_WhiteTexture == null)
      {
        s_WhiteTexture = new Texture2D();
      }
      return s_WhiteTexture;
    }
  }

  public virtual void SetAllDirty()
  {
    SetLayoutDirty();
    SetVerticesDirty();
    SetMaterialDirty();
  }

  public virtual void SetLayoutDirty()
  {
    if (!IsActive()) return;
    LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
  }

  public virtual void SetVerticesDirty()
  {
    if (!IsActive()) return;
    _verticesDirty = true;
    CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);
  }

  public virtual void SetMaterialDirty()
  {
    if (!IsActive()) return;
    _materialDirty = true;
    CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);
  }

  public virtual void Rebuild(CanvasUpdate executing)
  {
    switch (executing)
    {
      case CanvasUpdate.Layout:
        if (this is ILayoutElement layoutElement)
        {
          layoutElement.CalculateLayoutInputHorizontal();
          layoutElement.CalculateLayoutInputVertical();
        }
        if (this is ILayoutController layoutController)
        {
          layoutController.SetLayoutHorizontal();
          layoutController.SetLayoutVertical();
        }
        break;
      case CanvasUpdate.PreRender:
        if (_verticesDirty)
        {
          UpdateGeometry();
          _verticesDirty = false;
        }
        if (_materialDirty)
        {
          UpdateMaterial();
          _materialDirty = false;
        }
        break;
    }
  }

  public virtual void LayoutComplete() { }
  public virtual void GraphicUpdateComplete() { }

  public override bool IsDestroyed() => this == null;

  protected virtual void UpdateGeometry()
  {
    if (canvasRenderer is null) return;
    var vh = new VertexHelper();
    OnPopulateMesh(vh);
    var modifiers = GetComponents<IMeshModifier>();
    for (var i = 0; i < modifiers.Length; i++)
    {
      modifiers[i]?.ModifyMesh(vh);
    }
    var mesh = new Mesh();
    vh.FillMesh(mesh);
    canvasRenderer.SetMesh(mesh);
    vh.Dispose();
  }

  protected virtual void UpdateMaterial()
  {
    if (canvasRenderer is null) return;
    var mat = materialForRendering;
    canvasRenderer.SetMaterial(mat, mainTexture);
  }

  public virtual Material materialForRendering
  {
    get
    {
      var mat = material;
      if (this is IMaterialModifier materialModifier)
        mat = materialModifier.GetModifiedMaterial(mat);
      return mat;
    }
  }

  public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha)
  {
    color = targetColor;
  }

  public virtual void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale)
  {
    var c = color;
    c.a = alpha;
    color = c;
  }

  public virtual void OnCullStateChanged(bool cull)
  {
    if (canvasRenderer != null)
      canvasRenderer.cull = cull;
  }

  public virtual bool IsRaycastLocationValid(Vector2 sp, Camera? eventCamera)
  {
    return _raycastTarget;
  }

  public virtual void SetNativeSize()
  {
  }

  public virtual void OnPopulateMesh(VertexHelper vh)
  {
    vh.Clear();
    var rect = rectTransform != null ? rectTransform.rect : new Rect(0f, 0f, 100f, 100f);
    var color32 = (Color32)color;
    AddQuad(vh, rect, color32, new Vector2(0f, 0f), new Vector2(1f, 1f));
  }

  private static void AddQuad(VertexHelper vh, Rect rect, Color32 color, Vector2 uvMin, Vector2 uvMax)
  {
    AddQuad(vh, rect.xMin, rect.yMin, rect.xMax, rect.yMax, color, uvMin.x, uvMin.y, uvMax.x, uvMax.y);
  }

  private static void AddQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, Color32 color, float uvMinX, float uvMinY, float uvMaxX, float uvMaxY)
  {
    var startIndex = vh.currentVertCount;
    vh.AddVert(new Vector3(xMin, yMin, 0f), color, new Vector2(uvMinX, uvMinY));
    vh.AddVert(new Vector3(xMax, yMin, 0f), color, new Vector2(uvMaxX, uvMinY));
    vh.AddVert(new Vector3(xMax, yMax, 0f), color, new Vector2(uvMaxX, uvMaxY));
    vh.AddVert(new Vector3(xMin, yMax, 0f), color, new Vector2(uvMinX, uvMaxY));
    vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
    vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
  }

  protected override void OnEnable()
  {
    base.OnEnable();
    CacheCanvas();
    _graphics.Add(this);
    SetAllDirty();
  }

  protected override void OnDisable()
  {
    CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
    _graphics.Remove(this);
    base.OnDisable();
  }

  protected override void OnDestroy()
  {
    CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
    _graphics.Remove(this);
    base.OnDestroy();
  }

  protected override void OnTransformParentChanged()
  {
    base.OnTransformParentChanged();
    CacheCanvas();
    SetAllDirty();
  }

  protected override void OnCanvasHierarchyChanged()
  {
    base.OnCanvasHierarchyChanged();
    CacheCanvas();
  }

  private void CacheCanvas()
  {
    _canvas = GetComponentInParent<Canvas>();
  }

  internal static void GetAllGraphics(List<Graphic> result)
  {
    result.Clear();
    result.AddRange(_graphics);
  }
}

public class CanvasScaler : UIBehaviour
{
  private ScaleMode _uiScaleMode = ScaleMode.ConstantPixelSize;
  private Vector2 _referenceResolution = new(1920f, 1080f);
  private ScreenMatchMode _screenMatchMode = ScreenMatchMode.MatchWidthOrHeight;
  private float _matchWidthOrHeight;
  private float _scaleFactor = 1f;
  private float _uiScaleFactor = 1f;
  private float _referencePixelsPerUnit = 100f;
  private float _dynamicPixelsPerUnit = 1f;
  private float _fallbackScreenDPI = 96f;
  private float _defaultSpriteDPI = 96f;
  private Unit _physicalUnit = Unit.Points;
  private float _physicalUnitFactor = 1f;
  private Canvas? _canvas;

  public ScaleMode uiScaleMode
  {
    get => _uiScaleMode;
    set { _uiScaleMode = value; Handle(); }
  }

  public float uiScaleFactor
  {
    get => _uiScaleFactor;
    set { _uiScaleFactor = value; Handle(); }
  }

  public Vector2 referenceResolution
  {
    get => _referenceResolution;
    set { _referenceResolution = value; Handle(); }
  }

  public ScreenMatchMode screenMatchMode
  {
    get => _screenMatchMode;
    set { _screenMatchMode = value; Handle(); }
  }

  public float matchWidthOrHeight
  {
    get => _matchWidthOrHeight;
    set { _matchWidthOrHeight = Mathf.Clamp01(value); Handle(); }
  }

  public float scaleFactor
  {
    get => _scaleFactor;
    set { _scaleFactor = value; Handle(); }
  }

  public float referencePixelsPerUnit
  {
    get => _referencePixelsPerUnit;
    set { _referencePixelsPerUnit = value; Handle(); }
  }

  public float dynamicPixelsPerUnit
  {
    get => _dynamicPixelsPerUnit;
    set { _dynamicPixelsPerUnit = value; Handle(); }
  }

  public float fallbackScreenDPI
  {
    get => _fallbackScreenDPI;
    set { _fallbackScreenDPI = value; Handle(); }
  }

  public float defaultSpriteDPI
  {
    get => _defaultSpriteDPI;
    set { _defaultSpriteDPI = value; Handle(); }
  }

  public Unit physicalUnit
  {
    get => _physicalUnit;
    set { _physicalUnit = value; Handle(); }
  }

  public float physicalUnitFactor
  {
    get => _physicalUnitFactor;
    set { _physicalUnitFactor = value; Handle(); }
  }

  protected override void Awake()
  {
    base.Awake();
    _canvas = GetComponent<Canvas>();
    Handle();
  }

  protected override void OnEnable()
  {
    base.OnEnable();
    _canvas = GetComponent<Canvas>();
    Handle();
  }

  protected override void OnDisable()
  {
    base.OnDisable();
  }

  protected override void OnCanvasHierarchyChanged()
  {
    base.OnCanvasHierarchyChanged();
    _canvas = GetComponent<Canvas>();
    Handle();
  }

  protected virtual void Update()
  {
    Handle();
  }

  protected virtual void Handle()
  {
    if (_canvas == null)
      _canvas = GetComponent<Canvas>();
    if (_canvas == null || !_canvas.isRootCanvas) return;

    switch (_uiScaleMode)
    {
      case ScaleMode.ConstantPixelSize:
        HandleConstantPixelSize();
        break;
      case ScaleMode.ScaleWithScreenSize:
        HandleScaleWithScreenSize();
        break;
      case ScaleMode.ConstantPhysicalSize:
        HandleConstantPhysicalSize();
        break;
    }

    ApplyScaleFactorToCanvas();
  }

  protected virtual void HandleConstantPixelSize()
  {
    _scaleFactor = _uiScaleFactor;
  }

  protected virtual void HandleScaleWithScreenSize()
  {
    var screenWidth = (float)Screen.width;
    var screenHeight = (float)Screen.height;

    if (screenWidth <= 0f || screenHeight <= 0f || _referenceResolution.x <= 0f || _referenceResolution.y <= 0f)
    {
      _scaleFactor = 1f;
      return;
    }

    var scaleFactorWidth = screenWidth / _referenceResolution.x;
    var scaleFactorHeight = screenHeight / _referenceResolution.y;

    float scaleFactor;
    switch (_screenMatchMode)
    {
      case ScreenMatchMode.MatchWidthOrHeight:
        float logWidth = Mathf.Log(scaleFactorWidth, 2f);
        float logHeight = Mathf.Log(scaleFactorHeight, 2f);
        float logWeightedAverage = Mathf.Lerp(logWidth, logHeight, _matchWidthOrHeight);
        scaleFactor = Mathf.Pow(2f, logWeightedAverage);
        break;
      case ScreenMatchMode.Expand:
        scaleFactor = Mathf.Min(scaleFactorWidth, scaleFactorHeight);
        break;
      case ScreenMatchMode.Shrink:
        scaleFactor = Mathf.Max(scaleFactorWidth, scaleFactorHeight);
        break;
      default:
        scaleFactor = scaleFactorWidth;
        break;
    }

    _scaleFactor = scaleFactor;
  }

  protected virtual void HandleConstantPhysicalSize()
  {
    var currentDpi = Screen.dpi;
    var dpi = currentDpi == 0f ? _fallbackScreenDPI : currentDpi;

    float dpiFactor;
    switch (_physicalUnit)
    {
      case Unit.Centimeters:
        dpiFactor = dpi / 2.54f;
        break;
      case Unit.Millimeters:
        dpiFactor = dpi / 25.4f;
        break;
      case Unit.Inches:
        dpiFactor = dpi;
        break;
      case Unit.Points:
        dpiFactor = dpi / 72f;
        break;
      case Unit.Picas:
        dpiFactor = dpi / 6f;
        break;
      default:
        dpiFactor = dpi / 72f;
        break;
    }

    _scaleFactor = _physicalUnitFactor * dpiFactor;

    if (currentDpi != 0f && _defaultSpriteDPI != 0f)
    {
      _scaleFactor *= _fallbackScreenDPI / _defaultSpriteDPI;
    }
  }

  protected virtual void ApplyScaleFactorToCanvas()
  {
    if (_canvas == null) return;

    // Unity: Canvas.scaleFactor drives UI units → pixels; root RT size = display / scaleFactor
    _canvas.scaleFactor = _scaleFactor;
    _canvas.referencePixelsPerUnit = _referencePixelsPerUnit;

    if (!_canvas.isRootCanvas) return;

    // Re-apply layout for Overlay / Screen Space Camera after scale change
    if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay ||
        _canvas.renderMode == RenderMode.ScreenSpaceCamera)
    {
      _canvas.ApplyRootLayoutFromScale();
    }
    else if (_canvas.renderMode == RenderMode.WorldSpace)
    {
      // World Space: scaler multiplies root localScale (referencePixelsPerUnit aware)
      if (gameObject.TryGetComponent<RectTransform>(out var rt))
      {
        float s = _scaleFactor;
        rt.localScale = new Vector3(s, s, s);
      }
    }
  }

  public void SetScaleFactor(float scale)
  {
    _scaleFactor = Mathf.Max(0.001f, scale);
    ApplyScaleFactorToCanvas();
  }

  /// <summary>Unity-compatible: compute scale without applying (for tests / tooling).</summary>
  public float CalculateScaleFactor()
  {
    Handle();
    return _scaleFactor;
  }

  /// <summary>Force refresh using current Screen dimensions.</summary>
  public void HandleNow() => Handle();
}

public class GraphicRaycaster : BaseRaycaster
{
  [NonSerialized] private Canvas? _canvas;
  private bool _ignoreReversedGraphics = true;
  private BlockingObjects _blockingObjects = BlockingObjects.None;
  private LayerMask _blockingMask = -1;

  public bool ignoreReversedGraphics
  {
    get => _ignoreReversedGraphics;
    set => _ignoreReversedGraphics = value;
  }

  public BlockingObjects blockingObjects
  {
    get => _blockingObjects;
    set => _blockingObjects = value;
  }

  public LayerMask blockingMask
  {
    get => _blockingMask;
    set => _blockingMask = value;
  }

  public override int sortOrderPriority
  {
    get
    {
      var c = canvas;
      if (c is not null && c.renderMode == RenderMode.ScreenSpaceOverlay)
        return c.sortingOrder;
      return int.MinValue;
    }
  }

  public override int renderOrderPriority
  {
    get
    {
      var c = canvas;
      if (c is not null && c.renderMode == RenderMode.ScreenSpaceOverlay)
        return c.renderOrder;
      return int.MinValue;
    }
  }

  public override Camera eventCamera
  {
    get
    {
      // Unity: Overlay → null camera; Camera/World → worldCamera (or main fallback for Camera mode)
      var c = canvas;
      if (c is null) return null;
      if (c.renderMode == RenderMode.ScreenSpaceOverlay)
        return null;
      if (c.renderMode == RenderMode.ScreenSpaceCamera)
        return c.worldCamera != null ? c.worldCamera : Camera.main;
      // World Space
      return c.worldCamera != null ? c.worldCamera : Camera.main;
    }
  }

  public Canvas canvas => _canvas ??= GetComponent<Canvas>();

  public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
  {
    if (canvas is null) return;

    var eventPos = eventData.position;
    var graphics = new List<Graphic>();
    GetAllGraphicsUnderPointer(eventData, graphics);

    if (graphics.Count == 0) return;

    var camera = eventCamera;
    float hitDistance = 0f;
    if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
    {
      if (camera is null) return;
      var plane = new Plane(canvas.transform.forward, canvas.transform.position);
      var ray = camera.ScreenPointToRay(new Vector3(eventPos.x, eventPos.y, 0f));
      if (!plane.Raycast(ray, out var dist)) return;
      hitDistance = dist;

      // Optional 2D/3D blocking
      if (_blockingObjects == BlockingObjects.ThreeD || _blockingObjects == BlockingObjects.All)
      {
        // Soft block: if plane distance is infinite-like, skip
        if (hitDistance > 1e6f) return;
      }
    }

    for (var i = 0; i < graphics.Count; i++)
    {
      var g = graphics[i];
      if (!g.IsRaycastLocationValid(eventPos, camera)) continue;
      var go = g.gameObject;
      if (go is null) continue;
      if (_ignoreReversedGraphics && camera is not null)
      {
        var dir = go.transform.forward;
        var camDir = camera.transform.forward;
        if (Vector3.Dot(dir, camDir) <= 0f) continue;
      }

      var result = new RaycastResult
      {
        gameObject = go,
        module = this,
        distance = hitDistance,
        screenPosition = eventPos,
        depth = g.depth,
        sortingLayer = canvas.sortingLayerID,
        sortingOrder = canvas.sortingOrder,
        index = (float)resultAppendList.Count
      };
      resultAppendList.Add(result);
    }

    // Painter's algorithm: higher sortingOrder / depth first already via graphic sort
    resultAppendList.Sort((a, b) =>
    {
      int s = b.sortingOrder.CompareTo(a.sortingOrder);
      if (s != 0) return s;
      return b.depth.CompareTo(a.depth);
    });
  }

  private void GetAllGraphicsUnderPointer(PointerEventData eventData, List<Graphic> result)
  {
    var found = new List<Graphic>();
    Graphic.GetAllGraphics(found);

    var camera = eventCamera;
    var eventPos = eventData.position;

    for (var i = 0; i < found.Count; i++)
    {
      var g = found[i];
      if (g is null || g.depth == -1 || !g.raycastTarget || g.canvasRenderer is null || g.canvasRenderer.cull) continue;
      if (!g.IsActive()) continue;
      if (g.canvas != canvas) continue;
      var go = g.gameObject;
      if (go is null || !go.activeInHierarchy) continue;
      var rt = g.rectTransform;
      if (rt is null) continue;
      if (!RectTransformUtility.RectangleContainsScreenPoint(rt, eventPos, camera)) continue;
      if (g is MaskableGraphic mg && !mg.IsRaycastLocationValid(eventPos, camera)) continue;
      result.Add(g);
    }

    result.Sort((g1, g2) => g2.depth.CompareTo(g1.depth));
  }

  protected override void OnEnable()
  {
    base.OnEnable();
    _canvas = GetComponent<Canvas>();
  }
}
