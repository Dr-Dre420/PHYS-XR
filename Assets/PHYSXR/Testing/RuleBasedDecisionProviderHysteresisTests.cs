using System;
using System.Collections.Generic;
using System.Linq;
using PHYSXR.Core.Data;
using PHYSXR.Core.Enums;
using PHYSXR.Decision;

namespace PHYSXR.Testing
{
    // Dependency-light tests for RuleBasedDecisionProvider's hysteresis
    // post-processing layer (HYST-01..18). Same style as the other
    // *Tests classes: plain static checks, no NUnit/Unity Test Framework
    // asmdef required. Kept in its own file so the existing, already-
    // passing RuleBasedDecisionProviderTests.cs (DEC-01..13) and
    // RuleBasedDecisionProviderTemporalContextTests.cs (PDC-01..07) are
    // left untouched by this task.
    public static class RuleBasedDecisionProviderHysteresisTests
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

            Run("HYST-01_ProtectCandidate_AlwaysImmediate", Test_HYST01);
            Run("HYST-02_RetreatCandidate_AlwaysImmediate", Test_HYST02);
            Run("HYST-03_FollowCandidateBeforeDwell_WarnRetained", Test_HYST03);
            Run("HYST-04_FollowCandidateAfterDwell_FollowApplied", Test_HYST04);
            Run("HYST-05_ProtectExitBeforeDwell_ProtectRetained", Test_HYST05);
            Run("HYST-06_RetreatExitBeforeDwell_RetreatRetained", Test_HYST06);
            Run("HYST-07_ProtectToRetreat_Immediate", Test_HYST07);
            Run("HYST-08_WarnToInvestigate_ImmediateNotGated", Test_HYST08);
            Run("HYST-09_FirstDecisionEver_CandidateImmediate", Test_HYST09);
            Run("HYST-10_FirstDecisionAfterReset_CandidateImmediate", Test_HYST10);
            Run("HYST-11_SuppressedTransition_HistoryReflectsRetainedAction", Test_HYST11);
            Run("HYST-12_ConsecutiveSuppressedTicks_DoNotResetEntryTimestamp", Test_HYST12);
            Run("HYST-13_SustainedCalmPeriod_EventuallyReachesFollow", Test_HYST13);
            Run("HYST-14_Determinism_IdenticalSequenceProducesIdenticalResults", Test_HYST14);
            Run("HYST-15_ExistingDecisionRuleSuite_StillPasses", Test_HYST15);
            Run("HYST-16_SuppressedReason_HasExpectedFormat", Test_HYST16);
            Run("HYST-17_ZeroDwell_ReproducesPreHysteresisBehaviour", Test_HYST17);
            Run("HYST-18_BackwardTimestampDuringHoldoff_DoesNotCrash", Test_HYST18);

            Console.WriteLine(string.Format("Total: {0}, Passed: {1}, Failed: {2}", passed + failed, passed, failed));
            return failed;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private const long DefaultDwellMs = 1500L;

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

        // Candidate helpers - one WorldState per rule, so each test can
        // state its intent (R1..R8) without repeating the rule
        // conditions inline.
        private static WorldState ProtectCandidateState(long timestamp) =>
            BuildState(presence: PresenceLevel.VERY_NEAR, confidence: ConfidenceLevel.HIGH, inThreatZone: true, timestamp: timestamp);

        private static WorldState RetreatCandidateState(long timestamp) =>
            BuildState(threatLevel: 0.7f, inThreatZone: false, timestamp: timestamp);

        private static WorldState InvestigateCandidateState(long timestamp) =>
            BuildState(presence: PresenceLevel.NEAR, confidence: ConfidenceLevel.HIGH, disturbance: true, inThreatZone: false, timestamp: timestamp);

        private static WorldState WarnCandidateState(long timestamp) =>
            BuildState(presence: PresenceLevel.NEAR, confidence: ConfidenceLevel.LOW, inThreatZone: false, timestamp: timestamp);

        private static WorldState FollowCandidateState(long timestamp) =>
            BuildState(timestamp: timestamp);

        private static RuleBasedDecisionProvider NewProvider(long minimumActionDwellMs = DefaultDwellMs)
        {
            return new RuleBasedDecisionProvider(0.4f, 0.7f, minimumActionDwellMs);
        }

