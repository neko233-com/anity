using System;

namespace UnityEngine.UI;

public abstract class MaskableGraphic : Graphic, IMaskable, IMaterialModifier
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
  }

  public virtual void SetClipRect(Rect value, bool validRect)
  {
  }

  public virtual void SetClipSoftness(Vector2 clipSoftness)
  {
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

public class Mask : MaskableGraphic
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
    return true;
  }

  public override Material GetModifiedMaterial(Material baseMaterial)
  {
    return baseMaterial;
  }
}

public class RectMask2D : MonoBehaviour
{
  public virtual Rect canvasRect => default;

  public virtual void PerformClipping()
  {
  }

  public virtual void AddClippable(IClippable clippable)
  {
    _ = clippable;
  }

  public virtual void RemoveClippable(IClippable clippable)
  {
    _ = clippable;
  }

  public virtual void OnTransformParentChanged()
  {
  }

  public virtual void OnTransformChildrenChanged()
  {
  }
}

public interface IClippable
{
  Rect GetCanvasRect();
  void Cull(Rect clipRect, bool validRect);
  void SetClipRect(Rect value, bool validRect);
  void SetClipSoftness(Vector2 clipSoftness);
  void RecalculateMasking();
}

public class RectangularVertexClipper
{
  public Rect GetCanvasRect(RectTransform t, Canvas c)
  {
    _ = t;
    _ = c;
    return default;
  }
}

public class Clipping
{
  public static bool FindCullAndClipWorldRect(List<RectTransform> rectTransforms, out Rect clipRect, out bool validRect)
  {
    _ = rectTransforms;
    clipRect = default;
    validRect = false;
    return false;
  }
}
