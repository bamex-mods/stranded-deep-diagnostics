using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using StrandedDeepDiagnostics.Events;
using StrandedDeepDiagnostics.Reflection;
using StrandedDeepDiagnostics.Targets;

using StrandedDeepDiagnostics.Adapters.BamEx;

namespace StrandedDeepDiagnostics.Raft
{
    internal sealed class RaftDiagnostics
    {
        private const int RingCapacity = 4096;
        private const float DiscoveryInterval = 1.0f;
        private const float IncidentBeforeSeconds = 5.0f;
        private const float IncidentAfterSeconds = 5.0f;
        private const float TiltThresholdDegrees = 45.0f;
        private const float AngularSpikeThreshold = 2.0f;
        private const float LinearSpikeThreshold = 5.0f;
        private const float PositionJumpThreshold = 2.5f;
        private const float CenterOfMassJumpThreshold = 0.35f;

        private sealed class WatchedRaft
        {
            public GameObject Root;
            public Rigidbody Body;
            public RaftDescriptor Descriptor;
            public bool HasLast;
            public RaftSample Last;
            public int LastAnomalyFlags;
        }

        private readonly EventRingBuffer _events;
        private readonly RaftFurnitureAdapter _adapter = new RaftFurnitureAdapter();
        private readonly Dictionary<int, WatchedRaft> _watched = new Dictionary<int, WatchedRaft>();
        private readonly RaftSample[] _ring = new RaftSample[RingCapacity];
        private int _ringWrite;
        private int _ringCount;
        private float _nextDiscoveryAt;
        private bool _armed;
        private bool _incidentPending;
        private float _incidentRealtime;
        private int _incidentSerial;
        private string _incidentTargetSnapshot;
        private int _totalAnomalies;
        private string _lastAnomaly = "<none>";
        private string _lastDiscovery = "not scanned";

        public RaftDiagnostics(EventRingBuffer events)
        {
            _events = events;
        }

        public bool Armed { get { return _armed; } }
        public bool NeedsMonitoring { get { return _armed || _incidentPending; } }

        public string ToggleRecorder()
        {
            if (_armed)
            {
                DisableRecorder();
                return "RAFT recorder OFF";
            }

            _armed = true;
            _nextDiscoveryAt = 0f;
            RefreshWatchSet(true);
            return "RAFT recorder ON; watched=" + _watched.Count;
        }

        public void DisableRecorder()
        {
            _armed = false;
            _incidentPending = false;
            _incidentTargetSnapshot = null;
        }

        public void CancelIncident()
        {
            _incidentPending = false;
            _incidentTargetSnapshot = null;
        }

        public void InvalidateSceneState()
        {
            _watched.Clear();
            _ringWrite = 0;
            _ringCount = 0;
            _nextDiscoveryAt = 0f;
            _incidentPending = false;
            _incidentTargetSnapshot = null;
            _lastDiscovery = "scene invalidated";
        }

        public void Update(DiagnosticTarget target)
        {
            if (!_armed && !_incidentPending) return;
            float now = Time.realtimeSinceStartup;
            if (now >= _nextDiscoveryAt)
            {
                RefreshWatchSet(false);
                _nextDiscoveryAt = now + DiscoveryInterval;
            }
        }

        public void FixedUpdate()
        {
            if (!_armed && !_incidentPending) return;
            if (_watched.Count == 0) return;

            List<int> dead = null;
            foreach (KeyValuePair<int, WatchedRaft> pair in _watched)
            {
                WatchedRaft watched = pair.Value;
                if (watched == null || watched.Root == null || watched.Body == null)
                {
                    if (dead == null) dead = new List<int>();
                    dead.Add(pair.Key);
                    continue;
                }

                RaftSample sample;
                if (!TryCaptureSample(watched, out sample)) continue;
                StoreSample(sample);
                ObserveAnomalies(watched, sample);
                watched.Last = sample;
                watched.HasLast = true;
            }

            if (dead != null)
            {
                int i;
                for (i = 0; i < dead.Count; i++) _watched.Remove(dead[i]);
            }
        }

