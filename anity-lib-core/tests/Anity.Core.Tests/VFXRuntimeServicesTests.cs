using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class VFXRuntimeServicesTests
{
    [Fact]
    public void ExpressionScalarsRoundTripThroughStringAndId()
    {
        VFXExpressionValues values = VFXExpressionValues.Create();
        values.SetValue("Bool", true);
        values.SetValue("Int", -7);
        values.SetValue("UInt", 8u);
        values.SetValue("Float", 1.25f);
        Assert.True(values.GetBool(Shader.PropertyToID("Bool")));
        Assert.Equal(-7, values.GetInt("Int"));
        Assert.Equal(8u, values.GetUInt("UInt"));
        Assert.Equal(1.25f, values.GetFloat("Float"));
    }

    [Fact]
    public void ExpressionVectorsAndMatrixRoundTripExactly()
    {
        VFXExpressionValues values = VFXExpressionValues.Create();
        var v2 = new Vector2(1, 2);
        var v3 = new Vector3(3, 4, 5);
        var v4 = new Vector4(6, 7, 8, 9);
        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.m31 = 10;
        values.SetValue("V2", v2);
        values.SetValue("V3", v3);
        values.SetValue("V4", v4);
        values.SetValue("M", matrix);
        Assert.Equal(v2, values.GetVector2("V2"));
        Assert.Equal(v3, values.GetVector3("V3"));
        Assert.Equal(v4, values.GetVector4("V4"));
        Assert.Equal(matrix, values.GetMatrix4x4("M"));
    }

    [Fact]
    public void ExpressionTextureAndMeshKeepEngineObjectIdentity()
    {
        VFXExpressionValues values = VFXExpressionValues.Create();
        var texture = new Texture2D(1, 1);
        var mesh = new Mesh();
        values.SetValue("Texture", texture);
        values.SetValue("Mesh", mesh);
        Assert.Same(texture, values.GetTexture("Texture"));
        Assert.Same(mesh, values.GetMesh("Mesh"));
    }

    [Fact]
    public void ExpressionCurveReturnsIndependentNativeStyleCopy()
    {
        VFXExpressionValues values = VFXExpressionValues.Create();
        AnimationCurve source = AnimationCurve.Linear(0, 0, 1, 1);
        source.preWrapMode = WrapMode.Loop;
        values.SetValue("Curve", source);
        AnimationCurve copy = values.GetAnimationCurve("Curve");
        copy.keys = Array.Empty<Keyframe>();
        Assert.NotSame(source, copy);
        Assert.Equal(2, source.length);
        Assert.Equal(WrapMode.Loop, copy.preWrapMode);
    }

    [Fact]
    public void ExpressionGradientReturnsIndependentNativeStyleCopy()
    {
        VFXExpressionValues values = VFXExpressionValues.Create();
        var source = new Gradient { mode = GradientMode.Fixed };
        values.SetValue("Gradient", source);
        Gradient copy = values.GetGradient("Gradient");
        copy.mode = GradientMode.Blend;
        Assert.NotSame(source, copy);
        Assert.Equal(GradientMode.Fixed, source.mode);
    }

    [Fact]
    public void ExpressionMissingAndWrongTypesThrowInsteadOfCoercing()
    {
        VFXExpressionValues values = VFXExpressionValues.Create();
        values.SetValue("Value", 5);
        Assert.Throws<ArgumentException>(() => values.GetFloat("Value"));
        Assert.Throws<ArgumentException>(() => values.GetBool("Missing"));
    }

    [Fact]
    public void SpawnerCallbacksReceiveSharedRuntimeState()
    {
        var callbacks = new RecordingSpawnerCallbacks();
        using var state = new VFXSpawnerState { playing = true };
        VFXExpressionValues values = VFXExpressionValues.Create();
        values.SetValue("Rate", 3f);
        var effect = new VisualEffect();
        callbacks.OnPlay(state, values, effect);
        callbacks.OnUpdate(state, values, effect);
        callbacks.OnStop(state, values, effect);
        Assert.Equal(new[] { "play:3", "update:3", "stop:3" }, callbacks.Calls);
    }

    [Fact]
    public void CameraAndBatchValueTypesExposeOfficialFieldsAndFlags()
    {
        var xr = new VFXCameraXRSettings { viewTotal = 4, viewCount = 2, viewOffset = 1 };
        Assert.Equal(4u, xr.viewTotal);
        Assert.Equal(VFXCameraBufferTypes.Depth | VFXCameraBufferTypes.Normal,
            (VFXCameraBufferTypes)5);
        Assert.Equal(9, typeof(VFXBatchedEffectInfo).GetFields().Length);
    }

    [Fact]
    public void ManagerTimeStepsValidateAndPersistFinitePositiveValues()
    {
        float oldFixed = VFXManager.fixedTimeStep;
        float oldMax = VFXManager.maxDeltaTime;
        try
        {
            VFXManager.fixedTimeStep = 0.02f;
            VFXManager.maxDeltaTime = 0.15f;
            Assert.Equal(0.02f, VFXManager.fixedTimeStep);
            Assert.Equal(0.15f, VFXManager.maxDeltaTime);
            Assert.Throws<ArgumentOutOfRangeException>(() => VFXManager.fixedTimeStep = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => VFXManager.maxDeltaTime = float.NaN);
        }
        finally
        {
            VFXManager.fixedTimeStep = oldFixed;
            VFXManager.maxDeltaTime = oldMax;
        }
    }

    [Fact]
    public void ManagerEnumeratesLiveVisualEffectComponents()
    {
        var effect = new VisualEffect();
        Assert.Contains(effect, VFXManager.GetComponents());
        UnityEngine.Object.DestroyImmediate(effect);
        Assert.DoesNotContain(effect, VFXManager.GetComponents());
    }

    [Fact]
    public void BatchedInfoAggregatesActiveAndInactiveInstancesPerAsset()
    {
        var asset = new VisualEffectAsset();
        var active = new VisualEffect { visualEffectAsset = asset, enabled = true };
        var inactive = new VisualEffect { visualEffectAsset = asset, enabled = false };
        VFXBatchedEffectInfo info = VFXManager.GetBatchedEffectInfo(asset);
        Assert.Same(asset, info.vfxAsset);
        Assert.Equal(1u, info.activeInstanceCount);
        Assert.Equal(2u, info.totalInstanceCapacity);
        Assert.Equal(1u, info.activeBatchCount);
        Assert.Equal(1u, info.inactiveBatchCount);
        UnityEngine.Object.DestroyImmediate(active);
        UnityEngine.Object.DestroyImmediate(inactive);
    }

    [Fact]
    public void BatchedInfoListReplacesCallerContentsAndGroupsAssets()
    {
        var asset = new VisualEffectAsset();
        var effect = new VisualEffect { visualEffectAsset = asset };
        var infos = new List<VFXBatchedEffectInfo> { new() };
        VFXManager.GetBatchedEffectInfos(infos);
        Assert.Contains(infos, info => ReferenceEquals(info.vfxAsset, asset));
        Assert.DoesNotContain(infos, info => info.vfxAsset is null);
        UnityEngine.Object.DestroyImmediate(effect);
    }

    [Fact]
    public void CameraRequirementsAndBufferBindingShareRuntimeState()
    {
        var asset = new VisualEffectAsset
        {
            CameraBufferRequirements = VFXCameraBufferTypes.Depth | VFXCameraBufferTypes.Color
        };
        var effect = new VisualEffect { visualEffectAsset = asset };
        var camera = new Camera();
        var texture = new Texture2D(8, 8);
        Assert.Equal(VFXCameraBufferTypes.Depth | VFXCameraBufferTypes.Color,
            VFXManager.IsCameraBufferNeeded(camera));
        VFXManager.SetCameraBuffer(camera, VFXCameraBufferTypes.Depth, texture, 1, 2, 3, 4);
        Assert.True(VFXManager.TryGetCameraBuffer(camera, VFXCameraBufferTypes.Depth,
            out Texture? stored, out RectInt viewport));
        Assert.Same(texture, stored);
        Assert.Equal(new RectInt(1, 2, 3, 4), viewport);
        UnityEngine.Object.DestroyImmediate(effect);
        UnityEngine.Object.DestroyImmediate(camera);
        VFXManager.FlushEmptyBatches();
    }

    [Fact]
    public void CameraProcessingValidatesXrViewsAndAdvancesRunningEffects()
    {
        var camera = new Camera();
        var effect = new VisualEffect();
        using var commands = new CommandBuffer();
        float before = effect.currentTime;
        VFXManager.PrepareCamera(camera, new VFXCameraXRSettings { viewTotal = 2, viewCount = 1, viewOffset = 1 });
        VFXManager.ProcessCameraCommand(camera, commands,
            new VFXCameraXRSettings { viewTotal = 1, viewCount = 1 }, default);
        Assert.True(effect.currentTime >= before);
        Assert.Throws<ArgumentOutOfRangeException>(() => VFXManager.PrepareCamera(camera, default));
        UnityEngine.Object.DestroyImmediate(effect);
        UnityEngine.Object.DestroyImmediate(camera);
    }

    [Fact]
    public void ManagerNativeNotNullInputsRejectNull()
    {
        Assert.Throws<ArgumentNullException>(() => VFXManager.GetBatchedEffectInfo(null!));
        Assert.Throws<ArgumentNullException>(() => VFXManager.GetBatchedEffectInfos(null!));
        Assert.Throws<ArgumentNullException>(() => VFXManager.PrepareCamera(null!));
        Assert.Throws<ArgumentNullException>(() => VFXManager.IsCameraBufferNeeded(null!));
    }

    private sealed class RecordingSpawnerCallbacks : VFXSpawnerCallbacks
    {
        internal List<string> Calls { get; } = new();

        public override void OnPlay(VFXSpawnerState state, VFXExpressionValues vfxValues, VisualEffect vfxComponent)
            => Calls.Add($"play:{vfxValues.GetFloat("Rate")}");

        public override void OnUpdate(VFXSpawnerState state, VFXExpressionValues vfxValues, VisualEffect vfxComponent)
            => Calls.Add($"update:{vfxValues.GetFloat("Rate")}");

        public override void OnStop(VFXSpawnerState state, VFXExpressionValues vfxValues, VisualEffect vfxComponent)
            => Calls.Add($"stop:{vfxValues.GetFloat("Rate")}");
    }
}
