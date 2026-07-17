using System.Collections.ObjectModel;
using System.Text.Json;
using UnityEditor.ShaderGraph.Serialization;

namespace UnityEditor.ShaderGraph.Model;

internal enum ShaderGraphTargetKind
{
    Unknown,
    Universal,
    BuiltIn,
    HighDefinition
}

internal enum ShaderGraphSubTargetKind
{
    Unknown,
    UniversalLit,
    UniversalUnlit,
    UniversalDecal,
    UniversalFullscreen,
    BuiltInLit,
    BuiltInUnlit,
    HighDefinitionLit,
    HighDefinitionUnlit,
    HighDefinitionDecal,
    HighDefinitionFabric
}

internal enum ShaderGraphSurfaceType
{
    Opaque = 0,
    Transparent = 1
}

internal enum ShaderGraphZWriteControl
{
    Auto = 0,
    ForceEnabled = 1,
    ForceDisabled = 2
}

internal enum ShaderGraphZTestMode
{
    Disabled = 0,
    Never = 1,
    Less = 2,
    Equal = 3,
    LEqual = 4,
    Greater = 5,
    NotEqual = 6,
    GEqual = 7,
    Always = 8
}

internal enum ShaderGraphAlphaMode
{
    Alpha = 0,
    Premultiply = 1,
    Additive = 2,
    Multiply = 3
}

internal enum ShaderGraphRenderFace
{
    Both = 0,
    Back = 1,
    Front = 2
}

internal enum ShaderGraphWorkflowMode
{
    Specular = 0,
    Metallic = 1
}

internal enum ShaderGraphNormalDropOffSpace
{
    Tangent = 0,
    Object = 1,
    World = 2
}

internal sealed class ShaderGraphTargetSet
{
    private readonly ReadOnlyCollection<ShaderGraphTarget> _targets;

    private ShaderGraphTargetSet(List<ShaderGraphTarget> targets)
    {
        _targets = targets.AsReadOnly();
    }

    internal IReadOnlyList<ShaderGraphTarget> Targets => _targets;

    internal IReadOnlyList<ShaderGraphTarget> ProductTargets
        => _targets.Where(target => target.IsProductSupported).ToArray();

    internal static ShaderGraphTargetSet Create(MultiJsonAsset asset)
    {
        if (asset is null) throw new ArgumentNullException(nameof(asset));
        if (asset.Format != ShaderGraphSerializationFormat.MultiJson)
            throw new NotSupportedException("Legacy Shader Graph targets must be upgraded before model construction.");
        if (!asset.Graph.Root.TryGetProperty("m_ActiveTargets", out JsonElement references))
            return new ShaderGraphTargetSet(new List<ShaderGraphTarget>());
        if (references.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("GraphData.m_ActiveTargets must be an array.");

        var targets = new List<ShaderGraphTarget>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement reference in references.EnumerateArray())
        {
            string objectId = ShaderGraphJson.ReadObjectReference(reference, "GraphData.m_ActiveTargets");
            if (!seen.Add(objectId))
                throw new InvalidDataException("GraphData.m_ActiveTargets contains duplicate target '" + objectId + "'.");
            if (!asset.TryResolve(objectId, out MultiJsonDocument? document) || document is null)
                throw new InvalidDataException("GraphData.m_ActiveTargets references missing target '" + objectId + "'.");
            targets.Add(ShaderGraphTarget.Parse(asset, document));
        }
        return new ShaderGraphTargetSet(targets);
    }
}

internal sealed class ShaderGraphTarget
{
    private readonly ReadOnlyCollection<string> _dataObjectIds;

    private ShaderGraphTarget(
        MultiJsonDocument document,
        ShaderGraphTargetKind kind,
        ShaderGraphSubTarget subTarget,
        List<string> dataObjectIds,
        ShaderGraphSurfaceType? surfaceType,
        ShaderGraphZWriteControl? zWriteControl,
        ShaderGraphZTestMode? zTestMode,
        ShaderGraphAlphaMode? alphaMode,
        ShaderGraphRenderFace? renderFace,
        bool allowMaterialOverride,
        bool alphaClip,
        bool castShadows,
        bool receiveShadows,
        bool supportsLodCrossFade,
        bool supportVfx,
        string customEditorGui)
    {
        Document = document;
        Kind = kind;
        SubTarget = subTarget;
        _dataObjectIds = dataObjectIds.AsReadOnly();
        SurfaceType = surfaceType;
        ZWriteControl = zWriteControl;
        ZTestMode = zTestMode;
        AlphaMode = alphaMode;
        RenderFace = renderFace;
        AllowMaterialOverride = allowMaterialOverride;
        AlphaClip = alphaClip;
        CastShadows = castShadows;
        ReceiveShadows = receiveShadows;
        SupportsLodCrossFade = supportsLodCrossFade;
        SupportVfx = supportVfx;
        CustomEditorGui = customEditorGui;
    }

