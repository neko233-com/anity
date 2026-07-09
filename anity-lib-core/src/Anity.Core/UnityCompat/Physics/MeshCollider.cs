using System;

namespace UnityEngine;

/// <summary>
/// Unity MeshCollider component.
/// </summary>
public class MeshCollider : Collider
{
    private Mesh? _sharedMesh;
    private bool _convex;
    private MeshColliderCookingOptions _cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.WeldColocatedVertices;

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
