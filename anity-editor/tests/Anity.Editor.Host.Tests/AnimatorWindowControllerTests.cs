using UnityEditor;
using UnityEngine;
using Xunit;
using EditorAnimatorController = UnityEditor.Animations.AnimatorController;

namespace Anity.Editor.Host.Tests;

public sealed class AnimatorWindowControllerTests
{
    [Fact]
    public void SetAnimatorController_BindsBaseLayerGraph()
    {
        var window = new AnimatorWindow();
        var controller = new EditorAnimatorController();
        window.SetAnimatorController(controller);
        Assert.Same(controller, window.animatorController);
        Assert.Equal(3, window.stateNodes.Count);
    }

    [Fact]
    public void AddState_AddsStateAndGraphNode()
    {
        var window = BoundWindow(out var controller);
        var state = window.AddState("Idle", new Vector3(120, 80, 0));
        Assert.NotNull(state);
        Assert.Same(state, controller.layers[0].stateMachine.defaultState);
        Assert.Contains(window.stateNodes, node => node.state == state && node.position.x == 120 && node.position.y == 80);
    }

    [Fact]
    public void AddState_RejectsEmptyName()
    {
        var window = BoundWindow(out _);
        Assert.Null(window.AddState(" ", Vector3.zero));
    }

    [Fact]
    public void AddLayer_MakesNewLayerActive()
    {
        var window = BoundWindow(out var controller);
        var layer = window.AddLayer("Combat");
        Assert.NotNull(layer);
        Assert.Equal(1, window.activeLayerIndex);
        Assert.Equal("Combat", controller.layers[1].name);
    }

    [Fact]
    public void SetActiveLayer_SwitchesDisplayedStates()
    {
        var window = BoundWindow(out _);
        window.AddState("Idle", Vector3.zero);
        window.AddLayer("Combat");
        window.AddState("Attack", new Vector3(20, 30, 0));
        window.SetActiveLayer(0);
        Assert.Contains(window.stateNodes, node => node.name == "Idle");
        Assert.DoesNotContain(window.stateNodes, node => node.name == "Attack");
    }

    [Fact]
    public void SetActiveLayer_IgnoresInvalidIndex()
    {
        var window = BoundWindow(out _);
        window.SetActiveLayer(9);
        Assert.Equal(0, window.activeLayerIndex);
    }

    [Fact]
    public void AddParameter_AddsOnlyUniqueNames()
    {
        var window = BoundWindow(out var controller);
        window.AddParameter("Speed", AnimatorControllerParameterType.Float);
        window.AddParameter("Speed", AnimatorControllerParameterType.Int);
        Assert.Single(controller.parameters);
        Assert.Equal(AnimatorControllerParameterType.Float, controller.parameters[0].type);
    }

    [Fact]
    public void AddParameter_IgnoresBlankName()
    {
        var window = BoundWindow(out var controller);
        window.AddParameter("", AnimatorControllerParameterType.Bool);
        Assert.Empty(controller.parameters);
    }

    [Fact]
    public void GameObjectSelection_BindsAnimatorController()
    {
        var controller = new EditorAnimatorController();
        var gameObject = new GameObject("Actor");
        gameObject.AddComponent<Animator>().runtimeAnimatorController = controller;
        var window = new AnimatorWindow();
        window.SetSelectionGameObject(gameObject);
        Assert.Same(controller, window.animatorController);
    }

    [Fact]
    public void GameObjectWithoutAnimator_ClearsController()
    {
        var window = BoundWindow(out _);
        window.SetSelectionGameObject(new GameObject("Plain"));
        Assert.Null(window.animatorController);
        Assert.Empty(window.stateNodes);
    }

    private static AnimatorWindow BoundWindow(out EditorAnimatorController controller)
    {
        controller = new EditorAnimatorController();
        var window = new AnimatorWindow();
        window.SetAnimatorController(controller);
        return window;
    }
}
