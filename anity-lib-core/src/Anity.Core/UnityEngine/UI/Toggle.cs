using System;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

public class Toggle : Selectable, IPointerClickHandler, ISubmitHandler, ICanvasElement
{
  [SerializeField] private bool m_IsOn;
  private ToggleEvent _onValueChanged = new();
  private ToggleGroup? _group;
  private ToggleTransition _toggleTransition = ToggleTransition.Fade;

  public ToggleTransition toggleTransition
  {
    get => _toggleTransition;
    set => _toggleTransition = value;
  }

  public ToggleGroup? group
  {
    get => _group;
    set
    {
      if (_group == value) return;
      if (IsActive() && _group != null)
        _group.UnregisterToggle(this);
      _group = value;
      if (IsActive() && _group != null)
        _group.RegisterToggle(this);
    }
  }

  public bool isOn
  {
    get => m_IsOn;
    set => Set(value, true);
  }

  internal void Set(bool value, bool sendCallback, bool sendToGroup = true)
  {
    if (m_IsOn == value) return;
    m_IsOn = value;
    if (sendToGroup && _group != null && _group.isActiveAndEnabled && IsActive())
    {
      if (value || !_group.allowSwitchOff)
        _group.NotifyToggleOn(this, sendCallback);
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

  public new Graphic? targetGraphic
  {
    get => base.targetGraphic;
    set => base.targetGraphic = value;
  }

  public Graphic? graphic { get; set; }

  protected override void OnEnable()
  {
    base.OnEnable();
    if (_group != null)
      _group.RegisterToggle(this);
    PlayEffect();
  }

  protected override void OnDisable()
  {
    if (_group != null)
      _group.UnregisterToggle(this);
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
