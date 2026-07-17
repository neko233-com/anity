using System.Collections.ObjectModel;
using UnityEditor.VFX.Serialization;

namespace UnityEditor.VFX.Model;

internal enum VfxAttributeValueType
{
    Boolean,
    UInt32,
    Int32,
    Float,
    Float2,
    Float3,
    Float4
}

internal enum VfxAttributeVariadic
{
    False = 0,
    True = 1,
    BelongsToVariadic = 2
}

internal enum VfxSpaceableType
{
    None = 0,
    Position = 1,
    Direction = 2,
    Matrix = 3,
    Vector = 4
}

[Flags]
internal enum VfxAttributeMode
{
    None = 0,
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write,
    ReadSource = 4
}

internal enum VfxAttributeComposition
{
    Overwrite = 0,
    Add = 1,
    Multiply = 2,
    Blend = 3
}

internal enum VfxAttributeRandomMode
{
    Off = 0,
    PerComponent = 1,
    Uniform = 2
}

internal enum VfxAttributeValueSource
{
    Slot = 0,
    Source = 1
}

internal enum VfxVariadicChannels
{
    X = 0,
    Y = 1,
    Z = 2,
    XY = 3,
    XZ = 4,
    YZ = 5,
    XYZ = 6
}

internal sealed class VfxAttributeDefinition
{
    internal VfxAttributeDefinition(
        string name,
        VfxAttributeValueType valueType,
        string defaultHlsl,
        VfxAttributeVariadic variadic = VfxAttributeVariadic.False,
        VfxSpaceableType space = VfxSpaceableType.None,
        bool readOnly = false,
        bool writeOnly = false,
        bool localOnly = false,
        bool isInternal = false,
        IReadOnlyList<string>? componentNames = null)
    {
        Name = name;
        ValueType = valueType;
        DefaultHlsl = defaultHlsl;
        Variadic = variadic;
        Space = space;
        IsReadOnly = readOnly;
        IsWriteOnly = writeOnly;
        IsLocalOnly = localOnly;
        IsInternal = isInternal;
        ComponentNames = componentNames ?? Array.Empty<string>();
    }

    internal string Name { get; }
    internal VfxAttributeValueType ValueType { get; }
    internal string HlslType => ValueType switch
    {
        VfxAttributeValueType.Boolean => "bool",
        VfxAttributeValueType.UInt32 => "uint",
        VfxAttributeValueType.Int32 => "int",
        VfxAttributeValueType.Float => "float",
        VfxAttributeValueType.Float2 => "float2",
        VfxAttributeValueType.Float3 => "float3",
        VfxAttributeValueType.Float4 => "float4",
        _ => throw new ArgumentOutOfRangeException()
    };
    internal int ComponentCount => ValueType switch
    {
        VfxAttributeValueType.Float2 => 2,
        VfxAttributeValueType.Float3 => 3,
        VfxAttributeValueType.Float4 => 4,
        _ => 1
    };
    internal string DefaultHlsl { get; }
    internal VfxAttributeVariadic Variadic { get; }
    internal VfxSpaceableType Space { get; }
    internal bool IsReadOnly { get; }
    internal bool IsWriteOnly { get; }
    internal bool IsLocalOnly { get; }
    internal bool IsInternal { get; }
    internal IReadOnlyList<string> ComponentNames { get; }
}

internal static class VfxAttributeCatalog
{
    private static readonly ReadOnlyCollection<VfxAttributeDefinition> Definitions = CreateDefinitions().AsReadOnly();
    private static readonly ReadOnlyDictionary<string, VfxAttributeDefinition> ByName =
        new(Definitions.ToDictionary(definition => definition.Name, StringComparer.Ordinal));

    internal static IReadOnlyList<VfxAttributeDefinition> All => Definitions;

    internal static IReadOnlyList<VfxAttributeDefinition> Stored
        => Definitions.Where(definition => definition.Variadic != VfxAttributeVariadic.True).ToArray();

    internal static bool TryFind(string name, out VfxAttributeDefinition? definition)
        => ByName.TryGetValue(name, out definition);

    internal static VfxAttributeDefinition Find(string name)
    {
        if (!TryFind(name, out VfxAttributeDefinition? definition))
            throw new KeyNotFoundException("Unknown VFX attribute '" + name + "'.");
        return definition!;
    }

