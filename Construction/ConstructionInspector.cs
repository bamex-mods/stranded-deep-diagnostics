using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using StrandedDeepDiagnostics.Inspection;
using StrandedDeepDiagnostics.Reflection;
using StrandedDeepDiagnostics.Targets;

namespace StrandedDeepDiagnostics.Construction
{
    internal sealed class ConstructionInspector
    {
        private static readonly string[] Tokens = new string[] { "Construction", "Constructing", "Ghost", "Connector", "Crafter", "Raft", "Structure" };
        private static readonly string[] Members = new string[] { "Ghost", "Connector", "Connectors", "Colliding", "IsColliding", "CanPlace", "Valid", "IsValid", "Parent", "Structure", "Raft", "CraftingType" };

        public string[] DescribeOverlay(DiagnosticTarget target)
        {
            List<string> lines = new List<string>();
            List<Component> components = InspectionUtil.FindComponentsByTokens(target, Tokens, 20);
            lines.Add("CONSTRUCTION / RAFT candidates=" + components.Count);
            int i;
            for (i = 0; i < components.Count && i < 6; i++) lines.Add("  " + components[i].GetType().FullName);
            lines.Add("Hierarchy != physical ownership; references are reported separately");
            lines.Add("F12 exports transform chain + connector/ghost/raft references");
            return lines.ToArray();
        }

        public string RenderReport(DiagnosticTarget target)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== CONSTRUCTION / RAFT INSPECTOR ===");
            sb.AppendLine("Read-only. No placement validation, connector mutation or transform write is performed.");
            if (target != null && target.InspectionGameObject != null)
            {
                GameObject go = target.InspectionGameObject;
                sb.AppendLine("Primary: " + go.name);
                sb.AppendLine("Path: " + SafeReflection.GetHierarchyPath(go.transform, 64));
                sb.AppendLine("World pose: pos=" + ValueFormatter.FormatVector3(go.transform.position) + " rot=" + ValueFormatter.FormatQuaternionEuler(go.transform.rotation));
                sb.AppendLine("Local pose: pos=" + ValueFormatter.FormatVector3(go.transform.localPosition) + " rot=" + ValueFormatter.FormatQuaternionEuler(go.transform.localRotation));
            }

            List<Component> components = InspectionUtil.FindComponentsByTokens(target, Tokens, 24);
            sb.AppendLine("Candidates: " + components.Count);
            int i;
            for (i = 0; i < components.Count; i++)
            {
                Component c = components[i];
                sb.AppendLine();
                sb.AppendLine("COMPONENT " + c.GetType().FullName);
                sb.AppendLine("Path: " + InspectionUtil.SafePath(c));
                sb.Append(InspectionUtil.ReadKnownMembers(c, Members, 12));
                sb.Append(InspectionUtil.ReadMatchingFields(c, new string[] { "ghost", "connector", "collid", "place", "raft", "structure", "parent", "craft" }, 48, 12));
                sb.AppendLine("  -- Unity reference graph --");
                sb.Append(InspectionUtil.ReadUnityReferenceFields(c, 32));
            }
            return sb.ToString();
        }
    }
}
