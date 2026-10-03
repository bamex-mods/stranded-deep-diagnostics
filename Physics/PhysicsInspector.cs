using System;
using System.Collections.Generic;
using UnityEngine;
using StrandedDeepDiagnostics.Reflection;
using StrandedDeepDiagnostics.Snapshots;
using StrandedDeepDiagnostics.Targets;

namespace StrandedDeepDiagnostics.Physics
{
    internal static class PhysicsInspector
    {
        public static PhysicsSnapshot Capture(DiagnosticTarget target)
        {
            PhysicsSnapshot snapshot = new PhysicsSnapshot();
            snapshot.Rigidbodies = new List<RigidbodySnapshot>();
            snapshot.Colliders = new List<ColliderSnapshot>();

            if (target == null || !target.IsAlive)
            {
                return snapshot;
            }

            GameObject primary = target.InspectionGameObject;
            GameObject raw = target.GameObject;

            AddRigidbody(snapshot, FindDirectRigidbody(primary), "PRIMARY");
            AddRigidbody(snapshot, FindNearestAncestorRigidbody(primary), "ANCESTOR");
            if (target.HitCollider != null)
            {
                Rigidbody attached = null;
                try { attached = target.HitCollider.attachedRigidbody; }
                catch { }
                AddRigidbody(snapshot, attached, "HIT_ATTACHED");
            }

            AddDirectColliders(snapshot, primary, "PRIMARY");
            if (raw != primary)
            {
                AddDirectColliders(snapshot, raw, "RAW");
            }
            if (target.HitCollider != null)
            {
                AddCollider(snapshot, target.HitCollider, "HIT");
            }

            return snapshot;
        }

        public static string[] DescribeOverlay(DiagnosticTarget target)
        {
            PhysicsSnapshot snapshot = Capture(target);
            List<string> lines = new List<string>();
            lines.Add("RB      " + snapshot.Rigidbodies.Count + " unique");
            lines.Add("COLL    " + snapshot.Colliders.Count + " unique");

            if (snapshot.Rigidbodies.Count > 0)
            {
                RigidbodySnapshot rb = snapshot.Rigidbodies[0];
                lines.Add("RB[0]   " + rb.Source + " kin=" + rb.IsKinematic + " grav=" + rb.UseGravity + " sleep=" + rb.IsSleeping);
                lines.Add("VEL     " + rb.Velocity + "  ANG " + rb.AngularVelocity);
            }

            if (snapshot.Colliders.Count > 0)
            {
                ColliderSnapshot collider = snapshot.Colliders[0];
                lines.Add("COL[0]  " + collider.Source + " " + ShortType(collider.Type) + " trigger=" + collider.IsTrigger + " enabled=" + collider.Enabled);
                lines.Add("BOUNDS  center=" + collider.BoundsCenter + " size=" + collider.BoundsSize);
            }

            return lines.ToArray();
        }

        private static Rigidbody FindDirectRigidbody(GameObject gameObject)
        {
            if (gameObject == null) return null;
            try { return gameObject.GetComponent<Rigidbody>(); }
            catch { return null; }
        }

        private static Rigidbody FindNearestAncestorRigidbody(GameObject gameObject)
        {
            if (gameObject == null) return null;
            Transform current = gameObject.transform.parent;
            int depth = 0;
            while (current != null && depth < 16)
            {
                try
                {
                    Rigidbody body = current.GetComponent<Rigidbody>();
                    if (body != null) return body;
                }
                catch
                {
                }
                current = current.parent;
                depth++;
            }
            return null;
        }

        private static void AddDirectColliders(PhysicsSnapshot snapshot, GameObject gameObject, string source)
        {
            if (gameObject == null) return;
            Collider[] colliders;
            try { colliders = gameObject.GetComponents<Collider>(); }
            catch { return; }

            int i;
            for (i = 0; i < colliders.Length; i++)
            {
                AddCollider(snapshot, colliders[i], source);
            }
        }

