using System.Collections.Generic;

namespace PHYSXR.Telemetry
{
    // Simple runtime-safe ITelemetrySink implementation: stores records in
    // insertion order, in memory only. No file logging, networking,
    // database or external service - those remain explicitly out of scope
    // until the architecture actually calls for them. Useful both as a
    // real (if minimal) runtime sink and as the natural choice for tests.
    public class InMemoryTelemetrySink : ITelemetrySink
    {
        private readonly List<TelemetryRecord> records = new List<TelemetryRecord>();

        public IReadOnlyList<TelemetryRecord> Records => records;

        public void Record(TelemetryRecord record)
        {
            if (record == null)
                return;

            records.Add(record);
        }

        public void Clear()
        {
            records.Clear();
        }
    }
}
