using System.Collections.ObjectModel;
using System.Text.Json;
using UnityEditor.ShaderGraph.Serialization;

namespace UnityEditor.ShaderGraph.Model;

internal sealed class ShaderGraphBlackboard
{
    private readonly ReadOnlyCollection<ShaderGraphProperty> _properties;
    private readonly ReadOnlyCollection<ShaderGraphKeyword> _keywords;
    private readonly ReadOnlyCollection<ShaderGraphDropdown> _dropdowns;
    private readonly ReadOnlyCollection<ShaderGraphCategory> _categories;
    private readonly ReadOnlyDictionary<string, ShaderGraphProperty> _propertiesById;

    private ShaderGraphBlackboard(
        List<ShaderGraphProperty> properties,
        List<ShaderGraphKeyword> keywords,
        List<ShaderGraphDropdown> dropdowns,
        List<ShaderGraphCategory> categories)
    {
        _properties = properties.AsReadOnly();
        _keywords = keywords.AsReadOnly();
        _dropdowns = dropdowns.AsReadOnly();
        _categories = categories.AsReadOnly();
        _propertiesById = new ReadOnlyDictionary<string, ShaderGraphProperty>(
            properties.ToDictionary(value => value.ObjectId, StringComparer.Ordinal));
    }

    internal IReadOnlyList<ShaderGraphProperty> Properties => _properties;

    internal IReadOnlyList<ShaderGraphKeyword> Keywords => _keywords;

    internal IReadOnlyList<ShaderGraphDropdown> Dropdowns => _dropdowns;

    internal IReadOnlyList<ShaderGraphCategory> Categories => _categories;

    internal IReadOnlyDictionary<string, ShaderGraphProperty> PropertiesById => _propertiesById;

    internal static ShaderGraphBlackboard Create(MultiJsonAsset asset)
    {
        if (asset is null) throw new ArgumentNullException(nameof(asset));
        if (asset.Format != ShaderGraphSerializationFormat.MultiJson)
            throw new NotSupportedException("Legacy Shader Graph blackboards must be upgraded before model construction.");

        List<MultiJsonDocument> propertyDocuments = ResolveList(asset, "m_Properties");
        List<MultiJsonDocument> keywordDocuments = ResolveList(asset, "m_Keywords");
        List<MultiJsonDocument> dropdownDocuments = ResolveList(asset, "m_Dropdowns");
        List<MultiJsonDocument> categoryDocuments = ResolveList(asset, "m_CategoryData");

        var properties = propertyDocuments.Select(ShaderGraphProperty.Parse).ToList();
        var keywords = keywordDocuments.Select(ShaderGraphKeyword.Parse).ToList();
        var dropdowns = dropdownDocuments.Select(ShaderGraphDropdown.Parse).ToList();
        var knownInputs = properties.Select(value => value.ObjectId)
            .Concat(keywords.Select(value => value.ObjectId))
            .Concat(dropdowns.Select(value => value.ObjectId))
            .ToHashSet(StringComparer.Ordinal);
        var categories = categoryDocuments
            .Select(document => ShaderGraphCategory.Parse(document, knownInputs))
            .ToList();
        return new ShaderGraphBlackboard(properties, keywords, dropdowns, categories);
    }

    private static List<MultiJsonDocument> ResolveList(MultiJsonAsset asset, string propertyName)
    {
        var result = new List<MultiJsonDocument>();
        if (!asset.Graph.Root.TryGetProperty(propertyName, out JsonElement references)) return result;
        if (references.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"GraphData.{propertyName} must be an array.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement reference in references.EnumerateArray())
        {
            string objectId = ShaderGraphJson.ReadObjectReference(reference, "GraphData." + propertyName);
            if (!seen.Add(objectId))
                throw new InvalidDataException($"GraphData.{propertyName} contains duplicate object '{objectId}'.");
            if (!asset.TryResolve(objectId, out MultiJsonDocument? document) || document is null)
                throw new InvalidDataException($"GraphData.{propertyName} references missing object '{objectId}'.");
            result.Add(document);
        }
        return result;
    }
}

internal sealed class ShaderGraphProperty
{
    private ShaderGraphProperty(
        string objectId,
        string name,
        string referenceName,
        ShaderGraphPropertyKind kind,
        bool generatePropertyBlock,
        bool hidden,
        int precision,
        JsonElement value,
        int floatType,
        double rangeMinimum,
        double rangeMaximum,
        bool overrideHlslDeclaration,
        int hlslDeclaration)
    {
        ObjectId = objectId;
        Name = name;
        ReferenceName = referenceName;
        Kind = kind;
        GeneratePropertyBlock = generatePropertyBlock;
        Hidden = hidden;
        Precision = precision;
        Value = value;
        FloatType = floatType;
        RangeMinimum = rangeMinimum;
        RangeMaximum = rangeMaximum;
        OverrideHlslDeclaration = overrideHlslDeclaration;
        HlslDeclaration = hlslDeclaration;
    }

