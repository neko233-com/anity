using UnityEditor;
using UnityEngine;
using Xunit;
using EditorAnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorConditionMode = UnityEditor.Animations.AnimatorConditionMode;

namespace Anity.Editor.Host.Tests;

public sealed class AnimatorWindowTransitionTests
{
    [Fact]
    public void CreateTransition_BuildsGraphEdgeAndSelectsIt()
    {
        var window = WindowWithStates(out var idle, out var run);
        var transition = window.CreateTransition(idle, run);
        Assert.NotNull(transition);
        Assert.Same(transition, window.selectedTransition);
        Assert.Contains(window.transitionEdges, edge => edge.transition == transition && edge.source == idle && edge.destination == run);
    }

    [Fact]
    public void CreateTransition_RejectsStateOutsideActiveLayer()
    {
        var window = WindowWithStates(out var idle, out _);
        var foreign = new EditorAnimatorController().AddMotion(new AnimationClip { name = "Foreign" });
        Assert.Null(window.CreateTransition(idle, foreign));
    }

    [Fact]
    public void AnyStateEntryAndExitTransitions_AppearWithKinds()
    {
        var window = WindowWithStates(out var idle, out var run);
        var any = window.CreateAnyStateTransition(run);
        var entry = window.CreateEntryTransition(idle);
        var exit = window.CreateExitTransition(run, defaultExitTime: true);
        Assert.Contains(window.transitionEdges, edge => edge.transition == any && edge.isAnyState);
        Assert.Contains(window.transitionEdges, edge => edge.transition == entry && edge.isEntry);
        Assert.Contains(window.transitionEdges, edge => edge.transition == exit && edge.isExit);
        Assert.True(exit!.hasExitTime);
    }

    [Fact]
    public void RemoveTransition_RemovesSourceTransitionAndSelection()
    {
        var window = WindowWithStates(out var idle, out var run);
        var transition = window.CreateTransition(idle, run)!;
        Assert.True(window.RemoveTransition(transition));
        Assert.Empty(idle.transitions);
        Assert.DoesNotContain(window.transitionEdges, edge => edge.transition == transition);
        Assert.Null(window.selectedTransition);
    }

    [Fact]
    public void RemoveTransition_RemovesAnyAndEntryTransitions()
    {
        var window = WindowWithStates(out var idle, out var run);
        var any = window.CreateAnyStateTransition(run)!;
        var entry = window.CreateEntryTransition(idle)!;
        Assert.True(window.RemoveTransition(any));
        Assert.True(window.RemoveTransition(entry));
        Assert.DoesNotContain(window.transitionEdges, edge => edge.transition == any || edge.transition == entry);
    }

    [Fact]
    public void AddCondition_RequiresExistingControllerParameter()
    {
        var window = WindowWithStates(out var idle, out var run);
        var transition = window.CreateTransition(idle, run)!;
        Assert.False(window.AddTransitionCondition(transition, AnimatorConditionMode.Greater, .2f, "Speed"));
        window.AddParameter("Speed", AnimatorControllerParameterType.Float);
        Assert.True(window.AddTransitionCondition(transition, AnimatorConditionMode.Greater, .2f, "Speed"));
        Assert.Equal("Speed", transition.conditions[0].parameter);
    }

    [Fact]
    public void RemoveCondition_UpdatesTransitionModel()
    {
        var window = WindowWithStates(out var idle, out var run);
        window.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        var transition = window.CreateTransition(idle, run)!;
        window.AddTransitionCondition(transition, AnimatorConditionMode.If, 0f, "Grounded");
        Assert.True(window.RemoveTransitionCondition(transition, transition.conditions[0]));
        Assert.Empty(transition.conditions);
    }

    [Fact]
    public void SetDefaultState_ChangesNodeDefaultMarker()
    {
        var window = WindowWithStates(out var idle, out var run);
        window.SetDefaultState(run);
        Assert.False(window.stateNodes.Single(node => node.state == idle).isDefault);
        Assert.True(window.stateNodes.Single(node => node.state == run).isDefault);
    }

    [Fact]
    public void SetStatePosition_UpdatesGraphAndNode()
    {
        var window = WindowWithStates(out var idle, out _);
        Assert.True(window.SetStatePosition(idle, new Vector3(321, 123, 0)));
        var node = window.stateNodes.Single(item => item.state == idle);
        Assert.Equal(321f, node.position.x);
        Assert.Equal(123f, node.position.y);
    }

