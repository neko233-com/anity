using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Bindings = UnityEngine.Bindings;

namespace UnityEditor.Animations;

public enum AnimatorConditionMode { If = 1, IfNot = 2, Greater = 3, Less = 4, Equals = 6, NotEqual = 7 }
public enum AnimatorLayerBlendingMode { Override = 0, Additive = 1 }
public enum TransitionInterruptionSource { None = 0, Source = 1, Destination = 2, SourceThenDestination = 3, DestinationThenSource = 4 }
public enum BlendTreeType { Simple1D = 0, SimpleDirectional2D = 1, FreeformDirectional2D = 2, FreeformCartesian2D = 3, Direct = 4 }

[Bindings.NativeHeader("Editor/Src/Animation/Transition.h")]
public struct AnimatorCondition
{
    public AnimatorConditionMode mode { get; set; }
    public string? parameter { get; set; }
    public float threshold { get; set; }
}

[Bindings.NativeHeader("Editor/Src/Animation/StateMachine.bindings.h")]
[Bindings.NativeHeader("Editor/Src/Animation/StateMachine.h")]
[UnityEngine.Scripting.RequiredByNativeCode]
public struct ChildAnimatorState
{
    public Vector3 position { get; set; }
    public AnimatorState? state { get; set; }
}

[Bindings.NativeHeader("Editor/Src/Animation/StateMachine.bindings.h")]
[Bindings.NativeHeader("Editor/Src/Animation/StateMachine.h")]
[UnityEngine.Scripting.RequiredByNativeCode]
public struct ChildAnimatorStateMachine
{
    public Vector3 position { get; set; }
    public AnimatorStateMachine? stateMachine { get; set; }
}

[Bindings.NativeType("Editor/Src/Animation/BlendTree.h")]
public struct ChildMotion
{
    public Motion? motion { get; set; }
    public float threshold { get; set; }
    public Vector2 position { get; set; }
    public float timeScale { get; set; }
    public float cycleOffset { get; set; }
    public string? directBlendParameter { get; set; }
    public bool mirror { get; set; }
}

[Bindings.NativeHeader("Editor/Src/Animation/BlendTree.bindings.h")]
[Bindings.NativeType("Editor/Src/Animation/BlendTree.h")]
[UnityEngine.ExcludeFromPreset]
public class BlendTree : Motion
{
    private readonly List<ChildMotion> _children = new();
    public string blendParameter { get; set; } = string.Empty;
    public string blendParameterY { get; set; } = string.Empty;
    public BlendTreeType blendType { get; set; }
    public float minThreshold { get; set; }
    public float maxThreshold { get; set; } = 1f;
    public bool useAutomaticThresholds { get; set; } = true;
    public ChildMotion[] children { get => _children.ToArray(); set { _children.Clear(); if (value != null) _children.AddRange(value); } }

    public void AddChild(Motion motion) => AddChild(motion, 0f);
    public void AddChild(Motion motion, float threshold) { if (motion != null) _children.Add(new ChildMotion { motion = motion, threshold = threshold, timeScale = 1f, directBlendParameter = string.Empty }); }
    public void AddChild(Motion motion, Vector2 position) { if (motion != null) _children.Add(new ChildMotion { motion = motion, position = position, timeScale = 1f, directBlendParameter = string.Empty }); }
    public BlendTree CreateBlendTreeChild(float threshold) { var child = new BlendTree(); AddChild(child, threshold); return child; }
    public BlendTree CreateBlendTreeChild(Vector2 position) { var child = new BlendTree(); AddChild(child, position); return child; }
    public void RemoveChild(int index) { if (index >= 0 && index < _children.Count) _children.RemoveAt(index); }
}

[Bindings.NativeHeader("Editor/Src/Animation/Transition.h")]
[Bindings.NativeHeader("Modules/Animation/MecanimUtility.h")]
public class AnimatorTransitionBase : Object
{
    private readonly List<AnimatorCondition> _conditions = new();
    public AnimatorState? destinationState { get; set; }
    public AnimatorStateMachine? destinationStateMachine { get; set; }
    public bool isExit { get; set; }
    public bool mute { get; set; }
    public bool solo { get; set; }
    protected AnimatorTransitionBase() { }
    public AnimatorCondition[] conditions { get => _conditions.ToArray(); set { _conditions.Clear(); if (value != null) _conditions.AddRange(value); } }

