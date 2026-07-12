using System.Collections.Generic;

namespace UnityEngine;

public static class GL
{
    public const int TRIANGLES = 4;
    public const int QUADS = 7;
    public const int LINES = 1;
    public const int LINE_STRIP = 2;
    public const int TRIANGLE_STRIP = 5;
    public const int TRIANGLE_FAN = 6;
    public const int QUAD_STRIP = 8;
    public const int LINES_ADJACENCY = 10;
    public const int LINE_STRIP_ADJACENCY = 11;
    public const int TRIANGLES_ADJACENCY = 12;
    public const int TRIANGLE_STRIP_ADJACENCY = 13;
    public const int POINTS = 0;
    public const int PATCHES = 14;

    private static readonly Stack<Matrix4x4> _matrixStack = new();
    private static Matrix4x4 _modelview = Matrix4x4.identity;
    private static Matrix4x4 _projection = Matrix4x4.identity;
    private static bool _invertCulling;
    private static bool _wireframe;
    private static bool _sRGBWrite = true;
    private static int _currentMode = -1;
    private static UnityEngine.Color _currentColor = UnityEngine.Color.white;
    private static readonly List<Vector3> _vertices = new();
    private static readonly List<UnityEngine.Color> _colors = new();
    private static readonly List<Vector3> _texcoords = new();
    private static readonly List<Vector3> _normals = new();
    private static Rect _viewport = new(0, 0, Screen.width, Screen.height);
    private static UnityEngine.Color _clearColor = UnityEngine.Color.clear;
    private static float _clearDepth = 1f;
    private static bool _clearDepthFlag = true;
    private static bool _clearColorFlag = true;

    public static bool sRGBWrite
    {
        get => _sRGBWrite;
        set => _sRGBWrite = value;
    }

    public static bool invertCulling
    {
        get => _invertCulling;
        set => _invertCulling = value;
    }

    public static bool wireframe
    {
        get => _wireframe;
        set => _wireframe = value;
    }

    public static Matrix4x4 modelview
    {
        get => _modelview;
        set => _modelview = value;
    }

    public static Matrix4x4 projection
    {
        get => _projection;
        set => _projection = value;
    }

    public static void PushMatrix()
    {
        _matrixStack.Push(_modelview);
    }

    public static void PopMatrix()
    {
        if (_matrixStack.Count > 0)
        {
            _modelview = _matrixStack.Pop();
        }
    }

    public static void LoadIdentity()
    {
        _modelview = Matrix4x4.identity;
    }

