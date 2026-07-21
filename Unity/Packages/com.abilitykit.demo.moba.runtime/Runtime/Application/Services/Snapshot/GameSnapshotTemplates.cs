using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Services.Templates;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Services.Snapshot
{
    public abstract class LogicWorldSnapshotEmitterBase<TService> : LogicWorldServiceBase<TService>, AbilityKit.Demo.Moba.Services.IMobaSnapshotEmitter
        where TService : class
    {
        private FrameIndex _lastFrame;

        protected LogicWorldSnapshotEmitterBase()
        {
            ResetFrameGuard();
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            ThrowIfDisposed();

            if (!CanEmit(frame))
            {
                snapshot = default;
                return false;
            }

            if (UseFrameGuard && frame.Value == _lastFrame.Value)
            {
                snapshot = default;
                return false;
            }

            if (!TryBuildSnapshot(frame, out snapshot))
            {
                return false;
            }

            _lastFrame = frame;
            return true;
        }

        protected virtual bool CanEmit(FrameIndex frame)
        {
            return true;
        }

        protected virtual bool UseFrameGuard => true;

        protected abstract bool TryBuildSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot);

        protected void ResetFrameGuard()
        {
            _lastFrame = new FrameIndex(-999999);
        }

        protected override void OnDispose()
        {
            ResetFrameGuard();
        }
    }

    public abstract class LogicWorldSnapshotBufferEmitterBase<TService, TEntry> : LogicWorldSnapshotEmitterBase<TService>
        where TService : class
    {
        private readonly MobaSnapshotBuffer<TEntry> _buffer;

        protected LogicWorldSnapshotBufferEmitterBase(int initialCapacity, int maxRetainedCapacity)
        {
            _buffer = new MobaSnapshotBuffer<TEntry>(initialCapacity, maxRetainedCapacity);
        }

        protected int Count => _buffer.Count;

        protected override bool UseFrameGuard => false;

        protected void Add(TEntry entry)
        {
            _buffer.Add(entry);
        }

        protected void Clear()
        {
            _buffer.Clear();
        }

        protected int CopyTo(IList<TEntry> destination)
        {
            return _buffer.CopyTo(destination);
        }

        protected int DrainTo(IList<TEntry> destination)
        {
            return _buffer.DrainTo(destination);
        }

        protected TEntry[] ToArrayClearAndTrim()
        {
            return _buffer.ToArrayClearAndTrim();
        }

        protected override bool TryBuildSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (_buffer.Count == 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = CreateSnapshot(_buffer.ToArrayClearAndTrim());
            return true;
        }

        protected abstract WorldStateSnapshot CreateSnapshot(TEntry[] entries);

        protected override void OnDispose()
        {
            _buffer.ClearAndTrim();
            base.OnDispose();
        }
    }

    public abstract class LogicWorldSnapshotRouterBase<TEmitter> : LogicWorldInitializableServiceBase<LogicWorldSnapshotRouterBase<TEmitter>>, IWorldStateSnapshotProvider
        where TEmitter : class, AbilityKit.Demo.Moba.Services.IMobaSnapshotEmitter
    {
        private List<TEmitter> _emitters = new List<TEmitter>(8);

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            ThrowIfDisposed();

            for (int i = 0; i < _emitters.Count; i++)
            {
                if (_emitters[i].TryGetSnapshot(frame, out snapshot)) return true;
            }

            snapshot = default;
            return false;
        }

        protected void SetEmitters(List<TEmitter> emitters)
        {
            _emitters = emitters ?? new List<TEmitter>(0);
        }

        protected IReadOnlyList<TEmitter> Emitters => _emitters;
    }

    public abstract class GameSnapshotServiceBase<TService> : LogicWorldSnapshotEmitterBase<TService>
        where TService : class
    {
    }

    public abstract class GameSnapshotRouterBase<TEmitter> : LogicWorldSnapshotRouterBase<TEmitter>
        where TEmitter : class, AbilityKit.Demo.Moba.Services.IMobaSnapshotEmitter
    {
    }
}
