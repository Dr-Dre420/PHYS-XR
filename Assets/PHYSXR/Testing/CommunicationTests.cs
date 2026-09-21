using System;
using System.Reflection;
using PHYSXR.Communication;
using PHYSXR.Core.Data;
using PHYSXR.Core.Enums;
using PHYSXR.PhysicalWorld;

namespace PHYSXR.Testing
{
    // Dependency-light tests for the Communication Boundary (COMM-01..08).
    // Same style as SimulatedPhysicalStateSourceTests: plain static checks,
    // no NUnit/UnityEngine.TestRunner asmdef required.
    public static class CommunicationTests
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

            Run("COMM-01_ValidPacket_ParsesToExpectedPhysicalState", Test_COMM01);
            Run("COMM-02_MalformedPacket_IsRejected", Test_COMM02);
            Run("COMM-03_InvalidEnumValue_IsRejected", Test_COMM03);
            Run("COMM-04_OlderSequenceNumber_CannotOverwriteNewerState", Test_COMM04);
            Run("COMM-05_DuplicateEventId_IsRejected", Test_COMM05);
            Run("COMM-06_StalePacket_PreservesPriorStateAndReportsStaleStatus", Test_COMM06);
            Run("COMM-06b_SequenceOrderingAndDuplicateEventsStillEnforced", Test_COMM06b_SequenceOrderingAndDuplicateEventsStillEnforced);
            Run("COMM-07_CommunicationFailure_NeverProducesGhostAction", Test_COMM07);
            Run("COMM-08_SimulatedAndCommunicationPaths_ShareOneStateModel", Test_COMM08);

            Console.WriteLine(string.Format("Total: {0}, Passed: {1}, Failed: {2}", passed + failed, passed, failed));
            return failed;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static void Test_COMM01()
        {
            var validator = new PacketValidator();
            var parser = new PhysicalStateParser();

            string raw = ProvisionalTextPacketFormat.Encode(1, 1000, "NEAR", "HIGH", false, "", 0);
            ValidationResult result = validator.Validate(raw);

            AssertTrue(result.isValid, "expected valid packet to be accepted");
            AssertTrue(parser.TryParseState(result.packet, out PhysicalState state), "expected state to parse");
            AssertTrue(state.presence == PresenceLevel.NEAR, "presence mismatch");
            AssertTrue(state.confidence == ConfidenceLevel.HIGH, "confidence mismatch");
            AssertTrue(state.disturbance == false, "disturbance mismatch");
            AssertTrue(state.timestamp == 1000L, "timestamp mismatch");
        }

        private static void Test_COMM02()
        {
            var validator = new PacketValidator();

            AssertTrue(!validator.Validate("this is not a valid packet").isValid, "expected garbage text to be rejected");
            AssertTrue(!validator.Validate((byte[])null).isValid, "expected null bytes to be rejected");
            AssertTrue(!validator.Validate(new byte[0]).isValid, "expected empty bytes to be rejected");
            AssertTrue(!validator.Validate("seq=1;ts=1000").isValid, "expected packet missing required fields to be rejected");
        }

        private static void Test_COMM03()
        {
            var validator = new PacketValidator();

            string raw = ProvisionalTextPacketFormat.Encode(1, 1000, "ULTRA_NEAR", "HIGH", false, "", 0);
            ValidationResult result = validator.Validate(raw);

            AssertTrue(!result.isValid, "expected invalid presence enum value to be rejected");
        }

        private static void Test_COMM04()
        {
            var validator = new PacketValidator();

            string newer = ProvisionalTextPacketFormat.Encode(5, 5000, "NEAR", "HIGH", false, "", 0);
            string older = ProvisionalTextPacketFormat.Encode(3, 6000, "VERY_NEAR", "HIGH", true, "", 0);

            AssertTrue(validator.Validate(newer).isValid, "expected seq=5 to be accepted");
            AssertTrue(validator.LastAcceptedSequenceNumber == 5, "expected last accepted sequence to be 5");

            ValidationResult staleResult = validator.Validate(older);
            AssertTrue(!staleResult.isValid, "expected seq=3 to be rejected after seq=5 was accepted");
            AssertTrue(validator.LastAcceptedSequenceNumber == 5, "older sequence must not overwrite the newer accepted sequence");
        }

