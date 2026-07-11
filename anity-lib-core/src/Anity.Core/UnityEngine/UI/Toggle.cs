using System;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

public class Toggle : Selectable, IPointerClickHandler, ISubmitHandler, ICanvasElement
{
  [SerializeField] private bool m_IsOn;
  private ToggleEvent _onValueChanged = new();

  public ToggleGroup group;

  public bool isOn
  {
    get => m_IsOn;
    set => Set(value, true);
  }

  internal void Set(bool value, bool sendCallback, bool sendToGroup = true)
  {
    if (m_IsOn == value) return;
    m_IsOn = value;
    if (sendToGroup && group != null && group.isActiveAndEnabled && IsActive())
    {
      if (value || !group.allowSwitchOff)
        group.NotifyToggleOn(this, sendCallback);
    }
    PlayEffect();
    if (sendCallback)
      _onValueChanged?.Invoke(m_IsOn);
  }

  public ToggleEvent onValueChanged
  {
    get => _onValueChanged;
    set => _onValueChanged = value;
  }

  public Graphic targetGraphic;
  public Graphic graphic;

  protected override void OnEnable()
  {
    base.OnEnable();
    if (group != null)
      group.RegisterToggle(this);
    PlayEffect();
  }

  protected override void OnDisable()
  {
    if (group != null)
      group.UnregisterToggle(this);
    base.OnDisable();
  }

  private void PlayEffect()
  {
    if (graphic != null)
      graphic.gameObject.SetActive(m_IsOn);
  }

  public virtual void OnPointerClick(PointerEventData eventData)
  {
    if (eventData.button != PointerEventData.InputButton.Left) return;
    isOn = !isOn;
  }

  public virtual void OnSubmit(BaseEventData eventData)
  {
    isOn = !isOn;
  }

  public virtual void Rebuild(CanvasUpdate executing) { }
  public virtual void LayoutComplete() { }
  public virtual void GraphicUpdateComplete() { }
}

[Serializable]
public class ToggleEvent : UnityEngine.Events.UnityEvent<bool>
{
}
