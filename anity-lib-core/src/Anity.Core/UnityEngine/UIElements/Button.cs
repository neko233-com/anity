using System;

namespace UnityEngine.UIElements;

public class Button : VisualElement
{
  private Action _clicked;

  public string text { get; set; } = string.Empty;

  public event Action clicked
  {
    add => _clicked += value;
    remove => _clicked -= value;
  }

  public Button()
  {
  }

  public Button(Action clickEvent)
  {
    _clicked = clickEvent;
  }

  public void Click()
  {
    _clicked?.Invoke();
  }

  public void RegisterClickEvent(Action clickEvent)
  {
    _clicked += clickEvent;
  }

  public void UnregisterClickEvent(Action clickEvent)
  {
    _clicked -= clickEvent;
  }
}
