using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Bindings = UnityEngine.Bindings;

namespace UnityEditor;

public class AnimationClipCurveData
{
    public AnimationClipCurveData() { }
    public AnimationClipCurveData(EditorCurveBinding binding)
    {
        path = binding.path;
        type = binding.type;
        propertyName = binding.propertyName;
    }
    public string? path;
    public Type? type;
    public string? propertyName;
    public AnimationCurve? curve;
}

[Bindings.NativeAsStruct]
[Bindings.NativeType(1, "MonoAnimationClipSettings")]
[UnityEngine.Scripting.RequiredByNativeCode]
public class AnimationClipSettings
{
    public bool loopTime;
    public bool loopBlend;
    public bool loopBlendOrientation;
    public bool loopBlendPositionY;
    public bool loopBlendPositionXZ;
    public bool keepOriginalOrientation;
    public bool keepOriginalPositionY;
    public bool keepOriginalPositionXZ;
    public bool heightFromFeet;
    public bool mirror;
    public float cycleOffset;
    public float additiveReferencePoseTime;
    public float startTime;
    public float stopTime = 1f;
    public float orientationOffsetY;
    public float level;
    public bool hasAdditiveReferencePose;
    public AnimationClip? additiveReferencePoseClip;

    internal AnimationClipSettings Clone() => (AnimationClipSettings)MemberwiseClone();
}

public struct ObjectReferenceKeyframe
{
    public float time;
    public Object? value;
}

[Bindings.NativeHeader("Editor/Src/Animation/AnimationUtility.bindings.h")]
public class AnimationUtility
{
    public enum TangentMode { Free = 0, Auto = 1, Linear = 2, Constant = 3, ClampedAuto = 4 }
    public enum CurveModifiedType { CurveDeleted = 0, CurveModified = 1, ClipModified = 2 }
    public delegate void OnCurveWasModified(AnimationClip clip, EditorCurveBinding binding, CurveModifiedType type);

    public static OnCurveWasModified? onCurveWasModified;

    private static readonly ConditionalWeakTable<AnimationClip, ClipEditorData> ClipData = new();
    private static readonly ConditionalWeakTable<AnimationCurve, CurveEditorData> CurveData = new();

    public static string CalculateTransformPath([Bindings.NotNull("ArgumentNullException")] Transform targetTransform, Transform root)
    {
        if (targetTransform == null) throw new ArgumentNullException(nameof(targetTransform));
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (ReferenceEquals(targetTransform, root)) return string.Empty;
        var parts = new Stack<string>();
        Transform? current = targetTransform;
        for (; current != null && !ReferenceEquals(current, root); current = current.parent)
            parts.Push(current.gameObject?.name ?? string.Empty);
        return current == null ? string.Empty : string.Join("/", parts);
    }

    public static void ConstrainToPolynomialCurve(AnimationCurve curve)
    {
        if (curve == null) throw new ArgumentNullException(nameof(curve));
        for (var index = 0; index < curve.length; index++)
            curve.SmoothTangents(index, 1f);
    }

    [Obsolete("GetAllCurves is deprecated. Use GetCurveBindings and GetObjectReferenceCurveBindings instead.")]
    public static AnimationClipCurveData[] GetAllCurves(AnimationClip clip) => GetAllCurves(clip, true);
    [Obsolete("GetAllCurves is deprecated. Use GetCurveBindings and GetObjectReferenceCurveBindings instead.")]
    public static AnimationClipCurveData[] GetAllCurves(AnimationClip clip, [UnityEngine.Internal.DefaultValue("true")] bool includeCurveData)
    {
        if (clip == null) throw new ArgumentNullException(nameof(clip));
        var result = new List<AnimationClipCurveData>();
        foreach (var item in clip.bindings)
            result.Add(new AnimationClipCurveData { path = item.path, type = item.type, propertyName = item.propertyName, curve = includeCurveData ? item.curve : null });
        return result.ToArray();
    }

