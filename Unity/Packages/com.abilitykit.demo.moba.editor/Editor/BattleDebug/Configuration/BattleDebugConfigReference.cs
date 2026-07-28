using System;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Game.Editor
{
    internal enum BattleDebugConfigKind
    {
        Unknown = 0,
        Skill = 1,
        SkillFlow = 2,
        TriggerPlan = 3,
        Effect = 4,
        Buff = 5,
        Projectile = 6,
        Area = 7,
        Summon = 8,
        ContinuousProcess = 9,
        PresentationTemplate = 10,
    }

    internal readonly struct BattleDebugConfigReference : IEquatable<BattleDebugConfigReference>
    {
        public BattleDebugConfigReference(
            BattleDebugConfigKind kind,
            int id,
            string phaseId = null)
        {
            Kind = kind;
            Id = id;
            PhaseId = phaseId ?? string.Empty;
        }

        public BattleDebugConfigKind Kind { get; }
        public int Id { get; }
        public string PhaseId { get; }
        public bool IsValid => Kind != BattleDebugConfigKind.Unknown && Id > 0;

        public bool Equals(BattleDebugConfigReference other)
        {
            return Kind == other.Kind &&
                   Id == other.Id &&
                   string.Equals(PhaseId, other.PhaseId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDebugConfigReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Kind;
                hashCode = (hashCode * 397) ^ Id;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(PhaseId ?? string.Empty);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(PhaseId)
                ? $"{Kind} #{Id}"
                : $"{Kind} #{Id} / {PhaseId}";
        }
    }

    internal static class BattleDebugConfigReferenceMapper
    {
        public static bool TryFromEvent(
            in BattleDiagnosticEvent diagnosticEvent,
            out BattleDebugConfigReference reference)
        {
            if (diagnosticEvent.Kind == BattleDiagnosticEventKind.TriggerAnalysis &&
                diagnosticEvent.Payload.TryGetTriggerAnalysis(out var trigger))
            {
                return TryCreate(BattleDebugConfigKind.TriggerPlan, trigger.TriggerId, out reference);
            }

            var kind = MapEventKind(diagnosticEvent.Kind);
            return TryCreate(kind, diagnosticEvent.ConfigId, out reference);
        }

        public static bool TryFromTraceNode(
            in BattleDiagnosticTraceNodeSummary node,
            out BattleDebugConfigReference reference)
        {
            if (!Enum.TryParse(node.Kind, true, out MobaTraceKind traceKind))
            {
                reference = default;
                return false;
            }

            if (traceKind == MobaTraceKind.SkillPhase && node.CastFlowId > 0)
            {
                reference = new BattleDebugConfigReference(
                    BattleDebugConfigKind.SkillFlow,
                    node.CastFlowId,
                    node.PhaseId);
                return true;
            }

            return TryCreate(MapTraceKind(traceKind), node.ConfigId, out reference);
        }

        private static bool TryCreate(
            BattleDebugConfigKind kind,
            int id,
            out BattleDebugConfigReference reference)
        {
            reference = new BattleDebugConfigReference(kind, id);
            if (reference.IsValid) return true;

            reference = default;
            return false;
        }

        private static BattleDebugConfigKind MapEventKind(BattleDiagnosticEventKind kind)
        {
            switch (kind)
            {
                case BattleDiagnosticEventKind.SkillRuntimeStarted:
                case BattleDiagnosticEventKind.SkillRuntimeEnded:
                case BattleDiagnosticEventKind.SkillFailure:
                    return BattleDebugConfigKind.Skill;
                case BattleDiagnosticEventKind.BuffAdded:
                case BattleDiagnosticEventKind.BuffRemoved:
                    return BattleDebugConfigKind.Buff;
                case BattleDiagnosticEventKind.ProjectileSpawned:
                case BattleDiagnosticEventKind.ProjectileHit:
                case BattleDiagnosticEventKind.ProjectileEnded:
                    return BattleDebugConfigKind.Projectile;
                case BattleDiagnosticEventKind.AreaSpawned:
                case BattleDiagnosticEventKind.AreaEnded:
                    return BattleDebugConfigKind.Area;
                case BattleDiagnosticEventKind.SummonSpawned:
                case BattleDiagnosticEventKind.SummonEnded:
                    return BattleDebugConfigKind.Summon;
                case BattleDiagnosticEventKind.EffectStarted:
                case BattleDiagnosticEventKind.EffectEnded:
                    return BattleDebugConfigKind.Effect;
                case BattleDiagnosticEventKind.TriggerAnalysis:
                    return BattleDebugConfigKind.TriggerPlan;
                default:
                    return BattleDebugConfigKind.Unknown;
            }
        }

        private static BattleDebugConfigKind MapTraceKind(MobaTraceKind kind)
        {
            switch (kind)
            {
                case MobaTraceKind.SkillCast:
                case MobaTraceKind.SkillEffect:
                case MobaTraceKind.SkillPhase:
                    return BattleDebugConfigKind.Skill;
                case MobaTraceKind.EffectExecution:
                    return BattleDebugConfigKind.Effect;
                case MobaTraceKind.BuffApply:
                case MobaTraceKind.BuffTick:
                case MobaTraceKind.BuffRemove:
                    return BattleDebugConfigKind.Buff;
                case MobaTraceKind.ProjectileLaunch:
                case MobaTraceKind.ProjectileHit:
                    return BattleDebugConfigKind.Projectile;
                case MobaTraceKind.AreaSpawn:
                case MobaTraceKind.AreaEnter:
                case MobaTraceKind.AreaExit:
                case MobaTraceKind.AreaExpire:
                case MobaTraceKind.AreaStay:
                    return BattleDebugConfigKind.Area;
                case MobaTraceKind.SummonSpawn:
                case MobaTraceKind.SummonDeath:
                    return BattleDebugConfigKind.Summon;
                case MobaTraceKind.PresentationPlay:
                case MobaTraceKind.PresentationStop:
                    return BattleDebugConfigKind.PresentationTemplate;
                default:
                    return BattleDebugConfigKind.Unknown;
            }
        }
    }
}
