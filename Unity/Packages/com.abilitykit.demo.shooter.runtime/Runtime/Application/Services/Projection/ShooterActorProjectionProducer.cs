using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Transport.Projection;
using AbilityKit.World.Svelto;
using Svelto.DataStructures;
using Svelto.ECS;
using Svelto.ECS.Internal;

namespace AbilityKit.Demo.Shooter.Runtime
{
    /// <summary>
    /// Shooter 标准投影生产方：从 Svelto world 的 entity 组件提取 ActorProjectionData。
    ///
    /// 与 MOBA 的 <c>MobaActorProjectionProducer</c> 对齐，作为逻辑→表现/网关的
    /// 统一字段提取入口。所有消费方（snapshot 通道 / 预测通道 / 网关转发）
    /// 通过 <see cref="IActorProjectionProducer"/> 接口消费，不直接访问 Svelto EntitiesDB。
    ///
    /// 当前状态（P1 最小实现）：Player + Projectile + Enemy 三类实体的
    /// Position / Rotation(yaw) / HP / TeamId 提取。后续可扩展 Velocity / Spawn 层。
    /// </summary>
    public sealed class ShooterActorProjectionProducer : IActorProjectionProducer
    {
        // ActorProjectionData.Kind 约定：与 wire 协议对齐。
        // Character=1（Player/Enemy），Projectile=2（投射物）。
        private const int KindCharacter = 1;
        private const int KindProjectile = 2;

        private readonly IShooterEntityManager _entities;

        public ShooterActorProjectionProducer(IShooterEntityManager entities)
        {
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        }

        public ActorProjectionData ExtractFull(int actorId)
        {
            var context = _entities.SveltoContext;
            if (context == null) return default;

            // 尝试 Player
            if (TryExtractPlayer(context, actorId, out var data)) return data;

            // 尝试 Projectile
            if (TryExtractProjectile(context, actorId, out data)) return data;

            // 尝试 Enemy (GameplayTarget)
            if (TryExtractEnemy(context, actorId, out data)) return data;

            return default;
        }

        public void ExtractAll(List<ActorProjectionData> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();

            var context = _entities.SveltoContext;
            if (context == null) return;

            ExtractAllPlayers(context, buffer);
            ExtractAllProjectiles(context, buffer);
            ExtractAllEnemies(context, buffer);

            buffer.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
        }

        public ActorProjectionData ExtractSpawn(int actorId)
        {
            // Shooter Spawn 层字段（Kind/Code/OwnerNetId）当前最小实现：
            // Kind = 实体类型（Player/Projectile/Enemy），Code = 0，OwnerNetId = 投射物 owner
            return ExtractFull(actorId);
        }

        // ===== Player =====

        private static bool TryExtractPlayer(ISveltoWorldContext context, int actorId, out ActorProjectionData data)
        {
            data = default;
            var collection = context.EntitiesDB.QueryEntities<ShooterSveltoPlayerComponent>(
                (ExclusiveGroupStruct)ShooterSveltoGroups.Players);
            collection.Deconstruct(out NB<ShooterSveltoPlayerComponent> players, out _, out var count);

            for (int i = 0; i < count; i++)
            {
                if (players[i].PlayerId != actorId) continue;

                data = CreatePlayerProjection(in players[i]);
                return true;
            }

            return false;
        }

        private static void ExtractAllPlayers(ISveltoWorldContext context, List<ActorProjectionData> buffer)
        {
            var collection = context.EntitiesDB.QueryEntities<ShooterSveltoPlayerComponent>(
                (ExclusiveGroupStruct)ShooterSveltoGroups.Players);
            collection.Deconstruct(out NB<ShooterSveltoPlayerComponent> players, out _, out var count);

            for (int i = 0; i < count; i++)
            {
                buffer.Add(CreatePlayerProjection(in players[i]));
            }
        }

        private static ActorProjectionData CreatePlayerProjection(in ShooterSveltoPlayerComponent player)
        {
            // Player 无独立 Transform 组件——X/Y 在 PlayerComponent 中。
            // Rotation 从 AimX/AimY 推导 yaw。
            var yaw = MathF.Atan2(player.AimY, player.AimX);

            return new ActorProjectionData(
                player.PlayerId,
                posX: player.X, posY: player.Y, posZ: 0f,
                rotX: 0f, rotY: 0f, rotZ: 0f, rotW: 1f,
                scaleX: 1f, scaleY: 1f, scaleZ: 1f,
                hp: player.Hp, hpMax: player.Hp, // Shooter player 无独立 MaxHp 字段，用当前值
                teamId: (int)ShooterSveltoGameplayFaction.Shooter,
                velX: 0f, velZ: 0f,
                kind: KindCharacter,
                code: 0,
                ownerNetId: 0,
                ActorProjectionFields.FullState);
        }

