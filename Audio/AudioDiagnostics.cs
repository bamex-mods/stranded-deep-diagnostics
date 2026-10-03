using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;
using StrandedDeepDiagnostics.Players;

using StrandedDeepDiagnostics.Adapters.BamEx;

namespace StrandedDeepDiagnostics.Audio
{
    internal struct AudioMainSample
    {
        public float Realtime;
        public int Frame;
        public float FrameMs;
        public double DspTime;

        public bool SourceResolved;
        public int SourceInstanceId;
        public bool IsPlaying;
        public bool IsVirtual;
        public int TimeSamples;
        public int ClipSamples;
        public int ClipFrequency;
        public int ClipChannels;
        public int LoadState;
        public int Priority;
        public float Pitch;
        public float Volume;

        public double DeltaDsp;
        public double ExpectedAdvance;
        public int ActualAdvance;
        public bool PlayheadStall;

        public int TotalSources;
        public int EnabledSources;
        public int PlayingSources;
        public int VirtualSources;
        public int RealSources;
        public int PlayingNearP1;
        public int PlayingNearP2;

        public bool P1Moving;
        public bool P2Moving;
        public float P1Speed;
        public float P2Speed;

        public int Gc0;
        public int Gc1;
        public int Gc2;
        public long TotalMemory;

        public int DspCallbackSequence;
        public float DspCallbackGapMs;
        public float DspProcessorMs;
        public float DspInputRms;
        public float DspOutputRms;
    }

    internal sealed class AudioDiagnostics
    {
        private const int MainCapacity = 512;
        private const float SampleInterval = 0.04f;
        private const float VoiceScanInterval = 0.25f;
        private const float DiscoveryInterval = 1.0f;
        private const float IncidentBeforeSeconds = 5.0f;
        private const float IncidentAfterSeconds = 5.0f;

        private readonly AudioMainSample[] _main = new AudioMainSample[MainCapacity];
        private int _mainWrite;
        private int _mainCount;

        private readonly AudioDspTraceController _dspTrace = new AudioDspTraceController();
        private AudioSource _source;
        private Component _processor;
        private string _sourceResolution = "not scanned";
        private float _nextDiscoveryAt;
        private float _nextSampleAt;
        private float _nextVoiceScanAt;

        private int _voiceTotal;
        private int _voiceEnabled;
        private int _voicePlaying;
        private int _voiceVirtual;
        private int _voiceReal;
        private int _voiceNearP1;
        private int _voiceNearP2;

        private int _gc0;
        private int _gc1;
        private int _gc2;
        private long _totalMemory;

        private bool _hasLastSourceSample;
        private AudioMainSample _lastSourceSample;

        private bool _hasP1Position;
        private bool _hasP2Position;
        private Vector3 _lastP1Position;
        private Vector3 _lastP2Position;
        private float _lastMotionAt;
        private float _p1Speed;
        private float _p2Speed;

        private readonly int _outputSampleRate;
        private readonly int _dspBufferLength;
        private readonly int _dspNumBuffers;
        private readonly float _dspBufferMs;

        private bool _incidentPending;
        private float _incidentRealtime;
        private long _incidentStopwatchTicks;
        private string _incidentSourceDescriptor;
        private int _incidentSerial;

        public AudioDiagnostics()
        {
            int bufferLength = 0;
            int numBuffers = 0;
            try { UnityEngine.AudioSettings.GetDSPBufferSize(out bufferLength, out numBuffers); }
            catch { }
            _dspBufferLength = bufferLength;
            _dspNumBuffers = numBuffers;
            try { _outputSampleRate = UnityEngine.AudioSettings.outputSampleRate; }
            catch { _outputSampleRate = 0; }
            _dspBufferMs = _outputSampleRate > 0 && _dspBufferLength > 0
                ? ((float)_dspBufferLength / (float)_outputSampleRate) * 1000f
                : 0f;
        }

        public bool DspTraceActive
        {
            get { return _dspTrace.Active; }
        }

        public bool IncidentPending
        {
            get { return _incidentPending; }
        }

