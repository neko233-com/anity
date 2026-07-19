using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
    private readonly Dictionary<int, float> _nextStateTimes = new();
    private readonly Dictionary<int, float> _transitionTimes = new();
    private readonly Dictionary<int, float> _transitionDurations = new();
    private readonly Dictionary<int, float> _transitionDurationSeconds = new();
    private bool _initialized;
    private Vector3 _rootPosition;
    private Quaternion _rootRotation = Quaternion.identity;
    private Vector3 _deltaPosition;
    private Quaternion _deltaRotation = Quaternion.identity;
    private Vector3 _velocity;
    private Vector3 _angularVelocity;
    private bool _rootMotionInitialized;
    private AnimationRootMotionPose _rootMotionAnchor = AnimationRootMotionPose.Identity;
    private AnimationRootMotionPose _previousRootMotion = AnimationRootMotionPose.Identity;
    private float _lookAtWeight;
    private float _bodyWeight;
    private float _headWeight;
    private float _eyesWeight;
    private float _clampWeight;
    private readonly Dictionary<HumanBodyBones, Quaternion> _boneRotations = new();
    private readonly Dictionary<HumanBodyBones, Transform> _boneTransforms = new();
    private AvatarTarget _target;
    private float _targetNormalizedTime;
    private TargetRotation _targetRotation;
    private TargetPosition _targetPosition;
    private readonly Dictionary<AvatarIKGoal, IkData> _ikGoals = new();
    private readonly Dictionary<AvatarIKHint, IkHintData> _ikHints = new();

    private readonly struct SampledAnimationPose
    {
        public SampledAnimationPose(AnimationPose pose, AnimationFloatPose floatProperties,
            AnimationPose? additiveReferencePose = null,
            AnimationFloatPose? additiveReferenceFloatProperties = null,
            AnimationRootMotionPose? rootMotion = null)
        {
            Pose = pose;
            FloatProperties = floatProperties;
            AdditiveReferencePose = additiveReferencePose;
            AdditiveReferenceFloatProperties = additiveReferenceFloatProperties;
            RootMotion = rootMotion;
        }

        public AnimationPose Pose { get; }
        public AnimationFloatPose FloatProperties { get; }
        public AnimationPose? AdditiveReferencePose { get; }
        public AnimationFloatPose? AdditiveReferenceFloatProperties { get; }
        public AnimationRootMotionPose? RootMotion { get; }
        public static SampledAnimationPose Empty => new(new AnimationPose(), new AnimationFloatPose());
    }

    public RuntimeAnimatorController controller
    {
        get => _controller;
        set
        {
            _controller = value;
            ClearStateMachineRuntime();
            _initialized = false;
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
        set
        {
            if (_applyRootMotion == value) return;
            _applyRootMotion = value;
            ResetRootMotionRuntime();
        }
    }

    public float speed
    {
        get => _speed;
        set => _speed = value;
    }

    public bool isHuman { get; set; }
    public bool isOptimizable { get; set; }
    public bool keepAnimatorControllerStateOnDisable { get; set; }
    public bool hasRootMotion => HasHumanoidRootMotion();
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
            _currentStateTimes.TryGetValue(layerIndex, out float stateTime);
            float stateLength = Math.Max(0.000001f, state.length);
            return new AnimatorStateInfo(
                state.nameHash,
                state.nameHash,
                stateTime / stateLength,
                state.length,
                state.speed,
                1f,
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
            _nextStateTimes.TryGetValue(layerIndex, out float stateTime);
            float stateLength = Math.Max(0.000001f, state.length);
            return new AnimatorStateInfo(
                state.nameHash,
                state.nameHash,
                stateTime / stateLength,
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
        if (IsInTransition(layerIndex) && _nextStates.TryGetValue(layerIndex, out var nextState))
        {
            _transitionDurations.TryGetValue(layerIndex, out float reportedDuration);
            _transitionDurationSeconds.TryGetValue(layerIndex, out float durationSeconds);
            _transitionTimes.TryGetValue(layerIndex, out float elapsedTime);
            float normalizedTime = durationSeconds > 0f ? elapsedTime / durationSeconds : 1f;
            return new AnimatorTransitionInfo(
                nextState.nameHash,
                nextState.nameHash,
                nextState.nameHash,
                true,
                normalizedTime,
                reportedDuration,
                false
            );
        }
        return default;
    }

    public AnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layerIndex)
    {
        if (_currentStates.TryGetValue(layerIndex, out var state) && state?.motion is AnimationClip clip)
        {
            float weight = 1f - TransitionWeight(layerIndex);
            return new[] { new AnimatorClipInfo(clip, weight) };
        }
        return Array.Empty<AnimatorClipInfo>();
    }

    public AnimatorClipInfo[] GetNextAnimatorClipInfo(int layerIndex)
    {
        if (_nextStates.TryGetValue(layerIndex, out var state) && state?.motion is AnimationClip clip)
        {
            return new[] { new AnimatorClipInfo(clip, TransitionWeight(layerIndex)) };
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
        InitializeIfNeeded();
        var state = FindState(stateHashName, layerIdx);
        if (state == null) return;

        if (_currentStates.TryGetValue(layerIdx, out var current) && current != null)
        {
            float currentLength = Math.Max(0f, current.length);
            float seconds = Math.Max(0f, normalizedTransitionDuration) * currentLength;
            float destinationTime = float.IsNegativeInfinity(normalizedTimeOffset)
                ? 0f : Math.Max(0f, normalizedTimeOffset) * Math.Max(0f, state.length);
            BeginCrossFade(state, layerIdx, normalizedTransitionDuration, seconds,
                destinationTime, normalizedTransitionTime);
        }
        else
        {
            Play(stateHashName, layer, normalizedTimeOffset);
        }
    }

    public void CrossFadeInFixedTime(string stateName, float fixedTransitionDuration, int layer = -1, float fixedTimeOffset = 0, float normalizedTransitionTime = 0)
    {
        CrossFadeInFixedTime(StringToHash(stateName), fixedTransitionDuration, layer, fixedTimeOffset, normalizedTransitionTime);
    }

    public void CrossFadeInFixedTime(int stateHashName, float fixedTransitionDuration, int layer = -1, float fixedTimeOffset = 0, float normalizedTransitionTime = 0)
    {
        int layerIdx = layer < 0 ? 0 : layer;
        InitializeIfNeeded();
        var state = FindState(stateHashName, layerIdx);
        if (state == null) return;
        if (_currentStates.TryGetValue(layerIdx, out var current) && current != null)
        {
            float seconds = Math.Max(0f, fixedTransitionDuration);
            BeginCrossFade(state, layerIdx, fixedTransitionDuration, seconds,
                Math.Max(0f, fixedTimeOffset), normalizedTransitionTime);
        }
        else PlayInFixedTime(stateHashName, layer, fixedTimeOffset);
    }

    public void Play(string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity)
    {
        Play(StringToHash(stateName), layer, normalizedTime);
    }

    public void Play(int stateNameHash, int layer = -1, float normalizedTime = float.NegativeInfinity)
    {
        int layerIdx = layer < 0 ? 0 : layer;
        InitializeIfNeeded();
        var state = FindState(stateNameHash, layerIdx);
        if (state == null) return;
        float time = float.IsNegativeInfinity(normalizedTime)
            ? ExistingOrZeroTime(layerIdx, state)
            : normalizedTime * Math.Max(0f, state.length);
        PlayState(state, layerIdx, time);
    }

    public void PlayInFixedTime(string stateName, int layer = -1, float fixedTime = 0)
        => PlayInFixedTime(StringToHash(stateName), layer, fixedTime);

    public void PlayInFixedTime(int stateNameHash, int layer = -1, float fixedTime = 0)
    {
        int layerIdx = layer < 0 ? 0 : layer;
        InitializeIfNeeded();
        var state = FindState(stateNameHash, layerIdx);
        if (state == null) return;
        PlayState(state, layerIdx, Math.Max(0f, fixedTime));
    }

    public void Rebind()
    {
        _initialized = true;
        ClearStateMachineRuntime();
        InitializeDefaultState();
        ResetRootMotionRuntime();
    }

    public void Update(float deltaTime)
    {
        if (!enabledAnimator || gameObject == null) return;

        InitializeIfNeeded();

        float dt = deltaTime * _speed;
        if (dt < 0f) return;

        _deltaPosition = Vector3.zero;
        _deltaRotation = Quaternion.identity;
        _velocity = Vector3.zero;
        _angularVelocity = Vector3.zero;
        EnsureRootMotionTracking();

        var accumulatedPose = new AnimationPose();
        var accumulatedFloatProperties = new AnimationFloatPose();
        AnimationRootMotionPose? accumulatedRootMotion = null;
        int layers = layerCount;
        for (int i = 0; i < layers; i++)
        {
            SampledAnimationPose layerPose = UpdateLayer(i, dt);
            accumulatedPose = ComposeLayer(accumulatedPose, layerPose, i);
            accumulatedFloatProperties = ComposeFloatLayer(accumulatedFloatProperties, layerPose, i);
            accumulatedRootMotion = ComposeRootMotionLayer(accumulatedRootMotion, layerPose, i);
        }
        accumulatedPose.Apply();
        accumulatedFloatProperties.Apply();
        ApplyRootMotion(accumulatedRootMotion, dt);
    }

    private SampledAnimationPose UpdateLayer(int layerIndex, float deltaTime)
    {
        if (!_currentStates.TryGetValue(layerIndex, out var currentState) || currentState == null)
            return SampledAnimationPose.Empty;

        _currentStateTimes.TryGetValue(layerIndex, out float stateTime);
        float stateLength = Math.Max(0.001f, currentState.length);

        if (_nextStates.TryGetValue(layerIndex, out var nextState) && nextState != null)
        {
            _transitionDurationSeconds.TryGetValue(layerIndex, out float durationSeconds);
            _transitionTimes.TryGetValue(layerIndex, out float elapsedTime);
            _nextStateTimes.TryGetValue(layerIndex, out float nextTime);
            stateTime += deltaTime * currentState.speed;
            nextTime += deltaTime * nextState.speed;
            elapsedTime += deltaTime;
            _currentStateTimes[layerIndex] = stateTime;
            _nextStateTimes[layerIndex] = nextTime;
            _transitionTimes[layerIndex] = elapsedTime;

            if (durationSeconds <= 0f || elapsedTime >= durationSeconds)
            {
                OnStateExit(currentState, layerIndex);
                _currentStates[layerIndex] = nextState;
                _currentStateTimes[layerIndex] = nextTime;
                ClearTransition(layerIndex);
                return SampleState(nextState, nextTime);
            }
            float weight = Math.Clamp(elapsedTime / durationSeconds, 0f, 1f);
            return SampleBlendStates(currentState, stateTime, nextState, nextTime, weight);
        }
        else
        {
            stateTime += deltaTime * currentState.speed;
            _currentStateTimes[layerIndex] = stateTime;

            CheckTransitions(currentState, stateTime, stateLength, layerIndex);
            CheckAnyStateTransitions(layerIndex);

            if (!IsInTransition(layerIndex))
            {
                OnStateUpdate(currentState, layerIndex);
                return SampleState(currentState, stateTime);
            }
            if (_nextStates.TryGetValue(layerIndex, out nextState) && nextState != null)
            {
                _nextStateTimes.TryGetValue(layerIndex, out float nextTime);
                return SampleBlendStates(currentState, stateTime, nextState, nextTime,
                    TransitionWeight(layerIndex));
            }
        }
        return SampledAnimationPose.Empty;
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
                StartTransition(transition, layerIndex);
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
                StartTransition(transition, layerIndex);
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

    private void StartTransition(AnimatorStateTransition transition, int layerIndex)
    {
        AnimatorState destination = transition.destinationState;
        if (destination is null || !_currentStates.TryGetValue(layerIndex, out var current) || current is null) return;
        float durationSeconds = transition.hasFixedDuration
            ? Math.Max(0f, transition.duration)
            : Math.Max(0f, transition.duration) * Math.Max(0f, current.length);
        float destinationTime = transition.offset * Math.Max(0f, destination.length);
        BeginCrossFade(destination, layerIndex, transition.duration, durationSeconds,
            destinationTime, 0f);
    }

    private void BeginCrossFade(AnimatorState destination, int layerIndex, float reportedDuration,
        float durationSeconds, float destinationTime, float normalizedTransitionTime)
    {
        float progress = float.IsFinite(normalizedTransitionTime)
            ? Math.Clamp(normalizedTransitionTime, 0f, 1f) : 0f;
        _nextStates[layerIndex] = destination;
        _nextStateTimes[layerIndex] = destinationTime;
        _transitionDurations[layerIndex] = reportedDuration;
        _transitionDurationSeconds[layerIndex] = durationSeconds;
        _transitionTimes[layerIndex] = durationSeconds * progress;
        OnStateEnter(destination, layerIndex);
    }

    private float TransitionWeight(int layerIndex)
    {
        if (!_nextStates.ContainsKey(layerIndex)) return 0f;
        _transitionDurationSeconds.TryGetValue(layerIndex, out float durationSeconds);
        _transitionTimes.TryGetValue(layerIndex, out float elapsedTime);
        return durationSeconds > 0f ? Math.Clamp(elapsedTime / durationSeconds, 0f, 1f) : 1f;
    }

    private float ExistingOrZeroTime(int layerIndex, AnimatorState state)
        => _currentStates.TryGetValue(layerIndex, out var current) && ReferenceEquals(current, state) &&
           _currentStateTimes.TryGetValue(layerIndex, out float time) ? time : 0f;

    private void PlayState(AnimatorState state, int layerIndex, float time)
    {
        if (_currentStates.TryGetValue(layerIndex, out var oldState) && oldState != null)
            OnStateExit(oldState, layerIndex);
        _currentStates[layerIndex] = state;
        _currentStateTimes[layerIndex] = time;
        ClearTransition(layerIndex);
        OnStateEnter(state, layerIndex);
        ResetRootMotionRuntime();
    }

    private void ClearTransition(int layerIndex)
    {
        _nextStates.Remove(layerIndex);
        _nextStateTimes.Remove(layerIndex);
        _transitionTimes.Remove(layerIndex);
        _transitionDurations.Remove(layerIndex);
        _transitionDurationSeconds.Remove(layerIndex);
    }

    private SampledAnimationPose SampleState(AnimatorState state, float time)
    {
        return state?.motion is null || gameObject is null
            ? SampledAnimationPose.Empty
            : SampleMotion(state.motion, time + state.cycleOffset * Math.Max(0f, state.length));
    }

    private SampledAnimationPose SampleBlendStates(
        AnimatorState state1,
        float time1,
        AnimatorState state2,
        float time2,
        float weight2)
    {
        SampledAnimationPose first = SampleState(state1, time1);
        SampledAnimationPose second = SampleState(state2, time2);
        AnimationPose pose = AnimationPose.Blend(first.Pose, second.Pose, weight2);
        AnimationFloatPose properties = AnimationFloatPose.Blend(first.FloatProperties, second.FloatProperties, weight2);
        AnimationPose? reference = first.AdditiveReferencePose is not null && second.AdditiveReferencePose is not null
            ? AnimationPose.Blend(first.AdditiveReferencePose, second.AdditiveReferencePose, weight2)
            : null;
        AnimationFloatPose? referenceProperties = first.AdditiveReferenceFloatProperties is not null && second.AdditiveReferenceFloatProperties is not null
            ? AnimationFloatPose.Blend(first.AdditiveReferenceFloatProperties, second.AdditiveReferenceFloatProperties, weight2)
            : null;
        AnimationRootMotionPose? rootMotion = first.RootMotion.HasValue && second.RootMotion.HasValue
            ? AnimationRootMotionPose.Blend(first.RootMotion.Value, second.RootMotion.Value, weight2)
            : first.RootMotion ?? second.RootMotion;
        return new SampledAnimationPose(pose, properties, reference, referenceProperties, rootMotion);
    }

    private SampledAnimationPose SampleMotion(Motion motion, float time)
    {
        if (motion is AnimationClip clip && gameObject is not null)
        {
            AnimationPose pose = clip.EvaluateTransformPose(gameObject, time, true);
            AnimationFloatPose properties = clip.EvaluateFloatProperties(gameObject, time, true);
            clip.TryEvaluateAdditiveReferencePose(gameObject, out AnimationPose referencePose);
            clip.TryEvaluateAdditiveReferenceFloatProperties(gameObject, out AnimationFloatPose referenceProperties);
            AnimationRootMotionPose? rootMotion = clip.TryEvaluateRootMotion(time, out AnimationRootMotionPose sampledRoot)
                ? sampledRoot : null;
            return new SampledAnimationPose(pose, properties, referencePose.Count > 0 ? referencePose : null,
                referenceProperties.Count > 0 ? referenceProperties : null, rootMotion);
        }
        if (motion is BlendTree blendTree) return SampleBlendTree(blendTree, time);
        return SampledAnimationPose.Empty;
    }

    private SampledAnimationPose SampleBlendTree(BlendTree bt, float time)
    {
        if (bt == null || bt.children == null || bt.children.Length == 0 || gameObject == null)
            return SampledAnimationPose.Empty;

        float x = GetFloat(bt.blendParameter);
        float y = GetFloat(bt.blendParameterY);
        float[] weights = new float[bt.children.Length];
        bt.ComputeBlendTreeWeights(x, y, weights);
        var accumulated = new AnimationPose();
        var accumulatedProperties = new AnimationFloatPose();
        AnimationPose? accumulatedReference = null;
        AnimationFloatPose? accumulatedReferenceProperties = null;
        AnimationRootMotionPose? accumulatedRootMotion = null;
        float accumulatedWeight = 0f;
        bool allHaveTransformReference = true;
        bool allHaveFloatReference = true;
        for (int i = 0; i < weights.Length; i++)
        {
            float childWeight = weights[i];
            Motion childMotion = bt.children[i].motion;
            if (childMotion is null || childWeight <= 0f) continue;
            SampledAnimationPose child = SampleMotion(
                childMotion,
                (time + bt.children[i].cycleOffset * Math.Max(0.001f, childMotion.averageDuration)) * bt.children[i].timeScale);
            float combinedWeight = accumulatedWeight + childWeight;
            float blendWeight = accumulatedWeight <= 0f ? 1f : childWeight / combinedWeight;
            accumulated = AnimationPose.Blend(accumulated, child.Pose, blendWeight);
            accumulatedProperties = AnimationFloatPose.Blend(accumulatedProperties, child.FloatProperties, blendWeight);
            if (child.RootMotion.HasValue)
                accumulatedRootMotion = accumulatedRootMotion.HasValue
                    ? AnimationRootMotionPose.Blend(
                        accumulatedRootMotion.Value, child.RootMotion.Value, blendWeight)
                    : child.RootMotion;
            if (child.AdditiveReferencePose is null)
            {
                allHaveTransformReference = false;
            }
            else if (allHaveTransformReference)
            {
                accumulatedReference = accumulatedReference is null
                    ? child.AdditiveReferencePose.Clone()
                    : AnimationPose.Blend(accumulatedReference, child.AdditiveReferencePose, blendWeight);
            }
            if (child.AdditiveReferenceFloatProperties is null) allHaveFloatReference = false;
            else if (allHaveFloatReference)
                accumulatedReferenceProperties = accumulatedReferenceProperties is null
                    ? child.AdditiveReferenceFloatProperties.Clone()
                    : AnimationFloatPose.Blend(accumulatedReferenceProperties, child.AdditiveReferenceFloatProperties, blendWeight);
            accumulatedWeight = combinedWeight;
        }
        return new SampledAnimationPose(accumulated, accumulatedProperties,
            allHaveTransformReference ? accumulatedReference : null,
            allHaveFloatReference ? accumulatedReferenceProperties : null,
            accumulatedRootMotion);
    }

    private AnimationPose ComposeLayer(AnimationPose accumulated, SampledAnimationPose sampled, int layerIndex)
    {
        if (sampled.Pose.Count == 0) return accumulated;
        if (!(_controller is AnimatorController controller) || layerIndex <= 0 || layerIndex >= controller.layers.Length)
            return AnimationPose.Blend(accumulated, sampled.Pose, 1f);

        AnimatorControllerLayer layer = controller.layers[layerIndex];
        float weight = float.IsNaN(layer.weight)
            ? 0f
            : layer.weight < 0f || layer.weight > 1f
                ? 1f
                : layer.weight;
        if (weight <= 0f) return accumulated;
        Func<string, bool>? pathActive = layer.avatarMask is null
            ? null
            : layer.avatarMask.IsTransformPathActive;
        if (layer.blendingMode == AnimatorLayerBlendingMode.Additive)
        {
            if (sampled.AdditiveReferencePose is null) return accumulated;
            return AnimationPose.Blend(
                accumulated,
                sampled.Pose,
                weight,
                additive: true,
                referencePose: sampled.AdditiveReferencePose,
                pathActive: pathActive);
        }
        return AnimationPose.Blend(accumulated, sampled.Pose, weight, pathActive: pathActive);
    }

    private AnimationFloatPose ComposeFloatLayer(AnimationFloatPose accumulated, SampledAnimationPose sampled, int layerIndex)
    {
        if (sampled.FloatProperties.Count == 0) return accumulated;
        if (!(_controller is AnimatorController controller) || layerIndex <= 0 || layerIndex >= controller.layers.Length)
            return AnimationFloatPose.Blend(accumulated, sampled.FloatProperties, 1f);
        AnimatorControllerLayer layer = controller.layers[layerIndex];
        float weight = float.IsNaN(layer.weight) ? 0f : layer.weight < 0f || layer.weight > 1f ? 1f : layer.weight;
        if (weight <= 0f) return accumulated;
        Func<string, bool>? pathActive = layer.avatarMask is null ? null : layer.avatarMask.IsTransformPathActive;
        if (layer.blendingMode == AnimatorLayerBlendingMode.Additive)
        {
            if (sampled.AdditiveReferenceFloatProperties is null) return accumulated;
            return AnimationFloatPose.Blend(accumulated, sampled.FloatProperties, weight, true,
                sampled.AdditiveReferenceFloatProperties, pathActive);
        }
        return AnimationFloatPose.Blend(accumulated, sampled.FloatProperties, weight, pathActive: pathActive);
    }

    private AnimationRootMotionPose? ComposeRootMotionLayer(
        AnimationRootMotionPose? accumulated,
        SampledAnimationPose sampled,
        int layerIndex)
    {
        if (!sampled.RootMotion.HasValue) return accumulated;
        if (!(_controller is AnimatorController controller) || layerIndex <= 0 || layerIndex >= controller.layers.Length)
            return sampled.RootMotion;
        AnimatorControllerLayer layer = controller.layers[layerIndex];
        float weight = float.IsNaN(layer.weight)
            ? 0f
            : layer.weight < 0f || layer.weight > 1f ? 1f : layer.weight;
        if (weight <= 0f || layer.blendingMode == AnimatorLayerBlendingMode.Additive) return accumulated;
        if (layer.avatarMask is not null && !layer.avatarMask.IsTransformPathActive(string.Empty)) return accumulated;
        return accumulated.HasValue
            ? AnimationRootMotionPose.Blend(accumulated.Value, sampled.RootMotion.Value, weight)
            : sampled.RootMotion;
    }

    private void EnsureRootMotionTracking()
    {
        if (_rootMotionInitialized || transform is null) return;
        AnimationRootMotionPose? rootMotion = SampleCurrentRootMotion();
        if (!rootMotion.HasValue) return;
        _rootMotionAnchor = AnimationRootMotionPose.CalculateAnchor(
            new AnimationRootMotionPose(transform.position, transform.rotation),
            rootMotion.Value);
        _previousRootMotion = rootMotion.Value;
        _rootMotionInitialized = true;
        _rootPosition = transform.position;
        _rootRotation = transform.rotation;
    }

    private AnimationRootMotionPose? SampleCurrentRootMotion()
    {
        AnimationRootMotionPose? accumulated = null;
        for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
        {
            if (!_currentStates.TryGetValue(layerIndex, out AnimatorState? currentState) || currentState is null)
                continue;
            _currentStateTimes.TryGetValue(layerIndex, out float currentTime);
            SampledAnimationPose sampled;
            if (_nextStates.TryGetValue(layerIndex, out AnimatorState? nextState) && nextState is not null)
            {
                _nextStateTimes.TryGetValue(layerIndex, out float nextTime);
                sampled = SampleBlendStates(
                    currentState, currentTime, nextState, nextTime, TransitionWeight(layerIndex));
            }
            else sampled = SampleState(currentState, currentTime);
            accumulated = ComposeRootMotionLayer(accumulated, sampled, layerIndex);
        }
        return accumulated;
    }

    private void ApplyRootMotion(AnimationRootMotionPose? sampledRootMotion, float deltaTime)
    {
        if (transform is null) return;
        if (!sampledRootMotion.HasValue)
        {
            _rootMotionInitialized = false;
            _rootPosition = transform.position;
            _rootRotation = transform.rotation;
            return;
        }
        if (!_rootMotionInitialized)
        {
            _rootMotionAnchor = AnimationRootMotionPose.CalculateAnchor(
                new AnimationRootMotionPose(transform.position, transform.rotation),
                sampledRootMotion.Value);
            _previousRootMotion = sampledRootMotion.Value;
            _rootMotionInitialized = true;
        }

        AnimationRootMotionPose previousWorld = AnimationRootMotionPose.Anchor(
            _rootMotionAnchor, _previousRootMotion);
        AnimationRootMotionPose currentWorld = AnimationRootMotionPose.Anchor(
            _rootMotionAnchor, sampledRootMotion.Value);
        AnimationRootMotionDelta delta = AnimationRootMotionPose.CalculateDelta(
            previousWorld, currentWorld, deltaTime);

        if (_applyRootMotion)
        {
            transform.position = currentWorld.Position;
            transform.rotation = currentWorld.Rotation;
            _deltaPosition = delta.Position;
            _deltaRotation = delta.Rotation;
            _velocity = delta.Velocity;
            _angularVelocity = delta.AngularVelocity;
        }
        _previousRootMotion = sampledRootMotion.Value;
        _rootPosition = transform.position;
        _rootRotation = transform.rotation;
    }

    private bool HasHumanoidRootMotion()
    {
        if (!isHuman || _controller is null) return false;
        foreach (AnimationClip clip in _controller.animationClips)
            if (clip is not null && clip.hasMotionCurves) return true;
        return false;
    }

    private void ResetRootMotionRuntime()
    {
        _rootMotionInitialized = false;
        _rootMotionAnchor = AnimationRootMotionPose.Identity;
        _previousRootMotion = AnimationRootMotionPose.Identity;
        _deltaPosition = Vector3.zero;
        _deltaRotation = Quaternion.identity;
        _velocity = Vector3.zero;
        _angularVelocity = Vector3.zero;
        if (transform is not null)
        {
            _rootPosition = transform.position;
            _rootRotation = transform.rotation;
        }
        else
        {
            _rootPosition = Vector3.zero;
            _rootRotation = Quaternion.identity;
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
                OnStateEnter(layer.stateMachine.defaultState, i);
            }
            else if (layer?.stateMachine?.entryTransitions != null && layer.stateMachine.entryTransitions.Count > 0)
            {
                var entry = layer.stateMachine.entryTransitions[0];
                if (entry.destinationState != null)
                {
                    _currentStates[i] = entry.destinationState;
                    _currentStateTimes[i] = 0f;
                    OnStateEnter(entry.destinationState, i);
                }
            }
        }
    }

    private void ClearStateMachineRuntime()
    {
        _currentStates.Clear();
        _currentStateTimes.Clear();
        _nextStates.Clear();
        _nextStateTimes.Clear();
        _transitionTimes.Clear();
        _transitionDurations.Clear();
        _transitionDurationSeconds.Clear();
        ResetRootMotionRuntime();
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
    public void SetLookAtWeight(float weight)
    {
        _lookAtWeight = Mathf.Clamp01(weight);
        _bodyWeight = 0f;
        _headWeight = 1f;
        _eyesWeight = 0f;
        _clampWeight = 0.5f;
    }

    public void SetLookAtWeight(float weight, float bodyWeight)
    {
        _lookAtWeight = Mathf.Clamp01(weight);
        _bodyWeight = Mathf.Clamp01(bodyWeight);
        _headWeight = 1f;
        _eyesWeight = 0f;
        _clampWeight = 0.5f;
    }

    public void SetLookAtWeight(float weight, float bodyWeight, float headWeight)
    {
        _lookAtWeight = Mathf.Clamp01(weight);
        _bodyWeight = Mathf.Clamp01(bodyWeight);
        _headWeight = Mathf.Clamp01(headWeight);
        _eyesWeight = 0f;
        _clampWeight = 0.5f;
    }

    public void SetLookAtWeight(float weight, float bodyWeight, float headWeight, float eyesWeight)
    {
        _lookAtWeight = Mathf.Clamp01(weight);
        _bodyWeight = Mathf.Clamp01(bodyWeight);
        _headWeight = Mathf.Clamp01(headWeight);
        _eyesWeight = Mathf.Clamp01(eyesWeight);
        _clampWeight = 0.5f;
    }

    public void SetLookAtWeight(float weight, float bodyWeight, float headWeight, float eyesWeight, float clampWeight)
    {
        _lookAtWeight = Mathf.Clamp01(weight);
        _bodyWeight = Mathf.Clamp01(bodyWeight);
        _headWeight = Mathf.Clamp01(headWeight);
        _eyesWeight = Mathf.Clamp01(eyesWeight);
        _clampWeight = Mathf.Clamp01(clampWeight);
    }

    public bool LookAtPosition(Vector3 position, float weight) => false;

    public Transform GetBoneTransform(HumanBodyBones humanBoneId)
    {
        _boneTransforms.TryGetValue(humanBoneId, out var t);
        return t;
    }

    public void SetBoneLocalRotation(HumanBodyBones humanBoneId, Quaternion rotation)
    {
        _boneRotations[humanBoneId] = rotation;
    }

    public void ApplyBuiltinRootMotion()
    {
        if (transform != null)
        {
            transform.position += _deltaPosition;
            transform.rotation = _deltaRotation * transform.rotation;
            _rootPosition = transform.position;
            _rootRotation = transform.rotation;
        }
    }

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

    public void SetTarget(AvatarTarget targetIndex, float targetNormalizedTime)
    {
        _target = targetIndex;
        _targetNormalizedTime = targetNormalizedTime;
    }

    public TargetRotation GetTargetRotation()
    {
        return _targetRotation;
    }

    public TargetPosition GetTargetPosition()
    {
        return _targetPosition;
    }

    public Vector3 IkPosition(AvatarIKGoal goal)
    {
        if (_ikGoals.TryGetValue(goal, out var data))
            return data.position;
        return Vector3.zero;
    }

    public Quaternion IkRotation(AvatarIKGoal goal)
    {
        if (_ikGoals.TryGetValue(goal, out var data))
            return data.rotation;
        return Quaternion.identity;
    }

    public void SetIkPosition(AvatarIKGoal goal, Vector3 position)
    {
        if (!_ikGoals.TryGetValue(goal, out var data))
        {
            data = new IkData();
            _ikGoals[goal] = data;
        }
        data.position = position;
    }

    public void SetIkRotation(AvatarIKGoal goal, Quaternion rotation)
    {
        if (!_ikGoals.TryGetValue(goal, out var data))
        {
            data = new IkData();
            _ikGoals[goal] = data;
        }
        data.rotation = rotation;
    }

    public void SetIkPositionWeight(AvatarIKGoal goal, float value)
    {
        if (!_ikGoals.TryGetValue(goal, out var data))
        {
            data = new IkData();
            _ikGoals[goal] = data;
        }
        data.positionWeight = Mathf.Clamp01(value);
    }

    public void SetIkRotationWeight(AvatarIKGoal goal, float value)
    {
        if (!_ikGoals.TryGetValue(goal, out var data))
        {
            data = new IkData();
            _ikGoals[goal] = data;
        }
        data.rotationWeight = Mathf.Clamp01(value);
    }

    public void SetIkHint(AvatarIKHint hint, Vector3 position)
    {
        if (!_ikHints.TryGetValue(hint, out var data))
        {
            data = new IkHintData();
            _ikHints[hint] = data;
        }
        data.position = position;
    }

    public void SetIkHintWeight(AvatarIKHint hint, float value)
    {
        if (!_ikHints.TryGetValue(hint, out var data))
        {
            data = new IkHintData();
            _ikHints[hint] = data;
        }
        data.weight = Mathf.Clamp01(value);
    }
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

public struct IkData
{
    public Vector3 position;
    public Quaternion rotation;
    public float positionWeight;
    public float rotationWeight;
}

public struct IkHintData
{
    public Vector3 position;
    public float weight;
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
    private static readonly ConditionalWeakTable<RuntimeAnimatorController, AnimationClipProvider> ClipProviders = new();
    public virtual AnimationClip[] animationClips => ClipProviders.TryGetValue(this, out var provider) ? provider.GetClips() : Array.Empty<AnimationClip>();

    internal static void SetAnimationClipProvider(RuntimeAnimatorController controller, Func<AnimationClip[]> getClips)
    {
        ClipProviders.Remove(controller);
        ClipProviders.Add(controller, new AnimationClipProvider(getClips));
    }

    private sealed class AnimationClipProvider
    {
        private readonly Func<AnimationClip[]> _getClips;
        public AnimationClipProvider(Func<AnimationClip[]> getClips) => _getClips = getClips;
        public AnimationClip[] GetClips() => _getClips();
    }
}
