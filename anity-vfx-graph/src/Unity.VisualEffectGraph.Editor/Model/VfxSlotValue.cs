using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace UnityEditor.VFX.Model;

internal enum VfxCoordinateSpace
{
    Local = 0,
    World = 1,
    None = int.MaxValue
}

internal enum VfxSlotValueKind
{
    Empty,
    Float,
    Int32,
    UInt32,
    Boolean,
    Float2,
    Float3,
    Float4,
    Color,
    Position,
    Direction,
    Vector,
    Transform,
    ObjectReference,
    AnimationCurve,
    Gradient,
    Structured
}

internal sealed class VfxSlotProperty
{
    internal VfxSlotProperty(
        string name,
        string serializedTypeName,
        string valueTypeName,
        VfxCoordinateSpace space,
        VfxSlotValue value)
    {
        Name = name;
        SerializedTypeName = serializedTypeName;
        ValueTypeName = valueTypeName;
        Space = space;
        Value = value;
    }

    internal string Name { get; }

    internal string SerializedTypeName { get; }

    internal string ValueTypeName { get; }

    internal VfxCoordinateSpace Space { get; }

    internal VfxSlotValue Value { get; }
}

internal sealed class VfxSlotValue
{
    private static readonly ReadOnlyDictionary<string, string> ExpectedTypes =
        new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VFXSlotAnimationCurve"] = "UnityEngine.AnimationCurve",
            ["VFXSlotBool"] = "System.Boolean",
            ["VFXSlotColor"] = "UnityEngine.Color",
            ["VFXSlotDirection"] = "UnityEditor.VFX.DirectionType",
            ["VFXSlotFloat"] = "System.Single",
            ["VFXSlotFloat2"] = "UnityEngine.Vector2",
            ["VFXSlotFloat3"] = "UnityEngine.Vector3",
            ["VFXSlotFloat4"] = "UnityEngine.Vector4",
            ["VFXSlotGradient"] = "UnityEngine.Gradient",
            ["VFXSlotInt"] = "System.Int32",
            ["VFXSlotMesh"] = "UnityEngine.Mesh",
            ["VFXSlotPosition"] = "UnityEditor.VFX.Position",
            ["VFXSlotTexture2D"] = "UnityEngine.Texture2D",
            ["VFXSlotTransform"] = "UnityEditor.VFX.Transform",
            ["VFXSlotUint"] = "System.UInt32",
            ["VFXSlotVector"] = "UnityEditor.VFX.Vector"
        });

    private VfxSlotValue(
        VfxSlotValueKind kind,
        string rawSerializedObject,
        IReadOnlyList<double>? components = null,
        double? scalar = null,
        long? signedInteger = null,
        ulong? unsignedInteger = null,
        bool? boolean = null,
        JsonElement? json = null,
        VfxObjectReference? objectReference = null)
    {
        Kind = kind;
        RawSerializedObject = rawSerializedObject;
        Components = components ?? Array.Empty<double>();
        Scalar = scalar;
        SignedInteger = signedInteger;
        UnsignedInteger = unsignedInteger;
        Boolean = boolean;
        Json = json;
        ObjectReference = objectReference;
    }

    internal VfxSlotValueKind Kind { get; }

    internal string RawSerializedObject { get; }

    internal IReadOnlyList<double> Components { get; }

    internal double? Scalar { get; }

    internal long? SignedInteger { get; }

    internal ulong? UnsignedInteger { get; }

    internal bool? Boolean { get; }

    internal JsonElement? Json { get; }

    internal VfxObjectReference? ObjectReference { get; }

    internal static VfxSlotValue Parse(
        VfxScriptType scriptType,
        string serializedTypeName,
        string valueTypeName,
        string rawSerializedObject)
    {
        if (scriptType is null) throw new ArgumentNullException(nameof(scriptType));
        string propertyType = NormalizeTypeName(serializedTypeName);
        string valueType = NormalizeTypeName(valueTypeName);
        if (ExpectedTypes.TryGetValue(scriptType.TypeName, out string? expectedType))
        {
            ValidateType(propertyType, expectedType, scriptType.TypeName, "property");
            ValidateType(valueType, expectedType, scriptType.TypeName, "value");
        }
        if (propertyType.Length != 0 && valueType.Length != 0 &&
            !string.Equals(propertyType, valueType, StringComparison.Ordinal))
            throw new InvalidDataException($"VFX slot '{scriptType.TypeName}' property/value types disagree.");

        string raw = rawSerializedObject.Trim();
        if (raw.Length == 0) return new VfxSlotValue(VfxSlotValueKind.Empty, rawSerializedObject);
        switch (scriptType.TypeName)
        {
            case "VFXSlotFloat":
                return new VfxSlotValue(
                    VfxSlotValueKind.Float,
                    rawSerializedObject,
                    scalar: ParseDouble(raw, scriptType.TypeName));
            case "VFXSlotInt":
                return new VfxSlotValue(
                    VfxSlotValueKind.Int32,
                    rawSerializedObject,
                    signedInteger: ParseInt32(raw, scriptType.TypeName));
            case "VFXSlotUint":
                return new VfxSlotValue(
                    VfxSlotValueKind.UInt32,
                    rawSerializedObject,
                    unsignedInteger: ParseUInt32(raw, scriptType.TypeName));
            case "VFXSlotBool":
                if (!bool.TryParse(raw, out bool boolean))
                    throw new InvalidDataException("VFXSlotBool contains invalid serialized value '" + raw + "'.");
                return new VfxSlotValue(VfxSlotValueKind.Boolean, rawSerializedObject, boolean: boolean);
        }

        JsonElement json = ParseJson(raw, scriptType.TypeName);
        return scriptType.TypeName switch
        {
            "VFXSlotFloat2" => WithComponents(VfxSlotValueKind.Float2, rawSerializedObject, json, null, "x", "y"),
            "VFXSlotFloat3" => WithComponents(VfxSlotValueKind.Float3, rawSerializedObject, json, null, "x", "y", "z"),
            "VFXSlotFloat4" => WithComponents(VfxSlotValueKind.Float4, rawSerializedObject, json, null, "x", "y", "z", "w"),
            "VFXSlotColor" => WithComponents(VfxSlotValueKind.Color, rawSerializedObject, json, null, "r", "g", "b", "a"),
            "VFXSlotPosition" => WithComponents(VfxSlotValueKind.Position, rawSerializedObject, json, "position", "x", "y", "z"),
            "VFXSlotDirection" => WithComponents(VfxSlotValueKind.Direction, rawSerializedObject, json, "direction", "x", "y", "z"),
            "VFXSlotVector" => WithComponents(VfxSlotValueKind.Vector, rawSerializedObject, json, "vector", "x", "y", "z"),
            "VFXSlotTransform" => ParseTransform(rawSerializedObject, json),
            "VFXSlotTexture2D" or "VFXSlotMesh" => ParseObjectReference(rawSerializedObject, json),
            "VFXSlotAnimationCurve" => new VfxSlotValue(VfxSlotValueKind.AnimationCurve, rawSerializedObject, json: json),
            "VFXSlotGradient" => new VfxSlotValue(VfxSlotValueKind.Gradient, rawSerializedObject, json: json),
            _ => new VfxSlotValue(VfxSlotValueKind.Structured, rawSerializedObject, json: json)
        };
    }

    internal static string NormalizeTypeName(string serializedTypeName)
    {
        string trimmed = serializedTypeName.Trim();
        int comma = trimmed.IndexOf(',');
        return (comma < 0 ? trimmed : trimmed.Substring(0, comma)).Trim();
    }

    private static void ValidateType(string actual, string expected, string slotType, string role)
    {
        if (actual.Length != 0 && !string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"VFX slot '{slotType}' has invalid {role} type '{actual}'; expected '{expected}'.");
    }

    private static VfxSlotValue WithComponents(
        VfxSlotValueKind kind,
        string raw,
        JsonElement json,
        string? wrapper,
        params string[] names)
    {
        JsonElement value = json;
        if (wrapper is not null && (!json.TryGetProperty(wrapper, out value) || value.ValueKind != JsonValueKind.Object))
            throw new InvalidDataException("VFX slot JSON is missing object '" + wrapper + "'.");
        var components = new double[names.Length];
        for (int index = 0; index < names.Length; index++)
        {
            if (!value.TryGetProperty(names[index], out JsonElement component) || !component.TryGetDouble(out components[index]))
                throw new InvalidDataException("VFX slot JSON is missing numeric component '" + names[index] + "'.");
        }
        return new VfxSlotValue(kind, raw, Array.AsReadOnly(components), json: json);
    }

    private static VfxSlotValue ParseTransform(string raw, JsonElement json)
    {
        var components = new List<double>(9);
        foreach (string groupName in new[] { "position", "angles", "scale" })
        {
            if (!json.TryGetProperty(groupName, out JsonElement group) || group.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("VFX transform is missing object '" + groupName + "'.");
            foreach (string componentName in new[] { "x", "y", "z" })
            {
                if (!group.TryGetProperty(componentName, out JsonElement component) || !component.TryGetDouble(out double value))
                    throw new InvalidDataException("VFX transform is missing numeric component '" + groupName + "." + componentName + "'.");
                components.Add(value);
            }
        }
        return new VfxSlotValue(VfxSlotValueKind.Transform, raw, components.AsReadOnly(), json: json);
    }

    private static VfxSlotValue ParseObjectReference(string raw, JsonElement json)
    {
        if (!json.TryGetProperty("obj", out JsonElement value) || value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("fileID", out JsonElement fileIdValue) || !fileIdValue.TryGetInt64(out long fileId) ||
            !value.TryGetProperty("guid", out JsonElement guidValue) || guidValue.ValueKind != JsonValueKind.String ||
            !value.TryGetProperty("type", out JsonElement typeValue) || !typeValue.TryGetInt32(out int type))
            throw new InvalidDataException("VFX object slot contains an invalid object reference.");
        string guid = guidValue.GetString() ?? string.Empty;
        if (guid.Length != 32 || guid.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("VFX object slot contains an invalid GUID.");
        return new VfxSlotValue(
            VfxSlotValueKind.ObjectReference,
            raw,
            json: json,
            objectReference: new VfxObjectReference(fileId, guid.ToLowerInvariant(), type));
    }

    private static JsonElement ParseJson(string raw, string slotType)
    {
        string json = raw;
        if (raw.Length >= 2 && raw[0] == '\'' && raw[raw.Length - 1] == '\'')
            json = raw.Substring(1, raw.Length - 2).Replace("''", "'");
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("VFX slot JSON must contain an object root.");
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("VFX slot '" + slotType + "' contains invalid serialized JSON.", exception);
        }
    }

    private static double ParseDouble(string raw, string slotType)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            throw new InvalidDataException("VFX slot '" + slotType + "' contains invalid floating-point data.");
        return result;
    }

    private static int ParseInt32(string raw, string slotType)
    {
        if (!int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int result))
            throw new InvalidDataException("VFX slot '" + slotType + "' contains invalid Int32 data.");
        return result;
    }

    private static uint ParseUInt32(string raw, string slotType)
    {
        if (!uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out uint result))
            throw new InvalidDataException("VFX slot '" + slotType + "' contains invalid UInt32 data.");
        return result;
    }
}

internal sealed class VfxObjectReference
{
    internal VfxObjectReference(long fileId, string guid, int type)
    {
        FileId = fileId;
        Guid = guid;
        Type = type;
    }

    internal long FileId { get; }

    internal string Guid { get; }

    internal int Type { get; }
}
