using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.UIElements;

public class EnumField : VisualElement
{
  private Type _enumType;
  private Enum _value;
  private List<Enum> _choices = new();
  private int _index = -1;

  public Enum value
  {
    get => _value;
    set
    {
      if (value == null || !Equals(_value, value))
      {
        var previousValue = _value;
        _value = value;
        _index = value != null ? _choices.IndexOf(value) : -1;
        valueChanged?.Invoke(new ChangeEvent<Enum>
        {
          previousValue = previousValue,
          newValue = value,
          bubbles = true
        });
      }
    }
  }

  public string text
  {
    get
    {
      if (_value != null)
        return _value.ToString();
      return string.Empty;
    }
  }

  public int index
  {
    get => _index;
    set
    {
      if (value >= 0 && value < _choices.Count)
      {
        this.value = _choices[value];
      }
    }
  }

  public IList<Enum> choices => _choices;

  public event Action<ChangeEvent<Enum>> valueChanged;

  public EnumField()
  {
  }

  public EnumField(Enum defaultValue)
  {
    Init(defaultValue);
  }

  public EnumField(Enum defaultValue, Action<ChangeEvent<Enum>> callback)
  {
    Init(defaultValue);
    valueChanged += callback;
  }

  public void Init(Type enumType)
  {
    if (enumType == null || !enumType.IsEnum)
      throw new ArgumentException("Type must be an enum type", nameof(enumType));

    _enumType = enumType;
    _choices = Enum.GetValues(enumType).Cast<Enum>().ToList();
    if (_choices.Count > 0)
    {
      _value = _choices[0];
      _index = 0;
    }
  }

  public void Init(Enum defaultValue)
  {
    if (defaultValue == null)
      throw new ArgumentNullException(nameof(defaultValue));

    _enumType = defaultValue.GetType();
    _choices = Enum.GetValues(_enumType).Cast<Enum>().ToList();
    _value = defaultValue;
    _index = _choices.IndexOf(defaultValue);
  }

  public void SetValueWithoutNotify(Enum newValue)
  {
    _value = newValue;
    _index = newValue != null ? _choices.IndexOf(newValue) : -1;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<Enum>> callback)
  {
    valueChanged += callback;
  }

  public void UnregisterValueChangedCallback(Action<ChangeEvent<Enum>> callback)
  {
    valueChanged -= callback;
  }
}