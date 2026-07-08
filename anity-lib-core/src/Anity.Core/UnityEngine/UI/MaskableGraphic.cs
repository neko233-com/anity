namespace UnityEngine.UI;

public abstract class MaskableGraphic : Graphic
{
  private bool _maskable = true;

  public bool maskable
  {
    get => _maskable;
    set => _maskable = value;
  }

  public virtual bool Raycast(Vector2 sp, Camera? eventCamera)
  {
    _ = sp;
    _ = eventCamera;
    return raycastTarget;
  }
}
