using System.Collections.ObjectModel;

namespace UnityEditor.VFX.Model;

internal enum VfxModelKind
{
    Unknown,
    Graph,
    Ui,
    Context,
    Block,
    Operator,
    Parameter,
    Slot,
    Data,
    External
}

internal sealed class VfxScriptType
{
    internal VfxScriptType(string guid, string typeName, VfxModelKind kind, bool isProductSupported = true)
    {
        Guid = guid;
        TypeName = typeName;
        Kind = kind;
        IsProductSupported = isProductSupported;
    }

    internal string Guid { get; }

    internal string TypeName { get; }

    internal VfxModelKind Kind { get; }

    internal bool IsProductSupported { get; }
}

/// <summary>
/// Stable Unity 14.0.11 script GUID registry used by Unity 2022.3 VFX Graph assets.
/// GUIDs are serialized ABI: renaming the Anity implementation never changes asset identity.
/// </summary>
internal static class VfxScriptTypeRegistry
{
    private static readonly ReadOnlyDictionary<string, VfxScriptType> Types = CreateTypes();

    internal static IReadOnlyDictionary<string, VfxScriptType> All => Types;

    internal static bool TryResolve(string guid, out VfxScriptType? type)
    {
        if (guid is null) throw new ArgumentNullException(nameof(guid));
        return Types.TryGetValue(NormalizeGuid(guid), out type);
    }

    internal static VfxScriptType Resolve(string guid)
    {
        if (!TryResolve(guid, out VfxScriptType? type))
            throw new KeyNotFoundException("Unknown VFX Graph script GUID '" + guid + "'.");
        return type!;
    }

