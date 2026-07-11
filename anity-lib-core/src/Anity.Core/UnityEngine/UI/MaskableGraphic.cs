using System;
using System.Collections.Generic;

namespace UnityEngine.UI;

public abstract class MaskableGraphic : Graphic, IMaskable, IMaterialModifier, IClippable
{
  private bool _maskable = true;
  private bool _isMaskingGraphic;
  private Canvas? _maskCanvas;
  private CanvasRenderer? _maskCanvasRenderer;
  private Material? _maskMaterial;
  private Material? _unmaskMaterial;
  private bool _shouldRecalculateStencil;
  private int _stencilDepth;
  private bool _maskForced;
  private Rect _clipRect;
  private bool _clipRectValid;
  private Vector2 _clipSoftness;

  public bool maskable
  {
    get => _maskable;
    set
    {
      if (_maskable != value)
      {
        _maskable = value;
        _shouldRecalculateStencil = true;
        SetMaterialDirty();
      }
    }
  }

  public bool isMaskingGraphic
  {
    get => _isMaskingGraphic;
    set => _isMaskingGraphic = value;
  }

  public virtual bool Raycast(Vector2 sp, Camera? eventCamera)
  {
    if (!maskable)
    {
      return false;
    }

    return raycastTarget;
  }

  public virtual void RecalculateMasking()
  {
    _shouldRecalculateStencil = true;
  }

  public virtual Material GetModifiedMaterial(Material baseMaterial)
  {
    if (baseMaterial is null)
    {
      return baseMaterial;
    }

    if (!_maskable || !_maskForced)
    {
      return baseMaterial;
    }

    return baseMaterial;
  }

  public virtual void Cull(Rect clipRect, bool validRect)
  {
    _clipRect = clipRect;
    _clipRectValid = validRect;
  }

  public virtual void SetClipRect(Rect value, bool validRect)
  {
    _clipRect = value;
    _clipRectValid = validRect;
  }

  public virtual void SetClipSoftness(Vector2 clipSoftness)
  {
    _clipSoftness = clipSoftness;
  }

  protected override void OnEnable()
  {
    base.OnEnable();
    _shouldRecalculateStencil = true;
    RecalculateMasking();
  }

  protected override void OnDisable()
  {
    base.OnDisable();
    _shouldRecalculateStencil = true;
  }

  protected override void OnTransformParentChanged()
  {
    base.OnTransformParentChanged();
    _shouldRecalculateStencil = true;
    RecalculateMasking();
  }

  protected override void OnCanvasHierarchyChanged()
  {
    base.OnCanvasHierarchyChanged();
    _shouldRecalculateStencil = true;
    RecalculateMasking();
  }
}

public interface IMaskable
{
  bool maskable { get; set; }
  void RecalculateMasking();
}

public interface IMaterialModifier
{
  Material GetModifiedMaterial(Material baseMaterial);
}

public interface IClippable
{
  void Cull(Rect clipRect, bool validRect);
  void SetClipRect(Rect value, bool validRect);
  void SetClipSoftness(Vector2 clipSoftness);
}

public class Mask : MaskableGraphic, IClipper
{
  private bool _showMaskGraphic = true;
  private Graphic? _maskGraphic;

  public bool showMaskGraphic
  {
    get => _showMaskGraphic;
    set
    {
      if (_showMaskGraphic != value)
      {
        _showMaskGraphic = value;
        if (_maskGraphic is not null)
        {
          _maskGraphic.enabled = _showMaskGraphic;
        }
      }
    }
  }

  public Graphic? maskGraphic
  {
    get => _maskGraphic;
    set => _maskGraphic = value;
  }

  public bool MaskEnabled()
  {
    return IsActive() && _maskGraphic is not null;
  }

  public override bool IsRaycastLocationValid(Vector2 sp, Camera? eventCamera)
  {
    if (!isActiveAndEnabled) return true;
    return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, sp, eventCamera);
  }

  public override Material GetModifiedMaterial(Material baseMaterial)
  {
    return baseMaterial;
  }

  public virtual void PerformClipping()
  {
  }

  protected override void OnEnable()
  {
    base.OnEnable();
    _maskGraphic = GetComponent<Graphic>();
    ClipperRegistry.Register(this);
  }

  protected override void OnDisable()
  {
    ClipperRegistry.Unregister(this);
    base.OnDisable();
  }
}

