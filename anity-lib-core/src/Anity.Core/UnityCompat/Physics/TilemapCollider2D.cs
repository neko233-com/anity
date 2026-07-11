using UnityEngine.Tilemaps;

namespace UnityEngine;

[AddComponentMenu("Physics 2D/Tilemap Collider 2D")]
public class TilemapCollider2D : Collider2D
{
    private bool _usedByComposite;
    private float _maximumTileChangeCount = 2000f;

    public bool usedByComposite { get => _usedByComposite; set => _usedByComposite = value; }
    public float maximumTileChangeCount { get => _maximumTileChangeCount; set => _maximumTileChangeCount = value; }
    public int shapeCount => 0;
    public bool hasTilemapChanges => false;

    public void ProcessTilemapChanges() { }

    internal override ColliderShape2D GetShape()
    {
        return new ColliderShape2D(ColliderShapeType2D.Box, offset, Vector2.one, 0.5f);
    }
}
