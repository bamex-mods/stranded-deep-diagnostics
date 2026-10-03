using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using StrandedDeepDiagnostics.Events;
using StrandedDeepDiagnostics.Reflection;

namespace StrandedDeepDiagnostics.Tracing
{
    internal sealed class TraceEngine
    {
        private readonly Harmony _harmony;
        private readonly EventRingBuffer _events;
        private readonly FrequencyProfiler _frequency = new FrequencyProfiler();
        private readonly List<MethodBase> _patched = new List<MethodBase>();
        private string _profile;

        public static TraceEngine Current;

        public TraceEngine(EventRingBuffer events)
        {
            _events = events;
            _harmony = new Harmony("com.bamex.strandeddeep.diagnostics.trace");
        }

        public bool Active { get { return _patched.Count > 0; } }
        public string Profile { get { return string.IsNullOrEmpty(_profile) ? "OFF" : _profile; } }
        public int PatchedCount { get { return _patched.Count; } }

        public string Toggle(string requestedProfile)
        {
            if (Active && string.Equals(_profile, requestedProfile, StringComparison.OrdinalIgnoreCase))
            {
                Disable();
                return "Trace OFF";
            }
            Disable();
            int count = EnableProfile(requestedProfile);
            return count == 0 ? "Trace profile found no patchable methods" : "Trace ON " + Profile + " methods=" + count;
        }

        public int EnableProfile(string profile)
        {
            _profile = string.IsNullOrEmpty(profile) ? "TRACE" : profile;
            Current = this;
            _frequency.Clear();

            List<TraceCandidate> candidates = BuildCandidates(_profile);
            MethodInfo prefixMethod = typeof(TracePatchBridge).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public);
            MethodInfo postfixMethod = typeof(TracePatchBridge).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public);
            HarmonyMethod prefix = new HarmonyMethod(prefixMethod);
            HarmonyMethod postfix = new HarmonyMethod(postfixMethod);

            int i;
            for (i = 0; i < candidates.Count; i++)
            {
                TraceCandidate candidate = candidates[i];
                List<MethodBase> methods = ResolveMethods(candidate.TypeNames, candidate.MethodNames);
                int j;
                for (j = 0; j < methods.Count; j++)
                {
                    MethodBase method = methods[j];
                    if (ContainsMethod(method)) continue;
                    try
                    {
                        _harmony.Patch(method, prefix, postfix, null, null, null);
                        _patched.Add(method);
                    }
                    catch (Exception ex)
                    {
                        AddEvent("TRACE_PATCH", "PATCH-FAILED", method, null, ex.GetType().Name + ": " + ex.Message);
                    }
                }
            }