    internal static VfxAttributeDefinition CreateCustom(string name, int signature)
    {
        if (!IsShaderIdentifier(name))
            throw new InvalidDataException($"Custom VFX attribute name '{name}' is not a valid shader identifier.");
        VfxAttributeValueType type = signature switch
        {
            0 => VfxAttributeValueType.Float,
            1 => VfxAttributeValueType.Float2,
            2 => VfxAttributeValueType.Float3,
            3 => VfxAttributeValueType.Float4,
            4 => VfxAttributeValueType.Boolean,
            5 => VfxAttributeValueType.UInt32,
            6 => VfxAttributeValueType.Int32,
            _ => throw new InvalidDataException($"Custom VFX attribute '{name}' has invalid AttributeType '{signature}'.")
        };
        string defaultHlsl = type switch
        {
            VfxAttributeValueType.Boolean => "false",
            VfxAttributeValueType.UInt32 => "0u",
            VfxAttributeValueType.Int32 => "0",
            VfxAttributeValueType.Float => "0.0",
            _ => HlslType(type) + "(0.0, " + string.Join(", ", Enumerable.Repeat("0.0", ComponentCount(type) - 1)) + ")"
        };
        return new VfxAttributeDefinition(name, type, defaultHlsl);
    }

    internal static IReadOnlyList<VfxAttributeDefinition> Expand(
        VfxAttributeDefinition definition,
        VfxVariadicChannels channels)
    {
        if (definition.Variadic != VfxAttributeVariadic.True) return new[] { definition };
        string selected = ChannelsToString(channels);
        var result = new List<VfxAttributeDefinition>(selected.Length);
        foreach (char channel in selected)
        {
            int index = channel - 'X';
            if (index < 0 || index >= definition.ComponentNames.Count)
                throw new InvalidDataException($"VFX variadic attribute '{definition.Name}' cannot select channel '{channel}'.");
            result.Add(Find(definition.ComponentNames[index]));
        }
        return result.AsReadOnly();
    }

    internal static string ChannelsToString(VfxVariadicChannels channels) => channels switch
    {
        VfxVariadicChannels.X => "X",
        VfxVariadicChannels.Y => "Y",
        VfxVariadicChannels.Z => "Z",
        VfxVariadicChannels.XY => "XY",
        VfxVariadicChannels.XZ => "XZ",
        VfxVariadicChannels.YZ => "YZ",
        VfxVariadicChannels.XYZ => "XYZ",
        _ => throw new InvalidDataException($"VFX variadic channels value '{(int)channels}' is invalid.")
    };

