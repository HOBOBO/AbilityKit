using System;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Core.Logging;
using AbilityKit.Protocol.Moba;
using AbilityKit.Ability.Host.Extensions.Moba.Room;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartGame
{
    public enum MobaServerGameLifecyclePhase
    {
        Created = 0,
        StartRequested = 1,
        Starting = 2,
        Running = 3,
        Failed = 4,
        Stopped = 5,
    }

    public enum MobaServerGameLifecycleErrorCode
    {
        None = 0,
        AlreadyStarted = 1,
        RoomNotReady = 2,
        GameStartSpecNotBuilt = 3,
        RuntimePortNotResolved = 4,
        RuntimeStartRejected = 5,
        Exception = 6,
    }

    public interface IMobaServerGameLifecycle : IService
    {
        MobaServerGameLifecyclePhase Phase { get; }
        MobaServerGameLifecycleErrorCode LastErrorCode { get; }
        string LastMessage { get; }
        bool CanRequestStart { get; }
        void MarkStartRequested(string message = null);
        void MarkStarting(string message = null);
        void MarkRunning(string message = null);
        void MarkFailed(MobaServerGameLifecycleErrorCode errorCode, string message = null);
        void MarkStopped(string message = null);
    }

    [WorldService(typeof(IMobaServerGameLifecycle), WorldLifetime.Scoped)]
    public sealed class MobaServerGameLifecycle : IMobaServerGameLifecycle
    {
        public MobaServerGameLifecyclePhase Phase { get; private set; }
        public MobaServerGameLifecycleErrorCode LastErrorCode { get; private set; }
        public string LastMessage { get; private set; }
        public bool CanRequestStart => Phase == MobaServerGameLifecyclePhase.Created || Phase == MobaServerGameLifecyclePhase.Stopped;

        public void MarkStartRequested(string message = null)
        {
            Set(MobaServerGameLifecyclePhase.StartRequested, MobaServerGameLifecycleErrorCode.None, message);
        }

        public void MarkStarting(string message = null)
        {
            Set(MobaServerGameLifecyclePhase.Starting, MobaServerGameLifecycleErrorCode.None, message);
        }

        public void MarkRunning(string message = null)
        {
            Set(MobaServerGameLifecyclePhase.Running, MobaServerGameLifecycleErrorCode.None, message);
        }

        public void MarkFailed(MobaServerGameLifecycleErrorCode errorCode, string message = null)
        {
            Set(MobaServerGameLifecyclePhase.Failed, errorCode == MobaServerGameLifecycleErrorCode.None ? MobaServerGameLifecycleErrorCode.Exception : errorCode, message);
        }

        public void MarkStopped(string message = null)
        {
            Set(MobaServerGameLifecyclePhase.Stopped, MobaServerGameLifecycleErrorCode.None, message);
        }

        public void Dispose()
        {
        }

        public override string ToString()
        {
            return $"phase={Phase}, error={LastErrorCode}, message={LastMessage}";
        }

        private void Set(MobaServerGameLifecyclePhase phase, MobaServerGameLifecycleErrorCode errorCode, string message)
        {
            Phase = phase;
            LastErrorCode = errorCode;
            LastMessage = message ?? string.Empty;
            Log.Info($"[MobaServerGameLifecycle] {this}");
        }
    }

    public interface IMobaGameStartOrchestrator : IService
    {
        bool TryStartGame(IWorld world);
    }

    [WorldService(typeof(IMobaGameStartOrchestrator), WorldLifetime.Scoped)]
    public sealed class MobaGameStartOrchestrator : IMobaGameStartOrchestrator
    {
        private readonly IMobaRoomOrchestrator _room;
        private readonly IMobaServerGameLifecycle _lifecycle;

        private bool _started;

        public MobaGameStartOrchestrator(IMobaRoomOrchestrator room, IMobaServerGameLifecycle lifecycle)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        }

        public bool TryStartGame(IWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

            if (_started || !_lifecycle.CanRequestStart)
            {
                return Fail(MobaServerGameLifecycleErrorCode.AlreadyStarted, "game already started or lifecycle is not startable. " + _lifecycle);
            }

            _lifecycle.MarkStartRequested("room requested game start");

            if (!CanStartGame(_room))
            {
                return Fail(MobaServerGameLifecycleErrorCode.RoomNotReady, "room state cannot start game");
            }

            if (!TryBuildGameStartSpec(_room, out var spec))
            {
                return Fail(MobaServerGameLifecycleErrorCode.GameStartSpecNotBuilt, "game start spec not found or not built");
            }

            if (world.Services?.TryResolve<IMobaBattleRuntimePort>(out var runtime) != true || runtime == null)
            {
                return Fail(MobaServerGameLifecycleErrorCode.RuntimePortNotResolved, "IMobaBattleRuntimePort not found");
            }

            _lifecycle.MarkStarting("battle runtime start requested");
            var result = runtime.TryStartGame(in spec);
            if (!result.Succeeded)
            {
                return Fail(MobaServerGameLifecycleErrorCode.RuntimeStartRejected, result.ToString());
            }

            _started = true;
            _lifecycle.MarkRunning("battle runtime started");
            return true;
        }

        private static bool CanStartGame(IMobaRoomOrchestrator room)
        {
            if (room == null) return false;
            return room.State != null && room.State.CanStart();
        }

        private static bool TryBuildGameStartSpec(IMobaRoomOrchestrator room, out MobaGameStartSpec spec)
        {
            spec = default;
            if (room?.State == null) return false;

            // Pick any joined player as localPlayerId for spec building.
            foreach (var kv in room.State.Players)
            {
                var localPlayerId = new PlayerId(kv.Key);
                return room.TryBuildGameStartSpec(localPlayerId, out spec);
            }

            return false;
        }

        public void Dispose()
        {
        }

        private bool Fail(MobaServerGameLifecycleErrorCode errorCode, string message)
        {
            _lifecycle.MarkFailed(errorCode, message);
            Log.Warning($"[MobaGameStartOrchestrator] TryStartGame failed. {_lifecycle}");
            return false;
        }
    }
}

