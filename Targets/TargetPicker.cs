using System;
using UnityEngine;
using StrandedDeepDiagnostics.Players;

namespace StrandedDeepDiagnostics.Targets
{
    internal sealed class TargetPicker
    {
        private const int HitBufferSize = 64;
        private const int MaxRecordedHits = HitBufferSize;
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[HitBufferSize];

        public DiagnosticTarget Pick(PlayerContext player, float maxDistance)
        {
            if (player == null || player.Camera == null)
            {
                return null;
            }

            Camera camera = player.Camera;
            if (camera == null)
            {
                return null;
            }

            Ray ray;
            try
            {
                ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            }
            catch
            {
                return null;
            }

            int hitCount;
            try
            {
                hitCount = UnityEngine.Physics.RaycastNonAlloc(
                    ray,
                    _hitBuffer,
                    maxDistance,
                    ~0,
                    QueryTriggerInteraction.Collide);
            }
            catch
            {
                return null;
            }

            if (hitCount <= 0)
            {
                return null;
            }

            SortHitsByDistance(_hitBuffer, hitCount);

            Transform playerRoot = ResolvePlayerTransform(player);
            DiagnosticTarget target = new DiagnosticTarget();
            target.RaycastTruncated = hitCount >= _hitBuffer.Length;

            RaycastCandidate selected = null;
            RaycastCandidate firstNonSelf = null;
            RaycastCandidate firstAny = null;

            int i;
            for (i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (hit.collider == null)
                {
                    continue;
                }

                RaycastCandidate candidate = BuildCandidate(i, hit, playerRoot);
                if (candidate == null)
                {
                    continue;
                }

                if (target.RaycastCandidates.Count < MaxRecordedHits)
                {
                    target.RaycastCandidates.Add(candidate);
                }

                if (firstAny == null)
                {
                    firstAny = candidate;
                    target.RawGameObject = candidate.GameObject;
                    target.RawHitCollider = candidate.Collider;
                    target.RawHitPoint = candidate.Point;
                    target.RawHitNormal = candidate.Normal;
                    target.RawHitDistance = candidate.Distance;
                    target.RawLayer = candidate.Layer;
                }

                if (!candidate.IsSelf && firstNonSelf == null)
                {
                    firstNonSelf = candidate;
                }

                // Do not skip generic triggers: many real Stranded Deep interactions use them.
                // Only the active player's own hierarchy and explicit connector helper geometry
                // are filtered out when choosing the physical target to inspect.
                if (!candidate.IsSelf && !candidate.IsConnector && selected == null)
                {
                    selected = candidate;
                }
            }

            if (selected == null)
            {
                selected = firstNonSelf ?? firstAny;
            }

            if (selected == null || selected.GameObject == null)
            {
                return null;
            }

            selected.IsSelected = true;
            MarkRecordedCandidateSelected(target, selected);

            target.GameObject = selected.GameObject;
            target.HitCollider = selected.Collider;
            target.HasRaycastHit = true;
            target.HitPoint = selected.Point;
            target.HitNormal = selected.Normal;
            target.HitDistance = selected.Distance;
            target.Layer = selected.Layer;
            return target;
        }

        private static void SortHitsByDistance(RaycastHit[] hits, int count)
        {
            // Insertion sort avoids allocating a comparer/delegate every frame and is cheap for
            // the small center-ray hit sets expected here.
            int i;
            for (i = 1; i < count; i++)
            {
                RaycastHit value = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > value.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }
                hits[j + 1] = value;
            }
        }

        private static void MarkRecordedCandidateSelected(DiagnosticTarget target, RaycastCandidate selected)
        {
            int i;
            for (i = 0; i < target.RaycastCandidates.Count; i++)
            {
                RaycastCandidate candidate = target.RaycastCandidates[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.Collider == selected.Collider && candidate.Index == selected.Index)
                {
                    candidate.IsSelected = true;
                    return;
                }
            }
        }

        private static Transform ResolvePlayerTransform(PlayerContext player)
        {
            if (player == null || player.PlayerObject == null)
            {
                return null;
            }

            Component component = player.PlayerObject as Component;
            if (component == null)
            {
                return null;
            }

            try { return component.transform; }
            catch { return null; }
        }

        private static RaycastCandidate BuildCandidate(int index, RaycastHit hit, Transform playerRoot)
        {
            Collider collider = hit.collider;
            if (collider == null)
            {
                return null;
            }

            GameObject gameObject;
            try { gameObject = collider.gameObject; }
            catch { return null; }

            if (gameObject == null)
            {
                return null;
            }

            RaycastCandidate candidate = new RaycastCandidate();
            candidate.Index = index;
            candidate.GameObject = gameObject;
            candidate.Collider = collider;
            candidate.Point = hit.point;
            candidate.Normal = hit.normal;
            candidate.Distance = hit.distance;
            try { candidate.Layer = gameObject.layer; }
            catch { candidate.Layer = -1; }

            candidate.IsSelf = IsPlayerHierarchy(gameObject.transform, playerRoot);
            try { candidate.IsTrigger = collider.isTrigger; }
            catch { candidate.IsTrigger = false; }
            candidate.IsConnector = IsConnectorHelper(gameObject);
            candidate.Classification = BuildClassification(candidate);
            return candidate;
        }

        private static bool IsPlayerHierarchy(Transform hitTransform, Transform playerRoot)
        {
            if (hitTransform == null || playerRoot == null)
            {
                return false;
            }

            try
            {
                if (hitTransform == playerRoot)
                {
                    return true;
                }

                return hitTransform.IsChildOf(playerRoot);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsConnectorHelper(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            string name = string.Empty;
            try { name = gameObject.name ?? string.Empty; }
            catch { }

            if (name.IndexOf("CONNECTOR", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            Component[] components;
            try { components = gameObject.GetComponents<Component>(); }
            catch { return false; }

            int i;
            for (i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                string typeName;
                try
                {
                    Type type = component.GetType();
                    typeName = type.FullName ?? type.Name;
                }
                catch
                {
                    continue;
                }

                if (typeName.IndexOf("Connector", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildClassification(RaycastCandidate candidate)
        {
            if (candidate == null)
            {
                return "UNKNOWN";
            }

            if (candidate.IsSelf)
            {
                if (candidate.IsTrigger) return "SELF+TRIGGER";
                return "SELF";
            }

            if (candidate.IsConnector)
            {
                if (candidate.IsTrigger) return "CONNECTOR+TRIGGER";
                return "CONNECTOR";
            }

            if (candidate.IsTrigger)
            {
                return "TRIGGER";
            }

            return "NORMAL";
        }
    }
}
