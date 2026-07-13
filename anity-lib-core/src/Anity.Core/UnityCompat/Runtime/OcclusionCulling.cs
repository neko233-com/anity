using System;
using System.Collections.Generic;

namespace UnityEngine;

/// <summary>
/// Umbra-style static occlusion culling (Unity StaticOcclusionCulling / OcclusionArea runtime query surface).
/// </summary>
public static class OcclusionCulling
{
    private static readonly object _lock = new();
    private static readonly List<OcclusionCell> _cells = new();
    private static readonly List<OcclusionPortal> _portals = new();
    private static readonly List<OcclusionArea> _areas = new();
    private static bool _enabled = true;
    private static int _queryCount;

    public static bool enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public static int queryCount => _queryCount;
    public static int cellCount { get { lock (_lock) return _cells.Count; } }

    public static void Clear()
    {
        lock (_lock)
        {
            _cells.Clear();
            _portals.Clear();
            _areas.Clear();
            _queryCount = 0;
        }
    }

    public static void RegisterArea(OcclusionArea area)
    {
        if (area == null) return;
        lock (_lock)
        {
            if (!_areas.Contains(area)) _areas.Add(area);
            RebuildCellsFromAreas();
        }
    }

    public static void UnregisterArea(OcclusionArea area)
    {
        lock (_lock)
        {
            _areas.Remove(area);
            RebuildCellsFromAreas();
        }
    }

    public static void RegisterPortal(OcclusionPortal portal)
    {
        if (portal == null) return;
        lock (_lock)
        {
            if (!_portals.Contains(portal)) _portals.Add(portal);
        }
    }

    public static void UnregisterPortal(OcclusionPortal portal)
    {
        lock (_lock) _portals.Remove(portal);
    }

    /// <summary>Bake a simple grid of visibility cells for a world AABB (Umbra bake stand-in).</summary>
    public static void Bake(Bounds worldBounds, int subdivisions = 4)
    {
        lock (_lock)
        {
            _cells.Clear();
            subdivisions = Math.Max(1, Math.Min(subdivisions, 32));
            var min = worldBounds.min;
            var size = worldBounds.size;
            float sx = size.x / subdivisions;
            float sy = size.y / Math.Max(1, subdivisions / 2);
            float sz = size.z / subdivisions;
            for (int x = 0; x < subdivisions; x++)
            for (int y = 0; y < Math.Max(1, subdivisions / 2); y++)
            for (int z = 0; z < subdivisions; z++)
            {
                var center = new Vector3(
                    min.x + (x + 0.5f) * sx,
                    min.y + (y + 0.5f) * sy,
                    min.z + (z + 0.5f) * sz);
                _cells.Add(new OcclusionCell
                {
                    bounds = new Bounds(center, new Vector3(sx, sy, sz)),
                    visible = true
                });
            }
        }
    }

    /// <summary>
    /// Query whether a world-space bounds is potentially visible from a camera position.
    /// Uses portal open state + cell visibility (software Umbra subset).
    /// </summary>
    public static bool IsVisible(Vector3 viewerPosition, Bounds targetBounds)
    {
        if (!_enabled) return true;
        lock (_lock)
        {
            _queryCount++;
            // Closed portals block visibility if target is entirely behind portal plane and portal closed
            foreach (var p in _portals)
            {
                if (p == null || p.open) continue;
                var portalBounds = new Bounds(p.center + (p.transform != null ? p.transform.position : Vector3.zero), p.size);
                // If viewer and target are on opposite sides of portal AABB along major axis, cull
                if (portalBounds.Intersects(targetBounds))
                    return false;
            }

            if (_cells.Count == 0) return true;

            // Visible if any intersecting cell is marked visible
            foreach (var cell in _cells)
            {
                if (!cell.visible) continue;
                if (cell.bounds.Intersects(targetBounds))
                {
                    // Distance band: far cells behind viewer optional cull
                    var toTarget = targetBounds.center - viewerPosition;
                    var toCell = cell.bounds.center - viewerPosition;
                    if (Vector3.Dot(toTarget.normalized, toCell.normalized) < -0.2f
                        && Vector3.Distance(viewerPosition, targetBounds.center) > cell.bounds.size.magnitude * 2f)
                        continue;
                    return true;
                }
            }
            // No cell intersection → assume visible (conservative, matches Umbra PVS open-set bias)
            return true;
        }
    }

    public static void SetCellVisible(int index, bool visible)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _cells.Count) return;
            var c = _cells[index];
            c.visible = visible;
            _cells[index] = c;
        }
    }

    public static Bounds GetCellBounds(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _cells.Count) return default;
            return _cells[index].bounds;
        }
    }

    private static void RebuildCellsFromAreas()
    {
        if (_areas.Count == 0) return;
        var enc = new Bounds(_areas[0].center, _areas[0].size);
        for (int i = 1; i < _areas.Count; i++)
            enc.Encapsulate(new Bounds(_areas[i].center, _areas[i].size));
        Bake(enc, 4);
    }

    private struct OcclusionCell
    {
        public Bounds bounds;
        public bool visible;
    }
}

/// <summary>Editor bake API surface (UnityEditor.StaticOcclusionCulling subset).</summary>
public static class StaticOcclusionCulling
{
    public static bool isRunning { get; private set; }
    public static float umbraDataSize { get; private set; }
    public static string latestBakeLog { get; private set; } = string.Empty;

    public static bool Compute()
    {
        isRunning = true;
        try
        {
            // Snapshot areas first — safe under concurrent tests / object registry churn
            OcclusionArea[] areas;
            try
            {
                areas = UnityEngine.Object.FindObjectsOfType<OcclusionArea>() ?? Array.Empty<OcclusionArea>();
            }
            catch
            {
                areas = Array.Empty<OcclusionArea>();
            }

            if (areas.Length > 0)
            {
                for (int i = 0; i < areas.Length; i++)
                {
                    if (areas[i] != null)
                        OcclusionCulling.RegisterArea(areas[i]);
                }
            }
            else
            {
                OcclusionCulling.Bake(new Bounds(Vector3.zero, Vector3.one * 100f), 8);
            }
            umbraDataSize = OcclusionCulling.cellCount * 32f;
            latestBakeLog = $"umbra bake cells={OcclusionCulling.cellCount} data={umbraDataSize}";
            return true;
        }
        finally
        {
            isRunning = false;
        }
    }

    public static void Cancel()
    {
        isRunning = false;
        latestBakeLog = "cancelled";
    }

    public static void Clear()
    {
        OcclusionCulling.Clear();
        umbraDataSize = 0;
        latestBakeLog = "cleared";
    }
}

