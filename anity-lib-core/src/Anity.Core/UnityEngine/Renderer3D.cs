using System;
using System.Collections.Generic;

namespace UnityEngine;

public enum ShadowCastingMode
{
    Off = 0,
    On = 1,
    TwoSided = 2,
    ShadowsOnly = 3,
}

public enum ReceiveShadows
{
    Off = 0,
    On = 1,
}

public class BillboardRenderer : Renderer
{
    private BillboardAsset _billboard;
    private bool _useGPUBased;

    public BillboardAsset billboard
    {
        get => _billboard;
        set => _billboard = value;
    }

    public bool useGPUBased
    {
        get => _useGPUBased;
        set => _useGPUBased = value;
    }
}

public class BillboardAsset : Object
{
    public int version { get; set; } = 1;
    public BillboardFace[] faces { get; set; } = Array.Empty<BillboardFace>();
    public Vector2[] vertices { get; set; } = Array.Empty<Vector2>();
    public Vector2[] uvs { get; set; } = Array.Empty<Vector2>();
    public ushort[] indices { get; set; } = Array.Empty<ushort>();
    public Material material { get; set; }
    public Texture2D atlas { get; set; }
    public int atlasWidth { get; set; } = 512;
    public int atlasHeight { get; set; } = 512;
    public Vector2 atlasScale { get; set; } = Vector2.one;
    public float width { get; set; } = 1f;
    public float height { get; set; } = 1f;
    public int materialCount => 1;
    public int faceCount => faces?.Length ?? 0;
    public int vertexCount => vertices?.Length ?? 0;
    public int indexCount => indices?.Length ?? 0;

    public Material GetMaterial(int index) => material;
    public void SetMaterial(int index, Material mat) => material = mat;
}

public struct BillboardFace
{
    public Vector4 positions;
    public Vector4 texcoords;
}

public class LODGroup : Component
{
    private LOD[] _lods = Array.Empty<LOD>();
    private int _culledLevel = -1;
    private float _localReferenceScale = 1f;
    private bool _enabled = true;
    private LODCrossFade _crossFadeAnimationType = LODCrossFade.None;
    private float _animateCrossFading;
    private bool _fadeMode;
    private Transform _localRoot;

    public LOD[] lods
    {
        get => _lods;
        set => _lods = value ?? Array.Empty<LOD>();
    }

    public int lodCount => _lods?.Length ?? 0;
    public int culledLevel => _culledLevel;

    public float localWorldSpaceSize
    {
        get => _localReferenceScale;
        set => _localReferenceScale = value;
    }

    public bool enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public LODCrossFade crossFadeAnimationType
    {
        get => _crossFadeAnimationType;
        set => _crossFadeAnimationType = value;
    }

    public float animateCrossFading
    {
        get => _animateCrossFading;
        set => _animateCrossFading = value;
    }

    public bool fadeMode
    {
        get => _fadeMode;
        set => _fadeMode = value;
    }

    public Transform localRoot
    {
        get => _localRoot;
        set => _localRoot = value;
    }

    public void RecalculateBounds() { }
    public void SetLODs(LOD[] lods) { _lods = lods ?? Array.Empty<LOD>(); }
    public int GetLOD(Vector3 worldSpacePosition) => 0;
    public int GetVisibleLevel() => 0;
    public bool ForceLOD(int index) => true;
    public void SetLocalRoot(Transform root) { _localRoot = root; }

    public static void CrossFade(LODGroup lodGroup, int percentage) { }
}

public struct LOD
{
    public float screenRelativeTransitionHeight;
    public Renderer[] renderers;
    public int fadeTransitionWidth;

    public LOD(float screenRelativeTransitionHeight, Renderer[] renderers)
    {
        this.screenRelativeTransitionHeight = screenRelativeTransitionHeight;
        this.renderers = renderers ?? Array.Empty<Renderer>();
        this.fadeTransitionWidth = 0;
    }
}

public enum LODCrossFade
{
    None = 0,
    Analytic = 1,
    Dither = 2,
}

public class ReflectionProbe : Component
{
    private ReflectionProbeMode _mode = ReflectionProbeMode.Baked;
    private ReflectionProbeRefreshMode _refreshMode = ReflectionProbeRefreshMode.OnAwake;
    private ReflectionProbeTimeSlicing _timeSlicing = ReflectionProbeTimeSlicing.AllFacesAtOnce;
    private int _resolution = 128;
    private int _hdrResolution = 128;
    private bool _hdr = true;
    private float _shadowDistance = 100f;
    private Color _backgroundColor = Color.white;
    private int _cullingMask = ~0;
    private float _nearClipPlane = 0.3f;
    private float _farClipPlane = 1000f;
    private float _importance = 0f;
    private int _intensity = 1;
    private RenderTexture _bakedTexture;
    private RenderTexture _customTexture;
    private Texture _bakedTextureRef;
    private Vector3 _size = new Vector3(10, 10, 10);
    private Vector3 _center;
    private bool _boxProjection;
    private bool _blendDistance;
    private float _blendDistanceValue;
    private CameraClearFlags _clearFlags = CameraClearFlags.Skybox;
    private bool _useOcclusionCulling = true;

    public enum ReflectionProbeMode
    {
        Baked = 0,
        Realtime = 1,
        Custom = 2,
    }

    public enum ReflectionProbeRefreshMode
    {
        OnAwake = 0,
        EveryFrame = 1,
        ViaScripting = 2,
    }

    public enum ReflectionProbeTimeSlicing
    {
        AllFacesAtOnce = 0,
        IndividualFaces = 1,
        NoTimeSlicing = 2,
    }

    public CameraClearFlags clearFlags
    {
        get => _clearFlags;
        set => _clearFlags = value;
    }

