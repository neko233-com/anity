using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine;

public class Transform : Component, IEnumerable<Transform>
{
  private readonly List<Transform> _children = new();
  private Transform? _parent;
  private Vector3 _localPosition;
  private Quaternion _localRotation = Quaternion.identity;
  private Vector3 _localScale = Vector3.one;
  private bool _hasChanged;

  public Transform()
  {
    _localPosition = Vector3.zero;
  }

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
      return _parent.rotation * _localRotation;
    }
    set
    {
      if (_parent is null)
      {
        _localRotation = value;
      }
      else
      {
        _localRotation = Quaternion.Inverse(_parent.rotation) * value;
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
      if (_parent is null) return _localScale;
      var parentScale = _parent.lossyScale;
      return new Vector3(
        parentScale.x * _localScale.x,
        parentScale.y * _localScale.y,
        parentScale.z * _localScale.z);
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

  public Vector3 forward => rotation * Vector3.forward;
  public Vector3 up => rotation * Vector3.up;
  public Vector3 right => rotation * Vector3.right;

  public Matrix4x4 localToWorldMatrix => Matrix4x4.TRS(position, rotation, lossyScale);
  public Matrix4x4 worldToLocalMatrix => localToWorldMatrix.inverse;

  public IEnumerator<Transform> GetEnumerator() => _children.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => _children.GetEnumerator();

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

  public Transform GetChild(int index)
  {
    return _children[index];
  }

  public Transform? Find(string name)
  {
    if (name.Contains('/'))
    {
      var parts = name.Split('/');
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

    foreach (var child in _children)
    {
      if (string.Equals(child.gameObject?.name, name, StringComparison.Ordinal))
      {
        return child;
      }

      var deeper = child.Find(name);
      if (deeper is not null)
      {
        return deeper;
      }
    }

    return null;
  }

  public bool IsChildOf(Transform parent)
  {
    if (parent is null)
    {
      return false;
    }

    for (var current = _parent; current is not null; current = current._parent)
    {
      if (ReferenceEquals(current, parent))
      {
        return true;
      }
    }

    return false;
  }

  public void SetParent(Transform? parent)
  {
    SetParent(parent, true);
  }

  public void SetParent(Transform? parent, bool worldPositionStays)
  {
    if (ReferenceEquals(_parent, parent))
    {
      return;
    }

    if (parent is not null && IsChildOf(parent))
    {
      return;
    }

    Vector3 prevPosition = position;
    Quaternion prevRotation = rotation;
    Vector3 prevScale = lossyScale;

    if (_parent is not null)
    {
      _parent._children.Remove(this);
      try { if (gameObject is not null) gameObject.SendMessage("OnTransformParentChanged", null, SendMessageOptions.DontRequireReceiver); } catch { }
    }

    Transform? oldParent = _parent;
    _parent = parent;

    if (_parent is not null)
    {
      if (!_parent._children.Contains(this))
      {
        _parent._children.Add(this);
      }

      if (worldPositionStays)
      {
        _localPosition = _parent.InverseTransformPoint(prevPosition);
        _localRotation = Quaternion.Inverse(_parent.rotation) * prevRotation;
        var parentScale = _parent.lossyScale;
        _localScale = new Vector3(
          MathF.Abs(parentScale.x) > 1e-6f ? prevScale.x / parentScale.x : prevScale.x,
          MathF.Abs(parentScale.y) > 1e-6f ? prevScale.y / parentScale.y : prevScale.y,
          MathF.Abs(parentScale.z) > 1e-6f ? prevScale.z / parentScale.z : prevScale.z);
      }
    }
    else
    {
      _localPosition = prevPosition;
      _localRotation = prevRotation;
      _localScale = prevScale;
    }

    _hasChanged = true;
    try { if (gameObject is not null) gameObject.SendMessage("OnTransformParentChanged", null, SendMessageOptions.DontRequireReceiver); } catch { }
    for (int i = 0; i < childCount; i++)
    {
      _children[i].OnParentTransformChanged();
    }
  }

  private void OnParentTransformChanged()
  {
    _hasChanged = true;
    try { if (gameObject is not null) gameObject.SendMessage("OnTransformParentChanged", null, SendMessageOptions.DontRequireReceiver); } catch { }
    for (int i = 0; i < childCount; i++)
    {
      _children[i].OnParentTransformChanged();
    }
  }

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

  public void Translate(float x, float y, float z, Space relativeTo)
  {
    Translate(new Vector3(x, y, z), relativeTo);
  }

  public void Translate(Vector3 translation)
  {
    Translate(translation, Space.Self);
  }

  public void Translate(Vector3 translation, Space relativeTo)
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

  public void Rotate(Vector3 eulerAngles, Space relativeTo = Space.Self)
  {
    Rotate(eulerAngles.x, eulerAngles.y, eulerAngles.z, relativeTo);
  }

  public void Rotate(float xAngle, float yAngle, float zAngle, Space relativeTo = Space.Self)
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

  public void Rotate(Vector3 axis, float angle, Space relativeTo = Space.Self)
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

  public void RotateAround(Vector3 axis, float angle)
  {
    RotateAround(position, axis, angle);
  }

  public void LookAt(Vector3 worldPosition, Vector3 worldUp)
  {
    var dir = worldPosition - position;
    if (dir.sqrMagnitude < 1e-6f) return;
    rotation = Quaternion.LookRotation(dir, worldUp);
  }

  public void LookAt(Vector3 worldPosition)
  {
    LookAt(worldPosition, Vector3.up);
  }

  public void LookAt(Transform? target, Vector3 worldUp)
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
