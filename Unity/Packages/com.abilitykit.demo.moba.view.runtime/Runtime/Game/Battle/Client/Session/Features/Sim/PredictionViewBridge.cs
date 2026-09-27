using System.Collections.Generic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.World.ECS;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// P0-3 FIX: 预测 world → view EntityWorld 桥接。
    ///
    /// 当前渲染层只读服务端 snapshot 驱动的 view EntityWorld。预测 world 的状态
    /// 完全不流入渲染——玩家按键后要等完整 RTT 才看到反馈。预测回滚"暗跑"。
    ///
    /// 本桥在每帧预测 tick 后，通过 <see cref="IActorProjectionProducer"/> 提取本地玩家的
    /// 投影数据并覆写 view EntityWorld 中对应实体的值。远程玩家继续走 snapshot 插值（不变）。
    ///
    /// 投影规范迁移（2026-07-24）已完成：
    /// 本类实现 <see cref="IActorProjectionConsumer"/>，与 snapshot 通道共享同一套
    /// 提取/消费接口——提取逻辑只在 MobaActorProjectionProducer 一份，
    /// 字段增减不再需要双侧同步修改。
    /// </summary>
    internal sealed class PredictionViewBridge : IActorProjectionConsumer
    {
        private readonly IECWorld _viewWorld;
        private readonly BattleEntityLookup _viewLookup;
        private readonly BattleContext _viewContext;
        private readonly Vector3 _offset;
        private readonly List<ActorProjectionData> _buffer = new List<ActorProjectionData>(128);
        private readonly HashSet<int> _activeActors = new HashSet<int>();
        private readonly HashSet<int> _currentActors = new HashSet<int>();
        private readonly List<int> _removedActors = new List<int>();

        public PredictionViewBridge(IECWorld viewWorld, BattleEntityLookup viewLookup,
            BattleContext viewContext = null, Vector3 offset = default)
        {
            _viewWorld = viewWorld;
            _viewLookup = viewLookup;
            _viewContext = viewContext;
            _offset = offset;
        }

        public void SyncAllActors(IActorProjectionProducer producer)
        {
            if (producer == null || _viewContext?.EntityFactory == null) return;
            producer.ExtractAll(_buffer);
            _currentActors.Clear();
            foreach (var data in _buffer)
            {
                if (data.ActorId <= 0 || !data.Has(ActorProjectionFields.Core)) continue;
                _currentActors.Add(data.ActorId);
                if (!_activeActors.Contains(data.ActorId) ||
                    !_viewLookup.TryResolve(_viewWorld, new BattleNetId(data.ActorId), out _))
                {
                    var spawn = producer.ExtractSpawn(data.ActorId);
                    if (!spawn.Has(ActorProjectionFields.Spawn)) continue;
                    ApplyActor(in spawn);
                    _activeActors.Add(data.ActorId);
                }
                ApplyActor(in data);
            }

            _removedActors.Clear();
            foreach (var actorId in _activeActors)
                if (!_currentActors.Contains(actorId)) _removedActors.Add(actorId);
            foreach (var actorId in _removedActors) RemoveActor(actorId, 0);
        }

        public void ClearActors()
        {
            _removedActors.Clear();
            _removedActors.AddRange(_activeActors);
            foreach (var actorId in _removedActors) RemoveActor(actorId, 0);
            _buffer.Clear();
            _currentActors.Clear();
        }

        /// <summary>
        /// 从预测 world 读本地玩家状态，推送到 view EntityWorld。
        /// 只覆盖本地玩家——远程玩家由 snapshot 插值负责。
        /// </summary>
        /// <param name="producer">预测 world 的投影生产方（从 world Services 解析）</param>
        /// <param name="localActorId">本地玩家的 actorId</param>
        public void SyncLocalPlayer(IActorProjectionProducer producer, int localActorId)
        {
            if (producer == null || localActorId <= 0) return;

            var data = producer.ExtractFull(localActorId);
            if (!data.Has(ActorProjectionFields.Core)) return;

            ApplyActor(in data);
        }

        /// <summary>
        /// 将投影数据覆写到 view EntityWorld 中对应实体的 transform
        /// （优先于 snapshot 写入的值——本地玩家走预测）。
        /// </summary>
        public void ApplyActor(in ActorProjectionData data)
        {
            if (_viewWorld == null || _viewLookup == null) return;

            var netId = new BattleNetId(data.ActorId);
            if (!_viewLookup.TryResolve(_viewWorld, netId, out var viewEntity))
            {
                if (_viewContext?.EntityFactory == null || !data.Has(ActorProjectionFields.Spawn)) return;
                viewEntity = data.Kind == 2
                    ? _viewContext.EntityFactory.CreateProjectile(netId, new BattleNetId(data.OwnerNetId), data.Code)
                    : _viewContext.EntityFactory.CreateCharacter(netId, data.Code);
            }

            if (viewEntity.TryGetRef(out BattleTransformComponent viewTransform) && viewTransform != null)
            {
                viewTransform.Position = new Vector3(data.PosX, data.PosY, data.PosZ) + _offset;
                viewTransform.Forward = RotationToForward(data.RotX, data.RotY, data.RotZ, data.RotW);
                _viewContext?.DirtyEntities?.Add(viewEntity.Id);
            }
            if (_viewContext != null &&
                viewEntity.TryGetRef(out BattleCharacterComponent character) && character != null)
            {
                if (data.Has(ActorProjectionFields.TeamId)) character.TeamId = data.TeamId;
                if (data.Has(ActorProjectionFields.Hp))
                {
                    character.Hp = data.Hp;
                    character.HpMax = data.HpMax;
                }
                if (data.Has(ActorProjectionFields.Spawn)) character.ModelId = data.Code;
            }
        }

        /// <summary>
        /// The primary view keeps snapshot-owned entities; an independent view owns its actors.
        /// </summary>
        public void RemoveActor(int actorId, int frame)
        {
            if (_viewContext == null || actorId <= 0) return;
            var netId = new BattleNetId(actorId);
            if (_viewLookup.TryResolve(_viewWorld, netId, out var entity) && entity.IsValid)
                entity.Destroy();
            _viewLookup.Unbind(netId);
            _activeActors.Remove(actorId);
        }

        // forward = q * (0,0,1)，与 Transform3.Forward = Rotation.Rotate(Vec3.Forward) 同语义
        private static Vector3 RotationToForward(float x, float y, float z, float w)
        {
            return new Vector3(
                2f * (x * z + w * y),
                2f * (y * z - w * x),
                1f - 2f * (x * x + y * y));
        }
    }
}