    [Fact]
    public void SetStatePosition_RejectsForeignState()
    {
        var window = WindowWithStates(out _, out _);
        var foreign = new EditorAnimatorController().AddMotion(new AnimationClip());
        Assert.False(window.SetStatePosition(foreign, Vector3.zero));
    }

    [Fact]
    public void SelectStateAndTransition_AreMutuallyExclusive()
    {
        var window = WindowWithStates(out var idle, out var run);
        var transition = window.CreateTransition(idle, run)!;
        window.SelectState(idle);
        Assert.Same(idle, window.selectedState);
        Assert.Null(window.selectedTransition);
        window.SelectTransition(transition);
        Assert.Null(window.selectedState);
        Assert.Same(transition, window.selectedTransition);
    }

    [Fact]
    public void RefreshingNodePosition_PreservesSelectedTransition()
    {
        var window = WindowWithStates(out var idle, out var run);
        var transition = window.CreateTransition(idle, run)!;
        window.SetStatePosition(idle, new Vector3(50, 50, 0));
        Assert.Same(transition, window.selectedTransition);
    }

    [Fact]
    public void SwitchingLayer_RebuildsEdgesForThatLayerOnly()
    {
        var window = WindowWithStates(out var idle, out var run);
        var baseTransition = window.CreateTransition(idle, run)!;
        window.AddLayer("Combat");
        var attack = window.AddState("Attack", Vector3.zero)!;
        var recover = window.AddState("Recover", new Vector3(100, 0, 0))!;
        var combatTransition = window.CreateTransition(attack, recover)!;
        Assert.DoesNotContain(window.transitionEdges, edge => edge.transition == baseTransition);
        Assert.Contains(window.transitionEdges, edge => edge.transition == combatTransition);
    }

    [Fact]
    public void AddStateMachine_AddsNavigableGraphNode()
    {
        var window = WindowWithStates(out _, out _);
        var locomotion = window.AddStateMachine("Locomotion", new Vector3(260, 40, 0));
        Assert.NotNull(locomotion);
        Assert.Contains(window.stateNodes, node => node.stateMachine == locomotion && node.position.x == 260f);
    }

    [Fact]
    public void NavigateToStateMachine_UsesNestedMachineAsGraphRoot()
    {
        var window = WindowWithStates(out _, out _);
        var locomotion = window.AddStateMachine("Locomotion", new Vector3(260, 40, 0))!;
        var walk = locomotion.AddState("Walk", new Vector3(10, 20, 0));
        Assert.True(window.NavigateToStateMachine(locomotion));
        Assert.Equal(2, window.stateMachinePath.Count);
        Assert.Contains(window.stateNodes, node => node.state == walk);
        Assert.DoesNotContain(window.stateNodes, node => node.name == "Idle");
    }

    [Fact]
    public void NavigateToParentStateMachine_ReturnsToContainingGraph()
    {
        var window = WindowWithStates(out _, out _);
        var locomotion = window.AddStateMachine("Locomotion", Vector3.zero)!;
        window.NavigateToStateMachine(locomotion);
        Assert.True(window.NavigateToParentStateMachine());
        Assert.Single(window.stateMachinePath);
        Assert.Contains(window.stateNodes, node => node.stateMachine == locomotion);
        Assert.False(window.NavigateToParentStateMachine());
    }

    [Fact]
    public void NavigateToRootStateMachine_ResetsDeepPath()
    {
        var window = WindowWithStates(out _, out _);
        var locomotion = window.AddStateMachine("Locomotion", Vector3.zero)!;
        var gait = locomotion.AddStateMachine("Gait", Vector3.zero);
        window.NavigateToStateMachine(gait);
        window.NavigateToRootStateMachine();
        Assert.Single(window.stateMachinePath);
        Assert.Contains(window.stateNodes, node => node.stateMachine == locomotion);
    }

    [Fact]
    public void AddState_UsesCurrentNestedStateMachine()
    {
        var window = WindowWithStates(out _, out _);
        var locomotion = window.AddStateMachine("Locomotion", Vector3.zero)!;
        window.NavigateToStateMachine(locomotion);
        var walk = window.AddState("Walk", new Vector3(40, 50, 0));
        Assert.NotNull(walk);
        Assert.Contains(locomotion.states, child => child.state == walk);
        Assert.Contains(window.stateNodes, node => node.state == walk);
    }

