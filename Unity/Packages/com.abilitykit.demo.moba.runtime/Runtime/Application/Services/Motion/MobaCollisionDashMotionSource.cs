using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Continuous;

namespace AbilityKit.Demo.Moba.Services.Motion
{
    /// <summary>A collision-ending dash. Callback and terminal latch belong to this exact source.</summary>
    public sealed class MobaCollisionDashMotionSource : IMotionSource, IMotionSnapshotSource,
        IMotionCollisionPolicySource, IMotionCompletionCollisionPolicySource
    {
        private readonly FixedDeltaMotionSource _inner;
        private MobaMotionContinuousRuntime _owner;
        private bool _hitClaimed;
        public MobaCollisionDashMotionSource(FixedDeltaMotionSource inner, MobaMotionHitTriggerRuntime callback)
        { _inner = inner; Callback = callback; }
        public MobaMotionHitTriggerRuntime Callback { get; }
        public long SourceContextId => _owner?.SourceContextId ?? 0;
        public bool HasEndpointPolicy => _inner.HasCompletionCollisionPolicy;
        public MotionCollisionConstraints EndpointPolicy => _inner.CompletionCollisionPolicy;
        public int GroupId => _inner.GroupId;
        public int Priority => _inner.Priority;
        public MotionStacking Stacking => _inner.Stacking;
        public bool IsActive => _inner.IsActive;
        public bool HasCollisionPolicy => true;
        public MotionCollisionConstraints CollisionPolicy => _inner.CollisionPolicy;
        // The last integration step must still sweep enemies, even when duration expires in Tick.
        public bool HasCompletionCollisionPolicy => true;
        public MotionCollisionConstraints CompletionCollisionPolicy => CollisionPolicy;
        internal void Bind(MobaMotionContinuousRuntime owner) { _owner = owner; }
        public void Tick(int id, ref MotionState state, float dt, ref Vec3 desired) => _inner.Tick(id, ref state, dt, ref desired);
        public void Cancel() => _inner.Cancel();
        public bool TryClaimCollision()
        {
            if (_hitClaimed) return false;
            _hitClaimed = true;
            Cancel();
            _owner?.End(ContinuousEndReason.Completed);
            return true;
        }
        public bool ExportSnapshot(out MotionSourceSnapshot snapshot)
        {
            var result = _inner.ExportSnapshot(out snapshot);
            snapshot.Index = _hitClaimed ? 1 : 0;
            return result;
        }
        public bool ImportSnapshot(in MotionSourceSnapshot snapshot)
        {
            _hitClaimed = snapshot.Index != 0;
            return _inner.ImportSnapshot(in snapshot);
        }
    }
}
