using System;

namespace UnityEngine;

[Flags]
public enum CameraType
{
    Game = 1,
    SceneView = 2,
    Preview = 4,
    VR = 8,
    Reflection = 16,
}

public struct Vector2Int : IEquatable<Vector2Int>
{
    public int x;
    public int y;

    public Vector2Int(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public static Vector2Int zero => new Vector2Int(0, 0);
    public static Vector2Int one => new Vector2Int(1, 1);
    public static Vector2Int up => new Vector2Int(0, 1);
    public static Vector2Int down => new Vector2Int(0, -1);
    public static Vector2Int left => new Vector2Int(-1, 0);
    public static Vector2Int right => new Vector2Int(1, 0);

    public int sqrMagnitude => x * x + y * y;
    public float magnitude => (float)Math.Sqrt(sqrMagnitude);

    public static int Distance(Vector2Int a, Vector2Int b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        return (int)Math.Sqrt(dx * dx + dy * dy);
    }

    public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new Vector2Int(a.x + b.x, a.y + b.y);
    public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new Vector2Int(a.x - b.x, a.y - b.y);
    public static Vector2Int operator *(Vector2Int a, int d) => new Vector2Int(a.x * d, a.y * d);
    public static Vector2Int operator *(int d, Vector2Int a) => new Vector2Int(a.x * d, a.y * d);
    public static bool operator ==(Vector2Int lhs, Vector2Int rhs) => lhs.x == rhs.x && lhs.y == rhs.y;
    public static bool operator !=(Vector2Int lhs, Vector2Int rhs) => !(lhs == rhs);

    public bool Equals(Vector2Int other) => x == other.x && y == other.y;
    public override bool Equals(object obj) => obj is Vector2Int other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(x, y);
    public override string ToString() => $"({x}, {y})";

    public static implicit operator Vector2(Vector2Int v) => new Vector2(v.x, v.y);
}

public struct RectInt
{
    public int x;
    public int y;
    public int width;
    public int height;

    public RectInt(int x, int y, int width, int height)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }

    public int xMin
    {
        get => x;
        set { width += x - value; x = value; }
    }

    public int yMin
    {
        get => y;
        set { height += y - value; y = value; }
    }

    public int xMax
    {
        get => x + width;
        set => width = value - x;
    }

    public int yMax
    {
        get => y + height;
        set => height = value - y;
    }

    public Vector2Int position
    {
        get => new Vector2Int(x, y);
        set { x = value.x; y = value.y; }
    }

    public Vector2Int size
    {
        get => new Vector2Int(width, height);
        set { width = value.x; height = value.y; }
    }

    public Vector2Int center => new Vector2Int(x + width / 2, y + height / 2);

    public bool Contains(Vector2Int point)
    {
        return point.x >= x && point.x < x + width && point.y >= y && point.y < y + height;
    }

    public static RectInt zero => new RectInt(0, 0, 0, 0);
}

public struct RectOffset
{
    public int left;
    public int right;
    public int top;
    public int bottom;

    public RectOffset(int left, int right, int top, int bottom)
    {
        this.left = left;
        this.right = right;
        this.top = top;
        this.bottom = bottom;
    }

    public Rect Add(Rect rect)
    {
        return new Rect(rect.x - left, rect.y - top, rect.width + left + right, rect.height + top + bottom);
    }

    public Rect Remove(Rect rect)
    {
        return new Rect(rect.x + left, rect.y + top, rect.width - left - right, rect.height - top - bottom);
    }
}

public enum CameraClearFlags
{
    Skybox = 1,
    Color = 2,
    SolidColor = 2,
    Depth = 3,
    Nothing = 4,
}

public enum ScaleMode
{
    StretchToFill = 0,
    ScaleAndCrop = 1,
    ScaleToFit = 2,
}

public enum TextAnchor
{
    UpperLeft = 0,
    UpperCenter = 1,
    UpperRight = 2,
    MiddleLeft = 3,
    MiddleCenter = 4,
    MiddleRight = 5,
    LowerLeft = 6,
    LowerCenter = 7,
    LowerRight = 8,
}

public enum TextAlignment
{
    Left = 0,
    Center = 1,
    Right = 2,
    Justified = 3,
}

