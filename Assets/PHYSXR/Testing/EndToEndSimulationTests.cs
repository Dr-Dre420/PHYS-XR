using System;
using PHYSXR.Core.Data;
using PHYSXR.Core.Enums;
using PHYSXR.Decision;
using PHYSXR.Ghost;
using PHYSXR.PhysicalWorld;
using PHYSXR.Telemetry;
using PHYSXR.WorldStateSystem;

namespace PHYSXR.Testing
{
    // End-to-end SOFTWARE-ONLY simulation tests (E2E-01..06). These are not
    // hardware-in-loop tests - no ESP32, UDP, Quest, or XR is involved.
    // They compose the REAL production components exactly as GhostRuntime
    // does (GhostRuntime.Tick() itself fetches a WorldState and calls
    // GhostRuntimePipeline.Run() - nothing more), proving the full chain:
    //
    //   SimulatedPhysicalStateSource -> PhysicalStateManager -> WorldStateManager
    //     -> WorldState -> RuleBasedDecisionProvider -> GhostAction
    //     -> GhostActionExecutor -> GhostExecutionState -> GhostBehaviourController
    //                                        |
    //                                        +-> ITelemetrySink
    //
    // PhysicalEventManager is deliberately NOT part of this chain: no
    // production component (WorldStateManager included) currently composes
    // it into WorldState, so forcing it in here would test a path that
    // doesn't exist in production. See the class-level note at the bottom
    // of this file / the task report for details.
    //
    // Same style as the other *Tests classes: plain static checks, no
    // NUnit/UnityEngine.TestRunner asmdef, no UnityEngine dependency.
    public static class EndToEndSimulationTests
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

            Run("E2E-01_NormalPhysicalState_ProducesFollow", Test_NormalState_Follow);
            Run("E2E-02_NearbyDisturbanceHighConfidence_ProducesInvestigate", Test_Disturbance_Investigate);
            Run("E2E-03_PlayerAtRisk_ProducesProtect", Test_PlayerAtRisk_Protect);
            Run("E2E-04_CriticalVirtualThreatOutsideThreatZone_ProducesRetreat", Test_CriticalVirtualThreat_Retreat);
            Run("E2E-05_UncertainPhysicalCondition_ProducesWarn", Test_UncertainCondition_Warn);
            Run("E2E-06_IndependentHarnesses_SameInput_ProduceSameOutput", Test_IndependentHarnesses_Deterministic);

            Console.WriteLine(string.Format("Total: {0}, Passed: {1}, Failed: {2}", passed + failed, passed, failed));
            return failed;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        // Small test-only composition helper. Wires together the REAL
        // production classes exactly as GhostRuntime does (see
        // GhostRuntime.cs: it constructs the same three components and
        // hands them to the same GhostRuntimePipeline). This is not a
        // second production architecture - it exists only so the chain is
        // directly exercisable/assertable from a standalone test, since
        // GhostRuntime itself is a MonoBehaviour and cannot be instantiated
        // outside Unity.
        private class EndToEndHarness
        {
            public readonly SimulatedPhysicalStateSource PhysicalSource = new SimulatedPhysicalStateSource();
            public readonly PhysicalStateManager PhysicalStateManager = new PhysicalStateManager();
            public readonly WorldStateManager WorldStateManager;
            public readonly RuleBasedDecisionProvider DecisionProvider = new RuleBasedDecisionProvider();
            public readonly GhostActionExecutor ActionExecutor = new GhostActionExecutor();
            public readonly GhostBehaviourController BehaviourController = new GhostBehaviourController();
            public readonly InMemoryTelemetrySink TelemetrySink = new InMemoryTelemetrySink();
            public readonly GhostRuntimePipeline Pipeline;

            public EndToEndHarness()
            {
                WorldStateManager = new WorldStateManager(PhysicalStateManager);
                Pipeline = new GhostRuntimePipeline(DecisionProvider, ActionExecutor, BehaviourController)
                {
                    TelemetrySink = TelemetrySink
                };
            }
        }

        private static TelemetryRecord FindRecord(InMemoryTelemetrySink sink, TelemetryEventType eventType)
        {
            TelemetryRecord found = null;
            foreach (var record in sink.Records)
                if (record.eventType == eventType)
                    found = record;
            return found;
        }

