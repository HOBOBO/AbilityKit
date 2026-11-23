#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Triggering.Eventing;
using Newtonsoft.Json;

namespace AbilityKit.Game.Test.UnitTest
{
    public static partial class MobaMultiplayerHeadlessClientCommand
    {
        private static SummonRejectionProbe? _summonRejection;

        /// <summary>
        /// 通过正常输入制造权威序号拒绝，采集真实预测实体和同帧名单，不注入结算记录。
        /// </summary>
        private sealed class SummonRejectionProbe : IDisposable
        {
            private const int SeedSequence = 200;
            private const int StaleSequence = 199;
            private const int SummonConfig = 30030502;
            private const int SummonSkill = 30030501;
            private readonly BattleContext _context;
            private readonly ClientOptions _options;
            private readonly IClientPredictionDriverStats _stats;
            private readonly WorldId _worldId;
            private readonly MobaActorRegistry _actors;
            private readonly MobaEntityManager _entities;
            private readonly MobaActionAckSnapshotService _acks;
            private readonly MobaSummonService _summons;
            private readonly PlayerId _ownerPlayer;
            private readonly int _ownerActor;
            private readonly int _ownerVersion;
            private readonly IDisposable _spawnSubscription;
            private readonly List<int> _rosterIds = new List<int>();
            private readonly string _framePath;
            private readonly bool _isOwner;
            private readonly DateTime _started = DateTime.UtcNow;
            private int _seedFrame;
            private int _frame;
            private int _spawnActor;
            private int _spawnVersion;
            private int _spawnFrame;
            private int _confirmedAtSpawn;
            private int _rollbackFrame = -1;
            private long _rollbacksAtSample;
            private string? _roster;
            private string? _evidenceFault;

            internal SummonRejectionProbe(BattleContext context, ClientOptions options)
            {
                _context = context;
                _options = options;
                _isOwner = options.Role == ClientRole.Owner;
                _framePath = options.SkillSignalPath + ".summon-frame";
                if (context.SettledFrames?.CanSubmit != true || !context.Plan.Authority.EnableClientPrediction ||
                    !context.TryGetRuntimeWorld(out var world) || context.PredictionStats == null)
                    throw new InvalidOperationException("Summon rejection requires an authenticated settled prediction world.");

                _stats = context.PredictionStats;
                _worldId = world.Id;
                _actors = world.Services.Resolve<MobaActorRegistry>();
                _entities = world.Services.Resolve<MobaEntityManager>();
                _acks = world.Services.Resolve<MobaActionAckSnapshotService>();
                _summons = world.Services.Resolve<MobaSummonService>();
                var room = _controller!.CurrentSnapshot;
                var owner = room.Players.Single(player => player.AccountId == room.OwnerAccountId);
                _ownerPlayer = new PlayerId(owner.PlayerId.ToString(CultureInfo.InvariantCulture));
                var map = world.Services.Resolve<MobaPlayerActorMapService>();
                if (owner.HeroId != 3003 || !map.TryGetActorId(_ownerPlayer, out _ownerActor) ||
                    (_ownerVersion = _actors.GetEntityVersion(_ownerActor)) <= 0 || _summons.GetAliveCount(_ownerActor) != 0)
                    throw new InvalidOperationException("The probe requires a clean Annie owner with a known runtime identity.");

                var eventBus = world.Services.Resolve<IEventBus>();
                _spawnSubscription = eventBus.Subscribe(new EventKey<SummonEventPayload>(
                    TriggeringIdUtil.GetEventEid(MobaSummonTriggering.Events.Spawned)), OnSpawn);
                context.FrameSnapshots.FrameReceived += OnSnapshot;
            }

