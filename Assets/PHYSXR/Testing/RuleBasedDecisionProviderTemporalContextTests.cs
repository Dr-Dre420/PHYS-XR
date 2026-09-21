using System;
using PHYSXR.Core.Data;
using PHYSXR.Core.Enums;
using PHYSXR.Decision;

namespace PHYSXR.Testing
{
    // Integration tests for RuleBasedDecisionProvider's ownership/wiring of
    // its DecisionHistory temporal-context foundation. Kept in its own
    // file (rather than appended to RuleBasedDecisionProviderTests.cs) so
    // the existing, already-passing DEC-01..13 suite is left completely
    // untouched by this task.
    //
    // Unit coverage of DecisionHistory itself lives in
    // DecisionHistoryTests.cs - these tests only check that
    // RuleBasedDecisionProvider wires it correctly and that doing so does
    // not change any decision output.
    public static class RuleBasedDecisionProviderTemporalContextTests
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

            Run("PDC-01_FreshProvider_TemporalContextIsNeutral", Test_PDC01);
            Run("PDC-02_Decide_RecordsActionAndTimestampFromWorldState", Test_PDC02);
            Run("PDC-03_Decide_RecordsRelevantEventFromDisturbance", Test_PDC03);
            Run("PDC-04_Reset_ClearsTemporalContextOnly", Test_PDC04);
            Run("PDC-05_TemporalContextTracking_DoesNotChangeDecisionOutput", Test_PDC05);
            Run("PDC-06_TwoProviders_DoNotShareTemporalContext", Test_PDC06);

            Console.WriteLine(string.Format("Total: {0}, Passed: {1}, Failed: {2}", passed + failed, passed, failed));
            return failed;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private const float ElevatedThreatThreshold = 0.4f;
        private const float CriticalThreatThreshold = 0.7f;

        private static WorldState BuildState(
            PresenceLevel presence = PresenceLevel.NONE,
            ConfidenceLevel confidence = ConfidenceLevel.LOW,
            bool disturbance = false,
            long timestamp = 0L,
            bool inThreatZone = false,
            float threatLevel = 0f,
            bool eventActive = false)
        {
            return new WorldState
            {
                physical = new PhysicalState
                {
                    presence = presence,
                    confidence = confidence,
                    disturbance = disturbance,
                    timestamp = timestamp
                },
                player = new PlayerState
                {
                    inThreatZone = inThreatZone
                },
                virtualWorld = new VirtualState
                {
                    threatLevel = threatLevel,
                    eventActive = eventActive
                },
                ghost = new GhostState()
            };
        }

        private static RuleBasedDecisionProvider NewProvider()
        {
            return new RuleBasedDecisionProvider(ElevatedThreatThreshold, CriticalThreatThreshold);
        }

        private static void Test_PDC01()
        {
            var provider = NewProvider();

            AssertTrue(!provider.TemporalContext.PreviousAction.HasValue, "expected a fresh provider's temporal context to have no previous action");
            AssertTrue(provider.TemporalContext.RecentEventTimestamps.Count == 0, "expected a fresh provider's temporal context to have an empty recent-event window");
        }

        private static void Test_PDC02()
        {
            var provider = NewProvider();

            // R8 (NORMAL) -> FOLLOW.
            GhostAction result = provider.Decide(BuildState(timestamp: 1000L));

            AssertTrue(result == GhostAction.FOLLOW, "sanity check: expected R8 to produce FOLLOW");
            AssertTrue(provider.TemporalContext.PreviousAction == GhostAction.FOLLOW, "expected Decide() to record the returned action into the temporal context");
            AssertTrue(provider.TemporalContext.TimeEnteredCurrentAction == 1000L, "expected the temporal context to use WorldState.physical.timestamp as its deterministic time source");
        }