        // Shared scenario runner: drives one PhysicalState through the full
        // chain, asserts every stage matches the expected outcome, then
        // repeats the run to confirm determinism.
        private static void RunScenario(
            PresenceLevel presence,
            ConfidenceLevel confidence,
            bool disturbance,
            PlayerState player,
            VirtualState virtualState,
            GhostAction expectedAction,
            GhostExecutionState expectedExecutionState)
        {
            var harness = new EndToEndHarness();
            const long timestamp = 12345L;

            // 1. Simulated physical input, fed through the real PhysicalStateManager.
            PhysicalState simulated = harness.PhysicalSource.CreatePhysicalState(presence, confidence, disturbance, timestamp);
            harness.PhysicalStateManager.UpdateState(simulated);

            AssertTrue(harness.PhysicalStateManager.GetCurrentState().presence == presence, "PhysicalStateManager did not retain the simulated presence");
            AssertTrue(harness.PhysicalStateManager.GetCurrentState().confidence == confidence, "PhysicalStateManager did not retain the simulated confidence");
            AssertTrue(harness.PhysicalStateManager.GetCurrentState().disturbance == disturbance, "PhysicalStateManager did not retain the simulated disturbance");

            harness.WorldStateManager.UpdatePlayerState(player);
            harness.WorldStateManager.UpdateVirtualState(virtualState);

            // 2. WorldStateManager combines physical + player + virtual state.
            WorldState worldState = harness.WorldStateManager.GetCurrentWorldState();

            AssertTrue(worldState.physical.presence == presence, "WorldState.physical.presence mismatch");
            AssertTrue(worldState.physical.confidence == confidence, "WorldState.physical.confidence mismatch");
            AssertTrue(worldState.physical.disturbance == disturbance, "WorldState.physical.disturbance mismatch");
            AssertTrue(worldState.player.inThreatZone == player.inThreatZone, "WorldState.player.inThreatZone mismatch");
            AssertTrue(worldState.virtualWorld.threatLevel == virtualState.threatLevel, "WorldState.virtualWorld.threatLevel mismatch");
            AssertTrue(worldState.virtualWorld.eventActive == virtualState.eventActive, "WorldState.virtualWorld.eventActive mismatch");

            // 3. Decision -> Execution -> Behaviour, with telemetry attached.
            GhostExecutionState result = harness.Pipeline.Run(worldState, timestamp);

            AssertTrue(result == expectedExecutionState, "unexpected GhostExecutionState: got " + result + ", expected " + expectedExecutionState);
            AssertTrue(harness.BehaviourController.CurrentState == expectedExecutionState, "GhostBehaviourController did not end in the expected state");

            TelemetryRecord decisionRecord = FindRecord(harness.TelemetrySink, TelemetryEventType.DecisionMade);
            AssertTrue(decisionRecord != null, "expected a DecisionMade telemetry record");
            AssertTrue(decisionRecord.ghostAction == expectedAction, "DecisionMade telemetry recorded the wrong GhostAction");
            AssertTrue(decisionRecord.reason == harness.DecisionProvider.LastDecisionReason, "DecisionMade telemetry reason did not match LastDecisionReason");
            AssertTrue(!string.IsNullOrEmpty(decisionRecord.reason), "expected a non-empty decision reason");

            TelemetryRecord executedRecord = FindRecord(harness.TelemetrySink, TelemetryEventType.GhostActionExecuted);
            AssertTrue(executedRecord != null, "expected a GhostActionExecuted telemetry record");
            AssertTrue(executedRecord.ghostAction == expectedAction, "GhostActionExecuted telemetry recorded the wrong GhostAction");
            AssertTrue(executedRecord.ghostExecutionState == expectedExecutionState, "GhostActionExecuted telemetry recorded the wrong GhostExecutionState");

            TelemetryRecord changedRecord = FindRecord(harness.TelemetrySink, TelemetryEventType.GhostExecutionStateChanged);
            AssertTrue(changedRecord != null, "expected a GhostExecutionStateChanged telemetry record");
            AssertTrue(changedRecord.ghostExecutionState == expectedExecutionState, "GhostExecutionStateChanged telemetry recorded the wrong GhostExecutionState");

            // 4. Same input, run again: must produce the same output.
            WorldState secondWorldState = harness.WorldStateManager.GetCurrentWorldState();
            GhostExecutionState secondResult = harness.Pipeline.Run(secondWorldState, timestamp);
            AssertTrue(secondResult == expectedExecutionState, "repeated run with identical input produced a different result");
        }

