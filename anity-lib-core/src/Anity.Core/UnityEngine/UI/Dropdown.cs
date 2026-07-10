using System;
using System.Collections.Generic;

namespace UnityEngine.UI;

public class Dropdown : Selectable, IPointerClickHandler, ISubmitHandler
{
    public class OptionData
    {
        public string text { get; set; } = string.Empty;
        public Sprite? image { get; set; }

        public OptionData() { }

        public OptionData(string text)
        {
            this.text = text;
        }
    }

    private List<OptionData> _options = new();
    private int _value;
    private DropdownEvent _onValueChanged = new();

    public List<OptionData> options
    {
        get => _options;
        set => _options = value ?? new List<OptionData>();
    }

    public int value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                RefreshShownValue();
                _onValueChanged?.Invoke(_value);
            }
        }
    }

    public DropdownEvent onValueChanged
    {
        get => _onValueChanged;
        set => _onValueChanged = value;
    }

    public bool IsExpanded => false;

    public void RefreshShownValue()
    {
        _value = Mathf.Clamp(_value, 0, Mathf.Max(0, _options.Count - 1));
    }

    public void AddOptions(List<OptionData> options)
    {
        if (options is null) return;
        _options.AddRange(options);
        RefreshShownValue();
    }

    public void AddOptions(List<string> options)
    {
        if (options is null) return;
        foreach (var text in options)
        {
            _options.Add(new OptionData(text));
        }

        RefreshShownValue();
    }

    public void ClearOptions()
    {
        _options.Clear();
        _value = 0;
        RefreshShownValue();
    }

    public void Show() { }
    public void Hide() { }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        Show();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!IsInteractable()) return;
        Show();
    }

    public void SetValueWithoutNotify(int input)
    {
        _value = input;
        RefreshShownValue();
    }
}

[Serializable]
public class DropdownEvent
{
    public event Action<int>? ValueChanged;

    public void Invoke(int value)
    {
        ValueChanged?.Invoke(value);
    }
}
