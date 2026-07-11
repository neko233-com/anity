using System;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

public class Toggle : Selectable, IPointerClickHandler, ISubmitHandler
{
  private bool _isOn;
  private ToggleEvent _onValueChanged = new();

  public bool isOn
  {
    get => _isOn;
    set
    {
      if (_isOn == value) return;
      _isOn = value;
      _onValueChanged?.Invoke(_isOn);
    }
  }

  public ToggleEvent onValueChanged
  {
    get => _onValueChanged;
    set => _onValueChanged = value;
  }

  public Graphic targetGraphic;
  public Graphic graphic;

  public virtual void OnPointerClick(PointerEventData eventData)
  {
    if (eventData.button != PointerEventData.InputButton.Left) return;
    isOn = !isOn;
  }

  public virtual void OnSubmit(BaseEventData eventData)
  {
    isOn = !isOn;
  }
}

[Serializable]
public class ToggleEvent : UnityEngine.Events.UnityEvent<bool>
{
}
