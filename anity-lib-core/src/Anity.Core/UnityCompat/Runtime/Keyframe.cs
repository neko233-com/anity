namespace UnityEngine;

public struct Keyframe
{
  public float time;
  public float value;
  public float inTangent;
  public float outTangent;

  public Keyframe(float time, float value)
  {
    this.time = time;
    this.value = value;
    inTangent = 0f;
    outTangent = 0f;
  }
}

