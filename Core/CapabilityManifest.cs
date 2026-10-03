using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using UnityEngine;

namespace StrandedDeepDiagnostics.Core
{
    internal sealed class CapabilityRecord
    {
        public string Name;
        public string Status;
        public string Detail;
    }

    internal sealed class CapabilityManifest
    {
        public const string Available = "AVAILABLE";
        public const string Partial = "PARTIAL";
        public const string Unavailable = "UNAVAILABLE";

        private readonly List<CapabilityRecord> _records = new List<CapabilityRecord>();
        private readonly Dictionary<string, CapabilityRecord> _byName = new Dictionary<string, CapabilityRecord>(StringComparer.OrdinalIgnoreCase);

        public string GameVersion { get; private set; }
        public string GameExecutableFileVersion { get; private set; }
        public string GameExecutableProductVersion { get; private set; }
        public string AssemblyCSharpFileVersion { get; private set; }
        public string AssemblyCSharpProductVersion { get; private set; }
        public string UnityVersion { get; private set; }
        public string AssemblyCSharpPath { get; private set; }
        public string AssemblyCSharpSha256 { get; private set; }
        public string AssemblyCSharpMvid { get; private set; }
        public string BepInExVersion { get; private set; }
        public string HarmonyVersion { get; private set; }

        public IList<CapabilityRecord> Records
        {
            get { return _records.AsReadOnly(); }
        }

        public Type BeamPlayerType { get; private set; }

