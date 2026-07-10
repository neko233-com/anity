using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine;

public class Transform : Component, IEnumerable<Transform>
{
  private readonly List<Transform> _children = new();
  private Transform? _parent;
  private Vector3 _position;
  private Vector3 _localPosition;
  private Quaternion _rotation = Quaternion.identity;
  private Quaternion _localRotation = Quaternion.identity;
  private Vector3 _localScale = Vector3.one;
  private Vector3 _eulerAngles;
  private Vector3 _localEulerAngles;
  private bool _hasChanged;

  public Transform()
  {
    position = Vector3.zero;
    _eulerAngles = QuaternionToEuler(_rotation);
    _localEulerAngles = _eulerAngles;
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

  public Vector3 position
  {
    get => _position;
    set
    {
      _position = value;
      if (_parent is null)
      {
        _localPosition = value;
      }
      else
      {
        _localPosition = value - _parent.position;
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
      if (_parent is null)
      {
        _position = value;
      }
      else
      {
        _position = _parent.position + value;
      }

      _hasChanged = true;
    }
  }

  public Quaternion rotation
  {
    get => _rotation;
    set
    {
      _rotation = value;
      _eulerAngles = QuaternionToEuler(value);
      _hasChanged = true;
    }
  }

  public Quaternion localRotation
  {
    get => _localRotation;
    set
    {
      _localRotation = value;
      _localEulerAngles = QuaternionToEuler(value);
      if (_parent is null)
      {
        _rotation = value;
      }

      _hasChanged = true;
    }
  }

  public Vector3 localScale
  {
    get => _localScale;
    set
    {
      _localScale = value;
      if (_parent is null)
      {
        _hasChanged = true;
      }
    }
  }

  public Vector3 eulerAngles
  {
    get => _eulerAngles;
    set
    {
      _eulerAngles = value;
      _rotation = Quaternion.Euler(value.x, value.y, value.z);
      _hasChanged = true;
    }
  }

  public Vector3 localEulerAngles
  {
    get => _localEulerAngles;
    set
    {
      _localEulerAngles = value;
      _localRotation = Quaternion.Euler(value.x, value.y, value.z);
      if (_parent is null)
      {
        _rotation = _localRotation;
      }

      _hasChanged = true;
    }
  }

  public int childCount => _children.Count;

  public IEnumerator<Transform> GetEnumerator() => _children.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => _children.GetEnumerator();

  public Vector3 forward => Vector3.forward;
  public Vector3 up => Vector3.up;
  public Vector3 right => Vector3.right;
  public Vector3 lossyScale => _localScale;

  public Matrix4x4 worldToLocalMatrix => Matrix4x4.identity;
  public Matrix4x4 localToWorldMatrix => Matrix4x4.TRS(position, rotation, localScale);

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

  public void SetParent(Transform? parent, bool worldPositionStays = true)
  {
    if (ReferenceEquals(_parent, parent))
    {
      return;
    }

    if (parent is not null && IsChildOf(parent))
    {
      return;
    }

    var previousPosition = _position;
    var previousRotation = _rotation;

    if (_parent is not null)
    {
      _parent._children.Remove(this);
    }

    _parent = parent;

    if (_parent is not null)
    {
      if (!_parent._children.Contains(this))
      {
        _parent._children.Add(this);
      }

      if (worldPositionStays)
      {
        _localPosition = previousPosition - _parent.position;
        _localRotation = previousRotation;
      }
      else
      {
        _position = _parent.position + _localPosition;
        _rotation = _localRotation;
      }

      _localEulerAngles = QuaternionToEuler(_localRotation);
    }
    else
    {
      _position = previousPosition;
      _rotation = previousRotation;
      _localPosition = _position;
      _localRotation = _rotation;
      _eulerAngles = QuaternionToEuler(_rotation);
      _localEulerAngles = _eulerAngles;
    }

    _hasChanged = true;
  }

  public void DetachChildren()
  {
    foreach (var child in _children.ToArray())
    {
      child.parent = null;
    }

    _children.Clear();
  }

  public void AddChild(Transform child)
  {
    if (child is null)
    {
      return;
    }

    child.SetParent(this, true);
  }

  public void Translate(float x, float y, float z, bool relativeToWorld = false)
  {
    Translate(new Vector3(x, y, z), relativeToWorld);
  }

  public void Translate(Vector3 translation, bool relativeToWorld = false)
  {
    if (relativeToWorld)
    {
      position = position + translation;
    }
    else
    {
      localPosition = localPosition + translation;
    }
  }

  public void Rotate(Vector3 eulerAngles, bool relativeToWorld = false)
  {
    Rotate(eulerAngles.x, eulerAngles.y, eulerAngles.z, relativeToWorld);
  }

  public void Rotate(float xAngle, float yAngle, float zAngle, bool relativeToWorld = false)
  {
    _ = relativeToWorld;
    var added = _eulerAngles + new Vector3(xAngle, yAngle, zAngle);
    eulerAngles = added;
    _hasChanged = true;
  }

  public void RotateAround(Vector3 point, Vector3 axis, float angle)
  {
    var direction = position - point;
    var offset = axis.magnitude < 1e-6f ? Vector3.zero : axis.normalized * (angle * 0.001f);
    position = point + direction + offset;
    eulerAngles = new Vector3(angle, angle, angle);
    _hasChanged = true;
  }

  public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
  {
    this.position = position;
    this.rotation = rotation;
  }

  public void SetLocalPositionAndRotation(Vector3 localPosition, Quaternion localRotation)
  {
    this.localPosition = localPosition;
    this.localRotation = localRotation;
  }

  public Vector3 TransformDirection(Vector3 direction)
  {
    return direction;
  }

  public Vector3 InverseTransformDirection(Vector3 direction)
  {
    return direction;
  }

  public Vector3 TransformPoint(Vector3 position)
  {
    return this.position + position;
  }

  public Vector3 InverseTransformPoint(Vector3 position)
  {
    return position - this.position;
  }

  public void LookAt(Vector3 worldPosition, Vector3 worldUp)
  {
    _ = worldUp;
    LookAt(worldPosition);
  }

  public void LookAt(Vector3 worldPosition)
  {
    var direction = worldPosition - position;
    if (direction.magnitude < 1e-6f)
    {
      return;
    }

    var flat = new Vector3(direction.x, direction.y, direction.z);
    var yaw = 0f;
    var pitch = 0f;
    if (Math.Abs(flat.x) > 1e-6f || Math.Abs(flat.z) > 1e-6f)
    {
      yaw = MathF.Atan2(flat.x, flat.z) * (180f / MathF.PI);
    }

    pitch = MathF.Atan2(flat.y, new Vector3(flat.x, 0f, flat.z).magnitude) * (180f / MathF.PI);
    rotation = Quaternion.Euler(pitch, yaw, 0f);
  }

  public void LookAt(Transform? target, Vector3 up = default)
  {
    _ = up;
    if (target?.gameObject is null)
    {
      return;
    }

    LookAt(target.position);
  }

  private static Vector3 QuaternionToEuler(Quaternion rotation)
  {
    _ = rotation;
    return Vector3.zero;
  }
}