    public void AddCondition(AnimatorConditionMode mode, float threshold, string parameter) => _conditions.Add(new AnimatorCondition { mode = mode, threshold = threshold, parameter = parameter ?? string.Empty });
    public void RemoveCondition(AnimatorCondition condition) => _conditions.Remove(condition);
    public string GetDisplayName(Object source) => !string.IsNullOrEmpty(name) ? name : isExit ? "Exit" : destinationState?.name ?? destinationStateMachine?.name ?? source?.name ?? string.Empty;
}

[Bindings.NativeHeader("Editor/Src/Animation/StateMachine.bindings.h")]
[Bindings.NativeHeader("Editor/Src/Animation/Transition.h")]
public class AnimatorStateTransition : AnimatorTransitionBase
{
    public bool canTransitionToSelf { get; set; } = true;
    public float duration { get; set; } = .25f;
    public float exitTime { get; set; } = .75f;
    public bool hasExitTime { get; set; }
    public bool hasFixedDuration { get; set; } = true;
    public TransitionInterruptionSource interruptionSource { get; set; }
    public float offset { get; set; }
    public bool orderedInterruption { get; set; } = true;
}

[Bindings.NativeHeader("Editor/Src/Animation/StateMachine.bindings.h")]
[Bindings.NativeHeader("Editor/Src/Animation/Transition.h")]
public class AnimatorTransition : AnimatorTransitionBase { }

[Bindings.NativeHeader("Editor/Src/Animation/StateMachine.bindings.h")]
[Bindings.NativeHeader("Editor/Src/Animation/StateMachine.h")]
[Bindings.NativeHeader("Editor/Src/Animation/StateMachineBehaviourScripting.h")]
public sealed class AnimatorState : Object
{
    private readonly List<AnimatorStateTransition> _transitions = new();
    private readonly List<StateMachineBehaviour> _behaviours = new();
    public float cycleOffset { get; set; }
    public string cycleOffsetParameter { get; set; } = string.Empty;
    public bool cycleOffsetParameterActive { get; set; }
    public bool iKOnFeet { get; set; }
    public bool mirror { get; set; }
    public string mirrorParameter { get; set; } = string.Empty;
    public bool mirrorParameterActive { get; set; }
    public Motion? motion { get; set; }
    public int nameHash => Animator.StringToHash(name);
    public float speed { get; set; } = 1f;
    public string speedParameter { get; set; } = string.Empty;
    public bool speedParameterActive { get; set; }
    public string tag { get; set; } = string.Empty;
    public string timeParameter { get; set; } = string.Empty;
    public bool timeParameterActive { get; set; }
    public AnimatorStateTransition[] transitions { get => _transitions.ToArray(); set { _transitions.Clear(); if (value != null) _transitions.AddRange(value.Where(transition => transition != null)); } }
    [Obsolete("uniqueName does not exist anymore. Consider using .name instead.", true)] public string uniqueName => name;
    [Obsolete("uniqueNameHash does not exist anymore.", true)] public int uniqueNameHash => Animator.StringToHash(name);
    public bool writeDefaultValues { get; set; } = true;
    public StateMachineBehaviour[] behaviours { get => _behaviours.ToArray(); set { _behaviours.Clear(); if (value != null) _behaviours.AddRange(value.Where(behaviour => behaviour != null)); } }

