using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;
using Bindings = UnityEngine.Bindings;

namespace UnityEditor;

/// <summary>Identifies an animation curve in an editor clip.</summary>
[Bindings.NativeType(1, "MonoEditorCurveBinding")]
public struct EditorCurveBinding : IEquatable<EditorCurveBinding>
{
    private const int PPtr = 1;
    private const int Discrete = 2;
    private const int SerializeReference = 4;

    private int _curveKind;
    private long _referenceId;
    private Type? _type;

    public string path;
    public string propertyName;

    public Type? type
    {
        get => _type;
        set => _type = value;
    }

    public bool isDiscreteCurve => (_curveKind & Discrete) != 0;
    public bool isPPtrCurve => (_curveKind & PPtr) != 0;
    public bool isSerializeReferenceCurve => (_curveKind & SerializeReference) != 0;

    public static EditorCurveBinding FloatCurve(string inPath, Type inType, string inPropertyName)
        => Create(inPath, inType, inPropertyName, 0, 0);

    public static EditorCurveBinding PPtrCurve(string inPath, Type inType, string inPropertyName)
        => Create(inPath, inType, inPropertyName, PPtr, 0);

    public static EditorCurveBinding DiscreteCurve(string inPath, Type inType, string inPropertyName)
        => Create(inPath, inType, inPropertyName, Discrete, 0);

    public static EditorCurveBinding SerializeReferenceCurve(string inPath, Type inType, long refID, string inPropertyName, bool isPPtr, bool isDiscrete)
    {
        var kind = SerializeReference;
        if (isPPtr) kind |= PPtr;
        if (isDiscrete) kind |= Discrete;
        return Create(inPath, inType, inPropertyName, kind, refID);
    }

    public bool Equals(EditorCurveBinding other)
        => string.Equals(path, other.path, StringComparison.Ordinal)
           && string.Equals(propertyName, other.propertyName, StringComparison.Ordinal)
           && _type == other._type
           && _curveKind == other._curveKind
           && _referenceId == other._referenceId;

    public override bool Equals(object? other) => other is EditorCurveBinding binding && Equals(binding);
    public override int GetHashCode() => HashCode.Combine(path, propertyName, _type, _curveKind, _referenceId);
    public static bool operator ==(EditorCurveBinding lhs, EditorCurveBinding rhs) => lhs.Equals(rhs);
    public static bool operator !=(EditorCurveBinding lhs, EditorCurveBinding rhs) => !lhs.Equals(rhs);

    private static EditorCurveBinding Create(string inPath, Type inType, string inPropertyName, int kind, long referenceId)
        => new()
        {
            path = inPath ?? string.Empty,
            _type = inType,
            propertyName = inPropertyName ?? string.Empty,
            _curveKind = kind,
            _referenceId = referenceId
        };
}

[Serializable]
[Bindings.NativeAsStruct]
[UnityEngine.Scripting.RequiredByNativeCode]
public sealed class PropertyModification
{
    public Object? target;
    public string? propertyPath;
    public string? value;
    public Object? objectReference;
}

/// <summary>Identifies an independent editor animation preview session.</summary>
public class AnimationModeDriver : ScriptableObject
{
}

/// <summary>
/// Unity-compatible editor preview mode. Sampling is transactional: transforms and supported
/// serialized fields changed while previewing are restored when their animation mode stops.
/// </summary>
[Bindings.NativeHeader("Editor/Src/Animation/AnimationMode.bindings.h")]
[Bindings.NativeHeader("Editor/Src/Animation/EditorCurveBinding.bindings.h")]
[Bindings.NativeHeader("Editor/Src/Prefabs/PropertyModification.h")]
public class AnimationMode
{
    private static readonly object Gate = new();
    private static readonly AnimationModeDriver DefaultDriver = new();
    private static readonly Dictionary<AnimationModeDriver, PreviewSession> Sessions = new();
    private static int _samplingDepth;

    private static readonly Color AnimatedColor = new(1f, 0.6f, 0.6f, 1f);
    private static readonly Color CandidateColor = new(1f, 0.85f, 0.35f, 1f);
    private static readonly Color RecordedColor = new(1f, 0.2f, 0.2f, 1f);

    public static Color animatedPropertyColor => AnimatedColor;
    public static Color candidatePropertyColor => CandidateColor;
    public static Color recordedPropertyColor => RecordedColor;

    public static void StartAnimationMode() => StartAnimationMode(DefaultDriver);

    public static void StartAnimationMode(AnimationModeDriver driver)
    {
        if (driver == null) throw new ArgumentNullException(nameof(driver));
        lock (Gate)
            _ = GetOrCreateSession(driver);
    }

    public static void StopAnimationMode() => StopAnimationMode(DefaultDriver);

    public static void StopAnimationMode(AnimationModeDriver driver)
    {
        if (driver == null) throw new ArgumentNullException(nameof(driver));
        PreviewSession? session;
        lock (Gate)
        {
            if (!Sessions.Remove(driver, out session))
                return;
        }
        session.Restore();
    }