    internal string ObjectId { get; }

    internal string Name { get; }

    internal string ReferenceName { get; }

    internal ShaderGraphPropertyKind Kind { get; }

    internal bool GeneratePropertyBlock { get; }

    internal bool Hidden { get; }

    internal int Precision { get; }

    internal JsonElement Value { get; }

    internal int FloatType { get; }

    internal double RangeMinimum { get; }

    internal double RangeMaximum { get; }

    internal bool OverrideHlslDeclaration { get; }

    internal int HlslDeclaration { get; }

    internal int EffectiveHlslDeclaration
        => OverrideHlslDeclaration ? HlslDeclaration : GeneratePropertyBlock ? 2 : 1;

    internal static ShaderGraphProperty Parse(MultiJsonDocument document)
    {
        ShaderGraphPropertyKind kind = document.Type switch
        {
            "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty" => ShaderGraphPropertyKind.Vector1,
            "UnityEditor.ShaderGraph.Internal.Vector2ShaderProperty" => ShaderGraphPropertyKind.Vector2,
            "UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty" => ShaderGraphPropertyKind.Vector3,
            "UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty" => ShaderGraphPropertyKind.Vector4,
            "UnityEditor.ShaderGraph.Internal.ColorShaderProperty" => ShaderGraphPropertyKind.Color,
            "UnityEditor.ShaderGraph.Internal.BooleanShaderProperty" => ShaderGraphPropertyKind.Boolean,
            "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty" => ShaderGraphPropertyKind.Texture2D,
            "UnityEditor.ShaderGraph.Internal.Texture2DArrayShaderProperty" => ShaderGraphPropertyKind.Texture2DArray,
            "UnityEditor.ShaderGraph.Internal.Texture3DShaderProperty" => ShaderGraphPropertyKind.Texture3D,
            "UnityEditor.ShaderGraph.Internal.CubemapShaderProperty" => ShaderGraphPropertyKind.Cubemap,
            "UnityEditor.ShaderGraph.Internal.GradientShaderProperty" => ShaderGraphPropertyKind.Gradient,
            "UnityEditor.ShaderGraph.Internal.Matrix2ShaderProperty" => ShaderGraphPropertyKind.Matrix2,
            "UnityEditor.ShaderGraph.Internal.Matrix3ShaderProperty" => ShaderGraphPropertyKind.Matrix3,
            "UnityEditor.ShaderGraph.Internal.Matrix4ShaderProperty" => ShaderGraphPropertyKind.Matrix4,
            "UnityEditor.ShaderGraph.Internal.SamplerStateShaderProperty" => ShaderGraphPropertyKind.SamplerState,
            "UnityEditor.ShaderGraph.Internal.VirtualTextureShaderProperty" => ShaderGraphPropertyKind.VirtualTexture,
            _ => throw new NotSupportedException($"Shader Graph property type '{document.Type}' is not implemented.")
        };
        JsonElement root = document.Root;
        string name = ShaderGraphJson.ReadRequiredString(root, "m_Name", document.ObjectId);
        string defaultReference = ShaderGraphJson.ReadRequiredString(root, "m_DefaultReferenceName", document.ObjectId);
        string overrideReference = ShaderGraphJson.ReadOptionalString(root, "m_OverrideReferenceName");
        string referenceName = string.IsNullOrWhiteSpace(overrideReference) ? defaultReference : overrideReference;
        JsonElement value = ShaderGraphJson.ReadRequiredProperty(root, "m_Value", document.ObjectId).Clone();
        int floatType = kind == ShaderGraphPropertyKind.Vector1
            ? ShaderGraphJson.ReadRequiredInt32(root, "m_FloatType", document.ObjectId)
            : 0;
        double rangeMinimum = 0;
        double rangeMaximum = 1;
        if (kind == ShaderGraphPropertyKind.Vector1 && root.TryGetProperty("m_RangeValues", out JsonElement range))
        {
            rangeMinimum = ShaderGraphJson.ReadRequiredFiniteDouble(range, "x", document.ObjectId + ".m_RangeValues");
            rangeMaximum = ShaderGraphJson.ReadRequiredFiniteDouble(range, "y", document.ObjectId + ".m_RangeValues");
        }
        return new ShaderGraphProperty(
            document.ObjectId,
            name,
            referenceName,
            kind,
            ShaderGraphJson.ReadOptionalBoolean(root, "m_GeneratePropertyBlock", true),
            ShaderGraphJson.ReadOptionalBoolean(root, "m_Hidden", false),
            ShaderGraphJson.ReadOptionalInt32(root, "m_Precision", 0),
            value,
            floatType,
            rangeMinimum,
            rangeMaximum,
            ShaderGraphJson.ReadOptionalBoolean(root, "overrideHLSLDeclaration", false),
            ShaderGraphJson.ReadOptionalInt32(root, "hlslDeclarationOverride", 0));
    }
}

internal enum ShaderGraphPropertyKind
{
    Vector1,
    Vector2,
    Vector3,
    Vector4,
    Color,
    Boolean,
    Texture2D,
    Texture2DArray,
    Texture3D,
    Cubemap,
    Gradient,
    Matrix2,
    Matrix3,
    Matrix4,
    SamplerState,
    VirtualTexture
}

internal sealed class ShaderGraphKeyword
{
    private readonly ReadOnlyCollection<ShaderGraphKeywordEntry> _entries;

