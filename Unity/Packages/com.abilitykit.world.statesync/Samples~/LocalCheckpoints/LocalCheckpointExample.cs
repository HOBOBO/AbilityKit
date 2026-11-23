using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Ability.StateSync.Snapshot;

namespace AbilityKit.Ability.StateSync.Samples
{
    /// <summary>
    /// 演示捕获、恢复和淘汰本地检查点，不依赖 Unity 场景或传输。
    /// </summary>
    public static class LocalCheckpointExample
    {
        /// <summary>
        /// 执行一次固定的离线流程，并返回恢复与淘汰是否符合预期。
        /// </summary>
        public static bool Run()
        {
            var history = new SnapshotBuffer(maxBufferSize: 2);
            var checkpoints = new StateManager(history);
            var actor = new ExampleActor { Position = new Vec3(3f, 0f, 0f) };
            checkpoints.RegisterRollbackable(actor);
            checkpoints.CaptureState(10);

            actor.Position = new Vec3(6f, 0f, 0f);
            checkpoints.CaptureState(11);
            if (!checkpoints.TryRestore(10) || actor.Position.X != 3f)
                return false;

            checkpoints.CaptureState(12);
            var frames = checkpoints.GetCapturedFrames();
            return !checkpoints.TryRestore(10) && frames.Count == 2 && frames[0] == 11 && frames[1] == 12;
        }

        private sealed class ExampleActor : IRollbackable
        {
            internal Vec3 Position;

            public long EntityId => 1;
            public int SnapshotKey => 1;

            public IRollbackState CreateRollbackState() =>
                new EntityRollbackState(EntityId) { position = Position };

            public void RestoreFromRollbackState(IRollbackState state) =>
                Position = ((EntityRollbackState)state).position;
        }
    }
}