    public static bool InAnimationMode() { lock (Gate) return Sessions.Count != 0; }
    public static bool InAnimationMode(AnimationModeDriver driver)
    {
        if (driver == null) return false;
        lock (Gate) return Sessions.ContainsKey(driver);
    }

    [Bindings.NativeThrows]
    public static void BeginSampling()
    {
        if (!InAnimationMode())
            throw new InvalidOperationException("AnimationMode.BeginSampling requires an active animation mode.");
        lock (Gate) _samplingDepth++;
    }

    [Bindings.NativeThrows]
    public static void EndSampling()
    {
        lock (Gate)
        {
            if (_samplingDepth == 0)
                throw new InvalidOperationException("AnimationMode.EndSampling was called without BeginSampling.");
            _samplingDepth--;
        }
    }

    [Bindings.NativeThrows]
    public static void SampleAnimationClip([Bindings.NotNull("ArgumentNullException")] GameObject gameObject, [Bindings.NotNull("ArgumentNullException")] AnimationClip clip, float time)
    {
        if (gameObject == null) throw new ArgumentNullException(nameof(gameObject));
        if (clip == null) throw new ArgumentNullException(nameof(clip));
        CaptureTransformTree(gameObject);
        clip.SampleAnimation(gameObject, time);
    }

    [Bindings.NativeThrows]
    public static void SamplePlayableGraph(PlayableGraph graph, int index, float time)
    {
        if (graph == null) throw new ArgumentNullException(nameof(graph));
        if (!graph.IsValid()) throw new InvalidOperationException("Cannot sample an invalid PlayableGraph.");
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        graph.SetTime(time);
        graph.Evaluate(0f);
    }

    [Bindings.NativeThrows]
    public static void AddEditorCurveBinding([Bindings.NotNull("ArgumentNullException")] GameObject gameObject, EditorCurveBinding binding)
    {
        if (gameObject == null) throw new ArgumentNullException(nameof(gameObject));
        var session = RequireSession();
        session.Bindings.Add((gameObject, binding));
        CaptureTransformTree(gameObject);
    }

    [Bindings.NativeThrows]
    public static void AddPropertyModification(EditorCurveBinding binding, PropertyModification modification, bool keepPrefabOverride)
    {
        if (modification == null) throw new ArgumentNullException(nameof(modification));
        if (modification.target == null) throw new ArgumentException("PropertyModification.target is required.", nameof(modification));

        var session = RequireSession();
        session.AnimatedProperties.Add((modification.target, modification.propertyPath ?? binding.propertyName));
        session.CaptureObject(modification.target);
        ApplyModification(modification);
        _ = keepPrefabOverride; // Prefab override ownership is tracked by PrefabUtility in this managed editor.
    }

    public static bool IsPropertyAnimated(Object target, string propertyPath)
    {
        if (target == null || propertyPath == null) return false;
        lock (Gate)
        {
            foreach (var session in Sessions.Values)
            {
                if (session.IsAnimated(target, propertyPath))
                    return true;
            }
        }
        return false;
    }

    private static PreviewSession RequireSession()
    {
        lock (Gate)
        {
            if (Sessions.TryGetValue(DefaultDriver, out var defaultSession))
                return defaultSession;
            if (Sessions.Count == 1)
                return new List<PreviewSession>(Sessions.Values)[0];
        }
        throw new InvalidOperationException("AnimationMode operation requires an active animation mode.");
    }

    private static PreviewSession GetOrCreateSession(AnimationModeDriver driver)
    {
        if (!Sessions.TryGetValue(driver, out var session))
        {
            session = new PreviewSession();
            Sessions.Add(driver, session);
        }
        return session;
    }

    private static void CaptureTransformTree(GameObject gameObject)
    {
        lock (Gate)
        {
            foreach (var session in Sessions.Values)
                session.CaptureTransformTree(gameObject.transform);
        }
    }

    private static void ApplyModification(PropertyModification modification)
    {
        if (modification.objectReference != null)
        {
            SetMember(modification.target!, modification.propertyPath, modification.objectReference);
            return;
        }

        if (string.IsNullOrEmpty(modification.propertyPath))
            return;

        if (modification.target is Transform transform && TryApplyTransformComponent(transform, modification.propertyPath, modification.value))
            return;

        SetMember(modification.target!, modification.propertyPath, modification.value);
    }

