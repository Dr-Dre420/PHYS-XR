using System;
using PHYSXR.Core.Enums;
using PHYSXR.Ghost;
using PHYSXR.Telemetry;

namespace PHYSXR.Testing
{
    // Dependency-light tests for the Telemetry module (TEL-01..09).
    // Same style as the other *Tests classes: plain static checks, no
    // NUnit/UnityEngine.TestRunner asmdef required.
    public static class TelemetryTests
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

            Run("TEL-01_SingleRecord_IsStoredAndRetrievable", Test_SingleRecordStored);
            Run("TEL-02_MultipleRecords_PreserveInsertionOrder", Test_InsertionOrderPreserved);
            Run("TEL-03_RecordingNull_IsSafeNoOp", Test_RecordingNullIsNoOp);
            Run("TEL-04_DefaultValuedRecord_IsStoredAsIs", Test_DefaultValuedRecord);
            Run("TEL-05_AllSixEventTypes_CanBeRepresented", Test_AllEventTypesRepresentable);
            Run("TEL-06_DecisionEventFields_ArePreservedExactly", Test_DecisionEventFieldsPreserved);
            Run("TEL-07_GhostExecutionStateChangeFields_ArePreserved", Test_ExecutionStateChangeFieldsPreserved);
            Run("TEL-08_IndependentSinks_DoNotShareState", Test_IndependentSinksDoNotShareState);
            Run("TEL-09_RecordDoesNotMutateSuppliedRecord", Test_RecordDoesNotMutateInput);

            Console.WriteLine(string.Format("Total: {0}, Passed: {1}, Failed: {2}", passed + failed, passed, failed));
            return failed;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static void Test_SingleRecordStored()
        {
            var sink = new InMemoryTelemetrySink();
            var record = new TelemetryRecord { timestamp = 1000L, eventType = TelemetryEventType.WorldStateEvaluated };

            sink.Record(record);

            AssertTrue(sink.Records.Count == 1, "expected exactly one stored record");
            AssertTrue(ReferenceEquals(sink.Records[0], record), "expected the stored record to be the same instance supplied");
        }

        private static void Test_InsertionOrderPreserved()
        {
            var sink = new InMemoryTelemetrySink();

            sink.Record(new TelemetryRecord { timestamp = 1, eventType = TelemetryEventType.PhysicalStateUpdated });
            sink.Record(new TelemetryRecord { timestamp = 2, eventType = TelemetryEventType.PhysicalEventRegistered });
            sink.Record(new TelemetryRecord { timestamp = 3, eventType = TelemetryEventType.WorldStateEvaluated });

            AssertTrue(sink.Records.Count == 3, "expected three stored records");
            AssertTrue(sink.Records[0].eventType == TelemetryEventType.PhysicalStateUpdated, "expected first record order preserved");
            AssertTrue(sink.Records[1].eventType == TelemetryEventType.PhysicalEventRegistered, "expected second record order preserved");
            AssertTrue(sink.Records[2].eventType == TelemetryEventType.WorldStateEvaluated, "expected third record order preserved");
        }

        private static void Test_RecordingNullIsNoOp()
        {
            var sink = new InMemoryTelemetrySink();

            sink.Record(null);

            AssertTrue(sink.Records.Count == 0, "recording null must not throw and must not add an entry");
        }

        private static void Test_DefaultValuedRecord()
        {
            var sink = new InMemoryTelemetrySink();
            var record = new TelemetryRecord(); // every field left at its default

            sink.Record(record);

            AssertTrue(sink.Records.Count == 1, "expected the default-valued record to still be stored");
            TelemetryRecord stored = sink.Records[0];
            AssertTrue(stored.timestamp == 0L, "expected default timestamp 0");
            AssertTrue(stored.eventType == TelemetryEventType.PhysicalStateUpdated, "expected default eventType (first enum member)");
            AssertTrue(stored.ghostAction == null, "expected ghostAction to default to null");
            AssertTrue(stored.ghostExecutionState == null, "expected ghostExecutionState to default to null");
            AssertTrue(stored.reason == null, "expected reason to default to null");
            AssertTrue(stored.relatedId == null, "expected relatedId to default to null");
            AssertTrue(stored.detail == null, "expected detail to default to null");
        }

