using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Anity.Core.Runtime.Native;

/// <summary>
/// Converts Unity CanvasRenderer scene state into the persistent native UI queue.
/// This is deliberately internal: Unity's public API surface remains Canvas/CanvasRenderer.
/// </summary>
internal static class CanvasNativeRenderBridge
{
    internal readonly struct RenderCommand
    {
        internal RenderCommand(
            AnityNative.UIRenderCommandDesc desc,
            AnityNative.UIPackedVertex[] vertices,
            uint[] indices)
        {
            Desc = desc;
            Vertices = vertices;
            Indices = indices;
        }

        internal AnityNative.UIRenderCommandDesc Desc { get; }
        internal AnityNative.UIPackedVertex[] Vertices { get; }
        internal uint[] Indices { get; }
    }

    private static NativeGraphicsDevice? _device;
    private static NativeUICanvas? _canvas;
    private static ulong _frameId;

    internal static NativeUICanvas? CurrentCanvas => _canvas is { IsValid: true } ? _canvas : null;

    internal static void Flush(NativeGraphicsDevice? requestedDevice = null)
    {
        NativeGraphicsDevice? device = requestedDevice ?? NativeGraphicsDevice.Current;
        if (device is null || !device.IsValid || !device.HasSwapchain)
            return;

        // A caller-owned queue is an explicit rendering decision and always wins.
        if (device.AttachedUICanvas is { } attached && !ReferenceEquals(attached, _canvas))
            return;

        CanvasRenderer[] renderers = UnityEngine.Object.FindObjectsOfType<CanvasRenderer>()
            .Where(IsEligible)
            .ToArray();

        // Do not replace an explicitly attached NativeUICanvas when no Unity Canvas exists.
        if (renderers.Length == 0 && (_canvas is null || _device != device))
            return;

        EnsureCanvas(device);
        if (_canvas is null) return;

        _canvas.BeginFrame(++_frameId);
        _canvas.Clear();

        Array.Sort(renderers, CompareRenderers);
        for (int depth = 0; depth < renderers.Length; depth++)
        {
            CanvasRenderer renderer = renderers[depth];
            renderer.SetDepth(depth, depth);
            if (!TryBuildCommands(renderer, device, depth, out RenderCommand[] commands))
                continue;
            foreach (RenderCommand command in commands)
                _canvas.Upsert(command.Desc, command.Vertices, command.Indices);
        }

        _canvas.BuildBatches();
    }

    internal static void OnDeviceDisposed(NativeGraphicsDevice device)
    {
        if (_device != device) return;
        _canvas?.Dispose();
        _canvas = null;
        _device = null;
    }

    internal static void ResetForTests()
    {
        if (_device is not null)
            _device.AttachUICanvas(null);
        _canvas?.Dispose();
        _canvas = null;
        _device = null;
        _frameId = 0;
    }

    private static void EnsureCanvas(NativeGraphicsDevice device)
    {
        if (_device == device && _canvas is { IsValid: true }) return;
        if (_device is not null)
            _device.AttachUICanvas(null);
        _canvas?.Dispose();
        _canvas = NativeUICanvas.TryCreate();
        _device = _canvas is null ? null : device;
        if (_canvas is not null && !device.AttachUICanvas(_canvas))
        {
            _canvas.Dispose();
            _canvas = null;
            _device = null;
        }
    }

    private static bool IsEligible(CanvasRenderer renderer)
        => renderer is not null && renderer.gameObject is { activeInHierarchy: true } &&
           renderer.GetComponentInParent<Canvas>() is { enabled: true };

    private static int CompareRenderers(CanvasRenderer left, CanvasRenderer right)
    {
        Canvas? leftCanvas = left.GetComponentInParent<Canvas>();
        Canvas? rightCanvas = right.GetComponentInParent<Canvas>();
        int compare = (leftCanvas?.targetDisplay ?? 0).CompareTo(rightCanvas?.targetDisplay ?? 0);
        if (compare != 0) return compare;
        compare = (leftCanvas?.GetSortKey() ?? 0).CompareTo(rightCanvas?.GetSortKey() ?? 0);
        if (compare != 0) return compare;
        compare = CompareHierarchy(left.transform, right.transform);
        return compare != 0 ? compare : left.GetInstanceID().CompareTo(right.GetInstanceID());
    }

