using System;
using System.Collections.Generic;
using System.Text;

namespace StrandedDeepDiagnostics.Events
{
    internal sealed class EventRingBuffer
    {
        private readonly DiagnosticEvent[] _items;
        private readonly object _gate = new object();
        private int _count;
        private int _next;

        public EventRingBuffer(int capacity)
        {
            if (capacity < 32) capacity = 32;
            _items = new DiagnosticEvent[capacity];
        }

        public int Count { get { lock (_gate) { return _count; } } }
        public int Capacity { get { return _items.Length; } }

        public void Add(DiagnosticEvent item)
        {
            if (item == null) return;
            lock (_gate)
            {
                _items[_next] = item;
                _next = (_next + 1) % _items.Length;
                if (_count < _items.Length) _count++;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                Array.Clear(_items, 0, _items.Length);
                _count = 0;
                _next = 0;
            }
        }

        public List<DiagnosticEvent> Snapshot(int maxItems)
        {
            lock (_gate)
            {
                List<DiagnosticEvent> result = new List<DiagnosticEvent>();
                int take = Math.Min(_count, Math.Max(0, maxItems));
                int start = (_next - take + _items.Length) % _items.Length;
                int i;
                for (i = 0; i < take; i++)
                {
                    DiagnosticEvent item = _items[(start + i) % _items.Length];
                    if (item != null) result.Add(item);
                }
                return result;
            }
        }

        public string RenderReport(int maxItems)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== EVENT RING BUFFER ===");
            sb.AppendLine("Count: " + Count + " / " + _items.Length);
            List<DiagnosticEvent> items = Snapshot(maxItems);
            int i;
            for (i = 0; i < items.Count; i++)
            {
                DiagnosticEvent e = items[i];
                sb.Append(e.TimestampUtc.ToString("o"));
                sb.Append(" frame="); sb.Append(e.Frame);
                sb.Append(" phase="); sb.Append(e.Phase ?? "");
                sb.Append(" category="); sb.Append(e.Category ?? "");
                sb.Append(" module="); sb.Append(e.Module ?? "");
                if (!string.IsNullOrEmpty(e.Player)) { sb.Append(" player="); sb.Append(e.Player); }
                if (!string.IsNullOrEmpty(e.Target)) { sb.Append(" target="); sb.Append(e.Target); }
                if (!string.IsNullOrEmpty(e.Method)) { sb.Append(" method="); sb.Append(e.Method); }
                if (!string.IsNullOrEmpty(e.Payload)) { sb.Append(" | "); sb.Append(e.Payload); }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
