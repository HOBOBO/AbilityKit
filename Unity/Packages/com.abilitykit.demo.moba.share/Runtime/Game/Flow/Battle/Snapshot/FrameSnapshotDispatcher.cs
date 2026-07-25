using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// 帧快照分发器（share 包独立实现）。
    ///
    /// 负责将帧快照数据分发给订阅者。
    ///
    /// **设计定位（2026-07-20 核校）**：这是与
    /// <c>com.abilitykit.world.snapshot</c> / <c>com.abilitykit.demo.moba.view.runtime</c>
    /// 中同名 <c>FrameSnapshotDispatcher</c> **完全独立**的实现，服务于 share 包的
    /// 轻量共享场景：
    /// - 抽象层是 <c>(int frameIndex, T data)</c>，不是
    ///   <c>ISnapshotEnvelope</c>+<c>WorldStateSnapshot</c>。
    /// - 不依赖 <c>BattleLogicSession</c>，可在 .NET 共享端使用（无 Unity 依赖）。
    /// - 提供 <c>DispatchEnterGame / DispatchActorTransform / ...</c> 强类型快捷方法，
    ///   耦合 <c>MobaOpCodes</c>。
    /// - 用 <c>lock</c> 做线程安全（前两份是非线程安全的单线程订阅模型）。
    ///
    /// **不要把本类与 view.runtime 版混淆**：本类被 share 包的纯 .NET 调用方使用，
    /// view.runtime 版被真实 Unity 战斗订阅使用，两者通过不同接口（<c>IFrameSnapshotDispatcher</c>
    /// vs <c>ISnapshotDispatcher</c>）隔离。如果未来要统一，建议保留本类的 .NET 友好
    /// 抽象，把 world.snapshot 框架版的接口延伸到不依赖 <c>ISnapshotEnvelope</c> 的场景。
    /// </summary>
    public sealed class FrameSnapshotDispatcher : IFrameSnapshotDispatcher
    {
        private readonly Dictionary<int, List<SnapshotSubscription>> _subscriptions = new Dictionary<int, List<SnapshotSubscription>>();
        private readonly object _lock = new object();
        private bool _isDisposed;

        /// <summary>
        /// 订阅快照事件
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="opCode">操作码</param>
        /// <param name="handler">处理函数</param>
        /// <returns>订阅句柄，用于取消订阅</returns>
        public IDisposable Subscribe<T>(int opCode, Action<int, T> handler)
        {
            if (_isDisposed) return null;

            var subscription = new SnapshotSubscription(opCode, typeof(T), (frame, data) =>
            {
                if (data is T typedData)
                {
                    handler(frame, typedData);
                }
            });

            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(opCode, out var list))
                {
                    list = new List<SnapshotSubscription>();
                    _subscriptions[opCode] = list;
                }
                list.Add(subscription);
            }

            return subscription;
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <param name="subscription">订阅句柄</param>
        public void Unsubscribe(IDisposable subscription)
        {
            if (subscription == null) return;

            lock (_lock)
            {
                foreach (var kvp in _subscriptions)
                {
                    if (kvp.Value.Remove(subscription as SnapshotSubscription))
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 分发进入游戏事件
        /// </summary>
        public void DispatchEnterGame(int frameIndex, in EnterGameData data)
        {
            Dispatch(frameIndex, MobaOpCodes.Snapshot.EnterGame, data);
        }

        /// <summary>
        /// 分发角色变换事件
        /// </summary>
        public void DispatchActorTransform(int frameIndex, in ActorTransformData[] data)
        {
            Dispatch(frameIndex, MobaOpCodes.Snapshot.ActorTransform, data);
        }

        /// <summary>
        /// 分发弹道事件
        /// </summary>
        public void DispatchProjectileEvent(int frameIndex, in ProjectileEventData[] data)
        {
            Dispatch(frameIndex, MobaOpCodes.Snapshot.ProjectileEvent, data);
        }

        /// <summary>
        /// 分发区域事件
        /// </summary>
        public void DispatchAreaEvent(int frameIndex, in AreaEventData[] data)
        {
            Dispatch(frameIndex, MobaOpCodes.Snapshot.AreaEvent, data);
        }

        /// <summary>
        /// 分发伤害事件
        /// </summary>
        public void DispatchDamageEvent(int frameIndex, in DamageEventData[] data)
        {
            Dispatch(frameIndex, MobaOpCodes.Snapshot.DamageEvent, data);
        }

        /// <summary>
        /// 分发表现 Cue 事件
        /// </summary>
        public void DispatchPresentationCue(int frameIndex, in PresentationCueData[] data)
        {
            Dispatch(frameIndex, MobaOpCodes.Snapshot.PresentationCue, data);
        }

        /// <summary>
        /// 分发状态哈希
        /// </summary>
        public void DispatchStateHash(int frameIndex, in StateHashData data)
        {
            Dispatch(frameIndex, MobaOpCodes.Snapshot.StateHash, data);
        }

        /// <summary>
        /// 分发角色生成事件
        /// </summary>
        public void DispatchActorSpawn(int frameIndex, in ActorSpawnData[] data)
        {
            Dispatch(frameIndex, MobaOpCodes.Snapshot.ActorSpawn, data);
        }

        /// <summary>
        /// 分发快照数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="frameIndex">帧索引</param>
        /// <param name="opCode">操作码</param>
        /// <param name="data">数据</param>
        public void Dispatch<T>(int frameIndex, int opCode, T data)
        {
            if (_isDisposed) return;

            List<SnapshotSubscription> list;
            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(opCode, out list) || list.Count == 0)
                    return;
            }

            // 在 lock 外迭代——约定 handler 不在回调中调用 Unsubscribe（会 deadlock）。
            for (int i = 0; i < list.Count; i++)
            {
                try
                {
                    list[i].Invoke(frameIndex, data);
                }
                catch (Exception ex)
                {
                    LogException(ex, list[i].OpCode);
                }
            }
        }

        /// <summary>
        /// 清空所有订阅
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _subscriptions.Clear();
            }
        }

        /// <summary>
        /// 获取订阅数量
        /// </summary>
        public int GetSubscriptionCount(int opCode)
        {
            lock (_lock)
            {
                if (_subscriptions.TryGetValue(opCode, out var list))
                {
                    return list.Count;
                }
                return 0;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Clear();
        }

        private static void LogException(Exception ex, int opCode)
        {
            System.Diagnostics.Debug.WriteLine($"[FrameSnapshotDispatcher] Exception in handler for opCode {opCode}: {ex}");
        }
    }

    /// <summary>
    /// 快照订阅
    /// </summary>
    internal sealed class SnapshotSubscription : IDisposable
    {
        public int OpCode { get; }
        public Type DataType { get; }
        private readonly Action<int, object> _handler;

        public SnapshotSubscription(int opCode, Type dataType, Action<int, object> handler)
        {
            OpCode = opCode;
            DataType = dataType;
            _handler = handler;
        }

        public void Invoke<T>(int frameIndex, T data)
        {
            _handler(frameIndex, data);
        }

        public void Dispose()
        {
            // 订阅句柄的释放由订阅者负责
        }
    }

}
