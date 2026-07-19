using System;
using System.Collections.Generic;

namespace UnityEngine;

public struct AnimatorStateInfo
{
    public int fullPathHash { get; }
    public int shortNameHash { get; }
    public float normalizedTime { get; internal set; }
    public float length { get; }
    public float speed { get; }
    public float speedMultiplier { get; }
    public int tagHash { get; }
    public bool loop { get; }

    public AnimatorStateInfo(int fullPathHash, int shortNameHash, float normalizedTime, float length, float speed, float speedMultiplier, int tagHash, bool loop)
    {
        this.fullPathHash = fullPathHash;
        this.shortNameHash = shortNameHash;
        this.normalizedTime = normalizedTime;
        this.length = length;
        this.speed = speed;
        this.speedMultiplier = speedMultiplier;
        this.tagHash = tagHash;
        this.loop = loop;
    }

    public bool IsName(string name)
    {
        return shortNameHash == Animator.StringToHash(name) || fullPathHash == Animator.StringToHash(name);
    }

    public bool IsTag(string tag)
    {
        return tagHash == Animator.StringToHash(tag);
    }
}

public struct AnimatorClipInfo
{
    public AnimationClip clip { get; }
    public float weight { get; }

    public AnimatorClipInfo(AnimationClip clip, float weight)
    {
        this.clip = clip;
        this.weight = weight;
    }
}

public struct AnimatorTransitionInfo
{
    public int fullPathHash { get; }
    public int userNameHash { get; }
    public int nameHash { get; }
    public bool hasExitTime { get; }
    public float normalizedTime { get; }
    public float duration { get; }
    public bool anyState { get; }

    public AnimatorTransitionInfo(int fullPathHash, int userNameHash, int nameHash, bool hasExitTime, float normalizedTime, float duration, bool anyState)
    {
        this.fullPathHash = fullPathHash;
        this.userNameHash = userNameHash;
        this.nameHash = nameHash;
        this.hasExitTime = hasExitTime;
        this.normalizedTime = normalizedTime;
        this.duration = duration;
        this.anyState = anyState;
    }

    public bool IsName(string name)
    {
        return fullPathHash == Animator.StringToHash(name) || nameHash == Animator.StringToHash(name);
    }

    public bool IsUserName(string name)
    {
        return userNameHash == Animator.StringToHash(name);
    }
}

public class AnimatorControllerParameter
{
    public string name { get; set; } = string.Empty;
    public AnimatorControllerParameterType type { get; set; }
    public float defaultFloat { get; set; }
    public int defaultInt { get; set; }
    public bool defaultBool { get; set; }
    public int nameHash => Animator.StringToHash(name);

    public AnimatorControllerParameter()
    {
    }

    public AnimatorControllerParameter(string name, AnimatorControllerParameterType type)
    {
        this.name = name;
        this.type = type;
    }
}

public enum AnimatorControllerParameterType
{
    Float = 1,
    Int = 3,
    Bool = 4,
    Trigger = 9
}

public enum AnimatorConditionMode
{
    Greater = 1,
    Less = 2,
    Equals = 3,
    NotEqual = 4,
    If = 5,
    IfNot = 6
}

public struct AnimatorCondition
{
    public AnimatorConditionMode mode;
    public string parameter;
    public float threshold;
    public int parameterHash;
}

public abstract class AnimatorTransitionBase : Object
{
    public bool canTransitionToSelf { get; set; } = true;
    public bool solo { get; set; }
    public bool mute { get; set; }
    public bool isExit { get; set; }
    public AnimatorStateMachine destinationStateMachine { get; set; }
    public AnimatorState destinationState { get; set; }
    public List<AnimatorCondition> conditions { get; set; } = new();
    public string name { get; set; } = string.Empty;
    public bool hasExitTime { get; set; }
    public float exitTime { get; set; } = 0.75f;
    public float duration { get; set; } = 0.25f;
    public bool hasFixedDuration { get; set; } = true;
    public bool fixedDuration { get => hasFixedDuration; set => hasFixedDuration = value; }
    public float offset { get; set; }
    public TransitionInterruptionSource interruptionSource { get; set; }
    public bool orderedInterruption { get; set; } = true;
}

