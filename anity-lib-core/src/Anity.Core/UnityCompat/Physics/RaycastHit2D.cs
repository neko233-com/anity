namespace UnityEngine;

public struct RaycastHit2D
{
  public Collider2D? collider;
  public Rigidbody2D? rigidbody;
  public Vector2 point;
  public Vector2 normal;
  public float distance;
  public Transform? transform;
  public float fraction;
  public Vector3 point3D;
}
