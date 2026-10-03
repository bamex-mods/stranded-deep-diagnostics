using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using StrandedDeepDiagnostics.Inspection;
using StrandedDeepDiagnostics.Targets;

namespace StrandedDeepDiagnostics.Saveables
{
    internal sealed class SaveableInspector
    {
        private static readonly string[] Tokens = new string[] { "Saveable", "Reference", "InteractiveObject", "BoltEntity" };
        private static readonly string[] Members = new string[] { "ReferenceId", "ReferenceID", "MiniGuid", "MiniGUID", "PrefabId", "PrefabID", "CraftingType", "SaveableReference", "IsAttached", "Attached", "NetworkId", "NetworkID", "IsOwner" };

        public string[] DescribeOverlay(DiagnosticTarget target)
        {
            List<string> lines = new List<string>();
            List<Component> components = InspectionUtil.FindComponentsByTokens(target, Tokens, 16);
            lines.Add("SAVEABLE / NATIVE ID candidates=" + components.Count);
            int i;
            for (i = 0; i < components.Count && i < 5; i++)
                lines.Add("  " + components[i].GetType().FullName);
            lines.Add("Identity domains remain separate: runtime / persistent / network");
            lines.Add("F12 exports ReferenceId/MiniGuid/PrefabId/CraftingType/Bolt metadata when present");
            return lines.ToArray();
        }

        public string RenderReport(DiagnosticTarget target)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== SAVEABLE / NATIVE ITEM INSPECTOR ===");
            sb.AppendLine("Runtime InstanceID is never treated as persistent identity.");
            sb.AppendLine("Known identity members are read only; Save()/Load()/Instantiate() are not called.");
            List<Component> components = InspectionUtil.FindComponentsByTokens(target, Tokens, 20);
            sb.AppendLine("Candidates: " + components.Count);
            int i;
            for (i = 0; i < components.Count; i++)
            {
                Component c = components[i];
                sb.AppendLine();
                sb.AppendLine("COMPONENT " + c.GetType().FullName);
                sb.AppendLine("Path: " + InspectionUtil.SafePath(c));
                try { sb.AppendLine("Runtime InstanceID: " + c.GetInstanceID()); } catch { }
                sb.Append(InspectionUtil.ReadKnownMembers(c, Members, 8));
                sb.Append(InspectionUtil.ReadMatchingFields(c, new string[] { "reference", "guid", "prefab", "crafting", "network", "bolt", "owner" }, 32, 8));
                sb.Append(InspectionUtil.ReadUnityReferenceFields(c, 20));
            }
            return sb.ToString();
        }
    }
}
