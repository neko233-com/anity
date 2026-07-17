namespace UnityEngine;

[Scripting.UsedByNativeCode]
public struct UIVertex
{
  public Vector3 position;
  public Vector3 normal;
  public Vector4 tangent;
  public Color32 color;
  public Vector4 uv0;
  public Vector4 uv1;
  public Vector4 uv2;
  public Vector4 uv3;

  public static UIVertex simpleVert = new()
  {
    position = Vector3.zero,
    normal = new Vector3(0f, 0f, -1f),
    tangent = new Vector4(1f, 0f, 0f, -1f),
    color = new Color32(255, 255, 255, 255),
    uv0 = Vector4.zero,
    uv1 = Vector4.zero,
    uv2 = Vector4.zero,
    uv3 = Vector4.zero
  };
}
