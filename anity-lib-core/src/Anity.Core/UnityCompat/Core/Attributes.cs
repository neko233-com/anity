using System;

namespace UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public sealed class SerializeField : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
public sealed class HideInInspector : Attribute { }
