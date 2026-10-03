using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using StrandedDeepDiagnostics.Core;
using StrandedDeepDiagnostics.Inspection;
using StrandedDeepDiagnostics.Reflection;

namespace StrandedDeepDiagnostics.World
{
    internal sealed class WorldInspector
    {
        private readonly Type _worldType;
        private readonly Type _gameTimeType;

        public WorldInspector(CapabilityManifest manifest)
        {
            Assembly assembly = FindAssembly("Assembly-CSharp");
            _worldType = FindType(assembly, new string[] { "StrandedWorld", "Beam.StrandedWorld" });
            _gameTimeType = FindType(assembly, new string[] { "GameTime", "Beam.GameTime", "Beam.GameTimeManager" });
        }

        public string[] DescribeOverlay()
        {
            string world = _worldType == null ? "UNAVAILABLE" : _worldType.FullName;
            UnityEngine.Object instance = FindFirstLive(_worldType);
            string instanceText = instance == null ? "<none>" : ValueFormatter.FormatSimple(instance);
            return new string[]
            {
                "WORLD type=" + world,
                "INSTANCE " + instanceText,
                "GAMETIME type=" + (_gameTimeType == null ? "<unresolved>" : _gameTimeType.FullName),
                "F12 exports bounded Zones census and known world/time members",
                "No zone activation, terrain sampling or streaming mutation is performed"
            };
        }

        public string RenderReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== WORLD / ZONE / GAMETIME INSPECTOR ===");
            sb.AppendLine("WorldType: " + (_worldType == null ? "<unresolved>" : _worldType.FullName));
            sb.AppendLine("GameTimeType: " + (_gameTimeType == null ? "<unresolved>" : _gameTimeType.FullName));
            UnityEngine.Object world = FindFirstLive(_worldType);
            if (world == null)
            {
                sb.AppendLine("World instance: <none>");
                return sb.ToString();
            }

            sb.AppendLine("World instance: " + ValueFormatter.FormatSimple(world));
            sb.Append(InspectionUtil.ReadKnownMembers(world, new string[] { "Seed", "WorldSeed", "WORLD_SEED", "Zones", "CurrentZone", "IsLoaded", "Loaded" }, 16));
            sb.Append(InspectionUtil.ReadMatchingFields(world, new string[] { "seed", "zone", "load", "world" }, 32, 16));

            object zones = SafeReflection.GetKnownMemberValue(world, "Zones");
            if (zones != null)
            {
                sb.AppendLine();
                sb.AppendLine("=== ZONES (bounded to 32) ===");
                IList list = zones as IList;
                if (list != null)
                {
                    int count = 0;
                    try { count = list.Count; } catch { }
                    sb.AppendLine("ZoneCount: " + count);
                    int i;
                    int max = Math.Min(count, 32);
                    for (i = 0; i < max; i++)
                    {
                        object zone = null;
                        try { zone = list[i]; } catch { }
                        if (zone == null) continue;
                        sb.AppendLine("ZONE[" + i + "] " + ValueFormatter.FormatSimple(zone));
                        sb.Append(InspectionUtil.ReadKnownMembers(zone, new string[] { "Id", "ZoneId", "ZoneName", "Name", "Biome", "HasVisited", "Terrain", "IsLoaded", "Loaded" }, 4));
                    }
                    if (count > max) sb.AppendLine("<zones truncated>");
                }
                else
                {
                    sb.AppendLine(InspectionUtil.FormatBoundedValue(zones, 16));
                }
            }

            UnityEngine.Object gameTime = FindFirstLive(_gameTimeType);
            if (gameTime != null)
            {
                sb.AppendLine();
                sb.AppendLine("=== GAMETIME ===");
                sb.AppendLine(ValueFormatter.FormatSimple(gameTime));
                sb.Append(InspectionUtil.ReadKnownMembers(gameTime, new string[] { "Time", "CurrentTime", "Day", "Days", "Hour", "Hours", "Minutes", "Elapsed", "DateTime" }, 8));
                sb.Append(InspectionUtil.ReadMatchingFields(gameTime, new string[] { "time", "day", "hour", "minute" }, 24, 8));
            }
            return sb.ToString();
        }

        private static UnityEngine.Object FindFirstLive(Type type)
        {
            if (type == null || !typeof(UnityEngine.Object).IsAssignableFrom(type)) return null;
            try
            {
                UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(type);
                int i;
                for (i = 0; i < objects.Length; i++)
                {
                    Component c = objects[i] as Component;
                    if (c != null)
                    {
                        try { if (!c.gameObject.scene.IsValid()) continue; } catch { continue; }
                    }
                    return objects[i];
                }
            }
            catch { }
            return null;
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

        private static Type FindType(Assembly assembly, string[] names)
        {
            if (assembly == null) return null;
            int i;
            for (i = 0; i < names.Length; i++)
            {
                try { Type t = assembly.GetType(names[i], false, false); if (t != null) return t; } catch { }
            }
            return null;
        }
    }
}
