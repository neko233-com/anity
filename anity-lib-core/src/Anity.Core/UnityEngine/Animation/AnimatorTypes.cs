using System;

namespace UnityEngine;

public struct AnimatorStateInfo
{
    public int fullPathHash { get; }
    public int shortNameHash { get; }
    public float normalizedTime { get; }
    public float length { get; }
    public float speed { get; }
    public float speedMultiplier { get; }
    public int tagHash { get; }
    public bool loop { get; }

    public bool IsName(string name)
    {
        return fullPathHash == Animator.StringToHash(name);
    }

    public bool IsTag(string tag)
    {
        return tagHash == Animator.StringToHash(tag);
    }
}

public struct AnimatorClipInfo
{
    public AnimationClip clip { get; }
    public float weight { get; }

    public AnimatorClipInfo(AnimationClip clip, float weight)
    {
        this.clip = clip;
        this.weight = weight;
    }
}

public struct AnimatorTransitionInfo
{
    public int fullPathHash { get; }
    public int userNameHash { get; }
    public int nameHash { get; }
    public bool hasExitTime { get; }
    public float normalizedTime { get; }
    public float duration { get; }

    public bool IsName(string name)
    {
        return fullPathHash == Animator.StringToHash(name);
    }

    public bool IsUserName(string name)
    {
        return userNameHash == Animator.StringToHash(name);
    }
}

public struct AnimatorControllerParameter
{
    public string name { get; set; }
    public AnimatorControllerParameterType type { get; set; }
    public float defaultFloat { get; set; }
    public int defaultInt { get; set; }
    public bool defaultBool { get; set; }
    public int nameHash { get; }
}

public enum AnimatorControllerParameterType
{
    Float = 1,
    Int = 3,
    Bool = 4,
    Trigger = 9
}

public enum HumanBodyBones
{
    Hips = 0,
    LeftUpperLeg = 1,
    RightUpperLeg = 2,
    LeftLowerLeg = 3,
    RightLowerLeg = 4,
    LeftFoot = 5,
    RightFoot = 6,
    Spine = 7,
    Chest = 8,
    UpperChest = 54,
    Neck = 9,
    Head = 10,
    LeftShoulder = 11,
    RightShoulder = 12,
    LeftUpperArm = 13,
    RightUpperArm = 14,
    LeftLowerArm = 15,
    RightLowerArm = 16,
    LeftHand = 17,
    RightHand = 18,
    LeftToes = 19,
    RightToes = 20,
    LeftEye = 21,
    RightEye = 22,
    Jaw = 23,
    LeftThumbProximal = 24,
    LeftThumbIntermediate = 25,
    LeftThumbDistal = 26,
    LeftIndexProximal = 27,
    LeftIndexIntermediate = 28,
    LeftIndexDistal = 29,
    LeftMiddleProximal = 30,
    LeftMiddleIntermediate = 31,
    LeftMiddleDistal = 32,
    LeftRingProximal = 33,
    LeftRingIntermediate = 34,
    LeftRingDistal = 35,
    LeftLittleProximal = 36,
    LeftLittleIntermediate = 37,
    LeftLittleDistal = 38,
    RightThumbProximal = 39,
    RightThumbIntermediate = 40,
    RightThumbDistal = 41,
    RightIndexProximal = 42,
    RightIndexIntermediate = 43,
    RightIndexDistal = 44,
    RightMiddleProximal = 45,
    RightMiddleIntermediate = 46,
    RightMiddleDistal = 47,
    RightRingProximal = 48,
    RightRingIntermediate = 49,
    RightRingDistal = 50,
    RightLittleProximal = 51,
    RightLittleIntermediate = 52,
    RightLittleDistal = 53,
    LastBone = 55
}

public enum AvatarIKGoal
{
    LeftFoot = 0,
    RightFoot = 1,
    LeftHand = 2,
    RightHand = 3
}

public enum AvatarIKHint
{
    LeftKnee = 0,
    RightKnee = 1,
    LeftElbow = 2,
    RightElbow = 3
}

public enum AnimatorCullingMode
{
    AlwaysAnimate = 0,
    CullUpdateTransforms = 1,
    CullCompletely = 2
}

public enum AnimatorUpdateMode
{
    Normal = 0,
    AnimatePhysics = 1,
    UnscaledTime = 2
}

public enum AnimatorRecorderMode
{
    Offline = 0,
    Playback = 1,
    Record = 2
}

public enum AnimatorPlaybackPolicy
{
    Once,
    Loop,
    PingPong
}

public class AnimatorOverrideController : RuntimeAnimatorController
{
    private RuntimeAnimatorController _runtimeController;
    private string _name;

    public string name
    {
        get => _name;
        set => _name = value;
    }

    public RuntimeAnimatorController runtimeAnimatorController
    {
        get => _runtimeController;
        set => _runtimeController = value;
    }

    public int overridesCount { get; }

    public AnimationClip this[string name]
    {
        get => null;
        set { }
    }

    public AnimatorOverrideController()
    {
    }

    public AnimatorOverrideController(RuntimeAnimatorController controller)
    {
        _runtimeController = controller;
    }

    public void GetOverrides(List<KeyValuePair<AnimationClip, AnimationClip>> overrides)
    {
    }

    public void ApplyOverrides(IList<KeyValuePair<AnimationClip, AnimationClip>> overrides)
    {
    }
}

public class AnimatorController : RuntimeAnimatorController
{
    public string name { get; set; }
    public AnimatorControllerParameter[] parameters { get; } = Array.Empty<AnimatorControllerParameter>();
    public int layerCount { get; } = 1;

    public AnimatorController()
    {
    }

    public AnimatorControllerParameter GetParameter(int index)
    {
        return default;
    }

    public string GetLayerName(int layerIndex)
    {
        return string.Empty;
    }

    public int GetLayerIndex(string layerName)
    {
        return -1;
    }
}