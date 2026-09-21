using System.Collections.Generic;
using PHYSXR.Core.Data;

namespace PHYSXR.PhysicalWorld
{
    public class PhysicalEventManager
    {
        private readonly HashSet<long> processedEventIds = new HashSet<long>();

        public bool RegisterEvent(PhysicalEvent physicalEvent)
        {
            if (physicalEvent == null)
                return false;

            if (processedEventIds.Contains(physicalEvent.eventId))
                return false;

            processedEventIds.Add(physicalEvent.eventId);
            return true;
        }

        public bool HasProcessedEvent(long eventId)
        {
            return processedEventIds.Contains(eventId);
        }
    }
}