        private static void Test_PDC03()
        {
            var provider = NewProvider();

            // R5 (WARN via disturbance without proximity) - disturbance=true
            // is the "relevant physical event" signal.
            provider.Decide(BuildState(disturbance: true, timestamp: 500L));

            AssertTrue(provider.TemporalContext.LastEventTimestamp == 500L, "expected a disturbance=true WorldState to be recorded as a relevant event");
            AssertTrue(provider.TemporalContext.TimeSinceLastEvent(600L) == 100L, "expected time-since-event to be measured from the disturbance timestamp");

            provider.Decide(BuildState(disturbance: false, timestamp: 700L));

            AssertTrue(provider.TemporalContext.LastEventTimestamp == 500L, "expected a non-disturbance WorldState to leave the last event timestamp unchanged");
        }

        private static void Test_PDC04()
        {
            var provider = NewProvider();

            provider.Decide(BuildState(disturbance: true, timestamp: 100L));
            provider.Decide(BuildState(presence: PresenceLevel.NEAR, confidence: ConfidenceLevel.LOW, timestamp: 200L));

            provider.Reset();

            AssertTrue(!provider.TemporalContext.PreviousAction.HasValue, "expected Reset() to clear the previous action");
            AssertTrue(!provider.TemporalContext.TimeSinceLastEvent(1000L).HasValue, "expected Reset() to clear the last event timestamp");
            AssertTrue(provider.TemporalContext.RecentEventTimestamps.Count == 0, "expected Reset() to clear the recent-event window");

            // Reset() must not touch calibration thresholds.
            AssertTrue(provider.ElevatedThreatThreshold == ElevatedThreatThreshold, "expected Reset() to leave ElevatedThreatThreshold untouched");
            AssertTrue(provider.CriticalThreatThreshold == CriticalThreatThreshold, "expected Reset() to leave CriticalThreatThreshold untouched");
        }

        private static void Test_PDC05()
        {
            // Re-runs a representative slice of the frozen R1-R8 rule
            // battery through a provider whose temporal context is being
            // actively populated across calls, to confirm the context
            // tracking introduced by this task has no effect on the
            // decision itself or on LastDecisionReason.
            var provider = NewProvider();

            GhostAction r1 = provider.Decide(BuildState(presence: PresenceLevel.VERY_NEAR, confidence: ConfidenceLevel.HIGH, inThreatZone: true, timestamp: 10L));
            AssertTrue(r1 == GhostAction.PROTECT, "expected R1 to still produce PROTECT");
            AssertTrue(provider.LastDecisionReason == "PROTECT: player at risk + high-confidence VERY_NEAR physical presence", "expected R1 reason to be unchanged");

            GhostAction r2 = provider.Decide(BuildState(threatLevel: CriticalThreatThreshold, inThreatZone: false, timestamp: 20L));
            AssertTrue(r2 == GhostAction.RETREAT, "expected R2 to still produce RETREAT");

            GhostAction r4 = provider.Decide(BuildState(presence: PresenceLevel.NEAR, confidence: ConfidenceLevel.LOW, inThreatZone: false, timestamp: 30L));
            AssertTrue(r4 == GhostAction.WARN, "expected R4 to still produce WARN");

            GhostAction r8 = provider.Decide(BuildState(timestamp: 40L));
            AssertTrue(r8 == GhostAction.FOLLOW, "expected R8 to still produce FOLLOW");

            // Same input, same timestamp -> same output, even with
            // temporal-context history already populated from prior calls.
            GhostAction r8Repeat = provider.Decide(BuildState(timestamp: 40L));
            AssertTrue(r8Repeat == r8, "expected decision output to remain deterministic regardless of accumulated temporal context");
        }

        private static void Test_PDC06()
        {
            var providerA = NewProvider();
            var providerB = NewProvider();

            providerA.Decide(BuildState(disturbance: true, timestamp: 100L));

            AssertTrue(providerA.TemporalContext.PreviousAction.HasValue, "expected providerA's temporal context to be populated");
            AssertTrue(!providerB.TemporalContext.PreviousAction.HasValue, "expected providerB's temporal context to be unaffected by providerA");
            AssertTrue(providerB.TemporalContext.RecentEventTimestamps.Count == 0, "expected providerB's recent-event window to be unaffected by providerA");
        }
    }
}
