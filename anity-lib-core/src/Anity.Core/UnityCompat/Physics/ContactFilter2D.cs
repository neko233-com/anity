namespace UnityEngine;

public struct ContactFilter2D
{
  public bool useTriggers;
  public bool useLayerMask;
  public LayerMask layerMask;
  public bool useDepth;
  public float minDepth;
  public float maxDepth;
  public bool useNormalAngle;
  public float minNormalAngle;
  public float maxNormalAngle;
  public bool useOutsidePoint;
  public bool useOutsideDistance;

  public void NoFilter()
  {
  }

  public bool IsFiltering()
  {
    return false;
  }
}
