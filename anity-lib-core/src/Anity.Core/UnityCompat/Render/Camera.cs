using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine;

public class Camera : Behaviour
{
    private static readonly List<Camera> _allCameras = new();
    private static Camera? _main;
    private static int _cameraCount;

    private Matrix4x4 _projectionMatrix;
    private Matrix4x4 _worldToCameraMatrix;
    private bool _projectionMatrixOverride;
    private bool _worldToCameraMatrixOverride;
    private float _fieldOfView = 60f;
    private float _nearClipPlane = 0.3f;
    private float _farClipPlane = 1000f;
    private float _orthographicSize = 5f;
    private bool _orthographic;
    private float _depth;
    private int _cullingMask = -1;
    private int _pixelWidth = 1920;
    private int _pixelHeight = 1080;
    private float _aspectRatio;
    private Rect _rect = new(0f, 0f, 1f, 1f);
    private RenderingPath _renderingPath = RenderingPath.UsePlayerSettings;
    private CameraClearFlags _clearFlags = CameraClearFlags.Skybox;
    private Color _backgroundColor = Color.black;
    private RenderTexture? _targetTexture;
    private CameraType _cameraType = CameraType.Game;
    private bool _useOcclusionCulling;
    private bool _allowHDR = true;
    private bool _allowMSAA = true;
    private bool _enabled = true;
    private int _depthTextureMode;

    public static event CameraCallback? onPreCull;
    public static event CameraCallback? onPreRender;
    public static event CameraCallback? onPostRender;

    public float fieldOfView
    {
        get => _fieldOfView;
        set { _fieldOfView = value; _projectionMatrixOverride = false; }
    }

    public float nearClipPlane
    {
        get => _nearClipPlane;
        set { _nearClipPlane = value; _projectionMatrixOverride = false; }
    }

    public float farClipPlane
    {
        get => _farClipPlane;
        set { _farClipPlane = value; _projectionMatrixOverride = false; }
    }

    public float orthographicSize
    {
        get => _orthographicSize;
        set { _orthographicSize = value; _projectionMatrixOverride = false; }
    }

    public bool orthographic
    {
        get => _orthographic;
        set { _orthographic = value; _projectionMatrixOverride = false; }
    }

    public float depth
    {
        get => _depth;
        set => _depth = value;
    }

    public int cullingMask
    {
        get => _cullingMask;
        set => _cullingMask = value;
    }

    public int pixelWidth
    {
        get => _pixelWidth;
        set { _pixelWidth = value; _projectionMatrixOverride = false; }
    }

    public int pixelHeight
    {
        get => _pixelHeight;
        set { _pixelHeight = value; _projectionMatrixOverride = false; }
    }

    public CameraType cameraType
    {
        get => _cameraType;
        set => _cameraType = value;
    }

    public bool useOcclusionCulling
    {
        get => _useOcclusionCulling;
        set => _useOcclusionCulling = value;
    }

    public bool allowHDR
    {
        get => _allowHDR;
        set => _allowHDR = value;
    }

    public bool allowMSAA
    {
        get => _allowMSAA;
        set => _allowMSAA = value;
    }

    public new bool enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public int depthTextureMode
    {
        get => _depthTextureMode;
        set => _depthTextureMode = value;
    }

    public RenderingPath renderingPath
    {
        get => _renderingPath;
        set => _renderingPath = value;
    }

    public CameraClearFlags clearFlags
    {
        get => _clearFlags;
        set => _clearFlags = value;
    }

    public Color backgroundColor
    {
        get => _backgroundColor;
        set => _backgroundColor = value;
    }

    public RenderTexture? targetTexture
    {
        get => _targetTexture;
        set => _targetTexture = value;
    }

    public float aspect
    {
        get
        {
            if (_aspectRatio > 0f)
                return _aspectRatio;
            int h = _targetTexture != null ? _targetTexture.height : _pixelHeight;
            int w = _targetTexture != null ? _targetTexture.width : _pixelWidth;
            return h == 0 ? 1f : (float)w / h;
        }
        set => _aspectRatio = value;
    }

    public Rect rect
    {
        get => _rect;
        set => _rect = value;
    }

    public Rect pixelRect
    {
        get
        {
            int tw = _targetTexture != null ? _targetTexture.width : Screen.width;
            int th = _targetTexture != null ? _targetTexture.height : Screen.height;
            return new Rect(
                _rect.x * tw,
                _rect.y * th,
                _rect.width * tw,
                _rect.height * th);
        }
        set
        {
            int tw = _targetTexture != null ? _targetTexture.width : Screen.width;
            int th = _targetTexture != null ? _targetTexture.height : Screen.height;
            _rect = new Rect(
                tw > 0 ? value.x / tw : 0f,
                th > 0 ? value.y / th : 0f,
                tw > 0 ? value.width / tw : 1f,
                th > 0 ? value.height / th : 1f);
        }
    }

