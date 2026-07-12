namespace UnityEngine;

public struct ContactPoint
{
    public Vector3 point;
    public Vector3 normal;
    public Collider thisCollider;
    public Collider otherCollider;
    public float separation;
    public float impulse;
    public float normalImpulse;
    public float tangentImpulse;
    public Rigidbody rigidbody => thisCollider?.attachedRigidbody;
    public Rigidbody otherRigidbody => otherCollider?.attachedRigidbody;

    public ContactPoint(Vector3 point, Vector3 normal, Collider thisCollider, Collider otherCollider, float separation = 0f, float impulse = 0f)
    {
        this.point = point;
        this.normal = normal;
        this.thisCollider = thisCollider;
        this.otherCollider = otherCollider;
        this.separation = separation;
        this.impulse = impulse;
        normalImpulse = impulse;
        tangentImpulse = 0f;
    }
}

public class Collision
{
    public Collider collider { get; internal set; }
    public Collider otherCollider { get; internal set; }
    public Rigidbody rigidbody { get; internal set; }
    public Rigidbody otherRigidbody { get; internal set; }
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
        otherCollider = a;
        rigidbody = b.attachedRigidbody;
        otherRigidbody = a.attachedRigidbody;
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
