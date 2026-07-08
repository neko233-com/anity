using System;

namespace UnityEngine.UI;

public class Button : Selectable
{
  private ButtonClickedEvent _onClick = new();

  public ButtonClickedEvent onClick
  {
    get => _onClick;
    set => _onClick = value;
  }

  public override bool IsInteractable()
  {
    return interactable;
  }

  public void OnPointerClick(PointerEventData? eventData)
  {
    if (!IsInteractable())
    {
      return;
    }

    _onClick?.Invoke();
  }

  public new void OnSubmit(BaseEventData? eventData)
  {
    if (!IsInteractable())
    {
      return;
    }

    _onClick?.Invoke();
  }
}

[Serializable]
public class ButtonClickedEvent
{
  public event Action? Clicked;

  public void Invoke()
  {
    Clicked?.Invoke();
  }
}
