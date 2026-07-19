using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Xunit;
using EditorAssetImporter = UnityEditor.AssetImporter;

namespace Anity.Core.Tests;

/// <summary>Unity 2022 AssetPostprocessor public surface and name-dispatched import messages.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetPostprocessorParityTests : IDisposable
{
    private readonly string _project = Path.Combine(Path.GetTempPath(), "anity-asset-postprocessor-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetPostprocessorParityTests()
    {
        Directory.CreateDirectory(Path.Combine(_project, "Assets"));
        EditorApplication.OpenProject(_project);
        AssetPostprocessorProbeState.Reset();
    }

    public void Dispose()
    {
        AssetPostprocessorProbeState.Reset();
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_project, true); } catch { }
    }

    [Fact]
    public void Type_IsConcreteAndHasPublicParameterlessConstructor()
    {
        var type = typeof(AssetPostprocessor);
        Assert.False(type.IsAbstract);
        Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
    }

    [Fact]
    public void PublicProperties_MatchUnity2022Surface()
    {
        var properties = typeof(AssetPostprocessor).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToDictionary(property => property.Name);
        Assert.Equal(new[] { "assetImporter", "assetPath", "context", "preview" }, properties.Keys.OrderBy(name => name));
        Assert.Equal(typeof(string), properties["assetPath"].PropertyType);
        Assert.True(properties["assetPath"].CanRead);
        Assert.True(properties["assetPath"].CanWrite);
        Assert.Equal(typeof(EditorAssetImporter), properties["assetImporter"].PropertyType);
        Assert.False(properties["assetImporter"].CanWrite);
        Assert.Equal(typeof(AssetImportContext), properties["context"].PropertyType);
        Assert.True(properties["context"].CanWrite);
        Assert.True(properties["context"].GetSetMethod(true)!.IsAssembly);
        Assert.Equal(typeof(Texture2D), properties["preview"].PropertyType);
    }

    [Fact]
    public void PublicMethods_MatchUnity2022Surface()
    {
        var methods = typeof(AssetPostprocessor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name + "(" + string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.Name)) + ")")
            .OrderBy(signature => signature)
            .ToArray();
        Assert.Equal(new[]
        {
            "GetPostprocessOrder()", "GetVersion()", "LogError(String)", "LogError(String,Object)",
            "LogWarning(String)", "LogWarning(String,Object)"
        }.OrderBy(signature => signature), methods);
    }

    [Fact]
    public void ImportCallbacks_AreNotDeclaredByBaseType()
    {
        var names = typeof(AssetPostprocessor).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(method => method.Name).ToArray();
        Assert.DoesNotContain("OnPreprocessAsset", names);
        Assert.DoesNotContain("OnPostprocessAllAssets", names);
        Assert.DoesNotContain("OnPostprocessAvatar", names);
    }

    [Fact]
    public void Defaults_MatchUnity2022()
    {
        var processor = new AssetPostprocessor();
        Assert.Null(processor.assetPath);
        Assert.Null(processor.assetImporter);
        Assert.Null(processor.context);
        Assert.Equal(0, processor.GetPostprocessOrder());
        Assert.Equal(0u, processor.GetVersion());
    }

    [Fact]
    public void MissingAssetPath_HasNoImporter()
    {
        var processor = new AssetPostprocessor { assetPath = "Assets/Missing.txt" };
        Assert.Equal("Assets/Missing.txt", processor.assetPath);
        Assert.Null(processor.assetImporter);
    }

    [Fact]
    public void Preview_IsCompileTimeObsoleteLikeUnity2022()
    {
        var property = typeof(AssetPostprocessor).GetProperty("preview")!;
        var attribute = property.GetCustomAttribute<ObsoleteAttribute>();
        Assert.NotNull(attribute);
        Assert.True(attribute!.IsError);
        Assert.Equal("To set or get the preview, call EditorUtility.SetAssetPreview or AssetPreview.GetAssetPreview instead", attribute.Message);
        var processor = new AssetPostprocessor();
        var preview = new Texture2D(1, 1);
        property.SetValue(processor, preview);
        Assert.Same(preview, property.GetValue(processor));
    }

    [Fact]
    public void Context_UsesOfficialAssetImportersNamespaceAndPrivateConstructor()
    {
        Assert.Equal("UnityEditor.AssetImporters.AssetImportContext", typeof(AssetImportContext).FullName);
        Assert.Empty(typeof(AssetImportContext).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void PrivateNamedPreprocessCallbacks_AreInvokedInAscendingOrder()
    {
        ImportProbe();
        Assert.Equal(new[] { "late-pre", "early-pre" }, AssetPostprocessorProbeState.Calls.Where(call => call.EndsWith("-pre", StringComparison.Ordinal)));
    }

    [Fact]
    public void PreprocessCallback_ReceivesPathImporterAndContext()
    {
        ImportProbe();
        Assert.Equal(AssetPostprocessorProbeState.TargetPath, AssetPostprocessorProbeState.ObservedPath);
        Assert.NotNull(AssetPostprocessorProbeState.ObservedImporter);
        Assert.Equal(AssetPostprocessorProbeState.TargetPath, AssetPostprocessorProbeState.ObservedImporter!.assetPath);
        Assert.NotNull(AssetPostprocessorProbeState.ObservedContext);
        Assert.Equal(AssetPostprocessorProbeState.TargetPath, AssetPostprocessorProbeState.ObservedContext!.assetPath);
        Assert.Null(AssetPostprocessorProbeState.ObservedContext.mainObject);
    }

    [Fact]
    public void StaticFourArgumentPostprocessAllAssets_IsInvoked()
    {
        ImportProbe();
        Assert.Contains("late-post4", AssetPostprocessorProbeState.Calls);
    }

    [Fact]
    public void StaticFiveArgumentPostprocessAllAssets_IsInvokedWithFalseDomainReload()
    {
        ImportProbe();
        Assert.Contains("early-post5:false", AssetPostprocessorProbeState.Calls);
    }

    [Fact]
    public void PostprocessAllAssets_DoesNotUseGetPostprocessOrder()
    {
        ImportProbe();
        var calls = AssetPostprocessorProbeState.Calls.Where(call => call.Contains("-post", StringComparison.Ordinal)).ToArray();
        Assert.Equal(new[] { "early-post5:false", "late-post4" }, calls);
    }

    [Fact]
    public void WrongSignatureAndInstanceBatchCallbacks_AreIgnored()
    {
        ImportProbe();
        Assert.Equal(0, AssetPostprocessorProbeState.WrongSignatureCalls);
        Assert.Equal(0, AssetPostprocessorProbeState.InstanceBatchCalls);
    }

    [Fact]
    public void InternalPostprocessorType_WithPublicDefaultConstructor_IsDiscovered()
    {
        ImportProbe();
        Assert.Equal(1, AssetPostprocessorProbeState.InternalTypeCalls);
    }

    [Fact]
    public void PreprocessException_StopsAssetRegistration()
    {
        AssetPostprocessorProbeState.ThrowOnPreprocess = true;
        ImportProbe();
        Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPostprocessorProbeState.TargetPath));
        Assert.DoesNotContain("late-post4", AssetPostprocessorProbeState.Calls);
    }

    [Fact]
    public void RawAvatar_IsNotExposed()
    {
        Assert.Null(typeof(Avatar).Assembly.GetType("UnityEngine.RawAvatar"));
    }

    private void ImportProbe()
    {
        AssetPostprocessorProbeState.Enabled = true;
        File.WriteAllText(Path.Combine(_project, AssetPostprocessorProbeState.TargetPath), "probe");
        AssetDatabase.ImportAsset(AssetPostprocessorProbeState.TargetPath, ImportAssetOptions.ForceSynchronousImport);
    }
}