        public string MarkIncident(DiagnosticTarget target)
        {
            if (!_armed)
            {
                return "RAFT recorder is OFF; press F11 first";
            }
            if (_watched.Count == 0)
            {
                RefreshWatchSet(true);
                if (_watched.Count == 0) return "RAFT incident not marked: no loaded raft resolved";
            }

            _incidentPending = true;
            _incidentRealtime = Time.realtimeSinceStartup;
            _incidentSerial++;
            _incidentTargetSnapshot = _adapter.RenderTargetReport(target);
            AddEvent("USER_MARK", "incident=" + _incidentSerial + " watched=" + _watched.Count, null);
            return "RAFT incident marked; capturing +" + IncidentAfterSeconds.ToString("0", CultureInfo.InvariantCulture) + "s";
        }

        public bool TryCompleteIncident(out string report)
        {
            report = null;
            if (!_incidentPending) return false;
            if (Time.realtimeSinceStartup - _incidentRealtime < IncidentAfterSeconds) return false;
            report = BuildIncidentReport();
            _incidentPending = false;
            _incidentTargetSnapshot = null;
            return true;
        }

        public string[] DescribeOverlay(DiagnosticTarget target)
        {
            List<string> lines = new List<string>();
            lines.Add("RAFT recorder=" + (_armed ? "ON" : "OFF") + " watched=" + _watched.Count + " samples=" + _ringCount);
            lines.Add("DISCOVER " + Trim(_lastDiscovery, 72));
            lines.Add("ANOMALY total=" + _totalAnomalies + " last=" + Trim(_lastAnomaly, 58));

            int shown = 0;
            foreach (KeyValuePair<int, WatchedRaft> pair in _watched)
            {
                WatchedRaft watched = pair.Value;
                if (watched == null || !watched.HasLast) continue;
                RaftSample s = watched.Last;
                string name = watched.Descriptor == null ? "raft" : watched.Descriptor.Name;
                lines.Add("RAFT " + Trim(name, 24) + " tilt=" + s.TiltDegrees.ToString("0.0", CultureInfo.InvariantCulture) + " vel=" + s.Velocity.magnitude.ToString("0.00", CultureInfo.InvariantCulture) + " ang=" + s.AngularVelocity.magnitude.ToString("0.00", CultureInfo.InvariantCulture));
                lines.Add("  dPos=" + s.DeltaPosition.ToString("0.000", CultureInfo.InvariantCulture) + " dVel=" + s.DeltaVelocity.ToString("0.000", CultureInfo.InvariantCulture) + " dAng=" + s.DeltaAngularVelocity.ToString("0.000", CultureInfo.InvariantCulture));
                lines.Add("  coll=" + s.ColliderCount + " childRB=" + s.ChildRigidbodyCount + " kin=" + s.IsKinematic + " grav=" + s.UseGravity);
                shown++;
                if (shown >= 2) break;
            }

            lines.Add("TARGET  " + Trim(_adapter.DescribeTargetShort(target), 72));
            lines.Add(_incidentPending ? "INCIDENT waiting for +5s post-window" : "F11 arms recorder | F12 marks 5s-before/5s-after incident");
            return lines.ToArray();
        }

        public string RenderCurrentReport(DiagnosticTarget target)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== RAFT COMPOSITE STATUS ===");
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("Recorder armed: " + _armed);
            sb.AppendLine("Watched rafts: " + _watched.Count);
            sb.AppendLine("Ring samples: " + _ringCount + " / " + RingCapacity);
            sb.AppendLine("Total anomalies: " + _totalAnomalies);
            sb.AppendLine("Last anomaly: " + _lastAnomaly);
            sb.AppendLine();
            AppendCurrentRafts(sb);
            sb.AppendLine();
            sb.Append(_adapter.RenderTargetReport(target));
            return sb.ToString();
        }

