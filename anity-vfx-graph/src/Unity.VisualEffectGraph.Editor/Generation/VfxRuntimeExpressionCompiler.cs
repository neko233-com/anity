using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using UnityEngine.VFX;

namespace UnityEditor.VFX.Generation;

/// <summary>
/// Compiles the CPU-evaluated expression subset used by Custom Spawner
/// Callbacks. Instructions form an ordered SSA list: every operand references
/// an earlier instruction, so serialized programs cannot contain cycles.
/// </summary>
internal sealed class VfxRuntimeExpressionCompiler
{
    private static readonly BuiltInDescriptor[] BuiltIns =
    {
        new(1 << 0, VFXRuntimeExpressionOperation.VfxDeltaTime, VFXRuntimeValueType.Float),
        new(1 << 1, VFXRuntimeExpressionOperation.VfxUnscaledDeltaTime, VFXRuntimeValueType.Float),
        new(1 << 2, VFXRuntimeExpressionOperation.VfxTotalTime, VFXRuntimeValueType.Float),
        new(1 << 3, VFXRuntimeExpressionOperation.VfxFrameIndex, VFXRuntimeValueType.UInt32),
        new(1 << 4, VFXRuntimeExpressionOperation.VfxPlayRate, VFXRuntimeValueType.Float),
        new(1 << 5, VFXRuntimeExpressionOperation.VfxManagerFixedTimeStep, VFXRuntimeValueType.Float),
        new(1 << 6, VFXRuntimeExpressionOperation.VfxManagerMaxDeltaTime, VFXRuntimeValueType.Float),
        new(1 << 7, VFXRuntimeExpressionOperation.GameDeltaTime, VFXRuntimeValueType.Float),
        new(1 << 8, VFXRuntimeExpressionOperation.GameUnscaledDeltaTime, VFXRuntimeValueType.Float),
        new(1 << 9, VFXRuntimeExpressionOperation.GameSmoothDeltaTime, VFXRuntimeValueType.Float),
        new(1 << 10, VFXRuntimeExpressionOperation.GameTotalTime, VFXRuntimeValueType.Float),
        new(1 << 11, VFXRuntimeExpressionOperation.GameUnscaledTotalTime, VFXRuntimeValueType.Float),
        new(1 << 12, VFXRuntimeExpressionOperation.GameTotalTimeSinceSceneLoad, VFXRuntimeValueType.Float),
        new(1 << 13, VFXRuntimeExpressionOperation.GameTimeScale, VFXRuntimeValueType.Float),
        new(1 << 14, VFXRuntimeExpressionOperation.LocalToWorld, VFXRuntimeValueType.Matrix4x4),
        new(1 << 15, VFXRuntimeExpressionOperation.WorldToLocal, VFXRuntimeValueType.Matrix4x4),
        new(1 << 16, VFXRuntimeExpressionOperation.SystemSeed, VFXRuntimeValueType.UInt32)
    };
    private const int AllBuiltInFlags = (1 << 17) - 1;
    private readonly VfxTypedGraph _graph;
    private readonly List<VFXRuntimeExpressionInstructionData> _instructions = new();
    private readonly Dictionary<long, int> _compiledSlots = new();

    private VfxRuntimeExpressionCompiler(VfxTypedGraph graph)
    {
        _graph = graph;
    }

