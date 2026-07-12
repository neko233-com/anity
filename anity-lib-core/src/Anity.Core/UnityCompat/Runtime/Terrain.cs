using System;
using System.Collections.Generic;

namespace UnityEngine;

[AddComponentMenu("Terrain/Terrain")]
public class Terrain : Behaviour
{
    private TerrainData? _terrainData;
    private Material? _materialTemplate;
    private float _heightmapPixelError = 5.0f;
    private bool _delayedHeightmapApplied;
    private float _basemapDistance = 1000f;
    private float _detailObjectDistance = 80.0f;
    private float _treeDistance = 5000.0f;
    private int _treeMaximumFullLODCount = 50;
    private bool _drawTreesAndFoliage = true;
    private bool _collectDetailPatches = true;
    private bool _castShadows = true;
    private TerrainRenderFlags _editorRenderFlags = TerrainRenderFlags.All;

    public TerrainData? terrainData
    {
        get => _terrainData;
        set => _terrainData = value;
    }

    public Material? materialTemplate
    {
        get => _materialTemplate;
        set => _materialTemplate = value;
    }

    public float heightmapPixelError
    {
        get => _heightmapPixelError;
        set => _heightmapPixelError = value;
    }

    public float basemapDistance
    {
        get => _basemapDistance;
        set => _basemapDistance = value;
    }

    public float detailObjectDistance
    {
        get => _detailObjectDistance;
        set => _detailObjectDistance = value;
    }

    public float treeDistance
    {
        get => _treeDistance;
        set => _treeDistance = value;
    }

    public float billboardStart { get; set; } = 50f;

    public int treeMaximumFullLODCount
    {
        get => _treeMaximumFullLODCount;
        set => _treeMaximumFullLODCount = value;
    }

    public bool drawTreesAndFoliage
    {
        get => _drawTreesAndFoliage;
        set => _drawTreesAndFoliage = value;
    }

    public bool collectDetailPatches
    {
        get => _collectDetailPatches;
        set => _collectDetailPatches = value;
    }

    public bool castShadows
    {
        get => _castShadows;
        set => _castShadows = value;
    }

    public TerrainRenderFlags editorRenderFlags
    {
        get => _editorRenderFlags;
        set => _editorRenderFlags = value;
    }

    public float[] GetHeights(int xBase, int yBase, int width, int height)
    {
        if (_terrainData == null) return Array.Empty<float>();
        return _terrainData.GetHeights(xBase, yBase, width, height);
    }

    public void SetHeights(int xBase, int yBase, float[,] heights)
    {
        _terrainData?.SetHeights(xBase, yBase, heights);
    }

    public float GetHeight(int x, int y)
    {
        return _terrainData?.GetHeight(x, y) ?? 0f;
    }

    public Vector3 GetInterpolatedNormal(float x, float y)
    {
        return _terrainData?.GetInterpolatedNormal(x, y) ?? Vector3.up;
    }

    public Vector3 GetPosition()
    {
        return transform?.position ?? Vector3.zero;
    }

    public void SampleHeight(Vector3 worldPosition, out float height)
    {
        height = SampleHeight(worldPosition);
    }

    public float SampleHeight(Vector3 worldPosition)
    {
        if (_terrainData == null) return 0f;
        var pos = GetPosition();
        var scale = _terrainData.heightmapScale;
        float x = (worldPosition.x - pos.x) / scale.x;
        float y = (worldPosition.z - pos.z) / scale.z;
        return pos.y + _terrainData.GetInterpolatedHeight(x, y);
    }

    public void ApplyDelayedHeightmapModification() { _delayedHeightmapApplied = true; }

    public void CollectDetailPatches(float height, int layer) { _ = height; _ = layer; }

    public static Terrain? activeTerrain { get; set; }
}

public class TerrainData : Object
{
    private float[,] _heights = new float[33, 33];
    private float[,,] _alphamaps = new float[512, 512, 0];
    private int _heightmapResolution = 33;
    private int _alphamapResolution = 512;
    private int _alphamapLayers;
    private Vector3 _size = new Vector3(500, 600, 500);
    private Vector3 _heightmapScale;
    private List<TreePrototype> _treePrototypes = new();
    private List<TreeInstance> _treeInstances = new();
    private List<DetailPrototype> _detailPrototypes = new();

    public int heightmapWidth => _heightmapResolution;
    public int heightmapHeight => _heightmapResolution;
    public int heightmapResolution
    {
        get => _heightmapResolution;
        set
        {
            if (value < 2) value = 2;
            _heightmapResolution = value;
            var newHeights = new float[value, value];
            for (int y = 0; y < Math.Min(value, _heights.GetLength(1)); y++)
                for (int x = 0; x < Math.Min(value, _heights.GetLength(0)); x++)
                    newHeights[x, y] = _heights[x, y];
            _heights = newHeights;
            RecalculateHeightmapScale();
        }
    }

    public int alphamapResolution
    {
        get => _alphamapResolution;
        set => _alphamapResolution = value;
    }

    public int alphamapLayers => _alphamapLayers;

    public Vector3 heightmapScale => _heightmapScale;

    public Vector3 size
    {
        get => _size;
        set
        {
            _size = value;
            RecalculateHeightmapScale();
        }
    }

    public TerrainLayer[] terrainLayers { get; set; } = Array.Empty<TerrainLayer>();

    public List<TreePrototype> treePrototypes
    {
        get => _treePrototypes;
        set => _treePrototypes = value ?? new List<TreePrototype>();
    }

    public List<TreeInstance> treeInstances
    {
        get => _treeInstances;
        set => _treeInstances = value ?? new List<TreeInstance>();
    }

