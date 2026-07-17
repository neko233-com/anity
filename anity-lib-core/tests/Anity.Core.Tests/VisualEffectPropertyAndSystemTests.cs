using System.Reflection;
using UnityEngine;
using UnityEngine.VFX;
using Xunit;
using RenderingTextureDimension = UnityEngine.Rendering.TextureDimension;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class VisualEffectPropertyAndSystemTests
{
    [Fact]
    public void ScalarDefaultsOverridesAndBothNameFormsRoundTrip()
    {
        (VisualEffect effect, _) = CreateEffect();
        Assert.True(effect.GetBool("Enabled"));
        Assert.Equal(-3, effect.GetInt("Index"));
        Assert.Equal(9u, effect.GetUInt("Flags"));
        Assert.Equal(2.5f, effect.GetFloat("Rate"));
        effect.SetBool("Enabled", false);
        effect.SetInt(Shader.PropertyToID("Index"), 27);
        effect.SetUInt("Flags", uint.MaxValue);
        effect.SetFloat(Shader.PropertyToID("Rate"), -0.25f);
        Assert.False(effect.GetBool(Shader.PropertyToID("Enabled")));
        Assert.Equal(27, effect.GetInt("Index"));
        Assert.Equal(uint.MaxValue, effect.GetUInt("Flags"));
        Assert.Equal(-0.25f, effect.GetFloat("Rate"));
    }

    [Fact]
    public void VectorAndMatrixOverridesRemainStronglyTyped()
    {
        (VisualEffect effect, _) = CreateEffect();
        var v2 = new Vector2(1, 2);
        var v3 = new Vector3(3, 4, 5);
        var v4 = new Vector4(6, 7, 8, 9);
        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.m23 = 11;
        effect.SetVector2("V2", v2);
        effect.SetVector3("V3", v3);
        effect.SetVector4("V4", v4);
        effect.SetMatrix4x4("Matrix", matrix);
        Assert.Equal(v2, effect.GetVector2("V2"));
        Assert.Equal(v3, effect.GetVector3("V3"));
        Assert.Equal(v4, effect.GetVector4("V4"));
        Assert.Equal(matrix, effect.GetMatrix4x4("Matrix"));
        Assert.False(effect.HasFloat("V2"));
    }

    [Fact]
    public void ObjectOverridesReturnOriginalEngineObjects()
    {
        (VisualEffect effect, _) = CreateEffect();
        var texture = new Texture2D(2, 2);
        var curve = AnimationCurve.Linear(0, 1, 2, 3);
        var gradient = new Gradient();
        var mesh = new Mesh();
        var renderer = new SkinnedMeshRenderer();
        using var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 4, 16);
        effect.SetTexture("MainTex", texture);
        effect.SetAnimationCurve("Curve", curve);
        effect.SetGradient("Gradient", gradient);
        effect.SetMesh("Mesh", mesh);
        effect.SetSkinnedMeshRenderer("Skin", renderer);
        effect.SetGraphicsBuffer("Buffer", buffer);
        Assert.Same(texture, effect.GetTexture("MainTex"));
        Assert.Same(curve, effect.GetAnimationCurve("Curve"));
        Assert.Same(gradient, effect.GetGradient("Gradient"));
        Assert.Same(mesh, effect.GetMesh("Mesh"));
        Assert.Same(renderer, effect.GetSkinnedMeshRenderer("Skin"));
        Assert.True(effect.HasGraphicsBuffer("Buffer"));
    }

    [Fact]
    public void UnknownAndWrongTypedOverridesAreIgnored()
    {
        (VisualEffect effect, _) = CreateEffect();
        effect.SetFloat("Missing", 99);
        effect.SetInt("Rate", 8);
        Assert.False(effect.HasFloat("Missing"));
        Assert.False(effect.HasInt("Rate"));
        Assert.Equal(0f, effect.GetFloat("Missing"));
        Assert.Equal(2.5f, effect.GetFloat("Rate"));
    }

    [Fact]
    public void ResetOverrideRestoresCompiledAssetDefault()
    {
        (VisualEffect effect, _) = CreateEffect();
        effect.SetFloat("Rate", 100);
        effect.ResetOverride(Shader.PropertyToID("Rate"));
        Assert.Equal(2.5f, effect.GetFloat("Rate"));
        effect.SetInt("Index", 12);
        effect.ResetOverride("Index");
        Assert.Equal(-3, effect.GetInt("Index"));
    }

    [Fact]
    public void AssigningAnotherAssetClearsComponentOverrides()
    {
        (VisualEffect effect, _) = CreateEffect();
        effect.SetFloat("Rate", 100);
        var replacement = new VisualEffectAsset();
        replacement.DefineExposedProperty("Rate", typeof(float), defaultValue: 7f);
        effect.visualEffectAsset = replacement;
        Assert.Equal(7f, effect.GetFloat("Rate"));
    }

    [Fact]
    public void TextureDimensionComesFromCompiledPropertyMetadata()
    {
        (VisualEffect effect, _) = CreateEffect();
        Assert.Equal(RenderingTextureDimension.Tex2D, effect.GetTextureDimension("MainTex"));
        Assert.Equal(RenderingTextureDimension.Unknown, effect.GetTextureDimension("Missing"));
    }

    [Fact]
    public void NativeNotNullObjectSettersRejectNull()
    {
        (VisualEffect effect, _) = CreateEffect();
        Assert.Throws<ArgumentNullException>(() => effect.SetTexture("MainTex", null!));
        Assert.Throws<ArgumentNullException>(() => effect.SetAnimationCurve("Curve", null!));
        Assert.Throws<ArgumentNullException>(() => effect.SetGradient("Gradient", null!));
        Assert.Throws<ArgumentNullException>(() => effect.SetMesh("Mesh", null!));
    }

    [Fact]
    public void SystemNameQueriesReplaceCallerContentsByCategory()
    {
        (VisualEffect effect, VisualEffectAsset asset) = CreateEffect();
        asset.DefineParticleSystem("Particles", new VFXParticleSystemInfo(4, 64, false, default));
        using var spawn = new VFXSpawnerState { playing = true };
        asset.DefineSpawnSystem("Spawner", spawn);
        asset.DefineOutputEventSystem("Impact");
        var names = new List<string> { "stale" };
        effect.GetSystemNames(names);
        Assert.Equal(new[] { "Particles", "Spawner", "Impact" }, names);
        effect.GetParticleSystemNames(names);
        Assert.Equal(new[] { "Particles" }, names);
        effect.GetSpawnSystemNames(names);
        Assert.Equal(new[] { "Spawner" }, names);
        effect.GetOutputEventNames(names);
        Assert.Equal(new[] { "Impact" }, names);
        Assert.True(effect.HasSystem("Spawner"));
        Assert.False(effect.HasSystem("Missing"));
    }

    [Fact]
    public void ParticleSystemInfoReturnsCompiledRuntimeSnapshotAndThrowsForMissing()
    {
        (VisualEffect effect, VisualEffectAsset asset) = CreateEffect();
        var bounds = new Bounds(new Vector3(1, 2, 3), new Vector3(4, 5, 6));
        asset.DefineParticleSystem("Particles", new VFXParticleSystemInfo(17, 128, false, bounds));
        VFXParticleSystemInfo info = effect.GetParticleSystemInfo("Particles");
        Assert.Equal(17u, info.aliveCount);
        Assert.Equal(128u, info.capacity);
        Assert.False(info.sleeping);
        Assert.Equal(bounds.center, info.bounds.center);
        Assert.Throws<ArgumentException>(() => effect.GetParticleSystemInfo("Missing"));
    }

    [Fact]
    public void SpawnSystemInfoCopiesFullStateWithoutAliasing()
    {
        (VisualEffect effect, VisualEffectAsset asset) = CreateEffect();
        using var compiled = new VFXSpawnerState
        {
            playing = true,
            spawnCount = 12,
            deltaTime = 0.25f,
            totalTime = 3,
            delayBeforeLoop = 1,
            loopDuration = 2,
            delayAfterLoop = 4,
            loopIndex = 5,
            loopCount = 6
        };
        compiled.vfxEventAttribute.SetFloat(99, 8);
        asset.DefineSpawnSystem("Spawner", compiled);
        using VFXSpawnerState first = effect.GetSpawnSystemInfo("Spawner");
        using var destination = new VFXSpawnerState();
        effect.GetSpawnSystemInfo(Shader.PropertyToID("Spawner"), destination);
        first.spawnCount = 99;
        Assert.True(destination.playing);
        Assert.Equal(12, destination.spawnCount);
        Assert.Equal(0.25f, destination.deltaTime);
        Assert.Equal(8, destination.vfxEventAttribute.GetFloat(99));
        Assert.Throws<ArgumentException>(() => effect.GetSpawnSystemInfo("Missing"));
    }

    [Fact]
    public void AwakeQueryReflectsParticleAndSpawnerState()
    {
        (VisualEffect effect, VisualEffectAsset asset) = CreateEffect();
        asset.DefineParticleSystem("Sleeping", new VFXParticleSystemInfo(5, 8, true, default));
        using var stopped = new VFXSpawnerState { playing = false };
        asset.DefineSpawnSystem("Stopped", stopped);
        Assert.False(effect.HasAnySystemAwake());
        asset.DefineParticleSystem("Awake", new VFXParticleSystemInfo(1, 8, false, default));
        Assert.True(effect.HasAnySystemAwake());
    }

    [Fact]
    public void SpawnerStatePlayingLoopAndDisposalHaveRuntimeSemantics()
    {
        var state = new VFXSpawnerState();
        Assert.False(state.playing);
        state.playing = true;
        Assert.Equal(VFXSpawnerLoopState.Looping, state.loopState);
        state.loopIndex = 1;
        Assert.True(state.newLoop);
        state.playing = false;
        Assert.Equal(VFXSpawnerLoopState.Finished, state.loopState);
        state.Dispose();
        state.Dispose();
        Assert.Throws<ObjectDisposedException>(() => state.spawnCount = 1);
    }

    [Fact]
    public void NewPublicVfxTypesCarryOfficialShapeMetadata()
    {
        Assert.True(typeof(VFXSpawnerState).IsSealed);
        Assert.Contains(typeof(IDisposable), typeof(VFXSpawnerState).GetInterfaces());
        Assert.Equal(typeof(int), typeof(VisualEffect).GetProperty(nameof(VisualEffect.aliveParticleCount))!.PropertyType);
        Assert.Equal(4, Enum.GetValues<VFXSpawnerLoopState>().Length);
        Assert.Equal(4, typeof(VFXParticleSystemInfo).GetFields(BindingFlags.Public | BindingFlags.Instance).Length);
    }

    private static (VisualEffect Effect, VisualEffectAsset Asset) CreateEffect()
    {
        var asset = new VisualEffectAsset();
        asset.DefineExposedProperty("Enabled", typeof(bool), defaultValue: true);
        asset.DefineExposedProperty("Index", typeof(int), defaultValue: -3);
        asset.DefineExposedProperty("Flags", typeof(uint), defaultValue: 9u);
        asset.DefineExposedProperty("Rate", typeof(float), defaultValue: 2.5f);
        asset.DefineExposedProperty("V2", typeof(Vector2));
        asset.DefineExposedProperty("V3", typeof(Vector3));
        asset.DefineExposedProperty("V4", typeof(Vector4));
        asset.DefineExposedProperty("Matrix", typeof(Matrix4x4));
        asset.DefineExposedProperty("MainTex", typeof(Texture2D), RenderingTextureDimension.Tex2D);
        asset.DefineExposedProperty("Curve", typeof(AnimationCurve));
        asset.DefineExposedProperty("Gradient", typeof(Gradient));
        asset.DefineExposedProperty("Mesh", typeof(Mesh));
        asset.DefineExposedProperty("Skin", typeof(SkinnedMeshRenderer));
        asset.DefineExposedProperty("Buffer", typeof(GraphicsBuffer));
        return (new VisualEffect { visualEffectAsset = asset }, asset);
    }
}
