using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Anity.Core.Runtime.Native;
using UnityEngine.SceneManagement;

namespace UnityEngine;

[Bindings.NativeHeader("Configuration/UnityConfigure.h")]
[Bindings.NativeHeader("Runtime/Transform/ScriptBindings/TransformScriptBindings.h")]
[Bindings.NativeHeader("Runtime/Transform/Transform.h")]
[Scripting.RequiredByNativeCode]
public class Transform : Component, IEnumerable
{
  private readonly List<Transform> _children = new();
  private Transform? _parent;
  private Vector3 _localPosition;
  private Quaternion _localRotation = Quaternion.identity;
  private Vector3 _localScale = Vector3.one;
  private bool _hasChanged;
  private int _hierarchyCapacity = 4;

  protected Transform()
  {
    _localPosition = Vector3.zero;
  }

  internal static Transform CreateForGameObject(GameObject owner)
  {
    return new Transform { gameObject = owner };
  }

  internal void AdoptStateFrom(Transform source)
  {
    if (source is null || ReferenceEquals(this, source)) return;
    _localPosition = source._localPosition;
    _localRotation = source._localRotation;
    _localScale = source._localScale;
    _hasChanged = source._hasChanged;
    _hierarchyCapacity = source._hierarchyCapacity;
    _parent = source._parent;
    if (_parent is not null)
    {
      int index = _parent._children.IndexOf(source);
      if (index >= 0) _parent._children[index] = this;
    }
    foreach (Transform child in source._children)
    {
      child._parent = this;
      _children.Add(child);
    }
    source._children.Clear();
    source._parent = null;
  }

  [Bindings.NativeProperty("HasChangedDeprecated")]
  public bool hasChanged
  {
    get => _hasChanged;
    set => _hasChanged = value;
  }

  public Transform? parent
  {
    get => _parent;
    set
    {
      SetParent(value, true);
    }
  }

  public Transform root => _parent is null ? this : _parent.root;

  public Vector3 position
  {
    get
    {
      if (_parent is null) return _localPosition;
      return _parent.TransformPoint(_localPosition);
    }
    set
    {
      if (_parent is null)
      {
        _localPosition = value;
      }
      else
      {
        _localPosition = _parent.InverseTransformPoint(value);
      }
      _hasChanged = true;
    }
  }

  public Vector3 localPosition
  {
    get => _localPosition;
    set
    {
      _localPosition = value;
      _hasChanged = true;
    }
  }

  public Quaternion rotation
  {
    get
    {
      if (_parent is null) return _localRotation;
      return _parent.rotation * ApplyParentScaleSigns(_localRotation, _parent.lossyScale);
    }
    set
    {
      if (_parent is null)
      {
        _localRotation = value;
      }
      else
      {
        Quaternion parentRelativeRotation = Quaternion.Inverse(_parent.rotation) * value;
        _localRotation = ApplyParentScaleSigns(parentRelativeRotation, _parent.lossyScale);
      }
      _hasChanged = true;
    }
  }

  public Quaternion localRotation
  {
    get => _localRotation;
    set
    {
      _localRotation = value;
      _hasChanged = true;
    }
  }

  public Vector3 localScale
  {
    get => _localScale;
    set
    {
      _localScale = value;
      _hasChanged = true;
    }
  }

  public Vector3 lossyScale
  {
    get
    {
      Matrix4x4 matrix = localToWorldMatrix;
      Quaternion worldRotation = rotation;
      if (AnityNative.TryProjectTransformLossyScale(
            ToNative(matrix), ToNative(worldRotation), out AnityNative.TransformVector3 nativeScale))
        return new Vector3(nativeScale.x, nativeScale.y, nativeScale.z);

      Vector3 axisX = worldRotation * Vector3.right;
      Vector3 axisY = worldRotation * Vector3.up;
      Vector3 axisZ = worldRotation * Vector3.forward;
      return new Vector3(
        Vector3.Dot(new Vector3(matrix.m00, matrix.m10, matrix.m20), axisX),
        Vector3.Dot(new Vector3(matrix.m01, matrix.m11, matrix.m21), axisY),
        Vector3.Dot(new Vector3(matrix.m02, matrix.m12, matrix.m22), axisZ));
    }
  }

