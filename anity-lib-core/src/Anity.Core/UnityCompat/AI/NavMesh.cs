using System;
using System.Collections.Generic;

namespace UnityEngine.AI;

public static class NavMesh
{
    public const int AllAreas = -1;

    private static readonly Dictionary<string, int> _areaNames = new()
    {
        { "Walkable", 0 },
        { "Not Walkable", 1 },
        { "Jump", 2 },
    };

    private static readonly Dictionary<int, float> _areaCosts = new();

    public static float avoidancePredictionTime { get; set; } = 2f;
    public static int pathfindingIterationsPerFrame { get; set; } = 100;
    public static bool SampleWalkableMeshOnly { get; set; }

    static NavMesh()
    {
        _areaCosts[0] = 1f;
        _areaCosts[1] = 1f;
        _areaCosts[2] = 1f;
    }

    public static bool CalculatePath(Vector3 sourcePosition, Vector3 targetPosition, int areaMask, NavMeshPath path)
    {
        if (path == null) return false;
        path.ClearCorners();

        var corners = new List<Vector3> { sourcePosition };
        float dist = Vector3.Distance(sourcePosition, targetPosition);

        if (dist > 0f)
        {
            corners.Add(targetPosition);
            path.status = NavMeshPathStatus.PathComplete;
        }
        else
        {
            path.status = NavMeshPathStatus.PathInvalid;
        }

        path.SetCorners(corners.ToArray());
        return path.status == NavMeshPathStatus.PathComplete;
    }

    public static bool Raycast(Vector3 sourcePosition, Vector3 targetPosition, out NavMeshHit hit, int areaMask)
    {
        hit = new NavMeshHit();
        _ = areaMask;

        float dist = Vector3.Distance(sourcePosition, targetPosition);
        bool blocked = false;

        hit.position = blocked ? sourcePosition : targetPosition;
        hit.normal = Vector3.up;
        hit.distance = blocked ? 0f : dist;
        hit.mask = 0;
        hit.hit = blocked;
        hit.area = 0;
        return blocked;
    }

    public static bool Linecast(Vector3 sourcePosition, Vector3 targetPosition, out NavMeshHit hit, int areaMask)
    {
        return Raycast(sourcePosition, targetPosition, out hit, areaMask);
    }

    public static bool SamplePosition(Vector3 sourcePosition, out NavMeshHit hit, float maxDistance, int areaMask)
    {
        hit = new NavMeshHit();
        _ = areaMask;
        hit.position = sourcePosition;
        hit.normal = Vector3.up;
        hit.distance = 0f;
        hit.mask = 1;
        hit.hit = true;
        hit.area = 0;
        return true;
    }

    public static bool FindClosestEdge(Vector3 sourcePosition, out NavMeshHit hit, int areaMask)
    {
        hit = new NavMeshHit();
        _ = areaMask;
        hit.position = sourcePosition;
        hit.normal = Vector3.up;
        hit.distance = 0f;
        hit.mask = 0;
        hit.hit = false;
        hit.area = 0;
        return false;
    }

    public static void SetAreaCost(int areaIndex, float cost)
    {
        _areaCosts[areaIndex] = Math.Max(0f, cost);
    }

    public static float GetAreaCost(int areaIndex)
    {
        return _areaCosts.TryGetValue(areaIndex, out var cost) ? cost : 1f;
    }

    public static int GetAreaFromName(string areaName)
    {
        if (string.IsNullOrEmpty(areaName)) return -1;
        return _areaNames.TryGetValue(areaName, out var index) ? index : -1;
    }

    public static bool CalculatePathBetweenPoints(Vector3[] waypoints, int areaMask, NavMeshPath path)
    {
        if (waypoints == null || waypoints.Length < 2 || path == null) return false;
        path.ClearCorners();
        path.SetCorners((Vector3[])waypoints.Clone());
        path.status = NavMeshPathStatus.PathComplete;
        return true;
    }
}

public class NavMeshAgent : Behaviour
{
    private Vector3 _velocity;
    private NavMeshPath? _currentPath;
    private int _currentCorner;
    private float _stoppingDistance;
    private bool _isStopped;
    private bool _updatePosition = true;
    private bool _autoBraking = true;
    private float _baseOffset;
    private float _speed = 3.5f;
    private float _angularSpeed = 120f;
    private float _acceleration = 8f;
    private float _radius = 0.5f;
    private float _height = 2f;
    private Vector3 _destination;
    private Vector3 _nextPosition;
    private int _areaMask = -1;
    private int _agentTypeID;
    private ObstacleAvoidanceType _obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    private int _avoidancePriority = 50;
    private bool _autoTraverseOffMeshLink = true;
    private bool _autoRepath = true;
    private bool _pathPending;
    private bool _hasPath;
    private bool _isPathStale;
    private bool _isOnNavMesh = true;
    private float _remainingDistance;
    private Vector3 _steeringTarget;
    private Vector3 _desiredVelocity;
    private Object? _navMeshOwner;
    private OffMeshLinkData _currentOffMeshLinkData;
    private bool _activateCurrentOffMeshLink;

