using System;

namespace UnityEngine;

/// <summary>
/// Unity Animator component for controlling animations.
/// </summary>
[AddComponentMenu("Animation/Animator")]
public class Animator : Behaviour
{
    private RuntimeAnimatorController? _controller;
    private bool _applyRootMotion;
    private float _speed = 1.0f;

    public RuntimeAnimatorController? controller
    {
        get => _controller;
        set => _controller = value;
    }

    public bool applyRootMotion
    {
        get => _applyRootMotion;
        set => _applyRootMotion = value;
    }

    public float speed
    {
        get => _speed;
        set => _speed = value;
    }

    public bool isHuman { get; }
    public bool hasRootMotion { get; }
    public float humanScale { get; }
    public bool isInitialized { get; }
    public Vector3 deltaPosition { get; }
    public Quaternion deltaRotation { get; }
    public Vector3 velocity { get; }
    public Vector3 angularVelocity { get; }
    public Vector3 pivotPosition { get; }
    public float pivotWeight { get; }

    public float GetFloat(string name) => 0.0f;
    public float GetFloat(int id) => 0.0f;
    public void SetFloat(string name, float value) { }
    public void SetFloat(int id, float value) { }

    public bool GetBool(string name) => false;
    public bool GetBool(int id) => false;
    public void SetBool(string name, bool value) { }
    public void SetBool(int id, bool value) { }

    public int GetInteger(string name) => 0;
    public int GetInteger(int id) => 0;
    public void SetInteger(string name, int value) { }
    public void SetInteger(int id, int value) { }

    public void SetTrigger(string name) { }
    public void SetTrigger(int id) { }
    public void ResetTrigger(string name) { }
    public void ResetTrigger(int id) { }

