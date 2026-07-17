using System;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    [Scripting.RequiredByNativeCode]
    public sealed class RequireComponent : Attribute
    {
        public Type m_Type0;
        public Type? m_Type1;
        public Type? m_Type2;

        public RequireComponent(Type requiredComponent)
        {
            m_Type0 = requiredComponent;
        }

        public RequireComponent(Type requiredComponent, Type requiredComponent2)
        {
            m_Type0 = requiredComponent;
            m_Type1 = requiredComponent2;
        }

        public RequireComponent(Type requiredComponent, Type requiredComponent2, Type requiredComponent3)
        {
            m_Type0 = requiredComponent;
            m_Type1 = requiredComponent2;
            m_Type2 = requiredComponent3;
        }
    }

    public sealed class AddComponentMenu : Attribute
    {
        public string componentMenu { get; }
        public int componentOrder { get; }

        public AddComponentMenu(string menuName)
        {
            componentMenu = menuName;
        }

        public AddComponentMenu(string menuName, int order)
        {
            componentMenu = menuName;
            componentOrder = order;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    [Scripting.RequiredByNativeCode]
    public sealed class DisallowMultipleComponent : Attribute { }

    [Scripting.UsedByNativeCode]
    public sealed class ExecuteInEditMode : Attribute { }

    [Scripting.UsedByNativeCode]
    public sealed class ExecuteAlways : Attribute { }

    [Scripting.UsedByNativeCode]
    public sealed class HideInInspector : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    [Scripting.UsedByNativeCode]
    public class DefaultExecutionOrder : Attribute
    {
        public int order { get; }

        public DefaultExecutionOrder(int order)
        {
            this.order = order;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    [Scripting.UsedByNativeCode]
    public class HelpURLAttribute : Attribute
    {
        public string URL { get; }

        public HelpURLAttribute(string url)
        {
            URL = url;
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class ContextMenuItemAttribute : PropertyAttribute
    {
        public readonly string name;
        public readonly string function;

        public ContextMenuItemAttribute(string name, string function)
        {
            this.name = name;
            this.function = function;
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class SpaceAttribute : PropertyAttribute
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

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class HeaderAttribute : PropertyAttribute
    {
        public string header { get; }

        public HeaderAttribute(string header)
        {
            this.header = header;
        }
    }

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

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class MinAttribute : PropertyAttribute
    {
        public float min;

        public MinAttribute(float min)
        {
            this.min = min;
        }
    }

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

    public class PropertyAttribute : Attribute { }
}

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class InitializeOnLoadMethodAttribute : Attribute
    {
        public InitializeOnLoadMethodAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class InitializeOnLoadAttribute : Attribute
    {
        public InitializeOnLoadAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class OnOpenAssetAttribute : Attribute
    {
        public int order { get; set; }
        public OnOpenAssetAttribute() { }
        public OnOpenAssetAttribute(int order) { this.order = order; }
    }
}
