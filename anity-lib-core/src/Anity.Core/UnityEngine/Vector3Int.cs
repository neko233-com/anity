using System;

namespace UnityEngine;

/// <summary>
/// 3D integer vector.
/// </summary>
public struct Vector3Int : IEquatable<Vector3Int>
{
    public int x;
    public int y;
    public int z;

    public Vector3Int(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static Vector3Int zero => new Vector3Int(0, 0, 0);
    public static Vector3Int one => new Vector3Int(1, 1, 1);
    public static Vector3Int up => new Vector3Int(0, 1, 0);
    public static Vector3Int down => new Vector3Int(0, -1, 0);
    public static Vector3Int left => new Vector3Int(-1, 0, 0);
    public static Vector3Int right => new Vector3Int(1, 0, 0);
    public static Vector3Int forward => new Vector3Int(0, 0, 1);
    public static Vector3Int back => new Vector3Int(0, 0, -1);

    public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
    public float sqrMagnitude => x * x + y * y + z * z;

    public static Vector3Int operator +(Vector3Int a, Vector3Int b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Vector3Int operator -(Vector3Int a, Vector3Int b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Vector3Int operator *(Vector3Int a, int d) => new(a.x * d, a.y * d, a.z * d);
    public static Vector3Int operator *(int d, Vector3Int a) => new(a.x * d, a.y * d, a.z * d);
    public static Vector3Int operator /(Vector3Int a, int d) => new(a.x / d, a.y / d, a.z / d);
    public static Vector3Int operator -(Vector3Int a) => new(-a.x, -a.y, -a.z);

    public static bool operator ==(Vector3Int lhs, Vector3Int rhs) => lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z;
    public static bool operator !=(Vector3Int lhs, Vector3Int rhs) => !(lhs == rhs);

    public static implicit operator Vector3(Vector3Int v) => new(v.x, v.y, v.z);
    public static explicit operator Vector3Int(Vector3 v) => new((int)v.x, (int)v.y, (int)v.z);

    public bool Equals(Vector3Int other) => x == other.x && y == other.y && z == other.z;
    public override bool Equals(object obj) => obj is Vector3Int other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(x, y, z);
    public override string ToString() => $"({x}, {y}, {z})";
}

/// <summary>
/// Integer bounds.
/// </summary>
public struct BoundsInt : IEquatable<BoundsInt>
{
    public Vector3Int min;
    public Vector3Int max;

    public BoundsInt(Vector3Int min, Vector3Int max)
    {
        this.min = min;
        this.max = max;
    }

    public Vector3Int size => max - min;
    public Vector3Int center => (min + max) / 2;
    public Vector3Int allMin => new(Math.Min(min.x, min.y), Math.Min(min.y, min.z), Math.Min(min.z, min.x));
    public Vector3Int allMax => new(Math.Max(max.x, max.y), Math.Max(max.y, max.z), Math.Max(max.z, max.x));

    public bool Equals(BoundsInt other) => min.Equals(other.min) && max.Equals(other.max);
    public override bool Equals(object obj) => obj is BoundsInt other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(min, max);
    public override string ToString() => $"BoundsInt(min={min}, max={max})";
}
