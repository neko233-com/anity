using UnityEngine;

namespace UnityEditor;

public class ComputeShaderImporter : AssetImporter
{
    public bool disableOptimizations { get; set; }
    public int preprocessorOverride { get; set; }
    public string[] defaultTextures { get; set; }
    public string[] kernels { get; set; }
    public bool isComputeShader { get; set; } = true;

    public static new ComputeShaderImporter GetAtPath(string path)
    {
        return new ComputeShaderImporter { assetPath = path };
    }
}
