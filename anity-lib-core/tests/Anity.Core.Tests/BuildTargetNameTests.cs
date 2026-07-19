using UnityEditor;
using Xunit;

namespace Anity.Core.Tests;

public sealed class BuildTargetNameTests
{
    [Fact] public void PublicMethodExists() => Assert.NotNull(typeof(BuildPipeline).GetMethod(nameof(BuildPipeline.GetBuildTargetName)));
    [Fact] public void Windows64Name() => Assert.Equal("StandaloneWindows64", BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneWindows64));
    [Fact] public void WindowsName() => Assert.Equal("StandaloneWindows", BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneWindows));
    [Fact] public void MacName() => Assert.Equal("StandaloneOSX", BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX));
    [Fact] public void MacIntelAliasIsCanonical() => Assert.Equal("StandaloneOSX", BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSXIntel));
    [Fact] public void IosName() => Assert.Equal("iOS", BuildPipeline.GetBuildTargetName(BuildTarget.iOS));
    [Fact] public void IPhoneAliasIsCanonical() => Assert.Equal("iOS", BuildPipeline.GetBuildTargetName(BuildTarget.iPhone));
    [Fact] public void AndroidName() => Assert.Equal("Android", BuildPipeline.GetBuildTargetName(BuildTarget.Android));
    [Fact] public void WebGlName() => Assert.Equal("WebGL", BuildPipeline.GetBuildTargetName(BuildTarget.WebGL));
    [Fact] public void VisionOsName() => Assert.Equal("VisionOS", BuildPipeline.GetBuildTargetName(BuildTarget.VisionOS));
}