public class AnimatorStateTransition : AnimatorTransitionBase
{
}

public class AnimatorTransition : AnimatorTransitionBase
{
}

public enum TransitionInterruptionSource
{
    None = 0,
    CurrentState = 1,
    NextState = 2,
    CurrentStateThenNextState = 3,
    NextStateThenCurrentState = 4
}

public enum AnimatorLayerBlendingMode
{
    Override = 0,
    Additive = 1
}

public class AnimatorState : Object
{
    public string name { get; set; } = string.Empty;
    public float cycleOffset { get; set; }
    public float speed { get; set; } = 1f;
    public string speedParameter { get; set; } = string.Empty;
    public bool speedParameterActive { get; set; }
    public Motion motion { get; set; }
    public List<AnimatorStateTransition> transitions { get; set; } = new();
    public List<StateMachineBehaviour> behaviours { get; set; } = new();
    public bool iKOnFeet { get; set; }
    public bool writeDefaultValues { get; set; } = true;
    public string tag { get; set; } = string.Empty;
    public bool mirror { get; set; }
    public string mirrorParameter { get; set; } = string.Empty;
    public bool mirrorParameterActive { get; set; }
    public int nameHash => Animator.StringToHash(name);
    public int tagHash => Animator.StringToHash(tag);
    public float length
    {
        get
        {
            if (motion is AnimationClip clip) return clip.length;
            if (motion is BlendTree bt) return bt.averageDuration;
            return 1f;
        }
    }
    public bool loop
    {
        get
        {
            return motion?.isLooping == true;
        }
    }

    public AnimatorStateTransition AddTransition(AnimatorState destinationState)
    {
        var transition = new AnimatorStateTransition { destinationState = destinationState };
        transitions.Add(transition);
        return transition;
    }

    public AnimatorStateTransition AddTransition(AnimatorState destinationState, float exitTime)
    {
        var transition = new AnimatorStateTransition
        {
            destinationState = destinationState,
            hasExitTime = true,
            exitTime = exitTime
        };
        transitions.Add(transition);
        return transition;
    }

    public AnimatorStateTransition AddExitTransition()
    {
        var transition = new AnimatorStateTransition { isExit = true };
        transitions.Add(transition);
        return transition;
    }

    public AnimatorStateTransition AddExitTransition(float exitTime)
    {
        var transition = new AnimatorStateTransition
        {
            isExit = true,
            hasExitTime = true,
            exitTime = exitTime
        };
        transitions.Add(transition);
        return transition;
    }

    public void AddStateMachineBehaviour(StateMachineBehaviour behaviour)
    {
        if (behaviour != null) behaviours.Add(behaviour);
    }

    public T AddStateMachineBehaviour<T>() where T : StateMachineBehaviour, new()
    {
        var behaviour = new T();
        behaviours.Add(behaviour);
        return behaviour;
    }
}

public struct ChildAnimatorState
{
    public Vector3 position;
    public AnimatorState state;
}

public struct ChildAnimatorStateMachine
{
    public Vector3 position;
    public AnimatorStateMachine stateMachine;
}

public class AnimatorStateMachine : Object
{
    public string name { get; set; } = string.Empty;
    public List<ChildAnimatorState> states { get; set; } = new();
    public List<ChildAnimatorStateMachine> stateMachines { get; set; } = new();
    public List<AnimatorStateTransition> anyStateTransitions { get; set; } = new();
    public List<AnimatorStateTransition> entryTransitions { get; set; } = new();
    public List<AnimatorTransition> stateMachineTransitions { get; set; } = new();
    public AnimatorState defaultState { get; set; }

    public AnimatorState AddState(string name)
    {
        return AddState(name, Vector3.zero);
    }

    public AnimatorState AddState(string name, Vector3 position)
    {
        var state = new AnimatorState { name = name };
        states.Add(new ChildAnimatorState { position = position, state = state });
        if (defaultState == null) defaultState = state;
        return state;
    }

