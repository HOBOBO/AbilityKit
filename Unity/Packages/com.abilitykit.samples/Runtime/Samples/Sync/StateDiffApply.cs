#nullable enable

using System.Collections.Generic;
using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Ability.StateSync.Diff;
using AbilityKit.Ability.StateSync.Snapshot;
using AbilityKit.Samples.Abstractions;
using StateSnapshot = AbilityKit.Ability.StateSync.Snapshot.WorldStateSnapshot;

namespace AbilityKit.Samples.Logic.Samples.Sync
{
    [Sample(952, "sync", "state", "diff", "apply", "package-api", "web", "deterministic", "fixed-frame")]
    public sealed class StateDiffApply : SampleBase
    {
        public override string Title => "Sync State Diff Apply";
        public override string Description => "使用 SnapshotBuffer、StateManager、StateDiffProvider 和 StateHashComputer 捕获、差异化并校验状态";
        public override SampleCategory Category => SampleCategory.World;

        protected override void OnRun()
        {
            var buffer = new SnapshotBuffer(maxBufferSize: 8);
            var diffProvider = new StateDiffProvider(DiffCompressionLevel.None);
            var stateManager = new StateManager(buffer, diffProvider);
            var hero = new SyncHero(entityId: 1001, teamId: 1);
            var hashProvider = new BattleHashProvider(hero);

            stateManager.RegisterRollbackable(hero);

            Section("捕获基线帧");
            hero.ApplyFrame(positionX: 0f, healthPercent: 100, stateFlags: 0u);
            stateManager.CaptureState(frame: 10);
            var baseBusinessHash = StateHashComputer.ComputeWithBusinessData(CreateSnapshot(frame: 10), hashProvider);
            LogState("frame=10", hero, baseBusinessHash);
            KeyValue("StateDiff.BaseCaptured", $"frame=10,x={hero.Position.X:0.0},hp={hero.HealthPercent}");

            Divider();
            Section("捕获目标帧");
            hero.ApplyFrame(positionX: 3f, healthPercent: 72, stateFlags: 1u);
            stateManager.CaptureState(frame: 11);
            var targetBusinessHash = StateHashComputer.ComputeWithBusinessData(CreateSnapshot(frame: 11), hashProvider);
            LogState("frame=11", hero, targetBusinessHash);
            KeyValue("StateDiff.TargetCaptured", $"frame=11,x={hero.Position.X:0.0},hp={hero.HealthPercent},flags={hero.StateFlags}");

            Divider();
            Section("计算并应用快照差异");
            var incrementalDiff = stateManager.ComputeDiff(fromFrame: 10, toFrame: 11);
            var targetSnapshot = diffProvider.DeserializeState<StateSnapshot>(stateManager.GetFullState(frame: 11));
            var fullDiff = diffProvider.ComputeDiff(targetSnapshot, previous: null);
            var appliedSnapshot = diffProvider.ApplyDiff<StateSnapshot>(baseState: null, fullDiff);

            KeyValue("IncrementalFullSnapshot", incrementalDiff.IsFullSnapshot.ToString());
            KeyValue("IncrementalDiffBytes", (incrementalDiff.CompressedData?.Length ?? 0).ToString());
            KeyValue("StateDiff.Computed", $"incrementalFull={incrementalDiff.IsFullSnapshot},bytes={incrementalDiff.CompressedData?.Length ?? 0}");
            KeyValue("AppliedFullSnapshot", fullDiff.IsFullSnapshot.ToString());
            KeyValue("AppliedFrame", appliedSnapshot.Frame.ToString());
            var frameHashMatched = StateHashComputer.ValidateHash(appliedSnapshot.ComputeHash(), targetSnapshot.ComputeHash());
            KeyValue("FrameHashMatched", frameHashMatched.ToString());
            KeyValue("StateDiff.HashMatched", frameHashMatched.ToString());

            Divider();
            Section("回滚到基线帧");
            var restored = stateManager.TryRestore(frame: 10);
            var restoredBusinessHash = StateHashComputer.ComputeWithBusinessData(CreateSnapshot(frame: 10), hashProvider);
            KeyValue("Restored", restored.ToString());
            KeyValue("StateDiff.Restored", $"restored={restored},x={hero.Position.X:0.0},hp={hero.HealthPercent}");
            LogState("rollback=10", hero, restoredBusinessHash);
        }