        private static void Test_HYST01()
        {
            var provider = NewProvider();

            // Establish a previous action first (rule 0 only applies to
            // the very first call).
            provider.Decide(WarnCandidateState(0L));

            GhostAction result = provider.Decide(ProtectCandidateState(0L)); // elapsed = 0

            AssertTrue(result == GhostAction.PROTECT, "expected a PROTECT candidate to be applied immediately regardless of dwell");
            AssertTrue(provider.LastDecisionReason == "PROTECT: player at risk + high-confidence VERY_NEAR physical presence", "expected the genuine R1 reason, not a hysteresis reason");
        }

        private static void Test_HYST02()
        {
            var provider = NewProvider();

            provider.Decide(WarnCandidateState(0L));

            GhostAction result = provider.Decide(RetreatCandidateState(0L)); // elapsed = 0

            AssertTrue(result == GhostAction.RETREAT, "expected a RETREAT candidate to be applied immediately regardless of dwell");
            AssertTrue(provider.LastDecisionReason == "RETREAT: sufficiently high threat without active player-risk condition", "expected the genuine R2 reason, not a hysteresis reason");
        }

        private static void Test_HYST03()
        {
            var provider = NewProvider();

            provider.Decide(WarnCandidateState(0L)); // enters WARN at t=0

            GhostAction result = provider.Decide(FollowCandidateState(100L)); // elapsed = 100 < 1500

            AssertTrue(result == GhostAction.WARN, "expected WARN to be retained while the candidate FOLLOW is suppressed");
            AssertTrue(provider.LastDecisionReason.Contains("HYSTERESIS"), "expected a hysteresis explanation, not the genuine FOLLOW reason");
        }

        private static void Test_HYST04()
        {
            var provider = NewProvider();

            provider.Decide(WarnCandidateState(0L)); // enters WARN at t=0

            GhostAction result = provider.Decide(FollowCandidateState(DefaultDwellMs)); // elapsed == dwell

            AssertTrue(result == GhostAction.FOLLOW, "expected FOLLOW to be applied once the dwell has elapsed");
            AssertTrue(provider.LastDecisionReason == "FOLLOW: no active threat or event requiring intervention", "expected the genuine R8 reason to be restored");
        }

        private static void Test_HYST05()
        {
            var provider = NewProvider();

            provider.Decide(ProtectCandidateState(0L)); // enters PROTECT at t=0

            GhostAction result = provider.Decide(WarnCandidateState(100L)); // elapsed = 100 < 1500, non-safety candidate

            AssertTrue(result == GhostAction.PROTECT, "expected PROTECT to be retained while exiting to a non-safety candidate is suppressed");
            AssertTrue(provider.LastDecisionReason.Contains("HYSTERESIS"), "expected a hysteresis explanation");
        }

        private static void Test_HYST06()
        {
            var provider = NewProvider();

            provider.Decide(RetreatCandidateState(0L)); // enters RETREAT at t=0

            GhostAction result = provider.Decide(WarnCandidateState(100L)); // elapsed = 100 < 1500, non-safety candidate

            AssertTrue(result == GhostAction.RETREAT, "expected RETREAT to be retained while exiting to a non-safety candidate is suppressed");
            AssertTrue(provider.LastDecisionReason.Contains("HYSTERESIS"), "expected a hysteresis explanation");
        }

        private static void Test_HYST07()
        {
            var provider = NewProvider();

            provider.Decide(ProtectCandidateState(0L)); // enters PROTECT at t=0

            GhostAction result = provider.Decide(RetreatCandidateState(0L)); // elapsed = 0, but candidate is itself a safety action

            AssertTrue(result == GhostAction.RETREAT, "expected PROTECT -> RETREAT to be immediate (rule 1 overrides rule 2)");
            AssertTrue(provider.LastDecisionReason == "RETREAT: sufficiently high threat without active player-risk condition", "expected the genuine R2 reason, not a hysteresis reason");
        }

        private static void Test_HYST08()
        {
            var provider = NewProvider();

            provider.Decide(WarnCandidateState(0L)); // enters WARN at t=0

            GhostAction result = provider.Decide(InvestigateCandidateState(0L)); // elapsed = 0, non-FOLLOW <-> non-FOLLOW

            AssertTrue(result == GhostAction.INVESTIGATE, "expected WARN -> INVESTIGATE to be immediate; this transition is deliberately NOT gated in this design");
            AssertTrue(provider.LastDecisionReason == "INVESTIGATE: qualified physical disturbance detected", "expected the genuine R3 reason, not a hysteresis reason");
        }

