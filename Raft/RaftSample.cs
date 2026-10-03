using UnityEngine;

namespace StrandedDeepDiagnostics.Raft
{
    internal static class RaftAnomalyFlags
    {
        public const int None = 0;
        public const int Tilt = 1;
        public const int AngularSpike = 2;
        public const int LinearSpike = 4;
        public const int PositionJump = 8;
        public const int RigidbodyStateChanged = 16;
        public const int CenterOfMassChanged = 32;
        public const int ColliderTopologyChanged = 64;
        public const int ChildRigidbodyTopologyChanged = 128;
    }

    internal struct RaftSample
    {
        public float Realtime;
        public float FixedTime;
        public int Frame;
        public int InstanceId;

        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Up;
        public Vector3 Forward;

        public Vector3 RigidbodyPosition;
        public Quaternion RigidbodyRotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public float Mass;
        public float Drag;
        public float AngularDrag;
        public bool IsKinematic;
        public bool UseGravity;
        public int Constraints;
        public Vector3 CenterOfMass;
        public Vector3 WorldCenterOfMass;
        public bool IsSleeping;
        public int Interpolation;
        public int CollisionDetectionMode;

        public float TiltDegrees;
        public float DeltaPosition;
        public float DeltaRotationDegrees;
        public float DeltaVelocity;
        public float DeltaAngularVelocity;
        public float DeltaWorldCenterOfMass;

        public int ColliderCount;
        public int ColliderHash;
        public int ChildRigidbodyCount;
        public int ChildRigidbodyHash;
        public int AnomalyFlags;
    }

    internal sealed class RaftDescriptor
    {
        public int InstanceId;
        public string Name;
        public string Path;
        public string SourceType;
        public string ReferenceId;
        public int ColliderCount;
        public int ColliderHash;
        public int ChildRigidbodyCount;
        public int ChildRigidbodyHash;
    }
}
