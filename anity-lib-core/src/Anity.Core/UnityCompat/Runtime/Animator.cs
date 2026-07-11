using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine;

[AddComponentMenu("Animation/Animator")]
public class Animator : Behaviour
{
    private RuntimeAnimatorController _controller;
    private bool _applyRootMotion;
    private float _speed = 1.0f;
    private readonly Dictionary<int, float> _floatValues = new();
    private readonly Dictionary<int, int> _intValues = new();
    private readonly Dictionary<int, bool> _boolValues = new();
    private readonly HashSet<int> _triggers = new();
    private readonly Dictionary<int, AnimatorState> _currentStates = new();
    private readonly Dictionary<int, AnimatorState> _nextStates = new();
    private readonly Dictionary<int, float> _currentStateTimes = new();
    private readonly Dictionary<int, float> _transitionTimes = new();
    private readonly Dictionary<int, float> _transitionDurations = new();
    private readonly Dictionary<int, float> _stateSpeeds = new();
    private bool _initialized;
    private Vector3 _rootPosition;
    private Quaternion _rootRotation = Quaternion.identity;
    private Vector3 _deltaPosition;
    private Quaternion _deltaRotation = Quaternion.identity;
    private Vector3 _velocity;
    private Vector3 _angularVelocity;

    public RuntimeAnimatorController controller
    {
        get => _controller;
        set
        {
            _controller = value;
            InitializeParameters();
        }
    }

    public RuntimeAnimatorController runtimeAnimatorController
    {
        get => controller;
        set => controller = value;
    }

    public Avatar avatar { get; set; }

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

    public bool isHuman { get; set; }
    public bool isOptimizable { get; set; }
    public bool keepAnimatorControllerStateOnDisable { get; set; }
    public bool hasRootMotion { get; set; }
    public float humanScale { get; set; } = 1f;
    public bool isInitialized => _initialized;
    public bool hasAvatarMask { get; set; }
    public bool hasTransformHierarchy { get; } = true;
    public bool fireEvents { get; set; } = true;
    public bool animatePhysics { get; set; }
    public bool enabledAnimator { get; set; } = true;
    public bool stabilizeFeet { get; set; }
    public bool layersAffectMassCenter { get; set; }
    public int humanBoneCount { get; set; }
    public int transformCount { get; set; }
    public bool feetPivotActive { get; set; } = true;
    public Vector3 rootPosition => _rootPosition;
    public Quaternion rootRotation => _rootRotation;
    public Vector3 deltaPosition => _deltaPosition;
    public Quaternion deltaRotation => _deltaRotation;
    public Vector3 velocity => _velocity;
    public Vector3 angularVelocity => _angularVelocity;
    public Vector3 bodyPosition { get; set; }
    public Quaternion bodyRotation { get; set; }
    public Vector3 lookAtPosition { get; set; }
    public bool isMatchingTarget { get; private set; }
    public float playbackTime { get; set; }
    public float recorderStartTime { get; private set; }
    public float recorderStopTime { get; private set; }
    public float recorderDeltaTime { get; private set; }
    public int layerCount => _controller is AnimatorController ac ? ac.layerCount : 1;

    public AnimatorCullingMode cullingMode { get; set; }
    public AnimatorUpdateMode updateMode { get; set; }
    public AnimatorRecorderMode recorderMode { get; set; }

    public AnimatorControllerParameter[] parameters
    {
        get
        {
            if (_controller is AnimatorController ac) return ac.parameters;
            return Array.Empty<AnimatorControllerParameter>();
        }
    }

    public int parameterCount => parameters.Length;

    public AnimatorControllerParameter GetParameter(int index)
    {
        var list = parameters;
        if (index < 0 || index >= list.Length)
            return null;
        return list[index];
    }

    public static int StringToHash(string name) => name?.GetHashCode() ?? 0;

    public void SetFloat(string name, float value) => SetFloat(StringToHash(name), value);
    public void SetFloat(int id, float value)
    {
        _floatValues[id] = value;
    }

