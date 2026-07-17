using System.Globalization;
using System.Text;
using System.Text.Json;
using UnityEditor.ShaderGraph.Model;
using UnityEditor.ShaderGraph.Serialization;

namespace UnityEditor.ShaderGraph.Generation;

internal sealed class ScalarHlslCompiler
{
    private readonly MultiJsonAsset _asset;
    private readonly ShaderGraphTopology _topology;
    private readonly ShaderGraphBlackboard _blackboard;
    private readonly IReadOnlyDictionary<string, ShaderGraphCustomFunction> _customFunctions;
    private readonly Func<string, string>? _includePathResolver;
    private readonly Dictionary<string, string> _variables;
    private readonly Dictionary<InputIdentity, ShaderGraphSlotReference> _connections;
    private readonly HashSet<string> _emitting = new(StringComparer.Ordinal);
    private readonly HashSet<string> _emitted = new(StringComparer.Ordinal);
    private readonly HashSet<string> _usedPropertyIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _registeredFunctions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _registeredIncludes = new(StringComparer.Ordinal);
    private readonly List<string> _includes = new();
    private readonly List<string> _functionDefinitions = new();
    private readonly StringBuilder _body = new();

    private ScalarHlslCompiler(MultiJsonAsset asset, Func<string, string>? includePathResolver)
    {
        _asset = asset;
        _topology = ShaderGraphTopology.Create(asset);
        _blackboard = ShaderGraphBlackboard.Create(asset);
        _customFunctions = ShaderGraphCustomFunctionSet.Create(asset).Functions
            .ToDictionary(function => function.ObjectId, StringComparer.Ordinal);
        _includePathResolver = includePathResolver;
        if (!_topology.TryGetTopologicalOrder(out IReadOnlyList<MultiJsonDocument> order))
            throw new InvalidDataException("Shader Graph contains a cycle and cannot generate HLSL.");
        _variables = order
            .Select((node, index) => new { node.ObjectId, Name = "n" + index.ToString(CultureInfo.InvariantCulture) })
            .ToDictionary(item => item.ObjectId, item => item.Name, StringComparer.Ordinal);
        _connections = new Dictionary<InputIdentity, ShaderGraphSlotReference>();
        foreach (ShaderGraphEdge edge in _topology.Edges)
            _connections.Add(new InputIdentity(edge.Input.NodeObjectId, edge.Input.SlotId), edge.Output);
    }

    internal static string Compile(
        MultiJsonAsset asset,
        string outputNodeObjectId,
        Func<string, string>? includePathResolver = null)
    {
        if (asset is null) throw new ArgumentNullException(nameof(asset));
        if (string.IsNullOrWhiteSpace(outputNodeObjectId))
            throw new ArgumentException("Output node object id is required.", nameof(outputNodeObjectId));

        var compiler = new ScalarHlslCompiler(asset, includePathResolver);
        string output = compiler.EmitNode(outputNodeObjectId);
        return compiler.BuildIncludes() + compiler.BuildFunctionDefinitions() + compiler.BuildPropertyDeclarations() +
               "float AnityShaderGraphEvaluate()\n{\n" + compiler._body + "    return " + output + ";\n}\n";
    }

    private string EmitNode(string objectId)
    {
        if (_emitting.Contains(objectId))
            throw new InvalidDataException($"Shader Graph dependency cycle reaches node '{objectId}'.");
        if (!_topology.NodesById.TryGetValue(objectId, out MultiJsonDocument? node))
            throw new InvalidDataException($"Shader Graph output node '{objectId}' is not in GraphData.m_Nodes.");

        string variable = _variables[objectId];
        if (_emitted.Contains(objectId))
            return variable;

        _emitting.Add(objectId);
        if (node.Type == "UnityEditor.ShaderGraph.CustomFunctionNode")
        {
            EmitCustomFunction(node, variable);
            _emitting.Remove(objectId);
            _emitted.Add(objectId);
            return variable;
        }
        string expression;
        switch (node.Type)
        {
            case "UnityEditor.ShaderGraph.Vector1Node":
                expression = FormatFloat(ReadFiniteNumber(node.Root, "m_Value", node.ObjectId));
                break;
            case "UnityEditor.ShaderGraph.AddNode":
                expression = Binary(node, "+");
                break;
            case "UnityEditor.ShaderGraph.SubtractNode":
                expression = Binary(node, "-");
                break;
            case "UnityEditor.ShaderGraph.MultiplyNode":
                expression = Binary(node, "*");
                break;
            case "UnityEditor.ShaderGraph.DivideNode":
                expression = Binary(node, "/");
                break;
            case "UnityEditor.ShaderGraph.PropertyNode":
                expression = EmitProperty(node);
                break;
            default:
                throw new NotSupportedException($"Scalar HLSL generation does not yet support '{node.Type}'.");
        }
        _emitting.Remove(objectId);
        _body.Append("    float ").Append(variable).Append(" = ").Append(expression).Append(";\n");
        _emitted.Add(objectId);
        return variable;
    }

