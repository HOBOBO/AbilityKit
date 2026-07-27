#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    internal sealed class ShooterClientPredictionRuntimeAdapter : IWorld, IWorldInputSink
    {
        private static readonly IWorldResolver EmptyResolver = new EmptyWorldResolver();
        private readonly IShooterBattleRuntimePort _runtime;
        private readonly List<ShooterPlayerCommand> _commandBuffer = new List<ShooterPlayerCommand>(8);

        public ShooterClientPredictionRuntimeAdapter(IShooterBattleRuntimePort runtime, string worldId = "shooter-client-prediction")
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Id = new WorldId(worldId);
        }

        public WorldId Id { get; }
        public string WorldType => "ShooterClientPrediction";
        public IWorldResolver Services => EmptyResolver;

        public void Initialize()
        {
        }

        public void Tick(float deltaTime)
        {
            _runtime.Tick(deltaTime);
        }

        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (inputs == null || inputs.Count == 0)
            {
                return;
            }

            _commandBuffer.Clear();
            for (int i = 0; i < inputs.Count; i++)
            {
                var decoded = ShooterInputCodec.Deserialize(inputs[i].Payload);
                for (int j = 0; j < decoded.Length; j++)
                {
                    _commandBuffer.Add(decoded[j]);
                }
            }

            if (_commandBuffer.Count == 0)
            {
                return;
            }

            _runtime.SubmitInput(frame.Value, _commandBuffer);
        }

        public void Dispose()
        {
        }

        public static PlayerInputCommand CreateInputCommand(FrameIndex frame, in ShooterPlayerCommand command)
        {
            return new PlayerInputCommand(
                frame,
                new PlayerId(command.PlayerId.ToString()),
                ShooterClientPredictionInputOpCodes.PlayerCommand,
                ShooterInputCodec.Serialize(new[] { command }));
        }

        public static PlayerInputCommand[] CreateInputCommands(FrameIndex frame, ShooterPlayerCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
            {
                return Array.Empty<PlayerInputCommand>();
            }

            var inputs = new PlayerInputCommand[commands.Length];
            for (int i = 0; i < commands.Length; i++)
            {
                inputs[i] = CreateInputCommand(frame, in commands[i]);
            }

            return inputs;
        }

        public WorldStateHash ComputeHash(FrameIndex frame)
        {
            return new WorldStateHash(_runtime.ComputeStateHash());
        }

        private sealed class EmptyWorldResolver : IWorldResolver
        {
            public object Resolve(Type serviceType)
            {
                throw new InvalidOperationException($"Service not registered: {serviceType?.FullName ?? "<null>"}.");
            }

            public T Resolve<T>()
            {
                throw new InvalidOperationException($"Service not registered: {typeof(T).FullName}.");
            }

            public bool TryResolve(Type serviceType, out object instance)
            {
                instance = null!;
                return false;
            }

            public bool TryResolve<T>(out T instance)
            {
                instance = default!;
                return false;
            }
        }
    }

    internal static class ShooterClientPredictionInputOpCodes
    {
        public const int PlayerCommand = 1;
    }
}