        // ===== Projectile =====

        private static bool TryExtractProjectile(ISveltoWorldContext context, int actorId, out ActorProjectionData data)
        {
            data = default;
            var collection = context.EntitiesDB.QueryEntities<ShooterSveltoProjectileComponent>(
                (ExclusiveGroupStruct)ShooterSveltoGroups.Projectiles);
            collection.Deconstruct(out NB<ShooterSveltoProjectileComponent> projectiles, out _, out var count);

            for (int i = 0; i < count; i++)
            {
                if (projectiles[i].BulletId != actorId) continue;

                data = CreateProjectileProjection(in projectiles[i]);
                return true;
            }

            return false;
        }

        private static void ExtractAllProjectiles(ISveltoWorldContext context, List<ActorProjectionData> buffer)
        {
            var collection = context.EntitiesDB.QueryEntities<ShooterSveltoProjectileComponent>(
                (ExclusiveGroupStruct)ShooterSveltoGroups.Projectiles);
            collection.Deconstruct(out NB<ShooterSveltoProjectileComponent> projectiles, out _, out var count);

            for (int i = 0; i < count; i++)
            {
                buffer.Add(CreateProjectileProjection(in projectiles[i]));
            }
        }

        private static ActorProjectionData CreateProjectileProjection(in ShooterSveltoProjectileComponent proj)
        {
            var yaw = MathF.Atan2(proj.VelocityY, proj.VelocityX);

            return new ActorProjectionData(
                proj.BulletId,
                posX: proj.X, posY: proj.Y, posZ: 0f,
                rotX: 0f, rotY: 0f, rotZ: 0f, rotW: 1f,
                scaleX: 1f, scaleY: 1f, scaleZ: 1f,
                hp: 0f, hpMax: 0f,
                teamId: 0,
                velX: proj.VelocityX, velZ: proj.VelocityY,
                kind: KindProjectile,
                code: 0,
                ownerNetId: proj.OwnerPlayerId,
                ActorProjectionFields.FullState);
        }

        // ===== Enemy (GameplayTarget) =====

        private static bool TryExtractEnemy(ISveltoWorldContext context, int actorId, out ActorProjectionData data)
        {
            data = default;
            var collection = context.EntitiesDB.QueryEntities<ShooterSveltoTransformComponent, ShooterSveltoHealthComponent>(
                (ExclusiveGroupStruct)ShooterSveltoGroups.GameplayTargets);
            collection.Deconstruct(out NB<ShooterSveltoTransformComponent> transforms, out NB<ShooterSveltoHealthComponent> healths, out _, out var count);

            for (int i = 0; i < count; i++)
            {
                // Enemy 用 entity index 作为 actorId——需要检查 entity manager 的映射
                data = CreateEnemyProjection(actorId, in transforms[i], in healths[i]);
                return true;
            }

            return false;
        }

        private static void ExtractAllEnemies(ISveltoWorldContext context, List<ActorProjectionData> buffer)
        {
            var collection = context.EntitiesDB.QueryEntities<ShooterSveltoTransformComponent, ShooterSveltoHealthComponent>(
                (ExclusiveGroupStruct)ShooterSveltoGroups.GameplayTargets);
            collection.Deconstruct(out NB<ShooterSveltoTransformComponent> transforms, out NB<ShooterSveltoHealthComponent> healths, out _, out var count);

            for (int i = 0; i < count; i++)
            {
                buffer.Add(CreateEnemyProjection(i, in transforms[i], in healths[i]));
            }
        }

        private static ActorProjectionData CreateEnemyProjection(int actorId, in ShooterSveltoTransformComponent transform, in ShooterSveltoHealthComponent health)
        {
            var yaw = MathF.Atan2(transform.DirectionY, transform.DirectionX);

            return new ActorProjectionData(
                actorId,
                posX: transform.X, posY: transform.Y, posZ: 0f,
                rotX: 0f, rotY: 0f, rotZ: 0f, rotW: 1f,
                scaleX: 1f, scaleY: 1f, scaleZ: 1f,
                hp: health.Current, hpMax: health.Max,
                teamId: (int)ShooterSveltoGameplayFaction.Target,
                velX: 0f, velZ: 0f,
                kind: KindCharacter,
                code: 0,
                ownerNetId: 0,
                ActorProjectionFields.FullState);
        }
    }
}