    public void RemoveState(AnimatorState state)
    {
        if (state == null) return;
        for (int i = states.Count - 1; i >= 0; i--)
        {
            if (states[i].state == state)
            {
                states.RemoveAt(i);
                break;
            }
        }
        if (defaultState == state)
        {
            defaultState = states.Count > 0 ? states[0].state : null;
        }
    }

    public AnimatorStateMachine AddStateMachine(string name)
    {
        return AddStateMachine(name, Vector3.zero);
    }

    public AnimatorStateMachine AddStateMachine(string name, Vector3 position)
    {
        var sm = new AnimatorStateMachine { name = name };
        stateMachines.Add(new ChildAnimatorStateMachine { position = position, stateMachine = sm });
        return sm;
    }

    public AnimatorStateTransition AddAnyStateTransition(AnimatorState destinationState)
    {
        var transition = new AnimatorStateTransition { destinationState = destinationState };
        anyStateTransitions.Add(transition);
        return transition;
    }

    public AnimatorStateTransition AddAnyStateTransition(AnimatorState destinationState, float exitTime)
    {
        var transition = new AnimatorStateTransition
        {
            destinationState = destinationState,
            hasExitTime = true,
            exitTime = exitTime
        };
        anyStateTransitions.Add(transition);
        return transition;
    }

    public AnimatorStateTransition AddEntryTransition(AnimatorState destinationState)
    {
        var transition = new AnimatorStateTransition { destinationState = destinationState };
        entryTransitions.Add(transition);
        return transition;
    }

    public AnimatorTransition AddStateMachineTransition(AnimatorStateMachine destinationStateMachine)
    {
        var transition = new AnimatorTransition { destinationStateMachine = destinationStateMachine };
        stateMachineTransitions.Add(transition);
        return transition;
    }

    public AnimatorState FindState(string name)
    {
        foreach (var cs in states)
        {
            if (cs.state != null && cs.state.name == name) return cs.state;
        }
        foreach (var csm in stateMachines)
        {
            if (csm.stateMachine != null)
            {
                var found = csm.stateMachine.FindState(name);
                if (found != null) return found;
            }
        }
        return null;
    }

    public AnimatorState FindStateByHash(int hash)
    {
        foreach (var cs in states)
        {
            if (cs.state != null && cs.state.nameHash == hash) return cs.state;
        }
        foreach (var csm in stateMachines)
        {
            if (csm.stateMachine != null)
            {
                var found = csm.stateMachine.FindStateByHash(hash);
                if (found != null) return found;
            }
        }
        return null;
    }
}

public class AnimatorControllerLayer
{
    public string name { get; set; } = string.Empty;
    public AnimatorStateMachine stateMachine { get; set; } = new();
    public AnimatorLayerBlendingMode blendingMode { get; set; } = AnimatorLayerBlendingMode.Override;
    public float weight { get; set; } = 1f;
    public AvatarMask avatarMask { get; set; }
    public bool iKPass { get; set; }
    public bool defaultWeight { get; set; } = true;
    public int layerIndex { get; set; }
}

