using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

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

  public struct JobHandle
  {
    internal int m_JobId;
    internal bool m_IsCompleted;
    internal object m_Data;

    public bool IsCompleted
    {
      get
      {
        if (m_JobId <= 0) return true;
        JobScheduler.EnsureCompleted(m_JobId);
        return m_IsCompleted;
      }
    }

    public static JobHandle ScheduleBatchedJobs()
    {
      return new JobHandle { m_IsCompleted = true };
    }

    public static void CompleteDependencies(ref JobHandle job)
    {
      if (job.m_JobId > 0)
      {
        JobScheduler.EnsureCompleted(job.m_JobId);
        job.m_IsCompleted = true;
      }
    }

    public void Complete()
    {
      if (m_JobId > 0)
      {
        JobScheduler.EnsureCompleted(m_JobId);
        m_IsCompleted = true;
      }
    }

    public static JobHandle CombineDependencies(JobHandle job0, JobHandle job1)
    {
      return new JobHandle { m_IsCompleted = job0.IsCompleted && job1.IsCompleted };
    }

    public static JobHandle CombineDependencies(JobHandle job0, JobHandle job1, JobHandle job2)
    {
      return new JobHandle { m_IsCompleted = job0.IsCompleted && job1.IsCompleted && job2.IsCompleted };
    }

    public static JobHandle CombineDependencies(NativeArray<JobHandle> jobs)
    {
      bool allDone = true;
      for (int i = 0; i < jobs.Length; i++)
      {
        if (!jobs[i].IsCompleted)
        {
          allDone = false;
          break;
        }
      }
      return new JobHandle { m_IsCompleted = allDone };
    }

    public static bool CheckScheduleArrayEmptySafe(ref NativeArray<JobHandle> jobs)
    {
      return jobs.Length == 0;
    }

    public static JobHandle Schedule<T>(T jobData, JobHandle dependsOn = new JobHandle()) where T : struct, IJob
    {
      dependsOn.Complete();
      jobData.Execute();
      return new JobHandle { m_IsCompleted = true };
    }
  }

  [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
  public class BurstCompileAttribute : Attribute
  {
    public float FloatMode { get; set; }
    public FloatPrecision FloatPrecision { get; set; }
    public bool CompileSynchronously { get; set; }
    public bool Options { get; set; }
    public string[] OptionsNames { get; set; }

    public BurstCompileAttribute() { }
    public BurstCompileAttribute(float precision) { FloatPrecision = (FloatPrecision)precision; }
  }

  public enum FloatPrecision
  {
    Standard = 0,
    High = 1,
    Medium = 2,
    Low = 3,
    Fast = 4
  }

  public static class JobExtensions
  {
    public static JobHandle Schedule<T>(this T jobData, JobHandle dependsOn = new JobHandle()) where T : struct, IJob
    {
      dependsOn.Complete();
      jobData.Execute();
      return new JobHandle { m_IsCompleted = true };
    }

    public static JobHandle Run<T>(this T jobData) where T : struct, IJob
    {
      jobData.Execute();
      return new JobHandle { m_IsCompleted = true };
    }

    public static JobHandle Run<T>(this T jobData, int arrayLength) where T : struct, IJobParallelFor
    {
      for (int i = 0; i < arrayLength; i++)
      {
        jobData.Execute(i);
      }
      return new JobHandle { m_IsCompleted = true };
    }

    public static void RunWithoutBurst<T>(this T jobData) where T : struct, IJob
    {
      jobData.Execute();
    }

    public static JobHandle ScheduleBatch<T>(this T jobData, int arrayLength, int innerloopBatchCount, ref NativeList<JobHandle> jobHandles, JobHandle dependsOn = new JobHandle()) where T : struct, IJobParallelFor
    {
      dependsOn.Complete();
      var handle = jobData.Schedule(arrayLength, innerloopBatchCount, dependsOn);
      jobHandles.Add(handle);
      return handle;
    }
  }

  public static class IJobParallelForExtensions
  {
    public static JobHandle Schedule<T>(this T jobData, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = new JobHandle()) where T : struct, IJobParallelFor
    {
      dependsOn.Complete();
      for (int i = 0; i < arrayLength; i++)
      {
        jobData.Execute(i);
      }
      return new JobHandle { m_IsCompleted = true };
    }

    public static JobHandle Schedule<T>(this T jobData, NativeArray<int> indices, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = new JobHandle()) where T : struct, IJobParallelFor
    {
      dependsOn.Complete();
      for (int i = 0; i < arrayLength; i++)
      {
        jobData.Execute(i);
      }
      return new JobHandle { m_IsCompleted = true };
    }

    public static void Run<T>(this T jobData, int arrayLength) where T : struct, IJobParallelFor
    {
      for (int i = 0; i < arrayLength; i++)
      {
        jobData.Execute(i);
      }
    }

    public static void RunWithoutBurst<T>(this T jobData, int arrayLength) where T : struct, IJobParallelFor
    {
      for (int i = 0; i < arrayLength; i++)
      {
        jobData.Execute(i);
      }
    }
  }

  public struct TransformAccess
  {
    public int index;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 localScale;
    public string name;
  }

  internal static class JobScheduler
  {
    private static int s_NextJobId = 1;
    private static readonly Dictionary<int, ManualResetEvent> s_Jobs = new();
    private static readonly object s_Lock = new();

    public static int RegisterJob()
    {
      lock (s_Lock)
      {
        int id = s_NextJobId++;
        s_Jobs[id] = new ManualResetEvent(true);
        return id;
      }
    }

    public static void EnsureCompleted(int jobId)
    {
      lock (s_Lock)
      {
        if (s_Jobs.TryGetValue(jobId, out var ev))
        {
          ev.WaitOne();
          s_Jobs.Remove(jobId);
        }
      }
    }
  }

  public enum JobMode
  {
    Single,
    ParallelFor,
    ParallelForTransform,
    ParallelForBatch
  }

  [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
  public class ReadOnlyAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
  public class WriteOnlyAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
  public class NativeDisableParallelForRestrictionAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
  public class NativeContainerAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
  public class NativeSetThreadIndexAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
  public class NativeContainerIsAtomicWriteOnlyAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
  public class NativeContainerSupportsDeallocateOnJobCompletionAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
  public class NativeContainerSupportsMinMaxWriteRestrictionAttribute : Attribute
  {
  }

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
}
