using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Demo.Moba.Triggering.SummonSpawning
{
    public static class SpawnSummonSpecResolver
    {
        public static SpawnSummonSpec Resolve(ActionDef def, TriggerContext context)
        {
            if (def == null) return null;
            var args = def.Args;
            if (args == null) return null;

            var baseSpec = SpawnSummonSpecParser.FromDef(def);
            if (baseSpec == null) return null;

            if (!args.TryGetValue("templateId", out var tidObj) || tidObj == null)
            {
                return baseSpec;
            }

            var templateId = tidObj is int ti ? ti : tidObj is long tl ? (int)tl : 0;
            if (templateId <= 0) return baseSpec;

            var services = context != null ? context.Services : null;
            var db = services != null ? services.GetService(typeof(MobaConfigDatabase)) as MobaConfigDatabase : null;
            if (db == null)
            {
                Log.Warning("[Trigger] spawn_summon templateId provided but cannot resolve MobaConfigDatabase from DI");
                return baseSpec;
            }

            SpawnSummonActionTemplateDTO dto;
            if (!db.TryGetDto<SpawnSummonActionTemplateDTO>(templateId, out dto) || dto == null)
            {
                Log.Warning("[Trigger] spawn_summon templateId not found: " + templateId);
                return baseSpec;
            }

            var spec = SpawnSummonSpecParser.FromTemplate(dto);
            if (spec == null) return baseSpec;

            // Apply overrides from args
            SpawnSummonSpecParser.ApplyArgs(spec, args);
            return spec;
        }
    }
}
