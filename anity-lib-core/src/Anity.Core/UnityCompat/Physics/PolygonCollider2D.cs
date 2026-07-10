using System.Collections.Generic;

namespace UnityEngine;

/// <summary>
/// 2D polygon collider.
/// </summary>
public class PolygonCollider2D : Collider2D
{
    private Vector2[] _points = Array.Empty<Vector2>();

    public int pathCount { get; set; } = 1;

    public Vector2[] points
    {
        get => _points;
        set => _points = value ?? Array.Empty<Vector2>();
    }

    public Vector2[] GetPath(int index)
    {
        _ = index;
        return _points;
    }

    public void SetPath(int index, Vector2[] points)
    {
        _ = index;
        _points = points ?? Array.Empty<Vector2>();
    }

    public int GetPath(int index, List<Vector2> points)
    {
        _ = index;
        _ = points;
        return 0;
    }

    public void SetPath(int index, List<Vector2> points)
    {
        _ = index;
        _points = points?.ToArray() ?? Array.Empty<Vector2>();
    }

    public void CreatePrimitive(int sides)
    {
        _ = sides;
    }

    public void CreatePrimitive(int sides, Vector2 scale)
    {
        _ = sides;
        _ = scale;
    }

    public void CreatePrimitive(int sides, Vector2 scale, Vector2 offset)
    {
        _ = sides;
        _ = scale;
        _ = offset;
    }

    internal override ColliderShape2D GetShape()
    {
        return new ColliderShape2D(ColliderShapeType2D.Polygon, offset, Vector2.one, 0f);
    }
}