    internal static string NormalizeGuid(string guid)
    {
        if (guid is null) throw new ArgumentNullException(nameof(guid));
        string normalized = guid.Replace("-", string.Empty).ToLowerInvariant();
        if (normalized.Length != 32 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("VFX script GUID must contain exactly 32 hexadecimal digits.", nameof(guid));
        return normalized;
    }

    private static ReadOnlyDictionary<string, VfxScriptType> CreateTypes()
    {
        var types = new Dictionary<string, VfxScriptType>(StringComparer.Ordinal);

        Add(types, "7d4c867f6b72b714dbb5fd1780afe208", "VFXGraph", VfxModelKind.Graph);
        Add(types, "d01270efd3285ea4a9d6c555cb0a8027", "VFXUI", VfxModelKind.Ui);

        Add(types, "f44f205d2606d1a4a91cfa6aa5ca9d87", "VFXDataMesh", VfxModelKind.Data);
        Add(types, "d78581a96eae8bf4398c282eb0b098bd", "VFXDataParticle", VfxModelKind.Data);
        Add(types, "f68759077adc0b143b6e1c101e82065e", "VFXDataSpawner", VfxModelKind.Data);

        Add(types, "f42a6449da2296343af0d8536de8588a", "VFXBasicGPUEvent", VfxModelKind.Context);
        Add(types, "2461f61b3c026d54db1951a4e17ab20e", "VFXBasicEvent", VfxModelKind.Context);
        Add(types, "4f39de6f4fce95c4d9240e5055b057a6", "VFXOutputEvent", VfxModelKind.Context);
        Add(types, "9dfea48843f53fc438eabc12a3a30abc", "VFXBasicInitialize", VfxModelKind.Context);
        Add(types, "73a13919d81fb7444849bae8b5c812a2", "VFXBasicSpawner", VfxModelKind.Context);
        Add(types, "2dc095764ededfa4bb32fa602511ea4b", "VFXBasicUpdate", VfxModelKind.Context);
        Add(types, "a0b9e6b9139e58d4c957ec54595da7d3", "VFXPlanarPrimitiveOutput", VfxModelKind.Context);
        Add(types, "756b42789c29cb74085def1da319fa0b", "VFXQuadStripOutput", VfxModelKind.Context);
        Add(types, "5c71b248ba46954449b47392dc6c3bf9", "VFXStaticMeshOutput", VfxModelKind.Context);
        Add(types, "9207a95457a3f994581249dbe0a3409d", "VFXSubgraphContext", VfxModelKind.Context);

        Add(types, "01ec2c1930009b04ea08905b47262415", "AttributeFromCurve", VfxModelKind.Block);
        Add(types, "e37a1b60bef671c4985a8912143d984d", "AttributeMassFromVolume", VfxModelKind.Block);
        Add(types, "5c286b53e648ef840b8153892fdbe169", "SetCustomAttribute", VfxModelKind.Block);
        Add(types, "956b68870e880b144bab17e5aa6e7e94", "ColorOverLife", VfxModelKind.Block);
        Add(types, "e0048ae9203b6994ba8076d59457fd7a", "FlipbookPlay", VfxModelKind.Block);
        Add(types, "b294673e879f9cf449cc9de536818ea9", "Drag", VfxModelKind.Block);
        Add(types, "c079bc84df7c7e94f88c8ae0d1b0691d", "Force", VfxModelKind.Block);
        Add(types, "e5dce54ae3368c042b26ab1f305e15b2", "Gravity", VfxModelKind.Block);
        Add(types, "63716c0daf1806941a123003dc6d7398", "Turbulence", VfxModelKind.Block);
        Add(types, "2af1b51cb5343364eb75bae8fceffd25", "GPUEventRate", VfxModelKind.Block);
        Add(types, "d16c6aeaef944094b9a1633041804207", "Orient", VfxModelKind.Block);
        Add(types, "567e63db3cc5de64c970ebc31d3f3af1", "CameraFade", VfxModelKind.Block);
        Add(types, "d03231f387e7ed54d888c74e4e13228e", "SubpixelAA", VfxModelKind.Block);
        Add(types, "3ab9b05052599f344a6b1ae204834e10", "PositionSequential", VfxModelKind.Block);
        Add(types, "a7280c30c72d50147ad334d9c445b6ca", "PositionSphere", VfxModelKind.Block);
        Add(types, "a971fa2e110a0ac42ac1d8dae408704b", "SetAttribute", VfxModelKind.Block);
        Add(types, "502b29d070f1295498ab1e61ca20da2a", "ScreenSpaceSize", VfxModelKind.Block);
        Add(types, "5e382412bb691334bb79457a6c127924", "VFXSpawnerBurst", VfxModelKind.Block);
        Add(types, "f05c6884b705ce14d82ae720f0ec209f", "VFXSpawnerConstantRate", VfxModelKind.Block);
        Add(types, "162e4a5d99325f14da009fce43aa54ba", "VFXSpawnerVariableRate", VfxModelKind.Block);
        Add(types, "709ca816312218f4ba70763d893c34c9", "VFXSpawnerSetAttribute", VfxModelKind.Block);
        Add(types, "4bfc68bea08ee074899e288b438a2e89", "VFXSpawnerCustomWrapper", VfxModelKind.Block);
        Add(types, "45fdf0bbbd1d59d4e883e734442050d7", "VFXSubgraphBlock", VfxModelKind.Block);

        Add(types, "c7acf5424f3655744af4b8f63298fa0f", "Add", VfxModelKind.Operator);
        Add(types, "ba941214d319b454f90d5480e85886f2", "AgeOverLifetime", VfxModelKind.Operator);
        Add(types, "9717a5f0d23f1d843aef2943f049a21d", "Branch", VfxModelKind.Operator);
        Add(types, "1fb2f8fde2589884fae38ab8bc886b6f", "CurlNoise", VfxModelKind.Operator);
        Add(types, "b8ee8a7543fa09e42a7c8616f60d2ad7", "Multiply", VfxModelKind.Operator);
        Add(types, "a30aeb734589f22468d3ed89a2ecc09c", "Noise", VfxModelKind.Operator);
        Add(types, "c8ac0ebcb5fd27b408f3700034222acb", "OneMinus", VfxModelKind.Operator);
        Add(types, "c42128e17c583714a909b4997c80c916", "Random", VfxModelKind.Operator);
        Add(types, "0a02ebe9815b1084495277ae39c6c270", "Remap", VfxModelKind.Operator);
        Add(types, "f8bcc906a6d398c46b18826714448709", "SampleCurve", VfxModelKind.Operator);
        Add(types, "0155ae97d9a75e3449c6d0603b79c2f4", "Subtract", VfxModelKind.Operator);
        Add(types, "fa71feae8df37b6479bb8bc6ab99f797", "VFXSubgraphOperator", VfxModelKind.Operator);

        Add(types, "486e063e1ed58c843942ea4122829ab1", "VFXAttributeParameter", VfxModelKind.Parameter);
        Add(types, "a72fbb93ebe17974e90a144ef2ec8ceb", "VFXDynamicBuiltInParameter", VfxModelKind.Parameter);
        Add(types, "955b0c175a6f3bb4582e92f3de8f0626", "VFXInlineOperator", VfxModelKind.Parameter);
        Add(types, "330e0fca1717dde4aaa144f48232aa64", "VFXParameter", VfxModelKind.Parameter);

        Add(types, "c117b74c5c58db542bffe25c78fe92db", "VFXSlotAnimationCurve", VfxModelKind.Slot);
        Add(types, "b4c11ff25089a324daf359f4b0629b33", "VFXSlotBool", VfxModelKind.Slot);
        Add(types, "c82227d5759e296488798b1554a72a15", "VFXSlotColor", VfxModelKind.Slot);
        Add(types, "e8f2b4a846fd4c14a893cde576ad172b", "VFXSlotDirection", VfxModelKind.Slot);
        Add(types, "f780aa281814f9842a7c076d436932e7", "VFXSlotFloat", VfxModelKind.Slot);
        Add(types, "1b2b751071c7fc14f9fa503163991826", "VFXSlotFloat2", VfxModelKind.Slot);
        Add(types, "ac39bd03fca81b849929b9c966f1836a", "VFXSlotFloat3", VfxModelKind.Slot);
        Add(types, "c499060cea9bbb24b8d723eafa343303", "VFXSlotFloat4", VfxModelKind.Slot);
        Add(types, "76f778ff57c4e8145b9681fe3268d8e9", "VFXSlotGradient", VfxModelKind.Slot);
        Add(types, "4d246e354feb93041a837a9ef59437cb", "VFXSlotInt", VfxModelKind.Slot);
        Add(types, "b47b8679b468b7347a00cdd50589bc9f", "VFXSlotMesh", VfxModelKind.Slot);
        Add(types, "5265657162cc1a241bba03a3b0476d99", "VFXSlotPosition", VfxModelKind.Slot);
        Add(types, "70a331b1d86cc8d4aa106ccbe0da5852", "VFXSlotTexture2D", VfxModelKind.Slot);
        Add(types, "3e3f628d80ffceb489beac74258f9cf7", "VFXSlotTransform", VfxModelKind.Slot);
        Add(types, "c52d920e7fff73b498050a6b3c4404ca", "VFXSlotUint", VfxModelKind.Slot);
        Add(types, "a9f9544b71b7dab44a4644b6807e8bf6", "VFXSlotVector", VfxModelKind.Slot);
        Add(types, "1b605c022ee79394a8a776c0869b3f9a", "VFXSlot", VfxModelKind.Slot);

        // Serialized by official samples when HDRP is installed. Anity preserves it for import
        // diagnostics, but it is deliberately not a product path: Anity is URP-only.
        Add(types, "081ffb0090424ba4cb05370a42ead6b9", "VFXHDRPSubOutput", VfxModelKind.External, false);

        return new ReadOnlyDictionary<string, VfxScriptType>(types);
    }

    private static void Add(
        Dictionary<string, VfxScriptType> types,
        string guid,
        string typeName,
        VfxModelKind kind,
        bool isProductSupported = true)
    {
        var type = new VfxScriptType(NormalizeGuid(guid), typeName, kind, isProductSupported);
        if (!types.TryAdd(type.Guid, type))
            throw new InvalidOperationException("Duplicate VFX script GUID '" + type.Guid + "'.");
    }
}
