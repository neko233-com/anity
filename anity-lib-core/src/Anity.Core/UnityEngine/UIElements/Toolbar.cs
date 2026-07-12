using System;

namespace UnityEngine.UIElements;

public class Toolbar : VisualElement
{
  public Toolbar()
  {
  }
}

public class ToolbarMenu : VisualElement
{
  private Action _clicked;

  public string text { get; set; } = string.Empty;
  public string variant { get; set; } = string.Empty;

  public event Action clicked
  {
    add => _clicked += value;
    remove => _clicked -= value;
  }

  public ToolbarMenu()
  {
  }

  public ToolbarMenu(Action clickEvent)
  {
    _clicked = clickEvent;
  }

  public void Click()
  {
    _clicked?.Invoke();
  }
}

public class ToolbarButton : VisualElement
{
  private Action _clicked;

  public string text { get; set; } = string.Empty;

  public event Action clicked
  {
    add => _clicked += value;
    remove => _clicked -= value;
  }

  public ToolbarButton()
  {
  }

  public ToolbarButton(Action clickEvent)
  {
    _clicked = clickEvent;
  }

  public void Click()
  {
    _clicked?.Invoke();
  }
}

public class ToolbarSearchField : VisualElement
{
  private string _value = string.Empty;

  public string value
  {
    get => _value;
    set
    {
      if (_value != value)
      {
        var previousValue = _value;
        _value = value;
        valueChanged?.Invoke(new ChangeEvent<string>
        {
          previousValue = previousValue,
          newValue = value,
          bubbles = true
        });
      }
    }
  }

  public event Action<ChangeEvent<string>> valueChanged;

  public ToolbarSearchField()
  {
  }

  public void SetValueWithoutNotify(string newValue)
  {
    _value = newValue;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<string>> callback)
  {
    valueChanged += callback;
  }
}

public class ToolbarPopupSearchField : ToolbarSearchField
{
  public ToolbarPopupSearchField()
  {
  }
}

public class ToolbarSpacer : VisualElement
{
  public float flex { get; set; }

  public ToolbarSpacer()
  {
    flex = 1f;
  }

  public ToolbarSpacer(float flex)
  {
    this.flex = flex;
  }
}

public class ToolbarToggle : VisualElement
{
  private bool _value;

  public bool value
  {
    get => _value;
    set
    {
      if (_value != value)
      {
        var previousValue = _value;
        _value = value;
        valueChanged?.Invoke(new ChangeEvent<bool>
        {
          previousValue = previousValue,
          newValue = value,
          bubbles = true
        });
      }
    }
  }

  public string text { get; set; } = string.Empty;

  public event Action<ChangeEvent<bool>> valueChanged;

  public ToolbarToggle()
  {
  }

  public void SetValueWithoutNotify(bool newValue)
  {
    _value = newValue;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<bool>> callback)
  {
    valueChanged += callback;
  }
}