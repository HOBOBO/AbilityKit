using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow.Snapshot
{
    /// <summary>
    /// view.runtime 的 <see cref="FrameSnapshotDispatcher"/>：包装框架版
    /// <c>AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher</c>（位于
    /// <c>com.abilitykit.world.snapshot</c>），叠加 session 绑定和诊断日志。
    ///
    /// 委托关系（2026-07-24 重构）：
    /// - 路由注册、订阅管理、Dispatch 逻辑全部委托框架版，消除 ~120 行重复代码。
    /// - 本类只保留 demo 特化：session 生命周期绑定 + OpCode 诊断日志。
    ///
    /// 与 share 版的分工：
    /// - 本类（view.runtime）：session-bound + 诊断装饰，被真实战斗订阅使用。
    /// - share 版：独立设计，基于 (int frameIndex, T data) + lock 线程安全，
    ///   服务于 share 包的轻量共享场景，不依赖 BattleLogicSession。
    /// </summary>
    public sealed class FrameSnapshotDispatcher : ISnapshotDispatcher
    {
        private readonly Core.Snapshots.Routing.FrameSnapshotDispatcher _inner;
        private readonly BattleLogicSession _session;
        private readonly bool _subscribedToSession;

        public FrameSnapshotDispatcher(BattleLogicSession session)
            : this(session, subscribeToSession: true)
        {
        }

        public FrameSnapshotDispatcher(BattleLogicSession session, bool subscribeToSession)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _subscribedToSession = subscribeToSession;
            _inner = new Core.Snapshots.Routing.FrameSnapshotDispatcher();
            _inner.NoRouteForOpCode += opCode =>
                Log.Warning($"[FrameSnapshotDispatcher] No route for OpCode: {opCode}");
            if (subscribeToSession)
            {
                _session.FrameReceived += OnFrame;
            }
        }

        public event Action<ISnapshotEnvelope> FrameReceived
        {
            add => _inner.FrameReceived += value;
            remove => _inner.FrameReceived -= value;
        }

        public event Action<ISnapshotEnvelope, WorldStateSnapshot> SnapshotReceived
        {
            add => _inner.SnapshotReceived += value;
            remove => _inner.SnapshotReceived -= value;
        }

        public delegate bool TryDecode<T>(in WorldStateSnapshot snap, out T value);

        void ISnapshotDecoderRegistry.RegisterDecoder<T>(int opCode, ISnapshotDecoderRegistry.TryDecode<T> decoder)
        {
            ((ISnapshotDecoderRegistry)_inner).RegisterDecoder(opCode, decoder);
        }

        public void Register<T>(int opCode, TryDecode<T> decoder)
        {
            _inner.Register<T>(opCode, (in WorldStateSnapshot snap, out T value) => decoder(in snap, out value));
        }

        public IDisposable Subscribe<T>(int opCode, Action<ISnapshotEnvelope, T> handler)
        {
            return _inner.Subscribe(opCode, handler);
        }

        public void Dispose()
        {
            try
            {
                if (_subscribedToSession)
                {
                    _session.FrameReceived -= OnFrame;
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }

            _inner.Dispose();
        }

        public void Feed(ISnapshotEnvelope envelope)
        {
            _inner.Feed(envelope);
        }

        private void OnFrame(FramePacket packet)
        {
            OnEnvelope(packet);
        }

        private void OnEnvelope(ISnapshotEnvelope envelope)
        {
            if (!envelope.Snapshot.HasValue) return;
            var snap = envelope.Snapshot.Value;

            Log.Info($"[FrameSnapshotDispatcher] Received OpCode: {snap.OpCode}");

            _inner.Feed(envelope);
        }
    }
}
