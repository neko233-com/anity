using System.Collections.ObjectModel;
using System.Text.Json;
using UnityEditor.ShaderGraph.Serialization;

namespace UnityEditor.ShaderGraph.Model;

internal enum ShaderGraphHlslSourceType
{
    File = 0,
    String = 1
}

internal enum ShaderGraphCustomSlotKind
{
    Vector1,
    Vector2,
    Vector3,
    Vector4,
    Boolean,
    Texture2DInput
}

internal sealed class ShaderGraphCustomFunctionSet
{
    private readonly ReadOnlyCollection<ShaderGraphCustomFunction> _functions;

    private ShaderGraphCustomFunctionSet(List<ShaderGraphCustomFunction> functions)
    {
        _functions = functions.AsReadOnly();
    }

    internal IReadOnlyList<ShaderGraphCustomFunction> Functions => _functions;

    internal static ShaderGraphCustomFunctionSet Create(MultiJsonAsset asset)
    {
        if (asset is null) throw new ArgumentNullException(nameof(asset));
        if (asset.Format != ShaderGraphSerializationFormat.MultiJson)
            throw new NotSupportedException("Legacy Shader Graph custom functions must be upgraded first.");
        return new ShaderGraphCustomFunctionSet(asset.Documents
            .Where(document => string.Equals(
                document.Type,
                "UnityEditor.ShaderGraph.CustomFunctionNode",
                StringComparison.Ordinal))
            .Select(document => ShaderGraphCustomFunction.Parse(asset, document))
            .ToList());
    }
}

internal sealed class ShaderGraphCustomFunction
{
    private readonly ReadOnlyCollection<ShaderGraphCustomFunctionSlot> _slots;

    private ShaderGraphCustomFunction(
        MultiJsonDocument document,
        ShaderGraphHlslSourceType sourceType,
        string functionName,
        string functionSource,
        string functionBody,
        List<ShaderGraphCustomFunctionSlot> slots)
    {
        Document = document;
        SourceType = sourceType;
        FunctionName = functionName;
        FunctionSource = functionSource;
        FunctionBody = functionBody;
        _slots = slots.AsReadOnly();
    }

    internal MultiJsonDocument Document { get; }

    internal string ObjectId => Document.ObjectId;

    internal ShaderGraphHlslSourceType SourceType { get; }

    internal string FunctionName { get; }

    internal string HlslFunctionName => FunctionName + "_$precision";

    internal string FunctionSource { get; }

    internal string FunctionBody { get; }

    internal IReadOnlyList<ShaderGraphCustomFunctionSlot> Slots => _slots;

    internal IReadOnlyList<ShaderGraphCustomFunctionSlot> Inputs => _slots.Where(slot => slot.Direction == 0).ToArray();

    internal IReadOnlyList<ShaderGraphCustomFunctionSlot> Outputs => _slots.Where(slot => slot.Direction == 1).ToArray();

    internal bool IsConfigured
        => !string.Equals(FunctionName, "Enter function name here...", StringComparison.Ordinal) &&
           (SourceType == ShaderGraphHlslSourceType.File
               ? !string.IsNullOrWhiteSpace(FunctionSource) &&
                 !string.Equals(FunctionSource, "Enter function source file path here...", StringComparison.Ordinal)
               : !string.IsNullOrWhiteSpace(FunctionBody) &&
                 !string.Equals(FunctionBody, "Enter function body here...", StringComparison.Ordinal));

