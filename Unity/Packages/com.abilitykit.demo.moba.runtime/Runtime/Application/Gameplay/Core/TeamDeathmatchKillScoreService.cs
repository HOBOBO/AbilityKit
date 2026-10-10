using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;

namespace AbilityKit.Demo.Moba.Gameplay
{
    [WorldService(typeof(TeamDeathmatchKillScoreService))]
    public sealed class TeamDeathmatchKillScoreService : IService
    {
        private readonly MobaGameplayService _gameplay;
        private readonly MobaActorRegistry _actors;
        private readonly MobaGameplayVariableService _variables;

        public TeamDeathmatchKillScoreService(MobaGameplayService gameplay,
            MobaActorRegistry actors, MobaGameplayVariableService variables)
        {
            _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        }

        public bool TryAwardHeroKill(in UnitDieEventPayload death, int scoreKeyBaseId, double points,
            out int teamId, out double score)
        {
            teamId = 0;
            score = 0d;
            if (!_gameplay.IsRunning || scoreKeyBaseId <= 0 || points <= 0d ||
                death.KillerActorId <= 0 || death.ActorId <= 0 || death.KillerActorId == death.ActorId ||
                !_actors.TryGet(death.KillerActorId, out var killer) ||
                !_actors.TryGet(death.ActorId, out var victim) ||
                !killer.hasTeam || !victim.hasTeam || !killer.hasUnitSubType || !victim.hasUnitSubType ||
                killer.unitSubType.Value != UnitSubType.Hero ||
                victim.unitSubType.Value != UnitSubType.Hero ||
                (killer.team.Value != Team.Team1 && killer.team.Value != Team.Team2) ||
                (victim.team.Value != Team.Team1 && victim.team.Value != Team.Team2) ||
                killer.team.Value == victim.team.Value)
                return false;

            teamId = (int)killer.team.Value;
            score = _variables.Add(checked(scoreKeyBaseId + teamId), points);
            _gameplay.PublishScoreChanged("hero_kill", teamId, score);
            return true;
        }

        public void Dispose() { }
    }
}
