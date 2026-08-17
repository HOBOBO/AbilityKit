using System.Collections.Generic;
using AbilityKit.World.Svelto;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public interface IShooterEntityManager
    {
        ISveltoWorldContext SveltoContext { get; }

        int MaxEntityCount { get; }

        int PlayerCount { get; }

        int ProjectileCount { get; }

        int EnemyCount { get; }

        long MutationRevision { get; }

        IReadOnlyCollection<int> PlayerIds { get; }

        IReadOnlyCollection<int> ProjectileIds { get; }

        IReadOnlyCollection<int> EnemyIds { get; }

        void Clear();

        void BeginStructuralChanges();

        void EndStructuralChanges();

        void SubmitStructuralChanges();

        bool HasPlayer(int playerId);

        bool TryGetPlayer(int playerId, out ShooterSveltoPlayerComponent player);

        void AddPlayer(in ShooterSveltoPlayerComponent player);

        void SetPlayer(in ShooterSveltoPlayerComponent player);

        void RemovePlayer(int playerId);

        bool HasProjectile(int bulletId);

        bool TryGetProjectile(int bulletId, out ShooterSveltoProjectileComponent projectile);

        void AddProjectile(in ShooterSveltoProjectileComponent projectile);

        void SetProjectile(in ShooterSveltoProjectileComponent projectile);

        void RemoveProjectile(int bulletId);

        bool HasEnemy(int enemyId);

        bool TryGetEnemy(int enemyId, out ShooterSveltoTransformComponent transform, out ShooterSveltoHealthComponent health);

        void AddEnemy(int enemyId, in ShooterSveltoTransformComponent transform, in ShooterSveltoHealthComponent health);

        void AddEnemy(
            int enemyId,
            in ShooterSveltoTransformComponent transform,
            in ShooterSveltoHealthComponent health,
            in ShooterSveltoNavigationComponent navigation);

        void SetEnemy(int enemyId, in ShooterSveltoTransformComponent transform, in ShooterSveltoHealthComponent health);

        void RemoveEnemy(int enemyId);
    }
}
