using System;
using System.Collections.Generic;
using System.Linq;
using Anity.Core.Runtime.Native;
using UnityEngine.UI;

namespace UnityEngine;

/// <summary>
/// UnityEngine.Canvas — Screen Space Overlay / Camera / World Space (Unity 2022.3 Pro).
/// </summary>
public enum RenderMode
{
  ScreenSpaceOverlay = 0,
  ScreenSpaceCamera = 1,
  WorldSpace = 2
}

[Flags]
public enum AdditionalCanvasShaderChannels
{
  None = 0,
  TexCoord1 = 1,
  TexCoord2 = 2,
  TexCoord3 = 4,
  Normal = 8,
  Tangent = 16
}

public enum StandaloneRenderResize
{
  Enabled = 0,
  Disabled = 1
}

[Bindings.NativeHeader("Modules/UI/Canvas.h")]
[Bindings.NativeHeader("Modules/UI/CanvasManager.h")]
[Bindings.NativeHeader("Modules/UI/UIStructs.h")]
[NativeClass("UI::Canvas")]
[RequireComponent(typeof(RectTransform))]
public sealed class Canvas : Behaviour
{
  public delegate void WillRenderCanvases();

  internal static readonly List<Canvas> _canvases = new();
  private static readonly Material _defaultCanvasMaterial = new(Shader.Find("UI/Default"));
  private static readonly Material _etc1CanvasMaterial = new(Shader.Find("UI/DefaultETC1"));

  private RenderMode _renderMode = RenderMode.ScreenSpaceOverlay;
  private Camera? _worldCamera;
  private float _planeDistance = 100f;
  private float _scaleFactor = 1f;
  private int _sortingOrder;
  private bool _overrideSorting;
  private int _targetDisplay;
  private bool _pixelPerfect;
  private bool _overridePixelPerfect;
  private float _referencePixelsPerUnit = 100f;
  private string _sortingLayerName = "Default";
  private int _sortingLayerID;
  private AdditionalCanvasShaderChannels _additionalShaderChannels;
  private float _normalizedSortingGridSize = 0.1f;
  private bool _updateRectTransformForOverlay = true;
  private StandaloneRenderResize _updateRectTransformForStandalone;
  private bool _vertexColorAlwaysGammaSpace;

  internal static List<Canvas> canvases => _canvases;
  public static event WillRenderCanvases? preWillRenderCanvases;
  public static event WillRenderCanvases? willRenderCanvases;

  public RenderMode renderMode
  {
    get => _renderMode;
    set
    {
      if (_renderMode == value) return;
      _renderMode = value;
      SetupRenderMode();
    }
  }

  [Bindings.NativeProperty("Camera", false, Bindings.TargetType.Function)]
  public Camera? worldCamera
  {
    get => _worldCamera;
    set
    {
      _worldCamera = value;
      if (_renderMode == RenderMode.ScreenSpaceCamera)
        SetupRenderMode();
    }
  }

  public float planeDistance
  {
    get => _planeDistance;
    set
    {
      _planeDistance = Mathf.Max(0.01f, value);
      if (_renderMode == RenderMode.ScreenSpaceCamera)
        PlaceInFrontOfCamera();
    }
  }

  public int sortingOrder
  {
    get => _sortingOrder;
    set => _sortingOrder = value;
  }

  internal int sortOrder
  {
    get => _sortingOrder;
    set => _sortingOrder = value;
  }

  public int renderOrder => sortingOrder;

  public bool overrideSorting
  {
    get => _overrideSorting;
    set => _overrideSorting = value;
  }

  public int targetDisplay
  {
    get => _targetDisplay;
    set => _targetDisplay = Math.Max(0, value);
  }

  public float scaleFactor
  {
    get => _scaleFactor;
    set
    {
      _scaleFactor = Mathf.Max(0.001f, value);
      ApplyRootLayoutFromScale();
    }
  }

  public float referencePixelsPerUnit
  {
    get => _referencePixelsPerUnit;
    set => _referencePixelsPerUnit = Mathf.Max(0.001f, value);
  }

  public bool pixelPerfect
  {
    get => _pixelPerfect;
    set => _pixelPerfect = value;
  }

  public bool overridePixelPerfect
  {
    get => _overridePixelPerfect;
    set => _overridePixelPerfect = value;
  }

