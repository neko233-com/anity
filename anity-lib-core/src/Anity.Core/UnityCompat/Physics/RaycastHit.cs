namespace UnityEngine;

public struct RaycastHit
{
  public Collider? collider;
  public Rigidbody? rigidbody;
  public Vector3 point;
  public Vector3 normal;
  public float distance;
  public float triangleIndex;
  public Transform? transform;
  public Vector3 barycentricCoordinate;
  public Vector2 textureCoord;
  public Vector2 textureCoord2;

  public static RaycastHit empty => new();
}
