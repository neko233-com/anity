using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using Xunit;

namespace Anity.Editor.Host.Tests;

public sealed class AssemblyReloadEventsTests
{
    [Fact]
    public void BeforeAssemblyReload_InvokesSubscriber()
    {
        var calls = 0;
        AssemblyReloadEvents.AssemblyReloadCallback callback = () => calls++;
        AssemblyReloadEvents.beforeAssemblyReload += callback;
        try { AssemblyReloadEvents.OnBeforeAssemblyReload(); }
        finally { AssemblyReloadEvents.beforeAssemblyReload -= callback; }
        Assert.Equal(1, calls);
    }

    [Fact]
    public void AfterAssemblyReload_InvokesSubscriber()
    {
        var calls = 0;
        AssemblyReloadEvents.AssemblyReloadCallback callback = () => calls++;
        AssemblyReloadEvents.afterAssemblyReload += callback;
        try { AssemblyReloadEvents.OnAfterAssemblyReload(); }
        finally { AssemblyReloadEvents.afterAssemblyReload -= callback; }
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ReloadAssemblies_OrdersBeforeScriptAndAfter()
    {
        var order = new List<string>();
        AssemblyReloadEvents.AssemblyReloadCallback before = () => order.Add("before");
        Action script = () => order.Add("script");
        AssemblyReloadEvents.AssemblyReloadCallback after = () => order.Add("after");
        AssemblyReloadEvents.beforeAssemblyReload += before;
        InternalEditorUtility.scriptReloaded += script;
        AssemblyReloadEvents.afterAssemblyReload += after;
        try { InternalEditorUtility.ReloadAssemblies(); }
        finally
        {
            AssemblyReloadEvents.beforeAssemblyReload -= before;
            InternalEditorUtility.scriptReloaded -= script;
            AssemblyReloadEvents.afterAssemblyReload -= after;
        }
        Assert.Equal(new[] { "before", "script", "after" }, order);
    }

    [Fact]
    public void ReloadAssemblies_ReportsRecompilingDuringScriptCallback()
    {
        var reloading = false;
        Action script = () => reloading = InternalEditorUtility.IsRecompiling();
        InternalEditorUtility.scriptReloaded += script;
        try { InternalEditorUtility.ReloadAssemblies(); }
        finally { InternalEditorUtility.scriptReloaded -= script; }
        Assert.True(reloading);
        Assert.False(InternalEditorUtility.IsRecompiling());
    }

    [Fact]
    public void ReloadAssemblies_FiresAfterWhenScriptCallbackThrows()
    {
        var afterCalls = 0;
        Action script = () => throw new InvalidOperationException("expected");
        AssemblyReloadEvents.AssemblyReloadCallback after = () => afterCalls++;
        InternalEditorUtility.scriptReloaded += script;
        AssemblyReloadEvents.afterAssemblyReload += after;
        try { Assert.Throws<InvalidOperationException>(() => InternalEditorUtility.ReloadAssemblies()); }
        finally
        {
            InternalEditorUtility.scriptReloaded -= script;
            AssemblyReloadEvents.afterAssemblyReload -= after;
        }
        Assert.Equal(1, afterCalls);
        Assert.False(InternalEditorUtility.IsRecompiling());
    }

    [Fact]
    public void RequestScriptReload_UsesSameLifecycle()
    {
        var calls = 0;
        AssemblyReloadEvents.AssemblyReloadCallback before = () => calls++;
        AssemblyReloadEvents.beforeAssemblyReload += before;
        try { InternalEditorUtility.RequestScriptReload(); }
        finally { AssemblyReloadEvents.beforeAssemblyReload -= before; }
        Assert.Equal(1, calls);
    }

    [Fact]
    public void EditorUtilityRequestScriptReload_UsesAssemblyReloadLifecycle()
    {
        var calls = 0;
        AssemblyReloadEvents.AssemblyReloadCallback after = () => calls++;
        AssemblyReloadEvents.afterAssemblyReload += after;
        try { EditorUtility.RequestScriptReload(); }
        finally { AssemblyReloadEvents.afterAssemblyReload -= after; }
        Assert.Equal(1, calls);
    }

    [Fact]
    public void UnsubscribedCallback_IsNotInvoked()
    {
        var calls = 0;
        AssemblyReloadEvents.AssemblyReloadCallback callback = () => calls++;
        AssemblyReloadEvents.beforeAssemblyReload += callback;
        AssemblyReloadEvents.beforeAssemblyReload -= callback;
        InternalEditorUtility.ReloadAssemblies();
        Assert.Equal(0, calls);
    }

    [Fact]
    public void MultipleSubscribers_RunInSubscriptionOrder()
    {
        var order = new List<int>();
        AssemblyReloadEvents.AssemblyReloadCallback first = () => order.Add(1);
        AssemblyReloadEvents.AssemblyReloadCallback second = () => order.Add(2);
        AssemblyReloadEvents.afterAssemblyReload += first;
        AssemblyReloadEvents.afterAssemblyReload += second;
        try { AssemblyReloadEvents.OnAfterAssemblyReload(); }
        finally
        {
            AssemblyReloadEvents.afterAssemblyReload -= first;
            AssemblyReloadEvents.afterAssemblyReload -= second;
        }
        Assert.Equal(new[] { 1, 2 }, order);
    }

    [Fact]
    public void DirectBeforeAndAfter_AreIndependent()
    {
        var beforeCalls = 0;
        var afterCalls = 0;
        AssemblyReloadEvents.AssemblyReloadCallback before = () => beforeCalls++;
        AssemblyReloadEvents.AssemblyReloadCallback after = () => afterCalls++;
        AssemblyReloadEvents.beforeAssemblyReload += before;
        AssemblyReloadEvents.afterAssemblyReload += after;
        try
        {
            AssemblyReloadEvents.OnBeforeAssemblyReload();
            Assert.Equal(1, beforeCalls);
            Assert.Equal(0, afterCalls);
        }
        finally
        {
            AssemblyReloadEvents.beforeAssemblyReload -= before;
            AssemblyReloadEvents.afterAssemblyReload -= after;
        }
    }
}
