namespace UnityEngine;

public struct Vector4
{
  public float x;
  public float y;
  public float z;
  public float w;

  public Vector4(float x, float y, float z, float w)
  {
    this.x = x;
    this.y = y;
    this.z = z;
    this.w = w;
  }

  public static Vector4 zero => new(0f, 0f, 0f, 0f);
  public static Vector4 one => new(1f, 1f, 1f, 1f);

  public override string ToString()
  {
    return $"({x}, {y}, {z}, {w})";
  }
}

