using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

public class TabView : VisualElement
{
  private int _selectedIndex = -1;
  private readonly List<Tab> _tabs = new();

  public int selectedIndex
  {
    get => _selectedIndex;
    set
    {
      if (_selectedIndex != value && value >= 0 && value < _tabs.Count)
      {
        var previousIndex = _selectedIndex;
        _selectedIndex = value;
        if (previousIndex >= 0 && previousIndex < _tabs.Count)
        {
          _tabs[previousIndex].style.display = DisplayStyle.None;
        }
        if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
        {
          _tabs[_selectedIndex].style.display = DisplayStyle.Flex;
        }
        selectedIndexChanged?.Invoke(new ChangeEvent<int>
        {
          previousValue = previousIndex,
          newValue = value,
          bubbles = true
        });
      }
    }
  }

  public Tab selectedTab
  {
    get => _selectedIndex >= 0 && _selectedIndex < _tabs.Count ? _tabs[_selectedIndex] : null;
    set
    {
      var index = _tabs.IndexOf(value);
      if (index >= 0)
      {
        selectedIndex = index;
      }
    }
  }

  public IReadOnlyList<Tab> tabs => _tabs;

  public event Action<ChangeEvent<int>> selectedIndexChanged;

  public TabView()
  {
  }

  public void AddTab(Tab tab)
  {
    if (tab == null) return;
    _tabs.Add(tab);
    Add(tab);
    if (_tabs.Count == 1)
    {
      selectedIndex = 0;
    }
    else
    {
      tab.style.display = DisplayStyle.None;
    }
  }

  public void RemoveTab(Tab tab)
  {
    var index = _tabs.IndexOf(tab);
    if (index >= 0)
    {
      _tabs.RemoveAt(index);
      Remove(tab);
      if (_selectedIndex >= _tabs.Count)
      {
        selectedIndex = _tabs.Count - 1;
      }
      else if (_selectedIndex > index)
      {
        _selectedIndex--;
      }
    }
  }

  public void NextTab()
  {
    if (_tabs.Count == 0) return;
    selectedIndex = (_selectedIndex + 1) % _tabs.Count;
  }

  public void PreviousTab()
  {
    if (_tabs.Count == 0) return;
    selectedIndex = (_selectedIndex - 1 + _tabs.Count) % _tabs.Count;
  }
}

public class Tab : VisualElement
{
  public string label { get; set; } = string.Empty;

  public Tab()
  {
  }

  public Tab(string label)
  {
    this.label = label;
  }

  public Tab(string label, VisualElement content)
  {
    this.label = label;
    if (content != null)
      Add(content);
  }
}

public class TabButton : VisualElement
{
  private Action _clicked;

  public string text { get; set; } = string.Empty;
  public bool selected { get; set; }

  public event Action clicked
  {
    add => _clicked += value;
    remove => _clicked -= value;
  }

  public TabButton()
  {
  }

  public TabButton(string label)
  {
    text = label;
  }

  public TabButton(string label, Action clickEvent)
  {
    text = label;
    _clicked = clickEvent;
  }

  public void Click()
  {
    _clicked?.Invoke();
  }
}