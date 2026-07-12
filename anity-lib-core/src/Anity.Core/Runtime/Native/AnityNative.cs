using System;
using System.Runtime.InteropServices;

namespace Anity.Core.Runtime.Native;

/// <summary>
/// Full P/Invoke surface for anity-native (Unity 2022.3 Pro native parity).
/// </summary>
public static class AnityNative
{
    public const string LibraryName = "anity_native";

    public enum Result
    {
        Ok = 0,
        InvalidArg = 1,
        NotSupported = 2,
        OutOfMemory = 3,
        DeviceLost = 4,
        Io = 5,
        Decode = 6,
        Internal = 100
    }

    public enum Platform
    {
        Unknown = 0,
        Windows = 1,
        Linux = 2,
        MacOS = 3,
        iOS = 4,
        Android = 5,
        WebGL = 6
    }

    public enum GraphicsDeviceTypeNative
    {
        Null = 4,
        D3D11 = 2,
        OpenGLES2 = 8,
        OpenGLES3 = 11,
        Metal = 16,
        OpenGLCore = 17,
        D3D12 = 18,
        Vulkan = 21,
        WebGL2 = 28
    }

    static AnityNative()
    {
        try
        {
            var r = Initialize();
            Available = r == Result.Ok || r == Result.Internal;
            if (!Available)
            {
                // still try version probe
                _ = GetApiVersion();
                Available = true;
            }
        }
        catch (DllNotFoundException)
        {
            Available = false;
        }
        catch (EntryPointNotFoundException)
        {
            Available = false;
        }
        catch
        {
            Available = false;
        }
    }

    public static bool Available { get; private set; }

    public static void MarkUnavailable() => Available = false;

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Anity_GetApiVersion")]
    public static extern int GetApiVersion();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Anity_GetPlatform")]
    public static extern Platform GetPlatform();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Anity_Initialize")]
    public static extern Result Initialize();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Anity_Shutdown")]
    public static extern void Shutdown();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Anity_GetVersionString")]
    private static extern IntPtr GetVersionStringPtr();

    public static string GetVersionString()
    {
        if (!Available) return "anity-native unavailable (managed fallback)";
        try
        {
            var p = GetVersionStringPtr();
            return p == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(p) ?? string.Empty;
        }
        catch
        {
            return "anity-native unavailable";
        }
    }

