using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Anity.Core.Runtime.Native;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;

namespace UnityEditor;

internal sealed class ImportedModelAsset
{
  internal GameObject MainObject { get; init; } = null!;
  internal List<UnityEngine.Object> SubAssets { get; init; } = new();
  internal AnimationClip[] AnimationClips { get; set; } = Array.Empty<AnimationClip>();
  internal Avatar? Avatar { get; init; }
  internal NativeModelDecoder.Scene DecodedScene { get; init; } = null!;
  internal GameObject[] NodeObjects { get; init; } = Array.Empty<GameObject>();
}

internal static class ModelAssetImportPipeline
{
  internal static bool TryImport(string fullPath, string assetPath, ModelImporter importer, out ImportedModelAsset imported, out string error)
  {
    imported = null!;
    var options = new NativeModelDecoder.Options
    {
      globalScale = importer.globalScale,
      useFileUnits = importer.useFileUnits && importer.useFileScale ? (byte)1 : (byte)0,
      // Animation data is decoded before OnPreprocessAnimation so that callback
      // can still enable or disable clip construction, matching Unity's phase order.
      importAnimation = 1,
      generateMissingNormals = importer.importNormals != ModelImporterNormals.None ? (byte)1 : (byte)0,
      importBlendShapes = importer.importBlendShapes ? (byte)1 : (byte)0,
      maxBonesPerVertex = importer.skinWeights == ModelImporterSkinWeights.Unlimited ? 8 : Math.Clamp(importer.maxBonesPerVertex, 1, 8),
      minBoneWeight = Math.Clamp(importer.minBoneWeight, 0f, 1f),
      resampleCurves = importer.resampleCurves ? (byte)1 : (byte)0,
    };
    if (!NativeModelDecoder.TryLoad(fullPath, options, out var decoded, out error)) return false;

    try
    {
      var meshes = BuildMeshes(decoded, importer);
      var root = BuildHierarchy(decoded, meshes, Path.GetFileNameWithoutExtension(assetPath), importer, out var nodes);
      var defaultClips = decoded.Clips.Select(clip => new ModelImporterClipAnimation
      {
        name = clip.Name,
        takeName = clip.Name,
        firstFrame = clip.FirstFrame,
        lastFrame = clip.LastFrame,
      }).ToArray();
      importer.SetImportedModelMetadata(decoded.FileScale, defaultClips);

      root.hideFlags = HideFlags.NotEditable;
      var subAssets = new List<UnityEngine.Object>(meshes.Length + decoded.Clips.Length);
      subAssets.AddRange(meshes);
      var avatar = BuildAvatar(root, assetPath, importer);
      if (avatar is not null)
      {
        subAssets.Add(avatar);
        if (importer.animationType == ModelImporterAnimationType.Human)
        {
          var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
          animator.avatar = avatar;
        }
      }
      imported = new ImportedModelAsset
      {
        MainObject = root,
        SubAssets = subAssets,
        Avatar = avatar,
        DecodedScene = decoded,
        NodeObjects = nodes,
      };
      return true;
    }
    catch (Exception exception)
    {
      error = "Could not construct imported model assets: " + exception.Message;
      return false;
    }
  }

  private static Avatar? BuildAvatar(GameObject root, string assetPath, ModelImporter importer)
  {
    if (importer.animationType == ModelImporterAnimationType.None || importer.avatarSetup == ModelImporterAvatarSetup.NoAvatar)
      return null;
    Avatar avatar;
    if (importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther && importer.sourceAvatar is { } source)
      avatar = Avatar.Create(source.isValid, source.isHuman, source.humanDescription, source.ValidationFlags, source.HumanScale);
    else if (importer.animationType == ModelImporterAnimationType.Human)
      avatar = AvatarBuilder.BuildHumanAvatar(root, importer.humanDescription);
    else
      avatar = AvatarBuilder.BuildGenericAvatar(root, string.Empty);
    avatar.name = Path.GetFileNameWithoutExtension(assetPath) + "Avatar";
    avatar.hideFlags = HideFlags.NotEditable;
    return avatar;
  }

  internal static void ImportAnimations(ImportedModelAsset imported, ModelImporter importer)
  {
    foreach (var oldClip in imported.AnimationClips) imported.SubAssets.Remove(oldClip);
    imported.AnimationClips = BuildAnimationClips(
      imported.DecodedScene, importer, imported.MainObject, imported.NodeObjects, imported.Avatar);
    imported.SubAssets.AddRange(imported.AnimationClips);
  }