  public string sortingLayerName
  {
    get => _sortingLayerName;
    set => _sortingLayerName = value ?? string.Empty;
  }

  public int sortingLayerID
  {
    get => _sortingLayerID;
    set => _sortingLayerID = value;
  }

  public AdditionalCanvasShaderChannels additionalShaderChannels
  {
    get => _additionalShaderChannels;
    set => _additionalShaderChannels = value;
  }

  /// <summary>Unity API alias used by some packages.</summary>
  internal bool additionalShaderChannelsFlag
  {
    get => _additionalShaderChannels != AdditionalCanvasShaderChannels.None;
    set
    {
      if (value && _additionalShaderChannels == AdditionalCanvasShaderChannels.None)
        _additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
      else if (!value)
        _additionalShaderChannels = AdditionalCanvasShaderChannels.None;
    }
  }

  [Bindings.NativeProperty("SortingBucketNormalizedSize", false, Bindings.TargetType.Function)]
  public float normalizedSortingGridSize
  {
    get => _normalizedSortingGridSize;
    set => _normalizedSortingGridSize = Mathf.Clamp(value, 0.01f, 10f);
  }

  [Obsolete("Setting normalizedSize via a int is not supported. Please use normalizedSortingGridSize", false)]
  [Bindings.NativeProperty("SortingBucketNormalizedSize", false, Bindings.TargetType.Function)]
  public int sortingGridNormalizedSize
  {
    get => (int)_normalizedSortingGridSize;
    set => normalizedSortingGridSize = value;
  }

  public StandaloneRenderResize updateRectTransformForStandalone
  {
    get => _updateRectTransformForStandalone;
    set => _updateRectTransformForStandalone = value;
  }

  public bool vertexColorAlwaysGammaSpace
  {
    get => _vertexColorAlwaysGammaSpace;
    set => _vertexColorAlwaysGammaSpace = value;
  }

  public bool isRootCanvas
  {
    get
    {
      // Walk transform parents (do not use GetComponentInParent which includes self)
      var t = transform != null ? transform.parent : null;
      while (t != null)
      {
        if (t.GetComponent<Canvas>() != null)
          return false;
        t = t.parent;
      }
      return true;
    }
  }

  public Canvas? rootCanvas
  {
    get
    {
      Canvas root = this;
      var t = transform != null ? transform.parent : null;
      while (t != null)
      {
        var c = t.GetComponent<Canvas>();
        if (c != null) root = c;
        t = t.parent;
      }
      return root;
    }
  }

  internal RectTransform? renderTransform
  {
    get => transform as RectTransform ?? GetComponent<RectTransform>();
    set { /* Unity binds to own RT */ _ = value; }
  }

  /// <summary>Pixel rectangle of the canvas in screen/display space.</summary>
  public Rect pixelRect
  {
    get
    {
      if (_renderMode == RenderMode.WorldSpace)
      {
        var rt = renderTransform;
        if (rt != null)
        {
          var r = rt.rect;
          return new Rect(0, 0, Mathf.Abs(r.width * scaleFactor), Mathf.Abs(r.height * scaleFactor));
        }
      }
      return new Rect(0, 0, Screen.width, Screen.height);
    }
  }

  public Vector2 renderingDisplaySize
  {
    get
    {
      if (_renderMode == RenderMode.ScreenSpaceCamera && _worldCamera != null)
        return new Vector2(_worldCamera.pixelWidth, _worldCamera.pixelHeight);
      return new Vector2(Screen.width, Screen.height);
    }
  }

  internal bool cachedSortingLayerValueExists => true;
  public int cachedSortingLayerValue => _sortingLayerID;

  static Canvas()
  {
    willRenderCanvases += () =>
    {
      // Sort overlay/camera canvases by sortingOrder before layout/render
      foreach (var c in _canvases.OrderBy(c => c != null ? c.sortingOrder : 0))
      {
        if (c == null || !c.enabled) continue;
        if (c.isRootCanvas)
          c.SetupRenderMode();
      }
      CanvasUpdateRegistry.instance.PerformUpdate();
      ClipperRegistry.Cull();
    };
  }

  public static void ForceUpdateCanvases()
  {
    preWillRenderCanvases?.Invoke();
    willRenderCanvases?.Invoke();
    CanvasNativeRenderBridge.Flush();
  }

