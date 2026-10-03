using System;
using System.Collections.Generic;
using System.Text;
using StrandedDeepDiagnostics.Plugins;

namespace StrandedDeepDiagnostics.Adapters
{
    internal sealed class AdapterInspector
    {
        private sealed class AdapterDescriptor
        {
            public string Id;
            public string DisplayName;
            public string[] Tokens;
        }

        private static readonly AdapterDescriptor[] Adapters = new AdapterDescriptor[]
        {
            new AdapterDescriptor
            {
                Id = "bamex.boombox",
                DisplayName = "BamEx Boombox",
                Tokens = new string[] { "Boombox", "StrandedDeepBoombox" }
            },
            new AdapterDescriptor
            {
                Id = "bamex.raft-furniture",
                DisplayName = "BamEx Raft Furniture",
                Tokens = new string[] { "Raft Furniture", "RaftFurniture", "StrandedDeepRaftFurniture" }
            }
        };

        private readonly LoadedPluginsInspector _plugins;

        public AdapterInspector(LoadedPluginsInspector plugins)
        {
            _plugins = plugins;
        }

        public string RenderReport(float now)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== OPTIONAL ADAPTER DISCOVERY ===");
            sb.AppendLine("Adapters are one-way read-only integrations. Production mods do not reference Diagnostics.");

            IList<LoadedPluginRecord> records = _plugins.GetRecords(now, false);
            int a;
            for (a = 0; a < Adapters.Length; a++)
            {
                AdapterDescriptor adapter = Adapters[a];
                LoadedPluginRecord match = FindMatch(records, adapter.Tokens);
                if (match == null)
                {
                    sb.AppendLine(adapter.Id + " | UNAVAILABLE | target plugin not detected");
                }
                else
                {
                    sb.AppendLine(adapter.Id + " | AVAILABLE | " + adapter.DisplayName + " | " + match.Guid + " | " + match.Version);
                }
            }
            return sb.ToString();
        }

        private static LoadedPluginRecord FindMatch(IList<LoadedPluginRecord> records, string[] tokens)
        {
            int i;
            for (i = 0; i < records.Count; i++)
            {
                LoadedPluginRecord record = records[i];
                if (Matches(record.Name, tokens) || Matches(record.Guid, tokens)) return record;
            }
            return null;
        }

        private static bool Matches(string text, string[] tokens)
        {
            if (string.IsNullOrEmpty(text) || tokens == null) return false;
            int i;
            for (i = 0; i < tokens.Length; i++)
            {
                if (text.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}
