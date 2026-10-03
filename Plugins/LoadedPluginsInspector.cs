using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx;

namespace StrandedDeepDiagnostics.Plugins
{
    internal sealed class LoadedPluginRecord
    {
        public string Guid;
        public string Name;
        public string Version;
        public string Location;
        public string InstanceType;
        public bool LooksDiagnostic;
    }

    internal sealed class LoadedPluginsInspector
    {
        private List<LoadedPluginRecord> _records = new List<LoadedPluginRecord>();
        private float _lastRefreshRealtime = -1000f;

        public IList<LoadedPluginRecord> GetRecords(float realtime, bool force)
        {
            if (force || realtime - _lastRefreshRealtime >= 2f)
            {
                Refresh();
                _lastRefreshRealtime = realtime;
            }
            return _records.AsReadOnly();
        }

        public string RenderReport(float realtime)
        {
            IList<LoadedPluginRecord> records = GetRecords(realtime, true);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("STRANDED DEEP DIAGNOSTICS - LOADED PLUGINS");
            sb.AppendLine("Count: " + records.Count);
            sb.AppendLine();

            int i;
            for (i = 0; i < records.Count; i++)
            {
                LoadedPluginRecord record = records[i];
                sb.AppendLine("[" + (i + 1) + "] " + record.Name);
                sb.AppendLine("GUID: " + record.Guid);
                sb.AppendLine("Version: " + record.Version);
                sb.AppendLine("Location: " + record.Location);
                sb.AppendLine("InstanceType: " + record.InstanceType);
                sb.AppendLine("DiagnosticLike: " + record.LooksDiagnostic);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void Refresh()
        {
            List<LoadedPluginRecord> result = new List<LoadedPluginRecord>();

            try
            {
                Assembly bepinex = typeof(BaseUnityPlugin).Assembly;
                Type chainloaderType = bepinex.GetType("BepInEx.Bootstrap.Chainloader", false, false);
                if (chainloaderType == null)
                {
                    _records = result;
                    return;
                }

                PropertyInfo property = chainloaderType.GetProperty("PluginInfos", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (property == null)
                {
                    _records = result;
                    return;
                }

                object dictionaryObject = property.GetValue(null, null);
                IDictionary dictionary = dictionaryObject as IDictionary;
                if (dictionary == null)
                {
                    _records = result;
                    return;
                }

                foreach (DictionaryEntry entry in dictionary)
                {
                    LoadedPluginRecord record = BuildRecord(entry.Key, entry.Value);
                    result.Add(record);
                }
            }
            catch
            {
            }

            result.Sort(delegate(LoadedPluginRecord a, LoadedPluginRecord b)
            {
                return string.Compare(a.Guid, b.Guid, StringComparison.OrdinalIgnoreCase);
            });

            _records = result;
        }

        private static LoadedPluginRecord BuildRecord(object key, object info)
        {
            LoadedPluginRecord record = new LoadedPluginRecord();
            record.Guid = key == null ? "<unknown>" : Convert.ToString(key);
            record.Name = "<unknown>";
            record.Version = "<unknown>";
            record.Location = "<unknown>";
            record.InstanceType = "<unknown>";

            if (info != null)
            {
                object metadata = ReadKnownMember(info, "Metadata");
                if (metadata != null)
                {
                    object guid = ReadKnownMember(metadata, "GUID");
                    object name = ReadKnownMember(metadata, "Name");
                    object version = ReadKnownMember(metadata, "Version");
                    if (guid != null) record.Guid = Convert.ToString(guid);
                    if (name != null) record.Name = Convert.ToString(name);
                    if (version != null) record.Version = Convert.ToString(version);
                }

                object location = ReadKnownMember(info, "Location");
                if (location != null) record.Location = Convert.ToString(location);

                object instance = ReadKnownMember(info, "Instance");
                if (instance != null) record.InstanceType = instance.GetType().FullName;
            }

            string combined = (record.Guid + " " + record.Name + " " + record.Location).ToLowerInvariant();
            record.LooksDiagnostic =
                combined.IndexOf("diagnostic", StringComparison.Ordinal) >= 0 ||
                combined.IndexOf("inspector", StringComparison.Ordinal) >= 0 ||
                combined.IndexOf("probe", StringComparison.Ordinal) >= 0 ||
                combined.IndexOf("trace", StringComparison.Ordinal) >= 0 ||
                combined.IndexOf("validator", StringComparison.Ordinal) >= 0;
            return record;
        }

        private static object ReadKnownMember(object instance, string name)
        {
            if (instance == null) return null;
            Type type = instance.GetType();

            try
            {
                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.GetIndexParameters().Length == 0 && property.GetGetMethod(true) != null)
                {
                    return property.GetValue(instance, null);
                }
            }
            catch
            {
            }

            try
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return field.GetValue(instance);
            }
            catch
            {
            }

            return null;
        }
    }
}
