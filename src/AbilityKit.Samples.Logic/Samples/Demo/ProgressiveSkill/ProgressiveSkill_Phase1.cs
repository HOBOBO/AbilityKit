using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Demo.ProgressiveSkill
{
    /// <summary>
    /// ProgressiveSkill Phase1 - IContinuous 接口 + IContinuousManager (框架提供)
    ///
    /// 需求: 管理多个持续行为（灼烧、减速、中毒），支持暂停、恢复、中断。
    ///
    /// 框架能力: com.abilitykit.core 的 IContinuous 接口和 IContinuousManager。
    /// 从 Phase1_Continuous.json 加载配置数据。
    /// 展示如何用框架统一管理所有持续行为的生命周期。
    /// </summary>
    [Sample]
    public sealed class ProgressiveSkill_Phase1 : SampleBase
    {
        public override string Title => "ProgressiveSkill Phase1";
        public override string Description => "IContinuous - 统一生命周期管理 (框架)";
        public override SampleCategory Category => SampleCategory.Demo;

        private Phase1EffectManager _manager;

        protected override void OnRun()
        {
            Log("================================================================================");
            Log("===           渐进式技能系统 - Phase1: IContinuous (框架)           ===");
            Log("================================================================================");
            Output.Divider();

            // 从 JSON 配置加载
            var config = Phase1Config.Load();
            Log("【1】从 Phase1_Continuous.json 加载配置");
            Log($"  灼烧配置: tickInterval={config.Burning.TickInterval}s, damagePerTick={config.Burning.DamagePerTick}");
            Log($"  减速配置: tickInterval={config.Slow.TickInterval}s, slowPercent={config.Slow.SlowPercent:P0}");
            Log("");

            _manager = new Phase1EffectManager();

            // 需求
            Log("【2】需求: 管理多个持续行为（灼烧、减速），支持暂停、恢复、中断");
            Log("");

            // 创建目标
            var enemy = new Phase0Target(1, "哥布林王", 500f);
            Log($"  目标: {enemy}");
            Log("");

            // 添加灼烧效果
            Log("【3】添加灼烧效果");
            var burnEffect = new Phase1BurningEffect(
                configId: config.Burning.Id,
                ownerId: enemy.Id,
                damagePerTick: config.Burning.DamagePerTick,
                duration: config.Burning.DurationSeconds,
                onTick: damage => Log($"    [灼烧] 造成 {damage:F0} 点火焰伤害! (剩余: {enemy.Health:F0} HP)"),
                onEnd: reason => Log($"    [灼烧] 结束: {reason}"));

            _manager.TryActivate(burnEffect);
            Log($"  激活灼烧效果 (持续: {config.Burning.DurationSeconds}s)");
            Log("");

            // Tick 一段时间
            Log("【4】模拟 2 秒 (每 0.5 秒 Tick 一次)");
            for (int i = 0; i < 4; i++)
            {
                AdvanceTime(0.5f);
                _manager.Tick(0.5f);
                Log($"    [Tick {i + 1}] 活跃效果数: {_manager.ActiveCount}");
            }
            Log("");

            // 暂停
            Log("【5】暂停灼烧效果");
            _manager.Pause(burnEffect);
            Log("  调用 Pause() 后，Tick 不会推进时间");
            for (int i = 0; i < 2; i++)
            {
                AdvanceTime(0.5f);
                _manager.Tick(0.5f);
                Log($"    [Tick {i + 1}] Elapsed={burnEffect.ElapsedSeconds:F1}s (时间未推进)");
            }
            Log("");

            // 恢复
            Log("【6】恢复灼烧效果");
            _manager.Resume(burnEffect);
            Log("  调用 Resume() 后，继续计时");
            Log("");

            // 添加减速效果
            Log("【7】添加减速效果");
            var slowEffect = new Phase1SlowEffect(
                configId: config.Slow.Id,
                ownerId: enemy.Id,
                slowPercent: config.Slow.SlowPercent,
                duration: config.Slow.DurationSeconds,
                onTick: () => Log($"    [减速] 目标速度降低 {config.Slow.SlowPercent:P0}"),
                onEnd: reason => Log($"    [减速] 结束: {reason}"));

            _manager.TryActivate(slowEffect);
            Log($"  激活减速效果 (持续: {config.Slow.DurationSeconds}s)");
            Log("");

            // 演示中断
            Log("【8】假设被净化技能中断减速");
            _manager.Abort(slowEffect, "cleansed");
            Log("  调用 Abort() 后，效果立即终止");
            Log($"  当前活跃效果: {_manager.ActiveCount}");
            Log("");

            // 继续灼烧直到结束
            Log("【9】继续灼烧直到结束");
            for (int i = 0; i < 10; i++)
            {
                AdvanceTime(0.5f);
                _manager.Tick(0.5f);
            }
            Log($"  最终目标 HP: {enemy.Health:F0}");
            Log("");

            // 对比 Phase0
            Log("【对比 Phase0】");
            Output.Bullet("Phase0: 手动管理 elapsed/lastTick/active 状态");
            Output.Bullet("Phase1: IContinuous 接口 + IContinuousManager 统一管理 (框架提供)");
            Output.Bullet("生命周期由框架管理，无需手动遍历");
            Output.Bullet("暂停/恢复/中断由框架处理，无需重复代码");
            Log("");

            // 暴露下一个痛点
            Log("【下一个痛点】");
            Output.Bullet("如果需要在特定时机触发效果（如造成伤害时触发反击），Continuous 无法处理");
            Output.Bullet("Continuous 只管理\"持续\"，不处理\"事件\"");
            Log("  -> Phase2: Flow 异步编排 (框架)");
            Log("");

            Output.Divider();
        }
    }

    // ============================================================================
    // 配置模型
    // ============================================================================

    /// <summary>
    /// Phase1 配置 (从 JSON 加载)
    /// </summary>
    public sealed class Phase1Config
    {
        public Phase1EffectConfig Burning { get; set; } = new();
        public Phase1EffectConfig Slow { get; set; } = new();

        public static Phase1Config Load()
        {
            // 嵌入的 JSON 配置
            const string json = @"{
                ""burning"": {
                    ""id"": ""burning"",
                    ""tickInterval"": 1.0,
                    ""damagePerTick"": 8.0,
                    ""durationSeconds"": 5.0
                },
                ""slow"": {
                    ""id"": ""slow"",
                    ""tickInterval"": 2.0,
                    ""slowPercent"": 0.5,
                    ""durationSeconds"": 3.0
                }
            }";
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Phase1Config>(json);
        }
    }

    /// <summary>
    /// 效果配置
    /// </summary>
    public sealed class Phase1EffectConfig
    {
        public string Id { get; set; }
        public float TickInterval { get; set; }
        public float DamagePerTick { get; set; }
        public float SlowPercent { get; set; }
        public float DurationSeconds { get; set; }
    }

    // ============================================================================
    // 效果管理器 - 实现 IContinuousManager (框架接口)
    // ============================================================================

    /// <summary>
    /// 效果管理器 - 实现 IContinuousManager
    /// </summary>
    public sealed class Phase1EffectManager : IContinuousManager
    {
        private readonly List<IContinuous> _continuous = new();

        public int ActiveCount => _continuous.Count(c => c.State == ContinuousState.Active);
        public int TotalCount => _continuous.Count;

        public bool Register(IContinuous continuous)
        {
            _continuous.Add(continuous);
            return true;
        }

        public void Unregister(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.CleanedUp)
        {
            _continuous.Remove(continuous);
        }

        public bool TryActivate(IContinuous continuous)
        {
            if (continuous.State != ContinuousState.Inactive)
                return false;
            continuous.Activate();
            _continuous.Add(continuous);
            return true;
        }

        public bool TryPause(IContinuous continuous)
        {
            if (continuous == null || !continuous.IsActive || continuous.IsTerminated)
                return false;

            continuous.Pause();
            return continuous.IsPaused;
        }

        public bool TryResume(IContinuous continuous)
        {
            if (continuous == null || !continuous.IsPaused || continuous.IsTerminated)
                return false;

            continuous.Resume();
            return continuous.IsActive;
        }

        public bool TryEnd(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.Completed)
        {
            if (continuous == null || continuous.IsTerminated)
                return false;

            continuous.End(reason);
            if (!continuous.IsTerminated)
                return false;

            _continuous.Remove(continuous);
            return true;
        }

        public bool TryInterrupt(IContinuous continuous, string reason)
        {
            if (continuous == null || continuous.IsTerminated || !continuous.Config.CanBeInterrupted)
                return false;

            continuous.Abort(reason);
            if (!continuous.IsTerminated)
                return false;

            _continuous.Remove(continuous);
            return true;
        }

        public void Pause(IContinuous continuous)
        {
            TryPause(continuous);
        }

        public void Resume(IContinuous continuous)
        {
            TryResume(continuous);
        }

        public void Abort(IContinuous continuous, string reason)
        {
            TryInterrupt(continuous, reason);
        }

        public IReadOnlyList<IContinuous> GetOwnerContinuous(long ownerId)
        {
            var result = new List<IContinuous>();
            foreach (var c in _continuous)
            {
                if (c.Config.OwnerId == ownerId)
                    result.Add(c);
            }
            return result;
        }

        public IReadOnlyList<IContinuous> GetOwnerActiveContinuous(long ownerId)
        {
            var result = new List<IContinuous>();
            foreach (var c in _continuous)
            {
                if (c.Config.OwnerId == ownerId && c.State == ContinuousState.Active)
                    result.Add(c);
            }
            return result;
        }

        public void InterruptAll(long ownerId, string reason)
        {
            var toRemove = new List<IContinuous>();
            foreach (var c in _continuous)
            {
                if (c.Config.OwnerId == ownerId)
                {
                    c.Abort(reason);
                    toRemove.Add(c);
                }
            }
            foreach (var c in toRemove)
                _continuous.Remove(c);
        }

        public void PauseAll(long ownerId)
        {
            foreach (var c in GetOwnerActiveContinuous(ownerId))
                c.Pause();
        }

        public void ResumeAll(long ownerId)
        {
            foreach (var c in _continuous)
            {
                if (c.Config.OwnerId == ownerId && c.State == ContinuousState.Paused)
                    c.Resume();
            }
        }

        public void Tick(float deltaTime)
        {
            var toRemove = new List<IContinuous>();

            foreach (var c in _continuous)
            {
                if (c is Phase1BurningEffect burn)
                    burn.InternalTick(deltaTime);
                else if (c is Phase1SlowEffect slow)
                    slow.InternalTick(deltaTime);

                if (c.IsTerminated)
                    toRemove.Add(c);
            }

            foreach (var c in toRemove)
                _continuous.Remove(c);
        }
    }

    // ============================================================================
    // 灼烧效果 - 实现 IContinuous (框架接口)
    // ============================================================================

    /// <summary>
    /// 灼烧效果 - 实现 IContinuous (框架接口)
    /// </summary>
    public sealed class Phase1BurningEffect : IContinuous
    {
        private readonly Phase1ContinuousConfig _config;
        private readonly Action<float> _onTick;
        private readonly Action<string> _onEnd;
        private float _elapsed;
        private float _sinceLastTick;

        public IContinuousConfig Config => _config;
        public ContinuousState State { get; private set; } = ContinuousState.Inactive;
        public bool IsActive => State == ContinuousState.Active;
        public bool IsTerminated => State == ContinuousState.Expired || State == ContinuousState.Aborted;
        public bool IsPaused => State == ContinuousState.Paused;
        public float ElapsedSeconds => _elapsed;

        public event Action<IContinuous, ContinuousEndReason> OnEnded;

        public Phase1BurningEffect(string configId, long ownerId, float damagePerTick, float duration,
            Action<float> onTick, Action<string> onEnd)
        {
            _config = new Phase1ContinuousConfig(configId, ownerId, duration);
            DamagePerTick = damagePerTick;
            _onTick = onTick;
            _onEnd = onEnd;
        }

        public float DamagePerTick { get; }

        public void Activate()
        {
            State = ContinuousState.Active;
            _elapsed = 0f;
            _sinceLastTick = 0f;
        }

        public void Pause() => State = ContinuousState.Paused;
        public void Resume() => State = ContinuousState.Active;

        public void Abort(string reason)
        {
            End(ContinuousEndReason.Interrupted);
        }

        public void End(ContinuousEndReason reason)
        {
            if (IsTerminated)
                return;

            State = reason == ContinuousEndReason.Completed
                ? ContinuousState.Expired
                : ContinuousState.Aborted;
            _onEnd?.Invoke(reason.ToString());
            OnEnded?.Invoke(this, reason);
        }

        public void InternalTick(float deltaTime)
        {
            if (State != ContinuousState.Active) return;

            _elapsed += deltaTime;
            _sinceLastTick += deltaTime;

            // 每 1 秒造成一次伤害
            if (_sinceLastTick >= 1f)
            {
                _onTick?.Invoke(DamagePerTick);
                _sinceLastTick = 0f;
            }

            if (_elapsed >= _config.DurationSeconds)
            {
                End(ContinuousEndReason.Completed);
            }
        }
    }

    // ============================================================================
    // 减速效果 - 实现 IContinuous (框架接口)
    // ============================================================================

    /// <summary>
    /// 减速效果 - 实现 IContinuous (框架接口)
    /// </summary>
    public sealed class Phase1SlowEffect : IContinuous
    {
        private readonly Phase1ContinuousConfig _config;
        private readonly Action _onTick;
        private readonly Action<string> _onEnd;
        private float _elapsed;
        private float _sinceLastTick;

        public IContinuousConfig Config => _config;
        public ContinuousState State { get; private set; } = ContinuousState.Inactive;
        public bool IsActive => State == ContinuousState.Active;
        public bool IsTerminated => State == ContinuousState.Expired || State == ContinuousState.Aborted;
        public bool IsPaused => State == ContinuousState.Paused;
        public float ElapsedSeconds => _elapsed;

        public event Action<IContinuous, ContinuousEndReason> OnEnded;

        public Phase1SlowEffect(string configId, long ownerId, float slowPercent, float duration,
            Action onTick, Action<string> onEnd)
        {
            _config = new Phase1ContinuousConfig(configId, ownerId, duration);
            SlowPercent = slowPercent;
            _onTick = onTick;
            _onEnd = onEnd;
        }

        public float SlowPercent { get; }

        public void Activate()
        {
            State = ContinuousState.Active;
            _elapsed = 0f;
            _sinceLastTick = 0f;
        }

        public void Pause() => State = ContinuousState.Paused;
        public void Resume() => State = ContinuousState.Active;

        public void Abort(string reason)
        {
            End(ContinuousEndReason.Interrupted);
        }

        public void End(ContinuousEndReason reason)
        {
            if (IsTerminated)
                return;

            State = reason == ContinuousEndReason.Completed
                ? ContinuousState.Expired
                : ContinuousState.Aborted;
            _onEnd?.Invoke(reason.ToString());
            OnEnded?.Invoke(this, reason);
        }

        public void InternalTick(float deltaTime)
        {
            if (State != ContinuousState.Active) return;

            _elapsed += deltaTime;
            _sinceLastTick += deltaTime;

            // 每 2 秒触发一次
            if (_sinceLastTick >= 2f)
            {
                _onTick?.Invoke();
                _sinceLastTick = 0f;
            }

            if (_elapsed >= _config.DurationSeconds)
            {
                End(ContinuousEndReason.Completed);
            }
        }
    }

    // ============================================================================
    // 配置实现 - 实现 IContinuousConfig (框架接口)
    // ============================================================================

    /// <summary>
    /// 持续体配置 - 实现 IContinuousConfig (框架接口)
    /// </summary>
    public sealed class Phase1ContinuousConfig : IContinuousConfig
    {
        public string Id { get; }
        public long OwnerId { get; }
        public float DurationSeconds { get; }
        public bool CanBeInterrupted => true;

        public Phase1ContinuousConfig(string id, long ownerId, float durationSeconds)
        {
            Id = id;
            OwnerId = ownerId;
            DurationSeconds = durationSeconds;
        }
    }
}
