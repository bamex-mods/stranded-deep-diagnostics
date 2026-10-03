using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace StrandedDeepDiagnostics.Audio
{
    internal struct AudioDspRecord
    {
        public int Sequence;
        public long StartTicks;
        public long GapTicks;
        public long DurationTicks;
        public int BufferLength;
        public int Channels;
        public float InputPeak;
        public float InputRms;
        public float OutputPeak;
        public float OutputRms;
    }

    internal static class AudioDspTelemetry
    {
        private const int Capacity = 2048;
        private static readonly AudioDspRecord[] Records = new AudioDspRecord[Capacity];
        private static int _writeSequence;
        private static long _lastStartTicks;
        private static int _enabled;

        public static long StopwatchFrequency
        {
            get { return Stopwatch.Frequency; }
        }

        public static bool Enabled
        {
            get { return Interlocked.CompareExchange(ref _enabled, 0, 0) != 0; }
            set { Interlocked.Exchange(ref _enabled, value ? 1 : 0); }
        }

        public static int LatestSequence
        {
            get { return Interlocked.CompareExchange(ref _writeSequence, 0, 0); }
        }

        public static void Reset()
        {
            Enabled = false;
            Interlocked.Exchange(ref _lastStartTicks, 0L);
            Interlocked.Exchange(ref _writeSequence, 0);
            int i;
            for (i = 0; i < Records.Length; i++)
            {
                Interlocked.Exchange(ref Records[i].Sequence, 0);
            }
        }

        public static void Record(
            long startTicks,
            long durationTicks,
            int bufferLength,
            int channels,
            float inputPeak,
            float inputRms,
            float outputPeak,
            float outputRms)
        {
            if (!Enabled) return;

            long previousStart = Interlocked.Exchange(ref _lastStartTicks, startTicks);
            long gapTicks = previousStart <= 0L ? 0L : startTicks - previousStart;
            int sequence = Interlocked.Increment(ref _writeSequence);
            int index = sequence & (Capacity - 1);

            Interlocked.Exchange(ref Records[index].Sequence, -sequence);
            Records[index].StartTicks = startTicks;
            Records[index].GapTicks = gapTicks;
            Records[index].DurationTicks = durationTicks;
            Records[index].BufferLength = bufferLength;
            Records[index].Channels = channels;
            Records[index].InputPeak = inputPeak;
            Records[index].InputRms = inputRms;
            Records[index].OutputPeak = outputPeak;
            Records[index].OutputRms = outputRms;
            Thread.MemoryBarrier();
            Interlocked.Exchange(ref Records[index].Sequence, sequence);
        }

        public static List<AudioDspRecord> Snapshot()
        {
            List<AudioDspRecord> result = new List<AudioDspRecord>(Capacity);
            int i;
            for (i = 0; i < Records.Length; i++)
            {
                int sequence1 = Interlocked.CompareExchange(ref Records[i].Sequence, 0, 0);
                if (sequence1 <= 0) continue;

                Thread.MemoryBarrier();
                AudioDspRecord copy = Records[i];
                Thread.MemoryBarrier();
                int sequence2 = Interlocked.CompareExchange(ref Records[i].Sequence, 0, 0);
                if (sequence1 != sequence2 || sequence2 <= 0) continue;
                copy.Sequence = sequence2;
                result.Add(copy);
            }

            result.Sort(delegate(AudioDspRecord a, AudioDspRecord b)
            {
                return a.Sequence.CompareTo(b.Sequence);
            });
            return result;
        }

        public static bool TryGetLatest(out AudioDspRecord record)
        {
            record = new AudioDspRecord();
            int latest = LatestSequence;
            if (latest <= 0) return false;
            int index = latest & (Capacity - 1);
            int sequence1 = Interlocked.CompareExchange(ref Records[index].Sequence, 0, 0);
            if (sequence1 != latest) return false;
            Thread.MemoryBarrier();
            AudioDspRecord copy = Records[index];
            Thread.MemoryBarrier();
            int sequence2 = Interlocked.CompareExchange(ref Records[index].Sequence, 0, 0);
            if (sequence1 != sequence2 || sequence2 <= 0) return false;
            copy.Sequence = sequence2;
            record = copy;
            return true;
        }
    }
}
