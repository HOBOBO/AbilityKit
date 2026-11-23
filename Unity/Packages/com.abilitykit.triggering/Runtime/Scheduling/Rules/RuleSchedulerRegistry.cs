using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.RuleScheduler
{
    /// <summary>
    /// 宿主内的业务规则调度驱动注册表，负责路由而不统一保存驱动状态。
    /// 不驱动扁平动作或执行树，也不提供默认的回滚快照与跨驱动排序保证。
    /// </summary>
    public sealed class RuleSchedulerRegistry
    {
        private readonly Dictionary<string, IRuleSchedulerDriver> _drivers = new Dictionary<string, IRuleSchedulerDriver>(StringComparer.Ordinal);

        /// <summary>
        /// 省略驱动 ID 时采用的注册键。
        /// </summary>
        public string DefaultDriverId { get; private set; }

        /// <summary>
        /// 当前已注册的驱动 ID 数量。
        /// </summary>
        public int DriverCount => _drivers.Count;

        /// <summary>
        /// 注册指定的默认驱动；未提供时创建框架默认驱动。
        /// </summary>
        public RuleSchedulerRegistry(IRuleSchedulerDriver defaultDriver = null)
        {
            RegisterDriver(defaultDriver ?? new DefaultRuleSchedulerDriver(), setAsDefault: true);
        }

        /// <summary>
        /// 按非空 ID 注册驱动。同名注册会替换映射，不迁移或清理旧驱动的任务。
        /// </summary>
        public void RegisterDriver(IRuleSchedulerDriver driver, bool setAsDefault = false)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (string.IsNullOrEmpty(driver.DriverId)) throw new ArgumentException("Rule scheduler driver id cannot be empty.", nameof(driver));

            _drivers[driver.DriverId] = driver;
            if (setAsDefault || string.IsNullOrEmpty(DefaultDriverId))
            {
                DefaultDriverId = driver.DriverId;
            }
        }

        /// <summary>
        /// 按 ID 查询驱动；空 ID 使用默认驱动，未知 ID 返回否。
        /// </summary>
        public bool TryGetDriver(string driverId, out IRuleSchedulerDriver driver)
        {
            return _drivers.TryGetValue(NormalizeDriverId(driverId), out driver);
        }

        /// <summary>
        /// 获取驱动；空 ID 使用默认驱动，未知 ID 会被拒绝。
        /// </summary>
        public IRuleSchedulerDriver GetDriver(string driverId = null)
        {
            if (TryGetDriver(driverId, out var driver)) return driver;
            throw new InvalidOperationException($"Rule scheduler driver not registered: {NormalizeDriverId(driverId)}");
        }

        /// <summary>
        /// 把规则计划和效果交给选定驱动，由驱动验证、保存并返回句柄。
        /// </summary>
        public RuleScheduleHandle Schedule(in RuleSchedulePlan plan, IRuleScheduleEffect effect, string driverId = null)
        {
            return GetDriver(driverId).Schedule(in plan, effect);
        }

        /// <summary>
        /// 路由暂停请求；无效句柄或驱动不存在时返回否。
        /// </summary>
        public bool Pause(RuleScheduleHandle handle) => handle.IsValid && TryGetDriver(handle.DriverId, out var driver) && driver.Pause(handle);

        /// <summary>
        /// 路由恢复请求；无效句柄或驱动不存在时返回否。
        /// </summary>
        public bool Resume(RuleScheduleHandle handle) => handle.IsValid && TryGetDriver(handle.DriverId, out var driver) && driver.Resume(handle);

        /// <summary>
        /// 路由带可选原因的中断请求，由驱动决定状态是否允许中断。
        /// </summary>
        public bool Interrupt(RuleScheduleHandle handle, string reason = null) => handle.IsValid && TryGetDriver(handle.DriverId, out var driver) && driver.Interrupt(handle, reason);

        /// <summary>
        /// 路由取消请求，由驱动决定状态是否允许取消。
        /// </summary>
        public bool Cancel(RuleScheduleHandle handle) => handle.IsValid && TryGetDriver(handle.DriverId, out var driver) && driver.Cancel(handle);

        /// <summary>
        /// 将宿主提供的毫秒增量和本次上下文原样传给各驱动。
        /// 注册表不捕获驱动抛出的异常；后续驱动可能未推进。
        /// </summary>
        public void Update(float deltaTimeMs, object userContext = null)
        {
            foreach (var driver in _drivers.Values)
            {
                driver.Update(deltaTimeMs, userContext);
            }
        }

        /// <summary>
        /// 让当前驱动清理各自状态；不移除注册映射或默认驱动 ID。
        /// </summary>
        public void Clear()
        {
            foreach (var driver in _drivers.Values)
            {
                driver.Clear();
            }
        }

        private string NormalizeDriverId(string driverId)
        {
            return string.IsNullOrEmpty(driverId) ? DefaultDriverId : driverId;
        }
    }
}