        public bool NeedsMonitoring
        {
            get { return _dspTrace.Active || _incidentPending; }
        }

        public string ToggleDspTrace()
        {
            return _dspTrace.Toggle();
        }

        public void DisableTrace()
        {
            _dspTrace.Disable();
        }

        public void CancelIncident()
        {
            _incidentPending = false;
        }

        public void InvalidateSceneState()
        {
            _source = null;
            _processor = null;
            _sourceResolution = "scene changed; awaiting rediscovery";
            _nextDiscoveryAt = 0f;
            _hasLastSourceSample = false;
            _hasP1Position = false;
            _hasP2Position = false;
            _lastMotionAt = 0f;
            _p1Speed = 0f;
            _p2Speed = 0f;
            _incidentPending = false;
            _dspTrace.Disable();
        }

        public void Update(IList<PlayerContext> players)
        {
            float now = Time.realtimeSinceStartup;

            if (_source == null || now >= _nextDiscoveryAt)
            {
                ResolveSource();
                _nextDiscoveryAt = now + DiscoveryInterval;
            }

            UpdateMotion(players, now);

            if (now >= _nextVoiceScanAt)
            {
                ScanVoices(players);
                ScanGc();
                _nextVoiceScanAt = now + VoiceScanInterval;
            }

            if (now >= _nextSampleAt)
            {
                CaptureMainSample(now);
                _nextSampleAt = now + SampleInterval;
            }
        }

        public string MarkIncident()
        {
            _incidentPending = true;
            _incidentRealtime = Time.realtimeSinceStartup;
            _incidentStopwatchTicks = Stopwatch.GetTimestamp();
            _incidentSerial++;
            _incidentSourceDescriptor = BuildSourceDescriptor();
            return "AUDIO incident marked; capturing +" + IncidentAfterSeconds.ToString("0", CultureInfo.InvariantCulture) + "s";
        }

        public bool TryCompleteIncident(out string report)
        {
            report = null;
            if (!_incidentPending) return false;
            if (Time.realtimeSinceStartup - _incidentRealtime < IncidentAfterSeconds) return false;

            report = BuildIncidentReport();
            _incidentPending = false;
            return true;
        }

        public string[] DescribeOverlay()
        {
            List<string> lines = new List<string>();
            lines.Add("AUDIO source=" + DescribeSourceShort() + " | adapter=BamEx.Boombox(optional)");
            lines.Add("RESOLVE " + Trim(_sourceResolution, 72));

            if (_source != null)
            {
                try
                {
                    AudioClip clip = _source.clip;
                    lines.Add("PLAY    playing=" + _source.isPlaying + " virtual=" + _source.isVirtual + " samples=" + _source.timeSamples);
                    lines.Add("CLIP    " + (clip == null ? "<none>" : Trim(clip.name, 30)) + " load=" + (clip == null ? "<none>" : clip.loadState.ToString()) + " priority=" + _source.priority);
                }
                catch
                {
                    lines.Add("PLAY    <source became unavailable>");
                }
            }

            lines.Add("VOICES  playing=" + _voicePlaying + " real=" + _voiceReal + " virtual=" + _voiceVirtual + " total=" + _voiceTotal);
            lines.Add("PLAYERS P1 " + MotionText(_p1Speed) + " nearVoices=" + _voiceNearP1 + " | P2 " + MotionText(_p2Speed) + " nearVoices=" + _voiceNearP2);
            lines.Add("DSP CFG rate=" + _outputSampleRate + " buffer=" + _dspBufferLength + "x" + _dspNumBuffers + " budget=" + _dspBufferMs.ToString("0.00", CultureInfo.InvariantCulture) + "ms");

            AudioDspRecord dsp;
            if (AudioDspTelemetry.TryGetLatest(out dsp))
            {
                lines.Add("DSP CB  seq=" + dsp.Sequence + " gap=" + TicksToMs(dsp.GapTicks).ToString("0.00", CultureInfo.InvariantCulture) + "ms proc=" + TicksToMs(dsp.DurationTicks).ToString("0.000", CultureInfo.InvariantCulture) + "ms");
                lines.Add("RMS     in=" + dsp.InputRms.ToString("0.0000", CultureInfo.InvariantCulture) + " out=" + dsp.OutputRms.ToString("0.0000", CultureInfo.InvariantCulture));
            }
            else
            {
                lines.Add("DSP CB  <no callback telemetry>");
            }

            lines.Add("TRACE   " + (_dspTrace.Active ? "ON " + Trim(_dspTrace.PatchedMethodName, 52) : "OFF; F11 arms optional BamEx Boombox DSP trace"));
            lines.Add("GC      " + _gc0 + "/" + _gc1 + "/" + _gc2 + " mem=" + (_totalMemory / (1024L * 1024L)) + "MB");
            lines.Add(_incidentPending
                ? "INCIDENT marked; waiting for +5s post-window"
                : "F12 = mark audible gap; report contains ~5s before + 5s after");
            return lines.ToArray();
        }

