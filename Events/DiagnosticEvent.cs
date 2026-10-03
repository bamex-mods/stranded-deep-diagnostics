using System;

namespace StrandedDeepDiagnostics.Events
{
    internal sealed class DiagnosticEvent
    {
        public DateTime TimestampUtc;
        public int Frame;
        public string Phase;
        public string Category;
        public string Module;
        public string Player;
        public string Target;
        public string Method;
        public string Payload;
    }
}
