using System.Collections.Generic;

namespace UnityEngine;

/// <summary>
/// 2D edge collider.
/// </summary>
public class EdgeCollider2D : Collider2D
{
    private Vector2[] _points = Array.Empty<Vector2>();
    private bool _useAdjacentStartPoint;
    private bool _useAdjacentEndPoint;
    private Vector2 _adjacentStartPoint;
    private Vector2 _adjacentEndPoint;
    private int _edgeCount = 1;
    private int _pointCount;

    public Vector2[] points
    {
        get => _points;
        set => _points = value ?? Array.Empty<Vector2>();
    }

    public int edgeCount => _edgeCount;
    public int pointCount => _pointCount;

    public bool useAdjacentStartPoint
    {
        get => _useAdjacentStartPoint;
        set => _useAdjacentStartPoint = value;
    }

    public bool useAdjacentEndPoint
    {
        get => _useAdjacentEndPoint;
        set => _useAdjacentEndPoint = value;
    }

    public Vector2 adjacentStartPoint
    {
        get => _adjacentStartPoint;
        set => _adjacentStartPoint = value;
    }

    public Vector2 adjacentEndPoint
    {
        get => _adjacentEndPoint;
        set => _adjacentEndPoint = value;
    }

    public float edgeRadius { get; set; }

    public int GetPoints(List<Vector2> points)
    {
        _ = points;
        return 0;
    }

    public void SetPoints(List<Vector2> points)
    {
        _points = points?.ToArray() ?? Array.Empty<Vector2>();
    }

    public void Reset() { }

    internal override ColliderShape2D GetShape()
    {
        return new ColliderShape2D(ColliderShapeType2D.Edge, offset, Vector2.one, edgeRadius, _points);
    }
}