  private static Mesh[] BuildMeshes(NativeModelDecoder.Scene decoded, ModelImporter importer)
  {
    var meshes = new Mesh[decoded.Meshes.Length];
    for (var meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
    {
      var source = decoded.Meshes[meshIndex];
      var vertices = new Vector3[source.Vertices.Length];
      var normals = new Vector3[source.Vertices.Length];
      var tangents = new Vector4[source.Vertices.Length];
      var uv = new Vector2[source.Vertices.Length];
      var hasTangents = false;
      for (var vertexIndex = 0; vertexIndex < source.Vertices.Length; vertexIndex++)
      {
        var vertex = source.Vertices[vertexIndex];
        vertices[vertexIndex] = new Vector3(vertex.positionX, vertex.positionY, vertex.positionZ);
        normals[vertexIndex] = new Vector3(vertex.normalX, vertex.normalY, vertex.normalZ);
        tangents[vertexIndex] = new Vector4(vertex.tangentX, vertex.tangentY, vertex.tangentZ, vertex.tangentW);
        // The decoder currently exposes the first UV set. Preserve it until
        // UV1 is present instead of fabricating a cleared channel for swapUVChannels.
        uv[vertexIndex] = new Vector2(vertex.uvX, vertex.uvY);
        hasTangents |= vertex.tangentX != 0f || vertex.tangentY != 0f || vertex.tangentZ != 0f;
      }

      var mesh = new Mesh
      {
        name = string.IsNullOrEmpty(source.Name) ? $"Mesh{meshIndex}" : source.Name,
        vertices = vertices,
        uv = uv,
        isReadable = importer.isReadable,
        indexFormat = importer.indexFormat == ModelImporterIndexFormat.UInt32 || vertices.Length > ushort.MaxValue
          ? IndexFormat.UInt32 : IndexFormat.UInt16,
        hideFlags = HideFlags.NotEditable,
      };
      if (importer.importNormals != ModelImporterNormals.None) mesh.normals = normals;
      if (importer.importTangents == ModelImporterTangents.Import && hasTangents) mesh.tangents = tangents;
      mesh.subMeshCount = Math.Max(1, source.SubMeshes.Length);
      for (var subMeshIndex = 0; subMeshIndex < source.SubMeshes.Length; subMeshIndex++)
      {
        var subMesh = source.SubMeshes[subMeshIndex];
        if (subMesh.indexStart < 0 || subMesh.indexCount < 0 || subMesh.indexStart + subMesh.indexCount > source.Indices.Length)
          throw new InvalidDataException("Native model submesh range is outside the index buffer.");
        var indices = new int[subMesh.indexCount];
        for (var index = 0; index < indices.Length; index++) indices[index] = checked((int)source.Indices[subMesh.indexStart + index]);
        mesh.SetTriangles(indices, subMeshIndex);
      }
      mesh.RecalculateBounds();
      if (source.Bones.Length != 0)
      {
        var bindposes = new Matrix4x4[source.Bones.Length];
        for (var boneIndex = 0; boneIndex < bindposes.Length; boneIndex++)
        {
          var values = source.Bones[boneIndex].Bindpose;
          if (values.Length != 16) throw new InvalidDataException("Native model bindpose must contain 16 values.");
          bindposes[boneIndex] = new Matrix4x4
          {
            m00 = values[0], m01 = values[1], m02 = values[2], m03 = values[3],
            m10 = values[4], m11 = values[5], m12 = values[6], m13 = values[7],
            m20 = values[8], m21 = values[9], m22 = values[10], m23 = values[11],
            m30 = values[12], m31 = values[13], m32 = values[14], m33 = values[15],
          };
        }
        mesh.bindposes = bindposes;
        var counts = new byte[source.SkinVertices.Length];
        for (var vertexIndex = 0; vertexIndex < counts.Length; vertexIndex++)
        {
          var skinVertex = source.SkinVertices[vertexIndex];
          if (skinVertex.weightCount < 0 || skinVertex.weightCount > 8 ||
              skinVertex.weightStart < 0 || skinVertex.weightStart + skinVertex.weightCount > source.BoneWeights.Length)
            throw new InvalidDataException("Native model skin influence range is invalid.");
          counts[vertexIndex] = checked((byte)skinVertex.weightCount);
        }
        var weights = new BoneWeight1[source.BoneWeights.Length];
        for (var weightIndex = 0; weightIndex < weights.Length; weightIndex++)
          weights[weightIndex] = new BoneWeight1 { boneIndex = source.BoneWeights[weightIndex].boneIndex, weight = source.BoneWeights[weightIndex].weight };
        using var nativeCounts = new NativeArray<byte>(counts, Allocator.Temp);
        using var nativeWeights = new NativeArray<BoneWeight1>(weights, Allocator.Temp);
        mesh.SetBoneWeights(nativeCounts, nativeWeights);
      }
      foreach (var shape in source.BlendShapes)
      {
        foreach (var frame in shape.Frames)
        {
          if (frame.Deltas.Length != vertices.Length) throw new InvalidDataException("Native model blend shape vertex count does not match the mesh.");
          var deltaVertices = new Vector3[vertices.Length];
          var deltaNormals = new Vector3[vertices.Length];
          var deltaTangents = new Vector3[vertices.Length];
          for (var vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
          {
            var delta = frame.Deltas[vertexIndex];
            deltaVertices[vertexIndex] = new Vector3(delta.positionX, delta.positionY, delta.positionZ);
            deltaNormals[vertexIndex] = new Vector3(delta.normalX, delta.normalY, delta.normalZ);
          }
          mesh.AddBlendShapeFrame(shape.Name, frame.Weight, deltaVertices, deltaNormals, deltaTangents);
        }
      }
      meshes[meshIndex] = mesh;
    }
    return meshes;
  }

  private static GameObject BuildHierarchy(NativeModelDecoder.Scene decoded, Mesh[] meshes,
    string assetName, ModelImporter importer, out GameObject[] nodeObjects)
  {
    nodeObjects = new GameObject[decoded.Nodes.Length];
    var topNodes = Enumerable.Range(0, decoded.Nodes.Length).Where(index => decoded.Nodes[index].ParentIndex < 0).ToArray();
    var adoptedNode = topNodes.Length == 1 ? topNodes[0] : -1;
    var root = new GameObject(assetName);
    root.transform.name = root.name;
    if (adoptedNode >= 0)
    {
      nodeObjects[adoptedNode] = root;
      ApplyTransform(root.transform, decoded.Nodes[adoptedNode]);
    }

    for (var nodeIndex = 0; nodeIndex < decoded.Nodes.Length; nodeIndex++)
    {
      if (nodeIndex == adoptedNode) continue;
      var name = string.IsNullOrEmpty(decoded.Nodes[nodeIndex].Name) ? $"Node{nodeIndex}" : decoded.Nodes[nodeIndex].Name;
      nodeObjects[nodeIndex] = new GameObject(name);
      nodeObjects[nodeIndex].transform.name = name;
    }

    var hierarchyNodes = nodeObjects;
    for (var nodeIndex = 0; nodeIndex < decoded.Nodes.Length; nodeIndex++)
    {
      if (nodeIndex == adoptedNode) continue;
      var source = decoded.Nodes[nodeIndex];
      var parent = source.ParentIndex >= 0 && source.ParentIndex < nodeObjects.Length
        ? nodeObjects[source.ParentIndex].transform : root.transform;
      nodeObjects[nodeIndex].transform.SetParent(parent, false);
      ApplyTransform(nodeObjects[nodeIndex].transform, source);
    }

    for (var nodeIndex = 0; nodeIndex < decoded.Nodes.Length; nodeIndex++)
    {
      var source = decoded.Nodes[nodeIndex];
      if (source.AttributeType == NativeModelDecoder.NodeAttributeType.Camera && importer.importCameras)
      {
        var camera = nodeObjects[nodeIndex].AddComponent<Camera>();
        camera.orthographic = source.CameraOrthographic;
        camera.fieldOfView = source.CameraFieldOfView;
        camera.nearClipPlane = source.CameraNearClip;
        camera.farClipPlane = source.CameraFarClip;
        camera.aspect = 4f / 3f;
      }
      else if (source.AttributeType == NativeModelDecoder.NodeAttributeType.Light && importer.importLights)
      {
        var light = nodeObjects[nodeIndex].AddComponent<Light>();
        light.type = (LightType)source.LightType;
        light.color = new Color(source.LightColorR, source.LightColorG, source.LightColorB, 1f);
        light.intensity = source.LightIntensity;
        light.range = source.LightRange;
        light.spotAngle = source.LightSpotAngle;
        light.shadows = source.LightCastShadows ? LightShadows.Hard : LightShadows.None;
      }
      if (source.MeshIndex < 0 || source.MeshIndex >= meshes.Length) continue;
      var decodedMesh = decoded.Meshes[source.MeshIndex];
      if (decodedMesh.Bones.Length != 0 || decodedMesh.BlendShapes.Length != 0)
      {
        var renderer = nodeObjects[nodeIndex].AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = meshes[source.MeshIndex];
        renderer.localBounds = renderer.sharedMesh.bounds;
        // Unity 2022's FBX importer does not apply the static Visibility
        // property to SkinnedMeshRenderer.enabled, although it still imports
        // animated visibility as Renderer.m_Enabled.
        renderer.enabled = true;
        if (decodedMesh.Bones.Length != 0)
        {
          renderer.bones = decodedMesh.Bones.Select(bone => bone.NodeIndex >= 0 && bone.NodeIndex < hierarchyNodes.Length
            ? hierarchyNodes[bone.NodeIndex].transform : null!).ToArray();
          renderer.rootBone = CommonBoneAncestor(renderer.bones);
        }
      }
      else
      {
        var filter = nodeObjects[nodeIndex].AddComponent<MeshFilter>();
        filter.sharedMesh = meshes[source.MeshIndex];
        var renderer = nodeObjects[nodeIndex].AddComponent<MeshRenderer>();
        renderer.enabled = !importer.importVisibility || source.Visible;
      }

    }
    return root;
  }

  private static Transform? CommonBoneAncestor(Transform[] bones)
  {
    var valid = bones.Where(bone => bone is not null).ToArray();
    if (valid.Length == 0) return null;
    for (var candidate = valid[0]; candidate is not null; candidate = candidate.parent)
      if (valid.All(bone => ReferenceEquals(bone, candidate) || bone.IsChildOf(candidate))) return candidate;
    return null;
  }

  private static void ApplyTransform(Transform transform, NativeModelDecoder.Node source)
  {
    transform.localPosition = new Vector3(source.PositionX, source.PositionY, source.PositionZ);
    transform.localRotation = new Quaternion(source.RotationX, source.RotationY, source.RotationZ, source.RotationW);
    transform.localScale = new Vector3(source.ScaleX, source.ScaleY, source.ScaleZ);
  }

  private static AnimationClip[] BuildAnimationClips(
    NativeModelDecoder.Scene decoded, ModelImporter importer, GameObject root, GameObject[] nodes, Avatar? avatar)
  {
    if (!importer.importAnimation || !importer.importAnimations || importer.animationType == ModelImporterAnimationType.None)
      return Array.Empty<AnimationClip>();

    var clips = new List<AnimationClip>(decoded.Clips.Length);
    foreach (var source in decoded.Clips)
    {
      var matchingSettings = (importer.clipAnimations ?? Array.Empty<ModelImporterClipAnimation>()).Where(setting =>
        string.Equals(setting.takeName, source.Name, StringComparison.Ordinal) || string.Equals(setting.name, source.Name, StringComparison.Ordinal)).ToArray();
      if (matchingSettings.Length == 0) BuildClip(source, null);
      else foreach (var clipSettings in matchingSettings) BuildClip(source, clipSettings);
    }
    return clips.ToArray();

    void BuildClip(NativeModelDecoder.Clip source, ModelImporterClipAnimation? clipSettings)
    {
      var frameRate = source.FrameRate > 0f ? source.FrameRate : decoded.FrameRate;
      var rangeStart = clipSettings is not null
        ? (clipSettings.firstFrame - source.FirstFrame) / frameRate : 0f;
      var rangeEnd = clipSettings is not null && clipSettings.lastFrame >= 0f
        ? (clipSettings.lastFrame - source.FirstFrame) / frameRate : source.Duration;
      rangeStart = Math.Clamp(rangeStart, 0f, source.Duration);
      rangeEnd = Math.Clamp(rangeEnd, rangeStart, source.Duration);
      var clip = new AnimationClip
      {
        name = clipSettings is null || string.IsNullOrEmpty(clipSettings.name) ? source.Name : clipSettings.name,
        frameRate = frameRate,
        legacy = importer.animationType == ModelImporterAnimationType.Legacy,
        wrapMode = clipSettings?.wrapMode ?? WrapMode.Default,
        hideFlags = HideFlags.NotEditable,
      };
      clip.SetImportedLength(rangeEnd - rangeStart);
      var humanRootNodeIndex = importer.animationType == ModelImporterAnimationType.Human
        ? FindHumanRootNodeIndex(decoded, avatar?.humanDescription ?? importer.humanDescription)
        : -1;
      NativeModelDecoder.Track? motionTrack = null;
      NativeModelDecoder.Track? humanRootTrack = null;
      foreach (var track in source.Tracks)
      {
        if (track.NodeIndex < 0 || track.NodeIndex >= nodes.Length) continue;
        var path = RelativePath(root.transform, nodes[track.NodeIndex].transform);
        var isHumanRoot = track.NodeIndex == humanRootNodeIndex;
        if (isHumanRoot) humanRootTrack ??= track;
        if (!clip.legacy && !string.IsNullOrEmpty(importer.motionNodeName) &&
            (string.Equals(decoded.Nodes[track.NodeIndex].Name, importer.motionNodeName, StringComparison.Ordinal) ||
             string.Equals(path, importer.motionNodeName, StringComparison.Ordinal)))
          motionTrack ??= track;
        var hasRawPosition = track.TransformCurves.Any(curve => curve.Property <= NativeModelDecoder.TransformCurveProperty.PositionZ);
        var hasRawEuler = track.TransformCurves.Any(curve => curve.Property is >= NativeModelDecoder.TransformCurveProperty.EulerX and <= NativeModelDecoder.TransformCurveProperty.EulerZ);
        var hasRawScale = track.TransformCurves.Any(curve => curve.Property >= NativeModelDecoder.TransformCurveProperty.ScaleX);
        foreach (var curve in track.TransformCurves)
        {
          if (isHumanRoot && curve.Property <= NativeModelDecoder.TransformCurveProperty.EulerZ) continue;
          clip.SetCurve(path, typeof(Transform), TransformCurvePropertyName(curve.Property),
            ScalarCurveForRange(curve.Keys, rangeStart, rangeEnd,
              ModelImporterAnimationCompression.Off, 0f));
        }
        if (!hasRawPosition && !isHumanRoot)
          AddVectorCurves(clip, path, "m_LocalPosition", track.PositionKeys, rangeStart, rangeEnd,
            importer.animationCompression, importer.animationPositionError);
        if (isHumanRoot)
          AddHumanoidResidualRotationCurves(clip, path, track.RotationKeys, rangeStart, rangeEnd,
            importer.animationCompression, importer.animationRotationError,
            frameRate, source.FirstFrame / frameRate);
        else if (!hasRawEuler)
          AddQuaternionCurves(clip, path, track.RotationKeys, rangeStart, rangeEnd,
            importer.animationCompression, importer.animationRotationError,
            frameRate, source.FirstFrame / frameRate);
        if (!hasRawScale)
          AddVectorCurves(clip, path, "m_LocalScale", track.ScaleKeys, rangeStart, rangeEnd,
            importer.animationCompression, importer.animationScaleError);
      }
      var hasRoot = importer.animationType == ModelImporterAnimationType.Human && humanRootTrack is not null &&
        AddHumanoidRootCurves(clip, humanRootTrack, decoded.Nodes[humanRootNodeIndex], rangeStart, rangeEnd,
          importer, frameRate, source.FirstFrame / frameRate, avatar?.HumanScale ?? 1f);
      var hasMotion = importer.animationType != ModelImporterAnimationType.Human && motionTrack is not null &&
        AddRootMotionCurves(clip, motionTrack, rangeStart, rangeEnd, importer, frameRate,
          source.FirstFrame / frameRate);
      if (importer.importVisibility)
        foreach (var track in source.VisibilityTracks)
        {
          if (track.NodeIndex < 0 || track.NodeIndex >= nodes.Length) continue;
          var path = RelativePath(root.transform, nodes[track.NodeIndex].transform);
          clip.SetCurve(path, typeof(Renderer), "m_Enabled",
            ScalarCurveForRange(track.Keys, rangeStart, rangeEnd,
              importer.animationCompression, importer.animationPositionError));
        }
      if (importer.importBlendShapes && importer.importBlendShapeDeformPercent)
        foreach (var track in source.BlendShapeTracks)
        {
          if (track.NodeIndex < 0 || track.NodeIndex >= nodes.Length || string.IsNullOrEmpty(track.Name)) continue;
          var path = RelativePath(root.transform, nodes[track.NodeIndex].transform);
          clip.SetCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + track.Name,
            ScalarCurveForRange(track.Keys, rangeStart, rangeEnd,
              importer.animationCompression, importer.animationPositionError));
        }
      clip.EnsureQuaternionContinuity();
      clip.SetImportedLength(rangeEnd - rangeStart);
      clip.MarkMecanimDataBuilt();
      clip.SetImportedMotionMetadata(
        importer.animationType == ModelImporterAnimationType.Human,
        hasGenericRoot: false,
        hasMotion: hasMotion,
        hasMotionFloat: hasMotion,
        hasRoot: hasRoot);
      AnimationUtility.SetAnimationClipSettings(clip, new AnimationClipSettings
      {
        loopTime = clipSettings?.loopTime ?? false,
        loopBlend = clipSettings?.loopPose ?? false,
        loopBlendOrientation = clipSettings?.lockRootRotation ?? false,
        loopBlendPositionY = clipSettings?.lockRootHeightY ?? false,
        loopBlendPositionXZ = clipSettings?.lockRootPositionXZ ?? false,
        keepOriginalOrientation = clipSettings?.keepOriginalOrientation ?? false,
        keepOriginalPositionY = clipSettings?.keepOriginalPositionY ?? false,
        keepOriginalPositionXZ = clipSettings?.keepOriginalPositionXZ ?? false,
        heightFromFeet = clipSettings?.heightFromFeet ?? false,
        mirror = clipSettings?.mirror ?? false,
        cycleOffset = clipSettings?.cycleOffset ?? 0f,
        startTime = 0f,
        stopTime = clip.length,
      });
      clips.Add(clip);
    }
  }

  private static int FindHumanRootNodeIndex(NativeModelDecoder.Scene decoded, HumanDescription description)
  {
    var hips = (description.human ?? Array.Empty<HumanBone>()).FirstOrDefault(mapping =>
      string.Equals(mapping.humanName, "Hips", StringComparison.Ordinal));
    if (string.IsNullOrEmpty(hips.boneName)) return -1;
    for (var index = 0; index < decoded.Nodes.Length; index++)
      if (string.Equals(decoded.Nodes[index].Name, hips.boneName, StringComparison.Ordinal)) return index;
    return -1;
  }

  private static string RelativePath(Transform root, Transform target)
  {
    if (ReferenceEquals(root, target)) return string.Empty;
    var names = new Stack<string>();
    for (var current = target; current is not null && !ReferenceEquals(current, root); current = current.parent) names.Push(current.name);
    return string.Join("/", names);
  }

  private static void AddVectorCurves(AnimationClip clip, string path, string property,
    NativeModelDecoder.VectorKey[] keys, float start, float end,
    ModelImporterAnimationCompression compression, float error)
  {
    if (keys.Length == 0) return;
    clip.SetCurve(path, typeof(Transform), property + ".x", CurveForRange(keys.Select(key => (key.time, key.x)).ToArray(), start, end, compression, error));
    clip.SetCurve(path, typeof(Transform), property + ".y", CurveForRange(keys.Select(key => (key.time, key.y)).ToArray(), start, end, compression, error));
    clip.SetCurve(path, typeof(Transform), property + ".z", CurveForRange(keys.Select(key => (key.time, key.z)).ToArray(), start, end, compression, error));
  }

  private static bool AddHumanoidRootCurves(
    AnimationClip clip,
    NativeModelDecoder.Track track,
    NativeModelDecoder.Node restNode,
    float start,
    float end,
    ModelImporter importer,
    float sampleRate,
    float reductionTimeOffset,
    float humanScale)
  {
    var added = false;
    var scale = float.IsFinite(humanScale) && humanScale > 0f ? humanScale : 1f;
    if (track.PositionKeys.Length > 0)
    {
      var reference = track.PositionKeys[0];
      var positions = track.PositionKeys.Select(key => new NativeModelDecoder.VectorKey
      {
        time = key.time,
        x = restNode.PositionX + (key.x - reference.x) / scale,
        y = restNode.PositionY + (key.y - reference.y) / scale,
        z = restNode.PositionZ + (key.z - reference.z) / scale,
      }).ToArray();
      clip.SetCurve(string.Empty, typeof(Animator), "RootT.x",
        CurveForRange(positions.Select(key => (key.time, key.x)).ToArray(),
          start, end, importer.animationCompression, importer.animationPositionError));
      clip.SetCurve(string.Empty, typeof(Animator), "RootT.y",
        CurveForRange(positions.Select(key => (key.time, key.y)).ToArray(),
          start, end, importer.animationCompression, importer.animationPositionError));
      clip.SetCurve(string.Empty, typeof(Animator), "RootT.z",
        CurveForRange(positions.Select(key => (key.time, key.z)).ToArray(),
          start, end, importer.animationCompression, importer.animationPositionError));
      added = true;
    }
    if (track.RotationKeys.Length > 0)
    {
      var curves = CompressQuaternionCurves(new[]
      {
        Curve(track.RotationKeys.Select(key => (key.time, key.x)).ToArray()),
        Curve(track.RotationKeys.Select(key => (key.time, key.y)).ToArray()),
        Curve(track.RotationKeys.Select(key => (key.time, key.z)).ToArray()),
        Curve(track.RotationKeys.Select(key => (key.time, key.w)).ToArray()),
      }, importer.animationCompression, importer.animationRotationError,
        sampleRate, reductionTimeOffset);
      clip.SetCurve(string.Empty, typeof(Animator), "RootQ.x", SliceCurve(curves[0], start, end));
      clip.SetCurve(string.Empty, typeof(Animator), "RootQ.y", SliceCurve(curves[1], start, end));
      clip.SetCurve(string.Empty, typeof(Animator), "RootQ.z", SliceCurve(curves[2], start, end));
      clip.SetCurve(string.Empty, typeof(Animator), "RootQ.w", SliceCurve(curves[3], start, end));
      added = true;
    }
    return added;
  }

  private static void AddHumanoidResidualRotationCurves(
    AnimationClip clip,
    string path,
    NativeModelDecoder.QuaternionKey[] keys,
    float start,
    float end,
    ModelImporterAnimationCompression compression,
    float error,
    float sampleRate,
    float reductionTimeOffset)
  {
    if (keys.Length == 0) return;
    var residual = keys.Select(key =>
    {
      var source = new Quaternion(key.x, key.y, key.z, key.w).normalized;
      var twistLength = MathF.Sqrt(source.y * source.y + source.w * source.w);
      var twist = twistLength > 1e-6f
        ? new Quaternion(0f, source.y / twistLength, 0f, source.w / twistLength)
        : Quaternion.identity;
      var swing = (Quaternion.Inverse(twist) * source).normalized;
      return new NativeModelDecoder.QuaternionKey
      {
        time = key.time,
        x = swing.x,
        y = swing.y,
        z = swing.z,
        w = swing.w,
      };
    }).ToArray();
    AddQuaternionCurves(clip, path, residual, start, end, compression, error, sampleRate, reductionTimeOffset);
  }

  private static bool AddRootMotionCurves(
    AnimationClip clip,
    NativeModelDecoder.Track track,
    float start,
    float end,
    ModelImporter importer,
    float sampleRate,
    float reductionTimeOffset)
  {
    var added = false;
    if (track.PositionKeys.Length > 0)
    {
      clip.SetCurve(string.Empty, typeof(Animator), "MotionT.x",
        CurveForRange(track.PositionKeys.Select(key => (key.time, key.x)).ToArray(),
          start, end, importer.animationCompression, importer.animationPositionError));
      clip.SetCurve(string.Empty, typeof(Animator), "MotionT.y",
        CurveForRange(track.PositionKeys.Select(key => (key.time, key.y)).ToArray(),
          start, end, importer.animationCompression, importer.animationPositionError));
      clip.SetCurve(string.Empty, typeof(Animator), "MotionT.z",
        CurveForRange(track.PositionKeys.Select(key => (key.time, key.z)).ToArray(),
          start, end, importer.animationCompression, importer.animationPositionError));
      added = true;
    }
    if (track.RotationKeys.Length > 0)
    {
      var curves = CompressQuaternionCurves(new[]
      {
        Curve(track.RotationKeys.Select(key => (key.time, key.x)).ToArray()),
        Curve(track.RotationKeys.Select(key => (key.time, key.y)).ToArray()),
        Curve(track.RotationKeys.Select(key => (key.time, key.z)).ToArray()),
        Curve(track.RotationKeys.Select(key => (key.time, key.w)).ToArray()),
      }, importer.animationCompression, importer.animationRotationError,
        sampleRate, reductionTimeOffset);
      clip.SetCurve(string.Empty, typeof(Animator), "MotionQ.x", SliceCurve(curves[0], start, end));
      clip.SetCurve(string.Empty, typeof(Animator), "MotionQ.y", SliceCurve(curves[1], start, end));
      clip.SetCurve(string.Empty, typeof(Animator), "MotionQ.z", SliceCurve(curves[2], start, end));
      clip.SetCurve(string.Empty, typeof(Animator), "MotionQ.w", SliceCurve(curves[3], start, end));
      added = true;
    }
    return added;
  }

  private static string TransformCurvePropertyName(NativeModelDecoder.TransformCurveProperty property) => property switch
  {
    NativeModelDecoder.TransformCurveProperty.PositionX => "m_LocalPosition.x",
    NativeModelDecoder.TransformCurveProperty.PositionY => "m_LocalPosition.y",
    NativeModelDecoder.TransformCurveProperty.PositionZ => "m_LocalPosition.z",
    NativeModelDecoder.TransformCurveProperty.EulerX => "localEulerAnglesRaw.x",
    NativeModelDecoder.TransformCurveProperty.EulerY => "localEulerAnglesRaw.y",
    NativeModelDecoder.TransformCurveProperty.EulerZ => "localEulerAnglesRaw.z",
    NativeModelDecoder.TransformCurveProperty.ScaleX => "m_LocalScale.x",
    NativeModelDecoder.TransformCurveProperty.ScaleY => "m_LocalScale.y",
    NativeModelDecoder.TransformCurveProperty.ScaleZ => "m_LocalScale.z",
    _ => throw new ArgumentOutOfRangeException(nameof(property)),
  };

  private static void AddQuaternionCurves(AnimationClip clip, string path,
    NativeModelDecoder.QuaternionKey[] keys, float start, float end,
    ModelImporterAnimationCompression compression, float error,
    float sampleRate, float reductionTimeOffset)
  {
    if (keys.Length == 0) return;
    var curves = CompressQuaternionCurves(new[]
    {
      Curve(keys.Select(key => (key.time, key.x)).ToArray()),
      Curve(keys.Select(key => (key.time, key.y)).ToArray()),
      Curve(keys.Select(key => (key.time, key.z)).ToArray()),
      Curve(keys.Select(key => (key.time, key.w)).ToArray()),
    }, compression, error, sampleRate, reductionTimeOffset);
    clip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", SliceCurve(curves[0], start, end));
    clip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", SliceCurve(curves[1], start, end));
    clip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", SliceCurve(curves[2], start, end));
    clip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", SliceCurve(curves[3], start, end));
  }

  private static AnimationCurve[] CompressQuaternionCurves(AnimationCurve[] source,
    ModelImporterAnimationCompression compression, float rotationErrorDegrees,
    float sampleRate, float timeOffset)
  {
    var sourceKeys = source.Select(curve => curve.keys).ToArray();
    var count = sourceKeys[0].Length;
    if (compression == ModelImporterAnimationCompression.Off || count <= 2 ||
        float.IsNaN(rotationErrorDegrees) || rotationErrorDegrees <= 0f ||
        !float.IsFinite(sampleRate) || sampleRate <= 0f) return source;

    var keys = new Keyframe[4][];
    for (var component = 0; component < keys.Length; component++)
    {
      if (sourceKeys[component].Length != count) return source;
      keys[component] = new Keyframe[count];
      for (var index = 0; index < count; index++)
      {
        var key = sourceKeys[component][index];
        key.time += timeOffset;
        keys[component][index] = key;
      }
    }

    var radians = rotationErrorDegrees / 360f;
    radians += radians;
    radians *= MathF.PI;
    radians *= .5f;
    var minimumDot = MathF.Cos(radians);
    var step = 1f / sampleRate;
    var last = count - 1;
    var retained = new List<int> { 0 };
    if (CanReduceQuaternion(keys, 0, last, minimumDot, step, 0, last, false))
      retained.Add(last);
    else
    {
      var anchor = 0;
      var current = 1;
      while (current < last)
      {
        var following = current + 1;
        if (HasSteppedQuaternionEdge(keys, current, following))
        {
          if (QuaternionWithinError(KeyQuaternion(keys, current), KeyQuaternion(keys, following), minimumDot))
          {
            current = following;
            continue;
          }
          retained.Add(current);
          retained.Add(following);
          anchor = following;
          current = following + 1;
          continue;
        }

        var canReduce = CanReduceQuaternion(keys, anchor, following, minimumDot, step,
            anchor + 1, following, true);
        if (canReduce)
        {
          current = following;
          continue;
        }
        retained.Add(current);
        anchor = current;
        current = following;
      }
      if (anchor != last && retained[^1] != last) retained.Add(last);
    }

    if (retained.Count == count) return source;
    var result = new AnimationCurve[4];
    for (var component = 0; component < result.Length; component++)
      result[component] = new AnimationCurve(retained.Select(index => sourceKeys[component][index]).ToArray());
    return result;
  }

  private static bool CanReduceQuaternion(Keyframe[][] keys, int left, int right,
    float minimumDot, float sampleStep, int begin, int end, bool limitSpan)
  {
    var leftTime = keys[0][left].time;
    var rightTime = keys[0][right].time;
    var duration = rightTime - leftTime;
    var sampleCount = (int)(duration / sampleStep);
    for (var sample = 0; sample <= sampleCount; sample++)
    {
      var time = leftTime + sample * sampleStep;
      if (!QuaternionWithinError(EvaluateSourceQuaternion(keys, time),
          EvaluateReducedQuaternion(keys, left, right, time), minimumDot)) return false;
    }

    var previousTime = leftTime;
    for (var index = begin; index < end; index++)
    {
      var time = keys[0][index].time;
      if (!QuaternionWithinError(EvaluateSourceQuaternion(keys, time),
          EvaluateReducedQuaternion(keys, left, right, time), minimumDot)) return false;
      var midpoint = (previousTime + time) * .5f;
      if (!QuaternionWithinError(EvaluateSourceQuaternion(keys, midpoint),
          EvaluateReducedQuaternion(keys, left, right, midpoint), minimumDot)) return false;
      previousTime = time;
    }

    var finalMidpoint = (previousTime + rightTime) * .5f;
    if (!QuaternionWithinError(EvaluateSourceQuaternion(keys, finalMidpoint),
        EvaluateReducedQuaternion(keys, left, right, finalMidpoint), minimumDot)) return false;
    return !limitSpan || duration < sampleStep * 50f;
  }

  private static bool HasSteppedQuaternionEdge(Keyframe[][] keys, int left, int right)
  {
    for (var component = 0; component < keys.Length; component++)
      if (float.IsPositiveInfinity(keys[component][left].outTangent) ||
          float.IsPositiveInfinity(keys[component][right].inTangent)) return true;
    return false;
  }

  private static (float X, float Y, float Z, float W) KeyQuaternion(Keyframe[][] keys, int index) =>
    (keys[0][index].value, keys[1][index].value, keys[2][index].value, keys[3][index].value);

  private static (float X, float Y, float Z, float W) EvaluateSourceQuaternion(Keyframe[][] keys, float time) =>
    (EvaluateCachedHermite(keys[0], time), EvaluateCachedHermite(keys[1], time),
      EvaluateCachedHermite(keys[2], time), EvaluateCachedHermite(keys[3], time));

  private static (float X, float Y, float Z, float W) EvaluateReducedQuaternion(
    Keyframe[][] keys, int left, int right, float time) =>
    (EvaluateReductionHermite(keys[0][left], keys[0][right], time),
      EvaluateReductionHermite(keys[1][left], keys[1][right], time),
      EvaluateReductionHermite(keys[2][left], keys[2][right], time),
      EvaluateReductionHermite(keys[3][left], keys[3][right], time));

  private static bool QuaternionWithinError(
    (float X, float Y, float Z, float W) original,
    (float X, float Y, float Z, float W) reduced, float minimumDot)
  {
    var reducedLengthSquared = reduced.X * reduced.X + reduced.Y * reduced.Y;
    reducedLengthSquared += reduced.Z * reduced.Z;
    reducedLengthSquared += reduced.W * reduced.W;
    var reducedLength = MathF.Sqrt(reducedLengthSquared);
    if (!(MathF.Abs(1f - reducedLength) <= .001f)) return false;

    var originalLengthSquared = original.X * original.X + original.Y * original.Y;
    originalLengthSquared += original.Z * original.Z;
    originalLengthSquared += original.W * original.W;
    var originalLength = MathF.Sqrt(originalLengthSquared);
    if (!(originalLength >= .000001f) || !(reducedLength >= .000001f)) return false;

    var dot = original.X / originalLength * (reduced.X / reducedLength);
    dot += original.Y / originalLength * (reduced.Y / reducedLength);
    dot += original.Z / originalLength * (reduced.Z / reducedLength);
    dot += original.W / originalLength * (reduced.W / reducedLength);
    return dot >= minimumDot;
  }

  private static float EvaluateCachedHermite(Keyframe[] keys, float time)
  {
    if (time <= keys[0].time) return keys[0].value;
    if (time >= keys[^1].time) return keys[^1].value;
    var segment = keys.Length - 2;
    for (var index = 0; index + 1 < keys.Length; index++)
      if (time < keys[index + 1].time)
      {
        segment = index;
        break;
      }

    var left = keys[segment];
    var right = keys[segment + 1];
    var duration = MathF.Max(right.time - left.time, .0001f);
    var delta = right.value - left.value;
    var inverse = 1f / duration;
    var inverseSquared = inverse * inverse;
    var leftDuration = left.outTangent * duration;
    var rightDuration = right.inTangent * duration;
    var cubic = leftDuration + rightDuration - delta - delta;
    cubic *= inverseSquared;
    cubic *= inverse;
    var quadratic = delta + delta + delta - leftDuration - leftDuration - rightDuration;
    quadratic *= inverseSquared;
    var offset = time - left.time;
    var value = cubic * offset + quadratic;
    value = value * offset + left.outTangent;
    return value * offset + left.value;
  }

  private static float EvaluateReductionHermite(Keyframe left, Keyframe right, float time)
  {
    var duration = right.time - left.time;
    if (duration == 0f) return left.value;
    var leftDuration = left.outTangent * duration;
    var rightDuration = right.inTangent * duration;
    var t = (time - left.time) / duration;
    var squared = t * t;
    var cubed = t * squared;
    var leftValueWeight = cubed + cubed - 3f * squared + 1f;
    var leftTangentWeight = cubed - (squared + squared) + t;
    var rightTangentWeight = cubed - squared;
    var rightValueWeight = 3f * squared - (cubed + cubed);
    var value = left.value * leftValueWeight + leftDuration * leftTangentWeight;
    value += rightDuration * rightTangentWeight;
    return value + right.value * rightValueWeight;
  }

  private static AnimationCurve ScalarCurveForRange(NativeModelDecoder.ScalarKey[] values, float start, float end,
    ModelImporterAnimationCompression compression, float error)
  {
    var keys = values.Select(value => new Keyframe(
      value.time, value.value, value.inTangent, value.outTangent)).ToArray();
    return SliceCurve(CompressCurve(new AnimationCurve(keys), compression, error), start, end);
  }

  private static AnimationCurve CurveForRange((float Time, float Value)[] values, float start, float end,
    ModelImporterAnimationCompression compression, float error)
  {
    if (values.Length == 0) return new AnimationCurve();
    return SliceCurve(CompressCurve(Curve(values), compression, error), start, end);
  }

  private static AnimationCurve SliceCurve(AnimationCurve source, float start, float end)
  {
    var sourceKeys = source.keys;
    if (sourceKeys.Length == 0) return source;
    if (start <= sourceKeys[0].time && end >= sourceKeys[^1].time) return source;
    var startTangent = EvaluateDerivative(sourceKeys, start);
    var sliced = new List<Keyframe> { new(0f, source.Evaluate(start), startTangent, startTangent) };
    foreach (var sourceKey in sourceKeys)
      if (sourceKey.time > start && sourceKey.time < end)
      {
        var key = sourceKey;
        key.time -= start;
        sliced.Add(key);
      }
    var duration = end - start;
    if (duration > 0f)
    {
      var endTangent = EvaluateDerivative(sourceKeys, end);
      sliced.Add(new Keyframe(duration, source.Evaluate(end), endTangent, endTangent));
    }
    return new AnimationCurve(sliced.ToArray());
  }

  private static AnimationCurve Curve((float Time, float Value)[] values)
  {
    var keys = new Keyframe[values.Length];
    for (var index = 0; index < values.Length; index++)
    {
      var tangent = Slope(values, index == 0 ? index : index - 1,
        index + 1 < values.Length ? index + 1 : index);
      keys[index] = new Keyframe(values[index].Time, values[index].Value, tangent, tangent);
    }
    return new AnimationCurve(keys);
  }

  private static AnimationCurve CompressCurve(AnimationCurve source,
    ModelImporterAnimationCompression compression, float percentageError)
  {
    var keys = source.keys;
    if (compression == ModelImporterAnimationCompression.Off || keys.Length <= 2 ||
        float.IsNaN(percentageError) || percentageError <= 0f ||
        keys.Any(key => !float.IsFinite(key.inTangent) || !float.IsFinite(key.outTangent)))
      return source;
    var allowedRelativeError = float.IsPositiveInfinity(percentageError)
      ? float.PositiveInfinity : percentageError / 100f;
    var kept = new List<Keyframe> { keys[0] };
    var begin = 0;
    while (begin + 1 < keys.Length)
    {
      var best = begin + 1;
      for (var candidate = begin + 2; candidate < keys.Length; candidate++)
      {
        if (MaximumRelativeError(keys, begin, candidate) <= allowedRelativeError) best = candidate;
        else break;
      }
      kept.Add(keys[best]);
      begin = best;
    }
    return new AnimationCurve(kept.ToArray());
  }

  private static float MaximumRelativeError(Keyframe[] keys, int begin, int end)
  {
    var maximum = 0f;
    for (var segment = begin; segment < end; segment++)
    {
      // Unity 2022's importer checks the midpoint and right endpoint of every
      // source-key interval when deciding whether a reduced Hermite edge fits.
      for (var step = 1; step <= 2; step++)
      {
        var time = keys[segment].time +
          (keys[segment + 1].time - keys[segment].time) * (step / 2f);
        var original = EvaluateHermite(keys[segment], keys[segment + 1], time);
        var reduced = EvaluateHermite(keys[begin], keys[end], time);
        var relative = MathF.Abs(original - reduced) / MathF.Max(MathF.Abs(original), 0.000001f);
        if (relative > maximum) maximum = relative;
      }
    }
    return maximum;
  }

  private static float EvaluateDerivative(Keyframe[] keys, float time)
  {
    if (keys.Length == 0) return 0f;
    if (keys.Length == 1) return keys[0].outTangent;
    if (time <= keys[0].time) return keys[0].outTangent;
    for (var index = 1; index < keys.Length; index++)
      if (time <= keys[index].time) return EvaluateHermiteDerivative(keys[index - 1], keys[index], time);
    return keys[^1].inTangent;
  }

  private static float EvaluateHermite(Keyframe left, Keyframe right, float time)
  {
    var duration = right.time - left.time;
    if (MathF.Abs(duration) < 1e-8f) return left.value;
    if (!float.IsFinite(left.outTangent) || !float.IsFinite(right.inTangent))
      return time < right.time ? left.value : right.value;
    var t = Math.Clamp((time - left.time) / duration, 0f, 1f);
    var t2 = t * t;
    var t3 = t2 * t;
    return (2f * t3 - 3f * t2 + 1f) * left.value +
      (t3 - 2f * t2 + t) * duration * left.outTangent +
      (-2f * t3 + 3f * t2) * right.value +
      (t3 - t2) * duration * right.inTangent;
  }

  private static float EvaluateHermiteDerivative(Keyframe left, Keyframe right, float time)
  {
    var duration = right.time - left.time;
    if (MathF.Abs(duration) < 1e-8f) return 0f;
    if (!float.IsFinite(left.outTangent) || !float.IsFinite(right.inTangent)) return 0f;
    var t = Math.Clamp((time - left.time) / duration, 0f, 1f);
    var t2 = t * t;
    return ((6f * t2 - 6f * t) * left.value +
      (3f * t2 - 4f * t + 1f) * duration * left.outTangent +
      (-6f * t2 + 6f * t) * right.value +
      (3f * t2 - 2f * t) * duration * right.inTangent) / duration;
  }

  private static float Slope((float Time, float Value)[] values, int from, int to)
  {
    if (from < 0 || to < 0 || from >= values.Length || to >= values.Length) return 0f;
    var delta = values[to].Time - values[from].Time;
    return MathF.Abs(delta) < 1e-8f ? 0f : (values[to].Value - values[from].Value) / delta;
  }
}
