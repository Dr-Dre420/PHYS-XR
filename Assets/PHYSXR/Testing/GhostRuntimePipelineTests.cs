using System;
using PHYSXR.Core.Data;
using PHYSXR.Core.Enums;
using PHYSXR.Decision;
using PHYSXR.Ghost;
using PHYSXR.Telemetry;

namespace PHYSXR.Testing
{
    // Dependency-light tests for GhostRuntimePipeline - the plain C#,
    // Unity-independent orchestration extracted from GhostRuntime.Tick()
    // (GHOSTPIPE-01..11). Same style as the other *Tests classes.
    public static class GhostRuntimePipelineTests
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

            Run("GHOSTPIPE-01_RunsWithNoTelemetrySink", Test_RunsWithNoTelemetrySink);
            Run("GHOSTPIPE-02_RecordsWorldStateEvaluated", Test_RecordsWorldStateEvaluated);
            Run("GHOSTPIPE-03_RecordsDecisionMade", Test_RecordsDecisionMade);
            Run("GHOSTPIPE-04_DecisionTelemetry_ContainsGhostAction", Test_DecisionTelemetryContainsGhostAction);
            Run("GHOSTPIPE-05_DecisionTelemetry_ContainsLastDecisionReason", Test_DecisionTelemetryContainsReason);
            Run("GHOSTPIPE-06_RecordsGhostActionExecuted", Test_RecordsGhostActionExecuted);
            Run("GHOSTPIPE-07_RecordsResultingGhostExecutionState", Test_RecordsResultingExecutionState);
            Run("GHOSTPIPE-08_MultipleRuns_PreserveTelemetryInsertionOrder", Test_MultipleRunsPreserveOrder);
            Run("GHOSTPIPE-09_TelemetryDoesNotChangeResultingBehaviour", Test_TelemetryDoesNotChangeBehaviour);
            Run("GHOSTPIPE-10_SameWorldState_ProducesSameGhostAction", Test_SameWorldStateProducesSameAction);
            Run("GHOSTPIPE-11_FailingSink_DoesNotAlterPipelineResult", Test_FailingSinkIsIsolated);

            Console.WriteLine(string.Format("Total: {0}, Passed: {1}, Failed: {2}", passed + failed, passed, failed));
            return failed;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static WorldState BuildCriticalRiskState()
        {
            // Deliberately chosen to be non-default (R1: PROTECT) so the
            // resulting GhostAction/GhostExecutionState are distinguishable
            // from every field's zero/default value.
            return new WorldState
            {
                physical = new PhysicalState
                {
                    presence = PresenceLevel.VERY_NEAR,
                    confidence = ConfidenceLevel.HIGH,
                    disturbance = false,
                    timestamp = 100L
                },
                player = new PlayerState
                {
                    distanceToGhost = 0.5f,
                    movement = false,
                    inThreatZone = true
                },
                virtualWorld = new VirtualState { threatLevel = 0f, eventActive = false },
                ghost = new GhostState { currentState = GhostAction.FOLLOW }
            };
        }

        private static GhostRuntimePipeline NewPipeline()
        {
            return new GhostRuntimePipeline(
                new RuleBasedDecisionProvider(),
                new GhostActionExecutor(),
                new GhostBehaviourController());
        }

        private static void Test_RunsWithNoTelemetrySink()
        {
            var pipeline = NewPipeline(); // TelemetrySink left null (default)
            WorldState state = BuildCriticalRiskState();

            GhostExecutionState result = pipeline.Run(state, 1000L);

            AssertTrue(result == GhostExecutionState.Protecting, "expected pipeline to still produce the correct result with no telemetry sink");
        }

        private static void Test_RecordsWorldStateEvaluated()
        {
            var pipeline = NewPipeline();
            var sink = new InMemoryTelemetrySink();
            pipeline.TelemetrySink = sink;

            pipeline.Run(BuildCriticalRiskState(), 2000L);

            bool found = false;
            foreach (var record in sink.Records)
                if (record.eventType == TelemetryEventType.WorldStateEvaluated)
                    found = true;
            AssertTrue(found, "expected a WorldStateEvaluated record");
        }

        private static void Test_RecordsDecisionMade()
        {
            var pipeline = NewPipeline();
            var sink = new InMemoryTelemetrySink();
            pipeline.TelemetrySink = sink;

            pipeline.Run(BuildCriticalRiskState(), 3000L);

            bool found = false;
            foreach (var record in sink.Records)
                if (record.eventType == TelemetryEventType.DecisionMade)
                    found = true;
            AssertTrue(found, "expected a DecisionMade record");
        }

        private static void Test_DecisionTelemetryContainsGhostAction()
        {
            var pipeline = NewPipeline();
            var sink = new InMemoryTelemetrySink();
            pipeline.TelemetrySink = sink;

            pipeline.Run(BuildCriticalRiskState(), 4000L);

            TelemetryRecord decisionRecord = null;
            foreach (var record in sink.Records)
                if (record.eventType == TelemetryEventType.DecisionMade)
                    decisionRecord = record;

            AssertTrue(decisionRecord != null, "expected a DecisionMade record");
            AssertTrue(decisionRecord.ghostAction == GhostAction.PROTECT, "expected DecisionMade record to carry the resulting GhostAction");
        }