    private ShaderGraphKeyword(
        string objectId,
        string name,
        string referenceName,
        int keywordType,
        int definition,
        int scope,
        int stages,
        int value,
        List<ShaderGraphKeywordEntry> entries)
    {
        ObjectId = objectId;
        Name = name;
        ReferenceName = referenceName;
        KeywordType = keywordType;
        Definition = definition;
        Scope = scope;
        Stages = stages;
        Value = value;
        _entries = entries.AsReadOnly();
    }

    internal string ObjectId { get; }

    internal string Name { get; }

    internal string ReferenceName { get; }

    internal int KeywordType { get; }

    internal int Definition { get; }

    internal int Scope { get; }

    internal int Stages { get; }

    internal int Value { get; }

    internal IReadOnlyList<ShaderGraphKeywordEntry> Entries => _entries;

    internal static ShaderGraphKeyword Parse(MultiJsonDocument document)
    {
        if (!string.Equals(document.Type, "UnityEditor.ShaderGraph.ShaderKeyword", StringComparison.Ordinal))
            throw new InvalidDataException($"GraphData.m_Keywords contains '{document.Type}'.");
        JsonElement root = document.Root;
        var entries = ShaderGraphJson.ReadEntries(root, document.ObjectId, requireReferenceName: true)
            .Select(entry => new ShaderGraphKeywordEntry(entry.Id, entry.DisplayName, entry.ReferenceName!))
            .ToList();
        if (entries.Select(entry => entry.Id).Distinct().Count() != entries.Count)
            throw new InvalidDataException($"Shader keyword '{document.ObjectId}' contains duplicate entry ids.");
        int keywordType = ShaderGraphJson.ReadRequiredInt32(root, "m_KeywordType", document.ObjectId);
        int definition = ShaderGraphJson.ReadRequiredInt32(root, "m_KeywordDefinition", document.ObjectId);
        int scope = ShaderGraphJson.ReadRequiredInt32(root, "m_KeywordScope", document.ObjectId);
        int stages = ShaderGraphJson.ReadRequiredInt32(root, "m_KeywordStages", document.ObjectId);
        int value = ShaderGraphJson.ReadRequiredInt32(root, "m_Value", document.ObjectId);
        if (keywordType is < 0 or > 1)
            throw new InvalidDataException($"Shader keyword '{document.ObjectId}' has invalid type {keywordType}.");
        if (definition is < 0 or > 2)
            throw new InvalidDataException($"Shader keyword '{document.ObjectId}' has invalid definition {definition}.");
        if (scope is < 0 or > 1)
            throw new InvalidDataException($"Shader keyword '{document.ObjectId}' has invalid scope {scope}.");
        if (stages < 0 || (stages & ~63) != 0)
            throw new InvalidDataException($"Shader keyword '{document.ObjectId}' has invalid stage mask {stages}.");
        if (keywordType == 0 && value is < 0 or > 1)
            throw new InvalidDataException($"Boolean shader keyword '{document.ObjectId}' has invalid value {value}.");
        if (keywordType == 1 && (entries.Count == 0 || value < 0 || value >= entries.Count))
            throw new InvalidDataException($"Enum shader keyword '{document.ObjectId}' has invalid value {value}.");
        return new ShaderGraphKeyword(
            document.ObjectId,
            ShaderGraphJson.ReadRequiredString(root, "m_Name", document.ObjectId),
            ShaderGraphJson.ReadEffectiveReferenceName(root, document.ObjectId),
            keywordType,
            definition,
            scope,
            stages,
            value,
            entries);
    }
}

internal sealed class ShaderGraphKeywordEntry
{
    internal ShaderGraphKeywordEntry(int id, string displayName, string referenceName)
    {
        Id = id;
        DisplayName = displayName;
        ReferenceName = referenceName;
    }