  public Vector3 eulerAngles
  {
    get => QuaternionToEuler(rotation);
    set => rotation = Quaternion.Euler(value.x, value.y, value.z);
  }

  public Vector3 localEulerAngles
  {
    get => QuaternionToEuler(_localRotation);
    set => _localRotation = Quaternion.Euler(value.x, value.y, value.z);
  }

  public int childCount => _children.Count;

  public Vector3 forward
  {
    get => rotation * Vector3.forward;
    set => rotation = Quaternion.LookRotation(value);
  }

  public Vector3 up
  {
    get => rotation * Vector3.up;
    set => rotation = Quaternion.FromToRotation(Vector3.up, value);
  }

  public Vector3 right
  {
    get => rotation * Vector3.right;
    set => rotation = Quaternion.FromToRotation(Vector3.right, value);
  }

  public Matrix4x4 localToWorldMatrix
  {
    get
    {
      Matrix4x4 parentMatrix = _parent?.localToWorldMatrix ?? Matrix4x4.identity;
      if (AnityNative.TryComposeTransformLocalToWorld(
            ToNative(parentMatrix), ToNative(_localPosition), ToNative(_localRotation), ToNative(_localScale),
            out AnityNative.TransformMatrix4x4 nativeMatrix))
        return FromNative(nativeMatrix);

      return parentMatrix * Matrix4x4.TRS(_localPosition, _localRotation, _localScale);
    }
  }

  public Matrix4x4 worldToLocalMatrix
  {
    get
    {
      Matrix4x4 parentMatrix = _parent?.worldToLocalMatrix ?? Matrix4x4.identity;
      if (AnityNative.TryComposeTransformWorldToLocal(
            ToNative(parentMatrix), ToNative(_localPosition), ToNative(_localRotation), ToNative(_localScale),
            out AnityNative.TransformMatrix4x4 nativeMatrix))
        return FromNative(nativeMatrix);

      return InverseTrs(_localPosition, _localRotation, _localScale) * parentMatrix;
    }
  }

  public IEnumerator GetEnumerator() => _children.GetEnumerator();

  public int GetSiblingIndex()
  {
    return _parent is null ? 0 : Math.Max(0, _parent._children.IndexOf(this));
  }

  public void SetSiblingIndex(int index)
  {
    if (_parent is null)
    {
      return;
    }

    var children = _parent._children;
    _ = children.Remove(this);
    index = Math.Clamp(index, 0, children.Count);
    children.Insert(index, this);
  }

  public void SetAsFirstSibling()
  {
    SetSiblingIndex(0);
  }

  public void SetAsLastSibling()
  {
    if (_parent is null)
    {
      return;
    }

    SetSiblingIndex(_parent.childCount);
  }

  [Bindings.FreeFunction("GetChild", HasExplicitThis = true)]
  [Bindings.NativeThrows]
  public Transform GetChild(int index)
  {
    return _children[index];
  }

  public Transform? Find(string n)
  {
    if (n is null)
      return null;
    if (n.Length == 0)
      return this;

    if (n.Contains('/'))
    {
      var parts = n.Split('/');
      Transform current = this;
      foreach (var part in parts)
      {
        bool found = false;
        for (int i = 0; i < current.childCount; i++)
        {
          var child = current.GetChild(i);
          if (string.Equals(child.gameObject?.name, part, StringComparison.Ordinal))
          {
            current = child;
            found = true;
            break;
          }
        }
        if (!found) return null;
      }
      return current;
    }

    return _children.FirstOrDefault(child => string.Equals(child.gameObject?.name, n, StringComparison.Ordinal));
  }

  [Bindings.FreeFunction("Internal_IsChildOrSameTransform", HasExplicitThis = true)]
  public bool IsChildOf([Bindings.NotNull("ArgumentNullException")] Transform parent)
  {
    if (parent is null)
      throw new ArgumentNullException(nameof(parent));

    for (Transform? current = this; current is not null; current = current._parent)
    {
      if (ReferenceEquals(current, parent))
      {
        return true;
      }
    }

    return false;
  }

  public void SetParent(Transform? p)
  {
    SetParent(p, true);
  }

