using UnityEngine.Tilemaps;

namespace UnityEngine;

[AddComponentMenu("Physics 2D/Tilemap Collider 2D")]
public class TilemapCollider2D : Collider2D
{
    private bool _usedByComposite;
    private float _maximumTileChangeCount = 2000f;
    private List<Vector2[]> _tileShapes = new();
    private Vector2[] _cachedPoints = Array.Empty<Vector2>();

    public bool usedByComposite { get => _usedByComposite; set => _usedByComposite = value; }
    public float maximumTileChangeCount { get => _maximumTileChangeCount; set => _maximumTileChangeCount = value; }
    public int shapeCount => _tileShapes.Count;
    public bool hasTilemapChanges => false;

    public void ProcessTilemapChanges()
    {
        _tileShapes.Clear();
        var tilemap = GetComponent<Tilemap>();
        if (tilemap == null)
        {
            _cachedPoints = new Vector2[]
            {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-0.5f, 0.5f)
            };
            _tileShapes.Add(_cachedPoints);
            return;
        }

        var bounds = tilemap.cellBounds;
        var allPoints = new List<Vector2>();
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                var tile = tilemap.GetTile(pos);
                if (tile != null)
                {
                    Vector2 tileCenter = new Vector2(x + 0.5f, y + 0.5f);
                    Vector2 half = Vector2.one * 0.5f;
                    var box = new Vector2[]
                    {
                        tileCenter + new Vector2(-half.x, -half.y),
                        tileCenter + new Vector2(half.x, -half.y),
                        tileCenter + new Vector2(half.x, half.y),
                        tileCenter + new Vector2(-half.x, half.y)
                    };
                    _tileShapes.Add(box);
                    allPoints.AddRange(box);
                }
            }
        }

        if (allPoints.Count == 0)
        {
            _cachedPoints = new Vector2[]
            {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-0.5f, 0.5f)
            };
            _tileShapes.Add(_cachedPoints);
        }
        else
        {
            _cachedPoints = allPoints.ToArray();
        }
    }

    internal override ColliderShape2D GetShape()
    {
        if (_cachedPoints.Length == 0) ProcessTilemapChanges();
        return new ColliderShape2D(ColliderShapeType2D.Polygon, offset, Vector2.one, 0f, _cachedPoints, CapsuleDirection2D.Vertical, _tileShapes);
    }
}