    internal MultiJsonDocument Document { get; }

    internal string ObjectId => Document.ObjectId;

    internal ShaderGraphTargetKind Kind { get; }

    internal ShaderGraphSubTarget SubTarget { get; }

    internal IReadOnlyList<string> DataObjectIds => _dataObjectIds;

    internal bool IsProductSupported => Kind == ShaderGraphTargetKind.Universal && SubTarget.IsProductSupported;

    internal ShaderGraphSurfaceType? SurfaceType { get; }

    internal ShaderGraphZWriteControl? ZWriteControl { get; }

    internal ShaderGraphZTestMode? ZTestMode { get; }

    internal ShaderGraphAlphaMode? AlphaMode { get; }

    internal ShaderGraphRenderFace? RenderFace { get; }

    internal bool AllowMaterialOverride { get; }

    internal bool AlphaClip { get; }

    internal bool CastShadows { get; }

    internal bool ReceiveShadows { get; }

    internal bool SupportsLodCrossFade { get; }

    internal bool SupportVfx { get; }

    internal string CustomEditorGui { get; }

    internal static ShaderGraphTarget Parse(MultiJsonAsset asset, MultiJsonDocument document)
    {
        ShaderGraphTargetKind kind = document.Type switch
        {
            "UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget" => ShaderGraphTargetKind.Universal,
            "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInTarget" => ShaderGraphTargetKind.BuiltIn,
            "UnityEditor.Rendering.HighDefinition.ShaderGraph.HDTarget" => ShaderGraphTargetKind.HighDefinition,
            _ => ShaderGraphTargetKind.Unknown
        };
        JsonElement root = document.Root;
        JsonElement activeSubTarget = ShaderGraphJson.ReadRequiredProperty(root, "m_ActiveSubTarget", document.ObjectId);
        string subTargetId = ShaderGraphJson.ReadObjectReference(activeSubTarget, document.ObjectId + ".m_ActiveSubTarget");
        if (!asset.TryResolve(subTargetId, out MultiJsonDocument? subTargetDocument) || subTargetDocument is null)
            throw new InvalidDataException("Shader Graph target references missing sub-target '" + subTargetId + "'.");
        ShaderGraphSubTarget subTarget = ShaderGraphSubTarget.Parse(subTargetDocument);
        ValidatePair(kind, subTarget.Kind, document.ObjectId);
        List<string> dataIds = ResolveDataIds(asset, root, document.ObjectId);

        bool hasCommonSurface = kind is ShaderGraphTargetKind.Universal or ShaderGraphTargetKind.BuiltIn;
        return new ShaderGraphTarget(
            document,
            kind,
            subTarget,
            dataIds,
            hasCommonSurface
                ? ReadEnum(root, "m_SurfaceType", ShaderGraphSurfaceType.Opaque, document.ObjectId)
                : null,
            hasCommonSurface
                ? ReadEnum(root, "m_ZWriteControl", ShaderGraphZWriteControl.Auto, document.ObjectId)
                : null,
            hasCommonSurface
                ? ReadEnum(root, "m_ZTestMode", ShaderGraphZTestMode.LEqual, document.ObjectId)
                : null,
            hasCommonSurface
                ? ReadEnum(root, "m_AlphaMode", ShaderGraphAlphaMode.Alpha, document.ObjectId)
                : null,
            hasCommonSurface
                ? ReadEnum(root, "m_RenderFace", ShaderGraphRenderFace.Front, document.ObjectId)
                : null,
            ShaderGraphJson.ReadOptionalBoolean(root, "m_AllowMaterialOverride", false),
            ShaderGraphJson.ReadOptionalBoolean(root, "m_AlphaClip", false),
            ShaderGraphJson.ReadOptionalBoolean(root, "m_CastShadows", true),
            ShaderGraphJson.ReadOptionalBoolean(root, "m_ReceiveShadows", true),
            ShaderGraphJson.ReadOptionalBoolean(root, "m_SupportsLODCrossFade", false),
            ShaderGraphJson.ReadOptionalBoolean(root, "m_SupportVFX", false),
            ShaderGraphJson.ReadOptionalString(root, "m_CustomEditorGUI"));
    }

