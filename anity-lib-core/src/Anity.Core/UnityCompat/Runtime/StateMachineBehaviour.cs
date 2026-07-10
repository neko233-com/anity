using System;

namespace UnityEngine;

[Serializable]
public abstract class StateMachineBehaviour : ScriptableObject
{
    public virtual void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { }
    public virtual void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { }
    public virtual void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { }
    public virtual void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { }
    public virtual void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { }

    public virtual void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorControllerPlayable controller) { _ = controller; }
    public virtual void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorControllerPlayable controller) { _ = controller; }
    public virtual void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorControllerPlayable controller) { _ = controller; }
    public virtual void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorControllerPlayable controller) { _ = controller; }
    public virtual void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorControllerPlayable controller) { _ = controller; }

    public virtual void OnStateMachineEnter(Animator animator, int stateMachinePathHash) { }
    public virtual void OnStateMachineExit(Animator animator, int stateMachinePathHash) { }
    public virtual void OnStateMachineEnter(Animator animator, int stateMachinePathHash, AnimatorControllerPlayable controller) { _ = controller; }
    public virtual void OnStateMachineExit(Animator animator, int stateMachinePathHash, AnimatorControllerPlayable controller) { _ = controller; }
}

public struct AnimatorControllerPlayable
{
    public static AnimatorControllerPlayable Null => default;
    public bool IsValid() => false;
    public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex) { _ = layerIndex; return default; }
    public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex) { _ = layerIndex; return default; }
}