    public static void LoadOrtho()
    {
        _projection = Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 100f);
    }

    public static void LoadPixelMatrix()
    {
        _projection = Matrix4x4.Ortho(0f, Screen.width, 0f, Screen.height, -1f, 100f);
    }

    public static void LoadPixelMatrix(float left, float right, float bottom, float top)
    {
        _projection = Matrix4x4.Ortho(left, right, bottom, top, -1f, 100f);
    }

    public static void LoadProjectionMatrix(Matrix4x4 mat)
    {
        _projection = mat;
    }

    public static void MultMatrix(Matrix4x4 mat)
    {
        _modelview = mat * _modelview;
    }

    public static void Translate(float x, float y, float z)
    {
        var translate = Matrix4x4.identity;
        translate.m03 = x;
        translate.m13 = y;
        translate.m23 = z;
        _modelview = translate * _modelview;
    }

    public static void Translate(Vector3 translation)
    {
        Translate(translation.x, translation.y, translation.z);
    }

    public static void Rotate(float angle, float x, float y, float z)
    {
        var axis = new Vector3(x, y, z).normalized;
        var rad = angle * Mathf.Deg2Rad;
        var s = Mathf.Sin(rad);
        var c = Mathf.Cos(rad);
        var t = 1f - c;

        var rot = Matrix4x4.identity;
        rot.m00 = t * axis.x * axis.x + c;
        rot.m01 = t * axis.x * axis.y - s * axis.z;
        rot.m02 = t * axis.x * axis.z + s * axis.y;
        rot.m10 = t * axis.x * axis.y + s * axis.z;
        rot.m11 = t * axis.y * axis.y + c;
        rot.m12 = t * axis.y * axis.z - s * axis.x;
        rot.m20 = t * axis.x * axis.z - s * axis.y;
        rot.m21 = t * axis.y * axis.z + s * axis.x;
        rot.m22 = t * axis.z * axis.z + c;

        _modelview = rot * _modelview;
    }

    public static void Scale(float x, float y, float z)
    {
        var scale = Matrix4x4.identity;
        scale.m00 = x;
        scale.m11 = y;
        scale.m22 = z;
        _modelview = scale * _modelview;
    }

    public static void Scale(Vector3 s)
    {
        Scale(s.x, s.y, s.z);
    }

    public static void Begin(int mode)
    {
        _currentMode = mode;
        _vertices.Clear();
        _colors.Clear();
        _texcoords.Clear();
        _normals.Clear();
    }

    public static void End()
    {
        FlushImmediate();
        _currentMode = -1;
    }

    private static void FlushImmediate()
    {
        if (_currentMode < 0 || _vertices.Count == 0)
            return;

        _vertices.Clear();
        _colors.Clear();
        _texcoords.Clear();
        _normals.Clear();
    }

    public static void Color(UnityEngine.Color c)
    {
        _currentColor = c;
    }

    public static void Color(float r, float g, float b, float a)
    {
        _currentColor = new UnityEngine.Color(r, g, b, a);
    }

    public static void Color(float r, float g, float b)
    {
        _currentColor = new UnityEngine.Color(r, g, b, 1f);
    }

    public static void Vertex(Vector3 v)
    {
        if (_currentMode < 0)
            return;

        var transformed = _modelview * new Vector4(v.x, v.y, v.z, 1f);
        _vertices.Add(new Vector3(transformed.x, transformed.y, transformed.z));
        _colors.Add(_currentColor);
    }

    public static void Vertex(float x, float y, float z)
    {
        Vertex(new Vector3(x, y, z));
    }

    public static void Vertex(float x, float y)
    {
        Vertex(new Vector3(x, y, 0f));
    }

    public static void Vertex3(float x, float y, float z)
    {
        Vertex(new Vector3(x, y, z));
    }

    public static void TexCoord(Vector3 v)
    {
        if (_currentMode < 0)
            return;
        _texcoords.Add(v);
    }

    public static void TexCoord(Vector2 v)
    {
        TexCoord(new Vector3(v.x, v.y, 0f));
    }

    public static void TexCoord2(float x, float y)
    {
        TexCoord(new Vector3(x, y, 0f));
    }

    public static void TexCoord3(float x, float y, float z)
    {
        TexCoord(new Vector3(x, y, z));
    }

    public static void Normal(Vector3 n)
    {
        if (_currentMode < 0)
            return;
        _normals.Add(n);
    }

    public static void Normal(float x, float y, float z)
    {
        Normal(new Vector3(x, y, z));
    }

    public static void Viewport(Rect pixelRect)
    {
        _viewport = pixelRect;
    }

    public static void Viewport(int x, int y, int width, int height)
    {
        _viewport = new Rect(x, y, width, height);
    }

    public static void Clear(bool clearDepth, bool clearColor, UnityEngine.Color backgroundColor)
    {
        Clear(clearDepth, clearColor, backgroundColor, 1f);
    }

    public static void Clear(bool clearDepth, bool clearColor, UnityEngine.Color backgroundColor, float depth)
    {
        _clearDepthFlag = clearDepth;
        _clearColorFlag = clearColor;
        _clearColor = backgroundColor;
        _clearDepth = depth;
    }

    public static void ClearWithSkybox(bool clearDepth, Camera? camera)
    {
        if (camera != null)
        {
            Clear(clearDepth, true, camera.backgroundColor, 1f);
        }
        else
        {
            Clear(clearDepth, true, UnityEngine.Color.black, 1f);
        }
    }

    public static void DrawTexture(Rect screenRect, Texture texture, int sourceX, int sourceY, int sourceWidth, int sourceHeight)
    {
        _ = texture;
        _ = sourceX;
        _ = sourceY;
        _ = sourceWidth;
        _ = sourceHeight;
        _ = screenRect;
    }

    public static void DrawTexture(Rect screenRect, Texture texture, Rect sourceRect, int leftBorder, int rightBorder, int topBorder, int bottomBorder)
    {
        _ = texture;
        _ = sourceRect;
        _ = leftBorder;
        _ = rightBorder;
        _ = topBorder;
        _ = bottomBorder;
        _ = screenRect;
    }

    public static void DrawTexture(Rect screenRect, Texture texture, Rect sourceRect, int leftBorder, int rightBorder, int topBorder, int bottomBorder, UnityEngine.Color color)
    {
        _ = color;
        _ = texture;
        _ = sourceRect;
        _ = leftBorder;
        _ = rightBorder;
        _ = topBorder;
        _ = bottomBorder;
        _ = screenRect;
    }

    public static void DrawTexture(Rect screenRect, Texture texture)
    {
        _ = texture;
        _ = screenRect;
    }

    public static void DrawTexture(Rect screenRect, Texture texture, int sourceX, int sourceY, int sourceWidth, int sourceHeight, UnityEngine.Color color)
    {
        _ = color;
        _ = texture;
        _ = sourceX;
        _ = sourceY;
        _ = sourceWidth;
        _ = sourceHeight;
        _ = screenRect;
    }

    public static void IssuePluginEvent(IntPtr callback, int eventID)
    {
        _ = callback;
        _ = eventID;
    }

    public static void SetRevertBackfacing(bool revertBackFaces)
    {
        invertCulling = revertBackFaces;
    }

    public static void InvalidateState()
    {
        _currentMode = -1;
        _vertices.Clear();
        _colors.Clear();
        _texcoords.Clear();
        _normals.Clear();
    }

    public static void GetGPUProjectionMatrix(Matrix4x4 proj, bool renderIntoTexture)
    {
        _ = proj;
        _ = renderIntoTexture;
    }

    public static void WaitOnGPU()
    {
    }

    public static void Flush()
    {
        FlushImmediate();
    }

    public static void ResetMatrixMode()
    {
        while (_matrixStack.Count > 0)
            _matrixStack.Pop();
        _modelview = Matrix4x4.identity;
        _projection = Matrix4x4.identity;
    }

    public static void MultiRenderTarget(RenderBuffer[] colorBuffers, RenderBuffer depthBuffer)
    {
        _ = colorBuffers;
        _ = depthBuffer;
    }

    public static ImmediateModeRenderer GetImmediateRenderer(int vertexCount)
    {
        _ = vertexCount;
        return new ImmediateModeRenderer();
    }
}

public sealed class ImmediateModeRenderer
{
    internal ImmediateModeRenderer() { }

    public void Dispose() { }

    public void BaseVertex(float x, float y, float z) { _ = x; _ = y; _ = z; }

    public void Color(Color c) { _ = c; }

    public void Vertex(Vector3 v) { _ = v; }
    public void Vertex(float x, float y, float z) { _ = x; _ = y; _ = z; }

    public void TexCoord(Vector2 uv) { _ = uv; }
    public void TexCoord(float x, float y) { _ = x; _ = y; }

    public void Normal(Vector3 n) { _ = n; }
}
