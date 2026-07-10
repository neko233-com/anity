namespace UnityEngine;

/// <summary>
/// Immediate mode drawing utility.
/// </summary>
public static class GL
{
    public static bool sRGBWrite { get; set; } = true;
    public static bool invertCulling { get; set; }
    public static Matrix4x4 modelview { get; set; } = Matrix4x4.identity;
    public static Matrix4x4 projection { get; set; } = Matrix4x4.identity;

    public static void PushMatrix() { }
    public static void PopMatrix() { }
    public static void LoadIdentity() { }
    public static void LoadOrtho() { }
    public static void LoadPixelMatrix() { }
    public static void LoadProjectionMatrix(Matrix4x4 mat) => projection = mat;

    public static void Begin(int mode) { }
    public static void End() { }
    public static void Clear(bool clearDepth, bool clearColor, Color backgroundColor) { }
    public static void Clear(bool clearDepth, bool clearColor, Color backgroundColor, float depth) { }

    public static void Color(Color c) { }
    public static void Vertex(Vector3 v) { }
    public static void Vertex3(float x, float y, float z) { }
    public static void TexCoord(Vector3 v) { }
    public static void TexCoord2(float x, float y) { }
    public static void TexCoord3(float x, float y, float z) { }
    public static void Normal(Vector3 n) { }
    public static void MultMatrix(Matrix4x4 mat) { }

    public static void Viewport(Rect pixelRect) { }
    public static void Viewport(int x, int y, int width, int height) { }

    public static void DrawTexture(Rect screenRect, Texture texture, int sourceX, int sourceY, int sourceWidth, int sourceHeight) { }
    public static void DrawTexture(Rect screenRect, Texture texture, Rect sourceRect, int leftBorder, int rightBorder, int topBorder, int bottomBorder) { }
    public static void DrawTexture(Rect screenRect, Texture texture, Rect sourceRect, int leftBorder, int rightBorder, int topBorder, int bottomBorder, Color color) { }
    public static void DrawTexture(Rect screenRect, Texture texture) { }

    public static void IssuePluginEvent(IntPtr callback, int eventID) { }
}
