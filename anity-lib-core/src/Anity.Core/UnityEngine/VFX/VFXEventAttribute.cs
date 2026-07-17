using System.Buffers.Binary;

namespace UnityEngine.VFX;

internal readonly record struct VFXParticleCullingBounds(
    Bounds Bounds,
    bool HasStaticBounds,
    bool HasAutomaticBounds,
    bool WorldSpace,
    Vector3 AutomaticPadding,
    int PositionOffsetWords,
    int AliveOffsetWords,
    int SizeOffsetWords,
    int ScaleXOffsetWords,
    int ScaleYOffsetWords,
    int ScaleZOffsetWords);

[Bindings.NativeHeader("Modules/VFX/Public/ScriptBindings/VisualEffectAssetBindings.h")]
[Bindings.NativeHeader("Modules/VFX/Public/VisualEffectAsset.h")]
[Bindings.NativeHeader("VFXScriptingClasses.h")]
[Scripting.UsedByNativeCode]
public abstract class VisualEffectObject : Object
{
    protected VisualEffectObject()
    {
    }
}

[Scripting.UsedByNativeCode]
public struct VFXExposedProperty
{
    public string name;
    public Type type;
}

[Bindings.NativeHeader("Modules/VFX/Public/VisualEffectAsset.h")]
[Bindings.NativeHeader("VFXScriptingClasses.h")]
[Scripting.UsedByNativeCode]
public class VisualEffectAsset : VisualEffectObject
{
    public const string PlayEventName = "OnPlay";
    public const string StopEventName = "OnStop";
    public static readonly int PlayEventID = Shader.PropertyToID(PlayEventName);
    public static readonly int StopEventID = Shader.PropertyToID(StopEventName);

    private readonly List<VFXEventAttributeSchemaEntry> _eventAttributeSchema = new();
    private readonly Dictionary<int, VFXEventAttributeSchemaEntry> _eventAttributes = new();
    private readonly List<VFXExposedProperty> _exposedProperties = new();
    private readonly Dictionary<int, VFXExposedPropertyDefinition> _exposedPropertyDefinitions = new();
    private readonly List<string> _events = new();
    private readonly Dictionary<int, Rendering.TextureDimension> _textureDimensions = new();
    private readonly List<string> _systemNames = new();
    private readonly List<string> _particleSystemNames = new();
    private readonly List<string> _spawnSystemNames = new();
    private readonly List<string> _outputEventNames = new();
    private readonly Dictionary<int, VFXRuntimeInputEventData> _inputEventDispatches = new();
    private readonly Dictionary<int, VFXParticleSystemInfo> _particleSystems = new();
    private readonly Dictionary<int, VFXParticleCullingBounds> _particleCullingBounds = new();
    private readonly Dictionary<int, VFXSpawnerState> _spawnSystems = new();
    private readonly Dictionary<int, VFXRuntimeOutputEventData> _outputEventSystems = new();
    private readonly Dictionary<long, VFXRuntimeSpawnerProgramData> _spawnerProgramsByContext = new();
    private readonly Dictionary<int, VFXRuntimeSpawnerProgramData> _spawnerProgramsBySystem = new();
    private readonly Dictionary<int, List<VFXRuntimeSpawnerProgramData>> _spawnerProgramsByControlEvent = new();
    private readonly List<VFXRuntimeUpdateKernelData> _updateKernels = new();
    private readonly List<VFXRuntimePlanarOutputData> _planarOutputs = new();
    private uint _compilationVersion;

    [Bindings.FreeFunction(Name = "VisualEffectAssetBindings::GetTextureDimension", HasExplicitThis = true)]
    public Rendering.TextureDimension GetTextureDimension(int nameID)
        => _textureDimensions.TryGetValue(nameID, out Rendering.TextureDimension dimension)
            ? dimension
            : Rendering.TextureDimension.Unknown;

    public Rendering.TextureDimension GetTextureDimension(string name)
        => GetTextureDimension(Shader.PropertyToID(name));

    [Bindings.FreeFunction(Name = "VisualEffectAssetBindings::GetExposedProperties", HasExplicitThis = true)]
    public void GetExposedProperties([Bindings.NotNull("ArgumentNullException")] List<VFXExposedProperty> exposedProperties)
    {
        if (exposedProperties is null) throw new ArgumentNullException(nameof(exposedProperties));
        exposedProperties.Clear();
        exposedProperties.AddRange(_exposedProperties);
    }

    [Bindings.FreeFunction(Name = "VisualEffectAssetBindings::GetEvents", HasExplicitThis = true)]
    public void GetEvents([Bindings.NotNull("ArgumentNullException")] List<string> names)
    {
        if (names is null) throw new ArgumentNullException(nameof(names));
        names.Clear();
        names.AddRange(_events);
    }

    internal uint GetCompilationVersion() => _compilationVersion;
    internal static uint currentRuntimeDataVersion => 5;

