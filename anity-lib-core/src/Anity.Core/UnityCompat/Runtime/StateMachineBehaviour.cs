using System;

namespace UnityEngine;

[Serializable]
public abstract class StateMachineBehaviour : ScriptableObject
{
    public virtual void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { _ = animator; _ = stateInfo; _ = layerIndex; }
    public virtual void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { _ = animator; _ = stateInfo; _ = layerIndex; }
    public virtual void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { _ = animator; _ = stateInfo; _ = layerIndex; }
    public virtual void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { _ = animator; _ = stateInfo; _ = layerIndex; }
    public virtual void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { _ = animator; _ = stateInfo; _ = layerIndex; }

    public virtual void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorControllerPlayable controller) { _ = animator; _ = stateInfo; _ = layerIndex; _ = controller; }
    public virtual void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorControllerPlayable controller) { _ = animator; _ = stateInfo; _ = layerIndex; _ = controller; }
    public virtual void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorControllerPlayable controller) { _ = animator; _ = stateInfo; _ = layerIndex; _ = controller; }
    public virtual void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorControllerPlayable controller) { _ = animator; _ = stateInfo; _ = layerIndex; _ = controller; }
    public virtual void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorControllerPlayable controller) { _ = animator; _ = stateInfo; _ = layerIndex; _ = controller; }

    public virtual void OnStateMachineEnter(Animator animator, int stateMachinePathHash) { _ = animator; _ = stateMachinePathHash; }
    public virtual void OnStateMachineExit(Animator animator, int stateMachinePathHash) { _ = animator; _ = stateMachinePathHash; }
    public virtual void OnStateMachineEnter(Animator animator, int stateMachinePathHash, AnimatorControllerPlayable controller) { _ = animator; _ = stateMachinePathHash; _ = controller; }
    public virtual void OnStateMachineExit(Animator animator, int stateMachinePathHash, AnimatorControllerPlayable controller) { _ = animator; _ = stateMachinePathHash; _ = controller; }
}

public struct AnimatorControllerPlayable
{
    private Animator _animator;

    public static AnimatorControllerPlayable Null => default;

    internal AnimatorControllerPlayable(Animator animator)
    {
        _animator = animator;
    }

    public bool IsValid() => _animator != null;

    public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex)
    {
        if (_animator != null) return _animator.GetCurrentAnimatorStateInfo(layerIndex);
        return default;
    }

    public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex)
    {
        if (_animator != null) return _animator.GetNextAnimatorStateInfo(layerIndex);
        return default;
    }
}
