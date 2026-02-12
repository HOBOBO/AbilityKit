using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.Room
{
    public sealed class MobaRoomOrchestrator : IMobaRoomOrchestrator
    {
        public MobaRoomState State { get; }

        private readonly IMobaRoomGameStartSpecBuilder _specBuilder;

        private readonly List<Action<MobaRoomChangedArgs>> _changed = new List<Action<MobaRoomChangedArgs>>(8);
        private MobaRoomSnapshot _snapshot;
        private bool _snapshotDirty = true;

        private readonly Dictionary<string, int> _lastClientSeq = new Dictionary<string, int>();

        public MobaRoomSnapshot Snapshot
        {
            get
            {
                if (_snapshotDirty)
                {
                    _snapshot = State.BuildSnapshot();
                    _snapshotDirty = false;
                }
                return _snapshot;
            }
        }

        public MobaRoomOrchestrator(MobaRoomState state, IMobaRoomGameStartSpecBuilder specBuilder = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            _specBuilder = specBuilder ?? new DefaultMobaRoomGameStartSpecBuilder();
        }

        public void AddChanged(Action<MobaRoomChangedArgs> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _changed.Add(handler);
        }

        public void RemoveChanged(Action<MobaRoomChangedArgs> handler)
        {
            if (handler == null) return;
            _changed.Remove(handler);
        }

        public bool TryJoin(PlayerId playerId, int teamId = 0)
        {
            var ok = State.TryJoin(playerId, teamId);
            if (ok) OnChanged(new MobaRoomChangedArgs(MobaRoomChangeKind.PlayerJoined, playerId, State.Revision));
            return ok;
        }

        public bool TryLeave(PlayerId playerId)
        {
            var ok = State.TryLeave(playerId);
            if (ok) OnChanged(new MobaRoomChangedArgs(MobaRoomChangeKind.PlayerLeft, playerId, State.Revision));
            return ok;
        }

        public bool TrySetReady(PlayerId playerId, bool ready)
        {
            var ok = State.TrySetReady(playerId, ready);
            if (ok) OnChanged(new MobaRoomChangedArgs(MobaRoomChangeKind.ReadyChanged, playerId, State.Revision));
            return ok;
        }

        public bool TryPickHero(PlayerId playerId, int heroId, int attributeTemplateId = 0, int level = 1, int basicAttackSkillId = 0, int[] skillIds = null)
        {
            var ok = State.TryPickHero(playerId, heroId, attributeTemplateId, level, basicAttackSkillId, skillIds);
            if (ok) OnChanged(new MobaRoomChangedArgs(MobaRoomChangeKind.HeroPicked, playerId, State.Revision));
            return ok;
        }

        public bool TrySetSpawnPoint(PlayerId playerId, int spawnPointId)
        {
            var ok = State.TrySetSpawnPoint(playerId, spawnPointId);
            if (ok) OnChanged(new MobaRoomChangedArgs(MobaRoomChangeKind.SpawnPointChanged, playerId, State.Revision));
            return ok;
        }

        public bool TryBuildGameStartSpec(PlayerId localPlayerId, out MobaGameStartSpec spec)
            => State.TryBuildGameStartSpec(localPlayerId, out spec);

        public bool TryBuildRoomGameStartSpec(out MobaRoomGameStartSpec spec)
            => _specBuilder.TryBuild(State, out spec);

        public MobaRoomCommandResult Apply(in MobaRoomCommand command)
        {
            if (string.IsNullOrEmpty(command.PlayerId.Value)) return MobaRoomCommandResult.Fail(MobaRoomCommandError.InvalidPlayerId, 0);

            var currentRev = State.Revision;

            var clientSeq = command.ClientSeq;
            if (clientSeq > 0 && _lastClientSeq.TryGetValue(command.PlayerId.Value, out var lastSeq) && clientSeq <= lastSeq)
            {
                return MobaRoomCommandResult.Success(currentRev);
            }

            var expected = command.ExpectedRevision;
            if (expected > 0 && expected != currentRev)
            {
                return MobaRoomCommandResult.Fail(MobaRoomCommandError.StaleRevision, currentRev);
            }

            switch (command.Kind)
            {
                case MobaRoomCommandKind.Join:
                {
                    if (State.Players.ContainsKey(command.PlayerId.Value)) return MobaRoomCommandResult.Fail(MobaRoomCommandError.PlayerAlreadyExists, currentRev);
                    if (State.MaxPlayers > 0 && State.Players.Count >= State.MaxPlayers) return MobaRoomCommandResult.Fail(MobaRoomCommandError.RoomFull, currentRev);

                    if (!TryJoin(command.PlayerId, command.TeamId)) return MobaRoomCommandResult.Fail(MobaRoomCommandError.InvalidCommand, currentRev);

                    if (clientSeq > 0) _lastClientSeq[command.PlayerId.Value] = clientSeq;
                    return MobaRoomCommandResult.Success(State.Revision);
                }

                case MobaRoomCommandKind.Leave:
                {
                    if (!State.Players.ContainsKey(command.PlayerId.Value)) return MobaRoomCommandResult.Fail(MobaRoomCommandError.PlayerNotFound, currentRev);
                    if (!TryLeave(command.PlayerId)) return MobaRoomCommandResult.Fail(MobaRoomCommandError.InvalidCommand, currentRev);

                    _lastClientSeq.Remove(command.PlayerId.Value);
                    return MobaRoomCommandResult.Success(State.Revision);
                }

                case MobaRoomCommandKind.SetReady:
                {
                    if (!State.Players.ContainsKey(command.PlayerId.Value)) return MobaRoomCommandResult.Fail(MobaRoomCommandError.PlayerNotFound, currentRev);
                    var ready = command.Ready != 0;
                    if (!TrySetReady(command.PlayerId, ready)) return MobaRoomCommandResult.Fail(MobaRoomCommandError.InvalidCommand, currentRev);

                    if (clientSeq > 0) _lastClientSeq[command.PlayerId.Value] = clientSeq;
                    return MobaRoomCommandResult.Success(State.Revision);
                }

                case MobaRoomCommandKind.PickHero:
                {
                    if (!State.Players.ContainsKey(command.PlayerId.Value)) return MobaRoomCommandResult.Fail(MobaRoomCommandError.PlayerNotFound, currentRev);
                    if (command.HeroId <= 0) return MobaRoomCommandResult.Fail(MobaRoomCommandError.InvalidHeroId, currentRev);

                    if (!TryPickHero(
                            command.PlayerId,
                            command.HeroId,
                            command.AttributeTemplateId,
                            command.Level > 0 ? command.Level : 1,
                            command.BasicAttackSkillId,
                            command.SkillIds))
                        return MobaRoomCommandResult.Fail(MobaRoomCommandError.InvalidCommand, currentRev);

                    if (clientSeq > 0) _lastClientSeq[command.PlayerId.Value] = clientSeq;
                    return MobaRoomCommandResult.Success(State.Revision);
                }

                case MobaRoomCommandKind.SetSpawnPoint:
                {
                    if (!State.Players.ContainsKey(command.PlayerId.Value)) return MobaRoomCommandResult.Fail(MobaRoomCommandError.PlayerNotFound, currentRev);
                    if (command.SpawnPointId < 0) return MobaRoomCommandResult.Fail(MobaRoomCommandError.InvalidSpawnPointId, currentRev);

                    if (!TrySetSpawnPoint(command.PlayerId, command.SpawnPointId)) return MobaRoomCommandResult.Fail(MobaRoomCommandError.InvalidCommand, currentRev);

                    if (clientSeq > 0) _lastClientSeq[command.PlayerId.Value] = clientSeq;
                    return MobaRoomCommandResult.Success(State.Revision);
                }
            }

            return MobaRoomCommandResult.Fail(MobaRoomCommandError.InvalidCommand, currentRev);
        }

        private void OnChanged(in MobaRoomChangedArgs args)
        {
            _snapshotDirty = true;
            if (_changed.Count == 0) return;

            for (int i = 0; i < _changed.Count; i++)
            {
                try { _changed[i]?.Invoke(args); }
                catch { }
            }
        }
    }
}