    internal void ImportRuntimeData(ReadOnlySpan<byte> bytes)
    {
        VFXRuntimeAssetData runtimeData = VFXRuntimeAssetData.Deserialize(bytes);

        var eventAttributeSchema = new List<VFXEventAttributeSchemaEntry>(runtimeData.EventAttributes.Count);
        var eventAttributes = new Dictionary<int, VFXEventAttributeSchemaEntry>();
        foreach (VFXRuntimeAttributeData attribute in runtimeData.EventAttributes)
        {
            int nameId = Shader.PropertyToID(attribute.Name);
            var entry = new VFXEventAttributeSchemaEntry(
                attribute.Name,
                nameId,
                VFXEventAttributeValueType.FromSystemType(VFXRuntimeAssetData.SystemType(attribute.ValueType)),
                attribute.OffsetWords,
                DefaultEventAttributeValue(attribute.Name, attribute.ValueType));
            if (!eventAttributes.TryAdd(nameId, entry))
                throw new InvalidDataException($"VFX event attributes contain a property id collision at '{attribute.Name}'.");
            eventAttributeSchema.Add(entry);
        }

        var exposedProperties = new List<VFXExposedProperty>(runtimeData.ExposedProperties.Count);
        var exposedPropertyDefinitions = new Dictionary<int, VFXExposedPropertyDefinition>();
        foreach (VFXRuntimeExposedPropertyData property in runtimeData.ExposedProperties)
        {
            Type type = VFXRuntimeAssetData.SystemType(property.ValueType);
            int nameId = Shader.PropertyToID(property.Name);
            exposedProperties.Add(new VFXExposedProperty { name = property.Name, type = type });
            if (!exposedPropertyDefinitions.TryAdd(nameId, new VFXExposedPropertyDefinition(
                    property.Name, type, RuntimeDefaultValue(property), Rendering.TextureDimension.Unknown)))
                throw new InvalidDataException(
                    $"VFX exposed properties contain a property id collision at '{property.Name}'.");
        }

        var systemNames = new List<string>();
        var particleSystemNames = new List<string>();
        var spawnSystemNames = new List<string>();
        var outputEventNames = new List<string>();
        var particleSystems = new Dictionary<int, VFXParticleSystemInfo>();
        var particleCullingBounds = new Dictionary<int, VFXParticleCullingBounds>();
        var spawnSystems = new Dictionary<int, VFXSpawnerState>();
        var outputEventSystems = new Dictionary<int, VFXRuntimeOutputEventData>();
        var inputEventDispatches = new Dictionary<int, VFXRuntimeInputEventData>();
        var systemIds = new HashSet<int>();

        foreach (VFXRuntimeInputEventData inputEvent in runtimeData.InputEventDispatches)
        {
            int eventId = Shader.PropertyToID(inputEvent.Name);
            if (!inputEventDispatches.TryAdd(eventId, inputEvent))
                throw new InvalidDataException($"VFX input events contain a property id collision at '{inputEvent.Name}'.");
        }

        foreach (string name in runtimeData.Systems.Select(system => system.Name)
                     .Concat(runtimeData.OutputEvents.Select(outputEvent => outputEvent.Name)))
        {
            if (!systemIds.Add(Shader.PropertyToID(name)))
                throw new InvalidDataException($"VFX systems contain a property id collision at '{name}'.");
        }
        systemIds.Clear();

        foreach (VFXRuntimeSystemData system in runtimeData.Systems)
        {
            int nameId = AddSystemName(system.Name, systemNames, systemIds);
            switch (system.Kind)
            {
                case VFXRuntimeSystemKind.Particle:
                case VFXRuntimeSystemKind.ParticleStrip:
                    particleSystemNames.Add(system.Name);
                    Bounds bounds = system.HasStaticBounds
                        ? new Bounds(
                            new Vector3(system.BoundsCenterX, system.BoundsCenterY, system.BoundsCenterZ),
                            new Vector3(system.BoundsSizeX, system.BoundsSizeY, system.BoundsSizeZ))
                        : default;
                    particleSystems.Add(nameId, new VFXParticleSystemInfo(0, system.Capacity, true, bounds));
                    particleCullingBounds.Add(nameId, new VFXParticleCullingBounds(
                        bounds,
                        system.HasStaticBounds,
                        system.HasAutomaticBounds,
                        system.BoundsInWorldSpace,
                        new Vector3(
                            system.AutomaticBoundsPaddingX,
                            system.AutomaticBoundsPaddingY,
                            system.AutomaticBoundsPaddingZ),
                        system.PositionOffsetWords,
                        system.AliveOffsetWords,
                        system.SizeOffsetWords,
                        system.ScaleXOffsetWords,
                        system.ScaleYOffsetWords,
                        system.ScaleZOffsetWords));
                    break;
                case VFXRuntimeSystemKind.Spawn:
                    spawnSystemNames.Add(system.Name);
                    spawnSystems.Add(nameId, new VFXSpawnerState());
                    break;
                case VFXRuntimeSystemKind.Mesh:
                    break;
                default:
                    throw new InvalidDataException($"VFX system '{system.Name}' has an unsupported kind.");
            }
        }
        foreach (VFXRuntimeOutputEventData outputEvent in runtimeData.OutputEvents)
        {
            int nameId = AddSystemName(outputEvent.Name, systemNames, systemIds);
            outputEventNames.Add(outputEvent.Name);
            outputEventSystems.Add(nameId, outputEvent);
        }

        var spawnerProgramsByContext = new Dictionary<long, VFXRuntimeSpawnerProgramData>();
        var spawnerProgramsBySystem = new Dictionary<int, VFXRuntimeSpawnerProgramData>();
        var spawnerProgramsByControlEvent = new Dictionary<int, List<VFXRuntimeSpawnerProgramData>>();
        foreach (VFXRuntimeSpawnerProgramData program in runtimeData.SpawnerPrograms)
        {
            int systemId = Shader.PropertyToID(program.SystemName);
            spawnerProgramsByContext.Add(program.ContextId, program);
            spawnerProgramsBySystem.Add(systemId, program);
            foreach (VFXRuntimeSpawnerControlData control in program.Controls)
            {
                int eventId = Shader.PropertyToID(control.EventName);
                if (!spawnerProgramsByControlEvent.TryGetValue(eventId, out List<VFXRuntimeSpawnerProgramData>? list))
                {
                    list = new List<VFXRuntimeSpawnerProgramData>();
                    spawnerProgramsByControlEvent.Add(eventId, list);
                }
                if (!list.Contains(program)) list.Add(program);
            }
        }

        foreach (VFXSpawnerState oldState in _spawnSystems.Values) oldState.Dispose();
        _eventAttributeSchema.Clear();
        _eventAttributeSchema.AddRange(eventAttributeSchema);
        _eventAttributes.Clear();
        foreach ((int id, VFXEventAttributeSchemaEntry entry) in eventAttributes) _eventAttributes.Add(id, entry);
        _exposedProperties.Clear();
        _exposedProperties.AddRange(exposedProperties);
        _exposedPropertyDefinitions.Clear();
        foreach ((int id, VFXExposedPropertyDefinition definition) in exposedPropertyDefinitions)
            _exposedPropertyDefinitions.Add(id, definition);
        _textureDimensions.Clear();
        _events.Clear();
        _events.AddRange(runtimeData.InputEvents);
        _inputEventDispatches.Clear();
        foreach ((int id, VFXRuntimeInputEventData data) in inputEventDispatches)
            _inputEventDispatches.Add(id, data);
        ReplaceContents(_systemNames, systemNames);
        ReplaceContents(_particleSystemNames, particleSystemNames);
        ReplaceContents(_spawnSystemNames, spawnSystemNames);
        ReplaceContents(_outputEventNames, outputEventNames);
        _particleSystems.Clear();
        foreach ((int id, VFXParticleSystemInfo info) in particleSystems) _particleSystems.Add(id, info);
        _particleCullingBounds.Clear();
        foreach ((int id, VFXParticleCullingBounds bounds) in particleCullingBounds)
            _particleCullingBounds.Add(id, bounds);
        _spawnSystems.Clear();
        foreach ((int id, VFXSpawnerState state) in spawnSystems) _spawnSystems.Add(id, state);
        _outputEventSystems.Clear();
        foreach ((int id, VFXRuntimeOutputEventData data) in outputEventSystems) _outputEventSystems.Add(id, data);
        _spawnerProgramsByContext.Clear();
        foreach ((long id, VFXRuntimeSpawnerProgramData program) in spawnerProgramsByContext)
            _spawnerProgramsByContext.Add(id, program);
        _spawnerProgramsBySystem.Clear();
        foreach ((int id, VFXRuntimeSpawnerProgramData program) in spawnerProgramsBySystem)
            _spawnerProgramsBySystem.Add(id, program);
        _spawnerProgramsByControlEvent.Clear();
        foreach ((int id, List<VFXRuntimeSpawnerProgramData> programs) in spawnerProgramsByControlEvent)
            _spawnerProgramsByControlEvent.Add(id, programs);
        _updateKernels.Clear();
        _updateKernels.AddRange(runtimeData.UpdateKernels);
        _planarOutputs.Clear();
        _planarOutputs.AddRange(runtimeData.PlanarOutputs);
        EventAttributeStrideWords = runtimeData.EventAttributes.Sum(attribute => attribute.SizeWords);
        _compilationVersion++;
    }

