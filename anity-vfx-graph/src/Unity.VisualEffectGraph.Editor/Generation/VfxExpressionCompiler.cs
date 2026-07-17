using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using UnityEditor.VFX.Model;

namespace UnityEditor.VFX.Generation;

internal enum VfxExpressionValueType
{
    Float,
    Int32,
    UInt32,
    Boolean,
    Float2,
    Float3,
    Float4,
    Position,
    Direction,
    Vector
}

/// <summary>
/// Deterministic typed VFX expression compiler for the first numeric/vector operator subset.
/// Space conversion formulas match VFX Graph 14's VFXExpressionTransform implementations.
/// </summary>
internal sealed class VfxExpressionCompiler
{
    private readonly VfxTypedGraph _graph;
    private readonly Dictionary<long, VisitState> _states = new();
    private readonly List<long> _orderedSlotIds = new();
    private readonly Dictionary<(string Name, VfxAttributeMode Mode), VfxExpressionAttributeDependency> _attributeDependencies = new();
    private readonly IReadOnlyDictionary<long, VfxSerializedAttributeUsage> _attributeUsagesByModel;
    private readonly StringBuilder _source = new();

    private VfxExpressionCompiler(VfxTypedGraph graph)
    {
        _graph = graph;
        _attributeUsagesByModel = VfxAttributeUsageSet.Create(graph).Usages
            .ToDictionary(usage => usage.Model.FileId);
    }

