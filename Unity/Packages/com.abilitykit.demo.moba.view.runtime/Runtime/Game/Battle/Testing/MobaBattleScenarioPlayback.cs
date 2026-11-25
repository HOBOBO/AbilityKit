#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba;
using AbilityKit.Scenario;
using UnityEngine;

namespace AbilityKit.Game.Battle.Testing
{
    /// <summary>
    /// 在 Unity 本地战斗中播放 Battle Scenario 的已解析技能输入；不替代手势或 Headless 断言验收。
    /// </summary>
    public static class MobaBattleScenarioPlayback
    {
#if UNITY_EDITOR
        internal const string PendingTraceDiagnosticsKey = "AbilityKit.MobaBattleScenarioPlayback.PendingTraceDiagnostics";
#endif
        internal const string PreviewProfileId = "moba.skill_preview";
        private static (TestScenario Scenario, bool PreviewOnly) _pending;

        /// <summary>
        /// 为下一次编辑器本地自动开战暂存场景，或者以 null 清除未消费的请求。
        /// </summary>
        public static void SetPendingScenario(TestScenario scenario, bool previewOnly = false) =>
            _pending = scenario == null ? default : (scenario, previewOnly);

        /// <summary>
        /// 预检当前播放器支持的场景原语，避免将未消费的场景数据误当作已复现。
        /// </summary>
        public static void Validate(TestScenario scenario)
        {
            TestScenarioValidator.ThrowIfInvalid(scenario);
            if (scenario.Seed == 0)
                throw new NotSupportedException("?????????????????");
            if (scenario.Setup.Count != 0 || scenario.Obstacles.Count != 0 || scenario.Commands.Count != 0 ||
                scenario.NetworkWatches.Count != 0 || scenario.WorldParameters.Count != 0 ||
                !string.IsNullOrEmpty(scenario.EnvironmentProfileId) || !string.IsNullOrEmpty(scenario.NavigationProfileId) ||
                scenario.WorldProfileId != "default")
                throw new NotSupportedException("Unity 技能预览仅支持默认环境下的角色布置与本地技能输入。");
            if (scenario.Actors.Count(actor => !string.IsNullOrEmpty(actor.PlayerId)) != 1 ||
                scenario.Actors.Any(actor => actor.TeamId != 1 && actor.TeamId != 2 || actor.Position == null ||
                    actor.HeroId <= 0 || actor.Facing != null))
                throw new NotSupportedException("预览需一个有 PlayerId 的施法者，所有角色需指定队伍、正式英雄和坐标；暂不支持初始朝向覆盖。");
            var caster = scenario.Actors.Single(actor => !string.IsNullOrEmpty(actor.PlayerId));
            var aliases = new HashSet<string>(scenario.Actors.Select(actor => actor.Alias), StringComparer.Ordinal);
            if (scenario.Timeline.Count == 0 || scenario.Timeline.Any(step =>
                step.Action == "move_to" ? step.Position == null || !aliases.Contains(step.ActorAlias) :
                step.Action != "wait" && (step.ActorAlias != caster.Alias || step.Slot <= 0 ||
                (step.Action != "cast_skill" && step.Action != "press" && step.Action != "release" &&
                 step.Action != "hold" && step.Action != "cancel"))))
                throw new NotSupportedException("预览时间线仅支持本地角色技能输入、已布置角色的 move_to 和等待。");
        }

        /// <summary>
        /// 等待已按场景配置启动的原生本地会话，按逻辑帧提交技能输入。
        /// </summary>
        public static IEnumerator Play(GameEntry entry, TestScenario scenario, Action<Exception> completed,
            bool previewOnly = false)
        {
            using var playback = PlayCore(entry, scenario, previewOnly);
            while (true)
            {
                bool running;
                try { running = playback.MoveNext(); }
                catch (Exception error) { completed(error); yield break; }
                if (!running) break;
                yield return playback.Current;
            }
            completed(null);
        }

        private static Dictionary<string, PlayerId> BuildPlayerIds(TestScenario scenario) =>
            scenario.Actors.ToDictionary(actor => actor.Alias,
                actor => new PlayerId(string.IsNullOrEmpty(actor.PlayerId) ? $"preview_{actor.Alias}" : actor.PlayerId),
                StringComparer.Ordinal);