        private static void Test_NormalState_Follow()
        {
            RunScenario(
                presence: PresenceLevel.NONE,
                confidence: ConfidenceLevel.LOW,
                disturbance: false,
                player: new PlayerState { inThreatZone = false },
                virtualState: new VirtualState { threatLevel = 0f, eventActive = false },
                expectedAction: GhostAction.FOLLOW,
                expectedExecutionState: GhostExecutionState.Following);
        }

        private static void Test_Disturbance_Investigate()
        {
            RunScenario(
                presence: PresenceLevel.NEAR,
                confidence: ConfidenceLevel.HIGH,
                disturbance: true,
                player: new PlayerState { inThreatZone = false },
                virtualState: new VirtualState { threatLevel = 0f, eventActive = false },
                expectedAction: GhostAction.INVESTIGATE,
                expectedExecutionState: GhostExecutionState.Investigating);
        }

        private static void Test_PlayerAtRisk_Protect()
        {
            RunScenario(
                presence: PresenceLevel.VERY_NEAR,
                confidence: ConfidenceLevel.HIGH,
                disturbance: false,
                player: new PlayerState { inThreatZone = true },
                virtualState: new VirtualState { threatLevel = 0f, eventActive = false },
                expectedAction: GhostAction.PROTECT,
                expectedExecutionState: GhostExecutionState.Protecting);
        }

        private static void Test_CriticalVirtualThreat_Retreat()
        {
            var decisionProvider = new RuleBasedDecisionProvider(); // defaults: CriticalThreatThreshold = 0.7f
            RunScenario(
                presence: PresenceLevel.NONE,
                confidence: ConfidenceLevel.LOW,
                disturbance: false,
                player: new PlayerState { inThreatZone = false },
                virtualState: new VirtualState { threatLevel = decisionProvider.CriticalThreatThreshold, eventActive = false },
                expectedAction: GhostAction.RETREAT,
                expectedExecutionState: GhostExecutionState.Retreating);
        }

        private static void Test_UncertainCondition_Warn()
        {
            RunScenario(
                presence: PresenceLevel.NEAR,
                confidence: ConfidenceLevel.LOW,
                disturbance: false,
                player: new PlayerState { inThreatZone = false },
                virtualState: new VirtualState { threatLevel = 0f, eventActive = false },
                expectedAction: GhostAction.WARN,
                expectedExecutionState: GhostExecutionState.Warning);
        }

        private static void Test_IndependentHarnesses_Deterministic()
        {
            // Two completely independent harnesses (separate PhysicalStateManager,
            // WorldStateManager, decision/execution/behaviour instances) fed the
            // identical simulated input must reach the identical result - proving
            // determinism is a property of the components themselves, not of any
            // shared/hidden state between test runs.
            var harnessA = new EndToEndHarness();
            var harnessB = new EndToEndHarness();
            const long timestamp = 999L;

            var player = new PlayerState { inThreatZone = true };
            var virtualState = new VirtualState { threatLevel = 0f, eventActive = false };

            harnessA.PhysicalStateManager.UpdateState(harnessA.PhysicalSource.CreatePhysicalState(PresenceLevel.VERY_NEAR, ConfidenceLevel.HIGH, false, timestamp));
            harnessA.WorldStateManager.UpdatePlayerState(player);
            harnessA.WorldStateManager.UpdateVirtualState(virtualState);
            GhostExecutionState resultA = harnessA.Pipeline.Run(harnessA.WorldStateManager.GetCurrentWorldState(), timestamp);

            harnessB.PhysicalStateManager.UpdateState(harnessB.PhysicalSource.CreatePhysicalState(PresenceLevel.VERY_NEAR, ConfidenceLevel.HIGH, false, timestamp));
            harnessB.WorldStateManager.UpdatePlayerState(player);
            harnessB.WorldStateManager.UpdateVirtualState(virtualState);
            GhostExecutionState resultB = harnessB.Pipeline.Run(harnessB.WorldStateManager.GetCurrentWorldState(), timestamp);

            AssertTrue(resultA == GhostExecutionState.Protecting, "expected harnessA to reach Protecting");
            AssertTrue(resultA == resultB, "expected two independent harnesses given identical input to reach the identical result");
        }
    }
}
