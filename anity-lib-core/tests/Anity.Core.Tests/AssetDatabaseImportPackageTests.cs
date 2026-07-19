using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Package import lifecycle parity — success, failure, ordering and callback isolation.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetDatabaseImportPackageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "anity-package-" + Guid.NewGuid().ToString("N"));

    public AssetDatabaseImportPackageTests()
    {
        Directory.CreateDirectory(_directory);
        AssetDatabase.onImportPackageItemsCompleted = null;
    }

    public void Dispose()
    {
        AssetDatabase.onImportPackageItemsCompleted = null;
        try { Directory.Delete(_directory, recursive: true); } catch { }
    }

    [Fact]
    public void ExistingPackage_RaisesStartedThenCompleted()
    {
        var events = new List<string>();
        AssetDatabase.ImportPackageCallback started = package => events.Add("started:" + package);
        AssetDatabase.ImportPackageCallback completed = package => events.Add("completed:" + package);
        AssetDatabase.importPackageStarted += started;
        AssetDatabase.importPackageCompleted += completed;
        try
        {
            AssetDatabase.ImportPackage(CreatePackage("sample.unitypackage"), interactive: false);
            Assert.Equal(new[] { "started:sample", "completed:sample" }, events);
        }
        finally
        {
            AssetDatabase.importPackageStarted -= started;
            AssetDatabase.importPackageCompleted -= completed;
        }
    }

    [Fact]
    public void ExistingPackage_ReportsCanonicalCompletedItemPath()
    {
        string? completedItem = null;
        AssetDatabase.onImportPackageItemsCompleted = paths => completedItem = Assert.Single(paths);
        var package = CreatePackage("item.unitypackage");

        AssetDatabase.ImportPackage(package, interactive: true);

        Assert.Equal(package.Replace('\\', '/'), completedItem);
    }

    [Fact]
    public void ExistingPackage_DoesNotRaiseFailure()
    {
        var failures = 0;
        AssetDatabase.ImportPackageFailedCallback failed = (_, _) => failures++;
        AssetDatabase.importPackageFailed += failed;
        try
        {
            AssetDatabase.ImportPackage(CreatePackage("valid.unitypackage"), interactive: false);
            Assert.Equal(0, failures);
        }
        finally
        {
            AssetDatabase.importPackageFailed -= failed;
        }
    }

    [Fact]
    public void MissingPackage_RaisesStartedThenFailure()
    {
        var events = new List<string>();
        AssetDatabase.ImportPackageCallback started = package => events.Add("started:" + package);
        AssetDatabase.ImportPackageFailedCallback failed = (package, _) => events.Add("failed:" + package);
        AssetDatabase.importPackageStarted += started;
        AssetDatabase.importPackageFailed += failed;
        try
        {
            AssetDatabase.ImportPackage(Path.Combine(_directory, "missing.unitypackage"), interactive: false);
            Assert.Equal(new[] { "started:missing", "failed:missing" }, events);
        }
        finally
        {
            AssetDatabase.importPackageStarted -= started;
            AssetDatabase.importPackageFailed -= failed;
        }
    }

    [Fact]
    public void MissingPackage_FailureContainsResolvedPath()
    {
        string? error = null;
        AssetDatabase.ImportPackageFailedCallback failed = (_, message) => error = message;
        AssetDatabase.importPackageFailed += failed;
        var missing = Path.Combine(_directory, "not-there.unitypackage");
        try
        {
            AssetDatabase.ImportPackage(missing, interactive: false);
            Assert.Contains(Path.GetFullPath(missing), error);
        }
        finally
        {
            AssetDatabase.importPackageFailed -= failed;
        }
    }

    [Fact]
    public void MissingPackage_DoesNotRaiseCompleted()
    {
        var completed = 0;
        AssetDatabase.ImportPackageCallback callback = _ => completed++;
        AssetDatabase.importPackageCompleted += callback;
        try
        {
            AssetDatabase.ImportPackage(Path.Combine(_directory, "missing.unitypackage"), interactive: false);
            Assert.Equal(0, completed);
        }
        finally
        {
            AssetDatabase.importPackageCompleted -= callback;
        }
    }

    [Fact]
    public void MissingPackage_DoesNotReportImportedItems()
    {
        var calls = 0;
        AssetDatabase.onImportPackageItemsCompleted = _ => calls++;

        AssetDatabase.ImportPackage(Path.Combine(_directory, "missing.unitypackage"), interactive: false);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void EmptyPackagePath_RaisesFailureWithEmptyName()
    {
        string? name = null;
        string? error = null;
        AssetDatabase.ImportPackageFailedCallback failed = (package, message) => { name = package; error = message; };
        AssetDatabase.importPackageFailed += failed;
        try
        {
            AssetDatabase.ImportPackage(string.Empty, interactive: false);
            Assert.Equal(string.Empty, name);
            Assert.Contains("must not be empty", error);
        }
        finally
        {
            AssetDatabase.importPackageFailed -= failed;
        }
    }

    [Fact]
    public void RemovedCallback_IsNotCalled()
    {
        var calls = 0;
        AssetDatabase.ImportPackageCallback callback = _ => calls++;
        AssetDatabase.importPackageStarted += callback;
        AssetDatabase.importPackageStarted -= callback;

        AssetDatabase.ImportPackage(CreatePackage("unsubscribed.unitypackage"), interactive: false);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void MultipleStartedSubscribers_RunInSubscriptionOrder()
    {
        var calls = new List<int>();
        AssetDatabase.ImportPackageCallback first = _ => calls.Add(1);
        AssetDatabase.ImportPackageCallback second = _ => calls.Add(2);
        AssetDatabase.importPackageStarted += first;
        AssetDatabase.importPackageStarted += second;
        try
        {
            AssetDatabase.ImportPackage(CreatePackage("ordered.unitypackage"), interactive: false);
            Assert.Equal(new[] { 1, 2 }, calls);
        }
        finally
        {
            AssetDatabase.importPackageStarted -= first;
            AssetDatabase.importPackageStarted -= second;
        }
    }

    [Fact]
    public void PackageName_UsesFileNameWithoutExtension()
    {
        string? name = null;
        AssetDatabase.ImportPackageCallback started = package => name = package;
        AssetDatabase.importPackageStarted += started;
        try
        {
            AssetDatabase.ImportPackage(CreatePackage("my.package.unitypackage"), interactive: false);
            Assert.Equal("my.package", name);
        }
        finally
        {
            AssetDatabase.importPackageStarted -= started;
        }
    }

    private string CreatePackage(string fileName)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, "Anity test package");
        return path;
    }
}
