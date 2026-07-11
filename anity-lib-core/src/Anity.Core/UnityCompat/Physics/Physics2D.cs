using System;
using System.Collections.Generic;

namespace UnityEngine;

public static class Physics2D
{
  internal static Physics2DWorld s_world2D = new Physics2DWorld();

  private static Vector2 _gravity = new Vector2(0f, -9.81f);
  private static float _defaultContactOffset = 0.01f;
  private static float _bounceThreshold = 2f;
  private static float _sleepThreshold = 0.005f;
  private static readonly HashSet<(int, int)> _ignoredLayerCollisions = new();

  public static Vector2 gravity
  {
    get => _gravity;
    set
    {
      _gravity = value;
      s_world2D.gravity = value;
    }
  }

  public static float defaultContactOffset
  {
    get => _defaultContactOffset;
    set
    {
      _defaultContactOffset = value;
      s_world2D.defaultContactOffset = value;
    }
  }

  public static float bounceThreshold
  {
    get => _bounceThreshold;
    set
    {
      _bounceThreshold = value;
      s_world2D.bounceThreshold = value;
    }
  }

  public static float sleepThreshold
  {
    get => _sleepThreshold;
    set
    {
      _sleepThreshold = value;
      s_world2D.sleepThreshold = value;
    }
  }

  public static bool queriesHitTriggers
  {
    get => s_world2D.queriesHitTriggers;
    set => s_world2D.queriesHitTriggers = value;
  }

  public static bool Raycast(Vector2 origin, Vector2 direction, float distance = 1000f, int layerMask = -1)
  {
    return s_world2D.Raycast(origin, direction, out _, distance, layerMask);
  }

  public static bool Raycast(Vector2 origin, Vector2 direction, out RaycastHit2D hitInfo, float distance = 1000f, int layerMask = -1)
  {
    return s_world2D.Raycast(origin, direction, out hitInfo, distance, layerMask);
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
    return s_world2D.RaycastAll(origin, direction, distance, layerMask).ToArray();
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

    var hits = s_world2D.RaycastAll(origin, direction, distance, layerMask);
    int count = Math.Min(results.Length, hits.Count);
    for (int i = 0; i < count; i++) results[i] = hits[i];
    return count;
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
    return Raycast(start, end - start, out hitInfo, Vector2Distance(start, end), layerMask);
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
    return s_world2D.OverlapCircle(point, radius, layerMask, out _);
  }

  public static Collider2D[] OverlapCircleAll(Vector2 point, float radius, int layerMask = -1)
  {
    s_world2D.OverlapCircle(point, radius, layerMask, out var results);
    return results ?? Array.Empty<Collider2D>();
  }

  public static int OverlapCircleNonAlloc(Vector2 point, float radius, Collider2D[] results, int layerMask = -1)
  {
    if (results is null || results.Length == 0)
    {
      return 0;
    }

    if (!s_world2D.OverlapCircle(point, radius, layerMask, out var found))
    {
      return 0;
    }

    return FillCollider(results, found);
  }

  public static bool OverlapPoint(Vector2 point, int layerMask = -1)
  {
    return s_world2D.OverlapPoint(point, layerMask, out _);
  }

  public static Collider2D[] OverlapPointAll(Vector2 point, int layerMask = -1)
  {
    s_world2D.OverlapPoint(point, layerMask, out var results);
    return results ?? Array.Empty<Collider2D>();
  }

  public static int OverlapPointNonAlloc(Vector2 point, Collider2D[] results, int layerMask = -1)
  {
    if (results is null || results.Length == 0)
    {
      return 0;
    }

    if (!s_world2D.OverlapPoint(point, layerMask, out var found))
    {
      return 0;
    }

    return FillCollider(results, found);
  }

  public static Collider2D[] OverlapBoxAll(Vector2 point, Vector2 size, float angle, int layerMask = -1)
  {
    s_world2D.OverlapBox(point, size, angle, layerMask, out var results);
    return results ?? Array.Empty<Collider2D>();
  }

  public static int OverlapBoxNonAlloc(Vector2 point, Vector2 size, float angle, Collider2D[] results, int layerMask = -1)
  {
    if (results is null || results.Length == 0)
    {
      return 0;
    }

    if (!s_world2D.OverlapBox(point, size, angle, layerMask, out var found))
    {
      return 0;
    }

    return FillCollider(results, found);
  }

