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
    };
    if (!NativeModelDecoder.TryLoad(fullPath, options, out var decoded, out error)) return false;

    try
    {
      var meshes = BuildMeshes(decoded, importer);
      var root = BuildHierarchy(decoded, meshes, Path.GetFileNameWithoutExtension(assetPath), out var nodes);
      var defaultClips = decoded.Clips.Select(clip => new ModelImporterClipAnimation
      {
        name = clip.Name,
        takeName = clip.Name,
        firstFrame = 0f,
        lastFrame = MathF.Round(clip.Duration * clip.FrameRate),
      }).ToArray();
      importer.SetImportedModelMetadata(decoded.FileScale, defaultClips);

      root.hideFlags = HideFlags.NotEditable;
      var subAssets = new List<UnityEngine.Object>(meshes.Length + decoded.Clips.Length);
      subAssets.AddRange(meshes);
      var avatar = BuildAvatar(root, assetPath, importer);
      if (avatar is not null) subAssets.Add(avatar);
      imported = new ImportedModelAsset
      {
        MainObject = root,
        SubAssets = subAssets,
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
      avatar = Avatar.Create(source.isValid, source.isHuman, source.humanDescription, source.ValidationFlags);
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
      imported.DecodedScene, importer, imported.MainObject, imported.NodeObjects);
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

  private static GameObject BuildHierarchy(NativeModelDecoder.Scene decoded, Mesh[] meshes, string assetName, out GameObject[] nodeObjects)
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
      if (source.MeshIndex < 0 || source.MeshIndex >= meshes.Length) continue;
      var decodedMesh = decoded.Meshes[source.MeshIndex];
      if (decodedMesh.Bones.Length != 0 || decodedMesh.BlendShapes.Length != 0)
      {
        var renderer = nodeObjects[nodeIndex].AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = meshes[source.MeshIndex];
        renderer.localBounds = renderer.sharedMesh.bounds;
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
        nodeObjects[nodeIndex].AddComponent<MeshRenderer>();
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
    NativeModelDecoder.Scene decoded, ModelImporter importer, GameObject root, GameObject[] nodes)
  {
    if (!importer.importAnimation || !importer.importAnimations || importer.animationType == ModelImporterAnimationType.None)
      return Array.Empty<AnimationClip>();

    var clips = new List<AnimationClip>(decoded.Clips.Length);
    foreach (var source in decoded.Clips)
    {
      var clipSettings = (importer.clipAnimations ?? Array.Empty<ModelImporterClipAnimation>()).FirstOrDefault(setting =>
        string.Equals(setting.takeName, source.Name, StringComparison.Ordinal) || string.Equals(setting.name, source.Name, StringComparison.Ordinal));
      var clip = new AnimationClip
      {
        name = clipSettings is null || string.IsNullOrEmpty(clipSettings.name) ? source.Name : clipSettings.name,
        frameRate = source.FrameRate > 0f ? source.FrameRate : decoded.FrameRate,
        length = source.Duration,
        legacy = importer.animationType == ModelImporterAnimationType.Legacy,
        wrapMode = clipSettings?.wrapMode ?? WrapMode.Default,
        hideFlags = HideFlags.NotEditable,
      };
      foreach (var track in source.Tracks)
      {
        if (track.NodeIndex < 0 || track.NodeIndex >= nodes.Length) continue;
        var path = RelativePath(root.transform, nodes[track.NodeIndex].transform);
        AddVectorCurves(clip, path, "m_LocalPosition", track.PositionKeys);
        AddQuaternionCurves(clip, path, track.RotationKeys);
        AddVectorCurves(clip, path, "m_LocalScale", track.ScaleKeys);
      }
      clip.EnsureQuaternionContinuity();
      clips.Add(clip);
    }
    return clips.ToArray();
  }

  private static string RelativePath(Transform root, Transform target)
  {
    if (ReferenceEquals(root, target)) return string.Empty;
    var names = new Stack<string>();
    for (var current = target; current is not null && !ReferenceEquals(current, root); current = current.parent) names.Push(current.name);
    return string.Join("/", names);
  }

  private static void AddVectorCurves(AnimationClip clip, string path, string property, NativeModelDecoder.VectorKey[] keys)
  {
    if (keys.Length == 0) return;
    clip.SetCurve(path, typeof(Transform), property + ".x", Curve(keys.Select(key => (key.time, key.x)).ToArray()));
    clip.SetCurve(path, typeof(Transform), property + ".y", Curve(keys.Select(key => (key.time, key.y)).ToArray()));
    clip.SetCurve(path, typeof(Transform), property + ".z", Curve(keys.Select(key => (key.time, key.z)).ToArray()));
  }

  private static void AddQuaternionCurves(AnimationClip clip, string path, NativeModelDecoder.QuaternionKey[] keys)
  {
    if (keys.Length == 0) return;
    clip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", Curve(keys.Select(key => (key.time, key.x)).ToArray()));
    clip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", Curve(keys.Select(key => (key.time, key.y)).ToArray()));
    clip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", Curve(keys.Select(key => (key.time, key.z)).ToArray()));
    clip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", Curve(keys.Select(key => (key.time, key.w)).ToArray()));
  }

  private static AnimationCurve Curve((float Time, float Value)[] values)
  {
    var keys = new Keyframe[values.Length];
    for (var index = 0; index < values.Length; index++)
    {
      var inSlope = index == 0 ? Slope(values, index, index + 1) : Slope(values, index - 1, index);
      var outSlope = index + 1 >= values.Length ? inSlope : Slope(values, index, index + 1);
      keys[index] = new Keyframe(values[index].Time, values[index].Value, inSlope, outSlope);
    }
    return new AnimationCurve(keys);
  }

  private static float Slope((float Time, float Value)[] values, int from, int to)
  {
    if (from < 0 || to < 0 || from >= values.Length || to >= values.Length) return 0f;
    var delta = values[to].Time - values[from].Time;
    return MathF.Abs(delta) < 1e-8f ? 0f : (values[to].Value - values[from].Value) / delta;
  }
}