        public string RenderStatusReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== AUDIO DIAGNOSTICS STATUS ===");
            sb.AppendLine("Specialized source/DSP adapter: BamEx Boombox (optional)");
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("Source: " + BuildSourceDescriptor());
            sb.AppendLine("Resolution: " + _sourceResolution);
            sb.AppendLine("DSP trace active: " + _dspTrace.Active);
            sb.AppendLine("DSP method: " + _dspTrace.PatchedMethodName);
            sb.AppendLine("DSP resolution: " + _dspTrace.LastResolution);
            sb.AppendLine("OutputSampleRate: " + _outputSampleRate);
            sb.AppendLine("DSP bufferLength: " + _dspBufferLength);
            sb.AppendLine("DSP numBuffers: " + _dspNumBuffers);
            sb.AppendLine("DSP buffer duration ms: " + _dspBufferMs.ToString("0.000", CultureInfo.InvariantCulture));
            sb.AppendLine("Voices total/enabled/playing/virtual/real: " + _voiceTotal + "/" + _voiceEnabled + "/" + _voicePlaying + "/" + _voiceVirtual + "/" + _voiceReal);
            sb.AppendLine("Near P1/P2 playing voices: " + _voiceNearP1 + "/" + _voiceNearP2);
            sb.AppendLine("GC collections 0/1/2: " + _gc0 + "/" + _gc1 + "/" + _gc2);
            sb.AppendLine("GC total memory: " + _totalMemory);
            return sb.ToString();
        }

        private void ResolveSource()
        {
            if (_source != null)
            {
                try
                {
                    if (_source.gameObject != null && _source.gameObject.scene.IsValid()) return;
                }
                catch { }
            }

            Component processor;
            string reason;
            AudioSource source = BoomboxAudioAdapter.ResolveSource(out processor, out reason);
            if (source != _source)
            {
                _hasLastSourceSample = false;
            }
            _source = source;
            _processor = processor;
            _sourceResolution = reason;
        }

