namespace UnityEngine;

public class AnimationClip : Object
{
  public string name { get; set; } = string.Empty;
  public float length { get; set; }
  public float frameRate { get; set; } = 60f;
  public bool legacy { get; set; }
  public bool humanMotion { get; set; }
  public bool empty { get; set; }
  public bool hasGenericRootTransform { get; set; }
  public bool hasMotionFloatCurves { get; set; }
  public bool hasRootCurves { get; set; }
  public Bounds localBounds { get; set; }
  public AnimationEvent[] events { get; set; } = System.Array.Empty<AnimationEvent>();

  public void SampleAnimation(GameObject go, float time)
  {
    _ = go;
    _ = time;
  }

  public void SetCurve(string relativePath, System.Type type, string propertyName, AnimationCurve curve)
  {
    _ = relativePath;
    _ = type;
    _ = propertyName;
    _ = curve;
  }

  public void EnsureQuaternionContinuity()
  {
  }

  public void ClearCurves()
  {
  }

  public void AddEvent(AnimationEvent evt)
  {
    _ = evt;
  }
}

public struct AnimationEvent
{
  public float time { get; set; }
  public string functionName { get; set; }
  public float floatParameter { get; set; }
  public int intParameter { get; set; }
  public string stringParameter { get; set; }
  public Object objectReferenceParameter { get; set; }
  public SendMessageOptions messageOptions { get; set; }
  public bool isFiredByLegacy { get; set; }
  public bool isFiredByAnimator { get; set; }
  public AnimatorStateInfo animatorStateInfo { get; set; }
  public AnimatorClipInfo animatorClipInfo { get; set; }
}