    public static EditorCurveBinding[] GetAnimatableBindings(GameObject targetObject, GameObject root)
    {
        if (targetObject == null) throw new ArgumentNullException(nameof(targetObject));
        if (root == null) throw new ArgumentNullException(nameof(root));
        var result = new List<EditorCurveBinding>();
        AddTransformBindings(result, targetObject.transform, CalculateTransformPath(targetObject.transform, root.transform));
        return result.ToArray();
    }

    public static Object? GetAnimatedObject([Bindings.NotNull("ArgumentNullException")] GameObject root, EditorCurveBinding binding)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        Transform target = string.IsNullOrEmpty(binding.path) ? root.transform : root.transform.Find(binding.path);
        if (target == null) return null;
        if (binding.type == null || binding.type == typeof(Transform)) return target;
        if (binding.type == typeof(GameObject)) return target.gameObject;
        return target.gameObject?.GetComponent(binding.type);
    }

    [Obsolete("GetAnimationClips(Animation) is deprecated. Use GetAnimationClips(GameObject) instead.")]
    public static AnimationClip[] GetAnimationClips(Animation component) => component?.GetClips() ?? Array.Empty<AnimationClip>();
    public static AnimationClip[] GetAnimationClips(GameObject gameObject) => gameObject?.GetComponent<Animation>()?.GetClips() ?? Array.Empty<AnimationClip>();
    public static AnimationClipSettings GetAnimationClipSettings([Bindings.NotNull("ArgumentNullException")] AnimationClip clip) => GetData(clip).Settings.Clone();
    public static AnimationEvent[] GetAnimationEvents([Bindings.NotNull("ArgumentNullException")] AnimationClip clip) => clip?.events ?? Array.Empty<AnimationEvent>();
    public static EditorCurveBinding[] GetCurveBindings([Bindings.NotNull("ArgumentNullException")] AnimationClip clip)
    {
        if (clip == null) throw new ArgumentNullException(nameof(clip));
        var list = new List<EditorCurveBinding>();
        foreach (var item in clip.bindings) list.Add(EditorCurveBinding.FloatCurve(item.path, item.type!, item.propertyName));
        return list.ToArray();
    }

    public static bool GetDiscreteIntValue([Bindings.NotNull("ArgumentNullException")] GameObject root, EditorCurveBinding binding, out int data)
    {
        if (GetFloatValue(root, binding, out var value)) { data = (int)value; return true; }
        data = default; return false;
    }
    [Obsolete("This overload is deprecated. Use the one with EditorCurveBinding instead.")]
    public static AnimationCurve? GetEditorCurve(AnimationClip clip, string relativePath, Type type, string propertyName) => clip?.GetCurve(relativePath, type, propertyName);
    public static AnimationCurve? GetEditorCurve([Bindings.NotNull("ArgumentNullException")] AnimationClip clip, EditorCurveBinding binding) => GetEditorCurve(clip, binding.path, binding.type!, binding.propertyName);
    public static Type? GetEditorCurveValueType(GameObject root, EditorCurveBinding binding)
    {
        var target = GetAnimatedObject(root, binding);
        if (target is Renderer && string.Equals(binding.propertyName, "m_Enabled", StringComparison.Ordinal))
            return typeof(bool);
        return GetMemberType(target, binding.propertyName);
    }
    [Obsolete("This overload is deprecated. Use the one with EditorCurveBinding instead.")]
    public static bool GetFloatValue(GameObject root, string relativePath, Type type, string propertyName, out float data)
        => GetFloatValue(root, EditorCurveBinding.FloatCurve(relativePath, type, propertyName), out data);
    public static bool GetFloatValue([Bindings.NotNull("ArgumentNullException")] GameObject root, EditorCurveBinding binding, out float data)
    {
        data = default;
        var target = GetAnimatedObject(root, binding);
        if (target is Transform transform && TryGetTransformFloat(transform, binding.propertyName, out data)) return true;
        if (target is Renderer renderer && string.Equals(binding.propertyName, "m_Enabled", StringComparison.Ordinal))
        {
            data = renderer.enabled ? 1f : 0f;
            return true;
        }
        object? value = GetMember(target, binding.propertyName);
        if (value is float single) { data = single; return true; }
        if (value is int integer) { data = integer; return true; }
        if (value is bool boolean) { data = boolean ? 1f : 0f; return true; }
        return false;
    }
    [Obsolete("This is not used anymore.  Root motion curves are automatically generated if applyRootMotion is enabled on Animator component.")]
    public static bool GetGenerateMotionCurves(AnimationClip clip) => GetData(clip).GenerateMotionCurves;
    [Bindings.NativeThrows]
    public static bool GetKeyBroken([Bindings.NotNull("ArgumentNullException")] AnimationCurve curve, int index) => GetCurveData(curve).Broken.Contains(index);
    [Bindings.NativeThrows, Bindings.ThreadSafe]
    public static TangentMode GetKeyLeftTangentMode([Bindings.NotNull("ArgumentNullException")] AnimationCurve curve, int index) => GetCurveData(curve).Left.TryGetValue(index, out var mode) ? mode : TangentMode.Free;
    [Bindings.NativeThrows, Bindings.ThreadSafe]
    public static TangentMode GetKeyRightTangentMode([Bindings.NotNull("ArgumentNullException")] AnimationCurve curve, int index) => GetCurveData(curve).Right.TryGetValue(index, out var mode) ? mode : TangentMode.Free;
    public static ObjectReferenceKeyframe[] GetObjectReferenceCurve([Bindings.NotNull("ArgumentNullException")] AnimationClip clip, EditorCurveBinding binding)
        => GetData(clip).ObjectCurves.TryGetValue(binding, out var keys) ? (ObjectReferenceKeyframe[])keys.Clone() : Array.Empty<ObjectReferenceKeyframe>();
    public static EditorCurveBinding[] GetObjectReferenceCurveBindings([Bindings.NotNull("ArgumentNullException")] AnimationClip clip)
    {
        var data = GetData(clip); var result = new EditorCurveBinding[data.ObjectCurves.Count]; data.ObjectCurves.Keys.CopyTo(result, 0); return result;
    }
    public static bool GetObjectReferenceValue(GameObject root, EditorCurveBinding binding, out Object? data)
    {
        data = GetMember(GetAnimatedObject(root, binding), binding.propertyName) as Object;
        return data != null;
    }
    [Obsolete("Use AnimationMode.InAnimationMode instead.")]
    public static bool InAnimationMode() => AnimationMode.InAnimationMode();
    public static Type? PropertyModificationToEditorCurveBinding(PropertyModification modification, GameObject gameObject, out EditorCurveBinding binding)
    {
        binding = default;
        if (modification?.target == null || gameObject == null) return null;
        var target = modification.target;
        var transform = target as Transform ?? (target as Component)?.transform;
        if (transform == null || !transform.IsChildOf(gameObject.transform) && !ReferenceEquals(transform, gameObject.transform)) return null;
        var type = target is GameObject ? typeof(GameObject) : target.GetType();
        binding = EditorCurveBinding.FloatCurve(CalculateTransformPath(transform, gameObject.transform), type, modification.propertyPath ?? string.Empty);
        return GetMemberType(target, modification.propertyPath ?? string.Empty);
    }

    public static void SetAdditiveReferencePose(AnimationClip clip, AnimationClip referenceClip, float time)
    {
        var data = GetData(clip);
        bool valid = clip?.CanUseAdditiveReferencePose(referenceClip) == true;
        data.Settings.additiveReferencePoseClip = valid ? referenceClip : null;
        data.Settings.additiveReferencePoseTime = time;
        data.Settings.hasAdditiveReferencePose = valid;
        clip?.SetAdditiveReferencePose(referenceClip, time);
    }
    public static void SetAnimationClips([Bindings.NotNull("ArgumentNullException")] Animation animation, [Bindings.Unmarshalled] AnimationClip[] clips)
    {
        if (animation == null) throw new ArgumentNullException(nameof(animation));
        foreach (var existing in animation.GetClips()) animation.RemoveClip(existing);
        if (clips == null) return;
        foreach (var clip in clips) if (clip != null) animation.AddClip(clip, clip.name);
        animation.clip = clips.Length > 0 ? clips[0] : null;
    }
    public static void SetAnimationClipSettings([Bindings.NotNull("ArgumentNullException")] AnimationClip clip, AnimationClipSettings srcClipInfo)
    {
        AnimationClipSettings settings = (srcClipInfo ?? new AnimationClipSettings()).Clone();
        GetData(clip).Settings = settings;
        AnimationClip? reference = settings.hasAdditiveReferencePose ? settings.additiveReferencePoseClip : null;
        bool valid = clip?.CanUseAdditiveReferencePose(reference) == true;
        settings.hasAdditiveReferencePose = valid;
        settings.additiveReferencePoseClip = valid ? reference : null;
        clip?.SetAdditiveReferencePose(reference, settings.additiveReferencePoseTime);
    }
    [Bindings.NativeThrows]
    public static void SetAnimationEvents([Bindings.NotNull("ArgumentNullException")] AnimationClip clip, [Bindings.NotNull("ArgumentNullException"), Bindings.Unmarshalled] AnimationEvent[] events) { if (clip == null) throw new ArgumentNullException(nameof(clip)); clip.events = events ?? Array.Empty<AnimationEvent>(); Notify(clip, default, CurveModifiedType.ClipModified); }
    [Obsolete("SetAnimationType is no longer supported.")]
    public static void SetAnimationType(AnimationClip clip, ModelImporterAnimationType type) => GetData(clip).AnimationType = type;
    [Obsolete("This overload is deprecated. Use the one with EditorCurveBinding instead.")]
    public static void SetEditorCurve(AnimationClip clip, string relativePath, Type type, string propertyName, AnimationCurve curve) => SetEditorCurve(clip, EditorCurveBinding.FloatCurve(relativePath, type, propertyName), curve);
    public static void SetEditorCurve(AnimationClip clip, EditorCurveBinding binding, AnimationCurve curve)
    { if (clip == null) throw new ArgumentNullException(nameof(clip)); clip.SetCurve(binding.path, binding.type!, binding.propertyName, curve); Notify(clip, binding, curve == null ? CurveModifiedType.CurveDeleted : CurveModifiedType.CurveModified); }
    public static void SetEditorCurves(AnimationClip clip, EditorCurveBinding[] bindings, AnimationCurve[] curves)
    {
        if (bindings == null || curves == null || bindings.Length != curves.Length) throw new ArgumentException("Bindings and curves must have equal lengths.");
        for (var i = 0; i < bindings.Length; i++) SetEditorCurve(clip, bindings[i], curves[i]);
    }
    [Obsolete("This is not used anymore.  Root motion curves are automatically generated if applyRootMotion is enabled on Animator component.")]
    public static void SetGenerateMotionCurves(AnimationClip clip, bool value) => GetData(clip).GenerateMotionCurves = value;
    [Bindings.NativeThrows]
    public static void SetKeyBroken([Bindings.NotNull("ArgumentNullException")] AnimationCurve curve, int index, bool broken) { var data = GetCurveData(curve); if (broken) data.Broken.Add(index); else data.Broken.Remove(index); }
    [Bindings.NativeThrows, Bindings.ThreadSafe]
    public static void SetKeyLeftTangentMode([Bindings.NotNull("ArgumentNullException")] AnimationCurve curve, int index, TangentMode tangentMode) => GetCurveData(curve).Left[index] = tangentMode;
    [Bindings.NativeThrows, Bindings.ThreadSafe]
    public static void SetKeyRightTangentMode([Bindings.NotNull("ArgumentNullException")] AnimationCurve curve, int index, TangentMode tangentMode) => GetCurveData(curve).Right[index] = tangentMode;
    public static void SetObjectReferenceCurve(AnimationClip clip, EditorCurveBinding binding, [Bindings.Unmarshalled] ObjectReferenceKeyframe[] keyframes) { GetData(clip).ObjectCurves[binding] = keyframes is null ? Array.Empty<ObjectReferenceKeyframe>() : (ObjectReferenceKeyframe[])keyframes.Clone(); Notify(clip, binding, CurveModifiedType.CurveModified); }
    public static void SetObjectReferenceCurves(AnimationClip clip, EditorCurveBinding[] bindings, ObjectReferenceKeyframe[][] keyframes)
    { if (bindings == null || keyframes == null || bindings.Length != keyframes.Length) throw new ArgumentException("Bindings and keyframes must have equal lengths."); for (var i = 0; i < bindings.Length; i++) SetObjectReferenceCurve(clip, bindings[i], keyframes[i]); }
    [Obsolete("Use AnimationMode.StartAnimationmode instead.")]
    public static void StartAnimationMode(Object[] objects) { _ = objects; AnimationMode.StartAnimationMode(); }
    [Obsolete("Use AnimationMode.StopAnimationMode instead.")]
    public static void StopAnimationMode() => AnimationMode.StopAnimationMode();

    private static void AddTransformBindings(List<EditorCurveBinding> result, Transform transform, string path)
    { foreach (var property in new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" }) result.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), property)); }
    private static bool TryGetTransformFloat(Transform transform, string property, out float value)
    { value = 0; var key = property.ToLowerInvariant(); var p = transform.localPosition; var r = transform.localRotation; var s = transform.localScale; return key switch { "m_localposition.x" or "localposition.x" => Assign(p.x, out value), "m_localposition.y" or "localposition.y" => Assign(p.y, out value), "m_localposition.z" or "localposition.z" => Assign(p.z, out value), "m_localrotation.x" or "localrotation.x" => Assign(r.x, out value), "m_localrotation.y" or "localrotation.y" => Assign(r.y, out value), "m_localrotation.z" or "localrotation.z" => Assign(r.z, out value), "m_localrotation.w" or "localrotation.w" => Assign(r.w, out value), "m_localscale.x" or "localscale.x" => Assign(s.x, out value), "m_localscale.y" or "localscale.y" => Assign(s.y, out value), "m_localscale.z" or "localscale.z" => Assign(s.z, out value), _ => false }; }
    private static bool Assign(float input, out float output) { output = input; return true; }
    private static object? GetMember(Object? target, string property) { if (target == null || string.IsNullOrEmpty(property)) return null; var member = property.Contains('.') ? property[(property.LastIndexOf('.') + 1)..] : property; var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic; return target.GetType().GetProperty(member, flags)?.GetValue(target) ?? target.GetType().GetField(member, flags)?.GetValue(target); }
    private static Type? GetMemberType(Object? target, string property) { if (target == null) return null; if (target is Transform && (property.Contains("Position") || property.Contains("Rotation") || property.Contains("Scale"))) return typeof(float); var member = property.Contains('.') ? property[(property.LastIndexOf('.') + 1)..] : property; var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic; return target.GetType().GetProperty(member, flags)?.PropertyType ?? target.GetType().GetField(member, flags)?.FieldType; }
    private static ClipEditorData GetData(AnimationClip? clip) { if (clip == null) throw new ArgumentNullException(nameof(clip)); return ClipData.GetValue(clip, _ => new ClipEditorData()); }
    private static CurveEditorData GetCurveData(AnimationCurve? curve) { if (curve == null) throw new ArgumentNullException(nameof(curve)); return CurveData.GetValue(curve, _ => new CurveEditorData()); }
    private static void Notify(AnimationClip clip, EditorCurveBinding binding, CurveModifiedType type) => onCurveWasModified?.Invoke(clip, binding, type);
    private sealed class ClipEditorData { public AnimationClipSettings Settings = new(); public bool GenerateMotionCurves; public ModelImporterAnimationType AnimationType; public readonly Dictionary<EditorCurveBinding, ObjectReferenceKeyframe[]> ObjectCurves = new(); }
    private sealed class CurveEditorData { public readonly HashSet<int> Broken = new(); public readonly Dictionary<int, TangentMode> Left = new(); public readonly Dictionary<int, TangentMode> Right = new(); }
}
