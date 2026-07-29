#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Protocol.Shooter;
using AbilityKit.World.Svelto;
using Newtonsoft.Json;
using Svelto.DataStructures;
using Svelto.ECS;
using UnityHFSM;
using UnityHFSM.Extension;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal interface IShooterBotAiProfileProvider
    {
        ShooterBotAiConfig Resolve(string profileId);
    }

    internal sealed class ShooterBotAiProfileCatalogProvider : IShooterBotAiProfileProvider
    {
        public static ShooterBotAiProfileCatalogProvider Instance { get; } = new ShooterBotAiProfileCatalogProvider();

        private ShooterBotAiProfileCatalogProvider()
        {
        }

        public ShooterBotAiConfig Resolve(string profileId)
        {
            return ShooterBotAiProfileCatalog.Resolve(profileId);
        }
    }

    internal sealed class ShooterBotAiService : IShooterBotAiPort
    {
        private readonly IShooterBotAiRuntime _runtime;
        private readonly IShooterBotAiProfileProvider _profileProvider;

        public ShooterBotAiService(ShooterBotAiRuntime runtime)
            : this(runtime, ShooterBotAiProfileCatalogProvider.Instance)
        {
        }

        public ShooterBotAiService(ShooterBotAiRuntime runtime, IShooterBotAiProfileProvider profileProvider)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _profileProvider = profileProvider ?? throw new ArgumentNullException(nameof(profileProvider));
        }

        public int BotAiCount => _runtime.BotAiCount;

        public bool MountBotAi(in ShooterBotAiMountOptions options)
        {
            if (options.PlayerId <= 0 || options.Profile == ShooterBotAiProfile.None)
            {
                return false;
            }

            var config = _profileProvider.Resolve(options.ProfileId);
            return _runtime.MountBotAi(options.PlayerId, config);
        }

        public bool UnmountBotAi(int playerId)
        {
            return _runtime.UnmountBotAi(playerId);
        }

        public void ClearBotAi()
        {
            _runtime.ClearBotAi();
        }

        public void Tick(float deltaTime)
        {
            _runtime.Tick(deltaTime);
        }
    }

    internal sealed class ShooterBotAiController : IActionTimeSource
    {
        private readonly ShooterBattleState _state;
        private readonly IShooterEntityManager _entities;
        private readonly ShooterSpatialTargetIndex _targetIndex;
        private readonly ShooterBotAiConfig _config;
        private readonly StateMachine<string> _fsm;
        private float _deltaTime;
        private ShooterBotAiBlackboard _blackboard;

        private ShooterBotAiController(int playerId, ShooterBattleState state, IShooterEntityManager entities, ShooterSpatialTargetIndex targetIndex, ShooterBotAiConfig config)
        {
            PlayerId = playerId;
            _state = state;
            _entities = entities;
            _targetIndex = targetIndex;
            _config = config;
            _blackboard = new ShooterBotAiBlackboard(playerId, config);
            _fsm = new ShooterBotAiRuntimeBuilder().Build(this, _blackboard, config);
        }

        public int PlayerId { get; }

        public ShooterPlayerCommand Command => _blackboard.Command;

        public float DeltaTime => _deltaTime;

        public float UnscaledDeltaTime => _deltaTime;

        public static ShooterBotAiController Create(int playerId, ShooterBattleState state, IShooterEntityManager entities, ShooterSpatialTargetIndex targetIndex, ShooterBotAiConfig config)
        {
            return new ShooterBotAiController(playerId, state, entities, targetIndex, config ?? ShooterBotAiProfileCatalog.SimpleBattle);
        }

        public void Tick(float deltaTime)
        {
            _deltaTime = deltaTime;
            RefreshBlackboard();
            _fsm.OnLogic();
        }

        private void RefreshBlackboard()
        {
            _blackboard.Frame = _state.CurrentFrame;
            _blackboard.TargetPlayerId = 0;
            _blackboard.TargetDistanceSq = float.MaxValue;
            _blackboard.HasTarget = false;
            _blackboard.InAttackRange = false;

            if (!_targetIndex.TryGetLivePlayer(PlayerId, out var self))
            {
                return;
            }

            _blackboard.SelfX = self.X;
            _blackboard.SelfY = self.Y;

            if (_targetIndex.TryFindNearestTarget(self.X, self.Y, PlayerId, out var targetPlayerId, out var targetX, out var targetY, out var targetDistanceSq))
            {
                _blackboard.HasTarget = true;
                _blackboard.TargetPlayerId = targetPlayerId;
                _blackboard.TargetX = targetX;
                _blackboard.TargetY = targetY;
                _blackboard.TargetDistanceSq = targetDistanceSq;
            }

            var attackRangeSq = _config.AttackRange * _config.AttackRange;
            _blackboard.InAttackRange = _blackboard.HasTarget && _blackboard.TargetDistanceSq <= attackRangeSq;
        }
    }

    internal delegate IActionBehaviour ShooterBotAiActionFactory(ShooterBotAiBlackboard blackboard, ShooterBotAiActionConfig action);

    internal delegate bool ShooterBotAiConditionEvaluator(ShooterBotAiBlackboard blackboard);

    internal sealed class ShooterBotAiActionRegistry
    {
        private readonly Dictionary<string, ShooterBotAiActionFactory> _factories;

        public ShooterBotAiActionRegistry(IEnumerable<KeyValuePair<string, ShooterBotAiActionFactory>> factories)
        {
            _factories = new Dictionary<string, ShooterBotAiActionFactory>(StringComparer.OrdinalIgnoreCase);
            foreach (var factory in factories)
            {
                if (!string.IsNullOrWhiteSpace(factory.Key) && factory.Value != null)
                {
                    _factories[factory.Key] = factory.Value;
                }
            }
        }

        public IActionBehaviour Create(ShooterBotAiBlackboard blackboard, ShooterBotAiActionConfig action)
        {
            if (action != null && _factories.TryGetValue(action.Type, out var factory))
            {
                return factory(blackboard, action);
            }

            return new ShooterBotIdleBehaviour(blackboard);
        }

        public static ShooterBotAiActionRegistry Default { get; } = new ShooterBotAiActionRegistry(new[]
        {
            new KeyValuePair<string, ShooterBotAiActionFactory>(ShooterBotAiActionTypes.Idle, (blackboard, _) => new ShooterBotIdleBehaviour(blackboard)),
            new KeyValuePair<string, ShooterBotAiActionFactory>(ShooterBotAiActionTypes.Wander, (blackboard, action) => new ShooterBotWanderBehaviour(blackboard, action)),
            new KeyValuePair<string, ShooterBotAiActionFactory>(ShooterBotAiActionTypes.ChaseTarget, (blackboard, action) => new ShooterBotChaseBehaviour(blackboard, action)),
            new KeyValuePair<string, ShooterBotAiActionFactory>(ShooterBotAiActionTypes.AttackTarget, (blackboard, action) => new ShooterBotAttackBehaviour(blackboard, action))
        });
    }

    internal sealed class ShooterBotAiConditionRegistry
    {
        private readonly Dictionary<string, ShooterBotAiConditionEvaluator> _evaluators;

        public ShooterBotAiConditionRegistry(IEnumerable<KeyValuePair<string, ShooterBotAiConditionEvaluator>> evaluators)
        {
            _evaluators = new Dictionary<string, ShooterBotAiConditionEvaluator>(StringComparer.OrdinalIgnoreCase);
            foreach (var evaluator in evaluators)
            {
                if (!string.IsNullOrWhiteSpace(evaluator.Key) && evaluator.Value != null)
                {
                    _evaluators[evaluator.Key] = evaluator.Value;
                }
            }
        }

        public bool Evaluate(ShooterBotAiBlackboard blackboard, string condition)
        {
            return !string.IsNullOrWhiteSpace(condition)
                && _evaluators.TryGetValue(condition, out var evaluator)
                && evaluator(blackboard);
        }

        public static ShooterBotAiConditionRegistry Default { get; } = new ShooterBotAiConditionRegistry(new[]
        {
            new KeyValuePair<string, ShooterBotAiConditionEvaluator>(ShooterBotAiConditions.HasTarget, blackboard => blackboard.HasTarget),
            new KeyValuePair<string, ShooterBotAiConditionEvaluator>(ShooterBotAiConditions.NoTarget, blackboard => !blackboard.HasTarget),
            new KeyValuePair<string, ShooterBotAiConditionEvaluator>(ShooterBotAiConditions.InAttackRange, blackboard => blackboard.InAttackRange),
            new KeyValuePair<string, ShooterBotAiConditionEvaluator>(ShooterBotAiConditions.OutOfAttackRange, blackboard => blackboard.HasTarget && !blackboard.InAttackRange)
        });
    }

    internal sealed class ShooterBotAiRuntimeBuilder
    {
        private readonly HfsmHierarchicalRuntimeProfileBuilder<ShooterBotAiBlackboard, ShooterBotAiActionConfig> _builder;

        public ShooterBotAiRuntimeBuilder()
            : this(ShooterBotAiActionRegistry.Default, ShooterBotAiConditionRegistry.Default)
        {
        }

        public ShooterBotAiRuntimeBuilder(ShooterBotAiActionRegistry actions, ShooterBotAiConditionRegistry conditions)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (conditions == null) throw new ArgumentNullException(nameof(conditions));

            _builder = new HfsmHierarchicalRuntimeProfileBuilder<ShooterBotAiBlackboard, ShooterBotAiActionConfig>(
                actions.Create,
                conditions.Evaluate);
        }

        public StateMachine<string> Build(IActionTimeSource timeSource, ShooterBotAiBlackboard blackboard, ShooterBotAiConfig config)
        {
            return _builder.Build(timeSource, blackboard, config.ToHfsmRuntimeProfile());
        }
    }

    internal sealed class ShooterBotAiBlackboard
    {
        public ShooterBotAiBlackboard(int playerId, ShooterBotAiConfig config)
        {
            PlayerId = playerId;
            Config = config;
            Command = new ShooterPlayerCommand(playerId, 0f, 0f, 1f, 0f, false);
        }

        public int PlayerId { get; }

        public ShooterBotAiConfig Config { get; }

        public int Frame { get; set; }

        public float SelfX { get; set; }

        public float SelfY { get; set; }

        public bool HasTarget { get; set; }

        public bool InAttackRange { get; set; }

        public int TargetPlayerId { get; set; }

        public float TargetX { get; set; }

        public float TargetY { get; set; }

        public float TargetDistanceSq { get; set; }

        public ShooterPlayerCommand Command { get; set; }
    }

    internal sealed class ShooterBotIdleBehaviour : IActionBehaviour
    {
        private readonly ShooterBotAiBlackboard _blackboard;

        public ShooterBotIdleBehaviour(ShooterBotAiBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void Reset()
        {
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            _blackboard.Command = new ShooterPlayerCommand(_blackboard.PlayerId, 0f, 0f, 1f, 0f, false);
            return ActionBehaviourStatus.Running;
        }
    }

    internal sealed class ShooterBotWanderBehaviour : IActionBehaviour
    {
        private readonly ShooterBotAiBlackboard _blackboard;
        private readonly ShooterBotAiActionConfig _action;

        public ShooterBotWanderBehaviour(ShooterBotAiBlackboard blackboard, ShooterBotAiActionConfig action)
        {
            _blackboard = blackboard;
            _action = action;
        }

        public void Reset()
        {
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            var phase = (_blackboard.Frame + _blackboard.PlayerId * _action.PhaseOffset) * _action.PhaseScale;
            var moveX = MathF.Cos(phase) * _action.MoveScale;
            var moveY = MathF.Sin(phase) * _action.MoveScale;
            _blackboard.Command = new ShooterPlayerCommand(_blackboard.PlayerId, moveX, moveY, moveX, moveY, false);
            return ActionBehaviourStatus.Running;
        }
    }

    internal sealed class ShooterBotChaseBehaviour : IActionBehaviour
    {
        private readonly ShooterBotAiBlackboard _blackboard;
        private readonly ShooterBotAiActionConfig _action;

        public ShooterBotChaseBehaviour(ShooterBotAiBlackboard blackboard, ShooterBotAiActionConfig action)
        {
            _blackboard = blackboard;
            _action = action;
        }

        public void Reset()
        {
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            if (!_blackboard.HasTarget)
            {
                _blackboard.Command = new ShooterPlayerCommand(_blackboard.PlayerId, 0f, 0f, 1f, 0f, false);
                return ActionBehaviourStatus.Failure;
            }

            var dx = _blackboard.TargetX - _blackboard.SelfX;
            var dy = _blackboard.TargetY - _blackboard.SelfY;
            ShooterBotAiMath.Normalize(ref dx, ref dy);
            _blackboard.Command = new ShooterPlayerCommand(_blackboard.PlayerId, dx * _action.MoveScale, dy * _action.MoveScale, dx, dy, false);
            return ActionBehaviourStatus.Running;
        }
    }

    internal sealed class ShooterBotAttackBehaviour : IActionBehaviour
    {
        private readonly ShooterBotAiBlackboard _blackboard;
        private readonly ShooterBotAiActionConfig _action;

        public ShooterBotAttackBehaviour(ShooterBotAiBlackboard blackboard, ShooterBotAiActionConfig action)
        {
            _blackboard = blackboard;
            _action = action;
        }

        public void Reset()
        {
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            if (!_blackboard.HasTarget)
            {
                _blackboard.Command = new ShooterPlayerCommand(_blackboard.PlayerId, 0f, 0f, 1f, 0f, false);
                return ActionBehaviourStatus.Failure;
            }

            var aimX = _blackboard.TargetX - _blackboard.SelfX;
            var aimY = _blackboard.TargetY - _blackboard.SelfY;
            ShooterBotAiMath.Normalize(ref aimX, ref aimY);
            var strafePhase = (_blackboard.Frame + _blackboard.PlayerId * _action.PhaseOffset) * _action.PhaseScale;
            var moveX = -aimY * MathF.Cos(strafePhase) * _action.StrafeScale;
            var moveY = aimX * MathF.Cos(strafePhase) * _action.StrafeScale;
            var fireInterval = _action.FireInterval < 1 ? 1 : _action.FireInterval;
            var fire = _blackboard.Frame % fireInterval == _blackboard.PlayerId % fireInterval;
            _blackboard.Command = new ShooterPlayerCommand(_blackboard.PlayerId, moveX, moveY, aimX, aimY, fire);
            return ActionBehaviourStatus.Running;
        }
    }

    internal static class ShooterBotAiMath
    {
        public static void Normalize(ref float x, ref float y)
        {
            var lengthSq = x * x + y * y;
            if (lengthSq <= 0.000001f)
            {
                x = 1f;
                y = 0f;
                return;
            }

            var inv = 1f / MathF.Sqrt(lengthSq);
            x *= inv;
            y *= inv;
        }
    }

    internal sealed class ShooterBotAiConfig
    {
        public ShooterBotAiConfig(string id, string startState, float attackRange, IReadOnlyList<ShooterBotAiStateConfig> states, IReadOnlyList<ShooterBotAiTransitionConfig> transitions)
        {
            Id = string.IsNullOrWhiteSpace(id) ? ShooterBotAiProfileIds.SimpleBattle : id;
            StartState = string.IsNullOrWhiteSpace(startState) ? ShooterBotAiStateIds.Wander : startState;
            AttackRange = attackRange <= 0f ? 5.5f : attackRange;
            States = states == null || states.Count == 0 ? ShooterBotAiProfileCatalog.SimpleBattle.States : states;
            Transitions = transitions == null || transitions.Count == 0 ? ShooterBotAiProfileCatalog.SimpleBattle.Transitions : transitions;
        }

        public string Id { get; }

        public string StartState { get; }

        public float AttackRange { get; }

        public IReadOnlyList<ShooterBotAiStateConfig> States { get; }

        public IReadOnlyList<ShooterBotAiTransitionConfig> Transitions { get; }

        public HfsmHierarchicalRuntimeProfile<ShooterBotAiActionConfig> ToHfsmRuntimeProfile()
        {
            var states = new HfsmRuntimeNodeSpec<ShooterBotAiActionConfig>[States.Count];
            for (var i = 0; i < States.Count; i++)
            {
                var state = States[i];
                var children = new List<HfsmRuntimeBehaviourSpec<ShooterBotAiActionConfig>>(state.Actions.Count + 1);
                for (var actionIndex = 0; actionIndex < state.Actions.Count; actionIndex++)
                {
                    children.Add(HfsmRuntimeBehaviourSpec<ShooterBotAiActionConfig>.Task(state.Actions[actionIndex]));
                }

                if (state.Interval > 0f)
                {
                    children.Add(HfsmRuntimeBehaviourSpec<ShooterBotAiActionConfig>.Delay(state.Interval));
                }

                states[i] = new HfsmRuntimeNodeSpec<ShooterBotAiActionConfig>(
                    state.Id,
                    HfsmRuntimeBehaviourSpec<ShooterBotAiActionConfig>.Sequence(children.ToArray()),
                    ActionStateCompletionPolicy.Loop);
            }

            var transitions = new HfsmRuntimeTransitionSpec[Transitions.Count];
            for (var i = 0; i < Transitions.Count; i++)
            {
                var transition = Transitions[i];
                transitions[i] = new HfsmRuntimeTransitionSpec(transition.From, transition.To, transition.Condition);
            }

            return new HfsmHierarchicalRuntimeProfile<ShooterBotAiActionConfig>(
                Id,
                StartState,
                states,
                transitions);
        }
    }

    internal sealed class ShooterBotAiStateConfig
    {
        public ShooterBotAiStateConfig(string id, float interval, IReadOnlyList<ShooterBotAiActionConfig> actions)
        {
            Id = string.IsNullOrWhiteSpace(id) ? ShooterBotAiStateIds.Wander : id;
            Interval = interval < 0f ? 0f : interval;
            Actions = actions == null || actions.Count == 0
                ? new[] { ShooterBotAiActionConfig.Idle() }
                : actions;
        }

        public string Id { get; }

        public float Interval { get; }

        public IReadOnlyList<ShooterBotAiActionConfig> Actions { get; }
    }

    internal sealed class ShooterBotAiActionConfig
    {
        public ShooterBotAiActionConfig(string type, float moveScale, float strafeScale, float phaseScale, int phaseOffset, int fireInterval)
        {
            Type = string.IsNullOrWhiteSpace(type) ? ShooterBotAiActionTypes.Idle : type;
            MoveScale = moveScale;
            StrafeScale = strafeScale;
            PhaseScale = phaseScale;
            PhaseOffset = phaseOffset;
            FireInterval = fireInterval;
        }

        public string Type { get; }

        public float MoveScale { get; }

        public float StrafeScale { get; }

        public float PhaseScale { get; }

        public int PhaseOffset { get; }

        public int FireInterval { get; }

        public static ShooterBotAiActionConfig Idle()
        {
            return new ShooterBotAiActionConfig(ShooterBotAiActionTypes.Idle, 0f, 0f, 0.05f, 1, 1);
        }
    }

    internal sealed class ShooterBotAiTransitionConfig
    {
        public ShooterBotAiTransitionConfig(string from, string to, string condition)
        {
            From = string.IsNullOrWhiteSpace(from) ? ShooterBotAiStateIds.Wander : from;
            To = string.IsNullOrWhiteSpace(to) ? ShooterBotAiStateIds.Wander : to;
            Condition = string.IsNullOrWhiteSpace(condition) ? ShooterBotAiConditions.NoTarget : condition;
        }

        public string From { get; }

        public string To { get; }

        public string Condition { get; }
    }

    internal static class ShooterBotAiProfileCatalog
    {
        private static readonly IReadOnlyDictionary<string, ShooterBotAiConfig> Profiles = new Dictionary<string, ShooterBotAiConfig>(StringComparer.OrdinalIgnoreCase)
        {
            [ShooterBotAiProfileIds.SimpleBattle] = ShooterBotAiJsonParser.Parse(ShooterBotAiJsonCatalog.SimpleBattleJson)
        };

        public static ShooterBotAiConfig SimpleBattle => Profiles[ShooterBotAiProfileIds.SimpleBattle];

        public static ShooterBotAiConfig Resolve(string profileId)
        {
            if (!string.IsNullOrWhiteSpace(profileId) && Profiles.TryGetValue(profileId, out var profile))
            {
                return profile;
            }

            return SimpleBattle;
        }
    }

    internal static class ShooterBotAiJsonParser
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static ShooterBotAiConfig Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Shooter bot AI json is required.", nameof(json));
            }

            var dto = JsonConvert.DeserializeObject<ShooterBotAiConfigDto>(json, JsonSettings)
                ?? throw new InvalidOperationException("Shooter bot AI json cannot be parsed.");
            return dto.ToConfig();
        }
    }

    internal sealed class ShooterBotAiConfigDto
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("startState")]
        public string? StartState { get; set; }

        [JsonProperty("attackRange")]
        public float AttackRange { get; set; }

        [JsonProperty("states")]
        public List<ShooterBotAiStateConfigDto>? States { get; set; }

        [JsonProperty("transitions")]
        public List<ShooterBotAiTransitionConfigDto>? Transitions { get; set; }

        public ShooterBotAiConfig ToConfig()
        {
            var states = new List<ShooterBotAiStateConfig>();
            if (States != null)
            {
                for (var i = 0; i < States.Count; i++)
                {
                    states.Add(States[i].ToConfig());
                }
            }

            var transitions = new List<ShooterBotAiTransitionConfig>();
            if (Transitions != null)
            {
                for (var i = 0; i < Transitions.Count; i++)
                {
                    transitions.Add(Transitions[i].ToConfig());
                }
            }

            return new ShooterBotAiConfig(Id ?? string.Empty, StartState ?? string.Empty, AttackRange, states, transitions);
        }
    }

    internal sealed class ShooterBotAiStateConfigDto
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("interval")]
        public float Interval { get; set; }

        [JsonProperty("actions")]
        public List<ShooterBotAiActionConfigDto>? Actions { get; set; }

        public ShooterBotAiStateConfig ToConfig()
        {
            var actions = new List<ShooterBotAiActionConfig>();
            if (Actions != null)
            {
                for (var i = 0; i < Actions.Count; i++)
                {
                    actions.Add(Actions[i].ToConfig());
                }
            }

            return new ShooterBotAiStateConfig(Id ?? string.Empty, Interval, actions);
        }
    }

    internal sealed class ShooterBotAiActionConfigDto
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("moveScale")]
        public float MoveScale { get; set; }

        [JsonProperty("strafeScale")]
        public float StrafeScale { get; set; }

        [JsonProperty("phaseScale")]
        public float PhaseScale { get; set; }

        [JsonProperty("phaseOffset")]
        public int PhaseOffset { get; set; }

        [JsonProperty("fireInterval")]
        public int FireInterval { get; set; }

        public ShooterBotAiActionConfig ToConfig()
        {
            return new ShooterBotAiActionConfig(Type ?? string.Empty, MoveScale, StrafeScale, PhaseScale, PhaseOffset, FireInterval);
        }
    }

    internal sealed class ShooterBotAiTransitionConfigDto
    {
        [JsonProperty("from")]
        public string? From { get; set; }

        [JsonProperty("to")]
        public string? To { get; set; }

        [JsonProperty("condition")]
        public string? Condition { get; set; }

        public ShooterBotAiTransitionConfig ToConfig()
        {
            return new ShooterBotAiTransitionConfig(From ?? string.Empty, To ?? string.Empty, Condition ?? string.Empty);
        }
    }

    internal static class ShooterBotAiJsonCatalog
    {
        public const string SimpleBattleJson = "{\n" +
            "  \"id\": \"simple-battle\",\n" +
            "  \"startState\": \"Wander\",\n" +
            "  \"attackRange\": 5.5,\n" +
            "  \"states\": [\n" +
            "    {\n" +
            "      \"id\": \"Wander\",\n" +
            "      \"interval\": 0.2,\n" +
            "      \"actions\": [\n" +
            "        { \"type\": \"wander\", \"moveScale\": 0.55, \"phaseScale\": 0.035, \"phaseOffset\": 31, \"fireInterval\": 1 }\n" +
            "      ]\n" +
            "    },\n" +
            "    {\n" +
            "      \"id\": \"Chase\",\n" +
            "      \"interval\": 0.05,\n" +
            "      \"actions\": [\n" +
            "        { \"type\": \"chaseTarget\", \"moveScale\": 0.8, \"phaseScale\": 0.05, \"phaseOffset\": 1, \"fireInterval\": 1 }\n" +
            "      ]\n" +
            "    },\n" +
            "    {\n" +
            "      \"id\": \"Attack\",\n" +
            "      \"interval\": 0.05,\n" +
            "      \"actions\": [\n" +
            "        { \"type\": \"attackTarget\", \"strafeScale\": 0.35, \"phaseScale\": 0.08, \"phaseOffset\": 11, \"fireInterval\": 10 }\n" +
            "      ]\n" +
            "    }\n" +
            "  ],\n" +
            "  \"transitions\": [\n" +
            "    { \"from\": \"Wander\", \"to\": \"Chase\", \"condition\": \"outOfAttackRange\" },\n" +
            "    { \"from\": \"Wander\", \"to\": \"Attack\", \"condition\": \"inAttackRange\" },\n" +
            "    { \"from\": \"Chase\", \"to\": \"Wander\", \"condition\": \"noTarget\" },\n" +
            "    { \"from\": \"Chase\", \"to\": \"Attack\", \"condition\": \"inAttackRange\" },\n" +
            "    { \"from\": \"Attack\", \"to\": \"Wander\", \"condition\": \"noTarget\" },\n" +
            "    { \"from\": \"Attack\", \"to\": \"Chase\", \"condition\": \"outOfAttackRange\" }\n" +
            "  ]\n" +
            "}";
    }

    internal static class ShooterBotAiProfileIds
    {
        public const string SimpleBattle = "simple-battle";
    }

    internal static class ShooterBotAiStateIds
    {
        public const string Wander = "Wander";
        public const string Chase = "Chase";
        public const string Attack = "Attack";
    }

    internal static class ShooterBotAiActionTypes
    {
        public const string Idle = "idle";
        public const string Wander = "wander";
        public const string ChaseTarget = "chaseTarget";
        public const string AttackTarget = "attackTarget";
    }

    internal static class ShooterBotAiConditions
    {
        public const string HasTarget = "hasTarget";
        public const string NoTarget = "noTarget";
        public const string InAttackRange = "inAttackRange";
        public const string OutOfAttackRange = "outOfAttackRange";
    }
}