        internal static BattleStartConfig ResolveLocalStartConfig(BattleStartConfig source)
        {
            var (scenario, previewOnly) = _pending;
            if (!Application.isPlaying || source == null) return source;
            var enableTraceDiagnostics = false;
#if UNITY_EDITOR
            enableTraceDiagnostics = UnityEditor.SessionState.GetBool(PendingTraceDiagnosticsKey, false);
            if (enableTraceDiagnostics) UnityEditor.SessionState.EraseBool(PendingTraceDiagnosticsKey);
#endif
            if (scenario == null)
            {
                if (!enableTraceDiagnostics) return source;
                var traceConfig = UnityEngine.Object.Instantiate(source);
                traceConfig.EnableTraceDiagnostics = true;
                return traceConfig;
            }
            _pending = default;
            var actorPlayers = BuildPlayerIds(scenario);
            var caster = scenario.Actors.Single(actor => !string.IsNullOrEmpty(actor.PlayerId));
            var player = actorPlayers[caster.Alias];
            var config = UnityEngine.Object.Instantiate(source);
            var sourcePreset = config.Preset;
            var enter = UnityEngine.Object.Instantiate(sourcePreset != null ? sourcePreset.EnterGameSO : config.EnterGameSO);
            enter.TickRate = scenario.TickRate;
            enter.RandomSeed = scenario.Seed;
            var players = ScriptableObject.CreateInstance<BattlePlayersConfigSO>();
            players.LocalPlayerId = player.Value;
            players.Team1Players.Clear();
            players.Team2Players.Clear();
            var database = MobaConfigLoader.LoadDefault();
            foreach (var actor in scenario.Actors)
            {
                if (!MobaHeroLoadoutResolver.TryResolveHeroConfig(database, actor.HeroId,
                        out var character, out var basicAttack, out var activeSkills, out var error))
                    throw new InvalidOperationException($"无法装配预览角色 {actor.Alias}：{error}");
                var position = actor.Position.Value;
                var playerConfig = new BattlePlayersConfigSO.PlayerConfig
                {
                    PlayerId = actorPlayers[actor.Alias].Value,
                    TeamId = (AbilityKit.Demo.Moba.Team)actor.TeamId,
                    HeroId = actor.HeroId,
                    AttributeTemplateId = actor.AttributeTemplateId > 0 ? actor.AttributeTemplateId : character.AttributeTemplateId,
                    SkillIds = actor.SkillIds.Length > 0 ? actor.SkillIds : activeSkills,
                    BasicAttackSkillId = basicAttack,
                    EnableBrainOnSpawn = false,
                    SpawnPosition = new Vector3(position.X, position.Y, position.Z),
                };
                (actor.TeamId == 1 ? players.Team1Players : players.Team2Players).Add(playerConfig);
            }

            var preset = sourcePreset != null ? UnityEngine.Object.Instantiate(sourcePreset) : null;
            if (preset != null)
            {
                if (preset.HostMode != BattleHostMode.Local)
                    throw new InvalidOperationException("技能预览仅可使用本地预设。");
                preset.PlayersSO = players;
                preset.EnterGameSO = enter;
                if (previewOnly) preset.FlowProfileId = PreviewProfileId;
                config.Preset = preset;
            }
            else
            {
                if (config.HostMode != BattleHostMode.Local)
                    throw new InvalidOperationException("技能预览仅可使用本地预设。");
                config.PlayersSO = players;
                config.EnterGameSO = enter;
                if (previewOnly) config.FlowProfileId = PreviewProfileId;
            }
            config.UseRoomGameStartSpec = false;
            config.EnableSyncModeSelection = false;
            if (enableTraceDiagnostics || (!previewOnly && scenario.Expectations is MobaBattleScenarioAssertions assertions &&
                assertions.MustContain.Any(expectation => expectation.Kind == nameof(MobaExecutionKind.SkillCast))))
                config.EnableTraceDiagnostics = true;
            return config;
        }

        private static IEnumerator<object> PlayCore(GameEntry entry, TestScenario scenario, bool previewOnly)
        {
            var flow = entry.Get<GameFlowDomain>();
            var actorPlayers = BuildPlayerIds(scenario);
            var caster = scenario.Actors.Single(actor => !string.IsNullOrEmpty(actor.PlayerId));
            var player = actorPlayers[caster.Alias];
            BattleContext context = null;
            var deadline = Time.realtimeSinceStartup + 60f;
            while (flow.CurrentBattlePhase != MobaBattleState.InMatch ||
                !entry.TryGet(out context) || context.LocalActorId <= 0 || !context.CanSubmitGameplayInput)
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException("MOBA 预览启动超时：没有进入可输入的 InMatch 状态。");
                yield return null;
            }
            if (context.Plan.HostMode != BattleHostMode.Local || context.ResolveLocalControlPlayerId() != player.Value)
                throw new InvalidOperationException("预览没有使用请求的本地场景配置，已停止发送输入。");
            if (previewOnly && flow.CurrentBattleProfileId != PreviewProfileId)
                throw new InvalidOperationException("纯技能预览未启用独立的战斗流程，已停止发送输入。");
            if (!context.Session.TryGetWorld(out var world))
                throw new InvalidOperationException("MOBA 预览未获得本地战斗世界。");
            var actorMap = world.Services.Resolve<MobaPlayerActorMapService>();
            var registry = world.Services.Resolve<MobaActorRegistry>();
            var targets = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var alias in actorPlayers.Keys)
            {
                if (!actorMap.TryGetActorId(actorPlayers[alias], out var actorId) || actorId <= 0)
                    throw new InvalidOperationException($"MOBA 预览角色未绑定：{alias}");
                targets.Add(alias, actorId);
            }

