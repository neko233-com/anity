using UnityEditor.Animations;
using UnityEngine;
using Xunit;
using EditorAnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorConditionMode = UnityEditor.Animations.AnimatorConditionMode;
using AnimatorControllerLayer = UnityEditor.Animations.AnimatorControllerLayer;
using AnimatorState = UnityEditor.Animations.AnimatorState;
using AnimatorStateMachine = UnityEditor.Animations.AnimatorStateMachine;
using AnimatorStateTransition = UnityEditor.Animations.AnimatorStateTransition;

namespace Anity.Editor.Host.Tests;

public sealed class AnimatorControllerGraphTests
{
    [Fact]
    public void NewController_HasEditableBaseLayer()
    {
        var controller = new EditorAnimatorController();
        Assert.Single(controller.layers);
        Assert.Equal("Base Layer", controller.layers[0].name);
        Assert.NotNull(controller.layers[0].stateMachine);
    }

    [Fact]
    public void LayersAndParameters_UseUniqueNamesAndCanBeRemoved()
    {
        var controller = new EditorAnimatorController();
        controller.AddLayer("Combat");
        controller.AddLayer("Combat");
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Int);
        Assert.Equal(new[] { "Base Layer", "Combat", "Combat 1" }, controller.layers.Select(layer => layer.name));
        Assert.Equal(new[] { "Speed", "Speed 1" }, controller.parameters.Select(parameter => parameter.name));
        controller.RemoveLayer(2);
        controller.RemoveParameter(0);
        Assert.Equal(2, controller.layers.Length);
        Assert.Single(controller.parameters);
    }

    [Fact]
    public void StateMachine_AddsUniqueStatesAndMaintainsDefault()
    {
        var machine = new AnimatorStateMachine();
        var idle = machine.AddState("Idle", new Vector3(10, 20, 0));
        var duplicate = machine.AddState("Idle");
        Assert.Same(idle, machine.defaultState);
        Assert.Equal("Idle 1", duplicate.name);
        Assert.Equal(10, machine.states[0].position.x);
        machine.RemoveState(idle);
        Assert.Same(duplicate, machine.defaultState);
    }

    [Fact]
    public void State_TransitionsAndConditions_AreEditable()
    {
        var machine = new AnimatorStateMachine();
        var idle = machine.AddState("Idle");
        var run = machine.AddState("Run");
        var transition = idle.AddTransition(run, true);
        transition.AddCondition(AnimatorConditionMode.Greater, .25f, "Speed");
        Assert.True(transition.hasExitTime);
        Assert.Same(run, transition.destinationState);
        Assert.Equal("Speed", transition.conditions[0].parameter);
        transition.RemoveCondition(transition.conditions[0]);
        Assert.Empty(transition.conditions);
        idle.RemoveTransition(transition);
        Assert.Empty(idle.transitions);
    }

    [Fact]
    public void StateMachine_ManagesAnyEntryAndNestedTransitions()
    {
        var root = new AnimatorStateMachine();
        var child = root.AddStateMachine("Child");
        var target = root.AddState("Target");
        var any = root.AddAnyStateTransition(target);
        var entry = root.AddEntryTransition(child);
        var nested = root.AddStateMachineTransition(child, target);
        Assert.Same(target, any.destinationState);
        Assert.Same(child, entry.destinationStateMachine);
        Assert.Single(root.GetStateMachineTransitions(child));
        Assert.True(root.RemoveStateMachineTransition(child, nested));
        Assert.True(root.RemoveAnyStateTransition(any));
        Assert.True(root.RemoveEntryTransition(entry));
    }

    [Fact]
    public void BlendTree_StoresChildrenAndControllerCollectsNestedClips()
    {
        var controller = new EditorAnimatorController();
        var walk = new AnimationClip { name = "Walk" };
        var run = new AnimationClip { name = "Run" };
        var state = controller.CreateBlendTreeInController("Locomotion", out var tree);
        tree.AddChild(walk, 0f);
        tree.AddChild(run, 1f);
        var nested = tree.CreateBlendTreeChild(2f);
        nested.AddChild(walk);
        Assert.Same(tree, state.motion);
        Assert.Equal(3, tree.children.Length);
        Assert.Equal(new[] { walk, run }, controller.animationClips);
    }

    [Fact]
    public void EffectiveMotionAndBehaviours_AreScopedPerLayer()
    {
        var controller = new EditorAnimatorController();
        var state = controller.AddMotion(new AnimationClip { name = "Idle" });
        var overrideClip = new AnimationClip { name = "Override" };
        controller.SetStateEffectiveMotion(state, overrideClip, 0);
        var behaviour = controller.AddEffectiveStateMachineBehaviour<TestBehaviour>(state, 0);
        Assert.Same(overrideClip, controller.GetStateEffectiveMotion(state, 0));
        Assert.Contains(behaviour, controller.GetStateEffectiveBehaviours(state, 0));
    }

    [Fact]
    public void Controller_BindsToAnimatorRuntimeController()
    {
        var controller = new EditorAnimatorController();
        var animator = new GameObject("Actor").AddComponent<Animator>();
        EditorAnimatorController.SetAnimatorController(animator, controller);
        Assert.Same(controller, animator.runtimeAnimatorController);
    }

    [Fact]
    public void Transition_DisplayNameUsesDestinationAndExit()
    {
        var state = new AnimatorState { name = "Jump" };
        var transition = new AnimatorStateTransition { destinationState = state };
        Assert.Equal("Jump", transition.GetDisplayName(new GameObject("Source")));
        transition.isExit = true;
        Assert.Equal("Exit", transition.GetDisplayName(new GameObject("Source")));
    }

    [Fact]
    public void Layer_StoresMotionAndBehaviourOverrides()
    {
        var layer = new AnimatorControllerLayer();
        var state = new AnimatorState();
        var clip = new AnimationClip();
        var behaviour = new TestBehaviour();
        layer.SetOverrideMotion(state, clip);
        layer.SetOverrideBehaviours(state, new StateMachineBehaviour[] { behaviour });
        Assert.Same(clip, layer.GetOverrideMotion(state));
        Assert.Contains(behaviour, layer.GetOverrideBehaviours(state));
    }

    [Fact]
    public void StateMachineBehaviourContexts_FindOwningLayer()
    {
        var controller = new EditorAnimatorController();
        var state = controller.AddMotion(new AnimationClip());
        var behaviour = controller.AddEffectiveStateMachineBehaviour<TestBehaviour>(state, 0);
        var context = Assert.Single(EditorAnimatorController.FindStateMachineBehaviourContext(behaviour));
        Assert.Same(controller, context.animatorController);
        Assert.Equal(0, context.layerIndex);
    }

    [Fact]
    public void ChildStateMachineRemoval_RemovesGraphNode()
    {
        var root = new AnimatorStateMachine();
        var child = root.AddStateMachine("Subgraph", new Vector3(3, 4, 0));
        root.RemoveStateMachine(child);
        Assert.Empty(root.stateMachines);
    }

    private sealed class TestBehaviour : StateMachineBehaviour { }
}