        private void CaptureMainSample(float now)
        {
            AudioMainSample sample = new AudioMainSample();
            sample.Realtime = now;
            sample.Frame = Time.frameCount;
            sample.FrameMs = Time.unscaledDeltaTime * 1000f;
            try { sample.DspTime = UnityEngine.AudioSettings.dspTime; } catch { sample.DspTime = 0.0; }

            AudioSource source = _source;
            if (source != null)
            {
                try
                {
                    sample.SourceResolved = true;
                    sample.SourceInstanceId = source.GetInstanceID();
                    sample.IsPlaying = source.isPlaying;
                    sample.IsVirtual = source.isVirtual;
                    sample.TimeSamples = source.timeSamples;
                    sample.Priority = source.priority;
                    sample.Pitch = source.pitch;
                    sample.Volume = source.volume;
                    AudioClip clip = source.clip;
                    if (clip != null)
                    {
                        sample.ClipSamples = clip.samples;
                        sample.ClipFrequency = clip.frequency;
                        sample.ClipChannels = clip.channels;
                        sample.LoadState = (int)clip.loadState;
                    }
                }
                catch
                {
                    sample.SourceResolved = false;
                }
            }

            if (sample.SourceResolved && _hasLastSourceSample && _lastSourceSample.SourceResolved && sample.SourceInstanceId == _lastSourceSample.SourceInstanceId)
            {
                sample.DeltaDsp = sample.DspTime - _lastSourceSample.DspTime;
                if (sample.DeltaDsp < 0.0) sample.DeltaDsp = 0.0;
                sample.ExpectedAdvance = sample.DeltaDsp * (double)sample.ClipFrequency * Math.Abs((double)sample.Pitch);
                int actual = sample.TimeSamples - _lastSourceSample.TimeSamples;
                if (actual < 0 && sample.ClipSamples > 0) actual += sample.ClipSamples;
                sample.ActualAdvance = actual;
                sample.PlayheadStall = sample.IsPlaying && sample.ExpectedAdvance >= Math.Max(64.0, sample.ClipFrequency * 0.015) && sample.ActualAdvance < sample.ExpectedAdvance * 0.15;
            }

            if (sample.SourceResolved)
            {
                _lastSourceSample = sample;
                _hasLastSourceSample = true;
            }
            else
            {
                _hasLastSourceSample = false;
            }

            sample.TotalSources = _voiceTotal;
            sample.EnabledSources = _voiceEnabled;
            sample.PlayingSources = _voicePlaying;
            sample.VirtualSources = _voiceVirtual;
            sample.RealSources = _voiceReal;
            sample.PlayingNearP1 = _voiceNearP1;
            sample.PlayingNearP2 = _voiceNearP2;
            sample.P1Speed = _p1Speed;
            sample.P2Speed = _p2Speed;
            sample.P1Moving = _p1Speed > 0.08f;
            sample.P2Moving = _p2Speed > 0.08f;
            sample.Gc0 = _gc0;
            sample.Gc1 = _gc1;
            sample.Gc2 = _gc2;
            sample.TotalMemory = _totalMemory;

            AudioDspRecord dsp;
            if (AudioDspTelemetry.TryGetLatest(out dsp))
            {
                sample.DspCallbackSequence = dsp.Sequence;
                sample.DspCallbackGapMs = TicksToMs(dsp.GapTicks);
                sample.DspProcessorMs = TicksToMs(dsp.DurationTicks);
                sample.DspInputRms = dsp.InputRms;
                sample.DspOutputRms = dsp.OutputRms;
            }

            _main[_mainWrite] = sample;
            _mainWrite = (_mainWrite + 1) % MainCapacity;
            if (_mainCount < MainCapacity) _mainCount++;
        }

        private void ScanVoices(IList<PlayerContext> players)
        {
            AudioSource[] sources;
            try { sources = Resources.FindObjectsOfTypeAll<AudioSource>(); }
            catch { sources = new AudioSource[0]; }

            Vector3 p1;
            Vector3 p2;
            bool hasP1 = TryGetPlayerPosition(players, 0, out p1);
            bool hasP2 = TryGetPlayerPosition(players, 1, out p2);

            int total = 0;
            int enabled = 0;
            int playing = 0;
            int virtualCount = 0;
            int real = 0;
            int near1 = 0;
            int near2 = 0;

            int i;
            for (i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (!AudioDiscovery.IsRuntimeComponent(source)) continue;
                total++;
                bool isEnabled = false;
                bool isPlaying = false;
                bool isVirtual = false;
                try
                {
                    isEnabled = source.enabled;
                    isPlaying = source.isPlaying;
                    isVirtual = source.isVirtual;
                }
                catch { }

                if (isEnabled) enabled++;
                if (!isPlaying) continue;
                playing++;
                if (isVirtual) virtualCount++; else real++;

                Vector3 sourcePosition;
                try { sourcePosition = source.transform.position; }
                catch { continue; }
                if (hasP1 && (sourcePosition - p1).sqrMagnitude <= 100f) near1++;
                if (hasP2 && (sourcePosition - p2).sqrMagnitude <= 100f) near2++;
            }

            _voiceTotal = total;
            _voiceEnabled = enabled;
            _voicePlaying = playing;
            _voiceVirtual = virtualCount;
            _voiceReal = real;
            _voiceNearP1 = near1;
            _voiceNearP2 = near2;
        }