    internal static VFXRuntimeExpressionProgramData CompileInput(
        VfxTypedGraph graph,
        long inputSlotId)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        var compiler = new VfxRuntimeExpressionCompiler(graph);
        VfxModel input = compiler.ResolveSlot(inputSlotId);
        if (input.Direction != 0)
            throw new InvalidDataException(
                $"VFX runtime expression root '{inputSlotId}' is not an input slot.");
        int result = compiler.CompileSlot(input);
        VFXRuntimeValueType type = Type(input);
        return new VFXRuntimeExpressionProgramData(type, result, compiler._instructions.ToArray());
    }

    private int CompileSlot(VfxModel slot)
    {
        if (_compiledSlots.TryGetValue(slot.FileId, out int compiled)) return compiled;
        VFXRuntimeValueType type = Type(slot);
        int result;
        if (slot.Direction == 0)
        {
            if (slot.LinkedSlotIds.Count == 0) result = Constant(slot, type);
            else
            {
                if (slot.LinkedSlotIds.Count != 1)
                    throw new InvalidDataException(
                        $"VFX runtime expression input '{slot.FileId}' has multiple links.");
                VfxModel source = ResolveSlot(slot.LinkedSlotIds[0]);
                if (source.Direction != 1 || Type(source) != type)
                    throw new InvalidDataException(
                        $"VFX runtime expression link '{source.FileId}' -> '{slot.FileId}' changes type.");
                result = CompileSlot(source);
            }
        }
        else if (slot.Direction == 1)
        {
            result = CompileOutput(slot, type);
        }
        else
        {
            throw new InvalidDataException(
                $"VFX runtime expression slot '{slot.FileId}' has invalid direction.");
        }
        _compiledSlots.Add(slot.FileId, result);
        return result;
    }

    private int CompileOutput(VfxModel slot, VFXRuntimeValueType type)
    {
        if (slot.OwnerId == 0) return Constant(slot, type);
        if (!_graph.ModelsByFileId.TryGetValue(slot.OwnerId, out VfxModel? owner))
            throw new InvalidDataException(
                $"VFX runtime expression output '{slot.FileId}' has an unresolved owner.");
        if (owner.ScriptType.TypeName == "VFXParameter")
        {
            if (VfxYamlFields.ReadInt32(owner.Document.RawText, "m_Exposed") != 1)
                return Constant(slot, type);
            string name = VfxYamlFields.ReadString(owner.Document.RawText, "m_ExposedName")?.Trim()
                ?? string.Empty;
            if (name.Length == 0)
                throw new InvalidDataException($"VFX exposed parameter '{owner.FileId}' has no name.");
            return Emit(new VFXRuntimeExpressionInstructionData(
                VFXRuntimeExpressionOperation.ExposedProperty, type, -1, -1,
                Array.Empty<uint>(), name));
        }
        if (owner.ScriptType.TypeName == "VFXDynamicBuiltInParameter")
            return CompileBuiltIn(owner, slot, type);
        if (owner.Kind != VfxModelKind.Operator) return Constant(slot, type);

        VfxModel[] inputs = owner.InputSlotIds.Select(ResolveSlot).ToArray();
        if (inputs.Any(input => input.Direction != 0 || Type(input) != type))
            throw new InvalidDataException(
                $"VFX runtime expression operator '{owner.FileId}' changes input type.");
        int[] operands = inputs.Select(CompileSlot).ToArray();
        VFXRuntimeExpressionOperation operation = owner.ScriptType.TypeName switch
        {
            "Add" => Binary(owner, operands, VFXRuntimeExpressionOperation.Add),
            "Subtract" => Binary(owner, operands, VFXRuntimeExpressionOperation.Subtract),
            "Multiply" => Binary(owner, operands, VFXRuntimeExpressionOperation.Multiply),
            "OneMinus" => Unary(owner, operands, type),
            _ => throw new NotSupportedException(
                $"VFX runtime expression operator '{owner.ScriptType.TypeName}' is not implemented.")
        };
        return Emit(new VFXRuntimeExpressionInstructionData(
            operation,
            type,
            operands[0],
            operands.Length == 2 ? operands[1] : -1,
            Array.Empty<uint>(),
            null));
    }

    private int CompileBuiltIn(
        VfxModel owner,
        VfxModel output,
        VFXRuntimeValueType type)
    {
        int flags = VfxYamlFields.ReadInt32(owner.Document.RawText, "m_BuiltInParameters") ?? 0;
        if (flags == 0 || (flags & ~AllBuiltInFlags) != 0)
            throw new InvalidDataException(
                $"VFX built-in parameter '{owner.FileId}' has invalid flags '{flags}'.");
        BuiltInDescriptor[] enabled = BuiltIns.Where(descriptor =>
            (flags & descriptor.Flag) != 0).ToArray();
        if (enabled.Length != owner.OutputSlotIds.Count)
            throw new InvalidDataException(
                $"VFX built-in parameter '{owner.FileId}' output count does not match its flags.");
        int outputIndex = Array.IndexOf(owner.OutputSlotIds.ToArray(), output.FileId);
        if (outputIndex < 0)
            throw new InvalidDataException(
                $"VFX built-in parameter '{owner.FileId}' does not own output '{output.FileId}'.");
        BuiltInDescriptor descriptor = enabled[outputIndex];
        if (type != descriptor.Type)
            throw new InvalidDataException(
                $"VFX built-in parameter '{owner.FileId}' output '{output.FileId}' has type '{type}' " +
                $"instead of '{descriptor.Type}'.");
        return Emit(new VFXRuntimeExpressionInstructionData(
            descriptor.Operation, type, -1, -1, Array.Empty<uint>(), null));
    }

    private int Constant(VfxModel slot, VFXRuntimeValueType type)
    {
        VfxSlotProperty property = slot.SlotProperty
            ?? throw new InvalidDataException(
                $"VFX runtime expression slot '{slot.FileId}' has no typed value.");
        return Emit(new VFXRuntimeExpressionInstructionData(
            VFXRuntimeExpressionOperation.Constant,
            type,
            -1,
            -1,
            VfxRuntimeAssetCompiler.RuntimeWords(property.Value, type, slot.OwnerId, property.Name),
            null));
    }

    private static VFXRuntimeExpressionOperation Binary(
        VfxModel owner,
        IReadOnlyList<int> operands,
        VFXRuntimeExpressionOperation operation)
    {
        if (operands.Count != 2)
            throw new InvalidDataException(
                $"VFX runtime expression operator '{owner.ScriptType.TypeName}' requires two inputs.");
        return operation;
    }

    private static VFXRuntimeExpressionOperation Unary(
        VfxModel owner,
        IReadOnlyList<int> operands,
        VFXRuntimeValueType type)
    {
        if (operands.Count != 1)
            throw new InvalidDataException(
                $"VFX runtime expression operator '{owner.ScriptType.TypeName}' requires one input.");
        if (type is not (VFXRuntimeValueType.Float or VFXRuntimeValueType.Float2 or
                VFXRuntimeValueType.Float3 or VFXRuntimeValueType.Float4))
            throw new NotSupportedException("VFX runtime OneMinus requires a floating-point value.");
        return VFXRuntimeExpressionOperation.OneMinus;
    }

    private int Emit(VFXRuntimeExpressionInstructionData instruction)
    {
        int index = _instructions.Count;
        _instructions.Add(instruction);
        return index;
    }

    private VfxModel ResolveSlot(long id)
    {
        if (!_graph.ModelsByFileId.TryGetValue(id, out VfxModel? slot) ||
            slot.Kind != VfxModelKind.Slot)
            throw new InvalidDataException(
                $"VFX runtime expression cannot resolve slot '{id}'.");
        return slot;
    }

    private static VFXRuntimeValueType Type(VfxModel slot)
    {
        VfxSlotProperty property = slot.SlotProperty
            ?? throw new InvalidDataException(
                $"VFX runtime expression slot '{slot.FileId}' has no typed property.");
        return VfxRuntimeAssetCompiler.CallbackRuntimeType(
            property.Value.Kind, slot.OwnerId, property.Name);
    }

    private readonly record struct BuiltInDescriptor(
        int Flag,
        VFXRuntimeExpressionOperation Operation,
        VFXRuntimeValueType Type);
}
