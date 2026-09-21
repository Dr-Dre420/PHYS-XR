namespace PHYSXR.Communication
{
    // Outcome of PacketValidator.Validate(). Deliberately not an exception:
    // rejection is an expected, routine outcome on this boundary (malformed
    // data, stale sequence numbers, dropped/corrupted UDP payloads), not an
    // exceptional one.
    public class ValidationResult
    {
        public bool isValid;
        public SemanticPacket packet;
        public string failureReason;

        public static ValidationResult Reject(string reason)
        {
            return new ValidationResult
            {
                isValid = false,
                packet = null,
                failureReason = reason
            };
        }

        public static ValidationResult Accept(SemanticPacket packet)
        {
            return new ValidationResult
            {
                isValid = true,
                packet = packet,
                failureReason = null
            };
        }
    }
}
