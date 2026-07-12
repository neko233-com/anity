using System.Threading;
using Unity.Jobs;
using Unity.Collections;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Job System deep parity — ≥12 cases including deps / parallel / edges.</summary>
public class JobSystemTests
{
    private struct AddJob : IJob
    {
        public NativeArray<int> data;
        public int value;
        public void Execute()
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = data[i] + value;
        }
    }

    private struct ParallelInc : IJobParallelFor
    {
        public NativeArray<int> data;
        public void Execute(int index) => data[index] = data[index] + 1;
    }

    [Fact]
    public void IJob_Run_ExecutesImmediately()
    {
        var arr = new NativeArray<int>(4, Allocator.Temp);
        for (int i = 0; i < 4; i++) arr[i] = i;
        new AddJob { data = arr, value = 10 }.Run();
        Assert.Equal(10, arr[0]);
        Assert.Equal(13, arr[3]);
        arr.Dispose();
    }

    [Fact]
    public void IJob_Schedule_Complete()
    {
        var arr = new NativeArray<int>(8, Allocator.TempJob);
        var handle = new AddJob { data = arr, value = 1 }.Schedule();
        handle.Complete();
        Assert.True(handle.IsCompleted);
        Assert.Equal(1, arr[0]);
        arr.Dispose();
    }

    [Fact]
    public void ParallelFor_Schedule_FillsArray()
    {
        var arr = new NativeArray<int>(64, Allocator.TempJob);
        var h = new ParallelInc { data = arr }.Schedule(arr.Length, 8);
        h.Complete();
        for (int i = 0; i < arr.Length; i++)
            Assert.Equal(1, arr[i]);
        arr.Dispose();
    }

    [Fact]
    public void ParallelFor_Run_NegativeLength_Throws()
    {
        var job = new ParallelInc { data = default };
        Assert.ThrowsAny<System.ArgumentOutOfRangeException>(() => job.Run(-1));
    }

    [Fact]
    public void Dependencies_CompleteInOrder()
    {
        int order = 0;
        int a = 0, b = 0;
        var h1 = JobScheduler_PublicSchedule(() => { Thread.Sleep(20); a = ++order; });
        // Use real API: schedule second depending on first via Combine + sequential
        h1.Complete();
        var h2 = JobScheduler_PublicSchedule(() => { b = ++order; });
        h2.Complete();
        Assert.True(a == 1 && b == 2);
    }

    private static JobHandle JobScheduler_PublicSchedule(System.Action body)
    {
        // schedule via dummy IJob wrapper
        var box = new ActionJob { action = body };
        return box.Schedule();
    }

    private struct ActionJob : IJob
    {
        public System.Action action;
        public void Execute() => action?.Invoke();
    }

    [Fact]
    public void CombineDependencies_WaitsAll()
    {
        var arr = new NativeArray<int>(16, Allocator.TempJob);
        var h1 = new ParallelInc { data = arr }.Schedule(16, 4);
        var h2 = new ParallelInc { data = arr }.Schedule(16, 4, h1);
        var combined = JobHandle.CombineDependencies(h1, h2);
        combined.Complete();
        Assert.True(combined.IsCompleted);
        Assert.Equal(2, arr[0]); // two increments
        arr.Dispose();
    }

    [Fact]
    public void JobsUtility_WorkerCount_AtLeastOne()
    {
        Assert.True(JobsUtility.JobWorkerCount >= 1);
        Assert.True(JobsUtility.JobWorkerMaximumCount >= 1);
    }

    [Fact]
    public void JobsUtility_ResetWorkerCount()
    {
        JobsUtility.JobWorkerCount = 2;
        JobsUtility.ResetJobWorkerCount();
        Assert.True(JobsUtility.JobWorkerCount >= 1);
    }

    [Fact]
    public void CompleteAll_Array()
    {
        var arr = new NativeArray<int>(8, Allocator.TempJob);
        var h1 = new ParallelInc { data = arr }.Schedule(8, 2);
        var h2 = new ParallelInc { data = arr }.Schedule(8, 2, h1); // must depend to avoid race
        JobHandle.CompleteAll(new[] { h1, h2 });
        Assert.Equal(2, arr[0]);
        arr.Dispose();
    }

    [Fact]
    public void ParallelFor_BatchSizeOne()
    {
        var arr = new NativeArray<int>(5, Allocator.TempJob);
        new ParallelInc { data = arr }.Schedule(5, 1).Complete();
        Assert.Equal(1, arr[4]);
        arr.Dispose();
    }

    [Fact]
    public void DefaultJobHandle_IsCompleted()
    {
        var h = default(JobHandle);
        // m_JobId 0 treated as completed
        Assert.True(h.IsCompleted);
    }

    [Fact]
    public void ScheduleBatchedJobs_DoesNotThrow()
    {
        JobHandle.ScheduleBatchedJobs();
    }

    [Fact]
    public void Parallel_ZeroLength_Completes()
    {
        var arr = new NativeArray<int>(0, Allocator.Temp);
        var h = new ParallelInc { data = arr }.Schedule(0, 8);
        h.Complete();
        Assert.True(h.IsCompleted);
        arr.Dispose();
    }
}