    [Fact]
    public void PointerDoubleClick_StateMachineNodeNavigatesIntoIt()
    {
        var window = WindowWithStates(out _, out _);
        var locomotion = window.AddStateMachine("Locomotion", new Vector3(260, 40, 0))!;
        var canvas = new Rect(100, 50, 650, 350);
        var handled = window.ProcessStateMachineEvent(Pointer(EventType.MouseDown, 370, 100, clickCount: 2), canvas);
        Assert.True(handled);
        Assert.Same(locomotion, window.stateMachinePath[^1]);
    }

    [Fact]
    public void PointerClick_StateNodeSelectsIt()
    {
        var window = WindowWithStates(out var idle, out _);
        var handled = window.ProcessStateMachineEvent(Pointer(EventType.MouseDown, 110, 60), new Rect(100, 50, 650, 350));
        Assert.True(handled);
        Assert.Same(idle, window.selectedState);
    }

    [Fact]
    public void PointerDrag_SelectedStateUpdatesPersistentPosition()
    {
        var window = WindowWithStates(out var idle, out _);
        var canvas = new Rect(100, 50, 650, 350);
        window.ProcessStateMachineEvent(Pointer(EventType.MouseDown, 110, 60), canvas);
        Assert.True(window.ProcessStateMachineEvent(Pointer(EventType.MouseDrag, 310, 180), canvas));
        Assert.True(window.ProcessStateMachineEvent(Pointer(EventType.MouseUp, 310, 180), canvas));
        var node = window.stateNodes.Single(node => node.state == idle);
        Assert.Equal(200f, node.position.x);
        Assert.Equal(120f, node.position.y);
    }

    [Fact]
    public void PointerClick_TransitionLineSelectsTransition()
    {
        var window = WindowWithStates(out var idle, out var run);
        var transition = window.CreateTransition(idle, run)!;
        var handled = window.ProcessStateMachineEvent(Pointer(EventType.MouseDown, 210, 65), new Rect(100, 50, 650, 350));
        Assert.True(handled);
        Assert.Same(transition, window.selectedTransition);
    }

    [Fact]
    public void PointerClick_BlankCanvasClearsSelection()
    {
        var window = WindowWithStates(out var idle, out _);
        window.SelectState(idle);
        Assert.True(window.ProcessStateMachineEvent(Pointer(EventType.MouseDown, 700, 300), new Rect(100, 50, 650, 350)));
        Assert.Null(window.selectedState);
        Assert.Null(window.selectedTransition);
    }

    [Fact]
    public void DeleteKey_RemovesSelectedTransition()
    {
        var window = WindowWithStates(out var idle, out var run);
        var transition = window.CreateTransition(idle, run)!;
        Assert.True(window.ProcessStateMachineEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Delete }, new Rect(100, 50, 650, 350)));
        Assert.Empty(idle.transitions);
        Assert.Null(window.selectedTransition);
        Assert.DoesNotContain(window.transitionEdges, edge => edge.transition == transition);
    }

    [Fact]
    public void StateMachineTransition_CreatesAndRemovesGraphEdge()
    {
        var window = WindowWithStates(out _, out _);
        var locomotion = window.AddStateMachine("Locomotion", new Vector3(120, 0, 0))!;
        var combat = window.AddStateMachine("Combat", new Vector3(300, 0, 0))!;
        var transition = window.CreateStateMachineTransition(locomotion, combat)!;
        Assert.Contains(window.transitionEdges, edge => edge.transition == transition && edge.sourceStateMachine == locomotion && edge.destinationStateMachine == combat);
        Assert.True(window.RemoveTransition(transition));
        Assert.DoesNotContain(window.transitionEdges, edge => edge.transition == transition);
    }

    private static AnimatorWindow WindowWithStates(out UnityEditor.Animations.AnimatorState idle, out UnityEditor.Animations.AnimatorState run)
    {
        var window = new AnimatorWindow();
        window.SetAnimatorController(new EditorAnimatorController());
        idle = window.AddState("Idle", Vector3.zero)!;
        run = window.AddState("Run", new Vector3(120, 0, 0))!;
        return window;
    }

    private static Event Pointer(EventType type, float x, float y, int clickCount = 1) => new()
    {
        type = type,
        button = 0,
        clickCount = clickCount,
        mousePosition = new Vector2(x, y)
    };
}
