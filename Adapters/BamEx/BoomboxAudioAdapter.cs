using System;
using UnityEngine;
using StrandedDeepDiagnostics.Audio;

namespace StrandedDeepDiagnostics.Adapters.BamEx
{
    internal static class BoomboxAudioAdapter
    {
        public static Type FindProcessorType()
        {
            return AudioDiscovery.FindTypeBySimpleName("SplitStereoProcessor");
        }

        public static AudioSource ResolveSource(out Component processor, out string reason)
        {
            processor = null;
            reason = "unresolved";

            Type processorType = FindProcessorType();
            if (processorType != null)
            {
                UnityEngine.Object[] objects = new UnityEngine.Object[0];
                try { objects = Resources.FindObjectsOfTypeAll(processorType); }
                catch { }

                int i;
                for (i = 0; i < objects.Length; i++)
                {
                    Component component = objects[i] as Component;
                    if (!AudioDiscovery.IsRuntimeComponent(component)) continue;

                    AudioSource source = null;
                    try { source = component.GetComponent<AudioSource>(); } catch { }
                    if (source == null)
                    {
                        try { source = component.GetComponentInParent<AudioSource>(); } catch { }
                    }
                    if (source == null)
                    {
                        try { source = component.GetComponentInChildren<AudioSource>(true); } catch { }
                    }

                    if (source != null)
                    {
                        processor = component;
                        reason = "BamEx Boombox adapter: AudioSource colocated with " + processorType.FullName;
                        return source;
                    }
                }
            }

            AudioSource[] sources;
            try { sources = Resources.FindObjectsOfTypeAll<AudioSource>(); }
            catch { sources = new AudioSource[0]; }

            AudioSource best = null;
            int bestScore = 0;
            string bestReason = null;
            int s;
            for (s = 0; s < sources.Length; s++)
            {
                AudioSource source = sources[s];
                if (!AudioDiscovery.IsRuntimeComponent(source)) continue;
                int score = 0;
                string evidence = string.Empty;

                string goName = SafeName(source.gameObject);
                score += ScoreToken(goName, "BOOMBOX", 600, ref evidence);
                score += ScoreToken(goName, "RADIO", 350, ref evidence);
                score += ScoreToken(goName, "BROADCAST", 250, ref evidence);
                score += ScoreToken(goName, "STATION", 100, ref evidence);

                string path = AudioDiscovery.BuildPath(source.transform, 8);
                score += ScoreToken(path, "BOOMBOX", 500, ref evidence);
                score += ScoreToken(path, "RADIO", 250, ref evidence);

                AudioClip clip = null;
                try { clip = source.clip; } catch { }
                string clipName = clip == null ? string.Empty : SafeName(clip);
                score += ScoreToken(clipName, "BOOMBOX", 250, ref evidence);
                score += ScoreToken(clipName, "RADIO", 150, ref evidence);
                score += ScoreToken(clipName, "BROADCAST", 150, ref evidence);
                score += ScoreToken(clipName, "STATION", 80, ref evidence);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = source;
                    bestReason = "BamEx Boombox adapter heuristic score=" + score + " evidence=" + evidence;
                }
            }

            if (best != null && bestScore >= 150)
            {
                reason = bestReason;
                return best;
            }

            reason = processorType == null
                ? "BamEx Boombox adapter unavailable: SplitStereoProcessor not found and no strong radio source match"
                : "BamEx Boombox processor found but no colocated/strong AudioSource match";
            return null;
        }

        private static int ScoreToken(string text, string token, int value, ref string evidence)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0) return 0;
            if (!string.IsNullOrEmpty(evidence)) evidence += ",";
            evidence += token;
            return value;
        }

        private static string SafeName(UnityEngine.Object obj)
        {
            if (obj == null) return string.Empty;
            try { return obj.name ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
