using System;
using System.IO;
using System.Text;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>StreamingAssets path + IO helpers — ≥12 cases.</summary>
public class StreamingAssetsTests : IDisposable
{
    private readonly string _root;

    public StreamingAssetsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "anity_sa_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        StreamingAssets.SetRootForTests(_root);
    }

    public void Dispose()
    {
        StreamingAssets.ClearRootOverride();
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch { }
    }

    [Fact]
    public void Root_UsesOverride()
    {
        Assert.Equal(_root, StreamingAssets.root);
    }

    [Fact]
    public void GetPath_CombinesRelative()
    {
        string p = StreamingAssets.GetPath("cfg/data.json");
        Assert.True(p.Replace('\\', '/').EndsWith("cfg/data.json") || p.Contains("cfg"));
    }

    [Fact]
    public void WriteRead_TextRoundTrip()
    {
        StreamingAssets.WriteAllText("hello.txt", "anity");
        Assert.True(StreamingAssets.Exists("hello.txt"));
        Assert.Equal("anity", StreamingAssets.ReadAllText("hello.txt"));
    }

    [Fact]
    public void WriteRead_BytesRoundTrip()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        StreamingAssets.WriteAllBytes("bin/payload.dat", data);
        Assert.Equal(data, StreamingAssets.ReadAllBytes("bin/payload.dat"));
    }

    [Fact]
    public void ReadMissing_Empty()
    {
        Assert.Equal(string.Empty, StreamingAssets.ReadAllText("nope.txt"));
        Assert.Empty(StreamingAssets.ReadAllBytes("nope.bin"));
        Assert.False(StreamingAssets.Exists("nope.txt"));
    }

    [Fact]
    public void GetFiles_ListsRelative()
    {
        StreamingAssets.WriteAllText("a.txt", "a");
        StreamingAssets.WriteAllText("b.txt", "b");
        var files = StreamingAssets.GetFiles("", "*.txt");
        Assert.Contains("a.txt", files);
        Assert.Contains("b.txt", files);
    }

    [Fact]
    public void GetDirectories_Lists()
    {
        StreamingAssets.WriteAllText("sub/x.txt", "x");
        var dirs = StreamingAssets.GetDirectories();
        Assert.Contains("sub", dirs);
        Assert.True(StreamingAssets.DirectoryExists("sub"));
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        StreamingAssets.WriteAllText("del.txt", "x");
        Assert.True(StreamingAssets.Delete("del.txt"));
        Assert.False(StreamingAssets.Exists("del.txt"));
        Assert.False(StreamingAssets.Delete("del.txt"));
    }

    [Fact]
    public void GetFileUrl_FileScheme()
    {
        StreamingAssets.WriteAllText("u.txt", "u");
        string url = StreamingAssets.GetFileUrl("u.txt");
        Assert.StartsWith("file://", url);
        Assert.Contains("u.txt", url.Replace('\\', '/'));
    }

    [Fact]
    public void CopyFrom_InstallsContent()
    {
        string src = Path.Combine(Path.GetTempPath(), "anity_src_" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(src, "installed");
        try
        {
            StreamingAssets.CopyFrom(src, "content/in.txt");
            Assert.Equal("installed", StreamingAssets.ReadAllText("content/in.txt"));
        }
        finally
        {
            if (File.Exists(src)) File.Delete(src);
        }
    }

    [Fact]
    public void CopyFrom_Missing_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            StreamingAssets.CopyFrom(Path.Combine(_root, "missing_src.bin"), "out.bin"));
    }

    [Fact]
    public void Application_StreamingAssetsPath_NonEmpty()
    {
        Assert.False(string.IsNullOrEmpty(Application.streamingAssetsPath));
        Assert.Contains("StreamingAssets", Application.streamingAssetsPath);
    }

    [Fact]
    public void ClearRootOverride_FallsBack()
    {
        StreamingAssets.ClearRootOverride();
        Assert.Equal(Application.streamingAssetsPath, StreamingAssets.root);
        StreamingAssets.SetRootForTests(_root);
    }

    [Fact]
    public void NestedWrite_CreatesParents()
    {
        StreamingAssets.WriteAllText("deep/a/b/c.json", "{\"ok\":1}");
        Assert.True(StreamingAssets.Exists("deep/a/b/c.json"));
        Assert.Contains("ok", StreamingAssets.ReadAllText("deep/a/b/c.json", Encoding.UTF8));
    }
}
