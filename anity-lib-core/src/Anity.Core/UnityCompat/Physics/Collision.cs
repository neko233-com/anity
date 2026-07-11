namespace UnityEngine;

public struct ContactPoint
{
    public Vector3 point;
    public Vector3 normal;
    public Collider thisCollider;
    public Collider otherCollider;
    public float separation;

    public ContactPoint(Vector3 point, Vector3 normal, Collider thisCollider, Collider otherCollider, float separation = 0f)
    {
        this.point = point;
        this.normal = normal;
        this.thisCollider = thisCollider;
        this.otherCollider = otherCollider;
        this.separation = separation;
    }
}

public class Collision
{
    public Collider collider { get; internal set; }
    public Rigidbody rigidbody { get; internal set; }
    public GameObject gameObject { get; internal set; }
    public Transform transform { get; internal set; }
    public ContactPoint[] contacts { get; internal set; }
    public Vector3 relativeVelocity { get; internal set; }
    public Vector3 impulse { get; internal set; }

    public Collision()
    {
        contacts = Array.Empty<ContactPoint>();
        relativeVelocity = Vector3.zero;
        impulse = Vector3.zero;
    }

    internal void SetFrom(Collider a, Collider b, Vector3 normal, float penetration, Vector3 relVel)
    {
        collider = b;
        rigidbody = b.attachedRigidbody;
        gameObject = b.gameObject;
        transform = b.transform;
        relativeVelocity = relVel;
        contacts = new[]
        {
            new ContactPoint(
                b.transform != null ? b.transform.position : Vector3.zero,
                normal,
                a,
                b,
                -penetration)
        };
    }
}
