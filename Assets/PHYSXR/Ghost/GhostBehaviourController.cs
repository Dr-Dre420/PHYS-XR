using System;

namespace PHYSXR.Ghost
{
    // Plain C# state holder / application boundary for GhostExecutionState.
    // This is intentionally only that - a validated place to store "what
    // behaviour state is the Ghost currently in" - and nothing more. It
    // does not know about WorldState, GhostAction, IDecisionProvider,
    // RuleBasedDecisionProvider or IGhostActionExecutor, contains no
    // decision logic, and has no UnityEngine/XR/Animator/NavMesh/audio/
    // VFX/movement/networking/sensor dependency. Concrete Unity behaviour
    // is deliberately not implemented here yet.
    public class GhostBehaviourController : IGhostBehaviourController
    {
        public GhostExecutionState CurrentState { get; private set; } = GhostExecutionState.Following;

        public void ApplyState(GhostExecutionState state)
        {
            if (!Enum.IsDefined(typeof(GhostExecutionState), state))
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unhandled GhostExecutionState value");

            CurrentState = state;
        }
    }
}
