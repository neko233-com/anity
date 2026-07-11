using System;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

public class Button : Selectable, IPointerClickHandler, ISubmitHandler
{
  private ButtonClickedEvent _onClick = new();

  public ButtonClickedEvent onClick
  {
    get => _onClick;
    set => _onClick = value;
  }

  public virtual void OnPointerClick(PointerEventData eventData)
  {
    if (eventData.button != PointerEventData.InputButton.Left) return;
    Press();
  }

  public virtual void OnSubmit(BaseEventData eventData)
  {
    Press();
    if (!IsActive() || !IsInteractable()) return;
    DoStateTransition(SelectionState.Pressed, false);
  }

  private void Press()
  {
    if (!IsActive() || !IsInteractable()) return;
    _onClick?.Invoke();
  }
}

[Serializable]
public class ButtonClickedEvent : UnityEngine.Events.UnityEvent
{
}
