using System.Text;
using UnityEditor.VFX.Model;

namespace UnityEditor.VFX.Generation;

internal static class VfxAttributeCodeGenerator
{
    internal static string GenerateAttributeStruct(VfxAttributeUsageSet usageSet, string structName = "VFXAttributes")
    {
        if (usageSet is null) throw new ArgumentNullException(nameof(usageSet));
        return GenerateAttributeStruct(usageSet.Usages, structName);
    }

    internal static string GenerateAttributeStruct(
        IEnumerable<VfxSerializedAttributeUsage> usages,
        string structName = "VFXAttributes")
    {
        if (usages is null) throw new ArgumentNullException(nameof(usages));
        if (!VfxAttributeCatalog.IsShaderIdentifier(structName))
            throw new ArgumentException("VFX attribute struct name must be a shader identifier.", nameof(structName));

        IReadOnlyList<VfxAttributeDefinition> attributes = CollectStoredAttributes(usages);
        return GenerateAttributeStructFromAttributes(attributes, structName);
    }

    internal static string GenerateAttributeStructFromAttributes(
        IEnumerable<VfxAttributeDefinition> attributes,
        string structName = "VFXAttributes")
    {
        if (attributes is null) throw new ArgumentNullException(nameof(attributes));
        if (!VfxAttributeCatalog.IsShaderIdentifier(structName))
            throw new ArgumentException("VFX attribute struct name must be a shader identifier.", nameof(structName));

        IReadOnlyList<VfxAttributeDefinition> ordered = OrderAndValidate(attributes);
        var source = new StringBuilder().Append("struct ").Append(structName).Append("\n{\n");
        foreach (VfxAttributeDefinition attribute in ordered)
            source.Append("    ").Append(attribute.HlslType).Append(' ').Append(attribute.Name).Append(";\n");
        return source.Append("};\n").ToString();
    }

    internal static string GenerateSetAttributeStatement(
        VfxSerializedAttributeUsage usage,
        string targetPrefix = "")
    {
        if (usage is null) throw new ArgumentNullException(nameof(usage));
        if (usage.Model.ScriptType.TypeName is not ("SetAttribute" or "SetCustomAttribute"))
            throw new NotSupportedException(
                $"VFX attribute statement generation does not support '{usage.Model.ScriptType.TypeName}'.");

        if (targetPrefix.Length != 0 && !targetPrefix.EndsWith(".", StringComparison.Ordinal))
            throw new ArgumentException("VFX attribute target prefix must be empty or end in '.'.", nameof(targetPrefix));
        if (usage.IsCustom) return GenerateCustomStatement(usage, targetPrefix);
        VfxAttributeDefinition serialized = VfxAttributeCatalog.Find(usage.SerializedAttributeName);
        string[] channels = serialized.Variadic == VfxAttributeVariadic.True
            ? VfxAttributeCatalog.ChannelsToString(usage.Channels).Select(character => character.ToString()).ToArray()
            : new[] { string.Empty };
        var source = new StringBuilder();
        for (int index = 0; index < channels.Length; index++)
        {
            VfxAttributeDefinition target = usage.Attributes[index];
            int valueSize = serialized.Variadic == VfxAttributeVariadic.True ? 1 : target.ComponentCount;
            string parameterPostfix = serialized.Variadic == VfxAttributeVariadic.True
                ? "." + "xyzw"[index]
                : string.Empty;
            string value;
            if (usage.ValueSource == VfxAttributeValueSource.Source)
            {
                value = "Value" + parameterPostfix;
            }
            else if (usage.RandomMode == VfxAttributeRandomMode.Off)
            {
                value = PascalCase(serialized.Name) + parameterPostfix;
            }
            else
            {
                value = RandomValue(usage.RandomMode, valueSize, "A" + parameterPostfix, "B" + parameterPostfix);
            }
            source.Append(Compose(targetPrefix + target.Name, value, usage.Composition));
            if (index + 1 < channels.Length) source.Append('\n');
        }
        return source.ToString();
    }

