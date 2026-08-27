using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Aoi;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.Runtime
{
    /// <summary>
    /// Optional server-side timing hook. The callback is invoked for each
    /// battle system and selected RVO substages, and is never installed by
    /// the normal Unity client path.
    /// </summary>
    public interface IShooterBattlePerformancePort
    {
        Action<string, double>? StageTimingSink { get; set; }
    }

    public interface IShooterGameStartPort
    {
        bool IsStarted { get; }

        ShooterBattleMatchState MatchState { get; }

        ShooterMatchResultSnapshot MatchResult { get; }

        ShooterStartGamePayload StartSpec { get; }

        bool StartGame(in ShooterStartGamePayload spec);
    }

    public interface IShooterInputPort
    {
        int SubmitInput(int frame, ShooterPlayerCommand[] commands);

        int SubmitInput(int frame, IReadOnlyList<ShooterPlayerCommand> commands);
    }

    public interface IShooterSimulationClock
    {
        int CurrentFrame { get; }

        bool Tick(float deltaTime);
    }

    public interface IShooterSnapshotReadPort
    {
        ShooterStateSnapshotPayload GetSnapshot();
    }

    public interface IShooterStateHashProvider
    {
        uint ComputeStateHash();
    }

    public interface IShooterPackedSnapshotPort
    {
        ShooterPackedSnapshotPayload ExportPackedSnapshot(ulong worldId, bool isFullSnapshot = true, bool authorityOverride = false);

        bool ImportPackedSnapshot(in ShooterPackedSnapshotPayload snapshot);

        byte[] ExportPackedSnapshotBytes(ulong worldId, bool isFullSnapshot = true, bool authorityOverride = false);

        bool ImportPackedSnapshotBytes(byte[] payload);
    }

    public interface IShooterPureStateSnapshotPort
    {
        ShooterPureStateSnapshotPayload ExportPureStateSnapshot(
            ulong worldId,
            bool isFullBaseline = true,
            ShooterPureStateSyncSettings? settings = null,
            int baselineFrame = 0,
            uint baselineHash = 0,
            ShooterPureStateInterestScope? interestScope = null,
            AoiInterestSet? aoiInterestSet = null,
            bool computeStateHash = true);

        ShooterPureStateSnapshotPayload ExportPureStateSnapshotTransient(
            ulong worldId,
            bool isFullBaseline = true,
            ShooterPureStateSyncSettings? settings = null,
            int baselineFrame = 0,
            uint baselineHash = 0,
            ShooterPureStateInterestScope? interestScope = null,
            AoiInterestSet? aoiInterestSet = null,
            bool computeStateHash = true);
    }

    public readonly struct ShooterPureStateInterestScope
    {
        public ShooterPureStateInterestScope(int observerPlayerId, float centerX, float centerY, float radius, int maxEntities = 0)
            : this(observerPlayerId, centerX, centerY, radius, radius, maxEntities)
        {
        }

        public ShooterPureStateInterestScope(int observerPlayerId, float centerX, float centerY, float visibleRadius, float boundaryRadius, int maxEntities = 0)
        {
            ObserverPlayerId = observerPlayerId;
            CenterX = centerX;
            CenterY = centerY;
            Radius = visibleRadius;
            BoundaryRadius = boundaryRadius <= 0f ? visibleRadius : boundaryRadius;
            MaxEntities = maxEntities;
        }

        public int ObserverPlayerId { get; }

        public float CenterX { get; }

        public float CenterY { get; }

        public float Radius { get; }

        public float VisibleRadius => Radius;

        public float BoundaryRadius { get; }

        public int MaxEntities { get; }

        public bool HasRadius => Radius > 0f;

        public AoiInterestScope ToAoiScope()
        {
            return new AoiInterestScope(CenterX, CenterY, VisibleRadius, BoundaryRadius, MaxEntities);
        }
    }
}