    public AnimatorStateTransition AddTransition(AnimatorState destinationState) => AddTransition(destinationState, false);
    public AnimatorStateTransition AddTransition(AnimatorState destinationState, bool defaultExitTime) => AddTransitionInternal(destinationState, null, defaultExitTime);
    public AnimatorStateTransition AddTransition(AnimatorStateMachine destinationStateMachine) => AddTransition(destinationStateMachine, false);
    public AnimatorStateTransition AddTransition(AnimatorStateMachine destinationStateMachine, bool defaultExitTime) => AddTransitionInternal(null, destinationStateMachine, defaultExitTime);
    public void AddTransition(AnimatorStateTransition transition) { if (transition != null && !_transitions.Contains(transition)) _transitions.Add(transition); }
    public AnimatorStateTransition AddExitTransition() => AddExitTransition(false);
    public AnimatorStateTransition AddExitTransition(bool defaultExitTime) { var transition = new AnimatorStateTransition { isExit = true, hasExitTime = defaultExitTime }; _transitions.Add(transition); return transition; }
    public void RemoveTransition(AnimatorStateTransition transition) { if (transition != null) _transitions.Remove(transition); }
    [Obsolete("GetMotion() is obsolete. Use motion", true)]
    public Motion? GetMotion() => motion;
    [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
    public StateMachineBehaviour AddStateMachineBehaviour(Type stateMachineBehaviourType)
    {
        if (stateMachineBehaviourType == null || !typeof(StateMachineBehaviour).IsAssignableFrom(stateMachineBehaviourType)) throw new ArgumentException("Type must inherit StateMachineBehaviour.", nameof(stateMachineBehaviourType));
        var behaviour = (StateMachineBehaviour)Activator.CreateInstance(stateMachineBehaviourType)!; _behaviours.Add(behaviour); return behaviour;
    }
    public T AddStateMachineBehaviour<T>() where T : StateMachineBehaviour { var behaviour = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!; _behaviours.Add(behaviour); return behaviour; }
    private AnimatorStateTransition AddTransitionInternal(AnimatorState? state, AnimatorStateMachine? stateMachine, bool defaultExitTime)
    { var transition = new AnimatorStateTransition { destinationState = state, destinationStateMachine = stateMachine, hasExitTime = defaultExitTime }; _transitions.Add(transition); return transition; }
}

[Bindings.NativeHeader("Editor/Src/Animation/StateMachine.bindings.h")]
[Bindings.NativeHeader("Editor/Src/Animation/StateMachine.h")]
[Bindings.NativeHeader("Editor/Src/Animation/StateMachineBehaviourScripting.h")]
public sealed class AnimatorStateMachine : Object
{
    private readonly List<ChildAnimatorState> _states = new();
    private readonly List<ChildAnimatorStateMachine> _stateMachines = new();
    private readonly List<AnimatorStateTransition> _anyStateTransitions = new();
    private readonly List<AnimatorTransition> _entryTransitions = new();
    private readonly List<StateMachineBehaviour> _behaviours = new();
    private readonly Dictionary<AnimatorStateMachine, List<AnimatorTransition>> _stateMachineTransitions = new();
    public Vector3 anyStatePosition { get; set; } = new(20, 250, 0);
    public AnimatorStateTransition[] anyStateTransitions { get => _anyStateTransitions.ToArray(); set { _anyStateTransitions.Clear(); if (value != null) _anyStateTransitions.AddRange(value.Where(item => item != null)); } }
    public StateMachineBehaviour[] behaviours { get => _behaviours.ToArray(); set { _behaviours.Clear(); if (value != null) _behaviours.AddRange(value.Where(item => item != null)); } }
    public AnimatorState? defaultState { get; set; }
    public Vector3 entryPosition { get; set; } = new(20, 100, 0);
    public AnimatorTransition[] entryTransitions { get => _entryTransitions.ToArray(); set { _entryTransitions.Clear(); if (value != null) _entryTransitions.AddRange(value.Where(item => item != null)); } }
    public Vector3 exitPosition { get; set; } = new(600, 100, 0);
    public Vector3 parentStateMachinePosition { get; set; }
    public ChildAnimatorStateMachine[] stateMachines { get => _stateMachines.ToArray(); set { _stateMachines.Clear(); if (value != null) _stateMachines.AddRange(value); } }
    public ChildAnimatorState[] states { get => _states.ToArray(); set { _states.Clear(); if (value != null) _states.AddRange(value); EnsureDefaultState(); } }

