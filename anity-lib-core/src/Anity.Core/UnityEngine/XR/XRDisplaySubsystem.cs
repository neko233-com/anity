using System;
using System.Runtime.CompilerServices;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.XR;

/// <summary>Unity 2022-compatible XR display frame layout consumed by URP.</summary>
public enum XRTextureLayout
{
    Texture2D = 0,
    Texture2DArray = 1,
    SinglePassInstanced = 2,
    SeparateTexture2Ds = 3
}

public enum XRMirrorViewBlitMode
{
    LeftEye = 0,
    RightEye = 1,
    SideBySide = 2,
    SideBySideOcclusionMesh = 3,
    MotionVectors = 4
}

public readonly struct XRRenderParameter
{
    public Camera camera { get; }
    public Matrix4x4 projection { get; }
    public Matrix4x4 view { get; }
    public Rect viewport { get; }
    public int textureArraySlice { get; }

    internal XRRenderParameter(Camera camera, Matrix4x4 projection, Matrix4x4 view, Rect viewport, int textureArraySlice)
    {
        this.camera = camera;
        this.projection = projection;
        this.view = view;
        this.viewport = viewport;
        this.textureArraySlice = textureArraySlice;
    }
}

public readonly struct XRRenderPass
{
    public RenderTargetIdentifier renderTarget { get; }
    public RenderTextureDescriptor renderTargetDesc { get; }
    public int cullingPassIndex { get; }
    public int renderParameterCount { get; }
    private readonly XRRenderParameter[]? _parameters;

    internal XRRenderPass(RenderTexture renderTarget, RenderTextureDescriptor descriptor, int cullingPassIndex, XRRenderParameter[] parameters)
    {
        this.renderTarget = new RenderTargetIdentifier(renderTarget);
        renderTargetDesc = descriptor;
        this.cullingPassIndex = cullingPassIndex;
        _parameters = parameters;
        renderParameterCount = parameters.Length;
    }

    /// <summary>Returns a display parameter with Unity's XR render-pass API shape.</summary>
    public void GetRenderParameter(Camera camera, int index, out XRRenderParameter renderParameter)
    {
        if (camera is null) throw new ArgumentNullException(nameof(camera));
        if (_parameters is null || index < 0 || index >= _parameters.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (!ReferenceEquals(camera, _parameters[index].camera))
            throw new ArgumentException("The camera is not registered for this XR render pass.", nameof(camera));
        renderParameter = _parameters[index];
    }

    internal bool TryGetRenderParameter(Camera camera, int index, out XRRenderParameter renderParameter)
    {
        if (camera is null || _parameters is null || index < 0 || index >= _parameters.Length ||
            !ReferenceEquals(camera, _parameters[index].camera))
        {
            renderParameter = default;
            return false;
        }
        renderParameter = _parameters[index];
        return true;
    }
}

/// <summary>
/// Product XR display provider. It supplies a two-eye Texture2DArray layout to
/// the existing native Metal single-pass-instanced URP path; it does not
/// simulate stereo by issuing two independent provider frames.
/// </summary>
public sealed class XRDisplaySubsystem
{
    // A provider target carries scheduling semantics as well as texture shape.
    // Plain Tex2DArray targets keep the existing URP inference path; provider
    // targets additionally honour the display's explicit multipass request.
    private static readonly ConditionalWeakTable<RenderTexture, XRDisplaySubsystem> Providers = new();
    private XRRenderPass[] _passes = Array.Empty<XRRenderPass>();
    private RenderTexture? _target;

    public bool running { get; private set; }
    public bool displayOpaque { get; set; } = true;
    public float scaleOfAllViewports { get; set; } = 1f;
    /// <summary>Provider-owned dynamic-resolution multiplier applied after the XR viewport scale.</summary>
    public float dynamicResolutionScale { get; private set; } = 1f;
    public float minDynamicResolutionScale { get; set; } = .5f;
    public float maxDynamicResolutionScale { get; set; } = 1.5f;
    public float zNear { get; set; } = .1f;
    public float zFar { get; set; } = 1000f;
    public bool singlePassRenderingDisabled { get; set; }
    public XRTextureLayout textureLayout => singlePassRenderingDisabled ? XRTextureLayout.Texture2DArray : XRTextureLayout.SinglePassInstanced;
    public int renderPassCount => _passes.Length;
    public XRMirrorViewBlitMode mirrorViewBlitMode { get; set; } = XRMirrorViewBlitMode.SideBySide;

    public void Start() => running = true;
    public void Stop() => running = false;

    /// <summary>
    /// Updates the next provider frame's dynamic-resolution multiplier. The
    /// following <see cref="ConfigureStereoFrame"/> recreates the two-eye
    /// target only when the effective dimensions actually change.
    /// </summary>
    public void SetDynamicResolutionScale(float scale)
    {
        if (!float.IsFinite(scale)) throw new ArgumentOutOfRangeException(nameof(scale));
        if (!float.IsFinite(minDynamicResolutionScale) || !float.IsFinite(maxDynamicResolutionScale) ||
            minDynamicResolutionScale <= 0f || maxDynamicResolutionScale < minDynamicResolutionScale)
            throw new InvalidOperationException("XR dynamic-resolution bounds are invalid.");
        if (scale < minDynamicResolutionScale || scale > maxDynamicResolutionScale)
            throw new ArgumentOutOfRangeException(nameof(scale));
        dynamicResolutionScale = scale;
    }

    /// <summary>Allocates or reuses the provider-owned two-eye render target and assigns it to the camera.</summary>
    public RenderTexture ConfigureStereoFrame(Camera camera, int width, int height,
        RenderTextureFormat format = RenderTextureFormat.DefaultHDR, int msaaSamples = 1)
    {
        if (camera is null) throw new ArgumentNullException(nameof(camera));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (msaaSamples is not (1 or 2 or 4 or 8)) throw new ArgumentOutOfRangeException(nameof(msaaSamples));
        if (!float.IsFinite(scaleOfAllViewports) || scaleOfAllViewports <= 0f) throw new InvalidOperationException("XR viewport scale must be finite and positive.");
        if (!float.IsFinite(zNear) || !float.IsFinite(zFar) || zNear <= 0f || zFar <= zNear) throw new InvalidOperationException("XR clip planes are invalid.");

        float effectiveScale = scaleOfAllViewports * dynamicResolutionScale;
        if (!float.IsFinite(effectiveScale) || effectiveScale <= 0f)
            throw new InvalidOperationException("XR effective render scale must be finite and positive.");
        int scaledWidth = Math.Max(1, (int)Math.Round(width * effectiveScale));
        int scaledHeight = Math.Max(1, (int)Math.Round(height * effectiveScale));
        var descriptor = new RenderTextureDescriptor(scaledWidth, scaledHeight, format, 32)
        {
            dimension = global::UnityEngine.TextureDimension.Tex2DArray,
            volumeDepth = 2,
            msaaSamples = msaaSamples,
            useDynamicScale = true,
            vrUsage = global::UnityEngine.VRTextureUsage.TwoEyes
        };
        if (_target is null || _target.width != scaledWidth || _target.height != scaledHeight ||
            _target.format != format || _target.msaaSamples != msaaSamples || !_target.IsCreated())
        {
            if (_target is not null)
            {
                Providers.Remove(_target);
                _target.Release();
            }
            _target = new RenderTexture(descriptor);
            _target.Create();
            Providers.Add(_target, this);
        }

        camera.targetTexture = _target;
        camera.stereoTargetEye = Camera.StereoTargetEyeMask.Both;
        var viewport = new Rect(0f, 0f, scaledWidth, scaledHeight);
        _passes = new[]
        {
            new XRRenderPass(_target, descriptor, 0, new[]
            {
                new XRRenderParameter(camera, camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left), viewport, 0),
                new XRRenderParameter(camera, camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right), viewport, 1)
            })
        };
        return _target;
    }

    /// <summary>
    /// Returns whether a provider-owned array target may use native
    /// single-pass instancing this frame. This is intentionally internal: URP
    /// consumes provider scheduling state without expanding the public API.
    /// </summary>
    internal static bool AllowsSinglePassInstanced(RenderTexture target) =>
        !Providers.TryGetValue(target, out var provider) || !provider.singlePassRenderingDisabled;

    /// <summary>Lets URP consume the provider's public culling-pass contract.</summary>
    internal static bool TryGetCullingParameters(RenderTexture target, Camera camera,
        out ScriptableCullingParameters cullingParameters)
    {
        cullingParameters = default;
        return Providers.TryGetValue(target, out var provider) &&
            provider.GetCullingParameters(camera, 0, out cullingParameters);
    }

    /// <summary>Retrieves the display-provider render pass for the current frame.</summary>
    public bool GetRenderPass(int index, out XRRenderPass renderPass)
    {
        if (index < 0 || index >= _passes.Length)
        {
            renderPass = default;
            return false;
        }
        renderPass = _passes[index];
        return true;
    }

    /// <summary>
    /// Supplies XR-provider culling data for the pass. Both eye projections
    /// belong to one culling pass, while URP's single-pass path unions the two
    /// eye frusta before native submission.
    /// </summary>
    public bool GetCullingParameters(Camera camera, int cullingPassIndex,
        out ScriptableCullingParameters cullingParameters)
    {
        cullingParameters = default;
        if (camera is null || cullingPassIndex < 0 || cullingPassIndex >= _passes.Length)
            return false;
        var pass = _passes[cullingPassIndex];
        if (!pass.TryGetRenderParameter(camera, 0, out var leftEye))
            return false;
        cullingParameters = new ScriptableCullingParameters(camera)
        {
            cullingMatrix = leftEye.projection * leftEye.view,
            worldOrigin = leftEye.view.inverse.MultiplyPoint(Vector3.zero),
            cullStereoSeparate = pass.renderParameterCount > 1,
            stereoProjectionMatrix = true
        };
        return true;
    }

    /// <summary>
    /// Binds an overlay camera to this provider's current two-eye frame and
    /// registers it in the base camera's URP stack. URP then renders the
    /// overlay into the same native array layers without a color clear.
    /// </summary>
    public void AttachOverlayCamera(Camera baseCamera, Camera overlayCamera)
    {
        if (baseCamera is null) throw new ArgumentNullException(nameof(baseCamera));
        if (overlayCamera is null) throw new ArgumentNullException(nameof(overlayCamera));
        if (_target is null || !ReferenceEquals(baseCamera.targetTexture, _target) || !_target.IsCreated())
            throw new InvalidOperationException("Configure a stereo frame on the base camera before attaching XR overlays.");
        if (ReferenceEquals(baseCamera, overlayCamera)) throw new ArgumentException("A camera cannot overlay itself.", nameof(overlayCamera));

        overlayCamera.targetTexture = _target;
        overlayCamera.stereoTargetEye = Camera.StereoTargetEyeMask.Both;
        var baseData = baseCamera.GetUniversalAdditionalCameraData();
        var overlayData = overlayCamera.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay;
        if (!baseData.cameraStack.Contains(overlayCamera)) baseData.cameraStack.Add(overlayCamera);
    }

    public bool TryGetDisplayRefreshRate(out float refreshRate)
    {
        refreshRate = running ? 60f : 0f;
        return running;
    }
}

/// <summary>Legacy XR settings facade backed by the active display provider.</summary>
public static class XRSettings
{
    private static readonly XRDisplaySubsystem Display = new();
    public static bool enabled { get; set; }
    public static bool isDeviceActive => enabled && Display.running;
    public static string loadedDeviceName { get; private set; } = "Anity XR Display";
    public static string[] supportedDevices { get; } = { "Anity XR Display" };
    public static float eyeTextureResolutionScale { get => Display.scaleOfAllViewports; set => Display.scaleOfAllViewports = value; }
    public static float renderViewportScale { get => Display.scaleOfAllViewports; set => Display.scaleOfAllViewports = value; }
    public static XRDisplaySubsystem displaySubsystem => Display;

    public static void LoadDeviceByName(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName)) throw new ArgumentException("An XR device name is required.", nameof(deviceName));
        if (!string.Equals(deviceName, supportedDevices[0], StringComparison.Ordinal)) throw new ArgumentException("XR device is not supported.", nameof(deviceName));
        loadedDeviceName = deviceName;
    }

    public static void StartDevice() { enabled = true; Display.Start(); }
    public static void StopDevice() { Display.Stop(); enabled = false; }
}
