using System;
using System.Collections.Generic;
using Unity.Collections;

namespace UnityEngine.Rendering;

public enum CommandBufferType
{
    DrawRenderer,
    DrawMesh,
    DrawProcedural,
    DrawMeshInstanced,
    SetGlobalFloat,
    SetGlobalVector,
    SetGlobalColor,
    SetGlobalMatrix,
    SetGlobalTexture,
    SetGlobalInt,
    ClearRenderTarget,
    SetRenderTarget,
    Blit,
    SetViewport,
    SetViewProjectionMatrices,
    SetProjectionMatrix,
    SetViewMatrix,
    EnableScissor,
    DisableScissor,
    SetScissorRect,
    CopyTexture,
    GenerateMips,
    DispatchCompute,
    DrawProceduralIndirect,
    DrawMeshInstancedProcedural,
    DrawMeshInstancedIndirect,
    SetGlobalDepthBias,
    SetInvertCulling,
    NextSubPass,
    BeginRenderPass,
    EndRenderPass,
    MarkLateLatchMatrixShaderPropertyID,
    UnmarkLateLatchMatrixShaderPropertyID,
    ResetLateLatchMatrixShaderProperties,
    SetFogParams,
    SetGlobalVectorArray,
    SetGlobalMatrixArray,
    BeginSample,
    EndSample,
}

public struct CommandBufferCommand
{
    public CommandBufferType type;
    public Renderer renderer;
    public Mesh mesh;
    public Material material;
    public int submeshIndex;
    public int shaderPass;
    public Matrix4x4 matrix;
    public Matrix4x4[] matrices;
    public int instanceCount;
    public int vertexCount;
    public MeshTopology topology;
    public int nameID;
    public float floatValue;
    public Vector4 vectorValue;
    public Matrix4x4 matrixValue;
    public Color colorValue;
    public RenderTargetIdentifier rtValue;
    public bool boolDepth;
    public bool boolColor;
    public float depthValue;
    public RenderTargetIdentifier depthRT;
    public Rect viewport;
    public Rect scissor;
    public Matrix4x4 viewMatrix;
    public Matrix4x4 projMatrix;
    public Texture src;
    public RenderTargetIdentifier dst;
    public ComputeBuffer computeBuffer;
    public ComputeShader computeShader;
    public int kernelIndex;
    public MaterialPropertyBlock properties;
    public int intValue;
    public float depthBias;
    public float slopeBias;
    public bool invertCulling;
    public Vector4[] vectorArrayValue;
    public Matrix4x4[] matrixArrayValue;
}

public struct ShaderPassName
{
    private int _id;
    public string name { get; }
    public ShaderPassName(string name) { this.name = name; _id = Shader.PropertyToID(name); }
}

public class CommandBuffer : IDisposable
{
    private string _name = string.Empty;
    private bool _disposed;
    private readonly List<CommandBufferCommand> _commands = new();

    public CommandBuffer() { }

    public CommandBuffer(string name)
    {
        _name = name ?? string.Empty;
    }

    public string name
    {
        get => _name;
        set => _name = value ?? string.Empty;
    }

    public int sizeInBytes => _commands.Count * 64;
    public int commandCount => _commands.Count;

    public IReadOnlyList<CommandBufferCommand> GetCommands() => _commands;

    public void BeginSample(string name) { _commands.Add(new CommandBufferCommand { type = CommandBufferType.BeginSample, nameID = Shader.PropertyToID(name) }); }
    public void EndSample(string name) { _commands.Add(new CommandBufferCommand { type = CommandBufferType.EndSample, nameID = Shader.PropertyToID(name) }); }

    public void Clear()
    {
        _commands.Clear();
    }

    public void Release()
    {
        _commands.Clear();
        _disposed = true;
    }

    public void Dispose()
    {
        Release();
    }

