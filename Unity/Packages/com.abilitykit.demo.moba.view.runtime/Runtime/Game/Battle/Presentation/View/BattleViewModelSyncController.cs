namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewModelSyncController
    {
        private readonly BattleViewSyncHandleResolver _handles;
        private readonly BattleViewActorIdBinder _actorIds;
        private readonly BattleViewEntityPositionSampler _positions;
        private readonly BattleViewModelSyncPlanResolver _plans;
        private readonly BattleViewShellSyncOperation _shells;

        public BattleViewModelSyncController(
            BattleViewHandleStore handles,
            BattleViewShellController shells,
            BattleViewTransformController transforms,
            BattleViewResourceProvider resources = null,
            BattleViewModelSyncControllerFactory factory = null)
        {
            factory ??= new BattleViewModelSyncControllerFactory();

            _handles = factory.CreateHandleResolver(handles);
            _actorIds = factory.CreateActorIdBinder(handles);
            _positions = factory.CreatePositionSampler(transforms);
            _plans = factory.CreatePlanResolver(resources);
            _shells = factory.CreateShellSyncOperation(shells);
        }

        public bool Sync(in BattleViewEntitySyncInput input, IBattleRuntimeContext ctx, out BattleViewHandle handle)
        {
            if (!_handles.TryResolve(input.Entity.Id, out handle)) return false;

            _actorIds.Bind(handle, input.ActorId, input.Entity.Id);
            _positions.Sample(in input, ctx);

            var plan = _plans.Resolve(handle, input.Meta);
            _shells.Apply(handle, input.ActorId, in plan);

            return true;
        }
    }

    internal sealed class BattleViewModelSyncControllerFactory
    {
        public BattleViewSyncHandleResolver CreateHandleResolver(BattleViewHandleStore handles)
        {
            return new BattleViewSyncHandleResolver(handles);
        }

        public BattleViewActorIdBinder CreateActorIdBinder(BattleViewHandleStore handles)
        {
            return new BattleViewActorIdBinder(handles);
        }

        public BattleViewEntityPositionSampler CreatePositionSampler(BattleViewTransformController transforms)
        {
            return new BattleViewEntityPositionSampler(transforms);
        }

        public BattleViewModelSyncPlanResolver CreatePlanResolver(BattleViewResourceProvider resources)
        {
            return new BattleViewModelSyncPlanResolver(resources);
        }

        public BattleViewShellSyncOperation CreateShellSyncOperation(BattleViewShellController shells)
        {
            return new BattleViewShellSyncOperation(shells);
        }
    }

    internal sealed class BattleViewSyncHandleResolver
    {
        private readonly BattleViewHandleStore _handles;

        public BattleViewSyncHandleResolver(BattleViewHandleStore handles)
        {
            _handles = handles;
        }

        public bool TryResolve(AbilityKit.World.ECS.IEntityId entityId, out BattleViewHandle handle)
        {
            handle = _handles.GetOrCreate(entityId);
            return handle != null && !handle.Destroyed;
        }
    }

    internal sealed class BattleViewActorIdBinder
    {
        private readonly BattleViewHandleStore _handles;

        public BattleViewActorIdBinder(BattleViewHandleStore handles)
        {
            _handles = handles;
        }

        public void Bind(BattleViewHandle handle, int actorId, AbilityKit.World.ECS.IEntityId entityId)
        {
            _handles.SetActorId(handle, actorId, entityId);
        }
    }

    internal sealed class BattleViewEntityPositionSampler
    {
        private readonly BattleViewTransformController _transforms;

        public BattleViewEntityPositionSampler(BattleViewTransformController transforms)
        {
            _transforms = transforms;
        }

        public void Sample(in BattleViewEntitySyncInput input, IBattleRuntimeContext ctx)
        {
            var position = input.Transform.Position;
            _transforms.SampleEntity(input.Entity, in position, ctx);
        }
    }

    internal readonly struct BattleViewModelSyncPlan
    {
        public BattleViewModelSyncPlan(int desiredModelId, bool recreateShell, AbilityKit.Game.Battle.Entity.BattleEntityKind kind = AbilityKit.Game.Battle.Entity.BattleEntityKind.Unknown)
        {
            DesiredModelId = desiredModelId;
            RecreateShell = recreateShell;
            Kind = kind;
        }

        public int DesiredModelId { get; }
        public bool RecreateShell { get; }
        public AbilityKit.Game.Battle.Entity.BattleEntityKind Kind { get; }
    }

    internal sealed class BattleViewModelSyncPlanResolver
    {
        private readonly BattleViewResourceProvider _resources;

        public BattleViewModelSyncPlanResolver(BattleViewResourceProvider resources = null)
        {
            _resources = BattleViewResourceProvider.OrDefault(resources);
        }

        public BattleViewModelSyncPlan Resolve(BattleViewHandle handle, AbilityKit.Game.Battle.Entity.BattleEntityMetaComponent meta)
        {
            var desiredModelId = _resources.ResolveModelId(meta);
            var recreateShell = desiredModelId > 0 && (handle.GameObject == null || handle.ModelId != desiredModelId);
            var kind = meta != null ? meta.Kind : AbilityKit.Game.Battle.Entity.BattleEntityKind.Unknown;
            return new BattleViewModelSyncPlan(desiredModelId, recreateShell, kind);
        }
    }

    internal sealed class BattleViewShellSyncOperation
    {
        private readonly BattleViewShellController _shells;

        public BattleViewShellSyncOperation(BattleViewShellController shells)
        {
            _shells = shells;
        }

        public void Apply(BattleViewHandle handle, int actorId, in BattleViewModelSyncPlan plan)
        {
            if (!plan.RecreateShell) return;

            handle.Version++;
            _shells.Recreate(handle, actorId, plan.DesiredModelId, plan.Kind);
        }
    }
}
