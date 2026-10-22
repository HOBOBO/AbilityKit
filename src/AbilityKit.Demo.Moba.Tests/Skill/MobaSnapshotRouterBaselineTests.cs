using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaSnapshotRouterBaselineTests
{
    [Fact]
    public void CollectSnapshots_replays_requested_baseline_once()
    {
        var emitter = new BaselineEmitter(701, new byte[] { 7, 1 });
        var router = CreateRouter(emitter);
        var snapshots = new List<WorldStateSnapshot>();

        Assert.True(router.RequestActiveBaseline());
        Assert.Equal(1, router.CollectSnapshots(new FrameIndex(3), snapshots));
        var snapshot = Assert.Single(snapshots);
        Assert.Equal(701, snapshot.OpCode);
        Assert.Equal(new byte[] { 7, 1 }, snapshot.Payload);
        Assert.Equal(1, emitter.ConsumeCount);

        snapshots.Clear();
        Assert.Equal(0, router.CollectSnapshots(new FrameIndex(3), snapshots));
        Assert.Equal(1, emitter.ConsumeCount);
    }

    [Fact]
    public void TryGetSnapshot_replays_requested_baseline_once()
    {
        var emitter = new BaselineEmitter(702, new byte[] { 7, 2 });
        var router = CreateRouter(emitter);

        Assert.True(router.RequestActiveBaseline());
        Assert.True(router.TryGetSnapshot(new FrameIndex(4), out var snapshot));
        Assert.Equal(702, snapshot.OpCode);
        Assert.Equal(1, emitter.ConsumeCount);

        Assert.False(router.TryGetSnapshot(new FrameIndex(4), out _));
        Assert.Equal(1, emitter.ConsumeCount);
    }

    private static MobaSnapshotRouter CreateRouter(IMobaSnapshotEmitter emitter)
    {
        var router = new MobaSnapshotRouter();
        var field = typeof(MobaSnapshotRouter).GetField("_emitters", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(router, new List<IMobaSnapshotEmitter> { emitter });
        return router;
    }

    private sealed class BaselineEmitter : IMobaSnapshotEmitter, IActiveBaselineSnapshotProvider
    {
        private readonly WorldStateSnapshot _snapshot;
        private bool _requested;

        public BaselineEmitter(int opCode, byte[] payload)
        {
            _snapshot = new WorldStateSnapshot(opCode, payload);
        }

        public int ConsumeCount { get; private set; }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        public void RequestActiveBaseline() => _requested = true;

        public bool ConsumeActiveBaselineRequest()
        {
            if (!_requested) return false;
            _requested = false;
            ConsumeCount++;
            return true;
        }

        public bool TryGetActiveBaseline(out WorldStateSnapshot snapshot)
        {
            snapshot = _snapshot;
            return true;
        }
    }
}
