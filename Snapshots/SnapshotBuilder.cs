using System;
using System.Collections.Generic;
using UnityEngine;
using StrandedDeepDiagnostics.Core;
using StrandedDeepDiagnostics.Physics;
using StrandedDeepDiagnostics.Players;
using StrandedDeepDiagnostics.Reflection;
using StrandedDeepDiagnostics.Targets;

namespace StrandedDeepDiagnostics.Snapshots
{
    internal static class SnapshotBuilder
    {
        public static DiagnosticSnapshot Build(
            EpochManager epochs,
            CapabilityManifest capabilities,
            PlayerContext player,
            DiagnosticTarget target,
            bool deep,
            int maxHierarchyDepth,
            int maxFieldsPerComponent,
            string moduleId)
        {
            DiagnosticSnapshot snapshot = new DiagnosticSnapshot();
            snapshot.SchemaVersion = DiagnosticsConstants.SnapshotSchemaVersion;
            snapshot.TimestampUtc = DateTime.UtcNow;
            snapshot.Frame = Time.frameCount;
            snapshot.Phase = "Manual";
            snapshot.ProcessEpoch = epochs.ProcessEpoch;
            snapshot.WorldEpoch = epochs.WorldEpoch;
            snapshot.SceneEpoch = epochs.SceneEpoch;
            snapshot.ModuleId = moduleId;
            snapshot.Player = BuildPlayerSnapshot(player);
            snapshot.RawTarget = BuildObjectSnapshot(
                target == null ? null : target.RawGameObject,
                target,
                true,
                false,
                maxHierarchyDepth,
                maxFieldsPerComponent);
            snapshot.SelectedTarget = BuildObjectSnapshot(
                target == null ? null : target.GameObject,
                target,
                false,
                false,
                maxHierarchyDepth,
                maxFieldsPerComponent);
            snapshot.Target = BuildObjectSnapshot(
                target == null ? null : target.InspectionGameObject,
                target,
                false,
                deep,
                maxHierarchyDepth,
                maxFieldsPerComponent);
            snapshot.RaycastTruncated = target != null && target.RaycastTruncated;
            snapshot.RaycastCandidates = BuildRaycastCandidates(target, maxHierarchyDepth);
            snapshot.RelatedTargets = BuildRelatedTargets(target, maxHierarchyDepth);
            snapshot.Physics = PhysicsInspector.Capture(target);
            snapshot.CapabilityLines = BuildCapabilityLines(capabilities);
            return snapshot;
        }

        private static PlayerSnapshot BuildPlayerSnapshot(PlayerContext player)
        {
            if (player == null)
            {
                return null;
            }

            PlayerSnapshot snapshot = new PlayerSnapshot();
            snapshot.DisplayIndex = player.DisplayIndex;
            snapshot.DisplayName = player.DisplayName;
            snapshot.PlayerType = player.PlayerTypeName;
            snapshot.PlayerId = player.PlayerId;
            snapshot.CameraPath = player.CameraPath;
            snapshot.CameraRect = player.Camera == null ? "<unresolved>" : ValueFormatter.FormatSimple(player.Camera.rect);

            if (player.Camera != null)
            {
                snapshot.CameraPosition = ValueFormatter.FormatVector3(player.Camera.transform.position);
                snapshot.CameraRotation = ValueFormatter.FormatQuaternionEuler(player.Camera.transform.rotation);
                snapshot.CameraFov = player.Camera.fieldOfView.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                snapshot.CameraPosition = "<unresolved>";
                snapshot.CameraRotation = "<unresolved>";
                snapshot.CameraFov = "<unresolved>";
            }

            return snapshot;
        }

        private static ObjectSnapshot BuildObjectSnapshot(
            GameObject gameObject,
            DiagnosticTarget target,
            bool useRawHit,
            bool deep,
            int maxHierarchyDepth,
            int maxFieldsPerComponent)
        {
            if (gameObject == null)
            {
                return null;
            }

            ObjectSnapshot snapshot = new ObjectSnapshot();
            snapshot.Name = SafeName(gameObject);
            snapshot.RuntimeType = SafeReflection.GetPrimaryComponentTypeName(gameObject);
            snapshot.InstanceId = SafeInstanceId(gameObject);
            snapshot.SceneName = SafeSceneName(gameObject);
            snapshot.ActiveSelf = gameObject.activeSelf;
            snapshot.ActiveInHierarchy = gameObject.activeInHierarchy;
            snapshot.Layer = gameObject.layer;
            snapshot.Tag = SafeTag(gameObject);
            snapshot.HierarchyPath = SafeReflection.GetHierarchyPath(gameObject.transform, maxHierarchyDepth);
            snapshot.ParentPath = gameObject.transform.parent == null
                ? "<root>"
                : SafeReflection.GetHierarchyPath(gameObject.transform.parent, maxHierarchyDepth);

            snapshot.WorldPosition = ValueFormatter.FormatVector3(gameObject.transform.position);
            snapshot.WorldRotation = ValueFormatter.FormatQuaternionEuler(gameObject.transform.rotation);
            snapshot.LocalPosition = ValueFormatter.FormatVector3(gameObject.transform.localPosition);
            snapshot.LocalRotation = ValueFormatter.FormatQuaternionEuler(gameObject.transform.localRotation);
            snapshot.LocalScale = ValueFormatter.FormatVector3(gameObject.transform.localScale);

            snapshot.HasRaycastHit = target != null && target.HasRaycastHit;
            if (snapshot.HasRaycastHit)
            {
                Collider collider = useRawHit ? target.RawHitCollider : target.HitCollider;
                snapshot.HitColliderType = collider == null ? "<destroyed>" : collider.GetType().FullName;
                snapshot.HitColliderPath = collider == null
                    ? "<destroyed>"
                    : SafeReflection.GetHierarchyPath(collider.transform, maxHierarchyDepth);
                snapshot.HitPoint = ValueFormatter.FormatVector3(useRawHit ? target.RawHitPoint : target.HitPoint);
                snapshot.HitNormal = ValueFormatter.FormatVector3(useRawHit ? target.RawHitNormal : target.HitNormal);
                snapshot.HitDistance = useRawHit ? target.RawHitDistance : target.HitDistance;
            }
            else
            {
                snapshot.HitColliderType = "<not captured>";
                snapshot.HitColliderPath = "<not captured>";
                snapshot.HitPoint = "<not captured>";
                snapshot.HitNormal = "<not captured>";
                snapshot.HitDistance = 0f;
            }

            snapshot.ComponentLines = SafeReflection.DescribeComponents(gameObject, deep, maxFieldsPerComponent);
            return snapshot;
        }