        private void RefreshWatchSet(bool force)
        {
            List<RaftCandidate> candidates = RaftDiscovery.FindLoadedRafts();
            HashSet<int> seen = new HashSet<int>();
            int i;
            for (i = 0; i < candidates.Count; i++)
            {
                RaftCandidate candidate = candidates[i];
                if (candidate == null || candidate.Root == null || candidate.Body == null) continue;
                int id;
                try { id = candidate.Root.GetInstanceID(); }
                catch { continue; }
                seen.Add(id);

                WatchedRaft watched;
                if (!_watched.TryGetValue(id, out watched))
                {
                    watched = new WatchedRaft();
                    watched.Root = candidate.Root;
                    watched.Body = candidate.Body;
                    watched.Descriptor = ToDescriptor(id, candidate);
                    _watched[id] = watched;
                    AddEvent("RAFT_APPEARED", "source=" + candidate.SourceType + " ref=" + candidate.ReferenceId, watched.Descriptor);
                }
                else
                {
                    watched.Root = candidate.Root;
                    watched.Body = candidate.Body;
                    int oldColliderCount = watched.Descriptor == null ? -1 : watched.Descriptor.ColliderCount;
                    int oldColliderHash = watched.Descriptor == null ? 0 : watched.Descriptor.ColliderHash;
                    int oldBodyCount = watched.Descriptor == null ? -1 : watched.Descriptor.ChildRigidbodyCount;
                    int oldBodyHash = watched.Descriptor == null ? 0 : watched.Descriptor.ChildRigidbodyHash;
                    RaftDescriptor next = ToDescriptor(id, candidate);
                    watched.Descriptor = next;

                    if (oldColliderCount >= 0 && (oldColliderCount != next.ColliderCount || oldColliderHash != next.ColliderHash))
                    {
                        AddTopologyAnomaly(watched, RaftAnomalyFlags.ColliderTopologyChanged, "RAFT_COLLIDER_SET_CHANGED " + oldColliderCount + "/" + oldColliderHash + " -> " + next.ColliderCount + "/" + next.ColliderHash);
                    }
                    if (oldBodyCount >= 0 && (oldBodyCount != next.ChildRigidbodyCount || oldBodyHash != next.ChildRigidbodyHash))
                    {
                        AddTopologyAnomaly(watched, RaftAnomalyFlags.ChildRigidbodyTopologyChanged, "RAFT_CHILD_RB_CHANGED " + oldBodyCount + "/" + oldBodyHash + " -> " + next.ChildRigidbodyCount + "/" + next.ChildRigidbodyHash);
                    }
                }
            }

            List<int> remove = new List<int>();
            foreach (KeyValuePair<int, WatchedRaft> pair in _watched)
            {
                if (!seen.Contains(pair.Key)) remove.Add(pair.Key);
            }
            for (i = 0; i < remove.Count; i++)
            {
                WatchedRaft old = _watched[remove[i]];
                AddEvent("RAFT_DISAPPEARED", "instance=" + remove[i], old == null ? null : old.Descriptor);
                _watched.Remove(remove[i]);
            }

            _lastDiscovery = "candidates=" + candidates.Count + " watched=" + _watched.Count + (force ? " force" : string.Empty);
        }

        private static RaftDescriptor ToDescriptor(int id, RaftCandidate candidate)
        {
            RaftDescriptor d = new RaftDescriptor();
            d.InstanceId = id;
            try { d.Name = candidate.Root.name; } catch { d.Name = "<unavailable>"; }
            d.Path = candidate.Path;
            d.SourceType = candidate.SourceType;
            d.ReferenceId = candidate.ReferenceId;
            d.ColliderCount = candidate.ColliderCount;
            d.ColliderHash = candidate.ColliderHash;
            d.ChildRigidbodyCount = candidate.ChildRigidbodyCount;
            d.ChildRigidbodyHash = candidate.ChildRigidbodyHash;
            return d;
        }