    internal static ShaderGraphCustomFunction Parse(MultiJsonAsset asset, MultiJsonDocument document)
    {
        JsonElement root = document.Root;
        int sourceValue = ShaderGraphJson.ReadRequiredInt32(root, "m_SourceType", document.ObjectId);
        if (!Enum.IsDefined(typeof(ShaderGraphHlslSourceType), sourceValue))
            throw new InvalidDataException($"Custom Function '{document.ObjectId}' has invalid source type '{sourceValue}'.");
        string functionName = ShaderGraphJson.ReadRequiredString(root, "m_FunctionName", document.ObjectId);
        string functionSource = ShaderGraphJson.ReadOptionalString(root, "m_FunctionSource");
        string functionBody = ShaderGraphJson.ReadOptionalString(root, "m_FunctionBody");
        JsonElement slotReferences = ShaderGraphJson.ReadRequiredProperty(root, "m_Slots", document.ObjectId);
        if (slotReferences.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Custom Function '{document.ObjectId}' requires m_Slots array.");
        var slots = new List<ShaderGraphCustomFunctionSlot>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement reference in slotReferences.EnumerateArray())
        {
            string slotId = ShaderGraphJson.ReadObjectReference(reference, document.ObjectId + ".m_Slots");
            if (!seen.Add(slotId))
                throw new InvalidDataException($"Custom Function '{document.ObjectId}' contains duplicate slot '{slotId}'.");
            if (!asset.TryResolve(slotId, out MultiJsonDocument? slotDocument) || slotDocument is null)
                throw new InvalidDataException($"Custom Function '{document.ObjectId}' references missing slot '{slotId}'.");
            slots.Add(ShaderGraphCustomFunctionSlot.Parse(slotDocument));
        }
        return new ShaderGraphCustomFunction(
            document,
            (ShaderGraphHlslSourceType)sourceValue,
            functionName,
            functionSource,
            functionBody,
            slots);
    }
}

internal sealed class ShaderGraphCustomFunctionSlot
{
    private ShaderGraphCustomFunctionSlot(
        MultiJsonDocument document,
        ShaderGraphCustomSlotKind kind,
        int id,
        int direction,
        string displayName,
        string shaderOutputName)
    {
        Document = document;
        Kind = kind;
        Id = id;
        Direction = direction;
        DisplayName = displayName;
        ShaderOutputName = shaderOutputName;
    }

    internal MultiJsonDocument Document { get; }

    internal ShaderGraphCustomSlotKind Kind { get; }

    internal int Id { get; }

    internal int Direction { get; }

    internal string DisplayName { get; }

    internal string ShaderOutputName { get; }

    internal static ShaderGraphCustomFunctionSlot Parse(MultiJsonDocument document)
    {
        ShaderGraphCustomSlotKind kind = document.Type switch
        {
            "UnityEditor.ShaderGraph.Vector1MaterialSlot" => ShaderGraphCustomSlotKind.Vector1,
            "UnityEditor.ShaderGraph.Vector2MaterialSlot" => ShaderGraphCustomSlotKind.Vector2,
            "UnityEditor.ShaderGraph.Vector3MaterialSlot" => ShaderGraphCustomSlotKind.Vector3,
            "UnityEditor.ShaderGraph.Vector4MaterialSlot" => ShaderGraphCustomSlotKind.Vector4,
            "UnityEditor.ShaderGraph.BooleanMaterialSlot" => ShaderGraphCustomSlotKind.Boolean,
            "UnityEditor.ShaderGraph.Texture2DInputMaterialSlot" => ShaderGraphCustomSlotKind.Texture2DInput,
            _ => throw new NotSupportedException(
                $"Custom Function slot type '{document.Type}' is not implemented.")
        };
        JsonElement root = document.Root;
        int direction = ShaderGraphJson.ReadRequiredInt32(root, "m_SlotType", document.ObjectId);
        if (direction is not (0 or 1))
            throw new InvalidDataException($"Custom Function slot '{document.ObjectId}' has invalid direction '{direction}'.");
        if (kind == ShaderGraphCustomSlotKind.Texture2DInput && direction != 0)
            throw new InvalidDataException("Texture2DInputMaterialSlot cannot be a Custom Function output.");
        return new ShaderGraphCustomFunctionSlot(
            document,
            kind,
            ShaderGraphJson.ReadRequiredInt32(root, "m_Id", document.ObjectId),
            direction,
            ShaderGraphJson.ReadRequiredString(root, "m_DisplayName", document.ObjectId),
            ShaderGraphJson.ReadRequiredString(root, "m_ShaderOutputName", document.ObjectId));
    }
}
