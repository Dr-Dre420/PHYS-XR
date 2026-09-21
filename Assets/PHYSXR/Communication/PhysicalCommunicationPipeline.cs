using PHYSXR.Core.Data;
using PHYSXR.PhysicalWorld;

namespace PHYSXR.Communication
{
    // Wires PacketValidator -> PhysicalStateParser into the existing
    // PhysicalStateManager / PhysicalEventManager. This is integration glue
    // only: it contains no Ghost decision logic (never references
    // GhostAction/IDecisionProvider) and no sensor-specific logic.
    //
    // Duplicate event-ID rejection is intentionally NOT reimplemented here -
    // PhysicalEventManager.RegisterEvent already does it, so parsed events
    // are simply handed to it.
    public class PhysicalCommunicationPipeline
    {
        private readonly PacketValidator validator;
        private readonly PhysicalStateParser parser;
        private readonly PhysicalStateManager stateManager;
        private readonly PhysicalEventManager eventManager;
        private readonly long staleThresholdMillis;

        private long lastAcceptedTimestamp = -1;
        private bool lastPacketWasValid = true;
        private bool lastPacketWasStale;

        public PhysicalCommunicationPipeline(
            PacketValidator validator,
            PhysicalStateParser parser,
            PhysicalStateManager stateManager,
            PhysicalEventManager eventManager,
            long staleThresholdMillis = 2000)
        {
            this.validator = validator;
            this.parser = parser;
            this.stateManager = stateManager;
            this.eventManager = eventManager;
            this.staleThresholdMillis = staleThresholdMillis;
        }

        // nowTimestamp is supplied by the caller (rather than read from a
        // system clock in here) so staleness handling stays deterministic
        // and testable without real time passing.
        public void HandleRawPacket(byte[] rawData, long nowTimestamp)
        {
            ValidationResult result = validator.Validate(rawData);
            lastPacketWasValid = result.isValid;

            if (!result.isValid)
            {
                lastPacketWasStale = false;
                return;
            }

            if (!parser.TryParseState(result.packet, out PhysicalState state))
            {
                lastPacketWasValid = false;
                lastPacketWasStale = false;
                return;
            }

            // Staleness is decided before anything is committed, so a
            // time-stale packet can neither overwrite PhysicalStateManager
            // nor generate a PhysicalEvent - the previously accepted
            // PhysicalState is preserved untouched.
            bool isStale = (nowTimestamp - result.packet.timestamp) > staleThresholdMillis;
            lastPacketWasStale = isStale;

            if (isStale)
                return;

            stateManager.UpdateState(state);
            lastAcceptedTimestamp = result.packet.timestamp;

            if (parser.TryParseEvent(result.packet, out PhysicalEvent physicalEvent))
                eventManager.RegisterEvent(physicalEvent);
        }

        // Explicit stale/invalid signal for the future decision layer to
        // consult, rather than the communication layer selecting a
        // GhostAction on communication failure.
        public CommunicationState GetStatus(long nowTimestamp)
        {
            if (!lastPacketWasValid)
                return CommunicationState.Invalid;

            if (lastPacketWasStale)
                return CommunicationState.Stale;

            if (lastAcceptedTimestamp < 0)
                return CommunicationState.Stale;

            if ((nowTimestamp - lastAcceptedTimestamp) > staleThresholdMillis)
                return CommunicationState.Stale;

            return CommunicationState.Ok;
        }
    }
}
