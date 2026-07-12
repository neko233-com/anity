using System;

namespace UnityEngine
{
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

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class DisallowMultipleComponentAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ExecuteInEditModeAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ExecuteAlwaysAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class HideInInspectorAttribute : Attribute { }

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