        private static void Test_DecisionTelemetryContainsReason()
        {
            var decisionProvider = new RuleBasedDecisionProvider();
            var pipeline = new GhostRuntimePipeline(decisionProvider, new GhostActionExecutor(), new GhostBehaviourController());
            var sink = new InMemoryTelemetrySink();
            pipeline.TelemetrySink = sink;

            pipeline.Run(BuildCriticalRiskState(), 5000L);

            TelemetryRecord decisionRecord = null;
            foreach (var record in sink.Records)
                if (record.eventType == TelemetryEventType.DecisionMade)
                    decisionRecord = record;

            AssertTrue(decisionRecord != null, "expected a DecisionMade record");
            AssertTrue(decisionRecord.reason == decisionProvider.LastDecisionReason, "expected the DecisionMade record's reason to match RuleBasedDecisionProvider.LastDecisionReason exactly");
            AssertTrue(!string.IsNullOrEmpty(decisionRecord.reason), "expected a non-empty reason");
        }

        private static void Test_RecordsGhostActionExecuted()
        {
            var pipeline = NewPipeline();
            var sink = new InMemoryTelemetrySink();
            pipeline.TelemetrySink = sink;

            pipeline.Run(BuildCriticalRiskState(), 6000L);

            TelemetryRecord executedRecord = null;
            foreach (var record in sink.Records)
                if (record.eventType == TelemetryEventType.GhostActionExecuted)
                    executedRecord = record;

            AssertTrue(executedRecord != null, "expected a GhostActionExecuted record");
            AssertTrue(executedRecord.ghostAction == GhostAction.PROTECT, "expected GhostActionExecuted to carry the executed GhostAction");
        }

        private static void Test_RecordsResultingExecutionState()
        {
            var pipeline = NewPipeline();
            var sink = new InMemoryTelemetrySink();
            pipeline.TelemetrySink = sink;

            GhostExecutionState result = pipeline.Run(BuildCriticalRiskState(), 7000L);

            TelemetryRecord changedRecord = null;
            foreach (var record in sink.Records)
                if (record.eventType == TelemetryEventType.GhostExecutionStateChanged)
                    changedRecord = record;

            AssertTrue(changedRecord != null, "expected a GhostExecutionStateChanged record");
            AssertTrue(changedRecord.ghostExecutionState == result, "expected the recorded execution state to match Run()'s returned execution state");
        }

        private static void Test_MultipleRunsPreserveOrder()
        {
            var pipeline = NewPipeline();
            var sink = new InMemoryTelemetrySink();
            pipeline.TelemetrySink = sink;

            pipeline.Run(BuildCriticalRiskState(), 1L);
            pipeline.Run(BuildCriticalRiskState(), 2L);

            AssertTrue(sink.Records.Count == 8, "expected 4 records per run across 2 runs");
            for (int i = 0; i < 4; i++)
                AssertTrue(sink.Records[i].timestamp == 1L, "expected the first run's records to come first, in order");
            for (int i = 4; i < 8; i++)
                AssertTrue(sink.Records[i].timestamp == 2L, "expected the second run's records to follow, in order");
        }

        private static void Test_TelemetryDoesNotChangeBehaviour()
        {
            WorldState state = BuildCriticalRiskState();

            var pipelineWithout = NewPipeline();
            GhostExecutionState resultWithout = pipelineWithout.Run(state, 100L);

            var pipelineWith = NewPipeline();
            pipelineWith.TelemetrySink = new InMemoryTelemetrySink();
            GhostExecutionState resultWith = pipelineWith.Run(state, 100L);

            AssertTrue(resultWithout == resultWith, "expected identical resulting GhostExecutionState with and without a telemetry sink");
        }

        private static void Test_SameWorldStateProducesSameAction()
        {
            WorldState state = BuildCriticalRiskState();

            var sink = new InMemoryTelemetrySink();
            var pipeline = NewPipeline();
            pipeline.TelemetrySink = sink;

            pipeline.Run(state, 1L);
            pipeline.Run(state, 2L);

            GhostAction firstAction = GetDecisionActionAt(sink, 0);
            GhostAction secondAction = GetDecisionActionAt(sink, 4);

            AssertTrue(firstAction == secondAction, "expected the same WorldState to produce the same GhostAction across repeated runs, telemetry present or not");
        }

        private static GhostAction GetDecisionActionAt(InMemoryTelemetrySink sink, int decisionRecordIndexOffset)
        {
            // The DecisionMade record is the 2nd record emitted per run (index 1 within that run's 4 records).
            var record = sink.Records[decisionRecordIndexOffset + 1];
            return record.ghostAction ?? throw new Exception("expected a GhostAction on the DecisionMade record");
        }

        private class FaultyTelemetrySink : ITelemetrySink
        {
            public void Record(TelemetryRecord record)
            {
                throw new InvalidOperationException("simulated telemetry sink failure");
            }
        }

        private static void Test_FailingSinkIsIsolated()
        {
            var pipeline = NewPipeline();
            pipeline.TelemetrySink = new FaultyTelemetrySink();

            GhostExecutionState result = pipeline.Run(BuildCriticalRiskState(), 9000L);

            AssertTrue(result == GhostExecutionState.Protecting, "a failing telemetry sink must not alter the pipeline's result");
        }
    }
}
