using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering
{
  public class Volume : MonoBehaviour
  {
    [SerializeField] private bool m_IsGlobal = true;
    [SerializeField] private LayerMask m_LayerMask = 1;
    [SerializeField] private float m_BlendDistance = 0f;
    [SerializeField] private float m_Weight = 1f;
    [SerializeField] private VolumePriority m_Priority = 0f;
    [SerializeField] private List<VolumeComponent> m_Components = new List<VolumeComponent>();
    [SerializeField] internal VolumeProfile m_Profile;

    public bool isGlobal
    {
      get => m_IsGlobal;
      set => m_IsGlobal = value;
    }

    public LayerMask layerMask
    {
      get => m_LayerMask;
      set => m_LayerMask = value;
    }

    public float blendDistance
    {
      get => m_BlendDistance;
      set => m_BlendDistance = Mathf.Max(0f, value);
    }

    public float weight
    {
      get => m_Weight;
      set => m_Weight = Mathf.Clamp01(value);
    }

    public float priority
    {
      get => m_Priority;
      set => m_Priority = value;
    }

    public VolumeProfile profile
    {
      get => m_Profile;
      set => m_Profile = value;
    }

    public List<VolumeComponent> components => m_Components;

    public bool sharedProfile => true;

    private void OnEnable()
    {
      VolumeManager.instance.Register(this);
    }

    private void OnDisable()
    {
      VolumeManager.instance.Unregister(this);
    }

    private void OnDestroy()
    {
      VolumeManager.instance.Unregister(this);
    }

    public T Add<T>(bool overrides = false) where T : VolumeComponent
    {
      var comp = (T)Activator.CreateInstance(typeof(T));
      comp.active = true;
      comp.displayName = typeof(T).Name;
      m_Components.Add(comp);
      return comp;
    }

    public VolumeComponent Add(Type type, bool overrides = false)
    {
      if (!typeof(VolumeComponent).IsAssignableFrom(type))
        throw new ArgumentException("type must inherit from VolumeComponent");
      var comp = (VolumeComponent)Activator.CreateInstance(type);
      comp.active = true;
      comp.displayName = type.Name;
      m_Components.Add(comp);
      return comp;
    }

    public bool Has<T>() where T : VolumeComponent
    {
      for (int i = 0; i < m_Components.Count; i++)
        if (m_Components[i] is T)
          return true;
      if (m_Profile != null)
        return m_Profile.Has<T>();
      return false;
    }

    public bool Has(Type type)
    {
      for (int i = 0; i < m_Components.Count; i++)
        if (type.IsInstanceOfType(m_Components[i]))
          return true;
      if (m_Profile != null)
        return m_Profile.Has(type);
      return false;
    }

    public T Get<T>() where T : VolumeComponent
    {
      for (int i = 0; i < m_Components.Count; i++)
        if (m_Components[i] is T comp)
          return comp;
      if (m_Profile != null)
        return m_Profile.Get<T>();
      return null;
    }

    public bool TryGet<T>(out T component) where T : VolumeComponent
    {
      component = Get<T>();
      return component != null;
    }
  }

  public class VolumeProfile : ScriptableObject
  {
    [SerializeField] private List<VolumeComponent> m_Components = new List<VolumeComponent>();
    public bool isFallback;

    public List<VolumeComponent> components => m_Components;

    public T Add<T>(bool overrides = false) where T : VolumeComponent
    {
      var comp = (T)Activator.CreateInstance(typeof(T));
      comp.active = true;
      comp.displayName = typeof(T).Name;
      m_Components.Add(comp);
      return comp;
    }

    public VolumeComponent Add(Type type, bool overrides = false)
    {
      var comp = (VolumeComponent)Activator.CreateInstance(type);
      comp.active = true;
      comp.displayName = type.Name;
      m_Components.Add(comp);
      return comp;
    }

    public T Remove<T>() where T : VolumeComponent
    {
      for (int i = 0; i < m_Components.Count; i++)
      {
        if (m_Components[i] is T comp)
        {
          m_Components.RemoveAt(i);
          return comp;
        }
      }
      return null;
    }

    public bool Has<T>() where T : VolumeComponent
    {
      for (int i = 0; i < m_Components.Count; i++)
        if (m_Components[i] is T)
          return true;
      return false;
    }

    public bool Has(Type type)
    {
      for (int i = 0; i < m_Components.Count; i++)
        if (type.IsInstanceOfType(m_Components[i]))
          return true;
      return false;
    }

    public T Get<T>() where T : VolumeComponent
    {
      for (int i = 0; i < m_Components.Count; i++)
        if (m_Components[i] is T comp)
          return comp;
      return null;
    }

    public bool TryGet<T>(out T component) where T : VolumeComponent
    {
      component = Get<T>();
      return component != null;
    }

    public void Reset()
    {
      m_Components.Clear();
    }
  }

  [Serializable]
  public class VolumeComponent
  {
    public bool active = true;
    public string displayName { get; set; }
    internal string name { get; set; }

    public virtual void Override(VolumeComponent state, float interpFactor)
    {
      _ = state; _ = interpFactor;
    }

    public void SetAllOverridesTo(bool state)
    {
      _ = state;
    }
  }

  [Serializable]
  public class VolumeParameter
  {
    [SerializeField]
    internal bool m_OverrideState;
    [SerializeField]
    internal bool m_OverrideStateValue;
    [SerializeField]
    internal object m_Value;
    [SerializeField]
    internal object m_DisplayName;
    [SerializeField]
    internal object m_DisplayOrder;
    [SerializeField]
    internal object m_ParameterType;

    public bool overrideState
    {
      get => m_OverrideState;
      set => m_OverrideState = value;
    }

    public string displayName { get; set; }

    public float InterpObject;

    int displayOrder { get; set; }

    public Type parameterType { get; set; }

    public VolumeParameter()
    {
    }

    public void Interp(object from, object to, float t)
    {
      _ = from; _ = to; _ = t;
    }
  }

  [Serializable]
  public class VolumeParameter<T> : VolumeParameter
  {
    [SerializeField]
    protected T m_Value;

    public T value
    {
      get => m_Value;
      set => m_Value = value;
    }

    public VolumeParameter() { }

    public VolumeParameter(T value, bool overrideState = false)
    {
      m_Value = value;
      this.overrideState = overrideState;
    }

    public static implicit operator T(VolumeParameter<T> parameter)
    {
      return parameter.m_Value;
    }

    public virtual void Interp(T from, T to, float t)
    {
      m_Value = t > 0f ? to : from;
    }

    public void Override(T x)
    {
      m_Value = x;
      m_OverrideState = true;
    }
  }

  [Serializable]
  public sealed class MinFloatParameter : VolumeParameter<float>
  {
    public float min;

    public MinFloatParameter(float value, float min, bool overrideState = false)
      : base(value, overrideState)
    {
      this.min = min;
    }
  }

  [Serializable]
  public sealed class MaxFloatParameter : VolumeParameter<float>
  {
    public float max;

    public MaxFloatParameter(float value, float max, bool overrideState = false)
      : base(value, overrideState)
    {
      this.max = max;
    }
  }

  [Serializable]
  public sealed class ClampedFloatParameter : VolumeParameter<float>
  {
    public float min;
    public float max;

    public ClampedFloatParameter(float value, float min, float max, bool overrideState = false)
      : base(value, overrideState)
    {
      this.min = min;
      this.max = max;
    }
  }

  [Serializable]
  public sealed class FloatParameter : VolumeParameter<float>
  {
    public FloatParameter(float value, bool overrideState = false)
      : base(value, overrideState)
    {
    }
  }

  [Serializable]
  public sealed class IntParameter : VolumeParameter<int>
  {
    public IntParameter(int value, bool overrideState = false)
      : base(value, overrideState)
    {
    }
  }

  [Serializable]
  public sealed class BoolParameter : VolumeParameter<bool>
  {
    public BoolParameter(bool value, bool overrideState = false)
      : base(value, overrideState)
    {
    }
  }

  [Serializable]
  public sealed class ColorParameter : VolumeParameter<Color>
  {
    public bool showAlpha;
    public bool hdr;
    public bool showEyeDropper;

    public ColorParameter(Color value, bool showAlpha = true, bool hdr = false, bool showEyeDropper = true, bool overrideState = false)
      : base(value, overrideState)
    {
      this.showAlpha = showAlpha;
      this.hdr = hdr;
      this.showEyeDropper = showEyeDropper;
    }
  }

  [Serializable]
  public sealed class Vector2Parameter : VolumeParameter<Vector2>
  {
    public Vector2Parameter(Vector2 value, bool overrideState = false)
      : base(value, overrideState)
    {
    }
  }

  [Serializable]
  public sealed class Vector3Parameter : VolumeParameter<Vector3>
  {
    public Vector3Parameter(Vector3 value, bool overrideState = false)
      : base(value, overrideState)
    {
    }
  }

  [Serializable]
  public sealed class Vector4Parameter : VolumeParameter<Vector4>
  {
    public Vector4Parameter(Vector4 value, bool overrideState = false)
      : base(value, overrideState)
    {
    }
  }

  [Serializable]
  public sealed class TextureParameter : VolumeParameter<Texture>
  {
    public TextureParameter(Texture value, bool overrideState = false)
      : base(value, overrideState)
    {
    }
  }

  [Serializable]
  public sealed class Texture2DParameter : VolumeParameter<Texture2D>
  {
    public Texture2DParameter(Texture2D value, bool overrideState = false)
      : base(value, overrideState)
    {
    }
  }

  [Serializable]
  public sealed class CubemapParameter : VolumeParameter<Cubemap>
  {
    public CubemapParameter(Cubemap value, bool overrideState = false)
      : base(value, overrideState)
    {
    }
  }

  [Serializable]
  public sealed class AnimationCurveParameter : VolumeParameter<AnimationCurve>
  {
    public AnimationCurveParameter(AnimationCurve value, bool overrideState = false)
      : base(value, overrideState)
    {
    }
  }

  [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
  public class VolumeComponentMenuAttribute : Attribute
  {
    public string menu { get; }

    public VolumeComponentMenuAttribute(string menu)
    {
      this.menu = menu ?? string.Empty;
    }
  }

  public sealed class VolumeStack : IDisposable
  {
    private readonly Dictionary<Type, VolumeComponent> m_Components = new();

    public T Get<T>() where T : VolumeComponent
    {
      if (m_Components.TryGetValue(typeof(T), out var comp))
        return comp as T;
      return null;
    }

    public bool TryGet<T>(out T component) where T : VolumeComponent
    {
      component = Get<T>();
      return component != null;
    }

    internal bool TryGet(Type type, out VolumeComponent component)
    {
      return m_Components.TryGetValue(type, out component);
    }

    public void Add(Type type, VolumeComponent comp)
    {
      if (type != null && comp != null)
        m_Components[type] = comp;
    }

    public void Add(VolumeComponent comp)
    {
      if (comp != null)
        m_Components[comp.GetType()] = comp;
    }

    internal void Clear()
    {
      m_Components.Clear();
    }

    void IDisposable.Dispose()
    {
      Clear();
    }
  }

  public sealed class VolumeManager
  {
    private static readonly Lazy<VolumeManager> s_Instance = new(() => new VolumeManager());
    public static VolumeManager instance => s_Instance.Value;

    private VolumeStack m_Stack;
    private readonly List<Volume> m_Volumes = new();
    private readonly List<VolumeStack> m_Stacks = new();
    private VolumeStack m_DefaultStack;

    public VolumeStack stack
    {
      get
      {
        if (m_Stack == null)
          m_Stack = CreateStack();
        return m_Stack;
      }
    }

    public List<Volume> volumes => m_Volumes;

    public VolumeManager()
    {
      m_DefaultStack = CreateStack();
    }

    public VolumeStack CreateStack()
    {
      var s = new VolumeStack();
      m_Stacks.Add(s);
      return s;
    }

    public void DestroyStack(VolumeStack stack)
    {
      if (stack == null) return;
      m_Stacks.Remove(stack);
      if (m_Stack == stack) m_Stack = null;
      if (m_DefaultStack == stack) m_DefaultStack = null;
      (stack as IDisposable)?.Dispose();
    }

    public void Register(Volume volume)
    {
      if (!m_Volumes.Contains(volume))
        m_Volumes.Add(volume);
    }

    public void Unregister(Volume volume)
    {
      m_Volumes.Remove(volume);
    }

    public void Update(Transform trigger, LayerMask layerMask)
    {
      UpdateStack(stack, trigger, layerMask);
    }

    public void Update(Transform trigger)
    {
      UpdateStack(stack, trigger, -1);
    }

    private void UpdateStack(VolumeStack stack, Transform trigger, LayerMask layerMask)
    {
      stack.Clear();

      var sortedVolumes = new List<Volume>(m_Volumes);
      sortedVolumes.Sort((a, b) => b.priority.CompareTo(a.priority));

      for (var i = 0; i < sortedVolumes.Count; i++)
      {
        var vol = sortedVolumes[i];
        if (vol == null || !vol.enabled || !vol.gameObject.activeInHierarchy) continue;
        if (!vol.isGlobal && trigger != null)
        {
          if ((vol.layerMask & layerMask) == 0) continue;
          var dist = Vector3.Distance(trigger.position, vol.transform.position);
          if (dist > vol.blendDistance + vol.GetComponent<Collider>()?.bounds.extents.magnitude) continue;
        }

        var weight = vol.weight;
        if (weight <= 0f) continue;

        foreach (var comp in vol.components)
        {
          if (comp == null || !comp.active) continue;
          var type = comp.GetType();
          if (!stack.TryGet(type, out var existing))
          {
            stack.Add(type, comp);
          }
        }

        if (vol.profile != null)
        {
          foreach (var comp in vol.profile.components)
          {
            if (comp == null || !comp.active) continue;
            var type = comp.GetType();
            if (!stack.TryGet(type, out var existing))
            {
              stack.Add(type, comp);
            }
          }
        }
      }
    }
  }
}

// AnimationCurve / Keyframe live in UnityEngine (UnityCompat/Runtime) — do not redefine here.

