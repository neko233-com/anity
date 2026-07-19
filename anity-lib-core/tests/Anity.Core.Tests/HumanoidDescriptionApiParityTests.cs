using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

public sealed class HumanoidDescriptionApiParityTests
{
    [Theory]
    [InlineData(typeof(HumanDescription), "human", typeof(HumanBone[]))]
    [InlineData(typeof(HumanDescription), "skeleton", typeof(SkeletonBone[]))]
    [InlineData(typeof(HumanBone), "limit", typeof(HumanLimit))]
    [InlineData(typeof(SkeletonBone), "name", typeof(string))]
    [InlineData(typeof(SkeletonBone), "position", typeof(Vector3))]
    [InlineData(typeof(SkeletonBone), "rotation", typeof(Quaternion))]
    [InlineData(typeof(SkeletonBone), "scale", typeof(Vector3))]
    public void OfficialSerializedMembersArePublicFields(Type type, string name, Type fieldType)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.NotNull(field);
        Assert.Equal(fieldType, field!.FieldType);
        Assert.Null(type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));
    }

    [Theory]
    [InlineData(typeof(HumanDescription), "upperArmTwist", typeof(float))]
    [InlineData(typeof(HumanDescription), "lowerArmTwist", typeof(float))]
    [InlineData(typeof(HumanDescription), "upperLegTwist", typeof(float))]
    [InlineData(typeof(HumanDescription), "lowerLegTwist", typeof(float))]
    [InlineData(typeof(HumanDescription), "armStretch", typeof(float))]
    [InlineData(typeof(HumanDescription), "legStretch", typeof(float))]
    [InlineData(typeof(HumanDescription), "feetSpacing", typeof(float))]
    [InlineData(typeof(HumanDescription), "hasTranslationDoF", typeof(bool))]
    [InlineData(typeof(HumanBone), "boneName", typeof(string))]
    [InlineData(typeof(HumanBone), "humanName", typeof(string))]
    [InlineData(typeof(HumanLimit), "useDefaultValues", typeof(bool))]
    [InlineData(typeof(HumanLimit), "min", typeof(Vector3))]
    [InlineData(typeof(HumanLimit), "max", typeof(Vector3))]
    [InlineData(typeof(HumanLimit), "center", typeof(Vector3))]
    [InlineData(typeof(HumanLimit), "axisLength", typeof(float))]
    public void OfficialManagedMembersAreReadWriteProperties(Type type, string name, Type propertyType)
    {
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.NotNull(property);
        Assert.Equal(propertyType, property!.PropertyType);
        Assert.True(property.CanRead);
        Assert.True(property.CanWrite);
    }

    [Theory]
    [InlineData(typeof(HumanDescription), "human", "m_Human")]
    [InlineData(typeof(HumanDescription), "skeleton", "m_Skeleton")]
    [InlineData(typeof(HumanBone), "limit", "m_Limit")]
    [InlineData(typeof(SkeletonBone), "name", "m_Name")]
    [InlineData(typeof(SkeletonBone), "position", "m_Position")]
    [InlineData(typeof(SkeletonBone), "rotation", "m_Rotation")]
    [InlineData(typeof(SkeletonBone), "scale", "m_Scale")]
    public void SerializedFieldsCarryOfficialNativeNames(Type type, string name, string nativeName)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!;
        var attribute = Assert.Single(field.CustomAttributes.Where(item => item.AttributeType.FullName == "UnityEngine.Bindings.NativeNameAttribute"));
        Assert.Equal(nativeName, attribute.ConstructorArguments[0].Value);
    }

    [Fact]
    public void HumanDescriptionHasNoNonUnityPublicAliases()
    {
        var names = typeof(HumanDescription).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(member => member.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("armTwist", names);
        Assert.DoesNotContain("forearmTwist", names);
        Assert.DoesNotContain("legTwist", names);
        Assert.DoesNotContain("extraSkeleton", names);
        Assert.DoesNotContain("armIK", names);
        Assert.DoesNotContain("bodyYaw", names);
    }

    [Fact]
    public void SkeletonBoneTransformModifiedMatchesRemovedUnityContract()
    {
        var property = typeof(SkeletonBone).GetProperty("transformModified", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!;
        Assert.Equal(typeof(int), property.PropertyType);
        var obsolete = property.GetCustomAttribute<ObsoleteAttribute>();
        Assert.NotNull(obsolete);
        Assert.True(obsolete!.IsError);
        Assert.Equal("transformModified is no longer used and has been deprecated.", obsolete.Message);
        Assert.Equal(EditorBrowsableState.Never, property.GetCustomAttribute<EditorBrowsableAttribute>()!.State);
    }
}
