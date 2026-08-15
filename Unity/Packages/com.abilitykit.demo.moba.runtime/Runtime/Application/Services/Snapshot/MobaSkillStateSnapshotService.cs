using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(85)]
    [WorldService(typeof(MobaSkillStateSnapshotService))]
    public sealed class MobaSkillStateSnapshotService : IService, IMobaSnapshotEmitter
    {
        private readonly MobaLogicWorldRunGateService _phase;
        private readonly MobaActorRegistry _registry;
        private readonly IFrameTime _time;
        private readonly MobaSnapshotBuffer<MobaSkillStateSnapshotEntry> _entries = new MobaSnapshotBuffer<MobaSkillStateSnapshotEntry>(16, 256);

        private FrameIndex _lastFrame;

        public MobaSkillStateSnapshotService(
            MobaLogicWorldRunGateService phase,
            MobaActorRegistry registry,
            IFrameTime time)
        {
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _lastFrame = new FrameIndex(-999999);
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (!_phase.InGame)
            {
                snapshot = default;
                return false;
            }

            if (frame.Value == _lastFrame.Value)
            {
                snapshot = default;
                return false;
            }
            _lastFrame = frame;

            BuildEntries();
            if (_entries.Count == 0)
            {
                snapshot = default;
                return false;
            }

            var payload = MobaSkillStateSnapshotCodec.Serialize(_entries.ToArrayClearAndTrim());
            snapshot = new WorldStateSnapshot(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.SkillState, payload);
            return true;
        }

        public void Dispose()
        {
            _entries.ClearAndTrim();
            _lastFrame = new FrameIndex(-999999);
        }

        private void BuildEntries()
        {
            _entries.Clear();
            var nowMs = MobaSkillRuntimeAccess.GetCurrentTimeMs(_time);

            // 按 ActorId 定序：快照条目顺序跨端必须一致（字典序不保证）。
            foreach (var actorId in _registry.CopyActorIdsInOrder())
            {
                if (!_registry.TryGetRegistered(actorId, out var actor) || actor == null || !actor.hasSkillLoadout) continue;

                var activeSkills = actor.skillLoadout.ActiveSkills;
                if (activeSkills == null || activeSkills.Length == 0) continue;

                for (int i = 0; i < activeSkills.Length; i++)
                {
                    var runtime = activeSkills[i];
                    if (runtime == null || runtime.SkillId <= 0) continue;

                    var isCoolingDown = runtime.CooldownEndTimeMs > nowMs;
                    var remainingMs = isCoolingDown
                        ? ClampToInt(runtime.CooldownEndTimeMs - nowMs)
                        : 0;
                    var totalMs = isCoolingDown ? Math.Max(runtime.CooldownDurationMs, remainingMs) : Math.Max(0, runtime.CooldownDurationMs);

                    _entries.Add(new MobaSkillStateSnapshotEntry
                    {
                        ActorId = actorId,
                        Slot = i + 1,
                        SkillId = runtime.SkillId,
                        Level = runtime.Level,
                        CooldownTotalMs = totalMs,
                        CooldownRemainingMs = remainingMs,
                        CooldownEndTimeMs = isCoolingDown ? runtime.CooldownEndTimeMs : 0L,
                        ServerTimeMs = nowMs,
                        Availability = isCoolingDown ? MobaSkillAvailabilityState.CoolingDown : MobaSkillAvailabilityState.Available,
                        DisableReason = 0
                    });
                }
            }
        }

        private static int ClampToInt(long value)
        {
            if (value <= 0L) return 0;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
