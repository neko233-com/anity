using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Editor.Host.Tests;

public sealed class ActiveEditorTrackerTests
{
    [Fact]
    public void RebuildIfNecessary_CreatesEditorForSelection()
    {
        using var scope = new TrackerScope();
        Selection.SetSelection(new UnityEngine.Object[] { new GameObject("Selected") });
        scope.tracker.RebuildIfNecessary();
        Assert.Single(scope.tracker.activeEditors);
        Assert.False(scope.tracker.isDirty);
    }

    [Fact]
    public void SelectionChange_MarksTrackerDirty()
    {
        using var scope = new TrackerScope();
        scope.tracker.ForceRebuild();
        Selection.SetSelection(new UnityEngine.Object[] { new GameObject("Changed") });
        Assert.True(scope.tracker.isDirty);
    }

    [Fact]
    public void ClearDirty_ResetsDirtyFlag()
    {
        using var scope = new TrackerScope();
        Assert.True(scope.tracker.isDirty);
        scope.tracker.ClearDirty();
        Assert.False(scope.tracker.isDirty);
    }

    [Fact]
    public void LockingTracker_PreservesCurrentSelectionAcrossSelectionChanges()
    {
        using var scope = new TrackerScope();
        var first = new GameObject("First");
        var second = new GameObject("Second");
        Selection.SetSelection(new UnityEngine.Object[] { first });
        scope.tracker.isLocked = true;
        Selection.SetSelection(new UnityEngine.Object[] { second });
        scope.tracker.ForceRebuild();
        Assert.Same(first, scope.tracker.activeEditors[0].target);
    }

    [Fact]
    public void UnlockingTracker_UsesLiveSelection()
    {
        using var scope = new TrackerScope();
        var first = new GameObject("First");
        var second = new GameObject("Second");
        Selection.SetSelection(new UnityEngine.Object[] { first });
        scope.tracker.isLocked = true;
        Selection.SetSelection(new UnityEngine.Object[] { second });
        scope.tracker.isLocked = false;
        scope.tracker.ForceRebuild();
        Assert.Same(second, scope.tracker.activeEditors[0].target);
    }

    [Fact]
    public void SetObjectsLockedByThisTracker_ExportsConfiguredObjects()
    {
        using var scope = new TrackerScope();
        var first = new GameObject("First");
        var second = new GameObject("Second");
        scope.tracker.SetObjectsLockedByThisTracker(new List<UnityEngine.Object> { first, second, first });
        var exported = new List<UnityEngine.Object>();
        scope.tracker.GetObjectsLockedByThisTracker(exported);
        Assert.Equal(new UnityEngine.Object[] { first, second }, exported);
    }

    [Fact]
    public void Visibility_DefaultsToOneAndCanBeChanged()
    {
        using var scope = new TrackerScope();
        Selection.SetSelection(new UnityEngine.Object[] { new GameObject("Selected") });
        scope.tracker.ForceRebuild();
        Assert.Equal(1, scope.tracker.GetVisible(0));
        scope.tracker.SetVisible(0, 0);
        Assert.Equal(0, scope.tracker.GetVisible(0));
    }

    [Fact]
    public void Visibility_InvalidIndexIsSafe()
    {
        using var scope = new TrackerScope();
        scope.tracker.ForceRebuild();
        scope.tracker.SetVisible(-1, 3);
        scope.tracker.SetVisible(99, 3);
        Assert.Equal(0, scope.tracker.GetVisible(-1));
        Assert.Equal(0, scope.tracker.GetVisible(99));
    }

    [Fact]
    public void UnsavedChangesStateChanged_TracksActiveEditorState()
    {
        using var scope = new TrackerScope();
        Selection.SetSelection(new UnityEngine.Object[] { new GameObject("Selected") });
        scope.tracker.ForceRebuild();
        var editor = scope.tracker.activeEditors[0];
        scope.tracker.UnsavedChangesStateChanged(editor, true);
        Assert.True(scope.tracker.hasUnsavedChanges);
        scope.tracker.UnsavedChangesStateChanged(editor, false);
        Assert.False(scope.tracker.hasUnsavedChanges);
    }

    [Fact]
    public void DelayFlushDirtyRebuild_DefersSelectionRebuild()
    {
        using var scope = new TrackerScope();
        Selection.SetSelection(new UnityEngine.Object[] { new GameObject("Initial") });
        scope.tracker.ForceRebuild();
        ActiveEditorTracker.delayFlushDirtyRebuild = true;
        Selection.SetSelection(new UnityEngine.Object[] { new GameObject("Deferred") });
        scope.tracker.RebuildIfNecessary();
        Assert.True(scope.tracker.isDirty);
        ActiveEditorTracker.delayFlushDirtyRebuild = false;
        scope.tracker.RebuildIfNecessary();
        Assert.False(scope.tracker.isDirty);
    }

    [Fact]
    public void InspectorAndDataModes_MarkTrackerDirty()
    {
        using var scope = new TrackerScope();
        scope.tracker.ClearDirty();
        scope.tracker.inspectorMode = InspectorMode.Debug;
        Assert.True(scope.tracker.isDirty);
        scope.tracker.ClearDirty();
        scope.tracker.dataMode = DataMode.Runtime;
        Assert.True(scope.tracker.isDirty);
        Assert.Equal(DataMode.Runtime, scope.tracker.dataMode);
    }

    [Fact]
    public void TrackerRebuilt_EventFiresOnForceRebuild()
    {
        using var scope = new TrackerScope();
        var rebuilds = 0;
        Action callback = () => rebuilds++;
        ActiveEditorTracker.editorTrackerRebuilt += callback;
        try
        {
            scope.tracker.ForceRebuild();
        }
        finally
        {
            ActiveEditorTracker.editorTrackerRebuilt -= callback;
        }
        Assert.Equal(1, rebuilds);
    }

    [Fact]
    public void SharedTracker_ReturnsStableInstance()
    {
        Assert.Same(ActiveEditorTracker.sharedTracker, ActiveEditorTracker.sharedTracker);
    }

    private sealed class TrackerScope : IDisposable
    {
        public readonly ActiveEditorTracker tracker = new();

        public void Dispose()
        {
            ActiveEditorTracker.delayFlushDirtyRebuild = false;
            tracker.Destroy();
            Selection.Clear();
        }
    }
}
