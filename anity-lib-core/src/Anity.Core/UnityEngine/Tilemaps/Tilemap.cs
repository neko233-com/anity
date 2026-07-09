using System;

namespace UnityEngine.Tilemaps;

/// <summary>
/// Unity Tilemap component for 2D tile-based rendering.
/// </summary>
[AddComponentMenu("2D Object/Tilemap")]
public class Tilemap : TileBase
{
    public Vector3Int size { get; set; }
    public Vector3Int origin { get; set; }
    public float tileSize { get; set; } = 1.0f;
    public TileOrientation tileOrientation { get; set; }

    public void SetTile(Vector3Int position, TileBase tile) { }
    public TileBase GetTile(Vector3Int position) => null;
    public void RefreshAllTiles() { }
    public void RefreshTile(Vector3Int position) { }
    public void ClearAllTiles() { }
    public BoundsInt GetBounds() => default;
}

public enum TileOrientation
{
    FlipX,
    FlipY,
    Rot90,
    Rot180,
    Rot270
}

public class TileBase : UnityEngine.Object
{
    public virtual void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData) { }
    public virtual void GetTileData(Vector3Int position, Tilemap tilemap, ref TileData tileData) { }
}

public class Tile : TileBase
{
    public Sprite sprite { get; set; }
    public Color color { get; set; } = Color.white;
    public Matrix4x4 transformMatrix { get; set; } = Matrix4x4.identity;
    public GameObject instantiatedGameObject { get; set; }
    public TileFlags flags { get; set; }
    public TileColliderType colliderType { get; set; }
}

public struct TileData
{
    public Sprite sprite;
    public Color color;
    public Matrix4x4 transform;
    public GameObject gameObject;
    public TileFlags flags;
    public TileColliderType colliderType;
}

[Flags]
public enum TileFlags
{
    None = 0,
    LockColor = 1,
    LockTransform = 2,
    InstantiateGameObject = 4,
    HideFlags = 8
}

public enum TileColliderType
{
    None,
    Grid,
    Sprite
}

public interface ITilemap
{
    Tilemap tilemap { get; }
    Vector3Int origin { get; }
    Vector3Int size { get; }
    float tileSize { get; }
    TileOrientation tileOrientation { get; }
}
