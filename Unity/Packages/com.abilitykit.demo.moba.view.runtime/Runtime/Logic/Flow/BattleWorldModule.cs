using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Game.Flow.Dungeons;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Step 1 骨架：把"战斗阶段"内的服务集合声明为一个可被 world scope 接管的 <see cref="IWorldModule"/>。
    ///
    /// 设计意图（见 MobaFlowSpec.md "Flow ↔ World Scope" 一节）：
    /// - 战斗阶段服务全部注册成 <see cref="WorldLifetime.Scoped"/>，由 per-battle 的 <see cref="WorldScope"/> 缓存。
    /// - 进入战斗时建 scope、退出时 <c>scope.Dispose()</c>，scoped 实例（含 <see cref="IDisposable"/>）自动清理，
    ///   替换掉当前 <c>GameFlowDomain</c> 里靠字段 + flag 手工管理的 battle 生命周期。
    /// - 本步骤只验证"compose / resolve / dispose / 隔离"闭环成立，<b>暂不接入 flow</b>。
    ///
    /// 注意：本类型刻意保持纯 C#（不引用 UnityEngine），以便镜像进桌面 xUnit 工程做回归。
    /// </summary>
    public sealed class BattleWorldModule : IWorldModule
    {
        /// <inheritdoc />
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            // 战斗会话态：每个 battle scope 一个实例，退出战斗时随 scope.Dispose() 释放。
            builder.Register<IBattleScopedSession>(WorldLifetime.Scoped, _ => new BattleScopedSession());

            // 战斗运行态标志（Step 2 后续刀）：原 GameFlowDomain._battleSessionStarted /
            // _battleFirstFrameReceived 两个字段迁来；每局新 scope 默认 false，退出随 scope 释放。
            builder.Register<IBattleRuntimeState>(WorldLifetime.Scoped, _ => new BattleRuntimeState());

            // 战斗准入 gate 来源（Step 3）：原 GameFlowDomain 四个硬编码 return true 的 gate 方法迁来。
            // 默认实现四项全 true（零行为变化），后续接入真实判定时换 scope 内实现即可。
            builder.Register<IFlowGateProvider>(WorldLifetime.Scoped, _ => new DefaultFlowGateProvider());

            // 副本扩展注册中心（玩法处理器/场景装配/胜负规则/端口组）。
            // 注册中心本身无副本状态，因此以 Scoped 注册以保证每局独立且可测试。
            builder.Register<MobaDungeonRegistry>(WorldLifetime.Scoped, _ => MobaDungeonRegistry.CreateStandard());

            // 副本运行态：本局玩家数与已装配的场景句柄，随 scope 生命周期。
            builder.Register<MobaDungeonRuntimeState>(WorldLifetime.Scoped, _ => new MobaDungeonRuntimeState());

            // 依赖会话态的阶段服务（构造注入示例）：证明 scope 内依赖解析与共享实例语义。
            builder.Register<IBattleScopedClock>(
                WorldLifetime.Scoped,
                r => new BattleScopedClock(r.Resolve<IBattleScopedSession>()));
        }
    }

    /// <summary>战斗会话态服务（per-battle-scope）。承载阶段内的可释放资源占位。</summary>
    public interface IBattleScopedSession
    {
        /// <summary>该实例是否已被释放（用于验证 scope.Dispose 的清理语义）。</summary>
        bool Disposed { get; }

        /// <summary>实例的唯一序号，用于验证"同 scope 共享、跨 scope 隔离"。</summary>
        int InstanceId { get; }
    }

    /// <summary>战斗时钟服务，依赖 <see cref="IBattleScopedSession"/>，演示 scope 内构造注入。</summary>
    public interface IBattleScopedClock
    {
        /// <summary>持有的会话实例（应与同 scope 内解析到的 session 为同一引用）。</summary>
        IBattleScopedSession Session { get; }
    }

    internal sealed class BattleScopedSession : IBattleScopedSession, IDisposable
    {
        private static int _nextId;

        public BattleScopedSession()
        {
            InstanceId = System.Threading.Interlocked.Increment(ref _nextId);
        }

        public bool Disposed { get; private set; }

        public int InstanceId { get; }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    internal sealed class BattleScopedClock : IBattleScopedClock
    {
        public BattleScopedClock(IBattleScopedSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public IBattleScopedSession Session { get; }
    }
}
