using System;
using PHYSXR.Core.Data;
using PHYSXR.Core.Enums;

namespace PHYSXR.Testing
{
    // Lightweight, dependency-free test suite for SimulatedPhysicalStateSource.
    // No NUnit/UnityEngine.TestRunner asmdef exists in this project yet, so
    // these run as plain static checks (invokable from the Unity Editor or
    // compiled standalone) rather than introducing new test infrastructure.
    public static class SimulatedPhysicalStateSourceTests
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

            Run("NoneLowNoDisturbance_ProducesExpectedState", Test_NoneLowNoDisturbance);
            Run("NearHighNoDisturbance_ProducesExpectedState", Test_NearHighNoDisturbance);
            Run("VeryNearHighDisturbance_ProducesExpectedState", Test_VeryNearHighDisturbance);
            Run("Timestamp_IsPreserved", Test_TimestampPreserved);
            Run("SuppliedValues_AreNotModified", Test_SuppliedValuesAreNotModified);

            Console.WriteLine(string.Format("Total: {0}, Passed: {1}, Failed: {2}", passed + failed, passed, failed));
            return failed;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static void Test_NoneLowNoDisturbance()
        {
            var source = new SimulatedPhysicalStateSource();
            var state = source.CreatePhysicalState(PresenceLevel.NONE, ConfidenceLevel.LOW, false, 1000L);

            AssertTrue(state.presence == PresenceLevel.NONE, "presence mismatch");
            AssertTrue(state.confidence == ConfidenceLevel.LOW, "confidence mismatch");
            AssertTrue(state.disturbance == false, "disturbance mismatch");
            AssertTrue(state.timestamp == 1000L, "timestamp mismatch");
        }

        private static void Test_NearHighNoDisturbance()
        {
            var source = new SimulatedPhysicalStateSource();
            var state = source.CreatePhysicalState(PresenceLevel.NEAR, ConfidenceLevel.HIGH, false, 2000L);

            AssertTrue(state.presence == PresenceLevel.NEAR, "presence mismatch");
            AssertTrue(state.confidence == ConfidenceLevel.HIGH, "confidence mismatch");
            AssertTrue(state.disturbance == false, "disturbance mismatch");
            AssertTrue(state.timestamp == 2000L, "timestamp mismatch");
        }

        private static void Test_VeryNearHighDisturbance()
        {
            var source = new SimulatedPhysicalStateSource();
            var state = source.CreatePhysicalState(PresenceLevel.VERY_NEAR, ConfidenceLevel.HIGH, true, 3000L);

            AssertTrue(state.presence == PresenceLevel.VERY_NEAR, "presence mismatch");
            AssertTrue(state.confidence == ConfidenceLevel.HIGH, "confidence mismatch");
            AssertTrue(state.disturbance == true, "disturbance mismatch");
            AssertTrue(state.timestamp == 3000L, "timestamp mismatch");
        }

        private static void Test_TimestampPreserved()
        {
            var source = new SimulatedPhysicalStateSource();
            const long expectedTimestamp = 1234567890L;
            var state = source.CreatePhysicalState(PresenceLevel.NEAR, ConfidenceLevel.LOW, false, expectedTimestamp);

            AssertTrue(state.timestamp == expectedTimestamp, "timestamp not preserved");
        }

        private static void Test_SuppliedValuesAreNotModified()
        {
            var source = new SimulatedPhysicalStateSource();
            const PresenceLevel presence = PresenceLevel.VERY_NEAR;
            const ConfidenceLevel confidence = ConfidenceLevel.HIGH;
            const bool disturbance = true;
            const long timestamp = 42L;

            var state = source.CreatePhysicalState(presence, confidence, disturbance, timestamp);

            AssertTrue(presence == PresenceLevel.VERY_NEAR, "input presence was mutated");
            AssertTrue(confidence == ConfidenceLevel.HIGH, "input confidence was mutated");
            AssertTrue(disturbance == true, "input disturbance was mutated");
            AssertTrue(timestamp == 42L, "input timestamp was mutated");

            AssertTrue(state.presence == presence, "output presence differs from input");
            AssertTrue(state.confidence == confidence, "output confidence differs from input");
            AssertTrue(state.disturbance == disturbance, "output disturbance differs from input");
            AssertTrue(state.timestamp == timestamp, "output timestamp differs from input");
        }
    }
}
