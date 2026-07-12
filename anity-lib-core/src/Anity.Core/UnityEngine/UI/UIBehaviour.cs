namespace UnityEngine.UI;

/// <summary>
/// Base for uGUI components — lifecycle methods must override MonoBehaviour so Awake/OnEnable fire.
/// </summary>
public abstract class UIBehaviour : MonoBehaviour
{
  public RectTransform rectTransform => transform as RectTransform;

  protected override void Awake() {}
  protected override void OnEnable() {}
  protected override void OnDisable() {}
  protected override void OnDestroy() {}
  protected override void Start() {}
  protected override void Update() {}
  protected override void LateUpdate() {}
  protected override void FixedUpdate() {}
  protected virtual void OnRectTransformDimensionsChange() {}
  protected virtual void OnCanvasGroupChanged() {}
  protected virtual void OnCanvasHierarchyChanged() {}
  protected virtual void OnDidApplyAnimationProperties() {}
  protected virtual void OnTransformParentChanged() {}
  protected virtual void OnTransformChildrenChanged() {}
  protected virtual void OnBeforeTransformParentChanged() {}
  protected virtual void OnDidInstantiateMaterial() {}

  protected bool IsActive()
  {
    return gameObject is not null && gameObject.activeInHierarchy;
  }

  public virtual bool IsDestroyed()
  {
    return false;
  }
}
