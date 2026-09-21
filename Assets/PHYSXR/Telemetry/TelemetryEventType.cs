namespace PHYSXR.Telemetry
{
    // The pipeline stages telemetry can observe, in the order they occur:
    // PhysicalState -> PhysicalEvent -> WorldState -> Decision -> GhostAction -> GhostExecutionState.
    // Purely descriptive - selecting/producing a value here is never this
    // module's responsibility, only recording that it happened.
    public enum TelemetryEventType
    {
        PhysicalStateUpdated,
        PhysicalEventRegistered,
        WorldStateEvaluated,
        DecisionMade,
        GhostActionExecuted,
        GhostExecutionStateChanged
    }
}
