using System;
using System.Collections.Generic;

namespace UnityEngine.UI;

public class Image : MaskableGraphic, ILayoutElement, ICanvasRaycastFilter
{
  [SerializeField] private Sprite? _sprite;
  [SerializeField] private Sprite? _overrideSprite;
  [SerializeField] private ImageType _type = ImageType.Simple;
  [SerializeField] private ImageFillMethod _fillMethod = ImageFillMethod.Horizontal;
  [SerializeField] private float _fillAmount = 1f;
  [SerializeField] private bool _fillClockwise = true;
  [SerializeField] private int _fillOrigin;
  [SerializeField] private bool _preserveAspect;
  [SerializeField] private bool _fillCenter = true;
  [SerializeField] private float _alphaHitTestMinimumThreshold;
  [SerializeField] private bool _useSpriteMesh;
  [SerializeField] private float _pixelsPerUnitMultiplier = 1f;

  private static Material? s_ETC1DefaultUI;

  public Sprite? sprite
  {
    get => _sprite;
    set
    {
      if (_sprite != value)
      {
        _sprite = value;
        SetAllDirty();
      }
    }
  }

  public Sprite? overrideSprite
  {
    get => _overrideSprite ?? _sprite;
    set
    {
      if (_overrideSprite != value)
      {
        _overrideSprite = value;
        SetAllDirty();
      }
    }
  }

  private Sprite activeSprite => _overrideSprite ?? _sprite;

  public override Texture mainTexture
  {
    get
    {
      if (activeSprite != null)
        return activeSprite.texture ?? defaultWhiteTexture;
      return defaultWhiteTexture;
    }
  }

  public bool hasBorder
  {
    get
    {
      var spr = activeSprite;
      if (spr == null) return false;
      return spr.border.sqrMagnitude > 0f;
    }
  }

  public ImageType type
  {
    get => _type;
    set
    {
      if (_type != value)
      {
        _type = value;
        SetVerticesDirty();
        SetMaterialDirty();
      }
    }
  }

  public ImageFillMethod fillMethod
  {
    get => _fillMethod;
    set
    {
      if (_fillMethod != value)
      {
        _fillMethod = value;
        SetVerticesDirty();
      }
    }
  }

  public float fillAmount
  {
    get => _fillAmount;
    set
    {
      if (Mathf.Abs(_fillAmount - value) > float.Epsilon)
      {
        _fillAmount = Mathf.Clamp01(value);
        SetVerticesDirty();
      }
    }
  }

  public bool fillClockwise
  {
    get => _fillClockwise;
    set
    {
      if (_fillClockwise != value)
      {
        _fillClockwise = value;
        SetVerticesDirty();
      }
    }
  }

  public int fillOrigin
  {
    get => _fillOrigin;
    set
    {
      if (_fillOrigin != value)
      {
        _fillOrigin = value;
        SetVerticesDirty();
      }
    }
  }

  public bool preserveAspect
  {
    get => _preserveAspect;
    set
    {
      if (_preserveAspect != value)
      {
        _preserveAspect = value;
        SetVerticesDirty();
      }
    }
  }

  public bool fillCenter
  {
    get => _fillCenter;
    set
    {
      if (_fillCenter != value)
      {
        _fillCenter = value;
        SetVerticesDirty();
      }
    }
  }

  public float alphaHitTestMinimumThreshold
  {
    get => _alphaHitTestMinimumThreshold;
    set => _alphaHitTestMinimumThreshold = value;
  }

  public float pixelsPerUnitMultiplier
  {
    get => _pixelsPerUnitMultiplier;
    set => _pixelsPerUnitMultiplier = value;
  }

  public bool useSpriteMesh
  {
    get => _useSpriteMesh;
    set => _useSpriteMesh = value;
  }

  public virtual float minWidth => 0f;
  public virtual float preferredWidth
  {
    get
    {
      var spr = activeSprite;
      if (spr != null)
        return spr.rect.width / spr.pixelsPerUnit;
      return 0f;
    }
  }
  public virtual float flexibleWidth => -1f;
  public virtual float minHeight => 0f;
  public virtual float preferredHeight
  {
    get
    {
      var spr = activeSprite;
      if (spr != null)
        return spr.rect.height / spr.pixelsPerUnit;
      return 0f;
    }
  }
  public virtual float flexibleHeight => -1f;
  public virtual int layoutPriority => 0;