    private static object RuntimeDefaultValue(VFXRuntimeExposedPropertyData property)
    {
        float Float(int index) => BitConverter.Int32BitsToSingle(unchecked((int)property.DefaultWords[index]));
        return property.ValueType switch
        {
            VFXRuntimeValueType.Boolean => property.DefaultWords[0] != 0,
            VFXRuntimeValueType.UInt32 => property.DefaultWords[0],
            VFXRuntimeValueType.Int32 => unchecked((int)property.DefaultWords[0]),
            VFXRuntimeValueType.Float => Float(0),
            VFXRuntimeValueType.Float2 => new Vector2(Float(0), Float(1)),
            VFXRuntimeValueType.Float3 => new Vector3(Float(0), Float(1), Float(2)),
            VFXRuntimeValueType.Float4 => new Vector4(Float(0), Float(1), Float(2), Float(3)),
            VFXRuntimeValueType.Matrix4x4 => RuntimeMatrix(property.DefaultWords),
            _ => throw new InvalidDataException(
                $"VFX exposed property '{property.Name}' has unsupported type '{property.ValueType}'.")
        };
    }

    private static Matrix4x4 RuntimeMatrix(IReadOnlyList<uint> words)
    {
        var result = new Matrix4x4();
        for (int index = 0; index < 16; index++)
            result[index] = BitConverter.Int32BitsToSingle(unchecked((int)words[index]));
        return result;
    }

    internal void DefineEventAttribute(string name, Type type)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Event attribute name cannot be empty.", nameof(name));
        VFXEventAttributeValueType valueType = VFXEventAttributeValueType.FromSystemType(type);
        int nameID = Shader.PropertyToID(name);
        if (_eventAttributes.TryGetValue(nameID, out VFXEventAttributeSchemaEntry existing))
        {
            if (existing.ValueType != valueType)
                throw new InvalidOperationException($"VFX event attribute '{name}' is already defined as {existing.ValueType.SystemType.Name}.");
            return;
        }

