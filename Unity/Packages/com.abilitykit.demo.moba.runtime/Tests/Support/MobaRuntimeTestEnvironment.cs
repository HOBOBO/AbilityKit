using System;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Triggering.Testing;

namespace AbilityKit.Demo.Moba.Testing
{
    /// <summary>
    /// 在编辑器测试中组合 MOBA 配置、触发器夹具和战斗脚本驱动，不参与游戏运行时装配。
    /// </summary>
    public sealed class MobaRuntimeTestEnvironment<TCtx> : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// 创建测试环境；传入的触发器夹具也由该环境负责释放。
        /// </summary>
        public MobaRuntimeTestEnvironment(
            TCtx context = default(TCtx),
            MobaConfigDatabase configDatabase = null,
            TriggeringTestHarness<TCtx> triggering = null,
            BattleTestScriptRunner battleRunner = null)
        {
            ConfigBuilder = new MobaTestConfigBuilder();
            ConfigDatabase = configDatabase ?? new MobaConfigDatabase();
            Triggering = triggering ?? new TriggeringTestHarness<TCtx>(context);
            BattleRunner = battleRunner ?? new BattleTestScriptRunner();
        }

        /// <summary>
        /// 提供用于下一次加载的测试配置构建器。
        /// </summary>
        public MobaTestConfigBuilder ConfigBuilder { get; }

        /// <summary>
        /// 返回当前配置数据库，调用 LoadConfig 后替换为新数据库。
        /// </summary>
        public MobaConfigDatabase ConfigDatabase { get; private set; }

        /// <summary>
        /// 返回由测试环境持有的触发器夹具。
        /// </summary>
        public TriggeringTestHarness<TCtx> Triggering { get; }

        /// <summary>
        /// 返回用于执行测试脚本的运行器。
        /// </summary>
        public BattleTestScriptRunner BattleRunner { get; }

        /// <summary>
        /// 读取或更新触发器夹具下一次取用的宿主上下文。
        /// </summary>
        public TCtx Context
        {
            get => Triggering.Context;
            set => Triggering.SetContext(value);
        }

        /// <summary>
        /// 更新夹具的宿主上下文并返回当前环境；释放后不可调用。
        /// </summary>
        public MobaRuntimeTestEnvironment<TCtx> SetContext(TCtx context)
        {
            ThrowIfDisposed();
            Triggering.SetContext(context);
            return this;
        }

        /// <summary>
        /// 构建并替换当前测试数据库；严格模式由构建器执行。
        /// </summary>
        public MobaRuntimeTestEnvironment<TCtx> LoadConfig(bool strict = false)
        {
            ThrowIfDisposed();
            ConfigDatabase = ConfigBuilder.BuildDatabase(strict);
            return this;
        }

        /// <summary>
        /// 用指定驱动执行测试脚本；脚本和驱动均不得为空。
        /// </summary>
        public BattleTestScriptRunResult RunBattleScript(BattleTestScript script, IBattleTestScriptDriver driver)
        {
            ThrowIfDisposed();
            if (script == null) throw new ArgumentNullException(nameof(script));
            if (driver == null) throw new ArgumentNullException(nameof(driver));

            return BattleRunner.Run(script, driver);
        }

        /// <summary>
        /// 释放触发器夹具；重复调用无操作。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Triggering.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MobaRuntimeTestEnvironment<TCtx>));
            }
        }
    }
}