        private static void Test_HYST09()
        {
            var provider = NewProvider();

            GhostAction result = provider.Decide(FollowCandidateState(0L));

            AssertTrue(result == GhostAction.FOLLOW, "expected the very first decision ever to return the candidate immediately");
            AssertTrue(provider.LastDecisionReason == "FOLLOW: no active threat or event requiring intervention", "expected the genuine reason on the first-ever decision");
        }

        private static void Test_HYST10()
        {
            var provider = NewProvider();

            provider.Decide(WarnCandidateState(0L));
            provider.Reset();

            // Immediately after Reset(), a FOLLOW candidate must NOT be
            // gated even though a WARN was active moments before the
            // reset - there is no previous action any more.
            GhostAction result = provider.Decide(FollowCandidateState(0L));

            AssertTrue(result == GhostAction.FOLLOW, "expected the first decision after Reset() to return the candidate immediately, with no hysteresis suppression");
            AssertTrue(provider.LastDecisionReason == "FOLLOW: no active threat or event requiring intervention", "expected the genuine reason, not a hysteresis reason, right after Reset()");
        }

        private static void Test_HYST11()
        {
            var provider = NewProvider();

            provider.Decide(WarnCandidateState(0L)); // enters WARN at t=0
            provider.Decide(FollowCandidateState(100L)); // suppressed: candidate FOLLOW, elapsed 100 < 1500

            AssertTrue(provider.TemporalContext.PreviousAction == GhostAction.WARN, "expected DecisionHistory.PreviousAction to reflect the retained action (WARN), not the suppressed candidate (FOLLOW)");
            AssertTrue(provider.TemporalContext.TimeInCurrentAction(200L) == 200L, "expected TimeInCurrentAction to still be measured from WARN's original entry at t=0");
        }

        private static void Test_HYST12()
        {
            var provider = NewProvider();

            provider.Decide(WarnCandidateState(0L)); // enters WARN at t=0
            provider.Decide(FollowCandidateState(100L)); // suppressed
            provider.Decide(FollowCandidateState(300L)); // suppressed again
            provider.Decide(FollowCandidateState(600L)); // suppressed again

            AssertTrue(provider.TemporalContext.PreviousAction == GhostAction.WARN, "expected WARN to still be the retained action after several suppressed ticks");
            AssertTrue(provider.TemporalContext.TimeEnteredCurrentAction == 0L, "expected TimeEnteredCurrentAction to remain at WARN's original entry timestamp, not be reset by suppressed ticks");
            AssertTrue(provider.TemporalContext.TimeInCurrentAction(600L) == 600L, "expected elapsed time to keep counting from the original entry across multiple suppressed ticks");
        }

        private static void Test_HYST13()
        {
            var provider = NewProvider();

            provider.Decide(WarnCandidateState(0L)); // enters WARN at t=0
            GhostAction stillSuppressed = provider.Decide(FollowCandidateState(500L)); // elapsed 500 < 1500
            GhostAction stillSuppressed2 = provider.Decide(FollowCandidateState(1000L)); // elapsed 1000 < 1500
            GhostAction recovered = provider.Decide(FollowCandidateState(1600L)); // elapsed 1600 >= 1500

            AssertTrue(stillSuppressed == GhostAction.WARN, "expected WARN retained before the dwell elapses");
            AssertTrue(stillSuppressed2 == GhostAction.WARN, "expected WARN still retained before the dwell elapses");
            AssertTrue(recovered == GhostAction.FOLLOW, "expected a sustained calm period to eventually reach FOLLOW once the dwell has elapsed");
        }

