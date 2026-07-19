using System;
using System.Collections.Generic;
using Anity.Core.Runtime.Native;

namespace UnityEngine;

[Scripting.APIUpdating.MovedFrom(true, "UnityEditor.Animations", "UnityEditor", null)]
public enum AvatarMaskBodyPart
{
  Root = 0,
  Body = 1,
  Head = 2,
  LeftLeg = 3,
  RightLeg = 4,
  LeftArm = 5,
  RightArm = 6,
  LeftFingers = 7,
  RightFingers = 8,
  LeftFootIK = 9,
  RightFootIK = 10,
  LeftHandIK = 11,
  RightHandIK = 12,
  LastBodyPart = 13,
}

[Bindings.NativeHeader("Modules/Animation/AvatarMask.h")]
[Bindings.NativeHeader("Modules/Animation/ScriptBindings/Animation.bindings.h")]
[Scripting.APIUpdating.MovedFrom(true, "UnityEditor.Animations", "UnityEditor", null)]
[Scripting.UsedByNativeCode]
public sealed class AvatarMask : Object
{
  private IntPtr _nativeHandle;

  public AvatarMask()
  {
    if (!AnityNative.TryCreateAvatarMask(out _nativeHandle))
      throw new InvalidOperationException("anity-native AvatarMask runtime is unavailable.");
  }

  [Obsolete("AvatarMask.humanoidBodyPartCount is deprecated, use AvatarMaskBodyPart.LastBodyPart instead.")]
  public int humanoidBodyPartCount => (int)AvatarMaskBodyPart.LastBodyPart;

  public int transformCount
  {
    get
    {
      EnsureNative(AnityNative.TryGetAvatarMaskTransformCount(_nativeHandle, out int count));
      return count;
    }
    set => EnsureNative(AnityNative.TrySetAvatarMaskTransformCount(_nativeHandle, value));
  }

  [Bindings.NativeMethod("GetBodyPart")]
  public bool GetHumanoidBodyPartActive(AvatarMaskBodyPart index)
  {
    EnsureNative(AnityNative.TryGetAvatarMaskHumanoidBodyPartActive(_nativeHandle, (int)index, out bool active));
    return active;
  }

  [Bindings.NativeMethod("SetBodyPart")]
  public void SetHumanoidBodyPartActive(AvatarMaskBodyPart index, bool value)
  {
    EnsureNative(AnityNative.TrySetAvatarMaskHumanoidBodyPartActive(_nativeHandle, (int)index, value));
  }

  public bool GetTransformActive(int index)
  {
    EnsureNative(AnityNative.TryGetAvatarMaskTransformActive(_nativeHandle, index, out bool active));
    return active;
  }

  public void SetTransformActive(int index, bool value)
  {
    EnsureNative(AnityNative.TrySetAvatarMaskTransformActive(_nativeHandle, index, value));
  }

  public string GetTransformPath(int index)
  {
    EnsureNative(AnityNative.TryGetAvatarMaskTransformPath(_nativeHandle, index, out string path));
    return path;
  }

  public void SetTransformPath(int index, string path)
  {
    EnsureNative(AnityNative.TrySetAvatarMaskTransformPath(_nativeHandle, index, path ?? string.Empty));
  }

  public void AddTransformPath(Transform transform)
  {
    AddTransformPath(transform, true);
  }

  public void AddTransformPath(
    [Bindings.NotNull("ArgumentNullException")] Transform transform,
    [Internal.DefaultValue("true")] bool recursive)
  {
    if (transform is null) throw new ArgumentNullException(nameof(transform));
    AddTransformPathRecursive(transform, recursive);
  }

  public void RemoveTransformPath(Transform transform)
  {
    RemoveTransformPath(transform, true);
  }

  public void RemoveTransformPath(
    [Bindings.NotNull("ArgumentNullException")] Transform transform,
    [Internal.DefaultValue("true")] bool recursive)
  {
    if (transform is null) throw new ArgumentNullException(nameof(transform));
    EnsureNative(AnityNative.TryRemoveAvatarMaskTransformPath(_nativeHandle, GetTransformPathFromHierarchyRoot(transform), recursive));
  }

  internal void ReleaseNativeState()
  {
    IntPtr handle = _nativeHandle;
    _nativeHandle = IntPtr.Zero;
    AnityNative.DestroyAvatarMask(handle);
  }

  private void AddTransformPathRecursive(Transform transform, bool recursive)
  {
    EnsureNative(AnityNative.TryAddAvatarMaskTransformPath(_nativeHandle, GetTransformPathFromHierarchyRoot(transform)));
    if (!recursive) return;
    for (int index = 0; index < transform.childCount; ++index)
      AddTransformPathRecursive(transform.GetChild(index), true);
  }

  private static string GetTransformPathFromHierarchyRoot(Transform transform)
  {
    var names = new List<string>();
    for (Transform current = transform; current.parent is not null; current = current.parent)
      names.Add(current.gameObject?.name ?? string.Empty);
    names.Reverse();
    return string.Join("/", names);
  }

  private static void EnsureNative(bool success)
  {
    if (!success) throw new InvalidOperationException("anity-native AvatarMask operation failed.");
  }
}
