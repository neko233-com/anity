using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;
using EditorAssetImporter = UnityEditor.AssetImporter;

namespace Anity.Core.Tests;

/// <summary>ModelImporter registry and the common Unity 2022 YAML sections are kept in one importer identity.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class ModelImporterYamlTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-model-importer-yaml-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public ModelImporterYamlTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact] public void ImportAsset_RegistersStableModelImporter() { var path = ImportModel(); var first = ModelImporter.GetAtPath(path); Assert.Same(first, ModelImporter.GetAtPath(path)); Assert.IsType<ModelImporter>(EditorAssetImporter.GetAtPath(path)); }
    [Fact] public void ModelYaml_ReadsMaterialSettings() { var model = ModelImporter.GetAtPath(ImportModel()); Assert.True(model.importMaterials); Assert.Equal(ModelImporterMaterialImportMode.ImportStandard, model.materialImportMode); Assert.Equal(ModelImporterMaterialName.BasedOnTextureName, model.materialName); Assert.Equal(ModelImporterMaterialSearch.Everywhere, model.materialSearch); }
    [Fact] public void ModelYaml_ReadsAnimationSettings() { var model = ModelImporter.GetAtPath(ImportModel()); Assert.True(model.bakeSimulation); Assert.False(model.resampleCurves); Assert.True(model.optimizeGameObjects); Assert.Equal(ModelImporterAnimationCompression.Optimal, model.animationCompression); Assert.Equal(.25f, model.animationRotationError); Assert.True(model.isReadable); }
    [Fact] public void ModelYaml_ReadsMeshSettings() { var model = ModelImporter.GetAtPath(ImportModel()); Assert.Equal(2f, model.globalScale); Assert.Equal(ModelImporterMeshCompression.High, model.meshCompression); Assert.True(model.addCollider); Assert.False(model.importVisibility); Assert.False(model.importBlendShapes); Assert.False(model.importCameras); Assert.False(model.importLights); Assert.True(model.generateSecondaryUV); Assert.Equal(ModelImporterIndexFormat.UInt32, model.indexFormat); }
    [Fact] public void ModelYaml_ReadsTangentAnimationAndUserData() { var model = ModelImporter.GetAtPath(ImportModel()); Assert.Equal(ModelImporterNormals.Calculate, model.importNormals); Assert.Equal(ModelImporterTangents.CalculateLegacy, model.importTangents); Assert.False(model.importAnimation); Assert.Equal(ModelImporterAnimationType.Human, model.animationType); Assert.Equal("model-data", model.editorUserSettingsData); }
    [Fact] public void ModelYaml_ReadsOfficialAvatarSetupAndAutoMapping() { var model = ModelImporter.GetAtPath(ImportModel()); Assert.Equal(ModelImporterAvatarSetup.CopyFromOther, model.avatarSetup); Assert.False(model.autoGenerateAvatarMappingIfUnspecified); }
    [Fact] public void ModelYaml_SaveSettingsWritesOfficialAvatarSetupAndAutoMapping() { var path = ImportModel(); var model = ModelImporter.GetAtPath(path); model.avatarSetup = ModelImporterAvatarSetup.NoAvatar; model.autoGenerateAvatarMappingIfUnspecified = true; model.SaveSettings(); var meta = Meta(path); Assert.Contains("avatarSetup: 0", meta); Assert.Contains("autoGenerateAvatarMappingIfUnspecified: 1", meta); }
    [Fact] public void ModelYaml_ReadsHumanDescriptionScalars() { var human = ModelImporter.GetAtPath(ImportModel()).humanDescription; Assert.Equal(.5f, human.upperArmTwist); Assert.Equal(.4f, human.lowerArmTwist); Assert.Equal(.3f, human.upperLegTwist); Assert.Equal(.2f, human.lowerLegTwist); Assert.Equal(.05f, human.armStretch); Assert.Equal(.06f, human.legStretch); Assert.Equal(.1f, human.feetSpacing); Assert.True(human.hasTranslationDoF); }
    [Fact] public void ModelYaml_SaveSettingsWritesHumanDescriptionScalars() { var path = ImportModel(); var model = ModelImporter.GetAtPath(path); model.humanDescription = new HumanDescription { upperArmTwist = .9f, lowerArmTwist = .8f, upperLegTwist = .7f, lowerLegTwist = .6f, armStretch = .05f, legStretch = .04f, feetSpacing = .3f, hasTranslationDoF = false }; model.SaveSettings(); var meta = Meta(path); Assert.Contains("armTwist: 0.9", meta); Assert.Contains("foreArmTwist: 0.8", meta); Assert.Contains("upperLegTwist: 0.7", meta); Assert.Contains("legTwist: 0.6", meta); Assert.Contains("feetSpacing: 0.3", meta); Assert.Contains("hasTranslationDoF: 0", meta); Assert.Contains("hasExtraRoot: 1", meta); }
    [Theory]
    [InlineData("Idle", 0, 30, 1, 0, 0, 0, 0, 0, 0)]
    [InlineData("Walk", 1, 31, 0, 1, 0, 0, 0, 1, 1)]
    [InlineData("Run", 2, 32, 1, 1, 1, 0, 0, 2, 0)]
    [InlineData("Jump", 3, 33, 0, 0, 0, 1, 0, 3, 1)]
    [InlineData("Fall", 4, 34, 1, 0, 1, 1, 0, 0, 0)]
    [InlineData("Land", 5, 35, 0, 1, 0, 0, 1, 1, 1)]
    [InlineData("Attack", 6, 36, 1, 1, 1, 1, 1, 2, 0)]
    [InlineData("Hit", 7, 37, 0, 0, 0, 1, 1, 3, 1)]
    [InlineData("Dance", 8, 38, 1, 0, 1, 0, 1, 0, 0)]
    [InlineData("Emote", 9, 39, 0, 1, 0, 1, 1, 1, 1)]
    public void ModelYaml_ReadsClipAnimationScalars(string name, int first, int last, int loopTime, int loopPose, int lockRotation, int lockHeight, int mirror, int wrapMode, int additive) { var path = ImportModelWithClip(name, first, last, loopTime, loopPose, lockRotation, lockHeight, mirror, wrapMode, additive); var clip = Assert.Single(ModelImporter.GetAtPath(path).clipAnimations); Assert.Equal(name, clip.name); Assert.Equal(name + "Take", clip.takeName); Assert.Equal((float)first, clip.firstFrame); Assert.Equal((float)last, clip.lastFrame); Assert.Equal(loopTime == 1, clip.loopTime); Assert.Equal(loopPose == 1, clip.loopPose); Assert.Equal(lockRotation == 1, clip.lockRootRotation); Assert.Equal(lockHeight == 1, clip.lockRootHeightY); Assert.Equal(mirror == 1, clip.mirror); Assert.Equal((WrapMode)wrapMode, clip.wrapMode); Assert.Equal(additive == 1, clip.hasAdditiveReferencePose); }
    [Fact] public void ModelYaml_SaveSettingsWritesExistingClipAndPreservesUnknownFields() { var path = ImportModelWithClip("Idle", 0, 30, 0, 0, 0, 0, 0, 0, 0); var model = ModelImporter.GetAtPath(path); var clip = Assert.Single(model.clipAnimations); clip.name = "Idle Final"; clip.takeName = "Take Final"; clip.firstFrame = 2; clip.lastFrame = 40; clip.loopTime = true; clip.loopPose = true; clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = false; clip.mirror = true; clip.wrapMode = WrapMode.Loop; clip.cycleOffset = .25f; clip.keepOriginalOrientation = false; clip.keepOriginalPositionY = true; clip.keepOriginalPositionXZ = false; clip.heightFromFeet = true; clip.hasAdditiveReferencePose = true; model.clipAnimations = new[] { clip }; model.SaveSettings(); var meta = Meta(path); Assert.Contains("name: \"Idle Final\"", meta); Assert.Contains("takeName: \"Take Final\"", meta); Assert.Contains("firstFrame: 2", meta); Assert.Contains("lastFrame: 40", meta); Assert.Contains("loopTime: 1", meta); Assert.Contains("mirror: 1", meta); Assert.Contains("cycleOffset: 0.25", meta); Assert.Contains("hasAdditiveReferencePose: 1", meta); Assert.Contains("serializedVersion: 16", meta); }
    [Fact] public void ModelYaml_SaveSettingsAppendsNewClipWithoutReplacingExistingClip() { var path = ImportModelWithClip("Idle", 0, 30, 0, 0, 0, 0, 0, 0, 0); var model = ModelImporter.GetAtPath(path); var idle = Assert.Single(model.clipAnimations); var run = new ModelImporterClipAnimation { name = "Run", takeName = "RunTake", firstFrame = 31, lastFrame = 60, loopTime = true, mirror = true, wrapMode = WrapMode.Loop, cycleOffset = .5f, hasAdditiveReferencePose = true }; model.clipAnimations = new[] { idle, run }; model.SaveSettings(); var meta = Meta(path); Assert.Contains("name: Idle", meta); Assert.Contains("name: Run", meta); Assert.Contains("takeName: RunTake", meta); Assert.Contains("firstFrame: 31", meta); Assert.Contains("lastFrame: 60", meta); Assert.Contains("cycleOffset: 0.5", meta); }
    [Fact] public void ModelYaml_SaveSettingsConvertsEmptyClipArrayWhenAddingClip() { var path = ImportModelWithEmptyClipArray(); var model = ModelImporter.GetAtPath(path); Assert.Empty(model.clipAnimations); model.clipAnimations = new[] { new ModelImporterClipAnimation { name = "First", takeName = "FirstTake", lastFrame = 12 } }; model.SaveSettings(); var meta = Meta(path); Assert.DoesNotContain("clipAnimations: []", meta); Assert.Contains("clipAnimations:", meta); Assert.Contains("name: First", meta); }
    [Fact] public void ModelYaml_SaveSettingsDeletesTrailingClipBlocksWhenArrayShrinks() { var path = ImportModelWithClip("Idle", 0, 30, 0, 0, 0, 0, 0, 0, 0); var model = ModelImporter.GetAtPath(path); var idle = Assert.Single(model.clipAnimations); model.clipAnimations = new[] { idle, new ModelImporterClipAnimation { name = "Run", takeName = "RunTake", firstFrame = 31, lastFrame = 60 } }; model.SaveSettings(); model.clipAnimations = new[] { idle }; model.SaveSettings(); var meta = Meta(path); Assert.Contains("name: Idle", meta); Assert.DoesNotContain("name: Run", meta); Assert.DoesNotContain("takeName: RunTake", meta); }
    [Fact] public void ModelYaml_SaveSettingsWritesMaterialSettings() { var path = ImportModelWithOfficialMaterialSettings(1, 0, 1); var model = ModelImporter.GetAtPath(path); model.materialImportMode = ModelImporterMaterialImportMode.None; model.materialName = ModelImporterMaterialName.BasedOnModelNameAndMaterialName; model.materialSearch = ModelImporterMaterialSearch.Local; model.SaveSettings(); var meta = Meta(path); Assert.Contains("materialImportMode: 0", meta); Assert.Contains("materialName: 2", meta); Assert.Contains("materialSearch: 0", meta); }
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void ModelYaml_ReadsOfficialMaterialImportMode(int mode, bool importMaterials) { var model = ModelImporter.GetAtPath(ImportModelWithOfficialMaterialSettings(mode, 0, 1)); Assert.Equal((ModelImporterMaterialImportMode)mode, model.materialImportMode); Assert.Equal(importMaterials, model.importMaterials); }
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void ModelYaml_SaveSettingsWritesOfficialMaterialImportMode(int mode, bool importMaterials) { var path = ImportModelWithOfficialMaterialSettings(1, 0, 1); var model = ModelImporter.GetAtPath(path); model.materialImportMode = (ModelImporterMaterialImportMode)mode; model.SaveSettings(); var reloaded = ModelImporter.GetAtPath(path); Assert.Contains("materialImportMode: " + mode, Meta(path)); Assert.Equal(importMaterials, reloaded.importMaterials); }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ModelYaml_ReadsAndWritesOfficialMaterialLocation(int location) { var path = ImportModelWithOfficialMaterialSettings(1, location, 1); var model = ModelImporter.GetAtPath(path); Assert.Equal((ModelImporterMaterialLocation)location, model.materialLocation); model.materialLocation = location == 0 ? ModelImporterMaterialLocation.InPrefab : ModelImporterMaterialLocation.External; model.SaveSettings(); Assert.Contains("materialLocation: " + (location == 0 ? 1 : 0), Meta(path)); }
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void ModelYaml_ReadsAndWritesUseSrgbMaterialColor(int yamlValue, bool expected) { var path = ImportModelWithOfficialMaterialSettings(1, 0, yamlValue); var model = ModelImporter.GetAtPath(path); Assert.Equal(expected, model.useSRGBMaterialColor); model.useSRGBMaterialColor = !expected; model.SaveSettings(); Assert.Contains("useSRGBMaterialColor: " + (expected ? "0" : "1"), Meta(path)); }
    [Theory]
    [InlineData("bakeIK", true)]
    [InlineData("removeConstantScaleCurves", false)]
    [InlineData("importAnimatedCustomProperties", true)]
    [InlineData("importConstraints", false)]
    [InlineData("importPhysicalCameras", false)]
    [InlineData("sortHierarchyByName", false)]
    [InlineData("bakeAxisConversion", true)]
    [InlineData("preserveHierarchy", true)]
    [InlineData("strictVertexDataChecks", true)]
    [InlineData("importBlendShapeDeformPercent", false)]
    public void ModelYaml_ReadsAndWritesOfficialBooleanSettings(string propertyName, bool expected)
    {
        var path = ImportModelWithOfficialBooleanSettings();
        var model = ModelImporter.GetAtPath(path);
        var property = typeof(ModelImporter).GetProperty(propertyName)!;
        Assert.Equal(expected, (bool)property.GetValue(model)!);
        property.SetValue(model, !expected);
        model.SaveSettings();
        Assert.Contains(propertyName + ": " + (expected ? "0" : "1"), Meta(path));
    }
    [Theory]
    [InlineData("Hips", "Hips", 1f)]
    [InlineData("LeftUpperLeg", "LeftUpperLeg", 2f)]
    [InlineData("RightUpperLeg", "RightUpperLeg", 3f)]
    [InlineData("Spine", "Spine", 4f)]
    [InlineData("Chest", "Chest", 5f)]
    [InlineData("LeftUpperArm", "LeftUpperArm", 6f)]
    [InlineData("RightUpperArm", "RightUpperArm", 7f)]
    [InlineData("LeftHand", "LeftHand", 8f)]
    [InlineData("RightHand", "RightHand", 9f)]
    [InlineData("Head", "Head", 10f)]
    public void ModelYaml_ReadsOfficialHumanBoneMapping(string boneName, string humanName, float offset)
    {
        var bone = Assert.Single(ModelImporter.GetAtPath(ImportModelWithHumanBone(boneName, humanName, offset)).humanDescription.human);
        Assert.Equal(boneName, bone.boneName); Assert.Equal(humanName, bone.humanName); Assert.Equal(offset, bone.limit.min.x); Assert.Equal(offset + 1, bone.limit.max.y); Assert.Equal(offset + 2, bone.limit.center.z); Assert.Equal(offset + 3, bone.limit.axisLength); Assert.True(bone.limit.useDefaultValues);
    }
    [Theory]
    [InlineData("Hips", "Hips", 1f)]
    [InlineData("LeftUpperLeg", "LeftUpperLeg", 2f)]
    [InlineData("RightUpperLeg", "RightUpperLeg", 3f)]
    [InlineData("Spine", "Spine", 4f)]
    [InlineData("Chest", "Chest", 5f)]
    [InlineData("LeftUpperArm", "LeftUpperArm", 6f)]
    [InlineData("RightUpperArm", "RightUpperArm", 7f)]
    [InlineData("LeftHand", "LeftHand", 8f)]
    [InlineData("RightHand", "RightHand", 9f)]
    [InlineData("Head", "Head", 10f)]
    public void ModelYaml_SaveSettingsWritesOfficialHumanBoneMapping(string boneName, string humanName, float offset)
    {
        var path = ImportModelWithHumanBone("OldBone", "OldHuman", 0);
        var model = ModelImporter.GetAtPath(path);
        var description = model.humanDescription;
        description.human = new[] { CreateHumanBone(boneName, humanName, offset, false) };
        model.humanDescription = description;
        model.SaveSettings();
        var meta = Meta(path);
        Assert.Contains("boneName: " + boneName, meta);
        Assert.Contains("humanName: " + humanName, meta);
        Assert.Contains("min: {x: " + offset + ", y: " + (offset + 1) + ", z: " + (offset + 2) + "}", meta);
        Assert.Contains("max: {x: " + (offset + 3) + ", y: " + (offset + 4) + ", z: " + (offset + 5) + "}", meta);
        Assert.Contains("value: {x: " + (offset + 6) + ", y: " + (offset + 7) + ", z: " + (offset + 8) + "}", meta);
        Assert.Contains("length: " + (offset + 9), meta);
        Assert.Contains("modified: 1", meta);
    }
    [Fact] public void ModelYaml_SaveSettingsAppendsHumanBoneAndRoundTripsAcrossProjectSession() { var path = ImportModelWithHumanBone("Hips", "Hips", 1); var model = ModelImporter.GetAtPath(path); var description = model.humanDescription; description.human = new[] { description.human[0], CreateHumanBone("Head", "Head", 20, true) }; model.humanDescription = description; model.SaveSettings(); EditorApplication.OpenProject(_dir); AssetDatabase.ImportAsset(path); var bones = ModelImporter.GetAtPath(path).humanDescription.human; Assert.Equal(2, bones.Length); Assert.Equal("Hips", bones[0].boneName); Assert.Equal("Head", bones[1].boneName); Assert.Equal(29, bones[1].limit.axisLength); Assert.True(bones[1].limit.useDefaultValues); }
    [Fact] public void ModelYaml_SaveSettingsConvertsEmptyHumanArrayWhenAddingBone() { var path = ImportModelWithEmptyHumanArray(); var model = ModelImporter.GetAtPath(path); var description = model.humanDescription; description.human = new[] { CreateHumanBone("Hips", "Hips", 1, true) }; model.humanDescription = description; model.SaveSettings(); var meta = Meta(path); Assert.DoesNotContain("human: []", meta); Assert.Contains("human:\n    - boneName: Hips", meta); }
    [Fact] public void ModelYaml_SaveSettingsAddsMissingHumanArrayToExistingDescription() { var path = ImportModel(); var model = ModelImporter.GetAtPath(path); var description = model.humanDescription; description.human = new[] { CreateHumanBone("Spine", "Spine", 2, true) }; model.humanDescription = description; model.SaveSettings(); var meta = Meta(path); Assert.Contains("    human:\n    - boneName: Spine", meta); Assert.Contains("  animationType: 3", meta); }
    [Fact] public void ModelYaml_SaveSettingsDeletesTrailingHumanBonesWhenArrayShrinks() { var path = ImportModelWithHumanBone("Hips", "Hips", 1); var model = ModelImporter.GetAtPath(path); var description = model.humanDescription; description.human = new[] { description.human[0], CreateHumanBone("Head", "Head", 20, true) }; model.humanDescription = description; model.SaveSettings(); description = model.humanDescription; description.human = new[] { description.human[0] }; model.humanDescription = description; model.SaveSettings(); var meta = Meta(path); Assert.Contains("boneName: Hips", meta); Assert.DoesNotContain("boneName: Head", meta); }
    [Fact] public void ModelYaml_SaveSettingsWritesEmptyHumanArrayWhenMappingsAreCleared() { var path = ImportModelWithHumanBone("Hips", "Hips", 1); var model = ModelImporter.GetAtPath(path); var description = model.humanDescription; description.human = Array.Empty<HumanBone>(); model.humanDescription = description; model.SaveSettings(); var meta = Meta(path); Assert.Contains("human: []", meta); Assert.DoesNotContain("boneName:", meta); Assert.DoesNotContain("humanName:", meta); }
    [Fact] public void ModelYaml_SaveSettingsTreatsNullHumanArrayAsEmpty() { var path = ImportModelWithHumanBone("Hips", "Hips", 1); var model = ModelImporter.GetAtPath(path); var description = model.humanDescription; description.human = null!; model.humanDescription = description; model.SaveSettings(); Assert.Contains("human: []", Meta(path)); }
    [Fact] public void ModelYaml_SaveSettingsPreservesUnknownHumanBoneAndLimitFields() { var path = ImportModelWithHumanBone("Hips", "Hips", 1); var metaPath = FullPath(path) + ".meta"; File.WriteAllText(metaPath, Meta(path).Replace("      humanName: Hips", "      humanName: Hips\n      futureBoneField: 88", StringComparison.Ordinal).Replace("        modified: 0", "        modified: 0\n        futureLimitField: 77", StringComparison.Ordinal)); AssetDatabase.ImportAsset(path); var model = ModelImporter.GetAtPath(path); var description = model.humanDescription; description.human = new[] { CreateHumanBone("Spine", "Spine", 3, true) }; model.humanDescription = description; model.SaveSettings(); var saved = Meta(path); Assert.Contains("futureBoneField: 88", saved); Assert.Contains("futureLimitField: 77", saved); }
    [Fact] public void ModelYaml_SaveSettingsQuotesHumanBoneNamesThatNeedYamlEscaping() { var path = ImportModelWithHumanBone("Hips", "Hips", 1); var model = ModelImporter.GetAtPath(path); var description = model.humanDescription; description.human = new[] { CreateHumanBone("Rig: Left Arm", "Left Arm", 1, true) }; model.humanDescription = description; model.SaveSettings(); var meta = Meta(path); Assert.Contains("boneName: \"Rig: Left Arm\"", meta); Assert.Contains("humanName: \"Left Arm\"", meta); }
    [Fact] public void ModelYaml_SaveSettingsWritesAnimationSettings() { var path = ImportModel(); var model = ModelImporter.GetAtPath(path); model.bakeSimulation = false; model.resampleCurves = true; model.optimizeGameObjects = false; model.animationCompression = ModelImporterAnimationCompression.Off; model.animationRotationError = .75f; model.animationPositionError = .8f; model.animationScaleError = .9f; model.isReadable = false; model.SaveSettings(); var meta = Meta(path); Assert.Contains("bakeSimulation: 0", meta); Assert.Contains("resampleCurves: 1", meta); Assert.Contains("optimizeGameObjects: 0", meta); Assert.Contains("animationCompression: 0", meta); Assert.Contains("animationRotationError: 0.75", meta); Assert.Contains("isReadable: 0", meta); }
    [Fact] public void ModelYaml_SaveSettingsWritesMeshSettings() { var path = ImportModel(); var model = ModelImporter.GetAtPath(path); model.globalScale = 1.25f; model.meshCompression = ModelImporterMeshCompression.Low; model.addCollider = false; model.importVisibility = true; model.importBlendShapes = true; model.importCameras = true; model.importLights = true; model.swapUVChannels = true; model.generateSecondaryUV = false; model.optimizeMesh = false; model.keepQuads = true; model.weldVertices = false; model.indexFormat = ModelImporterIndexFormat.UInt16; model.SaveSettings(); var meta = Meta(path); Assert.Contains("globalScale: 1.25", meta); Assert.Contains("meshCompression: 1", meta); Assert.Contains("addColliders: 0", meta); Assert.Contains("swapUVChannels: 1", meta); Assert.Contains("keepQuads: 1", meta); Assert.Contains("weldVertices: 0", meta); Assert.Contains("indexFormat: 1", meta); }
    [Fact] public void ModelYaml_SaveSettingsWritesTangentAnimationTypeAndUserData() { var path = ImportModel(); var model = ModelImporter.GetAtPath(path); model.importNormals = ModelImporterNormals.None; model.importTangents = ModelImporterTangents.None; model.importAnimation = true; model.animationType = ModelImporterAnimationType.Generic; model.editorUserSettingsData = "model: final"; model.SaveSettings(); var meta = Meta(path); Assert.Contains("normalImportMode: 2", meta); Assert.Contains("tangentImportMode: 3", meta); Assert.Contains("importAnimation: 1", meta); Assert.Contains("animationType: 2", meta); Assert.Contains("userData: \"model: final\"", meta); }
    [Fact] public void ModelYaml_SaveSettingsPreservesUnknownFields() { var path = ImportModel(); var model = ModelImporter.GetAtPath(path); model.globalScale = 3; model.SaveSettings(); Assert.Contains("futureModelField: 99", Meta(path)); }
    [Fact] public void DefaultImporterYaml_ReadsAndWritesUserDataWithoutChangingUnknownFields() { var path = "Assets/Models/" + Guid.NewGuid().ToString("N") + ".txt"; Write(path, "plain"); File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nDefaultImporter:\n  externalObjects: {}\n  userData: old\n  futureDefaultField: 7\n  assetBundleName: \n  assetBundleVariant: \n"); AssetDatabase.ImportAsset(path); var importer = EditorAssetImporter.GetAtPath(path); Assert.Equal("old", importer.editorUserSettingsData); importer.editorUserSettingsData = "new: data"; importer.SaveSettings(); var meta = Meta(path); Assert.Contains("userData: \"new: data\"", meta); Assert.Contains("futureDefaultField: 7", meta); }

    private string ImportModel()
    {
        var path = "Assets/Models/" + Guid.NewGuid().ToString("N") + ".fbx";
        Write(path, "Kaydara FBX Binary");
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nModelImporter:\n  serializedVersion: 23\n  materials:\n    importMaterials: 1\n    materialName: 1\n    materialSearch: 2\n  animations:\n    bakeSimulation: 1\n    resampleCurves: 0\n    optimizeGameObjects: 1\n    animationCompression: 3\n    animationRotationError: 0.25\n    animationPositionError: 0.3\n    animationScaleError: 0.4\n    isReadable: 1\n  meshes:\n    globalScale: 2\n    meshCompression: 3\n    addColliders: 1\n    importVisibility: 0\n    importBlendShapes: 0\n    importCameras: 0\n    importLights: 0\n    swapUVChannels: 0\n    generateSecondaryUV: 1\n    useFileUnits: 1\n    optimizeMeshForGPU: 1\n    keepQuads: 0\n    weldVertices: 1\n    indexFormat: 2\n    secondaryUVAngleDistortion: 7\n    secondaryUVAreaDistortion: 12\n    secondaryUVHardAngle: 66\n    secondaryUVPackMargin: 3\n    useFileScale: 1\n  tangentSpace:\n    normalImportMode: 1\n    tangentImportMode: 1\n  importAnimation: 0\n  autoGenerateAvatarMappingIfUnspecified: 0\n  humanDescription:\n    armTwist: 0.5\n    foreArmTwist: 0.4\n    upperLegTwist: 0.3\n    legTwist: 0.2\n    armStretch: 0.05\n    legStretch: 0.06\n    feetSpacing: 0.1\n    hasTranslationDoF: 1\n    hasExtraRoot: 1\n  animationType: 3\n  avatarSetup: 2\n  userData: model-data\n  futureModelField: 99\n  assetBundleName: \n  assetBundleVariant: \n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private string ImportModelWithOfficialMaterialSettings(int materialImportMode, int materialLocation, int useSrgbMaterialColor)
    {
        var path = ImportModel();
        var metaPath = FullPath(path) + ".meta";
        var contents = File.ReadAllText(metaPath)
            .Replace("    importMaterials: 1", "    materialImportMode: " + materialImportMode + "\n    materialLocation: " + materialLocation, StringComparison.Ordinal)
            .Replace("    globalScale: 2", "    useSRGBMaterialColor: " + useSrgbMaterialColor + "\n    globalScale: 2", StringComparison.Ordinal);
        File.WriteAllText(metaPath, contents);
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private string ImportModelWithOfficialBooleanSettings()
    {
        var path = ImportModel();
        var metaPath = FullPath(path) + ".meta";
        var contents = File.ReadAllText(metaPath)
            .Replace("    bakeSimulation: 1", "    bakeSimulation: 1\n    bakeIK: 1\n    removeConstantScaleCurves: 0\n    importAnimatedCustomProperties: 1\n    importConstraints: 0", StringComparison.Ordinal)
            .Replace("    globalScale: 2", "    globalScale: 2\n    importPhysicalCameras: 0\n    sortHierarchyByName: 0\n    bakeAxisConversion: 1\n    preserveHierarchy: 1\n    strictVertexDataChecks: 1", StringComparison.Ordinal)
            .Replace("  importAnimation: 0", "  importAnimation: 0\n  importBlendShapeDeformPercent: 0", StringComparison.Ordinal);
        File.WriteAllText(metaPath, contents);
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private string ImportModelWithHumanBone(string boneName, string humanName, float offset)
    {
        var path = "Assets/Models/" + Guid.NewGuid().ToString("N") + ".fbx";
        Write(path, "Kaydara FBX Binary");
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nModelImporter:\n  serializedVersion: 23\n  humanDescription:\n    human:\n    - boneName: " + boneName + "\n      humanName: " + humanName + "\n      limit:\n        min: {x: " + offset + ", y: 0, z: 0}\n        max: {x: 0, y: " + (offset + 1) + ", z: 0}\n        value: {x: 0, y: 0, z: " + (offset + 2) + "}\n        length: " + (offset + 3) + "\n        modified: 0\n  assetBundleName: \n  assetBundleVariant: \n");
        AssetDatabase.ImportAsset(path); return path;
    }

    private string ImportModelWithEmptyHumanArray()
    {
        var path = "Assets/Models/" + Guid.NewGuid().ToString("N") + ".fbx";
        Write(path, "Kaydara FBX Binary");
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nModelImporter:\n  serializedVersion: 23\n  humanDescription:\n    human: []\n  assetBundleName: \n  assetBundleVariant: \n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private static HumanBone CreateHumanBone(string boneName, string humanName, float offset, bool useDefaultValues) => new()
    {
        boneName = boneName,
        humanName = humanName,
        limit = new HumanLimit
        {
            min = new Vector3(offset, offset + 1, offset + 2),
            max = new Vector3(offset + 3, offset + 4, offset + 5),
            center = new Vector3(offset + 6, offset + 7, offset + 8),
            axisLength = offset + 9,
            useDefaultValues = useDefaultValues,
        },
    };

    private string ImportModelWithClip(string name, int first, int last, int loopTime, int loopPose, int lockRotation, int lockHeight, int mirror, int wrapMode, int additive)
    {
        var path = "Assets/Models/" + Guid.NewGuid().ToString("N") + ".fbx";
        Write(path, "Kaydara FBX Binary");
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nModelImporter:\n  serializedVersion: 23\n  animations:\n    clipAnimations:\n    - serializedVersion: 16\n      name: " + name + "\n      takeName: " + name + "Take\n      firstFrame: " + first + "\n      lastFrame: " + last + "\n      loopTime: " + loopTime + "\n      loopPose: " + loopPose + "\n      lockRootRotation: " + lockRotation + "\n      lockRootHeightY: " + lockHeight + "\n      lockRootPositionXZ: 1\n      mirror: " + mirror + "\n      wrapMode: " + wrapMode + "\n      cycleOffset: 0.5\n      keepOriginalOrientation: 1\n      keepOriginalPositionY: 0\n      keepOriginalPositionXZ: 1\n      heightFromFeet: 0\n      hasAdditiveReferencePose: " + additive + "\n  assetBundleName: \n  assetBundleVariant: \n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private string ImportModelWithEmptyClipArray()
    {
        var path = "Assets/Models/" + Guid.NewGuid().ToString("N") + ".fbx";
        Write(path, "Kaydara FBX Binary");
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nModelImporter:\n  serializedVersion: 23\n  animations:\n    clipAnimations: []\n  assetBundleName: \n  assetBundleVariant: \n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private void Write(string path, string contents)
    {
        var fullPath = FullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
    }

    private string Meta(string path) => File.ReadAllText(FullPath(path) + ".meta");
    private string FullPath(string path) => Path.Combine(_dir, path);
}
