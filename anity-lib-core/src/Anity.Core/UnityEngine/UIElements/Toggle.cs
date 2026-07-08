using System;

namespace UnityEngine.UIElements;

public class Toggle : VisualElement
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
  public bool isOn { get; set; }

  public event Action<ChangeEvent<bool>> valueChanged;

  public Toggle()
  {
  }

  public Toggle(string label)
  {
    name = label;
  }

  public Toggle(string label, Action<ChangeEvent<bool>> callback)
  {
    name = label;
    valueChanged += callback;
  }

  public void SetValueWithoutNotify(bool newValue)
  {
    _value = newValue;
  }

  public void OnToggleChanged(Action<ChangeEvent<bool>> callback)
  {
    valueChanged += callback;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<bool>> callback)
  {
    valueChanged += callback;
  }
}