    internal int Id { get; }

    internal string DisplayName { get; }

    internal string ReferenceName { get; }
}

internal sealed class ShaderGraphDropdown
{
    private readonly ReadOnlyCollection<ShaderGraphDropdownEntry> _entries;

    private ShaderGraphDropdown(
        string objectId,
        string name,
        string referenceName,
        int value,
        List<ShaderGraphDropdownEntry> entries)
    {
        ObjectId = objectId;
        Name = name;
        ReferenceName = referenceName;
        Value = value;
        _entries = entries.AsReadOnly();
    }

    internal string ObjectId { get; }

    internal string Name { get; }

    internal string ReferenceName { get; }

    internal int Value { get; }

    internal IReadOnlyList<ShaderGraphDropdownEntry> Entries => _entries;

    internal static ShaderGraphDropdown Parse(MultiJsonDocument document)
    {
        if (!string.Equals(document.Type, "UnityEditor.ShaderGraph.ShaderDropdown", StringComparison.Ordinal))
            throw new InvalidDataException($"GraphData.m_Dropdowns contains '{document.Type}'.");
        JsonElement root = document.Root;
        var entries = ShaderGraphJson.ReadEntries(root, document.ObjectId, requireReferenceName: false)
            .Select(entry => new ShaderGraphDropdownEntry(entry.Id, entry.DisplayName))
            .ToList();
        if (entries.Select(entry => entry.Id).Distinct().Count() != entries.Count)
            throw new InvalidDataException($"Shader dropdown '{document.ObjectId}' contains duplicate entry ids.");
        return new ShaderGraphDropdown(
            document.ObjectId,
            ShaderGraphJson.ReadRequiredString(root, "m_Name", document.ObjectId),
            ShaderGraphJson.ReadEffectiveReferenceName(root, document.ObjectId),
            ShaderGraphJson.ReadRequiredInt32(root, "m_Value", document.ObjectId),
            entries);
    }
}

internal sealed class ShaderGraphDropdownEntry
{
    internal ShaderGraphDropdownEntry(int id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    internal int Id { get; }

    internal string DisplayName { get; }
}

internal sealed class ShaderGraphCategory
{
    private readonly ReadOnlyCollection<string> _childObjectIds;

    private ShaderGraphCategory(string objectId, string name, List<string> childObjectIds)
    {
        ObjectId = objectId;
        Name = name;
        _childObjectIds = childObjectIds.AsReadOnly();
    }

    internal string ObjectId { get; }

    internal string Name { get; }

    internal IReadOnlyList<string> ChildObjectIds => _childObjectIds;

