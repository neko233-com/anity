using System;

namespace UnityEngine;

/// <summary>
/// RequireComponent attribute for requiring components on a GameObject.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class RequireComponentAttribute : Attribute
{
    public Type m_Type0 { get; }
    public Type? m_Type1 { get; }
    public Type? m_Type2 { get; }

    public RequireComponentAttribute(Type requiredComponent)
    {
        m_Type0 = requiredComponent;
    }

    public RequireComponentAttribute(Type requiredComponent, Type requiredComponent2)
    {
        m_Type0 = requiredComponent;
        m_Type1 = requiredComponent2;
    }

    public RequireComponentAttribute(Type requiredComponent, Type requiredComponent2, Type requiredComponent3)
    {
        m_Type0 = requiredComponent;
        m_Type1 = requiredComponent2;
        m_Type2 = requiredComponent3;
    }
}

/// <summary>
/// AddComponentMenu attribute for adding components to the Unity menu.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class AddComponentMenuAttribute : Attribute
{
    public string componentMenu { get; }
    public int componentOrder { get; set; }

    public AddComponentMenuAttribute(string menuName)
    {
        componentMenu = menuName;
    }

    public AddComponentMenuAttribute(string menuName, int order)
    {
        componentMenu = menuName;
        componentOrder = order;
    }
}

/// <summary>
/// DisallowMultipleComponent attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class DisallowMultipleComponentAttribute : Attribute { }

/// <summary>
/// ExecuteInEditMode attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class ExecuteInEditModeAttribute : Attribute { }

/// <summary>
/// ExecuteAlways attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class ExecuteAlwaysAttribute : Attribute { }

/// <summary>
/// HideInInspector attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class HideInInspectorAttribute : Attribute { }

/// <summary>
/// Tooltip attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class TooltipAttribute : Attribute
{
    public string tooltip { get; }

    public TooltipAttribute(string tooltip)
    {
        this.tooltip = tooltip;
    }
}

/// <summary>
/// Space attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class SpaceAttribute : Attribute
{
    public float height { get; }

    public SpaceAttribute()
    {
        height = 8f;
    }

    public SpaceAttribute(float height)
    {
        this.height = height;
    }
}

/// <summary>
/// Header attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class HeaderAttribute : Attribute
{
    public string header { get; }

    public HeaderAttribute(string header)
    {
        this.header = header;
    }
}

/// <summary>
/// Range attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class RangeAttribute : PropertyAttribute
{
    public float min;
    public float max;

    public RangeAttribute(float min, float max)
    {
        this.min = min;
        this.max = max;
    }
}

/// <summary>
/// Min attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class MinAttribute : PropertyAttribute
{
    public float min;

    public MinAttribute(float min)
    {
        this.min = min;
    }
}

/// <summary>
/// TextArea attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class TextAreaAttribute : PropertyAttribute
{
    public int minLines;
    public int maxLines;

    public TextAreaAttribute()
    {
        minLines = 3;
        maxLines = 3;
    }

    public TextAreaAttribute(int minLines, int maxLines)
    {
        this.minLines = minLines;
        this.maxLines = maxLines;
    }
}

/// <summary>
/// PropertyAttribute base class.
/// </summary>
public class PropertyAttribute : Attribute { }
