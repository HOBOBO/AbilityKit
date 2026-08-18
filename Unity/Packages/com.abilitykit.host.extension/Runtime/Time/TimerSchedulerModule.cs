using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;
using AbilityKit.Timer;

namespace AbilityKit.Ability.Host.Extensions.Time
{
    /// <summary>
    /// 把 timer 的 IScheduler 作为每世界受驱服务接入 Host 运行时。
    /// 每个世界在创建时注册一个 DefaultScheduler（一世界一份），Host 的 PostTick 驱动
    /// scheduler.Tick(deltaTime)，避免各包各自 Tick 不受管理。
    ///
    /// 确定性边界：当前由 PostTick 的宿主墙钟 dt 驱动；确定性世界接入时应改用
    /// FrameSyncDriverEvents.AddPostStep 的帧驱动路径（固定步长 dt），见后续消费者迁移。
    /// </summary>
    public sealed class TimerSchedulerModule : IHostRuntimeModule
    {
        private readonly Dictionary<WorldId, DefaultScheduler> _schedulers = new Dictionary<WorldId, DefaultScheduler>();

        private readonly Action<WorldCreateOptions> _onBeforeCreateWorld;
        private readonly Action<WorldId> _onWorldDestroyed;
        private readonly Action<float> _onPostTick;

        public TimerSchedulerModule()
        {
            _onBeforeCreateWorld = OnBeforeCreateWorld;
            _onWorldDestroyed = OnWorldDestroyed;
            _onPostTick = OnPostTick;
        }

        /// <summary>按 worldId 取该世界的 scheduler（模块自有查询，便于测试与诊断）。</summary>
        public bool TryGet(WorldId worldId, out IScheduler scheduler)
        {
            if (_schedulers.TryGetValue(worldId, out var s) && s != null)
            {
                scheduler = s;
                return true;
            }

            scheduler = null!;
            return false;
        }

        public void Install(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.BeforeCreateWorld.Add(_onBeforeCreateWorld);
            options.WorldDestroyed.Add(_onWorldDestroyed);
            options.PostTick.Add(_onPostTick);
        }

        public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.BeforeCreateWorld.Remove(_onBeforeCreateWorld);
            options.WorldDestroyed.Remove(_onWorldDestroyed);
            options.PostTick.Remove(_onPostTick);

            _schedulers.Clear();
        }

        private void OnBeforeCreateWorld(WorldCreateOptions options)
        {
            if (options == null) return;

            if (options.ServiceBuilder == null)
            {
                options.ServiceBuilder = WorldServiceContainerFactory.CreateDefaultOnly();
            }

            if (!_schedulers.TryGetValue(options.Id, out var scheduler) || scheduler == null)
            {
                scheduler = new DefaultScheduler();
                _schedulers[options.Id] = scheduler;
            }

            options.ServiceBuilder.RegisterInstance<IScheduler>(scheduler);
        }

        private void OnWorldDestroyed(WorldId worldId)
        {
            _schedulers.Remove(worldId);
        }

        private void OnPostTick(float deltaTime)
        {
            foreach (var kv in _schedulers)
            {
                kv.Value?.Tick(deltaTime);
            }
        }
    }
}
