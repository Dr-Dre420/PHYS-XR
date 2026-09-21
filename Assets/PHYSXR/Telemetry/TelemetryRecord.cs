using PHYSXR.Core.Enums;
using PHYSXR.Ghost;

namespace PHYSXR.Telemetry
{
    // A single observational telemetry entry. Plain data contract - same
    // convention as PHYSXR.Core.Data (public fields, no behaviour) - so it
    // stays a passive record, never a place decision logic could hide.
    //
    // Deliberately does NOT carry a full WorldState/PhysicalState snapshot:
    // only the small set of already-computed values (GhostAction,
    // GhostExecutionState, decision reason, an optional identifier and a
    // short free-text detail) needed to correlate a pipeline stage without
    // duplicating the source contracts.
    public class TelemetryRecord
    {
        public long timestamp;
        public TelemetryEventType eventType;

        // Relevant only for decision/action/state-change events; null otherwise.
        public GhostAction? ghostAction;
        public GhostExecutionState? ghostExecutionState;

        // Decision explainability (e.g. RuleBasedDecisionProvider.LastDecisionReason).
        public string reason;

        // An identifier already available upstream where relevant
        // (e.g. a PhysicalEvent.eventId) - not a new identity scheme.
        public long? relatedId;

        // Short free-text context (e.g. "presence=NEAR"), not a WorldState dump.
        public string detail;
    }
}
