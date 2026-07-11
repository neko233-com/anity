namespace UnityEngine;

public struct Keyframe
{
  public float time;
  public float value;
  public float inTangent;
  public float outTangent;
  public float inWeight;
  public float outWeight;
  public int weightedMode;

  public Keyframe(float time, float value)
  {
    this.time = time;
    this.value = value;
    inTangent = 0f;
    outTangent = 0f;
    inWeight = 0.3333333f;
    outWeight = 0.3333333f;
    weightedMode = 0;
  }

  public Keyframe(float time, float value, float inTangent, float outTangent)
  {
    this.time = time;
    this.value = value;
    this.inTangent = inTangent;
    this.outTangent = outTangent;
    inWeight = 0.3333333f;
    outWeight = 0.3333333f;
    weightedMode = 0;
  }
}
