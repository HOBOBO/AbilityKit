#nullable enable
using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.Time
{
    /// <summary>
    /// 将每世界的帧时间服务接入 Host，优先跟随帧同步驱动，且不推进被世界帧守卫拒绝的帧。
    /// </summary>
    public sealed class ServerFrameTimeModule : IHostRuntimeModule
    {
        private readonly HostedWorldService<IFrameTime> _times;

        private readonly bool _advanceOnHostTickFallback;

        private readonly Action<WorldCreateOptions> _onBeforeCreateWorld;
        private readonly Action<FrameIndex, float> _onPostStep;

        private readonly Action<float> _onPostTick;

        private IFrameSyncDriverEvents? _frameEvents;
        private HostRuntime? _runtime;

        /// <summary>
        /// 创建帧时间模块，允许没有帧同步驱动时使用 Host 帧尾推进。
        /// </summary>
        public ServerFrameTimeModule()
            : this(0f)
        {
        }

        /// <summary>
        /// 创建帧时间模块。正的固定步长用于初始化世界时钟，不替换驱动传来的时间增量。
        /// </summary>
        /// <param name="fixedDeltaSeconds">时钟的初始固定步长，非正值使用时钟默认设置。</param>
        /// <param name="advanceOnHostTickFallback">安装时无帧同步驱动则是否跟随 Host 帧尾。</param>
        public ServerFrameTimeModule(
            float fixedDeltaSeconds,
            bool advanceOnHostTickFallback = true)
        {
            _times = new HostedWorldService<IFrameTime>(() => CreateTime(fixedDeltaSeconds));
            _advanceOnHostTickFallback = advanceOnHostTickFallback;
            _onBeforeCreateWorld = OnBeforeCreateWorld;
            _onPostStep = OnPostStep;

            _onPostTick = OnPostTick;
        }

        /// <summary>
        /// 查询仍已登记、且绑定到本次安装的世界时钟。
        /// </summary>
        public bool TryGet(WorldId worldId, out IFrameTime time)
        {
            if (_runtime?.Worlds.TryGet(worldId, out var world) == true)
                return _times.TryGet(world, out time);

            time = null!;
            return false;
        }

        /// <summary>
        /// 注册世界创建前的服务装配回调，并依据已安装的帧同步 Feature 选择唯一时间驱动源。
        /// </summary>
        public void Install(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _runtime = runtime;

            _frameEvents = runtime.Features.TryGetFeature<IFrameSyncDriverEvents>(out var frameEvents) ? frameEvents : null;

            options.BeforeCreateWorld.Add(_onBeforeCreateWorld);

            if (_frameEvents != null)
            {
                _frameEvents.AddPostStep(_onPostStep);
            }
            else if (_advanceOnHostTickFallback)
            {
                options.PostTick.Add(_onPostTick);
            }
        }

        /// <summary>
        /// 取消装配和步进订阅，并断开本次安装的时钟绑定，不重置世界已持有的时钟。
        /// </summary>
        public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.BeforeCreateWorld.Remove(_onBeforeCreateWorld);

            options.PostTick.Remove(_onPostTick);

            _frameEvents?.RemovePostStep(_onPostStep);
            _frameEvents = null;
            _runtime = null;
            _times.Detach();
        }

        private void OnBeforeCreateWorld(WorldCreateOptions options)
        {
            if (options != null) _times.Register(options);
        }

        private static FrameTime CreateTime(float fixedDeltaSeconds)
        {
            var time = new FrameTime();
            if (fixedDeltaSeconds > 0f)
                time.Reset(new FrameIndex(0), time: 0f, fixedDelta: fixedDeltaSeconds);
            return time;
        }

        private void OnPostStep(FrameIndex frame, float deltaTime)
        {
            if (_runtime == null) return;
            foreach (var world in _runtime.Worlds.Worlds.Values)
            {
                if (_times.TryGet(world, out var time) && time is FrameTime clock && CanCommitTime(world))
                    clock.StepTo(frame, deltaTime);
            }
        }

        private void OnPostTick(float deltaTime)
        {
            try
            {
                if (_runtime == null) return;
                foreach (var world in _runtime.Worlds.Worlds.Values)
                {
                    if (_times.TryGet(world, out var time) && time is FrameTime clock && CanCommitTime(world))
                        clock.StepTo(new FrameIndex(clock.Frame.Value + 1), deltaTime);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }

        private static bool CanCommitTime(IWorld world)
        {
            return world.Services?.TryResolve<IWorldFrameGuard>(out var guard) != true || guard.CanCommit;
        }
    }
}
