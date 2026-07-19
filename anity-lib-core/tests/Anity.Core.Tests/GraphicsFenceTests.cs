using UnityEngine;
using UnityEngine.Rendering;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>GraphicsFence/GPUFence command-buffer submission semantics and public Unity 2022 surface.</summary>
[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class GraphicsFenceTests
{
    [Fact]
    public void GraphicsFence_IsPendingUntilItsCommandBufferIsSubmitted()
    {
        var commandBuffer = new CommandBuffer();
        GraphicsFence fence = commandBuffer.CreateAsyncGraphicsFence();
        Assert.False(fence.passed);
        UnityRuntime.Tick(0.001f);
        Assert.False(fence.passed);
    }

    [Fact]
    public void GraphicsExecuteCommandBuffer_CompletesFenceOnNextFrame()
    {
        var commandBuffer = new CommandBuffer();
        GraphicsFence fence = commandBuffer.CreateAsyncGraphicsFence();
        Graphics.ExecuteCommandBuffer(commandBuffer);
        Assert.False(fence.passed);
        UnityRuntime.Tick(0.001f);
        Assert.True(fence.passed);
    }

    [Fact]
    public void AsyncCommandBuffer_CompletesGraphicsFenceOnNextFrame()
    {
        var commandBuffer = new CommandBuffer();
        GraphicsFence fence = commandBuffer.CreateAsyncGraphicsFence(SynchronisationStage.VertexProcessing);
        Graphics.ExecuteCommandBufferAsync(commandBuffer, ComputeQueueType.Background);
        UnityRuntime.Tick(0.001f);
        Assert.True(fence.passed);
    }

    [Fact]
    public void GpuFence_IsPendingThenCompletesOnNextFrame()
    {
        var commandBuffer = new CommandBuffer();
        GPUFence fence = commandBuffer.CreateGPUFence(SynchronisationStage.PixelProcessing);
        Graphics.ExecuteCommandBuffer(commandBuffer);
        Assert.False(fence.passed);
        UnityRuntime.Tick(0.001f);
        Assert.True(fence.passed);
    }

    [Fact]
    public void CpuSynchronisationFence_UsesSameSubmissionLifecycle()
    {
        var commandBuffer = new CommandBuffer();
        GraphicsFence fence = commandBuffer.CreateGraphicsFence(GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.AllGPUOperations);
        Graphics.ExecuteCommandBuffer(commandBuffer);
        UnityRuntime.Tick(0.001f);
        Assert.True(fence.passed);
    }

    [Fact]
    public void GraphicsFenceDependency_RequiresAValidSourceFence()
    {
        var sourceCommands = new CommandBuffer();
        GraphicsFence source = sourceCommands.CreateAsyncGraphicsFence();
        var dependentCommands = new CommandBuffer();
        GraphicsFence dependent = dependentCommands.CreateAsyncGraphicsFence();
        dependentCommands.WaitOnAsyncGraphicsFence(source);
        Graphics.ExecuteCommandBuffer(sourceCommands);
        Graphics.ExecuteCommandBuffer(dependentCommands);
        UnityRuntime.Tick(0.001f);
        Assert.True(source.passed);
        Assert.True(dependent.passed);
    }

    [Fact]
    public void GpuFenceDependency_RequiresAValidSourceFence()
    {
        var sourceCommands = new CommandBuffer();
        GPUFence source = sourceCommands.CreateGPUFence();
        var dependentCommands = new CommandBuffer();
        GPUFence dependent = dependentCommands.CreateGPUFence();
        dependentCommands.WaitOnGPUFence(source, SynchronisationStage.VertexProcessing);
        Graphics.ExecuteCommandBuffer(sourceCommands);
        Graphics.ExecuteCommandBuffer(dependentCommands);
        UnityRuntime.Tick(0.001f);
        Assert.True(source.passed);
        Assert.True(dependent.passed);
    }

    [Fact]
    public void DefaultFenceCannotBeUsedAsDependency()
    {
        var commandBuffer = new CommandBuffer();
        commandBuffer.CreateAsyncGraphicsFence();
        Assert.Throws<ArgumentException>(() => commandBuffer.WaitOnAsyncGraphicsFence(default));
        Assert.Throws<ArgumentException>(() => commandBuffer.WaitOnGPUFence(default));
    }

    [Fact]
    public void InvalidStageFlagsAreRejected()
    {
        var commandBuffer = new CommandBuffer();
        Assert.Throws<ArgumentOutOfRangeException>(() => commandBuffer.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => commandBuffer.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, (SynchronisationStageFlags)8));
    }

    [Fact]
    public void ScriptableRenderContextSubmit_CompletesEnqueuedFence()
    {
        var commandBuffer = new CommandBuffer();
        GraphicsFence fence = commandBuffer.CreateAsyncGraphicsFence();
        var context = new ScriptableRenderContext();
        context.ExecuteCommandBuffer(commandBuffer);
        context.Submit();
        UnityRuntime.Tick(0.001f);
        Assert.True(fence.passed);
    }

    [Fact]
    public void PublicEnumValuesMatchUnity2022()
    {
        Assert.Equal(0, (int)GraphicsFenceType.AsyncQueueSynchronisation);
        Assert.Equal(1, (int)GraphicsFenceType.CPUSynchronisation);
        Assert.Equal(0, (int)SynchronisationStage.VertexProcessing);
        Assert.Equal(1, (int)SynchronisationStage.PixelProcessing);
        Assert.Equal(7, (int)SynchronisationStageFlags.AllGPUOperations);
    }
}
