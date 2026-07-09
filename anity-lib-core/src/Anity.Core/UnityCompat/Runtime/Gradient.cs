using System;

namespace UnityEngine;

/// <summary>
/// Gradient class for color gradients.
/// </summary>
[Serializable]
public class Gradient
{
    private GradientColorKey[] _colorKeys = Array.Empty<GradientColorKey>();
    private GradientAlphaKey[] _alphaKeys = Array.Empty<GradientAlphaKey>();
    private GradientMode _mode;

    public GradientColorKey[] colorKeys
    {
        get => _colorKeys;
        set => _colorKeys = value ?? Array.Empty<GradientColorKey>();
    }

    public GradientAlphaKey[] alphaKeys
    {
        get => _alphaKeys;
        set => _alphaKeys = value ?? Array.Empty<GradientAlphaKey>();
    }

    public GradientMode mode
    {
        get => _mode;
        set => _mode = value;
    }

    public Color Evaluate(float time)
    {
        if (_colorKeys.Length == 0) return Color.white;
        return _colorKeys[0].color;
    }

    public void SetKeys(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys)
    {
        _colorKeys = colorKeys ?? Array.Empty<GradientColorKey>();
        _alphaKeys = alphaKeys ?? Array.Empty<GradientAlphaKey>();
    }
}

/// <summary>
/// Gradient color key.
/// </summary>
[Serializable]
public struct GradientColorKey
{
    public Color color;
    public float time;

    public GradientColorKey(Color col, float tim)
    {
        color = col;
        time = tim;
    }
}

/// <summary>
/// Gradient alpha key.
/// </summary>
[Serializable]
public struct GradientAlphaKey
{
    public float alpha;
    public float time;

    public GradientAlphaKey(float a, float t)
    {
        alpha = a;
        time = t;
    }
}

/// <summary>
/// Gradient mode.
/// </summary>
public enum GradientMode
{
    Blend,
    Fixed,
    PerceptualBlend
}
