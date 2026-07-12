using System;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MultilineAttribute : PropertyAttribute
    {
        public int lines { get; }
        public MultilineAttribute() { lines = 3; }
        public MultilineAttribute(int lines) { this.lines = lines; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DelayedAttribute : PropertyAttribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ContextMenuAttribute : Attribute
    {
        public string menuItem { get; }
        public bool validate { get; set; }
        public int priority { get; set; }

        public ContextMenuAttribute(string itemName)
        {
            menuItem = itemName;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class TooltipAttribute : PropertyAttribute
    {
        public string tooltip { get; }
        public TooltipAttribute(string tooltip) { this.tooltip = tooltip; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Delegate | AttributeTargets.Enum | AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Struct, Inherited = false)]
    public sealed class BeforeFieldInitAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class ImageEffectAfterStackAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class ImageEffectUsesLinearColorSpaceAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class PreferBinarySerializationAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName { get; set; }
        public string menuName { get; set; }
        public int order { get; set; }

        public CreateAssetMenuAttribute() { fileName = string.Empty; menuName = string.Empty; }
    }
}

namespace UnityEngine.Serialization
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class FormerlySerializedAsAttribute : Attribute
    {
        public string oldName { get; }
        public FormerlySerializedAsAttribute(string oldName) { this.oldName = oldName; }
    }
}
