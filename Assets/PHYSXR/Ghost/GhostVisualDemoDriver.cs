using UnityEngine;

namespace PHYSXR.Ghost
{
    // ================================================================
    // DEVELOPMENT-ONLY.
    //
    // Not part of the production decision/execution pipeline. Never
    // called by GhostRuntime, GhostRuntimePipeline, GhostBehaviourController,
    // or GhostVisualBinding, and contains no decision logic of its own -
    // it only ever forwards one of the five already-defined
    // GhostExecutionState values directly to GhostVisualController.
    //
    // Purpose: let a developer visually confirm all five GhostExecutionState
    // presentations are distinguishable without needing ESP32, Quest, or a
    // fully wired WorldStateManager/decision pipeline running in the scene.
    //
    // Usage: in Play mode (or Edit mode), right-click this component in the
    // Inspector and choose one of the "Apply <State>" entries, or
    // "Cycle To Next State" to step through all five in order. Intentionally
    // does not poll the legacy UnityEngine.Input class or the Input System
    // package, to avoid any extra runtime dependency for a dev-only tool.
    // ================================================================
    public class GhostVisualDemoDriver : MonoBehaviour
    {
        [SerializeField] private GhostVisualController visualController;

        private static readonly GhostExecutionState[] AllStates =
        {
            GhostExecutionState.Following,
            GhostExecutionState.Warning,
            GhostExecutionState.Investigating,
            GhostExecutionState.Protecting,
            GhostExecutionState.Retreating
        };

        private int cycleIndex;

        [ContextMenu("Apply Following")]
        private void ApplyFollowing() => Apply(GhostExecutionState.Following);

        [ContextMenu("Apply Warning")]
        private void ApplyWarning() => Apply(GhostExecutionState.Warning);

        [ContextMenu("Apply Investigating")]
        private void ApplyInvestigating() => Apply(GhostExecutionState.Investigating);

        [ContextMenu("Apply Protecting")]
        private void ApplyProtecting() => Apply(GhostExecutionState.Protecting);

        [ContextMenu("Apply Retreating")]
        private void ApplyRetreating() => Apply(GhostExecutionState.Retreating);

        [ContextMenu("Cycle To Next State")]
        private void CycleToNextState()
        {
            cycleIndex = (cycleIndex + 1) % AllStates.Length;
            Apply(AllStates[cycleIndex]);
        }

        private void Apply(GhostExecutionState state)
        {
            if (visualController == null)
            {
                Debug.LogWarning("GhostVisualDemoDriver: no GhostVisualController assigned.");
                return;
            }

            visualController.ApplyState(state);
            Debug.Log("GhostVisualDemoDriver (dev-only): applied " + state);
        }
    }
}
