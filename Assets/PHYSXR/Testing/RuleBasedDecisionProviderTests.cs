using System;
using PHYSXR.Core.Data;
using PHYSXR.Core.Enums;
using PHYSXR.Decision;

namespace PHYSXR.Testing
{
    // Dependency-light tests for RuleBasedDecisionProvider (DEC-01..13).
    // Same style as the other *Tests classes: plain static checks, no
    // NUnit/UnityEngine.TestRunner asmdef required.
    public static class RuleBasedDecisionProviderTests
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

            Run("DEC-01_NormalState_ProducesFollow", Test_DEC01);
            Run("DEC-02_CriticalPlayerRisk_ProducesProtect", Test_DEC02);
            Run("DEC-03_CriticalThreatOutsideThreatZone_ProducesRetreat", Test_DEC03);
            Run("DEC-04_QualifiedPhysicalDisturbance_ProducesInvestigate", Test_DEC04);
            Run("DEC-05_LowConfidenceNear_ProducesWarn", Test_DEC05);
            Run("DEC-06_DisturbanceWithoutProximity_ProducesWarn", Test_DEC06);
            Run("DEC-07_VirtualEvent_ProducesWarn", Test_DEC07);
            Run("DEC-08_ElevatedVirtualThreat_ProducesWarn", Test_DEC08);
            Run("DEC-09_PlayerRiskOutranksPhysicalInvestigation", Test_DEC09);
            Run("DEC-10_PlayerRiskOutranksElevatedVirtualThreat", Test_DEC10);
            Run("DEC-11_IdenticalState_ProducesIdenticalAction", Test_DEC11);
            Run("DEC-12_ReasonMatchesSelectedRule", Test_DEC12);
            Run("DEC-13_MovementAndGhostCurrentState_DoNotAffectResult", Test_DEC13);

            Console.WriteLine(string.Format("Total: {0}, Passed: {1}, Failed: {2}", passed + failed, passed, failed));
            return failed;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        // Calibration parameters used for these tests - not frozen
        // architecture values, same defaults RuleBasedDecisionProvider uses.
        private const float ElevatedThreatThreshold = 0.4f;
        private const float CriticalThreatThreshold = 0.7f;

