using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace UnityEditor.AI;

public class NavMeshBuildSettings
{
    public float agentRadius { get; set; } = 0.5f;
    public float agentHeight { get; set; } = 2f;
    public float agentSlope { get; set; } = 45f;
    public float agentClimb { get; set; } = 0.4f;
    public float voxelSize { get; set; } = 0.166f;
    public float tileSize { get; set; } = 256f;
    public float minRegionArea { get; set; } = 2f;
    public int agentTypeID { get; set; }
}

public class NavMeshBuildSource
{
    public Object? sourceObject;
    public Matrix4x4 transform = Matrix4x4.identity;
    public NavMeshBuildSourceShape shape;
    public int area;
}

public enum NavMeshBuildSourceShape
{
    Mesh,
    Terrain,
    Box,
    Sphere,
    Capsule,
    ModifierBox
}

public static class NavMeshBuilder
{
    private static readonly List<NavMeshData> _builtNavMeshes = new();
    private static readonly NavMeshBuildSettings _buildSettings = new();

    public static NavMeshBuildSettings GetBuildSettings()
    {
        return _buildSettings;
    }

    public static void BuildNavMesh()
    {
        var data = NavMesh.BuildNavMesh();
        _builtNavMeshes.Add(data);
    }

    public static void BuildNavMesh(float voxelSize, float tileSize, float minRegionArea)
    {
        _buildSettings.voxelSize = voxelSize;
        _buildSettings.tileSize = tileSize;
        _buildSettings.minRegionArea = minRegionArea;
        BuildNavMesh();
    }

    public static AsyncOperation BuildNavMeshAsync()
    {
        var op = new AsyncOperation();
        BuildNavMesh();
        op.SetDone();
        return op;
    }

    public static void ClearAllNavMeshes()
    {
        foreach (var data in _builtNavMeshes)
        {
            NavMesh.RemoveNavMeshData(data);
        }
        _builtNavMeshes.Clear();
    }

    public static void CollectSources(
        Bounds includedWorldBounds,
        int includedLayerMask,
        List<NavMeshBuildSource> sources,
        int defaultArea = 0)
    {
        _ = includedWorldBounds;
        _ = includedLayerMask;
        _ = defaultArea;
        sources?.Clear();
    }

    public static bool BuildNavMesh(
        List<NavMeshBuildSource> sources,
        Bounds localBounds,
        NavMeshBuildSettings buildSettings,
        NavMeshData data)
    {
        _ = sources;
        _ = localBounds;
        _ = buildSettings;
        if (data != null)
        {
            NavMesh.AddNavMeshData(data);
        }
        return true;
    }
}