  public virtual void CalculateLayoutInputHorizontal()
  {
  }

  public virtual void CalculateLayoutInputVertical()
  {
  }

  public virtual bool IsRaycastLocationValid(Vector2 screenPoint, Camera? eventCamera)
  {
    if (_alphaHitTestMinimumThreshold <= 0f)
      return true;

    if (_alphaHitTestMinimumThreshold > 1f)
      return false;

    if (mainTexture == null)
      return true;

    return true;
  }

  public override void SetNativeSize()
    {
      var spr = activeSprite;
      if (spr != null && rectTransform != null)
      {
        rectTransform.anchorMin = rectTransform.anchorMax;
        var w = spr.rect.width / spr.pixelsPerUnit;
        var h = spr.rect.height / spr.pixelsPerUnit;
        rectTransform.sizeDelta = new Vector2(w, h);
      }
    }

    public override void OnPopulateMesh(VertexHelper vh)
  {
    vh.Clear();

    var rect = rectTransform != null ? rectTransform.rect : new Rect(0f, 0f, 100f, 100f);
    var color32 = (Color32)color;

    var drawingRect = rect;
    if (_preserveAspect && activeSprite != null)
    {
      var size = activeSprite.rect.size;
      var spriteRatio = size.x / size.y;
      var rectRatio = rect.width / rect.height;

      if (spriteRatio > rectRatio)
      {
        var newHeight = rect.width / spriteRatio;
        drawingRect.height = newHeight;
        drawingRect.y = rect.y + (rect.height - newHeight) * 0.5f;
      }
      else
      {
        var newWidth = rect.height * spriteRatio;
        drawingRect.width = newWidth;
        drawingRect.x = rect.x + (rect.width - newWidth) * 0.5f;
      }
    }

    if (_type == ImageType.Filled)
    {
      GenerateFilledSprite(vh, drawingRect, color32);
    }
    else if (_type == ImageType.Sliced && hasBorder)
    {
      GenerateSlicedSprite(vh, drawingRect, color32);
    }
    else if (_type == ImageType.Tiled)
    {
      GenerateTiledSprite(vh, drawingRect, color32);
    }
    else
    {
      GenerateSimpleSprite(vh, drawingRect, color32, _preserveAspect);
    }
  }

  private void GenerateSimpleSprite(VertexHelper vh, Rect rect, Color32 color, bool preserveAspect)
  {
    _ = preserveAspect;
    var xMin = rect.xMin;
    var xMax = rect.xMax;
    var yMin = rect.yMin;
    var yMax = rect.yMax;

    var uvMinX = 0f;
    var uvMinY = 0f;
    var uvMaxX = 1f;
    var uvMaxY = 1f;

    AddQuad(vh, xMin, yMin, xMax, yMax, color, uvMinX, uvMinY, uvMaxX, uvMaxY);
  }

  private void GenerateSlicedSprite(VertexHelper vh, Rect rect, Color32 color)
  {
    var spr = activeSprite;
    if (spr == null)
    {
      GenerateSimpleSprite(vh, rect, color, false);
      return;
    }

    var border = spr.border;
    var adjustedBorders = GetAdjustedBorders(border / spr.pixelsPerUnit, rect);
    var xMin = rect.xMin;
    var yMin = rect.yMin;
    var xMax = rect.xMax;
    var yMax = rect.yMax;

    var left = adjustedBorders.x;
    var bottom = adjustedBorders.y;
    var right = adjustedBorders.z;
    var top = adjustedBorders.w;

    var uvX = border.x / spr.rect.width;
    var uvY = border.y / spr.rect.height;
    var uvZ = border.z / spr.rect.width;
    var uvW = border.w / spr.rect.height;

    var x1 = xMin + left;
    var x2 = xMax - right;
    var y1 = yMin + bottom;
    var y2 = yMax - top;

    var u1 = uvX;
    var u2 = 1f - uvZ;
    var v1 = uvY;
    var v2 = 1f - uvW;

    AddQuad(vh, xMin, yMin, x1, y1, color, 0f, 0f, u1, v1);
    AddQuad(vh, x1, yMin, x2, y1, color, u1, 0f, u2, v1);
    AddQuad(vh, x2, yMin, xMax, y1, color, u2, 0f, 1f, v1);

    AddQuad(vh, xMin, y1, x1, y2, color, 0f, v1, u1, v2);
    if (_fillCenter)
      AddQuad(vh, x1, y1, x2, y2, color, u1, v1, u2, v2);
    AddQuad(vh, x2, y1, xMax, y2, color, u2, v1, 1f, v2);

    AddQuad(vh, xMin, y2, x1, yMax, color, 0f, v2, u1, 1f);
    AddQuad(vh, x1, y2, x2, yMax, color, u1, v2, u2, 1f);
    AddQuad(vh, x2, y2, xMax, yMax, color, u2, v2, 1f, 1f);
  }

