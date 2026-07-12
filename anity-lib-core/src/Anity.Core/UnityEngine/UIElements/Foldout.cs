using System;

namespace UnityEngine.UIElements;

public class Foldout : VisualElement
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

  public Foldout()
  {
  }

  public Foldout(string label)
  {
    text = label;
  }

  public Foldout(string label, Action<ChangeEvent<bool>> callback)
  {
    text = label;
    valueChanged += callback;
  }

  public void Toggle()
  {
    value = !_value;
  }

  public void SetValueWithoutNotify(bool newValue)
  {
    _value = newValue;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<bool>> callback)
  {
    valueChanged += callback;
  }

  public void UnregisterValueChangedCallback(Action<ChangeEvent<bool>> callback)
  {
    valueChanged -= callback;
  }
}