    public Matrix4x4 projectionMatrix
    {
        get
        {
            if (_projectionMatrixOverride)
                return _projectionMatrix;
            return CalculateProjectionMatrix();
        }
        set
        {
            _projectionMatrix = value;
            _projectionMatrixOverride = true;
        }
    }

    public Matrix4x4 worldToCameraMatrix
    {
        get
        {
            if (_worldToCameraMatrixOverride)
                return _worldToCameraMatrix;
            return CalculateViewMatrix();
        }
        set
        {
            _worldToCameraMatrix = value;
            _worldToCameraMatrixOverride = true;
        }
    }

    public Matrix4x4 cameraToWorldMatrix => worldToCameraMatrix.inverse;

    public static Camera? main
    {
        get
        {
            if (_main != null && !_main.IsDestroyed)
                return _main;
            foreach (var cam in _allCameras)
            {
                if (cam.CompareTag("MainCamera"))
                {
                    _main = cam;
                    return cam;
                }
            }
            return _allCameras.Count > 0 ? _allCameras[0] : null;
        }
    }

    public static Camera? current { get; set; }

    public static Camera[] allCameras => _allCameras.ToArray();

    public int allCamerasCount => _allCameras.Count;

    public Camera()
    {
        _allCameras.Add(this);
        _cameraCount++;
        _pixelWidth = Screen.width;
        _pixelHeight = Screen.height;
    }

    private Matrix4x4 CalculateProjectionMatrix()
    {
        float asp = aspect;
        if (asp < 0.0001f)
            asp = 16f / 9f;

        if (_orthographic)
        {
            float height = _orthographicSize * 2f;
            float width = height * asp;
            return Matrix4x4.Ortho(-width * 0.5f, width * 0.5f, -height * 0.5f, height * 0.5f, _nearClipPlane, _farClipPlane);
        }
        else
        {
            return Matrix4x4.Perspective(_fieldOfView, asp, _nearClipPlane, _farClipPlane);
        }
    }

    private Matrix4x4 CalculateViewMatrix()
    {
        Vector3 pos = transform != null ? transform.position : Vector3.zero;
        Quaternion rot = transform != null ? transform.rotation : Quaternion.identity;
        Vector3 target = pos + rot * Vector3.forward;
        Vector3 up = rot * Vector3.up;
        return Matrix4x4.LookAt(pos, target, up);
    }

    public Matrix4x4 CalculateObliqueMatrix(Vector4 clipPlane)
    {
        Matrix4x4 proj = projectionMatrix;
        Vector4 q = proj.inverse * new Vector4(
            Mathf.Sign(clipPlane.x),
            Mathf.Sign(clipPlane.y),
            1f,
            1f);
        Vector4 c = clipPlane * (2f / Vector4.Dot(clipPlane, q));
        proj.SetRow(2, c - proj.GetRow(3));
        return proj;
    }

    public static Matrix4x4 Perspective(float fov, float aspect, float zNear, float zFar)
    {
        return Matrix4x4.Perspective(fov, aspect, zNear, zFar);
    }

    public static Matrix4x4 Ortho(float left, float right, float bottom, float top, float zNear, float zFar)
    {
        return Matrix4x4.Ortho(left, right, bottom, top, zNear, zFar);
    }

    public void Render()
    {
        var previous = current;
        current = this;
        try
        {
            onPreCull?.Invoke(this);
            onPreRender?.Invoke(this);
            var pipeline = RenderPipelineManager.currentPipeline;
            if (pipeline != null)
            {
                var context = new ScriptableRenderContext();
                pipeline.Render(context, new[] { this });
            }
            onPostRender?.Invoke(this);
        }
        finally
        {
            current = previous;
        }
    }

    public void RenderWithShader(Shader shader, string replacementTag)
    {
        _ = shader;
        _ = replacementTag;
        Render();
    }

    public bool RenderToCubemap(Cubemap cubemap)
    {
        _ = cubemap;
        Render();
        return true;
    }

    public bool RenderToCubemap(Cubemap cubemap, int faceMask)
    {
        _ = cubemap;
        _ = faceMask;
        Render();
        return true;
    }

    public bool RenderToCubemap(RenderTexture cubemap)
    {
        _ = cubemap;
        Render();
        return true;
    }

    public bool RenderToCubemap(RenderTexture cubemap, int faceMask)
    {
        _ = cubemap;
        _ = faceMask;
        Render();
        return true;
    }

    public void ResetProjectionMatrix()
    {
        _projectionMatrixOverride = false;
    }

    public void ResetWorldToCameraMatrix()
    {
        _worldToCameraMatrixOverride = false;
    }

    public Vector3 WorldToScreenPoint(Vector3 worldPosition)
    {
        Matrix4x4 view = worldToCameraMatrix;
        Matrix4x4 proj = projectionMatrix;
        Matrix4x4 vp = proj * view;

        Vector4 clipPos = vp * new Vector4(worldPosition.x, worldPosition.y, worldPosition.z, 1f);
        if (Mathf.Abs(clipPos.w) < 1e-8f)
            return Vector3.zero;

        Vector3 ndc = new Vector3(clipPos.x / clipPos.w, clipPos.y / clipPos.w, clipPos.z / clipPos.w);

        float screenX = (ndc.x + 1f) * 0.5f * _pixelWidth;
        float screenY = (ndc.y + 1f) * 0.5f * _pixelHeight;
        float screenZ = (ndc.z + 1f) * 0.5f;

        return new Vector3(screenX, screenY, screenZ);
    }

