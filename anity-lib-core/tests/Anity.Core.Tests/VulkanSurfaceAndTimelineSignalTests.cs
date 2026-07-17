using System;
using Anity.Core.Runtime.Native;
using Anity.Core.Runtime.Platform;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Vulkan multi-platform surface mapping + Timeline Signal + TimelineWindow — ≥20 cases.</summary>
[Collection("ScreenState")]
public class VulkanSurfaceAndTimelineSignalTests : IDisposable
{
    public void Dispose()
    {
        PlatformGraphics.ClearForce();
    }

    // --- Vulkan surface platform mapping ---

    [Fact]
    public void VulkanSurface_Android_IsANativeWindow()
    {
        Assert.Equal(PlatformGraphics.VulkanSurfaceKind.Android,
            PlatformGraphics.GetVulkanSurfaceKind(TargetPlatform.Android));
        Assert.Equal("ANativeWindow*", PlatformGraphics.DescribeNativeWindowType(TargetPlatform.Android));
        Assert.True(PlatformGraphics.PlatformUsesVulkanNativeSurface(TargetPlatform.Android));
    }

    [Fact]
    public void VulkanSurface_Linux_IsX11()
    {
        Assert.Equal(PlatformGraphics.VulkanSurfaceKind.X11,
            PlatformGraphics.GetVulkanSurfaceKind(TargetPlatform.Linux));
        Assert.Contains("X11", PlatformGraphics.DescribeNativeWindowType(TargetPlatform.Linux));
    }

    [Fact]
    public void VulkanSurface_Windows_IsWin32()
    {
        Assert.Equal(PlatformGraphics.VulkanSurfaceKind.Win32,
            PlatformGraphics.GetVulkanSurfaceKind(TargetPlatform.Windows));
        Assert.Equal("HWND", PlatformGraphics.DescribeNativeWindowType(TargetPlatform.Windows));
    }

    [Fact]
    public void VulkanSurface_iOS_None_MetalLayer()
    {
        Assert.Equal(PlatformGraphics.VulkanSurfaceKind.None,
            PlatformGraphics.GetVulkanSurfaceKind(TargetPlatform.iOS));
        Assert.Contains("CAMetalLayer", PlatformGraphics.DescribeNativeWindowType(TargetPlatform.iOS));
    }

    [Fact]
    public void VulkanSupportedSurfaceMask_NonNegative()
    {
        int mask = NativeGraphicsDevice.VulkanSupportedSurfaceMask;
        Assert.True(mask >= 0);
        // On Windows host expect Win32 bit when native built for Win32
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
        {
            Assert.True((mask & 1) != 0 || mask == NativeGraphicsDevice.ExpectedSurfaceMaskForHost());
        }
    }

