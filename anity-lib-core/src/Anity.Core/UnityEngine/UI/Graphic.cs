namespace UnityEngine.UI;

public abstract class Graphic : MonoBehaviour
{
  private Color _color = Color.white;
  private Material? _material;
  private bool _raycastTarget = true;
  private bool _useGUILayout = true;

  public virtual Color color
  {
    get => _color;
    set => _color = value;
  }

  public virtual Material? material
  {
    get => _material;
    set => _material = value;
  }

  public bool raycastTarget
  {
    get => _raycastTarget;
    set => _raycastTarget = value;
  }

  public virtual void SetAllDirty()
  {
    SetVerticesDirty();
    SetMaterialDirty();
  }

  public virtual void SetVerticesDirty() {}
  public virtual void SetMaterialDirty() {}

  public virtual void Rebuild(CanvasUpdate update)
  {
    _ = update;
  }

  public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha)
  {
    _ = targetColor;
    _ = duration;
    _ = ignoreTimeScale;
    _ = useAlpha;
  }

  public virtual void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale)
  {
    _ = alpha;
    _ = duration;
    _ = ignoreTimeScale;
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
