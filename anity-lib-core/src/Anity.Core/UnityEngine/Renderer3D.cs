using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine;

public struct LOD
{
    public float screenRelativeTransitionHeight;
    public float fadeTransitionWidth;
    public Renderer[] renderers;

    public LOD(float screenRelativeTransitionHeight, Renderer[] renderers)
    {
        this.screenRelativeTransitionHeight = screenRelativeTransitionHeight;
        this.renderers = renderers;
        fadeTransitionWidth = 0f;
    }
}

public sealed class LODGroup : Component
{
    private LOD[] _lods = Array.Empty<LOD>();
    private Vector3 _localReferencePoint = Vector3.zero;
    private float _size = 1f;
    private int _lodCount;
    private bool _crossFade;
    private float _animateCrossFadingSpeed = 1f;
    private bool _isCrossFading;
    private float _currentCrossFadeFactor;
    private static bool _crossFadeStatic;

    public bool animateCrossFading { get; set; }
    public int lodCount => _lods?.Length ?? 0;
    public LOD[] GetLODs() => _lods;

    public void SetLODs(LOD[] lods)
    {
        _lods = lods != null ? (LOD[])lods.Clone() : Array.Empty<LOD>();
        RecalculateBounds();
    }

    public void RecalculateBounds()
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool has = false;
        if (_lods != null)
        {
            for (int i = 0; i < _lods.Length; i++)
            {
                if (_lods[i].renderers == null) continue;
                for (int j = 0; j < _lods[i].renderers.Length; j++)
                {
                    var r = _lods[i].renderers[j];
                    if (r == null || r.bounds.extents.sqrMagnitude <= 0f) continue;
                    if (!has) { bounds = r.bounds; has = true; }
                    else bounds.Encapsulate(r.bounds);
                }
            }
        }
        if (has)
            localReferencePoint = transform.InverseTransformPoint(bounds.center);
    }

    public Vector3 localReferencePoint
    {
        get => _localReferencePoint;
        set => _localReferencePoint = value;
    }

    public float size
    {
        get => _size;
        set => _size = Math.Max(0.001f, value);
    }

    public float crossFadeAnimationDuration { get; set; } = 0.5f;

    public void CrossFade(float fadeDuration)
    {
        _isCrossFading = true;
        _animateCrossFadingSpeed = fadeDuration > 0f ? 1f / fadeDuration : 1f;
        _currentCrossFadeFactor = 0f;
    }

    public static bool crossFade
    {
        get => _crossFadeStatic;
        set => _crossFadeStatic = value;
    }

    public void ForceLOD(int lodIndex) { _currentCrossFadeFactor = lodIndex; }

    public void Reset()
    {
        var allRenderers = gameObject != null ? gameObject.GetComponentsInChildren<Renderer>() : Array.Empty<Renderer>();
        _lods = new LOD[] { new LOD(1f, allRenderers) };
        _localReferencePoint = Vector3.zero;
        _size = 1f;
        _crossFade = false;
        _isCrossFading = false;
        _currentCrossFadeFactor = 0f;
        RecalculateBounds();
    }
}

public static class StaticBatchingUtility
{
    public static void Combine(GameObject staticBatchRoot)
    {
        if (staticBatchRoot == null) return;
        var renderers = staticBatchRoot.GetComponentsInChildren<MeshRenderer>(true);
        var combinedVerts = new List<Vector3>();
        var combinedNorms = new List<Vector3>();
        var combinedUVs = new List<Vector2>();
        var combinedIndices = new List<int>();
        int vertexOffset = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mesh = mf.sharedMesh;
            var localToWorld = r.transform.localToWorldMatrix;

            var verts = mesh.vertices;
            var norms = mesh.normals;
            var uvs = mesh.uv;
            var tris = mesh.triangles;

            for (int v = 0; v < verts.Length; v++)
            {
                var worldV = localToWorld.MultiplyPoint(verts[v]);
                combinedVerts.Add(r.transform.InverseTransformPoint(worldV));
                if (norms.Length == verts.Length)
                    combinedNorms.Add(localToWorld.MultiplyVector(norms[v]).normalized);
                else
                    combinedNorms.Add(Vector3.up);
                if (uvs.Length == verts.Length)
                    combinedUVs.Add(uvs[v]);
                else
                    combinedUVs.Add(Vector2.zero);
            }
            for (int t = 0; t < tris.Length; t++)
                combinedIndices.Add(tris[t] + vertexOffset);
            vertexOffset += verts.Length;

            r.gameObject.isStatic = true;
        }

        if (combinedVerts.Count > 0 && staticBatchRoot != null)
        {
            var combinedMesh = new Mesh();
            combinedMesh.vertices = combinedVerts.ToArray();
            combinedMesh.normals = combinedNorms.ToArray();
            combinedMesh.uv = combinedUVs.ToArray();
            combinedMesh.triangles = combinedIndices.ToArray();
            combinedMesh.RecalculateBounds();
        }
    }

    public static void Combine(GameObject[] gos, GameObject staticBatchRoot)
    {
        if (gos == null) return;
        var tempRoot = staticBatchRoot ?? new GameObject("__StaticBatch");
        for (int i = 0; i < gos.Length; i++)
        {
            if (gos[i] != null)
                gos[i].transform.SetParent(tempRoot.transform, true);
        }
        Combine(tempRoot);
    }
}

public sealed class LightProbeGroup : Component
{
    private List<Vector3> _probePositions = new List<Vector3>();

    public Vector3[] probePositions
    {
        get => _probePositions.ToArray();
        set => _probePositions = value != null ? new List<Vector3>(value) : new List<Vector3>();
    }

