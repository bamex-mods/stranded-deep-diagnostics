using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using StrandedDeepDiagnostics.Inspection;
using StrandedDeepDiagnostics.Reflection;
using StrandedDeepDiagnostics.Targets;

namespace StrandedDeepDiagnostics.Interaction
{
    internal sealed class InteractionInspector
    {
        private static readonly string[] Tokens = new string[] { "InteractiveObject", "IPickupable", "IStorable", "Interact", "Draggable" };
        private static readonly string[] Members = new string[] { "DisplayName", "CraftingType", "CanInteract", "IsPickedUp", "IsHeld", "IsDraggable", "CanDrag", "Owner", "Holder", "Pickupable" };

        public string[] DescribeOverlay(DiagnosticTarget target)
        {
            List<string> lines = new List<string>();
            List<Component> components = InspectionUtil.FindComponentsByTokens(target, Tokens, 12);
            lines.Add("INTERACTION candidates=" + components.Count);
            int i;
            for (i = 0; i < components.Count && i < 6; i++)
                lines.Add("  " + components[i].GetType().FullName);
            if (components.Count == 0) lines.Add("  <no interaction component/interface on ancestor chain>");
            lines.Add("F12 exports allowlisted interaction members + references");
            return lines.ToArray();
        }

        public string RenderReport(DiagnosticTarget target)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== INTERACTION INSPECTOR ===");
            sb.AppendLine("Read-only. No Interact/Pickup/Drop method is invoked by this report.");
            sb.AppendLine();
            List<Component> components = InspectionUtil.FindComponentsByTokens(target, Tokens, 16);
            sb.AppendLine("Candidates: " + components.Count);
            int i;
            for (i = 0; i < components.Count; i++)
            {
                Component c = components[i];
                sb.AppendLine();
                sb.AppendLine("COMPONENT " + c.GetType().FullName);
                sb.AppendLine("Path: " + InspectionUtil.SafePath(c));
                sb.Append(InspectionUtil.ReadKnownMembers(c, Members, 8));
                sb.Append(InspectionUtil.ReadMatchingFields(c, new string[] { "interact", "pickup", "drag", "holder", "crafting", "display" }, 24, 8));
                sb.Append(InspectionUtil.ReadUnityReferenceFields(c, 16));
            }
            return sb.ToString();
        }
    }
}
