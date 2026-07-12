using System;

namespace UnityEngine.UIElements;

public class BoundsField : VisualElement
{
  private Bounds _value;

  public Bounds value
  {
    get => _value;
    set
    {
      if (_value.center != value.center || _value.size != value.size)
      {
        var previousValue = _value;
        _value = value;
        valueChanged?.Invoke(new ChangeEvent<Bounds>
        {
          previousValue = previousValue,
          newValue = value,
          bubbles = true
        });
      }
    }
  }

  public string label { get; set; } = string.Empty;

  public event Action<ChangeEvent<Bounds>> valueChanged;

  public BoundsField()
  {
    _value = new Bounds(Vector3.zero, Vector3.zero);
  }

  public BoundsField(string label)
  {
    this.label = label;
    _value = new Bounds(Vector3.zero, Vector3.zero);
  }

  public void SetValueWithoutNotify(Bounds newValue)
  {
    _value = newValue;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<Bounds>> callback)
  {
    valueChanged += callback;
  }
}

public class BoundsIntField : VisualElement
{
  private BoundsInt _value;

  public BoundsInt value
  {
    get => _value;
    set
    {
      if (!_value.Equals(value))
      {
        var previousValue = _value;
        _value = value;
        valueChanged?.Invoke(new ChangeEvent<BoundsInt>
        {
          previousValue = previousValue,
          newValue = value,
          bubbles = true
        });
      }
    }
  }

  public string label { get; set; } = string.Empty;

  public event Action<ChangeEvent<BoundsInt>> valueChanged;

  public BoundsIntField()
  {
    _value = BoundsInt.zero;
  }

  public BoundsIntField(string label)
  {
    this.label = label;
    _value = BoundsInt.zero;
  }

  public void SetValueWithoutNotify(BoundsInt newValue)
  {
    _value = newValue;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<BoundsInt>> callback)
  {
    valueChanged += callback;
  }
}

public class RectField : VisualElement
{
  private Rect _value;

  public Rect value
  {
    get => _value;
    set
    {
      if (_value != value)
      {
        var previousValue = _value;
        _value = value;
        valueChanged?.Invoke(new ChangeEvent<Rect>
        {
          previousValue = previousValue,
          newValue = value,
          bubbles = true
        });
      }
    }
  }

  public string label { get; set; } = string.Empty;

  public event Action<ChangeEvent<Rect>> valueChanged;

  public RectField()
  {
    _value = Rect.zero;
  }

  public RectField(string label)
  {
    this.label = label;
    _value = Rect.zero;
  }

  public void SetValueWithoutNotify(Rect newValue)
  {
    _value = newValue;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<Rect>> callback)
  {
    valueChanged += callback;
  }
}

public class RectIntField : VisualElement
{
  private RectInt _value;

  public RectInt value
  {
    get => _value;
    set
    {
      if (_value.x != value.x || _value.y != value.y || _value.width != value.width || _value.height != value.height)
      {
        var previousValue = _value;
        _value = value;
        valueChanged?.Invoke(new ChangeEvent<RectInt>
        {
          previousValue = previousValue,
          newValue = value,
          bubbles = true
        });
      }
    }
  }

  public string label { get; set; } = string.Empty;

  public event Action<ChangeEvent<RectInt>> valueChanged;

  public RectIntField()
  {
    _value = RectInt.zero;
  }

  public RectIntField(string label)
  {
    this.label = label;
    _value = RectInt.zero;
  }

  public void SetValueWithoutNotify(RectInt newValue)
  {
    _value = newValue;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<RectInt>> callback)
  {
    valueChanged += callback;
  }
}