    internal static VfxExpressionCompilation Compile(VfxTypedGraph graph, long outputSlotId)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        var compiler = new VfxExpressionCompiler(graph);
        VfxModel output = compiler.ResolveSlot(outputSlotId);
        if (output.Direction != 1)
            throw new InvalidDataException($"VFX expression compiler output slot '{outputSlotId}' is not an output.");
        return compiler.CompileRoot(output);
    }

    internal static VfxExpressionCompilation CompileInput(VfxTypedGraph graph, long inputSlotId)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        var compiler = new VfxExpressionCompiler(graph);
        VfxModel input = compiler.ResolveSlot(inputSlotId);
        if (input.Direction != 0)
            throw new InvalidDataException($"VFX expression compiler input slot '{inputSlotId}' is not an input.");
        return compiler.CompileRoot(input);
    }

    private VfxExpressionCompilation CompileRoot(VfxModel root)
    {
        CompileSlot(root);
        SlotDescriptor descriptor = Describe(root);
        return new VfxExpressionCompilation(
            _source.ToString(),
            VariableName(root.FileId),
            _orderedSlotIds.AsReadOnly(),
            descriptor.ValueType,
            descriptor.HlslType,
            descriptor.Space,
            _attributeDependencies.Values.ToArray());
    }

    private void CompileSlot(VfxModel slot)
    {
        if (_states.TryGetValue(slot.FileId, out VisitState state))
        {
            if (state == VisitState.Visiting)
                throw new InvalidDataException($"VFX expression slot topology contains a cycle at '{slot.FileId}'.");
            return;
        }
        SlotDescriptor descriptor = Describe(slot);
        _states.Add(slot.FileId, VisitState.Visiting);

        if (slot.Direction == 0)
            CompileInputSlot(slot, descriptor);
        else if (slot.Direction == 1)
            CompileOutputSlot(slot, descriptor);
        else
            throw new InvalidDataException($"VFX expression slot '{slot.FileId}' has invalid direction '{slot.Direction}'.");

        _states[slot.FileId] = VisitState.Done;
        _orderedSlotIds.Add(slot.FileId);
    }

    private void CompileInputSlot(VfxModel slot, SlotDescriptor descriptor)
    {
        if (slot.LinkedSlotIds.Count == 0)
        {
            Emit(slot, descriptor, ReadDefault(slot, descriptor));
            return;
        }
        if (slot.LinkedSlotIds.Count != 1)
            throw new InvalidDataException($"VFX expression input slot '{slot.FileId}' must have zero or one link.");
        VfxModel source = ResolveSlot(slot.LinkedSlotIds[0]);
        if (source.Direction != 1)
            throw new InvalidDataException($"VFX expression input slot '{slot.FileId}' links to a non-output slot.");
        SlotDescriptor sourceDescriptor = Describe(source);
        if (sourceDescriptor.ValueType != descriptor.ValueType)
            throw new InvalidDataException(
                $"VFX expression link '{source.FileId}' -> '{slot.FileId}' changes type from {sourceDescriptor.ValueType} to {descriptor.ValueType}.");
        CompileSlot(source);
        Emit(slot, descriptor, ConvertSpace(VariableName(source.FileId), sourceDescriptor, descriptor));
    }

    private void CompileOutputSlot(VfxModel slot, SlotDescriptor descriptor)
    {
        if (slot.OwnerId == 0)
        {
            Emit(slot, descriptor, ReadDefault(slot, descriptor));
            return;
        }
        if (!_graph.ModelsByFileId.TryGetValue(slot.OwnerId, out VfxModel? owner))
            throw new InvalidDataException($"VFX expression output slot '{slot.FileId}' has unresolved owner '{slot.OwnerId}'.");
        if (!owner.OutputSlotIds.Contains(slot.FileId))
            throw new InvalidDataException($"VFX expression output slot '{slot.FileId}' is absent from owner '{owner.FileId}'.");
        if (owner.ScriptType.TypeName == "VFXAttributeParameter")
        {
            Emit(slot, descriptor, AttributeParameterExpression(owner, descriptor));
            return;
        }
        if (owner.Kind != VfxModelKind.Operator)
        {
            Emit(slot, descriptor, ReadDefault(slot, descriptor));
            return;
        }

        VfxModel[] inputs = owner.InputSlotIds.Select(ResolveSlot).ToArray();
        foreach (VfxModel input in inputs)
        {
            if (input.Direction != 0)
                throw new InvalidDataException($"VFX operator '{owner.FileId}' contains non-input slot '{input.FileId}'.");
            SlotDescriptor inputDescriptor = Describe(input);
            if (inputDescriptor.ValueType != descriptor.ValueType || inputDescriptor.Space != descriptor.Space)
                throw new InvalidDataException(
                    $"VFX operator '{owner.FileId}' input '{input.FileId}' does not match output type/space.");
            CompileSlot(input);
        }

        string expression = owner.ScriptType.TypeName switch
        {
            "Add" => Binary(owner, inputs, "+"),
            "Subtract" => Binary(owner, inputs, "-"),
            "Multiply" => Binary(owner, inputs, "*"),
            "OneMinus" => OneMinus(owner, inputs, descriptor),
            _ => throw new NotSupportedException(
                $"VFX typed expression operator '{owner.ScriptType.TypeName}' is not implemented.")
        };
        Emit(slot, descriptor, expression);
    }

    private string AttributeParameterExpression(VfxModel owner, SlotDescriptor output)
    {
        if (!_attributeUsagesByModel.TryGetValue(owner.FileId, out VfxSerializedAttributeUsage? usage))
            throw new InvalidDataException($"VFX Attribute Parameter '{owner.FileId}' has no typed attribute usage.");
        VfxAttributeMode location = usage.Mode == VfxAttributeMode.ReadSource
            ? VfxAttributeMode.ReadSource
            : VfxAttributeMode.Read;
        string structName = location == VfxAttributeMode.ReadSource ? "sourceAttributes" : "attributes";
        foreach (VfxAttributeDefinition attribute in usage.Attributes)
            _attributeDependencies.TryAdd(
                (attribute.Name, location),
                new VfxExpressionAttributeDependency(attribute, location));

        if (usage.Attributes.Count == 1 && usage.Attributes[0].ComponentCount == output.ComponentCount)
            return structName + "." + usage.Attributes[0].Name;
        if (usage.Attributes.All(attribute => attribute.ComponentCount == 1) &&
            usage.Attributes.Count == output.ComponentCount && output.ComponentCount is >= 1 and <= 3)
        {
            if (output.ComponentCount == 1) return structName + "." + usage.Attributes[0].Name;
            return output.HlslType + "(" + string.Join(", ", usage.Attributes.Select(attribute =>
                structName + "." + attribute.Name)) + ")";
        }
        throw new InvalidDataException(
            $"VFX Attribute Parameter '{owner.FileId}' output type does not match attribute '{usage.SerializedAttributeName}'.");
    }

    private static string Binary(VfxModel owner, IReadOnlyList<VfxModel> inputs, string operation)
    {
        if (inputs.Count != 2)
            throw new InvalidDataException($"VFX operator '{owner.ScriptType.TypeName}' requires exactly two inputs.");
        return "(" + VariableName(inputs[0].FileId) + " " + operation + " " +
               VariableName(inputs[1].FileId) + ")";
    }

    private static string OneMinus(VfxModel owner, IReadOnlyList<VfxModel> inputs, SlotDescriptor descriptor)
    {
        if (inputs.Count != 1)
            throw new InvalidDataException($"VFX operator '{owner.ScriptType.TypeName}' requires exactly one input.");
        if (descriptor.IsSpaceable)
            throw new NotSupportedException("OneMinus does not accept VFX spaceable Position/Direction/Vector values.");
        if (descriptor.ValueType is not (VfxExpressionValueType.Float or
            VfxExpressionValueType.Float2 or VfxExpressionValueType.Float3 or VfxExpressionValueType.Float4))
            throw new NotSupportedException($"OneMinus does not accept VFX {descriptor.ValueType} values.");
        string one = descriptor.ComponentCount == 1
            ? "1.0"
            : descriptor.HlslType + "(" + string.Join(", ", Enumerable.Repeat("1.0", descriptor.ComponentCount)) + ")";
        return "(" + one + " - " + VariableName(inputs[0].FileId) + ")";
    }

    private void Emit(VfxModel slot, SlotDescriptor descriptor, string expression)
        => _source.Append(descriptor.HlslType).Append(' ').Append(VariableName(slot.FileId)).Append(" = ")
            .Append(expression).AppendLine(";");

    private static string ReadDefault(VfxModel slot, SlotDescriptor descriptor)
    {
        VfxSlotValue? value = slot.SlotProperty?.Value;
        if (value is null)
            throw new InvalidDataException($"VFX expression slot '{slot.FileId}' has no concrete value.");
        if (descriptor.ComponentCount == 1)
        {
            return descriptor.ValueType switch
            {
                VfxExpressionValueType.Float when value.Kind == VfxSlotValueKind.Float && value.Scalar is not null
                    => HlslLiteral(value.Scalar.Value),
                VfxExpressionValueType.Int32 when value.Kind == VfxSlotValueKind.Int32 && value.SignedInteger is not null
                    => value.SignedInteger.Value.ToString(CultureInfo.InvariantCulture),
                VfxExpressionValueType.UInt32 when value.Kind == VfxSlotValueKind.UInt32 && value.UnsignedInteger is not null
                    => value.UnsignedInteger.Value.ToString(CultureInfo.InvariantCulture) + "u",
                VfxExpressionValueType.Boolean when value.Kind == VfxSlotValueKind.Boolean && value.Boolean is not null
                    => value.Boolean.Value ? "true" : "false",
                _ => throw new InvalidDataException(
                    $"VFX expression slot '{slot.FileId}' has no concrete {descriptor.ValueType} value.")
            };
        }
        if (value.Components.Count != descriptor.ComponentCount)
            throw new InvalidDataException(
                $"VFX expression slot '{slot.FileId}' requires {descriptor.ComponentCount} components.");
        return descriptor.HlslType + "(" + string.Join(", ", value.Components.Select(HlslLiteral)) + ")";
    }

    private static string ConvertSpace(string expression, SlotDescriptor source, SlotDescriptor destination)
    {
        if (!destination.IsSpaceable || source.Space == destination.Space ||
            source.Space == VfxCoordinateSpace.None || destination.Space == VfxCoordinateSpace.None)
            return expression;
        string matrix = destination.Space switch
        {
            VfxCoordinateSpace.Local => "worldToLocal",
            VfxCoordinateSpace.World => "localToWorld",
            _ => throw new InvalidDataException("VFX expression cannot convert to an unknown coordinate space.")
        };
        return destination.ValueType switch
        {
            VfxExpressionValueType.Position => $"mul({matrix}, float4({expression}, 1.0)).xyz",
            VfxExpressionValueType.Vector => $"mul((float3x3){matrix}, {expression})",
            VfxExpressionValueType.Direction => $"normalize(mul((float3x3){matrix}, {expression}))",
            _ => throw new InvalidDataException("VFX expression requested space conversion for a non-spaceable value.")
        };
    }

    private VfxModel ResolveSlot(long fileId)
    {
        if (!_graph.ModelsByFileId.TryGetValue(fileId, out VfxModel? slot) || slot.Kind != VfxModelKind.Slot)
            throw new InvalidDataException($"VFX expression compiler cannot resolve slot '{fileId}'.");
        return slot;
    }

    private static SlotDescriptor Describe(VfxModel slot)
    {
        VfxCoordinateSpace space = slot.SlotProperty?.Space
            ?? throw new InvalidDataException($"VFX expression slot '{slot.FileId}' has no typed property.");
        SlotDescriptor descriptor = slot.ScriptType.TypeName switch
        {
            "VFXSlotFloat" => new(VfxExpressionValueType.Float, "float", 1, false, space),
            "VFXSlotInt" => new(VfxExpressionValueType.Int32, "int", 1, false, space),
            "VFXSlotUint" => new(VfxExpressionValueType.UInt32, "uint", 1, false, space),
            "VFXSlotBool" => new(VfxExpressionValueType.Boolean, "bool", 1, false, space),
            "VFXSlotFloat2" => new(VfxExpressionValueType.Float2, "float2", 2, false, space),
            "VFXSlotFloat3" => new(VfxExpressionValueType.Float3, "float3", 3, false, space),
            "VFXSlotFloat4" => new(VfxExpressionValueType.Float4, "float4", 4, false, space),
            "VFXSlotPosition" => new(VfxExpressionValueType.Position, "float3", 3, true, space),
            "VFXSlotDirection" => new(VfxExpressionValueType.Direction, "float3", 3, true, space),
            "VFXSlotVector" => new(VfxExpressionValueType.Vector, "float3", 3, true, space),
            _ => throw new NotSupportedException(
                $"VFX expression compiler does not support slot '{slot.FileId}' type '{slot.ScriptType.TypeName}'.")
        };
        if (!descriptor.IsSpaceable && space != VfxCoordinateSpace.None)
            throw new InvalidDataException($"VFX non-spaceable slot '{slot.FileId}' must use coordinate space None.");
        return descriptor;
    }

    private static string HlslLiteral(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new InvalidDataException("VFX expression HLSL does not support NaN or infinity constants.");
        string literal = value.ToString("R", CultureInfo.InvariantCulture).Replace("E", "e");
        if (literal.IndexOf('.') < 0 && literal.IndexOf('e') < 0) literal += ".0";
        return literal;
    }

    private static string VariableName(long fileId)
        => fileId < 0
            ? "vfx_slot_n" + fileId.ToString(CultureInfo.InvariantCulture).Substring(1)
            : "vfx_slot_" + fileId.ToString(CultureInfo.InvariantCulture);

    private readonly struct SlotDescriptor
    {
        internal SlotDescriptor(
            VfxExpressionValueType valueType,
            string hlslType,
            int componentCount,
            bool isSpaceable,
            VfxCoordinateSpace space)
        {
            ValueType = valueType;
            HlslType = hlslType;
            ComponentCount = componentCount;
            IsSpaceable = isSpaceable;
            Space = space;
        }

        internal VfxExpressionValueType ValueType { get; }
        internal string HlslType { get; }
        internal int ComponentCount { get; }
        internal bool IsSpaceable { get; }
        internal VfxCoordinateSpace Space { get; }
    }

    private enum VisitState
    {
        Visiting,
        Done
    }
}

