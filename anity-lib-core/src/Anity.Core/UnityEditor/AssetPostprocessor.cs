using System;
using System.Runtime.CompilerServices;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UnityEditor;

/// <summary>
/// Base class for import pipeline messages. Unity discovers callbacks such as
/// OnPreprocessAsset by name on derived classes; those callbacks are deliberately
/// not members of this public API surface.
/// </summary>
public class AssetPostprocessor
{
  private AssetImportContext m_Context;
  private string m_PathName;

  public string assetPath
  {
    get => m_PathName;
    set => m_PathName = value;
  }

  public AssetImporter assetImporter => AssetDatabase.GetImporterAtPath(assetPath);

  public AssetImportContext context
  {
    get => m_Context;
    internal set => m_Context = value;
  }

  [Obsolete("To set or get the preview, call EditorUtility.SetAssetPreview or AssetPreview.GetAssetPreview instead", true)]
  public Texture2D preview
  {
    get => AssetPostprocessorPreviewStore.Get(this);
    set => AssetPostprocessorPreviewStore.Set(this, value);
  }

  public virtual int GetPostprocessOrder() => 0;

  public virtual uint GetVersion() => 0u;

  public void LogWarning(string warning) => Debug.LogWarning(warning);

  public void LogWarning(string warning, UnityEngine.Object context) => Debug.LogWarning((object)warning, context);

  public void LogError(string warning) => Debug.LogError(warning);

  public void LogError(string warning, UnityEngine.Object context) => Debug.LogError((object)warning, context);
}

internal static class AssetPostprocessorPreviewStore
{
  private sealed class PreviewHolder
  {
    internal Texture2D Value;
  }

  private static readonly ConditionalWeakTable<AssetPostprocessor, PreviewHolder> Previews = new();

  internal static Texture2D Get(AssetPostprocessor processor)
  {
    return Previews.TryGetValue(processor, out var holder) ? holder.Value : null;
  }

  internal static void Set(AssetPostprocessor processor, Texture2D preview)
  {
    if (preview is null)
    {
      Previews.Remove(processor);
      return;
    }
    Previews.GetValue(processor, _ => new PreviewHolder()).Value = preview;
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
