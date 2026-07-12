using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Anity.Core.Runtime.Native;
using Unity.Collections;
using UnityEngine;

namespace Unity.Jobs
{
  public interface IJob
  {
    void Execute();
  }

  public interface IJobParallelFor
  {
    void Execute(int index);
  }

  public interface IJobParallelForTransform
  {
    void Execute(int index, TransformAccess transform);
  }

  public interface IJobParallelForBatch
  {
    void Execute(int startIndex, int count);
  }

  public interface IJobParallelForFilter
  {
    bool Execute(int index);
  }

  public interface IJobBurst
  {
    void Execute();
  }

  /// <summary>
  /// JobHandle with real dependency graph + completion events (Unity Jobs parity).
  /// </summary>
  public struct JobHandle
  {
    internal int m_JobId;
    internal bool m_IsCompleted;
    internal Action? _onComplete;
    internal int[]? _dependsOn;

    public bool IsCompleted
    {
      get
      {
        if (m_JobId <= 0) return m_IsCompleted || true;
        return JobScheduler.IsCompleted(m_JobId);
      }
    }

    public void Complete()
    {
      if (m_JobId > 0)
        JobScheduler.Complete(m_JobId);
      m_IsCompleted = true;
      _onComplete?.Invoke();
    }

    public static void ScheduleBatchedJobs()
    {
      JobScheduler.Flush();
    }

    public static void CompleteAll(NativeArray<JobHandle> jobs)
    {
      for (int i = 0; i < jobs.Length; i++)
        jobs[i].Complete();
    }

    public static void CompleteAll(JobHandle[] jobs)
    {
      if (jobs == null) return;
      for (int i = 0; i < jobs.Length; i++)
        jobs[i].Complete();
    }

    public static void CompleteDependencies(ref JobHandle job)
    {
      job.Complete();
    }

    public static JobHandle CombineDependencies(JobHandle job0, JobHandle job1)
    {
      return JobScheduler.Combine(new[] { job0, job1 });
    }

    public static JobHandle CombineDependencies(JobHandle job0, JobHandle job1, JobHandle job2)
    {
      return JobScheduler.Combine(new[] { job0, job1, job2 });
    }

    public static JobHandle CombineDependencies(NativeArray<JobHandle> jobs)
    {
      var arr = new JobHandle[jobs.Length];
      for (int i = 0; i < jobs.Length; i++)
        arr[i] = jobs[i];
      return JobScheduler.Combine(arr);
    }

    public static JobHandle CombineDependencies(JobHandle[] jobs)
    {
      return JobScheduler.Combine(jobs ?? Array.Empty<JobHandle>());
    }

    public static bool CheckFenceIsDependencyOrDidSyncFence(JobHandle dependency, JobHandle dependsOn)
    {
      return dependency.IsCompleted || dependsOn.IsCompleted;
    }
  }

  public static class IJobExtensions
  {
    public static JobHandle Schedule<T>(this T jobData, JobHandle dependsOn = default) where T : struct, IJob
    {
      var copy = jobData;
      return JobScheduler.Schedule(() => copy.Execute(), dependsOn, parallel: false);
    }

    public static void Run<T>(this T jobData) where T : struct, IJob
    {
      jobData.Execute();
    }

    public static void RunByRef<T>(this ref T jobData) where T : struct, IJob
    {
      jobData.Execute();
    }

    public static JobHandle ScheduleByRef<T>(this ref T jobData, JobHandle dependsOn = default) where T : struct, IJob
    {
      var copy = jobData;
      return JobScheduler.Schedule(() => copy.Execute(), dependsOn, parallel: false);
    }
  }

  public static class IJobParallelForExtensions
  {
    public static JobHandle Schedule<T>(this T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default) where T : struct, IJobParallelFor
    {
      if (arrayLength < 0) throw new ArgumentOutOfRangeException(nameof(arrayLength));
      if (innerloopBatchCount < 1) innerloopBatchCount = 1;
      var copy = jobData;
      int len = arrayLength;
      int batch = innerloopBatchCount;
      return JobScheduler.ScheduleParallel((start, count) =>
      {
        int end = start + count;
        for (int i = start; i < end && i < len; i++)
          copy.Execute(i);
      }, len, batch, dependsOn);
    }

    public static void Run<T>(this T jobData, int arrayLength) where T : struct, IJobParallelFor
    {
      if (arrayLength < 0) throw new ArgumentOutOfRangeException(nameof(arrayLength));
      for (int i = 0; i < arrayLength; i++)
        jobData.Execute(i);
    }

    public static void RunByRef<T>(this ref T jobData, int arrayLength) where T : struct, IJobParallelFor
    {
      for (int i = 0; i < arrayLength; i++)
        jobData.Execute(i);
    }

