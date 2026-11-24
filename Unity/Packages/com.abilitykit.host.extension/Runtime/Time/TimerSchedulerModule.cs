#nullable enable
using System;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Timer;

namespace AbilityKit.Ability.Host.Extensions.Time
{
    /// <summary>
    /// 在世界创建前向服务构建器注册调度器，由宿主帧尾按传入的时间增量推进。
    /// 不提供确定性帧推进或回滚能力。
    /// </summary>
    public sealed class TimerSchedulerModule : IHostRuntimeModule
    {
        private readonly HostedWorldService<IScheduler> _schedulers = new HostedWorldService<IScheduler>(() => new DefaultScheduler());
        private HostRuntime? _runtime;

        private readonly Action<WorldCreateOptions> _onBeforeCreateWorld;
        private readonly Action<float> _onPostTick;

        /// <summary>
        /// 创建独立的调度器模块实例。
        /// </summary>
        public TimerSchedulerModule()
        {
            _onBeforeCreateWorld = OnBeforeCreateWorld;
            _onPostTick = OnPostTick;
        }

        /// <summary>
        /// 查询仍已登记、且绑定到本次安装的世界调度器。
        /// </summary>
        public bool TryGet(WorldId worldId, out IScheduler scheduler)
        {
            if (_runtime?.Worlds.TryGet(worldId, out var world) == true)
                return _schedulers.TryGet(world, out scheduler);

            scheduler = null!;
            return false;
        }

        /// <summary>
        /// 注册世界服务装配与宿主帧尾回调。
        /// </summary>
        public void Install(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _runtime = runtime;
            options.BeforeCreateWorld.Add(_onBeforeCreateWorld);
            options.PostTick.Add(_onPostTick);
        }

        /// <summary>
        /// 取消宿主回调并断开本次安装的调度器绑定。
        /// </summary>
        public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.BeforeCreateWorld.Remove(_onBeforeCreateWorld);
            options.PostTick.Remove(_onPostTick);

            _runtime = null;
            _schedulers.Detach();
        }

        private void OnBeforeCreateWorld(WorldCreateOptions options)
        {
            if (options != null) _schedulers.Register(options);
        }

        private void OnPostTick(float deltaTime)
        {
            if (_runtime == null) return;
            foreach (var world in _runtime.Worlds.Worlds.Values)
            {
                if (_schedulers.TryGet(world, out var scheduler)) scheduler.Tick(deltaTime);
            }
        }
    }
}
