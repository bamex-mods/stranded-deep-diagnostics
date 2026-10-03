using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using StrandedDeepDiagnostics.Inspection;
using StrandedDeepDiagnostics.Reflection;
using StrandedDeepDiagnostics.Targets;
using StrandedDeepDiagnostics.Raft;

namespace StrandedDeepDiagnostics.Adapters.BamEx
{
    internal sealed class RaftFurnitureAdapter
    {
        private static readonly string[] AdapterTokens = new string[]
        {
            "RaftSmallStructureFollower", "RaftReposition", "RaftFurniture", "AttachmentDiagnostic", "AttachmentRegistry"
        };

        private static readonly string[] FieldTokens = new string[]
        {
            "raft", "attach", "local", "position", "rotation", "state", "drag", "collision", "reference", "parent"
        };

        public string DescribeTargetShort(DiagnosticTarget target)
        {
            if (target == null || !target.IsAlive) return "target=<none>";
            List<Component> components = FindAdapterComponents(target);
            if (components.Count == 0) return "adapter=<none on target chain>";
            return "adapter=" + components[0].GetType().Name + " candidates=" + components.Count;
        }

        public string RenderTargetReport(DiagnosticTarget target)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== RAFT FURNITURE OPTIONAL ADAPTER ===");
            sb.AppendLine("One-way read-only inspection. Production Raft Furniture does not reference Diagnostics.");
            if (target == null || !target.IsAlive)
            {
                sb.AppendLine("Target: <none>");
                return sb.ToString();
            }

            GameObject go = target.InspectionGameObject;
            sb.AppendLine("Target: " + SafeName(go));
            try { sb.AppendLine("Target path: " + SafeReflection.GetHierarchyPath(go.transform, 48)); } catch { }

            List<Component> components = FindAdapterComponents(target);
            sb.AppendLine("Adapter components on target chain: " + components.Count);
            int i;
            for (i = 0; i < components.Count; i++)
            {
                Component c = components[i];
                sb.AppendLine();
                sb.AppendLine("COMPONENT " + c.GetType().FullName);
                sb.AppendLine("Path: " + InspectionUtil.SafePath(c));
                sb.Append(InspectionUtil.ReadMatchingFields(c, FieldTokens, 48, 12));
                sb.Append(InspectionUtil.ReadUnityReferenceFields(c, 32));
            }

            GameObject owningRaft = ResolveOwningRaft(target, components);
            sb.AppendLine();
            sb.AppendLine("Owning raft candidate: " + (owningRaft == null ? "<unresolved>" : SafeName(owningRaft)));
            if (owningRaft != null)
            {
                try { sb.AppendLine("Owning raft path: " + SafeReflection.GetHierarchyPath(owningRaft.transform, 48)); } catch { }
                AppendCollisionIgnoreSummary(sb, go, owningRaft);
            }
            else
            {
                sb.AppendLine("Own-raft collision matrix: <not evaluated without owning raft>");
            }
            return sb.ToString();
        }

        private static List<Component> FindAdapterComponents(DiagnosticTarget target)
        {
            List<Component> all = InspectionUtil.GetComponentsOnAncestorChain(target, 20);
            List<Component> result = new List<Component>();
            int i;
            for (i = 0; i < all.Count; i++)
            {
                Component c = all[i];
                if (c != null && InspectionUtil.TypeMatches(c.GetType(), AdapterTokens)) result.Add(c);
            }
            return result;
        }

        private static GameObject ResolveOwningRaft(DiagnosticTarget target, List<Component> adapterComponents)
        {
            int i;
            for (i = 0; i < adapterComponents.Count; i++)
            {
                GameObject fromField = FindRaftReference(adapterComponents[i]);
                if (fromField != null) return RaftDiscovery.ResolveRaftRoot(fromField);
            }

            GameObject current = target == null ? null : target.InspectionGameObject;
            if (current == null) return null;
            Transform t = current.transform;
            int depth = 0;
            while (t != null && depth < 20)
            {
                if (RaftDiscovery.IsRaftLike(t.gameObject)) return RaftDiscovery.ResolveRaftRoot(t.gameObject);
                t = t.parent;
                depth++;
            }
            return null;
        }

        private static GameObject FindRaftReference(Component component)
        {
            if (component == null) return null;
            Type current = component.GetType();
            while (current != null)
            {
                FieldInfo[] fields;
                try { fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { fields = new FieldInfo[0]; }
                int i;
                for (i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (field.Name.IndexOf("raft", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    object value;
                    try { value = field.GetValue(component); } catch { continue; }
                    GameObject go = ToGameObject(value);
                    if (go != null) return go;
                }
                current = current.BaseType;
            }

            string[] names = new string[] { "Raft", "raft", "_raft", "OwningRaft", "OwnerRaft" };
            int j;
            for (j = 0; j < names.Length; j++)
            {
                object value = SafeReflection.GetKnownMemberValue(component, names[j]);
                GameObject go = ToGameObject(value);
                if (go != null) return go;
            }
            return null;
        }

        private static GameObject ToGameObject(object value)
        {
            if (value == null) return null;
            GameObject go = value as GameObject;
            if (go != null) return go;
            Component component = value as Component;
            if (component != null) return component.gameObject;
            Transform transform = value as Transform;
            if (transform != null) return transform.gameObject;
            return null;
        }

        private static void AppendCollisionIgnoreSummary(StringBuilder sb, GameObject target, GameObject raft)
        {
            Collider[] objectColliders;
            Collider[] raftColliders;
            try { objectColliders = target.GetComponentsInChildren<Collider>(true); }
            catch { objectColliders = new Collider[0]; }
            try { raftColliders = raft.GetComponentsInChildren<Collider>(true); }
            catch { raftColliders = new Collider[0]; }

            int totalPossible = objectColliders.Length * raftColliders.Length;
            int checkedPairs = 0;
            int ignored = 0;
            int active = 0;
            const int MaxPairs = 512;
            int i;
            int j;
            for (i = 0; i < objectColliders.Length && checkedPairs < MaxPairs; i++)
            {
                Collider a = objectColliders[i];
                if (a == null) continue;
                for (j = 0; j < raftColliders.Length && checkedPairs < MaxPairs; j++)
                {
                    Collider b = raftColliders[j];
                    if (b == null || a == b) continue;
                    bool isIgnored = false;
                    try { isIgnored = UnityEngine.Physics.GetIgnoreCollision(a, b); } catch { }
                    checkedPairs++;
                    if (isIgnored) ignored++; else active++;
                }
            }

            sb.AppendLine("Own-raft collision matrix:");
            sb.AppendLine("  objectColliders=" + objectColliders.Length + " raftColliders=" + raftColliders.Length);
            sb.AppendLine("  possiblePairs=" + totalPossible + " checkedPairs=" + checkedPairs + (checkedPairs < totalPossible ? " (bounded)" : string.Empty));
            sb.AppendLine("  ignored=" + ignored + " notIgnored=" + active);
        }

        private static string SafeName(GameObject go)
        {
            if (go == null) return "<null>";
            try { return go.name; } catch { return "<unavailable>"; }
        }
    }
}
