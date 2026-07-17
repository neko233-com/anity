using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVFXManualFrameTransactionTests
{
    private const float FixedStep = 1f / 60f;
    private const float MaxDelta = 1f / 20f;

    [Fact]
    public void ManualPrepare_UsesExactStepWithoutPlayRateScaling()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));

        Assert.True(device.PrepareVFXEffectManualFrame(
            1001, frame, 0.125f, out var prepared));

        Assert.Equal(1u, prepared.stepCount);
        AssertClose(0.125f, prepared.gameDeltaTime);
        AssertClose(0.125f, prepared.unscaledDeltaTime);
        AssertClose(0.125f, prepared.deltaTime);
        Assert.True(device.CommitVFXEffectFrame(1001, frame, out var committed));
        AssertClose(0.125f, committed.totalTime);
    }

    [Fact]
    public void Abort_FirstPreparedFrameRemovesUncommittedClock()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1002, frame, 0.25f, out _));

        Assert.True(device.AbortVFXEffectFrame(1002, frame));

        Assert.False(device.TryGetVFXEffectFrameState(1002, out _));
    }

    [Fact]
    public void Abort_RestoresCommittedTotalAndAccumulator()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint firstFrame));
        Assert.True(device.PrepareVFXEffectFrame(
            1003, firstFrame, 0.01f, 1f, FixedStep, MaxDelta, false, out _));
        Assert.True(device.CommitVFXEffectFrame(1003, firstFrame, out var baseline));
        Assert.True(device.BeginVFXFrame(out uint secondFrame));
        Assert.True(device.PrepareVFXEffectManualFrame(1003, secondFrame, 0.5f, out _));

        Assert.True(device.AbortVFXEffectFrame(1003, secondFrame));

        Assert.True(device.TryGetVFXEffectFrameState(1003, out var restored));
        Assert.Equal(0u, restored.prepared);
        AssertClose(baseline.totalTime, restored.totalTime);
        AssertClose(baseline.accumulator, restored.accumulator);
    }

    [Fact]
    public void Abort_WrongFrameDoesNotConsumePreparedTransaction()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1004, frame, 0.1f, out _));

        Assert.False(device.AbortVFXEffectFrame(1004, unchecked(frame + 1u)));
        Assert.True(device.TryGetVFXEffectFrameState(1004, out var prepared));
        Assert.Equal(1u, prepared.prepared);
        Assert.True(device.AbortVFXEffectFrame(1004, frame));
    }

    [Fact]
    public void AbortedTransaction_CannotBeCommitted()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1005, frame, 0.1f, out _));
        Assert.True(device.AbortVFXEffectFrame(1005, frame));

        Assert.False(device.CommitVFXEffectFrame(1005, frame, out _));
    }

    [Fact]
    public void Abort_AllowsAnotherTransactionInSameVfxFrame()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        Assert.True(device.BeginVFXFrame(out uint frame));
        Assert.True(device.PrepareVFXEffectManualFrame(1006, frame, 0.1f, out _));
        Assert.True(device.AbortVFXEffectFrame(1006, frame));

        Assert.True(device.PrepareVFXEffectManualFrame(1006, frame, 0.2f, out _));
        Assert.True(device.CommitVFXEffectFrame(1006, frame, out var committed));
        AssertClose(0.2f, committed.totalTime);
    }

    [Fact]
    public void Simulate_IsDeferredUntilNextVfxUpdate()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect { pause = true };
        try
        {
            effect.Simulate(0.125f, 1);
            Assert.Equal(0f, effect.currentTime);

            UnityRuntime.Tick(0.05f);

            AssertClose(0.125f, effect.currentTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void Simulate_IgnoresComponentPlayRate()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect { pause = true, playRate = 3f };
        try
        {
            effect.Simulate(0.125f, 1);
            UnityRuntime.Tick(0.05f);

            AssertClose(0.125f, effect.currentTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void Simulate_MultipleStepsShareFrameAndAccumulateExactTime()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect { pause = true };
        try
        {
            effect.Simulate(0.125f, 3);
            UnityRuntime.Tick(0.05f);

            AssertClose(0.375f, effect.currentTime);
            Assert.NotEqual(0u, effect.currentVfxFrameIndex);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void Simulate_ZeroStepsIsNoOp()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect { pause = true };
        try
        {
            effect.Simulate(5f, 0);
            UnityRuntime.Tick(0.05f);

            Assert.Equal(0f, effect.currentTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void Simulate_NegativeDeltaMatchesUnityAndRunsBackwards()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect { pause = true };
        try
        {
            effect.Simulate(-0.125f, 1);
            UnityRuntime.Tick(0.05f);

            AssertClose(-0.125f, effect.currentTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void Simulate_NaNMatchesUnityAndPropagatesClockValue()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect { pause = true };
        try
        {
            effect.Simulate(float.NaN, 1);
            UnityRuntime.Tick(0.05f);

            Assert.True(float.IsNaN(effect.currentTime));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void AdvanceOneFrame_OnlyQueuesWhilePaused()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect { pause = true, playRate = 1.5f };
        try
        {
            effect.AdvanceOneFrame();
            Assert.Equal(0f, effect.currentTime);
            UnityRuntime.Tick(0.05f);

            AssertClose(0.075f, effect.currentTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void AdvanceOneFrame_IsNoOpWhileUnpaused()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect { pause = false, playRate = 1.5f };
        try
        {
            effect.AdvanceOneFrame();
            UnityRuntime.Tick(0.05f);

            AssertClose(0.075f, effect.currentTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Fact]
    public void Reinit_CancelsQueuedManualUpdatesAndResetsImmediately()
    {
        using NativeGraphicsDevice device = CreateDevice();
        if (device.Handle == IntPtr.Zero) return;
        using var settings = new VfxSettingsScope();
        var effect = new VisualEffect { pause = true };
        try
        {
            effect.Simulate(0.25f, 2);
            effect.Reinit();
            Assert.Equal(0f, effect.currentTime);
            UnityRuntime.Tick(0.05f);

            Assert.Equal(0f, effect.currentTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effect);
        }
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
