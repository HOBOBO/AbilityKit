using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using AbilityKit.World.ECS;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.EntityCreation;
using AbilityKit.Game.Flow;
using AbilityKit.Game.View.Modules;
using AbilityKit.Network.Sdk;
using UnityEngine;

namespace AbilityKit.Game
{
    public sealed class GameEntry : MonoBehaviour, IGameHost
    {
        private static GameEntry _instance;

        [SerializeField] private bool _debugEnabled;
        [SerializeField] private BattleGatewayConfigSO _multiplayerGatewayConfig;
        [SerializeField] private BattleStartConfig _battleStartConfig;
        [SerializeField] private BattleStartPresetSO[] _battleStartPresets;

        public static GameEntry Instance
        {
            get
            {
                if (_instance == null) throw new InvalidOperationException("GameEntry is not initialized");
                return _instance;
            }
        }

        public static bool IsInitialized => _instance != null;

        public bool DebugEnabled
        {
            get => _debugEnabled;
            set => _debugEnabled = value;
        }

        public BattleStartConfig BattleStartConfig => _battleStartConfig;
        public IReadOnlyList<BattleStartPresetSO> BattleStartPresets => _battleStartPresets;

        public EntityWorld World { get; private set; }
        public IEntity Root { get; private set; }

        private ModuleHost<GameEntryModuleContext, IGameEntryModule> _entryModules;
        private GameEntryModuleContext _entryModuleContext;
        private GameEntryRuntimeGuiBridge _runtimeGuiBridge;
        private DemoMultiplayerLaunchRequest _multiplayerLaunchRequest;
        private Task _shutdownTask;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (_instance == this && _entryModules != null && _entryModules.IsAttached)
            {
                return;
            }

            _instance = this;

            // Install the sink before constructing the flow domain, which captures Log.Sink.
            GameEntryBootstrap.TryInstallUnityLogSink();

            World = new EntityWorld();
            Root = EntityGenerator.CreateRoot(World, "GameRoot");

            if (!Root.TryGetRef<GameFlowDomain>(out var existingFlow))
            {
                var flow = new GameFlowDomain(this, Root, new GamePresentationSink());
                Root.WithRef(flow);
                Root.WithRef<IFlowCommandSink>(flow);
                Root.WithRef<IGameFlowFeatureInstaller>(flow);
                Root.WithRef<IBattleViewFeatureHost>(flow);
            }
            else
            {
                Root.WithRef<IFlowCommandSink>(existingFlow);
                Root.WithRef<IGameFlowFeatureInstaller>(existingFlow);
                Root.WithRef<IBattleViewFeatureHost>(existingFlow);
            }

            var entrySelection = new LobbyBattleEntrySelection();
            Root.WithRef(entrySelection);
            ApplyPendingLaunchIntent(entrySelection);
            EnsureRuntimeGuiBridge();

            _entryModuleContext = new GameEntryModuleContext(this, Root);
            _entryModules = CreateEntryModules();
            if (_entryModules.TrySortByDependencies())
            {
                _entryModules.Attach(in _entryModuleContext);
            }
        }

        private void Start()
        {
            if (_shutdownTask != null || !Root.IsValid) return;
            if (Root.TryGetRef<GameFlowDomain>(out var flow))
            {
                flow.Start();
            }
        }

        private void Update()
        {
            if (_shutdownTask != null || !Root.IsValid) return;

            _entryModules?.Tick(in _entryModuleContext, Time.deltaTime);

            if (Root.TryGetRef<GameFlowDomain>(out var flow))
            {
                flow.Tick(Time.deltaTime);
            }
        }

        private void OnGUI()
        {
            if (_runtimeGuiBridge == null)
            {
                DispatchRuntimeGUI(drawBridgeStatus: false);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                FlushReliableEventCheckpoints(
                    ReliableEventCheckpointFlushTrigger.ApplicationPause);
            }
        }

        private void OnApplicationQuit()
        {
            FlushReliableEventCheckpoints(
                ReliableEventCheckpointFlushTrigger.ApplicationQuit);
        }

