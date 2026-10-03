using System;
using System.Collections.Generic;

namespace StrandedDeepDiagnostics.Snapshots
{
    internal sealed class DiagnosticSnapshot
    {
        public string SchemaVersion;
        public DateTime TimestampUtc;
        public int Frame;
        public string Phase;
        public int ProcessEpoch;
        public int WorldEpoch;
        public int SceneEpoch;
        public string ModuleId;
        public PlayerSnapshot Player;
        public ObjectSnapshot RawTarget;
        public ObjectSnapshot SelectedTarget;
        public ObjectSnapshot Target;
        public bool RaycastTruncated;
        public List<RaycastCandidateSnapshot> RaycastCandidates;
        public List<RelatedTargetSnapshot> RelatedTargets;
        public PhysicsSnapshot Physics;
        public List<string> CapabilityLines;
    }

    internal sealed class PlayerSnapshot
    {
        public int DisplayIndex;
        public string DisplayName;
        public string PlayerType;
        public string PlayerId;
        public string CameraPath;
        public string CameraRect;
        public string CameraPosition;
        public string CameraRotation;
        public string CameraFov;
    }

    internal sealed class ObjectSnapshot
    {
        public string Name;
        public string RuntimeType;
        public int InstanceId;
        public string SceneName;
        public bool ActiveSelf;
        public bool ActiveInHierarchy;
        public int Layer;
        public string Tag;
        public string HierarchyPath;
        public string ParentPath;
        public string WorldPosition;
        public string WorldRotation;
        public string LocalPosition;
        public string LocalRotation;
        public string LocalScale;
        public bool HasRaycastHit;
        public string HitColliderType;
        public string HitColliderPath;
        public string HitPoint;
        public string HitNormal;
        public float HitDistance;
        public List<string> ComponentLines;
    }

    internal sealed class RaycastCandidateSnapshot
    {
        public int Index;
        public string Name;
        public int InstanceId;
        public string Path;
        public string ColliderType;
        public float Distance;
        public int Layer;
        public bool IsSelf;
        public bool IsTrigger;
        public bool IsConnector;
        public bool IsSelected;
        public string Classification;
    }

    internal sealed class RelatedTargetSnapshot
    {
        public string Role;
        public string Name;
        public int InstanceId;
        public string Path;
        public string MatchedComponentTypes;
    }

    internal sealed class PhysicsSnapshot
    {
        public List<RigidbodySnapshot> Rigidbodies;
        public List<ColliderSnapshot> Colliders;
    }

    internal sealed class RigidbodySnapshot
    {
        public string Source;
        public int InstanceId;
        public string Path;
        public string Position;
        public string Rotation;
        public string CenterOfMass;
        public string WorldCenterOfMass;
        public float Mass;
        public bool IsKinematic;
        public bool UseGravity;
        public string Constraints;
        public string Velocity;
        public string AngularVelocity;
        public bool IsSleeping;
    }

    internal sealed class ColliderSnapshot
    {
        public string Source;
        public int InstanceId;
        public string Type;
        public string Path;
        public bool Enabled;
        public bool IsTrigger;
        public int Layer;
        public string BoundsCenter;
        public string BoundsSize;
        public string AttachedRigidbodyPath;
    }
}
