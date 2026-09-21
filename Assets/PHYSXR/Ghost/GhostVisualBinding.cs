using UnityEngine;

namespace PHYSXR.Ghost
{
    // Small, explicit Unity-facing adapter connecting GhostRuntime's
    // read-only CurrentState to GhostVisualController.ApplyState().
    //
    // This exists so GhostVisualController never needs to know GhostRuntime
    // exists (it only ever receives a GhostExecutionState), and so
    // GhostRuntime/GhostRuntimePipeline/GhostBehaviourController never need
    // to know a visual layer exists at all - the core pipeline is
    // completely unmodified by this class's presence.
    //
    // No singleton, no FindObjectOfType/FindFirstObjectByType, no static
    // state: both references are explicit, inspector-assigned fields.
    // GhostRuntime.CurrentState is a plain property (not an event), so this
    // polls it once per frame and only forwards a call when the value has
    // actually changed. Update() is appropriate here specifically because
    // this is a presentation-sync component, not the decision/execution
    // pipeline itself - GhostRuntime.Tick() still has no Awake()/Update()
    // of its own and must still be driven explicitly from elsewhere,
    // exactly as before this file existed.
    public class GhostVisualBinding : MonoBehaviour
    {
        [SerializeField] private GhostRuntime ghostRuntime;
        [SerializeField] private GhostVisualController visualController;

        private GhostExecutionState? lastAppliedState;

        private void Update()
        {
            if (ghostRuntime == null || visualController == null)
                return;

            GhostExecutionState current = ghostRuntime.CurrentState;
            if (lastAppliedState.HasValue && lastAppliedState.Value == current)
                return;

            visualController.ApplyState(current);
            lastAppliedState = current;
        }
    }
}
