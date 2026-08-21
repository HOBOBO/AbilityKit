using AbilityKit.Game.Battle;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public interface IShooterBattleRuntimePort :
        IBattleRuntimeStatusProvider,
        IShooterGameStartPort,
        IShooterInputPort,
        IShooterSimulationClock,
        IShooterSnapshotReadPort,
        IShooterStateHashProvider,
        IShooterPackedSnapshotPort,
        IShooterPureStateSnapshotPort,
        IShooterBotAiPort
    {
        /// <summary>
        /// Returns a snapshot backed by runtime-owned reusable arrays. Consume it before the next
        /// transient snapshot request on this runtime.
        /// </summary>
        ShooterStateSnapshotPayload GetSnapshotTransient();

        /// <summary>
        /// Returns only player state in a runtime-owned reusable array. Consume it before the next
        /// transient player or full snapshot request on this runtime.
        /// </summary>
        ShooterPlayerSnapshot[] GetPlayerSnapshotsTransient();

        /// <summary>
        /// Returns transform-only presentation samples in a runtime-owned reusable array.
        /// Consume the valid prefix before the next transient snapshot export.
        /// </summary>
        ShooterPureStateTransformSample[] ExportPureStateTransformSamplesTransient(out int count);

        bool TryGetPlayer(int playerId, out ShooterSveltoPlayerComponent player);

        void SetPlayer(in ShooterSveltoPlayerComponent player);
    }
}