    private void EmitCustomFunction(MultiJsonDocument node, string variable)
    {
        if (!_customFunctions.TryGetValue(node.ObjectId, out ShaderGraphCustomFunction? function))
            throw new InvalidDataException($"Shader Graph Custom Function '{node.ObjectId}' could not be resolved.");
        ValidateScalarCustomFunction(function);
        RegisterCustomFunction(function);

        string[] inputs = function.Inputs
            .Select(slot => ReadInput(node, slot.Document))
            .ToArray();
        _body.Append("    float ").Append(variable).Append(";\n    ")
            .Append(function.FunctionName).Append("_float(");
        if (inputs.Length > 0)
            _body.Append(string.Join(", ", inputs)).Append(", ");
        _body.Append(variable).Append(");\n");
    }

    private static void ValidateScalarCustomFunction(ShaderGraphCustomFunction function)
    {
        if (!function.IsConfigured)
            throw new InvalidDataException($"Custom Function '{function.ObjectId}' is not configured.");
        if (!IsHlslIdentifier(function.FunctionName))
            throw new InvalidDataException($"Custom Function '{function.ObjectId}' has invalid HLSL function name '{function.FunctionName}'.");
        ShaderGraphCustomFunctionSlot[] outputs = function.Outputs.ToArray();
        if (outputs.Length != 1 || outputs[0].Kind != ShaderGraphCustomSlotKind.Vector1)
            throw new NotSupportedException($"Scalar HLSL generation requires Custom Function '{function.ObjectId}' to have exactly one Vector1 output.");
        if (function.Inputs.Any(slot => slot.Kind != ShaderGraphCustomSlotKind.Vector1))
            throw new NotSupportedException($"Scalar HLSL generation only supports Vector1 inputs on Custom Function '{function.ObjectId}'.");
        foreach (ShaderGraphCustomFunctionSlot slot in function.Slots)
        {
            if (!IsHlslIdentifier(slot.ShaderOutputName))
                throw new InvalidDataException($"Custom Function slot '{slot.Document.ObjectId}' has invalid HLSL name '{slot.ShaderOutputName}'.");
        }
    }

