using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>URP renderer queue lifecycle, ordering, and reuse regression coverage.</summary>
public sealed class UrpRendererLifecycleTests
{
    [Fact]
    public void Setup_ReplacesPassesLeftByPreviousCamera()
    {
        var renderer = new TestRenderer();
        renderer.EnqueuePass(new TrackingPass("stale", RenderPassEvent.AfterRendering));
        var data = CreateRenderingData();

        renderer.Setup(default, ref data);

        Assert.Single(renderer.activeRenderPassQueue);
        Assert.IsType<PostProcessPass>(renderer.activeRenderPassQueue[0]);
    }

    [Fact]
    public void Execute_SortsByRenderPassEvent_AndPreservesTieOrder()
    {
        var renderer = new TestRenderer();
        var trace = new List<string>();
        renderer.EnqueuePass(new TrackingPass("late", RenderPassEvent.AfterRendering, trace));
        renderer.EnqueuePass(new TrackingPass("first", RenderPassEvent.BeforeRendering, trace));
        renderer.EnqueuePass(new TrackingPass("same-event", RenderPassEvent.BeforeRendering, trace));
        renderer.EnqueuePass(new TrackingPass("middle", RenderPassEvent.AfterRenderingOpaques, trace));
        var data = CreateRenderingData();

        renderer.Execute(default, ref data);

        Assert.Equal(new[] { "execute:first", "execute:same-event", "execute:middle", "execute:late" }, trace.FindAll(entry => entry.StartsWith("execute:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Execute_InvokesConfigureSetupExecuteAndReverseCleanup()
    {
        var renderer = new TestRenderer();
        var trace = new List<string>();
        renderer.EnqueuePass(new TrackingPass("a", RenderPassEvent.BeforeRendering, trace));
        renderer.EnqueuePass(new TrackingPass("b", RenderPassEvent.AfterRendering, trace));
        var data = CreateRenderingData();

        renderer.Execute(default, ref data);

        Assert.Equal(new[]
        {
            "configure:a", "setup:a", "execute:a", "configure:b", "setup:b", "execute:b", "cleanup:b", "cleanup:a"
        }, trace);
    }

    [Fact]
    public void Execute_AlwaysClearsQueueAfterSuccess()
    {
        var renderer = new TestRenderer();
        renderer.EnqueuePass(new TrackingPass("pass", RenderPassEvent.BeforeRendering));
        var data = CreateRenderingData();

        renderer.Execute(default, ref data);

        Assert.Empty(renderer.activeRenderPassQueue);
    }

    [Fact]
    public void Execute_AlwaysCleansAndClearsQueueWhenPassThrows()
    {
        var renderer = new TestRenderer();
        var trace = new List<string>();
        renderer.EnqueuePass(new TrackingPass("first", RenderPassEvent.BeforeRendering, trace));
        renderer.EnqueuePass(new TrackingPass("broken", RenderPassEvent.AfterRendering, trace, throws: true));
        var data = CreateRenderingData();

        Assert.Throws<InvalidOperationException>(() => renderer.Execute(default, ref data));

        Assert.Equal(new[] { "cleanup:broken", "cleanup:first" }, trace.FindAll(entry => entry.StartsWith("cleanup:", StringComparison.Ordinal)));
        Assert.Empty(renderer.activeRenderPassQueue);
    }

    [Fact]
    public void Execute_CleansCurrentPassWhenCameraSetupThrows()
    {
        var renderer = new TestRenderer();
        var trace = new List<string>();
        renderer.EnqueuePass(new TrackingPass("setup-failure", RenderPassEvent.BeforeRendering, trace, throwsOnSetup: true));
        var data = CreateRenderingData();

        Assert.Throws<InvalidOperationException>(() => renderer.Execute(default, ref data));

        Assert.Equal(new[] { "configure:setup-failure", "setup:setup-failure", "cleanup:setup-failure" }, trace);
        Assert.Empty(renderer.activeRenderPassQueue);
    }

    [Fact]
    public void Setup_CallsFeatureAddThenSetupRenderPasses()
    {
        var renderer = new TestRenderer();
        var feature = new TrackingFeature();
        renderer.rendererFeatures.Add(feature);
        var data = CreateRenderingData();

        renderer.Setup(default, ref data);

        Assert.Equal(new[] { "add", "setup-render-passes" }, feature.Trace);
    }

    [Fact]
    public void Execute_CallsFeatureCameraHooksAroundPasses()
    {
        var renderer = new TestRenderer();
        var trace = new List<string>();
        var feature = new TrackingFeature(trace);
        renderer.rendererFeatures.Add(feature);
        renderer.EnqueuePass(new TrackingPass("pass", RenderPassEvent.BeforeRendering, trace));
        var data = CreateRenderingData();

        renderer.Execute(default, ref data);

        Assert.Equal(new[] { "feature-camera-setup", "configure:pass", "setup:pass", "execute:pass", "cleanup:pass", "feature-camera-cleanup" }, trace);
    }

    [Fact]
    public void UniversalRenderer_RequeuesOpaqueTransparentAndPostProcessForEveryCamera()
    {
        var renderer = new UniversalRenderer(new UniversalRendererData());
        var data = CreateRenderingData();

        renderer.Setup(default, ref data);
        AssertPassEvents(renderer.activeRenderPassQueue, RenderPassEvent.BeforeRenderingOpaques, RenderPassEvent.BeforeRenderingTransparents, RenderPassEvent.BeforeRenderingPostProcessing);
        renderer.Execute(default, ref data);

        renderer.Setup(default, ref data);
        AssertPassEvents(renderer.activeRenderPassQueue, RenderPassEvent.BeforeRenderingOpaques, RenderPassEvent.BeforeRenderingTransparents, RenderPassEvent.BeforeRenderingPostProcessing);
    }

    [Fact]
    public void UniversalRenderer_SetupIsIdempotentBeforeExecution()
    {
        var renderer = new UniversalRenderer(new UniversalRendererData());
        var data = CreateRenderingData();

        renderer.Setup(default, ref data);
        renderer.Setup(default, ref data);

        AssertPassEvents(renderer.activeRenderPassQueue, RenderPassEvent.BeforeRenderingOpaques, RenderPassEvent.BeforeRenderingTransparents, RenderPassEvent.BeforeRenderingPostProcessing);
    }

    [Fact]
    public void Renderer2D_RequeuesSpritePassForEveryCamera()
    {
        var renderer = new Renderer2D(new Renderer2DData());
        var data = CreateRenderingData();

        renderer.Setup(default, ref data);
        AssertPassEvents(renderer.activeRenderPassQueue, RenderPassEvent.BeforeRenderingOpaques, RenderPassEvent.BeforeRenderingPostProcessing);
        renderer.Execute(default, ref data);

        renderer.Setup(default, ref data);
        AssertPassEvents(renderer.activeRenderPassQueue, RenderPassEvent.BeforeRenderingOpaques, RenderPassEvent.BeforeRenderingPostProcessing);
    }

    [Fact]
    public void ScriptableRenderPassInput_DefaultsToNone()
    {
        Assert.Equal(ScriptableRenderPassInput.None, new InputPass(ScriptableRenderPassInput.None).input);
    }

    [Fact]
    public void Setup_DepthInputRequestsDepthTexture()
    {
        var data = SetupWithInput(ScriptableRenderPassInput.Depth);
        Assert.True(data.cameraData.requiresDepthTexture);
        Assert.False(data.cameraData.requiresOpaqueTexture);
    }

    [Fact]
    public void Setup_ColorInputRequestsOpaqueTexture()
    {
        var data = SetupWithInput(ScriptableRenderPassInput.Color);
        Assert.True(data.cameraData.requiresOpaqueTexture);
        Assert.False(data.cameraData.requiresDepthTexture);
    }

    [Fact]
    public void Setup_NormalInputRequestsNormalsTexture()
    {
        var data = SetupWithInput(ScriptableRenderPassInput.Normal);
        Assert.True(data.cameraData.requiresNormalsTexture);
    }

    [Fact]
    public void Setup_MotionInputRequestsMotionVectors()
    {
        var data = SetupWithInput(ScriptableRenderPassInput.Motion);
        Assert.True(data.cameraData.requiresMotionVectors);
    }

    [Fact]
    public void Setup_CombinedInputsRequestEveryCorrespondingResource()
    {
        var data = SetupWithInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Motion);
        Assert.True(data.cameraData.requiresDepthTexture);
        Assert.True(data.cameraData.requiresNormalsTexture);
        Assert.True(data.cameraData.requiresOpaqueTexture);
        Assert.True(data.cameraData.requiresMotionVectors);
    }

    [Fact]
    public void ConfigureInput_ReplacesThePreviousPassDeclaration()
    {
        var pass = new InputPass(ScriptableRenderPassInput.Color);
        pass.SetInput(ScriptableRenderPassInput.Depth);
        Assert.Equal(ScriptableRenderPassInput.Depth, pass.input);
    }

    [Fact]
    public void Setup_PreservesCameraDeclaredInputRequirements()
    {
        var renderer = new TestRenderer();
        var data = CreateRenderingData();
        data.cameraData.requiresDepthTexture = true;

        renderer.Setup(default, ref data);

        Assert.True(data.cameraData.requiresDepthTexture);
    }

    [Fact]
    public void Setup_AggregatesInputConfiguredByFeatureSetupCallback()
    {
        var renderer = new TestRenderer();
        renderer.rendererFeatures.Add(new InputFeature(ScriptableRenderPassInput.Color));
        var data = CreateRenderingData();

        renderer.Setup(default, ref data);

        Assert.True(data.cameraData.requiresOpaqueTexture);
    }

    [Fact]
    public void UniversalRendererData_DepthDefaultRequestsDepthTexture()
    {
        var renderer = new UniversalRenderer(new UniversalRendererData { m_SupportsCameraDepthTexture = true });
        var data = CreateRenderingData();

        renderer.Setup(default, ref data);

        Assert.True(data.cameraData.requiresDepthTexture);
    }

    [Fact]
    public void UniversalRendererData_OpaqueDefaultRequestsOpaqueTexture()
    {
        var renderer = new UniversalRenderer(new UniversalRendererData { m_SupportsCameraOpaqueTexture = true });
        var data = CreateRenderingData();

        renderer.Setup(default, ref data);

        Assert.True(data.cameraData.requiresOpaqueTexture);
    }

    private static RenderingData CreateRenderingData()
    {
        return new RenderingData
        {
            cameraData = new CameraData
            {
                camera = new Camera(),
                cameraTargetDescriptor = new RenderTextureDescriptor(64, 64)
            }
        };
    }

    private static RenderingData SetupWithInput(ScriptableRenderPassInput input)
    {
        var renderer = new TestRenderer();
        var data = CreateRenderingData();
        renderer.rendererFeatures.Add(new InputFeature(input));
        renderer.Setup(default, ref data);
        return data;
    }

    private static void AssertPassEvents(IReadOnlyList<ScriptableRenderPass> queue, params RenderPassEvent[] expected)
    {
        Assert.Equal(expected.Length, queue.Count);
        var actual = new List<RenderPassEvent>();
        foreach (var pass in queue)
            actual.Add(pass.renderPassEvent);
        actual.Sort();
        Array.Sort(expected);
        Assert.Equal(expected, actual);
    }

    private sealed class TestRenderer : ScriptableRenderer { }

    private sealed class TrackingPass : ScriptableRenderPass
    {
        private readonly string _name;
        private readonly List<string>? _trace;
        private readonly bool _throws;
        private readonly bool _throwsOnSetup;

        public TrackingPass(string name, RenderPassEvent renderPassEvent, List<string>? trace = null, bool throws = false, bool throwsOnSetup = false)
        {
            _name = name;
            _trace = trace;
            _throws = throws;
            _throwsOnSetup = throwsOnSetup;
            this.renderPassEvent = renderPassEvent;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) => _trace?.Add($"configure:{_name}");
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            _trace?.Add($"setup:{_name}");
            if (_throwsOnSetup) throw new InvalidOperationException(_name);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            _trace?.Add($"execute:{_name}");
            if (_throws) throw new InvalidOperationException(_name);
        }
        public override void OnCameraCleanup(CommandBuffer cmd) => _trace?.Add($"cleanup:{_name}");
    }

    private sealed class InputPass : ScriptableRenderPass
    {
        public InputPass(ScriptableRenderPassInput input) => ConfigureInput(input);
        public void SetInput(ScriptableRenderPassInput input) => ConfigureInput(input);
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }
    }

    private sealed class InputFeature : ScriptableRendererFeature
    {
        private readonly ScriptableRenderPassInput _input;
        private InputPass? _pass;

        public InputFeature(ScriptableRenderPassInput input) => _input = input;
        public override void Create() => _pass = new InputPass(_input);
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            _pass ??= new InputPass(_input);
            renderer.EnqueuePass(_pass);
        }
    }

    private sealed class TrackingFeature : ScriptableRendererFeature
    {
        public List<string> Trace { get; }

        public TrackingFeature(List<string>? trace = null) => Trace = trace ?? new List<string>();

        public override void Create() { }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) => Trace.Add("add");
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData) => Trace.Add("setup-render-passes");
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) => Trace.Add("feature-camera-setup");
        public override void OnCameraCleanup(CommandBuffer cmd) => Trace.Add("feature-camera-cleanup");
    }
}
