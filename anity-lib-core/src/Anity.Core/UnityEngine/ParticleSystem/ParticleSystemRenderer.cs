namespace UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleSystemRenderer : Renderer
{
    public ParticleSystemRenderer()
    {
        renderMode = ParticleSystemRenderMode.Billboard;
        renderAlignment = ParticleSystemRenderSpace.View;
        sortMode = ParticleSystemSortMode.None;
        stretchRotation = ParticleSystemStretchRotation.View;
        cameraVelocityScale = 0f;
        speedScale = 1f;
        lengthScale = 2f;
        billboardRotation = 0f;
        flip = Vector2.zero;
        pivot = new Vector3(0.5f, 0.5f, 0f);
        allowRoll = true;
        minParticleSize = 0.01f;
        maxParticleSize = 0.5f;
        normalDirection = 0.5f;
        shadowCastingMode = ShadowCastingMode.On;
        receiveShadows = true;
        shadowBias = 0f;
        sortingFudge = 0f;
        _meshes = new Mesh[0];
        _meshCount = 0;
        trailMaterial = null;
        activeVertexStreams = ParticleSystemVertexStreams.Position | ParticleSystemVertexStreams.Color | ParticleSystemVertexStreams.UV;
        maskInteraction = SpriteMaskInteraction.None;
        sortingOrder = 0;
        sortingLayerID = 0;
        enabled = true;
    }

    public ParticleSystemRenderMode renderMode { get; set; }
    public ParticleSystemRenderSpace renderAlignment { get; set; }
    public ParticleSystemSortMode sortMode { get; set; }
    public ParticleSystemStretchRotation stretchRotation { get; set; }
    public float cameraVelocityScale { get; set; }
    public float speedScale { get; set; }
    public float lengthScale { get; set; }
    public float billboardRotation { get; set; }
    public Vector2 flip { get; set; }
    public Vector3 pivot { get; set; }
    public bool allowRoll { get; set; }
    public float minParticleSize { get; set; }
    public float maxParticleSize { get; set; }
    public float normalDirection { get; set; }
    public float shadowBias { get; set; }
    public float sortingFudge { get; set; }
    public Material trailMaterial { get; set; }
    public ParticleSystemVertexStreams activeVertexStreams { get; set; }
    public SpriteMaskInteraction maskInteraction { get; set; }
    public int sortingOrder { get; set; }
    public int sortingLayerID { get; set; }
    public bool enabled { get; set; }
    public bool isVisible { get; internal set; }

    private Mesh[] _meshes;
    private int _meshCount;

    public int meshCount => _meshCount;

    public Mesh mesh
    {
        get => _meshCount > 0 ? _meshes[0] : null;
        set
        {
            _meshes = new[] { value };
            _meshCount = 1;
        }
    }

    public Mesh GetMesh(int index)
    {
        return _meshes[index];
    }

    public void SetMesh(int index, Mesh mesh)
    {
        _meshes[index] = mesh;
    }

    public void SetMeshes(Mesh[] meshes)
    {
        SetMeshes(meshes, meshes.Length);
    }

    public void SetMeshes(Mesh[] meshes, int count)
    {
        _meshes = new Mesh[count];
        for (int i = 0; i < count; i++)
            _meshes[i] = meshes[i];
        _meshCount = count;
    }

    public int GetMeshes(Mesh[] meshes)
    {
        int count = Mathf.Min(meshes.Length, _meshCount);
        for (int i = 0; i < count; i++)
            meshes[i] = _meshes[i];
        return count;
    }

    public void EnableVertexStreams(ParticleSystemVertexStreams streams)
    {
        activeVertexStreams |= streams;
    }

    public void DisableVertexStreams(ParticleSystemVertexStreams streams)
    {
        activeVertexStreams &= ~streams;
    }

    public bool GetActiveVertexStreams(ParticleSystemVertexStreams streams)
    {
        return (activeVertexStreams & streams) == streams;
    }

    public void BakeMesh(Mesh mesh)
    {
        BakeMesh(mesh, true);
    }

    public void BakeMesh(Mesh mesh, bool useTransform)
    {
        _ = useTransform;
        if (mesh != null)
        {
            mesh.Clear();
        }
    }

    public void BakeTrailsMesh(Mesh mesh)
    {
        BakeTrailsMesh(mesh, true);
    }

    public void BakeTrailsMesh(Mesh mesh, bool useTransform)
    {
        _ = useTransform;
        if (mesh != null)
        {
            mesh.Clear();
        }
    }
}