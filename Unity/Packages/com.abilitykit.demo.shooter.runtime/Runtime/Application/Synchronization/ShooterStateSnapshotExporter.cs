using System;
using AbilityKit.Protocol.Shooter;
using AbilityKit.World.Svelto;
using Svelto.DataStructures;
using Svelto.ECS;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public sealed class ShooterStateSnapshotExporter
    {
        private readonly ShooterBattleState _state;
        private readonly IShooterEntityManager _entities;
        private readonly ISveltoWorldContext _context;
        private ShooterPlayerSnapshot[] _transientPlayers = Array.Empty<ShooterPlayerSnapshot>();
        private ShooterBulletSnapshot[] _transientBullets = Array.Empty<ShooterBulletSnapshot>();
        private ShooterEnemySnapshot[] _transientEnemies = Array.Empty<ShooterEnemySnapshot>();
        private ShooterEventSnapshot[] _transientEvents = Array.Empty<ShooterEventSnapshot>();

        public ShooterStateSnapshotExporter(ShooterBattleState state, IShooterEntityManager entities)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _context = _entities.SveltoContext;
        }

        public ShooterStateSnapshotPayload Export()
        {
            return ExportCore(useTransientBuffers: false);
        }

        /// <summary>
        /// Exports a payload backed by reusable exact-length arrays. The payload must be consumed
        /// before the next transient export on this exporter.
        /// </summary>
        public ShooterStateSnapshotPayload ExportTransient()
        {
            return ExportCore(useTransientBuffers: true);
        }

        /// <summary>
        /// Exports only players into the reusable player buffer. The array is invalidated by the
        /// next transient player or full snapshot export on this exporter.
        /// </summary>
        public ShooterPlayerSnapshot[] ExportPlayersTransient()
        {
            return ExportPlayers(useTransientBuffer: true);
        }

        private ShooterStateSnapshotPayload ExportCore(bool useTransientBuffers)
        {
            var players = ExportPlayers(useTransientBuffers);

            var projectileCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoProjectileComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.Projectiles);
            projectileCollection.Deconstruct(out NB<ShooterSveltoProjectileComponent> projectileComponents, out _, out var projectileCount);
            var bullets = useTransientBuffers
                ? EnsureExactLength(ref _transientBullets, projectileCount)
                : new ShooterBulletSnapshot[projectileCount];
            for (int i = 0; i < projectileCount; i++)
            {
                var bullet = projectileComponents[i];
                bullets[i] = new ShooterBulletSnapshot(bullet.BulletId, bullet.OwnerPlayerId, bullet.X, bullet.Y, bullet.VelocityX, bullet.VelocityY, bullet.RemainingFrames);
            }

            var enemyCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoTransformComponent, ShooterSveltoHealthComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.GameplayTargets);
            enemyCollection.Deconstruct(out NB<ShooterSveltoTransformComponent> enemyTransforms, out NB<ShooterSveltoHealthComponent> enemyHealths, out var enemyIds, out var enemyCount);
            var enemies = useTransientBuffers
                ? EnsureExactLength(ref _transientEnemies, enemyCount)
                : new ShooterEnemySnapshot[enemyCount];
            for (int i = 0; i < enemyCount; i++)
            {
                var transform = enemyTransforms[i];
                var health = enemyHealths[i];
                enemies[i] = new ShooterEnemySnapshot(
                    checked((int)enemyIds[i]),
                    transform.X,
                    transform.Y,
                    transform.DirectionX,
                    transform.DirectionY,
                    health.Current,
                    health.Max,
                    health.Alive != 0);
            }

            var eventCount = _state.Events.Count;
            var events = useTransientBuffers
                ? EnsureExactLength(ref _transientEvents, eventCount)
                : eventCount == 0
                    ? Array.Empty<ShooterEventSnapshot>()
                    : new ShooterEventSnapshot[eventCount];
            for (var i = 0; i < eventCount; i++)
            {
                events[i] = _state.Events[i];
            }

            return new ShooterStateSnapshotPayload(
                _state.CurrentFrame,
                players,
                bullets,
                events,
                (int)_state.MatchState,
                _state.TimeLimitFrames,
                _state.RemainingTimeFrames,
                enemies);
        }

        private ShooterPlayerSnapshot[] ExportPlayers(bool useTransientBuffer)
        {
            var playerCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoPlayerComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.Players);
            playerCollection.Deconstruct(out NB<ShooterSveltoPlayerComponent> playerComponents, out _, out var playerCount);
            var players = useTransientBuffer
                ? EnsureExactLength(ref _transientPlayers, playerCount)
                : new ShooterPlayerSnapshot[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                var player = playerComponents[i];
                players[i] = new ShooterPlayerSnapshot(player.PlayerId, player.X, player.Y, player.AimX, player.AimY, player.Hp, player.Score, player.Alive);
            }

            return players;
        }

        private static T[] EnsureExactLength<T>(ref T[] buffer, int length)
        {
            if (buffer.Length != length)
            {
                buffer = length == 0 ? Array.Empty<T>() : new T[length];
            }

            return buffer;
        }
    }
}
