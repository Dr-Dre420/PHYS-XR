using System.Collections.Generic;
using PHYSXR.Core.Enums;

namespace PHYSXR.Decision
{
    // Small, bounded temporal-context component owned by
    // RuleBasedDecisionProvider (not a standalone IDecisionProvider, not a
    // decorator - there is no need for either yet).
    //
    // Tracks exactly four things, nothing more:
    //   1. The previous GhostAction returned by the owning provider.
    //   2. How long the provider has been in its current action
    //      (via the timestamp at which that action was first entered).
    //   3. How long it has been since the last relevant physical-event
    //      OCCURRENCE (a rising edge - see below).
    //   4. A small bounded window of the most recent occurrence
    //      timestamps (for a future repeated-occurrence check).
    //
    // Deliberately NOT a generic event-history framework and NOT an
    // unbounded list - the recent-event window is capped at
    // MaxRecentOccurrences and older entries are evicted FIFO.
    //
    // RISING-EDGE SEMANTICS (architectural-review correction): the caller
    // supplies the raw, continuously-sampled disturbance condition on
    // every tick (WorldState.physical.disturbance can be true across many
    // consecutive samples for one ongoing physical condition). This class
    // treats only a false->true transition as a new "occurrence" -
    // false->true = one new occurrence, true->true = the same ongoing
    // occurrence (recorded once, not again), true->false = the occurrence
    // ending (recorded as nothing further). This prevents one sustained
    // disturbance from being miscounted as several distinct occurrences
    // in RecentEventTimestamps. The one extra bit of state this requires
    // (disturbanceWasActive) is exactly that - a single bit, not a
    // generic event-history framework.
    //
    // Deterministic by construction: every timestamp this class ever sees
    // is supplied explicitly by the caller (RuleBasedDecisionProvider,
    // itself fed by WorldState.physical.timestamp - see that class for
    // why this is the chosen time source). This class never reads any
    // wall-clock/runtime time source itself.
    //
    // Timestamps are assumed non-decreasing across calls, matching the
    // rest of the architecture (the Communication layer already enforces
    // sequence ordering upstream). This class does not detect or correct
    // backward-moving timestamps - see DecisionHistoryTests for a
    // regression test that documents/locks this assumption rather than
    // silently inventing correction behaviour.
    //
    // This is foundation only: nothing here changes any decision output.
    // RuleBasedDecisionProvider records into this after a decision has
    // already been made, and does not yet read from it.
    public class DecisionHistory
    {
        // "Approximately 3-5 recent occurrences" per spec.
        private const int MaxRecentOccurrences = 5;

        private readonly Queue<long> recentEventTimestamps = new Queue<long>();

        // The previous call's raw disturbance sample, used only to detect
        // a false->true rising edge on the next call. Not exposed
        // publicly - it is bookkeeping for Update(), not part of the
        // observable temporal context.
        private bool disturbanceWasActive;

        public GhostAction? PreviousAction { get; private set; }
        public long TimeEnteredCurrentAction { get; private set; }
        public long? LastEventTimestamp { get; private set; }

        // Oldest-first snapshot of the bounded recent-event window.
        public IReadOnlyCollection<long> RecentEventTimestamps => recentEventTimestamps;

        // Time spent in the current action, as of currentTimestamp.
        // Zero before any action has ever been recorded.
        public long TimeInCurrentAction(long currentTimestamp)
        {
            if (!PreviousAction.HasValue)
                return 0L;

            return currentTimestamp - TimeEnteredCurrentAction;
        }

        // Time since the last relevant physical event, as of
        // currentTimestamp. Null if no relevant event has ever occurred.
        public long? TimeSinceLastEvent(long currentTimestamp)
        {
            if (!LastEventTimestamp.HasValue)
                return null;

            return currentTimestamp - LastEventTimestamp.Value;
        }

        // Records the action selected for this tick. Only advances
        // TimeEnteredCurrentAction when the action actually changes, so
        // repeated identical actions accumulate time-in-state rather than
        // resetting it.
        public void RecordAction(GhostAction action, long timestamp)
        {
            if (!PreviousAction.HasValue || PreviousAction.Value != action)
                TimeEnteredCurrentAction = timestamp;

            PreviousAction = action;
        }

        // Records a disturbance-occurrence (rising-edge) at the given
        // timestamp, updating LastEventTimestamp and pushing into the
        // bounded recent-occurrence window (evicting the oldest entry
        // beyond MaxRecentOccurrences). Duplicate timestamps are not
        // deduplicated - two distinct rising edges that happen to carry
        // the same timestamp both occupy a window slot; see
        // DecisionHistoryTests for the explicit, intended behaviour.
        public void RecordEvent(long timestamp)
        {
            LastEventTimestamp = timestamp;

            recentEventTimestamps.Enqueue(timestamp);
            while (recentEventTimestamps.Count > MaxRecentOccurrences)
                recentEventTimestamps.Dequeue();
        }

        // Convenience combining both updates for a single tick - the
        // shape RuleBasedDecisionProvider actually calls.
        //
        // disturbanceActive is the RAW, continuously-sampled disturbance
        // condition for this tick (WorldState.physical.disturbance), not
        // a pre-computed "is this a new event" flag. A relevant-event
        // occurrence is only recorded on a false->true transition
        // relative to the previous call - see the rising-edge semantics
        // documented on this class.
        public void Update(GhostAction action, long timestamp, bool disturbanceActive)
        {
            RecordAction(action, timestamp);

            bool isRisingEdge = disturbanceActive && !disturbanceWasActive;
            disturbanceWasActive = disturbanceActive;

            if (isRisingEdge)
                RecordEvent(timestamp);
        }

        // Clears all temporal history back to a fresh/neutral state,
        // including the disturbance rising-edge tracking bit - after
        // Reset(), the very next true sample is treated as a fresh
        // occurrence rather than a continuation of whatever was active
        // before the reset.
        // Never called automatically by Update() - only an explicit
        // caller (RuleBasedDecisionProvider.Reset()) triggers this.
        public void Reset()
        {
            PreviousAction = null;
            TimeEnteredCurrentAction = 0L;
            LastEventTimestamp = null;
            recentEventTimestamps.Clear();
            disturbanceWasActive = false;
        }
    }
}
