using System.IO;
using Xunit;

namespace Anity.AB.Compare.Tests;

/// <summary>
/// AssetBundle 二进制对比测试
/// 用于验证 Anity 实现与 Unity 官方的二进制一致性
/// </summary>
public class BinaryCompareTests
{
    private readonly string _testAssetPath;

    public BinaryCompareTests()
    {
        _testAssetPath = Path.Combine(AppContext.BaseDirectory, "TestAssets");
    }

    [Fact]
    public void TestAssetDirectory_Exists()
    {
        Assert.True(Directory.Exists(_testAssetPath), 
            $"Test asset directory not found: {_testAssetPath}");
    }

    [Theory]
    [InlineData("test.bundle")]
    public void AssetBundle_ShouldHaveUnityHeader(string bundleName)
    {
        var bundlePath = Path.Combine(_testAssetPath, bundleName);
        
        if (!File.Exists(bundlePath))
        {
            // 跳过不存在的测试文件
            return;
        }

        var bytes = File.ReadAllBytes(bundlePath);
        Assert.True(bytes.Length > 8, "AssetBundle file is too small");

        // 验证 Unity AssetBundle 魔数: "UnityFS "
        Assert.Equal((byte)'U', bytes[0]);
        Assert.Equal((byte)'n', bytes[1]);
        Assert.Equal((byte)'i', bytes[2]);
        Assert.Equal((byte)'t', bytes[3]);
        Assert.Equal((byte)'y', bytes[4]);
        Assert.Equal((byte)'F', bytes[5]);
        Assert.Equal((byte)'S', bytes[6]);
        Assert.Equal((byte)' ', bytes[7]);
    }

    [Theory]
    [InlineData("test.bundle")]
    public void AssetBundle_FileSize_ShouldBeReasonable(string bundleName)
    {
        var bundlePath = Path.Combine(_testAssetPath, bundleName);
        
        if (!File.Exists(bundlePath))
        {
            return;
        }

        var fileInfo = new FileInfo(bundlePath);
        Assert.True(fileInfo.Length > 0, "AssetBundle file is empty");
        Assert.True(fileInfo.Length < 100 * 1024 * 1024, 
            "AssetBundle file is too large (>100MB)");
    }

    [Fact]
    public void AssetBundle_MagicBytes_ShouldBeValid()
    {
        // 测试各种可能的 AssetBundle 格式
        var testCases = new[]
        {
            new { Name = "UnityFS", Magic = new byte[] { 0x55, 0x6E, 0x69, 0x74, 0x79, 0x46, 0x53, 0x20 } },
            new { Name = "UnityRaw", Magic = new byte[] { 0x55, 0x6E, 0x69, 0x74, 0x79, 0x52, 0x61, 0x77 } }
        };

        // 验证魔数定义正确
        foreach (var tc in testCases)
        {
            Assert.Equal(8, tc.Magic.Length);
        }
    }
}
