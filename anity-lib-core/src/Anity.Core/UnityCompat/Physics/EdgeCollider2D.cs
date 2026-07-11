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
        set
        {
            _points = value ?? Array.Empty<Vector2>();
            _pointCount = _points.Length;
            _edgeCount = Math.Max(0, _points.Length - 1);
        }
    }

    public int edgeCount => Math.Max(0, _points.Length - 1);
    public int pointCount => _points.Length;

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
        if (points == null) return 0;
        points.Clear();
        points.AddRange(_points);
        return _points.Length;
    }

    public void SetPoints(List<Vector2> points)
    {
        _points = points?.ToArray() ?? Array.Empty<Vector2>();
        _pointCount = _points.Length;
        _edgeCount = Math.Max(0, _points.Length - 1);
    }

    public void Reset()
    {
        _points = new Vector2[] { new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f) };
        _pointCount = 2;
        _edgeCount = 1;
        _useAdjacentStartPoint = false;
        _useAdjacentEndPoint = false;
        _adjacentStartPoint = Vector2.zero;
        _adjacentEndPoint = Vector2.zero;
        edgeRadius = 0f;
    }

    internal override ColliderShape2D GetShape()
    {
        return new ColliderShape2D(ColliderShapeType2D.Edge, offset, Vector2.one, edgeRadius, _points);
    }
}
