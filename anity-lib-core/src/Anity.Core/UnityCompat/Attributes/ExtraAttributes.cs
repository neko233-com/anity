using System;

namespace UnityEngine;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class BeforeRenderOrderAttribute : Attribute
{
  public int order { get; }

  public BeforeRenderOrderAttribute(int order)
  {
    this.order = order;
  }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ImageEffectTransformsToLinearAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ImageEffectAfterTransparentAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class InspectorNameAttribute : PropertyAttribute
{
  public string displayName { get; }

  public InspectorNameAttribute(string displayName)
  {
    this.displayName = displayName;
  }
}