  [Bindings.FreeFunction("SetParent", HasExplicitThis = true)]
  public void SetParent(Transform? parent, bool worldPositionStays)
  {
    if (ReferenceEquals(_parent, parent))
    {
      return;
    }

    if (parent is not null && (ReferenceEquals(parent, this) || parent.IsChildOf(this)))
    {
      return;
    }

    Vector3 prevPosition = position;
    Quaternion prevRotation = rotation;
    Vector3 prevScale = lossyScale;
    Vector3 prevLocalPosition = _localPosition;
    Quaternion prevLocalRotation = _localRotation;
    Vector3 prevLocalScale = _localScale;

    Transform? oldParent = _parent;
    Scene oldScene = gameObject?.scene ?? default;
    if (oldParent is not null)
      oldParent._children.Remove(this);
    else if (gameObject is not null && oldScene.IsValid())
      SceneManager.UnregisterRootGameObject(gameObject, oldScene);

    _parent = parent;

    if (_parent is not null)
    {
      if (!_parent._children.Contains(this))
      {
        _parent._children.Add(this);
      }

      if (gameObject is not null && _parent.gameObject is not null && gameObject.scene != _parent.gameObject.scene)
        gameObject.SetSceneInternal(_parent.gameObject.scene);

      if (worldPositionStays)
      {
        _localPosition = _parent.InverseTransformPoint(prevPosition);
        Quaternion parentRelativeRotation = Quaternion.Inverse(_parent.rotation) * prevRotation;
        _localRotation = ApplyParentScaleSigns(parentRelativeRotation, _parent.lossyScale);
        _localScale = Vector3.one;
        Vector3 unitScaleProjection = lossyScale;
        _localScale = new Vector3(
          DivideScale(prevScale.x, unitScaleProjection.x),
          DivideScale(prevScale.y, unitScaleProjection.y),
          DivideScale(prevScale.z, unitScaleProjection.z));
      }
    }
    else
    {
      _localPosition = worldPositionStays ? prevPosition : prevLocalPosition;
      _localRotation = worldPositionStays ? prevRotation : prevLocalRotation;
      _localScale = worldPositionStays ? prevScale : prevLocalScale;
      if (gameObject is not null && oldScene.IsValid())
        SceneManager.RegisterRootGameObject(gameObject, oldScene);
    }

    root.EnsureHierarchyCapacity();
    _hasChanged = true;
    NotifyParentChangedRecursive();
    NotifyChildrenChanged(oldParent);
    NotifyChildrenChanged(_parent);
  }

  private void NotifyParentChangedRecursive()
  {
    _hasChanged = true;
    try { if (gameObject is not null) gameObject.SendMessage("OnTransformParentChanged", null, SendMessageOptions.DontRequireReceiver); } catch { }
    for (int i = 0; i < childCount; i++)
      _children[i].NotifyParentChangedRecursive();
  }

  private static void NotifyChildrenChanged(Transform? transform)
  {
    try { transform?.gameObject?.SendMessage("OnTransformChildrenChanged", null, SendMessageOptions.DontRequireReceiver); }
    catch { }
  }

  [Bindings.FreeFunction("DetachChildren", HasExplicitThis = true)]
  public void DetachChildren()
  {
    foreach (var child in _children.ToArray())
    {
      child.parent = null;
    }

    _children.Clear();
  }

  public void Translate(float x, float y, float z)
  {
    Translate(x, y, z, Space.Self);
  }

  public void Translate(float x, float y, float z, [Internal.DefaultValue("Space.Self")] Space relativeTo)
  {
    Translate(new Vector3(x, y, z), relativeTo);
  }

  public void Translate(Vector3 translation)
  {
    Translate(translation, Space.Self);
  }

  public void Translate(Vector3 translation, [Internal.DefaultValue("Space.Self")] Space relativeTo)
  {
    if (relativeTo == Space.World)
    {
      position = position + translation;
    }
    else
    {
      position = position + TransformDirection(translation);
    }
  }

  public void Translate(float x, float y, float z, Transform? relativeTo)
  {
    Translate(new Vector3(x, y, z), relativeTo);
  }

  public void Translate(Vector3 translation, Transform? relativeTo)
  {
    if (relativeTo is null)
    {
      Translate(translation, Space.Self);
    }
    else
    {
      position = position + relativeTo.TransformDirection(translation);
    }
  }

