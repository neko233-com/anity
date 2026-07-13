using System;
using System.Collections.Generic;

namespace UnityEngine;

/// <summary>Wind field evaluation across all active WindZones (Unity tree/grass wind).</summary>
public static class Wind
{
    private static readonly List<WindZone> _zones = new();
    private static readonly object _lock = new();

    public static void Register(WindZone z)
    {
        if (z == null) return;
        lock (_lock)
        {
            if (!_zones.Contains(z)) _zones.Add(z);
        }
    }

    public static void Unregister(WindZone z)
    {
        lock (_lock) _zones.Remove(z);
    }

    public static void Clear()
    {
        lock (_lock) _zones.Clear();
    }

    public static int zoneCount { get { lock (_lock) return _zones.Count; } }

    /// <summary>World-space wind velocity at position (m/s scale).</summary>
    public static Vector3 GetWindAt(Vector3 position)
    {
        Vector3 sum = Vector3.zero;
        float t = Time.time;
        lock (_lock)
        {
            foreach (var z in _zones)
            {
                if (z == null || !z.enabled || z.gameObject == null || !z.gameObject.activeInHierarchy)
                    continue;
                sum += EvaluateZone(z, position, t);
            }
        }
        return sum;
    }

    public static float GetWindMainAt(Vector3 position) => GetWindAt(position).magnitude;

    internal static Vector3 EvaluateZone(WindZone z, Vector3 position, float time)
    {
        var tr = z.transform;
        Vector3 dir = tr != null ? tr.forward : Vector3.forward;
        float pulse = 1f + z.windPulseMagnitude * MathF.Sin(time * (1f + z.windPulseFrequency * 60f));
        float turb = 1f + z.windTurbulence * 0.1f * MathF.Sin(time * 3.1f + position.x * 0.05f);
        float strength = z.windMain * pulse * turb;

        if (z.mode == WindZoneMode.Spherical)
        {
            Vector3 center = tr != null ? tr.position : Vector3.zero;
            Vector3 to = position - center;
            float dist = to.magnitude;
            if (dist > z.radius || z.radius <= 0f) return Vector3.zero;
            float atten = 1f - dist / z.radius;
            atten *= atten;
            if (dist > 1e-4f) dir = to / dist;
            strength *= atten;
        }

        return dir.normalized * strength;
    }
}
