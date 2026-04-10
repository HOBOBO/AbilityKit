using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.Pool;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Core.Common.SnapshotRouting;
using AbilityKit.Core.Common.Record.Lockstep;
using AbilityKit.World.ECS;
using EC = AbilityKit.World.ECS;
using AbilityKit.Game.Battle;
using System.Collections.Generic;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow.Battle.Modules;

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
        public double LogicTimeSeconds;

        public BattleEventBus Events;

        public int LocalActorId;

        public FrameSnapshotDispatcher FrameSnapshots;
        public SnapshotPipeline SnapshotPipeline;
        public SnapshotCmdHandler CmdHandler;

        public ILockstepInputRecordWriter InputRecordWriter;

        public BattleLocalInputQueue LocalInputQueue;

        public AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionDriverStats PredictionStats;

        public AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionReconcileTarget PredictionReconcileTarget;

        public AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionReconcileControl PredictionReconcileControl;

        public AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionTuningControl PredictionTuningControl;

        public WorldId RuntimeWorldId;

        public bool HasRuntimeWorldId;

        public EC.IEntity EntityNode;
        public EC.IECWorld EntityWorld;
        public BattleEntityLookup EntityLookup;
        public BattleEntityFactory EntityFactory;
        public IBattleEntityQuery EntityQuery;

        public List<EC.IEntityId> DirtyEntities;

        public float HudMoveDx;
        public float HudMoveDz;
        public bool HudHasMove;

        public int HudSkillClickSlot;

        public bool HudSkillAiming;
        public int HudSkillAimSlot;
        public float HudSkillAimDx;
        public float HudSkillAimDz;

        public bool HudSkillAimSubmit;
        public int HudSkillAimSubmitSlot;
        public float HudSkillAimSubmitDx;
        public float HudSkillAimSubmitDz;

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
            LogicTimeSeconds = 0d;

            Events = null;

            LocalActorId = 0;

            FrameSnapshots = null;
            SnapshotPipeline = null;
            CmdHandler = null;

            InputRecordWriter?.Dispose();
            InputRecordWriter = null;

            LocalInputQueue?.Dispose();
            LocalInputQueue = null;

            PredictionStats = null;
            PredictionReconcileTarget = null;
            PredictionReconcileControl = null;
            PredictionTuningControl = null;

            RuntimeWorldId = default;
            HasRuntimeWorldId = false;

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

            HudSkillAiming = false;
            HudSkillAimSlot = 0;
            HudSkillAimDx = 0f;
            HudSkillAimDz = 0f;

            HudSkillAimSubmit = false;
            HudSkillAimSubmitSlot = 0;
            HudSkillAimSubmitDx = 0f;
            HudSkillAimSubmitDz = 0f;
        }

        void IPoolable.OnPoolDestroy()
        {
            Session = null;
            Plan = default;
            LastFrame = 0;
            LogicTimeSeconds = 0d;

            Events = null;

            LocalActorId = 0;

            FrameSnapshots = null;
            SnapshotPipeline = null;
            CmdHandler = null;

            InputRecordWriter?.Dispose();
            InputRecordWriter = null;

            LocalInputQueue?.Dispose();
            LocalInputQueue = null;

            PredictionStats = null;
            PredictionReconcileTarget = null;
            PredictionReconcileControl = null;
            PredictionTuningControl = null;

            RuntimeWorldId = default;
            HasRuntimeWorldId = false;

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

            HudSkillAiming = false;
            HudSkillAimSlot = 0;
            HudSkillAimDx = 0f;
            HudSkillAimDz = 0f;

            HudSkillAimSubmit = false;
            HudSkillAimSubmitSlot = 0;
            HudSkillAimSubmitDx = 0f;
            HudSkillAimSubmitDz = 0f;
        }
    }

    public sealed class BattleContextFeature : IGamePhaseFeature
    {
        public void OnAttach(in GamePhaseContext ctx)
        {
            if (ctx.Root.TryGetRef(out BattleContext existing) && existing != null) return;
            ctx.Root.WithRef(BattleContext.Rent());
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (ctx.Root.IsValid)
            {
                if (ctx.Root.TryGetRef(out BattleContext existing) && existing != null)
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
