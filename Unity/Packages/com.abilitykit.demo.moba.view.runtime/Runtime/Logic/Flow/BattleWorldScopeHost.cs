#nullable enable
using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Game.Flow.Dungeons;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Step 2 骨架：把 per-battle 的 <see cref="WorldScope"/> 生命周期从 <c>GameFlowDomain</c> 中抽出来，
    /// 由本类型统一承载"进入战斗建 scope / 退出战斗 Dispose"的闭环。
    ///
    /// 设计意图（见 MobaFlowSpec.md "Flow ↔ World Scope" 一节 step 2）：
    /// - flow 进入 battle 时调用 <see cref="BeginBattle"/> 创建 scope；退出时调用 <see cref="EndBattle"/> 释放。
    /// - 本步骤<b>只引入 scope 句柄并对齐生命周期</b>，<c>GameFlowDomain</c> 的 <c>_battle*</c> 字段保持零行为变化；
    ///   后续步骤再把具体服务逐个迁移到 <see cref="Resolve{T}"/>。
    /// - <see cref="BeginBattle"/> 可重入：若已有活跃 scope（异常路径或重复进入），先释放旧 scope 再建新的，
    ///   保证"一场战斗一个 scope"的隔离语义。
    /// - <see cref="EndBattle"/> 异常安全：重复调用、未开始即调用都不抛。
    ///
    /// 注意：本类型刻意保持纯 C#（不引用 UnityEngine），以便镜像进桌面 xUnit 工程做回归。
    /// </summary>
    public sealed class BattleWorldScopeHost : IDisposable
    {
        private readonly WorldContainer _container;
        private WorldScope? _scope;
        private int _scopeGeneration;
        private bool _disposed;

        /// <summary>使用默认 <see cref="BattleWorldModule"/> 构建容器。</summary>
        public BattleWorldScopeHost()
            : this(new BattleWorldModule())
        {
        }

        /// <summary>使用指定模块构建容器（便于测试注入替身模块）。</summary>
        public BattleWorldScopeHost(IWorldModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            _container = new WorldContainerBuilder().AddModule(module).Build();
        }

        /// <summary>当前是否存在活跃的 battle scope。</summary>
        public bool HasActiveScope => _scope != null;

        /// <summary>
        /// scope 代次：每成功创建一个新 scope 自增 1。用于断言"重入会换新 scope"。
        /// </summary>
        public int ScopeGeneration => _scopeGeneration;

        /// <summary>
        /// 进入战斗：创建一个新的 per-battle scope。若已有活跃 scope，先释放旧的再建新的。
        /// </summary>
        public void BeginBattle()
        {
            ThrowIfDisposed();

            // 重入保护：保证一场战斗只对应一个活跃 scope。
            if (_scope != null)
            {
                DisposeScopeSafely();
            }

            _scope = _container.CreateScope();
            _scopeGeneration++;
        }

        /// <summary>
        /// 进入战斗并向新 scope「播种」跨阶段输入（如 per-battle 的 bootstrapper/gateway）。
        /// 与 <see cref="BeginBattle()"/> 共享同样的重入保护：已有活跃 scope 时先释放旧的再建新的。
        ///
        /// 播种实例的生命周期归调用方（flow），<see cref="EndBattle"/> / scope.Dispose 不接管其释放。
        /// </summary>
        public void BeginBattle(Action<IWorldScopeSeeder> configure)
        {
            ThrowIfDisposed();

            // 重入保护：保证一场战斗只对应一个活跃 scope。
            if (_scope != null)
            {
                DisposeScopeSafely();
            }

            _scope = _container.CreateScope(configure);
            _scopeGeneration++;
        }

        /// <summary>
        /// 退出战斗：释放当前 scope（含其中的 scoped <see cref="IDisposable"/> 实例）。
        /// 重复调用或未开始即调用均安全。
        /// </summary>
        public void EndBattle()
        {
            DisposeScopeSafely();
        }

        /// <summary>从当前活跃 scope 解析服务。无活跃 scope 时抛出。</summary>
        public T Resolve<T>()
        {
            ThrowIfDisposed();
            if (_scope == null)
            {
                throw new InvalidOperationException(
                    "BattleWorldScopeHost.Resolve called without an active battle scope. Call BeginBattle first.");
            }

            return _scope.Resolve<T>();
        }

        /// <summary>
        /// 尝试从当前活跃 scope 解析服务。无活跃 scope、或服务未注册/未播种时返回 false（不抛）。
        /// 用于「可缺省」的跨阶段输入取回（如某些局可能没有 bootstrapper，播种侧据此跳过）。
        /// </summary>
        public bool TryResolve<T>(out T instance)
        {
            ThrowIfDisposed();
            if (_scope != null && _scope.TryResolve(typeof(T), out var obj) && obj is T typed)
            {
                instance = typed;
                return true;
            }

            instance = default!;
            return false;
        }

        /// <summary>当前活跃 scope（无则为 null）。仅用于受控的内部协作/测试断言。</summary>
        public WorldScope? CurrentScope => _scope;

        /// <summary>
        /// 把本局玩家数写入副本运行态。仅在创建 scope 的开表回调中调用。
        /// </summary>
        public void SeedPlayerCount(int playerCount)
        {
            if (_scope == null)
            {
                throw new InvalidOperationException(
                    "BattleWorldScopeHost.SeedPlayerCount called without an active battle scope.");
            }

            // 必须改容器自身的 scoped 实例，不能用 Seed 覆盖：播种实例的生命周期归调用方，
            // 不会被 scope.Dispose() 释放，会让副本场景句柄泄漏。
            _scope.Resolve<MobaDungeonRuntimeState>().PlayerCount = playerCount > 0 ? playerCount : 1;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            DisposeScopeSafely();
            _container?.Dispose();
        }

        private void DisposeScopeSafely()
        {
            var scope = _scope;
            _scope = null;
            scope?.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BattleWorldScopeHost));
            }
        }
    }
}