        private void ScanGc()
        {
            try
            {
                _gc0 = GC.CollectionCount(0);
                _gc1 = GC.CollectionCount(1);
                _gc2 = GC.CollectionCount(2);
                _totalMemory = GC.GetTotalMemory(false);
            }
            catch { }
        }

        private void UpdateMotion(IList<PlayerContext> players, float now)
        {
            Vector3 p1;
            Vector3 p2;
            bool hasP1 = TryGetPlayerPosition(players, 0, out p1);
            bool hasP2 = TryGetPlayerPosition(players, 1, out p2);
            float delta = _lastMotionAt <= 0f ? 0f : now - _lastMotionAt;

            if (hasP1)
            {
                if (_hasP1Position && delta > 0.001f) _p1Speed = (p1 - _lastP1Position).magnitude / delta;
                _lastP1Position = p1;
                _hasP1Position = true;
            }
            else
            {
                _hasP1Position = false;
                _p1Speed = 0f;
            }

            if (hasP2)
            {
                if (_hasP2Position && delta > 0.001f) _p2Speed = (p2 - _lastP2Position).magnitude / delta;
                _lastP2Position = p2;
                _hasP2Position = true;
            }
            else
            {
                _hasP2Position = false;
                _p2Speed = 0f;
            }

            _lastMotionAt = now;
        }

        private static bool TryGetPlayerPosition(IList<PlayerContext> players, int index, out Vector3 position)
        {
            position = Vector3.zero;
            if (players == null || index < 0 || index >= players.Count) return false;
            PlayerContext player = players[index];
            if (player == null || player.PlayerObject == null) return false;
            try
            {
                Component component = player.PlayerObject as Component;
                if (component != null)
                {
                    position = component.transform.position;
                    return true;
                }
                GameObject go = player.PlayerObject as GameObject;
                if (go != null)
                {
                    position = go.transform.position;
                    return true;
                }
            }
            catch { }
            return false;
        }