        var entry = new VFXEventAttributeSchemaEntry(name, nameID, valueType, EventAttributeStrideWords);
        _eventAttributes.Add(nameID, entry);
        _eventAttributeSchema.Add(entry);
        RebuildEventOffsets();
        _compilationVersion++;
    }

    internal void DefineEvent(string name)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Event name cannot be empty.", nameof(name));
        if (!_events.Contains(name, StringComparer.Ordinal))
        {
            _events.Add(name);
            _inputEventDispatches[Shader.PropertyToID(name)] = new VFXRuntimeInputEventData(
                name, Array.Empty<VFXRuntimeInputEventTargetData>());
            _compilationVersion++;
        }
    }

    internal void DefineExposedProperty(
        string name,
        Type type,
        Rendering.TextureDimension textureDimension = Rendering.TextureDimension.Unknown,
        object? defaultValue = null)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Property name cannot be empty.", nameof(name));
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (defaultValue is not null && !type.IsInstanceOfType(defaultValue))
            throw new ArgumentException($"Default value must be assignable to '{type}'.", nameof(defaultValue));
        int nameID = Shader.PropertyToID(name);
        _exposedProperties.RemoveAll(property => string.Equals(property.name, name, StringComparison.Ordinal));
        _exposedProperties.Add(new VFXExposedProperty { name = name, type = type });
        _exposedPropertyDefinitions[nameID] = new VFXExposedPropertyDefinition(name, type, defaultValue, textureDimension);
        if (typeof(Texture).IsAssignableFrom(type))
            _textureDimensions[nameID] = textureDimension;
        else
            _textureDimensions.Remove(nameID);
        _compilationVersion++;
    }

    internal void DefineParticleSystem(string name, VFXParticleSystemInfo info)
    {
        int nameID = DefineSystemName(name, _particleSystemNames);
        _particleSystems[nameID] = info;
        _particleCullingBounds[nameID] = new VFXParticleCullingBounds(
            info.bounds, HasFiniteNonNegativeBounds(info.bounds), false, false,
            Vector3.zero, -1, -1, -1, -1, -1, -1);
        _compilationVersion++;
    }

    internal void DefineSpawnSystem(string name, VFXSpawnerState state)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));
        int nameID = DefineSystemName(name, _spawnSystemNames);
        _spawnSystems[nameID] = new VFXSpawnerState(state);
        _compilationVersion++;
    }

    internal void DefineOutputEventSystem(string name)
    {
        DefineSystemName(name, _outputEventNames);
        _compilationVersion++;
    }

    internal bool TryGetExposedProperty(int nameID, out VFXExposedPropertyDefinition definition)
        => _exposedPropertyDefinitions.TryGetValue(nameID, out definition);

    internal bool HasSystem(int nameID)
        => _systemNames.Any(name => Shader.PropertyToID(name) == nameID);

    internal void GetSystemNames(List<string> names) => ReplaceContents(names, _systemNames);
    internal void GetParticleSystemNames(List<string> names) => ReplaceContents(names, _particleSystemNames);
    internal void GetSpawnSystemNames(List<string> names) => ReplaceContents(names, _spawnSystemNames);
    internal void GetOutputEventNames(List<string> names) => ReplaceContents(names, _outputEventNames);

    internal bool TryGetParticleSystemInfo(int nameID, out VFXParticleSystemInfo info)
        => _particleSystems.TryGetValue(nameID, out info);

    internal bool TryGetParticleCullingBounds(
        int nameID, out Bounds bounds, out bool worldSpace)
    {
        if (_particleCullingBounds.TryGetValue(nameID, out VFXParticleCullingBounds value) &&
            value.HasStaticBounds)
        {
            bounds = value.Bounds;
            worldSpace = value.WorldSpace;
            return true;
        }
        bounds = default;
        worldSpace = false;
        return false;
    }

    internal bool TryGetParticleCullingBounds(
        int nameID, out VFXParticleCullingBounds bounds)
        => _particleCullingBounds.TryGetValue(nameID, out bounds);

    private static bool HasFiniteNonNegativeBounds(Bounds bounds)
        => float.IsFinite(bounds.center.x) && float.IsFinite(bounds.center.y) &&
           float.IsFinite(bounds.center.z) && float.IsFinite(bounds.size.x) &&
           float.IsFinite(bounds.size.y) && float.IsFinite(bounds.size.z) &&
           bounds.size.x >= 0f && bounds.size.y >= 0f && bounds.size.z >= 0f;

    internal bool TryGetSpawnSystemInfo(int nameID, out VFXSpawnerState state)
    {
        if (_spawnSystems.TryGetValue(nameID, out VFXSpawnerState? stored))
        {
            state = new VFXSpawnerState(stored);
            return true;
        }
        state = null!;
        return false;
    }

    internal bool TryGetOutputEventRuntimeData(int nameID, out VFXRuntimeOutputEventData data)
        => _outputEventSystems.TryGetValue(nameID, out data!);

    internal IEnumerable<KeyValuePair<int, VFXRuntimeOutputEventData>>
        GetOutputEventsForSpawner(long sourceSpawnerContextId)
        => _outputEventSystems.Where(pair => pair.Value.BufferMappings.Any(mapping =>
            mapping.SourceSpawnerContextId == sourceSpawnerContextId));

    internal bool TryGetInputEventRuntimeData(int nameID, out VFXRuntimeInputEventData data)
        => _inputEventDispatches.TryGetValue(nameID, out data!);

    internal bool TryGetSpawnerProgramByContext(long contextId, out VFXRuntimeSpawnerProgramData program)
        => _spawnerProgramsByContext.TryGetValue(contextId, out program!);

    internal bool TryGetSpawnerProgramBySystem(int systemId, out VFXRuntimeSpawnerProgramData program)
        => _spawnerProgramsBySystem.TryGetValue(systemId, out program!);

    internal IReadOnlyList<VFXRuntimeUpdateKernelData> GetUpdateKernels()
        => _updateKernels;

    internal IReadOnlyList<VFXRuntimePlanarOutputData> GetPlanarOutputs()
        => _planarOutputs;

    internal IReadOnlyList<VFXRuntimeSpawnerProgramData> GetSpawnerProgramsForControl(int eventId)
        => _spawnerProgramsByControlEvent.TryGetValue(eventId, out List<VFXRuntimeSpawnerProgramData>? programs)
            ? programs
            : Array.Empty<VFXRuntimeSpawnerProgramData>();

    internal IReadOnlyCollection<VFXRuntimeSpawnerProgramData> SpawnerPrograms
        => _spawnerProgramsBySystem.Values;

    internal bool HasAnySystemAwake
        => _particleSystems.Values.Any(info => !info.sleeping && info.aliveCount > 0) ||
           _spawnSystems.Values.Any(state => state.playing);

    internal bool TryGetEventAttribute(int nameID, out VFXEventAttributeSchemaEntry entry)
        => _eventAttributes.TryGetValue(nameID, out entry);

    internal IReadOnlyList<VFXEventAttributeSchemaEntry> EventAttributeSchema => _eventAttributeSchema;
    internal int EventAttributeStrideWords { get; private set; }
    internal VFXCameraBufferTypes CameraBufferRequirements { get; set; }

    private static object? DefaultEventAttributeValue(
        string name,
        VFXRuntimeValueType valueType)
    {
        if (valueType == VFXRuntimeValueType.Boolean &&
            name is "alive" or "stripAlive") return true;
        if (valueType == VFXRuntimeValueType.Float)
        {
            if (name == "size") return 0.1f;
            if (name is "alpha" or "scaleX" or "scaleY" or "scaleZ" or
                "lifetime" or "mass" or "spawnCount") return 1f;
        }
        if (valueType == VFXRuntimeValueType.Float3)
        {
            if (name == "direction") return new Vector3(0f, 0f, 1f);
            if (name == "color") return Vector3.one;
            if (name == "axisX") return new Vector3(1f, 0f, 0f);
            if (name == "axisY") return new Vector3(0f, 1f, 0f);
            if (name == "axisZ") return new Vector3(0f, 0f, 1f);
        }
        return null;
    }

    private int DefineSystemName(string name, List<string> category)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("System name cannot be empty.", nameof(name));
        if (!category.Contains(name, StringComparer.Ordinal)) category.Add(name);
        if (!_systemNames.Contains(name, StringComparer.Ordinal)) _systemNames.Add(name);
        return Shader.PropertyToID(name);
    }

    private static int AddSystemName(string name, List<string> names, HashSet<int> ids)
    {
        int nameId = Shader.PropertyToID(name);
        if (!ids.Add(nameId))
            throw new InvalidDataException($"VFX systems contain a property id collision at '{name}'.");
        names.Add(name);
        return nameId;
    }

    private static void ReplaceContents(List<string> destination, List<string> source)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        destination.Clear();
        destination.AddRange(source);
    }

    private void RebuildEventOffsets()
    {
        int offset = 0;
        for (int index = 0; index < _eventAttributeSchema.Count; index++)
        {
            VFXEventAttributeSchemaEntry current = _eventAttributeSchema[index];
            current = current with { OffsetWords = offset };
            _eventAttributeSchema[index] = current;
            _eventAttributes[current.NameID] = current;
            offset = checked(offset + current.ValueType.WordCount);
        }
        EventAttributeStrideWords = offset;
    }
}

