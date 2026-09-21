using PHYSXR.Core.Data;
using PHYSXR.Core.Enums;

namespace PHYSXR.Testing
{
    // Deterministic PhysicalState generator for testing the decision pipeline
    // without hardware. Produces the same PhysicalState contract the real
    // ESP32 communication path will populate, so callers cannot tell the
    // difference between a simulated and a real reading.
    public class SimulatedPhysicalStateSource
    {
        public PhysicalState CreatePhysicalState(
            PresenceLevel presence,
            ConfidenceLevel confidence,
            bool disturbance,
            long timestamp)
        {
            return new PhysicalState
            {
                presence = presence,
                confidence = confidence,
                disturbance = disturbance,
                timestamp = timestamp
            };
        }
    }
}