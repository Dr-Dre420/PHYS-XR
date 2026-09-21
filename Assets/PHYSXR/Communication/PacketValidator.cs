using System;
using System.Text;
using PHYSXR.Core.Enums;

namespace PHYSXR.Communication
{
    // Validates incoming semantic packets before anything downstream trusts
    // them. Contains no Ghost decision logic and never selects a
    // GhostAction - it only ever accepts or rejects a packet.
    //
    // Sequence tracking lives here (not in PhysicalStateManager) so that
    // "old sequence number can never overwrite newer state" is enforced at
    // the communication boundary, independent of the decision system.
    public class PacketValidator
    {
        private long lastAcceptedSequenceNumber = long.MinValue;
        private bool hasAcceptedAny;

        public ValidationResult Validate(byte[] rawData)
        {
            if (rawData == null || rawData.Length == 0)
                return ValidationResult.Reject("empty packet");

            string text;
            try
            {
                text = Encoding.UTF8.GetString(rawData);
            }
            catch (Exception)
            {
                return ValidationResult.Reject("undecodable packet bytes");
            }

            return Validate(text);
        }

        public ValidationResult Validate(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return ValidationResult.Reject("empty packet");

            var fields = ProvisionalTextPacketFormat.ParseFields(rawText);
            if (fields == null)
                return ValidationResult.Reject("malformed packet");

            if (!fields.TryGetValue("seq", out var seqText) || !long.TryParse(seqText, out long sequenceNumber))
                return ValidationResult.Reject("missing or invalid sequence number");

            if (!fields.TryGetValue("ts", out var tsText) || !long.TryParse(tsText, out long timestamp) || timestamp <= 0)
                return ValidationResult.Reject("missing or invalid timestamp");

            if (!fields.TryGetValue("presence", out var presenceText) || !IsDefinedEnumName<PresenceLevel>(presenceText))
                return ValidationResult.Reject("invalid presence value");

            if (!fields.TryGetValue("confidence", out var confidenceText) || !IsDefinedEnumName<ConfidenceLevel>(confidenceText))
                return ValidationResult.Reject("invalid confidence value");

            if (!fields.TryGetValue("disturbance", out var disturbanceText) || !bool.TryParse(disturbanceText, out bool disturbance))
                return ValidationResult.Reject("missing or invalid disturbance flag");

            if (hasAcceptedAny && sequenceNumber <= lastAcceptedSequenceNumber)
                return ValidationResult.Reject("stale or duplicate sequence number");

            fields.TryGetValue("eventType", out var eventType);
            bool hasEvent = fields.TryGetValue("eventId", out var eventIdText)
                            && long.TryParse(eventIdText, out long eventId)
                            && eventId != 0;

            var packet = new SemanticPacket
            {
                sequenceNumber = sequenceNumber,
                timestamp = timestamp,
                presence = presenceText,
                confidence = confidenceText,
                disturbance = disturbance,
                eventType = eventType ?? string.Empty,
                eventId = hasEvent ? long.Parse(eventIdText) : 0,
                hasEvent = hasEvent
            };

            lastAcceptedSequenceNumber = sequenceNumber;
            hasAcceptedAny = true;

            return ValidationResult.Accept(packet);
        }

        public long LastAcceptedSequenceNumber => lastAcceptedSequenceNumber;

        private static bool IsDefinedEnumName<TEnum>(string value) where TEnum : struct, Enum
        {
            return Enum.TryParse<TEnum>(value, out _);
        }
    }
}
