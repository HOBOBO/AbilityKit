using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Common.Pool;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow.Snapshot;
using EC = AbilityKit.Ability.EC;
using AbilityKit.Game.Battle;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleContext : IPoolable
    {
        private static readonly ObjectPool<BattleContext> Pool = Pools.GetPool(
            key: "BattleContext",
            createFunc: () => new BattleContext(),
            defaultCapacity: 1,
            maxSize: 8);

        public BattleLogicSession Session;
        public BattleStartPlan Plan;
        public int LastFrame;

        public FrameSnapshotDispatcher FrameSnapshots;
        public BattleSnapshotPipeline SnapshotPipeline;
        public BattleCmdHandler CmdHandler;

        public EC.Entity EntityNode;
        public EC.EntityWorld EntityWorld;
        public BattleEntityLookup EntityLookup;
        public BattleEntityFactory EntityFactory;
        public IBattleEntityQuery EntityQuery;

        public List<EC.EntityId> DirtyEntities;

        public float HudMoveDx;
        public float HudMoveDz;
        public bool HudHasMove;

        public int HudSkillClickSlot;

        public static BattleContext Rent()
        {
            return Pool.Get();
        }

        public static void Return(BattleContext ctx)
        {
            if (ctx == null) return;
            Pool.Release(ctx);
        }

        void IPoolable.OnPoolGet()
        {
        }

        void IPoolable.OnPoolRelease()
        {
            Session = null;
            Plan = default;
            LastFrame = 0;

            FrameSnapshots = null;
            SnapshotPipeline = null;
            CmdHandler = null;

            EntityNode = default;
            EntityWorld = null;
            EntityLookup = null;
            EntityFactory = null;
            EntityQuery = null;

            DirtyEntities?.Clear();

            HudMoveDx = 0f;
            HudMoveDz = 0f;
            HudHasMove = false;
            HudSkillClickSlot = 0;
        }

        void IPoolable.OnPoolDestroy()
        {
            Session = null;
            Plan = default;
            LastFrame = 0;

            FrameSnapshots = null;
            SnapshotPipeline = null;
            CmdHandler = null;

            EntityNode = default;
            EntityWorld = null;
            EntityLookup = null;
            EntityFactory = null;
            EntityQuery = null;

            DirtyEntities = null;

            HudMoveDx = 0f;
            HudMoveDz = 0f;
            HudHasMove = false;
            HudSkillClickSlot = 0;
        }
    }

    public sealed class BattleContextFeature : IGamePhaseFeature
    {
        public void OnAttach(in GamePhaseContext ctx)
        {
            if (ctx.Root.TryGetComponent(out BattleContext existing) && existing != null) return;
            ctx.Root.AddComponent(BattleContext.Rent());
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (ctx.Root.IsValid)
            {
                if (ctx.Root.TryGetComponent(out BattleContext existing) && existing != null)
                {
                    ctx.Root.RemoveComponent(typeof(BattleContext));
                    BattleContext.Return(existing);
                }
                else
                {
                    ctx.Root.RemoveComponent(typeof(BattleContext));
                }
            }
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }
    }
}
