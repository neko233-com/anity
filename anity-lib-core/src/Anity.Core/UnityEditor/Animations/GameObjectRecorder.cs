using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Bindings = UnityEngine.Bindings;
using EditorBrowsableAttribute = System.ComponentModel.EditorBrowsableAttribute;
using EditorBrowsableState = System.ComponentModel.EditorBrowsableState;

namespace UnityEditor.Animations;

public struct CurveFilterOptions
{
    public float floatError;
    public bool keyframeReduction;
    public float positionError;
    public float rotationError;
    public float scaleError;
    public bool unrollRotation;
}

[Bindings.NativeHeader("Editor/Src/Animation/EditorCurveBinding.bindings.h")]
[Bindings.NativeHeader("Editor/Src/Animation/GameObjectRecorder.h")]
[Bindings.NativeHeader("Modules/Animation/AnimationClip.h")]
[Bindings.NativeType]
public class GameObjectRecorder : Object
{
    private readonly HashSet<EditorCurveBinding> _bindings = new();
    private readonly Dictionary<EditorCurveBinding, List<Keyframe>> _samples = new();
    private GameObject? _root;
    private float _currentTime;

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("The GameObjectRecorder constructor now takes a root GameObject", true)]
    public GameObjectRecorder() { }
    public GameObjectRecorder(GameObject root) => _root = root ?? throw new ArgumentNullException(nameof(root));
    public GameObject? root => _root;
    public float currentTime => _currentTime;
    public bool isRecording => _root != null && _bindings.Count > 0;

    public void Bind(EditorCurveBinding binding)
    {
        if (binding.type == null || string.IsNullOrEmpty(binding.propertyName)) throw new ArgumentException("Binding must have a type and property name.", nameof(binding));
        _bindings.Add(binding);
    }

    public void BindAll(GameObject target, bool recursive)
    {
        EnsureRoot(target);
        BindTransform(target);
        foreach (var component in target.GetComponents<Component>()) BindComponent(component);
        if (recursive) ForEachChild(target.transform, child => BindAll(child.gameObject, false));
    }

    public void BindComponent([Bindings.NotNull("ArgumentNullException")] Component component)
    {
        if (component == null) throw new ArgumentNullException(nameof(component));
        EnsureRoot(component.gameObject);
        BindComponentProperties(component.gameObject, component.GetType());
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("\"BindComponent<T>() where T : Component\" is obsolete, use BindComponentsOfType<T>() instead (UnityUpgradable) -> BindComponentsOfType<T>(*)", true)]
    public void BindComponent<T>(GameObject target, bool recursive) where T : Component => BindComponentsOfType<T>(target, recursive);

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("BindComponent() using a System::Type is obsolete, use BindComponentsOfType() instead (UnityUpgradable) -> BindComponentsOfType(*)", true)]
    public void BindComponent(GameObject target, Type componentType, bool recursive)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (componentType == null) throw new ArgumentNullException(nameof(componentType));
        EnsureRoot(target);
        if (componentType == typeof(Component))
            foreach (var component in target.GetComponents<Component>()) BindComponentProperties(target, component.GetType());
        else if (typeof(Component).IsAssignableFrom(componentType) && target.GetComponent(componentType) != null)
            BindComponentProperties(target, componentType);
        if (recursive) ForEachChild(target.transform, child => BindComponent(child.gameObject, componentType, false));
    }

    public void BindComponentsOfType<T>(GameObject target, bool recursive) where T : Component
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        EnsureRoot(target);
        foreach (var component in target.GetComponents<T>()) BindComponentProperties(target, component.GetType());
        if (recursive) ForEachChild(target.transform, child => BindComponentsOfType<T>(child.gameObject, false));
    }

    public void BindComponentsOfType(GameObject target, Type componentType, bool recursive)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (componentType == null) throw new ArgumentNullException(nameof(componentType));
        if (!typeof(Component).IsAssignableFrom(componentType)) throw new ArgumentException("Type must inherit Component.", nameof(componentType));
        EnsureRoot(target);
        foreach (var component in target.GetComponents<Component>())
            if (componentType.IsAssignableFrom(component.GetType())) BindComponentProperties(target, component.GetType());
        if (recursive) ForEachChild(target.transform, child => BindComponentsOfType(child.gameObject, componentType, false));
    }
    public EditorCurveBinding[] GetBindings() => _bindings.OrderBy(binding => binding.path).ThenBy(binding => binding.type?.FullName).ThenBy(binding => binding.propertyName).ToArray();

    public void TakeSnapshot(float dt)
    {
        if (_root == null) throw new InvalidOperationException("A root GameObject is required before recording.");
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
        foreach (var binding in _bindings)
        {
            if (!AnimationUtility.GetFloatValue(_root, binding, out var value)) continue;
            if (!_samples.TryGetValue(binding, out var samples)) _samples[binding] = samples = new();
            samples.Add(new Keyframe(_currentTime, value));
        }
        _currentTime += dt;
    }

    public void ResetRecording()
    {
        _samples.Clear();
        _currentTime = 0f;
    }

    public void SaveToClip(AnimationClip clip) => SaveToClip(clip, 60f, default);
    public void SaveToClip(AnimationClip clip, float fps) => SaveToClip(clip, fps, default);
    public void SaveToClip(AnimationClip clip, float fps, CurveFilterOptions filterOptions)
    {
        if (clip == null) throw new ArgumentNullException(nameof(clip));
        if (float.IsNaN(fps) || float.IsInfinity(fps) || fps <= 0f) throw new ArgumentOutOfRangeException(nameof(fps));
        foreach (var (binding, samples) in _samples)
        {
            var keys = Reduce(samples, filterOptions);
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
        }
    }

    private void EnsureRoot(GameObject target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        _root ??= target;
    }

    private void BindTransform(GameObject target)
    {
        var path = AnimationUtility.CalculateTransformPath(target.transform, _root!.transform);
        foreach (var property in TransformProperties) Bind(EditorCurveBinding.FloatCurve(path, typeof(Transform), property));
    }

    private void BindComponentProperties(GameObject target, Type componentType)
    {
        if (componentType == typeof(Transform)) { BindTransform(target); return; }
        var path = AnimationUtility.CalculateTransformPath(target.transform, _root!.transform);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var property in componentType.GetProperties(flags))
            if (property.CanRead && IsCurveValue(property.PropertyType)) Bind(EditorCurveBinding.FloatCurve(path, componentType, property.Name));
        foreach (var field in componentType.GetFields(flags))
            if (IsCurveValue(field.FieldType)) Bind(EditorCurveBinding.FloatCurve(path, componentType, field.Name));
    }

    private static List<Keyframe> Reduce(List<Keyframe> samples, CurveFilterOptions options)
    {
        if (!options.keyframeReduction || samples.Count < 3) return new List<Keyframe>(samples);
        var result = new List<Keyframe> { samples[0] };
        var tolerance = MathF.Max(0f, options.floatError);
        for (var index = 1; index < samples.Count - 1; index++)
        {
            var previous = result[^1]; var current = samples[index]; var next = samples[index + 1];
            var span = next.time - previous.time;
            var interpolated = MathF.Abs(span) < 1e-6f ? previous.value : previous.value + (next.value - previous.value) * ((current.time - previous.time) / span);
            if (MathF.Abs(current.value - interpolated) > tolerance) result.Add(current);
        }
        result.Add(samples[^1]);
        return result;
    }

    private static bool IsCurveValue(Type type) => type == typeof(float) || type == typeof(int) || type == typeof(bool);
    private static void ForEachChild(Transform transform, Action<Transform> action) { for (var index = 0; index < transform.childCount; index++) action(transform.GetChild(index)); }
    private static readonly string[] TransformProperties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
}
