using PHYSXR.Core.Data;
using PHYSXR.Core.Enums;
using PHYSXR.Core.Interfaces;

namespace PHYSXR.Decision
{
    // Deterministic MVP decision provider (architecture ARCH-05). Implements
    // the approved first-match-wins rule set R1-R8. Consumes ONLY WorldState:
    // no sensor access, no networking, no Unity API dependency, no
    // randomness, no timers.
    //
    // The source architecture explicitly leaves exact numeric thresholds
    // unfrozen (implementation/calibration parameters, not architecture
    // requirements). ElevatedThreatThreshold/CriticalThreatThreshold are
    // therefore constructor parameters with sensible temporary defaults,
    // not hardcoded magic numbers, so they can be recalibrated later
    // without touching the rule logic.
    public class RuleBasedDecisionProvider : IDecisionProvider
    {
        // Calibration parameters - NOT frozen architecture values.
        public float ElevatedThreatThreshold { get; }
        public float CriticalThreatThreshold { get; }

        // Explainability (architecture §8.4): read-only reason for the most
        // recently returned decision. Decide() never reads this field, so
        // it cannot influence the decision - it is written only after the
        // action has already been selected.
        public string LastDecisionReason { get; private set; }
            = "FOLLOW: no active threat or event requiring intervention";

        // Deterministic temporal-context foundation (owned, not injected -
        // there is only ever one per provider instance). Nothing in
        // Decide() below reads from this yet: it is populated for a later
        // phase (hysteresis/context-aware decisions) but does not itself
        // influence which GhostAction is returned, exactly like
        // LastDecisionReason above.
        //
        // Exposed read-only so tests and a future phase can inspect it,
        // but it is not part of IDecisionProvider and is not meant to be
        // read by anything outside the decision layer.
        private readonly DecisionHistory temporalContext = new DecisionHistory();
        public DecisionHistory TemporalContext => temporalContext;

        public RuleBasedDecisionProvider(
            float elevatedThreatThreshold = 0.4f,
            float criticalThreatThreshold = 0.7f)
        {
            ElevatedThreatThreshold = elevatedThreatThreshold;
            CriticalThreatThreshold = criticalThreatThreshold;
        }

        // Clears the temporal context back to a fresh/neutral state.
        // Does not affect ElevatedThreatThreshold/CriticalThreatThreshold
        // or LastDecisionReason - only the temporal-context foundation.
        // Never called automatically; a runtime restart (new provider
        // instance) naturally starts clean without needing this.
        public void Reset()
        {
            temporalContext.Reset();
        }

        public GhostAction Decide(WorldState state)
        {
            PhysicalState physical = state?.physical;
            PlayerState player = state?.player;
            VirtualState virtualWorld = state?.virtualWorld;

            // PlayerState.movement, GhostState.currentState and
            // PhysicalState.timestamp are deliberately not read here per the
            // approved MVP rule policy - they remain valid WorldState fields
            // for future revisions.
            PresenceLevel presence = physical?.presence ?? PresenceLevel.NONE;
            ConfidenceLevel confidence = physical?.confidence ?? ConfidenceLevel.LOW;
            bool disturbance = physical?.disturbance ?? false;

            bool inThreatZone = player?.inThreatZone ?? false;

            float threatLevel = virtualWorld?.threatLevel ?? 0f;
            bool eventActive = virtualWorld?.eventActive ?? false;

            // R1 - CRITICAL PLAYER RISK
            if (inThreatZone && presence == PresenceLevel.VERY_NEAR && confidence == ConfidenceLevel.HIGH)
                return Resolve(GhostAction.PROTECT, "PROTECT: player at risk + high-confidence VERY_NEAR physical presence", state);

            // R2 - CRITICAL VIRTUAL/GENERAL THREAT
            if (threatLevel >= CriticalThreatThreshold && !inThreatZone)
                return Resolve(GhostAction.RETREAT, "RETREAT: sufficiently high threat without active player-risk condition", state);

            // R3 - QUALIFIED PHYSICAL EVENT
            if ((presence == PresenceLevel.NEAR || presence == PresenceLevel.VERY_NEAR)
                && disturbance
                && confidence == ConfidenceLevel.HIGH
                && !inThreatZone)
                return Resolve(GhostAction.INVESTIGATE, "INVESTIGATE: qualified physical disturbance detected", state);

            // R4 - UNCERTAIN PHYSICAL CONDITION
            if (presence == PresenceLevel.NEAR && confidence == ConfidenceLevel.LOW && !inThreatZone)
                return Resolve(GhostAction.WARN, "WARN: physical condition detected with low confidence", state);

            // R5 - QUALIFIED DISTURBANCE WITHOUT PROXIMITY
            if (disturbance && presence == PresenceLevel.NONE && !inThreatZone)
                return Resolve(GhostAction.WARN, "WARN: disturbance detected without proximity confirmation", state);

            // R6 - VIRTUAL EVENT (MVP: never INVESTIGATE for a purely virtual event)
            if (eventActive && presence == PresenceLevel.NONE && !inThreatZone && threatLevel < CriticalThreatThreshold)
                return Resolve(GhostAction.WARN, "WARN: virtual event detected", state);

            // R7 - ELEVATED VIRTUAL THREAT
            if (threatLevel >= ElevatedThreatThreshold
                && threatLevel < CriticalThreatThreshold
                && !inThreatZone
                && presence == PresenceLevel.NONE)
                return Resolve(GhostAction.WARN, "WARN: elevated virtual threat", state);

            // R8 - NORMAL
            return Resolve(GhostAction.FOLLOW, "FOLLOW: no active threat or event requiring intervention", state);
        }

        private GhostAction Resolve(GhostAction action, string reason, WorldState state)
        {
            LastDecisionReason = reason;

            // Temporal-context bookkeeping only - runs strictly after the
            // action has already been selected above, so it cannot affect
            // which action is returned (same guarantee LastDecisionReason
            // already has). WorldState.physical.timestamp is the
            // deterministic time source: it is already supplied by every
            // caller/test on every WorldState, so no new parameter or
            // clock abstraction is needed to keep Decide(WorldState)
            // deterministic and interface-frozen.
            long timestamp = state?.physical?.timestamp ?? 0L;
            bool relevantEvent = state?.physical?.disturbance ?? false;
            temporalContext.Update(action, timestamp, relevantEvent);

            return action;
        }
    }
}
