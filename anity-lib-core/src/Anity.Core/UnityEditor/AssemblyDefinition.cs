using System;
using System.Collections.Generic;

namespace UnityEditor;

/// <summary>
/// Unity Assembly Definition asset for defining assembly dependencies and compilation settings.
/// </summary>
[Serializable]
public class AssemblyDefinition
{
    public string name = string.Empty;
    public string rootNamespace = string.Empty;
    public string[] references = Array.Empty<string>();
    public string[] includePlatforms = Array.Empty<string>();
    public string[] excludePlatforms = Array.Empty<string>();
    public bool allowUnsafeCode = false;
    public bool overrideReferences = false;
    public string[] precompiledReferences = Array.Empty<string>();
    public bool autoReferenced = true;
    public int defineConstraints = 0;
    public string[] versionDefines = Array.Empty<string>();
    public bool noEngineReferences = false;

    public AssemblyDefinition()
    {
    }

    public AssemblyDefinition(string name)
    {
        this.name = name;
    }

    public AssemblyDefinition(string name, string[] references)
    {
        this.name = name;
        this.references = references;
    }
}

/// <summary>
/// Assembly Definition Reference asset for referencing other assemblies.
/// </summary>
[Serializable]
public class AssemblyDefinitionReference
{
    public string reference = string.Empty;

    public AssemblyDefinitionReference()
    {
    }

    public AssemblyDefinitionReference(string reference)
    {
        this.reference = reference;
    }
}
