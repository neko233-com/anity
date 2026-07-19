using UnityEditor;
using Xunit;
namespace Anity.Core.Tests;
public sealed class BuildTargetSupportTests
{
 [Fact] public void CorrectOrderOverloadExists()=>Assert.NotNull(typeof(BuildPipeline).GetMethod(nameof(BuildPipeline.IsBuildTargetSupported),new[]{typeof(BuildTargetGroup),typeof(BuildTarget)}));
 [Fact] public void WindowsStandalone()=>Assert.True(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64));
 [Fact] public void MacStandalone()=>Assert.True(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneOSX));
 [Fact] public void Android()=>Assert.True(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android));
 [Fact] public void Ios()=>Assert.True(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS,BuildTarget.iOS));
 [Fact] public void WebGl()=>Assert.True(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL,BuildTarget.WebGL));
 [Fact] public void TvOs()=>Assert.True(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.tvOS,BuildTarget.tvOS));
 [Fact] public void RejectsWrongGroup()=>Assert.False(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.WebGL));
 [Fact] public void RejectsUnknownGroup()=>Assert.False(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Unknown,BuildTarget.Android));
 [Fact] public void LegacyOrderDelegates()=>Assert.True(BuildPipeline.IsBuildTargetSupported(BuildTarget.Android,BuildTargetGroup.Android));
}
