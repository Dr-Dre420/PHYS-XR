namespace PHYSXR.Communication
{
    // Explicit communication-health signal for downstream consumers (the
    // future WorldStateManager/decision layer). The communication layer
    // reports this instead of ever picking a GhostAction itself - a
    // communication failure must surface as Stale/Invalid, not as a
    // decision.
    public enum CommunicationState
    {
        Ok,
        Stale,
        Invalid
    }
}