    private static string GenerateCustomStatement(VfxSerializedAttributeUsage usage, string targetPrefix)
    {
        VfxAttributeDefinition target = AssertSingle(usage.Attributes);
        string value = usage.RandomMode == VfxAttributeRandomMode.Off
            ? "_" + PascalCase(target.Name)
            : RandomValue(usage.RandomMode, target.ComponentCount, "Min", "Max");
        return Compose(targetPrefix + target.Name, value, usage.Composition);
    }

    private static IReadOnlyList<VfxAttributeDefinition> CollectStoredAttributes(
        IEnumerable<VfxSerializedAttributeUsage> usages)
    {
        var byName = new Dictionary<string, VfxAttributeDefinition>(StringComparer.Ordinal);
        foreach (VfxSerializedAttributeUsage usage in usages)
        {
            foreach (VfxAttributeDefinition attribute in usage.Attributes)
                AddOrValidate(byName, attribute);
            if (usage.RequiresRandomSeed) AddOrValidate(byName, VfxAttributeCatalog.Find("seed"));
        }
        return OrderAndValidate(byName.Values);
    }

    private static IReadOnlyList<VfxAttributeDefinition> OrderAndValidate(
        IEnumerable<VfxAttributeDefinition> candidates)
    {
        var byName = new Dictionary<string, VfxAttributeDefinition>(StringComparer.Ordinal);
        foreach (VfxAttributeDefinition candidate in candidates) AddOrValidate(byName, candidate);
        var catalogOrder = VfxAttributeCatalog.Stored.Select((attribute, index) => (attribute.Name, index))
            .ToDictionary(item => item.Name, item => item.index, StringComparer.Ordinal);
        return byName.Values
            .OrderBy(attribute => catalogOrder.TryGetValue(attribute.Name, out int index) ? index : int.MaxValue)
            .ThenBy(attribute => attribute.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddOrValidate(
        IDictionary<string, VfxAttributeDefinition> attributes,
        VfxAttributeDefinition candidate)
    {
        if (!attributes.TryGetValue(candidate.Name, out VfxAttributeDefinition? existing))
        {
            attributes.Add(candidate.Name, candidate);
            return;
        }
        if (existing.ValueType != candidate.ValueType || existing.Space != candidate.Space)
            throw new InvalidDataException(
                $"VFX attribute '{candidate.Name}' is used with conflicting type or space definitions.");
    }

    private static string Compose(string target, string value, VfxAttributeComposition composition)
        => composition switch
        {
            VfxAttributeComposition.Overwrite => target + " = " + value + ";",
            VfxAttributeComposition.Add => target + " += " + value + ";",
            VfxAttributeComposition.Multiply => target + " *= " + value + ";",
            VfxAttributeComposition.Blend => target + " = lerp(" + target + "," + value + ",Blend);",
            _ => throw new ArgumentOutOfRangeException(nameof(composition))
        };

    private static string RandomValue(
        VfxAttributeRandomMode mode,
        int componentCount,
        string minimum,
        string maximum)
        => mode switch
        {
            VfxAttributeRandomMode.Uniform => $"lerp({minimum},{maximum},RAND)",
            VfxAttributeRandomMode.PerComponent =>
                $"lerp({minimum},{maximum},RAND{(componentCount == 1 ? string.Empty : componentCount.ToString(System.Globalization.CultureInfo.InvariantCulture))})",
            _ => throw new InvalidDataException("VFX random interpolation requires Uniform or PerComponent mode.")
        };

    private static string PascalCase(string name)
        => char.ToUpperInvariant(name[0]) + name.Substring(1);

    private static VfxAttributeDefinition AssertSingle(IReadOnlyList<VfxAttributeDefinition> attributes)
    {
        if (attributes.Count != 1)
            throw new InvalidDataException("Custom VFX attribute usage must resolve to exactly one attribute.");
        return attributes[0];
    }
}
