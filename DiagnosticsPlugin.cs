using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;
using StrandedDeepDiagnostics.Core;
using StrandedDeepDiagnostics.Physics;
using StrandedDeepDiagnostics.CameraInspection;
using StrandedDeepDiagnostics.InputInspection;
using StrandedDeepDiagnostics.Players;
using StrandedDeepDiagnostics.Plugins;
using StrandedDeepDiagnostics.Reflection;
using StrandedDeepDiagnostics.Snapshots;
using StrandedDeepDiagnostics.Targets;
using StrandedDeepDiagnostics.UI;
using StrandedDeepDiagnostics.Interaction;
using StrandedDeepDiagnostics.Saveables;
using StrandedDeepDiagnostics.Storage;
using StrandedDeepDiagnostics.Inventory;
using StrandedDeepDiagnostics.World;
using StrandedDeepDiagnostics.Construction;
using StrandedDeepDiagnostics.Events;
using StrandedDeepDiagnostics.Tracing;
using StrandedDeepDiagnostics.Adapters;
using StrandedDeepDiagnostics.Audio;
using StrandedDeepDiagnostics.Raft;

namespace StrandedDeepDiagnostics
{
    [BepInPlugin(
        DiagnosticsConstants.PluginGuid,
        DiagnosticsConstants.PluginName,
        DiagnosticsConstants.PluginVersion)]
    public sealed class DiagnosticsPlugin : BaseUnityPlugin
    {
        private ConfigEntry<string> _toggleKeyConfig;
        private ConfigEntry<float> _maxRayDistanceConfig;
        private ConfigEntry<int> _maxHierarchyDepthConfig;
        private ConfigEntry<int> _maxFieldsConfig;
        private ConfigEntry<string> _reportRootConfig;

        private KeyCode _toggleKey;
        private bool _enabled;
        private bool _disposed;

        private CapabilityManifest _capabilities;
        private EpochManager _epochs;
        private PlayerResolver _playerResolver;
        private TargetPicker _targetPicker;
        private SemanticTargetResolver _targetResolver;
        private DiagnosticOverlay _overlay;
        private ReportWriter _reportWriter;
        private SnapshotHistory _history;
        private LoadedPluginsInspector _pluginsInspector;
        private InputInspector _inputInspector;
        private UICanvasInspector _uiCanvasInspector;
        private InteractionInspector _interactionInspector;
        private SaveableInspector _saveableInspector;
        private StorageInspector _storageInspector;
        private InventoryInspector _inventoryInspector;
        private WorldInspector _worldInspector;
        private ConstructionInspector _constructionInspector;
        private EventRingBuffer _eventBuffer;
        private TraceEngine _traceEngine;
        private AdapterInspector _adapterInspector;
        private AudioDiagnostics _audioDiagnostics;
        private RaftDiagnostics _raftDiagnostics;

        private List<PlayerContext> _players = new List<PlayerContext>();
        private int _activePlayerIndex;
        private float _nextPlayerRefreshAt;
        private DiagnosticTarget _liveTarget;
        private DiagnosticTarget _pinnedTarget;
        private OverlaySnapshot _overlaySnapshot;
        private string _statusMessage;
        private float _statusUntil;
        private int _moduleIndex;
        private readonly Dictionary<string, string> _textReportBaselines = new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly string[] _modules = new string[]
        {
            DiagnosticsConstants.ModuleObject,
            DiagnosticsConstants.ModulePhysics,
            DiagnosticsConstants.ModuleCamera,
            DiagnosticsConstants.ModuleInput,
            DiagnosticsConstants.ModuleUI,
            DiagnosticsConstants.ModuleInteraction,
            DiagnosticsConstants.ModuleSaveable,
            DiagnosticsConstants.ModuleStorage,
            DiagnosticsConstants.ModuleInventory,
            DiagnosticsConstants.ModuleWorld,
            DiagnosticsConstants.ModuleConstruction,
            DiagnosticsConstants.ModuleRaft,
            DiagnosticsConstants.ModuleAudio,
            DiagnosticsConstants.ModuleTrace,
            DiagnosticsConstants.ModulePlugins
        };

        private void Awake()
        {
            useGUILayout = false;

            BindConfig();
            _toggleKey = ParseKey(_toggleKeyConfig.Value, KeyCode.F8);

            _capabilities = CapabilityManifest.Build();
            _epochs = new EpochManager();
            _playerResolver = new PlayerResolver(_capabilities);
            _targetPicker = new TargetPicker();
            _targetResolver = new SemanticTargetResolver();
            _overlay = new DiagnosticOverlay();
            _reportWriter = new ReportWriter(_reportRootConfig.Value, _capabilities);
            _history = new SnapshotHistory(16);
            _pluginsInspector = new LoadedPluginsInspector();
            _inputInspector = new InputInspector();
            _uiCanvasInspector = new UICanvasInspector();
            _interactionInspector = new InteractionInspector();
            _saveableInspector = new SaveableInspector();
            _storageInspector = new StorageInspector();
            _inventoryInspector = new InventoryInspector();
            _worldInspector = new WorldInspector(_capabilities);
            _constructionInspector = new ConstructionInspector();
            _eventBuffer = new EventRingBuffer(2000);
            _traceEngine = new TraceEngine(_eventBuffer);
            _adapterInspector = new AdapterInspector(_pluginsInspector);
            _audioDiagnostics = new AudioDiagnostics();
            _raftDiagnostics = new RaftDiagnostics(_eventBuffer);

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            Logger.LogInfo(DiagnosticsConstants.PluginName + " v" + DiagnosticsConstants.PluginVersion + " loaded.");
            Logger.LogInfo("Toggle key: " + _toggleKey + ". v1.0.0-rc1 public release candidate: inspection-first core, optional adapters, capability manifest, portable reports, adaptive overlay layout, MIT-licensed public source.");
        }

