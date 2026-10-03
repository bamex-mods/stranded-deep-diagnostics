using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using StrandedDeepDiagnostics.Inspection;
using StrandedDeepDiagnostics.Players;
using StrandedDeepDiagnostics.Targets;

namespace StrandedDeepDiagnostics.Inventory
{
    internal sealed class InventoryInspector
    {
        private static readonly string[] Tokens = new string[] { "Holder", "Inventory", "Hotkey", "SlotStorage", "Crafter" };
        private static readonly string[] Members = new string[] { "HeldObject", "HeldItem", "CurrentItem", "SelectedItem", "Slots", "SlotCount", "Capacity", "Items", "Owner", "CraftingType" };

        public string[] DescribeOverlay(PlayerContext player)
        {
            List<string> lines = new List<string>();
            Component root = player == null ? null : player.PlayerObject as Component;
            List<Component> components = InspectionUtil.FindComponentsInPlayer(root, Tokens, 64);
            lines.Add("HOLDER / INVENTORY candidates=" + components.Count);
            int i;
            for (i = 0; i < components.Count && i < 6; i++) lines.Add("  " + components[i].GetType().FullName);
            lines.Add("Bound to active Beam.Player; never global controller/player state");
            lines.Add("F12 exports allowlisted holder/inventory fields");
            return lines.ToArray();
        }

        public string RenderReport(PlayerContext player, DiagnosticTarget target)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== HOLDER / INVENTORY INSPECTOR ===");
            sb.AppendLine("Player: " + (player == null ? "<unresolved>" : player.DisplayName));
            Component root = player == null ? null : player.PlayerObject as Component;
            List<Component> components = InspectionUtil.FindComponentsInPlayer(root, Tokens, 96);
            sb.AppendLine("Player component candidates: " + components.Count);
            int i;
            for (i = 0; i < components.Count; i++)
            {
                Component c = components[i];
                sb.AppendLine();
                sb.AppendLine("COMPONENT " + c.GetType().FullName);
                sb.AppendLine("Path: " + InspectionUtil.SafePath(c));
                sb.Append(InspectionUtil.ReadKnownMembers(c, Members, 12));
                sb.Append(InspectionUtil.ReadMatchingFields(c, new string[] { "hold", "inventory", "slot", "item", "current", "selected", "hotkey" }, 40, 12));
                sb.Append(InspectionUtil.ReadUnityReferenceFields(c, 20));
            }

            if (target != null)
            {
                sb.AppendLine();
                sb.AppendLine("=== TARGET ITEM CONTEXT ===");
                List<Component> targetComponents = InspectionUtil.GetComponentsOnAncestorChain(target, 10);
                for (i = 0; i < targetComponents.Count && i < 24; i++)
                {
                    Component c = targetComponents[i];
                    string name = c.GetType().FullName;
                    if (name.IndexOf("Interactive", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("Saveable", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("Pickup", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    sb.AppendLine("TARGET COMPONENT " + name);
                    sb.Append(InspectionUtil.ReadKnownMembers(c, new string[] { "DisplayName", "CraftingType", "ReferenceId", "MiniGuid", "PrefabId" }, 8));
                }
            }
            return sb.ToString();
        }
    }
}
