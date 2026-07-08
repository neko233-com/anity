using System;

namespace UnityEngine;

public static class Physics2D
{
  private static Vector2 _gravity = new Vector2(0f, -9.81f);
  private static float _defaultContactOffset = 0.01f;
  private static float _bounceThreshold = 2f;
  private static float _sleepThreshold = 0.005f;

  public static Vector2 gravity
  {
    get => _gravity;
    set => _gravity = value;
  }

  public static float defaultContactOffset
  {
    get => _defaultContactOffset;
    set => _defaultContactOffset = value;
  }

  public static float bounceThreshold
  {
    get => _bounceThreshold;
    set => _bounceThreshold = value;
  }

  public static float sleepThreshold
  {
    get => _sleepThreshold;
    set => _sleepThreshold = value;
  }

  public static bool Raycast(Vector2 origin, Vector2 direction, float distance = 1000f, int layerMask = -1)
  {
    _ = origin;
    _ = direction;
    _ = distance;
    _ = layerMask;
    return false;
  }

  public static bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hitInfo, float distance = 1000f, int layerMask = -1)
  {
    hitInfo = new RaycastHit2D();
    return Raycast(origin, direction, distance, layerMask);
  }

  public static bool Raycast(Vector2 origin, Vector2 direction, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return Raycast(origin, direction, distance, layerMask);
  }

  public static bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return Raycast(origin, direction, out hitInfo, distance, layerMask);
  }

  public static RaycastHit2D[] RaycastAll(Vector2 origin, Vector2 direction, float distance = 1000f, int layerMask = -1)
  {
    return Raycast(origin, direction, distance, layerMask) ? new[] { new RaycastHit2D() } : Array.Empty<RaycastHit2D>();
  }

  public static RaycastHit2D[] RaycastAll(Vector2 origin, Vector2 direction, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return RaycastAll(origin, direction, distance, layerMask);
  }

  public static int RaycastNonAlloc(Vector2 origin, Vector2 direction, RaycastHit2D[] results, float distance = 1000f, int layerMask = -1)
  {
    if (results is null)
    {
      return 0;
    }

    return Raycast(origin, direction, out var hitInfo, distance, layerMask) ? Fill(results, hitInfo) : 0;
  }

  public static int RaycastNonAlloc(Vector2 origin, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return RaycastNonAlloc(origin, direction, results, distance, layerMask);
  }

  public static bool Linecast(Vector2 start, Vector2 end, int layerMask = -1)
  {
    return Raycast(start, end - start, 1000f, layerMask);
  }

  public static bool Linecast(Vector2 start, Vector2 end, out RaycastHit2D hitInfo, int layerMask = -1)
  {
    hitInfo = new RaycastHit2D();
    return Linecast(start, end, out hitInfo, layerMask, 0, 0);
  }

  public static bool Linecast(Vector2 start, Vector2 end, out RaycastHit2D hitInfo, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return Raycast(start, end - start, out hitInfo, Vector2Distance(start, end), layerMask);
  }

  public static RaycastHit2D[] LinecastAll(Vector2 start, Vector2 end, int layerMask = -1)
  {
    return Linecast(start, end, out var hitInfo, layerMask) ? new[] { hitInfo } : Array.Empty<RaycastHit2D>();
  }

  public static RaycastHit2D[] LinecastAll(Vector2 start, Vector2 end, int layerMask, float minDepth, float maxDepth)
  {
    return Linecast(start, end, out var hitInfo, layerMask, minDepth, maxDepth)
      ? new[] { hitInfo }
      : Array.Empty<RaycastHit2D>();
  }

  public static int LinecastNonAlloc(Vector2 start, Vector2 end, RaycastHit2D[] results, int layerMask = -1)
  {
    return Linecast(start, end, out var hitInfo, layerMask) ? Fill(results, hitInfo) : 0;
  }

  public static bool OverlapCircle(Vector2 point, float radius, int layerMask = -1)
  {
    _ = point;
    _ = radius;
    _ = layerMask;
    return false;
  }

  public static Collider2D[] OverlapCircleAll(Vector2 point, float radius, int layerMask = -1)
  {
    _ = point;
    _ = radius;
    _ = layerMask;
    return Array.Empty<Collider2D>();
  }

  public static bool OverlapPoint(Vector2 point, int layerMask = -1)
  {
    _ = point;
    _ = layerMask;
    return false;
  }

  public static Collider2D[] OverlapBoxAll(Vector2 point, Vector2 size, float angle, int layerMask = -1)
  {
    _ = point;
    _ = size;
    _ = angle;
    _ = layerMask;
    return Array.Empty<Collider2D>();
  }

  public static Collider2D[] OverlapCapsuleAll(Vector2 point, float radius, CapsuleDirection2D direction, float size, int layerMask = -1)
  {
    _ = point;
    _ = radius;
    _ = direction;
    _ = size;
    _ = layerMask;
    return Array.Empty<Collider2D>();
  }

  public static bool CircleCast(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask = -1)
  {
    _ = origin;
    _ = radius;
    _ = direction;
    _ = distance;
    _ = layerMask;
    return false;
  }

  public static bool CircleCast(Vector2 origin, float radius, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask = -1)
  {
    hitInfo = new RaycastHit2D();
    return CircleCast(origin, radius, direction, distance, layerMask);
  }

  public static RaycastHit2D[] CircleCastAll(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask = -1)
  {
    return CircleCast(origin, radius, direction, distance, layerMask)
      ? new[] { new RaycastHit2D() }
      : Array.Empty<RaycastHit2D>();
  }

  public static bool CheckCollisionLayers(int layer1, int layer2)
  {
    return true;
  }

  public static bool IgnoreLayerCollision(int layer1, int layer2, bool ignore = true)
  {
    _ = layer1;
    _ = layer2;
    _ = ignore;
    return true;
  }

  public static bool IsLayerCollisionEnabled(int layer1, int layer2)
  {
    _ = layer1;
    _ = layer2;
    return false;
  }

  public static bool IgnoreCollision(Collider2D collider1, Collider2D collider2, bool ignore = true)
  {
    _ = collider1;
    _ = collider2;
    _ = ignore;
    return true;
  }

  public static void Simulate(float deltaTime)
  {
    _ = deltaTime;
  }

  // --- BoxCast ---

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, int layerMask = -1)
  {
    _ = origin;
    _ = size;
    _ = angle;
    _ = direction;
    _ = distance;
    _ = layerMask;
    return false;
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask = -1)
  {
    hitInfo = new RaycastHit2D();
    return BoxCast(origin, size, angle, direction, distance, layerMask);
  }

  public static int BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    _ = origin;
    _ = size;
    _ = angle;
    _ = direction;
    _ = results;
    _ = distance;
    _ = layerMask;
    return 0;
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, ContactFilter2D contactFilter, out RaycastHit2D hitInfo)
  {
    hitInfo = new RaycastHit2D();
    _ = contactFilter;
    return BoxCast(origin, size, angle, direction, distance);
  }

  public static int BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, ContactFilter2D contactFilter, RaycastHit2D[] results)
  {
    _ = origin;
    _ = size;
    _ = angle;
    _ = direction;
    _ = distance;
    _ = contactFilter;
    _ = results;
    return 0;
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return BoxCast(origin, size, angle, direction, distance, layerMask);
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask, float minDepth, float maxDepth)
  {
    hitInfo = new RaycastHit2D();
    return BoxCast(origin, size, angle, direction, distance, layerMask, minDepth, maxDepth);
  }

  public static int BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return BoxCast(origin, size, angle, direction, results, distance, layerMask);
  }

  // --- BoxCastAll ---

  public static RaycastHit2D[] BoxCastAll(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, int layerMask = -1)
  {
    return BoxCast(origin, size, angle, direction, distance, layerMask)
      ? new[] { new RaycastHit2D() }
      : Array.Empty<RaycastHit2D>();
  }

  public static RaycastHit2D[] BoxCastAll(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, ContactFilter2D contactFilter)
  {
    _ = contactFilter;
    return BoxCastAll(origin, size, angle, direction, distance);
  }

  // --- BoxCastNonAlloc ---

  public static int BoxCastNonAlloc(Vector2 origin, Vector2 size, float angle, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    if (results is null)
    {
      return 0;
    }

    return BoxCast(origin, size, angle, direction, out var hitInfo, distance, layerMask) ? Fill(results, hitInfo) : 0;
  }

  public static int BoxCastNonAlloc(Vector2 origin, Vector2 size, float angle, Vector2 direction, RaycastHit2D[] results, float distance, ContactFilter2D contactFilter)
  {
    _ = contactFilter;
    return BoxCastNonAlloc(origin, size, angle, direction, results, distance);
  }

  // --- CapsuleCast ---

  public static bool CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, int layerMask = -1)
  {
    _ = start;
    _ = end;
    _ = radius;
    _ = direction;
    _ = distance;
    _ = layerMask;
    return false;
  }

  public static bool CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask = -1)
  {
    hitInfo = new RaycastHit2D();
    return CapsuleCast(start, end, radius, direction, distance, layerMask);
  }

  public static int CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    _ = start;
    _ = end;
    _ = radius;
    _ = direction;
    _ = results;
    _ = distance;
    _ = layerMask;
    return 0;
  }

  public static bool CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, ContactFilter2D contactFilter, out RaycastHit2D hitInfo)
  {
    hitInfo = new RaycastHit2D();
    _ = contactFilter;
    return CapsuleCast(start, end, radius, direction, distance);
  }

  public static int CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, ContactFilter2D contactFilter, RaycastHit2D[] results)
  {
    _ = start;
    _ = end;
    _ = radius;
    _ = direction;
    _ = distance;
    _ = contactFilter;
    _ = results;
    return 0;
  }

  public static bool CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return CapsuleCast(start, end, radius, direction, distance, layerMask);
  }

  public static bool CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask, float minDepth, float maxDepth)
  {
    hitInfo = new RaycastHit2D();
    return CapsuleCast(start, end, radius, direction, distance, layerMask, minDepth, maxDepth);
  }

  public static int CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return CapsuleCast(start, end, radius, direction, results, distance, layerMask);
  }

  // --- CapsuleCastAll ---

  public static RaycastHit2D[] CapsuleCastAll(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, int layerMask = -1)
  {
    return CapsuleCast(start, end, radius, direction, distance, layerMask)
      ? new[] { new RaycastHit2D() }
      : Array.Empty<RaycastHit2D>();
  }

  public static RaycastHit2D[] CapsuleCastAll(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, ContactFilter2D contactFilter)
  {
    _ = contactFilter;
    return CapsuleCastAll(start, end, radius, direction, distance);
  }

  // --- CapsuleCastNonAlloc ---

  public static int CapsuleCastNonAlloc(Vector2 start, Vector2 end, float radius, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    if (results is null)
    {
      return 0;
    }

    return CapsuleCast(start, end, radius, direction, out var hitInfo, distance, layerMask) ? Fill(results, hitInfo) : 0;
  }

  public static int CapsuleCastNonAlloc(Vector2 start, Vector2 end, float radius, Vector2 direction, RaycastHit2D[] results, float distance, ContactFilter2D contactFilter)
  {
    _ = contactFilter;
    return CapsuleCastNonAlloc(start, end, radius, direction, results, distance);
  }

  // --- OverlapCircleNonAlloc ---

  public static int OverlapCircleNonAlloc(Vector2 point, float radius, Collider2D[] results, int layerMask = -1)
  {
    _ = point;
    _ = radius;
    _ = layerMask;
    return results?.Length > 0 ? FillCollider(results) : 0;
  }

  // --- OverlapPointNonAlloc ---

  public static int OverlapPointNonAlloc(Vector2 point, Collider2D[] results, int layerMask = -1)
  {
    _ = point;
    _ = layerMask;
    return results?.Length > 0 ? FillCollider(results) : 0;
  }

  // --- OverlapBoxNonAlloc ---

  public static int OverlapBoxNonAlloc(Vector2 point, Vector2 size, float angle, Collider2D[] results, int layerMask = -1)
  {
    _ = point;
    _ = size;
    _ = angle;
    _ = layerMask;
    return results?.Length > 0 ? FillCollider(results) : 0;
  }

  // --- OverlapCapsuleNonAlloc ---

  public static int OverlapCapsuleNonAlloc(Vector2 point, float radius, CapsuleDirection2D direction, float size, Collider2D[] results, int layerMask = -1)
  {
    _ = point;
    _ = radius;
    _ = direction;
    _ = size;
    _ = layerMask;
    return results?.Length > 0 ? FillCollider(results) : 0;
  }

  // --- GetRayIntersection ---

  public static RaycastHit2D GetRayIntersection(Ray ray, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    _ = ray;
    _ = distance;
    _ = layerMask;
    return new RaycastHit2D();
  }

  public static bool GetRayIntersection(Ray ray, out RaycastHit2D hitInfo, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    hitInfo = new RaycastHit2D();
    return false;
  }

  public static RaycastHit2D GetRayIntersection(Vector3 origin, Vector3 direction, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    _ = origin;
    _ = direction;
    _ = distance;
    _ = layerMask;
    return new RaycastHit2D();
  }

  // --- GetRayIntersectionAll ---

  public static RaycastHit2D[] GetRayIntersectionAll(Ray ray, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    _ = ray;
    _ = distance;
    _ = layerMask;
    return Array.Empty<RaycastHit2D>();
  }

  public static RaycastHit2D[] GetRayIntersectionAll(Vector3 origin, Vector3 direction, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    _ = origin;
    _ = direction;
    _ = distance;
    _ = layerMask;
    return Array.Empty<RaycastHit2D>();
  }

  // --- GetRayIntersectionNonAlloc ---

  public static int GetRayIntersectionNonAlloc(Ray ray, RaycastHit2D[] results, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    _ = ray;
    _ = distance;
    _ = layerMask;
    return results?.Length > 0 ? Fill(results, new RaycastHit2D()) : 0;
  }

  public static int GetRayIntersectionNonAlloc(Vector3 origin, Vector3 direction, RaycastHit2D[] results, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    _ = origin;
    _ = direction;
    _ = distance;
    _ = layerMask;
    return results?.Length > 0 ? Fill(results, new RaycastHit2D()) : 0;
  }

  private static int Fill(RaycastHit2D[] results, RaycastHit2D hit)
  {
    if (results.Length == 0)
    {
      return 0;
    }

    results[0] = hit;
    return 1;
  }

  private static int FillCollider(Collider2D[] results)
  {
    if (results.Length == 0)
    {
      return 0;
    }

    return 0;
  }

  private static float Vector2Distance(Vector2 a, Vector2 b)
  {
    var x = a.x - b.x;
    var y = a.y - b.y;
    return MathF.Sqrt(x * x + y * y);
  }
}

public enum CapsuleDirection2D
{
  Horizontal,
  Vertical
}
