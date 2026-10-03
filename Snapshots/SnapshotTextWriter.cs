using System.Globalization;
using System.Text;

namespace StrandedDeepDiagnostics.Snapshots
{
    internal static class SnapshotTextWriter
    {
        public static string Render(DiagnosticSnapshot snapshot, bool deep)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("STRANDED DEEP DIAGNOSTICS");
            sb.AppendLine("Snapshot schema: " + snapshot.SchemaVersion);
            sb.AppendLine("Mode: " + (deep ? "DEEP" : "NORMAL"));
            sb.AppendLine("Timestamp UTC: " + snapshot.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
            sb.AppendLine("Frame: " + snapshot.Frame.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("Phase: " + snapshot.Phase);
            sb.AppendLine("ProcessEpoch: " + snapshot.ProcessEpoch.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("WorldEpoch: " + snapshot.WorldEpoch.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("SceneEpoch: " + snapshot.SceneEpoch.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("Module: " + snapshot.ModuleId);
            sb.AppendLine();

            WritePlayer(sb, snapshot.Player);
            WriteRaycastCandidates(sb, snapshot.RaycastCandidates, snapshot.RaycastTruncated);
            WriteObject(sb, "RAW PHYSICAL HIT #0", snapshot.RawTarget);
            WriteObject(sb, "SELECTED PHYSICAL TARGET", snapshot.SelectedTarget);
            WriteObject(sb, "PRIMARY SEMANTIC TARGET", snapshot.Target);
            WriteRelated(sb, snapshot);
            WritePhysics(sb, snapshot.Physics);

            sb.AppendLine();
            sb.AppendLine("=== CAPABILITY SUMMARY ===");
            if (snapshot.CapabilityLines != null)
            {
                int i;
                for (i = 0; i < snapshot.CapabilityLines.Count; i++)
                {
                    sb.AppendLine(snapshot.CapabilityLines[i]);
                }
            }

            return sb.ToString();
        }

        public static string RenderDiff(DiagnosticSnapshot before, DiagnosticSnapshot after, SnapshotDiffResult diff)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("STRANDED DEEP DIAGNOSTICS - SNAPSHOT DIFF");
            sb.AppendLine("Schema: " + after.SchemaVersion);
            sb.AppendLine("Before UTC: " + before.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
            sb.AppendLine("After UTC:  " + after.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
            sb.AppendLine("Before frame: " + before.Frame.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("After frame:  " + after.Frame.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("Changes: " + diff.ChangeCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine();

            int i;
            for (i = 0; i < diff.Lines.Count; i++)
            {
                sb.AppendLine(diff.Lines[i]);
            }
            return sb.ToString();
        }

        private static void WritePlayer(StringBuilder sb, PlayerSnapshot player)
        {
            sb.AppendLine("=== PLAYER ===");
            if (player == null)
            {
                sb.AppendLine("<none>");
            }
            else
            {
                sb.AppendLine("Display: " + player.DisplayName);
                sb.AppendLine("Type: " + player.PlayerType);
                sb.AppendLine("Id: " + player.PlayerId);
                sb.AppendLine("Camera: " + player.CameraPath);
                sb.AppendLine("CameraRect: " + player.CameraRect);
                sb.AppendLine("CameraPosition: " + player.CameraPosition);
                sb.AppendLine("CameraRotation: " + player.CameraRotation);
                sb.AppendLine("CameraFOV: " + player.CameraFov);
            }
            sb.AppendLine();
        }

        private static void WriteRaycastCandidates(StringBuilder sb, System.Collections.Generic.List<RaycastCandidateSnapshot> candidates, bool truncated)
        {
            sb.AppendLine("=== RAYCAST CANDIDATES (DISTANCE SORTED) ===");
            sb.AppendLine("BufferTruncated: " + truncated);
            if (candidates == null || candidates.Count == 0)
            {
                sb.AppendLine("<none>");
                sb.AppendLine();
                return;
            }

            int i;
            for (i = 0; i < candidates.Count; i++)
            {
                RaycastCandidateSnapshot item = candidates[i];
                string selected = item.IsSelected ? " SELECTED" : string.Empty;
                sb.AppendLine("[" + item.Index.ToString(CultureInfo.InvariantCulture) + "] " + item.Classification + selected + " | " + item.Name + " | " + item.Distance.ToString("0.###", CultureInfo.InvariantCulture) + "m");
                sb.AppendLine("    InstanceID=" + item.InstanceId.ToString(CultureInfo.InvariantCulture) + " layer=" + item.Layer.ToString(CultureInfo.InvariantCulture) + " self=" + item.IsSelf + " trigger=" + item.IsTrigger + " connector=" + item.IsConnector);
                sb.AppendLine("    Collider: " + item.ColliderType);
                sb.AppendLine("    Path: " + item.Path);
            }
            sb.AppendLine();
        }

        private static void WriteObject(StringBuilder sb, string title, ObjectSnapshot target)
        {
            sb.AppendLine("=== " + title + " ===");
            if (target == null)
            {
                sb.AppendLine("<none>");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("Name: " + target.Name);
            sb.AppendLine("RuntimeType: " + target.RuntimeType);
            sb.AppendLine("InstanceID: " + target.InstanceId.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("Scene: " + target.SceneName);
            sb.AppendLine("ActiveSelf: " + target.ActiveSelf);
            sb.AppendLine("ActiveInHierarchy: " + target.ActiveInHierarchy);
            sb.AppendLine("Layer: " + target.Layer.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("Tag: " + target.Tag);
            sb.AppendLine("Hierarchy: " + target.HierarchyPath);
            sb.AppendLine("Parent: " + target.ParentPath);
            sb.AppendLine("WorldPosition: " + target.WorldPosition);
            sb.AppendLine("WorldRotation: " + target.WorldRotation);
            sb.AppendLine("LocalPosition: " + target.LocalPosition);
            sb.AppendLine("LocalRotation: " + target.LocalRotation);
            sb.AppendLine("LocalScale: " + target.LocalScale);
            sb.AppendLine();

            sb.AppendLine("--- Raycast context ---");
            sb.AppendLine("HasRaycastHit: " + target.HasRaycastHit);
            sb.AppendLine("Collider: " + target.HitColliderType);
            sb.AppendLine("ColliderPath: " + target.HitColliderPath);
            sb.AppendLine("HitPoint: " + target.HitPoint);
            sb.AppendLine("HitNormal: " + target.HitNormal);
            sb.AppendLine("HitDistance: " + target.HitDistance.ToString("0.###", CultureInfo.InvariantCulture));
            sb.AppendLine();

            sb.AppendLine("--- Components / safe fields ---");
            if (target.ComponentLines != null)
            {
                int i;
                for (i = 0; i < target.ComponentLines.Count; i++)
                {
                    sb.AppendLine(target.ComponentLines[i]);
                }
            }
            sb.AppendLine();
        }

        private static void WriteRelated(StringBuilder sb, DiagnosticSnapshot snapshot)
        {
            sb.AppendLine("=== RELATED SEMANTIC TARGETS ===");
            if (snapshot.RelatedTargets == null || snapshot.RelatedTargets.Count == 0)
            {
                sb.AppendLine("<none>");
            }
            else
            {
                int i;
                for (i = 0; i < snapshot.RelatedTargets.Count; i++)
                {
                    RelatedTargetSnapshot item = snapshot.RelatedTargets[i];
                    sb.AppendLine("[" + i + "] " + item.Role + " | " + item.Name + " | InstanceID=" + item.InstanceId.ToString(CultureInfo.InvariantCulture));
                    sb.AppendLine("    Path: " + item.Path);
                    sb.AppendLine("    Matched: " + (string.IsNullOrEmpty(item.MatchedComponentTypes) ? "<none>" : item.MatchedComponentTypes));
                }
            }
            sb.AppendLine();
        }

        private static void WritePhysics(StringBuilder sb, PhysicsSnapshot physics)
        {
            sb.AppendLine("=== PHYSICS ===");
            if (physics == null)
            {
                sb.AppendLine("<none>");
                return;
            }

            sb.AppendLine("Rigidbodies: " + (physics.Rigidbodies == null ? 0 : physics.Rigidbodies.Count));
            if (physics.Rigidbodies != null)
            {
                int i;
                for (i = 0; i < physics.Rigidbodies.Count; i++)
                {
                    RigidbodySnapshot rb = physics.Rigidbodies[i];
                    sb.AppendLine("RB[" + i + "] Source=" + rb.Source + " InstanceID=" + rb.InstanceId.ToString(CultureInfo.InvariantCulture));
                    sb.AppendLine("  Path: " + rb.Path);
                    sb.AppendLine("  Position: " + rb.Position);
                    sb.AppendLine("  Rotation: " + rb.Rotation);
                    sb.AppendLine("  COM: " + rb.CenterOfMass + " world=" + rb.WorldCenterOfMass);
                    sb.AppendLine("  Mass: " + rb.Mass.ToString("0.###", CultureInfo.InvariantCulture));
                    sb.AppendLine("  isKinematic=" + rb.IsKinematic + " useGravity=" + rb.UseGravity + " constraints=" + rb.Constraints + " sleeping=" + rb.IsSleeping);
                    sb.AppendLine("  Velocity: " + rb.Velocity);
                    sb.AppendLine("  AngularVelocity: " + rb.AngularVelocity);
                }
            }

            sb.AppendLine("Colliders: " + (physics.Colliders == null ? 0 : physics.Colliders.Count));
            if (physics.Colliders != null)
            {
                int i;
                for (i = 0; i < physics.Colliders.Count; i++)
                {
                    ColliderSnapshot c = physics.Colliders[i];
                    sb.AppendLine("COL[" + i + "] Source=" + c.Source + " InstanceID=" + c.InstanceId.ToString(CultureInfo.InvariantCulture) + " Type=" + c.Type);
                    sb.AppendLine("  Path: " + c.Path);
                    sb.AppendLine("  enabled=" + c.Enabled + " trigger=" + c.IsTrigger + " layer=" + c.Layer.ToString(CultureInfo.InvariantCulture));
                    sb.AppendLine("  BoundsCenter: " + c.BoundsCenter);
                    sb.AppendLine("  BoundsSize: " + c.BoundsSize);
                    sb.AppendLine("  AttachedRigidbody: " + c.AttachedRigidbodyPath);
                }
            }
        }
    }
}