        private static WorldState BuildState(
            PresenceLevel presence = PresenceLevel.NONE,
            ConfidenceLevel confidence = ConfidenceLevel.LOW,
            bool disturbance = false,
            long timestamp = 0L,
            float distanceToGhost = 0f,
            bool movement = false,
            bool inThreatZone = false,
            float threatLevel = 0f,
            bool eventActive = false,
            GhostAction ghostCurrentState = GhostAction.FOLLOW)
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
                    distanceToGhost = distanceToGhost,
                    movement = movement,
                    inThreatZone = inThreatZone
                },
                virtualWorld = new VirtualState
                {
                    threatLevel = threatLevel,
                    eventActive = eventActive
                },
                ghost = new GhostState
                {
                    currentState = ghostCurrentState
                }
            };
        }

        private static RuleBasedDecisionProvider NewProvider()
        {
            return new RuleBasedDecisionProvider(ElevatedThreatThreshold, CriticalThreatThreshold);
        }

        private static void Test_DEC01()
        {
            var provider = NewProvider();
            WorldState state = BuildState();

            AssertTrue(provider.Decide(state) == GhostAction.FOLLOW, "expected normal state to produce FOLLOW");
        }

        private static void Test_DEC02()
        {
            var provider = NewProvider();
            WorldState state = BuildState(
                presence: PresenceLevel.VERY_NEAR,
                confidence: ConfidenceLevel.HIGH,
                inThreatZone: true);

            AssertTrue(provider.Decide(state) == GhostAction.PROTECT, "expected R1 to produce PROTECT");
        }

        private static void Test_DEC03()
        {
            var provider = NewProvider();
            WorldState state = BuildState(threatLevel: CriticalThreatThreshold, inThreatZone: false);

            AssertTrue(provider.Decide(state) == GhostAction.RETREAT, "expected R2 to produce RETREAT");
        }

        private static void Test_DEC04()
        {
            var provider = NewProvider();
            WorldState state = BuildState(
                presence: PresenceLevel.NEAR,
                disturbance: true,
                confidence: ConfidenceLevel.HIGH,
                inThreatZone: false);

            AssertTrue(provider.Decide(state) == GhostAction.INVESTIGATE, "expected R3 to produce INVESTIGATE");
        }

        private static void Test_DEC05()
        {
            var provider = NewProvider();
            WorldState state = BuildState(
                presence: PresenceLevel.NEAR,
                confidence: ConfidenceLevel.LOW,
                inThreatZone: false);

            AssertTrue(provider.Decide(state) == GhostAction.WARN, "expected R4 to produce WARN");
        }

        private static void Test_DEC06()
        {
            var provider = NewProvider();
            WorldState state = BuildState(
                disturbance: true,
                presence: PresenceLevel.NONE,
                inThreatZone: false);

            AssertTrue(provider.Decide(state) == GhostAction.WARN, "expected R5 to produce WARN");
        }

        private static void Test_DEC07()
        {
            var provider = NewProvider();
            WorldState state = BuildState(
                eventActive: true,
                presence: PresenceLevel.NONE,
                inThreatZone: false,
                threatLevel: 0f);

            AssertTrue(provider.Decide(state) == GhostAction.WARN, "expected R6 to produce WARN");
        }

        private static void Test_DEC08()
        {
            var provider = NewProvider();
            WorldState state = BuildState(
                threatLevel: ElevatedThreatThreshold,
                inThreatZone: false,
                presence: PresenceLevel.NONE,
                eventActive: false);

            AssertTrue(provider.Decide(state) == GhostAction.WARN, "expected R7 to produce WARN");
        }

        private static void Test_DEC09()
        {
            var provider = NewProvider();
            // Satisfies R1 (inThreatZone + VERY_NEAR + HIGH) while also
            // carrying disturbance=true, which would additionally qualify
            // for R3's physical-event conditions if R1 did not fire first.
            WorldState state = BuildState(
                presence: PresenceLevel.VERY_NEAR,
                confidence: ConfidenceLevel.HIGH,
                disturbance: true,
                inThreatZone: true);

            AssertTrue(provider.Decide(state) == GhostAction.PROTECT, "expected critical player risk to outrank physical investigation");
        }

        private static void Test_DEC10()
        {
            var provider = NewProvider();
            // Satisfies R1 while threatLevel is also at/above the critical
            // threshold that would otherwise drive R2 (RETREAT).
            WorldState state = BuildState(
                presence: PresenceLevel.VERY_NEAR,
                confidence: ConfidenceLevel.HIGH,
                inThreatZone: true,
                threatLevel: CriticalThreatThreshold);

            AssertTrue(provider.Decide(state) == GhostAction.PROTECT, "expected critical player risk to outrank elevated/critical virtual threat");
        }

        private static void Test_DEC11()
        {
            var provider = NewProvider();
            WorldState state = BuildState(
                presence: PresenceLevel.NEAR,
                confidence: ConfidenceLevel.HIGH,
                disturbance: true);

            GhostAction first = provider.Decide(state);
            GhostAction second = provider.Decide(state);
            GhostAction third = new RuleBasedDecisionProvider(ElevatedThreatThreshold, CriticalThreatThreshold).Decide(state);

            AssertTrue(first == second, "expected repeated calls on the same provider to be identical");
            AssertTrue(first == third, "expected a fresh provider instance to produce the identical result for identical input");
        }

        private static void Test_DEC12()
        {
            var provider = NewProvider();

            provider.Decide(BuildState());
            AssertTrue(provider.LastDecisionReason == "FOLLOW: no active threat or event requiring intervention", "FOLLOW reason mismatch");

            provider.Decide(BuildState(presence: PresenceLevel.VERY_NEAR, confidence: ConfidenceLevel.HIGH, inThreatZone: true));
            AssertTrue(provider.LastDecisionReason == "PROTECT: player at risk + high-confidence VERY_NEAR physical presence", "PROTECT reason mismatch");

            provider.Decide(BuildState(threatLevel: CriticalThreatThreshold, inThreatZone: false));
            AssertTrue(provider.LastDecisionReason == "RETREAT: sufficiently high threat without active player-risk condition", "RETREAT reason mismatch");

            provider.Decide(BuildState(presence: PresenceLevel.NEAR, disturbance: true, confidence: ConfidenceLevel.HIGH, inThreatZone: false));
            AssertTrue(provider.LastDecisionReason == "INVESTIGATE: qualified physical disturbance detected", "INVESTIGATE reason mismatch");
        }

        private static void Test_DEC13()
        {
            var provider = NewProvider();

            WorldState baseline = BuildState(
                presence: PresenceLevel.NEAR,
                confidence: ConfidenceLevel.LOW,
                inThreatZone: false,
                movement: false,
                ghostCurrentState: GhostAction.FOLLOW);

            WorldState variant = BuildState(
                presence: PresenceLevel.NEAR,
                confidence: ConfidenceLevel.LOW,
                inThreatZone: false,
                movement: true,
                ghostCurrentState: GhostAction.RETREAT);

            GhostAction baselineResult = provider.Decide(baseline);
            GhostAction variantResult = provider.Decide(variant);

            AssertTrue(baselineResult == variantResult, "movement/ghost.currentState must not change the decision for otherwise identical input");
            AssertTrue(baselineResult == GhostAction.WARN, "sanity check: expected R4 (WARN) for this scenario");
        }
    }
}
