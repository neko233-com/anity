using System.Collections.ObjectModel;
using System.Text.Json;

namespace UnityEditor.ShaderGraph.Serialization;

/// <summary>
/// Reads Shader Graph 14.x's concatenated JSON object stream without changing its source text.
/// This is deliberately internal until the public Unity package API surface has been reflected.
/// </summary>
internal sealed class MultiJsonAsset
{
    private readonly ReadOnlyCollection<MultiJsonDocument> _documents;
    private readonly ReadOnlyDictionary<string, MultiJsonDocument> _objectsById;

    private MultiJsonAsset(
        string sourceText,
        List<MultiJsonDocument> documents,
        Dictionary<string, MultiJsonDocument> objectsById)
    {
        SourceText = sourceText;
        _documents = documents.AsReadOnly();
        _objectsById = new ReadOnlyDictionary<string, MultiJsonDocument>(objectsById);
        Graph = documents.FirstOrDefault(document =>
                    string.Equals(document.Type, "UnityEditor.ShaderGraph.GraphData", StringComparison.Ordinal) ||
                    string.Equals(document.Type, LegacyGraphType, StringComparison.Ordinal))
                ?? throw new InvalidDataException("Shader Graph stream does not contain UnityEditor.ShaderGraph.GraphData.");
        Format = string.Equals(Graph.Type, LegacyGraphType, StringComparison.Ordinal)
            ? ShaderGraphSerializationFormat.LegacySingleJson
            : ShaderGraphSerializationFormat.MultiJson;
    }

    internal string SourceText { get; }

    internal IReadOnlyList<MultiJsonDocument> Documents => _documents;

    internal IReadOnlyDictionary<string, MultiJsonDocument> ObjectsById => _objectsById;

    internal MultiJsonDocument Graph { get; }

    internal ShaderGraphSerializationFormat Format { get; }

    private const string LegacyGraphType = "UnityEditor.ShaderGraph.LegacyGraphData";
    private const string LegacyGraphObjectId = "__legacy_graph__";

    internal static MultiJsonAsset Parse(string sourceText)
    {
        if (sourceText is null) throw new ArgumentNullException(nameof(sourceText));

        var documents = new List<MultiJsonDocument>();
        var objectsById = new Dictionary<string, MultiJsonDocument>(StringComparer.Ordinal);
        int position = 0;
        bool firstToken = true;

        while (true)
        {
            SkipWhitespaceAndOptionalBom(sourceText, ref position, firstToken);
            firstToken = false;
            if (position == sourceText.Length) break;
            if (sourceText[position] != '{')
                throw new InvalidDataException($"Expected a JSON object at character {position}.");

            int start = position;
            int end = FindObjectEnd(sourceText, start);
            string rawText = sourceText.Substring(start, end - start);

            JsonElement root;
            try
            {
                using JsonDocument parsed = JsonDocument.Parse(rawText);
                root = parsed.RootElement.Clone();
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Invalid Shader Graph JSON object at character {start}.", exception);
            }

            bool isLegacyGraph = documents.Count == 0 && IsLegacyGraph(root);
            string type = isLegacyGraph ? LegacyGraphType : ReadRequiredString(root, "m_Type", start);
            string objectId = isLegacyGraph ? LegacyGraphObjectId : ReadRequiredString(root, "m_ObjectId", start);
            int shaderGraphVersion = ReadOptionalInt32(root, "m_SGVersion");
            var document = new MultiJsonDocument(type, objectId, shaderGraphVersion, rawText, root);
            if (!objectsById.TryAdd(objectId, document))
                throw new InvalidDataException($"Duplicate Shader Graph m_ObjectId '{objectId}'.");

            documents.Add(document);
            position = end;
        }

        if (documents.Count == 0)
            throw new InvalidDataException("Shader Graph stream is empty.");

        return new MultiJsonAsset(sourceText, documents, objectsById);
    }

    internal bool TryResolve(string objectId, out MultiJsonDocument? document)
        => _objectsById.TryGetValue(objectId, out document);

    internal IReadOnlyList<string> GetUnresolvedObjectIds()
    {
        var references = new HashSet<string>(StringComparer.Ordinal);
        foreach (MultiJsonDocument document in _documents)
            CollectObjectReferences(document.Root, references);
        references.ExceptWith(_objectsById.Keys);
        return references.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static void SkipWhitespaceAndOptionalBom(string text, ref int position, bool allowBom)
    {
        if (allowBom && position < text.Length && text[position] == '\uFEFF') position++;
        while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
    }

    private static int FindObjectEnd(string text, int start)
    {
        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int index = start; index < text.Length; index++)
        {
            char current = text[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (current == '"')
            {
                inString = true;
            }
            else if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;
                if (depth == 0) return index + 1;
                if (depth < 0) break;
            }
        }

        throw new InvalidDataException($"Unterminated Shader Graph JSON object at character {start}.");
    }

    private static string ReadRequiredString(JsonElement root, string propertyName, int offset)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException(
                $"Shader Graph JSON object at character {offset} requires a non-empty string {propertyName}.");
        }
        return value.GetString()!;
    }

    private static int ReadOptionalInt32(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt32(out int result)
            ? result
            : 0;

    private static bool IsLegacyGraph(JsonElement root)
        => root.ValueKind == JsonValueKind.Object &&
           root.TryGetProperty("m_SerializableNodes", out JsonElement nodes) &&
           nodes.ValueKind == JsonValueKind.Array &&
           root.TryGetProperty("m_SerializableEdges", out JsonElement edges) &&
           edges.ValueKind == JsonValueKind.Array;

    private static void CollectObjectReferences(JsonElement element, ISet<string> references)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, "m_Id", StringComparison.Ordinal) &&
                        property.Value.ValueKind == JsonValueKind.String)
                    {
                        string? value = property.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(value)) references.Add(value);
                    }
                    CollectObjectReferences(property.Value, references);
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                    CollectObjectReferences(item, references);
                break;
        }
    }
}

internal enum ShaderGraphSerializationFormat
{
    MultiJson = 0,
    LegacySingleJson = 1
}

internal sealed class MultiJsonDocument
{
    internal MultiJsonDocument(
        string type,
        string objectId,
        int shaderGraphVersion,
        string rawText,
        JsonElement root)
    {
        Type = type;
        ObjectId = objectId;
        ShaderGraphVersion = shaderGraphVersion;
        RawText = rawText;
        Root = root;
    }

    internal string Type { get; }

    internal string ObjectId { get; }

    internal int ShaderGraphVersion { get; }

    internal string RawText { get; }

    internal JsonElement Root { get; }
}