    internal static bool IsShaderIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name) || !(name[0] == '_' || IsAsciiLetter(name[0]))) return false;
        for (int index = 1; index < name.Length; index++)
            if (!(name[index] == '_' || IsAsciiLetter(name[index]) || char.IsDigit(name[index]))) return false;
        return true;
    }

    private static bool IsAsciiLetter(char value)
        => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static string HlslType(VfxAttributeValueType type)
        => new VfxAttributeDefinition(string.Empty, type, string.Empty).HlslType;

    private static int ComponentCount(VfxAttributeValueType type)
        => new VfxAttributeDefinition(string.Empty, type, string.Empty).ComponentCount;

    private static List<VfxAttributeDefinition> CreateDefinitions()
    {
        var definitions = new List<VfxAttributeDefinition>
        {
            D("seed", VfxAttributeValueType.UInt32, "0u", readOnly: true),
            D("oldPosition", VfxAttributeValueType.Float3, "float3(0.0, 0.0, 0.0)", space: VfxSpaceableType.Position),
            D("position", VfxAttributeValueType.Float3, "float3(0.0, 0.0, 0.0)", space: VfxSpaceableType.Position),
            D("velocity", VfxAttributeValueType.Float3, "float3(0.0, 0.0, 0.0)", space: VfxSpaceableType.Vector),
            D("direction", VfxAttributeValueType.Float3, "float3(0.0, 0.0, 1.0)", space: VfxSpaceableType.Vector),
            D("color", VfxAttributeValueType.Float3, "float3(1.0, 1.0, 1.0)"),
            D("alpha", VfxAttributeValueType.Float, "1.0"),
            D("size", VfxAttributeValueType.Float, "0.1"),
            D("scaleX", VfxAttributeValueType.Float, "1.0", VfxAttributeVariadic.BelongsToVariadic),
            D("scaleY", VfxAttributeValueType.Float, "1.0", VfxAttributeVariadic.BelongsToVariadic),
            D("scaleZ", VfxAttributeValueType.Float, "1.0", VfxAttributeVariadic.BelongsToVariadic),
            D("lifetime", VfxAttributeValueType.Float, "1.0"),
            D("age", VfxAttributeValueType.Float, "0.0"),
            D("angleX", VfxAttributeValueType.Float, "0.0", VfxAttributeVariadic.BelongsToVariadic),
            D("angleY", VfxAttributeValueType.Float, "0.0", VfxAttributeVariadic.BelongsToVariadic),
            D("angleZ", VfxAttributeValueType.Float, "0.0", VfxAttributeVariadic.BelongsToVariadic),
            D("angularVelocityX", VfxAttributeValueType.Float, "0.0", VfxAttributeVariadic.BelongsToVariadic),
            D("angularVelocityY", VfxAttributeValueType.Float, "0.0", VfxAttributeVariadic.BelongsToVariadic),
            D("angularVelocityZ", VfxAttributeValueType.Float, "0.0", VfxAttributeVariadic.BelongsToVariadic),
            D("texIndex", VfxAttributeValueType.Float, "0.0"),
            D("meshIndex", VfxAttributeValueType.UInt32, "0u"),
            D("pivotX", VfxAttributeValueType.Float, "0.0", VfxAttributeVariadic.BelongsToVariadic),
            D("pivotY", VfxAttributeValueType.Float, "0.0", VfxAttributeVariadic.BelongsToVariadic),
            D("pivotZ", VfxAttributeValueType.Float, "0.0", VfxAttributeVariadic.BelongsToVariadic),
            D("particleId", VfxAttributeValueType.UInt32, "0u", readOnly: true),
            D("axisX", VfxAttributeValueType.Float3, "float3(1.0, 0.0, 0.0)", space: VfxSpaceableType.Vector),
            D("axisY", VfxAttributeValueType.Float3, "float3(0.0, 1.0, 0.0)", space: VfxSpaceableType.Vector),
            D("axisZ", VfxAttributeValueType.Float3, "float3(0.0, 0.0, 1.0)", space: VfxSpaceableType.Vector),
            D("alive", VfxAttributeValueType.Boolean, "true"),
            D("mass", VfxAttributeValueType.Float, "1.0"),
            D("targetPosition", VfxAttributeValueType.Float3, "float3(0.0, 0.0, 0.0)", space: VfxSpaceableType.Position),
            D("eventCount", VfxAttributeValueType.UInt32, "0u", writeOnly: true, localOnly: true),
            D("spawnTime", VfxAttributeValueType.Float, "0.0", readOnly: true),
            D("particleIndexInStrip", VfxAttributeValueType.UInt32, "0u", readOnly: true, localOnly: true),
            D("spawnIndex", VfxAttributeValueType.UInt32, "0u", readOnly: true),
            D("stripIndex", VfxAttributeValueType.UInt32, "0u", readOnly: true, localOnly: true),
            D("particleCountInStrip", VfxAttributeValueType.UInt32, "0u", readOnly: true, localOnly: true),
            D("spawnIndexInStrip", VfxAttributeValueType.UInt32, "0u", readOnly: true),
            D("spawnCount", VfxAttributeValueType.Float, "1.0", readOnly: true),
            D("stripAlive", VfxAttributeValueType.Boolean, "true", isInternal: true),
            D("angle", VfxAttributeValueType.Float3, "float3(0.0, 0.0, 0.0)", VfxAttributeVariadic.True,
                componentNames: new[] { "angleX", "angleY", "angleZ" }),
            D("angularVelocity", VfxAttributeValueType.Float3, "float3(0.0, 0.0, 0.0)", VfxAttributeVariadic.True,
                componentNames: new[] { "angularVelocityX", "angularVelocityY", "angularVelocityZ" }),
            D("pivot", VfxAttributeValueType.Float3, "float3(0.0, 0.0, 0.0)", VfxAttributeVariadic.True,
                componentNames: new[] { "pivotX", "pivotY", "pivotZ" }),
            D("scale", VfxAttributeValueType.Float3, "float3(1.0, 1.0, 1.0)", VfxAttributeVariadic.True,
                componentNames: new[] { "scaleX", "scaleY", "scaleZ" })
        };
        if (definitions.Select(definition => definition.Name).Distinct(StringComparer.Ordinal).Count() != definitions.Count)
            throw new InvalidOperationException("VFX attribute catalog contains duplicate names.");
        return definitions;
    }

    private static VfxAttributeDefinition D(
        string name,
        VfxAttributeValueType type,
        string defaultHlsl,
        VfxAttributeVariadic variadic = VfxAttributeVariadic.False,
        VfxSpaceableType space = VfxSpaceableType.None,
        bool readOnly = false,
        bool writeOnly = false,
        bool localOnly = false,
        bool isInternal = false,
        IReadOnlyList<string>? componentNames = null)
        => new(name, type, defaultHlsl, variadic, space, readOnly, writeOnly, localOnly, isInternal, componentNames);
}