    public ReflectionProbeRefreshMode refreshMode
    {
        get => _refreshMode;
        set => _refreshMode = value;
    }

    public ReflectionProbeTimeSlicing timeSlicing
    {
        get => _timeSlicing;
        set => _timeSlicing = value;
    }

    public int resolution
    {
        get => _resolution;
        set => _resolution = value;
    }

    public int hdrResolution
    {
        get => _hdrResolution;
        set => _hdrResolution = value;
    }

    public bool hdr
    {
        get => _hdr;
        set => _hdr = value;
    }

    public float shadowDistance
    {
        get => _shadowDistance;
        set => _shadowDistance = value;
    }

    public Color backgroundColor
    {
        get => _backgroundColor;
        set => _backgroundColor = value;
    }

    public int cullingMask
    {
        get => _cullingMask;
        set => _cullingMask = value;
    }

    public float nearClipPlane
    {
        get => _nearClipPlane;
        set => _nearClipPlane = value;
    }

    public float farClipPlane
    {
        get => _farClipPlane;
        set => _farClipPlane = value;
    }

    public float importance
    {
        get => _importance;
        set => _importance = value;
    }

    public int intensity
    {
        get => _intensity;
        set => _intensity = value;
    }

    public RenderTexture bakedTexture
    {
        get => _bakedTexture;
        set => _bakedTexture = value;
    }

    public RenderTexture customTexture
    {
        get => _customTexture;
        set => _customTexture = value;
    }

    public Vector3 size
    {
        get => _size;
        set => _size = value;
    }

    public Vector3 center
    {
        get => _center;
        set => _center = value;
    }

    public bool boxProjection
    {
        get => _boxProjection;
        set => _boxProjection = value;
    }

    public bool useOcclusionCulling
    {
        get => _useOcclusionCulling;
        set => _useOcclusionCulling = value;
    }

    public Texture texture { get; }
    public Texture textureHDRI { get; }
    public RenderTexture targetTexture { get; set; }
    public float blendedDistance { get; set; }

    public void RenderProbe() { }
    public void RenderProbe(RenderTexture targetTexture) { }
    public void Reset() { }

    public static ReflectionProbe defaultReflectionProbe { get; }

    public static void BlendReflections(
        Texture src,
        Texture dst,
        RenderTexture target,
        float blendDistance,
        float blendNormalDistance,
        Vector3 blendDistanceApplyNormals,
        int cubemapSize,
        Vector3 boxProjectionCenter,
        Vector3 boxProjectionSize) { }
}

public class SortingGroup : Component
{
    private int _sortingLayerID;
    private string _sortingLayerName = string.Empty;
    private int _sortingOrder;
    private bool _enabled = true;

    public int sortingLayerID
    {
        get => _sortingLayerID;
        set => _sortingLayerID = value;
    }

    public string sortingLayerName
    {
        get => _sortingLayerName;
        set => _sortingLayerName = value ?? string.Empty;
    }

    public int sortingOrder
    {
        get => _sortingOrder;
        set => _sortingOrder = value;
    }

    public bool enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public static SortingGroup GetSortingGroupForRenderer(Renderer renderer, bool returnNULLIfNotEnabled = false)
    {
        return null;
    }
}

public struct CombineInstance
{
    public Mesh mesh;
    public int subMeshIndex;
    public Matrix4x4 transform;
    public bool lightmapScaleOffset;
    public Vector4 lightmapScaleOffsetValue;
    public bool realtimeLightmapScaleOffset;
    public Vector4 realtimeLightmapScaleOffsetValue;
}

public static class StaticBatchingUtility
{
    public static void Combine(GameObject staticBatchingRoot) { }
    public static void Combine(GameObject staticBatchingRoot, bool combineChildren = true) { }
    public static void Combine(GameObject[] gameObjects, GameObject staticBatchingRoot) { }
    public static void Combine(GameObject[] gameObjects, GameObject staticBatchingRoot, bool combineChildren = true) { }
}

public class OcclusionArea : Component
{
    public Vector3 center { get; set; }
    public Vector3 size { get; set; } = new Vector3(5, 5, 5);

    public static OcclusionArea[] occlusionAreas { get; } = Array.Empty<OcclusionArea>();
}

public class OcclusionPortal : Component
{
    public Vector3 center { get; set; }
    public Vector3 size { get; set; } = new Vector3(5, 5, 5);
    public bool open { get; set; }
}

public class FlareLayer : Behaviour
{
}

public class LensFlare : Behaviour
{
    public Flare flare { get; set; }
    public float brightness { get; set; } = 1f;
    public Color color { get; set; } = Color.white;
    public float fadeSpeed { get; set; } = 3f;
}

public class Flare : Object
{
    public FlareElement[] elements { get; set; } = Array.Empty<FlareElement>();
}

public struct FlareElement
{
    public Texture texture;
    public float size;
    public float position;
    public Color color;
    public bool useLightColor;
    public float aspectRatio;
    public float rotation;
}

public class LightProbeGroup : Component
{
    public Vector3[] positions { get; set; } = Array.Empty<Vector3>();

    public void AddProbe(Vector3 position) { }
    public void RemoveProbe(Vector3 position) { }
}

public class WindZone : Component
{
    public float radius { get; set; } = 20f;
    public float windMain { get; set; }
    public float windTurbulence { get; set; }
    public float windPulseMagnitude { get; set; }
    public float windPulseFrequency { get; set; }
    public Vector3 windZoneTransformForward { get; set; } = Vector3.forward;
    public WindZoneMode mode { get; set; } = WindZoneMode.Directional;
}

public enum WindZoneMode
{
    Directional = 0,
    Spherical = 1,
}
