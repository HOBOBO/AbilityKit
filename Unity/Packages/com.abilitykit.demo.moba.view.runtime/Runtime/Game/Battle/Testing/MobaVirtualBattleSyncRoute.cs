#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Snapshot;
using AbilityKit.Network.Runtime.Conditioning;

namespace AbilityKit.Game.Battle.Testing
{
    /// <summary>
    /// Connects socket-free virtual frames to the same snapshot route used by a live battle session.
    /// The caller owns the context and session and can bind a real prediction driver to the context.
    /// </summary>
    public sealed class MobaVirtualBattleSyncRoute : IDisposable
    {
        private readonly BattleContext _context;
        private readonly BattleLogicSession _session;
        private readonly SnapshotRoutingInstance _routing;
        private readonly BattleSyncFeature _syncFeature;
        private readonly GamePhaseContext _phase;
        private readonly long _bindingGeneration;
        private bool _attached;
        private bool _sessionSubscribed;
        private bool _disposed;

        public MobaVirtualBattleSyncRoute(BattleContext context, BattleLogicSession session)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (context.FrameSnapshots != null)
                throw new InvalidOperationException("BattleContext already has snapshot routing bound.");

            var catalog = new SnapshotRegistryCatalog()
                .Add("shared", SharedSnapshotRegistry.RegisterAll);
            _routing = SnapshotRoutingBuilder.Build(context, catalog);
            _bindingGeneration = context.BindSnapshotRouting(
                _routing.Snapshots!,
                _routing.Pipeline,
                _routing.CmdHandler);

            var features = new VirtualFeatureStore();
            features.Set(context);
            _phase = new GamePhaseContext(null, features, null);
            _syncFeature = new BattleSyncFeature();

            try
            {
                _syncFeature.OnAttach(in _phase);
                _attached = true;
                _session.FrameReceived += OnFrameReceived;
                _sessionSubscribed = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public MobaVirtualFrameSessionRunResult Run(
            VirtualNetworkScenarioPlan plan,
            Func<VirtualNetworkCommand, FramePacket> resolveFrame,
            int seed = 0,
            long timeoutMs = 30_000,
            Func<FramePacket, ArraySegment<byte>>? serializeFrame = null,
            Action<long>? advanceSimulationByMs = null,
            Func<string>? captureFinalState = null,
            Func<string>? captureSimulationState = null)
        {
            ThrowIfDisposed();
            return new MobaVirtualFrameSessionRunner().Run(
                _session.InjectRemoteFrame,
                plan,
                resolveFrame,
                seed,
                timeoutMs,
                serializeFrame,
                advanceSimulationByMs,
                captureFinalState,
                captureSimulationState);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_sessionSubscribed)
            {
                _session.FrameReceived -= OnFrameReceived;
                _sessionSubscribed = false;
            }

            if (_attached)
            {
                _syncFeature.OnDetach(in _phase);
                _attached = false;
            }

            _context.ClearSnapshotRouting(_bindingGeneration, _routing.Snapshots!);
            _routing.Dispose();
        }

        private void OnFrameReceived(FramePacket packet)
        {
            _routing.Snapshots!.Feed(packet);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MobaVirtualBattleSyncRoute));
        }

        private sealed class VirtualFeatureStore : IGameFeatureStore
        {
            private readonly Dictionary<Type, object> _features = new Dictionary<Type, object>();

            public bool TryGet<T>(out T component) where T : class
            {
                if (_features.TryGetValue(typeof(T), out var value))
                {
                    component = (T)value;
                    return true;
                }

                component = default!;
                return false;
            }

            public void Set<T>(T component) where T : class =>
                _features[typeof(T)] = component;

            public void Remove<T>() where T : class =>
                _features.Remove(typeof(T));

            public void Remove(Type componentType) =>
                _features.Remove(componentType);
        }
    }
}
