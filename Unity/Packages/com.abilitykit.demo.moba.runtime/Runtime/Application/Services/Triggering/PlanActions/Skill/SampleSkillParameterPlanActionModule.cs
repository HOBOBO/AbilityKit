using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct SampleSkillParameterArgs
    {
        public readonly int SkillId;
        public readonly string Parameter;
        public readonly BlackboardWriteTarget Result;
        public SampleSkillParameterArgs(int skillId, string parameter, BlackboardWriteTarget result)
        { SkillId = skillId; Parameter = parameter; Result = result; }
    }

    public sealed class SampleSkillParameterSchema : MobaPlanActionSchemaBase<SampleSkillParameterArgs>
    {
        public static readonly SampleSkillParameterSchema Instance = new SampleSkillParameterSchema();
        protected override string ActionName => "sample_skill_parameter";
        public override SampleSkillParameterArgs ParseArgs(Dictionary<string, ActionArgValue> args, ExecCtx<IWorldResolver> ctx)
        {
            TryReadBlackboardTarget(args, out var result, "result");
            return new SampleSkillParameterArgs(ReadInt(args, ctx, 0, "skill_id"),
                MobaEffectMagnitudeSchemaReader.ReadString(args, in ctx, "parameter"), result);
        }
        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
            => RequireAny(args, "skill_id", out error, "skill_id") &&
               RequireAny(args, "parameter", out error, "parameter") && RequireAny(args, "result", out error, "result");
    }

    [PlanActionModule(order: 10)]
    public sealed class SampleSkillParameterPlanActionModule : MobaPlanActionModuleBase<SampleSkillParameterArgs, SampleSkillParameterPlanActionModule>
    {
        protected override IActionSchema<SampleSkillParameterArgs, IWorldResolver> Schema => SampleSkillParameterSchema.Instance;
        protected override void Execute(object triggerArgs, SampleSkillParameterArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var input) || !input.HasCasterActor ||
                !ctx.Context.TryResolve<MobaActorLookupService>(out var actors) ||
                !actors.TryGetActorEntity(input.CasterActorId, out var actor) ||
                !ctx.Context.TryResolve<MobaConfigDatabase>(out var configs) ||
                !configs.TryGetSkill(args.SkillId, out var skill) ||
                !configs.TryGetSkillLevelTable(skill.LevelTableId, out var table))
            { LogRejected(ctx, "sample_skill_parameter requires a caster, skill and level table."); return; }
            var level = 0;
            if (actor.hasSkillLoadout && actor.skillLoadout.ActiveSkills != null)
                foreach (var entry in actor.skillLoadout.ActiveSkills)
                    if (entry != null && entry.SkillId == args.SkillId) { level = entry.Level; break; }
            var value = 0f;
            if (level > 0 && (!table.TryCreateParameterSnapshot(level, args.SkillId, out var parameters, out var error) ||
                !parameters.TryGetValue(args.Parameter, out value)))
            { LogRejected(ctx, "Current skill parameter is unavailable: " + args.Parameter); return; }
            if (!MobaPlanActionOutput.TryWrite(in ctx, in args.Result, value, out var writeError))
                LogRejected(ctx, writeError);
        }
    }
}
