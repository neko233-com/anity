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
}

/// <summary>
/// Runtime Animator Controller asset.
/// </summary>
public class RuntimeAnimatorController : Object { }

