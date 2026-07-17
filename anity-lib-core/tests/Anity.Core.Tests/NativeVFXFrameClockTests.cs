using System.Runtime.InteropServices;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVFXFrameClockTests
{
    private const float FixedStep = 1f / 60f;
    private const float MaxDelta = 1f / 20f;

    [Fact]
    public void NativeLayout_MatchesCAbi()
        => Assert.Equal(48, Marshal.SizeOf<AnityNative.GraphicsVFXFrameState>());

    [Fact]
    public void VisualEffectCache_IsSourcedFromNativePrepareAndCommit()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        var effect = new VisualEffect { playRate = 1.75f };
        try
        {
            Assert.True(device.BeginVFXFrame(out uint frame));
            float delta = effect.PrepareVfxFrame(0.0625f, frame, device);
            AssertClose(0.0875f, delta);
            Assert.Equal(frame, effect.currentVfxFrameIndex);
            AssertClose(0f, effect.currentTime);
            Assert.True(device.TryGetVFXEffectFrameState(
                unchecked((ulong)(uint)effect.GetInstanceID()), out var prepared));
            Assert.Equal(1u, prepared.prepared);

            effect.CompleteVfxFrame(device);

            AssertClose(delta, effect.currentTime);
            Assert.True(device.TryGetVFXEffectFrameState(
                unchecked((ulong)(uint)effect.GetInstanceID()), out var committed));
            Assert.Equal(0u, committed.prepared);
            AssertClose(committed.totalTime, effect.currentTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void BeginFrame_AdvancesDeviceGlobalClock()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.BeginVFXFrame(out uint first));
        Assert.True(device.BeginVFXFrame(out uint second));
        Assert.Equal(unchecked(first + 1u), second);
    }

    [Fact]
    public void SamePlayerLoopToken_ReusesOneNativeFrame()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.BeginVFXPlayerLoopFrame(10, out uint first, out bool beganFirst));
        Assert.True(beganFirst);
        Assert.True(device.BeginVFXPlayerLoopFrame(10, out uint second, out bool beganSecond));
        Assert.False(beganSecond);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void MonotonicPlayerLoopTokens_AdvanceExactlyOnce(int tokenCount)
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        uint first = 0;
        uint current = 0;

        for (ulong token = 1; token <= (ulong)tokenCount; token++)
        {
            Assert.True(device.BeginVFXPlayerLoopFrame(token, out current, out bool began));
            Assert.True(began);
            if (token == 1) first = current;
        }

        Assert.Equal(unchecked(first + (uint)tokenCount - 1u), current);
    }

    [Fact]
    public void StalePlayerLoopToken_IsRejected()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXPlayerLoopFrame(20, out _, out _));

        Assert.False(device.BeginVFXPlayerLoopFrame(19, out _, out _));
    }

    [Fact]
    public void ZeroPlayerLoopToken_IsRejectedByNativeAbi()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;

        Assert.Equal(AnityNative.Result.InvalidArg,
            AnityNative.Graphics_BeginVFXPlayerLoopFrame(
                device.Handle, 0, out _, out _));
    }

    [Fact]
    public void ExplicitFrame_InvalidatesReuseOfCurrentPlayerLoopToken()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXPlayerLoopFrame(30, out _, out _));
        Assert.True(device.BeginVFXFrame(out _));

        Assert.False(device.BeginVFXPlayerLoopFrame(30, out _, out _));
        Assert.True(device.BeginVFXPlayerLoopFrame(31, out _, out bool began));
        Assert.True(began);
    }

    [Fact]
    public void MultipleEffects_ShareOneFrameIndex()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectFrame(
            101, frame, 0.05f, 1f, FixedStep, MaxDelta, false, out var first));
        Assert.True(device.PrepareVFXEffectFrame(
            102, frame, 0.05f, 1f, FixedStep, MaxDelta, false, out var second));

        Assert.Equal(frame, first.frameIndex);
        Assert.Equal(frame, second.frameIndex);
        Assert.True(device.CommitVFXEffectFrame(101, frame, out _));
        Assert.True(device.CommitVFXEffectFrame(102, frame, out _));
    }

    [Fact]
    public void UnityRuntimePlayerLoop_AdvancesVfxWithoutAnyCamera()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect();
        try
        {
            Time.timeScale = 1f;
            UnityRuntime.Tick(0.05f);

            AssertClose(0.05f, effect.currentTime);
            AssertClose(0.05f, effect.currentVfxDeltaTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void PlayerLoopCameraCommands_DoNotAdvanceEffectTwice()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect();
        var firstCamera = new Camera();
        var secondCamera = new Camera();
        using var commands = new CommandBuffer();
        try
        {
            Time.timeScale = 1f;
            UnityRuntime.Tick(0.05f);
            float afterPlayerLoop = effect.currentTime;
            uint frame = effect.currentVfxFrameIndex;

            VFXManager.ProcessCameraCommand(firstCamera, commands,
                new VFXCameraXRSettings { viewTotal = 1, viewCount = 1 }, default);
            VFXManager.ProcessCameraCommand(secondCamera, commands,
                new VFXCameraXRSettings { viewTotal = 1, viewCount = 1 }, default);

            AssertClose(0.05f, afterPlayerLoop);
            AssertClose(afterPlayerLoop, effect.currentTime);
            Assert.Equal(frame, effect.currentVfxFrameIndex);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
            UnityEngine.Object.DestroyImmediate(firstCamera);
            UnityEngine.Object.DestroyImmediate(secondCamera);
        }
    }

    [Fact]
    public void PlayerLoop_MultipleEffectsShareFrameButKeepTotalsIndependent()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var first = new VisualEffect { playRate = 1f };
        var second = new VisualEffect { playRate = 2f };
        try
        {
            Time.timeScale = 1f;
            UnityRuntime.Tick(0.05f);

            Assert.Equal(first.currentVfxFrameIndex, second.currentVfxFrameIndex);
            AssertClose(0.05f, first.currentTime);
            AssertClose(0.1f, second.currentTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    [Fact]
    public void PlayerLoop_PausedEffectGetsFrameContextWithoutTimeAdvance()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect { pause = true };
        try
        {
            Time.timeScale = 1f;
            UnityRuntime.Tick(0.05f);

            Assert.NotEqual(0u, effect.currentVfxFrameIndex);
            Assert.Equal(0f, effect.currentVfxDeltaTime);
            Assert.Equal(0f, effect.currentTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void PlayerLoop_DisabledEffectIsNotPrepared()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect { enabled = false };
        try
        {
            Time.timeScale = 1f;
            UnityRuntime.Tick(0.05f);

            Assert.Equal(0f, effect.currentTime);
            Assert.False(device.TryGetVFXEffectFrameState(
                unchecked((ulong)(uint)effect.GetInstanceID()), out _));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void ExplicitCameraProcessingOutsidePlayerLoop_RemainsExplicit()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect();
        var camera = new Camera();
        using var commands = new CommandBuffer();
        try
        {
            Time.timeScale = 1f;
            Time.Tick(0.05f);

            VFXManager.ProcessCameraCommand(camera, commands,
                new VFXCameraXRSettings { viewTotal = 1, viewCount = 1 }, default);
            VFXManager.ProcessCameraCommand(camera, commands,
                new VFXCameraXRSettings { viewTotal = 1, viewCount = 1 }, default);

            AssertClose(0.1f, effect.currentTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
            UnityEngine.Object.DestroyImmediate(camera);
        }
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 2)]
    public void OfficialPlayerSequence_MatchesFixedStepConsumption(
        int targetFrame, int expectedSteps)
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        AnityNative.GraphicsVFXFrameState target = default;

        for (int index = 0; index <= targetFrame; index++)
        {
            float gameDelta = index == 0 ? 0.0625f : 0.03125f;
            Assert.True(device.BeginVFXFrame(out uint frame));
            Assert.True(device.PrepareVFXEffectFrame(
                200, frame, gameDelta, 1.75f, FixedStep, MaxDelta,
                false, out target));
            Assert.True(device.CommitVFXEffectFrame(200, frame, out _));
        }

        Assert.Equal(expectedSteps, (int)target.stepCount);
        AssertClose(expectedSteps * FixedStep, target.unscaledDeltaTime);
        AssertClose(expectedSteps * FixedStep * 1.75f, target.deltaTime);
    }

    [Fact]
    public void Prepare_ExposesPreCommitTotal_CommitAdvancesIt()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.BeginVFXFrame(out uint firstFrame));
        Assert.True(device.PrepareVFXEffectFrame(
            300, firstFrame, 0.05f, 1.75f, FixedStep, MaxDelta,
            false, out var first));
        Assert.Equal(0f, first.totalTime);
        Assert.Equal(1u, first.prepared);
        Assert.True(device.CommitVFXEffectFrame(300, firstFrame, out var committed));
        AssertClose(first.deltaTime, committed.totalTime);
        Assert.Equal(0u, committed.prepared);

        Assert.True(device.BeginVFXFrame(out uint secondFrame));
        Assert.True(device.PrepareVFXEffectFrame(
            300, secondFrame, 0.03125f, 1.75f, FixedStep, MaxDelta,
            false, out var second));
        AssertClose(committed.totalTime, second.totalTime);
    }

    [Fact]
    public void EffectAccumulators_AreIndependent()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.BeginVFXFrame(out uint firstFrame));
        Assert.True(device.PrepareVFXEffectFrame(
            401, firstFrame, 0.01f, 1f, FixedStep, MaxDelta, false, out var firstA));
        Assert.True(device.PrepareVFXEffectFrame(
            402, firstFrame, 0.01f, 1f, FixedStep, MaxDelta, false, out var firstB));
        Assert.Equal(0u, firstA.stepCount);
        Assert.Equal(0u, firstB.stepCount);
        Assert.True(device.CommitVFXEffectFrame(401, firstFrame, out _));
        Assert.True(device.CommitVFXEffectFrame(402, firstFrame, out _));

        Assert.True(device.BeginVFXFrame(out uint secondFrame));
        Assert.True(device.PrepareVFXEffectFrame(
            401, secondFrame, 0.01f, 1f, FixedStep, MaxDelta, false, out var secondA));
        Assert.True(device.PrepareVFXEffectFrame(
            402, secondFrame, 0.005f, 1f, FixedStep, MaxDelta, false, out var secondB));
        Assert.Equal(1u, secondA.stepCount);
        Assert.Equal(0u, secondB.stepCount);
    }

    [Fact]
    public void PausedFrame_DoesNotAccumulateOrAdvanceTotal()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectFrame(
            500, frame, 1f, 2f, FixedStep, MaxDelta, true, out var prepared));
        Assert.Equal(0u, prepared.stepCount);
        Assert.Equal(0f, prepared.accumulator);
        Assert.Equal(0f, prepared.deltaTime);
        Assert.True(device.CommitVFXEffectFrame(500, frame, out var committed));
        Assert.Equal(0f, committed.totalTime);
    }

    [Fact]
    public void ZeroPlayRate_ConsumesStepsWithoutAdvancingScaledTime()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectFrame(
            501, frame, 0.05f, 0f, FixedStep, MaxDelta, false, out var prepared));
        Assert.Equal(3u, prepared.stepCount);
        AssertClose(0.05f, prepared.unscaledDeltaTime);
        Assert.Equal(0f, prepared.deltaTime);
        Assert.True(device.CommitVFXEffectFrame(501, frame, out var committed));
        Assert.Equal(0f, committed.totalTime);
    }

    [Fact]
    public void DoublePrepare_IsRejectedUntilCommit()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectFrame(
            600, frame, 0.05f, 1f, FixedStep, MaxDelta, false, out _));
        Assert.False(device.PrepareVFXEffectFrame(
            600, frame, 0.05f, 1f, FixedStep, MaxDelta, false, out _));
        Assert.True(device.CommitVFXEffectFrame(600, frame, out _));
    }

    [Fact]
    public void Commit_RejectsWrongFrameWithoutConsumingPreparedState()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectFrame(
            601, frame, 0.05f, 1f, FixedStep, MaxDelta, false, out _));
        Assert.False(device.CommitVFXEffectFrame(601, unchecked(frame + 1u), out _));
        Assert.True(device.CommitVFXEffectFrame(601, frame, out _));
    }

    [Fact]
    public void Reset_RemovesEffectClockWithoutResettingGlobalFrame()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.BeginVFXFrame(out uint firstFrame));
        Assert.True(device.PrepareVFXEffectFrame(
            700, firstFrame, 0.05f, 1f, FixedStep, MaxDelta, false, out _));
        Assert.True(device.CommitVFXEffectFrame(700, firstFrame, out _));
        Assert.True(device.ResetVFXEffectFrameState(700));
        Assert.False(device.TryGetVFXEffectFrameState(700, out _));

        Assert.True(device.BeginVFXFrame(out uint secondFrame));
        Assert.Equal(unchecked(firstFrame + 1u), secondFrame);
        Assert.True(device.PrepareVFXEffectFrame(
            700, secondFrame, 0f, 1f, FixedStep, MaxDelta, false, out var reset));
        Assert.Equal(0f, reset.totalTime);
        Assert.Equal(0f, reset.accumulator);
    }

    [Fact]
    public void ClearEffect_RemovesFrameClockState()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;

        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectFrame(
            701, frame, 0.05f, 1f, FixedStep, MaxDelta, false, out _));
        Assert.True(device.CommitVFXEffectFrame(701, frame, out _));
        Assert.True(device.ClearVFXEffectState(701));
        Assert.False(device.TryGetVFXEffectFrameState(701, out _));
    }

    [Theory]
    [InlineData(-0.01f, 1f, 0.016666668f, 0.05f)]
    [InlineData(float.NaN, 1f, 0.016666668f, 0.05f)]
    [InlineData(0.01f, -1f, 0.016666668f, 0.05f)]
    [InlineData(0.01f, float.PositiveInfinity, 0.016666668f, 0.05f)]
    [InlineData(0.01f, 1f, 0f, 0.05f)]
    [InlineData(0.01f, 1f, float.NaN, 0.05f)]
    [InlineData(0.01f, 1f, 0.016666668f, 0f)]
    [InlineData(0.01f, 1f, 0.016666668f, float.PositiveInfinity)]
    public void InvalidClockParameters_AreRejected(
        float delta, float playRate, float fixedStep, float maxDelta)
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));

        Assert.Equal(AnityNative.Result.InvalidArg,
            AnityNative.Graphics_PrepareVFXEffectFrame(
                device.Handle, 800, frame, delta, playRate, fixedStep,
                maxDelta, 0, out _));
        Assert.False(device.TryGetVFXEffectFrameState(800, out _));
    }

    [Fact]
    public void MaximumStepRounding_UsesUnityNearestEvenRule()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));

        Assert.True(device.PrepareVFXEffectFrame(
            900, frame, 10f, 1f, 1f, 2.5f, false, out var prepared));
        Assert.Equal(2u, prepared.stepCount);
    }

    private static NativeGraphicsDevice CreateDevice()
    {
        NativeGraphicsDevice device = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Null, 16, 16, false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.NotEqual(IntPtr.Zero, device.Handle);
        return device;
    }

    private static void AssertClose(float expected, float actual)
        => Assert.InRange(actual, expected - 1e-6f, expected + 1e-6f);

    private sealed class VfxSettingsScope : IDisposable
    {
        private readonly float _fixedTimeStep = VFXManager.fixedTimeStep;
        private readonly float _maxDeltaTime = VFXManager.maxDeltaTime;

        internal VfxSettingsScope()
        {
            VFXManager.fixedTimeStep = FixedStep;
            VFXManager.maxDeltaTime = MaxDelta;
        }

        public void Dispose()
        {
            VFXManager.fixedTimeStep = _fixedTimeStep;
            VFXManager.maxDeltaTime = _maxDeltaTime;
        }
    }
}