    private static int CompareHierarchy(Transform? left, Transform? right)
    {
        int[] leftPath = HierarchyPath(left);
        int[] rightPath = HierarchyPath(right);
        int length = Math.Min(leftPath.Length, rightPath.Length);
        for (int index = 0; index < length; index++)
        {
            int compare = leftPath[index].CompareTo(rightPath[index]);
            if (compare != 0) return compare;
        }
        return leftPath.Length.CompareTo(rightPath.Length);
    }

    private static int[] HierarchyPath(Transform? transform)
    {
        var path = new List<int>();
        for (Transform? current = transform; current is not null; current = current.parent)
            path.Add(current.GetSiblingIndex());
        path.Reverse();
        return path.ToArray();
    }

    internal static bool TryBuild(
        CanvasRenderer renderer,
        NativeGraphicsDevice device,
        int sortDepth,
        out AnityNative.UIRenderCommandDesc desc,
        out AnityNative.UIPackedVertex[] packed,
        out uint[] indices)
    {
        if (!TryBuildCommands(renderer, device, sortDepth, out RenderCommand[] commands))
        {
            desc = default;
            packed = Array.Empty<AnityNative.UIPackedVertex>();
            indices = Array.Empty<uint>();
            return false;
        }

        desc = commands[0].Desc;
        desc.rendererId = Id(renderer);
        packed = commands[0].Vertices;
        if (commands.Length == 1)
        {
            indices = commands[0].Indices;
            return true;
        }

        int indexCount = commands.Sum(command => command.Indices.Length);
        indices = new uint[indexCount];
        int offset = 0;
        foreach (RenderCommand command in commands)
        {
            Array.Copy(command.Indices, 0, indices, offset, command.Indices.Length);
            offset += command.Indices.Length;
        }
        return true;
    }

    internal static bool TryBuildCommands(
        CanvasRenderer renderer,
        NativeGraphicsDevice device,
        int sortDepth,
        out RenderCommand[] commands)
    {
        commands = Array.Empty<RenderCommand>();

        Mesh? mesh = renderer.GetMesh();
        Canvas? canvas = renderer.GetComponentInParent<Canvas>();
        if (mesh is null || mesh.vertexCount == 0 || canvas is null || renderer.transform is null)
            return false;

        Vector3[] positions = mesh.vertices;
        Color32[] colors32 = mesh.colors32;
        Color[] colors = mesh.colors;
        Vector2[] uv0 = mesh.uv;
        Vector2[] uv1 = mesh.uv2;
        Vector2[] uv2 = mesh.uv3;
        Vector2[] uv3 = mesh.uv4;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;
        var nativeVertices = new AnityNative.UIPackedVertex[positions.Length];

        Color rendererColor = renderer.GetColor();
        float inheritedAlpha = renderer.GetInheritedAlpha();
        for (int index = 0; index < positions.Length; index++)
        {
            Vector3 screen = ToScreenPoint(renderer.transform, canvas, positions[index]);
            if (!IsFinite(screen)) return false;
            Color32 sourceColor = index < colors32.Length ? colors32[index]
                : index < colors.Length ? (Color32)colors[index] : new Color32(255, 255, 255, 255);
            Color32 finalColor = Multiply(sourceColor, rendererColor, inheritedAlpha);
            Vector3 normal = index < normals.Length ? renderer.transform.TransformDirection(normals[index]) : Vector3.zero;
            Vector4 tangent = index < tangents.Length ? tangents[index] : Vector4.zero;
            nativeVertices[index] = new AnityNative.UIPackedVertex
            {
                position = new AnityNative.UIVector3(screen.x, device.Height - screen.y, screen.z),
                color = new AnityNative.UIColor32(finalColor.r, finalColor.g, finalColor.b, finalColor.a),
                uv0 = ToNative(index < uv0.Length ? uv0[index] : Vector2.zero),
                uv1 = ToNative(index < uv1.Length ? uv1[index] : Vector2.zero),
                uv2 = ToNative(index < uv2.Length ? uv2[index] : Vector2.zero),
                uv3 = ToNative(index < uv3.Length ? uv3[index] : Vector2.zero),
                normal = new AnityNative.UIVector3(normal.x, normal.y, normal.z),
                tangent = new AnityNative.UIVector4(tangent.x, tangent.y, tangent.z, tangent.w)
            };
        }

        var subMeshIndices = new List<uint[]>();
        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            int[] source = mesh.GetIndices(subMesh);
            if (source.Length == 0) continue;
            if (mesh.GetTopology(subMesh) != MeshTopology.Triangles || source.Length % 3 != 0)
                return false;
            var nativeIndices = new uint[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                if (source[index] < 0 || source[index] >= positions.Length) return false;
                nativeIndices[index] = (uint)source[index];
            }
            subMeshIndices.Add(nativeIndices);
        }
        if (subMeshIndices.Count == 0) return false;