    [Fact]
    public void HeadlessSwapchain_SurfaceKindZero()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 64, 64, false);
        Assert.True(dev.CreateSwapchain(64, 64, nativeWindow: IntPtr.Zero));
        Assert.Equal(0, dev.SwapchainSurfaceKind);
        Assert.True(dev.SwapchainHeadless);
        Assert.False(dev.SwapchainHasNativeSurface);
    }

    [Fact]
    public void InvalidHwnd_FallsBackHeadless()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 100, 100, false);
        // Non-window pointer should not crash; surface fails → headless software ring
        Assert.True(dev.CreateSwapchain(100, 100, nativeWindow: new IntPtr(0x1)));
        Assert.True(dev.HasSwapchain);
        // On Windows IsWindow(1) fails → headless; surface kind stays 0
        Assert.Equal(0, dev.SwapchainSurfaceKind);
    }

    [Fact]
    public void AndroidDefault_IsVulkan()
    {
        Assert.Equal(GraphicsDeviceType.Vulkan, PlatformGraphics.GetDefaultDeviceType(TargetPlatform.Android));
        Assert.Contains(GraphicsDeviceType.Vulkan, PlatformGraphics.GetPreferredApis(TargetPlatform.Android));
    }

    [Fact]
    public void LinuxDefault_IsVulkan()
    {
        Assert.Equal(GraphicsDeviceType.Vulkan, PlatformGraphics.GetDefaultDeviceType(TargetPlatform.Linux));
    }

    // --- Timeline Signals ---

    [Fact]
    public void SignalAsset_Name()
    {
        var s = ScriptableObject.CreateInstance<SignalAsset>();
        s.signalName = "Hit";
        Assert.Equal("Hit", s.signalName);
    }

    [Fact]
    public void SignalTrack_CreateMarker()
    {
        var asset = ScriptableObject.CreateInstance<TimelineAsset>();
        var st = asset.CreateTrack<SignalTrack>("S");
        var sig = ScriptableObject.CreateInstance<SignalAsset>();
        sig.signalName = "A";
        var m = st.CreateMarker(1.5, sig);
        Assert.Equal(1.5, m.time);
        Assert.Equal(1, st.emitterCount);
    }

    [Fact]
    public void SignalReceiver_OnNotify()
    {
        var go = new GameObject("rx");
        var rx = go.AddComponent<SignalReceiver>();
        var sig = ScriptableObject.CreateInstance<SignalAsset>();
        int hits = 0;
        rx.AddReaction(sig, () => hits++);
        rx.OnNotify(sig);
        Assert.Equal(1, hits);
        Assert.Equal(1, rx.receiveCount);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void Director_EmitsSignal_OnEvaluate()
    {
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.SetDuration(5);
        var st = timeline.CreateTrack<SignalTrack>("Signals");
        var sig = ScriptableObject.CreateInstance<SignalAsset>();
        sig.signalName = "Fire";
        st.CreateMarker(0.5, sig);

        var go = new GameObject("dir");
        var rx = go.AddComponent<SignalReceiver>();
        int hits = 0;
        rx.signalReceived += _ => hits++;
        var dir = go.AddComponent<PlayableDirector>();
        dir.playOnAwake = false;
        dir.playableAsset = timeline;
        dir.signalReceiver = rx;
        dir.extrapolationMode = DirectorWrapMode.Hold;
        dir.timeUpdateMode = DirectorUpdateMode.Manual;
        dir.Play();
        dir.Evaluate(0.4f);
        Assert.Equal(0, hits);
        dir.Evaluate(0.2f); // crosses 0.5
        Assert.Equal(1, hits);
        Assert.True(dir.signalsEmitted >= 1);
        // emitOnce: no second fire
        dir.Evaluate(0.5f);
        Assert.Equal(1, hits);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void SignalUtility_Loop_Resets()
    {
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.SetDuration(1);
        var st = timeline.CreateTrack<SignalTrack>();
        var sig = ScriptableObject.CreateInstance<SignalAsset>();
        st.CreateMarker(0.25, sig);

        var go = new GameObject("rx2");
        var rx = go.AddComponent<SignalReceiver>();
        int hits = 0;
        rx.signalReceived += _ => hits++;

        Assert.Equal(1, SignalUtility.EvaluateTimelineSignals(timeline, 0, 0.5, rx));
        Assert.Equal(1, hits);
        // loop wrap: previous 0.9 → current 0.3 crosses marker 0.25 after reset
        Assert.True(SignalUtility.EvaluateTimelineSignals(timeline, 0.9, 0.3, rx, looping: true) >= 1);
        Assert.True(hits >= 2);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void MutedSignalTrack_DoesNotEmit()
    {
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        var st = timeline.CreateTrack<SignalTrack>();
        st.muted = true;
        var sig = ScriptableObject.CreateInstance<SignalAsset>();
        st.CreateMarker(0.1, sig);
        Assert.Equal(0, SignalUtility.EvaluateTimelineSignals(timeline, 0, 1, null));
    }

    // --- TimelineWindow ---

    [Fact]
    public void TimelineWindow_Open_AndPlayhead()
    {
        var asset = ScriptableObject.CreateInstance<TimelineAsset>();
        asset.SetDuration(10);
        asset.CreateTrack<AnimationTrack>("Anim");
        var w = TimelineWindow.Open(asset);
        Assert.NotNull(w.timeline);
        Assert.Equal(1, w.GetTrackCount());
        w.SetPlayhead(2.5);
        Assert.Equal(2.5, w.playhead);
        w.Close();
    }

    [Fact]
    public void TimelineWindow_AddSignalAndTrack()
    {
        var asset = ScriptableObject.CreateInstance<TimelineAsset>();
        var w = TimelineWindow.Open(asset);
        var anim = w.AddTrack<AnimationTrack>("A");
        Assert.NotNull(anim);
        var clip = w.AddDefaultClip(anim, 0, 3);
        Assert.Equal(3, clip.duration);
        var sig = ScriptableObject.CreateInstance<SignalAsset>();
        sig.signalName = "Boom";
        w.AddSignal(1.0, sig);
        Assert.True(w.GetTrackCount() >= 2);
        w.Close();
    }

    [Fact]
    public void TimelineWindow_PlayPauseStop_Tick()
    {
        var asset = ScriptableObject.CreateInstance<TimelineAsset>();
        asset.SetDuration(2);
        var go = new GameObject("tw");
        var dir = go.AddComponent<PlayableDirector>();
        dir.playOnAwake = false;
        dir.playableAsset = asset;
        var w = TimelineWindow.Open(asset, dir);
        w.Play();
        Assert.True(w.isPlaying);
        w.Tick(0.5f);
        Assert.True(w.playhead >= 0.4);
        w.Pause();
        Assert.False(w.isPlaying);
        w.Stop();
        Assert.Equal(0, w.playhead);
        w.Close();
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void TimelineWindow_GetClipsAtPlayhead()
    {
        var asset = ScriptableObject.CreateInstance<TimelineAsset>();
        var track = asset.CreateTrack<ActivationTrack>();
        var c = track.CreateDefaultClip();
        c.start = 1;
        c.duration = 2;
        var w = TimelineWindow.Open(asset);
        w.SetPlayhead(1.5);
        Assert.Single(w.GetClipsAtPlayhead());
        w.SetPlayhead(0);
        Assert.Empty(w.GetClipsAtPlayhead());
        w.Close();
    }

    [Fact]
    public void TimelineWindow_ShowWindow_Menu()
    {
        var w = TimelineWindow.ShowWindow();
        Assert.NotNull(w);
        Assert.Equal("Timeline", w.titleContent.text);
        w.Close();
    }

    [Fact]
    public void SignalReceiver_Clear()
    {
        var go = new GameObject("c");
        var rx = go.AddComponent<SignalReceiver>();
        var sig = ScriptableObject.CreateInstance<SignalAsset>();
        rx.AddReaction(sig, () => { });
        Assert.Equal(1, rx.reactionCount);
        rx.Clear();
        Assert.Equal(0, rx.reactionCount);
        UnityEngine.Object.DestroyImmediate(go);
    }
}
