using System;
using System.Collections.Generic;
using Unity.Jobs;

namespace Unity.Collections
{
  public struct NativeArray<T> : IDisposable, IEnumerable<T> where T : struct
  {
    private T[] m_Data;
    private Allocator m_Allocator;
    private bool m_IsCreated;
    private int m_Length;
    private int m_MinIndex;
    private int m_MaxIndex;
    private bool m_DisposeOnJobCompletion;

    public int Length => m_Length;
    public bool IsCreated => m_IsCreated;
    public Allocator Allocator => m_Allocator;

    public T this[int index]
    {
      get => m_Data[index];
      set => m_Data[index] = value;
    }

    public NativeArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
    {
      m_Data = new T[length];
      m_Length = length;
      m_Allocator = allocator;
      m_IsCreated = true;
      m_MinIndex = 0;
      m_MaxIndex = length - 1;
      m_DisposeOnJobCompletion = false;
      if (options == NativeArrayOptions.ClearMemory)
      {
        Array.Clear(m_Data, 0, length);
      }
    }

    public NativeArray(T[] array, Allocator allocator)
    {
      m_Data = (T[])array.Clone();
      m_Length = array.Length;
      m_Allocator = allocator;
      m_IsCreated = true;
      m_MinIndex = 0;
      m_MaxIndex = m_Length - 1;
      m_DisposeOnJobCompletion = false;
    }

    public NativeArray(NativeArray<T> other, Allocator allocator)
    {
      m_Data = new T[other.Length];
      Array.Copy(other.m_Data, m_Data, other.Length);
      m_Length = other.Length;
      m_Allocator = allocator;
      m_IsCreated = true;
      m_MinIndex = 0;
      m_MaxIndex = m_Length - 1;
      m_DisposeOnJobCompletion = false;
    }

    public void Dispose()
    {
      if (m_IsCreated)
      {
        m_Data = null;
        m_IsCreated = false;
        m_Length = 0;
      }
    }

    public JobHandle Dispose(JobHandle dependsOn)
    {
      dependsOn.Complete();
      Dispose();
      return new JobHandle { m_IsCompleted = true };
    }

    public T[] ToArray()
    {
      var result = new T[m_Length];
      Array.Copy(m_Data, result, m_Length);
      return result;
    }

    public NativeArray<T> CopyFrom(T[] array, Allocator allocator)
    {
      return new NativeArray<T>(array, allocator);
    }

    public void CopyFrom(T[] array)
    {
      Array.Copy(array, m_Data, Math.Min(array.Length, m_Length));
    }

    public void CopyFrom(NativeArray<T> other)
    {
      Array.Copy(other.m_Data, m_Data, Math.Min(other.Length, m_Length));
    }

    public void CopyTo(T[] array)
    {
      Array.Copy(m_Data, array, m_Length);
    }

    public void CopyTo(NativeArray<T> other)
    {
      Array.Copy(m_Data, other.m_Data, Math.Min(m_Length, other.Length));
    }

    public static void Copy(NativeArray<T> src, NativeArray<T> dst)
    {
      Array.Copy(src.m_Data, dst.m_Data, Math.Min(src.Length, dst.Length));
    }

    public static void Copy(NativeArray<T> src, NativeArray<T> dst, int length)
    {
      Array.Copy(src.m_Data, dst.m_Data, length);
    }

    public static void Copy(T[] src, NativeArray<T> dst)
    {
      Array.Copy(src, dst.m_Data, Math.Min(src.Length, dst.Length));
    }

    public static void Copy(NativeArray<T> src, T[] dst)
    {
      Array.Copy(src.m_Data, dst, src.Length);
    }

    public static NativeArray<T> ConvertExistingDataToNativeArray(T[] data, int length, Allocator allocator)
    {
      return new NativeArray<T>(data, allocator);
    }

    public NativeArray<T>.ReadOnly AsReadOnly()
    {
      return new NativeArray<T>.ReadOnly(m_Data, m_Length);
    }

    public NativeSlice<T> Slice()
    {
      return new NativeSlice<T>(this);
    }

    public NativeSlice<T> Slice(int start)
    {
      return new NativeSlice<T>(this, start, m_Length - start);
    }

    public NativeSlice<T> Slice(int start, int length)
    {
      return new NativeSlice<T>(this, start, length);
    }