internal readonly record struct VFXExposedPropertyDefinition(
    string Name,
    Type Type,
    object? DefaultValue,
    Rendering.TextureDimension TextureDimension);

internal readonly record struct VFXEventAttributeSchemaEntry(
    string Name,
    int NameID,
    VFXEventAttributeValueType ValueType,
    int OffsetWords,
    object? DefaultValue = null);

internal readonly record struct VFXEventAttributeValueType(Type SystemType, int WordCount)
{
    internal static readonly VFXEventAttributeValueType Bool = new(typeof(bool), 1);
    internal static readonly VFXEventAttributeValueType Int = new(typeof(int), 1);
    internal static readonly VFXEventAttributeValueType Uint = new(typeof(uint), 1);
    internal static readonly VFXEventAttributeValueType Float = new(typeof(float), 1);
    internal static readonly VFXEventAttributeValueType Vector2 = new(typeof(UnityEngine.Vector2), 2);
    internal static readonly VFXEventAttributeValueType Vector3 = new(typeof(UnityEngine.Vector3), 3);
    internal static readonly VFXEventAttributeValueType Vector4 = new(typeof(UnityEngine.Vector4), 4);
    internal static readonly VFXEventAttributeValueType Matrix4x4 = new(typeof(UnityEngine.Matrix4x4), 16);

    internal static VFXEventAttributeValueType FromSystemType(Type type)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (type == typeof(bool)) return Bool;
        if (type == typeof(int)) return Int;
        if (type == typeof(uint)) return Uint;
        if (type == typeof(float)) return Float;
        if (type == typeof(UnityEngine.Vector2)) return Vector2;
        if (type == typeof(UnityEngine.Vector3)) return Vector3;
        if (type == typeof(UnityEngine.Vector4)) return Vector4;
        if (type == typeof(UnityEngine.Matrix4x4)) return Matrix4x4;
        throw new ArgumentException($"Type '{type}' is not supported by VFXEventAttribute.", nameof(type));
    }
}

