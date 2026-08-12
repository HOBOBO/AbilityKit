using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Behavior;
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
        private readonly MobaActorInitializerProvider _initializers;
        private MobaConfigDatabase _config;

        public ActorEntityInitPipeline(IWorldResolver services)
            : this(services, null)
        {
        }

        internal ActorEntityInitPipeline(
            IWorldResolver services,
            IEnumerable<IMobaActorInitializerStep> extensionInitializers)
        {
            _services = services;
            _initializers = new MobaActorInitializerProvider(
                CreateCoreInitializers(),
                extensionInitializers);
            TryResolveConfig();
        }

        public MobaActorInitializerProvider Initializers => _initializers;

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

            var context = new MobaActorInitializationContext(entity, in loadout, in resolved);
            return _initializers.TryInitialize(in context, out error);
        }

        private IEnumerable<IMobaActorInitializerStep> CreateCoreInitializers()
        {
            return new IMobaActorInitializerStep[]
            {
                new ModelInitializer(),
                new AttributeInitializer(_attributes),
                new SkillInitializer(_skills),
                new BrainInitializer(this)
            };
        }

        private void ApplyConfiguredBrain(global::ActorEntity entity, in MobaPlayerLoadout loadout)
        {
            if (entity == null) return;

            if (_services != null && _services.TryResolve<MobaBrainService>(out var brains) && brains != null)
            {
                if (loadout.BrainId > 0 && loadout.EnableBrainOnSpawn)
                {
                    brains.ActivateBrain(entity, loadout.BrainId, MobaBrainSourceKinds.BattleTemplate, loadout.SpawnIndex);
                }
                else if (entity.hasActorBrain && entity.actorBrain.SourceKind == MobaBrainSourceKinds.BattleTemplate)
                {
                    brains.DeactivateBrain(entity);
                }
                return;
            }

            if (loadout.BrainId > 0 && loadout.EnableBrainOnSpawn)
            {
                if (entity.hasActorBrain)
                    entity.ReplaceActorBrain(loadout.BrainId, entity.hasActorId ? entity.actorId.Value : 0,
                        MobaBrainSourceKinds.BattleTemplate, loadout.SpawnIndex, 0L);
                else
                    entity.AddActorBrain(loadout.BrainId, entity.hasActorId ? entity.actorId.Value : 0,
                        MobaBrainSourceKinds.BattleTemplate, loadout.SpawnIndex, 0L);
            }
            else if (entity.hasActorBrain && entity.actorBrain.SourceKind == MobaBrainSourceKinds.BattleTemplate)
            {
                entity.RemoveActorBrain();
                if (entity.hasMoveInput) entity.ReplaceMoveInput(0f, 0f);
            }
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

        private sealed class ModelInitializer : IMobaActorInitializerStep
        {
            public string Id => "core.model";
            public int Order => 100;

            public bool TryPrepare(in MobaActorInitializationContext context, out object preparedState, out string error)
            {
                preparedState = context.ResolvedLoadout.Character.ModelId;
                error = null;
                return true;
            }

            public void Apply(in MobaActorInitializationContext context, object preparedState)
            {
                var modelId = (int)preparedState;
                if (modelId <= 0) return;
                if (context.Entity.hasModelId) context.Entity.ReplaceModelId(modelId);
                else context.Entity.AddModelId(modelId);
            }
        }

        private sealed class AttributeInitializer : IMobaActorInitializerStep
        {
            private readonly MobaActorAttributeInitializer _initializer;

            public AttributeInitializer(MobaActorAttributeInitializer initializer)
            {
                _initializer = initializer;
            }

            public string Id => "core.attributes";
            public int Order => 200;

            public bool TryPrepare(in MobaActorInitializationContext context, out object preparedState, out string error)
            {
                preparedState = _initializer.Prepare(context.ResolvedLoadout.AttributeTemplate);
                error = null;
                return true;
            }

            public void Apply(in MobaActorInitializationContext context, object preparedState)
            {
                var prepared = (MobaPreparedActorAttributes)preparedState;
                _initializer.Apply(context.Entity, in prepared);
            }
        }

        private sealed class SkillInitializer : IMobaActorInitializerStep
        {
            private readonly MobaActorSkillLoadoutInitializer _initializer;

            public SkillInitializer(MobaActorSkillLoadoutInitializer initializer)
            {
                _initializer = initializer;
            }

            public string Id => "core.skills";
            public int Order => 300;

            public bool TryPrepare(in MobaActorInitializationContext context, out object preparedState, out string error)
            {
                if (_initializer.TryPrepare(in context.ResolvedLoadout, out var prepared, out error))
                {
                    preparedState = prepared;
                    return true;
                }

                preparedState = null;
                return false;
            }

            public void Apply(in MobaActorInitializationContext context, object preparedState)
            {
                var prepared = (MobaPreparedActorSkillLoadout)preparedState;
                _initializer.Apply(context.Entity, in prepared);
            }
        }

        private sealed class BrainInitializer : IMobaActorInitializerStep
        {
            private readonly ActorEntityInitPipeline _pipeline;

            public BrainInitializer(ActorEntityInitPipeline pipeline)
            {
                _pipeline = pipeline;
            }

            public string Id => "core.brain";
            public int Order => 400;

            public bool TryPrepare(in MobaActorInitializationContext context, out object preparedState, out string error)
            {
                preparedState = null;
                error = null;
                return true;
            }

            public void Apply(in MobaActorInitializationContext context, object preparedState)
            {
                _pipeline.ApplyConfiguredBrain(context.Entity, in context.Loadout);
            }
        }
    }
}
