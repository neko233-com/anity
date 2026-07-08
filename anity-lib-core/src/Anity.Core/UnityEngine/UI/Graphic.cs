using System;
using System.Collections.Generic;

namespace UnityEngine.UI;

public abstract class Graphic : MonoBehaviour
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

  public virtual float minWidth => 0f;
  public virtual float preferredWidth => 0f;
  public virtual float flexibleWidth => -1f;
  public virtual float minHeight => 0f;
  public virtual float preferredHeight => 0f;
  public virtual float flexibleHeight => -1f;
  public virtual int layoutPriority => 0;

  public virtual void SetAllDirty()
  {
    SetLayoutDirty();
    SetVerticesDirty();
    SetMaterialDirty();
  }

  public virtual void SetLayoutDirty()
  {
  }

  public virtual void SetVerticesDirty()
  {
    _verticesDirty = true;
  }

  public virtual void SetMaterialDirty()
  {
    _materialDirty = true;
  }

  public virtual void Rebuild(CanvasUpdate update)
  {
    if (_canvasRenderer is null)
    {
      return;
    }

    switch (update)
    {
      case CanvasUpdate.PreRender:
        UpdateGeometry();
        break;
      case CanvasUpdate.Layout:
        UpdateMaterial();
        break;
    }
  }

  protected virtual void UpdateGeometry()
  {
    _verticesDirty = false;
  }

  protected virtual void UpdateMaterial()
  {
    _materialDirty = false;
  }

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
    SetAllDirty();
  }

  protected override void OnDisable()
  {
    base.OnDisable();
  }

  protected override void OnTransformParentChanged()
  {
    base.OnTransformParentChanged();
    CacheCanvas();
    SetAllDirty();
  }

  private void CacheCanvas()
  {
    _canvas = GetComponentInParent<Canvas>();
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
}

public class CanvasRenderer : MonoBehaviour
{
  public bool cull { get; set; }
  public bool hasPopInstruction { get; set; }
  public int materialCount { get; set; }
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

public class CanvasScaler : MonoBehaviour
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

public class GraphicRaycaster : MonoBehaviour
{
  public bool ignoreReversedGraphics { get; set; } = true;
  public BlockingObjects blockingObjects { get; set; } = BlockingObjects.None;
  public LayerMask blockingMask { get; set; }

  public void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
  {
    _ = eventData;
    _ = resultAppendList;
  }
}

public enum BlockingObjects
{
  None = 0,
  TwoD = 1,
  ThreeD = 2,
  All = 3
}

public struct RaycastResult
{
  public GameObject? gameObject;
  public float moduleIndex;
  public float distance;
  public float index;
  public float depth;
  public int sortingLayer;
  public int sortingOrder;
  public Vector3 worldPosition;
  public Vector3 worldNormal;
  public Vector2 screenPosition;
  public int displayIndex;
}
