using System;

namespace UnityEngine;

[AddComponentMenu("Physics 2D/Tilemap Collider 2D")]
public class TilemapCollider2D : Collider2D
{
    private bool _usedByComposite;
    private float _extrusionFactor;

    public bool usedByComposite { get => _usedByComposite; set => _usedByComposite = value; }
    public int shapeCount => 0;

    public bool hasTilemapChanges => false;

    public void ProcessTilemapChanges() { }

    internal override ColliderShape2D GetShape()
    {
        return new ColliderShape2D(ColliderShapeType2D.Box, offset, Vector2.one, 0.5f);
    }
}

public class TileBase : Object
{
    public virtual bool GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData) => false;
    public virtual void GetTileData(Vector3Int position, ITilemap tilemap, ref Sprite sprite, ref Color color, ref Matrix4x4 transform) { }
    public virtual bool StartUp(Vector3Int position, ITilemap tilemap, GameObject go) => false;
}

public class Tile : TileBase
{
    public Sprite? sprite { get; set; }
    public Color color { get; set; } = Color.white;
    public Matrix4x4 transform { get; set; } = Matrix4x4.identity;
    public GameObject? gameobject { get; set; }
    public TileFlags flags { get; set; } = TileFlags.None;
    public Tile.ColliderType colliderType { get; set; } = Tile.ColliderType.Sprite;

    public enum ColliderType
    {
        None,
        Sprite,
        Grid
    }
}

public enum TileFlags
{
    None = 0,
    LockColor = 1,
    LockTransform = 2,
    LockAll = LockColor | LockTransform
}

public struct TileData
{
    public Sprite? sprite;
    public Color color;
    public Matrix4x4 transform;
    public GameObject? gameObject;
    public TileFlags flags;
    public Tile.ColliderType colliderType;
}

public interface ITilemap
{
    Vector3Int origin { get; }
    Vector3Int size { get; }
    Bounds localBounds { get; }
    Sprite? GetSprite(Vector3Int position);
    Color GetColor(Vector3Int position);
    Matrix4x4 GetTransformMatrix(Vector3Int position);
    TileFlags GetTileFlags(Vector3Int position);
    GameObject? GetInstantiatedObject(Vector3Int position);
    void RefreshTile(Vector3Int position);
}

