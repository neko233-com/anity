using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Burst;

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
    internal Action _execute;

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
      _execute?.Invoke();
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
      return new JobHandle { m_IsCompleted = true, _execute = () => jobData.Execute() };
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

  public static class IJobParallelForTransformExtensions
  {
    public static JobHandle Schedule<T>(this T jobData, TransformAccessArray transforms, int innerloopBatchCount, JobHandle dependsOn = new JobHandle()) where T : struct, IJobParallelForTransform
    {
      dependsOn.Complete();
      for (int i = 0; i < transforms.length; i++)
      {
        jobData.Execute(i, transforms[i]);
      }
      return new JobHandle { m_IsCompleted = true };
    }

    public static void Run<T>(this T jobData, TransformAccessArray transforms) where T : struct, IJobParallelForTransform
    {
      for (int i = 0; i < transforms.length; i++)
      {
        jobData.Execute(i, transforms[i]);
      }
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

  [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field, AllowMultiple = false)]
  public class ReadOnlyAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field, AllowMultiple = false)]
  public class WriteOnlyAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field, AllowMultiple = false)]
  public class DeallocateOnJobCompletionAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Struct, AllowMultiple = false)]
  public class NativeDisableContainerSafetyRestrictionAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field, AllowMultiple = false)]
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