internal sealed class VfxSerializedAttributeUsage
{
    internal VfxSerializedAttributeUsage(
        VfxModel model,
        string serializedAttributeName,
        IReadOnlyList<VfxAttributeDefinition> attributes,
        VfxAttributeMode mode,
        VfxAttributeComposition composition,
        VfxAttributeRandomMode randomMode,
        VfxAttributeValueSource valueSource,
        VfxVariadicChannels channels,
        bool isCustom)
    {
        Model = model;
        SerializedAttributeName = serializedAttributeName;
        Attributes = attributes;
        Mode = mode;
        Composition = composition;
        RandomMode = randomMode;
        ValueSource = valueSource;
        Channels = channels;
        IsCustom = isCustom;
    }

    internal VfxModel Model { get; }
    internal string SerializedAttributeName { get; }
    internal IReadOnlyList<VfxAttributeDefinition> Attributes { get; }
    internal VfxAttributeMode Mode { get; }
    internal VfxAttributeComposition Composition { get; }
    internal VfxAttributeRandomMode RandomMode { get; }
    internal VfxAttributeValueSource ValueSource { get; }
    internal VfxVariadicChannels Channels { get; }
    internal bool IsCustom { get; }
    internal bool RequiresRandomSeed => RandomMode != VfxAttributeRandomMode.Off && ValueSource == VfxAttributeValueSource.Slot;
}

internal sealed class VfxAttributeUsageSet
{
    private readonly ReadOnlyCollection<VfxSerializedAttributeUsage> _usages;

    private VfxAttributeUsageSet(List<VfxSerializedAttributeUsage> usages)
    {
        _usages = usages.AsReadOnly();
    }

    internal IReadOnlyList<VfxSerializedAttributeUsage> Usages => _usages;

