using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Mathf.PerlinNoise — real noise (not constant zero).</summary>
public class MathfPerlinNoiseTests
{
    [Fact]
    public void PerlinNoise_NotConstantZero()
    {
        float a = Mathf.PerlinNoise(0.1f, 0.2f);
        float b = Mathf.PerlinNoise(1.5f, 3.7f);
        float c = Mathf.PerlinNoise(10f, 20f);
        // At least one sample non-zero; not all identical
        Assert.False(a == 0f && b == 0f && c == 0f);
        Assert.False(a == b && b == c);
    }

    [Fact]
    public void PerlinNoise_InUnityRange_01()
    {
        for (int i = 0; i < 50; i++)
        {
            float x = i * 0.37f;
            float y = i * 0.19f + 1.1f;
            float n = Mathf.PerlinNoise(x, y);
            Assert.InRange(n, 0f, 1f);
        }
    }

    [Fact]
    public void PerlinNoise_Deterministic()
    {
        float a = Mathf.PerlinNoise(2.25f, 4.5f);
        float b = Mathf.PerlinNoise(2.25f, 4.5f);
        Assert.Equal(a, b);
    }

    [Fact]
    public void PerlinNoise_Continuousish_Nearby()
    {
        float a = Mathf.PerlinNoise(1.0f, 1.0f);
        float b = Mathf.PerlinNoise(1.01f, 1.0f);
        // Adjacent samples should not jump by full range
        Assert.True(Mathf.Abs(a - b) < 0.5f, $"delta={Mathf.Abs(a - b)} a={a} b={b}");
    }

    [Fact]
    public void PerlinNoise_NegativeCoords()
    {
        float n = Mathf.PerlinNoise(-3.2f, -1.7f);
        Assert.InRange(n, 0f, 1f);
    }

    [Fact]
    public void PerlinNoise_IntegerLattice_Defined()
    {
        // Lattice points are defined (fade=0 at integers)
        float n = Mathf.PerlinNoise(2f, 3f);
        Assert.InRange(n, 0f, 1f);
    }

    [Fact]
    public void PerlinNoise_VariesAcrossSpace()
    {
        int distinct = 0;
        float last = float.NaN;
        for (int i = 0; i < 20; i++)
        {
            float n = Mathf.PerlinNoise(i * 0.5f, i * 0.3f);
            if (float.IsNaN(last) || Mathf.Abs(n - last) > 1e-4f)
                distinct++;
            last = n;
        }
        Assert.True(distinct >= 5);
    }

    [Fact]
    public void IsMsvcCl_DoesNotMatchClang()
    {
        Assert.False(Anity.Core.Runtime.Il2Cpp.Il2CppToolchain.IsMsvcCl(@"C:\llvm\bin\clang.exe"));
        Assert.False(Anity.Core.Runtime.Il2Cpp.Il2CppToolchain.IsMsvcCl(@"C:\llvm\bin\clang++.exe"));
        Assert.True(Anity.Core.Runtime.Il2Cpp.Il2CppToolchain.IsMsvcCl(@"C:\Program Files\MSVC\bin\cl.exe"));
        Assert.False(Anity.Core.Runtime.Il2Cpp.Il2CppToolchain.IsMsvcCl(""));
    }

    [Fact]
    public void IsMsvcCl_GccFalse()
    {
        Assert.False(Anity.Core.Runtime.Il2Cpp.Il2CppToolchain.IsMsvcCl("/usr/bin/g++"));
        Assert.False(Anity.Core.Runtime.Il2Cpp.Il2CppToolchain.IsMsvcCl("gcc"));
    }

    [Fact]
    public void PerlinNoiseRaw_NotAlwaysZero()
    {
        float r = Mathf.PerlinNoiseRaw(0.3f, 0.7f);
        // raw is roughly [-1,1]; should not be identically 0 for this sample
        Assert.NotEqual(0f, r);
    }
}
