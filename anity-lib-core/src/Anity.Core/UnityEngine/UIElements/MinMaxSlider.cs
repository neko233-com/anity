using System;

namespace UnityEngine.UIElements;

public class MinMaxSlider : VisualElement
{
  private Vector2 _value;

  public Vector2 value
  {
    get => _value;
    set
    {
      var clampedX = Mathf.Clamp(value.x, lowLimit, highLimit);
      var clampedY = Mathf.Clamp(value.y, lowLimit, highLimit);
      if (clampedX > clampedY)
      {
        (clampedX, clampedY) = (clampedY, clampedX);
      }

      var clamped = new Vector2(clampedX, clampedY);
      if (_value != clamped)
      {
        var previousValue = _value;
        _value = clamped;
        valueChanged?.Invoke(new ChangeEvent<Vector2>
        {
          previousValue = previousValue,
          newValue = clamped,
          bubbles = true
        });
      }
    }
  }

  public float minValue
  {
    get => _value.x;
    set
    {
      var v = this.value;
      v.x = value;
      this.value = v;
    }
  }

  public float maxValue
  {
    get => _value.y;
    set
    {
      var v = this.value;
      v.y = value;
      this.value = v;
    }
  }

  public float lowLimit { get; set; }
  public float highLimit { get; set; } = 1f;
  public string label { get; set; } = string.Empty;

  public event Action<ChangeEvent<Vector2>> valueChanged;

  public MinMaxSlider()
  {
    _value = new Vector2(0f, 1f);
  }

  public MinMaxSlider(float minValue, float maxValue, float lowLimit, float highLimit)
  {
    this.lowLimit = lowLimit;
    this.highLimit = highLimit;
    this.value = new Vector2(minValue, maxValue);
  }

  public MinMaxSlider(string label, float minValue, float maxValue, float lowLimit, float highLimit)
  {
    this.label = label;
    this.lowLimit = lowLimit;
    this.highLimit = highLimit;
    this.value = new Vector2(minValue, maxValue);
  }

  public void SetValueWithoutNotify(Vector2 newValue)
  {
    var clampedX = Mathf.Clamp(newValue.x, lowLimit, highLimit);
    var clampedY = Mathf.Clamp(newValue.y, lowLimit, highLimit);
    if (clampedX > clampedY)
    {
      (clampedX, clampedY) = (clampedY, clampedX);
    }
    _value = new Vector2(clampedX, clampedY);
  }

  public void SetMinMaxWithoutNotify(float minValue, float maxValue)
  {
    SetValueWithoutNotify(new Vector2(minValue, maxValue));
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<Vector2>> callback)
  {
    valueChanged += callback;
  }

  public void UnregisterValueChangedCallback(Action<ChangeEvent<Vector2>> callback)
  {
    valueChanged -= callback;
  }
}