    public static JobHandle ScheduleByRef<T>(this ref T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default) where T : struct, IJobParallelFor
    {
      return jobData.Schedule(arrayLength, innerloopBatchCount, dependsOn);
    }
  }

  public static class IJobParallelForBatchExtensions
  {
    public static JobHandle ScheduleBatch<T>(this T jobData, int arrayLength, int indicesPerJob, JobHandle dependsOn = default) where T : struct, IJobParallelForBatch
    {
      if (arrayLength < 0) throw new ArgumentOutOfRangeException(nameof(arrayLength));
      if (indicesPerJob < 1) indicesPerJob = 1;
      var copy = jobData;
      return JobScheduler.ScheduleParallel((start, count) => copy.Execute(start, count), arrayLength, indicesPerJob, dependsOn);
    }
  }

  public static class IJobParallelForTransformExtensions
  {
    public static JobHandle Schedule<T>(this T jobData, TransformAccessArray transforms, JobHandle dependsOn = default) where T : struct, IJobParallelForTransform
    {
      dependsOn.Complete();
      var copy = jobData;
      int len = transforms.length;
      return JobScheduler.ScheduleParallel((start, count) =>
      {
        for (int i = start; i < start + count && i < len; i++)
          copy.Execute(i, transforms[i]);
      }, len, 32, dependsOn);
    }

    public static void Run<T>(this T jobData, TransformAccessArray transforms) where T : struct, IJobParallelForTransform
    {
      for (int i = 0; i < transforms.length; i++)
        jobData.Execute(i, transforms[i]);
    }
  }

  public struct TransformAccess
  {
    public int index;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 localPosition;
    public Quaternion localRotation;
    public Vector3 localScale;
    public Matrix4x4 localToWorldMatrix;
    public Matrix4x4 worldToLocalMatrix;
    public string name;

    public TransformAccess(Transform transform)
    {
      index = 0;
      position = transform != null ? transform.position : Vector3.zero;
      rotation = transform != null ? transform.rotation : Quaternion.identity;
      localPosition = transform != null ? transform.localPosition : Vector3.zero;
      localRotation = transform != null ? transform.localRotation : Quaternion.identity;
      localScale = transform != null ? transform.localScale : Vector3.one;
      localToWorldMatrix = transform != null ? transform.localToWorldMatrix : Matrix4x4.identity;
      worldToLocalMatrix = transform != null ? transform.worldToLocalMatrix : Matrix4x4.identity;
      name = transform != null ? transform.name : string.Empty;
    }
  }

  public struct TransformAccessArray : IDisposable
  {
    private TransformAccess[] _data;
    public int length => _data?.Length ?? 0;
    public TransformAccess this[int index] => _data[index];

    public TransformAccessArray(int capacity, int desiredJobCount = -1)
    {
      _data = new TransformAccess[Math.Max(0, capacity)];
      _ = desiredJobCount;
    }

    public TransformAccessArray(Transform[] transforms, int desiredJobCount = -1)
    {
      _ = desiredJobCount;
      if (transforms == null)
      {
        _data = Array.Empty<TransformAccess>();
        return;
      }
      _data = new TransformAccess[transforms.Length];
      for (int i = 0; i < transforms.Length; i++)
      {
        _data[i] = new TransformAccess(transforms[i]) { index = i };
      }
    }

    public void Dispose() => _data = Array.Empty<TransformAccess>();
  }

  /// <summary>JobsUtility — worker count / early job init (Unity.Jobs.LowLevel.Unsafe subset).</summary>
  public static class JobsUtility
  {
    public static int JobWorkerCount
    {
      get => JobScheduler.WorkerCount;
      set => JobScheduler.SetWorkerCount(value);
    }

    public static int JobWorkerMaximumCount => Environment.ProcessorCount;

    public static bool JobDebuggerEnabled { get; set; }

    public static bool JobCompilerEnabled { get; set; } = true;

    public static void ResetJobWorkerCount() => JobScheduler.SetWorkerCount(0);

    public static JobHandle ScheduleParallelFor(IntPtr reflectionData, int arrayLength, int batch, JobHandle dependsOn)
    {
      _ = reflectionData;
      // managed path only
      return dependsOn;
    }
  }

  internal static class JobScheduler
  {
    private static int s_NextId = 1;
    private static int s_WorkerCount;
    private static readonly object s_Lock = new();
    private static readonly ConcurrentDictionary<int, JobEntry> s_Jobs = new();

    static JobScheduler()
    {
      SetWorkerCount(0);
      try
      {
        if (AnityNative.Available)
          AnityNative.Jobs_Initialize(s_WorkerCount);
      }
      catch { /* managed fallback */ }
    }

    public static int WorkerCount => s_WorkerCount;

