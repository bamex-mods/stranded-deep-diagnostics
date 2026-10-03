using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using StrandedDeepDiagnostics.Reflection;

namespace StrandedDeepDiagnostics.Raft
{
    internal sealed class RaftCandidate
    {
        public GameObject Root;
        public Rigidbody Body;
        public string SourceType;
        public string ReferenceId;
        public string Path;
        public int ColliderCount;
        public int ColliderHash;
        public int ChildRigidbodyCount;
        public int ChildRigidbodyHash;
    }

    internal static class RaftDiscovery
    {
        private static readonly List<Type> CandidateTypes = new List<Type>();
        private static bool _typesResolved;
        private static MethodInfo _findObjectsOfTypeAllByType;

        public static List<RaftCandidate> FindLoadedRafts()
        {
            EnsureTypes();
            List<RaftCandidate> result = new List<RaftCandidate>();
            HashSet<int> seen = new HashSet<int>();

            int i;
            for (i = 0; i < CandidateTypes.Count; i++)
            {
                Type type = CandidateTypes[i];
                Array found = FindObjects(type);
                if (found == null) continue;

                int j;
                for (j = 0; j < found.Length; j++)
                {
                    Component component = found.GetValue(j) as Component;
                    if (component == null) continue;
                    GameObject root = ResolveRaftRoot(component.gameObject);
                    if (root == null) continue;
                    try
                    {
                        if (!root.scene.IsValid()) continue;
                    }
                    catch { continue; }

                    int id;
                    try { id = root.GetInstanceID(); }
                    catch { continue; }
                    if (!seen.Add(id)) continue;

                    Rigidbody body = FindNearestRigidbody(component.transform);
                    if (body == null)
                    {
                        try { body = root.GetComponent<Rigidbody>(); }
                        catch { }
                    }
                    if (body == null) continue;
                    try
                    {
                        if (body.gameObject != null) root = body.gameObject;
                    }
                    catch { }

                    int bodyRootId;
                    try { bodyRootId = root.GetInstanceID(); } catch { continue; }
                    if (bodyRootId != id)
                    {
                        seen.Remove(id);
                        id = bodyRootId;
                        if (!seen.Add(id)) continue;
                    }

                    RaftCandidate candidate = new RaftCandidate();
                    candidate.Root = root;
                    candidate.Body = body;
                    candidate.SourceType = type.FullName ?? type.Name;
                    candidate.ReferenceId = TryReadReferenceId(root);
                    try { candidate.Path = SafeReflection.GetHierarchyPath(root.transform, 48); }
                    catch { candidate.Path = "<unavailable>"; }
                    CaptureTopology(root, body, candidate);
                    result.Add(candidate);
                }
            }

            return result;
        }

        public static GameObject ResolveRaftRoot(GameObject start)
        {
            if (start == null) return null;
            Transform current = start.transform;
            Transform bestRaftNamed = null;
            Transform bestRigidbody = null;
            int depth = 0;

            while (current != null && depth < 16)
            {
                bool raftLike = IsRaftLike(current.gameObject);
                Rigidbody rb = null;
                try { rb = current.GetComponent<Rigidbody>(); } catch { }
                if (raftLike) bestRaftNamed = current;
                if (rb != null) bestRigidbody = current;

                current = current.parent;
                depth++;
            }

            if (bestRaftNamed != null) return bestRaftNamed.gameObject;
            if (bestRigidbody != null && IsRaftLike(bestRigidbody.gameObject)) return bestRigidbody.gameObject;
            return start;
        }

