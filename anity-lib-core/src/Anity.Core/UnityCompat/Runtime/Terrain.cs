using System;

namespace UnityEngine;

/// <summary>
/// Unity Terrain component for rendering terrains.
/// </summary>
[AddComponentMenu("Terrain/Terrain")]
public class Terrain : Behaviour
{
    private TerrainData? _terrainData;
    private float _heightmapPixelError = 5.0f;
    private int _basemapResolution = 257;
    private float _detailObjectDistance = 80.0f;
    private float _detailObjectDensity = 1.0f;
    private float _treeDistance = 5000.0f;
    private float _treeBillboardDistance = 50.0f;
    private float _treeCrossFadeLength = 5.0f;
    private int _treeMaximumFullLODCount = 50;
    private float _detailObjectDensity1 = 1.0f;
    private bool _drawHeightmap = true;
    private bool _drawTreesAndFoliage = true;
    private bool _collectDetailPatches = true;
    private bool _legacyShadows = true;

    public TerrainData? terrainData
    {
        get => _terrainData;
        set => _terrainData = value;
    }

    public float heightmapPixelError
    {
        get => _heightmapPixelError;
        set => _heightmapPixelError = value;
    }

    public int basemapResolution
    {
        get => _basemapResolution;
        set => _basemapResolution = value;
    }

    public float detailObjectDistance
    {
        get => _detailObjectDistance;
        set => _detailObjectDistance = value;
    }

    public float detailObjectDensity
    {
        get => _detailObjectDensity;
        set => _detailObjectDensity = value;
    }

    public float treeDistance
    {
        get => _treeDistance;
        set => _treeDistance = value;
    }

    public float treeBillboardDistance
    {
        get => _treeBillboardDistance;
        set => _treeBillboardDistance = value;
    }

    public float treeCrossFadeLength
    {
        get => _treeCrossFadeLength;
        set => _treeCrossFadeLength = value;
    }

    public int treeMaximumFullLODCount
    {
        get => _treeMaximumFullLODCount;
        set => _treeMaximumFullLODCount = value;
    }

    public bool drawHeightmap
    {
        get => _drawHeightmap;
        set => _drawHeightmap = value;
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

    public bool legacyShadows
    {
        get => _legacyShadows;
        set => _legacyShadows = value;
    }

    public float[] GetHeights(int x, int y, int width, int height) => new float[width * height];
    public void SetHeights(int x, int y, float[,] heights) { }
    public float GetHeight(int x, int y) => 0.0f;
    public Vector3 GetInterpolatedNormal(float x, float y) => Vector3.up;
    public Vector3 GetPosition() => transform?.position ?? Vector3.zero;
    public void SampleHeight(Vector3 worldPosition, out float height) => height = 0.0f;
    public void ApplyDelayedHeightmapModification() { }
}

/// <summary>
/// Unity TerrainData asset.
/// </summary>
public class TerrainData : UnityEngine.Object
{
    public int heightmapResolution { get; set; } = 33;
    public Vector3 size { get; set; } = new Vector3(500, 600, 500);
    public float[]alphamaps { get; set; } = Array.Empty<float>();
    public int alphamapResolution { get; set; } = 512;
    public int alphamapLayers { get; set; }
    public float[,,] GetAlphamaps(int x, int y, int width, int height) => new float[width, height, alphamapLayers];
    public void SetAlphamaps(int x, int y, float[,,] alphamaps) { }
    public float[,] GetHeights(int x, int y, int width, int height) => new float[width, height];
    public void SetHeights(int x, int y, float[,] heights) { }
    public float[,] GetInterpolatedHeights(float x, float y, int width, int height) => new float[width, height];
    public Vector3 GetNormal(float x, float y) => Vector3.up;
    public Vector3 GetInterpolatedNormal(float x, float y) => Vector3.up;
    public float GetHeight(int x, int y) => 0.0f;
    public float GetBaseHeight(int x, int y) => 0.0f;
    public void SetBaseHeight(int x, int y, float height) { }
    public Texture2D GetAlphamapTexture(int index) => null;
    public void SetAlphamapTexture(int index, Texture2D texture) { }
    public int alphamapTextureCount { get; set; }
    public Texture2D[] alphamapTextures { get; set; } = Array.Empty<Texture2D>();
    public int detailResolution { get; set; }
    public int detailResolutionPerPatch { get; set; } = 16;
    public int detailPatchCount { get; set; }
    public int detailDatabaseResolution { get; set; }
    public Texture2D detailDatabaseTexture { get; set; }
    public int detailDatabaseTextureResolution { get; set; }
    public Material[] detailPrototypes { get; set; } = Array.Empty<Material>();
    public void SetDetailLayer(int x, int y, int detailLayer, int[,] detail) { }
    public int[,] GetDetailLayer(int x, int y, int width, int height, int detailLayer) => new int[width, height];
    public void SetDetailResolution(int resolution, int resolutionPerPatch) { }
    public void RefreshPrototypes() { }
    public TreePrototype[] treePrototypes { get; set; } = Array.Empty<TreePrototype>();
    public void SetTreePrototypes(TreePrototype[] prototypes) { }
    public TreeInstance[] treeInstances { get; set; } = Array.Empty<TreeInstance>();
    public void SetTreeInstances(TreeInstance[] instances) { }
    public void AddTreeInstance(TreeInstance instance) { }
    public void RemoveTreeInstance(int index) { }
    public void SetTreeInstance(int index, TreeInstance instance) { }
    public TreeInstance GetTreeInstance(int index) => default;
    public int treeInstanceCount { get; set; }
    public TerrainLayer[] terrainLayers { get; set; } = Array.Empty<TerrainLayer>();
    public void SetTerrainLayers(TerrainLayer[] layers) { }
    public void AddTerrainLayer(TerrainLayer layer) { }
    public void RemoveTerrainLayer(int index) { }
    public TerrainLayer GetTerrainLayer(int index) => null;
    public int terrainLayerCount { get; set; }
}

/// <summary>
/// Tree prototype for Terrain.
/// </summary>
[Serializable]
public class TreePrototype
{
    public GameObject? prefab;
    public float bendFactor;
}

/// <summary>
/// Tree instance for Terrain.
/// </summary>
public struct TreeInstance
{
    public Vector3 position;
    public float widthScale;
    public float heightScale;
    public float rotation;
    public Color color;
    public int prototypeIndex;
    public bool temporaryTreeInstance;
}

/// <summary>
/// Terrain layer.
/// </summary>
public class TerrainLayer : UnityEngine.Object
{
    public Texture2D diffuseTexture { get; set; }
    public Texture2D normalMapTexture { get; set; }
    public Vector4 tileOffset { get; set; }
    public Vector2 tileSize { get; set; } = Vector2.one;
    public float metallic { get; set; }
    public float smoothness { get; set; }
    public float normalScale { get; set; } = 1.0f;
    public Vector4 maskMapRemappingMin { get; set; }
    public Vector4 maskMapRemappingMax { get; set; }
}

/// <summary>
/// Terrain tools for editing terrains.
/// </summary>
public static class TerrainTools
{
    public static void PaintHeight(Terrain terrain, Vector3 position, float radius, float opacity) { }
    public static void PaintTexture(Terrain terrain, Vector3 position, float radius, float opacity, int layer) { }
    public static void PaintDetails(Terrain terrain, Vector3 position, float radius, int detail, float opacity) { }
    public static void PaintTrees(Terrain terrain, Vector3 position, float radius, int tree, float opacity) { }
}