public enum FontStyle
{
    Normal = 0,
    Bold = 1,
    Italic = 2,
    BoldAndItalic = 3,
}

public enum ImageType
{
    Simple = 0,
    Sliced = 1,
    Tiled = 2,
    Filled = 3,
}

public enum HorizontalWrapMode
{
    Wrap = 0,
    Overflow = 1,
}

public enum VerticalWrapMode
{
    Truncate = 0,
    Overflow = 1,
}

public class AssetImporter : Object
{
    public string assetPath { get; protected set; } = string.Empty;
}

public class ComputeBuffer : IDisposable
{
    private int _count;
    private int _stride;
    private ComputeBufferType _type;
    private byte[] _data;
    private bool _released;
    private bool _disposed;

    public ComputeBuffer(int count, int stride) : this(count, stride, ComputeBufferType.Default)
    {
    }

    public ComputeBuffer(int count, int stride, ComputeBufferType type)
    {
        if (count <= 0) throw new ArgumentException("Count must be greater than zero.", nameof(count));
        if (stride <= 0) throw new ArgumentException("Stride must be greater than zero.", nameof(stride));

        _count = count;
        _stride = stride;
        _type = type;
        _data = new byte[count * stride];
        _released = false;
        _disposed = false;
    }

    public int count => _count;
    public int stride => _stride;
    public ComputeBufferType type => _type;

    public void SetData(Array data)
    {
        if (_released) throw new InvalidOperationException("ComputeBuffer has been released.");
        if (data == null) throw new ArgumentNullException(nameof(data));

        int totalBytes = System.Buffer.ByteLength(data);
        int bytesToCopy = Math.Min(totalBytes, _data.Length);
        System.Buffer.BlockCopy(data, 0, _data, 0, bytesToCopy);
    }

    public void GetData(Array data)
    {
        if (_released) throw new InvalidOperationException("ComputeBuffer has been released.");
        if (data == null) throw new ArgumentNullException(nameof(data));

        int totalBytes = System.Buffer.ByteLength(data);
        int bytesToCopy = Math.Min(_data.Length, totalBytes);
        System.Buffer.BlockCopy(_data, 0, data, 0, bytesToCopy);
    }

    public void Release()
    {
        if (!_released)
        {
            _data = null;
            _count = 0;
            _stride = 0;
            _released = true;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Release();
            _disposed = true;
        }
    }
}

public enum ComputeBufferType
{
    Default,
    Raw,
    Append,
    Counter,
    IndirectArguments,
    Structured
}

public class ComputeShader : Object
{
    private readonly Dictionary<int, float> _floats = new();
    private readonly Dictionary<int, int> _ints = new();
    private readonly Dictionary<int, Vector4> _vectors = new();
    private readonly Dictionary<int, Matrix4x4> _matrices = new();
    private readonly Dictionary<int, Texture> _textures = new();
    private readonly Dictionary<int, ComputeBuffer> _buffers = new();
    private readonly HashSet<string> _kernelNames = new();
    private int _dispatchCount;

    public bool HasKernel(string name)
    {
        return _kernelNames.Contains(name);
    }

    public int FindKernel(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        _kernelNames.Add(name);
        return name.GetHashCode();
    }

    public void SetFloat(int nameID, float val)
    {
        _floats[nameID] = val;
    }

    public void SetInt(int nameID, int val)
    {
        _ints[nameID] = val;
    }

    public void SetVector(int nameID, Vector4 val)
    {
        _vectors[nameID] = val;
    }

    public void SetMatrix(int nameID, Matrix4x4 val)
    {
        _matrices[nameID] = val;
    }

    public void SetTexture(int kernelIndex, int nameID, Texture texture)
    {
        _textures[nameID] = texture;
    }

    public void SetBuffer(int kernelIndex, int nameID, ComputeBuffer buffer)
    {
        _buffers[nameID] = buffer;
    }

    public void Dispatch(int kernelIndex, int threadGroupsX, int threadGroupsY, int threadGroupsZ)
    {
        _dispatchCount++;
    }
}

public class Flare : Object
{
    public Texture texture { get; set; }
}