    internal static VfxAttributeUsageSet Create(VfxTypedGraph graph)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        var usages = new List<VfxSerializedAttributeUsage>();
        foreach (VfxModel model in graph.Models)
        {
            switch (model.ScriptType.TypeName)
            {
                case "SetAttribute":
                case "AttributeFromCurve":
                case "VFXAttributeParameter":
                case "VFXSpawnerSetAttribute":
                    usages.Add(ParseBuiltIn(model));
                    break;
                case "SetCustomAttribute":
                    usages.Add(ParseCustom(model));
                    break;
            }
        }
        return new VfxAttributeUsageSet(usages);
    }

    private static VfxSerializedAttributeUsage ParseBuiltIn(VfxModel model)
    {
        string attributeName = ReadRequiredString(model, "attribute");
        VfxAttributeDefinition definition;
        try
        {
            definition = VfxAttributeCatalog.Find(attributeName);
        }
        catch (KeyNotFoundException exception)
        {
            throw new InvalidDataException($"VFX model '{model.FileId}' references unknown built-in attribute '{attributeName}'.", exception);
        }
        VfxVariadicChannels channels = ReadEnum(model, "channels", VfxVariadicChannels.XYZ);
        IReadOnlyList<VfxAttributeDefinition> expanded = VfxAttributeCatalog.Expand(definition, channels);

        if (model.ScriptType.TypeName == "VFXSpawnerSetAttribute")
        {
            VfxAttributeRandomMode spawnerRandom = ReadEnum(
                model, "randomMode", VfxAttributeRandomMode.Off);
            return new VfxSerializedAttributeUsage(
                model, attributeName, expanded,
                VfxAttributeMode.Write,
                VfxAttributeComposition.Overwrite,
                spawnerRandom,
                VfxAttributeValueSource.Slot,
                channels,
                false);
        }

        if (model.ScriptType.TypeName == "VFXAttributeParameter")
        {
            int location = VfxYamlFields.ReadInt32(model.Document.RawText, "location") ?? 0;
            if (location is not (0 or 1))
                throw new InvalidDataException($"VFX Attribute Parameter '{model.FileId}' has invalid location '{location}'.");
            string mask = VfxYamlFields.ReadString(model.Document.RawText, "mask") ?? "xyz";
            if (definition.Variadic == VfxAttributeVariadic.True)
                expanded = ExpandMask(definition, mask, model.FileId);
            return new VfxSerializedAttributeUsage(
                model, attributeName, expanded,
                location == 0 ? VfxAttributeMode.Read : VfxAttributeMode.ReadSource,
                VfxAttributeComposition.Overwrite, VfxAttributeRandomMode.Off,
                VfxAttributeValueSource.Slot, channels, false);
        }

        VfxAttributeComposition composition = ReadEnum(model, "Composition", VfxAttributeComposition.Overwrite);
        VfxAttributeRandomMode random = model.ScriptType.TypeName == "SetAttribute"
            ? ReadEnum(model, "Random", VfxAttributeRandomMode.Off)
            : VfxAttributeRandomMode.Off;
        VfxAttributeValueSource source = model.ScriptType.TypeName == "SetAttribute"
            ? ReadEnum(model, "Source", VfxAttributeValueSource.Slot)
            : VfxAttributeValueSource.Slot;
        if (source == VfxAttributeValueSource.Source && random != VfxAttributeRandomMode.Off)
            throw new InvalidDataException($"VFX SetAttribute '{model.FileId}' Source mode requires Random Off.");
        ValidateAccess(expanded, composition == VfxAttributeComposition.Overwrite ? VfxAttributeMode.Write : VfxAttributeMode.ReadWrite, model);
        return new VfxSerializedAttributeUsage(
            model, attributeName, expanded,
            composition == VfxAttributeComposition.Overwrite ? VfxAttributeMode.Write : VfxAttributeMode.ReadWrite,
            composition, random, source, channels, false);
    }

    private static VfxSerializedAttributeUsage ParseCustom(VfxModel model)
    {
        string attributeName = ReadRequiredString(model, "attribute");
        int signature = VfxYamlFields.ReadInt32(model.Document.RawText, "AttributeType") ?? 0;
        VfxAttributeDefinition definition = VfxAttributeCatalog.CreateCustom(attributeName, signature);
        VfxAttributeComposition composition = ReadEnum(model, "Composition", VfxAttributeComposition.Overwrite);
        VfxAttributeRandomMode random = ReadEnum(model, "Random", VfxAttributeRandomMode.Off);
        return new VfxSerializedAttributeUsage(
            model, attributeName, new[] { definition },
            composition == VfxAttributeComposition.Overwrite ? VfxAttributeMode.Write : VfxAttributeMode.ReadWrite,
            composition, random, VfxAttributeValueSource.Slot, VfxVariadicChannels.XYZ, true);
    }

    private static IReadOnlyList<VfxAttributeDefinition> ExpandMask(
        VfxAttributeDefinition definition,
        string mask,
        long modelId)
    {
        if (mask.Length is < 1 or > 3 || mask.Any(character => "xyzXYZ".IndexOf(character) < 0))
            throw new InvalidDataException($"VFX Attribute Parameter '{modelId}' has invalid variadic mask '{mask}'.");
        return mask.Select(character => definition.ComponentNames[char.ToLowerInvariant(character) - 'x'])
            .Select(VfxAttributeCatalog.Find)
            .ToArray();
    }

    private static void ValidateAccess(
        IReadOnlyList<VfxAttributeDefinition> attributes,
        VfxAttributeMode mode,
        VfxModel model)
    {
        foreach (VfxAttributeDefinition attribute in attributes)
        {
            if (attribute.IsReadOnly && (mode & VfxAttributeMode.Write) != 0)
                throw new InvalidDataException($"VFX model '{model.FileId}' cannot write read-only attribute '{attribute.Name}'.");
            if (attribute.IsWriteOnly && (mode & VfxAttributeMode.Read) != 0)
                throw new InvalidDataException($"VFX model '{model.FileId}' cannot read write-only attribute '{attribute.Name}'.");
        }
    }

    private static string ReadRequiredString(VfxModel model, string field)
    {
        string? value = VfxYamlFields.ReadString(model.Document.RawText, field);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"VFX model '{model.FileId}' requires non-empty {field}.");
        return value;
    }

    private static T ReadEnum<T>(VfxModel model, string field, T defaultValue) where T : struct, Enum
    {
        int value = VfxYamlFields.ReadInt32(model.Document.RawText, field) ?? Convert.ToInt32(defaultValue);
        if (!Enum.IsDefined(typeof(T), value))
            throw new InvalidDataException($"VFX model '{model.FileId}' has invalid {field} '{value}'.");
        return (T)Enum.ToObject(typeof(T), value);
    }
}
