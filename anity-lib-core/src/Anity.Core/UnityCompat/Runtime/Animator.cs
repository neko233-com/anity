using System;

namespace UnityEngine;

/// <summary>
/// Unity Animator component for controlling animations.
/// </summary>
[AddComponentMenu("Animation/Animator")]
public class Animator : Behaviour
{
    private RuntimeAnimatorController? _controller;
    private bool _applyRootMotion;
    private float _speed = 1.0f;
    private readonly Dictionary<int, AnimatorControllerParameter> _parameters = new();
    private readonly Dictionary<int, float> _floatValues = new();
    private readonly Dictionary<int, int> _intValues = new();
    private readonly Dictionary<int, bool> _boolValues = new();
    private readonly HashSet<int> _triggers = new();
    private int _currentStateHash;
    private float _currentNormalizedTime;
    private float _currentStateLength = 1f;
    private bool _currentLoop;
    private int _nextStateHash;
    private float _transitionDuration;
    private float _transitionTime;

    public RuntimeAnimatorController? controller
    {
        get => _controller;
        set => _controller = value;
    }

    public RuntimeAnimatorController? runtimeAnimatorController
    {
        get => controller;
        set => controller = value;
    }

    public Avatar? avatar { get; set; }

    public bool applyRootMotion
    {
        get => _applyRootMotion;
        set => _applyRootMotion = value;
    }

    public float speed
    {
        get => _speed;
        set => _speed = value;
    }

    public bool isHuman { get; }
    public bool hasRootMotion { get; }
    public float humanScale { get; } = 1f;
    public bool isInitialized { get; } = true;
    public bool hasAvatarMask { get; }
    public bool hasTransformHierarchy { get; } = true;
    public Vector3 deltaPosition { get; }
    public Quaternion deltaRotation { get; }
    public Vector3 velocity { get; }
    public Vector3 angularVelocity { get; }
    public Vector3 pivotPosition { get; }
    public float pivotWeight { get; }
    public Vector3 gravityWeight { get; }
    public Vector3 bodyPosition { get; set; }
    public Quaternion bodyRotation { get; set; }
    public Vector3 lookAtPosition { get; set; }
    public bool isMatchingTarget { get; }
    public float playbackTime { get; set; }
    public float recorderStartTime { get; }
    public float recorderStopTime { get; }
    public bool feetPivotActive { get; set; }
    public int layerCount { get; } = 1;

    public AnimatorCullingMode cullingMode { get; set; }
    public AnimatorUpdateMode updateMode { get; set; }
    public AnimatorRecorderMode recorderMode { get; set; }

    public AnimatorControllerParameter[] parameters
    {
        get
        {
            var list = new AnimatorControllerParameter[_parameters.Count];
            var i = 0;
            foreach (var kv in _parameters)
            {
                list[i++] = kv.Value;
            }

            return list;
        }
    }

    public int parameterCount => _parameters.Count;

    public AnimatorControllerParameter GetParameter(int index)
    {
        var list = parameters;
        if (index < 0 || index >= list.Length)
            return default;
        return list[index];
    }

    public static int StringToHash(string name) => name?.GetHashCode() ?? 0;

    public void SetFloat(string name, float value) => SetFloat(StringToHash(name), value);
    public void SetFloat(int id, float value) => _floatValues[id] = value;
    public void SetFloat(string name, float value, float dampTime, float deltaTime) => SetFloat(name, value);
    public void SetFloat(int id, float value, float dampTime, float deltaTime) => SetFloat(id, value);

    public float GetFloat(string name) => GetFloat(StringToHash(name));
    public float GetFloat(int id) => _floatValues.TryGetValue(id, out var value) ? value : 0f;

    public void SetInteger(string name, int value) => SetInteger(StringToHash(name), value);
    public void SetInteger(int id, int value) => _intValues[id] = value;
    public int GetInteger(string name) => GetInteger(StringToHash(name));
    public int GetInteger(int id) => _intValues.TryGetValue(id, out var value) ? value : 0;

    public void SetBool(string name, bool value) => SetBool(StringToHash(name), value);
    public void SetBool(int id, bool value) => _boolValues[id] = value;
    public bool GetBool(string name) => GetBool(StringToHash(name));
    public bool GetBool(int id) => _boolValues.TryGetValue(id, out var value) && value;

    public void SetTrigger(string name) => SetTrigger(StringToHash(name));
    public void SetTrigger(int id) => _triggers.Add(id);
    public void ResetTrigger(string name) => ResetTrigger(StringToHash(name));
    public void ResetTrigger(int id) => _triggers.Remove(id);

    public bool IsInTransition(int layerIndex)
    {
        _ = layerIndex;
        return _transitionTime > 0f;
    }

