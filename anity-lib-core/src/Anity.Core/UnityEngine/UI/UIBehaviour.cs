namespace UnityEngine.UI;

public abstract class UIBehaviour : MonoBehaviour
{
  protected virtual void Awake() {}
  protected virtual void OnEnable() {}
  protected virtual void OnDisable() {}
  protected virtual void OnDestroy() {}
  protected virtual void Start() {}
  protected virtual void Update() {}
  protected virtual void LateUpdate() {}
  protected virtual void FixedUpdate() {}
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

  protected bool IsDestroyed()
  {
    return false;
  }
}