    public NativeArray<T>.Enumerator GetEnumerator()
    {
      return new NativeArray<T>.Enumerator(ref this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
      for (int i = 0; i < m_Length; i++)
      {
        yield return m_Data[i];
      }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    public ref T GetReadWriteRef(int index)
    {
      return ref m_Data[index];
    }

    public static implicit operator NativeArray<T>(T[] array)
    {
      return new NativeArray<T>(array, Allocator.Temp);
    }

    public struct ReadOnly
    {
      private T[] m_Data;
      private int m_Length;
      public ReadOnly(T[] data, int length) { m_Data = data; m_Length = length; }
      public int Length => m_Length;
      public T this[int index] => m_Data[index];
    }

    public struct Enumerator : IEnumerator<T>
    {
      private NativeArray<T> m_Array;
      private int m_Index;

      public T Current => m_Array[m_Index];
      object System.Collections.IEnumerator.Current => Current;

      public Enumerator(ref NativeArray<T> array)
      {
        m_Array = array;
        m_Index = -1;
      }

      public bool MoveNext()
      {
        m_Index++;
        return m_Index < m_Array.Length;
      }

      public void Reset()
      {
        m_Index = -1;
      }

      public void Dispose() { }
    }
  }

  public struct NativeList<T> : IDisposable where T : struct
  {
    private NativeArray<T> m_Data;
    private int m_Length;
    private Allocator m_Allocator;
    private bool m_IsCreated;

    public int Length => m_Length;
    public int Count => m_Length;
    public int Capacity => m_Data.IsCreated ? m_Data.Length : 0;
    public bool IsCreated => m_IsCreated;
    public Allocator Allocator => m_Allocator;

    public T this[int index]
    {
      get => m_Data[index];
      set => m_Data[index] = value;
    }

    public NativeList(Allocator allocator)
    {
      m_Data = new NativeArray<T>(16, allocator, NativeArrayOptions.UninitializedMemory);
      m_Length = 0;
      m_Allocator = allocator;
      m_IsCreated = true;
    }

    public NativeList(int initialCapacity, Allocator allocator)
    {
      m_Data = new NativeArray<T>(initialCapacity, allocator, NativeArrayOptions.UninitializedMemory);
      m_Length = 0;
      m_Allocator = allocator;
      m_IsCreated = true;
    }

    public NativeList(T[] array, Allocator allocator)
    {
      m_Data = new NativeArray<T>(array, allocator);
      m_Length = array.Length;
      m_Allocator = allocator;
      m_IsCreated = true;
    }

    public void Add(T value)
    {
      if (m_Length >= Capacity)
      {
        Resize(Math.Max(Capacity * 2, 16));
      }
      m_Data[m_Length] = value;
      m_Length++;
    }

    public void AddRange(NativeArray<T> range)
    {
      int newLength = m_Length + range.Length;
      if (newLength > Capacity)
      {
        Resize(newLength);
      }
      for (int i = 0; i < range.Length; i++)
      {
        m_Data[m_Length + i] = range[i];
      }
      m_Length = newLength;
    }

    public void AddRange(NativeList<T> range)
    {
      AddRange(range.AsArray());
    }

    public void Insert(int index, T value)
    {
      if (index < 0 || index > m_Length) throw new ArgumentOutOfRangeException(nameof(index));
      if (m_Length >= Capacity)
      {
        Resize(Math.Max(Capacity * 2, 16));
      }
      for (int i = m_Length; i > index; i--)
      {
        m_Data[i] = m_Data[i - 1];
      }
      m_Data[index] = value;
      m_Length++;
    }

    public void RemoveAt(int index)
    {
      if (index < 0 || index >= m_Length) throw new ArgumentOutOfRangeException(nameof(index));
      for (int i = index; i < m_Length - 1; i++)
      {
        m_Data[i] = m_Data[i + 1];
      }
      m_Length--;
    }

    public void RemoveAtSwapBack(int index)
    {
      if (index < 0 || index >= m_Length) throw new ArgumentOutOfRangeException(nameof(index));
      m_Data[index] = m_Data[m_Length - 1];
      m_Length--;
    }

    public void Clear()
    {
      m_Length = 0;
    }

    public void Resize(int newLength, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
    {
      if (newLength <= Capacity)
      {
        if (newLength > m_Length && options == NativeArrayOptions.ClearMemory)
        {
          for (int i = m_Length; i < newLength; i++)
          {
            m_Data[i] = default;
          }
        }
        m_Length = newLength;
        return;
      }

      var newData = new NativeArray<T>(newLength, m_Allocator, options);
      if (m_Data.IsCreated)
      {
        for (int i = 0; i < m_Length; i++)
        {
          newData[i] = m_Data[i];
        }
        m_Data.Dispose();
      }
      m_Data = newData;
      m_Length = newLength;
    }

    public void TrimExcess()
    {
      if (m_Length == Capacity) return;
      var newData = new NativeArray<T>(m_Length, m_Allocator);
      for (int i = 0; i < m_Length; i++)
      {
        newData[i] = m_Data[i];
      }
      m_Data.Dispose();
      m_Data = newData;
    }

    public T[] ToArray()
    {
      var result = new T[m_Length];
      for (int i = 0; i < m_Length; i++)
      {
        result[i] = m_Data[i];
      }
      return result;
    }

    public NativeArray<T> AsArray()
    {
      return m_Data;
    }

    public NativeArray<T> AsDeferredJobArray()
    {
      return m_Data;
    }

    public ParallelReader AsParallelReader()
    {
      return new ParallelReader(m_Data);
    }

    public void Dispose()
    {
      if (m_IsCreated)
      {
        m_Data.Dispose();
        m_IsCreated = false;
        m_Length = 0;
      }
    }

    public JobHandle Dispose(JobHandle dependsOn)
    {
      dependsOn.Complete();
      Dispose();
      return new JobHandle { m_IsCompleted = true };
    }

    public NativeList<T>.Enumerator GetEnumerator()
    {
      return new NativeList<T>.Enumerator(ref this);
    }

    public struct ParallelReader
    {
      private NativeArray<T> m_Data;
      public ParallelReader(NativeArray<T> data) { m_Data = data; }
      public T this[int index] => m_Data[index];
      public int Length => m_Data.Length;
    }

    public struct Enumerator
    {
      private NativeList<T> m_List;
      private int m_Index;

      public T Current => m_List[m_Index];

      public Enumerator(ref NativeList<T> list)
      {
        m_List = list;
        m_Index = -1;
      }

      public bool MoveNext()
      {
        m_Index++;
        return m_Index < m_List.Length;
      }

      public void Reset()
      {
        m_Index = -1;
      }
    }
  }

  public struct NativeHashMap<TKey, TValue> : IDisposable
    where TKey : struct, IEquatable<TKey>
    where TValue : struct
  {
    private Dictionary<TKey, TValue> m_Data;
    private Allocator m_Allocator;
    private bool m_IsCreated;

    public int Count => m_Data?.Count ?? 0;
    public int Capacity => m_Data?.Count ?? 0;
    public bool IsCreated => m_IsCreated;
    public Allocator Allocator => m_Allocator;

    public TValue this[TKey key]
    {
      get => m_Data[key];
      set => m_Data[key] = value;
    }

    public NativeHashMap(int capacity, Allocator allocator)
    {
      m_Data = new Dictionary<TKey, TValue>(capacity);
      m_Allocator = allocator;
      m_IsCreated = true;
    }

    public bool TryAdd(TKey key, TValue value)
    {
      if (m_Data.ContainsKey(key)) return false;
      m_Data.Add(key, value);
      return true;
    }

    public void Add(TKey key, TValue value)
    {
      m_Data.Add(key, value);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
      return m_Data.TryGetValue(key, out value);
    }

    public bool ContainsKey(TKey key)
    {
      return m_Data.ContainsKey(key);
    }

    public bool Remove(TKey key)
    {
      return m_Data.Remove(key);
    }

    public void Clear()
    {
      m_Data.Clear();
    }

    public ReadOnly AsReadOnly()
    {
      return new ReadOnly(m_Data);
    }

    public Concurrent ToConcurrent()
    {
      return new Concurrent(m_Data);
    }

    public void Dispose()
    {
      if (m_IsCreated)
      {
        m_Data = null;
        m_IsCreated = false;
      }
    }

    public JobHandle Dispose(JobHandle dependsOn)
    {
      dependsOn.Complete();
      Dispose();
      return new JobHandle { m_IsCompleted = true };
    }

    public NativeKeyValueArrays<TKey, TValue> GetKeyValueArrays(Allocator allocator)
    {
      var keys = new NativeArray<TKey>(Count, allocator);
      var values = new NativeArray<TValue>(Count, allocator);
      int i = 0;
      foreach (var kvp in m_Data)
      {
        keys[i] = kvp.Key;
        values[i] = kvp.Value;
        i++;
      }
      return new NativeKeyValueArrays<TKey, TValue>(keys, values);
    }

    public Enumerator GetEnumerator()
    {
      return new Enumerator(m_Data.GetEnumerator());
    }

    public struct Enumerator
    {
      private Dictionary<TKey, TValue>.Enumerator m_Enumerator;
      public Enumerator(Dictionary<TKey, TValue>.Enumerator enumerator) { m_Enumerator = enumerator; }
      public KeyValuePair<TKey, TValue> Current => m_Enumerator.Current;
      public bool MoveNext() => m_Enumerator.MoveNext();
      public void Dispose() => m_Enumerator.Dispose();
    }

    public struct ReadOnly
    {
      private Dictionary<TKey, TValue> m_Data;
      public ReadOnly(Dictionary<TKey, TValue> data) { m_Data = data; }
      public int Count => m_Data.Count;
      public bool TryGetValue(TKey key, out TValue value) => m_Data.TryGetValue(key, out value);
      public bool ContainsKey(TKey key) => m_Data.ContainsKey(key);
    }

    public struct Concurrent
    {
      private Dictionary<TKey, TValue> m_Data;
      public Concurrent(Dictionary<TKey, TValue> data) { m_Data = data; }
      public bool TryAdd(TKey key, TValue value)
      {
        lock (m_Data)
        {
          if (m_Data.ContainsKey(key)) return false;
          m_Data.Add(key, value);
          return true;
        }
      }
      public int Count => m_Data.Count;
    }
  }

  public struct NativeMultiHashMap<TKey, TValue> : IDisposable
    where TKey : struct, IEquatable<TKey>
    where TValue : struct
  {
    private Dictionary<TKey, List<TValue>> m_Data;
    private Allocator m_Allocator;
    private bool m_IsCreated;
    private int m_Count;

    public int Count => m_Count;
    public bool IsCreated => m_IsCreated;
    public Allocator Allocator => m_Allocator;

    public NativeMultiHashMap(int capacity, Allocator allocator)
    {
      m_Data = new Dictionary<TKey, List<TValue>>(capacity);
      m_Allocator = allocator;
      m_IsCreated = true;
      m_Count = 0;
    }

    public void Add(TKey key, TValue value)
    {
      if (!m_Data.TryGetValue(key, out var list))
      {
        list = new List<TValue>();
        m_Data[key] = list;
      }
      list.Add(value);
      m_Count++;
    }

    public bool Remove(TKey key)
    {
      if (m_Data.TryGetValue(key, out var list))
      {
        m_Count -= list.Count;
        return m_Data.Remove(key);
      }
      return false;
    }

    public void Remove(TKey key, TValue value)
    {
      if (m_Data.TryGetValue(key, out var list))
      {
        if (list.Remove(value))
        {
          m_Count--;
          if (list.Count == 0)
          {
            m_Data.Remove(key);
          }
        }
      }
    }

    public bool ContainsKey(TKey key)
    {
      return m_Data.ContainsKey(key);
    }

    public bool TryGetFirstValue(TKey key, out TValue item, out NativeMultiHashMapIterator<TKey> it)
    {
      it = default;
      if (m_Data.TryGetValue(key, out var list) && list.Count > 0)
      {
        it.key = key;
        it.entryIndex = 0;
        item = list[0];
        return true;
      }
      item = default;
      return false;
    }

    public bool TryGetNextValue(out TValue item, ref NativeMultiHashMapIterator<TKey> it)
    {
      if (m_Data.TryGetValue(it.key, out var list) && it.entryIndex + 1 < list.Count)
      {
        it.entryIndex++;
        item = list[it.entryIndex];
        return true;
      }
      item = default;
      return false;
    }

    public void Clear()
    {
      m_Data.Clear();
      m_Count = 0;
    }

    public ReadOnly AsReadOnly()
    {
      return new ReadOnly(m_Data);
    }

    public Concurrent ToConcurrent()
    {
      return new Concurrent(m_Data);
    }

    public void Dispose()
    {
      if (m_IsCreated)
      {
        m_Data = null;
        m_IsCreated = false;
        m_Count = 0;
      }
    }

    public JobHandle Dispose(JobHandle dependsOn)
    {
      dependsOn.Complete();
      Dispose();
      return new JobHandle { m_IsCompleted = true };
    }

    public struct ReadOnly
    {
      private Dictionary<TKey, List<TValue>> m_Data;
      public ReadOnly(Dictionary<TKey, List<TValue>> data) { m_Data = data; }
      public bool ContainsKey(TKey key) => m_Data.ContainsKey(key);
    }

    public struct Concurrent
    {
      private Dictionary<TKey, List<TValue>> m_Data;
      public Concurrent(Dictionary<TKey, List<TValue>> data) { m_Data = data; }
      public void Add(TKey key, TValue value)
      {
        lock (m_Data)
        {
          if (!m_Data.TryGetValue(key, out var list))
          {
            list = new List<TValue>();
            m_Data[key] = list;
          }
          list.Add(value);
        }
      }
    }
  }

  public struct NativeMultiHashMapIterator<TKey> where TKey : struct, IEquatable<TKey>
  {
    internal TKey key;
    internal int entryIndex;
  }

  public struct NativeKeyValueArrays<TKey, TValue> : IDisposable
    where TKey : struct, IEquatable<TKey>
    where TValue : struct
  {
    public NativeArray<TKey> Keys;
    public NativeArray<TValue> Values;
    public int Length => Keys.Length;

    public NativeKeyValueArrays(NativeArray<TKey> keys, NativeArray<TValue> values)
    {
      Keys = keys;
      Values = values;
    }

    public void Dispose()
    {
      Keys.Dispose();
      Values.Dispose();
    }
  }

  public struct NativeQueue<T> : IDisposable where T : struct
  {
    private Queue<T> m_Data;
    private Allocator m_Allocator;
    private bool m_IsCreated;

    public int Count => m_Data?.Count ?? 0;
    public bool IsCreated => m_IsCreated;
    public Allocator Allocator => m_Allocator;

    public NativeQueue(Allocator allocator)
    {
      m_Data = new Queue<T>();
      m_Allocator = allocator;
      m_IsCreated = true;
    }

    public void Enqueue(T value)
    {
      m_Data.Enqueue(value);
    }

    public bool TryDequeue(out T value)
    {
      return m_Data.TryDequeue(out value);
    }

    public T Dequeue()
    {
      return m_Data.Dequeue();
    }

    public T Peek()
    {
      return m_Data.Peek();
    }

    public void Clear()
    {
      m_Data.Clear();
    }

    public ParallelWriter AsParallelWriter()
    {
      return new ParallelWriter(m_Data);
    }

    public void Dispose()
    {
      if (m_IsCreated)
      {
        m_Data = null;
        m_IsCreated = false;
      }
    }

    public JobHandle Dispose(JobHandle dependsOn)
    {
      dependsOn.Complete();
      Dispose();
      return new JobHandle { m_IsCompleted = true };
    }

    public NativeArray<T> ToArray(Allocator allocator)
    {
      return new NativeArray<T>(m_Data.ToArray(), allocator);
    }

    public T[] ToArray()
    {
      return m_Data.ToArray();
    }

    public struct ParallelWriter
    {
      private Queue<T> m_Data;
      public ParallelWriter(Queue<T> data) { m_Data = data; }
      public void Enqueue(T value)
      {
        lock (m_Data)
        {
          m_Data.Enqueue(value);
        }
      }
    }
  }

  public struct NativeSlice<T> where T : struct
  {
    private NativeArray<T> m_Array;
    private int m_Start;
    private int m_Length;

    public int Length => m_Length;
    public int Stride => 1;

    public T this[int index]
    {
      get => m_Array[m_Start + index];
      set => m_Array[m_Start + index] = value;
    }

    public NativeSlice(NativeArray<T> array)
    {
      m_Array = array;
      m_Start = 0;
      m_Length = array.Length;
    }

    public NativeSlice(NativeArray<T> array, int start, int length)
    {
      m_Array = array;
      m_Start = start;
      m_Length = length;
    }

    public NativeArray<T> ToArray()
    {
      var result = new NativeArray<T>(m_Length, Allocator.Temp);
      for (int i = 0; i < m_Length; i++)
      {
        result[i] = m_Array[m_Start + i];
      }
      return result;
    }

    public NativeSlice<T> Slice(int start)
    {
      return new NativeSlice<T>(m_Array, m_Start + start, m_Length - start);
    }

    public NativeSlice<T> Slice(int start, int length)
    {
      return new NativeSlice<T>(m_Array, m_Start + start, length);
    }

    public static implicit operator NativeSlice<T>(NativeArray<T> array)
    {
      return new NativeSlice<T>(array);
    }
  }
}
