using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

using StrandedDeepDiagnostics.Adapters.BamEx;

namespace StrandedDeepDiagnostics.Audio
{
    internal sealed class AudioDspTraceController
    {
        private readonly Harmony _harmony = new Harmony("com.bamex.strandeddeep.diagnostics.audio");
        private MethodBase _patchedMethod;
        private Type _processorType;
        private string _lastResolution = "not scanned";

        public bool Active
        {
            get { return _patchedMethod != null; }
        }

        public Type ProcessorType
        {
            get { return _processorType; }
        }

        public string LastResolution
        {
            get { return _lastResolution ?? string.Empty; }
        }

        public string PatchedMethodName
        {
            get
            {
                if (_patchedMethod == null) return "<none>";
                try { return (_patchedMethod.DeclaringType == null ? "<type>" : _patchedMethod.DeclaringType.FullName) + "." + _patchedMethod.Name; }
                catch { return "<method unavailable>"; }
            }
        }

        public string Toggle()
        {
            if (Active)
            {
                Disable();
                return "AUDIO DSP trace OFF";
            }

            int count = Enable();
            return count == 0 ? "AUDIO DSP trace unavailable: " + LastResolution : "AUDIO DSP trace ON: " + PatchedMethodName;
        }

        public int Enable()
        {
            Disable();
            _processorType = BoomboxAudioAdapter.FindProcessorType();
            if (_processorType == null)
            {
                _lastResolution = "SplitStereoProcessor type not found in loaded assemblies";
                return 0;
            }

            MethodInfo method = null;
            try
            {
                method = _processorType.GetMethod(
                    "OnAudioFilterRead",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(float[]), typeof(int) },
                    null);
            }
            catch (Exception ex)
            {
                _lastResolution = "method lookup failed: " + ex.GetType().Name;
            }

            if (method == null)
            {
                _lastResolution = _processorType.FullName + ".OnAudioFilterRead(float[], int) not found";
                return 0;
            }

            MethodInfo prefixMethod = typeof(AudioDspPatchBridge).GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
            MethodInfo postfixMethod = typeof(AudioDspPatchBridge).GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
            if (prefixMethod == null || postfixMethod == null)
            {
                _lastResolution = "audio patch bridge methods unavailable";
                return 0;
            }

            try
            {
                AudioDspTelemetry.Reset();
                _harmony.Patch(method, new HarmonyMethod(prefixMethod), new HarmonyMethod(postfixMethod), null, null, null);
                _patchedMethod = method;
                AudioDspTelemetry.Enabled = true;
                _lastResolution = "patched " + PatchedMethodName;
                return 1;
            }
            catch (Exception ex)
            {
                AudioDspTelemetry.Enabled = false;
                _patchedMethod = null;
                _lastResolution = "patch failed: " + ex.GetType().Name + ": " + ex.Message;
                return 0;
            }
        }

        public void Disable()
        {
            AudioDspTelemetry.Enabled = false;
            if (_patchedMethod != null)
            {
                try { _harmony.UnpatchSelf(); } catch { }
            }
            _patchedMethod = null;
        }
    }

    public static class AudioDspPatchBridge
    {
        [ThreadStatic]
        private static bool _armed;
        [ThreadStatic]
        private static long _startTicks;
        [ThreadStatic]
        private static float _inputPeak;
        [ThreadStatic]
        private static float _inputRms;
        [ThreadStatic]
        private static int _bufferLength;
        [ThreadStatic]
        private static int _channels;

        public static void Prefix(float[] __0, int __1)
        {
            if (!AudioDspTelemetry.Enabled)
            {
                _armed = false;
                return;
            }

            _armed = true;
            _bufferLength = __0 == null ? 0 : __0.Length;
            _channels = __1;
            ComputeMetrics(__0, out _inputPeak, out _inputRms);
            _startTicks = Stopwatch.GetTimestamp();
        }

        public static void Postfix(float[] __0, int __1)
        {
            if (!_armed || !AudioDspTelemetry.Enabled) return;

            long endTicks = Stopwatch.GetTimestamp();
            long durationTicks = endTicks - _startTicks;
            float outputPeak;
            float outputRms;
            ComputeMetrics(__0, out outputPeak, out outputRms);

            AudioDspTelemetry.Record(
                _startTicks,
                durationTicks,
                _bufferLength,
                _channels,
                _inputPeak,
                _inputRms,
                outputPeak,
                outputRms);

            _armed = false;
        }

        private static void ComputeMetrics(float[] data, out float peak, out float rms)
        {
            peak = 0f;
            rms = 0f;
            if (data == null || data.Length == 0) return;

            double sumSquares = 0.0;
            int i;
            for (i = 0; i < data.Length; i++)
            {
                float sample = data[i];
                float abs = sample < 0f ? -sample : sample;
                if (abs > peak) peak = abs;
                sumSquares += (double)sample * (double)sample;
            }
            rms = (float)Math.Sqrt(sumSquares / (double)data.Length);
        }
    }
}