    private void RegisterCustomFunction(ShaderGraphCustomFunction function)
    {
        string hlslName = function.FunctionName + "_float";
        ShaderGraphCustomFunctionSlot[] orderedSlots = function.Inputs.Concat(function.Outputs).ToArray();
        string signature = string.Join(",", orderedSlots.Select(
            slot => slot.Direction.ToString(CultureInfo.InvariantCulture) + ":" + slot.Kind + ":" + slot.ShaderOutputName));
        string registration = function.SourceType + "|" + signature + "|" +
                              (function.SourceType == ShaderGraphHlslSourceType.File ? function.FunctionSource : function.FunctionBody);
        if (_registeredFunctions.TryGetValue(hlslName, out string? existing))
        {
            if (!string.Equals(existing, registration, StringComparison.Ordinal))
                throw new InvalidDataException($"Custom Function '{hlslName}' has conflicting definitions.");
            return;
        }
        _registeredFunctions.Add(hlslName, registration);

        if (function.SourceType == ShaderGraphHlslSourceType.File)
        {
            if (_includePathResolver is null)
                throw new InvalidDataException($"Custom Function '{function.ObjectId}' requires an include-path resolver for GUID '{function.FunctionSource}'.");
            string path = _includePathResolver(function.FunctionSource);
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException($"Custom Function '{function.ObjectId}' source GUID '{function.FunctionSource}' did not resolve to a path.");
            string extension = Path.GetExtension(path);
            if (!string.Equals(extension, ".hlsl", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".cginc", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".cg", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Custom Function '{function.ObjectId}' source '{path}' must be .hlsl, .cginc, or .cg.");
            string normalized = path.Replace('\\', '/');
            if (normalized.IndexOfAny(new[] { '\"', '\r', '\n' }) >= 0)
                throw new InvalidDataException($"Custom Function '{function.ObjectId}' resolved to an invalid include path.");
            if (_registeredIncludes.Add(normalized)) _includes.Add(normalized);
            return;
        }

        string parameters = string.Join(", ", orderedSlots.Select(slot =>
            (slot.Direction == 1 ? "out " : string.Empty) + "float " + slot.ShaderOutputName));
        string body = function.FunctionBody.Replace("\r\n", "\n").Replace('\r', '\n');
        var definition = new StringBuilder()
            .Append("void ").Append(hlslName).Append('(').Append(parameters).Append(")\n{\n");
        foreach (string line in body.Split('\n')) definition.Append("    ").Append(line).Append('\n');
        definition.Append("}\n\n");
        _functionDefinitions.Add(definition.ToString());
    }

    private string BuildIncludes()
    {
        if (_includes.Count == 0) return string.Empty;
        var builder = new StringBuilder();
        foreach (string include in _includes) builder.Append("#include \"").Append(include).Append("\"\n");
        return builder.Append('\n').ToString();
    }

    private string BuildFunctionDefinitions()
        => _functionDefinitions.Count == 0 ? string.Empty : string.Concat(_functionDefinitions);

    private string EmitProperty(MultiJsonDocument node)
    {
        if (!node.Root.TryGetProperty("m_Property", out JsonElement propertyReference))
            throw new InvalidDataException($"Shader Graph PropertyNode '{node.ObjectId}' requires m_Property.");
        string propertyId = ReadObjectReference(propertyReference, node.ObjectId + ".m_Property");
        if (!_blackboard.PropertiesById.TryGetValue(propertyId, out ShaderGraphProperty? property))
            throw new InvalidDataException($"Shader Graph PropertyNode '{node.ObjectId}' references missing property '{propertyId}'.");
        if (property.Kind is not (ShaderGraphPropertyKind.Vector1 or ShaderGraphPropertyKind.Boolean))
            throw new NotSupportedException(
                $"Scalar HLSL generation cannot consume {property.Kind} property '{property.ReferenceName}'.");
        _usedPropertyIds.Add(propertyId);
        return property.EffectiveHlslDeclaration == 3
            ? $"UNITY_ACCESS_HYBRID_INSTANCED_PROP({property.ReferenceName}, float)"
            : property.ReferenceName;
    }

    private string BuildPropertyDeclarations()
    {
        if (_usedPropertyIds.Count == 0) return string.Empty;
        ShaderGraphProperty[] used = _blackboard.Properties
            .Where(property => _usedPropertyIds.Contains(property.ObjectId))
            .ToArray();
        ShaderGraphProperty[] material = used
            .Where(property => property.EffectiveHlslDeclaration is 2 or 3)
            .ToArray();
        ShaderGraphProperty[] hybrid = used
            .Where(property => property.EffectiveHlslDeclaration == 3)
            .ToArray();
        ShaderGraphProperty[] globals = used
            .Where(property => property.EffectiveHlslDeclaration == 1)
            .ToArray();
        var builder = new StringBuilder();
        builder.Append("CBUFFER_START(UnityPerMaterial)\n");
        foreach (ShaderGraphProperty property in material)
            builder.Append("    float ").Append(property.ReferenceName).Append(";\n");
        builder.Append("CBUFFER_END\n\n");

        if (hybrid.Length > 0)
        {
            builder.Append("#if defined(DOTS_INSTANCING_ON)\n")
                .Append("UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)\n");
            foreach (ShaderGraphProperty property in hybrid)
                builder.Append("    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, ").Append(property.ReferenceName).Append(")\n");
            builder.Append("UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)\n")
                .Append("#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)\n")
                .Append("#elif defined(UNITY_INSTANCING_ENABLED)\n")
                .Append("UNITY_INSTANCING_BUFFER_START(SGPerInstanceData)\n");
            foreach (ShaderGraphProperty property in hybrid)
                builder.Append("    UNITY_DEFINE_INSTANCED_PROP(float, ").Append(property.ReferenceName).Append(")\n");
            builder.Append("UNITY_INSTANCING_BUFFER_END(SGPerInstanceData)\n")
                .Append("#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_INSTANCED_PROP(SGPerInstanceData, var)\n")
                .Append("#else\n")
                .Append("#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var\n")
                .Append("#endif\n\n");
        }

        builder.Append("// Object and Global properties\n");
        foreach (ShaderGraphProperty property in globals)
            builder.Append("float ").Append(property.ReferenceName).Append(";\n");
        builder.Append('\n');
        return builder.ToString();
    }

    private string Binary(MultiJsonDocument node, string operation)
    {
        MultiJsonDocument[] inputs = ReadInputSlots(node).Take(2).ToArray();
        if (inputs.Length != 2)
            throw new InvalidDataException($"Shader Graph node '{node.ObjectId}' requires exactly two scalar inputs.");
        return "(" + ReadInput(node, inputs[0]) + " " + operation + " " + ReadInput(node, inputs[1]) + ")";
    }

    private string ReadInput(MultiJsonDocument node, MultiJsonDocument slot)
    {
        int slotId = ReadInt32(slot.Root, "m_Id", slot.ObjectId);
        if (_connections.TryGetValue(new InputIdentity(node.ObjectId, slotId), out ShaderGraphSlotReference? source))
        {
            ValidateCustomFunctionOutputSlot(source);
            return EmitNode(source.NodeObjectId);
        }
        return FormatFloat(ReadFiniteNumber(slot.Root, "m_Value", slot.ObjectId));
    }

    private void ValidateCustomFunctionOutputSlot(ShaderGraphSlotReference source)
    {
        if (!_customFunctions.TryGetValue(source.NodeObjectId, out ShaderGraphCustomFunction? function)) return;
        ShaderGraphCustomFunctionSlot[] outputs = function.Outputs.ToArray();
        if (outputs.Length != 1 || outputs[0].Id != source.SlotId)
            throw new InvalidDataException(
                $"Custom Function '{source.NodeObjectId}' scalar edge references invalid output slot '{source.SlotId}'.");
    }

    private IEnumerable<MultiJsonDocument> ReadInputSlots(MultiJsonDocument node)
    {
        if (!node.Root.TryGetProperty("m_Slots", out JsonElement slots) || slots.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Shader Graph node '{node.ObjectId}' requires an m_Slots array.");
        foreach (JsonElement reference in slots.EnumerateArray())
        {
            string objectId = ReadObjectReference(reference, node.ObjectId);
            if (!_asset.TryResolve(objectId, out MultiJsonDocument? slot) || slot is null)
                throw new InvalidDataException($"Shader Graph node '{node.ObjectId}' references missing slot '{objectId}'.");
            if (ReadInt32(slot.Root, "m_SlotType", slot.ObjectId) == 0) yield return slot;
        }
    }

    private static string ReadObjectReference(JsonElement reference, string owner)
    {
        if (reference.ValueKind != JsonValueKind.Object ||
            !reference.TryGetProperty("m_Id", out JsonElement id) ||
            id.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(id.GetString()))
        {
            throw new InvalidDataException($"Shader Graph node '{owner}' has an invalid slot reference.");
        }
        return id.GetString()!;
    }

    private static int ReadInt32(JsonElement root, string propertyName, string owner)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value) || !value.TryGetInt32(out int result))
            throw new InvalidDataException($"Shader Graph object '{owner}' requires integer {propertyName}.");
        return result;
    }

