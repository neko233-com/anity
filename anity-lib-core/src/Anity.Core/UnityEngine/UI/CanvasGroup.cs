namespace UnityEngine.UI;

public class CanvasGroup : Behaviour
{
  private float _alpha = 1f;
  private bool _interactable = true;
  private bool _blocksRaycasts = true;
  private bool _ignoreParentGroups;

  public float alpha
  {
    get => _alpha;
    set => _alpha = value;
  }

  public bool interactable
  {
    get => _interactable;
    set => _interactable = value;
  }

  public bool blocksRaycasts
  {
    get => _blocksRaycasts;
    set => _blocksRaycasts = value;
  }

  public bool ignoreParentGroups
  {
    get => _ignoreParentGroups;
    set => _ignoreParentGroups = value;
  }
}