  public void Rotate(Vector3 eulers)
  {
    Rotate(eulers, Space.Self);
  }

  public void Rotate(Vector3 eulers, [Internal.DefaultValue("Space.Self")] Space relativeTo)
  {
    Rotate(eulers.x, eulers.y, eulers.z, relativeTo);
  }

  public void Rotate(float xAngle, float yAngle, float zAngle)
  {
    Rotate(xAngle, yAngle, zAngle, Space.Self);
  }

  public void Rotate(float xAngle, float yAngle, float zAngle, [Internal.DefaultValue("Space.Self")] Space relativeTo)
  {
    var rot = Quaternion.Euler(xAngle, yAngle, zAngle);
    if (relativeTo == Space.Self)
    {
      _localRotation = _localRotation * rot;
    }
    else
    {
      rotation = rot * rotation;
    }
    _hasChanged = true;
  }

  public void Rotate(Vector3 axis, float angle)
  {
    Rotate(axis, angle, Space.Self);
  }

  public void Rotate(Vector3 axis, float angle, [Internal.DefaultValue("Space.Self")] Space relativeTo)
  {
    if (relativeTo == Space.Self)
    {
      _localRotation = _localRotation * Quaternion.AngleAxis(angle, axis);
    }
    else
    {
      rotation = Quaternion.AngleAxis(angle, axis) * rotation;
    }
    _hasChanged = true;
  }

  public void RotateAround(Vector3 point, Vector3 axis, float angle)
  {
    var q = Quaternion.AngleAxis(angle, axis);
    var dir = position - point;
    dir = q * dir;
    position = point + dir;
    rotation = q * rotation;
    _hasChanged = true;
  }

  [Obsolete("warning use Transform.Rotate instead.")]
  public void RotateAround(Vector3 axis, float angle)
  {
    RotateAround(position, axis, angle);
  }

  [Obsolete("warning use Transform.Rotate instead.")]
  public void RotateAroundLocal(Vector3 axis, float angle)
  {
    Rotate(axis, angle, Space.Self);
  }

  public void LookAt(Vector3 worldPosition, [Internal.DefaultValue("Vector3.up")] Vector3 worldUp)
  {
    var dir = worldPosition - position;
    if (dir.sqrMagnitude < 1e-6f) return;
    rotation = Quaternion.LookRotation(dir, worldUp);
  }

  public void LookAt(Vector3 worldPosition)
  {
    LookAt(worldPosition, Vector3.up);
  }

  public void LookAt(Transform? target, [Internal.DefaultValue("Vector3.up")] Vector3 worldUp)
  {
    if (target is null) return;
    LookAt(target.position, worldUp);
  }

  public void LookAt(Transform? target)
  {
    if (target is null) return;
    LookAt(target.position, Vector3.up);
  }

  public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
  {
    this.position = position;
    this.rotation = rotation;
  }

  public void SetLocalPositionAndRotation(Vector3 localPosition, Quaternion localRotation)
  {
    _localPosition = localPosition;
    _localRotation = localRotation;
    _hasChanged = true;
  }

  public void GetPositionAndRotation(out Vector3 position, out Quaternion rotation)
  {
    position = this.position;
    rotation = this.rotation;
  }

  public void GetLocalPositionAndRotation(out Vector3 localPosition, out Quaternion localRotation)
  {
    localPosition = _localPosition;
    localRotation = _localRotation;
  }

  public Vector3 TransformDirection(Vector3 direction)
  {
    return rotation * direction;
  }

  public Vector3 TransformDirection(float x, float y, float z)
  {
    return TransformDirection(new Vector3(x, y, z));
  }

  public Vector3 InverseTransformDirection(Vector3 direction)
  {
    return Quaternion.Inverse(rotation) * direction;
  }

  public Vector3 InverseTransformDirection(float x, float y, float z)
  {
    return InverseTransformDirection(new Vector3(x, y, z));
  }

  public Vector3 TransformVector(Vector3 vector)
  {
    var mat = localToWorldMatrix;
    return mat.MultiplyVector(vector);
  }

  public Vector3 TransformVector(float x, float y, float z)
  {
    return TransformVector(new Vector3(x, y, z));
  }