    private static double ReadFiniteNumber(JsonElement root, string propertyName, string owner)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out double result) ||
            double.IsNaN(result) || double.IsInfinity(result))
        {
            throw new InvalidDataException($"Shader Graph object '{owner}' requires finite scalar {propertyName}.");
        }
        return result;
    }

    private static string FormatFloat(double value)
    {
        string formatted = value.ToString("R", CultureInfo.InvariantCulture).Replace('E', 'e');
        return formatted.IndexOf('.') < 0 && formatted.IndexOf('e') < 0 ? formatted + ".0" : formatted;
    }

    private static bool IsHlslIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || !(value[0] == '_' || char.IsLetter(value[0]))) return false;
        for (int index = 1; index < value.Length; index++)
            if (!(value[index] == '_' || char.IsLetterOrDigit(value[index]))) return false;
        return true;
    }

    private sealed class InputIdentity : IEquatable<InputIdentity>
    {
        internal InputIdentity(string nodeObjectId, int slotId)
        {
            NodeObjectId = nodeObjectId;
            SlotId = slotId;
        }

        private string NodeObjectId { get; }

        private int SlotId { get; }

        public bool Equals(InputIdentity? other)
            => other is not null && SlotId == other.SlotId &&
               string.Equals(NodeObjectId, other.NodeObjectId, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as InputIdentity);

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(NodeObjectId) * 397) ^ SlotId;
            }
        }
    }
}
