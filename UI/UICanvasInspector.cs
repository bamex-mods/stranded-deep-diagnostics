using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using StrandedDeepDiagnostics.Players;
using StrandedDeepDiagnostics.Reflection;

namespace StrandedDeepDiagnostics.UI
{
    internal sealed class UICanvasInspector
    {
        private readonly List<UICanvasRecord> _records = new List<UICanvasRecord>();
        private float _nextRefreshAt;

        public IList<UICanvasRecord> GetRecords(IList<PlayerContext> players, float realtimeSinceStartup, bool force)
        {
            if (force || realtimeSinceStartup >= _nextRefreshAt)
            {
                Refresh(players);
                _nextRefreshAt = realtimeSinceStartup + 0.75f;
            }
            return _records.AsReadOnly();
        }

        public string[] DescribeOverlay(IList<PlayerContext> players, PlayerContext activePlayer, float realtimeSinceStartup)
        {
            IList<UICanvasRecord> records = GetRecords(players, realtimeSinceStartup, false);
            List<string> lines = new List<string>();
            int active = 0;
            int owned = 0;
            int i;
            for (i = 0; i < records.Count; i++)
            {
                if (records[i].ActiveInHierarchy) active++;
                if (activePlayer != null && string.Equals(records[i].Owner, activePlayer.DisplayName, StringComparison.Ordinal)) owned++;
            }

            lines.Add("CANVASES total=" + records.Count.ToString(CultureInfo.InvariantCulture) + " active=" + active.ToString(CultureInfo.InvariantCulture));
            lines.Add("ACTIVE PLAYER MATCHES " + owned.ToString(CultureInfo.InvariantCulture));

            int emitted = 0;
            for (i = 0; i < records.Count && emitted < 6; i++)
            {
                UICanvasRecord record = records[i];
                if (activePlayer != null && !string.Equals(record.Owner, activePlayer.DisplayName, StringComparison.Ordinal)) continue;
                lines.Add(Trim(record.Owner + " " + record.RenderMode + " " + record.Path, 76));
                if (!string.IsNullOrEmpty(record.ReferenceResolution))
                    lines.Add("  ref=" + record.ReferenceResolution + " scale=" + record.ScaleFactor);
                emitted++;
            }

            if (emitted == 0)
            {
                int max = Math.Min(4, records.Count);
                for (i = 0; i < max; i++)
                    lines.Add(Trim(records[i].Owner + " " + records[i].RenderMode + " " + records[i].Path, 76));
            }

            lines.Add("F12 exports full scene-valid Canvas census");
            return lines.ToArray();
        }

