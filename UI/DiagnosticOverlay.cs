using UnityEngine;

namespace StrandedDeepDiagnostics.UI
{
    internal sealed class DiagnosticOverlay
    {
        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;

        public void Draw(OverlaySnapshot snapshot)
        {
            if (snapshot == null || !snapshot.Visible || snapshot.Lines == null)
            {
                return;
            }

            EnsureStyles();

            Rect viewport = snapshot.ViewportRect;
            float availableWidth = Mathf.Max(260f, viewport.width - 24f);
            float maxWidth = Mathf.Min(760f, availableWidth);
            float width = ResolveWidth(snapshot.Lines, maxWidth);
            float contentWidth = Mathf.Max(220f, width - 20f);
            float height = ResolveHeight(snapshot.Lines, contentWidth);
            height = Mathf.Min(height, Mathf.Max(120f, viewport.height - 24f));

            Rect box = new Rect(viewport.x + 12f, viewport.y + 12f, width, height);
            GUI.Box(box, GUIContent.none, _boxStyle);

            float y = box.y + 8f;
            int i;
            for (i = 0; i < snapshot.Lines.Length; i++)
            {
                string text = snapshot.Lines[i] ?? string.Empty;
                GUIContent content = new GUIContent(text);
                float lineHeight = Mathf.Max(18f, _labelStyle.CalcHeight(content, contentWidth));

                if (y + lineHeight > box.yMax - 4f)
                {
                    break;
                }

                GUI.Label(new Rect(box.x + 10f, y, contentWidth, lineHeight), content, _labelStyle);
                y += lineHeight + 1f;
            }
        }

        private float ResolveWidth(string[] lines, float maxWidth)
        {
            float desired = 260f;
            int i;
            for (i = 0; i < lines.Length; i++)
            {
                string text = lines[i] ?? string.Empty;
                float lineWidth = _labelStyle.CalcSize(new GUIContent(text)).x + 20f;
                if (lineWidth > desired) desired = lineWidth;
            }

            return Mathf.Clamp(desired, 260f, maxWidth);
        }

        private float ResolveHeight(string[] lines, float contentWidth)
        {
            float height = 16f;
            int i;
            for (i = 0; i < lines.Length; i++)
            {
                string text = lines[i] ?? string.Empty;
                float lineHeight = Mathf.Max(18f, _labelStyle.CalcHeight(new GUIContent(text), contentWidth));
                height += lineHeight + 1f;
            }

            return height + 4f;
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null && _boxStyle != null)
            {
                return;
            }

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 14;
            _labelStyle.alignment = TextAnchor.UpperLeft;
            _labelStyle.wordWrap = true;
            _labelStyle.clipping = TextClipping.Clip;

            _boxStyle = new GUIStyle(GUI.skin.box);
        }
    }
}
