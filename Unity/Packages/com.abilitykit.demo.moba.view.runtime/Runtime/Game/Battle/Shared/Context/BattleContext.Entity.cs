using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleContext
    {
        private readonly BattleEntityContext _entities = new BattleEntityContext();
        private readonly BattlePresentationContext _presentation =
            new BattlePresentationContext();

        public WorldId RuntimeWorldId
        {
            get => _entities.RuntimeWorldId;
            set => _entities.RuntimeWorldId = value;
        }

        public bool HasRuntimeWorldId
        {
            get => _entities.HasRuntimeWorldId;
            set => _entities.HasRuntimeWorldId = value;
        }

        /// <summary>
        /// 远端插值路径激活标记（会话实例级，替代原 BattleSyncFeature 静态标志）。
        /// Gateway 传输创建插值播放后置 true；DisposeRemoteInterpolation 复位。
        /// 为 true 时 BattleSyncFeature 不再订阅 ActorTransform（由插值路径写入）。
        /// </summary>
        public bool EnableRemoteInterpolation
        {
            get => _presentation.EnableRemoteInterpolation;
            set => _presentation.EnableRemoteInterpolation = value;
        }

        public EC.IEntity EntityNode
        {
            get => _entities.EntityNode;
            set => _entities.EntityNode = value;
        }

        public EC.IECWorld EntityWorld
        {
            get => _entities.EntityWorld;
            set => _entities.EntityWorld = value;
        }

        public BattleEntityLookup EntityLookup
        {
            get => _entities.EntityLookup;
            set => _entities.EntityLookup = value;
        }

        public BattleEntityFactory EntityFactory
        {
            get => _entities.EntityFactory;
            set => _entities.EntityFactory = value;
        }

        public IBattleEntityQuery EntityQuery
        {
            get => _entities.EntityQuery;
            set => _entities.EntityQuery = value;
        }

        public List<EC.IEntityId> DirtyEntities
        {
            get => _entities.DirtyEntities;
            set => _entities.DirtyEntities = value;
        }

        public BattleVfxManager ViewVfxManager
        {
            get => _presentation.ViewVfxManager;
            set => _presentation.ViewVfxManager = value;
        }

        public EC.IEntity ViewVfxNode
        {
            get => _presentation.ViewVfxNode;
            set => _presentation.ViewVfxNode = value;
        }

        internal long BindEntityRuntime(
            EC.IEntity node,
            EC.IECWorld world,
            BattleEntityLookup lookup,
            BattleEntityFactory factory,
            IBattleEntityQuery query,
            List<EC.IEntityId> dirtyEntities = null) =>
            _entities.Bind(node, world, lookup, factory, query, dirtyEntities);

        internal bool ClearEntityRuntime(long bindingGeneration) =>
            _entities.ClearBinding(bindingGeneration);

        internal long BindViewVfx(BattleVfxManager manager, EC.IEntity node) =>
            _presentation.BindVfx(manager, node);

        internal bool ClearViewVfx(long bindingGeneration) =>
            _presentation.ClearVfx(bindingGeneration);

        internal long BeginRemoteInterpolation() =>
            _presentation.BeginRemoteInterpolation();

        internal bool EndRemoteInterpolation(long generation) =>
            _presentation.EndRemoteInterpolation(generation);
    }
}