        private static void Test_AllEventTypesRepresentable()
        {
            var sink = new InMemoryTelemetrySink();

            foreach (TelemetryEventType eventType in Enum.GetValues(typeof(TelemetryEventType)))
                sink.Record(new TelemetryRecord { timestamp = 1L, eventType = eventType });

            AssertTrue(sink.Records.Count == 6, "expected all six TelemetryEventType values to be recordable");
        }

        private static void Test_DecisionEventFieldsPreserved()
        {
            var sink = new InMemoryTelemetrySink();
            var record = new TelemetryRecord
            {
                timestamp = 5000L,
                eventType = TelemetryEventType.DecisionMade,
                ghostAction = GhostAction.PROTECT,
                reason = "PROTECT: player at risk + high-confidence VERY_NEAR physical presence"
            };

            sink.Record(record);

            TelemetryRecord stored = sink.Records[0];
            AssertTrue(stored.eventType == TelemetryEventType.DecisionMade, "eventType mismatch");
            AssertTrue(stored.ghostAction == GhostAction.PROTECT, "ghostAction mismatch");
            AssertTrue(stored.reason == "PROTECT: player at risk + high-confidence VERY_NEAR physical presence", "reason mismatch");
            AssertTrue(stored.ghostExecutionState == null, "expected ghostExecutionState to remain unset for a decision event");
        }

        private static void Test_ExecutionStateChangeFieldsPreserved()
        {
            var sink = new InMemoryTelemetrySink();
            var record = new TelemetryRecord
            {
                timestamp = 6000L,
                eventType = TelemetryEventType.GhostExecutionStateChanged,
                ghostExecutionState = GhostExecutionState.Retreating,
                detail = "transition from Following"
            };

            sink.Record(record);

            TelemetryRecord stored = sink.Records[0];
            AssertTrue(stored.eventType == TelemetryEventType.GhostExecutionStateChanged, "eventType mismatch");
            AssertTrue(stored.ghostExecutionState == GhostExecutionState.Retreating, "ghostExecutionState mismatch");
            AssertTrue(stored.detail == "transition from Following", "detail mismatch");
        }

        private static void Test_IndependentSinksDoNotShareState()
        {
            var sinkA = new InMemoryTelemetrySink();
            var sinkB = new InMemoryTelemetrySink();

            sinkA.Record(new TelemetryRecord { timestamp = 1L, eventType = TelemetryEventType.PhysicalStateUpdated });

            AssertTrue(sinkA.Records.Count == 1, "expected sinkA to have one record");
            AssertTrue(sinkB.Records.Count == 0, "expected sinkB to remain empty - no shared/static state between instances");
        }

        private static void Test_RecordDoesNotMutateInput()
        {
            var sink = new InMemoryTelemetrySink();
            var record = new TelemetryRecord
            {
                timestamp = 42L,
                eventType = TelemetryEventType.GhostActionExecuted,
                ghostAction = GhostAction.WARN,
                reason = "WARN: elevated virtual threat",
                relatedId = 7L,
                detail = "context"
            };

            sink.Record(record);

            AssertTrue(record.timestamp == 42L, "Record() must not mutate the supplied record's timestamp");
            AssertTrue(record.eventType == TelemetryEventType.GhostActionExecuted, "Record() must not mutate the supplied record's eventType");
            AssertTrue(record.ghostAction == GhostAction.WARN, "Record() must not mutate the supplied record's ghostAction");
            AssertTrue(record.reason == "WARN: elevated virtual threat", "Record() must not mutate the supplied record's reason");
            AssertTrue(record.relatedId == 7L, "Record() must not mutate the supplied record's relatedId");
            AssertTrue(record.detail == "context", "Record() must not mutate the supplied record's detail");
        }
    }
}