        private static void Test_COMM05()
        {
            var eventManager = new PhysicalEventManager();
            var parser = new PhysicalStateParser();

            var first = new SemanticPacket { hasEvent = true, eventType = "DISTURBANCE", eventId = 77, timestamp = 1000 };
            var duplicate = new SemanticPacket { hasEvent = true, eventType = "DISTURBANCE", eventId = 77, timestamp = 2000 };

            AssertTrue(parser.TryParseEvent(first, out PhysicalEvent firstEvent), "expected first event to parse");
            AssertTrue(parser.TryParseEvent(duplicate, out PhysicalEvent duplicateEvent), "expected duplicate event to parse");

            AssertTrue(eventManager.RegisterEvent(firstEvent), "expected first registration to succeed");
            AssertTrue(!eventManager.RegisterEvent(duplicateEvent), "expected duplicate event id to be rejected");
        }

        private static void Test_COMM06()
        {
            var validator = new PacketValidator();
            var parser = new PhysicalStateParser();
            var stateManager = new PhysicalStateManager();
            var eventManager = new PhysicalEventManager();
            var pipeline = new PhysicalCommunicationPipeline(validator, parser, stateManager, eventManager, staleThresholdMillis: 1000);

            // 1. A valid, non-stale packet updates PhysicalStateManager.
            string firstRaw = ProvisionalTextPacketFormat.Encode(1, 1000, "NEAR", "HIGH", false, "", 0);
            pipeline.HandleRawPacket(System.Text.Encoding.UTF8.GetBytes(firstRaw), nowTimestamp: 1000);

            PhysicalState afterFirst = stateManager.GetCurrentState();
            AssertTrue(afterFirst != null, "expected a valid non-stale packet to populate PhysicalStateManager");
            AssertTrue(afterFirst.presence == PresenceLevel.NEAR, "expected first accepted state to be NEAR");
            AssertTrue(pipeline.GetStatus(1000) == CommunicationState.Ok, "expected status to be Ok right after a fresh valid packet");

            // 2 & 3. A stale packet (timestamp far behind "now", but with a
            // valid/newer sequence number) must not overwrite the previously
            // accepted state and must not register an event.
            string staleRaw = ProvisionalTextPacketFormat.Encode(2, 1500, "VERY_NEAR", "HIGH", true, "DISTURBANCE", 555);
            pipeline.HandleRawPacket(System.Text.Encoding.UTF8.GetBytes(staleRaw), nowTimestamp: 10000);

            PhysicalState afterStale = stateManager.GetCurrentState();
            AssertTrue(ReferenceEquals(afterStale, afterFirst), "stale packet must not overwrite the previously accepted PhysicalState");
            AssertTrue(afterStale.presence == PresenceLevel.NEAR, "previously accepted PhysicalState must be preserved untouched");
            AssertTrue(!eventManager.HasProcessedEvent(555), "stale packet must not register a new event");

            // 4. The communication status must explicitly report the stale condition.
            AssertTrue(pipeline.GetStatus(10000) == CommunicationState.Stale, "pipeline should report Stale for a time-stale packet");

            // 5. A later valid (fresh, higher-sequence) packet can still
            // update PhysicalStateManager normally.
            string recoveredRaw = ProvisionalTextPacketFormat.Encode(3, 10000, "VERY_NEAR", "HIGH", true, "DISTURBANCE", 556);
            pipeline.HandleRawPacket(System.Text.Encoding.UTF8.GetBytes(recoveredRaw), nowTimestamp: 10000);

            PhysicalState afterRecovery = stateManager.GetCurrentState();
            AssertTrue(!ReferenceEquals(afterRecovery, afterFirst), "a later fresh packet must update PhysicalStateManager");
            AssertTrue(afterRecovery.presence == PresenceLevel.VERY_NEAR, "expected recovered state to reflect the new fresh packet");
            AssertTrue(eventManager.HasProcessedEvent(556), "expected the fresh packet's event to register");
            AssertTrue(pipeline.GetStatus(10000) == CommunicationState.Ok, "expected status to return to Ok after a fresh valid packet");
        }

