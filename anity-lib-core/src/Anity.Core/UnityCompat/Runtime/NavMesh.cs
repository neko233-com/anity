namespace UnityEngine.AI;

/// <summary>
/// NavMesh pathfinding data and queries.
/// </summary>
public static class NavMesh
{
    public static float avoidancePredictionTime { get; set; } = 2f;
    public static int pathfindingIterationsPerFrame { get; set; } = 100;

    public static bool Raycast(Vector3 sourcePosition, Vector3 targetPosition, out NavMeshHit hit, int areaMask)
    {
        hit = default;
        _ = sourcePosition;
        _ = targetPosition;
        _ = areaMask;
        return false;
    }

    public static bool CalculatePath(Vector3 sourcePosition, Vector3 targetPosition, int areaMask, NavMeshPath path)
    {
        _ = sourcePosition;
        _ = targetPosition;
        _ = areaMask;
        _ = path;
        return false;
    }

    public static bool FindClosestEdge(Vector3 sourcePosition, out NavMeshHit hit, int areaMask)
    {
        hit = default;
        _ = sourcePosition;
        _ = areaMask;
        return false;
    }

    public static bool SamplePosition(Vector3 sourcePosition, out NavMeshHit hit, float maxDistance, int areaMask)
    {
        hit = default;
        _ = sourcePosition;
        _ = maxDistance;
        _ = areaMask;
        return false;
    }

    public static void SetAreaCost(int areaIndex, float cost) { }
    public static float GetAreaCost(int areaIndex) => 1f;
    public static int GetAreaFromName(string areaName) => -1;
    public static bool SampleWalkableMeshOnly { get; set; }

    public static bool CalculatePathBetweenPoints(Vector3[] waypoints, int areaMask, NavMeshPath path)
    {
        _ = waypoints;
        _ = areaMask;
        _ = path;
        return false;
    }
}

/// <summary>
/// NavMesh agent component.
/// </summary>
public class NavMeshAgent : Behaviour
{
    public float speed { get; set; } = 3.5f;
    public float angularSpeed { get; set; } = 120f;
    public float acceleration { get; set; } = 8f;
    public float stoppingDistance { get; set; } = 0f;
    public bool autoBraking { get; set; } = true;
    public int areaMask { get; set; } = ~0;
    public bool isOnNavMesh { get; protected set; }
    public bool hasPath { get; protected set; }
    public bool pathPending { get; protected set; }
    public float remainingDistance { get; protected set; }
    public Vector3 destination { get; set; }
    public Vector3 velocity { get; set; }
    public Vector3 nextPosition { get; set; }
    public bool updatePosition { get; set; } = true;
    public bool updateRotation { get; set; } = true;
    public float radius { get; set; } = 0.5f;
    public float height { get; set; } = 2f;
    public ObstacleAvoidanceType obstacleAvoidanceType { get; set; } = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    public int avoidancePriority { get; set; } = 50;

    public bool SetDestination(Vector3 target)
    {
        destination = target;
        return true;
    }

    public void ResetPath() { }
    public bool Warp(Vector3 newPosition)
    {
        nextPosition = newPosition;
        return true;
    }

    public void Move(Vector3 offset) { }
    public bool SamplePathPosition(int areaMask, float maxDistance, out NavMeshHit hit)
    {
        hit = default;
        _ = areaMask;
        _ = maxDistance;
        return false;
    }
}

public class NavMeshPath
{
    public Vector3[] corners { get; protected set; } = Array.Empty<Vector3>();
    public NavMeshPathStatus status { get; protected set; }
}

public struct NavMeshHit
{
    public Vector3 position;
    public Vector3 normal;
    public float distance;
    public int mask;
    public bool hit;
}

public enum NavMeshPathStatus
{
    PathComplete,
    PathPartial,
    PathInvalid
}

public enum ObstacleAvoidanceType
{
    NoObstacleAvoidance,
    LowQualityObstacleAvoidance,
    MedQualityObstacleAvoidance,
    HighQualityObstacleAvoidance,
    GoodQualityObstacleAvoidance
}
