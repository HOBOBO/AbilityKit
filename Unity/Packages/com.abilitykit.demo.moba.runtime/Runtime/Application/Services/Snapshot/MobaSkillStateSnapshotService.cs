using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(85)]
    [WorldService(typeof(MobaSkillStateSnapshotService))]
    public sealed class MobaSkillStateSnapshotService : IService, IWorldInitializable, IMobaSnapshotEmitter
    {
        private readonly MobaLogicWorldRunGateService _phase;
        private readonly MobaActorRegistry _registry;
        private readonly IFrameTime _time;
        private readonly MobaSkillEconomyService _economy;
        private MobaSkillSlotCandidateService _slotCandidates;
        private MobaSkillCastRuntimeService _skillRuntimes;
        private MobaSkillWindowRuntimeService _skillWindows;
        private readonly MobaSnapshotBuffer<MobaSkillStateSnapshotEntry> _entries = new MobaSnapshotBuffer<MobaSkillStateSnapshotEntry>(16, 256);

        private FrameIndex _lastFrame;

        public MobaSkillStateSnapshotService(
            MobaLogicWorldRunGateService phase,
            MobaActorRegistry registry,
            IFrameTime time,
            MobaSkillEconomyService economy)
        {
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _lastFrame = new FrameIndex(-999999);
        }

        public void OnInit(IWorldResolver services)
        {
            services?.TryResolve(out _slotCandidates);
            services?.TryResolve(out _skillRuntimes);
            services?.TryResolve(out _skillWindows);
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

                    var effectiveSkillId = _slotCandidates?.Resolve(actorId, i + 1, runtime.SkillId).EffectiveSkillId
                        ?? runtime.SkillId;

                    MobaSkillEconomyService.RefreshCharges(runtime, nowMs);

                    var sharedEndTimeMs = _economy.GetCooldownGroupEndTimeMs(actorId, runtime.CooldownGroupId);
                    var globalEndTimeMs = runtime.IgnoreGlobalCooldown ? 0L : _economy.GetGlobalCooldownEndTimeMs(actorId);
                    var chargeEndTimeMs = runtime.MaxCharges > 1 && runtime.CurrentCharges <= 0
                        ? runtime.NextChargeRecoveryTimeMs
                        : 0L;
                    var effectiveEndTimeMs = Math.Max(runtime.CooldownEndTimeMs,
                        Math.Max(sharedEndTimeMs, Math.Max(globalEndTimeMs, chargeEndTimeMs)));
                    var isCoolingDown = effectiveEndTimeMs > nowMs;
                    var remainingMs = isCoolingDown ? ClampToInt(effectiveEndTimeMs - nowMs) : 0;
                    var totalMs = isCoolingDown ? Math.Max(runtime.CooldownDurationMs, remainingMs) : Math.Max(0, runtime.CooldownDurationMs);
                    var sharedRemainingMs = ClampToInt(sharedEndTimeMs - nowMs);
                    var globalRemainingMs = ClampToInt(globalEndTimeMs - nowMs);
                    var chargeRecoveryRemainingMs = ClampToInt(runtime.NextChargeRecoveryTimeMs - nowMs);
                    ResolveCastingState(actorId, i + 1, runtime.SkillId,
                        out var castingState, out var castElapsedMs, out var castTotalMs,
                        out var castTier, out var castMaxTier);
                    _entries.Add(new MobaSkillStateSnapshotEntry
                    {
                        ActorId = actorId,
                        Slot = i + 1,
                        SkillId = effectiveSkillId,
                        Level = runtime.Level,
                        CooldownTotalMs = totalMs,
                        CooldownRemainingMs = remainingMs,
                        CooldownEndTimeMs = isCoolingDown ? effectiveEndTimeMs : 0L,
                        ServerTimeMs = nowMs,
                        Availability = isCoolingDown ? MobaSkillAvailabilityState.CoolingDown : MobaSkillAvailabilityState.Available,
                        DisableReason = 0,
                        MaxCharges = Math.Max(1, runtime.MaxCharges),
                        CurrentCharges = Math.Max(0, runtime.CurrentCharges),
                        ChargeRecoveryRemainingMs = chargeRecoveryRemainingMs,
                        SharedCooldownRemainingMs = sharedRemainingMs,
                        GlobalCooldownRemainingMs = globalRemainingMs,
                        CastingState = castingState,
                        CastElapsedMs = castElapsedMs,
                        CastTotalMs = castTotalMs,
                        CastTier = castTier,
                        CastMaxTier = castMaxTier,
                    });
                }
            }
        }

        private void ResolveCastingState(int actorId, int slot, int skillId,
            out MobaSkillCastingState state, out int elapsedMs, out int totalMs,
            out int tier, out int maxTier)
        {
            state = MobaSkillCastingState.None;
            elapsedMs = totalMs = tier = maxTier = 0;
            if (_skillRuntimes == null || _skillWindows == null) return;

            if (!_skillRuntimes.TryGetActiveSkill(actorId, slot, skillId, out var runtime)) return;
            var handle = runtime.Handle;
            if (!_skillWindows.TryGetSnapshot(in handle, out var window) || !window.IsOpen ||
                window.Kind != AbilityKit.Demo.Moba.Share.Config.SkillWindowKind.Charge) return;

            state = MobaSkillCastingState.Charging;
            elapsedMs = Math.Max(0, window.ElapsedMs);
            totalMs = Math.Max(0, window.TotalMs);
            tier = Math.Max(0, window.ChargeTier);
            maxTier = Math.Max(0, window.MaxChargeTier);
        }

        private static int ClampToInt(long value)
        {
            if (value <= 0L) return 0;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
