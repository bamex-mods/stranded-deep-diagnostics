using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using StrandedDeepDiagnostics.Players;
using StrandedDeepDiagnostics.Reflection;

namespace StrandedDeepDiagnostics.CameraInspection
{
    internal static class CameraInspector
    {
        public static string[] DescribeOverlay(PlayerContext player)
        {
            List<string> lines = new List<string>();
            Camera camera = player == null ? null : player.Camera;
            if (camera == null)
            {
                lines.Add("CAMERA  <unresolved>");
                return lines.ToArray();
            }

            lines.Add("ENABLED " + SafeBool(camera.enabled) + " active=" + SafeActive(camera));
            lines.Add("RECT    " + ValueFormatter.FormatSimple(camera.rect));
            lines.Add("PIXEL   " + ValueFormatter.FormatSimple(camera.pixelRect) + " " + camera.pixelWidth + "x" + camera.pixelHeight);
            lines.Add("FOV     " + F(camera.fieldOfView) + " near=" + F(camera.nearClipPlane) + " far=" + F(camera.farClipPlane));
            lines.Add("DEPTH   " + F(camera.depth) + " clear=" + camera.clearFlags + " mask=0x" + camera.cullingMask.ToString("X8", CultureInfo.InvariantCulture));
            lines.Add("POS     " + ValueFormatter.FormatVector3(camera.transform.position));
            lines.Add("FORWARD " + ValueFormatter.FormatVector3(camera.transform.forward));
            lines.Add("TARGET  " + DescribeTargetTexture(camera.targetTexture));
            lines.Add("ACTIVE CAMERAS  " + SafeAllCameraCount());
            lines.Add("F12 exports current camera + active camera census");
            return lines.ToArray();
        }

        public static string RenderReport(PlayerContext player)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== CAMERA INSPECTOR ===");
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("Player: " + (player == null ? "<unresolved>" : player.DisplayName));
            sb.AppendLine();

            Camera camera = player == null ? null : player.Camera;
            WriteCamera(sb, "ACTIVE PLAYER CAMERA", camera);

            sb.AppendLine("=== Camera.allCameras ===");
            Camera[] cameras = SafeAllCameras();
            sb.AppendLine("Count: " + cameras.Length.ToString(CultureInfo.InvariantCulture));
            int i;
            for (i = 0; i < cameras.Length; i++)
            {
                WriteCamera(sb, "CAMERA[" + i.ToString(CultureInfo.InvariantCulture) + "]", cameras[i]);
            }

            return sb.ToString();
        }

        private static void WriteCamera(StringBuilder sb, string title, Camera camera)
        {
            sb.AppendLine("--- " + title + " ---");
            if (camera == null)
            {
                sb.AppendLine("<none>");
                sb.AppendLine();
                return;
            }

            try { sb.AppendLine("Name: " + camera.name); } catch { sb.AppendLine("Name: <unavailable>"); }
            try { sb.AppendLine("InstanceID: " + camera.GetInstanceID().ToString(CultureInfo.InvariantCulture)); } catch { sb.AppendLine("InstanceID: <unavailable>"); }
            try { sb.AppendLine("Path: " + SafeReflection.GetHierarchyPath(camera.transform, 48)); } catch { sb.AppendLine("Path: <unavailable>"); }
            sb.AppendLine("Enabled: " + SafeBool(camera.enabled));
            sb.AppendLine("ActiveInHierarchy: " + SafeActive(camera));
            sb.AppendLine("Rect: " + ValueFormatter.FormatSimple(camera.rect));
            sb.AppendLine("PixelRect: " + ValueFormatter.FormatSimple(camera.pixelRect));
            sb.AppendLine("PixelSize: " + camera.pixelWidth.ToString(CultureInfo.InvariantCulture) + "x" + camera.pixelHeight.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("FieldOfView: " + F(camera.fieldOfView));
            sb.AppendLine("NearClip: " + F(camera.nearClipPlane));
            sb.AppendLine("FarClip: " + F(camera.farClipPlane));
            sb.AppendLine("Depth: " + F(camera.depth));
            sb.AppendLine("ClearFlags: " + camera.clearFlags);
            sb.AppendLine("CullingMask: 0x" + camera.cullingMask.ToString("X8", CultureInfo.InvariantCulture));
            sb.AppendLine("Orthographic: " + camera.orthographic);
            sb.AppendLine("OrthographicSize: " + F(camera.orthographicSize));
            sb.AppendLine("Position: " + ValueFormatter.FormatVector3(camera.transform.position));
            sb.AppendLine("Rotation: " + ValueFormatter.FormatQuaternionEuler(camera.transform.rotation));
            sb.AppendLine("Forward: " + ValueFormatter.FormatVector3(camera.transform.forward));
            sb.AppendLine("Up: " + ValueFormatter.FormatVector3(camera.transform.up));
            sb.AppendLine("TargetTexture: " + DescribeTargetTexture(camera.targetTexture));
            sb.AppendLine();
        }

        private static string DescribeTargetTexture(RenderTexture target)
        {
            if (target == null) return "<screen>";
            try
            {
                return target.name + " " + target.width + "x" + target.height + " format=" + target.format;
            }
            catch
            {
                return "<RenderTexture unavailable>";
            }
        }

        private static int SafeAllCameraCount()
        {
            return SafeAllCameras().Length;
        }

        private static Camera[] SafeAllCameras()
        {
            try { return Camera.allCameras ?? new Camera[0]; }
            catch { return new Camera[0]; }
        }

        private static string SafeBool(bool value)
        {
            return value ? "True" : "False";
        }

        private static string SafeActive(Camera camera)
        {
            try { return camera.gameObject.activeInHierarchy ? "True" : "False"; }
            catch { return "<unavailable>"; }
        }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
