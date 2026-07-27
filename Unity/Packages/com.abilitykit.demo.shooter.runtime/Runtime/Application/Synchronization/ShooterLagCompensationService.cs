#nullable enable

using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Network.Runtime.LagCompensation;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public readonly struct ShooterLagCompensationTelemetry
    {
        public ShooterLagCompensationTelemetry(int capturedFrameCount, int oldestFrame, int latestFrame)
        {
            CapturedFrameCount = capturedFrameCount;
            OldestFrame = oldestFrame;
            LatestFrame = latestFrame;
        }

        public int CapturedFrameCount { get; }
        public int OldestFrame { get; }
        public int LatestFrame { get; }
    }

    public readonly struct ShooterLagCompensationEvaluation
    {
        public ShooterLagCompensationEvaluation(in ShooterLagCompensationShot shot, in LagCompensationHitResult result)
        {
            Shot = shot;
            Accepted = result.Accepted;
            Reason = result.Reason;
            RequestedFrame = result.RequestedFrame;
            EvaluatedFrame = result.EvaluatedFrame;
            HitEntityId = result.HitEntityId;
            Distance = result.Distance;
        }

        public ShooterLagCompensationShot Shot { get; }
        public bool Accepted { get; }
        public LagCompensationResultReason Reason { get; }
        public int RequestedFrame { get; }
        public int EvaluatedFrame { get; }
        public int HitEntityId { get; }
        public float Distance { get; }
    }

    public readonly struct ShooterLagCompensationShot
    {
        public ShooterLagCompensationShot(
            int shooterPlayerId,
            float originX,
            float originY,
            float directionX,
            float directionY,
            float maxDistance,
            int rewindFrame,
            int serverReceiveFrame,
            int targetLayerMask = ShooterLagCompensationService.PlayerLayerMask)
        {
            ShooterPlayerId = shooterPlayerId;
            OriginX = originX;
            OriginY = originY;
            DirectionX = directionX;
            DirectionY = directionY;
            MaxDistance = maxDistance;
            RewindFrame = rewindFrame;
            ServerReceiveFrame = serverReceiveFrame;
            TargetLayerMask = targetLayerMask;
        }

        public int ShooterPlayerId { get; }
        public float OriginX { get; }
        public float OriginY { get; }
        public float DirectionX { get; }
        public float DirectionY { get; }
        public float MaxDistance { get; }
        public int RewindFrame { get; }
        public int ServerReceiveFrame { get; }
        public int TargetLayerMask { get; }
    }

    public sealed class ShooterLagCompensationService
    {
        public const int PlayerLayerMask = 1;

        private readonly ServerRewindLagCompensationService _lagCompensation;
        private readonly IShooterBattleRules _rules;
        private LagCompensatedEntitySnapshot[] _entityBuffer = Array.Empty<LagCompensatedEntitySnapshot>();
        private ShooterLagCompensationEvaluation? _lastEvaluation;

        public ShooterLagCompensationService()
            : this(ServerRewindLagCompensationConfig.Default, ShooterBattleRules.Default)
        {
        }

        public ShooterLagCompensationService(ServerRewindLagCompensationConfig config)
            : this(config, ShooterBattleRules.Default)
        {
        }

        public ShooterLagCompensationService(ServerRewindLagCompensationConfig config, IShooterBattleRules rules)
            : this(new ServerRewindLagCompensationService(config), rules)
        {
        }

        public ShooterLagCompensationService(ServerRewindLagCompensationService lagCompensation)
            : this(lagCompensation, ShooterBattleRules.Default)
        {
        }

        public ShooterLagCompensationService(ServerRewindLagCompensationService lagCompensation, IShooterBattleRules rules)
        {
            _lagCompensation = lagCompensation ?? throw new ArgumentNullException(nameof(lagCompensation));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public int CapturedFrameCount => _lagCompensation.CapturedFrameCount;
        public int OldestFrame => _lagCompensation.OldestFrame;
        public int LatestFrame => _lagCompensation.LatestFrame;
        public ShooterLagCompensationTelemetry Telemetry => new ShooterLagCompensationTelemetry(
            CapturedFrameCount,
            OldestFrame,
            LatestFrame);
        public ShooterLagCompensationEvaluation? LastEvaluation => _lastEvaluation;

        public void RecordFrame(IShooterBattleRuntimePort runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));

            RecordFrame(runtime.CurrentFrame, runtime.GetPlayerSnapshotsTransient());
        }

        public void RecordFrame(in ShooterStateSnapshotPayload snapshot)
        {
            RecordFrame(snapshot.Frame, snapshot.Players ?? Array.Empty<ShooterPlayerSnapshot>());
        }

        private void RecordFrame(int frame, ShooterPlayerSnapshot[] players)
        {
            if (_entityBuffer.Length != players.Length)
            {
                _entityBuffer = players.Length == 0
                    ? Array.Empty<LagCompensatedEntitySnapshot>()
                    : new LagCompensatedEntitySnapshot[players.Length];
            }

            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                var position = new Vec3(player.X, player.Y, 0f);
                _entityBuffer[i] = new LagCompensatedEntitySnapshot(
                    player.PlayerId,
                    in position,
                    _rules.HitRadius,
                    PlayerLayerMask,
                    player.Alive);
            }

            // The rewind service copies this buffer into its owned history frame.
            _lagCompensation.RecordFrame(frame, _entityBuffer);
        }

        public bool TryEvaluateShot(in ShooterLagCompensationShot shot, out LagCompensationHitResult result)
        {
            var origin = new Vec3(shot.OriginX, shot.OriginY, 0f);
            var direction = new Vec3(shot.DirectionX, shot.DirectionY, 0f);
            var query = new LagCompensationQuery(
                shot.ShooterPlayerId,
                in origin,
                in direction,
                shot.MaxDistance,
                shot.TargetLayerMask,
                shot.RewindFrame,
                shot.ServerReceiveFrame);

            var accepted = _lagCompensation.TryEvaluateHit(in query, out result);
            _lastEvaluation = new ShooterLagCompensationEvaluation(in shot, in result);
            return accepted;
        }

        public void Clear()
        {
            _lagCompensation.Clear();
            _lastEvaluation = null;
        }
    }
}