public class RectMask2D : UIBehaviour, IClipper
{
  private readonly List<IClippable> _clippables = new();
  private Vector4 _cornerRadius = Vector4.zero;
  private readonly RectangularVertexClipper _vertexClipper = new();

  public virtual Rect canvasRect
  {
    get
    {
      var rt = transform as RectTransform;
      if (rt == null) return default;
      var canvas = GetComponentInParent<Canvas>();
      return _vertexClipper.GetCanvasRect(rt, canvas);
    }
  }

  public Vector4 softness { get; set; }
  public Vector4 padding { get; set; }

  public virtual void PerformClipping()
  {
    var rect = canvasRect;
    var validRect = gameObject != null && gameObject.activeInHierarchy;
    foreach (var clippable in _clippables)
    {
      clippable?.Cull(rect, validRect);
      clippable?.SetClipRect(rect, validRect);
    }
  }

  public virtual void AddClippable(IClippable clippable)
  {
    if (clippable is null || _clippables.Contains(clippable))
    {
      return;
    }

    _clippables.Add(clippable);
  }

  public virtual void RemoveClippable(IClippable clippable)
  {
    if (clippable is null)
    {
      return;
    }

    _ = _clippables.Remove(clippable);
    clippable.Cull(default, false);
    clippable.SetClipRect(default, false);
  }

  protected override void OnEnable()
  {
    base.OnEnable();
    ClipperRegistry.Register(this);
  }

  protected override void OnDisable()
  {
    ClipperRegistry.Unregister(this);
    foreach (var clippable in _clippables)
    {
      clippable?.Cull(default, false);
      clippable?.SetClipRect(default, false);
    }
    base.OnDisable();
  }

  protected override void OnTransformParentChanged()
  {
    base.OnTransformParentChanged();
    PerformClipping();
  }

  protected override void OnTransformChildrenChanged()
  {
    base.OnTransformChildrenChanged();
    PerformClipping();
  }

  protected override void OnRectTransformDimensionsChange()
  {
    base.OnRectTransformDimensionsChange();
    PerformClipping();
  }
}

public class ClipperRegistry
{
  private static readonly List<IClipper> _clippers = new();
  private static ClipperRegistry? _instance;
  public static ClipperRegistry instance => _instance ??= new ClipperRegistry();

  public static void Register(IClipper c)
  {
    if (c == null || _clippers.Contains(c)) return;
    _clippers.Add(c);
  }

  public static void Unregister(IClipper c)
  {
    if (c == null) return;
    _clippers.Remove(c);
  }

  public static void Cull()
  {
    foreach (var c in _clippers)
    {
      c.PerformClipping();
    }
  }

  public void PerformClipping()
  {
    Cull();
  }
}

public interface IClipper
{
  void PerformClipping();
}

public class RectangularVertexClipper
{
  public Rect GetCanvasRect(RectTransform t, Canvas c)
  {
    _ = c;
    if (t is null)
    {
      return default;
    }

    var pos = t.position;
    var size = t.sizeDelta;
    return new Rect(pos.x - size.x * 0.5f, pos.y - size.y * 0.5f, size.x, size.y);
  }
}

public class Clipping
{
  public static bool FindCullAndClipWorldRect(List<RectTransform> rectTransforms, out Rect clipRect, out bool validRect)
  {
    clipRect = default;
    validRect = false;

    if (rectTransforms is null || rectTransforms.Count == 0)
    {
      return false;
    }

    var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
    var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

    foreach (var t in rectTransforms)
    {
      if (t is null)
      {
        continue;
      }

      var rect = new Rect(t.position.x - t.sizeDelta.x * 0.5f, t.position.y - t.sizeDelta.y * 0.5f, t.sizeDelta.x, t.sizeDelta.y);
      min.x = MathF.Min(min.x, rect.xMin);
      min.y = MathF.Min(min.y, rect.yMin);
      max.x = MathF.Max(max.x, rect.xMax);
      max.y = MathF.Max(max.y, rect.yMax);
      validRect = true;
    }

    if (validRect)
    {
      clipRect = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }

    return validRect;
  }
}
