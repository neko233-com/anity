namespace UnityEngine.UI;

public class Image : MaskableGraphic
{
  private Sprite? _sprite;
  private ImageType _type = ImageType.Simple;
  private ImageFillMethod _fillMethod = ImageFillMethod.Horizontal;
  private float _fillAmount = 1f;
  private bool _fillClockwise = true;
  private int _fillOrigin;
  private bool _preserveAspect;
  private bool _fillCenter = true;
  private Sprite? _overrideSprite;

  public Sprite? sprite
  {
    get => _sprite;
    set
    {
      _sprite = value;
      SetVerticesDirty();
      SetMaterialDirty();
    }
  }

  public Sprite? overrideSprite
  {
    get => _overrideSprite ?? _sprite;
    set
    {
      _overrideSprite = value;
      SetVerticesDirty();
      SetMaterialDirty();
    }
  }

  public ImageType type
  {
    get => _type;
    set
    {
      _type = value;
      SetVerticesDirty();
    }
  }

  public ImageFillMethod fillMethod
  {
    get => _fillMethod;
    set => _fillMethod = value;
  }

  public float fillAmount
  {
    get => _fillAmount;
    set
    {
      _fillAmount = value;
      SetVerticesDirty();
    }
  }

  public bool fillClockwise
  {
    get => _fillClockwise;
    set => _fillClockwise = value;
  }

  public int fillOrigin
  {
    get => _fillOrigin;
    set => _fillOrigin = value;
  }

  public bool preserveAspect
  {
    get => _preserveAspect;
    set
    {
      _preserveAspect = value;
      SetVerticesDirty();
    }
  }

  public bool fillCenter
  {
    get => _fillCenter;
    set => _fillCenter = value;
  }

  public float alphaHitTestMinimumThreshold { get; set; }
  public float minimumAdjacentAngle { get; set; }

  public override void Rebuild(CanvasUpdate update)
  {
    base.Rebuild(update);
  }
}
