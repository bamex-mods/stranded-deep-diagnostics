using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace StrandedDeepDiagnostics.Audio
{
    internal static class AudioDiscovery
    {
        public static Type FindTypeBySimpleName(string simpleName)
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

        public static bool IsRuntimeComponent(Component component)
        {
            if (component == null) return false;
            try
            {
                GameObject go = component.gameObject;
                if (go == null) return false;
                return go.scene.IsValid();
            }
            catch
            {
                return false;
            }
        }

        public static string BuildPath(Transform transform, int maxDepth)
        {
            if (transform == null) return string.Empty;
            List<string> parts = new List<string>();
            Transform current = transform;
            int depth = 0;
            while (current != null && depth < maxDepth)
            {
                parts.Add(SafeName(current.gameObject));
                current = current.parent;
                depth++;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static string SafeName(UnityEngine.Object obj)
        {
            if (obj == null) return string.Empty;
            try { return obj.name ?? string.Empty; }
            catch { return string.Empty; }
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
    }
}
