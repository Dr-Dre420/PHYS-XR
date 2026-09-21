namespace PHYSXR.Communication
{
    // In-memory representation of a decoded-but-not-yet-trusted ESP32
    // semantic packet. This is a communication-boundary type, not a
    // competing state model: PacketValidator produces it, PhysicalStateParser
    // consumes it and converts it into the existing PHYSXR.Core.Data
    // contracts (PhysicalState / PhysicalEvent).
    public class SemanticPacket
    {
        public long sequenceNumber;
        public long timestamp;

        // Raw enum names as received on the wire. Kept as strings here
        // because PacketValidator is responsible for confirming they match
        // a known PresenceLevel/ConfidenceLevel before anything downstream
        // trusts them.
        public string presence;
        public string confidence;

        public bool disturbance;

        // A packet may optionally carry a discrete event alongside the
        // continuous state update (e.g. "a disturbance just happened").
        public bool hasEvent;
        public string eventType;
        public long eventId;
    }
}