        private void BindConfig()
        {
            _toggleKeyConfig = Config.Bind(
                "Input",
                "ToggleKey",
                "F8",
                "Diagnostics ON/OFF. Alt + the same key switches active split-screen player.");

            _maxRayDistanceConfig = Config.Bind(
                "Inspection",
                "MaxRayDistance",
                100f,
                "Maximum center-view ray distance in world units.");

            _maxHierarchyDepthConfig = Config.Bind(
                "Inspection",
                "MaxHierarchyDepth",
                32,
                "Maximum hierarchy depth written into reports.");

            _maxFieldsConfig = Config.Bind(
                "Inspection",
                "MaxFieldsPerComponent",
                48,
                "Maximum number of safe field reads per component in a report.");

            _reportRootConfig = Config.Bind(
                "Reports",
                "ReportRoot",
                "",
                "Optional custom report root. Empty uses BepInEx/config/StrandedDeepDiagnostics/Reports.");
        }

        private void Update()
        {
            if (_disposed) return;

            HandleToggleAndPlayerKeys();
            if (!_enabled) return;

            if (Time.realtimeSinceStartup >= _nextPlayerRefreshAt)
            {
                RefreshPlayers();
                _nextPlayerRefreshAt = Time.realtimeSinceStartup + 0.5f;
            }

            if (_audioDiagnostics != null && (CurrentModuleId == DiagnosticsConstants.ModuleAudio || _audioDiagnostics.NeedsMonitoring))
            {
                _audioDiagnostics.Update(_players);
            }

            if (_raftDiagnostics != null && (CurrentModuleId == DiagnosticsConstants.ModuleRaft || _raftDiagnostics.NeedsMonitoring))
            {
                _raftDiagnostics.Update(GetInspectionTarget());
            }

            HandleDiagnosticCommands();
            PollAudioIncidentExport();
            PollRaftIncidentExport();
        }

        private void FixedUpdate()
        {
            if (_disposed || !_enabled) return;
            if (_raftDiagnostics != null && _raftDiagnostics.NeedsMonitoring)
            {
                _raftDiagnostics.FixedUpdate();
            }
        }

        private void LateUpdate()
        {
            if (_disposed || !_enabled)
            {
                _overlaySnapshot = null;
                return;
            }

            PlayerContext player = GetActivePlayer();
            _liveTarget = _targetPicker.Pick(player, Mathf.Max(1f, _maxRayDistanceConfig.Value));
            if (_liveTarget != null)
            {
                _targetResolver.Resolve(_liveTarget, 16);
            }

            if (_pinnedTarget != null && !_pinnedTarget.IsAlive)
            {
                _pinnedTarget = null;
            }

            BuildOverlaySnapshot(player);
        }

        private void OnGUI()
        {
            if (_disposed || !_enabled) return;
            _overlay.Draw(_overlaySnapshot);
        }

        private void OnDestroy()
        {
            DisposePlugin();
        }

        private void OnApplicationQuit()
        {
            DisposePlugin();
        }

        private void DisposePlugin()
        {
            if (_disposed) return;

            _disposed = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            _liveTarget = null;
            _pinnedTarget = null;
            _players.Clear();
            _history.Clear();
            if (_traceEngine != null) _traceEngine.Disable();
            if (_audioDiagnostics != null)
            {
                _audioDiagnostics.DisableTrace();
                _audioDiagnostics.CancelIncident();
            }
            if (_raftDiagnostics != null)
            {
                _raftDiagnostics.DisableRecorder();
                _raftDiagnostics.CancelIncident();
            }
        }

        private void HandleToggleAndPlayerKeys()
        {
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (alt && Input.GetKeyDown(_toggleKey))
            {
                if (_enabled) SwitchActivePlayer();
                return;
            }

            if (Input.GetKeyDown(_toggleKey))
            {
                _enabled = !_enabled;
                if (_enabled)
                {
                    RefreshPlayers();
                    _nextPlayerRefreshAt = Time.realtimeSinceStartup + 0.5f;
                    SetStatus("Diagnostics ON", 2f);
                }
                else
                {
                    _liveTarget = null;
                    _overlaySnapshot = null;
                    if (_traceEngine != null) _traceEngine.Disable();
                    if (_audioDiagnostics != null)
                    {
                        _audioDiagnostics.DisableTrace();
                        _audioDiagnostics.CancelIncident();
                    }
                    if (_raftDiagnostics != null)
                    {
                        _raftDiagnostics.DisableRecorder();
                        _raftDiagnostics.CancelIncident();
                    }
                    SetStatus("Diagnostics OFF; traces/recorders disabled", 1.5f);
                }
            }
        }

