using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

public sealed class BuildStreamedSceneAssetBundleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-scenes-" + Guid.NewGuid().ToString("N"));
    public BuildStreamedSceneAssetBundleTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
    [Fact] public void FourLegacyOverloads_ArePublic() => Assert.Equal(4, typeof(BuildPipeline).GetMethods().Count(m => m.Name == nameof(BuildPipeline.BuildStreamedSceneAssetBundle)));
    [Fact] public void SingleScene_WritesBundle() { var p = Output(); Assert.True(Build(new[] { "Assets/One.unity" }, p)); Assert.True(File.Exists(p)); }
    [Fact] public void SingleScene_LoadsAsStreamedSceneBundle() { var p = Output(); Assert.True(Build(new[] { "Assets/One.unity" }, p)); var b = AssetBundle.LoadFromFile(p); Assert.NotNull(b); Assert.True(b!.isStreamedSceneAssetBundle); b.Unload(true); }
    [Fact] public void MultiScene_LoadsAllScenePaths() { var p = Output(); Assert.True(Build(new[] { "Assets/One.unity", "Assets/Two.unity" }, p)); var b = AssetBundle.LoadFromFile(p); Assert.NotNull(b); Assert.Equal(2, b!.GetAllScenePaths().Length); b.Unload(true); }
    [Fact] public void CrcOverload_ReturnsFinalCrc() { var p = Output(); Assert.True(Build(new[] { "Assets/One.unity" }, p, out var crc)); Assert.Equal(AssetBundleFormat.ComputeCrc(File.ReadAllBytes(p)), crc); }
    [Fact] public void OptionsOverload_WritesBundle() { var p = Output(); Assert.True(BuildPipeline.BuildStreamedSceneAssetBundle(new[] { "Assets/One.unity" }, p, BuildTarget.StandaloneOSX, BuildOptions.None)); }
    [Fact] public void RejectsEmptyLevels() => Assert.False(Build(Array.Empty<string>(), Output()));
    [Fact] public void RejectsNonSceneLevel() => Assert.False(Build(new[] { "Assets/One.txt" }, Output()));
    [Fact] public void RejectsMissingParent() => Assert.False(Build(new[] { "Assets/One.unity" }, Path.Combine(_dir, "missing", "bundle")));
    [Fact] public void RejectsEmptyPath() => Assert.False(Build(new[] { "Assets/One.unity" }, string.Empty));
#pragma warning disable CS0618
    private static bool Build(string[] levels, string path) => BuildPipeline.BuildStreamedSceneAssetBundle(levels, path, BuildTarget.StandaloneOSX);
    private static bool Build(string[] levels, string path, out uint crc) => BuildPipeline.BuildStreamedSceneAssetBundle(levels, path, BuildTarget.StandaloneOSX, out crc);
#pragma warning restore CS0618
    private string Output() => Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".bundle");
}
