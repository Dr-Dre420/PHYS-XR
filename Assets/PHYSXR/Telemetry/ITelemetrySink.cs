namespace PHYSXR.Telemetry
{
    // Minimal telemetry boundary, mirroring the project's other single-
    // method plug-in interfaces (IDecisionProvider, IGhostActionExecutor,
    // IGhostBehaviourController). Purely observational: nothing upstream
    // depends on this interface, and implementations must never feed
    // information back into decision-making.
    public interface ITelemetrySink
    {
        void Record(TelemetryRecord record);
    }
}