public enum HumanBodyBones
{
    Hips = 0,
    LeftUpperLeg = 1,
    RightUpperLeg = 2,
    LeftLowerLeg = 3,
    RightLowerLeg = 4,
    LeftFoot = 5,
    RightFoot = 6,
    Spine = 7,
    Chest = 8,
    UpperChest = 54,
    Neck = 9,
    Head = 10,
    LeftShoulder = 11,
    RightShoulder = 12,
    LeftUpperArm = 13,
    RightUpperArm = 14,
    LeftLowerArm = 15,
    RightLowerArm = 16,
    LeftHand = 17,
    RightHand = 18,
    LeftToes = 19,
    RightToes = 20,
    LeftEye = 21,
    RightEye = 22,
    Jaw = 23,
    LeftThumbProximal = 24,
    LeftThumbIntermediate = 25,
    LeftThumbDistal = 26,
    LeftIndexProximal = 27,
    LeftIndexIntermediate = 28,
    LeftIndexDistal = 29,
    LeftMiddleProximal = 30,
    LeftMiddleIntermediate = 31,
    LeftMiddleDistal = 32,
    LeftRingProximal = 33,
    LeftRingIntermediate = 34,
    LeftRingDistal = 35,
    LeftLittleProximal = 36,
    LeftLittleIntermediate = 37,
    LeftLittleDistal = 38,
    RightThumbProximal = 39,
    RightThumbIntermediate = 40,
    RightThumbDistal = 41,
    RightIndexProximal = 42,
    RightIndexIntermediate = 43,
    RightIndexDistal = 44,
    RightMiddleProximal = 45,
    RightMiddleIntermediate = 46,
    RightMiddleDistal = 47,
    RightRingProximal = 48,
    RightRingIntermediate = 49,
    RightRingDistal = 50,
    RightLittleProximal = 51,
    RightLittleIntermediate = 52,
    RightLittleDistal = 53,
    LastBone = 55
}

public enum AvatarIKGoal
{
    LeftFoot = 0,
    RightFoot = 1,
    LeftHand = 2,
    RightHand = 3
}

public enum AvatarIKHint
{
    LeftKnee = 0,
    RightKnee = 1,
    LeftElbow = 2,
    RightElbow = 3
}

public enum AnimatorCullingMode
{
    AlwaysAnimate = 0,
    CullUpdateTransforms = 1,
    CullCompletely = 2
}

public enum AnimatorUpdateMode
{
    Normal = 0,
    AnimatePhysics = 1,
    UnscaledTime = 2
}

public enum AnimatorRecorderMode
{
    Offline = 0,
    Playback = 1,
    Record = 2
}

public enum AnimatorPlaybackPolicy
{
    Once,
    Loop,
    PingPong
}

public class AnimatorOverrideController : RuntimeAnimatorController
{
    private RuntimeAnimatorController _runtimeController;
    private Dictionary<AnimationClip, AnimationClip> _overrides = new();

    public string name { get; set; } = string.Empty;

    public RuntimeAnimatorController runtimeAnimatorController
    {
        get => _runtimeController;
        set
        {
            _runtimeController = value;
            RebuildClipsList();
        }
    }

    private List<AnimationClip> _originalClips = new();
    public override AnimationClip[] animationClips => _originalClips.ToArray();
    public int overridesCount => _overrides.Count;

    public AnimatorOverrideController()
    {
    }

    public AnimatorOverrideController(RuntimeAnimatorController controller)
    {
        _runtimeController = controller;
        RebuildClipsList();
    }

    public AnimationClip this[AnimationClip originalClip]
    {
        get
        {
            _overrides.TryGetValue(originalClip, out var overrideClip);
            return overrideClip;
        }
        set
        {
            if (originalClip != null && value != null)
            {
                _overrides[originalClip] = value;
            }
        }
    }

    public AnimationClip this[string originalClipName]
    {
        get
        {
            foreach (var clip in _originalClips)
            {
                if (clip != null && clip.name == originalClipName)
                {
                    _overrides.TryGetValue(clip, out var overrideClip);
                    return overrideClip;
                }
            }
            return null;
        }
        set
        {
            foreach (var clip in _originalClips)
            {
                if (clip != null && clip.name == originalClipName)
                {
                    if (value != null) _overrides[clip] = value;
                    return;
                }
            }
        }
    }

    public void GetOverrides(List<KeyValuePair<AnimationClip, AnimationClip>> overrides)
    {
        if (overrides == null) return;
        overrides.Clear();
        foreach (var original in _originalClips)
        {
            AnimationClip overrideClip = null;
            _overrides.TryGetValue(original, out overrideClip);
            overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(original, overrideClip));
        }
    }

    public void ApplyOverrides(IList<KeyValuePair<AnimationClip, AnimationClip>> overrides)
    {
        if (overrides == null) return;
        _overrides.Clear();
        foreach (var pair in overrides)
        {
            if (pair.Key != null && pair.Value != null)
            {
                _overrides[pair.Key] = pair.Value;
            }
        }
    }

    private void RebuildClipsList()
    {
        _originalClips.Clear();
        if (_runtimeController is AnimatorController ac)
        {
            foreach (var clip in ac.animationClips)
            {
                _originalClips.Add(clip);
            }
        }
    }
}