        private bool TryCaptureSample(WatchedRaft watched, out RaftSample sample)
        {
            sample = new RaftSample();
            GameObject root = watched.Root;
            Rigidbody rb = watched.Body;
            if (root == null || rb == null) return false;

            try
            {
                sample.Realtime = Time.realtimeSinceStartup;
                sample.FixedTime = Time.fixedTime;
                sample.Frame = Time.frameCount;
                sample.InstanceId = root.GetInstanceID();
                sample.Position = root.transform.position;
                sample.Rotation = root.transform.rotation;
                sample.Up = root.transform.up;
                sample.Forward = root.transform.forward;

                sample.RigidbodyPosition = rb.position;
                sample.RigidbodyRotation = rb.rotation;
                sample.Velocity = rb.velocity;
                sample.AngularVelocity = rb.angularVelocity;
                sample.Mass = rb.mass;
                sample.Drag = rb.drag;
                sample.AngularDrag = rb.angularDrag;
                sample.IsKinematic = rb.isKinematic;
                sample.UseGravity = rb.useGravity;
                sample.Constraints = (int)rb.constraints;
                sample.CenterOfMass = rb.centerOfMass;
                sample.WorldCenterOfMass = rb.worldCenterOfMass;
                sample.IsSleeping = rb.IsSleeping();
                sample.Interpolation = (int)rb.interpolation;
                sample.CollisionDetectionMode = (int)rb.collisionDetectionMode;
                sample.TiltDegrees = Vector3.Angle(sample.Up, Vector3.up);

                if (watched.Descriptor != null)
                {
                    sample.ColliderCount = watched.Descriptor.ColliderCount;
                    sample.ColliderHash = watched.Descriptor.ColliderHash;
                    sample.ChildRigidbodyCount = watched.Descriptor.ChildRigidbodyCount;
                    sample.ChildRigidbodyHash = watched.Descriptor.ChildRigidbodyHash;
                }

                if (watched.HasLast)
                {
                    RaftSample previous = watched.Last;
                    sample.DeltaPosition = Vector3.Distance(previous.Position, sample.Position);
                    sample.DeltaRotationDegrees = Quaternion.Angle(previous.Rotation, sample.Rotation);
                    sample.DeltaVelocity = (sample.Velocity - previous.Velocity).magnitude;
                    sample.DeltaAngularVelocity = (sample.AngularVelocity - previous.AngularVelocity).magnitude;
                    sample.DeltaWorldCenterOfMass = Vector3.Distance(previous.WorldCenterOfMass, sample.WorldCenterOfMass);
                    sample.AnomalyFlags = EvaluateAnomalies(previous, sample);
                }
                else
                {
                    sample.AnomalyFlags = sample.TiltDegrees >= TiltThresholdDegrees ? RaftAnomalyFlags.Tilt : RaftAnomalyFlags.None;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int EvaluateAnomalies(RaftSample previous, RaftSample current)
        {
            int flags = RaftAnomalyFlags.None;
            if (current.TiltDegrees >= TiltThresholdDegrees) flags |= RaftAnomalyFlags.Tilt;
            if (current.DeltaAngularVelocity >= AngularSpikeThreshold) flags |= RaftAnomalyFlags.AngularSpike;
            if (current.DeltaVelocity >= LinearSpikeThreshold) flags |= RaftAnomalyFlags.LinearSpike;
            if (current.DeltaPosition >= PositionJumpThreshold) flags |= RaftAnomalyFlags.PositionJump;
            if (current.DeltaWorldCenterOfMass >= CenterOfMassJumpThreshold) flags |= RaftAnomalyFlags.CenterOfMassChanged;
            if (previous.IsKinematic != current.IsKinematic || previous.UseGravity != current.UseGravity || previous.Constraints != current.Constraints)
                flags |= RaftAnomalyFlags.RigidbodyStateChanged;
            return flags;
        }

        private void ObserveAnomalies(WatchedRaft watched, RaftSample sample)
        {
            int flags = sample.AnomalyFlags;
            int previousFlags = watched.LastAnomalyFlags;
            watched.LastAnomalyFlags = flags;
            if (flags == 0) return;

            int newlyActive = flags & ~previousFlags;
            int transient = flags & (RaftAnomalyFlags.AngularSpike | RaftAnomalyFlags.LinearSpike | RaftAnomalyFlags.PositionJump | RaftAnomalyFlags.CenterOfMassChanged | RaftAnomalyFlags.RigidbodyStateChanged);
            int emit = newlyActive | transient;
            if (emit == 0) return;

            string text = FlagsToText(emit) + " instance=" + sample.InstanceId + " tilt=" + sample.TiltDegrees.ToString("0.0", CultureInfo.InvariantCulture) + " dPos=" + sample.DeltaPosition.ToString("0.000", CultureInfo.InvariantCulture) + " dVel=" + sample.DeltaVelocity.ToString("0.000", CultureInfo.InvariantCulture) + " dAng=" + sample.DeltaAngularVelocity.ToString("0.000", CultureInfo.InvariantCulture);
            _totalAnomalies++;
            _lastAnomaly = text;
            AddEvent("RAFT_ANOMALY", text, watched.Descriptor);
        }

        private void AddTopologyAnomaly(WatchedRaft watched, int flag, string text)
        {
            _totalAnomalies++;
            _lastAnomaly = text;
            AddEvent("RAFT_ANOMALY", text, watched == null ? null : watched.Descriptor);
        }

        private void StoreSample(RaftSample sample)
        {
            _ring[_ringWrite] = sample;
            _ringWrite = (_ringWrite + 1) % RingCapacity;
            if (_ringCount < RingCapacity) _ringCount++;
        }

        private string BuildIncidentReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== RAFT INCIDENT ===");
            sb.AppendLine("Incident: " + _incidentSerial);
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("User mark realtime: " + _incidentRealtime.ToString("0.000", CultureInfo.InvariantCulture));
            sb.AppendLine("Window: -" + IncidentBeforeSeconds.ToString("0", CultureInfo.InvariantCulture) + "s / +" + IncidentAfterSeconds.ToString("0", CultureInfo.InvariantCulture) + "s");
            sb.AppendLine("Watched rafts now: " + _watched.Count);
            sb.AppendLine("Total anomalies since scene/reset: " + _totalAnomalies);
            sb.AppendLine();
            AppendCurrentRafts(sb);
            sb.AppendLine();
            sb.AppendLine("=== TARGET / RAFT FURNITURE ADAPTER AT USER MARK ===");
            sb.AppendLine(_incidentTargetSnapshot ?? "<none>");
            sb.AppendLine();
            sb.AppendLine("=== TIMELINE ===");
            AppendIncidentSamples(sb);
            sb.AppendLine();
            sb.AppendLine("=== CORRELATED EVENT BUFFER (last 256 events) ===");
            if (_events != null) sb.Append(_events.RenderReport(256));
            return sb.ToString();
        }

        private void AppendIncidentSamples(StringBuilder sb)
        {
            float min = _incidentRealtime - IncidentBeforeSeconds;
            float max = _incidentRealtime + IncidentAfterSeconds;
            int start = (_ringWrite - _ringCount + RingCapacity) % RingCapacity;
            int i;
            int emitted = 0;
            for (i = 0; i < _ringCount; i++)
            {
                RaftSample s = _ring[(start + i) % RingCapacity];
                if (s.Realtime < min || s.Realtime > max) continue;
                sb.Append("t="); sb.Append((s.Realtime - _incidentRealtime).ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture));
                sb.Append(" frame="); sb.Append(s.Frame);
                sb.Append(" fixed="); sb.Append(s.FixedTime.ToString("0.000", CultureInfo.InvariantCulture));
                sb.Append(" raft="); sb.Append(s.InstanceId);
                sb.Append(" pos="); sb.Append(ValueFormatter.FormatVector3(s.Position));
                sb.Append(" vel="); sb.Append(ValueFormatter.FormatVector3(s.Velocity));
                sb.Append(" ang="); sb.Append(ValueFormatter.FormatVector3(s.AngularVelocity));
                sb.Append(" tilt="); sb.Append(s.TiltDegrees.ToString("0.0", CultureInfo.InvariantCulture));
                sb.Append(" dPos="); sb.Append(s.DeltaPosition.ToString("0.000", CultureInfo.InvariantCulture));
                sb.Append(" dVel="); sb.Append(s.DeltaVelocity.ToString("0.000", CultureInfo.InvariantCulture));
                sb.Append(" dAng="); sb.Append(s.DeltaAngularVelocity.ToString("0.000", CultureInfo.InvariantCulture));
                sb.Append(" dWCOM="); sb.Append(s.DeltaWorldCenterOfMass.ToString("0.000", CultureInfo.InvariantCulture));
                sb.Append(" kin="); sb.Append(s.IsKinematic);
                sb.Append(" grav="); sb.Append(s.UseGravity);
                sb.Append(" coll="); sb.Append(s.ColliderCount);
                sb.Append(" childRB="); sb.Append(s.ChildRigidbodyCount);
                if (s.AnomalyFlags != 0) { sb.Append(" FLAGS="); sb.Append(FlagsToText(s.AnomalyFlags)); }
                sb.AppendLine();
                emitted++;
            }
            if (emitted == 0) sb.AppendLine("<no raft samples in incident window>");
        }

        private void AppendCurrentRafts(StringBuilder sb)
        {
            if (_watched.Count == 0)
            {
                sb.AppendLine("<no loaded rafts resolved>");
                return;
            }

            foreach (KeyValuePair<int, WatchedRaft> pair in _watched)
            {
                WatchedRaft watched = pair.Value;
                RaftDescriptor d = watched == null ? null : watched.Descriptor;
                sb.AppendLine("RAFT instance=" + pair.Key);
                if (d != null)
                {
                    sb.AppendLine("  name=" + d.Name);
                    sb.AppendLine("  sourceType=" + d.SourceType);
                    sb.AppendLine("  referenceId=" + d.ReferenceId);
                    sb.AppendLine("  path=" + d.Path);
                    sb.AppendLine("  colliderTopology=" + d.ColliderCount + "/" + d.ColliderHash);
                    sb.AppendLine("  childRigidbodyTopology=" + d.ChildRigidbodyCount + "/" + d.ChildRigidbodyHash);
                }
                if (watched != null && watched.HasLast)
                {
                    RaftSample s = watched.Last;
                    sb.AppendLine("  position=" + ValueFormatter.FormatVector3(s.Position));
                    sb.AppendLine("  rotationEuler=" + ValueFormatter.FormatQuaternionEuler(s.Rotation));
                    sb.AppendLine("  up=" + ValueFormatter.FormatVector3(s.Up));
                    sb.AppendLine("  forward=" + ValueFormatter.FormatVector3(s.Forward));
                    sb.AppendLine("  rbPosition=" + ValueFormatter.FormatVector3(s.RigidbodyPosition));
                    sb.AppendLine("  rbRotationEuler=" + ValueFormatter.FormatQuaternionEuler(s.RigidbodyRotation));
                    sb.AppendLine("  velocity=" + ValueFormatter.FormatVector3(s.Velocity));
                    sb.AppendLine("  angularVelocity=" + ValueFormatter.FormatVector3(s.AngularVelocity));
                    sb.AppendLine("  mass/drag/angularDrag=" + s.Mass.ToString("0.###", CultureInfo.InvariantCulture) + "/" + s.Drag.ToString("0.###", CultureInfo.InvariantCulture) + "/" + s.AngularDrag.ToString("0.###", CultureInfo.InvariantCulture));
                    sb.AppendLine("  isKinematic/useGravity/constraints=" + s.IsKinematic + "/" + s.UseGravity + "/" + s.Constraints);
                    sb.AppendLine("  centerOfMass=" + ValueFormatter.FormatVector3(s.CenterOfMass));
                    sb.AppendLine("  worldCenterOfMass=" + ValueFormatter.FormatVector3(s.WorldCenterOfMass));
                    sb.AppendLine("  sleeping/interpolation/collisionMode=" + s.IsSleeping + "/" + s.Interpolation + "/" + s.CollisionDetectionMode);
                    sb.AppendLine("  tilt=" + s.TiltDegrees.ToString("0.0", CultureInfo.InvariantCulture));
                }
            }
        }

        private void AddEvent(string phase, string payload, RaftDescriptor descriptor)
        {
            if (_events == null) return;
            DiagnosticEvent e = new DiagnosticEvent();
            e.TimestampUtc = DateTime.UtcNow;
            e.Frame = Time.frameCount;
            e.Phase = phase;
            e.Category = "RAFT";
            e.Module = "RAFT";
            e.Target = descriptor == null ? null : descriptor.Name + "#" + descriptor.InstanceId;
            e.Payload = payload;
            _events.Add(e);
        }

        private static string FlagsToText(int flags)
        {
            List<string> names = new List<string>();
            if ((flags & RaftAnomalyFlags.Tilt) != 0) names.Add("RAFT_TILT");
            if ((flags & RaftAnomalyFlags.AngularSpike) != 0) names.Add("RAFT_ANGULAR_SPIKE");
            if ((flags & RaftAnomalyFlags.LinearSpike) != 0) names.Add("RAFT_LINEAR_SPIKE");
            if ((flags & RaftAnomalyFlags.PositionJump) != 0) names.Add("RAFT_POSITION_JUMP");
            if ((flags & RaftAnomalyFlags.RigidbodyStateChanged) != 0) names.Add("RAFT_RB_STATE_CHANGED");
            if ((flags & RaftAnomalyFlags.CenterOfMassChanged) != 0) names.Add("RAFT_CENTER_OF_MASS_CHANGED");
            if ((flags & RaftAnomalyFlags.ColliderTopologyChanged) != 0) names.Add("RAFT_COLLIDER_SET_CHANGED");
            if ((flags & RaftAnomalyFlags.ChildRigidbodyTopologyChanged) != 0) names.Add("RAFT_CHILD_RB_CHANGED");
            return names.Count == 0 ? "NONE" : string.Join("|", names.ToArray());
        }

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            if (text.Length <= max) return text;
            return text.Substring(0, Math.Max(0, max - 3)) + "...";
        }
    }
}