    public static void SetWorkerCount(int count)
    {
      if (count <= 0)
        count = Math.Max(1, Environment.ProcessorCount);
      s_WorkerCount = count;
      try
      {
        if (AnityNative.Available)
          AnityNative.Jobs_Initialize(s_WorkerCount);
      }
      catch { }
    }

    private sealed class JobEntry
    {
      public ManualResetEventSlim Done = new(false);
      public Exception? Error;
      public List<int> DependsOn = new();
      public Action? Body;
    }

    public static bool IsCompleted(int id)
    {
      if (!s_Jobs.TryGetValue(id, out var e)) return true;
      return e.Done.IsSet;
    }

    public static void Complete(int id)
    {
      if (!s_Jobs.TryGetValue(id, out var e)) return;
      // complete dependencies first
      foreach (var d in e.DependsOn)
        Complete(d);
      e.Done.Wait();
      if (e.Error != null)
        throw new AggregateException(e.Error);
      s_Jobs.TryRemove(id, out _);
    }

    public static void Flush()
    {
      foreach (var id in s_Jobs.Keys)
        Complete(id);
    }

    public static JobHandle Combine(JobHandle[] jobs)
    {
      if (jobs == null || jobs.Length == 0)
        return new JobHandle { m_IsCompleted = true };

      var deps = new List<int>();
      foreach (var j in jobs)
        if (j.m_JobId > 0) deps.Add(j.m_JobId);

      return Schedule(() =>
      {
        foreach (var d in deps)
          Complete(d);
      }, default, parallel: false, extraDeps: deps);
    }

    public static JobHandle Schedule(Action body, JobHandle dependsOn, bool parallel, List<int>? extraDeps = null)
    {
      int id;
      lock (s_Lock) { id = s_NextId++; }

      var entry = new JobEntry { Body = body };
      if (dependsOn.m_JobId > 0)
        entry.DependsOn.Add(dependsOn.m_JobId);
      if (extraDeps != null)
        entry.DependsOn.AddRange(extraDeps);

      s_Jobs[id] = entry;

      ThreadPool.QueueUserWorkItem(_ =>
      {
        try
        {
          foreach (var d in entry.DependsOn)
            Complete(d);
          entry.Body?.Invoke();
        }
        catch (Exception ex)
        {
          entry.Error = ex;
        }
        finally
        {
          entry.Done.Set();
        }
      });

      return new JobHandle { m_JobId = id, m_IsCompleted = false };
    }

    public static JobHandle ScheduleParallel(Action<int, int> batchBody, int length, int batchSize, JobHandle dependsOn)
    {
      if (length <= 0)
        return Schedule(() => { }, dependsOn, false);

      if (batchSize < 1) batchSize = 1;
      int batches = (length + batchSize - 1) / batchSize;

      // Prefer native parallel when available for pure index work
      int id;
      lock (s_Lock) { id = s_NextId++; }
      var entry = new JobEntry();
      if (dependsOn.m_JobId > 0)
        entry.DependsOn.Add(dependsOn.m_JobId);
      s_Jobs[id] = entry;

      ThreadPool.QueueUserWorkItem(_ =>
      {
        try
        {
          foreach (var d in entry.DependsOn)
            Complete(d);

          var options = new ParallelOptions { MaxDegreeOfParallelism = s_WorkerCount };
          Parallel.For(0, batches, options, b =>
          {
            int start = b * batchSize;
            int count = Math.Min(batchSize, length - start);
            batchBody(start, count);
          });
        }
        catch (Exception ex)
        {
          entry.Error = ex;
        }
        finally
        {
          entry.Done.Set();
        }
      });

      return new JobHandle { m_JobId = id };
    }
  }

  // Compatibility attributes
  [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field)]
  public class ReadOnlyAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field)]
  public class WriteOnlyAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field)]
  public class DeallocateOnJobCompletionAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Struct)]
  public class NativeDisableContainerSafetyRestrictionAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field)]
  public class NativeDisableParallelForRestrictionAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Struct)]
  public class NativeContainerAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Field)]
  public class NativeSetThreadIndexAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Struct)]
  public class NativeContainerIsAtomicWriteOnlyAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Struct)]
  public class NativeContainerSupportsDeallocateOnJobCompletionAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Struct)]
  public class NativeContainerSupportsMinMaxWriteRestrictionAttribute : Attribute { }

  public enum Allocator
  {
    Invalid = 0,
    None = 1,
    Temp = 2,
    TempJob = 3,
    Persistent = 4,
    AudioKernel = 5
  }

  public enum NativeArrayOptions
  {
    UninitializedMemory = 0,
    ClearMemory = 1
  }

  public enum JobMode
  {
    Single,
    ParallelFor,
    ParallelForTransform,
    ParallelForBatch
  }

  public enum FloatPrecision
  {
    Standard = 0,
    High = 1,
    Medium = 2,
    Low = 3,
    Fast = 4
  }
}
