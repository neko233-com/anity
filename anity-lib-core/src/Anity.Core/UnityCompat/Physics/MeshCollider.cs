using System;

namespace UnityEngine;

/// <summary>
/// Unity MeshCollider component.
/// </summary>
[AddComponentMenu("Physics/Mesh Collider")]
public class MeshCollider : Collider
{
    private Mesh? _sharedMesh;
    private bool _convex;
    private MeshColliderCookingOptions _cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices;

    public Mesh? sharedMesh
    {
        get => _sharedMesh;
        set => _sharedMesh = value;
    }

    public bool convex
    {
        get => _convex;
        set => _convex = value;
    }

    public MeshColliderCookingOptions cookingOptions
    {
        get => _cookingOptions;
        set => _cookingOptions = value;
    }
}

/// <summary>
/// MeshCollider cooking options.
/// </summary>
[Flags]
public enum MeshColliderCookingOptions
{
    None = 0,
    CookForFasterSimulation = 1,
    EnableMeshCleaning = 2,
    WeldColocatedVertices = 4,
    UseFastMidphase = 8
}
