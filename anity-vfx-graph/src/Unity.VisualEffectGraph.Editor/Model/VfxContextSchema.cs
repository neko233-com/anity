using System.Collections.ObjectModel;
using UnityEditor.VFX.Serialization;

namespace UnityEditor.VFX.Model;

[Flags]
internal enum VfxContextType
{
    None = 0,
    Spawner = 1 << 0,
    Init = 1 << 1,
    OutputEvent = 1 << 2,
    Update = 1 << 3,
    Output = 1 << 4,
    Event = 1 << 5,
    SpawnerGpu = 1 << 6,
    Subgraph = 1 << 7,
    Filter = 1 << 8
}

[Flags]
internal enum VfxDataType
{
    None = 0,
    SpawnEvent = 1 << 0,
    OutputEvent = 1 << 1,
    Particle = 1 << 2,
    Mesh = 1 << 3,
    ParticleStrip = (1 << 4) | Particle
}

internal enum VfxTaskKind
{
    None,
    Spawner,
    Initialize,
    Update,
    Output,
    ParticlePointOutput,
    ParticleQuadOutput,
    ParticleTriangleOutput,
    ParticleOctagonOutput
}

internal enum VfxBoundsSettingMode
{
    Recorded = 0,
    Manual = 1,
    Automatic = 2
}

internal sealed class VfxDataDescriptor
{
    internal VfxDataDescriptor(
        VfxModel model,
        VfxDataType dataType,
        uint? capacity,
        uint? stripCapacity,
        uint? particlesPerStrip,
        VfxCoordinateSpace space,
        VfxBoundsSettingMode? boundsMode,
        bool needsComputeBounds)
    {
        Model = model;
        DataType = dataType;
        Capacity = capacity;
        StripCapacity = stripCapacity;
        ParticlesPerStrip = particlesPerStrip;
        Space = space;
        BoundsMode = boundsMode;
        NeedsComputeBounds = needsComputeBounds;
    }

    internal VfxModel Model { get; }
    internal VfxDataType DataType { get; }
    internal uint? Capacity { get; }
    internal uint? StripCapacity { get; }
    internal uint? ParticlesPerStrip { get; }
    internal VfxCoordinateSpace Space { get; }
    internal VfxBoundsSettingMode? BoundsMode { get; }
    internal bool NeedsComputeBounds { get; }
}

internal sealed class VfxContextDescriptor
{
    internal VfxContextDescriptor(
        VfxModel model,
        VfxContextType contextType,
        VfxDataType inputType,
        VfxDataType outputType,
        VfxTaskKind task,
        string? eventName,
        VfxDataDescriptor? data)
    {
        Model = model;
        ContextType = contextType;
        InputType = inputType;
        OutputType = outputType;
        Task = task;
        EventName = eventName;
        Data = data;
    }

    internal VfxModel Model { get; }
    internal VfxContextType ContextType { get; }
    internal VfxDataType InputType { get; }
    internal VfxDataType OutputType { get; }
    internal VfxTaskKind Task { get; }
    internal string? EventName { get; }
    internal VfxDataDescriptor? Data { get; }
    internal bool GeneratesCompute => Task is VfxTaskKind.Initialize or VfxTaskKind.Update;
}

internal sealed class VfxContextSchema
{
    private readonly ReadOnlyCollection<VfxDataDescriptor> _data;
    private readonly ReadOnlyCollection<VfxContextDescriptor> _contexts;
    private readonly ReadOnlyDictionary<long, VfxDataDescriptor> _dataById;
    private readonly ReadOnlyDictionary<long, VfxContextDescriptor> _contextsById;

    private VfxContextSchema(
        List<VfxDataDescriptor> data,
        List<VfxContextDescriptor> contexts,
        Dictionary<long, VfxDataDescriptor> dataById,
        Dictionary<long, VfxContextDescriptor> contextsById)
    {
        _data = data.AsReadOnly();
        _contexts = contexts.AsReadOnly();
        _dataById = new ReadOnlyDictionary<long, VfxDataDescriptor>(dataById);
        _contextsById = new ReadOnlyDictionary<long, VfxContextDescriptor>(contextsById);
    }

    internal IReadOnlyList<VfxDataDescriptor> Data => _data;
    internal IReadOnlyList<VfxContextDescriptor> Contexts => _contexts;
    internal IReadOnlyDictionary<long, VfxDataDescriptor> DataById => _dataById;
    internal IReadOnlyDictionary<long, VfxContextDescriptor> ContextsById => _contextsById;

