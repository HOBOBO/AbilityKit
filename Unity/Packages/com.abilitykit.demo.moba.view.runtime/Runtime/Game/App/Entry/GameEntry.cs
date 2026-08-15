using System;
using System.Collections;
using System.Collections.Generic;
using AbilityKit.World.ECS;
using AbilityKit.Demo.Common.Rooms;
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
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            World = new EntityWorld();
            Root = EntityGenerator.CreateRoot(World, "GameRoot");

            if (!Root.TryGetRef<GameFlowDomain>(out var existingFlow))
            {
                var flow = new GameFlowDomain(this, Root, new GamePresentationSink());
                Root.WithRef(flow);
                Root.WithRef<IFlowCommandSink>(flow);
                Root.WithRef<IGameFlowFeatureInstaller>(flow);
            }
            else
            {
                Root.WithRef<IFlowCommandSink>(existingFlow);
                Root.WithRef<IGameFlowFeatureInstaller>(existingFlow);
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
            if (!Root.IsValid) return;
            if (Root.TryGetRef<GameFlowDomain>(out var flow))
            {
                flow.Start();
            }
        }

        private void Update()
        {
            if (!Root.IsValid) return;

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
                !Root.TryGetRef<GatewayMultiplayerRoomSession>(out var session) ||
                session == null)
            {
                return;
            }

            try
            {
                session.FlushReliableEventCheckpointsAsync(trigger).GetAwaiter().GetResult();
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
            try
            {
                if (Root.IsValid && Root.TryGetRef<GameFlowDomain>(out var flow))
                {
                    try
                    {
                        flow.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }

                if (_entryModules != null && _entryModules.IsAttached)
                {
                    try
                    {
                        _entryModules.Detach(in _entryModuleContext);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
            finally
            {
                _entryModules = null;
                _entryModuleContext = default;

                try
                {
                    if (Root.IsValid)
                    {
                        World?.DestroyRecursive(Root.Id);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                finally
                {
                    Root = default;
                    World = null;
                    if (_instance == this) _instance = null;
                }
            }
        }

        private ModuleHost<GameEntryModuleContext, IGameEntryModule> CreateEntryModules()
        {
            var modules = new List<IGameEntryModule>
            {
                new GameEntryBootstrap()
            };
            if (_multiplayerGatewayConfig != null)
            {
                modules.Add(new MultiplayerGatewayEntryModule(
                    _multiplayerGatewayConfig,
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
                    out _multiplayerLaunchRequest))
            {
                return;
            }

            BattleStartPresetSO remotePreset = null;
            if (_battleStartPresets != null)
            {
                for (var i = 0; i < _battleStartPresets.Length; i++)
                {
                    var candidate = _battleStartPresets[i];
                    if (candidate != null && candidate.HostMode == BattleHostMode.GatewayRemote)
                    {
                        remotePreset = candidate;
                        break;
                    }
                }
            }

            if (_battleStartConfig == null || remotePreset == null)
            {
                throw new InvalidOperationException(
                    "Multiplayer starter requires a BattleStartConfig and a GatewayRemote preset.");
            }

            selection.SelectRemote(_battleStartConfig, remotePreset);
        }

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

    internal sealed class GameEntryRuntimeGuiBridge : MonoBehaviour
    {
        private GameEntry _entry;
        private BattleLocalDebugController _localDebug;
        private string _localDebugMessage;
        private string _replaceHeroIdText = "1001";
        private Vector2 _heroOptionsScroll;

        public void Bind(GameEntry entry)
        {
            if (!ReferenceEquals(_entry, entry))
            {
                _localDebug = null;
                _localDebugMessage = null;
                _replaceHeroIdText = "1001";
            }

            _entry = entry;
        }

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

        private void DrawLocalDebugShortcuts()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_entry == null || !_entry.DebugEnabled) return;

            _entry.TryGet(out IFlowCommandSink sink);
            var inBattle = sink != null && sink.CurrentRootPhase == MobaRootState.Battle;
            EnsureLocalDebugController();
            var debugSnapshot = _localDebug != null
                ? _localDebug.CaptureSnapshot()
                : default;

            const float width = 300f;
            var height = Mathf.Min(520f, Screen.height - 20f);
            GUILayout.BeginArea(new Rect(Screen.width - width - 10f, 10f, width, height), "英雄技能验收", GUI.skin.window);
            GUILayout.Label("版本：hero-replacement-v8");
            GUILayout.Label($"根阶段：{(sink != null ? sink.CurrentRootPhase.ToString() : "缺失")}");

            if (!inBattle)
            {
                GUILayout.Label("状态：等待进入战斗");
            }
            else if (_localDebug == null)
            {
                GUILayout.Label("状态：调试控制器缺失");
            }
            else
            {
                GUILayout.Label($"状态：{(debugSnapshot.IsAvailable ? "就绪" : debugSnapshot.UnavailableReason)}");
                GUILayout.Label($"模式：{debugSnapshot.HostModeName}");
                GUILayout.Label($"玩家：{debugSnapshot.CurrentPlayerId}");
                GUILayout.Label($"角色：{debugSnapshot.CurrentActorId}");
                GUILayout.Label($"当前英雄：{debugSnapshot.CurrentHeroId}");
            }

            var previousEnabled = GUI.enabled;
            GUI.enabled = _localDebug != null && debugSnapshot.IsAvailable;
            GUILayout.Label("完整替换当前英雄");
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

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重置技能冷却", GUILayout.Height(30f)))
            {
                RunLocalDebugAction(_localDebug.TryResetCooldowns);
            }
            if (GUILayout.Button(debugSnapshot.IsEnemyAiEnabled ? "关闭敌方 AI" : "开启敌方 AI", GUILayout.Height(30f)))
            {
                RunLocalDebugAction(_localDebug.TryToggleEnemyAi);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("创建己方单位", GUILayout.Height(30f)))
            {
                RunLocalDebugAction(_localDebug.TrySpawnAlly);
            }

            if (GUILayout.Button("创建敌方单位", GUILayout.Height(30f)))
            {
                RunLocalDebugAction(_localDebug.TrySpawnEnemy);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;

            if (!string.IsNullOrEmpty(_localDebugMessage))
            {
                GUILayout.Label(_localDebugMessage);
            }

            GUILayout.EndArea();
#endif
        }

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
