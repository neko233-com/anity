using System;
using System.Collections.Generic;

namespace UnityEngine.Tilemaps;

[AddComponentMenu("2D Object/Tilemap")]
public class Tilemap : GridLayout
{
    private readonly Dictionary<Vector3Int, TileBase> _tiles = new();
    private Vector3Int _origin;
    private Vector3Int _size;
    private Matrix4x4 _transformMatrix = Matrix4x4.identity;
    private TilemapRenderer _renderer;

    public Vector3Int origin => _origin;
    public Vector3Int size => _size;
    public BoundsInt cellBounds
    {
        get
        {
            if (_tiles.Count == 0) return new BoundsInt(Vector3Int.zero, Vector3Int.zero);
            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
            foreach (var pos in _tiles.Keys)
            {
                if (pos.x < minX) minX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.z < minZ) minZ = pos.z;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y > maxY) maxY = pos.y;
                if (pos.z > maxZ) maxZ = pos.z;
            }
            return new BoundsInt(new Vector3Int(minX, minY, minZ), new Vector3Int(maxX - minX + 1, maxY - minY + 1, maxZ - minZ + 1));
        }
    }

    public TileBase this[Vector3Int position]
    {
        get => GetTile(position);
        set => SetTile(position, value);
    }

    public void SetTile(Vector3Int position, TileBase tile)
    {
        if (tile == null)
        {
            _tiles.Remove(position);
        }
        else
        {
            _tiles[position] = tile;
        }
        UpdateBounds();
    }

    public void SetTiles(Vector3Int[] positionArray, TileBase[] tileArray)
    {
        if (positionArray == null || tileArray == null) return;
        for (int i = 0; i < positionArray.Length && i < tileArray.Length; i++)
        {
            SetTile(positionArray[i], tileArray[i]);
        }
    }

    public TileBase GetTile(Vector3Int position)
    {
        _tiles.TryGetValue(position, out var tile);
        return tile;
    }

    public T GetTile<T>(Vector3Int position) where T : TileBase
    {
        return GetTile(position) as T;
    }

    public bool HasTile(Vector3Int position)
    {
        return _tiles.ContainsKey(position);
    }

    public void RefreshTile(Vector3Int position)
    {
        _ = position;
    }

    public void RefreshAllTiles() { }

    public void SwapTile(TileBase changeTile, TileBase newTile)
    {
        if (changeTile == null || newTile == null) return;
        var positions = new List<Vector3Int>(_tiles.Keys);
        foreach (var pos in positions)
        {
            if (_tiles[pos] == changeTile)
            {
                _tiles[pos] = newTile;
            }
        }
    }

    public void RemoveTile(Vector3Int position)
    {
        _tiles.Remove(position);
        UpdateBounds();
    }

    public void ClearAllTiles()
    {
        _tiles.Clear();
        _origin = Vector3Int.zero;
        _size = Vector3Int.zero;
    }

    public void FloodFill(Vector3Int position, TileBase tile)
    {
        if (tile == null) return;
        var targetTile = GetTile(position);
        if (targetTile == tile) return;

        var queue = new Queue<Vector3Int>();
        queue.Enqueue(position);
        var visited = new HashSet<Vector3Int> { position };

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            if (GetTile(pos) != targetTile) continue;
            SetTile(pos, tile);

            var neighbors = new[]
            {
                pos + Vector3Int.right,
                pos + Vector3Int.left,
                pos + Vector3Int.up,
                pos + Vector3Int.down
            };
            foreach (var n in neighbors)
            {
                if (visited.Add(n)) queue.Enqueue(n);
            }
        }
    }

    public void BoxFill(Vector3Int position, TileBase tile, Vector3Int size)
    {
        for (int x = 0; x < size.x; x++)
        for (int y = 0; y < size.y; y++)
        for (int z = 0; z < size.z; z++)
        {
            SetTile(position + new Vector3Int(x, y, z), tile);
        }
    }

    public void InsertCells(Vector3Int position, Vector3Int insertCells)
    {
        _ = position;
        _ = insertCells;
    }

    public void DeleteCells(Vector3Int position, Vector3Int deleteCells)
    {
        _ = position;
        _ = deleteCells;
    }

    public void SetTransformMatrix(Vector3Int position, Matrix4x4 matrix)
    {
        _transformMatrix = matrix;
    }

    public Matrix4x4 GetTransformMatrix(Vector3Int position)
    {
        return _transformMatrix;
    }

    public void CompressBounds()
    {
        _ = cellBounds;
    }

    public Vector3 GetCellCenterLocal(Vector3Int position)
    {
        return (Vector3)position + new Vector3(0.5f, 0.5f, 0.5f);
    }

    public Vector3 GetCellCenterWorld(Vector3Int position)
    {
        var local = GetCellCenterLocal(position);
        return transform.localToWorldMatrix.MultiplyPoint(local);
    }

    private void UpdateBounds()
    {
        if (_tiles.Count == 0)
        {
            _origin = Vector3Int.zero;
            _size = Vector3Int.zero;
            return;
        }
        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
        foreach (var pos in _tiles.Keys)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.z < minZ) minZ = pos.z;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y > maxY) maxY = pos.y;
            if (pos.z > maxZ) maxZ = pos.z;
        }
        _origin = new Vector3Int(minX, minY, minZ);
        _size = new Vector3Int(maxX - minX + 1, maxY - minY + 1, maxZ - minZ + 1);
    }
}

