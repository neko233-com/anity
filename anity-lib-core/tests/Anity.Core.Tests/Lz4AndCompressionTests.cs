using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Real LZ4 codec + AssetBundle ALZ4/LZ4 compression — ≥12 cases.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public class Lz4AndCompressionTests : IDisposable
{
    private readonly string _dir;

    public Lz4AndCompressionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "lz4_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        AssetBundleCompression.DefaultCodec = AssetBundleCompression.CodecLz4;
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Lz4_Empty_RoundTrip()
    {
        var e = Lz4Codec.Encode(Array.Empty<byte>());
        Assert.Empty(e);
    }

    [Fact]
    public void Lz4_SmallLiteral_RoundTrip()
    {
        byte[] src = Encoding.UTF8.GetBytes("hello-lz4");
        var enc = Lz4Codec.Encode(src);
        var dec = Lz4Codec.Decode(enc, src.Length);
        Assert.Equal(src, dec);
    }

    [Fact]
    public void Lz4_Repetitive_CompressesAndRoundTrips()
    {
        // highly compressible
        byte[] src = Encoding.ASCII.GetBytes(new string('A', 4096) + new string('B', 4096));
        var enc = Lz4Codec.Encode(src);
        Assert.True(enc.Length < src.Length, $"enc={enc.Length} src={src.Length}");
        var dec = Lz4Codec.Decode(enc, src.Length);
        Assert.Equal(src, dec);
    }

    [Fact]
    public void Lz4_Random_RoundTrip()
    {
        var rng = new System.Random(42);
        byte[] src = new byte[2048];
        rng.NextBytes(src);
        var enc = Lz4Codec.Encode(src);
        var dec = Lz4Codec.Decode(enc, src.Length);
        Assert.Equal(src, dec);
    }

    [Fact]
    public void Lz4_Large_RoundTrip()
    {
        byte[] src = new byte[64 * 1024];
        for (int i = 0; i < src.Length; i++)
            src[i] = (byte)(i % 251);
        var enc = Lz4Codec.Encode(src);
        var dec = Lz4Codec.Decode(enc, src.Length);
        Assert.Equal(src, dec);
    }

    [Fact]
    public void Alz4_Lz4Codec_RoundTrip()
    {
        byte[] raw = Encoding.UTF8.GetBytes("unityfs-payload-" + new string('x', 200));
        var packed = AssetBundleCompression.Compress(raw, AssetBundleCompression.CodecLz4);
        Assert.True(AssetBundleCompression.IsCompressed(packed));
        Assert.Equal(AssetBundleCompression.CodecLz4, AssetBundleCompression.GetCodec(packed));
        Assert.Equal(AssetBundleCompression.Magic, BitConverter.ToUInt32(packed, 0));
        var outp = AssetBundleCompression.DecompressIfNeeded(packed);
        Assert.Equal(raw, outp);
    }

    [Fact]
    public void Alz4_DeflateCodec_RoundTrip()
    {
        byte[] raw = Encoding.UTF8.GetBytes("deflate-body-" + new string('y', 100));
        var packed = AssetBundleCompression.Compress(raw, AssetBundleCompression.CodecDeflate);
        Assert.Equal(AssetBundleCompression.CodecDeflate, AssetBundleCompression.GetCodec(packed));
        Assert.Equal(raw, AssetBundleCompression.DecompressIfNeeded(packed));
    }

    [Fact]
    public void Uncompressed_Passthrough()
    {
        byte[] raw = Encoding.UTF8.GetBytes("raw-ab");
        var r = AssetBundleCompression.MaybeCompress(raw, BuildAssetBundleOptions.UncompressedAssetBundle);
        Assert.Same(raw, r); // same reference for uncompressed
    }

    [Fact]
    public void MaybeCompress_None_NoMagic()
    {
        byte[] raw = Encoding.UTF8.GetBytes("UnityFS...");
        var r = AssetBundleCompression.MaybeCompress(raw, BuildAssetBundleOptions.None);
        Assert.False(AssetBundleCompression.IsCompressed(r));
    }

    [Fact]
    public void ChunkBased_Build_UsesLz4Codec()
    {
        AssetDatabase.CreateAsset(new TextAsset("lz4-real-payload-" + new string('z', 300)), "Assets/lz4real.txt");
        BuildPipeline.BuildAssetBundles(_dir, new[]
        {
            new AssetBundleBuild { assetBundleName = "lz4real", assetNames = new[] { "Assets/lz4real.txt" } }
        }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);

        string path = Path.Combine(_dir, "lz4real");
        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(AssetBundleCompression.Magic, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(AssetBundleCompression.CodecLz4, AssetBundleCompression.GetCodec(bytes));

        var ab = AssetBundle.LoadFromFile(path);
        Assert.NotNull(ab);
        var names = ab!.GetAllAssetNames();
        Assert.NotEmpty(names);
        var ta = ab.LoadAsset<TextAsset>(names[0]);
        Assert.NotNull(ta);
        Assert.Contains("lz4-real-payload", ta!.text);
        ab.Unload(true);
    }

    [Fact]
    public void DecompressIfNeeded_PlainUnityFs_Unchanged()
    {
        byte[] plain = Encoding.ASCII.GetBytes("UnityFS\0fake");
        var r = AssetBundleCompression.DecompressIfNeeded(plain);
        Assert.Equal(plain, r);
    }

    [Fact]
    public void GetCodec_NonCompressed_IsFF()
    {
        Assert.Equal(0xFF, AssetBundleCompression.GetCodec(new byte[] { 1, 2, 3, 4 }));
    }
}