  public Vector3 InverseTransformVector(Vector3 vector)
  {
    return worldToLocalMatrix.MultiplyVector(vector);
  }

  public Vector3 InverseTransformVector(float x, float y, float z)
  {
    return InverseTransformVector(new Vector3(x, y, z));
  }

  public Vector3 TransformPoint(Vector3 position)
  {
    var mat = localToWorldMatrix;
    return mat.MultiplyPoint(position);
  }

  public Vector3 TransformPoint(float x, float y, float z)
  {
    return TransformPoint(new Vector3(x, y, z));
  }

  public Vector3 InverseTransformPoint(Vector3 position)
  {
    return worldToLocalMatrix.MultiplyPoint(position);
  }

  public Vector3 InverseTransformPoint(float x, float y, float z)
  {
    return InverseTransformPoint(new Vector3(x, y, z));
  }

  public void TransformDirections(ReadOnlySpan<Vector3> directions, Span<Vector3> transformedDirections)
    => TransformSpan(directions, transformedDirections, TransformDirection, nameof(TransformDirections));

  public void TransformDirections(Span<Vector3> directions)
    => TransformDirections((ReadOnlySpan<Vector3>)directions, directions);

  public void InverseTransformDirections(ReadOnlySpan<Vector3> directions, Span<Vector3> transformedDirections)
    => TransformSpan(directions, transformedDirections, InverseTransformDirection, nameof(InverseTransformDirections));

  public void InverseTransformDirections(Span<Vector3> directions)
    => InverseTransformDirections((ReadOnlySpan<Vector3>)directions, directions);

  public void TransformVectors(ReadOnlySpan<Vector3> vectors, Span<Vector3> transformedVectors)
    => TransformSpan(vectors, transformedVectors, TransformVector, nameof(TransformVectors));

  public void TransformVectors(Span<Vector3> vectors)
    => TransformVectors((ReadOnlySpan<Vector3>)vectors, vectors);

  public void InverseTransformVectors(ReadOnlySpan<Vector3> vectors, Span<Vector3> transformedVectors)
    => TransformSpan(vectors, transformedVectors, InverseTransformVector, nameof(InverseTransformVectors));

  public void InverseTransformVectors(Span<Vector3> vectors)
    => InverseTransformVectors((ReadOnlySpan<Vector3>)vectors, vectors);

  public void TransformPoints(ReadOnlySpan<Vector3> positions, Span<Vector3> transformedPositions)
    => TransformSpan(positions, transformedPositions, TransformPoint, nameof(TransformPoints));

  public void TransformPoints(Span<Vector3> positions)
    => TransformPoints((ReadOnlySpan<Vector3>)positions, positions);

  public void InverseTransformPoints(ReadOnlySpan<Vector3> positions, Span<Vector3> transformedPositions)
    => TransformSpan(positions, transformedPositions, InverseTransformPoint, nameof(InverseTransformPoints));

  public void InverseTransformPoints(Span<Vector3> positions)
    => InverseTransformPoints((ReadOnlySpan<Vector3>)positions, positions);

  [Obsolete("FindChild has been deprecated. Use Find instead (UnityUpgradable) -> Find([mscorlib] System.String)", false)]
  public Transform? FindChild(string n) => Find(n);

  [Obsolete("warning use Transform.childCount instead (UnityUpgradable) -> Transform.childCount", false)]
  [Bindings.NativeMethod("GetChildrenCount")]
  public int GetChildCount() => childCount;

  public int hierarchyCapacity
  {
    get => root._hierarchyCapacity;
    set
    {
      Transform hierarchyRoot = root;
      hierarchyRoot._hierarchyCapacity = Math.Max(value, hierarchyRoot.hierarchyCount);
    }
  }

  public int hierarchyCount => root.CountHierarchy();

  private static void TransformSpan(
    ReadOnlySpan<Vector3> source,
    Span<Vector3> destination,
    Func<Vector3, Vector3> transform,
    string methodName)
  {
    if (source.Length != destination.Length)
      throw new InvalidOperationException($"Both spans passed to Transform.{methodName}() must be the same length");

    if (source.Overlaps(destination, out int elementOffset) && elementOffset > 0)
    {
      for (int i = source.Length - 1; i >= 0; i--)
        destination[i] = transform(source[i]);
      return;
    }

    for (int i = 0; i < source.Length; i++)
      destination[i] = transform(source[i]);
  }

