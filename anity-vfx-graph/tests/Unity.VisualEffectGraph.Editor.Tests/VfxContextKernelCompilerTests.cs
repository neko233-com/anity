using System.Globalization;
using UnityEditor.VFX.Generation;
using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using UnityEngine.VFX;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxContextKernelCompilerTests
{
    [Fact]
    public void UpdateOverwrite_EmitsExecutableParticleBufferKernel()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("position")),
            Slot(101, 20, SlotKind.Position, "Position", new[] { 1.0, 2.0, 3.0 })));

        Assert.Contains("RWByteAddressBuffer attributeBuffer", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("[numthreads(64, 1, 1)]", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("if (particleIndex >= particleCount) return;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float3 Position = float3(1.0, 2.0, 3.0);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.position = Position;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("AnityVfxStoreAttributes(particleIndex, attributes);", result.HlslSource, StringComparison.Ordinal);
        Assert.Equal(64, result.ThreadGroupSize);
        Assert.Equal(new long[] { 20 }, result.CompiledBlockIds);
    }

    [Theory]
    [InlineData(1, "attributes.size += Size;")]
    [InlineData(2, "attributes.size *= Size;")]
    public void UpdateComposition_EmitsReadModifyWriteOperator(int composition, string expected)
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size", composition)),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 2.5 })));

        Assert.Contains(expected, result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateBlend_DeclaresBlendFactorAndEmitsLerp()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101, 102 }, Attribute("size", 3)),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 4.0 }),
            Slot(102, 20, SlotKind.Float, "Blend", new[] { 0.25 })));

        Assert.Contains("float Blend = 0.25;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.size = lerp(attributes.size,Size,Blend);", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PerComponentRandom_UsesDeterministicStateAndPersistsSeed()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101, 102 }, Attribute("velocity", random: 1)),
            Slot(101, 20, SlotKind.Vector, "A", new[] { -1.0, -2.0, -3.0 }),
            Slot(102, 20, SlotKind.Vector, "B", new[] { 1.0, 2.0, 3.0 })));

        Assert.True(result.UsesRandom);
        Assert.Contains("uint seed;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("uint randomState = attributes.seed ^ particleIndex;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float3(AnityVfxRandom(randomState), AnityVfxRandom(randomState), AnityVfxRandom(randomState))", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.seed = randomState;", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RAND", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void UniformRandom_UsesOneRandomValueForAllComponents()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101, 102 }, Attribute("position", random: 2)),
            Slot(101, 20, SlotKind.Position, "A", new[] { 0.0, 1.0, 2.0 }),
            Slot(102, 20, SlotKind.Position, "B", new[] { 3.0, 4.0, 5.0 })));

        Assert.Contains("lerp(A,B,AnityVfxRandom(randomState))", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomIntAttribute_UsesTypedDefaultAndOfficialLocalName()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetCustomAttributeGuid, new long[] { 101 },
                "attribute: heat\n  Composition: 0\n  Random: 0\n  AttributeType: 6"),
            Slot(101, 20, SlotKind.Int, "_Heat", new[] { -7.0 })));

        Assert.Contains("int heat;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("int _Heat = -7;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.heat = _Heat;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledUnsupportedBlock_IsSkippedWhenAnEnabledSupportedBlockExists()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, TurbulenceGuid, Array.Empty<long>(), string.Empty, disabled: 1),
            Block(21, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 21, SlotKind.Float, "Size", new[] { 1.0 })));

        Assert.Equal(new long[] { 21 }, result.CompiledBlockIds);
    }

    [Fact]
    public void FalseConstantActivation_IsSkipped()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size"), activationId: 100),
            Slot(100, 20, SlotKind.Bool, "activation", new[] { 0.0 }),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 5.0 }),
            Block(21, SetAttributeGuid, new long[] { 102 }, Attribute("mass")),
            Slot(102, 21, SlotKind.Float, "Mass", new[] { 2.0 })));

        Assert.Equal(new long[] { 21 }, result.CompiledBlockIds);
        Assert.DoesNotContain("attributes.size", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedCurrentAttributeActivation_EmitsRuntimeConditionAndDependency()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size"), activationId: 100),
            Slot(100, 20, SlotKind.Bool, "_vfx_enabled", new[] { 1.0 }, new long[] { 201 }),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 2.0 }),
            Parameter(30, 201, "alive", 0),
            Slot(201, 30, SlotKind.Bool, "alive", new[] { 1.0 }, new long[] { 100 }, direction: 1)));

        Assert.Contains("bool alive;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("bool vfx_slot_201 = attributes.alive;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("bool _vfx_enabled;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("if (_vfx_enabled)", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.size = Size;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedSourceAttributeActivation_UsesUpdateEntrySnapshot()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size"), activationId: 100),
            Slot(100, 20, SlotKind.Bool, "_vfx_enabled", new[] { 1.0 }, new long[] { 201 }),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 2.0 }),
            Parameter(30, 201, "alive", 1),
            Slot(201, 30, SlotKind.Bool, "alive", new[] { 1.0 }, new long[] { 100 }, direction: 1)));

        Assert.True(result.UsesSource);
        Assert.Contains("sourceAttributes.alive = true;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("bool vfx_slot_201 = sourceAttributes.alive;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializedActivationSlotOverridesDeprecatedDisabledFlag()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size"), disabled: 1, activationId: 100),
            Slot(100, 20, SlotKind.Bool, "_vfx_enabled", new[] { 1.0 }),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 3.0 })));

        Assert.Equal(new long[] { 20 }, result.CompiledBlockIds);
        Assert.Contains("attributes.size = Size;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedNonBooleanActivation_IsRejected()
        => Assert.Throws<InvalidDataException>(() => Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size"), activationId: 100),
            Slot(100, 20, SlotKind.Bool, "_vfx_enabled", new[] { 1.0 }, new long[] { 201 }),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 2.0 }),
            Slot(201, 0, SlotKind.Float, "value", new[] { 1.0 }, new long[] { 100 }, direction: 1))));

    [Fact]
    public void EnabledUnsupportedBlock_IsRejected()
        => Assert.Throws<NotSupportedException>(() => Compile(Graph(
            Block(20, TurbulenceGuid, Array.Empty<long>(), string.Empty))));

    [Fact]
    public void Gravity_UsesUnityFormulaAndTriggersImplicitEulerIntegration()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, GravityGuid, new long[] { 101 }, string.Empty),
            Slot(101, 20, SlotKind.Vector, "Force", new[] { 0.0, -9.81, 0.0 })));

        Assert.Contains("float deltaTime;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float3 velocity;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float3 position;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.velocity += Force * deltaTime;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.position += attributes.velocity * deltaTime;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AbsoluteForce_DividesByMassLikeUnityForceHelper()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, ForceGuid, new long[] { 101 }, "Mode: 0"),
            Slot(101, 20, SlotKind.Vector, "Force", new[] { 1.0, 2.0, 3.0 })));

        Assert.Contains("float mass;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.velocity += (Force / attributes.mass) * deltaTime;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativeForce_UsesVelocityTargetDragAndMassClamp()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, ForceGuid, new long[] { 101, 102 }, "Mode: 1"),
            Slot(101, 20, SlotKind.Vector, "Velocity", new[] { 1.0, 2.0, 3.0 }),
            Slot(102, 20, SlotKind.Float, "Drag", new[] { 0.5 })));

        Assert.Contains("float3 Velocity = float3(1.0, 2.0, 3.0);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float Drag = 0.5;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.velocity += (Velocity - attributes.velocity) * min(1.0, Drag * deltaTime / attributes.mass);", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Force_InvalidModeIsRejected()
        => Assert.Throws<InvalidDataException>(() => Compile(Graph(
            Block(20, ForceGuid, new long[] { 101 }, "Mode: 2"),
            Slot(101, 20, SlotKind.Vector, "Force", new[] { 1.0, 0.0, 0.0 }))));

    [Fact]
    public void Force_RejectsWrongTypedInputBeforeHlslEmission()
        => Assert.Throws<InvalidDataException>(() => Compile(Graph(
            Block(20, ForceGuid, new long[] { 101 }, "Mode: 0"),
            Slot(101, 20, SlotKind.Position, "Force", new[] { 1.0, 0.0, 0.0 }))));

    [Fact]
    public void LinearDrag_UsesUnityMassClampedDecayFormula()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, DragGuid, new long[] { 101 }, "UseParticleSize: 0"),
            Slot(101, 20, SlotKind.Float, "dragCoefficient", new[] { 0.5 })));

        Assert.Contains("float dragCoefficient = 0.5;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.velocity *= max(0.0, (1.0 - (dragCoefficient * deltaTime) / attributes.mass));", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("float2 side", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LinearDrag_WithParticleSizeUsesSizeAndXYScaleArea()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, DragGuid, new long[] { 101 }, "UseParticleSize: 1"),
            Slot(101, 20, SlotKind.Float, "dragCoefficient", new[] { 0.25 })));

        Assert.Contains("float size;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float scaleX;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float scaleY;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float2 side = attributes.size * float2(attributes.scaleX, attributes.scaleY);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("dragCoefficient *= side.x * side.y;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LinearDrag_InvalidParticleSizeSettingIsRejected()
        => Assert.Throws<InvalidDataException>(() => Compile(Graph(
            Block(20, DragGuid, new long[] { 101 }, "UseParticleSize: 2"),
            Slot(101, 20, SlotKind.Float, "dragCoefficient", new[] { 0.5 }))));

    [Fact]
    public void BasicUpdate_IntegrationNoneSuppressesPositionIntegration()
    {
        VfxContextKernelCompilation result = Compile(GraphWithContextSettings(
            "integration: 1",
            Block(20, GravityGuid, new long[] { 101 }, string.Empty),
            Slot(101, 20, SlotKind.Vector, "Force", new[] { 0.0, -9.81, 0.0 })));

        Assert.DoesNotContain("float3 position;", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("attributes.position +=", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BasicUpdate_AngularEulerIntegratesOnlyWrittenChannels()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("angularVelocity", channels: 0)),
            Slot(101, 20, SlotKind.Float, "AngularVelocity", new[] { 2.0 })));

        Assert.Contains("attributes.angleX += attributes.angularVelocityX * deltaTime;", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("attributes.angleY +=", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("attributes.angleZ +=", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BasicUpdate_LifetimeWriteInsertsAgeThenReapInOfficialOrder()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("lifetime")),
            Slot(101, 20, SlotKind.Float, "Lifetime", new[] { 3.0 })));

        int write = result.HlslSource.IndexOf("attributes.lifetime = Lifetime;", StringComparison.Ordinal);
        int age = result.HlslSource.IndexOf("attributes.age += deltaTime;", StringComparison.Ordinal);
        int reap = result.HlslSource.IndexOf("if (attributes.age > attributes.lifetime) { attributes.alive = false; }", StringComparison.Ordinal);
        Assert.True(write >= 0 && write < age && age < reap);
        Assert.Contains("bool alive;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("if (!attributes.alive) return;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BasicUpdate_AgeDisabledStillReapsLifetimeParticles()
    {
        VfxContextKernelCompilation result = Compile(GraphWithContextSettings(
            "ageParticles: 0",
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("lifetime")),
            Slot(101, 20, SlotKind.Float, "Lifetime", new[] { 3.0 })));

        Assert.DoesNotContain("attributes.age += deltaTime;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("if (attributes.age > attributes.lifetime) { attributes.alive = false; }", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BasicUpdate_ReapDisabledStillAgesParticles()
    {
        VfxContextKernelCompilation result = Compile(GraphWithContextSettings(
            "reapParticles: 0",
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("lifetime")),
            Slot(101, 20, SlotKind.Float, "Lifetime", new[] { 3.0 })));

        Assert.Contains("attributes.age += deltaTime;", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("attributes.alive = false", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BasicUpdate_SkipZeroDeltaWrapsExplicitAndImplicitBlocks()
    {
        VfxContextKernelCompilation result = Compile(GraphWithContextSettings(
            "skipZeroDeltaUpdate: 1",
            Block(20, GravityGuid, new long[] { 101 }, string.Empty),
            Slot(101, 20, SlotKind.Vector, "Force", new[] { 0.0, -9.81, 0.0 })));

        int condition = result.HlslSource.IndexOf("if (deltaTime != 0.0)", StringComparison.Ordinal);
        int gravity = result.HlslSource.IndexOf("attributes.velocity += Force * deltaTime;", StringComparison.Ordinal);
        int integration = result.HlslSource.IndexOf("attributes.position += attributes.velocity * deltaTime;", StringComparison.Ordinal);
        Assert.True(condition >= 0 && condition < gravity && gravity < integration);
    }

    [Fact]
    public void BasicUpdate_OldPositionReadInsertsBackupBeforeExplicitBlocks()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("position")),
            Slot(101, 20, SlotKind.Position, "Position", new[] { 0.0, 0.0, 0.0 }, new long[] { 201 }),
            Parameter(30, 201, "oldPosition", 0),
            Slot(201, 30, SlotKind.Position, "oldPosition", new[] { 0.0, 0.0, 0.0 }, new long[] { 101 }, direction: 1)));

        int backup = result.HlslSource.IndexOf("attributes.oldPosition = attributes.position;", StringComparison.Ordinal);
        int write = result.HlslSource.IndexOf("attributes.position = Position;", StringComparison.Ordinal);
        Assert.True(backup >= 0 && backup < write);
    }

    [Fact]
    public void BasicUpdate_InvalidIntegrationSettingIsRejected()
        => Assert.Throws<InvalidDataException>(() => Compile(GraphWithContextSettings(
            "integration: 2",
            Block(20, GravityGuid, new long[] { 101 }, string.Empty),
            Slot(101, 20, SlotKind.Vector, "Force", new[] { 0.0, -9.81, 0.0 }))));

    [Fact]
    public void AliveUsage_DeclaresUnityStyleDeadListResourcesForOrdinaryParticles()
    {
        VfxContextKernelCompilation result = Compile(LifetimeGraph());

        Assert.True(result.UsesDeadList);
        Assert.Contains("RWStructuredBuffer<uint> deadListOut : register(u1);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("RWStructuredBuffer<uint> deadListCount : register(u2);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("uint deadListCapacity;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AliveUsage_FiltersInitiallyDeadParticleBeforeAnyBlockExecution()
    {
        VfxContextKernelCompilation result = Compile(LifetimeGraph());

        int gate = result.HlslSource.IndexOf("if (!attributes.alive) return;", StringComparison.Ordinal);
        int lifetimeWrite = result.HlslSource.IndexOf("attributes.lifetime = Lifetime;", StringComparison.Ordinal);
        Assert.True(gate >= 0 && gate < lifetimeWrite);
    }

    [Fact]
    public void AliveParticle_PersistsAllUpdatedAttributesBeforeLeavingKernel()
    {
        VfxContextKernelCompilation result = Compile(LifetimeGraph());

        Assert.Contains(
            "if (attributes.alive)\n    {\n        AnityVfxStoreAttributes(particleIndex, attributes);",
            result.HlslSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NewlyDeadParticle_PersistsOnlyAliveFromOriginalAttributeSnapshot()
    {
        VfxContextKernelCompilation result = Compile(LifetimeGraph());

        Assert.Contains("VFXAttributes deadAttributes = AnityVfxLoadAttributes(particleIndex);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("deadAttributes.alive = false;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("AnityVfxStoreAttributes(particleIndex, deadAttributes);", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void NewlyDeadParticle_AtomicallyAppendsParticleIndexToDeadList()
    {
        VfxContextKernelCompilation result = Compile(LifetimeGraph());

        int add = result.HlslSource.IndexOf("InterlockedAdd(deadListCount[0], 1u, deadIndex);", StringComparison.Ordinal);
        int append = result.HlslSource.IndexOf("deadListOut[deadIndex] = particleIndex;", StringComparison.Ordinal);
        Assert.True(add >= 0 && add < append);
    }

    [Fact]
    public void DeadListAppend_IsCapacityGuardedAndOverflowRestoresCount()
    {
        VfxContextKernelCompilation result = Compile(LifetimeGraph());

        int add = result.HlslSource.IndexOf("InterlockedAdd(deadListCount[0], 1u, deadIndex);", StringComparison.Ordinal);
        int guard = result.HlslSource.IndexOf("if (deadIndex < deadListCapacity)", StringComparison.Ordinal);
        int append = result.HlslSource.IndexOf("deadListOut[deadIndex] = particleIndex;", StringComparison.Ordinal);
        Assert.True(add >= 0 && add < guard && guard < append);
        Assert.Contains(
            "InterlockedAdd(deadListCount[0], 0xffffffffu, ignoredDeadCount);",
            result.HlslSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitSetAlive_EnablesDeadListEvenWithoutImplicitReap()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("alive")),
            Slot(101, 20, SlotKind.Bool, "Alive", new[] { 0.0 })));

        Assert.True(result.UsesDeadList);
        Assert.Contains("attributes.alive = Alive;", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("attributes.age > attributes.lifetime", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadOnlyAliveDependency_StillUsesOfficialDeadListTemplateBranch()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size"), activationId: 100),
            Slot(100, 20, SlotKind.Bool, "_vfx_enabled", new[] { 1.0 }, new long[] { 201 }),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 2.0 }),
            Parameter(30, 201, "alive", 0),
            Slot(201, 30, SlotKind.Bool, "alive", new[] { 1.0 }, new long[] { 100 }, direction: 1)));

        Assert.True(result.UsesDeadList);
        Assert.Contains("if (!attributes.alive) return;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipZeroDelta_LeavesDeadListClassificationAfterWrappedUpdateBody()
    {
        VfxContextKernelCompilation result = Compile(GraphWithContextSettings(
            "skipZeroDeltaUpdate: 1",
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("alive")),
            Slot(101, 20, SlotKind.Bool, "Alive", new[] { 0.0 })));

        int zeroGate = result.HlslSource.IndexOf("if (deltaTime != 0.0)", StringComparison.Ordinal);
        int setAlive = result.HlslSource.IndexOf("attributes.alive = Alive;", StringComparison.Ordinal);
        int classify = result.HlslSource.LastIndexOf("if (attributes.alive)", StringComparison.Ordinal);
        Assert.True(zeroGate >= 0 && zeroGate < setAlive && setAlive < classify);
    }

    [Fact]
    public void ParticleStripAliveUsage_DoesNotBindOrdinaryParticleDeadList()
    {
        string graph = LifetimeGraph()
            .Replace("dataType: 0", "dataType: 1", StringComparison.Ordinal)
            .Replace("capacity: 128", "capacity: 256", StringComparison.Ordinal);

        VfxContextKernelCompilation result = Compile(graph);

        Assert.False(result.UsesDeadList);
        Assert.DoesNotContain("deadListOut", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("deadListCount", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("AnityVfxStoreAttributes(particleIndex, attributes);", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void KernelWithoutAlive_DoesNotPolluteDispatchOrUavContractWithDeadList()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 1.0 })));

        Assert.False(result.UsesDeadList);
        Assert.DoesNotContain("deadListCapacity", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("register(u1)", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("register(u2)", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAttributeMode_SnapshotsInitialCurrentValueLikeUnityUpdate()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("position", source: 1))));

        Assert.True(result.UsesSource);
        Assert.Contains("struct VFXSourceAttributes", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("sourceAttributes.position = attributes.position;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float3 Value = sourceAttributes.position;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.position = Value;", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceAttributesBuffer", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceSnapshot_IsCapturedBeforeEarlierBlockMutatesCurrentValue()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 9.0 }),
            Block(21, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1))));

        int snapshot = result.HlslSource.IndexOf("sourceAttributes.size = attributes.size;", StringComparison.Ordinal);
        int mutation = result.HlslSource.IndexOf("attributes.size = Size;", StringComparison.Ordinal);
        int restore = result.HlslSource.LastIndexOf("attributes.size = Value;", StringComparison.Ordinal);
        Assert.True(snapshot >= 0 && snapshot < mutation && mutation < restore);
    }

    [Fact]
    public void SourceVariadicChannels_ArePackedInSerializedChannelOrder()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("scale", source: 1, channels: 4))));

        Assert.Contains("float2 Value = float2(sourceAttributes.scaleX, sourceAttributes.scaleZ);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.scaleX = Value.x;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.scaleZ = Value.y;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacySourceBlend_PreservesSerializedBlendInputAndSnapshot()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size", 3, source: 1)),
            Slot(101, 20, SlotKind.Float, "Blend", new[] { 0.25 })));

        Assert.Contains("float Value = sourceAttributes.size;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.size = lerp(attributes.size,Value,Blend);", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SlotOnlyKernel_DoesNotEmitSourceStruct()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 1.0 })));

        Assert.False(result.UsesSource);
        Assert.DoesNotContain("VFXSourceAttributes", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedScalarOperatorDag_IsInlinedIntoBlockScope()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 0.0 }, new long[] { 203 }),
            Operator(30, new long[] { 201, 202 }, new long[] { 203 }),
            Slot(201, 30, SlotKind.Float, "A", new[] { 2.0 }),
            Slot(202, 30, SlotKind.Float, "B", new[] { 3.0 }),
            Slot(203, 30, SlotKind.Float, "Out", new[] { 0.0 }, new long[] { 101 }, direction: 1)));

        Assert.Contains("float vfx_slot_203 = (vfx_slot_201 + vfx_slot_202);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("Size = vfx_slot_101;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.size = Size;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedPosition_WorldToLocal_UsesOfficialTransformFormulaAndDispatchMatrices()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("position")),
            Slot(101, 20, SlotKind.Position, "Position", new[] { 0.0, 0.0, 0.0 }, new long[] { 201 }, space: 0),
            Slot(201, 0, SlotKind.Position, "Source", new[] { 1.0, 2.0, 3.0 }, new long[] { 101 }, direction: 1, space: 1)));

        Assert.Contains("float4x4 worldToLocal;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("mul(worldToLocal, float4(vfx_slot_201, 1.0)).xyz", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedBoolToFloatTypeMismatch_IsRejectedExplicitly()
        => Assert.Throws<InvalidDataException>(() => Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 0.0 }, new long[] { 201 }),
            Slot(201, 0, SlotKind.Bool, "Source", new[] { 1.0 }, new long[] { 101 }, direction: 1))));

    [Fact]
    public void CurrentAttributeParameter_AddsReachableReadDependencyAndExpression()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 0.0 }, new long[] { 201 }),
            Parameter(30, 201, "alpha", 0),
            Slot(201, 30, SlotKind.Float, "alpha", new[] { 0.0 }, new long[] { 101 }, direction: 1)));

        Assert.Contains("float alpha;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float vfx_slot_201 = attributes.alpha;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.size = Size;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAttributeParameter_UsesDefaultWhenAttributeIsNotCurrentInUpdate()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 0.0 }, new long[] { 201 }),
            Parameter(30, 201, "alpha", 1),
            Slot(201, 30, SlotKind.Float, "alpha", new[] { 0.0 }, new long[] { 101 }, direction: 1)));

        Assert.True(result.UsesSource);
        Assert.Contains("sourceAttributes.alpha = 1.0;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float vfx_slot_201 = sourceAttributes.alpha;", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("attributes.alpha", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAttributeParameter_SnapshotsCurrentFieldWhenContextAlsoUsesIt()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("alpha")),
            Slot(101, 20, SlotKind.Float, "Alpha", new[] { 0.25 }),
            Block(21, SetAttributeGuid, new long[] { 102 }, Attribute("size")),
            Slot(102, 21, SlotKind.Float, "Size", new[] { 0.0 }, new long[] { 201 }),
            Parameter(30, 201, "alpha", 1),
            Slot(201, 30, SlotKind.Float, "alpha", new[] { 0.0 }, new long[] { 102 }, direction: 1)));

        Assert.Contains("sourceAttributes.alpha = attributes.alpha;", result.HlslSource, StringComparison.Ordinal);
        int snapshot = result.HlslSource.IndexOf("sourceAttributes.alpha = attributes.alpha;", StringComparison.Ordinal);
        int mutation = result.HlslSource.IndexOf("attributes.alpha = Alpha;", StringComparison.Ordinal);
        Assert.True(snapshot >= 0 && snapshot < mutation);
    }

    [Fact]
    public void VariadicAttributeParameter_PreservesMaskOrderInFloat2Expression()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("scale", channels: 4)),
            Slot(101, 20, SlotKind.Float2, "Scale", new[] { 0.0, 0.0 }, new long[] { 201 }),
            Parameter(30, 201, "scale", 0, "xz"),
            Slot(201, 30, SlotKind.Float2, "scale", new[] { 0.0, 0.0 }, new long[] { 101 }, direction: 1)));

        Assert.Contains(
            "float2 vfx_slot_201 = float2(attributes.scaleX, attributes.scaleZ);",
            result.HlslSource,
            StringComparison.Ordinal);
        Assert.Contains("attributes.scaleX = Scale.x;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.scaleZ = Scale.y;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AttributeParameter_OutputTypeMismatchIsRejected()
        => Assert.Throws<InvalidDataException>(() => Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 0.0 }, new long[] { 201 }),
            Parameter(30, 201, "position", 0),
            Slot(201, 30, SlotKind.Float, "position", new[] { 0.0 }, new long[] { 101 }, direction: 1))));

    [Fact]
    public void MissingRequiredNamedInput_IsRejectedBeforeInvalidHlslCanEscape()
        => Assert.Throws<InvalidDataException>(() => Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "WrongName", new[] { 1.0 }))));

    [Fact]
    public void InvalidDisabledValue_IsRejected()
        => Assert.Throws<InvalidDataException>(() => Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size"), disabled: 2),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 1.0 }))));

    [Fact]
    public void InitializeOverwrite_EmitsDefaultsAndExecutableKernel()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 1.0 })));

        Assert.Contains("void Initialize(uint3 dispatchThreadId : SV_DispatchThreadID)", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("VFXAttributes attributes = (VFXAttributes)0;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.size = 0.1;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.size = Size;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("AnityVfxStoreAttributes(particleIndex, attributes);", result.HlslSource, StringComparison.Ordinal);
        Assert.False(result.UsesSource);
        Assert.False(result.UsesGpuEventSource);
    }

    [Fact]
    public void InitializeComposition_ReadsUnityDefaultBeforeMutation()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("mass", composition: 1)),
            Slot(101, 20, SlotKind.Float, "Mass", new[] { 2.0 })));

        Assert.Contains("attributes.mass = 1.0;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.mass += Mass;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void InitializeSourceAttribute_LoadsDedicatedSourceBuffer()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1))));

        Assert.True(result.UsesSource);
        Assert.Contains("ByteAddressBuffer sourceAttributeBuffer", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("VFXSourceAttributes sourceAttributes = AnityVfxLoadSourceAttributes(sourceIndex);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float Value = sourceAttributes.size;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.size = Value;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void InitializeSourceAttribute_IsExportedToRuntimeAssetSchema()
    {
        VfxTypedGraph graph = Build(InitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1)),
            Block(21, SetAttributeGuid, Array.Empty<long>(), Attribute("position", source: 1))));
        var asset = new VisualEffectAsset();

        VfxRuntimeAssetCompiler.CompileInto(graph, asset);

        using VFXEventAttribute attribute = new(asset);
        Assert.True(attribute.HasFloat("spawnCount"));
        Assert.Equal(1f, attribute.GetFloat("spawnCount"));
        Assert.True(attribute.HasFloat("size"));
        Assert.True(attribute.HasVector3("position"));
        Assert.Equal(new[] { "spawnCount", "position", "size" },
            asset.EventAttributeSchema.Select(field => field.Name));
        Assert.Equal(new[] { 0, 1, 4 }, asset.EventAttributeSchema.Select(field => field.OffsetWords));
    }

    [Fact]
    public void InitializeCpuSource_UsesGuardedPrefixSumBinarySearch()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1))));

        Assert.Contains("StructuredBuffer<uint> spawnCountPrefixSum", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("if (sourceEventCount == 0u) return 0u;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("if (spawnIndex < spawnCountPrefixSum[middle]) high = middle;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("AnityVfxFindSourceIndex(spawnThreadIndex, sourceEventCount)", result.HlslSource, StringComparison.Ordinal);
        Assert.False(result.UsesGpuEventSource);
    }

    [Fact]
    public void InitializeRandom_SeedsFromParticleAndSystemSeedAndPersistsState()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101, 102 }, Attribute("velocity", random: 1)),
            Slot(101, 20, SlotKind.Vector, "A", new[] { -1.0, -2.0, -3.0 }),
            Slot(102, 20, SlotKind.Vector, "B", new[] { 1.0, 2.0, 3.0 })));

        Assert.True(result.UsesRandom);
        Assert.Contains("attributes.seed = AnityVfxHash(particleIndex ^ systemSeed);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("value = (value ^ 61u) ^ (value >> 16);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("value *= 0x27d4eb2du;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("uint randomState = attributes.seed;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.seed = randomState;", result.HlslSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("particleId", "attributes.particleId = particleIndex;")]
    [InlineData("spawnIndex", "attributes.spawnIndex = spawnThreadIndex;")]
    public void InitializeBuiltInIdentityAttribute_IsAssignedBeforeExpressionRead(
        string attribute,
        string expectedAssignment)
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("meshIndex")),
            Slot(101, 20, SlotKind.UInt, "MeshIndex", new[] { 0.0 }, new long[] { 201 }),
            Parameter(30, 201, attribute, 0),
            Slot(201, 30, SlotKind.UInt, attribute, new[] { 0.0 }, new long[] { 101 }, direction: 1)));

        int assignment = result.HlslSource.IndexOf(expectedAssignment, StringComparison.Ordinal);
        int expressionRead = result.HlslSource.IndexOf($"uint vfx_slot_201 = attributes.{attribute};", StringComparison.Ordinal);
        Assert.True(assignment >= 0 && expressionRead > assignment);
    }

    [Fact]
    public void InitializeCurrentAttributeExpression_ReadsInitializedDefault()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 0.0 }, new long[] { 201 }),
            Parameter(30, 201, "mass", 0),
            Slot(201, 30, SlotKind.Float, "mass", new[] { 0.0 }, new long[] { 101 }, direction: 1)));

        int initializeDefault = result.HlslSource.IndexOf("attributes.mass = 1.0;", StringComparison.Ordinal);
        int expressionRead = result.HlslSource.IndexOf("float vfx_slot_201 = attributes.mass;", StringComparison.Ordinal);
        Assert.True(initializeDefault >= 0 && expressionRead > initializeDefault);
    }

    [Fact]
    public void InitializeSourceAttributeExpression_ReadsSourceBuffer()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 0.0 }, new long[] { 201 }),
            Parameter(30, 201, "mass", 1),
            Slot(201, 30, SlotKind.Float, "mass", new[] { 0.0 }, new long[] { 101 }, direction: 1)));

        Assert.True(result.UsesSource);
        Assert.Contains("float vfx_slot_201 = sourceAttributes.mass;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void InitializeGpuEventSource_UsesEventListIndexInsteadOfCpuPrefixSum()
    {
        VfxContextKernelCompilation result = Compile(GpuEventInitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1))));

        Assert.True(result.UsesGpuEventSource);
        Assert.True(result.UsesSource);
        Assert.Contains("StructuredBuffer<uint> eventList : register(t1);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("uint sourceIndex = eventList[spawnThreadIndex];", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("spawnCountPrefixSum", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceEventCount", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void InitializeGpuEventSource_InsertsUnityImplicitAliveBeforeExplicitBlocks()
    {
        VfxContextKernelCompilation result = Compile(GpuEventInitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 1.0 })));

        Assert.True(result.UsesGpuEventSource);
        Assert.True(result.UsesDeadList);
        Assert.Contains("StructuredBuffer<uint> eventList : register(t1);", result.HlslSource, StringComparison.Ordinal);
        int implicitAlive = result.HlslSource.IndexOf("attributes.alive = true;", StringComparison.Ordinal);
        int explicitBlock = result.HlslSource.IndexOf("attributes.size = Size;", StringComparison.Ordinal);
        Assert.True(implicitAlive >= 0 && explicitBlock > implicitAlive);
    }

    [Fact]
    public void InitializeGpuEventWithoutExplicitBlocks_StillCompilesImplicitAlive()
    {
        VfxContextKernelCompilation result = Compile(GpuEventInitializeGraph());

        Assert.Empty(result.CompiledBlockIds);
        Assert.True(result.UsesGpuEventSource);
        Assert.True(result.UsesDeadList);
        Assert.Contains("attributes.alive = true;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("if (!attributes.alive) return;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void InitializeAliveParticle_ConsumesDeadListAfterBlocks()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("alive")),
            Slot(101, 20, SlotKind.Bool, "Alive", new[] { 1.0 })));

        Assert.True(result.UsesDeadList);
        Assert.Contains("maxSpawnCount = min(maxSpawnCount, deadListCountSnapshot);", result.HlslSource, StringComparison.Ordinal);
        int block = result.HlslSource.IndexOf("attributes.alive = Alive;", StringComparison.Ordinal);
        int pop = result.HlslSource.IndexOf("InterlockedAdd(deadListCount[0], 0xffffffffu, previousDeadCount);", StringComparison.Ordinal);
        Assert.True(block >= 0 && pop > block);
        Assert.Contains("uint outputParticleIndex = deadListIn[previousDeadCount - 1u];", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void InitializeDeadParticle_ReturnsBeforeDeadListConsumption()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("alive")),
            Slot(101, 20, SlotKind.Bool, "Alive", new[] { 0.0 })));

        int aliveWrite = result.HlslSource.IndexOf("attributes.alive = Alive;", StringComparison.Ordinal);
        int aliveGuard = result.HlslSource.IndexOf("if (!attributes.alive) return;", StringComparison.Ordinal);
        int deadListPop = result.HlslSource.IndexOf("InterlockedAdd(deadListCount[0]", StringComparison.Ordinal);
        Assert.True(aliveWrite >= 0 && aliveGuard > aliveWrite && deadListPop > aliveGuard);
    }

    [Fact]
    public void InitializeWithoutAlive_DoesNotBindDeadList()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 1.0 })));

        Assert.False(result.UsesDeadList);
        Assert.DoesNotContain("deadListIn", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("deadListCount", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void InitializeDisabledUnsupportedBlock_IsSkipped()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, TurbulenceGuid, Array.Empty<long>(), string.Empty, disabled: 1),
            Block(21, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 21, SlotKind.Float, "Size", new[] { 1.0 })));

        Assert.Equal(new long[] { 21 }, result.CompiledBlockIds);
    }

    [Fact]
    public void InitializeEnabledNonSetBlock_IsRejectedBeforeHlslEmission()
        => Assert.Throws<NotSupportedException>(() => Compile(InitializeGraph(
            Block(20, GravityGuid, new long[] { 101 }, string.Empty),
            Slot(101, 20, SlotKind.Vector, "Force", new[] { 0.0, -9.81, 0.0 }))));

    [Fact]
    public void OutputContext_IsRejectedByKernelCompiler()
    {
        string source = Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 1.0 }))
            .Replace(UpdateGuid, PlanarOutputGuid, StringComparison.Ordinal);

        Assert.Throws<NotSupportedException>(() => Compile(source));
    }

    [Fact]
    public void DataWideLayout_IsIdenticalAcrossInitializeAndUpdate()
    {
        string source = InitializeUpdateGraph(
            new[]
            {
                Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("position")),
                Slot(101, 20, SlotKind.Position, "Position", new[] { 1.0, 2.0, 3.0 })
            },
            new[]
            {
                Block(21, SetAttributeGuid, new long[] { 102 }, Attribute("size"), parentId: 60),
                Slot(102, 21, SlotKind.Float, "Size", new[] { 2.0 })
            });
        VfxTypedGraph graph = Build(source);

        VfxContextKernelCompilation initialize = VfxContextKernelCompiler.Compile(graph, 50);
        VfxContextKernelCompilation update = VfxContextKernelCompiler.Compile(graph, 60);

        Assert.Equal(
            initialize.AttributeLayout.Select(field => (field.Name, field.HlslType, field.OffsetBytes, field.SizeBytes)),
            update.AttributeLayout.Select(field => (field.Name, field.HlslType, field.OffsetBytes, field.SizeBytes)));
        Assert.Equal(initialize.AttributeStrideBytes, update.AttributeStrideBytes);
    }

    [Fact]
    public void DataWideLayout_InitializeDefaultsAttributeUsedOnlyByUpdate()
    {
        string source = InitializeUpdateGraph(
            new[]
            {
                Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("position")),
                Slot(101, 20, SlotKind.Position, "Position", new[] { 1.0, 2.0, 3.0 })
            },
            new[]
            {
                Block(21, SetAttributeGuid, new long[] { 102 }, Attribute("size"), parentId: 60),
                Slot(102, 21, SlotKind.Float, "Size", new[] { 2.0 })
            });

        VfxContextKernelCompilation initialize = VfxContextKernelCompiler.Compile(Build(source), 50);

        Assert.Contains("float size;", initialize.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.size = 0.1;", initialize.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DataWideLayout_UpdateCarriesAttributeWrittenOnlyByInitialize()
    {
        string source = InitializeUpdateGraph(
            new[]
            {
                Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("position")),
                Slot(101, 20, SlotKind.Position, "Position", new[] { 1.0, 2.0, 3.0 })
            },
            new[]
            {
                Block(21, SetAttributeGuid, new long[] { 102 }, Attribute("size"), parentId: 60),
                Slot(102, 21, SlotKind.Float, "Size", new[] { 2.0 })
            });

        VfxContextKernelCompilation update = VfxContextKernelCompiler.Compile(Build(source), 60);

        Assert.Contains("float3 position;", update.HlslSource, StringComparison.Ordinal);
        Assert.Equal(new[] { "position", "size" }, update.AttributeLayout.Select(field => field.Name));
    }

    [Fact]
    public void DataWideLayout_ReportsDeterministicStructuredBufferOffsetsAndStride()
    {
        string source = InitializeUpdateGraph(
            new[]
            {
                Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("position")),
                Slot(101, 20, SlotKind.Position, "Position", new[] { 1.0, 2.0, 3.0 })
            },
            new[]
            {
                Block(21, SetAttributeGuid, new long[] { 102 }, Attribute("size"), parentId: 60),
                Slot(102, 21, SlotKind.Float, "Size", new[] { 2.0 })
            });

        VfxContextKernelCompilation result = VfxContextKernelCompiler.Compile(Build(source), 50);

        Assert.Collection(result.AttributeLayout,
            position =>
            {
                Assert.Equal("position", position.Name);
                Assert.Equal(0, position.OffsetBytes);
                Assert.Equal(12, position.SizeBytes);
            },
            size =>
            {
                Assert.Equal("size", size.Name);
                Assert.Equal(12, size.OffsetBytes);
                Assert.Equal(4, size.SizeBytes);
            });
        Assert.Equal(16, result.AttributeStrideBytes);
    }

    [Fact]
    public void DataWideLayout_UpdateReapForcesInitializeAliveAndDeadListConsumption()
    {
        string source = InitializeUpdateGraph(
            new[]
            {
                Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
                Slot(101, 20, SlotKind.Float, "Size", new[] { 1.0 })
            },
            new[]
            {
                Block(21, SetAttributeGuid, new long[] { 102 }, Attribute("lifetime"), parentId: 60),
                Slot(102, 21, SlotKind.Float, "Lifetime", new[] { 3.0 })
            });
        VfxTypedGraph graph = Build(source);

        VfxContextKernelCompilation initialize = VfxContextKernelCompiler.Compile(graph, 50);
        VfxContextKernelCompilation update = VfxContextKernelCompiler.Compile(graph, 60);

        Assert.True(initialize.UsesDeadList);
        Assert.True(update.UsesDeadList);
        Assert.Contains("attributes.alive = true;", initialize.HlslSource, StringComparison.Ordinal);
        Assert.Contains("InterlockedAdd(deadListCount[0], 0xffffffffu, previousDeadCount);", initialize.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DataWideLayout_UpdateRandomSeedIsInitializedByInitialize()
    {
        string source = InitializeUpdateGraph(
            new[]
            {
                Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
                Slot(101, 20, SlotKind.Float, "Size", new[] { 1.0 })
            },
            new[]
            {
                Block(21, SetAttributeGuid, new long[] { 102, 103 }, Attribute("velocity", random: 1), parentId: 60),
                Slot(102, 21, SlotKind.Vector, "A", new[] { -1.0, -1.0, -1.0 }),
                Slot(103, 21, SlotKind.Vector, "B", new[] { 1.0, 1.0, 1.0 })
            });

        VfxContextKernelCompilation initialize = VfxContextKernelCompiler.Compile(Build(source), 50);

        Assert.False(initialize.UsesRandom);
        Assert.Contains(initialize.AttributeLayout, field => field.Name == "seed");
        Assert.Contains("attributes.seed = AnityVfxHash(particleIndex ^ systemSeed);", initialize.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DataWideLayout_UpdateCurrentParameterIsDefaultedByInitialize()
    {
        string source = InitializeUpdateGraph(
            new[]
            {
                Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("position")),
                Slot(101, 20, SlotKind.Position, "Position", new[] { 0.0, 0.0, 0.0 })
            },
            new[]
            {
                Block(21, SetAttributeGuid, new long[] { 102 }, Attribute("size"), parentId: 60),
                Slot(102, 21, SlotKind.Float, "Size", new[] { 0.0 }, new long[] { 201 }),
                Parameter(30, 201, "mass", 0),
                Slot(201, 30, SlotKind.Float, "mass", new[] { 0.0 }, new long[] { 102 }, direction: 1)
            });

        VfxContextKernelCompilation initialize = VfxContextKernelCompiler.Compile(Build(source), 50);

        Assert.Contains(initialize.AttributeLayout, field => field.Name == "mass");
        Assert.Contains("attributes.mass = 1.0;", initialize.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DataWideLayout_SameCustomAttributeAcrossContextsIsStoredOnce()
    {
        string custom = "attribute: heat\n  Composition: 0\n  Random: 0\n  AttributeType: 0";
        string source = InitializeUpdateGraph(
            new[]
            {
                Block(20, SetCustomAttributeGuid, new long[] { 101 }, custom),
                Slot(101, 20, SlotKind.Float, "_Heat", new[] { 1.0 })
            },
            new[]
            {
                Block(21, SetCustomAttributeGuid, new long[] { 102 }, custom, parentId: 60),
                Slot(102, 21, SlotKind.Float, "_Heat", new[] { 2.0 })
            });

        VfxContextKernelCompilation result = VfxContextKernelCompiler.Compile(Build(source), 50);

        Assert.Single(result.AttributeLayout, field => field.Name == "heat");
    }

    [Fact]
    public void DataWideLayout_ConflictingCustomAttributeTypesAreRejected()
    {
        string source = InitializeUpdateGraph(
            new[]
            {
                Block(20, SetCustomAttributeGuid, new long[] { 101 },
                    "attribute: heat\n  Composition: 0\n  Random: 0\n  AttributeType: 0"),
                Slot(101, 20, SlotKind.Float, "_Heat", new[] { 1.0 })
            },
            new[]
            {
                Block(21, SetCustomAttributeGuid, new long[] { 102 },
                    "attribute: heat\n  Composition: 0\n  Random: 0\n  AttributeType: 6", parentId: 60),
                Slot(102, 21, SlotKind.Int, "_Heat", new[] { 2.0 })
            });

        Assert.Throws<InvalidDataException>(() => VfxContextKernelCompiler.Compile(Build(source), 50));
    }

    [Fact]
    public void DataWideLayout_DisabledBlockDoesNotPolluteSharedAbi()
    {
        string source = InitializeUpdateGraph(
            new[]
            {
                Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("position")),
                Slot(101, 20, SlotKind.Position, "Position", new[] { 0.0, 0.0, 0.0 })
            },
            new[]
            {
                Block(21, SetAttributeGuid, new long[] { 102 }, Attribute("mass"), disabled: 1, parentId: 60),
                Slot(102, 21, SlotKind.Float, "Mass", new[] { 5.0 }),
                Block(22, SetAttributeGuid, new long[] { 103 }, Attribute("size"), parentId: 60),
                Slot(103, 22, SlotKind.Float, "Size", new[] { 2.0 })
            });

        VfxContextKernelCompilation result = VfxContextKernelCompiler.Compile(Build(source), 50);

        Assert.DoesNotContain(result.AttributeLayout, field => field.Name == "mass");
        Assert.Equal(new[] { "position", "size" }, result.AttributeLayout.Select(field => field.Name));
    }

    [Theory]
    [InlineData("size", -1, (int)SlotKind.Float)]
    [InlineData("position", -1, (int)SlotKind.Position)]
    [InlineData("alive", -1, (int)SlotKind.Bool)]
    [InlineData("meshIndex", -1, (int)SlotKind.UInt)]
    [InlineData("heat", 6, (int)SlotKind.Int)]
    [InlineData("uv", 1, (int)SlotKind.Float2)]
    [InlineData("customVelocity", 2, (int)SlotKind.Float3)]
    [InlineData("tint", 3, (int)SlotKind.Float4)]
    public void RawAttributeBuffer_EmitsTypedLoadAndStoreForEveryStoredValueShape(
        string attribute,
        int customSignature,
        int slotKind)
    {
        SlotKind kind = (SlotKind)slotKind;
        string pascalName = char.ToUpperInvariant(attribute[0]) + attribute.Substring(1);
        bool custom = customSignature >= 0;
        string settings = custom
            ? $"attribute: {attribute}\n  Composition: 0\n  Random: 0\n  AttributeType: {customSignature}"
            : Attribute(attribute);
        string propertyName = custom ? "_" + pascalName : pascalName;
        double[] values = kind switch
        {
            SlotKind.Float2 => new[] { 1.0, 2.0 },
            SlotKind.Float3 or SlotKind.Position => new[] { 1.0, 2.0, 3.0 },
            SlotKind.Float4 => new[] { 1.0, 2.0, 3.0, 4.0 },
            _ => new[] { 1.0 }
        };
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, custom ? SetCustomAttributeGuid : SetAttributeGuid, new long[] { 101 }, settings),
            Slot(101, 20, kind, propertyName, values)));
        VfxAttributeLayoutField field = Assert.Single(result.AttributeLayout);
        string loadCall = field.SizeBytes == 4 ? "Load" : "Load" + (field.SizeBytes / 4).ToString(CultureInfo.InvariantCulture);
        string storeCall = field.SizeBytes == 4 ? "Store" : "Store" + (field.SizeBytes / 4).ToString(CultureInfo.InvariantCulture);

        Assert.Contains($"attributeBuffer.{loadCall}(baseAddress + 0u)", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains($"attributeBuffer.{storeCall}(baseAddress + 0u", result.HlslSource, StringComparison.Ordinal);
        if (kind == SlotKind.Bool)
        {
            Assert.Contains("attributeBuffer.Load(baseAddress + 0u) != 0u", result.HlslSource, StringComparison.Ordinal);
            Assert.Contains("attributes.alive ? 1u : 0u", result.HlslSource, StringComparison.Ordinal);
        }
        else if (kind == SlotKind.UInt)
        {
            Assert.Contains("attributes.meshIndex = attributeBuffer.Load(baseAddress + 0u);", result.HlslSource, StringComparison.Ordinal);
            Assert.Contains("attributeBuffer.Store(baseAddress + 0u, attributes.meshIndex);", result.HlslSource, StringComparison.Ordinal);
        }
        else if (kind == SlotKind.Int)
        {
            Assert.Contains("asint(attributeBuffer.Load(baseAddress + 0u))", result.HlslSource, StringComparison.Ordinal);
            Assert.Contains("asuint(attributes.heat)", result.HlslSource, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("asfloat(attributeBuffer.", result.HlslSource, StringComparison.Ordinal);
            Assert.Contains("asuint(attributes.", result.HlslSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RawAttributeBuffer_StrideConstantMatchesDataWideAbiMetadata()
    {
        string source = InitializeUpdateGraph(
            new[]
            {
                Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("position")),
                Slot(101, 20, SlotKind.Position, "Position", new[] { 0.0, 0.0, 0.0 })
            },
            new[]
            {
                Block(21, SetAttributeGuid, new long[] { 102 }, Attribute("size"), parentId: 60),
                Slot(102, 21, SlotKind.Float, "Size", new[] { 1.0 })
            });
        VfxContextKernelCompilation result = VfxContextKernelCompiler.Compile(Build(source), 50);

        Assert.Equal(16, result.AttributeStrideBytes);
        Assert.Contains("static const uint ANITY_VFX_ATTRIBUTE_STRIDE = 16u;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RawAttributeBuffer_UpdateLoadsBeforeBlocksAndStoresAfterBlocks()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 2.0 })));

        int load = result.HlslSource.IndexOf("VFXAttributes attributes = AnityVfxLoadAttributes(particleIndex);", StringComparison.Ordinal);
        int block = result.HlslSource.IndexOf("attributes.size = Size;", StringComparison.Ordinal);
        int store = result.HlslSource.LastIndexOf("AnityVfxStoreAttributes(particleIndex, attributes);", StringComparison.Ordinal);
        Assert.True(load >= 0 && block > load && store > block);
    }

    [Fact]
    public void RawAttributeBuffer_LocalOnlyEventAttributeIsRejectedInsteadOfCorruptingParticleAbi()
        => Assert.Throws<NotSupportedException>(() => Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("eventCount")),
            Slot(101, 20, SlotKind.UInt, "EventCount", new[] { 1.0 }))));

    [Theory]
    [InlineData("size", "float", 4, "asfloat(sourceAttributeBuffer.Load(baseAddress + 0u))")]
    [InlineData("position", "float3", 12, "asfloat(sourceAttributeBuffer.Load3(baseAddress + 0u))")]
    [InlineData("alive", "bool", 4, "sourceAttributeBuffer.Load(baseAddress + 0u) != 0u")]
    [InlineData("meshIndex", "uint", 4, "sourceAttributeBuffer.Load(baseAddress + 0u)")]
    public void SourceRawLayout_EmitsTypedReadOnlyLoad(
        string attribute,
        string hlslType,
        int sizeBytes,
        string expectedLoad)
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute(attribute, source: 1))));
        VfxAttributeLayoutField field = Assert.Single(result.SourceAttributeLayout);

        Assert.True(result.UsesExternalSourceBuffer);
        Assert.Equal(attribute, field.Name);
        Assert.Equal(hlslType, field.HlslType);
        Assert.Equal(sizeBytes, field.SizeBytes);
        Assert.Contains(expectedLoad, result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceRawLayout_MultipleAttributesUseCatalogOrderOffsetsAndStride()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1)),
            Block(21, SetAttributeGuid, Array.Empty<long>(), Attribute("position", source: 1))));

        Assert.Collection(result.SourceAttributeLayout,
            position =>
            {
                Assert.Equal("position", position.Name);
                Assert.Equal(0, position.OffsetBytes);
                Assert.Equal(12, position.SizeBytes);
            },
            size =>
            {
                Assert.Equal("size", size.Name);
                Assert.Equal(12, size.OffsetBytes);
                Assert.Equal(4, size.SizeBytes);
            });
        Assert.Equal(16, result.SourceAttributeStrideBytes);
        Assert.Contains("static const uint ANITY_VFX_SOURCE_ATTRIBUTE_STRIDE = 16u;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceRawLayout_CpuPrefixSumIndexIsResolvedBeforeRawLoad()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1))));

        int sourceIndex = result.HlslSource.IndexOf(
            "uint sourceIndex = AnityVfxFindSourceIndex(spawnThreadIndex, sourceEventCount);",
            StringComparison.Ordinal);
        int load = result.HlslSource.IndexOf(
            "VFXSourceAttributes sourceAttributes = AnityVfxLoadSourceAttributes(sourceIndex);",
            StringComparison.Ordinal);
        Assert.True(sourceIndex >= 0 && load > sourceIndex);
    }

    [Fact]
    public void SourceRawLayout_BatchedStartEventIndexOffsetsEveryCpuSourceLoad()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1))));

        Assert.True(result.UsesBatchedSourceEventOffset);
        Assert.Contains("uint startEventIndex;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains(
            "uint baseAddress = (startEventIndex + sourceIndex) * ANITY_VFX_SOURCE_ATTRIBUTE_STRIDE;",
            result.HlslSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SourceRawLayout_GpuEventIndexIsResolvedBeforeRawLoad()
    {
        VfxContextKernelCompilation result = Compile(GpuEventInitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1))));

        int sourceIndex = result.HlslSource.IndexOf(
            "uint sourceIndex = eventList[spawnThreadIndex];",
            StringComparison.Ordinal);
        int load = result.HlslSource.IndexOf(
            "VFXSourceAttributes sourceAttributes = AnityVfxLoadSourceAttributes(sourceIndex);",
            StringComparison.Ordinal);
        Assert.True(sourceIndex >= 0 && load > sourceIndex);
        Assert.True(result.UsesBatchedSourceEventOffset);
        Assert.Contains("uint startEventIndex;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceRawLayout_SourceAttributeParameterReadsLoadedRawField()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 0.0 }, new long[] { 201 }),
            Parameter(30, 201, "mass", 1),
            Slot(201, 30, SlotKind.Float, "mass", new[] { 0.0 }, new long[] { 101 }, direction: 1)));

        Assert.Contains("sourceAttributes.mass = asfloat(sourceAttributeBuffer.Load(baseAddress + 0u));", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float vfx_slot_201 = sourceAttributes.mass;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceRawLayout_NoSourceDoesNotBindExternalBufferOrMetadata()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 1.0 })));

        Assert.False(result.UsesExternalSourceBuffer);
        Assert.False(result.UsesBatchedSourceEventOffset);
        Assert.Empty(result.SourceAttributeLayout);
        Assert.Equal(0, result.SourceAttributeStrideBytes);
        Assert.DoesNotContain("sourceAttributeBuffer", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AnityVfxLoadSourceAttributes", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceRawLayout_UpdateSourceRemainsEntrySnapshotWithoutExternalBinding()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1))));

        Assert.True(result.UsesSource);
        Assert.False(result.UsesExternalSourceBuffer);
        Assert.Single(result.SourceAttributeLayout);
        Assert.DoesNotContain("ByteAddressBuffer sourceAttributeBuffer", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("sourceAttributes.size = attributes.size;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceRawLayout_InitializeSourceAdvertisesExternalBinding()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1))));

        Assert.True(result.UsesSource);
        Assert.True(result.UsesExternalSourceBuffer);
        Assert.Contains("ByteAddressBuffer sourceAttributeBuffer : register(t0);", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceRawLayout_HelperIsReadOnlyAndNeverStoresIntoSourceBuffer()
    {
        VfxContextKernelCompilation result = Compile(InitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("position", source: 1))));

        Assert.DoesNotContain("sourceAttributeBuffer.Store", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RWByteAddressBuffer sourceAttributeBuffer", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeInitializeIr_ExportsPackedDefaultsAndConstantOverwrite()
    {
        VfxTypedGraph graph = Build(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 2.5 })));

        VFXRuntimeInitializeKernelData kernel = VfxInitializeRuntimeKernelCompiler.Compile(
            graph, 50, 128, new Dictionary<string, VFXRuntimeAttributeData>());

        VFXRuntimeInitializeAttributeData attribute = Assert.Single(kernel.Attributes);
        Assert.Equal("size", attribute.Layout.Name);
        Assert.Equal(BitConverter.SingleToUInt32Bits(0.1f), Assert.Single(attribute.DefaultWords));
        VFXRuntimeInitializeOperationData operation = Assert.Single(kernel.Operations);
        Assert.Equal(VFXRuntimeInitializeValueSource.Constant, operation.ValueSource);
        Assert.Equal(BitConverter.SingleToUInt32Bits(2.5f), Assert.Single(operation.ValueA));
        Assert.Equal(1, kernel.SourceStrideWords);
    }

    [Fact]
    public void RuntimeInitializeIr_MapsSourceToRuntimeEventRecordOffset()
    {
        VfxTypedGraph graph = Build(InitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1))));
        var source = new Dictionary<string, VFXRuntimeAttributeData>
        {
            ["size"] = new("size", VFXRuntimeValueType.Float, 3, 1)
        };

        VFXRuntimeInitializeKernelData kernel = VfxInitializeRuntimeKernelCompiler.Compile(
            graph, 50, 128, source);

        VFXRuntimeInitializeOperationData operation = Assert.Single(kernel.Operations);
        Assert.Equal(VFXRuntimeInitializeValueSource.Source, operation.ValueSource);
        Assert.Equal(3, operation.SourceOffsetWords);
        Assert.Equal(4, kernel.SourceStrideWords);
    }

    [Fact]
    public void RuntimeInitializeIr_ExportsOfficialFirstFieldSpawnCountPrefixBinding()
    {
        VfxTypedGraph graph = Build(InitializeGraph(
            Block(20, SetAttributeGuid, Array.Empty<long>(), Attribute("size", source: 1))));
        var source = new Dictionary<string, VFXRuntimeAttributeData>
        {
            ["spawnCount"] = new("spawnCount", VFXRuntimeValueType.Float, 0, 1),
            ["size"] = new("size", VFXRuntimeValueType.Float, 1, 1)
        };

        VFXRuntimeInitializeKernelData kernel = VfxInitializeRuntimeKernelCompiler.Compile(
            graph, 50, 128, source);

        Assert.Equal(0, kernel.SpawnCountSourceOffsetWords);
        Assert.Equal(2, kernel.SourceStrideWords);
        Assert.Equal(1, Assert.Single(kernel.Operations).SourceOffsetWords);
    }

    [Fact]
    public void RuntimeInitializeIr_ExportsRandomRangeAndUniformMode()
    {
        VfxTypedGraph graph = Build(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101, 102 }, Attribute("size", random: 2)),
            Slot(101, 20, SlotKind.Float, "A", new[] { 1.0 }),
            Slot(102, 20, SlotKind.Float, "B", new[] { 5.0 })));

        VFXRuntimeInitializeOperationData operation = Assert.Single(
            VfxInitializeRuntimeKernelCompiler.Compile(
                    graph, 50, 128, new Dictionary<string, VFXRuntimeAttributeData>()).Operations
                .Where(candidate => candidate.ValueSource == VFXRuntimeInitializeValueSource.Constant));

        Assert.Equal(VFXRuntimeInitializeRandomMode.Uniform, operation.RandomMode);
        Assert.Equal(BitConverter.SingleToUInt32Bits(1f), Assert.Single(operation.ValueA));
        Assert.Equal(BitConverter.SingleToUInt32Bits(5f), Assert.Single(operation.ValueB));
    }

    [Fact]
    public void RuntimeInitializeIr_ExportsBlendCompositionAndFactor()
    {
        VfxTypedGraph graph = Build(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101, 102 }, Attribute("size", composition: 3)),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 8.0 }),
            Slot(102, 20, SlotKind.Float, "Blend", new[] { 0.25 })));

        VFXRuntimeInitializeOperationData operation = Assert.Single(
            VfxInitializeRuntimeKernelCompiler.Compile(
                graph, 50, 128, new Dictionary<string, VFXRuntimeAttributeData>()).Operations);

        Assert.Equal(VFXRuntimeInitializeComposition.Blend, operation.Composition);
        Assert.Equal(BitConverter.SingleToUInt32Bits(0.25f), operation.BlendFactorBits);
    }

    [Fact]
    public void RuntimeInitializeIr_RejectsLinkedInputUntilExpressionOpcodesExist()
    {
        VfxTypedGraph graph = Build(InitializeGraph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 0.0 }, new long[] { 201 }),
            Parameter(30, 201, "mass", 0),
            Slot(201, 30, SlotKind.Float, "mass", new[] { 0.0 }, new long[] { 101 }, direction: 1)));

        Assert.Throws<NotSupportedException>(() => VfxInitializeRuntimeKernelCompiler.Compile(
            graph, 50, 128, new Dictionary<string, VFXRuntimeAttributeData>()));
    }

    [Fact]
    public void PlanarOutputLayout_AddsEveryOfficialOutputAttributeToParticleAbi()
    {
        VfxContextKernelCompilation result = VfxContextKernelCompiler.Compile(
            Build(InitializeUpdatePlanarOutputGraph()), 50);
        string[] expected =
        {
            "position", "color", "alpha", "alive",
            "axisX", "axisY", "axisZ",
            "angleX", "angleY", "angleZ",
            "pivotX", "pivotY", "pivotZ",
            "size", "scaleX", "scaleY", "scaleZ"
        };

        Assert.Equal(expected.Length, result.AttributeLayout.Count);
        foreach (string name in expected)
            Assert.Contains(result.AttributeLayout, field => field.Name == name);
    }

    [Fact]
    public void PlanarOutputLayout_InitializeDefaultsOutputOnlyAttributes()
    {
        VfxContextKernelCompilation result = VfxContextKernelCompiler.Compile(
            Build(InitializeUpdatePlanarOutputGraph()), 50);

        Assert.Contains("attributes.color = float3(1.0, 1.0, 1.0);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.alpha = 1.0;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.axisX = float3(1.0, 0.0, 0.0);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("attributes.scaleZ = 1.0;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanarOutputLayout_AliveReadForcesInitializeDeadListConsumption()
    {
        VfxContextKernelCompilation result = VfxContextKernelCompiler.Compile(
            Build(InitializeUpdatePlanarOutputGraph()), 50);

        Assert.True(result.UsesDeadList);
        Assert.Contains("attributes.alive = true;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("InterlockedAdd(deadListCount[0], 0xffffffffu, previousDeadCount);", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanarOutputLayout_IsIdenticalForInitializeAndUpdateKernels()
    {
        VfxTypedGraph graph = Build(InitializeUpdatePlanarOutputGraph());
        VfxContextKernelCompilation initialize = VfxContextKernelCompiler.Compile(graph, 50);
        VfxContextKernelCompilation update = VfxContextKernelCompiler.Compile(graph, 60);

        Assert.Equal(
            initialize.AttributeLayout.Select(field => (field.Name, field.OffsetBytes, field.SizeBytes)),
            update.AttributeLayout.Select(field => (field.Name, field.OffsetBytes, field.SizeBytes)));
        Assert.Equal(initialize.AttributeStrideBytes, update.AttributeStrideBytes);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void PlanarOutputLayout_UvModeControlsTexIndexLikeUnity(int uvMode, bool expectsTexIndex)
    {
        VfxContextKernelCompilation result = VfxContextKernelCompiler.Compile(
            Build(InitializeUpdatePlanarOutputGraph(uvMode)), 50);

        Assert.Equal(expectsTexIndex, result.AttributeLayout.Any(field => field.Name == "texIndex"));
    }

    [Fact]
    public void PlanarOutputLayout_InvalidUvModeIsRejected()
        => Assert.Throws<InvalidDataException>(() => VfxContextKernelCompiler.Compile(
            Build(InitializeUpdatePlanarOutputGraph(5)), 50));

    [Fact]
    public void PlanarOutputLayout_ShaderGraphSuppressesLegacyFlipbookTexIndex()
    {
        VfxContextKernelCompilation result = VfxContextKernelCompiler.Compile(
            Build(InitializeUpdatePlanarOutputGraph(1, 999)), 50);

        Assert.DoesNotContain(result.AttributeLayout, field => field.Name == "texIndex");
    }

    [Fact]
    public void PlanarOutputLayout_StrideAndGeneratedConstantIncludeOutputFields()
    {
        VfxContextKernelCompilation result = VfxContextKernelCompiler.Compile(
            Build(InitializeUpdatePlanarOutputGraph()), 50);

        Assert.Equal(108, result.AttributeStrideBytes);
        Assert.Contains("static const uint ANITY_VFX_ATTRIBUTE_STRIDE = 108u;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanarOutputKernel_CompilesAsReadOnlyVertexStage()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput();

        Assert.Equal(VfxKernelStage.Vertex, result.Stage);
        Assert.Equal(0, result.ThreadGroupSize);
        Assert.True(result.UsesReadOnlyAttributeBuffer);
        Assert.Contains("ByteAddressBuffer attributeBuffer : register(t0);", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RWByteAddressBuffer attributeBuffer", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("attributeBuffer.Store", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanarOutputKernel_QuadUsesFourVerticesAndUnityOffsets()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput(primitiveType: 1);

        Assert.Equal(4, result.VerticesPerParticle);
        Assert.Contains("const uint verticesPerParticle = 4u;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float(localVertexId & 2u) * 0.5", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("vOffsets = uv - 0.5;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanarOutputKernel_TriangleUsesOfficialEquilateralGeometry()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput(primitiveType: 0);

        Assert.Equal(3, result.VerticesPerParticle);
        Assert.Contains("const uint verticesPerParticle = 3u;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("-0.288675129413604736328125", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("0.57735025882720947265625", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("0.866025388240814208984375", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanarOutputKernel_OctagonUsesEightVerticesAndOfficialDefaultCrop()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput(primitiveType: 2);

        Assert.Equal(8, result.VerticesPerParticle);
        Assert.Contains("const uint verticesPerParticle = 8u;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float cropFactor = 0.293;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("1.0 - cropFactor", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanarOutputKernel_BoundsAndAliveCullUseNanClipPosition()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput();

        Assert.Contains("particleIndex >= particleCount", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("if (!AnityVfxLoadAlive(particleIndex))", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("asfloat(0x7fc00000u)", result.HlslSource, StringComparison.Ordinal);
        Assert.True(
            result.HlslSource.IndexOf("AnityVfxLoadAlive(particleIndex)", StringComparison.Ordinal) <
            result.HlslSource.IndexOf("AnityVfxLoadAttributes(particleIndex)", StringComparison.Ordinal));
    }

    [Fact]
    public void PlanarOutputKernel_ComposesUnitySizeScaleAxisAnglePivotAndPosition()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput();

        Assert.Contains("size3.x *= attributes.scaleX;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("size3.y *= attributes.scaleY;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("size3.z *= attributes.scaleZ;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("AnityVfxGetEulerMatrix(radians(angles))", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("transpose(float3x3(axisX, axisY, axisZ))", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("position -= mul(rotationAndScale, pivot);", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanarOutputKernel_TransformsVfxToWorldThenClipAndEmitsColor()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput();

        Assert.Contains("mul(vfxToWorld, float4(positionVFX, 1.0))", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("mul(worldToClip, float4(positionWS, 1.0))", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("output.positionWS = positionWS;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("output.color = float4(attributes.color, attributes.alpha);", result.HlslSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void PlanarOutputKernel_FlipbookModesResolveTexIndexIntoFrameUvs(int uvMode)
    {
        VfxContextKernelCompilation result = CompilePlanarOutput(uvMode);

        Assert.Contains(result.AttributeLayout, field => field.Name == "texIndex");
        Assert.Contains("attributes.texIndex", result.HlslSource, StringComparison.Ordinal);
        if (uvMode == 1)
            Assert.Contains("attributes.texIndex - frac(attributes.texIndex)", result.HlslSource, StringComparison.Ordinal);
        else
            Assert.Contains("attributes.texIndex - frameBlend", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("AnityVfxGetSubUV", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("output.texIndex", result.HlslSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void PlanarOutputKernel_NonFlipbookModesDoNotReadTexIndex(int uvMode)
    {
        VfxContextKernelCompilation result = CompilePlanarOutput(uvMode);

        Assert.DoesNotContain(result.AttributeLayout, field => field.Name == "texIndex");
        Assert.DoesNotContain("output.texIndex", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanarOutputKernel_ShaderGraphPassIsRejectedBeforeHlslEmission()
        => Assert.Throws<NotSupportedException>(() => CompilePlanarOutput(shaderGraphFileId: 999));

    [Fact]
    public void PlanarOutputKernel_GeometryShaderExpansionIsRejectedBeforeHlslEmission()
        => Assert.Throws<NotSupportedException>(() => CompilePlanarOutput(useGeometryShader: true));

    [Fact]
    public void PlanarOutputPass_DeclaresLinkedVertexAndFragmentEntrypoints()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput();

        Assert.Equal("AnityVfxPlanarVert", result.VertexEntryPoint);
        Assert.Equal("AnityVfxPlanarFrag", result.FragmentEntryPoint);
        Assert.Contains("#pragma vertex AnityVfxPlanarVert", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("#pragma fragment AnityVfxPlanarFrag", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("Texture2D<float4> mainTexture : register(t1);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("input.color * textureColor", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanarOutputPass_DefaultRenderStateMatchesUnityAlphaOutput()
    {
        VfxPlanarRenderState state = Assert.IsType<VfxPlanarRenderState>(CompilePlanarOutput().PlanarRenderState);

        Assert.Equal(VfxPlanarBlendMode.Alpha, state.BlendMode);
        Assert.True(state.BlendEnabled);
        Assert.Equal("SrcAlpha", state.SourceBlendFactor);
        Assert.Equal("OneMinusSrcAlpha", state.DestinationBlendFactor);
        Assert.Equal(VfxPlanarCullMode.Off, state.CullMode);
        Assert.Equal(VfxPlanarZTest.LEqual, state.ZTest);
        Assert.False(state.ZWrite);
        Assert.Equal("Transparent", state.RenderQueue);
        Assert.True(state.RequiresSorting);
    }

    [Theory]
    [InlineData(0, "SrcAlpha", "One", false)]
    [InlineData(1, "SrcAlpha", "OneMinusSrcAlpha", false)]
    [InlineData(2, "One", "OneMinusSrcAlpha", false)]
    [InlineData(3, "One", "Zero", true)]
    public void PlanarOutputPass_BlendAndDefaultDepthWriteMatchUnity(
        int blendMode,
        string sourceFactor,
        string destinationFactor,
        bool zWrite)
    {
        VfxPlanarRenderState state = Assert.IsType<VfxPlanarRenderState>(
            CompilePlanarOutput(outputSettings: $"  blendMode: {blendMode}\n").PlanarRenderState);

        Assert.Equal(sourceFactor, state.SourceBlendFactor);
        Assert.Equal(destinationFactor, state.DestinationBlendFactor);
        Assert.Equal(zWrite, state.ZWrite);
        Assert.Equal(blendMode != 3, state.BlendEnabled);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void PlanarOutputPass_ExplicitDepthWriteOverridesBlendDefault(int zWriteMode, bool expected)
    {
        VfxPlanarRenderState state = Assert.IsType<VfxPlanarRenderState>(
            CompilePlanarOutput(outputSettings: $"  zWriteMode: {zWriteMode}\n").PlanarRenderState);

        Assert.Equal(expected, state.ZWrite);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 0)]
    public void PlanarOutputPass_CullModeMatchesUnity(int cullMode, int expected)
    {
        VfxPlanarRenderState state = Assert.IsType<VfxPlanarRenderState>(
            CompilePlanarOutput(outputSettings: $"  cullMode: {cullMode}\n").PlanarRenderState);

        Assert.Equal((VfxPlanarCullMode)expected, state.CullMode);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 4)]
    [InlineData(6, 5)]
    [InlineData(7, 6)]
    public void PlanarOutputPass_ZTestModeMatchesUnity(int zTestMode, int expected)
    {
        VfxPlanarRenderState state = Assert.IsType<VfxPlanarRenderState>(
            CompilePlanarOutput(outputSettings: $"  zTestMode: {zTestMode}\n").PlanarRenderState);

        Assert.Equal((VfxPlanarZTest)expected, state.ZTest);
    }

    [Fact]
    public void PlanarOutputPass_OpaqueAlphaClipUsesAlphaTestQueueAndThreshold()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput(
            outputSettings: "  blendMode: 3\n  useAlphaClipping: 1\n  sortingPriority: 7\n");
        VfxPlanarRenderState state = Assert.IsType<VfxPlanarRenderState>(result.PlanarRenderState);

        Assert.True(state.AlphaClipping);
        Assert.Equal("AlphaTest+7", state.RenderQueue);
        Assert.Contains("float alphaThreshold = 0.5;", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("clip(outputColor.a - input.alphaThreshold);", result.HlslSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void PlanarOutputPass_SortActivationMatchesUnity(int sort, bool expected)
    {
        VfxPlanarRenderState state = Assert.IsType<VfxPlanarRenderState>(
            CompilePlanarOutput(outputSettings: $"  sort: {sort}\n").PlanarRenderState);

        Assert.Equal(expected, state.RequiresSorting);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 6)]
    [InlineData(2, 18)]
    public void PlanarOutputPass_ExposesNativeTriangleIndexPattern(int primitiveType, int expectedIndexCount)
    {
        VfxContextKernelCompilation result = CompilePlanarOutput(primitiveType: primitiveType);

        Assert.Equal(expectedIndexCount, result.IndexPattern.Count);
        Assert.Equal(0, result.IndexPattern[0]);
        Assert.All(result.IndexPattern, index => Assert.InRange(index, 0, result.VerticesPerParticle - 1));
    }

    [Fact]
    public void PlanarOutputPass_ExpandsQuadIndicesForMultipleParticles()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput(primitiveType: 1);

        Assert.Equal(8, result.GetPlanarVertexCount(2));
        Assert.Equal(12, result.GetPlanarIndexCount(2));
        Assert.Equal(
            new uint[] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 },
            result.BuildPlanarIndexBuffer(2));
    }

    [Fact]
    public void PlanarOutputPass_ZeroParticlesProduceEmptyIndexBuffer()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput();

        Assert.Equal(0, result.GetPlanarVertexCount(0));
        Assert.Equal(0, result.GetPlanarIndexCount(0));
        Assert.Empty(result.BuildPlanarIndexBuffer(0));
    }

    [Fact]
    public void PlanarOutputPass_NegativeParticleCountIsRejected()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput();

        Assert.Throws<ArgumentOutOfRangeException>(() => result.GetPlanarVertexCount(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.GetPlanarIndexCount(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.BuildPlanarIndexBuffer(-1));
    }

    [Fact]
    public void PlanarOutputPass_DrawCountOverflowIsRejected()
    {
        VfxContextKernelCompilation result = CompilePlanarOutput(primitiveType: 2);

        Assert.Throws<OverflowException>(() => result.GetPlanarVertexCount(int.MaxValue));
        Assert.Throws<OverflowException>(() => result.GetPlanarIndexCount(int.MaxValue));
    }

    [Fact]
    public void PlanarOutputPass_NonOutputCompilationRejectsDrawLayoutQueries()
    {
        VfxContextKernelCompilation result = Compile(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 1.0 })));

        Assert.Throws<InvalidOperationException>(() => result.GetPlanarVertexCount(1));
        Assert.Throws<InvalidOperationException>(() => result.GetPlanarIndexCount(1));
        Assert.Throws<InvalidOperationException>(() => result.BuildPlanarIndexBuffer(1));
    }

    [Fact]
    public void PlanarOutputPass_FlipbookBlendSamplesAndInterpolatesTwoFrames()
    {
        string hlsl = CompilePlanarOutput(uvMode: 2).HlslSource;

        Assert.Contains("float4 uv : TEXCOORD1;", hlsl, StringComparison.Ordinal);
        Assert.Contains("float4 flipbookUv", hlsl, StringComparison.Ordinal);
        Assert.Contains("frame0 = mainTexture.Sample", hlsl, StringComparison.Ordinal);
        Assert.Contains("frame1 = mainTexture.Sample", hlsl, StringComparison.Ordinal);
        Assert.Contains("lerp(frame0, frame1, input.frameBlend)", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanarOutputPass_FlipbookMotionBlendSamplesMotionMapAndOffsetsFrames()
    {
        string hlsl = CompilePlanarOutput(uvMode: 4).HlslSource;

        Assert.Contains("Texture2D<float4> motionVectorMap : register(t2);", hlsl, StringComparison.Ordinal);
        Assert.Contains("motionVectorScale * invFlipBookSize", hlsl, StringComparison.Ordinal);
        Assert.Contains("motion0 * (-input.motionVectorScale * input.frameBlend)", hlsl, StringComparison.Ordinal);
        Assert.Contains("input.uv.xy + offset0", hlsl, StringComparison.Ordinal);
        Assert.Contains("input.uv.zw + offset1", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanarOutputPass_ScaleAndBiasTransformsUvBeforeFragmentSampling()
    {
        string hlsl = CompilePlanarOutput(uvMode: 3).HlslSource;

        Assert.Contains("float2 uvScale = float2(1.0, 1.0);", hlsl, StringComparison.Ordinal);
        Assert.Contains("float2 uvBias = float2(0.0, 0.0);", hlsl, StringComparison.Ordinal);
        Assert.Contains("uv = uv * uvScale + uvBias;", hlsl, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("  colorMapping: 1\n")]
    [InlineData("  useSoftParticle: 1\n")]
    [InlineData("  flipbookLayout: 1\n")]
    public void PlanarOutputPass_UnsupportedMaterialBranchesAreRejected(string settings)
        => Assert.Throws<NotSupportedException>(() => CompilePlanarOutput(uvMode: 1, outputSettings: settings));

    [Theory]
    [InlineData("  blendMode: 4\n")]
    [InlineData("  cullMode: 4\n")]
    [InlineData("  zWriteMode: 3\n")]
    [InlineData("  zTestMode: 8\n")]
    [InlineData("  sort: 3\n")]
    public void PlanarOutputPass_InvalidRenderStateIsRejected(string settings)
        => Assert.Throws<InvalidDataException>(() => CompilePlanarOutput(outputSettings: settings));

    [Fact]
    public void MissingContextId_IsRejected()
        => Assert.Throws<KeyNotFoundException>(() => VfxContextKernelCompiler.Compile(Build(Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("size")),
            Slot(101, 20, SlotKind.Float, "Size", new[] { 1.0 }))), 999));

    private static VfxContextKernelCompilation Compile(string source)
        => VfxContextKernelCompiler.Compile(Build(source), ContextId);

    private static VfxContextKernelCompilation CompilePlanarOutput(
        int uvMode = 0,
        long shaderGraphFileId = 0,
        int primitiveType = 1,
        bool useGeometryShader = false,
        string outputSettings = "")
        => VfxContextKernelCompiler.Compile(
            Build(InitializeUpdatePlanarOutputGraph(
                uvMode,
                shaderGraphFileId,
                primitiveType,
                useGeometryShader,
                outputSettings)),
            70);

    private static VfxTypedGraph Build(string source)
        => VfxTypedGraph.Build(VfxYamlAsset.Parse(source));

    private static string LifetimeGraph()
        => Graph(
            Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("lifetime")),
            Slot(101, 20, SlotKind.Float, "Lifetime", new[] { 3.0 }));

    private static string Graph(params string[] documents)
        => GraphWithContextSettings(string.Empty, documents);

    private static string InitializeGraph(params string[] documents)
        => Graph(documents).Replace(UpdateGuid, InitializeGuid, StringComparison.Ordinal);

    private static string GpuEventInitializeGraph(params string[] documents)
    {
        string source = InitializeGraph(documents)
            .Replace(
                "  m_Children:\n  - {fileID: 50}\n  m_InputSlots: []",
                "  m_Children:\n  - {fileID: 50}\n  - {fileID: 60}\n  m_InputSlots: []",
                StringComparison.Ordinal)
            .Replace(
                "  m_InputFlowSlot:\n  - link: []\n  m_OutputFlowSlot:",
                "  m_InputFlowSlot:\n  - link:\n    - context: {fileID: 60}\n      slotIndex: 0\n  m_OutputFlowSlot:",
                StringComparison.Ordinal);
        return source.Replace(Resource, GpuEventContext() + GpuEventData() + Resource, StringComparison.Ordinal);
    }

    private static string InitializeUpdateGraph(
        IReadOnlyList<string> initializeDocuments,
        IReadOnlyList<string> updateDocuments)
    {
        long[] updateBlockIds = updateDocuments
            .Where(document => document.StartsWith("BLOCK:", StringComparison.Ordinal))
            .Select(ReadTaggedId)
            .ToArray();
        long[] updateGraphModelIds = updateDocuments
            .Where(document => document.StartsWith("OPERATOR:", StringComparison.Ordinal) ||
                               document.StartsWith("PARAMETER:", StringComparison.Ordinal))
            .Select(ReadTaggedId)
            .ToArray();
        string addedGraphChildren = "  - {fileID: 60}\n" +
                                    string.Concat(updateGraphModelIds.Select(id => $"  - {{fileID: {id}}}\n"));
        string source = InitializeGraph(initializeDocuments.ToArray())
            .Replace(
                "  m_Children:\n  - {fileID: 50}\n  m_InputSlots: []",
                "  m_Children:\n  - {fileID: 50}\n" + addedGraphChildren + "  m_InputSlots: []",
                StringComparison.Ordinal)
            .Replace(
                "  m_OutputFlowSlot:\n  - link: []\n",
                "  m_OutputFlowSlot:\n  - link:\n    - context: {fileID: 60}\n      slotIndex: 0\n",
                StringComparison.Ordinal)
            .Replace(
                "  m_Owners:\n  - {fileID: 50}\n",
                "  m_Owners:\n  - {fileID: 50}\n  - {fileID: 60}\n",
                StringComparison.Ordinal);
        string updateContext = "--- !u!114 &60\nMonoBehaviour:\n" +
                               $"  m_Script: {{fileID: 11500000, guid: {UpdateGuid}, type: 3}}\n" +
                               "  m_Name: Update\n  m_Parent: {fileID: 10}\n" +
                               References("m_Children", updateBlockIds) +
                               "  m_InputSlots: []\n  m_OutputSlots: []\n  m_Data: {fileID: 80}\n" +
                               "  m_InputFlowSlot:\n  - link:\n    - context: {fileID: 50}\n      slotIndex: 0\n" +
                               "  m_OutputFlowSlot:\n  - link: []\n";
        string updateModels = string.Concat(updateDocuments.Select(RemoveTag));
        return source.Replace(Resource, updateContext + updateModels + Resource, StringComparison.Ordinal);
    }

    internal static string InitializeUpdatePlanarOutputGraph(
        int uvMode = 0,
        long shaderGraphFileId = 0,
        int primitiveType = 1,
        bool useGeometryShader = false,
        string outputSettings = "")
    {
        string source = InitializeUpdateGraph(
            new[]
            {
                Block(20, SetAttributeGuid, new long[] { 101 }, Attribute("position")),
                Slot(101, 20, SlotKind.Position, "Position", new[] { 0.0, 0.0, 0.0 })
            },
            new[]
            {
                Block(21, SetAttributeGuid, new long[] { 102 }, Attribute("size"), parentId: 60),
                Slot(102, 21, SlotKind.Float, "Size", new[] { 1.0 })
            })
            .Replace(
                "  - {fileID: 60}\n  m_InputSlots: []",
                "  - {fileID: 60}\n  - {fileID: 70}\n  m_InputSlots: []",
                StringComparison.Ordinal)
            .Replace(
                "  m_Owners:\n  - {fileID: 50}\n  - {fileID: 60}\n",
                "  m_Owners:\n  - {fileID: 50}\n  - {fileID: 60}\n  - {fileID: 70}\n",
                StringComparison.Ordinal);
        int updateStart = source.IndexOf("--- !u!114 &60\n", StringComparison.Ordinal);
        const string emptyOutput = "  m_OutputFlowSlot:\n  - link: []\n";
        int updateOutput = source.IndexOf(emptyOutput, updateStart, StringComparison.Ordinal);
        if (updateOutput < 0) throw new InvalidOperationException("Synthetic Update output flow was not found.");
        string linkedOutput = "  m_OutputFlowSlot:\n  - link:\n" +
                              "    - context: {fileID: 70}\n      slotIndex: 0\n";
        source = source.Remove(updateOutput, emptyOutput.Length).Insert(updateOutput, linkedOutput);
        string outputContext = "--- !u!114 &70\nMonoBehaviour:\n" +
                               $"  m_Script: {{fileID: 11500000, guid: {PlanarOutputGuid}, type: 3}}\n" +
                               "  m_Name: Output\n  m_Parent: {fileID: 10}\n  m_Children: []\n" +
                               "  m_InputSlots: []\n  m_OutputSlots: []\n  m_Data: {fileID: 80}\n" +
                               "  m_InputFlowSlot:\n  - link:\n    - context: {fileID: 60}\n      slotIndex: 0\n" +
                               "  m_OutputFlowSlot:\n  - link: []\n" +
                               $"  primitiveType: {primitiveType}\n" +
                               $"  useGeometryShader: {(useGeometryShader ? 1 : 0)}\n" +
                               outputSettings +
                               $"  uvMode: {uvMode}\n" +
                               (shaderGraphFileId == 0
                                   ? "  shaderGraph: {fileID: 0}\n"
                                   : "  shaderGraph: {fileID: 4800000, guid: 11111111111111111111111111111111, type: 3}\n");
        return source.Replace(Resource, outputContext + Resource, StringComparison.Ordinal);
    }

    private static string GraphWithContextSettings(string settings, params string[] documents)
    {
        long[] blockIds = documents.Where(document => document.StartsWith("BLOCK:", StringComparison.Ordinal))
            .Select(ReadTaggedId).ToArray();
        string[] actual = documents.Select(RemoveTag).ToArray();
        long[] graphChildren = new[] { ContextId }.Concat(
            documents.Where(document =>
                    document.StartsWith("OPERATOR:", StringComparison.Ordinal) ||
                    document.StartsWith("PARAMETER:", StringComparison.Ordinal))
                .Select(ReadTaggedId)).ToArray();
        return Preamble + GraphDocument(graphChildren) + Context(blockIds, settings) +
               string.Concat(actual) + ParticleData() + Resource;
    }

    private static string Block(
        long id,
        string guid,
        IReadOnlyList<long> inputs,
        string settings,
        int disabled = 0,
        long activationId = 0,
        long parentId = ContextId)
        => "BLOCK:" + id + "\n" +
           $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n" +
           $"  m_Name: Block{id}\n  m_Parent: {{fileID: {parentId}}}\n  m_Children: []\n" +
           References("m_InputSlots", inputs) + "  m_OutputSlots: []\n" +
           $"  m_Disabled: {disabled}\n" +
           (activationId == 0 ? string.Empty : $"  m_ActivationSlot: {{fileID: {activationId}}}\n") +
           (settings.Length == 0 ? string.Empty : "  " + settings + "\n");

    private static string Operator(long id, IReadOnlyList<long> inputs, IReadOnlyList<long> outputs)
        => "OPERATOR:" + id + "\n" +
           $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {AddGuid}, type: 3}}\n" +
           $"  m_Name: Operator{id}\n  m_Parent: {{fileID: 10}}\n  m_Children: []\n" +
           References("m_InputSlots", inputs) + References("m_OutputSlots", outputs);

    private static string Parameter(
        long id,
        long outputId,
        string attribute,
        int location,
        string mask = "xyz")
        => "PARAMETER:" + id + "\n" +
           $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {AttributeParameterGuid}, type: 3}}\n" +
           $"  m_Name: Parameter{id}\n  m_Parent: {{fileID: 10}}\n  m_Children: []\n" +
           "  m_InputSlots: []\n" + References("m_OutputSlots", new[] { outputId }) +
           $"  attribute: {attribute}\n  location: {location}\n  mask: {mask}\n";

    private static string Slot(
        long id,
        long ownerId,
        SlotKind kind,
        string name,
        IReadOnlyList<double> values,
        IReadOnlyList<long>? links = null,
        int direction = 0,
        int? space = null)
    {
        string type = TypeName(kind);
        string serializedValue = SerializedValue(kind, values);
        return $"--- !u!114 &{id}\nMonoBehaviour:\n" +
               $"  m_Script: {{fileID: 11500000, guid: {SlotGuid(kind)}, type: 3}}\n" +
               $"  m_Name: Slot{id}\n  m_Parent: {{fileID: 0}}\n  m_Children: []\n" +
               $"  m_MasterSlot: {{fileID: {id}}}\n  m_MasterData:\n    m_Owner: {{fileID: {ownerId}}}\n" +
               $"    m_Value:\n      m_Type:\n        m_SerializableType: {type}, assembly\n" +
               $"      m_SerializableObject: {serializedValue}\n    m_Space: {space ?? Space(kind)}\n" +
               $"  m_Property:\n    name: {name}\n    m_serializedType:\n      m_SerializableType: {type}, assembly\n" +
               $"  m_Direction: {direction}\n" + References("m_LinkedSlots", links ?? Array.Empty<long>());
    }

    private static string GraphDocument(IReadOnlyList<long> children)
        => "--- !u!114 &10\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {GraphGuid}, type: 3}}\n" +
           "  m_Name: Graph\n  m_Parent: {fileID: 0}\n" + References("m_Children", children) +
           "  m_InputSlots: []\n  m_OutputSlots: []\n";

    private static string Context(IReadOnlyList<long> children, string settings)
        => $"--- !u!114 &{ContextId}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {UpdateGuid}, type: 3}}\n" +
           $"  m_Name: Update\n  m_Parent: {{fileID: 10}}\n" + References("m_Children", children) +
           "  m_InputSlots: []\n  m_OutputSlots: []\n  m_Data: {fileID: 80}\n" +
           "  m_InputFlowSlot:\n  - link: []\n  m_OutputFlowSlot:\n  - link: []\n" +
           (settings.Length == 0 ? string.Empty : "  " + settings + "\n");

    private static string ParticleData()
        => "--- !u!114 &80\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {ParticleDataGuid}, type: 3}}\n" +
           "  m_Name: Particles\n  m_Parent: {fileID: 0}\n  m_Children: []\n" +
           $"  m_Owners:\n  - {{fileID: {ContextId}}}\n" +
           "  dataType: 0\n  capacity: 128\n  stripCapacity: 16\n" +
           "  particlePerStripCount: 16\n  needsComputeBounds: 0\n  boundsMode: 0\n  m_Space: 0\n";

    private static string GpuEventContext()
        => "--- !u!114 &60\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {GpuEventGuid}, type: 3}}\n" +
           "  m_Name: GPUEvent\n  m_Parent: {fileID: 10}\n  m_Children: []\n" +
           "  m_InputSlots: []\n  m_OutputSlots: []\n  m_Data: {fileID: 81}\n" +
           "  m_InputFlowSlot:\n  - link: []\n" +
           "  m_OutputFlowSlot:\n  - link:\n    - context: {fileID: 50}\n      slotIndex: 0\n";

    private static string GpuEventData()
        => "--- !u!114 &81\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {SpawnerDataGuid}, type: 3}}\n" +
           "  m_Name: GPUEventData\n  m_Parent: {fileID: 0}\n  m_Children: []\n" +
           "  m_Owners:\n  - {fileID: 60}\n";

    private static string Attribute(
        string name,
        int composition = 0,
        int source = 0,
        int random = 0,
        int channels = 6)
        => $"attribute: {name}\n  Composition: {composition}\n  Source: {source}\n  Random: {random}\n  channels: {channels}";

    private static long ReadTaggedId(string document)
    {
        int end = document.IndexOf('\n');
        int colon = document.IndexOf(':');
        return long.Parse(document.Substring(colon + 1, end - colon - 1), CultureInfo.InvariantCulture);
    }

    private static string RemoveTag(string document)
    {
        if (!document.StartsWith("BLOCK:", StringComparison.Ordinal) &&
            !document.StartsWith("OPERATOR:", StringComparison.Ordinal) &&
            !document.StartsWith("PARAMETER:", StringComparison.Ordinal)) return document;
        return document.Substring(document.IndexOf('\n') + 1);
    }

    private static string References(string name, IReadOnlyList<long> values)
        => values.Count == 0
            ? $"  {name}: []\n"
            : $"  {name}:\n" + string.Concat(values.Select(value => $"  - {{fileID: {value}}}\n"));

    private static int Space(SlotKind kind)
        => kind is SlotKind.Position or SlotKind.Vector ? 0 : int.MaxValue;

    private static string SerializedValue(SlotKind kind, IReadOnlyList<double> values)
        => kind switch
        {
            SlotKind.Float => Literal(values[0]),
            SlotKind.Int => ((int)values[0]).ToString(CultureInfo.InvariantCulture),
            SlotKind.UInt => ((uint)values[0]).ToString(CultureInfo.InvariantCulture),
            SlotKind.Bool => values[0] == 0.0 ? "false" : "true",
            SlotKind.Float2 => "'{\"x\":" + Literal(values[0]) + ",\"y\":" + Literal(values[1]) + "}'",
            SlotKind.Float3 => PlainVector(values, 3),
            SlotKind.Float4 => PlainVector(values, 4),
            SlotKind.Position => WrappedVector("position", values),
            SlotKind.Vector => WrappedVector("vector", values),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    private static string WrappedVector(string wrapper, IReadOnlyList<double> values)
        => "'{\"" + wrapper + "\":{\"x\":" + Literal(values[0]) +
           ",\"y\":" + Literal(values[1]) + ",\"z\":" + Literal(values[2]) + "}}'";

    private static string PlainVector(IReadOnlyList<double> values, int count)
        => "'{" + string.Join(",", new[] { "x", "y", "z", "w" }.Take(count)
            .Select((component, index) => "\"" + component + "\":" + Literal(values[index]))) + "}'";

    private static string Literal(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string TypeName(SlotKind kind) => kind switch
    {
        SlotKind.Float => "System.Single",
        SlotKind.Int => "System.Int32",
        SlotKind.UInt => "System.UInt32",
        SlotKind.Bool => "System.Boolean",
        SlotKind.Float2 => "UnityEngine.Vector2",
        SlotKind.Float3 => "UnityEngine.Vector3",
        SlotKind.Float4 => "UnityEngine.Vector4",
        SlotKind.Position => "UnityEditor.VFX.Position",
        SlotKind.Vector => "UnityEditor.VFX.Vector",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string SlotGuid(SlotKind kind) => kind switch
    {
        SlotKind.Float => FloatSlotGuid,
        SlotKind.Int => IntSlotGuid,
        SlotKind.UInt => UIntSlotGuid,
        SlotKind.Bool => BoolSlotGuid,
        SlotKind.Float2 => Float2SlotGuid,
        SlotKind.Float3 => Float3SlotGuid,
        SlotKind.Float4 => Float4SlotGuid,
        SlotKind.Position => PositionSlotGuid,
        SlotKind.Vector => VectorSlotGuid,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private enum SlotKind { Float, Int, UInt, Bool, Float2, Float3, Float4, Position, Vector }

    private const long ContextId = 50;
    private const string Preamble = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
    private const string Resource = "--- !u!2058629511 &900\nVisualEffectResource:\n  m_Graph: {fileID: 10}\n";
    private const string GraphGuid = "7d4c867f6b72b714dbb5fd1780afe208";
    private const string UpdateGuid = "2dc095764ededfa4bb32fa602511ea4b";
    private const string InitializeGuid = "9dfea48843f53fc438eabc12a3a30abc";
    private const string PlanarOutputGuid = "a0b9e6b9139e58d4c957ec54595da7d3";
    private const string GpuEventGuid = "f42a6449da2296343af0d8536de8588a";
    private const string ParticleDataGuid = "d78581a96eae8bf4398c282eb0b098bd";
    private const string SpawnerDataGuid = "f68759077adc0b143b6e1c101e82065e";
    private const string SetAttributeGuid = "a971fa2e110a0ac42ac1d8dae408704b";
    private const string SetCustomAttributeGuid = "5c286b53e648ef840b8153892fdbe169";
    private const string GravityGuid = "e5dce54ae3368c042b26ab1f305e15b2";
    private const string DragGuid = "b294673e879f9cf449cc9de536818ea9";
    private const string ForceGuid = "c079bc84df7c7e94f88c8ae0d1b0691d";
    private const string TurbulenceGuid = "63716c0daf1806941a123003dc6d7398";
    private const string AddGuid = "c7acf5424f3655744af4b8f63298fa0f";
    private const string AttributeParameterGuid = "486e063e1ed58c843942ea4122829ab1";
    private const string FloatSlotGuid = "f780aa281814f9842a7c076d436932e7";
    private const string IntSlotGuid = "4d246e354feb93041a837a9ef59437cb";
    private const string UIntSlotGuid = "c52d920e7fff73b498050a6b3c4404ca";
    private const string BoolSlotGuid = "b4c11ff25089a324daf359f4b0629b33";
    private const string Float2SlotGuid = "1b2b751071c7fc14f9fa503163991826";
    private const string Float3SlotGuid = "ac39bd03fca81b849929b9c966f1836a";
    private const string Float4SlotGuid = "c499060cea9bbb24b8d723eafa343303";
    private const string PositionSlotGuid = "5265657162cc1a241bba03a3b0476d99";
    private const string VectorSlotGuid = "a9f9544b71b7dab44a4644b6807e8bf6";
}