    // --- Graphics device ---
    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsDeviceDesc
    {
        public int preferred; // GraphicsDeviceTypeNative
        public int width;
        public int height;
        public int hdrEnabled;
        public int msaaSamples;
        public int vsync;
        public IntPtr nativeWindow;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CreateDevice")]
    public static extern Result Graphics_CreateDevice(ref GraphicsDeviceDesc desc, out IntPtr device);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_DestroyDevice")]
    public static extern void Graphics_DestroyDevice(IntPtr device);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetDeviceType")]
    public static extern int Graphics_GetDeviceType(IntPtr device);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_BeginFrame")]
    public static extern Result Graphics_BeginFrame(IntPtr device);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_EndFrame")]
    public static extern Result Graphics_EndFrame(IntPtr device);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_Present")]
    public static extern Result Graphics_Present(IntPtr device);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SupportsHDR")]
    public static extern int Graphics_SupportsHDR(IntPtr device);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetDefaultDeviceType")]
    public static extern int Graphics_GetDefaultDeviceType(Platform platform);

    [StructLayout(LayoutKind.Sequential)]
    public struct SwapchainDesc
    {
        public int width;
        public int height;
        public int imageCount;
        public int vsync;
        public int hdr;
        public IntPtr nativeWindow;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CreateSwapchain")]
    public static extern Result Graphics_CreateSwapchain(IntPtr device, ref SwapchainDesc desc, out IntPtr swapchain);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_DestroySwapchain")]
    public static extern void Graphics_DestroySwapchain(IntPtr swapchain);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_AcquireNextImage")]
    public static extern Result Graphics_AcquireNextImage(IntPtr swapchain, out int imageIndex);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_PresentSwapchain")]
    public static extern Result Graphics_PresentSwapchain(IntPtr swapchain);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetSwapchainImageCount")]
    public static extern int Graphics_GetSwapchainImageCount(IntPtr swapchain);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetSwapchainWidth")]
    public static extern int Graphics_GetSwapchainWidth(IntPtr swapchain);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetSwapchainHeight")]
    public static extern int Graphics_GetSwapchainHeight(IntPtr swapchain);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_IsSwapchainHeadless")]
    public static extern int Graphics_IsSwapchainHeadless(IntPtr swapchain);

    // --- HDR ---
    [StructLayout(LayoutKind.Sequential)]
    public struct HDRDisplayInfo
    {
        public int available;
        public int active;
        public int displayColorGamut;
        public int bitsPerColorComponent;
        public float maxFullFrameToneMapLuminance;
        public float maxToneMapLuminance;
        public float minToneMapLuminance;
        public float paperWhiteNits;
        public int automaticHDRTonemapping;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HDRColorGrade
    {
        public float postExposure;
        public float contrast;
        public float saturation;
        public float temperature;
        public float tint;
        public float bloomThreshold;
        public float bloomIntensity;
        public int tonemapMode;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityHDR_QueryDisplay")]
    public static extern Result HDR_QueryDisplay(out HDRDisplayInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityHDR_SetActive")]
    public static extern Result HDR_SetActive(int active);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityHDR_SetPaperWhiteNits")]
    public static extern Result HDR_SetPaperWhiteNits(float nits);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityHDR_SetAutomaticTonemapping")]
    public static extern Result HDR_SetAutomaticTonemapping(int enabled);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityHDR_LinearToGammaSpace")]
    public static extern float HDR_LinearToGammaSpace(float value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityHDR_GammaToLinearSpace")]
    public static extern float HDR_GammaToLinearSpace(float value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityHDR_ProcessFrame")]
    public static extern Result HDR_ProcessFrame(
        float[] rgbaHdr, int width, int height,
        ref HDRColorGrade grade,
        float[] rgbaOut,
        int outHdr10);

    // --- Physics ---
    [StructLayout(LayoutKind.Sequential)]
    public struct Vec3 { public float x, y, z; public Vec3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityPhysics3D_SphereSphereTOI")]
    public static extern int Physics3D_SphereSphereTOI(
        Vec3 posA, float radiusA, Vec3 velA,
        Vec3 posB, float radiusB,
        float deltaTime,
        out float outTOI, out Vec3 outNormal, out Vec3 outPoint);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityPhysics2D_PolygonSAT")]
    public static extern int Physics2D_PolygonSAT(
        float[] polyA, int countA,
        float[] polyB, int countB,
        out float outNx, out float outNy, out float outPenetration);

    // --- Audio ---
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAudio_DecodeFile")]
    public static extern Result Audio_DecodeFile(
        [MarshalAs(UnmanagedType.LPStr)] string path,
        out IntPtr samples, out int sampleCount, out int channels, out int frequency);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAudio_FreeSamples")]
    public static extern void Audio_FreeSamples(IntPtr samples);

    // --- Texture ---
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityTexture_CalculateImageSize")]
    public static extern int Texture_CalculateImageSize(int width, int height, int format);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityTexture_CompressRGBA8")]
    public static extern Result Texture_CompressRGBA8(
        byte[] rgba, int width, int height, int format,
        byte[] outBuffer, int outBufferSize);

    // --- Jobs ---
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityJobs_Initialize")]
    public static extern Result Jobs_Initialize(int workerCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityJobs_GetWorkerCount")]
    public static extern int Jobs_GetWorkerCount();

    public static bool TryQueryHDR(out HDRDisplayInfo info)
    {
        info = default;
        if (!Available) return false;
        try { return HDR_QueryDisplay(out info) == Result.Ok; }
        catch { Available = false; return false; }
    }

    public static bool TrySphereSphereTOI(
        float ax, float ay, float az, float radiusA,
        float vx, float vy, float vz,
        float bx, float by, float bz, float radiusB,
        float dt, out float toi, out float nx, out float ny, out float nz)
    {
        toi = 0; nx = 0; ny = 1; nz = 0;
        if (!Available) return false;
        try
        {
            int hit = Physics3D_SphereSphereTOI(
                new Vec3(ax, ay, az), radiusA, new Vec3(vx, vy, vz),
                new Vec3(bx, by, bz), radiusB, dt,
                out toi, out var n, out _);
            if (hit == 0) return false;
            nx = n.x; ny = n.y; nz = n.z;
            return true;
        }
        catch { Available = false; return false; }
    }

    public static bool TryPolygonSAT(float[] a, float[] b, out float nx, out float ny, out float pen)
    {
        nx = 0; ny = 1; pen = 0;
        if (!Available || a == null || b == null) return false;
        try
        {
            int countA = a.Length / 2;
            int countB = b.Length / 2;
            return Physics2D_PolygonSAT(a, countA, b, countB, out nx, out ny, out pen) != 0;
        }
        catch { Available = false; return false; }
    }
}