    public NavMeshAgent()
    {
        var startPos = transform != null ? transform.position : Vector3.zero;
        _nextPosition = startPos;
        _destination = startPos;
        _steeringTarget = startPos;
    }

    public Vector3 destination
    {
        get => _destination;
        set => SetDestination(value);
    }

    public float speed
    {
        get => _speed;
        set => _speed = Math.Max(0f, value);
    }

    public float angularSpeed
    {
        get => _angularSpeed;
        set => _angularSpeed = Math.Max(0f, value);
    }

    public float acceleration
    {
        get => _acceleration;
        set => _acceleration = Math.Max(0f, value);
    }

    public Vector3 velocity
    {
        get => _velocity;
        set => _velocity = value;
    }

    public float stoppingDistance
    {
        get => _stoppingDistance;
        set => _stoppingDistance = Math.Max(0f, value);
    }

    public float remainingDistance => _remainingDistance;
    public bool pathPending => _pathPending;
    public bool isStopped { get => _isStopped; set => _isStopped = value; }
    public Vector3 nextPosition { get => _nextPosition; set => _nextPosition = value; }
    public bool updatePosition { get => _updatePosition; set => _updatePosition = value; }
    public bool updateRotation { get; set; } = true;
    public float radius { get => _radius; set => _radius = Math.Max(0.01f, value); }
    public float height { get => _height; set => _height = Math.Max(0.01f, value); }
    public float baseOffset { get => _baseOffset; set => _baseOffset = value; }
    public int areaMask { get => _areaMask; set => _areaMask = value; }
    public int agentTypeID { get => _agentTypeID; set => _agentTypeID = value; }
    public ObstacleAvoidanceType obstacleAvoidanceType { get => _obstacleAvoidanceType; set => _obstacleAvoidanceType = value; }
    public int avoidancePriority { get => _avoidancePriority; set => _avoidancePriority = Math.Clamp(value, 0, 99); }
    public bool autoTraverseOffMeshLink { get => _autoTraverseOffMeshLink; set => _autoTraverseOffMeshLink = value; }
    public bool autoRepath { get => _autoRepath; set => _autoRepath = value; }
    public bool autoBraking { get => _autoBraking; set => _autoBraking = value; }
    public bool hasPath => _hasPath;
    public bool isPathStale => _isPathStale;
    public bool isOnNavMesh => _isOnNavMesh;
    public Vector3 steeringTarget => _steeringTarget;
    public Vector3 desiredVelocity => _desiredVelocity;
    public Object? navMeshOwner { get => _navMeshOwner; set => _navMeshOwner = value; }
    public OffMeshLinkData currentOffMeshLinkData => _currentOffMeshLinkData;
    public bool isOnOffMeshLink { get; private set; }

    public bool Warp(Vector3 newPosition)
    {
        _nextPosition = newPosition;
        if (_updatePosition && transform != null)
        {
            transform.position = newPosition;
        }
        _isOnNavMesh = true;
        return true;
    }

    public void Move(Vector3 offset)
    {
        _nextPosition += offset;
        if (_updatePosition && transform != null)
        {
            transform.position = _nextPosition;
        }
    }

    public bool SetDestination(Vector3 target)
    {
        _destination = target;
        _currentPath = new NavMeshPath();
        var pos = transform != null ? transform.position : _nextPosition;
        bool success = NavMesh.CalculatePath(pos, target, _areaMask, _currentPath);
        _currentCorner = 0;
        _hasPath = success;
        _pathPending = false;
        _isPathStale = false;
        UpdateSteeringTarget();
        return success;
    }

    public bool SetPath(NavMeshPath path)
    {
        _currentPath = path;
        _currentCorner = 0;
        _hasPath = path != null && path.status == NavMeshPathStatus.PathComplete;
        _pathPending = false;
        _isPathStale = false;
        UpdateSteeringTarget();
        return _hasPath;
    }

    public void ResetPath()
    {
        _currentPath = null;
        _hasPath = false;
        _pathPending = false;
        _remainingDistance = 0f;
        _velocity = Vector3.zero;
        _desiredVelocity = Vector3.zero;
    }

