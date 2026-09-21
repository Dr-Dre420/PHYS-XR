using PHYSXR.Core.Enums;

namespace PHYSXR.Ghost
{
    // Minimal execution-boundary interface, mirroring IDecisionProvider's
    // plug-in shape. Consumes a GhostAction already chosen elsewhere and
    // translates it into an execution-level representation - it does not
    // decide which action should occur.
    public interface IGhostActionExecutor
    {
        GhostExecutionState Execute(GhostAction action);
    }
}
