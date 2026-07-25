using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Transport.Projection;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services.Projection
{
    /// <summary>
    /// 标准投影生产方：从逻辑 world 的 MobaActorRegistry 提取 ActorProjectionData。
    ///
    /// 这是逻辑→表现/网关的**唯一标准提取入口**。所有消费方（snapshot 通道 / 预测通道 /
    /// 网关转发）通过 <see cref="IActorProjectionProducer"/> 接口消费，不直接访问 Entitas entity。
    ///
    /// 关键价值：提取逻辑只有一份。字段增减只改这里，所有下游自动对齐。
    ///
    /// 字段约定（与 wire 协议对齐）：
    /// - TeamId：team 组件枚举值（Team.None → 0）
    /// - VelX/VelZ：motion 组件的 MotionOutput.NewVelocity（无 motion 组件时为 0）
    /// - Kind：SpawnEntityKind（Character=1 / Projectile=2，按 entityMainType 判定）
    /// - Code：modelId 配置 id（投射物为模板 id）
    /// - OwnerNetId：ownerLink.OwnerActorId（投射物的拥有者 actorId；角色为 0）
    /// </summary>
    [WorldService(typeof(IActorProjectionProducer))]
    public sealed class MobaActorProjectionProducer : IActorProjectionProducer, IService
    {
        private readonly MobaActorRegistry _registry;

        public MobaActorProjectionProducer(MobaActorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void Dispose()
        {
        }

        public ActorProjectionData ExtractFull(int actorId)
        {
            return Extract(actorId, ActorProjectionFields.FullState);
        }

        public void ExtractAll(List<ActorProjectionData> buffer)
        {
            if (buffer == null || _registry == null) return;
            buffer.Clear();
            foreach (var kv in _registry.Entries)
            {
                var data = ExtractFull(kv.Key);
                if (data.Has(ActorProjectionFields.Core))
                {
                    buffer.Add(data);
                }
            }
            // 按 ActorId 排序确保确定性
            buffer.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
        }

        public ActorProjectionData ExtractSpawn(int actorId)
        {
            return Extract(actorId, ActorProjectionFields.SpawnInfo);
        }

        private ActorProjectionData Extract(int actorId, ActorProjectionFields fields)
        {
            if (!_registry.TryGet(actorId, out var e) || e == null) return default;
            if (!e.hasTransform) return default;

            var t = e.transform.Value;

            float hp = 0f, hpMax = 0f;
            if (e.hasAttributeGroup && e.attributeGroup.Group != null)
            {
                var group = e.attributeGroup.Group;
                hp = group.GetValue(MobaAttributeIds.HP);
                hpMax = group.GetValue(MobaAttributeIds.MAX_HP);
            }

            var teamId = e.hasTeam ? (int)e.team.Value : 0;

            float velX = 0f, velZ = 0f;
            if (e.hasMotion)
            {
                var v = e.motion.Output.NewVelocity;
                velX = v.X;
                velZ = v.Z;
            }

            var kind = e.hasEntityMainType && e.entityMainType.Value == EntityMainType.Projectile
                ? (int)SpawnEntityKind.Projectile
                : (int)SpawnEntityKind.Character;
            var code = e.hasModelId ? e.modelId.Value : 0;
            var ownerNetId = e.hasOwnerLink ? e.ownerLink.OwnerActorId : 0;

            return new ActorProjectionData(
                actorId,
                t.Position.X, t.Position.Y, t.Position.Z,
                t.Rotation.X, t.Rotation.Y, t.Rotation.Z, t.Rotation.W,
                t.Scale.X, t.Scale.Y, t.Scale.Z,
                hp, hpMax,
                teamId,
                velX, velZ,
                kind, code, ownerNetId,
                fields);
        }
    }
}
