using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AbilityKit.Game.Test.UnitTest
{
    [InitializeOnLoad]
    public static class MultiplayerHeadlessHeroReplacementCommand
    {
        private const string StartConfigPath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/BattleStart/BattleStartConfig.asset";
        private const string RemotePresetPath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/BattleStart/BattleStartPreset_远程.asset";
        private const string GatewayConfigPath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/BattleStart/BattleGatewayConfig.asset";
        private const string RunningKey = "AbilityKit.MultiplayerHeadlessHeroReplacement.Running";
        private const string ResultPathKey = "AbilityKit.MultiplayerHeadlessHeroReplacement.ResultPath";
        private const int InitialHeroId = 1001;
        private const int ReplacementHeroId = 1002;
        private static readonly TimeSpan OverallTimeout = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan ReplacementTimeout = TimeSpan.FromMinutes(1);

        private static int _updates;
        private static string _lastStage;
        private static HeadlessStage _stage;
        private static Task _operation;
        private static CancellationTokenSource _lifetime;
        private static BattleStartConfig _runtimeConfig;
        private static BattleStartPresetSO _runtimePreset;
        private static BattlePlayersConfigSO _runtimePlayers;
        private static MultiplayerRoomFlowController _controller;
        private static GatewayMultiplayerRoomSession _roomSession;
        private static ClientRoomStore _roomStore;
        private static GameFlowDomain _flow;
        private static uint _authoritativePlayerId;
        private static string _playerId;
        private static int _oldActorId;
        private static int _newActorId;
        private static int _submittedFrame;
        private static int _baselineRevision;
        private static DateTime _deadlineUtc;
        private static DateTime _replacementDeadlineUtc;

        static MultiplayerHeadlessHeroReplacementCommand()
        {
            EditorApplication.update -= ContinueInPlayMode;
            EditorApplication.update += ContinueInPlayMode;
        }

        public static void Run()
        {
            var resultPath = GetArgValue("-multiplayerHeadlessResult");
            if (string.IsNullOrWhiteSpace(resultPath))
            {
                // Default to local/Logs/headless/ under the repository root (Unity/.. = repo root).
                // Named by timestamp so repeated runs do not overwrite each other.
                // Override with -multiplayerHeadlessResult for CI or ad-hoc diagnostic snapshots.
                var headlessDir = Path.GetFullPath("../../local/Logs/headless");
                Directory.CreateDirectory(headlessDir);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                resultPath = Path.Combine(headlessDir, $"MultiplayerHeadlessHeroReplacement-{stamp}.xml");
            }

            try
            {
                ResetState();
                SessionState.SetBool(RunningKey, true);
                SessionState.SetString(ResultPathKey, resultPath);

                DemoGameplayTestLauncher.OpenMobaLocalForPlay();

                var gateway = LoadAsset<BattleGatewayConfigSO>(GatewayConfigPath);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Finish(false, "EXCEPTION: " + exception);
            }
        }

        private static void ContinueInPlayMode()
        {
            if (!SessionState.GetBool(RunningKey, false) ||
                !EditorApplication.isPlaying ||
                EditorApplication.isPaused)
            {
                return;
            }

            try
            {
                _updates++;
                if (_deadlineUtc == default)
                {
                    _deadlineUtc = DateTime.UtcNow + OverallTimeout;
                }

                if (DateTime.UtcNow > _deadlineUtc)
                {
                    throw new TimeoutException("Multiplayer headless hero replacement timed out. " + BuildDiagnostic());
                }

                Tick();
            }
            catch (Exception exception)
            {
                Finish(false, "EXCEPTION: " + exception + " | " + BuildDiagnostic());
            }
        }

        private static void Tick()
        {
            var entry = Object.FindObjectOfType<GameEntry>();
            if (entry == null || !GameEntry.IsInitialized)
            {
                Stage("waitingForGameEntry");
                return;
            }

            switch (_stage)
            {
                case HeadlessStage.WaitingForEntry:
                    _lifetime?.Dispose();
                    _lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                    CreateRuntimeConfiguration();
                    _flow = entry.Get<GameFlowDomain>();
                    _controller = entry.Get<MultiplayerRoomFlowController>();
                    _roomSession = entry.Get<GatewayMultiplayerRoomSession>();
                    _roomStore = entry.Get<ClientRoomStore>();
                    var selection = entry.Get<LobbyBattleEntrySelection>();
                    selection.SelectRemote(_runtimeConfig, _runtimePreset);
                    _stage = HeadlessStage.WaitingForGateway;
                    Stage("remotePresetSelected");
                    return;

                case HeadlessStage.WaitingForGateway:
                    var gatewayRuntime = entry.Get<IMultiplayerGatewayRuntime>();
                    if (gatewayRuntime.ConnectionState != ConnectionState.Connected)
                    {
                        Stage("waitingForGateway." + gatewayRuntime.ConnectionState);
                        return;
                    }

                    StartOperation(
                        _controller.StartCreateRoomAsync(
                            new MultiplayerRoomLaunchSpec
                            {
                                Region = _runtimePreset.GatewaySO.Region,
                                ServerId = _runtimePreset.GatewaySO.ServerId,
                                RoomType = "moba",
                                RoomTitle = "Unity single-client frame-sync acceptance",
                                MaxPlayers = 1,
                                MinPlayers = 1
                            },
                            _lifetime.Token),
                        HeadlessStage.CreatingRoom,
                        "creatingSinglePlayerRoom");
                    return;

                case HeadlessStage.CreatingRoom:
                    if (!CompleteOperation("createRoom")) return;
                    ConfigureAuthoritativePlayer();
                    var initial = _runtimePlayers.Team1Players[0];
                    StartOperation(
                        _controller.PickHeroAsync(
                            new MultiplayerLoadoutSpec(
                                initial.HeroId,
                                (int)initial.TeamId,
                                initial.SpawnIndex,
                                initial.Level,
                                initial.AttributeTemplateId,
                                initial.BasicAttackSkillId,
                                initial.SkillIds),
                            _lifetime.Token),
                        HeadlessStage.ConfiguringHero,
                        "configuringInitialHero");
                    return;

                case HeadlessStage.ConfiguringHero:
                    if (!CompleteOperation("pickHero")) return;
                    StartOperation(
                        _controller.SetReadyAsync(true, _lifetime.Token),
                        HeadlessStage.SettingReady,
                        "settingReady");
                    return;

                case HeadlessStage.SettingReady:
                    if (!CompleteOperation("setReady")) return;
                    if (_controller.CurrentSnapshot == null || !_controller.CurrentSnapshot.CanStart)
                    {
                        throw new InvalidOperationException("One-player MOBA room did not become startable.");
                    }

                    StartOperation(
                        _controller.BeginLoadingAsync(_lifetime.Token),
                        HeadlessStage.BeginningLoading,
                        "beginningLoading");
                    return;

                case HeadlessStage.BeginningLoading:
                    if (!CompleteOperation("beginLoading")) return;
                    StartOperation(
                        _controller.ReportAssetsLoadedAsync(_lifetime.Token),
                        HeadlessStage.ReportingAssets,
                        "reportingAssetsLoaded");
                    return;

                case HeadlessStage.ReportingAssets:
                    if (!CompleteOperation("reportAssetsLoaded")) return;
                    StartOperation(
                        _controller.WaitForBattleStartAsync(_lifetime.Token),
                        HeadlessStage.WaitingForBattleStart,
                        "waitingForAuthoritativeBattleStart");
                    return;

                case HeadlessStage.WaitingForBattleStart:
                    if (!CompleteOperation("waitForBattleStart")) return;
                    _stage = HeadlessStage.WaitingForInMatch;
                    Stage("waitingForFormalLobbyBattleEntry");
                    return;

                case HeadlessStage.WaitingForInMatch:
                    if (_flow.CurrentBattlePhase != MobaBattleState.InMatch ||
                        !entry.TryGet(out BattleContext context) ||
                        !TryResolvePlayerActor(context, out _oldActorId))
                    {
                        Stage("waitingForInMatchAndLocalActor");
                        return;
                    }

                    context.LocalActorId = _oldActorId;
                    _baselineRevision = context.RuntimePlayerLoadoutRevision;
                    _submittedFrame = SessionSimRuntimeTuning.ResolveInputSubmitFrame(
                        context.LastFrame,
                        in context.Plan);
                    var playerId = new PlayerId(_playerId);
                    var command = BattleInputCommandFactory.CreateDebugReplaceHero(
                        _submittedFrame,
                        playerId,
                        ReplacementHeroId);
                    new BattleInputSubmitter(context, playerId, new WorldId(context.Plan.World.WorldId))
                        .Submit(in command);
                    _replacementDeadlineUtc = DateTime.UtcNow + ReplacementTimeout;
                    _stage = HeadlessStage.WaitingForReplacement;
                    Stage("replacementSubmitted");
                    return;

                case HeadlessStage.WaitingForReplacement:
                    if (!entry.TryGet(out BattleContext ctx))
                    {
                        Stage("waitingForReplacementContext");
                        return;
                    }

                    if (!TryValidateReplacement(ctx, out var validation))
                    {
                        if (_replacementDeadlineUtc != default && DateTime.UtcNow > _replacementDeadlineUtc)
                        {
                            throw new TimeoutException("Hero replacement did not return through authoritative frames. " + validation);
                        }

                        Stage("waitingForAuthoritativeReplacement." + validation);
                        return;
                    }

                    Finish(true, BuildSuccess(ctx, validation));
                    return;

                default:
                    throw new InvalidOperationException("Unsupported headless stage: " + _stage);
            }
        }

        private static void ConfigureAuthoritativePlayer()
        {
            var snapshot = RequireRoomSnapshot();
            var player = snapshot.Players.SingleOrDefault();
            if (player == null || player.PlayerId == 0)
            {
                throw new InvalidOperationException("Single-player room did not expose an authoritative player id.");
            }

            _authoritativePlayerId = player.PlayerId;
            _playerId = player.PlayerId.ToString(CultureInfo.InvariantCulture);
            _runtimePlayers.LocalPlayerId = _playerId;
            _runtimePlayers.Team1Players = new List<BattlePlayersConfigSO.PlayerConfig>
            {
                new BattlePlayersConfigSO.PlayerConfig
                {
                    PlayerId = _playerId,
                    TeamId = Team.Team1,
                    MainType = EntityMainType.Unit,
                    UnitSubType = UnitSubType.Hero,
                    HeroId = InitialHeroId,
                    AttributeTemplateId = 1001,
                    Level = 1,
                    BasicAttackSkillId = 10010001,
                    SkillIds = new[] { 10010101, 10010201, 10010301 },
                    SpawnIndex = 0,
                    SpawnPosition = Vector3.zero
                }
            };
            _runtimePlayers.Team2Players = new List<BattlePlayersConfigSO.PlayerConfig>();
            Stage("authoritativePlayerConfigured." + _playerId);
        }

        private static bool TryValidateReplacement(BattleContext context, out string detail)
        {
            detail = string.Empty;
            if (context.LastFrame < _submittedFrame)
            {
                detail = $"framePending(last={context.LastFrame},submitted={_submittedFrame})";
                return false;
            }

            if (!TryResolvePlayerActor(context, out _newActorId) || _newActorId <= 0 || _newActorId == _oldActorId)
            {
                detail = $"actorMapPending(old={_oldActorId},new={_newActorId})";
                return false;
            }

            if (context.LocalActorId != _newActorId)
            {
                detail = $"localActorPending(local={context.LocalActorId},new={_newActorId})";
                return false;
            }

            if (context.RuntimePlayerLoadoutRevision <= _baselineRevision)
            {
                detail = $"loadoutRevisionPending(baseline={_baselineRevision},current={context.RuntimePlayerLoadoutRevision})";
                return false;
            }

            var loadout = context.BuildEffectivePlayerLoadouts()
                .SingleOrDefault(candidate => string.Equals(candidate.PlayerId.Value, _playerId, StringComparison.Ordinal));
            if (loadout.HeroId != ReplacementHeroId ||
                loadout.AttributeTemplateId <= 0 ||
                loadout.BasicAttackSkillId <= 0 ||
                loadout.SkillIds == null ||
                loadout.SkillIds.Length == 0)
            {
                detail = $"loadoutPending(hero={loadout.HeroId},attr={loadout.AttributeTemplateId},basic={loadout.BasicAttackSkillId},skills={loadout.SkillIds?.Length ?? 0})";
                return false;
            }

            if (!TryGetEntityManager(context, out var entities) ||
                !entities.TryGetActorEntity(_newActorId, out var replacement) ||
                replacement == null ||
                !replacement.hasSkillLoadout ||
                !replacement.hasAttributeGroup)
            {
                detail = "replacementEntityIncomplete";
                return false;
            }

            var oldLifecycle = "despawned";
            if (entities.TryGetActorEntity(_oldActorId, out var previous) && previous != null)
            {
                if (!previous.hasActorDespawnRequest ||
                    previous.actorDespawnRequest.Reason != ActorDespawnReason.HeroReplaced)
                {
                    detail = "previousActorLifecyclePending";
                    return false;
                }

                oldLifecycle = "HeroReplaced";
            }

            detail = $"loadout(hero={loadout.HeroId},attr={loadout.AttributeTemplateId},basic={loadout.BasicAttackSkillId},skills={string.Join(",", loadout.SkillIds)}),oldLifecycle={oldLifecycle}";
            return true;
        }

        private static bool TryResolvePlayerActor(BattleContext context, out int actorId)
        {
            actorId = 0;
            if (context == null ||
                !context.TryGetRuntimeWorld(out var world) ||
                world.Services == null ||
                !world.Services.TryResolve<MobaPlayerActorMapService>(out var map) ||
                map == null)
            {
                return false;
            }

            return map.TryGetActorId(new PlayerId(_playerId), out actorId) && actorId > 0;
        }

        private static bool TryGetEntityManager(BattleContext context, out MobaEntityManager entities)
        {
            entities = null;
            return context != null &&
                   context.TryGetRuntimeWorld(out var world) &&
                   world.Services != null &&
                   world.Services.TryResolve(out entities) &&
                   entities != null;
        }

        private static ClientRoomSnapshot RequireRoomSnapshot()
        {
            return _roomStore?.Current ??
                   throw new InvalidOperationException("Authoritative room snapshot is unavailable.");
        }

        private static void StartOperation(Task operation, HeadlessStage stage, string diagnosticStage)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
            _stage = stage;
            Stage(diagnosticStage);
        }

        private static bool CompleteOperation(string operationName)
        {
            if (_operation == null)
            {
                throw new InvalidOperationException(operationName + " operation was not started.");
            }

            if (!_operation.IsCompleted)
            {
                Stage("waitingFor." + operationName);
                return false;
            }

            if (_operation.IsCanceled)
            {
                throw new OperationCanceledException(operationName + " was canceled.");
            }

            if (_operation.IsFaulted)
            {
                throw _operation.Exception?.GetBaseException() ??
                      new InvalidOperationException(operationName + " failed.");
            }

            _operation = null;
            return true;
        }

        private static string BuildSuccess(BattleContext context, string validation)
        {
            var snapshot = RequireRoomSnapshot();
            return "PASS: one-client authoritative frame-sync hero replacement completed. " +
                   $"roomId={snapshot.RoomId},numericRoomId={snapshot.NumericRoomId},worldId={snapshot.WorldId}," +
                   $"playerId={_authoritativePlayerId},submittedFrame={_submittedFrame},observedFrame={context.LastFrame}," +
                   $"oldActorId={_oldActorId},newActorId={_newActorId}," +
                   $"loadoutRevision={context.RuntimePlayerLoadoutRevision},{validation}";
        }

        private static string BuildDiagnostic()
        {
            var remaining = _deadlineUtc == default
                ? "n/a"
                : Math.Max(0d, (_deadlineUtc - DateTime.UtcNow).TotalSeconds).ToString("F1", CultureInfo.InvariantCulture);
            return $"stage={_lastStage ?? "notStarted"},updates={_updates},remainingSeconds={remaining},roomState={_controller?.CurrentState.ToString() ?? "n/a"}," +
                   $"roomError={_controller?.LastError ?? "n/a"},roomId={_roomStore?.Current?.RoomId ?? "n/a"}," +
                   $"playerId={_playerId ?? "n/a"},battlePhase={_flow?.CurrentBattlePhase.ToString() ?? "n/a"}," +
                   $"oldActorId={_oldActorId},newActorId={_newActorId},submittedFrame={_submittedFrame}," +
                   BuildInputDiagnostic();
        }

        private static string BuildInputDiagnostic()
        {
            var jitter = BattleFlowDebugProvider.JitterBufferStats;
            var jitterText = jitter == null
                ? "jitter=n/a"
                : $"jitter(target={jitter.TargetFrame},max={jitter.MaxReceivedFrame},consumed={jitter.LastConsumedFrame}," +
                  $"buffered={jitter.BufferedCount},added={jitter.AddedCount},late={jitter.LateCount},filled={jitter.FilledDefaultCount})";

            var context = BattleFlowDebugProvider.Current;
            if (context == null ||
                !context.TryGetRuntimeWorld(out var world) ||
                world.Services == null ||
                !world.Services.TryResolve<IMobaBattleDiagnosticsService>(out var diagnostics) ||
                diagnostics == null)
            {
                return jitterText + ",inputWarning=n/a";
            }

            var warning = diagnostics.GetWarningsSnapshot()
                .LastOrDefault(item => item.Key.StartsWith("input.", StringComparison.Ordinal));
            return string.IsNullOrEmpty(warning.Key)
                ? jitterText + ",inputWarning=none"
                : $"{jitterText},inputWarning={warning.Key}:{warning.Message}";
        }

        private static void Stage(string stage)
        {
            if (string.Equals(_lastStage, stage, StringComparison.Ordinal) && _updates % 120 != 0)
            {
                return;
            }

            _lastStage = stage;
            Debug.Log("[MultiplayerHeadlessHeroReplacementCommand] " + BuildDiagnostic());
        }

        private static void Finish(bool success, string message)
        {
            var resultPath = SessionState.GetString(
                ResultPathKey,
                Path.GetFullPath("../MultiplayerHeadlessHeroReplacement.xml"));
            SessionState.EraseBool(RunningKey);
            SessionState.EraseString(ResultPathKey);
            _lifetime?.Cancel();
            WriteResult(resultPath, success, message);
            Debug.Log("[MultiplayerHeadlessHeroReplacementCommand] " + message);
            EditorApplication.update -= ContinueInPlayMode;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            EditorApplication.Exit(success ? 0 : 1);
        }

        private static void CreateRuntimeConfiguration()
        {
            var gateway = LoadAsset<BattleGatewayConfigSO>(GatewayConfigPath);
            gateway.Host = GetArgValue("-gatewayHost") ?? "127.0.0.1";
            gateway.Port = GetIntArgValue("-gatewayPort", 41101);
            gateway.Region = GetArgValue("-gatewayRegion") ?? "local";
            gateway.ServerId = GetArgValue("-gatewayServerId") ?? "moba-smoke";
            gateway.UseGatewayTransport = true;

            _runtimePlayers = ScriptableObject.CreateInstance<BattlePlayersConfigSO>();
            _runtimePlayers.hideFlags = HideFlags.DontSave;
            _runtimePlayers.Team1Players = new List<BattlePlayersConfigSO.PlayerConfig>();
            _runtimePlayers.Team2Players = new List<BattlePlayersConfigSO.PlayerConfig>();

            _runtimePreset = Object.Instantiate(LoadAsset<BattleStartPresetSO>(RemotePresetPath));
            _runtimePreset.hideFlags = HideFlags.DontSave;
            _runtimePreset.GatewaySO = gateway;
            _runtimePreset.PlayersSO = _runtimePlayers;

            _runtimeConfig = Object.Instantiate(LoadAsset<BattleStartConfig>(StartConfigPath));
            _runtimeConfig.hideFlags = HideFlags.DontSave;
            _runtimeConfig.Preset = _runtimePreset;
            _runtimeConfig.GatewaySO = gateway;
            _runtimeConfig.PlayersSO = _runtimePlayers;
        }

        private static void ResetState()
        {
            _updates = 0;
            _lastStage = null;
            _stage = HeadlessStage.WaitingForEntry;
            _operation = null;
            _lifetime?.Dispose();
            _lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            _runtimeConfig = null;
            _runtimePreset = null;
            _runtimePlayers = null;
            _controller = null;
            _roomSession = null;
            _roomStore = null;
            _flow = null;
            _authoritativePlayerId = 0;
            _playerId = null;
            _oldActorId = 0;
            _newActorId = 0;
            _submittedFrame = 0;
            _baselineRevision = 0;
            _deadlineUtc = default;
            _replacementDeadlineUtc = default;
        }

        private static T LoadAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new FileNotFoundException("Required Unity asset was not found.", path);
            }

            return asset;
        }

        private static string GetArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static int GetIntArgValue(string name, int fallback)
        {
            return int.TryParse(GetArgValue(name), out var value) ? value : fallback;
        }

        private static void WriteResult(string path, bool success, string message)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message ?? string.Empty));
            File.WriteAllText(
                path,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                $"<multiplayerHeadlessHeroReplacement success=\"{success.ToString().ToLowerInvariant()}\">\n" +
                $"  <message encoding=\"base64\">{encoded}</message>\n" +
                "</multiplayerHeadlessHeroReplacement>\n");
        }

        private enum HeadlessStage
        {
            WaitingForEntry,
            WaitingForGateway,
            CreatingRoom,
            ConfiguringHero,
            SettingReady,
            BeginningLoading,
            ReportingAssets,
            WaitingForBattleStart,
            WaitingForInMatch,
            WaitingForReplacement
        }
    }
}
