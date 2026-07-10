using UnityEngine;

namespace UnityEngine;

[AddComponentMenu("Physics/Terrain Collider")]
public class TerrainCollider : Collider
{
    private TerrainData? _terrainData;

    public TerrainData? terrainData
    {
        get => _terrainData;
        set => _terrainData = value;
    }

    public override Vector3 ClosestPoint(Vector3 point)
    {
        return point;
    }

    public override bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance = 1000f)
    {
        hitInfo = new RaycastHit { point = ray.origin };
        return false;
    }
}
