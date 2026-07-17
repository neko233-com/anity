using UnityEditor.VFX.Generation;
using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using UnityEngine.VFX;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxAttributeSchemaTests
{
    [Fact]
    public void Catalog_MatchesUnity14StoredAndVariadicCounts()
    {
        Assert.Equal(44, VfxAttributeCatalog.All.Count);
        Assert.Equal(40, VfxAttributeCatalog.Stored.Count);
        Assert.Equal(4, VfxAttributeCatalog.All.Count(attribute => attribute.Variadic == VfxAttributeVariadic.True));
    }

    [Fact]
    public void Catalog_PreservesReadWriteAndLocalOnlyRules()
    {
        Assert.True(VfxAttributeCatalog.Find("seed").IsReadOnly);
        Assert.True(VfxAttributeCatalog.Find("eventCount").IsWriteOnly);
        Assert.True(VfxAttributeCatalog.Find("eventCount").IsLocalOnly);
        Assert.True(VfxAttributeCatalog.Find("particleIndexInStrip").IsReadOnly);
        Assert.True(VfxAttributeCatalog.Find("particleIndexInStrip").IsLocalOnly);
        Assert.True(VfxAttributeCatalog.Find("stripAlive").IsInternal);
    }

    [Fact]
    public void Catalog_PreservesOfficialDefaultsAndSpaceSemantics()
    {
        Assert.Equal("0.1", VfxAttributeCatalog.Find("size").DefaultHlsl);
        Assert.Equal("float3(0.0, 0.0, 1.0)", VfxAttributeCatalog.Find("direction").DefaultHlsl);
        Assert.Equal(VfxSpaceableType.Position, VfxAttributeCatalog.Find("position").Space);
        Assert.Equal(VfxSpaceableType.Vector, VfxAttributeCatalog.Find("velocity").Space);
        Assert.Equal("bool", VfxAttributeCatalog.Find("alive").HlslType);
    }

    [Theory]
    [InlineData(0, "scaleX")]
    [InlineData(1, "scaleY")]
    [InlineData(2, "scaleZ")]
    [InlineData(3, "scaleX,scaleY")]
    [InlineData(4, "scaleX,scaleZ")]
    [InlineData(5, "scaleY,scaleZ")]
    [InlineData(6, "scaleX,scaleY,scaleZ")]
    public void Catalog_ExpandsEveryOfficialVariadicChannelCombination(
        int channels,
        string expected)
    {
        IReadOnlyList<VfxAttributeDefinition> result = VfxAttributeCatalog.Expand(
            VfxAttributeCatalog.Find("scale"),
            (VfxVariadicChannels)channels);

        Assert.Equal(expected, string.Join(",", result.Select(attribute => attribute.Name)));
    }

    [Theory]
    [InlineData(0, "float")]
    [InlineData(1, "float2")]
    [InlineData(2, "float3")]
    [InlineData(3, "float4")]
    [InlineData(4, "bool")]
    [InlineData(5, "uint")]
    [InlineData(6, "int")]
    public void Catalog_MapsEveryCustomAttributeSignature(int signature, string hlslType)
        => Assert.Equal(hlslType, VfxAttributeCatalog.CreateCustom("customValue", signature).HlslType);

    [Theory]
    [InlineData("")]
    [InlineData("1value")]
    [InlineData("has space")]
    [InlineData("has-dash")]
    [InlineData("颜色")]
    public void Catalog_RejectsInvalidCustomShaderIdentifiers(string name)
        => Assert.Throws<InvalidDataException>(() => VfxAttributeCatalog.CreateCustom(name, 0));

    [Fact]
    public void UsageSet_ParsesSetAttributeSettingsAndWriteMode()
    {
        VfxSerializedAttributeUsage usage = Assert.Single(ParseUsage(
            Model(20, SetAttributeGuid, "attribute: position\n  Composition: 0\n  Source: 0\n  Random: 0\n  channels: 6")));

        Assert.Equal("position", Assert.Single(usage.Attributes).Name);
        Assert.Equal(VfxAttributeMode.Write, usage.Mode);
        Assert.Equal(VfxAttributeComposition.Overwrite, usage.Composition);
        Assert.False(usage.RequiresRandomSeed);
    }

    [Fact]
    public void UsageSet_ExpandsVariadicAttributeAndPreservesSelectedChannels()
    {
        VfxSerializedAttributeUsage usage = Assert.Single(ParseUsage(
            Model(20, SetAttributeGuid, "attribute: scale\n  Composition: 1\n  Source: 0\n  Random: 0\n  channels: 4")));

        Assert.Equal(new[] { "scaleX", "scaleZ" }, usage.Attributes.Select(attribute => attribute.Name));
        Assert.Equal(VfxAttributeMode.ReadWrite, usage.Mode);
    }

    [Fact]
    public void UsageSet_ParsesCurrentAndSourceAttributeParameters()
    {
        VfxSerializedAttributeUsage current = Assert.Single(ParseUsage(
            Model(20, AttributeParameterGuid, "attribute: velocity\n  location: 0\n  mask: xyz")));
        VfxSerializedAttributeUsage source = Assert.Single(ParseUsage(
            Model(20, AttributeParameterGuid, "attribute: velocity\n  location: 1\n  mask: xyz")));

        Assert.Equal(VfxAttributeMode.Read, current.Mode);
        Assert.Equal(VfxAttributeMode.ReadSource, source.Mode);
    }

    [Fact]
    public void UsageSet_ParsesCustomAttributeTypeAndRandomSeedRequirement()
    {
        VfxSerializedAttributeUsage usage = Assert.Single(ParseUsage(
            Model(20, SetCustomAttributeGuid,
                "attribute: customVelocity\n  Composition: 2\n  Random: 1\n  AttributeType: 2")));

        Assert.True(usage.IsCustom);
        Assert.Equal("float3", Assert.Single(usage.Attributes).HlslType);
        Assert.True(usage.RequiresRandomSeed);
    }

    [Fact]
    public void UsageSet_RejectsImpossibleSourceRandomCombination()
        => Assert.Throws<InvalidDataException>(() => ParseUsage(
            Model(20, SetAttributeGuid, "attribute: position\n  Composition: 0\n  Source: 1\n  Random: 2\n  channels: 6")));

    [Fact]
    public void UsageSet_RejectsWritingReadOnlyAttribute()
        => Assert.Throws<InvalidDataException>(() => ParseUsage(
            Model(20, SetAttributeGuid, "attribute: seed\n  Composition: 0\n  Source: 0\n  Random: 0\n  channels: 6")));

    [Fact]
    public void UsageSet_ParsesSpawnerSetAttributeLowercaseRandomMode()
    {
        VfxSerializedAttributeUsage usage = Assert.Single(ParseUsage(
            Model(20, SpawnerSetAttributeGuid, "attribute: position\n  randomMode: 0")));

        Assert.Equal(VfxAttributeMode.Write, usage.Mode);
        Assert.Equal(VfxAttributeComposition.Overwrite, usage.Composition);
        Assert.Equal(VfxAttributeRandomMode.Off, usage.RandomMode);
        Assert.Equal(VfxAttributeValueSource.Slot, usage.ValueSource);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void UsageSet_ParsesSpawnerRandomModes(
        int serialized,
        int expected)
    {
        VfxSerializedAttributeUsage usage = Assert.Single(ParseUsage(
            Model(20, SpawnerSetAttributeGuid, $"attribute: color\n  randomMode: {serialized}")));

        Assert.Equal((VfxAttributeRandomMode)expected, usage.RandomMode);
        Assert.True(usage.RequiresRandomSeed);
    }

    [Fact]
    public void UsageSet_RejectsInvalidSpawnerRandomMode()
        => Assert.Throws<InvalidDataException>(() => ParseUsage(
            Model(20, SpawnerSetAttributeGuid, "attribute: position\n  randomMode: 9")));

    [Theory]
    [InlineData("spawnCount")]
    [InlineData("spawnTime")]
    public void UsageSet_SpawnerMayWriteOfficialSpawnOnlyAttributes(string attribute)
    {
        VfxSerializedAttributeUsage usage = Assert.Single(ParseUsage(
            Model(20, SpawnerSetAttributeGuid, $"attribute: {attribute}\n  randomMode: 0")));

        Assert.Equal(attribute, Assert.Single(usage.Attributes).Name);
        Assert.True(Assert.Single(usage.Attributes).IsReadOnly);
        Assert.Equal(VfxAttributeMode.Write, usage.Mode);
    }

    [Fact]
    public void UsageSet_SpawnerVariadicDefaultsToAllChannels()
    {
        VfxSerializedAttributeUsage usage = Assert.Single(ParseUsage(
            Model(20, SpawnerSetAttributeGuid, "attribute: scale\n  randomMode: 0")));

        Assert.Equal(new[] { "scaleX", "scaleY", "scaleZ" },
            usage.Attributes.Select(attribute => attribute.Name));
    }

    [Theory]
    [InlineData(0, "position = Position;")]
    [InlineData(1, "position += Position;")]
    [InlineData(2, "position *= Position;")]
    [InlineData(3, "position = lerp(position,Position,Blend);")]
    public void CodeGenerator_EmitsEveryOfficialComposition(int composition, string expected)
    {
        VfxSerializedAttributeUsage usage = Assert.Single(ParseUsage(
            Model(20, SetAttributeGuid,
                $"attribute: position\n  Composition: {composition}\n  Source: 0\n  Random: 0\n  channels: 6")));

        Assert.Equal(expected, VfxAttributeCodeGenerator.GenerateSetAttributeStatement(usage));
    }

    [Fact]
    public void CodeGenerator_VariadicStatementUsesSelectedTargetsAndPackedInputChannels()
    {
        VfxSerializedAttributeUsage usage = Assert.Single(ParseUsage(
            Model(20, SetAttributeGuid, "attribute: scale\n  Composition: 0\n  Source: 0\n  Random: 0\n  channels: 4")));

        Assert.Equal("scaleX = Scale.x;\nscaleZ = Scale.y;", VfxAttributeCodeGenerator.GenerateSetAttributeStatement(usage));
    }

    [Theory]
    [InlineData(1, "position = lerp(A,B,RAND3);")]
    [InlineData(2, "position = lerp(A,B,RAND);")]
    public void CodeGenerator_EmitsPerComponentAndUniformRandomMacros(int random, string expected)
    {
        VfxSerializedAttributeUsage usage = Assert.Single(ParseUsage(
            Model(20, SetAttributeGuid,
                $"attribute: position\n  Composition: 0\n  Source: 0\n  Random: {random}\n  channels: 6")));

        Assert.Equal(expected, VfxAttributeCodeGenerator.GenerateSetAttributeStatement(usage));
    }

    [Fact]
    public void CodeGenerator_CustomAttributeUsesOfficialUnderscoreLocalName()
    {
        VfxSerializedAttributeUsage usage = Assert.Single(ParseUsage(
            Model(20, SetCustomAttributeGuid,
                "attribute: stripProgress\n  Composition: 0\n  Random: 0\n  AttributeType: 0")));

        Assert.Equal("stripProgress = _StripProgress;", VfxAttributeCodeGenerator.GenerateSetAttributeStatement(usage));
    }

    [Fact]
    public void CodeGenerator_StructUsesCatalogOrderAndAddsSeedForRandomBlock()
    {
        VfxAttributeUsageSet usageSet = ParseUsageSet(
            Model(20, SetAttributeGuid, "attribute: position\n  Composition: 0\n  Source: 0\n  Random: 2\n  channels: 6"),
            Model(30, SetAttributeGuid, "attribute: size\n  Composition: 0\n  Source: 0\n  Random: 0\n  channels: 6"));

        Assert.Equal(
            "struct VFXAttributes\n{\n    uint seed;\n    float3 position;\n    float size;\n};\n",
            VfxAttributeCodeGenerator.GenerateAttributeStruct(usageSet));
    }

    [Fact]
    public void OfficialUnity14Fixtures_AllAttributeModelsPassTypedSchemaGate_WhenAvailable()
    {
        string? package = FindPackage();
        if (package is null) return;
        string[] fixtures = Directory.EnumerateFiles(package, "*.vfx", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(12, fixtures.Length);
        int usages = 0;
        int compiledAttributeParameters = 0;
        foreach (string fixture in fixtures)
        {
            VfxTypedGraph graph = VfxTypedGraph.Build(VfxYamlAsset.Parse(File.ReadAllText(fixture)));
            VfxAttributeUsageSet usageSet = VfxAttributeUsageSet.Create(graph);
            usages += usageSet.Usages.Count;
            _ = VfxAttributeCodeGenerator.GenerateAttributeStruct(usageSet);
            foreach (VfxSerializedAttributeUsage usage in usageSet.Usages.Where(usage =>
                         usage.Model.ScriptType.TypeName is "SetAttribute" or "SetCustomAttribute"))
                _ = VfxAttributeCodeGenerator.GenerateSetAttributeStatement(usage);
            foreach (VfxModel parameter in graph.Parameters.Where(model =>
                         model.ScriptType.TypeName == "VFXAttributeParameter"))
            {
                foreach (long outputSlotId in parameter.OutputSlotIds)
                    _ = VfxExpressionCompiler.Compile(graph, outputSlotId);
                compiledAttributeParameters++;
            }
        }
        Assert.Equal(80, usages);
        Assert.Equal(6, compiledAttributeParameters);
    }

    [Fact]
    public void OfficialUnity14SimpleParticle_CompilesConstantRateSpawnerProgram_WhenAvailable()
    {
        string? package = FindPackage();
        if (package is null) return;
        string fixture = Path.Combine(package, "Editor", "Templates", "SimpleParticleSystem.vfx");
        if (!File.Exists(fixture)) return;
        VfxTypedGraph graph = VfxTypedGraph.Build(VfxYamlAsset.Parse(File.ReadAllText(fixture)));

        VFXRuntimeAssetData data = VFXRuntimeAssetData.Deserialize(VfxRuntimeAssetCompiler.Compile(graph));

        VFXRuntimeSpawnerProgramData program = Assert.Single(data.SpawnerPrograms);
        VFXRuntimeSpawnerBlockData block = Assert.Single(program.Blocks);
        Assert.Equal(VFXRuntimeSpawnerBlockKind.ConstantRate, block.Kind);
        Assert.Equal(16f, block.ValueMin);
        Assert.Equal(block.ValueMin, block.ValueMax);
        Assert.Equal(VisualEffectAsset.PlayEventName,
            Assert.Single(program.Controls.Where(control => control.InputSlotIndex == 0)).EventName);
        Assert.NotEmpty(program.Outputs);
    }

    [Fact]
    public void OfficialUnity14SimpleParticle_CompilesOrderedCustomSpawnerCallback_WhenAvailable()
    {
        string? package = FindPackage();
        if (package is null) return;
        string fixture = Path.Combine(package, "Editor", "Templates", "SimpleParticleSystem.vfx");
        if (!File.Exists(fixture)) return;
        string source = File.ReadAllText(fixture);
        VfxTypedGraph original = VfxTypedGraph.Build(VfxYamlAsset.Parse(source));
        long spawnerId = Assert.Single(original.ModelsByFileId.Values.Where(model =>
            model.ScriptType.TypeName == "VFXBasicSpawner")).FileId;
        const long blockId = 930001;
        const long deltaSlotId = 930002;
        const long markerSlotId = 930003;
        string marker = $"--- !u!114 &{spawnerId}\n";
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        int end = source.IndexOf("--- !u!", start + marker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        string document = source[start..end].Replace(
            "  m_Children:\n",
            $"  m_Children:\n  - {{fileID: {blockId}}}\n",
            StringComparison.Ordinal);
        source = source[..start] + document + source[end..] +
                 SpawnerCustomCallbackBlock(
                     blockId, spawnerId, new[] { deltaSlotId, markerSlotId }) +
                 SpawnerSlot(deltaSlotId, blockId, "VFXSlotFloat",
                     "f780aa281814f9842a7c076d436932e7", "SpawnDelta", "System.Single", "2") +
                 SpawnerSlot(markerSlotId, blockId, "VFXSlotFloat",
                     "f780aa281814f9842a7c076d436932e7", "Marker", "System.Single", "100");

        VFXRuntimeAssetData data = VFXRuntimeAssetData.Deserialize(
            VfxRuntimeAssetCompiler.Compile(VfxTypedGraph.Build(VfxYamlAsset.Parse(source))));

        VFXRuntimeSpawnerProgramData program = Assert.Single(data.SpawnerPrograms);
        VFXRuntimeSpawnerBlockData callback = program.Blocks[0];
        Assert.Equal(VFXRuntimeSpawnerBlockKind.CustomCallback, callback.Kind);
        Assert.Equal(
            "Tests.ProbeSpawnerCallbacks, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
            callback.CallbackTypeName);
        Assert.Equal(new[] { "SpawnDelta", "Marker" },
            callback.CallbackValues.Select(value => value.Name));
        Assert.Equal(2f, BitConverter.Int32BitsToSingle(unchecked((int)callback.CallbackValues[0].Words[0])));
        Assert.Equal(100f, BitConverter.Int32BitsToSingle(unchecked((int)callback.CallbackValues[1].Words[0])));
        Assert.Equal(data.EventAttributes.Sum(attribute => attribute.SizeWords), program.EventStrideWords);
        Assert.Equal(VFXRuntimeSpawnerBlockKind.ConstantRate, program.Blocks[1].Kind);
    }

    [Fact]
    public void OfficialUnity14SimpleParticle_CompilesDirectExposedPropertyCallbackLink_WhenAvailable()
    {
        string? package = FindPackage();
        if (package is null) return;
        string fixture = Path.Combine(package, "Editor", "Templates", "SimpleParticleSystem.vfx");
        if (!File.Exists(fixture)) return;
        string source = File.ReadAllText(fixture);
        VfxTypedGraph original = VfxTypedGraph.Build(VfxYamlAsset.Parse(source));
        long graphId = original.Graph.FileId;
        long spawnerId = Assert.Single(original.ModelsByFileId.Values.Where(model =>
            model.ScriptType.TypeName == "VFXBasicSpawner")).FileId;
        const long blockId = 931001;
        const long inputSlotId = 931002;
        const long markerSlotId = 931003;
        const long parameterId = 931004;
        const long outputSlotId = 931005;
        source = AddChild(source, spawnerId, blockId);
        source = AddChild(source, graphId, parameterId);
        source += SpawnerCustomCallbackBlock(blockId, spawnerId, new[] { inputSlotId, markerSlotId }) +
                  SpawnerSlot(inputSlotId, blockId, "VFXSlotFloat",
                      "f780aa281814f9842a7c076d436932e7", "SpawnDelta", "System.Single", "2", outputSlotId) +
                  SpawnerSlot(markerSlotId, blockId, "VFXSlotFloat",
                      "f780aa281814f9842a7c076d436932e7", "Marker", "System.Single", "100") +
                  ExposedParameter(parameterId, graphId, outputSlotId, "Dynamic Delta") +
                  SpawnerSlot(outputSlotId, parameterId, "VFXSlotFloat",
                      "f780aa281814f9842a7c076d436932e7", "o", "System.Single", "6", inputSlotId, 1);

        VFXRuntimeAssetData data = VFXRuntimeAssetData.Deserialize(
            VfxRuntimeAssetCompiler.Compile(VfxTypedGraph.Build(VfxYamlAsset.Parse(source))));

        VFXRuntimeExposedPropertyData property = Assert.Single(data.ExposedProperties);
        Assert.Equal("Dynamic Delta", property.Name);
        Assert.Equal(6f, BitConverter.Int32BitsToSingle(unchecked((int)property.DefaultWords[0])));
        VFXRuntimeSpawnerBlockData callback = data.SpawnerPrograms[0].Blocks[0];
        Assert.Equal("Dynamic Delta", callback.CallbackValues[0].SourcePropertyName);
    }

    [Fact]
    public void OfficialUnity14SimpleParticle_CompilesCallbackExpressionDag_WhenAvailable()
    {
        string? package = FindPackage();
        if (package is null) return;
        string fixture = Path.Combine(package, "Editor", "Templates", "SimpleParticleSystem.vfx");
        if (!File.Exists(fixture)) return;
        string source = File.ReadAllText(fixture);
        VfxTypedGraph original = VfxTypedGraph.Build(VfxYamlAsset.Parse(source));
        long graphId = original.Graph.FileId;
        long spawnerId = Assert.Single(original.ModelsByFileId.Values.Where(model =>
            model.ScriptType.TypeName == "VFXBasicSpawner")).FileId;
        const long blockId = 932001;
        const long callbackInputId = 932002;
        const long markerSlotId = 932003;
        const long parameterId = 932004;
        const long parameterOutputId = 932005;
        const long operatorId = 932006;
        const long operatorInputAId = 932007;
        const long operatorInputBId = 932008;
        const long operatorOutputId = 932009;
        source = AddChild(source, spawnerId, blockId);
        source = AddChild(source, graphId, parameterId);
        source = AddChild(source, graphId, operatorId);
        source += SpawnerCustomCallbackBlock(blockId, spawnerId,
                      new[] { callbackInputId, markerSlotId }) +
                  SpawnerSlot(callbackInputId, blockId, "VFXSlotFloat",
                      "f780aa281814f9842a7c076d436932e7", "SpawnDelta", "System.Single", "0",
                      operatorOutputId) +
                  SpawnerSlot(markerSlotId, blockId, "VFXSlotFloat",
                      "f780aa281814f9842a7c076d436932e7", "Marker", "System.Single", "100") +
                  ExposedParameter(parameterId, graphId, parameterOutputId, "Dynamic Delta") +
                  SpawnerSlot(parameterOutputId, parameterId, "VFXSlotFloat",
                      "f780aa281814f9842a7c076d436932e7", "o", "System.Single", "6",
                      operatorInputAId, 1) +
                  Operator(operatorId, graphId, "c7acf5424f3655744af4b8f63298fa0f",
                      new[] { operatorInputAId, operatorInputBId }, new[] { operatorOutputId }) +
                  SpawnerSlot(operatorInputAId, operatorId, "VFXSlotFloat",
                      "f780aa281814f9842a7c076d436932e7", "a", "System.Single", "0",
                      parameterOutputId) +
                  SpawnerSlot(operatorInputBId, operatorId, "VFXSlotFloat",
                      "f780aa281814f9842a7c076d436932e7", "b", "System.Single", "2") +
                  SpawnerSlot(operatorOutputId, operatorId, "VFXSlotFloat",
                      "f780aa281814f9842a7c076d436932e7", "o", "System.Single", "0",
                      callbackInputId, 1);

        VFXRuntimeAssetData data = VFXRuntimeAssetData.Deserialize(
            VfxRuntimeAssetCompiler.Compile(VfxTypedGraph.Build(VfxYamlAsset.Parse(source))));

        VFXRuntimeSpawnerExpressionValueData value = data.SpawnerPrograms[0].Blocks[0].CallbackValues[0];
        Assert.Null(value.SourcePropertyName);
        VFXRuntimeExpressionProgramData expression = Assert.IsType<VFXRuntimeExpressionProgramData>(value.Expression);
        Assert.Equal(new[]
        {
            VFXRuntimeExpressionOperation.ExposedProperty,
            VFXRuntimeExpressionOperation.Constant,
            VFXRuntimeExpressionOperation.Add
        }, expression.Instructions.Select(instruction => instruction.Operation));
        Assert.Equal("Dynamic Delta", expression.Instructions[0].PropertyName);
        Assert.Equal(2, expression.ResultIndex);
    }

    [Fact]
    public void OfficialUnity14SimpleParticle_CompilesDynamicBuiltInCallbackInput_WhenAvailable()
    {
        string? package = FindPackage();
        if (package is null) return;
        string fixture = Path.Combine(package, "Editor", "Templates", "SimpleParticleSystem.vfx");
        if (!File.Exists(fixture)) return;
        string source = File.ReadAllText(fixture);
        VfxTypedGraph original = VfxTypedGraph.Build(VfxYamlAsset.Parse(source));
        long graphId = original.Graph.FileId;
        long spawnerId = Assert.Single(original.ModelsByFileId.Values.Where(model =>
            model.ScriptType.TypeName == "VFXBasicSpawner")).FileId;
        const long blockId = 933001;
        const long callbackInputId = 933002;
        const long markerSlotId = 933003;
        const long builtInId = 933004;
        const long builtInOutputId = 933005;
        source = AddChild(source, spawnerId, blockId);
        source = AddChild(source, graphId, builtInId);
        source += SpawnerCustomCallbackBlock(blockId, spawnerId,
                      new[] { callbackInputId, markerSlotId }) +
                  SpawnerSlot(callbackInputId, blockId, "VFXSlotFloat",
                      "f780aa281814f9842a7c076d436932e7", "SpawnDelta", "System.Single", "0",
                      builtInOutputId) +
                  SpawnerSlot(markerSlotId, blockId, "VFXSlotFloat",
                      "f780aa281814f9842a7c076d436932e7", "Marker", "System.Single", "100") +
                  DynamicBuiltInParameter(builtInId, graphId, builtInOutputId, 1 << 4) +
                  SpawnerSlot(builtInOutputId, builtInId, "VFXSlotFloat",
                      "f780aa281814f9842a7c076d436932e7", "playRate", "System.Single", "0",
                      callbackInputId, 1);

        VFXRuntimeAssetData data = VFXRuntimeAssetData.Deserialize(
            VfxRuntimeAssetCompiler.Compile(VfxTypedGraph.Build(VfxYamlAsset.Parse(source))));

        VFXRuntimeExpressionProgramData expression = Assert.IsType<VFXRuntimeExpressionProgramData>(
            data.SpawnerPrograms[0].Blocks[0].CallbackValues[0].Expression);
        Assert.Equal(VFXRuntimeExpressionOperation.VfxPlayRate,
            Assert.Single(expression.Instructions).Operation);
    }

    [Fact]
    public void OfficialUnity14SimpleParticle_CompilesSetSpawnEventAttributeOpcode_WhenAvailable()
    {
        string? package = FindPackage();
        if (package is null) return;
        string fixture = Path.Combine(package, "Editor", "Templates", "SimpleParticleSystem.vfx");
        if (!File.Exists(fixture)) return;
        string source = File.ReadAllText(fixture);
        VfxTypedGraph original = VfxTypedGraph.Build(VfxYamlAsset.Parse(source));
        long spawnerId = Assert.Single(original.ModelsByFileId.Values.Where(model =>
            model.ScriptType.TypeName == "VFXBasicSpawner")).FileId;
        const long blockId = 920001;
        const long slotId = 920002;
        string marker = $"--- !u!114 &{spawnerId}\n";
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        int end = source.IndexOf("--- !u!", start + marker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        string document = source[start..end];
        document = document.Replace(
            "  m_Children:\n",
            $"  m_Children:\n  - {{fileID: {blockId}}}\n",
            StringComparison.Ordinal);
        source = source[..start] + document + source[end..] +
                 SpawnerSetAttributeBlock(blockId, spawnerId, new[] { slotId }, "size", 0) +
                 SpawnerSlot(slotId, blockId, "VFXSlotFloat",
                     "f780aa281814f9842a7c076d436932e7", "size",
                     "System.Single", "-4.5");

        VFXRuntimeAssetData data = VFXRuntimeAssetData.Deserialize(
            VfxRuntimeAssetCompiler.Compile(VfxTypedGraph.Build(VfxYamlAsset.Parse(source))));

        VFXRuntimeSpawnerProgramData program = Assert.Single(data.SpawnerPrograms);
        VFXRuntimeSpawnerBlockData block = Assert.Single(program.Blocks.Where(candidate =>
            candidate.Kind == VFXRuntimeSpawnerBlockKind.SetAttribute));
        VFXRuntimeAttributeData size = Assert.Single(data.EventAttributes.Where(attribute =>
            attribute.Name == "size"));
        Assert.Equal(size.OffsetWords, block.TargetOffsetWords);
        Assert.Equal(VFXRuntimeValueType.Float, block.TargetValueType);
        Assert.Equal(VFXRuntimeInitializeRandomMode.Off, block.RandomMode);
        Assert.Equal(unchecked((uint)BitConverter.SingleToInt32Bits(-4.5f)), Assert.Single(block.ValueA));
        Assert.Equal(block.ValueA, block.ValueB);
        Assert.Equal(data.EventAttributes.Sum(attribute => attribute.SizeWords), program.EventStrideWords);
    }

    [Fact]
    public void OfficialUnity14SimpleParticle_CompilesSpawnCountAtReservedOffsetZeroInBlockOrder_WhenAvailable()
    {
        string? package = FindPackage();
        if (package is null) return;
        string fixture = Path.Combine(package, "Editor", "Templates", "SimpleParticleSystem.vfx");
        if (!File.Exists(fixture)) return;
        string source = File.ReadAllText(fixture);
        VfxTypedGraph original = VfxTypedGraph.Build(VfxYamlAsset.Parse(source));
        long spawnerId = Assert.Single(original.ModelsByFileId.Values.Where(model =>
            model.ScriptType.TypeName == "VFXBasicSpawner")).FileId;
        const long blockId = 920021;
        const long slotId = 920022;
        string marker = $"--- !u!114 &{spawnerId}\n";
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        int end = source.IndexOf("--- !u!", start + marker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        string document = source[start..end].Replace(
            "  m_Children:\n",
            $"  m_Children:\n  - {{fileID: {blockId}}}\n",
            StringComparison.Ordinal);
        source = source[..start] + document + source[end..] +
                 SpawnerSetAttributeBlock(blockId, spawnerId, new[] { slotId }, "spawnCount", 0) +
                 SpawnerSlot(slotId, blockId, "VFXSlotFloat",
                     "f780aa281814f9842a7c076d436932e7", "spawnCount",
                     "System.Single", "3.0");

        VFXRuntimeSpawnerProgramData program = Assert.Single(VFXRuntimeAssetData.Deserialize(
            VfxRuntimeAssetCompiler.Compile(VfxTypedGraph.Build(VfxYamlAsset.Parse(source)))).SpawnerPrograms);

        Assert.Equal(VFXRuntimeSpawnerBlockKind.SetAttribute, program.Blocks[0].Kind);
        Assert.Equal(0, program.Blocks[0].TargetOffsetWords);
        Assert.Equal(VFXRuntimeSpawnerBlockKind.ConstantRate, program.Blocks[1].Kind);
    }

    [Fact]
    public void OfficialUnity14SimpleParticle_CompilesSetAttributeOnlySpawner_WhenAvailable()
    {
        string? package = FindPackage();
        if (package is null) return;
        string fixture = Path.Combine(package, "Editor", "Templates", "SimpleParticleSystem.vfx");
        if (!File.Exists(fixture)) return;
        string source = File.ReadAllText(fixture);
        VfxTypedGraph original = VfxTypedGraph.Build(VfxYamlAsset.Parse(source));
        VfxModel spawner = Assert.Single(original.ModelsByFileId.Values.Where(model =>
            model.ScriptType.TypeName == "VFXBasicSpawner"));
        long rateId = Assert.Single(spawner.ChildrenIds.Where(id =>
            original.ModelsByFileId[id].ScriptType.TypeName == "VFXSpawnerConstantRate"));
        string rateMarker = $"--- !u!114 &{rateId}\n";
        int rateStart = source.IndexOf(rateMarker, StringComparison.Ordinal);
        int rateEnd = source.IndexOf("--- !u!", rateStart + rateMarker.Length, StringComparison.Ordinal);
        Assert.True(rateStart >= 0 && rateEnd > rateStart);
        string rateDocument = source[rateStart..rateEnd].Replace(
            "  m_Disabled: 0\n", "  m_Disabled: 1\n", StringComparison.Ordinal);
        source = source[..rateStart] + rateDocument + source[rateEnd..];
        const long blockId = 920031;
        const long slotId = 920032;
        string marker = $"--- !u!114 &{spawner.FileId}\n";
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        int end = source.IndexOf("--- !u!", start + marker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        string document = source[start..end]
            .Replace("  m_Children:\n",
                $"  m_Children:\n  - {{fileID: {blockId}}}\n",
                StringComparison.Ordinal);
        source = source[..start] + document + source[end..] +
                 SpawnerSetAttributeBlock(blockId, spawner.FileId, new[] { slotId }, "spawnCount", 0) +
                 SpawnerSlot(slotId, blockId, "VFXSlotFloat",
                     "f780aa281814f9842a7c076d436932e7", "spawnCount",
                     "System.Single", "3.0");

        VFXRuntimeSpawnerProgramData program = Assert.Single(VFXRuntimeAssetData.Deserialize(
            VfxRuntimeAssetCompiler.Compile(VfxTypedGraph.Build(VfxYamlAsset.Parse(source)))).SpawnerPrograms);

        VFXRuntimeSpawnerBlockData block = Assert.Single(program.Blocks);
        Assert.Equal(VFXRuntimeSpawnerBlockKind.SetAttribute, block.Kind);
        Assert.Equal(0, block.TargetOffsetWords);
        Assert.NotEmpty(program.Outputs);
    }

    [Theory]
    [InlineData(1, (int)VFXRuntimeInitializeRandomMode.PerComponent)]
    [InlineData(2, (int)VFXRuntimeInitializeRandomMode.Uniform)]
    public void OfficialUnity14SimpleParticle_CompilesRandomSetSpawnEventColor_WhenAvailable(
        int serializedRandomMode,
        int expectedRuntimeMode)
    {
        string? package = FindPackage();
        if (package is null) return;
        string fixture = Path.Combine(package, "Editor", "Templates", "SimpleParticleSystem.vfx");
        if (!File.Exists(fixture)) return;
        string source = File.ReadAllText(fixture);
        VfxTypedGraph original = VfxTypedGraph.Build(VfxYamlAsset.Parse(source));
        long spawnerId = Assert.Single(original.ModelsByFileId.Values.Where(model =>
            model.ScriptType.TypeName == "VFXBasicSpawner")).FileId;
        const long blockId = 920011;
        long[] slotIds = { 920012, 920013 };
        string marker = $"--- !u!114 &{spawnerId}\n";
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        int end = source.IndexOf("--- !u!", start + marker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        string document = source[start..end].Replace(
            "  m_Children:\n",
            $"  m_Children:\n  - {{fileID: {blockId}}}\n",
            StringComparison.Ordinal);
        source = source[..start] + document + source[end..] +
                 SpawnerSetAttributeBlock(blockId, spawnerId, slotIds, "color", serializedRandomMode) +
                 SpawnerSlot(slotIds[0], blockId, "VFXSlotColor",
                     "c82227d5759e296488798b1554a72a15", "Min",
                     "UnityEngine.Color", "{\"r\":-1.0,\"g\":0.25,\"b\":0.5,\"a\":1.0}") +
                 SpawnerSlot(slotIds[1], blockId, "VFXSlotColor",
                     "c82227d5759e296488798b1554a72a15", "Max",
                     "UnityEngine.Color", "{\"r\":2.0,\"g\":0.75,\"b\":1.5,\"a\":1.0}");

        VFXRuntimeAssetData data = VFXRuntimeAssetData.Deserialize(
            VfxRuntimeAssetCompiler.Compile(VfxTypedGraph.Build(VfxYamlAsset.Parse(source))));

        VFXRuntimeSpawnerBlockData block = Assert.Single(Assert.Single(data.SpawnerPrograms).Blocks.Where(candidate =>
            candidate.Kind == VFXRuntimeSpawnerBlockKind.SetAttribute));
        Assert.Equal(VFXRuntimeValueType.Float3, block.TargetValueType);
        Assert.Equal((VFXRuntimeInitializeRandomMode)expectedRuntimeMode, block.RandomMode);
        Assert.Equal(new[] { -1f, 0.25f, 0.5f }, block.ValueA.Select(word =>
            BitConverter.Int32BitsToSingle(unchecked((int)word))));
        Assert.Equal(new[] { 2f, 0.75f, 1.5f }, block.ValueB.Select(word =>
            BitConverter.Int32BitsToSingle(unchecked((int)word))));
    }

    [Fact]
    public void OfficialUnity14SimpleParticle_CompilesAllSpawnerLoopOperands_WhenAvailable()
    {
        string? package = FindPackage();
        if (package is null) return;
        string fixture = Path.Combine(package, "Editor", "Templates", "SimpleParticleSystem.vfx");
        if (!File.Exists(fixture)) return;
        string source = File.ReadAllText(fixture);
        VfxTypedGraph original = VfxTypedGraph.Build(VfxYamlAsset.Parse(source));
        long spawnerId = Assert.Single(original.ModelsByFileId.Values.Where(model =>
            model.ScriptType.TypeName == "VFXBasicSpawner")).FileId;
        long[] slots = { 910001, 910002, 910003, 910004 };
        string marker = $"--- !u!114 &{spawnerId}\n";
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        int end = source.IndexOf("--- !u!", start + marker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        string document = source[start..end]
            .Replace("  m_InputSlots: []\n",
                "  m_InputSlots:\n" + string.Concat(slots.Select(id => $"  - {{fileID: {id}}}\n")),
                StringComparison.Ordinal)
            .Replace("  loopDuration: 0\n", "  loopDuration: 2\n", StringComparison.Ordinal)
            .Replace("  loopCount: 0\n", "  loopCount: 2\n", StringComparison.Ordinal)
            .Replace("  delayBeforeLoop: 0\n", "  delayBeforeLoop: 1\n", StringComparison.Ordinal)
            .Replace("  delayAfterLoop: 0\n", "  delayAfterLoop: 2\n", StringComparison.Ordinal);
        source = source[..start] + document + source[end..] +
                 SpawnerSlot(slots[0], spawnerId, "VFXSlotFloat2",
                     "1b2b751071c7fc14f9fa503163991826", "LoopDuration",
                     "UnityEngine.Vector2", "{\"x\":1.25,\"y\":2.5}") +
                 SpawnerSlot(slots[1], spawnerId, "VFXSlotFloat2",
                     "1b2b751071c7fc14f9fa503163991826", "LoopCount",
                     "UnityEngine.Vector2", "{\"x\":1.25,\"y\":3.75}") +
                 SpawnerSlot(slots[2], spawnerId, "VFXSlotFloat",
                     "f780aa281814f9842a7c076d436932e7", "DelayBeforeLoop",
                     "System.Single", "0.125") +
                 SpawnerSlot(slots[3], spawnerId, "VFXSlotFloat2",
                     "1b2b751071c7fc14f9fa503163991826", "DelayAfterLoop",
                     "UnityEngine.Vector2", "{\"x\":0.25,\"y\":0.75}");

        VFXRuntimeAssetData data = VFXRuntimeAssetData.Deserialize(
            VfxRuntimeAssetCompiler.Compile(VfxTypedGraph.Build(VfxYamlAsset.Parse(source))));

        VFXRuntimeSpawnerProgramData program = Assert.Single(data.SpawnerPrograms);
        Assert.Equal(VFXRuntimeSpawnerValueMode.Random, program.LoopDurationMode);
        Assert.Equal((1.25f, 2.5f), (program.LoopDurationMin, program.LoopDurationMax));
        Assert.Equal(VFXRuntimeSpawnerValueMode.Random, program.LoopCountMode);
        Assert.Equal((1.25d, 3.75d), (program.LoopCountMin, program.LoopCountMax));
        Assert.Equal(VFXRuntimeSpawnerValueMode.Constant, program.DelayBeforeLoopMode);
        Assert.Equal((0.125f, 0.125f), (program.DelayBeforeLoopMin, program.DelayBeforeLoopMax));
        Assert.Equal(VFXRuntimeSpawnerValueMode.Random, program.DelayAfterLoopMode);
        Assert.Equal((0.25f, 0.75f), (program.DelayAfterLoopMin, program.DelayAfterLoopMax));
    }

    private static IReadOnlyList<VfxSerializedAttributeUsage> ParseUsage(params string[] models)
        => ParseUsageSet(models).Usages;

    private static VfxAttributeUsageSet ParseUsageSet(params string[] models)
    {
        long[] ids = models.Select(ReadFileId).ToArray();
        string source = Preamble + Graph(ids) + string.Concat(models) + Resource;
        return VfxAttributeUsageSet.Create(VfxTypedGraph.Build(VfxYamlAsset.Parse(source)));
    }

    private static string Graph(IEnumerable<long> children)
        => "--- !u!114 &10\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {GraphGuid}, type: 3}}\n" +
           "  m_Name: Graph\n  m_Parent: {fileID: 0}\n  m_Children:\n" +
           string.Concat(children.Select(id => $"  - {{fileID: {id}}}\n")) +
           "  m_InputSlots: []\n  m_OutputSlots: []\n";

    private static string Model(long id, string guid, string settings)
        => $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n" +
           $"  m_Name: Model{id}\n  m_Parent: {{fileID: 10}}\n  m_Children: []\n" +
           "  m_InputSlots: []\n  m_OutputSlots: []\n  m_Disabled: 0\n  " + settings + "\n";

    private static long ReadFileId(string model)
    {
        const string prefix = "--- !u!114 &";
        int end = model.IndexOf('\n');
        return long.Parse(model.Substring(prefix.Length, end - prefix.Length), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? FindPackage()
    {
        string? configured = Environment.GetEnvironmentVariable("ANITY_UNITY_VFXGRAPH_PACKAGE");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured)) return configured;
        const string candidate = "/Applications/Unity/Hub/Editor/2022.3.51f1/Unity.app/Contents/Resources/PackageManager/BuiltInPackages/com.unity.visualeffectgraph";
        return Directory.Exists(candidate) ? candidate : null;
    }

    private static string SpawnerSlot(
        long id,
        long ownerId,
        string slotType,
        string guid,
        string propertyName,
        string valueType,
        string serializedObject,
        long linkedSlotId = 0,
        int direction = 0)
        => $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n" +
           "  m_Name: \n  m_Parent: {fileID: 0}\n  m_Children: []\n" +
           "  m_InputSlots: []\n  m_OutputSlots: []\n" +
           $"  m_MasterSlot: {{fileID: {id}}}\n  m_MasterData:\n" +
           $"    m_Owner: {{fileID: {ownerId}}}\n    m_Value:\n      m_Type:\n" +
           $"        m_SerializableType: {valueType}, mscorlib\n" +
           $"      m_SerializableObject: {(serializedObject.StartsWith("{", StringComparison.Ordinal) ? $"'{serializedObject}'" : serializedObject)}\n" +
           "    m_Space: 2147483647\n  m_Property:\n" +
           $"    name: {propertyName}\n    m_serializedType:\n" +
           $"      m_SerializableType: {valueType}, mscorlib\n" +
           $"  m_Direction: {direction}\n" +
           (linkedSlotId == 0
               ? "  m_LinkedSlots: []\n"
               : $"  m_LinkedSlots:\n  - {{fileID: {linkedSlotId}}}\n");

    private static string ExposedParameter(
        long id, long parentId, long outputSlotId, string name)
        => $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           "  m_Script: {fileID: 11500000, guid: 330e0fca1717dde4aaa144f48232aa64, type: 3}\n" +
           $"  m_Name: \n  m_Parent: {{fileID: {parentId}}}\n  m_Children: []\n" +
           $"  m_InputSlots: []\n  m_OutputSlots:\n  - {{fileID: {outputSlotId}}}\n" +
           $"  m_ExposedName: {name}\n  m_Exposed: 1\n  m_Order: 0\n  m_Category: \n" +
           "  m_Min:\n    m_Type:\n      m_SerializableType: \n    m_SerializableObject: \n" +
           "  m_Max:\n    m_Type:\n      m_SerializableType: \n    m_SerializableObject: \n" +
           "  m_IsOutput: 0\n  m_EnumValues: []\n  m_ValueFilter: 0\n  m_Tooltip: \n  m_Nodes: []\n";

    private static string DynamicBuiltInParameter(
        long id,
        long parentId,
        long outputSlotId,
        int flags)
        => $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           "  m_Script: {fileID: 11500000, guid: a72fbb93ebe17974e90a144ef2ec8ceb, type: 3}\n" +
           $"  m_Name: \n  m_Parent: {{fileID: {parentId}}}\n  m_Children: []\n" +
           $"  m_InputSlots: []\n  m_OutputSlots:\n  - {{fileID: {outputSlotId}}}\n" +
           $"  m_BuiltInParameters: {flags}\n";

    private static string AddChild(string source, long parentId, long childId)
    {
        string marker = $"--- !u!114 &{parentId}\n";
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        int end = source.IndexOf("--- !u!", start + marker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        string document = source[start..end].Replace(
            "  m_Children:\n",
            $"  m_Children:\n  - {{fileID: {childId}}}\n",
            StringComparison.Ordinal);
        return source[..start] + document + source[end..];
    }

    private static string SpawnerSetAttributeBlock(
        long id,
        long parentId,
        IReadOnlyList<long> inputSlots,
        string attribute,
        int randomMode)
        => $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {SpawnerSetAttributeGuid}, type: 3}}\n" +
           $"  m_Name: Set SpawnEvent {attribute}\n  m_Parent: {{fileID: {parentId}}}\n" +
           "  m_Children: []\n  m_InputSlots:\n" +
           string.Concat(inputSlots.Select(slot => $"  - {{fileID: {slot}}}\n")) +
           "  m_OutputSlots: []\n  m_Disabled: 0\n" +
           $"  attribute: {attribute}\n  randomMode: {randomMode}\n";

    private static string Operator(
        long id,
        long parentId,
        string scriptGuid,
        IReadOnlyList<long> inputSlots,
        IReadOnlyList<long> outputSlots)
        => $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}\n" +
           $"  m_Name: Add\n  m_Parent: {{fileID: {parentId}}}\n  m_Children: []\n" +
           "  m_InputSlots:\n" +
           string.Concat(inputSlots.Select(slot => $"  - {{fileID: {slot}}}\n")) +
           "  m_OutputSlots:\n" +
           string.Concat(outputSlots.Select(slot => $"  - {{fileID: {slot}}}\n"));

    private static string SpawnerCustomCallbackBlock(
        long id,
        long parentId,
        IReadOnlyList<long> inputSlots)
        => $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {SpawnerCustomWrapperGuid}, type: 3}}\n" +
           $"  m_Name: Custom Callback\n  m_Parent: {{fileID: {parentId}}}\n" +
           "  m_Children: []\n  m_InputSlots:\n" +
           string.Concat(inputSlots.Select(slot => $"  - {{fileID: {slot}}}\n")) +
           "  m_OutputSlots: []\n  m_Disabled: 0\n" +
           "  m_customType:\n" +
           "    m_SerializableType: Tests.ProbeSpawnerCallbacks, Assembly-CSharp, Version=0.0.0.0,\n" +
           "      Culture=neutral, PublicKeyToken=null\n";

    private const string Preamble = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
    private const string Resource = "--- !u!2058629511 &90\nVisualEffectResource:\n  m_Graph: {fileID: 10}\n";
    private const string GraphGuid = "7d4c867f6b72b714dbb5fd1780afe208";
    private const string SetAttributeGuid = "a971fa2e110a0ac42ac1d8dae408704b";
    private const string SetCustomAttributeGuid = "5c286b53e648ef840b8153892fdbe169";
    private const string AttributeParameterGuid = "486e063e1ed58c843942ea4122829ab1";
    private const string SpawnerSetAttributeGuid = "709ca816312218f4ba70763d893c34c9";
    private const string SpawnerCustomWrapperGuid = "4bfc68bea08ee074899e288b438a2e89";
}
