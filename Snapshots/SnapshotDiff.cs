using System;
using System.Collections.Generic;
using System.Globalization;

namespace StrandedDeepDiagnostics.Snapshots
{
    internal sealed class SnapshotDiffResult
    {
        public List<string> Lines = new List<string>();
        public int ChangeCount;
    }

    internal static class SnapshotDiff
    {
        public static SnapshotDiffResult Compare(DiagnosticSnapshot before, DiagnosticSnapshot after)
        {
            SnapshotDiffResult result = new SnapshotDiffResult();
            if (before == null || after == null)
            {
                result.Lines.Add("Diff unavailable: one snapshot is null.");
                return result;
            }

            Add(result, "WorldEpoch", before.WorldEpoch.ToString(CultureInfo.InvariantCulture), after.WorldEpoch.ToString(CultureInfo.InvariantCulture));
            Add(result, "SceneEpoch", before.SceneEpoch.ToString(CultureInfo.InvariantCulture), after.SceneEpoch.ToString(CultureInfo.InvariantCulture));
            Add(result, "Player", PlayerId(before), PlayerId(after));
            CompareObject(result, "Raw", before.RawTarget, after.RawTarget);
            CompareObject(result, "Selected", before.SelectedTarget, after.SelectedTarget);
            CompareObject(result, "Primary", before.Target, after.Target);
            ComparePhysics(result, before.Physics, after.Physics);

            if (result.ChangeCount == 0)
            {
                result.Lines.Add("NO CHANGES detected in snapshot comparison fields.");
            }
            return result;
        }

        private static string PlayerId(DiagnosticSnapshot snapshot)
        {
            if (snapshot.Player == null) return "<none>";
            return snapshot.Player.DisplayName + "|" + snapshot.Player.PlayerId;
        }

        private static void CompareObject(SnapshotDiffResult result, string prefix, ObjectSnapshot a, ObjectSnapshot b)
        {
            if (a == null || b == null)
            {
                Add(result, prefix, a == null ? "<none>" : a.Name, b == null ? "<none>" : b.Name);
                return;
            }

            Add(result, prefix + ".Name", a.Name, b.Name);
            Add(result, prefix + ".RuntimeType", a.RuntimeType, b.RuntimeType);
            Add(result, prefix + ".InstanceID", a.InstanceId.ToString(CultureInfo.InvariantCulture), b.InstanceId.ToString(CultureInfo.InvariantCulture));
            Add(result, prefix + ".Hierarchy", a.HierarchyPath, b.HierarchyPath);
            Add(result, prefix + ".Parent", a.ParentPath, b.ParentPath);
            Add(result, prefix + ".ActiveSelf", a.ActiveSelf.ToString(), b.ActiveSelf.ToString());
            Add(result, prefix + ".ActiveInHierarchy", a.ActiveInHierarchy.ToString(), b.ActiveInHierarchy.ToString());
            Add(result, prefix + ".WorldPosition", a.WorldPosition, b.WorldPosition);
            Add(result, prefix + ".WorldRotation", a.WorldRotation, b.WorldRotation);
            Add(result, prefix + ".LocalPosition", a.LocalPosition, b.LocalPosition);
            Add(result, prefix + ".LocalRotation", a.LocalRotation, b.LocalRotation);
            Add(result, prefix + ".LocalScale", a.LocalScale, b.LocalScale);
        }

        private static void ComparePhysics(SnapshotDiffResult result, PhysicsSnapshot a, PhysicsSnapshot b)
        {
            int aRb = a == null || a.Rigidbodies == null ? 0 : a.Rigidbodies.Count;
            int bRb = b == null || b.Rigidbodies == null ? 0 : b.Rigidbodies.Count;
            int aCol = a == null || a.Colliders == null ? 0 : a.Colliders.Count;
            int bCol = b == null || b.Colliders == null ? 0 : b.Colliders.Count;
            Add(result, "Physics.RigidbodyCount", aRb.ToString(CultureInfo.InvariantCulture), bRb.ToString(CultureInfo.InvariantCulture));
            Add(result, "Physics.ColliderCount", aCol.ToString(CultureInfo.InvariantCulture), bCol.ToString(CultureInfo.InvariantCulture));

            int i;
            int maxRb = Math.Max(aRb, bRb);
            for (i = 0; i < maxRb && i < 8; i++)
            {
                RigidbodySnapshot ra = i < aRb ? a.Rigidbodies[i] : null;
                RigidbodySnapshot rb = i < bRb ? b.Rigidbodies[i] : null;
                CompareRigidbody(result, i, ra, rb);
            }

            int maxCol = Math.Max(aCol, bCol);
            for (i = 0; i < maxCol && i < 16; i++)
            {
                ColliderSnapshot ca = i < aCol ? a.Colliders[i] : null;
                ColliderSnapshot cb = i < bCol ? b.Colliders[i] : null;
                CompareCollider(result, i, ca, cb);
            }
        }

        private static void CompareRigidbody(SnapshotDiffResult result, int index, RigidbodySnapshot a, RigidbodySnapshot b)
        {
            string prefix = "RB[" + index + "]";
            if (a == null || b == null)
            {
                Add(result, prefix, a == null ? "<none>" : a.Path, b == null ? "<none>" : b.Path);
                return;
            }
            Add(result, prefix + ".Path", a.Path, b.Path);
            Add(result, prefix + ".Position", a.Position, b.Position);
            Add(result, prefix + ".Rotation", a.Rotation, b.Rotation);
            Add(result, prefix + ".IsKinematic", a.IsKinematic.ToString(), b.IsKinematic.ToString());
            Add(result, prefix + ".UseGravity", a.UseGravity.ToString(), b.UseGravity.ToString());
            Add(result, prefix + ".Constraints", a.Constraints, b.Constraints);
            Add(result, prefix + ".Velocity", a.Velocity, b.Velocity);
            Add(result, prefix + ".AngularVelocity", a.AngularVelocity, b.AngularVelocity);
            Add(result, prefix + ".Sleeping", a.IsSleeping.ToString(), b.IsSleeping.ToString());
        }

        private static void CompareCollider(SnapshotDiffResult result, int index, ColliderSnapshot a, ColliderSnapshot b)
        {
            string prefix = "Collider[" + index + "]";
            if (a == null || b == null)
            {
                Add(result, prefix, a == null ? "<none>" : a.Path, b == null ? "<none>" : b.Path);
                return;
            }
            Add(result, prefix + ".Path", a.Path, b.Path);
            Add(result, prefix + ".Type", a.Type, b.Type);
            Add(result, prefix + ".Enabled", a.Enabled.ToString(), b.Enabled.ToString());
            Add(result, prefix + ".IsTrigger", a.IsTrigger.ToString(), b.IsTrigger.ToString());
            Add(result, prefix + ".BoundsCenter", a.BoundsCenter, b.BoundsCenter);
            Add(result, prefix + ".BoundsSize", a.BoundsSize, b.BoundsSize);
            Add(result, prefix + ".AttachedRigidbody", a.AttachedRigidbodyPath, b.AttachedRigidbodyPath);
        }

        private static void Add(SnapshotDiffResult result, string key, string before, string after)
        {
            string a = before ?? "<null>";
            string b = after ?? "<null>";
            if (string.Equals(a, b, StringComparison.Ordinal)) return;
            result.ChangeCount++;
            result.Lines.Add("CHANGED " + key);
            result.Lines.Add("  BEFORE: " + a);
            result.Lines.Add("  AFTER : " + b);
        }
    }
}
