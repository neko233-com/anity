using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UnityEditor.ShaderGraph.Serialization;

internal static class LegacyShaderGraphUpgrader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const string ZeroGuid = "00000000000000000000000000000000";

    internal static MultiJsonAsset Upgrade(MultiJsonAsset legacyAsset)
    {
        if (legacyAsset is null) throw new ArgumentNullException(nameof(legacyAsset));
        if (legacyAsset.Format != ShaderGraphSerializationFormat.LegacySingleJson)
            throw new ArgumentException("Only legacy single-JSON Shader Graph assets can be upgraded.", nameof(legacyAsset));

        JsonObject legacy = JsonNode.Parse(legacyAsset.SourceText)?.AsObject()
            ?? throw new InvalidDataException("Legacy Shader Graph root must be a JSON object.");
        var documents = new List<JsonObject>();
        var propertyReferences = new JsonArray();
        var nodeReferences = new JsonArray();
        var groupReferences = new JsonArray();
        var edges = new JsonArray();

        foreach (JsonNode? wrappedProperty in ReadArray(legacy, "m_SerializedProperties"))
        {
            JsonObject wrapper = RequireObject(wrappedProperty, "m_SerializedProperties");
            string type = NormalizePropertyType(ReadWrappedType(wrapper));
            JsonObject property = ReadWrappedPayload(wrapper);
            string objectId = ReadLegacyPropertyId(property);
            PrepareDocument(property, type, objectId);
            EnsurePropertyCompatibilityFields(property);
            documents.Add(property);
            propertyReferences.Add(Reference(objectId));
        }

        foreach (JsonNode? serializedGroup in ReadArray(legacy, "m_Groups", required: false))
        {
            JsonObject oldGroup = RequireObject(serializedGroup, "m_Groups");
            string objectId = NormalizeGuid(ReadRequiredString(oldGroup, "m_GuidSerialized", "group"));
            var group = new JsonObject
            {
                ["m_SGVersion"] = 0,
                ["m_Type"] = "UnityEditor.ShaderGraph.GroupData",
                ["m_ObjectId"] = objectId,
                ["m_Title"] = ReadOptionalString(oldGroup, "m_Title"),
                ["m_Position"] = oldGroup["m_Position"]?.DeepClone() ?? new JsonObject { ["x"] = 0.0, ["y"] = 0.0 }
            };
            documents.Add(group);
            groupReferences.Add(Reference(objectId));
        }

        foreach (JsonNode? wrappedNode in ReadArray(legacy, "m_SerializableNodes"))
        {
            JsonObject wrapper = RequireObject(wrappedNode, "m_SerializableNodes");
            string type = ReadWrappedType(wrapper);
            JsonObject node = ReadWrappedPayload(wrapper);
            string objectId = NormalizeGuid(ReadRequiredString(node, "m_GuidSerialized", type));
            node.Remove("m_GuidSerialized");
            string groupGuid = NormalizeGuid(ReadOptionalString(node, "m_GroupGuidSerialized"));
            node.Remove("m_GroupGuidSerialized");
            if (groupGuid != ZeroGuid) node["m_Group"] = Reference(groupGuid);

            var slotReferences = new JsonArray();
            foreach (JsonNode? wrappedSlot in ReadArray(node, "m_SerializableSlots", required: false))
            {
                JsonObject slotWrapper = RequireObject(wrappedSlot, objectId + ".m_SerializableSlots");
                string slotType = ReadWrappedType(slotWrapper);
                JsonObject slot = ReadWrappedPayload(slotWrapper);
                int slotId = ReadRequiredInt32(slot, "m_Id", slotType);
                string slotObjectId = StableObjectId("slot:" + objectId + ":" + slotId.ToString(CultureInfo.InvariantCulture));
                PrepareDocument(slot, slotType, slotObjectId);
                documents.Add(slot);
                slotReferences.Add(Reference(slotObjectId));
            }
            node.Remove("m_SerializableSlots");
            node["m_Slots"] = slotReferences;

            string propertyGuid = NormalizeGuid(ReadOptionalString(node, "m_PropertyGuidSerialized"));
            node.Remove("m_PropertyGuidSerialized");
            if (propertyGuid != ZeroGuid) node["m_Property"] = Reference(propertyGuid);
            PrepareDocument(node, type, objectId);
            documents.Add(node);
            nodeReferences.Add(Reference(objectId));
        }

        foreach (JsonNode? wrappedEdge in ReadArray(legacy, "m_SerializableEdges"))
        {
            JsonObject wrapper = RequireObject(wrappedEdge, "m_SerializableEdges");
            JsonObject edge = ReadWrappedPayload(wrapper);
            edges.Add(new JsonObject
            {
                ["m_OutputSlot"] = UpgradeSlotReference(edge, "m_OutputSlot"),
                ["m_InputSlot"] = UpgradeSlotReference(edge, "m_InputSlot")
            });
        }

        string graphId = StableObjectId("graph:" + legacyAsset.SourceText);
        string categoryId = StableObjectId("category:" + graphId);
        var categoryChildren = new JsonArray();
        foreach (JsonNode? reference in propertyReferences)
            categoryChildren.Add(reference?.DeepClone());
        var category = new JsonObject
        {
            ["m_SGVersion"] = 0,
            ["m_Type"] = "UnityEditor.ShaderGraph.CategoryData",
            ["m_ObjectId"] = categoryId,
            ["m_Name"] = string.Empty,
            ["m_ChildObjectList"] = categoryChildren
        };
        documents.Add(category);

        var graph = new JsonObject
        {
            ["m_SGVersion"] = 3,
            ["m_Type"] = "UnityEditor.ShaderGraph.GraphData",
            ["m_ObjectId"] = graphId,
            ["m_Properties"] = propertyReferences,
            ["m_Keywords"] = new JsonArray(),
            ["m_Dropdowns"] = new JsonArray(),
            ["m_CategoryData"] = new JsonArray(Reference(categoryId)),
            ["m_Nodes"] = nodeReferences,
            ["m_Edges"] = edges,
            ["m_Groups"] = groupReferences,
            ["m_StickyNotes"] = new JsonArray(),
            ["m_Path"] = ReadOptionalString(legacy, "m_Path")
        };

        var output = new StringBuilder();
        output.Append(graph.ToJsonString(JsonOptions));
        foreach (JsonObject document in documents)
            output.Append("\n\n").Append(document.ToJsonString(JsonOptions));
        return MultiJsonAsset.Parse(output.ToString());
    }

    private static JsonArray ReadArray(JsonObject owner, string propertyName, bool required = true)
    {
        if (!owner.TryGetPropertyValue(propertyName, out JsonNode? value) || value is null)
        {
            if (!required) return new JsonArray();
            throw new InvalidDataException("Legacy Shader Graph requires " + propertyName + ".");
        }
        if (value is not JsonArray array)
            throw new InvalidDataException("Legacy Shader Graph " + propertyName + " must be an array.");
        return array;
    }

    private static JsonObject RequireObject(JsonNode? value, string location)
        => value as JsonObject
           ?? throw new InvalidDataException("Legacy Shader Graph " + location + " entry must be an object.");

    private static string ReadWrappedType(JsonObject wrapper)
    {
        JsonObject typeInfo = RequireObject(wrapper["typeInfo"], "typeInfo");
        return ReadRequiredString(typeInfo, "fullName", "typeInfo");
    }

    private static JsonObject ReadWrappedPayload(JsonObject wrapper)
    {
        string payload = ReadRequiredString(wrapper, "JSONnodeData", "serialized wrapper");
        return JsonNode.Parse(payload)?.AsObject()
               ?? throw new InvalidDataException("Legacy Shader Graph JSONnodeData must contain an object.");
    }

    private static string ReadLegacyPropertyId(JsonObject property)
    {
        JsonObject guid = RequireObject(property["m_Guid"], "property.m_Guid");
        return NormalizeGuid(ReadRequiredString(guid, "m_GuidSerialized", "property.m_Guid"));
    }

    private static void PrepareDocument(JsonObject document, string type, string objectId)
    {
        document["m_SGVersion"] = 0;
        document["m_Type"] = type;
        document["m_ObjectId"] = objectId;
    }

    private static void EnsurePropertyCompatibilityFields(JsonObject property)
    {
        string name = ReadRequiredString(property, "m_Name", "property");
        string defaultReference = ReadOptionalString(property, "m_DefaultReferenceName");
        if (string.IsNullOrWhiteSpace(defaultReference))
            property["m_DefaultReferenceName"] = GenerateReferenceName(name);
        if (!property.ContainsKey("m_OverrideReferenceName")) property["m_OverrideReferenceName"] = string.Empty;
        if (!property.ContainsKey("m_GeneratePropertyBlock")) property["m_GeneratePropertyBlock"] = true;
        if (!property.ContainsKey("m_Hidden")) property["m_Hidden"] = false;
        if (!property.ContainsKey("m_Precision")) property["m_Precision"] = 0;
    }

    private static JsonObject UpgradeSlotReference(JsonObject edge, string propertyName)
    {
        JsonObject old = RequireObject(edge[propertyName], "edge." + propertyName);
        string nodeId = NormalizeGuid(ReadRequiredString(old, "m_NodeGUIDSerialized", "edge." + propertyName));
        int slotId = ReadRequiredInt32(old, "m_SlotId", "edge." + propertyName);
        return new JsonObject
        {
            ["m_Node"] = Reference(nodeId),
            ["m_SlotId"] = slotId
        };
    }

    private static JsonObject Reference(string objectId) => new() { ["m_Id"] = objectId };

    private static string NormalizePropertyType(string type)
    {
        const string prefix = "UnityEditor.ShaderGraph.";
        if (type.StartsWith(prefix, StringComparison.Ordinal) &&
            type.EndsWith("ShaderProperty", StringComparison.Ordinal) &&
            !type.StartsWith(prefix + "Internal.", StringComparison.Ordinal))
        {
            return prefix + "Internal." + type.Substring(prefix.Length);
        }
        return type;
    }

    private static string NormalizeGuid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ZeroGuid;
        string normalized = value.Replace("-", string.Empty).ToLowerInvariant();
        if (normalized.Length != 32 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("Legacy Shader Graph contains invalid GUID '" + value + "'.");
        return normalized;
    }

    private static string StableObjectId(string value)
    {
        byte[] hash;
        using (SHA256 sha256 = SHA256.Create())
            hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
        var output = new StringBuilder(32);
        for (int index = 0; index < 16; index++) output.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
        return output.ToString();
    }

    private static string GenerateReferenceName(string displayName)
    {
        var output = new StringBuilder("_");
        bool previousUnderscore = false;
        foreach (char character in displayName)
        {
            bool valid = char.IsLetterOrDigit(character) || character == '_';
            char next = valid ? character : '_';
            if (next == '_' && previousUnderscore) continue;
            output.Append(next);
            previousUnderscore = next == '_';
        }
        return output.Length == 1 ? "_Property" : output.ToString();
    }

    private static string ReadRequiredString(JsonObject owner, string propertyName, string location)
    {
        if (owner[propertyName] is not JsonValue value || !value.TryGetValue(out string? result) || string.IsNullOrWhiteSpace(result))
            throw new InvalidDataException("Legacy Shader Graph " + location + " requires non-empty " + propertyName + ".");
        return result;
    }

    private static string ReadOptionalString(JsonObject owner, string propertyName)
    {
        if (!owner.TryGetPropertyValue(propertyName, out JsonNode? node) || node is null) return string.Empty;
        if (node is not JsonValue value || !value.TryGetValue(out string? result))
            throw new InvalidDataException("Legacy Shader Graph " + propertyName + " must be a string.");
        return result ?? string.Empty;
    }

    private static int ReadRequiredInt32(JsonObject owner, string propertyName, string location)
    {
        if (owner[propertyName] is not JsonValue value || !value.TryGetValue(out int result))
            throw new InvalidDataException("Legacy Shader Graph " + location + " requires integer " + propertyName + ".");
        return result;
    }
}