public class GridLayout : Component
{
    public Vector3 cellSize { get; set; } = Vector3.one;
    public Vector3 cellGap { get; set; } = Vector3.zero;
    public CellLayout cellLayout { get; set; } = CellLayout.Rectangle;
    public CellSwizzle cellSwizzle { get; set; } = CellSwizzle.XYZ;

    public Vector3 CellToLocal(Vector3Int cellPosition) => (Vector3)cellPosition;
    public Vector3Int LocalToCell(Vector3 localPosition) => new Vector3Int((int)localPosition.x, (int)localPosition.y, (int)localPosition.z);
    public Vector3 CellToWorld(Vector3Int cellPosition) => transform.localToWorldMatrix.MultiplyPoint3x4(CellToLocal(cellPosition));
    public Vector3Int WorldToCell(Vector3 worldPosition) => LocalToCell(transform.worldToLocalMatrix.MultiplyPoint3x4(worldPosition));
}

public class TilemapRenderer : Renderer
{
    public SortOrder sortOrder { get; set; }
    public Tilemap tilemap { get; set; }
}

public class TileBase : ScriptableObject
{
    public virtual void RefreshTile(Vector3Int position, ITilemap tilemap) { }
    public virtual void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData) { }
    public virtual bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData) => false;
    public virtual bool StartUp(Vector3Int position, ITilemap tilemap, GameObject go) => false;
}

public class Tile : TileBase
{
    public Sprite sprite { get; set; }
    public Color color { get; set; } = Color.white;
    public Matrix4x4 transform { get; set; } = Matrix4x4.identity;
    public GameObject instantiatedGameObject { get; set; }
    public TileFlags flags { get; set; }
    public TileColliderType colliderType { get; set; }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = sprite;
        tileData.color = color;
        tileData.transform = transform;
        tileData.gameObject = instantiatedGameObject;
        tileData.flags = flags;
        tileData.colliderType = colliderType;
    }
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

public struct TileAnimationData
{
    public Sprite[] animatedSprites;
    public float animationSpeed;
    public float animationStartTime;
}

public interface ITilemap
{
    Tilemap tilemap { get; }
    Vector3Int origin { get; }
    Vector3Int size { get; }
    BoundsInt cellBounds { get; }
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

public enum CellLayout
{
    Rectangle,
    Hexagon,
    Isometric,
    IsometricZAsY
}

public enum CellSwizzle
{
    XYZ,
    XZY,
    YXZ,
    YZX,
    ZXY,
    ZYX
}

public enum SortOrder
{
    BottomLeft,
    BottomRight,
    TopLeft,
    TopRight
}

public struct BoundsInt
{
    public Vector3Int position;
    public Vector3Int size;

    public BoundsInt(Vector3Int position, Vector3Int size)
    {
        this.position = position;
        this.size = size;
    }

    public Vector3Int min => position;
    public Vector3Int max => position + size;
    public int xMin { get => position.x; set => position.x = value; }
    public int yMin { get => position.y; set => position.y = value; }
    public int zMin { get => position.z; set => position.z = value; }
    public int xMax { get => position.x + size.x; set => size.x = value - position.x; }
    public int yMax { get => position.y + size.y; set => size.y = value - position.y; }
    public int zMax { get => position.z + size.z; set => size.z = value - position.z; }
}