        /// <summary>在 Unity 暂停或退出前同步提交可靠事件检查点。</summary>
        private void FlushReliableEventCheckpoints(
            ReliableEventCheckpointFlushTrigger trigger)
        {
            if (!Root.IsValid ||
                !Root.TryGetRef<IMobaReliableEventCheckpointLifecycle>(out var checkpoints) ||
                checkpoints == null)
            {
                return;
            }

            try
            {
                checkpoints.FlushAsync(trigger).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        internal void DispatchRuntimeGUI(bool drawBridgeStatus)
        {
            if (drawBridgeStatus)
            {
                DrawRuntimeGuiBridgeStatus();
            }

            if (!Root.IsValid) return;
            if (Root.TryGetRef<GameFlowDomain>(out var flow))
            {
                flow.OnGUI();
            }
        }

        private void EnsureRuntimeGuiBridge()
        {
            _runtimeGuiBridge = GetComponent<GameEntryRuntimeGuiBridge>();
            if (_runtimeGuiBridge == null)
            {
                _runtimeGuiBridge = gameObject.AddComponent<GameEntryRuntimeGuiBridge>();
            }

            _runtimeGuiBridge.Bind(this);
        }

        private void DrawRuntimeGuiBridgeStatus()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.BeginArea(new Rect(10f, Screen.height - 78f, 300f, 68f), "GameEntry GUI", GUI.skin.window);
            GUILayout.Label($"Debug: {_debugEnabled}");
            GUILayout.Label($"Root: {(Root.IsValid ? "valid" : "invalid")}");
            GUILayout.EndArea();
#endif
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            _ = ObserveShutdownAsync(ShutdownAsync());
        }

        public Task ShutdownAsync()
        {
            if (_shutdownTask != null) return _shutdownTask;

            var root = Root;
            var world = World;
            var modules = _entryModules;
            var moduleContext = _entryModuleContext;
            var flow = root.IsValid && root.TryGetRef<GameFlowDomain>(out var currentFlow)
                ? currentFlow
                : null;

            _shutdownTask = GameEntryShutdownCoordinator.RunAsync(
                () => flow?.ShutdownAsync() ?? Task.CompletedTask,
                () =>
                {
                    if (modules?.IsAttached == true) modules.Detach(in moduleContext);
                },
                () =>
                {
                    if (modules == null) return Task.CompletedTask;
                    var pending = new List<Task>();
                    foreach (var module in modules.Modules)
                    {
                        if (module is IAsyncGameEntryModuleTeardown asyncModule)
                            pending.Add(asyncModule.PendingTeardown ?? Task.CompletedTask);
                    }
                    return Task.WhenAll(pending);
                },
                () =>
                {
                    try
                    {
                        if (root.IsValid) world?.DestroyRecursive(root.Id);
                    }
                    finally
                    {
                        _entryModules = null;
                        _entryModuleContext = default;
                        Root = default;
                        World = null;
                    }
                });
            return _shutdownTask;
        }

        private static async Task ObserveShutdownAsync(Task shutdown)
        {
            try { await shutdown; }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private ModuleHost<GameEntryModuleContext, IGameEntryModule> CreateEntryModules()
        {
            var modules = new List<IGameEntryModule>
            {
                new GameEntryBootstrap()
            };
            if (_multiplayerGatewayConfig != null)
            {
                var assetLoading = new MultiplayerBattleAssetLoadingOptions(
                    ResourcesBattleAssetLoadService.Default,
                    ResourcesBattleAssetDependencyProvider.Default,
                    AbilityKit.Demo.Moba.Services.MobaGameplayTagCatalog.VerifyPublishedBundle(
                        new ResourcesTextAssetLoader()));
                modules.Add(new MultiplayerGatewayEntryModule(
                    _multiplayerGatewayConfig,
                    assetLoading,
                    _multiplayerLaunchRequest));
            }

            return new ModuleHost<GameEntryModuleContext, IGameEntryModule>(
                modules,
                message => Debug.LogError($"[GameEntry] {message}"));
        }

        private void ApplyPendingLaunchIntent(LobbyBattleEntrySelection selection)
        {
            if (!DemoMultiplayerLaunchIntent.TryConsume(
                    DemoMultiplayerGameplay.Moba,
                    out var launchRequest))
            {
                return;
            }

            _multiplayerLaunchRequest = launchRequest;

            BattleStartPresetSO remotePreset = null;
            var requestedSyncTemplate = DemoMultiplayerSyncTemplates.Normalize(
                launchRequest != null ? launchRequest.SyncTemplateId : null);
            if (_battleStartPresets != null)
            {
                for (var i = 0; i < _battleStartPresets.Length; i++)
                {
                    var candidate = _battleStartPresets[i];
                    if (candidate != null &&
                        candidate.HostMode == BattleHostMode.GatewayRemote &&
                        IsSyncTemplate(candidate, requestedSyncTemplate))
                    {
                        remotePreset = candidate;
                        break;
                    }
                }

            }

#if UNITY_EDITOR
            if (remotePreset != null &&
                HasCommandLineFlag("-mobaHeadlessEnableConfirmedAuthorityWorld", "1"))
            {
                remotePreset = Instantiate(remotePreset);
                remotePreset.name += "_HeadlessComparison";
                remotePreset.EnableConfirmedAuthorityWorld = true;
                Debug.Log("[GameEntry] Enabled confirmed authority world on an in-memory headless preset clone.");
            }
#endif

            if (_battleStartConfig == null || remotePreset == null)
            {
                throw new InvalidOperationException(
                    "Multiplayer starter requires a BattleStartConfig and a GatewayRemote preset.");
            }

            selection.SelectRemote(_battleStartConfig, remotePreset);
        }

        private static bool IsSyncTemplate(BattleStartPresetSO preset, string syncTemplateId)
        {
            if (preset == null) return false;

            return string.Equals(
                syncTemplateId,
                preset.SyncMode == BattleSyncMode.SnapshotAuthority
                    ? DemoMultiplayerSyncTemplates.MobaStateSyncAuthority
                    : DemoMultiplayerSyncTemplates.MobaSettledFrameSync,
                StringComparison.OrdinalIgnoreCase);
        }

#if UNITY_EDITOR
        private static bool HasCommandLineFlag(string name, string expectedValue)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < args.Length; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                    return string.Equals(args[index + 1], expectedValue, StringComparison.Ordinal);
            }
            return false;
        }
#endif