[Bindings.NativeType(Header = "Modules/VFX/Public/VFXEventAttribute.h")]
[Scripting.RequiredByNativeCode]
public sealed class VFXEventAttribute : IDisposable
{
    private readonly Dictionary<int, (VFXEventAttributeValueType Type, object Value)> _values = new();
    private VisualEffectAsset? _vfxAsset;
    private bool _disposed;

    internal VFXEventAttribute(VisualEffectAsset? vfxAsset)
    {
        _vfxAsset = vfxAsset;
        if (vfxAsset is null) return;
        foreach (VFXEventAttributeSchemaEntry field in vfxAsset.EventAttributeSchema)
            if (field.DefaultValue is not null)
                _values[field.NameID] = (field.ValueType, field.DefaultValue);
    }

    public VFXEventAttribute(VFXEventAttribute original)
    {
        if (original is null)
            throw new ArgumentNullException("VFXEventAttribute expect a non null attribute");
        original.ThrowIfDisposed();
        _vfxAsset = original._vfxAsset;
        CopyValues(original);
    }

    internal VisualEffectAsset? vfxAsset => _vfxAsset;

    [Bindings.NativeName("HasValueFromScript<bool>")] public bool HasBool(int nameID) => Has(nameID, VFXEventAttributeValueType.Bool);
    [Bindings.NativeName("HasValueFromScript<int>")] public bool HasInt(int nameID) => Has(nameID, VFXEventAttributeValueType.Int);
    [Bindings.NativeName("HasValueFromScript<UInt32>")] public bool HasUint(int nameID) => Has(nameID, VFXEventAttributeValueType.Uint);
    [Bindings.NativeName("HasValueFromScript<float>")] public bool HasFloat(int nameID) => Has(nameID, VFXEventAttributeValueType.Float);
    [Bindings.NativeName("HasValueFromScript<Vector2f>")] public bool HasVector2(int nameID) => Has(nameID, VFXEventAttributeValueType.Vector2);
    [Bindings.NativeName("HasValueFromScript<Vector3f>")] public bool HasVector3(int nameID) => Has(nameID, VFXEventAttributeValueType.Vector3);
    [Bindings.NativeName("HasValueFromScript<Vector4f>")] public bool HasVector4(int nameID) => Has(nameID, VFXEventAttributeValueType.Vector4);
    [Bindings.NativeName("HasValueFromScript<Matrix4x4f>")] public bool HasMatrix4x4(int nameID) => Has(nameID, VFXEventAttributeValueType.Matrix4x4);

    [Bindings.NativeName("SetValueFromScript<bool>")] public void SetBool(int nameID, bool b) => Set(nameID, VFXEventAttributeValueType.Bool, b);
    [Bindings.NativeName("SetValueFromScript<int>")] public void SetInt(int nameID, int i) => Set(nameID, VFXEventAttributeValueType.Int, i);
    [Bindings.NativeName("SetValueFromScript<UInt32>")] public void SetUint(int nameID, uint i) => Set(nameID, VFXEventAttributeValueType.Uint, i);
    [Bindings.NativeName("SetValueFromScript<float>")] public void SetFloat(int nameID, float f) => Set(nameID, VFXEventAttributeValueType.Float, f);
    [Bindings.NativeName("SetValueFromScript<Vector2f>")] public void SetVector2(int nameID, Vector2 v) => Set(nameID, VFXEventAttributeValueType.Vector2, v);
    [Bindings.NativeName("SetValueFromScript<Vector3f>")] public void SetVector3(int nameID, Vector3 v) => Set(nameID, VFXEventAttributeValueType.Vector3, v);
    [Bindings.NativeName("SetValueFromScript<Vector4f>")] public void SetVector4(int nameID, Vector4 v) => Set(nameID, VFXEventAttributeValueType.Vector4, v);
    [Bindings.NativeName("SetValueFromScript<Matrix4x4f>")] public void SetMatrix4x4(int nameID, Matrix4x4 v) => Set(nameID, VFXEventAttributeValueType.Matrix4x4, v);

    [Bindings.NativeName("GetValueFromScript<bool>")] public bool GetBool(int nameID) => Get(nameID, VFXEventAttributeValueType.Bool, false);
    [Bindings.NativeName("GetValueFromScript<int>")] public int GetInt(int nameID) => Get(nameID, VFXEventAttributeValueType.Int, 0);
    [Bindings.NativeName("GetValueFromScript<UInt32>")] public uint GetUint(int nameID) => Get(nameID, VFXEventAttributeValueType.Uint, 0u);
    [Bindings.NativeName("GetValueFromScript<float>")] public float GetFloat(int nameID) => Get(nameID, VFXEventAttributeValueType.Float, 0f);
    [Bindings.NativeName("GetValueFromScript<Vector2f>")] public Vector2 GetVector2(int nameID) => Get(nameID, VFXEventAttributeValueType.Vector2, Vector2.zero);
    [Bindings.NativeName("GetValueFromScript<Vector3f>")] public Vector3 GetVector3(int nameID) => Get(nameID, VFXEventAttributeValueType.Vector3, Vector3.zero);
    [Bindings.NativeName("GetValueFromScript<Vector4f>")] public Vector4 GetVector4(int nameID) => Get(nameID, VFXEventAttributeValueType.Vector4, Vector4.zero);
    [Bindings.NativeName("GetValueFromScript<Matrix4x4f>")] public Matrix4x4 GetMatrix4x4(int nameID) => Get(nameID, VFXEventAttributeValueType.Matrix4x4, Matrix4x4.zero);

