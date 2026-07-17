namespace UnityEngine;

[Bindings.NativeHeader("Modules/UI/CanvasGroup.h")]
[NativeClass("UI::CanvasGroup")]
public sealed class CanvasGroup : Behaviour, ICanvasRaycastFilter
{
  private float _alpha = 1f;
  private bool _interactable = true;
  private bool _blocksRaycasts = true;
  private bool _ignoreParentGroups;

  [Bindings.NativeProperty("Alpha", false, Bindings.TargetType.Function)]
  public float alpha
  {
    get => _alpha;
    set => _alpha = value;
  }

  [Bindings.NativeProperty("Interactable", false, Bindings.TargetType.Function)]
  public bool interactable
  {
    get => _interactable;
    set => _interactable = value;
  }

  [Bindings.NativeProperty("BlocksRaycasts", false, Bindings.TargetType.Function)]
  public bool blocksRaycasts
  {
    get => _blocksRaycasts;
    set => _blocksRaycasts = value;
  }

  [Bindings.NativeProperty("IgnoreParentGroups", false, Bindings.TargetType.Function)]
  public bool ignoreParentGroups
  {
    get => _ignoreParentGroups;
    set => _ignoreParentGroups = value;
  }

  public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
  {
    return blocksRaycasts;
  }
}