    internal static ShaderGraphCategory Parse(MultiJsonDocument document, ISet<string> knownInputs)
    {
        if (!string.Equals(document.Type, "UnityEditor.ShaderGraph.CategoryData", StringComparison.Ordinal))
            throw new InvalidDataException($"GraphData.m_CategoryData contains '{document.Type}'.");
        JsonElement root = document.Root;
        if (!root.TryGetProperty("m_ChildObjectList", out JsonElement children) || children.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Shader category '{document.ObjectId}' requires m_ChildObjectList array.");
        var childIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement child in children.EnumerateArray())
        {
            string objectId = ShaderGraphJson.ReadObjectReference(child, document.ObjectId + ".m_ChildObjectList");
            if (!seen.Add(objectId))
                throw new InvalidDataException($"Shader category '{document.ObjectId}' contains duplicate child '{objectId}'.");
            if (!knownInputs.Contains(objectId))
                throw new InvalidDataException($"Shader category '{document.ObjectId}' references unknown input '{objectId}'.");
            childIds.Add(objectId);
        }
        return new ShaderGraphCategory(
            document.ObjectId,
            ShaderGraphJson.ReadOptionalString(root, "m_Name"),
            childIds);
    }
}

internal static class ShaderGraphJson
{
    internal static string ReadObjectReference(JsonElement reference, string owner)
    {
        if (reference.ValueKind != JsonValueKind.Object ||
            !reference.TryGetProperty("m_Id", out JsonElement id) ||
            id.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(id.GetString()))
        {
            throw new InvalidDataException(owner + " requires a non-empty string m_Id.");
        }
        return id.GetString()!;
    }

    internal static JsonElement ReadRequiredProperty(JsonElement root, string propertyName, string owner)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
            throw new InvalidDataException($"Shader Graph object '{owner}' requires {propertyName}.");
        return value;
    }

    internal static string ReadRequiredString(JsonElement root, string propertyName, string owner)
    {
        JsonElement value = ReadRequiredProperty(root, propertyName, owner);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"Shader Graph object '{owner}' requires non-empty string {propertyName}.");
        return value.GetString()!;
    }

    internal static string ReadOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value)) return string.Empty;
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Shader Graph property {propertyName} must be a string.");
        return value.GetString() ?? string.Empty;
    }

    internal static string ReadEffectiveReferenceName(JsonElement root, string owner)
    {
        string defaultName = ReadRequiredString(root, "m_DefaultReferenceName", owner);
        string overrideName = ReadOptionalString(root, "m_OverrideReferenceName");
        return string.IsNullOrWhiteSpace(overrideName) ? defaultName : overrideName;
    }

    internal static int ReadRequiredInt32(JsonElement root, string propertyName, string owner)
    {
        JsonElement value = ReadRequiredProperty(root, propertyName, owner);
        if (!value.TryGetInt32(out int result))
            throw new InvalidDataException($"Shader Graph object '{owner}' requires integer {propertyName}.");
        return result;
    }

    internal static int ReadOptionalInt32(JsonElement root, string propertyName, int fallback)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value)) return fallback;
        if (!value.TryGetInt32(out int result))
            throw new InvalidDataException($"Shader Graph property {propertyName} must be an integer.");
        return result;
    }

    internal static bool ReadOptionalBoolean(JsonElement root, string propertyName, bool fallback)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value)) return fallback;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException($"Shader Graph property {propertyName} must be a boolean.");
        return value.GetBoolean();
    }

    internal static double ReadRequiredFiniteDouble(JsonElement root, string propertyName, string owner)
    {
        JsonElement value = ReadRequiredProperty(root, propertyName, owner);
        if (!value.TryGetDouble(out double result) || double.IsNaN(result) || double.IsInfinity(result))
            throw new InvalidDataException($"Shader Graph object '{owner}' requires finite number {propertyName}.");
        return result;
    }

    internal static List<ShaderGraphEntryData> ReadEntries(
        JsonElement root,
        string owner,
        bool requireReferenceName)
    {
        JsonElement serialized = ReadRequiredProperty(root, "m_Entries", owner);
        if (serialized.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Shader Graph object '{owner}' requires m_Entries array.");
        var result = new List<ShaderGraphEntryData>();
        foreach (JsonElement entry in serialized.EnumerateArray())
        {
            int id = ReadRequiredInt32(entry, "id", owner + ".m_Entries");
            string displayName = ReadRequiredString(entry, "displayName", owner + ".m_Entries");
            string? referenceName = requireReferenceName
                ? ReadRequiredString(entry, "referenceName", owner + ".m_Entries")
                : null;
            result.Add(new ShaderGraphEntryData(id, displayName, referenceName));
        }
        return result;
    }
}

internal sealed class ShaderGraphEntryData
{
    internal ShaderGraphEntryData(int id, string displayName, string? referenceName)
    {
        Id = id;
        DisplayName = displayName;
        ReferenceName = referenceName;
    }

    internal int Id { get; }

    internal string DisplayName { get; }

    internal string? ReferenceName { get; }
}