    public void SetFloat(string name, float value, float dampTime, float deltaTime)
    {
        if (dampTime <= 0f || deltaTime <= 0f)
        {
            SetFloat(name, value);
            return;
        }
        var current = GetFloat(name);
        var t = 1f - MathF.Exp(-deltaTime / dampTime);
        SetFloat(name, Mathf.Lerp(current, value, t));
    }

    public void SetFloat(int id, float value, float dampTime, float deltaTime)
    {
        if (dampTime <= 0f || deltaTime <= 0f)
        {
            SetFloat(id, value);
            return;
        }
        var current = GetFloat(id);
        var t = 1f - MathF.Exp(-deltaTime / dampTime);
        SetFloat(id, Mathf.Lerp(current, value, t));
    }

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
        return _nextStates.TryGetValue(layerIndex, out var next) && next != null;
    }

    public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex)
    {
        if (_currentStates.TryGetValue(layerIndex, out var state) && state != null)
        {
            float normTime = 0f;
            _currentStateTimes.TryGetValue(layerIndex, out normTime);
            float speed = 1f;
            _stateSpeeds.TryGetValue(layerIndex, out speed);
            return new AnimatorStateInfo(
                state.nameHash,
                state.nameHash,
                normTime,
                state.length,
                state.speed * speed,
                speed,
                state.tagHash,
                state.loop
            );
        }
        return default;
    }

    public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex)
    {
        if (_nextStates.TryGetValue(layerIndex, out var state) && state != null)
        {
            return new AnimatorStateInfo(
                state.nameHash,
                state.nameHash,
                0f,
                state.length,
                state.speed,
                1f,
                state.tagHash,
                state.loop
            );
        }
        return default;
    }

    public AnimatorTransitionInfo GetAnimatorTransitionInfo(int layerIndex)
    {
        if (IsInTransition(layerIndex))
        {
            float dur = 0f;
            _transitionDurations.TryGetValue(layerIndex, out dur);
            float time = 0f;
            _transitionTimes.TryGetValue(layerIndex, out time);
            return new AnimatorTransitionInfo();
        }
        return default;
    }

    public AnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layerIndex)
    {
        if (_currentStates.TryGetValue(layerIndex, out var state) && state?.motion is AnimationClip clip)
        {
            return new[] { new AnimatorClipInfo(clip, 1f) };
        }
        return Array.Empty<AnimatorClipInfo>();
    }

    public AnimatorClipInfo[] GetNextAnimatorClipInfo(int layerIndex)
    {
        if (_nextStates.TryGetValue(layerIndex, out var state) && state?.motion is AnimationClip clip)
        {
            return new[] { new AnimatorClipInfo(clip, 1f) };
        }
        return Array.Empty<AnimatorClipInfo>();
    }

    public void CrossFade(string stateName, float normalizedTransitionDuration, int layer = -1, float normalizedTimeOffset = 0, float normalizedTransitionTime = 0)
    {
        CrossFade(StringToHash(stateName), normalizedTransitionDuration, layer, normalizedTimeOffset, normalizedTransitionTime);
    }

    public void CrossFade(int stateHashName, float normalizedTransitionDuration, int layer = -1, float normalizedTimeOffset = 0, float normalizedTransitionTime = 0)
    {
        int layerIdx = layer < 0 ? 0 : layer;
        var state = FindState(stateHashName, layerIdx);
        if (state == null) return;

        if (_currentStates.TryGetValue(layerIdx, out var current) && current != null)
        {
            _nextStates[layerIdx] = state;
            _transitionDurations[layerIdx] = normalizedTransitionDuration;
            _transitionTimes[layerIdx] = normalizedTransitionDuration;
            _currentStateTimes[layerIdx] = normalizedTimeOffset < 0f ? 0f : normalizedTimeOffset;
            OnStateExit(current, layerIdx);
            OnStateEnter(state, layerIdx);
        }
        else
        {
            Play(stateHashName, layer, normalizedTimeOffset);
        }
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
        int layerIdx = layer < 0 ? 0 : layer;
        var state = FindState(stateNameHash, layerIdx);
        if (state == null) return;

        if (_currentStates.TryGetValue(layerIdx, out var oldState) && oldState != null)
        {
            OnStateExit(oldState, layerIdx);
        }

        _currentStates[layerIdx] = state;
        _currentStateTimes[layerIdx] = normalizedTime == float.NegativeInfinity ? 0f : normalizedTime;
        _nextStates[layerIdx] = null;
        _transitionTimes[layerIdx] = 0f;
        _stateSpeeds[layerIdx] = state.speed;
        OnStateEnter(state, layerIdx);
    }

    public void PlayInFixedTime(string stateName, int layer = -1, float fixedTime = 0) => Play(stateName, layer, fixedTime > 0f ? fixedTime : 0f);
    public void PlayInFixedTime(int stateNameHash, int layer = -1, float fixedTime = 0) => Play(stateNameHash, layer, fixedTime > 0f ? fixedTime : 0f);

    public void Rebind()
    {
        _initialized = true;
        InitializeDefaultState();
    }

    public void Update(float deltaTime)
    {
        if (!enabledAnimator || gameObject == null) return;

        InitializeIfNeeded();

        float dt = deltaTime * _speed;
        if (dt <= 0f) return;

        _deltaPosition = Vector3.zero;
        _deltaRotation = Quaternion.identity;

        int layers = layerCount;
        for (int i = 0; i < layers; i++)
        {
            UpdateLayer(i, dt);
        }
    }

    private void UpdateLayer(int layerIndex, float deltaTime)
    {
        if (!_currentStates.TryGetValue(layerIndex, out var currentState) || currentState == null) return;

        float stateTime = 0f;
        _currentStateTimes.TryGetValue(layerIndex, out stateTime);
        float stateSpeed = 1f;
        _stateSpeeds.TryGetValue(layerIndex, out stateSpeed);

        float stateLength = Math.Max(0.001f, currentState.length);

        if (_nextStates.TryGetValue(layerIndex, out var nextState) && nextState != null)
        {
            float transDuration = 0f;
            _transitionDurations.TryGetValue(layerIndex, out transDuration);
            float transTime = 0f;
            _transitionTimes.TryGetValue(layerIndex, out transTime);

            float actualTransDur = transDuration * stateLength;
            transTime -= deltaTime;
            _transitionTimes[layerIndex] = transTime;

            if (transTime <= 0f)
            {
                OnStateExit(currentState, layerIndex);
                _currentStates[layerIndex] = nextState;
                _nextStates[layerIndex] = null;
                _currentStateTimes[layerIndex] = 0f;
                _stateSpeeds[layerIndex] = nextState.speed;
                OnStateEnter(nextState, layerIndex);
                SampleState(nextState, 0f, 1f, layerIndex);
            }
            else
            {
                float t = 1f - (transTime / actualTransDur);
                t = Math.Clamp(t, 0f, 1f);

                stateTime += deltaTime * stateSpeed;
                if (currentState.loop)
                {
                    while (stateTime > stateLength) stateTime -= stateLength;
                    while (stateTime < 0f) stateTime += stateLength;
                }
                else
                {
                    stateTime = Math.Clamp(stateTime, 0f, stateLength);
                }
                _currentStateTimes[layerIndex] = stateTime;

                float nextTime = (actualTransDur - transTime) * nextState.speed;
                float nextLength = Math.Max(0.001f, nextState.length);
                if (nextState.loop)
                {
                    while (nextTime > nextLength) nextTime -= nextLength;
                }
                else
                {
                    nextTime = Math.Clamp(nextTime, 0f, nextLength);
                }

                SampleBlendStates(currentState, stateTime, 1f - t, nextState, nextTime, t, layerIndex);
            }
        }
        else
        {
            stateTime += deltaTime * stateSpeed;

            CheckTransitions(currentState, stateTime, stateLength, layerIndex);
            CheckAnyStateTransitions(layerIndex);

            if (!IsInTransition(layerIndex))
            {
                if (currentState.loop)
                {
                    while (stateTime > stateLength) stateTime -= stateLength;
                    while (stateTime < 0f) stateTime += stateLength;
                }
                else
                {
                    stateTime = Math.Clamp(stateTime, 0f, stateLength);
                }
                _currentStateTimes[layerIndex] = stateTime;
                SampleState(currentState, stateTime, 1f, layerIndex);
                OnStateUpdate(currentState, layerIndex);
            }
        }
    }

    private void CheckTransitions(AnimatorState state, float stateTime, float stateLength, int layerIndex)
    {
        if (state?.transitions == null) return;
        float normalizedTime = stateTime / stateLength;

        foreach (var transition in state.transitions)
        {
            if (transition.mute) continue;
            if (transition.hasExitTime && normalizedTime < transition.exitTime) continue;
            if (!CheckConditions(transition.conditions)) continue;

            if (transition.destinationState != null)
            {
                StartTransition(transition.destinationState, transition.duration, layerIndex);
                return;
            }
        }
    }

    private void CheckAnyStateTransitions(int layerIndex)
    {
        if (!(_controller is AnimatorController ac)) return;
        if (layerIndex >= ac.layers.Length) return;
        var layer = ac.layers[layerIndex];
        if (layer?.stateMachine?.anyStateTransitions == null) return;

        foreach (var transition in layer.stateMachine.anyStateTransitions)
        {
            if (transition.mute) continue;
            if (!CheckConditions(transition.conditions)) continue;
            if (transition.destinationState != null && _currentStates.TryGetValue(layerIndex, out var current) && transition.destinationState != current)
            {
                StartTransition(transition.destinationState, transition.duration, layerIndex);
                return;
            }
        }
    }

    private bool CheckConditions(List<AnimatorCondition> conditions)
    {
        if (conditions == null || conditions.Count == 0) return false;

        foreach (var cond in conditions)
        {
            int paramHash = cond.parameterHash != 0 ? cond.parameterHash : StringToHash(cond.parameter);
            float floatVal = GetFloat(paramHash);
            int intVal = GetInteger(paramHash);
            bool boolVal = GetBool(paramHash);
            bool triggerVal = _triggers.Contains(paramHash);

            bool result = cond.mode switch
            {
                AnimatorConditionMode.Greater => floatVal > cond.threshold,
                AnimatorConditionMode.Less => floatVal < cond.threshold,
                AnimatorConditionMode.Equals => Math.Abs(floatVal - cond.threshold) < 0.0001f || intVal == (int)cond.threshold,
                AnimatorConditionMode.NotEqual => Math.Abs(floatVal - cond.threshold) > 0.0001f && intVal != (int)cond.threshold,
                AnimatorConditionMode.If => boolVal || triggerVal,
                AnimatorConditionMode.IfNot => !boolVal && !triggerVal,
                _ => false
            };

            if (!result) return false;

            if (cond.mode == AnimatorConditionMode.If && triggerVal)
            {
                _triggers.Remove(paramHash);
            }
        }
        return true;
    }

    private void StartTransition(AnimatorState destination, float normalizedDuration, int layerIndex)
    {
        if (_currentStates.TryGetValue(layerIndex, out var current) && current != null)
        {
            OnStateExit(current, layerIndex);
        }
        _nextStates[layerIndex] = destination;
        _transitionDurations[layerIndex] = normalizedDuration;
        _transitionTimes[layerIndex] = normalizedDuration;
        OnStateEnter(destination, layerIndex);
    }

    private void SampleState(AnimatorState state, float time, float weight, int layerIndex)
    {
        if (state?.motion == null || gameObject == null) return;

        if (state.motion is AnimationClip clip)
        {
            if (weight >= 0.999f)
            {
                clip.SampleAnimation(gameObject, time);
            }
            else
            {
                SampleClipWithWeight(clip, time, weight);
            }
        }
        else if (state.motion is BlendTree bt)
        {
            SampleBlendTree(bt, time, weight);
        }
    }

    private void SampleBlendStates(AnimatorState state1, float time1, float weight1, AnimatorState state2, float time2, float weight2, int layerIndex)
    {
        if (gameObject == null) return;

        SampleState(state1, time1, weight1, layerIndex);
        SampleState(state2, time2, weight2, layerIndex);
    }

    private void SampleClipWithWeight(AnimationClip clip, float time, float weight)
    {
    }

    private void SampleBlendTree(BlendTree bt, float time, float weight)
    {
        if (bt == null || bt.children == null || bt.children.Length == 0 || gameObject == null) return;

        float x = GetFloat(bt.blendParameter);
        float y = GetFloat(bt.blendParameterY);
        float[] weights = new float[bt.children.Length];
        bt.ComputeBlendTreeWeights(x, y, weights);

        float totalActive = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            if (bt.children[i].motion is AnimationClip && weights[i] > 0f)
            {
                totalActive += weights[i];
            }
        }

        if (totalActive > 0f && Math.Abs(weight - 1f) < 0.001f)
        {
            if (totalActive > 0.999f && weights.Count(w => w > 0.01f) == 1)
            {
                for (int i = 0; i < bt.children.Length; i++)
                {
                    if (bt.children[i].motion is AnimationClip clip && weights[i] > 0.01f)
                    {
                        clip.SampleAnimation(gameObject, time);
                        break;
                    }
                }
            }
        }
    }

    private AnimatorState FindState(int hash, int layerIndex)
    {
        if (_controller is AnimatorController ac && layerIndex < ac.layers.Length)
        {
            var layer = ac.layers[layerIndex];
            return layer?.stateMachine?.FindStateByHash(hash);
        }
        return null;
    }

    private void InitializeIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;
        InitializeParameters();
        InitializeDefaultState();
    }

    private void InitializeParameters()
    {
        if (!(_controller is AnimatorController ac)) return;
        foreach (var param in ac.parameters)
        {
            if (param == null) continue;
            int hash = param.nameHash;
            switch (param.type)
            {
                case AnimatorControllerParameterType.Float:
                    if (!_floatValues.ContainsKey(hash)) _floatValues[hash] = param.defaultFloat;
                    break;
                case AnimatorControllerParameterType.Int:
                    if (!_intValues.ContainsKey(hash)) _intValues[hash] = param.defaultInt;
                    break;
                case AnimatorControllerParameterType.Bool:
                    if (!_boolValues.ContainsKey(hash)) _boolValues[hash] = param.defaultBool;
                    break;
            }
        }
        ac.CollectClips();
    }

    private void InitializeDefaultState()
    {
        if (!(_controller is AnimatorController ac)) return;
        for (int i = 0; i < ac.layers.Length; i++)
        {
            var layer = ac.layers[i];
            if (layer?.stateMachine?.defaultState != null)
            {
                _currentStates[i] = layer.stateMachine.defaultState;
                _currentStateTimes[i] = 0f;
                _stateSpeeds[i] = layer.stateMachine.defaultState.speed;
                OnStateEnter(layer.stateMachine.defaultState, i);
            }
            else if (layer?.stateMachine?.entryTransitions != null && layer.stateMachine.entryTransitions.Count > 0)
            {
                var entry = layer.stateMachine.entryTransitions[0];
                if (entry.destinationState != null)
                {
                    _currentStates[i] = entry.destinationState;
                    _currentStateTimes[i] = 0f;
                    _stateSpeeds[i] = entry.destinationState.speed;
                    OnStateEnter(entry.destinationState, i);
                }
            }
        }
    }

    private void OnStateEnter(AnimatorState state, int layerIndex)
    {
        if (state?.behaviours == null) return;
        var stateInfo = GetCurrentAnimatorStateInfo(layerIndex);
        foreach (var behaviour in state.behaviours)
        {
            behaviour?.OnStateEnter(this, stateInfo, layerIndex);
        }
    }

    private void OnStateUpdate(AnimatorState state, int layerIndex)
    {
        if (state?.behaviours == null) return;
        var stateInfo = GetCurrentAnimatorStateInfo(layerIndex);
        foreach (var behaviour in state.behaviours)
        {
            behaviour?.OnStateUpdate(this, stateInfo, layerIndex);
        }
    }

    private void OnStateExit(AnimatorState state, int layerIndex)
    {
        if (state?.behaviours == null) return;
        var stateInfo = GetCurrentAnimatorStateInfo(layerIndex);
        foreach (var behaviour in state.behaviours)
        {
            behaviour?.OnStateExit(this, stateInfo, layerIndex);
        }
    }

    public void StartPlayback() { recorderMode = AnimatorRecorderMode.Playback; }
    public void StartRecording(int frameCount) { recorderMode = AnimatorRecorderMode.Record; recorderStartTime = playbackTime; }
    public void StopPlayback() { recorderMode = AnimatorRecorderMode.Offline; }
    public void StopRecording() { recorderMode = AnimatorRecorderMode.Offline; recorderStopTime = playbackTime; }

    public int GetLayerCount() => layerCount;
    public string GetLayerName(int layerIndex)
    {
        if (_controller is AnimatorController ac) return ac.GetLayerName(layerIndex);
        return string.Empty;
    }

    public int GetLayerIndex(string layerName)
    {
        if (_controller is AnimatorController ac) return ac.GetLayerIndex(layerName);
        return -1;
    }

    public float GetLayerWeight(int layerIndex)
    {
        if (_controller is AnimatorController ac && layerIndex < ac.layers.Length)
            return ac.layers[layerIndex].weight;
        return layerIndex == 0 ? 1f : 0f;
    }

    public void SetLayerWeight(int layerIndex, float weight)
    {
        if (_controller is AnimatorController ac && layerIndex < ac.layers.Length)
            ac.layers[layerIndex].weight = weight;
    }

    public void SetLookAtPosition(Vector3 lookAtPosition) { this.lookAtPosition = lookAtPosition; }
    public void SetLookAtWeight(float weight) { }
    public void SetLookAtWeight(float weight, float bodyWeight) { }
    public void SetLookAtWeight(float weight, float bodyWeight, float headWeight) { }
    public void SetLookAtWeight(float weight, float bodyWeight, float headWeight, float eyesWeight) { }
    public void SetLookAtWeight(float weight, float bodyWeight, float headWeight, float eyesWeight, float clampWeight) { }
    public bool LookAtPosition(Vector3 position, float weight) => false;

    public Transform GetBoneTransform(HumanBodyBones humanBoneId) => null;
    public void SetBoneLocalRotation(HumanBodyBones humanBoneId, Quaternion rotation) { }
    public void ApplyBuiltinRootMotion() { }

    public bool HasState(int layerIndex, int stateID)
    {
        return FindState(stateID, layerIndex) != null;
    }

    public void MatchTarget(Vector3 matchPosition, Quaternion matchRotation, AvatarTarget targetBodyPart, MatchTargetWeightMask weightMask, float startNormalizedTime, float targetNormalizedTime = 1)
    {
        isMatchingTarget = true;
    }

    public void InterruptMatchTarget(bool completeMatch = true)
    {
        isMatchingTarget = false;
    }

    public void SetTarget(AvatarTarget targetIndex, float targetNormalizedTime) { }
    public TargetRotation GetTargetRotation() => default;
    public TargetPosition GetTargetPosition() => default;

    public Vector3 IkPosition(AvatarIKGoal goal) => Vector3.zero;
    public Quaternion IkRotation(AvatarIKGoal goal) => Quaternion.identity;
    public void SetIkPosition(AvatarIKGoal goal, Vector3 position) { }
    public void SetIkRotation(AvatarIKGoal goal, Quaternion rotation) { }
    public void SetIkPositionWeight(AvatarIKGoal goal, float value) { }
    public void SetIkRotationWeight(AvatarIKGoal goal, float value) { }
    public void SetIkHint(AvatarIKHint hint, Vector3 position) { }
    public void SetIkHintWeight(AvatarIKHint hint, float value) { }
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

public abstract class RuntimeAnimatorController : Object
{
    public abstract AnimationClip[] animationClips { get; }
}
