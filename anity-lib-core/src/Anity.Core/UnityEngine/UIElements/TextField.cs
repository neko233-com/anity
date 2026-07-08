using System;

namespace UnityEngine.UIElements;

public class TextField : VisualElement
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

  public string placeholder { get; set; } = string.Empty;
  public bool isReadOnly { get; set; }
  public int maxLength { get; set; } = -1;
  public string maskChar { get; set; } = "*";
  public bool isPasswordField { get; set; }
  public int cursorIndex { get; set; }
  public int selectionIndex { get; set; }
  public bool doubleClickSelectsAll { get; set; }
  public bool tripleClickSelectsAll { get; set; }
  public bool isDelayed { get; set; }

  public event Action<ChangeEvent<string>> valueChanged;

  public TextField()
  {
  }

  public TextField(string label)
  {
    name = label;
  }

  public TextField(int maxLength, bool isDelayed, bool isPasswordField, char maskChar)
  {
    this.maxLength = maxLength;
    this.isDelayed = isDelayed;
    this.isPasswordField = isPasswordField;
    this.maskChar = maskChar.ToString();
  }

  public void SetValueWithoutNotify(string newValue)
  {
    _value = newValue;
  }

  public void SelectAll()
  {
    selectionIndex = 0;
    cursorIndex = _value?.Length ?? 0;
  }

  public void ClearValue()
  {
    value = string.Empty;
  }
}