public class AnimatorController : RuntimeAnimatorController
{
    private readonly List<AnimatorControllerLayer> _layers = new();
    private readonly List<AnimatorControllerParameter> _parameters = new();
    private readonly List<AnimationClip> _clips = new();

    public string name { get; set; } = string.Empty;
    public AnimatorControllerLayer[] layers => _layers.ToArray();
    public AnimatorControllerParameter[] parameters => _parameters.ToArray();
    public override AnimationClip[] animationClips => _clips.ToArray();
    public int layerCount => _layers.Count;
    public int parameterCount => _parameters.Count;

    public AnimatorController()
    {
        AddLayer("Base Layer");
    }

    public AnimatorControllerParameter GetParameter(int index)
    {
        if (index >= 0 && index < _parameters.Count) return _parameters[index];
        return null;
    }

    public string GetLayerName(int layerIndex)
    {
        if (layerIndex >= 0 && layerIndex < _layers.Count) return _layers[layerIndex].name;
        return string.Empty;
    }

    public int GetLayerIndex(string layerName)
    {
        for (int i = 0; i < _layers.Count; i++)
        {
            if (_layers[i].name == layerName) return i;
        }
        return -1;
    }

    public AnimatorControllerLayer AddLayer(string name)
    {
        var layer = new AnimatorControllerLayer
        {
            name = name,
            layerIndex = _layers.Count,
            stateMachine = new AnimatorStateMachine { name = name }
        };
        _layers.Add(layer);
        return layer;
    }

    public void AddParameter(string name, AnimatorControllerParameterType type)
    {
        var param = new AnimatorControllerParameter { name = name, type = type };
        _parameters.Add(param);
    }

    public void AddParameter(AnimatorControllerParameter parameter)
    {
        if (parameter != null) _parameters.Add(parameter);
    }

    public AnimatorState AddMotion(Motion motion)
    {
        if (motion is AnimationClip clip && !_clips.Contains(clip))
        {
            _clips.Add(clip);
        }

        if (_layers.Count > 0 && _layers[0].stateMachine != null)
        {
            var state = _layers[0].stateMachine.AddState(motion != null ? motion.name : "State");
            state.motion = motion;
            return state;
        }
        return null;
    }

    public AnimatorState AddAnimationClip(AnimationClip clip, string stateName)
    {
        if (clip != null && !_clips.Contains(clip))
        {
            _clips.Add(clip);
        }

        if (_layers.Count > 0 && _layers[0].stateMachine != null)
        {
            var state = _layers[0].stateMachine.AddState(stateName);
            state.motion = clip;
            return state;
        }
        return null;
    }

    internal void CollectClips()
    {
        _clips.Clear();
        foreach (var layer in _layers)
        {
            CollectClipsFromStateMachine(layer.stateMachine);
        }
    }

    private void CollectClipsFromStateMachine(AnimatorStateMachine sm)
    {
        if (sm == null) return;
        foreach (var cs in sm.states)
        {
            if (cs.state?.motion is AnimationClip clip && !_clips.Contains(clip))
            {
                _clips.Add(clip);
            }
            else if (cs.state?.motion is BlendTree bt)
            {
                CollectClipsFromBlendTree(bt);
            }
        }
        foreach (var csm in sm.stateMachines)
        {
            CollectClipsFromStateMachine(csm.stateMachine);
        }
    }

    private void CollectClipsFromBlendTree(BlendTree bt)
    {
        if (bt == null) return;
        foreach (var child in bt.children)
        {
            if (child.motion is AnimationClip clip && !_clips.Contains(clip))
            {
                _clips.Add(clip);
            }
            else if (child.motion is BlendTree childBt)
            {
                CollectClipsFromBlendTree(childBt);
            }
        }
    }
}
