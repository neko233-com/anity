using System;
using System.Linq;

namespace UnityEngine;

[Serializable]
public class Gradient
{
    private GradientColorKey[] _colorKeys;
    private GradientAlphaKey[] _alphaKeys;
    private GradientMode _mode;

    public GradientColorKey[] colorKeys
    {
        get => _colorKeys;
        set => _colorKeys = value != null && value.Length > 0 ? value : new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
    }

    public GradientAlphaKey[] alphaKeys
    {
        get => _alphaKeys;
        set => _alphaKeys = value != null && value.Length > 0 ? value : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
    }

    public GradientMode mode
    {
        get => _mode;
        set => _mode = value;
    }

    public Gradient()
    {
        _colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        _alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
        _mode = GradientMode.Blend;
    }

    public Color Evaluate(float time)
    {
        time = Mathf.Clamp01(time);

        if (_colorKeys.Length == 0) return Color.white;
        if (_colorKeys.Length == 1) return _colorKeys[0].color;

        var sortedColors = _colorKeys.OrderBy(k => k.time).ToArray();
        var sortedAlphas = _alphaKeys.OrderBy(k => k.time).ToArray();

        if (_mode == GradientMode.Fixed)
        {
            GradientColorKey ck = sortedColors[0];
            foreach (var key in sortedColors)
            {
                if (key.time <= time) ck = key;
                else break;
            }
            GradientAlphaKey ak = sortedAlphas[0];
            foreach (var key in sortedAlphas)
            {
                if (key.time <= time) ak = key;
                else break;
            }
            return new Color(ck.color.r, ck.color.g, ck.color.b, ak.alpha);
        }

        GradientColorKey c0 = sortedColors[0], c1 = sortedColors[sortedColors.Length - 1];
        for (int i = 0; i < sortedColors.Length - 1; i++)
        {
            if (time >= sortedColors[i].time && time <= sortedColors[i + 1].time)
            {
                c0 = sortedColors[i];
                c1 = sortedColors[i + 1];
                break;
            }
        }

        GradientAlphaKey a0 = sortedAlphas[0], a1 = sortedAlphas[sortedAlphas.Length - 1];
        for (int i = 0; i < sortedAlphas.Length - 1; i++)
        {
            if (time >= sortedAlphas[i].time && time <= sortedAlphas[i + 1].time)
            {
                a0 = sortedAlphas[i];
                a1 = sortedAlphas[i + 1];
                break;
            }
        }

        float colorT = 0f;
        if (c1.time != c0.time)
            colorT = (time - c0.time) / (c1.time - c0.time);
        colorT = Mathf.Clamp01(colorT);

        float alphaT = 0f;
        if (a1.time != a0.time)
            alphaT = (time - a0.time) / (a1.time - a0.time);
        alphaT = Mathf.Clamp01(alphaT);

        Color c = Color.Lerp(c0.color, c1.color, colorT);
        c.a = Mathf.Lerp(a0.alpha, a1.alpha, alphaT);
        return c;
    }

    public void SetKeys(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys)
    {
        _colorKeys = colorKeys != null && colorKeys.Length > 0
            ? (GradientColorKey[])colorKeys.Clone()
            : new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        _alphaKeys = alphaKeys != null && alphaKeys.Length > 0
            ? (GradientAlphaKey[])alphaKeys.Clone()
            : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
    }
}

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

public enum GradientMode
{
    Blend,
    Fixed,
    PerceptualBlend
}