            var plan = context.Plan;
            var input = new BattleInputSubmitter(context.InputRuntime, player, BattleInputSessionIdentity.ResolveWorldId(in plan));
            var origin = context.LastFrame;
            deadline = Time.realtimeSinceStartup + Math.Max(60f, scenario.TimeoutMs / 1000f + 5f);
            var nextFrame = origin;
            foreach (var step in scenario.Timeline.OrderBy(step => step.AtMs))
            {
                nextFrame = Math.Max(nextFrame, origin + ToFrames(step.AtMs, scenario.TickRate));
                if (step.Action == "wait")
                {
                    nextFrame += ToFrames(step.DurationMs, scenario.TickRate);
                    continue;
                }
                while (context.LastFrame < nextFrame)
                {
                    EnsurePlaying(flow, deadline);
                    yield return null;
                }
                if (step.Action == "move_to")
                {
                    if (!registry.TryGet(targets[step.ActorAlias], out var actor) || actor == null || !actor.hasTransform)
                        throw new InvalidOperationException($"MOBA 预览角色无法移动：{step.ActorAlias}");
                    var pose = actor.transform.Value;
                    actor.ReplaceTransform(new Transform3(ToWorldVector(step.Position), pose.Rotation, pose.Scale));
                    continue;
                }
                var phase = step.Action switch
                {
                    "release" => SkillInputPhase.Release,
                    "hold" => SkillInputPhase.Hold,
                    "cancel" => SkillInputPhase.Cancel,
                    _ => SkillInputPhase.Press,
                };
                var frame = SessionSimRuntimeTuning.ResolveInputSubmitFrame(context.LastFrame, in plan);
                var target = string.IsNullOrEmpty(step.TargetAlias) ? 0 : targets[step.TargetAlias];
                var aimPos = ToWorldVector(step.Position);
                var aimDir = ToWorldVector(step.Direction);
                if (phase == SkillInputPhase.Press && target == 0 && aimPos.Equals(Vec3.Zero))
                    aimDir = default;
                var skill = new SkillInputEvent(slot: step.Slot, phase: phase, targetActorId: target,
                    aimPos: in aimPos, aimDir: in aimDir, targetFrame: frame);
                var command = new PlayerInputCommand(new FrameIndex(frame), player,
                    MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in skill));
                if (!input.Submit(in command))
                    throw new InvalidOperationException($"预览输入未提交：{scenario.CaseId} t={step.AtMs} slot={step.Slot}。");
                nextFrame = context.LastFrame + 1;
            }
            if (scenario.Execution?.EndCondition.Kind == TestEndConditionKinds.Duration)
                nextFrame = Math.Max(nextFrame, origin + ToFrames(scenario.Execution.EndCondition.DurationMs, scenario.TickRate));
            var endFrame = nextFrame + ToFrames(scenario.Execution?.SettleDurationMs ?? 500, scenario.TickRate);
            while (context.LastFrame < endFrame)
            {
                EnsurePlaying(flow, deadline);
                yield return null;
            }
            if (scenario.Expectations is MobaBattleScenarioAssertions assertions)
            {
                var casts = assertions.MustContain.Where(expectation => expectation.Kind == nameof(MobaExecutionKind.SkillCast)).ToArray();
                if (casts.Length != 0)
                {
                    if (!world.Services.TryResolve<MobaTraceRegistry>(out var trace))
                        throw new InvalidOperationException("SkillCast assertions require Trace diagnostics enabled before world creation.");
                    foreach (var expectation in casts)
                    {
                        var count = 0;
                        foreach (var node in trace.GetNodesByKind((int)MobaExecutionKind.SkillCast))
                        {
                            if (node.Metadata?.ConfigId == expectation.ConfigId) count++;
                        }
                        if (count < expectation.MinCount || (expectation.MaxCount > 0 && count > expectation.MaxCount))
                            throw new InvalidOperationException($"预览未复现预期施法：{scenario.CaseId} skill={expectation.ConfigId} casts={count}。");
                    }
                }
            }
        }

        private static int ToFrames(int milliseconds, int tickRate) => (int)Math.Ceiling(milliseconds * tickRate / 1000d);

        private static Vec3 ToWorldVector(TestVector3? value) =>
            value is TestVector3 vector ? new Vec3(vector.X, vector.Y, vector.Z) : Vec3.Zero;

        private static void EnsurePlaying(GameFlowDomain flow, float deadline)
        {
            if (flow.CurrentBattlePhase != MobaBattleState.InMatch)
                throw new InvalidOperationException("MOBA 预览在播放完成前离开了战斗。");
            if (Time.realtimeSinceStartup > deadline)
                throw new TimeoutException("MOBA 预览战斗时钟没有在允许的时间内走完。");
        }
    }
}
#endif