    public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex)
    {
        _ = layerIndex;
        return new AnimatorStateInfo(_currentStateHash, _currentStateHash, _currentNormalizedTime, _currentStateLength, _speed, 1f, 0, _currentLoop);
    }

    public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex)
    {
        _ = layerIndex;
        return new AnimatorStateInfo(_nextStateHash, _nextStateHash, 0f, _currentStateLength, _speed, 1f, 0, _currentLoop);
    }

    public AnimatorTransitionInfo GetAnimatorTransitionInfo(int layerIndex) => default;
    public AnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layerIndex) => Array.Empty<AnimatorClipInfo>();
    public AnimatorClipInfo[] GetNextAnimatorClipInfo(int layerIndex) => Array.Empty<AnimatorClipInfo>();

    public void CrossFade(string stateName, float normalizedTransitionDuration, int layer = -1, float normalizedTimeOffset = 0, float normalizedTransitionTime = 0)
    {
        CrossFade(StringToHash(stateName), normalizedTransitionDuration, layer, normalizedTimeOffset, normalizedTransitionTime);
    }

    public void CrossFade(int stateHashName, float normalizedTransitionDuration, int layer = -1, float normalizedTimeOffset = 0, float normalizedTransitionTime = 0)
    {
        _ = layer;
        _nextStateHash = stateHashName;
        _transitionDuration = normalizedTransitionDuration;
        _transitionTime = normalizedTransitionDuration;
        _currentNormalizedTime = normalizedTimeOffset;
    }

    public void CrossFadeInFixedTime(string stateName, float fixedTransitionDuration, int layer = -1, float fixedTimeOffset = 0, float normalizedTransitionTime = 0)
    {
        CrossFade(stateName, fixedTransitionDuration, layer, fixedTimeOffset, normalizedTransitionTime);
    }

    public void CrossFadeInFixedTime(int stateHashName, float fixedTransitionDuration, int layer = -1, float fixedTimeOffset = 0, float normalizedTransitionTime = 0)
    {
        CrossFade(stateHashName, fixedTransitionDuration, layer, fixedTimeOffset, normalizedTransitionTime);
    }

    public void Play(string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity)
    {
        Play(StringToHash(stateName), layer, normalizedTime);
    }

    public void Play(int stateNameHash, int layer = -1, float normalizedTime = float.NegativeInfinity)
    {
        _ = layer;
        _currentStateHash = stateNameHash;
        _currentNormalizedTime = normalizedTime == float.NegativeInfinity ? 0f : normalizedTime;
        _nextStateHash = 0;
        _transitionTime = 0f;
    }

    public void PlayInFixedTime(string stateName, int layer = -1, float fixedTime = 0) => Play(stateName, layer);
    public void PlayInFixedTime(int stateNameHash, int layer = -1, float fixedTime = 0) => Play(stateNameHash, layer);

    public void Rebind() { }

    public void Update(float deltaTime)
    {
        if (_transitionTime > 0f)
        {
            _transitionTime -= deltaTime / MathF.Max(_currentStateLength, 0.001f);
            if (_transitionTime <= 0f)
            {
                _currentStateHash = _nextStateHash;
                _nextStateHash = 0;
                _currentNormalizedTime = 0f;
            }
        }

        _currentNormalizedTime += deltaTime * _speed / MathF.Max(_currentStateLength, 0.001f);
        if (_currentLoop && _currentNormalizedTime > 1f)
        {
            _currentNormalizedTime -= MathF.Floor(_currentNormalizedTime);
        }
        else if (!_currentLoop)
        {
            _currentNormalizedTime = MathF.Min(_currentNormalizedTime, 1f);
        }
    }

    public void StartPlayback() { }
    public void StartRecording(int frameCount) { }
    public void StopPlayback() { }
    public void StopRecording() { }

    public int GetLayerCount() => 1;
    public string GetLayerName(int layerIndex) => string.Empty;
    public int GetLayerIndex(string layerName) => -1;
    public float GetLayerWeight(int layerIndex) => 0f;
    public void SetLayerWeight(int layerIndex, float weight) { }

    public void SetLookAtPosition(Vector3 lookAtPosition) { }
    public void SetLookAtWeight(float weight) { }
    public void SetLookAtWeight(float weight, float bodyWeight) { }
    public void SetLookAtWeight(float weight, float bodyWeight, float headWeight) { }
    public void SetLookAtWeight(float weight, float bodyWeight, float headWeight, float eyesWeight) { }
    public void SetLookAtWeight(float weight, float bodyWeight, float headWeight, float eyesWeight, float clampWeight) { }
    public bool LookAtPosition(Vector3 position, float weight) => false;

    public Transform? GetBoneTransform(HumanBodyBones humanBoneId) => null;
    public void SetBoneLocalRotation(HumanBodyBones humanBoneId, Quaternion rotation) { }

    public bool HasState(int layerIndex, int stateID) => false;

    public bool MatchTarget(Vector3 matchPosition, Quaternion matchRotation, AvatarTarget targetBodyPart, MatchTargetWeightMask weightMask, float startNormalizedTime, float targetNormalizedTime = 1) => false;
    public void InterruptMatchTarget(bool completeMatch = true) { }

    public void SetTarget(AvatarTarget targetIndex, float targetNormalizedTime) { }
    public TargetRotation GetTargetRotation() => default;
    public TargetPosition GetTargetPosition() => default;
}

public struct MatchTargetWeightMask
{
    public Vector3 positionXYZWeight;
    public float rotationWeight;

    public MatchTargetWeightMask(Vector3 positionXYZWeight, float rotationWeight)
    {
        this.positionXYZWeight = positionXYZWeight;
        this.rotationWeight = rotationWeight;
    }
}

public struct TargetRotation
{
    public bool active;
    public Quaternion rotation;
}

public struct TargetPosition
{
    public bool active;
    public Vector3 position;
}

public enum AvatarTarget
{
    Root = 0,
    Body = 1,
    LeftFoot = 2,
    RightFoot = 3,
    LeftHand = 4,
    RightHand = 5,
    LeftForeArmStretch = 6,
    RightForeArmStretch = 7,
    LeftThighStretch = 8,
    RightThighStretch = 9,
    LeftLowerLegStretch = 10,
    RightLowerLegStretch = 11,
    LeftUpperArmStretch = 12,
    RightUpperArmStretch = 13,
    LeftFingers = 14,
    RightFingers = 15,
    LeftFootIK = 16,
    RightFootIK = 17,
    LeftHandIK = 18,
    RightHandIK = 19
}

/// <summary>
/// Runtime Animator Controller asset.
/// </summary>
public class RuntimeAnimatorController : Object { }