internal sealed class VfxExpressionCompilation
{
    internal VfxExpressionCompilation(
        string hlslSource,
        string resultVariable,
        IReadOnlyList<long> orderedSlotIds,
        VfxExpressionValueType resultType,
        string hlslType,
        VfxCoordinateSpace resultSpace,
        IReadOnlyList<VfxExpressionAttributeDependency> attributeDependencies)
    {
        HlslSource = hlslSource;
        ResultVariable = resultVariable;
        OrderedSlotIds = new ReadOnlyCollection<long>(orderedSlotIds.ToArray());
        ResultType = resultType;
        HlslType = hlslType;
        ResultSpace = resultSpace;
        AttributeDependencies = new ReadOnlyCollection<VfxExpressionAttributeDependency>(attributeDependencies.ToArray());
    }

    internal string HlslSource { get; }
    internal string ResultVariable { get; }
    internal IReadOnlyList<long> OrderedSlotIds { get; }
    internal VfxExpressionValueType ResultType { get; }
    internal string HlslType { get; }
    internal VfxCoordinateSpace ResultSpace { get; }
    internal IReadOnlyList<VfxExpressionAttributeDependency> AttributeDependencies { get; }
}

internal sealed class VfxExpressionAttributeDependency
{
    internal VfxExpressionAttributeDependency(VfxAttributeDefinition attribute, VfxAttributeMode mode)
    {
        Attribute = attribute;
        Mode = mode;
    }

    internal VfxAttributeDefinition Attribute { get; }
    internal VfxAttributeMode Mode { get; }
}
