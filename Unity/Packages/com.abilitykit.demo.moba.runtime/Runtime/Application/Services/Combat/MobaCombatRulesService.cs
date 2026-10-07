using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.GameplayTags;
using moba_luban;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaCombatRuleFailure
    {
        None = 0,
        InvalidActor = 1,
        Dead = 2,
        SameTeamBlocked = 3,
        EnemyTeamBlocked = 4,
        Untargetable = 5,
        Invulnerable = 6,
        Silenced = 7,
        Stunned = 8,
        Disabled = 9,
    }

    public readonly struct MobaCombatRuleResult
    {
        public static readonly MobaCombatRuleResult Pass = new MobaCombatRuleResult(true, MobaCombatRuleFailure.None, null);

        public MobaCombatRuleResult(bool passed, MobaCombatRuleFailure failure, string message)
        {
            Passed = passed;
            Failure = failure;
            Message = message;
        }

        public bool Passed { get; }
        public MobaCombatRuleFailure Failure { get; }
        public string Message { get; }

        public static MobaCombatRuleResult Fail(MobaCombatRuleFailure failure, string message)
        {
            return new MobaCombatRuleResult(false, failure, message);
        }
    }

    [WorldService(typeof(MobaCombatRulesService))]
    public sealed class MobaCombatRulesService : IService
    {
        private readonly MobaActorLookupService _actors;
        private readonly IMobaEffectiveTagQueryService _effectiveTags;
        private readonly IMobaTagResultQueryService _tagResults;
        private readonly IMobaTagBehaviorGateQueryService _behaviorGates;

        public MobaCombatRulesService(MobaActorLookupService actors, IMobaEffectiveTagQueryService effectiveTags = null)
        {
            _actors = actors;
            _effectiveTags = effectiveTags;
            _tagResults = effectiveTags as IMobaTagResultQueryService;
            _behaviorGates = effectiveTags as IMobaTagBehaviorGateQueryService;
            if (effectiveTags != null && (_tagResults == null || _behaviorGates == null))
                throw new ArgumentException("MOBA effective tags must provide cached results and behavior gates.", nameof(effectiveTags));
        }

        public bool TryGetActor(int actorId, out global::ActorEntity actor)
        {
            actor = null;
            return actorId > 0 && _actors != null && _actors.TryGetActorEntity(actorId, out actor) && actor != null;
        }

        public bool IsAlive(int actorId)
        {
            return TryGetActor(actorId, out var actor) && IsAlive(actor);
        }

        public bool IsAlive(global::ActorEntity actor)
        {
            if (actor == null) return false;
            if (!actor.hasAttributeGroup) return true;

            var attrs = actor.GetMobaAttrs();
            return attrs.Hp > 0f;
        }

        public bool AreSameTeam(int actorId, int otherActorId)
        {
            return TryGetTeam(actorId, out var team) && TryGetTeam(otherActorId, out var otherTeam) && team != Team.None && team == otherTeam;
        }

        public bool AreEnemies(int actorId, int otherActorId)
        {
            return TryGetTeam(actorId, out var team) && TryGetTeam(otherActorId, out var otherTeam) && team != Team.None && otherTeam != Team.None && team != otherTeam;
        }

        public bool TryGetTeam(int actorId, out Team team)
        {
            team = Team.None;
            if (!TryGetActor(actorId, out var actor)) return false;
            if (!actor.hasTeam) return false;

            team = actor.team.Value;
            return team != Team.None;
        }

        public bool HasAnyTag(int actorId, GameplayTagContainer query)
        {
            return TryGetAnyTag(actorId, query, out _);
        }

        public bool TryGetAnyTag(int actorId, GameplayTagContainer query, out GameplayTag matchedTag)
        {
            matchedTag = default;
            if (actorId <= 0 || query == null || query.Count == 0) return false;
            if (_effectiveTags == null) return false;

            foreach (var tag in query)
            {
                if (_effectiveTags.HasTag(actorId, tag))
                {
                    matchedTag = tag;
                    return true;
                }
            }

            return false;
        }

        public bool IsUntargetable(int actorId)
        {
            return IsBlocked(actorId, MobaTagBehaviorGate.Targetable);
        }

        public bool IsInvulnerable(int actorId)
        {
            return IsBlocked(actorId, MobaTagBehaviorGate.Damageable);
        }

        public bool IsSilenced(int actorId)
        {
            return IsBlocked(actorId, MobaTagBehaviorGate.Cast);
        }

        public bool IsStunned(int actorId)
        {
            return IsBlocked(actorId, MobaTagBehaviorGate.Stunned);
        }

        public bool CanMove(int actorId)
        {
            return IsAlive(actorId) && !IsBlocked(actorId, MobaTagBehaviorGate.Move);
        }

        public bool CanAttack(int actorId)
        {
            return IsAlive(actorId) && !IsBlocked(actorId, MobaTagBehaviorGate.Attack);
        }

        public bool CanBeControlled(int actorId)
        {
            return IsAlive(actorId)
                && !IsBlocked(actorId, MobaTagBehaviorGate.Controlled);
        }

        private bool IsBlocked(int actorId, MobaTagBehaviorGate gateId)
        {
            if (actorId <= 0 || _behaviorGates == null) return false;
            if (!_behaviorGates.TryIsBlocked(actorId, gateId, out var blocked))
                throw new InvalidOperationException($"Missing MOBA tag behavior gate: {gateId}");
            return blocked;
        }

        public bool HasAnyTagResult(int actorId) =>
            (_tagResults?.GetResultFlags(actorId) ?? MobaTagResultFlags.None) != MobaTagResultFlags.None;

        public MobaCombatRuleResult CanBeSearchedTarget(int casterActorId, int targetActorId)
        {
            if (!TryGetActor(targetActorId, out _)) return MobaCombatRuleResult.Fail(MobaCombatRuleFailure.InvalidActor, "target_not_found");
            if (!IsAlive(targetActorId)) return MobaCombatRuleResult.Fail(MobaCombatRuleFailure.Dead, "target_dead");
            if (IsUntargetable(targetActorId)) return MobaCombatRuleResult.Fail(MobaCombatRuleFailure.Untargetable, "target_untargetable");
            return MobaCombatRuleResult.Pass;
        }

        public MobaCombatRuleResult CanReceiveDamage(int attackerActorId, int targetActorId)
        {
            if (!TryGetActor(targetActorId, out _)) return MobaCombatRuleResult.Fail(MobaCombatRuleFailure.InvalidActor, "target_not_found");
            if (!IsAlive(targetActorId)) return MobaCombatRuleResult.Fail(MobaCombatRuleFailure.Dead, "target_dead");
            if (IsInvulnerable(targetActorId)) return MobaCombatRuleResult.Fail(MobaCombatRuleFailure.Invulnerable, "target_invulnerable");
            return MobaCombatRuleResult.Pass;
        }

        public MobaCombatRuleResult CanCastSkill(int casterActorId, ulong executionBlockedResultMask)
        {
            if (!TryGetActor(casterActorId, out _)) return MobaCombatRuleResult.Fail(MobaCombatRuleFailure.InvalidActor, "caster_not_found");
            if (!IsAlive(casterActorId)) return MobaCombatRuleResult.Fail(MobaCombatRuleFailure.Dead, "caster_dead");
            if (executionBlockedResultMask != 0UL &&
                (_tagResults == null || ((ulong)_tagResults.GetResultFlags(casterActorId) & executionBlockedResultMask) != 0UL))
                return MobaCombatRuleResult.Fail(MobaCombatRuleFailure.Disabled, "caster_execution_blocked");
            return MobaCombatRuleResult.Pass;
        }

        public void Dispose()
        {
        }
    }
}
