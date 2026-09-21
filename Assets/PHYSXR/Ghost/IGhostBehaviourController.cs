namespace PHYSXR.Ghost
{
    // Minimal Unity-facing behaviour boundary, mirroring the
    // IDecisionProvider/IGhostActionExecutor plug-in shape. Consumes a
    // GhostExecutionState already produced elsewhere - it does not decide
    // or execute concrete behaviour, only holds/applies the current one.
    public interface IGhostBehaviourController
    {
        void ApplyState(GhostExecutionState state);

        GhostExecutionState CurrentState { get; }
    }
}
