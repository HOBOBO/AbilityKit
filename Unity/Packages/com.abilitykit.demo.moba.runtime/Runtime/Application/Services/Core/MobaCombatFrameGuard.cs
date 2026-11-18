using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Services.Area;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 帧完成与故障状态；只约束当前会话，不进入回滚快照。
    /// 失败后必须重建会话，恢复检查点不能将失败世界重新启用。
    /// </summary>
    [WorldService(typeof(MobaCombatFrameGuard), WorldLifetime.Scoped)]
    [WorldService(typeof(IWorldFrameGuard), WorldLifetime.Scoped)]
    public sealed class MobaCombatFrameGuard : IService, IWorldFrameGuard
    {
        [WorldInject] private IProjectileService _projectiles;
        [WorldInject] private MobaAreaRuntimeService _areas;
        [WorldInject] private IFrameTime _time;
        private bool _executing;
        private Exception _failure;

        /// <summary>
        /// 首次失败原因；不被后续失败覆盖。
        /// </summary>
        public Exception Failure => _failure;

        /// <summary>
        /// 失败和正在执行的世界均不能再次进入。
        /// </summary>
        public bool CanAdvance => _failure == null && !_executing;

        /// <summary>
        /// 动态核对全部临时通知及本帧已到期区域延迟；未来任务不影响完成。
        /// </summary>
        public bool CanCommit => CanAdvance &&
            _projectiles is IProjectilePendingNotifications pending && pending.PendingNotificationCount == 0 &&
            !_areas.HasDueDelayAreas(_time.Frame.Value);

        /// <summary>
        /// 进入一帧执行；非法重入也会永久标记为失败。
        /// </summary>
        public void BeginFrame()
        {
            if (!CanAdvance)
            {
                var error = new InvalidOperationException("MOBA world cannot advance; recreate the failed session.", _failure);
                FailFrame(error);
                throw error;
            }
            _executing = true;
        }

        /// <summary>
        /// 原系统树及 Cleanup 完成后验证所有通知已结算；不循环清理树。
        /// </summary>
        public void CompleteFrame()
        {
            if (!_executing) throw new InvalidOperationException("MOBA frame was not started.");
            _executing = false;
            if (CanCommit) return;
            var error = new InvalidOperationException("MOBA frame incomplete: failed settlement or notifications produced after settlement.", _failure);
            FailFrame(error);
            throw error;
        }

        /// <summary>
        /// 保存首次故障，不清除待处理项，不尝试补发或自动恢复。
        /// </summary>
        public void FailFrame(Exception error)
        {
            if (_failure == null) _failure = error ?? new InvalidOperationException("MOBA frame failed.");
            _executing = false;
        }

        /// <summary>
        /// 检查捕获及输出资格；两帧间产生通知同样拒绝。
        /// </summary>
        public void RequireCommit()
        {
            if (!CanCommit) throw new InvalidOperationException("MOBA world has no complete frame to capture or publish.", _failure);
        }

        /// <summary>
        /// 会话释放不执行玩法或通知清理。
        /// </summary>
        public void Dispose() { }
    }
}
