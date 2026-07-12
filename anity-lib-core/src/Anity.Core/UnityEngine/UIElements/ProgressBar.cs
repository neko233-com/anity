using System;

namespace UnityEngine.UIElements;

public class ProgressBar : VisualElement
{
  private float _value;

  public float value
  {
    get => _value;
    set
    {
      value = Mathf.Clamp(value, 0f, 1f);
      if (Math.Abs(_value - value) > float.Epsilon)
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
  public float highValue { get; set; } = 100f;
  public string title { get; set; } = string.Empty;

  public event Action<ChangeEvent<float>> valueChanged;

  public ProgressBar()
  {
  }

  public ProgressBar(float lowValue, float highValue)
  {
    this.lowValue = lowValue;
    this.highValue = highValue;
  }

  public ProgressBar(float lowValue, float highValue, string title)
  {
    this.lowValue = lowValue;
    this.highValue = highValue;
    this.title = title;
  }

  public void SetValueWithoutNotify(float newValue)
  {
    _value = Mathf.Clamp(newValue, 0f, 1f);
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<float>> callback)
  {
    valueChanged += callback;
  }
}