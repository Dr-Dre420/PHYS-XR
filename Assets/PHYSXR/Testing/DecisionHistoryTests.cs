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

            history.Update(GhostAction.FOLLOW, 100L, relevantEventOccurred: false);

            AssertTrue(history.PreviousAction == GhostAction.FOLLOW, "expected first action to become the previous action");
            AssertTrue(history.TimeEnteredCurrentAction == 100L, "expected time-entered to be the first timestamp supplied");
            AssertTrue(history.TimeInCurrentAction(100L) == 0L, "expected zero time-in-state at the moment the action was entered");
        }

        private static void Test_DH03()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.FOLLOW, 100L, relevantEventOccurred: false);
            history.Update(GhostAction.FOLLOW, 150L, relevantEventOccurred: false);
            history.Update(GhostAction.FOLLOW, 300L, relevantEventOccurred: false);

            AssertTrue(history.TimeEnteredCurrentAction == 100L, "expected time-entered to stay at the first timestamp while the action repeats");
            AssertTrue(history.TimeInCurrentAction(400L) == 300L, "expected time-in-state to accumulate across repeated identical actions");
        }

        private static void Test_DH04()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.FOLLOW, 100L, relevantEventOccurred: false);
            history.Update(GhostAction.FOLLOW, 200L, relevantEventOccurred: false);
            history.Update(GhostAction.WARN, 250L, relevantEventOccurred: false);

            AssertTrue(history.PreviousAction == GhostAction.WARN, "expected the new action to become the previous action");
            AssertTrue(history.TimeEnteredCurrentAction == 250L, "expected an action transition to reset time-entered to the transition timestamp");
            AssertTrue(history.TimeInCurrentAction(300L) == 50L, "expected time-in-state to be measured from the transition, not the original action");
        }

        private static void Test_DH05()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.FOLLOW, 100L, relevantEventOccurred: true);
            AssertTrue(history.TimeSinceLastEvent(150L) == 50L, "expected time-since-event to be measured from the relevant event");

            // A subsequent non-event update must not move LastEventTimestamp.
            history.Update(GhostAction.FOLLOW, 200L, relevantEventOccurred: false);
            AssertTrue(history.TimeSinceLastEvent(250L) == 150L, "expected time-since-event to keep referencing the last RELEVANT event, not the last update");

            history.Update(GhostAction.WARN, 300L, relevantEventOccurred: true);
            AssertTrue(history.TimeSinceLastEvent(320L) == 20L, "expected a new relevant event to advance time-since-event");
        }

        private static void Test_DH06()
        {
            var history = new DecisionHistory();

            for (int i = 1; i <= 7; i++)
                history.Update(GhostAction.WARN, i * 10L, relevantEventOccurred: true);

            List<long> window = history.RecentEventTimestamps.ToList();

            AssertTrue(window.Count == 5, "expected the recent-occurrence window to stay bounded at 5 entries after 7 events");
            AssertTrue(window[0] == 30L, "expected the oldest surviving entry to be the 3rd event (FIFO eviction of the first two)");
            AssertTrue(window[4] == 70L, "expected the newest entry to be the 7th event");
        }

        private static void Test_DH07()
        {
            var history = new DecisionHistory();

            history.Update(GhostAction.FOLLOW, 100L, relevantEventOccurred: true);
            history.Update(GhostAction.WARN, 200L, relevantEventOccurred: true);
            history.Update(GhostAction.INVESTIGATE, 300L, relevantEventOccurred: true);

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
                history.Update(GhostAction.FOLLOW, 100L, relevantEventOccurred: false);
                history.Update(GhostAction.WARN, 150L, relevantEventOccurred: true);
                history.Update(GhostAction.WARN, 220L, relevantEventOccurred: false);
                history.Update(GhostAction.INVESTIGATE, 260L, relevantEventOccurred: true);
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

            historyA.Update(GhostAction.PROTECT, 500L, relevantEventOccurred: true);

            AssertTrue(historyA.PreviousAction == GhostAction.PROTECT, "expected historyA to reflect its own update");
            AssertTrue(!historyB.PreviousAction.HasValue, "expected historyB to be unaffected by historyA's update");
            AssertTrue(historyB.RecentEventTimestamps.Count == 0, "expected historyB's recent-event window to be unaffected by historyA's update");
        }
    }
}