public static class AssetPostprocessorProbeState
{
    public const string TargetPath = "Assets/AssetPostprocessorProbe.txt";
    public static bool Enabled { get; set; }
    public static bool ThrowOnPreprocess { get; set; }
    public static List<string> Calls { get; } = new();
    public static string? ObservedPath { get; set; }
    public static EditorAssetImporter? ObservedImporter { get; set; }
    public static AssetImportContext? ObservedContext { get; set; }
    public static int WrongSignatureCalls { get; set; }
    public static int InstanceBatchCalls { get; set; }
    public static int InternalTypeCalls { get; set; }

    public static void Reset()
    {
        Enabled = false;
        ThrowOnPreprocess = false;
        Calls.Clear();
        ObservedPath = null;
        ObservedImporter = null;
        ObservedContext = null;
        WrongSignatureCalls = 0;
        InstanceBatchCalls = 0;
        InternalTypeCalls = 0;
    }
}

public sealed class EarlyAssetPostprocessorProbe : AssetPostprocessor
{
    public override int GetPostprocessOrder() => 20;

    private void OnPreprocessAsset()
    {
        if (AssetPostprocessorProbeState.Enabled && assetPath == AssetPostprocessorProbeState.TargetPath)
            AssetPostprocessorProbeState.Calls.Add("early-pre");
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom, bool didDomainReload)
    {
        if (AssetPostprocessorProbeState.Enabled && imported.Contains(AssetPostprocessorProbeState.TargetPath))
            AssetPostprocessorProbeState.Calls.Add("early-post5:" + didDomainReload.ToString().ToLowerInvariant());
    }
}

public sealed class LateAssetPostprocessorProbe : AssetPostprocessor
{
    public override int GetPostprocessOrder() => -20;

    private void OnPreprocessAsset()
    {
        if (!AssetPostprocessorProbeState.Enabled || assetPath != AssetPostprocessorProbeState.TargetPath) return;
        AssetPostprocessorProbeState.Calls.Add("late-pre");
        AssetPostprocessorProbeState.ObservedPath = assetPath;
        AssetPostprocessorProbeState.ObservedImporter = assetImporter;
        AssetPostprocessorProbeState.ObservedContext = context;
        if (AssetPostprocessorProbeState.ThrowOnPreprocess) throw new InvalidOperationException("intentional preprocess failure");
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (AssetPostprocessorProbeState.Enabled && imported.Contains(AssetPostprocessorProbeState.TargetPath))
            AssetPostprocessorProbeState.Calls.Add("late-post4");
    }
}

public sealed class InvalidAssetPostprocessorProbe : AssetPostprocessor
{
    private int OnPreprocessAsset()
    {
        AssetPostprocessorProbeState.WrongSignatureCalls++;
        return 0;
    }

    private void OnPreprocessAsset(string path)
    {
        AssetPostprocessorProbeState.WrongSignatureCalls++;
    }

    private void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        AssetPostprocessorProbeState.InstanceBatchCalls++;
    }
}

internal sealed class InternalAssetPostprocessorParityProbe : AssetPostprocessor
{
    public InternalAssetPostprocessorParityProbe()
    {
    }

    private void OnPreprocessAsset()
    {
        if (AssetPostprocessorProbeState.Enabled && assetPath == AssetPostprocessorProbeState.TargetPath)
            AssetPostprocessorProbeState.InternalTypeCalls++;
    }
}
