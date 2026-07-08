using System;

namespace UnityEngine.UIElements;

public class Slider : VisualElement
{
  private float _value;

  public float value
  {
    get => _value;
    set
    {
      value = Math.Clamp(value, lowValue, highValue);
      if (_value != value)
      {
        var previousValue = _value;
        _value = value;
        valueChanged?.Invoke(new ChangeEvent<float>
        {
          previousValue = previousValue,
          newValue = value,
          bubbles = true
        });
      }
    }
  }

  public float lowValue { get; set; }
  public float highValue { get; set; }
  public SliderDirection direction { get; set; } = SliderDirection.Horizontal;
  public SliderDirection inverted { get; set; }
  public string label { get; set; } = string.Empty;
  public bool showInputField { get; set; }
  public int pageSize { get; set; }
  public bool showMixedValue { get; set; }
  public string directionUssClassName { get; set; } = "unity-slider--";
  public string inputUssClassName { get; set; } = "unity-base-slider__input";
  public SliderType sliderType { get; set; }

  public event Action<ChangeEvent<float>> valueChanged;

  public Slider()
  {
  }

  public Slider(float lowValue, float highValue)
  {
    this.lowValue = lowValue;
    this.highValue = highValue;
  }

  public Slider(float lowValue, float highValue, SliderDirection direction)
  {
    this.lowValue = lowValue;
    this.highValue = highValue;
    this.direction = direction;
  }

  public Slider(float lowValue, float highValue, SliderDirection direction, float pageSize)
  {
    this.lowValue = lowValue;
    this.highValue = highValue;
    this.direction = direction;
    this.pageSize = (int)pageSize;
  }

  public void SetValueWithoutNotify(float newValue)
  {
    _value = Math.Clamp(newValue, lowValue, highValue);
  }

  public void ClampValue()
  {
    _value = Math.Clamp(_value, lowValue, highValue);
  }
}

public enum SliderType
{
  Slider = 0,
  SliderRange = 1,
  SliderDoubleRange = 2
}
