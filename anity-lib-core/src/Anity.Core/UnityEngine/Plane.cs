namespace UnityEngine;

public struct Plane
{
    public Vector3 normal;
    public float distance;

    public Plane(Vector3 inNormal, Vector3 inPoint)
    {
        normal = inNormal.normalized;
        distance = -Vector3.Dot(normal, inPoint);
    }

    public Plane(Vector3 inNormal, float d)
    {
        normal = inNormal.normalized;
        distance = d;
    }

    public Plane(Vector3 a, Vector3 b, Vector3 c)
    {
        normal = Vector3.Cross(b - a, c - a).normalized;
        distance = -Vector3.Dot(normal, a);
    }

    public void SetNormalAndPosition(Vector3 inNormal, Vector3 inPoint)
    {
        normal = inNormal.normalized;
        distance = -Vector3.Dot(normal, inPoint);
    }

    public void Set3Points(Vector3 a, Vector3 b, Vector3 c)
    {
        normal = Vector3.Cross(b - a, c - a).normalized;
        distance = -Vector3.Dot(normal, a);
    }

    public float GetDistanceToPoint(Vector3 point)
    {
        return Vector3.Dot(normal, point) + distance;
    }

    public Vector3 ClosestPointOnPlane(Vector3 point)
    {
        return point - normal * GetDistanceToPoint(point);
    }

    public bool GetSide(Vector3 point)
    {
        return Vector3.Dot(normal, point) + distance > 0f;
    }

    public bool SameSide(Vector3 inPt0, Vector3 inPt1)
    {
        float d0 = GetDistanceToPoint(inPt0);
        float d1 = GetDistanceToPoint(inPt1);
        return d0 * d1 >= 0;
    }

    public void Flip()
    {
        normal = -normal;
        distance = -distance;
    }

    public Plane flipped => new Plane(-normal, -distance);

    public void Translate(Vector3 translation)
    {
        distance -= Vector3.Dot(normal, translation);
    }

    public static Plane Translate(Plane plane, Vector3 translation)
    {
        return new Plane(plane.normal, plane.distance - Vector3.Dot(plane.normal, translation));
    }

    public bool Raycast(Ray ray, out float enter)
    {
        float num = Vector3.Dot(ray.direction, normal);
        if (Mathf.Approximately(num, 0f))
        {
            enter = 0f;
            return false;
        }
        enter = -(Vector3.Dot(ray.origin, normal) + distance) / num;
        return enter >= 0f;
    }
}