    private static List<string> ResolveDataIds(MultiJsonAsset asset, JsonElement root, string owner)
    {
        var result = new List<string>();
        if (!root.TryGetProperty("m_Datas", out JsonElement values)) return result;
        if (values.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Shader Graph target m_Datas must be an array.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement value in values.EnumerateArray())
        {
            string id = ShaderGraphJson.ReadObjectReference(value, owner + ".m_Datas");
            if (!seen.Add(id)) throw new InvalidDataException("Shader Graph target contains duplicate data '" + id + "'.");
            if (!asset.TryResolve(id, out _))
                throw new InvalidDataException("Shader Graph target references missing data '" + id + "'.");
            result.Add(id);
        }
        return result;
    }

    private static TEnum ReadEnum<TEnum>(JsonElement root, string name, TEnum fallback, string owner)
        where TEnum : struct, Enum
    {
        int value = ShaderGraphJson.ReadOptionalInt32(root, name, Convert.ToInt32(fallback));
        if (!Enum.IsDefined(typeof(TEnum), value))
            throw new InvalidDataException($"Shader Graph target '{owner}' has invalid {name} value '{value}'.");
        return (TEnum)Enum.ToObject(typeof(TEnum), value);
    }

    private static void ValidatePair(
        ShaderGraphTargetKind target,
        ShaderGraphSubTargetKind subTarget,
        string owner)
    {
        bool valid = target switch
        {
            ShaderGraphTargetKind.Universal => subTarget is
                ShaderGraphSubTargetKind.UniversalLit or
                ShaderGraphSubTargetKind.UniversalUnlit or
                ShaderGraphSubTargetKind.UniversalDecal or
                ShaderGraphSubTargetKind.UniversalFullscreen,
            ShaderGraphTargetKind.BuiltIn => subTarget is
                ShaderGraphSubTargetKind.BuiltInLit or ShaderGraphSubTargetKind.BuiltInUnlit,
            ShaderGraphTargetKind.HighDefinition => subTarget is
                ShaderGraphSubTargetKind.HighDefinitionLit or
                ShaderGraphSubTargetKind.HighDefinitionUnlit or
                ShaderGraphSubTargetKind.HighDefinitionDecal or
                ShaderGraphSubTargetKind.HighDefinitionFabric,
            _ => true
        };
        if (!valid)
            throw new InvalidDataException($"Shader Graph target '{owner}' has incompatible sub-target '{subTarget}'.");
    }
}

internal sealed class ShaderGraphSubTarget
{
    private ShaderGraphSubTarget(
        MultiJsonDocument document,
        ShaderGraphSubTargetKind kind,
        ShaderGraphWorkflowMode workflowMode,
        ShaderGraphNormalDropOffSpace normalDropOffSpace,
        bool blendModePreserveSpecular,
        bool clearCoat,
        ShaderGraphDecalData? decalData)
    {
        Document = document;
        Kind = kind;
        WorkflowMode = workflowMode;
        NormalDropOffSpace = normalDropOffSpace;
        BlendModePreserveSpecular = blendModePreserveSpecular;
        ClearCoat = clearCoat;
        DecalData = decalData;
    }

    internal MultiJsonDocument Document { get; }

    internal ShaderGraphSubTargetKind Kind { get; }

    internal bool IsProductSupported => Kind is
        ShaderGraphSubTargetKind.UniversalLit or
        ShaderGraphSubTargetKind.UniversalUnlit or
        ShaderGraphSubTargetKind.UniversalDecal or
        ShaderGraphSubTargetKind.UniversalFullscreen;

    internal ShaderGraphWorkflowMode WorkflowMode { get; }

    internal ShaderGraphNormalDropOffSpace NormalDropOffSpace { get; }

    internal bool BlendModePreserveSpecular { get; }

    internal bool ClearCoat { get; }

    internal ShaderGraphDecalData? DecalData { get; }

