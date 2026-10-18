using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow.Battle.Modules;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleContext
    {
        BattleLogicSession IBattleRuntimeContext.Session => Session;
        BattleStartPlan IBattleRuntimeContext.Plan => Plan;
        int IBattleRuntimeContext.LastFrame => LastFrame;
        double IBattleRuntimeContext.LogicTimeSeconds => LogicTimeSeconds;
        int IBattleRuntimeContext.LocalActorId => LocalActorId;
        BattleSessionHooks IBattleRuntimeContext.Hooks => Hooks;

        WorldId IBattleEntityContext.RuntimeWorldId => RuntimeWorldId;
        bool IBattleEntityContext.HasRuntimeWorldId => HasRuntimeWorldId;
        EC.IEntity IBattleEntityContext.EntityNode => EntityNode;
        EC.IECWorld IBattleEntityContext.EntityWorld => EntityWorld;
        BattleEntityLookup IBattleEntityContext.EntityLookup => EntityLookup;
        BattleEntityFactory IBattleEntityContext.EntityFactory => EntityFactory;
        IBattleEntityQuery IBattleEntityContext.EntityQuery => EntityQuery;

        long IBattleEntityRuntimeBindingPort.BindEntityRuntime(
            EC.IEntity node, EC.IECWorld world, BattleEntityLookup lookup,
            BattleEntityFactory factory, IBattleEntityQuery query) =>
            BindEntityRuntime(node, world, lookup, factory, query);
        bool IBattleEntityRuntimeBindingPort.ClearEntityRuntime(long bindingGeneration) =>
            ClearEntityRuntime(bindingGeneration);

        IFrameRecordWriter IBattleInputContext.InputRecordWriter => InputRecordWriter;
        BattleLocalInputQueue IBattleInputContext.LocalInputQueue => LocalInputQueue;

        FrameSnapshotDispatcher IBattleSnapshotRoutingContext.FrameSnapshots => FrameSnapshots;
        SnapshotPipeline IBattleSnapshotRoutingContext.SnapshotPipeline => SnapshotPipeline;
        SnapshotCmdHandler IBattleSnapshotRoutingContext.CmdHandler => CmdHandler;
    }
}
