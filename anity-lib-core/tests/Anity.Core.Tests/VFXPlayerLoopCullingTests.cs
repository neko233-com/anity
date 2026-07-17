using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;
using Object = UnityEngine.Object;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class VFXPlayerLoopCullingTests
{
    [Fact]
    public void OutsideBounds_SimulateFirstFrameThenCullNextFrame()
    {
        using TestScope scope = Scope();
        VisualEffect effect = scope.Effect(new Bounds(new Vector3(4f, 0f, 0f), Vector3.one));
        scope.Camera();

        UnityRuntime.Tick(0.05f);
        Assert.True(effect.culled);
        AssertClose(0.05f, effect.currentTime);

        UnityRuntime.Tick(0.05f);
        Assert.True(effect.culled);
        AssertClose(0.05f, effect.currentTime);
    }

    [Fact]
    public void ReturningToView_ResumesAfterOneVisibilityFrame()
    {
        using TestScope scope = Scope();
        VisualEffect effect = scope.Effect(new Bounds(new Vector3(4f, 0f, 0f), Vector3.one));
        scope.Camera();
        UnityRuntime.Tick(0.05f);
        UnityRuntime.Tick(0.05f);

        effect.transform!.position = new Vector3(-4f, 0f, 0f);
        UnityRuntime.Tick(0.05f);
        Assert.False(effect.culled);
        AssertClose(0.05f, effect.currentTime);

        UnityRuntime.Tick(0.05f);
        AssertClose(0.1f, effect.currentTime);
    }

    [Fact]
    public void FrameWithoutCamera_NeverCullsStaticBounds()
    {
        using TestScope scope = Scope();
        VisualEffect effect = scope.Effect(new Bounds(new Vector3(100f, 0f, 0f), Vector3.one));

        UnityRuntime.Tick(0.05f);
        UnityRuntime.Tick(0.05f);

        Assert.False(effect.culled);
        AssertClose(0.1f, effect.currentTime);
    }

    [Fact]
    public void MultipleCameras_UseOrVisibility()
    {
        using TestScope scope = Scope();
        VisualEffect effect = scope.Effect(new Bounds(new Vector3(4f, 0f, 0f), Vector3.one));
        scope.Camera();
        Camera visible = scope.Camera();
        visible.worldToCameraMatrix = Matrix4x4.Translate(new Vector3(-4f, 0f, 0f));

        UnityRuntime.Tick(0.05f);
        UnityRuntime.Tick(0.05f);

        Assert.False(effect.culled);
        AssertClose(0.1f, effect.currentTime);
    }

    [Fact]
    public void DisabledCamera_DoesNotContributeVisibility()
    {
        using TestScope scope = Scope();
        VisualEffect effect = scope.Effect(new Bounds(new Vector3(4f, 0f, 0f), Vector3.one));
        scope.Camera();
        Camera camera = scope.Camera();
        camera.worldToCameraMatrix = Matrix4x4.Translate(new Vector3(-4f, 0f, 0f));
        camera.enabled = false;

        UnityRuntime.Tick(0.05f);
        UnityRuntime.Tick(0.05f);

        Assert.True(effect.culled);
        AssertClose(0.05f, effect.currentTime);
    }

    [Fact]
    public void CameraMaskExcludingEffectLayer_CullsVisibleBounds()
    {
        using TestScope scope = Scope();
        VisualEffect effect = scope.Effect(new Bounds(Vector3.zero, Vector3.one), layer: 7);
        Camera camera = scope.Camera();
        camera.cullingMask = 1 << 3;

        UnityRuntime.Tick(0.05f);
        UnityRuntime.Tick(0.05f);

        Assert.True(effect.culled);
        AssertClose(0.05f, effect.currentTime);
    }

    [Fact]
    public void CameraMaskIncludingEffectLayer_KeepsVisibleBoundsActive()
    {
        using TestScope scope = Scope();
        VisualEffect effect = scope.Effect(new Bounds(Vector3.zero, Vector3.one), layer: 7);
        Camera camera = scope.Camera();
        camera.cullingMask = 1 << 7;

        UnityRuntime.Tick(0.05f);
        UnityRuntime.Tick(0.05f);

        Assert.False(effect.culled);
        AssertClose(0.1f, effect.currentTime);
    }

    [Fact]
    public void LocalBounds_TransformRotationAndNonUniformScaleToWorldAabb()
    {
        using TestScope scope = Scope(createDevice: false);
        VisualEffect effect = scope.Effect(new Bounds(
            new Vector3(1f, 0f, 0f), new Vector3(2f, 4f, 6f)));
        effect.transform!.position = new Vector3(5f, 6f, 7f);
        effect.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        effect.transform.localScale = new Vector3(2f, 3f, 4f);

        Assert.True(effect.TryGetWorldCullingBounds(out Bounds world));
        AssertClose(5f, world.center.x);
        AssertClose(8f, world.center.y);
        AssertClose(7f, world.center.z);
        AssertClose(12f, world.size.x);
        AssertClose(4f, world.size.y);
        AssertClose(24f, world.size.z);
    }

    [Fact]
    public void EffectWithoutStaticBounds_AlwaysSimulates()
    {
        using TestScope scope = Scope();
        VisualEffect effect = scope.EffectWithoutBounds();
        effect.transform!.position = new Vector3(100f, 0f, 0f);
        scope.Camera();

        UnityRuntime.Tick(0.05f);
        UnityRuntime.Tick(0.05f);

        Assert.False(effect.culled);
        AssertClose(0.1f, effect.currentTime);
    }

    [Fact]
    public void RenderingSameCameraTwice_SubmitsOneCullingCamera()
    {
        using TestScope scope = Scope();
        VisualEffect effect = scope.Effect(new Bounds(Vector3.zero, Vector3.one));
        Camera camera = scope.Camera();
        Time.Tick(0.05f);
        try
        {
            VFXManager.ProcessPlayerLoopUpdate();
            camera.Render();
            camera.Render();
            VFXManager.CompletePlayerLoopCulling();

            Assert.True(scope.Device!.TryGetVFXCullingState(
                unchecked((ulong)(uint)effect.GetInstanceID()), out var state));
            Assert.Equal(1, state.cameraCount);
            Assert.Equal(1, state.visibleCameraCount);
        }
        finally
        {
            VFXManager.CompletePlayerLoopCulling();
        }
    }

    [Theory]
    [InlineData(CameraType.SceneView)]
    [InlineData(CameraType.Preview)]
    public void EditorCameraTypes_ContributeVisibility(CameraType cameraType)
    {
        using TestScope scope = Scope();
        VisualEffect effect = scope.Effect(new Bounds(Vector3.zero, Vector3.one));
        Camera camera = scope.Camera();
        camera.cameraType = cameraType;

        UnityRuntime.Tick(0.05f);
        UnityRuntime.Tick(0.05f);

        Assert.False(effect.culled);
        AssertClose(0.1f, effect.currentTime);
    }

    [Fact]
    public void RuntimeV12WorldBounds_AreNotTransformedByComponent()
    {
        using TestScope scope = Scope(createDevice: false);
        var runtimeSystem = new VFXRuntimeSystemData(
            "Particles", VFXRuntimeSystemKind.Particle, 64)
        {
            HasStaticBounds = true,
            BoundsInWorldSpace = true,
            BoundsCenterX = 2f,
            BoundsCenterY = 3f,
            BoundsCenterZ = 4f,
            BoundsSizeX = 10f,
            BoundsSizeY = 20f,
            BoundsSizeZ = 30f
        };
        var runtime = new VFXRuntimeAssetData(
            Array.Empty<VFXRuntimeAttributeData>(),
            Array.Empty<string>(),
            Array.Empty<VFXRuntimeInputEventData>(),
            new[] { runtimeSystem },
            Array.Empty<VFXRuntimeOutputEventData>());
        var asset = new VisualEffectAsset();
        asset.ImportRuntimeData(runtime.Serialize());
        var gameObject = new GameObject("World-space VFX");
        VisualEffect effect = gameObject.AddComponent<VisualEffect>();
        effect.visualEffectAsset = asset;
        effect.transform!.position = new Vector3(100f, 100f, 100f);
        try
        {
            Assert.True(effect.TryGetWorldCullingBounds(out Bounds bounds));
            Assert.Equal(new Vector3(2f, 3f, 4f), bounds.center);
            Assert.Equal(new Vector3(10f, 20f, 30f), bounds.size);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    private static TestScope Scope(bool createDevice = true)
    {
        foreach (VisualEffect effect in VFXManager.GetComponents())
            Object.DestroyImmediate(effect.gameObject ?? (Object)effect);
        foreach (Camera camera in Camera.AllCameras)
            Object.DestroyImmediate(camera.gameObject ?? (Object)camera);
        return new TestScope(createDevice);
    }

    private static void AssertClose(float expected, float actual)
        => Assert.InRange(actual, expected - 0.00001f, expected + 0.00001f);

    private sealed class TestScope : IDisposable
    {
        private readonly List<Object> _objects = new();
        private readonly RenderPipeline? _previousPipeline;
        private readonly float _previousFixed = VFXManager.fixedTimeStep;
        private readonly float _previousMax = VFXManager.maxDeltaTime;

        internal TestScope(bool createDevice)
        {
            _previousPipeline = RenderPipelineManager.currentPipeline;
            RenderPipelineManager.SetCurrentPipeline(new TestRenderPipeline());
            VFXManager.fixedTimeStep = 1f / 60f;
            VFXManager.maxDeltaTime = 1f / 20f;
            Time.timeScale = 1f;
            if (createDevice)
            {
                Device = NativeGraphicsDevice.Create(GraphicsDeviceType.Null, 16, 16, false);
                if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
                    Assert.NotEqual(IntPtr.Zero, Device.Handle);
            }
        }

        internal NativeGraphicsDevice? Device { get; }

        internal VisualEffect Effect(Bounds bounds, int layer = 0)
        {
            var asset = new VisualEffectAsset();
            asset.DefineParticleSystem("Particles", new VFXParticleSystemInfo(0, 64, false, bounds));
            var gameObject = new GameObject("VFX Culling Effect") { layer = layer };
            _objects.Add(gameObject);
            VisualEffect effect = gameObject.AddComponent<VisualEffect>();
            effect.visualEffectAsset = asset;
            return effect;
        }

        internal VisualEffect EffectWithoutBounds()
        {
            var gameObject = new GameObject("Unbounded VFX Effect");
            _objects.Add(gameObject);
            return gameObject.AddComponent<VisualEffect>();
        }

        internal Camera Camera()
        {
            var gameObject = new GameObject("VFX Culling Camera");
            _objects.Add(gameObject);
            Camera camera = gameObject.AddComponent<Camera>();
            camera.projectionMatrix = Matrix4x4.identity;
            camera.worldToCameraMatrix = Matrix4x4.identity;
            return camera;
        }

        public void Dispose()
        {
            try { VFXManager.CompletePlayerLoopCulling(); } catch { }
            foreach (Object item in _objects.AsEnumerable().Reverse())
                if (item != null && !item.IsDestroyed) Object.DestroyImmediate(item);
            Device?.Dispose();
            RenderPipelineManager.SetCurrentPipeline(_previousPipeline);
            VFXManager.fixedTimeStep = _previousFixed;
            VFXManager.maxDeltaTime = _previousMax;
        }
    }

    private sealed class TestRenderPipeline : RenderPipeline
    {
        protected override void ExecuteRender(ScriptableRenderContext context, Camera camera)
        {
        }
    }
}