        private void HandleDiagnosticCommands()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                CycleModule();
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                TogglePinnedTarget();
            }

            if (Input.GetKeyDown(KeyCode.F11))
            {
                try
                {
                    if (CurrentModuleId == DiagnosticsConstants.ModuleAudio)
                    {
                        if (_traceEngine != null) _traceEngine.Disable();
                        if (_raftDiagnostics != null) _raftDiagnostics.DisableRecorder();
                        string audioResult = _audioDiagnostics.ToggleDspTrace();
                        SetStatus(audioResult, 3f);
                    }
                    else if (CurrentModuleId == DiagnosticsConstants.ModuleRaft)
                    {
                        if (_traceEngine != null) _traceEngine.Disable();
                        if (_audioDiagnostics != null) _audioDiagnostics.DisableTrace();
                        string raftResult = _raftDiagnostics.ToggleRecorder();
                        SetStatus(raftResult, 3f);
                    }
                    else
                    {
                        if (_audioDiagnostics != null) _audioDiagnostics.DisableTrace();
                        if (_raftDiagnostics != null) _raftDiagnostics.DisableRecorder();
                        string traceProfile = ResolveTraceProfile(CurrentModuleId);
                        if (string.IsNullOrEmpty(traceProfile))
                        {
                            SetStatus("No targeted trace profile for " + CurrentModuleId, 2.5f);
                        }
                        else
                        {
                            string result = _traceEngine.Toggle(traceProfile);
                            SetStatus(result, 3f);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Trace toggle failed: " + ex);
                    SetStatus("Trace toggle failed: " + ex.GetType().Name, 3f);
                }
            }

            if (Input.GetKeyDown(KeyCode.F12))
            {
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                if (CurrentModuleId == DiagnosticsConstants.ModuleAudio)
                {
                    SetStatus(_audioDiagnostics.MarkIncident(), 6f);
                    return;
                }

                if (CurrentModuleId == DiagnosticsConstants.ModuleRaft)
                {
                    if (shift)
                    {
                        ExportSpecialReport("raft-status", _raftDiagnostics.RenderCurrentReport(GetInspectionTarget()));
                    }
                    else
                    {
                        SetStatus(_raftDiagnostics.MarkIncident(GetInspectionTarget()), 6f);
                    }
                    return;
                }

                if (shift && CurrentModuleId != DiagnosticsConstants.ModuleObject && CurrentModuleId != DiagnosticsConstants.ModulePhysics)
                {
                    CaptureSpecialDiff();
                    return;
                }

                if (CurrentModuleId == DiagnosticsConstants.ModulePlugins)
                {
                    ExportPluginReport();
                }
                else if (CurrentModuleId == DiagnosticsConstants.ModuleCamera)
                {
                    ExportCameraReport();
                }
                else if (CurrentModuleId == DiagnosticsConstants.ModuleInput)
                {
                    ExportInputReport();
                }
                else if (CurrentModuleId == DiagnosticsConstants.ModuleUI)
                {
                    ExportUiReport();
                }
                else if (CurrentModuleId == DiagnosticsConstants.ModuleInteraction)
                {
                    ExportSpecialReport("interaction", _interactionInspector.RenderReport(GetInspectionTarget()));
                }
                else if (CurrentModuleId == DiagnosticsConstants.ModuleSaveable)
                {
                    ExportSpecialReport("saveable-native", _saveableInspector.RenderReport(GetInspectionTarget()));
                }
                else if (CurrentModuleId == DiagnosticsConstants.ModuleStorage)
                {
                    ExportSpecialReport("storage", _storageInspector.RenderReport(GetInspectionTarget()));
                }
                else if (CurrentModuleId == DiagnosticsConstants.ModuleInventory)
                {
                    ExportSpecialReport("inventory", _inventoryInspector.RenderReport(GetActivePlayer(), GetInspectionTarget()));
                }
                else if (CurrentModuleId == DiagnosticsConstants.ModuleWorld)
                {
                    ExportSpecialReport("world-zones", _worldInspector.RenderReport());
                }
                else if (CurrentModuleId == DiagnosticsConstants.ModuleConstruction)
                {
                    ExportSpecialReport("construction-raft", _constructionInspector.RenderReport(GetInspectionTarget()));
                }
                else if (CurrentModuleId == DiagnosticsConstants.ModuleTrace)
                {
                    ExportSpecialReport("trace-events", _traceEngine.RenderReport());
                }
                else if (shift)
                {
                    CaptureAndDiff();
                }
                else
                {
                    CaptureAndExport(ctrl);
                }
            }
        }

        private string CurrentModuleId
        {
            get
            {
                if (_moduleIndex < 0 || _moduleIndex >= _modules.Length) _moduleIndex = 0;
                return _modules[_moduleIndex];
            }
        }

        private void CycleModule()
        {
            _moduleIndex++;
            if (_moduleIndex >= _modules.Length) _moduleIndex = 0;
            SetStatus("Module: " + CurrentModuleId, 1.8f);
        }

        private void RefreshPlayers()
        {
            List<PlayerContext> resolved = _playerResolver.ResolvePlayers();
            _players = resolved;

            bool hasPlayers = _players.Count > 0;
            bool worldStarted = _epochs.ObservePlayerPresence(hasPlayers);
            if (worldStarted)
            {
                _liveTarget = null;
                _pinnedTarget = null;
                _history.Clear();
                _textReportBaselines.Clear();
                if (_inputInspector != null) _inputInspector.Invalidate();
                if (_uiCanvasInspector != null) _uiCanvasInspector.Invalidate();
                AddLifecycleEvent("WORLD", "START", "players=" + _players.Count);
            }

            if (!hasPlayers)
            {
                _activePlayerIndex = 0;
                _liveTarget = null;
                _pinnedTarget = null;
                return;
            }

            if (_activePlayerIndex < 0 || _activePlayerIndex >= _players.Count)
            {
                _activePlayerIndex = 0;
            }
        }

        private void SwitchActivePlayer()
        {
            if (_players == null || _players.Count == 0) RefreshPlayers();

            if (_players.Count == 0)
            {
                SetStatus("No active Beam.Player contexts resolved", 2.5f);
                return;
            }

            _activePlayerIndex++;
            if (_activePlayerIndex >= _players.Count) _activePlayerIndex = 0;

            _liveTarget = null;
            _pinnedTarget = null;
            SetStatus("Active player: " + GetActivePlayer().DisplayName, 2f);
        }

        private PlayerContext GetActivePlayer()
        {
            if (_players == null || _players.Count == 0) return null;
            if (_activePlayerIndex < 0 || _activePlayerIndex >= _players.Count) _activePlayerIndex = 0;
            return _players[_activePlayerIndex];
        }

        private DiagnosticTarget GetInspectionTarget()
        {
            if (_pinnedTarget != null && _pinnedTarget.IsAlive) return _pinnedTarget;
            if (_liveTarget != null && _liveTarget.IsAlive) return _liveTarget;
            return null;
        }

        private void TogglePinnedTarget()
        {
            if (_liveTarget != null && _liveTarget.IsAlive)
            {
                if (_pinnedTarget != null && _pinnedTarget.IsAlive && _pinnedTarget.GameObject == _liveTarget.GameObject)
                {
                    _pinnedTarget = null;
                    SetStatus("Target unpinned", 1.5f);
                }
                else
                {
                    _pinnedTarget = _liveTarget.CloneShallow();
                    SetStatus("Pinned target: " + SafeObjectName(_pinnedTarget.InspectionGameObject), 1.8f);
                }
                return;
            }

            if (_pinnedTarget != null)
            {
                _pinnedTarget = null;
                SetStatus("Target unpinned", 1.5f);
            }
            else
            {
                SetStatus("No live target under center ray", 2f);
            }
        }

        private DiagnosticSnapshot BuildCurrentSnapshot(bool deep)
        {
            DiagnosticTarget target = GetInspectionTarget();
            if (target == null) return null;

            return SnapshotBuilder.Build(
                _epochs,
                _capabilities,
                GetActivePlayer(),
                target,
                deep,
                Mathf.Clamp(_maxHierarchyDepthConfig.Value, 4, 128),
                Mathf.Clamp(_maxFieldsConfig.Value, 4, 256),
                CurrentModuleId);
        }

        private void CaptureAndExport(bool deep)
        {
            DiagnosticSnapshot snapshot = BuildCurrentSnapshot(deep);
            if (snapshot == null)
            {
                SetStatus("No target to capture", 2f);
                return;
            }

            try
            {
                string path = _reportWriter.WriteSnapshot(snapshot, deep);
                _history.Add(snapshot);
                Logger.LogInfo("Diagnostic snapshot written: " + path);
                SetStatus((deep ? "Deep dump" : "Snapshot") + " exported; history=" + _history.Count, 2.5f);
            }
            catch (Exception ex)
            {
                Logger.LogError("Snapshot export failed: " + ex);
                SetStatus("Snapshot export failed: " + ex.GetType().Name, 3f);
            }
        }

        private void CaptureAndDiff()
        {
            DiagnosticSnapshot current = BuildCurrentSnapshot(false);
            if (current == null)
            {
                SetStatus("No target to diff", 2f);
                return;
            }

            try
            {
                DiagnosticSnapshot previous = _history.Last;
                string snapshotPath = _reportWriter.WriteSnapshot(current, false);

                if (previous == null)
                {
                    _history.Add(current);
                    Logger.LogInfo("Diff baseline snapshot written: " + snapshotPath);
                    SetStatus("Diff baseline captured; repeat Shift+F12 after a state change", 3f);
                    return;
                }

                SnapshotDiffResult diff = SnapshotDiff.Compare(previous, current);
                string diffPath = _reportWriter.WriteDiff(previous, current, diff);
                _history.Add(current);
                Logger.LogInfo("Diagnostic diff written: " + diffPath);
                SetStatus("Diff exported: " + diff.ChangeCount + " changes", 2.8f);
            }
            catch (Exception ex)
            {
                Logger.LogError("Snapshot diff failed: " + ex);
                SetStatus("Snapshot diff failed: " + ex.GetType().Name, 3f);
            }
        }

        private void ExportPluginReport()
        {
            try
            {
                string report = _pluginsInspector.RenderReport(Time.realtimeSinceStartup) + "\r\n" + _adapterInspector.RenderReport(Time.realtimeSinceStartup);
                string path = _reportWriter.WriteNamedReport("plugins", report);
                Logger.LogInfo("Loaded plugins report written: " + path);
                SetStatus("Loaded plugins exported", 2.5f);
            }
            catch (Exception ex)
            {
                Logger.LogError("Loaded plugins export failed: " + ex);
                SetStatus("Plugins export failed: " + ex.GetType().Name, 3f);
            }
        }

        private void ExportCameraReport()
        {
            try
            {
                PlayerContext player = GetActivePlayer();
                string report = CameraInspector.RenderReport(player);
                string suffix = player == null ? "PX" : "P" + player.DisplayIndex;
                string path = _reportWriter.WriteNamedReport("camera-" + suffix, report);
                Logger.LogInfo("Camera report written: " + path);
                SetStatus("Camera report exported", 2.5f);
            }
            catch (Exception ex)
            {
                Logger.LogError("Camera export failed: " + ex);
                SetStatus("Camera export failed: " + ex.GetType().Name, 3f);
            }
        }

        private void ExportInputReport()
        {
            try
            {
                PlayerContext player = GetActivePlayer();
                string report = _inputInspector.RenderReport(player);
                string suffix = player == null ? "PX" : "P" + player.DisplayIndex;
                string path = _reportWriter.WriteNamedReport("input-" + suffix, report);
                Logger.LogInfo("Input report written: " + path);
                SetStatus("Input report exported", 2.5f);
            }
            catch (Exception ex)
            {
                Logger.LogError("Input export failed: " + ex);
                SetStatus("Input export failed: " + ex.GetType().Name, 3f);
            }
        }

        private void ExportUiReport()
        {
            try
            {
                string report = _uiCanvasInspector.RenderReport(_players, Time.realtimeSinceStartup);
                string path = _reportWriter.WriteNamedReport("ui-canvases", report);
                Logger.LogInfo("UI Canvas report written: " + path);
                SetStatus("UI Canvas census exported", 2.5f);
            }
            catch (Exception ex)
            {
                Logger.LogError("UI Canvas export failed: " + ex);
                SetStatus("UI export failed: " + ex.GetType().Name, 3f);
            }
        }

        private void CaptureSpecialDiff()
        {
            string kind;
            string body;
            if (!TryBuildSpecialReport(out kind, out body))
            {
                SetStatus("No text diff source for " + CurrentModuleId, 2.5f);
                return;
            }

            string previous;
            if (!_textReportBaselines.TryGetValue(CurrentModuleId, out previous))
            {
                _textReportBaselines[CurrentModuleId] = body ?? string.Empty;
                ExportSpecialReport(kind + "-diff-baseline", body);
                SetStatus("Text diff baseline captured for " + CurrentModuleId, 2.8f);
                return;
            }

            string diff = TextReportDiff.Compare(previous, body);
            _textReportBaselines[CurrentModuleId] = body ?? string.Empty;
            ExportSpecialReport(kind + "-diff", diff);
        }

        private bool TryBuildSpecialReport(out string kind, out string body)
        {
            kind = null;
            body = null;
            PlayerContext player = GetActivePlayer();
            if (CurrentModuleId == DiagnosticsConstants.ModulePlugins)
            {
                kind = "plugins";
                body = _pluginsInspector.RenderReport(Time.realtimeSinceStartup) + "\r\n" + _adapterInspector.RenderReport(Time.realtimeSinceStartup);
                return true;
            }
            if (CurrentModuleId == DiagnosticsConstants.ModuleCamera)
            {
                kind = "camera-" + (player == null ? "PX" : "P" + player.DisplayIndex);
                body = CameraInspector.RenderReport(player);
                return true;
            }
            if (CurrentModuleId == DiagnosticsConstants.ModuleInput)
            {
                kind = "input-" + (player == null ? "PX" : "P" + player.DisplayIndex);
                body = _inputInspector.RenderReport(player);
                return true;
            }
            if (CurrentModuleId == DiagnosticsConstants.ModuleUI) { kind = "ui-canvases"; body = _uiCanvasInspector.RenderReport(_players, Time.realtimeSinceStartup); return true; }
            if (CurrentModuleId == DiagnosticsConstants.ModuleInteraction) { kind = "interaction"; body = _interactionInspector.RenderReport(GetInspectionTarget()); return true; }
            if (CurrentModuleId == DiagnosticsConstants.ModuleSaveable) { kind = "saveable-native"; body = _saveableInspector.RenderReport(GetInspectionTarget()); return true; }
            if (CurrentModuleId == DiagnosticsConstants.ModuleStorage) { kind = "storage"; body = _storageInspector.RenderReport(GetInspectionTarget()); return true; }
            if (CurrentModuleId == DiagnosticsConstants.ModuleInventory) { kind = "inventory"; body = _inventoryInspector.RenderReport(player, GetInspectionTarget()); return true; }
            if (CurrentModuleId == DiagnosticsConstants.ModuleWorld) { kind = "world-zones"; body = _worldInspector.RenderReport(); return true; }
            if (CurrentModuleId == DiagnosticsConstants.ModuleConstruction) { kind = "construction-raft"; body = _constructionInspector.RenderReport(GetInspectionTarget()); return true; }
            if (CurrentModuleId == DiagnosticsConstants.ModuleRaft) { kind = "raft-status"; body = _raftDiagnostics.RenderCurrentReport(GetInspectionTarget()); return true; }
            if (CurrentModuleId == DiagnosticsConstants.ModuleAudio) { kind = "audio-status"; body = _audioDiagnostics.RenderStatusReport(); return true; }
            if (CurrentModuleId == DiagnosticsConstants.ModuleTrace) { kind = "trace-events"; body = _traceEngine.RenderReport(); return true; }
            return false;
        }

        private void ExportSpecialReport(string name, string body)
        {
            try
            {
                string path = _reportWriter.WriteNamedReport(name, body ?? string.Empty);
                Logger.LogInfo("Diagnostic report written: " + path);
                SetStatus(name + " report exported", 2.5f);
            }
            catch (Exception ex)
            {
                Logger.LogError(name + " export failed: " + ex);
                SetStatus(name + " export failed: " + ex.GetType().Name, 3f);
            }
        }

        private void PollAudioIncidentExport()
        {
            if (_audioDiagnostics == null) return;
            string report;
            if (!_audioDiagnostics.TryCompleteIncident(out report)) return;

            try
            {
                string path = _reportWriter.WriteNamedReport("audio-incident", report);
                Logger.LogInfo("Audio incident report written: " + path);
                SetStatus("AUDIO incident exported", 3f);
            }
            catch (Exception ex)
            {
                Logger.LogError("Audio incident export failed: " + ex);
                SetStatus("AUDIO incident export failed: " + ex.GetType().Name, 3f);
            }
        }

        private void PollRaftIncidentExport()
        {
            if (_raftDiagnostics == null) return;
            string report;
            if (!_raftDiagnostics.TryCompleteIncident(out report)) return;

            try
            {
                string path = _reportWriter.WriteNamedReport("raft-incident", report);
                Logger.LogInfo("Raft incident report written: " + path);
                SetStatus("RAFT incident exported", 3f);
            }
            catch (Exception ex)
            {
                Logger.LogError("Raft incident export failed: " + ex);
                SetStatus("RAFT incident export failed: " + ex.GetType().Name, 3f);
            }
        }

        private void BuildOverlaySnapshot(PlayerContext player)
        {
            OverlaySnapshot snapshot = new OverlaySnapshot();
            snapshot.Visible = true;
            snapshot.ViewportRect = ResolveGuiViewport(player);

            List<string> lines = new List<string>();
            lines.Add("STRANDED DEEP DIAGNOSTICS v" + DiagnosticsConstants.PluginVersion);
            lines.Add("PLAYER  " + (player == null ? "<unresolved>" : player.DisplayName) + "    MODULE  " + CurrentModuleId);
            if (_capabilities != null)
            {
                lines.Add("CAPS    " + TrimMiddle(_capabilities.DescribeModule(CurrentModuleId), 96));
            }
            lines.Add("EPOCH   process=" + _epochs.ProcessEpoch + " world=" + _epochs.WorldEpoch + " scene=" + _epochs.SceneEpoch);

            if (player != null)
            {
                lines.Add("CAMERA  " + TrimMiddle(player.CameraPath, 64));
                lines.Add("VIEW    " + ValueFormatter.FormatSimple(player.CameraRect));
            }
            else
            {
                lines.Add("CAMERA  <unresolved>");
            }

            if (CurrentModuleId == DiagnosticsConstants.ModulePlugins)
            {
                BuildPluginsOverlay(lines);
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModulePhysics)
            {
                BuildTargetHeader(lines);
                string[] physicsLines = PhysicsInspector.DescribeOverlay(GetInspectionTarget());
                int i;
                for (i = 0; i < physicsLines.Length; i++) lines.Add(physicsLines[i]);
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModuleCamera)
            {
                string[] cameraLines = CameraInspector.DescribeOverlay(player);
                int i;
                for (i = 0; i < cameraLines.Length; i++) lines.Add(cameraLines[i]);
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModuleInput)
            {
                string[] inputLines = _inputInspector.DescribeOverlay(player, Time.realtimeSinceStartup);
                int i;
                for (i = 0; i < inputLines.Length; i++) lines.Add(inputLines[i]);
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModuleUI)
            {
                string[] uiLines = _uiCanvasInspector.DescribeOverlay(_players, player, Time.realtimeSinceStartup);
                int i;
                for (i = 0; i < uiLines.Length; i++) lines.Add(uiLines[i]);
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModuleInteraction)
            {
                BuildTargetHeader(lines);
                AddLines(lines, _interactionInspector.DescribeOverlay(GetInspectionTarget()));
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModuleSaveable)
            {
                BuildTargetHeader(lines);
                AddLines(lines, _saveableInspector.DescribeOverlay(GetInspectionTarget()));
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModuleStorage)
            {
                BuildTargetHeader(lines);
                AddLines(lines, _storageInspector.DescribeOverlay(GetInspectionTarget()));
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModuleInventory)
            {
                AddLines(lines, _inventoryInspector.DescribeOverlay(player));
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModuleWorld)
            {
                AddLines(lines, _worldInspector.DescribeOverlay());
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModuleConstruction)
            {
                BuildTargetHeader(lines);
                AddLines(lines, _constructionInspector.DescribeOverlay(GetInspectionTarget()));
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModuleRaft)
            {
                AddLines(lines, _raftDiagnostics.DescribeOverlay(GetInspectionTarget()));
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModuleAudio)
            {
                AddLines(lines, _audioDiagnostics.DescribeOverlay());
            }
            else if (CurrentModuleId == DiagnosticsConstants.ModuleTrace)
            {
                AddLines(lines, _traceEngine.DescribeOverlay());
            }
            else
            {
                BuildObjectOverlay(lines);
            }

            if (!string.IsNullOrEmpty(_statusMessage) && Time.realtimeSinceStartup <= _statusUntil)
            {
                lines.Add("STATUS  " + _statusMessage);
            }

            lines.Add(_toggleKey + " off | Alt+" + _toggleKey + " player | F9 module | F10 pin");
            lines.Add("F11 trace/record | F12 report/incident");
            snapshot.Lines = lines.ToArray();
            _overlaySnapshot = snapshot;
        }

        private void BuildTargetHeader(List<string> lines)
        {
            lines.Add("LIVE    " + DescribeTarget(_liveTarget));
            lines.Add("PINNED  " + DescribeTarget(_pinnedTarget));

            DiagnosticTarget shown = GetInspectionTarget();
            if (shown != null && shown.IsAlive)
            {
                string rawClass = ResolveRawClassification(shown);
                lines.Add("RAW#0   " + SafeObjectName(shown.RawGameObject) + " [" + rawClass + "] @ " + shown.RawHitDistance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "m");
                lines.Add("PICK    " + SafeObjectName(shown.GameObject) + " @ " + shown.HitDistance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "m");
                lines.Add("PRIMARY " + SafeObjectName(shown.InspectionGameObject));
                lines.Add("HITS    " + shown.RaycastCandidates.Count + (shown.RaycastTruncated ? "+ (buffer full)" : "") + " sorted; SELF/CONNECTOR skipped for PICK");

                int i;
                int max = Math.Min(3, shown.RaycastCandidates.Count);
                for (i = 0; i < max; i++)
                {
                    RaycastCandidate candidate = shown.RaycastCandidates[i];
                    if (candidate == null || candidate.GameObject == null) continue;
                    string marker = candidate.IsSelected ? ">" : " ";
                    lines.Add(marker + " #" + candidate.Index + " " + candidate.Classification + " " + TrimMiddle(SafeObjectName(candidate.GameObject), 36) + " " + candidate.Distance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "m");
                }
            }
        }

        private static string ResolveRawClassification(DiagnosticTarget target)
        {
            if (target == null || target.RaycastCandidates == null || target.RaycastCandidates.Count == 0)
            {
                return "UNKNOWN";
            }

            RaycastCandidate candidate = target.RaycastCandidates[0];
            return candidate == null || string.IsNullOrEmpty(candidate.Classification)
                ? "UNKNOWN"
                : candidate.Classification;
        }

        private void BuildObjectOverlay(List<string> lines)
        {
            BuildTargetHeader(lines);
            DiagnosticTarget shown = GetInspectionTarget();
            if (shown == null || !shown.IsAlive) return;

            GameObject primary = shown.InspectionGameObject;
            lines.Add("TYPE    " + SafeReflection.GetPrimaryComponentTypeName(primary));
            lines.Add("PATH    " + TrimMiddle(SafeReflection.GetHierarchyPath(primary.transform, 16), 76));
            lines.Add("COMP    " + TrimMiddle(SafeReflection.GetComponentTypeSummary(primary, 8), 76));
            lines.Add("RELATED " + shown.RelatedTargets.Count + " ancestor candidates");

            int i;
            int max = Math.Min(3, shown.RelatedTargets.Count);
            for (i = 0; i < max; i++)
            {
                RelatedTarget item = shown.RelatedTargets[i];
                lines.Add("  " + item.Role + "  " + TrimMiddle(SafeObjectName(item.GameObject), 48));
            }
        }

        private void BuildPluginsOverlay(List<string> lines)
        {
            IList<LoadedPluginRecord> records = _pluginsInspector.GetRecords(Time.realtimeSinceStartup, false);
            int diagnosticLike = 0;
            int i;
            for (i = 0; i < records.Count; i++)
            {
                if (records[i].LooksDiagnostic) diagnosticLike++;
            }

            lines.Add("LOADED  " + records.Count + " plugins; diagnostic-like=" + diagnosticLike);
            int max = Math.Min(8, records.Count);
            for (i = 0; i < max; i++)
            {
                LoadedPluginRecord record = records[i];
                string marker = record.LooksDiagnostic ? "! " : "  ";
                lines.Add(marker + TrimMiddle(record.Name + " " + record.Version, 68));
            }
            lines.Add("F12 exports full GUID/name/version/location report");
            lines.Add("Known project mods use one-way optional adapter discovery only");
        }

        private static void AddLines(List<string> target, string[] source)
        {
            if (target == null || source == null) return;
            int i;
            for (i = 0; i < source.Length; i++) target.Add(source[i]);
        }

        private static string ResolveTraceProfile(string module)
        {
            if (module == DiagnosticsConstants.ModuleInteraction) return DiagnosticsConstants.ModuleInteraction;
            if (module == DiagnosticsConstants.ModuleStorage) return DiagnosticsConstants.ModuleStorage;
            if (module == DiagnosticsConstants.ModuleInventory) return DiagnosticsConstants.ModuleInventory;
            if (module == DiagnosticsConstants.ModuleSaveable) return DiagnosticsConstants.ModuleSaveable;
            if (module == DiagnosticsConstants.ModuleWorld) return DiagnosticsConstants.ModuleWorld;
            if (module == DiagnosticsConstants.ModuleConstruction) return DiagnosticsConstants.ModuleConstruction;
            if (module == DiagnosticsConstants.ModuleTrace) return DiagnosticsConstants.ModuleTrace;
            return null;
        }

        private void AddLifecycleEvent(string category, string phase, string payload)
        {
            if (_eventBuffer == null) return;
            DiagnosticEvent e = new DiagnosticEvent();
            e.TimestampUtc = DateTime.UtcNow;
            e.Frame = Time.frameCount;
            e.Phase = phase;
            e.Category = category;
            e.Module = CurrentModuleId;
            PlayerContext p = GetActivePlayer();
            e.Player = p == null ? null : p.DisplayName;
            e.Payload = payload;
            _eventBuffer.Add(e);
        }

        private Rect ResolveGuiViewport(PlayerContext player)
        {
            if (player == null || player.Camera == null)
            {
                return new Rect(0f, 0f, Screen.width, Screen.height);
            }

            Rect r = player.Camera.rect;
            float x = r.x * Screen.width;
            float width = r.width * Screen.width;
            float height = r.height * Screen.height;
            float y = Screen.height - ((r.y + r.height) * Screen.height);
            return new Rect(x, y, width, height);
        }

        private static string DescribeTarget(DiagnosticTarget target)
        {
            if (target == null || !target.IsAlive) return "<none>";
            string name = SafeObjectName(target.GameObject);
            if (target.HasRaycastHit)
            {
                return name + " @ " + target.HitDistance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "m";
            }
            return name;
        }

        private static string SafeObjectName(GameObject gameObject)
        {
            if (gameObject == null) return "<destroyed>";
            try { return gameObject.name; }
            catch { return "<unavailable>"; }
        }

        private static string TrimMiddle(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text ?? string.Empty;
            int side = (maxLength - 3) / 2;
            return text.Substring(0, side) + "..." + text.Substring(text.Length - side);
        }

        private void SetStatus(string message, float seconds)
        {
            _statusMessage = message;
            _statusUntil = Time.realtimeSinceStartup + seconds;
        }

        private static KeyCode ParseKey(string text, KeyCode fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            try { return (KeyCode)Enum.Parse(typeof(KeyCode), text, true); }
            catch { return fallback; }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _epochs.NotifySceneLoaded(scene, mode);
            AddLifecycleEvent("SCENE", "LOADED", scene.name + " mode=" + mode);
            InvalidateSceneScopedState();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            _epochs.NotifySceneUnloaded(scene);
            AddLifecycleEvent("SCENE", "UNLOADED", scene.name);
            if (_traceEngine != null) _traceEngine.Disable();
            if (_audioDiagnostics != null) _audioDiagnostics.DisableTrace();
            if (_raftDiagnostics != null) _raftDiagnostics.DisableRecorder();
            InvalidateSceneScopedState();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            _epochs.NotifyActiveSceneChanged(previous, next);
            AddLifecycleEvent("SCENE", "ACTIVE_CHANGED", previous.name + " -> " + next.name);
            InvalidateSceneScopedState();
        }

        private void InvalidateSceneScopedState()
        {
            _liveTarget = null;
            _pinnedTarget = null;
            _players.Clear();
            _activePlayerIndex = 0;
            _nextPlayerRefreshAt = 0f;
            _textReportBaselines.Clear();
            if (_inputInspector != null) _inputInspector.Invalidate();
            if (_uiCanvasInspector != null) _uiCanvasInspector.Invalidate();
            if (_audioDiagnostics != null) _audioDiagnostics.InvalidateSceneState();
            if (_raftDiagnostics != null) _raftDiagnostics.InvalidateSceneState();
        }
    }
}
