using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class ImportedHumanoidModelTests : IDisposable
{
    private readonly string _project = Path.Combine(
        Path.GetTempPath(), "anity-imported-humanoid-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();
    private readonly string _assetPath = "Assets/Models/HumanoidRootMotion.fbx";
    private readonly GameObject _root;
    private readonly Animator _animator;
    private readonly Avatar _avatar;
    private readonly AnimationClip _clip;

    public ImportedHumanoidModelTests()
    {
        Directory.CreateDirectory(Path.Combine(_project, "Assets", "Models"));
        EditorApplication.OpenProject(_project);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Models", "HumanoidRootMotion.fbx"),
            Path.Combine(_project, _assetPath));
        AssetDatabase.ImportAsset(_assetPath);
        ModelImporter importer = ModelImporter.GetAtPath(_assetPath);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.resampleCurves = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.humanDescription = Description();
        importer.SaveAndReimport();
        _root = AssetDatabase.LoadAssetAtPath<GameObject>(_assetPath)!;
        _animator = _root.GetComponent<Animator>()!;
        _avatar = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(_assetPath).OfType<Avatar>());
        _clip = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(_assetPath).OfType<AnimationClip>());
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_project, true); } catch { }
    }

    [Fact]
    public void HumanImportAddsAnimatorToPrefabRoot()
    {
        Assert.NotNull(_animator);
        Assert.Same(_avatar, _animator.avatar);
    }

    [Fact]
    public void ImportedAvatarIsValidHumanAvatar()
    {
        Assert.True(_avatar.isValid);
        Assert.True(_avatar.isHuman);
        Assert.True(_animator.isHuman);
    }

    [Fact]
    public void NativeTPoseMassDistributionMatchesUnityHumanScale()
    {
        Assert.Equal(1.12227261f, _animator.humanScale, 6);
    }

    [Fact]
    public void HumanClipUsesRootMetadataInsteadOfGenericMotionMetadata()
    {
        Assert.True(_clip.isHumanMotion);
        Assert.True(_clip.hasRootCurves);
        Assert.False(_clip.hasMotionCurves);
        Assert.False(_clip.hasMotionFloatCurves);
        Assert.False(_clip.hasGenericRootTransform);
    }

    [Fact]
    public void HumanClipExposesUnityRootBindings()
    {
        string[] rootBindings = _clip.bindings
            .Where(binding => binding.path.Length == 0 && binding.type == typeof(Animator))
            .Select(binding => binding.propertyName).OrderBy(value => value).ToArray();
        Assert.Equal(new[]
        {
            "RootQ.w", "RootQ.x", "RootQ.y", "RootQ.z",
            "RootT.x", "RootT.y", "RootT.z",
        }, rootBindings);
    }

    [Fact]
    public void HumanClipDoesNotRetainLegacyMotionBindings()
    {
        Assert.DoesNotContain(_clip.bindings, binding =>
            binding.propertyName.StartsWith("MotionT", StringComparison.Ordinal) ||
            binding.propertyName.StartsWith("MotionQ", StringComparison.Ordinal));
    }

    [Fact]
    public void RootRotationUsesHipsBodyRotation()
    {
        Assert.Equal(0f, Root("RootQ.x").Evaluate(0f), 6);
        Assert.Equal(1f, Root("RootQ.w").Evaluate(0f), 6);
        Assert.Equal(0.0616284162f, Root("RootQ.x").Evaluate(_clip.length), 5);
        Assert.Equal(0.704416037f, Root("RootQ.y").Evaluate(_clip.length), 5);
        Assert.Equal(-0.183012709f, Root("RootQ.z").Evaluate(_clip.length), 5);
        Assert.Equal(0.6830127f, Root("RootQ.w").Evaluate(_clip.length), 5);
    }

    [Fact]
    public void RootTranslationIsNormalizedByImportedHumanScale()
    {
        Transform hips = _root.transform.Find("Hips")!;
        float startX = Root("RootT.x").Evaluate(0f);
        float startY = Root("RootT.y").Evaluate(0f);
        float startZ = Root("RootT.z").Evaluate(0f);
        Assert.Equal(hips.localPosition.x, startX, 6);
        Assert.Equal(hips.localPosition.y, startY, 6);
        Assert.Equal(hips.localPosition.z, startZ, 6);
        Assert.Equal(2f / _animator.humanScale, Root("RootT.x").Evaluate(_clip.length) - startX, 5);
        Assert.Equal(1f / _animator.humanScale, Root("RootT.y").Evaluate(_clip.length) - startY, 5);
        Assert.Equal(1f / _animator.humanScale, Root("RootT.z").Evaluate(_clip.length) - startZ, 5);
    }

    [Fact]
    public void HipsPositionCurvesAreConsumedByHumanBodyRoot()
    {
        Assert.DoesNotContain(_clip.bindings, binding =>
            binding.path == "Hips" && binding.propertyName.StartsWith("m_LocalPosition", StringComparison.Ordinal));
    }

    [Fact]
    public void HipsResidualRotationRemovesBodyYawTwist()
    {
        AnimationCurve residualY = _clip.GetCurve("Hips", typeof(Transform), "m_LocalRotation.y");
        Assert.NotNull(residualY);
        Assert.InRange(MathF.Abs(residualY.Evaluate(_clip.length)), 0f, 1e-5f);
        Assert.Equal(0.174290746f,
            _clip.GetCurve("Hips", typeof(Transform), "m_LocalRotation.x").Evaluate(_clip.length), 5);
        Assert.Equal(-0.0831531659f,
            _clip.GetCurve("Hips", typeof(Transform), "m_LocalRotation.z").Evaluate(_clip.length), 5);
    }

    [Fact]
    public void AnimatorRootMotionConsumesImportedHumanScale()
    {
        var controller = new AnimatorController();
        AnimatorState state = controller.layers[0].stateMachine.AddState("HumanMotion");
        state.motion = _clip;
        controller.layers[0].stateMachine.defaultState = state;
        _animator.runtimeAnimatorController = controller;
        _animator.applyRootMotion = true;
        _animator.Rebind();
        _animator.Play(state.nameHash, 0, 0f);
        _animator.Update(0f);
        _animator.Update(_clip.length);
        Assert.Equal(2f, _animator.deltaPosition.x, 4);
        Assert.Equal(1f, _animator.deltaPosition.y, 4);
        Assert.Equal(1f, _animator.deltaPosition.z, 4);
    }

    [Fact]
    public void AvatarAssignmentRestoresScaleAfterGenericAvatar()
    {
        Avatar human = _animator.avatar;
        _animator.avatar = AvatarBuilder.BuildGenericAvatar(_root, string.Empty);
        Assert.Equal(1f, _animator.humanScale);
        _animator.avatar = human;
        Assert.Equal(1.12227261f, _animator.humanScale, 6);
    }

    private AnimationCurve Root(string property) =>
        _clip.GetCurve(string.Empty, typeof(Animator), property);

    private static HumanDescription Description()
    {
        string[] names =
        {
            "Hips", "Spine", "Chest", "Neck", "Head",
            "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot",
        };
        HumanBone[] human = names.Select(name => new HumanBone
        {
            boneName = name,
            humanName = name,
            limit = new HumanLimit { useDefaultValues = true },
        }).ToArray();
        var skeleton = new List<SkeletonBone>
        {
            Bone("HumanoidRoot", Vector3.zero),
            Bone("Hips", new Vector3(0f, 1f, 0f)),
            Bone("Spine", new Vector3(0f, .25f, 0f)),
            Bone("Chest", new Vector3(0f, .25f, 0f)),
            Bone("Neck", new Vector3(0f, .2f, 0f)),
            Bone("Head", new Vector3(0f, .2f, 0f)),
            Bone("LeftShoulder", new Vector3(-.15f, .15f, 0f)),
            Bone("LeftUpperArm", new Vector3(-.25f, 0f, 0f)),
            Bone("LeftLowerArm", new Vector3(-.3f, 0f, 0f)),
            Bone("LeftHand", new Vector3(-.25f, 0f, 0f)),
            Bone("RightShoulder", new Vector3(.15f, .15f, 0f)),
            Bone("RightUpperArm", new Vector3(.25f, 0f, 0f)),
            Bone("RightLowerArm", new Vector3(.3f, 0f, 0f)),
            Bone("RightHand", new Vector3(.25f, 0f, 0f)),
            Bone("LeftUpperLeg", new Vector3(-.12f, -.15f, 0f)),
            Bone("LeftLowerLeg", new Vector3(0f, -.45f, 0f)),
            Bone("LeftFoot", new Vector3(0f, -.4f, .12f)),
            Bone("RightUpperLeg", new Vector3(.12f, -.15f, 0f)),
            Bone("RightLowerLeg", new Vector3(0f, -.45f, 0f)),
            Bone("RightFoot", new Vector3(0f, -.4f, .12f)),
            Bone("BodyMesh", Vector3.zero),
        };
        return new HumanDescription
        {
            human = human,
            skeleton = skeleton.ToArray(),
            upperArmTwist = .5f,
            lowerArmTwist = .5f,
            upperLegTwist = .5f,
            lowerLegTwist = .5f,
            armStretch = .05f,
            legStretch = .05f,
        };
    }

    private static SkeletonBone Bone(string name, Vector3 position) => new()
    {
        name = name,
        position = position,
        rotation = Quaternion.identity,
        scale = Vector3.one,
    };
}