  [Bindings.FreeFunction("UI::GetDefaultUIMaterial")]
  public static Material GetDefaultCanvasMaterial() => _defaultCanvasMaterial;

  [Obsolete("Shared default material now used for text and general UI elements, call Canvas.GetDefaultCanvasMaterial()", false)]
  [Bindings.FreeFunction("UI::GetDefaultUIMaterial")]
  public static Material GetDefaultCanvasTextMaterial() => _defaultCanvasMaterial;

  [Bindings.FreeFunction("UI::GetETC1SupportedCanvasMaterial")]
  public static Material GetETC1SupportedCanvasMaterial() => _etc1CanvasMaterial;

  public Canvas()
  {
    if (!_canvases.Contains(this))
      _canvases.Add(this);
  }

  internal void UnregisterCanvas()
  {
    _canvases.Remove(this);
  }

  /// <summary>
  /// Configure transform/camera binding per Unity RenderMode semantics.
  /// Overlay: screen-aligned, no world camera required.
  /// Camera: positioned at planeDistance along camera forward.
  /// World: free world transform, event camera optional via worldCamera.
  /// </summary>
  internal void SetupRenderMode()
  {
    switch (_renderMode)
    {
      case RenderMode.ScreenSpaceOverlay:
        // Overlay draws in screen space; camera ignored for rendering
        ApplyRootLayoutFromScale();
        break;
      case RenderMode.ScreenSpaceCamera:
        if (_worldCamera == null)
          _worldCamera = Camera.main;
        PlaceInFrontOfCamera();
        ApplyRootLayoutFromScale();
        break;
      case RenderMode.WorldSpace:
        // Keep authoring transform; ensure RectTransform exists
        EnsureRectTransform();
        break;
    }
  }

  internal void PlaceInFrontOfCamera()
  {
    if (_worldCamera == null || transform == null) return;
    var camT = _worldCamera.transform;
    if (camT == null) return;
    transform.position = camT.position + camT.forward * _planeDistance;
    transform.rotation = camT.rotation;
  }

  /// <summary>
  /// Root canvas layout: Unity sets sizeDelta to (screen / scaleFactor) for Overlay/Camera.
  /// </summary>
  internal void ApplyRootLayoutFromScale()
  {
    if (!isRootCanvas || !_updateRectTransformForOverlay) return;
    if (_renderMode == RenderMode.WorldSpace) return;

    var rt = EnsureRectTransform();
    if (rt == null) return;

    float sf = Mathf.Max(0.001f, _scaleFactor);
    Vector2 display = renderingDisplaySize;
    // Unity CanvasScaler: root canvas size in canvas units
    float w = display.x / sf;
    float h = display.y / sf;

    rt.anchorMin = new Vector2(0.5f, 0.5f);
    rt.anchorMax = new Vector2(0.5f, 0.5f);
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.sizeDelta = new Vector2(w, h);
    // scaleFactor is applied via Canvas.scaleFactor / graphic scale, not localScale blow-up
    rt.localScale = new Vector3(sf, sf, 1f);
    rt.position = new Vector3(display.x * 0.5f, display.y * 0.5f, 0f);
  }

  internal RectTransform EnsureRectTransform()
  {
    var rt = GetComponent<RectTransform>();
    if (rt == null && gameObject != null)
      rt = gameObject.AddComponent<RectTransform>();
    return rt!;
  }

  /// <summary>Screen point → local point in this canvas (for raycasters / EventSystem).</summary>
  internal bool ScreenPointToLocalPointInRectangle(Vector2 screenPoint, out Vector2 localPoint)
  {
    localPoint = Vector2.zero;
    var rt = renderTransform;
    if (rt == null) return false;

    Camera? eventCam = null;
    if (_renderMode == RenderMode.ScreenSpaceCamera || _renderMode == RenderMode.WorldSpace)
      eventCam = _worldCamera;

    return RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, eventCam, out localPoint);
  }

  /// <summary>Effective sorting key for painter's algorithm (layer + order).</summary>
  internal int GetSortKey()
  {
    return (_sortingLayerID << 16) ^ (_sortingOrder & 0xFFFF);
  }

  internal static IEnumerable<Canvas> GetSortedCanvases()
  {
    return _canvases
      .Where(c => c != null && c.enabled)
      .OrderBy(c => c.targetDisplay)
      .ThenBy(c => c.GetSortKey());
  }
}
