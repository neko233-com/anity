using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public class ShaderImporter : AssetImporter
{
    private readonly Dictionary<ShaderCompilerPlatform, string[]> _compilerDefines = new();

    public TextureImporterCompression defaultTextureCompression { get; set; } = TextureImporterCompression.Automatic;
    public int preprocessorOverride { get; set; }
    public HashSet<ShaderCompilerPlatform> shaderCompilerPlatforms { get; set; } = new();
    public bool disableOptimizations { get; set; }
    public bool nonModifiableTextures { get; set; }

    public void OnImportAsset(AssetImportContext ctx)
    {
        _ = ctx;
    }

    public string[] GetShaderCompilerDefines(ShaderCompilerPlatform platform)
    {
        return _compilerDefines.TryGetValue(platform, out var defines) ? (string[])defines.Clone() : Array.Empty<string>();
    }

    public void SetShaderCompilerDefines(ShaderCompilerPlatform platform, string[] defines)
    {
        if (defines != null)
            _compilerDefines[platform] = (string[])defines.Clone();
        else
            _compilerDefines.Remove(platform);
    }

    public static new ShaderImporter GetAtPath(string path)
    {
        return new ShaderImporter { assetPath = path };
    }
}

public class AssetImportContext
{
    public string assetPath { get; set; } = string.Empty;
    public UnityEngine.Object mainObject { get; set; }

    public void AddObjectToAsset(string identifier, UnityEngine.Object obj)
    {
        _ = identifier;
        _ = obj;
    }

    public void SetMainObject(UnityEngine.Object obj)
    {
        mainObject = obj;
    }

    public void DependsOnSourceAsset(string path)
    {
        _ = path;
    }
}
