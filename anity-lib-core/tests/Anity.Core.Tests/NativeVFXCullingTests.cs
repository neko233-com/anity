using System.Runtime.InteropServices;
using Anity.Core.Runtime.Native;
using UnityEngine.Rendering;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVFXCullingTests
{
    [Fact]
    public void NativeLayouts_MatchCAbi()
    {
        Assert.Equal(40, Marshal.SizeOf<AnityNative.GraphicsVFXCullingBounds>());
        Assert.Equal(80, Marshal.SizeOf<AnityNative.GraphicsVFXCullingCamera>());
        Assert.Equal(40, Marshal.SizeOf<AnityNative.GraphicsVFXCullingState>());
    }

    [Fact]
    public void VisibleBounds_RemainSimulated()
    {
        using NativeGraphicsDevice device = Device();
        Begin(device, 1, Bounds(101, 0f));
        Assert.True(device.SubmitVFXCullingCamera(1, Camera(1)));
        Assert.True(device.CompleteVFXCullingFrame(1));

        AnityNative.GraphicsVFXCullingState state = State(device, 101);
        Assert.Equal(0, state.culled);
        Assert.Equal(1, state.cameraCount);
        Assert.Equal(1, state.visibleCameraCount);
    }

    [Fact]
    public void BoundsOutsideAllClipPlanes_AreCulled()
    {
        using NativeGraphicsDevice device = Device();
        Begin(device, 1, Bounds(102, 4f));
        Assert.True(device.SubmitVFXCullingCamera(1, Camera(1)));
        Assert.True(device.CompleteVFXCullingFrame(1));

        Assert.Equal(1, State(device, 102).culled);
    }

    [Fact]
    public void CompletedVisibility_IsPublishedAfterSimulationSnapshot()
    {
        using NativeGraphicsDevice device = Device();
        Begin(device, 1, Bounds(103, 4f));
        Assert.Equal(0, State(device, 103).culled);
        Assert.True(device.SubmitVFXCullingCamera(1, Camera(1)));
        Assert.Equal(0, State(device, 103).culled);
        Assert.True(device.CompleteVFXCullingFrame(1));
        Assert.Equal(1, State(device, 103).culled);
    }

    [Fact]
    public void FrameWithoutCamera_AlwaysSimulates()
    {
        using NativeGraphicsDevice device = Device();
        Begin(device, 1, Bounds(104, 100f));
        Assert.True(device.CompleteVFXCullingFrame(1));

        AnityNative.GraphicsVFXCullingState state = State(device, 104);
        Assert.Equal(0, state.culled);
        Assert.Equal(0, state.cameraCount);
    }

    [Fact]
    public void EffectWithoutStaticBounds_AlwaysSimulates()
    {
        using NativeGraphicsDevice device = Device();
        AnityNative.GraphicsVFXCullingBounds bounds = Bounds(105, 100f);
        bounds.valid = 0;
        Begin(device, 1, bounds);
        Assert.True(device.SubmitVFXCullingCamera(1, Camera(1)));
        Assert.True(device.CompleteVFXCullingFrame(1));

        AnityNative.GraphicsVFXCullingState state = State(device, 105);
        Assert.Equal(0, state.culled);
        Assert.Equal(0, state.hasBounds);
    }

    [Fact]
    public void CameraCullingMask_ExcludesEffectLayer()
    {
        using NativeGraphicsDevice device = Device();
        AnityNative.GraphicsVFXCullingBounds bounds = Bounds(106, 0f);
        bounds.layer = 7;
        Begin(device, 1, bounds);
        AnityNative.GraphicsVFXCullingCamera camera = Camera(1);
        camera.cullingMask = 1 << 3;
        Assert.True(device.SubmitVFXCullingCamera(1, camera));
        Assert.True(device.CompleteVFXCullingFrame(1));

        Assert.Equal(1, State(device, 106).culled);
    }

    [Fact]
    public void MultipleCameras_OrVisibilityAcrossStack()
    {
        using NativeGraphicsDevice device = Device();
        Begin(device, 1, Bounds(107, 4f));
        Assert.True(device.SubmitVFXCullingCamera(1, Camera(1)));
        AnityNative.GraphicsVFXCullingCamera translated = Camera(2);
        translated.m03 = -4f;
        Assert.True(device.SubmitVFXCullingCamera(1, translated));
        Assert.True(device.CompleteVFXCullingFrame(1));

        AnityNative.GraphicsVFXCullingState state = State(device, 107);
        Assert.Equal(0, state.culled);
        Assert.Equal(2, state.cameraCount);
        Assert.Equal(1, state.visibleCameraCount);
    }

    [Fact]
    public void DuplicateCamera_IsRejectedWithoutChangingCounts()
    {
        using NativeGraphicsDevice device = Device();
        Begin(device, 1, Bounds(108, 0f));
        Assert.True(device.SubmitVFXCullingCamera(1, Camera(1)));
        Assert.False(device.SubmitVFXCullingCamera(1, Camera(1)));
        Assert.True(device.CompleteVFXCullingFrame(1));

        Assert.Equal(1, State(device, 108).cameraCount);
    }

    [Fact]
    public void DuplicateEffectBounds_AreRejectedTransactionally()
    {
        using NativeGraphicsDevice device = Device();
        Assert.True(device.BeginVFXPlayerLoopFrame(1, out _, out _));
        Assert.False(device.BeginVFXCullingFrame(
            1, new[] { Bounds(109, 0f), Bounds(109, 4f) }));
        Assert.False(device.TryGetVFXCullingState(109, out _));
    }

    [Fact]
    public void NonFiniteCameraMatrix_IsRejected()
    {
        using NativeGraphicsDevice device = Device();
        Begin(device, 1, Bounds(110, 0f));
        AnityNative.GraphicsVFXCullingCamera camera = Camera(1);
        camera.m22 = float.NaN;
        Assert.False(device.SubmitVFXCullingCamera(1, camera));
        Assert.True(device.CompleteVFXCullingFrame(1));
        Assert.Equal(0, State(device, 110).cameraCount);
    }

    [Fact]
    public void CompleteTwiceAndStaleToken_AreRejected()
    {
        using NativeGraphicsDevice device = Device();
        Begin(device, 2, Bounds(111, 0f));
        Assert.True(device.CompleteVFXCullingFrame(2));
        Assert.False(device.CompleteVFXCullingFrame(2));
        Assert.False(device.BeginVFXCullingFrame(1, new[] { Bounds(111, 0f) }));
    }

    [Fact]
    public void NextFramePreservesPriorCullUntilNewCameraResultCompletes()
    {
        using NativeGraphicsDevice device = Device();
        Begin(device, 1, Bounds(112, 4f));
        Assert.True(device.SubmitVFXCullingCamera(1, Camera(1)));
        Assert.True(device.CompleteVFXCullingFrame(1));
        Assert.Equal(1, State(device, 112).culled);

        Begin(device, 2, Bounds(112, 0f));
        Assert.Equal(1, State(device, 112).culled);
        Assert.True(device.SubmitVFXCullingCamera(2, Camera(2)));
        Assert.True(device.CompleteVFXCullingFrame(2));
        Assert.Equal(0, State(device, 112).culled);
    }

    [Fact]
    public void ClearEffect_RemovesCullingState()
    {
        using NativeGraphicsDevice device = Device();
        Begin(device, 1, Bounds(113, 0f));
        Assert.True(device.CompleteVFXCullingFrame(1));
        Assert.True(device.TryGetVFXCullingState(113, out _));
        Assert.True(device.ClearVFXEffectState(113));
        Assert.False(device.TryGetVFXCullingState(113, out _));
    }

    private static void Begin(
        NativeGraphicsDevice device, ulong token,
        params AnityNative.GraphicsVFXCullingBounds[] bounds)
    {
        Assert.NotEqual(IntPtr.Zero, device.Handle);
        Assert.True(device.BeginVFXPlayerLoopFrame(token, out _, out bool began));
        Assert.True(began);
        Assert.True(device.BeginVFXCullingFrame(token, bounds));
    }

    private static AnityNative.GraphicsVFXCullingState State(
        NativeGraphicsDevice device, ulong effectId)
    {
        Assert.True(device.TryGetVFXCullingState(effectId, out var state));
        Assert.Equal(effectId, state.effectId);
        Assert.True(state.generation > 0);
        return state;
    }

    private static AnityNative.GraphicsVFXCullingBounds Bounds(
        ulong effectId, float centerX)
        => new()
        {
            effectId = effectId,
            centerX = centerX,
            centerY = 0f,
            centerZ = 0f,
            extentsX = 0.25f,
            extentsY = 0.25f,
            extentsZ = 0.25f,
            layer = 0,
            valid = 1
        };

    private static AnityNative.GraphicsVFXCullingCamera Camera(ulong cameraId)
        => new()
        {
            cameraId = cameraId,
            m00 = 1f,
            m11 = 1f,
            m22 = 1f,
            m33 = 1f,
            cullingMask = -1,
            cameraType = 1
        };

    private static NativeGraphicsDevice Device()
    {
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Null, 16, 16, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        return device;
    }
}
