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
    //   3. How long it has been since the last "relevant physical event".
    //   4. A small bounded window of the most recent relevant-event
    //      timestamps (for a future repeated-occurrence check).
    //
    // Deliberately NOT a generic event-history framework and NOT an
    // unbounded list - the recent-event window is capped at
    // MaxRecentOccurrences and older entries are evicted FIFO.
    //
    // Deterministic by construction: every timestamp this class ever sees
    // is supplied explicitly by the caller (RuleBasedDecisionProvider,
    // itself fed by WorldState.physical.timestamp - see that class for
    // why this is the chosen time source). This class never reads any
    // wall-clock/runtime time source itself.
    //
    // This is foundation only: nothing here changes any decision output.
    // RuleBasedDecisionProvider records into this after a decision has
    // already been made, and does not yet read from it.
    public class DecisionHistory
    {
        // "Approximately 3-5 recent occurrences" per spec.
        private const int MaxRecentOccurrences = 5;

        private readonly Queue<long> recentEventTimestamps = new Queue<long>();

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

        // Records a relevant physical event at the given timestamp,
        // updating LastEventTimestamp and pushing into the bounded
        // recent-occurrence window (evicting the oldest entry beyond
        // MaxRecentOccurrences).
        public void RecordEvent(long timestamp)
        {
            LastEventTimestamp = timestamp;

            recentEventTimestamps.Enqueue(timestamp);
            while (recentEventTimestamps.Count > MaxRecentOccurrences)
                recentEventTimestamps.Dequeue();
        }

        // Convenience combining both updates for a single tick - the
        // shape RuleBasedDecisionProvider actually calls.
        public void Update(GhostAction action, long timestamp, bool relevantEventOccurred)
        {
            RecordAction(action, timestamp);

            if (relevantEventOccurred)
                RecordEvent(timestamp);
        }

        // Clears all temporal history back to a fresh/neutral state.
        // Never called automatically by Update() - only an explicit
        // caller (RuleBasedDecisionProvider.Reset()) triggers this.
        public void Reset()
        {
            PreviousAction = null;
            TimeEnteredCurrentAction = 0L;
            LastEventTimestamp = null;
            recentEventTimestamps.Clear();
        }
    }
}