  public static Collider2D[] OverlapCapsuleAll(Vector2 point, Vector2 size, CapsuleDirection2D direction, float angle, int layerMask = -1)
  {
    _ = direction;
    _ = angle;
    s_world2D.OverlapCapsule(point, size, direction, layerMask, out var results);
    return results ?? Array.Empty<Collider2D>();
  }

  public static int OverlapCapsuleNonAlloc(Vector2 point, Vector2 size, CapsuleDirection2D direction, float angle, Collider2D[] results, int layerMask = -1)
  {
    _ = direction;
    _ = angle;
    if (results is null || results.Length == 0)
    {
      return 0;
    }

    if (!s_world2D.OverlapCapsule(point, size, direction, layerMask, out var found))
    {
      return 0;
    }

    return FillCollider(results, found);
  }

  public static bool CircleCast(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask = -1)
  {
    return s_world2D.CircleCast(origin, radius, direction, distance, layerMask, out _);
  }

  public static bool CircleCast(Vector2 origin, float radius, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask = -1)
  {
    return s_world2D.CircleCast(origin, radius, direction, distance, layerMask, out hitInfo);
  }

  public static RaycastHit2D[] CircleCastAll(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask = -1)
  {
    return s_world2D.CircleCast(origin, radius, direction, distance, layerMask, out var hit)
      ? new[] { hit }
      : Array.Empty<RaycastHit2D>();
  }

  public static int CircleCastNonAlloc(Vector2 origin, float radius, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    if (results is null || results.Length == 0) return 0;
    if (!s_world2D.CircleCast(origin, radius, direction, distance, layerMask, out var hit)) return 0;
    results[0] = hit;
    return 1;
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
    s_world2D.Simulate(deltaTime);
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, int layerMask = -1)
  {
    return s_world2D.BoxCast(origin, size, angle, direction, distance, layerMask, out _);
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask = -1)
  {
    return s_world2D.BoxCast(origin, size, angle, direction, distance, layerMask, out hitInfo);
  }