        public static CapabilityManifest Build()
        {
            CapabilityManifest manifest = new CapabilityManifest();
            manifest.GameVersion = SafeString(Application.version);
            manifest.UnityVersion = SafeString(Application.unityVersion);
            string executablePath = ResolveGameExecutablePath();
            manifest.GameExecutableFileVersion = TryFileVersion(executablePath, true);
            manifest.GameExecutableProductVersion = TryFileVersion(executablePath, false);
            manifest.BepInExVersion = SafeVersion(typeof(BaseUnityPlugin).Assembly);
            manifest.HarmonyVersion = FindLoadedAssemblyVersion("0Harmony");

            Assembly assemblyCSharp = FindLoadedAssembly("Assembly-CSharp");
            if (assemblyCSharp != null)
            {
                try { manifest.AssemblyCSharpMvid = assemblyCSharp.ManifestModule.ModuleVersionId.ToString("D"); }
                catch { manifest.AssemblyCSharpMvid = "<unavailable>"; }

                manifest.AssemblyCSharpPath = ResolveAssemblyPath(assemblyCSharp);
                manifest.AssemblyCSharpSha256 = TrySha256(manifest.AssemblyCSharpPath);
                manifest.AssemblyCSharpFileVersion = TryFileVersion(manifest.AssemblyCSharpPath, true);
                manifest.AssemblyCSharpProductVersion = TryFileVersion(manifest.AssemblyCSharpPath, false);
                manifest.Add("Core.Assembly-CSharp", Available, assemblyCSharp.FullName);
            }
            else
            {
                manifest.AssemblyCSharpPath = "<assembly not loaded>";
                manifest.AssemblyCSharpSha256 = "<unavailable>";
                manifest.AssemblyCSharpMvid = "<unavailable>";
                manifest.AssemblyCSharpFileVersion = "<unavailable>";
                manifest.AssemblyCSharpProductVersion = "<unavailable>";
                manifest.Add("Core.Assembly-CSharp", Unavailable, "assembly not loaded");
            }

            Assembly harmonyAssembly = FindLoadedAssembly("0Harmony");
            manifest.Add("Core.Harmony", harmonyAssembly == null ? Unavailable : Available,
                harmonyAssembly == null ? "0Harmony not loaded; targeted trace is unavailable" : SafeVersion(harmonyAssembly));

            Type beamPlayer = manifest.AddTypeCapability(assemblyCSharp, "Game.Beam.Player", new string[] { "Beam.Player" });
            manifest.BeamPlayerType = beamPlayer;
            Type strandedWorld = manifest.AddTypeCapability(assemblyCSharp, "Game.StrandedWorld", new string[] { "StrandedWorld", "Beam.StrandedWorld" });
            Type saveManager = manifest.AddTypeCapability(assemblyCSharp, "Game.SaveManager", new string[] { "SaveManager", "Beam.SaveManager" });
            Type interactiveObject = manifest.AddTypeCapability(assemblyCSharp, "Game.Beam.InteractiveObject", new string[] { "Beam.InteractiveObject" });
            Type slotStorage = manifest.AddTypeCapability(assemblyCSharp, "Game.Beam.SlotStorage", new string[] { "Beam.SlotStorage" });
            Type interactiveStorage = manifest.AddTypeCapability(assemblyCSharp, "Game.Beam.Crafting.Interactive_STORAGE", new string[] { "Beam.Crafting.Interactive_STORAGE" });
            Type constructionObject = manifest.AddTypeCapability(assemblyCSharp, "Game.Beam.Crafting.ConstructionObject", new string[] { "Beam.Crafting.ConstructionObject" });
            Type crafter = manifest.AddTypeCapability(assemblyCSharp, "Game.Beam.Crafting.Crafter", new string[] { "Beam.Crafting.Crafter" });
            Type ghost = manifest.AddTypeCapability(assemblyCSharp, "Game.Beam.Crafting.Ghost", new string[] { "Beam.Crafting.Ghost" });
            Type holder = manifest.AddTypeCapability(assemblyCSharp, "Game.Beam.Crafting.Holder", new string[] { "Beam.Crafting.Holder" });
            Type gameTime = manifest.AddTypeCapability(assemblyCSharp, "Game.GameTime", new string[] { "GameTime", "Beam.GameTime", "Beam.GameTimeManager" });
            Type zone = manifest.AddTypeCapability(assemblyCSharp, "Game.Zone", new string[] { "Zone", "Beam.Zone" });
            Type saveablePrefab = manifest.AddTypeCapability(assemblyCSharp, "Game.SaveablePrefab", new string[] { "Beam.SaveablePrefab", "SaveablePrefab" });
            Type saveableReference = manifest.AddTypeCapability(assemblyCSharp, "Game.SaveableReference", new string[] { "Beam.SaveableReference", "SaveableReference" });
            Type multiplayer = manifest.AddTypeCapability(assemblyCSharp, "Game.Funlabs.MultiplayerMng", new string[] { "Funlabs.MultiplayerMng" });

            Assembly boltAssembly = FindLoadedAssembly("bolt");
            Type boltEntity = manifest.AddTypeCapability(boltAssembly, "Game.Photon.Bolt.BoltEntity", new string[] { "Photon.Bolt.BoltEntity" });

            Assembly rewiredAssembly = FindLoadedAssembly("Rewired_Core");
            Type rewiredPlayer = manifest.AddTypeCapability(rewiredAssembly, "Game.Rewired.Player", new string[] { "Rewired.Player" });

            manifest.AddComposite("Module.INPUT", new Type[] { beamPlayer, rewiredPlayer }, 1,
                "Beam.Player + Rewired.Player");
            manifest.AddComposite("Module.STORAGE", new Type[] { slotStorage, interactiveStorage }, 1,
                "SlotStorage + Interactive_STORAGE");
            manifest.AddComposite("Module.INVENTORY", new Type[] { beamPlayer, holder }, 1,
                "Beam.Player + Holder");
            manifest.AddComposite("Module.WORLD", new Type[] { strandedWorld, gameTime, zone }, 1,
                "StrandedWorld/GameTime/Zone runtime discovery");
            manifest.AddComposite("Module.CONSTRUCTION", new Type[] { constructionObject, crafter, ghost }, 1,
                "ConstructionObject/Crafter/Ghost");
            manifest.AddComposite("Module.SAVEABLE", new Type[] { saveablePrefab, saveableReference }, 1,
                "SaveablePrefab/SaveableReference");
            manifest.AddComposite("Module.INTERACTION", new Type[] { interactiveObject }, 1,
                "Beam.InteractiveObject");
            manifest.Add("Module.TRACE",
                assemblyCSharp != null && harmonyAssembly != null ? Available : (assemblyCSharp != null || harmonyAssembly != null ? Partial : Unavailable),
                harmonyAssembly == null ? "Harmony unavailable" : "targeted Harmony instrumentation available");

            Type raftType = FindTypeBySimpleNameAcrossLoadedAssemblies("STRUCTURE_RAFT");
            manifest.Add("Module.RAFT", raftType == null ? Partial : Available,
                raftType == null ? "runtime raft type is discovered lazily by token/watch-set" : raftType.FullName);

            manifest.Add("Module.AUDIO", Available,
                "generic Unity AudioSource/voice/frame inspection; specialized DSP adapters are optional");

            Type boomboxProcessor = FindTypeBySimpleNameAcrossLoadedAssemblies("SplitStereoProcessor");
            manifest.Add("Adapter.BamEx.Boombox", boomboxProcessor == null ? Unavailable : Available,
                boomboxProcessor == null ? "optional adapter not detected" : boomboxProcessor.FullName);

            Type raftFollower = FindTypeBySimpleNameAcrossLoadedAssemblies("RaftSmallStructureFollower");
            Type raftReposition = FindTypeBySimpleNameAcrossLoadedAssemblies("RaftRepositionController");
            int raftAdapterCount = (raftFollower == null ? 0 : 1) + (raftReposition == null ? 0 : 1);
            manifest.Add("Adapter.BamEx.RaftFurniture",
                raftAdapterCount == 0 ? Unavailable : (raftAdapterCount == 2 ? Available : Partial),
                "optional types detected=" + raftAdapterCount + "/2");

            manifest.Add("Core.ReadOnlySafety", Available,
                "no SaveGame command, spawn, Transform/Rigidbody mutation, IgnoreCollision mutation, or Physics.SyncTransforms in stable core");

            return manifest;
        }

