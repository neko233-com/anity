using System;
using System.Collections.Generic;

namespace UnityEditor
{
  public enum ManagedStrippingLevel
  {
    Disabled = 0,
    Low = 1,
    Medium = 2,
    High = 3
  }

  public enum ManagedStrippingEngineClass
  {
    Disabled = 0,
    ModuleStrip = 1,
    ModuleStripAndEngine = 2,
    LinkXml = 3
  }

  public enum StrippingUsedAsOption
  {
    EngineModule = 0,
    Scripts = 1,
    EngineModuleAndScripts = 2
  }

  [Flags]
  public enum ManagedStrippingEngineClassStripOptions
  {
    None = 0,
    Engine = 1,
    Module = 2,
    StripReleaseEngineCodeOnly = 4
  }

  [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Interface | AttributeTargets.Delegate, AllowMultiple = true)]
  public class PreserveAttribute : Attribute
  {
    public PreserveAttribute() { }
  }

  [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
  public class UsedByNativeCodeAttribute : Attribute
  {
    public string Name { get; set; }
    public UsedByNativeCodeAttribute() { }
    public UsedByNativeCodeAttribute(string name) { Name = name; }
  }

  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Interface | AttributeTargets.Delegate, AllowMultiple = false)]
  public class UsedByNativeCodeAttribute2 : Attribute
  {
    public string Name { get; set; }
    public UsedByNativeCodeAttribute2() { }
    public UsedByNativeCodeAttribute2(string name) { Name = name; }
  }

  public class LinkXmlGenerator
  {
    private readonly List<LinkAssembly> m_Assemblies = new List<LinkAssembly>();

    public IReadOnlyList<LinkAssembly> Assemblies => m_Assemblies;

    public void AddAssembly(string name)
    {
      m_Assemblies.Add(new LinkAssembly { Name = name });
    }

    public void AddType(string assembly, string type)
    {
      var asm = FindOrCreate(assembly);
      asm.Types.Add(new LinkType { Name = type, PreserveAll = true });
    }

    public void AddMethod(string assembly, string type, string method)
    {
      var asm = FindOrCreate(assembly);
      var t = FindOrCreateType(asm, type);
      t.Methods.Add(new LinkMethod { Name = method });
    }

    public void AddField(string assembly, string type, string field)
    {
      var asm = FindOrCreate(assembly);
      var t = FindOrCreateType(asm, type);
      t.Fields.Add(new LinkField { Name = field });
    }

    public string ToXml()
    {
      var lines = new List<string>();
      lines.Add("<linker>");
      foreach (var asm in m_Assemblies)
      {
        lines.Add($"  <assembly fullname=\"{asm.Name}\" preserve=\"{(asm.PreserveAll ? "all" : "nothing")}\">");
        foreach (var type in asm.Types)
        {
          if (type.PreserveAll)
          {
            lines.Add($"    <type fullname=\"{type.Name}\" preserve=\"all\"/>");
          }
          else
          {
            lines.Add($"    <type fullname=\"{type.Name}\">");
            foreach (var method in type.Methods)
              lines.Add($"      <method signature=\"{method.Name}\"/>");
            foreach (var field in type.Fields)
              lines.Add($"      <field signature=\"{field.Name}\"/>");
            lines.Add("    </type>");
          }
        }
        lines.Add("  </assembly>");
      }
      lines.Add("</linker>");
      return string.Join(Environment.NewLine, lines);
    }

    private LinkAssembly FindOrCreate(string assembly)
    {
      foreach (var asm in m_Assemblies)
      {
        if (string.Equals(asm.Name, assembly, StringComparison.Ordinal))
          return asm;
      }
      var newAsm = new LinkAssembly { Name = assembly };
      m_Assemblies.Add(newAsm);
      return newAsm;
    }

    private LinkType FindOrCreateType(LinkAssembly asm, string type)
    {
      foreach (var t in asm.Types)
      {
        if (string.Equals(t.Name, type, StringComparison.Ordinal))
          return t;
      }
      var newType = new LinkType { Name = type };
      asm.Types.Add(newType);
      return newType;
    }
  }

  public class LinkAssembly
  {
    public string Name { get; set; }
    public bool PreserveAll { get; set; }
    public List<LinkType> Types { get; } = new List<LinkType>();
  }

  public class LinkType
  {
    public string Name { get; set; }
    public bool PreserveAll { get; set; }
    public List<LinkMethod> Methods { get; } = new List<LinkMethod>();
    public List<LinkField> Fields { get; } = new List<LinkField>();
  }

  public class LinkMethod
  {
    public string Name { get; set; }
    public string Signature { get; set; }
  }

  public class LinkField
  {
    public string Name { get; set; }
  }

  public sealed class ManagedStrippingInfo
  {
    public ManagedStrippingLevel StrippingLevel { get; set; } = ManagedStrippingLevel.Medium;
    public bool StripEngineCode { get; set; } = true;
    public bool StripPhysicsCode { get; set; }
    public int EngineCodeStrippingOptions { get; set; } = 2;
    public string[] LinkXmlFiles { get; set; } = Array.Empty<string>();

    public ManagedStrippingInfo() { }
  }

  public static class CodeStrippingUtils
  {
    public static string GenerateLinkXml(ManagedStrippingInfo info)
    {
      var gen = new LinkXmlGenerator();
      return gen.ToXml();
    }

    public static string[] GetStrippedAssemblies(ManagedStrippingInfo info, string[] allAssemblies)
    {
      if (info.StrippingLevel == ManagedStrippingLevel.Disabled)
        return allAssemblies;
      return allAssemblies;
    }

    public static long CalculateStrippedSize(string assemblyPath, ManagedStrippingLevel level)
    {
      return 0L;
    }
  }
}