  private int CountHierarchy()
  {
    int count = 1;
    foreach (Transform child in _children)
      count += child.CountHierarchy();
    return count;
  }

  private void EnsureHierarchyCapacity()
  {
    Transform hierarchyRoot = root;
    int count = hierarchyRoot.CountHierarchy();
    int capacity = Math.Max(4, hierarchyRoot._hierarchyCapacity);
    while (capacity < count)
      capacity *= 2;
    hierarchyRoot._hierarchyCapacity = capacity;
  }

  private static float DivideScale(float desiredScale, float projectedUnitScale)
    => projectedUnitScale == 0f ? 0f : desiredScale / projectedUnitScale;

  private static Quaternion ApplyParentScaleSigns(Quaternion value, Vector3 parentLossyScale)
  {
    float signX = parentLossyScale.x < 0f ? -1f : 1f;
    float signY = parentLossyScale.y < 0f ? -1f : 1f;
    float signZ = parentLossyScale.z < 0f ? -1f : 1f;
    return new Quaternion(
      value.x * signY * signZ,
      value.y * signX * signZ,
      value.z * signX * signY,
      value.w);
  }

  private static Matrix4x4 InverseTrs(Vector3 position, Quaternion rotation, Vector3 scale)
  {
    Vector3 reciprocalScale = new Vector3(
      scale.x == 0f ? 0f : 1f / scale.x,
      scale.y == 0f ? 0f : 1f / scale.y,
      scale.z == 0f ? 0f : 1f / scale.z);
    return Matrix4x4.Scale(reciprocalScale)
      * Matrix4x4.Rotate(Quaternion.Inverse(rotation))
      * Matrix4x4.Translate(-position);
  }

  private static AnityNative.TransformVector3 ToNative(Vector3 value)
    => new AnityNative.TransformVector3(value.x, value.y, value.z);

  private static AnityNative.TransformQuaternion ToNative(Quaternion value)
    => new AnityNative.TransformQuaternion(value.x, value.y, value.z, value.w);

  private static AnityNative.TransformMatrix4x4 ToNative(Matrix4x4 value)
    => new AnityNative.TransformMatrix4x4
    {
      m00 = value.m00, m01 = value.m01, m02 = value.m02, m03 = value.m03,
      m10 = value.m10, m11 = value.m11, m12 = value.m12, m13 = value.m13,
      m20 = value.m20, m21 = value.m21, m22 = value.m22, m23 = value.m23,
      m30 = value.m30, m31 = value.m31, m32 = value.m32, m33 = value.m33
    };

  private static Matrix4x4 FromNative(AnityNative.TransformMatrix4x4 value)
    => new Matrix4x4
    {
      m00 = value.m00, m01 = value.m01, m02 = value.m02, m03 = value.m03,
      m10 = value.m10, m11 = value.m11, m12 = value.m12, m13 = value.m13,
      m20 = value.m20, m21 = value.m21, m22 = value.m22, m23 = value.m23,
      m30 = value.m30, m31 = value.m31, m32 = value.m32, m33 = value.m33
    };

  internal static Vector3 QuaternionToEuler(Quaternion q)
  {
    q = q.normalized;
    float sinr_cosp = 2f * (q.w * q.x + q.y * q.z);
    float cosr_cosp = 1f - 2f * (q.x * q.x + q.y * q.y);
    float roll = MathF.Atan2(sinr_cosp, cosr_cosp);

    float sinp = 2f * (q.w * q.y - q.z * q.x);
    float pitch;
    if (MathF.Abs(sinp) >= 1f)
      pitch = Math.Sign(sinp) * (MathF.PI / 2f);
    else
      pitch = MathF.Asin(sinp);

    float siny_cosp = 2f * (q.w * q.z + q.x * q.y);
    float cosy_cosp = 1f - 2f * (q.y * q.y + q.z * q.z);
    float yaw = MathF.Atan2(siny_cosp, cosy_cosp);

    return new Vector3(
      pitch * (180f / MathF.PI),
      yaw * (180f / MathF.PI),
      roll * (180f / MathF.PI));
  }
}

public enum Space
{
  World,
  Self
}
