using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.LogicWorld;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 逻辑世界输入协调器：负责玩法侧上下文构建和命令处理器分发。
    /// </summary>
    [WorldService(typeof(IWorldInputSink))]
    [WorldService(typeof(IMobaInputCoordinator))]
    [WorldService(typeof(MobaInputCoordinator))]
    public sealed class MobaInputCoordinator : LogicWorldInputCoordinatorBase<MobaInputCommandContext>, IMobaInputCoordinator, IWorldInputSink
    {
        private readonly MobaLogicWorldRunGateService _phase;
        private readonly MobaPlayerActorMapService _playerActorMap;
        private readonly MobaEntityManager _entities;
        private readonly MobaInputCommandContractRegistry _contracts;
        private readonly MobaInputCommandHandlerRegistry _handlers;

        private SkillCastCoordinator _skills;

        public MobaInputCoordinator(MobaLogicWorldRunGateService phase, MobaPlayerActorMapService playerActorMap, MobaEntityManager entities, MobaInputCommandContractRegistry contracts)
        {
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));
            _playerActorMap = playerActorMap ?? throw new ArgumentNullException(nameof(playerActorMap));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
            _handlers = _contracts.HandlerRegistry;
        }

        protected override void OnServicesReady(IWorldResolver services)
        {
            if (services == null) return;

            _handlers.BindHandlers(services);
            if (_skills != null) return;

            ResolveSkillExecutor(services);
        }

        protected override MobaInputCommandContext CreateContext(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            return new MobaInputCommandContext(_phase, _playerActorMap, _entities, _skills, Services);
        }

        protected override bool Dispatch(MobaInputCommandContext context, FrameIndex frame, PlayerInputCommand command, out MobaInputCommandResult result)
        {
            if (!_contracts.TryValidateCommand(context, frame, command, out result))
            {
                return false;
            }

            return _handlers.TryHandle(context, frame, command, out result);
        }

        private void ResolveSkillExecutor(IWorldResolver services)
        {
            try
            {
                _skills = services.Resolve<SkillCastCoordinator>();
                if (_skills == null)
                {
                    MobaRuntimeLog.Error(MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.Validation, nameof(MobaInputCoordinator), "SkillCastCoordinator resolved as null.");
                }
            }
            catch (Exception ex)
            {
                MobaRuntimeLog.Exception(ex, MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.Exception, nameof(MobaInputCoordinator), "Failed to resolve SkillCastCoordinator.");
                MobaDependencyResolveDiagnostics.LogSkillExecutionDependencies(services, nameof(MobaInputCoordinator));
            }
        }

    }
}