        public string GetStatus(string name)
        {
            CapabilityRecord record;
            if (_byName.TryGetValue(name, out record)) return record.Status;
            return Unavailable;
        }

        public string DescribeModule(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId)) return Unavailable;
            string key = "Module." + moduleId;
            CapabilityRecord record;
            if (_byName.TryGetValue(key, out record))
            {
                return record.Status + (string.IsNullOrEmpty(record.Detail) ? string.Empty : " | " + record.Detail);
            }

            // Modules backed only by stable Unity primitives are core-available when the plugin itself is running.
            if (string.Equals(moduleId, DiagnosticsConstants.ModuleObject, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(moduleId, DiagnosticsConstants.ModulePhysics, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(moduleId, DiagnosticsConstants.ModuleCamera, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(moduleId, DiagnosticsConstants.ModuleUI, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(moduleId, DiagnosticsConstants.ModulePlugins, StringComparison.OrdinalIgnoreCase))
            {
                return Available + " | Unity/BepInEx primitive";
            }

            return Partial + " | no explicit capability record";
        }

        private void Add(string name, string status, string detail)
        {
            CapabilityRecord record = new CapabilityRecord();
            record.Name = name;
            record.Status = status;
            record.Detail = detail;
            _records.Add(record);
            _byName[name] = record;
        }

        private Type AddTypeCapability(Assembly assembly, string name, string[] candidates)
        {
            Type type = FindFirstType(assembly, candidates);
            if (assembly == null)
            {
                Add(name, Unavailable, "assembly not loaded");
            }
            else if (type == null)
            {
                Add(name, Unavailable, "type not found by known exact names");
            }
            else
            {
                Add(name, Available, type.FullName);
            }
            return type;
        }

        private void AddComposite(string name, Type[] types, int minimumForPartial, string detail)
        {
            int found = 0;
            int i;
            for (i = 0; i < types.Length; i++) if (types[i] != null) found++;
            string status = found == types.Length ? Available : (found >= minimumForPartial ? Partial : Unavailable);
            Add(name, status, detail + " | found=" + found + "/" + types.Length);
        }

        public string ToText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== CAPABILITY MANIFEST ===");
            sb.AppendLine("Diagnostics: " + DiagnosticsConstants.PluginVersion);
            sb.AppendLine("Status model: AVAILABLE / PARTIAL / UNAVAILABLE");
            sb.AppendLine("Stranded Deep Application.version: " + GameVersion);
            sb.AppendLine("Unity Player executable FileVersion: " + GameExecutableFileVersion);
            sb.AppendLine("Unity Player executable ProductVersion: " + GameExecutableProductVersion);
            sb.AppendLine("UnityVersion: " + UnityVersion);
            sb.AppendLine("BepInEx: " + BepInExVersion);
            sb.AppendLine("Harmony: " + HarmonyVersion);
            sb.AppendLine("Assembly-CSharp: " + AssemblyCSharpPath);
            sb.AppendLine("Assembly-CSharp FileVersion: " + AssemblyCSharpFileVersion);
            sb.AppendLine("Assembly-CSharp ProductVersion: " + AssemblyCSharpProductVersion);
            sb.AppendLine("Assembly-CSharp SHA256: " + AssemblyCSharpSha256);
            sb.AppendLine("Assembly-CSharp MVID: " + AssemblyCSharpMvid);
            sb.AppendLine();

            int i;
            for (i = 0; i < _records.Count; i++)
            {
                CapabilityRecord record = _records[i];
                sb.Append(record.Name);
                sb.Append(" = ");
                sb.Append(record.Status);
                if (!string.IsNullOrEmpty(record.Detail))
                {
                    sb.Append(" | ");
                    sb.Append(record.Detail);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static Type FindFirstType(Assembly assembly, string[] names)
        {
            if (assembly == null || names == null) return null;
            int i;
            for (i = 0; i < names.Length; i++)
            {
                try
                {
                    Type type = assembly.GetType(names[i], false, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }

        private static Type FindTypeBySimpleNameAcrossLoadedAssemblies(string simpleName)
        {
            if (string.IsNullOrEmpty(simpleName)) return null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            int i;
            for (i = 0; i < assemblies.Length; i++)
            {
                Type[] types = GetTypesSafe(assemblies[i]);
                int t;
                for (t = 0; t < types.Length; t++)
                {
                    Type type = types[t];
                    if (type != null && string.Equals(type.Name, simpleName, StringComparison.Ordinal)) return type;
                }
            }
            return null;
        }

        private static Type[] GetTypesSafe(Assembly assembly)
        {
            if (assembly == null) return new Type[0];
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                List<Type> types = new List<Type>();
                if (ex.Types != null)
                {
                    int i;
                    for (i = 0; i < ex.Types.Length; i++) if (ex.Types[i] != null) types.Add(ex.Types[i]);
                }
                return types.ToArray();
            }
            catch { return new Type[0]; }
        }

        private static Assembly FindLoadedAssembly(string simpleName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            int i;
            for (i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    if (string.Equals(assemblies[i].GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase)) return assemblies[i];
                }
                catch { }
            }
            return null;
        }

        private static string FindLoadedAssemblyVersion(string simpleName)
        {
            Assembly assembly = FindLoadedAssembly(simpleName);
            return assembly == null ? "<not loaded>" : SafeVersion(assembly);
        }

        private static string SafeVersion(Assembly assembly)
        {
            try
            {
                Version version = assembly.GetName().Version;
                return version == null ? "<unknown>" : version.ToString();
            }
            catch { return "<unavailable>"; }
        }

        private static string ResolveAssemblyPath(Assembly assembly)
        {
            try
            {
                if (!string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location)) return assembly.Location;
            }
            catch { }

            try
            {
                string fallback = Path.Combine(Paths.GameRootPath, "Stranded_Deep_Data");
                fallback = Path.Combine(fallback, "Managed");
                fallback = Path.Combine(fallback, "Assembly-CSharp.dll");
                return fallback;
            }
            catch { return "<unavailable>"; }
        }

        private static string TrySha256(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "<unavailable>";
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream);
                    StringBuilder sb = new StringBuilder(hash.Length * 2);
                    int i;
                    for (i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return "<unavailable>"; }
        }

        private static string ResolveGameExecutablePath()
        {
            string[] names = new string[] { "Stranded_Deep.exe", "Stranded_Deep_64.exe", "Stranded Deep.exe" };
            int i;
            for (i = 0; i < names.Length; i++)
            {
                try
                {
                    string candidate = Path.Combine(Paths.GameRootPath, names[i]);
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return Path.Combine(Paths.GameRootPath, "Stranded_Deep.exe");
        }

        private static string TryFileVersion(string path, bool fileVersion)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "<unavailable>";
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                string value = fileVersion ? info.FileVersion : info.ProductVersion;
                return SafeString(value);
            }
            catch { return "<unavailable>"; }
        }

        private static string SafeString(string value)
        {
            return string.IsNullOrEmpty(value) ? "<unknown>" : value;
        }
    }
}
