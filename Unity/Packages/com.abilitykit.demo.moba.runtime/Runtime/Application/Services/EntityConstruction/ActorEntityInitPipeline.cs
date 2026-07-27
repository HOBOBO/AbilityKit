using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Protocol.Moba;
using MO = AbilityKit.Demo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    /// <summary>
    /// Actor 初始化编排服务：负责把读表结果和进入战斗 loadout 分发给属性、技能初始化器。
    /// </summary>
    [WorldService(typeof(ActorEntityInitPipeline), WorldLifetime.Scoped)]
    public sealed class ActorEntityInitPipeline : IService
    {
        private readonly IWorldResolver _services;
        private readonly MobaActorInitDiagnostics _diagnostics = new MobaActorInitDiagnostics();
        private readonly MobaActorAttributeInitializer _attributes = new MobaActorAttributeInitializer();
        private readonly MobaActorSkillLoadoutInitializer _skills = new MobaActorSkillLoadoutInitializer();
        private MobaConfigDatabase _config;

        public ActorEntityInitPipeline(IWorldResolver services)
        {
            _services = services;
            TryResolveConfig();
        }

        public void InitializeFromAttributeTemplate(global::ActorEntity entity, int attributeTemplateId)
        {
            if (entity == null) return;

            _attributes.EnsureContainers(entity);

            if (!EnsureConfig()) return;
            if (attributeTemplateId <= 0) return;

            var template = ResolveAttributeTemplate(attributeTemplateId);
            _attributes.ApplyTemplate(entity, template);
        }

        public void InitializeFromLoadout(global::ActorEntity entity, in MobaPlayerLoadout loadout)
        {
            if (!TryInitializeFromLoadout(entity, in loadout, out var error) && !string.IsNullOrEmpty(error))
            {
                _diagnostics.LogMissingAttributeTemplate(
                    loadout.AttributeTemplateId,
                    $"[ActorEntityInitPipeline] Loadout initialization failed. heroId={loadout.HeroId} error={error}");
            }
        }

        public bool TryInitializeFromLoadout(
            global::ActorEntity entity,
            in MobaPlayerLoadout loadout,
            out string error)
        {
            error = null;
            if (entity == null)
            {
                error = "actor entity is required";
                return false;
            }

            if (!EnsureConfig())
            {
                error = "config database is unavailable";
                return false;
            }

            if (!MobaResolvedHeroLoadoutResolver.TryResolve(
                    _config,
                    loadout.HeroId,
                    out var resolved,
                    out error))
            {
                return false;
            }

            MobaPreparedActorAttributes preparedAttributes;
            try
            {
                preparedAttributes = _attributes.Prepare(resolved.AttributeTemplate);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (!_skills.TryPrepare(in resolved, out var preparedSkills, out error))
            {
                return false;
            }

            var modelId = resolved.Character.ModelId;
            if (modelId > 0)
            {
                if (entity.hasModelId) entity.ReplaceModelId(modelId);
                else entity.AddModelId(modelId);
            }

            _attributes.Apply(entity, in preparedAttributes);
            _skills.Apply(entity, in preparedSkills);
            return true;
        }

        private bool EnsureConfig()
        {
            if (_config != null) return true;
            if (TryResolveConfig()) return true;

            _diagnostics.LogMissingConfig("[ActorEntityInitPipeline] MobaConfigDatabase is not available. Ensure it is registered when creating the world.");
            return false;
        }

        private bool TryResolveConfig()
        {
            if (_config != null) return true;
            if (_services == null) return false;

            if (_services.TryResolve<MobaConfigDatabase>(out var config) && config != null)
            {
                _config = config;
                return true;
            }

            try
            {
                _config = _services.Resolve<MobaConfigDatabase>();
                return _config != null;
            }
            catch (Exception ex)
            {
                _diagnostics.LogConfigResolveException(ex);
                return false;
            }
        }

        private MO.BattleAttributeTemplateMO ResolveAttributeTemplate(int attributeTemplateId)
        {
            try
            {
                return _config.GetAttributeTemplate(attributeTemplateId);
            }
            catch (Exception ex)
            {
                _diagnostics.LogMissingAttributeTemplate(attributeTemplateId, ex);
                return null;
            }
        }

        public void Dispose()
        {
        }
    }
}
