using System.Collections.ObjectModel;
using UnityEditor.VFX.Model;

namespace UnityEditor.VFX.Generation;

/// <summary>
/// Compatibility entry point for callers that require a scalar result.
/// The shared typed compiler performs the actual DAG and HLSL generation.
/// </summary>
internal static class VfxScalarExpressionCompiler
{
    internal static VfxScalarCompilation Compile(VfxTypedGraph graph, long outputSlotId)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        if (!graph.ModelsByFileId.TryGetValue(outputSlotId, out VfxModel? output) || output.Kind != VfxModelKind.Slot)
            throw new InvalidDataException($"VFX scalar compiler cannot resolve slot '{outputSlotId}'.");
        if (!string.Equals(output.ScriptType.TypeName, "VFXSlotFloat", StringComparison.Ordinal))
            throw new NotSupportedException(
                $"VFX scalar compiler only supports VFXSlotFloat; slot '{outputSlotId}' is '{output.ScriptType.TypeName}'.");
        VfxExpressionCompilation compilation = VfxExpressionCompiler.Compile(graph, outputSlotId);
        if (compilation.ResultType != VfxExpressionValueType.Float)
            throw new NotSupportedException(
                $"VFX scalar compiler only supports VFXSlotFloat; slot '{outputSlotId}' is '{compilation.ResultType}'.");
        return new VfxScalarCompilation(
            compilation.HlslSource,
            compilation.ResultVariable,
            compilation.OrderedSlotIds);
    }
}

internal sealed class VfxScalarCompilation
{
    internal VfxScalarCompilation(string hlslSource, string resultVariable, IReadOnlyList<long> orderedSlotIds)
    {
        HlslSource = hlslSource;
        ResultVariable = resultVariable;
        OrderedSlotIds = new ReadOnlyCollection<long>(orderedSlotIds.ToArray());
    }

    internal string HlslSource { get; }

    internal string ResultVariable { get; }

    internal IReadOnlyList<long> OrderedSlotIds { get; }
}
