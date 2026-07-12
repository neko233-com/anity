using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

public class RadioButton : VisualElement
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

  public RadioButton()
  {
  }

  public RadioButton(string label)
  {
    text = label;
  }

  public RadioButton(string label, Action<ChangeEvent<bool>> callback)
  {
    text = label;
    valueChanged += callback;
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

public class RadioButtonGroup : VisualElement
{
  private int _value = -1;
  private readonly List<RadioButton> _buttons = new();

  public int value
  {
    get => _value;
    set
    {
      if (_value != value)
      {
        var previousValue = _value;
        _value = value;
        for (var i = 0; i < _buttons.Count; i++)
        {
          _buttons[i].SetValueWithoutNotify(i == _value);
        }
        valueChanged?.Invoke(new ChangeEvent<int>
        {
          previousValue = previousValue,
          newValue = value,
          bubbles = true
        });
      }
    }
  }

  public IReadOnlyList<RadioButton> buttons => _buttons;

  public event Action<ChangeEvent<int>> valueChanged;

  public RadioButtonGroup()
  {
  }

  public RadioButtonGroup(string label, List<string> choices = null)
  {
    name = label;
    if (choices != null)
    {
      foreach (var choice in choices)
      {
        Add(new RadioButton(choice));
      }
    }
  }

  public new void Add(VisualElement child)
  {
    if (child is RadioButton button)
    {
      Add(button);
    }
    else
    {
      base.Add(child);
    }
  }

  public void Add(RadioButton button)
  {
    if (button == null) return;
    _buttons.Add(button);
    base.Add(button);
    var capturedIndex = _buttons.Count - 1;
    button.RegisterValueChangedCallback(evt =>
    {
      if (evt.newValue)
      {
        value = capturedIndex;
      }
    });
  }

  public void SetValueWithoutNotify(int newValue)
  {
    _value = newValue;
    for (var i = 0; i < _buttons.Count; i++)
    {
      _buttons[i].SetValueWithoutNotify(i == _value);
    }
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<int>> callback)
  {
    valueChanged += callback;
  }
}