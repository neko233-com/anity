using System;

namespace UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public sealed class SerializeField : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
public sealed class MultilineAttribute : Attribute
{
  public int lines { get; }
  public MultilineAttribute() { lines = 3; }
  public MultilineAttribute(int lines) { this.lines = lines; }
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class DelayedAttribute : Attribute { }
