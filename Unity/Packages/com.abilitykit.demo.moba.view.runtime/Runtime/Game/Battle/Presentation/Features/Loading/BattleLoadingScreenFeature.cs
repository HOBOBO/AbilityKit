using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Battle.Presentation.Features.Loading
{
    /// <summary>
    /// 战斗加载界面 Feature。挂载在 <c>Battle.LoadAssets</c> 阶段：
    /// - 在 OnAttach 时构建并启动 <see cref="BattleAssetLoadCoordinator"/>
    /// - 通过当前 feature 实例的进度观察接口接收 scoped load 进度
    /// - OnGUI 渲染居中加载卡片 + 进度条 + 当前资源名 + 取消/重试/返回大厅按钮
    /// - 加载成功时由上层流程自动推进到 Battle.InMatch；失败/取消时显示错误并允许重试/返回大厅
    ///
    /// 默认从当前 <see cref="BattleSessionFeature"/> 构建 manifest；测试可通过
    /// <see cref="InjectCoordinator"/> 注入替代加载器。
    /// </summary>
    public sealed class BattleLoadingScreenFeature : IGamePhaseFeature, IOnGUIFeature, IBattleAssetLoadProgressObserver
    {
        private readonly BattleAssetLoadProgressSnapshot _snapshot = new BattleAssetLoadProgressSnapshot();

        private IBattleAssetLoadCoordinator _coordinator;
        private IFlowCommandSink _flowSink;
        private Action _assetsLoaded;
        private Action<IBattleAssetLease> _adoptLease;
        private bool _started;
        private bool _cancelRequested;
        private bool _completionPending;
        private string _statusLine = "Initializing...";
        private bool _show = true;

        public BattleLoadingScreenFeature()
        {
        }

        // 测试 / DI 入口
        internal BattleLoadingScreenFeature(IBattleAssetLoadCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        public void OnAttach(in GamePhaseContext ctx)
        {
            _flowSink = ctx.Entry.Get<IFlowCommandSink>();

            if (ctx.Features.TryGet(out BattleSessionFeature session))
            {
                _assetsLoaded = session.NotifyAssetsLoadCompleted;
                _adoptLease = session.AdoptAssetLease;

                if (_coordinator == null && TryAdoptPreloadedLease(ctx, session))
                {
                    return;
                }

                if (_coordinator != null)
                {
                    StartWith(_coordinator);
                    return;
                }

                var manifest = BattleAssetManifestResolver.Resolve(
                    new BattlePlanManifestSource(session.Plan),
                    ResourcesBattleAssetDependencyProvider.Default);
                _coordinator = new BattleAssetLoadCoordinator(
                    ResourcesBattleAssetLoadService.Default,
                    () => manifest,
                    new InlineProgress<BattleAssetLoadProgress>(OnProgress));
            }

            if (_coordinator != null)
            {
                StartWith(_coordinator);
            }
            else
            {
                _statusLine = "Battle asset loader is unavailable";
                _snapshot.Completed = true;
                _snapshot.Success = false;
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (_coordinator != null && _coordinator.IsLoading)
            {
                try
                {
                    _coordinator.Cancel();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[BattleLoadingScreen] Cancel failed: " + ex.Message);
                }
            }

            _coordinator?.ReleaseLease();
            _coordinator = null;
            _assetsLoaded = null;
            _adoptLease = null;
            _flowSink = null;
            _completionPending = false;
        }

        private bool TryAdoptPreloadedLease(
            in GamePhaseContext ctx,
            BattleSessionFeature session)
        {
            if (ctx.Entry == null ||
                !ctx.Entry.TryGet(out IBattleAssetLeaseTransferSource transferSource) ||
                transferSource == null)
            {
                return false;
            }

            var lease = transferSource.TakeLease();
            if (lease == null) return false;
            if (!lease.IsActive)
            {
                lease.Dispose();
                return false;
            }

            session.AdoptAssetLease(lease);
            _started = true;
            _show = false;
            _statusLine = "Load complete";
            _snapshot.IsLoading = false;
            _snapshot.Completed = true;
            _snapshot.Success = true;
            _snapshot.LoadedCount = 1;
            _snapshot.TotalCount = 1;
            _snapshot.CurrentAssetKey = string.Empty;
            _snapshot.ErrorMessage = string.Empty;
            _snapshot.Errors = Array.Empty<BattleAssetLoadError>();
            _completionPending = true;
            return true;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (!_completionPending) return;

            _completionPending = false;
            var completion = _assetsLoaded;
            completion?.Invoke();
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (!_show) return;
            DrawLoadingCard();
        }

        // ===== Scoped progress observer =====

        void IBattleAssetLoadProgressObserver.OnLoadStarted(BattleAssetLoadProgressSnapshot snapshot)
        {
            CopyFrom(snapshot);
            _statusLine = $"Loading {snapshot.TotalCount} asset(s)...";
        }

        void IBattleAssetLoadProgressObserver.OnLoadProgressed(BattleAssetLoadProgressSnapshot snapshot)
        {
            CopyFrom(snapshot);
            if (!string.IsNullOrEmpty(snapshot.CurrentAssetKey))
            {
                _statusLine = $"[{snapshot.LoadedCount}/{snapshot.TotalCount}] {snapshot.CurrentAssetKey}";
            }
            else
            {
                _statusLine = $"[{snapshot.LoadedCount}/{snapshot.TotalCount}]";
            }
        }

        void IBattleAssetLoadProgressObserver.OnLoadCompleted(BattleAssetLoadProgressSnapshot snapshot)
        {
            CopyFrom(snapshot);
            _statusLine = snapshot.Success
                ? "Load complete"
                : "Load failed: " + (snapshot.ErrorMessage ?? "unknown");
        }

        void IBattleAssetLoadProgressObserver.OnLoadCancelled(BattleAssetLoadProgressSnapshot snapshot)
        {
            CopyFrom(snapshot);
            _statusLine = "Cancelled";
        }

        // ===== Public hooks =====

        public BattleAssetLoadProgressSnapshot CurrentSnapshot => _snapshot;

        /// <summary>
        /// 注入 coordinator 并立即启动加载。供 GameFlow/Bootstrap 阶段使用。
        /// </summary>
        internal void InjectCoordinator(IBattleAssetLoadCoordinator coordinator)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            StartWith(_coordinator);
        }

        // ===== Internal =====

        private void CopyFrom(BattleAssetLoadProgressSnapshot src)
        {
            _snapshot.IsLoading = src.IsLoading;
            _snapshot.LoadedCount = src.LoadedCount;
            _snapshot.TotalCount = src.TotalCount;
            _snapshot.CurrentAssetKey = src.CurrentAssetKey;
            _snapshot.Completed = src.Completed;
            _snapshot.Success = src.Success;
            _snapshot.ErrorMessage = src.ErrorMessage;
            _snapshot.Errors = src.Errors;
        }

        private void StartWith(IBattleAssetLoadCoordinator coordinator)
        {
            if (_started) return;
            _started = true;
            _cancelRequested = false;
            _show = true;

            var snap = new BattleAssetLoadProgressSnapshot { IsLoading = true };
            ((IBattleAssetLoadProgressObserver)this).OnLoadStarted(snap);
            try
            {
                coordinator.StartLoading(success =>
                {
                    var result = coordinator.LastResult;
                    snap.Errors = result?.Errors ?? Array.Empty<BattleAssetLoadError>();
                    if (!success && snap.Errors.Count > 0)
                    {
                        snap.ErrorMessage = BuildErrorSummary(snap.Errors);
                    }

                    if (success && _adoptLease != null)
                    {
                        var lease = coordinator.TakeLease();
                        if (lease == null)
                        {
                            success = false;
                            snap.ErrorMessage = "Asset load completed without an active lease";
                        }
                        else
                        {
                            _adoptLease(lease);
                        }
                    }

                    snap.Completed = true;
                    snap.IsLoading = false;
                    snap.Success = success;
                    if (!success && string.IsNullOrEmpty(snap.ErrorMessage))
                    {
                        snap.ErrorMessage = "Load failed";
                    }
                    if (_cancelRequested)
                    {
                        ((IBattleAssetLoadProgressObserver)this).OnLoadCancelled(snap);
                    }
                    else
                    {
                        ((IBattleAssetLoadProgressObserver)this).OnLoadCompleted(snap);
                    }

                    if (success)
                    {
                        _show = false;
                        _completionPending = true;
                    }
                });
            }
            catch (Exception ex)
            {
                snap.Completed = true;
                snap.IsLoading = false;
                snap.Success = false;
                snap.ErrorMessage = ex.Message;
                ((IBattleAssetLoadProgressObserver)this).OnLoadCompleted(snap);
                Debug.LogWarning("[BattleLoadingScreen] Start failed: " + ex.Message);
            }
        }

        private static string BuildErrorSummary(IReadOnlyList<BattleAssetLoadError> errors)
        {
            if (errors == null || errors.Count == 0) return "Load failed";

            var first = errors[0];
            var keyOrPath = !string.IsNullOrEmpty(first.AssetKey)
                ? first.AssetKey
                : first.AssetPath;
            var summary = string.IsNullOrEmpty(keyOrPath)
                ? first.Reason
                : keyOrPath + ": " + first.Reason;
            return errors.Count > 1
                ? summary + " (and " + (errors.Count - 1) + " more)"
                : summary;
        }

        // ===== Rendering =====

        private void DrawLoadingCard()
        {
            const float cardWidth = 480f;
            const float cardHeight = 200f;
            var cx = Screen.width * 0.5f;
            var cy = Screen.height * 0.5f;
            var rect = new Rect(cx - cardWidth * 0.5f, cy - cardHeight * 0.5f, cardWidth, cardHeight);

            // dim background
            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(dim, Texture2D.whiteTexture);
            GUI.color = prevColor;

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("Loading Battle Assets", BoldHeaderStyle());
            GUILayout.Space(8f);

            GUILayout.Label(_statusLine);

            var progress = Mathf.Clamp01(_snapshot.Progress01);
            DrawProgressBar(progress);

            GUILayout.Space(8f);
            GUILayout.Label($"{_snapshot.LoadedCount} / {_snapshot.TotalCount}  ({Mathf.RoundToInt(progress * 100f)}%)");

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (_snapshot.IsLoading)
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(32f)))
                {
                    _cancelRequested = true;
                    try { _coordinator?.Cancel(); }
                    catch (Exception ex) { Debug.LogWarning(ex.Message); }
                }
            }
            else if (_snapshot.Completed && !_snapshot.Success)
            {
                if (GUILayout.Button("Retry", GUILayout.Height(32f)))
                {
                    _started = false;
                    if (_coordinator != null) StartWith(_coordinator);
                }
                if (GUILayout.Button("Back to Lobby", GUILayout.Height(32f)))
                {
                    _flowSink?.RequestReturnLobby();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private static GUIStyle _boldHeaderCache;
        private static GUIStyle BoldHeaderStyle()
        {
            if (_boldHeaderCache != null) return _boldHeaderCache;
            _boldHeaderCache = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 18 };
            return _boldHeaderCache;
        }

        private static void DrawProgressBar(float progress01)
        {
            const float barHeight = 22f;
            var rect = GUILayoutUtility.GetRect(0f, barHeight, GUILayout.ExpandWidth(true));
            var prev = GUI.color;

            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            var fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress01), rect.height);
            GUI.color = new Color(0.25f, 0.75f, 0.95f, 1f);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

            GUI.color = prev;
        }

        private void OnProgress(BattleAssetLoadProgress progress)
        {
            ((IBattleAssetLoadProgressObserver)this).OnLoadProgressed(new BattleAssetLoadProgressSnapshot
            {
                IsLoading = true,
                LoadedCount = progress.LoadedCount,
                TotalCount = progress.TotalCount,
                CurrentAssetKey = progress.CurrentAssetKey
            });
        }

        private sealed class BattlePlanManifestSource : IBattleAssetManifestSource
        {
            private readonly BattleStartPlan _plan;
            private readonly IReadOnlyList<IBattleAssetManifestPlayer> _players;

            public BattlePlanManifestSource(BattleStartPlan plan)
            {
                _plan = plan;
                var loadouts = plan.LaunchSpec.Players;
                if (loadouts == null || loadouts.Length == 0)
                {
                    _players = Array.Empty<IBattleAssetManifestPlayer>();
                    return;
                }

                var players = new IBattleAssetManifestPlayer[loadouts.Length];
                for (var i = 0; i < loadouts.Length; i++)
                {
                    players[i] = new BattlePlanManifestPlayer(loadouts[i]);
                }
                _players = players;
            }

            public IReadOnlyList<IBattleAssetManifestPlayer> Players => _players;
            public int LaunchManifestVersion => Math.Max(1, _plan.LaunchSpec.ConfigVersion);
            public string LaunchManifestHash =>
                "plan:" + (_plan.LaunchSpec.MatchId ?? _plan.World.WorldId ?? string.Empty) +
                ":" + LaunchManifestVersion;
            public long LaunchGeneration => 1L;
        }

        private sealed class BattlePlanManifestPlayer : IBattleAssetManifestPlayer
        {
            private readonly AbilityKit.Protocol.Moba.MobaPlayerLoadout _loadout;

            public BattlePlanManifestPlayer(AbilityKit.Protocol.Moba.MobaPlayerLoadout loadout)
            {
                _loadout = loadout;
            }

            public int HeroId => _loadout.HeroId;
            public int BasicAttackSkillId => _loadout.BasicAttackSkillId;
            public IReadOnlyList<int> SkillIds => _loadout.SkillIds ?? Array.Empty<int>();
        }

        private sealed class InlineProgress<T> : IProgress<T>
        {
            private readonly Action<T> _report;

            public InlineProgress(Action<T> report)
            {
                _report = report ?? throw new ArgumentNullException(nameof(report));
            }

            public void Report(T value)
            {
                _report(value);
            }
        }
    }
}