    public bool IsInTransition(int layerIndex) => false;
    public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex) => default;
    public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex) => default;
    public AnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layerIndex) => Array.Empty<AnimatorClipInfo>();
    public AnimatorClipInfo[] GetNextAnimatorClipInfo(int layerIndex) => Array.Empty<AnimatorClipInfo>();

    public void CrossFadeInFixedTime(string stateName, float normalizedTransitionDuration) { }
    public void CrossFadeInFixedTime(string stateName, float normalizedTransitionDuration, int layer) { }
    public void CrossFadeInFixedTime(string stateName, float normalizedTransitionDuration, int layer, float fixedTime) { }
    public void CrossFadeInFixedTime(int stateNameHash, float normalizedTransitionDuration) { }
    public void CrossFadeInFixedTime(int stateNameHash, float normalizedTransitionDuration, int layer) { }
    public void CrossFadeInFixedTime(int stateNameHash, float normalizedTransitionDuration, int layer, float fixedTime) { }

    public void CrossFade(string stateName, float normalizedTransitionDuration) { }
    public void CrossFade(string stateName, float normalizedTransitionDuration, int layer) { }
    public void CrossFade(string stateName, float normalizedTransitionDuration, int layer, float normalizedTimeOffset) { }
    public void CrossFade(int stateNameHash, float normalizedTransitionDuration) { }
    public void CrossFade(int stateNameHash, float normalizedTransitionDuration, int layer) { }
    public void CrossFade(int stateNameHash, float normalizedTransitionDuration, int layer, float normalizedTimeOffset) { }

    public void Play(string stateName) { }
    public void Play(string stateName, int layer) { }
    public void Play(string stateName, int layer, float normalizedTimeOffset) { }
    public void Play(int stateNameHash) { }
    public void Play(int stateNameHash, int layer) { }
    public void Play(int stateNameHash, int layer, float normalizedTimeOffset) { }

    public void Rebind() { }
    public void Update(float deltaTime) { }

    public int GetLayerCount() => 0;
    public string GetLayerName(int layerIndex) => string.Empty;
    public int GetLayerIndex(string layerName) => -1;
    public float GetLayerWeight(int layerIndex) => 0.0f;
    public void SetLayerWeight(int layerIndex, float weight) { }

    public void SetLookAtPosition(Vector3 lookAtPosition) { }
    public void SetLookAtWeight(float weight) { }
    public void SetLookAtWeight(float weight, float bodyWeight) { }
    public void SetLookAtWeight(float weight, float bodyWeight, float headWeight) { }
    public void SetLookAtWeight(float weight, float bodyWeight, float headWeight, float eyesWeight) { }
    public void SetLookAtWeight(float weight, float bodyWeight, float headWeight, float eyesWeight, float clampWeight) { }

    public Transform? GetBoneTransform(HumanBodyBones humanBoneId) => null;
    public void SetBoneLocalRotation(HumanBodyBones humanBoneId, Quaternion rotation) { }

    public bool HasState(int layerIndex, int stateID) => false;

    public Avatar? avatar { get; set; }

    public static int StringToHash(string name) => name?.GetHashCode() ?? 0;

    public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex) => default;
    public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex) => default;
    public AnimatorTransitionInfo GetAnimatorTransitionInfo(int layerIndex) => default;

    public AnimatorControllerParameter[] parameters { get; } = Array.Empty<AnimatorControllerParameter>();
    public int parameterCount => parameters?.Length ?? 0;

    public AnimatorControllerParameter GetParameter(int index)
    {
        if (parameters == null || index < 0 || index >= parameters.Length)
            return default;
        return parameters[index];
    }

    public AnimatorCullingMode cullingMode { get; set; }
    public AnimatorUpdateMode updateMode { get; set; }
    public AnimatorRecorderMode recorderMode { get; set; }

    public bool hasTransformHierarchy { get; } = true;
    public bool isHuman { get; }
    public bool hasRootMotion { get; }
    public float humanScale { get; } = 1f;
    public bool isInitialized { get; } = true;
    public bool hasAvatarMask { get; }

    public Vector3 gravityWeight { get; }
    public Vector3 bodyPosition { get; set; }
    public Quaternion bodyRotation { get; set; }

    public void SetBoneLocalRotation(HumanBodyBones humanBoneId, Quaternion rotation) { }

    public Transform GetBoneTransform(HumanBodyBones humanBoneId) => null;

    public bool LookAtPosition(Vector3 position, float weight) => false;
    public void SetLookAtWeight(float weight, float bodyWeight = 0f, float headWeight = 1f, float eyesWeight = 0f, float clampWeight = 0.5f) { }
    public Vector3 lookAtPosition { get; set; }

    public bool MatchTarget(Vector3 matchPosition, Quaternion matchRotation, AvatarTarget targetBodyPart, MatchTargetWeightMask weightMask, float startNormalizedTime, float targetNormalizedTime = 1) => false;
    public bool isMatchingTarget { get; }
    public void InterruptMatchTarget(bool completeMatch = true) { }

    public void SetTarget(AvatarTarget targetIndex, float targetNormalizedTime) { }
    public TargetRotation GetTargetRotation() => default;
    public TargetPosition GetTargetPosition() => default;

    public void SetFloat(string name, float value) { }
    public void SetFloat(int id, float value) { }
    public void SetFloat(string name, float value, float dampTime, float deltaTime) { }
    public void SetFloat(int id, float value, float dampTime, float deltaTime) { }

    public float GetFloat(string name) => 0f;
    public float GetFloat(int id) => 0f;

    public void SetInteger(string name, int value) { }
    public void SetInteger(int id, int value) { }
    public int GetInteger(string name) => 0;
    public int GetInteger(int id) => 0;

    public void SetBool(string name, bool value) { }
    public void SetBool(int id, bool value) { }
    public bool GetBool(string name) => false;
    public bool GetBool(int id) => false;

    public void SetTrigger(string name) { }
    public void SetTrigger(int id) { }
    public void ResetTrigger(string name) { }
    public void ResetTrigger(int id) { }

    public void CrossFade(string stateName, float normalizedTransitionDuration, int layer = -1, float normalizedTimeOffset = 0, float normalizedTransitionTime = 0) { }
    public void CrossFade(int stateHashName, float normalizedTransitionDuration, int layer = -1, float normalizedTimeOffset = 0, float normalizedTransitionTime = 0) { }
    public void CrossFadeInFixedTime(string stateName, float fixedTransitionDuration, int layer = -1, float fixedTimeOffset = 0, float normalizedTransitionTime = 0) { }
    public void CrossFadeInFixedTime(int stateHashName, float fixedTransitionDuration, int layer = -1, float fixedTimeOffset = 0, float normalizedTransitionTime = 0) { }

    public void Play(string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity) { }
    public void Play(int stateNameHash, int layer = -1, float normalizedTime = float.NegativeInfinity) { }

    public void PlayInFixedTime(string stateName, int layer = -1, float fixedTime = 0) { }
    public void PlayInFixedTime(int stateNameHash, int layer = -1, float fixedTime = 0) { }

    public bool IsInTransition(int layerIndex) => false;

    public int GetLayerIndex(string layerName) => -1;
    public string GetLayerName(int layerIndex) => string.Empty;
    public int GetLayerCount() => 1;
    public float GetLayerWeight(int layerIndex) => 0f;
    public void SetLayerWeight(int layerIndex, float weight) { }

    public AnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layerIndex) => Array.Empty<AnimatorClipInfo>();
    public AnimatorClipInfo[] GetNextAnimatorClipInfo(int layerIndex) => Array.Empty<AnimatorClipInfo>();

    public void Rebind() { }
    public void Update(float deltaTime) { }
    public void StartPlayback() { }
    public void StartRecording(int frameCount) { }
    public void StopPlayback() { }
    public void StopRecording() { }

    public float playbackTime { get; set; }
    public float recorderStartTime { get; }
    public float recorderStopTime { get; }
    public float speed { get; set; } = 1f;
    public bool applyRootMotion { get; set; }
    public bool feetPivotActive { get; set; }
    public int layerCount { get; } = 1;

    public Vector3 deltaPosition { get; }
    public Quaternion deltaRotation { get; }
    public Vector3 velocity { get; }
    public Vector3 angularVelocity { get; }
    public Vector3 pivotPosition { get; }
    public float pivotWeight { get; }

    public RuntimeAnimatorController? runtimeAnimatorController
    {
        get => controller;
        set => controller = value;
    }
}

public struct MatchTargetWeightMask
{
    public Vector3 positionXYZWeight;
    public float rotationWeight;

    public MatchTargetWeightMask(Vector3 positionXYZWeight, float rotationWeight)
    {
        this.positionXYZWeight = positionXYZWeight;
        this.rotationWeight = rotationWeight;
    }
}

public struct TargetRotation
{
    public bool active;
    public Quaternion rotation;
}

public struct TargetPosition
{
    public bool active;
    public Vector3 position;
}

public enum AvatarTarget
{
    Root = 0,
    Body = 1,
    LeftFoot = 2,
    RightFoot = 3,
    LeftHand = 4,
    RightHand = 5,
    LeftForeArmStretch = 6,
    RightForeArmStretch = 7,
    LeftThighStretch = 8,
    RightThighStretch = 9,
    LeftLowerLegStretch = 10,
    RightLowerLegStretch = 11,
    LeftUpperArmStretch = 12,
    RightUpperArmStretch = 13,
    LeftFingers = 14,
    RightFingers = 15,
    LeftFootIK = 16,
    RightFootIK = 17,
    LeftHandIK = 18,
    RightHandIK = 19
}

/// <summary>
/// Runtime Animator Controller asset.
/// </summary>
public class RuntimeAnimatorController : Object { }

