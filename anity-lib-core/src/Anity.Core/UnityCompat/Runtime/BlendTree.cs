using System;

namespace UnityEngine;

public class BlendTree : Motion
{
    public string blendParameter { get; set; } = string.Empty;
    public string blendParameterY { get; set; } = string.Empty;
    public BlendTreeType blendType { get; set; } = BlendTreeType.Simple1D;
    public float minThreshold { get; set; }
    public float maxThreshold { get; set; } = 1f;
    public bool useAutomaticThresholds { get; set; } = true;

    private ChildMotion[] _children = Array.Empty<ChildMotion>();

    public ChildMotion[] children
    {
        get => _children;
        set => _children = value ?? Array.Empty<ChildMotion>();
    }

    public void AddChild(Motion motion)
    {
        if (motion is null) return;
        Array.Resize(ref _children, _children.Length + 1);
        _children[^1] = new ChildMotion { motion = motion, threshold = 0f, timeScale = 1f };
    }

    public void RemoveChild(int index)
    {
        if (index < 0 || index >= _children.Length) return;
        var list = new System.Collections.Generic.List<ChildMotion>(_children);
        list.RemoveAt(index);
        _children = list.ToArray();
    }

    public Motion? CreateBlendTreeChild(float threshold)
    {
        var child = new BlendTree();
        AddChild(child);
        _children[^1].threshold = threshold;
        return child;
    }
}

public class Motion : UnityEngine.Object { }

public struct ChildMotion
{
    public Motion? motion;
    public float threshold;
    public float timeScale;
    public float cycleOffset;
    public string directBlendParameter;
    public bool mirror;
}

public enum BlendTreeType
{
    Simple1D = 0,
    SimpleDirectional2D = 1,
    FreeformDirectional2D = 2,
    FreeformCartesian2D = 3,
    Direct = 4
}
