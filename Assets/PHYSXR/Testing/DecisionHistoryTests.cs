using System;
using System.Collections.Generic;
using System.Linq;
using PHYSXR.Core.Enums;
using PHYSXR.Decision;

namespace PHYSXR.Testing
{
    // Dependency-light tests for DecisionHistory, the deterministic
    // temporal-context foundation owned by RuleBasedDecisionProvider.
    // Same style as the other *Tests classes: plain static checks, no
    // NUnit/UnityEngine.TestRunner asmdef required.
    public static class DecisionHistoryTests
    {
        public static int RunAll()
        {
            int passed = 0;
            int failed = 0;

            void Run(string name, Action test)
            {
                try
                {
                    test();
                    passed++;
                    Console.WriteLine("PASS: " + name);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("FAIL: " + name + " - " + ex.Message);
                }
            }

            Run("DH-01_FreshContext_HasNoPreviousAction", Test_DH01);
            Run("DH-02_FirstAction_EstablishesPreviousAction", Test_DH02);
            Run("DH-03_RepeatedSameAction_AccumulatesTimeInState", Test_DH03);
            Run("DH-04_ActionTransition_ResetsTimeInCurrentAction", Test_DH04);
            Run("DH-05_RelevantEvent_UpdatesTimeSinceLastEvent", Test_DH05);
            Run("DH-06_RepeatedOccurrenceWindow_RemainsBounded", Test_DH06);
            Run("DH-07_Reset_CompletelyClearsHistory", Test_DH07);
            Run("DH-08_IdenticalInputs_ProduceIdenticalContext", Test_DH08);
            Run("DH-09_TwoInstances_DoNotShareState", Test_DH09);
            Run("DH-10_SustainedDisturbance_ProducesSingleOccurrence", Test_DH10);
            Run("DH-11_DistinctDisturbanceOccurrences_ProduceTwoEntries", Test_DH11);
            Run("DH-12_DuplicateTimestampRisingEdges_BothRecordedInWindow", Test_DH12);
            Run("DH-13_Reset_ClearsDisturbanceEdgeStateToo", Test_DH13);
            Run("DH-14_ActionReEntry_StartsFreshDurationInterval", Test_DH14);
            Run("DH-15_BackwardTimestamp_ProducesNegativeDurationWithoutCorrection", Test_DH15);

            Console.WriteLine(string.Format("Total: {0}, Passed: {1}, Failed: {2}", passed + failed, passed, failed));
            return failed;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static void Test_DH01()
        {
            var history = new DecisionHistory();

            AssertTrue(!history.PreviousAction.HasValue, "expected no previous action on a fresh context");
            AssertTrue(history.TimeInCurrentAction(1000L) == 0L, "expected zero time-in-state before any action recorded");
            AssertTrue(!history.TimeSinceLastEvent(1000L).HasValue, "expected no time-since-event before any event recorded");
            AssertTrue(history.RecentEventTimestamps.Count == 0, "expected empty recent-event window on a fresh context");
        }

        private static void Test_DH02()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.FOLLOW, 100L, disturbanceActive: false);

            AssertTrue(history.PreviousAction == GhostAction.FOLLOW, "expected first action to become the previous action");
            AssertTrue(history.TimeEnteredCurrentAction == 100L, "expected time-entered to be the first timestamp supplied");
            AssertTrue(history.TimeInCurrentAction(100L) == 0L, "expected zero time-in-state at the moment the action was entered");
        }