            AddEvent("TRACE", "ENABLED", null, null, "profile=" + _profile + " methods=" + _patched.Count);
            return _patched.Count;
        }

        public void Disable()
        {
            if (_patched.Count > 0)
            {
                try { _harmony.UnpatchSelf(); } catch { }
                AddEvent("TRACE", "DISABLED", null, null, "profile=" + _profile + " methods=" + _patched.Count);
            }
            _patched.Clear();
            _profile = null;
            if (Current == this) Current = null;
        }

        public void Record(string phase, MethodBase method, object[] args)
        {
            if (method == null) return;
            string key = SafeMethodName(method);
            _frequency.Increment(key + " " + phase);
            string payload = FormatArgs(args, 6);
            AddEvent("TRACE", phase, method, null, payload);
        }

        public string[] DescribeOverlay()
        {
            List<string> lines = new List<string>();
            lines.Add("TRACE " + (Active ? "ON" : "OFF") + " profile=" + Profile + " patched=" + PatchedCount);
            lines.Add("Events=" + _events.Count + "/" + _events.Capacity + " (ring buffer; not spammed to BepInEx log)");
            List<DiagnosticEvent> recent = _events.Snapshot(4);
            int i;
            for (i = recent.Count - 1; i >= 0; i--)
            {
                DiagnosticEvent e = recent[i];
                string text = (e.Phase ?? "") + " " + (e.Method ?? e.Category ?? "");
                if (text.Length > 72) text = text.Substring(0, 69) + "...";
                lines.Add("  " + text);
            }
            lines.Add("F11 toggles targeted Harmony Prefix/Postfix for current module; no args/results/control-flow changed");
            lines.Add("F12 exports events + frequency table");
            return lines.ToArray();
        }

        public string RenderReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== TRACE ENGINE ===");
            sb.AppendLine("Active: " + Active);
            sb.AppendLine("Profile: " + Profile);
            sb.AppendLine("Patched methods: " + _patched.Count);
            int i;
            for (i = 0; i < _patched.Count; i++) sb.AppendLine("  " + SafeMethodName(_patched[i]));
            sb.AppendLine();
            sb.Append(_frequency.RenderReport());
            sb.AppendLine();
            sb.Append(_events.RenderReport(1000));
            return sb.ToString();
        }

        private void AddEvent(string category, string phase, MethodBase method, string target, string payload)
        {
            DiagnosticEvent e = new DiagnosticEvent();
            e.TimestampUtc = DateTime.UtcNow;
            e.Frame = Time.frameCount;
            e.Phase = phase;
            e.Category = category;
            e.Module = _profile;
            e.Target = target;
            e.Method = method == null ? null : SafeMethodName(method);
            e.Payload = payload;
            _events.Add(e);
        }

        private static string FormatArgs(object[] args, int max)
        {
            if (args == null || args.Length == 0) return "args=[]";
            StringBuilder sb = new StringBuilder();
            sb.Append("args=[");
            int count = Math.Min(args.Length, max);
            int i;
            for (i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(ValueFormatter.FormatSimple(args[i]));
            }
            if (args.Length > count) sb.Append(", ...");
            sb.Append("]");
            return sb.ToString();
        }

        private static List<TraceCandidate> BuildCandidates(string profile)
        {
            List<TraceCandidate> list = new List<TraceCandidate>();
            string p = (profile ?? "").ToUpperInvariant();
            if (p == "INTERACTION" || p == "TRACE")
                list.Add(new TraceCandidate(new string[] { "Beam.InteractiveObject", "Beam.Crafting.Interacter", "Beam.Interacter" }, new string[] { "Interact", "Pickup", "Drop", "ReplicateInteract" }));
            if (p == "STORAGE" || p == "TRACE")
            {
                list.Add(new TraceCandidate(new string[] { "Beam.SlotStorage" }, new string[] { "Store", "GetStored", "SetStored", "Clear", "StorageCommand" }));
                list.Add(new TraceCandidate(new string[] { "Beam.Crafting.Interactive_STORAGE" }, new string[] { "Interact", "Open", "Close" }));
            }
            if (p == "INVENTORY" || p == "TRACE")
                list.Add(new TraceCandidate(new string[] { "Beam.Crafting.Holder" }, new string[] { "Hold", "Drop", "Pickup", "Release", "Select", "SwitchCurrent", "Set", "Clear", "HolderCommand" }));
            if (p == "SAVEABLE" || p == "WORLD" || p == "TRACE")
                list.Add(new TraceCandidate(new string[] { "SaveManager", "Beam.SaveManager" }, new string[] { "SaveGame", "LoadGame", "Save", "Load" }));
            if (p == "CONSTRUCTION" || p == "TRACE")
            {
                list.Add(new TraceCandidate(new string[] { "Beam.Crafting.Crafter" }, new string[] { "FinishPlacing", "Place", "Craft", "ReplicateCraft", "ReplicateFinishPlacing" }));
                list.Add(new TraceCandidate(new string[] { "Funlabs.MultiplayerMng" }, new string[] { "Instantiate" }));
            }
            return list;
        }

        private static List<MethodBase> ResolveMethods(string[] typeNames, string[] methodNames)
        {
            List<MethodBase> result = new List<MethodBase>();
            Assembly assembly = FindAssembly("Assembly-CSharp");
            if (assembly == null) return result;
            int t;
            for (t = 0; t < typeNames.Length; t++)
            {
                Type type = null;
                try { type = assembly.GetType(typeNames[t], false, false); } catch { }
                if (type == null) continue;
                MethodInfo[] methods;
                try { methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { methods = new MethodInfo[0]; }
                int i;
                for (i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.IsAbstract || method.ContainsGenericParameters) continue;
                    int n;
                    for (n = 0; n < methodNames.Length; n++)
                    {
                        if (string.Equals(method.Name, methodNames[n], StringComparison.Ordinal))
                        {
                            result.Add(method);
                            break;
                        }
                    }
                }
            }
            return result;
        }

        private bool ContainsMethod(MethodBase method)
        {
            int i;
            for (i = 0; i < _patched.Count; i++) if (_patched[i] == method) return true;
            return false;
        }

        private static Assembly FindAssembly(string name)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            int i;
            for (i = 0; i < assemblies.Length; i++)
            {
                try { if (string.Equals(assemblies[i].GetName().Name, name, StringComparison.OrdinalIgnoreCase)) return assemblies[i]; }
                catch { }
            }
            return null;
        }

        private static string SafeMethodName(MethodBase method)
        {
            try { return (method.DeclaringType == null ? "<type>" : method.DeclaringType.FullName) + "." + method.Name; }
            catch { return "<method unavailable>"; }
        }

        private sealed class TraceCandidate
        {
            public readonly string[] TypeNames;
            public readonly string[] MethodNames;
            public TraceCandidate(string[] typeNames, string[] methodNames) { TypeNames = typeNames; MethodNames = methodNames; }
        }
    }

    public static class TracePatchBridge
    {
        public static void Prefix(MethodBase __originalMethod, object[] __args)
        {
            TraceEngine current = TraceEngine.Current;
            if (current != null) current.Record("PREFIX", __originalMethod, __args);
        }

        public static void Postfix(MethodBase __originalMethod, object[] __args)
        {
            TraceEngine current = TraceEngine.Current;
            if (current != null) current.Record("POSTFIX", __originalMethod, __args);
        }
    }
}
