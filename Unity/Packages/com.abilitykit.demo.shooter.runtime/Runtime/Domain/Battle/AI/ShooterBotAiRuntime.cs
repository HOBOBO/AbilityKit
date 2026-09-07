#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Shooter;
using AbilityKit.World.Svelto;
using Svelto.DataStructures;
using Svelto.ECS;
using AbilityKit.HFSM;
using AbilityKit.HFSM.Extension;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal interface IShooterBotAiRuntime
    {
        int BotAiCount { get; }

        bool MountBotAi(int playerId, ShooterBotAiConfig config);

        bool UnmountBotAi(int playerId);

        void ClearBotAi();

        void Tick(float deltaTime);
    }

    internal sealed class ShooterBotAiRuntime : IShooterBotAiRuntime
    {
        private readonly ShooterBattleState _state;
        private readonly IShooterEntityManager _entities;
        private ShooterSpatialTargetIndex _targetIndex => _state.PlayerTargetIndex;
        private readonly Dictionary<int, ShooterBotAiAttachment> _attachments = new Dictionary<int, ShooterBotAiAttachment>();

        public ShooterBotAiRuntime(ShooterBattleState state, IShooterEntityManager entities)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        }

        public int BotAiCount => _attachments.Count;

        public bool MountBotAi(int playerId, ShooterBotAiConfig config)
        {
            if (playerId <= 0 || !_entities.HasPlayer(playerId))
            {
                return false;
            }

            var attachment = ShooterBotAiAttachment.Create(
                playerId,
                _state,
                _entities,
                _targetIndex,
                config ?? ShooterBotAiProfileCatalog.SimpleBattle);
            _attachments[playerId] = attachment;
            return true;
        }

        public bool UnmountBotAi(int playerId)
        {
            return _attachments.Remove(playerId);
        }

        public void ClearBotAi()
        {
            _attachments.Clear();
        }

        public void Tick(float deltaTime)
        {
            if (_attachments.Count == 0)
            {
                return;
            }

            _targetIndex.Rebuild(_entities.SveltoContext, _state.CurrentFrame);

            foreach (var kv in _attachments)
            {
                var attachment = kv.Value;
                if (!attachment.TryTick(deltaTime, out var command))
                {
                    _state.InputBuffer.RemoveLatestCommand(attachment.PlayerId);
                    continue;
                }

                _state.InputBuffer.SubmitCommand(_state.CurrentFrame, in command);
            }
        }
    }

    internal sealed class ShooterBotAiAttachment
    {
        private readonly ShooterBotAiController _controller;
        private readonly ShooterSpatialTargetIndex _targetIndex;

        private ShooterBotAiAttachment(int playerId, ShooterBotAiConfig config, ShooterSpatialTargetIndex targetIndex, ShooterBotAiController controller)
        {
            PlayerId = playerId;
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _targetIndex = targetIndex ?? throw new ArgumentNullException(nameof(targetIndex));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public int PlayerId { get; }

        public ShooterBotAiConfig Config { get; }

        public static ShooterBotAiAttachment Create(
            int playerId,
            ShooterBattleState state,
            IShooterEntityManager entities,
            ShooterSpatialTargetIndex targetIndex,
            ShooterBotAiConfig config)
        {
            var resolvedConfig = config ?? ShooterBotAiProfileCatalog.SimpleBattle;
            return new ShooterBotAiAttachment(
                playerId,
                resolvedConfig,
                targetIndex,
                ShooterBotAiController.Create(playerId, state, entities, targetIndex, resolvedConfig));
        }

        public bool TryTick(float deltaTime, out ShooterPlayerCommand command)
        {
            command = default;
            if (!_targetIndex.TryGetLivePlayer(PlayerId, out _))
            {
                return false;
            }

            _controller.Tick(deltaTime);
            command = _controller.Command;
            return true;
        }
    }
}
