using System;
using UnityEngine.Rendering;

namespace UnityEngine;

public class Camera : Behaviour
{
  private static readonly Func<float, float> _safeDenominator = value => Mathf.Max(Math.Abs(value), 0.000001f);

  public float fieldOfView = 60f;
  public float nearClipPlane = 0.3f;
  public float farClipPlane = 1000f;
  public float orthographicSize = 5f;
  public bool orthographic;
  public float depth;
  public int cullingMask = -1;
  public int pixelWidth = 1920;
  public int pixelHeight = 1080;
  public CameraType cameraType = CameraType.Game;
  public bool useOcclusionCulling;
  public bool allowHDR = true;
  public bool allowMSAA = true;
  public RenderingPath renderingPath = RenderingPath.UsePlayerSettings;
  public CameraClearFlags clearFlags = CameraClearFlags.Skybox;
  public Color backgroundColor = Color.black;
  public float aspect => pixelHeight == 0 ? 0f : (float)pixelWidth / pixelHeight;
  public Vector4 rect { get; set; } = Vector4.one;
  public Matrix4x4 worldToCameraMatrix => Matrix4x4.identity;
  public Matrix4x4 projectionMatrix => Matrix4x4.identity;

  public static Camera main { get; } = new();
  public static Camera? current { get; set; } = main;

  public void Render()
  {
    var pipeline = RenderPipelineManager.currentPipeline;
    if (pipeline is null)
    {
      return;
    }

    var previous = current;
    current = this;
    var context = new ScriptableRenderContext();
    pipeline.Render(context, new[] { this });
    current = previous;
  }

  public void RenderWithShader(Shader shader, string replacementTag)
  {
    _ = shader;
    _ = replacementTag;
    Render();
  }

  public bool RenderToCubemap(Cubemap cubemap)
  {
    _ = cubemap;
    Render();
    return true;
  }

  public Vector3 WorldToScreenPoint(Vector3 worldPosition)
  {
    var world = worldPosition;
    var scale = orthographic ? orthographicSize : fieldOfView * 0.5f;
    scale = _safeDenominator(scale);
    var sx = (world.x / scale) * 0.5f + 0.5f;
    var sy = (world.y / scale) * 0.5f + 0.5f;
    return new Vector3(Mathf.Clamp01(sx) * pixelWidth, Mathf.Clamp01(sy) * pixelHeight, world.z);
  }

  public Vector3 WorldToViewportPoint(Vector3 worldPosition)
  {
    return ScreenToViewportPoint(WorldToScreenPoint(worldPosition));
  }

  public Vector3 ScreenToViewportPoint(Vector3 screenPosition)
  {
    if (pixelWidth == 0 || pixelHeight == 0)
    {
      return Vector3.zero;
    }

    return new Vector3(screenPosition.x / pixelWidth, screenPosition.y / pixelHeight, screenPosition.z);
  }

  public Vector3 ScreenToWorldPoint(Vector3 screenPosition)
  {
    if (pixelWidth == 0 || pixelHeight == 0)
    {
      return screenPosition;
    }

    var scale = orthographic ? orthographicSize : fieldOfView * 0.5f;
    scale = _safeDenominator(scale);
    var x = (screenPosition.x / pixelWidth - 0.5f) * scale;
    var y = (screenPosition.y / pixelHeight - 0.5f) * scale;
    return new Vector3(x, y, screenPosition.z);
  }

  public Vector3 ViewportToScreenPoint(Vector3 viewportPoint)
  {
    return new Vector3(viewportPoint.x * pixelWidth, viewportPoint.y * pixelHeight, viewportPoint.z);
  }

  public Vector3 ViewportToWorldPoint(Vector3 viewportPoint)
  {
    return ScreenToWorldPoint(ViewportToScreenPoint(viewportPoint));
  }

  public Ray ViewportPointToRay(Vector3 viewportPoint)
  {
    return ScreenPointToRay(ViewportToScreenPoint(viewportPoint));
  }

  public Ray ViewportPointToRay(float x, float y)
  {
    return ViewportPointToRay(new Vector3(x, y, 0f));
  }

  public Ray ScreenPointToRay(Vector3 pos)
  {
    return new Ray(pos, new Vector3(0f, 0f, 1f));
  }

  public Ray ScreenPointToRay(float x, float y)
  {
    return ScreenPointToRay(new Vector3(x, y, 0f));
  }

  public Ray ViewportPointToRay(Vector2 uv)
  {
    return ViewportPointToRay(new Vector3(uv.x, uv.y, 0f));
  }

  public class RenderRequest
  {
    public RenderTexture? destination;
    public int mipLevel;
    public CubemapFace face;
    public int slice;
    public bool isValid;
  }
}

public enum RenderingPath
{
  UsePlayerSettings,
  Forward,
  DeferredLighting,
  DeferredShading,
  VertexLit
}
