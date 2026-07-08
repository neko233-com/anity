using System;

namespace UnityEngine;

public static class Random
{
  private static readonly System.Random _rng = new();

  public static float value => (float)_rng.NextDouble();
  public static int Range(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);
  public static float Range(float minInclusive, float maxInclusive) => minInclusive + (maxInclusive - minInclusive) * value;
}
