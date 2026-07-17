using System.Security.Cryptography;
using System.Text;

namespace UnityEngine.VFX;

internal enum VFXRuntimeValueType : byte
{
    Boolean = 1,
    UInt32 = 2,
    Int32 = 3,
    Float = 4,
    Float2 = 5,
    Float3 = 6,
    Float4 = 7,
    Matrix4x4 = 8
}

internal enum VFXRuntimeSystemKind : byte
{
    Particle = 1,
    ParticleStrip = 2,
    Spawn = 3,
    Mesh = 4
}

internal enum VFXRuntimeInitializeValueSource : byte
{
    Constant = 0,
    Source = 1,
    ParticleId = 2,
    Seed = 3,
    SpawnIndex = 4
}

internal enum VFXRuntimeInitializeComposition : byte
{
    Overwrite = 0,
    Add = 1,
    Multiply = 2,
    Blend = 3
}

internal enum VFXRuntimeInitializeRandomMode : byte
{
    Off = 0,
    PerComponent = 1,
    Uniform = 2
}

internal enum VFXRuntimeUpdateOperationKind : byte
{
    SetAttribute = 0,
    CopyAttribute = 1,
    Integrate = 2,
    Reap = 3,
    Force = 4,
    RelativeForce = 5,
    Drag = 6
}

internal enum VFXRuntimeSpawnerBlockKind : byte
{
    ConstantRate = 1,
    VariableRate = 2,
    Burst = 3,
    SetAttribute = 4,
    CustomCallback = 5
}

internal enum VFXRuntimeSpawnerValueMode : byte
{
    Disabled = 0,
    Constant = 1,
    Random = 2,
    Infinite = 3
}

internal enum VFXRuntimeExpressionOperation : byte
{
    Constant = 1,
    ExposedProperty = 2,
    Add = 3,
    Subtract = 4,
    Multiply = 5,
    OneMinus = 6,
    VfxDeltaTime = 7,
    VfxUnscaledDeltaTime = 8,
    VfxTotalTime = 9,
    VfxFrameIndex = 10,
    VfxPlayRate = 11,
    VfxManagerFixedTimeStep = 12,
    VfxManagerMaxDeltaTime = 13,
    GameDeltaTime = 14,
    GameUnscaledDeltaTime = 15,
    GameSmoothDeltaTime = 16,
    GameTotalTime = 17,
    GameUnscaledTotalTime = 18,
    GameTotalTimeSinceSceneLoad = 19,
    GameTimeScale = 20,
    LocalToWorld = 21,
    WorldToLocal = 22,
    SystemSeed = 23
}

internal readonly record struct VFXRuntimeAttributeData(
    string Name,
    VFXRuntimeValueType ValueType,
    int OffsetWords,
    int SizeWords);

internal readonly record struct VFXRuntimeSystemData(
    string Name,
    VFXRuntimeSystemKind Kind,
    uint Capacity)
{
    internal bool HasStaticBounds { get; init; }
    internal bool BoundsInWorldSpace { get; init; }
    internal float BoundsCenterX { get; init; }
    internal float BoundsCenterY { get; init; }
    internal float BoundsCenterZ { get; init; }
    internal float BoundsSizeX { get; init; }
    internal float BoundsSizeY { get; init; }
    internal float BoundsSizeZ { get; init; }
    internal bool HasAutomaticBounds { get; init; }
    internal int PositionOffsetWords { get; init; } = -1;
    internal int AliveOffsetWords { get; init; } = -1;
    internal int SizeOffsetWords { get; init; } = -1;
    internal int ScaleXOffsetWords { get; init; } = -1;
    internal int ScaleYOffsetWords { get; init; } = -1;
    internal int ScaleZOffsetWords { get; init; } = -1;
    internal float AutomaticBoundsPaddingX { get; init; }
    internal float AutomaticBoundsPaddingY { get; init; }
    internal float AutomaticBoundsPaddingZ { get; init; }
}

internal sealed record VFXRuntimeInitializeAttributeData(
    VFXRuntimeAttributeData Layout,
    IReadOnlyList<uint> DefaultWords);

internal sealed record VFXRuntimeInitializeOperationData(
    int TargetOffsetWords,
    int SourceOffsetWords,
    VFXRuntimeValueType ValueType,
    VFXRuntimeInitializeValueSource ValueSource,
    VFXRuntimeInitializeComposition Composition,
    VFXRuntimeInitializeRandomMode RandomMode,
    IReadOnlyList<uint> ValueA,
    IReadOnlyList<uint> ValueB,
    uint BlendFactorBits);

internal sealed record VFXRuntimeInitializeKernelData(
    long ContextId,
    uint ParticleCapacity,
    int AttributeStrideWords,
    int SourceStrideWords,
    bool UsesDeadList,
    IReadOnlyList<VFXRuntimeInitializeAttributeData> Attributes,
    IReadOnlyList<VFXRuntimeInitializeOperationData> Operations,
    int SpawnCountSourceOffsetWords = -1);

internal sealed record VFXRuntimeUpdateOperationData(
    VFXRuntimeUpdateOperationKind Kind,
    int TargetOffsetWords,
    int SourceAOffsetWords,
    int SourceBOffsetWords,
    int AuxiliaryOffset0Words,
    int AuxiliaryOffset1Words,
    VFXRuntimeValueType ValueType,
    VFXRuntimeInitializeComposition Composition,
    VFXRuntimeInitializeRandomMode RandomMode,
    bool ReadSourceSnapshot,
    IReadOnlyList<uint> ValueA,
    IReadOnlyList<uint> ValueB,
    uint BlendFactorBits);

internal sealed record VFXRuntimeUpdateKernelData(
    long ContextId,
    string ParticleSystemName,
    uint ParticleCapacity,
    int AttributeStrideWords,
    bool UsesDeadList,
    bool SkipZeroDeltaUpdate,
    int AliveOffsetWords,
    int SeedOffsetWords,
    IReadOnlyList<VFXRuntimeUpdateOperationData> Operations);

internal sealed record VFXRuntimeInputEventTargetData(
    long InitializeContextId,
    string ParticleSystemName,
    IReadOnlyList<long> SpawnerContextIds,
    IReadOnlyList<string> SpawnSystemNames,
    VFXRuntimeInitializeKernelData? InitializeKernel = null);

internal sealed record VFXRuntimeInputEventData(
    string Name,
    IReadOnlyList<VFXRuntimeInputEventTargetData> Targets);

internal readonly record struct VFXRuntimeOutputEventMapping(string Name, long SourceSpawnerContextId);

internal sealed record VFXRuntimeOutputEventData(
    string Name,
    IReadOnlyList<long> ContextIds,
    IReadOnlyList<VFXRuntimeOutputEventMapping> BufferMappings,
    IReadOnlyList<VFXRuntimeAttributeData> Attributes,
    int StrideWords);

internal sealed record VFXRuntimePlanarOutputData(
    long ContextId,
    string ParticleSystemName,
    int PrimitiveType,
    int VerticesPerParticle,
    IReadOnlyList<int> IndexPattern,
    int UvMode,
    int BlendMode,
    int CullMode,
    bool ZWrite,
    int ZTest,
    bool AlphaClipping,
    string RenderQueue,
    bool RequiresSorting,
    bool IndirectDraw,
    bool RuntimeExecutable,
    IReadOnlyList<VFXRuntimeAttributeData> Attributes,
    int AttributeStrideWords);

internal readonly record struct VFXRuntimeSpawnerControlData(string EventName, int InputSlotIndex);

internal sealed record VFXRuntimeExposedPropertyData(
    string Name,
    VFXRuntimeValueType ValueType,
    IReadOnlyList<uint> DefaultWords);

internal sealed record VFXRuntimeExpressionInstructionData(
    VFXRuntimeExpressionOperation Operation,
    VFXRuntimeValueType ValueType,
    int InputA,
    int InputB,
    IReadOnlyList<uint> ConstantWords,
    string? PropertyName);

internal sealed record VFXRuntimeExpressionProgramData(
    VFXRuntimeValueType ResultType,
    int ResultIndex,
    IReadOnlyList<VFXRuntimeExpressionInstructionData> Instructions);

internal sealed record VFXRuntimeSpawnerOutputData(
    long InitializeContextId,
    string ParticleSystemName,
    VFXRuntimeInitializeKernelData? InitializeKernel);

internal sealed record VFXRuntimeSpawnerExpressionValueData(
    string Name,
    VFXRuntimeValueType ValueType,
    IReadOnlyList<uint> Words)
{
    internal string? SourcePropertyName { get; init; }
    internal VFXRuntimeExpressionProgramData? Expression { get; init; }
}

internal sealed record VFXRuntimeSpawnerBlockData(
    long BlockId,
    VFXRuntimeSpawnerBlockKind Kind,
    float ValueMin,
    float ValueMax,
    float PeriodMin,
    float PeriodMax,
    bool Periodic)
{
    internal int TargetOffsetWords { get; init; } = -1;
    internal VFXRuntimeValueType TargetValueType { get; init; } = VFXRuntimeValueType.Float;
    internal VFXRuntimeInitializeRandomMode RandomMode { get; init; } = VFXRuntimeInitializeRandomMode.Off;
    internal IReadOnlyList<uint> ValueA { get; init; } = Array.Empty<uint>();
    internal IReadOnlyList<uint> ValueB { get; init; } = Array.Empty<uint>();
    internal string? CallbackTypeName { get; init; }
    internal IReadOnlyList<VFXRuntimeSpawnerExpressionValueData> CallbackValues { get; init; } =
        Array.Empty<VFXRuntimeSpawnerExpressionValueData>();
}

internal sealed record VFXRuntimeSpawnerProgramData(
    long ContextId,
    string SystemName,
    VFXRuntimeSpawnerValueMode LoopDurationMode,
    VFXRuntimeSpawnerValueMode LoopCountMode,
    VFXRuntimeSpawnerValueMode DelayBeforeLoopMode,
    VFXRuntimeSpawnerValueMode DelayAfterLoopMode,
    float LoopDurationMin,
    float LoopDurationMax,
    double LoopCountMin,
    double LoopCountMax,
    float DelayBeforeLoopMin,
    float DelayBeforeLoopMax,
    float DelayAfterLoopMin,
    float DelayAfterLoopMax,
    IReadOnlyList<VFXRuntimeSpawnerControlData> Controls,
    IReadOnlyList<VFXRuntimeSpawnerOutputData> Outputs,
    IReadOnlyList<VFXRuntimeSpawnerBlockData> Blocks)
{
    internal int EventStrideWords { get; init; }
}