    internal static VfxContextSchema Create(VfxTypedGraph graph)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        var data = graph.Data.Select(ParseData).ToList();
        var dataById = data.ToDictionary(item => item.Model.FileId);
        var contexts = graph.Contexts.Select(context => ParseContext(context, dataById)).ToList();
        var contextsById = contexts.ToDictionary(context => context.Model.FileId);
        ValidateFlowTypes(graph.FlowEdges, contextsById);
        return new VfxContextSchema(data, contexts, dataById, contextsById);
    }

    private static VfxDataDescriptor ParseData(VfxModel model)
    {
        switch (model.ScriptType.TypeName)
        {
            case "VFXDataSpawner":
                return new VfxDataDescriptor(
                    model, VfxDataType.SpawnEvent, null, null, null,
                    VfxCoordinateSpace.None, null, false);
            case "VFXDataMesh":
                return new VfxDataDescriptor(
                    model, VfxDataType.Mesh, null, null, null,
                    VfxCoordinateSpace.None, null, false);
            case "VFXDataParticle":
                return ParseParticleData(model);
            default:
                throw new NotSupportedException($"VFX data type '{model.ScriptType.TypeName}' is not implemented.");
        }
    }

    private static VfxDataDescriptor ParseParticleData(VfxModel model)
    {
        int dataType = ReadRequiredInt(model, "dataType");
        VfxDataType type = dataType switch
        {
            0 => VfxDataType.Particle,
            1 => VfxDataType.ParticleStrip,
            _ => throw new InvalidDataException($"VFX particle data '{model.FileId}' has invalid dataType '{dataType}'.")
        };
        uint capacity = ReadPositiveUInt(model, "capacity");
        uint stripCapacity = ReadPositiveUInt(model, "stripCapacity");
        uint particlesPerStrip = ReadPositiveUInt(model, "particlePerStripCount");
        if (type == VfxDataType.ParticleStrip &&
            checked((ulong)stripCapacity * particlesPerStrip) != capacity)
            throw new InvalidDataException(
                $"VFX particle strip data '{model.FileId}' capacity must equal stripCapacity * particlePerStripCount.");
        int space = ReadRequiredInt(model, "m_Space");
        if (space is not ((int)VfxCoordinateSpace.Local) and not ((int)VfxCoordinateSpace.World))
            throw new InvalidDataException($"VFX particle data '{model.FileId}' has invalid simulation space '{space}'.");
        VfxBoundsSettingMode boundsMode = ReadEnum(model, "boundsMode", VfxBoundsSettingMode.Recorded);
        bool needsComputeBounds = ReadBool(model, "needsComputeBounds", false);
        return new VfxDataDescriptor(
            model, type, capacity, stripCapacity, particlesPerStrip,
            (VfxCoordinateSpace)space, boundsMode, needsComputeBounds);
    }

    private static VfxContextDescriptor ParseContext(
        VfxModel model,
        IReadOnlyDictionary<long, VfxDataDescriptor> dataById)
    {
        VfxDataDescriptor? data = null;
        if (model.DataId != 0 && !dataById.TryGetValue(model.DataId, out data))
            throw new InvalidDataException($"VFX context '{model.FileId}' has unresolved typed data '{model.DataId}'.");

        VfxContextDescriptor descriptor = model.ScriptType.TypeName switch
        {
            "VFXBasicSpawner" => C(model, VfxContextType.Spawner, VfxDataType.SpawnEvent, VfxDataType.SpawnEvent, VfxTaskKind.Spawner, data),
            "VFXBasicEvent" => C(model, VfxContextType.Event, VfxDataType.None, VfxDataType.SpawnEvent, VfxTaskKind.None, data,
                ReadEventName(model, "eventName", "OnPlay")),
            "VFXBasicGPUEvent" => C(model, VfxContextType.SpawnerGpu, VfxDataType.None, VfxDataType.SpawnEvent, VfxTaskKind.None, data),
            "VFXBasicInitialize" => C(model, VfxContextType.Init, VfxDataType.SpawnEvent, RequireDataType(model, data), VfxTaskKind.Initialize, data),
            "VFXBasicUpdate" => C(model, VfxContextType.Update, RequireDataType(model, data), RequireDataType(model, data), VfxTaskKind.Update, data),
            "VFXPlanarPrimitiveOutput" => C(model, VfxContextType.Output, RequireDataType(model, data), VfxDataType.None, PlanarTask(model), data),
            "VFXQuadStripOutput" => C(model, VfxContextType.Output, RequireDataType(model, data), VfxDataType.None, VfxTaskKind.ParticleQuadOutput, data),
            "VFXStaticMeshOutput" => C(model, VfxContextType.Output, VfxDataType.Mesh, VfxDataType.None, VfxTaskKind.Output, data),
            "VFXOutputEvent" => C(model, VfxContextType.OutputEvent, VfxDataType.SpawnEvent, VfxDataType.OutputEvent, VfxTaskKind.None, data,
                ReadEventName(model, "eventName", "On Received Event")),
            "VFXSubgraphContext" => C(model, VfxContextType.Subgraph, VfxDataType.SpawnEvent, VfxDataType.None, VfxTaskKind.None, data),
            _ => throw new NotSupportedException($"VFX context type '{model.ScriptType.TypeName}' is not implemented.")
        };
        ValidateFlowSlotCounts(descriptor);
        ValidateDataCompatibility(descriptor);
        return descriptor;
    }

    private static VfxContextDescriptor C(
        VfxModel model,
        VfxContextType contextType,
        VfxDataType inputType,
        VfxDataType outputType,
        VfxTaskKind task,
        VfxDataDescriptor? data,
        string? eventName = null)
        => new(model, contextType, inputType, outputType, task, eventName, data);

    private static VfxTaskKind PlanarTask(VfxModel model)
    {
        if (ReadBool(model, "useGeometryShader", false)) return VfxTaskKind.ParticlePointOutput;
        return (VfxYamlFields.ReadInt32(model.Document.RawText, "primitiveType") ?? 1) switch
        {
            0 => VfxTaskKind.ParticleTriangleOutput,
            1 => VfxTaskKind.ParticleQuadOutput,
            2 => VfxTaskKind.ParticleOctagonOutput,
            int value => throw new InvalidDataException($"VFX planar output '{model.FileId}' has invalid primitiveType '{value}'.")
        };
    }

    private static void ValidateFlowSlotCounts(VfxContextDescriptor descriptor)
    {
        (int Inputs, int Outputs)? expected = descriptor.ContextType switch
        {
            VfxContextType.Spawner => (2, 1),
            VfxContextType.OutputEvent => (1, 0),
            VfxContextType.Subgraph => null,
            _ => (1, 1)
        };
        if (expected is not null &&
            (descriptor.Model.InputFlowSlots.Count != expected.Value.Inputs ||
             descriptor.Model.OutputFlowSlots.Count != expected.Value.Outputs))
            throw new InvalidDataException(
                $"VFX context '{descriptor.Model.FileId}' flow slot count does not match {descriptor.ContextType}.");
    }

    private static void ValidateDataCompatibility(VfxContextDescriptor descriptor)
    {
        if (descriptor.Data is null)
        {
            if (descriptor.ContextType is VfxContextType.Event or VfxContextType.OutputEvent or VfxContextType.Subgraph)
                return;
            throw new InvalidDataException($"VFX context '{descriptor.Model.FileId}' requires typed data.");
        }
        VfxDataType owned = descriptor.ContextType == VfxContextType.Output
            ? descriptor.InputType
            : descriptor.OutputType;
        if (owned != VfxDataType.None && (owned & descriptor.Data.DataType) != owned)
            throw new InvalidDataException(
                $"VFX context '{descriptor.Model.FileId}' data type '{descriptor.Data.DataType}' is incompatible with '{owned}'.");
    }

    private static void ValidateFlowTypes(
        IReadOnlyList<VfxFlowEdge> edges,
        IReadOnlyDictionary<long, VfxContextDescriptor> contexts)
    {
        foreach (VfxFlowEdge edge in edges)
        {
            VfxContextDescriptor source = contexts[edge.SourceContextId];
            VfxContextDescriptor target = contexts[edge.TargetContextId];
            if (source.OutputType == VfxDataType.None || target.InputType == VfxDataType.None ||
                source.OutputType != target.InputType)
                throw new InvalidDataException(
                    $"VFX flow '{source.Model.FileId}' -> '{target.Model.FileId}' has incompatible data types {source.OutputType} -> {target.InputType}.");
        }
    }

    private static VfxDataType RequireDataType(VfxModel model, VfxDataDescriptor? data)
        => data?.DataType ?? throw new InvalidDataException($"VFX context '{model.FileId}' requires data.");

    private static string ReadEventName(VfxModel model, string field, string defaultValue)
    {
        string value = VfxYamlFields.ReadString(model.Document.RawText, field) ?? defaultValue;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"VFX event context '{model.FileId}' requires a non-empty event name.");
        return value;
    }

    private static int ReadRequiredInt(VfxModel model, string field)
        => VfxYamlFields.ReadInt32(model.Document.RawText, field)
           ?? throw new InvalidDataException($"VFX model '{model.FileId}' requires integer {field}.");

    private static uint ReadPositiveUInt(VfxModel model, string field)
    {
        int value = ReadRequiredInt(model, field);
        if (value <= 0) throw new InvalidDataException($"VFX model '{model.FileId}' requires positive {field}.");
        return (uint)value;
    }

    private static bool ReadBool(VfxModel model, string field, bool defaultValue)
    {
        int value = VfxYamlFields.ReadInt32(model.Document.RawText, field) ?? (defaultValue ? 1 : 0);
        if (value is not (0 or 1))
            throw new InvalidDataException($"VFX model '{model.FileId}' field {field} must be 0 or 1.");
        return value == 1;
    }

    private static T ReadEnum<T>(VfxModel model, string field, T defaultValue) where T : struct, Enum
    {
        int value = VfxYamlFields.ReadInt32(model.Document.RawText, field) ?? Convert.ToInt32(defaultValue);
        if (!Enum.IsDefined(typeof(T), value))
            throw new InvalidDataException($"VFX model '{model.FileId}' has invalid {field} '{value}'.");
        return (T)Enum.ToObject(typeof(T), value);
    }
}