    internal static ShaderGraphSubTarget Parse(MultiJsonDocument document)
    {
        ShaderGraphSubTargetKind kind = document.Type switch
        {
            "UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget" => ShaderGraphSubTargetKind.UniversalLit,
            "UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget" => ShaderGraphSubTargetKind.UniversalUnlit,
            "UnityEditor.Rendering.Universal.ShaderGraph.UniversalDecalSubTarget" => ShaderGraphSubTargetKind.UniversalDecal,
            "UnityEditor.Rendering.Universal.ShaderGraph.UniversalFullscreenSubTarget" => ShaderGraphSubTargetKind.UniversalFullscreen,
            "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInLitSubTarget" => ShaderGraphSubTargetKind.BuiltInLit,
            "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInUnlitSubTarget" => ShaderGraphSubTargetKind.BuiltInUnlit,
            "UnityEditor.Rendering.HighDefinition.ShaderGraph.HDLitSubTarget" => ShaderGraphSubTargetKind.HighDefinitionLit,
            "UnityEditor.Rendering.HighDefinition.ShaderGraph.HDUnlitSubTarget" => ShaderGraphSubTargetKind.HighDefinitionUnlit,
            "UnityEditor.Rendering.HighDefinition.ShaderGraph.DecalSubTarget" => ShaderGraphSubTargetKind.HighDefinitionDecal,
            "UnityEditor.Rendering.HighDefinition.ShaderGraph.FabricSubTarget" => ShaderGraphSubTargetKind.HighDefinitionFabric,
            _ => ShaderGraphSubTargetKind.Unknown
        };
        JsonElement root = document.Root;
        ShaderGraphDecalData? decalData = null;
        if (kind == ShaderGraphSubTargetKind.UniversalDecal && root.TryGetProperty("m_DecalData", out JsonElement serializedDecal))
            decalData = ShaderGraphDecalData.Parse(serializedDecal, document.ObjectId);
        return new ShaderGraphSubTarget(
            document,
            kind,
            ReadEnum(root, "m_WorkflowMode", ShaderGraphWorkflowMode.Metallic, document.ObjectId),
            ReadEnum(root, "m_NormalDropOffSpace", ShaderGraphNormalDropOffSpace.Tangent, document.ObjectId),
            ShaderGraphJson.ReadOptionalBoolean(root, "m_BlendModePreserveSpecular", true),
            ShaderGraphJson.ReadOptionalBoolean(root, "m_ClearCoat", false),
            decalData);
    }

    private static TEnum ReadEnum<TEnum>(JsonElement root, string name, TEnum fallback, string owner)
        where TEnum : struct, Enum
    {
        int value = ShaderGraphJson.ReadOptionalInt32(root, name, Convert.ToInt32(fallback));
        if (!Enum.IsDefined(typeof(TEnum), value))
            throw new InvalidDataException($"Shader Graph sub-target '{owner}' has invalid {name} value '{value}'.");
        return (TEnum)Enum.ToObject(typeof(TEnum), value);
    }
}

internal sealed class ShaderGraphDecalData
{
    private ShaderGraphDecalData(
        bool affectsAlbedo,
        bool affectsNormalBlend,
        bool affectsNormal,
        bool affectsMaos,
        bool affectsEmission,
        int drawOrder,
        bool supportLodCrossFade,
        bool angleFade)
    {
        AffectsAlbedo = affectsAlbedo;
        AffectsNormalBlend = affectsNormalBlend;
        AffectsNormal = affectsNormal;
        AffectsMaos = affectsMaos;
        AffectsEmission = affectsEmission;
        DrawOrder = drawOrder;
        SupportLodCrossFade = supportLodCrossFade;
        AngleFade = angleFade;
    }

    internal bool AffectsAlbedo { get; }
    internal bool AffectsNormalBlend { get; }
    internal bool AffectsNormal { get; }
    internal bool AffectsMaos { get; }
    internal bool AffectsEmission { get; }
    internal int DrawOrder { get; }
    internal bool SupportLodCrossFade { get; }
    internal bool AngleFade { get; }

    internal static ShaderGraphDecalData Parse(JsonElement root, string owner)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Shader Graph decal data must be an object.");
        return new ShaderGraphDecalData(
            ShaderGraphJson.ReadOptionalBoolean(root, "affectsAlbedo", true),
            ShaderGraphJson.ReadOptionalBoolean(root, "affectsNormalBlend", true),
            ShaderGraphJson.ReadOptionalBoolean(root, "affectsNormal", true),
            ShaderGraphJson.ReadOptionalBoolean(root, "affectsMAOS", true),
            ShaderGraphJson.ReadOptionalBoolean(root, "affectsEmission", false),
            ShaderGraphJson.ReadOptionalInt32(root, "drawOrder", 0),
            ShaderGraphJson.ReadOptionalBoolean(root, "supportLodCrossFade", false),
            ShaderGraphJson.ReadOptionalBoolean(root, "angleFade", false));
    }
}