  private void GenerateTiledSprite(VertexHelper vh, Rect rect, Color32 color)
  {
    var spr = activeSprite;
    if (spr == null)
    {
      GenerateSimpleSprite(vh, rect, color, false);
      return;
    }

    var tileSize = spr.rect.size / spr.pixelsPerUnit;
    var uvScaleX = 1f;
    var uvScaleY = 1f;

    var x = rect.xMin;
    while (x < rect.xMax)
    {
      var y = rect.yMin;
      var tileWidth = Mathf.Min(tileSize.x, rect.xMax - x);
      uvScaleX = tileWidth / tileSize.x;

      while (y < rect.yMax)
      {
        var tileHeight = Mathf.Min(tileSize.y, rect.yMax - y);
        uvScaleY = tileHeight / tileSize.y;

        AddQuad(vh, x, y, x + tileWidth, y + tileHeight, color, 0f, 0f, uvScaleX, uvScaleY);
        y += tileHeight;
      }
      x += tileWidth;
    }
  }

  private Vector4 GetAdjustedBorders(Vector4 border, Rect rect)
  {
    var horizontalTotal = border.x + border.z;
    var verticalTotal = border.y + border.w;

    if (horizontalTotal > rect.width)
    {
      var ratio = rect.width / horizontalTotal;
      border.x *= ratio;
      border.z *= ratio;
    }
    if (verticalTotal > rect.height)
    {
      var ratio = rect.height / verticalTotal;
      border.y *= ratio;
      border.w *= ratio;
    }

    return border;
  }

  private void GenerateFilledSprite(VertexHelper vh, Rect rect, Color32 color)
  {
    if (_fillAmount < 0.001f)
      return;

    var xMin = rect.xMin;
    var xMax = rect.xMax;
    var yMin = rect.yMin;
    var yMax = rect.yMax;
    var fillAmt = _fillClockwise ? _fillAmount : 1f - _fillAmount;

    switch (_fillMethod)
    {
      case ImageFillMethod.Horizontal:
        var fillWidth = (xMax - xMin) * _fillAmount;
        if (!_fillClockwise)
          AddQuad(vh, xMax - fillWidth, yMin, xMax, yMax, color, 1f - _fillAmount, 0f, 1f, 1f);
        else
          AddQuad(vh, xMin, yMin, xMin + fillWidth, yMax, color, 0f, 0f, _fillAmount, 1f);
        break;

      case ImageFillMethod.Vertical:
        var fillHeight = (yMax - yMin) * _fillAmount;
        if (!_fillClockwise)
          AddQuad(vh, xMin, yMax - fillHeight, xMax, yMax, color, 0f, 1f - _fillAmount, 1f, 1f);
        else
          AddQuad(vh, xMin, yMin, xMax, yMin + fillHeight, color, 0f, 0f, 1f, _fillAmount);
        break;

      case ImageFillMethod.Radial90:
        GenerateRadial90(vh, rect, color, _fillAmount, _fillClockwise, _fillOrigin);
        break;

      case ImageFillMethod.Radial180:
        GenerateRadial180(vh, rect, color, _fillAmount, _fillClockwise, _fillOrigin);
        break;

      case ImageFillMethod.Radial360:
        GenerateRadial360(vh, rect, color, _fillAmount, _fillClockwise, _fillOrigin);
        break;
    }
  }

