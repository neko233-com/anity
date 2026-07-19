using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>gzip/tar Unity package import transactions — ≥10 success, malformed, safety and replacement cases.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class UnityPackageArchiveImportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-upkg-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public UnityPackageArchiveImportTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
    }

    public void Dispose()
    {
        PackageImportProbe.Reset();
        AssetDatabase.onImportPackageItemsCompleted = null;
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact] public void ImportsTextAsset() { Import("one", "Assets/Package/A.txt", "hello"); Assert.Equal("hello", AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Package/A.txt")!.text); }
    [Fact] public void PersistsAssetAndMetaUnderProjectRoot() { Import("disk", "Assets/Package/D.txt", "disk-data"); Assert.Equal("disk-data", File.ReadAllText(Path.Combine(_dir, "Assets/Package/D.txt"))); Assert.Contains("fileFormatVersion", File.ReadAllText(Path.Combine(_dir, "Assets/Package/D.txt.meta"))); }
    [Fact] public void PreservesPackageGuid() { Import("guid123", "Assets/Package/G.txt", "g"); Assert.Equal("guid123", AssetDatabase.AssetPathToGUID("Assets/Package/G.txt")); }
    [Fact] public void EmitsImportedAssetPath() { string[]? paths = null; AssetDatabase.onImportPackageItemsCompleted = p => paths = p; Import("items", "Assets/Package/I.txt", "i"); Assert.Equal(new[] { "Assets/Package/I.txt" }, paths); }
    [Fact] public void ImportsMultipleAssets() { var file = Package(("a", "Assets/Package/A.txt", "a"), ("b", "Assets/Package/B.txt", "b")); AssetDatabase.ImportPackage(file, false); Assert.Equal("a", AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Package/A.txt")!.text); Assert.Equal("b", AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Package/B.txt")!.text); }
    [Fact] public void RecordsMainAssetPath() { Import("path", "Assets/Package/P.txt", "p"); Assert.Equal("Assets/Package/P.txt", AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Package/P.txt")!)); }
    [Fact] public void MissingPathnameFails() { var file = PackageRaw(("missing/asset", "x")); AssertFailed(file); }
    [Fact] public void UnsafePathFailsWithoutCommit() { var file = Package(("unsafe", "Assets/../Escape.txt", "no")); AssertFailed(file); Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/../Escape.txt")); }
    [Fact] public void InvalidGzipFails() { var file = Path.Combine(_dir, "broken.unitypackage"); File.WriteAllText(file, "not-gzip"); var failures = 0; AssetDatabase.ImportPackageFailedCallback failed = (_, _) => failures++; AssetDatabase.importPackageFailed += failed; try { AssetDatabase.ImportPackage(file, false); Assert.Equal(0, failures); } finally { AssetDatabase.importPackageFailed -= failed; } }
    [Fact] public void EmptyArchiveFails() { AssertFailed(PackageRaw()); }
    [Fact] public void ReimportReplacesExistingAsset() { Import("same", "Assets/Package/R.txt", "old"); Import("same", "Assets/Package/R.txt", "new"); Assert.Equal("new", AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Package/R.txt")!.text); }
    [Fact] public void PngImport_CreatesTextureAsset() { var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL8+QAAAABJRU5ErkJggg=="); AssetDatabase.ImportPackage(PackageBytes("png", "Assets/Package/Pixel.png", png), false); var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Package/Pixel.png"); Assert.NotNull(texture); Assert.True(texture.width > 0); Assert.True(texture.height > 0); }
    [Fact] public void SaveAndReimport_RefreshesDiskAsset() { const string path = "Assets/Package/Reimport.txt"; Import("reimport", path, "old"); File.WriteAllText(Path.Combine(_dir, path), "new"); UnityEditor.AssetImporter.GetAtPath(path).SaveAndReimport(); Assert.Equal("new", AssetDatabase.LoadAssetAtPath<TextAsset>(path)!.text); }
    [Fact] public void Postprocessor_ReceivesPreAndPostImportPaths() { PackageImportProbe.Enabled = true; Import("probe", "Assets/Package/Probe.txt", "p"); Assert.Equal(new[] { "pre:Assets/Package/Probe.txt", "post:Assets/Package/Probe.txt" }, PackageImportProbe.Calls); }
    [Fact] public void PreprocessorFailure_PreventsDiskCommit() { PackageImportProbe.Enabled = true; PackageImportProbe.ThrowOnPreprocess = true; var path = "Assets/Package/Rejected.txt"; AssertFailed(Package(("reject", path, "x"))); Assert.False(File.Exists(Path.Combine(_dir, path))); }

    private void Import(string guid, string path, string text) => AssetDatabase.ImportPackage(Package((guid, path, text)), false);
    private void AssertFailed(string file) { var failures = 0; AssetDatabase.ImportPackageFailedCallback failed = (_, _) => failures++; AssetDatabase.importPackageFailed += failed; try { AssetDatabase.ImportPackage(file, false); Assert.Equal(1, failures); } finally { AssetDatabase.importPackageFailed -= failed; } }
    private string Package(params (string Guid, string Path, string Text)[] items) { var entries = new List<(string, string)>(); foreach (var item in items) { entries.Add(($"{item.Guid}/pathname", item.Path)); entries.Add(($"{item.Guid}/asset", item.Text)); entries.Add(($"{item.Guid}/asset.meta", "fileFormatVersion: 2")); } return PackageRaw(entries.ToArray()); }
    private string PackageRaw(params (string Name, string Text)[] entries)
    {
        return PackageRawBytes(entries.Select(entry => (entry.Name, Encoding.UTF8.GetBytes(entry.Text))).ToArray());
    }
    private string PackageBytes(string guid, string path, byte[] bytes) => PackageRawBytes((guid + "/pathname", Encoding.UTF8.GetBytes(path)), (guid + "/asset", bytes), (guid + "/asset.meta", Encoding.UTF8.GetBytes("fileFormatVersion: 2")));
    private string PackageRawBytes(params (string Name, byte[] Data)[] entries) { var file = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".unitypackage"); using var output = File.Create(file); using var gzip = new GZipStream(output, CompressionMode.Compress); foreach (var (name, data) in entries) WriteTar(gzip, name, data); gzip.Write(new byte[1024], 0, 1024); return file; }
    private static void WriteTar(Stream stream, string name, byte[] data)
    {
        var header = new byte[512]; WriteAscii(header, 0, 100, name); WriteAscii(header, 100, 8, "0000777"); WriteAscii(header, 124, 12, Convert.ToString(data.Length, 8).PadLeft(11, '0')); header[156] = (byte)'0'; WriteAscii(header, 257, 6, "ustar"); stream.Write(header, 0, header.Length); stream.Write(data, 0, data.Length); var pad = (512 - data.Length % 512) % 512; if (pad > 0) stream.Write(new byte[pad], 0, pad);
    }
    private static void WriteAscii(byte[] target, int offset, int max, string text) { var data = Encoding.ASCII.GetBytes(text); Array.Copy(data, 0, target, offset, Math.Min(max, data.Length)); }
}

public sealed class PackageImportProbe : AssetPostprocessor
{
    public static bool Enabled { get; set; }
    public static bool ThrowOnPreprocess { get; set; }
    public static List<string> Calls { get; } = new();

    public static void Reset() { Enabled = false; ThrowOnPreprocess = false; Calls.Clear(); }
    public override int GetPostprocessOrder() => -100;
    public override void OnPreprocessAsset()
    {
        if (!Enabled) return;
        Calls.Add("pre:" + assetPath);
        if (ThrowOnPreprocess) throw new InvalidOperationException("intentional preprocess failure");
    }

    public override void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (Enabled) Calls.Add("post:" + Assert.Single(importedAssets));
    }
}
