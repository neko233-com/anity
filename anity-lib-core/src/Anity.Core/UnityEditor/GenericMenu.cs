using System;
using System.Collections.Generic;
using UnityEngine;
using Rect = UnityEngine.Rect;

namespace UnityEditor;

public sealed class GenericMenu
{
  private readonly List<MenuItemEntry> _entries = new();

  public delegate void MenuFunction();
  public delegate void MenuFunction2(object userData);

  public void AddItem(GUIContent content, bool isOn, Action callback)
  {
    _entries.Add(new MenuItemEntry(content?.text ?? string.Empty, callback, false, isOn));
  }

  public void AddItem(string itemName, bool isOn, Action callback)
  {
    AddItem(new GUIContent(itemName), isOn, callback);
  }

  public void AddItem(GUIContent content, bool isOn, Action<object?> callback, object? userData)
  {
    _entries.Add(new MenuItemEntry(content?.text ?? string.Empty, () => callback(userData), false, isOn));
  }

  public void AddItem(string itemName, bool isOn, Action<object?> callback, object? userData)
  {
    AddItem(new GUIContent(itemName), isOn, callback, userData);
  }

  public void AddItem(string itemName, bool isOn, MenuFunction callback)
  {
    _entries.Add(new MenuItemEntry(itemName, callback is null ? null : () => callback(), false, isOn));
  }

  public void AddItem(string itemName, bool isOn, MenuFunction2 callback, object userData)
  {
    _entries.Add(new MenuItemEntry(itemName, () => callback(userData), false, isOn));
  }

  public void AddDisabledItem(GUIContent content, bool isOn)
  {
    _entries.Add(new MenuItemEntry(content?.text ?? string.Empty, null, true, isOn));
  }

  public void AddDisabledItem(string itemName, bool isOn = false)
  {
    AddDisabledItem(new GUIContent(itemName), isOn);
  }

  public void AddSeparator(string path)
  {
    _ = path;
    _entries.Add(new MenuItemEntry("---", null, false, false, true));
  }

  public void AddSeparator()
  {
    _entries.Add(new MenuItemEntry("---", null, false, false, true));
  }

  public void DropDown(Rect position)
  {
    _ = position;
    _ = _entries.Count;
  }

  public void DropDown()
  {
    ShowAsContext();
  }

  public void ShowAsContext(Rect position)
  {
    _ = position;
    ShowAsContext();
  }

  public int GetItemCount() => _entries.Count;

  public void ShowAsContext()
  {
    foreach (var entry in _entries)
    {
      if (entry.IsSeparator)
      {
        continue;
      }
      entry.Action?.Invoke();
    }
  }

  private readonly record struct MenuItemEntry(string Name, Action? Action, bool IsDisabled, bool IsOn = false, bool IsSeparator = false);
}