        private static void Test_COMM06b_SequenceOrderingAndDuplicateEventsStillEnforced()
        {
            var validator = new PacketValidator();
            var parser = new PhysicalStateParser();
            var stateManager = new PhysicalStateManager();
            var eventManager = new PhysicalEventManager();
            var pipeline = new PhysicalCommunicationPipeline(validator, parser, stateManager, eventManager, staleThresholdMillis: 1000);

            string first = ProvisionalTextPacketFormat.Encode(5, 5000, "NEAR", "HIGH", false, "DISTURBANCE", 900);
            pipeline.HandleRawPacket(System.Text.Encoding.UTF8.GetBytes(first), nowTimestamp: 5000);
            AssertTrue(eventManager.HasProcessedEvent(900), "expected first event to register");

            // Older sequence number: must be rejected outright by the validator,
            // so it can't reach PhysicalStateManager either.
            PhysicalState beforeOlder = stateManager.GetCurrentState();
            string older = ProvisionalTextPacketFormat.Encode(2, 5100, "VERY_NEAR", "HIGH", true, "DISTURBANCE", 901);
            pipeline.HandleRawPacket(System.Text.Encoding.UTF8.GetBytes(older), nowTimestamp: 5100);
            AssertTrue(ReferenceEquals(stateManager.GetCurrentState(), beforeOlder), "older sequence number must not overwrite state");
            AssertTrue(!eventManager.HasProcessedEvent(901), "older sequence number must not register its event");

            // Duplicate event id on an otherwise fresh, valid, in-order packet.
            string duplicate = ProvisionalTextPacketFormat.Encode(6, 5200, "NEAR", "HIGH", false, "DISTURBANCE", 900);
            pipeline.HandleRawPacket(System.Text.Encoding.UTF8.GetBytes(duplicate), nowTimestamp: 5200);
            AssertTrue(stateManager.GetCurrentState().timestamp == 5200, "state should still update even when the event id is a duplicate");
        }

        private static void Test_COMM07()
        {
            var validator = new PacketValidator();
            var parser = new PhysicalStateParser();
            var stateManager = new PhysicalStateManager();
            var eventManager = new PhysicalEventManager();
            var pipeline = new PhysicalCommunicationPipeline(validator, parser, stateManager, eventManager);

            // Feed garbage - this must not throw and must not touch GhostAction.
            pipeline.HandleRawPacket(System.Text.Encoding.UTF8.GetBytes("not a packet"), nowTimestamp: 1000);
            AssertTrue(pipeline.GetStatus(1000) == CommunicationState.Invalid, "expected failed packet to report Invalid status");

            Type[] communicationTypes =
            {
                typeof(SemanticPacket),
                typeof(ValidationResult),
                typeof(PacketValidator),
                typeof(PhysicalStateParser),
                typeof(CommunicationState),
                typeof(PhysicalCommunicationPipeline),
                typeof(UDPReceiver),
                typeof(ProvisionalTextPacketFormat)
            };

            const BindingFlags allMembers = BindingFlags.Public | BindingFlags.NonPublic
                                             | BindingFlags.Instance | BindingFlags.Static
                                             | BindingFlags.DeclaredOnly;

            foreach (Type type in communicationTypes)
            {
                foreach (FieldInfo field in type.GetFields(allMembers))
                    AssertTrue(field.FieldType.Name != "GhostAction", type.Name + "." + field.Name + " must not reference GhostAction");

                foreach (MethodInfo method in type.GetMethods(allMembers))
                {
                    AssertTrue(method.ReturnType.Name != "GhostAction", type.Name + "." + method.Name + " must not return GhostAction");
                    foreach (ParameterInfo parameter in method.GetParameters())
                        AssertTrue(parameter.ParameterType.Name != "GhostAction", type.Name + "." + method.Name + " must not accept GhostAction");
                }
            }
        }

        private static void Test_COMM08()
        {
            var simulated = new SimulatedPhysicalStateSource();
            PhysicalState fromSimulation = simulated.CreatePhysicalState(PresenceLevel.NEAR, ConfidenceLevel.HIGH, false, 9000L);

            var parser = new PhysicalStateParser();
            var packet = new SemanticPacket
            {
                presence = "NEAR",
                confidence = "HIGH",
                disturbance = false,
                timestamp = 9000L
            };
            AssertTrue(parser.TryParseState(packet, out PhysicalState fromCommunication), "expected communication path to parse a state");

            AssertTrue(fromSimulation.GetType() == typeof(PhysicalState), "simulation path must produce PhysicalState");
            AssertTrue(fromCommunication.GetType() == typeof(PhysicalState), "communication path must produce PhysicalState");
            AssertTrue(fromSimulation.GetType() == fromCommunication.GetType(), "both paths must produce the same state type, not competing models");

            AssertTrue(fromSimulation.presence == fromCommunication.presence, "presence should match for equivalent inputs");
            AssertTrue(fromSimulation.confidence == fromCommunication.confidence, "confidence should match for equivalent inputs");
            AssertTrue(fromSimulation.disturbance == fromCommunication.disturbance, "disturbance should match for equivalent inputs");
            AssertTrue(fromSimulation.timestamp == fromCommunication.timestamp, "timestamp should match for equivalent inputs");
        }
    }
}
