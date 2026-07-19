using System.Reflection;
using System.Text;
using Anity.Core.Runtime.Native;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeAvatarBuilderTests
{
    private static readonly string[] RequiredHumanNames =
    {
        "Hips", "Spine", "Head",
        "LeftUpperArm", "RightUpperArm", "LeftLowerArm", "RightLowerArm", "LeftHand", "RightHand",
        "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg", "LeftFoot", "RightFoot",
    };

    [Fact]
    public void Avatar_PublicSurfaceMatchesUnity2022()
    {
        PropertyInfo[] properties = typeof(Avatar).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.Equal(new[] { "humanDescription", "isHuman", "isValid" }, properties.Select(property => property.Name).OrderBy(name => name));
        Assert.All(properties, property => Assert.False(property.CanWrite));
        Assert.Empty(typeof(Avatar).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Contains(typeof(Avatar).CustomAttributes, attribute =>
            attribute.AttributeType.FullName == "UnityEngine.Bindings.NativeHeaderAttribute" &&
            attribute.ConstructorArguments[0].Value?.ToString() == "Modules/Animation/Avatar.h");
    }

    [Fact]
    public void AvatarBuilder_PublicSurfaceMatchesUnity2022()
    {
        Type type = typeof(AvatarBuilder);
        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        Assert.False(type.IsAbstract);
        Assert.False(type.IsSealed);
        Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        Assert.Equal(new[] { "BuildGenericAvatar", "BuildHumanAvatar" }, methods.Select(method => method.Name).OrderBy(name => name));
        MethodInfo generic = Assert.Single(methods, method => method.Name == "BuildGenericAvatar");
        Assert.Equal(new[] { typeof(GameObject), typeof(string) }, generic.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.All(generic.GetParameters(), parameter => Assert.Contains(parameter.CustomAttributes, attribute =>
            attribute.AttributeType.FullName == "UnityEngine.Bindings.NotNullAttribute"));
        MethodInfo human = Assert.Single(methods, method => method.Name == "BuildHumanAvatar");
        Assert.DoesNotContain(human.CustomAttributes, attribute =>
            attribute.AttributeType.FullName == "UnityEngine.Bindings.FreeFunctionAttribute");
    }

    [Fact]
    public void BuildHumanAvatar_ValidHierarchyProducesHumanAvatar()
    {
        (GameObject root, HumanDescription description) = CreateValidHumanRig();

        Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, description);

        Assert.True(avatar.isValid);
        Assert.True(avatar.isHuman);
        Assert.Same(description.human, avatar.humanDescription.human);
        Assert.Same(description.skeleton, avatar.humanDescription.skeleton);
    }

    [Fact]
    public void BuildHumanAvatar_NullRootThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AvatarBuilder.BuildHumanAvatar(null!, default));
    }

    [Fact]
    public void BuildHumanAvatar_MissingSceneTransformProducesInvalidAvatar()
    {
        (GameObject root, HumanDescription description) = CreateValidHumanRig();
        description.skeleton[2].name = "NotInHierarchy";

        Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, description);

        Assert.False(avatar.isValid);
        Assert.True(avatar.isHuman);
    }

    [Fact]
    public void BuildHumanAvatar_ZeroScaleProducesInvalidAvatar()
    {
        (GameObject root, HumanDescription description) = CreateValidHumanRig();
        description.skeleton[4].scale = new Vector3(1f, 0f, 1f);

        Assert.False(AvatarBuilder.BuildHumanAvatar(root, description).isValid);
    }

    [Fact]
    public void BuildHumanAvatar_FiniteNonUnitScaleRemainsValid()
    {
        (GameObject root, HumanDescription description) = CreateValidHumanRig();
        description.skeleton[4].scale = new Vector3(1.25f, 0.75f, 2f);

        Assert.True(AvatarBuilder.BuildHumanAvatar(root, description).isValid);
    }

    [Fact]
    public void BuildGenericAvatar_ValidNamedRootProducesGenericAvatar()
    {
        var root = new GameObject("Root");
        GameObject hips = AddChild(root, "Hips");
        _ = AddChild(hips, "Motion");

        Avatar avatar = AvatarBuilder.BuildGenericAvatar(root, "Motion");

        Assert.True(avatar.isValid);
        Assert.False(avatar.isHuman);
    }

    [Fact]
    public void BuildGenericAvatar_EmptyRootNameUsesHierarchyRoot()
    {
        var root = new GameObject("Root");
        _ = AddChild(root, "Child");

        Assert.True(AvatarBuilder.BuildGenericAvatar(root, string.Empty).isValid);
    }

    [Fact]
    public void BuildGenericAvatar_MissingNamedRootProducesInvalidAvatar()
    {
        var root = new GameObject("Root");

        Assert.False(AvatarBuilder.BuildGenericAvatar(root, "Missing").isValid);
    }

    [Fact]
    public void BuildGenericAvatar_NullArgumentsThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AvatarBuilder.BuildGenericAvatar(null!, string.Empty));
        Assert.Throws<ArgumentNullException>(() => AvatarBuilder.BuildGenericAvatar(new GameObject("Root"), null!));
    }

    [Fact]
    public void NativeHumanValidation_EmptySkeletonAndMappingReportsBothFlags()
    {
        AnityNative.AvatarBuildResult result = ValidateHuman(Array.Empty<AnityNative.AvatarSkeletonBoneDesc>(), Array.Empty<AnityNative.AvatarHumanBoneDesc>());

        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.EmptySkeleton));
        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.EmptyHumanMapping));
        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.MissingRequiredHumanBone));
    }

    [Fact]
    public void NativeHumanValidation_DuplicateSkeletonNameIsRejected()
    {
        AnityNative.AvatarSkeletonBoneDesc[] skeleton = { Bone("Root", -1), Bone("Root", 0) };

        AnityNative.AvatarBuildResult result = ValidateHuman(skeleton, RequiredMappings("Root"));

        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.DuplicateSkeletonName));
        Assert.Equal(1, result.errorIndex);
    }

    [Fact]
    public void NativeHumanValidation_InvalidParentIndexIsRejected()
    {
        AnityNative.AvatarSkeletonBoneDesc[] skeleton = { Bone("Root", -1), Bone("Child", 7) };

        AnityNative.AvatarBuildResult result = ValidateHuman(skeleton, RequiredMappings("Root"));

        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.InvalidParent));
    }

    [Fact]
    public void NativeHumanValidation_MultipleRootsAreRejected()
    {
        AnityNative.AvatarSkeletonBoneDesc[] skeleton = { Bone("RootA", -1), Bone("RootB", -1) };

        AnityNative.AvatarBuildResult result = ValidateHuman(skeleton, RequiredMappings("RootA"));

        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.MultipleRoots));
    }

    [Fact]
    public void NativeHumanValidation_CycleIsRejected()
    {
        AnityNative.AvatarSkeletonBoneDesc[] skeleton = { Bone("Root", -1), Bone("A", 2), Bone("B", 1) };

        AnityNative.AvatarBuildResult result = ValidateHuman(skeleton, RequiredMappings("Root"));

        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.HierarchyCycle));
    }

    [Fact]
    public void NativeHumanValidation_ZeroQuaternionIsRejected()
    {
        AnityNative.AvatarSkeletonBoneDesc bone = Bone("Root", -1);
        bone.rotationW = 0f;

        AnityNative.AvatarBuildResult result = ValidateHuman(new[] { bone }, RequiredMappings("Root"));

        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.InvalidTransform));
    }

    [Fact]
    public void NativeHumanValidation_NonFinitePositionIsRejected()
    {
        AnityNative.AvatarSkeletonBoneDesc bone = Bone("Root", -1);
        bone.positionX = float.NaN;

        AnityNative.AvatarBuildResult result = ValidateHuman(new[] { bone }, RequiredMappings("Root"));

        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.InvalidTransform));
    }

    [Fact]
    public void NativeHumanValidation_DuplicateHumanBoneIsRejected()
    {
        AnityNative.AvatarHumanBoneDesc[] mappings = RequiredUniqueMappings();
        mappings[1].boneNameHash = mappings[0].boneNameHash;

        AnityNative.AvatarBuildResult result = ValidateHuman(RequiredUniqueSkeleton(), mappings);

        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.DuplicateHumanBone));
    }

    [Fact]
    public void NativeHumanValidation_DuplicateHumanNameIsRejected()
    {
        AnityNative.AvatarHumanBoneDesc[] mappings = RequiredUniqueMappings();
        mappings[1].humanNameHash = mappings[0].humanNameHash;

        AnityNative.AvatarBuildResult result = ValidateHuman(RequiredUniqueSkeleton(), mappings);

        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.DuplicateHumanName));
    }

    [Fact]
    public void NativeHumanValidation_MissingMappedBoneIsRejected()
    {
        AnityNative.AvatarHumanBoneDesc[] mappings = RequiredUniqueMappings();
        mappings[0].boneNameHash = Hash("Missing");

        AnityNative.AvatarBuildResult result = ValidateHuman(RequiredUniqueSkeleton(), mappings);

        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.MissingMappedBone));
    }

    [Fact]
    public void NativeHumanValidation_MissingRequiredHumanNameIsRejected()
    {
        var mapping = new AnityNative.AvatarHumanBoneDesc { boneNameHash = Hash("Root"), humanNameHash = Hash("Hips") };

        AnityNative.AvatarBuildResult result = ValidateHuman(new[] { Bone("Root", -1) }, new[] { mapping });

        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.MissingRequiredHumanBone));
    }

    [Fact]
    public void NativeGenericValidation_MissingRootMotionNameIsRejected()
    {
        AnityNative.AvatarSkeletonBoneDesc[] skeleton = { Bone("Root", -1), Bone("Motion", 0) };

        Assert.True(AnityNative.TryValidateGenericAvatar(skeleton, Hash("Missing"), out AnityNative.AvatarBuildResult result));
        Assert.True(result.flags.HasFlag(AnityNative.AvatarValidationFlags.MissingRootMotionTransform));
    }

    private static AnityNative.AvatarBuildResult ValidateHuman(
        AnityNative.AvatarSkeletonBoneDesc[] skeleton,
        AnityNative.AvatarHumanBoneDesc[] human)
    {
        Assert.True(AnityNative.TryValidateHumanAvatar(skeleton, human, out AnityNative.AvatarBuildResult result));
        return result;
    }

    private static AnityNative.AvatarSkeletonBoneDesc Bone(string name, int parentIndex)
    {
        return new AnityNative.AvatarSkeletonBoneDesc
        {
            nameHash = Hash(name),
            parentIndex = parentIndex,
            rotationW = 1f,
            scaleX = 1f,
            scaleY = 1f,
            scaleZ = 1f,
        };
    }

    private static AnityNative.AvatarHumanBoneDesc[] RequiredMappings(string boneName)
    {
        return RequiredHumanNames.Select(humanName => new AnityNative.AvatarHumanBoneDesc
        {
            boneNameHash = Hash(boneName),
            humanNameHash = Hash(humanName),
        }).ToArray();
    }

    private static AnityNative.AvatarSkeletonBoneDesc[] RequiredUniqueSkeleton()
    {
        return new[] { Bone("Root", -1) }
            .Concat(RequiredHumanNames.Select((_, index) => Bone("Bone" + index, 0)))
            .ToArray();
    }

    private static AnityNative.AvatarHumanBoneDesc[] RequiredUniqueMappings()
    {
        return RequiredHumanNames.Select((humanName, index) => new AnityNative.AvatarHumanBoneDesc
        {
            boneNameHash = Hash("Bone" + index),
            humanNameHash = Hash(humanName),
        }).ToArray();
    }

    private static (GameObject Root, HumanDescription Description) CreateValidHumanRig()
    {
        var root = new GameObject("Root");
        GameObject hips = AddChild(root, "HipsBone");
        GameObject spine = AddChild(hips, "SpineBone");
        _ = AddChild(spine, "HeadBone");
        GameObject leftUpperArm = AddChild(spine, "LeftUpperArmBone");
        GameObject rightUpperArm = AddChild(spine, "RightUpperArmBone");
        GameObject leftLowerArm = AddChild(leftUpperArm, "LeftLowerArmBone");
        GameObject rightLowerArm = AddChild(rightUpperArm, "RightLowerArmBone");
        _ = AddChild(leftLowerArm, "LeftHandBone");
        _ = AddChild(rightLowerArm, "RightHandBone");
        GameObject leftUpperLeg = AddChild(hips, "LeftUpperLegBone");
        GameObject rightUpperLeg = AddChild(hips, "RightUpperLegBone");
        GameObject leftLowerLeg = AddChild(leftUpperLeg, "LeftLowerLegBone");
        GameObject rightLowerLeg = AddChild(rightUpperLeg, "RightLowerLegBone");
        _ = AddChild(leftLowerLeg, "LeftFootBone");
        _ = AddChild(rightLowerLeg, "RightFootBone");

        Transform[] transforms = Enumerate(root.transform).ToArray();
        var skeleton = transforms.Select(transform => new SkeletonBone
        {
            name = transform.gameObject.name,
            position = transform.localPosition,
            rotation = transform.localRotation,
            scale = transform.localScale,
        }).ToArray();
        var human = RequiredHumanNames.Select(humanName => new HumanBone
        {
            boneName = humanName + "Bone",
            humanName = humanName,
        }).ToArray();
        return (root, new HumanDescription { skeleton = skeleton, human = human });
    }

    private static GameObject AddChild(GameObject parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static IEnumerable<Transform> Enumerate(Transform root)
    {
        yield return root;
        for (int index = 0; index < root.childCount; ++index)
        {
            foreach (Transform child in Enumerate(root.GetChild(index))) yield return child;
        }
    }

    private static ulong Hash(string value)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte item in Encoding.UTF8.GetBytes(value))
        {
            hash ^= item;
            hash *= 1099511628211UL;
        }
        return hash;
    }
}