    public bool HasBool(string name) => HasBool(PropertyId(name));
    public bool HasInt(string name) => HasInt(PropertyId(name));
    public bool HasUint(string name) => HasUint(PropertyId(name));
    public bool HasFloat(string name) => HasFloat(PropertyId(name));
    public bool HasVector2(string name) => HasVector2(PropertyId(name));
    public bool HasVector3(string name) => HasVector3(PropertyId(name));
    public bool HasVector4(string name) => HasVector4(PropertyId(name));
    public bool HasMatrix4x4(string name) => HasMatrix4x4(PropertyId(name));

    public void SetBool(string name, bool b) => SetBool(PropertyId(name), b);
    public void SetInt(string name, int i) => SetInt(PropertyId(name), i);
    public void SetUint(string name, uint i) => SetUint(PropertyId(name), i);
    public void SetFloat(string name, float f) => SetFloat(PropertyId(name), f);
    public void SetVector2(string name, Vector2 v) => SetVector2(PropertyId(name), v);
    public void SetVector3(string name, Vector3 v) => SetVector3(PropertyId(name), v);
    public void SetVector4(string name, Vector4 v) => SetVector4(PropertyId(name), v);
    public void SetMatrix4x4(string name, Matrix4x4 v) => SetMatrix4x4(PropertyId(name), v);

    public bool GetBool(string name) => GetBool(PropertyId(name));
    public int GetInt(string name) => GetInt(PropertyId(name));
    public uint GetUint(string name) => GetUint(PropertyId(name));
    public float GetFloat(string name) => GetFloat(PropertyId(name));
    public Vector2 GetVector2(string name) => GetVector2(PropertyId(name));
    public Vector3 GetVector3(string name) => GetVector3(PropertyId(name));
    public Vector4 GetVector4(string name) => GetVector4(PropertyId(name));
    public Matrix4x4 GetMatrix4x4(string name) => GetMatrix4x4(PropertyId(name));