            internal bool Tick()
            {
                if (_context.SettledFrames?.Fault is { } fault)
                    throw new InvalidOperationException(fault);
                if (_evidenceFault != null) throw new InvalidOperationException(_evidenceFault);
                if (DateTime.UtcNow - _started > TimeSpan.FromSeconds(30))
                    throw new TimeoutException($"Summon evidence timed out: seed={_seedFrame}, target={_frame}, spawn={_spawnActor}, roster={_roster}.");

                if (_isOwner && _seedFrame == 0)
                {
                    _seedFrame = FutureFrame();
                    var seed = BattleInputCommandFactory.CreateSkillSlot(_seedFrame, _ownerPlayer, slot: 3,
                        predictionKey: SeedSequence, inputSequence: SeedSequence, entityVersion: _ownerVersion,
                        interruptEpoch: _context.ActionInterruptEpoch);
                    Submit(in seed);
                    return false;
                }

                if (_isOwner && _frame == 0)
                {
                    if (!_acks.TryGetDecision(_ownerActor, _ownerVersion, SeedSequence, out var seed)) return false;
                    if (!seed.Accepted || seed.AuthoritativeFrame != _seedFrame)
                        throw new InvalidOperationException($"Sequence seed was not executed: frame={seed.AuthoritativeFrame}, reason={seed.Reason}.");
                    if (!ReadFrames(out var confirmed, out _) || confirmed < _seedFrame + 45) return false;
                    if (!TryGetOwnerRuntimePosition(_context, out var position, out _)) return false;
                    _frame = FutureFrame();
                    File.WriteAllText(_framePath, _frame.ToString(CultureInfo.InvariantCulture));
                    var cast = BattleInputCommandFactory.CreateSkillAimRelease(_frame, _ownerPlayer, slot: 4,
                        position.x + 2f, position.y, position.z, 1f, 0f, 0f,
                        StaleSequence, StaleSequence, _ownerVersion, _context.ActionInterruptEpoch);
                    Submit(in cast);
                    return false;
                }

                ObserveFrame();
                if (_frame == 0 || !ReadFrames(out var settled, out _) || settled < _frame) return false;
                if (!_acks.TryGetDecision(_ownerActor, _ownerVersion, StaleSequence, out var rejected) ||
                    rejected.Accepted || rejected.AuthoritativeFrame != _frame || rejected.SkillId != SummonSkill ||
                    rejected.Reason != (int)MobaActionAckReason.InvalidInput)
                    throw new InvalidOperationException($"Missing authority sequence rejection for the summon at {_frame}: {rejected.Reason}.");
                if (_isOwner && (_spawnActor <= 0 || _spawnVersion <= 0 || _confirmedAtSpawn >= _frame ||
                    _spawnFrame < _frame || _rollbackFrame != _frame - 1))
                    throw new InvalidOperationException($"Missing predicted summon or F-1 restore: summon={_spawnActor}:{_spawnVersion}, " +
                        $"spawnFrame={_spawnFrame}, confirmedAtSpawn={_confirmedAtSpawn}, rollback={_rollbackFrame}, target={_frame}.");
                if (!_isOwner && (_spawnActor != 0 || _rollbacksAtSample != _stats.TotalRollbackCount))
                    throw new InvalidOperationException("Member did not retain an unchanged simulation of the rejected frame.");
                if (_roster == null || _summons.GetAliveCount(_ownerActor) != 0 ||
                    (_spawnActor > 0 && (_actors.TryGet(_spawnActor, out _) || _entities.TryGetActorEntity(_spawnActor, out _))) ||
                    _stats.TotalRollbackRestoreFailed != 0)
                    throw new InvalidOperationException($"Rejected summon is not cleanly absent at frame {_frame}: roster={_roster}.");

                File.WriteAllText(_options.ResultPath + ".summon.json", JsonConvert.SerializeObject(new
                {
                    frame = _frame, confirmed = settled, roster = _roster, ownerActor = _ownerActor, ownerVersion = _ownerVersion,
                    predictionKey = StaleSequence, reason = rejected.Reason, authoritySequence = rejected.AuthoritySequence,
                    spawnActor = _spawnActor, spawnVersion = _spawnVersion, spawnFrame = _spawnFrame,
                    confirmedAtSpawn = _confirmedAtSpawn, restoreFrame = _rollbackFrame
                }, Formatting.Indented));
                return true;
            }

            private void Submit(in PlayerInputCommand command)
            {
                if (!_context.InputRuntime.Submit(in command, _ownerPlayer, _worldId))
                    throw new InvalidOperationException("The normal battle input port refused the probe input.");
                _context.LocalInputQueue.Flush();
            }

            private int FutureFrame() => (_context.SettledFrames!.Receiver.MaxReceivedFrame) + 9;

            private bool ReadFrames(out int confirmed, out int predicted)
            {
                var found = _stats.TryGetFrames(_worldId, out var confirmedIndex, out var predictedIndex);
                confirmed = confirmedIndex.Value;
                predicted = predictedIndex.Value;
                return found;
            }

            private void OnSpawn(SummonEventPayload payload)
            {
                if (payload.RootOwnerActorId != _ownerActor || payload.SummonId != SummonConfig) return;
                if (_spawnActor != 0 || !ReadFrames(out _confirmedAtSpawn, out _spawnFrame))
                {
                    _evidenceFault = "Duplicate or unframed summon event.";
                    return;
                }
                _spawnActor = payload.SummonActorId;
                _spawnVersion = _actors.GetEntityVersion(_spawnActor);
            }

            private void OnSnapshot(ISnapshotEnvelope envelope)
            {
                if (envelope.WorldId.Equals(_worldId)) ObserveFrame();
            }

            private void ObserveFrame()
            {
                if (_frame == 0 && !_isOwner && File.Exists(_framePath) &&
                    int.TryParse(File.ReadAllText(_framePath), NumberStyles.None, CultureInfo.InvariantCulture, out var frame))
                    _frame = frame;
                if (_frame == 0 || !ReadFrames(out var confirmed, out var predicted) || predicted != _frame ||
                    (_isOwner && confirmed != _frame)) return;
                if (_isOwner) _rollbackFrame = _stats.LastRollbackFrame.Value;
                _rollbacksAtSample = _stats.TotalRollbackCount;
                _entities.GetRegisteredActorIds(_rosterIds);
                _rosterIds.Sort();
                _roster = string.Join(",", _rosterIds.Select(id => $"{id}:{_actors.GetEntityVersion(id)}"));
            }

            public void Dispose()
            {
                _context.FrameSnapshots.FrameReceived -= OnSnapshot;
                _spawnSubscription.Dispose();
            }
        }
    }
}