        private static void Test_DH03()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.FOLLOW, 100L, disturbanceActive: false);
            history.Update(GhostAction.FOLLOW, 150L, disturbanceActive: false);
            history.Update(GhostAction.FOLLOW, 300L, disturbanceActive: false);

            AssertTrue(history.TimeEnteredCurrentAction == 100L, "expected time-entered to stay at the first timestamp while the action repeats");
            AssertTrue(history.TimeInCurrentAction(400L) == 300L, "expected time-in-state to accumulate across repeated identical actions");
        }

        private static void Test_DH04()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.FOLLOW, 100L, disturbanceActive: false);
            history.Update(GhostAction.FOLLOW, 200L, disturbanceActive: false);
            history.Update(GhostAction.WARN, 250L, disturbanceActive: false);

            AssertTrue(history.PreviousAction == GhostAction.WARN, "expected the new action to become the previous action");
            AssertTrue(history.TimeEnteredCurrentAction == 250L, "expected an action transition to reset time-entered to the transition timestamp");
            AssertTrue(history.TimeInCurrentAction(300L) == 50L, "expected time-in-state to be measured from the transition, not the original action");
        }

        private static void Test_DH05()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.FOLLOW, 100L, disturbanceActive: true);
            AssertTrue(history.TimeSinceLastEvent(150L) == 50L, "expected time-since-event to be measured from the relevant event");

            // A subsequent non-event update must not move LastEventTimestamp.
            history.Update(GhostAction.FOLLOW, 200L, disturbanceActive: false);
            AssertTrue(history.TimeSinceLastEvent(250L) == 150L, "expected time-since-event to keep referencing the last RELEVANT event, not the last update");

            history.Update(GhostAction.WARN, 300L, disturbanceActive: true);
            AssertTrue(history.TimeSinceLastEvent(320L) == 20L, "expected a new relevant event to advance time-since-event");
        }

        private static void Test_DH06()
        {
            var history = new DecisionHistory();

            // 7 DISTINCT rising edges (each true sample preceded by a
            // false sample), not 7 consecutive true samples - under
            // rising-edge semantics, consecutive true samples would
            // collapse to a single occurrence (see DH-10), so the window
            // test must actually alternate to produce 7 real occurrences.
            for (int i = 1; i <= 7; i++)
            {
                history.Update(GhostAction.WARN, i * 10L - 5L, disturbanceActive: false);
                history.Update(GhostAction.WARN, i * 10L, disturbanceActive: true);
            }

            List<long> window = history.RecentEventTimestamps.ToList();

            AssertTrue(window.Count == 5, "expected the recent-occurrence window to stay bounded at 5 entries after 7 occurrences");
            AssertTrue(window[0] == 30L, "expected the oldest surviving entry to be the 3rd occurrence (FIFO eviction of the first two)");
            AssertTrue(window[4] == 70L, "expected the newest entry to be the 7th occurrence");
        }

        private static void Test_DH07()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.FOLLOW, 100L, disturbanceActive: true);
            history.Update(GhostAction.WARN, 200L, disturbanceActive: true);
            history.Update(GhostAction.INVESTIGATE, 300L, disturbanceActive: true);

            history.Reset();

            AssertTrue(!history.PreviousAction.HasValue, "expected Reset() to clear the previous action");
            AssertTrue(history.TimeInCurrentAction(1000L) == 0L, "expected Reset() to clear time-in-state");
            AssertTrue(!history.TimeSinceLastEvent(1000L).HasValue, "expected Reset() to clear time-since-event");
            AssertTrue(history.RecentEventTimestamps.Count == 0, "expected Reset() to clear the recent-event window");
        }

        private static void Test_DH08()
        {
            var historyA = new DecisionHistory();
            var historyB = new DecisionHistory();

            void ApplySameSequence(DecisionHistory history)
            {
                history.Update(GhostAction.FOLLOW, 100L, disturbanceActive: false);
                history.Update(GhostAction.WARN, 150L, disturbanceActive: true);
                history.Update(GhostAction.WARN, 220L, disturbanceActive: false);
                history.Update(GhostAction.INVESTIGATE, 260L, disturbanceActive: true);
            }

            ApplySameSequence(historyA);
            ApplySameSequence(historyB);

            AssertTrue(historyA.PreviousAction == historyB.PreviousAction, "expected identical sequences to produce identical previous action");
            AssertTrue(historyA.TimeInCurrentAction(300L) == historyB.TimeInCurrentAction(300L), "expected identical sequences to produce identical time-in-state");
            AssertTrue(historyA.TimeSinceLastEvent(300L) == historyB.TimeSinceLastEvent(300L), "expected identical sequences to produce identical time-since-event");
            AssertTrue(historyA.RecentEventTimestamps.SequenceEqual(historyB.RecentEventTimestamps), "expected identical sequences to produce identical recent-event windows");
        }

        private static void Test_DH09()
        {
            var historyA = new DecisionHistory();
            var historyB = new DecisionHistory();

            historyA.Update(GhostAction.PROTECT, 500L, disturbanceActive: true);

            AssertTrue(historyA.PreviousAction == GhostAction.PROTECT, "expected historyA to reflect its own update");
            AssertTrue(!historyB.PreviousAction.HasValue, "expected historyB to be unaffected by historyA's update");
            AssertTrue(historyB.RecentEventTimestamps.Count == 0, "expected historyB's recent-event window to be unaffected by historyA's update");
        }

        // A. Sustained disturbance: false -> true -> true -> true must
        // produce exactly ONE recent-event occurrence, timestamped at the
        // FIRST true sample (the rising edge), not at every true sample.
        private static void Test_DH10()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.WARN, 100L, disturbanceActive: false);
            history.Update(GhostAction.WARN, 110L, disturbanceActive: true);
            history.Update(GhostAction.WARN, 120L, disturbanceActive: true);
            history.Update(GhostAction.WARN, 130L, disturbanceActive: true);

            AssertTrue(history.RecentEventTimestamps.Count == 1, "expected a sustained disturbance to produce exactly one occurrence");
            AssertTrue(history.RecentEventTimestamps.First() == 110L, "expected the occurrence timestamp to be the first true sample (the rising edge)");
            AssertTrue(history.LastEventTimestamp == 110L, "expected LastEventTimestamp to stay at the rising edge, not advance on continued true samples");
        }

        // B. Distinct disturbance occurrences: false -> true -> false ->
        // true must produce exactly two occurrences, at the two rising
        // edges.
        private static void Test_DH11()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.WARN, 100L, disturbanceActive: false);
            history.Update(GhostAction.WARN, 110L, disturbanceActive: true);
            history.Update(GhostAction.WARN, 120L, disturbanceActive: false);
            history.Update(GhostAction.WARN, 130L, disturbanceActive: true);

            List<long> window = history.RecentEventTimestamps.ToList();

            AssertTrue(window.Count == 2, "expected two distinct disturbance occurrences to produce two entries");
            AssertTrue(window[0] == 110L, "expected the first occurrence to be at the first rising edge");
            AssertTrue(window[1] == 130L, "expected the second occurrence to be at the second rising edge");
            AssertTrue(history.LastEventTimestamp == 130L, "expected LastEventTimestamp to reflect the most recent rising edge");
        }

        // C. Duplicate timestamp: two rising edges that happen to carry
        // the identical timestamp are NOT deduplicated - both occupy a
        // window slot. This documents the intended (non-deduplicating)
        // queue behaviour explicitly.
        private static void Test_DH12()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.WARN, 100L, disturbanceActive: false);
            history.Update(GhostAction.WARN, 100L, disturbanceActive: true);  // rising edge #1 @ 100
            history.Update(GhostAction.WARN, 100L, disturbanceActive: false);
            history.Update(GhostAction.WARN, 100L, disturbanceActive: true);  // rising edge #2 @ 100 (same timestamp)

            List<long> window = history.RecentEventTimestamps.ToList();

            AssertTrue(window.Count == 2, "expected two distinct rising edges to both be recorded even though they share the same timestamp");
            AssertTrue(window[0] == 100L && window[1] == 100L, "expected both window entries to carry the duplicate timestamp, unmodified");
        }

        // D. Reset must clear disturbance-edge tracking, not just the
        // publicly-observable fields - after Reset(), an immediate true
        // sample must be treated as a NEW rising edge rather than a
        // continuation of whatever was active before the reset.
        private static void Test_DH13()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.WARN, 100L, disturbanceActive: false);
            history.Update(GhostAction.WARN, 110L, disturbanceActive: true);  // rising edge -> disturbanceWasActive becomes true

            history.Reset();

            AssertTrue(!history.PreviousAction.HasValue, "expected Reset() to clear the previous action");
            AssertTrue(history.TimeInCurrentAction(1000L) == 0L, "expected Reset() to clear time-in-state");
            AssertTrue(!history.TimeSinceLastEvent(1000L).HasValue, "expected Reset() to clear time-since-event");
            AssertTrue(history.RecentEventTimestamps.Count == 0, "expected Reset() to clear the recent-event window");

            // If disturbanceWasActive were NOT cleared by Reset(), this
            // immediate true sample would be seen as true->true (no new
            // edge) and would fail to register an occurrence.
            history.Update(GhostAction.WARN, 500L, disturbanceActive: true);

            AssertTrue(history.LastEventTimestamp == 500L, "expected the edge-tracking bit to also be cleared by Reset(), so a post-reset true sample is a fresh occurrence");
            AssertTrue(history.RecentEventTimestamps.Count == 1, "expected exactly one occurrence recorded after the post-reset true sample");
        }

        // E. A -> B -> A action sequence: re-entering a previously-visited
        // action must start a brand-new duration interval, not resume the
        // time accumulated the first time that action was active.
        private static void Test_DH14()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.FOLLOW, 100L, disturbanceActive: false); // A entered @100
            history.Update(GhostAction.WARN, 150L, disturbanceActive: false);   // B entered @150
            history.Update(GhostAction.FOLLOW, 200L, disturbanceActive: false); // A re-entered @200

            AssertTrue(history.PreviousAction == GhostAction.FOLLOW, "expected the re-entered action to be the previous action");
            AssertTrue(history.TimeEnteredCurrentAction == 200L, "expected re-entering A to reset time-entered to the re-entry timestamp, not the original 100");
            AssertTrue(history.TimeInCurrentAction(230L) == 30L, "expected time-in-state to be measured from the re-entry, not the original entry");
        }

        // F. Backward timestamp: documents/locks the EXISTING assumption
        // that callers supply non-decreasing timestamps (matching the
        // Communication layer's own upstream sequence enforcement).
        // DecisionHistory does not detect or silently correct a
        // backward-moving timestamp - this test pins down today's actual
        // behaviour (a negative duration) rather than inventing a new
        // policy.
        private static void Test_DH15()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.FOLLOW, 100L, disturbanceActive: false);
            history.Update(GhostAction.FOLLOW, 50L, disturbanceActive: false); // timestamp moves BACKWARD, same action

            // Same action, so TimeEnteredCurrentAction is untouched (it
            // only advances on an action CHANGE) - it is still 100, so
            // querying at a timestamp before that produces a negative
            // duration. This is today's documented behaviour, not a bug
            // fix target for this task.
            AssertTrue(history.TimeEnteredCurrentAction == 100L, "expected time-entered to remain at the original entry timestamp for a repeated action");
            AssertTrue(history.TimeInCurrentAction(50L) == -50L, "expected a backward timestamp to produce a negative duration, with no silent correction");

            // The same applies to a backward-timestamped rising edge:
            // LastEventTimestamp is simply overwritten, with no ordering
            // check.
            history.Update(GhostAction.FOLLOW, 40L, disturbanceActive: true); // rising edge at an even-earlier timestamp
            AssertTrue(history.LastEventTimestamp == 40L, "expected a backward-timestamped rising edge to simply overwrite LastEventTimestamp, with no correction");
        }
    }
}
