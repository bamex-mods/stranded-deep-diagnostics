using System;
using System.Collections.Generic;
using System.Text;

namespace StrandedDeepDiagnostics.Tracing
{
    internal sealed class FrequencyProfiler
    {
        private readonly Dictionary<string, long> _counts = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly object _gate = new object();

        public void Increment(string key)
        {
            if (string.IsNullOrEmpty(key)) key = "<unknown>";
            lock (_gate)
            {
                long count;
                if (!_counts.TryGetValue(key, out count)) count = 0;
                _counts[key] = count + 1;
            }
        }

        public void Clear() { lock (_gate) { _counts.Clear(); } }

        public string RenderReport()
        {
            List<KeyValuePair<string, long>> items;
            lock (_gate) { items = new List<KeyValuePair<string, long>>(_counts); }
            items.Sort(delegate(KeyValuePair<string, long> a, KeyValuePair<string, long> b) { return b.Value.CompareTo(a.Value); });
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== TRACE FREQUENCY ===");
            int i;
            for (i = 0; i < items.Count; i++) sb.AppendLine(items[i].Value + "  " + items[i].Key);
            return sb.ToString();
        }
    }
}
