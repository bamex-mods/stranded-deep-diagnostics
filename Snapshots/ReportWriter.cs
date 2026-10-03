using System;
using System.IO;
using BepInEx;
using StrandedDeepDiagnostics.Core;

namespace StrandedDeepDiagnostics.Snapshots
{
    internal sealed class ReportWriter
    {
        private readonly string _configuredRoot;
        private readonly CapabilityManifest _manifest;
        private string _sessionDirectory;
        private bool _sessionHeaderWritten;

        public ReportWriter(string configuredRoot, CapabilityManifest manifest)
        {
            _configuredRoot = configuredRoot;
            _manifest = manifest;
        }

        public string WriteSnapshot(DiagnosticSnapshot snapshot, bool deep)
        {
            string directory = EnsureSessionDirectory();
            EnsureSessionHeader(directory);

            string player = snapshot.Player == null ? "PX" : "P" + snapshot.Player.DisplayIndex;
            string mode = deep ? "deep" : "object";
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string fileName = stamp + "-" + mode + "-" + player + ".txt";
            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path, SnapshotTextWriter.Render(snapshot, deep));
            return path;
        }

        public string WriteDiff(DiagnosticSnapshot before, DiagnosticSnapshot after, SnapshotDiffResult diff)
        {
            string directory = EnsureSessionDirectory();
            EnsureSessionHeader(directory);

            string player = after.Player == null ? "PX" : "P" + after.Player.DisplayIndex;
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string fileName = stamp + "-diff-" + player + ".txt";
            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path, SnapshotTextWriter.RenderDiff(before, after, diff));
            return path;
        }

        public string WriteNamedReport(string kind, string text)
        {
            string directory = EnsureSessionDirectory();
            EnsureSessionHeader(directory);
            string safeKind = string.IsNullOrEmpty(kind) ? "report" : kind;
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string path = Path.Combine(directory, stamp + "-" + safeKind + ".txt");
            File.WriteAllText(path, text ?? string.Empty);
            return path;
        }

        public string SessionDirectory
        {
            get { return EnsureSessionDirectory(); }
        }

        private string EnsureSessionDirectory()
        {
            if (!string.IsNullOrEmpty(_sessionDirectory) && Directory.Exists(_sessionDirectory))
            {
                return _sessionDirectory;
            }

            string fallbackRoot = Path.Combine(Paths.ConfigPath, "StrandedDeepDiagnostics");
            fallbackRoot = Path.Combine(fallbackRoot, "Reports");

            string root = string.IsNullOrEmpty(_configuredRoot)
                ? fallbackRoot
                : _configuredRoot;

            try
            {
                Directory.CreateDirectory(root);
            }
            catch
            {
                root = fallbackRoot;
                Directory.CreateDirectory(root);
            }

            string sessionName = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-v" + DiagnosticsConstants.PluginVersion;
            _sessionDirectory = Path.Combine(root, sessionName);
            Directory.CreateDirectory(_sessionDirectory);
            return _sessionDirectory;
        }

        private void EnsureSessionHeader(string directory)
        {
            if (_sessionHeaderWritten)
            {
                return;
            }

            string path = Path.Combine(directory, "session.txt");
            string text = _manifest == null ? "<capability manifest unavailable>" : _manifest.ToText();
            File.WriteAllText(path, text);
            _sessionHeaderWritten = true;
        }
    }
}
