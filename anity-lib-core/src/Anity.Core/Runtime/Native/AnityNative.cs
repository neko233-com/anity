using System;
using System.Runtime.InteropServices;
using System.Text;

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
        Timeout = 7,
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

    [Flags]
    public enum AvatarValidationFlags : uint
    {
        Valid = 0,
        EmptySkeleton = 1u << 0,
        InvalidSkeletonName = 1u << 1,
        DuplicateSkeletonName = 1u << 2,
        InvalidParent = 1u << 3,
        MultipleRoots = 1u << 4,
        HierarchyCycle = 1u << 5,
        InvalidTransform = 1u << 6,
        EmptyHumanMapping = 1u << 7,
        InvalidHumanMapping = 1u << 8,
        DuplicateHumanBone = 1u << 9,
        DuplicateHumanName = 1u << 10,
        MissingMappedBone = 1u << 11,
        MissingRequiredHumanBone = 1u << 12,
        MissingRootMotionTransform = 1u << 13
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AvatarSkeletonBoneDesc
    {
        public ulong nameHash;
        public int parentIndex;
        public float positionX, positionY, positionZ;
        public float rotationX, rotationY, rotationZ, rotationW;
        public float scaleX, scaleY, scaleZ;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AvatarHumanBoneDesc
    {
        public ulong boneNameHash;
        public ulong humanNameHash;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AvatarBuildResult
    {
        public AvatarValidationFlags flags;
        public int rootIndex;
        public int errorIndex;
        public int mappedBoneCount;
    }

    [Flags]
    public enum AnimationPoseFlags : uint
    {
        Position = 1u << 0,
        Rotation = 1u << 1,
        Scale = 1u << 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AnimationTransformPose
    {
        public float positionX, positionY, positionZ;
        public float rotationX, rotationY, rotationZ, rotationW;
        public float scaleX, scaleY, scaleZ;
        public AnimationPoseFlags flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AnimationRootMotionPose
    {
        public float positionX, positionY, positionZ;
        public float rotationX, rotationY, rotationZ, rotationW;
    }

    [Flags]
    public enum AnimationHumanoidRootMotionFlags : uint
    {
        None = 0,
        LockRotation = 1u << 0,
        LockHeightY = 1u << 1,
        LockPositionXZ = 1u << 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AnimationRootMotionDelta
    {
        public float positionX, positionY, positionZ;
        public float rotationX, rotationY, rotationZ, rotationW;
        public float velocityX, velocityY, velocityZ;
        public float angularVelocityX, angularVelocityY, angularVelocityZ;
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

    public enum GraphicsVFXFailurePoint
    {
        InitializeCommand = 1,
        UpdateCommand = 2,
        PlanarCameraCommand = 3,
        DeviceRemoval = 4
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

    private static bool _avatarNativeAvailable = true;

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatar_ValidateHuman")]
    private static extern Result Avatar_ValidateHuman(
        [In] AvatarSkeletonBoneDesc[] skeleton,
        int skeletonCount,
        [In] AvatarHumanBoneDesc[] human,
        int humanCount,
        out AvatarBuildResult result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatar_ValidateGeneric")]
    private static extern Result Avatar_ValidateGeneric(
        [In] AvatarSkeletonBoneDesc[] skeleton,
        int skeletonCount,
        ulong rootMotionTransformNameHash,
        out AvatarBuildResult result);

    public static bool TryValidateHumanAvatar(
        AvatarSkeletonBoneDesc[] skeleton,
        AvatarHumanBoneDesc[] human,
        out AvatarBuildResult result)
    {
        if (skeleton is null) throw new ArgumentNullException(nameof(skeleton));
        if (human is null) throw new ArgumentNullException(nameof(human));
        result = default;
        if (!_avatarNativeAvailable) return false;
        try
        {
            return Avatar_ValidateHuman(skeleton, skeleton.Length, human, human.Length, out result) == Result.Ok;
        }
        catch (DllNotFoundException) { return HandleMissingAvatarNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAvatarNative(); }
    }

    public static bool TryValidateGenericAvatar(
        AvatarSkeletonBoneDesc[] skeleton,
        ulong rootMotionTransformNameHash,
        out AvatarBuildResult result)
    {
        if (skeleton is null) throw new ArgumentNullException(nameof(skeleton));
        result = default;
        if (!_avatarNativeAvailable) return false;
        try
        {
            return Avatar_ValidateGeneric(skeleton, skeleton.Length, rootMotionTransformNameHash, out result) == Result.Ok;
        }
        catch (DllNotFoundException) { return HandleMissingAvatarNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAvatarNative(); }
    }

    private static bool HandleMissingAvatarNative()
    {
        _avatarNativeAvailable = false;
        if (NativeTransformRequired)
            throw new DllNotFoundException("anity-native AvatarBuilder entry points are required but unavailable.");
        return false;
    }

    private static bool _avatarMaskNativeAvailable = true;

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_Create")]
    private static extern Result AvatarMask_Create(out IntPtr mask);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_Destroy")]
    private static extern void AvatarMask_Destroy(IntPtr mask);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_GetHumanoidBodyPartActive")]
    private static extern Result AvatarMask_GetHumanoidBodyPartActive(IntPtr mask, int index, out int active);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_SetHumanoidBodyPartActive")]
    private static extern Result AvatarMask_SetHumanoidBodyPartActive(IntPtr mask, int index, int active);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_GetTransformCount")]
    private static extern Result AvatarMask_GetTransformCount(IntPtr mask, out int count);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_SetTransformCount")]
    private static extern Result AvatarMask_SetTransformCount(IntPtr mask, int count);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_GetTransformActive")]
    private static extern Result AvatarMask_GetTransformActive(IntPtr mask, int index, out int active);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_SetTransformActive")]
    private static extern Result AvatarMask_SetTransformActive(IntPtr mask, int index, int active);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_CopyTransformPath")]
    private static extern Result AvatarMask_CopyTransformPath(
        IntPtr mask, int index, IntPtr buffer, int bufferCapacity, out int requiredBytes);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_SetTransformPath")]
    private static extern Result AvatarMask_SetTransformPath(
        IntPtr mask, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_AddTransformPath")]
    private static extern Result AvatarMask_AddTransformPath(
        IntPtr mask, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_RemoveTransformPath")]
    private static extern Result AvatarMask_RemoveTransformPath(
        IntPtr mask, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int recursive);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAvatarMask_GetTransformPathActive")]
    private static extern Result AvatarMask_GetTransformPathActive(
        IntPtr mask, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, out int active);

    public static bool TryCreateAvatarMask(out IntPtr mask)
    {
        mask = IntPtr.Zero;
        if (!_avatarMaskNativeAvailable) return false;
        try { return AvatarMask_Create(out mask) == Result.Ok && mask != IntPtr.Zero; }
        catch (DllNotFoundException) { return HandleMissingAvatarMaskNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAvatarMaskNative(); }
    }

    public static void DestroyAvatarMask(IntPtr mask)
    {
        if (mask == IntPtr.Zero || !_avatarMaskNativeAvailable) return;
        try { AvatarMask_Destroy(mask); }
        catch (DllNotFoundException) { _avatarMaskNativeAvailable = false; }
        catch (EntryPointNotFoundException) { _avatarMaskNativeAvailable = false; }
    }

    public static bool TryGetAvatarMaskHumanoidBodyPartActive(IntPtr mask, int index, out bool active)
    {
        active = false;
        try
        {
            int value = 0;
            bool success = _avatarMaskNativeAvailable &&
                AvatarMask_GetHumanoidBodyPartActive(mask, index, out value) == Result.Ok;
            active = success && value != 0;
            return success;
        }
        catch (DllNotFoundException) { return HandleMissingAvatarMaskNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAvatarMaskNative(); }
    }

    public static bool TrySetAvatarMaskHumanoidBodyPartActive(IntPtr mask, int index, bool active)
        => TryAvatarMaskCall(() => AvatarMask_SetHumanoidBodyPartActive(mask, index, active ? 1 : 0));

    public static bool TryGetAvatarMaskTransformCount(IntPtr mask, out int count)
    {
        count = 0;
        try
        {
            return _avatarMaskNativeAvailable && AvatarMask_GetTransformCount(mask, out count) == Result.Ok;
        }
        catch (DllNotFoundException) { return HandleMissingAvatarMaskNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAvatarMaskNative(); }
    }

    public static bool TrySetAvatarMaskTransformCount(IntPtr mask, int count)
        => TryAvatarMaskCall(() => AvatarMask_SetTransformCount(mask, count));

    public static bool TryGetAvatarMaskTransformActive(IntPtr mask, int index, out bool active)
    {
        active = false;
        try
        {
            int value = 0;
            bool success = _avatarMaskNativeAvailable &&
                AvatarMask_GetTransformActive(mask, index, out value) == Result.Ok;
            active = success && value != 0;
            return success;
        }
        catch (DllNotFoundException) { return HandleMissingAvatarMaskNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAvatarMaskNative(); }
    }

    public static bool TrySetAvatarMaskTransformActive(IntPtr mask, int index, bool active)
        => TryAvatarMaskCall(() => AvatarMask_SetTransformActive(mask, index, active ? 1 : 0));

    public static bool TryGetAvatarMaskTransformPath(IntPtr mask, int index, out string path)
    {
        path = string.Empty;
        IntPtr buffer = IntPtr.Zero;
        try
        {
            if (!_avatarMaskNativeAvailable ||
                AvatarMask_CopyTransformPath(mask, index, IntPtr.Zero, 0, out int requiredBytes) != Result.Ok ||
                requiredBytes <= 0)
                return false;
            buffer = Marshal.AllocHGlobal(requiredBytes);
            if (AvatarMask_CopyTransformPath(mask, index, buffer, requiredBytes, out int writtenBytes) != Result.Ok ||
                writtenBytes != requiredBytes)
                return false;
            int contentBytes = requiredBytes - 1;
            if (contentBytes == 0) return true;
            var bytes = new byte[contentBytes];
            Marshal.Copy(buffer, bytes, 0, contentBytes);
            path = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch (DllNotFoundException) { return HandleMissingAvatarMaskNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAvatarMaskNative(); }
        finally
        {
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
        }
    }

    public static bool TrySetAvatarMaskTransformPath(IntPtr mask, int index, string path)
        => TryAvatarMaskCall(() => AvatarMask_SetTransformPath(mask, index, path ?? string.Empty));

    public static bool TryAddAvatarMaskTransformPath(IntPtr mask, string path)
        => TryAvatarMaskCall(() => AvatarMask_AddTransformPath(mask, path ?? string.Empty));

    public static bool TryRemoveAvatarMaskTransformPath(IntPtr mask, string path, bool recursive)
        => TryAvatarMaskCall(() => AvatarMask_RemoveTransformPath(mask, path ?? string.Empty, recursive ? 1 : 0));

    public static bool TryGetAvatarMaskTransformPathActive(IntPtr mask, string path, out bool active)
    {
        active = false;
        try
        {
            int value = 0;
            bool success = _avatarMaskNativeAvailable &&
                AvatarMask_GetTransformPathActive(mask, path ?? string.Empty, out value) == Result.Ok;
            active = success && value != 0;
            return success;
        }
        catch (DllNotFoundException) { return HandleMissingAvatarMaskNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAvatarMaskNative(); }
    }

    private static bool TryAvatarMaskCall(Func<Result> call)
    {
        try { return _avatarMaskNativeAvailable && call() == Result.Ok; }
        catch (DllNotFoundException) { return HandleMissingAvatarMaskNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAvatarMaskNative(); }
    }

    private static bool HandleMissingAvatarMaskNative()
    {
        _avatarMaskNativeAvailable = false;
        if (NativeTransformRequired)
            throw new DllNotFoundException("anity-native AvatarMask entry points are required but unavailable.");
        return false;
    }

    private static bool _animationPoseNativeAvailable = true;

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAnimation_BlendTransformPose")]
    private static extern Result Animation_BlendTransformPose(
        in AnimationTransformPose basePose,
        in AnimationTransformPose layerPose,
        IntPtr referencePose,
        float weight,
        int additive,
        out AnimationTransformPose outPose);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityAnimation_BlendTransformPose")]
    private static extern Result Animation_BlendTransformPoseWithReference(
        in AnimationTransformPose basePose,
        in AnimationTransformPose layerPose,
        in AnimationTransformPose referencePose,
        float weight,
        int additive,
        out AnimationTransformPose outPose);

    public static bool TryBlendAnimationTransformPose(
        in AnimationTransformPose basePose,
        in AnimationTransformPose layerPose,
        AnimationTransformPose? referencePose,
        float weight,
        bool additive,
        out AnimationTransformPose result)
    {
        result = default;
        if (!_animationPoseNativeAvailable) return false;
        try
        {
            Result nativeResult;
            if (referencePose.HasValue)
            {
                AnimationTransformPose reference = referencePose.Value;
                nativeResult = Animation_BlendTransformPoseWithReference(
                    in basePose, in layerPose, in reference, weight, additive ? 1 : 0, out result);
            }
            else
            {
                nativeResult = Animation_BlendTransformPose(
                    in basePose, in layerPose, IntPtr.Zero, weight, additive ? 1 : 0, out result);
            }
            return nativeResult == Result.Ok;
        }
        catch (DllNotFoundException) { return HandleMissingAnimationPoseNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAnimationPoseNative(); }
    }

    private static bool HandleMissingAnimationPoseNative()
    {
        _animationPoseNativeAvailable = false;
        if (NativeTransformRequired)
            throw new DllNotFoundException("anity-native animation pose entry points are required but unavailable.");
        return false;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "AnityAnimation_ResolveRootMotion")]
    private static extern Result Animation_ResolveRootMotion(
        in AnimationRootMotionPose startPose,
        in AnimationRootMotionPose endPose,
        in AnimationRootMotionPose samplePose,
        long completedLoops,
        out AnimationRootMotionPose outPose);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "AnityAnimation_PrepareHumanoidRootMotion")]
    private static extern Result Animation_PrepareHumanoidRootMotion(
        in AnimationRootMotionPose referencePose,
        in AnimationRootMotionPose samplePose,
        float humanScale,
        AnimationHumanoidRootMotionFlags flags,
        out AnimationRootMotionPose outPose);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "AnityAnimation_BlendRootMotion")]
    private static extern Result Animation_BlendRootMotion(
        in AnimationRootMotionPose basePose,
        in AnimationRootMotionPose layerPose,
        float weight,
        out AnimationRootMotionPose outPose);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "AnityAnimation_AnchorRootMotion")]
    private static extern Result Animation_AnchorRootMotion(
        in AnimationRootMotionPose anchorPose,
        in AnimationRootMotionPose motionPose,
        out AnimationRootMotionPose outPose);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "AnityAnimation_CalculateRootMotionAnchor")]
    private static extern Result Animation_CalculateRootMotionAnchor(
        in AnimationRootMotionPose worldPose,
        in AnimationRootMotionPose motionPose,
        out AnimationRootMotionPose outAnchorPose);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "AnityAnimation_CalculateRootMotionDelta")]
    private static extern Result Animation_CalculateRootMotionDelta(
        in AnimationRootMotionPose previousPose,
        in AnimationRootMotionPose currentPose,
        float deltaTime,
        out AnimationRootMotionDelta outDelta);

    public static bool TryResolveAnimationRootMotion(
        in AnimationRootMotionPose startPose,
        in AnimationRootMotionPose endPose,
        in AnimationRootMotionPose samplePose,
        long completedLoops,
        out AnimationRootMotionPose result)
    {
        result = default;
        if (!_animationPoseNativeAvailable) return false;
        try
        {
            return Animation_ResolveRootMotion(
                in startPose, in endPose, in samplePose, completedLoops, out result) == Result.Ok;
        }
        catch (DllNotFoundException) { return HandleMissingAnimationPoseNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAnimationPoseNative(); }
    }

    public static bool TryPrepareHumanoidAnimationRootMotion(
        in AnimationRootMotionPose referencePose,
        in AnimationRootMotionPose samplePose,
        float humanScale,
        AnimationHumanoidRootMotionFlags flags,
        out AnimationRootMotionPose result)
    {
        result = default;
        if (!_animationPoseNativeAvailable) return false;
        try
        {
            return Animation_PrepareHumanoidRootMotion(
                in referencePose, in samplePose, humanScale, flags, out result) == Result.Ok;
        }
        catch (DllNotFoundException) { return HandleMissingAnimationPoseNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAnimationPoseNative(); }
    }

    public static bool TryBlendAnimationRootMotion(
        in AnimationRootMotionPose basePose,
        in AnimationRootMotionPose layerPose,
        float weight,
        out AnimationRootMotionPose result)
    {
        result = default;
        if (!_animationPoseNativeAvailable) return false;
        try
        {
            return Animation_BlendRootMotion(in basePose, in layerPose, weight, out result) == Result.Ok;
        }
        catch (DllNotFoundException) { return HandleMissingAnimationPoseNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAnimationPoseNative(); }
    }

    public static bool TryAnchorAnimationRootMotion(
        in AnimationRootMotionPose anchorPose,
        in AnimationRootMotionPose motionPose,
        out AnimationRootMotionPose result)
    {
        result = default;
        if (!_animationPoseNativeAvailable) return false;
        try
        {
            return Animation_AnchorRootMotion(in anchorPose, in motionPose, out result) == Result.Ok;
        }
        catch (DllNotFoundException) { return HandleMissingAnimationPoseNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAnimationPoseNative(); }
    }

    public static bool TryCalculateAnimationRootMotionAnchor(
        in AnimationRootMotionPose worldPose,
        in AnimationRootMotionPose motionPose,
        out AnimationRootMotionPose result)
    {
        result = default;
        if (!_animationPoseNativeAvailable) return false;
        try
        {
            return Animation_CalculateRootMotionAnchor(
                in worldPose, in motionPose, out result) == Result.Ok;
        }
        catch (DllNotFoundException) { return HandleMissingAnimationPoseNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAnimationPoseNative(); }
    }

    public static bool TryCalculateAnimationRootMotionDelta(
        in AnimationRootMotionPose previousPose,
        in AnimationRootMotionPose currentPose,
        float deltaTime,
        out AnimationRootMotionDelta result)
    {
        result = default;
        if (!_animationPoseNativeAvailable) return false;
        try
        {
            return Animation_CalculateRootMotionDelta(
                in previousPose, in currentPose, deltaTime, out result) == Result.Ok;
        }
        catch (DllNotFoundException) { return HandleMissingAnimationPoseNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingAnimationPoseNative(); }
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

    [Flags]
    public enum GraphicsCameraPassFlags : uint
    {
        ClearColor = 1 << 0,
        ClearDepth = 1 << 1,
        StoreColor = 1 << 2,
        StoreDepth = 1 << 3,
        Final = 1 << 4,
        Hdr = 1 << 5,
        TargetIsCameraTarget = 1 << 6
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsCameraPassDesc
    {
        public ulong targetId;
        public int targetWidth;
        public int targetHeight;
        public float viewportX;
        public float viewportY;
        public float viewportWidth;
        public float viewportHeight;
        public float clearR;
        public float clearG;
        public float clearB;
        public float clearA;
        public float clearDepth;
        public int msaaSamples;
        public int depthSlice;
        public int depthSliceCount;
        public GraphicsCameraPassFlags flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsCameraPassInfo
    {
        public ulong frameId;
        public ulong sequence;
        public GraphicsCameraPassDesc desc;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsCameraRenderTargetDesc
    {
        public ulong targetId;
        public int width;
        public int height;
        public int msaaSamples;
        public int hdrEnabled;
        public int dimension;
        public int volumeDepth;
        /// <summary>0=default target, 1=R8G8B8A8 SNorm, 2=R16G16 SFloat.</summary>
        public int colorFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsMeshVertex
    {
        public float px, py, pz;
        public float ppx, ppy, ppz;
        public float nx, ny, nz;
        public float tx, ty, tz, tw;
        public float u, v;
        public float r, g, b, a;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsCameraMeshDrawDesc
    {
        public ulong targetId;
        public int targetIsCameraTarget;
        public int blendMode;
        public int depthWriteEnabled;
        public int writeMotionVectors;
        public int depthSlice;
        public int alphaClipEnabled;
        public float alphaClipThreshold;
        public ulong baseTextureId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] baseMapST;
        public ulong normalMapTextureId;
        public IntPtr vertices;
        public int vertexCount;
        public IntPtr indices;
        public int indexCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public float[] objectToClip;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public float[] motionObjectToClip;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public float[] previousObjectToClip;
        public int hasPreviousObjectToClip;
        public int stereoInstanceCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public float[] stereoObjectToClip;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public float[] stereoMotionObjectToClip;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public float[] stereoPreviousObjectToClip;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsSkinVertex
    {
        public float px, py, pz;
        public float nx, ny, nz;
        public float tx, ty, tz, tw;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsBlendShapeDesc
    {
        public IntPtr sourceVertices;
        public IntPtr shapeDeltas;
        public int vertexCount;
        public int shapeCount;
        public IntPtr outVertices;
        public int outVertexCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsBoneWeight
    {
        public float w0, w1, w2, w3;
        public int i0, i1, i2, i3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsBoneWeight1
    {
        public float weight;
        public int boneIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsSkinMeshDesc
    {
        public IntPtr sourceVertices;
        public IntPtr boneWeights;
        public int vertexCount;
        public IntPtr boneMatrices;
        public int boneCount;
        public IntPtr outVertices;
        public int outVertexCount;
        public IntPtr bonesPerVertex;
        public IntPtr allBoneWeights;
        public int allBoneWeightCount;
        public int maxInfluences;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsUIUploadStats
    {
        public ulong frameId;
        public ulong uploadGeneration;
        public int submitted;
        public int batchCount;
        public int drawCount;
        public int vertexCount;
        public int indexCount;
        public int vertexBytes;
        public int indexBytes;
        public int ringIndex;
        /// <summary>0=CPU/null, 1=Vulkan, 2=Metal, 3=D3D11.</summary>
        public int backendKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsTextureDesc
    {
        public ulong textureId;
        public ulong revision;
        public int width;
        public int height;
        public int mipCount;
        public int filterMode;
        public int wrapU;
        public int wrapV;
        public int linear;
        public float mipMapBias;
        public int anisoLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsTextureInfo
    {
        public GraphicsTextureDesc desc;
        public int byteCount;
        public ulong uploadGeneration;
        public int backendKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXEventUploadDesc
    {
        public ulong effectId;
        public ulong sequence;
        public int eventNameId;
        public int recordCount;
        public int strideBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXEventUploadInfo
    {
        public GraphicsVFXEventUploadDesc desc;
        public int byteCount;
        public ulong uploadGeneration;
        public int backendKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXEventDispatchPlanInfo
    {
        public ulong effectId;
        public ulong firstSequence;
        public ulong lastSequence;
        public int batchCount;
        public int recordCount;
        public int strideBytes;
        public int byteCount;
        public ulong uploadGeneration;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXEventDispatchBatch
    {
        public ulong sequence;
        public int eventNameId;
        public int startEventIndex;
        public int recordCount;
        public int strideBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXInitializeDispatchDesc
    {
        public ulong effectId;
        public ulong sequence;
        public long initializeContextId;
        public long sourceSpawnerContextId;
        public int eventNameId;
        public int particleSystemId;
        public int spawnSystemId;
        public int startEventIndex;
        public int recordCount;
        public int strideBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXInitializeDispatchInfo
    {
        public GraphicsVFXInitializeDispatchDesc desc;
        public int sourceByteCount;
        public int outputByteCount;
        public ulong dispatchGeneration;
        public int backendKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXInitializeKernelDesc
    {
        public uint version;
        public uint flags;
        public int particleCapacity;
        public int attributeStrideBytes;
        public int sourceStrideBytes;
        public int attributeStart;
        public int attributeCount;
        public int operationStart;
        public int operationCount;
        public int spawnCountSourceOffsetBytes;
        public uint systemSeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXInitializeAttributeDesc
    {
        public int offsetBytes;
        public int componentCount;
        public int valueType;
        public int semantic;
        public uint default0, default1, default2, default3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXInitializeOperationDesc
    {
        public int targetOffsetBytes;
        public int sourceOffsetBytes;
        public int componentCount;
        public int valueType;
        public int valueSource;
        public int composition;
        public int randomMode;
        public int reserved;
        public uint valueA0, valueA1, valueA2, valueA3;
        public uint valueB0, valueB1, valueB2, valueB3;
        public uint blendFactorBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXParticleSystemInfo
    {
        public ulong effectId;
        public int particleSystemId;
        public int capacity;
        public int attributeStrideBytes;
        public int aliveCount;
        public int deadCount;
        public int backendKind;
        public ulong generation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXUpdateKernelDesc
    {
        public uint version;
        public uint flags;
        public ulong effectId;
        public long contextId;
        public int particleSystemId;
        public int particleCapacity;
        public int attributeStrideBytes;
        public int operationStart;
        public int operationCount;
        public int aliveOffsetBytes;
        public int seedOffsetBytes;
        public float deltaTime;
        public uint systemSeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXUpdateOperationDesc
    {
        public int kind;
        public int targetOffsetBytes;
        public int sourceAOffsetBytes;
        public int sourceBOffsetBytes;
        public int auxiliaryOffset0Bytes;
        public int auxiliaryOffset1Bytes;
        public int componentCount;
        public int valueType;
        public int composition;
        public int randomMode;
        public int flags;
        public uint valueA0, valueA1, valueA2, valueA3;
        public uint valueB0, valueB1, valueB2, valueB3;
        public uint blendFactorBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXUpdateBackendStats
    {
        public ulong effectId;
        public int particleSystemId;
        public int backendKind;
        public ulong residentGeneration;
        public ulong dispatchCount;
        public ulong particleUploadCount;
        public ulong operationUploadCount;
        public ulong gpuCopyCount;
        public ulong completionCount;
        public int ringIndex;
        public int ringSize;
        public ulong particleBufferCapacityBytes;
        public ulong operationBufferCapacityBytes;
        public ulong synchronousReadbackCount;
        public int lastBatchWidth;
        public int peakBatchWidth;
        public ulong asyncBatchCount;
        public ulong boundsDispatchCount;
        public ulong boundsResidentHitCount;
        public ulong boundsParticleUploadCount;
        public ulong boundsCompletionCount;
        public ulong boundsResultCacheHitCount;
        public ulong boundsPendingDispatchCount;
        public ulong boundsPendingPublishCount;
        public ulong boundsPendingDiscardCount;
        public ulong deadPrefixPassCount;
        public ulong deadCompactionDispatchCount;
        public ulong residentOnlyPublishCount;
        public ulong deferredParticleReadbackCount;
        public ulong deferredParticleReadbackBytes;
        public ulong residentSnapshotCount;
        public ulong residentRestoreCount;
        public ulong residentSnapshotDiscardCount;
        public ulong asynchronousResidentPublishCount;
        public ulong asynchronousResidentCompletionCount;
        public ulong asynchronousResidentRollbackCount;
        public ulong completionWaitCount;
        public ulong cameraDependencyCount;
        public ulong pendingUpdateCount;
        public ulong preparationPollCount;
        public ulong preparationDeferredCount;
        public ulong preparationRetiredCount;
        public ulong allocationStateGeneration;
        public ulong allocationStateUploadCount;
        public ulong allocationStateGpuCopyCount;
        public ulong allocationStateResidentHitCount;
        public ulong residentInitializeCount;
        public ulong residentInitializeSpawnCount;
        public ulong residentInitializeReadbackAvoidedBytes;
        public ulong residentInitializeAllocationStateReadCount;
        public ulong allocationStateReadbackCount;
        public ulong deadListReadbackCount;
        public ulong metadataReadbackBytes;
        public ulong metadataReadbackGeneration;
        public ulong residentInitializeIndirectDispatchCount;
        public ulong residentInitializeIndirectPreparationCount;
        public ulong residentInitializeSourceStateGpuCopyCount;
        public ulong initializeCpuDispatchSizingCount;
        public ulong residentInitializeTargetCopyCount;
        public ulong residentInitializeTargetCopyBytes;
        public ulong residentInitializeAtomicPublishCount;
        public ulong asynchronousInitializeBeginCount;
        public ulong asynchronousInitializePollCount;
        public ulong asynchronousInitializeCompletionCount;
        public ulong asynchronousInitializeCancelCount;
        public ulong asynchronousInitializeResidentPublishCount;
        public ulong asynchronousInitializeResidentCompletionCount;
        public ulong asynchronousInitializeResidentRollbackCount;
        public ulong pendingInitializeCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXUpdateTicketInfo
    {
        public ulong ticketId;
        public ulong effectId;
        public uint frameIndex;
        public int state;
        public int kernelCount;
        public int backendKind;
        public ulong preparedFrameGeneration;
        public ulong submitGeneration;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXInitializeTicketInfo
    {
        public ulong ticketId;
        public ulong effectId;
        public int state;
        public int dispatchCount;
        public int backendKind;
        public int effectCount;
        public ulong sourceRegistryGeneration;
        public ulong targetRegistryGeneration;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXBoundsReductionDesc
    {
        public ulong effectId;
        public int particleSystemId;
        public int positionOffsetBytes;
        public int aliveOffsetBytes;
        public int sizeOffsetBytes;
        public int scaleXOffsetBytes;
        public int scaleYOffsetBytes;
        public int scaleZOffsetBytes;
        public float paddingX, paddingY, paddingZ;
        public int boundsInWorldSpace;
        public int reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXBoundsReductionResult
    {
        public ulong effectId;
        public int particleSystemId;
        public int valid;
        public float centerX, centerY, centerZ;
        public float extentsX, extentsY, extentsZ;
        public int backendKind;
        public int boundsInWorldSpace;
        public ulong generation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXFrameState
    {
        public ulong effectId;
        public uint frameIndex;
        public uint stepCount;
        public float gameDeltaTime;
        public float unscaledDeltaTime;
        public float deltaTime;
        public float totalTime;
        public float accumulator;
        public uint prepared;
        public ulong generation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXCullingBounds
    {
        public ulong effectId;
        public float centerX, centerY, centerZ;
        public float extentsX, extentsY, extentsZ;
        public int layer;
        public int valid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXCullingCamera
    {
        public ulong cameraId;
        public float m00, m01, m02, m03;
        public float m10, m11, m12, m13;
        public float m20, m21, m22, m23;
        public float m30, m31, m32, m33;
        public int cullingMask;
        public int cameraType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXCullingState
    {
        public ulong effectId;
        public ulong playerLoopToken;
        public int culled;
        public int hasBounds;
        public int cameraCount;
        public int visibleCameraCount;
        public ulong generation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXPlanarOutputDesc
    {
        public uint version;
        public uint flags;
        public ulong effectId;
        public long contextId;
        public int particleSystemId;
        public int primitiveType;
        public int particleCapacity;
        public int attributeStrideBytes;
        public int aliveOffsetBytes;
        public int positionOffsetBytes;
        public int colorOffsetBytes;
        public int alphaOffsetBytes;
        public int axisXOffsetBytes;
        public int axisYOffsetBytes;
        public int axisZOffsetBytes;
        public int angleXOffsetBytes;
        public int angleYOffsetBytes;
        public int angleZOffsetBytes;
        public int pivotXOffsetBytes;
        public int pivotYOffsetBytes;
        public int pivotZOffsetBytes;
        public int sizeOffsetBytes;
        public int scaleXOffsetBytes;
        public int scaleYOffsetBytes;
        public int scaleZOffsetBytes;
        public int uvMode;
        public int blendMode;
        public int cullMode;
        public int zWrite;
        public int zTest;
        public int renderQueue;
        public int reserved0;
        public int reserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXPlanarCameraDesc
    {
        public ulong cameraId;
        public float localToWorld00, localToWorld01, localToWorld02, localToWorld03;
        public float localToWorld10, localToWorld11, localToWorld12, localToWorld13;
        public float localToWorld20, localToWorld21, localToWorld22, localToWorld23;
        public float localToWorld30, localToWorld31, localToWorld32, localToWorld33;
        public float worldToClip00, worldToClip01, worldToClip02, worldToClip03;
        public float worldToClip10, worldToClip11, worldToClip12, worldToClip13;
        public float worldToClip20, worldToClip21, worldToClip22, worldToClip23;
        public float worldToClip30, worldToClip31, worldToClip32, worldToClip33;
        public int cullingMask;
        public int cameraType;
        public int flags;
        public int reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXPlanarDrawInfo
    {
        public ulong effectId;
        public ulong cameraId;
        public ulong residentGeneration;
        public int outputCount;
        public int drawCount;
        public int skippedOutputCount;
        public int particleCount;
        public int vertexCount;
        public int backendKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXPlanarEffectDesc
    {
        public ulong effectId;
        public float localToWorld00, localToWorld01, localToWorld02, localToWorld03;
        public float localToWorld10, localToWorld11, localToWorld12, localToWorld13;
        public float localToWorld20, localToWorld21, localToWorld22, localToWorld23;
        public float localToWorld30, localToWorld31, localToWorld32, localToWorld33;
        public int layer;
        public int sortOrder;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXPlanarCameraBatchDesc
    {
        public ulong cameraId;
        public float worldToClip00, worldToClip01, worldToClip02, worldToClip03;
        public float worldToClip10, worldToClip11, worldToClip12, worldToClip13;
        public float worldToClip20, worldToClip21, worldToClip22, worldToClip23;
        public float worldToClip30, worldToClip31, worldToClip32, worldToClip33;
        public int cullingMask;
        public int cameraType;
        public int flags;
        public int reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXPlanarCameraDrawInfo
    {
        public ulong cameraId;
        public ulong residentGeneration;
        public ulong submissionGeneration;
        public int effectCount;
        public int outputCount;
        public int drawCount;
        public int skippedOutputCount;
        public int particleCount;
        public int vertexCount;
        public int backendKind;
        public int commandBufferCount;
        public int renderPassCount;
        public int reserved;
        public int aliveCompactionCount;
        public int aliveCompactionCacheHitCount;
        public int alivePrefixPassCount;
        public int indirectArgumentCount;
        public ulong capacityVertexCount;
        public int depthTestOutputCount;
        public int depthWriteOutputCount;
        public int depthStateChangeCount;
        public int depthClearCount;
        public int sortedOutputCount;
        public int sortCacheHitCount;
        public int sortMapDispatchCount;
        public int sortStageDispatchCount;
        public int sortExtractDispatchCount;
        public int sortPaddedParticleCount;
        public int sortCacheInsertCount;
        public int sortCacheEvictionCount;
        public int sortCacheEntryCount;
        public int sortCacheCapacityPerSystem;
        public ulong submissionId;
        public int asyncSubmissionCount;
        public int synchronousWaitCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXPlanarSubmissionStats
    {
        public ulong submissionCount;
        public ulong completionCount;
        public ulong failureCount;
        public ulong lastSubmittedId;
        public ulong lastCompletedId;
        public ulong lastFailedId;
        public ulong waitCount;
        public int inFlightCount;
        public int maxInFlightCount;
        public int backendKind;
        public int deviceLost;
        public ulong resultEvictionCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXSpawnerProgramDesc
    {
        public uint version;
        public uint eventStrideWords;
        public ulong effectId;
        public long contextId;
        public int systemId;
        public int blockStart;
        public int blockCount;
        public int loopDurationMode;
        public int loopCountMode;
        public int delayBeforeLoopMode;
        public int delayAfterLoopMode;
        public float loopDurationMin;
        public float loopDurationMax;
        public double loopCountMin;
        public double loopCountMax;
        public float delayBeforeLoopMin;
        public float delayBeforeLoopMax;
        public float delayAfterLoopMin;
        public float delayAfterLoopMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXSpawnerBlockDesc
    {
        public long blockId;
        public int kind;
        public int periodic;
        public float valueMin;
        public float valueMax;
        public float periodMin;
        public float periodMax;
        public int targetOffsetWords;
        public int valueType;
        public int randomMode;
        public int valueWordCount;
        public uint valueA0;
        public uint valueA1;
        public uint valueA2;
        public uint valueA3;
        public uint valueB0;
        public uint valueB1;
        public uint valueB2;
        public uint valueB3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsVFXSpawnerState
    {
        public ulong effectId;
        public long contextId;
        public int systemId;
        public int loopState;
        public int playing;
        public int newLoop;
        public float spawnCount;
        public float deltaTime;
        public float totalTime;
        public float delayBeforeLoop;
        public float loopDuration;
        public float delayAfterLoop;
        public int loopIndex;
        public int loopCount;
        public float eventSpawnCount;
        public ulong generation;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Result GraphicsVFXSpawnerCallback(
        IntPtr userData, long blockId, int phase,
        ref GraphicsVFXSpawnerState state, IntPtr eventRecord,
        int eventRecordByteCount);

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

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_RecordCameraPass")]
    public static extern Result Graphics_RecordCameraPass(
        IntPtr device, ref GraphicsCameraPassDesc desc, out GraphicsCameraPassInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetLastCameraPass")]
    public static extern Result Graphics_GetLastCameraPass(
        IntPtr device, out GraphicsCameraPassInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_EnsureCameraRenderTarget")]
    public static extern Result Graphics_EnsureCameraRenderTarget(
        IntPtr device, ref GraphicsCameraRenderTargetDesc desc);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_DestroyCameraRenderTarget")]
    public static extern Result Graphics_DestroyCameraRenderTarget(IntPtr device, ulong targetId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ReadbackCameraRenderTargetRGBA8")]
    public static extern Result Graphics_ReadbackCameraRenderTargetRGBA8(
        IntPtr device, ulong targetId, [Out] byte[] pixels, int pixelCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ReadbackCameraRenderTargetSliceRGBA8")]
    public static extern Result Graphics_ReadbackCameraRenderTargetSliceRGBA8(
        IntPtr device, ulong targetId, int depthSlice, [Out] byte[] pixels, int pixelCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ReadbackCameraRenderTargetToneMappedRGBA8")]
    public static extern Result Graphics_ReadbackCameraRenderTargetToneMappedRGBA8(
        IntPtr device, ulong targetId, [Out] byte[] pixels, int pixelCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ReadbackCameraRenderTargetToneMappedSliceRGBA8")]
    public static extern Result Graphics_ReadbackCameraRenderTargetToneMappedSliceRGBA8(
        IntPtr device, ulong targetId, int depthSlice, [Out] byte[] pixels, int pixelCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CopyCameraRenderTargetColor")]
    public static extern Result Graphics_CopyCameraRenderTargetColor(
        IntPtr device, ulong sourceTargetId, int sourceIsCameraTarget, ulong destinationTargetId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CopyCameraRenderTargetColorSlice")]
    public static extern Result Graphics_CopyCameraRenderTargetColorSlice(
        IntPtr device, ulong sourceTargetId, int sourceIsCameraTarget, int sourceSlice, int destinationSlice,
        ulong destinationTargetId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CopyCameraRenderTargetDepthToColor")]
    public static extern Result Graphics_CopyCameraRenderTargetDepthToColor(
        IntPtr device, ulong sourceTargetId, int sourceIsCameraTarget, ulong destinationTargetId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CopyCameraRenderTargetDepthToColorSlice")]
    public static extern Result Graphics_CopyCameraRenderTargetDepthToColorSlice(
        IntPtr device, ulong sourceTargetId, int sourceIsCameraTarget, int sourceSlice, int destinationSlice,
        ulong destinationTargetId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CopyCameraRenderTargetNormalsToColor")]
    public static extern Result Graphics_CopyCameraRenderTargetNormalsToColor(
        IntPtr device, ulong sourceTargetId, int sourceIsCameraTarget, ulong destinationTargetId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CopyCameraRenderTargetNormalsToColorSlice")]
    public static extern Result Graphics_CopyCameraRenderTargetNormalsToColorSlice(
        IntPtr device, ulong sourceTargetId, int sourceIsCameraTarget, int sourceSlice, int destinationSlice,
        ulong destinationTargetId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CopyCameraRenderTargetMotionToColor")]
    public static extern Result Graphics_CopyCameraRenderTargetMotionToColor(
        IntPtr device, ulong sourceTargetId, int sourceIsCameraTarget, ulong destinationTargetId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CopyCameraRenderTargetMotionToColorSlice")]
    public static extern Result Graphics_CopyCameraRenderTargetMotionToColorSlice(
        IntPtr device, ulong sourceTargetId, int sourceIsCameraTarget, int sourceSlice, int destinationSlice,
        ulong destinationTargetId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_DrawCameraMesh")]
    public static extern Result Graphics_DrawCameraMesh(
        IntPtr device, ref GraphicsCameraMeshDrawDesc desc);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SkinMeshVertices")]
    public static extern Result Graphics_SkinMeshVertices(ref GraphicsSkinMeshDesc desc);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ApplyBlendShapeDeltas")]
    public static extern Result Graphics_ApplyBlendShapeDeltas(ref GraphicsBlendShapeDesc desc);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ProcessCameraRenderTargetHDR")]
    public static extern Result Graphics_ProcessCameraRenderTargetHDR(
        IntPtr device, ulong targetId, ref HDRColorGrade grade);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetHDRPostProcessStats")]
    public static extern Result Graphics_GetHDRPostProcessStats(
        IntPtr device, out GraphicsHDRPostProcessStats stats);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SetUICanvas")]
    public static extern Result Graphics_SetUICanvas(IntPtr device, IntPtr canvas);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SubmitUICanvas")]
    public static extern Result Graphics_SubmitUICanvas(IntPtr device, IntPtr canvas);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetUIUploadStats")]
    public static extern Result Graphics_GetUIUploadStats(IntPtr device, out GraphicsUIUploadStats stats);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_UploadTextureRGBA8")]
    public static extern Result Graphics_UploadTextureRGBA8(
        IntPtr device, ref GraphicsTextureDesc desc,
        byte[] pixels, int byteCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_DestroyTexture")]
    public static extern Result Graphics_DestroyTexture(IntPtr device, ulong textureId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetTextureInfo")]
    public static extern Result Graphics_GetTextureInfo(
        IntPtr device, ulong textureId, out GraphicsTextureInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetTextureNativeHandle")]
    public static extern IntPtr Graphics_GetTextureNativeHandle(IntPtr device, ulong textureId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_UploadVFXEventRecords")]
    public static extern Result Graphics_UploadVFXEventRecords(
        IntPtr device, ref GraphicsVFXEventUploadDesc desc,
        byte[] records, int byteCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXEventUploadInfo")]
    public static extern Result Graphics_GetVFXEventUploadInfo(
        IntPtr device, ulong effectId, out GraphicsVFXEventUploadInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ReadbackVFXEventRecords")]
    public static extern Result Graphics_ReadbackVFXEventRecords(
        IntPtr device, ulong effectId, [Out] byte[] records,
        int recordCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXEventDispatchPlanInfo")]
    public static extern Result Graphics_GetVFXEventDispatchPlanInfo(
        IntPtr device, ulong effectId, out GraphicsVFXEventDispatchPlanInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CopyVFXEventDispatchBatches")]
    public static extern Result Graphics_CopyVFXEventDispatchBatches(
        IntPtr device, ulong effectId, ulong throughSequence,
        [Out] GraphicsVFXEventDispatchBatch[] batches, int batchCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CopyVFXEventDispatchRecords")]
    public static extern Result Graphics_CopyVFXEventDispatchRecords(
        IntPtr device, ulong effectId, ulong throughSequence,
        [Out] byte[] records, int recordCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ConsumeVFXEventDispatchPlan")]
    public static extern Result Graphics_ConsumeVFXEventDispatchPlan(
        IntPtr device, ulong effectId, ulong throughSequence);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SubmitVFXInitializeDispatch")]
    public static extern Result Graphics_SubmitVFXInitializeDispatch(
        IntPtr device, ref GraphicsVFXInitializeDispatchDesc desc,
        byte[] sourceRecords, int sourceByteCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SubmitVFXInitializeDispatches")]
    public static extern Result Graphics_SubmitVFXInitializeDispatches(
        IntPtr device, [In] GraphicsVFXInitializeDispatchDesc[] descs,
        int descCount, byte[] sourceRecords, int sourceByteCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SubmitVFXInitializeKernels")]
    public static extern Result Graphics_SubmitVFXInitializeKernels(
        IntPtr device,
        [In] GraphicsVFXInitializeDispatchDesc[] dispatches,
        [In] GraphicsVFXInitializeKernelDesc[] kernels,
        int dispatchCount,
        [In] GraphicsVFXInitializeAttributeDesc[] attributes,
        int attributeCount,
        [In] GraphicsVFXInitializeOperationDesc[] operations,
        int operationCount,
        byte[] sourceRecords,
        int sourceByteCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_BeginVFXInitializeKernels")]
    public static extern Result Graphics_BeginVFXInitializeKernels(
        IntPtr device,
        [In] GraphicsVFXInitializeDispatchDesc[] dispatches,
        [In] GraphicsVFXInitializeKernelDesc[] kernels,
        int dispatchCount,
        [In] GraphicsVFXInitializeAttributeDesc[] attributes,
        int attributeCount,
        [In] GraphicsVFXInitializeOperationDesc[] operations,
        int operationCount,
        byte[] sourceRecords,
        int sourceByteCount,
        out ulong ticketId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXInitializeTicketInfo")]
    public static extern Result Graphics_GetVFXInitializeTicketInfo(
        IntPtr device, ulong ticketId,
        out GraphicsVFXInitializeTicketInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CompleteVFXInitializeKernels")]
    public static extern Result Graphics_CompleteVFXInitializeKernels(
        IntPtr device, ulong ticketId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CancelVFXInitializeKernels")]
    public static extern Result Graphics_CancelVFXInitializeKernels(
        IntPtr device, ulong ticketId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXInitializeDispatchInfo")]
    public static extern Result Graphics_GetVFXInitializeDispatchInfo(
        IntPtr device, ulong effectId, long initializeContextId,
        out GraphicsVFXInitializeDispatchInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ReadbackVFXInitializeDispatch")]
    public static extern Result Graphics_ReadbackVFXInitializeDispatch(
        IntPtr device, ulong effectId, long initializeContextId,
        [Out] byte[] records, int recordCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXParticleSystemInfo")]
    public static extern Result Graphics_GetVFXParticleSystemInfo(
        IntPtr device, ulong effectId, int particleSystemId,
        out GraphicsVFXParticleSystemInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ReadbackVFXParticleSystem")]
    public static extern Result Graphics_ReadbackVFXParticleSystem(
        IntPtr device, ulong effectId, int particleSystemId,
        [Out] byte[] records, int recordCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ReadbackVFXParticleDeadList")]
    public static extern Result Graphics_ReadbackVFXParticleDeadList(
        IntPtr device, ulong effectId, int particleSystemId,
        [Out] uint[] indices, int indexCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_DispatchVFXUpdateKernels")]
    public static extern Result Graphics_DispatchVFXUpdateKernels(
        IntPtr device,
        [In] GraphicsVFXUpdateKernelDesc[] kernels, int kernelCount,
        [In] GraphicsVFXUpdateOperationDesc[] operations, int operationCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_BeginVFXUpdateKernels")]
    public static extern Result Graphics_BeginVFXUpdateKernels(
        IntPtr device,
        [In] GraphicsVFXUpdateKernelDesc[] kernels, int kernelCount,
        [In] GraphicsVFXUpdateOperationDesc[] operations, int operationCount,
        out ulong ticketId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_BeginVFXUpdateKernelsWithBounds")]
    public static extern Result Graphics_BeginVFXUpdateKernelsWithBounds(
        IntPtr device,
        [In] GraphicsVFXUpdateKernelDesc[] kernels, int kernelCount,
        [In] GraphicsVFXUpdateOperationDesc[] operations, int operationCount,
        [In] GraphicsVFXBoundsReductionDesc[] boundsDescs, int boundsCount,
        out ulong ticketId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXUpdateTicketInfo")]
    public static extern Result Graphics_GetVFXUpdateTicketInfo(
        IntPtr device, ulong ticketId, out GraphicsVFXUpdateTicketInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CompleteVFXUpdateKernels")]
    public static extern Result Graphics_CompleteVFXUpdateKernels(
        IntPtr device, ulong ticketId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CancelVFXUpdateKernels")]
    public static extern Result Graphics_CancelVFXUpdateKernels(
        IntPtr device, ulong ticketId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXUpdateBackendStats")]
    public static extern Result Graphics_GetVFXUpdateBackendStats(
        IntPtr device, ulong effectId, int particleSystemId,
        out GraphicsVFXUpdateBackendStats stats);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SetVFXFailureInjection")]
    public static extern Result Graphics_SetVFXFailureInjection(
        IntPtr device, GraphicsVFXFailurePoint failurePoint, int failureCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ReduceVFXParticleBounds")]
    public static extern Result Graphics_ReduceVFXParticleBounds(
        IntPtr device, ref GraphicsVFXBoundsReductionDesc desc,
        out GraphicsVFXBoundsReductionResult result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_BeginVFXFrame")]
    public static extern Result Graphics_BeginVFXFrame(
        IntPtr device, out uint frameIndex);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_BeginVFXPlayerLoopFrame")]
    public static extern Result Graphics_BeginVFXPlayerLoopFrame(
        IntPtr device, ulong playerLoopToken, out uint frameIndex,
        out int beganFrame);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_BeginVFXCullingFrame")]
    public static extern Result Graphics_BeginVFXCullingFrame(
        IntPtr device, ulong playerLoopToken,
        [In] GraphicsVFXCullingBounds[] bounds, int boundsCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SubmitVFXCullingCamera")]
    public static extern Result Graphics_SubmitVFXCullingCamera(
        IntPtr device, ulong playerLoopToken,
        ref GraphicsVFXCullingCamera camera);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CompleteVFXCullingFrame")]
    public static extern Result Graphics_CompleteVFXCullingFrame(
        IntPtr device, ulong playerLoopToken);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXCullingState")]
    public static extern Result Graphics_GetVFXCullingState(
        IntPtr device, ulong effectId, out GraphicsVFXCullingState state);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SetVFXPlanarOutputs")]
    public static extern Result Graphics_SetVFXPlanarOutputs(
        IntPtr device, ulong effectId,
        [In] GraphicsVFXPlanarOutputDesc[] outputs, int outputCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXPlanarOutputCount")]
    public static extern Result Graphics_GetVFXPlanarOutputCount(
        IntPtr device, ulong effectId, out int outputCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_DrawVFXPlanarOutputs")]
    public static extern Result Graphics_DrawVFXPlanarOutputs(
        IntPtr device, ulong effectId,
        ref GraphicsVFXPlanarCameraDesc camera,
        out GraphicsVFXPlanarDrawInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_DrawVFXPlanarCamera")]
    public static extern Result Graphics_DrawVFXPlanarCamera(
        IntPtr device,
        ref GraphicsVFXPlanarCameraBatchDesc camera,
        [In] GraphicsVFXPlanarEffectDesc[] effects, int effectCount,
        out GraphicsVFXPlanarCameraDrawInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXPlanarSubmissionStats")]
    public static extern Result Graphics_GetVFXPlanarSubmissionStats(
        IntPtr device, out GraphicsVFXPlanarSubmissionStats stats);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_WaitForVFXPlanarSubmissions")]
    public static extern Result Graphics_WaitForVFXPlanarSubmissions(
        IntPtr device, ulong throughSubmissionId, int timeoutMilliseconds);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_PrepareVFXEffectFrame")]
    public static extern Result Graphics_PrepareVFXEffectFrame(
        IntPtr device, ulong effectId, uint frameIndex,
        float gameDeltaTime, float playRate, float fixedTimeStep,
        float maxDeltaTime, int paused, out GraphicsVFXFrameState state);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_PrepareVFXEffectManualFrame")]
    public static extern Result Graphics_PrepareVFXEffectManualFrame(
        IntPtr device, ulong effectId, uint frameIndex,
        float stepDeltaTime, out GraphicsVFXFrameState state);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_CommitVFXEffectFrame")]
    public static extern Result Graphics_CommitVFXEffectFrame(
        IntPtr device, ulong effectId, uint frameIndex,
        out GraphicsVFXFrameState state);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_AbortVFXEffectFrame")]
    public static extern Result Graphics_AbortVFXEffectFrame(
        IntPtr device, ulong effectId, uint frameIndex);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXEffectFrameState")]
    public static extern Result Graphics_GetVFXEffectFrameState(
        IntPtr device, ulong effectId, out GraphicsVFXFrameState state);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ResetVFXEffectFrameState")]
    public static extern Result Graphics_ResetVFXEffectFrameState(
        IntPtr device, ulong effectId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SetVFXSpawnerPrograms")]
    public static extern Result Graphics_SetVFXSpawnerPrograms(
        IntPtr device, ulong effectId,
        [In] GraphicsVFXSpawnerProgramDesc[] programs, int programCount,
        [In] GraphicsVFXSpawnerBlockDesc[] blocks, int blockCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SetVFXSpawnerEventRecordDefaults")]
    public static extern Result Graphics_SetVFXSpawnerEventRecordDefaults(
        IntPtr device, ulong effectId, long contextId,
        [In] byte[] record, int recordByteCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ControlVFXSpawner")]
    public static extern Result Graphics_ControlVFXSpawner(
        IntPtr device, ulong effectId, long contextId,
        int play, uint seed, int resetSeed);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ControlVFXSpawnerWithCallbacks")]
    public static extern Result Graphics_ControlVFXSpawnerWithCallbacks(
        IntPtr device, ulong effectId, long contextId,
        int play, uint seed, int resetSeed,
        GraphicsVFXSpawnerCallback callback, IntPtr userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_TickVFXSpawners")]
    public static extern Result Graphics_TickVFXSpawners(
        IntPtr device, ulong effectId, float deltaTime,
        [Out] GraphicsVFXSpawnerState[] states, int stateCapacity,
        out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_TickVFXSpawnersWithCallbacks")]
    public static extern Result Graphics_TickVFXSpawnersWithCallbacks(
        IntPtr device, ulong effectId, float deltaTime,
        [Out] GraphicsVFXSpawnerState[] states, int stateCapacity,
        out int written, GraphicsVFXSpawnerCallback callback,
        IntPtr userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXSpawnerState")]
    public static extern Result Graphics_GetVFXSpawnerState(
        IntPtr device, ulong effectId, long contextId,
        out GraphicsVFXSpawnerState state);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ReadVFXSpawnerEventRecord")]
    public static extern Result Graphics_ReadVFXSpawnerEventRecord(
        IntPtr device, ulong effectId, long contextId,
        [Out] byte[] record, int recordCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ClearVFXEffectState")]
    public static extern Result Graphics_ClearVFXEffectState(
        IntPtr device, ulong effectId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_EnqueueVFXOutputEventRecords")]
    public static extern Result Graphics_EnqueueVFXOutputEventRecords(
        IntPtr device, ref GraphicsVFXEventUploadDesc desc,
        byte[] records, int byteCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetVFXOutputEventCount")]
    public static extern Result Graphics_GetVFXOutputEventCount(
        IntPtr device, ulong effectId, out int count);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_PeekVFXOutputEventInfo")]
    public static extern Result Graphics_PeekVFXOutputEventInfo(
        IntPtr device, ulong effectId, out GraphicsVFXEventUploadInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_DequeueVFXOutputEventRecords")]
    public static extern Result Graphics_DequeueVFXOutputEventRecords(
        IntPtr device, ulong effectId, ulong expectedSequence,
        [Out] byte[] records, int recordCapacity, out int written);

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

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetSwapchainPresentCount")]
    public static extern int Graphics_GetSwapchainPresentCount(IntPtr swapchain);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ReadbackSwapchainRGBA8")]
    public static extern Result Graphics_ReadbackSwapchainRGBA8(
        IntPtr swapchain, [Out] byte[] pixels, int pixelCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ReadbackSwapchainToneMappedRGBA8")]
    public static extern Result Graphics_ReadbackSwapchainToneMappedRGBA8(
        IntPtr swapchain, [Out] byte[] pixels, int pixelCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_ProcessSwapchainHDR")]
    public static extern Result Graphics_ProcessSwapchainHDR(IntPtr swapchain, ref HDRColorGrade grade);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_SwapchainHasNativeSurface")]
    public static extern int Graphics_SwapchainHasNativeSurface(IntPtr swapchain);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetSwapchainBackendKind")]
    public static extern int Graphics_GetSwapchainBackendKind(IntPtr swapchain);

    /// <summary>0=none, 1=Win32, 2=Android, 3=X11, 4=Wayland</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_GetSwapchainSurfaceKind")]
    public static extern int Graphics_GetSwapchainSurfaceKind(IntPtr swapchain);

    /// <summary>bit0=Win32, bit1=Android, bit2=X11, bit3=Wayland</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityGraphics_Vulkan_GetSupportedSurfaceMask")]
    public static extern int Graphics_Vulkan_GetSupportedSurfaceMask();

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
        public float hueShift;
        public float colorFilterR;
        public float colorFilterG;
        public float colorFilterB;
        public float mixerRedR;
        public float mixerRedG;
        public float mixerRedB;
        public float mixerGreenR;
        public float mixerGreenG;
        public float mixerGreenB;
        public float mixerBlueR;
        public float mixerBlueG;
        public float mixerBlueB;
        public int curveEnabled;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
        public float[] curveLut;
        public float bloomThreshold;
        public float bloomIntensity;
        public float bloomScatter;
        public int bloomMaxIterations;
        public int bloomDownscale;
        public int bloomHighQualityFiltering;
        public float bloomTintR;
        public float bloomTintG;
        public float bloomTintB;
        public ulong bloomDirtTextureId;
        public float bloomDirtIntensity;
        public int tonemapMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsHDRPostProcessStats
    {
        public int backendKind;
        public int curveLutSamplesPerCurve;
        public ulong curveLutByteCapacity;
        public ulong curveLutUploadCount;
        public ulong curveLutCacheHitCount;
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

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityHDR_ProcessFrameWithLensDirtRGBA8")]
    public static extern Result HDR_ProcessFrameWithLensDirtRGBA8(
        float[] rgbaHdr, int width, int height,
        ref HDRColorGrade grade,
        byte[] dirtRgba8, int dirtWidth, int dirtHeight,
        int dirtFilterMode, int dirtWrapU, int dirtWrapV,
        int dirtLinear, int dirtByteCount,
        float[] rgbaOut,
        int outHdr10);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityHDR_ProcessFrameWithLensDirtRGBA8Mips")]
    public static extern Result HDR_ProcessFrameWithLensDirtRGBA8Mips(
        float[] rgbaHdr, int width, int height,
        ref HDRColorGrade grade,
        byte[] dirtRgba8, int dirtWidth, int dirtHeight,
        int dirtMipCount, int dirtFilterMode, int dirtWrapU, int dirtWrapV,
        int dirtLinear, int dirtByteCount,
        float[] rgbaOut,
        int outHdr10);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityHDR_ProcessFrameWithLensDirtRGBA8MipsBias")]
    public static extern Result HDR_ProcessFrameWithLensDirtRGBA8MipsBias(
        float[] rgbaHdr, int width, int height,
        ref HDRColorGrade grade,
        byte[] dirtRgba8, int dirtWidth, int dirtHeight,
        int dirtMipCount, int dirtFilterMode, int dirtWrapU, int dirtWrapV,
        int dirtLinear, float dirtMipBias, int dirtByteCount,
        float[] rgbaOut,
        int outHdr10);

    // --- Transform ---
    [StructLayout(LayoutKind.Sequential)]
    public struct TransformVector3
    {
        public float x, y, z;
        public TransformVector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TransformQuaternion
    {
        public float x, y, z, w;
        public TransformQuaternion(float x, float y, float z, float w)
        {
            this.x = x; this.y = y; this.z = z; this.w = w;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TransformMatrix4x4
    {
        public float m00, m01, m02, m03;
        public float m10, m11, m12, m13;
        public float m20, m21, m22, m23;
        public float m30, m31, m32, m33;

        public static TransformMatrix4x4 Identity => new TransformMatrix4x4
        {
            m00 = 1f, m11 = 1f, m22 = 1f, m33 = 1f
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MatrixFrustumPlanes
    {
        public float left, right, bottom, top, zNear, zFar;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UIVector3
    {
        public float x, y, z;
        public UIVector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UIVector4
    {
        public float x, y, z, w;
        public UIVector4(float x, float y, float z, float w)
        {
            this.x = x; this.y = y; this.z = z; this.w = w;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UIColor32
    {
        public byte r, g, b, a;
        public UIColor32(byte r, byte g, byte b, byte a)
        {
            this.r = r; this.g = g; this.b = b; this.a = a;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UIVertexNative
    {
        public UIVector3 position;
        public UIVector3 normal;
        public UIVector4 tangent;
        public UIColor32 color;
        public UIVector4 uv0, uv1, uv2, uv3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UIPackedVertex
    {
        public UIVector3 position;
        public UIColor32 color;
        public UIVector4 uv0, uv1, uv2, uv3;
        public UIVector3 normal;
        public UIVector4 tangent;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UIBounds
    {
        public UIVector3 min;
        public UIVector3 max;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UIRenderState
    {
        public UIBounds bounds;
        public float clipXMin, clipYMin, clipXMax, clipYMax;
        public float softnessX, softnessY;
        public float colorAlpha, inheritedAlpha;
        public int hasGeometry, rectClipping, cullTransparentMesh;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UIVisibility
    {
        public float effectiveAlpha;
        public float innerClipXMin, innerClipYMin, innerClipXMax, innerClipYMax;
        public int visible, clipped, culledByAlpha, culledByClip;
    }

    [Flags]
    public enum UIRenderCommandFlags : uint
    {
        Visible = 1u << 0,
        RectClip = 1u << 1,
        Mask = 1u << 2,
        Pop = 1u << 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UIRenderCommandDesc
    {
        public ulong rendererId, materialId, textureId, alphaTextureId;
        public int sortDepth;
        public UIRenderCommandFlags flags;
        public float clipXMin, clipYMin, clipXMax, clipYMax;
        public float softnessX, softnessY;
        public float effectiveAlpha;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UIBatchInfo
    {
        public ulong materialId, textureId, alphaTextureId;
        public int firstSortDepth, lastSortDepth;
        public UIRenderCommandFlags flags;
        public int commandCount, vertexCount, indexCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UICanvasStats
    {
        public ulong frameId, generation;
        public int commandCount, visibleCommandCount, batchCount, vertexCount, indexCount;
    }

    private static bool _transformNativeAvailable = true;

    private static bool _matrixNativeAvailable = true;

    private static bool _uiRendererNativeAvailable = true;

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUIRenderer_PackVertices")]
    private static extern Result UIRenderer_PackVertices(
        [In] UIVertexNative[] vertices,
        int vertexCount,
        [Out] UIPackedVertex[] packedVertices,
        int packedCapacity,
        out int written,
        out UIBounds bounds);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUIRenderer_BuildQuadIndices")]
    private static extern Result UIRenderer_BuildQuadIndices(
        int vertexCount,
        [Out] uint[] indices,
        int indexCapacity,
        out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUIRenderer_EvaluateVisibility")]
    private static extern Result UIRenderer_EvaluateVisibility(
        ref UIRenderState state,
        out UIVisibility visibility);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUICanvas_Create")]
    public static extern Result UICanvas_Create(out IntPtr canvas);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUICanvas_Destroy")]
    public static extern void UICanvas_Destroy(IntPtr canvas);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUICanvas_BeginFrame")]
    public static extern Result UICanvas_BeginFrame(IntPtr canvas, ulong frameId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUICanvas_Clear")]
    public static extern Result UICanvas_Clear(IntPtr canvas);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUICanvas_UpsertCommand")]
    public static extern Result UICanvas_UpsertCommand(
        IntPtr canvas, ref UIRenderCommandDesc desc,
        [In] UIPackedVertex[] vertices, int vertexCount,
        [In] uint[] indices, int indexCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUICanvas_RemoveCommand")]
    public static extern Result UICanvas_RemoveCommand(IntPtr canvas, ulong rendererId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUICanvas_BuildBatches")]
    public static extern Result UICanvas_BuildBatches(IntPtr canvas);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUICanvas_GetStats")]
    public static extern Result UICanvas_GetStats(IntPtr canvas, out UICanvasStats stats);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUICanvas_GetBatchInfo")]
    public static extern Result UICanvas_GetBatchInfo(IntPtr canvas, int batchIndex, out UIBatchInfo info);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUICanvas_CopyBatchVertices")]
    public static extern Result UICanvas_CopyBatchVertices(
        IntPtr canvas, int batchIndex, [Out] UIPackedVertex[] vertices,
        int vertexCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityUICanvas_CopyBatchIndices")]
    public static extern Result UICanvas_CopyBatchIndices(
        IntPtr canvas, int batchIndex, [Out] uint[] indices,
        int indexCapacity, out int written);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityMatrix_Determinant")]
    private static extern float Matrix_Determinant(ref TransformMatrix4x4 matrix);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityMatrix_Inverse")]
    private static extern int Matrix_Inverse(ref TransformMatrix4x4 matrix, out TransformMatrix4x4 result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityMatrix_Inverse3DAffine")]
    private static extern int Matrix_Inverse3DAffine(ref TransformMatrix4x4 matrix, out TransformMatrix4x4 result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityMatrix_Transpose")]
    private static extern int Matrix_Transpose(ref TransformMatrix4x4 matrix, out TransformMatrix4x4 result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityMatrix_TRS")]
    private static extern int Matrix_TRS(
        TransformVector3 position, TransformQuaternion rotation, TransformVector3 scale,
        out TransformMatrix4x4 result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityMatrix_Ortho")]
    private static extern int Matrix_Ortho(
        float left, float right, float bottom, float top, float zNear, float zFar,
        out TransformMatrix4x4 result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityMatrix_Perspective")]
    private static extern int Matrix_Perspective(
        float fov, float aspect, float zNear, float zFar,
        out TransformMatrix4x4 result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityMatrix_Frustum")]
    private static extern int Matrix_Frustum(
        float left, float right, float bottom, float top, float zNear, float zFar,
        out TransformMatrix4x4 result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityMatrix_LookAt")]
    private static extern int Matrix_LookAt(
        TransformVector3 from, TransformVector3 to, TransformVector3 up,
        out TransformMatrix4x4 result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityMatrix_ExtractRotation")]
    private static extern int Matrix_ExtractRotation(
        ref TransformMatrix4x4 matrix, out TransformQuaternion result);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityMatrix_ValidTRS")]
    private static extern int Matrix_ValidTRS(ref TransformMatrix4x4 matrix);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityMatrix_DecomposeProjection")]
    private static extern int Matrix_DecomposeProjection(
        ref TransformMatrix4x4 matrix, out MatrixFrustumPlanes result);

    [DllImport("anity_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityTransform_ComposeLocalToWorld")]
    private static extern int Transform_ComposeLocalToWorld_Windows(
        ref TransformMatrix4x4 parentLocalToWorld,
        TransformVector3 localPosition,
        TransformQuaternion localRotation,
        TransformVector3 localScale,
        out TransformMatrix4x4 localToWorld);

    [DllImport("libanity_native.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityTransform_ComposeLocalToWorld")]
    private static extern int Transform_ComposeLocalToWorld_MacOS(
        ref TransformMatrix4x4 parentLocalToWorld,
        TransformVector3 localPosition,
        TransformQuaternion localRotation,
        TransformVector3 localScale,
        out TransformMatrix4x4 localToWorld);

    [DllImport("libanity_native.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityTransform_ComposeLocalToWorld")]
    private static extern int Transform_ComposeLocalToWorld_Unix(
        ref TransformMatrix4x4 parentLocalToWorld,
        TransformVector3 localPosition,
        TransformQuaternion localRotation,
        TransformVector3 localScale,
        out TransformMatrix4x4 localToWorld);

    [DllImport("anity_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityTransform_ComposeWorldToLocal")]
    private static extern int Transform_ComposeWorldToLocal_Windows(
        ref TransformMatrix4x4 parentWorldToLocal,
        TransformVector3 localPosition,
        TransformQuaternion localRotation,
        TransformVector3 localScale,
        out TransformMatrix4x4 worldToLocal);

    [DllImport("libanity_native.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityTransform_ComposeWorldToLocal")]
    private static extern int Transform_ComposeWorldToLocal_MacOS(
        ref TransformMatrix4x4 parentWorldToLocal,
        TransformVector3 localPosition,
        TransformQuaternion localRotation,
        TransformVector3 localScale,
        out TransformMatrix4x4 worldToLocal);

    [DllImport("libanity_native.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityTransform_ComposeWorldToLocal")]
    private static extern int Transform_ComposeWorldToLocal_Unix(
        ref TransformMatrix4x4 parentWorldToLocal,
        TransformVector3 localPosition,
        TransformQuaternion localRotation,
        TransformVector3 localScale,
        out TransformMatrix4x4 worldToLocal);

    [DllImport("anity_native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityTransform_ProjectLossyScale")]
    private static extern int Transform_ProjectLossyScale_Windows(
        ref TransformMatrix4x4 localToWorld,
        TransformQuaternion worldRotation,
        out TransformVector3 lossyScale);

    [DllImport("libanity_native.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityTransform_ProjectLossyScale")]
    private static extern int Transform_ProjectLossyScale_MacOS(
        ref TransformMatrix4x4 localToWorld,
        TransformQuaternion worldRotation,
        out TransformVector3 lossyScale);

    [DllImport("libanity_native.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityTransform_ProjectLossyScale")]
    private static extern int Transform_ProjectLossyScale_Unix(
        ref TransformMatrix4x4 localToWorld,
        TransformQuaternion worldRotation,
        out TransformVector3 lossyScale);

    // --- Physics ---
    [StructLayout(LayoutKind.Sequential)]
    public struct Vec3 { public float x, y, z; public Vec3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }

    [StructLayout(LayoutKind.Sequential)]
    public struct Vec2 { public float x, y; public Vec2(float x, float y) { this.x = x; this.y = y; } }

    [StructLayout(LayoutKind.Sequential)]
    public struct Quat
    {
        public float x, y, z, w;
        public Quat(float x, float y, float z, float w)
        {
            this.x = x; this.y = y; this.z = z; this.w = w;
        }
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityPhysics3D_ResolveConstantForce")]
    public static extern int Physics3D_ResolveConstantForce(
        Vec3 force, Vec3 relativeForce, Vec3 torque, Vec3 relativeTorque, Quat rotation,
        out Vec3 outForce, out Vec3 outTorque);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityPhysics2D_ResolveConstantForce")]
    public static extern int Physics2D_ResolveConstantForce(
        Vec2 force, Vec2 relativeForce, Quat rotation, float torque,
        out Vec2 outForce, out float outTorque);

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

    public static bool TryPackUIVertices(
        UIVertexNative[] vertices, int vertexCount,
        out UIPackedVertex[] packedVertices, out UIBounds bounds)
    {
        packedVertices = Array.Empty<UIPackedVertex>();
        bounds = default;
        if (!_uiRendererNativeAvailable || vertices is null || vertexCount < 0 || vertexCount > vertices.Length)
            return false;

        UIPackedVertex[] output = new UIPackedVertex[vertexCount];
        try
        {
            Result result = UIRenderer_PackVertices(
                vertices, vertexCount, output, output.Length, out int written, out bounds);
            if (result != Result.Ok || written != vertexCount)
                return false;
            packedVertices = output;
            return true;
        }
        catch (DllNotFoundException) { return HandleMissingUIRendererNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingUIRendererNative(); }
    }

    public static bool TryBuildUIQuadIndices(int vertexCount, out int[] indices)
    {
        indices = Array.Empty<int>();
        if (!_uiRendererNativeAvailable || vertexCount < 0 || vertexCount % 4 != 0)
            return false;

        uint[] nativeIndices = new uint[(vertexCount / 4) * 6];
        try
        {
            Result result = UIRenderer_BuildQuadIndices(
                vertexCount, nativeIndices, nativeIndices.Length, out int written);
            if (result != Result.Ok || written != nativeIndices.Length)
                return false;
            var managedIndices = new int[written];
            for (int index = 0; index < written; index++)
                managedIndices[index] = checked((int)nativeIndices[index]);
            indices = managedIndices;
            return true;
        }
        catch (DllNotFoundException) { return HandleMissingUIRendererNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingUIRendererNative(); }
    }

    public static bool TryEvaluateUIVisibility(UIRenderState state, out UIVisibility visibility)
    {
        visibility = default;
        if (!_uiRendererNativeAvailable)
            return false;
        try
        {
            return UIRenderer_EvaluateVisibility(ref state, out visibility) == Result.Ok;
        }
        catch (DllNotFoundException) { return HandleMissingUIRendererNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingUIRendererNative(); }
    }

    public static bool TryComposeTransformLocalToWorld(
        TransformMatrix4x4 parentLocalToWorld,
        TransformVector3 localPosition,
        TransformQuaternion localRotation,
        TransformVector3 localScale,
        out TransformMatrix4x4 localToWorld)
    {
        localToWorld = default;
        if (!_transformNativeAvailable) return false;
        try
        {
            int result;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                result = Transform_ComposeLocalToWorld_Windows(ref parentLocalToWorld, localPosition, localRotation, localScale, out localToWorld);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                result = Transform_ComposeLocalToWorld_MacOS(ref parentLocalToWorld, localPosition, localRotation, localScale, out localToWorld);
            else
                result = Transform_ComposeLocalToWorld_Unix(ref parentLocalToWorld, localPosition, localRotation, localScale, out localToWorld);
            return result != 0;
        }
        catch (DllNotFoundException) { _transformNativeAvailable = false; if (NativeTransformRequired) throw; return false; }
        catch (EntryPointNotFoundException) { _transformNativeAvailable = false; if (NativeTransformRequired) throw; return false; }
    }

    public static bool TryComposeTransformWorldToLocal(
        TransformMatrix4x4 parentWorldToLocal,
        TransformVector3 localPosition,
        TransformQuaternion localRotation,
        TransformVector3 localScale,
        out TransformMatrix4x4 worldToLocal)
    {
        worldToLocal = default;
        if (!_transformNativeAvailable) return false;
        try
        {
            int result;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                result = Transform_ComposeWorldToLocal_Windows(ref parentWorldToLocal, localPosition, localRotation, localScale, out worldToLocal);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                result = Transform_ComposeWorldToLocal_MacOS(ref parentWorldToLocal, localPosition, localRotation, localScale, out worldToLocal);
            else
                result = Transform_ComposeWorldToLocal_Unix(ref parentWorldToLocal, localPosition, localRotation, localScale, out worldToLocal);
            return result != 0;
        }
        catch (DllNotFoundException) { _transformNativeAvailable = false; if (NativeTransformRequired) throw; return false; }
        catch (EntryPointNotFoundException) { _transformNativeAvailable = false; if (NativeTransformRequired) throw; return false; }
    }

    public static bool TryProjectTransformLossyScale(
        TransformMatrix4x4 localToWorld,
        TransformQuaternion worldRotation,
        out TransformVector3 lossyScale)
    {
        lossyScale = default;
        if (!_transformNativeAvailable) return false;
        try
        {
            int result;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                result = Transform_ProjectLossyScale_Windows(ref localToWorld, worldRotation, out lossyScale);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                result = Transform_ProjectLossyScale_MacOS(ref localToWorld, worldRotation, out lossyScale);
            else
                result = Transform_ProjectLossyScale_Unix(ref localToWorld, worldRotation, out lossyScale);
            return result != 0;
        }
        catch (DllNotFoundException) { _transformNativeAvailable = false; if (NativeTransformRequired) throw; return false; }
        catch (EntryPointNotFoundException) { _transformNativeAvailable = false; if (NativeTransformRequired) throw; return false; }
    }

    public static bool TryMatrixDeterminant(TransformMatrix4x4 matrix, out float determinant)
    {
        determinant = 0f;
        if (!_matrixNativeAvailable) return false;
        try
        {
            determinant = Matrix_Determinant(ref matrix);
            return true;
        }
        catch (DllNotFoundException) { return HandleMissingMatrixNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingMatrixNative(); }
    }

    public static bool TryMatrixInverse(TransformMatrix4x4 matrix, out TransformMatrix4x4 result)
    {
        result = default;
        if (!_matrixNativeAvailable) return false;
        try
        {
            _ = Matrix_Inverse(ref matrix, out result);
            return true;
        }
        catch (DllNotFoundException) { return HandleMissingMatrixNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingMatrixNative(); }
    }

    public static bool TryMatrixInverse3DAffine(
        TransformMatrix4x4 matrix, out TransformMatrix4x4 result, out bool invertible)
    {
        result = default;
        invertible = false;
        if (!_matrixNativeAvailable) return false;
        try
        {
            invertible = Matrix_Inverse3DAffine(ref matrix, out result) != 0;
            return true;
        }
        catch (DllNotFoundException) { return HandleMissingMatrixNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingMatrixNative(); }
    }

    public static bool TryMatrixTranspose(TransformMatrix4x4 matrix, out TransformMatrix4x4 result)
    {
        result = default;
        if (!_matrixNativeAvailable) return false;
        try { return Matrix_Transpose(ref matrix, out result) != 0; }
        catch (DllNotFoundException) { return HandleMissingMatrixNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingMatrixNative(); }
    }

    public static bool TryMatrixTRS(
        TransformVector3 position, TransformQuaternion rotation, TransformVector3 scale,
        out TransformMatrix4x4 result)
    {
        result = default;
        if (!_matrixNativeAvailable) return false;
        try { return Matrix_TRS(position, rotation, scale, out result) != 0; }
        catch (DllNotFoundException) { return HandleMissingMatrixNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingMatrixNative(); }
    }

    public static bool TryMatrixOrtho(
        float left, float right, float bottom, float top, float zNear, float zFar,
        out TransformMatrix4x4 result)
    {
        result = default;
        if (!_matrixNativeAvailable) return false;
        try { return Matrix_Ortho(left, right, bottom, top, zNear, zFar, out result) != 0; }
        catch (DllNotFoundException) { return HandleMissingMatrixNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingMatrixNative(); }
    }

    public static bool TryMatrixPerspective(
        float fov, float aspect, float zNear, float zFar,
        out TransformMatrix4x4 result)
    {
        result = default;
        if (!_matrixNativeAvailable) return false;
        try { return Matrix_Perspective(fov, aspect, zNear, zFar, out result) != 0; }
        catch (DllNotFoundException) { return HandleMissingMatrixNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingMatrixNative(); }
    }

    public static bool TryMatrixFrustum(
        float left, float right, float bottom, float top, float zNear, float zFar,
        out TransformMatrix4x4 result)
    {
        result = default;
        if (!_matrixNativeAvailable) return false;
        try { return Matrix_Frustum(left, right, bottom, top, zNear, zFar, out result) != 0; }
        catch (DllNotFoundException) { return HandleMissingMatrixNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingMatrixNative(); }
    }

    public static bool TryMatrixLookAt(
        TransformVector3 from, TransformVector3 to, TransformVector3 up,
        out TransformMatrix4x4 result)
    {
        result = default;
        if (!_matrixNativeAvailable) return false;
        try { return Matrix_LookAt(from, to, up, out result) != 0; }
        catch (DllNotFoundException) { return HandleMissingMatrixNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingMatrixNative(); }
    }

    public static bool TryMatrixExtractRotation(
        TransformMatrix4x4 matrix, out TransformQuaternion result)
    {
        result = default;
        if (!_matrixNativeAvailable) return false;
        try { return Matrix_ExtractRotation(ref matrix, out result) != 0; }
        catch (DllNotFoundException) { return HandleMissingMatrixNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingMatrixNative(); }
    }

    public static bool TryMatrixValidTRS(TransformMatrix4x4 matrix, out bool valid)
    {
        valid = false;
        if (!_matrixNativeAvailable) return false;
        try
        {
            valid = Matrix_ValidTRS(ref matrix) != 0;
            return true;
        }
        catch (DllNotFoundException) { return HandleMissingMatrixNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingMatrixNative(); }
    }

    public static bool TryMatrixDecomposeProjection(
        TransformMatrix4x4 matrix, out MatrixFrustumPlanes result)
    {
        result = default;
        if (!_matrixNativeAvailable) return false;
        try { return Matrix_DecomposeProjection(ref matrix, out result) != 0; }
        catch (DllNotFoundException) { return HandleMissingMatrixNative(); }
        catch (EntryPointNotFoundException) { return HandleMissingMatrixNative(); }
    }

    private static bool HandleMissingMatrixNative()
    {
        _matrixNativeAvailable = false;
        if (NativeTransformRequired)
            throw new DllNotFoundException("anity-native Matrix4x4 entry points are required but unavailable.");
        return false;
    }

    private static bool HandleMissingUIRendererNative()
    {
        _uiRendererNativeAvailable = false;
        if (NativeTransformRequired)
            throw new DllNotFoundException("anity-native CanvasRenderer entry points are required but unavailable.");
        return false;
    }

    private static bool NativeTransformRequired
        => string.Equals(Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE"), "1", StringComparison.Ordinal);

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

    public static bool TryResolveConstantForce3D(
        float fx, float fy, float fz,
        float rfx, float rfy, float rfz,
        float tx, float ty, float tz,
        float rtx, float rty, float rtz,
        float qx, float qy, float qz, float qw,
        out Vec3 worldForce, out Vec3 worldTorque)
    {
        worldForce = default;
        worldTorque = default;
        if (!Available) return false;
        try
        {
            return Physics3D_ResolveConstantForce(
                new Vec3(fx, fy, fz), new Vec3(rfx, rfy, rfz),
                new Vec3(tx, ty, tz), new Vec3(rtx, rty, rtz),
                new Quat(qx, qy, qz, qw), out worldForce, out worldTorque) != 0;
        }
        catch
        {
            Available = false;
            return false;
        }
    }

    public static bool TryResolveConstantForce2D(
        float fx, float fy, float rfx, float rfy,
        float qx, float qy, float qz, float qw, float torque,
        out Vec2 worldForce, out float worldTorque)
    {
        worldForce = default;
        worldTorque = default;
        if (!Available) return false;
        try
        {
            return Physics2D_ResolveConstantForce(
                new Vec2(fx, fy), new Vec2(rfx, rfy), new Quat(qx, qy, qz, qw), torque,
                out worldForce, out worldTorque) != 0;
        }
        catch
        {
            Available = false;
            return false;
        }
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