    public void DrawRenderer(Renderer renderer, Material material)
        => DrawRenderer(renderer, material, 0, -1);
    public void DrawRenderer(Renderer renderer, Material material, int submeshIndex)
        => DrawRenderer(renderer, material, submeshIndex, -1);
    public void DrawRenderer(Renderer renderer, Material material, int submeshIndex, int shaderPass)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.DrawRenderer,
            renderer = renderer,
            material = material,
            submeshIndex = submeshIndex,
            shaderPass = shaderPass,
        });
    }

    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material)
        => DrawMesh(mesh, matrix, material, 0, -1, null);
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex)
        => DrawMesh(mesh, matrix, material, submeshIndex, -1, null);
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex, int shaderPass)
        => DrawMesh(mesh, matrix, material, submeshIndex, shaderPass, null);
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex, int shaderPass, MaterialPropertyBlock properties)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.DrawMesh,
            mesh = mesh,
            matrix = matrix,
            material = material,
            submeshIndex = submeshIndex,
            shaderPass = shaderPass,
            properties = properties,
        });
    }

    public void DrawProcedural(Matrix4x4 matrix, Material material, int shaderPass, MeshTopology topology, int vertexCount, int instanceCount = 1)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.DrawProcedural,
            matrix = matrix,
            material = material,
            shaderPass = shaderPass,
            topology = topology,
            vertexCount = vertexCount,
            instanceCount = instanceCount,
        });
    }

    public void DrawProcedural(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, int vertexCount)
        => DrawProcedural(matrix, material, -1, topology, vertexCount, 1);
    public void DrawProcedural(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, int vertexCount, int instanceCount)
        => DrawProcedural(matrix, material, -1, topology, vertexCount, instanceCount);

    public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, int shaderPass, Matrix4x4[] matrices, int count)
        => DrawMeshInstanced(mesh, submeshIndex, material, shaderPass, matrices, count, null);
    public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices)
        => DrawMeshInstanced(mesh, submeshIndex, material, -1, matrices, matrices?.Length ?? 0, null);
    public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices, int count)
        => DrawMeshInstanced(mesh, submeshIndex, material, -1, matrices, count, null);
    public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices, int count, MaterialPropertyBlock properties)
        => DrawMeshInstanced(mesh, submeshIndex, material, -1, matrices, count, properties);
    public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, int shaderPass, Matrix4x4[] matrices, int count, MaterialPropertyBlock properties)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.DrawMeshInstanced,
            mesh = mesh,
            material = material,
            submeshIndex = submeshIndex,
            shaderPass = shaderPass,
            matrices = matrices,
            instanceCount = count,
            properties = properties,
        });
    }

    public void DrawMeshInstancedProcedural(Mesh mesh, int submeshIndex, Material material, int shaderPass, int count, MaterialPropertyBlock properties = null)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.DrawMeshInstancedProcedural,
            mesh = mesh,
            material = material,
            submeshIndex = submeshIndex,
            shaderPass = shaderPass,
            instanceCount = count,
            properties = properties,
        });
    }

    public void DrawMeshInstancedIndirect(Mesh mesh, int submeshIndex, Material material, int shaderPass, ComputeBuffer bufferWithArgs, int argsOffset = 0, MaterialPropertyBlock properties = null)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.DrawMeshInstancedIndirect,
            mesh = mesh,
            material = material,
            submeshIndex = submeshIndex,
            shaderPass = shaderPass,
            computeBuffer = bufferWithArgs,
            properties = properties,
        });
    }

    public void DrawProceduralIndirect(Matrix4x4 matrix, Material material, int shaderPass, MeshTopology topology, ComputeBuffer bufferWithArgs, int argsOffset = 0)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.DrawProceduralIndirect,
            matrix = matrix,
            material = material,
            shaderPass = shaderPass,
            topology = topology,
            computeBuffer = bufferWithArgs,
            instanceCount = argsOffset,
        });
    }

    public void SetGlobalFloat(string name, float value)
        => SetGlobalFloat(Shader.PropertyToID(name), value);
    public void SetGlobalFloat(int nameID, float value)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.SetGlobalFloat,
            nameID = nameID,
            floatValue = value,
        });
        Shader.SetGlobalFloat(nameID, value);
    }

    public void SetGlobalVector(string name, Vector4 value)
        => SetGlobalVector(Shader.PropertyToID(name), value);
    public void SetGlobalVector(int nameID, Vector4 value)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.SetGlobalVector,
            nameID = nameID,
            vectorValue = value,
        });
        Shader.SetGlobalVector(nameID, value);
    }

    public void SetGlobalColor(string name, Color value)
        => SetGlobalColor(Shader.PropertyToID(name), value);
    public void SetGlobalColor(int nameID, Color value)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.SetGlobalColor,
            nameID = nameID,
            colorValue = value,
        });
        Shader.SetGlobalColor(nameID, value);
    }

    public void SetGlobalMatrix(string name, Matrix4x4 value)
        => SetGlobalMatrix(Shader.PropertyToID(name), value);
    public void SetGlobalMatrix(int nameID, Matrix4x4 value)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.SetGlobalMatrix,
            nameID = nameID,
            matrixValue = value,
        });
        Shader.SetGlobalMatrix(nameID, value);
    }

    public void SetGlobalTexture(string name, RenderTargetIdentifier value)
        => SetGlobalTexture(Shader.PropertyToID(name), value);
    public void SetGlobalTexture(int nameID, RenderTargetIdentifier value)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.SetGlobalTexture,
            nameID = nameID,
            rtValue = value,
        });
    }

    public void SetGlobalInt(string name, int value)
        => SetGlobalInt(Shader.PropertyToID(name), value);
    public void SetGlobalInt(int nameID, int value)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.SetGlobalInt,
            nameID = nameID,
            intValue = value,
        });
        Shader.SetGlobalInt(nameID, value);
    }

    public void SetGlobalVectorArray(int nameID, Vector4[] values)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.SetGlobalVectorArray,
            nameID = nameID,
            vectorArrayValue = values,
        });
        Shader.SetGlobalVectorArray(nameID, values);
    }

    public void SetGlobalMatrixArray(int nameID, Matrix4x4[] values)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.SetGlobalMatrixArray,
            nameID = nameID,
            matrixArrayValue = values,
        });
        Shader.SetGlobalMatrixArray(nameID, values);
    }

    public void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor)
        => ClearRenderTarget(clearDepth, clearColor, backgroundColor, 1.0f);
    public void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor, float depth)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.ClearRenderTarget,
            boolDepth = clearDepth,
            boolColor = clearColor,
            colorValue = backgroundColor,
            depthValue = depth,
        });
        if (clearColor) Graphics._clearColor = backgroundColor;
        if (clearDepth) Graphics._clearDepth = depth;
    }

    public void SetRenderTarget(RenderTargetIdentifier rt)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.SetRenderTarget,
            rtValue = rt,
        });
        Graphics._currentColorRT = rt;
    }

    public void SetRenderTarget(RenderTargetIdentifier color, RenderTargetIdentifier depth)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.SetRenderTarget,
            rtValue = color,
            depthRT = depth,
        });
        Graphics._currentColorRT = color;
        Graphics._currentDepthRT = depth;
    }

    public void Blit(Texture source, RenderTargetIdentifier dest)
        => Blit(source, dest, null, -1);
    public void Blit(Texture source, RenderTargetIdentifier dest, Material mat)
        => Blit(source, dest, mat, -1);
    public void Blit(Texture source, RenderTargetIdentifier dest, Material mat, int pass)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.Blit,
            src = source,
            dst = dest,
            material = mat,
            shaderPass = pass,
        });
        Graphics._currentColorRT = dest;
    }

    public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest)
        => Blit((Texture)null, dest, null, -1);
    public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat)
        => Blit((Texture)null, dest, mat, -1);
    public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat, int pass)
        => Blit((Texture)null, dest, mat, pass);

    public void SetViewport(Rect pixelRect)
    {
        _commands.Add(new CommandBufferCommand { type = CommandBufferType.SetViewport, viewport = pixelRect });
    }

    public void SetViewProjectionMatrices(Matrix4x4 view, Matrix4x4 proj)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.SetViewProjectionMatrices,
            viewMatrix = view,
            projMatrix = proj,
        });
    }

    public void SetProjectionMatrix(Matrix4x4 proj)
    {
        _commands.Add(new CommandBufferCommand { type = CommandBufferType.SetProjectionMatrix, projMatrix = proj });
    }

    public void SetViewMatrix(Matrix4x4 view)
    {
        _commands.Add(new CommandBufferCommand { type = CommandBufferType.SetViewMatrix, viewMatrix = view });
    }

    public void EnableScissor(Rect scissor)
    {
        _commands.Add(new CommandBufferCommand { type = CommandBufferType.EnableScissor, scissor = scissor });
    }

    public void DisableScissor()
    {
        _commands.Add(new CommandBufferCommand { type = CommandBufferType.DisableScissor });
    }

    public void CopyTexture(Texture src, Texture dst)
    {
        _commands.Add(new CommandBufferCommand { type = CommandBufferType.CopyTexture, src = src, rtValue = new RenderTargetIdentifier(dst) });
    }

    public void GenerateMips(RenderTargetIdentifier rt)
    {
        _commands.Add(new CommandBufferCommand { type = CommandBufferType.GenerateMips, rtValue = rt });
    }

    public void DispatchCompute(ComputeShader computeShader, int kernelIndex, int threadGroupsX, int threadGroupsY, int threadGroupsZ)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.DispatchCompute,
            computeShader = computeShader,
            kernelIndex = kernelIndex,
        });
        computeShader?.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
    }

    public void SetGlobalDepthBias(float bias, float slopeBias)
    {
        _commands.Add(new CommandBufferCommand
        {
            type = CommandBufferType.SetGlobalDepthBias,
            depthBias = bias,
            slopeBias = slopeBias,
        });
    }

    public void SetInvertCulling(bool invertCulling)
    {
        _commands.Add(new CommandBufferCommand { type = CommandBufferType.SetInvertCulling, invertCulling = invertCulling });
    }
}
