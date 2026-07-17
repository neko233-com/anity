using UnityEditor.VFX.Model;
using System.Text.Json;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxSlotValueTests
{
    [Fact]
    public void Float_ParsesInvariantScalar()
    {
        VfxSlotValue value = Parse("VFXSlotFloat", "System.Single", "-1.25e2");

        Assert.Equal(VfxSlotValueKind.Float, value.Kind);
        Assert.Equal(-125d, value.Scalar);
    }

    [Fact]
    public void Int_ParsesSignedValue()
    {
        VfxSlotValue value = Parse("VFXSlotInt", "System.Int32", int.MinValue.ToString());

        Assert.Equal(VfxSlotValueKind.Int32, value.Kind);
        Assert.Equal(int.MinValue, value.SignedInteger);
    }

    [Fact]
    public void Uint_ParsesFullRange()
    {
        VfxSlotValue value = Parse("VFXSlotUint", "System.UInt32", "4294967295");

        Assert.Equal(VfxSlotValueKind.UInt32, value.Kind);
        Assert.Equal(uint.MaxValue, value.UnsignedInteger);
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void Bool_ParsesUnityCasing(string raw, bool expected)
    {
        Assert.Equal(expected, Parse("VFXSlotBool", "System.Boolean", raw).Boolean);
    }

    [Fact]
    public void Float2_ParsesYamlQuotedJson()
    {
        VfxSlotValue value = Parse("VFXSlotFloat2", "UnityEngine.Vector2", "'{\"x\":1.5,\"y\":-2.0}'");

        Assert.Equal(VfxSlotValueKind.Float2, value.Kind);
        Assert.Equal(new[] { 1.5, -2d }, value.Components);
    }

    [Fact]
    public void Float3_ParsesComponents()
    {
        VfxSlotValue value = Parse("VFXSlotFloat3", "UnityEngine.Vector3", "'{\"x\":1,\"y\":2,\"z\":3}'");

        Assert.Equal(new[] { 1d, 2d, 3d }, value.Components);
    }

    [Fact]
    public void Color_ParsesRgba()
    {
        VfxSlotValue value = Parse("VFXSlotColor", "UnityEngine.Color", "'{\"r\":1,\"g\":0.5,\"b\":0.25,\"a\":0}'");

        Assert.Equal(VfxSlotValueKind.Color, value.Kind);
        Assert.Equal(new[] { 1d, 0.5, 0.25, 0d }, value.Components);
    }

    [Theory]
    [InlineData("VFXSlotPosition", "UnityEditor.VFX.Position", "position", "Position")]
    [InlineData("VFXSlotDirection", "UnityEditor.VFX.DirectionType", "direction", "Direction")]
    [InlineData("VFXSlotVector", "UnityEditor.VFX.Vector", "vector", "Vector")]
    public void SpaceableVector_ParsesWrappedComponents(
        string slotType,
        string serializedType,
        string wrapper,
        string expectedKind)
    {
        string raw = $"'{{\"{wrapper}\":{{\"x\":4,\"y\":5,\"z\":6}}}}'";
        VfxSlotValue value = Parse(slotType, serializedType, raw);

        Assert.Equal(Enum.Parse<VfxSlotValueKind>(expectedKind), value.Kind);
        Assert.Equal(new[] { 4d, 5d, 6d }, value.Components);
    }

    [Fact]
    public void Transform_ParsesPositionAnglesScale()
    {
        const string raw = "'{\"position\":{\"x\":1,\"y\":2,\"z\":3},\"angles\":{\"x\":4,\"y\":5,\"z\":6},\"scale\":{\"x\":7,\"y\":8,\"z\":9}}'";
        VfxSlotValue value = Parse("VFXSlotTransform", "UnityEditor.VFX.Transform", raw);

        Assert.Equal(VfxSlotValueKind.Transform, value.Kind);
        Assert.Equal(Enumerable.Range(1, 9).Select(number => (double)number), value.Components);
    }

    [Theory]
    [InlineData("VFXSlotTexture2D", "UnityEngine.Texture2D")]
    [InlineData("VFXSlotMesh", "UnityEngine.Mesh")]
    public void ObjectSlot_ParsesUnityObjectReference(string slotType, string serializedType)
    {
        const string raw = "'{\"obj\":{\"fileID\":2800000,\"guid\":\"276d9e395ae18fe40a9b4988549f2349\",\"type\":3}}'";
        VfxObjectReference reference = Parse(slotType, serializedType, raw).ObjectReference!;

        Assert.Equal(2800000, reference.FileId);
        Assert.Equal("276d9e395ae18fe40a9b4988549f2349", reference.Guid);
        Assert.Equal(3, reference.Type);
    }

    [Fact]
    public void AnimationCurve_PreservesValidatedJson()
    {
        VfxSlotValue value = Parse(
            "VFXSlotAnimationCurve",
            "UnityEngine.AnimationCurve",
            "'{\"frames\":[],\"preWrapMode\":8,\"postWrapMode\":8}'");

        Assert.Equal(VfxSlotValueKind.AnimationCurve, value.Kind);
        Assert.Equal(8, value.Json!.Value.GetProperty("preWrapMode").GetInt32());
    }

    [Fact]
    public void Gradient_PreservesValidatedJson()
    {
        VfxSlotValue value = Parse(
            "VFXSlotGradient",
            "UnityEngine.Gradient",
            "'{\"colorKeys\":[],\"alphaKeys\":[],\"gradientMode\":0}'");

        Assert.Equal(VfxSlotValueKind.Gradient, value.Kind);
        Assert.Equal(JsonValueKind.Array, value.Json!.Value.GetProperty("colorKeys").ValueKind);
    }

    [Fact]
    public void GenericSlot_PreservesStructuredValue()
    {
        VfxSlotValue value = Parse(
            "VFXSlot",
            "UnityEditor.VFX.AABox",
            "'{\"center\":{\"x\":0,\"y\":0,\"z\":0},\"size\":{\"x\":1,\"y\":1,\"z\":1}}'");

        Assert.Equal(VfxSlotValueKind.Structured, value.Kind);
        Assert.True(value.Json!.Value.TryGetProperty("center", out _));
    }

    [Fact]
    public void EmptyValue_IsPreservedForChildSlot()
    {
        VfxSlotValue value = Parse("VFXSlotFloat", "System.Single", string.Empty, string.Empty);

        Assert.Equal(VfxSlotValueKind.Empty, value.Kind);
    }

    [Fact]
    public void WrongOfficialSlotType_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => Parse("VFXSlotFloat", "System.Int32", "1"));
    }

    [Fact]
    public void PropertyAndValueTypeDisagreement_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => Parse("VFXSlot", "System.Single", "1", "System.Int32"));
    }

    [Fact]
    public void MalformedJson_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => Parse("VFXSlotFloat3", "UnityEngine.Vector3", "'{bad}'"));
    }

    [Fact]
    public void MissingVectorComponent_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => Parse(
            "VFXSlotFloat3",
            "UnityEngine.Vector3",
            "'{\"x\":1,\"y\":2}'"));
    }

    [Fact]
    public void ObjectReferenceInvalidGuid_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => Parse(
            "VFXSlotTexture2D",
            "UnityEngine.Texture2D",
            "'{\"obj\":{\"fileID\":1,\"guid\":\"bad\",\"type\":3}}'"));
    }

    private static VfxSlotValue Parse(
        string slotTypeName,
        string propertyType,
        string raw,
        string? valueType = null)
    {
        VfxScriptType type = VfxScriptTypeRegistry.All.Values.Single(candidate => candidate.TypeName == slotTypeName);
        return VfxSlotValue.Parse(type, propertyType, valueType ?? propertyType, raw);
    }
}
