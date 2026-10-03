using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using StrandedDeepDiagnostics.Core;
using StrandedDeepDiagnostics.Reflection;

namespace StrandedDeepDiagnostics.Players
{
    internal sealed class PlayerResolver
    {
        private readonly Type _playerType;

        public PlayerResolver(CapabilityManifest manifest)
        {
            _playerType = manifest == null ? null : manifest.BeamPlayerType;
        }

        public List<PlayerContext> ResolvePlayers()
        {
            List<PlayerContext> result = new List<PlayerContext>();
            if (_playerType == null)
            {
                return result;
            }

            UnityEngine.Object[] objects;
            try
            {
                objects = Resources.FindObjectsOfTypeAll(_playerType);
            }
            catch
            {
                return result;
            }

            int i;
            for (i = 0; i < objects.Length; i++)
            {
                Component component = objects[i] as Component;
                if (component == null)
                {
                    continue;
                }

                try
                {
                    if (!component.gameObject.scene.IsValid())
                    {
                        continue;
                    }

                    if (!component.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                PlayerContext context = new PlayerContext();
                context.PlayerObject = component;
                context.PlayerTypeName = component.GetType().FullName;
                context.PlayerId = ResolveKnownId(component);
                context.Camera = ResolveCamera(component);
                context.CameraPath = context.Camera == null ? "<unresolved>" : SafeReflection.GetHierarchyPath(context.Camera.transform, 32);
                context.CameraRect = context.Camera == null ? new Rect(0f, 0f, 1f, 1f) : context.Camera.rect;
                result.Add(context);
            }

            result.Sort(ComparePlayers);
            for (i = 0; i < result.Count; i++)
            {
                result[i].DisplayIndex = i + 1;
            }

            return result;
        }

        private static int ComparePlayers(PlayerContext a, PlayerContext b)
        {
            if (a == null && b == null)
            {
                return 0;
            }
            if (a == null)
            {
                return 1;
            }
            if (b == null)
            {
                return -1;
            }

            if (a.Camera == null && b.Camera != null)
            {
                return 1;
            }
            if (a.Camera != null && b.Camera == null)
            {
                return -1;
            }
            if (a.Camera == null && b.Camera == null)
            {
                return string.Compare(a.PlayerId, b.PlayerId, StringComparison.Ordinal);
            }

            int xCompare = a.CameraRect.x.CompareTo(b.CameraRect.x);
            if (xCompare != 0)
            {
                return xCompare;
            }

            int yCompare = b.CameraRect.y.CompareTo(a.CameraRect.y);
            if (yCompare != 0)
            {
                return yCompare;
            }

            return a.Camera.GetInstanceID().CompareTo(b.Camera.GetInstanceID());
        }

        private static string ResolveKnownId(object player)
        {
            object value = SafeReflection.GetKnownMemberValue(player, "Id");
            if (value == null)
            {
                return "<unknown>";
            }

            return ValueFormatter.FormatSimple(value);
        }

        private static Camera ResolveCamera(Component player)
        {
            object playerCamera = SafeReflection.GetKnownMemberValue(player, "PlayerCamera");
            if (playerCamera != null)
            {
                Camera direct = playerCamera as Camera;
                if (direct != null)
                {
                    return direct;
                }

                object cameraMember = SafeReflection.GetKnownMemberValue(playerCamera, "Camera");
                Camera memberCamera = cameraMember as Camera;
                if (memberCamera != null)
                {
                    return memberCamera;
                }

                Component playerCameraComponent = playerCamera as Component;
                if (playerCameraComponent != null)
                {
                    Camera nested = FindBestCamera(playerCameraComponent.gameObject);
                    if (nested != null)
                    {
                        return nested;
                    }
                }
            }

            object playerCameraMember = SafeReflection.GetKnownMemberValue(player, "Camera");
            Camera camera = playerCameraMember as Camera;
            if (camera != null)
            {
                return camera;
            }

            return FindBestCamera(player.gameObject);
        }

        private static Camera FindBestCamera(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            Camera[] cameras;
            try
            {
                cameras = root.GetComponentsInChildren<Camera>(true);
            }
            catch
            {
                return null;
            }

            Camera best = null;
            int bestScore = int.MinValue;
            int i;
            for (i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate == null)
                {
                    continue;
                }

                int score = 0;
                string path = SafeReflection.GetHierarchyPath(candidate.transform, 32);
                string upper = path == null ? string.Empty : path.ToUpperInvariant();

                if (upper.IndexOf("PLAYERCAMERA", StringComparison.Ordinal) >= 0)
                {
                    score += 100;
                }
                if (upper.IndexOf("PLAYER CAMERA", StringComparison.Ordinal) >= 0)
                {
                    score += 80;
                }
                if (upper.IndexOf("UI", StringComparison.Ordinal) >= 0 || upper.IndexOf("GAMEUI", StringComparison.Ordinal) >= 0)
                {
                    score -= 120;
                }
                if (candidate.targetTexture == null)
                {
                    score += 30;
                }
                if (candidate.rect.width > 0.1f && candidate.rect.height > 0.1f)
                {
                    score += 20;
                }
                if (candidate.enabled)
                {
                    score += 10;
                }

                if (best == null || score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }
    }
}
