using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaLobbyInputSink : IWorldInputSink, IWorldInitializable
    {
        private readonly MobaLobbyStateService _lobby;
        private readonly MobaEnterGameFlowService _enterGame;
        private readonly MobaPlayerActorMapService _playerActorMap;
        private readonly MobaEntityManager _entities;
        private readonly Entitas.IContexts _contexts;
        private SkillExecutor _skills;

        private IWorldResolver _services;

        private readonly Dictionary<int, Action<PlayerInputCommand>> _handlers;

        public MobaLobbyInputSink(MobaLobbyStateService lobby, MobaEnterGameFlowService enterGame, MobaPlayerActorMapService playerActorMap, MobaEntityManager entities, Entitas.IContexts contexts)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _enterGame = enterGame ?? throw new ArgumentNullException(nameof(enterGame));
            _playerActorMap = playerActorMap ?? throw new ArgumentNullException(nameof(playerActorMap));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

            _handlers = new Dictionary<int, Action<PlayerInputCommand>>
            {
                { (int)MobaOpCode.Ready, cmd => _lobby.SetReady(cmd.Player, true) },
                { (int)MobaOpCode.Unready, cmd => _lobby.SetReady(cmd.Player, false) },
                { (int)MobaOpCode.Move, HandleMove },
                { (int)MobaOpCode.SkillInput, HandleSkillInput },
            };
        }

        public void OnInit(IWorldResolver services)
        {
            if (_skills != null) return;
            if (services == null) return;

            _services = services;

            try
            {
                _skills = services.Resolve<SkillExecutor>();
                if (_skills == null)
                {
                    Log.Error("[MobaLobbyInputSink] SkillExecutor resolved as null.");
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaLobbyInputSink] Failed to resolve SkillExecutor.");

                if (services is IWorldServiceContainer c)
                {
                    Log.Error($"[MobaLobbyInputSink] Registered: SkillExecutor={c.IsRegistered(typeof(SkillExecutor))}, IFrameTime={c.IsRegistered(typeof(AbilityKit.Ability.FrameSync.IFrameTime))}, IUnitResolver={c.IsRegistered(typeof(AbilityKit.Ability.Share.ECS.IUnitResolver))}, IMobaSkillPipelineLibrary={c.IsRegistered(typeof(IMobaSkillPipelineLibrary))}, IWorldClock={c.IsRegistered(typeof(IWorldClock))}, IEventBus={c.IsRegistered(typeof(AbilityKit.Triggering.Eventing.IEventBus))}");

                    if (services.TryResolve(typeof(IWorldClock), out _) == false) Log.Error("[MobaLobbyInputSink] Resolve check failed: IWorldClock");
                    if (services.TryResolve(typeof(IFrameTime), out _) == false) Log.Error("[MobaLobbyInputSink] Resolve check failed: IFrameTime");
                    if (services.TryResolve(typeof(AbilityKit.Triggering.Eventing.IEventBus), out _) == false) Log.Error("[MobaLobbyInputSink] Resolve check failed: IEventBus");
                    if (services.TryResolve(typeof(AbilityKit.Ability.Share.ECS.IUnitResolver), out _) == false) Log.Error("[MobaLobbyInputSink] Resolve check failed: IUnitResolver");
                    if (services.TryResolve(typeof(MobaSkillLoadoutService), out _) == false) Log.Error("[MobaLobbyInputSink] Resolve check failed: MobaSkillLoadoutService");
                    if (services.TryResolve(typeof(MobaActorLookupService), out _) == false) Log.Error("[MobaLobbyInputSink] Resolve check failed: MobaActorLookupService");
                    if (services.TryResolve(typeof(IMobaSkillPipelineLibrary), out _) == false) Log.Error("[MobaLobbyInputSink] Resolve check failed: IMobaSkillPipelineLibrary");
                }

                try
                {
                    services.Resolve<IMobaSkillPipelineLibrary>();
                }
                catch (Exception libEx)
                {
                    Log.Exception(libEx, "[MobaLobbyInputSink] IMobaSkillPipelineLibrary resolve failed.");
                }

                try
                {
                    services.Resolve<AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MobaConfigDatabase>();
                }
                catch (Exception cfgEx)
                {
                    Log.Exception(cfgEx, "[MobaLobbyInputSink] MobaConfigDatabase resolve failed.");
                }

                try
                {
                    services.Resolve<MobaEffectExecutionService>();
                }
                catch (Exception effEx)
                {
                    Log.Exception(effEx, "[MobaLobbyInputSink] MobaEffectExecutionService resolve failed.");
                }

                try
                {
                    services.Resolve<AbilityKit.Triggering.Eventing.IEventBus>();
                }
                catch (Exception busEx)
                {
                    Log.Exception(busEx, "[MobaLobbyInputSink] IEventBus resolve failed.");
                }
            }
        }

        private void HandleMove(PlayerInputCommand cmd)
        {
            if (!_lobby.Started) return;
            if (!_playerActorMap.TryGetActorId(cmd.Player, out var actorId)) return;
            if (!TryGetEntity(actorId, out var entity) || entity == null) return;
            if (!entity.hasTransform) return;

            MobaMoveCodec.Deserialize(cmd.Payload, out var dx, out var dz);

            if (!entity.hasMoveInput)
            {
                entity.AddMoveInput(dx, dz);
            }
            else
            {
                entity.ReplaceMoveInput(dx, dz);
            }
        }

        private void HandleSkillInput(PlayerInputCommand cmd)
        {
            if (!_lobby.Started) return;
            if (!_playerActorMap.TryGetActorId(cmd.Player, out var actorId)) return;
            if (!TryGetEntity(actorId, out var entity) || entity == null) return;
            if (!entity.hasTransform) return;

            if (cmd.Payload == null || cmd.Payload.Length == 0) return;
            var evt = SkillInputCodec.Deserialize(cmd.Payload);

            _skills?.HandleInput(actorId, in evt);
        }

        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (inputs == null || inputs.Count == 0) return;

            for (int i = 0; i < inputs.Count; i++)
            {
                var cmd = inputs[i];

                if (_services != null && _services.TryResolve<IMobaLobbyInputHotfixRouter>(out var router) && router != null)
                {
                    try
                    {
                        if (router.TryHandle(_services, frame, cmd)) continue;
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "[MobaLobbyInputSink] Hotfix router TryHandle failed.");
                    }
                }

                if (_handlers.TryGetValue(cmd.OpCode, out var handler)) handler(cmd);
            }

            if (!_lobby.Started && _lobby.CanStartGame())
            {
                _enterGame.TryStartGame(((global::Contexts)_contexts).actor);
            }
        }

        private bool TryGetEntity(int actorId, out ActorEntity entity)
        {
            if (_entities != null && _entities.TryGetActorEntity(actorId, out entity) && entity != null)
            {
                return true;
            }

            entity = null;
            return false;
        }

        public void Dispose()
        {
            _handlers?.Clear();
        }
    }
}