    public AnimatorState AddState(string name) => AddState(name, Vector3.zero);
    public AnimatorState AddState(string name, Vector3 position) { var state = new AnimatorState { name = MakeUniqueStateName(name) }; AddState(state, position); return state; }
    public void AddState(AnimatorState state, Vector3 position) { if (state == null) return; _states.Add(new ChildAnimatorState { state = state, position = position }); if (defaultState == null) defaultState = state; }
    public void RemoveState(AnimatorState state) { _states.RemoveAll(item => ReferenceEquals(item.state, state)); if (ReferenceEquals(defaultState, state)) defaultState = _states.FirstOrDefault().state; }
    public AnimatorStateMachine AddStateMachine(string name) => AddStateMachine(name, Vector3.zero);
    public AnimatorStateMachine AddStateMachine(string name, Vector3 position) { var stateMachine = new AnimatorStateMachine { name = MakeUniqueStateMachineName(name) }; AddStateMachine(stateMachine, position); return stateMachine; }
    public void AddStateMachine(AnimatorStateMachine stateMachine, Vector3 position) { if (stateMachine != null) _stateMachines.Add(new ChildAnimatorStateMachine { stateMachine = stateMachine, position = position }); }
    public void RemoveStateMachine(AnimatorStateMachine stateMachine) { _stateMachines.RemoveAll(item => ReferenceEquals(item.stateMachine, stateMachine)); _stateMachineTransitions.Remove(stateMachine); }
    public AnimatorStateTransition AddAnyStateTransition(AnimatorState destinationState) => AddAnyStateTransitionInternal(destinationState, null);
    public AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine destinationStateMachine) => AddAnyStateTransitionInternal(null, destinationStateMachine);
    public bool RemoveAnyStateTransition(AnimatorStateTransition transition) => transition != null && _anyStateTransitions.Remove(transition);
    public AnimatorTransition AddEntryTransition(AnimatorState destinationState) => AddEntryTransitionInternal(destinationState, null);
    public AnimatorTransition AddEntryTransition(AnimatorStateMachine destinationStateMachine) => AddEntryTransitionInternal(null, destinationStateMachine);
    public bool RemoveEntryTransition(AnimatorTransition transition) => transition != null && _entryTransitions.Remove(transition);
    public AnimatorTransition AddStateMachineTransition(AnimatorStateMachine sourceStateMachine) => AddStateMachineTransition(sourceStateMachine, (AnimatorStateMachine?)null);
    public AnimatorTransition AddStateMachineTransition(AnimatorStateMachine sourceStateMachine, AnimatorState destinationState) => AddStateMachineTransitionInternal(sourceStateMachine, destinationState, null);
    public AnimatorTransition AddStateMachineTransition(AnimatorStateMachine sourceStateMachine, AnimatorStateMachine destinationStateMachine) => AddStateMachineTransitionInternal(sourceStateMachine, null, destinationStateMachine);
    public AnimatorTransition AddStateMachineExitTransition(AnimatorStateMachine sourceStateMachine) { var transition = AddStateMachineTransitionInternal(sourceStateMachine, null, null); transition.isExit = true; return transition; }
    public AnimatorTransition[] GetStateMachineTransitions(AnimatorStateMachine sourceStateMachine) => sourceStateMachine != null && _stateMachineTransitions.TryGetValue(sourceStateMachine, out var result) ? result.ToArray() : Array.Empty<AnimatorTransition>();
    public void SetStateMachineTransitions(AnimatorStateMachine sourceStateMachine, AnimatorTransition[] transitions) { if (sourceStateMachine == null) return; _stateMachineTransitions[sourceStateMachine] = transitions?.Where(item => item != null).ToList() ?? new(); }
    public bool RemoveStateMachineTransition(AnimatorStateMachine sourceStateMachine, AnimatorTransition transition) => sourceStateMachine != null && transition != null && _stateMachineTransitions.TryGetValue(sourceStateMachine, out var transitions) && transitions.Remove(transition);
    public string MakeUniqueStateName(string name) => MakeUniqueName(name, _states.Select(item => item.state?.name));
    public string MakeUniqueStateMachineName(string name) => MakeUniqueName(name, _stateMachines.Select(item => item.stateMachine?.name));
    [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
    public StateMachineBehaviour AddStateMachineBehaviour(Type stateMachineBehaviourType) { if (stateMachineBehaviourType == null || !typeof(StateMachineBehaviour).IsAssignableFrom(stateMachineBehaviourType)) throw new ArgumentException("Type must inherit StateMachineBehaviour.", nameof(stateMachineBehaviourType)); var result = (StateMachineBehaviour)Activator.CreateInstance(stateMachineBehaviourType)!; _behaviours.Add(result); return result; }
    public T AddStateMachineBehaviour<T>() where T : StateMachineBehaviour { var result = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!; _behaviours.Add(result); return result; }
    private AnimatorStateTransition AddAnyStateTransitionInternal(AnimatorState? state, AnimatorStateMachine? stateMachine) { var result = new AnimatorStateTransition { destinationState = state, destinationStateMachine = stateMachine }; _anyStateTransitions.Add(result); return result; }
    private AnimatorTransition AddEntryTransitionInternal(AnimatorState? state, AnimatorStateMachine? stateMachine) { var result = new AnimatorTransition { destinationState = state, destinationStateMachine = stateMachine }; _entryTransitions.Add(result); return result; }
    private AnimatorTransition AddStateMachineTransitionInternal(AnimatorStateMachine source, AnimatorState? state, AnimatorStateMachine? stateMachine) { if (source == null) throw new ArgumentNullException(nameof(source)); var result = new AnimatorTransition { destinationState = state, destinationStateMachine = stateMachine }; if (!_stateMachineTransitions.TryGetValue(source, out var transitions)) _stateMachineTransitions[source] = transitions = new(); transitions.Add(result); return result; }
    private void EnsureDefaultState() { if (defaultState != null && _states.Any(item => ReferenceEquals(item.state, defaultState))) return; defaultState = _states.FirstOrDefault().state; }
    private static string MakeUniqueName(string name, IEnumerable<string?> names) { var seed = string.IsNullOrWhiteSpace(name) ? "New State" : name; var used = new HashSet<string>(names.Where(item => !string.IsNullOrEmpty(item))!, StringComparer.Ordinal); if (!used.Contains(seed)) return seed; for (var index = 1; ; index++) { var candidate = $"{seed} {index}"; if (!used.Contains(candidate)) return candidate; } }
}

[Bindings.NativeAsStruct]
[Bindings.NativeHeader("Editor/Src/Animation/AnimatorControllerLayer.bindings.h")]
[Bindings.NativeHeader("Editor/Src/Animation/AnimatorControllerLayer.h")]
[Bindings.NativeType(1, "MonoAnimatorControllerLayer")]
public class AnimatorControllerLayer
{
    private readonly Dictionary<AnimatorState, Motion?> _overrideMotions = new();
    private readonly Dictionary<AnimatorState, StateMachineBehaviour[]> _overrideBehaviours = new();
    public string name { get; set; } = string.Empty;
    public AnimatorStateMachine stateMachine { get; set; } = new();
    public AnimatorLayerBlendingMode blendingMode { get; set; }
    public float defaultWeight { get; set; } = 1f;
    public AvatarMask? avatarMask { get; set; }
    public bool iKPass { get; set; }
    public int syncedLayerIndex { get; set; } = -1;
    public bool syncedLayerAffectsTiming { get; set; }
    public Motion? GetOverrideMotion(AnimatorState state) => state != null && _overrideMotions.TryGetValue(state, out var motion) ? motion : null;
    public void SetOverrideMotion(AnimatorState state, Motion motion) { if (state != null) _overrideMotions[state] = motion; }
    public StateMachineBehaviour[] GetOverrideBehaviours(AnimatorState state) => state != null && _overrideBehaviours.TryGetValue(state, out var behaviours) ? behaviours.ToArray() : Array.Empty<StateMachineBehaviour>();
    public void SetOverrideBehaviours(AnimatorState state, StateMachineBehaviour[] behaviours) { if (state != null) _overrideBehaviours[state] = behaviours?.Where(item => item != null).ToArray() ?? Array.Empty<StateMachineBehaviour>(); }
}

[Serializable]
[Bindings.NativeAsStruct]
[Bindings.NativeHeader("Editor/Src/Animation/StateMachineBehaviourContext.h")]
public class StateMachineBehaviourContext
{
    [Bindings.NativeName("m_AnimatorController")] public AnimatorController? animatorController;
    [Bindings.NativeName("m_AnimatorObject")] public Object? animatorObject;
    [Bindings.NativeName("m_LayerIndex")] public int layerIndex;
}

[Bindings.NativeHeader("Editor/Src/Animation/AnimatorController.bindings.h")]
[Bindings.NativeHeader("Editor/Src/Animation/StateMachineBehaviourScripting.h")]
[Bindings.NativeHeader("Modules/Animation/Animator.h")]
[Bindings.NativeHeader("Modules/Animation/AnimatorController.h")]
[UnityEngine.NativeClass(null)]
public sealed class AnimatorController : RuntimeAnimatorController
{
    private static readonly HashSet<AnimatorController> Controllers = new();
    private static readonly object ControllersLock = new();
    private readonly List<AnimatorControllerLayer> _layers = new();
    private readonly List<AnimatorControllerParameter> _parameters = new();
    private readonly Dictionary<(AnimatorState State, int Layer), Motion?> _effectiveMotions = new();
    private readonly Dictionary<(AnimatorState State, int Layer), StateMachineBehaviour[]> _effectiveBehaviours = new();
    public AnimatorControllerLayer[] layers { get => _layers.ToArray(); set { _layers.Clear(); if (value != null) _layers.AddRange(value.Where(layer => layer != null)); EnsureBaseLayer(); } }
    public AnimatorControllerParameter[] parameters { get => _parameters.ToArray(); set { _parameters.Clear(); if (value != null) _parameters.AddRange(value.Where(parameter => parameter != null)); } }
    public AnimatorController() { lock (ControllersLock) Controllers.Add(this); EnsureBaseLayer(); RuntimeAnimatorController.SetAnimationClipProvider(this, () => CollectClips().ToArray()); }
    public void AddLayer(string name) => AddLayer(new AnimatorControllerLayer { name = MakeUniqueLayerName(name), stateMachine = new AnimatorStateMachine { name = name } });
    public void AddLayer(AnimatorControllerLayer layer) { if (layer == null) return; layer.name = MakeUniqueLayerName(layer.name); layer.stateMachine ??= new AnimatorStateMachine { name = layer.name }; _layers.Add(layer); }
    public void RemoveLayer(int index) { if (index >= 0 && index < _layers.Count) _layers.RemoveAt(index); EnsureBaseLayer(); }
    public string MakeUniqueLayerName(string name) => MakeUniqueName(name, _layers.Select(layer => layer.name), "Base Layer");
    public void AddParameter(string name, AnimatorControllerParameterType type) => AddParameter(new AnimatorControllerParameter { name = MakeUniqueParameterName(name), type = type });
    public void AddParameter(AnimatorControllerParameter paramater) { if (paramater == null) return; paramater.name = MakeUniqueParameterName(paramater.name); _parameters.Add(paramater); }
    public void RemoveParameter(int index) { if (index >= 0 && index < _parameters.Count) _parameters.RemoveAt(index); }
    public void RemoveParameter(AnimatorControllerParameter parameter) { if (parameter != null) _parameters.Remove(parameter); }
    public string MakeUniqueParameterName(string name) => MakeUniqueName(name, _parameters.Select(parameter => parameter.name), "New Parameter");
    public AnimatorState AddMotion(Motion motion) => AddMotion(motion, 0);
    public AnimatorState AddMotion(Motion motion, int layerIndex) { var layer = GetLayer(layerIndex); if (layer == null) throw new ArgumentOutOfRangeException(nameof(layerIndex)); var state = layer.stateMachine.AddState(motion?.name ?? "State"); state.motion = motion; return state; }
    public static AnimationClip AllocateAnimatorClip(string name) { var clip = new AnimationClip { name = name ?? string.Empty }; return clip; }
    public static AnimatorController CreateAnimatorControllerAtPath(string path) { var controller = new AnimatorController { name = System.IO.Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(controller, path); return controller; }
    public static AnimatorController CreateAnimatorControllerAtPathWithClip(string path, AnimationClip clip) { var controller = CreateAnimatorControllerAtPath(path); if (clip != null) controller.AddMotion(clip); return controller; }
    public AnimatorState CreateBlendTreeInController(string name, out BlendTree tree) => CreateBlendTreeInController(name, out tree, 0);
    public AnimatorState CreateBlendTreeInController(string name, out BlendTree tree, int layerIndex) { tree = new BlendTree { name = name ?? string.Empty }; return AddMotion(tree, layerIndex); }
    public Motion? GetStateEffectiveMotion(AnimatorState state) => GetStateEffectiveMotion(state, 0);
    public Motion? GetStateEffectiveMotion(AnimatorState state, int layerIndex) => state != null && _effectiveMotions.TryGetValue((state, layerIndex), out var motion) ? motion : state?.motion;
    public void SetStateEffectiveMotion(AnimatorState state, Motion motion) => SetStateEffectiveMotion(state, motion, 0);
    public void SetStateEffectiveMotion(AnimatorState state, Motion motion, int layerIndex) { if (state != null) _effectiveMotions[(state, layerIndex)] = motion; }
    public StateMachineBehaviour[] GetStateEffectiveBehaviours(AnimatorState state, int layerIndex) => state != null && _effectiveBehaviours.TryGetValue((state, layerIndex), out var behaviours) ? behaviours.ToArray() : state?.behaviours ?? Array.Empty<StateMachineBehaviour>();
    public void SetStateEffectiveBehaviours(AnimatorState state, int layerIndex, StateMachineBehaviour[] behaviours) { if (state != null) _effectiveBehaviours[(state, layerIndex)] = behaviours?.Where(item => item != null).ToArray() ?? Array.Empty<StateMachineBehaviour>(); }
    [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeReferencedByFirstArgument)]
    public StateMachineBehaviour AddEffectiveStateMachineBehaviour(Type stateMachineBehaviourType, AnimatorState state, int layerIndex) { var behaviour = state.AddStateMachineBehaviour(stateMachineBehaviourType); SetStateEffectiveBehaviours(state, layerIndex, state.behaviours); return behaviour; }
    public T AddEffectiveStateMachineBehaviour<T>(AnimatorState state, int layerIndex) where T : StateMachineBehaviour { var behaviour = state.AddStateMachineBehaviour<T>(); SetStateEffectiveBehaviours(state, layerIndex, state.behaviours); return behaviour; }
    public T[] GetBehaviours<T>() where T : StateMachineBehaviour => _layers.SelectMany(layer => CollectStates(layer.stateMachine)).SelectMany(state => state.behaviours).OfType<T>().Distinct().ToArray();
    [Bindings.FreeFunction("AnimatorControllerBindings::Internal_CreateStateMachineBehaviour")]
    public static int CreateStateMachineBehaviour(MonoScript script) => script == null ? -1 : 0;
    public static StateMachineBehaviourContext[] FindStateMachineBehaviourContext(StateMachineBehaviour behaviour)
    {
        if (behaviour == null) return Array.Empty<StateMachineBehaviourContext>();
        AnimatorController[] controllers;
        lock (ControllersLock) controllers = Controllers.ToArray();
        return controllers.SelectMany(controller => controller._layers.SelectMany((layer, index) => CollectStates(layer.stateMachine).Where(state => state.behaviours.Contains(behaviour)).Select(_ => new StateMachineBehaviourContext { animatorController = controller, layerIndex = index }))).ToArray();
    }
    public static void SetAnimatorController(Animator animator, AnimatorController controller) { if (animator == null) throw new ArgumentNullException(nameof(animator)); animator.runtimeAnimatorController = controller; }
    private AnimatorControllerLayer? GetLayer(int index) => index >= 0 && index < _layers.Count ? _layers[index] : null;
    private void EnsureBaseLayer() { if (_layers.Count == 0) _layers.Add(new AnimatorControllerLayer { name = "Base Layer", stateMachine = new AnimatorStateMachine { name = "Base Layer" } }); }
    private IEnumerable<AnimationClip> CollectClips() => _layers.SelectMany(layer => CollectStates(layer.stateMachine)).SelectMany(state => CollectClips(state.motion)).Distinct();
    private static IEnumerable<AnimatorState> CollectStates(AnimatorStateMachine stateMachine) => stateMachine.states.Where(child => child.state != null).Select(child => child.state!).Concat(stateMachine.stateMachines.Where(child => child.stateMachine != null).SelectMany(child => CollectStates(child.stateMachine!)));
    private static IEnumerable<AnimationClip> CollectClips(Motion? motion) => motion switch { AnimationClip clip => new[] { clip }, BlendTree tree => tree.children.SelectMany(child => CollectClips(child.motion)), _ => Array.Empty<AnimationClip>() };
    private static string MakeUniqueName(string name, IEnumerable<string> names, string fallback) { var seed = string.IsNullOrWhiteSpace(name) ? fallback : name; var used = new HashSet<string>(names, StringComparer.Ordinal); if (!used.Contains(seed)) return seed; for (var index = 1; ; index++) { var candidate = $"{seed} {index}"; if (!used.Contains(candidate)) return candidate; } }
}
