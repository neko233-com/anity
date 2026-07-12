namespace UnityEngine;

public enum WeightedMode
{
    None = 0,
    In = 1,
    Out = 2,
    Both = 3
}

public struct Keyframe
{
  public float time;
  public float value;
  public float inTangent;
  public float outTangent;
  public float inWeight;
  public float outWeight;
  public WeightedMode weightedMode;
  public int tangentMode;

  public Keyframe(float time, float value)
  {
    this.time = time;
    this.value = value;
    inTangent = 0f;
    outTangent = 0f;
    inWeight = 0.3333333f;
    outWeight = 0.3333333f;
    weightedMode = WeightedMode.None;
    tangentMode = 0;
  }

  public Keyframe(float time, float value, float inTangent, float outTangent)
  {
    this.time = time;
    this.value = value;
    this.inTangent = inTangent;
    this.outTangent = outTangent;
    inWeight = 0.3333333f;
    outWeight = 0.3333333f;
    weightedMode = WeightedMode.None;
    tangentMode = 0;
  }
}
