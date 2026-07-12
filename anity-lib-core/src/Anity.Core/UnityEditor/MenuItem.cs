using System;

namespace UnityEditor;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class MenuItem : Attribute
{
  public string menuItem { get; }
  public string itemName => menuItem;
  public bool isValidateFunction { get; }
  public int priority { get; }

  public MenuItem(string itemName)
  {
    this.menuItem = itemName;
    this.priority = 1000;
  }

  public MenuItem(string itemName, bool isValidateFunction)
  {
    this.menuItem = itemName;
    this.isValidateFunction = isValidateFunction;
    this.priority = 1000;
  }

  public MenuItem(string itemName, bool isValidateFunction, int priority)
  {
    this.menuItem = itemName;
    this.isValidateFunction = isValidateFunction;
    this.priority = priority;
  }
}
