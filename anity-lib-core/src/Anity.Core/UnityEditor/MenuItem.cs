using System;

namespace UnityEditor;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class MenuItem : Attribute
{
  public string itemName { get; }
  public bool isValidateFunction { get; }
  public int priority { get; }

  public MenuItem(string itemName)
  {
    this.itemName = itemName;
    this.priority = 0;
  }

  public MenuItem(string itemName, bool isValidateFunction, int priority = 0)
  {
    this.itemName = itemName;
    this.isValidateFunction = isValidateFunction;
    this.priority = priority;
  }
}