    private static bool TryApplyTransformComponent(Transform transform, string propertyPath, string? text)
    {
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float component))
            return false;

        var (property, axis) = propertyPath.LastIndexOf('.') is var dot && dot >= 0
            ? (propertyPath[..dot], propertyPath[(dot + 1)..])
            : (string.Empty, string.Empty);
        if (axis is not ("x" or "y" or "z" or "w")) return false;

        if (property.Contains("Position", StringComparison.OrdinalIgnoreCase))
        {
            var value = transform.localPosition;
            if (axis == "x") value.x = component; else if (axis == "y") value.y = component; else if (axis == "z") value.z = component; else return false;
            transform.localPosition = value;
            return true;
        }
        if (property.Contains("Scale", StringComparison.OrdinalIgnoreCase))
        {
            var value = transform.localScale;
            if (axis == "x") value.x = component; else if (axis == "y") value.y = component; else if (axis == "z") value.z = component; else return false;
            transform.localScale = value;
            return true;
        }
        if (property.Contains("Rotation", StringComparison.OrdinalIgnoreCase))
        {
            var value = transform.localRotation;
            if (axis == "x") value.x = component; else if (axis == "y") value.y = component; else if (axis == "z") value.z = component; else value.w = component;
            transform.localRotation = value;
            return true;
        }
        return false;
    }

    private static void SetMember(Object target, string? propertyPath, object? value)
    {
        if (string.IsNullOrWhiteSpace(propertyPath)) return;
        string memberName = propertyPath.Contains('.') ? propertyPath[(propertyPath.LastIndexOf('.') + 1)..] : propertyPath;
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var property = target.GetType().GetProperty(memberName, flags);
        if (property?.CanWrite == true)
        {
            property.SetValue(target, ConvertValue(value, property.PropertyType));
            return;
        }
        var field = target.GetType().GetField(memberName, flags);
        if (field != null)
            field.SetValue(target, ConvertValue(value, field.FieldType));
    }

    private static object? ConvertValue(object? value, Type destination)
    {
        if (value == null || destination.IsInstanceOfType(value)) return value;
        if (value is string text)
        {
            if (destination == typeof(string)) return text;
            if (destination == typeof(float)) return float.Parse(text, CultureInfo.InvariantCulture);
            if (destination == typeof(int)) return int.Parse(text, CultureInfo.InvariantCulture);
            if (destination == typeof(bool)) return bool.Parse(text);
        }
        return Convert.ChangeType(value, destination, CultureInfo.InvariantCulture);
    }

    private sealed class PreviewSession
    {
        private readonly Dictionary<Transform, TransformSnapshot> _transforms = new();
        private readonly Dictionary<Object, ObjectSnapshot> _objects = new();
        public readonly List<(GameObject GameObject, EditorCurveBinding Binding)> Bindings = new();
        public readonly List<(Object Target, string PropertyPath)> AnimatedProperties = new();

        public void CaptureTransformTree(Transform transform)
        {
            CaptureTransform(transform);
            for (int index = 0; index < transform.childCount; index++)
                CaptureTransformTree(transform.GetChild(index));
        }

        public void CaptureObject(Object target)
        {
            if (target is Transform transform)
            {
                CaptureTransform(transform);
                return;
            }
            if (!_objects.ContainsKey(target))
                _objects.Add(target, ObjectSnapshot.Create(target));
        }

        public bool IsAnimated(Object target, string propertyPath)
        {
            foreach (var entry in AnimatedProperties)
            {
                if (ReferenceEquals(entry.Target, target) && string.Equals(entry.PropertyPath, propertyPath, StringComparison.Ordinal))
                    return true;
            }
            foreach (var entry in Bindings)
            {
                if (ReferenceEquals(entry.GameObject, target) && string.Equals(entry.Binding.propertyName, propertyPath, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public void Restore()
        {
            foreach (var pair in _transforms)
                pair.Value.Restore(pair.Key);
            foreach (var pair in _objects)
                pair.Value.Restore(pair.Key);
        }

        private void CaptureTransform(Transform transform)
        {
            if (!_transforms.ContainsKey(transform))
                _transforms.Add(transform, new TransformSnapshot(transform));
        }
    }

    private readonly struct TransformSnapshot
    {
        private readonly Vector3 _position;
        private readonly Quaternion _rotation;
        private readonly Vector3 _scale;
        public TransformSnapshot(Transform transform)
        {
            _position = transform.localPosition;
            _rotation = transform.localRotation;
            _scale = transform.localScale;
        }
        public void Restore(Transform transform)
        {
            transform.localPosition = _position;
            transform.localRotation = _rotation;
            transform.localScale = _scale;
        }
    }

    private sealed class ObjectSnapshot
    {
        private readonly Dictionary<FieldInfo, object?> _fields;
        private ObjectSnapshot(Dictionary<FieldInfo, object?> fields) => _fields = fields;
        public static ObjectSnapshot Create(Object target)
        {
            var fields = new Dictionary<FieldInfo, object?>();
            foreach (var field in target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (!field.IsInitOnly) fields.Add(field, field.GetValue(target));
            return new ObjectSnapshot(fields);
        }
        public void Restore(Object target)
        {
            foreach (var pair in _fields)
                pair.Key.SetValue(target, pair.Value);
        }
    }
}