internal sealed record VFXRuntimeAssetData(
    IReadOnlyList<VFXRuntimeAttributeData> EventAttributes,
    IReadOnlyList<string> InputEvents,
    IReadOnlyList<VFXRuntimeInputEventData> InputEventDispatches,
    IReadOnlyList<VFXRuntimeSystemData> Systems,
    IReadOnlyList<VFXRuntimeOutputEventData> OutputEvents)
{
    private const uint Magic = 0x58564641; // AFVX
    private const uint FormatVersion = 15;
    private const uint MinimumSupportedVersion = 1;
    private const int HashSize = 32;
    private const int MaxPayloadBytes = 64 * 1024 * 1024;
    private const int MaxCollectionCount = 1_000_000;
    private const int MaxStringBytes = 1024 * 1024;

    internal IReadOnlyList<VFXRuntimeSpawnerProgramData> SpawnerPrograms { get; init; } =
        Array.Empty<VFXRuntimeSpawnerProgramData>();
    internal IReadOnlyList<VFXRuntimeExposedPropertyData> ExposedProperties { get; init; } =
        Array.Empty<VFXRuntimeExposedPropertyData>();
    internal IReadOnlyList<VFXRuntimeUpdateKernelData> UpdateKernels { get; init; } =
        Array.Empty<VFXRuntimeUpdateKernelData>();
    internal IReadOnlyList<VFXRuntimePlanarOutputData> PlanarOutputs { get; init; } =
        Array.Empty<VFXRuntimePlanarOutputData>();

    internal byte[] Serialize()
    {
        Validate();
        byte[] payload;
        using (var payloadStream = new MemoryStream())
        using (var writer = new BinaryWriter(payloadStream, Encoding.UTF8, leaveOpen: true))
        {
            WriteAttributes(writer, EventAttributes);
            WriteCount(writer, InputEvents.Count);
            foreach (string name in InputEvents) WriteString(writer, name);
            WriteCount(writer, InputEventDispatches.Count);
            foreach (VFXRuntimeInputEventData inputEvent in InputEventDispatches)
            {
                WriteString(writer, inputEvent.Name);
                WriteCount(writer, inputEvent.Targets.Count);
                foreach (VFXRuntimeInputEventTargetData target in inputEvent.Targets)
                {
                    writer.Write(target.InitializeContextId);
                    WriteString(writer, target.ParticleSystemName);
                    WriteCount(writer, target.SpawnerContextIds.Count);
                    foreach (long contextId in target.SpawnerContextIds) writer.Write(contextId);
                    WriteCount(writer, target.SpawnSystemNames.Count);
                    foreach (string systemName in target.SpawnSystemNames) WriteString(writer, systemName);
                    writer.Write(target.InitializeKernel is not null);
                    if (target.InitializeKernel is not null)
                        WriteInitializeKernel(writer, target.InitializeKernel);
                }
            }
            WriteCount(writer, Systems.Count);
            foreach (VFXRuntimeSystemData system in Systems)
            {
                WriteString(writer, system.Name);
                writer.Write((byte)system.Kind);
                writer.Write(system.Capacity);
                writer.Write(system.HasStaticBounds);
                writer.Write(system.BoundsInWorldSpace);
                writer.Write(system.BoundsCenterX);
                writer.Write(system.BoundsCenterY);
                writer.Write(system.BoundsCenterZ);
                writer.Write(system.BoundsSizeX);
                writer.Write(system.BoundsSizeY);
                writer.Write(system.BoundsSizeZ);
                writer.Write(system.HasAutomaticBounds);
                writer.Write(system.PositionOffsetWords);
                writer.Write(system.AliveOffsetWords);
                writer.Write(system.SizeOffsetWords);
                writer.Write(system.ScaleXOffsetWords);
                writer.Write(system.ScaleYOffsetWords);
                writer.Write(system.ScaleZOffsetWords);
                writer.Write(system.AutomaticBoundsPaddingX);
                writer.Write(system.AutomaticBoundsPaddingY);
                writer.Write(system.AutomaticBoundsPaddingZ);
            }
            WriteCount(writer, OutputEvents.Count);
            foreach (VFXRuntimeOutputEventData outputEvent in OutputEvents)
            {
                WriteString(writer, outputEvent.Name);
                WriteCount(writer, outputEvent.ContextIds.Count);
                foreach (long contextId in outputEvent.ContextIds) writer.Write(contextId);
                WriteCount(writer, outputEvent.BufferMappings.Count);
                foreach (VFXRuntimeOutputEventMapping mapping in outputEvent.BufferMappings)
                {
                    WriteString(writer, mapping.Name);
                    writer.Write(mapping.SourceSpawnerContextId);
                }
                WriteAttributes(writer, outputEvent.Attributes);
                writer.Write(outputEvent.StrideWords);
            }
            WriteCount(writer, ExposedProperties.Count);
            foreach (VFXRuntimeExposedPropertyData property in ExposedProperties)
            {
                WriteString(writer, property.Name);
                writer.Write((byte)property.ValueType);
                WriteCount(writer, property.DefaultWords.Count);
                foreach (uint word in property.DefaultWords) writer.Write(word);
            }
            WriteCount(writer, SpawnerPrograms.Count);
            foreach (VFXRuntimeSpawnerProgramData program in SpawnerPrograms)
            {
                writer.Write(program.ContextId);
                WriteString(writer, program.SystemName);
                writer.Write((byte)program.LoopDurationMode);
                writer.Write((byte)program.LoopCountMode);
                writer.Write((byte)program.DelayBeforeLoopMode);
                writer.Write((byte)program.DelayAfterLoopMode);
                writer.Write(program.LoopDurationMin);
                writer.Write(program.LoopDurationMax);
                writer.Write(program.LoopCountMin);
                writer.Write(program.LoopCountMax);
                writer.Write(program.DelayBeforeLoopMin);
                writer.Write(program.DelayBeforeLoopMax);
                writer.Write(program.DelayAfterLoopMin);
                writer.Write(program.DelayAfterLoopMax);
                writer.Write(program.EventStrideWords);
                WriteCount(writer, program.Controls.Count);
                foreach (VFXRuntimeSpawnerControlData control in program.Controls)
                {
                    WriteString(writer, control.EventName);
                    writer.Write(control.InputSlotIndex);
                }
                WriteCount(writer, program.Outputs.Count);
                foreach (VFXRuntimeSpawnerOutputData output in program.Outputs)
                {
                    writer.Write(output.InitializeContextId);
                    WriteString(writer, output.ParticleSystemName);
                    writer.Write(output.InitializeKernel is not null);
                    if (output.InitializeKernel is not null)
                        WriteInitializeKernel(writer, output.InitializeKernel);
                }
                WriteCount(writer, program.Blocks.Count);
                foreach (VFXRuntimeSpawnerBlockData block in program.Blocks)
                {
                    writer.Write(block.BlockId);
                    writer.Write((byte)block.Kind);
                    writer.Write(block.ValueMin);
                    writer.Write(block.ValueMax);
                    writer.Write(block.PeriodMin);
                    writer.Write(block.PeriodMax);
                    writer.Write(block.Periodic);
                    writer.Write(block.TargetOffsetWords);
                    writer.Write((byte)block.TargetValueType);
                    writer.Write((byte)block.RandomMode);
                    WriteCount(writer, block.ValueA.Count);
                    foreach (uint word in block.ValueA) writer.Write(word);
                    WriteCount(writer, block.ValueB.Count);
                    foreach (uint word in block.ValueB) writer.Write(word);
                    writer.Write(block.CallbackTypeName is not null);
                    if (block.CallbackTypeName is not null)
                        WriteString(writer, block.CallbackTypeName);
                    WriteCount(writer, block.CallbackValues.Count);
                    foreach (VFXRuntimeSpawnerExpressionValueData value in block.CallbackValues)
                    {
                        WriteString(writer, value.Name);
                        writer.Write((byte)value.ValueType);
                        WriteCount(writer, value.Words.Count);
                        foreach (uint word in value.Words) writer.Write(word);
                        writer.Write(value.SourcePropertyName is not null);
                        if (value.SourcePropertyName is not null)
                            WriteString(writer, value.SourcePropertyName);
                        writer.Write(value.Expression is not null);
                        if (value.Expression is not null)
                            WriteExpressionProgram(writer, value.Expression);
                    }
                }
            }
            WriteCount(writer, UpdateKernels.Count);
            foreach (VFXRuntimeUpdateKernelData kernel in UpdateKernels)
            {
                writer.Write(kernel.ContextId);
                WriteString(writer, kernel.ParticleSystemName);
                writer.Write(kernel.ParticleCapacity);
                writer.Write(kernel.AttributeStrideWords);
                writer.Write(kernel.UsesDeadList);
                writer.Write(kernel.SkipZeroDeltaUpdate);
                writer.Write(kernel.AliveOffsetWords);
                writer.Write(kernel.SeedOffsetWords);
                WriteCount(writer, kernel.Operations.Count);
                foreach (VFXRuntimeUpdateOperationData operation in kernel.Operations)
                {
                    writer.Write((byte)operation.Kind);
                    writer.Write(operation.TargetOffsetWords);
                    writer.Write(operation.SourceAOffsetWords);
                    writer.Write(operation.SourceBOffsetWords);
                    writer.Write(operation.AuxiliaryOffset0Words);
                    writer.Write(operation.AuxiliaryOffset1Words);
                    writer.Write((byte)operation.ValueType);
                    writer.Write((byte)operation.Composition);
                    writer.Write((byte)operation.RandomMode);
                    writer.Write(operation.ReadSourceSnapshot);
                    WriteCount(writer, operation.ValueA.Count);
                    foreach (uint word in operation.ValueA) writer.Write(word);
                    WriteCount(writer, operation.ValueB.Count);
                    foreach (uint word in operation.ValueB) writer.Write(word);
                    writer.Write(operation.BlendFactorBits);
                }
            }
            WriteCount(writer, PlanarOutputs.Count);
            foreach (VFXRuntimePlanarOutputData output in PlanarOutputs)
            {
                writer.Write(output.ContextId);
                WriteString(writer, output.ParticleSystemName);
                writer.Write(output.PrimitiveType);
                writer.Write(output.VerticesPerParticle);
                WriteCount(writer, output.IndexPattern.Count);
                foreach (int index in output.IndexPattern) writer.Write(index);
                writer.Write(output.UvMode);
                writer.Write(output.BlendMode);
                writer.Write(output.CullMode);
                writer.Write(output.ZWrite);
                writer.Write(output.ZTest);
                writer.Write(output.AlphaClipping);
                WriteString(writer, output.RenderQueue);
                writer.Write(output.RequiresSorting);
                writer.Write(output.IndirectDraw);
                writer.Write(output.RuntimeExecutable);
                WriteAttributes(writer, output.Attributes);
                writer.Write(output.AttributeStrideWords);
            }
            writer.Flush();
            payload = payloadStream.ToArray();
        }
        if (payload.Length > MaxPayloadBytes)
            throw new InvalidDataException("VFX runtime payload exceeds the supported size.");

        byte[] hash;
        using (SHA256 sha256 = SHA256.Create()) hash = sha256.ComputeHash(payload);
        using var result = new MemoryStream(checked(12 + payload.Length + HashSize));
        using var envelope = new BinaryWriter(result, Encoding.UTF8, leaveOpen: true);
        envelope.Write(Magic);
        envelope.Write(FormatVersion);
        envelope.Write(payload.Length);
        envelope.Write(payload);
        envelope.Write(hash);
        envelope.Flush();
        return result.ToArray();
    }

    internal static VFXRuntimeAssetData Deserialize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 + HashSize)
            throw new InvalidDataException("VFX runtime payload is truncated.");
        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic) throw new InvalidDataException("VFX runtime payload has an invalid magic value.");
        uint version = reader.ReadUInt32();
        if (version < MinimumSupportedVersion || version > FormatVersion)
            throw new InvalidDataException($"VFX runtime payload version '{version}' is not supported.");
        int payloadLength = reader.ReadInt32();
        if (payloadLength < 0 || payloadLength > MaxPayloadBytes ||
            stream.Length != 12L + payloadLength + HashSize)
            throw new InvalidDataException("VFX runtime payload has an invalid length.");
        byte[] payload = reader.ReadBytes(payloadLength);
        byte[] expectedHash = reader.ReadBytes(HashSize);
        byte[] actualHash;
        using (SHA256 sha256 = SHA256.Create()) actualHash = sha256.ComputeHash(payload);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            throw new InvalidDataException("VFX runtime payload checksum does not match.");

        VFXRuntimeAssetData data;
        using (var payloadStream = new MemoryStream(payload, writable: false))
        using (var payloadReader = new BinaryReader(payloadStream, Encoding.UTF8, leaveOpen: true))
        {
            VFXRuntimeAttributeData[] attributes = ReadAttributes(payloadReader);
            string[] inputEvents = ReadArray(payloadReader, () => ReadString(payloadReader));
            VFXRuntimeInputEventData[] inputEventDispatches = version >= 2
                ? ReadArray(payloadReader, () =>
                {
                    string eventName = ReadString(payloadReader);
                    VFXRuntimeInputEventTargetData[] targets = ReadArray(payloadReader, () =>
                    {
                        long initializeContextId = payloadReader.ReadInt64();
                        string particleSystemName = ReadString(payloadReader);
                        long[] spawnerContextIds = ReadArray(payloadReader, payloadReader.ReadInt64);
                        string[] spawnSystemNames = ReadArray(payloadReader, () => ReadString(payloadReader));
                        VFXRuntimeInitializeKernelData? kernel = version >= 3 && payloadReader.ReadBoolean()
                            ? ReadInitializeKernel(payloadReader, version)
                            : null;
                        return new VFXRuntimeInputEventTargetData(
                            initializeContextId, particleSystemName, spawnerContextIds, spawnSystemNames, kernel);
                    });
                    return new VFXRuntimeInputEventData(eventName, targets);
                })
                : inputEvents.Select(name =>
                    new VFXRuntimeInputEventData(name, Array.Empty<VFXRuntimeInputEventTargetData>())).ToArray();
            VFXRuntimeSystemData[] systems = ReadArray(payloadReader, () =>
            {
                string name = ReadString(payloadReader);
                VFXRuntimeSystemKind kind = ReadEnum<VFXRuntimeSystemKind>(payloadReader);
                uint capacity = payloadReader.ReadUInt32();
                if (version < 12) return new VFXRuntimeSystemData(name, kind, capacity);
                VFXRuntimeSystemData system = new(name, kind, capacity)
                {
                    HasStaticBounds = payloadReader.ReadBoolean(),
                    BoundsInWorldSpace = payloadReader.ReadBoolean(),
                    BoundsCenterX = payloadReader.ReadSingle(),
                    BoundsCenterY = payloadReader.ReadSingle(),
                    BoundsCenterZ = payloadReader.ReadSingle(),
                    BoundsSizeX = payloadReader.ReadSingle(),
                    BoundsSizeY = payloadReader.ReadSingle(),
                    BoundsSizeZ = payloadReader.ReadSingle()
                };
                if (version < 13) return system;
                return system with
                {
                    HasAutomaticBounds = payloadReader.ReadBoolean(),
                    PositionOffsetWords = payloadReader.ReadInt32(),
                    AliveOffsetWords = payloadReader.ReadInt32(),
                    SizeOffsetWords = payloadReader.ReadInt32(),
                    ScaleXOffsetWords = payloadReader.ReadInt32(),
                    ScaleYOffsetWords = payloadReader.ReadInt32(),
                    ScaleZOffsetWords = payloadReader.ReadInt32(),
                    AutomaticBoundsPaddingX = payloadReader.ReadSingle(),
                    AutomaticBoundsPaddingY = payloadReader.ReadSingle(),
                    AutomaticBoundsPaddingZ = payloadReader.ReadSingle()
                };
            });
            VFXRuntimeOutputEventData[] outputEvents = ReadArray(payloadReader, () =>
            {
                string name = ReadString(payloadReader);
                long[] contextIds = ReadArray(payloadReader, payloadReader.ReadInt64);
                VFXRuntimeOutputEventMapping[] mappings = ReadArray(payloadReader, () =>
                    new VFXRuntimeOutputEventMapping(ReadString(payloadReader), payloadReader.ReadInt64()));
                VFXRuntimeAttributeData[] outputAttributes = ReadAttributes(payloadReader);
                int strideWords = payloadReader.ReadInt32();
                return new VFXRuntimeOutputEventData(name, contextIds, mappings, outputAttributes, strideWords);
            });
            VFXRuntimeExposedPropertyData[] exposedProperties = version >= 9
                ? ReadArray(payloadReader, () =>
                    new VFXRuntimeExposedPropertyData(
                        ReadString(payloadReader),
                        ReadEnum<VFXRuntimeValueType>(payloadReader),
                        ReadArray(payloadReader, payloadReader.ReadUInt32)))
                : Array.Empty<VFXRuntimeExposedPropertyData>();
            VFXRuntimeSpawnerProgramData[] spawnerPrograms = version >= 5
                ? ReadArray(payloadReader, () =>
                {
                    long contextId = payloadReader.ReadInt64();
                    string systemName = ReadString(payloadReader);
                    VFXRuntimeSpawnerValueMode loopDurationMode = ReadEnum<VFXRuntimeSpawnerValueMode>(payloadReader);
                    VFXRuntimeSpawnerValueMode loopCountMode = ReadEnum<VFXRuntimeSpawnerValueMode>(payloadReader);
                    VFXRuntimeSpawnerValueMode delayBeforeMode = ReadEnum<VFXRuntimeSpawnerValueMode>(payloadReader);
                    VFXRuntimeSpawnerValueMode delayAfterMode = ReadEnum<VFXRuntimeSpawnerValueMode>(payloadReader);
                    float loopDurationMin = payloadReader.ReadSingle();
                    float loopDurationMax = payloadReader.ReadSingle();
                    double loopCountMin = version >= 6
                        ? payloadReader.ReadDouble()
                        : payloadReader.ReadInt32();
                    double loopCountMax = version >= 6
                        ? payloadReader.ReadDouble()
                        : payloadReader.ReadInt32();
                    float delayBeforeMin = payloadReader.ReadSingle();
                    float delayBeforeMax = payloadReader.ReadSingle();
                    float delayAfterMin = payloadReader.ReadSingle();
                    float delayAfterMax = payloadReader.ReadSingle();
                    int eventStrideWords = version >= 7 ? payloadReader.ReadInt32() : 0;
                    VFXRuntimeSpawnerControlData[] controls = ReadArray(payloadReader, () =>
                        new VFXRuntimeSpawnerControlData(ReadString(payloadReader), payloadReader.ReadInt32()));
                    VFXRuntimeSpawnerOutputData[] outputs = ReadArray(payloadReader, () =>
                    {
                        long initializeContextId = payloadReader.ReadInt64();
                        string particleSystemName = ReadString(payloadReader);
                        VFXRuntimeInitializeKernelData? kernel = payloadReader.ReadBoolean()
                            ? ReadInitializeKernel(payloadReader, version)
                            : null;
                        return new VFXRuntimeSpawnerOutputData(initializeContextId, particleSystemName, kernel);
                    });
                    VFXRuntimeSpawnerBlockData[] blocks = ReadArray(payloadReader, () =>
                    {
                        var block = new VFXRuntimeSpawnerBlockData(
                            payloadReader.ReadInt64(),
                            ReadEnum<VFXRuntimeSpawnerBlockKind>(payloadReader),
                            payloadReader.ReadSingle(),
                            payloadReader.ReadSingle(),
                            payloadReader.ReadSingle(),
                            payloadReader.ReadSingle(),
                            payloadReader.ReadBoolean());
                        if (version < 7) return block;
                        block = block with
                        {
                            TargetOffsetWords = payloadReader.ReadInt32(),
                            TargetValueType = ReadEnum<VFXRuntimeValueType>(payloadReader),
                            RandomMode = ReadEnum<VFXRuntimeInitializeRandomMode>(payloadReader),
                            ValueA = ReadArray(payloadReader, payloadReader.ReadUInt32),
                            ValueB = ReadArray(payloadReader, payloadReader.ReadUInt32)
                        };
                        if (version < 8) return block;
                        string? callbackTypeName = payloadReader.ReadBoolean()
                            ? ReadString(payloadReader)
                            : null;
                        return block with
                        {
                            CallbackTypeName = callbackTypeName,
                            CallbackValues = ReadArray(payloadReader, () =>
                            {
                                var value = new VFXRuntimeSpawnerExpressionValueData(
                                    ReadString(payloadReader),
                                    ReadEnum<VFXRuntimeValueType>(payloadReader),
                                    ReadArray(payloadReader, payloadReader.ReadUInt32));
                                if (version >= 9 && payloadReader.ReadBoolean())
                                    value = value with { SourcePropertyName = ReadString(payloadReader) };
                                if (version >= 10 && payloadReader.ReadBoolean())
                                    value = value with { Expression = ReadExpressionProgram(payloadReader) };
                                return value;
                            })
                        };
                    });
                    return new VFXRuntimeSpawnerProgramData(
                        contextId, systemName, loopDurationMode, loopCountMode,
                        delayBeforeMode, delayAfterMode,
                        loopDurationMin, loopDurationMax, loopCountMin, loopCountMax,
                        delayBeforeMin, delayBeforeMax, delayAfterMin, delayAfterMax,
                        controls, outputs, blocks)
                    {
                        EventStrideWords = eventStrideWords
                    };
                })
                : Array.Empty<VFXRuntimeSpawnerProgramData>();
            VFXRuntimeUpdateKernelData[] updateKernels = version >= 14
                ? ReadArray(payloadReader, () => new VFXRuntimeUpdateKernelData(
                    payloadReader.ReadInt64(),
                    ReadString(payloadReader),
                    payloadReader.ReadUInt32(),
                    payloadReader.ReadInt32(),
                    payloadReader.ReadBoolean(),
                    payloadReader.ReadBoolean(),
                    payloadReader.ReadInt32(),
                    payloadReader.ReadInt32(),
                    ReadArray(payloadReader, () => new VFXRuntimeUpdateOperationData(
                        ReadEnum<VFXRuntimeUpdateOperationKind>(payloadReader),
                        payloadReader.ReadInt32(),
                        payloadReader.ReadInt32(),
                        payloadReader.ReadInt32(),
                        payloadReader.ReadInt32(),
                        payloadReader.ReadInt32(),
                        ReadEnum<VFXRuntimeValueType>(payloadReader),
                        ReadEnum<VFXRuntimeInitializeComposition>(payloadReader),
                        ReadEnum<VFXRuntimeInitializeRandomMode>(payloadReader),
                        payloadReader.ReadBoolean(),
                        ReadArray(payloadReader, payloadReader.ReadUInt32),
                        ReadArray(payloadReader, payloadReader.ReadUInt32),
                        payloadReader.ReadUInt32()))))
                : Array.Empty<VFXRuntimeUpdateKernelData>();
            VFXRuntimePlanarOutputData[] planarOutputs = version >= 15
                ? ReadArray(payloadReader, () => new VFXRuntimePlanarOutputData(
                    payloadReader.ReadInt64(),
                    ReadString(payloadReader),
                    payloadReader.ReadInt32(),
                    payloadReader.ReadInt32(),
                    ReadArray(payloadReader, payloadReader.ReadInt32),
                    payloadReader.ReadInt32(),
                    payloadReader.ReadInt32(),
                    payloadReader.ReadInt32(),
                    payloadReader.ReadBoolean(),
                    payloadReader.ReadInt32(),
                    payloadReader.ReadBoolean(),
                    ReadString(payloadReader),
                    payloadReader.ReadBoolean(),
                    payloadReader.ReadBoolean(),
                    payloadReader.ReadBoolean(),
                    ReadAttributes(payloadReader),
                    payloadReader.ReadInt32()))
                : Array.Empty<VFXRuntimePlanarOutputData>();
            if (payloadStream.Position != payloadStream.Length)
                throw new InvalidDataException("VFX runtime payload contains trailing data.");
            data = new VFXRuntimeAssetData(attributes, inputEvents, inputEventDispatches, systems, outputEvents)
            {
                SpawnerPrograms = spawnerPrograms,
                ExposedProperties = exposedProperties,
                UpdateKernels = updateKernels,
                PlanarOutputs = planarOutputs
            };
        }
        data.Validate();
        return data;
    }

    internal void Validate()
    {
        ValidateAttributes(EventAttributes, requirePackedLayout: true, "event attribute");
        ValidateUniqueNames(InputEvents, "input event");
        if (!InputEvents.SequenceEqual(InputEventDispatches.Select(inputEvent => inputEvent.Name), StringComparer.Ordinal))
            throw new InvalidDataException("VFX input event dispatch order must exactly match the input event list.");
        ValidateUniqueNames(Systems.Select(system => system.Name), "system");
        ValidateUniqueNames(OutputEvents.Select(outputEvent => outputEvent.Name), "output event");
        ValidateUniqueNames(ExposedProperties.Select(property => property.Name), "exposed property");
        foreach (VFXRuntimeExposedPropertyData property in ExposedProperties)
        {
            ValidateName(property.Name, "exposed property");
            if (!Enum.IsDefined(typeof(VFXRuntimeValueType), property.ValueType) ||
                property.DefaultWords.Count != WordCount(property.ValueType) ||
                IsFloating(property.ValueType) && property.DefaultWords.Any(word =>
                    !float.IsFinite(BitConverter.Int32BitsToSingle(unchecked((int)word)))))
                throw new InvalidDataException($"VFX exposed property '{property.Name}' has invalid default data.");
        }
        IReadOnlyDictionary<string, VFXRuntimeExposedPropertyData> exposedPropertiesByName = ExposedProperties
            .ToDictionary(property => property.Name, StringComparer.Ordinal);
        ValidateUniqueNames(
            Systems.Select(system => system.Name).Concat(OutputEvents.Select(outputEvent => outputEvent.Name)),
            "runtime system");
        IReadOnlyDictionary<string, VFXRuntimeValueType> globalAttributeTypes = EventAttributes
            .ToDictionary(attribute => attribute.Name, attribute => attribute.ValueType, StringComparer.Ordinal);
        foreach (VFXRuntimeSystemData system in Systems)
        {
            ValidateName(system.Name, "system");
            if (!Enum.IsDefined(typeof(VFXRuntimeSystemKind), system.Kind))
                throw new InvalidDataException($"VFX system '{system.Name}' has an invalid kind.");
            if (system.Kind is (VFXRuntimeSystemKind.Particle or VFXRuntimeSystemKind.ParticleStrip) && system.Capacity == 0)
                throw new InvalidDataException($"VFX particle system '{system.Name}' requires a positive capacity.");
            if (system.HasStaticBounds &&
                (system.Kind is not (VFXRuntimeSystemKind.Particle or VFXRuntimeSystemKind.ParticleStrip) ||
                 !float.IsFinite(system.BoundsCenterX) || !float.IsFinite(system.BoundsCenterY) ||
                 !float.IsFinite(system.BoundsCenterZ) || !float.IsFinite(system.BoundsSizeX) ||
                 !float.IsFinite(system.BoundsSizeY) || !float.IsFinite(system.BoundsSizeZ) ||
                 system.BoundsSizeX < 0f || system.BoundsSizeY < 0f || system.BoundsSizeZ < 0f))
                throw new InvalidDataException($"VFX system '{system.Name}' has invalid static bounds.");
            if (system.HasStaticBounds && system.HasAutomaticBounds)
                throw new InvalidDataException(
                    $"VFX system '{system.Name}' cannot use static and Automatic bounds together.");
            if (!system.HasStaticBounds &&
                (system.BoundsCenterX != 0f || system.BoundsCenterY != 0f ||
                 system.BoundsCenterZ != 0f || system.BoundsSizeX != 0f || system.BoundsSizeY != 0f ||
                 system.BoundsSizeZ != 0f))
                throw new InvalidDataException($"VFX system '{system.Name}' has bounds data without static bounds.");
            int[] automaticOffsets =
            {
                system.PositionOffsetWords, system.AliveOffsetWords,
                system.SizeOffsetWords, system.ScaleXOffsetWords,
                system.ScaleYOffsetWords, system.ScaleZOffsetWords
            };
            if (system.HasAutomaticBounds &&
                (system.Kind is not (VFXRuntimeSystemKind.Particle or VFXRuntimeSystemKind.ParticleStrip) ||
                 system.PositionOffsetWords < 0 ||
                 automaticOffsets.Skip(1).Any(offset => offset < -1) ||
                 !float.IsFinite(system.AutomaticBoundsPaddingX) ||
                 !float.IsFinite(system.AutomaticBoundsPaddingY) ||
                 !float.IsFinite(system.AutomaticBoundsPaddingZ) ||
                 system.AutomaticBoundsPaddingX < 0f ||
                 system.AutomaticBoundsPaddingY < 0f ||
                 system.AutomaticBoundsPaddingZ < 0f))
                throw new InvalidDataException(
                    $"VFX system '{system.Name}' has invalid Automatic bounds metadata.");
            if (!system.HasAutomaticBounds &&
                (automaticOffsets.Any(offset => offset != -1) ||
                 system.AutomaticBoundsPaddingX != 0f ||
                 system.AutomaticBoundsPaddingY != 0f ||
                 system.AutomaticBoundsPaddingZ != 0f ||
                 !system.HasStaticBounds && system.BoundsInWorldSpace))
                throw new InvalidDataException(
                    $"VFX system '{system.Name}' has Automatic bounds metadata without Automatic bounds.");
        }
        IReadOnlyDictionary<string, VFXRuntimeSystemKind> systemKinds = Systems
            .ToDictionary(system => system.Name, system => system.Kind, StringComparer.Ordinal);
        var updateContextIds = new HashSet<long>();
        foreach (VFXRuntimeUpdateKernelData kernel in UpdateKernels)
        {
            if (kernel.ContextId == 0 || !updateContextIds.Add(kernel.ContextId) ||
                !systemKinds.TryGetValue(kernel.ParticleSystemName, out VFXRuntimeSystemKind kind) ||
                kind is not (VFXRuntimeSystemKind.Particle or VFXRuntimeSystemKind.ParticleStrip))
                throw new InvalidDataException("VFX Update kernels require unique contexts and a particle system target.");
            VFXRuntimeSystemData system = Systems.Single(candidate =>
                string.Equals(candidate.Name, kernel.ParticleSystemName, StringComparison.Ordinal));
            if (kernel.ParticleCapacity != system.Capacity ||
                kernel.AttributeStrideWords <= 0 || kernel.Operations.Count == 0 ||
                kernel.Operations.Count > 65536 ||
                kernel.AliveOffsetWords < -1 || kernel.SeedOffsetWords < -1 ||
                kernel.UsesDeadList && kernel.AliveOffsetWords < 0)
                throw new InvalidDataException(
                    $"VFX Update kernel '{kernel.ContextId}' has invalid particle layout metadata.");
            ValidateUpdateOffset(kernel, kernel.AliveOffsetWords, 1, required: false);
            ValidateUpdateOffset(kernel, kernel.SeedOffsetWords, 1, required: false);
            foreach (VFXRuntimeUpdateOperationData operation in kernel.Operations)
                ValidateUpdateOperation(kernel, operation);
        }
        var planarContextIds = new HashSet<long>();
        foreach (VFXRuntimePlanarOutputData output in PlanarOutputs)
        {
            if (output.ContextId == 0 || !planarContextIds.Add(output.ContextId) ||
                !systemKinds.TryGetValue(output.ParticleSystemName, out VFXRuntimeSystemKind kind) ||
                kind != VFXRuntimeSystemKind.Particle)
                throw new InvalidDataException(
                    "VFX Planar Outputs require unique contexts and a non-strip particle system target.");
            IReadOnlyList<int> expectedPattern = output.PrimitiveType switch
            {
                0 => new[] { 0, 1, 2 },
                1 => new[] { 0, 2, 1, 1, 2, 3 },
                2 => new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 6, 0, 6, 7 },
                _ => Array.Empty<int>()
            };
            int expectedVertices = output.PrimitiveType switch { 0 => 3, 1 => 4, 2 => 8, _ => 0 };
            if (expectedVertices == 0 || output.VerticesPerParticle != expectedVertices ||
                !output.IndexPattern.SequenceEqual(expectedPattern) ||
                output.UvMode is < 0 or > 4 || output.BlendMode is < 0 or > 3 ||
                output.CullMode is < 0 or > 2 || output.ZTest is < 0 or > 6 ||
                string.IsNullOrWhiteSpace(output.RenderQueue) ||
                output.RenderQueue.Length > 64 || output.AttributeStrideWords <= 0)
                throw new InvalidDataException(
                    $"VFX Planar Output '{output.ContextId}' has invalid draw metadata.");
            ValidateAttributes(
                output.Attributes, requirePackedLayout: true,
                $"Planar Output '{output.ContextId}' attribute");
            int requiredStride = output.Attributes.Count == 0
                ? 0
                : output.Attributes.Max(attribute => checked(attribute.OffsetWords + attribute.SizeWords));
            if (requiredStride != output.AttributeStrideWords)
                throw new InvalidDataException(
                    $"VFX Planar Output '{output.ContextId}' has an invalid particle stride.");
            RequirePlanarAttribute(output, "alive", VFXRuntimeValueType.Boolean);
            RequirePlanarAttribute(output, "position", VFXRuntimeValueType.Float3);
            RequirePlanarAttribute(output, "color", VFXRuntimeValueType.Float3);
            RequirePlanarAttribute(output, "alpha", VFXRuntimeValueType.Float);
            RequirePlanarAttribute(output, "size", VFXRuntimeValueType.Float);
            foreach (string scalar in new[]
                     {
                         "scaleX", "scaleY", "scaleZ", "angleX", "angleY", "angleZ",
                         "pivotX", "pivotY", "pivotZ"
                     })
                RequirePlanarAttribute(output, scalar, VFXRuntimeValueType.Float);
            foreach (string vector in new[] { "axisX", "axisY", "axisZ" })
                RequirePlanarAttribute(output, vector, VFXRuntimeValueType.Float3);
            VFXRuntimeUpdateKernelData? update = UpdateKernels.SingleOrDefault(kernel =>
                string.Equals(kernel.ParticleSystemName, output.ParticleSystemName, StringComparison.Ordinal));
            if (update is not null && update.AttributeStrideWords != output.AttributeStrideWords)
                throw new InvalidDataException(
                    $"VFX Planar Output '{output.ContextId}' layout differs from its Update kernel.");
        }
        var spawnerContextIds = new HashSet<long>();
        var spawnerSystemNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (VFXRuntimeSpawnerProgramData program in SpawnerPrograms)
        {
            if (program.ContextId == 0 || !spawnerContextIds.Add(program.ContextId))
                throw new InvalidDataException("VFX Spawner programs require unique non-zero context ids.");
            if (!spawnerSystemNames.Add(program.SystemName) ||
                !systemKinds.TryGetValue(program.SystemName, out VFXRuntimeSystemKind programKind) ||
                programKind != VFXRuntimeSystemKind.Spawn)
                throw new InvalidDataException($"VFX Spawner program targets invalid spawn system '{program.SystemName}'.");
            ValidateSpawnerMode(program.LoopDurationMode, allowInfinite: true, "loop duration");
            ValidateSpawnerMode(program.LoopCountMode, allowInfinite: true, "loop count");
            ValidateSpawnerMode(program.DelayBeforeLoopMode, allowInfinite: false, "delay before loop");
            ValidateSpawnerMode(program.DelayAfterLoopMode, allowInfinite: false, "delay after loop");
            if (!IsFiniteNonNegative(program.LoopDurationMin) ||
                !IsFiniteNonNegative(program.LoopDurationMax) ||
                program.LoopDurationMin > program.LoopDurationMax ||
                !double.IsFinite(program.LoopCountMin) ||
                !double.IsFinite(program.LoopCountMax) ||
                program.LoopCountMin < 0 || program.LoopCountMin > program.LoopCountMax ||
                program.LoopCountMax > int.MaxValue ||
                (program.LoopCountMode == VFXRuntimeSpawnerValueMode.Infinite &&
                 (program.LoopCountMin != 0d || program.LoopCountMax != 0d)) ||
                (program.LoopCountMode == VFXRuntimeSpawnerValueMode.Constant &&
                 (program.LoopCountMin != program.LoopCountMax ||
                  program.LoopCountMin != Math.Truncate(program.LoopCountMin))) ||
                (program.LoopCountMode == VFXRuntimeSpawnerValueMode.Random &&
                 program.LoopCountMax > 2147483520d) ||
                !IsFiniteNonNegative(program.DelayBeforeLoopMin) ||
                !IsFiniteNonNegative(program.DelayBeforeLoopMax) ||
                program.DelayBeforeLoopMin > program.DelayBeforeLoopMax ||
                !IsFiniteNonNegative(program.DelayAfterLoopMin) ||
                !IsFiniteNonNegative(program.DelayAfterLoopMax) ||
                program.DelayAfterLoopMin > program.DelayAfterLoopMax)
                throw new InvalidDataException($"VFX Spawner program '{program.SystemName}' has invalid loop operands.");
            if (program.Controls.Count == 0 || program.Controls.Any(control =>
                    string.IsNullOrEmpty(control.EventName) || control.InputSlotIndex is < 0 or > 1) ||
                program.Controls.Distinct().Count() != program.Controls.Count)
                throw new InvalidDataException($"VFX Spawner program '{program.SystemName}' has invalid controls.");
            bool hasMappedOutputEvent = OutputEvents.Any(outputEvent =>
                outputEvent.BufferMappings.Any(mapping =>
                    mapping.SourceSpawnerContextId == program.ContextId));
            if ((program.Outputs.Count == 0 && !hasMappedOutputEvent) ||
                program.Outputs.Select(output => output.InitializeContextId).Distinct().Count() != program.Outputs.Count)
                throw new InvalidDataException($"VFX Spawner program '{program.SystemName}' requires unique outputs.");
            foreach (VFXRuntimeSpawnerOutputData output in program.Outputs)
            {
                if (output.InitializeContextId == 0 ||
                    !systemKinds.TryGetValue(output.ParticleSystemName, out VFXRuntimeSystemKind outputKind) ||
                    outputKind is not (VFXRuntimeSystemKind.Particle or VFXRuntimeSystemKind.ParticleStrip))
                    throw new InvalidDataException($"VFX Spawner program '{program.SystemName}' has an invalid output.");
                if (output.InitializeKernel is not null)
                {
                    VFXRuntimeSystemData particleSystem = Systems.Single(system =>
                        string.Equals(system.Name, output.ParticleSystemName, StringComparison.Ordinal));
                    ValidateInitializeKernel(
                        output.InitializeKernel, output.InitializeContextId,
                        particleSystem.Capacity, EventAttributes);
                    ValidateAutomaticBoundsLayout(particleSystem, output.InitializeKernel);
                    ValidateUpdateKernelLayout(particleSystem, output.InitializeKernel);
                }
            }
            if (program.Blocks.Select(block => block.BlockId).Distinct().Count() != program.Blocks.Count)
                throw new InvalidDataException($"VFX Spawner program '{program.SystemName}' has duplicate block ids.");
            foreach (VFXRuntimeSpawnerBlockData block in program.Blocks)
            {
                if (block.BlockId == 0 || !Enum.IsDefined(typeof(VFXRuntimeSpawnerBlockKind), block.Kind))
                    throw new InvalidDataException($"VFX Spawner block '{block.BlockId}' has invalid operands.");
                if (block.Kind == VFXRuntimeSpawnerBlockKind.SetAttribute)
                {
                    int wordCount = WordCount(block.TargetValueType);
                    if (block.Periodic || block.ValueMin != 0f || block.ValueMax != 0f ||
                        block.PeriodMin != 0f || block.PeriodMax != 0f ||
                        block.TargetOffsetWords < 0 || program.EventStrideWords != EventAttributes.Sum(field => field.SizeWords) ||
                        block.TargetOffsetWords > program.EventStrideWords - wordCount ||
                        block.ValueA.Count != wordCount || block.ValueB.Count != wordCount ||
                        block.CallbackTypeName is not null || block.CallbackValues.Count != 0 ||
                        !Enum.IsDefined(typeof(VFXRuntimeInitializeRandomMode), block.RandomMode) ||
                        (block.RandomMode != VFXRuntimeInitializeRandomMode.Off &&
                         block.TargetValueType is not (VFXRuntimeValueType.Float or VFXRuntimeValueType.Float2 or
                             VFXRuntimeValueType.Float3 or VFXRuntimeValueType.Float4)))
                        throw new InvalidDataException($"VFX Set SpawnEvent Attribute block '{block.BlockId}' has invalid operands.");
                    VFXRuntimeAttributeData[] coveredFields = EventAttributes.Where(field =>
                            field.OffsetWords < block.TargetOffsetWords + wordCount &&
                            field.OffsetWords + field.SizeWords > block.TargetOffsetWords)
                        .ToArray();
                    bool exactField = coveredFields.Length == 1 &&
                                      coveredFields[0].OffsetWords == block.TargetOffsetWords &&
                                      coveredFields[0].SizeWords == wordCount &&
                                      coveredFields[0].ValueType == block.TargetValueType;
                    bool variadicFloatFields = (block.TargetValueType is VFXRuntimeValueType.Float2 or
                                                   VFXRuntimeValueType.Float3 or VFXRuntimeValueType.Float4) &&
                                               coveredFields.Length == wordCount &&
                                               coveredFields.All(field => field.ValueType == VFXRuntimeValueType.Float &&
                                                   field.SizeWords == 1) &&
                                               coveredFields.Select((field, index) =>
                                                   field.OffsetWords == block.TargetOffsetWords + index).All(value => value);
                    if (!exactField && !variadicFloatFields)
                        throw new InvalidDataException(
                            $"VFX Set SpawnEvent Attribute block '{block.BlockId}' does not match the Event attribute layout.");
                    if ((block.TargetValueType is VFXRuntimeValueType.Float or VFXRuntimeValueType.Float2 or
                        VFXRuntimeValueType.Float3 or VFXRuntimeValueType.Float4) &&
                        block.ValueA.Concat(block.ValueB).Any(word =>
                            !float.IsFinite(BitConverter.Int32BitsToSingle(unchecked((int)word)))))
                        throw new InvalidDataException($"VFX Set SpawnEvent Attribute block '{block.BlockId}' has non-finite operands.");
                    continue;
                }
                if (block.Kind == VFXRuntimeSpawnerBlockKind.CustomCallback)
                {
                    if (block.Periodic || block.ValueMin != 0f || block.ValueMax != 0f ||
                        block.PeriodMin != 0f || block.PeriodMax != 0f ||
                        block.TargetOffsetWords != -1 ||
                        block.TargetValueType != VFXRuntimeValueType.Float ||
                        block.RandomMode != VFXRuntimeInitializeRandomMode.Off ||
                        block.ValueA.Count != 0 || block.ValueB.Count != 0 ||
                        string.IsNullOrWhiteSpace(block.CallbackTypeName) ||
                        block.CallbackValues.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() !=
                            block.CallbackValues.Count)
                        throw new InvalidDataException(
                            $"VFX Custom Spawner Callback block '{block.BlockId}' has invalid operands.");
                    foreach (VFXRuntimeSpawnerExpressionValueData value in block.CallbackValues)
                    {
                        ValidateName(value.Name, "custom spawner callback input");
                        if (!Enum.IsDefined(typeof(VFXRuntimeValueType), value.ValueType) ||
                            value.Words.Count != WordCount(value.ValueType))
                            throw new InvalidDataException(
                                $"VFX Custom Spawner Callback block '{block.BlockId}' input '{value.Name}' has invalid operands.");
                        if ((value.ValueType is VFXRuntimeValueType.Float or VFXRuntimeValueType.Float2 or
                            VFXRuntimeValueType.Float3 or VFXRuntimeValueType.Float4 or VFXRuntimeValueType.Matrix4x4) &&
                            value.Words.Any(word => !float.IsFinite(
                                BitConverter.Int32BitsToSingle(unchecked((int)word)))))
                            throw new InvalidDataException(
                                $"VFX Custom Spawner Callback block '{block.BlockId}' input '{value.Name}' is non-finite.");
                        if (value.SourcePropertyName is not null &&
                            (!exposedPropertiesByName.TryGetValue(value.SourcePropertyName, out VFXRuntimeExposedPropertyData? property) ||
                             property.ValueType != value.ValueType))
                            throw new InvalidDataException(
                                $"VFX Custom Spawner Callback block '{block.BlockId}' input '{value.Name}' references an invalid exposed property.");
                        if (value.SourcePropertyName is not null && value.Expression is not null)
                            throw new InvalidDataException(
                                $"VFX Custom Spawner Callback block '{block.BlockId}' input '{value.Name}' has multiple runtime sources.");
                        if (value.Expression is not null)
                        {
                            ValidateExpressionProgram(value.Expression, exposedPropertiesByName);
                            if (value.Expression.ResultType != value.ValueType)
                                throw new InvalidDataException(
                                    $"VFX Custom Spawner Callback block '{block.BlockId}' input '{value.Name}' expression type does not match.");
                        }
                    }
                    continue;
                }
                if (block.CallbackTypeName is not null || block.CallbackValues.Count != 0)
                    throw new InvalidDataException(
                        $"VFX Spawner block '{block.BlockId}' has unexpected callback operands.");
                if (!IsFiniteNonNegative(block.ValueMin) || !IsFiniteNonNegative(block.ValueMax) ||
                    !IsFiniteNonNegative(block.PeriodMin) || !IsFiniteNonNegative(block.PeriodMax) ||
                    block.ValueMin > block.ValueMax || block.PeriodMin > block.PeriodMax ||
                    block.TargetOffsetWords != -1 || block.RandomMode != VFXRuntimeInitializeRandomMode.Off ||
                    block.ValueA.Count != 0 || block.ValueB.Count != 0)
                    throw new InvalidDataException($"VFX Spawner block '{block.BlockId}' has invalid operands.");
                if (block.Kind == VFXRuntimeSpawnerBlockKind.ConstantRate &&
                    (block.ValueMin != block.ValueMax || block.PeriodMin != 0f ||
                     block.PeriodMax != 0f || block.Periodic))
                    throw new InvalidDataException($"VFX Constant Rate block '{block.BlockId}' has invalid operands.");
                if (block.Kind == VFXRuntimeSpawnerBlockKind.VariableRate &&
                    (block.Periodic || block.PeriodMax <= 0f))
                    throw new InvalidDataException($"VFX Variable Rate block '{block.BlockId}' has invalid period operands.");
            }
        }
        foreach (VFXRuntimeInputEventData inputEvent in InputEventDispatches)
        {
            ValidateName(inputEvent.Name, "input event dispatch");
            var targetKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (VFXRuntimeInputEventTargetData target in inputEvent.Targets)
            {
                if (target.InitializeContextId == 0)
                    throw new InvalidDataException(
                        $"VFX input event '{inputEvent.Name}' has an invalid Initialize context id.");
                if (!systemKinds.TryGetValue(target.ParticleSystemName, out VFXRuntimeSystemKind particleKind) ||
                    particleKind is not (VFXRuntimeSystemKind.Particle or VFXRuntimeSystemKind.ParticleStrip))
                    throw new InvalidDataException(
                        $"VFX input event '{inputEvent.Name}' targets unknown particle system '{target.ParticleSystemName}'.");
                if (target.SpawnerContextIds.Count != target.SpawnSystemNames.Count ||
                    target.SpawnerContextIds.Any(contextId => contextId == 0) ||
                    target.SpawnerContextIds.Distinct().Count() != target.SpawnerContextIds.Count)
                    throw new InvalidDataException(
                        $"VFX input event '{inputEvent.Name}' has an invalid Spawner context path.");
                foreach (string spawnSystemName in target.SpawnSystemNames)
                {
                    if (!systemKinds.TryGetValue(spawnSystemName, out VFXRuntimeSystemKind spawnKind) ||
                        spawnKind != VFXRuntimeSystemKind.Spawn)
                        throw new InvalidDataException(
                            $"VFX input event '{inputEvent.Name}' targets unknown spawn system '{spawnSystemName}'.");
                }
                string targetKey = target.InitializeContextId + ":" +
                    string.Join(",", target.SpawnerContextIds.Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                if (!targetKeys.Add(targetKey))
                    throw new InvalidDataException($"VFX input event '{inputEvent.Name}' contains a duplicate dispatch target.");
                if (target.InitializeKernel is not null)
                {
                    VFXRuntimeSystemData particleSystem = Systems.Single(system =>
                        string.Equals(system.Name, target.ParticleSystemName, StringComparison.Ordinal));
                    ValidateInitializeKernel(
                        target.InitializeKernel, target.InitializeContextId,
                        particleSystem.Capacity, EventAttributes);
                    ValidateAutomaticBoundsLayout(particleSystem, target.InitializeKernel);
                    ValidateUpdateKernelLayout(particleSystem, target.InitializeKernel);
                }
            }
        }
        foreach (VFXRuntimeOutputEventData outputEvent in OutputEvents)
        {
            ValidateName(outputEvent.Name, "output event");
            if (outputEvent.ContextIds.Count == 0 || outputEvent.ContextIds.Distinct().Count() != outputEvent.ContextIds.Count)
                throw new InvalidDataException($"VFX output event '{outputEvent.Name}' requires unique context ids.");
            if (outputEvent.BufferMappings.Any(mapping => string.IsNullOrEmpty(mapping.Name)))
                throw new InvalidDataException($"VFX output event '{outputEvent.Name}' has an invalid buffer mapping.");
            if (outputEvent.BufferMappings.Select(mapping => mapping.SourceSpawnerContextId).Distinct().Count() !=
                outputEvent.BufferMappings.Count)
                throw new InvalidDataException($"VFX output event '{outputEvent.Name}' has duplicate spawner mappings.");
            ValidateAttributes(outputEvent.Attributes, requirePackedLayout: false, $"output event '{outputEvent.Name}' attribute");
            foreach (VFXRuntimeAttributeData attribute in outputEvent.Attributes)
            {
                if (!globalAttributeTypes.TryGetValue(attribute.Name, out VFXRuntimeValueType globalType) ||
                    globalType != attribute.ValueType)
                    throw new InvalidDataException(
                        $"VFX output event '{outputEvent.Name}' attribute '{attribute.Name}' is absent from the global event schema.");
            }
            int requiredStride = outputEvent.Attributes.Count == 0
                ? 0
                : outputEvent.Attributes.Max(attribute => checked(attribute.OffsetWords + attribute.SizeWords));
            if (outputEvent.StrideWords < requiredStride || outputEvent.StrideWords < 0)
                throw new InvalidDataException($"VFX output event '{outputEvent.Name}' has an invalid stride.");
        }
    }

    private static void ValidateSpawnerMode(
        VFXRuntimeSpawnerValueMode mode, bool allowInfinite, string category)
    {
        if (!Enum.IsDefined(typeof(VFXRuntimeSpawnerValueMode), mode) ||
            (mode == VFXRuntimeSpawnerValueMode.Disabled && category.StartsWith("loop", StringComparison.Ordinal)) ||
            (mode == VFXRuntimeSpawnerValueMode.Infinite && !allowInfinite))
            throw new InvalidDataException($"VFX Spawner {category} mode is invalid.");
    }

    private static void RequirePlanarAttribute(
        VFXRuntimePlanarOutputData output,
        string name,
        VFXRuntimeValueType type)
    {
        VFXRuntimeAttributeData[] matches = output.Attributes
            .Where(attribute => string.Equals(attribute.Name, name, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 || matches[0].ValueType != type ||
            matches[0].SizeWords != WordCount(type))
            throw new InvalidDataException(
                $"VFX Planar Output '{output.ContextId}' requires {type} attribute '{name}'.");
    }

    private static bool IsFloating(VFXRuntimeValueType type)
        => type is VFXRuntimeValueType.Float or VFXRuntimeValueType.Float2 or
            VFXRuntimeValueType.Float3 or VFXRuntimeValueType.Float4 or VFXRuntimeValueType.Matrix4x4;

    private static void ValidateExpressionProgram(
        VFXRuntimeExpressionProgramData program,
        IReadOnlyDictionary<string, VFXRuntimeExposedPropertyData> exposedProperties)
    {
        if (!Enum.IsDefined(typeof(VFXRuntimeValueType), program.ResultType) ||
            program.Instructions.Count == 0 || program.ResultIndex < 0 ||
            program.ResultIndex >= program.Instructions.Count ||
            program.Instructions[program.ResultIndex].ValueType != program.ResultType)
            throw new InvalidDataException("VFX runtime expression has an invalid result contract.");
        for (int index = 0; index < program.Instructions.Count; index++)
        {
            VFXRuntimeExpressionInstructionData instruction = program.Instructions[index];
            if (!Enum.IsDefined(typeof(VFXRuntimeExpressionOperation), instruction.Operation) ||
                !Enum.IsDefined(typeof(VFXRuntimeValueType), instruction.ValueType))
                throw new InvalidDataException("VFX runtime expression contains an invalid opcode or type.");
            bool floating = IsFloating(instruction.ValueType);
            switch (instruction.Operation)
            {
                case VFXRuntimeExpressionOperation.Constant:
                    if (instruction.InputA != -1 || instruction.InputB != -1 ||
                        instruction.PropertyName is not null ||
                        instruction.ConstantWords.Count != WordCount(instruction.ValueType) ||
                        floating && instruction.ConstantWords.Any(word =>
                            !float.IsFinite(BitConverter.Int32BitsToSingle(unchecked((int)word)))))
                        throw new InvalidDataException("VFX runtime expression contains an invalid constant.");
                    break;
                case VFXRuntimeExpressionOperation.ExposedProperty:
                    if (instruction.InputA != -1 || instruction.InputB != -1 ||
                        instruction.ConstantWords.Count != 0 || string.IsNullOrEmpty(instruction.PropertyName) ||
                        !exposedProperties.TryGetValue(instruction.PropertyName, out VFXRuntimeExposedPropertyData? property) ||
                        property.ValueType != instruction.ValueType)
                        throw new InvalidDataException("VFX runtime expression contains an invalid exposed property reference.");
                    break;
                case VFXRuntimeExpressionOperation.Add:
                case VFXRuntimeExpressionOperation.Subtract:
                case VFXRuntimeExpressionOperation.Multiply:
                    if (!IsArithmetic(instruction.ValueType) ||
                        !ValidExpressionInput(program.Instructions, instruction.InputA, index, instruction.ValueType) ||
                        !ValidExpressionInput(program.Instructions, instruction.InputB, index, instruction.ValueType) ||
                        instruction.ConstantWords.Count != 0 || instruction.PropertyName is not null)
                        throw new InvalidDataException("VFX runtime expression contains an invalid binary operation.");
                    break;
                case VFXRuntimeExpressionOperation.OneMinus:
                    if (instruction.ValueType is not (VFXRuntimeValueType.Float or VFXRuntimeValueType.Float2 or
                            VFXRuntimeValueType.Float3 or VFXRuntimeValueType.Float4) ||
                        !ValidExpressionInput(program.Instructions, instruction.InputA, index, instruction.ValueType) ||
                        instruction.InputB != -1 || instruction.ConstantWords.Count != 0 ||
                        instruction.PropertyName is not null)
                        throw new InvalidDataException("VFX runtime expression contains an invalid OneMinus operation.");
                    break;
                case VFXRuntimeExpressionOperation.VfxDeltaTime:
                case VFXRuntimeExpressionOperation.VfxUnscaledDeltaTime:
                case VFXRuntimeExpressionOperation.VfxTotalTime:
                case VFXRuntimeExpressionOperation.VfxPlayRate:
                case VFXRuntimeExpressionOperation.VfxManagerFixedTimeStep:
                case VFXRuntimeExpressionOperation.VfxManagerMaxDeltaTime:
                case VFXRuntimeExpressionOperation.GameDeltaTime:
                case VFXRuntimeExpressionOperation.GameUnscaledDeltaTime:
                case VFXRuntimeExpressionOperation.GameSmoothDeltaTime:
                case VFXRuntimeExpressionOperation.GameTotalTime:
                case VFXRuntimeExpressionOperation.GameUnscaledTotalTime:
                case VFXRuntimeExpressionOperation.GameTotalTimeSinceSceneLoad:
                case VFXRuntimeExpressionOperation.GameTimeScale:
                    ValidateBuiltInInstruction(instruction, VFXRuntimeValueType.Float);
                    break;
                case VFXRuntimeExpressionOperation.VfxFrameIndex:
                case VFXRuntimeExpressionOperation.SystemSeed:
                    ValidateBuiltInInstruction(instruction, VFXRuntimeValueType.UInt32);
                    break;
                case VFXRuntimeExpressionOperation.LocalToWorld:
                case VFXRuntimeExpressionOperation.WorldToLocal:
                    ValidateBuiltInInstruction(instruction, VFXRuntimeValueType.Matrix4x4);
                    break;
            }
        }
    }

    private static void ValidateBuiltInInstruction(
        VFXRuntimeExpressionInstructionData instruction,
        VFXRuntimeValueType expectedType)
    {
        if (instruction.ValueType != expectedType || instruction.InputA != -1 ||
            instruction.InputB != -1 || instruction.ConstantWords.Count != 0 ||
            instruction.PropertyName is not null)
            throw new InvalidDataException("VFX runtime expression contains an invalid built-in value.");
    }

    private static bool ValidExpressionInput(
        IReadOnlyList<VFXRuntimeExpressionInstructionData> instructions,
        int input,
        int current,
        VFXRuntimeValueType type)
        => input >= 0 && input < current && instructions[input].ValueType == type;

    private static bool IsArithmetic(VFXRuntimeValueType type)
        => type is VFXRuntimeValueType.UInt32 or VFXRuntimeValueType.Int32 or
            VFXRuntimeValueType.Float or VFXRuntimeValueType.Float2 or
            VFXRuntimeValueType.Float3 or VFXRuntimeValueType.Float4;

    private static bool IsFiniteNonNegative(float value)
        => float.IsFinite(value) && value >= 0f;

    private static void ValidateAttributes(
        IReadOnlyList<VFXRuntimeAttributeData> attributes,
        bool requirePackedLayout,
        string category)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        int expectedOffset = 0;
        int lastEnd = 0;
        foreach (VFXRuntimeAttributeData attribute in attributes)
        {
            ValidateName(attribute.Name, category);
            if (!names.Add(attribute.Name)) throw new InvalidDataException($"Duplicate VFX {category} '{attribute.Name}'.");
            if (!Enum.IsDefined(typeof(VFXRuntimeValueType), attribute.ValueType))
                throw new InvalidDataException($"VFX {category} '{attribute.Name}' has an invalid value type.");
            int expectedSize = WordCount(attribute.ValueType);
            if (attribute.SizeWords != expectedSize || attribute.OffsetWords < lastEnd)
                throw new InvalidDataException($"VFX {category} '{attribute.Name}' has an invalid layout.");
            if (requirePackedLayout && attribute.OffsetWords != expectedOffset)
                throw new InvalidDataException($"VFX {category} '{attribute.Name}' is not tightly packed.");
            lastEnd = checked(attribute.OffsetWords + attribute.SizeWords);
            expectedOffset = lastEnd;
        }
    }

    internal static int WordCount(VFXRuntimeValueType valueType) => valueType switch
    {
        VFXRuntimeValueType.Float2 => 2,
        VFXRuntimeValueType.Float3 => 3,
        VFXRuntimeValueType.Float4 => 4,
        VFXRuntimeValueType.Matrix4x4 => 16,
        VFXRuntimeValueType.Boolean or VFXRuntimeValueType.UInt32 or VFXRuntimeValueType.Int32 or VFXRuntimeValueType.Float => 1,
        _ => throw new InvalidDataException($"Unknown VFX runtime value type '{valueType}'.")
    };

    internal static Type SystemType(VFXRuntimeValueType valueType) => valueType switch
    {
        VFXRuntimeValueType.Boolean => typeof(bool),
        VFXRuntimeValueType.UInt32 => typeof(uint),
        VFXRuntimeValueType.Int32 => typeof(int),
        VFXRuntimeValueType.Float => typeof(float),
        VFXRuntimeValueType.Float2 => typeof(Vector2),
        VFXRuntimeValueType.Float3 => typeof(Vector3),
        VFXRuntimeValueType.Float4 => typeof(Vector4),
        VFXRuntimeValueType.Matrix4x4 => typeof(Matrix4x4),
        _ => throw new InvalidDataException($"Unknown VFX runtime value type '{valueType}'.")
    };

    internal static VFXRuntimeValueType RuntimeType(Type type)
    {
        if (type == typeof(bool)) return VFXRuntimeValueType.Boolean;
        if (type == typeof(uint)) return VFXRuntimeValueType.UInt32;
        if (type == typeof(int)) return VFXRuntimeValueType.Int32;
        if (type == typeof(float)) return VFXRuntimeValueType.Float;
        if (type == typeof(Vector2)) return VFXRuntimeValueType.Float2;
        if (type == typeof(Vector3)) return VFXRuntimeValueType.Float3;
        if (type == typeof(Vector4)) return VFXRuntimeValueType.Float4;
        if (type == typeof(Matrix4x4)) return VFXRuntimeValueType.Matrix4x4;
        throw new InvalidDataException($"Unknown VFX runtime system type '{type}'.");
    }

    private static void WriteExpressionProgram(
        BinaryWriter writer,
        VFXRuntimeExpressionProgramData program)
    {
        writer.Write((byte)program.ResultType);
        writer.Write(program.ResultIndex);
        WriteCount(writer, program.Instructions.Count);
        foreach (VFXRuntimeExpressionInstructionData instruction in program.Instructions)
        {
            writer.Write((byte)instruction.Operation);
            writer.Write((byte)instruction.ValueType);
            writer.Write(instruction.InputA);
            writer.Write(instruction.InputB);
            WriteCount(writer, instruction.ConstantWords.Count);
            foreach (uint word in instruction.ConstantWords) writer.Write(word);
            writer.Write(instruction.PropertyName is not null);
            if (instruction.PropertyName is not null) WriteString(writer, instruction.PropertyName);
        }
    }

    private static VFXRuntimeExpressionProgramData ReadExpressionProgram(BinaryReader reader)
    {
        VFXRuntimeValueType resultType = ReadEnum<VFXRuntimeValueType>(reader);
        int resultIndex = reader.ReadInt32();
        VFXRuntimeExpressionInstructionData[] instructions = ReadArray(reader, () =>
            new VFXRuntimeExpressionInstructionData(
                ReadEnum<VFXRuntimeExpressionOperation>(reader),
                ReadEnum<VFXRuntimeValueType>(reader),
                reader.ReadInt32(),
                reader.ReadInt32(),
                ReadArray(reader, reader.ReadUInt32),
                reader.ReadBoolean() ? ReadString(reader) : null));
        return new VFXRuntimeExpressionProgramData(resultType, resultIndex, instructions);
    }

    private static void WriteAttributes(BinaryWriter writer, IReadOnlyList<VFXRuntimeAttributeData> attributes)
    {
        WriteCount(writer, attributes.Count);
        foreach (VFXRuntimeAttributeData attribute in attributes)
        {
            WriteString(writer, attribute.Name);
            writer.Write((byte)attribute.ValueType);
            writer.Write(attribute.OffsetWords);
            writer.Write(attribute.SizeWords);
        }
    }

    private static void WriteInitializeKernel(BinaryWriter writer, VFXRuntimeInitializeKernelData kernel)
    {
        writer.Write(kernel.ContextId);
        writer.Write(kernel.ParticleCapacity);
        writer.Write(kernel.AttributeStrideWords);
        writer.Write(kernel.SourceStrideWords);
        writer.Write(kernel.SpawnCountSourceOffsetWords);
        writer.Write(kernel.UsesDeadList);
        WriteCount(writer, kernel.Attributes.Count);
        foreach (VFXRuntimeInitializeAttributeData attribute in kernel.Attributes)
        {
            WriteString(writer, attribute.Layout.Name);
            writer.Write((byte)attribute.Layout.ValueType);
            writer.Write(attribute.Layout.OffsetWords);
            writer.Write(attribute.Layout.SizeWords);
            WriteWords(writer, attribute.DefaultWords);
        }
        WriteCount(writer, kernel.Operations.Count);
        foreach (VFXRuntimeInitializeOperationData operation in kernel.Operations)
        {
            writer.Write(operation.TargetOffsetWords);
            writer.Write(operation.SourceOffsetWords);
            writer.Write((byte)operation.ValueType);
            writer.Write((byte)operation.ValueSource);
            writer.Write((byte)operation.Composition);
            writer.Write((byte)operation.RandomMode);
            WriteWords(writer, operation.ValueA);
            WriteWords(writer, operation.ValueB);
            writer.Write(operation.BlendFactorBits);
        }
    }

    private static VFXRuntimeInitializeKernelData ReadInitializeKernel(BinaryReader reader, uint version)
    {
        long contextId = reader.ReadInt64();
        uint particleCapacity = reader.ReadUInt32();
        int attributeStrideWords = reader.ReadInt32();
        int sourceStrideWords = reader.ReadInt32();
        int spawnCountSourceOffsetWords = version >= 4 ? reader.ReadInt32() : -1;
        bool usesDeadList = reader.ReadBoolean();
        VFXRuntimeInitializeAttributeData[] attributes = ReadArray(reader, () =>
        {
            var layout = new VFXRuntimeAttributeData(
                ReadString(reader),
                ReadEnum<VFXRuntimeValueType>(reader),
                reader.ReadInt32(),
                reader.ReadInt32());
            return new VFXRuntimeInitializeAttributeData(layout, ReadWords(reader));
        });
        VFXRuntimeInitializeOperationData[] operations = ReadArray(reader, () =>
            new VFXRuntimeInitializeOperationData(
                reader.ReadInt32(),
                reader.ReadInt32(),
                ReadEnum<VFXRuntimeValueType>(reader),
                ReadEnum<VFXRuntimeInitializeValueSource>(reader),
                ReadEnum<VFXRuntimeInitializeComposition>(reader),
                ReadEnum<VFXRuntimeInitializeRandomMode>(reader),
                ReadWords(reader),
                ReadWords(reader),
                reader.ReadUInt32()));
        return new VFXRuntimeInitializeKernelData(
            contextId, particleCapacity, attributeStrideWords, sourceStrideWords,
            usesDeadList, attributes, operations, spawnCountSourceOffsetWords);
    }

    private static void WriteWords(BinaryWriter writer, IReadOnlyList<uint> words)
    {
        WriteCount(writer, words.Count);
        foreach (uint word in words) writer.Write(word);
    }

    private static uint[] ReadWords(BinaryReader reader)
        => ReadArray(reader, reader.ReadUInt32);

    private static void ValidateInitializeKernel(
        VFXRuntimeInitializeKernelData kernel,
        long expectedContextId,
        uint expectedCapacity,
        IReadOnlyList<VFXRuntimeAttributeData> sourceAttributes)
    {
        if (kernel.ContextId != expectedContextId || kernel.ContextId == 0)
            throw new InvalidDataException("VFX Initialize kernel context does not match its dispatch target.");
        if (kernel.ParticleCapacity != expectedCapacity || kernel.ParticleCapacity == 0 ||
            kernel.ParticleCapacity > int.MaxValue)
            throw new InvalidDataException("VFX Initialize kernel capacity does not match its particle system.");
        if (kernel.AttributeStrideWords <= 0 || kernel.SourceStrideWords <= 0)
            throw new InvalidDataException("VFX Initialize kernel has an invalid buffer stride.");
        if (kernel.SpawnCountSourceOffsetWords < -1 ||
            kernel.SpawnCountSourceOffsetWords >= kernel.SourceStrideWords)
            throw new InvalidDataException("VFX Initialize kernel has an invalid spawnCount source offset.");
        if (kernel.SpawnCountSourceOffsetWords >= 0 &&
            !sourceAttributes.Any(attribute =>
                string.Equals(attribute.Name, "spawnCount", StringComparison.Ordinal) &&
                attribute.ValueType == VFXRuntimeValueType.Float &&
                attribute.SizeWords == 1 &&
                attribute.OffsetWords == kernel.SpawnCountSourceOffsetWords))
            throw new InvalidDataException(
                "VFX Initialize kernel spawnCount source does not match the global Event attribute layout.");
        var byOffset = new Dictionary<int, VFXRuntimeInitializeAttributeData>();
        int lastEnd = 0;
        foreach (VFXRuntimeInitializeAttributeData attribute in kernel.Attributes)
        {
            VFXRuntimeAttributeData layout = attribute.Layout;
            ValidateName(layout.Name, "Initialize attribute");
            int wordCount = WordCount(layout.ValueType);
            if (layout.SizeWords != wordCount || layout.OffsetWords != lastEnd ||
                attribute.DefaultWords.Count != wordCount || !byOffset.TryAdd(layout.OffsetWords, attribute))
                throw new InvalidDataException($"VFX Initialize attribute '{layout.Name}' has an invalid layout or default value.");
            lastEnd = checked(layout.OffsetWords + layout.SizeWords);
        }
        if (kernel.Attributes.Count == 0 || lastEnd != kernel.AttributeStrideWords)
            throw new InvalidDataException("VFX Initialize attribute layout does not fill its declared stride.");
        bool hasAlive = kernel.Attributes.Any(attribute =>
            string.Equals(attribute.Layout.Name, "alive", StringComparison.Ordinal) &&
            attribute.Layout.ValueType == VFXRuntimeValueType.Boolean);
        if (kernel.UsesDeadList && !hasAlive)
            throw new InvalidDataException("VFX Initialize dead-list execution requires a Boolean alive attribute.");
        foreach (VFXRuntimeInitializeOperationData operation in kernel.Operations)
        {
            if (!byOffset.TryGetValue(operation.TargetOffsetWords, out VFXRuntimeInitializeAttributeData? target) ||
                target.Layout.ValueType != operation.ValueType ||
                !Enum.IsDefined(typeof(VFXRuntimeInitializeValueSource), operation.ValueSource) ||
                !Enum.IsDefined(typeof(VFXRuntimeInitializeComposition), operation.Composition) ||
                !Enum.IsDefined(typeof(VFXRuntimeInitializeRandomMode), operation.RandomMode))
                throw new InvalidDataException("VFX Initialize operation has an invalid target or opcode.");
            int wordCount = WordCount(operation.ValueType);
            if (operation.ValueSource == VFXRuntimeInitializeValueSource.Source)
            {
                if (operation.RandomMode != VFXRuntimeInitializeRandomMode.Off ||
                    operation.SourceOffsetWords < 0 ||
                    checked(operation.SourceOffsetWords + wordCount) > kernel.SourceStrideWords ||
                    operation.ValueA.Count != 0 || operation.ValueB.Count != 0)
                    throw new InvalidDataException("VFX Initialize source operation has an invalid binding.");
            }
            else if (operation.ValueSource == VFXRuntimeInitializeValueSource.Constant &&
                     (operation.SourceOffsetWords != -1 || operation.ValueA.Count != wordCount ||
                     (operation.RandomMode == VFXRuntimeInitializeRandomMode.Off
                         ? operation.ValueB.Count != 0
                         : operation.ValueB.Count != wordCount)))
            {
                throw new InvalidDataException("VFX Initialize constant operation has invalid operands.");
            }
            else if ((operation.ValueSource is VFXRuntimeInitializeValueSource.ParticleId or
                          VFXRuntimeInitializeValueSource.Seed or
                          VFXRuntimeInitializeValueSource.SpawnIndex) &&
                     (operation.ValueType != VFXRuntimeValueType.UInt32 ||
                      operation.SourceOffsetWords != -1 || operation.RandomMode != VFXRuntimeInitializeRandomMode.Off ||
                      operation.Composition != VFXRuntimeInitializeComposition.Overwrite ||
                      operation.ValueA.Count != 0 || operation.ValueB.Count != 0))
            {
                throw new InvalidDataException("VFX Initialize system-value operation has invalid operands.");
            }
            if (operation.RandomMode != VFXRuntimeInitializeRandomMode.Off &&
                operation.ValueType is not (VFXRuntimeValueType.Float or VFXRuntimeValueType.Float2 or
                    VFXRuntimeValueType.Float3 or VFXRuntimeValueType.Float4))
                throw new InvalidDataException("VFX Initialize random interpolation requires floating-point operands.");
        }
    }

    private static void ValidateUpdateOffset(
        VFXRuntimeUpdateKernelData kernel,
        int offsetWords,
        int componentCount,
        bool required)
    {
        if (offsetWords == -1 && !required) return;
        if (offsetWords < 0 || componentCount <= 0 ||
            (long)offsetWords + componentCount > kernel.AttributeStrideWords)
            throw new InvalidDataException(
                $"VFX Update kernel '{kernel.ContextId}' contains an invalid particle attribute offset.");
    }

    private static void ValidateUpdateOperation(
        VFXRuntimeUpdateKernelData kernel,
        VFXRuntimeUpdateOperationData operation)
    {
        if (!Enum.IsDefined(typeof(VFXRuntimeUpdateOperationKind), operation.Kind) ||
            !Enum.IsDefined(typeof(VFXRuntimeValueType), operation.ValueType) ||
            !Enum.IsDefined(typeof(VFXRuntimeInitializeComposition), operation.Composition) ||
            !Enum.IsDefined(typeof(VFXRuntimeInitializeRandomMode), operation.RandomMode) ||
            operation.ValueType == VFXRuntimeValueType.Matrix4x4)
            throw new InvalidDataException(
                $"VFX Update kernel '{kernel.ContextId}' contains an invalid operation enum.");
        int wordCount = WordCount(operation.ValueType);
        ValidateUpdateOffset(kernel, operation.TargetOffsetWords, wordCount, required: true);
        bool noComposition = operation.Composition == VFXRuntimeInitializeComposition.Overwrite;
        bool noRandom = operation.RandomMode == VFXRuntimeInitializeRandomMode.Off;
        bool finiteBlend = float.IsFinite(BitConverter.Int32BitsToSingle(
            unchecked((int)operation.BlendFactorBits)));
        switch (operation.Kind)
        {
            case VFXRuntimeUpdateOperationKind.SetAttribute:
                if (operation.ReadSourceSnapshot)
                {
                    ValidateUpdateOffset(kernel, operation.SourceAOffsetWords, wordCount, required: true);
                    if (!noRandom || operation.ValueA.Count != 0 || operation.ValueB.Count != 0)
                        Invalid("source-snapshot SetAttribute operands");
                }
                else
                {
                    if (operation.SourceAOffsetWords != -1 || operation.ValueA.Count != wordCount ||
                        operation.ValueB.Count != (noRandom ? 0 : wordCount))
                        Invalid("constant SetAttribute operands");
                    ValidateFiniteUpdateWords(operation.ValueType, operation.ValueA);
                    ValidateFiniteUpdateWords(operation.ValueType, operation.ValueB);
                }
                if (operation.SourceBOffsetWords != -1 || operation.AuxiliaryOffset0Words != -1 ||
                    operation.AuxiliaryOffset1Words != -1 ||
                    (!noRandom && operation.ValueType < VFXRuntimeValueType.Float) || !finiteBlend)
                    Invalid("SetAttribute metadata");
                break;
            case VFXRuntimeUpdateOperationKind.CopyAttribute:
                ValidateUpdateOffset(kernel, operation.SourceAOffsetWords, wordCount, required: true);
                RequirePlain("CopyAttribute", operation.SourceBOffsetWords, operation.AuxiliaryOffset0Words,
                    operation.AuxiliaryOffset1Words, operation.ValueA, operation.ValueB);
                break;
            case VFXRuntimeUpdateOperationKind.Integrate:
                if (operation.ValueType < VFXRuntimeValueType.Float) Invalid("Integrate value type");
                if (operation.SourceAOffsetWords >= 0)
                {
                    ValidateUpdateOffset(kernel, operation.SourceAOffsetWords, wordCount, required: true);
                    if (operation.ValueA.Count != 0) Invalid("Integrate source operands");
                }
                else
                {
                    if (operation.ValueA.Count != wordCount) Invalid("Integrate constant operands");
                    ValidateFiniteUpdateWords(operation.ValueType, operation.ValueA);
                }
                RequirePlain("Integrate", operation.SourceBOffsetWords, operation.AuxiliaryOffset0Words,
                    operation.AuxiliaryOffset1Words, Array.Empty<uint>(), operation.ValueB);
                break;
            case VFXRuntimeUpdateOperationKind.Reap:
                if (operation.ValueType != VFXRuntimeValueType.Boolean ||
                    operation.TargetOffsetWords != kernel.AliveOffsetWords)
                    Invalid("Reap target");
                ValidateUpdateOffset(kernel, operation.SourceAOffsetWords, 1, required: true);
                ValidateUpdateOffset(kernel, operation.SourceBOffsetWords, 1, required: true);
                RequirePlain("Reap", -1, operation.AuxiliaryOffset0Words,
                    operation.AuxiliaryOffset1Words, operation.ValueA, operation.ValueB);
                break;
            case VFXRuntimeUpdateOperationKind.Force:
                RequireFloat3Target("Force");
                ValidateUpdateOffset(kernel, operation.SourceAOffsetWords, 1, required: true);
                if (operation.ValueA.Count != 3 || operation.ValueB.Count != 0)
                    Invalid("Force operands");
                ValidateFiniteUpdateWords(VFXRuntimeValueType.Float3, operation.ValueA);
                RequireUnusedOffsets(operation.SourceBOffsetWords, operation.AuxiliaryOffset0Words,
                    operation.AuxiliaryOffset1Words, "Force");
                break;
            case VFXRuntimeUpdateOperationKind.RelativeForce:
                RequireFloat3Target("RelativeForce");
                ValidateUpdateOffset(kernel, operation.SourceAOffsetWords, 1, required: true);
                if (operation.ValueA.Count != 3 || operation.ValueB.Count != 1)
                    Invalid("RelativeForce operands");
                ValidateFiniteUpdateWords(VFXRuntimeValueType.Float3, operation.ValueA);
                ValidateFiniteUpdateWords(VFXRuntimeValueType.Float, operation.ValueB);
                RequireUnusedOffsets(operation.SourceBOffsetWords, operation.AuxiliaryOffset0Words,
                    operation.AuxiliaryOffset1Words, "RelativeForce");
                break;
            case VFXRuntimeUpdateOperationKind.Drag:
                RequireFloat3Target("Drag");
                ValidateUpdateOffset(kernel, operation.SourceAOffsetWords, 1, required: true);
                if (operation.ValueA.Count != 1 || operation.ValueB.Count != 0)
                    Invalid("Drag operands");
                ValidateFiniteUpdateWords(VFXRuntimeValueType.Float, operation.ValueA);
                if (operation.SourceBOffsetWords >= 0)
                {
                    ValidateUpdateOffset(kernel, operation.SourceBOffsetWords, 1, required: true);
                    ValidateUpdateOffset(kernel, operation.AuxiliaryOffset0Words, 1, required: true);
                    ValidateUpdateOffset(kernel, operation.AuxiliaryOffset1Words, 1, required: true);
                }
                else if (operation.AuxiliaryOffset0Words != -1 || operation.AuxiliaryOffset1Words != -1)
                {
                    Invalid("Drag size metadata");
                }
                break;
            default:
                Invalid("operation kind");
                break;
        }
        return;

        void RequirePlain(
            string name,
            int sourceB,
            int auxiliary0,
            int auxiliary1,
            IReadOnlyList<uint> valueA,
            IReadOnlyList<uint> valueB)
        {
            if (!noComposition || !noRandom || operation.ReadSourceSnapshot || !finiteBlend ||
                sourceB != -1 || auxiliary0 != -1 || auxiliary1 != -1 ||
                valueA.Count != 0 || valueB.Count != 0)
                Invalid(name + " metadata");
        }

        void RequireFloat3Target(string name)
        {
            if (operation.ValueType != VFXRuntimeValueType.Float3 || !noComposition ||
                !noRandom || operation.ReadSourceSnapshot || !finiteBlend)
                Invalid(name + " target");
        }

        void RequireUnusedOffsets(int sourceB, int auxiliary0, int auxiliary1, string name)
        {
            if (sourceB != -1 || auxiliary0 != -1 || auxiliary1 != -1)
                Invalid(name + " offsets");
        }

        void Invalid(string reason) => throw new InvalidDataException(
            $"VFX Update kernel '{kernel.ContextId}' contains invalid {reason}.");
    }

    private static void ValidateFiniteUpdateWords(
        VFXRuntimeValueType type,
        IReadOnlyList<uint> words)
    {
        if (type < VFXRuntimeValueType.Float) return;
        if (words.Any(word => !float.IsFinite(
                BitConverter.Int32BitsToSingle(unchecked((int)word)))))
            throw new InvalidDataException("VFX Update floating-point operands must be finite.");
    }

    private void ValidateUpdateKernelLayout(
        VFXRuntimeSystemData system,
        VFXRuntimeInitializeKernelData initializeKernel)
    {
        foreach (VFXRuntimeUpdateKernelData update in UpdateKernels.Where(candidate =>
                     string.Equals(candidate.ParticleSystemName, system.Name, StringComparison.Ordinal)))
        {
            if (update.ParticleCapacity != initializeKernel.ParticleCapacity ||
                update.AttributeStrideWords != initializeKernel.AttributeStrideWords ||
                update.UsesDeadList != initializeKernel.UsesDeadList)
                throw new InvalidDataException(
                    $"VFX Update kernel '{update.ContextId}' does not match Initialize layout for '{system.Name}'.");
            IReadOnlyDictionary<int, VFXRuntimeInitializeAttributeData> byOffset =
                initializeKernel.Attributes.ToDictionary(attribute => attribute.Layout.OffsetWords);
            ValidateNamed(update.AliveOffsetWords, "alive", VFXRuntimeValueType.Boolean, required: update.UsesDeadList);
            ValidateNamed(update.SeedOffsetWords, "seed", VFXRuntimeValueType.UInt32, required: false);
            foreach (VFXRuntimeUpdateOperationData operation in update.Operations)
            {
                ValidateTyped(operation.TargetOffsetWords, operation.ValueType);
                switch (operation.Kind)
                {
                    case VFXRuntimeUpdateOperationKind.SetAttribute when operation.ReadSourceSnapshot:
                    case VFXRuntimeUpdateOperationKind.CopyAttribute:
                    case VFXRuntimeUpdateOperationKind.Integrate when operation.SourceAOffsetWords >= 0:
                        ValidateTyped(operation.SourceAOffsetWords, operation.ValueType);
                        break;
                    case VFXRuntimeUpdateOperationKind.Reap:
                        ValidateTyped(operation.SourceAOffsetWords, VFXRuntimeValueType.Float);
                        ValidateTyped(operation.SourceBOffsetWords, VFXRuntimeValueType.Float);
                        break;
                    case VFXRuntimeUpdateOperationKind.Force:
                    case VFXRuntimeUpdateOperationKind.RelativeForce:
                    case VFXRuntimeUpdateOperationKind.Drag:
                        ValidateTyped(operation.SourceAOffsetWords, VFXRuntimeValueType.Float);
                        if (operation.SourceBOffsetWords >= 0)
                        {
                            ValidateTyped(operation.SourceBOffsetWords, VFXRuntimeValueType.Float);
                            ValidateTyped(operation.AuxiliaryOffset0Words, VFXRuntimeValueType.Float);
                            ValidateTyped(operation.AuxiliaryOffset1Words, VFXRuntimeValueType.Float);
                        }
                        break;
                }
            }

            void ValidateNamed(int offset, string name, VFXRuntimeValueType type, bool required)
            {
                if (offset < 0)
                {
                    if (required) throw new InvalidDataException(
                        $"VFX Update kernel '{update.ContextId}' is missing '{name}'.");
                    return;
                }
                if (!byOffset.TryGetValue(offset, out VFXRuntimeInitializeAttributeData? attribute) ||
                    !string.Equals(attribute.Layout.Name, name, StringComparison.Ordinal) ||
                    attribute.Layout.ValueType != type)
                    throw new InvalidDataException(
                        $"VFX Update kernel '{update.ContextId}' has invalid '{name}' layout.");
            }

            void ValidateTyped(int offset, VFXRuntimeValueType type)
            {
                if (!byOffset.TryGetValue(offset, out VFXRuntimeInitializeAttributeData? attribute) ||
                    attribute.Layout.ValueType != type)
                    throw new InvalidDataException(
                        $"VFX Update kernel '{update.ContextId}' operation layout does not match Initialize.");
            }
        }
    }

    private static void ValidateAutomaticBoundsLayout(
        VFXRuntimeSystemData system,
        VFXRuntimeInitializeKernelData kernel)
    {
        if (!system.HasAutomaticBounds) return;
        var byName = kernel.Attributes.ToDictionary(
            attribute => attribute.Layout.Name, StringComparer.Ordinal);
        Validate("position", system.PositionOffsetWords, VFXRuntimeValueType.Float3, required: true);
        Validate("alive", system.AliveOffsetWords, VFXRuntimeValueType.Boolean, required: false);
        Validate("size", system.SizeOffsetWords, VFXRuntimeValueType.Float, required: false);
        Validate("scaleX", system.ScaleXOffsetWords, VFXRuntimeValueType.Float, required: false);
        Validate("scaleY", system.ScaleYOffsetWords, VFXRuntimeValueType.Float, required: false);
        Validate("scaleZ", system.ScaleZOffsetWords, VFXRuntimeValueType.Float, required: false);
        return;

        void Validate(
            string name,
            int expectedOffset,
            VFXRuntimeValueType expectedType,
            bool required)
        {
            if (!byName.TryGetValue(name, out VFXRuntimeInitializeAttributeData? attribute))
            {
                if (required || expectedOffset != -1)
                    throw new InvalidDataException(
                        $"VFX Automatic bounds system '{system.Name}' is missing Initialize attribute '{name}'.");
                return;
            }
            if (expectedOffset != attribute.Layout.OffsetWords ||
                attribute.Layout.ValueType != expectedType)
                throw new InvalidDataException(
                    $"VFX Automatic bounds system '{system.Name}' attribute '{name}' does not match its Initialize layout.");
        }
    }

    private static VFXRuntimeAttributeData[] ReadAttributes(BinaryReader reader) => ReadArray(reader, () =>
        new VFXRuntimeAttributeData(
            ReadString(reader),
            ReadEnum<VFXRuntimeValueType>(reader),
            reader.ReadInt32(),
            reader.ReadInt32()));

    private static TEnum ReadEnum<TEnum>(BinaryReader reader) where TEnum : struct, Enum
    {
        TEnum value = (TEnum)Enum.ToObject(typeof(TEnum), reader.ReadByte());
        if (!Enum.IsDefined(typeof(TEnum), value))
            throw new InvalidDataException($"VFX runtime payload contains invalid {typeof(TEnum).Name} value.");
        return value;
    }

    private static T[] ReadArray<T>(BinaryReader reader, Func<T> read)
    {
        int count = ReadCount(reader);
        if (count == 0) return Array.Empty<T>();
        var result = new T[count];
        for (int index = 0; index < count; index++) result[index] = read();
        return result;
    }

    private static void WriteCount(BinaryWriter writer, int count)
    {
        if (count < 0 || count > MaxCollectionCount)
            throw new InvalidDataException("VFX runtime collection exceeds the supported count.");
        writer.Write(count);
    }

    private static int ReadCount(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > MaxCollectionCount)
            throw new InvalidDataException("VFX runtime payload contains an invalid collection count.");
        return count;
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        ValidateName(value, "string");
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > MaxStringBytes) throw new InvalidDataException("VFX runtime string is too long.");
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length <= 0 || length > MaxStringBytes)
            throw new InvalidDataException("VFX runtime payload contains an invalid string length.");
        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new InvalidDataException("VFX runtime payload string is truncated.");
        return new UTF8Encoding(false, true).GetString(bytes);
    }

    private static void ValidateUniqueNames(IEnumerable<string> names, string category)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            ValidateName(name, category);
            if (!seen.Add(name)) throw new InvalidDataException($"Duplicate VFX {category} '{name}'.");
        }
    }

    private static void ValidateName(string value, string category)
    {
        if (string.IsNullOrEmpty(value)) throw new InvalidDataException($"VFX {category} name cannot be empty.");
    }
}
