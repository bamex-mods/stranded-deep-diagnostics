using System.Collections.Generic;
using UnityEngine;

namespace StrandedDeepDiagnostics.Targets
{
    internal sealed class RelatedTarget
    {
        public string Role;
        public GameObject GameObject;
        public string MatchedComponentTypes;
    }

    internal sealed class RaycastCandidate
    {
        public int Index;
        public GameObject GameObject;
        public Collider Collider;
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;
        public int Layer;
        public bool IsSelf;
        public bool IsTrigger;
        public bool IsConnector;
        public bool IsSelected;
        public string Classification;
    }

    internal sealed class DiagnosticTarget
    {
        // First physical hit returned by the sorted multi-hit ray.
        public GameObject RawGameObject;
        public Collider RawHitCollider;
        public Vector3 RawHitPoint;
        public Vector3 RawHitNormal;
        public float RawHitDistance;
        public int RawLayer;

        // First meaningful physical hit after safe filtering (SELF / connector helpers).
        public GameObject GameObject;
        public GameObject PrimaryGameObject;
        public Collider HitCollider;
        public bool HasRaycastHit;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public float HitDistance;
        public int Layer;

        public bool RaycastTruncated;
        public List<RaycastCandidate> RaycastCandidates = new List<RaycastCandidate>();
        public List<RelatedTarget> RelatedTargets = new List<RelatedTarget>();

        public bool IsAlive
        {
            get { return GameObject != null; }
        }

        public GameObject InspectionGameObject
        {
            get
            {
                if (PrimaryGameObject != null)
                {
                    return PrimaryGameObject;
                }
                return GameObject;
            }
        }

        public DiagnosticTarget CloneShallow()
        {
            DiagnosticTarget clone = new DiagnosticTarget();
            clone.RawGameObject = RawGameObject;
            clone.RawHitCollider = RawHitCollider;
            clone.RawHitPoint = RawHitPoint;
            clone.RawHitNormal = RawHitNormal;
            clone.RawHitDistance = RawHitDistance;
            clone.RawLayer = RawLayer;

            clone.GameObject = GameObject;
            clone.PrimaryGameObject = PrimaryGameObject;
            clone.HitCollider = HitCollider;
            clone.HasRaycastHit = HasRaycastHit;
            clone.HitPoint = HitPoint;
            clone.HitNormal = HitNormal;
            clone.HitDistance = HitDistance;
            clone.Layer = Layer;
            clone.RaycastTruncated = RaycastTruncated;

            int i;
            for (i = 0; i < RaycastCandidates.Count; i++)
            {
                RaycastCandidate originalCandidate = RaycastCandidates[i];
                RaycastCandidate candidateCopy = new RaycastCandidate();
                candidateCopy.Index = originalCandidate.Index;
                candidateCopy.GameObject = originalCandidate.GameObject;
                candidateCopy.Collider = originalCandidate.Collider;
                candidateCopy.Point = originalCandidate.Point;
                candidateCopy.Normal = originalCandidate.Normal;
                candidateCopy.Distance = originalCandidate.Distance;
                candidateCopy.Layer = originalCandidate.Layer;
                candidateCopy.IsSelf = originalCandidate.IsSelf;
                candidateCopy.IsTrigger = originalCandidate.IsTrigger;
                candidateCopy.IsConnector = originalCandidate.IsConnector;
                candidateCopy.IsSelected = originalCandidate.IsSelected;
                candidateCopy.Classification = originalCandidate.Classification;
                clone.RaycastCandidates.Add(candidateCopy);
            }

            for (i = 0; i < RelatedTargets.Count; i++)
            {
                RelatedTarget original = RelatedTargets[i];
                RelatedTarget copy = new RelatedTarget();
                copy.Role = original.Role;
                copy.GameObject = original.GameObject;
                copy.MatchedComponentTypes = original.MatchedComponentTypes;
                clone.RelatedTargets.Add(copy);
            }

            return clone;
        }
    }
}