        private string BuildIncidentReport()
        {
            float start = _incidentRealtime - IncidentBeforeSeconds;
            float end = _incidentRealtime + IncidentAfterSeconds;
            List<AudioMainSample> main = GetMainWindow(start, end);
            List<AudioDspRecord> dsp = AudioDspTelemetry.Snapshot();

            int playheadStalls = 0;
            int virtualTransitions = 0;
            bool virtualObserved = false;
            bool previousVirtualKnown = false;
            bool previousVirtual = false;
            float maxFrameMs = 0f;
            int maxPlaying = 0;
            int maxVirtual = 0;
            int gc0Start = -1;
            int gc1Start = -1;
            int gc2Start = -1;
            int gc0End = -1;
            int gc1End = -1;
            int gc2End = -1;

            int i;
            for (i = 0; i < main.Count; i++)
            {
                AudioMainSample s = main[i];
                if (s.PlayheadStall) playheadStalls++;
                if (s.FrameMs > maxFrameMs) maxFrameMs = s.FrameMs;
                if (s.PlayingSources > maxPlaying) maxPlaying = s.PlayingSources;
                if (s.VirtualSources > maxVirtual) maxVirtual = s.VirtualSources;
                if (s.SourceResolved)
                {
                    if (s.IsVirtual) virtualObserved = true;
                    if (previousVirtualKnown && previousVirtual != s.IsVirtual) virtualTransitions++;
                    previousVirtual = s.IsVirtual;
                    previousVirtualKnown = true;
                }
                if (gc0Start < 0)
                {
                    gc0Start = s.Gc0; gc1Start = s.Gc1; gc2Start = s.Gc2;
                }
                gc0End = s.Gc0; gc1End = s.Gc1; gc2End = s.Gc2;
            }

            float maxGapMs = 0f;
            float maxProcessorMs = 0f;
            int dspWindowCount = 0;
            int dspOutputDropCount = 0;
            int dspInputSilenceCount = 0;
            long frequency = AudioDspTelemetry.StopwatchFrequency;
            for (i = 0; i < dsp.Count; i++)
            {
                AudioDspRecord r = dsp[i];
                double rel = frequency <= 0 ? 0.0 : ((double)(r.StartTicks - _incidentStopwatchTicks) / (double)frequency);
                if (rel < -IncidentBeforeSeconds || rel > IncidentAfterSeconds) continue;
                dspWindowCount++;
                float gap = TicksToMs(r.GapTicks);
                float proc = TicksToMs(r.DurationTicks);
                if (gap > maxGapMs) maxGapMs = gap;
                if (proc > maxProcessorMs) maxProcessorMs = proc;
                if (r.InputRms > 0.003f && r.OutputRms < Math.Max(0.0002f, r.InputRms * 0.05f)) dspOutputDropCount++;
                if (r.InputRms < 0.0002f && r.OutputRms < 0.0002f) dspInputSilenceCount++;
            }

            bool callbackGap = _dspBufferMs > 0f ? maxGapMs > _dspBufferMs * 2.5f : maxGapMs > 60f;
            bool processorOverBudget = _dspBufferMs > 0f ? maxProcessorMs > _dspBufferMs : maxProcessorMs > 20f;
            bool frameStall = maxFrameMs > Math.Max(50f, _dspBufferMs * 3f);
            bool gcActivity = gc0Start >= 0 && (gc0End > gc0Start || gc1End > gc1Start || gc2End > gc2Start);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== AUDIO INCIDENT ===");
            sb.AppendLine("Incident serial: " + _incidentSerial);
            sb.AppendLine("UTC generated: " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("User mark realtime: " + _incidentRealtime.ToString("0.000", CultureInfo.InvariantCulture));
            sb.AppendLine("Window: -5.0s .. +5.0s");
            sb.AppendLine();
            sb.AppendLine("=== SOURCE ===");
            sb.AppendLine(_incidentSourceDescriptor ?? "<unavailable>");
            sb.AppendLine("Resolution: " + _sourceResolution);
            sb.AppendLine();
            sb.AppendLine("=== DSP CONFIG ===");
            sb.AppendLine("OutputSampleRate: " + _outputSampleRate);
            sb.AppendLine("BufferLength: " + _dspBufferLength);
            sb.AppendLine("NumBuffers: " + _dspNumBuffers);
            sb.AppendLine("Single buffer duration ms: " + _dspBufferMs.ToString("0.000", CultureInfo.InvariantCulture));
            sb.AppendLine("DSP trace active at report time: " + _dspTrace.Active);
            sb.AppendLine("Patched method: " + _dspTrace.PatchedMethodName);
            sb.AppendLine();
            sb.AppendLine("=== EVIDENCE SUMMARY ===");
            sb.AppendLine("Main samples: " + main.Count);
            sb.AppendLine("DSP callbacks in window: " + dspWindowCount);
            sb.AppendLine("Boombox virtual observed: " + virtualObserved);
            sb.AppendLine("Virtual state transitions: " + virtualTransitions);
            sb.AppendLine("Playhead stall samples: " + playheadStalls);
            sb.AppendLine("Max callback gap ms: " + maxGapMs.ToString("0.000", CultureInfo.InvariantCulture));
            sb.AppendLine("Max processor duration ms: " + maxProcessorMs.ToString("0.000", CultureInfo.InvariantCulture));
            sb.AppendLine("DSP input-present/output-drop callbacks: " + dspOutputDropCount);
            sb.AppendLine("DSP input+output silence callbacks: " + dspInputSilenceCount);
            sb.AppendLine("Max frame ms: " + maxFrameMs.ToString("0.000", CultureInfo.InvariantCulture));
            sb.AppendLine("Max playing voices: " + maxPlaying);
            sb.AppendLine("Max virtual voices: " + maxVirtual);
            sb.AppendLine("GC collections changed: " + gcActivity);
            sb.AppendLine();
            sb.AppendLine("=== DIAGNOSTIC FLAGS ===");
            sb.AppendLine("VOICE_VIRTUALIZATION_EVIDENCE: " + (virtualObserved || virtualTransitions > 0 ? "YES" : "NO"));
            sb.AppendLine("SOURCE_PLAYHEAD_STALL_EVIDENCE: " + (playheadStalls > 0 ? "YES" : "NO"));
            sb.AppendLine("DSP_OUTPUT_DROP_EVIDENCE: " + (dspOutputDropCount > 0 ? "YES" : "NO"));
            sb.AppendLine("AUDIO_CALLBACK_GAP_EVIDENCE: " + (callbackGap ? "YES" : "NO"));
            sb.AppendLine("DSP_PROCESSOR_OVER_BUDGET: " + (processorOverBudget ? "YES" : "NO"));
            sb.AppendLine("FRAME_STALL_EVIDENCE: " + (frameStall ? "YES" : "NO"));
            sb.AppendLine("GC_ACTIVITY_IN_WINDOW: " + (gcActivity ? "YES" : "NO"));
            sb.AppendLine("NOTE: flags are evidence, not an automatic root-cause verdict.");
            sb.AppendLine();
            sb.AppendLine("=== MAIN THREAD SAMPLES ===");
            sb.AppendLine("rel_s frame frame_ms dsp_s playing virtual timeSamples expAdvance actualAdvance stall voicesPlaying voicesVirtual p1Speed p2Speed gc0 gc1 gc2 cbSeq cbGapMs procMs inRms outRms");
            for (i = 0; i < main.Count; i++)
            {
                AudioMainSample s = main[i];
                float rel = s.Realtime - _incidentRealtime;
                sb.Append(rel.ToString("0.000", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(s.Frame).Append(' ')
                  .Append(s.FrameMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(s.DspTime.ToString("0.000000", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(s.IsPlaying ? "1" : "0").Append(' ')
                  .Append(s.IsVirtual ? "1" : "0").Append(' ')
                  .Append(s.TimeSamples).Append(' ')
                  .Append(s.ExpectedAdvance.ToString("0.0", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(s.ActualAdvance).Append(' ')
                  .Append(s.PlayheadStall ? "1" : "0").Append(' ')
                  .Append(s.PlayingSources).Append(' ')
                  .Append(s.VirtualSources).Append(' ')
                  .Append(s.P1Speed.ToString("0.00", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(s.P2Speed.ToString("0.00", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(s.Gc0).Append(' ')
                  .Append(s.Gc1).Append(' ')
                  .Append(s.Gc2).Append(' ')
                  .Append(s.DspCallbackSequence).Append(' ')
                  .Append(s.DspCallbackGapMs.ToString("0.00", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(s.DspProcessorMs.ToString("0.000", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(s.DspInputRms.ToString("0.00000", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(s.DspOutputRms.ToString("0.00000", CultureInfo.InvariantCulture))
                  .AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("=== DSP CALLBACKS ===");
            sb.AppendLine("rel_s seq gap_ms processor_ms buffer channels inputPeak inputRms outputPeak outputRms");
            for (i = 0; i < dsp.Count; i++)
            {
                AudioDspRecord r = dsp[i];
                double rel = frequency <= 0 ? 0.0 : ((double)(r.StartTicks - _incidentStopwatchTicks) / (double)frequency);
                if (rel < -IncidentBeforeSeconds || rel > IncidentAfterSeconds) continue;
                sb.Append(rel.ToString("0.000000", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(r.Sequence).Append(' ')
                  .Append(TicksToMs(r.GapTicks).ToString("0.000", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(TicksToMs(r.DurationTicks).ToString("0.000", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(r.BufferLength).Append(' ')
                  .Append(r.Channels).Append(' ')
                  .Append(r.InputPeak.ToString("0.00000", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(r.InputRms.ToString("0.00000", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(r.OutputPeak.ToString("0.00000", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(r.OutputRms.ToString("0.00000", CultureInfo.InvariantCulture))
                  .AppendLine();
            }

            return sb.ToString();
        }

        private List<AudioMainSample> GetMainWindow(float start, float end)
        {
            List<AudioMainSample> result = new List<AudioMainSample>(_mainCount);
            int first = (_mainWrite - _mainCount + MainCapacity) % MainCapacity;
            int i;
            for (i = 0; i < _mainCount; i++)
            {
                AudioMainSample sample = _main[(first + i) % MainCapacity];
                if (sample.Realtime >= start && sample.Realtime <= end) result.Add(sample);
            }
            return result;
        }

        private string BuildSourceDescriptor()
        {
            AudioSource source = _source;
            if (source == null) return "Boombox AudioSource: <unresolved>";
            try
            {
                AudioClip clip = source.clip;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("GameObject: " + source.gameObject.name);
                sb.AppendLine("Path: " + AudioDiscovery.BuildPath(source.transform, 12));
                sb.AppendLine("InstanceID: " + source.GetInstanceID());
                sb.AppendLine("AudioSource.enabled: " + source.enabled);
                sb.AppendLine("AudioSource.isPlaying: " + source.isPlaying);
                sb.AppendLine("AudioSource.isVirtual: " + source.isVirtual);
                sb.AppendLine("AudioSource.time: " + source.time.ToString("0.000", CultureInfo.InvariantCulture));
                sb.AppendLine("AudioSource.timeSamples: " + source.timeSamples);
                sb.AppendLine("volume/mute/pitch/loop: " + source.volume.ToString("0.000", CultureInfo.InvariantCulture) + "/" + source.mute + "/" + source.pitch.ToString("0.000", CultureInfo.InvariantCulture) + "/" + source.loop);
                sb.AppendLine("priority: " + source.priority);
                sb.AppendLine("spatialBlend/panStereo/doppler/spread: " + source.spatialBlend.ToString("0.000", CultureInfo.InvariantCulture) + "/" + source.panStereo.ToString("0.000", CultureInfo.InvariantCulture) + "/" + source.dopplerLevel.ToString("0.000", CultureInfo.InvariantCulture) + "/" + source.spread.ToString("0.000", CultureInfo.InvariantCulture));
                sb.AppendLine("minDistance/maxDistance: " + source.minDistance.ToString("0.000", CultureInfo.InvariantCulture) + "/" + source.maxDistance.ToString("0.000", CultureInfo.InvariantCulture));
                sb.AppendLine("outputAudioMixerGroup: " + (source.outputAudioMixerGroup == null ? "<none>" : source.outputAudioMixerGroup.name));
                if (clip == null)
                {
                    sb.AppendLine("clip: <none>");
                }
                else
                {
                    sb.AppendLine("clip.name: " + clip.name);
                    sb.AppendLine("clip.length: " + clip.length.ToString("0.000", CultureInfo.InvariantCulture));
                    sb.AppendLine("clip.samples: " + clip.samples);
                    sb.AppendLine("clip.frequency: " + clip.frequency);
                    sb.AppendLine("clip.channels: " + clip.channels);
                    sb.AppendLine("clip.loadState: " + clip.loadState);
                }
                sb.AppendLine("SplitStereoProcessor: " + (_processor == null ? "<unresolved>" : _processor.GetType().FullName));
                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                return "Boombox AudioSource descriptor failed: " + ex.GetType().Name;
            }
        }

        private string DescribeSourceShort()
        {
            if (_source == null) return "<unresolved>";
            try { return Trim(_source.gameObject.name, 34) + " id=" + _source.GetInstanceID(); }
            catch { return "<unavailable>"; }
        }

        private static string MotionText(float speed)
        {
            return speed > 0.08f
                ? "MOVE " + speed.ToString("0.00", CultureInfo.InvariantCulture) + "m/s"
                : "IDLE";
        }

        private static float TicksToMs(long ticks)
        {
            long frequency = AudioDspTelemetry.StopwatchFrequency;
            if (ticks <= 0L || frequency <= 0L) return 0f;
            return (float)((double)ticks * 1000.0 / (double)frequency);
        }

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? string.Empty;
            return text.Substring(0, Math.Max(0, max - 3)) + "...";
        }
    }
}