        bool visible = !renderer.cull && (!renderer.cullTransparentMesh || finalAlpha(renderer) > 0f);
        AnityNative.UIRenderCommandFlags flags = visible ? AnityNative.UIRenderCommandFlags.Visible : 0;
        if (renderer.hasRectClipping) flags |= AnityNative.UIRenderCommandFlags.RectClip;
        if (renderer.nativeIsMask) flags |= AnityNative.UIRenderCommandFlags.Mask;
        if (renderer.hasPopInstruction) flags |= AnityNative.UIRenderCommandFlags.Pop;

        Rect clip = renderer.nativeClippingRect;
        var built = new RenderCommand[subMeshIndices.Count];
        Texture? explicitTexture = renderer.GetTexture();
        int commandIndex = 0;
        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            int[] source = mesh.GetIndices(subMesh);
            if (source.Length == 0) continue;
            Material? material = renderer.GetMaterial(subMesh) ?? renderer.GetMaterial(0);
            Texture? texture = explicitTexture ?? material?.mainTexture;
            device.EnsureTexture(texture);
            device.EnsureTexture(renderer.nativeAlphaTexture);
            var desc = new AnityNative.UIRenderCommandDesc
            {
                rendererId = CommandId(renderer, subMesh, subMeshIndices.Count),
                materialId = Id(material),
                textureId = Id(texture),
                alphaTextureId = Id(renderer.nativeAlphaTexture),
                sortDepth = sortDepth,
                flags = flags,
                clipXMin = clip.xMin,
                clipYMin = device.Height - clip.yMax,
                clipXMax = clip.xMax,
                clipYMax = device.Height - clip.yMin,
                softnessX = renderer.clippingSoftness.x,
                softnessY = renderer.clippingSoftness.y,
                effectiveAlpha = finalAlpha(renderer)
            };
            built[commandIndex] = new RenderCommand(desc, nativeVertices, subMeshIndices[commandIndex]);
            commandIndex++;
        }
        commands = built;
        return true;
    }

    private static float finalAlpha(CanvasRenderer renderer)
        => Mathf.Clamp01(renderer.GetColor().a * renderer.GetInheritedAlpha());

    private static Vector3 ToScreenPoint(Transform transform, Canvas canvas, Vector3 local)
    {
        Vector3 world = transform.TransformPoint(local);
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return world;
        Camera? camera = canvas.worldCamera;
        return camera is null ? world : camera.WorldToScreenPoint(world);
    }

    private static Color32 Multiply(Color32 source, Color tint, float inheritedAlpha)
        => new(
            ToByte(source.r / 255f * tint.r),
            ToByte(source.g / 255f * tint.g),
            ToByte(source.b / 255f * tint.b),
            ToByte(source.a / 255f * tint.a * inheritedAlpha));

    private static byte ToByte(float value)
        => (byte)Math.Clamp((int)MathF.Round(Mathf.Clamp01(value) * 255f), 0, 255);

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    private static AnityNative.UIVector4 ToNative(Vector2 value)
        => new(value.x, value.y, 0f, 0f);

    private static ulong Id(UnityEngine.Object? value)
        => value is null ? 0UL : unchecked((ulong)(uint)value.GetInstanceID());

    private static ulong CommandId(CanvasRenderer renderer, int subMesh, int commandCount)
    {
        ulong rendererId = Id(renderer);
        return commandCount == 1
            ? rendererId
            : (rendererId << 32) | unchecked((uint)(subMesh + 1));
    }
}
