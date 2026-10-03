using System.Collections.Generic;

namespace StrandedDeepDiagnostics.Snapshots
{
    internal sealed class SnapshotHistory
    {
        private readonly List<DiagnosticSnapshot> _items = new List<DiagnosticSnapshot>();
        private readonly int _capacity;

        public SnapshotHistory(int capacity)
        {
            _capacity = capacity < 2 ? 2 : capacity;
        }

        public DiagnosticSnapshot Last
        {
            get
            {
                if (_items.Count == 0) return null;
                return _items[_items.Count - 1];
            }
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public void Add(DiagnosticSnapshot snapshot)
        {
            if (snapshot == null) return;
            _items.Add(snapshot);
            while (_items.Count > _capacity)
            {
                _items.RemoveAt(0);
            }
        }

        public void Clear()
        {
            _items.Clear();
        }
    }
}