  public static int BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    if (results is null || results.Length == 0) return 0;
    if (!s_world2D.BoxCast(origin, size, angle, direction, distance, layerMask, out var hit)) return 0;
    results[0] = hit;
    return 1;
  }

  public static bool BoxCast(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, ContactFilter2D contactFilter, out RaycastHit2D hitInfo)
  {
    _ = contactFilter;
    return BoxCast(origin, size, angle, direction, out hitInfo, distance);
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

  public static RaycastHit2D[] BoxCastAll(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, int layerMask = -1)
  {
    return s_world2D.BoxCast(origin, size, angle, direction, distance, layerMask, out var hit)
      ? new[] { hit }
      : Array.Empty<RaycastHit2D>();
  }

  public static RaycastHit2D[] BoxCastAll(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, ContactFilter2D contactFilter)
  {
    _ = contactFilter;
    return BoxCastAll(origin, size, angle, direction, distance);
  }

  public static int BoxCastNonAlloc(Vector2 origin, Vector2 size, float angle, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    return BoxCast(origin, size, angle, direction, results, distance, layerMask);
  }

  public static int BoxCastNonAlloc(Vector2 origin, Vector2 size, float angle, Vector2 direction, RaycastHit2D[] results, float distance, ContactFilter2D contactFilter)
  {
    _ = contactFilter;
    return BoxCastNonAlloc(origin, size, angle, direction, results, distance);
  }

  public static bool CapsuleCast(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 direction, float distance, int layerMask = -1)
  {
    return s_world2D.CapsuleCast(origin, size, directionType, angle, direction, distance, layerMask, out _);
  }

  public static bool CapsuleCast(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask = -1)
  {
    return s_world2D.CapsuleCast(origin, size, directionType, angle, direction, distance, layerMask, out hitInfo);
  }

  public static int CapsuleCast(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    if (results is null || results.Length == 0) return 0;
    if (!s_world2D.CapsuleCast(origin, size, directionType, angle, direction, distance, layerMask, out var hit)) return 0;
    results[0] = hit;
    return 1;
  }

  public static bool CapsuleCast(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 direction, float distance, ContactFilter2D contactFilter, out RaycastHit2D hitInfo)
  {
    _ = contactFilter;
    return CapsuleCast(origin, size, directionType, angle, direction, out hitInfo, distance);
  }

  public static int CapsuleCast(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 direction, float distance, ContactFilter2D contactFilter, RaycastHit2D[] results)
  {
    _ = contactFilter;
    return CapsuleCast(origin, size, directionType, angle, direction, results, distance);
  }

  public static bool CapsuleCast(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 direction, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return CapsuleCast(origin, size, directionType, angle, direction, distance, layerMask);
  }

  public static bool CapsuleCast(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return CapsuleCast(origin, size, directionType, angle, direction, out hitInfo, distance, layerMask);
  }

  public static int CapsuleCast(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask, float minDepth, float maxDepth)
  {
    _ = minDepth;
    _ = maxDepth;
    return CapsuleCast(origin, size, directionType, angle, direction, results, distance, layerMask);
  }

  public static RaycastHit2D[] CapsuleCastAll(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 direction, float distance, int layerMask = -1)
  {
    return s_world2D.CapsuleCast(origin, size, directionType, angle, direction, distance, layerMask, out var hit)
      ? new[] { hit }
      : Array.Empty<RaycastHit2D>();
  }

  public static RaycastHit2D[] CapsuleCastAll(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 direction, float distance, ContactFilter2D contactFilter)
  {
    _ = contactFilter;
    return CapsuleCastAll(origin, size, directionType, angle, direction, distance);
  }

  public static int CapsuleCastNonAlloc(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    return CapsuleCast(origin, size, directionType, angle, direction, results, distance, layerMask);
  }

  public static int CapsuleCastNonAlloc(Vector2 origin, Vector2 size, CapsuleDirection2D directionType, float angle, Vector2 direction, float distance, ContactFilter2D contactFilter, RaycastHit2D[] results)
  {
    _ = contactFilter;
    return CapsuleCastNonAlloc(origin, size, directionType, angle, direction, results, distance);
  }

  public static bool CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, int layerMask = -1)
  {
    var size = new Vector2(radius * 2f, (end - start).magnitude + radius * 2f);
    return CapsuleCast(start, size, CapsuleDirection2D.Vertical, 0f, direction, distance, layerMask);
  }

  public static bool CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, out RaycastHit2D hitInfo, float distance, int layerMask = -1)
  {
    var size = new Vector2(radius * 2f, (end - start).magnitude + radius * 2f);
    return CapsuleCast(start, size, CapsuleDirection2D.Vertical, 0f, direction, out hitInfo, distance, layerMask);
  }

  public static int CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    var size = new Vector2(radius * 2f, (end - start).magnitude + radius * 2f);
    return CapsuleCast(start, size, CapsuleDirection2D.Vertical, 0f, direction, results, distance, layerMask);
  }

  public static bool CapsuleCast(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, ContactFilter2D contactFilter, out RaycastHit2D hitInfo)
  {
    _ = contactFilter;
    return CapsuleCast(start, end, radius, direction, out hitInfo, distance);
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

  public static RaycastHit2D[] CapsuleCastAll(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, int layerMask = -1)
  {
    var size = new Vector2(radius * 2f, (end - start).magnitude + radius * 2f);
    return CapsuleCastAll(start, size, CapsuleDirection2D.Vertical, 0f, direction, distance, layerMask);
  }

  public static RaycastHit2D[] CapsuleCastAll(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, ContactFilter2D contactFilter)
  {
    _ = contactFilter;
    return CapsuleCastAll(start, end, radius, direction, distance);
  }

  public static int CapsuleCastNonAlloc(Vector2 start, Vector2 end, float radius, Vector2 direction, RaycastHit2D[] results, float distance, int layerMask = -1)
  {
    return CapsuleCast(start, end, radius, direction, results, distance, layerMask);
  }

  public static int CapsuleCastNonAlloc(Vector2 start, Vector2 end, float radius, Vector2 direction, float distance, ContactFilter2D contactFilter, RaycastHit2D[] results)
  {
    _ = contactFilter;
    return CapsuleCastNonAlloc(start, end, radius, direction, results, distance);
  }

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
