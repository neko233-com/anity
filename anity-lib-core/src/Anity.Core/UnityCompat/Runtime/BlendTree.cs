using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine;

public abstract class Motion : Object
{
    public string name { get; set; } = string.Empty;
    public bool humanCycle { get; set; }
    public bool humanTranslation { get; set; }

    public virtual float duration
    {
        get { return averageDuration; }
    }

    public virtual float averageDuration
    {
        get
        {
            if (this is AnimationClip clip) return clip.length;
            if (this is BlendTree bt) return ComputeAverageDuration(bt);
            return 0f;
        }
    }

    public virtual float averageAngularSpeed { get; set; }
    public virtual Vector3 averageSpeed { get; set; } = Vector3.zero;

    public int ComputeHashCode()
    {
        return name?.GetHashCode() ?? 0;
    }

    private float ComputeAverageDuration(BlendTree bt)
    {
        if (bt.children == null || bt.children.Length == 0) return 0f;
        float total = 0f;
        int count = 0;
        foreach (var child in bt.children)
        {
            if (child.motion != null)
            {
                total += child.motion.averageDuration;
                count++;
            }
        }
        return count > 0 ? total / count : 0f;
    }
}

public class BlendTree : Motion
{
    public string blendParameter { get; set; } = string.Empty;
    public string blendParameterY { get; set; } = string.Empty;
    public BlendTreeType blendType { get; set; } = BlendTreeType.Simple1D;
    public float minThreshold { get; set; }
    public float maxThreshold { get; set; } = 1f;
    public bool useAutomaticThresholds { get; set; } = true;

    private ChildMotion[] _children = Array.Empty<ChildMotion>();
    private bool _synced;

    public void Sync()
    {
        _synced = true;
    }

    public ChildMotion[] children
    {
        get => _children;
        set => _children = value ?? Array.Empty<ChildMotion>();
    }

    public BlendTreeChild[] childrenSerializable
    {
        get => _children.Select(c => new BlendTreeChild { motion = c.motion, threshold = c.threshold, position = c.position, mirror = c.mirror, timeScale = c.timeScale }).ToArray();
        set
        {
            if (value == null) { _children = Array.Empty<ChildMotion>(); return; }
            _children = value.Select(c => new ChildMotion { motion = c.motion, threshold = c.threshold, position = c.position, mirror = c.mirror, timeScale = c.timeScale, cycleOffset = 0f, directBlendParameter = string.Empty }).ToArray();
        }
    }

    public void AddChild(Motion motion)
    {
        if (motion is null) return;
        Array.Resize(ref _children, _children.Length + 1);
        _children[^1] = new ChildMotion { motion = motion, threshold = 0f, timeScale = 1f };
    }

    public void AddChild(Motion motion, float threshold)
    {
        if (motion is null) return;
        Array.Resize(ref _children, _children.Length + 1);
        _children[^1] = new ChildMotion { motion = motion, threshold = threshold, timeScale = 1f };
    }

    public void AddChild(Motion motion, Vector2 position)
    {
        if (motion is null) return;
        Array.Resize(ref _children, _children.Length + 1);
        _children[^1] = new ChildMotion { motion = motion, position = position, timeScale = 1f };
    }

    public void RemoveChild(int index)
    {
        if (index < 0 || index >= _children.Length) return;
        var list = new List<ChildMotion>(_children);
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

    public Motion? CreateBlendTreeChild(Vector2 position)
    {
        var child = new BlendTree();
        AddChild(child, position);
        return child;
    }

    public void SetThresholds(float[] thresholds)
    {
        for (int i = 0; i < thresholds.Length && i < _children.Length; i++)
        {
            _children[i].threshold = thresholds[i];
        }
    }

    public void SetDirectBlendTreeThresholds(string[] parameterNames)
    {
        for (int i = 0; i < parameterNames.Length && i < _children.Length; i++)
        {
            _children[i].directBlendParameter = parameterNames[i];
        }
    }

    public void ComputeBlendTreeWeights(float x, float y, float[] weights)
    {
        if (weights == null || _children.Length == 0) return;
        Array.Clear(weights, 0, weights.Length);

        switch (blendType)
        {
            case BlendTreeType.Simple1D:
                Compute1DWeights(x, weights);
                break;
            case BlendTreeType.SimpleDirectional2D:
            case BlendTreeType.FreeformDirectional2D:
            case BlendTreeType.FreeformCartesian2D:
                Compute2DWeights(x, y, weights);
                break;
            case BlendTreeType.Direct:
                ComputeDirectWeights(weights);
                break;
        }
    }

    private void Compute1DWeights(float param, float[] weights)
    {
        if (_children.Length == 0) return;
        if (_children.Length == 1)
        {
            weights[0] = 1f;
            return;
        }

        var sorted = _children.Select((c, i) => (c, i)).OrderBy(x => x.c.threshold).ToArray();

        if (param <= sorted[0].c.threshold)
        {
            weights[sorted[0].i] = 1f;
            return;
        }

        if (param >= sorted[^1].c.threshold)
        {
            weights[sorted[^1].i] = 1f;
            return;
        }

        for (int i = 0; i < sorted.Length - 1; i++)
        {
            var a = sorted[i];
            var b = sorted[i + 1];
            if (param >= a.c.threshold && param <= b.c.threshold)
            {
                float range = b.c.threshold - a.c.threshold;
                if (MathF.Abs(range) < 1e-6f)
                {
                    weights[a.i] = 0.5f;
                    weights[b.i] = 0.5f;
                }
                else
                {
                    float t = (param - a.c.threshold) / range;
                    weights[a.i] = 1f - t;
                    weights[b.i] = t;
                }
                return;
            }
        }
    }

    private void Compute2DWeights(float x, float y, float[] weights)
    {
        if (_children.Length == 0) return;
        if (_children.Length == 1)
        {
            weights[0] = 1f;
            return;
        }

        float totalWeight = 0f;
        float[] distances = new float[_children.Length];

        for (int i = 0; i < _children.Length; i++)
        {
            var pos = _children[i].position;
            float dx = x - pos.x;
            float dy = y - pos.y;
            float distSq = dx * dx + dy * dy;
            distances[i] = distSq;
        }

        int nearestIdx = 0;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < distances.Length; i++)
        {
            if (distances[i] < nearestDist)
            {
                nearestDist = distances[i];
                nearestIdx = i;
            }
        }

        if (nearestDist < 1e-6f)
        {
            weights[nearestIdx] = 1f;
            return;
        }

        for (int i = 0; i < _children.Length; i++)
        {
            float w = 1f / (distances[i] + 1e-6f);
            weights[i] = w;
            totalWeight += w;
        }

        if (totalWeight > 0f)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] /= totalWeight;
            }
        }
    }

    private void ComputeDirectWeights(float[] weights)
    {
        for (int i = 0; i < weights.Length && i < _children.Length; i++)
        {
            weights[i] = 1f;
        }
    }
}

public struct ChildMotion
{
    public Motion? motion;
    public float threshold;
    public Vector2 position;
    public float timeScale;
    public float cycleOffset;
    public string directBlendParameter;
    public bool mirror;
}

public struct BlendTreeChild
{
    public Motion? motion;
    public float threshold;
    public Vector2 position;
    public bool mirror;
    public float timeScale;
}

public enum BlendTreeType
{
    Simple1D = 0,
    SimpleDirectional2D = 1,
    FreeformDirectional2D = 2,
    FreeformCartesian2D = 3,
    Direct = 4
}
