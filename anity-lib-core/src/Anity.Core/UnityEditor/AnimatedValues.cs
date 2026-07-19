using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnityEditor.AnimatedValues;

/// <summary>Editor-time interpolated value driven by <see cref="EditorApplication.update"/>.</summary>
public abstract class BaseAnimValue<T> : ISerializationCallbackReceiver
{
    private T _value = default!;
    private T _target = default!;
    private bool _isAnimating;
    private double _lastUpdateTime;

    public float speed = 1f;
    [NonSerialized] public UnityEvent valueChanged = new();

    protected BaseAnimValue(T value) : this(value, null) { }

    protected BaseAnimValue(T value, UnityAction callback)
    {
        _value = value;
        _target = value;
        start = value;
        lerpPosition = 1f;
        if (callback != null)
            valueChanged.AddListener(callback);
    }

    public bool isAnimating => _isAnimating;

    public T value
    {
        get => GetValue();
        set => StopAnim(value);
    }

    public T target
    {
        get => _target;
        set
        {
            if (!AreEqual(_target, value))
                BeginAnimating(value, this.value);
        }
    }

    protected T start { get; private set; } = default!;
    protected float lerpPosition { get; private set; } = 1f;

    protected virtual bool AreEqual(T a, T b) => EqualityComparer<T>.Default.Equals(a, b);
    protected abstract T GetValue();

    protected void BeginAnimating(T newTarget, T newStart)
    {
        start = newStart;
        _value = newStart;
        _target = newTarget;
        lerpPosition = 0f;
        _lastUpdateTime = EditorApplication.timeSinceStartup;

        if (!_isAnimating)
        {
            _isAnimating = true;
            EditorApplication.update += UpdateAnimation;
        }
    }

    protected void StopAnim(T newValue)
    {
        bool changed = _isAnimating || !AreEqual(_value, newValue) || !AreEqual(_target, newValue);
        if (_isAnimating)
            EditorApplication.update -= UpdateAnimation;

        _isAnimating = false;
        _value = newValue;
        _target = newValue;
        start = newValue;
        lerpPosition = 1f;
        if (changed)
            valueChanged.Invoke();
    }

    private void UpdateAnimation()
    {
        if (!_isAnimating)
            return;

        double now = EditorApplication.timeSinceStartup;
        float delta = (float)Math.Max(0d, now - _lastUpdateTime);
        _lastUpdateTime = now;
        lerpPosition = Mathf.Clamp01(lerpPosition + delta * Math.Max(0f, speed));
        _value = GetValue();
        valueChanged.Invoke();

        if (lerpPosition >= 1f)
            StopAnim(_target);
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize() { }

    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        valueChanged ??= new UnityEvent();
        _value = GetValue();
    }
}

/// <summary>Allocation-free equality specialization used by Unity's common value animators.</summary>
public abstract class BaseAnimValueNonAlloc<T> : BaseAnimValue<T>, ISerializationCallbackReceiver
    where T : IEquatable<T>
{
    protected BaseAnimValueNonAlloc(T value) : base(value) { }
    protected BaseAnimValueNonAlloc(T value, UnityAction callback) : base(value, callback) { }

    protected override bool AreEqual(T a, T b) => a.Equals(b);
}

[Serializable]
public class AnimBool : BaseAnimValueNonAlloc<bool>
{
    public AnimBool() : this(false) { }
    public AnimBool(bool value) : base(value) { }
    public AnimBool(UnityAction callback) : base(false, callback) { }
    public AnimBool(bool value, UnityAction callback) : base(value, callback) { }

    public float faded => Mathf.Lerp(start ? 1f : 0f, target ? 1f : 0f, lerpPosition);
    public float Fade(float from, float to) => Mathf.Lerp(from, to, faded);

    protected override bool GetValue() => lerpPosition < 0.5f ? start : target;
}

[Serializable]
public class AnimFloat : BaseAnimValueNonAlloc<float>
{
    public AnimFloat(float value) : base(value) { }
    public AnimFloat(float value, UnityAction callback) : base(value, callback) { }

    protected override float GetValue() => Mathf.Lerp(start, target, lerpPosition);
}

[Serializable]
public class AnimQuaternion : BaseAnimValueNonAlloc<Quaternion>
{
    public AnimQuaternion(Quaternion value) : base(value) { }
    public AnimQuaternion(Quaternion value, UnityAction callback) : base(value, callback) { }

    protected override Quaternion GetValue() => lerpPosition >= 1f ? target : Quaternion.Slerp(start, target, lerpPosition);
}

[Serializable]
public class AnimVector3 : BaseAnimValueNonAlloc<Vector3>
{
    public AnimVector3() : this(Vector3.zero) { }
    public AnimVector3(Vector3 value) : base(value) { }
    public AnimVector3(Vector3 value, UnityAction callback) : base(value, callback) { }

    protected override Vector3 GetValue() => Vector3.Lerp(start, target, lerpPosition);
}
