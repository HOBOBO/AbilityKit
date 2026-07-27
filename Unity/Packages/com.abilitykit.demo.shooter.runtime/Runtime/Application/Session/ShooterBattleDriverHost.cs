#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Coordinator;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.Runtime
{
    /// <summary>
    /// Shooter logic-world driver bridge for AbilityKit.Coordinator.
    /// It centralizes frame advancement, input submission and snapshot-state projection for both client-side authority comparison and server authority runtime.
    /// </summary>
    public sealed class ShooterBattleDriverHost : ILogicWorldDriverBridge
    {
        private readonly IShooterBattleRuntimePort _runtime;
        private readonly List<ShooterPlayerCommand> _commandBuffer = new List<ShooterPlayerCommand>(16);
        private readonly ShooterPlayerCommand[] _singleCommandBuffer = new ShooterPlayerCommand[1];
        private readonly List<SnapshotEntityState> _stateBuffer = new List<SnapshotEntityState>(64);
        private double _logicTimeSeconds;
        private bool _isRunning;

        public ShooterBattleDriverHost(IShooterBattleRuntimePort runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public int CurrentFrame => _runtime.CurrentFrame;

        public double LogicTimeSeconds => _logicTimeSeconds;

        public bool IsRunning => _isRunning;

        public IShooterBattleRuntimePort Runtime => _runtime;

        public void Start()
        {
            _isRunning = true;
            _logicTimeSeconds = 0d;
        }

        public void Stop()
        {
            _isRunning = false;
        }

        public void SubmitInputs(PlayerInput[] inputs)
        {
            if (!_isRunning || inputs == null || inputs.Length == 0)
            {
                return;
            }

            _commandBuffer.Clear();
            var targetFrame = CurrentFrame + 1;
            for (int i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                if (TryConvertInput(in input, out var command))
                {
                    _commandBuffer.Add(command);
                    if (input.Frame > 0)
                    {
                        targetFrame = input.Frame;
                    }
                }
            }

            SubmitCommands(targetFrame, _commandBuffer);
        }

        public int SubmitCommands(int frame, IReadOnlyList<ShooterPlayerCommand> commands)
        {
            if (!_isRunning || commands == null || commands.Count == 0)
            {
                return 0;
            }

            return _runtime.SubmitInput(frame, commands);
        }

        public int SubmitCommand(int frame, in ShooterPlayerCommand command)
        {
            if (!_isRunning)
            {
                return 0;
            }

            _singleCommandBuffer[0] = command;
            return _runtime.SubmitInput(frame, _singleCommandBuffer);
        }

        public void AdvanceFrame(float deltaTime)
        {
            if (!_isRunning)
            {
                return;
            }

            if (_runtime.Tick(deltaTime))
            {
                _logicTimeSeconds += Math.Max(0f, deltaTime);
            }
        }

        public SnapshotEntityState[] GetAllEntityStates()
        {
            _stateBuffer.Clear();
            var players = _runtime.GetPlayerSnapshotsTransient();
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                var state = new EntityState(player.PlayerId)
                {
                    X = player.X,
                    Y = 0f,
                    Z = player.Y,
                    Rotation = MathF.Atan2(player.AimY, player.AimX),
                    VelocityX = 0f,
                    VelocityZ = 0f,
                    Hp = player.Hp,
                    HpMax = Math.Max(0, player.Hp),
                    TeamId = 0,
                    IsDead = !player.Alive
                };
                _stateBuffer.Add(state.ToSnapshotEntityState());
            }

            return _stateBuffer.ToArray();
        }

        private static bool TryConvertInput(in PlayerInput input, out ShooterPlayerCommand command)
        {
            if (input.OpCode == ShooterOpCodes.Input.PlayerCommand)
            {
                var commands = ShooterInputCodec.Deserialize(input.Payload ?? Array.Empty<byte>());
                if (commands.Length > 0)
                {
                    command = commands[0];
                    return true;
                }
            }

            command = new ShooterPlayerCommand(
                input.PlayerId,
                moveX: 0f,
                moveY: 0f,
                aimX: 1f,
                aimY: 0f,
                fire: false);
            return input.PlayerId > 0;
        }
    }
}