        public static bool IsRaftLike(GameObject gameObject)
        {
            if (gameObject == null) return false;
            string name = string.Empty;
            try { name = gameObject.name ?? string.Empty; } catch { }
            if (name.IndexOf("STRUCTURE_RAFT", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            Component[] components;
            try { components = gameObject.GetComponents<Component>(); }
            catch { components = new Component[0]; }
            int i;
            for (i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null) continue;
                string typeName = c.GetType().FullName ?? c.GetType().Name;
                if (typeName.IndexOf("STRUCTURE_RAFT", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public static void CaptureTopology(GameObject root, Rigidbody rootBody, RaftCandidate target)
        {
            if (target == null) return;
            Collider[] colliders;
            Rigidbody[] bodies;
            try { colliders = root.GetComponentsInChildren<Collider>(true); }
            catch { colliders = new Collider[0]; }
            try { bodies = root.GetComponentsInChildren<Rigidbody>(true); }
            catch { bodies = new Rigidbody[0]; }

            int colliderHash = 0;
            int colliderCount = 0;
            int i;
            for (i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null) continue;
                int id = 0;
                int attachedId = 0;
                bool enabled = false;
                bool trigger = false;
                try { id = c.GetInstanceID(); } catch { }
                try { enabled = c.enabled; } catch { }
                try { trigger = c.isTrigger; } catch { }
                try
                {
                    Rigidbody attached = c.attachedRigidbody;
                    if (attached != null) attachedId = attached.GetInstanceID();
                }
                catch { }
                colliderCount++;
                unchecked
                {
                    colliderHash ^= (id * 397) ^ attachedId ^ (enabled ? 0x13579 : 0) ^ (trigger ? 0x24680 : 0);
                }
            }

            int bodyHash = 0;
            int bodyCount = 0;
            for (i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (rb == null || rb == rootBody) continue;
                int id = 0;
                bool kin = false;
                bool gravity = false;
                int constraints = 0;
                try { id = rb.GetInstanceID(); } catch { }
                try { kin = rb.isKinematic; } catch { }
                try { gravity = rb.useGravity; } catch { }
                try { constraints = (int)rb.constraints; } catch { }
                bodyCount++;
                unchecked
                {
                    bodyHash ^= (id * 397) ^ constraints ^ (kin ? 0x10203 : 0) ^ (gravity ? 0x40506 : 0);
                }
            }

            target.ColliderCount = colliderCount;
            target.ColliderHash = colliderHash;
            target.ChildRigidbodyCount = bodyCount;
            target.ChildRigidbodyHash = bodyHash;
        }

        private static void EnsureTypes()
        {
            if (_typesResolved) return;
            _typesResolved = true;

            try
            {
                _findObjectsOfTypeAllByType = typeof(Resources).GetMethod(
                    "FindObjectsOfTypeAll",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(Type) },
                    null);
            }
            catch { }

            Assembly[] assemblies;
            try { assemblies = AppDomain.CurrentDomain.GetAssemblies(); }
            catch { assemblies = new Assembly[0]; }

            int i;
            for (i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try { types = assemblies[i].GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                catch { types = new Type[0]; }

                if (types == null) continue;
                int j;
                for (j = 0; j < types.Length; j++)
                {
                    Type type = types[j];
                    if (type == null || !typeof(Component).IsAssignableFrom(type)) continue;
                    string name = type.FullName ?? type.Name;
                    if (name.IndexOf("STRUCTURE_RAFT", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!CandidateTypes.Contains(type)) CandidateTypes.Add(type);
                }
            }
        }

        private static Array FindObjects(Type type)
        {
            if (type == null || _findObjectsOfTypeAllByType == null) return null;
            try
            {
                object value = _findObjectsOfTypeAllByType.Invoke(null, new object[] { type });
                return value as Array;
            }
            catch { return null; }
        }

        private static Rigidbody FindNearestRigidbody(Transform start)
        {
            Transform current = start;
            int depth = 0;
            while (current != null && depth < 16)
            {
                try
                {
                    Rigidbody rb = current.GetComponent<Rigidbody>();
                    if (rb != null) return rb;
                }
                catch { }
                current = current.parent;
                depth++;
            }
            return null;
        }

        private static string TryReadReferenceId(GameObject root)
        {
            if (root == null) return "<unknown>";
            Component[] components;
            try { components = root.GetComponentsInChildren<Component>(true); }
            catch { components = new Component[0]; }

            int max = Math.Min(components.Length, 96);
            int i;
            for (i = 0; i < max; i++)
            {
                Component c = components[i];
                if (c == null) continue;
                string typeName = c.GetType().FullName ?? c.GetType().Name;
                if (typeName.IndexOf("Saveable", StringComparison.OrdinalIgnoreCase) < 0 &&
                    typeName.IndexOf("Reference", StringComparison.OrdinalIgnoreCase) < 0 &&
                    typeName.IndexOf("Structure", StringComparison.OrdinalIgnoreCase) < 0 &&
                    typeName.IndexOf("Construction", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                object value = SafeReflection.GetKnownMemberValue(c, "ReferenceId");
                if (value == null) value = SafeReflection.GetKnownMemberValue(c, "ReferenceID");
                if (value != null) return FormatIdentity(value);

                object saveable = SafeReflection.GetKnownMemberValue(c, "SaveableReference");
                if (saveable != null)
                {
                    value = SafeReflection.GetKnownMemberValue(saveable, "ReferenceId");
                    if (value == null) value = SafeReflection.GetKnownMemberValue(saveable, "ReferenceID");
                    if (value != null) return FormatIdentity(value);
                }
            }
            return "<unknown>";
        }

        private static string FormatIdentity(object value)
        {
            if (value == null) return "<null>";
            if (value is string) return (string)value;
            if (value is Guid) return ((Guid)value).ToString("D");
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal)
            {
                try { return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture); }
                catch { }
            }

            FieldInfo[] fields;
            try { fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
            catch { fields = new FieldInfo[0]; }
            List<string> parts = new List<string>();
            int i;
            for (i = 0; i < fields.Length && parts.Count < 8; i++)
            {
                FieldInfo field = fields[i];
                Type ft = field.FieldType;
                if (!(ft.IsPrimitive || ft.IsEnum || ft == typeof(string) || ft == typeof(Guid))) continue;
                object fv;
                try { fv = field.GetValue(value); } catch { continue; }
                string text;
                if (fv is Guid) text = ((Guid)fv).ToString("D");
                else
                {
                    try { text = Convert.ToString(fv, System.Globalization.CultureInfo.InvariantCulture); }
                    catch { continue; }
                }
                parts.Add(field.Name + "=" + text);
            }
            if (parts.Count > 0) return (type.FullName ?? type.Name) + "{" + string.Join(",", parts.ToArray()) + "}";
            return "<" + (type.FullName ?? type.Name) + ">";
        }
    }
}
