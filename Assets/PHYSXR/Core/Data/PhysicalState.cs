using PHYSXR.Core.Enums;

namespace PHYSXR.Core.Data
{
    public class PhysicalState
    {
        public PresenceLevel presence;
        public ConfidenceLevel confidence;
        public bool disturbance;
        public long timestamp;
    }
}