        public string RenderReport(IList<PlayerContext> players, float realtimeSinceStartup)
        {
            IList<UICanvasRecord> records = GetRecords(players, realtimeSinceStartup, true);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== UI CANVAS CENSUS ===");
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("Scene-valid canvases: " + records.Count.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("Includes inactive scene objects; asset/prefab objects with invalid Scene are excluded.");
            sb.AppendLine();

            int i;
            for (i = 0; i < records.Count; i++)
            {
                UICanvasRecord r = records[i];
                sb.AppendLine("--- CANVAS[" + i.ToString(CultureInfo.InvariantCulture) + "] ---");
                sb.AppendLine("InstanceID: " + r.InstanceId.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("Owner: " + r.Owner);
                sb.AppendLine("Scene: " + r.SceneName);
                sb.AppendLine("Path: " + r.Path);
                sb.AppendLine("activeSelf=" + r.ActiveSelf + " activeInHierarchy=" + r.ActiveInHierarchy + " enabled=" + r.Enabled);
                sb.AppendLine("RenderMode: " + r.RenderMode);
                sb.AppendLine("SortingOrder: " + r.SortingOrder.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("ScaleFactor: " + r.ScaleFactor);
                sb.AppendLine("WorldCamera: " + r.WorldCameraPath);
                sb.AppendLine("WorldCameraRect: " + r.WorldCameraRect);
                sb.AppendLine("RootCanvas: " + r.RootCanvasPath);
                sb.AppendLine("Rect: " + r.Rect);
                sb.AppendLine("AnchoredPosition: " + r.AnchoredPosition);
                sb.AppendLine("SizeDelta: " + r.SizeDelta);
                sb.AppendLine("AnchorMin: " + r.AnchorMin + " AnchorMax: " + r.AnchorMax + " Pivot: " + r.Pivot);
                sb.AppendLine("CanvasScaler: " + r.CanvasScalerType);
                sb.AppendLine("ScaleMode: " + r.ScaleMode);
                sb.AppendLine("ReferenceResolution: " + r.ReferenceResolution);
                sb.AppendLine("ScreenMatchMode: " + r.ScreenMatchMode + " match=" + r.MatchWidthOrHeight);
                sb.AppendLine("Components: " + r.ComponentTypes);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public void Invalidate()
        {
            _records.Clear();
            _nextRefreshAt = 0f;
        }

        private void Refresh(IList<PlayerContext> players)
        {
            _records.Clear();
            Canvas[] canvases;
            try { canvases = Resources.FindObjectsOfTypeAll<Canvas>(); }
            catch { canvases = new Canvas[0]; }

            int i;
            for (i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null) continue;
                GameObject go;
                try { go = canvas.gameObject; }
                catch { continue; }
                if (go == null) continue;
                try { if (!go.scene.IsValid()) continue; }
                catch { continue; }

                _records.Add(Capture(canvas, players));
            }

            _records.Sort(CompareRecords);
        }

        private static UICanvasRecord Capture(Canvas canvas, IList<PlayerContext> players)
        {
            UICanvasRecord r = new UICanvasRecord();
            GameObject go = canvas.gameObject;
            r.InstanceId = SafeInstanceId(canvas);
            try { r.SceneName = go.scene.name; } catch { r.SceneName = "<unavailable>"; }
            r.Path = SafeReflection.GetHierarchyPath(canvas.transform, 48);
            try { r.ActiveSelf = go.activeSelf; } catch { }
            try { r.ActiveInHierarchy = go.activeInHierarchy; } catch { }
            try { r.Enabled = canvas.enabled; } catch { }
            try { r.RenderMode = canvas.renderMode.ToString(); } catch { r.RenderMode = "<unavailable>"; }
            try { r.SortingOrder = canvas.sortingOrder; } catch { }
            try { r.ScaleFactor = canvas.scaleFactor.ToString("0.###", CultureInfo.InvariantCulture); } catch { r.ScaleFactor = "<unavailable>"; }

            Camera worldCamera = null;
            try { worldCamera = canvas.worldCamera; } catch { }
            r.WorldCameraPath = worldCamera == null ? "<none>" : SafeReflection.GetHierarchyPath(worldCamera.transform, 48);
            r.WorldCameraRect = worldCamera == null ? "<none>" : ValueFormatter.FormatSimple(worldCamera.rect);
            r.Owner = ResolveOwner(worldCamera, players);

            try
            {
                Canvas root = canvas.rootCanvas;
                r.RootCanvasPath = root == null ? "<none>" : SafeReflection.GetHierarchyPath(root.transform, 48);
            }
            catch { r.RootCanvasPath = "<unavailable>"; }

            RectTransform rt = canvas.transform as RectTransform;
            if (rt != null)
            {
                try { r.Rect = ValueFormatter.FormatSimple(rt.rect); } catch { r.Rect = "<unavailable>"; }
                r.AnchoredPosition = ValueFormatter.FormatSimple(rt.anchoredPosition);
                r.SizeDelta = ValueFormatter.FormatSimple(rt.sizeDelta);
                r.AnchorMin = ValueFormatter.FormatSimple(rt.anchorMin);
                r.AnchorMax = ValueFormatter.FormatSimple(rt.anchorMax);
                r.Pivot = ValueFormatter.FormatSimple(rt.pivot);
            }
            else
            {
                r.Rect = r.AnchoredPosition = r.SizeDelta = r.AnchorMin = r.AnchorMax = r.Pivot = "<not RectTransform>";
            }

            CanvasScaler scaler = null;
            try { scaler = go.GetComponent<CanvasScaler>(); } catch { }
            if (scaler != null)
            {
                r.CanvasScalerType = scaler.GetType().FullName;
                try { r.ScaleMode = scaler.uiScaleMode.ToString(); } catch { r.ScaleMode = "<unavailable>"; }
                try { r.ReferenceResolution = ValueFormatter.FormatSimple(scaler.referenceResolution); } catch { r.ReferenceResolution = "<unavailable>"; }
                try { r.ScreenMatchMode = scaler.screenMatchMode.ToString(); } catch { r.ScreenMatchMode = "<unavailable>"; }
                try { r.MatchWidthOrHeight = scaler.matchWidthOrHeight.ToString("0.###", CultureInfo.InvariantCulture); } catch { r.MatchWidthOrHeight = "<unavailable>"; }
            }
            else
            {
                r.CanvasScalerType = "<none>";
                r.ScaleMode = r.ReferenceResolution = r.ScreenMatchMode = r.MatchWidthOrHeight = "<none>";
            }

            r.ComponentTypes = SafeReflection.GetComponentTypeSummary(go, 16);
            return r;
        }

        private static string ResolveOwner(Camera worldCamera, IList<PlayerContext> players)
        {
            if (players == null || players.Count == 0) return "<unowned>";
            int i;
            for (i = 0; i < players.Count; i++)
            {
                PlayerContext player = players[i];
                if (player == null || player.Camera == null) continue;
                if (worldCamera == player.Camera) return player.DisplayName;
            }

            if (worldCamera != null)
            {
                for (i = 0; i < players.Count; i++)
                {
                    PlayerContext player = players[i];
                    if (player == null || player.Camera == null) continue;
                    if (ApproximatelySameRect(worldCamera.rect, player.Camera.rect)) return player.DisplayName + " (viewport-match)";
                }
            }

            return "<unowned>";
        }

        private static bool ApproximatelySameRect(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) < 0.001f &&
                   Mathf.Abs(a.y - b.y) < 0.001f &&
                   Mathf.Abs(a.width - b.width) < 0.001f &&
                   Mathf.Abs(a.height - b.height) < 0.001f;
        }

        private static int CompareRecords(UICanvasRecord a, UICanvasRecord b)
        {
            int owner = string.Compare(a.Owner, b.Owner, StringComparison.Ordinal);
            if (owner != 0) return owner;
            return string.Compare(a.Path, b.Path, StringComparison.Ordinal);
        }

        private static int SafeInstanceId(UnityEngine.Object obj)
        {
            try { return obj == null ? 0 : obj.GetInstanceID(); }
            catch { return 0; }
        }

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? string.Empty;
            return text.Substring(0, max - 3) + "...";
        }
    }

    internal sealed class UICanvasRecord
    {
        public int InstanceId;
        public string Owner;
        public string SceneName;
        public string Path;
        public bool ActiveSelf;
        public bool ActiveInHierarchy;
        public bool Enabled;
        public string RenderMode;
        public int SortingOrder;
        public string ScaleFactor;
        public string WorldCameraPath;
        public string WorldCameraRect;
        public string RootCanvasPath;
        public string Rect;
        public string AnchoredPosition;
        public string SizeDelta;
        public string AnchorMin;
        public string AnchorMax;
        public string Pivot;
        public string CanvasScalerType;
        public string ScaleMode;
        public string ReferenceResolution;
        public string ScreenMatchMode;
        public string MatchWidthOrHeight;
        public string ComponentTypes;
    }
}
