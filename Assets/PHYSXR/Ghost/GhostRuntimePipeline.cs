using PHYSXR.Core.Data;
using PHYSXR.Core.Enums;
using PHYSXR.Decision;
using PHYSXR.Telemetry;

namespace PHYSXR.Ghost
{
    // Plain C#, Unity-independent orchestration extracted from
    // GhostRuntime.Tick() so the decide -> execute -> apply sequence (and
    // the optional telemetry emitted alongside it) stays directly testable
    // outside Unity, the same way every other stage of this pipeline
    // already is. GhostRuntime itself stays a thin MonoBehaviour wrapper:
    // it fetches WorldState from WorldStateManager and hands it here.
    //
    // TelemetrySink is optional (may be left null) and purely observational:
    // - it is never required for Run() to work correctly;
    // - a failing sink can never alter the resulting GhostAction/
    //   GhostExecutionState, because emitting a record is wrapped so any
    //   exception it throws is swallowed rather than propagated;
    // - no core component (RuleBasedDecisionProvider, GhostActionExecutor,
    //   GhostBehaviourController) was changed or given an ITelemetrySink
    //   dependency to make this possible - this class only reads their
    //   already-public outputs (GhostAction, LastDecisionReason,
    //   GhostExecutionState).
    public class GhostRuntimePipeline
    {
        private readonly RuleBasedDecisionProvider decisionProvider;
        private readonly IGhostActionExecutor actionExecutor;
        private readonly IGhostBehaviourController behaviourController;

        public ITelemetrySink TelemetrySink { get; set; }

        public GhostRuntimePipeline(
            RuleBasedDecisionProvider decisionProvider,
            IGhostActionExecutor actionExecutor,
            IGhostBehaviourController behaviourController)
        {
            this.decisionProvider = decisionProvider;
            this.actionExecutor = actionExecutor;
            this.behaviourController = behaviourController;
        }

        public GhostExecutionState Run(WorldState worldState, long timestamp)
        {
            Emit(TelemetryEventType.WorldStateEvaluated, timestamp);

            GhostAction action = decisionProvider.Decide(worldState);
            Emit(TelemetryEventType.DecisionMade, timestamp, ghostAction: action, reason: decisionProvider.LastDecisionReason);

            GhostExecutionState executionState = actionExecutor.Execute(action);
            Emit(TelemetryEventType.GhostActionExecuted, timestamp, ghostAction: action, ghostExecutionState: executionState);

            behaviourController.ApplyState(executionState);
            Emit(TelemetryEventType.GhostExecutionStateChanged, timestamp, ghostExecutionState: executionState);

            return executionState;
        }

        private void Emit(
            TelemetryEventType eventType,
            long timestamp,
            GhostAction? ghostAction = null,
            GhostExecutionState? ghostExecutionState = null,
            string reason = null)
        {
            if (TelemetrySink == null)
                return;

            try
            {
                TelemetrySink.Record(new TelemetryRecord
                {
                    timestamp = timestamp,
                    eventType = eventType,
                    ghostAction = ghostAction,
                    ghostExecutionState = ghostExecutionState,
                    reason = reason
                });
            }
            catch
            {
                // Telemetry must never affect the Ghost decision/execution
                // pipeline - a failing sink is swallowed here, not propagated.
            }
        }
    }
}