        private static void Test_HYST14()
        {
            var providerA = NewProvider();
            var providerB = NewProvider();

            WorldState[] sequence =
            {
                WarnCandidateState(0L),
                FollowCandidateState(100L),          // suppressed
                FollowCandidateState(1000L),         // suppressed
                InvestigateCandidateState(1000L),    // immediate (non-FOLLOW <-> non-FOLLOW)
                ProtectCandidateState(1000L),        // immediate (safety escalation)
                WarnCandidateState(1050L),           // suppressed (exiting PROTECT)
                WarnCandidateState(2600L)             // dwell elapsed -> applied
            };

            List<GhostAction> resultsA = sequence.Select(providerA.Decide).ToList();
            List<GhostAction> resultsB = sequence.Select(providerB.Decide).ToList();
            string reasonA = providerA.LastDecisionReason;
            string reasonB = providerB.LastDecisionReason;

            AssertTrue(resultsA.SequenceEqual(resultsB), "expected identical action sequences across independent providers given identical input");
            AssertTrue(reasonA == reasonB, "expected identical final LastDecisionReason across independent providers given identical input");
            AssertTrue(providerA.TemporalContext.PreviousAction == providerB.TemporalContext.PreviousAction, "expected identical final PreviousAction across independent providers");
            AssertTrue(providerA.TemporalContext.TimeEnteredCurrentAction == providerB.TemporalContext.TimeEnteredCurrentAction, "expected identical final TimeEnteredCurrentAction across independent providers");
        }

        private static void Test_HYST15()
        {
            int failed = RuleBasedDecisionProviderTests.RunAll();
            AssertTrue(failed == 0, "expected the existing DEC-01..13 suite to still pass unchanged with the hysteresis layer present");
        }

        private static void Test_HYST16()
        {
            var provider = NewProvider();

            provider.Decide(WarnCandidateState(0L));
            provider.Decide(FollowCandidateState(100L)); // suppressed

            string reason = provider.LastDecisionReason;

            AssertTrue(reason.Contains("HYSTERESIS"), "expected the suppressed reason to contain the HYSTERESIS marker");
            AssertTrue(reason.Contains("WARN"), "expected the suppressed reason to name the retained action");
            AssertTrue(reason.Contains("FOLLOW"), "expected the suppressed reason to name the suppressed candidate");

            bool matchesAnyGenuineReason =
                reason == "PROTECT: player at risk + high-confidence VERY_NEAR physical presence" ||
                reason == "RETREAT: sufficiently high threat without active player-risk condition" ||
                reason == "INVESTIGATE: qualified physical disturbance detected" ||
                reason == "WARN: physical condition detected with low confidence" ||
                reason == "WARN: disturbance detected without proximity confirmation" ||
                reason == "WARN: virtual event detected" ||
                reason == "WARN: elevated virtual threat" ||
                reason == "FOLLOW: no active threat or event requiring intervention";

            AssertTrue(!matchesAnyGenuineReason, "expected the hysteresis reason to be distinguishable from every genuine R1-R8 reason string");
        }

        private static void Test_HYST17()
        {
            var provider = NewProvider(minimumActionDwellMs: 0L);

            provider.Decide(WarnCandidateState(0L)); // enters WARN at t=0
            GhostAction result = provider.Decide(FollowCandidateState(0L)); // elapsed = 0, dwell = 0 -> not suppressed

            AssertTrue(result == GhostAction.FOLLOW, "expected MinimumActionDwellMs=0 to reproduce pre-hysteresis (immediate) behaviour");
            AssertTrue(provider.LastDecisionReason == "FOLLOW: no active threat or event requiring intervention", "expected the genuine reason, not a hysteresis reason, when dwell is 0");
        }

        private static void Test_HYST18()
        {
            var provider = NewProvider();

            provider.Decide(WarnCandidateState(100L)); // enters WARN at t=100

            // Backward timestamp while a holdoff would otherwise apply -
            // must not throw, and must follow DecisionHistory's existing,
            // already-documented (not "fixed" here) backward-timestamp
            // behaviour rather than any new correction logic.
            GhostAction result = provider.Decide(FollowCandidateState(50L));

            // TimeInCurrentAction(50) = 50 - 100 = -50, which is less
            // than any non-negative MinimumActionDwellMs, so the
            // candidate is suppressed and WARN is retained - documenting
            // today's actual (unmodified) behaviour, not asserting a
            // "correct" notion of backward time.
            AssertTrue(result == GhostAction.WARN, "expected a backward timestamp to still route through the existing (undefended) dwell-gate arithmetic without crashing");
            AssertTrue(provider.LastDecisionReason.Contains("HYSTERESIS"), "expected a hysteresis explanation for the retained action");
        }
    }
}