    public void CopyValuesFrom([Bindings.NotNull("ArgumentNullException")] VFXEventAttribute eventAttibute)
    {
        if (eventAttibute is null) throw new ArgumentNullException(nameof(eventAttibute));
        ThrowIfDisposed();
        eventAttibute.ThrowIfDisposed();
        _values.Clear();
        foreach ((int key, (VFXEventAttributeValueType type, object value)) in eventAttibute._values)
        {
            if (_vfxAsset is null ||
                _vfxAsset.TryGetEventAttribute(key, out VFXEventAttributeSchemaEntry schema) && schema.ValueType == type)
                _values.Add(key, (type, value));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _values.Clear();
        _vfxAsset = null;
        GC.SuppressFinalize(this);
    }

    ~VFXEventAttribute()
    {
        Dispose();
    }

    internal byte[] PackValues(out int strideWords)
    {
        ThrowIfDisposed();
        if (_vfxAsset is null)
        {
            strideWords = 0;
            return Array.Empty<byte>();
        }

        strideWords = _vfxAsset.EventAttributeStrideWords;
        byte[] result = new byte[checked(strideWords * sizeof(uint))];
        foreach (VFXEventAttributeSchemaEntry field in _vfxAsset.EventAttributeSchema)
        {
            if (!_values.TryGetValue(field.NameID, out var stored) || stored.Type != field.ValueType)
                continue;
            WriteValue(result.AsSpan(field.OffsetWords * sizeof(uint)), stored.Value, field.ValueType);
        }
        return result;
    }

    internal void LoadRuntimeRecord(
        VFXRuntimeOutputEventData outputEvent,
        ReadOnlySpan<byte> record)
    {
        ThrowIfDisposed();
        if (_vfxAsset is null) throw new InvalidOperationException("VFX output records require a compiled VisualEffectAsset.");
        if (outputEvent is null) throw new ArgumentNullException(nameof(outputEvent));
        int expectedBytes = checked(outputEvent.StrideWords * sizeof(uint));
        if (record.Length != expectedBytes)
            throw new InvalidDataException($"VFX output event '{outputEvent.Name}' record has an invalid byte length.");
        _values.Clear();
        foreach (VFXRuntimeAttributeData field in outputEvent.Attributes)
        {
            int byteOffset = checked(field.OffsetWords * sizeof(uint));
            int byteLength = checked(field.SizeWords * sizeof(uint));
            ReadOnlySpan<byte> source = record.Slice(byteOffset, byteLength);
            int nameId = Shader.PropertyToID(field.Name);
            object value = ReadRuntimeValue(source, field.ValueType);
            VFXEventAttributeValueType valueType = VFXEventAttributeValueType.FromSystemType(value.GetType());
            if (!_vfxAsset.TryGetEventAttribute(nameId, out VFXEventAttributeSchemaEntry schema) ||
                schema.ValueType != valueType)
                throw new InvalidDataException(
                    $"VFX output event '{outputEvent.Name}' attribute '{field.Name}' does not match the compiled Asset schema.");
            _values.Add(nameId, (valueType, value));
        }
    }

    internal void LoadRuntimeRecord(ReadOnlySpan<byte> record)
    {
        ThrowIfDisposed();
        if (_vfxAsset is null)
            throw new InvalidOperationException("VFX runtime records require a compiled VisualEffectAsset.");
        int expectedBytes = checked(_vfxAsset.EventAttributeStrideWords * sizeof(uint));
        if (record.Length != expectedBytes)
            throw new InvalidDataException("VFX runtime Event record has an invalid byte length.");
        _values.Clear();
        foreach (VFXEventAttributeSchemaEntry field in _vfxAsset.EventAttributeSchema)
        {
            int byteOffset = checked(field.OffsetWords * sizeof(uint));
            int byteLength = checked(field.ValueType.WordCount * sizeof(uint));
            object value = ReadRuntimeValue(
                record.Slice(byteOffset, byteLength),
                VFXRuntimeAssetData.RuntimeType(field.ValueType.SystemType));
            _values.Add(field.NameID, (field.ValueType, value));
        }
    }

    private bool Has(int nameID, VFXEventAttributeValueType type)
    {
        ThrowIfDisposed();
        if (_vfxAsset is not null)
            return _vfxAsset.TryGetEventAttribute(nameID, out VFXEventAttributeSchemaEntry schema) && schema.ValueType == type;
        return _values.TryGetValue(nameID, out var value) && value.Type == type;
    }

    private void Set<T>(int nameID, VFXEventAttributeValueType type, T value) where T : notnull
    {
        ThrowIfDisposed();
        if (_vfxAsset is not null &&
            (!_vfxAsset.TryGetEventAttribute(nameID, out VFXEventAttributeSchemaEntry schema) || schema.ValueType != type))
            return;
        _values[nameID] = (type, value);
    }

    private T Get<T>(int nameID, VFXEventAttributeValueType type, T defaultValue)
    {
        ThrowIfDisposed();
        return _values.TryGetValue(nameID, out var value) && value.Type == type
            ? (T)value.Value
            : defaultValue;
    }

    private void CopyValues(VFXEventAttribute source)
    {
        foreach (var pair in source._values)
            _values.Add(pair.Key, pair.Value);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VFXEventAttribute));
    }

    private static int PropertyId(string name)
        => Shader.PropertyToID(name);

    private static void WriteValue(Span<byte> destination, object value, VFXEventAttributeValueType type)
    {
        if (type == VFXEventAttributeValueType.Bool) WriteWord(destination, (bool)value ? 1u : 0u);
        else if (type == VFXEventAttributeValueType.Int) WriteWord(destination, unchecked((uint)(int)value));
        else if (type == VFXEventAttributeValueType.Uint) WriteWord(destination, (uint)value);
        else if (type == VFXEventAttributeValueType.Float) WriteFloat(destination, (float)value);
        else if (type == VFXEventAttributeValueType.Vector2)
        {
            Vector2 vector = (Vector2)value;
            WriteFloat(destination, vector.x); WriteFloat(destination[4..], vector.y);
        }
        else if (type == VFXEventAttributeValueType.Vector3)
        {
            Vector3 vector = (Vector3)value;
            WriteFloat(destination, vector.x); WriteFloat(destination[4..], vector.y); WriteFloat(destination[8..], vector.z);
        }
        else if (type == VFXEventAttributeValueType.Vector4)
        {
            Vector4 vector = (Vector4)value;
            for (int component = 0; component < 4; component++) WriteFloat(destination[(component * 4)..], vector[component]);
        }
        else if (type == VFXEventAttributeValueType.Matrix4x4)
        {
            Matrix4x4 matrix = (Matrix4x4)value;
            for (int component = 0; component < 16; component++) WriteFloat(destination[(component * 4)..], matrix[component]);
        }
    }

    private static void WriteFloat(Span<byte> destination, float value)
        => WriteWord(destination, unchecked((uint)BitConverter.SingleToInt32Bits(value)));

    private static void WriteWord(Span<byte> destination, uint value)
        => BinaryPrimitives.WriteUInt32LittleEndian(destination, value);

    private static object ReadRuntimeValue(ReadOnlySpan<byte> source, VFXRuntimeValueType type)
    {
        return type switch
        {
            VFXRuntimeValueType.Boolean => ReadRuntimeWord(source, 0) != 0,
            VFXRuntimeValueType.UInt32 => ReadRuntimeWord(source, 0),
            VFXRuntimeValueType.Int32 => unchecked((int)ReadRuntimeWord(source, 0)),
            VFXRuntimeValueType.Float => ReadRuntimeFloat(source, 0),
            VFXRuntimeValueType.Float2 => new Vector2(ReadRuntimeFloat(source, 0), ReadRuntimeFloat(source, 1)),
            VFXRuntimeValueType.Float3 => new Vector3(
                ReadRuntimeFloat(source, 0), ReadRuntimeFloat(source, 1), ReadRuntimeFloat(source, 2)),
            VFXRuntimeValueType.Float4 => new Vector4(
                ReadRuntimeFloat(source, 0), ReadRuntimeFloat(source, 1),
                ReadRuntimeFloat(source, 2), ReadRuntimeFloat(source, 3)),
            VFXRuntimeValueType.Matrix4x4 => ReadMatrix(source),
            _ => throw new InvalidDataException($"Unsupported VFX output attribute type '{type}'.")
        };
    }

    private static uint ReadRuntimeWord(ReadOnlySpan<byte> source, int component)
        => BinaryPrimitives.ReadUInt32LittleEndian(
            source.Slice(component * sizeof(uint), sizeof(uint)));

    private static float ReadRuntimeFloat(ReadOnlySpan<byte> source, int component)
        => BitConverter.Int32BitsToSingle(unchecked((int)ReadRuntimeWord(source, component)));

    private static Matrix4x4 ReadMatrix(ReadOnlySpan<byte> source)
    {
        Matrix4x4 matrix = default;
        for (int component = 0; component < 16; component++)
            matrix[component] = BitConverter.Int32BitsToSingle(unchecked((int)
                BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(component * sizeof(uint), sizeof(uint)))));
        return matrix;
    }
}
