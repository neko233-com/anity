using Unity.Jobs;

namespace UnityEngine;

public struct TransformAccessArray
{
  private Unity.Collections.NativeList<TransformAccess> m_Transforms;

  public int length => m_Transforms.IsCreated ? m_Transforms.Length : 0;

  public TransformAccess this[int index] => m_Transforms.IsCreated ? m_Transforms[index] : default;

  public TransformAccessArray(int capacity)
  {
    m_Transforms = new Unity.Collections.NativeList<TransformAccess>(capacity, Unity.Jobs.Allocator.Persistent);
  }

  public void Add(Transform transform)
  {
    m_Transforms.Add(new TransformAccess(transform));
  }

  public void SetTransforms(Transform[] transforms)
  {
    m_Transforms.Clear();
    if (transforms != null)
    {
      foreach (var t in transforms)
      {
        m_Transforms.Add(new TransformAccess(t));
      }
    }
  }

  public void Dispose()
  {
    if (m_Transforms.IsCreated)
    {
      m_Transforms.Dispose();
    }
  }
}
