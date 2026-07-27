using System.Collections.Generic;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Vfx;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleContext
    {
        public WorldId RuntimeWorldId;
        public bool HasRuntimeWorldId;
        /// <summary>
        /// 远端插值路径激活标记（会话实例级，替代原 BattleSyncFeature 静态标志）。
        /// Gateway 传输创建插值播放后置 true；DisposeRemoteInterpolation 复位。
        /// 为 true 时 BattleSyncFeature 不再订阅 ActorTransform（由插值路径写入）。
        /// </summary>
        public bool EnableRemoteInterpolation;
        public EC.IEntity EntityNode;
        public EC.IECWorld EntityWorld;
        public BattleEntityLookup EntityLookup;
        public BattleEntityFactory EntityFactory;
        public IBattleEntityQuery EntityQuery;
        public BattleVfxManager ViewVfxManager;
        public EC.IEntity ViewVfxNode;
        public List<EC.IEntityId> DirtyEntities;
    }
}