    public List<DetailPrototype> detailPrototypes
    {
        get => _detailPrototypes;
        set => _detailPrototypes = value ?? new List<DetailPrototype>();
    }

    public TerrainData()
    {
        RecalculateHeightmapScale();
    }

    private void RecalculateHeightmapScale()
    {
        _heightmapScale = new Vector3(
            _size.x / (_heightmapResolution - 1),
            _size.y,
            _size.z / (_heightmapResolution - 1));
    }

    public float GetHeight(int x, int y)
    {
        if (x < 0 || x >= _heightmapResolution || y < 0 || y >= _heightmapResolution)
            return 0f;
        return _heights[x, y] * _heightmapScale.y;
    }

    public float[] GetHeights(int xBase, int yBase, int width, int height)
    {
        var result = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int hx = xBase + x;
                int hy = yBase + y;
                if (hx >= 0 && hx < _heightmapResolution && hy >= 0 && hy < _heightmapResolution)
                    result[y * width + x] = _heights[hx, hy] * _heightmapScale.y;
            }
        }
        return result;
    }

    public void SetHeights(int xBase, int yBase, float[,] heights)
    {
        if (heights == null) return;
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int hx = xBase + x;
                int hy = yBase + y;
                if (hx >= 0 && hx < _heightmapResolution && hy >= 0 && hy < _heightmapResolution)
                    _heights[hx, hy] = heights[x, y] / _heightmapScale.y;
            }
        }
    }

    public void SetHeightsDelayLOD(int xBase, int yBase, float[,] heights)
    {
        SetHeights(xBase, yBase, heights);
    }

    public float GetInterpolatedHeight(float x, float y)
    {
        int x0 = (int)x;
        int y0 = (int)y;
        if (x0 < 0 || x0 >= _heightmapResolution - 1 || y0 < 0 || y0 >= _heightmapResolution - 1)
            return GetHeight(x0, y0);
        float fx = x - x0;
        float fy = y - y0;
        float h00 = _heights[x0, y0];
        float h10 = _heights[x0 + 1, y0];
        float h01 = _heights[x0, y0 + 1];
        float h11 = _heights[x0 + 1, y0 + 1];
        return Mathf.Lerp(Mathf.Lerp(h00, h10, fx), Mathf.Lerp(h01, h11, fx), fy) * _heightmapScale.y;
    }

    public Vector3 GetInterpolatedNormal(float x, float y)
    {
        return Vector3.up;
    }

    public Vector3 GetNormal(float x, float y)
    {
        return Vector3.up;
    }

    public float[,,] GetAlphamaps(int x, int y, int width, int height)
    {
        if (_alphamaps.GetLength(2) == 0)
            return new float[width, height, 0];
        var result = new float[width, height, _alphamapLayers];
        return result;
    }

    public void SetAlphamaps(int x, int y, float[,,] alphamaps)
    {
        if (alphamaps == null) return;
        _alphamapLayers = alphamaps.GetLength(2);
        _alphamaps = new float[_alphamapResolution, _alphamapResolution, _alphamapLayers];
    }

    public void SetTreeInstances(TreeInstance[] instances, bool snapToGround)
    {
        _ = snapToGround;
        _treeInstances = instances != null ? new List<TreeInstance>(instances) : new List<TreeInstance>();
    }

    public void AddTreeInstance(TreeInstance instance)
    {
        _treeInstances.Add(instance);
    }

    public TreeInstance[] GetTreeInstances()
    {
        return _treeInstances.ToArray();
    }

    public void RefreshPrototypes() { }
}

public class TreePrototype
{
    public GameObject? prefab { get; set; }
    public float bendFactor { get; set; }
    public Color navMeshColor { get; set; } = Color.white;
}

public struct TreeInstance
{
    public Vector3 position;
    public float widthScale;
    public float heightScale;
    public float rotation;
    public Color32 color;
    public Color32 lightmapColor;
    public int prototypeIndex;
}

public class DetailPrototype
{
    public GameObject? prototype { get; set; }
    public Texture2D? prototypeTexture { get; set; }
    public float minWidth { get; set; } = 1f;
    public float maxWidth { get; set; } = 2f;
    public float minHeight { get; set; } = 1f;
    public float maxHeight { get; set; } = 2f;
    public Color dryColor { get; set; } = new Color(0.855f, 0.737f, 0.494f);
    public Color healthyColor { get; set; } = new Color(0.263f, 0.976f, 0.165f);
    public DetailRenderMode renderMode { get; set; }
    public bool usePrototypeMesh { get; set; }
    public float noiseSpread { get; set; } = 0.1f;
    public float bendFactor { get; set; } = 0.1f;
}

public enum DetailRenderMode
{
    GrassBillboard = 0,
    VertexLit = 1,
    Grass = 2
}

[Flags]
public enum TerrainRenderFlags
{
    None = 0,
    Heightmap = 1,
    Trees = 2,
    Details = 4,
    All = Heightmap | Trees | Details
}

public class TerrainLayer : Object
{
    public string name { get; set; }
    public Texture2D diffuseTexture { get; set; }
    public Texture2D normalMapTexture { get; set; }
    public Texture2D maskMapTexture { get; set; }
    public Vector2 tileSize { get; set; } = new Vector2(1, 1);
    public Vector2 tileOffset { get; set; }
    public Color specular { get; set; } = Color.gray;
    public float metallic { get; set; }
    public float smoothness { get; set; }
    public float normalScale { get; set; } = 1f;
    public Vector4 diffuseRemap { get; set; } = new Vector4(0, 1, 0, 1);
    public Vector4 maskMapRemap { get; set; } = new Vector4(0, 1, 0, 1);
}