        private void LogState(string label, SyncHero hero, StateHash hash)
        {
            Log($"{label}: x={hero.Position.X:0.0}, hp={hero.HealthPercent}, flags={hero.StateFlags}, hash={hash}");
        }

        private static StateSnapshot CreateSnapshot(int frame)
        {
            return new StateSnapshot
            {
                WorldId = 7,
                Frame = frame,
                Timestamp = frame,
                WorldFlags = 1,
                IsFullSnapshot = true
            };
        }

        private sealed class SyncHero : IRollbackable, IHashableState
        {
            public SyncHero(long entityId, int teamId)
            {
                EntityId = entityId;
                TeamId = teamId;
                Rotation = Quat.Identity;
            }

            public long EntityId { get; }
            public int SnapshotKey => EntityId.GetHashCode();
            public Vec3 Position { get; private set; }
            public Quat Rotation { get; private set; }
            public Vec3 Velocity { get; private set; }
            public byte HealthPercent { get; private set; }
            public uint StateFlags { get; private set; }
            public long ActiveAbilityMask { get; private set; }
            public int TeamId { get; }
            public byte ControlFlags { get; private set; }

            public void ApplyFrame(float positionX, byte healthPercent, uint stateFlags)
            {
                Position = new Vec3(positionX, 0f, 0f);
                Velocity = new Vec3(1f, 0f, 0f);
                HealthPercent = healthPercent;
                StateFlags = stateFlags;
                ActiveAbilityMask = stateFlags == 0 ? 0 : 1L;
                ControlFlags = stateFlags == 0 ? (byte)0 : (byte)2;
            }

            public IRollbackState CreateRollbackState()
            {
                return new EntityRollbackState(EntityId)
                {
                    position = Position,
                    rotation = Rotation,
                    velocity = Velocity,
                    healthPercent = HealthPercent,
                    StateFlags = StateFlags,
                    ActiveAbilityMask = ActiveAbilityMask,
                    TeamId = TeamId,
                    ControlFlags = ControlFlags
                };
            }

            public void RestoreFromRollbackState(IRollbackState state)
            {
                if (state is not EntityRollbackState entityState)
                {
                    return;
                }

                Position = entityState.position;
                Rotation = entityState.rotation;
                Velocity = entityState.velocity;
                HealthPercent = entityState.healthPercent;
                StateFlags = entityState.StateFlags;
                ActiveAbilityMask = entityState.ActiveAbilityMask;
                ControlFlags = entityState.ControlFlags;
            }

            public ulong ComputeHash()
            {
                unchecked
                {
                    var hash = (ulong)1469598103934665603UL;
                    hash = (hash ^ (ulong)EntityId) * 1099511628211UL;
                    hash = (hash ^ (ulong)(int)(Position.X * 1000f)) * 1099511628211UL;
                    hash = (hash ^ HealthPercent) * 1099511628211UL;
                    hash = (hash ^ StateFlags) * 1099511628211UL;
                    hash = (hash ^ (ulong)ActiveAbilityMask) * 1099511628211UL;
                    return hash;
                }
            }
        }

        private sealed class BattleHashProvider : IBusinessHashProvider
        {
            private readonly IReadOnlyList<IHashableState> _states;

            public BattleHashProvider(params IHashableState[] states)
            {
                _states = states;
            }

            public ulong GetAllBusinessEntityHashes()
            {
                unchecked
                {
                    ulong hash = 0x9E3779B97F4A7C15UL;
                    foreach (var state in _states)
                    {
                        hash ^= state.ComputeHash() + 0x9E3779B97F4A7C15UL + (hash << 6) + (hash >> 2);
                    }

                    return hash;
                }
            }
        }
    }
}
