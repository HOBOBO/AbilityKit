using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Continuous;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Continuous
{
    /// <summary>
    /// 演示 IContinuous 与 IContinuousManager 如何管理 DOT 的激活、Tick、暂停、恢复和中断。
    /// </summary>
    [Sample(502, "continuous", "dot", "lifecycle", "package-api", "web", "deterministic")]
    public sealed class ContinuousBasics : SampleBase
    {
        public override string Title => "DOT Lifecycle";
        public override string Description => "使用 IContinuous 和 IContinuousManager 表达持续伤害生命周期";
        public override SampleCategory Category => SampleCategory.Continuous;

        protected override void OnRun()
        {
            var manager = new DotContinuousManager();
            var target = new TargetState(ownerId: 1001, hp: 120);
            var burning = new DamageOverTime(
                new DotConfig(id: "burning", ownerId: target.OwnerId, durationSeconds: 4f, tickIntervalSeconds: 1f, canBeInterrupted: true),
                target,
                damagePerTick: 12f);

            burning.OnEnded += (_, reason) => Log($"[Event] burning ended: {reason}");

            Section("激活 DOT");
            KeyValue("DOT.TargetInitialHp", target.Hp.ToString("F1"));
            KeyValue("DOT.Burning.State", burning.State.ToString());
            KeyValue("Register", manager.Register(burning).ToString());
            KeyValue("TryActivate", manager.TryActivate(burning).ToString());
            KeyValue("State", burning.State.ToString());
            KeyValue("DOT.ActiveCount", manager.ActiveCount.ToString());

            Divider();
            Section("宿主固定步进 Tick");
            Step(manager, target, 1f);
            Step(manager, target, 1f);
            KeyValue("DOT.Burning.EndReason", burning.IsTerminated ? burning.State.ToString() : "Running");

            Divider();
            Section("暂停与恢复");
            KeyValue("TryPause", manager.TryPause(burning).ToString());
            Step(manager, target, 1f);
            KeyValue("TryResume", manager.TryResume(burning).ToString());
            Step(manager, target, 1f);
            Step(manager, target, 1f);
            KeyValue("DOT.Step", $"pause={burning.IsPaused}, active={burning.IsActive}");

            Divider();
            Section("中断另一个 DOT");
            var poison = new DamageOverTime(
                new DotConfig(id: "poison", ownerId: target.OwnerId, durationSeconds: 8f, tickIntervalSeconds: 1f, canBeInterrupted: true),
                target,
                damagePerTick: 5f);
            poison.OnEnded += (_, reason) => Log($"[Event] poison ended: {reason}");
            manager.Register(poison);
            manager.TryActivate(poison);
            Step(manager, target, 1f);
            KeyValue("DOT.Poison.Interrupted", manager.TryInterrupt(poison, "cleanse").ToString());
            KeyValue("ActiveCount", manager.ActiveCount.ToString());
            KeyValue("DOT.ActiveCount", manager.ActiveCount.ToString());
            KeyValue("TotalCount", manager.TotalCount.ToString());

            Divider();
            Section("这个示例实际接入的包能力");
            Bullet("IContinuous：把 DOT 抽象为可激活、暂停、恢复、结束和中断的持续体。");
            Bullet("IContinuousConfig：提供持续体 id、所属实体和可中断策略。");
            Bullet("IContinuousManager：由业务层实现注册、激活、暂停、恢复、清理和 owner 查询。");
            Bullet("ContinuousState / ContinuousEndReason：让宿主和 UI 能观察生命周期状态与结束原因。");
        }

        private void Step(DotContinuousManager manager, TargetState target, float deltaTime)
        {
            AdvanceTime(deltaTime);
            manager.Tick(deltaTime);
            KeyValue($"t={Time:F1}s", $"hp={target.Hp:F1}, active={manager.ActiveCount}, total={manager.TotalCount}");
        }

        private sealed class TargetState
        {
            public TargetState(long ownerId, float hp)
            {
                OwnerId = ownerId;
                Hp = hp;
            }

            public long OwnerId { get; }
            public float Hp { get; set; }
        }

        private sealed class DotConfig : IContinuousConfig, IDurationConfig
        {
            public DotConfig(string id, long ownerId, float durationSeconds, float tickIntervalSeconds, bool canBeInterrupted)
            {
                Id = id;
                OwnerId = ownerId;
                DurationSeconds = durationSeconds;
                TickIntervalSeconds = tickIntervalSeconds;
                CanBeInterrupted = canBeInterrupted;
            }

            public string Id { get; }
            public long OwnerId { get; }
            public float? DurationSeconds { get; }
            public float TickIntervalSeconds { get; }
            public bool CanBeInterrupted { get; }
        }

        private sealed class DamageOverTime : IContinuous
        {
            private readonly DotConfig _config;
            private readonly TargetState _target;
            private readonly float _damagePerTick;
            private float _tickAccumulator;

            public DamageOverTime(DotConfig config, TargetState target, float damagePerTick)
            {
                _config = config;
                _target = target;
                _damagePerTick = damagePerTick;
            }

            public event Action<IContinuous, ContinuousEndReason>? OnEnded;

            public IContinuousConfig Config => _config;
            public ContinuousState State { get; private set; } = ContinuousState.Inactive;
            public bool IsActive => State == ContinuousState.Active;
            public bool IsTerminated => State == ContinuousState.Expired || State == ContinuousState.Aborted;
            public bool IsPaused => State == ContinuousState.Paused;
            public float ElapsedSeconds { get; private set; }

            public void Activate()
            {
                State = ContinuousState.Active;
                ElapsedSeconds = 0f;
                _tickAccumulator = 0f;
            }

            public void Pause()
            {
                if (IsActive)
                {
                    State = ContinuousState.Paused;
                }
            }

            public void Resume()
            {
                if (IsPaused)
                {
                    State = ContinuousState.Active;
                }
            }

            public void End(ContinuousEndReason reason)
            {
                if (IsTerminated)
                {
                    return;
                }

                State = reason == ContinuousEndReason.Completed ? ContinuousState.Expired : ContinuousState.Aborted;
                OnEnded?.Invoke(this, reason);
            }

            public void Abort(string reason)
            {
                End(ContinuousEndReason.Interrupted);
            }

            public void Tick(float deltaTime)
            {
                if (!IsActive)
                {
                    return;
                }

                ElapsedSeconds += deltaTime;
                _tickAccumulator += deltaTime;

                while (_tickAccumulator >= _config.TickIntervalSeconds && IsActive)
                {
                    _tickAccumulator -= _config.TickIntervalSeconds;
                    _target.Hp -= _damagePerTick;
                }

                if (_config.DurationSeconds.HasValue && ElapsedSeconds >= _config.DurationSeconds.Value)
                {
                    End(ContinuousEndReason.Completed);
                }
            }
        }

        private sealed class DotContinuousManager : IContinuousManager
        {
            private readonly List<IContinuous> _items = new List<IContinuous>();

            public int ActiveCount => _items.Count(item => item.IsActive);
            public int TotalCount => _items.Count;

            public bool Register(IContinuous continuous)
            {
                if (_items.Contains(continuous))
                {
                    return false;
                }

                _items.Add(continuous);
                return true;
            }

            public void Unregister(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.CleanedUp)
            {
                if (_items.Remove(continuous) && !continuous.IsTerminated)
                {
                    continuous.End(reason);
                }
            }

            public bool TryActivate(IContinuous continuous)
            {
                if (!_items.Contains(continuous))
                {
                    Register(continuous);
                }

                if (continuous.State != ContinuousState.Inactive)
                {
                    return false;
                }

                continuous.Activate();
                return true;
            }

            public bool TryPause(IContinuous continuous)
            {
                if (!continuous.IsActive)
                {
                    return false;
                }

                continuous.Pause();
                return true;
            }

            public bool TryResume(IContinuous continuous)
            {
                if (!continuous.IsPaused)
                {
                    return false;
                }

                continuous.Resume();
                return true;
            }

            public bool TryEnd(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.Completed)
            {
                if (!_items.Contains(continuous) || continuous.IsTerminated)
                {
                    return false;
                }

                continuous.End(reason);
                _items.Remove(continuous);
                return true;
            }

            public bool TryInterrupt(IContinuous continuous, string reason)
            {
                if (!_items.Contains(continuous) || continuous.IsTerminated || !continuous.Config.CanBeInterrupted)
                {
                    return false;
                }

                continuous.Abort(reason);
                _items.Remove(continuous);
                return true;
            }

            public IReadOnlyList<IContinuous> GetOwnerContinuous(long ownerId)
            {
                return _items.Where(item => item.Config.OwnerId == ownerId).ToArray();
            }

            public IReadOnlyList<IContinuous> GetOwnerActiveContinuous(long ownerId)
            {
                return _items.Where(item => item.Config.OwnerId == ownerId && item.IsActive).ToArray();
            }

            public void InterruptAll(long ownerId, string reason)
            {
                foreach (var continuous in GetOwnerContinuous(ownerId).ToArray())
                {
                    TryInterrupt(continuous, reason);
                }
            }

            public void PauseAll(long ownerId)
            {
                foreach (var continuous in GetOwnerActiveContinuous(ownerId))
                {
                    TryPause(continuous);
                }
            }

            public void ResumeAll(long ownerId)
            {
                foreach (var continuous in GetOwnerContinuous(ownerId).Where(item => item.IsPaused))
                {
                    TryResume(continuous);
                }
            }

            public void Tick(float deltaTime)
            {
                foreach (var continuous in _items.OfType<DamageOverTime>().ToArray())
                {
                    continuous.Tick(deltaTime);
                    if (continuous.IsTerminated)
                    {
                        _items.Remove(continuous);
                    }
                }
            }
        }
    }
}