    public void Stop()
    {
        _isStopped = true;
        _velocity = Vector3.zero;
        _desiredVelocity = Vector3.zero;
    }

    public void Resume()
    {
        _isStopped = false;
    }

    public bool CalculatePath(Vector3 targetPosition, NavMeshPath path)
    {
        var pos = transform != null ? transform.position : _nextPosition;
        return NavMesh.CalculatePath(pos, targetPosition, _areaMask, path);
    }

    public void ActivateCurrentOffMeshLink(bool activated)
    {
        _activateCurrentOffMeshLink = activated;
        isOnOffMeshLink = activated;
    }

    public void CompleteOffMeshLink()
    {
        isOnOffMeshLink = false;
        _activateCurrentOffMeshLink = false;
    }

    public bool SamplePathPosition(int areaMask, float maxDistance, out NavMeshHit hit)
    {
        hit = new NavMeshHit();
        _ = areaMask;
        _ = maxDistance;
        hit.hit = false;
        return false;
    }

    public void UpdatePosition(Vector3 position)
    {
        _nextPosition = position;
        if (transform != null)
        {
            transform.position = position;
        }
    }

    internal void UpdateAgent(float deltaTime)
    {
        if (_isStopped || !_hasPath || _currentPath == null || deltaTime <= 0f)
        {
            _velocity = MoveTowardsVector(_velocity, Vector3.zero, _acceleration * deltaTime);
            _desiredVelocity = Vector3.zero;
            return;
        }

        var currentPos = _updatePosition && transform != null ? transform.position : _nextPosition;

        UpdateSteeringTarget();

        Vector3 toTarget = _steeringTarget - currentPos;
        _remainingDistance = toTarget.magnitude;

        if (_remainingDistance <= _stoppingDistance)
        {
            if (_currentCorner < _currentPath.corners.Length - 1)
            {
                _currentCorner++;
                UpdateSteeringTarget();
                toTarget = _steeringTarget - currentPos;
                _remainingDistance = toTarget.magnitude;
            }
            else
            {
                _hasPath = false;
                _velocity = Vector3.zero;
                _desiredVelocity = Vector3.zero;
                return;
            }
        }

        Vector3 dir = toTarget.normalized;
        float targetSpeed = _speed;

        if (_autoBraking && _currentCorner >= _currentPath.corners.Length - 1)
        {
            float breakDist = (_speed * _speed) / (2f * _acceleration);
            if (_remainingDistance < breakDist)
            {
                targetSpeed = _speed * (_remainingDistance / breakDist);
            }
        }

        _desiredVelocity = dir * targetSpeed;
        _velocity = MoveTowardsVector(_velocity, _desiredVelocity, _acceleration * deltaTime);

        Vector3 move = _velocity * deltaTime;
        _nextPosition = currentPos + move;

        if (_updatePosition && transform != null)
        {
            transform.position = _nextPosition;
        }
    }

    private static Vector3 MoveTowardsVector(Vector3 current, Vector3 target, float maxDistanceDelta)
    {
        Vector3 delta = target - current;
        float dist = delta.magnitude;
        if (dist <= maxDistanceDelta || dist < 1e-6f)
            return target;
        return current + delta.normalized * maxDistanceDelta;
    }

    private void UpdateSteeringTarget()
    {
        if (_currentPath == null || _currentPath.corners.Length == 0)
        {
            _steeringTarget = _destination;
            return;
        }

        if (_currentCorner < 0) _currentCorner = 0;
        if (_currentCorner >= _currentPath.corners.Length) _currentCorner = _currentPath.corners.Length - 1;
        _steeringTarget = _currentPath.corners[_currentCorner];
    }
}

public class NavMeshPath
{
    private Vector3[] _corners = Array.Empty<Vector3>();

    public Vector3[] corners => _corners;
    public NavMeshPathStatus status { get; internal set; }

    public void ClearCorners()
    {
        _corners = Array.Empty<Vector3>();
        status = NavMeshPathStatus.PathInvalid;
    }

    internal void SetCorners(Vector3[] corners)
    {
        _corners = corners ?? Array.Empty<Vector3>();
    }
}

public struct NavMeshHit
{
    public Vector3 position;
    public Vector3 normal;
    public float distance;
    public int mask;
    public bool hit;
    public int area;
}

public struct OffMeshLinkData
{
    public bool activated;
    public Vector3 startPos;
    public Vector3 endPos;
    public Object? owner;
    public bool valid;
    public OffMeshLinkType linkType;
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

public enum OffMeshLinkType
{
    LinkTypeManual,
    LinkTypeDropDown,
    LinkTypeJumpAcross
}
