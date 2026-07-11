using System.Collections.Generic;

namespace UnityEngine;

public class PolygonCollider2D : Collider2D
{
    private List<Vector2[]> _paths = new();
    private Vector2[] _points = Array.Empty<Vector2>();

    public int pathCount
    {
        get => _paths.Count;
        set
        {
            while (_paths.Count < value) _paths.Add(Array.Empty<Vector2>());
            while (_paths.Count > value) _paths.RemoveAt(_paths.Count - 1);
        }
    }

    public Vector2[] points
    {
        get => _points;
        set
        {
            _points = value ?? Array.Empty<Vector2>();
            if (_paths.Count == 0) _paths.Add(_points);
            else _paths[0] = _points;
        }
    }

    public Vector2[] GetPath(int index)
    {
        if (index < 0 || index >= _paths.Count) return Array.Empty<Vector2>();
        return (Vector2[])_paths[index].Clone();
    }

    public void SetPath(int index, Vector2[] points)
    {
        if (index < 0) return;
        while (_paths.Count <= index) _paths.Add(Array.Empty<Vector2>());
        _paths[index] = points ?? Array.Empty<Vector2>();
        if (index == 0) _points = _paths[index];
    }

    public int GetPath(int index, List<Vector2> points)
    {
        if (points == null || index < 0 || index >= _paths.Count) return 0;
        points.Clear();
        points.AddRange(_paths[index]);
        return _paths[index].Length;
    }

    public void SetPath(int index, List<Vector2> points)
    {
        if (index < 0) return;
        while (_paths.Count <= index) _paths.Add(Array.Empty<Vector2>());
        _paths[index] = points?.ToArray() ?? Array.Empty<Vector2>();
        if (index == 0) _points = _paths[index];
    }

    public void CreatePrimitive(int sides)
    {
        CreatePrimitive(sides, Vector2.one, Vector2.zero);
    }

    public void CreatePrimitive(int sides, Vector2 scale)
    {
        CreatePrimitive(sides, scale, Vector2.zero);
    }

    public void CreatePrimitive(int sides, Vector2 scale, Vector2 offset)
    {
        sides = Math.Max(3, sides);
        var pts = new Vector2[sides];
        float angleStep = 2f * MathF.PI / sides;
        for (int i = 0; i < sides; i++)
        {
            float angle = i * angleStep - MathF.PI / 2f;
            pts[i] = new Vector2(
                MathF.Cos(angle) * scale.x * 0.5f + offset.x,
                MathF.Sin(angle) * scale.y * 0.5f + offset.y);
        }
        _points = pts;
        _paths.Clear();
        _paths.Add(_points);
    }

    internal override ColliderShape2D GetShape()
    {
        if (_points.Length == 0 && _paths.Count > 0) _points = _paths[0];
        return new ColliderShape2D(ColliderShapeType2D.Polygon, offset, Vector2.one, 0f, _points, CapsuleDirection2D.Vertical, _paths);
    }
}
