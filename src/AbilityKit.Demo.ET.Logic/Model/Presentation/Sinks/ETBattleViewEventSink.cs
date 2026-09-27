using System;
using AbilityKit.Demo.Moba.Share;
using ET.AbilityKit.Demo.ET.Share;

namespace ET.Logic
{
    /// <summary>
    /// ET 战斗视图事件接收器。
    /// 将 IBattleViewEventSink 事件桥接到 ET 事件系统。
    /// 同时更新实体缓存组件，供 ET.View 查询。
    ///
    /// 设计：
    /// - 使用虚方法允许子类扩展。
    /// - 将 ET 侧表现缓存更新限制在该适配器内部。
    /// </summary>
    public class ETBattleViewEventSink : IBattleViewEventSink
    {
        private readonly ETBattleComponent _battleComponent;
        private readonly ETBattleEntityCacheComponent _cacheComponent;

        public ETBattleViewEventSink(ETBattleComponent battleComponent, ETBattleEntityCacheComponent cacheComponent)
        {
            _battleComponent = battleComponent ?? throw new ArgumentNullException(nameof(battleComponent));
            _cacheComponent = cacheComponent;
        }

        #region 单位事件

        public void OnEnterGameSnapshot(in FrameSnapshotData snapshot)
        {
            var scene = _battleComponent.Scene();
            if (scene == null)
                return;

            CreatePresentationUnits(scene, in snapshot);

            if (_cacheComponent != null)
            {
                _cacheComponent.UpdateCache(snapshot.FrameIndex, snapshot);
            }
        }

        private void CreatePresentationUnits(Scene scene, in FrameSnapshotData snapshot)
        {
            if (snapshot.ActorSpawns == null || snapshot.ActorSpawns.Count == 0)
            {
                return;
            }

            var unitComponent = scene.GetComponent<ETUnitComponent>();
            if (unitComponent == null)
            {
                Log.Warning("[ETBattleViewEventSink] ETUnitComponent not found, cannot create presentation units");
                return;
            }

            foreach (var spawn in snapshot.ActorSpawns)
            {
                var unit = unitComponent.CreateUnit(
                    actorId: spawn.ActorId,
                    entityCode: spawn.EntityCode,
                    kind: spawn.EntityCode == 1 ? ActorKind.Hero : ActorKind.Monster,
                    name: string.IsNullOrEmpty(spawn.Name) ? $"Actor_{spawn.ActorId}" : spawn.Name,
                    x: spawn.PositionX,
                    y: spawn.PositionY,
                    maxHp: spawn.MaxHp > 0f ? spawn.MaxHp : 100f);

                _cacheComponent?.AddEntity(spawn.ActorId, unit);
            }
        }

        public void OnActorTransformSnapshot(in FrameSnapshotData snapshot)
        {
            var scene = _battleComponent.Scene();
            if (scene == null)
                return;

            if (_cacheComponent != null)
            {
                _cacheComponent.UpdateCache(snapshot.FrameIndex, snapshot);
            }

            // 发布 ActorMoveEvent 事件
            if (snapshot.ActorTransforms != null)
            {
                foreach (var transform in snapshot.ActorTransforms)
                {
                    EventSystem.Instance.Publish<Scene, ActorMoveEvent>(
                        scene,
                        new ActorMoveEvent
                        {
                            ActorId = transform.ActorId,
                            X = transform.PositionX,
                            Y = transform.PositionY,
                            Z = transform.PositionZ,
                            Rotation = transform.RotationY
                        });
                }
            }
        }

        public void OnActorDespawnSnapshot(in FrameSnapshotData snapshot)
        {
            if (snapshot.ActorDespawns == null || snapshot.ActorDespawns.Count == 0)
            {
                return;
            }

            var unitComponent = _battleComponent.Scene()?.GetComponent<ETUnitComponent>();
            foreach (var despawn in snapshot.ActorDespawns)
            {
                if (despawn.ActorId <= 0) continue;

                unitComponent?.RemoveUnit(despawn.ActorId);
                _cacheComponent?.RemoveEntity(despawn.ActorId);
            }
        }

        public virtual void OnSkillStateSnapshot(in FrameSnapshotData snapshot)
        {
        }

        public void OnDamageEventSnapshot(in FrameSnapshotData snapshot)
        {
            var scene = _battleComponent.Scene();
            if (scene == null)
                return;

            if (_cacheComponent != null)
            {
                _cacheComponent.UpdateCache(snapshot.FrameIndex, snapshot);
            }

            // 发布伤害和死亡事件
            foreach (var damage in snapshot.DamageEvents)
            {
                EventSystem.Instance.Publish<Scene, ActorDamageEvent>(
                    scene,
                    new ActorDamageEvent
                    {
                        ActorId = damage.TargetId,
                        SourceActorId = damage.AttackerId,
                        Damage = damage.DamageValue,
                        CurrentHp = damage.TargetHpAfter,
                        MaxHp = 100f
                    });

                if (damage.IsKill)
                {
                    EventSystem.Instance.Publish<Scene, ActorDeadEvent>(
                        scene,
                        new ActorDeadEvent
                        {
                            ActorId = damage.TargetId,
                            KillerId = damage.AttackerId
                        });
                }

                Log.Debug($"[ETBattleViewEventSink] Damage: {damage.AttackerId} -> {damage.TargetId}, dmg={damage.DamageValue}, kill={damage.IsKill}");
            }
        }

        public void OnPresentationCueSnapshot(in FrameSnapshotData snapshot)
        {
        }

        #endregion

        #region 战斗事件

        public void OnBattleStart(int frameIndex)
        {
            _battleComponent.ViewSink?.OnBattleStart(new BattleStartEvent()
            {
                BattleId = _battleComponent.BattleId,
                PlayerId = _battleComponent.PlayerId
            });
        }

        public void OnBattleEnd(int frameIndex, int winTeamId)
        {
            bool isVictory = winTeamId == 1;
            _battleComponent.ViewSink?.OnBattleEnd(new BattleEndEvent()
            {
                BattleId = _battleComponent.BattleId,
                IsVictory = isVictory
            });
        }

        public void OnFrameSyncComplete(int frameIndex)
        {
        }

        #endregion

        #region 扩展事件

        public virtual void OnProjectileEventSnapshot(in FrameSnapshotData snapshot)
        {
        }

        public virtual void OnAreaEventSnapshot(in FrameSnapshotData snapshot)
        {
        }

        public virtual void OnStateHashSnapshot(in FrameSnapshotData snapshot)
        {
        }

        public virtual void OnTriggerEvent(in TriggerEventData evt)
        {
        }

        #endregion
    }
}