        private static List<RaycastCandidateSnapshot> BuildRaycastCandidates(DiagnosticTarget target, int maxHierarchyDepth)
        {
            List<RaycastCandidateSnapshot> snapshots = new List<RaycastCandidateSnapshot>();
            if (target == null || target.RaycastCandidates == null)
            {
                return snapshots;
            }

            int i;
            for (i = 0; i < target.RaycastCandidates.Count; i++)
            {
                RaycastCandidate candidate = target.RaycastCandidates[i];
                if (candidate == null || candidate.GameObject == null)
                {
                    continue;
                }

                RaycastCandidateSnapshot item = new RaycastCandidateSnapshot();
                item.Index = candidate.Index;
                item.Name = SafeName(candidate.GameObject);
                item.InstanceId = SafeInstanceId(candidate.GameObject);
                item.Path = SafeReflection.GetHierarchyPath(candidate.GameObject.transform, maxHierarchyDepth);
                try { item.ColliderType = candidate.Collider == null ? "<destroyed>" : candidate.Collider.GetType().FullName; }
                catch { item.ColliderType = "<unavailable>"; }
                item.Distance = candidate.Distance;
                item.Layer = candidate.Layer;
                item.IsSelf = candidate.IsSelf;
                item.IsTrigger = candidate.IsTrigger;
                item.IsConnector = candidate.IsConnector;
                item.IsSelected = candidate.IsSelected;
                item.Classification = candidate.Classification;
                snapshots.Add(item);
            }

            return snapshots;
        }

        private static List<RelatedTargetSnapshot> BuildRelatedTargets(DiagnosticTarget target, int maxHierarchyDepth)
        {
            List<RelatedTargetSnapshot> snapshots = new List<RelatedTargetSnapshot>();
            if (target == null || target.RelatedTargets == null)
            {
                return snapshots;
            }

            int i;
            for (i = 0; i < target.RelatedTargets.Count; i++)
            {
                RelatedTarget related = target.RelatedTargets[i];
                if (related == null || related.GameObject == null)
                {
                    continue;
                }

                RelatedTargetSnapshot item = new RelatedTargetSnapshot();
                item.Role = related.Role;
                item.Name = SafeName(related.GameObject);
                item.InstanceId = SafeInstanceId(related.GameObject);
                item.Path = SafeReflection.GetHierarchyPath(related.GameObject.transform, maxHierarchyDepth);
                item.MatchedComponentTypes = related.MatchedComponentTypes;
                snapshots.Add(item);
            }
            return snapshots;
        }

        private static List<string> BuildCapabilityLines(CapabilityManifest capabilities)
        {
            List<string> lines = new List<string>();
            if (capabilities == null)
            {
                return lines;
            }

            lines.Add("StrandedDeepApplicationVersion=" + capabilities.GameVersion);
            lines.Add("UnityPlayerExecutableFileVersion=" + capabilities.GameExecutableFileVersion);
            lines.Add("UnityPlayerExecutableProductVersion=" + capabilities.GameExecutableProductVersion);
            lines.Add("AssemblyCSharpFileVersion=" + capabilities.AssemblyCSharpFileVersion);
            lines.Add("AssemblyCSharpProductVersion=" + capabilities.AssemblyCSharpProductVersion);
            lines.Add("UnityVersion=" + capabilities.UnityVersion);
            lines.Add("AssemblyCSharpSha256=" + capabilities.AssemblyCSharpSha256);
            lines.Add("AssemblyCSharpMvid=" + capabilities.AssemblyCSharpMvid);

            int i;
            for (i = 0; i < capabilities.Records.Count; i++)
            {
                CapabilityRecord record = capabilities.Records[i];
                lines.Add(record.Name + "=" + record.Status + "|" + record.Detail);
            }

            return lines;
        }

        private static string SafeName(GameObject gameObject)
        {
            try { return gameObject.name; }
            catch { return "<unavailable>"; }
        }

        private static int SafeInstanceId(UnityEngine.Object obj)
        {
            try { return obj.GetInstanceID(); }
            catch { return 0; }
        }

        private static string SafeSceneName(GameObject gameObject)
        {
            try { return gameObject.scene.IsValid() ? gameObject.scene.name : "<invalid>"; }
            catch { return "<unavailable>"; }
        }

        private static string SafeTag(GameObject gameObject)
        {
            try { return gameObject.tag; }
            catch { return "<unavailable>"; }
        }
    }
}
