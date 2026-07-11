using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

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
      case CanvasUpdate.Layout:
        break;
    }
  }

  public override bool IsDestroyed() => this == null;

  protected virtual void UpdateGeometry() { }

  protected virtual void UpdateMaterial() { }

  public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha)
  {
  }

  public virtual void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale)
  {
  }

  public virtual void OnCullStateChanged(bool cull)
  {
  }

  public virtual bool IsRaycastLocationValid(Vector2 sp, Camera? eventCamera)
  {
    return _raycastTarget;
  }

  public virtual void SetNativeSize()
  {
  }

  public virtual void OnFillVh(List<UIVertex> vh)
  {
    _ = vh;
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

public enum CanvasUpdate
{
  Prelayout = 0,
  Layout = 1,
  PostLayout = 2,
  PreRender = 3,
  LatePreRender = 4,
  MaxUpdateValue = 5
}

public struct UIVertex
{
  public Vector3 position;
  public Vector3 normal;
  public Vector4 tangent;
  public Color32 color;
  public Vector4 uv0;
  public Vector4 uv1;
  public Vector4 uv2;
  public Vector4 uv3;

  public static UIVertex simpleVert = new()
  {
    position = Vector3.zero,
    normal = new Vector3(0f, 0f, -1f),
    tangent = new Vector4(1f, 0f, 0f, -1f),
    color = new Color32(255, 255, 255, 255),
    uv0 = new Vector4(0f, 0f, 0f, 1f)
  };
}

public class CanvasRenderer : MonoBehaviour
{
  public bool cull { get; set; }
  public bool hasPopInstruction { get; set; }
  public int materialCount { get; set; } = 1;
  public int popMaterialCount { get; set; }
  public int absoluteDepth { get; set; }
  public bool hasMoved { get; set; }

  public void SetMaterial(Material? material, int index)
  {
    _ = material;
    _ = index;
  }

  public Material? GetMaterial(int index)
  {
    _ = index;
    return null;
  }

  public void SetAlpha(float alpha)
  {
    _ = alpha;
  }

  public float GetAlpha()
  {
    return 1f;
  }

  public void SetColor(Color color)
  {
    _ = color;
  }

  public Color GetColor()
  {
    return Color.white;
  }

  public void Clear()
  {
  }

  public void SetVertices(List<UIVertex> vertices)
  {
    _ = vertices;
  }

  public void SetVertices(List<UIVertex> vertices, int size)
  {
    _ = vertices;
    _ = size;
  }

  public void SetVertices(List<UIVertex> vertices, int size, bool canTrimVertices)
  {
    _ = vertices;
    _ = size;
    _ = canTrimVertices;
  }

  public int GetVertexCount()
  {
    return 0;
  }

  public UIVertex GetUIVertex(int index)
  {
    _ = index;
    return default;
  }
}

public class CanvasScaler : UIBehaviour
{
  public ScaleMode uiScaleMode { get; set; } = ScaleMode.ConstantPixelSize;
  public Vector2 referenceResolution { get; set; } = new Vector2(1920, 1080);
  public ScreenMatchMode screenMatchMode { get; set; } = ScreenMatchMode.MatchWidthOrHeight;
  public float matchWidthOrHeight { get; set; }
  public float scaleFactor { get; set; } = 1f;
  public float referencePixelsPerUnit { get; set; } = 100f;
  public float defaultUnit { get; set; } = 1f;
  public float fallbackScreenDPI { get; set; } = 96f;
  public Unit physicalUnit { get; set; } = Unit.Centimeters;
}

public enum ScaleMode
{
  ConstantPixelSize,
  ScaleWithScreenSize,
  ConstantPhysicalSize
}

public enum ScreenMatchMode
{
  MatchWidthOrHeight,
  Expand,
  Shrink
}

public enum Unit
{
  Centimeters,
  Millimeters,
  Inches,
  Points,
  Picas
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
      var c = canvas;
      if (c is null || c.renderMode == RenderMode.ScreenSpaceOverlay || c.worldCamera is null)
        return Camera.main;
      return c.worldCamera;
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
    float hitDistance;
    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
      hitDistance = 0f;
    else
    {
      var plane = new Plane(transform.forward, transform.position);
      var ray = camera.ScreenPointToRay(new Vector3(eventPos.x, eventPos.y, 0f));
      if (!plane.Raycast(ray, out var dist)) return;
      hitDistance = dist;
    }

    for (var i = 0; i < graphics.Count; i++)
    {
      var g = graphics[i];
      if (!g.IsRaycastLocationValid(eventPos, camera)) continue;
      var go = g.gameObject;
      if (_ignoreReversedGraphics)
      {
        var dir = go.transform.forward;
        var camDir = camera is not null ? camera.transform.forward : Vector3.forward;
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

public enum BlockingObjects
{
  None = 0,
  TwoD = 1,
  ThreeD = 2,
  All = 3
}
