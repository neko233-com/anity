using System.Collections.Generic;

namespace UnityEngine;

public class Collider : Component
{
    private PhysicMaterial _material;
    private bool _isTrigger;
    private List<ContactPoint> _contactPoints = new();
    private Rigidbody _attachedRigidbody;
    private bool _enabled = true;

    public bool enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public bool isActiveAndEnabled => _enabled && gameObject != null && gameObject.activeInHierarchy;

    public bool isTrigger
    {
        get => _isTrigger;
        set => _isTrigger = value;
    }

    public Rigidbody attachedRigidbody
    {
        get
        {
            if (_attachedRigidbody != null) return _attachedRigidbody;
            return GetComponent<Rigidbody>();
        }
        internal set => _attachedRigidbody = value;
    }

    public PhysicMaterial material
    {
        get => _material ?? sharedMaterial;
        set => _material = value;
    }

    public PhysicMaterial sharedMaterial
    {
        get => _material;
        set => _material = value;
    }

    public virtual Bounds bounds
    {
        get
        {
            if (transform == null) return new Bounds(Vector3.zero, Vector3.one);
            return new Bounds(transform.position, Vector3.one);
        }
    }

    public float contactOffset { get; set; } = 0.01f;
    public int layerOverridePriority { get; set; }
    public bool hasModifiableContacts { get; set; }
    public bool providesContacts { get; set; }

    public Collider()
    {
        Physics.s_world.Register(this);
    }

    ~Collider()
    {
        Physics.s_world.UnregisterCollider(this);
    }

    public virtual Vector3 ClosestPoint(Vector3 position)
    {
        Bounds b = bounds;
        return new Vector3(
            Math.Clamp(position.x, b.min.x, b.max.x),
            Math.Clamp(position.y, b.min.y, b.max.y),
            Math.Clamp(position.z, b.min.z, b.max.z));
    }

    public Vector3 ClosestPointOnBounds(Vector3 position)
    {
        Bounds b = bounds;
        return new Vector3(
            Math.Clamp(position.x, b.min.x, b.max.x),
            Math.Clamp(position.y, b.min.y, b.max.y),
            Math.Clamp(position.z, b.min.z, b.max.z));
    }

    public virtual bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance)
    {
        return Physics.Raycast(ray, out hitInfo, maxDistance);
    }

    public bool Raycast(Ray ray, out RaycastHit hitInfo)
    {
        return Raycast(ray, out hitInfo, float.PositiveInfinity);
    }

    public virtual ColliderShape GetShape()
    {
        if (transform == null) return new ColliderShape(ColliderShapeType.Box, Vector3.zero, Vector3.one, 0f, 0f, 0);
        return new ColliderShape(ColliderShapeType.Box, transform.position, Vector3.one, 0f, 0f, 0);
    }

    public bool GetContact(int index, out ContactPoint contact)
    {
        if (index < 0 || index >= _contactPoints.Count)
        {
            contact = default;
            return false;
        }
        contact = _contactPoints[index];
        return true;
    }

    public int GetContacts(ContactPoint[] contacts)
    {
        if (contacts == null) return 0;
        int count = Math.Min(_contactPoints.Count, contacts.Length);
        for (int i = 0; i < count; i++)
            contacts[i] = _contactPoints[i];
        return count;
    }

    public int GetContacts(List<ContactPoint> contacts)
    {
        if (contacts == null) return 0;
        contacts.AddRange(_contactPoints);
        return _contactPoints.Count;
    }

    internal void ClearContacts()
    {
        _contactPoints.Clear();
    }

    internal void AddContact(ContactPoint cp)
    {
        _contactPoints.Add(cp);
    }

    public Vector3 ClosestPoint(Vector3 position, Vector3 colliderPosition, Quaternion colliderRotation)
    {
        return ClosestPoint(position);
    }
}
