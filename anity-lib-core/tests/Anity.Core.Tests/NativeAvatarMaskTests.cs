using System.Reflection;
using UnityEngine;
using Xunit;
using Object = UnityEngine.Object;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeAvatarMaskTests
{
    [Fact]
    public void PublicSurfaceAndMetadataMatchUnity2022()
    {
        Type type = typeof(AvatarMask);
        string[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name + "(" + string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.Name)) + ")")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.True(type.IsSealed);
        Assert.Equal(new[]
        {
            "AddTransformPath(Transform)",
            "AddTransformPath(Transform,Boolean)",
            "GetHumanoidBodyPartActive(AvatarMaskBodyPart)",
            "GetTransformActive(Int32)",
            "GetTransformPath(Int32)",
            "RemoveTransformPath(Transform)",
            "RemoveTransformPath(Transform,Boolean)",
            "SetHumanoidBodyPartActive(AvatarMaskBodyPart,Boolean)",
            "SetTransformActive(Int32,Boolean)",
            "SetTransformPath(Int32,String)",
        }, methods);
        Assert.Equal(new[] { "humanoidBodyPartCount", "transformCount" },
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Contains(type.CustomAttributes, attribute =>
            attribute.AttributeType.FullName == "UnityEngine.Scripting.UsedByNativeCodeAttribute");
        Assert.Equal(2, type.CustomAttributes.Count(attribute =>
            attribute.AttributeType.FullName == "UnityEngine.Bindings.NativeHeaderAttribute"));
    }

    [Fact]
    public void AvatarMaskBodyPartValuesMatchUnity2022()
    {
        Assert.Equal(new[]
        {
            "Root=0", "Body=1", "Head=2", "LeftLeg=3", "RightLeg=4", "LeftArm=5", "RightArm=6",
            "LeftFingers=7", "RightFingers=8", "LeftFootIK=9", "RightFootIK=10",
            "LeftHandIK=11", "RightHandIK=12", "LastBodyPart=13",
        }, Enum.GetNames<AvatarMaskBodyPart>().Select(name => name + "=" + (int)Enum.Parse<AvatarMaskBodyPart>(name)));
    }

    [Fact]
    public void DefaultMaskEnablesAllHumanoidPartsAndHasNoTransforms()
    {
        AvatarMask mask = CreateMask();
        try
        {
            Assert.Equal(13, mask.humanoidBodyPartCount);
            Assert.Equal(0, mask.transformCount);
            for (int index = 0; index < (int)AvatarMaskBodyPart.LastBodyPart; ++index)
                Assert.True(mask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)index));
        }
        finally { Object.DestroyImmediate(mask); }
    }

    [Fact]
    public void HumanoidPartWritesAreIndependent()
    {
        AvatarMask mask = CreateMask();
        try
        {
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, false);

            Assert.False(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm));
            Assert.True(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm));
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            Assert.True(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm));
        }
        finally { Object.DestroyImmediate(mask); }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(99)]
    public void InvalidHumanoidPartReadsFalseAndWritesAreIgnored(int index)
    {
        AvatarMask mask = CreateMask();
        try
        {
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)index, true);
            Assert.False(mask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)index));
        }
        finally { Object.DestroyImmediate(mask); }
    }

    [Fact]
    public void TransformCountGrowCreatesEmptyInactiveEntries()
    {
        AvatarMask mask = CreateMask();
        try
        {
            mask.transformCount = 2;

            Assert.Equal(2, mask.transformCount);
            Assert.Equal(string.Empty, mask.GetTransformPath(0));
            Assert.Equal(string.Empty, mask.GetTransformPath(1));
            Assert.False(mask.GetTransformActive(0));
            Assert.False(mask.GetTransformActive(1));
        }
        finally { Object.DestroyImmediate(mask); }
    }

    [Fact]
    public void TransformCountShrinkPreservesLeadingEntryAndNegativeClampsToZero()
    {
        AvatarMask mask = CreateMask();
        try
        {
            mask.transformCount = 2;
            mask.SetTransformPath(0, "A");
            mask.SetTransformPath(1, "B");
            mask.SetTransformActive(0, true);
            mask.transformCount = 1;

            Assert.Equal(1, mask.transformCount);
            Assert.Equal("A", mask.GetTransformPath(0));
            Assert.True(mask.GetTransformActive(0));
            mask.transformCount = -10;
            Assert.Equal(0, mask.transformCount);
        }
        finally { Object.DestroyImmediate(mask); }
    }

    [Fact]
    public void InvalidTransformIndexesAreSafeAndDoNotResize()
    {
        AvatarMask mask = CreateMask();
        try
        {
            mask.SetTransformPath(-1, "X");
            mask.SetTransformPath(1, "X");
            mask.SetTransformActive(0, true);

            Assert.Equal(0, mask.transformCount);
            Assert.Equal(string.Empty, mask.GetTransformPath(-1));
            Assert.Equal(string.Empty, mask.GetTransformPath(1));
            Assert.False(mask.GetTransformActive(-1));
            Assert.False(mask.GetTransformActive(1));
        }
        finally { Object.DestroyImmediate(mask); }
    }

    [Fact]
    public void TransformPathSupportsNullAndUtf8RoundTrip()
    {
        AvatarMask mask = CreateMask();
        try
        {
            mask.transformCount = 1;
            mask.SetTransformPath(0, "角色/左手");
            Assert.Equal("角色/左手", mask.GetTransformPath(0));
            mask.SetTransformPath(0, null!);
            Assert.Equal(string.Empty, mask.GetTransformPath(0));
        }
        finally { Object.DestroyImmediate(mask); }
    }

    [Fact]
    public void AddRootRecursivelyUsesUnityRelativeDepthFirstPaths()
    {
        (GameObject root, GameObject child, _) = CreateHierarchy();
        AvatarMask mask = CreateMask();
        try
        {
            mask.AddTransformPath(root.transform);

            Assert.Equal(new[] { string.Empty, "Child", "Child/Grandchild" }, Paths(mask));
            Assert.All(Enumerable.Range(0, mask.transformCount), index => Assert.True(mask.GetTransformActive(index)));
        }
        finally
        {
            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void AddChildRecursivelyIncludesChildAndDescendantsButExcludesHierarchyRoot()
    {
        (GameObject root, GameObject child, _) = CreateHierarchy();
        AvatarMask mask = CreateMask();
        try
        {
            mask.AddTransformPath(child.transform);

            Assert.Equal(new[] { "Child", "Child/Grandchild" }, Paths(mask));
        }
        finally
        {
            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void FlatAddOnlyAddsSelectedTransformAndAllowsDuplicates()
    {
        (GameObject root, GameObject child, _) = CreateHierarchy();
        AvatarMask mask = CreateMask();
        try
        {
            mask.AddTransformPath(child.transform, false);
            mask.AddTransformPath(child.transform, false);

            Assert.Equal(new[] { "Child", "Child" }, Paths(mask));
        }
        finally
        {
            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void RecursiveRemoveClearsSelectedTransformDescendantsAndAllDuplicates()
    {
        (GameObject root, GameObject child, _) = CreateHierarchy();
        AvatarMask mask = CreateMask();
        try
        {
            mask.AddTransformPath(root.transform);
            mask.AddTransformPath(child.transform);
            mask.RemoveTransformPath(child.transform);

            Assert.Equal(new[] { string.Empty }, Paths(mask));
        }
        finally
        {
            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void FlatRemoveKeepsDescendantsAndClearsEveryExactDuplicate()
    {
        (GameObject root, GameObject child, _) = CreateHierarchy();
        AvatarMask mask = CreateMask();
        try
        {
            mask.AddTransformPath(root.transform);
            mask.AddTransformPath(child.transform, false);
            mask.RemoveTransformPath(child.transform, false);

            Assert.Equal(new[] { string.Empty, "Child/Grandchild" }, Paths(mask));
        }
        finally
        {
            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(root);
        }
    }

    [Fact]
    public void NullTransformAddAndRemoveThrowArgumentNullException()
    {
        AvatarMask mask = CreateMask();
        try
        {
            Assert.Throws<ArgumentNullException>(() => mask.AddTransformPath(null!));
            Assert.Throws<ArgumentNullException>(() => mask.AddTransformPath(null!, false));
            Assert.Throws<ArgumentNullException>(() => mask.RemoveTransformPath(null!));
            Assert.Throws<ArgumentNullException>(() => mask.RemoveTransformPath(null!, false));
        }
        finally { Object.DestroyImmediate(mask); }
    }

    private static AvatarMask CreateMask() => new();

    private static string[] Paths(AvatarMask mask)
        => Enumerable.Range(0, mask.transformCount).Select(mask.GetTransformPath).ToArray();

    private static (GameObject Root, GameObject Child, GameObject Grandchild) CreateHierarchy()
    {
        var root = new GameObject("Root");
        var child = new GameObject("Child");
        child.transform.SetParent(root.transform, false);
        var grandchild = new GameObject("Grandchild");
        grandchild.transform.SetParent(child.transform, false);
        return (root, child, grandchild);
    }
}
