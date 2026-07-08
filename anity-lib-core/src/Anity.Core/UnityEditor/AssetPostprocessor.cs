using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public abstract class AssetPostprocessor
{
  public string? assetPath;

  public virtual int GetAssetLoadPriority() => 0;

  public virtual int GetPostprocessOrder()
  {
    return 0;
  }

  public virtual void OnPreprocessAsset()
  {
  }

  public virtual void OnPostprocessAssetbundleNameChanged(string assetPath, string previous, string next)
  {
    _ = assetPath;
    _ = previous;
    _ = next;
  }

  public virtual uint GetVersion()
  {
    return 0u;
  }

  public virtual void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
  {
    _ = importedAssets;
    _ = deletedAssets;
    _ = movedAssets;
    _ = movedFromAssetPaths;
  }

  public virtual void OnPreprocessTexture()
  {
  }

  public virtual void OnPostprocessTexture(Texture2D texture)
  {
    _ = texture;
  }

  public virtual void OnPreprocessModel()
  {
  }

  public virtual void OnPostprocessModel(GameObject root)
  {
    _ = root;
  }

  public virtual void OnPreprocessAnimation()
  {
  }

  public virtual void OnPostprocessAnimation(GameObject root, AnimationClip clip)
  {
    _ = root;
    _ = clip;
  }

  public virtual void OnPreprocessAudio()
  {
  }

  public virtual void OnPostprocessAudio(AudioClip clip)
  {
    _ = clip;
  }

  public virtual void OnPreprocessHumanoid()
  {
  }

  public virtual void OnPostprocessHumanoid(GameObject root, AvatarMask avatarMask, float humanScale)
  {
    _ = root;
    _ = avatarMask;
    _ = humanScale;
  }

  public virtual void OnPreprocessSpeedTree()
  {
  }

  public virtual void OnPostprocessSpeedTree(GameObject root)
  {
    _ = root;
  }

  public virtual void OnPostprocessSprite(Texture2D texture, Sprite[] sprites)
  {
    _ = texture;
    _ = sprites;
  }

  public virtual void OnPostprocessMaterial(Material material)
  {
    _ = material;
  }

  public virtual void OnPostprocessGameObjectWithUserProperties(GameObject root, string[] propNames, object[] values)
  {
    _ = root;
    _ = propNames;
    _ = values;
  }

  public virtual void OnPostprocessRenderTexture(RenderTexture renderTexture)
  {
    _ = renderTexture;
  }

  public virtual void OnPostprocessCubemap(Cubemap cubemap)
  {
    _ = cubemap;
  }

  public virtual void OnPostprocessFont(Font font)
  {
    _ = font;
  }

  public virtual void OnPostprocessLightmap(LightmapData lightmapData)
  {
    _ = lightmapData;
  }

  public virtual void OnPostprocessMesh(Mesh mesh)
  {
    _ = mesh;
  }

  public virtual void OnPostprocessAvatar(RawAvatar avatar)
  {
    _ = avatar;
  }

  public virtual void OnPreprocessShader()
  {
  }

  public virtual void OnPostprocessShader(Shader shader, ShaderSnippetData snippet, ShaderCompilerData compilerData)
  {
    _ = shader;
    _ = snippet;
    _ = compilerData;
  }
}

public struct ShaderSnippetData
{
  public ShaderCompilerPlatform platform;
  public ShaderType shaderType;
  public string source;
}

public struct ShaderCompilerData
{
  public ShaderCompilerPlatform platform;
  public GraphicsTier graphicsTier;
  public string defines;
}

public enum ShaderType
{
  Vertex,
  Fragment,
  Geometry,
  Tessellation,
  MeshHull,
  Domain
}

public enum GraphicsTier
{
  Tier1,
  Tier2,
  Tier3
}
