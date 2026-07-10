using System;
using System.Collections.Generic;

namespace UnityEngine;

public static class Physics2D
{
  private static Vector2 _gravity = new Vector2(0f, -9.81f);
  private static float _defaultContactOffset = 0.01f;
  private static float _bounceThreshold = 2f;
  private static float _sleepThreshold = 0.005f;
  private static readonly HashSet<(int, int)> _ignoredLayerCollisions = new();

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
    return Physics2DWorld.Raycast(origin, direction, out _, distance, layerMask);
  }

  public static bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hitInfo, float distance = 1000f, int layerMask = -1)
  {
    return Physics2DWorld.Raycast(origin, direction, out hitInfo, distance, layerMask);
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
    var hits = new List<RaycastHit2D>();
    var current = origin;
    var remaining = distance;
    while (Physics2DWorld.Raycast(current, direction, out var hit, remaining, layerMask))
    {
      hits.Add(hit);
      var offset = direction.normalized * 1e-3f;
      current = hit.point + offset;
      remaining -= hit.distance;
      if (remaining <= 1e-3f)
      {
        break;
      }
    }

    return hits.ToArray();
  }

  public static RaycastHit2D[] RaycastAll(Vector2 origin, Vector2 direction, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return RaycastAll(origin, direction, distance, layerMask);
  }

  public static int RaycastNonAlloc(Vector2 origin, Vector2 direction, RaycastHit2D[] results, float distance = 1000f, int layerMask = -1)
  {
    if (results is null || results.Length == 0)
    {
      return 0;
    }

    if (!Physics2DWorld.Raycast(origin, direction, out var hitInfo, distance, layerMask))
    {
      return 0;
    }

    results[0] = hitInfo;
    return 1;
  }

  public static int RaycastNonAlloc(Vector2 origin, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return RaycastNonAlloc(origin, direction, results, distance, layerMask);
  }

  public static bool Linecast(Vector2 start, Vector2 end, int layerMask = -1)
  {
    return Raycast(start, end - start, Vector2Distance(start, end), layerMask);
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
    return RaycastAll(start, end - start, Vector2Distance(start, end), layerMask);
  }

  public static RaycastHit2D[] LinecastAll(Vector2 start, Vector2 end, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return LinecastAll(start, end, layerMask);
  }

  public static int LinecastNonAlloc(Vector2 start, Vector2 end, RaycastHit2D[] results, int layerMask = -1)
  {
    return RaycastNonAlloc(start, end - start, results, Vector2Distance(start, end), layerMask);
  }

  public static bool OverlapCircle(Vector2 point, float radius, int layerMask = -1)
  {
    return Physics2DWorld.OverlapCircle(point, radius, layerMask, out _);
  }

  public static Collider2D[] OverlapCircleAll(Vector2 point, float radius, int layerMask = -1)
  {
    Physics2DWorld.OverlapCircle(point, radius, layerMask, out var results);
    return results ?? Array.Empty<Collider2D>();
  }

  public static int OverlapCircleNonAlloc(Vector2 point, float radius, Collider2D[] results, int layerMask = -1)
  {
    if (results is null || results.Length == 0)
    {
      return 0;
    }

    if (!Physics2DWorld.OverlapCircle(point, radius, layerMask, out var found))
    {
      return 0;
    }

    return FillCollider(results, found);
  }

  public static bool OverlapPoint(Vector2 point, int layerMask = -1)
  {
    return Physics2DWorld.OverlapPoint(point, layerMask, out _);
  }

  public static Collider2D[] OverlapPointAll(Vector2 point, int layerMask = -1)
  {
    Physics2DWorld.OverlapPoint(point, layerMask, out var results);
    return results ?? Array.Empty<Collider2D>();
  }

  public static int OverlapPointNonAlloc(Vector2 point, Collider2D[] results, int layerMask = -1)
  {
    if (results is null || results.Length == 0)
    {
      return 0;
    }

    if (!Physics2DWorld.OverlapPoint(point, layerMask, out var found))
    {
      return 0;
    }

    return FillCollider(results, found);
  }

  public static Collider2D[] OverlapBoxAll(Vector2 point, Vector2 size, float angle, int layerMask = -1)
  {
    Physics2DWorld.OverlapBox(point, size, angle, layerMask, out var results);
    return results ?? Array.Empty<Collider2D>();
  }

  public static int OverlapBoxNonAlloc(Vector2 point, Vector2 size, float angle, Collider2D[] results, int layerMask = -1)
  {
    if (results is null || results.Length == 0)
    {
      return 0;
    }

    if (!Physics2DWorld.OverlapBox(point, size, angle, layerMask, out var found))
    {
      return 0;
    }

    return FillCollider(results, found);
  }

  public static Collider2D[] OverlapCapsuleAll(Vector2 point, float radius, CapsuleDirection2D direction, float size, int layerMask = -1)
  {
    _ = direction;
    _ = size;
    Physics2DWorld.OverlapCircle(point, radius, layerMask, out var results);
    return results ?? Array.Empty<Collider2D>();
  }

  public static int OverlapCapsuleNonAlloc(Vector2 point, float radius, CapsuleDirection2D direction, float size, Collider2D[] results, int layerMask = -1)
  {
    _ = direction;
    _ = size;
    if (results is null || results.Length == 0)
    {
      return 0;
    }

    if (!Physics2DWorld.OverlapCircle(point, radius, layerMask, out var found))
    {
      return 0;
    }

    return FillCollider(results, found);
  }

  public static bool CircleCast(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask = -1)
  {
    return CircleCast(origin, radius, direction, out _, distance, layerMask);
  }

  public static bool CircleCast(Vector2 origin, float radius, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask = -1)
  {
    hitInfo = new RaycastHit2D();
    if (!Physics2DWorld.Raycast(origin, direction, out var hit, distance, layerMask))
    {
      return false;
    }

    hitInfo = hit;
    hitInfo.point -= direction.normalized * radius;
    return true;
  }

  public static RaycastHit2D[] CircleCastAll(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask = -1)
  {
    _ = radius;
    return RaycastAll(origin, direction, distance, layerMask);
  }

  public static int CircleCastNonAlloc(Vector2 origin, float radius, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    _ = radius;
    return RaycastNonAlloc(origin, direction, results, distance, layerMask);
  }

  public static bool CheckCollisionLayers(int layer1, int layer2)
  {
    return IsLayerCollisionEnabled(layer1, layer2);
  }

  public static bool IgnoreLayerCollision(int layer1, int layer2, bool ignore = true)
  {
    var key = layer1 < layer2 ? (layer1, layer2) : (layer2, layer1);
    if (ignore)
    {
      _ = _ignoredLayerCollisions.Add(key);
    }
    else
    {
      _ = _ignoredLayerCollisions.Remove(key);
    }

    return true;
  }

  public static bool IsLayerCollisionEnabled(int layer1, int layer2)
  {
    var key = layer1 < layer2 ? (layer1, layer2) : (layer2, layer1);
    return !_ignoredLayerCollisions.Contains(key);
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
    Physics2DWorld.Simulate(deltaTime);
  }

  // --- BoxCast ---

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, int layerMask = -1)
  {
    _ = angle;
    return Raycast(origin, direction, distance, layerMask);
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask = -1)
  {
    _ = angle;
    return Raycast(origin, direction, out hitInfo, distance, layerMask);
  }

  public static int BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    _ = angle;
    return RaycastNonAlloc(origin, direction, results, distance, layerMask);
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, ContactFilter2D contactFilter, out RaycastHit2D hitInfo)
  {
    hitInfo = new RaycastHit2D();
    _ = contactFilter;
    return BoxCast(origin, size, angle, direction, distance);
  }

  public static int BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, ContactFilter2D contactFilter, RaycastHit2D[] results)
  {
    _ = contactFilter;
    return BoxCast(origin, size, angle, direction, results, distance);
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return BoxCast(origin, size, angle, direction, distance, layerMask);
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return BoxCast(origin, size, angle, direction, out hitInfo, distance, layerMask);
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
    _ = angle;
    return RaycastAll(origin, direction, distance, layerMask);
  }

  public static RaycastHit2D[] BoxCastAll(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, ContactFilter2D contactFilter)
  {
    _ = contactFilter;
    return BoxCastAll(origin, size, angle, direction, distance);
  }

  // --- BoxCastNonAlloc ---

  public static int BoxCastNonAlloc(Vector2 origin, Vector2 size, float angle, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    _ = angle;
    return RaycastNonAlloc(origin, direction, results, distance, layerMask);
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
    return Raycast(start, direction, distance, layerMask);
  }

  public static bool CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask = -1)
  {
    hitInfo = new RaycastHit2D();
    _ = end;
    _ = radius;
    return Raycast(start, direction, out hitInfo, distance, layerMask);
  }

  public static int CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    _ = end;
    _ = radius;
    return RaycastNonAlloc(start, direction, results, distance, layerMask);
  }

  public static bool CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, ContactFilter2D contactFilter, out RaycastHit2D hitInfo)
  {
    hitInfo = new RaycastHit2D();
    _ = contactFilter;
    return CapsuleCast(start, end, radius, direction, distance);
  }

  public static int CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, ContactFilter2D contactFilter, RaycastHit2D[] results)
  {
    _ = contactFilter;
    return CapsuleCast(start, end, radius, direction, results, distance);
  }

  public static bool CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return CapsuleCast(start, end, radius, direction, distance, layerMask);
  }

  public static bool CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return CapsuleCast(start, end, radius, direction, out hitInfo, distance, layerMask);
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
    _ = end;
    _ = radius;
    return RaycastAll(start, direction, distance, layerMask);
  }

  public static RaycastHit2D[] CapsuleCastAll(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, ContactFilter2D contactFilter)
  {
    _ = contactFilter;
    return CapsuleCastAll(start, end, radius, direction, distance);
  }

  // --- CapsuleCastNonAlloc ---

  public static int CapsuleCastNonAlloc(Vector2 start, Vector2 end, float radius, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    _ = end;
    _ = radius;
    return RaycastNonAlloc(start, direction, results, distance, layerMask);
  }

  public static int CapsuleCastNonAlloc(Vector2 start, Vector2 end, float radius, Vector2 direction, RaycastHit2D[] results, float distance, ContactFilter2D contactFilter)
  {
    _ = contactFilter;
    return CapsuleCastNonAlloc(start, end, radius, direction, results, distance);
  }

  // --- GetRayIntersection ---

  public static RaycastHit2D GetRayIntersection(Ray ray, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    _ = layerMask;
    var origin = new Vector2(ray.origin.x, ray.origin.y);
    var direction = new Vector2(ray.direction.x, ray.direction.y);
    if (Raycast(origin, direction, out var hit, distance))
    {
      return hit;
    }

    return new RaycastHit2D();
  }

  public static bool GetRayIntersection(Ray ray, out RaycastHit2D hitInfo, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    hitInfo = GetRayIntersection(ray, distance, layerMask);
    return hitInfo.collider is not null;
  }

  public static RaycastHit2D GetRayIntersection(Vector3 origin, Vector3 direction, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    return GetRayIntersection(new Ray(origin, direction), distance, layerMask);
  }

  // --- GetRayIntersectionAll ---

  public static RaycastHit2D[] GetRayIntersectionAll(Ray ray, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    var origin = new Vector2(ray.origin.x, ray.origin.y);
    var direction = new Vector2(ray.direction.x, ray.direction.y);
    return RaycastAll(origin, direction, distance, layerMask);
  }

  public static RaycastHit2D[] GetRayIntersectionAll(Vector3 origin, Vector3 direction, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    var o = new Vector2(origin.x, origin.y);
    var d = new Vector2(direction.x, direction.y);
    return RaycastAll(o, d, distance, layerMask);
  }

  // --- GetRayIntersectionNonAlloc ---

  public static int GetRayIntersectionNonAlloc(Ray ray, RaycastHit2D[] results, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    var origin = new Vector2(ray.origin.x, ray.origin.y);
    var direction = new Vector2(ray.direction.x, ray.direction.y);
    return RaycastNonAlloc(origin, direction, results, distance, layerMask);
  }

  public static int GetRayIntersectionNonAlloc(Vector3 origin, Vector3 direction, RaycastHit2D[] results, float distance = float.PositiveInfinity, int layerMask = -1)
  {
    var o = new Vector2(origin.x, origin.y);
    var d = new Vector2(direction.x, direction.y);
    return RaycastNonAlloc(o, d, results, distance, layerMask);
  }

  private static int FillCollider(Collider2D[] results, Collider2D[] found)
  {
    if (results.Length == 0)
    {
      return 0;
    }

    var count = Math.Min(results.Length, found.Length);
    for (var i = 0; i < count; i++)
    {
      results[i] = found[i];
    }

    return count;
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
