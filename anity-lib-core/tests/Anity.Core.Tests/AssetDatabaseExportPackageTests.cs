using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Unity gzip/tar package export and import round-trip coverage.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetDatabaseExportPackageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-export-package-" + Guid.NewGuid().ToString("N"));
    private readonly string _source;
    private readonly string _destination;
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetDatabaseExportPackageTests()
    {
        _source = Path.Combine(_dir, "source");
        _destination = Path.Combine(_dir, "destination");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_destination);
        EditorApplication.OpenProject(_source);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact] public void ExportPackage_WritesGzipArchive() { var asset = Add("One.txt", 1, "one"); var package = PackagePath("gzip"); AssetDatabase.ExportPackage(new[] { asset }, package, false); var bytes = File.ReadAllBytes(package); Assert.True(bytes.Length > 2); Assert.Equal((byte)0x1f, bytes[0]); Assert.Equal((byte)0x8b, bytes[1]); }
    [Fact] public void ExportPackage_RoundTripsTextAsset() { var asset = Add("RoundTrip.txt", 1, "payload"); var package = PackagePath("roundtrip"); AssetDatabase.ExportPackage(new[] { asset }, package, false); ImportIntoDestination(package); Assert.Equal("payload", AssetDatabase.LoadAssetAtPath<TextAsset>(asset)!.text); }
    [Fact] public void ExportPackage_PreservesMetaGuid() { var asset = Add("Guid.txt", 1, "payload"); var guid = AssetDatabase.AssetPathToGUID(asset); var package = PackagePath("guid"); AssetDatabase.ExportPackage(new[] { asset }, package, false); ImportIntoDestination(package); Assert.Equal(guid, AssetDatabase.AssetPathToGUID(asset)); }
    [Fact] public void ExportPackage_RoundTripsMultipleAssets() { var first = Add("First.txt", 1, "first"); var second = Add("Second.txt", 2, "second"); var package = PackagePath("multiple"); AssetDatabase.ExportPackage(new[] { first, second }, package, false); ImportIntoDestination(package); Assert.Equal("first", AssetDatabase.LoadAssetAtPath<TextAsset>(first)!.text); Assert.Equal("second", AssetDatabase.LoadAssetAtPath<TextAsset>(second)!.text); }
    [Fact] public void ExportPackage_DeduplicatesRepeatedPaths() { var asset = Add("Duplicate.txt", 1, "payload"); var package = PackagePath("dedupe"); AssetDatabase.ExportPackage(new[] { asset, asset }, package, false); ImportIntoDestination(package); Assert.Single(AssetDatabase.GetAllAssetPaths()); }
    [Fact] public void ExportPackage_CreatesOutputDirectory() { var asset = Add("Output.txt", 1, "payload"); var package = Path.Combine(_dir, "created", "nested", "export.unitypackage"); AssetDatabase.ExportPackage(new[] { asset }, package, false); Assert.True(File.Exists(package)); }
    [Fact] public void ExportPackage_ReplacesExistingOutput() { var asset = Add("Replace.txt", 1, "payload"); var package = PackagePath("replace"); File.WriteAllText(package, "old"); AssetDatabase.ExportPackage(new[] { asset }, package, false); Assert.Equal((byte)0x1f, File.ReadAllBytes(package)[0]); }
    [Fact] public void ExportPackage_RejectsMissingSourceWithoutOutput() { var package = PackagePath("missing"); Assert.Throws<FileNotFoundException>(() => AssetDatabase.ExportPackage(new[] { "Assets/None.txt" }, package, false)); Assert.False(File.Exists(package)); }
    [Fact] public void ExportPackage_RejectsMemoryOnlyAsset() { var path = "Assets/Virtual/" + Guid.NewGuid().ToString("N") + ".asset"; AssetDatabase.CreateAsset(new TextAsset("memory"), path); Assert.Throws<FileNotFoundException>(() => AssetDatabase.ExportPackage(new[] { path }, PackagePath("virtual"), false)); }
    [Fact] public void ExportPackage_GeneratesPackageMetaWhenSourceHasNone() { const string path = "Assets/Export/NoMeta.txt"; Directory.CreateDirectory(Path.GetDirectoryName(FullPath(path))!); File.WriteAllText(FullPath(path), "payload"); AssetDatabase.ImportAsset(path); var package = PackagePath("nometa"); AssetDatabase.ExportPackage(new[] { path }, package, false); ImportIntoDestination(package); Assert.Matches("^[0-9a-f]{32}$", AssetDatabase.AssetPathToGUID(path)); }
    [Fact] public void ExportPackage_InteractiveFlagUsesSameArchivePath() { var asset = Add("Interactive.txt", 1, "payload"); var package = PackagePath("interactive"); AssetDatabase.ExportPackage(new[] { asset }, package, true); using var stream = File.OpenRead(package); using var gzip = new GZipStream(stream, CompressionMode.Decompress); var firstTarBlock = new byte[512]; Assert.Equal(512, gzip.Read(firstTarBlock, 0, firstTarBlock.Length)); Assert.Contains("pathname", Encoding.UTF8.GetString(firstTarBlock)); }
    [Fact] public void ExportPackageOptions_HasUnity2022FlagValues() { Assert.Equal(0, (int)ExportPackageOptions.Default); Assert.Equal(1, (int)ExportPackageOptions.Interactive); Assert.Equal(2, (int)ExportPackageOptions.Recurse); Assert.Equal(4, (int)ExportPackageOptions.IncludeDependencies); Assert.Equal(8, (int)ExportPackageOptions.IncludeLibraryAssets); }
    [Fact] public void ExportPackage_StringOverload_RoundTripsAsset() { var asset = Add("String.txt", 1, "payload"); var package = PackagePath("string"); AssetDatabase.ExportPackage(asset, package); ImportIntoDestination(package); Assert.Equal("payload", AssetDatabase.LoadAssetAtPath<TextAsset>(asset)!.text); }
    [Fact] public void ExportPackage_ArrayOverload_RoundTripsAsset() { var asset = Add("Array.txt", 1, "payload"); var package = PackagePath("array"); AssetDatabase.ExportPackage(new[] { asset }, package); ImportIntoDestination(package); Assert.Equal("payload", AssetDatabase.LoadAssetAtPath<TextAsset>(asset)!.text); }
    [Fact] public void ExportPackage_Recurse_ExportsFolderContents() { var asset = Add("Folder.txt", 1, "payload"); var package = PackagePath("recurse"); AssetDatabase.ExportPackage("Assets/Export", package, ExportPackageOptions.Recurse); ImportIntoDestination(package); Assert.Equal("payload", AssetDatabase.LoadAssetAtPath<TextAsset>(asset)!.text); }
    [Fact] public void ExportPackage_FolderWithoutRecurseIsRejected() { Add("FolderReject.txt", 1, "payload"); Assert.Throws<ArgumentException>(() => AssetDatabase.ExportPackage("Assets/Export", PackagePath("folder-reject"), ExportPackageOptions.Default)); }
    [Fact] public void ExportPackage_IncludeDependencies_ExportsTransitiveAsset() { var dependency = Add("Dependency.txt", 2, "dependency"); var root = Add("Root.txt", 1, "reference: { guid: " + AssetDatabase.AssetPathToGUID(dependency) + " }"); var package = PackagePath("dependencies"); AssetDatabase.ExportPackage(root, package, ExportPackageOptions.IncludeDependencies); ImportIntoDestination(package); Assert.Equal("dependency", AssetDatabase.LoadAssetAtPath<TextAsset>(dependency)!.text); }
    [Fact] public void ExportPackage_IncludeLibraryAssetsReportsUnsupported() { var asset = Add("Library.txt", 1, "payload"); Assert.Throws<NotSupportedException>(() => AssetDatabase.ExportPackage(asset, PackagePath("library"), ExportPackageOptions.IncludeLibraryAssets)); }
    [Fact] public void ExportPackage_RejectsUnknownOptionBits() { var asset = Add("Unknown.txt", 1, "payload"); Assert.Throws<ArgumentOutOfRangeException>(() => AssetDatabase.ExportPackage(asset, PackagePath("unknown"), (ExportPackageOptions)16)); }

    private string Add(string name, int id, string contents)
    {
        var path = "Assets/Export/" + name;
        Directory.CreateDirectory(Path.GetDirectoryName(FullPath(path))!);
        File.WriteAllText(FullPath(path), contents);
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: " + id.ToString("x32") + "\n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private void ImportIntoDestination(string package)
    {
        EditorApplication.OpenProject(_destination);
        AssetDatabase.ImportPackage(package, false);
    }

    private string PackagePath(string name) => Path.Combine(_dir, name + ".unitypackage");
    private string FullPath(string path) => Path.Combine(_source, path);
}
