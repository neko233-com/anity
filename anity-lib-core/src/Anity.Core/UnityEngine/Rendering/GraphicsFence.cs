using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering;

public enum GraphicsFenceType
{
    AsyncQueueSynchronisation = 0,
    CPUSynchronisation = 1,
}

public enum SynchronisationStage
{
    VertexProcessing = 0,
    PixelProcessing = 1,
}

[Flags]
public enum SynchronisationStageFlags
{
    VertexProcessing = 1,
    PixelProcessing = 2,
    ComputeProcessing = 4,
    AllGPUOperations = VertexProcessing | PixelProcessing | ComputeProcessing,
}

public struct GraphicsFence
{
    private readonly GraphicsFenceState? _state;
    internal GraphicsFence(GraphicsFenceState state) => _state = state;
    public bool passed => _state?.Passed ?? false;
    internal GraphicsFenceState? State => _state;
}

public struct GPUFence
{
    private readonly GPUFenceState? _state;
    internal GPUFence(GPUFenceState state) => _state = state;
    public bool passed => _state?.Passed ?? false;
    internal GPUFenceState? State => _state;
}

internal sealed class GraphicsFenceState
{
    internal GraphicsFenceState(GraphicsFenceType type, SynchronisationStageFlags stage) { Type = type; Stage = stage; }
    internal GraphicsFenceType Type { get; }
    internal SynchronisationStageFlags Stage { get; }
    internal bool Passed { get; private set; }
    internal int ReadyFrame { get; private set; } = int.MaxValue;
    internal GraphicsFenceState? Dependency { get; set; }
    internal void Schedule(int readyFrame) => ReadyFrame = Math.Min(ReadyFrame, readyFrame);
    internal void Process(int frame) { if (!Passed && frame >= ReadyFrame && (Dependency == null || Dependency.Passed)) Passed = true; }
}

internal sealed class GPUFenceState
{
    internal GPUFenceState(SynchronisationStageFlags stage) => Stage = stage;
    internal SynchronisationStageFlags Stage { get; }
    internal bool Passed { get; private set; }
    internal int ReadyFrame { get; private set; } = int.MaxValue;
    internal GPUFenceState? Dependency { get; set; }
    internal void Schedule(int readyFrame) => ReadyFrame = Math.Min(ReadyFrame, readyFrame);
    internal void Process(int frame) { if (!Passed && frame >= ReadyFrame && (Dependency == null || Dependency.Passed)) Passed = true; }
}

/// <summary>Frame-accurate fence retirement for the managed command-buffer scheduler.</summary>
internal static class GraphicsFenceScheduler
{
    private static readonly object Gate = new();
    private static readonly List<GraphicsFenceState> GraphicsPending = new();
    private static readonly List<GPUFenceState> GpuPending = new();

    internal static void AddDependency(List<GraphicsFenceState> destinations, GraphicsFence source)
    {
        if (source.State == null) throw new ArgumentException("The graphics fence is not valid.", nameof(source));
        foreach (GraphicsFenceState destination in destinations) destination.Dependency = source.State;
    }

    internal static void AddDependency(List<GPUFenceState> destinations, GPUFence source)
    {
        if (source.State == null) throw new ArgumentException("The GPU fence is not valid.", nameof(source));
        foreach (GPUFenceState destination in destinations) destination.Dependency = source.State;
    }

    internal static void Schedule(IReadOnlyList<GraphicsFenceState> graphicsFences, IReadOnlyList<GPUFenceState> gpuFences)
    {
        lock (Gate)
        {
            int readyFrame = checked(Time.frameCount + 1);
            foreach (GraphicsFenceState fence in graphicsFences)
            {
                fence.Schedule(readyFrame);
                if (!GraphicsPending.Contains(fence)) GraphicsPending.Add(fence);
            }
            foreach (GPUFenceState fence in gpuFences)
            {
                fence.Schedule(readyFrame);
                if (!GpuPending.Contains(fence)) GpuPending.Add(fence);
            }
        }
    }

    internal static void ProcessFrame()
    {
        lock (Gate)
        {
            int frame = Time.frameCount;
            foreach (GraphicsFenceState fence in GraphicsPending) fence.Process(frame);
            foreach (GPUFenceState fence in GpuPending) fence.Process(frame);
            GraphicsPending.RemoveAll(fence => fence.Passed);
            GpuPending.RemoveAll(fence => fence.Passed);
        }
    }
}