    public Vector3 WorldToViewportPoint(Vector3 worldPosition)
    {
        Vector3 screen = WorldToScreenPoint(worldPosition);
        return new Vector3(screen.x / _pixelWidth, screen.y / _pixelHeight, screen.z);
    }

    public Vector3 ScreenToViewportPoint(Vector3 screenPosition)
    {
        if (_pixelWidth == 0 || _pixelHeight == 0)
            return Vector3.zero;
        return new Vector3(screenPosition.x / _pixelWidth, screenPosition.y / _pixelHeight, screenPosition.z);
    }

    public Vector3 ViewportToScreenPoint(Vector3 viewportPoint)
    {
        return new Vector3(viewportPoint.x * _pixelWidth, viewportPoint.y * _pixelHeight, viewportPoint.z);
    }

    public Vector3 ScreenToWorldPoint(Vector3 screenPosition)
    {
        if (_pixelWidth == 0 || _pixelHeight == 0)
            return screenPosition;

        Matrix4x4 view = worldToCameraMatrix;
        Matrix4x4 proj = projectionMatrix;
        Matrix4x4 invVP = (proj * view).inverse;

        float ndcX = 2f * screenPosition.x / _pixelWidth - 1f;
        float ndcY = 2f * screenPosition.y / _pixelHeight - 1f;
        float ndcZ = screenPosition.z * 2f - 1f;

        Vector4 worldPos = invVP * new Vector4(ndcX, ndcY, ndcZ, 1f);
        if (Mathf.Abs(worldPos.w) < 1e-8f)
            return new Vector3(worldPos.x, worldPos.y, worldPos.z);

        return new Vector3(worldPos.x / worldPos.w, worldPos.y / worldPos.w, worldPos.z / worldPos.w);
    }

    public Vector3 ViewportToWorldPoint(Vector3 viewportPoint)
    {
        return ScreenToWorldPoint(ViewportToScreenPoint(viewportPoint));
    }

    public Ray ViewportPointToRay(Vector3 viewportPoint)
    {
        return ScreenPointToRay(ViewportToScreenPoint(viewportPoint));
    }

    public Ray ViewportPointToRay(float x, float y)
    {
        return ViewportPointToRay(new Vector3(x, y, 0f));
    }

    public Ray ViewportPointToRay(Vector2 uv)
    {
        return ViewportPointToRay(new Vector3(uv.x, uv.y, 0f));
    }

    public Ray ScreenPointToRay(Vector3 pos)
    {
        Vector3 origin = ScreenToWorldPoint(new Vector3(pos.x, pos.y, _nearClipPlane));
        Vector3 farPoint = ScreenToWorldPoint(new Vector3(pos.x, pos.y, _farClipPlane));
        Vector3 direction = (farPoint - origin).normalized;
        return new Ray(origin, direction);
    }

    public Ray ScreenPointToRay(float x, float y)
    {
        return ScreenPointToRay(new Vector3(x, y, 0f));
    }

    public Vector3 ScreenToWorldPoint(Vector2 screenPosition, float distance)
    {
        return ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distance));
    }

    public static int GetAllCameras(Camera[] cameras)
    {
        int count = Mathf.Min(cameras.Length, _allCameras.Count);
        for (int i = 0; i < count; i++)
            cameras[i] = _allCameras[i];
        return count;
    }

    public void CopyFrom(Camera other)
    {
        if (other == null) return;
        _fieldOfView = other._fieldOfView;
        _nearClipPlane = other._nearClipPlane;
        _farClipPlane = other._farClipPlane;
        _orthographicSize = other._orthographicSize;
        _orthographic = other._orthographic;
        _depth = other._depth;
        _cullingMask = other._cullingMask;
        _pixelWidth = other._pixelWidth;
        _pixelHeight = other._pixelHeight;
        _aspectRatio = other._aspectRatio;
        _rect = other._rect;
        _renderingPath = other._renderingPath;
        _clearFlags = other._clearFlags;
        _backgroundColor = other._backgroundColor;
        _targetTexture = other._targetTexture;
        _cameraType = other._cameraType;
        _allowHDR = other._allowHDR;
        _allowMSAA = other._allowMSAA;
        _depthTextureMode = other._depthTextureMode;
    }

    public class RenderRequest
    {
        public RenderTexture? destination;
        public int mipLevel;
        public CubemapFace face;
        public int slice;
        public bool isValid;
    }
}

public delegate void CameraCallback(Camera cam);

public enum RenderingPath
{
    UsePlayerSettings,
    Forward,
    DeferredLighting,
    DeferredShading,
    VertexLit
}
