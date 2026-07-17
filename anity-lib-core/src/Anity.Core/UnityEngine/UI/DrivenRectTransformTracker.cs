using System;
using System.Collections.Generic;

namespace UnityEngine;

[Flags]
public enum DrivenTransformProperties
{
  None = 0,
  AnchoredPositionX = 1 << 1,
  AnchoredPositionY = 1 << 2,
  AnchoredPosition = AnchoredPositionX | AnchoredPositionY,
  AnchoredPositionZ = 1 << 3,
  AnchoredPosition3D = AnchoredPosition | AnchoredPositionZ,
  Rotation = 1 << 4,
  ScaleX = 1 << 5,
  ScaleY = 1 << 6,
  ScaleZ = 1 << 7,
  Scale = ScaleX | ScaleY | ScaleZ,
  AnchorMinX = 1 << 8,
  AnchorMinY = 1 << 9,
  AnchorMin = AnchorMinX | AnchorMinY,
  AnchorMaxX = 1 << 10,
  AnchorMaxY = 1 << 11,
  AnchorMax = AnchorMaxX | AnchorMaxY,
  Anchors = AnchorMin | AnchorMax,
  SizeDeltaX = 1 << 12,
  SizeDeltaY = 1 << 13,
  SizeDelta = SizeDeltaX | SizeDeltaY,
  PivotX = 1 << 14,
  PivotY = 1 << 15,
  Pivot = PivotX | PivotY,
  All = -1
}

[Bindings.NativeHeader("Editor/Src/Animation/AnimationModeSnapshot.h")]
[Bindings.NativeHeader("Editor/Src/Undo/PropertyUndoManager.h")]
public struct DrivenRectTransformTracker
{
  private List<RectTransform>? _tracked;

  public void Add(Object driver, RectTransform rectTransform, DrivenTransformProperties drivenProperties)
  {
    _tracked ??= new List<RectTransform>();
    _tracked.Add(rectTransform);
    rectTransform.SetDriven(driver, drivenProperties);
  }

  [Obsolete("revertValues parameter is ignored. Please use Clear() instead.")]
  public void Clear(bool revertValues)
  {
    Clear();
  }

  public void Clear()
  {
    if (_tracked is null)
      return;

    for (int i = 0; i < _tracked.Count; i++)
      _tracked[i]?.SetDriven(null, DrivenTransformProperties.None);

    _tracked.Clear();
  }

  public static void StartRecordingUndo()
  {
  }

  public static void StopRecordingUndo()
  {
  }
}