        public T Get<T>() where T : class
        {
            if (!Root.IsValid) throw new InvalidOperationException("Root entity is not valid");
            return Root.GetRef<T>();
        }

        public bool TryGet<T>(out T component) where T : class
        {
            if (!Root.IsValid)
            {
                component = default(T);
                return false;
            }

            return Root.TryGetRef(out component);
        }

        public void Set<T>(T component) where T : class
        {
            if (!Root.IsValid) throw new InvalidOperationException("Root entity is not valid");
            Root.WithRef(component);
        }

        public IEntity CreateNode(int childId)
        {
            if (!Root.IsValid) throw new InvalidOperationException("Root entity is not valid");
            return Root.World.CreateChild(Root, childId);
        }

        public IEntity GetNode(int childId)
        {
            if (!Root.IsValid) throw new InvalidOperationException("Root entity is not valid");
            Root.TryGetChildById(childId, out var node);
            return node;
        }

        public bool TryGetNode(int childId, out IEntity node)
        {
            if (!Root.IsValid)
            {
                node = default(IEntity);
                return false;
            }

            return Root.TryGetChildById(childId, out node);
        }

        public void RunCoroutine(IEnumerator coroutine)
        {
            StartCoroutine(coroutine);
        }
    }

    internal static class GameEntryShutdownCoordinator
    {
        internal static async Task RunAsync(
            Func<Task> stopFlow,
            Action detachModules,
            Func<Task> waitForModules,
            Action releaseRoot)
        {
            var failures = new List<Exception>();
            try { await (stopFlow?.Invoke() ?? Task.CompletedTask); }
            catch (Exception exception) { failures.Add(exception); }
            try { detachModules?.Invoke(); }
            catch (Exception exception) { failures.Add(exception); }
            try { await (waitForModules?.Invoke() ?? Task.CompletedTask); }
            catch (Exception exception) { failures.Add(exception); }
            try { releaseRoot?.Invoke(); }
            catch (Exception exception) { failures.Add(exception); }
            if (failures.Count > 0)
                throw new AggregateException("Game entry shutdown failed.", failures);
        }
    }

    internal sealed class GameEntryRuntimeGuiBridge : MonoBehaviour
    {
        private GameEntry _entry;
        private BattleLocalDebugController _localDebug;
        private string _localDebugMessage;
        private string _replaceHeroIdText = "1001";
        private Vector2 _heroOptionsScroll;
        private Vector2 _localDebugPanelScroll;
        private float _localDebugPanelScale = 1f;
        private bool _localDebugPanelVisible = true;
        private string _dummyOffsetX = "4";
        private string _dummyOffsetZ = "0";
        private bool _trainingRangesVisible;
        private int _trainingSkillSlot = 1;
        private string _trainingTargetActorId = string.Empty;
        private readonly BattleTrainingReadout _trainingReadout = new BattleTrainingReadout();
        private float _nextTrainingFailureRefresh;

        public void Bind(GameEntry entry)
        {
            if (!ReferenceEquals(_entry, entry))
            {
                _trainingReadout.Dispose();
                _localDebug = null;
                _localDebugMessage = null;
                _replaceHeroIdText = "1001";
                _localDebugPanelVisible = true;
                _localDebugPanelScale = 1f;
                _localDebugPanelScroll = Vector2.zero;
            }

            _entry = entry;
        }

        private void OnDisable() => _trainingReadout.Dispose();

        private void OnGUI()
        {
            if (_entry == null && GameEntry.IsInitialized)
            {
                _entry = GameEntry.Instance;
            }

            if (_entry == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                GUILayout.BeginArea(new Rect(10f, Screen.height - 58f, 300f, 48f), "GameEntry GUI", GUI.skin.window);
                GUILayout.Label("Entry: missing");
                GUILayout.EndArea();
#endif
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DrawLocalDebugShortcuts();
            _entry.DispatchRuntimeGUI(drawBridgeStatus: true);
#else
            _entry.DispatchRuntimeGUI(drawBridgeStatus: false);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 依据当前会话判断是否展示本地调试面板，不改变普通战斗的调试开关。
        /// </summary>
        internal bool ShouldDrawLocalDebugShortcuts()
        {
            if (_entry == null || !_entry.DebugEnabled) return false;

#if UNITY_EDITOR
            if (_entry.TryGet(out GameFlowDomain flow) &&
                flow.CurrentBattleProfileId == Battle.Testing.MobaBattleScenarioPlayback.PreviewProfileId)
                return false;
#endif

            _entry.TryGet(out IFlowCommandSink sink);
            var inBattle = sink != null && sink.CurrentRootPhase == MobaRootState.Battle;
            var battleContext = ResolveBattleContext();
            return !inBattle || (battleContext != null && battleContext.Plan.HostMode == BattleHostMode.Local);
        }
#endif

        private void DrawLocalDebugShortcuts()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!ShouldDrawLocalDebugShortcuts())
            {
                _trainingReadout.Dispose();
                return;
            }

            EnsureLocalDebugController();
            var debugSnapshot = _localDebug != null
                ? _localDebug.CaptureSnapshot()
                : default;
            _trainingReadout.Bind(debugSnapshot.IsAvailable ? ResolveBattleContext() : null);

            if (GUI.Button(
                    new Rect(10f, 10f, 150f, 32f),
                    _localDebugPanelVisible ? "隐藏调试面板" : "显示调试面板"))
            {
                _localDebugPanelVisible = !_localDebugPanelVisible;
            }

            if (!_localDebugPanelVisible) return;

            const float width = 300f;
            var height = Mathf.Min(520f, Screen.height - 60f);
            var previousMatrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(
                new Vector2(_localDebugPanelScale, _localDebugPanelScale),
                new Vector2(10f, 50f));
            GUILayout.BeginArea(new Rect(10f, 50f, width, height), "英雄技能训练", GUI.skin.window);
            GUILayout.BeginHorizontal();
            GUILayout.Label("面板缩放", GUILayout.Width(70f));
            _localDebugPanelScale = GUILayout.HorizontalSlider(
                _localDebugPanelScale,
                0.55f,
                1f,
                GUILayout.Width(150f));
            GUILayout.Label($"{Mathf.RoundToInt(_localDebugPanelScale * 100f)}%", GUILayout.Width(42f));
            GUILayout.EndHorizontal();
            _localDebugPanelScroll = GUILayout.BeginScrollView(_localDebugPanelScroll);
            var previousEnabled = GUI.enabled;
            GUI.enabled = _localDebug != null && debugSnapshot.IsAvailable;
            GUILayout.Label("英雄", GUI.skin.box);
            if (GUILayout.Button("切换主控单位（P1/P2）", GUILayout.Height(30f)))
            {
                RunLocalDebugAction(_localDebug.TrySwitchControl);
            }

            GUILayout.Label(debugSnapshot.CurrentHeroLevel > 0
                ? $"当前英雄等级：{debugSnapshot.CurrentHeroLevel}"
                : "当前英雄等级：—");
            var debugActionsEnabled = GUI.enabled;
            GUI.enabled = debugActionsEnabled && debugSnapshot.CurrentHeroLevel > 0;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("增加等级 (+1)", GUILayout.Height(30f)))
            {
                RunLocalDebugAction(_localDebug.TryIncreaseHeroLevel);
            }
            if (GUILayout.Button("重置等级", GUILayout.Height(30f)))
            {
                RunLocalDebugAction(_localDebug.TryResetHeroLevel);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = debugActionsEnabled;

            GUILayout.Label("完整替换当前英雄");
            var includePlaceholders = GUILayout.Toggle(
                debugSnapshot.IncludePlaceholderHeroes,
                "启用占位英雄");
            if (includePlaceholders != debugSnapshot.IncludePlaceholderHeroes)
            {
                _localDebug.SetIncludePlaceholderHeroes(includePlaceholders);
                debugSnapshot = _localDebug.CaptureSnapshot();
            }

            var heroOptions = debugSnapshot.HeroOptions ?? Array.Empty<BattleDebugHeroOption>();
            if (heroOptions.Length == 0)
            {
                GUILayout.Label("没有完整可用的战斗英雄配置");
            }
            else
            {
                _heroOptionsScroll = GUILayout.BeginScrollView(
                    _heroOptionsScroll,
                    GUILayout.Height(Mathf.Min(150f, ((heroOptions.Length + 1) / 2) * 34f + 6f)));
                for (var i = 0; i < heroOptions.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    DrawHeroOption(heroOptions[i]);
                    if (i + 1 < heroOptions.Length)
                    {
                        DrawHeroOption(heroOptions[i + 1]);
                    }
                    else
                    {
                        GUILayout.Space(136f);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("目标英雄 ID", GUILayout.Width(88f));
            _replaceHeroIdText = GUILayout.TextField(_replaceHeroIdText, GUILayout.Height(24f));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("按 ID 完整替换", GUILayout.Height(30f)))
            {
                if (!int.TryParse(_replaceHeroIdText, out var heroId) || heroId <= 0)
                {
                    _localDebugMessage = "请输入有效的英雄 ID";
                }
                else
                {
                    ReplaceHero(heroId);
                }
            }

            GUILayout.Label("技能与资源", GUI.skin.box);
            var noCooldown = false;
            var infiniteMana = false;
            _localDebug?.ReadTrainingOptions(out noCooldown, out infiniteMana);
            GUILayout.BeginHorizontal();
            var nextNoCooldown = GUILayout.Toggle(noCooldown, "技能无冷却");
            var nextInfiniteMana = GUILayout.Toggle(infiniteMana, "无限法力");
            GUILayout.EndHorizontal();
            if (nextNoCooldown != noCooldown || nextInfiniteMana != infiniteMana)
                _localDebug.TrySetTrainingOptions(nextNoCooldown, nextInfiniteMana, out _localDebugMessage);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重置技能冷却", GUILayout.Height(30f)))
            {
                RunLocalDebugAction(_localDebug.TryResetCooldowns);
            }
            if (GUILayout.Button("补满技能资源", GUILayout.Height(30f)))
            {
                RunLocalDebugAction(_localDebug.TryRestoreResources);
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("技能槽（同时用于范围观测）");
            _trainingSkillSlot = GUILayout.SelectionGrid(_trainingSkillSlot - 1, TrainingSlots, 6) + 1;
            if (_localDebug.TryReadSkillRank(_trainingSkillSlot, out var rank, out var maxRank))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"技能 Rank：{rank}/{maxRank}");
                var actionsEnabled = GUI.enabled;
                GUI.enabled = actionsEnabled && rank > 1;
                if (GUILayout.Button("−1"))
                    _localDebug.TrySetSkillRank(_trainingSkillSlot, rank - 1, out _localDebugMessage);
                if (GUILayout.Button("重置"))
                    _localDebug.TrySetSkillRank(_trainingSkillSlot, 1, out _localDebugMessage);
                GUI.enabled = actionsEnabled && rank < maxRank;
                if (GUILayout.Button("+1"))
                    _localDebug.TrySetSkillRank(_trainingSkillSlot, rank + 1, out _localDebugMessage);
                GUI.enabled = actionsEnabled;
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("该槽无可调整的技能等级表");
            }

            GUILayout.Label("目标与场景", GUI.skin.box);
            if (GUILayout.Button(debugSnapshot.IsEnemyAiEnabled ? "关闭敌方 AI" : "开启敌方 AI", GUILayout.Height(30f)))
            {
                RunLocalDebugAction(_localDebug.TryToggleEnemyAi);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("创建己方英雄", GUILayout.Height(30f)))
            {
                RunLocalDebugAction(_localDebug.TrySpawnAlly);
            }

            if (GUILayout.Button("创建敌方英雄", GUILayout.Height(30f)))
            {
                RunLocalDebugAction(_localDebug.TrySpawnEnemy);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("相对偏移 X / Z", GUILayout.Width(90f));
            _dummyOffsetX = GUILayout.TextField(_dummyOffsetX);
            _dummyOffsetZ = GUILayout.TextField(_dummyOffsetZ);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("放置固定靶", GUILayout.Height(28f)))
            {
                if (float.TryParse(_dummyOffsetX, NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetX) &&
                    float.TryParse(_dummyOffsetZ, NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetZ))
                    _localDebug.TrySpawnDummy(offsetX, offsetZ, out _localDebugMessage);
                else
                    _localDebugMessage = "请输入有效的 X/Z 偏移";
            }
            if (GUILayout.Button("清除固定靶", GUILayout.Height(28f)))
                RunLocalDebugAction(_localDebug.TryClearDummies);
            GUILayout.EndHorizontal();

            GUILayout.Label("观测", GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("目标 Actor ID", GUILayout.Width(95f));
            _trainingTargetActorId = GUILayout.TextField(_trainingTargetActorId);
            GUILayout.EndHorizontal();
            var validTarget = string.IsNullOrWhiteSpace(_trainingTargetActorId) ||
                (int.TryParse(_trainingTargetActorId, out var parsedTarget) && parsedTarget > 0);
            var targetId = validTarget && int.TryParse(_trainingTargetActorId, out var explicitTarget) ? explicitTarget : 0;
            if (validTarget)
                _trainingReadout.SetTarget(targetId);
            GUILayout.Label(validTarget ? (targetId > 0 ? $"伤害统计目标：#{targetId}" : "伤害统计：全部目标") : "请输入有效的目标 ID");
            GUILayout.Label($"单次命中 {_trainingReadout.LastHit:0.##}  /  累计 {_trainingReadout.TotalDamage:0.##}（{_trainingReadout.HitCount} 次）");
            if (GUILayout.Button("清零伤害统计")) _trainingReadout.ResetDamage();

            if (Event.current.type == EventType.Repaint && Time.unscaledTime >= _nextTrainingFailureRefresh)
            {
                _trainingReadout.RefreshFailure();
                _nextTrainingFailureRefresh = Time.unscaledTime + 0.25f;
            }
            GUILayout.Label("最近施法失败：" + (_trainingReadout.FailureText ?? "—"));

            _trainingRangesVisible = GUILayout.Toggle(_trainingRangesVisible, "显示普攻 / 技能范围");
            var drawRanges = false;
            var rangeCenter = Vector3.zero;
            var attackRange = 0f;
            var skillRange = 0f;
            if (debugSnapshot.IsAvailable && _trainingRangesVisible && validTarget)
            {
                if (targetId == 0) GUILayout.Label("距离目标：最近敌方单位");
                if (_localDebug.TryReadRanges(_trainingSkillSlot, targetId, out var position, out attackRange,
                        out skillRange, out var actualTarget, out var distance))
                {
                    GUILayout.Label($"普攻 {attackRange:0.##}  /  槽 {_trainingSkillSlot}：{skillRange:0.##}");
                    GUILayout.Label(actualTarget > 0 ? $"目标 #{actualTarget}：距离 {distance:0.##}" : "无可用目标");
                    rangeCenter = new Vector3(position.X, position.Y, position.Z);
                    drawRanges = true;
                }
            }
            GUI.enabled = previousEnabled;

            if (!string.IsNullOrEmpty(_localDebugMessage))
            {
                GUILayout.Label(_localDebugMessage);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
            if (drawRanges && Event.current.type == EventType.Repaint)
            {
                var camera = (BattleFlowDebugProvider.CurrentView as IViewFeatureRuntime)?.CameraController?.Camera ?? Camera.main;
                if (camera != null)
                {
                    DrawTrainingRange(camera, rangeCenter, attackRange, new Color(0.25f, 0.95f, 0.50f, 0.9f));
                    if (!Mathf.Approximately(skillRange, attackRange))
                        DrawTrainingRange(camera, rangeCenter, skillRange, new Color(0.35f, 0.76f, 1f, 0.9f));
                }
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly string[] TrainingSlots = { "1", "2", "3", "4", "5", "6" };

        private static void DrawTrainingRange(Camera camera, Vector3 center, float radius, Color color)
        {
            if (!(radius > 0f) || float.IsInfinity(radius)) return;
            const int segments = 48;
            var savedColor = GUI.color;
            var savedMatrix = GUI.matrix;
            GUI.color = color;
            var previous = camera.WorldToScreenPoint(center + Vector3.right * radius);
            for (var segment = 1; segment <= segments; segment++)
            {
                var theta = segment * (2f * Mathf.PI / segments);
                var point = center + new Vector3(Mathf.Cos(theta) * radius, 0f, Mathf.Sin(theta) * radius);
                var current = camera.WorldToScreenPoint(point);
                if (previous.z > 0f && current.z > 0f)
                {
                    var start = new Vector2(previous.x, Screen.height - previous.y);
                    var delta = new Vector2(current.x - previous.x, previous.y - current.y);
                    GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
                    GUI.DrawTexture(new Rect(start.x, start.y, delta.magnitude, 2f), Texture2D.whiteTexture);
                    GUI.matrix = savedMatrix;
                }
                previous = current;
            }
            GUI.color = savedColor;
        }
#endif

        private void DrawHeroOption(BattleDebugHeroOption option)
        {
            if (GUILayout.Button(option.DisplayName, GUILayout.Height(30f), GUILayout.Width(136f)))
            {
                ReplaceHero(option.HeroId);
            }
        }

        private void ReplaceHero(int heroId)
        {
            _replaceHeroIdText = heroId.ToString();
            _localDebug.TryReplaceHero(heroId, out _localDebugMessage);
        }

        private void EnsureLocalDebugController()
        {
            if (_localDebug != null) return;
            _localDebug = new BattleLocalDebugController(ResolveBattleContext, ResolveBattleHudFeature);
        }

        private BattleContext ResolveBattleContext()
        {
            var current = BattleFlowDebugProvider.Current;
            if (current != null) return current;
            return _entry != null && _entry.TryGet(out BattleContext ctx) ? ctx : null;
        }

        private BattleHudFeature ResolveBattleHudFeature()
        {
            var current = BattleFlowDebugProvider.CurrentHud;
            if (current != null) return current;
            return _entry != null && _entry.TryGet(out BattleHudFeature hud) ? hud : null;
        }

        private void RunLocalDebugAction(LocalDebugAction action)
        {
            if (action == null) return;
            action(out _localDebugMessage);
        }

        private delegate bool LocalDebugAction(out string message);
    }
}
