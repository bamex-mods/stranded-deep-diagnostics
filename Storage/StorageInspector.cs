using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using StrandedDeepDiagnostics.Inspection;
using StrandedDeepDiagnostics.Targets;

namespace StrandedDeepDiagnostics.Storage
{
    internal sealed class StorageInspector
    {
        private static readonly string[] Tokens = new string[] { "SlotStorage", "Interactive_STORAGE", "Storage", "Container" };
        private static readonly string[] Members = new string[] { "Slots", "SlotCount", "Capacity", "Storage", "Stored", "Items", "StorageData", "IsOpen", "Owner", "CraftingType" };

        public string[] DescribeOverlay(DiagnosticTarget target)
        {
            List<string> lines = new List<string>();
            List<Component> components = InspectionUtil.FindComponentsByTokens(target, Tokens, 16);
            lines.Add("STORAGE candidates=" + components.Count);
            int i;
            for (i = 0; i < components.Count && i < 5; i++) lines.Add("  " + components[i].GetType().FullName);
            lines.Add("Lazy restore rule: runtime empty != serialized state absent");
            lines.Add("F12 compares runtime storage-like members with pending *storageData fields");
            return lines.ToArray();
        }

        public string RenderReport(DiagnosticTarget target)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== STORAGE INSPECTOR ===");
            sb.AppendLine("Read-only; no Store/GetStored/materialization method is invoked.");
            sb.AppendLine("Interpret pending serialized fields separately from materialized runtime collections.");
            List<Component> components = InspectionUtil.FindComponentsByTokens(target, Tokens, 20);
            sb.AppendLine("Candidates: " + components.Count);
            int i;
            for (i = 0; i < components.Count; i++)
            {
                Component c = components[i];
                sb.AppendLine();
                sb.AppendLine("COMPONENT " + c.GetType().FullName);
                sb.AppendLine("Path: " + InspectionUtil.SafePath(c));
                sb.Append(InspectionUtil.ReadKnownMembers(c, Members, 12));
                sb.AppendLine("  -- storage/slot fields --");
                sb.Append(InspectionUtil.ReadMatchingFields(c, new string[] { "storage", "stored", "slot", "item", "data", "content" }, 48, 12));
                sb.Append(InspectionUtil.ReadUnityReferenceFields(c, 20));
            }
            return sb.ToString();
        }
    }
}
