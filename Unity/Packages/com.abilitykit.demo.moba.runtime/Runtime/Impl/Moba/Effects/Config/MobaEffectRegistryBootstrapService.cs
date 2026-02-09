using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Triggering.Json;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Effects.Core;
using Newtonsoft.Json;

namespace AbilityKit.Ability.Impl.Moba.Effects.Config
{
    public sealed class MobaEffectRegistryBootstrapService : IService, IWorldInitializable
    {
        private readonly EffectRegistry _registry;
        private readonly ITextLoader _texts;

        public MobaEffectRegistryBootstrapService(EffectRegistry registry, ITextLoader texts)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _texts = texts;
        }

        public void OnInit(IWorldResolver services)
        {
            if (_texts == null) return;

            if (!_texts.TryLoad(MobaEffectConfigPaths.DefaultResourcesId, out var text) || string.IsNullOrEmpty(text))
            {
                return;
            }

            MobaEffectDefCollection defs;
            try
            {
                defs = JsonConvert.DeserializeObject<MobaEffectDefCollection>(text);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaEffectRegistryBootstrapService] Deserialize failed");
                throw;
            }

            if (defs?.Effects == null) return;

            for (int i = 0; i < defs.Effects.Length; i++)
            {
                var dto = defs.Effects[i];
                if (!EffectDefMapper.TryMap(dto, out var def) || def == null) continue;

                var inst = new EffectInstance(instanceId: def.EffectId, def: def, scope: def.DefaultScope);
                _registry.Register(inst);
            }
        }

        public void Dispose()
        {
        }
    }
}
