using System;

namespace UnityEngine;

public static class Physics
{
  private static Vector3 _gravity = new Vector3(0f, -9.81f, 0f);

  public static Vector3 gravity
  {
    get => _gravity;
    set => _gravity = value;
  }

  public static bool autoSimulation { get; set; } = true;
  public static bool IgnoreLayerCollision(int layer1, int layer2, bool ignore = true)
  {
    _ = layer1;
    _ = layer2;
    _ = ignore;
    return true;
  }

  // Backward-compatible typo-cased overload used by existing internal callers.
  public static bool ignoreLayerCollision(int layer1, int layer2, bool ignore = true)
  {
    return IgnoreLayerCollision(layer1, layer2, ignore);
  }

  public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = origin;
    _ = direction;
    _ = maxDistance;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return false;
  }

  public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    hitInfo = RaycastHit.empty;
    return Raycast(origin, direction, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    hitInfo = RaycastHit.empty;
    return Raycast(ray.origin, ray.direction, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static bool Linecast(Vector3 start, Vector3 end, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    return Raycast(start, end - start, Vector3Distance(start, end), layerMask, queryTriggerInteraction);
  }

  public static bool Linecast(Vector3 start, Vector3 end, out RaycastHit hitInfo, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    var direction = end - start;
    hitInfo = RaycastHit.empty;
    return Raycast(start, direction, out hitInfo, Vector3Distance(start, end), layerMask, queryTriggerInteraction);
  }

  public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = radius;
    return Raycast(origin, direction, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = radius;
    hitInfo = RaycastHit.empty;
    return SphereCast(origin, radius, direction, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, float minDepth, float maxDepth, out RaycastHit hitInfo)
  {
    _ = minDepth;
    _ = maxDepth;
    hitInfo = RaycastHit.empty;
    return SphereCast(origin, radius, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static RaycastHit[] SphereCastAll(Vector3 origin, float radius, Vector3 direction, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = radius;
    return SphereCast(origin, radius, direction, out var hitInfo, maxDistance, layerMask, queryTriggerInteraction) ? new[] { hitInfo } : Array.Empty<RaycastHit>();
  }

  public static int SphereCastNonAlloc(Vector3 origin, float radius, Vector3 direction, RaycastHit[] results, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    if (SphereCast(origin, radius, direction, out var hitInfo, maxDistance, layerMask, queryTriggerInteraction))
    {
      return Fill(results, hitInfo);
    }

    return 0;
  }

  public static int SphereCastNonAlloc(Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, float minDepth, float maxDepth, RaycastHit[] results)
  {
    _ = minDepth;
    _ = maxDepth;
    return SphereCastNonAlloc(origin, radius, direction, results, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static bool CheckSphere(Vector3 position, float radius, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = position;
    _ = radius;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return false;
  }

  public static bool IgnoreCollision(Collider collider1, Collider collider2, bool ignore = true)
  {
    _ = collider1;
    _ = collider2;
    _ = ignore;
    return true;
  }

  public static int RaycastNonAlloc(Vector3 origin, Vector3 direction, RaycastHit[] results, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    if (results is null)
    {
      return 0;
    }

    if (Raycast(origin, direction, out var hit, maxDistance, layerMask, queryTriggerInteraction))
    {
      results[0] = hit;
      return 1;
    }

    return 0;
  }

  public static int LinecastNonAlloc(Vector3 start, Vector3 end, RaycastHit[] results, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    return Linecast(start, end, out var hitInfo, layerMask, queryTriggerInteraction)
      ? Fill(results, hitInfo)
      : 0;
  }

  public static RaycastHit[] LinecastAll(Vector3 start, Vector3 end, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    if (Linecast(start, end, out var hit, layerMask, queryTriggerInteraction))
    {
      return new[] { hit };
    }

    return Array.Empty<RaycastHit>();
  }

  public static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = origin;
    _ = direction;
    _ = maxDistance;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return Array.Empty<RaycastHit>();
  }

  public static Collider[] OverlapSphere(Vector3 position, float radius, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = position;
    _ = radius;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return Array.Empty<Collider>();
  }

  public static bool OverlapSphere(Vector3 position, float radius, Collider[] results, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = position;
    _ = radius;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return results is not null && results.Length > 0;
  }

  public static Collider[] OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = center;
    _ = halfExtents;
    _ = orientation;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return Array.Empty<Collider>();
  }

  public static bool OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, Collider[] results, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = center;
    _ = halfExtents;
    _ = orientation;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return results is not null && results.Length > 0;
  }

  public static Collider[] OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = point0;
    _ = point1;
    _ = radius;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return Array.Empty<Collider>();
  }

  public static bool OverlapCapsule(Vector3 point0, Vector3 point1, float radius, Collider[] results, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = point0;
    _ = point1;
    _ = radius;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return results is not null && results.Length > 0;
  }

  public static bool CheckBox(Vector3 center, Vector3 halfExtents, Quaternion orientation = default, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = center;
    _ = halfExtents;
    _ = orientation;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return false;
  }

  public static bool CheckCapsule(Vector3 point1, Vector3 point2, float radius, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = point1;
    _ = point2;
    _ = radius;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return false;
  }

  public static bool BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = center;
    _ = halfExtents;
    _ = direction;
    _ = maxDistance;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return false;
  }

  public static bool BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, out RaycastHit hitInfo, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    hitInfo = RaycastHit.empty;
    return BoxCast(center, halfExtents, direction, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static bool BoxCast(Vector3 center, Vector3 halfExtents, Quaternion orientation, Vector3 direction, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = orientation;
    return BoxCast(center, halfExtents, direction, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static bool BoxCast(Vector3 center, Vector3 halfExtents, Quaternion orientation, Vector3 direction, out RaycastHit hitInfo, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = orientation;
    hitInfo = RaycastHit.empty;
    return BoxCast(center, halfExtents, direction, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static RaycastHit[] BoxCastAll(Vector3 center, Vector3 halfExtents, Vector3 direction, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = center;
    _ = halfExtents;
    _ = direction;
    _ = maxDistance;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return Array.Empty<RaycastHit>();
  }

  public static int BoxCastNonAlloc(Vector3 center, Vector3 halfExtents, Vector3 direction, RaycastHit[] results, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = center;
    _ = halfExtents;
    _ = direction;
    _ = maxDistance;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return 0;
  }

  public static int BoxCastNonAlloc(Vector3 center, Vector3 halfExtents, Quaternion orientation, Vector3 direction, RaycastHit[] results, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = orientation;
    return BoxCastNonAlloc(center, halfExtents, direction, results, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = point1;
    _ = point2;
    _ = radius;
    _ = direction;
    _ = maxDistance;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return false;
  }

  public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    hitInfo = RaycastHit.empty;
    return CapsuleCast(point1, point2, radius, direction, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, float minDepth, float maxDepth, out RaycastHit hitInfo)
  {
    _ = minDepth;
    _ = maxDepth;
    return CapsuleCast(point1, point2, radius, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static RaycastHit[] CapsuleCastAll(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = point1;
    _ = point2;
    _ = radius;
    _ = direction;
    _ = maxDistance;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return Array.Empty<RaycastHit>();
  }

  public static int CapsuleCastNonAlloc(Vector3 point1, Vector3 point2, float radius, Vector3 direction, RaycastHit[] results, float maxDistance = 1000f, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = point1;
    _ = point2;
    _ = radius;
    _ = direction;
    _ = maxDistance;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return 0;
  }

  public static int CapsuleCastNonAlloc(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction, float minDepth, float maxDepth, RaycastHit[] results)
  {
    _ = minDepth;
    _ = maxDepth;
    return CapsuleCastNonAlloc(point1, point2, radius, direction, results, maxDistance, layerMask, queryTriggerInteraction);
  }

  public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = position;
    _ = radius;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return 0;
  }

  public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation = default, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = center;
    _ = halfExtents;
    _ = orientation;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return 0;
  }

  public static int OverlapCapsuleNonAlloc(Vector3 point0, Vector3 point1, float radius, Collider[] results, int layerMask = -1, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
  {
    _ = point0;
    _ = point1;
    _ = radius;
    _ = layerMask;
    _ = queryTriggerInteraction;
    return 0;
  }

  public static bool ComputePenetration(Vector3 directionA, Vector3 positionA, Quaternion rotationA, Vector3 directionB, Vector3 positionB, Quaternion rotationB, out Vector3 movementDirection, out float movementDistance)
  {
    _ = directionA;
    _ = positionA;
    _ = rotationA;
    _ = directionB;
    _ = positionB;
    _ = rotationB;
    movementDirection = Vector3.zero;
    movementDistance = 0f;
    return false;
  }

  public static Vector3 ClosestPoint(Vector3 position, Vector3 closestToPosition, Vector3 size)
  {
    _ = position;
    _ = closestToPosition;
    _ = size;
    return closestToPosition;
  }

  public static void SyncTransforms()
  {
  }

  public static void Simulate(float deltaTime)
  {
    _ = deltaTime;
  }

  public static void StepSimulation(float deltaTime)
  {
    Simulate(deltaTime);
  }

  public static bool GetIgnoreLayerCollision(int layer1, int layer2)
  {
    _ = layer1;
    _ = layer2;
    return false;
  }

  public static bool IsSleeping()
  {
    return true;
  }

  private static int Fill(RaycastHit[] results, RaycastHit hitInfo)
  {
    if (results.Length == 0)
    {
      return 0;
    }

    results[0] = hitInfo;
    return 1;
  }

  private static float Vector3Distance(Vector3 a, Vector3 b)
  {
    var x = a.x - b.x;
    var y = a.y - b.y;
    var z = a.z - b.z;
    return MathF.Sqrt(x * x + y * y + z * z);
  }
}
