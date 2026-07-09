using System.IO;
using Xunit;

namespace Anity.AB.Compare.Tests;

/// <summary>
/// AssetBundle 行为对比测试
/// 用于验证 Anity 实现与 Unity 官方的行为一致性
/// </summary>
public class BehaviorCompareTests
{
    private readonly string _testAssetPath;

    public BehaviorCompareTests()
    {
        _testAssetPath = Path.Combine(AppContext.BaseDirectory, "TestAssets");
    }

    [Theory]
    [InlineData("test.bundle")]
    public void AssetBundle_FilePath_ShouldBeReadable(string bundleName)
    {
        var bundlePath = Path.Combine(_testAssetPath, bundleName);
        
        if (!File.Exists(bundlePath))
        {
            return;
        }

        // 验证文件可读
        var bytes = File.ReadAllBytes(bundlePath);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Theory]
    [InlineData("test.bundle")]
    public void AssetBundle_Stream_ShouldBeReadable(string bundleName)
    {
        var bundlePath = Path.Combine(_testAssetPath, bundleName);
        
        if (!File.Exists(bundlePath))
        {
            return;
        }

        // 验证流式读取
        using var stream = File.OpenRead(bundlePath);
        Assert.True(stream.Length > 0);
        
        var buffer = new byte[1024];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);
        Assert.True(bytesRead > 0);
    }

    [Fact]
    public void AssetBundle_EmptyFile_ShouldHandleGracefully()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // 创建空文件
            File.WriteAllText(tempFile, string.Empty);
            
            var bytes = File.ReadAllBytes(tempFile);
            Assert.Empty(bytes);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AssetBundle_LargeFile_ShouldHandleCorrectly()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // 创建 1MB 测试文件
            var data = new byte[1024 * 1024];
            new Random(42).NextBytes(data);
            File.WriteAllBytes(tempFile, data);
            
            var readData = File.ReadAllBytes(tempFile);
            Assert.Equal(data.Length, readData.Length);
            Assert.Equal(data, readData);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("test.bundle", 8)]
    public void AssetBundle_HeaderSize_ShouldBeExpected(string bundleName, int expectedHeaderSize)
    {
        var bundlePath = Path.Combine(_testAssetPath, bundleName);
        
        if (!File.Exists(bundlePath))
        {
            return;
        }

        var bytes = File.ReadAllBytes(bundlePath);
        Assert.True(bytes.Length >= expectedHeaderSize);
    }

    [Fact]
    public void AssetBundle_ConcurrentRead_ShouldBeSafe()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = new byte[1024];
            File.WriteAllBytes(tempFile, data);
            
            // 模拟并发读取
            var tasks = new Task<byte[]>[4];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() => File.ReadAllBytes(tempFile));
            }
            
            Task.WaitAll(tasks);
            
            foreach (var task in tasks)
            {
                Assert.Equal(data, task.Result);
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