    public void AddProbe(Vector3 position)
    {
        _probePositions.Add(position);
    }

    public void RemoveProbe(int index)
    {
        if (index >= 0 && index < _probePositions.Count)
            _probePositions.RemoveAt(index);
    }

    public void RemoveProbeAt(int index) => RemoveProbe(index);

    public int GetProbeCount() => _probePositions.Count;
}

public sealed class LightProbes
{
    private List<Vector3> _positions = new List<Vector3>();
    private List<SphericalHarmonicsL2> _coefficients = new List<SphericalHarmonicsL2>();
    public Vector3[] positions { get => _positions.ToArray(); set => _positions = value != null ? new List<Vector3>(value) : new List<Vector3>(); }
    public SphericalHarmonicsL2[] bakedProbes { get => _coefficients.ToArray(); set => _coefficients = value != null ? new List<SphericalHarmonicsL2>(value) : new List<SphericalHarmonicsL2>(); }
    public int count => _positions.Count;

    public static LightProbes GetLightProbesForScene(int sceneHandle) => null;
}

public class ReflectionProbe : Behaviour
{
    public Bounds bounds { get; set; }
    public float nearClipPlane { get; set; }
    public float farClipPlane { get; set; } = 1000f;
    public float intensity { get; set; } = 1f;
    public int resolution { get; set; } = 128;
    public bool boxProjection { get; set; }
    public ReflectionProbeMode mode { get; set; } = ReflectionProbeMode.Baked;
    public ReflectionProbeRefreshMode refreshMode { get; set; } = ReflectionProbeRefreshMode.OnAwake;
    public ReflectionProbeTimeSlicingMode timeSlicingMode { get; set; }
    public Color backgroundColor { get; set; } = Color.black;
    public float blendDistance { get; set; }
    public Vector3 size { get; set; } = Vector3.one;
    public Vector3 center { get; set; }
    public int importance { get; set; } = 1;
    public Cubemap bakedTexture { get; set; }
    public Texture customBakedTexture { get; set; }
    public RenderTexture realtimeTexture { get; set; }
    public bool shadows { get; set; } = true;
    public int cullingMask { get; set; } = -1;
    public void Reset()
    {
        resolution = 128;
        boxProjection = false;
        mode = ReflectionProbeMode.Baked;
        refreshMode = ReflectionProbeRefreshMode.OnAwake;
        timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        backgroundColor = Color.black;
        blendDistance = 0f;
        size = Vector3.one;
        center = Vector3.zero;
        importance = 1;
        bakedTexture = null;
        customBakedTexture = null;
        realtimeTexture = null;
        shadows = true;
        cullingMask = -1;
    }
    public int RenderProbe() => 0;
    public bool IsFinishedRendering(int renderId) => true;
}

public enum ReflectionProbeMode { Baked = 0, Realtime = 1, Custom = 2 }
public enum ReflectionProbeRefreshMode { OnAwake = 0, EveryFrame = 1, ViaScripting = 2 }
public enum ReflectionProbeTimeSlicingMode { AllFacesAtOnce = 0, IndividualFaces = 1, NoTimeSlicing = 2 }

public class Projector : Behaviour
{
    public float nearClipPlane { get; set; }
    public float farClipPlane { get; set; }
    public float fieldOfView { get; set; } = 60f;
    public float aspectRatio { get; set; } = 1f;
    public float orthographicSize { get; set; }
    public bool orthographic { get; set; }
    public Material material { get; set; }
    public Color color { get; set; } = Color.white;
    public int ignoreLayers { get; set; }
}

public class Skybox : Behaviour
{
    public Material material { get; set; }
}

public enum FogMode { Linear = 1, Exponential = 2, ExponentialSquared = 3 }

public enum DefaultReflectionMode { Skybox = 0, Custom = 1 }

public static class RenderSettings
{
    public static bool fog { get; set; }
    public static Color fogColor { get; set; } = new Color(0.5f, 0.5f, 0.5f, 1f);
    public static FogMode fogMode { get; set; } = FogMode.ExponentialSquared;
    public static float fogDensity { get; set; } = 0.01f;
    public static float fogStartDistance { get; set; }
    public static float fogEndDistance { get; set; } = 300f;
    public static AmbientMode ambientMode { get; set; } = AmbientMode.Skybox;
    public static Color ambientSkyColor { get; set; } = new Color(0.212f, 0.227f, 0.259f);
    public static Color ambientEquatorColor { get; set; } = new Color(0.114f, 0.125f, 0.133f);
    public static Color ambientGroundColor { get; set; } = new Color(0.047f, 0.043f, 0.035f);
    public static Color ambientLight { get; set; } = new Color(0.212f, 0.227f, 0.259f);
    public static float ambientIntensity { get; set; } = 1f;
    public static DefaultReflectionMode defaultReflectionMode { get; set; } = DefaultReflectionMode.Skybox;
    public static int defaultReflectionResolution { get; set; } = 128;
    public static float reflectionIntensity { get; set; } = 1f;
    public static int reflectionBounces { get; set; } = 1;
    public static Material skybox { get; set; }
    public static Light sun { get; set; }
    public static Color subtractiveShadowColor { get; set; } = new Color(0.42f, 0.478f, 0.627f);
    public static float flareFadeSpeed { get; set; } = 3f;
    public static float flareStrength { get; set; } = 1f;
    public static float haloStrength { get; set; } = 1f;
    public static SphericalHarmonicsL2 ambientProbe { get; set; }
    public static Cubemap customReflection { get; set; }
}
