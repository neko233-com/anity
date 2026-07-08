using System;

namespace UnityEngine;

[Flags]
public enum MeshColliderCookingOptions
{
  None = 0,
  CookForFasterSimulation = 1,
  DisableMeshDeformeration = 2,
  WeldColocatedVertices = 4,
  UseFastMidphase = 8
}
