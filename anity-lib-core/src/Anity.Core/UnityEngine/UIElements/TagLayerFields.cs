using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

public class TagField : VisualElement
{
  private string _value = string.Empty;
  private List<string> _choices = new();
  private int _index = -1;

  public string value
  {
    get => _value;
    set
    {
      if (_value != value)
      {
        var previousValue = _value;
        _value = value;
        _index = value != null ? _choices.IndexOf(value) : -1;
        valueChanged?.Invoke(new ChangeEvent<string>
        {
          previousValue = previousValue,
          newValue = value,
          bubbles = true
        });
      }
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

  public IList<string> choices => _choices;

  public event Action<ChangeEvent<string>> valueChanged;

  public TagField()
  {
  }

  public TagField(string label)
  {
    name = label;
  }

  public TagField(string label, List<string> tags)
  {
    name = label;
    _choices = tags ?? new List<string>();
  }

  public void SetValueWithoutNotify(string newValue)
  {
    _value = newValue;
    _index = newValue != null ? _choices.IndexOf(newValue) : -1;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<string>> callback)
  {
    valueChanged += callback;
  }
}

public class LayerField : VisualElement
{
  private int _value;
  private List<string> _choices = new();
  private int _index = -1;

  public int value
  {
    get => _value;
    set
    {
      if (_value != value)
      {
        var previousValue = _value;
        _value = value;
        _index = value;
        valueChanged?.Invoke(new ChangeEvent<int>
        {
          previousValue = previousValue,
          newValue = value,
          bubbles = true
        });
      }
    }
  }

  public int index
  {
    get => _index;
    set
    {
      if (value >= 0 && value < _choices.Count)
      {
        this.value = value;
      }
    }
  }

  public IList<string> choices => _choices;

  public event Action<ChangeEvent<int>> valueChanged;

  public LayerField()
  {
  }

  public LayerField(string label)
  {
    name = label;
  }

  public LayerField(string label, int defaultLayer)
  {
    name = label;
    _value = defaultLayer;
  }

  public void SetValueWithoutNotify(int newValue)
  {
    _value = newValue;
    _index = newValue;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<int>> callback)
  {
    valueChanged += callback;
  }
}

public class MaskField : VisualElement
{
  private int _value;
  private List<string> _choices = new();

  public int value
  {
    get => _value;
    set
    {
      if (_value != value)
      {
        var previousValue = _value;
        _value = value;
        valueChanged?.Invoke(new ChangeEvent<int>
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
      if (_choices.Count == 0) return string.Empty;
      var count = 0;
      var last = -1;
      for (var i = 0; i < _choices.Count; i++)
      {
        if ((_value & (1 << i)) != 0)
        {
          count++;
          last = i;
        }
      }
      if (count == 0) return "Nothing";
      if (count == _choices.Count) return "Everything";
      if (count == 1) return _choices[last];
      return "Mixed...";
    }
  }

  public IList<string> choices => _choices;

  public event Action<ChangeEvent<int>> valueChanged;

  public MaskField()
  {
  }

  public MaskField(string label)
  {
    name = label;
  }

  public MaskField(List<string> choices, int defaultMask)
  {
    _choices = choices ?? new List<string>();
    _value = defaultMask;
  }

  public MaskField(string label, List<string> choices, int defaultMask)
  {
    name = label;
    _choices = choices ?? new List<string>();
    _value = defaultMask;
  }

  public void SetValueWithoutNotify(int newValue)
  {
    _value = newValue;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<int>> callback)
  {
    valueChanged += callback;
  }
}