        private static void AddRigidbody(PhysicsSnapshot snapshot, Rigidbody body, string source)
        {
            if (body == null) return;

            int id;
            try { id = body.GetInstanceID(); }
            catch { id = 0; }

            int i;
            for (i = 0; i < snapshot.Rigidbodies.Count; i++)
            {
                if (snapshot.Rigidbodies[i].InstanceId == id && id != 0) return;
            }

            RigidbodySnapshot item = new RigidbodySnapshot();
            item.Source = source;
            item.InstanceId = id;

            try { item.Path = SafeReflection.GetHierarchyPath(body.transform, 32); }
            catch { item.Path = "<unavailable>"; }
            try { item.Position = ValueFormatter.FormatVector3(body.position); }
            catch { item.Position = "<unavailable>"; }
            try { item.Rotation = ValueFormatter.FormatQuaternionEuler(body.rotation); }
            catch { item.Rotation = "<unavailable>"; }
            try { item.CenterOfMass = ValueFormatter.FormatVector3(body.centerOfMass); }
            catch { item.CenterOfMass = "<unavailable>"; }
            try { item.WorldCenterOfMass = ValueFormatter.FormatVector3(body.worldCenterOfMass); }
            catch { item.WorldCenterOfMass = "<unavailable>"; }
            try { item.Mass = body.mass; }
            catch { item.Mass = 0f; }
            try { item.IsKinematic = body.isKinematic; }
            catch { item.IsKinematic = false; }
            try { item.UseGravity = body.useGravity; }
            catch { item.UseGravity = false; }
            try { item.Constraints = body.constraints.ToString(); }
            catch { item.Constraints = "<unavailable>"; }
            try { item.Velocity = ValueFormatter.FormatVector3(body.velocity); }
            catch { item.Velocity = "<unavailable>"; }
            try { item.AngularVelocity = ValueFormatter.FormatVector3(body.angularVelocity); }
            catch { item.AngularVelocity = "<unavailable>"; }
            try { item.IsSleeping = body.IsSleeping(); }
            catch { item.IsSleeping = false; }

            snapshot.Rigidbodies.Add(item);
        }

        private static void AddCollider(PhysicsSnapshot snapshot, Collider collider, string source)
        {
            if (collider == null) return;

            int id;
            try { id = collider.GetInstanceID(); }
            catch { id = 0; }

            int i;
            for (i = 0; i < snapshot.Colliders.Count; i++)
            {
                if (snapshot.Colliders[i].InstanceId == id && id != 0) return;
            }

            ColliderSnapshot item = new ColliderSnapshot();
            item.Source = source;
            item.InstanceId = id;
            try { item.Type = collider.GetType().FullName; }
            catch { item.Type = "<unavailable>"; }
            try { item.Path = SafeReflection.GetHierarchyPath(collider.transform, 32); }
            catch { item.Path = "<unavailable>"; }
            try { item.Enabled = collider.enabled; }
            catch { item.Enabled = false; }
            try { item.IsTrigger = collider.isTrigger; }
            catch { item.IsTrigger = false; }
            try { item.Layer = collider.gameObject.layer; }
            catch { item.Layer = -1; }
            try
            {
                Bounds bounds = collider.bounds;
                item.BoundsCenter = ValueFormatter.FormatVector3(bounds.center);
                item.BoundsSize = ValueFormatter.FormatVector3(bounds.size);
            }
            catch
            {
                item.BoundsCenter = "<unavailable>";
                item.BoundsSize = "<unavailable>";
            }

            Rigidbody attached = null;
            try { attached = collider.attachedRigidbody; }
            catch { }
            item.AttachedRigidbodyPath = attached == null ? "<none>" : SafeReflection.GetHierarchyPath(attached.transform, 32);
            snapshot.Colliders.Add(item);
        }

        private static string ShortType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "<unknown>";
            int index = fullName.LastIndexOf('.');
            if (index >= 0 && index + 1 < fullName.Length) return fullName.Substring(index + 1);
            return fullName;
        }
    }
}