  private void GenerateRadial90(VertexHelper vh, Rect rect, Color32 color, float fill, bool cw, int origin)
  {
    var xMin = rect.xMin;
    var yMin = rect.yMin;
    var xMax = rect.xMax;
    var yMax = rect.yMax;
    var rx = xMax - xMin;
    var ry = yMax - yMin;

    float x0 = xMin, y0 = yMin, x1 = xMax, y1 = yMax, u0 = 0f, v0 = 0f, u1 = 1f, v1 = 1f;

    if (cw)
    {
      switch (origin)
      {
        case 0: x1 = xMin + rx * fill; u1 = fill; break;
        case 1: y1 = yMin + ry * fill; v1 = fill; break;
        case 2: x0 = xMax - rx * fill; u0 = 1f - fill; break;
        case 3: y0 = yMax - ry * fill; v0 = 1f - fill; break;
      }
    }
    else
    {
      switch (origin)
      {
        case 0: x0 = xMax - rx * fill; u0 = 1f - fill; break;
        case 1: y0 = yMax - ry * fill; v0 = 1f - fill; break;
        case 2: x1 = xMin + rx * fill; u1 = fill; break;
        case 3: y1 = yMin + ry * fill; v1 = fill; break;
      }
    }

    AddQuad(vh, x0, y0, x1, y1, color, u0, v0, u1, v1);
  }

  private void GenerateRadial180(VertexHelper vh, Rect rect, Color32 color, float fill, bool cw, int origin)
  {
    var halfRect = rect;
    var fillHalf = Mathf.Clamp01(fill * 2f);

    if (origin < 2)
    {
      halfRect.width *= 0.5f;
      if (origin == 1) halfRect.x += rect.width * 0.5f;
    }
    else
    {
      halfRect.height *= 0.5f;
      if (origin == 2) halfRect.y += rect.height * 0.5f;
    }

    if (fill > 0.5f)
      GenerateSimpleSprite(vh, halfRect, color, false);
    GenerateRadial90(vh, halfRect, color, fillHalf, cw, origin % 4);
  }

  private void GenerateRadial360(VertexHelper vh, Rect rect, Color32 color, float fill, bool cw, int origin)
  {
    if (fill >= 1f)
    {
      GenerateSimpleSprite(vh, rect, color, false);
      return;
    }

    var fillHalf = fill;
    var fillFirstHalf = Mathf.Clamp01(fill * 2f);
    var fillSecondHalf = Mathf.Clamp01((fill - 0.5f) * 2f);

    var quadRect = rect;
    quadRect.width *= 0.5f;
    quadRect.height *= 0.5f;

    if (fill > 0.75f)
    {
      var r = quadRect; r.x = rect.xMin; r.y = rect.yMin + quadRect.height; AddQuad(vh, r.xMin, r.yMin, r.xMax, r.yMax, color, 0f, 0.5f, 0.5f, 1f);
    }
    if (fill > 0.5f)
    {
      var r = quadRect; r.x = rect.xMin; r.y = rect.yMin; GenerateRadial90(vh, r, color, fillSecondHalf, !cw, (origin + 1) % 4);
    }
    if (fill > 0.25f)
    {
      var r = quadRect; r.x = rect.xMin + quadRect.width; r.y = rect.yMin; AddQuad(vh, r.xMin, r.yMin, r.xMax, r.yMax, color, 0.5f, 0f, 1f, 0.5f);
    }
    if (fill > 0f)
    {
      var r = quadRect; r.x = rect.xMin + quadRect.width; r.y = rect.yMin + quadRect.height; GenerateRadial90(vh, r, color, fillFirstHalf, cw, origin % 4);
    }
  }

  private void AddQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, Color32 color, float uvMinX, float uvMinY, float uvMaxX, float uvMaxY)
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
    SetAllDirty();
  }

  protected override void OnDisable()
  {
    CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
    base.OnDisable();
  }

  protected override void OnDestroy()
  {
    CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
    base.OnDestroy();
  }
}

public interface ICanvasRaycastFilter
{
  bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera);
}
