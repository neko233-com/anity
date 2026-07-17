using System.Reflection;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class RandomParityTests
{
    [Theory]
    [InlineData(0, 0.9996846914f)]
    [InlineData(1, 0.7742627859f)]
    [InlineData(2, 0.6809838414f)]
    [InlineData(3, 0.4604561925f)]
    [InlineData(4, 0.5944274068f)]
    [InlineData(5, 0.7847894430f)]
    [InlineData(6, 0.9143837690f)]
    [InlineData(7, 0.1373541504f)]
    [InlineData(8, 0.2568917572f)]
    [InlineData(9, 0.5561134219f)]
    [InlineData(10, 0.7822797298f)]
    [InlineData(11, 0.3288125098f)]
    public void Value_MatchesUnity2022PlayerSequenceForSeedOne(int index, float expected)
    {
        UnityEngine.Random.State saved = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(1);
            float actual = 0f;
            for (int current = 0; current <= index; current++) actual = UnityEngine.Random.value;
            Assert.Equal(expected, actual);
        }
        finally
        {
            UnityEngine.Random.state = saved;
        }
    }

    [Theory]
    [InlineData(1, 1u, 1812433254u, 1900727103u, 3690981084u)]
    [InlineData(2, 2u, 3624866507u, 1989020952u, 1186267769u)]
    [InlineData(17, 17u, 746594230u, 3313428687u, 2270273708u)]
    [InlineData(991, 991u, 825023996u, 3412291693u, 2220929026u)]
    [InlineData(-1, uint.MaxValue, 2482534044u, 1724139405u, 110473122u)]
    public void InitState_MatchesUnity2022FourWordState(
        int seed, uint s0, uint s1, uint s2, uint s3)
    {
        UnityEngine.Random.State saved = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(seed);
            Assert.Equal(new[] { s0, s1, s2, s3 }, StateWords(UnityEngine.Random.state));
        }
        finally
        {
            UnityEngine.Random.state = saved;
        }
    }

    [Fact]
    public void State_RestoreContinuesExactSequence()
    {
        UnityEngine.Random.State saved = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(991);
            _ = UnityEngine.Random.value;
            UnityEngine.Random.State checkpoint = UnityEngine.Random.state;
            float expected = UnityEngine.Random.value;
            _ = UnityEngine.Random.value;
            UnityEngine.Random.state = checkpoint;
            Assert.Equal(expected, UnityEngine.Random.value);
        }
        finally
        {
            UnityEngine.Random.state = saved;
        }
    }

    [Fact]
    public void State_HasNoPublicFieldsOrConstructors()
    {
        Type type = typeof(UnityEngine.Random.State);
        Assert.Empty(type.GetFields(BindingFlags.Instance | BindingFlags.Public));
        Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    private static uint[] StateWords(UnityEngine.Random.State state)
        => typeof(UnityEngine.Random.State)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .OrderBy(field => field.MetadataToken)
            .Select(field => unchecked((uint)(int)field.GetValue(state)!))
            .ToArray();
}
